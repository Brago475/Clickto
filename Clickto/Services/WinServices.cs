using System;
using System.Collections.Generic;
using Clickto.Models;

namespace Clickto.Services;

// Windows implementations — STUBS for now. Real Win32 code added in Phase 2,
// written on Mac and tested in the Windows VM.

public class WinMouseService : IMouseService
{
    public void ClickAt(double x, double y)
        => throw new NotImplementedException("Windows mouse not implemented yet.");
}

public class WinRecorderService : IRecorderService
{
    public event Action<int>? ClickCaptured;
    public void Start()
        => throw new NotImplementedException("Windows recorder not implemented yet.");
    public List<ClickStep> Stop() => new();
}

public class WinHotkeyService : IHotkeyService
{
    public event Action? StopPressed;
    public event Action? PausePressed;
    public event Action<long>? KeyCaptured;
    public void StartListening(long stopKey, long pauseKey)
        => throw new NotImplementedException("Windows hotkeys not implemented yet.");
    public void BeginCapture()
        => throw new NotImplementedException("Windows hotkeys not implemented yet.");
    public void Stop() { }
}