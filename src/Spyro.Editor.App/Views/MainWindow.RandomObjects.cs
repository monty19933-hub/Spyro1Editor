using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Generation;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

/// <summary>Objects › Random objects: fills a level you built with objects placed the way the game places them
/// (<see cref="LevelScatter"/>): pick whose things (a level's animals and chests) and whose enemies, how hard it should
/// be, type the treasure the level should hold and how many dragonfly eggs, and press Randomize. Every press takes away what the last one
/// placed and places anew, another way. Everything it places is an ordinary added object.</summary>
public sealed partial class MainWindow
{
    /// <param name="EnemyLevelKey">Whose enemies ("" : the level type's).</param>
    /// <param name="EnemyClass">One kind of enemy only (its class), or 0 for all of that level's kinds.</param>
    /// <param name="Treasure">The gems the level holds in all when it is done, as the game counts them.</param>
    /// <param name="Difficulty">How hard the arrangement is (<see cref="LevelScatter.Request.Difficulty"/>); it also sets
    /// how many animals there are for Sparx (none in Kaizo) and whether there is an extra life (none in Kaizo).</param>
    /// <param name="Scenery">The level type's own scenery too: its trees or cactuses, torches and lamps, flowers and grass.</param>
    /// <param name="TealGems">A rare few teal gems (50 each) take part of the treasure (<see cref="LevelScatter.HighGemsFor"/>).</param>
    /// <param name="RainbowGems">A rare few rainbow gems (100 each) take part of the treasure.</param>
    internal sealed record RandomObjectsChoice(string LevelKey, string EnemyLevelKey, int EnemyClass, int Enemies, int Treasure, int Eggs,
        bool Chests = true, bool Animals = true, bool Thieves = true, bool Dragons = true, bool Exit = true, long? Seed = null,
        LevelDifficulty Difficulty = LevelDifficulty.Medium, bool Scenery = true, bool TealGems = false, bool RainbowGems = false);

    /// <summary>What a scenery object of the game is to Random objects, by its class: a tree or cactus, a torch or lamp
    /// that stands on the floor, one that hangs on a wall, or flowers and grass. Null: not placed (flags fly from
    /// tower tops, a stalactite hangs from a roof, tents and banners belong to one level's own places).</summary>
    internal static LevelScatter.Kind? RandomObjectsSceneryKind(int classId) => classId switch
    {
        0x07F or 0x08A or 0x08D or 0x0DE or 0x0DF or 0x0E0 or 0x162 or 0x163 or 0x164 or 0x1F3 or 0x1F4 or 0x1F7 or 0x1F8 or 0x0CE or 0x0EF or 0x0F2 or 0x1EF => LevelScatter.Kind.Tree,
        0x1E6 or 0x081 or 0x040 or 0x192 => LevelScatter.Kind.Light,
        0x080 or 0x04A => LevelScatter.Kind.WallLight,
        0x150 or 0x156 or 0x1F5 or 0x005 or 0x050 => LevelScatter.Kind.Plant,
        _ => null,
    };

    /// <summary>What the last Randomize placed, by kind (for tests and the card's note).</summary>
    internal IReadOnlyDictionary<string, int> LastRandomObjects { get; private set; } = new Dictionary<string, int>();
    /// <summary>Why Create BIN would refuse what the last Randomize placed (null: it would not).</summary>
    internal string? LastRandomObjectsRefusal { get; private set; }

    private Border? _randomObjectsCard;
    private ComboBox? _randomObjectsLevel, _randomObjectsEnemyLevel, _randomObjectsEnemyKind, _randomObjectsDifficulty;
    private NumericUpDown? _randomObjectsTreasure, _randomObjectsEggs;
    private CheckBox? _randomObjectsEnemies, _randomObjectsChests, _randomObjectsAnimals, _randomObjectsThieves, _randomObjectsDragons, _randomObjectsExit;
    private CheckBox? _randomObjectsScenery, _randomObjectsTeal, _randomObjectsRainbow;
    private TextBlock? _randomObjectsNote;
    private List<(int Class, string Name)> _randomObjectsEnemyKinds = [];
    private string? _randomObjectsScene;
    private readonly Random _randomObjectsSeeds = new();
    /// <summary>The enemies a level of this treasure and difficulty gets: as many as the game's own levels have for
    /// their treasure (about seven for every hundred gems), fewer when easy, more when hard.</summary>
    internal static int RandomObjectsEnemies(LevelDifficulty difficulty, int treasure = 300) => LevelScatter.EnemiesFor(treasure, difficulty);

    /// <summary>The levels whose things can be chosen: the game's levels on foot (not the flights).</summary>
    private List<LevelDefinition> RandomObjectsLevels() =>
        [.. _retailCatalog.Levels.Where(l => l.LevelId is >= 10 and <= 64 && l.SourceWadEntry > 0 && !l.Key.Contains("flight", StringComparison.OrdinalIgnoreCase)).OrderBy(l => l.LevelId)];

