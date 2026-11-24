using MacDock.Core.Config;
using MacDock.Core.Dock;
using MacDock.Core.Windows;

namespace MacDock.Core.Tests.Dock;

internal sealed class FakeLauncher(bool result = true) : IAppLauncher
{
    public List<PinnedApp> Launched { get; } = [];
    public bool TryLaunch(PinnedApp app) { Launched.Add(app); return result; }
}

internal sealed class MemoryStore(DockConfig config) : IConfigStore
{
    public DockConfig Current { get; private set; } = config;
    public int Saves { get; private set; }
    public DockConfig Load() => Current;
    public void Save(DockConfig c) { Current = c; Saves++; }
}

internal sealed class FakeWindows : IWindowService
{
    public IReadOnlyList<WindowInfo> Windows { get; private set; } = [];
    public nint Foreground { get; private set; }
    public event EventHandler? Changed;
    public List<string> Calls { get; } = [];

    public void Set(nint foreground, params WindowInfo[] windows)
    {
        Windows = windows;
        Foreground = foreground;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Activate(nint h) => Calls.Add($"activate:{h}");
    public void Minimize(nint h) => Calls.Add($"minimize:{h}");
    public void Close(nint h) => Calls.Add($"close:{h}");
}

public class DockViewModelTests
{
    private static readonly DockConfig ThreePins = new(
        [new("a", "A", @"C:\a.exe"), new("b", "B", @"C:\b.exe"), new("c", "C", @"C:\c.exe")],
        new DockSettings());

    private static WindowInfo Win(nint h, string exe, bool minimized = false) => new(h, 1, exe, null, "title", minimized);

    private sealed record Fixture(DockViewModel Vm, MemoryStore Store, FakeLauncher Launcher, FakeWindows Windows);

    private static Fixture Create(DockConfig? config = null, bool launchResult = true)
    {
        var store = new MemoryStore(config ?? ThreePins);
        var launcher = new FakeLauncher(launchResult);
        var windows = new FakeWindows();
        var vm = new DockViewModel(store, launcher, windows);
        vm.SetContainerWidth(600);
        return new Fixture(vm, store, launcher, windows);
    }

    private static string[] Keys(DockViewModel vm) => vm.Items.Select(i => i.Key).ToArray();

    // --- M1 behaviour -------------------------------------------------------------------

    [Fact]
    public void LoadsPinsFromConfig() => Assert.Equal(["a", "b", "c"], Create().Vm.Items.Select(i => i.Pin!.Id));

    [Fact]
    public void UpdateCursor_OnFirstIcon_FirstIconGrows()
    {
        var vm = Create().Vm;
        vm.UpdateCursor(Magnification.RestCenters(3, vm.Options, 600)[0]);
        vm.Tick(10);
        Assert.Equal(1.8, vm.Items[0].Scale, 6);
        Assert.True(vm.Items[0].Scale > vm.Items[2].Scale);
    }

    [Fact]
    public void UpdateCursor_Null_ThenTickLong_AllScalesOne()
    {
        var vm = Create().Vm;
        vm.UpdateCursor(300); vm.Tick(10);
        vm.UpdateCursor(null); vm.Tick(10);
        Assert.All(vm.Items, i => Assert.Equal(1.0, i.Scale));
    }

    [Fact]
    public void Tick_ReturnsTrueWhileMoving_FalseWhenSettled()
    {
        var vm = Create().Vm;
        vm.UpdateCursor(300);
        Assert.True(vm.Tick(0.016));
        Assert.False(vm.Tick(10));
    }

    [Fact]
    public void UpdateCursor_BeforeContainerWidth_DoesNotThrow()
    {
        var vm = new DockViewModel(new MemoryStore(ThreePins), new FakeLauncher(), new FakeWindows());
        vm.UpdateCursor(10);
        Assert.False(vm.Tick(0.016));
    }

    [Fact]
    public void MaxWindowWidth_FitsFullyMagnifiedRow()
    {
        var vm = Create().Vm;
        var o = vm.Options;
        Assert.True(vm.MaxWindowWidth >= 3 * o.IconSize * o.MaxScale + 2 * o.Spacing);
    }

    [Fact]
    public void EmptyDock_DoesNotThrow()
    {
        var f = Create(new DockConfig([], new DockSettings()));
        f.Vm.UpdateCursor(10);
        Assert.False(f.Vm.Tick(0.016));
    }

    // --- Clicking -----------------------------------------------------------------------

