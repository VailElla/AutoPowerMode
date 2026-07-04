using System.Text.Json.Serialization;

namespace AutoPowerMode;

public sealed class AppConfig
{
    public const int MinIdleThresholdSeconds = 10;
    public const int MaxIdleThresholdSeconds = 14400;
    public const int DefaultIdleThresholdSeconds = 1200;
    public const int MinCheckIntervalSeconds = 5;
    public const int MaxCheckIntervalSeconds = 3600;
    public const int DefaultCheckIntervalSeconds = 10;

    public int IdleThresholdSeconds { get; set; } = DefaultIdleThresholdSeconds;

    [JsonIgnore]
    public int? LegacyIdleThresholdMinutes { get; set; }

    public int CheckIntervalSeconds { get; set; } = DefaultCheckIntervalSeconds;

    [JsonIgnore]
    public int? LegacyCheckIntervalMinutes { get; set; }

    public string IdlePowerPlanGuid { get; set; } = string.Empty;

    public string ActivePowerPlanGuid { get; set; } = string.Empty;

    public bool AutoStart { get; set; } = true;

    public bool IsPaused { get; set; }

    public bool PowerPlansConfiguredByUser { get; set; }

    [JsonIgnore]
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(IdlePowerPlanGuid) &&
        !string.IsNullOrWhiteSpace(ActivePowerPlanGuid);

    public static AppConfig CreateDefault() => new();

    public AppConfig Clone()
    {
        return new AppConfig
        {
            IdleThresholdSeconds = IdleThresholdSeconds,
            CheckIntervalSeconds = CheckIntervalSeconds,
            IdlePowerPlanGuid = IdlePowerPlanGuid,
            ActivePowerPlanGuid = ActivePowerPlanGuid,
            AutoStart = AutoStart,
            IsPaused = IsPaused,
            PowerPlansConfiguredByUser = PowerPlansConfiguredByUser
        };
    }

    public void Normalize()
    {
        if (IdleThresholdSeconds <= 0)
        {
            IdleThresholdSeconds = DefaultIdleThresholdSeconds;
        }

        IdleThresholdSeconds = Math.Clamp(
            IdleThresholdSeconds,
            MinIdleThresholdSeconds,
            MaxIdleThresholdSeconds);

        if (CheckIntervalSeconds <= 0)
        {
            CheckIntervalSeconds = DefaultCheckIntervalSeconds;
        }

        CheckIntervalSeconds = Math.Clamp(
            CheckIntervalSeconds,
            MinCheckIntervalSeconds,
            MaxCheckIntervalSeconds);

        IdlePowerPlanGuid = IdlePowerPlanGuid.Trim();
        ActivePowerPlanGuid = ActivePowerPlanGuid.Trim();
    }
}
