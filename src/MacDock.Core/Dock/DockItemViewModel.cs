using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacDock.Core.Config;
using MacDock.Core.Windows;

namespace MacDock.Core.Dock;

/// <summary>
/// One icon in the dock: a pinned app, a running unpinned app, or both. Behaviour lives in
/// <see cref="DockViewModel"/>, which owns the pins and the window service; commands delegate to it.
/// </summary>
public sealed partial class DockItemViewModel : ObservableObject
{
    private readonly DockViewModel _owner;

    internal DockItemViewModel(DockViewModel owner, DockEntry entry, double baseSize)
    {
        _owner = owner;
        Key = entry.Key;
        BaseSize = baseSize;
        Update(entry);
    }

    /// <summary>Identity used to keep the same instance across refreshes (so animations don't reset).</summary>
    public string Key { get; }

    public double BaseSize { get; }

    /// <summary>Rendered edge length (<see cref="BaseSize"/> × <see cref="Scale"/>); neighbours shift as icons grow.</summary>
    public double Size => BaseSize * Scale;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Size))]
    private double _scale = 1.0;

    public double TargetScale { get; internal set; } = 1.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPinned))]
    [NotifyCanExecuteChangedFor(nameof(TogglePinCommand))]
    private PinnedApp? _pin;

    public bool IsPinned => Pin is not null;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TogglePinCommand))]
    private string _iconTarget = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(QuitCommand))]
    private bool _isRunning;

    /// <summary>This app's windows, topmost first.</summary>
    public IReadOnlyList<WindowInfo> Windows { get; private set; } = [];

    /// <summary>Raised after a successful launch so the view can play the bounce animation.</summary>
    public event EventHandler? BounceRequested;

    /// <summary>What a fresh launch of this item starts.</summary>
    internal PinnedApp LaunchTarget => Pin ?? new PinnedApp(Key, DisplayName, IconTarget);

    internal void Update(DockEntry entry)
    {
        Pin = entry.Pin;
        DisplayName = entry.DisplayName;
        IconTarget = entry.IconTarget;
        Windows = entry.Windows;
        IsRunning = entry.IsRunning;
    }

    internal void RaiseBounce() => BounceRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Launch if not running; minimize if it owns the foreground window; otherwise bring it to the front.</summary>
    [RelayCommand]
    private void Click() => _owner.Click(this);

    [RelayCommand]
    private void NewWindow() => _owner.Launch(this);

    [RelayCommand(CanExecute = nameof(CanTogglePin))]
    private void TogglePin() => _owner.TogglePin(this);

    private bool CanTogglePin() => IsPinned || !string.IsNullOrEmpty(IconTarget);

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Quit() => _owner.Quit(this);
}
