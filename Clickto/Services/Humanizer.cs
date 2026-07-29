using System;

namespace Clickto.Services;

/// <summary>
/// Adds small random variation to timing and position so playback does not
/// land on identical values every repetition.
/// </summary>
public static class Humanizer
{
    // Shared instance is fine here. Playback is single threaded.
    private static readonly Random Rng = new();

    /// <summary>
    /// Scales a delay by a random factor within plus or minus percent.
    /// A percent of 15 means the result lands between 85% and 115%.
    /// </summary>
    public static int Delay(int ms, int percent)
    {
        if (percent <= 0 || ms <= 0) return ms;

        double spread = Math.Clamp(percent, 0, 90) / 100.0;
        double factor = 1.0 + ((Rng.NextDouble() * 2.0) - 1.0) * spread;
        return (int)Math.Round(ms * factor);
    }

    /// <summary>
    /// Offsets a coordinate by up to plus or minus pixels. Uses a triangular
    /// distribution so small offsets are more common than large ones, which
    /// is closer to how a real hand misses a target.
    /// </summary>
    public static double Position(double value, int pixels)
    {
        if (pixels <= 0) return value;

        double offset = (Rng.NextDouble() + Rng.NextDouble() - 1.0) * pixels;
        return value + offset;
    }

    /// <summary>
    /// Produces a realistic press duration. Real clicks are not instant, they
    /// hold for roughly 60 to 130 ms. Deliberate holds are left alone.
    /// </summary>
    public static int Hold(int holdMs)
    {
        if (holdMs > 0) return Delay(holdMs, 10);
        return Rng.Next(60, 131);
    }
}