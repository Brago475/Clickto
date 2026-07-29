namespace Clickto.Models;

/// <summary>What a step actually does when played back.</summary>
public enum ActionType
{
    Click = 0,
    Move = 1,
    Scroll = 2,
    KeyPress = 3,
    Delay = 4
}

public enum MouseButton
{
    Left = 0,
    Right = 1,
    Middle = 2
}