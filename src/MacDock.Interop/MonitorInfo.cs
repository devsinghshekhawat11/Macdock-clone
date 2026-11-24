using System.Runtime.InteropServices;
using System.Windows;

namespace MacDock.Interop;

/// <summary>
/// Physical-pixel geometry of the primary monitor. WPF's <c>SystemParameters.WorkArea</c> is expressed
/// at the DPI the process started with, so it goes stale when the user changes display scaling while
/// the dock runs; reading pixels and dividing by the window's current DPI is always right.
/// </summary>
public static class MonitorInfo
{
    private const uint MONITOR_DEFAULTTOPRIMARY = 1;

    /// <summary>Primary monitor work area (screen minus taskbar/app bars) in physical pixels.</summary>
    public static Rect PrimaryWorkAreaPixels()
    {
        IntPtr monitor = MonitorFromPoint(new POINT(0, 0), MONITOR_DEFAULTTOPRIMARY);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info))
            return new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
        var w = info.rcWork;
        return new Rect(w.Left, w.Top, w.Right - w.Left, w.Bottom - w.Top);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT(int x, int y) { public int X = x, Y = y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
}
