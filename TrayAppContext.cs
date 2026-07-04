using System.Drawing;
using System.Windows.Forms;

namespace AutoPowerMode;

public sealed class TrayAppContext : ApplicationContext
{
    private static readonly TimeSpan SwitchCooldown = TimeSpan.FromSeconds(10);

    private readonly object _sync = new();
    private readonly SynchronizationContext _uiContext;
    private readonly ConfigService _configService = new();
    private readonly StartupService _startupService = new();
    private readonly PowerPlanManager _powerPlanManager = new();
    private readonly IdleDetector _idleDetector = new();
    private readonly PowerModeTransitionPolicy _transitionPolicy = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu = new();
    private readonly ToolStripMenuItem _statusMenuItem = new();
    private readonly ToolStripMenuItem _currentPlanMenuItem = new();
    private readonly ToolStripMenuItem _versionMenuItem = new();
    private readonly ToolStripMenuItem _idleThresholdMenuItem = new();
    private readonly ToolStripMenuItem _checkIntervalMenuItem = new();
    private readonly ToolStripMenuItem _togglePauseMenuItem = new();
    private readonly ToolStripMenuItem _switchToActiveMenuItem = new();
    private readonly ToolStripMenuItem _switchToIdleMenuItem = new();
    private readonly ToolStripMenuItem _settingsMenuItem = new();
    private readonly ToolStripMenuItem _autoStartMenuItem = new();
    private readonly ToolStripMenuItem _exitMenuItem = new();

    private AppConfig _config;
    private List<PowerPlan> _powerPlans = [];
    private Task? _monitorTask;
    private SettingsForm? _settingsForm;
    private PowerModeState _currentState = PowerModeState.NotConfigured;
    private PowerPlan? _currentPowerPlan;
    private DateTimeOffset _lastSwitchTime = DateTimeOffset.MinValue;
    private bool _isExiting;
    private bool _configSaveFailureShown;

    public TrayAppContext()
    {
        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        Logger.Info("软件启动。");

        _config = _configService.Load();
        InitializePowerPlans();
        SyncStartupRegistration();

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = AppInfo.DisplayName,
            Visible = true,
            ContextMenuStrip = _menu
        };

        _notifyIcon.DoubleClick += (_, _) => OpenSettings();

        BuildTrayMenu();
        RefreshStateFromConfig();
        RefreshMenuItems();

