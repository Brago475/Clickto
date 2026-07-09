using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using Clickto.Models;
using Clickto.Services;

namespace Clickto.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private string _log = "Ready.";

    [ObservableProperty]
    private string _statusColor = "#3A3F4B";

    [ObservableProperty]
    private int _countdown;

    [ObservableProperty]
    private int _loadedCount;

    [ObservableProperty]
    private bool _isDark = true;

    [ObservableProperty]
    private bool _saveFlash;

    [ObservableProperty]
    private bool _loadFlash;

    [ObservableProperty]
    private bool _deleteFlash;

    // When true, loops flow into each other without the pause before the
    // first click of each repeat. Recorded timing within the sequence is kept.
    [ObservableProperty]
    private bool _removeDelays;

    public bool CanStart => !IsRunning && !IsRecording;

    partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(CanStart));
    partial void OnIsRecordingChanged(bool value) => OnPropertyChanged(nameof(CanStart));

    private CancellationTokenSource? _cts;
    private List<ClickStep> _steps = new();

    private readonly IRecorderService _recorder = PlatformServices.CreateRecorder();
    private readonly IHotkeyService _hotkey = PlatformServices.CreateHotkey();
    private readonly IMouseService _mouse = PlatformServices.CreateMouse();

    private string? _pendingDelete;
    private bool _stoppedByButton;

    // --- Loop options ---

    public ObservableCollection<string> LoopOptions { get; } = new()
        { "Forever", "1", "5", "10", "25", "50", "100", "Custom…" };

    [ObservableProperty]
    private string _selectedLoop = "10";

    [ObservableProperty]
    private bool _isCustomLoop;

    [ObservableProperty]
    private int _customLoops = 10;

    partial void OnSelectedLoopChanged(string value) => IsCustomLoop = value == "Custom…";

    private int ResolveLoopCount()
    {
        if (SelectedLoop == "Forever") return -1;
        if (SelectedLoop == "Custom…") return CustomLoops < 1 ? 1 : CustomLoops;
        return int.TryParse(SelectedLoop, out var n) ? n : 1;
    }

    // --- Speed options ---

    public ObservableCollection<string> SpeedOptions { get; } = new()
        { "0.5x", "1x", "2x", "5x", "Custom…" };

    [ObservableProperty]
    private string _selectedSpeed = "1x";

    [ObservableProperty]
    private bool _isCustomSpeed;

    [ObservableProperty]
    private double _customSpeed = 1.0;

    partial void OnSelectedSpeedChanged(string value) => IsCustomSpeed = value == "Custom…";

    private double ResolveSpeed()
    {
        if (SelectedSpeed == "Custom…") return CustomSpeed <= 0 ? 1.0 : CustomSpeed;
        var text = SelectedSpeed.TrimEnd('x');
        return double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var s) && s > 0
            ? s : 1.0;
    }

    // --- Hotkeys ---

    [ObservableProperty]
    private string _stopKeyName = "F8";

    [ObservableProperty]
    private string _pauseKeyName = "F9";

    private long _stopKeyCode = PlatformServices.DefaultStopKey();
    private long _pauseKeyCode = PlatformServices.DefaultPauseKey();
    private string? _capturingFor;

    // --- Presets ---

    [ObservableProperty]
    private string _presetName = "my_clicks";

    public ObservableCollection<string> Presets { get; } = new();

    [ObservableProperty]
    private string? _selectedPreset;

    public MainWindowViewModel()
    {
        _hotkey.StopPressed += () => Dispatcher.UIThread.Post(HandleStopKey);
        _hotkey.PausePressed += () => Dispatcher.UIThread.Post(HandlePauseKey);
        _hotkey.KeyCaptured += code => Dispatcher.UIThread.Post(() => HandleCapturedKey(code));

        _recorder.ClickCaptured += count => Dispatcher.UIThread.Post(() =>
        {
            if (count < 0)
            {
                Log = "Recording failed — grant Input Monitoring permission and restart.";
                IsRecording = false;
                UpdateStatus();
            }
            else
            {
                Log = $"Recording... {count} click(s) captured.";
                LoadedCount = count;
            }
        });

        _hotkey.StartListening(_stopKeyCode, _pauseKeyCode);
        RefreshPresets();
    }

    private void UpdateStatus()
    {
        if (IsRecording) StatusColor = "#EF4444";
        else if (IsPaused) StatusColor = "#F59E0B";
        else if (IsRunning) StatusColor = "#06B6D4";
        else StatusColor = "#3A3F4B";
    }

    private async void Flash(Action<bool> set)
    {
        set(true);
        await Task.Delay(1000);
        set(false);
    }

    [RelayCommand]
    private void ToggleTheme() => IsDark = !IsDark;

    // --- Hotkey handlers ---

    private void HandleStopKey()
    {
        if (IsRecording)
        {
            StopRecording();
            return;
        }

        if (IsRunning)
        {
            _cts?.Cancel();
            IsRunning = false;
            IsPaused = false;
            UpdateStatus();
            Log = $"Stopped by {StopKeyName}.";
        }
    }

    private void HandlePauseKey()
    {
        if (!IsRunning) return;
        IsPaused = !IsPaused;
        UpdateStatus();
        Log = IsPaused ? $"Paused. Press {PauseKeyName} to resume." : "Resumed.";
    }

    private void HandleCapturedKey(long code)
    {
        var name = KeyName(code);
        if (_capturingFor == "stop") { _stopKeyCode = code; StopKeyName = name; }
        else if (_capturingFor == "pause") { _pauseKeyCode = code; PauseKeyName = name; }
        _capturingFor = null;

        _hotkey.StartListening(_stopKeyCode, _pauseKeyCode);
        Log = $"Stop = {StopKeyName}, Pause = {PauseKeyName}.";
    }

    [RelayCommand]
    private void CaptureStopKey()
    {
        _capturingFor = "stop";
        _hotkey.BeginCapture();
        Log = "Press any key to set STOP...";
    }

    [RelayCommand]
    private void CapturePauseKey()
    {
        _capturingFor = "pause";
        _hotkey.BeginCapture();
        Log = "Press any key to set PAUSE/RESUME...";
    }

    // --- Playback ---

    [RelayCommand]
    private async Task Start()
    {
        if (IsRunning) return;

        if (_steps.Count == 0)
        {
            Log = "Nothing to play — record or load clicks first.";
            return;
        }

        IsRunning = true;
        IsPaused = false;
        UpdateStatus();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            for (int c = 3; c >= 1; c--)
            {
                Countdown = c;
                Log = $"Starting in {c}...";
                await Task.Delay(600, token);
            }
        }
        catch (TaskCanceledException)
        {
            Countdown = 0;
            IsRunning = false;
            UpdateStatus();
            Log = "Stopped.";
            return;
        }
        Countdown = 0;

        int loops = ResolveLoopCount();
        double speed = ResolveSpeed();
        string loopText = loops == -1 ? "forever" : $"{loops} loop(s)";
        string delayText = RemoveDelays ? "seamless loop" : "recorded timing";
        Log = $"Started. {_steps.Count} steps, {loopText}, {speed}x speed, {delayText}. Stop={StopKeyName}, Pause={PauseKeyName}.";

        try
        {
            int rep = 0;
            while (loops == -1 || rep < loops)
            {
                rep++;

                for (int i = 0; i < _steps.Count; i++)
                {
                    var step = _steps[i];

                    if (token.IsCancellationRequested) break;

                    while (IsPaused && !token.IsCancellationRequested)
                        await Task.Delay(100, token);

                    if (token.IsCancellationRequested) break;

                    // Normally wait this step's recorded delay. When "Seamless
                    // loop" is on, skip the delay before the first click of every
                    // repeat after the first — that's the pause between loops.
                    bool skipDelay = RemoveDelays && rep > 1 && i == 0;
                    if (!skipDelay)
                    {
                        int delay = (int)(step.DelayMs / speed);
                        await Task.Delay(delay, token);
                    }

                    _mouse.ClickAt(step.X, step.Y);

                    string label = loops == -1 ? $"loop {rep}" : $"loop {rep}/{loops}";
                    Log = $"{label}: clicked ({step.X}, {step.Y})";
                }

                if (token.IsCancellationRequested) break;
            }
        }
        catch (TaskCanceledException) { }

        IsRunning = false;
        IsPaused = false;
        UpdateStatus();
        if (!token.IsCancellationRequested)
            Log = "Finished.";
    }

    [RelayCommand]
    private void Stop()
    {
        _cts?.Cancel();
        Countdown = 0;
        IsRunning = false;
        IsPaused = false;
        UpdateStatus();
        Log = "Stopped.";
    }

    // --- Recording ---

    [RelayCommand]
    private void Record()
    {
        if (IsRecording || IsRunning) return;
        IsRecording = true;
        UpdateStatus();
        _recorder.Start();
        Log = $"Recording... click anywhere. Press {StopKeyName} or Stop Rec to finish.";
    }

    [RelayCommand]
    private void StopRecordingButton()
    {
        _stoppedByButton = true;
        StopRecording();
    }

    private void StopRecording()
    {
        if (!IsRecording) return;
        IsRecording = false;
        UpdateStatus();
        _steps = _recorder.Stop();

        if (_stoppedByButton && _steps.Count > 0)
            _steps.RemoveAt(_steps.Count - 1);
        _stoppedByButton = false;

        LoadedCount = _steps.Count;
        Log = $"Recorded {_steps.Count} clicks.";
    }

    // --- Presets ---

    [RelayCommand]
    private void SavePreset()
    {
        if (_steps.Count == 0) { Log = "Nothing to save — record some clicks first."; return; }
        if (string.IsNullOrWhiteSpace(PresetName)) { Log = "Enter a name for the preset."; return; }

        string name = PresetName;
        if (Presets.Contains(name))
        {
            int n = 1;
            while (Presets.Contains($"{PresetName}_{n}"))
                n++;
            name = $"{PresetName}_{n}";
        }

        PresetService.Save(name, _steps);
        RefreshPresets();
        Flash(v => SaveFlash = v);
        Log = $"Saved '{name}' ({_steps.Count} clicks).";
    }

    [RelayCommand]
    private void LoadPreset()
    {
        if (string.IsNullOrWhiteSpace(SelectedPreset)) { Log = "Pick a preset to load."; return; }

        _steps = PresetService.Load(SelectedPreset);
        LoadedCount = _steps.Count;
        Flash(v => LoadFlash = v);
        Log = $"Loaded '{SelectedPreset}' ({_steps.Count} clicks).";
    }

    [RelayCommand]
    private void DeletePreset()
    {
        if (string.IsNullOrWhiteSpace(SelectedPreset)) { Log = "Pick a preset to delete."; return; }

        if (_pendingDelete == SelectedPreset)
        {
            PresetService.Delete(SelectedPreset);
            Flash(v => DeleteFlash = v);
            Log = $"Deleted '{SelectedPreset}'.";
            _pendingDelete = null;
            SelectedPreset = null;
            RefreshPresets();
        }
        else
        {
            _pendingDelete = SelectedPreset;
            Log = $"Click Delete again to confirm removing '{SelectedPreset}'.";
        }
    }

    private void RefreshPresets()
    {
        Presets.Clear();
        foreach (var name in PresetService.ListPresets())
            Presets.Add(name);
    }

    // --- Helpers ---

    private static string KeyName(long code) => code switch
    {
        100 => "F8",
        101 => "F9",
        97 => "F6",
        53 => "Escape",
        49 => "Space",
        36 => "Return",
        _ => $"Key {code}"
    };
}