using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private string PreservedResearchExportNote() =>
        _releaseMode && (Id65BlankLabHasCanonicalAuthoringDocument() || Id65BlankLabHasAnyAuthoredArtifacts())
            ? " Older experimental drafts are preserved separately and are not included."
            : "";

    private readonly Dictionary<string, LevelOwnRows> _ownCompletionRows = new(StringComparer.OrdinalIgnoreCase);
    private bool _completionCatalogRequested;

    /// <summary>The line under the level's treasure: how many of its own dragons and egg thieves (which finishing the game
    /// needs) the level has, and what to add back. Hidden for levels with neither, and until the source disc is read.</summary>
    private void RefreshCompletionLine()
    {
        string? text = null, hint = null; bool complete = true;
        string? key = _currentLevel == null ? null : _freshSceneDocument?.TargetBinding?.LevelKey ?? _currentLevel.Key;
        if (key != null && _retailCatalog.FindByKey(key) is { SourceWadEntry: > 0 } slot)
        {
            if (!_ownCompletionRows.TryGetValue(key, out var own) && _universalCatalog is { } ready)
            {
                try { own = _ownCompletionRows[key] = LevelCompletionCheck.OriginalRows(ready.Library.Get(key).Data); }
                catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException) { own = _ownCompletionRows[key] = LevelOwnRows.None; }
            }
            if (own != null && _universalCatalog is { } library)
            {
                bool cleared = _freshSceneDocument != null;
                var gap = LevelCompletionCheck.Check(own, key, cleared,
                    _currentMobys.Where(m => !m.IsAdded && m.IsRemoved && m.TrueIndex >= 0).Select(m => m.TrueIndex),
                    _currentMobys.Where(m => m.IsAdded && !m.IsRemoved).Select(m => (m.CrossLevelSourceLevelKey, m.CrossLevelSourceTrueIndex)),
                    LevelCompletionCheck.SourceClass(library.Library));
                text = LevelCompletionCheck.Summary(gap);
                hint = LevelCompletionCheck.Describe(gap, slot.DisplayName, cleared ? _freshSceneDocument!.Name : null);
                complete = hint == null;
            }
            else if (!_completionCatalogRequested)
            {
                // The Add Object library reads the original disc in the background; the line appears once it has.
                _completionCatalogRequested = true;
                EnsureUniversalCatalogAsync().ContinueWith(t =>
                { if (t.IsCompletedSuccessfully) Avalonia.Threading.Dispatcher.UIThread.Post(RefreshCompletionLine); }, TaskScheduler.Default);
            }
        }
        _completionText.IsVisible = text != null;
        _completionText.Text = text == null ? "" : hint == null ? text : text + "\n" + hint;
        _completionText.Foreground = new SolidColorBrush(complete ? Color.FromRgb(46, 125, 72) : Color.FromRgb(168, 98, 0));
    }

    /// <summary>For every level the game will contain changed: its own dragons and egg thieves that are no longer in it
    /// (<see cref="LevelCompletionCheck"/>), and a built level's faces under the lowest kill height Create BIN can give its
    /// slot (<see cref="Spyro.Editor.Core.Exporting.NativeKillHeights.Warning"/>). Empty when nothing is missing or the
    /// source disc can't be read.</summary>
    private List<string> CompletionGapLines(IReadOnlyList<SceneDocument> scenes, IReadOnlyList<EditedLevelExportTarget> targets)
    {
        List<string> lines = [];
        try
        {
            var library = _universalCatalog?.Library ?? new UniversalSourceLibrary(ResolveUniversalSourceDisc(), _retailCatalog);
            HashSet<string> built = new(StringComparer.OrdinalIgnoreCase);
            foreach (var scene in scenes)
            {
                if (scene.TargetBinding?.LevelKey is not { } key || _retailCatalog.FindByKey(key) is not { SourceWadEntry: > 0 } slot) continue;
                built.Add(key);
                if (Spyro.Editor.Core.Exporting.NativeKillHeights.Warning(library.Disc.Exe, slot.LevelId, scene) is { } fall)
                    lines.Add($"{scene.Name} ({slot.DisplayName}): {fall}");
                var added = scene.Objects.Where(o => o.Value.IsAdded && !o.Value.IsRemoved)
                    .Select(o => (o.Value.CrossLevelSourceLevelKey, o.Value.CrossLevelSourceTrueIndex));
                if (LevelCompletionCheck.Describe(LevelCompletionCheck.Check(library, key, true, [], added), slot.DisplayName, scene.Name) is { } line)
                    lines.Add(line);
            }
            foreach (var target in targets.Where(t => !built.Contains(t.Level.Key) && t.Level.SourceWadEntry > 0))
            {
                var (removed, added) = LevelCompletionCheck.ReadNativeEdits(Path.Combine(_workspace.RootPath, $"{target.Level.Key}-native-edits.json"));
                if (LevelCompletionCheck.Describe(LevelCompletionCheck.Check(library, target.Level.Key, false, removed, added), target.Level.DisplayName) is { } line)
                    lines.Add(line);
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or FileNotFoundException or InvalidOperationException or JsonException or KeyNotFoundException or ArgumentException)
        { lines.Add("(The dragon, egg and Return Home check could not run: " + e.Message + ")"); }
        return lines;
    }

    /// <summary>What Create BIN says when the game has the project's new level (the game's unused slot, id 65) and no
    /// doorway leads to it: Spyro only gets there when New Game starts in it. Empty when it has a doorway, is where New
    /// Game starts (<paramref name="startLevelId"/>; null: not chosen yet), or is not in the game.</summary>
    private static string NewLevelWayInNote(IReadOnlyList<SceneDocument> scenes, PortalRouteDocument routes, int? startLevelId)
    {
        var own = scenes.FirstOrDefault(s => s.TargetBinding == null && s.WorldLevelId == null);
        if (own == null || startLevelId == 65 || routes.Destinations.Any(d => d.DestinationLevelId == 65)) return "";
        return $" {own.Name} has no doorway leading to it yet: Spyro only gets there when New Game starts in it" +
               (startLevelId == null ? " (choose it under New Game starts in)" : " (choose it in the Create BIN window)") +
               ", or once a homeworld's portal is sent to it (open the homeworld, then Game Settings › Portals & Return Home).";
    }

    private async Task<int?> ShowAuthoredProjectExportScopeAsync(IReadOnlyList<SceneDocument> scenes, int currentLevelId)
    {
        var targets = ExportTargets();
        // Fly Anywhere touches every level; listing all of them as "your changes" hides the ones that are.
        var listed = _releaseMode ? targets.Where(HasOrdinarySavedEdits).ToList() : targets.ToList();
        var flightIntent = _releaseMode ? ExportFlightIntent(_workspace.RootPath) : null;
        string flightLine = flightIntent is not { Enabled: true } ? ""
            : flightIntent.WholeGame ? "\nFly Anywhere is on: Spyro can fly in every level of this game."
            : "\nFly Anywhere is on in " + DescribeFlightLevels(flightIntent.EnabledLevelIds) + ".";
        string existing = (listed.Count == 0 ? "No saved changes to existing levels." :
            (_releaseMode ? "Includes your saved changes to: " : "Saved changes: ") + string.Join(", ", listed.Select(t => t.Level.DisplayName)) + ".") +
            flightLine;
        var choices = new[] { (Id:10, Name:scenes.FirstOrDefault(s=>s.TargetBinding?.LevelId==10)?.Name??"Artisans") }
            .Concat(scenes.Where(s=>s.ResolveTarget(_retailCatalog).LevelId!=10).Select(s=>(Id:s.ResolveTarget(_retailCatalog).LevelId,Name:s.Name))).ToArray();
        // Fix It For Me is making the game: New Game starts where it was chosen last time (Artisans if that level is out).
        if (_quietCreate)
            return choices.Any(c => c.Id == _lastExportStartLevel) ? _lastExportStartLevel : choices[0].Id;
        var start = new ComboBox
        {
            Name = "ExportStartLevel", HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = choices.Select(c=>c.Name).ToArray(),
            SelectedIndex = Math.Max(0,Array.FindIndex(choices,c=>c.Id==currentLevelId))
        };
        var content = new StackPanel { Spacing = 12, Margin = new Thickness(22) };
        content.Children.Add(new TextBlock
        {
            Text = _releaseMode ? "Create your game" : "Create a playable copy", FontSize = 20, FontWeight = FontWeight.SemiBold
        });
        content.Children.Add(new TextBlock
        {
            Name = "ExportIncludedLevels", TextWrapping = TextWrapping.Wrap,
            Text = existing + (_releaseMode ? "\nLevels you built: " : "\nRebuilt levels: ") + string.Join(", ",scenes.Select(s=>s.Name)) + "." +
                (scenes.Any(s=>s.WorldLevelId!=null)
                    ? $"\nYour new world ({string.Join(", ",scenes.Where(s=>s.WorldLevelId!=null).OrderBy(s=>s.WorldLevelId).Select(s=>s.Name))}) is part of this game. " +
                      "Its homeworld's doorways lead to its levels; choose its homeworld for New Game to start there."
                    : "")
        });
        content.Children.Add(new TextBlock { Text = "New Game starts in" });
        content.Children.Add(start);
        content.Children.Add(new TextBlock
        {
            Text = _releaseMode
                ? "Choose New Game in the exported game to play from here. A saved game from before keeps what it had already collected.\n" +
                  "The BIN and CUE are written to this project's output folder. Your original disc is not changed."
                : "Start a New Game to play the rebuilt layout from its safe player start. Loading an existing game keeps its previous collected state.",
            TextWrapping = TextWrapping.Wrap
        });
        if (CompletionGapLines(scenes, targets) is { Count: > 0 } gaps)
            content.Children.Add(new TextBlock
            {
                Name = "ExportCompletionGaps", TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(176, 104, 16)),
                Text = "Before you play:\n" + string.Join("\n", gaps.Select(g => "• " + g)) +
                    "\nThe game can still be played; missing dragons or eggs just can't be collected, and a level without Return Home is left from the pause menu."
            });
        if (NewLevelWayInNote(scenes, ExportPortalRoutes(_workspace.RootPath).Document, null) is { Length: > 0 } wayIn)
            content.Children.Add(new TextBlock
            {
                Name = "ExportNewLevelWayIn", TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(176, 104, 16)), Text = wayIn.Trim()
            });
        if (LeftOutItems().Count > 0)
            content.Children.Add(new TextBlock
            {
                Name = "ExportLeftOut", TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(176, 104, 16)),
                Text = "Left out of this game by Fix It For Me (still in your project): " + string.Join("; ", LeftOutItems().Select(i => PartLabel(i.Part))) +
                    ".\nMore › Left Out of the Game… says why and puts them back."
            });
        string research = PreservedResearchExportNote().Trim();
        if (research.Length > 0)
            content.Children.Add(new TextBlock { Name = "ExportPreservedDrafts", Text = research, TextWrapping = TextWrapping.Wrap });
        var dialog = new Window
        {
            Title = "Create BIN/CUE", Name = "ProjectExportScope", Width = 520,
            SizeToContent = SizeToContent.Height, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = content
        };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", Name = "ExportCancel" };
        var create = new Button { Content = "Create BIN/CUE", Name = "ExportCreate" };
        StyleModernPrimaryButton(create, ModernBlue, 132);
        cancel.Click += (_, _) => dialog.Close(null);
        create.Click += (_, _) => dialog.Close((int?)choices[start.SelectedIndex].Id);
        actions.Children.Add(cancel); actions.Children.Add(create); content.Children.Add(actions);
        int? chosen = await dialog.ShowDialog<int?>(this);
        if (chosen != null) _lastExportStartLevel = chosen;
        return chosen;
    }
}
