using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace AutoPowerMode;

public sealed class SettingsForm : Form
{
    private const int InputMinimumWidth = 190;

    private readonly NumericUpDown _idleThresholdInput = new();
    private readonly Label _monitoringScheduleValueLabel = new();
    private readonly ComboBox _activePlanCombo = new();
    private readonly ComboBox _idlePlanCombo = new();
    private readonly ComboBox _languageCombo = new();
    private readonly CheckBox _autoStartCheckBox = new();
    private readonly CheckBox _notificationsEnabledCheckBox = new();
    private readonly CheckBox _preventIdleOnExecutionStateCheckBox = new();
    private readonly CheckBox _preventIdleOnFullscreenCheckBox = new();
    private readonly Label _automationStatusValueLabel = new();
    private readonly TableLayoutPanel _root = new();
    private readonly ToolTip _toolTip = new();
    private readonly AppConfig _originalConfig;

    public SettingsForm(AppConfig config, IReadOnlyList<PowerPlan> powerPlans, string automationStatusText)
    {
        _originalConfig = config.Clone();
        SavedConfig = config.Clone();

        Text = LocalizationService.Format("SettingsTitle", AppInfo.DisplayName);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScroll = true;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        SizeGripStyle = SizeGripStyle.Show;
        ClientSize = new Size(DpiLayoutPolicy.InitialClientWidth, DpiLayoutPolicy.InitialClientHeight);

        BuildLayout(powerPlans);
        _automationStatusValueLabel.Text = automationStatusText;
        LoadValues(config, powerPlans);

        Load += (_, _) => ApplyDpiAwareLayout(applyInitialSize: true);
        DpiChanged += (_, _) =>
        {
            if (!IsHandleCreated || IsDisposed)
            {
                return;
            }

            BeginInvoke(new Action(() => ApplyDpiAwareLayout(applyInitialSize: false)));
        };
    }

    public AppConfig SavedConfig { get; private set; }

