using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MacDock.Core.Config;

namespace MacDock.Core.Dock;

/// <summary>
/// State and behavior of the dock, independent of any UI framework.
/// The view reports the cursor position and drives <see cref="Tick"/> once per rendered frame.
/// </summary>
public sealed partial class DockViewModel : ObservableObject
{
    private double _containerWidth;
    private double[] _restCenters = [];

    public DockViewModel(IConfigStore store, IAppLauncher launcher)
    {
        var config = store.Load();
        Options = new MagnificationOptions(IconSize: config.Settings.IconSize, MaxScale: config.Settings.MaxScale);
        Items = new ObservableCollection<DockItemViewModel>(config.Pins.Select(p => new DockItemViewModel(p, launcher)));
    }

    public ObservableCollection<DockItemViewModel> Items { get; }

    public MagnificationOptions Options { get; }

    /// <summary>Width of the row with every icon at rest.</summary>
    public double RestWidth => Items.Count == 0 ? 0 : Items.Count * Options.IconSize + (Items.Count - 1) * Options.Spacing;

    /// <summary>
    /// A width that can hold the row in any magnification state (every icon at max scale is an upper bound).
    /// The window is sized to this once, so it never has to resize while animating.
    /// </summary>
    public double MaxWindowWidth => Items.Count == 0 ? 0 : Items.Count * Options.IconSize * Options.MaxScale + (Items.Count - 1) * Options.Spacing;

    /// <summary>Tells the model how wide the coordinate space of <see cref="UpdateCursor"/> is.</summary>
    public void SetContainerWidth(double width)
    {
        _containerWidth = width;
        _restCenters = Magnification.RestCenters(Items.Count, Options, _containerWidth);
    }

    /// <summary>Cursor x in container coordinates, or <c>null</c> when the cursor left the dock.</summary>
    public void UpdateCursor(double? x)
    {
        var targets = Magnification.ComputeScales(_restCenters, x, Options);
        for (int i = 0; i < Items.Count && i < targets.Length; i++)
            Items[i].TargetScale = targets[i];
    }

    /// <summary>Advances the animation by <paramref name="dtSeconds"/>.</summary>
    /// <returns><c>true</c> while any icon is still moving; the view can stop its render loop when this is <c>false</c>.</returns>
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
}
