using MacDock.Core.Dock;

namespace MacDock.Core.Tests.Dock;

public class SmoothingTests
{
    [Fact]
    public void ZeroDt_NoChange() => Assert.Equal(1.0, Smoothing.Step(1.0, 1.8, 0));

    [Fact]
    public void LargeDt_ReachesTarget() => Assert.Equal(1.8, Smoothing.Step(1.0, 1.8, 10), 6);

    [Fact]
    public void TwoHalfSteps_EqualOneFullStep()
    {
        double once = Smoothing.Step(1.0, 1.8, 0.032);
        double twice = Smoothing.Step(Smoothing.Step(1.0, 1.8, 0.016), 1.8, 0.016);
        Assert.Equal(once, twice, 9);
    }

    [Fact]
    public void IsSettled_TrueWithinEpsilon()
    {
        Assert.True(Smoothing.IsSettled(1.0005, 1.0));
        Assert.False(Smoothing.IsSettled(1.01, 1.0));
    }
}
