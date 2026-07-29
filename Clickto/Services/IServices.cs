using System;
using System.Collections.Generic;
using Clickto.Models;

namespace Clickto.Services;

// Drives the physical mouse.
public interface IMouseService
{
    // Kept so existing callers keep working. Plain single left click.
    void ClickAt(double x, double y);

    // Full click: button choice, repeat count, and how long to hold each press.
    void ClickAt(double x, double y, MouseButton button, int clickCount, int holdMs);

    // Moves the cursor without pressing anything.
    void MoveTo(double x, double y);

    // Scrolls the wheel. Positive is up, negative is down, measured in notches.
    void Scroll(double x, double y, int notches);
}

// Captures mouse input globally while recording.
public interface IRecorderService
{
    // Fires with the running action count (or -1 if the capture failed to start).
    event Action<int>? ClickCaptured;
    void Start();
    List<ClickStep> Stop();
}

// Listens for two global hotkeys and supports "press any key to set".
public interface IHotkeyService
{
    event Action? StopPressed;
    event Action? PausePressed;
    event Action<long>? KeyCaptured;

    void StartListening(long stopKey, long pauseKey);
    void BeginCapture();
    void Stop();
}