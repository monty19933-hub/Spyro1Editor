using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Workspace;

EditorWorkspace workspace = EditorWorkspace.Find(args.FirstOrDefault());
LevelCatalog levels = LevelCatalog.Load(workspace.RootPath);
if (levels.Levels.Count != 35)
    throw new InvalidOperationException($"Expected all 35 retail levels, found {levels.Levels.Count} in {workspace.RootPath}.");

IReadOnlyList<MobyRasterIconDefinition> definitions = MobyRasterIconCatalog.Definitions;
IReadOnlyList<MobyRasterIconAtlasContract> atlases = MobyRasterIconCatalog.Atlases;
if (definitions.Count != 40)
    throw new InvalidOperationException($"Expected 40 audited raster icon families, found {definitions.Count}.");
if (atlases.Count != 3)
    throw new InvalidOperationException($"Expected exactly three installed Moby icon atlases, found {atlases.Count}.");
if (atlases.Select(atlas => atlas.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != atlases.Count ||
    atlases.Select(atlas => atlas.FileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != atlases.Count)
{
    throw new InvalidOperationException("Installed Moby icon atlas keys and file names must remain unique.");
}

MobyRasterIconAtlasContract installedAtlas1 = MobyRasterIconCatalog.InstalledAtlas1;
MobyRasterIconAtlasContract installedAtlas2 = MobyRasterIconCatalog.InstalledAtlas2;
MobyRasterIconAtlasContract installedAtlas3 = MobyRasterIconCatalog.InstalledAtlas3;
if (!atlases.Contains(installedAtlas1) ||
    installedAtlas1.Key != "moby-family-atlas-1" ||
    installedAtlas1.FileName != "moby-icon-atlas.png" ||
    installedAtlas1.Columns != 4 ||
    installedAtlas1.Rows != 4 ||
    installedAtlas1.UsedCellCount != 14 ||
    installedAtlas1.EmptyCellCount != 2)
{
    throw new InvalidOperationException("Installed atlas 1 must remain a 4 x 4 sheet with used cells 0..13 and empty cells 14/15.");
}
if (!atlases.Contains(installedAtlas2) ||
    installedAtlas2.Key != "moby-family-atlas-2" ||
    installedAtlas2.FileName != "moby-icon-atlas-2.png" ||
    installedAtlas2.Columns != 4 ||
    installedAtlas2.Rows != 4 ||
    installedAtlas2.UsedCellCount != 14 ||
    installedAtlas2.EmptyCellCount != 2)
{
    throw new InvalidOperationException("Installed atlas 2 must remain a 4 x 4 sheet with used cells 0..13 and empty cells 14/15.");
}
if (!atlases.Contains(installedAtlas3) ||
    installedAtlas3.Key != "moby-family-atlas-3" ||
    installedAtlas3.FileName != "moby-icon-atlas-3.png" ||
    installedAtlas3.Columns != 4 ||
    installedAtlas3.Rows != 4 ||
    installedAtlas3.UsedCellCount != 12 ||
    installedAtlas3.EmptyCellCount != 4)
{
    throw new InvalidOperationException("Installed atlas 3 must remain a 4 x 4 sheet with used cells 0..11 and empty cells 12..15.");
}

if (definitions.Select(item => item.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != definitions.Count ||
    definitions.Select(item => item.FileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != definitions.Count ||
    definitions.Select(item => item.AtlasCellId).Distinct().Count() != definitions.Count)
{
    throw new InvalidOperationException("Raster icon catalog keys, file names, and atlas-qualified cells must remain unique.");
}
foreach (MobyRasterIconAtlasContract atlas in atlases)
{
    MobyRasterIconDefinition[] atlasDefinitions = definitions
        .Where(definition => definition.Atlas == atlas)
        .ToArray();
    if (!atlasDefinitions.Select(definition => definition.AtlasCell).Order()
        .SequenceEqual(Enumerable.Range(0, atlas.UsedCellCount)) ||
        atlasDefinitions.Any(definition => !atlas.ContainsUsedCell(definition.AtlasCell)))
    {
        throw new InvalidOperationException(
            $"Atlas {atlas.Key} definitions must fill its explicit used-cell range 0..{atlas.UsedCellCount - 1} exactly once.");
    }
}

Dictionary<string, int> matchedCounts = definitions.ToDictionary(item => item.Key, _ => 0, StringComparer.OrdinalIgnoreCase);
Dictionary<string, HashSet<string>> matchedLevels = definitions.ToDictionary(
    item => item.Key,
    _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
    StringComparer.OrdinalIgnoreCase);
Dictionary<string, int> remainingGenericFamilies = new(StringComparer.OrdinalIgnoreCase);
Dictionary<MobyGenericMarkerFallbackKind, int> genericBaseline = Enum
    .GetValues<MobyGenericMarkerFallbackKind>()
    .Where(kind => kind != MobyGenericMarkerFallbackKind.None)
    .ToDictionary(kind => kind, _ => 0);
int catalogCoveredGenericRecords = 0;
int loadedMobys = 0;
List<string> gatedNonGenericMatches = [];
List<string> atlas3RecordKeys = [];
Dictionary<MobyGenericMarkerFallbackKind, int> atlas3FallbackDistribution = Enum
    .GetValues<MobyGenericMarkerFallbackKind>()
    .Where(kind => kind != MobyGenericMarkerFallbackKind.None)
    .ToDictionary(kind => kind, _ => 0);

foreach (LevelDefinition level in levels.Levels)
{
    string cachePath = Path.Combine(workspace.RootPath, "editor-cache", $"{level.Key}-mobys.json");
    if (!File.Exists(cachePath))
        throw new FileNotFoundException($"The all-level raster icon audit requires {level.DisplayName}'s cache.", cachePath);

    List<Moby> mobys = MobyLoader.LoadCached(cachePath).ToList();
    MobyMetadataEnricher.Apply(workspace, level.Key, mobys);
    loadedMobys += mobys.Count;

    foreach (Moby moby in mobys)
    {
        MobyGenericMarkerFallbackKind fallbackKind = EditorViewport.ClassifyGenericMarkerFallback(moby);
        bool hasRasterDefinition = MobyRasterIconCatalog.TryMatch(moby, out MobyRasterIconDefinition definition);
        if (hasRasterDefinition)
        {
            matchedCounts[definition.Key]++;
            matchedLevels[definition.Key].Add(level.Key);
            if (definition.GenericFallbackOnly && fallbackKind == MobyGenericMarkerFallbackKind.None)
                gatedNonGenericMatches.Add($"{level.Key}:T{moby.TrueIndex} {moby.DisplayLabel}");
            if (definition.Atlas == installedAtlas3)
            {
                atlas3RecordKeys.Add($"{level.Key}:T{moby.TrueIndex}");
                atlas3FallbackDistribution[fallbackKind]++;
            }
        }

        if (fallbackKind == MobyGenericMarkerFallbackKind.None)
            continue;

        genericBaseline[fallbackKind]++;
        if (hasRasterDefinition)
        {
            catalogCoveredGenericRecords++;
            continue;
        }

        string label = StableAuditLabel(moby);
        remainingGenericFamilies[label] = remainingGenericFamilies.GetValueOrDefault(label) + 1;
    }
}

string[] missingFamilies = definitions
    .Where(definition => matchedCounts[definition.Key] == 0)
    .Select(definition => definition.DisplayName)
    .ToArray();
if (missingFamilies.Length > 0)
    throw new InvalidOperationException($"Audited icon families did not match the enriched 35-level cache: {string.Join(", ", missingFamilies)}.");
if (gatedNonGenericMatches.Count > 0)
{
    throw new InvalidOperationException(
        $"A generic-only atlas escaped its renderer-fallback eligibility gate: {string.Join(", ", gatedNonGenericMatches)}.");
}

Dictionary<MobyGenericMarkerFallbackKind, int> expectedBaseline = new()
{
    [MobyGenericMarkerFallbackKind.GenericGnorc] = 192,
    [MobyGenericMarkerFallbackKind.GenericTree] = 109,
    [MobyGenericMarkerFallbackKind.ActorTriangle] = 40,
    [MobyGenericMarkerFallbackKind.FlightBullseye] = 39
};
foreach ((MobyGenericMarkerFallbackKind kind, int expected) in expectedBaseline)
{
    if (genericBaseline[kind] != expected)
        throw new InvalidOperationException($"{kind} baseline changed: expected {expected}, found {genericBaseline[kind]}.");
}
if (genericBaseline.Values.Sum() != 380)
    throw new InvalidOperationException($"The renderer generic-fallback baseline should be 380, found {genericBaseline.Values.Sum()}.");
Dictionary<string, int> expectedMatchedCounts = new(StringComparer.OrdinalIgnoreCase)
{
    ["tnt-train-car"] = 16,
    ["barrel-supply-hatch"] = 22,
    ["steel-barrel"] = 11,
    ["fairy-cage"] = 15,
    ["caged-fairy"] = 15,
    ["shielded-greenie"] = 26,
    ["tin-soldier"] = 25,
    ["power-pole"] = 17,
    ["volt-shooter"] = 16,
    ["flight-lighthouse"] = 16,
    ["clock-fool"] = 15,
    ["metal-door"] = 12,
    ["fat-bat"] = 12,
    ["train"] = 24,
    ["fat-claw-monster"] = 11,
    ["barrel-engineer"] = 10,
    ["beast"] = 9,
    ["dockworker-tnt-wrangler"] = 9,
    ["lantern-post"] = 9,
    ["bull"] = 8,
    ["ram"] = 8,
    ["floor-shocker"] = 7,
    ["metal-claw-monster"] = 7,
    ["shepherd"] = 10,
    ["summoning-wizard"] = 6,
    ["flight-direction-arrow-sign"] = 5,
    ["superflame-fairy"] = 6,
    ["armored-gnorc"] = 4,
    ["beast-makers-banner"] = 4,
    ["crocodile"] = 4,
    ["ice-stalactite"] = 3,
    ["rescue-fairy"] = 3,
    ["super-grenadier"] = 3,
    ["campfire"] = 2,
    ["chicken-cage"] = 2,
    ["drawbridge-lever"] = 2,
    ["wall-lantern"] = 2,
    ["arrow-sign-fairy"] = 1,
    ["balloon"] = 2,
    ["metalhead-boss"] = 1
};
foreach ((string key, int expected) in expectedMatchedCounts)
{
    if (matchedCounts[key] != expected)
        throw new InvalidOperationException($"Raster family {key} changed: expected {expected}, found {matchedCounts[key]}.");
}
int atlas1Coverage = definitions.Where(definition => definition.Atlas == installedAtlas1).Sum(definition => matchedCounts[definition.Key]);
int atlas2Coverage = definitions.Where(definition => definition.Atlas == installedAtlas2).Sum(definition => matchedCounts[definition.Key]);
int atlas3Coverage = definitions.Where(definition => definition.Atlas == installedAtlas3).Sum(definition => matchedCounts[definition.Key]);
if (atlas1Coverage != 242 || atlas2Coverage != 109 || atlas3Coverage != 29 || catalogCoveredGenericRecords != 380)
{
    throw new InvalidOperationException(
        $"Expected atlas coverage 242 + 109 + 29 = 380/380; found {atlas1Coverage} + {atlas2Coverage} + {atlas3Coverage} = {catalogCoveredGenericRecords}/380.");
}

Dictionary<MobyGenericMarkerFallbackKind, int> expectedAtlas3FallbackDistribution = new()
{
    [MobyGenericMarkerFallbackKind.GenericTree] = 17,
    [MobyGenericMarkerFallbackKind.GenericGnorc] = 8,
    [MobyGenericMarkerFallbackKind.ActorTriangle] = 4,
    [MobyGenericMarkerFallbackKind.FlightBullseye] = 0
};
foreach ((MobyGenericMarkerFallbackKind kind, int expected) in expectedAtlas3FallbackDistribution)
{
    if (atlas3FallbackDistribution[kind] != expected)
        throw new InvalidOperationException($"Atlas 3 {kind} distribution changed: expected {expected}, found {atlas3FallbackDistribution[kind]}.");
}
if (remainingGenericFamilies.Count != 0)
    throw new InvalidOperationException($"All 380 generic renderer fallbacks must be covered; {remainingGenericFamilies.Values.Sum()} record(s) remain.");

const string ExpectedAtlas3RecordSetSha256 = "79E8A8B04608E505DCEBE53584EDCF666AE47C89737B356D15A9A4DAD3D456B0";
string atlas3RecordSetSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
    string.Join('\n', atlas3RecordKeys.OrderBy(value => value, StringComparer.Ordinal)))));
if (!atlas3RecordSetSha256.Equals(ExpectedAtlas3RecordSetSha256, StringComparison.Ordinal))
{
    throw new InvalidOperationException(
        $"Atlas 3 record-set SHA changed: expected {ExpectedAtlas3RecordSetSha256}, found {atlas3RecordSetSha256}.");
}

MobyRasterIconDefinition beastDefinition = definitions.Single(definition => definition.Key == "beast");
if (!beastDefinition.ExcludedMatchPhrasesOrEmpty.Contains("beast makers banner", StringComparer.Ordinal))
    throw new InvalidOperationException("Atlas 2 Beast must continue excluding the more-specific Beast Makers banner phrase.");

// Synthetic guards catch ordering/cross-level-import regressions that the current retail
// placement set cannot always express (notably TNT trains versus ordinary train pieces).
RequireMatch("Train Piece - TNTs", "", "tnt-train-car");
RequireMatch("Train Piece", "", "train");
RequireMatch("Renamed import", "Lofty Castle caged-fairy cage prop", "fairy-cage");
RequireMatch("Renamed import", "Lofty Castle caged fairy rescue actor", "caged-fairy");
RequireNoMatch("Metal chest", "generic metal scenery");
RequireGenericMatch("Fat Claw Monster (Blue reward)", "", MobyGenericMarkerFallbackKind.GenericGnorc, "fat-claw-monster");
RequireGenericMatch("Renamed import", "Gnorc Cove barrel engineer", MobyGenericMarkerFallbackKind.GenericGnorc, "barrel-engineer");
RequireGenericMatch("Beast", "", MobyGenericMarkerFallbackKind.GenericGnorc, "beast");
RequireGenericMatch("Dockworker / TNT Wrangler (Green reward)", "", MobyGenericMarkerFallbackKind.GenericGnorc, "dockworker-tnt-wrangler");
RequireGenericMatch("Lantern post", "", MobyGenericMarkerFallbackKind.GenericTree, "lantern-post");
RequireGenericMatch("Bull", "", MobyGenericMarkerFallbackKind.GenericGnorc, "bull");
RequireGenericMatch("Ram", "", MobyGenericMarkerFallbackKind.GenericGnorc, "ram");
RequireGenericMatch("Floor Shocker", "", MobyGenericMarkerFallbackKind.GenericGnorc, "floor-shocker");
RequireGenericMatch("Metal Claw Monster (Yellow reward)", "", MobyGenericMarkerFallbackKind.GenericGnorc, "metal-claw-monster");
RequireGenericMatch("Shepard", "", MobyGenericMarkerFallbackKind.GenericGnorc, "shepherd");
RequireGenericMatch("Shepherd", "", MobyGenericMarkerFallbackKind.GenericGnorc, "shepherd");
RequireGenericMatch("Summoning Wizard", "", MobyGenericMarkerFallbackKind.GenericGnorc, "summoning-wizard");
RequireGenericMatch("Flight direction arrow sign", "", MobyGenericMarkerFallbackKind.GenericTree, "flight-direction-arrow-sign");
RequireGenericMatch("Temporary Superflame Fairy", "", MobyGenericMarkerFallbackKind.ActorTriangle, "superflame-fairy");
RequireGenericMatch("Permanent Superflame Fairy", "", MobyGenericMarkerFallbackKind.ActorTriangle, "superflame-fairy");
RequireGenericMatch("Armored Gnorc", "", MobyGenericMarkerFallbackKind.GenericGnorc, "armored-gnorc");
RequireGenericMatch("Beast Makers banner", "", MobyGenericMarkerFallbackKind.GenericTree, "beast-makers-banner");
RequireGenericMatch("Crocodile", "", MobyGenericMarkerFallbackKind.GenericGnorc, "crocodile");
RequireGenericMatch("Ice stalactite", "", MobyGenericMarkerFallbackKind.GenericTree, "ice-stalactite");
RequireGenericMatch("Rescue Fairy", "", MobyGenericMarkerFallbackKind.ActorTriangle, "rescue-fairy");
RequireGenericMatch("Super Grenadier", "", MobyGenericMarkerFallbackKind.GenericGnorc, "super-grenadier");
RequireGenericMatch("Campfire", "", MobyGenericMarkerFallbackKind.GenericTree, "campfire");
RequireGenericMatch("Chicken cage", "", MobyGenericMarkerFallbackKind.GenericTree, "chicken-cage");
RequireGenericMatch("Drawbridge lever", "", MobyGenericMarkerFallbackKind.GenericTree, "drawbridge-lever");
RequireGenericMatch("Wall lantern", "", MobyGenericMarkerFallbackKind.GenericTree, "wall-lantern");
RequireGenericMatch("Arrow-sign Fairy", "", MobyGenericMarkerFallbackKind.ActorTriangle, "arrow-sign-fairy");
RequireGenericMatch("Ballon", "", MobyGenericMarkerFallbackKind.GenericTree, "balloon");
RequireGenericMatch("Balloon", "", MobyGenericMarkerFallbackKind.GenericTree, "balloon");
RequireGenericMatch("Metalhead boss (Blue reward)", "", MobyGenericMarkerFallbackKind.GenericGnorc, "metalhead-boss");
RequireGenericMatch("Renamed import", "Beast Makers banner cross-level family", MobyGenericMarkerFallbackKind.GenericTree, "beast-makers-banner");
RequireGenericMatch("Renamed import", "Peace Keepers Ballon", MobyGenericMarkerFallbackKind.GenericTree, "balloon");
RequireNoMatch("Beast Makers banner", "");
RequireNoMatch("Floor Shocker linked record", "");
RequireNoMatch("Super Grenadier linked record", "");
RequireNoMatch("Balloon", "transport balloon scenery prop");
RequireNoMatch("Transport balloon prop", "");
RequireNoMatch("Balloognorc balloon", "");

Console.WriteLine($"Moby raster icon catalog: PASSED {levels.Levels.Count} caches / {loadedMobys:N0} Mobys / {definitions.Count} audited families / {atlases.Count} atlases.");
Console.WriteLine($"Renderer generic-fallback baseline: {genericBaseline.Values.Sum()} record(s) " +
    $"(Gnorc={genericBaseline[MobyGenericMarkerFallbackKind.GenericGnorc]}, " +
    $"tree={genericBaseline[MobyGenericMarkerFallbackKind.GenericTree]}, " +
    $"triangle={genericBaseline[MobyGenericMarkerFallbackKind.ActorTriangle]}, " +
    $"bullseye={genericBaseline[MobyGenericMarkerFallbackKind.FlightBullseye]}). " +
    $"Catalog coverage: {atlas1Coverage} + {atlas2Coverage} + {atlas3Coverage} = {catalogCoveredGenericRecords}/380.");
Console.WriteLine(
    $"Atlas 3 record set: {atlas3Coverage} record(s), SHA-256 {atlas3RecordSetSha256}; " +
    $"tree={atlas3FallbackDistribution[MobyGenericMarkerFallbackKind.GenericTree]}, " +
    $"Gnorc={atlas3FallbackDistribution[MobyGenericMarkerFallbackKind.GenericGnorc]}, " +
    $"triangle={atlas3FallbackDistribution[MobyGenericMarkerFallbackKind.ActorTriangle]}.");
foreach (MobyRasterIconDefinition definition in definitions)
{
    string sourceLevels = string.Join(",", matchedLevels[definition.Key].OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
    Console.WriteLine(
        $"  {definition.DisplayName}: {matchedCounts[definition.Key]} record(s) in [{sourceLevels}] " +
        $"-> Assets/MobyIcons/{definition.FileName}, fallback {definition.AtlasCellId}");
}

Console.WriteLine($"Remaining generic fallbacks: {remainingGenericFamilies.Values.Sum()} record(s) across {remainingGenericFamilies.Count} exact label(s).");
foreach ((string label, int count) in remainingGenericFamilies
    .OrderByDescending(item => item.Value)
    .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
    .Take(80))
{
    Console.WriteLine($"  {count,4}  {label}");
}
if (remainingGenericFamilies.Count > 80)
    Console.WriteLine($"  ... {remainingGenericFamilies.Count - 80} more named fallback family label(s)");

return 0;

void RequireMatch(string label, string family, string expectedKey)
{
    if (!MobyRasterIconCatalog.TryMatch(label, family, out MobyRasterIconDefinition definition) ||
        !definition.Key.Equals(expectedKey, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"'{label}' / '{family}' should resolve to {expectedKey}.");
    }
}

void RequireNoMatch(string label, string family)
{
    if (MobyRasterIconCatalog.TryMatch(label, family, out MobyRasterIconDefinition definition))
        throw new InvalidOperationException($"'{label}' / '{family}' unexpectedly resolved to {definition.Key}.");
}

void RequireGenericMatch(
    string label,
    string family,
    MobyGenericMarkerFallbackKind fallbackKind,
    string expectedKey)
{
    if (!MobyRasterIconCatalog.TryMatch(label, family, fallbackKind, out MobyRasterIconDefinition definition) ||
        !definition.Key.Equals(expectedKey, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Generic '{label}' / '{family}' should resolve to {expectedKey}.");
    }
}

static string StableAuditLabel(Moby moby)
{
    string label = moby.DisplayLabel;
    int reward = label.IndexOf(" (drops ", StringComparison.OrdinalIgnoreCase);
    if (reward > 0)
        label = label[..reward];
    int observed = label.IndexOf(" (", StringComparison.Ordinal);
    if (observed > 0)
        label = label[..observed];
    return label.Trim();
}