    private Border BuildRandomObjectsCard()
    {
        var levels = RandomObjectsLevels();
        StackPanel content = new();
        content.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(90, 98, 110)),
            Text = "Fills the level the way the game's own levels are filled, by their measure: for the gems you ask for, about as many chests, enemies and loose gems as a level of the game with that treasure has. Most of the treasure is in chests and with enemies; loose gems, mostly reds and greens, lie in ones, twos and threes by the walls and along the way, the best at the hardest places. Press it again for another arrangement.",
        });
        ComboBox Combo(string name, IEnumerable<string> items, int selected = 0) =>
            new() { Name = name, HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = items.ToArray(), SelectedIndex = selected };
        Control Row(string label, Control control, string? tip = null)
        {
            Grid row = new() { ColumnDefinitions = new ColumnDefinitions("120,*") };
            TextBlock text = new() { Text = label, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
            if (tip != null) { ToolTip.SetTip(text, tip); ToolTip.SetTip(control, tip); }
            Grid.SetColumn(control, 1);
            row.Children.Add(text); row.Children.Add(control);
            return row;
        }
        _randomObjectsLevel = Combo("RandomObjectsLevel", levels.Select(l => l.DisplayName));
        _randomObjectsEnemyLevel = Combo("RandomObjectsEnemyLevel", new[] { "The same level" }.Concat(levels.Select(l => l.DisplayName)));
        _randomObjectsEnemyKind = Combo("RandomObjectsEnemyKind", ["All of its enemies"]);
        _randomObjectsDifficulty = Combo("RandomObjectsDifficulty", ["Easy", "Medium", "Hard", "Kaizo"], 1);
        _randomObjectsTreasure = new() { Name = "RandomObjectsTreasure", Minimum = 0, Maximum = 2000, Increment = 10, Value = 300, FormatString = "0", HorizontalAlignment = HorizontalAlignment.Stretch };
        _randomObjectsEggs = new() { Name = "RandomObjectsEggs", Minimum = 0, Maximum = 12, Increment = 1, Value = 0, FormatString = "0", HorizontalAlignment = HorizontalAlignment.Stretch };
        content.Children.Add(Row("Level type", _randomObjectsLevel, "Whose things: that level's animals, chests and gems."));
        content.Children.Add(Row("Enemy type", _randomObjectsEnemyLevel, "Whose enemies: the same level's, or another level's."));
        content.Children.Add(Row("", _randomObjectsEnemyKind, "All of that level's kinds of enemy, or one kind only."));
        content.Children.Add(Row("Difficulty", _randomObjectsDifficulty, "Easy: a few enemies, kept well back from where Spyro comes in, and plenty of animals for Sparx. Medium: as the game's own levels. Hard: many enemies waiting close together, few animals, more of the treasure behind enemies and chests. Kaizo: enemies everywhere, no animals for Sparx and no extra life."));
        content.Children.Add(Row("Gems", _randomObjectsTreasure, "The treasure the level holds in all, as the game counts it (a red gem 1, green 2, blue 5, gold 10, pink 25). Loose gems of every colour, with what the chests and enemies give, come to exactly this."));
        content.Children.Add(Row("Dragonfly eggs", _randomObjectsEggs, "Dragonfly eggs to hide in the level. They come from the Japanese version of the game: choose that disc in Add object… first."));
        CheckBox Box(string name, string text, string tip) { CheckBox box = new() { Name = name, Content = text, IsChecked = true, FontSize = 12 }; ToolTip.SetTip(box, tip); return box; }
        _randomObjectsEnemies = Box("RandomObjectsEnemies", "Enemies", "The enemy type's enemies, waiting in the rooms beyond the first and on the paths: as many as a level of the game with that many gems has, fewer when easy, more when hard.");
        _randomObjectsChests = Box("RandomObjectsChests", "Chests of every kind", "Baskets and vases, strong chests, chests that take three flames, firework and spring chests, an extra life, and a locked chest with its key.");
        _randomObjectsAnimals = Box("RandomObjectsAnimals", "Animals", "The level type's animals (sheep, rabbits, chickens…), in little groups: food for Sparx.");
        _randomObjectsThieves = Box("RandomObjectsThieves", "Egg thieves", "The egg thieves this level's place in the game has, each with a run.");
        _randomObjectsDragons = Box("RandomObjectsDragons", "Dragons", "The dragons this level's place in the game has, spread through the rooms.");
        _randomObjectsExit = Box("RandomObjectsExit", "The way out", "A Return Home vortex at the far end from where Spyro starts; in a homeworld, its balloonist and his balloon.");
        _randomObjectsScenery = Box("RandomObjectsScenery", "Trees, torches, flowers", "The level type's own scenery, placed the way that level has it: trees or cactuses on open ground or along the banks, torches and lamp posts by the edges of paths, lamps on the walls, flowers and grass in little clusters. Levels whose scenery is part of their land have none to place.");
        _randomObjectsTeal = Box("RandomObjectsTeal", "Teal gems (50)", "A rare few teal gems worth 50 each, each alone at one of the level's hardest places: on a ledge only a glide reaches, at the top of a climb, at the far end. They are part of the Gems total, which stays exact. A level needs 100 gems for one.");
        _randomObjectsRainbow = Box("RandomObjectsRainbow", "Rainbow gems (100)", "A rare few rainbow gems worth 100 each, each alone at one of the level's hardest places. They are part of the Gems total, which stays exact. A level needs 200 gems for one (300 with teal gems too).");
        _randomObjectsTeal.IsChecked = _randomObjectsRainbow.IsChecked = false;
        Grid boxes = new() { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto") };
        foreach (var (box, i) in new[] { _randomObjectsEnemies, _randomObjectsChests, _randomObjectsAnimals, _randomObjectsThieves, _randomObjectsDragons, _randomObjectsExit, _randomObjectsScenery }.Select((b, i) => (b, i)))
        { Grid.SetColumn(box, i % 2); Grid.SetRow(box, i / 2); boxes.Children.Add(box); }
        // The rare gems on a row of their own: they are off unless asked for.
        Grid.SetColumn(_randomObjectsTeal, 0); Grid.SetRow(_randomObjectsTeal, 4); boxes.Children.Add(_randomObjectsTeal);
        Grid.SetColumn(_randomObjectsRainbow, 1); Grid.SetRow(_randomObjectsRainbow, 4); boxes.Children.Add(_randomObjectsRainbow);
        content.Children.Add(boxes);
        Button randomize = NewAsyncButton("Randomize", async () => { if (ReadRandomObjectsChoice() is { } choice) await RandomizeObjectsAsync(choice); });
        randomize.Name = "RandomObjectsRandomize";
        StyleModernPrimaryButton(randomize, ModernBlue);
        Button remove = NewButton("Remove Them", () => { RemoveRandomObjects(true); });
        remove.Name = "RandomObjectsRemove";
        StyleModernSecondaryButton(remove);
        ToolTip.SetTip(remove, "Takes away everything Randomize placed. What you added yourself stays.");
        Grid buttons = new() { ColumnDefinitions = new ColumnDefinitions("*,8,*") };
        Grid.SetColumn(remove, 2);
        randomize.HorizontalAlignment = remove.HorizontalAlignment = HorizontalAlignment.Stretch;
        buttons.Children.Add(randomize); buttons.Children.Add(remove);
        content.Children.Add(buttons);
        _randomObjectsNote = new() { Name = "RandomObjectsNote", TextWrapping = TextWrapping.Wrap, FontSize = 12, IsVisible = false };
        content.Children.Add(_randomObjectsNote);
        _randomObjectsLevel.SelectionChanged += (_, _) => RefreshRandomObjectsEnemyKinds();
        _randomObjectsEnemyLevel.SelectionChanged += (_, _) => RefreshRandomObjectsEnemyKinds();
        _randomObjectsCard = TerrainCard("Random objects", content);
        _randomObjectsCard.Name = "RandomObjectsCard";
        _randomObjectsCard.IsVisible = false;
        return _randomObjectsCard;
    }

    /// <summary>The card shows in a level you built; its level type starts as the level this one takes the place of (or
    /// <paramref name="look"/>, the level a generated level was made in the style of) and its gems as that level's treasure.</summary>
    private void RefreshRandomObjectsCard(string? look = null, LevelDifficulty? difficulty = null)
    {
        if (_randomObjectsCard == null) return;
        if (difficulty is { } made && _randomObjectsDifficulty != null) _randomObjectsDifficulty.SelectedIndex = (int)made;
        _randomObjectsCard.IsVisible = _freshSceneDocument != null;
        if (_freshSceneDocument == null || _randomObjectsLevel == null) return;
        // Only when another level is opened (or a look is given): the choices stay as the user left them otherwise.
        if (look == null && _randomObjectsScene == _freshSceneDocument.Id) return;
        _randomObjectsScene = _freshSceneDocument.Id;
        var levels = RandomObjectsLevels();
        string? key = look ?? _freshSceneDocument.TargetBinding?.LevelKey;
        int at = key == null ? -1 : levels.FindIndex(l => string.Equals(l.Key, key, StringComparison.OrdinalIgnoreCase));
        if (at >= 0) _randomObjectsLevel.SelectedIndex = at;
        if (_randomObjectsNote != null) _randomObjectsNote.IsVisible = false;
        RefreshRandomObjectsEnemyKinds();
    }

    private string RandomObjectsEnemyLevelKey()
    {
        var levels = RandomObjectsLevels();
        // No levels to choose from (a project whose level list has not been read from a disc yet).
        if (levels.Count == 0) return "";
        int type = Math.Clamp(_randomObjectsLevel?.SelectedIndex ?? 0, 0, levels.Count - 1), enemy = _randomObjectsEnemyLevel?.SelectedIndex ?? 0;
        return enemy <= 0 || enemy > levels.Count ? levels[type].Key : levels[enemy - 1].Key;
    }

    /// <summary>The kinds of enemy the chosen level has, once the disc's objects have been read (until then: all of them).</summary>
    private void RefreshRandomObjectsEnemyKinds()
    {
        if (_randomObjectsEnemyKind == null) return;
        string key = RandomObjectsEnemyLevelKey();
        _randomObjectsEnemyKinds = _universalCatalog is { } ready
            ? [.. RandomObjectsEnemies(ready.Entries, key).Select(k => (k.Entry.ClassId, k.Entry.Name)).OrderBy(k => k.Name, StringComparer.OrdinalIgnoreCase)]
            : [];
        _randomObjectsEnemyKind.ItemsSource = new[] { "All of its enemies" }.Concat(_randomObjectsEnemyKinds.Select(k => k.Name)).ToArray();
        _randomObjectsEnemyKind.SelectedIndex = 0;
    }

    private static List<(UniversalMobyCatalogEntry Entry, UniversalMobyVariant Variant)> RandomObjectsEnemies(IReadOnlyList<UniversalMobyCatalogEntry> entries, string key) =>
        [.. entries.Where(e => e.Placeable && e.Category == "Enemies" && e.SubCategory != "Egg Thieves")
            .Select(e => (e, e.Variants.FirstOrDefault(v => string.Equals(v.SourceLevelKey, key, StringComparison.OrdinalIgnoreCase) && v.RestrictionReason == null)))
            .Where(p => p.Item2 != null).Select(p => (p.e, p.Item2!))];

    private RandomObjectsChoice? ReadRandomObjectsChoice()
    {
        if (_randomObjectsLevel == null) return null;
        var levels = RandomObjectsLevels();
        if (levels.Count == 0) { _statusText.Text = "Random objects needs the original Spyro the Dragon disc: open your game first."; return null; }
        string type = levels[Math.Clamp(_randomObjectsLevel.SelectedIndex, 0, levels.Count - 1)].Key;
        int kind = _randomObjectsEnemyKind?.SelectedIndex ?? 0;
        var difficulty = (LevelDifficulty)Math.Clamp(_randomObjectsDifficulty?.SelectedIndex ?? 1, 0, 3);
        return new(type, RandomObjectsEnemyLevelKey(), kind > 0 && kind <= _randomObjectsEnemyKinds.Count ? _randomObjectsEnemyKinds[kind - 1].Class : 0,
            _randomObjectsEnemies?.IsChecked == false ? 0 : RandomObjectsEnemies(difficulty, (int)(_randomObjectsTreasure?.Value ?? 300)),
            (int)(_randomObjectsTreasure?.Value ?? 300), (int)(_randomObjectsEggs?.Value ?? 0),
            _randomObjectsChests?.IsChecked == true, _randomObjectsAnimals?.IsChecked == true, _randomObjectsThieves?.IsChecked == true,
            _randomObjectsDragons?.IsChecked == true, _randomObjectsExit?.IsChecked == true, Difficulty: difficulty,
            Scenery: _randomObjectsScenery?.IsChecked == true, TealGems: _randomObjectsTeal?.IsChecked == true, RainbowGems: _randomObjectsRainbow?.IsChecked == true);
    }

    /// <summary>Takes away what Randomize placed (the objects go altogether; their save rows wait for the next
    /// Randomize). Returns how many went.</summary>
    private int RemoveRandomObjects(bool report)
    {
        if (_freshSceneDocument == null || _startBlankBusy || _createBinBusy) return 0;
        var placed = (_freshSceneDocument.RandomObjects ?? []).ToHashSet(StringComparer.Ordinal);
        var gone = _currentMobys.Where(m => placed.Contains(m.UniversalPlacementId)).ToList();
        // Their rows, where the level's ledger already knows them, can be taken again.
        var known = _freshSceneDocument.Ledger.Entries.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var rows = (_freshSceneDocument.RandomObjectRows ?? []).Concat(gone.Select(m => m.UniversalPlacementId)).Where(known.Contains).Distinct(StringComparer.Ordinal).ToArray();
        foreach (var m in gone) _currentMobys.Remove(m);
        _freshSceneDocument = _freshSceneDocument with { RandomObjects = null, RandomObjectRows = rows.Length > 0 ? rows : null };
        if (gone.Count > 0)
        {
            if (_selectedMoby != null && gone.Contains(_selectedMoby)) _selectedMoby = null;
            if (_currentLevel != null) MobyRelationshipRepair.RepairChestContentLinks(_currentLevel.Key, _currentMobys);
            InvalidateBuildSafetySummary();
            _viewport.Mobys = _currentMobys;
        }
        if (report)
        {
            RefreshPlacedObjectRoutes(); RefreshMobyList(null); RefreshCurrentLevelDetails();
            _statusText.Text = gone.Count == 0 ? "Randomize has placed nothing in this level." : $"Removed the {Count(gone.Count, "object", "objects")} Randomize placed.";
            if (_randomObjectsNote != null) _randomObjectsNote.IsVisible = false;
        }
        return gone.Count;
    }

    private async Task RandomizeObjectsAsync(RandomObjectsChoice choice)
    {
        if (_startBlankBusy || _createBinBusy || _regularEditorPersistenceBusy || _workspaceTransitionBusy || _loadingLevel) return;
        if (_freshSceneDocument == null || _currentLevel == null || _currentGeometry == null)
        { _statusText.Text = "Random objects works in a level you built: New Level, Random Level, Start Blank or a level from another game. Open one first."; return; }
        var level = _currentLevel; string root = _workspace.RootPath;
        _statusText.Text = "Reading the original disc's objects…";
        UniversalSourceLibrary lib; IReadOnlyList<UniversalMobyCatalogEntry> entries;
        try { (_, lib, entries) = await EnsureUniversalCatalogAsync(); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        { _statusText.Text = "Random objects needs the original Spyro the Dragon disc: " + e.Message; return; }
        if (_currentLevel != level || _workspace.RootPath != root || _freshSceneDocument == null) return;
        var japanese = ResolveUniversalJapaneseSource();
        var specials = UniversalSpecialCatalog.Entries(japanese != null);
        long seed = choice.Seed ?? _randomObjectsSeeds.NextInt64(1, long.MaxValue);
        var rnd = new LevelRandom(seed);

        RemoveRandomObjects(false);
        string? key = _freshSceneDocument.TargetBinding?.LevelKey;
        var slot = key == null ? null : _retailCatalog.FindByKey(key);
        var own = slot is { SourceWadEntry: > 0 } ? LevelCompletionCheck.OriginalRows(lib.Get(key!).Data) : LevelOwnRows.None;
        var live = _currentMobys.Where(m => m.IsAdded && !m.IsRemoved).ToList();
        bool Has(int row) => live.Any(m => string.Equals(m.CrossLevelSourceLevelKey, key, StringComparison.OrdinalIgnoreCase) && m.CrossLevelSourceTrueIndex == row);
        var dragonRows = choice.Dragons ? own.Dragons.Where(r => !Has(r)).ToList() : [];
        var thiefRows = choice.Thieves ? own.Thieves.Where(r => !Has(r)).ToList() : [];
        var gap = LevelCompletionCheck.Check(own, key ?? "", true, [], live.Select(m => (m.CrossLevelSourceLevelKey, m.CrossLevelSourceTrueIndex)), LevelCompletionCheck.SourceClass(lib));
        // A homeworld's way out is its balloonist, who comes with his balloon.
        (UniversalMobyCatalogEntry Entry, UniversalMobyVariant Variant)? balloonist = null;
        if (slot is { LevelId: var home } && home % 10 == 0 && home is >= 10 and <= 60 && _freshSceneDocument.WorldLevelId == null &&
            entries.FirstOrDefault(e => e.ClassId == UniversalBalloonistRows.BalloonistClass + home / 10 - 1 && e.Placeable) is { } guide &&
            guide.Variants.FirstOrDefault(v => v.RestrictionReason == null && string.Equals(v.SourceLevelKey, key, StringComparison.OrdinalIgnoreCase)) is { } his && !Has(his.SourceRow))
            balloonist = (guide, his);
        // Your world's homeworld: Artisans' balloonist (it is built on Artisans' place), unless the world is off the route.
        else if (_freshSceneDocument.WorldLevelId == NewWorldDocument.Homeworld && (NewWorldDocument.Read(root)?.Balloon?.OnRoute ?? true) &&
            entries.FirstOrDefault(e => e.ClassId == UniversalBalloonistRows.BalloonistClass && e.Placeable) is { } pilot &&
            pilot.Variants.FirstOrDefault(v => v.RestrictionReason == null && v.SourceLevelKey == NativeNewWorldExport.TemplateKey) is { } marco &&
            !live.Any(m => m.SourceByte37 == 0 && m.SourceByte36 is >= UniversalBalloonistRows.BalloonistClass and <= UniversalBalloonistRows.BalloonistClass + 5))
            balloonist = (pilot, marco);
        bool exit = choice.Exit && (gap.ExitNeeded && !gap.ExitPresent || balloonist != null);
        List<(int Row, UniversalRowTemplate Template, int Nodes)> thieves = [];
        foreach (int row in thiefRows)
        {
            var template = lib.Get(key!).Data.CaptureRow(key!, row);
            thieves.Add((row, template, UniversalPropertyCoordinates.DefaultPath(template, 0, 0, 0)?.Count ?? 0));
        }

        // A kind of object: the level's own copy first (nothing new to bring in), then the level type's, then any level's.
        (UniversalMobyCatalogEntry Entry, UniversalMobyVariant Variant)? Of(int classId, params string?[] prefer)
        {
            if (entries.FirstOrDefault(e => e.ClassId == classId && e.Placeable) is not { } entry) return null;
            var usable = entry.Variants.Where(v => v.RestrictionReason == null).ToList();
            foreach (string? level in prefer)
                if (level != null && usable.FirstOrDefault(v => string.Equals(v.SourceLevelKey, level, StringComparison.OrdinalIgnoreCase)) is { } hit) return (entry, hit);
            return usable.Count > 0 ? (entry, usable[0]) : null;
        }
        var chestClasses = new (LevelScatter.Kind Kind, int[] Classes)[]
        {
            (LevelScatter.Kind.Basket, [0x0C2]), (LevelScatter.Kind.StrongChest, [0x0C3]), (LevelScatter.Kind.FlameChest, [0x186, 0x086]), (LevelScatter.Kind.Firework, [0x138]),
            (LevelScatter.Kind.SpringChest, [0x149, 0x0C7]), (LevelScatter.Kind.LifeChest, [0x1A5]), (LevelScatter.Kind.LockedChest, [0x0AE]),
        };
        var chest = new Dictionary<LevelScatter.Kind, (UniversalMobyCatalogEntry Entry, UniversalMobyVariant Variant)>();
        if (choice.Chests)
            foreach (var (kind, classes) in chestClasses)
                if (classes.Select(c => Of(c, key, choice.LevelKey)).FirstOrDefault(v => v != null) is { } found) chest[kind] = found;
        var keyKind = Of(0x0AD, chest.TryGetValue(LevelScatter.Kind.LockedChest, out var lockedKind) ? lockedKind.Variant.SourceLevelKey : null, key, choice.LevelKey);
        if (keyKind == null) chest.Remove(LevelScatter.Kind.LockedChest);
        if (choice.Difficulty == LevelDifficulty.Kaizo) chest.Remove(LevelScatter.Kind.LifeChest);
        // Enemies: the chosen kind, or up to four of the chosen level's kinds (each kind is brought into the level whole).
        string enemyLevel = choice.EnemyLevelKey is { Length: > 0 } other ? other : choice.LevelKey;
        var foeKinds = RandomObjectsEnemies(entries, enemyLevel).Where(k => choice.EnemyClass == 0 || k.Entry.ClassId == choice.EnemyClass).OrderBy(k => k.Entry.ClassId).ToList();
        rnd.Shuffle(foeKinds);
        var animalKinds = choice.Animals
            ? entries.Where(e => e.Placeable && e.Category == "NPCs" && e.SubCategory == "Fodder & Animals" && !e.Name.Contains("cage", StringComparison.OrdinalIgnoreCase))
                .Select(e => (e, e.Variants.FirstOrDefault(v => string.Equals(v.SourceLevelKey, choice.LevelKey, StringComparison.OrdinalIgnoreCase) && v.RestrictionReason == null)))
                .Where(p => p.Item2 != null).Select(p => (Entry: p.e, Variant: p.Item2!)).OrderBy(p => p.Entry.ClassId).ToList()
            : [];
        // Scenery: the level type's own, a kind or two of each sort (every kind is brought into the level whole).
        List<(LevelScatter.Kind Kind, UniversalMobyCatalogEntry Entry, UniversalMobyVariant Variant)> sceneryKinds = [];
        if (choice.Scenery)
        {
            var all = entries.Where(e => e.Placeable && e.Category == "Props" && RandomObjectsSceneryKind(e.ClassId) != null)
                .Select(e => (Kind: RandomObjectsSceneryKind(e.ClassId)!.Value, Entry: e, Variant: e.Variants.FirstOrDefault(v => string.Equals(v.SourceLevelKey, choice.LevelKey, StringComparison.OrdinalIgnoreCase) && v.RestrictionReason == null)))
                .Where(p => p.Variant != null).OrderBy(p => p.Entry.ClassId).ToList();
            rnd.Shuffle(all);
            foreach (var group in all.GroupBy(p => p.Kind))
                sceneryKinds.AddRange(group.Take(group.Key is LevelScatter.Kind.Tree or LevelScatter.Kind.Plant ? 2 : 1).Select(p => (p.Kind, p.Entry, p.Variant!)));
        }
        // Only kinds Create BIN takes in this level. Some creatures need things of their own level that this one lacks
        // (a Haunted Towers wizard his resurrection effect), so each kind is tried on first: the level as it is, with
        // one of the kind. Kinds that are refused are left out and named.
        _statusText.Text = "Checking which objects this level can hold…";
        INativeAuthoredCompilation? compilation = null;
        try { compilation = await CaptureFreshNativeAsync(); }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException) { compilation = null; }
        if (_currentLevel != level || _workspace.RootPath != root || _freshSceneDocument == null) return;
        var special = UniversalSpecials(japanese, lib);
        var standing = CurrentUniversalPlacements();
        List<string> refusedKinds = [];
        // Kinds whose route Create BIN takes only as it came (a Misty Bog boar's): they are placed only where that
        // route, moved with them, lies on the floor.
        HashSet<(string, int)> fixedRoutes = [];
        string? Refusal(UniversalMobyVariant v, bool movedRoute = false)
        {
            if (compilation == null || UniversalSpecialCatalog.IsSpecial(v)) return null;
            byte[] row = lib.Get(v.SourceLevelKey).Data.Row(v.SourceRow);
            var one = new UniversalMobyPlacement(v.SourceLevelKey, v.SourceRow, BitConverter.ToInt32(row, 0x0C), BitConverter.ToInt32(row, 0x10), BitConverter.ToInt32(row, 0x14),
                EditorId: Guid.NewGuid().ToString("N"), PersistentSlot: standing.Count);
            if (movedRoute)
            {
                // The same placement with its route's points a step aside, as a fitted route would have them.
                List<int[]>? path;
                try { path = UniversalPropertyCoordinates.DefaultPath(lib.Get(v.SourceLevelKey).Data.CaptureRow(v.SourceLevelKey, v.SourceRow), one.X, one.Y, one.Z); }
                catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException) { path = null; }
                if (path == null) return null;
                one = one with { PathNodes = [.. path.Select(n => { var p = (int[])n.Clone(); p[0] += 16; return p; })] };
            }
            IReadOnlyList<UniversalMobyPlacement> trial;
            try { trial = UniversalPlacementGroups.Expand(lib.Get, one); } catch (InvalidDataException) { trial = [one]; }
            return DryRunFresh(compilation, lib, [.. standing, .. trial], special).Error;
        }
        // Together, too: some kinds cannot share a level (their code looks at the kind of other objects), and every kind
        // takes memory. The wanted kinds are tried all at once; when the level refuses them together they are taken on
        // one at a time (gems first, then chests, enemies, animals) and whatever will not go with the rest is left out.
        var gemKinds = GemValue.Known.Where(g => g.Value is 1 or 2 or 5 or 10 or 25).Select(g => Of(g.IdByte, key, choice.LevelKey)).Where(g => g != null).Select(g => g!.Value).ToList();
        var chestKinds = chest.ToList();
        (foeKinds, animalKinds, var keptChests, bool keyFits, sceneryKinds) = await Task.Run(() =>
        {
            string? Together(IEnumerable<UniversalMobyVariant> kinds)
            {
                if (compilation == null) return null;
                List<UniversalMobyPlacement> trial = [.. standing];
                foreach (var v in kinds.Where(v => !UniversalSpecialCatalog.IsSpecial(v)).DistinctBy(v => (v.SourceLevelKey, v.SourceRow)))
                {
                    byte[] row = lib.Get(v.SourceLevelKey).Data.Row(v.SourceRow);
                    var one = new UniversalMobyPlacement(v.SourceLevelKey, v.SourceRow, BitConverter.ToInt32(row, 0x0C), BitConverter.ToInt32(row, 0x10), BitConverter.ToInt32(row, 0x14),
                        EditorId: Guid.NewGuid().ToString("N"), PersistentSlot: trial.Count);
                    try { trial.AddRange(UniversalPlacementGroups.Expand(lib.Get, one)); } catch (InvalidDataException) { trial.Add(one); }
                }
                return DryRunFresh(compilation, lib, trial, special).Error;
            }
            var wantFoes = foeKinds.Take(4).ToList(); var wantAnimals = animalKinds.Take(2).ToList();
            var all = gemKinds.Select(g => g.Variant).Concat(chestKinds.Select(c => c.Value.Variant)).Concat(keyKind is { } k0 ? [k0.Variant] : [])
                .Concat(wantFoes.Select(f => f.Variant)).Concat(wantAnimals.Select(a => a.Variant)).Concat(sceneryKinds.Select(p => p.Variant)).ToList();
            List<(UniversalMobyCatalogEntry Entry, UniversalMobyVariant Variant)> foes = [], animals = [];
            List<(LevelScatter.Kind Kind, UniversalMobyCatalogEntry Entry, UniversalMobyVariant Variant)> scenery = [];
            var chests = new Dictionary<LevelScatter.Kind, (UniversalMobyCatalogEntry Entry, UniversalMobyVariant Variant)>();
            bool keyOk = keyKind != null;
            if (Together(all) == null) { foes = wantFoes; animals = wantAnimals; scenery = sceneryKinds; foreach (var c in chestKinds) chests[c.Key] = c.Value; }
            else
            {
                List<UniversalMobyVariant> taken = [];
                bool Take(UniversalMobyVariant v, string name)
                {
                    if (Together(taken.Append(v)) == null) { taken.Add(v); return true; }
                    lock (refusedKinds) refusedKinds.Add(name);
                    return false;
                }
                foreach (var g in gemKinds) Take(g.Variant, g.Entry.Name);
                foreach (var c in chestKinds) if (Take(c.Value.Variant, c.Value.Entry.Name)) chests[c.Key] = c.Value;
                if (keyKind is { } k1) keyOk = Take(k1.Variant, k1.Entry.Name);
                foreach (var f in foeKinds) { if (foes.Count >= 4) break; if (Take(f.Variant, f.Entry.Name)) foes.Add(f); }
                foreach (var a in animalKinds) { if (animals.Count >= 2) break; if (Take(a.Variant, a.Entry.Name)) animals.Add(a); }
                // Scenery last: what the level has room for after everything that is played with.
                foreach (var p in sceneryKinds) if (Take(p.Variant, p.Entry.Name)) scenery.Add(p);
            }
            // Whose route may not be changed.
            foreach (var kind in foes.Concat(animals))
                if (Refusal(kind.Variant) == null && Refusal(kind.Variant, movedRoute: true) != null) lock (fixedRoutes) fixedRoutes.Add((kind.Variant.SourceLevelKey, kind.Variant.SourceRow));
            return (foes, animals, chests, keyOk, scenery);
        });
        if (_currentLevel != level || _workspace.RootPath != root || _freshSceneDocument == null) return;
        chest.Clear();
        foreach (var c in keptChests) chest[c.Key] = c.Value;
        if (!keyFits) chest.Remove(LevelScatter.Kind.LockedChest);
        var eggEntry = specials.FirstOrDefault(e => e.SubCategory == "Dragonfly Eggs");
        var tealEntry = specials.FirstOrDefault(e => e.ClassId == UniversalHighValueGems.TealClass && e.Placeable);
        var rainbowEntry = specials.FirstOrDefault(e => e.ClassId == UniversalHighValueGems.RainbowClass && e.Placeable);
        int eggs = eggEntry is { Placeable: true } ? choice.Eggs : 0;

        var scene = CaptureFreshScene();
        var spare = new Queue<string>(_freshSceneDocument.RandomObjectRows ?? []);
        // A new world's homeworld is built on Artisans' place at Create BIN and has only the rows Artisans leaves.
        int taken = scene.TargetBinding?.NativePrefixRows ?? (scene.WorldLevelId == NewWorldDocument.Homeworld ? 256 - NativeNewWorldExport.HomeworldSaveRoom(lib, _retailCatalog) : 0);
        int free = Math.Max(0, 256 - taken - scene.Ledger.Entries.Count + spare.Count);
        var occupied = _currentMobys.Where(m => !m.IsRemoved).Select(m => m.Position).ToList();
        int before = _currentMobys.Where(m => !m.IsRemoved).Sum(m => m.TreasureValue);
        // Enemies that bring helpers with them take a row for each: the plan is made for fewer rows by that much.
        int helperRows = 0;
        foreach (var kind in foeKinds)
        {
            try { helperRows = Math.Max(helperRows, lib.Get(kind.Variant.SourceLevelKey).Data.CaptureRow(kind.Variant.SourceLevelKey, kind.Variant.SourceRow).SourceRowIndices.Count); }
            catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException) { }
        }
        int capacity = free;
        free = Math.Max(0, free - helperRows * (foeKinds.Count > 0 ? choice.Enemies : 0));
        var request = new LevelScatter.Request(seed, foeKinds.Count > 0 ? choice.Enemies : 0, animalKinds.Count > 0 ? LevelScatter.AnimalsFor(choice.Difficulty) : 0,
            Math.Max(0, choice.Treasure - before), Math.Max(0, free - LevelFillSpareRows))
        {
            Dragons = dragonRows.Count, ThiefRoutes = [.. thieves.Select(t => t.Nodes)], Exit = exit, ChestKinds = chest.Keys.ToHashSet(), Eggs = eggs, Difficulty = choice.Difficulty,
            // About as many of each sort as a level of the game has; fewer where the level has no room or no rows left.
            Scenery = sceneryKinds.GroupBy(p => p.Kind).ToDictionary(g => g.Key, g => g.Key switch
            {
                LevelScatter.Kind.Tree => 12, LevelScatter.Kind.Light => 10, LevelScatter.Kind.WallLight => 6, _ => 16,
            }),
            TealGems = choice.TealGems && tealEntry != null, RainbowGems = choice.RainbowGems && rainbowEntry != null,
        };
        var plan = await Task.Run(() => LevelScatter.Make(scene, request, scene.GemSpots, occupied));
        if (_currentLevel != level || _workspace.RootPath != root || _freshSceneDocument == null) return;

        _startBlankBusy = true;
        List<string> ids = [], missing = [];
        Dictionary<string, int> placed = [];
        Dictionary<Moby, int> givers = [], foeGivers = [];
        // The level's save rows: every object takes one, and some kinds bring others with them (a wizard his helpers,
        // a balloonist his balloon). What does not fit is taken out again, so the level can always be saved.
        int budget = Math.Max(0, capacity - LevelFillSpareRows), usedRows = 0, leftOut = 0;
        void Track(IReadOnlyList<Moby> objects, string what)
        {
            if (objects.Count == 0) return;
            if (usedRows + objects.Count > budget)
            {
                foreach (var m in objects) _currentMobys.Remove(m);
                leftOut++;
                return;
            }
            usedRows += objects.Count;
            foreach (var m in objects)
            {
                // A row an earlier Randomize used and gave back, while there are any.
                if (spare.Count > 0) m.UniversalPlacementId = spare.Dequeue();
                ids.Add(m.UniversalPlacementId);
            }
            placed[what] = placed.GetValueOrDefault(what) + 1;
        }
        void Give(Moby m, int gems)
        {
            var match = GemValue.Known.Where(g => g.Value == gems).ToList();
            if (match.Count == 0) return;
            m.Flag4B = match[0].IdByte; m.UniversalRewardByte = match[0].IdByte;
        }
        // An enemy's route (if its kind has one) fitted to the floor where it stands, shrunk if need be.
        List<int[]>? Route(UniversalRowTemplate template, LevelScatter.Item item, out bool fits, bool asItCame = false)
        {
            fits = true;
            var row = template.Row;
            var home = new Vector3f(BitConverter.ToInt32(row, 0x0C) / 16f, BitConverter.ToInt32(row, 0x10) / 16f, BitConverter.ToInt32(row, 0x14) / 16f);
            var raw = UniversalPropertyCoordinates.DefaultPath(template, BitConverter.ToInt32(row, 0x0C), BitConverter.ToInt32(row, 0x10), BitConverter.ToInt32(row, 0x14));
            if (raw == null) return null;
            if (asItCame)
            {
                // Its route may not be changed: it fits here only if, moved with it, every point lies on the floor.
                var moved = raw.Select(n => new Vector3f(n[0] / 16f - home.X + item.Position.X, n[1] / 16f - home.Y + item.Position.Y, n[2] / 16f - home.Z + item.Position.Z));
                fits = plan.Grid != null && plan.Grid.Holds(moved, 28);
                return null;
            }
            var fitted = plan.Grid == null ? null : LevelFill.FitRoute(plan.Grid, home, [.. raw.Select(n => new Vector3f(n[0] / 16f, n[1] / 16f, n[2] / 16f))], item.Position);
            if (fitted == null) { fits = false; return null; }
            return [.. raw.Select((n, j) => { var p = (int[])n.Clone(); p[0] = (int)MathF.Round(fitted[j].X * 16); p[1] = (int)MathF.Round(fitted[j].Y * 16); p[2] = (int)MathF.Round(fitted[j].Z * 16); return p; })];
        }
        try
        {
            var foeTemplates = foeKinds.Select(k => { try { return (UniversalRowTemplate?)lib.Get(k.Variant.SourceLevelKey).Data.CaptureRow(k.Variant.SourceLevelKey, k.Variant.SourceRow); } catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException) { return null; } }).ToList();
            var animalTemplates = animalKinds.Select(k => { try { return (UniversalRowTemplate?)lib.Get(k.Variant.SourceLevelKey).Data.CaptureRow(k.Variant.SourceLevelKey, k.Variant.SourceRow); } catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException) { return null; } }).ToList();
            int done = 0, dragon = 0, thief = 0, foe = 0, animal = 0;
            // The teal and rainbow gems first: they are part of the treasure everything else is measured against.
            foreach (var (position, value) in plan.HighGems)
            {
                var entry = value == LevelScatter.Rainbow ? rainbowEntry : tealEntry;
                string what = value == LevelScatter.Rainbow ? "rainbow gem" : "teal gem";
                var objects = entry == null ? [] : await AddUniversalMobyAtAsync(lib, entry, entry.Variants[0], position, 0);
                if (objects.Count == 0) missing.Add(what); else Track(objects, what);
            }
            foreach (var item in plan.Items)
            {
                if (++done % 4 == 1) _statusText.Text = $"Placing objects ({done} of {plan.Items.Count})…";
                // Scenery goes in last, after the gems: it takes only the save rows that are left over.
                if (item.Kind is LevelScatter.Kind.Tree or LevelScatter.Kind.Light or LevelScatter.Kind.WallLight or LevelScatter.Kind.Plant) continue;
                if (usedRows >= budget) { leftOut++; continue; }
                IReadOnlyList<Moby> objects = [];
                switch (item.Kind)
                {
                    case LevelScatter.Kind.Exit when balloonist is { } flier:
                    {
                        objects = await AddUniversalMobyAtAsync(lib, flier.Entry, flier.Variant, item.Position, item.Yaw);
                        if (objects.Count == 0) missing.Add("balloonist"); else Track(objects, "balloonist");
                        break;
                    }
                    case LevelScatter.Kind.Exit:
                    {
                        int ownExit = own.Exits.Count > 0 ? own.Exits[0] : -1;
                        (UniversalMobyCatalogEntry Entry, UniversalMobyVariant Variant)? exitKind = null;
                        if (ownExit >= 0)
                        {
                            var p = entries.SelectMany(e => e.Variants.Select(x => (e, x))).FirstOrDefault(p => string.Equals(p.x.SourceLevelKey, key, StringComparison.OrdinalIgnoreCase) && p.x.SourceRow == ownExit);
                            if (p.e != null) exitKind = (p.e, p.x);
                        }
                        exitKind ??= Of(LevelCompletionCheck.ReturnHomeClass, key, choice.LevelKey);
                        if (exitKind is { } way) objects = await AddUniversalMobyAtAsync(lib, way.Entry, way.Variant, item.Position, item.Yaw);
                        if (objects.Count == 0) missing.Add("Return Home"); else Track(objects, "Return Home vortex");
                        break;
                    }
                    case LevelScatter.Kind.Dragon when dragon < dragonRows.Count:
                    {
                        int row = dragonRows[dragon++];
                        var v = entries.SelectMany(e => e.Variants.Select(x => (e, x))).FirstOrDefault(p => string.Equals(p.x.SourceLevelKey, key, StringComparison.OrdinalIgnoreCase) && p.x.SourceRow == row);
                        if (v.e != null) objects = await AddUniversalMobyAtAsync(lib, v.e, v.x, item.Position, item.Yaw);
                        if (objects.Count == 0) missing.Add("dragon"); else Track(objects, "dragon");
                        break;
                    }
                    case LevelScatter.Kind.Thief when thief < thieves.Count:
                    {
                        var (row, template, _) = thieves[thief++];
                        var v = entries.SelectMany(e => e.Variants.Select(x => (e, x))).FirstOrDefault(p => string.Equals(p.x.SourceLevelKey, key, StringComparison.OrdinalIgnoreCase) && p.x.SourceRow == row);
                        if (v.e != null) objects = await AddUniversalMobyAtAsync(lib, v.e, v.x, item.Position, item.Yaw);
                        if (objects.Count == 0) { missing.Add("egg thief"); break; }
                        var runner = objects[0];
                        if (item.Route is { } run && UniversalPropertyCoordinates.DefaultPath(template, (int)MathF.Round(runner.Position.X * 16), (int)MathF.Round(runner.Position.Y * 16), (int)MathF.Round(runner.Position.Z * 16)) is { } original &&
                            original.Count == run.Count)
                            runner.UniversalPathNodes = [.. original.Select((n, k) => { var p = (int[])n.Clone(); p[0] = (int)MathF.Round(run[k].X * 16); p[1] = (int)MathF.Round(run[k].Y * 16); p[2] = (int)MathF.Round(run[k].Z * 16); return p; })];
                        Track(objects, "egg thief");
                        break;
                    }
                    case LevelScatter.Kind.Enemy:
                    {
                        // The next kind whose route fits here.
                        for (int k = 0; k < foeKinds.Count && objects.Count == 0; k++)
                        {
                            int at = (foe + k) % foeKinds.Count;
                            if (foeTemplates[at] is not { } template) continue;
                            var route = Route(template, item, out bool fits, fixedRoutes.Contains((foeKinds[at].Variant.SourceLevelKey, foeKinds[at].Variant.SourceRow)));
                            if (!fits) continue;
                            objects = await AddUniversalMobyAtAsync(lib, foeKinds[at].Entry, foeKinds[at].Variant, item.Position, item.Yaw);
                            if (objects.Count > 0 && route != null) objects[0].UniversalPathNodes = route;
                        }
                        foe++;
                        // An enemy starts with what it gives in its own level. One that gives a gem can give another
                        // (UniversalRewardChoice), so it takes part in the balancing below; the last in line, after the chests.
                        if (objects.Count > 0)
                        {
                            Track(objects, "enemy");
                            if (CanChooseReward(objects[0]) && LevelScatter.Colours.Contains(objects[0].TreasureValue)) foeGivers[objects[0]] = objects[0].TreasureValue;
                        }
                        break;
                    }
                    case LevelScatter.Kind.Animal:
                    {
                        for (int k = 0; k < animalKinds.Count && objects.Count == 0; k++)
                        {
                            int at = (animal + k) % animalKinds.Count;
                            if (animalTemplates[at] is not { } template) continue;
                            var route = Route(template, item, out bool fits, fixedRoutes.Contains((animalKinds[at].Variant.SourceLevelKey, animalKinds[at].Variant.SourceRow)));
                            if (!fits) continue;
                            objects = await AddUniversalMobyAtAsync(lib, animalKinds[at].Entry, animalKinds[at].Variant, item.Position, item.Yaw);
                            if (objects.Count > 0 && route != null) objects[0].UniversalPathNodes = route;
                        }
                        animal += rnd.Chance(0.3) ? 1 : 0;
                        if (objects.Count > 0) Track(objects, "animal");
                        break;
                    }
                    case LevelScatter.Kind.Key:
                        if (keyKind is { } kk) objects = await AddUniversalMobyAtAsync(lib, kk.Entry, kk.Variant, item.Position, item.Yaw);
                        if (objects.Count == 0) missing.Add("key"); else Track(objects, "key");
                        break;
                    case LevelScatter.Kind.Egg:
                        if (eggEntry != null)
                            objects = await AddUniversalMobyAtAsync(lib, eggEntry, eggEntry.Variants[rnd.Next(eggEntry.Variants.Count)], item.Position, item.Yaw, japanese);
                        if (objects.Count == 0) missing.Add("dragonfly egg"); else Track(objects, "dragonfly egg");
                        break;
                    default:
                        if (!chest.TryGetValue(item.Kind, out var c)) break;
                        objects = await AddUniversalMobyAtAsync(lib, c.Entry, c.Variant, item.Position, item.Yaw);
                        if (objects.Count == 0) { missing.Add(c.Entry.Name); break; }
                        // The chest itself is the one that stands where it was put; what it gives is set on it.
                        var body = objects.FirstOrDefault(m => !m.IsGemLike && !m.IsKey) ?? objects[0];
                        if (item.Kind is not (LevelScatter.Kind.LifeChest or LevelScatter.Kind.LockedChest)) { Give(body, item.Reward); givers[body] = item.Reward; }
                        Track(objects, item.Kind switch
                        {
                            LevelScatter.Kind.Basket => "basket", LevelScatter.Kind.StrongChest => "strong chest", LevelScatter.Kind.FlameChest => "three-flame chest",
                            LevelScatter.Kind.Firework => "firework chest", LevelScatter.Kind.SpringChest => "spring chest", LevelScatter.Kind.LifeChest => "extra life chest", _ => "locked chest",
                        });
                        break;
                }
            }
            // Enemies give what they give in their own levels, so the chests take up the difference: up or down a colour
            // at a time until the loose gems have about the share of the treasure that was planned for them. When the
            // chests are down to red gems and the loose gems still have too little (a level's enemies that all give
            // gold and pink), the enemies come down a colour at a time too.
            int planned = plan.Gems.Sum(g => g.Value);
            // One gem of every colour costs 43; with a few reds beside them the loose gems need 48 at least.
            double least = Math.Max(planned * 0.8 - 2, choice.Treasure >= 100 ? 48 : 0);
            for (int guard = 0; guard < 900 && givers.Count + foeGivers.Count > 0 && planned > 0; guard++)
            {
                int left = choice.Treasure - _currentMobys.Where(m => !m.IsRemoved).Sum(m => m.TreasureValue);
                if (left < least)
                {
                    var rich = givers.Where(g => g.Value > 1).OrderByDescending(g => g.Value).ThenBy(g => g.Key.TrueIndex).Select(g => g.Key).FirstOrDefault();
                    if (rich == null)
                    {
                        var foeRich = foeGivers.Where(g => g.Value > 1).OrderByDescending(g => g.Value).ThenBy(g => g.Key.TrueIndex).Select(g => g.Key).FirstOrDefault();
                        if (foeRich == null) break;
                        int less = LevelScatter.Colours[Array.IndexOf(LevelScatter.Colours, foeGivers[foeRich]) - 1];
                        Give(foeRich, less); foeGivers[foeRich] = less;
                        continue;
                    }
                    int lower = LevelScatter.Colours[Array.IndexOf(LevelScatter.Colours, givers[rich]) - 1];
                    Give(rich, lower); givers[rich] = lower;
                }
                else if (left > planned * 1.2 + 2)
                {
                    var poor = givers.Where(g => g.Value < 25).OrderBy(g => g.Value).ThenBy(g => g.Key.TrueIndex).Select(g => g.Key).FirstOrDefault();
                    if (poor == null) break;
                    int higher = LevelScatter.Colours[Array.IndexOf(LevelScatter.Colours, givers[poor]) + 1];
                    if (left - (higher - givers[poor]) < planned * 0.8 - 2) break;
                    Give(poor, higher); givers[poor] = higher;
                }
                else break;
            }
            // Gems last: what is in the level by now (chests, enemies, anything that was there) is counted as the editor
            // counts it, and the loose gems make up the rest of the treasure exactly, in every colour.
            int soFar = _currentMobys.Where(m => !m.IsRemoved).Sum(m => m.TreasureValue);
            int loose = choice.Treasure - soFar;
            // As many of the planned gems as the rows left hold (whole groups first, in the plan's order).
            int gemRoom = Math.Min(plan.Gems.Count, Math.Max(0, budget - usedRows));
            if (gemRoom < plan.Gems.Count) leftOut += plan.Gems.Count - gemRoom;
            // Few enough that one of every colour fits what is left for them (a pink, a gold, a blue and a green cost 38
            // more than reds): what enemies give is their own, so the loose gems' share is only known now.
            if (loose >= 48) gemRoom = Math.Min(gemRoom, Math.Max(5, loose - 38));
            var values = gemRoom > 0 && loose > 0
                ? LevelScatter.Values([.. plan.GemHardness.Take(gemRoom)], [.. plan.GemGroups.Take(gemRoom)], loose, new LevelRandom(seed ^ 0x47454D53)) : [];
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] <= 0) continue;
                if (i % 6 == 0) _statusText.Text = $"Placing gems ({i + 1} of {values.Length})…";
                int cls = GemValue.Known.First(g => g.Value == values[i]).IdByte;
                if (Of(cls, key, choice.LevelKey) is not { } gem) { missing.Add("gem"); continue; }
                var objects = await AddUniversalMobyAtAsync(lib, gem.Entry, gem.Variant, plan.Gems[i].Position, 0);
                if (objects.Count == 0) { missing.Add("gem"); continue; }
                Track(objects, values[i] switch { 1 => "red gem", 2 => "green gem", 5 => "blue gem", 10 => "gold gem", _ => "pink gem" });
            }
            // Scenery: each sort's kinds in turn.
            var sceneryTurn = new Dictionary<LevelScatter.Kind, int>();
            foreach (var item in plan.Items.Where(i => i.Kind is LevelScatter.Kind.Tree or LevelScatter.Kind.Light or LevelScatter.Kind.WallLight or LevelScatter.Kind.Plant))
            {
                var kinds = sceneryKinds.Where(p => p.Kind == item.Kind).ToList();
                if (kinds.Count == 0 || usedRows >= budget) continue;
                int turn = sceneryTurn.GetValueOrDefault(item.Kind);
                var (_, entry, variant) = kinds[turn % kinds.Count];
                // Flowers and grass of a cluster are one kind; trees take turns.
                sceneryTurn[item.Kind] = turn + (item.Kind == LevelScatter.Kind.Plant && rnd.Chance(0.7) ? 0 : 1);
                _statusText.Text = "Placing scenery…";
                var objects = await AddUniversalMobyAtAsync(lib, entry, variant, item.Position, item.Yaw);
                if (objects.Count == 0) continue;
                // A wall lamp hangs where it was put (objects are set down on the floor when added).
                if (item.Kind == LevelScatter.Kind.WallLight)
                    MoveMoby(objects[0], item.Position.X - objects[0].Position.X, item.Position.Y - objects[0].Position.Y, item.Position.Z - objects[0].Position.Z);
                Track(objects, item.Kind switch { LevelScatter.Kind.Tree => "tree", LevelScatter.Kind.Light => "torch", LevelScatter.Kind.WallLight => "wall lamp", _ => "plant" });
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException or InvalidOperationException)
        { missing.Add(e.Message); }
        finally
        {
            _startBlankBusy = false;
            _freshSceneDocument = _freshSceneDocument! with { RandomObjects = ids.Count > 0 ? [.. ids] : null, RandomObjectRows = spare.Count > 0 ? [.. spare] : null };
            RefreshPlacedObjectRoutes(); RefreshMobyList(null); RefreshCurrentLevelDetails();
        }
        // The whole arrangement once more, as Create BIN will take it.
        string? wholeRefusal = null;
        if (compilation != null && ids.Count > 0)
        {
            _statusText.Text = "Checking the level as Create BIN will build it…";
            var all = CurrentUniversalPlacements();
            wholeRefusal = await Task.Run(() => DryRunFresh(compilation, lib, all, special).Error);
            if (_currentLevel != level || _workspace.RootPath != root || _freshSceneDocument == null) return;
        }
        LastRandomObjects = placed;
        LastRandomObjectsRefusal = wholeRefusal;
        int treasure = _currentMobys.Where(m => !m.IsRemoved).Sum(m => m.TreasureValue);
        int gemCount = placed.Where(p => p.Key.EndsWith(" gem", StringComparison.Ordinal)).Sum(p => p.Value);
        var parts = placed.Where(p => !p.Key.EndsWith(" gem", StringComparison.Ordinal)).OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => p.Value == 1 ? "1 " + p.Key : $"{p.Value} {p.Key switch { "enemy" => "enemies", "egg thief" => "egg thieves", "Return Home vortex" => "Return Home vortexes", "key" => "keys", "torch" => "torches and lamps", "plant" => "flowers and grasses", var w when w.EndsWith("chest") => w + "s", var w => w + "s" }}").ToList();
        if (gemCount > 0)
            parts.Add($"{gemCount} gems ({string.Join(", ", new[] { "red", "green", "blue", "gold", "pink", "teal", "rainbow" }.Where(c => placed.ContainsKey(c + " gem")).Select(c => $"{placed[c + " gem"]} {c}"))})");
        List<string> notes = [.. plan.Notes];
        if (refusedKinds.Count > 0) notes.Add($"Left out because this level cannot hold them: {string.Join(", ", refusedKinds.Distinct())}.");
        if (wholeRefusal != null) notes.Add($"Create BIN would refuse this arrangement ({wholeRefusal}) Press Randomize again, or choose another enemy type.");
        if (leftOut > 0) notes.Add($"The level's save rows are full: {Count(leftOut, "thing was", "things were")} left out.");
        if (choice.Eggs > 0 && eggs == 0) notes.Add("Dragonfly eggs come from the Japanese version of the game: choose that disc in Add object… first.");
        if ((choice.TealGems || choice.RainbowGems) && plan.HighGems.Count == 0 && LevelScatter.HighGemsFor(request.Treasure, choice.TealGems, choice.RainbowGems) == (0, 0))
            notes.Add($"{request.Treasure} gems are too few for a teal or rainbow gem: a teal gem needs 100 (150 with rainbow gems asked for too), a rainbow gem 200 (300 with teal ones).");
        if (choice.Scenery && sceneryKinds.Count == 0 && _retailCatalog.FindByKey(choice.LevelKey) is { } sceneryLevel)
            notes.Add($"{sceneryLevel.DisplayName} has no scenery objects to place: its trees and torches are part of its land.");
        if (choice.Difficulty == LevelDifficulty.Kaizo && (choice.Animals || choice.Chests)) notes.Add("Kaizo: no animals for Sparx and no extra life.");
        if (choice.Enemies > 0 && foeKinds.Count == 0) notes.Add($"{_retailCatalog.FindByKey(enemyLevel)?.DisplayName ?? "That level"} has no enemies that can be placed.");
        if ((choice.Dragons || choice.Thieves) && own.Dragons.Count + own.Thieves.Count == 0)
            notes.Add("Dragons and egg thieves belong to a level's place in the game; this level's place has none.");
        if (treasure != choice.Treasure && choice.Treasure > 0) notes.Add($"The level's treasure comes to {treasure}.");
        string summary = parts.Count == 0
            ? "Nothing was placed" + (missing.Count > 0 ? ": " + string.Join("; ", missing.Distinct()) : ".")
            : $"Placed {string.Join(", ", parts)}. The level's treasure is {treasure}." +
              (notes.Any(n => n.Length > 0) ? " " + string.Join(" ", notes.Where(n => n.Length > 0)) : "") +
              (missing.Count > 0 ? " Not placed: " + string.Join(", ", missing.Distinct()) + "." : "");
        _statusText.Text = summary + (parts.Count > 0 ? " Randomize again for another arrangement; Save Level keeps this one." : "");
        if (_randomObjectsNote != null) { _randomObjectsNote.Text = summary; _randomObjectsNote.IsVisible = true; }
    }
}
