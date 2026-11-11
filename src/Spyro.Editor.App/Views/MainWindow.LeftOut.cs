using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.App.Views;

/// <summary>What Fix It For Me left out of the project's games (<see cref="ExportLeftOutSettings"/>): Create BIN makes
/// the game as if a left-out part were not in the project, says so every time, and More › Left Out of the Game… puts
/// parts back. Nothing is deleted from the project.</summary>
public sealed partial class MainWindow
{
    private List<ExportLeftOutSettings.Item>? _leftOut;
    private string _leftOutRoot = "";

    /// <summary>A part of the project a game can be made without: its id, what to call it, and the names a refusal
    /// would use for it (a level's name).</summary>
    private sealed record ExportPart(string Id, string Label, IReadOnlyList<string> Names)
    {
        /// <summary>For one of the game's own levels: the kinds of saved change it has (to leave out no more than needed).</summary>
        public IReadOnlyList<string> Kinds { get; init; } = [];
        /// <summary>How much work is in the part (the size of its saved files): when a stop names several parts and
        /// leaving out any one of them gets past it, the one with the least work in it goes.</summary>
        public long Work { get; init; }
    }

    private List<ExportLeftOutSettings.Item> LeftOutItems()
    {
        string root = _workspace.RootPath;
        if (_leftOut == null || !string.Equals(_leftOutRoot, root, StringComparison.Ordinal))
        {
            _leftOut = [.. ExportLeftOutSettings.Load(root, AppReleaseIdentity.DiagnosticVersion)];
            _leftOutRoot = root;
        }
        return _leftOut;
    }

    internal IReadOnlyList<string> LeftOutForTesting => [.. LeftOutItems().Select(i => i.Part)];
    internal void PutBackForTesting(string part) => PutBack(part);
    internal void LeaveOutForTesting(string part, string why) => LeaveOut(part, why);

    private bool IsLeftOut(string part) => LeftOutItems().Any(i => i.Part == part);
    /// <summary>The kinds of object added to a level that are left out by themselves (null: none).</summary>
    private IReadOnlySet<string>? LeftOutAdded(string levelKey)
    {
        string prefix = ExportParts.AddedPrefix(levelKey);
        var kinds = LeftOutItems().Where(i => i.Part.StartsWith(prefix, StringComparison.Ordinal)).Select(i => i.Part[prefix.Length..]).ToHashSet(StringComparer.Ordinal);
        return kinds.Count == 0 ? null : kinds;
    }
    /// <summary>Whether the window that lists what was left out opens by itself when Fix It For Me has left something out.</summary>
    internal bool ShowLeftOutAfterFixIt { get; set; } = true;
    /// <summary>One kind of saved change to one of the game's own levels is left out (by itself or with the whole level).</summary>
    private bool IsLeftOut(string levelKey, string kind) => IsLeftOut(ExportParts.Level(levelKey)) || IsLeftOut(ExportParts.Level(levelKey, kind));

    private void LeaveOut(string part, string why)
    {
        var items = LeftOutItems();
        items.RemoveAll(i => i.Part == part);
        items.Add(new(part, why, DateTimeOffset.Now));
        ExportLeftOutSettings.Save(_leftOutRoot, AppReleaseIdentity.DiagnosticVersion, items);
    }

    private void PutBack(string part)
    {
        var items = LeftOutItems();
        if (items.RemoveAll(i => i.Part == part) > 0)
            ExportLeftOutSettings.Save(_leftOutRoot, AppReleaseIdentity.DiagnosticVersion, items);
    }

    /// <summary>The game's own levels whose saved changes go into the game: every level with saved changes, less what
    /// was left out (a level with nothing left is not a target).</summary>
    private IReadOnlyList<EditedLevelExportTarget> ExportTargets()
    {
        var all = FindEditedLevelExportTargets();
        if (LeftOutItems().Count == 0) return all;
        List<EditedLevelExportTarget> kept = [];
        foreach (var t in all)
        {
            string key = t.Level.Key;
            bool objects = IsLeftOut(key, ExportParts.Objects), terrain = IsLeftOut(key, ExportParts.Terrain);
            var left = t with
            {
                HasObjectEdits = t.HasObjectEdits && !objects,
                HasTerrainEdits = t.HasTerrainEdits && !terrain,
                HasCustomTerrainTextures = t.HasCustomTerrainTextures && !terrain,
                HasNativeTerrainTextureRelocations = t.HasNativeTerrainTextureRelocations && !terrain,
                HasLevelTextEdit = t.HasLevelTextEdit && !IsLeftOut(key, ExportParts.Name),
                HasLevelMusicEdit = t.HasLevelMusicEdit && !IsLeftOut(key, ExportParts.Music),
                HasSkyboxEdit = t.HasSkyboxEdit && !IsLeftOut(key, ExportParts.Sky),
                HasFreeFlightSelection = t.HasFreeFlightSelection && !IsLeftOut(ExportParts.FlyAnywhere),
                HasPortalRoutes = t.HasPortalRoutes && !IsLeftOut(ExportParts.Doorways),
                HasCrashPlayer = t.HasCrashPlayer && !objects && !IsLeftOut(ExportParts.PlayAsCrash),
                ObjectsLeftOut = objects,
            };
            if (left.HasObjectEdits || left.HasTerrainEdits || left.HasCustomTerrainTextures || left.HasNativeTerrainTextureRelocations ||
                left.HasLevelTextEdit || left.HasLevelMusicEdit || left.HasSkyboxEdit || left.HasFreeFlightSelection || left.HasPortalRoutes || left.HasCrashPlayer)
                kept.Add(left);
        }
        return kept;
    }

