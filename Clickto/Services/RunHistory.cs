using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Clickto.Services;

/// <summary>One completed playback run.</summary>
public class RunRecord
{
    public string PresetName { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public int DurationMs { get; set; }
    public int Actions { get; set; }
    public int Loops { get; set; }

    /// <summary>Finished, Stopped, Emergency, or a stop condition.</summary>
    public string Outcome { get; set; } = "Finished";

    public string DurationText
    {
        get
        {
            if (DurationMs < 1000) return $"{DurationMs} ms";
            var span = TimeSpan.FromMilliseconds(DurationMs);
            if (span.TotalMinutes < 1) return $"{span.TotalSeconds:0.0} s";
            if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m {span.Seconds}s";
            return $"{(int)span.TotalHours}h {span.Minutes}m";
        }
    }

    public string WhenText
    {
        get
        {
            var span = DateTime.Now - StartedAt;
            if (span.TotalMinutes < 1) return "just now";
            if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalDays < 1) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 30) return $"{(int)span.TotalDays}d ago";
            return StartedAt.ToString("d MMM");
        }
    }

    public string Summary => $"{Actions} actions · {DurationText} · {WhenText}";
}

/// <summary>Keeps the most recent runs on disk so they survive a restart.</summary>
public static class RunHistory
{
    private const int MaxEntries = 40;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private static string HistoryPath
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var folder = Path.Combine(home, "Clickto");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "history.json");
        }
    }

    public static List<RunRecord> Load()
    {
        try
        {
            if (!File.Exists(HistoryPath)) return new();
            var json = File.ReadAllText(HistoryPath);
            return JsonSerializer.Deserialize<List<RunRecord>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }

    /// <summary>Adds a run to the front and trims the tail.</summary>
    public static List<RunRecord> Add(RunRecord record)
    {
        var all = Load();
        all.Insert(0, record);

        if (all.Count > MaxEntries)
            all.RemoveRange(MaxEntries, all.Count - MaxEntries);

        Save(all);
        return all;
    }

    public static void Clear() => Save(new List<RunRecord>());

    private static void Save(List<RunRecord> records)
    {
        try
        {
            File.WriteAllText(HistoryPath, JsonSerializer.Serialize(records, Options));
        }
        catch
        {
            // History is a convenience. Losing it is not worth crashing over.
        }
    }
}
