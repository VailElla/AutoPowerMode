using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace AutoPowerMode;

public sealed class SettingsForm : Form
{
    private const int NumericInputMinimumWidth = 96;
    private const int SelectionInputMinimumWidth = 160;

    private readonly NumericUpDown _idleThresholdInput = new();
    private readonly NumericUpDown _activeCheckIntervalInput = new();
    private readonly NumericUpDown _idleCheckIntervalInput = new();
    private readonly ComboBox _activePlanCombo = new();
    private readonly ComboBox _idlePlanCombo = new();
    private readonly ComboBox _languageCombo = new();
    private readonly CheckBox _autoStartCheckBox = new();
    private readonly CheckBox _notificationsEnabledCheckBox = new();
    private readonly CheckBox _preventIdleOnExecutionStateCheckBox = new();
    private readonly CheckBox _preventIdleOnFullscreenCheckBox = new();
    private readonly Label _automationStatusLabel = new();
    private readonly Label _idleThresholdLabel = new();
    private readonly Label _activeCheckIntervalLabel = new();
    private readonly Label _idleCheckIntervalLabel = new();
    private readonly Label _activePlanLabel = new();
    private readonly Label _idlePlanLabel = new();
    private readonly Label _idleProtectionLabel = new();
    private readonly Label _startupLabel = new();
    private readonly Label _systemNotificationsLabel = new();
    private readonly Label _languageLabel = new();
    private readonly Label _idleThresholdUnitLabel = new();
    private readonly Label _activeCheckIntervalUnitLabel = new();
    private readonly Label _idleCheckIntervalUnitLabel = new();
    private readonly Label _automationStatusValueLabel = new();
    private readonly TableLayoutPanel _root = new();
    private readonly TableLayoutPanel _idleProtectionOptions = new();
    private readonly Panel _settingsScrollPanel = new();
    private readonly TableLayoutPanel _buttonBar = new();
    private readonly ToolTip _toolTip = new();
    private readonly AppConfig _originalConfig;
    private bool? _compactSettingsLayout;
    private bool _updatingScrollableLayout;

