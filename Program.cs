using System.Windows.Forms;

namespace AutoPowerMode;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        if (!OperatingSystem.IsWindows())
        {
            MessageBox.Show(
                "AutoPowerMode 只能在 Windows 上运行。",
                AppInfo.DisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, args) =>
        {
            Logger.Error("WinForms UI 线程异常。", args.Exception);
            MessageBox.Show(
                $"程序遇到异常，详情请查看日志。{Environment.NewLine}{Logger.GetLogLocationMessage()}",
                AppInfo.DisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                Logger.Error("程序未处理异常。", exception);
            }
            else
            {
                Logger.Error($"程序未处理异常：{args.ExceptionObject}");
            }
        };

        try
        {
            using var singleInstanceService = SingleInstanceService.Create();
            if (!singleInstanceService.IsFirstInstance)
            {
                if (!singleInstanceService.SignalExistingInstance())
                {
                    MessageBox.Show(
                        "AutoPowerMode 已在运行，但无法自动打开现有窗口。请从系统托盘打开设置。",
                        AppInfo.DisplayName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                return;
            }

            using var trayAppContext = new TrayAppContext();
            singleInstanceService.StartActivationServer(trayAppContext.OpenSettingsFromExternalRequest);
            Application.Run(trayAppContext);
        }
        catch (Exception ex)
        {
            Logger.Error("程序启动失败。", ex);
            MessageBox.Show(
                $"程序启动失败，详情请查看日志。{Environment.NewLine}{Logger.GetLogLocationMessage()}",
                AppInfo.DisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
