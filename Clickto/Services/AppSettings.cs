using System;
using System.IO;
using System.Text.Json;

namespace Clickto.Services;

/// <summary>
/// Everything that should survive a restart. Defaults here match the values
/// the ViewModel starts with, so a missing or corrupt file behaves like a
/// fresh install rather than an error.
/// </summary>
public class AppSettings
{
    // Appearance
    public bool IsDark { get; set; } = true;
    public bool IsAdvanced { get; set; }
    public string SelectedLayout { get; set; } = "Three column";
    public bool ShowHints { get; set; } = true;

    // Window size per mode, so each remembers how the user left it.
    public double SimpleWidth { get; set; } = 700;
    public double SimpleHeight { get; set; } = 760;
    public double AdvancedWidth { get; set; } = 1280;
    public double AdvancedHeight { get; set; } = 860;

    // Panels
    public bool ShowControlsPanel { get; set; } = true;
    public bool ShowPlaybackPanel { get; set; } = true;
    public bool ShowTimelinePanel { get; set; } = true;
    public bool ShowPropertiesPanel { get; set; } = true;
    public bool ShowHotkeysPanel { get; set; } = true;
    public bool ShowPresetsPanel { get; set; } = true;

    // Playback
    public string SelectedLoop { get; set; } = "10";
    public int CustomLoops { get; set; } = 10;
    public string SelectedSpeed { get; set; } = "1x";
    public double CustomSpeed { get; set; } = 1.0;
    public int StartDelayMs { get; set; }
    public int LoopPauseMs { get; set; }
    public bool RemoveDelays { get; set; }
    public bool NaturalClicks { get; set; }
    public int DelayJitterPercent { get; set; } = 15;
    public int PositionJitterPx { get; set; } = 3;

    public bool SmoothTravel { get; set; }

    // Stop conditions
    public bool StopAfterActionsEnabled { get; set; }
    public int StopAfterActions { get; set; } = 1000;
    public bool StopAfterTimeEnabled { get; set; }
    public int StopAfterMinutes { get; set; } = 10;

    // Hotkeys, stored as platform key codes.
    public long StopKeyCode { get; set; } = -1;
    public long PauseKeyCode { get; set; } = -1;
    public long RecordKeyCode { get; set; } = -1;
    public long StopRecKeyCode { get; set; } = -1;
    public long EmergencyKeyCode { get; set; } = -1;

    // Last used preset name, restored into the name box.
    public string PresetName { get; set; } = "my_clicks";
}

/// <summary>Reads and writes AppSettings as JSON in the Clickto folder.</summary>
public static class SettingsService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private static string SettingsPath
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var folder = Path.Combine(home, "Clickto");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "settings.json");
        }
    }

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            // A broken settings file should never stop the app from starting.
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, Options));
        }
        catch
        {
            // Saving preferences is best effort, not something to crash over.
        }
    }
}