    public SettingsForm(AppConfig config, IReadOnlyList<PowerPlan> powerPlans, string automationStatusText)
    {
        _originalConfig = config.Clone();
        SavedConfig = config.Clone();

        Text = LocalizationService.Format("SettingsTitle", AppInfo.DisplayName);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
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
        root.Location = Point.Empty;
        root.AutoSize = true;
        root.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        root.Padding = new Padding(12, 12, 12, 6);
        root.Margin = Padding.Empty;
        root.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;

        ConfigureTextLabel(_automationStatusLabel, LocalizationService.Text("AutomationStatus"));
        ConfigureTextLabel(_idleThresholdLabel, LocalizationService.Text("IdleSwitchDelay"));
        ConfigureTextLabel(_activeCheckIntervalLabel, LocalizationService.Text("ActiveCheckInterval"));
        ConfigureTextLabel(_idleCheckIntervalLabel, LocalizationService.Text("IdleCheckInterval"));
        ConfigureTextLabel(_activePlanLabel, LocalizationService.Text("ActivePowerPlan"));
        ConfigureTextLabel(_idlePlanLabel, LocalizationService.Text("IdlePowerPlan"));
        ConfigureTextLabel(_idleProtectionLabel, LocalizationService.Text("IdleProtection"));
        ConfigureTextLabel(_startupLabel, LocalizationService.Text("Startup"));
        ConfigureTextLabel(_systemNotificationsLabel, LocalizationService.Text("SystemNotifications"));
        ConfigureTextLabel(_languageLabel, LocalizationService.Text("Language"));
        ConfigureTextLabel(_idleThresholdUnitLabel, LocalizationService.Text("Seconds"));
        ConfigureTextLabel(_activeCheckIntervalUnitLabel, LocalizationService.Text("Seconds"));
        ConfigureTextLabel(_idleCheckIntervalUnitLabel, LocalizationService.Text("Seconds"));

        _idleThresholdInput.Minimum = AppConfig.MinIdleThresholdSeconds;
        _idleThresholdInput.Maximum = AppConfig.MaxIdleThresholdSeconds;
        _idleThresholdInput.DecimalPlaces = 0;
        _idleThresholdInput.ThousandsSeparator = false;
        _idleThresholdInput.Dock = DockStyle.Fill;
        _idleThresholdInput.MinimumSize = new Size(NumericInputMinimumWidth, 0);

        ConfigureCheckIntervalInput(_activeCheckIntervalInput);
        ConfigureCheckIntervalInput(_idleCheckIntervalInput);

        _automationStatusValueLabel.AutoSize = true;
        _automationStatusValueLabel.AutoEllipsis = false;
        _automationStatusValueLabel.Anchor = AnchorStyles.Left;
        _automationStatusValueLabel.TextAlign = ContentAlignment.MiddleLeft;

        ConfigureComboBox(_activePlanCombo, powerPlans);
        ConfigureComboBox(_idlePlanCombo, powerPlans);
        AttachPlanToolTip(_activePlanCombo);
        AttachPlanToolTip(_idlePlanCombo);

        _languageCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _languageCombo.Dock = DockStyle.Fill;
        _languageCombo.MinimumSize = new Size(SelectionInputMinimumWidth, 0);
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

        var idleProtectionOptions = _idleProtectionOptions;
        idleProtectionOptions.AutoSize = true;
        idleProtectionOptions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        idleProtectionOptions.Dock = DockStyle.Fill;
        idleProtectionOptions.ColumnCount = 1;
        idleProtectionOptions.RowCount = 2;
        idleProtectionOptions.Margin = Padding.Empty;
        idleProtectionOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        idleProtectionOptions.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        idleProtectionOptions.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        idleProtectionOptions.Controls.Add(_preventIdleOnExecutionStateCheckBox, 0, 0);
        idleProtectionOptions.Controls.Add(_preventIdleOnFullscreenCheckBox, 0, 1);

        ArrangeSettingsControls(compact: false);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Right,
            WrapContents = false,
            Margin = Padding.Empty
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

        var buttonBar = _buttonBar;
        buttonBar.Dock = DockStyle.Fill;
        buttonBar.AutoSize = true;
        buttonBar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        buttonBar.ColumnCount = 2;
        buttonBar.RowCount = 1;
        buttonBar.Padding = new Padding(12, 6, 12, 12);
        buttonBar.Margin = Padding.Empty;
        buttonBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttonBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttonBar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        buttonBar.Controls.Add(githubButton, 0, 0);
        buttonBar.Controls.Add(buttons, 1, 0);

        _settingsScrollPanel.Dock = DockStyle.Fill;
        _settingsScrollPanel.AutoScroll = true;
        _settingsScrollPanel.Controls.Add(root);
        _settingsScrollPanel.ClientSizeChanged += (_, _) => UpdateScrollableSettingsLayout();

        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        shell.Controls.Add(_settingsScrollPanel, 0, 0);
        shell.Controls.Add(buttonBar, 0, 1);

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        Controls.Add(shell);
    }

    private static void ConfigureCheckIntervalInput(NumericUpDown input)
    {
        input.Minimum = AppConfig.MinCheckIntervalSeconds;
        input.Maximum = AppConfig.MaxCheckIntervalSeconds;
        input.DecimalPlaces = 0;
        input.ThousandsSeparator = false;
        input.Dock = DockStyle.Fill;
        input.MinimumSize = new Size(NumericInputMinimumWidth, 0);
    }

    private static void ConfigureTextLabel(Label label, string text)
    {
        label.Text = text;
        label.AutoSize = true;
        label.Anchor = AnchorStyles.Left;
        label.TextAlign = ContentAlignment.MiddleLeft;
    }

