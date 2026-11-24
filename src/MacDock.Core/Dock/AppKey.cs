using MacDock.Core.Config;
using MacDock.Core.Windows;

namespace MacDock.Core.Dock;

/// <summary>
/// Identity rules that decide which windows belong to the same dock icon.
/// Paths are always Windows paths, so the helpers here split on both separators themselves rather
/// than use <see cref="Path"/>, whose behaviour depends on the OS running the tests.
/// </summary>
public static class AppKey
{
    public const string AppsFolderPrefix = @"shell:AppsFolder\";

    /// <summary>AppUserModelID if present (groups UWP apps hosted by ApplicationFrameHost), else exe path, else process id.</summary>
    public static string ForWindow(WindowInfo w)
        => !string.IsNullOrEmpty(w.AppId) ? w.AppId
         : !string.IsNullOrEmpty(w.ExePath) ? w.ExePath.ToLowerInvariant()
         : $"pid:{w.ProcessId}";

    /// <summary>Stable key of a pinned item (independent of whether it's running).</summary>
    public static string ForPin(PinnedApp pin) => "pin:" + pin.Id;

    public static bool PinMatches(PinnedApp pin, WindowInfo w)
    {
        string target = pin.Target.Trim();
        if (target.StartsWith(AppsFolderPrefix, StringComparison.OrdinalIgnoreCase))
            return string.Equals(target[AppsFolderPrefix.Length..], w.AppId, StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(w.ExePath)) return false;
        return IsBareFileName(target)
            ? string.Equals(FileName(target), FileName(w.ExePath), StringComparison.OrdinalIgnoreCase)
            : string.Equals(target, w.ExePath, StringComparison.OrdinalIgnoreCase);
    }

    internal static string FileName(string path)
    {
        int i = path.LastIndexOfAny(['\\', '/']);
        return i < 0 ? path : path[(i + 1)..];
    }

    internal static string FileNameWithoutExtension(string path)
    {
        string name = FileName(path);
        int dot = name.LastIndexOf('.');
        return dot <= 0 ? name : name[..dot];
    }

    private static bool IsBareFileName(string target) => target.IndexOfAny(['\\', '/', ':']) < 0;
}
