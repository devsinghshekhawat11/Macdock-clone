using MacDock.Core.Dock;

namespace MacDock.Core.Tests.Dock;

public class MagnificationTests
{
    private static readonly MagnificationOptions Opt = new();

    private static double[] Centers(int n) => Magnification.RestCenters(n, Opt, 1000);

    [Fact]
    public void NullCursor_AllScalesAreOne()
    {
        var scales = Magnification.ComputeScales(Centers(5), null, Opt);
        Assert.All(scales, s => Assert.Equal(1.0, s));
    }

    [Fact]
    public void CursorOnIconCenter_ThatIconIsMaxScale()
    {
        var c = Centers(5);
        var scales = Magnification.ComputeScales(c, c[2], Opt);
        Assert.Equal(1.8, scales[2], 9);
    }

    [Fact]
    public void ScalesDecreaseWithDistance()
    {
        var c = Centers(5);
        var scales = Magnification.ComputeScales(c, c[0], Opt);
        for (int i = 1; i < scales.Length; i++)
            Assert.True(scales[i] <= scales[i - 1], $"scale[{i}]={scales[i]} > scale[{i - 1}]={scales[i - 1]}");
        Assert.True(scales[1] > 1.0 && scales[1] < 1.8);
    }

    [Fact]
    public void BeyondRange_ScaleIsOne()
    {
        var c = Centers(3);
        var scales = Magnification.ComputeScales(c, c[^1] + Opt.Range + 1, Opt);
        Assert.All(scales, s => Assert.Equal(1.0, s));
    }

    [Fact]
    public void ScalesSymmetricAroundCursor()
    {
        var c = Centers(5);
        var scales = Magnification.ComputeScales(c, c[2], Opt);
        Assert.Equal(scales[1], scales[3], 9);
        Assert.Equal(scales[0], scales[4], 9);
    }

    [Fact]
    public void RestCenters_AreCenteredInContainer()
    {
        var c = Magnification.RestCenters(3, Opt, 400);
        Assert.Equal(new[] { 144.0, 200.0, 256.0 }, c);
    }

    [Fact]
    public void RestCenters_ZeroIcons_IsEmpty()
    {
        Assert.Empty(Magnification.RestCenters(0, Opt, 400));
    }
}
