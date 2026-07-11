using System.Globalization;

namespace AutoPowerMode;

public enum AppLanguage
{
    English,
    SimplifiedChinese
}

public static class LocalizationService
{
    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>
    {
        ["WindowsOnly"] = "AutoPowerMode can only run on Windows.",
        ["UiThreadError"] = "The application encountered an error. See the log for details.",
        ["StartupFailed"] = "The application failed to start. See the log for details.",
        ["ExistingInstanceOpenFailed"] = "AutoPowerMode is already running, but the existing window could not be opened automatically. Open Settings from the system tray.",
        ["LogLocation"] = "Log: {0}",
        ["PortableLogLocation"] = "Portable log: {0}",

        ["SettingsTitle"] = "{0} Settings",
        ["AutomationStatus"] = "Automation status",
        ["IdleSwitchDelay"] = "Switch to power saver after idle",
        ["Seconds"] = "seconds",
        ["MonitoringSchedule"] = "Detection schedule",
        ["MonitoringScheduleValue"] = "Active: every {0} sec; idle plan: every {1} sec",
        ["ActivePowerPlan"] = "Power plan while active",
        ["IdlePowerPlan"] = "Power plan while idle",
        ["IdleProtection"] = "Idle protection",
        ["PreventIdleOnExecutionState"] = "Honor app keep-awake requests",
        ["PreventIdleOnExecutionStateHint"] = "Do not apply the idle rule while another program declares ES_SYSTEM_REQUIRED, ES_DISPLAY_REQUIRED, or ES_AWAYMODE_REQUIRED.",
        ["PreventIdleOnFullscreen"] = "Protect fullscreen foreground apps",
        ["PreventIdleOnFullscreenHint"] = "Do not apply the idle rule while the current foreground window covers its entire monitor.",
        ["Startup"] = "Startup",
        ["SystemNotifications"] = "System notifications",
        ["Language"] = "Language",
        ["EnableAutoStart"] = "Start automatically for the current user",
        ["EnableNotifications"] = "Enable power mode notifications",
        ["LanguageSystem"] = "Follow system language",
        ["LanguageEnglish"] = "English",
        ["LanguageChinese"] = "Simplified Chinese",
        ["GitHubHomepage"] = "GitHub project page",
        ["Save"] = "Save",
        ["Cancel"] = "Cancel",
        ["IdleRangeValidation"] = "Idle time must be between {0} and {1} seconds.",
        ["SelectActivePlan"] = "Select the power plan to use while active.",
        ["SelectIdlePlan"] = "Select the power plan to use while idle.",
        ["OpenGitHubFailed"] = "Could not open the GitHub project page.",

        ["Diagnostics"] = "Diagnostics",
        ["Close"] = "Close",
        ["RestoreAutoControl"] = "Restore automatic control",
        ["RedetectPowerPlans"] = "Detect power plans again",
        ["OpenLogDirectory"] = "Open log folder",
        ["CopyDiagnostics"] = "Copy diagnostics",
        ["CopyDiagnosticsFailed"] = "Could not copy diagnostics. See the log for details.",
        ["DiagnosticsHeading"] = "AutoPowerMode diagnostics",
        ["ApplicationVersion"] = "Application version: {0}",
        ["CurrentState"] = "Current state: {0} / {1}",
        ["StatusDescription"] = "Status: {0}",
        ["CurrentPowerPlan"] = "Current power plan: {0}",
        ["ActivePlanConfig"] = "Active plan setting: {0}",
        ["IdlePlanConfig"] = "Idle plan setting: {0}",
        ["IdleTime"] = "Idle time: {0}",
        ["IdleThreshold"] = "Idle threshold: {0}",
        ["MonitoringScheduleDiagnostic"] = "Detection schedule: {0}",
        ["IdleProtectionSettings"] = "Idle protection settings: {0}",
        ["IdleProtectionSettingsValue"] = "execution state {0}; fullscreen {1}",
        ["CurrentIdleProtection"] = "Current idle protection: {0}",
        ["NotificationsValue"] = "System notifications: {0}",
        ["LastSwitch"] = "Last switch: {0}",
        ["LastPowerCfgError"] = "Last powercfg error: {0}",
        ["ConfigPath"] = "Configuration file: {0}",
        ["LogPath"] = "Log file: {0}",
        ["PortableLogPath"] = "Portable log file: {0}",

        ["Enabled"] = "On",
        ["Disabled"] = "Off",
        ["Unknown"] = "Unknown",
        ["None"] = "None",
        ["NotConfigured"] = "Not configured",
        ["PlanNotFound"] = "Not found ({0})",
        ["HoursMinutesSeconds"] = "{0} hr {1} min {2} sec",
        ["MinutesSeconds"] = "{0} min {1} sec",
        ["SecondsValue"] = "{0} sec",
        ["NotSwitchedYet"] = "No switch has been performed yet",

        ["VersionMenu"] = "Version: {0}",
        ["StatusMenu"] = "Current status: {0}",
        ["CurrentPlanMenu"] = "Current power plan: {0}",
        ["IdleThresholdMenu"] = "Idle threshold: {0} seconds",
        ["MonitoringScheduleMenu"] = "Detection: active {0} sec / idle {1} sec",
        ["ResumeSwitching"] = "Resume automatic switching",
        ["PauseSwitching"] = "Pause automatic switching",
        ["SwitchToActive"] = "Switch to the active power plan now",
        ["SwitchToIdle"] = "Switch to the idle power plan now",
        ["AutoStartMenu"] = "Start automatically: {0}",
        ["Settings"] = "Settings",
        ["Exit"] = "Exit",
        ["StatusPaused"] = "Automatic switching is paused by the user",
        ["StatusNotConfigured"] = "Power plans are not configured",
        ["StatusExternalOverride"] = "An external power plan is active; automatic switching is paused",
        ["StatusSwitchFailed"] = "Switch failed; open Diagnostics to see the powercfg error",
        ["StatusActive"] = "Active; using the active power plan",
        ["StatusIdle"] = "Idle; using the idle power plan",
        ["StatusWaiting"] = "Automatic switching is running; waiting for the next check",
        ["StatusIdleProtected"] = "Idle switch blocked: {0}",
        ["IdleProtectionExecutionStateReason"] = "another app requested system, display, or away-mode availability",
        ["IdleProtectionFullscreenReason"] = "the foreground window is fullscreen",
        ["IdleProtectionBothReasons"] = "an app requested availability and the foreground window is fullscreen",

        ["TargetPlanMissing"] = "Target power plan is not configured",
        ["TargetPowerPlan"] = "target power plan",
        ["ExternalOverrideStatus"] = "Skipped: external power plan override",
        ["ExternalChangeTitle"] = "External change detected",
        ["ExternalChangeMessage"] = "Current plan: \"{0}\". Automatic switching is paused.",
        ["SwitchSuccessTo"] = "Success: switched to {0}",
        ["SwitchSuccessFromTo"] = "Success: {0} -> {1}",
        ["SwitchFailedStatus"] = "Failed: switch or verification did not succeed",
        ["SwitchExceptionStatus"] = "Failed: power plan switch error",
        ["StartupSynchronized"] = "Startup sync: current plan is {0}",
        ["StartupNotificationTitle"] = "AutoPowerMode started",
        ["CurrentPlanNotification"] = "Current plan: \"{0}\".",
        ["ExternalActivatedStatus"] = "Detected: {0} was activated externally",
        ["PlanChangeDetectedTitle"] = "Power plan change detected",
        ["PlanChangeDetectedMessage"] = "\"{0}\" is already active; AutoPowerMode did not perform this switch.",
        ["NoSwitchNeededStatus"] = "No switch needed: {0} is already active",
        ["NoSwitchNeededTitle"] = "No switch needed",
        ["SwitchedToMessage"] = "Switched to \"{0}\".",
        ["SwitchedFromToMessage"] = "Switched from \"{0}\" to \"{1}\".",
        ["PowerModeSwitchedTitle"] = "Power mode switched",
        ["SwitchFailedTitle"] = "Power mode switch failed",
        ["SwitchFailedMessage"] = "Could not switch to \"{0}\". Open Diagnostics for details.",
        ["AutoStartChangeFailed"] = "Could not change the startup setting. See the log for details.",
        ["AutoStartStatePreserved"] = "Could not change the startup setting. The current registry state was preserved. See the log for details.",
        ["OpenLogFailed"] = "Could not open the log folder.",
        ["SaveAutoMatchedPlansFailed"] = "Could not save the automatically matched power plans.",
        ["PauseSaveFailed"] = "Could not save the pause state. It may not be restored next time.",
        ["AutoStartSaveFailed"] = "Could not save the startup setting. It may not be restored next time.",
        ["SettingsSaveFailed"] = "Could not save settings. This change may not be restored next time.",
        ["RestoreStateSaveFailed"] = "Could not save the restored automatic-control state.",
        ["SyncAutoStartSaveFailed"] = "Could not save the synchronized startup state.",
        ["ExitSaveFailed"] = "Could not save settings before exit. The last change may not be restored next time."
    };

