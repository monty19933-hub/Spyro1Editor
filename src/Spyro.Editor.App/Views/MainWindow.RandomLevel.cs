using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Generation;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

/// <summary>File ▾ › Random Level…: a level made from a seed and a few choices (<see cref="RandomLevelGenerator"/>): a
/// valley, islands, sky islands, caverns, a canyon trail or a castle, every jump, glide and climb inside Spyro's moves,
/// textured from the game's own levels or with painted or the user's own pictures, under a painted or borrowed sky. It
/// goes in place of the open level (with a recovery copy), in the project's New Level slot, or in a level of the
/// project's new world, ready for objects.</summary>
public sealed partial class MainWindow
{
    internal enum RandomLevelPlace { OpenLevel, NewLevel, World }
    internal sealed record RandomLevelChoice(RandomLevelOptions Options, RandomLevelPlace Place, int WorldLevelId = 0, bool FillIn = false);
    /// <summary>Answers the Random Level dialog in tests (null: cancel).</summary>
    internal Func<RandomLevelChoice?>? RandomLevelChoiceForTesting { get; set; }
    /// <summary>The last random level made (tests read it).</summary>
    internal RandomLevelResult? LastRandomLevelForTesting { get; private set; }
    private List<(int Wad, int Id)>? _pictureCarriers;
    private string? _pictureCarriersDisc;

    private static readonly (LevelStyle? Style, string Label)[] RandomLevelKinds =
    [
        (null, "Surprise me"), (LevelStyle.Valley, "Clearings among the hills (like Artisans)"), (LevelStyle.Islands, "Islands in the water"),
        (LevelStyle.SkyIslands, "Islands in the sky (like Lofty Castle)"), (LevelStyle.Caverns, "Caverns and tunnels"),
        (LevelStyle.Canyon, "Canyon (like Dry Canyon)"), (LevelStyle.Castle, "Castle courtyards"),
    ];
    /// <summary>How a level is laid out (<see cref="RandomLevelOptions.Layout"/>); the first leaves it to the seed.</summary>
    private static readonly (string? Layout, string Label)[] RandomLevelLayouts =
    [
        (null, "Surprise me"), ("journey", "A way from room to room, with an end"), ("loop", "A ring that comes back over its start"),
        ("hub", "A clearing with rooms off it all round"), ("arena", "One great room with a hill, a mound or a moat"), ("climb", "A hill of terraces to climb"),
        ("gorge", "A winding gorge"), ("web", "A web of rooms, more than one way round"),
    ];
    private static readonly (SurfaceRole Role, string Label)[] PictureRoles =
    [
        (SurfaceRole.Ground, "Ground"), (SurfaceRole.GroundAlt, "Banks and hilltops"), (SurfaceRole.Path, "Paths"), (SurfaceRole.Dirt, "Bare earth"),
        (SurfaceRole.Sand, "Sand and shores"), (SurfaceRole.CliffTop, "Tops of rock walls"), (SurfaceRole.Cliff, "Rock walls"), (SurfaceRole.Rock, "Rims and stepping stones"),
        (SurfaceRole.CaveWall, "Cave walls"), (SurfaceRole.Ceiling, "Cave ceilings"), (SurfaceRole.Brick, "Castle walls and towers"),
        (SurfaceRole.Paving, "Paving"), (SurfaceRole.Roof, "Tower roofs"), (SurfaceRole.Wood, "Bridges and planks"),
        (SurfaceRole.Underside, "Under floating islands"), (SurfaceRole.Water, "Water"), (SurfaceRole.Lava, "Lava"), (SurfaceRole.Goo, "Ooze"),
        (SurfaceRole.Charge, "Charge ramps"),
    ];

    private Task RandomLevelAsync() => RandomLevelAsync(homeworld: false);
    /// <summary>File ▾ › Random Homeworld…: the Random Level dialog set to make a homeworld.</summary>
    private Task RandomHomeworldAsync() => RandomLevelAsync(homeworld: true);
    internal Task RandomHomeworldForTestingAsync() => RandomLevelAsync(homeworld: true);

    private async Task RandomLevelAsync(bool homeworld)
    {
        if (_startBlankBusy || _createBinBusy || _regularEditorPersistenceBusy || _workspaceTransitionBusy || _loadingLevel) return;
        if ((HasUnsavedTerrainEdits() || HasUnsavedMobyEdits()) && !await SaveCurrentEditsAsync()) return;
        string root = _workspace.RootPath;
        var (choice, preview) = RandomLevelChoiceForTesting != null ? (RandomLevelChoiceForTesting(), null) : await ShowRandomLevelDialogAsync(homeworld);
        if (choice == null || _workspace.RootPath != root) return;
        await MakeRandomLevelAsync(choice, preview);
    }

