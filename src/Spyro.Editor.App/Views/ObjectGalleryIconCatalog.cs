using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Spyro.Editor.App.Views;

/// <summary>
/// Selector-only generated artwork for objects that previously used the viewport's
/// small procedural markers. This catalog is deliberately independent from
/// <see cref="MobyRasterIconCatalog"/> so gallery art cannot change map-marker
/// identity matching or the source-locked viewport atlas contracts.
/// </summary>
internal static class ObjectGalleryIconCatalog
{
    private static readonly MobyRasterIconAtlasContract CoreAtlas = new(
        "object-gallery-core",
        "object-gallery-atlas-core.png",
        columns: 4,
        rows: 4,
        usedCellCount: 14);

    private static readonly MobyRasterIconAtlasContract EnemiesAAtlas = new(
        "object-gallery-enemies-a",
        "object-gallery-atlas-enemies-a.png",
        columns: 4,
        rows: 4,
        usedCellCount: 14);

    private static readonly MobyRasterIconAtlasContract EnemiesBAtlas = new(
        "object-gallery-enemies-b",
        "object-gallery-atlas-enemies-b.png",
        columns: 4,
        rows: 4,
        usedCellCount: 14);

    private static readonly MobyRasterIconAtlasContract EnemiesCAtlas = new(
        "object-gallery-enemies-c",
        "object-gallery-atlas-enemies-c.png",
        columns: 4,
        rows: 4,
        usedCellCount: 14);

    private static readonly MobyRasterIconAtlasContract SceneryDAtlas = new(
        "object-gallery-scenery-d",
        "object-gallery-atlas-scenery-d.png",
        columns: 4,
        rows: 4,
        usedCellCount: 14);

    private static readonly MobyRasterIconAtlasContract SceneryEAtlas = new(
        "object-gallery-scenery-e",
        "object-gallery-atlas-scenery-e.png",
        columns: 4,
        rows: 4,
        usedCellCount: 8);

    private static readonly MobyRasterIconAtlasContract LandmarksFAtlas = new(
        "object-gallery-landmarks-f",
        "object-gallery-atlas-landmarks-f.png",
        columns: 4,
        rows: 4,
        usedCellCount: 12);

    private static readonly MobyRasterIconAtlasContract ObjectsGAtlas = new(
        "object-gallery-objects-g",
        "object-gallery-atlas-objects-g.png",
        columns: 4,
        rows: 4,
        usedCellCount: 14);

    private static readonly MobyRasterIconAtlasContract ObjectsHAtlas = new(
        "object-gallery-objects-h",
        "object-gallery-atlas-objects-h.png",
        columns: 4,
        rows: 4,
        usedCellCount: 4);

    private static readonly MobyRasterIconAtlasContract ObjectsIAtlas = new(
        "object-gallery-objects-i",
        "object-gallery-atlas-objects-i.png",
        columns: 4,
        rows: 4,
        usedCellCount: 2);

    private static readonly MobyRasterIconAtlasContract ObjectsJAtlas = new(
        "object-gallery-objects-j",
        "object-gallery-atlas-objects-j.png",
        columns: 4,
        rows: 4,
        usedCellCount: 2);

    private static readonly MobyRasterIconAtlasContract ObjectsKAtlas = new(
        "object-gallery-objects-k",
        "object-gallery-atlas-objects-k.png",
        columns: 4,
        rows: 4,
        usedCellCount: 2);

    private static readonly IReadOnlyList<MobyRasterIconAtlasContract> AtlasesInternal =
    [
        CoreAtlas,
        EnemiesAAtlas,
        EnemiesBAtlas,
        EnemiesCAtlas,
        SceneryDAtlas,
        SceneryEAtlas,
        LandmarksFAtlas,
        ObjectsGAtlas,
        ObjectsHAtlas,
        ObjectsIAtlas,
        ObjectsJAtlas,
        ObjectsKAtlas
    ];

