using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MacDock.Core.Config;
using MacDock.Core.Windows;

namespace MacDock.Core.Dock;

/// <summary>
/// State and behaviour of the dock, independent of any UI framework. Combines the user's pins with
/// the live window list, and drives magnification. The view reports the cursor and calls
/// <see cref="Tick"/> once per rendered frame.
/// </summary>
public sealed partial class DockViewModel : ObservableObject
{
    private readonly IConfigStore _store;
    private readonly IAppLauncher _launcher;
    private readonly IWindowService _windows;
    private readonly List<PinnedApp> _pins;
    private readonly DockSettings _settings;
    private IReadOnlyList<string> _unpinnedOrder = [];
    private double _containerWidth;
    private double[] _restCenters = [];

    public DockViewModel(IConfigStore store, IAppLauncher launcher, IWindowService windows)
    {
        _store = store;
        _launcher = launcher;
        _windows = windows;

        var config = store.Load();
        _pins = [.. config.Pins];
        _settings = config.Settings;
        Options = new MagnificationOptions(IconSize: _settings.IconSize, MaxScale: _settings.MaxScale);

        Refresh();
        _windows.Changed += (_, _) => Refresh();
    }

    public ObservableCollection<DockItemViewModel> Items { get; } = [];

    public MagnificationOptions Options { get; }

    /// <summary>Raised when the number of icons changes, so the view can resize and re-center its window.</summary>
    public event EventHandler? LayoutChanged;

    public double RestWidth => Items.Count == 0 ? 0 : Items.Count * Options.IconSize + (Items.Count - 1) * Options.Spacing;

    /// <summary>A width that holds the row in any magnification state (every icon at max scale is an upper bound).</summary>
    public double MaxWindowWidth => Items.Count == 0 ? 0 : Items.Count * Options.IconSize * Options.MaxScale + (Items.Count - 1) * Options.Spacing;

    // ---- Layout & magnification -------------------------------------------------------

    public void SetContainerWidth(double width)
    {
        _containerWidth = width;
        // Until the view has been laid out there is no geometry to magnify against.
        _restCenters = width > 0 ? Magnification.RestCenters(Items.Count, Options, width) : [];
    }

    public void UpdateCursor(double? x)
    {
        var targets = Magnification.ComputeScales(_restCenters, x, Options);
        for (int i = 0; i < Items.Count && i < targets.Length; i++)
            Items[i].TargetScale = targets[i];
    }

    /// <returns><c>true</c> while any icon is still moving.</returns>
    public bool Tick(double dtSeconds)
    {
        bool moving = false;
        foreach (var item in Items)
        {
            double next = Smoothing.Step(item.Scale, item.TargetScale, dtSeconds);
            if (Smoothing.IsSettled(next, item.TargetScale)) next = item.TargetScale;
            else moving = true;
            item.Scale = next;
        }
        return moving;
    }

    /// <summary>Index of the icon whose resting center is nearest <paramref name="x"/> (container coordinates).</summary>
    public int IndexFromX(double x)
    {
        int best = 0;
        for (int i = 1; i < _restCenters.Length; i++)
            if (Math.Abs(_restCenters[i] - x) < Math.Abs(_restCenters[best] - x)) best = i;
        return best;
    }

    // ---- Syncing with running windows -------------------------------------------------

    /// <summary>
    /// Regroups pins and windows and updates <see cref="Items"/> in place. Existing items are reused by
    /// key and moved rather than recreated, so their scale animation and bounce subscriptions survive.
    /// </summary>
    public void Refresh()
    {
        var entries = DockGrouping.Group(_pins, _windows.Windows, _unpinnedOrder);
        _unpinnedOrder = entries.Where(e => e.Pin is null).Select(e => e.Key).ToList();
        int oldCount = Items.Count;

        for (int i = 0; i < entries.Count; i++)
        {
            int existing = IndexOfKey(entries[i].Key, start: i);
            if (existing < 0)
            {
                Items.Insert(i, new DockItemViewModel(this, entries[i], Options.IconSize));
                continue;
            }
            if (existing != i) Items.Move(existing, i);
            Items[i].Update(entries[i]);
        }
        while (Items.Count > entries.Count) Items.RemoveAt(Items.Count - 1);

        SetContainerWidth(_containerWidth);
        if (Items.Count != oldCount) LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private int IndexOfKey(string key, int start)
    {
        for (int j = start; j < Items.Count; j++)
            if (Items[j].Key == key) return j;
        return -1;
    }

    // ---- Item actions (called by DockItemViewModel commands) ---------------------------

    internal void Click(DockItemViewModel item)
    {
        if (!item.IsRunning) { Launch(item); return; }

        var foreground = item.Windows.FirstOrDefault(w => w.Handle == _windows.Foreground);
        if (foreground is { IsMinimized: false })
            _windows.Minimize(foreground.Handle);
        else
            _windows.Activate(item.Windows[0].Handle);
    }

    internal void Launch(DockItemViewModel item)
    {
        if (_launcher.TryLaunch(item.LaunchTarget)) item.RaiseBounce();
    }

    internal void TogglePin(DockItemViewModel item)
    {
        if (item.Pin is { } pin) _pins.Remove(pin);
        else _pins.Add(item.LaunchTarget);
        SaveAndRefresh();
    }

    internal void Quit(DockItemViewModel item)
    {
        foreach (var w in item.Windows) _windows.Close(w.Handle);
    }

    /// <summary>Moves a pin to a new position. Indices are dock positions; drops past the last pin clamp to it.</summary>
    public void MovePin(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _pins.Count) return;
        toIndex = Math.Clamp(toIndex, 0, _pins.Count - 1);
        if (toIndex == fromIndex) return;

        var pin = _pins[fromIndex];
        _pins.RemoveAt(fromIndex);
        _pins.Insert(toIndex, pin);
        SaveAndRefresh();
    }

    private void SaveAndRefresh()
    {
        _store.Save(new DockConfig([.. _pins], _settings));
        Refresh();
    }
}
