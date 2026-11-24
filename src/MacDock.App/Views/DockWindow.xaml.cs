using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using MacDock.App.Converters;
using MacDock.Core.Dock;
using MacDock.Interop;

namespace MacDock.App.Views;

/// <summary>
/// The dock surface. Code-behind is limited to view concerns: window placement, forwarding the
/// cursor to the view model, and running the per-frame animation loop only while something moves.
/// </summary>
public partial class DockWindow : Window
{
    /// <summary>Extra space above the fully magnified icons so the launch bounce isn't clipped.</summary>
    private const double BounceHeadroom = 30;
    private const double BottomChrome = 6 + 9; // DockArea margin + icon row margin
    private const double SideChrome = 9 * 2 + 40; // icon row margins + breathing room

    private static readonly Brush HoverZoneBrush = CreateHoverZoneBrush();

    private readonly DockViewModel _vm;
    private readonly Stopwatch _clock = new();
    private bool _animating;

    public DockWindow(DockViewModel vm, IIconProvider icons)
    {
        // Must exist before InitializeComponent resolves {StaticResource TargetToIcon}.
        Resources.Add("TargetToIcon", new TargetToIconConverter(icons));
        Resources.Add("BoolToVisibility", new BooleanToVisibilityConverter());
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        Root.SizeChanged += (_, _) => _vm.SetContainerWidth(Root.ActualWidth);
        _vm.LayoutChanged += (_, _) => PlaceOnScreen();
        SystemParameters.StaticPropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SystemParameters.WorkArea)) PlaceOnScreen();
        };
        PlaceOnScreen();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowStyles.MakeShellSurface(new WindowInteropHelper(this).Handle);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi); // WPF moves the window to Windows' suggested rect…
        PlaceOnScreen();                  // …then we re-pin it to the bottom-center at the new DPI.
    }

    /// <summary>
    /// Sizes the window once to the fully magnified bounds and pins it to the bottom-center of the
    /// primary work area. The work area is read in physical pixels and converted with this window's
    /// current DPI, so it stays correct when display scaling changes while the dock is running.
    /// </summary>
    private void PlaceOnScreen()
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        Rect px = MonitorInfo.PrimaryWorkAreaPixels();
        var area = new Rect(px.X / dpi.DpiScaleX, px.Y / dpi.DpiScaleY, px.Width / dpi.DpiScaleX, px.Height / dpi.DpiScaleY);
        Width = Math.Min(_vm.MaxWindowWidth + SideChrome, area.Width);
        Height = _vm.Options.IconSize * _vm.Options.MaxScale + BottomChrome + BounceHeadroom;
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Bottom - Height;
    }

    private void OnDockMouseMove(object sender, MouseEventArgs e)
    {
        HoverZone.Background = HoverZoneBrush;
        _vm.UpdateCursor(e.GetPosition(Root).X);
        StartAnimating();
    }

    private void OnDockMouseLeave(object sender, MouseEventArgs e)
    {
        HoverZone.Background = null;
        _vm.UpdateCursor(null);
        StartAnimating();
    }

    private void StartAnimating()
    {
        if (_animating) return;
        _animating = true;
        _clock.Restart();
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        // Clamp so a long stall (e.g. resume from sleep) doesn't produce one huge jump.
        double dt = Math.Min(_clock.Elapsed.TotalSeconds, 0.1);
        _clock.Restart();

        if (!_vm.Tick(dt))
        {
            // Idle: unhook so the dock costs ~0% CPU when nothing is moving.
            CompositionTarget.Rendering -= OnRendering;
            _animating = false;
        }
    }

    // ---- Drag to reorder pins ---------------------------------------------------------
    // Mouse down remembers the item; moving past a small threshold turns the press into a drag by taking
    // mouse capture away from the button (which also cancels its click). Mouse up drops at the nearest slot.

    private const double DragThreshold = 6;
    private DockItemViewModel? _pressedItem;
    private FrameworkElement? _pressedElement;
    private Point _pressPoint;
    private bool _dragging;

    private void OnIconMouseDown(object sender, MouseButtonEventArgs e)
    {
        _pressedElement = sender as FrameworkElement;
        _pressedItem = _pressedElement?.DataContext as DockItemViewModel;
        _pressPoint = e.GetPosition(Root);
    }

    private void OnDockPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_pressedItem is null || _dragging || e.LeftButton != MouseButtonState.Pressed) return;
        if (!_pressedItem.IsPinned) return;
        if (Math.Abs(e.GetPosition(Root).X - _pressPoint.X) < DragThreshold) return;

        _dragging = true;
        if (_pressedElement is not null) _pressedElement.Opacity = 0.6;
        Mouse.Capture(DockArea, CaptureMode.SubTree);
    }

    private void OnDockMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragging && _pressedItem is not null)
        {
            int from = _vm.Items.IndexOf(_pressedItem);
            int to = _vm.IndexFromX(e.GetPosition(Root).X);
            EndDrag();
            _vm.MovePin(from, to);
            e.Handled = true; // a drag is not a click
            return;
        }
        _pressedItem = null;
        _pressedElement = null;
    }

    private void OnDockLostCapture(object sender, MouseEventArgs e)
    {
        if (_dragging && !DockArea.IsMouseCaptureWithin) EndDrag();
    }

    private void EndDrag()
    {
        _dragging = false;
        if (_pressedElement is not null) _pressedElement.Opacity = 1;
        _pressedItem = null;
        _pressedElement = null;
        if (DockArea.IsMouseCaptured) DockArea.ReleaseMouseCapture();
    }

    private void OnQuitClick(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private static Brush CreateHoverZoneBrush()
    {
        var brush = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
        brush.Freeze();
        return brush;
    }
}
