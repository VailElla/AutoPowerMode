using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;

namespace AutoPowerMode;

public sealed class TrayAppContext : ApplicationContext
{
    private static readonly TimeSpan SwitchCooldown = TimeSpan.FromSeconds(10);
    private const int NotificationDurationMilliseconds = 5000;

    private readonly object _sync = new();
    private readonly object _switchOperationSync = new();
    private readonly SynchronizationContext _uiContext;
    private readonly ConfigService _configService = new();
    private readonly StartupService _startupService = new();
    private readonly PowerPlanManager _powerPlanManager = new();
    private readonly IdleDetector _idleDetector = new();
    private readonly SystemIdleProtectionDetector _idleProtectionDetector = new();
    private readonly PowerModeTransitionPolicy _transitionPolicy = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu = new();
    private readonly ToolStripMenuItem _statusMenuItem = new();
    private readonly ToolStripMenuItem _currentPlanMenuItem = new();
    private readonly ToolStripMenuItem _versionMenuItem = new();
    private readonly ToolStripMenuItem _idleThresholdMenuItem = new();
    private readonly ToolStripMenuItem _monitoringScheduleMenuItem = new();
    private readonly ToolStripMenuItem _togglePauseMenuItem = new();
    private readonly ToolStripMenuItem _switchToActiveMenuItem = new();
    private readonly ToolStripMenuItem _switchToIdleMenuItem = new();
    private readonly ToolStripMenuItem _resumeAutoControlMenuItem = new();
    private readonly ToolStripMenuItem _diagnosticsMenuItem = new();
    private readonly ToolStripMenuItem _settingsMenuItem = new();
    private readonly ToolStripMenuItem _autoStartMenuItem = new();
    private readonly ToolStripMenuItem _exitMenuItem = new();

    private AppConfig _config;
    private List<PowerPlan> _powerPlans = [];
    private Task? _monitorTask;
    private SettingsForm? _settingsForm;
    private DiagnosticForm? _diagnosticForm;
    private UserActivityState _userActivityState = UserActivityState.Unknown;
    private IdleProtectionReason _idleProtectionReason = IdleProtectionReason.None;
    private ControlState _controlState = ControlState.NotConfigured;
    private PowerPlan? _currentPowerPlan;
    private DateTimeOffset _lastSwitchTime = DateTimeOffset.MinValue;
    private DateTimeOffset _lastSwitchStatusTime = DateTimeOffset.MinValue;
    private string _lastSwitchStatus = string.Empty;
    private string? _notifiedFailureTargetGuid;
    private bool _isExiting;
    private bool _configSaveFailureShown;

    public TrayAppContext()
    {
        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        Logger.Info("软件启动。");

        _config = _configService.Load();
        LocalizationService.UsePreference(_config.Language);
        _lastSwitchStatus = LocalizationService.Text("NotSwitchedYet");
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
            SaveConfigWithUserFeedback(LocalizationService.Text("SaveAutoMatchedPlansFailed"));
        }

