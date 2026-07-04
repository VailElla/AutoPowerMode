using System.Drawing;
using System.Windows.Forms;

namespace AutoPowerMode;

public sealed class SettingsForm : Form
{
    private readonly NumericUpDown _idleThresholdInput = new();
    private readonly NumericUpDown _checkIntervalInput = new();
    private readonly ComboBox _activePlanCombo = new();
    private readonly ComboBox _idlePlanCombo = new();
    private readonly CheckBox _autoStartCheckBox = new();
    private readonly ToolTip _powerPlanToolTip = new();
    private readonly AppConfig _originalConfig;

    public SettingsForm(AppConfig config, IReadOnlyList<PowerPlan> powerPlans)
    {
        _originalConfig = config.Clone();
        SavedConfig = config.Clone();

        Text = $"{AppInfo.DisplayName} 设置";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(640, 290);

        BuildLayout(powerPlans);
        LoadValues(config, powerPlans);
    }

    public AppConfig SavedConfig { get; private set; }

    private void BuildLayout(IReadOnlyList<PowerPlan> powerPlans)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 3,
            RowCount = 6
        };

        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));

        for (var i = 0; i < 5; i++)
        {
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        }

        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _idleThresholdInput.Minimum = AppConfig.MinIdleThresholdSeconds;
        _idleThresholdInput.Maximum = AppConfig.MaxIdleThresholdSeconds;
        _idleThresholdInput.DecimalPlaces = 0;
        _idleThresholdInput.ThousandsSeparator = false;
        _idleThresholdInput.Dock = DockStyle.Fill;

        _checkIntervalInput.Minimum = AppConfig.MinCheckIntervalSeconds;
        _checkIntervalInput.Maximum = AppConfig.MaxCheckIntervalSeconds;
        _checkIntervalInput.DecimalPlaces = 0;
        _checkIntervalInput.ThousandsSeparator = false;
        _checkIntervalInput.Dock = DockStyle.Fill;

        ConfigureComboBox(_activePlanCombo, powerPlans);
        ConfigureComboBox(_idlePlanCombo, powerPlans);
        AttachPlanToolTip(_activePlanCombo);
        AttachPlanToolTip(_idlePlanCombo);

        _autoStartCheckBox.Text = "启用当前用户开机自启";
        _autoStartCheckBox.AutoSize = true;
        _autoStartCheckBox.Anchor = AnchorStyles.Left;

        AddText(root, "空闲多久后切换到节能模式", 0, 0);
        root.Controls.Add(_idleThresholdInput, 1, 0);
        AddText(root, "秒", 2, 0);

        AddText(root, "检测间隔", 0, 1);
        root.Controls.Add(_checkIntervalInput, 1, 1);
        AddText(root, "秒", 2, 1);

        AddText(root, "活跃时电源计划", 0, 2);
        root.Controls.Add(_activePlanCombo, 1, 2);
        root.SetColumnSpan(_activePlanCombo, 2);

        AddText(root, "空闲时电源计划", 0, 3);
        root.Controls.Add(_idlePlanCombo, 1, 3);
        root.SetColumnSpan(_idlePlanCombo, 2);

        AddText(root, "开机自启", 0, 4);
        root.Controls.Add(_autoStartCheckBox, 1, 4);
        root.SetColumnSpan(_autoStartCheckBox, 2);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill
        };

        var saveButton = new Button
        {
            Text = "保存",
            AutoSize = true,
            DialogResult = DialogResult.None
        };

        var cancelButton = new Button
        {
            Text = "取消",
            AutoSize = true,
            DialogResult = DialogResult.Cancel
        };

        saveButton.Click += SaveButton_Click;

        buttons.Controls.Add(saveButton);
        buttons.Controls.Add(cancelButton);

        root.Controls.Add(buttons, 0, 5);
        root.SetColumnSpan(buttons, 3);

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        Controls.Add(root);
    }

    private void LoadValues(AppConfig config, IReadOnlyList<PowerPlan> powerPlans)
    {
        _idleThresholdInput.Value = Math.Clamp(
            config.IdleThresholdSeconds,
            AppConfig.MinIdleThresholdSeconds,
            AppConfig.MaxIdleThresholdSeconds);

        _checkIntervalInput.Value = Math.Clamp(
            config.CheckIntervalSeconds,
            AppConfig.MinCheckIntervalSeconds,
            AppConfig.MaxCheckIntervalSeconds);

        SelectPlan(_activePlanCombo, config.ActivePowerPlanGuid, preferActivePlan: true);
        SelectPlan(_idlePlanCombo, config.IdlePowerPlanGuid, preferActivePlan: false);
        UpdatePlanToolTip(_activePlanCombo);
        UpdatePlanToolTip(_idlePlanCombo);
        _autoStartCheckBox.Checked = config.AutoStart;
    }

    private static void ConfigureComboBox(ComboBox comboBox, IReadOnlyList<PowerPlan> powerPlans)
    {
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBox.Dock = DockStyle.Fill;
        comboBox.IntegralHeight = false;
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
        _powerPlanToolTip.SetToolTip(comboBox, text);
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
        SavedConfig.CheckIntervalSeconds = (int)_checkIntervalInput.Value;
        SavedConfig.ActivePowerPlanGuid = activePlan?.Guid ?? string.Empty;
        SavedConfig.IdlePowerPlanGuid = idlePlan?.Guid ?? string.Empty;
        SavedConfig.AutoStart = _autoStartCheckBox.Checked;
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
                $"空闲时间必须在 {AppConfig.MinIdleThresholdSeconds} 到 {AppConfig.MaxIdleThresholdSeconds} 秒之间。",
                AppInfo.DisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        if (_checkIntervalInput.Value < AppConfig.MinCheckIntervalSeconds ||
            _checkIntervalInput.Value > AppConfig.MaxCheckIntervalSeconds)
        {
            MessageBox.Show(
                $"检测间隔必须在 {AppConfig.MinCheckIntervalSeconds} 到 {AppConfig.MaxCheckIntervalSeconds} 秒之间。",
                AppInfo.DisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        if (_activePlanCombo.SelectedItem is not PowerPlan)
        {
            MessageBox.Show(
                "请选择活跃时使用的电源计划。",
                AppInfo.DisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        if (_idlePlanCombo.SelectedItem is not PowerPlan)
        {
            MessageBox.Show(
                "请选择空闲时使用的电源计划。",
                AppInfo.DisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _powerPlanToolTip.Dispose();
        }

        base.Dispose(disposing);
    }
}
