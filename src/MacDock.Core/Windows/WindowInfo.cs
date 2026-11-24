namespace MacDock.Core.Windows;

/// <summary>A top-level window that belongs on the dock.</summary>
/// <param name="Handle">The HWND.</param>
/// <param name="ExePath">Full path of the owning process's exe, or <c>null</c> if it can't be read (e.g. some elevated apps).</param>
/// <param name="AppId">Explicit AppUserModelID, or <c>null</c>. Packaged (UWP) apps always have one.</param>
public sealed record WindowInfo(nint Handle, int ProcessId, string? ExePath, string? AppId, string Title, bool IsMinimized);
