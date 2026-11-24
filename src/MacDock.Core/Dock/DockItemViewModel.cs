using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacDock.Core.Config;

namespace MacDock.Core.Dock;

/// <summary>One icon in the dock.</summary>
public sealed partial class DockItemViewModel : ObservableObject
{
    private readonly IAppLauncher _launcher;

    public DockItemViewModel(PinnedApp app, IAppLauncher launcher)
    {
        App = app;
        _launcher = launcher;
    }

    public PinnedApp App { get; }

    /// <summary>Current, animated scale (what the view renders).</summary>
    [ObservableProperty]
    private double _scale = 1.0;

    /// <summary>Scale the item is easing toward.</summary>
    public double TargetScale { get; internal set; } = 1.0;

    /// <summary>Raised after a successful launch so the view can play the bounce animation.</summary>
    public event EventHandler? BounceRequested;

    [RelayCommand]
    private void Launch()
    {
        if (_launcher.TryLaunch(App))
            BounceRequested?.Invoke(this, EventArgs.Empty);
    }
}
