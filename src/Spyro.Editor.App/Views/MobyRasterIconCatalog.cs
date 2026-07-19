using System.Text;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.App.Views;

/// <summary>
/// Stable mapping between enriched Moby identities and optional release raster icons.
/// The catalog deliberately keys off the visible identity label and cross-level family,
/// not volatile type/radius bytes or true indexes. A missing PNG is supported: the
/// viewport keeps drawing its existing procedural marker.
/// </summary>
public static class MobyRasterIconCatalog
{
    public static MobyRasterIconAtlasContract InstalledAtlas1 { get; } = new(
        "moby-family-atlas-1",
        "moby-icon-atlas.png",
        columns: 4,
        rows: 4,
        usedCellCount: 14);

    public static MobyRasterIconAtlasContract InstalledAtlas2 { get; } = new(
        "moby-family-atlas-2",
        "moby-icon-atlas-2.png",
        columns: 4,
        rows: 4,
        usedCellCount: 14);

    public static MobyRasterIconAtlasContract InstalledAtlas3 { get; } = new(
        "moby-family-atlas-3",
        "moby-icon-atlas-3.png",
        columns: 4,
        rows: 4,
        usedCellCount: 12);

    private static readonly IReadOnlyList<MobyRasterIconAtlasContract> AtlasesInternal =
        [InstalledAtlas1, InstalledAtlas2, InstalledAtlas3];

