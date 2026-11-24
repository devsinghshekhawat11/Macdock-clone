namespace MacDock.Core.Config;

public interface IConfigStore
{
    /// <summary>Loads the config. Never throws for a missing or unreadable file; falls back to <see cref="DockConfig.Default"/>.</summary>
    DockConfig Load();

    void Save(DockConfig config);
}
