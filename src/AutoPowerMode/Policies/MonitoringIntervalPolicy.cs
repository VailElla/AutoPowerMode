namespace AutoPowerMode;

internal static class MonitoringIntervalPolicy
{
    public static TimeSpan GetInterval(
        UserActivityState activityState,
        int activeCheckIntervalSeconds,
        int idleCheckIntervalSeconds)
    {
        var intervalSeconds = activityState == UserActivityState.Idle
            ? idleCheckIntervalSeconds
            : activeCheckIntervalSeconds;

        return TimeSpan.FromSeconds(
            Math.Clamp(
                intervalSeconds,
                AppConfig.MinCheckIntervalSeconds,
                AppConfig.MaxCheckIntervalSeconds));
    }
}