    private static readonly IReadOnlyDictionary<string, string> SimplifiedChinese = new Dictionary<string, string>
    {
        ["WindowsOnly"] = "AutoPowerMode 只能在 Windows 上运行。",
        ["UiThreadError"] = "程序遇到异常，详情请查看日志。",
        ["StartupFailed"] = "程序启动失败，详情请查看日志。",
        ["ExistingInstanceOpenFailed"] = "AutoPowerMode 已在运行，但无法自动打开现有窗口。请从系统托盘打开设置。",
        ["LogLocation"] = "日志位置：{0}",
        ["PortableLogLocation"] = "便携目录日志：{0}",

        ["SettingsTitle"] = "{0} 设置",
        ["AutomationStatus"] = "自动切换状态",
        ["IdleSwitchDelay"] = "空闲后切换到节能模式",
        ["Seconds"] = "秒",
        ["MonitoringSchedule"] = "检测频率",
        ["MonitoringScheduleValue"] = "活跃：每 {0} 秒；空闲计划：每 {1} 秒",
        ["ActivePowerPlan"] = "活跃时电源计划",
        ["IdlePowerPlan"] = "空闲时电源计划",
        ["IdleProtection"] = "空闲误触保护",
        ["PreventIdleOnExecutionState"] = "其他程序请求保持唤醒时不切换",
        ["PreventIdleOnExecutionStateHint"] = "其他程序声明 ES_SYSTEM_REQUIRED、ES_DISPLAY_REQUIRED 或 ES_AWAYMODE_REQUIRED 时，不应用空闲规则。",
        ["PreventIdleOnFullscreen"] = "前台应用全屏时不切换",
        ["PreventIdleOnFullscreenHint"] = "当前前台窗口覆盖其所在显示器的完整区域时，不应用空闲规则。",
        ["Startup"] = "开机自启",
        ["SystemNotifications"] = "系统通知",
        ["Language"] = "语言",
        ["EnableAutoStart"] = "启用当前用户开机自启",
        ["EnableNotifications"] = "启用电源模式通知",
        ["LanguageSystem"] = "跟随系统语言",
        ["LanguageEnglish"] = "English",
        ["LanguageChinese"] = "简体中文",
        ["GitHubHomepage"] = "GitHub 项目主页",
        ["Save"] = "保存",
        ["Cancel"] = "取消",
        ["IdleRangeValidation"] = "空闲时间必须在 {0} 到 {1} 秒之间。",
        ["SelectActivePlan"] = "请选择活跃时使用的电源计划。",
        ["SelectIdlePlan"] = "请选择空闲时使用的电源计划。",
        ["OpenGitHubFailed"] = "无法打开 GitHub 项目主页。",

        ["Diagnostics"] = "诊断信息",
        ["Close"] = "关闭",
        ["RestoreAutoControl"] = "恢复自动控制",
        ["RedetectPowerPlans"] = "重新检测电源计划",
        ["OpenLogDirectory"] = "打开日志目录",
        ["CopyDiagnostics"] = "复制诊断信息",
        ["CopyDiagnosticsFailed"] = "复制诊断信息失败，详情请查看日志。",
        ["DiagnosticsHeading"] = "AutoPowerMode 诊断",
        ["ApplicationVersion"] = "应用版本：{0}",
        ["CurrentState"] = "当前状态：{0} / {1}",
        ["StatusDescription"] = "状态说明：{0}",
        ["CurrentPowerPlan"] = "当前电源计划：{0}",
        ["ActivePlanConfig"] = "活跃计划配置：{0}",
        ["IdlePlanConfig"] = "空闲计划配置：{0}",
        ["IdleTime"] = "空闲时间：{0}",
        ["IdleThreshold"] = "空闲阈值：{0}",
        ["MonitoringScheduleDiagnostic"] = "检测频率：{0}",
        ["IdleProtectionSettings"] = "空闲误触保护设置：{0}",
        ["IdleProtectionSettingsValue"] = "执行状态 {0}；全屏 {1}",
        ["CurrentIdleProtection"] = "当前生效的空闲保护：{0}",
        ["NotificationsValue"] = "系统通知：{0}",
        ["LastSwitch"] = "最近一次切换：{0}",
        ["LastPowerCfgError"] = "最近一次 powercfg 错误：{0}",
        ["ConfigPath"] = "配置文件位置：{0}",
        ["LogPath"] = "日志位置：{0}",
        ["PortableLogPath"] = "便携日志位置：{0}",

        ["Enabled"] = "已开启",
        ["Disabled"] = "已关闭",
        ["Unknown"] = "未知",
        ["None"] = "无",
        ["NotConfigured"] = "未配置",
        ["PlanNotFound"] = "未找到 ({0})",
        ["HoursMinutesSeconds"] = "{0} 小时 {1} 分 {2} 秒",
        ["MinutesSeconds"] = "{0} 分 {1} 秒",
        ["SecondsValue"] = "{0} 秒",
        ["NotSwitchedYet"] = "尚未切换",

        ["VersionMenu"] = "版本：{0}",
        ["StatusMenu"] = "当前状态：{0}",
        ["CurrentPlanMenu"] = "当前电源计划：{0}",
        ["IdleThresholdMenu"] = "空闲阈值：{0} 秒",
        ["MonitoringScheduleMenu"] = "检测频率：活跃 {0} 秒 / 空闲 {1} 秒",
        ["ResumeSwitching"] = "恢复自动切换",
        ["PauseSwitching"] = "暂停自动切换",
        ["SwitchToActive"] = "立即切换到高性能计划",
        ["SwitchToIdle"] = "立即切换到节能计划",
        ["AutoStartMenu"] = "开机自启：{0}",
        ["Settings"] = "设置",
        ["Exit"] = "退出",
        ["StatusPaused"] = "用户暂停自动切换",
        ["StatusNotConfigured"] = "电源计划未配置",
        ["StatusExternalOverride"] = "外部电源计划覆盖，自动切换已暂停",
        ["StatusSwitchFailed"] = "切换失败，请打开诊断信息查看 powercfg 错误",
        ["StatusActive"] = "活跃，使用高性能计划",
        ["StatusIdle"] = "空闲，使用节能计划",
        ["StatusWaiting"] = "自动切换正常运行，等待下一次检测",
        ["StatusIdleProtected"] = "已阻止空闲切换：{0}",
        ["IdleProtectionExecutionStateReason"] = "其他程序请求系统、显示器或离开模式保持可用",
        ["IdleProtectionFullscreenReason"] = "当前前台窗口为全屏",
        ["IdleProtectionBothReasons"] = "其他程序请求保持可用，且当前前台窗口为全屏",

        ["TargetPlanMissing"] = "目标电源计划未配置",
        ["TargetPowerPlan"] = "目标电源计划",
        ["ExternalOverrideStatus"] = "跳过：外部电源计划覆盖",
        ["ExternalChangeTitle"] = "检测到外部改动",
        ["ExternalChangeMessage"] = "当前计划：「{0}」。自动切换已暂停。",
        ["SwitchSuccessTo"] = "成功：已切换至{0}",
        ["SwitchSuccessFromTo"] = "成功：{0} -> {1}",
        ["SwitchFailedStatus"] = "失败：切换或状态确认未成功",
        ["SwitchExceptionStatus"] = "失败：电源计划切换异常",
        ["StartupSynchronized"] = "启动同步：当前为 {0}",
        ["StartupNotificationTitle"] = "AutoPowerMode 已启动",
        ["CurrentPlanNotification"] = "当前：「{0}」。",
        ["ExternalActivatedStatus"] = "检测：{0} 已由外部激活",
        ["PlanChangeDetectedTitle"] = "检测到电源计划改动",
        ["PlanChangeDetectedMessage"] = "当前已是「{0}」，并非本次自动切换。",
        ["NoSwitchNeededStatus"] = "无需切换：当前已是 {0}",
        ["NoSwitchNeededTitle"] = "无需切换",
        ["SwitchedToMessage"] = "已切换至「{0}」。",
        ["SwitchedFromToMessage"] = "已从「{0}」切换至「{1}」。",
        ["PowerModeSwitchedTitle"] = "电源模式已切换",
        ["SwitchFailedTitle"] = "电源模式切换失败",
        ["SwitchFailedMessage"] = "切换至「{0}」失败，请查看诊断信息。",
        ["AutoStartChangeFailed"] = "开机自启设置失败，详情请查看日志。",
        ["AutoStartStatePreserved"] = "开机自启设置失败，已保留当前注册表状态。详情请查看日志。",
        ["OpenLogFailed"] = "打开日志目录失败。",
        ["SaveAutoMatchedPlansFailed"] = "保存自动匹配到的电源计划失败。",
        ["PauseSaveFailed"] = "暂停状态保存失败，下次启动可能无法恢复当前暂停状态。",
        ["AutoStartSaveFailed"] = "开机自启设置保存失败，下次启动可能无法恢复当前设置。",
        ["SettingsSaveFailed"] = "设置保存失败，下次启动可能无法恢复本次修改。",
        ["RestoreStateSaveFailed"] = "恢复自动控制状态保存失败。",
        ["SyncAutoStartSaveFailed"] = "同步开机自启状态保存失败。",
        ["ExitSaveFailed"] = "退出前保存配置失败，下次启动可能无法恢复最后一次修改。"
    };

