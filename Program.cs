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
                "AutoPowerMode",
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
                "程序遇到异常，详情请查看日志。",
                "AutoPowerMode",
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

        Application.Run(new TrayAppContext());
    }
}
