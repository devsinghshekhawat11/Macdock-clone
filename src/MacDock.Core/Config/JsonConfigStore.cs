using System.Text.Json;

namespace MacDock.Core.Config;

/// <summary>
/// Stores <see cref="DockConfig"/> as indented JSON. Saves are atomic (write to a temp file, then
/// rename over the original) so a crash mid-save can never leave a half-written config.
/// </summary>
public sealed class JsonConfigStore(string path) : IConfigStore
{
    // Lenient reading because people edit this file by hand: any casing, trailing commas and // comments.
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>Default location: <c>%APPDATA%\MacDock\config.json</c>.</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MacDock", "config.json");

    public DockConfig Load()
    {
        if (!File.Exists(path)) return DockConfig.Default;

        string text;
        try { text = File.ReadAllText(path); }
        catch (IOException) { return DockConfig.Default; }
        catch (UnauthorizedAccessException) { return DockConfig.Default; }

        if (string.IsNullOrWhiteSpace(text)) return DockConfig.Default;

        try
        {
            var config = JsonSerializer.Deserialize<DockConfig>(text, Json);
            return config is null ? DockConfig.Default : config.Normalize();
        }
        catch (JsonException)
        {
            // Keep the user's broken file so hand edits aren't silently lost (best effort).
            try { File.Copy(path, path + ".bak", overwrite: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return DockConfig.Default;
        }
    }

    public void Save(DockConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(config, Json));
        File.Move(tmp, path, overwrite: true);
    }
}