    /// <summary>The levels a homeworld made in this place gets a doorway to: a homeworld of the game leads to its own
    /// levels, your new world's homeworld to the world's levels and back out to the game. None: the place is not a
    /// homeworld (doorways work only where the game keeps its doorway code).</summary>
    private IReadOnlyList<string> RandomHomeworldDoorways(RandomLevelPlace place, int worldLevelId)
    {
        try
        {
            switch (place)
            {
                case RandomLevelPlace.OpenLevel:
                {
                    string? key = _freshSceneDocument?.TargetBinding?.LevelKey ?? _currentLevel?.Key;
                    if (key == null || _retailCatalog.FindByKey(key) is not { } level || level.LevelId % 10 != 0) return [];
                    return [.. HomeworldPortalControlCatalog.ForLevel(key).OrderBy(d => d.DestinationLevelId)
                        .Select(d => _retailCatalog.Levels.FirstOrDefault(l => l.LevelId == d.DestinationLevelId)?.DisplayName ?? d.DestinationName)];
                }
                case RandomLevelPlace.World when worldLevelId == NewWorldDocument.Homeworld:
                {
                    if (NewWorldDocument.Read(_workspace.RootPath) is not { } world) return [];
                    var names = world.LevelIds.Where(id => id != NewWorldDocument.Homeworld).Select(id => DestinationNameOrNull(id) ?? $"level {id - NewWorldDocument.Homeworld}").ToList();
                    if (_retailCatalog.FindByKey(world.EntryHomeworldKey) is { } back) names.Add($"{back.DisplayName} (the way back out)");
                    return names;
                }
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException) { }
        return [];
    }

    /// <summary>The generated homeworld's doorways, built where the generator kept places for them: one to each level
    /// the scene's homeworld leads to. Returns the scene and a sentence saying where they lead.</summary>
    private (SceneDocument Scene, string Note) WithHomeworldDoorways(SceneDocument scene, RandomLevelResult result)
    {
        if (result.Doorways.Count == 0 || result.DoorwayMaterial == null || SceneDoorways.Unavailable(scene, _retailCatalog) != null) return (scene, "");
        var destinations = SceneDoorways.Destinations(scene, _retailCatalog, DestinationNameOrNull, DoorwayWayBack());
        var shape = LevelThemes.DoorwayShape(result.Report.Theme);
        var (built, added, problems) = SceneDoorways.AddAll(scene, result.Doorways, destinations, shape, result.DoorwayMaterial);
        if (added.Count == 0) return (scene, problems.Count > 0 ? "Its doorways could not be built: " + problems[0] : "");
        string names = string.Join(", ", added.Select(id => destinations.First(d => d.LevelId == id).Name));
        string missing = string.Join(", ", destinations.Where(d => !d.Taken && !added.Contains(d.LevelId)).Select(d => d.Name));
        var kinds = built.Portals!.Where(d => added.Contains(d.DestinationLevelId)).Select(d => DoorwayShapes.Find(d.Shape)?.Name.ToLowerInvariant() ?? "doorway").Distinct().ToList();
        string look = kinds.Count == 1 ? $"{kinds[0]} doorways" : $"doorways ({string.Join(", ", kinds.Take(kinds.Count - 1))} and {kinds[^1]})";
        if (added.Count == 1) look = $"a {kinds[0]} doorway";
        return (built, $"It has {look}, framed in the level's own stone, to {names}" +
            (missing.Length > 0 ? $"; there was no place left for {missing} (add it with Terrain › Doorways)." : "."));
    }

    /// <summary>Where the level can go: in place of the open level (a game level, homeworld or one rebuilt from one), the
    /// project's New Level slot, and each level of the project's new world.</summary>
    private List<(RandomLevelPlace Place, int World, string Label)> RandomLevelPlaces()
    {
        List<(RandomLevelPlace, int, string)> places = [];
        if (_currentLevel != null && CanStartBlankCurrentLevel() && (_freshSceneDocument == null || _freshSceneDocument.TargetBinding != null))
            places.Add((RandomLevelPlace.OpenLevel, 0, $"In place of {_currentLevel.DisplayName} (a recovery copy is kept)"));
        bool hasNew = File.Exists(SceneDocumentStore.PathForTarget(_workspace.RootPath, "authored65"));
        places.Add((RandomLevelPlace.NewLevel, 0, hasNew ? "As the project's new level (the one there now is copied to backups)" : "As the project's new level (File ▾ › New Level's slot)"));
        try
        {
            if (NewWorldDocument.Read(_workspace.RootPath) is { } world)
                foreach (int id in world.LevelIds)
                {
                    string path = SceneDocumentStore.PathForTarget(_workspace.RootPath, NewWorldDocument.KeyFor(id));
                    string now = File.Exists(path) ? SceneDocument.Read(path).Name : "empty";
                    places.Add((RandomLevelPlace.World, id, $"In {world.Name}: {(id == NewWorldDocument.Homeworld ? "its homeworld" : $"level {id - NewWorldDocument.Homeworld}")} (now {now})"));
                }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException) { }
        return places;
    }

    private async Task<(RandomLevelChoice? Choice, RandomLevelResult? Preview)> ShowRandomLevelDialogAsync(bool homeworld = false)
    {
        var allPlaces = RandomLevelPlaces();
        var places = allPlaces;
        var rnd = new Random();
        RandomLevelResult? preview = null;
        RandomLevelOptions? previewOptions = null;
        var pictures = new Dictionary<SurfaceRole, string>();
        CancellationTokenSource? running = null;

        TextBlock Label(string text) => new() { Text = text, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
        var left = new StackPanel { Spacing = 6, Width = 340 };
        var kind = new ComboBox { Name = "RandomLevelKind", HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = RandomLevelKinds.Select(k => k.Label).ToArray(), SelectedIndex = 0 };
        var layout = new ComboBox { Name = "RandomLevelLayout", HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = RandomLevelLayouts.Select(l => l.Label).ToArray(), SelectedIndex = 0 };
        ToolTip.SetTip(layout, "How the level's rooms lie. Left to the seed, every level is laid out another way, with a few big rooms, a mix, or many small ones.");
        var look = new ComboBox { Name = "RandomLevelLook", HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = new[] { "Surprise me" }.Concat(LevelThemes.All.Select(t => t.Name)).ToArray(), SelectedIndex = 0 };
        var size = new ComboBox { Name = "RandomLevelSize", HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = new[] { "Small", "Medium", "Large" }, SelectedIndex = 1 };
        var what = new ComboBox
        {
            Name = "RandomLevelMakes", HorizontalAlignment = HorizontalAlignment.Stretch, SelectedIndex = homeworld ? 1 : 0,
            ItemsSource = new[] { "A level", "A homeworld with its doorways" },
        };
        var whatNote = new TextBlock { Name = "RandomLevelMakesNote", TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = 0.75, IsVisible = false };
        var difficulty = new ComboBox { Name = "RandomLevelDifficulty", HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = new[] { "Easy", "Medium", "Hard", "Kaizo" }, SelectedIndex = 1 };
        ToolTip.SetTip(difficulty, "Easy: wide paths, bridges, short hops, plenty of room. Hard: narrow paths, stepping stones, more ledges and lookouts, pools that cost a life. Kaizo: next to nothing to spare. Every jump and glide stays inside what Spyro can do.");
        left.Children.Add(Label("Make")); left.Children.Add(what); left.Children.Add(whatNote);
        left.Children.Add(Label("Kind of level")); left.Children.Add(kind);
        left.Children.Add(Label("Laid out as")); left.Children.Add(layout);
        left.Children.Add(Label("In the style of")); left.Children.Add(look);
        left.Children.Add(new TextBlock { Text = "The level is built from that level's own ground, rock and paths, painted in its colours.", TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = 0.75 });
        var sizeRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*"), RowDefinitions = new RowDefinitions("Auto,Auto") };
        var sizeLabel = Label("Size"); var difficultyLabel = Label("Difficulty");
        Grid.SetColumn(difficultyLabel, 2); Grid.SetRow(size, 1); Grid.SetRow(difficulty, 1); Grid.SetColumn(difficulty, 2);
        sizeRow.Children.Add(sizeLabel); sizeRow.Children.Add(difficultyLabel); sizeRow.Children.Add(size); sizeRow.Children.Add(difficulty);
        left.Children.Add(sizeRow);
        left.Children.Add(Label("What it has"));
        left.Children.Add(new TextBlock { Text = "A filled square leaves it to chance; a tick asks for it; empty leaves it out.", TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = 0.75 });
        var featureGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto") };
        left.Children.Add(featureGrid);
        CheckBox Feature(string name, string text, string tip)
        {
            var box = new CheckBox { Name = name, Content = text, IsThreeState = true, IsChecked = null };
            ToolTip.SetTip(box, tip);
            int n = featureGrid.Children.Count;
            Grid.SetColumn(box, n % 2); Grid.SetRow(box, n / 2);
            featureGrid.Children.Add(box);
            return box;
        }
        var caves = Feature("RandomLevelCaves", "Caves", "Caverns: more of the rooms are under rock. Canyons and castles: some paths run through tunnels.");
        var charge = Feature("RandomLevelCharge", "Charge ramps", "A ramp too steep to walk, marked with arrows, up to a hidden nook: Spyro climbs it by charging.");
        var glides = Feature("RandomLevelGlides", "Glides", "Steps up to a lookout, and a ledge across the room that Spyro reaches only by gliding from it.");
        var hazards = Feature("RandomLevelHazards", "Pools", "Pools of water, lava or ooze at the side of some rooms.");
        var bridges = Feature("RandomLevelBridges", "Bridges", "Islands are joined by bridges (without them, by stepping stones).");
        var secrets = Feature("RandomLevelSecrets", "Hidden nooks", "Small rooms off the level's rooms, with room for treasure.");
        left.Children.Add(Label("Textures"));
        var gameTextures = new RadioButton { Name = "RandomLevelGameTextures", GroupName = "RandomLevelTextures", Content = "That level's own textures and colours", IsChecked = true };
        var painted = new RadioButton { Name = "RandomLevelPainted", GroupName = "RandomLevelTextures", Content = "New pictures painted in the look's colours" };
        left.Children.Add(gameTextures); left.Children.Add(painted);
        var ownPictures = NewButton("Use My Own Pictures…"); ownPictures.Name = "RandomLevelOwnPictures";
        var ownNote = new TextBlock { Name = "RandomLevelOwnNote", TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = 0.75, Text = "None chosen: every surface follows the choice above." };
        left.Children.Add(ownPictures); left.Children.Add(ownNote);
        left.Children.Add(Label("Sky"));
        var paintedSky = new RadioButton { Name = "RandomLevelPaintedSky", GroupName = "RandomLevelSky", Content = "A sky painted to match" };
        var gameSky = new RadioButton { Name = "RandomLevelGameSky", GroupName = "RandomLevelSky", Content = "That level's sky", IsChecked = true };
        var skyRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        skyRow.Children.Add(gameSky); skyRow.Children.Add(paintedSky);
        left.Children.Add(skyRow);

        var right = new StackPanel { Spacing = 8, Width = 380 };
        var map = new Image { Name = "RandomLevelMap", Width = 300, Height = 300, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center };
        RenderOptions.SetBitmapInterpolationMode(map, BitmapInterpolationMode.None);
        var mapFrame = new Border { Background = new SolidColorBrush(Color.FromRgb(24, 28, 40)), CornerRadius = new CornerRadius(6), Padding = new Thickness(8), Child = map };
        var summary = new TextBlock { Name = "RandomLevelSummary", TextWrapping = TextWrapping.Wrap, MinHeight = 80, Text = "Making a level…" };
        var busy = new ProgressBar { IsIndeterminate = true, Height = 4, IsVisible = false };
        var nameBox = new TextBox { Name = "RandomLevelName", MaxLength = NewWorldDocument.MaxNameLength, Watermark = "LEVEL NAME" };
        var seedBox = new TextBox { Name = "RandomLevelSeed", Text = rnd.NextInt64(1, 999_999_999).ToString() };
        var generate = NewButton("New Seed"); generate.Name = "RandomLevelGenerate";
        ToolTip.SetTip(generate, "Roll a new seed and make another level with these choices.");
        var again = NewButton("Rebuild"); again.Name = "RandomLevelRebuild";
        ToolTip.SetTip(again, "Make the level again from this seed with the choices on the left (and the name you typed).");
        right.Children.Add(mapFrame); right.Children.Add(busy); right.Children.Add(summary);
        right.Children.Add(Label("Name"));
        right.Children.Add(nameBox);
        right.Children.Add(Label("Seed: the same seed and choices always make the same level"));
        var seedRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        seedBox.Width = 140;
        seedRow.Children.Add(seedBox); seedRow.Children.Add(generate); seedRow.Children.Add(again);
        right.Children.Add(seedRow);

        var columns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        columns.Children.Add(left);
        columns.Children.Add(right);

        var content = new StackPanel { Margin = new Thickness(22), Spacing = 12 };
        var heading = new TextBlock { Text = homeworld ? "Make a random homeworld" : "Make a random level", FontSize = 20, FontWeight = FontWeight.SemiBold };
        content.Children.Add(heading);
        content.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, MaxWidth = 780, HorizontalAlignment = HorizontalAlignment.Left,
            Text = "A new level made the way the game's own levels are, and no two alike: a way from room to room, a ring, a clearing with rooms off it, a hill of terraces, a winding gorge, a web of rooms, with a few big rooms or many small ones, sloping paths and flights of steps between them, steps to jump up, lookouts and hilltops to glide from. It is built and painted like the level you pick: that level's ground, rock and paths in its colours, under its sky, to its music, with its own towers and trees. Every jump, glide and climb is inside what Spyro can do. Choose what you like or leave it to chance, then put it in a level slot and add your own objects and enemies."
        });
        content.Children.Add(columns);
        var place = new ComboBox { Name = "RandomLevelPlace", HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = places.Select(p => p.Label).ToArray(), SelectedIndex = 0 };
        content.Children.Add(Label("Put it"));
        content.Children.Add(place);
        var fill = new CheckBox { Name = "RandomLevelFillIn", Content = "Then fill it with objects for me: enemies, chests, gems, dragons and the way out (Objects › Random objects does it again another way)", IsChecked = false };
        content.Children.Add(fill);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        Button cancel = NewButton("Cancel"), make = NewButton("Make Level");
        cancel.Name = "RandomLevelCancel"; make.Name = "RandomLevelMake";
        StyleModernPrimaryButton(make, ModernBlue, 132);
        actions.Children.Add(cancel); actions.Children.Add(make); content.Children.Add(actions);
        var dialog = new Window
        {
            Name = "RandomLevelDialog", Title = "Random Level", Width = 840, SizeToContent = SizeToContent.Height, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = new ScrollViewer { Content = content }
        };

        // A homeworld goes where the game keeps its doorway code: the places that are homeworlds.
        IReadOnlyList<string> Doorways() => what.SelectedIndex == 1 && place.SelectedIndex >= 0 && place.SelectedIndex < places.Count
            ? RandomHomeworldDoorways(places[place.SelectedIndex].Place, places[place.SelectedIndex].World) : [];
        void ShowPlaces()
        {
            bool home = what.SelectedIndex == 1;
            places = home ? [.. allPlaces.Where(p => RandomHomeworldDoorways(p.Place, p.World).Count > 0)] : allPlaces;
            place.ItemsSource = places.Count > 0 ? places.Select(p => p.Label).ToArray() : new[] { "No homeworld is open" };
            place.SelectedIndex = 0;
            place.IsEnabled = places.Count > 0;
            make.Content = home ? "Make Homeworld" : "Make Level";
            heading.Text = home ? "Make a random homeworld" : "Make a random level";
            ShowDoorways();
        }
        void ShowDoorways()
        {
            bool home = what.SelectedIndex == 1;
            var names = Doorways();
            whatNote.IsVisible = home;
            whatNote.Text = !home ? "" : places.Count == 0
                ? "A generated homeworld goes in the place of one of the game's homeworlds or in the homeworld of a world of your own. Open a homeworld first (the level picker), or make a world with File ▾ › New World…, then choose this again."
                : $"Spyro arrives in the first of its rooms. {(names.Count == 1 ? "A doorway" : $"{names.Count} doorways")} will stand in the rooms, one to a room, with open floor in front to fly back out onto: to {string.Join(", ", names)}.";
            make.IsEnabled = !home || places.Count > 0;
        }
        RandomLevelOptions Read()
        {
            long seed = long.TryParse(seedBox.Text?.Trim(), out long s) ? s : 1;
            string? name = (nameBox.Text ?? "").Trim().ToUpperInvariant();
            return new RandomLevelOptions
            {
                Seed = seed, Style = RandomLevelKinds[Math.Max(0, kind.SelectedIndex)].Style, Size = (LevelSize)Math.Max(0, size.SelectedIndex),
                Layout = RandomLevelLayouts[Math.Clamp(layout.SelectedIndex, 0, RandomLevelLayouts.Length - 1)].Layout,
                Theme = look.SelectedIndex > 0 ? LevelThemes.All[look.SelectedIndex - 1].Key : null,
                Difficulty = (LevelDifficulty)Math.Clamp(difficulty.SelectedIndex, 0, 3), Doorways = Doorways().Count,
                Caves = caves.IsChecked, ChargePaths = charge.IsChecked, Glides = glides.IsChecked, Hazards = hazards.IsChecked,
                Bridges = bridges.IsChecked, Secrets = secrets.IsChecked,
                Textures = painted.IsChecked == true ? LevelTextureMode.GeneratedPictures : LevelTextureMode.GameTextures,
                Pictures = new Dictionary<SurfaceRole, string>(pictures), PaintedSky = paintedSky.IsChecked == true,
                Name = string.IsNullOrEmpty(name) ? null : name,
            };
        }
        async Task Preview(bool keepName)
        {
            running?.Cancel();
            var cancelSource = running = new CancellationTokenSource();
            var options = Read() with { Name = keepName ? Read().Name : null };
            busy.IsVisible = true; summary.Text = "Making a level…"; make.IsEnabled = false;
            try
            {
                var catalog = _retailCatalog;
                var lookSource = RandomLevelLookSource();
                var result = await Task.Run(() => RandomLevelGenerator.Generate(options with { Textures = LevelTextureMode.GameTextures, Pictures = new Dictionary<SurfaceRole, string>() }, catalog, token: cancelSource.Token, source: lookSource), cancelSource.Token);
                if (cancelSource.IsCancellationRequested) return;
                preview = result; previewOptions = options;
                map.Source = MapBitmap(result);
                if (!keepName) nameBox.Text = result.Scene.Name;
                summary.Text = result.Report.Summary + (result.Report.Notes.Count > 0 ? "\n" + string.Join("\n", result.Report.Notes.Take(3)) : "");
            }
            catch (OperationCanceledException) { }
            catch (Exception e) when (e is InvalidDataException or InvalidOperationException or ArgumentException)
            { summary.Text = "This one could not be made: " + e.Message + " Try another seed."; preview = null; }
            finally { if (running == cancelSource) { busy.IsVisible = false; make.IsEnabled = what.SelectedIndex != 1 || places.Count > 0; } }
        }
        what.SelectionChanged += async (_, _) => { ShowPlaces(); await Preview(true); };
        place.SelectionChanged += async (_, _) =>
        {
            if (what.SelectedIndex != 1) return;
            ShowDoorways();
            // Another homeworld, another number of doorways: the level is laid out again for them.
            if (previewOptions is { } shown && shown.Doorways != Doorways().Count) await Preview(true);
        };
        if (homeworld) ShowPlaces();
        generate.Click += async (_, _) => { seedBox.Text = rnd.NextInt64(1, 999_999_999).ToString(); await Preview(false); };
        again.Click += async (_, _) => await Preview(true);
        ownPictures.Click += async (_, _) =>
        {
            await ShowRandomLevelPicturesAsync(dialog, pictures);
            ownNote.Text = pictures.Count == 0 ? "None chosen: every surface follows the choice above." : $"Your pictures for: {string.Join(", ", PictureRoles.Where(r => pictures.ContainsKey(r.Role)).Select(r => r.Label))}.";
        };
        bool accepted = false;
        cancel.Click += (_, _) => dialog.Close();
        make.Click += (_, _) => { accepted = true; dialog.Close(); };
        dialog.Opened += async (_, _) => await Preview(false);
        await dialog.ShowDialog(this);
        running?.Cancel();
        if (!accepted || place.SelectedIndex < 0 || place.SelectedIndex >= places.Count) return (null, null);
        var chosen = places[place.SelectedIndex];
        var final = Read();
        // The preview was made with the game's textures; it is the level itself when nothing but the name changed.
        bool same = previewOptions is { } p && preview != null && final.Textures == LevelTextureMode.GameTextures && final.Pictures.Count == 0 &&
            p.Seed == final.Seed && p.Style == final.Style && p.Layout == final.Layout && p.Size == final.Size && p.Theme == final.Theme && p.Caves == final.Caves &&
            p.ChargePaths == final.ChargePaths && p.Glides == final.Glides && p.Hazards == final.Hazards && p.Bridges == final.Bridges &&
            p.Secrets == final.Secrets && p.PaintedSky == final.PaintedSky && p.Difficulty == final.Difficulty && p.Doorways == final.Doorways;
        if (same && final.Name is { } typed && NewWorldDocument.IsValidName(typed) && typed.All(c => !char.IsDigit(c)) && typed != preview!.Scene.Name)
            preview = preview with { Scene = preview.Scene with { Name = typed } };
        return (new RandomLevelChoice(final, chosen.Place, chosen.World, fill.IsChecked == true), same ? preview : null);
    }

