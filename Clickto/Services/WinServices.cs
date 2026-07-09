using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Clickto.Models;

namespace Clickto.Services;

// ============================================================
// Windows implementations. Written on Mac (compiles fine),
// only RUN on Windows. Tested in the Windows VM.
// ============================================================

// --- Mouse: move cursor + left click ---
public class WinMouseService : IMouseService
{
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, nint dwExtraInfo);

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;

    public void ClickAt(double x, double y)
    {
        SetCursorPos((int)x, (int)y);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
    }
}

// --- Recorder: global low-level mouse hook captures each click ---
public class WinRecorderService : IRecorderService
{
    public event Action<int>? ClickCaptured;

    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;

    private nint _hook = nint.Zero;
    private LowLevelMouseProc? _proc;
    private readonly List<ClickStep> _steps = new();
    private DateTime _lastClick;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public nint dwExtraInfo;
    }

    private delegate nint LowLevelMouseProc(int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll")]
    private static extern nint GetModuleHandle(string? lpModuleName);

    public void Start()
    {
        _steps.Clear();
        _lastClick = DateTime.Now;
        _proc = HookCallback;
        _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);

        if (_hook == nint.Zero)
            ClickCaptured?.Invoke(-1);
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && (int)wParam == WM_LBUTTONDOWN)
        {
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            var now = DateTime.Now;
            int delay = _steps.Count == 0 ? 0 : (int)(now - _lastClick).TotalMilliseconds;
            _lastClick = now;

            _steps.Add(new ClickStep { X = data.pt.x, Y = data.pt.y, DelayMs = delay });
            ClickCaptured?.Invoke(_steps.Count);
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public List<ClickStep> Stop()
    {
        if (_hook != nint.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = nint.Zero;
        }
        return new List<ClickStep>(_steps);
    }
}

// --- Hotkeys: global low-level keyboard hook ---
public class WinHotkeyService : IHotkeyService
{
    public event Action? StopPressed;
    public event Action? PausePressed;
    public event Action<long>? KeyCaptured;

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;

    private nint _hook = nint.Zero;
    private LowLevelKeyboardProc? _proc;
    private long _stopKey = 119;   // F8 on Windows
    private long _pauseKey = 120;  // F9 on Windows
    private bool _capturing;

    private delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll")]
    private static extern nint GetModuleHandle(string? lpModuleName);

    public void StartListening(long stopKey, long pauseKey)
    {
        _stopKey = stopKey;
        _pauseKey = pauseKey;
        _capturing = false;
        EnsureHook();
    }

    public void BeginCapture()
    {
        _capturing = true;
        EnsureHook();
    }

    private void EnsureHook()
    {
        if (_hook != nint.Zero) return;
        _proc = HookCallback;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && (int)wParam == WM_KEYDOWN)
        {
            int vk = Marshal.ReadInt32(lParam);

            if (_capturing)
            {
                _capturing = false;
                KeyCaptured?.Invoke(vk);
            }
            else if (vk == _stopKey)
            {
                StopPressed?.Invoke();
            }
            else if (vk == _pauseKey)
            {
                PausePressed?.Invoke();
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Stop()
    {
        if (_hook != nint.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = nint.Zero;
        }
    }
}