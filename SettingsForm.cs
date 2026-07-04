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
    private readonly AppConfig _originalConfig;

    public SettingsForm(AppConfig config, IReadOnlyList<PowerPlan> powerPlans)
    {
        _originalConfig = config.Clone();
        SavedConfig = config.Clone();

        Text = "AutoPowerMode 设置";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(560, 270);

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

        _idleThresholdInput.Minimum = AppConfig.MinIdleThresholdMinutes;
        _idleThresholdInput.Maximum = AppConfig.MaxIdleThresholdMinutes;
        _idleThresholdInput.DecimalPlaces = 0;
        _idleThresholdInput.ThousandsSeparator = false;
        _idleThresholdInput.Dock = DockStyle.Fill;

        _checkIntervalInput.Minimum = AppConfig.MinCheckIntervalMinutes;
        _checkIntervalInput.Maximum = AppConfig.MaxCheckIntervalMinutes;
        _checkIntervalInput.DecimalPlaces = 0;
        _checkIntervalInput.ThousandsSeparator = false;
        _checkIntervalInput.Dock = DockStyle.Fill;

        ConfigureComboBox(_activePlanCombo, powerPlans);
        ConfigureComboBox(_idlePlanCombo, powerPlans);

        _autoStartCheckBox.Text = "启用当前用户开机自启";
        _autoStartCheckBox.AutoSize = true;
        _autoStartCheckBox.Anchor = AnchorStyles.Left;

        AddText(root, "空闲多久后切换到节能模式", 0, 0);
        root.Controls.Add(_idleThresholdInput, 1, 0);
        AddText(root, "分钟", 2, 0);

        AddText(root, "检测间隔", 0, 1);
        root.Controls.Add(_checkIntervalInput, 1, 1);
        AddText(root, "分钟", 2, 1);

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
            config.IdleThresholdMinutes,
            AppConfig.MinIdleThresholdMinutes,
            AppConfig.MaxIdleThresholdMinutes);

        _checkIntervalInput.Value = Math.Clamp(
            config.CheckIntervalMinutes,
            AppConfig.MinCheckIntervalMinutes,
            AppConfig.MaxCheckIntervalMinutes);

        SelectPlan(_activePlanCombo, config.ActivePowerPlanGuid, powerPlans);
        SelectPlan(_idlePlanCombo, config.IdlePowerPlanGuid, powerPlans);
        _autoStartCheckBox.Checked = config.AutoStart;
    }

    private static void ConfigureComboBox(ComboBox comboBox, IReadOnlyList<PowerPlan> powerPlans)
    {
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBox.DisplayMember = nameof(PowerPlan.DisplayName);
        comboBox.ValueMember = nameof(PowerPlan.Guid);
        comboBox.Dock = DockStyle.Fill;
        comboBox.DataSource = powerPlans.ToList();
        comboBox.Enabled = powerPlans.Count > 0;
    }

    private static void SelectPlan(ComboBox comboBox, string guid, IReadOnlyList<PowerPlan> powerPlans)
    {
        var index = powerPlans
            .Select((plan, i) => new { plan, i })
            .FirstOrDefault(item => string.Equals(item.plan.Guid, guid, StringComparison.OrdinalIgnoreCase))
            ?.i;

        if (index.HasValue)
        {
            comboBox.SelectedIndex = index.Value;
        }
        else if (powerPlans.Count > 0)
        {
            comboBox.SelectedIndex = 0;
        }
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
        SavedConfig.IdleThresholdMinutes = (int)_idleThresholdInput.Value;
        SavedConfig.CheckIntervalMinutes = (int)_checkIntervalInput.Value;
        SavedConfig.ActivePowerPlanGuid = activePlan?.Guid ?? string.Empty;
        SavedConfig.IdlePowerPlanGuid = idlePlan?.Guid ?? string.Empty;
        SavedConfig.AutoStart = _autoStartCheckBox.Checked;
        SavedConfig.Normalize();

        DialogResult = DialogResult.OK;
        Close();
    }

    private bool ValidateInputs()
    {
        if (_idleThresholdInput.Value < AppConfig.MinIdleThresholdMinutes ||
            _idleThresholdInput.Value > AppConfig.MaxIdleThresholdMinutes)
        {
            MessageBox.Show(
                $"空闲时间必须在 {AppConfig.MinIdleThresholdMinutes} 到 {AppConfig.MaxIdleThresholdMinutes} 分钟之间。",
                "AutoPowerMode",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        if (_checkIntervalInput.Value < AppConfig.MinCheckIntervalMinutes ||
            _checkIntervalInput.Value > AppConfig.MaxCheckIntervalMinutes)
        {
            MessageBox.Show(
                $"检测间隔必须在 {AppConfig.MinCheckIntervalMinutes} 到 {AppConfig.MaxCheckIntervalMinutes} 分钟之间。",
                "AutoPowerMode",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        if (_activePlanCombo.SelectedItem is not PowerPlan)
        {
            MessageBox.Show(
                "请选择活跃时使用的电源计划。",
                "AutoPowerMode",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        if (_idlePlanCombo.SelectedItem is not PowerPlan)
        {
            MessageBox.Show(
                "请选择空闲时使用的电源计划。",
                "AutoPowerMode",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }
}