    private static readonly IReadOnlyDictionary<string, ObjectGalleryIconDefinition> Definitions =
        new Dictionary<string, ObjectGalleryIconDefinition>(StringComparer.Ordinal)
        {
            // Universal collectibles, chests, and simple scenery.
            ["gem treasure"] = Icon("gem-treasure", CoreAtlas, 0),
            ["key"] = Icon("key", CoreAtlas, 1),
            ["key locked chest"] = Icon("key-locked-chest", CoreAtlas, 2),
            ["flame charge chest"] = Icon("flame-charge-chest", CoreAtlas, 3),
            ["charge chest"] = Icon("charge-chest", CoreAtlas, 4),
            ["life chest"] = Icon("life-chest", CoreAtlas, 5),
            ["spring chest"] = Icon("spring-chest", CoreAtlas, 6),
            ["firework chest"] = Icon("firework-chest", CoreAtlas, 7),
            ["super flame chest"] = Icon("super-flame-chest", CoreAtlas, 8),
            ["3x flame chest"] = Icon("three-hit-chest", CoreAtlas, 9),
            ["lamp scenery"] = Icon("lamp-scenery", CoreAtlas, 13),

            // Enemy atlas A.
            ["agile snowballer"] = Icon("agile-snowballer", EnemiesAAtlas, 0),
            ["armored banana boy"] = Icon("armored-banana-boy", EnemiesAAtlas, 1),
            ["armored druid"] = Icon("armored-druid", EnemiesAAtlas, 2),
            ["attack frog"] = Icon("attack-frog", EnemiesAAtlas, 3),
            ["balloognorc"] = Icon("balloognorc", EnemiesAAtlas, 4),
            ["balloognorc balloon"] = Icon("balloognorc-balloon", EnemiesAAtlas, 5),
            ["banana boy"] = Icon("banana-boy", EnemiesAAtlas, 6),
            ["bat"] = Icon("bat", EnemiesAAtlas, 7),
            ["big armor gnorc"] = Icon("big-armor-gnorc", EnemiesAAtlas, 8),
            ["big gnorc w bird"] = Icon("big-gnorc-with-bird", EnemiesAAtlas, 9),
            ["bird"] = Icon("bird", EnemiesAAtlas, 10),
            ["boar"] = Icon("boar", EnemiesAAtlas, 11),
            ["cannon gnorc"] = Icon("cannon-gnorc", EnemiesAAtlas, 12),
            ["chicken"] = Icon("chicken", EnemiesAAtlas, 13),

            // Enemy atlas B.
            ["devil cupid"] = Icon("devil-cupid", EnemiesBAtlas, 0),
            ["dog"] = Icon("dog", EnemiesBAtlas, 1),
            ["dragon eating plant"] = Icon("dragon-eating-plant", EnemiesBAtlas, 2),
            ["egg thief"] = Icon("egg-thief", EnemiesBAtlas, 3),
            ["elder wizard"] = Icon("elder-wizard", EnemiesBAtlas, 4),
            ["electro gnorc"] = Icon("electro-gnorc", EnemiesBAtlas, 5),
            ["fat momma cauldron"] = Icon("fat-momma-cauldron", EnemiesBAtlas, 6),
            ["fodder"] = Icon("fodder", EnemiesBAtlas, 7),
            ["frilled lizard"] = Icon("frilled-lizard", EnemiesBAtlas, 8),
            ["giant boar"] = Icon("giant-boar", EnemiesBAtlas, 9),
            ["goat sheep"] = Icon("goat-sheep", EnemiesBAtlas, 10),
            ["green druid"] = Icon("green-druid", EnemiesBAtlas, 11),
            ["kamikaze gnorc"] = Icon("kamikaze-gnorc", EnemiesBAtlas, 12),
            ["lamp fool"] = Icon("lamp-fool", EnemiesBAtlas, 13),

            // Enemy/scenery atlas C.
            ["large alpine tree"] = Icon("large-alpine-tree", EnemiesCAtlas, 0),
            ["large gnorc"] = Icon("large-gnorc", EnemiesCAtlas, 1),
            ["laser gnorc"] = Icon("laser-gnorc", EnemiesCAtlas, 2),
            ["metalback spider"] = Icon("metalback-spider", EnemiesCAtlas, 3),
            ["mushroom"] = Icon("mushroom", EnemiesCAtlas, 4),
            ["puppy devil dog"] = Icon("puppy-devil-dog", EnemiesCAtlas, 5),
            ["rabbit"] = Icon("rabbit", EnemiesCAtlas, 6),
            ["rat"] = Icon("rat", EnemiesCAtlas, 7),
            ["red flag"] = Icon("red-flag", EnemiesCAtlas, 8),
            ["regular gnorc"] = Icon("regular-gnorc", EnemiesCAtlas, 9),
            ["slender alpine tree"] = Icon("slender-alpine-tree", EnemiesCAtlas, 10),
            ["small gnorc"] = Icon("small-gnorc", EnemiesCAtlas, 11),
            ["small swaying alpine tree"] = Icon("small-swaying-alpine-tree", EnemiesCAtlas, 12),
            ["snow gnorc"] = Icon("snow-gnorc", EnemiesCAtlas, 13),

            // Enemy/scenery atlas D.
            ["snowballer"] = Icon("snowballer", SceneryDAtlas, 0),
            ["spear gnorc"] = Icon("spear-gnorc", SceneryDAtlas, 1),
            ["spotted chicken"] = Icon("spotted-chicken", SceneryDAtlas, 2),
            ["strongarm"] = Icon("strongarm", SceneryDAtlas, 3),
            ["sunny flight stepping stone"] = Icon("sunny-flight-stepping-stone", SceneryDAtlas, 4),
            ["swamp grass clump"] = Icon("swamp-grass-clump", SceneryDAtlas, 5),
            ["tall 2 ball skinny tree"] = Icon("tall-two-ball-tree", SceneryDAtlas, 6),
            ["thief key"] = Icon("thief-key", SceneryDAtlas, 7),
            ["torch"] = Icon("torch", SceneryDAtlas, 8),
            ["tornado wizard"] = Icon("tornado-wizard", SceneryDAtlas, 9),
            ["torro gnorc"] = Icon("torro-gnorc", SceneryDAtlas, 10),
            ["tower side flag long"] = Icon("tower-side-flag-long", SceneryDAtlas, 11),
            ["treasure gnorc 3 hit"] = Icon("treasure-gnorc-three-hit", SceneryDAtlas, 12),
            ["tulip flowers"] = Icon("tulip-flowers", SceneryDAtlas, 13),

            // Final scenery atlas.
            ["turtle mutant turtle"] = Icon("turtle-mutant-turtle", SceneryEAtlas, 0),
            ["wall lamp"] = Icon("wall-lamp", SceneryEAtlas, 1),
            ["wavy shield gnorc"] = Icon("wavy-shield-gnorc", SceneryEAtlas, 2),
            ["wide tree"] = Icon("wide-tree", SceneryEAtlas, 3),
            ["tower flag"] = Icon("tower-flag", SceneryEAtlas, 4),
            ["sheep"] = Icon("sheep", SceneryEAtlas, 5),
            ["grass"] = Icon("grass", SceneryEAtlas, 6),
            ["lamp post"] = Icon("lamp-post", SceneryEAtlas, 7),

            // Landmarks and scene helpers.
            ["crystal dragon statue"] = Icon("crystal-dragon-statue", LandmarksFAtlas, 0),
            ["crystal dragon"] = Icon("crystal-dragon", LandmarksFAtlas, 0),
            ["dragon"] = Icon("dragon", LandmarksFAtlas, 0),
            ["whirlwind"] = Icon("whirlwind", LandmarksFAtlas, 1),
            ["portal"] = Icon("portal", LandmarksFAtlas, 2),
            ["balloon"] = Icon("balloon", LandmarksFAtlas, 3),
            ["balloonist"] = Icon("balloonist", LandmarksFAtlas, 4),
            ["dragon pedestal"] = Icon("dragon-pedestal", LandmarksFAtlas, 5),
            ["rescue fairy"] = Icon("rescue-fairy", LandmarksFAtlas, 6),
            ["level entry vortex"] = Icon("level-entry-vortex", LandmarksFAtlas, 7),
            ["wooden bridge"] = Icon("wooden-bridge", LandmarksFAtlas, 8),
            ["stone platform"] = Icon("stone-platform", LandmarksFAtlas, 9),
            ["flowering tree"] = Icon("flowering-tree", LandmarksFAtlas, 10),
            ["beast makers banner"] = Icon("beast-makers-banner", LandmarksFAtlas, 11),

            // Additional public gallery objects.
            ["armored gnorc"] = Icon("armored-gnorc", ObjectsGAtlas, 0),
            ["barrel engineer"] = Icon("barrel-engineer", ObjectsGAtlas, 1),
            ["beast"] = Icon("beast", ObjectsGAtlas, 2),
            ["bull"] = Icon("bull", ObjectsGAtlas, 3),
            ["caged fairy"] = Icon("caged-fairy", ObjectsGAtlas, 4),
            ["campfire"] = Icon("campfire", ObjectsGAtlas, 5),
            ["chicken cage"] = Icon("chicken-cage", ObjectsGAtlas, 6),
            ["clock fool"] = Icon("clock-fool", ObjectsGAtlas, 7),
            ["crocodile"] = Icon("crocodile", ObjectsGAtlas, 8),
            ["dockworker tnt wrangler"] = Icon("dockworker-tnt-wrangler", ObjectsGAtlas, 9),
            ["drawbridge lever"] = Icon("drawbridge-lever", ObjectsGAtlas, 10),
            ["fairy cage prop"] = Icon("fairy-cage-prop", ObjectsGAtlas, 11),
            ["fat bat"] = Icon("fat-bat", ObjectsGAtlas, 12),
            ["fat claw monster"] = Icon("fat-claw-monster", ObjectsGAtlas, 13),

            ["flight direction arrow sign"] = Icon("flight-direction-arrow-sign", ObjectsHAtlas, 0),
            ["floor shocker"] = Icon("floor-shocker", ObjectsHAtlas, 1),
            ["lantern post"] = Icon("lantern-post", ObjectsHAtlas, 2),
            ["metal claw monster"] = Icon("metal-claw-monster", ObjectsHAtlas, 3),
            ["ram"] = Icon("ram", ObjectsJAtlas, 0),
            ["shepard"] = Icon("shepherd", ObjectsJAtlas, 1),
            ["shepherd"] = Icon("shepherd", ObjectsJAtlas, 1),
            ["shielded greenie"] = Icon("shielded-greenie", ObjectsKAtlas, 0),
            ["summoning wizard"] = Icon("summoning-wizard", ObjectsKAtlas, 1),
            ["volt shooter"] = Icon("volt-shooter", ObjectsIAtlas, 0),
            ["wall lantern"] = Icon("wall-lantern", ObjectsIAtlas, 1)
        };

