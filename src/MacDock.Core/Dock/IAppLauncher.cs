using MacDock.Core.Config;

namespace MacDock.Core.Dock;

/// <summary>Starts apps. Implemented with the Windows shell in MacDock.Interop; faked in tests.</summary>
public interface IAppLauncher
{
    /// <returns><c>true</c> if the app was started; <c>false</c> if it could not be (missing file, bad URI…). Never throws.</returns>
    bool TryLaunch(PinnedApp app);
}