    private void ArrangeSettingsControls(bool compact)
    {
        if (_compactSettingsLayout == compact && _root.Controls.Count > 0)
        {
            return;
        }

        _root.SuspendLayout();
        try
        {
            _root.Controls.Clear();
            _root.ColumnStyles.Clear();
            _root.RowStyles.Clear();

            if (compact)
            {
                ArrangeCompactSettingsControls();
            }
            else
            {
                ArrangeWideSettingsControls();
            }

            _compactSettingsLayout = compact;
        }
        finally
        {
            _root.ResumeLayout(performLayout: false);
        }
    }

    private void ArrangeWideSettingsControls()
    {
        _root.ColumnCount = 3;
        _root.RowCount = 10;
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        AddAutoSizeRows(_root, 10);

        _root.Controls.Add(_automationStatusLabel, 0, 0);
        _root.Controls.Add(_automationStatusValueLabel, 1, 0);
        _root.SetColumnSpan(_automationStatusValueLabel, 2);

        _root.Controls.Add(_idleThresholdLabel, 0, 1);
        _root.Controls.Add(_idleThresholdInput, 1, 1);
        _root.Controls.Add(_idleThresholdUnitLabel, 2, 1);

        _root.Controls.Add(_activeCheckIntervalLabel, 0, 2);
        _root.Controls.Add(_activeCheckIntervalInput, 1, 2);
        _root.Controls.Add(_activeCheckIntervalUnitLabel, 2, 2);

        _root.Controls.Add(_idleCheckIntervalLabel, 0, 3);
        _root.Controls.Add(_idleCheckIntervalInput, 1, 3);
        _root.Controls.Add(_idleCheckIntervalUnitLabel, 2, 3);

        _root.Controls.Add(_activePlanLabel, 0, 4);
        _root.Controls.Add(_activePlanCombo, 1, 4);
        _root.SetColumnSpan(_activePlanCombo, 2);

        _root.Controls.Add(_idlePlanLabel, 0, 5);
        _root.Controls.Add(_idlePlanCombo, 1, 5);
        _root.SetColumnSpan(_idlePlanCombo, 2);

        _root.Controls.Add(_idleProtectionLabel, 0, 6);
        _root.Controls.Add(_idleProtectionOptions, 1, 6);
        _root.SetColumnSpan(_idleProtectionOptions, 2);

        _root.Controls.Add(_startupLabel, 0, 7);
        _root.Controls.Add(_autoStartCheckBox, 1, 7);
        _root.SetColumnSpan(_autoStartCheckBox, 2);

        _root.Controls.Add(_systemNotificationsLabel, 0, 8);
        _root.Controls.Add(_notificationsEnabledCheckBox, 1, 8);
        _root.SetColumnSpan(_notificationsEnabledCheckBox, 2);

        _root.Controls.Add(_languageLabel, 0, 9);
        _root.Controls.Add(_languageCombo, 1, 9);
        _root.SetColumnSpan(_languageCombo, 2);
    }

    private void ArrangeCompactSettingsControls()
    {
        _root.ColumnCount = 2;
        _root.RowCount = 20;
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        AddAutoSizeRows(_root, 20);

        var row = 0;
        AddCompactSpanningRow(_automationStatusLabel, row++);
        AddCompactSpanningRow(_automationStatusValueLabel, row++);

        AddCompactSpanningRow(_idleThresholdLabel, row++);
        _root.Controls.Add(_idleThresholdInput, 0, row);
        _root.Controls.Add(_idleThresholdUnitLabel, 1, row++);

        AddCompactSpanningRow(_activeCheckIntervalLabel, row++);
        _root.Controls.Add(_activeCheckIntervalInput, 0, row);
        _root.Controls.Add(_activeCheckIntervalUnitLabel, 1, row++);

        AddCompactSpanningRow(_idleCheckIntervalLabel, row++);
        _root.Controls.Add(_idleCheckIntervalInput, 0, row);
        _root.Controls.Add(_idleCheckIntervalUnitLabel, 1, row++);

        AddCompactSpanningRow(_activePlanLabel, row++);
        AddCompactSpanningRow(_activePlanCombo, row++);

        AddCompactSpanningRow(_idlePlanLabel, row++);
        AddCompactSpanningRow(_idlePlanCombo, row++);

        AddCompactSpanningRow(_idleProtectionLabel, row++);
        AddCompactSpanningRow(_idleProtectionOptions, row++);

        AddCompactSpanningRow(_startupLabel, row++);
        AddCompactSpanningRow(_autoStartCheckBox, row++);

        AddCompactSpanningRow(_systemNotificationsLabel, row++);
        AddCompactSpanningRow(_notificationsEnabledCheckBox, row++);

        AddCompactSpanningRow(_languageLabel, row++);
        AddCompactSpanningRow(_languageCombo, row);
    }

