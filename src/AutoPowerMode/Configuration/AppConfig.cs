using System.Text.Json.Serialization;

namespace AutoPowerMode;

public sealed class AppConfig
{
    public const int MinIdleThresholdSeconds = 10;
    public const int MaxIdleThresholdSeconds = 14400;
    public const int DefaultIdleThresholdSeconds = 1200;

    public int IdleThresholdSeconds { get; set; } = DefaultIdleThresholdSeconds;

    [JsonIgnore]
    public int? LegacyIdleThresholdMinutes { get; set; }

    public string IdlePowerPlanGuid { get; set; } = string.Empty;

    public string ActivePowerPlanGuid { get; set; } = string.Empty;

    public bool AutoStart { get; set; }

    public bool NotificationsEnabled { get; set; } = true;

    public bool PreventIdleOnExecutionState { get; set; }

    public bool PreventIdleOnFullscreen { get; set; }

    public string Language { get; set; } = AppLanguagePreference.System;

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
            IdlePowerPlanGuid = IdlePowerPlanGuid,
            ActivePowerPlanGuid = ActivePowerPlanGuid,
            AutoStart = AutoStart,
            NotificationsEnabled = NotificationsEnabled,
            PreventIdleOnExecutionState = PreventIdleOnExecutionState,
            PreventIdleOnFullscreen = PreventIdleOnFullscreen,
            Language = Language,
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

        IdlePowerPlanGuid = IdlePowerPlanGuid.Trim();
        ActivePowerPlanGuid = ActivePowerPlanGuid.Trim();
        Language = AppLanguagePreference.Normalize(Language);
    }
}
