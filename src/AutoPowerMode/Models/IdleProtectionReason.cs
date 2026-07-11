namespace AutoPowerMode;

[Flags]
internal enum IdleProtectionReason
{
    None = 0,
    ExecutionState = 1,
    FullscreenForegroundWindow = 2
}