    private void BuildLayout(IReadOnlyList<PowerPlan> powerPlans)
    {
        var root = _root;
        root.Dock = DockStyle.Fill;
        root.Padding = new Padding(12);
        root.ColumnCount = 3;
        root.RowCount = 10;
        root.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;

        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        for (var i = 0; i < 9; i++)
        {
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _idleThresholdInput.Minimum = AppConfig.MinIdleThresholdSeconds;
        _idleThresholdInput.Maximum = AppConfig.MaxIdleThresholdSeconds;
        _idleThresholdInput.DecimalPlaces = 0;
        _idleThresholdInput.ThousandsSeparator = false;
        _idleThresholdInput.Dock = DockStyle.Fill;
        _idleThresholdInput.MinimumSize = new Size(InputMinimumWidth, 0);

        _monitoringScheduleValueLabel.Text = LocalizationService.Format(
            "MonitoringScheduleValue",
            (int)MonitoringIntervalPolicy.ActiveInterval.TotalSeconds,
            (int)MonitoringIntervalPolicy.IdleInterval.TotalSeconds);
        _monitoringScheduleValueLabel.AutoSize = true;
        _monitoringScheduleValueLabel.Anchor = AnchorStyles.Left;

        _automationStatusValueLabel.AutoSize = false;
        _automationStatusValueLabel.AutoEllipsis = true;
        _automationStatusValueLabel.Dock = DockStyle.Fill;
        _automationStatusValueLabel.TextAlign = ContentAlignment.MiddleLeft;

        ConfigureComboBox(_activePlanCombo, powerPlans);
        ConfigureComboBox(_idlePlanCombo, powerPlans);
        AttachPlanToolTip(_activePlanCombo);
        AttachPlanToolTip(_idlePlanCombo);

        _languageCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _languageCombo.Dock = DockStyle.Fill;
        _languageCombo.MinimumSize = new Size(InputMinimumWidth, 0);
        _languageCombo.Items.AddRange(
        [
            new LanguageOption(AppLanguagePreference.System, LocalizationService.Text("LanguageSystem")),
            new LanguageOption(AppLanguagePreference.English, LocalizationService.Text("LanguageEnglish")),
            new LanguageOption(AppLanguagePreference.SimplifiedChinese, LocalizationService.Text("LanguageChinese"))
        ]);

        _autoStartCheckBox.Text = LocalizationService.Text("EnableAutoStart");
        _autoStartCheckBox.AutoSize = true;
        _autoStartCheckBox.Anchor = AnchorStyles.Left;

        _notificationsEnabledCheckBox.Text = LocalizationService.Text("EnableNotifications");
        _notificationsEnabledCheckBox.AutoSize = true;
        _notificationsEnabledCheckBox.Anchor = AnchorStyles.Left;

        _preventIdleOnExecutionStateCheckBox.Text = LocalizationService.Text("PreventIdleOnExecutionState");
        _preventIdleOnExecutionStateCheckBox.AutoSize = true;
        _preventIdleOnExecutionStateCheckBox.Anchor = AnchorStyles.Left;
        _toolTip.SetToolTip(
            _preventIdleOnExecutionStateCheckBox,
            LocalizationService.Text("PreventIdleOnExecutionStateHint"));

        _preventIdleOnFullscreenCheckBox.Text = LocalizationService.Text("PreventIdleOnFullscreen");
        _preventIdleOnFullscreenCheckBox.AutoSize = true;
        _preventIdleOnFullscreenCheckBox.Anchor = AnchorStyles.Left;
        _toolTip.SetToolTip(
            _preventIdleOnFullscreenCheckBox,
            LocalizationService.Text("PreventIdleOnFullscreenHint"));

        AddText(root, LocalizationService.Text("AutomationStatus"), 0, 0);
        root.Controls.Add(_automationStatusValueLabel, 1, 0);
        root.SetColumnSpan(_automationStatusValueLabel, 2);

        AddText(root, LocalizationService.Text("IdleSwitchDelay"), 0, 1);
        root.Controls.Add(_idleThresholdInput, 1, 1);
        AddText(root, LocalizationService.Text("Seconds"), 2, 1);

        AddText(root, LocalizationService.Text("MonitoringSchedule"), 0, 2);
        root.Controls.Add(_monitoringScheduleValueLabel, 1, 2);
        root.SetColumnSpan(_monitoringScheduleValueLabel, 2);

        AddText(root, LocalizationService.Text("ActivePowerPlan"), 0, 3);
        root.Controls.Add(_activePlanCombo, 1, 3);
        root.SetColumnSpan(_activePlanCombo, 2);

        AddText(root, LocalizationService.Text("IdlePowerPlan"), 0, 4);
        root.Controls.Add(_idlePlanCombo, 1, 4);
        root.SetColumnSpan(_idlePlanCombo, 2);

        var idleProtectionOptions = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty
        };
        idleProtectionOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        idleProtectionOptions.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        idleProtectionOptions.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        idleProtectionOptions.Controls.Add(_preventIdleOnExecutionStateCheckBox, 0, 0);
        idleProtectionOptions.Controls.Add(_preventIdleOnFullscreenCheckBox, 0, 1);

        AddText(root, LocalizationService.Text("IdleProtection"), 0, 5);
        root.Controls.Add(idleProtectionOptions, 1, 5);
        root.SetColumnSpan(idleProtectionOptions, 2);

        AddText(root, LocalizationService.Text("Startup"), 0, 6);
        root.Controls.Add(_autoStartCheckBox, 1, 6);
        root.SetColumnSpan(_autoStartCheckBox, 2);

        AddText(root, LocalizationService.Text("SystemNotifications"), 0, 7);
        root.Controls.Add(_notificationsEnabledCheckBox, 1, 7);
        root.SetColumnSpan(_notificationsEnabledCheckBox, 2);

        AddText(root, LocalizationService.Text("Language"), 0, 8);
        root.Controls.Add(_languageCombo, 1, 8);
        root.SetColumnSpan(_languageCombo, 2);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            WrapContents = false
        };

        var githubButton = new Button
        {
            Text = LocalizationService.Text("GitHubHomepage"),
            AutoSize = true,
            Anchor = AnchorStyles.Left
        };

        var saveButton = new Button
        {
            Text = LocalizationService.Text("Save"),
            AutoSize = true,
            DialogResult = DialogResult.None
        };

        var cancelButton = new Button
        {
            Text = LocalizationService.Text("Cancel"),
            AutoSize = true,
            DialogResult = DialogResult.Cancel
        };

        saveButton.Click += SaveButton_Click;
        githubButton.Click += (_, _) => OpenRepository();

        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(cancelButton);

