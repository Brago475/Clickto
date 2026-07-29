using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Clickto.Models;

namespace Clickto.Services;

// ============================================================
// Windows implementations. Written on Mac (compiles fine),
// only RUN on Windows. Tested in the Windows VM.
// ============================================================

// --- Mouse: move cursor, click any button, hold, scroll ---
public class WinMouseService : IMouseService
{
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, nint dwExtraInfo);

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;

    // One wheel notch in Windows units.
    private const int WHEEL_DELTA = 120;

    public void ClickAt(double x, double y)
        => ClickAt(x, y, MouseButton.Left, 1, 0);

    public void ClickAt(double x, double y, MouseButton button, int clickCount, int holdMs)
    {
        if (clickCount < 1) clickCount = 1;

        SetCursorPos((int)x, (int)y);
        (uint down, uint up) = Resolve(button);

        for (int i = 1; i <= clickCount; i++)
        {
            mouse_event(down, 0, 0, 0, 0);
            if (holdMs > 0) Thread.Sleep(holdMs);
            mouse_event(up, 0, 0, 0, 0);

            // Consecutive clicks need a small gap to register as a double click.
            if (i < clickCount) Thread.Sleep(40);
        }
    }

    public void MoveTo(double x, double y)
        => SetCursorPos((int)x, (int)y);

    public void Scroll(double x, double y, int notches)
    {
        if (notches == 0) return;

        SetCursorPos((int)x, (int)y);
        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, unchecked((uint)(notches * WHEEL_DELTA)), 0);
    }

    private static (uint down, uint up) Resolve(MouseButton button) => button switch
    {
        MouseButton.Right => (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP),
        MouseButton.Middle => (MOUSEEVENTF_MIDDLEDOWN, MOUSEEVENTF_MIDDLEUP),
        _ => (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP)
    };
}

// --- Recorder: global low-level mouse hook captures each click ---
public class WinRecorderService : IRecorderService
{
    public event Action<int>? ClickCaptured;

    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_MBUTTONUP = 0x0208;
    private const int WM_MOUSEWHEEL = 0x020A;

    // One wheel notch, as reported in the high word of mouseData.
    private const int WHEEL_DELTA = 120;

    // Presses shorter than this are ordinary clicks, not deliberate holds.
    private const int HoldThresholdMs = 250;

    private nint _hook = nint.Zero;
    private LowLevelMouseProc? _proc;
    private readonly List<ClickStep> _steps = new();
    private DateTime _lastClick;

    // Tracks the press currently held down so the matching release can be
    // turned into a hold duration on the step already recorded.
    private DateTime _pressedAt;
    private int _pendingIndex = -1;

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
        _pendingIndex = -1;
        _lastClick = DateTime.Now;
        _proc = HookCallback;
        _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);

        if (_hook == nint.Zero)
            ClickCaptured?.Invoke(-1);
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

            switch ((int)wParam)
            {
                case WM_LBUTTONDOWN:
                    RecordPress(data, MouseButton.Left);
                    break;

                case WM_RBUTTONDOWN:
                    RecordPress(data, MouseButton.Right);
                    break;

                case WM_MBUTTONDOWN:
                    RecordPress(data, MouseButton.Middle);
                    break;

                case WM_LBUTTONUP:
                case WM_RBUTTONUP:
                case WM_MBUTTONUP:
                    RecordRelease();
                    break;

                case WM_MOUSEWHEEL:
                    RecordScroll(data);
                    break;
            }
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private void RecordPress(MSLLHOOKSTRUCT data, MouseButton button)
    {
        var now = DateTime.Now;
        int delay = _steps.Count == 0 ? 0 : (int)(now - _lastClick).TotalMilliseconds;
        _lastClick = now;

        _steps.Add(new ClickStep
        {
            X = data.pt.x,
            Y = data.pt.y,
            DelayMs = delay,
            Type = ActionType.Click,
            Button = button,
            ClickCount = 1
        });

        _pendingIndex = _steps.Count - 1;
        _pressedAt = now;

        ClickCaptured?.Invoke(_steps.Count);
    }

    private void RecordRelease()
    {
        if (_pendingIndex < 0 || _pendingIndex >= _steps.Count) return;

        int held = (int)(DateTime.Now - _pressedAt).TotalMilliseconds;
        if (held >= HoldThresholdMs)
            _steps[_pendingIndex].HoldMs = held;

        // Time spent holding should not also count as the gap before the
        // next action, so the delay clock restarts at the release.
        _lastClick = DateTime.Now;
        _pendingIndex = -1;
    }

    private void RecordScroll(MSLLHOOKSTRUCT data)
    {
        // The wheel delta lives in the high word of mouseData, signed.
        short raw = (short)((data.mouseData >> 16) & 0xFFFF);
        int notches = raw / WHEEL_DELTA;
        if (notches == 0) return;

        var now = DateTime.Now;
        int delay = _steps.Count == 0 ? 0 : (int)(now - _lastClick).TotalMilliseconds;
        _lastClick = now;

        _steps.Add(new ClickStep
        {
            X = data.pt.x,
            Y = data.pt.y,
            DelayMs = delay,
            Type = ActionType.Scroll,
            ScrollAmount = notches
        });

        ClickCaptured?.Invoke(_steps.Count);
    }

    public List<ClickStep> Stop()
    {
        if (_hook != nint.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = nint.Zero;
        }
        _pendingIndex = -1;
        return new List<ClickStep>(_steps);
    }
}

// --- Hotkeys: global low-level keyboard hook ---
public class WinHotkeyService : IHotkeyService
{
    public event Action<HotkeyAction>? ActionTriggered;
    public event Action<long>? KeyCaptured;

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;

    private nint _hook = nint.Zero;
    private LowLevelKeyboardProc? _proc;
    private Dictionary<HotkeyAction, long> _bindings = new();
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

    public void StartListening(IReadOnlyDictionary<HotkeyAction, long> bindings)
    {
        _bindings = new Dictionary<HotkeyAction, long>(bindings);
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
            else
            {
                foreach (var pair in _bindings)
                {
                    if (pair.Value >= 0 && pair.Value == vk)
                    {
                        ActionTriggered?.Invoke(pair.Key);
                        break;
                    }
                }
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