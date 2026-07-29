using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Clickto.Models;
using Clickto.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clickto.ViewModels;

/// <summary>
/// One editable row in the timeline. Wraps a ClickStep so the UI can bind to
/// and mutate individual values without rebuilding the whole list.
/// </summary>
public partial class StepRow : ObservableObject
{
    [ObservableProperty]
    private int _index;

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private int _delayMs;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private ActionType _type = ActionType.Click;

    [ObservableProperty]
    private MouseButton _button = MouseButton.Left;

    [ObservableProperty]
    private int _clickCount = 1;

    [ObservableProperty]
    private int _holdMs;

    [ObservableProperty]
    private int _scrollAmount;

    [ObservableProperty]
    private long _keyCode;

    public string Position => $"({X:0}, {Y:0})";
    public string DelayText => $"{DelayMs} ms";
    public string Number => $"{Index + 1}";
    public string HoldText => HoldMs > 0 ? $"{HoldMs} ms" : "-";

    /// <summary>Mouse button name, or a dash where a button makes no sense.</summary>
    public string ButtonText => Type == ActionType.Click
        ? Button.ToString()
        : "-";

    /// <summary>Inverse of IsMuted, for the Enabled checkbox in the timeline.</summary>
    public bool IsEnabled
    {
        get => !IsMuted;
        set => IsMuted = !value;
    }

    /// <summary>Coordinates are meaningless for delay steps.</summary>
    public string PositionText =>
        Type == ActionType.Delay ? "-" : Position;

    /// <summary>Short label for the timeline, for example "Right Click (Hold)".</summary>
    public string TypeLabel => Type switch
    {
        ActionType.Move => "Move",
        ActionType.Scroll => ScrollAmount >= 0 ? "Scroll Up" : "Scroll Down",
        ActionType.KeyPress => "Key Press",
        ActionType.Delay => "Delay",
        _ => BuildClickLabel()
    };

    private string BuildClickLabel()
    {
        string name = Button switch
        {
            MouseButton.Right => "Right Click",
            MouseButton.Middle => "Middle Click",
            _ => "Left Click"
        };

        if (ClickCount >= 2) return $"{name} (x{ClickCount})";
        if (HoldMs > 0) return $"{name} (Hold)";
        return name;
    }

    partial void OnXChanged(double value) => OnPropertyChanged(nameof(Position));
    partial void OnYChanged(double value) => OnPropertyChanged(nameof(Position));
    partial void OnDelayMsChanged(int value) => OnPropertyChanged(nameof(DelayText));
    partial void OnIndexChanged(int value) => OnPropertyChanged(nameof(Number));
    partial void OnTypeChanged(ActionType value)
    {
        OnPropertyChanged(nameof(TypeLabel));
        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(ButtonText));
    }

    partial void OnIsMutedChanged(bool value) => OnPropertyChanged(nameof(IsEnabled));
    partial void OnButtonChanged(MouseButton value)
    {
        OnPropertyChanged(nameof(TypeLabel));
        OnPropertyChanged(nameof(ButtonText));
    }
    partial void OnClickCountChanged(int value) => OnPropertyChanged(nameof(TypeLabel));
    partial void OnScrollAmountChanged(int value) => OnPropertyChanged(nameof(TypeLabel));

    partial void OnHoldMsChanged(int value)
    {
        OnPropertyChanged(nameof(TypeLabel));
        OnPropertyChanged(nameof(HoldText));
    }

    public StepRow() { }

    public StepRow(ClickStep step)
    {
        _x = step.X;
        _y = step.Y;
        _delayMs = step.DelayMs;
        _isMuted = step.IsMuted;
        _type = step.Type;
        _button = step.Button;
        _clickCount = step.ClickCount < 1 ? 1 : step.ClickCount;
        _holdMs = step.HoldMs;
        _scrollAmount = step.ScrollAmount;
        _keyCode = step.KeyCode;
    }

    public ClickStep ToStep() => new ClickStep
    {
        X = X,
        Y = Y,
        DelayMs = DelayMs,
        IsMuted = IsMuted,
        Type = Type,
        Button = Button,
        ClickCount = ClickCount,
        HoldMs = HoldMs,
        ScrollAmount = ScrollAmount,
        KeyCode = KeyCode
    };

    public StepRow Clone() => new StepRow
    {
        X = X,
        Y = Y,
        DelayMs = DelayMs,
        IsMuted = IsMuted,
        Type = Type,
        Button = Button,
        ClickCount = ClickCount,
        HoldMs = HoldMs,
        ScrollAmount = ScrollAmount,
        KeyCode = KeyCode
    };
}

/// <summary>One row in the preset list, with its favorite star and summary.</summary>
public partial class PresetEntry : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private string _description = "";

    public string Star => IsFavorite ? "★" : "☆";

    partial void OnIsFavoriteChanged(bool value) => OnPropertyChanged(nameof(Star));
}

public partial class MainWindowViewModel : ViewModelBase
{
    // Guardrails. Below roughly 5 ms the OS input queue starts dropping
    // events, so anything faster looks quicker but clicks less reliably.
    private const double MinSpeed = 0.1;
    private const double MaxSpeed = 20.0;
    private const int MinDelayMs = 5;
    private const int MaxDelayMs = 600000;

    // --- State ---

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

    /// <summary>Simple mode hides the timeline editor, hotkey binding and presets.</summary>
    [ObservableProperty]
    private bool _isAdvanced;

    public string ModeLabel => IsAdvanced ? "Advanced" : "Simple";
    public string ModeToggleLabel => IsAdvanced ? "Simple mode" : "Advanced mode";

    partial void OnIsAdvancedChanged(bool value)
    {
        OnPropertyChanged(nameof(ModeLabel));
        OnPropertyChanged(nameof(ModeToggleLabel));
        RaiseLayoutFlags();
        PersistSettings();

        if (_loadingSettings) return;

        // Each mode keeps its own remembered size, so switching back and
        // forth does not throw away a window the user resized by hand.
        if (value)
        {
            WindowWidth = _settings.AdvancedWidth;
            WindowHeight = _settings.AdvancedHeight;
        }
        else
        {
            WindowWidth = _settings.SimpleWidth;
            WindowHeight = _settings.SimpleHeight;
        }
    }

    /// <summary>
    /// When true, loops flow into each other without the pause before the
    /// first click of each repeat. Timing inside the sequence is kept.
    /// </summary>
    [ObservableProperty]
    private bool _removeDelays;

/// <summary>
    /// Adds small random variation to delays, click positions and press
    /// duration so playback is not bit-identical every repetition.
    /// </summary>
    [ObservableProperty]
    private bool _naturalClicks;

    /// <summary>Delay variation, plus or minus this percent.</summary>
    [ObservableProperty]
    private int _delayJitterPercent = 15;

