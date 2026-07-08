using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Clickto.Models;

namespace Clickto.Services;

/// <summary>
/// Saves and loads click sequences as JSON preset files.
/// </summary>
public static class PresetService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static string PresetsFolder
    {
        get
        {
            var home = System.Environment.GetFolderPath(
                System.Environment.SpecialFolder.UserProfile);
            var folder = Path.Combine(home, "Clickto", "presets");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    /// <summary>Write a sequence to presets/{name}.json.</summary>
    public static void Save(string name, List<ClickStep> steps)
    {
        var path = Path.Combine(PresetsFolder, name + ".json");
        var json = JsonSerializer.Serialize(steps, Options);
        File.WriteAllText(path, json);
    }

    /// <summary>Read a sequence back from presets/{name}.json.</summary>
    public static List<ClickStep> Load(string name)
    {
        var path = Path.Combine(PresetsFolder, name + ".json");
        if (!File.Exists(path))
            return new List<ClickStep>();

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<ClickStep>>(json)
               ?? new List<ClickStep>();
    }

    /// <summary>Delete presets/{name}.json if it exists.</summary>
    public static void Delete(string name)
    {
        var path = Path.Combine(PresetsFolder, name + ".json");
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>List preset names (without the .json extension).</summary>
    public static List<string> ListPresets()
    {
        var names = new List<string>();
        foreach (var file in Directory.GetFiles(PresetsFolder, "*.json"))
            names.Add(Path.GetFileNameWithoutExtension(file));
        return names;
    }
}