    public static AppLanguage CurrentLanguage { get; private set; } = ResolveLanguage(
        AppLanguagePreference.System,
        CultureInfo.CurrentUICulture);

    internal static bool ResourceKeysMatch =>
        English.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(SimplifiedChinese.Keys);

    public static AppLanguage ResolveLanguage(string? preference, CultureInfo systemCulture)
    {
        var normalized = AppLanguagePreference.Normalize(preference);
        if (normalized == AppLanguagePreference.English)
        {
            return AppLanguage.English;
        }

        if (normalized == AppLanguagePreference.SimplifiedChinese)
        {
            return AppLanguage.SimplifiedChinese;
        }

        return string.Equals(systemCulture.TwoLetterISOLanguageName, "zh", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.SimplifiedChinese
            : AppLanguage.English;
    }

    public static void UsePreference(string? preference)
    {
        CurrentLanguage = ResolveLanguage(preference, CultureInfo.CurrentUICulture);
    }

    public static string Text(string key)
    {
        var resources = CurrentLanguage == AppLanguage.SimplifiedChinese ? SimplifiedChinese : English;
        if (resources.TryGetValue(key, out var value))
        {
            return value;
        }

        throw new KeyNotFoundException($"Missing localization key: {key}");
    }

    public static string Format(string key, params object?[] arguments)
    {
        return string.Format(CultureInfo.CurrentCulture, Text(key), arguments);
    }
}