    private static readonly object CacheGate = new();
    private static readonly Dictionary<string, Bitmap?> AtlasCache =
        new(StringComparer.Ordinal);

    internal static IReadOnlyList<MobyRasterIconAtlasContract> Atlases => AtlasesInternal;
    internal static int DefinitionCount => Definitions.Count;

    internal static bool TryDraw(
        DrawingContext context,
        Rect bounds,
        string? displayName)
    {
        if (!TryResolve(displayName, out ObjectGalleryIconDefinition definition))
            return false;

        Bitmap? atlas = GetAtlas(definition.Atlas);
        if (atlas == null)
            return false;

        Rect source = MobyIconAtlasLoader.GetCellSourceRect(
            atlas.PixelSize,
            definition.Atlas,
            definition.AtlasCell);
        // Image generation can leave a few anti-aliased pixels from an adjacent
        // cell exactly on a mathematical grid edge. A small source gutter keeps
        // every selector card visually isolated while retaining the generous
        // subject padding requested by the atlas contract.
        double sourceInset = Math.Max(2, Math.Min(source.Width, source.Height) * 0.06);
        source = new Rect(
            source.X + sourceInset,
            source.Y + sourceInset,
            Math.Max(1, source.Width - (sourceInset * 2)),
            Math.Max(1, source.Height - (sourceInset * 2)));
        double side = Math.Max(1, Math.Min(bounds.Width, bounds.Height) - 4);
        Rect destination = new(
            bounds.Center.X - (side * 0.5),
            bounds.Center.Y - (side * 0.5),
            side,
            side);
        context.DrawImage(atlas, source, destination);
        return true;
    }

