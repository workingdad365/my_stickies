using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MyStickies.Interop;

/// <summary>앞에 있는 창이 덱과 같은 모니터를 전체화면으로 덮고 있는지 판정</summary>
public static class FullscreenDetector
{
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    /// <summary>바탕화면 껍데기 창. 화면 전체 크기지만 전체화면 앱이 아님</summary>
    private static readonly HashSet<string> ShellClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder name, int maxCount);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    /// <summary>창 사각형이 모니터 사각형을 완전히 덮는지</summary>
    public static bool CoversMonitor(RECT window, RECT monitor) =>
        window.Left <= monitor.Left && window.Top <= monitor.Top &&
        window.Right >= monitor.Right && window.Bottom >= monitor.Bottom;

    /// <summary>
    /// 앞에 있는 다른 프로세스의 창이 ownerHwnd가 놓인 모니터를 전체화면으로 덮고 있으면 true.
    /// 자기 프로세스 창, 바탕화면 껍데기 창은 제외
    /// </summary>
    public static bool IsForegroundFullscreenOnSameMonitor(IntPtr ownerHwnd)
    {
        var fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == ownerHwnd) return false;

        GetWindowThreadProcessId(fg, out var pid);
        if (pid == (uint)Environment.ProcessId) return false;

        var cls = new StringBuilder(64);
        GetClassName(fg, cls, cls.Capacity);
        if (ShellClasses.Contains(cls.ToString())) return false;

        var fgMonitor = MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST);
        var ownMonitor = MonitorFromWindow(ownerHwnd, MONITOR_DEFAULTTONEAREST);
        if (fgMonitor == IntPtr.Zero || fgMonitor != ownMonitor) return false;

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(fgMonitor, ref info)) return false;
        if (!GetWindowRect(fg, out var rect)) return false;

        return CoversMonitor(rect, info.rcMonitor);
    }
}
