namespace AutoPowerMode;

internal sealed class PowerModeTransitionPolicy
{
    public const int DefaultRequiredIdleDetections = 2;

    private readonly int _requiredIdleDetections;
    private int _idleDetectionCount;

    public PowerModeTransitionPolicy(int requiredIdleDetections = DefaultRequiredIdleDetections)
    {
        _requiredIdleDetections = Math.Max(1, requiredIdleDetections);
    }

    public UserActivityState CurrentActivityState { get; private set; } = UserActivityState.Unknown;

    public void MarkActivityState(UserActivityState state)
    {
        CurrentActivityState = state;
        _idleDetectionCount = 0;
    }

    public void Reset()
    {
        CurrentActivityState = UserActivityState.Unknown;
        _idleDetectionCount = 0;
    }

    public UserActivityState? SuppressIdleTransition()
    {
        _idleDetectionCount = 0;
        return CurrentActivityState == UserActivityState.Active
            ? null
            : UserActivityState.Active;
    }

    public UserActivityState? Evaluate(
        TimeSpan idleTime,
        TimeSpan idleThreshold,
        TimeSpan activeResumeThreshold)
    {
        if (idleTime < activeResumeThreshold)
        {
            _idleDetectionCount = 0;
            return CurrentActivityState == UserActivityState.Active ? null : UserActivityState.Active;
        }

        if (idleTime >= idleThreshold)
        {
            if (CurrentActivityState == UserActivityState.Idle)
            {
                return null;
            }

            _idleDetectionCount = Math.Min(_idleDetectionCount + 1, _requiredIdleDetections);
            return _idleDetectionCount >= _requiredIdleDetections ? UserActivityState.Idle : null;
        }

        _idleDetectionCount = 0;
        return null;
    }
}
