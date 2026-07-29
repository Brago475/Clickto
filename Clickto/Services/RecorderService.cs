using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Clickto.Models;

namespace Clickto.Services;

/// <summary>
/// Records mouse input anywhere on screen using a global CoreGraphics event tap.
/// Captures left, right and middle clicks, scroll wheel, and hold duration.
/// Requires Input Monitoring permission on macOS.
/// </summary>
public class MacRecorderService : IRecorderService
{
    private const string CG = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
    private const string CF = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string LIBDL = "/usr/lib/libSystem.dylib";

    // CGEventType values we care about.
    private const uint LeftMouseDown = 1;
    private const uint LeftMouseUp = 2;
    private const uint RightMouseDown = 3;
    private const uint RightMouseUp = 4;
    private const uint ScrollWheel = 22;
    private const uint OtherMouseDown = 25;
    private const uint OtherMouseUp = 26;

    // Bitmask of every event type the tap should receive.
    private const ulong EventMask =
        (1UL << (int)LeftMouseDown) |
        (1UL << (int)LeftMouseUp) |
        (1UL << (int)RightMouseDown) |
        (1UL << (int)RightMouseUp) |
        (1UL << (int)ScrollWheel) |
        (1UL << (int)OtherMouseDown) |
        (1UL << (int)OtherMouseUp);

    // CGEventField codes.
    private const uint ScrollWheelEventDeltaAxis1 = 11;
    private const uint MouseEventClickState = 1;

    // Fires with the running action count so the UI can update live.
    public event Action<int>? ClickCaptured;

    private delegate nint CGEventTapCallBack(
        nint proxy, uint type, nint eventRef, nint userInfo);

    [DllImport(CG)]
    private static extern nint CGEventTapCreate(
        uint tap, uint place, uint options, ulong eventsOfInterest,
        CGEventTapCallBack callback, nint userInfo);

    [DllImport(CF)]
    private static extern nint CFMachPortCreateRunLoopSource(
        nint allocator, nint port, nint order);

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
    private static extern CGPoint CGEventGetLocation(nint eventRef);

    [DllImport(CG)]
    private static extern long CGEventGetIntegerValueField(nint eventRef, uint field);

    // Used to read the kCFRunLoopCommonModes data symbol from CoreFoundation.
    [DllImport(LIBDL)]
    private static extern nint dlopen(string path, int mode);

    [DllImport(LIBDL)]
    private static extern nint dlsym(nint handle, string symbol);

    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint
    {
        public double X;
        public double Y;
    }

    private readonly List<ClickStep> _recorded = new();
    private readonly Stopwatch _timer = new();
    private Thread? _tapThread;
    private nint _runLoop;
    private CGEventTapCallBack? _callback;
    private bool _isRecording;

    // Tracks the press that is currently held down, so the matching release
    // can be turned into a hold duration on the step we already recorded.
    private readonly Stopwatch _holdTimer = new();
    private int _pendingIndex = -1;

    // Anything shorter than this is a normal click, not a deliberate hold.
    private const int HoldThresholdMs = 250;

    public void Start()
    {
        if (_isRecording) return;
        _isRecording = true;
        _recorded.Clear();
        _pendingIndex = -1;
        _timer.Restart();

        _tapThread = new Thread(RunTap) { IsBackground = true };
        _tapThread.Start();
    }

    public List<ClickStep> Stop()
    {
        _isRecording = false;
        if (_runLoop != 0)
            CFRunLoopStop(_runLoop);
        _timer.Stop();
        _holdTimer.Reset();
        _pendingIndex = -1;
        return new List<ClickStep>(_recorded);
    }

    private void RunTap()
    {
        _callback = OnEvent;

        var tap = CGEventTapCreate(0, 0, 1, EventMask, _callback, 0);
        if (tap == 0)
        {
            // Missing Input Monitoring permission, or tap creation failed.
            _isRecording = false;
            ClickCaptured?.Invoke(-1);   // signal failure to the UI
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
        if (!_isRecording) return eventRef;

        switch (type)
        {
            case LeftMouseDown:
                RecordPress(eventRef, MouseButton.Left);
                break;

            case RightMouseDown:
                RecordPress(eventRef, MouseButton.Right);
                break;

            case OtherMouseDown:
                RecordPress(eventRef, MouseButton.Middle);
                break;

            case LeftMouseUp:
            case RightMouseUp:
            case OtherMouseUp:
                RecordRelease();
                break;

            case ScrollWheel:
                RecordScroll(eventRef);
                break;
        }

        return eventRef;
    }

    private void RecordPress(nint eventRef, MouseButton button)
    {
        var loc = CGEventGetLocation(eventRef);
        int delay = (int)_timer.ElapsedMilliseconds;
        _timer.Restart();

        // macOS reports 2 for the second click of a double click, 3 for a
        // triple, and so on. Recording that keeps double clicks intact.
        int clickState = (int)CGEventGetIntegerValueField(eventRef, MouseEventClickState);
        if (clickState < 1) clickState = 1;

        // A double click arrives as a second press right after the first.
        // Fold it into the existing step instead of adding a duplicate.
        if (clickState > 1 && _recorded.Count > 0)
        {
            var previous = _recorded[^1];
            if (previous.Type == ActionType.Click && previous.Button == button)
            {
                previous.ClickCount = clickState;
                _pendingIndex = _recorded.Count - 1;
                _holdTimer.Restart();
                ClickCaptured?.Invoke(_recorded.Count);
                return;
            }
        }

        _recorded.Add(new ClickStep
        {
            X = loc.X,
            Y = loc.Y,
            DelayMs = delay,
            Type = ActionType.Click,
            Button = button,
            ClickCount = 1
        });

        _pendingIndex = _recorded.Count - 1;
        _holdTimer.Restart();

        ClickCaptured?.Invoke(_recorded.Count);
    }

    private void RecordRelease()
    {
        if (_pendingIndex < 0 || _pendingIndex >= _recorded.Count) return;

        int held = (int)_holdTimer.ElapsedMilliseconds;
        _holdTimer.Reset();

        // Only treat a long press as a hold. Short presses are plain clicks.
        if (held >= HoldThresholdMs)
            _recorded[_pendingIndex].HoldMs = held;

        // The time spent holding should not also count as the delay before
        // the next action, so restart the gap timer at the release.
        _timer.Restart();
        _pendingIndex = -1;
    }

    private void RecordScroll(nint eventRef)
    {
        var loc = CGEventGetLocation(eventRef);
        int notches = (int)CGEventGetIntegerValueField(eventRef, ScrollWheelEventDeltaAxis1);
        if (notches == 0) return;

        int delay = (int)_timer.ElapsedMilliseconds;
        _timer.Restart();

        _recorded.Add(new ClickStep
        {
            X = loc.X,
            Y = loc.Y,
            DelayMs = delay,
            Type = ActionType.Scroll,
            ScrollAmount = notches
        });

        ClickCaptured?.Invoke(_recorded.Count);
    }

    // Reads the real kCFRunLoopCommonModes constant from CoreFoundation.
    private nint GetCommonModes()
    {
        var handle = dlopen(CF, 2 /* RTLD_NOW */);
        var symbol = dlsym(handle, "kCFRunLoopCommonModes");
        // symbol points to a CFStringRef*; dereference one pointer.
        return Marshal.ReadIntPtr(symbol);
    }
}