    /// <summary>Reads a look's level from the project's level cache (the untouched original terrain), so the generator
    /// can stand that level's own towers, trees and rocks in the new level. Called off the UI thread; a level that
    /// cannot be read gives none, and the level is made without them.</summary>
    private Func<string, LevelPropSource?> RandomLevelLookSource()
    {
        var paths = LevelThemes.All.ToDictionary(t => t.Key, t => FindCachedTerrainOverlayPath(t.Key), StringComparer.OrdinalIgnoreCase);
        return key =>
        {
            if (!paths.TryGetValue(key, out string? path) || string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
            lock (_randomLevelLookSources)
            {
                if (_randomLevelLookSources.TryGetValue(path, out var known)) return known;
                LevelPropSource? read = null;
                try
                {
                    if (GeometryCacheHealth.InspectOverlay(key, path)?.BlocksLoading != true)
                        read = LevelPropSource.FromGeometry(key, GeometryOverlayLoader.LoadFirstCandidate(path));
                }
                catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException) { }
                return _randomLevelLookSources[path] = read;
            }
        };
    }
    private readonly Dictionary<string, LevelPropSource?> _randomLevelLookSources = new(StringComparer.OrdinalIgnoreCase);

    private static WriteableBitmap MapBitmap(RandomLevelResult r)
    {
        var bitmap = new WriteableBitmap(new PixelSize(r.MapWidth, r.MapHeight), new Vector(96, 96), PixelFormats.Rgba8888, AlphaFormat.Unpremul);
        using var locked = bitmap.Lock();
        byte[] row = new byte[r.MapWidth * 4];
        for (int y = 0; y < r.MapHeight; y++)
        {
            for (int x = 0; x < r.MapWidth; x++)
            {
                var p = r.Map[y * r.MapWidth + x];
                row[x * 4] = p.R; row[x * 4 + 1] = p.G; row[x * 4 + 2] = p.B; row[x * 4 + 3] = 255;
            }
            Marshal.Copy(row, 0, locked.Address + y * locked.RowBytes, row.Length);
        }
        return bitmap;
    }

