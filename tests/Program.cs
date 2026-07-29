using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Clickto.Models;
using Clickto.Services;
using Clickto.ViewModels;

int passed = 0, failed = 0;

void Check(string name, bool condition, string detail = "")
{
    if (condition) { passed++; Console.WriteLine($"  pass  {name}"); }
    else { failed++; Console.WriteLine($"  FAIL  {name}  {detail}"); }
}

void Section(string title) => Console.WriteLine($"\n{title}");

// Everything writes into the real Clickto folder, so it gets moved aside first.
var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var live = Path.Combine(home, "Clickto");
var stash = Path.Combine(home, "Clickto.testbackup");

if (Directory.Exists(stash)) Directory.Delete(stash, true);
if (Directory.Exists(live)) Directory.Move(live, stash);

try
{
    Section("ClickStep model");
    {
        var step = new ClickStep(100, 200, 50);
        Check("defaults to a single left click",
            step.Type == ActionType.Click && step.Button == MouseButton.Left && step.ClickCount == 1);
        Check("left click label", step.TypeLabel == "Left Click", step.TypeLabel);

        step.Button = MouseButton.Right;
        Check("right click label", step.TypeLabel == "Right Click", step.TypeLabel);

        step.ClickCount = 2;
        Check("double click label", step.TypeLabel == "Right Click (x2)", step.TypeLabel);

        step.ClickCount = 1;
        step.HoldMs = 400;
        Check("hold label", step.TypeLabel == "Right Click (Hold)", step.TypeLabel);

        var scroll = new ClickStep { Type = ActionType.Scroll, ScrollAmount = -3 };
        Check("scroll down label", scroll.TypeLabel == "Scroll Down", scroll.TypeLabel);
        scroll.ScrollAmount = 3;
        Check("scroll up label", scroll.TypeLabel == "Scroll Up", scroll.TypeLabel);

        var delay = new ClickStep { Type = ActionType.Delay };
        Check("delay label", delay.TypeLabel == "Delay", delay.TypeLabel);
    }

    Section("Old preset files still load");
    {
        // Shape written by v1.1, before the extra fields existed.
        var legacy = "[{\"X\":10,\"Y\":20,\"DelayMs\":75}]";
        var steps = JsonSerializer.Deserialize<List<ClickStep>>(legacy);

        Check("parses", steps != null && steps.Count == 1);
        Check("keeps coordinates", steps![0].X == 10 && steps[0].Y == 20 && steps[0].DelayMs == 75);
        Check("fills in click type", steps[0].Type == ActionType.Click);
        Check("fills in button", steps[0].Button == MouseButton.Left);
        Check("fills in click count", steps[0].ClickCount == 1);
    }

    Section("StepRow round trip");
    {
        var original = new ClickStep
        {
            X = 512.7, Y = 384.2, DelayMs = 120, IsMuted = true,
            Type = ActionType.Scroll, Button = MouseButton.Middle,
            ClickCount = 3, HoldMs = 250, ScrollAmount = -5, KeyCode = 42
        };

        var back = new StepRow(original).ToStep();

        Check("x", back.X == original.X);
        Check("y", back.Y == original.Y);
        Check("delay", back.DelayMs == original.DelayMs);
        Check("muted survives", back.IsMuted == original.IsMuted);
        Check("type", back.Type == original.Type);
        Check("button", back.Button == original.Button);
        Check("click count", back.ClickCount == original.ClickCount);
        Check("hold", back.HoldMs == original.HoldMs);
        Check("scroll", back.ScrollAmount == original.ScrollAmount);
        Check("key code", back.KeyCode == original.KeyCode);

        var row = new StepRow(original);
        Check("IsEnabled is the inverse of IsMuted", row.IsEnabled == false);
        row.IsEnabled = true;
        Check("setting IsEnabled clears IsMuted", row.IsMuted == false);

        var clone = row.Clone();
        Check("clone copies every field",
            clone.X == row.X && clone.Type == row.Type && clone.ScrollAmount == row.ScrollAmount);

        var delayRow = new StepRow(new ClickStep { Type = ActionType.Delay, X = 5, Y = 5 });
        Check("delay row hides its position", delayRow.PositionText == "-", delayRow.PositionText);

        var clickRow = new StepRow(new ClickStep { Type = ActionType.Click, Button = MouseButton.Right });
        Check("click row names its button", clickRow.ButtonText == "Right", clickRow.ButtonText);
        Check("non click row has no button", delayRow.ButtonText == "-", delayRow.ButtonText);
    }

    Section("Preset storage");
    {
        var steps = new List<ClickStep>
        {
            new ClickStep(10, 20, 0),
            new ClickStep { X = 30, Y = 40, DelayMs = 100, Type = ActionType.Scroll, ScrollAmount = 2 }
        };

        PresetService.Save("test_alpha", steps);
        Check("file exists after save", PresetService.Exists("test_alpha"));

        var loaded = PresetService.Load("test_alpha");
        Check("loads the same count", loaded.Count == 2, $"got {loaded.Count}");
        Check("preserves the scroll step",
            loaded[1].Type == ActionType.Scroll && loaded[1].ScrollAmount == 2);

        PresetService.Save("test_beta", steps);
        var names = PresetService.ListPresets();
        Check("lists both", names.Contains("test_alpha") && names.Contains("test_beta"));

        var missing = PresetService.Load("does_not_exist");
        Check("missing preset returns empty, not null", missing != null && missing.Count == 0);

        var exportPath = Path.Combine(Path.GetTempPath(), "clickto_export_test.json");
        PresetService.ExportTo(exportPath, steps);
        var imported = PresetService.ImportFrom(exportPath);
        Check("export then import round trips", imported != null && imported.Count == 2);

        var junkPath = Path.Combine(Path.GetTempPath(), "clickto_junk_test.json");
        File.WriteAllText(junkPath, "this is not json at all");
        Check("junk file returns null rather than throwing", PresetService.ImportFrom(junkPath) == null);
        Check("missing file returns null", PresetService.ImportFrom("/nowhere/nothing.json") == null);

        File.Delete(exportPath);
        File.Delete(junkPath);

        PresetService.Delete("test_beta");
        Check("delete removes it", !PresetService.Exists("test_beta"));
    }

    Section("Preset metadata");
    {
        PresetMeta.Record("test_alpha", 12);
        var info = PresetMeta.Get("test_alpha");
        Check("records the action count", info.ActionCount == 12);
        Check("records a timestamp", info.LastUsed != default);

        Check("not a favorite to begin with", !PresetMeta.IsFavorite("test_alpha"));
        Check("toggle returns the new state", PresetMeta.ToggleFavorite("test_alpha"));
        Check("favorite persisted", PresetMeta.IsFavorite("test_alpha"));
        Check("toggle back", !PresetMeta.ToggleFavorite("test_alpha"));

        PresetMeta.ToggleFavorite("zebra");
        var sorted = PresetMeta.SortForDisplay(new[] { "apple", "zebra", "mango" });
        Check("favorites sort first", sorted[0] == "zebra", string.Join(",", sorted));
        Check("the rest stay alphabetical", sorted[1] == "apple" && sorted[2] == "mango");

        Check("describes a known preset", PresetMeta.Describe("test_alpha").Contains("12 actions"));
        Check("unknown preset describes as empty", PresetMeta.Describe("never_seen") == "");

        PresetMeta.Remove("test_alpha");
        Check("remove clears the record", PresetMeta.Get("test_alpha").ActionCount == 0);
    }

    Section("Run history");
    {
        RunHistory.Clear();
        Check("starts empty", RunHistory.Load().Count == 0);

        var list = RunHistory.Add(new RunRecord
        {
            PresetName = "first", StartedAt = DateTime.Now,
            DurationMs = 1500, Actions = 10, Loops = 2, Outcome = "Finished"
        });
        Check("one entry after adding", list.Count == 1);

        list = RunHistory.Add(new RunRecord { PresetName = "second", StartedAt = DateTime.Now });
        Check("newest is first", list[0].PresetName == "second", list[0].PresetName);

        for (int i = 0; i < 60; i++)
            list = RunHistory.Add(new RunRecord { PresetName = $"run{i}", StartedAt = DateTime.Now });
        Check("trims to the cap", list.Count == 40, $"got {list.Count}");

        var record = new RunRecord { DurationMs = 1500, Actions = 10, StartedAt = DateTime.Now };
        Check("formats seconds", record.DurationText == "1.5 s", record.DurationText);
        Check("says just now", record.WhenText == "just now", record.WhenText);
        Check("summary reads well", record.Summary.Contains("10 actions"), record.Summary);

        RunHistory.Clear();
        Check("clear empties it", RunHistory.Load().Count == 0);
    }

    Section("Settings");
    {
        var settings = new AppSettings
        {
            IsDark = false, IsAdvanced = true, SelectedSpeed = "5x",
            StartDelayMs = 250, NaturalClicks = true, SmoothTravel = true,
            StopAfterActionsEnabled = true, StopAfterActions = 42,
            AdvancedWidth = 1400, StopKeyCode = 999
        };

        SettingsService.Save(settings);
        var back = SettingsService.Load();

        Check("theme", back.IsDark == false);
        Check("mode", back.IsAdvanced == true);
        Check("speed", back.SelectedSpeed == "5x");
        Check("start delay", back.StartDelayMs == 250);
        Check("natural clicks", back.NaturalClicks);
        Check("smooth travel", back.SmoothTravel);
        Check("stop condition", back.StopAfterActionsEnabled && back.StopAfterActions == 42);
        Check("window width", back.AdvancedWidth == 1400);
        Check("hotkey code", back.StopKeyCode == 999);

        var fresh = new AppSettings();
        Check("defaults are safe: natural clicks off", !fresh.NaturalClicks);
        Check("defaults are safe: smooth travel off", !fresh.SmoothTravel);
        Check("defaults are safe: stop conditions off",
            !fresh.StopAfterActionsEnabled && !fresh.StopAfterTimeEnabled);
        Check("defaults are safe: starts in simple mode", !fresh.IsAdvanced);
        Check("defaults: repeat 10 at 1x", fresh.SelectedLoop == "10" && fresh.SelectedSpeed == "1x");
    }

    Section("Humanizer");
    {
        Check("zero percent leaves the delay alone", Humanizer.Delay(100, 0) == 100);
        Check("zero delay stays zero", Humanizer.Delay(0, 50) == 0);

        bool inRange = true, varied = false;
        int firstResult = Humanizer.Delay(1000, 20);
        for (int i = 0; i < 400; i++)
        {
            int result = Humanizer.Delay(1000, 20);
            if (result < 800 || result > 1200) inRange = false;
            if (result != firstResult) varied = true;
        }
        Check("stays within the stated range", inRange);
        Check("actually varies", varied);

        bool capped = true;
        for (int i = 0; i < 200; i++)
        {
            // Percent is clamped at 90, so this must not go negative.
            if (Humanizer.Delay(1000, 500) < 0) capped = false;
        }
        Check("extreme percent cannot go negative", capped);

        Check("zero pixels leaves position alone", Humanizer.Position(500, 0) == 500);

        bool nearby = true;
        for (int i = 0; i < 400; i++)
        {
            double p = Humanizer.Position(500, 3);
            if (Math.Abs(p - 500) > 3.01) nearby = false;
        }
        Check("position stays within the pixel budget", nearby);

        bool realistic = true;
        for (int i = 0; i < 200; i++)
        {
            int hold = Humanizer.Hold(0);
            if (hold < 60 || hold > 130) realistic = false;
        }
        Check("generated hold is a realistic press length", realistic);

        bool keepsIntent = true;
        for (int i = 0; i < 200; i++)
        {
            int hold = Humanizer.Hold(1000);
            if (hold < 850 || hold > 1150) keepsIntent = false;
        }
        Check("a deliberate hold stays roughly that long", keepsIntent);
    }

    Section("Cursor path");
    {
        Check("tiny hops generate no path", CursorPath.Build(100, 100, 101, 101, 200).Count == 0);
        Check("tiny hops need no duration", CursorPath.DurationFor(2, 1.0) == 0);

        var path = CursorPath.Build(0, 0, 800, 600, 400);
        Check("generates points", path.Count > 1, $"got {path.Count}");

        var last = path[^1];
        Check("ends on the target",
            Math.Abs(last.X - 800) < 0.01 && Math.Abs(last.Y - 600) < 0.01,
            $"({last.X:0.##}, {last.Y:0.##})");

        Check("longer moves take longer",
            CursorPath.DurationFor(1000, 1.0) > CursorPath.DurationFor(50, 1.0));
        Check("speed shortens the travel",
            CursorPath.DurationFor(500, 4.0) < CursorPath.DurationFor(500, 1.0));

        bool bounded = true;
        for (int i = 0; i < 100; i++)
        {
            int d = CursorPath.DurationFor(5000, 0.1);
            if (d < 30 || d > 1200) bounded = false;
        }
        Check("duration stays within its clamp", bounded);

        // A curve should not be a straight line.
        var curve = CursorPath.Build(0, 0, 1000, 0, 400);
        bool bowed = curve.Any(pt => Math.Abs(pt.Y) > 1);
        Check("the path bows rather than running straight", bowed);

        Check("step delay is sane", CursorPath.StepDelayMs > 0 && CursorPath.StepDelayMs <= 20);
    }

    Section("Cleanup");
    {
        PresetService.Delete("test_alpha");
        Check("test presets removed", !PresetService.Exists("test_alpha"));
    }
}
finally
{
    if (Directory.Exists(live)) Directory.Delete(live, true);
    if (Directory.Exists(stash)) Directory.Move(stash, live);
}

Console.WriteLine($"\n{passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;
