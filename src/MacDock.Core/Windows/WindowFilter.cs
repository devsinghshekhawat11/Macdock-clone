namespace MacDock.Core.Windows;

/// <summary>
/// Mirrors the Windows taskbar's rules for which top-level windows get a button. Kept as a pure
/// function so the rules are unit-tested; Interop only gathers the <see cref="WindowTraits"/>.
/// </summary>
public static class WindowFilter
{
    public static bool IsTaskbarWindow(WindowTraits t)
    {
        // Cloaked = hidden by DWM: windows on other virtual desktops and suspended UWP frames.
        if (!t.IsVisible || t.IsCloaked || t.IsOwnProcess || string.IsNullOrWhiteSpace(t.Title))
            return false;

        // WS_EX_APPWINDOW forces a taskbar button; otherwise owned, tool and no-activate windows are skipped.
        return t.IsAppWindow || (!t.IsToolWindow && !t.HasOwner && !t.IsNoActivate);
    }
}