    /// <summary>The user's own pictures, by the surfaces they go on.</summary>
    private async Task ShowRandomLevelPicturesAsync(Window owner, Dictionary<SurfaceRole, string> pictures)
    {
        var list = new StackPanel { Spacing = 6 };
        var dialog = new Window
        {
            Name = "RandomLevelPicturesDialog", Title = "Your Pictures", Width = 520, SizeToContent = SizeToContent.Height, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        void Fill()
        {
            list.Children.Clear();
            foreach (var (role, label) in PictureRoles)
            {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("170,*,Auto,Auto") };
                row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
                var file = new TextBlock { Text = pictures.TryGetValue(role, out var p) ? Path.GetFileName(p) : "(the level's own)", VerticalAlignment = VerticalAlignment.Center, Opacity = 0.8, TextTrimming = TextTrimming.CharacterEllipsis };
                Grid.SetColumn(file, 1); row.Children.Add(file);
                var choose = NewButton("Choose…"); choose.Name = $"RandomLevelPicture{role}"; Grid.SetColumn(choose, 2); row.Children.Add(choose);
                var clear = NewButton("Clear"); clear.IsEnabled = pictures.ContainsKey(role); Grid.SetColumn(clear, 3); row.Children.Add(clear);
                choose.Click += async (_, _) =>
                {
                    var files = await dialog.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = $"A picture for {label.ToLowerInvariant()} (PNG, JPEG, WebP or BMP)", AllowMultiple = false,
                        FileTypeFilter = [new FilePickerFileType("Pictures") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"] }]
                    });
                    if (files.FirstOrDefault()?.Path.LocalPath is { } path) { pictures[role] = path; Fill(); }
                };
                clear.Click += (_, _) => { pictures.Remove(role); Fill(); };
                list.Children.Add(row);
            }
        }
        Fill();
        var content = new StackPanel { Margin = new Thickness(22), Spacing = 10 };
        content.Children.Add(new TextBlock { Text = "Your own pictures", FontSize = 18, FontWeight = FontWeight.SemiBold });
        content.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = "Choose a picture for any surface. It is shrunk to the game's 64 × 64 texture size and shown on every face of that kind; " +
                   "the rest keep the textures chosen in the main window. Pictures that repeat seamlessly edge to edge look best."
        });
        content.Children.Add(new ScrollViewer { Content = list, MaxHeight = 460 });
        var done = NewButton("Done"); done.Name = "RandomLevelPicturesDone"; done.HorizontalAlignment = HorizontalAlignment.Right;
        StyleModernPrimaryButton(done, ModernBlue, 100);
        done.Click += (_, _) => dialog.Close();
        content.Children.Add(done);
        dialog.Content = content;
        await dialog.ShowDialog(owner);
    }

    /// <summary>Makes the chosen level (pictures written into the project, textures checked against the built level's
    /// texture memory with fewer textures when they do not fit) and puts it where it was asked.</summary>
    private async Task MakeRandomLevelAsync(RandomLevelChoice choice, RandomLevelResult? preview)
    {
        string root = _workspace.RootPath;
        var options = choice.Options;
        var slot = _currentLevel;
        if (options.Doorways > 0)
        {
            // A homeworld: as many doorways as the place's homeworld has levels to lead to.
            int doorways = RandomHomeworldDoorways(choice.Place, choice.WorldLevelId).Count;
            if (doorways == 0)
            {
                _statusText.Text = "A generated homeworld goes in the place of one of the game's homeworlds or in the homeworld of a world of your own: open a homeworld (or your world's homeworld) and choose Random Homeworld again.";
                return;
            }
            if (doorways != options.Doorways) { options = options with { Doorways = doorways }; preview = null; }
        }
        _startBlankBusy = true;
        try
        {
            _statusText.Text = "Making the random level…";
            string folder = Path.Combine(root, "custom-terrain-textures", $"random-{(ulong)options.Seed:x}");
            bool needPictures = options.Textures == LevelTextureMode.GeneratedPictures || options.Pictures.Count > 0;
            PictureCarriers? carriers = null;
            string disc = ResolveUniversalSourceDisc();
            if (needPictures)
            {
                if (string.IsNullOrEmpty(disc) || !File.Exists(disc))
                { _statusText.Text = "Pictures need your original Spyro the Dragon disc (Open Game…). Choose the game's own textures, or open the disc first."; return; }
                Directory.CreateDirectory(folder);
                // The user's pictures, read with the app's image support and written into the project at the game's size.
                var own = new Dictionary<SurfaceRole, string>();
                foreach (var (role, path) in options.Pictures)
                {
                    var image = DecodeSkyPicture(await File.ReadAllBytesAsync(path), 512);
                    string target = Path.Combine(folder, $"mine-{role.ToString().ToLowerInvariant()}.png");
                    await TerrainTexturePngWriter.WriteRgbaAsync(target, 64, 64, Resize64(image));
                    own[role] = target;
                }
                options = options with { Pictures = own };
                _statusText.Text = "Finding the game's texture slots that can carry pictures (the first time takes a moment)…";
                if (_pictureCarriers == null || _pictureCarriersDisc != disc)
                {
                    var catalog = _retailCatalog;
                    _pictureCarriers = await Task.Run(() => Spyro.Editor.Core.Crash.CrashLevelPictures.Carriers(new UniversalSourceLibrary(disc, catalog), catalog,
                        catalog.Levels.Select(l => l.Key), disc).Select(c => (c.Wad, c.Id)).ToList());
                    _pictureCarriersDisc = disc;
                }
                carriers = new(_pictureCarriers, folder);
            }
            string? replaced = choice.Place == RandomLevelPlace.OpenLevel ? _freshSceneDocument?.TargetBinding?.LevelKey ?? slot?.Key : null;
            var lookSource = RandomLevelLookSource();
            RandomLevelResult result = preview ?? await Task.Run(() => RandomLevelGenerator.Generate(options, _retailCatalog, carriers, source: lookSource));
            // Fewer textures when they do not fit the built level's texture memory.
            if (!string.IsNullOrEmpty(disc) && File.Exists(disc))
            {
                _statusText.Text = "Checking the level's textures fit the game's texture memory…";
                for (int budget = result.Materials.Count; ; )
                {
                    string? problem = await RandomLevelTextureCheck.CheckAsync(disc, _retailCatalog, result.Materials, replaced);
                    if (problem == null) break;
                    budget = Math.Min(budget, result.Materials.Count) - 3;
                    if (budget < 5) { _statusText.Text = "The level's textures do not fit the game's texture memory: " + problem; return; }
                    int b = budget;
                    result = await Task.Run(() => RandomLevelGenerator.Generate(options, _retailCatalog, carriers, b, source: lookSource));
                }
            }
            foreach (var p in result.Pictures) await TerrainTexturePngWriter.WriteRgbaAsync(p.Path, 64, 64, p.Pixels);
            if (_workspace.RootPath != root) return;
            LastRandomLevelForTesting = result;
            var scene = result.Scene;
            string where, doors = "";
            switch (choice.Place)
            {
                case RandomLevelPlace.OpenLevel:
                {
                    if (slot == null || _currentLevel != slot) return;
                    var binding = await StartBlankBindingAsync(slot);
                    string slotName = _retailCatalog.FindByKey(binding?.LevelKey ?? "")?.DisplayName ?? slot.DisplayName;
                    var blank = await StartBlankConfirmedAsync(slot, starter: false);
                    if (blank == null) return;
                    if (blank.Ledger.Entries.Count > 0 && await FreeRetiredSaveRowsConfirmedAsync() > 0) blank = CaptureFreshScene();
                    (var placed, doors) = WithHomeworldDoorways(Placed(blank, scene), result);
                    ApplyFreshScene(placed);
                    if (!await SaveFreshSceneAsync()) return;
                    where = $"in {slotName}{(slotName.EndsWith('s') ? "'" : "'s")} place (File ▾ › Restore Before Clear… brings {slotName} back)";
                    break;
                }
                case RandomLevelPlace.NewLevel:
                {
                    string existing = SceneDocumentStore.PathForTarget(root, "authored65");
                    if (File.Exists(existing))
                    {
                        string backups = Path.Combine(root, "backups"); Directory.CreateDirectory(backups);
                        File.Copy(existing, Path.Combine(backups, $"created-level-{DateTime.Now:yyyyMMdd-HHmmss}.scene.json"), overwrite: false);
                    }
                    ApplyFreshScene(scene with { TargetBinding = null, WorldLevelId = null });
                    if (!await SaveFreshSceneAsync()) return;
                    where = "as the project's new level (Create BIN starts New Game in it)";
                    break;
                }
                default:
                {
                    string path = SceneDocumentStore.PathForTarget(root, NewWorldDocument.KeyFor(choice.WorldLevelId));
                    if (File.Exists(path))
                    {
                        string backups = Path.Combine(root, "backups"); Directory.CreateDirectory(backups);
                        File.Copy(path, Path.Combine(backups, $"world-level-{choice.WorldLevelId}-{DateTime.Now:yyyyMMdd-HHmmss}.scene.json"), overwrite: false);
                    }
                    (var placed, doors) = WithHomeworldDoorways(scene with { TargetBinding = null, WorldLevelId = choice.WorldLevelId }, result);
                    await placed.SaveAsync(path);
                    await OpenWorldLevelAsync(choice.WorldLevelId);
                    where = "in your world (the earlier version is in backups)";
                    break;
                }
            }
            ApplyEditorExperienceForCurrentLevel(_modernTerrainWorkspaceTab);
            // Objects › Random objects starts on the level this one was made in the style of.
            RefreshRandomObjectsCard(result.Report.Theme, options.Difficulty);
            string filled = "";
            if (choice.FillIn)
            {
                // The way out, dragons, enemies, chests and gems, placed at random in that level's manner
                // (Objects › Random objects places them again another way).
                _startBlankBusy = false;
                if (ReadRandomObjectsChoice() is { } objects) await RandomizeObjectsAsync(objects with { LevelKey = result.Report.Theme, EnemyLevelKey = result.Report.Theme, EnemyClass = 0 });
                filled = " " + (_statusText.Text ?? "");
                _startBlankBusy = true;
                if (_freshSceneDocument != null) await SaveFreshSceneAsync();
            }
            if (doors.Length > 0) { _doorwaysSignature = ""; RefreshDoorwaysCard(); }
            _statusText.Text = $"{scene.Name} is made {where}. " + (doors.Length > 0 ? doors + " " : "") + (choice.FillIn ? "" : "Objects › Random objects fills it for you, or add objects and enemies with Add Object; then Create BIN.") + filled;
            if (RandomLevelChoiceForTesting == null)
                await ShowRandomLevelReadyAsync(scene.Name, result.Report.Summary + (doors.Length > 0 ? "\n" + doors + " Terrain › Doorways moves, turns and reshapes them; their frames are ordinary tiles you can repaint." : ""), where, choice.FillIn);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException or InvalidOperationException or System.Text.Json.JsonException)
        { _statusText.Text = "The random level could not be made: " + e.Message; }
        finally { _startBlankBusy = false; RefreshCurrentLevelDetails(); }
    }

    /// <summary>The generated level in an emptied level's place: its own faces, start, sky, music, name and gem places, the
    /// emptied level's save rows and binding kept.</summary>
    private static SceneDocument Placed(SceneDocument blank, SceneDocument level) => blank with
    {
        Name = level.Name, Surfaces = level.Surfaces, Spawn = level.Spawn, Heading = level.Heading, SkyColor = level.SkyColor,
        SkySourceWadEntry = level.SkySourceWadEntry, CustomSky = level.CustomSky, MusicTrack = level.MusicTrack, GemSpots = level.GemSpots,
        FarMeshes = null, CollisionFaces = null, Portals = null,
    };

    /// <summary>A picture shrunk (box average) to the game's 64 x 64 texture size.</summary>
    private static Rgba32[] Resize64(Spyro.Editor.Core.Sky.SkyImage image)
    {
        var px = new Rgba32[64 * 64];
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                int x0 = x * image.Width / 64, x1 = Math.Max(x0 + 1, (x + 1) * image.Width / 64), y0 = y * image.Height / 64, y1 = Math.Max(y0 + 1, (y + 1) * image.Height / 64);
                long r = 0, g = 0, b = 0, n = 0;
                for (int yy = y0; yy < y1; yy++) for (int xx = x0; xx < x1; xx++) { var p = image.Pixels[yy * image.Width + xx]; r += p.R; g += p.G; b += p.B; n++; }
                px[y * 64 + x] = new((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
            }
        return px;
    }

    private async Task ShowRandomLevelReadyAsync(string name, string summary, string where, bool filled)
    {
        var content = new StackPanel { Margin = new Thickness(22), Spacing = 12 };
        content.Children.Add(new TextBlock { Text = $"{name} is ready to build on", FontSize = 20, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new TextBlock { Name = "RandomLevelReadySummary", TextWrapping = TextWrapping.Wrap, Text = $"It is {where}.\n{summary}" });
        content.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, Text = filled
                ? "Its objects are placed. Objects › Random objects places them again another way (choose whose enemies, how many gems, dragonfly eggs), or move any of them yourself. Game Settings changes the music and sky."
                : "Next, its objects: Objects › Random objects fills the level for you (enemies, chests, gems of every colour, dragons, the way out: choose a level type and press Randomize), " +
                  "or add them yourself with Add Object. Terrain › Ground and Choose Texture repaint any surface; Game Settings changes the music and sky."
        });
        var ok = NewButton("OK"); ok.Name = "RandomLevelReadyOk"; StyleModernPrimaryButton(ok, ModernBlue, 100); ok.HorizontalAlignment = HorizontalAlignment.Right;
        content.Children.Add(ok);
        var dialog = new Window { Name = "RandomLevelReadyDialog", Title = "Random Level", Width = 540, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = content };
        ok.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }
}
