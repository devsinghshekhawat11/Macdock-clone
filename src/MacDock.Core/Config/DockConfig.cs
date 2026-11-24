namespace MacDock.Core.Config;

/// <summary>Everything MacDock persists between runs.</summary>
public sealed record DockConfig(IReadOnlyList<PinnedApp> Pins, DockSettings Settings)
{
    public static DockConfig Default { get; } = new(
        [
            new PinnedApp("explorer", "File Explorer", "explorer.exe"),
            new PinnedApp("settings", "Settings", "ms-settings:"),
            new PinnedApp("notepad", "Notepad", "notepad.exe"),
        ],
        new DockSettings());
}
