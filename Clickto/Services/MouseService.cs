using System.Runtime.InteropServices;

namespace Clickto.Services;

/// <summary>
/// Controls the physical mouse on macOS via CoreGraphics.
/// Uses P/Invoke to call native functions in ApplicationServices.
/// </summary>
public static class MouseService
{
    // The CoreGraphics framework path on macOS.
    private const string CG = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";

    // Mouse event type codes CoreGraphics understands.
    private const uint LeftDown = 1;
    private const uint LeftUp = 2;

    // Mouse button codes.
    private const uint LeftButton = 0;

    // Build a mouse event object. source can be IntPtr.Zero (default source).
    [DllImport(CG)]
    private static extern nint CGEventCreateMouseEvent(
        nint source, uint mouseType, CGPoint mouseCursorPosition, uint mouseButton);

    // Fire the event into the system. tap=0 means the HID event stream.
    [DllImport(CG)]
    private static extern void CGEventPost(uint tap, nint eventRef);

    // CoreFoundation objects must be released or you leak memory.
    [DllImport(CG)]
    private static extern void CFRelease(nint cf);

    // A point in screen coordinates. Struct layout must match the native side.
    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint
    {
        public double X;
        public double Y;
        public CGPoint(double x, double y) { X = x; Y = y; }
    }

    /// <summary>
    /// Performs a left click at the given screen coordinates.
    /// A click is a press (down) followed by a release (up).
    /// </summary>
    public static void ClickAt(double x, double y)
    {
        var point = new CGPoint(x, y);

        var down = CGEventCreateMouseEvent(0, LeftDown, point, LeftButton);
        CGEventPost(0, down);
        CFRelease(down);

        var up = CGEventCreateMouseEvent(0, LeftUp, point, LeftButton);
        CGEventPost(0, up);
        CFRelease(up);
    }
}