using System.Drawing;
using System.Windows.Forms;

namespace AutoPowerMode;

internal sealed class DiagnosticForm : Form
{
    private readonly Func<DiagnosticSnapshot> _snapshotProvider;
    private readonly Action _openLogDirectory;
    private readonly Action _redetectPowerPlans;
    private readonly Func<Task> _restoreAutoControlAsync;
    private readonly TextBox _diagnosticTextBox = new();

    public DiagnosticForm(
        Func<DiagnosticSnapshot> snapshotProvider,
        Action openLogDirectory,
        Action redetectPowerPlans,
        Func<Task> restoreAutoControlAsync)
    {
        _snapshotProvider = snapshotProvider;
        _openLogDirectory = openLogDirectory;
        _redetectPowerPlans = redetectPowerPlans;
        _restoreAutoControlAsync = restoreAutoControlAsync;

        Text = "AutoPowerMode 诊断";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimizeBox = false;
        ClientSize = new Size(720, 520);

        BuildLayout();
        RefreshSnapshot();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 2,
            ColumnCount = 1
        };

        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        _diagnosticTextBox.Dock = DockStyle.Fill;
        _diagnosticTextBox.Multiline = true;
        _diagnosticTextBox.ReadOnly = true;
        _diagnosticTextBox.ScrollBars = ScrollBars.Vertical;
        _diagnosticTextBox.Font = new Font(FontFamily.GenericMonospace, 9f);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        var closeButton = CreateButton("关闭");
        closeButton.Click += (_, _) => Close();

        var restoreButton = CreateButton("恢复自动控制");
        restoreButton.Click += async (_, _) =>
        {
            await _restoreAutoControlAsync();
            RefreshSnapshot();
        };

        var redetectButton = CreateButton("重新检测电源计划");
        redetectButton.Click += (_, _) =>
        {
            _redetectPowerPlans();
            RefreshSnapshot();
        };

        var openLogButton = CreateButton("打开日志目录");
        openLogButton.Click += (_, _) => _openLogDirectory();

        var copyButton = CreateButton("复制诊断信息");
        copyButton.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(_diagnosticTextBox.Text);
            }
            catch (Exception ex)
            {
                Logger.Error("复制诊断信息失败。", ex);
                MessageBox.Show(
                    "复制诊断信息失败，详情请查看日志。",
                    AppInfo.DisplayName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        };

        buttons.Controls.Add(closeButton);
        buttons.Controls.Add(restoreButton);
        buttons.Controls.Add(redetectButton);
        buttons.Controls.Add(openLogButton);
        buttons.Controls.Add(copyButton);

        root.Controls.Add(_diagnosticTextBox, 0, 0);
        root.Controls.Add(buttons, 0, 1);
        Controls.Add(root);
    }

    private static Button CreateButton(string text)
    {
        return new Button
        {
            Text = text,
            AutoSize = true,
            Height = 32
        };
    }

    public void RefreshSnapshot()
    {
        _diagnosticTextBox.Text = _snapshotProvider().ToDisplayText();
    }
}
