namespace AutoPowerMode;

internal sealed class DiagnosticSnapshot
{
    public string AppVersion { get; init; } = string.Empty;

    public ControlState ControlState { get; init; }

    public UserActivityState UserActivityState { get; init; }

    public string DisplayStatus { get; init; } = string.Empty;

    public string CurrentPowerPlan { get; init; } = string.Empty;

    public string ActivePlanConfig { get; init; } = string.Empty;

    public string IdlePlanConfig { get; init; } = string.Empty;

    public string IdleTime { get; init; } = string.Empty;

    public string IdleThreshold { get; init; } = string.Empty;

    public string CheckInterval { get; init; } = string.Empty;

    public bool NotificationsEnabled { get; init; }

    public string LastSwitchStatus { get; init; } = string.Empty;

    public string LastPowerCfgError { get; init; } = string.Empty;

    public string ConfigPath { get; init; } = string.Empty;

    public string LogPath { get; init; } = string.Empty;

    public string PortableLogPath { get; init; } = string.Empty;

    public string ToDisplayText()
    {
        return string.Join(
            Environment.NewLine,
            LocalizationService.Text("DiagnosticsHeading"),
            "",
            LocalizationService.Format("ApplicationVersion", AppVersion),
            LocalizationService.Format("CurrentState", ControlState, UserActivityState),
            LocalizationService.Format("StatusDescription", DisplayStatus),
            "",
            LocalizationService.Format("CurrentPowerPlan", CurrentPowerPlan),
            LocalizationService.Format("ActivePlanConfig", ActivePlanConfig),
            LocalizationService.Format("IdlePlanConfig", IdlePlanConfig),
            LocalizationService.Format("IdleTime", IdleTime),
            LocalizationService.Format("IdleThreshold", IdleThreshold),
            LocalizationService.Format("CheckIntervalValue", CheckInterval),
            LocalizationService.Format("NotificationsValue", LocalizationService.Text(NotificationsEnabled ? "Enabled" : "Disabled")),
            "",
            LocalizationService.Format("LastSwitch", LastSwitchStatus),
            LocalizationService.Format("LastPowerCfgError", LastPowerCfgError),
            "",
            LocalizationService.Format("ConfigPath", ConfigPath),
            LocalizationService.Format("LogPath", LogPath),
            LocalizationService.Format("PortableLogPath", PortableLogPath));
    }
}
