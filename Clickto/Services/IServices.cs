using System;
using System.Collections.Generic;
using Clickto.Models;

namespace Clickto.Services;

// Clicks the physical mouse at a screen coordinate.
public interface IMouseService
{
    void ClickAt(double x, double y);
}

// Captures mouse clicks globally while recording.
public interface IRecorderService
{
    // Fires with the running click count (or -1 if the capture failed to start).
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