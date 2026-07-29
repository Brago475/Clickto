using System;
using System.Collections.Generic;

namespace Clickto.Services;

/// <summary>
/// Generates intermediate points between two screen positions so the cursor
/// travels instead of teleporting. Entirely optional: when smooth travel is
/// off, none of this runs and playback behaves exactly as before.
/// </summary>
public static class CursorPath
{
    private static readonly Random Rng = new();

    // One step every few milliseconds is enough to look continuous without
    // flooding the input queue.
    private const int StepIntervalMs = 8;

    /// <summary>
    /// Builds a curved path from start to end. The curve comes from a single
    /// control point offset perpendicular to the straight line, which is what
    /// makes it read as a hand movement rather than a ruler.
    /// </summary>
    public static List<(double X, double Y)> Build(
        double startX, double startY, double endX, double endY, int durationMs)
    {
        var points = new List<(double, double)>();

        double dx = endX - startX;
        double dy = endY - startY;
        double distance = Math.Sqrt(dx * dx + dy * dy);

        // Very short hops are not worth animating.
        if (distance < 4) return points;

        int steps = Math.Max(2, durationMs / StepIntervalMs);

        // Perpendicular offset, scaled to distance, so long moves bow more
        // than short ones. Capped so it never looks like a detour.
        double bow = Math.Min(distance * 0.12, 60) * (Rng.NextDouble() * 2 - 1);
        double midX = (startX + endX) / 2 - (dy / distance) * bow;
        double midY = (startY + endY) / 2 + (dx / distance) * bow;

        for (int i = 1; i <= steps; i++)
        {
            double t = (double)i / steps;
            double eased = EaseInOut(t);

            // Quadratic bezier through the offset midpoint.
            double u = 1 - eased;
            double x = u * u * startX + 2 * u * eased * midX + eased * eased * endX;
            double y = u * u * startY + 2 * u * eased * midY + eased * eased * endY;

            points.Add((x, y));
        }

        return points;
    }

    /// <summary>Delay between each generated point.</summary>
    public static int StepDelayMs => StepIntervalMs;

    /// <summary>
    /// Slow at both ends, fast through the middle. Real hand movement
    /// accelerates away from rest and decelerates onto the target.
    /// </summary>
    private static double EaseInOut(double t)
        => t < 0.5
            ? 2 * t * t
            : 1 - Math.Pow(-2 * t + 2, 2) / 2;

    /// <summary>
    /// How long a move of this distance should take. Loosely follows Fitts's
    /// law: longer moves take more time, but not proportionally more.
    /// </summary>
    public static int DurationFor(double distance, double speed)
    {
        if (distance < 4) return 0;

        double ms = 90 + 130 * Math.Log2(1 + distance / 90.0);
        ms /= Math.Max(0.1, speed);

        return (int)Math.Clamp(ms, 30, 1200);
    }
}
