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

    public PowerModeState CurrentState { get; private set; } = PowerModeState.NotConfigured;

    public void MarkState(PowerModeState state)
    {
        CurrentState = state;
        _idleDetectionCount = 0;
    }

    public PowerModeState? Evaluate(
        TimeSpan idleTime,
        TimeSpan idleThreshold,
        TimeSpan activeResumeThreshold,
        bool isPaused,
        bool isConfigured)
    {
        if (isPaused)
        {
            _idleDetectionCount = 0;
            return CurrentState == PowerModeState.Paused ? null : PowerModeState.Paused;
        }

        if (!isConfigured)
        {
            _idleDetectionCount = 0;
            return CurrentState == PowerModeState.NotConfigured ? null : PowerModeState.NotConfigured;
        }

        if (idleTime < activeResumeThreshold)
        {
            _idleDetectionCount = 0;
            return CurrentState == PowerModeState.Active ? null : PowerModeState.Active;
        }

        if (idleTime >= idleThreshold)
        {
            if (CurrentState == PowerModeState.Idle)
            {
                return null;
            }

            _idleDetectionCount = Math.Min(_idleDetectionCount + 1, _requiredIdleDetections);
            return _idleDetectionCount >= _requiredIdleDetections ? PowerModeState.Idle : null;
        }

        _idleDetectionCount = 0;
        return null;
    }
}
