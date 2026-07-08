namespace Clickto.Models;

/// <summary>
/// One recorded action: click at (X, Y) after waiting DelayMs
/// milliseconds since the previous step.
/// </summary>
public class ClickStep
{
    public double X { get; set; }
    public double Y { get; set; }

    // Milliseconds to wait BEFORE performing this click,
    // measured from the end of the previous step.
    public int DelayMs { get; set; }

    public ClickStep() { }

    public ClickStep(double x, double y, int delayMs)
    {
        X = x;
        Y = y;
        DelayMs = delayMs;
    }
}