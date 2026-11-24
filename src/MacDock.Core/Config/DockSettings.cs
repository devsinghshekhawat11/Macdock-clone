namespace MacDock.Core.Config;

/// <summary>User-adjustable dock settings.</summary>
public sealed record DockSettings(double IconSize = 48, double MaxScale = 1.8, bool AutoHide = false);
