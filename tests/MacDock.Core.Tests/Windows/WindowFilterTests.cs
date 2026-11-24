using MacDock.Core.Windows;

namespace MacDock.Core.Tests.Windows;

public class WindowFilterTests
{
    private static readonly WindowTraits Normal = new(
        IsVisible: true, IsCloaked: false, HasOwner: false, IsToolWindow: false,
        IsAppWindow: false, IsNoActivate: false, Title: "Untitled - Notepad", IsOwnProcess: false);

    [Fact] public void NormalTopLevelWindow_Included() => Assert.True(WindowFilter.IsTaskbarWindow(Normal));
    [Fact] public void Invisible_Excluded() => Assert.False(WindowFilter.IsTaskbarWindow(Normal with { IsVisible = false }));
    [Fact] public void Cloaked_Excluded() => Assert.False(WindowFilter.IsTaskbarWindow(Normal with { IsCloaked = true }));
    [Fact] public void OwnProcess_Excluded() => Assert.False(WindowFilter.IsTaskbarWindow(Normal with { IsOwnProcess = true }));
    [Fact] public void EmptyTitle_Excluded() => Assert.False(WindowFilter.IsTaskbarWindow(Normal with { Title = "  " }));
    [Fact] public void ToolWindow_Excluded() => Assert.False(WindowFilter.IsTaskbarWindow(Normal with { IsToolWindow = true }));
    [Fact] public void OwnedWindow_Excluded() => Assert.False(WindowFilter.IsTaskbarWindow(Normal with { HasOwner = true }));
    [Fact] public void NoActivateWindow_Excluded() => Assert.False(WindowFilter.IsTaskbarWindow(Normal with { IsNoActivate = true }));

    [Fact]
    public void AppWindowStyle_OverridesOwnerAndToolWindow_Included()
        => Assert.True(WindowFilter.IsTaskbarWindow(Normal with { IsAppWindow = true, HasOwner = true, IsToolWindow = true }));
}