    [Fact]
    public void Click_NotRunning_Launches_AndBounces()
    {
        var f = Create();
        int bounces = 0;
        f.Vm.Items[1].BounceRequested += (_, _) => bounces++;
        f.Vm.Items[1].ClickCommand.Execute(null);
        Assert.Equal("b", Assert.Single(f.Launcher.Launched).Id);
        Assert.Equal(1, bounces);
    }

    [Fact]
    public void Click_LaunchFails_NoBounce_NoThrow()
    {
        var f = Create(launchResult: false);
        int bounces = 0;
        f.Vm.Items[0].BounceRequested += (_, _) => bounces++;
        f.Vm.Items[0].ClickCommand.Execute(null);
        Assert.Equal(0, bounces);
    }

    [Fact]
    public void Click_RunningNotForeground_ActivatesTopmostWindow()
    {
        var f = Create();
        f.Windows.Set(foreground: 99, Win(10, @"C:\a.exe"), Win(11, @"C:\a.exe"));
        f.Vm.Items[0].ClickCommand.Execute(null);
        Assert.Equal(["activate:10"], f.Windows.Calls);
        Assert.Empty(f.Launcher.Launched);
    }

    [Fact]
    public void Click_RunningAndForeground_MinimizesForeground()
    {
        var f = Create();
        f.Windows.Set(foreground: 11, Win(10, @"C:\a.exe"), Win(11, @"C:\a.exe"));
        f.Vm.Items[0].ClickCommand.Execute(null);
        Assert.Equal(["minimize:11"], f.Windows.Calls);
    }

    [Fact]
    public void Click_RunningMinimized_Activates()
    {
        var f = Create();
        f.Windows.Set(foreground: 10, Win(10, @"C:\a.exe", minimized: true));
        f.Vm.Items[0].ClickCommand.Execute(null);
        Assert.Equal(["activate:10"], f.Windows.Calls);
    }

    [Fact]
    public void NewWindow_AlwaysLaunches()
    {
        var f = Create();
        f.Windows.Set(0, Win(10, @"C:\a.exe"));
        f.Vm.Items[0].NewWindowCommand.Execute(null);
        Assert.Single(f.Launcher.Launched);
    }

    // --- Running apps -------------------------------------------------------------------

    [Fact]
    public void RunningPinnedApp_IsRunning()
    {
        var f = Create();
        f.Windows.Set(0, Win(10, @"C:\b.exe"));
        Assert.Equal([false, true, false], f.Vm.Items.Select(i => i.IsRunning));
    }

    [Fact]
    public void UnpinnedRunningApp_AppearsAfterPins()
    {
        var f = Create();
        f.Windows.Set(0, Win(10, @"C:\z.exe"));
        Assert.Equal(4, f.Vm.Items.Count);
        var z = f.Vm.Items[3];
        Assert.False(z.IsPinned);
        Assert.True(z.IsRunning);
        Assert.Equal("z", z.DisplayName);
    }

    [Fact]
    public void Refresh_ReusesItemInstances_AndKeepsScale()
    {
        var f = Create();
        var first = f.Vm.Items[0];
        first.Scale = 1.5;
        f.Windows.Set(0, Win(10, @"C:\a.exe"), Win(11, @"C:\z.exe"));
        Assert.Same(first, f.Vm.Items[0]);
        Assert.Equal(1.5, f.Vm.Items[0].Scale);
    }

    [Fact]
    public void WindowClosed_ItemRemoved_OthersKeepOrder()
    {
        var f = Create();
        f.Windows.Set(0, Win(10, @"C:\x.exe"), Win(11, @"C:\y.exe"), Win(12, @"C:\z.exe"));
        var z = f.Vm.Items.Single(i => i.Key == @"c:\z.exe");
        f.Windows.Set(0, Win(10, @"C:\x.exe"), Win(12, @"C:\z.exe"));
        Assert.Equal(["pin:a", "pin:b", "pin:c", @"c:\x.exe", @"c:\z.exe"], Keys(f.Vm));
        Assert.Same(z, f.Vm.Items[4]);
    }

    [Fact]
    public void Refresh_RecomputesMagnification_ForStillCursor()
    {
        var f = Create();
        double x = Magnification.RestCenters(3, f.Vm.Options, 600)[0];
        f.Vm.UpdateCursor(x);
        f.Windows.Set(0, Win(10, @"C:\z.exe")); // 4 icons now: every rest center moved

        var expected = Magnification.ComputeScales(Magnification.RestCenters(4, f.Vm.Options, 600), x, f.Vm.Options);
        Assert.Equal(expected, f.Vm.Items.Select(i => i.TargetScale));
    }