    /// <summary>Click position variation, plus or minus this many pixels.</summary>
    [ObservableProperty]
    private int _positionJitterPx = 3;

    // --- Stop conditions ---

    /// <summary>Stop once this many actions have been performed.</summary>
    [ObservableProperty]
    private bool _stopAfterActionsEnabled;

    [ObservableProperty]
    private int _stopAfterActions = 1000;

    /// <summary>Stop once the run has lasted this long.</summary>
    [ObservableProperty]
    private bool _stopAfterTimeEnabled;

    [ObservableProperty]
    private int _stopAfterMinutes = 10;

    /// <summary>Running total for the current playback, shown while running.</summary>
    [ObservableProperty]
    private int _actionsPerformed;

    // --- Timing ---

    /// <summary>Wait before the first action of a run.</summary>
    [ObservableProperty]
    private int _startDelayMs;

    /// <summary>Extra wait inserted between repeats.</summary>
    [ObservableProperty]
    private int _loopPauseMs;

    /// <summary>Inline helper text under settings. Off leaves tooltips only.</summary>
    [ObservableProperty]
    private bool _showHints = true;

    // --- Layout ---

    public ObservableCollection<string> LayoutOptions { get; } = new()
        { "Three column", "Two column", "Stacked" };

    [ObservableProperty]
    private string _selectedLayout = "Three column";

    // Simple mode is always a single stack. The layout picker only applies
    // once the user is in Advanced.
    public bool IsThreeColumn => IsAdvanced && SelectedLayout == "Three column";
    public bool IsTwoColumn => IsAdvanced && SelectedLayout == "Two column";
    public bool IsStacked => !IsAdvanced || SelectedLayout == "Stacked";

    partial void OnSelectedLayoutChanged(string value)
    {
        RaiseLayoutFlags();
        PersistSettings();
    }

    private void RaiseLayoutFlags()
    {
        OnPropertyChanged(nameof(IsThreeColumn));
        OnPropertyChanged(nameof(IsTwoColumn));
        OnPropertyChanged(nameof(IsStacked));
    }

    // Panels the user can individually hide.
    [ObservableProperty] private bool _showControlsPanel = true;
    [ObservableProperty] private bool _showPlaybackPanel = true;
    [ObservableProperty] private bool _showTimelinePanel = true;
    [ObservableProperty] private bool _showPropertiesPanel = true;
    [ObservableProperty] private bool _showHotkeysPanel = true;
    [ObservableProperty] private bool _showPresetsPanel = true;

    [RelayCommand]
    private void ResetPanels()
    {
        ShowControlsPanel = true;
        ShowPlaybackPanel = true;
        ShowTimelinePanel = true;
        ShowPropertiesPanel = true;
        ShowHotkeysPanel = true;
        ShowPresetsPanel = true;
        SelectedLayout = "Three column";
        ShowHints = true;
        Log = "Layout restored to defaults.";
    }

    // --- Step editor ---

    public ObservableCollection<ActionType> ActionTypeOptions { get; } = new()
        { ActionType.Click, ActionType.Move, ActionType.Scroll, ActionType.Delay };

    public ObservableCollection<MouseButton> MouseButtonOptions { get; } = new()
        { MouseButton.Left, MouseButton.Right, MouseButton.Middle };

    public bool CanStart => !IsRunning && !IsRecording && Timeline.Count > 0;
    public bool CanEdit => !IsRunning && !IsRecording;
    public bool HasSteps => Timeline.Count > 0;
    public bool HasSelection => SelectedStep != null;

    public string PauseButtonLabel => IsPaused ? "► Resume" : "II Pause";

    /// <summary>One word for the current state, for the status blocks.</summary>
    public string StatusText
    {
        get
        {
            if (IsRecording) return "Recording";
            if (IsPaused) return "Paused";
            if (IsRunning) return "Playing";
            return "Idle";
        }
    }

    [ObservableProperty]
    private string _elapsedText = "00:00";

    [ObservableProperty]
    private int _currentLoop;

    [ObservableProperty]
    private int _totalLoops;

    /// <summary>"loop 3 / 10" during a run, blank otherwise.</summary>
    public string LoopProgress
    {
        get
        {
            if (!IsRunning) return "";
            return TotalLoops < 0 ? $"loop {CurrentLoop}" : $"loop {CurrentLoop} / {TotalLoops}";
        }
    }