        var buttonBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };
        buttonBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttonBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttonBar.Controls.Add(githubButton, 0, 0);
        buttonBar.Controls.Add(buttons, 1, 0);

        root.Controls.Add(buttonBar, 0, 9);
        root.SetColumnSpan(buttonBar, 3);

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        Controls.Add(root);
    }

    private void ApplyDpiAwareLayout(bool applyInitialSize)
    {
        var nonClientSize = new Size(
            Math.Max(0, Width - ClientSize.Width),
            Math.Max(0, Height - ClientSize.Height));
        var workingArea = Screen.FromControl(this).WorkingArea;
        var metrics = DpiLayoutPolicy.Calculate(DeviceDpi, workingArea.Size, nonClientSize);

        SuspendLayout();
        try
        {
            _root.PerformLayout();
            var preferredContentSize = _root.GetPreferredSize(metrics.MinimumClientSize);
            AutoScrollMinSize = new Size(
                Math.Max(metrics.MinimumClientSize.Width, preferredContentSize.Width),
                Math.Max(metrics.MinimumClientSize.Height, preferredContentSize.Height));
            MinimumSize = new Size(
                metrics.MinimumClientSize.Width + nonClientSize.Width,
                metrics.MinimumClientSize.Height + nonClientSize.Height);

            if (applyInitialSize)
            {
                ClientSize = new Size(
                    Math.Min(
                        metrics.MaximumClientSize.Width,
                        Math.Max(metrics.InitialClientSize.Width, preferredContentSize.Width)),
                    Math.Min(
                        metrics.MaximumClientSize.Height,
                        Math.Max(metrics.InitialClientSize.Height, preferredContentSize.Height)));
            }
            else
            {
                var clampedClientSize = new Size(
                    Math.Min(ClientSize.Width, metrics.MaximumClientSize.Width),
                    Math.Min(ClientSize.Height, metrics.MaximumClientSize.Height));

                if (clampedClientSize != ClientSize)
                {
                    ClientSize = clampedClientSize;
                }
            }
        }
        finally
        {
            ResumeLayout(performLayout: true);
        }
    }

    private void LoadValues(AppConfig config, IReadOnlyList<PowerPlan> powerPlans)
    {
        _idleThresholdInput.Value = Math.Clamp(
            config.IdleThresholdSeconds,
            AppConfig.MinIdleThresholdSeconds,
            AppConfig.MaxIdleThresholdSeconds);

        SelectPlan(_activePlanCombo, config.ActivePowerPlanGuid, preferActivePlan: true);
        SelectPlan(_idlePlanCombo, config.IdlePowerPlanGuid, preferActivePlan: false);
        UpdatePlanToolTip(_activePlanCombo);
        UpdatePlanToolTip(_idlePlanCombo);
        _autoStartCheckBox.Checked = config.AutoStart;
        _notificationsEnabledCheckBox.Checked = config.NotificationsEnabled;
        _preventIdleOnExecutionStateCheckBox.Checked = config.PreventIdleOnExecutionState;
        _preventIdleOnFullscreenCheckBox.Checked = config.PreventIdleOnFullscreen;
        SelectLanguage(config.Language);
    }

    private void SelectLanguage(string preference)
    {
        var normalized = AppLanguagePreference.Normalize(preference);
        for (var index = 0; index < _languageCombo.Items.Count; index++)
        {
            if (_languageCombo.Items[index] is LanguageOption option && option.Preference == normalized)
            {
                _languageCombo.SelectedIndex = index;
                return;
            }
        }

        _languageCombo.SelectedIndex = 0;
    }

    private static void ConfigureComboBox(ComboBox comboBox, IReadOnlyList<PowerPlan> powerPlans)
    {
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBox.Dock = DockStyle.Fill;
        comboBox.IntegralHeight = false;
        comboBox.MinimumSize = new Size(InputMinimumWidth, 0);
        comboBox.MaxDropDownItems = Math.Clamp(powerPlans.Count, 1, 12);
        comboBox.Items.Clear();
        comboBox.DropDown += (_, _) =>
        {
            comboBox.DropDownWidth = CalculatePlanDropDownWidth(comboBox, powerPlans);
        };

        foreach (var powerPlan in powerPlans)
        {
            comboBox.Items.Add(powerPlan);
        }

        comboBox.Enabled = powerPlans.Count > 0;
    }

    private void AttachPlanToolTip(ComboBox comboBox)
    {
        comboBox.SelectedIndexChanged += (_, _) => UpdatePlanToolTip(comboBox);
        comboBox.MouseHover += (_, _) => UpdatePlanToolTip(comboBox);
    }

    private void UpdatePlanToolTip(ComboBox comboBox)
    {
        var text = comboBox.SelectedItem is PowerPlan plan ? plan.TooltipText : string.Empty;
        _toolTip.SetToolTip(comboBox, text);
    }

    private static int CalculatePlanDropDownWidth(ComboBox comboBox, IReadOnlyList<PowerPlan> powerPlans)
    {
        if (powerPlans.Count == 0)
        {
            return comboBox.Width;
        }

        using var graphics = comboBox.CreateGraphics();
        var longestTextWidth = powerPlans
            .Select(plan => TextRenderer.MeasureText(graphics, plan.DisplayName, comboBox.Font).Width)
            .DefaultIfEmpty(comboBox.Width)
            .Max();

        var desiredWidth = longestTextWidth + SystemInformation.VerticalScrollBarWidth + 32;
        return Math.Clamp(desiredWidth, comboBox.Width, 900);
    }

    private static void SelectPlan(ComboBox comboBox, string guid, bool preferActivePlan)
    {
        var index = -1;

        for (var i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is PowerPlan plan &&
                string.Equals(plan.Guid, guid, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        if (index >= 0)
        {
            comboBox.SelectedIndex = index;
        }
        else
        {
            SelectDefaultPlan(comboBox, preferActivePlan);
        }
    }

    private static void SelectDefaultPlan(ComboBox comboBox, bool preferActivePlan)
    {
        var index = -1;

        for (var i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is not PowerPlan plan)
            {
                continue;
            }

            var isPreferred = preferActivePlan
                ? IsHighPerformancePlan(plan)
                : IsPowerSaverPlan(plan);

            if (isPreferred)
            {
                index = i;
                break;
            }
        }

        if (index >= 0)
        {
            comboBox.SelectedIndex = index;
        }
        else if (comboBox.Items.Count > 0)
        {
            comboBox.SelectedIndex = 0;
        }
    }

    private static bool IsHighPerformancePlan(PowerPlan plan)
    {
        return string.Equals(plan.Guid, "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", StringComparison.OrdinalIgnoreCase)
               || string.Equals(plan.Name, "High Performance", StringComparison.CurrentCultureIgnoreCase)
               || string.Equals(plan.Name, "高性能", StringComparison.CurrentCultureIgnoreCase)
               || string.Equals(plan.Name, "高效能", StringComparison.CurrentCultureIgnoreCase);
    }

    private static bool IsPowerSaverPlan(PowerPlan plan)
    {
        return string.Equals(plan.Guid, "a1841308-3541-4fab-bc81-f71556f20b4a", StringComparison.OrdinalIgnoreCase)
               || string.Equals(plan.Name, "Power Saver", StringComparison.CurrentCultureIgnoreCase)
               || string.Equals(plan.Name, "节能", StringComparison.CurrentCultureIgnoreCase)
               || string.Equals(plan.Name, "節能", StringComparison.CurrentCultureIgnoreCase)
               || string.Equals(plan.Name, "省电", StringComparison.CurrentCultureIgnoreCase)
               || string.Equals(plan.Name, "省電", StringComparison.CurrentCultureIgnoreCase);
    }

    private static void AddText(TableLayoutPanel panel, string text, int column, int row)
    {
        panel.Controls.Add(
            new Label
            {
                Text = text,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                TextAlign = ContentAlignment.MiddleLeft
            },
            column,
            row);
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        if (!ValidateInputs())
        {
            return;
        }

        var activePlan = _activePlanCombo.SelectedItem as PowerPlan;
        var idlePlan = _idlePlanCombo.SelectedItem as PowerPlan;

        SavedConfig = _originalConfig.Clone();
        SavedConfig.IdleThresholdSeconds = (int)_idleThresholdInput.Value;
        SavedConfig.ActivePowerPlanGuid = activePlan?.Guid ?? string.Empty;
        SavedConfig.IdlePowerPlanGuid = idlePlan?.Guid ?? string.Empty;
        SavedConfig.AutoStart = _autoStartCheckBox.Checked;
        SavedConfig.NotificationsEnabled = _notificationsEnabledCheckBox.Checked;
        SavedConfig.PreventIdleOnExecutionState = _preventIdleOnExecutionStateCheckBox.Checked;
        SavedConfig.PreventIdleOnFullscreen = _preventIdleOnFullscreenCheckBox.Checked;
        SavedConfig.Language = (_languageCombo.SelectedItem as LanguageOption)?.Preference
                               ?? AppLanguagePreference.System;
        SavedConfig.PowerPlansConfiguredByUser = true;
        SavedConfig.Normalize();

        DialogResult = DialogResult.OK;
        Close();
    }

    private bool ValidateInputs()
    {
        if (_idleThresholdInput.Value < AppConfig.MinIdleThresholdSeconds ||
            _idleThresholdInput.Value > AppConfig.MaxIdleThresholdSeconds)
        {
            MessageBox.Show(
                LocalizationService.Format(
                    "IdleRangeValidation",
                    AppConfig.MinIdleThresholdSeconds,
                    AppConfig.MaxIdleThresholdSeconds),
                AppInfo.DisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        if (_activePlanCombo.SelectedItem is not PowerPlan)
        {
            MessageBox.Show(
                LocalizationService.Text("SelectActivePlan"),
                AppInfo.DisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        if (_idlePlanCombo.SelectedItem is not PowerPlan)
        {
            MessageBox.Show(
                LocalizationService.Text("SelectIdlePlan"),
                AppInfo.DisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    private void OpenRepository()
    {
        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = AppInfo.RepositoryUrl,
                    UseShellExecute = true
                });
        }
        catch (Exception ex)
        {
            Logger.Error("打开 GitHub 项目主页失败。", ex);
            MessageBox.Show(
                $"{LocalizationService.Text("OpenGitHubFailed")}{Environment.NewLine}{AppInfo.RepositoryUrl}",
                AppInfo.DisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed record LanguageOption(string Preference, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }
}