        _currentPowerPlan = _powerPlanManager.GetActivePowerPlan();
    }

    private void BuildTrayMenu()
    {
        _statusMenuItem.Enabled = false;
        _currentPlanMenuItem.Enabled = false;
        _versionMenuItem.Enabled = false;
        _idleThresholdMenuItem.Enabled = false;
        _monitoringScheduleMenuItem.Enabled = false;

        _togglePauseMenuItem.Click += (_, _) => TogglePause();
        _switchToActiveMenuItem.Click += async (_, _) => await SwitchToConfiguredPlanAsync(UserActivityState.Active, manual: true, CancellationToken.None);
        _switchToIdleMenuItem.Click += async (_, _) => await SwitchToConfiguredPlanAsync(UserActivityState.Idle, manual: true, CancellationToken.None);
        _resumeAutoControlMenuItem.Click += async (_, _) => await RestoreAutoControlAsync();
        _diagnosticsMenuItem.Text = LocalizationService.Text("Diagnostics");
        _diagnosticsMenuItem.Click += (_, _) => OpenDiagnostics();
        _settingsMenuItem.Text = LocalizationService.Text("Settings");
        _settingsMenuItem.Click += (_, _) => OpenSettings();
        _autoStartMenuItem.Click += (_, _) => ToggleAutoStart();
        _exitMenuItem.Text = LocalizationService.Text("Exit");
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
            _monitoringScheduleMenuItem,
            new ToolStripSeparator(),
            _togglePauseMenuItem,
            _switchToActiveMenuItem,
            _switchToIdleMenuItem,
            _resumeAutoControlMenuItem,
            _diagnosticsMenuItem,
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
                var delay = MonitoringIntervalPolicy.GetInterval(
                    _userActivityState,
                    config.ActiveCheckIntervalSeconds,
                    config.IdleCheckIntervalSeconds);
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

        if (config.IsPaused)
        {
            SetControlState(ControlState.PausedByUser);
            SetIdleProtectionReason(IdleProtectionReason.None);
            _transitionPolicy.Reset();
            return;
        }

        if (!isConfigured)
        {
            SetControlState(ControlState.NotConfigured);
            SetIdleProtectionReason(IdleProtectionReason.None);
            _transitionPolicy.Reset();
            return;
        }

        var idleTime = _idleDetector.GetIdleTime();
        var idleProtectionReason = DetectIdleProtectionReason(config, idleTime, idleThreshold);
        SetIdleProtectionReason(idleProtectionReason);

        if (idleProtectionReason != IdleProtectionReason.None)
        {
            var protectedTargetState = _transitionPolicy.SuppressIdleTransition();
            if (protectedTargetState == UserActivityState.Active)
            {
                await SwitchToConfiguredPlanAsync(UserActivityState.Active, manual: false, cancellationToken);
            }

            return;
        }

        var targetActivityState = _transitionPolicy.Evaluate(
            idleTime,
            idleThreshold,
            activeResumeThreshold: idleThreshold);

        if (targetActivityState is UserActivityState.Active or UserActivityState.Idle)
        {
            await SwitchToConfiguredPlanAsync(targetActivityState.Value, manual: false, cancellationToken);
        }
    }

    private IdleProtectionReason DetectIdleProtectionReason(
        AppConfig config,
        TimeSpan idleTime,
        TimeSpan idleThreshold)
    {
        if (idleTime < idleThreshold)
        {
            return IdleProtectionReason.None;
        }

        var reason = IdleProtectionReason.None;
        if (config.PreventIdleOnExecutionState && _idleProtectionDetector.IsExecutionStateBlockingIdle())
        {
            reason |= IdleProtectionReason.ExecutionState;
        }

        if (config.PreventIdleOnFullscreen && _idleProtectionDetector.IsForegroundWindowFullscreen())
        {
            reason |= IdleProtectionReason.FullscreenForegroundWindow;
        }

        return reason;
    }

    private Task SwitchToConfiguredPlanAsync(UserActivityState targetActivityState, bool manual, CancellationToken cancellationToken)
    {
        var config = GetConfigSnapshot();
        var targetGuid = targetActivityState == UserActivityState.Idle
            ? config.IdlePowerPlanGuid
            : config.ActivePowerPlanGuid;

        return SwitchToPlanAsync(targetGuid, targetActivityState, manual, config, cancellationToken);
    }

    private Task SwitchToPlanAsync(string targetGuid, UserActivityState targetActivityState, bool manual, AppConfig config, CancellationToken cancellationToken)
    {
        return Task.Run(
            () =>
            {
                lock (_switchOperationSync)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(targetGuid))
                        {
                            Logger.Error("目标电源计划未配置。");
                            SetLastSwitchStatus(LocalizationService.Text("TargetPlanMissing"));
                            SetControlState(ControlState.NotConfigured);
                            NotifySwitchFailure(targetGuid, LocalizationService.Text("TargetPlanMissing"));
                            return;
                        }

                        var targetPlan = _powerPlanManager.FindByGuid(_powerPlans, targetGuid);
                        var targetPlanName = targetPlan?.Name ?? LocalizationService.Text("TargetPowerPlan");
                        var activePlan = _powerPlanManager.GetActivePowerPlan();
                        if (activePlan is not null)
                        {
                            SetCurrentPowerPlan(activePlan);
                        }

                        if (PowerPlanOverridePolicy.ShouldSkipAutomaticSwitch(manual, activePlan?.Guid, config))
                        {
                            var wasExternalOverride = _controlState == ControlState.ExternalOverride;
                            Logger.Info($"检测到外部手动切换到非配置电源计划，跳过本次自动切换：{activePlan?.Name} ({activePlan?.Guid})");
                            SetLastSwitchStatus(LocalizationService.Text("ExternalOverrideStatus"));
                            SetControlState(ControlState.ExternalOverride);
                            _transitionPolicy.Reset();

                            if (!wasExternalOverride)
                            {
                                ShowNotification(
                                    LocalizationService.Text("ExternalChangeTitle"),
                                    LocalizationService.Format(
                                        "ExternalChangeMessage",
                                        activePlan?.Name ?? LocalizationService.Text("Unknown")),
                                    ToolTipIcon.Warning);
                            }

                            return;
                        }

                        if (!manual &&
                            targetActivityState == UserActivityState.Idle &&
                            DateTimeOffset.Now - _lastSwitchTime < SwitchCooldown)
                        {
                            return;
                        }

                        if (PowerPlanManager.GuidEquals(activePlan?.Guid, targetGuid))
                        {
                            HandleAlreadyActivePlan(activePlan!, targetActivityState, manual);
                            return;
                        }

                        if (_powerPlanManager.TrySetActivePlan(
                                targetGuid,
                                out var verifiedPlan,
                                out var switchCommandExecuted))
                        {
                            if (!switchCommandExecuted && verifiedPlan is not null)
                            {
                                HandleAlreadyActivePlan(verifiedPlan, targetActivityState, manual);
                                return;
                            }

                            var switchedPlan = verifiedPlan ?? targetPlan;
                            var switchStatus = activePlan is null
                                ? LocalizationService.Format("SwitchSuccessTo", switchedPlan?.Name ?? targetPlanName)
                                : LocalizationService.Format("SwitchSuccessFromTo", activePlan.Name, switchedPlan?.Name ?? targetPlanName);

                            ClearFailureNotification();
                            SetLastSwitchStatus(switchStatus, successfulSwitch: true);
                            SetCurrentPowerPlan(switchedPlan);
                            SetActivityState(targetActivityState);
                            SetControlState(ControlState.Running);
                            ShowSuccessfulSwitchNotification(activePlan, switchedPlan, targetPlanName);
                        }
                        else
                        {
                            if (verifiedPlan is not null)
                            {
                                SetCurrentPowerPlan(verifiedPlan);
                            }

                            SetLastSwitchStatus(LocalizationService.Text("SwitchFailedStatus"));
                            SetControlState(ControlState.SwitchFailed);
                            NotifySwitchFailure(targetGuid, targetPlanName);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("电源计划切换异常。", ex);
                        SetLastSwitchStatus(LocalizationService.Text("SwitchExceptionStatus"));
                        SetControlState(ControlState.SwitchFailed);
                        NotifySwitchFailure(
                            targetGuid,
                            _powerPlanManager.FindByGuid(_powerPlans, targetGuid)?.Name
                            ?? LocalizationService.Text("TargetPowerPlan"));
                    }
                }
            },
            cancellationToken);
    }

    private void HandleAlreadyActivePlan(PowerPlan activePlan, UserActivityState targetActivityState, bool manual)
    {
        var previousActivityState = _userActivityState;
        var disposition = PowerPlanNotificationPolicy.ClassifyAlreadyActivePlan(
            previousActivityState,
            targetActivityState,
            manual);

        ClearFailureNotification();
        SetCurrentPowerPlan(activePlan);
        SetActivityState(targetActivityState);
        SetControlState(ControlState.Running);

        switch (disposition)
        {
            case AlreadyActivePlanDisposition.StartupSynchronized:
                SetLastSwitchStatus(LocalizationService.Format("StartupSynchronized", activePlan.Name));
                ShowNotification(
                    LocalizationService.Text("StartupNotificationTitle"),
                    LocalizationService.Format("CurrentPlanNotification", activePlan.Name),
                    ToolTipIcon.Info);
                break;

            case AlreadyActivePlanDisposition.ExternalChangeDetected:
                Logger.Info($"检测到目标电源计划已在本次自动切换前激活：{activePlan.Name} ({activePlan.Guid})");
                SetLastSwitchStatus(LocalizationService.Format("ExternalActivatedStatus", activePlan.Name));
                ShowNotification(
                    LocalizationService.Text("PlanChangeDetectedTitle"),
                    LocalizationService.Format("PlanChangeDetectedMessage", activePlan.Name),
                    ToolTipIcon.Warning);
                break;

            default:
                SetLastSwitchStatus(LocalizationService.Format("NoSwitchNeededStatus", activePlan.Name));
                if (manual)
                {
                    ShowNotification(
                        LocalizationService.Text("NoSwitchNeededTitle"),
                        LocalizationService.Format("CurrentPlanNotification", activePlan.Name),
                        ToolTipIcon.Info);
                }

                break;
        }
    }

    private void ShowSuccessfulSwitchNotification(PowerPlan? previousPlan, PowerPlan? currentPlan, string fallbackTargetName)
    {
        var currentPlanName = currentPlan?.Name ?? fallbackTargetName;
        var message = previousPlan is null
            ? LocalizationService.Format("SwitchedToMessage", currentPlanName)
            : LocalizationService.Format("SwitchedFromToMessage", previousPlan.Name, currentPlanName);

        ShowNotification(LocalizationService.Text("PowerModeSwitchedTitle"), message, ToolTipIcon.Info);
    }

    private void NotifySwitchFailure(string targetGuid, string targetPlanName)
    {
        if (!GetConfigSnapshot().NotificationsEnabled)
        {
            return;
        }

        var failureKey = string.IsNullOrWhiteSpace(targetGuid) ? "<not-configured>" : targetGuid;
        if (string.Equals(_notifiedFailureTargetGuid, failureKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _notifiedFailureTargetGuid = failureKey;
        ShowNotification(
            LocalizationService.Text("SwitchFailedTitle"),
            LocalizationService.Format("SwitchFailedMessage", targetPlanName),
            ToolTipIcon.Error);
    }

    private void ClearFailureNotification()
    {
        _notifiedFailureTargetGuid = null;
    }

    private void ShowNotification(string title, string message, ToolTipIcon icon)
    {
        if (!GetConfigSnapshot().NotificationsEnabled || _isExiting)
        {
            return;
        }

        PostToUi(
            () =>
            {
                try
                {
                    _notifyIcon.ShowBalloonTip(
                        NotificationDurationMilliseconds,
                        TruncateNotificationText(title, 63),
                        TruncateNotificationText(message, 255),
                        icon);
                }
                catch (Exception ex)
                {
                    Logger.Error("显示系统通知失败。", ex);
                }
            });
    }

    private static string TruncateNotificationText(string value, int maximumLength)
    {
        if (value.Length <= maximumLength)
        {
            return value;
        }

        return value[..Math.Max(0, maximumLength - 1)] + "…";
    }

    private void TogglePause()
    {
        lock (_sync)
        {
            _config.IsPaused = !_config.IsPaused;
            Logger.Info(_config.IsPaused ? "用户暂停自动切换。" : "用户恢复自动切换。");
        }

        SaveConfigWithUserFeedback(LocalizationService.Text("PauseSaveFailed"));

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
                LocalizationService.Text("AutoStartChangeFailed"),
                AppInfo.DisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        lock (_sync)
        {
            _config.AutoStart = _startupService.IsEnabled();
        }

        SaveConfigWithUserFeedback(LocalizationService.Text("AutoStartSaveFailed"));

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
                SaveConfigWithUserFeedback(LocalizationService.Text("SaveAutoMatchedPlansFailed"));
            }
        }

        var configForForm = GetConfigSnapshot();
        configForForm.AutoStart = _startupService.IsEnabled();

        _settingsForm = new SettingsForm(configForForm, _powerPlans, GetDisplayStatusText(configForForm));
        if (_settingsForm.ShowDialog() == DialogResult.OK)
        {
            ApplySettings(_settingsForm.SavedConfig);
        }

        _settingsForm.Dispose();
        _settingsForm = null;
    }

    private void ApplySettings(AppConfig newConfig)
    {
        var previousLanguagePreference = GetConfigSnapshot().Language;
        newConfig.Normalize();
        LocalizationService.UsePreference(newConfig.Language);

        if (!_startupService.SetEnabled(newConfig.AutoStart))
        {
            MessageBox.Show(
                LocalizationService.Text("AutoStartStatePreserved"),
                AppInfo.DisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        newConfig.AutoStart = _startupService.IsEnabled();
        if (_powerPlanManager.IsBalancedPlanGuid(_powerPlans, newConfig.ActivePowerPlanGuid))
        {
            _powerPlanManager.TryAutoConfigure(newConfig, _powerPlans);
        }

        if (!string.Equals(previousLanguagePreference, newConfig.Language, StringComparison.Ordinal))
        {
            _lastSwitchStatus = LocalizationService.Text("NotSwitchedYet");
            _lastSwitchStatusTime = DateTimeOffset.MinValue;
        }

        lock (_sync)
        {
            _config = newConfig;
        }

        if (!newConfig.PreventIdleOnExecutionState && !newConfig.PreventIdleOnFullscreen)
        {
            SetIdleProtectionReason(IdleProtectionReason.None);
        }

        SaveConfigWithUserFeedback(LocalizationService.Text("SettingsSaveFailed"), alwaysShowFailure: true);
        Logger.Info("用户修改设置。");
        RefreshStateFromConfig();
        RefreshMenuItems();
        _diagnosticForm?.Close();

        if (!newConfig.IsPaused)
        {
            QueueEvaluateOnce();
        }
    }

    private void OpenDiagnostics()
    {
        if (_diagnosticForm is { IsDisposed: false })
        {
            _diagnosticForm.RefreshSnapshot();
            _diagnosticForm.WindowState = FormWindowState.Normal;
            _diagnosticForm.BringToFront();
            _diagnosticForm.Activate();
            return;
        }

        _diagnosticForm = new DiagnosticForm(
            BuildDiagnosticSnapshot,
            OpenLogDirectory,
            RedetectPowerPlans,
            RestoreAutoControlAsync);
        _diagnosticForm.FormClosed += (_, _) => _diagnosticForm = null;
        _diagnosticForm.Show();
    }

    private void OpenLogDirectory()
    {
        try
        {
            Directory.CreateDirectory(Logger.LogDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = Logger.LogDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.Error("打开日志目录失败。", ex);
            MessageBox.Show(
                $"{LocalizationService.Text("OpenLogFailed")}{Environment.NewLine}{Logger.GetLogLocationMessage()}",
                AppInfo.DisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void RedetectPowerPlans()
    {
        _powerPlans = _powerPlanManager.GetPowerPlans();
        lock (_sync)
        {
            if (_powerPlanManager.TryAutoConfigure(_config, _powerPlans))
            {
                SaveConfigWithUserFeedback(LocalizationService.Text("SaveAutoMatchedPlansFailed"));
            }
        }

        _currentPowerPlan = _powerPlanManager.GetActivePowerPlan() ?? _currentPowerPlan;
        RefreshStateFromConfig();
        RefreshMenuItems();
    }

    private async Task RestoreAutoControlAsync()
    {
        var config = GetConfigSnapshot();
        if (config.IsPaused)
        {
            lock (_sync)
            {
                _config.IsPaused = false;
            }

            SaveConfigWithUserFeedback(LocalizationService.Text("RestoreStateSaveFailed"));
            config = GetConfigSnapshot();
        }

        if (!IsConfigured(config))
        {
            SetControlState(ControlState.NotConfigured);
            return;
        }

        _transitionPolicy.Reset();
        SetControlState(ControlState.Running);
        await SwitchToConfiguredPlanAsync(UserActivityState.Active, manual: true, CancellationToken.None);
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
            SaveConfigWithUserFeedback(LocalizationService.Text("SyncAutoStartSaveFailed"));
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
            SetIdleProtectionReason(IdleProtectionReason.None);
            SetControlState(ControlState.PausedByUser);
        }
        else if (!IsConfigured(config))
        {
            SetIdleProtectionReason(IdleProtectionReason.None);
            SetControlState(ControlState.NotConfigured);
        }
        else if (_controlState is ControlState.PausedByUser or ControlState.NotConfigured)
        {
            SetControlState(ControlState.Running);
        }
    }

    private void RefreshMenuItems()
    {
        if (_isExiting)
        {
            return;
        }

        var config = GetConfigSnapshot();
        var statusText = GetDisplayStatusText(config);
        var currentPowerPlanName = _currentPowerPlan?.Name ?? LocalizationService.Text("Unknown");
        var autoStartText = LocalizationService.Text(_startupService.IsEnabled() ? "Enabled" : "Disabled");

        _versionMenuItem.Text = LocalizationService.Format("VersionMenu", AppInfo.Version);
        _statusMenuItem.Text = LocalizationService.Format("StatusMenu", statusText);
        _currentPlanMenuItem.Text = LocalizationService.Format("CurrentPlanMenu", currentPowerPlanName);
        _idleThresholdMenuItem.Text = LocalizationService.Format("IdleThresholdMenu", config.IdleThresholdSeconds);
        _monitoringScheduleMenuItem.Text = LocalizationService.Format(
            "MonitoringScheduleMenu",
            config.ActiveCheckIntervalSeconds,
            config.IdleCheckIntervalSeconds);
        _togglePauseMenuItem.Text = LocalizationService.Text(config.IsPaused ? "ResumeSwitching" : "PauseSwitching");
        _switchToActiveMenuItem.Text = LocalizationService.Text("SwitchToActive");
        _switchToIdleMenuItem.Text = LocalizationService.Text("SwitchToIdle");
        _switchToActiveMenuItem.Enabled = _powerPlanManager.FindByGuid(_powerPlans, config.ActivePowerPlanGuid) is not null;
        _switchToIdleMenuItem.Enabled = _powerPlanManager.FindByGuid(_powerPlans, config.IdlePowerPlanGuid) is not null;
        _resumeAutoControlMenuItem.Text = LocalizationService.Text("RestoreAutoControl");
        _resumeAutoControlMenuItem.Enabled = IsConfigured(config) && _controlState is ControlState.ExternalOverride or ControlState.SwitchFailed;
        _diagnosticsMenuItem.Text = LocalizationService.Text("Diagnostics");
        _settingsMenuItem.Text = LocalizationService.Text("Settings");
        _autoStartMenuItem.Text = LocalizationService.Format("AutoStartMenu", autoStartText);
        _exitMenuItem.Text = LocalizationService.Text("Exit");

        try
        {
            _notifyIcon.Text = $"{AppInfo.Name} {AppInfo.Version}: {GetCompactStatusText(config)}";
        }
        catch
        {
            _notifyIcon.Text = AppInfo.DisplayName;
        }
    }

    private string GetDisplayStatusText(AppConfig config)
    {
        if (config.IsPaused)
        {
            return LocalizationService.Text("StatusPaused");
        }

        if (!IsConfigured(config))
        {
            return LocalizationService.Text("StatusNotConfigured");
        }

        if (_controlState == ControlState.Running && _idleProtectionReason != IdleProtectionReason.None)
        {
            return LocalizationService.Format(
                "StatusIdleProtected",
                GetIdleProtectionReasonText(_idleProtectionReason));
        }

        return _controlState switch
        {
            ControlState.ExternalOverride => LocalizationService.Text("StatusExternalOverride"),
            ControlState.SwitchFailed => LocalizationService.Text("StatusSwitchFailed"),
            ControlState.PausedByUser => LocalizationService.Text("StatusPaused"),
            ControlState.NotConfigured => LocalizationService.Text("StatusNotConfigured"),
            _ => _userActivityState switch
            {
                UserActivityState.Active => LocalizationService.Text("StatusActive"),
                UserActivityState.Idle => LocalizationService.Text("StatusIdle"),
                _ => LocalizationService.Text("StatusWaiting")
            }
        };
    }

    private string GetCompactStatusText(AppConfig config)
    {
        if (config.IsPaused)
        {
            return "Paused";
        }

        if (!IsConfigured(config))
        {
            return "NotConfigured";
        }

        return _controlState switch
        {
            ControlState.ExternalOverride => "ExternalOverride",
            ControlState.SwitchFailed => "SwitchFailed",
            ControlState.Running => _userActivityState.ToString(),
            _ => _controlState.ToString()
        };
    }

    private DiagnosticSnapshot BuildDiagnosticSnapshot()
    {
        var config = GetConfigSnapshot();
        var idleTimeText = LocalizationService.Text("Unknown");
        try
        {
            idleTimeText = FormatDuration(_idleDetector.GetIdleTime());
        }
        catch (Exception ex)
        {
            Logger.Error("读取诊断空闲时间失败。", ex);
        }

        return new DiagnosticSnapshot
        {
            AppVersion = AppInfo.Version,
            ControlState = _controlState,
            UserActivityState = _userActivityState,
            DisplayStatus = GetDisplayStatusText(config),
            CurrentPowerPlan = FormatPlan(_currentPowerPlan),
            ActivePlanConfig = FormatPlan(_powerPlanManager.FindByGuid(_powerPlans, config.ActivePowerPlanGuid), config.ActivePowerPlanGuid),
            IdlePlanConfig = FormatPlan(_powerPlanManager.FindByGuid(_powerPlans, config.IdlePowerPlanGuid), config.IdlePowerPlanGuid),
            IdleTime = idleTimeText,
            IdleThreshold = FormatDuration(TimeSpan.FromSeconds(config.IdleThresholdSeconds)),
            MonitoringSchedule = LocalizationService.Format(
                "MonitoringScheduleValue",
                config.ActiveCheckIntervalSeconds,
                config.IdleCheckIntervalSeconds),
            IdleProtectionSettings = LocalizationService.Format(
                "IdleProtectionSettingsValue",
                LocalizationService.Text(config.PreventIdleOnExecutionState ? "Enabled" : "Disabled"),
                LocalizationService.Text(config.PreventIdleOnFullscreen ? "Enabled" : "Disabled")),
            CurrentIdleProtection = GetIdleProtectionReasonText(_idleProtectionReason),
            NotificationsEnabled = config.NotificationsEnabled,
            LastSwitchStatus = _lastSwitchStatusTime == DateTimeOffset.MinValue
                ? _lastSwitchStatus
                : $"{_lastSwitchStatus}（{_lastSwitchStatusTime:yyyy-MM-dd HH:mm:ss}）",
            LastPowerCfgError = string.IsNullOrWhiteSpace(_powerPlanManager.LastPowerCfgError)
                ? LocalizationService.Text("None")
                : _powerPlanManager.LastPowerCfgError,
            ConfigPath = Logger.SanitizeMessage(_configService.ConfigPath),
            LogPath = Logger.SanitizeMessage(Logger.LogFilePath),
            PortableLogPath = Logger.SanitizeMessage(Logger.PortableLogFilePath)
        };
    }

    private static string FormatPlan(PowerPlan? plan, string? fallbackGuid = null)
    {
        if (plan is not null)
        {
            return $"{plan.Name} ({plan.Guid})";
        }

        return string.IsNullOrWhiteSpace(fallbackGuid)
            ? LocalizationService.Text("NotConfigured")
            : LocalizationService.Format("PlanNotFound", fallbackGuid);
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return LocalizationService.Format(
                "HoursMinutesSeconds",
                (int)duration.TotalHours,
                duration.Minutes,
                duration.Seconds);
        }

        if (duration.TotalMinutes >= 1)
        {
            return LocalizationService.Format("MinutesSeconds", duration.Minutes, duration.Seconds);
        }

        return LocalizationService.Format("SecondsValue", Math.Max(0, (int)duration.TotalSeconds));
    }

    private static string GetIdleProtectionReasonText(IdleProtectionReason reason)
    {
        return reason switch
        {
            IdleProtectionReason.ExecutionState => LocalizationService.Text("IdleProtectionExecutionStateReason"),
            IdleProtectionReason.FullscreenForegroundWindow => LocalizationService.Text("IdleProtectionFullscreenReason"),
            IdleProtectionReason.ExecutionState | IdleProtectionReason.FullscreenForegroundWindow =>
                LocalizationService.Text("IdleProtectionBothReasons"),
            _ => LocalizationService.Text("None")
        };
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

    private void SetActivityState(UserActivityState state)
    {
        _userActivityState = state;
        _transitionPolicy.MarkActivityState(state);
        PostToUi(RefreshMenuItems);
    }

    private void SetIdleProtectionReason(IdleProtectionReason reason)
    {
        if (_idleProtectionReason == reason)
        {
            return;
        }

        _idleProtectionReason = reason;
        if (reason == IdleProtectionReason.None)
        {
            Logger.Info("空闲误触保护已解除。");
        }
        else
        {
            Logger.Info($"空闲误触保护已生效：{reason}。");
        }

        PostToUi(RefreshMenuItems);
    }

    private void SetControlState(ControlState state)
    {
        _controlState = state;
        PostToUi(RefreshMenuItems);
    }

    private void SetLastSwitchStatus(string status, bool successfulSwitch = false)
    {
        var now = DateTimeOffset.Now;
        _lastSwitchStatus = status;
        _lastSwitchStatusTime = now;

        if (successfulSwitch)
        {
            _lastSwitchTime = now;
        }
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
            SaveConfigWithUserFeedback(LocalizationService.Text("ExitSaveFailed"));
            _monitorTask?.Wait(TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            Logger.Error("退出程序时后台任务取消异常。", ex);
        }
        finally
        {
            _notifyIcon.Visible = false;
            _diagnosticForm?.Dispose();
            _settingsForm?.Dispose();
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
