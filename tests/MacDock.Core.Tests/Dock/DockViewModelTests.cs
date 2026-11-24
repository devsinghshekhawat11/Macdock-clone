using MacDock.Core.Config;
using MacDock.Core.Dock;

namespace MacDock.Core.Tests.Dock;

public class DockViewModelTests
{
    private sealed class FakeLauncher(bool result) : IAppLauncher
    {
        public List<PinnedApp> Launched { get; } = [];
        public bool TryLaunch(PinnedApp app) { Launched.Add(app); return result; }
    }

    private sealed class MemoryStore(DockConfig config) : IConfigStore
    {
        public DockConfig Load() => config;
        public void Save(DockConfig c) => config = c;
    }

    private static readonly DockConfig ThreePins = new(
        [new("a", "A", "a.exe"), new("b", "B", "b.exe"), new("c", "C", "c.exe")],
        new DockSettings());

    private static DockViewModel Create(bool launchResult = true, FakeLauncher? launcher = null)
    {
        var vm = new DockViewModel(new MemoryStore(ThreePins), launcher ?? new FakeLauncher(launchResult));
        vm.SetContainerWidth(600);
        return vm;
    }

    [Fact]
    public void LoadsPinsFromConfig()
    {
        var vm = Create();
        Assert.Equal(["a", "b", "c"], vm.Items.Select(i => i.App.Id));
    }

    [Fact]
    public void UpdateCursor_OnFirstIcon_FirstIconGrows()
    {
        var vm = Create();
        double firstCenter = Magnification.RestCenters(3, vm.Options, 600)[0];
        vm.UpdateCursor(firstCenter);
        vm.Tick(10);
        Assert.Equal(1.8, vm.Items[0].Scale, 6);
        Assert.True(vm.Items[0].Scale > vm.Items[2].Scale);
    }

    [Fact]
    public void UpdateCursor_Null_ThenTickLong_AllScalesOne()
    {
        var vm = Create();
        vm.UpdateCursor(300);
        vm.Tick(10);
        vm.UpdateCursor(null);
        vm.Tick(10);
        Assert.All(vm.Items, i => Assert.Equal(1.0, i.Scale));
    }

    [Fact]
    public void Tick_ReturnsTrueWhileMoving_FalseWhenSettled()
    {
        var vm = Create();
        vm.UpdateCursor(300);
        Assert.True(vm.Tick(0.016));
        Assert.False(vm.Tick(10));
    }

    [Fact]
    public void Launch_Success_RaisesBounce()
    {
        var launcher = new FakeLauncher(true);
        var vm = Create(launcher: launcher);
        int bounces = 0;
        vm.Items[1].BounceRequested += (_, _) => bounces++;

        vm.Items[1].LaunchCommand.Execute(null);

        Assert.Equal("b", Assert.Single(launcher.Launched).Id);
        Assert.Equal(1, bounces);
    }

    [Fact]
    public void Launch_Failure_NoBounce_NoThrow()
    {
        var vm = Create(launchResult: false);
        int bounces = 0;
        vm.Items[0].BounceRequested += (_, _) => bounces++;

        vm.Items[0].LaunchCommand.Execute(null);

        Assert.Equal(0, bounces);
    }

    [Fact]
    public void MaxWindowWidth_FitsFullyMagnifiedRow()
    {
        var vm = Create();
        var o = vm.Options;
        // Worst case: every icon at max scale.
        double worst = 3 * o.IconSize * o.MaxScale + 2 * o.Spacing;
        Assert.True(vm.MaxWindowWidth >= worst);
    }

    [Fact]
    public void EmptyDock_DoesNotThrow()
    {
        var vm = new DockViewModel(new MemoryStore(new DockConfig([], new DockSettings())), new FakeLauncher(true));
        vm.SetContainerWidth(600);
        vm.UpdateCursor(10);
        Assert.False(vm.Tick(0.016));
    }
}

public class DockItemSizeTests
{
    private sealed class NoopLauncher : MacDock.Core.Dock.IAppLauncher
    {
        public bool TryLaunch(MacDock.Core.Config.PinnedApp app) => true;
    }

    [Fact]
    public void Size_IsBaseSizeTimesScale_AndNotifies()
    {
        var item = new DockItemViewModel(new("a", "A", "a.exe"), new NoopLauncher(), baseSize: 50);
        var changed = new List<string?>();
        item.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        item.Scale = 1.5;

        Assert.Equal(75, item.Size, 9);
        Assert.Contains(nameof(DockItemViewModel.Size), changed);
    }
}
