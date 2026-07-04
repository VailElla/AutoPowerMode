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

    public string LastSwitchStatus { get; init; } = string.Empty;

    public string LastPowerCfgError { get; init; } = string.Empty;

    public string ConfigPath { get; init; } = string.Empty;

    public string LogPath { get; init; } = string.Empty;

    public string PortableLogPath { get; init; } = string.Empty;

    public string ToDisplayText()
    {
        return string.Join(
            Environment.NewLine,
            "AutoPowerMode 诊断",
            "",
            $"应用版本：{AppVersion}",
            $"当前状态：{ControlState} / {UserActivityState}",
            $"状态说明：{DisplayStatus}",
            "",
            $"当前电源计划：{CurrentPowerPlan}",
            $"活跃计划配置：{ActivePlanConfig}",
            $"空闲计划配置：{IdlePlanConfig}",
            $"空闲时间：{IdleTime}",
            $"空闲阈值：{IdleThreshold}",
            $"检测间隔：{CheckInterval}",
            "",
            $"最近一次切换：{LastSwitchStatus}",
            $"最近一次 powercfg 错误：{LastPowerCfgError}",
            "",
            $"配置文件位置：{ConfigPath}",
            $"日志位置：{LogPath}",
            $"便携日志位置：{PortableLogPath}");
    }
}
