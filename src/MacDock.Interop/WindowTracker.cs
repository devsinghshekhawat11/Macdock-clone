using System.Text;
using System.Windows.Threading;
using MacDock.Core.Windows;
using MacDock.Interop.Native;

namespace MacDock.Interop;

/// <summary>
/// Keeps a live list of app windows. WinEvent hooks tell us <em>that</em> something changed; we then
/// re-enumerate everything once, 60 ms later, coalescing bursts (an app opening fires dozens of events).
/// A full <c>EnumWindows</c> pass takes well under a millisecond, and re-deriving the whole list is much
/// harder to get wrong than patching it event by event. There is no polling: idle costs nothing.
/// </summary>
public sealed class WindowTracker : IWindowService, IDisposable
{
    private static readonly (uint Min, uint Max)[] HookedEvents =
    [
        (User32.EVENT_SYSTEM_FOREGROUND, User32.EVENT_SYSTEM_FOREGROUND),
        (User32.EVENT_SYSTEM_MINIMIZESTART, User32.EVENT_SYSTEM_MINIMIZEEND),
        (User32.EVENT_OBJECT_CREATE, User32.EVENT_OBJECT_HIDE),
        (User32.EVENT_OBJECT_NAMECHANGE, User32.EVENT_OBJECT_NAMECHANGE),
        (User32.EVENT_OBJECT_CLOAKED, User32.EVENT_OBJECT_UNCLOAKED),
    ];

    private readonly int _ownProcessId = Environment.ProcessId;
    private readonly Dictionary<int, string?> _exeByPid = [];
    private readonly List<IntPtr> _hooks = [];
    private User32.WinEventProc? _winEventProc; // held in a field so the GC can't collect it while Windows calls it
    private DispatcherTimer? _debounce;

    public IReadOnlyList<WindowInfo> Windows { get; private set; } = [];
    public nint Foreground { get; private set; }
    public event EventHandler? Changed;

    /// <summary>Takes the first snapshot and installs the hooks. Call on the UI thread (hooks need its message loop).</summary>
    public void Start()
    {
        if (_winEventProc is not null) return;

        _debounce = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(60) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Rescan(); };

        _winEventProc = OnWinEvent;
        foreach (var (min, max) in HookedEvents)
        {
            IntPtr hook = User32.SetWinEventHook(min, max, IntPtr.Zero, _winEventProc, 0, 0,
                User32.WINEVENT_OUTOFCONTEXT | User32.WINEVENT_SKIPOWNPROCESS);
            if (hook != IntPtr.Zero) _hooks.Add(hook);
        }
        Rescan();
    }

    public void Dispose()
    {
        foreach (var hook in _hooks) User32.UnhookWinEvent(hook);
        _hooks.Clear();
        _debounce?.Stop();
    }

    public void Activate(nint handle)
    {
        if (User32.IsIconic(handle)) User32.ShowWindow(handle, User32.SW_RESTORE);
        if (User32.SetForegroundWindow(handle)) return;

        // Windows refuses focus changes from processes it doesn't consider "in charge". A synthetic Alt
        // press makes us the last input source, which lifts the restriction (a well-known shell workaround).
        User32.keybd_event(User32.VK_MENU, 0, 0, UIntPtr.Zero);
        User32.SetForegroundWindow(handle);
        User32.keybd_event(User32.VK_MENU, 0, User32.KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    public void Minimize(nint handle) => User32.ShowWindowAsync(handle, User32.SW_MINIMIZE);

    public void Close(nint handle) => User32.PostMessage(handle, User32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

    private void OnWinEvent(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        // Only whole windows matter; ignore events about carets, scrollbars, menu items, etc.
        if (idObject != User32.OBJID_WINDOW || idChild != User32.CHILDID_SELF) return;
        _debounce?.Stop();
        _debounce?.Start();
    }

    private void Rescan()
    {
        var handles = new List<IntPtr>();
        User32.EnumWindows((hwnd, _) => { handles.Add(hwnd); return true; }, IntPtr.Zero);

        var windows = new List<WindowInfo>();
        var livePids = new HashSet<int>();
        foreach (var hwnd in handles)
        {
            User32.GetWindowThreadProcessId(hwnd, out int pid);
            if (!WindowFilter.IsTaskbarWindow(ReadTraits(hwnd, pid))) continue;

            livePids.Add(pid);
            windows.Add(new WindowInfo(hwnd, pid, ExePathFor(pid), AppUserModel.GetWindowAppId(hwnd), GetTitle(hwnd), User32.IsIconic(hwnd)));
        }

        // Forget processes that have exited so a recycled pid can't inherit a stale exe path.
        foreach (int pid in _exeByPid.Keys.Where(p => !livePids.Contains(p)).ToList()) _exeByPid.Remove(pid);

        nint foreground = User32.GetForegroundWindow();
        if (foreground == Foreground && windows.SequenceEqual(Windows)) return;

        Windows = windows;
        Foreground = foreground;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private WindowTraits ReadTraits(IntPtr hwnd, int pid)
    {
        long exStyle = User32.GetWindowLongPtr(hwnd, User32.GWL_EXSTYLE).ToInt64();
        bool cloaked = Dwmapi.DwmGetWindowAttribute(hwnd, Dwmapi.DWMWA_CLOAKED, out int c, sizeof(int)) == 0 && c != 0;
        return new WindowTraits(
            IsVisible: User32.IsWindowVisible(hwnd),
            IsCloaked: cloaked,
            HasOwner: User32.GetWindow(hwnd, User32.GW_OWNER) != IntPtr.Zero,
            IsToolWindow: (exStyle & User32.WS_EX_TOOLWINDOW) != 0,
            IsAppWindow: (exStyle & User32.WS_EX_APPWINDOW) != 0,
            IsNoActivate: (exStyle & User32.WS_EX_NOACTIVATE) != 0,
            Title: GetTitle(hwnd),
            IsOwnProcess: pid == _ownProcessId);
    }

    private static string GetTitle(IntPtr hwnd)
    {
        int length = User32.GetWindowTextLength(hwnd);
        if (length <= 0) return string.Empty;
        var sb = new StringBuilder(length + 1);
        User32.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private string? ExePathFor(int pid)
    {
        if (_exeByPid.TryGetValue(pid, out var cached)) return cached;

        string? path = null;
        IntPtr process = Kernel32.OpenProcess(Kernel32.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process != IntPtr.Zero)
        {
            try
            {
                var sb = new StringBuilder(1024);
                int size = sb.Capacity;
                if (Kernel32.QueryFullProcessImageName(process, 0, sb, ref size)) path = sb.ToString(0, size);
            }
            finally { Kernel32.CloseHandle(process); }
        }
        return _exeByPid[pid] = path;
    }
}
