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
        (null, "Surprise me"), (LevelStyle.Valley, "Valley: one big area (like Dark Hollow)"), (LevelStyle.Islands, "Islands in the water"),
        (LevelStyle.SkyIslands, "Islands in the sky (like Lofty Castle)"), (LevelStyle.Caverns, "Caverns and tunnels"),
        (LevelStyle.Canyon, "Canyon trail"), (LevelStyle.Castle, "Castle and courtyards"),
    ];
    private static readonly (SurfaceRole Role, string Label)[] PictureRoles =
    [
        (SurfaceRole.Ground, "Ground"), (SurfaceRole.GroundAlt, "Ground patches"), (SurfaceRole.Path, "Paths"), (SurfaceRole.Dirt, "Ramps and banks"),
        (SurfaceRole.Sand, "Sand and shores"), (SurfaceRole.CliffTop, "Cliff tops"), (SurfaceRole.Cliff, "Cliffs"), (SurfaceRole.Rock, "Rocks"),
        (SurfaceRole.CaveWall, "Cave walls"), (SurfaceRole.Ceiling, "Cave ceilings"), (SurfaceRole.Brick, "Walls and houses"),
        (SurfaceRole.Paving, "Paving and ledges"), (SurfaceRole.Roof, "Roofs"), (SurfaceRole.Wood, "Bridges and planks"),
        (SurfaceRole.Underside, "Under floating islands"), (SurfaceRole.Water, "Water"), (SurfaceRole.Lava, "Lava"), (SurfaceRole.Goo, "Ooze"),
        (SurfaceRole.Charge, "Supercharge ramps"),
    ];

    private async Task RandomLevelAsync()
    {
        if (_startBlankBusy || _createBinBusy || _regularEditorPersistenceBusy || _workspaceTransitionBusy || _loadingLevel) return;
        if ((HasUnsavedTerrainEdits() || HasUnsavedMobyEdits()) && !await SaveCurrentEditsAsync()) return;
        string root = _workspace.RootPath;
        var (choice, preview) = RandomLevelChoiceForTesting != null ? (RandomLevelChoiceForTesting(), null) : await ShowRandomLevelDialogAsync();
        if (choice == null || _workspace.RootPath != root) return;
        await MakeRandomLevelAsync(choice, preview);
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

    private async Task<(RandomLevelChoice? Choice, RandomLevelResult? Preview)> ShowRandomLevelDialogAsync()
    {
        var places = RandomLevelPlaces();
        var rnd = new Random();
        RandomLevelResult? preview = null;
        RandomLevelOptions? previewOptions = null;
        var pictures = new Dictionary<SurfaceRole, string>();
        CancellationTokenSource? running = null;

        TextBlock Label(string text) => new() { Text = text, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
        var left = new StackPanel { Spacing = 6, Width = 340 };
        var kind = new ComboBox { Name = "RandomLevelKind", HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = RandomLevelKinds.Select(k => k.Label).ToArray(), SelectedIndex = 0 };
        var look = new ComboBox { Name = "RandomLevelLook", HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = new[] { "Surprise me" }.Concat(LevelThemes.All.Select(t => t.Name)).ToArray(), SelectedIndex = 0 };
        var size = new ComboBox { Name = "RandomLevelSize", HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = new[] { "Small", "Medium", "Large" }, SelectedIndex = 1 };
        left.Children.Add(Label("Kind of level")); left.Children.Add(kind);
        left.Children.Add(Label("Look")); left.Children.Add(look);
        left.Children.Add(Label("Size")); left.Children.Add(size);
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
        var caves = Feature("RandomLevelCaves", "Caves", "Areas under a rock roof, and tunnels.");
        var charge = Feature("RandomLevelCharge", "Supercharge paths", "Steep ramps Spyro climbs only by charging, and supercharge runways: charge down the arrows and leap a wide pit.");
        var glides = Feature("RandomLevelGlides", "Glides", "Gaps and ledges crossed by jumping and gliding.");
        var hazards = Feature("RandomLevelHazards", "Water or lava", "Pools, channels and chasms of water, lava or ooze that Spyro must not fall into.");
        var bridges = Feature("RandomLevelBridges", "Bridges", "Plank bridges over water and chasms.");
        var secrets = Feature("RandomLevelSecrets", "Secret ledges", "Hidden ledges and platforms with room for treasure.");
        left.Children.Add(Label("Textures"));
        var gameTextures = new RadioButton { Name = "RandomLevelGameTextures", GroupName = "RandomLevelTextures", Content = "The game's own textures, picked for each surface", IsChecked = true };
        var painted = new RadioButton { Name = "RandomLevelPainted", GroupName = "RandomLevelTextures", Content = "New pictures painted in the look's colours" };
        left.Children.Add(gameTextures); left.Children.Add(painted);
        var ownPictures = NewButton("Use My Own Pictures…"); ownPictures.Name = "RandomLevelOwnPictures";
        var ownNote = new TextBlock { Name = "RandomLevelOwnNote", TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = 0.75, Text = "None chosen: every surface follows the choice above." };
        left.Children.Add(ownPictures); left.Children.Add(ownNote);
        left.Children.Add(Label("Sky"));
        var paintedSky = new RadioButton { Name = "RandomLevelPaintedSky", GroupName = "RandomLevelSky", Content = "A sky painted to match", IsChecked = true };
        var gameSky = new RadioButton { Name = "RandomLevelGameSky", GroupName = "RandomLevelSky", Content = "The look's game sky" };
        var skyRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        skyRow.Children.Add(paintedSky); skyRow.Children.Add(gameSky);
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
        content.Children.Add(new TextBlock { Text = "Make a random level", FontSize = 20, FontWeight = FontWeight.SemiBold });
        content.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, MaxWidth = 780, HorizontalAlignment = HorizontalAlignment.Left,
            Text = "Every level is different: valleys, islands, sky islands, caverns, canyons and castles, with ramps, ledges, bridges, " +
                   "stepping stones, glides and charge ramps all built inside Spyro's jumps, glides and climbs. Choose what you like or leave it to chance, " +
                   "then put it in a level slot and add your own objects and enemies."
        });
        content.Children.Add(columns);
        var place = new ComboBox { Name = "RandomLevelPlace", HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = places.Select(p => p.Label).ToArray(), SelectedIndex = 0 };
        content.Children.Add(Label("Put it"));
        content.Children.Add(place);
        var fill = new CheckBox { Name = "RandomLevelFillIn", Content = "Then put in the level's dragons, egg thieves, Return Home and gems for me (you can move them)", IsChecked = false };
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

        RandomLevelOptions Read()
        {
            long seed = long.TryParse(seedBox.Text?.Trim(), out long s) ? s : 1;
            string? name = (nameBox.Text ?? "").Trim().ToUpperInvariant();
            return new RandomLevelOptions
            {
                Seed = seed, Style = RandomLevelKinds[Math.Max(0, kind.SelectedIndex)].Style, Size = (LevelSize)Math.Max(0, size.SelectedIndex),
                Theme = look.SelectedIndex > 0 ? LevelThemes.All[look.SelectedIndex - 1].Key : null,
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
                var result = await Task.Run(() => RandomLevelGenerator.Generate(options with { Textures = LevelTextureMode.GameTextures, Pictures = new Dictionary<SurfaceRole, string>() }, catalog, token: cancelSource.Token), cancelSource.Token);
                if (cancelSource.IsCancellationRequested) return;
                preview = result; previewOptions = options;
                map.Source = MapBitmap(result);
                if (!keepName) nameBox.Text = result.Scene.Name;
                summary.Text = result.Report.Summary + (result.Report.Notes.Count > 0 ? "\n" + string.Join("\n", result.Report.Notes.Take(3)) : "");
            }
            catch (OperationCanceledException) { }
            catch (Exception e) when (e is InvalidDataException or InvalidOperationException or ArgumentException)
            { summary.Text = "This one could not be made: " + e.Message + " Try another seed."; preview = null; }
            finally { if (running == cancelSource) { busy.IsVisible = false; make.IsEnabled = true; } }
        }
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
        if (!accepted || place.SelectedIndex < 0) return (null, null);
        var chosen = places[place.SelectedIndex];
        var final = Read();
        // The preview was made with the game's textures; it is the level itself when nothing but the name changed.
        bool same = previewOptions is { } p && preview != null && final.Textures == LevelTextureMode.GameTextures && final.Pictures.Count == 0 &&
            p.Seed == final.Seed && p.Style == final.Style && p.Size == final.Size && p.Theme == final.Theme && p.Caves == final.Caves &&
            p.ChargePaths == final.ChargePaths && p.Glides == final.Glides && p.Hazards == final.Hazards && p.Bridges == final.Bridges &&
            p.Secrets == final.Secrets && p.PaintedSky == final.PaintedSky;
        if (same && final.Name is { } typed && NewWorldDocument.IsValidName(typed) && typed.All(c => !char.IsDigit(c)) && typed != preview!.Scene.Name)
            preview = preview with { Scene = preview.Scene with { Name = typed } };
        return (new RandomLevelChoice(final, chosen.Place, chosen.World, fill.IsChecked == true), same ? preview : null);
    }

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
            RandomLevelResult result = preview ?? await Task.Run(() => RandomLevelGenerator.Generate(options, _retailCatalog, carriers));
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
                    result = await Task.Run(() => RandomLevelGenerator.Generate(options, _retailCatalog, carriers, b));
                }
            }
            foreach (var p in result.Pictures) await TerrainTexturePngWriter.WriteRgbaAsync(p.Path, 64, 64, p.Pixels);
            if (_workspace.RootPath != root) return;
            LastRandomLevelForTesting = result;
            var scene = result.Scene;
            string where;
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
                    ApplyFreshScene(Placed(blank, scene));
                    if (!await SaveFreshSceneAsync()) return;
                    where = $"in {slotName}'s place (File ▾ › Restore Before Clear… brings {slotName} back)";
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
                    await (scene with { TargetBinding = null, WorldLevelId = choice.WorldLevelId }).SaveAsync(path);
                    await OpenWorldLevelAsync(choice.WorldLevelId);
                    where = "in your world (the earlier version is in backups)";
                    break;
                }
            }
            ApplyEditorExperienceForCurrentLevel(_modernTerrainWorkspaceTab);
            string filled = "";
            if (choice.FillIn)
            {
                _startBlankBusy = false;
                await FillInLevelAsync(new LevelFillChoice(true, true, true, -1));
                filled = " " + (_statusText.Text ?? "");
                _startBlankBusy = true;
                if (_freshSceneDocument != null) await SaveFreshSceneAsync();
            }
            _statusText.Text = $"{scene.Name} is made {where}. Add objects and enemies with Add Object, then Create BIN." + filled;
            if (RandomLevelChoiceForTesting == null) await ShowRandomLevelReadyAsync(scene.Name, result.Report.Summary, where, choice.FillIn);
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
        FarMeshes = null, CollisionFaces = null,
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
                ? "Play it through once and move anything that sits oddly, then add enemies with Add Object. Game Settings changes the music and sky."
                : "Next, add objects with Add Object: the level's dragons and egg thief (the line under the treasure counts them), a Return Home vortex near the end, " +
                  "gems along the paths and on the hidden ledges, and some enemies. File ▾ › Fill In What the Level Needs… can do the first part for you. " +
                  "Terrain › Ground and Choose Texture repaint any surface; Game Settings changes the music and sky."
        });
        var ok = NewButton("OK"); ok.Name = "RandomLevelReadyOk"; StyleModernPrimaryButton(ok, ModernBlue, 100); ok.HorizontalAlignment = HorizontalAlignment.Right;
        content.Children.Add(ok);
        var dialog = new Window { Name = "RandomLevelReadyDialog", Title = "Random Level", Width = 540, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = content };
        ok.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }
}
