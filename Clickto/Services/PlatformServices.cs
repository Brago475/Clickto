using System;
using System.Runtime.InteropServices;

namespace Clickto.Services;

// Creates the correct platform-specific service implementations.
// On macOS -> the Mac* classes. On Windows -> the Win* classes (added later).
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
}