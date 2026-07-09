using System;
using System.Runtime.InteropServices;

namespace Clickto.Services;

// Creates the correct platform-specific service implementations.
// On macOS -> the Mac* classes. On Windows -> the Win* classes.
public static class PlatformServices
{
    public static IMouseService CreateMouse()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new MacMouseService();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WinMouseService();
        throw new PlatformNotSupportedException("Unsupported OS.");
    }

    public static IRecorderService CreateRecorder()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new MacRecorderService();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WinRecorderService();
        throw new PlatformNotSupportedException("Unsupported OS.");
    }

    public static IHotkeyService CreateHotkey()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new MacHotkeyService();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WinHotkeyService();
        throw new PlatformNotSupportedException("Unsupported OS.");
    }

    // Default hotkey codes differ by platform (F8/F9 have different keycodes).
    public static long DefaultStopKey()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 119 : 100;

    public static long DefaultPauseKey()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 120 : 101;
}