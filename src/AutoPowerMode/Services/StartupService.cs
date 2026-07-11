using Microsoft.Win32;
using System.Windows.Forms;

namespace AutoPowerMode;

public sealed class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AutoPowerMode";

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var value = key?.GetValue(ValueName) as string;
            return StartupRegistrationValue.IsEnabledValue(value, GetExecutablePath());
        }
        catch (Exception ex)
        {
            Logger.Error("读取开机自启状态失败。", ex);
            return false;
        }
    }

    public bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                Logger.Error("注册表 Run 项打开失败，无法修改开机自启。");
                return false;
            }

            if (enabled)
            {
                key.SetValue(ValueName, StartupRegistrationValue.QuotePath(GetExecutablePath()), RegistryValueKind.String);
                Logger.Info("开机自启已开启。");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                Logger.Info("开机自启已关闭。");
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error("注册表写入失败，无法修改开机自启。", ex);
            return false;
        }
    }

    private static string GetExecutablePath()
    {
        return Environment.ProcessPath ?? Application.ExecutablePath;
    }

}
