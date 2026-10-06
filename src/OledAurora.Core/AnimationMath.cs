using System;

namespace OledAurora.Core;

public static class AnimationMath
{
    public static double Envelope(double seconds, double duration)
    {
        if (duration <= 0 || seconds <= 0 || seconds >= duration) return 0;
        return SmoothStep(seconds / (duration * .20)) * SmoothStep((duration - seconds) / (duration * .25));
    }

    public static double SmoothStep(double value)
    {
        value = Math.Clamp(value, 0, 1);
        return value * value * (3 - 2 * value);
    }

    public static int CoverageHeight(int screenHeight, double percent)
        => Math.Max(1, (int)Math.Round(screenHeight * percent / 100, MidpointRounding.AwayFromZero));
}
