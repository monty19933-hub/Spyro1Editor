using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Text;

public sealed class TextTargetCatalog
{
    private readonly IReadOnlyList<TextTargetEntry> _targets;
    private readonly Dictionary<string, TextTargetEntry> _byKey;

    private TextTargetCatalog(IEnumerable<TextTargetEntry> targets)
    {
        _targets = targets.ToArray();
        _byKey = new Dictionary<string, TextTargetEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (TextTargetEntry target in _targets)
        {
            AddKey(target.LevelKey, target);
            AddKey(target.ScriptKey, target);
            AddKey(target.DisplayName, target);
            AddKey(target.OriginalText, target);
        }
    }

    public IReadOnlyList<TextTargetEntry> Targets => _targets;

    public TextTargetEntry? FindForLevel(LevelDefinition level)
    {
        return Find(level.Key)
            ?? Find(level.ScriptKey)
            ?? Find(level.DisplayName);
    }

    public TextTargetEntry? Find(string value)
    {
        _byKey.TryGetValue(LevelCatalog.NormalizeKey(value), out TextTargetEntry? target);
        return target;
    }

    public static TextTargetCatalog CreateDefault()
    {
        return new TextTargetCatalog([
            HomeworldEntry("artisans", "Artisans", "Artisans", "ARTISANS", 0),
            LevelEntry("stonehill", "StoneHill", "Stone Hill", "STONE HILL", 1),
            LevelEntry("darkhollow", "DarkHollow", "Dark Hollow", "DARK HOLLOW", 2),
            LevelEntry("townsquare", "TownSquare", "Town Square", "TOWN SQUARE", 3),
            LevelEntry("toasty", "Toasty", "Toasty", "TOASTY", 4),
            LevelEntry("sunnyflight", "SunnyFlight", "Sunny Flight", "SUNNY FLIGHT", 5),

            HomeworldEntry("peacekeepers", "PeaceKeepers", "Peace Keepers", "PEACE KEEPERS", 1),
            LevelEntry("drycanyon", "DryCanyon", "Dry Canyon", "DRY CANYON", 7),
            LevelEntry("clifftown", "CliffTown", "Cliff Town", "CLIFF TOWN", 8),
            LevelEntry("icecavern", "IceCavern", "Ice Cavern", "ICE CAVERN", 9),
            LevelEntry("doctorshemp", "DoctorShemp", "Doctor Shemp", "DOCTOR SHEMP", 10),
            LevelEntry("nightflight", "NightFlight", "Night Flight", "NIGHT FLIGHT", 11),

            HomeworldEntry("magiccrafters", "MagicCrafters", "Magic Crafters", "MAGIC CRAFTERS", 2),
            LevelEntry("alpineridge", "AlpineRidge", "Alpine Ridge", "ALPINE RIDGE", 13),
            LevelEntry("highcaves", "HighCaves", "High Caves", "HIGH CAVES", 14),
            LevelEntry("wizardpeak", "WizardPeak", "Wizard Peak", "WIZARD PEAK", 15),
            LevelEntry("blowhard", "Blowhard", "Blowhard", "BLOWHARD", 16),
            LevelEntry("crystalflight", "CrystalFlight", "Crystal Flight", "CRYSTAL FLIGHT", 17),

            HomeworldEntry("beastmakers", "BeastMakers", "Beast Makers", "BEAST MAKERS", 3),
            LevelEntry("terracevillage", "TerraceVillage", "Terrace Village", "TERRACE VILLAGE", 19),
            LevelEntry("mistybog", "MistyBog", "Misty Bog", "MISTY BOG", 20),
            LevelEntry("treetops", "TreeTops", "Tree Tops", "TREE TOPS", 21),
            LevelEntry("metalhead", "Metalhead", "Metalhead", "METALHEAD", 22),
            LevelEntry("wildflight", "WildFlight", "Wild Flight", "WILD FLIGHT", 23),

            HomeworldEntry("dreamweavers", "DreamWeavers", "Dream Weavers", "DREAM WEAVERS", 4),
            LevelEntry("darkpassage", "DarkPassage", "Dark Passage", "DARK PASSAGE", 25),
            LevelEntry("loftycastle", "LoftyCastle", "Lofty Castle", "LOFTY CASTLE", 26),
            LevelEntry("hauntedtowers", "HauntedTowers", "Haunted Towers", "HAUNTED TOWERS", 27),
            LevelEntry("jacques", "Jacques", "Jacques", "JACQUES", 28),
            LevelEntry("icyflight", "IcyFlight", "Icy Flight", "ICY FLIGHT", 29),

            HomeworldEntry("gnastysworld", "GnastyWorld", "Gnasty's World", "GNASTY'S WORLD", 5),
            LevelEntry("gnorccove", "GnorcCove", "Gnorc Cove", "GNORC COVE", 31),
            LevelEntry("twilightharbor", "TwilightHarbor", "Twilight Harbor", "TWILIGHT HARBOR", 32),
            LevelEntry("gnastygnorc", "GnastyGnorc", "Gnasty Gnorc", "GNASTY GNORC", 33),
            LevelEntry("gnastysloot", "GnastyLoot", "Gnasty's Loot", "GNASTY'S LOOT", 34)
        ]);
    }

    public static bool IsSafeReplacement(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string replacement = NormalizeReplacement(value);
        return replacement.Length <= maxLength
            && replacement.All(ch => ch is >= 'A' and <= 'Z' || ch is '\'' or ' ');
    }

    public static string NormalizeReplacement(string value) => value.Trim().ToUpperInvariant();

    private static TextTargetEntry HomeworldEntry(
        string levelKey,
        string scriptKey,
        string displayName,
        string originalText,
        int tableIndex) =>
        new(levelKey, scriptKey, displayName, originalText, originalText.Length, 19, LevelTextTableKind.HomeworldNames, tableIndex);

    private static TextTargetEntry LevelEntry(
        string levelKey,
        string scriptKey,
        string displayName,
        string originalText,
        int tableIndex) =>
        new(
            levelKey,
            scriptKey,
            displayName,
            originalText,
            originalText.Length,
            IsConfrontingLevelSlot(tableIndex) ? 16 : 19,
            LevelTextTableKind.LevelNames,
            tableIndex);

    private static bool IsConfrontingLevelSlot(int tableIndex) =>
        tableIndex is 4 or 10 or 16 or 22 or 28 or 33;

    private void AddKey(string value, TextTargetEntry target)
    {
        string key = LevelCatalog.NormalizeKey(value);
        if (!string.IsNullOrWhiteSpace(key))
            _byKey[key] = target;
    }
}

public enum LevelTextTableKind
{
    HomeworldNames,
    LevelNames
}

public sealed record TextTargetEntry(
    string LevelKey,
    string ScriptKey,
    string DisplayName,
    string OriginalText,
    int OriginalSlotLength,
    int MaxLength,
    LevelTextTableKind TableKind,
    int TableIndex)
{
    public bool AffectsPortalLettering => TableKind == LevelTextTableKind.LevelNames;

    public override string ToString() => DisplayName;
}