    private static readonly IReadOnlyList<MobyRasterIconDefinition> DefinitionsInternal =
    [
        // Keep the more-specific train/cage/barrel identities before their broader siblings.
        new("tnt-train-car", "TNT train car / cluster", "tnt-train-car.png", InstalledAtlas1, 13,
            ["train piece tnt", "train piece tnts", "train part tnt", "train car tnt", "tnt train car", "tnt train cluster"]),
        new("barrel-supply-hatch", "Barrel supply hatch", "barrel-supply-hatch.png", InstalledAtlas1, 2,
            ["barrel supply hatch", "barrel supply"]),
        new("steel-barrel", "Steel barrel", "steel-barrel.png", InstalledAtlas1, 11,
            ["steel barrel", "steel barrel shell"]),
        new("fairy-cage", "Fairy cage", "fairy-cage.png", InstalledAtlas1, 7,
            ["fairy cage", "caged fairy cage", "cage prop"]),
        new("caged-fairy", "Caged Fairy", "caged-fairy.png", InstalledAtlas1, 6,
            ["caged fairy", "fairy rescue actor"]),
        new("shielded-greenie", "Shielded Greenie", "shielded-greenie.png", InstalledAtlas1, 0,
            ["shielded greenie"]),
        new("tin-soldier", "Tin Soldier", "tin-soldier.png", InstalledAtlas1, 1,
            ["tin soldier"]),
        new("power-pole", "Power pole", "power-pole.png", InstalledAtlas1, 3,
            ["power pole", "power pylon"]),
        new("volt-shooter", "Volt Shooter", "volt-shooter.png", InstalledAtlas1, 4,
            ["volt shooter"]),
        new("flight-lighthouse", "Flight lighthouse", "flight-lighthouse.png", InstalledAtlas1, 5,
            ["flight lighthouse", "lighthouse target", "lighthouse"]),
        new("clock-fool", "Clock Fool", "clock-fool.png", InstalledAtlas1, 8,
            ["clock fool"]),
        new("metal-door", "Metal door", "metal-door.png", InstalledAtlas1, 9,
            ["metal door"]),
        new("fat-bat", "Fat Bat", "fat-bat.png", InstalledAtlas1, 10,
            ["fat bat"]),
        new("train", "Train", "train.png", InstalledAtlas1, 12,
            ["train piece", "train part", "train car", "flight train", "train target", "train"]),

        // Atlas 2 covers only records that would otherwise reach one of the four
        // renderer-generic fallbacks. This gate is essential: phrase containment
        // alone would also catch linked Floor Shocker/Super Grenadier controls and
        // unrelated transport/Balloognorc balloon components.
        new("fat-claw-monster", "Fat Claw Monster", "fat-claw-monster.png", InstalledAtlas2, 0,
            ["fat claw monster"], GenericFallbackOnly: true),
        new("barrel-engineer", "Barrel Engineer", "barrel-engineer.png", InstalledAtlas2, 1,
            ["barrel engineer"], GenericFallbackOnly: true),
        // This exact Atlas 3 identity must precede Atlas 2's broader one-token
        // Beast phrase. Both native and imported CrossLevelFamily text use the
        // same token-bounded matcher.
        new("beast-makers-banner", "Beast Makers banner", "beast-makers-banner.png", InstalledAtlas3, 0,
            ["beast makers banner"], GenericFallbackOnly: true),
        new("beast", "Beast", "beast.png", InstalledAtlas2, 2,
            ["beast"], GenericFallbackOnly: true, ExcludedMatchPhrases: ["beast makers banner"]),
        new("dockworker-tnt-wrangler", "Dockworker / TNT Wrangler", "dockworker-tnt-wrangler.png", InstalledAtlas2, 3,
            ["dockworker tnt wrangler"], GenericFallbackOnly: true),
        new("lantern-post", "Lantern post", "lantern-post.png", InstalledAtlas2, 4,
            ["lantern post"], GenericFallbackOnly: true),
        new("bull", "Bull", "bull.png", InstalledAtlas2, 5,
            ["bull"], GenericFallbackOnly: true),
        new("ram", "Ram", "ram.png", InstalledAtlas2, 6,
            ["ram"], GenericFallbackOnly: true),
        new("floor-shocker", "Floor Shocker", "floor-shocker.png", InstalledAtlas2, 7,
            ["floor shocker"], GenericFallbackOnly: true),
        new("metal-claw-monster", "Metal Claw Monster", "metal-claw-monster.png", InstalledAtlas2, 8,
            ["metal claw monster"], GenericFallbackOnly: true),
        new("shepherd", "Shepherd / Shepard", "shepherd.png", InstalledAtlas2, 9,
            ["shepard", "shepherd"], GenericFallbackOnly: true),
        new("summoning-wizard", "Summoning Wizard", "summoning-wizard.png", InstalledAtlas2, 10,
            ["summoning wizard"], GenericFallbackOnly: true),
        new("flight-direction-arrow-sign", "Flight direction arrow sign", "flight-direction-arrow-sign.png", InstalledAtlas2, 11,
            ["flight direction arrow sign"], GenericFallbackOnly: true),
        new("superflame-fairy", "Superflame Fairy", "superflame-fairy.png", InstalledAtlas2, 12,
            ["superflame fairy"], GenericFallbackOnly: true),
        new("armored-gnorc", "Armored Gnorc", "armored-gnorc.png", InstalledAtlas2, 13,
            ["armored gnorc"], GenericFallbackOnly: true),

        // Atlas 3 resolves the final 29 records that previously used a generic
        // tree, Gnorc, or actor-triangle marker. Keep the generic-fallback gate:
        // Super Grenadier and balloon phrases also occur on linked/control or
        // transport-component records which already have a specific renderer.
        new("crocodile", "Crocodile", "crocodile.png", InstalledAtlas3, 1,
            ["crocodile"], GenericFallbackOnly: true),
        new("ice-stalactite", "Ice stalactite", "ice-stalactite.png", InstalledAtlas3, 2,
            ["ice stalactite"], GenericFallbackOnly: true),
        new("rescue-fairy", "Rescue Fairy", "rescue-fairy.png", InstalledAtlas3, 3,
            ["rescue fairy"], GenericFallbackOnly: true),
        new("super-grenadier", "Super Grenadier", "super-grenadier.png", InstalledAtlas3, 4,
            ["super grenadier"], GenericFallbackOnly: true),
        new("campfire", "Campfire", "campfire.png", InstalledAtlas3, 5,
            ["campfire"], GenericFallbackOnly: true),
        new("chicken-cage", "Chicken cage", "chicken-cage.png", InstalledAtlas3, 6,
            ["chicken cage"], GenericFallbackOnly: true),
        new("drawbridge-lever", "Drawbridge lever", "drawbridge-lever.png", InstalledAtlas3, 7,
            ["drawbridge lever"], GenericFallbackOnly: true),
        new("wall-lantern", "Wall lantern", "wall-lantern.png", InstalledAtlas3, 8,
            ["wall lantern"], GenericFallbackOnly: true),
        new("arrow-sign-fairy", "Arrow-sign Fairy", "arrow-sign-fairy.png", InstalledAtlas3, 9,
            ["arrow sign fairy"], GenericFallbackOnly: true),
        new("balloon", "Balloon", "balloon.png", InstalledAtlas3, 10,
            ["ballon", "balloon"], GenericFallbackOnly: true),
        new("metalhead-boss", "Metalhead boss", "metalhead-boss.png", InstalledAtlas3, 11,
            ["metalhead boss"], GenericFallbackOnly: true)
    ];

    public static IReadOnlyList<MobyRasterIconAtlasContract> Atlases => AtlasesInternal;
    public static IReadOnlyList<MobyRasterIconDefinition> Definitions => DefinitionsInternal;

    public static bool TryMatch(Moby moby, out MobyRasterIconDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(moby);
        MobyGenericMarkerFallbackKind fallbackKind = EditorViewport.ClassifyGenericMarkerFallback(moby);
        return TryMatch(moby.DisplayLabel, moby.CrossLevelFamily, fallbackKind, out definition);
    }

