using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Clickto.Models;

namespace Clickto.Services;

/// <summary>
/// Records mouse clicks anywhere on screen using a global CoreGraphics event tap.
/// Requires Input Monitoring permission on macOS.
/// </summary>
public class MacRecorderService : IRecorderService
{
    private const string CG = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
    private const string CF = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string LIBDL = "/usr/lib/libSystem.dylib";

    private const uint LeftMouseDown = 1;
    private const ulong LeftMouseDownMask = 1UL << (int)LeftMouseDown;

    // Fires when a captured click happens, so the UI can update live.
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

    public void Start()
    {
        if (_isRecording) return;
        _isRecording = true;
        _recorded.Clear();
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
        return new List<ClickStep>(_recorded);
    }

    private void RunTap()
    {
        _callback = OnEvent;

        var tap = CGEventTapCreate(0, 0, 1, LeftMouseDownMask, _callback, 0);
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
        if (type == LeftMouseDown && _isRecording)
        {
            var loc = CGEventGetLocation(eventRef);
            int delay = (int)_timer.ElapsedMilliseconds;
            _timer.Restart();
            _recorded.Add(new ClickStep(loc.X, loc.Y, delay));
            ClickCaptured?.Invoke(_recorded.Count);   // tell the UI live count
        }

        return eventRef;
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