        _monitorTask = Task.Run(() => MonitorLoopAsync(_cancellationTokenSource.Token));
    }

    public void OpenSettingsFromExternalRequest()
    {
        PostToUi(OpenSettings);
    }

    private void InitializePowerPlans()
    {
        _powerPlans = _powerPlanManager.GetPowerPlans();

        if (_powerPlanManager.TryAutoConfigure(_config, _powerPlans))
        {
            SaveConfigWithUserFeedback("保存自动匹配到的电源计划失败。");
        }

        _currentPowerPlan = _powerPlanManager.GetActivePowerPlan();
    }

    private void BuildTrayMenu()
    {
        _statusMenuItem.Enabled = false;
        _currentPlanMenuItem.Enabled = false;
        _versionMenuItem.Enabled = false;
        _idleThresholdMenuItem.Enabled = false;
        _checkIntervalMenuItem.Enabled = false;

        _togglePauseMenuItem.Click += (_, _) => TogglePause();
        _switchToActiveMenuItem.Click += async (_, _) => await SwitchToConfiguredPlanAsync(PowerModeState.Active, manual: true, CancellationToken.None);
        _switchToIdleMenuItem.Click += async (_, _) => await SwitchToConfiguredPlanAsync(PowerModeState.Idle, manual: true, CancellationToken.None);
        _settingsMenuItem.Text = "设置";
        _settingsMenuItem.Click += (_, _) => OpenSettings();
        _autoStartMenuItem.Click += (_, _) => ToggleAutoStart();
        _exitMenuItem.Text = "退出";
        _exitMenuItem.Click += (_, _) => ExitApplication();

        _menu.Opening += (_, _) =>
        {
            _currentPowerPlan = _powerPlanManager.GetActivePowerPlan() ?? _currentPowerPlan;
            RefreshMenuItems();
        };
        _menu.Items.AddRange(
        [
            _versionMenuItem,
            _statusMenuItem,
            _currentPlanMenuItem,
            _idleThresholdMenuItem,
            _checkIntervalMenuItem,
            new ToolStripSeparator(),
            _togglePauseMenuItem,
            _switchToActiveMenuItem,
            _switchToIdleMenuItem,
            _settingsMenuItem,
            _autoStartMenuItem,
            _exitMenuItem
        ]);
    }

    private async Task MonitorLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateOnceAsync(cancellationToken);

                var config = GetConfigSnapshot();
                var delay = TimeSpan.FromSeconds(config.CheckIntervalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Error("后台检测循环异常。", ex);

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private async Task EvaluateOnceAsync(CancellationToken cancellationToken)
    {
        var config = GetConfigSnapshot();

        var isConfigured = IsConfigured(config);
        var idleThreshold = TimeSpan.FromSeconds(config.IdleThresholdSeconds);

        if (config.IsPaused || !isConfigured)
        {
            var inactiveTargetState = _transitionPolicy.Evaluate(
                TimeSpan.Zero,
                idleThreshold,
                activeResumeThreshold: idleThreshold,
                config.IsPaused,
                isConfigured);

            if (inactiveTargetState is not null)
            {
                SetState(inactiveTargetState.Value);
            }

            return;
        }

        var idleTime = _idleDetector.GetIdleTime();
        var targetState = _transitionPolicy.Evaluate(
            idleTime,
            idleThreshold,
            activeResumeThreshold: idleThreshold,
            config.IsPaused,
            isConfigured);

        switch (targetState)
        {
            case PowerModeState.Paused:
            case PowerModeState.NotConfigured:
                SetState(targetState.Value);
                break;
            case PowerModeState.Active:
            case PowerModeState.Idle:
                await SwitchToConfiguredPlanAsync(targetState.Value, manual: false, cancellationToken);
                break;
        }
    }

    private Task SwitchToConfiguredPlanAsync(PowerModeState targetState, bool manual, CancellationToken cancellationToken)
    {
        var config = GetConfigSnapshot();
        var targetGuid = targetState == PowerModeState.Idle
            ? config.IdlePowerPlanGuid
            : config.ActivePowerPlanGuid;

        return SwitchToPlanAsync(targetGuid, targetState, manual, config, cancellationToken);
    }

    private Task SwitchToPlanAsync(string targetGuid, PowerModeState targetState, bool manual, AppConfig config, CancellationToken cancellationToken)
    {
        return Task.Run(
            () =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(targetGuid))
                    {
                        Logger.Error("目标电源计划未配置。");
                        SetState(PowerModeState.NotConfigured);
                        return;
                    }

                    var activePlan = _powerPlanManager.GetActivePowerPlan();
                    if (activePlan is not null)
                    {
                        SetCurrentPowerPlan(activePlan);
                    }

                    if (PowerPlanOverridePolicy.ShouldSkipAutomaticSwitch(manual, activePlan?.Guid, config))
                    {
                        Logger.Info($"检测到外部手动切换到非配置电源计划，跳过本次自动切换：{activePlan?.Name} ({activePlan?.Guid})");
                        return;
                    }

                    if (!manual &&
                        targetState == PowerModeState.Idle &&
                        DateTimeOffset.Now - _lastSwitchTime < SwitchCooldown)
                    {
                        return;
                    }

                    if (string.Equals(activePlan?.Guid, targetGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        SetState(targetState);
                        return;
                    }

                    if (_powerPlanManager.SetActivePlan(targetGuid))
                    {
                        _lastSwitchTime = DateTimeOffset.Now;
                        var plan = _powerPlanManager.FindByGuid(_powerPlans, targetGuid)
                                   ?? _powerPlanManager.GetActivePowerPlan();
                        SetCurrentPowerPlan(plan);
                        SetState(targetState);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("电源计划切换异常。", ex);
                }
            },
            cancellationToken);
    }

    private void TogglePause()
    {
        lock (_sync)
        {
            _config.IsPaused = !_config.IsPaused;
            Logger.Info(_config.IsPaused ? "用户暂停自动切换。" : "用户恢复自动切换。");
        }

        SaveConfigWithUserFeedback("暂停状态保存失败，下次启动可能无法恢复当前暂停状态。");

        RefreshStateFromConfig();
        RefreshMenuItems();

        if (!GetConfigSnapshot().IsPaused)
        {
            QueueEvaluateOnce();
        }
    }

    private void ToggleAutoStart()
    {
        var enabled = _startupService.IsEnabled();
        var targetEnabled = !enabled;

        if (!_startupService.SetEnabled(targetEnabled))
        {
            MessageBox.Show(
                "开机自启设置失败，详情请查看日志。",
                AppInfo.DisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        lock (_sync)
        {
            _config.AutoStart = _startupService.IsEnabled();
        }

        SaveConfigWithUserFeedback("开机自启设置保存失败，下次启动可能无法恢复当前设置。");

        RefreshMenuItems();
    }

    private void OpenSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.WindowState = FormWindowState.Normal;
            _settingsForm.BringToFront();
            _settingsForm.Activate();
            return;
        }

        _powerPlans = _powerPlanManager.GetPowerPlans();
        lock (_sync)
        {
            if (_powerPlanManager.TryAutoConfigure(_config, _powerPlans))
            {
                SaveConfigWithUserFeedback("保存自动匹配到的电源计划失败。");
            }
        }

        var configForForm = GetConfigSnapshot();
        configForForm.AutoStart = _startupService.IsEnabled();

        _settingsForm = new SettingsForm(configForForm, _powerPlans);
        if (_settingsForm.ShowDialog() == DialogResult.OK)
        {
            ApplySettings(_settingsForm.SavedConfig);
        }

        _settingsForm.Dispose();
        _settingsForm = null;
    }

    private void ApplySettings(AppConfig newConfig)
    {
        if (!_startupService.SetEnabled(newConfig.AutoStart))
        {
            MessageBox.Show(
                "开机自启设置失败，已保留当前注册表状态。详情请查看日志。",
                AppInfo.DisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        newConfig.AutoStart = _startupService.IsEnabled();
        if (_powerPlanManager.IsBalancedPlanGuid(_powerPlans, newConfig.ActivePowerPlanGuid))
        {
            _powerPlanManager.TryAutoConfigure(newConfig, _powerPlans);
        }

        newConfig.Normalize();

        lock (_sync)
        {
            _config = newConfig;
        }

        SaveConfigWithUserFeedback("设置保存失败，下次启动可能无法恢复本次修改。", alwaysShowFailure: true);
        Logger.Info("用户修改设置。");
        RefreshStateFromConfig();
        RefreshMenuItems();

        if (!newConfig.IsPaused)
        {
            QueueEvaluateOnce();
        }
    }

    private void QueueEvaluateOnce()
    {
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await EvaluateOnceAsync(_cancellationTokenSource.Token);
                }
                catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
                {
                    // Expected during application shutdown.
                }
                catch (Exception ex)
                {
                    Logger.Error("即时检测任务异常。", ex);
                }
            });
    }

    private void SyncStartupRegistration()
    {
        if (!_startupService.SetEnabled(_config.AutoStart))
        {
            _config.AutoStart = _startupService.IsEnabled();
            SaveConfigWithUserFeedback("同步开机自启状态保存失败。");
        }
        else
        {
            _config.AutoStart = _startupService.IsEnabled();
        }
    }

    private void RefreshStateFromConfig()
    {
        var config = GetConfigSnapshot();
        if (config.IsPaused)
        {
            SetState(PowerModeState.Paused);
        }
        else if (!IsConfigured(config))
        {
            SetState(PowerModeState.NotConfigured);
        }
    }

    private void RefreshMenuItems()
    {
        if (_isExiting)
        {
            return;
        }

        var config = GetConfigSnapshot();
        var state = GetDisplayState(config);
        var currentPowerPlanName = _currentPowerPlan?.Name ?? "未知";
        var autoStartText = _startupService.IsEnabled() ? "已开启" : "已关闭";

        _versionMenuItem.Text = $"版本：{AppInfo.Version}";
        _statusMenuItem.Text = state == PowerModeState.NotConfigured
            ? "当前状态：NotConfigured（电源计划未配置）"
            : $"当前状态：{state}";
        _currentPlanMenuItem.Text = $"当前电源计划：{currentPowerPlanName}";
        _idleThresholdMenuItem.Text = $"空闲阈值：{config.IdleThresholdSeconds} 秒";
        _checkIntervalMenuItem.Text = $"检测间隔：{config.CheckIntervalSeconds} 秒";
        _togglePauseMenuItem.Text = config.IsPaused ? "恢复自动切换" : "暂停自动切换";
        _switchToActiveMenuItem.Text = "立即切换到高性能计划";
        _switchToIdleMenuItem.Text = "立即切换到节能计划";
        _switchToActiveMenuItem.Enabled = _powerPlanManager.FindByGuid(_powerPlans, config.ActivePowerPlanGuid) is not null;
        _switchToIdleMenuItem.Enabled = _powerPlanManager.FindByGuid(_powerPlans, config.IdlePowerPlanGuid) is not null;
        _autoStartMenuItem.Text = $"开机自启：{autoStartText}";

        try
        {
            _notifyIcon.Text = $"{AppInfo.Name} {AppInfo.Version}: {state}";
        }
        catch
        {
            _notifyIcon.Text = AppInfo.DisplayName;
        }
    }

    private PowerModeState GetDisplayState(AppConfig config)
    {
        if (config.IsPaused)
        {
            return PowerModeState.Paused;
        }

        return IsConfigured(config) ? _currentState : PowerModeState.NotConfigured;
    }

    private AppConfig GetConfigSnapshot()
    {
        lock (_sync)
        {
            return _config.Clone();
        }
    }

    private bool SaveConfigWithUserFeedback(string failureMessage, bool alwaysShowFailure = false)
    {
        var saved = _configService.Save(GetConfigSnapshot());
        if (!saved && (alwaysShowFailure || !_configSaveFailureShown))
        {
            _configSaveFailureShown = true;
            MessageBox.Show(
                $"{failureMessage}{Environment.NewLine}{Logger.GetLogLocationMessage()}",
                AppInfo.DisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        return saved;
    }

    private bool IsConfigured(AppConfig config)
    {
        return !string.IsNullOrWhiteSpace(config.IdlePowerPlanGuid)
               && !string.IsNullOrWhiteSpace(config.ActivePowerPlanGuid)
               && _powerPlanManager.FindByGuid(_powerPlans, config.IdlePowerPlanGuid) is not null
               && _powerPlanManager.FindByGuid(_powerPlans, config.ActivePowerPlanGuid) is not null;
    }

    private void SetState(PowerModeState state)
    {
        _currentState = state;
        _transitionPolicy.MarkState(state);
        PostToUi(RefreshMenuItems);
    }

    private void SetCurrentPowerPlan(PowerPlan? powerPlan)
    {
        _currentPowerPlan = powerPlan;
        PostToUi(RefreshMenuItems);
    }

    private void PostToUi(Action action)
    {
        if (_isExiting)
        {
            return;
        }

        try
        {
            _uiContext.Post(_ => action(), null);
        }
        catch (Exception ex)
        {
            Logger.Error("刷新托盘菜单失败。", ex);
        }
    }

    private void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;

        try
        {
            _cancellationTokenSource.Cancel();
            SaveConfigWithUserFeedback("退出前保存配置失败，下次启动可能无法恢复最后一次修改。");
            _monitorTask?.Wait(TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            Logger.Error("退出程序时后台任务取消异常。", ex);
        }
        finally
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _menu.Dispose();
            _cancellationTokenSource.Dispose();
            Logger.Info("软件退出。");
            ExitThread();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_isExiting)
        {
            ExitApplication();
        }

        base.Dispose(disposing);
    }
}
