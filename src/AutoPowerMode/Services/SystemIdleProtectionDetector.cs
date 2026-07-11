using System.Runtime.InteropServices;
using System.Text;

namespace AutoPowerMode;

internal sealed class SystemIdleProtectionDetector
{
    private const int SystemExecutionStateInformationLevel = 16;
    private const uint StatusSuccess = 0;
    private const uint EsSystemRequired = 0x00000001;
    private const uint EsDisplayRequired = 0x00000002;
    private const uint EsAwayModeRequired = 0x00000040;
    private const uint MonitorDefaultToNearest = 0x00000002;
    private const int FullscreenBoundsTolerance = 2;

    private int _executionStateFailureLogged;

    public bool IsExecutionStateBlockingIdle()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var status = CallNtPowerInformation(
            SystemExecutionStateInformationLevel,
            IntPtr.Zero,
            0,
            out var executionState,
            sizeof(uint));

        if (status != StatusSuccess)
        {
            if (Interlocked.Exchange(ref _executionStateFailureLogged, 1) == 0)
            {
                Logger.Error($"读取 Windows SystemExecutionState 失败，NTSTATUS=0x{status:X8}；本轮按未声明保持唤醒处理。");
            }

            return false;
        }

        Interlocked.Exchange(ref _executionStateFailureLogged, 0);
        return HasBlockingExecutionState(executionState);
    }

    public bool IsForegroundWindowFullscreen()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var windowHandle = GetForegroundWindow();
        if (windowHandle == IntPtr.Zero ||
            IsExcludedShellWindow(windowHandle) ||
            !GetWindowRect(windowHandle, out var windowBounds))
        {
            return false;
        }

        var monitorHandle = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
        if (monitorHandle == IntPtr.Zero)
        {
            return false;
        }

        var monitorInfo = new MonitorInfo
        {
            Size = (uint)Marshal.SizeOf<MonitorInfo>()
        };

        return GetMonitorInfo(monitorHandle, ref monitorInfo) &&
               CoversMonitorBounds(windowBounds, monitorInfo.MonitorBounds, FullscreenBoundsTolerance);
    }

    internal static bool HasBlockingExecutionState(uint executionState)
    {
        const uint blockingMask = EsSystemRequired | EsDisplayRequired | EsAwayModeRequired;
        return (executionState & blockingMask) != 0;
    }

    internal static bool CoversMonitorBounds(NativeRect windowBounds, NativeRect monitorBounds, int tolerance = FullscreenBoundsTolerance)
    {
        var effectiveTolerance = Math.Max(0, tolerance);
        return IsWithinTolerance(windowBounds.Left, monitorBounds.Left, effectiveTolerance) &&
               IsWithinTolerance(windowBounds.Top, monitorBounds.Top, effectiveTolerance) &&
               IsWithinTolerance(windowBounds.Right, monitorBounds.Right, effectiveTolerance) &&
               IsWithinTolerance(windowBounds.Bottom, monitorBounds.Bottom, effectiveTolerance);
    }

    internal static bool IsExcludedShellWindowClass(string? windowClassName)
    {
        return windowClassName is not null &&
               (windowClassName.Equals("Progman", StringComparison.OrdinalIgnoreCase) ||
                windowClassName.Equals("WorkerW", StringComparison.OrdinalIgnoreCase) ||
                windowClassName.Equals("Shell_TrayWnd", StringComparison.OrdinalIgnoreCase) ||
                windowClassName.Equals("Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsExcludedShellWindow(IntPtr windowHandle)
    {
        var className = new StringBuilder(256);
        return GetClassName(windowHandle, className, className.Capacity) > 0 &&
               IsExcludedShellWindowClass(className.ToString());
    }

    private static bool IsWithinTolerance(int first, int second, int tolerance)
    {
        return Math.Abs((long)first - second) <= tolerance;
    }

    [DllImport("powrprof.dll")]
    private static extern uint CallNtPowerInformation(
        int informationLevel,
        IntPtr inputBuffer,
        uint inputBufferLength,
        out uint outputBuffer,
        uint outputBufferLength);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(IntPtr windowHandle, StringBuilder className, int maximumCount);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr windowHandle, out NativeRect rectangle);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr windowHandle, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitorHandle, ref MonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRect
    {
        public NativeRect(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint Size;
        public NativeRect MonitorBounds;
        public NativeRect WorkAreaBounds;
        public uint Flags;
    }
}
