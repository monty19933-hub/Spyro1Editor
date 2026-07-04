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
            Entry("artisans", "Artisans", "Artisans", "ARTISANS"),
            Entry("stonehill", "StoneHill", "Stone Hill", "STONE HILL"),
            Entry("darkhollow", "DarkHollow", "Dark Hollow", "DARK HOLLOW"),
            Entry("townsquare", "TownSquare", "Town Square", "TOWN SQUARE"),
            Entry("sunnyflight", "SunnyFlight", "Sunny Flight", "SUNNY FLIGHT"),
            Entry("drycanyon", "DryCanyon", "Dry Canyon", "DRY CANYON"),
            Entry("clifftown", "CliffTown", "Cliff Town", "CLIFF TOWN"),
            Entry("icecavern", "IceCavern", "Ice Cavern", "ICE CAVERN"),
            Entry("doctorshemp", "DoctorShemp", "Doctor Shemp", "DOCTOR SHEMP"),
            Entry("nightflight", "NightFlight", "Night Flight", "NIGHT FLIGHT"),
            Entry("peacekeepers", "PeaceKeepers", "Peace Keepers", "PEACE KEEPERS"),
            Entry("magiccrafters", "MagicCrafters", "Magic Crafters", "MAGIC CRAFTERS"),
            Entry("alpineridge", "AlpineRidge", "Alpine Ridge", "ALPINE RIDGE"),
            Entry("highcaves", "HighCaves", "High Caves", "HIGH CAVES"),
            Entry("wizardpeak", "WizardPeak", "Wizard Peak", "WIZARD PEAK"),
            Entry("blowhard", "Blowhard", "Blowhard", "BLOWHARD"),
            Entry("crystalflight", "CrystalFlight", "Crystal Flight", "CRYSTAL FLIGHT"),
            Entry("beastmakers", "BeastMakers", "Beast Makers", "BEAST MAKERS"),
            Entry("terracevillage", "TerraceVillage", "Terrace Village", "TERRACE VILLAGE"),
            Entry("mistybog", "MistyBog", "Misty Bog", "MISTY BOG"),
            Entry("treetops", "TreeTops", "Tree Tops", "TREE TOPS"),
            Entry("metalhead", "Metalhead", "Metalhead", "METALHEAD"),
            Entry("wildflight", "WildFlight", "Wild Flight", "WILD FLIGHT"),
            Entry("dreamweavers", "DreamWeavers", "Dream Weavers", "DREAM WEAVERS"),
            Entry("darkpassage", "DarkPassage", "Dark Passage", "DARK PASSAGE"),
            Entry("loftycastle", "LoftyCastle", "Lofty Castle", "LOFTY CASTLE"),
            Entry("hauntedtowers", "HauntedTowers", "Haunted Towers", "HAUNTED TOWERS"),
            Entry("icyflight", "IcyFlight", "Icy Flight", "ICY FLIGHT"),
            Entry("gnastysworld", "GnastyWorld", "Gnasty's World", "GNASTY'S WORLD"),
            Entry("gnorccove", "GnorcCove", "Gnorc Cove", "GNORC COVE"),
            Entry("twilightharbor", "TwilightHarbor", "Twilight Harbor", "TWILIGHT HARBOR"),
            Entry("gnastygnorc", "GnastyGnorc", "Gnasty Gnorc", "GNASTY GNORC"),
            Entry("gnastysloot", "GnastyLoot", "Gnasty's Loot", "GNASTY'S LOOT")
        ]);
    }

    public static bool IsSafeReplacement(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string replacement = NormalizeReplacement(value);
        return replacement.Length <= maxLength
            && replacement.All(ch => ch is >= 'A' and <= 'Z' || ch is >= '0' and <= '9' || ch is '\'' or ' ' or '.' or '!' or '?' or '&' or '-');
    }

    public static string NormalizeReplacement(string value) => value.Trim().ToUpperInvariant();

    private static TextTargetEntry Entry(string levelKey, string scriptKey, string displayName, string originalText)
    {
        return new TextTargetEntry(levelKey, scriptKey, displayName, originalText, originalText.Length);
    }

    private void AddKey(string value, TextTargetEntry target)
    {
        string key = LevelCatalog.NormalizeKey(value);
        if (!string.IsNullOrWhiteSpace(key))
            _byKey[key] = target;
    }
}

public sealed record TextTargetEntry(string LevelKey, string ScriptKey, string DisplayName, string OriginalText, int MaxLength);
