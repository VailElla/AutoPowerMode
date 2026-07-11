using System.Windows.Forms;

namespace AutoPowerMode;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        LocalizationService.UsePreference(AppLanguagePreference.System);

        if (!OperatingSystem.IsWindows())
        {
            MessageBox.Show(
                LocalizationService.Text("WindowsOnly"),
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
                $"{LocalizationService.Text("UiThreadError")}{Environment.NewLine}{Logger.GetLogLocationMessage()}",
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
                        LocalizationService.Text("ExistingInstanceOpenFailed"),
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
                $"{LocalizationService.Text("StartupFailed")}{Environment.NewLine}{Logger.GetLogLocationMessage()}",
                AppInfo.DisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
