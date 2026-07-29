using System.Runtime.InteropServices;
using System.Threading;
using Clickto.Models;

namespace Clickto.Services;

/// <summary>
/// Controls the physical mouse on macOS via CoreGraphics.
/// Uses P/Invoke to call native functions in ApplicationServices.
/// </summary>
public class MacMouseService : IMouseService
{
    // The CoreGraphics framework path on macOS.
    private const string CG = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";

    // Mouse event type codes CoreGraphics understands.
    private const uint LeftDown = 1;
    private const uint LeftUp = 2;
    private const uint MouseMoved = 5;
    private const uint RightDown = 3;
    private const uint RightUp = 4;
    private const uint OtherDown = 25;
    private const uint OtherUp = 26;

    // Mouse button codes.
    private const uint LeftButton = 0;
    private const uint RightButton = 1;
    private const uint CenterButton = 2;

    // Scroll wheel unit: 0 is pixel, 1 is line. Lines feel like real notches.
    private const uint ScrollLine = 1;

    // Tells the OS this is the Nth click in a row, which is what makes
    // double click register as a double click instead of two singles.
    private const uint ClickStateField = 1;

    [DllImport(CG)]
    private static extern nint CGEventCreateMouseEvent(
        nint source, uint mouseType, CGPoint mouseCursorPosition, uint mouseButton);

    [DllImport(CG)]
    private static extern nint CGEventCreateScrollWheelEvent(
        nint source, uint units, uint wheelCount, int wheel1);

    [DllImport(CG)]
    private static extern void CGEventPost(uint tap, nint eventRef);

    [DllImport(CG)]
    private static extern void CGEventSetIntegerValueField(nint eventRef, uint field, long value);

    [DllImport(CG)]
    private static extern void CGWarpMouseCursorPosition(CGPoint newCursorPosition);

    [DllImport(CG)]
    private static extern void CFRelease(nint cf);

    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint
    {
        public double X;
        public double Y;
        public CGPoint(double x, double y) { X = x; Y = y; }
    }

    /// <summary>Plain single left click. Preserved for existing callers.</summary>
    public void ClickAt(double x, double y)
        => ClickAt(x, y, MouseButton.Left, 1, 0);

    public void ClickAt(double x, double y, MouseButton button, int clickCount, int holdMs)
    {
        if (clickCount < 1) clickCount = 1;

        var point = new CGPoint(x, y);
        (uint downType, uint upType, uint buttonCode) = Resolve(button);

        for (int i = 1; i <= clickCount; i++)
        {
            var down = CGEventCreateMouseEvent(0, downType, point, buttonCode);
            CGEventSetIntegerValueField(down, ClickStateField, i);
            CGEventPost(0, down);
            CFRelease(down);

            if (holdMs > 0) Thread.Sleep(holdMs);

            var up = CGEventCreateMouseEvent(0, upType, point, buttonCode);
            CGEventSetIntegerValueField(up, ClickStateField, i);
            CGEventPost(0, up);
            CFRelease(up);

            // macOS needs consecutive clicks close together to treat them
            // as a double click, but not simultaneous.
            if (i < clickCount) Thread.Sleep(40);
        }
    }

    public void MoveTo(double x, double y)
    {
        var point = new CGPoint(x, y);

        // Warp sets the hardware cursor. The synthetic move event makes
        // apps that track mouse position actually notice.
        CGWarpMouseCursorPosition(point);

        var move = CGEventCreateMouseEvent(0, MouseMoved, point, LeftButton);
        CGEventPost(0, move);
        CFRelease(move);
    }

    public void Scroll(double x, double y, int notches)
    {
        if (notches == 0) return;

        MoveTo(x, y);

        var scroll = CGEventCreateScrollWheelEvent(0, ScrollLine, 1, notches);
        CGEventPost(0, scroll);
        CFRelease(scroll);
    }

    private static (uint down, uint up, uint code) Resolve(MouseButton button) => button switch
    {
        MouseButton.Right => (RightDown, RightUp, RightButton),
        MouseButton.Middle => (OtherDown, OtherUp, CenterButton),
        _ => (LeftDown, LeftUp, LeftButton)
    };
}