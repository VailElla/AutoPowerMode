namespace AutoPowerMode;

internal static class MonitoringIntervalPolicy
{
    public static readonly TimeSpan ActiveInterval = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan IdleInterval = TimeSpan.FromSeconds(1);

    public static TimeSpan GetInterval(UserActivityState activityState)
    {
        return activityState == UserActivityState.Idle
            ? IdleInterval
            : ActiveInterval;
    }
}
