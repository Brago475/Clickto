namespace Clickto.Models;

/// <summary>
/// One recorded action. Defaults describe a plain single left click, so
/// preset files written before these fields existed still load correctly.
/// </summary>
public class ClickStep
{
    public double X { get; set; }
    public double Y { get; set; }

    // Milliseconds to wait BEFORE performing this action,
    // measured from the end of the previous step.
    public int DelayMs { get; set; }

    // --- Added in v1.2. Old presets get these defaults on load. ---

    public ActionType Type { get; set; } = ActionType.Click;

    public MouseButton Button { get; set; } = MouseButton.Left;

    /// <summary>1 for single, 2 for double, and so on.</summary>
    public int ClickCount { get; set; } = 1;

    /// <summary>How long the button stays down. 0 is a normal click.</summary>
    public int HoldMs { get; set; }

    /// <summary>Scroll wheel notches. Positive is up, negative is down.</summary>
    public int ScrollAmount { get; set; }

    /// <summary>Platform key code for KeyPress actions.</summary>
    public long KeyCode { get; set; }

    /// <summary>Skipped during playback but kept in the timeline.</summary>
    public bool IsMuted { get; set; }

    public ClickStep() { }

    public ClickStep(double x, double y, int delayMs)
    {
        X = x;
        Y = y;
        DelayMs = delayMs;
    }

    /// <summary>Short human label for the timeline, for example "Right Click (Hold)".</summary>
    public string TypeLabel => Type switch
    {
        ActionType.Move => "Move",
        ActionType.Scroll => ScrollAmount >= 0 ? "Scroll Up" : "Scroll Down",
        ActionType.KeyPress => "Key Press",
        ActionType.Delay => "Delay",
        _ => BuildClickLabel()
    };

    private string BuildClickLabel()
    {
        string button = Button switch
        {
            MouseButton.Right => "Right Click",
            MouseButton.Middle => "Middle Click",
            _ => "Left Click"
        };

        if (ClickCount >= 2) return $"{button} (x{ClickCount})";
        if (HoldMs > 0) return $"{button} (Hold)";
        return button;
    }
}