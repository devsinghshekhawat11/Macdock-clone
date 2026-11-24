using MacDock.Core.Config;
using MacDock.Core.Windows;

namespace MacDock.Core.Dock;

/// <summary>One icon's worth of state: a pin, its running windows, or a running unpinned app.</summary>
public sealed record DockEntry(string Key, PinnedApp? Pin, string DisplayName, string IconTarget, IReadOnlyList<WindowInfo> Windows)
{
    public bool IsRunning => Windows.Count > 0;
}

public static class DockGrouping
{
    /// <summary>
    /// Builds the dock's icons: pins first in config order, then running unpinned apps. Unpinned apps keep
    /// the order given by <paramref name="previousUnpinnedOrder"/> (new ones go last), so icons don't jump
    /// around every time a different window comes to the front.
    /// </summary>
    /// <param name="windows">Windows in z-order (topmost first); each entry's windows keep that order.</param>
    public static IReadOnlyList<DockEntry> Group(
        IReadOnlyList<PinnedApp> pins,
        IReadOnlyList<WindowInfo> windows,
        IReadOnlyList<string> previousUnpinnedOrder)
    {
        var pinWindows = pins.Select(_ => new List<WindowInfo>()).ToArray();
        var unpinned = new Dictionary<string, List<WindowInfo>>();
        var firstSeen = new List<string>();

        foreach (var w in windows)
        {
            int pinIndex = IndexOfMatchingPin(pins, w);
            if (pinIndex >= 0) { pinWindows[pinIndex].Add(w); continue; }

            string key = AppKey.ForWindow(w);
            if (!unpinned.TryGetValue(key, out var list))
            {
                unpinned[key] = list = [];
                firstSeen.Add(key);
            }
            list.Add(w);
        }

        var result = new List<DockEntry>(pins.Count + unpinned.Count);
        for (int i = 0; i < pins.Count; i++)
            result.Add(new DockEntry(AppKey.ForPin(pins[i]), pins[i], pins[i].DisplayName, pins[i].Target, pinWindows[i]));

        var ordered = previousUnpinnedOrder.Where(unpinned.ContainsKey)
            .Concat(firstSeen.Where(k => !previousUnpinnedOrder.Contains(k)));
        foreach (string key in ordered)
        {
            var list = unpinned[key];
            var first = list[0];
            result.Add(new DockEntry(key, null, DisplayNameFor(first), IconTargetFor(first), list));
        }
        return result;
    }

    private static int IndexOfMatchingPin(IReadOnlyList<PinnedApp> pins, WindowInfo w)
    {
        for (int i = 0; i < pins.Count; i++)
            if (AppKey.PinMatches(pins[i], w)) return i;
        return -1;
    }

    // UWP apps are hosted by ApplicationFrameHost.exe, so for them the exe says nothing about the app:
    // use the window title and the AppsFolder entry instead. Everything else uses its own exe.
    private static bool IsHostedPackagedApp(WindowInfo w)
        => !string.IsNullOrEmpty(w.AppId)
        && (string.IsNullOrEmpty(w.ExePath) || AppKey.FileName(w.ExePath).Equals("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase));

    private static string DisplayNameFor(WindowInfo w)
        => !IsHostedPackagedApp(w) && !string.IsNullOrEmpty(w.ExePath) ? AppKey.FileNameWithoutExtension(w.ExePath) : w.Title;

    private static string IconTargetFor(WindowInfo w)
        => IsHostedPackagedApp(w) ? AppKey.AppsFolderPrefix + w.AppId : w.ExePath ?? string.Empty;
}
