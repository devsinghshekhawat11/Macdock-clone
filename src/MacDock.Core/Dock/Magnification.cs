namespace MacDock.Core.Dock;

/// <summary>Tunable parameters for dock magnification. All lengths are in device-independent pixels (DIPs).</summary>
/// <param name="IconSize">Resting icon edge length.</param>
/// <param name="Spacing">Gap between resting icons.</param>
/// <param name="MaxScale">Scale of the icon directly under the cursor.</param>
/// <param name="RangeFactor">Influence radius, in multiples of <paramref name="IconSize"/>.</param>
public sealed record MagnificationOptions(
    double IconSize = 48,
    double Spacing = 8,
    double MaxScale = 1.8,
    double RangeFactor = 3)
{
    /// <summary>Distance from the cursor (DIPs) beyond which icons are not magnified.</summary>
    public double Range => IconSize * RangeFactor;
}

/// <summary>
/// Pure functions behind the dock's "fish-eye" effect.
/// Scales are always computed from <em>rest</em> (unscaled) icon centers: if we used the
/// current, already-magnified positions, every frame's layout would shift the inputs of the
/// next frame and the icons would jitter.
/// </summary>
public static class Magnification
{
    /// <summary>Centers of <paramref name="count"/> unscaled icons laid out in a row centered in a container.</summary>
    public static double[] RestCenters(int count, MagnificationOptions o, double containerWidth)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count == 0) return [];

        double total = count * o.IconSize + (count - 1) * o.Spacing;
        double first = (containerWidth - total) / 2 + o.IconSize / 2;
        var centers = new double[count];
        for (int i = 0; i < count; i++)
            centers[i] = first + i * (o.IconSize + o.Spacing);
        return centers;
    }

    /// <summary>
    /// Scale factor for each icon given the cursor's x position (or <c>null</c> when the cursor is not over the dock).
    /// Uses a raised-cosine falloff: smooth at the peak and reaching exactly 1.0 at <see cref="MagnificationOptions.Range"/>.
    /// </summary>
    public static double[] ComputeScales(IReadOnlyList<double> restCenters, double? cursorX, MagnificationOptions o)
    {
        var scales = new double[restCenters.Count];
        for (int i = 0; i < scales.Length; i++)
        {
            if (cursorX is not double x) { scales[i] = 1.0; continue; }

            double d = Math.Abs(restCenters[i] - x);
            double f = d >= o.Range ? 0 : (Math.Cos(Math.PI * d / o.Range) + 1) / 2;
            scales[i] = 1 + (o.MaxScale - 1) * f;
        }
        return scales;
    }
}
