namespace MacDock.Core.Windows;

/// <summary>Live view of the desktop's app windows plus the actions the dock performs on them.</summary>
public interface IWindowService
{
    /// <summary>Current app windows in z-order, topmost first.</summary>
    IReadOnlyList<WindowInfo> Windows { get; }

    /// <summary>The foreground window's handle (may not be in <see cref="Windows"/>).</summary>
    nint Foreground { get; }

    /// <summary>Raised on the UI thread when <see cref="Windows"/> or <see cref="Foreground"/> changes.</summary>
    event EventHandler? Changed;

    /// <summary>Restores the window if minimized and brings it to the front.</summary>
    void Activate(nint handle);

    void Minimize(nint handle);

    /// <summary>Asks the window to close (like clicking its X); the app may still prompt to save.</summary>
    void Close(nint handle);
}
