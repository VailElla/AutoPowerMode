using System.Runtime.InteropServices;

namespace AutoPowerMode;

public sealed class IdleDetector
{
    public TimeSpan GetIdleTime()
    {
        var lastInputInfo = new LastInputInfo
        {
            CbSize = (uint)Marshal.SizeOf<LastInputInfo>()
        };

        if (!GetLastInputInfo(ref lastInputInfo))
        {
            throw new InvalidOperationException("GetLastInputInfo 调用失败。");
        }

        var currentTick = unchecked((uint)Environment.TickCount);
        var idleMilliseconds = currentTick - lastInputInfo.DwTime;
        return TimeSpan.FromMilliseconds(idleMilliseconds);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetLastInputInfo(ref LastInputInfo plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint CbSize;
        public uint DwTime;
    }
}
