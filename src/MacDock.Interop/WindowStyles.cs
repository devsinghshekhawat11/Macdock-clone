using System.Runtime.InteropServices;

namespace MacDock.Interop;

/// <summary>Extended window styles that make a WPF window behave like a shell surface.</summary>
public static class WindowStyles
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x00000080; // hidden from Alt+Tab and the taskbar
    private const long WS_EX_NOACTIVATE = 0x08000000; // clicking the dock doesn't steal focus from your app

    /// <summary>Applies WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE to <paramref name="hwnd"/>.</summary>
    public static void MakeShellSurface(IntPtr hwnd)
    {
        long style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