    internal static IReadOnlyList<MobyIconAtlasDiagnostics> LoadDiagnosticsForTesting()
    {
        List<MobyIconAtlasDiagnostics> diagnostics = new(AtlasesInternal.Count);
        foreach (MobyRasterIconAtlasContract atlas in AtlasesInternal)
        {
            MobyIconAtlasDiagnostics? last = null;
            foreach (string candidatePath in CandidatePaths(atlas.FileName))
            {
                MobyIconAtlasLoadResult loaded = MobyIconAtlasLoader.Load(candidatePath, atlas);
                if (last == null ||
                    File.Exists(candidatePath) ||
                    !File.Exists(last.SourcePath))
                {
                    last = loaded.Diagnostics;
                }
                if (loaded.Succeeded)
                    break;
            }

            diagnostics.Add(last ?? throw new InvalidOperationException(
                $"No candidate path was produced for object-gallery atlas '{atlas.Key}'."));
        }

        return diagnostics;
    }

    internal static bool TryResolve(
        string? displayName,
        out ObjectGalleryIconDefinition definition)
    {
        string canonical = Canonicalize(displayName);
        return Definitions.TryGetValue(canonical, out definition!);
    }

    internal static string Canonicalize(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return "";

        string withoutReward = Regex.Replace(
            displayName,
            @"\s*\((red|green|blue|yellow|purple)\s+reward\)\s*$",
            "",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        string normalized = Normalize(withoutReward);
        return normalized switch
        {
            "sping chest green" or
            "spring chest blue" or
            "spring chest green" => "spring chest",
            "tulip flower" or
            "tulip flower single" => "tulip flowers",
            _ => normalized
        };
    }

    private static string Normalize(string value)
    {
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

    private static ObjectGalleryIconDefinition Icon(
        string key,
        MobyRasterIconAtlasContract atlas,
        int atlasCell) =>
        new(key, atlas, atlasCell);

    private static Bitmap? GetAtlas(MobyRasterIconAtlasContract atlas)
    {
        lock (CacheGate)
        {
            if (AtlasCache.TryGetValue(atlas.Key, out Bitmap? cached))
                return cached;

            foreach (string candidatePath in CandidatePaths(atlas.FileName))
            {
                MobyIconAtlasLoadResult loaded = MobyIconAtlasLoader.Load(candidatePath, atlas);
                if (!loaded.Succeeded)
                    continue;

                AtlasCache[atlas.Key] = loaded.Bitmap;
                return loaded.Bitmap;
            }

            AtlasCache[atlas.Key] = null;
            return null;
        }
    }

    private static IEnumerable<string> CandidatePaths(string fileName)
    {
        string baseDirectory = AppContext.BaseDirectory;
        yield return Path.Combine(baseDirectory, "Assets", "ObjectGalleryIcons", fileName);
        yield return Path.GetFullPath(
            Path.Combine(baseDirectory, "../../../Assets/ObjectGalleryIcons", fileName));
    }
}

internal sealed record ObjectGalleryIconDefinition(
    string Key,
    MobyRasterIconAtlasContract Atlas,
    int AtlasCell);