    [Fact]
    public void ItemCountChange_RaisesLayoutChanged()
    {
        var f = Create();
        int raised = 0;
        f.Vm.LayoutChanged += (_, _) => raised++;
        f.Windows.Set(0, Win(10, @"C:\a.exe")); // count unchanged
        Assert.Equal(0, raised);
        f.Windows.Set(0, Win(11, @"C:\z.exe"));
        Assert.Equal(1, raised);
    }

    // --- Pin / unpin / quit / reorder ---------------------------------------------------

    [Fact]
    public void Pin_RunningApp_SavesConfig()
    {
        var f = Create();
        f.Windows.Set(0, Win(10, @"C:\z.exe"));
        f.Vm.Items[3].TogglePinCommand.Execute(null);
        var pin = f.Store.Current.Pins[^1];
        Assert.Equal(@"C:\z.exe", pin.Target);
        Assert.Equal("z", pin.DisplayName);
        Assert.True(f.Vm.Items[3].IsPinned);
        Assert.True(f.Vm.Items[3].IsRunning);
    }

    [Fact]
    public void Unpin_RunningApp_StaysAsUnpinned_AndSavesConfig()
    {
        var f = Create();
        f.Windows.Set(0, Win(10, @"C:\a.exe"));
        f.Vm.Items[0].TogglePinCommand.Execute(null);
        Assert.Equal(["b", "c"], f.Store.Current.Pins.Select(p => p.Id));
        var last = f.Vm.Items[^1];
        Assert.False(last.IsPinned);
        Assert.True(last.IsRunning);
        Assert.Equal(3, f.Vm.Items.Count);
    }

    [Fact]
    public void Unpin_NotRunning_RemovesItem()
    {
        var f = Create();
        f.Vm.Items[1].TogglePinCommand.Execute(null);
        Assert.Equal(["pin:a", "pin:c"], Keys(f.Vm));
    }

    [Fact]
    public void Quit_ClosesAllWindowsOfApp()
    {
        var f = Create();
        f.Windows.Set(0, Win(10, @"C:\a.exe"), Win(11, @"C:\b.exe"), Win(12, @"C:\a.exe"));
        Assert.True(f.Vm.Items[0].QuitCommand.CanExecute(null));
        Assert.False(f.Vm.Items[2].QuitCommand.CanExecute(null));
        f.Vm.Items[0].QuitCommand.Execute(null);
        Assert.Equal(["close:10", "close:12"], f.Windows.Calls);
    }

    [Fact]
    public void MovePin_ReordersAndSaves()
    {
        var f = Create();
        f.Vm.MovePin(0, 2);
        Assert.Equal(["b", "c", "a"], f.Store.Current.Pins.Select(p => p.Id));
        Assert.Equal(["pin:b", "pin:c", "pin:a"], Keys(f.Vm));
    }

    [Fact]
    public void MovePin_OntoUnpinnedArea_ClampsToLastPin()
    {
        var f = Create();
        f.Windows.Set(0, Win(10, @"C:\z.exe"));
        f.Vm.MovePin(0, 3);
        Assert.Equal(["b", "c", "a"], f.Store.Current.Pins.Select(p => p.Id));
    }

    [Fact]
    public void MovePin_FromUnpinned_Ignored()
    {
        var f = Create();
        f.Windows.Set(0, Win(10, @"C:\z.exe"));
        f.Vm.MovePin(3, 0);
        Assert.Equal(0, f.Store.Saves);
    }

    [Fact]
    public void IndexFromX_NearestCenter_Clamped()
    {
        var vm = Create().Vm;
        var c = Magnification.RestCenters(3, vm.Options, 600);
        Assert.Equal(0, vm.IndexFromX(-500));
        Assert.Equal(1, vm.IndexFromX(c[1] + 3));
        Assert.Equal(2, vm.IndexFromX(5000));
    }
}

public class DockItemSizeTests
{
    [Fact]
    public void Size_IsBaseSizeTimesScale_AndNotifies()
    {
        var f = new DockViewModel(
            new MemoryStore(new DockConfig([new("a", "A", "a.exe")], new DockSettings(IconSize: 50))),
            new FakeLauncher(), new FakeWindows());
        var item = f.Items[0];
        var changed = new List<string?>();
        item.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        item.Scale = 1.5;

        Assert.Equal(75, item.Size, 9);
        Assert.Contains(nameof(DockItemViewModel.Size), changed);
    }
}