    private void AddCompactSpanningRow(Control control, int row)
    {
        _root.Controls.Add(control, 0, row);
        _root.SetColumnSpan(control, 2);
    }

    private static void AddAutoSizeRows(TableLayoutPanel panel, int count)
    {
        for (var row = 0; row < count; row++)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }
    }

    private Size LayoutSettingsAtWidth(int requestedWidth)
    {
        var contentWidth = Math.Max(1, requestedWidth);
        var compact = DpiLayoutPolicy.ShouldUseCompactSettingsLayout(DeviceDpi, contentWidth);
        ArrangeSettingsControls(compact);
        ConfigureWrappingConstraints(contentWidth, compact);

        _root.MinimumSize = new Size(contentWidth, 0);
        _root.MaximumSize = new Size(contentWidth, 0);
        _root.Width = contentWidth;
        _root.PerformLayout();

        var preferredSize = _root.GetPreferredSize(new Size(contentWidth, 0));
        preferredSize = new Size(contentWidth, Math.Max(1, preferredSize.Height));
        _root.Size = preferredSize;
        return preferredSize;
    }

    private void ConfigureWrappingConstraints(int contentWidth, bool compact)
    {
        var innerWidth = Math.Max(1, contentWidth - _root.Padding.Horizontal);
        var settingLabels = new[]
        {
            _automationStatusLabel,
            _idleThresholdLabel,
            _activeCheckIntervalLabel,
            _idleCheckIntervalLabel,
            _activePlanLabel,
            _idlePlanLabel,
            _idleProtectionLabel,
            _startupLabel,
            _systemNotificationsLabel,
            _languageLabel
        };

        foreach (var label in settingLabels)
        {
            label.MaximumSize = compact
                ? new Size(Math.Max(1, innerWidth - label.Margin.Horizontal), 0)
                : Size.Empty;
        }

        var firstColumnWidth = compact
            ? 0
            : settingLabels.Max(label => label.GetPreferredSize(Size.Empty).Width + label.Margin.Horizontal);
        var valueAreaWidth = Math.Max(1, innerWidth - firstColumnWidth);

        SetWrappingWidth(_automationStatusValueLabel, valueAreaWidth);
        SetWrappingWidth(_autoStartCheckBox, valueAreaWidth);
        SetWrappingWidth(_notificationsEnabledCheckBox, valueAreaWidth);
        SetWrappingWidth(_preventIdleOnExecutionStateCheckBox, valueAreaWidth);
        SetWrappingWidth(_preventIdleOnFullscreenCheckBox, valueAreaWidth);
        _idleProtectionOptions.MaximumSize = new Size(valueAreaWidth, 0);
    }

    private static void SetWrappingWidth(Control control, int availableWidth)
    {
        control.MaximumSize = new Size(
            Math.Max(1, availableWidth - control.Margin.Horizontal),
            0);
    }

    private void UpdateScrollableSettingsLayout()
    {
        if (_updatingScrollableLayout ||
            _settingsScrollPanel.ClientSize.Width <= 0 ||
            _settingsScrollPanel.ClientSize.Height <= 0)
        {
            return;
        }

        _updatingScrollableLayout = true;
        try
        {
            var viewportSize = _settingsScrollPanel.ClientSize;
            var contentSize = LayoutSettingsAtWidth(viewportSize.Width);

            if (!_settingsScrollPanel.VerticalScroll.Visible &&
                contentSize.Height > viewportSize.Height)
            {
                var contentWidth = DpiLayoutPolicy.ReserveVerticalScrollBar(
                    viewportSize.Width,
                    SystemInformation.VerticalScrollBarWidth);
                contentSize = LayoutSettingsAtWidth(contentWidth);
            }

            _settingsScrollPanel.AutoScrollMinSize = new Size(0, contentSize.Height);

            var scrollPosition = _settingsScrollPanel.AutoScrollPosition;
            if (scrollPosition.X != 0)
            {
                _settingsScrollPanel.AutoScrollPosition = new Point(0, -scrollPosition.Y);
            }
        }
        finally
        {
            _updatingScrollableLayout = false;
        }
    }

    private void ApplyDpiAwareLayout(bool applyInitialSize)
    {
        var nonClientSize = new Size(
            Math.Max(0, Width - ClientSize.Width),
            Math.Max(0, Height - ClientSize.Height));
        var workingArea = Screen.FromControl(this).WorkingArea;
        var metrics = DpiLayoutPolicy.Calculate(DeviceDpi, workingArea.Size, nonClientSize);

        _updatingScrollableLayout = true;
        SuspendLayout();
        try
        {
            var widthConstraint = Math.Min(
                metrics.MaximumClientSize.Width,
                Math.Max(metrics.InitialClientSize.Width, metrics.MinimumClientSize.Width));
            var preferredSettingsSize = LayoutSettingsAtWidth(widthConstraint);
            _buttonBar.PerformLayout();
            var preferredButtonBarSize = _buttonBar.GetPreferredSize(new Size(widthConstraint, 0));
            _settingsScrollPanel.AutoScrollMinSize = new Size(0, preferredSettingsSize.Height);
            MinimumSize = new Size(
                metrics.MinimumClientSize.Width + nonClientSize.Width,
                metrics.MinimumClientSize.Height + nonClientSize.Height);

            if (applyInitialSize)
            {
                var preferredClientSize = new Size(
                    Math.Max(preferredSettingsSize.Width, preferredButtonBarSize.Width),
                    preferredSettingsSize.Height + preferredButtonBarSize.Height);
                ClientSize = DpiLayoutPolicy.FitInitialClientSize(metrics, preferredClientSize);
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
            _updatingScrollableLayout = false;
        }

        UpdateScrollableSettingsLayout();
    }

    private void LoadValues(AppConfig config, IReadOnlyList<PowerPlan> powerPlans)
    {
        _idleThresholdInput.Value = Math.Clamp(
            config.IdleThresholdSeconds,
            AppConfig.MinIdleThresholdSeconds,
            AppConfig.MaxIdleThresholdSeconds);
        _activeCheckIntervalInput.Value = Math.Clamp(
            config.ActiveCheckIntervalSeconds,
            AppConfig.MinCheckIntervalSeconds,
            AppConfig.MaxCheckIntervalSeconds);
        _idleCheckIntervalInput.Value = Math.Clamp(
            config.IdleCheckIntervalSeconds,
            AppConfig.MinCheckIntervalSeconds,
            AppConfig.MaxCheckIntervalSeconds);

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
        comboBox.MinimumSize = new Size(SelectionInputMinimumWidth, 0);
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
        SavedConfig.ActiveCheckIntervalSeconds = (int)_activeCheckIntervalInput.Value;
        SavedConfig.IdleCheckIntervalSeconds = (int)_idleCheckIntervalInput.Value;
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

        if (_activeCheckIntervalInput.Value < AppConfig.MinCheckIntervalSeconds ||
            _activeCheckIntervalInput.Value > AppConfig.MaxCheckIntervalSeconds ||
            _idleCheckIntervalInput.Value < AppConfig.MinCheckIntervalSeconds ||
            _idleCheckIntervalInput.Value > AppConfig.MaxCheckIntervalSeconds)
        {
            MessageBox.Show(
                LocalizationService.Format(
                    "CheckIntervalRangeValidation",
                    AppConfig.MinCheckIntervalSeconds,
                    AppConfig.MaxCheckIntervalSeconds),
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
