using MacDock.Core.Config;
using MacDock.Core.Dock;
using MacDock.Core.Windows;

namespace MacDock.Core.Tests.Dock;

public class DockGroupingTests
{
    private static int _next = 1;
    private static WindowInfo Win(string? exe, string? appId = null, int pid = 100, string title = "t")
        => new(_next++, pid, exe, appId, title, false);

    private static readonly PinnedApp NotepadPin = new("notepad", "Notepad", "notepad.exe");
    private const string NotepadExe = @"C:\Windows\System32\notepad.exe";
    private const string FrameHost = @"C:\Windows\System32\ApplicationFrameHost.exe";

    [Fact]
    public void NoWindows_OnlyPins_NotRunning()
    {
        var e = Assert.Single(DockGrouping.Group([NotepadPin], [], []));
        Assert.Same(NotepadPin, e.Pin);
        Assert.False(e.IsRunning);
    }

    [Fact]
    public void BareExePin_MatchesWindowByFileName()
    {
        var e = Assert.Single(DockGrouping.Group([NotepadPin], [Win(NotepadExe)], []));
        Assert.True(e.IsRunning);
    }

    [Fact]
    public void FullPathPin_DoesNotMatchSameNameInOtherFolder()
    {
        var pin = new PinnedApp("np", "Notepad", @"C:\Tools\notepad.exe");
        var entries = DockGrouping.Group([pin], [Win(NotepadExe)], []);
        Assert.Equal(2, entries.Count);
        Assert.False(entries[0].IsRunning);
    }

    [Fact]
    public void TwoWindowsSameApp_OneEntryTwoWindows()
    {
        var entries = DockGrouping.Group([], [Win(@"C:\A\brave.exe", pid: 1), Win(@"C:\A\Brave.exe", pid: 2)], []);
        var e = Assert.Single(entries);
        Assert.Equal(2, e.Windows.Count);
        Assert.Null(e.Pin);
        Assert.Equal("brave", e.DisplayName);
        Assert.Equal(@"C:\A\brave.exe", e.IconTarget);
    }

    [Fact]
    public void AppsFolderPin_MatchesByAppId()
    {
        var pin = new PinnedApp("calc", "Calculator", @"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App");
        var e = Assert.Single(DockGrouping.Group([pin], [Win(FrameHost, "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App")], []));
        Assert.True(e.IsRunning);
    }

    [Fact]
    public void FrameHostWindows_GroupByAppId_NotByExe()
    {
        var entries = DockGrouping.Group([], [Win(FrameHost, "App.One!App", title: "Calculator"), Win(FrameHost, "App.Two!App", title: "Settings")], []);
        Assert.Equal(2, entries.Count);
        Assert.Equal(@"shell:AppsFolder\App.One!App", entries[0].IconTarget);
        Assert.Equal("Calculator", entries[0].DisplayName);
    }

    [Fact]
    public void NullExePath_KeyedByPid_NotMerged()
    {
        var entries = DockGrouping.Group([], [Win(null, pid: 7, title: "Task Manager"), Win(null, pid: 8, title: "Other")], []);
        Assert.Equal(2, entries.Count);
        Assert.Equal("pid:7", entries[0].Key);
        Assert.Equal("Task Manager", entries[0].DisplayName);
    }

    [Fact]
    public void UnpinnedOrder_IsStable()
    {
        var a = Win(@"C:\a.exe"); var b = Win(@"C:\b.exe"); var c = Win(@"C:\c.exe");
        var entries = DockGrouping.Group([], [a, c, b], [AppKey.ForWindow(b), AppKey.ForWindow(a)]);
        Assert.Equal([@"c:\b.exe", @"c:\a.exe", @"c:\c.exe"], entries.Select(e => e.Key));
    }

    [Fact]
    public void PinsAlwaysFirst_InConfigOrder()
    {
        var p1 = new PinnedApp("x", "X", @"C:\x.exe");
        var p2 = new PinnedApp("y", "Y", @"C:\y.exe");
        var entries = DockGrouping.Group([p1, p2], [Win(@"C:\z.exe"), Win(@"C:\y.exe")], []);
        Assert.Equal(["x", "y"], entries.Take(2).Select(e => e.Pin!.Id));
        Assert.Null(entries[2].Pin);
    }

    [Fact]
    public void PackagedAppUnderWindowsApps_UsesAppsFolderTarget()
    {
        const string terminal = @"C:\Program Files\WindowsApps\Microsoft.WindowsTerminal_1.21.0.0_x64__8wekyb3d8bbwe\WindowsTerminal.exe";
        var e = Assert.Single(DockGrouping.Group([], [Win(terminal, "Microsoft.WindowsTerminal_8wekyb3d8bbwe!App")], []));
        Assert.Equal(@"shell:AppsFolder\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", e.IconTarget);
    }
}
