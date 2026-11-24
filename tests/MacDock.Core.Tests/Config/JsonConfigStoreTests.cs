using MacDock.Core.Config;

namespace MacDock.Core.Tests.Config;

public sealed class JsonConfigStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "macdock-tests-" + Guid.NewGuid().ToString("N"));
    private string ConfigPath => Path.Combine(_dir, "config.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void MissingFile_ReturnsDefault()
    {
        var config = new JsonConfigStore(ConfigPath).Load();
        Assert.Equal(DockConfig.Default.Pins, config.Pins);
        Assert.Equal(DockConfig.Default.Settings, config.Settings);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = new JsonConfigStore(ConfigPath);
        var original = new DockConfig(
            [new PinnedApp("code", "VS Code", @"C:\Apps\Code.exe", "--new-window")],
            new DockSettings(IconSize: 56, MaxScale: 2.0, AutoHide: true));

        store.Save(original);
        var loaded = store.Load();

        Assert.Equal(original.Pins, loaded.Pins);
        Assert.Equal(original.Settings, loaded.Settings);
    }

    [Fact]
    public void CorruptJson_ReturnsDefault_AndKeepsBackup()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(ConfigPath, "{ not json");

        var config = new JsonConfigStore(ConfigPath).Load();

        Assert.Equal(DockConfig.Default.Pins, config.Pins);
        Assert.Equal("{ not json", File.ReadAllText(ConfigPath + ".bak"));
    }

    [Fact]
    public void EmptyFile_ReturnsDefault()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(ConfigPath, "");
        Assert.Equal(DockConfig.Default.Pins, new JsonConfigStore(ConfigPath).Load().Pins);
    }

    [Fact]
    public void JsonNull_ReturnsDefault()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(ConfigPath, "null");
        Assert.Equal(DockConfig.Default.Pins, new JsonConfigStore(ConfigPath).Load().Pins);
    }

    [Fact]
    public void Save_CreatesDirectory()
    {
        new JsonConfigStore(ConfigPath).Save(DockConfig.Default);
        Assert.True(File.Exists(ConfigPath));
        Assert.False(File.Exists(ConfigPath + ".tmp"));
    }
}
