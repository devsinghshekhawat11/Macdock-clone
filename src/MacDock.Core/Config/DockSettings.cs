namespace MacDock.Core.Config;

/// <summary>User-adjustable dock settings.</summary>
public sealed record DockSettings(double IconSize = 48, double MaxScale = 1.8, bool AutoHide = false)
{
    public const double MinIconSize = 16, MaxIconSize = 256;
    public const double MinMaxScale = 1.0, MaxMaxScale = 3.0;
}
