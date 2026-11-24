namespace MacDock.Core.Config;

/// <summary>An app pinned to the dock.</summary>
/// <param name="Id">Stable identifier, unique within the dock.</param>
/// <param name="DisplayName">Label shown in tooltips.</param>
/// <param name="Target">An exe path, a shell URI (e.g. <c>ms-settings:</c>), or a <c>shell:AppsFolder\…</c> id.</param>
/// <param name="Arguments">Optional command-line arguments.</param>
public sealed record PinnedApp(string Id, string DisplayName, string Target, string? Arguments = null);