    /// <summary>The levels built in the game's places (and the new level) that go into the game.</summary>
    private IReadOnlyList<SceneDocument> ExportScenes(IReadOnlyList<SceneDocument> saved) =>
        LeftOutItems().Count == 0 ? saved : [.. saved.Where(s => !IsLeftOut(ExportParts.Built(s.ResolveTarget(_retailCatalog).Key)))];

    /// <summary>Fly Anywhere as Create BIN uses it (left out: as in a project that never chose it).</summary>
    private NativeFreeFlightProjectExportIntent ExportFlightIntent(string root)
    {
        var intent = NativeFreeFlightProjectExportIntent.Capture(root);
        return IsLeftOut(ExportParts.FlyAnywhere) ? intent.WithoutFlight() : intent;
    }

    /// <summary>The doorways' destinations as Create BIN uses them (left out: the game's own).</summary>
    private PortalRouteSnapshot ExportPortalRoutes(string root)
    {
        var snapshot = PortalRouteStore.Capture(root);
        return IsLeftOut(ExportParts.Doorways) ? snapshot with { Document = PortalRouteDocument.Empty } : snapshot;
    }

    /// <summary>Everything in the project a game could be made without and that is still in it, likeliest culprits of an
    /// unexplained stop first: levels you built, the new world, your changes to the game's levels, then game settings.</summary>
    private List<ExportPart> ProjectParts()
    {
        string root = _workspace.RootPath;
        List<ExportPart> parts = [];
        void Add(ExportPart part) { if (!IsLeftOut(part.Id)) parts.Add(part); }
        void Try(Action read) { try { read(); } catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException) { } }
        Try(() =>
        {
            foreach (var scene in SceneDocumentStore.ReadAll(root, _retailCatalog))
            {
                var target = scene.ResolveTarget(_retailCatalog);
                string? slot = scene.TargetBinding == null ? null : _retailCatalog.FindByKey(scene.TargetBinding.LevelKey)?.DisplayName ?? scene.TargetBinding.LevelKey;
                Add(new(ExportParts.Built(target.Key), slot == null ? $"{scene.Name} (your new level)" : $"{scene.Name} (the level you built in {slot}'s place; the game keeps the original {slot})",
                    slot == null ? [scene.Name, "the new level"] : [scene.Name, slot]) { Work = Size(SceneDocumentStore.PathForTarget(root, target.Key)) });
            }
        });
        Try(() =>
        {
            if (!ProjectHasNewWorldLevels() || NewWorldDocument.Read(root) is not { } world) return;
            var names = SceneDocumentStore.ReadWorld(root, _retailCatalog).Select(s => s.Name).Prepend(world.Name).ToArray();
            Add(new(ExportParts.World, $"your new world {world.Name}", names) { Work = world.LevelIds.Sum(id => Size(SceneDocumentStore.PathForTarget(root, NewWorldDocument.KeyFor(id)))) });
        });
        Try(() =>
        {
            foreach (var t in FindEditedLevelExportTargets())
            {
                string key = t.Level.Key;
                List<string> kinds = [];
                if ((t.HasObjectEdits || t.HasCrashPlayer) && !IsLeftOut(key, ExportParts.Objects)) kinds.Add(ExportParts.Objects);
                if ((t.HasTerrainEdits || t.HasCustomTerrainTextures || t.HasNativeTerrainTextureRelocations) && !IsLeftOut(key, ExportParts.Terrain)) kinds.Add(ExportParts.Terrain);
                if (t.HasSkyboxEdit && !IsLeftOut(key, ExportParts.Sky)) kinds.Add(ExportParts.Sky);
                if (t.HasLevelMusicEdit && !IsLeftOut(key, ExportParts.Music)) kinds.Add(ExportParts.Music);
                if (t.HasLevelTextEdit && !IsLeftOut(key, ExportParts.Name)) kinds.Add(ExportParts.Name);
                if (kinds.Count > 0)
                    Add(new(ExportParts.Level(key), $"your changes to {t.Level.DisplayName}", [t.Level.DisplayName])
                    {
                        Kinds = kinds,
                        Work = Directory.EnumerateFiles(root, key + "-*.json").Sum(Size),
                    });
            }
        });
        Try(() => { if (SpyroMovesSettings.LoadHover(root)) Add(new(ExportParts.Hover, "Hover (Colours tab)", ["Hover"])); });
        Try(() => { if (CreatureColourSettings.Load(root).Count > 0) Add(new(ExportParts.CreatureColours, "this level's colours (Colours tab)", ["creatures' colours", "Creatures' colours"])); });
        Try(() => { var book = SpyroColourSettings.LoadAll(root); if (book.Whole.Count > 0 || book.Levels.Count > 0) Add(new(ExportParts.SpyroColours, "Spyro's colours (Colours tab)", ["Spyro's colours"])); });
        Try(() => { if (EggThiefColourSettings.Load(root) != null) Add(new(ExportParts.EggThieves, "the egg thieves' colour (Colours tab)", ["egg thieves' colour"])); });
        Try(() => { if (NativeFreeFlightProjectExportIntent.Capture(root).RequiresRebuild) Add(new(ExportParts.FlyAnywhere, "Fly Anywhere (Colours tab)", ["Fly Anywhere"])); });
        Try(() => { if (PortalRouteStore.Capture(root).Document.HasEdits) Add(new(ExportParts.Doorways, "the doorways you sent to other levels", ["portal", "Portal", "doorway"])); });
        Try(() => { if (CrashPlayerSettings.Load(root).Count > 0) Add(new(ExportParts.PlayAsCrash, "Play as Crash", ["Crash"])); });
        return parts;
    }

    private static long Size(string path) { try { return File.Exists(path) ? new FileInfo(path).Length : 0; } catch (IOException) { return 0; } }

    /// <summary>What to call a part (also one that is no longer in the project).</summary>
    private string PartLabel(string id)
    {
        if (id == ExportParts.SkippedChanges) return "saved changes the game cannot take (Create BIN names them each time)";
        string root = _workspace.RootPath;
        try
        {
            if (id.StartsWith("built:", StringComparison.Ordinal))
            {
                string key = id["built:".Length..];
                var scene = SceneDocumentStore.ReadAll(root, _retailCatalog).FirstOrDefault(s => string.Equals(s.ResolveTarget(_retailCatalog).Key, key, StringComparison.OrdinalIgnoreCase));
                string? slot = scene?.TargetBinding == null ? null : _retailCatalog.FindByKey(scene.TargetBinding.LevelKey)?.DisplayName;
                return scene == null ? "a level you built (" + key + ")" : slot == null ? $"{scene.Name} (your new level)" : $"{scene.Name} (the level you built in {slot}'s place; the game keeps the original {slot})";
            }
            if (id == ExportParts.World) return NewWorldDocument.Read(root) is { } world ? $"your new world {world.Name}" : "your new world";
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException) { }
        if (id.StartsWith("level:", StringComparison.Ordinal))
        {
            string[] bits = id.Split(':');
            string name = _retailCatalog.FindByKey(bits[1])?.DisplayName ?? bits[1];
            if (bits.Length >= 4 && bits[2] == "added")
            {
                string kind = id[ExportParts.AddedPrefix(bits[1]).Length..];
                try
                {
                    var found = Spyro.Editor.Core.Universal.UniversalPlacementReader.ReadAddedKinds(Path.Combine(root, bits[1] + "-native-edits.json")).FirstOrDefault(k => k.Kind == kind);
                    if (found.Kind != null) return (found.Count > 1 ? $"{found.Count} × {found.Label}" : found.Label) + $" (added to {name})";
                }
                catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException) { }
                return $"an object you added to {name} (it is no longer there)";
            }
            return bits.Length < 3 ? $"your changes to {name}" : bits[2] switch
            {
                ExportParts.Objects => $"your object changes in {name}",
                ExportParts.Terrain => $"your ground and texture changes in {name}",
                ExportParts.Sky => $"{name}'s new sky",
                ExportParts.Music => $"{name}'s new music",
                ExportParts.Name => $"{name}'s new name",
                _ => $"your changes to {name}",
            };
        }
        return id switch
        {
            ExportParts.Hover => "Hover (Colours tab)",
            ExportParts.SpyroColours => "Spyro's colours (Colours tab)",
            ExportParts.CreatureColours => "this level's colours (Colours tab)",
            ExportParts.EggThieves => "the egg thieves' colour (Colours tab)",
            ExportParts.FlyAnywhere => "Fly Anywhere (Colours tab)",
            ExportParts.Doorways => "the doorways you sent to other levels",
            ExportParts.PlayAsCrash => "Play as Crash",
            _ => id,
        };
    }

    /// <summary>The sentence a finished Create BIN adds while parts are left out (empty: nothing is).</summary>
    private string LeftOutNote()
    {
        var items = LeftOutItems();
        if (items.Count == 0) return "";
        return " LEFT OUT of this game by Fix It For Me (still in your project): " + string.Join("; ", items.Select(i => PartLabel(i.Part))) +
               ". More › Left Out of the Game… says why and puts them back.";
    }

    /// <summary>More › Left Out of the Game…: each part Fix It For Me left out, why, and Put Back.</summary>
    private Task ShowLeftOutAsync() => ShowLeftOutCoreAsync(afterFixIt: false);

    /// <summary>The same window, opened by Fix It For Me when it has just made a game without something: what it left
    /// out must not be only a line at the bottom of the window.</summary>
    private async Task ShowLeftOutCoreAsync(bool afterFixIt)
    {
        Window dialog = new()
        {
            Title = "Left Out of the Game", Name = "LeftOutWindow", Width = 620, MinWidth = 480, MaxHeight = 620,
            SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brushes.White,
        };
        StackPanel body = new() { Margin = new Thickness(22), Spacing = 12 };
        void Rebuild()
        {
            body.Children.Clear();
            var items = LeftOutItems().ToArray();
            body.Children.Add(new TextBlock
            {
                Name = "LeftOutHeading", Text = afterFixIt && items.Length > 0 ? "Your game was made, but with something left out" : "Left out of the game",
                FontSize = 20, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(ModernInk)
            });
            body.Children.Add(new TextBlock
            {
                Name = "LeftOutIntro", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(ModernMutedInk), LineHeight = 20,
                Text = items.Length == 0
                    ? "Nothing is left out: Create BIN makes the game with everything in your project."
                    : afterFixIt
                    ? "Create BIN stopped on something the editor could not repair, so Fix It For Me made the game without what is listed here. " +
                      "Nothing was deleted: it is all still in your project and in the editor, only not in the game. " +
                      "Put Back includes a part again (then press Create BIN); if it still stops Create BIN, change or remove it in the editor first."
                    : "When Create BIN stopped on something the editor could not repair, Fix It For Me made the game without the part that stopped it. " +
                      "These parts are still in your project; Create BIN leaves them out until you put them back. If a part still stops Create BIN, Fix It For Me can leave it out again.",
            });
            foreach (var item in items)
            {
                Grid row = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
                StackPanel text = new() { Spacing = 2 };
                text.Children.Add(new TextBlock { Text = Capitalized(PartLabel(item.Part)), FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(ModernInk) });
                text.Children.Add(new TextBlock
                {
                    Text = "Because Create BIN said: " + item.Why, TextWrapping = TextWrapping.Wrap, FontSize = 12,
                    Foreground = new SolidColorBrush(ModernMutedInk), MaxLines = 5, TextTrimming = TextTrimming.CharacterEllipsis,
                });
                row.Children.Add(text);
                Button back = NewButton("Put Back", () => { PutBack(item.Part); Rebuild(); _statusText.Text = $"Put back: {PartLabel(item.Part)}. Create BIN includes it again."; });
                back.Name = "LeftOutPutBack"; back.VerticalAlignment = VerticalAlignment.Top;
                Grid.SetColumn(back, 1); row.Children.Add(back);
                body.Children.Add(new Border { BorderBrush = new SolidColorBrush(ModernLine), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 10, 0, 0), Child = row });
            }
            StackPanel actions = new() { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
            if (items.Length > 1)
            {
                Button all = NewButton("Put Everything Back", () =>
                {
                    foreach (var item in items) PutBack(item.Part);
                    Rebuild(); _statusText.Text = "Put everything back. Create BIN makes the game with everything in your project again.";
                });
                all.Name = "LeftOutPutAllBack"; actions.Children.Add(all);
            }
            Button close = NewButton("Close", () => dialog.Close()); close.Name = "LeftOutClose"; actions.Children.Add(close);
            body.Children.Add(actions);
        }
        Rebuild();
        dialog.Content = new ScrollViewer { Content = body };
        await dialog.ShowDialog(this);
    }

    private static string Capitalized(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
