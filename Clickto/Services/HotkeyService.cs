using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Clickto.Services;

/// <summary>
/// Global keyboard listener. In normal mode it watches for two specific
/// keys (stop and pause) and fires the matching event. In capture mode it
/// reports the next key pressed, so the UI can let the user set a key.
/// </summary>
public class MacHotkeyService : IHotkeyService
{
    private const string CG = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
    private const string CF = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string LIBDL = "/usr/lib/libSystem.dylib";

    private const uint KeyDown = 10;
    private const ulong KeyDownMask = 1UL << (int)KeyDown;

    // Fired when the stop key is pressed.
    public event Action? StopPressed;
    // Fired when the pause/resume key is pressed.
    public event Action? PausePressed;
    // Fired in capture mode with the key code the user just pressed.
    public event Action<long>? KeyCaptured;

    private long _stopKeyCode = 100;    // F8 default
    private long _pauseKeyCode = 101;   // F9 default
    private bool _captureMode;

    private delegate nint CGEventTapCallBack(
        nint proxy, uint type, nint eventRef, nint userInfo);

    [DllImport(CG)]
    private static extern nint CGEventTapCreate(
        uint tap, uint place, uint options, ulong eventsOfInterest,
        CGEventTapCallBack callback, nint userInfo);

    [DllImport(CF)]
    private static extern nint CFMachPortCreateRunLoopSource(nint allocator, nint port, nint order);

    [DllImport(CF)]
    private static extern nint CFRunLoopGetCurrent();

    [DllImport(CF)]
    private static extern void CFRunLoopAddSource(nint rl, nint source, nint mode);

    [DllImport(CF)]
    private static extern void CFRunLoopRun();

    [DllImport(CF)]
    private static extern void CFRunLoopStop(nint rl);

    [DllImport(CG)]
    private static extern void CGEventTapEnable(nint tap, bool enable);

    [DllImport(CG)]
    private static extern long CGEventGetIntegerValueField(nint eventRef, uint field);

    [DllImport(LIBDL)]
    private static extern nint dlopen(string path, int mode);

    [DllImport(LIBDL)]
    private static extern nint dlsym(nint handle, string symbol);

    private Thread? _tapThread;
    private nint _runLoop;
    private CGEventTapCallBack? _callback;
    private bool _isListening;

    /// <summary>Start listening in normal mode for the two given keys.</summary>
    public void StartListening(long stopKey, long pauseKey)
    {
        _stopKeyCode = stopKey;
        _pauseKeyCode = pauseKey;
        _captureMode = false;
        EnsureTapRunning();
    }

    /// <summary>Switch to capture mode: the next key press is reported, not matched.</summary>
    public void BeginCapture()
    {
        _captureMode = true;
        EnsureTapRunning();
    }

    public void Stop()
    {
        _isListening = false;
        if (_runLoop != 0)
            CFRunLoopStop(_runLoop);
    }

    private void EnsureTapRunning()
    {
        if (_isListening) return;   // tap already up; mode flags handle behavior
        _isListening = true;
        _tapThread = new Thread(RunTap) { IsBackground = true };
        _tapThread.Start();
    }

    private void RunTap()
    {
        _callback = OnEvent;

        var tap = CGEventTapCreate(0, 0, 1, KeyDownMask, _callback, 0);
        if (tap == 0)
        {
            _isListening = false;
            return;
        }

        var source = CFMachPortCreateRunLoopSource(0, tap, 0);
        _runLoop = CFRunLoopGetCurrent();

        CFRunLoopAddSource(_runLoop, source, GetCommonModes());
        CGEventTapEnable(tap, true);
        CFRunLoopRun();
    }

    private nint OnEvent(nint proxy, uint type, nint eventRef, nint userInfo)
    {
        if (type == KeyDown && _isListening)
        {
            long code = CGEventGetIntegerValueField(eventRef, 9);

            if (_captureMode)
            {
                _captureMode = false;
                KeyCaptured?.Invoke(code);
            }
            else
            {
                if (code == _stopKeyCode)
                    StopPressed?.Invoke();
                else if (code == _pauseKeyCode)
                    PausePressed?.Invoke();
            }
        }

        return eventRef;
    }

    private nint GetCommonModes()
    {
        var handle = dlopen(CF, 2);
        var symbol = dlsym(handle, "kCFRunLoopCommonModes");
        return Marshal.ReadIntPtr(symbol);
    }
}