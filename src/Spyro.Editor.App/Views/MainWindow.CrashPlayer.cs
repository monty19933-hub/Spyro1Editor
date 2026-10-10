using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private readonly Dictionary<string, CheckBox> _crashPlayerLevelBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly TextBlock _crashPlayerSummary = new() { Name = "CrashPlayerSummary", TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private bool _syncingCrashPlayer;

    /// <summary>Play as Crash: one box per level Crash can play (every normal level but the flight levels), grouped by
    /// home world; each tick is saved at once.</summary>
    private Control BuildCrashPlayerSettingsPanel()
    {
        StackPanel content = new() { Name = "CrashPlayerPanel", Spacing = 6, Margin = new Avalonia.Thickness(2, 4, 2, 2) };
        TextBlock Note(string text) => new()
        {
            Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = new SolidColorBrush(ModernMutedInk)
        };
        content.Children.Add(Note("Crash Bandicoot takes Spyro's place in the levels you tick: his model, animations and " +
            "sounds come from your Crash Bandicoot: Warped disc (choose it with \"Crash disc…\" in the Add Object browser). " +
            "Spyro arrives as himself and turns into Crash with a puff of stars."));
        content.Children.Add(Note("X jump, X again in the air: double jump · Square spin; in the air it keeps him up " +
            "(tornado spin); held: dash · Circle: crouch, slide when running, belly flop in the air · crouch + X: high jump · " +
            "land on enemies to bounce off them."));
        content.Children.Add(Note("Each level needs about 70 KB of free game memory. Several of the game's own levels are too " +
            "full; Create BIN names any that are. Levels you clear with Start Blank or build with New Level have room."));
        content.Children.Add(_crashPlayerSummary);
        var worlds = _retailCatalog.Levels
            .Where(level => UniversalCrashPlayer.IsPlayableLevel(level.LevelId))
            .GroupBy(level => level.LevelId / 10)
            .OrderBy(group => group.Key);
        foreach (var world in worlds)
        {
            LevelDefinition? home = world.FirstOrDefault(level => level.LevelId % 10 == 0);
            content.Children.Add(new TextBlock
            {
                Text = home?.DisplayName ?? $"World {world.Key}",
                FontWeight = FontWeight.SemiBold,
                FontSize = 12,
                Margin = new Avalonia.Thickness(0, 6, 0, 0)
            });
            WrapPanel row = new() { Orientation = Avalonia.Layout.Orientation.Horizontal };
            foreach (LevelDefinition level in world.OrderBy(level => level.LevelId))
            {
                string key = level.Key;
                CheckBox box = new() { Name = "CrashPlayerLevel" + level.LevelId, Content = level.DisplayName, Margin = new Avalonia.Thickness(0, 0, 14, 2), MinWidth = 150 };
                ToolTip.SetTip(box, $"Play {level.DisplayName} as Crash in the game you export.");
                box.PropertyChanged += (_, change) =>
                {
                    if (_syncingCrashPlayer || change.Property != ToggleButton.IsCheckedProperty) return;
                    try { SaveCrashPlayerLevel(key, box.IsChecked == true); }
                    catch (Exception error)
                    {
                        EditorDiagnostics.RecordException("saving the Play as Crash choice", error);
                        _statusText.Text = "Could not save the Play as Crash choice: " + error.Message;
                    }
                    RefreshCrashPlayerSettings();
                };
                _crashPlayerLevelBoxes[key] = box;
                row.Children.Add(box);
            }
            content.Children.Add(row);
        }
        RefreshCrashPlayerSettings();
        return content;
    }

    private void SaveCrashPlayerLevel(string key, bool enabled)
    {
        if (_createBinBusy || _workspaceTransitionBusy) throw new InvalidOperationException("Wait for the current task to finish.");
        var keys = CrashPlayerSettings.Load(_workspace.RootPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (enabled) keys.Add(key); else keys.Remove(key);
        CrashPlayerSettings.Save(_workspace.RootPath, keys);
        _statusText.Text = enabled
            ? $"Crash will play {_retailCatalog.FindByKey(key)?.DisplayName ?? key} in the game you create next."
            : $"Spyro plays {_retailCatalog.FindByKey(key)?.DisplayName ?? key} again.";
    }

    /// <summary>Shows the project's saved choice (on project and level changes, and after each tick).</summary>
    private void RefreshCrashPlayerSettings()
    {
        IReadOnlySet<string> keys;
        string error = "";
        try { keys = CrashPlayerSettings.Load(_workspace.RootPath); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { keys = new HashSet<string>(); error = e.Message; }
        _syncingCrashPlayer = true;
        try
        {
            foreach ((string key, CheckBox box) in _crashPlayerLevelBoxes)
            {
                box.IsChecked = keys.Contains(key);
                box.IsEnabled = !_createBinBusy;
            }
        }
        finally { _syncingCrashPlayer = false; }
        var names = _retailCatalog.Levels.Where(l => keys.Contains(l.Key)).OrderBy(l => l.LevelId).Select(l => l.DisplayName).ToArray();
        _crashPlayerSummary.Text = error.Length > 0 ? "The saved choice could not be read: " + error
            : names.Length == 0 ? "Spyro plays every level."
            : "Crash plays " + (names.Length == 1 ? names[0] : string.Join(", ", names[..^1]) + " and " + names[^1]) + ".";
    }
}
