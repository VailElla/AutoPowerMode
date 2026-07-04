using System.Text.Json.Serialization;

namespace AutoPowerMode;

public sealed class AppConfig
{
    public const int MinIdleThresholdMinutes = 1;
    public const int MaxIdleThresholdMinutes = 240;
    public const int MinCheckIntervalMinutes = 1;
    public const int MaxCheckIntervalMinutes = 60;

    public int IdleThresholdMinutes { get; set; } = 5;

    public int CheckIntervalMinutes { get; set; } = 1;

    public string IdlePowerPlanGuid { get; set; } = string.Empty;

    public string ActivePowerPlanGuid { get; set; } = string.Empty;

    public bool AutoStart { get; set; } = true;

    public bool IsPaused { get; set; }

    [JsonIgnore]
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(IdlePowerPlanGuid) &&
        !string.IsNullOrWhiteSpace(ActivePowerPlanGuid);

    public static AppConfig CreateDefault() => new();

    public AppConfig Clone()
    {
        return new AppConfig
        {
            IdleThresholdMinutes = IdleThresholdMinutes,
            CheckIntervalMinutes = CheckIntervalMinutes,
            IdlePowerPlanGuid = IdlePowerPlanGuid,
            ActivePowerPlanGuid = ActivePowerPlanGuid,
            AutoStart = AutoStart,
            IsPaused = IsPaused
        };
    }

    public void Normalize()
    {
        IdleThresholdMinutes = Math.Clamp(
            IdleThresholdMinutes,
            MinIdleThresholdMinutes,
            MaxIdleThresholdMinutes);

        CheckIntervalMinutes = Math.Clamp(
            CheckIntervalMinutes,
            MinCheckIntervalMinutes,
            MaxCheckIntervalMinutes);

        IdlePowerPlanGuid = IdlePowerPlanGuid.Trim();
        ActivePowerPlanGuid = ActivePowerPlanGuid.Trim();
    }
}
