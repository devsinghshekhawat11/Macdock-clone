namespace MacDock.Core.Dock;

/// <summary>
/// Frame-rate-independent exponential easing. Moving a fixed fraction per frame would make the
/// animation speed depend on the monitor's refresh rate; scaling by elapsed time keeps it identical
/// at 60 Hz and 144 Hz (two 8 ms steps equal one 16 ms step).
/// </summary>
public static class Smoothing
{
    /// <param name="speed">Higher is snappier. 18 ≈ 90% of the way in ~130 ms.</param>
    public static double Step(double current, double target, double dtSeconds, double speed = 18)
        => current + (target - current) * (1 - Math.Exp(-speed * dtSeconds));

    public static bool IsSettled(double current, double target, double epsilon = 0.001)
        => Math.Abs(current - target) <= epsilon;
}