    private void RaiseRunState()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(LoopProgress));
    }

    partial void OnCurrentLoopChanged(int value) => OnPropertyChanged(nameof(LoopProgress));
    partial void OnTotalLoopsChanged(int value) => OnPropertyChanged(nameof(LoopProgress));

    partial void OnIsPausedChanged(bool value)
    {
        OnPropertyChanged(nameof(PauseButtonLabel));
        RaiseRunState();
    }

    partial void OnIsRunningChanged(bool value)
    {
        RaiseGateFlags();
        RaiseRunState();
    }

    partial void OnIsRecordingChanged(bool value)
    {
        RaiseGateFlags();
        RaiseRunState();
    }

    private void RaiseGateFlags()
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(HasSteps));
    }

    private CancellationTokenSource? _cts;

    private readonly IRecorderService _recorder = PlatformServices.CreateRecorder();
    private readonly IHotkeyService _hotkey = PlatformServices.CreateHotkey();
    private readonly IMouseService _mouse = PlatformServices.CreateMouse();

    private string? _pendingDelete;
    private bool _stoppedByButton;

    private readonly AppSettings _settings = SettingsService.Load();

    // Property setters fire while we are applying saved values. Without this
    // guard the first launch would write defaults back over the real file.
    private bool _loadingSettings;

    // --- Timeline ---

    public ObservableCollection<StepRow> Timeline { get; } = new();

    [ObservableProperty]
    private StepRow? _selectedStep;

    [ObservableProperty]
    private int _playheadIndex = -1;

    partial void OnSelectedStepChanged(StepRow? value) => OnPropertyChanged(nameof(HasSelection));

    /// <summary>Bulk value used by "set all delays" and the nudge buttons.</summary>
    [ObservableProperty]
    private int _bulkDelayMs = 100;

    public string TimelineSummary
    {
        get
        {
            int active = Timeline.Count(s => !s.IsMuted);
            if (Timeline.Count == 0) return "No steps loaded.";
            string muted = Timeline.Count - active > 0 ? $", {Timeline.Count - active} muted" : "";
            return $"{Timeline.Count} steps{muted}, {FormatDuration(PassDurationMs())} per pass at {ResolveSpeed():0.##}x";
        }
    }

    /// <summary>"Total duration: 1.42 sec" for the timeline footer.</summary>
    public string TotalDurationText => $"Total duration: {FormatDuration(PassDurationMs())}";

    private double PassDurationMs()
    {
        double speed = ResolveSpeed();
        double total = 0;
        foreach (var row in Timeline)
        {
            if (row.IsMuted) continue;
            total += Math.Max(MinDelayMs, row.DelayMs / speed);
            total += row.HoldMs;
        }
        return total;
    }

    private static string FormatDuration(double ms)
    {
        if (ms < 1000) return $"{ms:0} ms";
        var span = TimeSpan.FromMilliseconds(ms);
        if (span.TotalMinutes < 1) return $"{span.TotalSeconds:0.0} s";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m {span.Seconds}s";
        return $"{(int)span.TotalHours}h {span.Minutes}m";
    }

    private void RefreshTimelineMeta()
    {
        _clearArmed = false;

        for (int i = 0; i < Timeline.Count; i++)
            Timeline[i].Index = i;

        LoadedCount = Timeline.Count;
        OnPropertyChanged(nameof(TimelineSummary));
        OnPropertyChanged(nameof(TotalDurationText));
        RaiseGateFlags();
        OnPropertyChanged(nameof(HasSelection));
    }

    private void LoadTimeline(IEnumerable<ClickStep> steps)
    {
        foreach (var row in Timeline)
            row.PropertyChanged -= OnStepRowChanged;

        Timeline.Clear();
        foreach (var step in steps)
        {
            var row = new StepRow(step);
            row.PropertyChanged += OnStepRowChanged;
            Timeline.Add(row);
        }
        SelectedStep = null;
        RefreshTimelineMeta();
    }

    private void AddRow(StepRow row, int index)
    {
        row.PropertyChanged += OnStepRowChanged;
        Timeline.Insert(index, row);
        RefreshTimelineMeta();
    }

    private void RemoveRow(StepRow row)
    {
        row.PropertyChanged -= OnStepRowChanged;
        Timeline.Remove(row);
        RefreshTimelineMeta();
    }

    private void OnStepRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StepRow.DelayMs)
            or nameof(StepRow.IsMuted)
            or nameof(StepRow.HoldMs))
        {
            OnPropertyChanged(nameof(TimelineSummary));
            OnPropertyChanged(nameof(TotalDurationText));
        }
    }

    // --- Loop options ---

    public ObservableCollection<string> LoopOptions { get; } = new()
        { "Forever", "1", "5", "10", "25", "50", "100", "Custom" };

    [ObservableProperty]
    private string _selectedLoop = "10";

    [ObservableProperty]
    private bool _isCustomLoop;

    [ObservableProperty]
    private int _customLoops = 10;

    partial void OnSelectedLoopChanged(string value)
    {
        IsCustomLoop = value == "Custom";
        PersistSettings();
    }

    private int ResolveLoopCount()
    {
        if (SelectedLoop == "Forever") return -1;
        if (SelectedLoop == "Custom") return CustomLoops < 1 ? 1 : CustomLoops;
        return int.TryParse(SelectedLoop, out var n) ? n : 1;
    }

    // --- Speed ---

    public ObservableCollection<string> SpeedOptions { get; } = new()
        { "0.25x", "0.5x", "0.75x", "1x", "1.5x", "2x", "3x", "5x", "10x", "Custom" };

    [ObservableProperty]
    private string _selectedSpeed = "1x";

    [ObservableProperty]
    private bool _isCustomSpeed;

    [ObservableProperty]
    private double _customSpeed = 1.0000;

    partial void OnSelectedSpeedChanged(string value)
    {
        IsCustomSpeed = value == "Custom";
        OnPropertyChanged(nameof(SpeedLabel));
        OnPropertyChanged(nameof(TimelineSummary));
        PersistSettings();
    }

    partial void OnCustomSpeedChanged(double value)
    {
        OnPropertyChanged(nameof(SpeedLabel));
        OnPropertyChanged(nameof(TimelineSummary));
        PersistSettings();
    }

    public string SpeedLabel => $"{ResolveSpeed():0.##}x";

    private double ResolveSpeed()
    {
        double s;
        if (SelectedSpeed == "Custom")
        {
            s = CustomSpeed;
        }
        else
        {
            var text = SelectedSpeed.TrimEnd('x');
            if (!double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out s))
                s = 1.0;
        }

        if (s <= 0) s = 1.0;
        return Math.Clamp(s, MinSpeed, MaxSpeed);
    }

    // --- Hotkeys ---

    [ObservableProperty]
    private string _stopKeyName = "F8";

    [ObservableProperty]
    private string _pauseKeyName = "F9";

    private long _stopKeyCode = PlatformServices.DefaultStopKey();
    private long _pauseKeyCode = PlatformServices.DefaultPauseKey();

    // Which action the next captured key should be assigned to.
    private HotkeyAction? _capturingAction;

    [ObservableProperty]
    private string _recordKeyName = "F6";

    [ObservableProperty]
    private string _stopRecKeyName = "F7";

    [ObservableProperty]
    private string _emergencyKeyName = "F10";

    private long _recordKeyCode = IsWindows ? 117 : 97;      // F6
    private long _stopRecKeyCode = IsWindows ? 118 : 98;     // F7
    private long _emergencyKeyCode = IsWindows ? 121 : 109;  // F10

    /// <summary>Current bindings, in the shape the hotkey service wants.</summary>
    private Dictionary<HotkeyAction, long> BuildBindings() => new()
    {
        [HotkeyAction.Record] = _recordKeyCode,
        [HotkeyAction.StopRecording] = _stopRecKeyCode,
        [HotkeyAction.StartStop] = _stopKeyCode,
        [HotkeyAction.PauseResume] = _pauseKeyCode,
        [HotkeyAction.Emergency] = _emergencyKeyCode
    };

    private void RebindHotkeys() => _hotkey.StartListening(BuildBindings());

    // --- Presets ---

    [ObservableProperty]
    private string _presetName = "my_clicks";

    /// <summary>The preset currently open, or null if this is unsaved work.</summary>
    [ObservableProperty]
    private string? _loadedPresetName;

    public bool HasLoadedPreset => !string.IsNullOrWhiteSpace(LoadedPresetName);

    partial void OnLoadedPresetNameChanged(string? value)
        => OnPropertyChanged(nameof(HasLoadedPreset));

    public ObservableCollection<string> Presets { get; } = new();

    public ObservableCollection<PresetEntry> PresetEntries { get; } = new();

    [ObservableProperty]
    private PresetEntry? _selectedEntry;

    public bool HasPresets => PresetEntries.Count > 0;

    partial void OnSelectedEntryChanged(PresetEntry? value)
    {
        // Keep the name based selection in step so load and delete keep working.
        SelectedPreset = value?.Name;
    }

    [ObservableProperty]
    private string? _selectedPreset;

    // --- Construction ---

    public MainWindowViewModel()
    {
        _hotkey.ActionTriggered += action =>
            Dispatcher.UIThread.Post(() => HandleHotkey(action));
        _hotkey.KeyCaptured += code => Dispatcher.UIThread.Post(() => HandleCapturedKey(code));

        _recorder.ClickCaptured += count => Dispatcher.UIThread.Post(() =>
        {
            if (count < 0)
            {
                Log = "Recording failed. Grant Input Monitoring permission and restart.";
                IsRecording = false;
                UpdateStatus();
            }
            else
            {
                Log = $"Recording... {count} action(s) captured.";
                LoadedCount = count;
            }
        });

        ApplySettings();

        StopKeyName = KeyName(_stopKeyCode);
        PauseKeyName = KeyName(_pauseKeyCode);

        RecordKeyName = KeyName(_recordKeyCode);
        StopRecKeyName = KeyName(_stopRecKeyCode);
        EmergencyKeyName = KeyName(_emergencyKeyCode);

        RebindHotkeys();
        RefreshPresets();
    }

    /// <summary>Routes a global hotkey to the right behaviour.</summary>
    private void HandleHotkey(HotkeyAction action)
    {
        switch (action)
        {
            case HotkeyAction.Record:
                if (!IsRecording && !IsRunning) Record();
                break;

            case HotkeyAction.StopRecording:
                if (IsRecording) StopRecordingButton();
                break;

            case HotkeyAction.Emergency:
                EmergencyStop();
                break;

            case HotkeyAction.PauseResume:
                HandlePauseKey();
                break;

            default:
                HandleStopKey();
                break;
        }
    }

    /// <summary>
    /// Halts everything immediately, whether recording or playing back. This
    /// is the escape hatch when a fast macro has taken over the mouse.
    /// </summary>
    [RelayCommand]
    private void EmergencyStop()
    {
        bool wasBusy = IsRunning || IsRecording;

        _cts?.Cancel();

        if (IsRecording)
        {
            IsRecording = false;
            _recorder.Stop();
        }

        Countdown = 0;
        IsRunning = false;
        IsPaused = false;
        PlayheadIndex = -1;
        UpdateStatus();

        Log = wasBusy
            ? $"Emergency stop. Everything halted by {EmergencyKeyName}."
            : "Nothing was running.";
    }

    partial void OnIsDarkChanged(bool value) => PersistSettings();
    partial void OnShowHintsChanged(bool value) => PersistSettings();
    partial void OnShowControlsPanelChanged(bool value) => PersistSettings();
    partial void OnShowPlaybackPanelChanged(bool value) => PersistSettings();
    partial void OnShowTimelinePanelChanged(bool value) => PersistSettings();
    partial void OnShowPropertiesPanelChanged(bool value) => PersistSettings();
    partial void OnShowHotkeysPanelChanged(bool value) => PersistSettings();
    partial void OnShowPresetsPanelChanged(bool value) => PersistSettings();
    partial void OnCustomLoopsChanged(int value) => PersistSettings();
    partial void OnStartDelayMsChanged(int value) => PersistSettings();
    partial void OnLoopPauseMsChanged(int value) => PersistSettings();
    partial void OnRemoveDelaysChanged(bool value) => PersistSettings();
    partial void OnNaturalClicksChanged(bool value) => PersistSettings();
    partial void OnDelayJitterPercentChanged(int value) => PersistSettings();
    partial void OnPositionJitterPxChanged(int value) => PersistSettings();
    partial void OnStopAfterActionsEnabledChanged(bool value) => PersistSettings();
    partial void OnStopAfterActionsChanged(int value) => PersistSettings();
    partial void OnStopAfterTimeEnabledChanged(bool value) => PersistSettings();
    partial void OnStopAfterMinutesChanged(int value) => PersistSettings();
    partial void OnPresetNameChanged(string value) => PersistSettings();
    // True once the window has settled, so its size reports are real user
    // resizes rather than startup noise.
    private bool _windowReady;

    partial void OnWindowWidthChanged(double value)
    {
        if (!_windowReady) return;
        PersistSettings();
    }

    partial void OnWindowHeightChanged(double value)
    {
        if (!_windowReady) return;
        PersistSettings();
    }

    /// <summary>
    /// Called by the view once the window is open. Re-applies the saved size,
    /// because the platform may have overridden it during startup, and then
    /// starts honouring resizes.
    /// </summary>
    public void OnWindowOpened()
    {
        _loadingSettings = true;
        WindowWidth = IsAdvanced ? _settings.AdvancedWidth : _settings.SimpleWidth;
        WindowHeight = IsAdvanced ? _settings.AdvancedHeight : _settings.SimpleHeight;
        _loadingSettings = false;

        _windowReady = true;
    }

    private void PersistSettingsHook() => PersistSettings();

    /// <summary>
    /// Copies current state into the settings object and writes it to disk.
    /// Called from every property that should survive a restart.
    /// </summary>
    private void PersistSettings()
    {
        if (_loadingSettings) return;

        _settings.IsDark = IsDark;
        _settings.IsAdvanced = IsAdvanced;
        _settings.SelectedLayout = SelectedLayout;
        _settings.ShowHints = ShowHints;

        _settings.ShowControlsPanel = ShowControlsPanel;
        _settings.ShowPlaybackPanel = ShowPlaybackPanel;
        _settings.ShowTimelinePanel = ShowTimelinePanel;
        _settings.ShowPropertiesPanel = ShowPropertiesPanel;
        _settings.ShowHotkeysPanel = ShowHotkeysPanel;
        _settings.ShowPresetsPanel = ShowPresetsPanel;

        _settings.SelectedLoop = SelectedLoop;
        _settings.CustomLoops = CustomLoops;
        _settings.SelectedSpeed = SelectedSpeed;
        _settings.CustomSpeed = CustomSpeed;
        _settings.StartDelayMs = StartDelayMs;
        _settings.LoopPauseMs = LoopPauseMs;
        _settings.RemoveDelays = RemoveDelays;
        _settings.NaturalClicks = NaturalClicks;
        _settings.DelayJitterPercent = DelayJitterPercent;
        _settings.PositionJitterPx = PositionJitterPx;
        _settings.StopAfterActionsEnabled = StopAfterActionsEnabled;
        _settings.StopAfterActions = StopAfterActions;
        _settings.StopAfterTimeEnabled = StopAfterTimeEnabled;
        _settings.StopAfterMinutes = StopAfterMinutes;

        _settings.StopKeyCode = _stopKeyCode;
        _settings.PauseKeyCode = _pauseKeyCode;
        _settings.RecordKeyCode = _recordKeyCode;
        _settings.StopRecKeyCode = _stopRecKeyCode;
        _settings.EmergencyKeyCode = _emergencyKeyCode;
        _settings.PresetName = PresetName;

        // Window size belongs to whichever mode is showing right now.
        if (IsAdvanced)
        {
            _settings.AdvancedWidth = WindowWidth;
            _settings.AdvancedHeight = WindowHeight;
        }
        else
        {
            _settings.SimpleWidth = WindowWidth;
            _settings.SimpleHeight = WindowHeight;
        }

        SettingsService.Save(_settings);
    }

    /// <summary>Copies the saved settings onto the matching properties.</summary>
    private void ApplySettings()
    {
        _loadingSettings = true;

        IsDark = _settings.IsDark;
        IsAdvanced = _settings.IsAdvanced;
        SelectedLayout = _settings.SelectedLayout;
        ShowHints = _settings.ShowHints;

        ShowControlsPanel = _settings.ShowControlsPanel;
        ShowPlaybackPanel = _settings.ShowPlaybackPanel;
        ShowTimelinePanel = _settings.ShowTimelinePanel;
        ShowPropertiesPanel = _settings.ShowPropertiesPanel;
        ShowHotkeysPanel = _settings.ShowHotkeysPanel;
        ShowPresetsPanel = _settings.ShowPresetsPanel;

        SelectedLoop = _settings.SelectedLoop;
        CustomLoops = _settings.CustomLoops;
        SelectedSpeed = _settings.SelectedSpeed;
        CustomSpeed = _settings.CustomSpeed;
        StartDelayMs = _settings.StartDelayMs;
        LoopPauseMs = _settings.LoopPauseMs;
        RemoveDelays = _settings.RemoveDelays;
        NaturalClicks = _settings.NaturalClicks;
        DelayJitterPercent = _settings.DelayJitterPercent;
        PositionJitterPx = _settings.PositionJitterPx;
        StopAfterActionsEnabled = _settings.StopAfterActionsEnabled;
        StopAfterActions = _settings.StopAfterActions;
        StopAfterTimeEnabled = _settings.StopAfterTimeEnabled;
        StopAfterMinutes = _settings.StopAfterMinutes;

        PresetName = _settings.PresetName;

        // A stored code of -1 means the user never rebound the key, so the
        // platform default applies instead.
        if (_settings.StopKeyCode >= 0) _stopKeyCode = _settings.StopKeyCode;
        if (_settings.PauseKeyCode >= 0) _pauseKeyCode = _settings.PauseKeyCode;
        if (_settings.RecordKeyCode >= 0) _recordKeyCode = _settings.RecordKeyCode;
        if (_settings.StopRecKeyCode >= 0) _stopRecKeyCode = _settings.StopRecKeyCode;
        if (_settings.EmergencyKeyCode >= 0) _emergencyKeyCode = _settings.EmergencyKeyCode;

        WindowWidth = IsAdvanced ? _settings.AdvancedWidth : _settings.SimpleWidth;
        WindowHeight = IsAdvanced ? _settings.AdvancedHeight : _settings.SimpleHeight;

        _loadingSettings = false;
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

    /// <summary>Drops a custom value and returns to the preset list.</summary>
    [RelayCommand]
    private void ClearCustomSpeed() => SelectedSpeed = "1x";

    [RelayCommand]
    private void ClearCustomLoop() => SelectedLoop = "10";

    [RelayCommand]
    private void ToggleMode() => IsAdvanced = !IsAdvanced;

    // --- Window size presets ---

    public ObservableCollection<string> ScreenSizes { get; } = new()
        { "Compact", "Standard", "Comfort", "Wide", "Tall", "Studio" };

    [ObservableProperty]
    private string _selectedScreenSize = "Standard";

    [ObservableProperty]
    private double _windowWidth = 700;

    [ObservableProperty]
    private double _windowHeight = 760;

    partial void OnSelectedScreenSizeChanged(string value)
    {
        (double w, double h) = value switch
        {
            "Compact"  => (380d, 600d),
            "Standard" => (440d, 730d),
            "Comfort"  => (520d, 830d),
            "Wide"     => (660d, 830d),
            "Tall"     => (520d, 1000d),
            "Studio"   => (780d, 1000d),
            _          => (440d, 730d)
        };

        WindowWidth = w;
        WindowHeight = h;
        Log = $"Window set to {value}, {w:0} by {h:0}.";
    }

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
            PlayheadIndex = -1;
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
        if (_capturingAction == null) return;

        var name = KeyName(code);
        var target = _capturingAction.Value;
        _capturingAction = null;

        // Refuse a key that is already doing something else, otherwise one
        // press would fire two actions and the second binding would be dead.
        foreach (var pair in BuildBindings())
        {
            if (pair.Key != target && pair.Value == code)
            {
                Log = $"{name} is already assigned to {Describe(pair.Key)}.";
                return;
            }
        }

        switch (target)
        {
            case HotkeyAction.Record: _recordKeyCode = code; RecordKeyName = name; break;
            case HotkeyAction.StopRecording: _stopRecKeyCode = code; StopRecKeyName = name; break;
            case HotkeyAction.PauseResume: _pauseKeyCode = code; PauseKeyName = name; break;
            case HotkeyAction.Emergency: _emergencyKeyCode = code; EmergencyKeyName = name; break;
            default: _stopKeyCode = code; StopKeyName = name; break;
        }

        RebindHotkeys();
        PersistSettings();
        Log = $"{Describe(target)} is now {name}.";
    }

    private static string Describe(HotkeyAction action) => action switch
    {
        HotkeyAction.Record => "Record",
        HotkeyAction.StopRecording => "Stop recording",
        HotkeyAction.PauseResume => "Pause / Resume",
        HotkeyAction.Emergency => "Emergency stop",
        _ => "Start / Stop"
    };

    private void BeginCapture(HotkeyAction action)
    {
        _capturingAction = action;
        _hotkey.BeginCapture();
        Log = $"Press any key to set {Describe(action)}...";
    }

    [RelayCommand]
    private void CaptureRecordKey() => BeginCapture(HotkeyAction.Record);

    [RelayCommand]
    private void CaptureStopRecKey() => BeginCapture(HotkeyAction.StopRecording);

    [RelayCommand]
    private void CaptureEmergencyKey() => BeginCapture(HotkeyAction.Emergency);

    [RelayCommand]
    private void CaptureStopKey() => BeginCapture(HotkeyAction.StartStop);

    [RelayCommand]
    private void CapturePauseKey() => BeginCapture(HotkeyAction.PauseResume);

    // --- Playback ---

    [RelayCommand]
    private async Task Start()
    {
        if (IsRunning) return;

        var plan = Timeline.Where(s => !s.IsMuted).ToList();
        if (plan.Count == 0)
        {
            Log = Timeline.Count == 0
                ? "Nothing to play. Record or load actions first."
                : "Every action is muted. Unmute at least one.";
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

            if (StartDelayMs > 0)
            {
                Log = $"Waiting {StartDelayMs} ms before the first action...";
                await Task.Delay(StartDelayMs, token);
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

        TotalLoops = loops;
        CurrentLoop = 0;
        ActionsPerformed = 0;
        var runClock = System.Diagnostics.Stopwatch.StartNew();

        // Captured once so editing the box mid-run cannot move the target.
        int actionLimit = StopAfterActionsEnabled ? Math.Max(1, StopAfterActions) : -1;
        double timeLimitMs = StopAfterTimeEnabled ? Math.Max(1, StopAfterMinutes) * 60000.0 : -1;
        string? stopReason = null;
        string loopText = loops == -1 ? "forever" : $"{loops} loop(s)";
        string delayText = RemoveDelays ? "seamless loop" : "recorded timing";
        Log = $"Started. {plan.Count} actions, {loopText}, {speed:0.##}x speed, {delayText}. Stop={StopKeyName}, Pause={PauseKeyName}.";

        try
        {
            int rep = 0;
            while (loops == -1 || rep < loops)
            {
                rep++;
                CurrentLoop = rep;
                ElapsedText = runClock.Elapsed.ToString(@"mm\:ss");

                if (rep > 1 && LoopPauseMs > 0)
                    await Task.Delay(LoopPauseMs, token);

                for (int i = 0; i < plan.Count; i++)
                {
                    var step = plan[i];

                    if (token.IsCancellationRequested) break;

                    while (IsPaused && !token.IsCancellationRequested)
                        await Task.Delay(100, token);

                    if (token.IsCancellationRequested) break;

                    // Normally wait this step's delay, scaled by speed. When
                    // seamless loop is on, skip the delay before the first
                    // action of every repeat after the first. That gap is the
                    // pause between loops.
                    bool skipDelay = RemoveDelays && rep > 1 && i == 0;
                    if (!skipDelay)
                    {
                        int delay = (int)Math.Round(step.DelayMs / speed);
                        if (NaturalClicks) delay = Humanizer.Delay(delay, DelayJitterPercent);
                        if (delay < MinDelayMs) delay = MinDelayMs;
                        await Task.Delay(delay, token);
                    }

                    Execute(step);
                    PlayheadIndex = step.Index;
                    ActionsPerformed++;

                    if (actionLimit > 0 && ActionsPerformed >= actionLimit)
                    {
                        stopReason = $"Reached the {actionLimit} action limit.";
                        break;
                    }

                    if (timeLimitMs > 0 && runClock.Elapsed.TotalMilliseconds >= timeLimitMs)
                    {
                        stopReason = $"Reached the {StopAfterMinutes} minute limit.";
                        break;
                    }

                    string label = loops == -1 ? $"loop {rep}" : $"loop {rep}/{loops}";
                    Log = $"{label}: step {step.Index + 1} {step.TypeLabel} at ({step.X:0}, {step.Y:0})";
                }

                if (token.IsCancellationRequested || stopReason != null) break;
            }
        }
        catch (TaskCanceledException) { }

        runClock.Stop();
        ElapsedText = runClock.Elapsed.ToString(@"mm\:ss");

        IsRunning = false;
        IsPaused = false;
        PlayheadIndex = -1;
        UpdateStatus();
        if (!token.IsCancellationRequested)
            Log = stopReason ?? "Finished.";
    }

    /// <summary>Performs one step according to its action type.</summary>
    private void Execute(StepRow step)
    {
        double x = step.X;
        double y = step.Y;

        // Scatter the landing point slightly so repeated runs do not hit the
        // exact same pixel. Movement and scroll are left alone since their
        // position is usually structural rather than a target.
        if (NaturalClicks && step.Type == ActionType.Click)
        {
            x = Humanizer.Position(x, PositionJitterPx);
            y = Humanizer.Position(y, PositionJitterPx);
        }

        switch (step.Type)
        {
            case ActionType.Move:
                _mouse.MoveTo(x, y);
                break;

            case ActionType.Scroll:
                _mouse.Scroll(x, y, step.ScrollAmount);
                break;

            case ActionType.Delay:
                // The wait already happened before this step ran.
                break;

            case ActionType.KeyPress:
                // Keyboard playback is not implemented yet, so this is a no-op
                // rather than a wrong click. Recorded key steps stay in the
                // timeline and start working once key output lands.
                break;

            default:
                int hold = NaturalClicks ? Humanizer.Hold(step.HoldMs) : step.HoldMs;
                _mouse.ClickAt(x, y, step.Button, step.ClickCount, hold);
                break;
        }
    }

    /// <summary>Same path the pause hotkey uses, so the two cannot diverge.</summary>
    [RelayCommand]
    private void TogglePause() => HandlePauseKey();

    [RelayCommand]
    private void Stop()
    {
        _cts?.Cancel();
        Countdown = 0;
        IsRunning = false;
        IsPaused = false;
        PlayheadIndex = -1;
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

        var captured = _recorder.Stop();

        // The click that hit the Stop Rec button is not part of the macro.
        if (_stoppedByButton && captured.Count > 0)
            captured.RemoveAt(captured.Count - 1);
        _stoppedByButton = false;

        LoadTimeline(captured);
        Log = $"Recorded {Timeline.Count} actions.";
    }

    // --- Timeline editing ---

    [RelayCommand]
    private void DeleteStep()
    {
        if (SelectedStep == null || !CanEdit) return;
        int at = Timeline.IndexOf(SelectedStep);
        RemoveRow(SelectedStep);
        SelectedStep = Timeline.Count == 0 ? null : Timeline[Math.Min(at, Timeline.Count - 1)];
        Log = $"Deleted action. {Timeline.Count} remaining.";
    }

    [RelayCommand]
    private void DuplicateStep()
    {
        if (SelectedStep == null || !CanEdit) return;
        int at = Timeline.IndexOf(SelectedStep);
        var copy = SelectedStep.Clone();
        AddRow(copy, at + 1);
        SelectedStep = copy;
        Log = $"Duplicated action {at + 1}.";
    }

    [RelayCommand]
    private void MoveStepUp()
    {
        if (SelectedStep == null || !CanEdit) return;
        int at = Timeline.IndexOf(SelectedStep);
        if (at <= 0) return;
        Timeline.Move(at, at - 1);
        RefreshTimelineMeta();
        Log = $"Moved action to position {at}.";
    }

    [RelayCommand]
    private void MoveStepDown()
    {
        if (SelectedStep == null || !CanEdit) return;
        int at = Timeline.IndexOf(SelectedStep);
        if (at < 0 || at >= Timeline.Count - 1) return;
        Timeline.Move(at, at + 1);
        RefreshTimelineMeta();
        Log = $"Moved action to position {at + 2}.";
    }

    [RelayCommand]
    private void ToggleMuteStep()
    {
        if (SelectedStep == null || !CanEdit) return;
        SelectedStep.IsMuted = !SelectedStep.IsMuted;
        OnPropertyChanged(nameof(TimelineSummary));
        Log = SelectedStep.IsMuted ? "Action muted, it will be skipped." : "Action unmuted.";
    }

    /// <summary>Deletes everything before the selected action.</summary>
    [RelayCommand]
    private void TrimBefore()
    {
        if (SelectedStep == null || !CanEdit) return;
        int at = Timeline.IndexOf(SelectedStep);
        if (at <= 0) { Log = "Nothing before this action."; return; }

        for (int i = at - 1; i >= 0; i--)
        {
            Timeline[i].PropertyChanged -= OnStepRowChanged;
            Timeline.RemoveAt(i);
        }
        RefreshTimelineMeta();
        Log = $"Trimmed {at} action(s) from the start.";
    }

    /// <summary>Deletes everything after the selected action.</summary>
    [RelayCommand]
    private void TrimAfter()
    {
        if (SelectedStep == null || !CanEdit) return;
        int at = Timeline.IndexOf(SelectedStep);
        int removed = Timeline.Count - at - 1;
        if (removed <= 0) { Log = "Nothing after this action."; return; }

        for (int i = Timeline.Count - 1; i > at; i--)
        {
            Timeline[i].PropertyChanged -= OnStepRowChanged;
            Timeline.RemoveAt(i);
        }
        RefreshTimelineMeta();
        Log = $"Trimmed {removed} action(s) from the end.";
    }

    // Set once Clear has been pressed, so a second press confirms.
    private bool _clearArmed;

    [RelayCommand]
    private void ClearTimeline()
    {
        if (!CanEdit) return;
        if (Timeline.Count == 0) { Log = "Timeline is already empty."; return; }

        // Clearing throws away unsaved work with no undo, so ask twice.
        if (!_clearArmed)
        {
            _clearArmed = true;
            Log = $"Click Clear again to discard all {Timeline.Count} actions.";
            return;
        }

        _clearArmed = false;
        LoadTimeline(Array.Empty<ClickStep>());
        Log = "Timeline cleared.";
    }

    /// <summary>
    /// Rewrites every delay by the current speed and resets speed to 1x, so
    /// the timing becomes permanent instead of a playback setting.
    /// </summary>
    [RelayCommand]
    private void BakeSpeed()
    {
        if (!CanEdit || Timeline.Count == 0) { Log = "Nothing to bake."; return; }

        double speed = ResolveSpeed();
        if (Math.Abs(speed - 1.0) < 0.001) { Log = "Speed is already 1x."; return; }

        foreach (var row in Timeline)
            row.DelayMs = ClampDelay((int)Math.Round(row.DelayMs / speed));

        SelectedSpeed = "1x";
        CustomSpeed = 1.0;
        RefreshTimelineMeta();
        Log = $"Baked {speed:0.##}x into the timeline. Speed reset to 1x.";
    }

    [RelayCommand]
    private void SetAllDelays()
    {
        if (!CanEdit || Timeline.Count == 0) { Log = "Nothing to change."; return; }

        int value = ClampDelay(BulkDelayMs);
        foreach (var row in Timeline)
            row.DelayMs = value;

        RefreshTimelineMeta();
        Log = $"All {Timeline.Count} actions set to {value} ms.";
    }

    [RelayCommand]
    private void NudgeFaster() => NudgeSelected(-10);

    [RelayCommand]
    private void NudgeSlower() => NudgeSelected(10);

    private void NudgeSelected(int deltaMs)
    {
        if (SelectedStep == null || !CanEdit) return;
        SelectedStep.DelayMs = ClampDelay(SelectedStep.DelayMs + deltaMs);
        OnPropertyChanged(nameof(TimelineSummary));
        Log = $"Action {SelectedStep.Index + 1} delay is now {SelectedStep.DelayMs} ms.";
    }

    private static int ClampDelay(int ms) => Math.Clamp(ms, MinDelayMs, MaxDelayMs);

    // --- Presets ---

    /// <summary>Adds a blank left click after the selection, or at the end.</summary>
    [RelayCommand]
    private void AddStep()
    {
        if (!CanEdit) return;

        int at = SelectedStep == null ? Timeline.Count : Timeline.IndexOf(SelectedStep) + 1;
        var row = new StepRow { Type = ActionType.Click, ClickCount = 1, DelayMs = 100 };

        AddRow(row, at);
        SelectedStep = row;
        Log = $"Added an action at position {at + 1}. Set its position in the editor.";
    }

    /// <summary>Adds a pure wait with no mouse action.</summary>
    [RelayCommand]
    private void AddDelayStep()
    {
        if (!CanEdit) return;

        int at = SelectedStep == null ? Timeline.Count : Timeline.IndexOf(SelectedStep) + 1;
        var row = new StepRow { Type = ActionType.Delay, DelayMs = 500 };

        AddRow(row, at);
        SelectedStep = row;
        Log = $"Added a 500 ms delay at position {at + 1}.";
    }

    [RelayCommand]
    private void EnableAll()
    {
        if (!CanEdit || Timeline.Count == 0) return;
        foreach (var row in Timeline) row.IsMuted = false;
        OnPropertyChanged(nameof(TimelineSummary));
        Log = "All actions enabled.";
    }

    [RelayCommand]
    private void DisableAll()
    {
        if (!CanEdit || Timeline.Count == 0) return;
        foreach (var row in Timeline) row.IsMuted = true;
        OnPropertyChanged(nameof(TimelineSummary));
        Log = "All actions disabled.";
    }

    /// <summary>Overwrites the preset that is currently open.</summary>
    [RelayCommand]
    private void SaveChanges()
    {
        if (string.IsNullOrWhiteSpace(LoadedPresetName))
        {
            Log = "No preset is open. Use Save as new instead.";
            return;
        }

        if (Timeline.Count == 0) { Log = "Nothing to save."; return; }

        PresetService.Save(LoadedPresetName, Timeline.Select(r => r.ToStep()).ToList());
        PresetMeta.Record(LoadedPresetName, Timeline.Count);
        RefreshPresets();
        Flash(v => SaveFlash = v);
        Log = $"Saved changes to '{LoadedPresetName}' ({Timeline.Count} actions).";
    }

    /// <summary>Renames the open preset, keeping its steps and favorite state.</summary>
    [RelayCommand]
    private void RenamePreset()
    {
        if (string.IsNullOrWhiteSpace(LoadedPresetName)) { Log = "No preset is open."; return; }
        if (string.IsNullOrWhiteSpace(PresetName)) { Log = "Enter a new name first."; return; }
        if (PresetName == LoadedPresetName) { Log = "That is already the name."; return; }
        if (Presets.Contains(PresetName)) { Log = $"'{PresetName}' already exists."; return; }

        string oldName = LoadedPresetName;
        bool wasFavorite = PresetMeta.IsFavorite(oldName);

        PresetService.Save(PresetName, Timeline.Select(r => r.ToStep()).ToList());
        PresetMeta.Record(PresetName, Timeline.Count);
        if (wasFavorite) PresetMeta.ToggleFavorite(PresetName);

        PresetService.Delete(oldName);
        PresetMeta.Remove(oldName);

        LoadedPresetName = PresetName;
        RefreshPresets();
        Log = $"Renamed '{oldName}' to '{PresetName}'.";
    }

    /// <summary>Appends a saved preset onto the end of the current timeline.</summary>
    [RelayCommand]
    private void AppendPreset()
    {
        if (string.IsNullOrWhiteSpace(SelectedPreset)) { Log = "Pick a preset to append."; return; }
        if (!CanEdit) return;

        var steps = PresetService.Load(SelectedPreset);
        if (steps.Count == 0) { Log = $"'{SelectedPreset}' is empty."; return; }

        foreach (var step in steps)
        {
            var row = new StepRow(step);
            row.PropertyChanged += OnStepRowChanged;
            Timeline.Add(row);
        }

        RefreshTimelineMeta();
        Log = $"Appended {steps.Count} actions from '{SelectedPreset}'.";
    }

    [RelayCommand]
    private void SavePreset()
    {
        if (Timeline.Count == 0) { Log = "Nothing to save. Record some actions first."; return; }
        if (string.IsNullOrWhiteSpace(PresetName)) { Log = "Enter a name for the preset."; return; }

        string name = PresetName;
        if (Presets.Contains(name))
        {
            int n = 1;
            while (Presets.Contains($"{PresetName}_{n}"))
                n++;
            name = $"{PresetName}_{n}";
        }

        PresetService.Save(name, Timeline.Select(r => r.ToStep()).ToList());
        PresetMeta.Record(name, Timeline.Count);
        LoadedPresetName = name;
        PresetName = name;
        RefreshPresets();
        Flash(v => SaveFlash = v);
        Log = $"Saved '{name}' ({Timeline.Count} actions).";
    }

    [RelayCommand]
    private void LoadPreset()
    {
        if (string.IsNullOrWhiteSpace(SelectedPreset)) { Log = "Pick a preset to load."; return; }

        LoadTimeline(PresetService.Load(SelectedPreset));
        PresetMeta.Record(SelectedPreset, Timeline.Count);

        // Adopt the loaded name so the header and the save target agree.
        PresetName = SelectedPreset;
        LoadedPresetName = SelectedPreset;

        Flash(v => LoadFlash = v);
        Log = $"Loaded '{SelectedPreset}' ({Timeline.Count} actions).";
    }

    [RelayCommand]
    private void DeletePreset()
    {
        if (string.IsNullOrWhiteSpace(SelectedPreset)) { Log = "Pick a preset to delete."; return; }

        if (_pendingDelete == SelectedPreset)
        {
            PresetService.Delete(SelectedPreset);
            PresetMeta.Remove(SelectedPreset);
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
        var sorted = PresetMeta.SortForDisplay(PresetService.ListPresets());

        Presets.Clear();
        PresetEntries.Clear();

        foreach (var name in sorted)
        {
            Presets.Add(name);
            PresetEntries.Add(new PresetEntry
            {
                Name = name,
                IsFavorite = PresetMeta.IsFavorite(name),
                Description = PresetMeta.Describe(name)
            });
        }

        OnPropertyChanged(nameof(HasPresets));
    }

    /// <summary>Stars or unstars a preset and re-sorts the list.</summary>
    [RelayCommand]
    private void ToggleFavorite(PresetEntry? entry)
    {
        if (entry == null) return;

        bool nowFavorite = PresetMeta.ToggleFavorite(entry.Name);
        string keep = entry.Name;

        RefreshPresets();

        foreach (var row in PresetEntries)
        {
            if (row.Name == keep)
            {
                SelectedEntry = row;
                break;
            }
        }

        Log = nowFavorite ? $"'{keep}' added to favorites." : $"'{keep}' removed from favorites.";
    }

    // --- Helpers ---

    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private static string KeyName(long code)
    {
        if (IsWindows) return WindowsKeyName(code);
        return MacKeyName(code);
    }

    /// <summary>Windows virtual key codes.</summary>
    private static string WindowsKeyName(long code) => code switch
    {
        112 => "F1", 113 => "F2", 114 => "F3", 115 => "F4",
        116 => "F5", 117 => "F6", 118 => "F7", 119 => "F8",
        120 => "F9", 121 => "F10", 122 => "F11", 123 => "F12",
        8 => "Backspace", 9 => "Tab", 13 => "Return", 19 => "Pause",
        20 => "Caps Lock", 27 => "Escape", 32 => "Space",
        33 => "Page Up", 34 => "Page Down", 35 => "End", 36 => "Home",
        37 => "Left", 38 => "Up", 39 => "Right", 40 => "Down",
        45 => "Insert", 46 => "Delete",
        96 => "Numpad 0", 97 => "Numpad 1", 98 => "Numpad 2",
        99 => "Numpad 3", 100 => "Numpad 4", 101 => "Numpad 5",
        102 => "Numpad 6", 103 => "Numpad 7", 104 => "Numpad 8",
        105 => "Numpad 9",
        >= 48 and <= 57 => ((char)code).ToString(),
        >= 65 and <= 90 => ((char)code).ToString(),
        _ => $"Key {code}"
    };

    /// <summary>macOS virtual key codes, which do not match the Windows set.</summary>
    private static string MacKeyName(long code) => code switch
    {
        122 => "F1", 120 => "F2", 99 => "F3", 118 => "F4",
        96 => "F5", 97 => "F6", 98 => "F7", 100 => "F8",
        101 => "F9", 109 => "F10", 103 => "F11", 111 => "F12",
        51 => "Delete", 48 => "Tab", 36 => "Return", 53 => "Escape",
        49 => "Space", 117 => "Forward Delete",
        115 => "Home", 119 => "End", 116 => "Page Up", 121 => "Page Down",
        123 => "Left", 124 => "Right", 125 => "Down", 126 => "Up",
        0 => "A", 11 => "B", 8 => "C", 2 => "D", 14 => "E", 3 => "F",
        5 => "G", 4 => "H", 34 => "I", 38 => "J", 40 => "K", 37 => "L",
        46 => "M", 45 => "N", 31 => "O", 35 => "P", 12 => "Q", 15 => "R",
        1 => "S", 17 => "T", 32 => "U", 9 => "V", 13 => "W", 7 => "X",
        16 => "Y", 6 => "Z",
        29 => "0", 18 => "1", 19 => "2", 20 => "3", 21 => "4",
        23 => "5", 22 => "6", 26 => "7", 28 => "8", 25 => "9",
        _ => $"Key {code}"
    };
}