using Microsoft.Win32;

namespace MacDock.Interop;

/// <summary>Turns a pin's <c>Target</c> into something the shell can parse (full path or shell: name).</summary>
internal static class TargetResolver
{
    public static string Resolve(string target)
    {
        if (string.IsNullOrWhiteSpace(target)) return target;
        if (target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) || Path.IsPathRooted(target)) return target;
        if (target.Contains(':')) return target; // URI such as ms-settings:

        // Bare file name like "notepad.exe": look where ShellExecute would — App Paths first
        // (that's how "msedge.exe" or "chrome.exe" resolve), then the Windows dirs and PATH.
        if (FromAppPaths(target) is { } registered) return registered;

        var dirs = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.SystemDirectory,
        };
        dirs.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));

        foreach (var dir in dirs)
        {
            try
            {
                string candidate = Path.Combine(dir.Trim(), target);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException) { /* malformed PATH entry */ }
        }
        return target;
    }

    private static string? FromAppPaths(string exeName)
    {
        const string key = @"Software\Microsoft\Windows\CurrentVersion\App Paths\";
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            try
            {
                using var sub = hive.OpenSubKey(key + exeName);
                if (sub?.GetValue(null) is string value)
                {
                    string path = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
                    if (File.Exists(path)) return path;
                }
            }
            catch (System.Security.SecurityException) { }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }
        return null;
    }
}
