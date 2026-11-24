namespace MacDock.Core.Config;

/// <summary>Everything MacDock persists between runs.</summary>
public sealed record DockConfig(IReadOnlyList<PinnedApp> Pins, DockSettings Settings)
{
    public static DockConfig Default { get; } = new(
        [
            new PinnedApp("explorer", "File Explorer", "explorer.exe"),
            new PinnedApp("settings", "Settings", @"shell:AppsFolder\windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel"),
            new PinnedApp("notepad", "Notepad", "notepad.exe"),
        ],
        new DockSettings());

    /// <summary>
    /// Repairs what a hand-edited or older file can get wrong: missing sections, null or target-less
    /// pins, and out-of-range sizes that would otherwise crash the window layout.
    /// </summary>
    public DockConfig Normalize()
    {
        // Deserialized data can violate the non-null annotations, hence the defensive checks.
        var pins = Pins is null
            ? Default.Pins
            : Pins.Where(p => p is not null && !string.IsNullOrWhiteSpace(p.Target)).ToList();
        var s = Settings ?? new DockSettings();
        return new DockConfig(pins, s with
        {
            IconSize = Math.Clamp(s.IconSize, DockSettings.MinIconSize, DockSettings.MaxIconSize),
            MaxScale = Math.Clamp(s.MaxScale, DockSettings.MinMaxScale, DockSettings.MaxMaxScale),
        });
    }
}