    public static bool TryMatch(
        string? displayLabel,
        string? crossLevelFamily,
        out MobyRasterIconDefinition definition) =>
        TryMatch(displayLabel, crossLevelFamily, MobyGenericMarkerFallbackKind.None, out definition);

    public static bool TryMatch(
        string? displayLabel,
        string? crossLevelFamily,
        MobyGenericMarkerFallbackKind fallbackKind,
        out MobyRasterIconDefinition definition)
    {
        string normalizedLabel = Normalize(displayLabel);
        string normalizedFamily = Normalize(crossLevelFamily);

        foreach (MobyRasterIconDefinition candidate in DefinitionsInternal)
        {
            if (candidate.GenericFallbackOnly && fallbackKind == MobyGenericMarkerFallbackKind.None)
                continue;
            if (candidate.ExcludedMatchPhrasesOrEmpty.Any(phrase =>
                    ContainsPhrase(normalizedLabel, phrase) || ContainsPhrase(normalizedFamily, phrase)))
            {
                continue;
            }
            if (candidate.MatchPhrases.Any(phrase => ContainsPhrase(normalizedLabel, phrase)) ||
                candidate.MatchPhrases.Any(phrase => ContainsPhrase(normalizedFamily, phrase)))
            {
                definition = candidate;
                return true;
            }
        }

        definition = null!;
        return false;
    }

    public static string GetAssetPath(string baseDirectory, MobyRasterIconDefinition definition) =>
        Path.Combine(baseDirectory, "Assets", "MobyIcons", definition.FileName);

    public static string GetAtlasAssetPath(string baseDirectory, MobyRasterIconAtlasContract atlas) =>
        Path.Combine(baseDirectory, "Assets", "MobyIcons", atlas.FileName);

    private static bool ContainsPhrase(string normalizedText, string phrase)
    {
        if (string.IsNullOrEmpty(normalizedText))
            return false;

        string normalizedPhrase = Normalize(phrase);
        return normalizedText.Equals(normalizedPhrase, StringComparison.Ordinal) ||
            $" {normalizedText} ".Contains($" {normalizedPhrase} ", StringComparison.Ordinal);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        StringBuilder result = new(value.Length);
        bool pendingSpace = false;
        foreach (char character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                if (pendingSpace && result.Length > 0)
                    result.Append(' ');
                result.Append(char.ToLowerInvariant(character));
                pendingSpace = false;
            }
            else
            {
                pendingSpace = true;
            }
        }

        return result.ToString();
    }
}

public sealed record MobyRasterIconDefinition(
    string Key,
    string DisplayName,
    string FileName,
    MobyRasterIconAtlasContract Atlas,
    int AtlasCell,
    IReadOnlyList<string> MatchPhrases,
    bool GenericFallbackOnly = false,
    IReadOnlyList<string>? ExcludedMatchPhrases = null)
{
    public MobyRasterIconAtlasCellId AtlasCellId => new(Atlas.Key, AtlasCell);
    public IReadOnlyList<string> ExcludedMatchPhrasesOrEmpty => ExcludedMatchPhrases ?? Array.Empty<string>();
}

/// <summary>
/// Describes one generated icon sheet independently of the families that use it.
/// UsedCellCount marks the leading subject cells; every remaining cell is an
/// explicit empty background sample used by the in-memory alpha-mask loader.
/// </summary>
public sealed record MobyRasterIconAtlasContract
{
    public MobyRasterIconAtlasContract(
        string key,
        string fileName,
        int columns,
        int rows,
        int usedCellCount)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("An atlas key is required.", nameof(key));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("An atlas file name is required.", nameof(fileName));
        if (columns <= 0)
            throw new ArgumentOutOfRangeException(nameof(columns));
        if (rows <= 0)
            throw new ArgumentOutOfRangeException(nameof(rows));

        int cellCount = checked(columns * rows);
        if (usedCellCount < 0 || usedCellCount >= cellCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(usedCellCount),
                "A generated atlas must leave at least one trailing cell empty for background sampling.");
        }

        Key = key;
        FileName = fileName;
        Columns = columns;
        Rows = rows;
        UsedCellCount = usedCellCount;
    }

    public string Key { get; }
    public string FileName { get; }
    public int Columns { get; }
    public int Rows { get; }
    public int UsedCellCount { get; }
    public int CellCount => Columns * Rows;
    public int EmptyCellCount => CellCount - UsedCellCount;

    public bool ContainsCell(int atlasCell) => atlasCell >= 0 && atlasCell < CellCount;
    public bool ContainsUsedCell(int atlasCell) => atlasCell >= 0 && atlasCell < UsedCellCount;
}

public readonly record struct MobyRasterIconAtlasCellId(string AtlasKey, int AtlasCell)
{
    public override string ToString() => $"{AtlasKey}:{AtlasCell}";
}
