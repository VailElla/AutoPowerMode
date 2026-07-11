namespace AutoPowerMode;

internal enum AlreadyActivePlanDisposition
{
    StartupSynchronized,
    ExternalChangeDetected,
    NoChange
}

internal static class PowerPlanNotificationPolicy
{
    public static AlreadyActivePlanDisposition ClassifyAlreadyActivePlan(
        UserActivityState previousActivityState,
        UserActivityState targetActivityState,
        bool manual)
    {
        if (previousActivityState == UserActivityState.Unknown)
        {
            return AlreadyActivePlanDisposition.StartupSynchronized;
        }

        if (!manual && previousActivityState != targetActivityState)
        {
            return AlreadyActivePlanDisposition.ExternalChangeDetected;
        }

        return AlreadyActivePlanDisposition.NoChange;
    }
}
