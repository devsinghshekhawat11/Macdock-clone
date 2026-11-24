using System.ComponentModel;
using System.Diagnostics;
using MacDock.Core.Config;
using MacDock.Core.Dock;

namespace MacDock.Interop;

/// <summary>Launches pins through ShellExecute, which handles exe paths, URIs and shell: names alike.</summary>
public sealed class ShellAppLauncher : IAppLauncher
{
    public bool TryLaunch(PinnedApp app)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = app.Target,
                Arguments = app.Arguments ?? string.Empty,
                UseShellExecute = true,
            });
            return true; // Process may be null when an existing instance handled the request; still a success.
        }
        catch (Win32Exception) { return false; }
        catch (FileNotFoundException) { return false; }
        catch (InvalidOperationException) { return false; }
    }
}
