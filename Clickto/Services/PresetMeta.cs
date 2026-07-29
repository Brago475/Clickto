using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Clickto.Services;

/// <summary>Extra information about a preset that does not belong in the step list.</summary>
public class PresetInfo
{
    public bool IsFavorite { get; set; }
    public int ActionCount { get; set; }
    public DateTime LastUsed { get; set; }
}

/// <summary>
/// Stores favorites, action counts and timestamps in a sidecar file so the
/// preset files themselves stay a plain list of steps and keep loading in
/// older builds.
/// </summary>
public static class PresetMeta
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    // Deliberately outside the presets folder, which is scanned for *.json.
    private static string MetaPath
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var folder = Path.Combine(home, "Clickto");
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "presets-meta.json");
        }
    }

    private static Dictionary<string, PresetInfo> Load()
    {
        try
        {
            if (!File.Exists(MetaPath)) return new();
            var json = File.ReadAllText(MetaPath);
            return JsonSerializer.Deserialize<Dictionary<string, PresetInfo>>(json) ?? new();
        }
        catch
        {
            // A corrupt sidecar should never stop presets from loading.
            return new();
        }
    }

    private static void Save(Dictionary<string, PresetInfo> data)
    {
        try
        {
            File.WriteAllText(MetaPath, JsonSerializer.Serialize(data, Options));
        }
        catch
        {
            // Metadata is a convenience. Failing to write it is not fatal.
        }
    }

    public static PresetInfo Get(string name)
    {
        var data = Load();
        return data.TryGetValue(name, out var info) ? info : new PresetInfo();
    }

    public static bool IsFavorite(string name) => Get(name).IsFavorite;

    /// <summary>Flips the favorite flag and returns the new state.</summary>
    public static bool ToggleFavorite(string name)
    {
        var data = Load();
        if (!data.TryGetValue(name, out var info))
        {
            info = new PresetInfo();
            data[name] = info;
        }

        info.IsFavorite = !info.IsFavorite;
        Save(data);
        return info.IsFavorite;
    }

    public static void Record(string name, int actionCount)
    {
        var data = Load();
        if (!data.TryGetValue(name, out var info))
        {
            info = new PresetInfo();
            data[name] = info;
        }

        info.ActionCount = actionCount;
        info.LastUsed = DateTime.Now;
        Save(data);
    }

    public static void Remove(string name)
    {
        var data = Load();
        if (data.Remove(name)) Save(data);
    }

    /// <summary>Favorites first, then alphabetical.</summary>
    public static List<string> SortForDisplay(IEnumerable<string> names)
    {
        var data = Load();
        var list = new List<string>(names);
        list.Sort((a, b) =>
        {
            bool fa = data.TryGetValue(a, out var ia) && ia.IsFavorite;
            bool fb = data.TryGetValue(b, out var ib) && ib.IsFavorite;
            if (fa != fb) return fa ? -1 : 1;
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        });
        return list;
    }

    /// <summary>"12 actions - 2 days ago" for the preset list.</summary>
    public static string Describe(string name)
    {
        var info = Get(name);
        if (info.ActionCount == 0 && info.LastUsed == default) return "";

        string count = info.ActionCount > 0 ? $"{info.ActionCount} actions" : "";
        if (info.LastUsed == default) return count;

        var span = DateTime.Now - info.LastUsed;
        string when =
            span.TotalMinutes < 1 ? "just now" :
            span.TotalHours < 1 ? $"{(int)span.TotalMinutes}m ago" :
            span.TotalDays < 1 ? $"{(int)span.TotalHours}h ago" :
            span.TotalDays < 30 ? $"{(int)span.TotalDays}d ago" :
            info.LastUsed.ToString("d MMM");

        return count.Length > 0 ? $"{count} · {when}" : when;
    }
}
