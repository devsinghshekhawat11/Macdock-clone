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

    private DockConfig LoadFrom(string json)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(ConfigPath, json);
        return new JsonConfigStore(ConfigPath).Load();
    }

    [Fact]
    public void LowercasePropertyNames_AreAccepted()
    {
        var config = LoadFrom("""{ "pins": [ { "id": "x", "displayName": "X", "target": "x.exe" } ] }""");
        Assert.Equal("x", Assert.Single(config.Pins).Id);
    }

    [Fact]
    public void TrailingCommasAndComments_AreAccepted()
    {
        var config = LoadFrom("""
            {
              // my pins
              "Pins": [ { "Id": "x", "DisplayName": "X", "Target": "x.exe" }, ],
            }
            """);
        Assert.Equal("x", Assert.Single(config.Pins).Id);
        Assert.False(File.Exists(ConfigPath + ".bak"));
    }

    [Fact]
    public void MissingPinsSection_UsesDefaultPins()
    {
        var config = LoadFrom("""{ "Settings": { "IconSize": 56 } }""");
        Assert.Equal(DockConfig.Default.Pins, config.Pins);
        Assert.Equal(56, config.Settings.IconSize);
    }

    [Fact]
    public void NullPinsAndPinsWithoutTarget_AreDropped()
    {
        var config = LoadFrom("""{ "Pins": [ null, { "Id": "a", "DisplayName": "A", "Target": "" }, { "Id": "b", "DisplayName": "B", "Target": "b.exe" } ] }""");
        Assert.Equal("b", Assert.Single(config.Pins).Id);
    }

    [Theory]
    [InlineData(-48, 16)]
    [InlineData(0, 16)]
    [InlineData(1000, 256)]
    [InlineData(64, 64)]
    public void IconSize_IsClamped(double input, double expected)
    {
        var config = LoadFrom($$"""{ "Settings": { "IconSize": {{input}} } }""");
        Assert.Equal(expected, config.Settings.IconSize);
    }

    [Theory]
    [InlineData(0.2, 1.0)]
    [InlineData(9, 3.0)]
    [InlineData(2.0, 2.0)]
    public void MaxScale_IsClamped(double input, double expected)
    {
        var config = LoadFrom($$"""{ "Settings": { "MaxScale": {{input}} } }""");
        Assert.Equal(expected, config.Settings.MaxScale);
    }

    [Fact]
    public void CorruptJson_BackupFails_StillReturnsDefault()
    {
        Directory.CreateDirectory(_dir);
        Directory.CreateDirectory(ConfigPath + ".bak"); // a directory where the backup file should go makes File.Copy throw
        File.WriteAllText(ConfigPath, "{ not json");

        var config = new JsonConfigStore(ConfigPath).Load();

        Assert.Equal(DockConfig.Default.Pins, config.Pins);
    }
}
