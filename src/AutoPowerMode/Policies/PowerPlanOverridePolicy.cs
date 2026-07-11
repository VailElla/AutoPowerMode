namespace AutoPowerMode;

internal static class PowerPlanOverridePolicy
{
    public static bool ShouldSkipAutomaticSwitch(
        bool manual,
        string? currentPlanGuid,
        AppConfig config)
    {
        if (manual || string.IsNullOrWhiteSpace(currentPlanGuid))
        {
            return false;
        }

        return !PowerPlanManager.GuidEquals(currentPlanGuid, config.ActivePowerPlanGuid)
               && !PowerPlanManager.GuidEquals(currentPlanGuid, config.IdlePowerPlanGuid);
    }
}
