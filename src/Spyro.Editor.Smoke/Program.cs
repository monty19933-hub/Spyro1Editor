using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Spyro.Editor.Core;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Cache;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Skyboxes;
using Spyro.Editor.Core.Text;
using Spyro.Editor.Core.Workspace;

bool buildCache = args.Contains("--build-cache", StringComparer.OrdinalIgnoreCase);
bool exportTextTest = args.Contains("--export-text-test", StringComparer.OrdinalIgnoreCase);
bool exportSkyboxTest = args.Contains("--export-skybox-test", StringComparer.OrdinalIgnoreCase);
bool exportCrossLevelObjectTest = args.Contains("--export-cross-level-object-test", StringComparer.OrdinalIgnoreCase);
bool exportCurrentSpringCandidate = args.Contains("--export-current-spring-candidate", StringComparer.OrdinalIgnoreCase);
bool springChestDiagnosticOnly = args.Contains("--spring-chest-diagnostic-only", StringComparer.OrdinalIgnoreCase);
bool repairGnastyLootCache = args.Contains("--repair-gnastysloot-cache", StringComparer.OrdinalIgnoreCase);
string? workspaceArg = args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal));
EditorWorkspace workspace = EditorWorkspace.Find(workspaceArg);
LevelCatalog catalog = LevelCatalog.Load(workspace.RootPath);
SkyboxCatalog skyboxes = SkyboxCatalog.Load(workspace);
TextTargetCatalog textTargets = TextTargetCatalog.CreateDefault();
const int IdentityTestBatchLimit = 24;
const int IdentityMicroscopeLimit = 80;
const string CurrentStoneHillSpringRecipeId = "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6BluePreHitSafeNativeSpringEffectOnlyNoPickupBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalManualSpyroDelayedJumpTouchCollectBlankVisualAwardPlainTreasureWordsManualRewardPopArcNativeEffectVisualNativeStateZeroShellStackHelper.v136";
const string CurrentStoneHillSpringTemplateId = "common.spring_chest.peacekeepers.t70";
const string CurrentStoneHillSpringCandidateSlug = "stonehill-springchest-v159-peacekeepers-t70-blueprehit-native-effect-bluegem-delayed-jumptouch-blankvisual-plain-treasure-poparc-stable-restored";
int[][] SourceDerivedCollisionPointOrders =
[
    [0, 1, 2],
    [0, 2, 1],
    [1, 0, 2],
    [1, 2, 0],
    [2, 0, 1],
    [2, 1, 0]
];
uint[] SourceDerivedCollisionZFlags = [0x0000u, 0x4000u, 0x8000u, 0xC000u];

Console.WriteLine($"Workspace: {workspace.RootPath}");
Console.WriteLine($"Levels: {catalog.Levels.Count}");
ReportEditorUiDefaults();
ReportCrossLevelObjectTemplateCatalog();

LevelDefinition? stoneHill = catalog.FindByKey("stonehill");
if (stoneHill == null)
{
    Console.Error.WriteLine("Stone Hill was not found in the level catalog.");
    return 2;
}

Console.WriteLine($"Stone Hill source records: {stoneHill.SourceRecordCount}");
string sourceImage = DiscImageLocator.FindImage(workspace);
string wadAnalysis = WadAnalysisLocator.Find(workspace);
if (springChestDiagnosticOnly)
{
    await ReportSpringChestInteractionDiagnostic();
    return 0;
}

if (exportCurrentSpringCandidate)
{
    await ExportCurrentStoneHillSpringCandidateOnly(stoneHill);
    return 0;
}

SkyboxLevelEntry? stoneHillSky = skyboxes.FindForLevel("stonehill");
Console.WriteLine(stoneHillSky == null
    ? "Stone Hill skybox catalog: missing"
    : $"Stone Hill skybox catalog: WAD entry {stoneHillSky.AssetWadEntry}, subfile {stoneHillSky.SkySubfileIndex}, {stoneHillSky.SkySubfileSize} bytes");
await ReportCrossLevelObjectImportAnalysis();
await ReportUniversalChestCompatibility();
await ReportUniversalChestStrategy();
await ReportUniversalChestPackageRecipePlanner();
ReportCrossLevelEditorTemplateReadiness();
await ReportSpringChestDependencyAudit();
await ReportSpringChestInteractionDiagnostic();
SkyboxPreset keeper = SkyboxPresetCatalog.Presets.First(preset => preset.Id == "StoneHillNightKeeper");
if (File.Exists(sourceImage) && File.Exists(wadAnalysis))
{
    SkyboxColorPatchPlan skyPlan = SkyboxColorPatchExporter.BuildPlan(
        sourceImage,
        DiscImageLocator.FindCueForImage(sourceImage),
        wadAnalysis,
        Path.Combine(workspace.RootPath, "_local", "skybox", "smoke.bin"),
        Path.Combine(workspace.RootPath, "_local", "skybox", "smoke.cue"),
        keeper,
        "");
    Console.WriteLine($"Stone Hill native skybox patch: {skyPlan.PatchCount} patches, {skyPlan.TotalPatchedBytes} bytes");
    if (exportSkyboxTest)
    {
        Directory.CreateDirectory(Path.Combine(workspace.RootPath, "_local", "skybox"));
        SkyboxColorPatchResult skyResult = await SkyboxColorPatchExporter.ExportAsync(new SkyboxColorPatchRequest(
            SourceImagePath: sourceImage,
            SourceCuePath: DiscImageLocator.FindCueForImage(sourceImage),
            WadAnalysisPath: wadAnalysis,
            OutputPrefix: Path.Combine(workspace.RootPath, "_local", "skybox", "Spyro the Dragon (USA)-stonehill-skycolors-stonehill-night-keeper"),
            Preset: keeper,
            CustomPaletteHex: "",
            WriteImage: true));
        Console.WriteLine($"Stone Hill native skybox export: {skyResult.OutputCuePath}");
    }
}
else
{
    Console.WriteLine("Stone Hill native skybox patch: source disc or WAD analysis not found; skipping export smoke.");
}
TextTargetEntry? stoneHillText = textTargets.FindForLevel(stoneHill);
Console.WriteLine(stoneHillText == null
    ? "Stone Hill text target: missing"
    : $"Stone Hill text target: {stoneHillText.OriginalText}, max {stoneHillText.MaxLength}");
List<LevelDefinition> missingTextTargets = catalog.Levels
    .Where(level => textTargets.FindForLevel(level) == null)
    .ToList();
string[] expectedFreeTextLevelKeys = ["toasty", "jacques"];
List<LevelDefinition> unexpectedMissingTextTargets = missingTextTargets
    .Where(level => !expectedFreeTextLevelKeys.Contains(LevelCatalog.NormalizeKey(level.Key), StringComparer.OrdinalIgnoreCase))
    .ToList();
Console.WriteLine($"Anchored level-name target coverage: {catalog.Levels.Count - missingTextTargets.Count}/{catalog.Levels.Count}");
if (missingTextTargets.Count > 0)
    Console.WriteLine("Free-text-only targets: " + string.Join(", ", missingTextTargets.Select(level => level.DisplayName)));
if (stoneHillText != null && File.Exists(sourceImage))
{
    LevelTextPatchPlan plan = LevelTextPatchExporter.BuildPlan(
        sourceImage,
        DiscImageLocator.FindCueForImage(sourceImage),
        Path.Combine(workspace.RootPath, "_local", "text", "smoke.bin"),
        Path.Combine(workspace.RootPath, "_local", "text", "smoke.cue"),
        stoneHillText,
        "MOON HILL");
    Console.WriteLine($"Stone Hill native text patch: EXE {plan.ExeName}, offset 0x{plan.ExeFileOffset:X}, {plan.ByteLength} bytes");
    if (exportTextTest)
    {
        Directory.CreateDirectory(Path.Combine(workspace.RootPath, "_local", "text"));
        LevelTextPatchResult result = await LevelTextPatchExporter.ExportAsync(new LevelTextPatchRequest(
            SourceImagePath: sourceImage,
            SourceCuePath: DiscImageLocator.FindCueForImage(sourceImage),
            OutputPrefix: Path.Combine(workspace.RootPath, "_local", "text", "Spyro the Dragon (USA)-text-stonehill-moon-hill"),
            Target: stoneHillText,
            ReplacementText: "MOON HILL",
            WriteImage: true));
        Console.WriteLine($"Stone Hill native text export: {result.OutputCuePath}");
    }
}
else
{
    Console.WriteLine("Stone Hill native text patch: source disc not found; skipping export smoke.");
}
if (File.Exists(sourceImage))
{
    foreach ((string original, string replacement) in new[] { ("TOASTY", "ROASTY"), ("JACQUES", "JESTER") })
    {
        ExeStringPatchPlan bossPlan = ExeStringPatchExporter.BuildPlan(
            sourceImage,
            DiscImageLocator.FindCueForImage(sourceImage),
            Path.Combine(workspace.RootPath, "_local", "text", $"{original.ToLowerInvariant()}-smoke.bin"),
            Path.Combine(workspace.RootPath, "_local", "text", $"{original.ToLowerInvariant()}-smoke.cue"),
            original,
            replacement);
        Console.WriteLine($"Free UI text target: {bossPlan.OriginalText} -> {bossPlan.ReplacementText}, offset 0x{bossPlan.ExeFileOffset:X}, {bossPlan.ByteLength} bytes");
    }
}
if (File.Exists(sourceImage))
{
    ExeStringPatchPlan exeStringPlan = ExeStringPatchExporter.BuildPlan(
        sourceImage,
        DiscImageLocator.FindCueForImage(sourceImage),
        Path.Combine(workspace.RootPath, "_local", "text", "entering-loading-smoke.bin"),
        Path.Combine(workspace.RootPath, "_local", "text", "entering-loading-smoke.cue"),
        "ENTERING %s...",
        "LOADING %s...");
    Console.WriteLine($"Native EXE string patch: {exeStringPlan.OriginalText} -> {exeStringPlan.ReplacementText}, offset 0x{exeStringPlan.ExeFileOffset:X}, {exeStringPlan.ByteLength} bytes");
}

if (buildCache)
{
    PortableEditorCacheResult result = await PortableEditorCacheBuilder.BuildAsync(workspace, catalog);
    Console.WriteLine($"Cache: {result.MobyCacheCount}/{result.LevelCount} moby files, {result.OverlayCacheCount}/{result.LevelCount} overlays");
}

string overlayPath = Path.Combine(workspace.RootPath, "editor-cache", "stonehill-runtime-scene-editor-overlay.json");
if (!File.Exists(overlayPath))
    overlayPath = workspace.ResolveFile("stonehill-runtime-scene-editor-overlay.json", "generated-research");
if (File.Exists(overlayPath))
{
    GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
    TerrainMaterialClassifier.Apply("stonehill", workspace.RootPath, geometry);
    Console.WriteLine($"Stone Hill geometry: {geometry.Polygons.Count} faces, {geometry.Edges.Count} edges");
    Console.WriteLine("Stone Hill surfaces: " + string.Join(", ", TerrainMaterialClassifier.CountSurfaces(geometry).Take(5).Select(pair => $"{pair.Key}={pair.Value}")));
    Console.WriteLine("Stone Hill terrain behavior candidates: " + string.Join(", ", TerrainBehaviorClassifier.CountBehaviors(geometry).Take(5).Select(pair => $"{pair.Key}={pair.Value}")));
    ReportTerrainVertexSeamAlignerSmoke();
    ReportTerrainBrushPathSamplerSmoke();
    ReportTerrainCopyPlacementSmoke();
    ReportTerrainTopSurfaceSnapSmoke();
    await ReportTerrainPatchPlan(stoneHill, geometry);
}
else
{
    Console.WriteLine("Stone Hill geometry overlay not present in this checkout; skipping geometry smoke.");
}
await ReportGeometryCacheHealth();
await ReportSourceSceneOverlayRecovery(repairGnastyLootCache);
await ReportSourceMobyRecovery(repairGnastyLootCache);
ReportTerrainColorFidelity();
await ReportTerrainMaterialBehaviorAudit();
ReportBuiltInTerrainMaterialOverrides();
ReportTerrainPalettePresetCoverage();
ReportTerrainTextureAssetRouting();
await ReportStoneHillCollisionMaterialAudit();
await ReportTerrainBehaviorEvidence();
await ReportTerrainBehaviorRamPairComparerSmoke();
await ReportTerrainBehaviorProofStoreSmoke();
await ReportTerrainBehaviorProofTargets();
await ReportTerrainBehaviorProofSummary();
await ReportCustomTerrainTextureManifestRoundTrip();
await ReportImportedTerrainPaletteRoundTrip();
await ReportCrossLevelTerrainTexturePatchPlan();
await ReportCrossLevelTerrainGeometryPatchPlan();
await ReportCrossLevelTerrainSideWallPatchPlan();
await ReportCrossLevelTerrainStructurePatchPlan();

string cachePath = Path.Combine(workspace.RootPath, "editor-cache", "stonehill-mobys.json");
if (File.Exists(cachePath))
{
    List<Moby> mobys = MobyLoader.LoadCached(cachePath).ToList();
    MobyMetadataResult metadata = MobyMetadataEnricher.Apply(workspace, "stonehill", mobys);
    Console.WriteLine($"Stone Hill cached mobys: {mobys.Count}");
    Console.WriteLine($"Stone Hill moby metadata: {metadata.LabeledMobys} labels, {metadata.BehaviorLinkGroups + metadata.InferredLinkGroups} link groups");
    ReportIdentityCoverage("Stone Hill", mobys);
    ReportStoneHillWhirlwindIdentity(mobys);
    ReportStoneHillLifeChestIdentity(mobys);
    ReportStoneHillKeyIdentity(mobys);
    ReportStoneHillKeyChestIdentity(mobys);
    ReportLinkedMoveTraversal("Stone Hill", mobys);
    await ReportMobySourcePatchPlan(stoneHill, mobys);
    await ReportMobyIdentityBytePatchPlan(stoneHill, mobys);
    await ReportAddedMobyRoundTrip(mobys);
    await ReportCrossLevelMobyTemplateRoundTripAndPatch(stoneHill, mobys);
    await ReportArtisansKeyChestPackagePreview();
    await ReportToastyWizardPackageWritePlan();
    await ReportCrossLevelCandidateValidationChecklist();
    ReportCrossLevelCandidateLaunchers();
    await ReportExistingGemValueRoundTrip();
await ReportRewardGemValueRoundTrip();
}
else
{
    Console.WriteLine("Stone Hill moby cache not present in this checkout; skipping moby smoke.");
}

if (HasAnyMobyCache())
{
    ReportChestContents("darkhollow");
    await ReportChestContentSourcePatch("darkhollow");
    await ReportLockedChestShellRewardGuard("darkhollow");
    await ReportPastedLooseGemPatch("darkhollow");
    await ReportMixedCopiedObjectAppendGuard("darkhollow");
    await ReportNativeSlotReusePatch("darkhollow");
    await ReportAllLevelLooseGemPlacementSectors();
    ReportHomeWorldBalloonistIdentities();
    ReportArtisansSelectionLinks();
    ReportDragonLinks("peacekeepers");
    ReportAllWhirlwindIdentities();
    ReportLifeChestIdentityFamily();
    ReportEggThiefIdentities();
    ReportType18FlameChargeChestIdentities();
    ReportObjectVisualCategoryCleanliness();
    ReportGnastyLootAircraftVisuals();
    ReportGnastyLootChestVisuals();
    ReportIcyFlightCopterIdentities();
    ReportMetalheadBirdSupportIdentities();
    ReportAllLevelIdentityCoverage();
    ReportCleanReusableIdentityPromotions();
    await ReportDeepIdentityAudit();
    ReportQuickWinIdentityBatchMetadata();
    ReportIdentityBatchOverlayRoundTrip();
}
else
{
    Console.WriteLine("Moby identity reports: editor-cache moby files not found; build local cache from source/RAM files before refreshing ID review batches.");
}

return catalog.Levels.Count > 30 && unexpectedMissingTextTargets.Count == 0 ? 0 : 1;

void ReportEditorUiDefaults()
{
    if (!EditorUiDefaults.UseGameViewMapOrientation)
        throw new InvalidOperationException("Editor should start in game-view map orientation, not raw capture orientation.");

    double defaultXSign = EditorUiDefaults.MapXAxisScreenSign(EditorUiDefaults.UseGameViewMapOrientation);
    double defaultYSign = EditorUiDefaults.MapYAxisScreenSign(EditorUiDefaults.UseGameViewMapOrientation);
    double rawXSign = EditorUiDefaults.MapXAxisScreenSign(false);
    double rawYSign = EditorUiDefaults.MapYAxisScreenSign(false);
    if (defaultXSign <= 0)
        throw new InvalidOperationException("Game-view map orientation should keep world X rightward on screen.");
    if (defaultYSign >= 0)
        throw new InvalidOperationException("Game-view map orientation should draw increasing world Y upward on screen.");
    if (rawXSign <= 0)
        throw new InvalidOperationException("Raw capture map orientation should draw increasing world X rightward on screen.");
    if (rawYSign <= 0)
        throw new InvalidOperationException("Raw capture map orientation should draw increasing world Y downward on screen.");

    Console.WriteLine("Editor UI defaults: game-view map orientation is on by default and flips raw world Y into the game-facing map view");
}

void ReportChestContents(string levelKey)
{
    string mobyPath = Path.Combine(workspace.RootPath, "editor-cache", $"{levelKey}-mobys.json");
    if (!File.Exists(mobyPath))
        return;

    List<Moby> mobys = MobyLoader.LoadCached(mobyPath).ToList();
    MobyMetadataResult metadata = MobyMetadataEnricher.Apply(workspace, levelKey, mobys);
    int repairedChestLinks = MobyRelationshipRepair.RepairChestContentLinks(levelKey, mobys);
    int chestContentLinks = mobys
        .SelectMany(moby => moby.Links)
        .Where(link => string.Equals(link.Kind, "chest contents", StringComparison.OrdinalIgnoreCase))
        .Select(link => link.Key)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
    int chests = mobys.Count(moby => moby.IsChest);
    int chestContentMobys = mobys.Count(moby => moby.IsChestContent);
    Console.WriteLine($"{levelKey} moby metadata: {metadata.LabeledMobys} labels, {metadata.InferredLinkGroups} inferred link(s), {chestContentLinks} chest content link(s), {repairedChestLinks} repaired chest link(s), {chests} chest(s), {chestContentMobys} content moby(s)");
    ReportIdentityCoverage(levelKey, mobys);
    ReportLinkedMoveTraversal(levelKey, mobys);
    ReportSyntheticChestContentLink(levelKey, mobys);
}

void ReportIdentityCoverage(string label, IReadOnlyList<Moby> mobys)
{
    int weak = mobys.Count(IsWeakIdentityLabel);
    int strong = mobys.Count - weak;
    string visualSummary = string.Join(", ", mobys
        .GroupBy(moby => moby.VisualKind)
        .OrderByDescending(group => group.Count())
        .Take(5)
        .Select(group => $"{group.Key}={group.Count()}"));
    Console.WriteLine($"{label} identity coverage: {strong}/{mobys.Count} named or inferred, weak={weak}, visuals: {visualSummary}");
}

void ReportObjectVisualCategoryCleanliness()
{
    List<(LevelDefinition Level, Moby Moby)> allMobys = new();
    foreach (LevelDefinition level in catalog.Levels)
    {
        string mobyPath = Path.Combine(workspace.RootPath, "editor-cache", $"{level.Key}-mobys.json");
        if (!File.Exists(mobyPath))
            continue;

        List<Moby> mobys = MobyLoader.LoadCached(mobyPath).ToList();
        MobyMetadataEnricher.Apply(workspace, level.Key, mobys);
        allMobys.AddRange(mobys.Select(moby => (level, moby)));
    }

    List<(LevelDefinition Level, Moby Moby)> falseGemMarkers = allMobys
        .Where(item => item.Moby.VisualKind == MobyVisualKind.Gem && !item.Moby.IsGemLike)
        .Take(12)
        .ToList();
    if (falseGemMarkers.Count > 0)
    {
        string summary = string.Join("; ", falseGemMarkers.Select(item => $"{item.Level.DisplayName}:T{item.Moby.TrueIndex} {item.Moby.DisplayLabel} {item.Moby.TechnicalSummary}"));
        throw new InvalidOperationException($"Object visual categories still have non-gem objects shown as gems: {summary}");
    }

    AssertRepresentativeVisualKind(allMobys, "Regular Gnorc", MobyVisualKind.Actor);
    AssertRepresentativeVisualKind(allMobys, "Treasure Gnorc", MobyVisualKind.Actor);
    AssertRepresentativeVisualKind(allMobys, "Tulip flower", MobyVisualKind.Scenery);
    AssertRepresentativeVisualKind(allMobys, "Tulip flowers", MobyVisualKind.Scenery);

    int rewardObjectCount = allMobys.Count(item =>
        item.Moby.RewardGem.Value > 0 &&
        !item.Moby.IsGemLike &&
        item.Moby.VisualKind is MobyVisualKind.Actor or MobyVisualKind.Scenery or MobyVisualKind.Chest);
    int visibleGemCount = allMobys.Count(item => item.Moby.VisualKind == MobyVisualKind.Gem);
    Console.WriteLine($"Object visual categories: no non-gem gem markers, visible/contained gems={visibleGemCount}, reward-bearing objects={rewardObjectCount}");
}

void AssertRepresentativeVisualKind(
    IReadOnlyList<(LevelDefinition Level, Moby Moby)> allMobys,
    string label,
    MobyVisualKind expected)
{
    List<(LevelDefinition Level, Moby Moby)> matches = allMobys
        .Where(item => item.Moby.DisplayLabel.Contains(label, StringComparison.OrdinalIgnoreCase))
        .ToList();
    if (matches.Count == 0)
        throw new InvalidOperationException($"Object visual category smoke could not find a representative {label}.");

    List<(LevelDefinition Level, Moby Moby)> mismatches = matches
        .Where(item => item.Moby.VisualKind != expected)
        .Take(6)
        .ToList();
    if (mismatches.Count > 0)
    {
        string summary = string.Join("; ", mismatches.Select(item => $"{item.Level.DisplayName}:T{item.Moby.TrueIndex} {item.Moby.DisplayLabel}/{item.Moby.VisualKind}"));
        throw new InvalidOperationException($"{label} should render as {expected}, but found {summary}.");
    }
}

void ReportStoneHillWhirlwindIdentity(IReadOnlyList<Moby> mobys)
{
    Moby? whirlwind = mobys.FirstOrDefault(moby =>
        moby.Type == 0x00
        && moby.SourceByte36 == 0x2C
        && moby.Flag4A == 0x10
        && moby.Flag4B == 0xFF);

    if (whirlwind == null)
    {
        Console.WriteLine("Stone Hill whirlwind identity: no matching marker found");
        return;
    }

    bool isWhirlwind = whirlwind.DisplayLabel.Contains("Whirlwind", StringComparison.OrdinalIgnoreCase)
        && whirlwind.VisualKind == MobyVisualKind.Whirlwind;
    if (!isWhirlwind)
    {
        throw new InvalidOperationException($"Stone Hill whirlwind identity was not promoted: T{whirlwind.TrueIndex} {whirlwind.DisplayLabel} / {whirlwind.VisualKind}.");
    }

    Console.WriteLine($"Stone Hill whirlwind identity: T{whirlwind.TrueIndex} {whirlwind.DisplayLabel}, {whirlwind.Confidence}");
}

void ReportStoneHillLifeChestIdentity(IReadOnlyList<Moby> mobys)
{
    Moby lifeChest = mobys.First(moby => moby.TrueIndex == 174);
    bool isLifeChest = lifeChest.Type == 0x20
        && lifeChest.SourceByte36 == 0xA5
        && lifeChest.Flag4A == 0x10
        && lifeChest.Flag4B == 0x0E
        && lifeChest.DisplayLabel.Contains("Life chest", StringComparison.OrdinalIgnoreCase)
        && lifeChest.VisualKind == MobyVisualKind.Chest;
    if (!isLifeChest)
    {
        throw new InvalidOperationException($"Stone Hill T174 should be Life chest, got {lifeChest.DisplayLabel} / {lifeChest.VisualKind}.");
    }

    Console.WriteLine($"Stone Hill life chest identity: T{lifeChest.TrueIndex} {lifeChest.DisplayLabel}, {lifeChest.Confidence}");
}

void ReportStoneHillKeyChestIdentity(IReadOnlyList<Moby> mobys)
{
    Moby correctedScenery = mobys.First(moby => moby.TrueIndex == 49);
    bool isCorrectedScenery = correctedScenery.Type == 0x30
        && correctedScenery.SourceByte36 == 0xE0
        && !correctedScenery.DisplayLabel.Contains("Key Chest", StringComparison.OrdinalIgnoreCase)
        && correctedScenery.VisualKind == MobyVisualKind.Scenery;
    if (!isCorrectedScenery)
    {
        throw new InvalidOperationException($"Stone Hill T49 should be corrected scenery, got {correctedScenery.DisplayLabel} / {correctedScenery.VisualKind}.");
    }

    Moby userMarkedChest = mobys.First(moby => moby.TrueIndex == 75);
    bool isUserMarkedKeyChest = userMarkedChest.DisplayLabel.Contains("Key Chest", StringComparison.OrdinalIgnoreCase)
        && userMarkedChest.VisualKind == MobyVisualKind.Chest;
    if (!isUserMarkedKeyChest)
    {
        throw new InvalidOperationException($"Stone Hill T75 should be Key Chest, got {userMarkedChest.DisplayLabel} / {userMarkedChest.VisualKind}.");
    }

    Console.WriteLine($"Stone Hill key chest identities: T{correctedScenery.TrueIndex} corrected to {correctedScenery.DisplayLabel}; T{userMarkedChest.TrueIndex} {userMarkedChest.DisplayLabel}");
}

void ReportStoneHillKeyIdentity(IReadOnlyList<Moby> mobys)
{
    Moby key = mobys.First(moby => moby.TrueIndex == 26);
    bool isKey = key.Type == 0x18
        && key.SourceByte36 == 0xAD
        && key.DisplayLabel.Contains("Key", StringComparison.OrdinalIgnoreCase)
        && key.VisualKind == MobyVisualKind.Key
        && !key.IsGemLike;
    if (!isKey)
    {
        throw new InvalidOperationException($"Stone Hill T26 should be Key category, got {key.DisplayLabel} / {key.VisualKind} / gemLike={key.IsGemLike}.");
    }

    Console.WriteLine($"Stone Hill key identity: T{key.TrueIndex} {key.DisplayLabel}, {key.Confidence}, visual={key.VisualKind}");
}

void ReportType18FlameChargeChestIdentities()
{
    List<(string LevelKey, int TrueIndex)> checks =
    [
        ("peacekeepers", 39),
        ("drycanyon", 14),
        ("drycanyon", 15),
        ("drycanyon", 16)
    ];

    List<string> promoted = new();
    foreach ((string levelKey, int trueIndex) in checks)
    {
        string mobyPath = Path.Combine(workspace.RootPath, "editor-cache", $"{levelKey}-mobys.json");
        if (!File.Exists(mobyPath))
            continue;

        List<Moby> mobys = MobyLoader.LoadCached(mobyPath).ToList();
        MobyMetadataEnricher.Apply(workspace, levelKey, mobys);
        Moby? chest = mobys.FirstOrDefault(moby => moby.TrueIndex == trueIndex);
        if (chest == null)
            continue;

        bool isType18FlameChargeChest = chest.Type == 0x18
            && chest.SourceByte36 == 0xC2
            && chest.Flag4A == 0x40
            && chest.Flag4B is 0x55 or 0x56
            && chest.DisplayLabel.Contains("Flame/charge chest", StringComparison.OrdinalIgnoreCase)
            && chest.VisualKind == MobyVisualKind.Chest;
        if (!isType18FlameChargeChest)
        {
            throw new InvalidOperationException($"{levelKey}:T{trueIndex} should be type 0x18 Flame/charge chest, got {chest.DisplayLabel} / {chest.VisualKind} / {chest.TechnicalSummary}.");
        }

        promoted.Add($"{levelKey}:T{trueIndex}");
    }

    Console.WriteLine($"Type 0x18 flame/charge chest identities: {string.Join(", ", promoted)}");
}

void ReportLifeChestIdentityFamily()
{
    List<(string LevelKey, int TrueIndex)> checks =
    [
        ("artisans", 94),
        ("townsquare", 106),
        ("peacekeepers", 90),
        ("drycanyon", 146),
        ("drycanyon", 176),
        ("stonehill", 174)
    ];

    List<string> promoted = new();
    foreach ((string levelKey, int trueIndex) in checks)
    {
        Moby moby = LoadMoby(levelKey, trueIndex);
        bool isLifeChest = moby.Type == 0x20
            && moby.SourceByte36 == 0xA5
            && moby.Flag4A == 0x10
            && moby.Flag4B == 0x0E
            && moby.DisplayLabel.Contains("Life chest", StringComparison.OrdinalIgnoreCase)
            && moby.VisualKind == MobyVisualKind.Chest;
        if (!isLifeChest)
            throw new InvalidOperationException($"{levelKey} T{trueIndex} should be Life chest, got {moby.DisplayLabel} / {moby.VisualKind}.");

        promoted.Add($"{levelKey}:T{trueIndex}");
    }

    Console.WriteLine($"Life chest identity family: {string.Join(", ", promoted)}");
}

void ReportHomeWorldBalloonistIdentities()
{
    List<(string LevelKey, int SourceByte36, string ExpectedLabel, int? ExpectedTrueIndex)> checks =
    [
        ("artisans", 0xBB, "Balloonist", null),
        ("peacekeepers", 0xBC, "Balloonist", null),
        ("magiccrafters", 0xBD, "Balloonist", 39)
    ];

    List<string> promoted = new();
    foreach ((string levelKey, int sourceByte36, string expectedLabel, int? expectedTrueIndex) in checks)
    {
        string mobyPath = Path.Combine(workspace.RootPath, "editor-cache", $"{levelKey}-mobys.json");
        if (!File.Exists(mobyPath))
            continue;

        List<Moby> mobys = MobyLoader.LoadCached(mobyPath).ToList();
        MobyMetadataEnricher.Apply(workspace, levelKey, mobys);
        Moby? moby = mobys.FirstOrDefault(item =>
            item.Type == 0x20 &&
            item.SourceByte36 == sourceByte36 &&
            item.Flag4A == 0x10 &&
            item.Flag4B == 0xFF);
        if (moby == null)
            throw new InvalidOperationException($"{levelKey} balloonist identity check did not find source byte 0x{sourceByte36:X2}.");

        if (!moby.DisplayLabel.Contains(expectedLabel, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{levelKey} balloonist identity was not promoted: expected {expectedLabel}, got T{moby.TrueIndex} {moby.DisplayLabel}.");

        if (expectedTrueIndex.HasValue && moby.TrueIndex != expectedTrueIndex.Value)
            throw new InvalidOperationException($"{levelKey} balloonist identity promoted the wrong source record: expected T{expectedTrueIndex.Value}, got T{moby.TrueIndex}.");

        promoted.Add($"{levelKey}:T{moby.TrueIndex} {moby.DisplayLabel}");
    }

    Console.WriteLine($"Home-world balloonist identities: {string.Join("; ", promoted)}");
}

void ReportAllWhirlwindIdentities()
{
    List<string> promoted = new();
    foreach (LevelDefinition level in catalog.Levels)
    {
        string mobyPath = Path.Combine(workspace.RootPath, "editor-cache", $"{level.Key}-mobys.json");
        if (!File.Exists(mobyPath))
            continue;

        List<Moby> mobys = MobyLoader.LoadCached(mobyPath).ToList();
        MobyMetadataEnricher.Apply(workspace, level.Key, mobys);
        List<Moby> whirlwinds = mobys
            .Where(moby => moby.Type == 0x00 &&
                moby.SourceByte36 == 0x2C)
            .ToList();
        foreach (Moby moby in whirlwinds)
        {
            if (!moby.DisplayLabel.Contains("Whirlwind", StringComparison.OrdinalIgnoreCase) ||
                moby.VisualKind != MobyVisualKind.Whirlwind)
            {
                throw new InvalidOperationException($"{level.Key} T{moby.TrueIndex} should be Whirlwind, got {moby.DisplayLabel} / {moby.VisualKind}.");
            }
        }

        if (whirlwinds.Count > 0)
            promoted.Add($"{level.Key}:{whirlwinds.Count}");
    }

    Console.WriteLine($"All-level whirlwind identities: {string.Join(", ", promoted)}");
}

void ReportEggThiefIdentities()
{
    List<(string LevelKey, int TrueIndex)> checks =
    [
        ("townsquare", 88),
        ("peacekeepers", 44),
        ("stonehill", 166)
    ];

    List<string> promoted = new();
    foreach ((string levelKey, int trueIndex) in checks)
    {
        Moby moby = LoadMoby(levelKey, trueIndex);
        bool isEggThief = moby.DisplayLabel.Contains("Egg thief", StringComparison.OrdinalIgnoreCase)
            && moby.VisualKind == MobyVisualKind.Actor;
        if (!isEggThief)
            throw new InvalidOperationException($"{levelKey} T{trueIndex} should be Egg thief, got {moby.DisplayLabel} / {moby.VisualKind}.");

        promoted.Add($"{levelKey}:T{trueIndex}");
    }

    Console.WriteLine($"Egg thief identities: {string.Join(", ", promoted)}");
}

void ReportCleanReusableIdentityPromotions()
{
    List<(string LevelKey, int SourceByte36, int Flag4A, int Flag4B, string ExpectedLabel)> checks =
    [
        ("artisans", 0x53, 0x10, 0x53, "Treasure Gnorc"),
        ("townsquare", 0x8B, 0x10, 0x53, "Torro Gnorc"),
        ("darkhollow", 0x9D, 0x10, 0xFF, "Campfire"),
        ("darkhollow", 0xA6, 0x10, 0x55, "Big Armor Gnorc"),
        ("alpineridge", 0x4D, 0x10, 0xFF, "Dragon pedestal")
    ];

    List<string> promoted = new();
    foreach ((string levelKey, int sourceByte36, int flag4A, int flag4B, string expectedLabel) in checks)
    {
        string mobyPath = Path.Combine(workspace.RootPath, "editor-cache", $"{levelKey}-mobys.json");
        if (!File.Exists(mobyPath))
            continue;

        List<Moby> mobys = MobyLoader.LoadCached(mobyPath).ToList();
        MobyMetadataEnricher.Apply(workspace, levelKey, mobys);
        Moby? moby = mobys.FirstOrDefault(item =>
            item.Type == 0x20 &&
            item.SourceByte36 == sourceByte36 &&
            item.Flag4A == flag4A &&
            item.Flag4B == flag4B);
        if (moby == null)
            throw new InvalidOperationException($"{levelKey} clean reusable identity check did not find source byte 0x{sourceByte36:X2}.");

        if (!moby.DisplayLabel.Contains(expectedLabel, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{levelKey} clean reusable identity was not promoted: expected {expectedLabel}, got T{moby.TrueIndex} {moby.DisplayLabel}.");

        promoted.Add($"{levelKey}:T{moby.TrueIndex} {moby.DisplayLabel}");
    }

    Console.WriteLine($"Clean reusable identity promotions: {string.Join("; ", promoted)}");
}

void ReportIcyFlightCopterIdentities()
{
    string mobyPath = Path.Combine(workspace.RootPath, "editor-cache", "icyflight-mobys.json");
    if (!File.Exists(mobyPath))
        return;

    List<Moby> mobys = MobyLoader.LoadCached(mobyPath).ToList();
    MobyMetadataEnricher.Apply(workspace, "icyflight", mobys);
    List<Moby> copters = mobys
        .Where(moby => moby.Type == 0x50 &&
            moby.SourceByte36 == 0xA3 &&
            moby.Flag4A == 0x10 &&
            moby.Flag4B == 0xFF)
        .ToList();
    if (copters.Count != 8)
        throw new InvalidOperationException($"Icy Flight copter target check expected 8 source byte 0xA3 records, found {copters.Count}.");

    if (copters.Any(moby => !moby.DisplayLabel.Contains("Copter", StringComparison.OrdinalIgnoreCase) || moby.VisualKind != MobyVisualKind.FlightTarget))
    {
        string summary = string.Join("; ", copters.Select(moby => $"T{moby.TrueIndex} {moby.DisplayLabel}/{moby.VisualKind}"));
        throw new InvalidOperationException($"Icy Flight copter target identity was not promoted: {summary}");
    }

    Console.WriteLine($"Icy Flight copter identities: {copters.Count} Copter Gnorc target(s), T{string.Join(",T", copters.Select(moby => moby.TrueIndex).Take(4))}...");
}

void ReportGnastyLootAircraftVisuals()
{
    string mobyPath = Path.Combine(workspace.RootPath, "editor-cache", "gnastysloot-mobys.json");
    if (!File.Exists(mobyPath))
        return;

    List<Moby> mobys = MobyLoader.LoadCached(mobyPath).ToList();
    MobyMetadataEnricher.Apply(workspace, "gnastysloot", mobys);
    List<Moby> aircraft = mobys
        .Where(moby => moby.Type == 0x50 &&
            moby.SourceByte36 == 0xB1 &&
            moby.Flag4A == 0x10 &&
            moby.Flag4B == 0xFF)
        .OrderBy(moby => moby.TrueIndex)
        .ToList();
    if (aircraft.Count != 2)
        throw new InvalidOperationException($"Gnasty's Loot aircraft visual check expected 2 type 0x50/source byte 0xB1 records, found {aircraft.Count}.");

    if (aircraft.Any(moby => !moby.DisplayLabel.Contains("aircraft", StringComparison.OrdinalIgnoreCase) || moby.VisualKind != MobyVisualKind.FlightTarget))
    {
        string summary = string.Join("; ", aircraft.Select(moby => $"T{moby.TrueIndex} {moby.DisplayLabel}/{moby.VisualKind}"));
        throw new InvalidOperationException($"Gnasty's Loot aircraft records should use flight-target map visuals: {summary}");
    }

    Console.WriteLine($"Gnasty's Loot aircraft visuals: {aircraft.Count} plane thief aircraft record(s), T{string.Join(",T", aircraft.Select(moby => moby.TrueIndex))}");
}

void ReportGnastyLootChestVisuals()
{
    string mobyPath = Path.Combine(workspace.RootPath, "editor-cache", "gnastysloot-mobys.json");
    if (!File.Exists(mobyPath))
        return;

    List<Moby> mobys = MobyLoader.LoadCached(mobyPath).ToList();
    MobyMetadataEnricher.Apply(workspace, "gnastysloot", mobys);
    List<Moby> chests = mobys
        .Where(moby => moby.Type == 0x20 &&
            moby.SourceByte36 == 0x86 &&
            moby.Flag4A == 0xFF &&
            moby.Flag4B is 0x56 or 0x57)
        .OrderBy(moby => moby.TrueIndex)
        .ToList();
    if (chests.Count != 2)
        throw new InvalidOperationException($"Gnasty's Loot 3x flame chest visual check expected 2 source byte 0x86 records, found {chests.Count}.");

    if (chests.Any(moby => !moby.DisplayLabel.Contains("3x flame chest", StringComparison.OrdinalIgnoreCase) || moby.VisualKind != MobyVisualKind.Chest))
    {
        string summary = string.Join("; ", chests.Select(moby => $"T{moby.TrueIndex} {moby.DisplayLabel}/{moby.VisualKind}"));
        throw new InvalidOperationException($"Gnasty's Loot source byte 0x86 records should use chest map visuals: {summary}");
    }

    Console.WriteLine($"Gnasty's Loot 3x flame chest visuals: {chests.Count} chest record(s), T{string.Join(",T", chests.Select(moby => moby.TrueIndex))}");
}

void ReportMetalheadBirdSupportIdentities()
{
    string mobyPath = Path.Combine(workspace.RootPath, "editor-cache", "metalhead-mobys.json");
    if (!File.Exists(mobyPath))
        return;

    List<Moby> mobys = MobyLoader.LoadCached(mobyPath).ToList();
    MobyMetadataEnricher.Apply(workspace, "metalhead", mobys);
    List<Moby> birdSupport = mobys
        .Where(moby => moby.Type == 0x2A &&
            moby.SourceByte36 == 0xAA &&
            moby.Flag4A == 0x50 &&
            moby.Flag4B is 0x54 or 0x55)
        .OrderBy(moby => moby.TrueIndex)
        .ToList();
    if (birdSupport.Count != 5)
        throw new InvalidOperationException($"Metalhead bird support identity check expected 5 source byte 0xAA support records, found {birdSupport.Count}.");

    if (birdSupport.Any(moby => !moby.DisplayLabel.Contains("Bird support", StringComparison.OrdinalIgnoreCase) || moby.VisualKind != MobyVisualKind.Actor))
    {
        string summary = string.Join("; ", birdSupport.Select(moby => $"T{moby.TrueIndex} {moby.DisplayLabel}/{moby.VisualKind}"));
        throw new InvalidOperationException($"Metalhead bird support identity was not promoted cleanly: {summary}");
    }

    Console.WriteLine($"Metalhead bird support identities: {birdSupport.Count} support record(s), T{string.Join(",T", birdSupport.Select(moby => moby.TrueIndex))}");
}

Moby LoadMoby(string levelKey, int trueIndex)
{
    string mobyPath = Path.Combine(workspace.RootPath, "editor-cache", $"{levelKey}-mobys.json");
    if (!File.Exists(mobyPath))
        throw new FileNotFoundException($"Moby cache missing for {levelKey}.", mobyPath);

    List<Moby> mobys = MobyLoader.LoadCached(mobyPath).ToList();
    MobyMetadataEnricher.Apply(workspace, levelKey, mobys);
    return mobys.First(moby => moby.TrueIndex == trueIndex);
}

bool HasAnyMobyCache()
{
    string cacheDir = Path.Combine(workspace.RootPath, "editor-cache");
    return Directory.Exists(cacheDir) && Directory.EnumerateFiles(cacheDir, "*-mobys.json").Any();
}

void ReportLinkedMoveTraversal(string label, IReadOnlyList<Moby> mobys)
{
    Moby? linked = mobys.FirstOrDefault(moby => moby.Links.Any(link => MobyLinkTraversal.IsActiveMoveLink(link) && link.TrueIndexes.Count > 1));
    if (linked == null)
    {
        Console.WriteLine($"{label} linked move traversal: no linked-move group in loaded metadata");
        return;
    }

    int expected = linked.Links
        .Where(MobyLinkTraversal.IsActiveMoveLink)
        .SelectMany(link => link.TrueIndexes)
        .Where(trueIndex => trueIndex == linked.TrueIndex || mobys.Any(moby => !moby.IsRemoved && moby.TrueIndex == trueIndex))
        .Distinct()
        .Count();
    int actual = MobyLinkTraversal.GetLinkedMoveMobys(linked, mobys).Select(moby => moby.TrueIndex).Distinct().Count();
    if (actual < expected)
        throw new InvalidOperationException($"{label} linked move traversal returned {actual} moby(s), expected at least {expected}.");

    Console.WriteLine($"{label} linked move traversal: T{linked.TrueIndex} moves {actual} linked moby(s)");
}

void ReportArtisansSelectionLinks()
{
    string mobyPath = Path.Combine(workspace.RootPath, "editor-cache", "artisans-mobys.json");
    if (!File.Exists(mobyPath))
        return;

    List<Moby> mobys = MobyLoader.LoadCached(mobyPath).ToList();
    MobyMetadataEnricher.Apply(workspace, "artisans", mobys);

    Moby stoneHillPortalName = mobys.First(moby => moby.TrueIndex == 144);
    HashSet<int> visiblePortalLinks = MobyLinkTraversal.GetVisibleLinkedTrueIndexes(stoneHillPortalName);
    if (visiblePortalLinks.Count != 1 || !visiblePortalLinks.Contains(157))
        throw new InvalidOperationException($"Artisans Stone Hill portal selection should only highlight T157, got {string.Join(", ", visiblePortalLinks.Order())}.");

    bool broadPortalMoveActive = stoneHillPortalName.Links.Any(link =>
        MobyLinkTraversal.IsActiveMoveLink(link) &&
        link.TrueIndexes.Count > 10);
    if (broadPortalMoveActive)
        throw new InvalidOperationException("Artisans broad portal scaffold is still active as a move link.");

    Moby portalClusterMarker = mobys.First(moby => moby.TrueIndex == 141);
    HashSet<int> visiblePortalClusterLinks = MobyLinkTraversal.GetVisibleLinkedTrueIndexes(portalClusterMarker);
    if (visiblePortalClusterLinks.Count != 0)
        throw new InvalidOperationException($"Artisans portal cluster helper T141 should not visibly highlight broad scaffold links, got {string.Join(", ", visiblePortalClusterLinks.Order())}.");

    Moby dragon = mobys.First(moby => moby.TrueIndex == 91);
    HashSet<int> dragonLinks = MobyLinkTraversal.GetVisibleLinkedTrueIndexes(dragon);
    if (!dragonLinks.SetEquals(new[] { 90 }))
        throw new InvalidOperationException($"Artisans dragon T91 should only highlight pedestal T90, got {string.Join(", ", dragonLinks.Order())}.");

    Console.WriteLine($"Artisans focused selection links: portal T144->{string.Join(",", visiblePortalLinks.Order())}; dragon T91->{string.Join(",", dragonLinks.Order())}");
}

bool IsWeakIdentityLabel(Moby moby)
{
    return string.IsNullOrWhiteSpace(moby.Label)
        || moby.Label.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
        || string.Equals(moby.Label, Moby.FallbackLabel(moby.Type), StringComparison.Ordinal);
}

void ReportAllLevelIdentityCoverage()
{
    int total = 0;
    int weak = 0;
    List<string> weakest = new();
    Dictionary<string, WeakIdentityGroup> weakGroups = new(StringComparer.OrdinalIgnoreCase);
    foreach (LevelDefinition level in catalog.Levels)
    {
        string mobyPath = Path.Combine(workspace.RootPath, "editor-cache", $"{level.Key}-mobys.json");
        if (!File.Exists(mobyPath))
            continue;

        List<Moby> mobys = MobyLoader.LoadCached(mobyPath).ToList();
        MobyMetadataEnricher.Apply(workspace, level.Key, mobys);
        MobyRelationshipRepair.RepairChestContentLinks(level.Key, mobys);
        int levelWeak = mobys.Count(IsWeakIdentityLabel);
        foreach (Moby weakMoby in mobys.Where(IsWeakIdentityLabel))
        {
            string key = $"type=0x{weakMoby.Type:X2} b36=0x{weakMoby.SourceByte36:X2} f4A=0x{weakMoby.Flag4A:X2} f4B=0x{weakMoby.Flag4B:X2} b4F=0x{weakMoby.SourceByte4F:X2}";
            if (!weakGroups.TryGetValue(key, out WeakIdentityGroup? group))
            {
                group = new WeakIdentityGroup(key);
                weakGroups[key] = group;
            }

            group.Count++;
            if (group.Levels.Count < 4)
                group.Levels.Add(level.DisplayName);
            if (group.Samples.Count < 4)
                group.Samples.Add($"T{weakMoby.TrueIndex}");
        }

        total += mobys.Count;
        weak += levelWeak;
        if (levelWeak > 0)
            weakest.Add($"{level.DisplayName} {levelWeak}/{mobys.Count}");
    }

    Console.WriteLine($"All-level identity coverage: {total - weak}/{total} named or inferred, weak={weak}");
    if (weakest.Count > 0)
        Console.WriteLine("Remaining weak identity levels: " + string.Join("; ", weakest.Take(8)));
    if (weakGroups.Count > 0)
    {
        Console.WriteLine("Top weak identity groups:");
        foreach (WeakIdentityGroup group in weakGroups.Values.OrderByDescending(group => group.Count).Take(8))
            Console.WriteLine($"  {group.Count}x {group.Key} in {string.Join(", ", group.Levels.Distinct())} samples {string.Join(", ", group.Samples)}");
    }
}

async Task ReportDeepIdentityAudit()
{
    List<(LevelDefinition Level, Moby Moby)> allMobys = new();
    foreach (LevelDefinition level in catalog.Levels)
    {
        string mobyPath = Path.Combine(workspace.RootPath, "editor-cache", $"{level.Key}-mobys.json");
        if (!File.Exists(mobyPath))
            continue;

        List<Moby> mobys = MobyLoader.LoadCached(mobyPath).ToList();
        MobyMetadataEnricher.Apply(workspace, level.Key, mobys);
        MobyRelationshipRepair.RepairChestContentLinks(level.Key, mobys);
        allMobys.AddRange(mobys.Select(moby => (level, moby)));
    }

    int exact = allMobys.Count(item => IsExactIdentity(item.Moby));
    int questionable = allMobys.Count(item => IsQuestionableIdentity(item.Moby));
    int inferred = allMobys.Count - exact - questionable;

    Dictionary<string, PrecisionIdentityGroup> groups = new(StringComparer.OrdinalIgnoreCase);
    foreach ((LevelDefinition level, Moby moby) in allMobys.Where(item => IsQuestionableIdentity(item.Moby)))
    {
        string key = IdentityFingerprint(moby);
        if (!groups.TryGetValue(key, out PrecisionIdentityGroup? group))
        {
            group = new PrecisionIdentityGroup(key, moby.DisplayLabel, moby.CandidateKind, moby.Evidence);
            groups[key] = group;
        }

        group.AddOccurrence(level, moby);
    }

    string outDir = Path.Combine(workspace.RootPath, "_local", "smoke");
    Directory.CreateDirectory(outDir);
    List<PrecisionIdentityGroup> rankedGroups = groups.Values
        .OrderByDescending(group => group.Count)
        .ThenBy(group => group.Key)
        .ToList();

    string markdownPath = Path.Combine(outDir, "moby-identity-deep-audit.md");
    string jsonPath = Path.Combine(outDir, "moby-identity-deep-audit.json");
    StringBuilder markdown = new();
    markdown.AppendLine("# Moby Identity Deep Audit");
    markdown.AppendLine();
    markdown.AppendLine($"Generated from the portable editor cache and current metadata on {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}.");
    markdown.AppendLine();
    markdown.AppendLine($"- Total mobys audited: {allMobys.Count}");
    markdown.AppendLine($"- Exact or observed identities: {exact}");
    markdown.AppendLine($"- Useful inferred identities: {inferred}");
    markdown.AppendLine($"- Still-questionable identities: {questionable}");
    markdown.AppendLine();
    markdown.AppendLine("## Top Questionable Fingerprints");
    markdown.AppendLine();
    markdown.AppendLine("| Count | Fingerprint | Current label | Likely lane | Next proof step | Samples |");
    markdown.AppendLine("|---:|---|---|---|---|---|");
    foreach (PrecisionIdentityGroup group in rankedGroups.Take(40))
    {
        string lane = IdentityTestLane(group);
        markdown.AppendLine($"| {group.Count} | `{group.Key}` | {EscapeMarkdown(group.LabelSummary)} | {EscapeMarkdown(lane)} | {EscapeMarkdown(IdentityTestRecipe(group, lane))} | {EscapeMarkdown(string.Join(", ", group.Samples.Select(sample => sample.Display)))} |");
    }
    markdown.AppendLine();
    markdown.AppendLine("## Focused Test Batches");
    markdown.AppendLine();
    if (rankedGroups.Count == 0)
        markdown.AppendLine("No focused live-test batches are currently needed; the present cache has no question-mark moby fingerprints.");
    else
        markdown.AppendLine("Use these as the next live-test queue. Move the listed records to a clear, flat spot in the named level, spaced apart in a row, then note the visible model or whether nothing appears. Promote a fingerprint only after at least one visible sample is observed, and use two samples when the group spans multiple levels.");
    markdown.AppendLine();
    foreach (PrecisionIdentityGroup group in rankedGroups.Take(12))
    {
        string lane = IdentityTestLane(group);
        markdown.AppendLine($"### {group.Key}");
        markdown.AppendLine();
        markdown.AppendLine($"- Current label: {group.LabelSummary}");
        markdown.AppendLine($"- Likely lane: {lane}");
        markdown.AppendLine($"- Proof step: {IdentityTestRecipe(group, lane)}");
        markdown.AppendLine($"- Suggested samples: {string.Join(", ", group.Samples.Take(4).Select(sample => sample.Display))}");
        markdown.AppendLine();
    }
    markdown.AppendLine();
    markdown.AppendLine("## Recent Strong Promotions");
    markdown.AppendLine();
    markdown.AppendLine("- `0x18/0xAD` is treated as `Key`, not a green gem.");
    markdown.AppendLine("- `0x20/0xA5` with behavior byte `0x0E` is treated as `Life chest`, not a dragon-scene object.");
    markdown.AppendLine("- Exact home-world `0x20/0xBB`, `0x20/0xBC`, and Magic Crafters `0x20/0xBD` actor rows are treated as `Balloonist`, including Magic Crafters T39.");
    markdown.AppendLine("- `0x20/0x49` is treated as `Spring chest`, with the reward color read from the reward byte.");
    markdown.AppendLine("- `0x20/0x21/0x30/0x22` is treated as `Egg thief`, replacing stale conflicting labels.");
    markdown.AppendLine("- `0x20/0x86` reward variants `0x53`-`0x56` are treated as `3x flame chest`, with reward color read from the reward byte.");
    markdown.AppendLine("- `0x20/0xE6` no-reward records are treated as `Bird`, extending the observed bird actor byte to its Beast Makers-family no-reward variant.");
    markdown.AppendLine("- `0x20/0x72` reward variants are treated as `Regular Gnorc`; `0x20/0x91` reward variants are treated as `Super flame chest`.");
    markdown.AppendLine("- Flight target source bytes `0x2B`, `0x34`, `0x54`, `0x61`, `0x8D/0x8F`, Wild Flight `0xA2`, and Icy Flight `0xA3` are separated into chest, airplane, ring, arch, lighthouse, boat, and copter target families.");
    markdown.AppendLine("- `0x40/0x59`-`0x5B` flight records are treated as `Flight timer number`.");
    markdown.AppendLine("- `0x20/0xFA`, `0x20/0x4B`, and Magic Crafters-family `0x20/0x4D` are split into `Dragon` and `Dragon pedestal`.");
    markdown.AppendLine("- Stale labels containing `?` are now weak enough for stronger classifier evidence to replace them.");
    markdown.AppendLine("- Home-world `0x00/0x01` and `0x00/0x8E` records are separated as portal destination and portal pad trigger markers.");
    File.WriteAllText(markdownPath, markdown.ToString());

    var json = new
    {
        generatedAt = DateTimeOffset.Now,
        totals = new
        {
            audited = allMobys.Count,
            exactOrObserved = exact,
            usefulInferred = inferred,
            stillQuestionable = questionable
        },
        questionableGroups = rankedGroups.Select(group =>
        {
            string lane = IdentityTestLane(group);
            return new
            {
                group.Count,
                fingerprint = group.Key,
                currentLabel = group.LabelSummary,
                currentKind = group.Kind,
                likelyLane = lane,
                proofStep = IdentityTestRecipe(group, lane),
                samples = group.Samples.Select(sample => new
                {
                    sample.LevelKey,
                    sample.LevelName,
                    sample.TrueIndex,
                    sample.X,
                    sample.Y,
                    sample.Z
                })
            };
        })
    };
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));
    Dictionary<string, ObservedFingerprintGroup> observedFingerprints = WriteObservedFingerprintReport(outDir, rankedGroups);
    List<IdentityTestBatch> testBatches = await WriteIdentityTestBatchArtifacts(outDir, rankedGroups, allMobys);
    List<IdentityTestBatch> quickWinBatches = await WriteQuickWinIdentityTestBatches(outDir, rankedGroups, allMobys, observedFingerprints);
    WriteNextLiveTestReport(outDir, rankedGroups, observedFingerprints, testBatches);
    WriteIdentityObservationTemplate(outDir, rankedGroups, observedFingerprints, testBatches);
    string workingObservationPath = WriteIdentityObservationWorkingFileIfMissing(outDir, rankedGroups, observedFingerprints, testBatches);
    WriteIdentityContextReport(outDir, rankedGroups, allMobys, observedFingerprints);
    string legacyEvidencePath = WriteLegacyIdentityEvidenceReport(outDir, rankedGroups);
    string levelWorkbenchPath = WriteIdentityLevelWorkbenchReport(outDir, rankedGroups, allMobys, observedFingerprints, testBatches);
    string unknownTriagePath = WriteUnknownMobyTriageReport(outDir, rankedGroups, allMobys, observedFingerprints, testBatches);
    string unknownClusterPath = WriteUnknownMobyClusterMapReport(outDir, allMobys, testBatches);
    string unknownClusterReviewPath = WriteUnknownMobyClusterReviewPacket(outDir, allMobys, testBatches);
    string familyObservationPath = WriteIdentityFamilyObservationTemplate(outDir, allMobys, testBatches);
    string modelFamilyPath = WriteIdentityModelFamilyReport(outDir, allMobys, testBatches);
    string microscopePath = WriteIdentityMicroscopeReport(outDir, rankedGroups, allMobys, testBatches);
    string rawSignaturePath = WriteRawSpecialDataSignatureReport(outDir, rankedGroups, allMobys);
    string releaseReviewPath = WriteIdentityReleaseReviewPacket(outDir, rankedGroups, observedFingerprints, testBatches, quickWinBatches);

    Console.WriteLine($"Deep identity audit: exact/observed={exact}, inferred={inferred}, still-questionable={questionable}, report={markdownPath}, json={jsonPath}, microscope={microscopePath}, workbench={levelWorkbenchPath}, unknownTriage={unknownTriagePath}, unknownClusters={unknownClusterPath}, clusterReview={unknownClusterReviewPath}, modelFamilies={modelFamilyPath}, familyObservations={familyObservationPath}, rawSignatures={rawSignaturePath}, legacyEvidence={legacyEvidencePath}, observations={workingObservationPath}, releaseReview={releaseReviewPath}, testBatches={testBatches.Count}, quickWins={quickWinBatches.Count}");
    foreach (PrecisionIdentityGroup group in groups.Values.OrderByDescending(group => group.Count).Take(5))
        Console.WriteLine($"  questionable {group.Count}x {group.Key}: {group.LabelSummary} ({string.Join(", ", group.Samples.Take(3).Select(sample => sample.Display))})");
}

async Task<List<IdentityTestBatch>> WriteIdentityTestBatchArtifacts(
    string outDir,
    IReadOnlyList<PrecisionIdentityGroup> rankedGroups,
    IReadOnlyList<(LevelDefinition Level, Moby Moby)> allMobys)
{
    string batchDir = Path.Combine(outDir, "identity-batches");
    Directory.CreateDirectory(batchDir);
    foreach (string stalePath in Directory.GetFiles(batchDir, "*-identity-batch-*-native-edits.json"))
        File.Delete(stalePath);

    List<IdentityTestBatch> batches = new();
    int batchNumber = 1;
    foreach (PrecisionIdentityGroup group in rankedGroups)
    {
        string lane = IdentityTestLane(group);
        List<(LevelDefinition Level, Moby Moby)> samples = SelectBatchSamples(group, allMobys);
        if (samples.Count == 0)
            continue;

        string? editManifest = null;
        if (CanWriteIdentityEditBatch(lane, samples))
        {
            LevelDefinition level = samples[0].Level;
            string batchLabel = group.PreferredLabelForLevel(level.Key);
            Vector3f origin = samples[0].Moby.OriginalPosition;
            List<Moby> edited = new();
            List<(Moby Moby, Vector3f Position, string Label)> restore = new();
            for (int i = 0; i < samples.Count; i++)
            {
                Moby moby = samples[i].Moby;
                restore.Add((moby, moby.Position, moby.Label));
                moby.Position = new Vector3f(origin.X + 384 + i * 384, origin.Y, origin.Z + 96);
                moby.Label = $"Identity batch {batchNumber} slot {i + 1}: {batchLabel}";
                edited.Add(moby);
            }

            editManifest = Path.Combine(batchDir, $"{level.Key}-identity-batch-{batchNumber:00}-native-edits.json");
            try
            {
                await MobyEditStore.SaveAsync(editManifest, edited, $"{level.DisplayName} identity test batch {batchNumber}");
            }
            finally
            {
                foreach ((Moby moby, Vector3f position, string label) in restore)
                {
                    moby.Position = position;
                    moby.Label = label;
                }
            }
        }
        else if (CanWriteControlClusterIdentityEditBatch(lane, samples))
        {
            editManifest = await WriteControlClusterIdentityEditBatch(
                batchDir,
                batchNumber,
                group,
                samples,
                allMobys);
        }

        List<PrecisionIdentitySample> batchSamples = samples.Select(sample => new PrecisionIdentitySample(
            sample.Level.Key,
            sample.Level.DisplayName,
            sample.Moby.TrueIndex,
            sample.Moby.OriginalPosition.X,
            sample.Moby.OriginalPosition.Y,
            sample.Moby.OriginalPosition.Z)).ToList();
        IReadOnlyList<string> rosterLeads = RosterLeadsForSamples(samples);
        batches.Add(new IdentityTestBatch(
            batchNumber,
            group.Count,
            CountBatchScopeQuestionables(group.Key, samples, allMobys),
            group.Key,
            group.PreferredLabelForLevel(samples[0].Level.Key),
            lane,
            IdentityTestRecipeForSample(lane, batchSamples[0]),
            batchSamples,
            editManifest,
            rosterLeads));
        batchNumber++;
    }

    string planPath = Path.Combine(outDir, "moby-identity-test-batches.md");
    StringBuilder markdown = new();
    markdown.AppendLine("# Moby Identity Test Batches");
    markdown.AppendLine();
    markdown.AppendLine("Generated from the current deep audit. These are disposable verification aids for every currently ranked questionable fingerprint that can be tested from one level: inspect a batch in the editor before creating a test BIN, and keep the original cache/save files as the source of truth.");
    markdown.AppendLine();
    foreach (IdentityTestBatch batch in batches)
    {
        markdown.AppendLine($"## Batch {batch.Number:00}: {batch.Fingerprint}");
        markdown.AppendLine();
        markdown.AppendLine($"- Resolves: {batch.QuestionableCount} currently-questionable moby record(s) if confirmed.");
        if (batch.ScopeQuestionableCount != batch.QuestionableCount)
            markdown.AppendLine($"- Safe scoped resolve: {batch.ScopeQuestionableCount} record(s) in this level/model-family batch.");
        markdown.AppendLine($"- Current label: {batch.CurrentLabel}");
        markdown.AppendLine($"- Likely lane: {batch.Lane}");
        if (batch.RosterLeads.Count > 0)
            markdown.AppendLine($"- Roster leads: {string.Join(", ", batch.RosterLeads)}");
        markdown.AppendLine($"- Proof step: {batch.ProofStep}");
        markdown.AppendLine($"- Samples: {string.Join(", ", batch.Samples.Select(sample => sample.Display))}");
        markdown.AppendLine(batch.EditManifestPath == null
            ? "- Native edit manifest: not generated for this helper/control-style group."
            : $"- Native edit manifest: `{Path.GetRelativePath(workspace.RootPath, batch.EditManifestPath)}`");
        markdown.AppendLine();
    }

    File.WriteAllText(planPath, markdown.ToString());
    string jsonPath = Path.Combine(outDir, "moby-identity-test-batches.json");
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
    {
        generatedAt = DateTimeOffset.Now,
        note = "Disposable identity verification batches generated from questionable moby fingerprints.",
        batches = batches.Select(batch => new
        {
            batch.Number,
            questionableCount = batch.QuestionableCount,
            scopeQuestionableCount = batch.ScopeQuestionableCount,
            fingerprint = batch.Fingerprint,
            currentLabel = batch.CurrentLabel,
            lane = batch.Lane,
            rosterLeads = batch.RosterLeads,
            proofStep = batch.ProofStep,
            editManifestPath = batch.EditManifestPath,
            samples = batch.Samples.Select(sample => new
            {
                sample.LevelKey,
                sample.LevelName,
                sample.TrueIndex,
                sample.X,
                sample.Y,
                sample.Z
            })
        })
    }, new JsonSerializerOptions { WriteIndented = true }));

    return batches;
}

async Task<List<IdentityTestBatch>> WriteQuickWinIdentityTestBatches(
    string outDir,
    IReadOnlyList<PrecisionIdentityGroup> rankedGroups,
    IReadOnlyList<(LevelDefinition Level, Moby Moby)> allMobys,
    IReadOnlyDictionary<string, ObservedFingerprintGroup> observedFingerprints)
{
    string batchDir = Path.Combine(outDir, "identity-batches");
    Directory.CreateDirectory(batchDir);

    List<IdentityTestBatch> batches = new();
    HashSet<string> usedFingerprints = new(StringComparer.OrdinalIgnoreCase);
    int batchNumber = 1;
    foreach (PrecisionIdentityGroup group in rankedGroups
        .Where(group => IsQuickWinIdentityGroup(group, allMobys, observedFingerprints))
        .Take(16))
    {
        List<(LevelDefinition Level, Moby Moby)> samples = SelectBatchSamples(group, allMobys);
        if (!CanWriteIdentityEditBatch(IdentityTestLane(group), samples))
            continue;
        if (!usedFingerprints.Add(group.Key))
            continue;

        string lane = IdentityTestLane(group);
        LevelDefinition level = samples[0].Level;
        string batchLabel = group.PreferredLabelForLevel(level.Key);
        Vector3f origin = samples[0].Moby.OriginalPosition;
        List<Moby> edited = new();
        List<(Moby Moby, Vector3f Position, string Label)> restore = new();
        for (int i = 0; i < samples.Count; i++)
        {
            Moby moby = samples[i].Moby;
            restore.Add((moby, moby.Position, moby.Label));
            moby.Position = new Vector3f(origin.X + 384 + i * 384, origin.Y, origin.Z + 96);
            moby.Label = $"Quick ID {batchNumber} slot {i + 1}: {batchLabel}";
            edited.Add(moby);
        }

        string editManifest = Path.Combine(batchDir, $"{level.Key}-identity-batch-qw{batchNumber:00}-native-edits.json");
        try
        {
            await MobyEditStore.SaveAsync(editManifest, edited, $"{level.DisplayName} quick identity test {batchNumber}");
        }
        finally
        {
            foreach ((Moby moby, Vector3f position, string label) in restore)
            {
                moby.Position = position;
                moby.Label = label;
            }
        }
        List<PrecisionIdentitySample> batchSamples = samples.Select(sample => new PrecisionIdentitySample(
            sample.Level.Key,
            sample.Level.DisplayName,
            sample.Moby.TrueIndex,
            sample.Moby.OriginalPosition.X,
            sample.Moby.OriginalPosition.Y,
            sample.Moby.OriginalPosition.Z)).ToList();
        IReadOnlyList<string> rosterLeads = RosterLeadsForSamples(samples);
        batches.Add(new IdentityTestBatch(
            batchNumber,
            group.Count,
            CountBatchScopeQuestionables(group.Key, samples, allMobys),
            group.Key,
            batchLabel,
            lane,
            IdentityTestRecipeForSample(lane, batchSamples[0]),
            batchSamples,
            editManifest,
            rosterLeads));
        batchNumber++;
    }

    string markdownPath = Path.Combine(outDir, "moby-identity-quick-win-tests.md");
    StringBuilder markdown = new();
    markdown.AppendLine("# Moby Identity Quick-Win Tests");
    markdown.AppendLine();
    markdown.AppendLine("These are not the biggest unknowns; they are the easiest precise wins. Each row is a same-level repeated fingerprint with one dominant package/special-data pattern, so two visible matching samples should usually be enough to name the fingerprint.");
    markdown.AppendLine();
    markdown.AppendLine("| Priority | Count | Fingerprint | Current label | Level | Roster leads | Batch file | Why this is quick |");
    markdown.AppendLine("|---:|---:|---|---|---|---|---|---|");
    foreach (IdentityTestBatch batch in batches)
    {
        PrecisionIdentityGroup group = rankedGroups.First(item => string.Equals(item.Key, batch.Fingerprint, StringComparison.OrdinalIgnoreCase));
        string batchFile = batch.EditManifestPath == null ? "" : Path.GetRelativePath(workspace.RootPath, batch.EditManifestPath);
        markdown.AppendLine($"| {batch.Number} | {group.Count} | `{batch.Fingerprint}` | {EscapeMarkdown(batch.CurrentLabel)} | {EscapeMarkdown(batch.Samples[0].LevelName)} | {EscapeMarkdown(string.Join(", ", batch.RosterLeads))} | {EscapeMarkdown(batchFile)} | {EscapeMarkdown(QuickWinReason(group, allMobys))} |");
    }

    File.WriteAllText(markdownPath, markdown.ToString());
    string jsonPath = Path.Combine(outDir, "moby-identity-quick-win-tests.json");
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
    {
        generatedAt = DateTimeOffset.Now,
        note = "Same-level repeated identity tests selected for fast, precise live validation.",
        batches = batches.Select(batch => new
        {
            batch.Number,
            fingerprint = batch.Fingerprint,
            currentLabel = batch.CurrentLabel,
            questionableCount = batch.QuestionableCount,
            scopeQuestionableCount = batch.ScopeQuestionableCount,
            lane = batch.Lane,
            rosterLeads = batch.RosterLeads,
            proofStep = batch.ProofStep,
            quickWinReason = QuickWinReason(rankedGroups.First(group => string.Equals(group.Key, batch.Fingerprint, StringComparison.OrdinalIgnoreCase)), allMobys),
            editManifestPath = batch.EditManifestPath,
            samples = batch.Samples.Select(sample => new
            {
                sample.LevelKey,
                sample.LevelName,
                sample.TrueIndex,
                sample.X,
                sample.Y,
                sample.Z
            })
        })
    }, new JsonSerializerOptions { WriteIndented = true }));

    return batches;
}

void ReportQuickWinIdentityBatchMetadata()
{
    string path = Path.Combine(workspace.RootPath, "_local", "smoke", "moby-identity-quick-win-tests.json");
    if (!File.Exists(path))
        return;

    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
    if (!document.RootElement.TryGetProperty("batches", out JsonElement batches) ||
        batches.ValueKind != JsonValueKind.Array)
    {
        throw new InvalidOperationException("Quick-win identity batch metadata is missing its batch list.");
    }

    int total = 0;
    int withCounts = 0;
    foreach (JsonElement batch in batches.EnumerateArray())
    {
        total++;
        int questionableCount = ReadJsonInt32(batch, "questionableCount", 0);
        int scopeQuestionableCount = ReadJsonInt32(batch, "scopeQuestionableCount", 0);
        string editManifestPath = ReadJsonString(batch, "editManifestPath");
        if (questionableCount > 0 &&
            scopeQuestionableCount > 0 &&
            !string.IsNullOrWhiteSpace(editManifestPath))
        {
            withCounts++;
        }
    }

    if (total == 0)
        return;
    if (withCounts != total)
        throw new InvalidOperationException($"Quick-win identity batch metadata is incomplete: {withCounts}/{total} batches include counts and edit manifests.");

    Console.WriteLine($"Quick-win identity batch metadata: {withCounts}/{total} batches carry counts and edit manifests");
}

void ReportIdentityBatchOverlayRoundTrip()
{
    string path = Path.Combine(workspace.RootPath, "_local", "smoke", "moby-identity-quick-win-tests.json");
    if (!File.Exists(path))
        return;

    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
    if (!document.RootElement.TryGetProperty("batches", out JsonElement batches) ||
        batches.ValueKind != JsonValueKind.Array)
    {
        return;
    }

    JsonElement batch = batches.EnumerateArray().FirstOrDefault(item => !string.IsNullOrWhiteSpace(ReadJsonString(item, "editManifestPath")));
    if (batch.ValueKind != JsonValueKind.Object)
        return;

    string editManifestPath = ReadJsonString(batch, "editManifestPath");
    if (!File.Exists(editManifestPath))
        return;

    string levelKey = "";
    if (batch.TryGetProperty("samples", out JsonElement samples) && samples.ValueKind == JsonValueKind.Array)
    {
        JsonElement sample = samples.EnumerateArray().FirstOrDefault();
        if (sample.ValueKind == JsonValueKind.Object)
            levelKey = ReadJsonString(sample, "levelKey", ReadJsonString(sample, "LevelKey"));
    }

    if (string.IsNullOrWhiteSpace(levelKey))
        return;

    string mobyPath = Path.Combine(workspace.RootPath, "editor-cache", $"{levelKey}-mobys.json");
    if (!File.Exists(mobyPath))
        return;

    List<Moby> mobys = MobyLoader.LoadCached(mobyPath).ToList();
    MobyMetadataEnricher.Apply(workspace, levelKey, mobys);
    Dictionary<int, (Vector3f Position, string Label, bool Loaded, string Summary)> original = IdentityBatchOverlay
        .ReadTrueIndexes(editManifestPath)
        .Select(trueIndex => mobys.FirstOrDefault(moby => moby.TrueIndex == trueIndex))
        .Where(moby => moby != null)
        .ToDictionary(
            moby => moby!.TrueIndex,
            moby => (moby!.Position, moby.Label, moby.HasLoadedNativeEdit, moby.LoadedNativeEditSummary));

    IdentityBatchOverlayResult result = IdentityBatchOverlay.Apply(editManifestPath, mobys);
    if (result.ChangedMobys.Count == 0 || result.RestoreSet.Count == 0)
        throw new InvalidOperationException("Identity batch overlay did not change any cached mobys.");

    foreach (Moby moby in result.ChangedMobys)
    {
        if (!moby.HasLoadedNativeEdit ||
            !moby.Label.Contains("ID", StringComparison.OrdinalIgnoreCase) && !moby.Label.Contains("Identity batch", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Identity batch overlay did not mark T{moby.TrueIndex} as a loaded test edit.");
        }
    }

    int restored = IdentityBatchOverlay.Clear(result.RestoreSet, mobys);
    if (restored != result.RestoreSet.Count)
        throw new InvalidOperationException($"Identity batch overlay restored {restored}/{result.RestoreSet.Count} mobys.");

    foreach ((int trueIndex, (Vector3f position, string label, bool loaded, string summary)) in original)
    {
        Moby moby = mobys.First(item => item.TrueIndex == trueIndex);
        bool restoredPosition = Math.Abs(moby.Position.X - position.X) < 0.001f &&
            Math.Abs(moby.Position.Y - position.Y) < 0.001f &&
            Math.Abs(moby.Position.Z - position.Z) < 0.001f;
        if (!restoredPosition ||
            !string.Equals(moby.Label, label, StringComparison.Ordinal) ||
            moby.HasLoadedNativeEdit != loaded ||
            !string.Equals(moby.LoadedNativeEditSummary, summary, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Identity batch overlay did not restore {levelKey} T{trueIndex}.");
        }
    }

    Console.WriteLine($"Identity batch overlay roundtrip: {levelKey}, changed/restored {restored} moby(s), {Path.GetFileName(editManifestPath)}");
}

int CountBatchScopeQuestionables(
    string fingerprint,
    IReadOnlyList<(LevelDefinition Level, Moby Moby)> samples,
    IReadOnlyList<(LevelDefinition Level, Moby Moby)> allMobys)
{
    if (samples.Count == 0)
        return 0;

    string levelKey = samples[0].Level.Key;
    uint specialDataPointer = samples[0].Moby.SpecialDataPointer;
    bool sameScope = samples.All(sample =>
        string.Equals(sample.Level.Key, levelKey, StringComparison.OrdinalIgnoreCase) &&
        sample.Moby.SpecialDataPointer == specialDataPointer);
    if (!sameScope)
        return allMobys.Count(item => IsQuestionableIdentity(item.Moby) && string.Equals(IdentityFingerprint(item.Moby), fingerprint, StringComparison.OrdinalIgnoreCase));

    return allMobys.Count(item =>
        IsQuestionableIdentity(item.Moby) &&
        string.Equals(IdentityFingerprint(item.Moby), fingerprint, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(item.Level.Key, levelKey, StringComparison.OrdinalIgnoreCase) &&
        item.Moby.SpecialDataPointer == specialDataPointer);
}

Dictionary<string, ObservedFingerprintGroup> WriteObservedFingerprintReport(string outDir, IReadOnlyList<PrecisionIdentityGroup> rankedQuestionableGroups)
{
    Dictionary<string, ObservedFingerprintGroup> observed = new(StringComparer.OrdinalIgnoreCase);
    foreach (string overridePath in EnumerateObservationFiles())
    {
        try
        {
            using FileStream stream = File.OpenRead(overridePath);
            using JsonDocument document = JsonDocument.Parse(stream);
            string levelKey = ReadJsonString(document.RootElement, "levelKey", LevelKeyFromObservationFile(overridePath));
            if (string.IsNullOrWhiteSpace(levelKey))
                continue;

            string cachePath = Path.Combine(workspace.RootPath, "editor-cache", $"{levelKey}-mobys.json");
            if (!File.Exists(cachePath))
                continue;

            Dictionary<int, Moby> byTrueIndex = MobyLoader.LoadCached(cachePath)
                .Where(moby => moby.TrueIndex >= 0)
                .ToDictionary(moby => moby.TrueIndex);
            if (!document.RootElement.TryGetProperty("mobys", out JsonElement items) || items.ValueKind != JsonValueKind.Array)
                continue;

            foreach (JsonElement item in items.EnumerateArray())
            {
                bool hasTrueIndex = item.TryGetProperty("trueIndex", out JsonElement trueIndexElement) &&
                    trueIndexElement.ValueKind == JsonValueKind.Number &&
                    trueIndexElement.TryGetInt32(out _);
                int trueIndex = ReadJsonInt32(item, "trueIndex", ReadJsonInt32(item, "index", -1));
                if (trueIndex < 0 || !byTrueIndex.TryGetValue(trueIndex, out Moby? moby))
                    continue;
                if (!hasTrueIndex && HasConflictingObservedMetadata(item, moby))
                    continue;

                string label = ReadJsonString(item, "displayTargetLabel", ReadJsonString(item, "label")).Trim();
                if (string.IsNullOrWhiteSpace(label))
                    continue;

                string key = IdentityFingerprint(moby);
                if (!observed.TryGetValue(key, out ObservedFingerprintGroup? group))
                {
                    group = new ObservedFingerprintGroup(key);
                    observed[key] = group;
                }

                string source = overridePath.EndsWith("-live-validation-overrides.json", StringComparison.OrdinalIgnoreCase)
                    ? "live"
                    : "user";
                group.Add(label, $"{LevelDisplayName(levelKey)}:T{trueIndex} ({source})");
            }
        }
        catch
        {
            continue;
        }
    }

    Dictionary<string, PrecisionIdentityGroup> questionableByKey = rankedQuestionableGroups
        .ToDictionary(group => group.Key, StringComparer.OrdinalIgnoreCase);
    List<ObservedFingerprintGroup> rows = observed.Values
        .OrderByDescending(group => questionableByKey.TryGetValue(group.Key, out PrecisionIdentityGroup? questionable) ? questionable.Count : 0)
        .ThenByDescending(group => group.TotalObservations)
        .ThenBy(group => group.Key)
        .ToList();

    string markdownPath = Path.Combine(outDir, "moby-observed-fingerprint-report.md");
    StringBuilder markdown = new();
    markdown.AppendLine("# Observed Moby Fingerprint Report");
    markdown.AppendLine();
    markdown.AppendLine("This joins copied Windows user labels and live-validation labels back to the portable cache bytes. Use `clean reusable` rows for safe classifier promotions; treat conflict, sketchy, and single-observation rows as live-test leads only.");
    markdown.AppendLine();
    markdown.AppendLine("| Remaining ? | Observed | Status | Fingerprint | Labels | Examples |");
    markdown.AppendLine("|---:|---:|---|---|---|---|");
    foreach (ObservedFingerprintGroup group in rows.Take(120))
    {
        int remaining = questionableByKey.TryGetValue(group.Key, out PrecisionIdentityGroup? questionable) ? questionable.Count : 0;
        markdown.AppendLine($"| {remaining} | {group.TotalObservations} | {EscapeMarkdown(ObservedStatus(group))} | `{group.Key}` | {EscapeMarkdown(group.LabelSummary)} | {EscapeMarkdown(string.Join(", ", group.Samples.Take(8)))} |");
    }

    File.WriteAllText(markdownPath, markdown.ToString());
    string jsonPath = Path.Combine(outDir, "moby-observed-fingerprint-report.json");
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
    {
        generatedAt = DateTimeOffset.Now,
        note = "Joined user override and live-validation labels to cache-derived moby fingerprints.",
        groups = rows.Select(group => new
        {
            fingerprint = group.Key,
            remainingQuestionable = questionableByKey.TryGetValue(group.Key, out PrecisionIdentityGroup? questionable) ? questionable.Count : 0,
            observed = group.TotalObservations,
            status = ObservedStatus(group),
            labels = group.LabelCounts.Select(pair => new { label = pair.Key, count = pair.Value }),
            samples = group.Samples
        })
    }, new JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine($"Observed fingerprint report: {rows.Count} groups, reusable={rows.Count(group => string.Equals(ObservedStatus(group), "clean reusable", StringComparison.OrdinalIgnoreCase))}, report={markdownPath}");
    return observed;
}

void WriteIdentityObservationTemplate(
    string outDir,
    IReadOnlyList<PrecisionIdentityGroup> rankedQuestionableGroups,
    IReadOnlyDictionary<string, ObservedFingerprintGroup> observedFingerprints,
    IReadOnlyList<IdentityTestBatch> testBatches)
{
    Dictionary<string, IdentityTestBatch> batchByFingerprint = testBatches
        .ToDictionary(batch => batch.Fingerprint, StringComparer.OrdinalIgnoreCase);
    string path = Path.Combine(outDir, "moby-identity-observation-template.json");
    var observations = rankedQuestionableGroups.Select(group =>
        BuildIdentityObservationRecord(group, observedFingerprints, batchByFingerprint, templateEvidence: true));

    File.WriteAllText(path, JsonSerializer.Serialize(new
    {
        generatedAt = DateTimeOffset.Now,
        instructions = "Copy completed observations to moby-identity-observations.json or _local/moby-identity-observations.json. Blank labels, vague labels like object/chest/prop, and labels containing ?, unknown, candidate, or related are ignored by the editor.",
        observations
    }, new JsonSerializerOptions { WriteIndented = true }));
}

string WriteIdentityObservationWorkingFileIfMissing(
    string outDir,
    IReadOnlyList<PrecisionIdentityGroup> rankedQuestionableGroups,
    IReadOnlyDictionary<string, ObservedFingerprintGroup> observedFingerprints,
    IReadOnlyList<IdentityTestBatch> testBatches)
{
    string path = Path.Combine(outDir, "moby-identity-observations.json");
    Dictionary<string, IdentityTestBatch> batchByFingerprint = testBatches
        .ToDictionary(batch => batch.Fingerprint, StringComparer.OrdinalIgnoreCase);

    List<JsonElement> observations = new();
    HashSet<string> existingFingerprints = new(StringComparer.OrdinalIgnoreCase);
    if (File.Exists(path))
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement existing = document.RootElement;
            JsonElement existingArray = existing.ValueKind == JsonValueKind.Array
                ? existing
                : existing.TryGetProperty("observations", out JsonElement foundArray) && foundArray.ValueKind == JsonValueKind.Array
                    ? foundArray
                    : default;
            if (existingArray.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement observation in existingArray.EnumerateArray())
                {
                    string fingerprint = ReadJsonString(observation, "fingerprint");
                    bool userContent = HasUserObservationContent(observation);
                    if (!userContent)
                        continue;

                    observations.Add(observation.Clone());
                    if (!string.IsNullOrWhiteSpace(fingerprint))
                        existingFingerprints.Add(fingerprint);
                }
            }
        }
        catch
        {
            observations.Clear();
            existingFingerprints.Clear();
        }
    }

    foreach (PrecisionIdentityGroup group in rankedQuestionableGroups)
    {
        if (!existingFingerprints.Add(group.Key))
            continue;

        observations.Add(JsonSerializer.SerializeToElement(
            BuildIdentityObservationRecord(group, observedFingerprints, batchByFingerprint, templateEvidence: false)));
    }

    File.WriteAllText(path, JsonSerializer.Serialize(new
    {
        generatedAt = DateTimeOffset.Now,
        instructions = "Fill label/candidateKind/evidence only after a live test confirms the object. The editor reads this file directly on the next level load. User-filled rows are preserved; blank generated rows are refreshed from the current audit. Blank labels, vague labels like object/chest/prop, and labels containing ?, unknown, candidate, or related are ignored.",
        observations
    }, new JsonSerializerOptions { WriteIndented = true }));
    return path;
}

bool HasUserObservationContent(JsonElement observation)
{
    return !string.IsNullOrWhiteSpace(ReadJsonString(observation, "label")) ||
        !string.IsNullOrWhiteSpace(ReadJsonString(observation, "displayTargetLabel")) ||
        !string.IsNullOrWhiteSpace(ReadJsonString(observation, "candidateKind")) ||
        !string.IsNullOrWhiteSpace(ReadJsonString(observation, "evidence")) ||
        !string.IsNullOrWhiteSpace(ReadJsonString(observation, "color"));
}

object BuildIdentityObservationRecord(
    PrecisionIdentityGroup group,
    IReadOnlyDictionary<string, ObservedFingerprintGroup> observedFingerprints,
    IReadOnlyDictionary<string, IdentityTestBatch> batchByFingerprint,
    bool templateEvidence)
{
    IdentityTestBatch? batch = batchByFingerprint.TryGetValue(group.Key, out IdentityTestBatch? foundBatch)
        ? foundBatch
        : null;
    IdentityFingerprintParts parts = ParseIdentityFingerprint(group.Key);
    string evidenceState = observedFingerprints.TryGetValue(group.Key, out ObservedFingerprintGroup? observed)
        ? $"{ObservedStatus(observed)}: {observed.LabelSummary}"
        : "no copied/live label evidence";
    IReadOnlyList<PrecisionIdentitySample> samples = batch?.Samples ?? group.Samples.Take(4).ToList();
    string lane = batch?.Lane ?? IdentityTestLane(group);
    return new
    {
        fingerprint = group.Key,
        typeHex = parts.TypeHex,
        sourceByte36Hex = parts.SourceByte36Hex,
        flag4AHex = parts.Flag4AHex,
        flag4BHex = parts.Flag4BHex,
        sourceByte4FHex = parts.SourceByte4FHex,
        label = "",
        candidateKind = "",
        confidence = "live-observed-fingerprint",
        color = "",
        evidence = templateEvidence ? $"Live-test observation for {group.Key}. Replace this sentence with what was seen before using the observation file." : "",
        currentLabel = group.PreferredLabelForSamples(samples),
        evidenceState,
        lane,
        proofStep = batch?.ProofStep ?? IdentityTestRecipe(group, lane),
        testBatch = batch?.EditManifestPath == null ? "" : Path.GetRelativePath(workspace.RootPath, batch.EditManifestPath),
        samples = samples.Select(sample => new { sample.LevelKey, sample.LevelName, sample.TrueIndex, sample.X, sample.Y, sample.Z })
    };
}

void WriteNextLiveTestReport(
    string outDir,
    IReadOnlyList<PrecisionIdentityGroup> rankedQuestionableGroups,
    IReadOnlyDictionary<string, ObservedFingerprintGroup> observedFingerprints,
    IReadOnlyList<IdentityTestBatch> testBatches)
{
    Dictionary<string, IdentityTestBatch> batchByFingerprint = testBatches
        .ToDictionary(batch => batch.Fingerprint, StringComparer.OrdinalIgnoreCase);
    string path = Path.Combine(outDir, "moby-identity-next-live-tests.md");
    StringBuilder markdown = new();
    markdown.AppendLine("# Next Moby Live Tests");
    markdown.AppendLine();
    markdown.AppendLine(rankedQuestionableGroups.Count == 0
        ? "No next live tests are currently needed; the current cache has no remaining question-mark fingerprints."
        : "These are the highest-value remaining question-mark fingerprints after cache mining and old-label promotion. Start with rows that have no old evidence: they are now the real unknowns.");
    markdown.AppendLine();
    markdown.AppendLine("| Priority | Count | Evidence state | Fingerprint | Current label | Test samples | Batch file | What to record |");
    markdown.AppendLine("|---:|---:|---|---|---|---|---|---|");

    int priority = 1;
    foreach (PrecisionIdentityGroup group in rankedQuestionableGroups.Take(IdentityTestBatchLimit))
    {
        IdentityTestBatch? batch = batchByFingerprint.TryGetValue(group.Key, out IdentityTestBatch? foundBatch)
            ? foundBatch
            : null;
        string evidenceState = observedFingerprints.TryGetValue(group.Key, out ObservedFingerprintGroup? observed)
            ? $"{ObservedStatus(observed)}: {observed.LabelSummary}"
            : "no copied/live label evidence";
        string batchFile = batch?.EditManifestPath != null
            ? Path.GetRelativePath(workspace.RootPath, batch.EditManifestPath)
            : "manual/cluster test";
        IReadOnlyList<PrecisionIdentitySample> samples = batch?.Samples ?? group.Samples.Take(4).ToList();
        string lane = IdentityTestLane(group);
        string record = lane switch
        {
            "visual scenery/prop" => "Visible model name, whether duplicates match, and one screenshot.",
            "actor or interactive object" => "Visible model, movement, collision, flame/charge reaction, reward byte/drop.",
            "control/helper" => "Nearby visible cluster, camera/portal/rescue/trigger behavior, and whether solo move has no effect.",
            "flight/special object" => "Target type, route behavior, timer/count behavior, and whether moving breaks it.",
            _ => "Visible model or no-visual result plus screenshot."
        };

        markdown.AppendLine($"| {priority++} | {group.Count} | {EscapeMarkdown(evidenceState)} | `{group.Key}` | {EscapeMarkdown(group.PreferredLabelForSamples(samples))} | {EscapeMarkdown(string.Join(", ", samples.Select(sample => sample.Display)))} | {EscapeMarkdown(batchFile)} | {EscapeMarkdown(record)} |");
    }

    File.WriteAllText(path, markdown.ToString());
}

string WriteIdentityReleaseReviewPacket(
    string outDir,
    IReadOnlyList<PrecisionIdentityGroup> rankedQuestionableGroups,
    IReadOnlyDictionary<string, ObservedFingerprintGroup> observedFingerprints,
    IReadOnlyList<IdentityTestBatch> testBatches,
    IReadOnlyList<IdentityTestBatch> quickWinBatches)
{
    string reviewRoot = Path.Combine(workspace.RootPath, "_local", "identity-review");
    string screenshotRoot = Path.Combine(reviewRoot, "screenshots");
    string resultsRoot = Path.Combine(reviewRoot, "results");
    string promotionRoot = Path.Combine(reviewRoot, "promotion-candidates");
    Directory.CreateDirectory(screenshotRoot);
    Directory.CreateDirectory(resultsRoot);
    Directory.CreateDirectory(promotionRoot);

    List<IdentityReleaseReviewRow> rows = BuildIdentityReleaseReviewRows(
        rankedQuestionableGroups,
        observedFingerprints,
        testBatches,
        quickWinBatches,
        screenshotRoot);
    string templatePath = Path.Combine(resultsRoot, "moby-identity-results-template.tsv");
    WriteIdentityReleaseResultsTsv(templatePath, rows);

    string workingPath = Path.Combine(resultsRoot, "moby-identity-results.tsv");
    if (!File.Exists(workingPath) || !HasIdentityReleaseTesterResults(workingPath))
        WriteIdentityReleaseResultsTsv(workingPath, rows);

    List<IdentityPromotionCandidate> promotionCandidates = ReadIdentityPromotionCandidates(workingPath);
    string promotionJsonPath = Path.Combine(promotionRoot, "moby-identity-promotion-candidates.json");
    File.WriteAllText(promotionJsonPath, JsonSerializer.Serialize(new
    {
        generatedAt = DateTimeOffset.Now,
        instructions = "These rows are not auto-applied. Review screenshots/evidence, then copy only proven rows into _local/smoke/moby-identity-observations.json or _local/moby-identity-observations.json.",
        sourceResults = Path.GetRelativePath(workspace.RootPath, workingPath),
        candidateCount = promotionCandidates.Count,
        candidates = promotionCandidates
    }, new JsonSerializerOptions { WriteIndented = true }));

    string readmePath = Path.Combine(reviewRoot, "README.md");
    StringBuilder readme = new();
    readme.AppendLine("# Moby Identity Release Review");
    readme.AppendLine();
    readme.AppendLine("This folder is the release-safe identity pipeline for questionable mobys. It is designed so testers can collect evidence without accidentally turning guesses into editor labels.");
    readme.AppendLine();
    readme.AppendLine("## Workflow");
    readme.AppendLine();
    readme.AppendLine("1. Open the editor and choose **Objects > ID review queue > Review Top Families**.");
    readme.AppendLine("2. Load a listed identity batch, create a disposable test BIN/CUE, then test that batch in-game.");
    readme.AppendLine("3. Put screenshots or short clips in the row's screenshot folder.");
    readme.AppendLine($"4. Fill `{Path.GetRelativePath(workspace.RootPath, workingPath)}`.");
    readme.AppendLine("5. Mark `resultStatus` as `confirmed` only when the visible model/behavior is clear. Use `unknown`, `no-visual`, `crash`, or `skipped` for anything else.");
    readme.AppendLine("6. Rerun smoke. Confirmed rows with concrete labels become promotion candidates, but they are still not auto-applied.");
    readme.AppendLine();
    readme.AppendLine("## Promotion Rules");
    readme.AppendLine();
    readme.AppendLine("- Never promote a vague label like object, prop, scenery, actor, enemy, chest, helper, or marker.");
    readme.AppendLine("- Never promote labels containing `?`, `unknown`, `candidate`, `related`, `maybe`, or `probably`.");
    readme.AppendLine("- Prefer two matching samples for a repeated scenery/prop family.");
    readme.AppendLine("- For actor/container rows, record visible model plus flame/charge/pickup/reward behavior when possible.");
    readme.AppendLine("- For control/helper rows, keep the label as Needs ID unless moving the nearby cluster proves what it controls.");
    readme.AppendLine();
    readme.AppendLine("## Generated Files");
    readme.AppendLine();
    readme.AppendLine($"- Results template: `{Path.GetRelativePath(workspace.RootPath, templatePath)}`");
    readme.AppendLine($"- Working results: `{Path.GetRelativePath(workspace.RootPath, workingPath)}`");
    readme.AppendLine($"- Promotion candidates: `{Path.GetRelativePath(workspace.RootPath, promotionJsonPath)}`");
    readme.AppendLine($"- Screenshot folders: `{Path.GetRelativePath(workspace.RootPath, screenshotRoot)}`");
    File.WriteAllText(readmePath, readme.ToString());

    string packetPath = Path.Combine(outDir, "moby-identity-release-review.md");
    StringBuilder markdown = new();
    markdown.AppendLine("# Moby Identity Release Review Packet");
    markdown.AppendLine();
    markdown.AppendLine("This is the tester-facing queue for getting the editor ready for release without guessing moby names.");
    markdown.AppendLine();
    markdown.AppendLine($"- Review folder: `{Path.GetRelativePath(workspace.RootPath, reviewRoot)}`");
    markdown.AppendLine($"- Result rows generated: {rows.Count}");
    markdown.AppendLine($"- Promotion candidates from current tester results: {promotionCandidates.Count}");
    markdown.AppendLine(rows.Count == 0
        ? "- No unconfirmed identity rows currently remain in the editor cache."
        : "- Unconfirmed rows remain clearly labeled as Needs ID in the editor.");
    markdown.AppendLine();
    markdown.AppendLine("## First Test Rows");
    markdown.AppendLine();
    markdown.AppendLine("| Priority | Kind | Batch | Resolves | Level | Current label | Roster leads | Batch file | Screenshot folder | Proof step |");
    markdown.AppendLine("|---:|---|---:|---:|---|---|---|---|---|---|");
    foreach (IdentityReleaseReviewRow row in rows.Take(32))
    {
        markdown.AppendLine($"| {row.Priority} | {EscapeMarkdown(row.BatchKind)} | {row.BatchNumber} | {row.ScopeQuestionableCount} | {EscapeMarkdown(row.LevelName)} | {EscapeMarkdown(row.CurrentLabel)} | {EscapeMarkdown(row.RosterLeads)} | {EscapeMarkdown(row.EditManifestPath)} | {EscapeMarkdown(row.ScreenshotFolder)} | {EscapeMarkdown(row.ProofStep)} |");
    }
    markdown.AppendLine();
    markdown.AppendLine("## Safe Result Status Values");
    markdown.AppendLine();
    markdown.AppendLine("- `confirmed`: concrete label, screenshot/evidence notes present, safe to review for promotion.");
    markdown.AppendLine("- `unknown`: visible or behavioral result was not clear.");
    markdown.AppendLine("- `no-visual`: moved/tested object did not appear visibly.");
    markdown.AppendLine("- `crash`: test crashed, hung, or regressed.");
    markdown.AppendLine("- `skipped`: not tested yet.");
    File.WriteAllText(packetPath, markdown.ToString());

    string jsonPath = Path.Combine(outDir, "moby-identity-release-review.json");
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
    {
        generatedAt = DateTimeOffset.Now,
        reviewRoot = Path.GetRelativePath(workspace.RootPath, reviewRoot),
        resultsTemplate = Path.GetRelativePath(workspace.RootPath, templatePath),
        workingResults = Path.GetRelativePath(workspace.RootPath, workingPath),
        promotionCandidates = Path.GetRelativePath(workspace.RootPath, promotionJsonPath),
        rows,
        promotionCandidateCount = promotionCandidates.Count
    }, new JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine($"Identity release review packet: rows={rows.Count}, promotionCandidates={promotionCandidates.Count}, results={workingPath}");
    return packetPath;
}

List<IdentityReleaseReviewRow> BuildIdentityReleaseReviewRows(
    IReadOnlyList<PrecisionIdentityGroup> rankedQuestionableGroups,
    IReadOnlyDictionary<string, ObservedFingerprintGroup> observedFingerprints,
    IReadOnlyList<IdentityTestBatch> testBatches,
    IReadOnlyList<IdentityTestBatch> quickWinBatches,
    string screenshotRoot)
{
    Dictionary<string, PrecisionIdentityGroup> groupsByFingerprint = rankedQuestionableGroups
        .ToDictionary(group => group.Key, StringComparer.OrdinalIgnoreCase);
    Dictionary<string, IdentityTestBatch> mainBatchByFingerprint = testBatches
        .ToDictionary(batch => batch.Fingerprint, StringComparer.OrdinalIgnoreCase);
    List<IdentityReleaseReviewRow> rows = new();
    HashSet<string> used = new(StringComparer.OrdinalIgnoreCase);
    int priority = 1;

    foreach (IdentityTestBatch batch in quickWinBatches)
    {
        if (!groupsByFingerprint.TryGetValue(batch.Fingerprint, out PrecisionIdentityGroup? group) ||
            !used.Add(batch.Fingerprint))
        {
            continue;
        }

        rows.Add(BuildIdentityReleaseReviewRow(priority++, "quick-win", batch, group, observedFingerprints, screenshotRoot));
    }

    foreach (PrecisionIdentityGroup group in rankedQuestionableGroups)
    {
        if (!mainBatchByFingerprint.TryGetValue(group.Key, out IdentityTestBatch? batch) ||
            !used.Add(group.Key))
        {
            continue;
        }

        rows.Add(BuildIdentityReleaseReviewRow(priority++, "high-impact", batch, group, observedFingerprints, screenshotRoot));
        if (rows.Count >= 96)
            break;
    }

    return rows;
}

IdentityReleaseReviewRow BuildIdentityReleaseReviewRow(
    int priority,
    string batchKind,
    IdentityTestBatch batch,
    PrecisionIdentityGroup group,
    IReadOnlyDictionary<string, ObservedFingerprintGroup> observedFingerprints,
    string screenshotRoot)
{
    PrecisionIdentitySample first = batch.Samples.First();
    string evidenceState = observedFingerprints.TryGetValue(batch.Fingerprint, out ObservedFingerprintGroup? observed)
        ? $"{ObservedStatus(observed)}: {observed.LabelSummary}"
        : "no copied/live label evidence";
    string folderName = $"{priority:000}-{SafeIdentityPathPart(first.LevelKey)}-batch-{SafeIdentityPathPart(batchKind)}-{batch.Number:00}";
    string screenshotFolder = Path.Combine(screenshotRoot, folderName);
    Directory.CreateDirectory(screenshotFolder);
    string editManifest = batch.EditManifestPath == null ? "" : Path.GetRelativePath(workspace.RootPath, batch.EditManifestPath);

    return new IdentityReleaseReviewRow(
        priority,
        batchKind,
        batch.Number,
        batch.QuestionableCount,
        batch.ScopeQuestionableCount,
        batch.Fingerprint,
        first.LevelKey,
        first.LevelName,
        string.Join(",", batch.Samples.Select(sample => $"T{sample.TrueIndex}")),
        group.PreferredLabelForSamples(batch.Samples),
        batch.Lane,
        batch.RosterLeads.Count == 0 ? "" : string.Join(", ", batch.RosterLeads),
        evidenceState,
        batch.ProofStep,
        editManifest,
        Path.GetRelativePath(workspace.RootPath, screenshotFolder),
        "",
        "",
        "",
        "",
        "fingerprint");
}

void WriteIdentityReleaseResultsTsv(string path, IReadOnlyList<IdentityReleaseReviewRow> rows)
{
    StringBuilder builder = new();
    builder.AppendLine(string.Join('\t', IdentityReleaseReviewHeader()));
    foreach (IdentityReleaseReviewRow row in rows)
        builder.AppendLine(string.Join('\t', IdentityReleaseReviewValues(row).Select(EscapeTsv)));
    File.WriteAllText(path, builder.ToString());
}

bool HasIdentityReleaseTesterResults(string path)
{
    if (!File.Exists(path))
        return false;

    string[] lines = File.ReadAllLines(path);
    if (lines.Length < 2)
        return false;

    string[] headers = lines[0].Split('\t');
    Dictionary<string, int> headerIndex = headers
        .Select((name, index) => (name, index))
        .ToDictionary(item => item.name, item => item.index, StringComparer.OrdinalIgnoreCase);

    for (int i = 1; i < lines.Length; i++)
    {
        if (string.IsNullOrWhiteSpace(lines[i]))
            continue;

        string[] cells = lines[i].Split('\t');
        if (!string.IsNullOrWhiteSpace(ReadTsvCell(cells, headerIndex, "resultStatus")) ||
            !string.IsNullOrWhiteSpace(ReadTsvCell(cells, headerIndex, "observedLabel")) ||
            !string.IsNullOrWhiteSpace(ReadTsvCell(cells, headerIndex, "candidateKind")) ||
            !string.IsNullOrWhiteSpace(ReadTsvCell(cells, headerIndex, "evidenceNotes")))
        {
            return true;
        }
    }

    return false;
}

List<IdentityPromotionCandidate> ReadIdentityPromotionCandidates(string path)
{
    if (!File.Exists(path))
        return new List<IdentityPromotionCandidate>();

    string[] lines = File.ReadAllLines(path);
    if (lines.Length < 2)
        return new List<IdentityPromotionCandidate>();

    string[] headers = lines[0].Split('\t');
    Dictionary<string, int> headerIndex = headers
        .Select((name, index) => (name, index))
        .ToDictionary(item => item.name, item => item.index, StringComparer.OrdinalIgnoreCase);
    List<IdentityPromotionCandidate> candidates = new();
    for (int i = 1; i < lines.Length; i++)
    {
        if (string.IsNullOrWhiteSpace(lines[i]))
            continue;

        string[] cells = lines[i].Split('\t');
        string status = ReadTsvCell(cells, headerIndex, "resultStatus").Trim();
        if (!IsConfirmedIdentityResultStatus(status))
            continue;

        string label = ReadTsvCell(cells, headerIndex, "observedLabel").Trim();
        if (!IsUsableReleaseIdentityLabel(label))
            continue;

        string evidence = ReadTsvCell(cells, headerIndex, "evidenceNotes").Trim();
        string screenshotFolder = ReadTsvCell(cells, headerIndex, "screenshotFolder").Trim();
        bool hasScreenshotEvidence = !string.IsNullOrWhiteSpace(screenshotFolder) &&
            Directory.Exists(Path.Combine(workspace.RootPath, screenshotFolder)) &&
            Directory.EnumerateFiles(Path.Combine(workspace.RootPath, screenshotFolder)).Any();
        if (string.IsNullOrWhiteSpace(evidence) && !hasScreenshotEvidence)
            continue;

        candidates.Add(new IdentityPromotionCandidate(
            ReadTsvCell(cells, headerIndex, "fingerprint"),
            ReadTsvCell(cells, headerIndex, "levelKey"),
            ReadTsvCell(cells, headerIndex, "trueIndexes"),
            label,
            FirstIdentityValue(ReadTsvCell(cells, headerIndex, "candidateKind"), "live-observed moby identity"),
            "live-observed-fingerprint",
            evidence,
            screenshotFolder,
            ReadTsvCell(cells, headerIndex, "promoteScope"),
            status));
    }

    return candidates;
}

string FirstIdentityValue(string value, string fallback)
{
    return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

bool IsConfirmedIdentityResultStatus(string status)
{
    return status.Equals("confirmed", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("proven", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("live-observed", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("promote", StringComparison.OrdinalIgnoreCase);
}

bool IsUsableReleaseIdentityLabel(string label)
{
    if (string.IsNullOrWhiteSpace(label))
        return false;

    string text = label.Trim();
    string lower = text.ToLowerInvariant();
    if (lower.Contains("?") ||
        lower.Contains("unknown") ||
        lower.Contains("candidate") ||
        lower.Contains("related") ||
        lower.Contains("maybe") ||
        lower.Contains("probably"))
    {
        return false;
    }

    string[] vague =
    [
        "object",
        "moby",
        "prop",
        "scenery",
        "actor",
        "enemy",
        "chest",
        "control",
        "helper",
        "marker",
        "special"
    ];
    return !vague.Any(item => string.Equals(lower, item, StringComparison.OrdinalIgnoreCase));
}

string[] IdentityReleaseReviewHeader() =>
[
    "priority",
    "batchKind",
    "batchNumber",
    "questionableCount",
    "scopeQuestionableCount",
    "fingerprint",
    "levelKey",
    "levelName",
    "trueIndexes",
    "currentLabel",
    "likelyLane",
    "rosterLeads",
    "evidenceState",
    "proofStep",
    "editManifestPath",
    "screenshotFolder",
    "resultStatus",
    "observedLabel",
    "candidateKind",
    "evidenceNotes",
    "promoteScope"
];

static IEnumerable<string> IdentityReleaseReviewValues(IdentityReleaseReviewRow row)
{
    yield return row.Priority.ToString(CultureInfo.InvariantCulture);
    yield return row.BatchKind;
    yield return row.BatchNumber.ToString(CultureInfo.InvariantCulture);
    yield return row.QuestionableCount.ToString(CultureInfo.InvariantCulture);
    yield return row.ScopeQuestionableCount.ToString(CultureInfo.InvariantCulture);
    yield return row.Fingerprint;
    yield return row.LevelKey;
    yield return row.LevelName;
    yield return row.TrueIndexes;
    yield return row.CurrentLabel;
    yield return row.LikelyLane;
    yield return row.RosterLeads;
    yield return row.EvidenceState;
    yield return row.ProofStep;
    yield return row.EditManifestPath;
    yield return row.ScreenshotFolder;
    yield return row.ResultStatus;
    yield return row.ObservedLabel;
    yield return row.CandidateKind;
    yield return row.EvidenceNotes;
    yield return row.PromoteScope;
}

string ReadTsvCell(IReadOnlyList<string> cells, IReadOnlyDictionary<string, int> headerIndex, string name)
{
    return headerIndex.TryGetValue(name, out int index) && index >= 0 && index < cells.Count
        ? cells[index]
        : "";
}

string EscapeTsv(string value)
{
    return (value ?? "")
        .Replace("\t", " ", StringComparison.Ordinal)
        .Replace("\r", " ", StringComparison.Ordinal)
        .Replace("\n", " ", StringComparison.Ordinal);
}

string SafeIdentityPathPart(string value)
{
    string cleaned = new((value ?? "").Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-').ToArray());
    while (cleaned.Contains("--", StringComparison.Ordinal))
        cleaned = cleaned.Replace("--", "-", StringComparison.Ordinal);
    return string.IsNullOrWhiteSpace(cleaned) ? "identity" : cleaned.Trim('-');
}

void WriteIdentityContextReport(
    string outDir,
    IReadOnlyList<PrecisionIdentityGroup> rankedQuestionableGroups,
    IReadOnlyList<(LevelDefinition Level, Moby Moby)> allMobys,
    IReadOnlyDictionary<string, ObservedFingerprintGroup> observedFingerprints)
{
    string markdownPath = Path.Combine(outDir, "moby-identity-context-report.md");
    string jsonPath = Path.Combine(outDir, "moby-identity-context-report.json");
    Dictionary<string, List<(LevelDefinition Level, Moby Moby)>> byLevel = allMobys
        .GroupBy(item => item.Level.Key, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

    List<object> jsonRows = new();
    StringBuilder markdown = new();
    markdown.AppendLine("# Moby Identity Context Report");
    markdown.AppendLine();
    markdown.AppendLine("This report is conservative: it does not rename mobys from proximity alone. It surfaces the clues needed to tell reusable actor/model identities from level-local props and helper/control records.");

    foreach (PrecisionIdentityGroup group in rankedQuestionableGroups.Take(IdentityMicroscopeLimit))
    {
        List<(LevelDefinition Level, Moby Moby)> matches = allMobys
            .Where(item => string.Equals(IdentityFingerprint(item.Moby), group.Key, StringComparison.OrdinalIgnoreCase))
            .ToList();
        List<string> levels = matches
            .GroupBy(item => item.Level.DisplayName)
            .OrderByDescending(levelGroup => levelGroup.Count())
            .ThenBy(levelGroup => levelGroup.Key)
            .Select(levelGroup => $"{levelGroup.Key} {levelGroup.Count()}")
            .ToList();
        List<string> specialPointers = matches
            .GroupBy(item => item.Moby.SpecialDataPointer)
            .OrderByDescending(pointerGroup => pointerGroup.Count())
            .ThenBy(pointerGroup => pointerGroup.Key)
            .Take(8)
            .Select(pointerGroup => $"0x{pointerGroup.Key:X8} ({pointerGroup.Count()})")
            .ToList();
        List<string> actorByteAliases = BuildActorByteAliasSummary(group, allMobys);
        List<string> neighbors = BuildNeighborSummary(group, matches, byLevel);
        string evidenceState = observedFingerprints.TryGetValue(group.Key, out ObservedFingerprintGroup? observed)
            ? $"{ObservedStatus(observed)}: {observed.LabelSummary}"
            : "no copied/live label evidence";
        string interpretation = BuildContextInterpretation(group, matches, actorByteAliases, evidenceState);

        markdown.AppendLine();
        markdown.AppendLine($"## {group.Key}");
        markdown.AppendLine();
        markdown.AppendLine($"- Current label: {group.LabelSummary}");
        markdown.AppendLine($"- Count: {group.Count}");
        markdown.AppendLine($"- Evidence state: {evidenceState}");
        markdown.AppendLine($"- Level spread: {string.Join("; ", levels.Take(10))}");
        markdown.AppendLine($"- Special-data pointers: {string.Join("; ", specialPointers)}");
        markdown.AppendLine($"- Same actor-byte exact labels: {(actorByteAliases.Count == 0 ? "none found" : string.Join("; ", actorByteAliases))}");
        markdown.AppendLine($"- Nearby exact objects: {(neighbors.Count == 0 ? "none within 768 units of sampled rows" : string.Join("; ", neighbors))}");
        markdown.AppendLine($"- Read: {interpretation}");

        jsonRows.Add(new
        {
            fingerprint = group.Key,
            group.Count,
            currentLabel = group.LabelSummary,
            evidenceState,
            levels,
            specialDataPointers = specialPointers,
            sameActorByteExactLabels = actorByteAliases,
            nearbyExactObjects = neighbors,
            interpretation
        });
    }

    File.WriteAllText(markdownPath, markdown.ToString());
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
    {
        generatedAt = DateTimeOffset.Now,
        note = jsonRows.Count == 0
            ? "No remaining question-mark moby fingerprints in the current cache."
            : "Conservative context for remaining question-mark moby fingerprints.",
        groups = jsonRows
    }, new JsonSerializerOptions { WriteIndented = true }));
}

string WriteLegacyIdentityEvidenceReport(
    string outDir,
    IReadOnlyList<PrecisionIdentityGroup> rankedQuestionableGroups)
{
    string markdownPath = Path.Combine(outDir, "moby-identity-legacy-evidence.md");
    string jsonPath = Path.Combine(outDir, "moby-identity-legacy-evidence.json");
    Dictionary<string, PrecisionIdentityGroup> questionableByFingerprint = rankedQuestionableGroups
        .ToDictionary(group => group.Key, StringComparer.OrdinalIgnoreCase);
    List<LegacyIdentityEvidence> evidence = ReadLegacyIdentityEvidence().ToList();
    var exactGroups = evidence
        .GroupBy(item => item.Fingerprint, StringComparer.OrdinalIgnoreCase)
        .Select(group => new
        {
            Fingerprint = group.Key,
            Count = group.Count(),
            Labels = group.GroupBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(labelGroup => labelGroup.Count())
                .ThenBy(labelGroup => labelGroup.Key)
                .Select(labelGroup => $"{labelGroup.Key} ({labelGroup.Count()})")
                .ToList(),
            Samples = group.Take(8).Select(item => item.Display).ToList(),
            RemainingQuestionable = questionableByFingerprint.TryGetValue(group.Key, out PrecisionIdentityGroup? questionable) ? questionable.Count : 0,
            Status = LegacyEvidenceStatus(group)
        })
        .OrderByDescending(group => group.RemainingQuestionable)
        .ThenByDescending(group => group.Count)
        .ThenBy(group => group.Fingerprint)
        .ToList();
    var byteGroups = evidence
        .GroupBy(item => $"{item.TypeHex}/{item.SourceByte36Hex}", StringComparer.OrdinalIgnoreCase)
        .Select(group => new
        {
            ByteKey = group.Key,
            Count = group.Count(),
            Labels = group.GroupBy(item => NormalizeLegacyLabel(item.Label), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(labelGroup => labelGroup.Count())
                .ThenBy(labelGroup => labelGroup.Key)
                .Select(labelGroup => $"{labelGroup.Key} ({labelGroup.Count()})")
                .ToList(),
            Fingerprints = group.Select(item => item.Fingerprint).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            Samples = group.Take(8).Select(item => item.Display).ToList()
        })
        .OrderByDescending(group => group.Labels.Count)
        .ThenByDescending(group => group.Count)
        .ThenBy(group => group.ByteKey)
        .ToList();

    StringBuilder markdown = new();
    markdown.AppendLine("# Legacy Moby Identity Evidence");
    markdown.AppendLine();
    markdown.AppendLine("This report joins the copied Windows/user labels back to the current portable cache bytes. It is intentionally conservative: exact-fingerprint matches are strong evidence, while source-byte groups with multiple labels are test leads, not automatic names.");
    markdown.AppendLine();
    markdown.AppendLine("## Exact Fingerprints");
    markdown.AppendLine();
    markdown.AppendLine("| Remaining ? | Observed | Status | Fingerprint | Labels | Examples |");
    markdown.AppendLine("|---:|---:|---|---|---|---|");
    foreach (var group in exactGroups.Take(120))
    {
        markdown.AppendLine($"| {group.RemainingQuestionable} | {group.Count} | {EscapeMarkdown(group.Status)} | `{group.Fingerprint}` | {EscapeMarkdown(string.Join("; ", group.Labels))} | {EscapeMarkdown(string.Join(", ", group.Samples))} |");
    }

    markdown.AppendLine();
    markdown.AppendLine("## Ambiguous Source Bytes");
    markdown.AppendLine();
    markdown.AppendLine("These bytes have more than one old label. Keep them model-pointer or live-test scoped.");
    markdown.AppendLine();
    markdown.AppendLine("| Source byte | Observed | Fingerprints | Labels | Examples |");
    markdown.AppendLine("|---|---:|---:|---|---|");
    foreach (var group in byteGroups.Where(group => group.Labels.Count > 1).Take(80))
    {
        markdown.AppendLine($"| `{group.ByteKey}` | {group.Count} | {group.Fingerprints} | {EscapeMarkdown(string.Join("; ", group.Labels))} | {EscapeMarkdown(string.Join(", ", group.Samples))} |");
    }

    File.WriteAllText(markdownPath, markdown.ToString());
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
    {
        generatedAt = DateTimeOffset.Now,
        note = "Copied Windows/user labels joined to current cache-derived moby bytes.",
        exactFingerprints = exactGroups.Select(group => new
        {
            fingerprint = group.Fingerprint,
            observed = group.Count,
            remainingQuestionable = group.RemainingQuestionable,
            status = group.Status,
            labels = group.Labels,
            samples = group.Samples
        }),
        sourceBytes = byteGroups.Select(group => new
        {
            sourceByte = group.ByteKey,
            observed = group.Count,
            fingerprintCount = group.Fingerprints,
            labels = group.Labels,
            ambiguous = group.Labels.Count > 1,
            samples = group.Samples
        })
    }, new JsonSerializerOptions { WriteIndented = true }));

    return markdownPath;
}

string WriteIdentityFamilyObservationTemplate(
    string outDir,
    IReadOnlyList<(LevelDefinition Level, Moby Moby)> allMobys,
    IReadOnlyList<IdentityTestBatch> testBatches)
{
    string markdownPath = Path.Combine(outDir, "moby-identity-family-observations.md");
    string jsonPath = Path.Combine(outDir, "moby-identity-family-observations.json");
    Dictionary<string, IdentityTestBatch> batchByFingerprint = testBatches
        .GroupBy(batch => batch.Fingerprint, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

    var rows = allMobys
        .Where(item => item.Moby.Type != 0x18 && IsQuestionableIdentity(item.Moby))
        .GroupBy(item => $"{IdentityModelFamilyKey(item.Level, item.Moby)}|{IdentityFingerprint(item.Moby)}", StringComparer.OrdinalIgnoreCase)
        .Select(group =>
        {
            List<(LevelDefinition Level, Moby Moby)> mobys = group
                .OrderBy(item => item.Moby.TrueIndex)
                .ToList();
            (LevelDefinition Level, Moby Moby) first = mobys[0];
            string fingerprint = IdentityFingerprint(first.Moby);
            PrecisionIdentityGroup identityGroup = new(fingerprint, first.Moby.DisplayLabel, first.Moby.CandidateKind, first.Moby.Evidence)
            {
                Count = mobys.Count
            };
            identityGroup.Levels.Add(first.Level.DisplayName);
            foreach ((LevelDefinition sampleLevel, Moby sampleMoby) in mobys.Take(4))
                identityGroup.Samples.Add(new PrecisionIdentitySample(sampleLevel.Key, sampleLevel.DisplayName, sampleMoby.TrueIndex, sampleMoby.Position.X, sampleMoby.Position.Y, sampleMoby.Position.Z));

            IdentityFingerprintParts parts = ParseIdentityFingerprint(fingerprint);
            string lane = IdentityTestLane(identityGroup);
            string proofStep = IdentityTestRecipe(identityGroup, lane);
            string batchFile = batchByFingerprint.TryGetValue(fingerprint, out IdentityTestBatch? batch) &&
                batch.EditManifestPath != null &&
                batch.Samples.Any(sample => string.Equals(sample.LevelKey, first.Level.Key, StringComparison.OrdinalIgnoreCase))
                ? Path.GetRelativePath(workspace.RootPath, batch.EditManifestPath)
                : "";
            string pointerHex = $"0x{first.Moby.SpecialDataPointer:X8}";
            int? matchTrueIndex = first.Moby.SpecialDataPointer == 0 ? first.Moby.TrueIndex : null;
            List<string> rosterCandidates = RosterIdentityCandidates(first.Level.Key, first.Moby).ToList();
            List<string> currentReads = mobys
                .GroupBy(item => item.Moby.DisplayLabel, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(labelGroup => labelGroup.Count())
                .ThenBy(labelGroup => labelGroup.Key)
                .Select(labelGroup => $"{labelGroup.Key} ({labelGroup.Count()})")
                .ToList();

            return new
            {
                SortCount = mobys.Count,
                LevelName = first.Level.DisplayName,
                Fingerprint = fingerprint,
                PointerHex = pointerHex,
                CurrentReads = currentReads,
                RosterCandidates = rosterCandidates,
                BatchFile = batchFile,
                Samples = identityGroup.Samples.ToList(),
                Observation = new
                {
                    levelKey = first.Level.Key,
                    levelName = first.Level.DisplayName,
                    fingerprint,
                    typeHex = parts.TypeHex,
                    sourceByte36Hex = parts.SourceByte36Hex,
                    flag4AHex = parts.Flag4AHex,
                    flag4BHex = parts.Flag4BHex,
                    sourceByte4FHex = parts.SourceByte4FHex,
                    specialDataPointerHex = pointerHex,
                    matchTrueIndex,
                    label = "",
                    displayTargetLabel = "",
                    candidateKind = "",
                    confidence = "live-observed-model-family",
                    color = "",
                    evidence = "",
                    currentReads,
                    rosterCandidates,
                    scopeQuestionableCount = mobys.Count,
                    lane,
                    proofStep,
                    testBatch = batchFile,
                    samples = identityGroup.Samples.Select(sample => new { sample.LevelKey, sample.LevelName, sample.TrueIndex, sample.X, sample.Y, sample.Z })
                }
            };
        })
        .OrderByDescending(row => row.SortCount)
        .ThenBy(row => row.LevelName)
        .ThenBy(row => row.Fingerprint)
        .ToList();

    StringBuilder markdown = new();
    markdown.AppendLine("# Moby Identity Family Observations");
    markdown.AppendLine();
    markdown.AppendLine(rows.Count == 0
        ? "No level-scoped family observation rows are currently needed; every cached moby has a concrete or useful inferred identity."
        : "These blank rows are scoped by level plus special-data pointer, so a confirmed live name can be applied without renaming unrelated objects that only share the broad fingerprint.");
    markdown.AppendLine();
    markdown.AppendLine("| Scope ? | Level | Fingerprint | Pointer | Current reads | Roster leads | Samples | Batch |");
    markdown.AppendLine("|---:|---|---|---|---|---|---|---|");
    foreach (var row in rows.Take(160))
    {
        markdown.AppendLine($"| {row.SortCount} | {EscapeMarkdown(row.LevelName)} | `{row.Fingerprint}` | `{row.PointerHex}` | {EscapeMarkdown(string.Join("; ", row.CurrentReads))} | {EscapeMarkdown(row.RosterCandidates.Count == 0 ? "" : string.Join(", ", row.RosterCandidates))} | {EscapeMarkdown(string.Join(", ", row.Samples.Select(sample => sample.Display)))} | {EscapeMarkdown(row.BatchFile)} |");
    }

    File.WriteAllText(markdownPath, markdown.ToString());
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
    {
        generatedAt = DateTimeOffset.Now,
        instructions = "Fill label/displayTargetLabel/candidateKind/evidence only after a live test confirms this exact level-local model family. These rows include levelKey and specialDataPointerHex so the editor will not spread the name to unrelated matching fingerprints.",
        observations = rows.Select(row => row.Observation)
    }, new JsonSerializerOptions { WriteIndented = true }));
    return markdownPath;
}

IReadOnlyList<string> RosterIdentityCandidates(string levelKey, Moby moby)
{
    string lane = moby.Type == 0x00 || moby.Flag4B == 0xFF
        ? "prop-or-control"
        : "actor";

    if (lane != "actor" && moby.Type != 0x20)
        return Array.Empty<string>();

    return levelKey.ToLowerInvariant() switch
    {
        "stonehill" when moby.Type == 0x20 => ["Ram", "Shepherd", "Sheep fodder"],
        "townsquare" when moby.Type == 0x20 => ["Bull", "Toreador Gnorc", "Chicken fodder", "Egg Thief"],
        "toasty" when moby.Type == 0x20 => ["Dog", "Shepherd", "Toasty boss"],
        "peacekeepers" when moby.Type == 0x20 => ["Foot Soldier Gnorc", "Cannon Gnorc", "Spear Gnorc", "Rabbit fodder"],
        "drycanyon" when moby.Type == 0x20 => ["Bird", "Big Gnorc with bird", "Gnorc with gun", "Rabbit fodder"],
        "clifftown" when moby.Type == 0x20 => ["Vulture", "Fat Lady", "Mama Gnorc", "Armored fodder"],
        "icecavern" when moby.Type == 0x20 => ["Snowball Gnorc", "Armored Gnorc", "Bat fodder"],
        "doctorshemp" when moby.Type == 0x20 => ["Doctor Shemp boss", "Kamikaze tribesman", "Strongman enemy"],
        "magiccrafters" when moby.Type == 0x20 => ["Armored Druid", "Magic Druid", "Egg Thief"],
        "alpineridge" when moby.Type == 0x20 => ["Green Druid", "Beast", "Elder Wizard", "Egg Thief"],
        "highcaves" when moby.Type == 0x20 => ["Armored Druid", "Spider", "Tornado Wizard", "Fairy helper"],
        "wizardpeak" when moby.Type == 0x20 => ["Armored Druid", "Magic Druid", "Tornado Wizard", "Egg Thief"],
        "blowhard" when moby.Type == 0x20 => ["Blowhard boss", "Magic Druid"],
        "beastmakers" when moby.Type == 0x20 => ["Boar", "Electric Gnorc", "Chicken fodder"],
        "terracevillage" when moby.Type == 0x20 => ["Gnorc soldier", "Electric Gnorc", "Bird"],
        "treetops" when moby.Type == 0x20 => ["Strongarm", "Banana Boy", "Bird"],
        "metalhead" when moby.Type == 0x20 => ["Metalback Spider", "Banana Boy", "Bird", "Metalhead boss"],
        "dreamweavers" when moby.Type == 0x20 => ["Clock Fool", "Mushroom/Fool enemy"],
        "darkpassage" when moby.Type == 0x20 => ["Lamp Fool", "Devil Cupid", "Turtle / Mutant Turtle", "Puppy / Devil Dog"],
        "loftycastle" when moby.Type == 0x20 => ["Cupid", "Armored Fool", "Devil Dog"],
        "mistybog" when moby.Type == 0x20 => ["Attack Frog", "Dragon-eating Plant", "Chicken fodder"],
        "hauntedtowers" when moby.Type == 0x20 => ["Tin Soldier", "Wizard/Sorcerer", "Bomb-throwing troll"],
        "jacques" when moby.Type == 0x20 => ["Clock Fool", "Boxer Fool", "Nightmare Beast", "Jacques boss"],
        "gnorccove" when moby.Type == 0x20 => ["Dockworker", "Barrel Engineer", "TNT Wrangler", "Rat"],
        "twilightharbor" when moby.Type == 0x20 => ["Gnorc Soldier", "Machine Gun Gnorc", "Grenade Gnorc"],
        "gnastygnorc" when moby.Type == 0x20 => ["Gnasty Gnorc boss", "Thief", "Gnorc guard"],
        "gnastysloot" when moby.Type == 0x20 => ["Thief"],
        _ => Array.Empty<string>()
    };
}

IReadOnlyList<string> RosterLeadsForSamples(IReadOnlyList<(LevelDefinition Level, Moby Moby)> samples)
{
    return samples
        .SelectMany(sample => RosterIdentityCandidates(sample.Level.Key, sample.Moby))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(8)
        .ToList();
}

IEnumerable<LegacyIdentityEvidence> ReadLegacyIdentityEvidence()
{
    foreach (string overridePath in EnumerateObservationFiles())
    {
        using JsonDocument? document = TryReadJsonDocument(overridePath);
        if (document == null)
            continue;

        string levelKey = ReadJsonString(document.RootElement, "levelKey", LevelKeyFromObservationFile(overridePath));
        if (string.IsNullOrWhiteSpace(levelKey))
            continue;

        string cachePath = Path.Combine(workspace.RootPath, "editor-cache", $"{levelKey}-mobys.json");
        if (!File.Exists(cachePath))
            continue;

        Dictionary<int, Moby> byTrueIndex = MobyLoader.LoadCached(cachePath)
            .Where(moby => moby.TrueIndex >= 0)
            .ToDictionary(moby => moby.TrueIndex);
        if (!document.RootElement.TryGetProperty("mobys", out JsonElement items) || items.ValueKind != JsonValueKind.Array)
            continue;

        foreach (JsonElement item in items.EnumerateArray())
        {
            int trueIndex = ReadJsonInt32(item, "trueIndex", ReadJsonInt32(item, "index", -1));
            if (trueIndex < 0 || !byTrueIndex.TryGetValue(trueIndex, out Moby? moby))
                continue;

            string label = ReadJsonString(item, "displayTargetLabel", ReadJsonString(item, "label")).Trim();
            if (string.IsNullOrWhiteSpace(label))
                continue;

            string source = overridePath.EndsWith("-live-validation-overrides.json", StringComparison.OrdinalIgnoreCase)
                ? "live"
                : "user";
            yield return new LegacyIdentityEvidence(
                levelKey,
                LevelDisplayName(levelKey),
                trueIndex,
                label,
                ReadJsonString(item, "candidateKind"),
                $"0x{moby.Type:X2}",
                $"0x{moby.SourceByte36:X2}",
                $"0x{moby.SpecialDataPointer:X8}",
                $"0x{moby.Flag4A:X2}",
                $"0x{moby.Flag4B:X2}",
                $"0x{moby.SourceByte4F:X2}",
                $"0x{moby.State:X2}",
                IdentityFingerprint(moby),
                source);
        }
    }
}

JsonDocument? TryReadJsonDocument(string path)
{
    try
    {
        return JsonDocument.Parse(File.ReadAllText(path));
    }
    catch
    {
        return null;
    }
}

string LegacyEvidenceStatus(IEnumerable<LegacyIdentityEvidence> group)
{
    List<LegacyIdentityEvidence> items = group.ToList();
    int labelCount = items
        .Select(item => NormalizeLegacyLabel(item.Label))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
    if (labelCount != 1)
        return "conflict";
    if (items.Any(item => IsSketchyObservedLabel(item.Label)))
        return "sketchy";
    return items.Count >= 2 ? "clean exact evidence" : "single exact observation";
}

string NormalizeLegacyLabel(string label)
{
    return label
        .Trim()
        .Replace("Theif", "Thief", StringComparison.OrdinalIgnoreCase)
        .Replace("Shepard", "Shepherd", StringComparison.OrdinalIgnoreCase)
        .Replace("Sping", "Spring", StringComparison.OrdinalIgnoreCase);
}

string WriteIdentityLevelWorkbenchReport(
    string outDir,
    IReadOnlyList<PrecisionIdentityGroup> rankedQuestionableGroups,
    IReadOnlyList<(LevelDefinition Level, Moby Moby)> allMobys,
    IReadOnlyDictionary<string, ObservedFingerprintGroup> observedFingerprints,
    IReadOnlyList<IdentityTestBatch> testBatches)
{
    string markdownPath = Path.Combine(outDir, "moby-identity-level-workbench.md");
    string jsonPath = Path.Combine(outDir, "moby-identity-level-workbench.json");
    Dictionary<string, PrecisionIdentityGroup> groupByFingerprint = rankedQuestionableGroups
        .ToDictionary(group => group.Key, StringComparer.OrdinalIgnoreCase);
    Dictionary<string, IdentityTestBatch> batchByFingerprint = testBatches
        .GroupBy(batch => batch.Fingerprint, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    Dictionary<string, List<(LevelDefinition Level, Moby Moby)>> byLevel = allMobys
        .GroupBy(item => item.Level.Key, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

    var levelRows = allMobys
        .GroupBy(item => item.Level.Key, StringComparer.OrdinalIgnoreCase)
        .Select(levelGroup =>
        {
            LevelDefinition level = levelGroup.First().Level;
            List<(LevelDefinition Level, Moby Moby)> mobys = levelGroup.ToList();
            List<IGrouping<string, (LevelDefinition Level, Moby Moby)>> questionableGroups = mobys
                .Where(item => IsQuestionableIdentity(item.Moby))
                .GroupBy(item => IdentityFingerprint(item.Moby), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .ToList();
            return new
            {
                Level = level,
                Total = mobys.Count,
                Exact = mobys.Count(item => IsExactIdentity(item.Moby)),
                Questionable = questionableGroups.Sum(group => group.Count()),
                Groups = questionableGroups
            };
        })
        .Where(row => row.Questionable > 0)
        .OrderByDescending(row => row.Questionable)
        .ThenBy(row => row.Level.DisplayName)
        .ToList();

    List<object> jsonLevels = new();
    StringBuilder markdown = new();
    markdown.AppendLine("# Moby Identity Level Workbench");
    markdown.AppendLine();
    markdown.AppendLine(levelRows.Count == 0
        ? "No level workbench rows are currently needed; every cached level has zero question-mark mobys."
        : "This is the practical checklist for the remaining question-mark mobys. Work one level at a time: each row gives the repeated fingerprint, how many records use it in that level, the nearest already-confirmed objects, and the generated test batch when available.");
    markdown.AppendLine();
    markdown.AppendLine("| Level | Questionable | Exact/observed | Top unresolved fingerprints |");
    markdown.AppendLine("|---|---:|---:|---|");
    foreach (var row in levelRows.Take(20))
    {
        string top = string.Join("<br>", row.Groups.Take(4).Select(group =>
        {
            string label = groupByFingerprint.TryGetValue(group.Key, out PrecisionIdentityGroup? identityGroup)
                ? identityGroup.Label
                : "questionable";
            return $"{group.Count()}x `{group.Key}` {EscapeMarkdown(label)}";
        }));
        markdown.AppendLine($"| {EscapeMarkdown(row.Level.DisplayName)} | {row.Questionable} | {row.Exact} | {top} |");
    }

    foreach (var row in levelRows.Take(12))
    {
        markdown.AppendLine();
        markdown.AppendLine($"## {row.Level.DisplayName}");
        markdown.AppendLine();
        markdown.AppendLine($"- Questionable records: {row.Questionable}/{row.Total}");
        markdown.AppendLine($"- Exact or observed records: {row.Exact}");
        markdown.AppendLine();
        markdown.AppendLine("| Count | Fingerprint | Current read | Evidence state | Pointer groups | Best samples | Nearest exact objects | Batch |");
        markdown.AppendLine("|---:|---|---|---|---|---|---|---|");

        List<object> jsonGroups = new();
        foreach (IGrouping<string, (LevelDefinition Level, Moby Moby)> fingerprintGroup in row.Groups.Take(12))
        {
            List<(LevelDefinition Level, Moby Moby)> samples = fingerprintGroup
                .OrderBy(item => item.Moby.TrueIndex)
                .ToList();
            PrecisionIdentityGroup? identityGroup = groupByFingerprint.TryGetValue(fingerprintGroup.Key, out PrecisionIdentityGroup? foundGroup)
                ? foundGroup
                : null;
            string currentRead = identityGroup?.Label ?? samples[0].Moby.DisplayLabel;
            string evidenceState = observedFingerprints.TryGetValue(fingerprintGroup.Key, out ObservedFingerprintGroup? observed)
                ? $"{ObservedStatus(observed)}: {observed.LabelSummary}"
                : "no copied/live label evidence";
            string pointerGroups = string.Join(", ", samples
                .GroupBy(item => item.Moby.SpecialDataPointer)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Take(4)
                .Select(group => $"0x{group.Key:X8} ({group.Count()})"));
            string sampleText = string.Join(", ", samples.Take(4).Select(item => $"T{item.Moby.TrueIndex}"));
            string neighborText = string.Join("; ", samples.Take(2)
                .Select(sample => $"T{sample.Moby.TrueIndex}: {BuildNearestExactObjectSummary(sample, fingerprintGroup.Key, byLevel)}"));
            string batchPath = batchByFingerprint.TryGetValue(fingerprintGroup.Key, out IdentityTestBatch? batch) && batch.EditManifestPath != null
                ? Path.GetRelativePath(workspace.RootPath, batch.EditManifestPath)
                : "";
            string batchText = string.IsNullOrWhiteSpace(batchPath) ? "" : $"`{batchPath}`";

            markdown.AppendLine($"| {samples.Count} | `{fingerprintGroup.Key}` | {EscapeMarkdown(currentRead)} | {EscapeMarkdown(evidenceState)} | {EscapeMarkdown(pointerGroups)} | {EscapeMarkdown(sampleText)} | {EscapeMarkdown(neighborText)} | {batchText} |");

            jsonGroups.Add(new
            {
                fingerprint = fingerprintGroup.Key,
                count = samples.Count,
                currentRead,
                evidenceState,
                lane = identityGroup == null ? "" : IdentityTestLane(identityGroup),
                proofStep = identityGroup == null ? "" : IdentityTestRecipe(identityGroup, IdentityTestLane(identityGroup)),
                pointerGroups,
                samples = samples.Take(8).Select(item => new
                {
                    trueIndex = item.Moby.TrueIndex,
                    x = item.Moby.Position.X,
                    y = item.Moby.Position.Y,
                    z = item.Moby.Position.Z,
                    specialDataPointer = $"0x{item.Moby.SpecialDataPointer:X8}",
                    nearestExactObjects = BuildNearestExactObjectSummary(item, fingerprintGroup.Key, byLevel)
                }),
                batch = batchPath
            });
        }

        jsonLevels.Add(new
        {
            levelKey = row.Level.Key,
            levelName = row.Level.DisplayName,
            totalMobys = row.Total,
            exactOrObserved = row.Exact,
            questionable = row.Questionable,
            groups = jsonGroups
        });
    }

    File.WriteAllText(markdownPath, markdown.ToString());
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
    {
        generatedAt = DateTimeOffset.Now,
        note = "Level-first workbench for remaining questionable moby identities.",
        levels = jsonLevels
    }, new JsonSerializerOptions { WriteIndented = true }));
    return markdownPath;
}

string WriteUnknownMobyTriageReport(
    string outDir,
    IReadOnlyList<PrecisionIdentityGroup> rankedQuestionableGroups,
    IReadOnlyList<(LevelDefinition Level, Moby Moby)> allMobys,
    IReadOnlyDictionary<string, ObservedFingerprintGroup> observedFingerprints,
    IReadOnlyList<IdentityTestBatch> testBatches)
{
    string markdownPath = Path.Combine(outDir, "moby-unknown-link-triage.md");
    string jsonPath = Path.Combine(outDir, "moby-unknown-link-triage.json");
    Dictionary<string, PrecisionIdentityGroup> groupByFingerprint = rankedQuestionableGroups
        .ToDictionary(group => group.Key, StringComparer.OrdinalIgnoreCase);
    Dictionary<string, IdentityTestBatch> batchByFingerprint = testBatches
        .GroupBy(batch => batch.Fingerprint, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    Dictionary<string, List<(LevelDefinition Level, Moby Moby)>> byLevel = allMobys
        .GroupBy(item => item.Level.Key, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

    List<UnknownMobyTriageRow> rows = allMobys
        .Where(item => IsQuestionableIdentity(item.Moby))
        .Select(item =>
        {
            string fingerprint = IdentityFingerprint(item.Moby);
            PrecisionIdentityGroup? group = groupByFingerprint.TryGetValue(fingerprint, out PrecisionIdentityGroup? foundGroup)
                ? foundGroup
                : null;
            string lane = group == null ? MobyIdentityLaneFromMoby(item.Moby) : IdentityTestLane(group);
            string proofStep = group == null
                ? IdentityTestRecipeForSample(lane, new PrecisionIdentitySample(item.Level.Key, item.Level.DisplayName, item.Moby.TrueIndex, item.Moby.Position.X, item.Moby.Position.Y, item.Moby.Position.Z))
                : IdentityTestRecipe(group, lane);
            string evidenceState = observedFingerprints.TryGetValue(fingerprint, out ObservedFingerprintGroup? observed)
                ? $"{ObservedStatus(observed)}: {observed.LabelSummary}"
                : "no copied/live label evidence";
            string batchPath = batchByFingerprint.TryGetValue(fingerprint, out IdentityTestBatch? batch) && batch.EditManifestPath != null
                ? Path.GetRelativePath(workspace.RootPath, batch.EditManifestPath)
                : "";
            string directLinks = BuildUnknownDirectLinkSummary(item, byLevel);
            string pointerFamily = BuildUnknownPointerFamilySummary(item, byLevel);
            string nearbyKnown = BuildNearestExactObjectSummary(item, fingerprint, byLevel);
            string nearbyUnknown = BuildNearbyQuestionableSummary(item, fingerprint, byLevel);
            string suggestedProof = BuildUnknownMobyProofHint(lane, directLinks, pointerFamily, nearbyKnown, nearbyUnknown, proofStep);
            int priority = UnknownMobyTriagePriority(item.Moby, lane, directLinks, pointerFamily, nearbyKnown, nearbyUnknown);

            return new UnknownMobyTriageRow(
                item.Level.Key,
                item.Level.DisplayName,
                item.Moby.TrueIndex,
                fingerprint,
                item.Moby.DisplayLabel,
                lane,
                evidenceState,
                directLinks,
                pointerFamily,
                nearbyKnown,
                nearbyUnknown,
                suggestedProof,
                batchPath,
                priority);
        })
        .OrderBy(row => row.Priority)
        .ThenBy(row => row.LevelName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(row => row.TrueIndex)
        .ToList();

    StringBuilder markdown = new();
    markdown.AppendLine("# Unknown Moby Link Triage");
    markdown.AppendLine();
    markdown.AppendLine("Every unresolved moby is probably doing something. This report does not name them automatically; it shows the strongest relationship clues so testers can run focused proof passes before promoting labels.");
    markdown.AppendLine();
    markdown.AppendLine($"- Unknown rows: {rows.Count}");
    markdown.AppendLine($"- Rows with direct editor links: {rows.Count(row => !row.DirectLinks.StartsWith("none", StringComparison.OrdinalIgnoreCase))}");
    markdown.AppendLine($"- Rows with shared special-data families: {rows.Count(row => !row.PointerFamily.StartsWith("none", StringComparison.OrdinalIgnoreCase) && !row.PointerFamily.StartsWith("unique", StringComparison.OrdinalIgnoreCase))}");
    markdown.AppendLine();
    markdown.AppendLine("## Highest Priority Unknowns");
    markdown.AppendLine();
    markdown.AppendLine("| Priority | Level | Moby | Current read | Lane | Relationship lead | Suggested proof | Batch |");
    markdown.AppendLine("|---:|---|---:|---|---|---|---|---|");
    foreach (UnknownMobyTriageRow row in rows.Take(80))
    {
        string relationship = FirstMeaningfulLead(row.DirectLinks, row.PointerFamily, row.NearbyKnown, row.NearbyUnknown);
        string batchText = string.IsNullOrWhiteSpace(row.BatchPath) ? "" : $"`{row.BatchPath}`";
        markdown.AppendLine($"| {row.Priority} | {EscapeMarkdown(row.LevelName)} | T{row.TrueIndex} | {EscapeMarkdown(row.CurrentRead)} | {EscapeMarkdown(row.Lane)} | {EscapeMarkdown(relationship)} | {EscapeMarkdown(row.SuggestedProof)} | {batchText} |");
    }

    foreach (IGrouping<string, UnknownMobyTriageRow> levelGroup in rows
        .GroupBy(row => row.LevelName, StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(group => group.Count())
        .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
        .Take(12))
    {
        markdown.AppendLine();
        markdown.AppendLine($"## {levelGroup.Key}");
        markdown.AppendLine();
        markdown.AppendLine("| Moby | Current read | Direct links | Special-data family | Nearby confirmed | Nearby unknowns | Proof focus |");
        markdown.AppendLine("|---:|---|---|---|---|---|---|");
        foreach (UnknownMobyTriageRow row in levelGroup.OrderBy(row => row.Priority).ThenBy(row => row.TrueIndex).Take(24))
            markdown.AppendLine($"| T{row.TrueIndex} | {EscapeMarkdown(row.CurrentRead)} | {EscapeMarkdown(row.DirectLinks)} | {EscapeMarkdown(row.PointerFamily)} | {EscapeMarkdown(row.NearbyKnown)} | {EscapeMarkdown(row.NearbyUnknown)} | {EscapeMarkdown(row.SuggestedProof)} |");
    }

    File.WriteAllText(markdownPath, markdown.ToString());
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
    {
        generatedAt = DateTimeOffset.Now,
        note = "Relationship-focused triage for unresolved mobys. Use as proof planning, not automatic naming.",
        rows
    }, new JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine($"Unknown moby link triage: {rows.Count} unresolved row(s), report={markdownPath}");
    return markdownPath;
}

string WriteUnknownMobyClusterMapReport(
    string outDir,
    IReadOnlyList<(LevelDefinition Level, Moby Moby)> allMobys,
    IReadOnlyList<IdentityTestBatch> testBatches)
{
    string markdownPath = Path.Combine(outDir, "moby-unknown-cluster-map.md");
    string jsonPath = Path.Combine(outDir, "moby-unknown-cluster-map.json");
    Dictionary<string, IdentityTestBatch> batchByFingerprint = testBatches
        .GroupBy(batch => batch.Fingerprint, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    List<UnknownMobyClusterRow> rows = BuildUnknownMobyClusterRows(allMobys, batchByFingerprint)
        .OrderBy(row => row.Priority)
        .ThenByDescending(row => row.UnknownCount)
        .ThenBy(row => row.LevelName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(row => row.ClusterKey, StringComparer.OrdinalIgnoreCase)
        .ToList();

    StringBuilder markdown = new();
    markdown.AppendLine("# Unknown Moby Cluster Map");
    markdown.AppendLine();
    markdown.AppendLine("This collapses unresolved mobys into proof packets. Start with direct-link clusters, then shared special-data families, then repeated local model groups. A cluster is a place to test relationships, not an automatic label.");
    markdown.AppendLine();
    markdown.AppendLine($"- Proof clusters: {rows.Count}");
    markdown.AppendLine($"- Direct-link clusters: {rows.Count(row => row.ClusterKind.Equals("Direct link", StringComparison.OrdinalIgnoreCase))}");
    markdown.AppendLine($"- Shared special-data families: {rows.Count(row => row.ClusterKind.Equals("Shared special data", StringComparison.OrdinalIgnoreCase))}");
    markdown.AppendLine($"- Repeated local model/proximity groups: {rows.Count(row => row.ClusterKind.Equals("Local repeated model", StringComparison.OrdinalIgnoreCase))}");
    markdown.AppendLine();
    markdown.AppendLine("## Best Next Proof Packets");
    markdown.AppendLine();
    markdown.AppendLine("| Priority | Kind | Level | Unknowns | Current reads | Known anchors | Relationship clue | Proof move | Batches |");
    markdown.AppendLine("|---:|---|---|---:|---|---|---|---|---|");
    foreach (UnknownMobyClusterRow row in rows.Take(80))
    {
        string batches = row.BatchPaths.Count == 0 ? "" : string.Join("<br>", row.BatchPaths.Select(path => $"`{path}`"));
        markdown.AppendLine($"| {row.Priority} | {EscapeMarkdown(row.ClusterKind)} | {EscapeMarkdown(row.LevelName)} | {row.UnknownCount} | {EscapeMarkdown(string.Join("; ", row.CurrentReads))} | {EscapeMarkdown(EmptyAsNone(row.KnownAnchors))} | {EscapeMarkdown(row.RelationshipClue)} | {EscapeMarkdown(row.ProofMove)} | {batches} |");
    }

    foreach (IGrouping<string, UnknownMobyClusterRow> levelGroup in rows
        .GroupBy(row => row.LevelName, StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(group => group.Sum(row => row.UnknownCount))
        .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
        .Take(12))
    {
        markdown.AppendLine();
        markdown.AppendLine($"## {levelGroup.Key}");
        markdown.AppendLine();
        markdown.AppendLine("| Kind | Unknown records | Known anchors | Relationship clue | Proof move |");
        markdown.AppendLine("|---|---|---|---|---|");
        foreach (UnknownMobyClusterRow row in levelGroup.OrderBy(row => row.Priority).ThenByDescending(row => row.UnknownCount).Take(18))
            markdown.AppendLine($"| {EscapeMarkdown(row.ClusterKind)} | {EscapeMarkdown(string.Join(", ", row.UnknownRecords))} | {EscapeMarkdown(EmptyAsNone(row.KnownAnchors))} | {EscapeMarkdown(row.RelationshipClue)} | {EscapeMarkdown(row.ProofMove)} |");
    }

    File.WriteAllText(markdownPath, markdown.ToString());
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
    {
        generatedAt = DateTimeOffset.Now,
        note = "Clustered proof packets for unresolved mobys. Use for tester planning, not automatic identity promotion.",
        clusters = rows
    }, new JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine($"Unknown moby cluster map: {rows.Count} proof cluster(s), report={markdownPath}");
    return markdownPath;
}

string WriteUnknownMobyClusterReviewPacket(
    string outDir,
    IReadOnlyList<(LevelDefinition Level, Moby Moby)> allMobys,
    IReadOnlyList<IdentityTestBatch> testBatches)
{
    string reviewRoot = Path.Combine(workspace.RootPath, "_local", "identity-review", "clusters");
    string screenshotRoot = Path.Combine(reviewRoot, "screenshots");
    string resultsRoot = Path.Combine(reviewRoot, "results");
    string promotionRoot = Path.Combine(reviewRoot, "promotion-candidates");
    Directory.CreateDirectory(screenshotRoot);
    Directory.CreateDirectory(resultsRoot);
    Directory.CreateDirectory(promotionRoot);

    Dictionary<string, IdentityTestBatch> batchByFingerprint = testBatches
        .GroupBy(batch => batch.Fingerprint, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    List<UnknownMobyClusterReviewRow> rows = BuildUnknownMobyClusterRows(allMobys, batchByFingerprint)
        .OrderBy(row => row.Priority)
        .ThenByDescending(row => row.UnknownCount)
        .ThenBy(row => row.LevelName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(row => row.ClusterKey, StringComparer.OrdinalIgnoreCase)
        .Select((row, index) => BuildUnknownMobyClusterReviewRow(index + 1, row, screenshotRoot))
        .ToList();

    string templatePath = Path.Combine(resultsRoot, "moby-cluster-results-template.tsv");
    WriteUnknownMobyClusterResultsTsv(templatePath, rows);

    string workingPath = Path.Combine(resultsRoot, "moby-cluster-results.tsv");
    if (!File.Exists(workingPath) || !HasUnknownMobyClusterTesterResults(workingPath))
        WriteUnknownMobyClusterResultsTsv(workingPath, rows);

    int testerVerdicts = CountUnknownMobyClusterTesterVerdicts(workingPath);
    List<UnknownMobyClusterPromotionCandidate> promotionCandidates = ReadUnknownMobyClusterPromotionCandidates(workingPath);
    string promotionJsonPath = Path.Combine(promotionRoot, "moby-cluster-promotion-candidates.json");
    File.WriteAllText(promotionJsonPath, JsonSerializer.Serialize(new
    {
        generatedAt = DateTimeOffset.Now,
        instructions = "Cluster candidates are not auto-applied. Review evidence and apply only concrete labels with matching screenshots or notes.",
        sourceResults = Path.GetRelativePath(workspace.RootPath, workingPath),
        candidateCount = promotionCandidates.Count,
        candidates = promotionCandidates
    }, new JsonSerializerOptions { WriteIndented = true }));

    string readmePath = Path.Combine(reviewRoot, "README.md");
    StringBuilder readme = new();
    readme.AppendLine("# Unknown Moby Cluster Review");
    readme.AppendLine();
    readme.AppendLine("This folder turns unresolved mobys into proof packets. The goal is to collapse whole clusters after evidence, not to guess labels from byte patterns.");
    readme.AppendLine();
    readme.AppendLine("## Workflow");
    readme.AppendLine();
    readme.AppendLine("1. Start with the lowest priority rows in the working results sheet.");
    readme.AppendLine("2. Load the listed batch file when one exists, or use the level/object list to inspect the cluster in the editor.");
    readme.AppendLine("3. Test the cluster in-game with a disposable BIN/CUE.");
    readme.AppendLine("4. Put screenshots or short clips in the row's screenshot folder.");
    readme.AppendLine($"5. Fill `{Path.GetRelativePath(workspace.RootPath, workingPath)}`.");
    readme.AppendLine("6. Use `clusterVerdict` to describe the relationship before trying to name the objects.");
    readme.AppendLine();
    readme.AppendLine("## Cluster Verdicts");
    readme.AppendLine();
    readme.AppendLine("- `same-object`: the unknown records are the same model/behavior and can be labeled together.");
    readme.AppendLine("- `variant-family`: same family, but reward/state/model variants need separate names.");
    readme.AppendLine("- `support-control`: helper/control records tied to a visible object or behavior.");
    readme.AppendLine("- `not-related`: the grouping was misleading and should not be used for promotion.");
    readme.AppendLine("- `needs-live-test`: not enough evidence yet.");
    readme.AppendLine();
    readme.AppendLine("## Generated Files");
    readme.AppendLine();
    readme.AppendLine($"- Results template: `{Path.GetRelativePath(workspace.RootPath, templatePath)}`");
    readme.AppendLine($"- Working results: `{Path.GetRelativePath(workspace.RootPath, workingPath)}`");
    readme.AppendLine($"- Promotion review candidates: `{Path.GetRelativePath(workspace.RootPath, promotionJsonPath)}`");
    readme.AppendLine($"- Screenshot folders: `{Path.GetRelativePath(workspace.RootPath, screenshotRoot)}`");
    File.WriteAllText(readmePath, readme.ToString());

    string packetPath = Path.Combine(outDir, "moby-unknown-cluster-review.md");
    StringBuilder markdown = new();
    markdown.AppendLine("# Unknown Moby Cluster Review Packet");
    markdown.AppendLine();
    markdown.AppendLine("This is the tester-facing queue for reducing the remaining unknown mobys by proof cluster.");
    markdown.AppendLine();
    markdown.AppendLine($"- Review folder: `{Path.GetRelativePath(workspace.RootPath, reviewRoot)}`");
    markdown.AppendLine($"- Cluster rows generated: {rows.Count}");
    markdown.AppendLine($"- Rows with tester verdicts: {testerVerdicts}");
    markdown.AppendLine($"- Promotion review candidates: {promotionCandidates.Count}");
    markdown.AppendLine("- Cluster verdicts do not auto-promote labels. They are evidence for the next naming pass.");
    markdown.AppendLine();
    markdown.AppendLine("## First Cluster Rows");
    markdown.AppendLine();
    markdown.AppendLine("| Priority | Kind | Level | Unknowns | Current reads | Known anchors | Relationship clue | Proof move | Batch files | Screenshot folder |");
    markdown.AppendLine("|---:|---|---|---:|---|---|---|---|---|---|");
    foreach (UnknownMobyClusterReviewRow row in rows.Take(40))
    {
        string batches = string.IsNullOrWhiteSpace(row.BatchPaths) ? "" : row.BatchPaths.Replace(";", "<br>", StringComparison.Ordinal);
        markdown.AppendLine($"| {row.Priority} | {EscapeMarkdown(row.ClusterKind)} | {EscapeMarkdown(row.LevelName)} | {row.UnknownCount} | {EscapeMarkdown(row.CurrentReads)} | {EscapeMarkdown(row.KnownAnchors)} | {EscapeMarkdown(row.RelationshipClue)} | {EscapeMarkdown(row.ProofMove)} | {EscapeMarkdown(batches)} | {EscapeMarkdown(row.ScreenshotFolder)} |");
    }
    if (promotionCandidates.Count > 0)
    {
        markdown.AppendLine();
        markdown.AppendLine("## Promotion Review Candidates");
        markdown.AppendLine();
        markdown.AppendLine("| Priority | Verdict | Level | Observed label | Unknowns | Evidence |");
        markdown.AppendLine("|---:|---|---|---|---:|---|");
        foreach (UnknownMobyClusterPromotionCandidate candidate in promotionCandidates.Take(32))
            markdown.AppendLine($"| {candidate.Priority} | {EscapeMarkdown(candidate.ClusterVerdict)} | {EscapeMarkdown(candidate.LevelName)} | {EscapeMarkdown(candidate.ObservedLabel)} | {candidate.UnknownCount} | {EscapeMarkdown(candidate.Evidence)} |");
    }
    File.WriteAllText(packetPath, markdown.ToString());

    string jsonPath = Path.Combine(outDir, "moby-unknown-cluster-review.json");
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
    {
        generatedAt = DateTimeOffset.Now,
        reviewRoot = Path.GetRelativePath(workspace.RootPath, reviewRoot),
        resultsTemplate = Path.GetRelativePath(workspace.RootPath, templatePath),
        workingResults = Path.GetRelativePath(workspace.RootPath, workingPath),
        promotionCandidates = Path.GetRelativePath(workspace.RootPath, promotionJsonPath),
        rowCount = rows.Count,
        testerVerdicts,
        promotionCandidateCount = promotionCandidates.Count,
        rows
    }, new JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine($"Unknown moby cluster review packet: rows={rows.Count}, testerVerdicts={testerVerdicts}, promotionCandidates={promotionCandidates.Count}, results={workingPath}");
    return packetPath;
}

UnknownMobyClusterReviewRow BuildUnknownMobyClusterReviewRow(
    int reviewPriority,
    UnknownMobyClusterRow row,
    string screenshotRoot)
{
    string folderName = $"{reviewPriority:000}-{SafeIdentityPathPart(row.LevelKey)}-{SafeIdentityPathPart(row.ClusterKind)}";
    string screenshotFolder = Path.Combine(screenshotRoot, folderName);
    Directory.CreateDirectory(screenshotFolder);
    return new UnknownMobyClusterReviewRow(
        reviewPriority,
        row.ClusterKind,
        row.ClusterKey,
        row.LevelKey,
        row.LevelName,
        row.UnknownCount,
        string.Join(", ", row.UnknownRecords),
        string.Join("; ", row.CurrentReads),
        EmptyAsNone(row.KnownAnchors),
        row.RelationshipClue,
        row.ProofMove,
        string.Join("; ", row.BatchPaths),
        Path.GetRelativePath(workspace.RootPath, screenshotFolder),
        "",
        "",
        "",
        "",
        "");
}

void WriteUnknownMobyClusterResultsTsv(string path, IReadOnlyList<UnknownMobyClusterReviewRow> rows)
{
    StringBuilder builder = new();
    builder.AppendLine(string.Join('\t', UnknownMobyClusterReviewHeader()));
    foreach (UnknownMobyClusterReviewRow row in rows)
        builder.AppendLine(string.Join('\t', UnknownMobyClusterReviewValues(row).Select(EscapeTsv)));
    File.WriteAllText(path, builder.ToString());
}

bool HasUnknownMobyClusterTesterResults(string path)
{
    return CountUnknownMobyClusterTesterVerdicts(path) > 0;
}

int CountUnknownMobyClusterTesterVerdicts(string path)
{
    if (!File.Exists(path))
        return 0;

    string[] lines = File.ReadAllLines(path);
    if (lines.Length < 2)
        return 0;

    string[] headers = lines[0].Split('\t');
    Dictionary<string, int> headerIndex = headers
        .Select((name, index) => (name, index))
        .ToDictionary(item => item.name, item => item.index, StringComparer.OrdinalIgnoreCase);
    int count = 0;
    for (int i = 1; i < lines.Length; i++)
    {
        if (string.IsNullOrWhiteSpace(lines[i]))
            continue;

        string[] cells = lines[i].Split('\t');
        if (!string.IsNullOrWhiteSpace(ReadTsvCell(cells, headerIndex, "resultStatus")) ||
            !string.IsNullOrWhiteSpace(ReadTsvCell(cells, headerIndex, "clusterVerdict")) ||
            !string.IsNullOrWhiteSpace(ReadTsvCell(cells, headerIndex, "observedLabel")) ||
            !string.IsNullOrWhiteSpace(ReadTsvCell(cells, headerIndex, "evidenceNotes")))
        {
            count++;
        }
    }

    return count;
}

List<UnknownMobyClusterPromotionCandidate> ReadUnknownMobyClusterPromotionCandidates(string path)
{
    if (!File.Exists(path))
        return new List<UnknownMobyClusterPromotionCandidate>();

    string[] lines = File.ReadAllLines(path);
    if (lines.Length < 2)
        return new List<UnknownMobyClusterPromotionCandidate>();

    string[] headers = lines[0].Split('\t');
    Dictionary<string, int> headerIndex = headers
        .Select((name, index) => (name, index))
        .ToDictionary(item => item.name, item => item.index, StringComparer.OrdinalIgnoreCase);
    List<UnknownMobyClusterPromotionCandidate> candidates = new();
    for (int i = 1; i < lines.Length; i++)
    {
        if (string.IsNullOrWhiteSpace(lines[i]))
            continue;

        string[] cells = lines[i].Split('\t');
        string status = ReadTsvCell(cells, headerIndex, "resultStatus").Trim();
        if (!IsConfirmedIdentityResultStatus(status))
            continue;

        string verdict = ReadTsvCell(cells, headerIndex, "clusterVerdict").Trim();
        if (!IsPromotableClusterVerdict(verdict))
            continue;

        string label = ReadTsvCell(cells, headerIndex, "observedLabel").Trim();
        if (!IsUsableReleaseIdentityLabel(label))
            continue;

        string evidence = ReadTsvCell(cells, headerIndex, "evidenceNotes").Trim();
        string screenshotFolder = ReadTsvCell(cells, headerIndex, "screenshotFolder").Trim();
        bool hasScreenshotEvidence = !string.IsNullOrWhiteSpace(screenshotFolder) &&
            Directory.Exists(Path.Combine(workspace.RootPath, screenshotFolder)) &&
            Directory.EnumerateFiles(Path.Combine(workspace.RootPath, screenshotFolder)).Any();
        if (string.IsNullOrWhiteSpace(evidence) && !hasScreenshotEvidence)
            continue;

        candidates.Add(new UnknownMobyClusterPromotionCandidate(
            ReadTsvCell(cells, headerIndex, "priority"),
            ReadTsvCell(cells, headerIndex, "clusterKind"),
            ReadTsvCell(cells, headerIndex, "clusterKey"),
            ReadTsvCell(cells, headerIndex, "levelKey"),
            ReadTsvCell(cells, headerIndex, "levelName"),
            ReadTsvCell(cells, headerIndex, "unknownCount"),
            ReadTsvCell(cells, headerIndex, "unknownRecords"),
            verdict,
            label,
            evidence,
            screenshotFolder,
            FirstIdentityValue(ReadTsvCell(cells, headerIndex, "promoteScope"), "cluster-only"),
            status));
    }

    return candidates;
}

bool IsPromotableClusterVerdict(string verdict)
{
    return verdict.Equals("same-object", StringComparison.OrdinalIgnoreCase) ||
        verdict.Equals("variant-family", StringComparison.OrdinalIgnoreCase);
}

string[] UnknownMobyClusterReviewHeader() =>
[
    "priority",
    "clusterKind",
    "clusterKey",
    "levelKey",
    "levelName",
    "unknownCount",
    "unknownRecords",
    "currentReads",
    "knownAnchors",
    "relationshipClue",
    "proofMove",
    "batchPaths",
    "screenshotFolder",
    "resultStatus",
    "clusterVerdict",
    "observedLabel",
    "evidenceNotes",
    "promoteScope"
];

static IEnumerable<string> UnknownMobyClusterReviewValues(UnknownMobyClusterReviewRow row)
{
    yield return row.Priority.ToString(CultureInfo.InvariantCulture);
    yield return row.ClusterKind;
    yield return row.ClusterKey;
    yield return row.LevelKey;
    yield return row.LevelName;
    yield return row.UnknownCount.ToString(CultureInfo.InvariantCulture);
    yield return row.UnknownRecords;
    yield return row.CurrentReads;
    yield return row.KnownAnchors;
    yield return row.RelationshipClue;
    yield return row.ProofMove;
    yield return row.BatchPaths;
    yield return row.ScreenshotFolder;
    yield return row.ResultStatus;
    yield return row.ClusterVerdict;
    yield return row.ObservedLabel;
    yield return row.EvidenceNotes;
    yield return row.PromoteScope;
}

List<UnknownMobyClusterRow> BuildUnknownMobyClusterRows(
    IReadOnlyList<(LevelDefinition Level, Moby Moby)> allMobys,
    IReadOnlyDictionary<string, IdentityTestBatch> batchByFingerprint)
{
    List<UnknownMobyClusterRow> rows = new();
    foreach (IGrouping<string, (LevelDefinition Level, Moby Moby)> levelGroup in allMobys.GroupBy(item => item.Level.Key, StringComparer.OrdinalIgnoreCase))
    {
        LevelDefinition level = levelGroup.First().Level;
        List<Moby> levelMobys = levelGroup.Select(item => item.Moby).Where(moby => !moby.IsRemoved).OrderBy(moby => moby.TrueIndex).ToList();
        Dictionary<int, Moby> byTrueIndex = levelMobys
            .Where(moby => moby.TrueIndex >= 0)
            .GroupBy(moby => moby.TrueIndex)
            .ToDictionary(group => group.Key, group => group.First());
        List<Moby> unknowns = levelMobys.Where(IsQuestionableIdentity).ToList();

        rows.AddRange(BuildDirectLinkClusterRows(level, unknowns, byTrueIndex, batchByFingerprint));
        rows.AddRange(BuildSharedPointerClusterRows(level, unknowns, levelMobys, batchByFingerprint));
        rows.AddRange(BuildLocalRepeatedModelClusterRows(level, unknowns, levelMobys, batchByFingerprint));
    }

    rows.AddRange(BuildCrossLevelFingerprintClusterRows(allMobys, batchByFingerprint));
    return rows;
}

IEnumerable<UnknownMobyClusterRow> BuildDirectLinkClusterRows(
    LevelDefinition level,
    IReadOnlyList<Moby> unknowns,
    IReadOnlyDictionary<int, Moby> byTrueIndex,
    IReadOnlyDictionary<string, IdentityTestBatch> batchByFingerprint)
{
    return unknowns
        .SelectMany(moby => moby.Links
            .Where(MobyLinkTraversal.IsVisibleLink)
            .Select(link => new { Moby = moby, Link = link, Key = DirectLinkClusterKey(level, moby, link) }))
        .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
        .Select(group =>
        {
            List<Moby> clusterUnknowns = group.Select(item => item.Moby).DistinctBy(moby => moby.TrueIndex).OrderBy(moby => moby.TrueIndex).ToList();
            List<int> targetIndexes = group.SelectMany(item => item.Link.TrueIndexes).Distinct().OrderBy(value => value).ToList();
            List<Moby> anchors = targetIndexes
                .Where(index => byTrueIndex.TryGetValue(index, out _))
                .Select(index => byTrueIndex[index])
                .Where(moby => moby.TrueIndex != clusterUnknowns[0].TrueIndex && IsExactIdentity(moby))
                .DistinctBy(moby => moby.TrueIndex)
                .OrderBy(moby => moby.TrueIndex)
                .ToList();
            MobyLink firstLink = group.First().Link;
            List<Moby> linkedMobys = targetIndexes
                .Where(index => byTrueIndex.ContainsKey(index))
                .Select(index => byTrueIndex[index])
                .ToList();
            string relationship = $"{firstLink.DisplayName} ({firstLink.Kind}, {(firstLink.LinkedMove ? "move-linked" : "reference")}) -> {FormatMobyList(linkedMobys, 6)}";
            return NewUnknownClusterRow(
                level,
                "Direct link",
                group.Key,
                clusterUnknowns,
                anchors,
                relationship,
                "Move/test this linked set together first; if one visible object follows or one behavior breaks, record the relationship before naming individual records.",
                1,
                batchByFingerprint);
        });
}

IEnumerable<UnknownMobyClusterRow> BuildSharedPointerClusterRows(
    LevelDefinition level,
    IReadOnlyList<Moby> unknowns,
    IReadOnlyList<Moby> levelMobys,
    IReadOnlyDictionary<string, IdentityTestBatch> batchByFingerprint)
{
    return unknowns
        .Where(moby => moby.SpecialDataPointer != 0)
        .GroupBy(moby => moby.SpecialDataPointer)
        .Select(group =>
        {
            List<Moby> clusterUnknowns = group.OrderBy(moby => moby.TrueIndex).ToList();
            List<Moby> anchors = levelMobys
                .Where(moby => moby.SpecialDataPointer == group.Key && IsExactIdentity(moby))
                .OrderBy(moby => moby.TrueIndex)
                .ToList();
            if (clusterUnknowns.Count < 2 && anchors.Count == 0)
                return null;
            string relationship = $"shared special-data pointer 0x{group.Key:X8}; family size {levelMobys.Count(moby => moby.SpecialDataPointer == group.Key)}";
            string proofMove = anchors.Count > 0
                ? "Compare the unknown records against the known anchor using the same special data; confirm whether it is a variant, reward child, or support record."
                : "Move two records from this shared-data family to a clear test area and compare model, behavior, reward, and whether the family must stay together.";
            int priority = anchors.Count > 0 ? 8 : 14;
            return NewUnknownClusterRow(level, "Shared special data", $"{level.Key}|ptr|{group.Key:X8}", clusterUnknowns, anchors, relationship, proofMove, priority, batchByFingerprint);
        })
        .Where(row => row != null)
        .Select(row => row!);
}

IEnumerable<UnknownMobyClusterRow> BuildLocalRepeatedModelClusterRows(
    LevelDefinition level,
    IReadOnlyList<Moby> unknowns,
    IReadOnlyList<Moby> levelMobys,
    IReadOnlyDictionary<string, IdentityTestBatch> batchByFingerprint)
{
    return unknowns
        .Where(moby => moby.SpecialDataPointer == 0)
        .GroupBy(moby => $"{IdentityFingerprint(moby)}|cell={MobyClusterCell(moby)}", StringComparer.OrdinalIgnoreCase)
        .Where(group => group.Count() >= 2)
        .Select(group =>
        {
            List<Moby> clusterUnknowns = group.OrderBy(moby => moby.TrueIndex).ToList();
            Vector3f center = AveragePosition(clusterUnknowns);
            List<Moby> anchors = levelMobys
                .Where(IsExactIdentity)
                .Select(moby => new { Moby = moby, Distance = Math.Sqrt(DistanceSquared(moby.Position, center)) })
                .Where(item => item.Distance <= 896)
                .OrderBy(item => item.Distance)
                .ThenBy(item => item.Moby.TrueIndex)
                .Take(6)
                .Select(item => item.Moby)
                .ToList();
            string relationship = $"same fingerprint in one local area: {group.Key}";
            return NewUnknownClusterRow(level, "Local repeated model", $"{level.Key}|local|{group.Key}", clusterUnknowns, anchors, relationship, "Test one sample and one sibling side by side; if they render/behave the same, promote the local family together.", 24, batchByFingerprint);
        });
}

IEnumerable<UnknownMobyClusterRow> BuildCrossLevelFingerprintClusterRows(
    IReadOnlyList<(LevelDefinition Level, Moby Moby)> allMobys,
    IReadOnlyDictionary<string, IdentityTestBatch> batchByFingerprint)
{
    return allMobys
        .Where(item => IsQuestionableIdentity(item.Moby))
        .GroupBy(item => IdentityFingerprint(item.Moby), StringComparer.OrdinalIgnoreCase)
        .Where(group => group.Count() >= 8)
        .Select(group =>
        {
            List<(LevelDefinition Level, Moby Moby)> samples = group
                .OrderBy(item => item.Level.DisplayName)
                .ThenBy(item => item.Moby.TrueIndex)
                .Take(12)
                .ToList();
            List<Moby> sampleMobys = samples.Select(item => item.Moby).ToList();
            string levelNames = string.Join(", ", group
                .Select(item => item.Level.DisplayName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5));
            string relationship = $"same byte fingerprint across {group.Select(item => item.Level.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count()} level(s): {group.Key}";
            LevelDefinition crossLevel = new()
            {
                Key = "multiple",
                DisplayName = levelNames
            };
            return NewUnknownClusterRow(
                crossLevel,
                "Cross-level repeated fingerprint",
                $"cross|{group.Key}",
                sampleMobys,
                Array.Empty<Moby>(),
                relationship,
                $"Pick one sample from each of these levels and compare screenshots/behavior before promoting globally: {levelNames}.",
                32,
                batchByFingerprint);
        });
}

UnknownMobyClusterRow NewUnknownClusterRow(
    LevelDefinition level,
    string clusterKind,
    string clusterKey,
    IReadOnlyList<Moby> unknowns,
    IReadOnlyList<Moby> knownAnchors,
    string relationshipClue,
    string proofMove,
    int priority,
    IReadOnlyDictionary<string, IdentityTestBatch> batchByFingerprint)
{
    List<string> currentReads = unknowns
        .GroupBy(moby => moby.DisplayLabel, StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(group => group.Count())
        .ThenBy(group => group.Key)
        .Take(6)
        .Select(group => $"{group.Key} ({group.Count()})")
        .ToList();
    List<string> batchPaths = unknowns
        .Select(IdentityFingerprint)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(fingerprint => batchByFingerprint.TryGetValue(fingerprint, out IdentityTestBatch? batch) && batch.EditManifestPath != null
            && (string.Equals(level.Key, "multiple", StringComparison.OrdinalIgnoreCase) || batch.Samples.Any(sample => string.Equals(sample.LevelKey, level.Key, StringComparison.OrdinalIgnoreCase)))
            ? Path.GetRelativePath(workspace.RootPath, batch.EditManifestPath)
            : "")
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(4)
        .ToList();

    return new UnknownMobyClusterRow(
        level.Key,
        level.DisplayName,
        clusterKind,
        clusterKey,
        unknowns.Count,
        unknowns.Take(12).Select(moby => $"T{moby.TrueIndex} {moby.DisplayLabel}").ToList(),
        currentReads,
        knownAnchors.Take(8).Select(moby => $"T{moby.TrueIndex} {moby.DisplayLabel}").ToList(),
        relationshipClue,
        proofMove,
        batchPaths,
        priority);
}

string DirectLinkClusterKey(LevelDefinition level, Moby moby, MobyLink link)
{
    string linked = string.Join(",", link.TrueIndexes.OrderBy(value => value));
    return $"{level.Key}|link|{link.Key}|{moby.TrueIndex}|{linked}";
}

string MobyClusterCell(Moby moby)
{
    int x = (int)MathF.Floor(moby.Position.X / 768f);
    int y = (int)MathF.Floor(moby.Position.Y / 768f);
    int z = (int)MathF.Floor(moby.Position.Z / 768f);
    return $"{x},{y},{z}";
}

Vector3f AveragePosition(IReadOnlyList<Moby> mobys)
{
    if (mobys.Count == 0)
        return new Vector3f(0, 0, 0);
    return new Vector3f(
        mobys.Sum(moby => moby.Position.X) / mobys.Count,
        mobys.Sum(moby => moby.Position.Y) / mobys.Count,
        mobys.Sum(moby => moby.Position.Z) / mobys.Count);
}

string FormatMobyList(IReadOnlyList<Moby> mobys, int limit)
{
    if (mobys.Count == 0)
        return "none";
    string text = string.Join(", ", mobys.Take(limit).Select(moby => $"T{moby.TrueIndex} {moby.DisplayLabel}"));
    return mobys.Count <= limit ? text : $"{text}, +{mobys.Count - limit} more";
}

string EmptyAsNone(IReadOnlyList<string> values)
{
    return values.Count == 0 ? "none" : string.Join("; ", values);
}

string WriteIdentityModelFamilyReport(
    string outDir,
    IReadOnlyList<(LevelDefinition Level, Moby Moby)> allMobys,
    IReadOnlyList<IdentityTestBatch> testBatches)
{
    string markdownPath = Path.Combine(outDir, "moby-identity-model-families.md");
    string jsonPath = Path.Combine(outDir, "moby-identity-model-families.json");
    Dictionary<string, IdentityTestBatch> batchByFingerprint = testBatches
        .GroupBy(batch => batch.Fingerprint, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    Dictionary<string, List<(LevelDefinition Level, Moby Moby)>> byLevel = allMobys
        .GroupBy(item => item.Level.Key, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

    var rows = allMobys
        .Where(item => item.Moby.Type != 0x18)
        .GroupBy(item => IdentityModelFamilyKey(item.Level, item.Moby), StringComparer.OrdinalIgnoreCase)
        .Select(group =>
        {
            List<(LevelDefinition Level, Moby Moby)> familyMobys = group
                .OrderBy(item => item.Moby.TrueIndex)
                .ToList();
            List<(LevelDefinition Level, Moby Moby)> questionable = familyMobys
                .Where(item => IsQuestionableIdentity(item.Moby))
                .ToList();
            return new
            {
                Key = group.Key,
                Level = familyMobys[0].Level,
                Representative = familyMobys[0].Moby,
                All = familyMobys,
                Questionable = questionable
            };
        })
        .Where(row => row.Questionable.Count > 0)
        .OrderByDescending(row => row.Questionable.Count)
        .ThenByDescending(row => row.All.Count)
        .ThenBy(row => row.Level.DisplayName)
        .ThenBy(row => row.Key)
        .ToList();

    List<object> jsonRows = new();
    StringBuilder markdown = new();
    markdown.AppendLine("# Moby Identity Model Families");
    markdown.AppendLine();
    markdown.AppendLine(rows.Count == 0
        ? "No model-family review rows are currently needed; every cached model/package family has a concrete or useful inferred identity."
        : "This groups the remaining question-mark records by level-local model/package signature. It is designed for the precise ID pass: if one family has several reward/state variants, test one row and then confirm whether the sibling variants are the same visible model with different drops or behavior.");
    markdown.AppendLine();
    markdown.AppendLine("| Questionable | Family total | Level | Family key | Current reads | Reward/behavior variants | Exact labels in family | Samples | Batch files |");
    markdown.AppendLine("|---:|---:|---|---|---|---|---|---|---|");

    foreach (var row in rows.Take(120))
    {
        List<string> currentReads = row.Questionable
            .GroupBy(item => item.Moby.DisplayLabel, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => $"{group.Key} ({group.Count()})")
            .ToList();
        List<string> variants = row.All
            .GroupBy(item => $"f4A=0x{item.Moby.Flag4A:X2} f4B=0x{item.Moby.Flag4B:X2} b4F=0x{item.Moby.SourceByte4F:X2} state=0x{item.Moby.State:X2}", StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => $"{group.Key} ({group.Count()})")
            .Take(8)
            .ToList();
        List<string> exactLabels = row.All
            .Where(item => IsExactIdentity(item.Moby))
            .GroupBy(item => NormalizeIdentityLabel(item.Moby.DisplayLabel), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => $"{group.Key} ({group.Count()})")
            .Take(6)
            .ToList();
        List<string> sampleText = row.Questionable
            .Take(6)
            .Select(item => $"T{item.Moby.TrueIndex}")
            .ToList();
        List<string> batchFiles = row.Questionable
            .Select(item => IdentityFingerprint(item.Moby))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(fingerprint => batchByFingerprint.TryGetValue(fingerprint, out IdentityTestBatch? batch) &&
                batch.EditManifestPath != null &&
                batch.Samples.Any(sample => string.Equals(sample.LevelKey, row.Level.Key, StringComparison.OrdinalIgnoreCase))
                ? Path.GetRelativePath(workspace.RootPath, batch.EditManifestPath)
                : "")
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
        string familyKey = IdentityModelFamilyDisplay(row.Level, row.Representative);

        markdown.AppendLine($"| {row.Questionable.Count} | {row.All.Count} | {EscapeMarkdown(row.Level.DisplayName)} | `{familyKey}` | {EscapeMarkdown(string.Join("; ", currentReads))} | {EscapeMarkdown(string.Join("; ", variants))} | {EscapeMarkdown(exactLabels.Count == 0 ? "none" : string.Join("; ", exactLabels))} | {EscapeMarkdown(string.Join(", ", sampleText))} | {EscapeMarkdown(string.Join("; ", batchFiles))} |");

        jsonRows.Add(new
        {
            familyKey,
            levelKey = row.Level.Key,
            levelName = row.Level.DisplayName,
            typeHex = $"0x{row.Representative.Type:X2}",
            sourceByte36Hex = $"0x{row.Representative.SourceByte36:X2}",
            specialDataPointer = $"0x{row.Representative.SpecialDataPointer:X8}",
            questionable = row.Questionable.Count,
            total = row.All.Count,
            currentReads,
            rewardAndBehaviorVariants = variants,
            exactLabelsInFamily = exactLabels,
            samples = row.Questionable.Take(12).Select(item => new
            {
                trueIndex = item.Moby.TrueIndex,
                fingerprint = IdentityFingerprint(item.Moby),
                label = item.Moby.DisplayLabel,
                x = item.Moby.Position.X,
                y = item.Moby.Position.Y,
                z = item.Moby.Position.Z,
                nearestExactObjects = BuildNearestExactObjectSummary(item, IdentityFingerprint(item.Moby), byLevel)
            }),
            batchFiles
        });
    }

    File.WriteAllText(markdownPath, markdown.ToString());
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
    {
        generatedAt = DateTimeOffset.Now,
        note = "Questionable mobys grouped by level-local model/package signature.",
        families = jsonRows
    }, new JsonSerializerOptions { WriteIndented = true }));
    return markdownPath;
}

string IdentityModelFamilyKey(LevelDefinition level, Moby moby)
{
    string special = moby.SpecialDataPointer == 0 ? $"runtime:{moby.RuntimeAddress:X8}:t{moby.TrueIndex}" : $"special:{moby.SpecialDataPointer:X8}";
    return $"{level.Key}|type={moby.Type:X2}|b36={moby.SourceByte36:X2}|{special}";
}

string IdentityModelFamilyDisplay(LevelDefinition level, Moby moby)
{
    string special = moby.SpecialDataPointer == 0 ? $"runtime 0x{moby.RuntimeAddress:X8}/T{moby.TrueIndex}" : $"special 0x{moby.SpecialDataPointer:X8}";
    return $"{level.Key} type=0x{moby.Type:X2} b36=0x{moby.SourceByte36:X2} {special}";
}

string NormalizeIdentityLabel(string label)
{
    string result = label.Trim().ToLowerInvariant();
    int parenthetical = result.IndexOf(" (", StringComparison.Ordinal);
    if (parenthetical >= 0)
        result = result[..parenthetical];
    return result
        .Replace("safe-ground observed", "", StringComparison.OrdinalIgnoreCase)
        .Replace("live-tested", "", StringComparison.OrdinalIgnoreCase)
        .Trim();
}

string WriteIdentityMicroscopeReport(
    string outDir,
    IReadOnlyList<PrecisionIdentityGroup> rankedQuestionableGroups,
    IReadOnlyList<(LevelDefinition Level, Moby Moby)> allMobys,
    IReadOnlyList<IdentityTestBatch> testBatches)
{
    string markdownPath = Path.Combine(outDir, "moby-identity-microscope.md");
    string jsonPath = Path.Combine(outDir, "moby-identity-microscope.json");
    Dictionary<string, List<(LevelDefinition Level, Moby Moby)>> byLevel = allMobys
        .GroupBy(item => item.Level.Key, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
    Dictionary<string, IdentityTestBatch> batchByFingerprint = testBatches
        .GroupBy(batch => batch.Fingerprint, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

    List<object> jsonGroups = new();
    StringBuilder markdown = new();
    markdown.AppendLine("# Moby Identity Microscope");
    markdown.AppendLine();
    markdown.AppendLine(rankedQuestionableGroups.Count == 0
        ? "No microscope rows are currently needed; the current cache has no remaining question-mark fingerprints."
        : "Record-level view for the remaining question-mark fingerprints. Use this when a group needs precise visual testing: it shows the exact records, package pointers, current evidence, nearest confirmed objects, and the generated test batch when one exists.");

    foreach (PrecisionIdentityGroup group in rankedQuestionableGroups.Take(40))
    {
        List<(LevelDefinition Level, Moby Moby)> matches = allMobys
            .Where(item => string.Equals(IdentityFingerprint(item.Moby), group.Key, StringComparison.OrdinalIgnoreCase))
            .Where(item => IsQuestionableIdentity(item.Moby))
            .OrderBy(item => item.Level.Key)
            .ThenBy(item => item.Moby.TrueIndex)
            .ToList();
        if (matches.Count == 0)
            continue;

        string batchPath = batchByFingerprint.TryGetValue(group.Key, out IdentityTestBatch? batch) && batch.EditManifestPath != null
            ? Path.GetRelativePath(workspace.RootPath, batch.EditManifestPath)
            : "";
        markdown.AppendLine();
        markdown.AppendLine($"## {group.Key}");
        markdown.AppendLine();
        markdown.AppendLine($"- Current label: {group.LabelSummary}");
        markdown.AppendLine($"- Count: {matches.Count}");
        markdown.AppendLine($"- Lane: {IdentityTestLane(group)}");
        markdown.AppendLine(string.IsNullOrWhiteSpace(batchPath)
            ? "- Test batch: none generated for this exact fingerprint."
            : $"- Test batch: `{batchPath}`");
        markdown.AppendLine();
        markdown.AppendLine("| Record | XYZ | Special data | Current label/evidence | Nearest exact objects |");
        markdown.AppendLine("|---|---|---|---|---|");

        var recordRows = matches.Take(16).Select(item =>
        {
            string xyz = $"{item.Moby.Position.X:0.##}, {item.Moby.Position.Y:0.##}, {item.Moby.Position.Z:0.##}";
            string evidence = CompactEvidence(item.Moby);
            string neighbors = BuildNearestExactObjectSummary(item, group.Key, byLevel);
            markdown.AppendLine($"| {EscapeMarkdown(item.Level.DisplayName)} T{item.Moby.TrueIndex} | `{xyz}` | `0x{item.Moby.SpecialDataPointer:X8}` | {EscapeMarkdown(evidence)} | {EscapeMarkdown(neighbors)} |");
            return new
            {
                levelKey = item.Level.Key,
                levelName = item.Level.DisplayName,
                trueIndex = item.Moby.TrueIndex,
                x = item.Moby.Position.X,
                y = item.Moby.Position.Y,
                z = item.Moby.Position.Z,
                specialDataPointer = $"0x{item.Moby.SpecialDataPointer:X8}",
                currentLabel = item.Moby.DisplayLabel,
                item.Moby.CandidateKind,
                item.Moby.Confidence,
                item.Moby.Evidence,
                nearestExactObjects = neighbors
            };
        }).ToList();

        jsonGroups.Add(new
        {
            fingerprint = group.Key,
            currentLabel = group.LabelSummary,
            count = matches.Count,
            lane = IdentityTestLane(group),
            testBatch = batchPath,
            records = recordRows
        });
    }

    File.WriteAllText(markdownPath, markdown.ToString());
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
    {
        generatedAt = DateTimeOffset.Now,
        note = jsonGroups.Count == 0
            ? "No remaining questionable moby identities in the current cache."
            : "Record-level microscope for remaining questionable moby identities.",
        groups = jsonGroups
    }, new JsonSerializerOptions { WriteIndented = true }));
    return markdownPath;
}

string WriteRawSpecialDataSignatureReport(
    string outDir,
    IReadOnlyList<PrecisionIdentityGroup> rankedQuestionableGroups,
    IReadOnlyList<(LevelDefinition Level, Moby Moby)> allMobys)
{
    string markdownPath = Path.Combine(outDir, "moby-identity-raw-special-signatures.md");
    string jsonPath = Path.Combine(outDir, "moby-identity-raw-special-signatures.json");
    Dictionary<string, string> rawRamByLevel = FindRawRamDumps();
    Dictionary<string, byte[]> rawBytesByLevel = new(StringComparer.OrdinalIgnoreCase);
    Dictionary<string, SourceSpecialDataSignature> sourceSignatures = BuildSourceSpecialDataSignatures(allMobys);
    Dictionary<string, SourceSignatureExactEvidence> exactEvidenceBySourceSignature = BuildExactEvidenceBySourceSignature(allMobys, sourceSignatures);
    Dictionary<string, int> rankByFingerprint = rankedQuestionableGroups
        .Select((group, index) => new { group.Key, Rank = index + 1 })
        .ToDictionary(item => item.Key, item => item.Rank, StringComparer.OrdinalIgnoreCase);

    foreach ((string levelKey, string path) in rawRamByLevel)
    {
        try
        {
            rawBytesByLevel[levelKey] = File.ReadAllBytes(path);
        }
        catch
        {
            // This report is evidence-gathering only, so skip unreadable capture files.
        }
    }

    var rows = allMobys
        .Where(item => IsQuestionableIdentity(item.Moby))
        .Where(item => item.Moby.SpecialDataPointer != 0)
        .GroupBy(item => $"{item.Level.Key}|{IdentityFingerprint(item.Moby)}|0x{item.Moby.SpecialDataPointer:X8}", StringComparer.OrdinalIgnoreCase)
        .Select(group =>
        {
            List<(LevelDefinition Level, Moby Moby)> items = group
                .OrderBy(item => item.Moby.TrueIndex)
                .ToList();
            (LevelDefinition Level, Moby Moby) first = items[0];
            string fingerprint = IdentityFingerprint(first.Moby);
            string rawPath = rawRamByLevel.TryGetValue(first.Level.Key, out string? foundPath) ? foundPath : "";
            RawSpecialDataSignature? signature = null;
            if (rawBytesByLevel.TryGetValue(first.Level.Key, out byte[]? ramBytes))
                signature = TryBuildRawSpecialDataSignature(first.Moby.SpecialDataPointer, ramBytes);
            SourceSpecialDataSignature? sourceSignature = sourceSignatures.TryGetValue(SourceSignatureKey(first.Level.Key, first.Moby.TrueIndex), out SourceSpecialDataSignature? foundSourceSignature)
                ? foundSourceSignature
                : null;

            List<string> samePointerExactLabels = allMobys
                .Where(item => string.Equals(item.Level.Key, first.Level.Key, StringComparison.OrdinalIgnoreCase))
                .Where(item => item.Moby.SpecialDataPointer == first.Moby.SpecialDataPointer)
                .Where(item => IsExactIdentity(item.Moby))
                .GroupBy(item => NormalizeIdentityLabel(item.Moby.DisplayLabel), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(labelGroup => labelGroup.Count())
                .ThenBy(labelGroup => labelGroup.Key)
                .Take(6)
                .Select(labelGroup => $"{labelGroup.Key} ({labelGroup.Count()})")
                .ToList();

            SourceSignatureExactEvidence sameSourceSignatureExactEvidence = sourceSignature != null &&
                exactEvidenceBySourceSignature.TryGetValue(SourceSignatureEvidenceKey(first.Level.Key, sourceSignature), out SourceSignatureExactEvidence? foundSourceEvidence)
                    ? foundSourceEvidence
                    : SourceSignatureExactEvidence.Empty;

            int rank = rankByFingerprint.TryGetValue(fingerprint, out int foundRank)
                ? foundRank
                : int.MaxValue;

            return new RawSpecialDataRow(
                SortRank: rank,
                Count: items.Count,
                LevelKey: first.Level.Key,
                LevelName: first.Level.DisplayName,
                Fingerprint: fingerprint,
                CurrentLabel: first.Moby.DisplayLabel,
                SpecialDataPointer: first.Moby.SpecialDataPointer,
                RawRamPath: rawPath,
                Signature: signature,
                SourceSignature: sourceSignature,
                SamePointerExactLabels: samePointerExactLabels,
                SameSourceSignatureExactLabels: sameSourceSignatureExactEvidence.Labels,
                SameSourceSignatureExactSamples: sameSourceSignatureExactEvidence.Samples,
                Samples: items.Take(6).Select(item => $"T{item.Moby.TrueIndex}").ToList());
        })
        .OrderBy(row => row.SortRank)
        .ThenByDescending(row => row.Signature != null)
        .ThenByDescending(row => row.Count)
        .ThenBy(row => row.LevelName)
        .ThenBy(row => row.Fingerprint)
        .ToList();

    int rowsWithRaw = rows.Count(row => row.Signature != null);
    int rowsWithSource = rows.Count(row => row.SourceSignature != null);
    int captureNeeded = rows.Count(row => string.IsNullOrWhiteSpace(row.RawRamPath));
    var repeatedRawSignatures = rows
        .Where(row => row.Signature != null)
        .GroupBy(row => $"{row.LevelKey}|{row.Signature!.Hash}", StringComparer.OrdinalIgnoreCase)
        .Where(group => group.Count() > 1)
        .OrderByDescending(group => group.Sum(row => row.Count))
        .Take(20)
        .ToList();

    StringBuilder markdown = new();
    markdown.AppendLine("# Moby Raw Special-Data Signatures");
    markdown.AppendLine();
    markdown.AppendLine(rows.Count == 0
        ? "No raw special-data signature rows are currently needed; the current cache has no remaining question-mark moby packages."
        : "This report reads any copied Windows RAM snapshots and fingerprints the bytes behind each remaining question-mark moby package. It is evidence for precise naming, not an automatic rename pass.");
    markdown.AppendLine();
    markdown.AppendLine($"- Raw RAM snapshots found: {rawRamByLevel.Count}");
    markdown.AppendLine($"- Question-mark package rows with readable bytes: {rowsWithRaw}/{rows.Count}");
    markdown.AppendLine($"- Question-mark package rows with source-disc special bytes: {rowsWithSource}/{rows.Count}");
    markdown.AppendLine($"- Rows needing a fresh level capture before byte-level proof: {captureNeeded}");
    markdown.AppendLine();
    markdown.AppendLine("## Highest-Value Rows");
    markdown.AppendLine();
    markdown.AppendLine("| ? | Level | Fingerprint | Pointer | RAM signature | Source signature | First source bytes | Pointer refs | Exact labels sharing pointer | Exact labels sharing source signature | Exact source samples | Samples | Capture status |");
    markdown.AppendLine("|---:|---|---|---|---|---|---|---|---|---|---|---|---|");
    foreach (RawSpecialDataRow row in rows.Take(120))
    {
        string pointer = $"0x{row.SpecialDataPointer:X8}";
        string rawStatus = row.Signature == null
            ? string.IsNullOrWhiteSpace(row.RawRamPath) ? "need RAM capture" : "pointer not readable"
            : Path.GetFileName(row.RawRamPath);
        string refs = row.Signature == null ? "" : string.Join(", ", row.Signature.PointerRefs);
        string sourceHash = row.SourceSignature == null ? "" : $"{row.SourceSignature.Hash} @{row.SourceSignature.OffsetHex}";
        markdown.AppendLine($"| {row.Count} | {EscapeMarkdown(row.LevelName)} | `{row.Fingerprint}` | `{pointer}` | {EscapeMarkdown(row.Signature?.Hash ?? "")} | {EscapeMarkdown(sourceHash)} | `{row.SourceSignature?.PrefixHex ?? ""}` | {EscapeMarkdown(refs)} | {EscapeMarkdown(row.SamePointerExactLabels.Count == 0 ? "" : string.Join("; ", row.SamePointerExactLabels))} | {EscapeMarkdown(row.SameSourceSignatureExactLabels.Count == 0 ? "" : string.Join("; ", row.SameSourceSignatureExactLabels))} | {EscapeMarkdown(row.SameSourceSignatureExactSamples.Count == 0 ? "" : string.Join(", ", row.SameSourceSignatureExactSamples))} | {EscapeMarkdown(string.Join(", ", row.Samples))} | {EscapeMarkdown(rawStatus)} |");
    }

    markdown.AppendLine();
    markdown.AppendLine("## Repeated Source Signatures");
    markdown.AppendLine();
    markdown.AppendLine("Rows here share identical source-disc special-data bytes. This works even when no RAM capture is available for that level.");
    markdown.AppendLine();
    markdown.AppendLine("| Source signature | Rows | Records | Members |");
    markdown.AppendLine("|---|---:|---:|---|");
    foreach (IGrouping<string, RawSpecialDataRow> group in rows
        .Where(row => row.SourceSignature != null)
        .GroupBy(row => $"{row.LevelKey}|{row.SourceSignature!.Hash}", StringComparer.OrdinalIgnoreCase)
        .Where(group => group.Count() > 1)
        .OrderByDescending(group => group.Sum(row => row.Count))
        .Take(24))
    {
        SourceSpecialDataSignature? signature = group.First().SourceSignature;
        string hash = signature == null ? "" : $"{signature.Hash} @{signature.OffsetHex}";
        string members = string.Join("; ", group.Select(row => $"{row.LevelName} {row.Fingerprint} {row.CurrentLabel} ({row.Count})"));
        markdown.AppendLine($"| {EscapeMarkdown(hash)} | {group.Count()} | {group.Sum(row => row.Count)} | {EscapeMarkdown(members)} |");
    }

    markdown.AppendLine();
    markdown.AppendLine("## Repeated RAM Signatures");
    markdown.AppendLine();
    markdown.AppendLine("Rows here share identical first-128-byte package data inside the same level capture. If one row is visually confirmed, these are the first siblings to inspect before promoting related variants.");
    markdown.AppendLine();
    markdown.AppendLine("| Raw signature | Rows | Records | Members |");
    markdown.AppendLine("|---|---:|---:|---|");
    foreach (IGrouping<string, RawSpecialDataRow> group in repeatedRawSignatures)
    {
        string hash = group.First().Signature?.Hash ?? "";
        string members = string.Join("; ", group.Select(row => $"{row.LevelName} {row.Fingerprint} {row.CurrentLabel} ({row.Count})"));
        markdown.AppendLine($"| {EscapeMarkdown(hash)} | {group.Count()} | {group.Sum(row => row.Count)} | {EscapeMarkdown(members)} |");
    }

    markdown.AppendLine();
    markdown.AppendLine("## Missing Captures");
    markdown.AppendLine();
    markdown.AppendLine("These levels have question-mark packages but no `*-before-clean.bin` RAM snapshot available to the Mac editor yet. Capturing these will let this report read the model/control package bytes directly.");
    markdown.AppendLine();
    foreach (var levelGroup in rows
        .Where(row => string.IsNullOrWhiteSpace(row.RawRamPath))
        .GroupBy(row => row.LevelName)
        .OrderByDescending(group => group.Sum(row => row.Count))
        .ThenBy(group => group.Key)
        .Take(20))
    {
        markdown.AppendLine($"- {levelGroup.Key}: {levelGroup.Sum(row => row.Count)} question-mark record(s) across {levelGroup.Count()} package row(s)");
    }

    File.WriteAllText(markdownPath, markdown.ToString());
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
    {
        generatedAt = DateTimeOffset.Now,
        note = rows.Count == 0
            ? "No remaining questionable moby packages in the current cache."
            : "Raw special-data byte signatures for remaining questionable moby packages. Empty signatures mean the level RAM capture is missing or the pointer is outside the available RAM image.",
        rawRamSnapshots = rawRamByLevel
            .OrderBy(pair => pair.Key)
            .Select(pair => new { levelKey = pair.Key, path = pair.Value }),
        summary = new
        {
            packageRows = rows.Count,
            packageRowsWithReadableRawBytes = rowsWithRaw,
            packageRowsWithReadableSourceBytes = rowsWithSource,
            packageRowsNeedingFreshCapture = captureNeeded
        },
        rows = rows.Select(row => new
        {
            row.Count,
            row.LevelKey,
            row.LevelName,
            fingerprint = row.Fingerprint,
            currentLabel = row.CurrentLabel,
            specialDataPointer = $"0x{row.SpecialDataPointer:X8}",
            rawRamPath = row.RawRamPath,
            signature = row.Signature,
            sourceSignature = row.SourceSignature,
            samePointerExactLabels = row.SamePointerExactLabels,
            sameSourceSignatureExactLabels = row.SameSourceSignatureExactLabels,
            sameSourceSignatureExactSamples = row.SameSourceSignatureExactSamples,
            samples = row.Samples
        })
    }, new JsonSerializerOptions { WriteIndented = true }));

    return markdownPath;
}

Dictionary<string, string> FindRawRamDumps()
{
    Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
    string codexRoot = new DirectoryInfo(workspace.RootPath).Parent?.FullName ?? workspace.RootPath;
    List<string> searchRoots = [workspace.RootPath];
    if (!string.Equals(codexRoot, workspace.RootPath, StringComparison.OrdinalIgnoreCase))
        searchRoots.Add(codexRoot);

    foreach (string root in searchRoots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
    {
        foreach (string path in Directory.EnumerateFiles(root, "*-before-clean.bin", SearchOption.AllDirectories))
            AddRawRamCandidate(path);
        foreach (string path in Directory.EnumerateFiles(root, "*-before-gem-clean.bin", SearchOption.AllDirectories))
            AddRawRamCandidate(path);
    }

    return result;

    void AddRawRamCandidate(string path)
    {
        string fileName = Path.GetFileName(path);
        string levelKey = fileName;
        foreach (string suffix in new[] { "-before-clean.bin", "-before-gem-clean.bin" })
        {
            if (levelKey.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                levelKey = levelKey[..^suffix.Length];
                break;
            }
        }

        if (levelKey.Contains("spring", StringComparison.OrdinalIgnoreCase) ||
            levelKey.Contains("validation", StringComparison.OrdinalIgnoreCase))
            return;

        if (!catalog.Levels.Any(level => string.Equals(level.Key, levelKey, StringComparison.OrdinalIgnoreCase)))
            return;

        if (!result.TryGetValue(levelKey, out string? current) || RawRamCandidateScore(path) < RawRamCandidateScore(current))
            result[levelKey] = path;
    }
}

int RawRamCandidateScore(string path)
{
    string fileName = Path.GetFileName(path);
    int score = path.Length;
    if (fileName.EndsWith("-before-clean.bin", StringComparison.OrdinalIgnoreCase))
        score -= 1000;
    if (path.StartsWith(workspace.RootPath, StringComparison.OrdinalIgnoreCase))
        score -= 500;
    return score;
}

Dictionary<string, SourceSpecialDataSignature> BuildSourceSpecialDataSignatures(IReadOnlyList<(LevelDefinition Level, Moby Moby)> allMobys)
{
    Dictionary<string, SourceSpecialDataSignature> result = new(StringComparer.OrdinalIgnoreCase);
    if (!File.Exists(sourceImage))
        return result;

    SourceDiscLayout layout;
    try
    {
        layout = DetectSourceDiscLayout(sourceImage);
    }
    catch
    {
        return result;
    }

    using FileStream stream = File.OpenRead(sourceImage);
    foreach (LevelDefinition level in catalog.Levels.Where(level => level.HasSourceTable))
    {
        List<Moby> levelMobys = allMobys
            .Where(item => string.Equals(item.Level.Key, level.Key, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Moby)
            .Where(moby => moby.TrueIndex >= 0 && moby.TrueIndex < level.SourceRecordCount)
            .ToList();
        if (levelMobys.Count == 0)
            continue;

        long tableWadOffset;
        long tableRelativeOffset;
        try
        {
            tableWadOffset = ParseFlexibleLong(level.SourceTableWadOffset);
            tableRelativeOffset = ParseFlexibleLong(level.SourceTableRelativeOffset);
        }
        catch
        {
            continue;
        }

        if (tableRelativeOffset <= 0)
            continue;

        Dictionary<int, byte[]> records = new();
        foreach (int trueIndex in levelMobys.Select(moby => moby.TrueIndex).Distinct())
        {
            try
            {
                records[trueIndex] = ReadSourceWadBytes(stream, layout, tableWadOffset + (trueIndex * MobyLoader.RuntimeRecordStride), MobyLoader.RuntimeRecordStride);
            }
            catch
            {
                // Keep mining other levels even if one table entry is unreadable.
            }
        }

        Dictionary<uint, int> lengths = BuildSourceSpecialLengths(records.Values, tableRelativeOffset);
        long wadBaseOffset = tableWadOffset - tableRelativeOffset;
        foreach ((int trueIndex, byte[] record) in records)
        {
            uint sourceOffset = BitConverter.ToUInt32(record, 0);
            if (!IsSourceSpecialDataOffset(tableRelativeOffset, sourceOffset))
                continue;

            int length = lengths.TryGetValue(sourceOffset, out int foundLength)
                ? foundLength
                : DefaultSourceSpecialLength(record[0x50]);
            length = Math.Clamp(length, 1, 128);

            try
            {
                byte[] bytes = ReadSourceWadBytes(stream, layout, wadBaseOffset + sourceOffset, length);
                byte[] hashBytes = SHA256.HashData(bytes);
                result[SourceSignatureKey(level.Key, trueIndex)] = new SourceSpecialDataSignature(
                    OffsetHex: $"0x{sourceOffset:X}",
                    ByteLength: bytes.Length,
                    Hash: Convert.ToHexString(hashBytes.AsSpan(0, 8)).ToLowerInvariant(),
                    PrefixHex: FormatHex(bytes.AsSpan(0, Math.Min(32, bytes.Length))));
            }
            catch
            {
                // Source signatures are advisory evidence; ignore unreadable individual blocks.
            }
        }
    }

    return result;
}

Dictionary<string, SourceSignatureExactEvidence> BuildExactEvidenceBySourceSignature(
    IReadOnlyList<(LevelDefinition Level, Moby Moby)> allMobys,
    IReadOnlyDictionary<string, SourceSpecialDataSignature> sourceSignatures)
{
    return allMobys
        .Where(item => IsExactIdentity(item.Moby))
        .Select(item =>
        {
            SourceSpecialDataSignature? signature = sourceSignatures.TryGetValue(SourceSignatureKey(item.Level.Key, item.Moby.TrueIndex), out SourceSpecialDataSignature? found)
                ? found
                : null;
            return new
            {
                item.Level,
                item.Moby,
                Signature = signature,
                Label = NormalizeIdentityLabel(item.Moby.DisplayLabel)
            };
        })
        .Where(item => item.Signature != null && !string.IsNullOrWhiteSpace(item.Label))
        .GroupBy(item => SourceSignatureEvidenceKey(item.Level.Key, item.Signature!), StringComparer.OrdinalIgnoreCase)
        .ToDictionary(
            group => group.Key,
            group =>
            {
                List<string> labels = group
                    .GroupBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(labelGroup => labelGroup.Count())
                    .ThenBy(labelGroup => labelGroup.Key)
                    .Take(8)
                    .Select(labelGroup => $"{labelGroup.Key} ({labelGroup.Count()})")
                    .ToList();
                List<string> samples = group
                    .OrderBy(item => item.Moby.TrueIndex)
                    .Take(12)
                    .Select(item => $"{item.Level.DisplayName}:T{item.Moby.TrueIndex} {item.Moby.DisplayLabel}")
                    .ToList();
                return new SourceSignatureExactEvidence(labels, samples);
            },
            StringComparer.OrdinalIgnoreCase);
}

string SourceSignatureEvidenceKey(string levelKey, SourceSpecialDataSignature signature)
{
    return $"{levelKey}|{signature.Hash}|{signature.ByteLength}";
}

Dictionary<uint, int> BuildSourceSpecialLengths(IEnumerable<byte[]> sourceRecords, long tableRelativeOffset)
{
    List<(uint Offset, int Type)> unique = sourceRecords
        .Select(record => (Offset: BitConverter.ToUInt32(record, 0), Type: (int)record[0x50]))
        .Where(item => IsSourceSpecialDataOffset(tableRelativeOffset, item.Offset))
        .GroupBy(item => item.Offset)
        .Select(group => group.First())
        .OrderBy(item => item.Offset)
        .ToList();

    Dictionary<uint, int> lengths = new();
    for (int i = 0; i < unique.Count; i++)
    {
        int length = 0;
        if (i + 1 < unique.Count)
        {
            uint delta = unique[i + 1].Offset - unique[i].Offset;
            if (delta > 0 && delta <= 0x400)
                length = (int)delta;
        }

        if (length <= 0)
            length = DefaultSourceSpecialLength(unique[i].Type);
        lengths[unique[i].Offset] = length;
    }

    return lengths;
}

bool IsSourceSpecialDataOffset(long tableRelativeOffset, uint offset)
{
    return tableRelativeOffset > 0 && offset > 0 && offset < (uint)tableRelativeOffset;
}

int DefaultSourceSpecialLength(int type) => type == 0x00 ? 0x28 : 0x18;

string SourceSignatureKey(string levelKey, int trueIndex) => $"{levelKey}:T{trueIndex}";

SourceDiscLayout DetectSourceDiscLayout(string imagePath)
{
    using FileStream stream = File.OpenRead(imagePath);
    foreach ((int sectorSize, int userOffset) in new[] { (2048, 0), (2352, 24), (2336, 8) })
    {
        long offset = (16L * sectorSize) + userOffset;
        if (stream.Length < offset + 2048)
            continue;

        byte[] sector = new byte[2048];
        stream.Position = offset;
        if (stream.Read(sector, 0, sector.Length) != sector.Length)
            continue;

        if (sector[0] == 1 &&
            sector[1] == (byte)'C' &&
            sector[2] == (byte)'D' &&
            sector[3] == (byte)'0' &&
            sector[4] == (byte)'0' &&
            sector[5] == (byte)'1')
            return new SourceDiscLayout(sectorSize, userOffset, GetUInt32(sector, 158), GetUInt32(sector, 166));
    }

    throw new InvalidOperationException("Could not detect source disc layout.");
}

byte[] ReadLogicalWadForSmoke(string imagePath)
{
    SourceDiscLayout layout = DetectSourceDiscLayout(imagePath);
    using FileStream stream = File.OpenRead(imagePath);
    const int wadLba = 37;
    int wadSize = (int)Math.Max(0, Math.Min(110260224L, 2048L * Math.Max(0, (stream.Length / layout.SectorSize) - wadLba)));
    byte[] wad = new byte[wadSize];
    int remaining = wad.Length;
    int written = 0;
    int lba = wadLba;
    while (remaining > 0)
    {
        int toRead = Math.Min(2048, remaining);
        stream.Position = ((long)lba * layout.SectorSize) + layout.UserOffset;
        int read = stream.Read(wad, written, toRead);
        if (read != toRead)
            throw new EndOfStreamException("Could not read logical WAD bytes.");

        written += read;
        remaining -= read;
        lba++;
    }

    return wad;
}

byte[] ReadSourceWadBytes(FileStream stream, SourceDiscLayout layout, long wadOffset, int length)
{
    const int wadLba = 37;
    byte[] result = new byte[length];
    int remaining = length;
    int written = 0;
    long absolute = wadOffset;
    while (remaining > 0)
    {
        int sectorOffset = (int)(absolute % 2048);
        int toRead = Math.Min(2048 - sectorOffset, remaining);
        long sector = wadLba + (long)Math.Floor(absolute / 2048d);
        stream.Position = (sector * layout.SectorSize) + layout.UserOffset + sectorOffset;
        int read = stream.Read(result, written, toRead);
        if (read != toRead)
            throw new EndOfStreamException("Could not read WAD bytes.");

        written += toRead;
        remaining -= toRead;
        absolute += toRead;
    }

    return result;
}

SourceDiscFileRecord FindSourceExecutable(FileStream stream, SourceDiscLayout layout)
{
    byte[] data = new byte[layout.RootLength];
    int remaining = data.Length;
    int written = 0;
    int sector = layout.RootExtent;
    while (remaining > 0)
    {
        stream.Position = ((long)sector * layout.SectorSize) + layout.UserOffset;
        int toRead = Math.Min(2048, remaining);
        int read = stream.Read(data, written, toRead);
        if (read != toRead)
            throw new EndOfStreamException("Could not read source disc root directory.");
        written += toRead;
        remaining -= toRead;
        sector++;
    }

    int offset = 0;
    while (offset < data.Length)
    {
        int recordLength = data[offset];
        if (recordLength == 0)
        {
            offset = ((offset / 2048) + 1) * 2048;
            continue;
        }

        if (offset + recordLength > data.Length || recordLength < 34)
            break;

        int nameLength = data[offset + 32];
        string name = Encoding.ASCII.GetString(data, offset + 33, nameLength).Replace(";1", "", StringComparison.OrdinalIgnoreCase);
        string upper = name.ToUpperInvariant();
        if (upper.StartsWith("SCUS", StringComparison.Ordinal) ||
            upper.StartsWith("SCES", StringComparison.Ordinal) ||
            upper.StartsWith("SCPS", StringComparison.Ordinal) ||
            upper.StartsWith("SLUS", StringComparison.Ordinal) ||
            upper.StartsWith("SLES", StringComparison.Ordinal) ||
            upper.StartsWith("SLPS", StringComparison.Ordinal))
        {
            return new SourceDiscFileRecord(name, GetUInt32(data, offset + 2), GetUInt32(data, offset + 10));
        }

        offset += recordLength;
    }

    throw new InvalidOperationException("Could not find source executable file.");
}

byte[] ReadSourceFileBytes(FileStream stream, SourceDiscLayout layout, int fileLba, long fileOffset, int length)
{
    byte[] result = new byte[length];
    int remaining = length;
    int written = 0;
    long absolute = fileOffset;
    while (remaining > 0)
    {
        int sectorOffset = (int)(absolute % 2048);
        long sector = fileLba + (long)Math.Floor(absolute / 2048d);
        int toRead = Math.Min(2048 - sectorOffset, remaining);
        stream.Position = (sector * layout.SectorSize) + layout.UserOffset + sectorOffset;
        int read = stream.Read(result, written, toRead);
        if (read != toRead)
            throw new EndOfStreamException("Could not read source file bytes.");

        written += toRead;
        remaining -= toRead;
        absolute += toRead;
    }

    return result;
}

int GetUInt32(byte[] bytes, int offset) => (int)BitConverter.ToUInt32(bytes, offset);

long ParseFlexibleLong(string text)
{
    string trimmed = (text ?? "").Trim();
    if (string.IsNullOrWhiteSpace(trimmed))
        return 0;
    return trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
        ? long.Parse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
        : long.Parse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture);
}

RawSpecialDataSignature? TryBuildRawSpecialDataSignature(uint specialDataPointer, byte[] ram)
{
    bool fullRuntimeAddress = specialDataPointer >= 0x80000000u && specialDataPointer < 0x80200000u;
    bool rawRamOffset = specialDataPointer < 0x00200000u;
    if (!fullRuntimeAddress && !rawRamOffset)
        return null;

    int offset = fullRuntimeAddress
        ? (int)(specialDataPointer & 0x001FFFFFu)
        : (int)specialDataPointer;
    if (offset < 0 || offset >= ram.Length)
        return null;

    int length = Math.Min(128, ram.Length - offset);
    if (length <= 0)
        return null;

    byte[] bytes = ram.AsSpan(offset, length).ToArray();
    byte[] hashBytes = SHA256.HashData(bytes);
    string hash = Convert.ToHexString(hashBytes.AsSpan(0, 8)).ToLowerInvariant();
    string prefixHex = FormatHex(bytes.AsSpan(0, Math.Min(32, bytes.Length)));
    List<string> pointerRefs = new();
    for (int i = 0; i + 4 <= bytes.Length; i += 4)
    {
        uint word = BitConverter.ToUInt32(bytes, i);
        int refOffset = (int)(word & 0x001FFFFFu);
        if (word >= 0x80000000u && word < 0x80200000u && refOffset >= 0 && refOffset < ram.Length)
            pointerRefs.Add($"+0x{i:X2}->0x{word:X8}");
        if (pointerRefs.Count >= 8)
            break;
    }

    return new RawSpecialDataSignature(
        OffsetHex: $"0x{offset:X}",
        ByteLength: length,
        Hash: hash,
        PrefixHex: prefixHex,
        PointerRefs: pointerRefs);
}

string FormatHex(ReadOnlySpan<byte> bytes)
{
    return string.Join(" ", bytes.ToArray().Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
}

string FormatHexValue(uint value, int minDigits)
{
    int digits = Math.Max(1, minDigits);
    return $"0x{value.ToString($"X{digits}", CultureInfo.InvariantCulture)}";
}

string CompactEvidence(Moby moby)
{
    List<string> parts = new();
    if (!string.IsNullOrWhiteSpace(moby.DisplayLabel))
        parts.Add(moby.DisplayLabel);
    if (!string.IsNullOrWhiteSpace(moby.CandidateKind))
        parts.Add(moby.CandidateKind);
    if (!string.IsNullOrWhiteSpace(moby.Confidence))
        parts.Add(moby.Confidence);
    if (!string.IsNullOrWhiteSpace(moby.Evidence))
        parts.Add(moby.Evidence);
    string text = string.Join(" / ", parts);
    return text.Length <= 160 ? text : text[..157] + "...";
}

string BuildNearestExactObjectSummary(
    (LevelDefinition Level, Moby Moby) sample,
    string fingerprint,
    IReadOnlyDictionary<string, List<(LevelDefinition Level, Moby Moby)>> byLevel)
{
    if (!byLevel.TryGetValue(sample.Level.Key, out List<(LevelDefinition Level, Moby Moby)>? levelMobys))
        return "none nearby";

    List<string> nearest = levelMobys
        .Where(item => item.Moby.TrueIndex != sample.Moby.TrueIndex)
        .Where(item => !string.Equals(IdentityFingerprint(item.Moby), fingerprint, StringComparison.OrdinalIgnoreCase))
        .Where(item => IsExactIdentity(item.Moby))
        .Select(item => new
        {
            item.Moby,
            Distance = Math.Sqrt(DistanceSquared(item.Moby.Position, sample.Moby.Position))
        })
        .Where(item => item.Distance <= 896)
        .OrderBy(item => item.Distance)
        .ThenBy(item => item.Moby.TrueIndex)
        .Take(4)
        .Select(item => $"T{item.Moby.TrueIndex} {item.Moby.DisplayLabel} @{item.Distance:0}")
        .ToList();
    return nearest.Count == 0 ? "none within 896" : string.Join(", ", nearest);
}

string BuildUnknownDirectLinkSummary(
    (LevelDefinition Level, Moby Moby) sample,
    IReadOnlyDictionary<string, List<(LevelDefinition Level, Moby Moby)>> byLevel)
{
    if (sample.Moby.Links.Count == 0)
        return "none";
    if (!byLevel.TryGetValue(sample.Level.Key, out List<(LevelDefinition Level, Moby Moby)>? levelMobys))
        return $"unresolved link metadata ({sample.Moby.Links.Count} link group(s))";

    Dictionary<int, Moby> mobyByTrueIndex = levelMobys
        .Select(item => item.Moby)
        .Where(moby => moby.TrueIndex >= 0)
        .GroupBy(moby => moby.TrueIndex)
        .ToDictionary(group => group.Key, group => group.First());
    List<string> summaries = sample.Moby.Links
        .Where(MobyLinkTraversal.IsVisibleLink)
        .Select(link =>
        {
            List<string> targets = link.TrueIndexes
                .Where(trueIndex => trueIndex != sample.Moby.TrueIndex)
                .Distinct()
                .Take(4)
                .Select(trueIndex => mobyByTrueIndex.TryGetValue(trueIndex, out Moby? linked)
                    ? $"T{trueIndex} {linked.DisplayLabel}"
                    : $"T{trueIndex}")
                .ToList();
            if (targets.Count == 0)
                return "";
            string move = link.LinkedMove ? "move-linked" : "reference";
            return $"{link.DisplayName} ({link.Kind}, {move}) -> {string.Join(", ", targets)}";
        })
        .Where(text => !string.IsNullOrWhiteSpace(text))
        .Take(3)
        .ToList();

    if (summaries.Count > 0)
        return string.Join("; ", summaries);
    return $"hidden/broad relationship scaffold only ({sample.Moby.Links.Count} link group(s))";
}

string BuildUnknownPointerFamilySummary(
    (LevelDefinition Level, Moby Moby) sample,
    IReadOnlyDictionary<string, List<(LevelDefinition Level, Moby Moby)>> byLevel)
{
    if (sample.Moby.SpecialDataPointer == 0)
        return "none";
    if (!byLevel.TryGetValue(sample.Level.Key, out List<(LevelDefinition Level, Moby Moby)>? levelMobys))
        return $"unique pointer 0x{sample.Moby.SpecialDataPointer:X8}";

    List<Moby> siblings = levelMobys
        .Select(item => item.Moby)
        .Where(moby => moby.TrueIndex != sample.Moby.TrueIndex)
        .Where(moby => moby.SpecialDataPointer == sample.Moby.SpecialDataPointer)
        .OrderBy(moby => moby.TrueIndex)
        .ToList();
    if (siblings.Count == 0)
        return $"unique pointer 0x{sample.Moby.SpecialDataPointer:X8}";

    List<string> known = siblings
        .Where(IsExactIdentity)
        .Take(4)
        .Select(moby => $"T{moby.TrueIndex} {moby.DisplayLabel}")
        .ToList();
    List<string> unknown = siblings
        .Where(IsQuestionableIdentity)
        .Take(4)
        .Select(moby => $"T{moby.TrueIndex} {moby.DisplayLabel}")
        .ToList();
    List<string> parts = new() { $"shared pointer 0x{sample.Moby.SpecialDataPointer:X8} ({siblings.Count + 1})" };
    if (known.Count > 0)
        parts.Add($"known: {string.Join(", ", known)}");
    if (unknown.Count > 0)
        parts.Add($"unknown: {string.Join(", ", unknown)}");
    return string.Join("; ", parts);
}

string BuildNearbyQuestionableSummary(
    (LevelDefinition Level, Moby Moby) sample,
    string fingerprint,
    IReadOnlyDictionary<string, List<(LevelDefinition Level, Moby Moby)>> byLevel)
{
    if (!byLevel.TryGetValue(sample.Level.Key, out List<(LevelDefinition Level, Moby Moby)>? levelMobys))
        return "none nearby";

    List<string> nearest = levelMobys
        .Where(item => item.Moby.TrueIndex != sample.Moby.TrueIndex)
        .Where(item => !string.Equals(IdentityFingerprint(item.Moby), fingerprint, StringComparison.OrdinalIgnoreCase))
        .Where(item => IsQuestionableIdentity(item.Moby))
        .Select(item => new
        {
            item.Moby,
            Distance = Math.Sqrt(DistanceSquared(item.Moby.Position, sample.Moby.Position))
        })
        .Where(item => item.Distance <= 640)
        .OrderBy(item => item.Distance)
        .ThenBy(item => item.Moby.TrueIndex)
        .Take(4)
        .Select(item => $"T{item.Moby.TrueIndex} {item.Moby.DisplayLabel} @{item.Distance:0}")
        .ToList();
    return nearest.Count == 0 ? "none within 640" : string.Join(", ", nearest);
}

string BuildUnknownMobyProofHint(
    string lane,
    string directLinks,
    string pointerFamily,
    string nearbyKnown,
    string nearbyUnknown,
    string fallbackProofStep)
{
    string text = $"{lane} {directLinks} {pointerFamily} {nearbyKnown} {nearbyUnknown}".ToLowerInvariant();
    if (text.Contains("dragon") || text.Contains("pedestal"))
        return "Test as a dragon/rescue cluster: move only the linked group and verify rescue/camera/pedestal behavior together.";
    if (text.Contains("portal") || text.Contains("return-home") || text.Contains("pad"))
        return "Test as a portal cluster: keep pad, arch, collision, and control markers together and verify destination/name behavior.";
    if (text.Contains("chest") || text.Contains("container") || text.Contains("contained"))
        return "Test as a container/content pair: confirm shell, hit response, contained reward, and whether linked records must move together.";
    if (text.Contains("flight") || text.Contains("gate") || text.Contains("route"))
        return "Test in the flight/special context; verify route, timer, target, and scoring behavior before naming.";
    if (text.Contains("control") || text.Contains("helper") || text.Contains("nonvisual"))
        return "Do not name from the marker alone. Move or isolate the linked visible cluster and record what breaks or follows.";
    if (!directLinks.StartsWith("none", StringComparison.OrdinalIgnoreCase) &&
        !directLinks.StartsWith("hidden", StringComparison.OrdinalIgnoreCase))
        return "Start with the linked cluster; prove whether this record controls, decorates, contains, or triggers the visible neighbor.";
    if (!pointerFamily.StartsWith("none", StringComparison.OrdinalIgnoreCase) &&
        !pointerFamily.StartsWith("unique", StringComparison.OrdinalIgnoreCase))
        return "Compare all records sharing this special-data pointer before promoting a label; it may be a model/reward variant family.";
    return fallbackProofStep;
}

int UnknownMobyTriagePriority(
    Moby moby,
    string lane,
    string directLinks,
    string pointerFamily,
    string nearbyKnown,
    string nearbyUnknown)
{
    int priority = 50;
    if (!directLinks.StartsWith("none", StringComparison.OrdinalIgnoreCase) &&
        !directLinks.StartsWith("hidden", StringComparison.OrdinalIgnoreCase))
        priority -= 25;
    if (!pointerFamily.StartsWith("none", StringComparison.OrdinalIgnoreCase) &&
        !pointerFamily.StartsWith("unique", StringComparison.OrdinalIgnoreCase))
        priority -= 15;
    if (lane.Contains("control", StringComparison.OrdinalIgnoreCase) || lane.Contains("helper", StringComparison.OrdinalIgnoreCase))
        priority -= 10;
    if (lane.Contains("actor", StringComparison.OrdinalIgnoreCase) || lane.Contains("interactive", StringComparison.OrdinalIgnoreCase))
        priority -= 8;
    if (nearbyKnown.Contains("dragon", StringComparison.OrdinalIgnoreCase) ||
        nearbyKnown.Contains("portal", StringComparison.OrdinalIgnoreCase) ||
        nearbyKnown.Contains("chest", StringComparison.OrdinalIgnoreCase))
        priority -= 6;
    if (!nearbyUnknown.StartsWith("none", StringComparison.OrdinalIgnoreCase))
        priority -= 3;
    if (moby.Links.Count > 0 && !directLinks.StartsWith("none", StringComparison.OrdinalIgnoreCase))
        priority -= 2;
    return Math.Clamp(priority, 1, 99);
}

string MobyIdentityLaneFromMoby(Moby moby)
{
    string text = $"{moby.DisplayLabel} {moby.CandidateKind} {moby.Evidence} {IdentityFingerprint(moby)}".ToLowerInvariant();
    return moby.VisualKind switch
    {
        MobyVisualKind.FlightTarget => "flight/special object",
        MobyVisualKind.Control => "control/helper",
        MobyVisualKind.Actor or MobyVisualKind.Chest or MobyVisualKind.Gem or MobyVisualKind.Key or MobyVisualKind.Dragon or MobyVisualKind.Portal => "actor or interactive object",
        MobyVisualKind.Scenery or MobyVisualKind.Whirlwind => "visual scenery/prop",
        _ when text.Contains("special object 0x40") || text.Contains("special object 0x50") || text.Contains("special object 0x60") ||
            text.Contains("type=0x40") || text.Contains("type=0x50") || text.Contains("type=0x60") => "flight/special object",
        _ when text.Contains("nonvisual") || text.Contains("control") || text.Contains("helper") || text.Contains("type=0x00") => "control/helper",
        _ when text.Contains("actor") || text.Contains("enemy") || text.Contains("container") || text.Contains("f4b=0x10") ||
            text.Contains("f4b=0x54") || text.Contains("f4b=0x55") || text.Contains("f4b=0x56") => "actor or interactive object",
        _ when text.Contains("scenery") || text.Contains("prop") || text.Contains("f4b=0xff") => "visual scenery/prop",
        _ => "unknown visual candidate"
    };
}

string FirstMeaningfulLead(params string[] leads)
{
    foreach (string lead in leads)
    {
        if (string.IsNullOrWhiteSpace(lead))
            continue;
        if (lead.StartsWith("none", StringComparison.OrdinalIgnoreCase) ||
            lead.StartsWith("unique", StringComparison.OrdinalIgnoreCase))
            continue;
        return lead;
    }

    return leads.FirstOrDefault(lead => !string.IsNullOrWhiteSpace(lead)) ?? "";
}

List<string> BuildActorByteAliasSummary(
    PrecisionIdentityGroup group,
    IReadOnlyList<(LevelDefinition Level, Moby Moby)> allMobys)
{
    IdentityFingerprintParts parts = ParseIdentityFingerprint(group.Key);
    if (string.IsNullOrWhiteSpace(parts.TypeHex) || string.IsNullOrWhiteSpace(parts.SourceByte36Hex))
        return new List<string>();

    return allMobys
        .Where(item => IsExactIdentity(item.Moby))
        .Where(item => $"0x{item.Moby.Type:X2}".Equals(parts.TypeHex, StringComparison.OrdinalIgnoreCase))
        .Where(item => $"0x{item.Moby.SourceByte36:X2}".Equals(parts.SourceByte36Hex, StringComparison.OrdinalIgnoreCase))
        .GroupBy(item => item.Moby.DisplayLabel, StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(labelGroup => labelGroup.Count())
        .ThenBy(labelGroup => labelGroup.Key)
        .Take(8)
        .Select(labelGroup => $"{labelGroup.Key} ({labelGroup.Count()})")
        .ToList();
}

bool IsQuickWinIdentityGroup(
    PrecisionIdentityGroup group,
    IReadOnlyList<(LevelDefinition Level, Moby Moby)> allMobys,
    IReadOnlyDictionary<string, ObservedFingerprintGroup> observedFingerprints)
{
    if (group.Count < 4)
        return false;

    string lane = IdentityTestLane(group);
    if (string.Equals(lane, "control/helper", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(lane, "flight/special object", StringComparison.OrdinalIgnoreCase))
        return false;

    if (observedFingerprints.TryGetValue(group.Key, out ObservedFingerprintGroup? observed) &&
        string.Equals(ObservedStatus(observed), "conflict", StringComparison.OrdinalIgnoreCase))
        return false;

    List<(LevelDefinition Level, Moby Moby)> matches = allMobys
        .Where(item => string.Equals(IdentityFingerprint(item.Moby), group.Key, StringComparison.OrdinalIgnoreCase))
        .ToList();
    if (matches.Count < 4)
        return false;

    bool singleLevel = matches.Select(item => item.Level.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1;
    bool dominantSpecialPointer = matches
        .GroupBy(item => item.Moby.SpecialDataPointer)
        .Any(pointerGroup => pointerGroup.Count() >= Math.Max(4, matches.Count * 3 / 4));
    bool noExactActorByteAlias = BuildActorByteAliasSummary(group, allMobys).Count == 0;

    return singleLevel && dominantSpecialPointer && noExactActorByteAlias;
}

string QuickWinReason(PrecisionIdentityGroup group, IReadOnlyList<(LevelDefinition Level, Moby Moby)> allMobys)
{
    List<(LevelDefinition Level, Moby Moby)> matches = allMobys
        .Where(item => string.Equals(IdentityFingerprint(item.Moby), group.Key, StringComparison.OrdinalIgnoreCase))
        .ToList();
    string levelName = matches
        .GroupBy(item => item.Level.DisplayName)
        .OrderByDescending(levelGroup => levelGroup.Count())
        .FirstOrDefault()?.Key ?? "single level";
    string pointer = matches
        .GroupBy(item => item.Moby.SpecialDataPointer)
        .OrderByDescending(pointerGroup => pointerGroup.Count())
        .Select(pointerGroup => $"0x{pointerGroup.Key:X8} ({pointerGroup.Count()}/{matches.Count})")
        .FirstOrDefault() ?? "one package";
    return $"{levelName}-local repeated fingerprint with dominant special-data pointer {pointer}.";
}

List<string> BuildNeighborSummary(
    PrecisionIdentityGroup group,
    IReadOnlyList<(LevelDefinition Level, Moby Moby)> matches,
    IReadOnlyDictionary<string, List<(LevelDefinition Level, Moby Moby)>> byLevel)
{
    List<string> result = new();
    IEnumerable<(LevelDefinition Level, Moby Moby)> samples = matches
        .GroupBy(item => item.Level.Key, StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(levelGroup => levelGroup.Count())
        .ThenBy(levelGroup => levelGroup.Key)
        .SelectMany(levelGroup => levelGroup.OrderBy(item => item.Moby.TrueIndex).Take(2))
        .Take(6);

    foreach ((LevelDefinition level, Moby moby) in samples)
    {
        if (!byLevel.TryGetValue(level.Key, out List<(LevelDefinition Level, Moby Moby)>? levelMobys))
            continue;

        List<string> neighbors = levelMobys
            .Where(item => item.Moby.TrueIndex != moby.TrueIndex)
            .Where(item => !string.Equals(IdentityFingerprint(item.Moby), group.Key, StringComparison.OrdinalIgnoreCase))
            .Where(item => IsExactIdentity(item.Moby))
            .Select(item => new
            {
                item.Moby,
                Distance = Math.Sqrt(DistanceSquared(item.Moby.Position, moby.Position))
            })
            .Where(item => item.Distance <= 768)
            .OrderBy(item => item.Distance)
            .ThenBy(item => item.Moby.TrueIndex)
            .Take(3)
            .Select(item => $"T{item.Moby.TrueIndex} {item.Moby.DisplayLabel} @{item.Distance:0}")
            .ToList();
        if (neighbors.Count > 0)
            result.Add($"{level.DisplayName}:T{moby.TrueIndex} near {string.Join(", ", neighbors)}");
    }

    return result;
}

string BuildContextInterpretation(
    PrecisionIdentityGroup group,
    IReadOnlyList<(LevelDefinition Level, Moby Moby)> matches,
    IReadOnlyList<string> actorByteAliases,
    string evidenceState)
{
    string lane = IdentityTestLane(group);
    int levelCount = matches.Select(item => item.Level.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count();
    int specialPointerCount = matches.Select(item => item.Moby.SpecialDataPointer).Distinct().Count();
    bool hasSameActorByteAliases = actorByteAliases.Count > 0;

    if (lane == "control/helper")
        return "Treat as behavior infrastructure first. Solo movement can prove placement, but not a precise role; test it with the nearest visible cluster.";
    if (evidenceState.StartsWith("single observation", StringComparison.OrdinalIgnoreCase))
        return "One label exists but is not enough for automatic promotion unless a live test matches this exact fingerprint again.";
    if (hasSameActorByteAliases && levelCount > 1)
        return "The same actor byte has exact labels elsewhere, but this fingerprint differs by behavior/reward bytes and/or level package. Do not reuse that name without a focused visual test.";
    if (specialPointerCount == 1 && levelCount == 1)
        return "Level-local repeated package: a same-level row test with two samples should be enough to promote this fingerprint.";
    if (lane == "visual scenery/prop" && levelCount > 1)
        return "Likely a reusable prop family, but level-local package differences can share the same actor byte. Test two levels before global promotion.";
    if (lane == "actor or interactive object")
        return "Needs visual plus behavior/reward confirmation; model-only evidence is not enough for interactive actors or containers.";
    return "Use the listed nearby objects and special-data grouping to design the next focused live test.";
}

double DistanceSquared(Vector3f a, Vector3f b)
{
    double dx = a.X - b.X;
    double dy = a.Y - b.Y;
    double dz = a.Z - b.Z;
    return dx * dx + dy * dy + dz * dz;
}

string LevelDisplayName(string levelKey)
{
    return catalog.FindByKey(levelKey)?.DisplayName ?? levelKey;
}

string ObservedStatus(ObservedFingerprintGroup group)
{
    if (group.LabelCounts.Count != 1)
        return "conflict";

    string label = group.LabelCounts.Keys.First();
    if (IsSketchyObservedLabel(label))
        return "sketchy";

    return group.TotalObservations >= 2 ? "clean reusable" : "single observation";
}

bool IsSketchyObservedLabel(string label)
{
    string text = label.ToLowerInvariant();
    return text.Contains("?") ||
        text.Contains("related") ||
        text.Contains("crash") ||
        text.Contains("non working") ||
        text.Contains("helper") ||
        text.Contains("unknown");
}

string ReadJsonString(JsonElement element, string name, string fallback = "")
{
    if (!element.TryGetProperty(name, out JsonElement property))
        return fallback;

    return property.ValueKind switch
    {
        JsonValueKind.String => property.GetString() ?? fallback,
        JsonValueKind.Number => property.ToString(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => fallback
    };
}

int ReadJsonInt32(JsonElement element, string name, int fallback = 0)
{
    if (!element.TryGetProperty(name, out JsonElement property))
        return fallback;

    if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int numeric))
        return numeric;
    if (property.ValueKind == JsonValueKind.String)
    {
        string text = property.GetString()?.Trim() ?? "";
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex))
            return hex;
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            return parsed;
    }
    return fallback;
}

long ReadJsonInt64(JsonElement element, string name, long fallback = 0)
{
    if (!element.TryGetProperty(name, out JsonElement property))
        return fallback;

    if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out long numeric))
        return numeric;
    if (property.ValueKind == JsonValueKind.String)
    {
        string text = property.GetString()?.Trim() ?? "";
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            long.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hex))
            return hex;
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
            return parsed;
    }
    return fallback;
}

byte[] ParseHexPreview(string text)
{
    string compact = text.Replace(" ", "", StringComparison.Ordinal).Trim();
    if (compact.Length % 2 != 0)
        throw new InvalidOperationException($"Invalid hex preview length: {text}");

    byte[] bytes = new byte[compact.Length / 2];
    for (int i = 0; i < bytes.Length; i++)
        bytes[i] = byte.Parse(compact.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    return bytes;
}

bool TryGetSideWallAppendedTextureCoverage(TerrainPatchPlan plan, int appendedFaceCount, int textureId, out int matchingFaces)
{
    matchingFaces = 0;
    if (appendedFaceCount <= 0 || textureId < 0)
        return false;

    TerrainPatch? repack = plan.Patches.FirstOrDefault(patch =>
        string.Equals(patch.Kind, "terrain-sector-repack-hp-side-wall", StringComparison.OrdinalIgnoreCase));
    if (repack == null)
        return false;

    byte[] after = ParseHexPreview(repack.AfterHexPreview);
    int appendedByteCount = appendedFaceCount * 16;
    if (after.Length < appendedByteCount)
        return false;

    int appendedStart = after.Length - appendedByteCount;
    for (int i = 0; i < appendedFaceCount; i++)
    {
        int faceOffset = appendedStart + (i * 16);
        uint word3 = BitConverter.ToUInt32(after, faceOffset + 8);
        if ((word3 & 0x7Fu) == (uint)(textureId & 0x7F))
            matchingFaces++;
    }

    if (matchingFaces == appendedFaceCount)
        return true;

    int summaryMatchingFaces = plan.TerrainSideWalls
        .Where(summary => summary.TextureIds.Contains(textureId))
        .Sum(summary => summary.EmittedFaceCount);
    if (summaryMatchingFaces > 0)
    {
        matchingFaces = summaryMatchingFaces;
        return matchingFaces >= appendedFaceCount;
    }

    return false;
}

bool ContainsBytes(byte[] bytes, params byte[] needle)
{
    if (needle.Length == 0 || bytes.Length < needle.Length)
        return false;

    for (int i = 0; i <= bytes.Length - needle.Length; i++)
    {
        bool match = true;
        for (int j = 0; j < needle.Length; j++)
        {
            if (bytes[i + j] != needle[j])
            {
                match = false;
                break;
            }
        }

        if (match)
            return true;
    }

    return false;
}

void ReportCrossLevelObjectTemplateCatalog()
{
    string path = workspace.ResolveFile("spyro-object-templates.json");
    if (!File.Exists(path))
        throw new FileNotFoundException("Missing cross-level object template catalog.", path);

    using FileStream stream = File.OpenRead(path);
    using JsonDocument document = JsonDocument.Parse(stream);
    if (!document.RootElement.TryGetProperty("templates", out JsonElement templatesElement) ||
        templatesElement.ValueKind != JsonValueKind.Array)
        throw new InvalidOperationException("Cross-level object template catalog does not contain a templates array.");

    JsonElement[] templates = templatesElement
        .EnumerateArray()
        .ToArray();
    JsonElement[] addTemplates = templates
        .Where(template => ReadJsonBool(template, "showInAddList"))
        .ToArray();
    string[] requiredIds =
    [
        "common.key.peacekeepers.t78",
        "common.locked_chest.peacekeepers.t79",
        "enemy.green_wizard.wizardpeak.t6"
    ];
    string[] missing = requiredIds
        .Where(id => !addTemplates.Any(template => string.Equals(ReadJsonString(template, "id"), id, StringComparison.OrdinalIgnoreCase)))
        .ToArray();
    if (missing.Length > 0)
        throw new InvalidOperationException($"Cross-level object template catalog is missing Add Object template(s): {string.Join(", ", missing)}.");
    if (!templates.Any(template => string.Equals(ReadJsonString(template, "id"), CurrentStoneHillSpringTemplateId, StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException("Cross-level object template catalog is missing the diagnostic Spring Chest template metadata.");
    if (addTemplates.Any(template => string.Equals(ReadJsonString(template, "id"), CurrentStoneHillSpringTemplateId, StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException("Cross-level Spring Chest should stay hidden from the normal Add Object list until it passes in-game.");

    foreach (JsonElement template in addTemplates)
    {
        int type = ReadJsonInt32(template, "typeHex", -1);
        int flag4B = ReadJsonInt32(template, "flag4BHex", -1);
        if (type < 0 || flag4B < 0)
            throw new InvalidOperationException($"Cross-level object template {ReadJsonString(template, "id", "?")} has unparseable type/flag bytes.");
    }

    string visible = string.Join(", ", addTemplates.Select(template => ReadJsonString(template, "displayName", ReadJsonString(template, "id"))));
    Console.WriteLine($"Cross-level object templates: {addTemplates.Length} Add Object template(s): {visible}");
}

async Task ReportCrossLevelObjectImportAnalysis()
{
    if (!File.Exists(sourceImage))
    {
        Console.WriteLine("Cross-level import analysis: source disc not found; skipping.");
        return;
    }

    string templatePath = workspace.ResolveFile("spyro-object-templates.json");
    if (!File.Exists(templatePath))
    {
        Console.WriteLine("Cross-level import analysis: template catalog not found; skipping.");
        return;
    }

    string outDir = Path.Combine(workspace.RootPath, "_local", "smoke");
    Directory.CreateDirectory(outDir);
    LevelDefinition? stoneHillForEvidence = catalog.FindByKey("stonehill");
    if (stoneHillForEvidence != null)
    {
        await EnsureSyntheticCandidateFailureAsync(
            Path.Combine(workspace.RootPath, "_local", "objects", "stonehill-spring-chest-preserve-00c2-candidate.candidate-result.json"),
            stoneHillForEvidence,
            "stonehill.townsquare.springChest.package.local00C2Shell0149Over000E.v2",
            "Stone Hill hung indefinitely on the loading/flying screen after adding the Spring Chest candidate. Keep this recipe guarded and test the next mapped candidate instead.");
        await EnsureSyntheticSpringChestWrongBehaviorFailureAsync(
            Path.Combine(workspace.RootPath, "_local", "objects", "stonehill-spring-chest-prefix0700-candidate.candidate-result.json"),
            stoneHillForEvidence,
            "stonehill.townsquare.springChest.prefix0700CopyOnlyNoRoot.v12",
            "Stone Hill loads, but the placed Spring Chest candidate behaves as a regular flame/charge chest and rewards one red gem. Keep this recipe guarded; it is not a valid Spring Chest behavior candidate.");
        await EnsureSyntheticSpringChestWrongBehaviorFailureAsync(
            Path.Combine(workspace.RootPath, "_local", "objects", "stonehill-spring-chest-controller-alias-candidate.candidate-result.json"),
            stoneHillForEvidence,
            "stonehill.townsquare.springChest.package.fullControllerAlias01FEZeroGap26928.v14",
            "Stone Hill loads, but the placed Spring Chest candidate behaves as a regular Flame/Charge chest and rewards one red gem. Keep this recipe guarded; it is not a valid Spring Chest behavior candidate.");
        await EnsureSyntheticSpringChestWrongBehaviorFailureAsync(
            Path.Combine(workspace.RootPath, "_local", "objects", "stonehill-spring-chest-townsquare-native00c2-candidate.candidate-result.json"),
            stoneHillForEvidence,
            "stonehill.townsquare.springChest.package.native00C2ReplaceZeroGap26928.v2",
            "The native 0x00C2 replacement path is intentionally blocked after the Town Square route repeatedly failed to produce Spring Chest behavior in Stone Hill.");
    }

    CrossLevelObjectImportAnalysis analysis = CrossLevelObjectImportAnalyzer.Build(
        workspace,
        catalog,
        sourceImage,
        templatePath,
        ["stonehill", "artisans", "toasty"]);
    string jsonPath = Path.Combine(outDir, "cross-level-object-import-analysis.json");
    string markdownPath = Path.Combine(outDir, "cross-level-object-import-analysis.md");
    await CrossLevelObjectImportAnalyzer.WriteAsync(analysis, jsonPath, markdownPath);

    CrossLevelObjectImportRow? artisansKey = analysis.Rows.FirstOrDefault(row =>
        string.Equals(row.TemplateId, "common.key.peacekeepers.t78", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(row.TargetLevelKey, "artisans", StringComparison.OrdinalIgnoreCase));
    if (artisansKey == null || !string.Equals(artisansKey.SupportStatus, "supported-lightweight-object", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Cross-level key should remain the supported lightweight control case for Artisans.");

    CrossLevelObjectImportRow? toastyWizard = analysis.Rows.FirstOrDefault(row =>
        string.Equals(row.TemplateId, "enemy.green_wizard.wizardpeak.t6", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(row.TargetLevelKey, "toasty", StringComparison.OrdinalIgnoreCase));
    if (toastyWizard == null ||
        string.Equals(toastyWizard.SupportStatus, "supported-lightweight-object", StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(toastyWizard.RequiredExporterFeature, "ActorPackageImportAndPointerRebase", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Toasty dog-to-wizard analysis did not stay on the actor-package import path.");
    }
    if (!string.Equals(toastyWizard.RecipeId, "toasty.wizardpeak.greenWizard.package.overwriteUnused00EA.v2", StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(toastyWizard.RecipeStatus, "experimental-image-write", StringComparison.OrdinalIgnoreCase) ||
        toastyWizard.RecipeCopySegmentCount != 1 ||
        toastyWizard.RecipeRootEntryCount != 1)
    {
        throw new InvalidOperationException("Toasty dog-to-wizard should report the mapped experimental Green Wizard package recipe.");
    }
    if (CrossLevelActorPackageRecipeCatalog.FindPreferred("toasty", "wizardpeak", "enemyTransform")?.Status.Equals("experimental-image-write", StringComparison.OrdinalIgnoreCase) != true)
        throw new InvalidOperationException("Toasty Green Wizard should be current-level ready through the recipe catalog.");

    CrossLevelObjectImportRow? stoneHillSpring = analysis.Rows.FirstOrDefault(row =>
        string.Equals(row.TemplateId, CurrentStoneHillSpringTemplateId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(row.TargetLevelKey, "stonehill", StringComparison.OrdinalIgnoreCase));
    if (stoneHillSpring != null)
    {
        throw new InvalidOperationException("Stone Hill Peace Keepers T70 spring chest route should be hidden from the normal cross-level Add Object analysis after the latest in-game failure.");
    }
    CrossLevelActorPackageRecipe? stoneHillSpringRecipe = CrossLevelActorPackageRecipeCatalog.FindPreferred("stonehill", "peacekeepers", "springChest", workspace.RootPath);
    if (stoneHillSpringRecipe != null &&
        !string.Equals(stoneHillSpringRecipe.Status, "experimental-plan-only", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Stone Hill Peace Keepers spring chest diagnostic recipes should stay guarded and plan-only after the latest in-game failure.");
    }
    if (CrossLevelActorPackageRecipeCatalog.FindPreferred("stonehill", "townsquare", "springChest", workspace.RootPath) != null)
    {
        throw new InvalidOperationException("Stone Hill Town Square Spring Chest recipes should all be guarded after the repeated normal-chest result.");
    }

    CrossLevelObjectImportRow? artisansKeyChest = analysis.Rows.FirstOrDefault(row =>
        string.Equals(row.TemplateId, "common.locked_chest.peacekeepers.t79", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(row.TargetLevelKey, "artisans", StringComparison.OrdinalIgnoreCase));
    if (artisansKeyChest == null ||
        !string.Equals(artisansKeyChest.RecipeId, "artisans.peacekeepers.lockedChest.package.safeGapActorId.v4", StringComparison.OrdinalIgnoreCase) ||
        artisansKeyChest.RecipeCopySegmentCount != 1 ||
        artisansKeyChest.RecipeRootEntryCount != 1)
    {
        throw new InvalidOperationException("Artisans key chest analysis should point at the native safe-gap recipe candidate.");
    }
    if (CrossLevelActorPackageRecipeCatalog.FindPreferred("artisans", "peacekeepers", "lockedChest")?.Status.Equals("experimental-image-write", StringComparison.OrdinalIgnoreCase) != true)
        throw new InvalidOperationException("Artisans Key Chest should be current-level ready through the recipe catalog.");

    int supported = analysis.Rows.Count(row => string.Equals(row.SupportStatus, "supported-lightweight-object", StringComparison.OrdinalIgnoreCase));
    int packageBlocked = analysis.Rows.Count(row => row.SupportStatus.Contains("actor-package", StringComparison.OrdinalIgnoreCase));
    int recipeCandidates = analysis.Rows.Count(row => !string.IsNullOrWhiteSpace(row.RecipeId));
    Console.WriteLine($"Cross-level import analysis: {analysis.Rows.Count} target/template row(s), supported={supported}, actor-package blocked={packageBlocked}, recipe candidates={recipeCandidates}, report={markdownPath}");
}

async Task ReportUniversalChestCompatibility()
{
    if (!File.Exists(sourceImage))
    {
        Console.WriteLine("Universal chest compatibility: source disc not found; skipping.");
        return;
    }

    string templatePath = workspace.ResolveFile("spyro-object-templates.json");
    if (!File.Exists(templatePath))
    {
        Console.WriteLine("Universal chest compatibility: template catalog not found; skipping.");
        return;
    }

    string[] allLevelKeys = catalog.Levels
        .Where(level => level.HasSourceTable)
        .Select(level => level.Key)
        .ToArray();
    CrossLevelObjectImportAnalysis analysis = CrossLevelObjectImportAnalyzer.Build(
        workspace,
        catalog,
        sourceImage,
        templatePath,
        allLevelKeys);

    CrossLevelObjectImportRow[] chestRows = analysis.Rows
        .Where(row =>
            string.Equals(row.Family, "key", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(row.Family, "lockedChest", StringComparison.OrdinalIgnoreCase))
        .OrderBy(row => row.TargetLevelName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(row => UniversalChestSort(row.Family))
        .ToArray();

    string outDir = Path.Combine(workspace.RootPath, "_local", "smoke");
    Directory.CreateDirectory(outDir);
    string jsonPath = Path.Combine(outDir, "universal-chest-compatibility.json");
    string markdownPath = Path.Combine(outDir, "universal-chest-compatibility.md");
    await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(new
    {
        generatedAt = DateTimeOffset.Now,
        sourceImage,
        templatePath,
        levelCount = allLevelKeys.Length,
        rows = chestRows
    }, new JsonSerializerOptions { WriteIndented = true }));
    await File.WriteAllTextAsync(markdownPath, BuildUniversalChestCompatibilityMarkdown(chestRows, allLevelKeys.Length));

    int expectedRows = allLevelKeys.Length * 2;
    if (chestRows.Length != expectedRows)
        throw new InvalidOperationException($"Universal chest compatibility should cover public beta Key and Key Chest templates for every source-table level. Expected {expectedRows}, found {chestRows.Length}.");
    if (chestRows.Any(row => string.Equals(row.Family, "key", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(row.SupportStatus, "supported-lightweight-object", StringComparison.OrdinalIgnoreCase)))
    {
        throw new InvalidOperationException("Universal chest compatibility found a level where the Key template is not lightweight-supported.");
    }

    int lockedChestBlocked = chestRows.Count(row => string.Equals(row.Family, "lockedChest", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(row.SupportStatus, "supported-lightweight-object", StringComparison.OrdinalIgnoreCase));
    int recipeRows = chestRows.Count(row => !string.IsNullOrWhiteSpace(row.RecipeId));
    Console.WriteLine($"Universal chest compatibility: {allLevelKeys.Length} levels, key-chest blocked={lockedChestBlocked}, mapped recipes={recipeRows}, report={markdownPath}");
}

async Task ReportUniversalChestStrategy()
{
    if (!File.Exists(sourceImage))
    {
        Console.WriteLine("Universal chest strategy: source disc not found; skipping.");
        return;
    }

    CrossLevelChestStrategyReport report = CrossLevelChestStrategyAnalyzer.Build(workspace, catalog, sourceImage);
    string outDir = Path.Combine(workspace.RootPath, "_local", "smoke");
    Directory.CreateDirectory(outDir);
    string jsonPath = Path.Combine(outDir, "universal-chest-strategy.json");
    string markdownPath = Path.Combine(outDir, "universal-chest-strategy.md");
    await CrossLevelChestStrategyAnalyzer.WriteAsync(report, jsonPath, markdownPath);
    UniversalNativeChestCloneExportReport cloneReport = await ReportUniversalNativeChestCloneExports(report);

    if (report.Levels.Count(level => level.Key.Strategy == "universal-lightweight") != report.Levels.Count)
        throw new InvalidOperationException("Universal chest strategy expected every level to support lightweight keys.");

    CrossLevelChestLevelStrategy stoneHill = report.Levels.First(level => string.Equals(level.LevelKey, "stonehill", StringComparison.OrdinalIgnoreCase));
    if (stoneHill.KeyChest.Strategy != "native-clone" || stoneHill.KeyChest.FirstNativeTrueIndex < 0)
        throw new InvalidOperationException("Stone Hill should use its native Key Chest donor path instead of cross-level package import.");
    if (stoneHill.SpringChest.Strategy == "native-clone")
        throw new InvalidOperationException("Stone Hill should not claim native Spring Chest cloning; it has no native 0x0149 source records.");

    int nativeKeyChests = report.Levels.Count(level => level.KeyChest.Strategy == "native-clone");
    int nativeSpringChests = report.Levels.Count(level => level.SpringChest.Strategy == "native-clone");
    int packageWork = report.Levels.Count(level => level.KeyChest.Strategy.Contains("package", StringComparison.OrdinalIgnoreCase) || level.SpringChest.Strategy.Contains("package", StringComparison.OrdinalIgnoreCase));
    if (!cloneReport.Rows.Any(row =>
        string.Equals(row.LevelKey, "stonehill", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(row.ActorIdHex, "0x00AE", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(row.Status, "proved", StringComparison.OrdinalIgnoreCase)))
    {
        throw new InvalidOperationException("Native chest clone proof did not prove Stone Hill's native Key Chest donor path.");
    }

    int blockedCloneProofs = cloneReport.Rows.Count(row => !string.Equals(row.Status, "proved", StringComparison.OrdinalIgnoreCase));
    Console.WriteLine($"Universal chest strategy: native key-chest clone levels={nativeKeyChests}, native spring-chest clone levels={nativeSpringChests}, package-work levels={packageWork}, blocked clone proofs={blockedCloneProofs}, report={markdownPath}");
}

async Task ReportUniversalChestPackageRecipePlanner()
{
    if (!File.Exists(sourceImage))
    {
        Console.WriteLine("Universal chest package planner: source disc not found; skipping.");
        return;
    }

    CrossLevelChestPackageRecipePlanReport report = CrossLevelChestPackageRecipePlanner.Build(workspace, catalog, sourceImage);
    string outDir = Path.Combine(workspace.RootPath, "_local", "smoke");
    Directory.CreateDirectory(outDir);
    string jsonPath = Path.Combine(outDir, "universal-chest-package-recipe-planner.json");
    string markdownPath = Path.Combine(outDir, "universal-chest-package-recipe-planner.md");
    await CrossLevelChestPackageRecipePlanner.WriteAsync(report, jsonPath, markdownPath);

    int plannedKeyChests = report.Rows.Count(row => row.CanPlan && string.Equals(row.Family, "lockedChest", StringComparison.OrdinalIgnoreCase));
    int plannedSpringChests = report.Rows.Count(row => row.CanPlan && string.Equals(row.Family, "springChest", StringComparison.OrdinalIgnoreCase));
    int neededKeyChests = report.Rows.Count(row => string.Equals(row.CurrentStrategy, "needs-package-recipe", StringComparison.OrdinalIgnoreCase) && string.Equals(row.Family, "lockedChest", StringComparison.OrdinalIgnoreCase));
    int neededSpringChests = report.Rows.Count(row => string.Equals(row.CurrentStrategy, "needs-package-recipe", StringComparison.OrdinalIgnoreCase) && string.Equals(row.Family, "springChest", StringComparison.OrdinalIgnoreCase));
    int plannedNeededKeyChests = report.Rows.Count(row => row.CanPlan && string.Equals(row.CurrentStrategy, "needs-package-recipe", StringComparison.OrdinalIgnoreCase) && string.Equals(row.Family, "lockedChest", StringComparison.OrdinalIgnoreCase));
    int plannedNeededSpringChests = report.Rows.Count(row => row.CanPlan && string.Equals(row.CurrentStrategy, "needs-package-recipe", StringComparison.OrdinalIgnoreCase) && string.Equals(row.Family, "springChest", StringComparison.OrdinalIgnoreCase));
    if (neededKeyChests == 0 || plannedKeyChests == 0)
        throw new InvalidOperationException("Universal Key Chest package planner should find candidate slots for missing Key Chest recipe levels.");
    if (neededSpringChests == 0 || plannedSpringChests == 0)
        throw new InvalidOperationException("Universal Spring Chest package planner should find candidate slots for missing Spring Chest recipe levels.");
    if (plannedNeededKeyChests > neededKeyChests || plannedNeededSpringChests > neededSpringChests)
        throw new InvalidOperationException("Universal chest package planner should not plan more recipes than levels that need package recipes.");

    Console.WriteLine($"Universal chest package planner: key-chest plans={plannedKeyChests} ({plannedNeededKeyChests}/{neededKeyChests} missing), spring-chest plans={plannedSpringChests} ({plannedNeededSpringChests}/{neededSpringChests} missing), report={markdownPath}");
}

async Task<UniversalNativeChestCloneExportReport> ReportUniversalNativeChestCloneExports(CrossLevelChestStrategyReport strategyReport)
{
    string outDir = Path.Combine(workspace.RootPath, "_local", "smoke", "native-chest-clone-edits");
    Directory.CreateDirectory(outDir);
    SourceDiscLayout layout = DetectSourceDiscLayout(sourceImage);
    using FileStream stream = File.OpenRead(sourceImage);

    List<UniversalNativeChestCloneExportRow> rows = [];
    foreach (CrossLevelChestLevelStrategy strategyLevel in strategyReport.Levels.OrderBy(level => level.LevelName, StringComparer.OrdinalIgnoreCase))
    {
        LevelDefinition? level = catalog.FindByKey(strategyLevel.LevelKey);
        if (level?.HasSourceTable != true)
            continue;

        List<Moby> edits = [];
        int nextIndex = 0;
        int nextTrueIndex = level.SourceRecordCount;

        if (strategyLevel.KeyChest.Strategy == "native-clone")
        {
            Moby keyChest = CreateNativeChestCloneMoby(stream, layout, level, strategyLevel.KeyChest, nextIndex++, nextTrueIndex++, "Native Key Chest");
            Moby key = CreateUniversalKeyMoby(
                level,
                nextIndex++,
                nextTrueIndex++,
                strategyLevel.KeyChest.DisplayName,
                new Vector3f(keyChest.Position.X - 160, keyChest.Position.Y, keyChest.Position.Z));
            edits.Add(key);
            edits.Add(keyChest);
        }

        if (strategyLevel.SpringChest.Strategy == "native-clone")
            edits.Add(CreateNativeChestCloneMoby(stream, layout, level, strategyLevel.SpringChest, nextIndex++, nextTrueIndex++, "Native Spring Chest"));

        if (edits.Count == 0)
            continue;

        string editPath = Path.Combine(outDir, $"{strategyLevel.LevelKey}-native-chest-clones.json");
        await MobyEditStore.SaveAsync(editPath, edits, $"{strategyLevel.LevelName} native chest clone proof");
        MobySourcePatchPlan plan;
        try
        {
            plan = MobySourcePatchExporter.BuildPlan(
                sourceImage,
                DiscImageLocator.FindCueForImage(sourceImage),
                Path.Combine(workspace.RootPath, "_local", "objects", "native-chest-clone-proofs", $"{strategyLevel.LevelKey}.bin"),
                Path.Combine(workspace.RootPath, "_local", "objects", "native-chest-clone-proofs", $"{strategyLevel.LevelKey}.cue"),
                level,
                editPath);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or JsonException)
        {
            rows.Add(new UniversalNativeChestCloneExportRow(
                LevelKey: strategyLevel.LevelKey,
                LevelName: strategyLevel.LevelName,
                ObjectName: "Native chest clone proof",
                ActorIdHex: "",
                TrueIndex: -1,
                PatchCount: 0,
                HasSpecialClone: false,
                EditPath: editPath,
                Status: "blocked",
                Notes: ex.Message));
            continue;
        }

        if (plan.SkippedEdits.Count != 0)
            throw new InvalidOperationException($"{strategyLevel.LevelName} native chest clone proof skipped edit(s): {string.Join("; ", plan.SkippedEdits)}");
        if (plan.PackageImportPreviews.Count != 0 || plan.Patches.Any(patch => patch.Kind.Contains("actor-package", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"{strategyLevel.LevelName} native chest clone proof should not use actor-package imports.");

        bool hasSourceCountPatch = plan.Patches.Any(patch => string.Equals(patch.Kind, "moby-source-count", StringComparison.OrdinalIgnoreCase));
        string rowStatus = hasSourceCountPatch ? "proved" : "candidate-no-count-field";
        string rowNotes = hasSourceCountPatch
            ? ""
            : string.Join(" ", plan.Notes.Where(note => note.Contains("bytes before the source moby table", StringComparison.OrdinalIgnoreCase)));

        foreach (Moby edit in edits)
        {
            MobySourcePatch? append = plan.Patches.FirstOrDefault(patch =>
                string.Equals(patch.Kind, "moby-record-append", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(patch.MobyLabel, edit.Label, StringComparison.OrdinalIgnoreCase));
            if (append == null)
                throw new InvalidOperationException($"{strategyLevel.LevelName} native clone proof did not append {edit.Label}.");

            byte[] after = ParseHexPreview(append.AfterHexPreview);
            int actorId = after[0x36] | (after[0x37] << 8);
            if (edit.SourceByte36 != after[0x36] || edit.SourceByte37 != after[0x37])
                throw new InvalidOperationException($"{strategyLevel.LevelName} native clone proof wrote the wrong actor bytes for {edit.Label}.");

            bool shouldHaveSpecialClone = edit.Type == 0x20 && edit.SpecialDataPointer != 0;
            bool hasSpecialClone = plan.Patches.Any(patch =>
                string.Equals(patch.Kind, "moby-special-data-clone", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(patch.MobyLabel, edit.Label, StringComparison.OrdinalIgnoreCase));
            if (shouldHaveSpecialClone && !hasSpecialClone)
                throw new InvalidOperationException($"{strategyLevel.LevelName} native clone proof did not clone special data for {edit.Label}.");

            rows.Add(new UniversalNativeChestCloneExportRow(
                LevelKey: strategyLevel.LevelKey,
                LevelName: strategyLevel.LevelName,
                ObjectName: edit.Label,
                ActorIdHex: $"0x{actorId:X4}",
                TrueIndex: edit.TrueIndex,
                PatchCount: plan.PatchCount,
                HasSpecialClone: hasSpecialClone,
                EditPath: editPath,
                Status: rowStatus,
                Notes: rowNotes));
        }
    }

    int keyChestProofs = rows.Count(row => row.Status == "proved" && string.Equals(row.ActorIdHex, "0x00AE", StringComparison.OrdinalIgnoreCase));
    int springChestProofs = rows.Count(row => row.Status == "proved" && string.Equals(row.ActorIdHex, "0x0149", StringComparison.OrdinalIgnoreCase));
    UniversalNativeChestCloneExportReport report = new(DateTimeOffset.Now, keyChestProofs, springChestProofs, rows);
    string jsonPath = Path.Combine(workspace.RootPath, "_local", "smoke", "universal-native-chest-clone-export.json");
    string markdownPath = Path.Combine(workspace.RootPath, "_local", "smoke", "universal-native-chest-clone-export.md");
    await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    await File.WriteAllTextAsync(markdownPath, BuildUniversalNativeChestCloneExportMarkdown(report));
    int blocked = rows.Count(row => row.Status == "blocked");
    int candidateNoCount = rows.Count(row => row.Status == "candidate-no-count-field");
    Console.WriteLine($"Universal native chest clone export: key-chest proofs={keyChestProofs}, spring-chest proofs={springChestProofs}, blocked={blocked}, count-field candidates={candidateNoCount}, report={markdownPath}");
    return report;
}

Moby CreateUniversalKeyMoby(LevelDefinition level, int index, int trueIndex, string companionName, Vector3f position)
{
    return new Moby
    {
        Index = index,
        TrueIndex = trueIndex,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(trueIndex),
        Position = position,
        OriginalPosition = position,
        Type = 0x18,
        OriginalType = 0x18,
        State = 0,
        OriginalState = 0,
        SourceByte36 = 0xAD,
        OriginalSourceByte36 = 0xAD,
        SourceByte37 = 0x00,
        SourceByte4F = 0x02,
        OriginalSourceByte4F = 0x02,
        Flag4A = 0x40,
        Flag4B = 0xFF,
        OriginalFlag4B = 0xFF,
        Color = GemValue.Yellow.Color,
        Label = $"Universal Key for {companionName}",
        OriginalLabel = $"Universal Key for {companionName}",
        PatchStatus = "supported-lightweight-object",
        PatchLead = $"Universal key paired with a native {companionName} clone in {level.DisplayName}.",
        CrossLevelTemplateId = "common.key.peacekeepers.t78",
        CrossLevelFamily = "key",
        CrossLevelSourceLevelKey = "PeaceKeepers",
        CrossLevelSourceLevelName = "Peace Keepers",
        CrossLevelSourceTrueIndex = 78,
        CrossLevelRequiredExporterFeature = "DirectSourceRecordAppend",
        IsAdded = true
    };
}

Moby CreateNativeChestCloneMoby(FileStream stream, SourceDiscLayout layout, LevelDefinition level, ChestObjectStrategy strategy, int index, int trueIndex, string label)
{
    if (strategy.FirstNativeTrueIndex < 0)
        throw new InvalidOperationException($"{level.DisplayName} has no native donor for {strategy.DisplayName}.");

    long tableWadOffset = ParseFlexibleLong(level.SourceTableWadOffset);
    byte[] record = ReadSourceWadBytes(stream, layout, tableWadOffset + ((long)strategy.FirstNativeTrueIndex * 0x58), 0x58);
    Vector3f donorPosition = new(
        BitConverter.ToInt32(record, 0x0C) / 16f,
        BitConverter.ToInt32(record, 0x10) / 16f,
        BitConverter.ToInt32(record, 0x14) / 16f);
    Vector3f position = new(donorPosition.X + 128 + (index * 32), donorPosition.Y, donorPosition.Z);
    int actorId = record[0x36] | (record[0x37] << 8);
    uint specialOffset = BitConverter.ToUInt32(record, 0);
    return new Moby
    {
        Index = index,
        TrueIndex = trueIndex,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(trueIndex),
        Position = position,
        OriginalPosition = position,
        Type = record[0x50],
        OriginalType = record[0x50],
        State = record[0x51],
        OriginalState = record[0x51],
        SourceByte36 = record[0x36],
        OriginalSourceByte36 = record[0x36],
        SourceByte37 = record[0x37],
        OriginalSourceByte37 = record[0x37],
        SourceByte4F = record[0x4F],
        OriginalSourceByte4F = record[0x4F],
        Flag4A = record[0x52],
        OriginalFlag4A = record[0x52],
        Flag4B = record[0x53],
        OriginalFlag4B = record[0x53],
        SpecialDataPointer = specialOffset,
        Color = Moby.ColorForType(record[0x50]),
        Label = $"{level.DisplayName} {label}",
        OriginalLabel = $"{level.DisplayName} {label}",
        CandidateKind = $"{strategy.DisplayName} native clone",
        Confidence = "native-clone-proof",
        Evidence = $"Cloned from {level.DisplayName} T{strategy.FirstNativeTrueIndex}, actor 0x{actorId:X4}.",
        PatchStatus = "native-clone",
        PatchLead = $"Same-level clone from {level.DisplayName} {strategy.DisplayName} donor T{strategy.FirstNativeTrueIndex}; no actor-package import expected.",
        IsAdded = true
    };
}

string BuildUniversalNativeChestCloneExportMarkdown(UniversalNativeChestCloneExportReport report)
{
    StringBuilder builder = new();
    builder.AppendLine("# Universal Native Chest Clone Export");
    builder.AppendLine();
    builder.AppendLine("This proves the levels where Key Chests or Spring Chests can be added by cloning native same-level donors, without actor-package imports.");
    builder.AppendLine();
    builder.AppendLine($"Key Chest proofs: {report.KeyChestProofs}");
    builder.AppendLine($"Spring Chest proofs: {report.SpringChestProofs}");
    builder.AppendLine();
    builder.AppendLine("| Level | Object | Actor | New T | Special cloned | Status | Notes | Edit manifest |");
    builder.AppendLine("|---|---|---|---:|---|---|---|---|");
    foreach (UniversalNativeChestCloneExportRow row in report.Rows.OrderBy(row => row.LevelName, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.ObjectName, StringComparer.OrdinalIgnoreCase))
        builder.AppendLine($"| {MarkdownCell(row.LevelName)} | {MarkdownCell(row.ObjectName)} | `{row.ActorIdHex}` | {row.TrueIndex} | {row.HasSpecialClone} | {MarkdownCell(row.Status)} | {MarkdownCell(row.Notes)} | `{row.EditPath}` |");
    return builder.ToString();
}

string BuildUniversalChestCompatibilityMarkdown(IReadOnlyList<CrossLevelObjectImportRow> rows, int levelCount)
{
    StringBuilder builder = new();
    builder.AppendLine("# Universal Chest Compatibility");
    builder.AppendLine();
    builder.AppendLine("This report tracks public beta cross-level Keys and Key Chests. Spring Chests are kept in the dedicated diagnostic report until their behavior is proven in-game.");
    builder.AppendLine();
    builder.AppendLine($"Levels covered: {levelCount}");
    builder.AppendLine();
    builder.AppendLine("| Object | Supported | Needs RAM | Missing package | Missing signature | Recipe mapped |");
    builder.AppendLine("|---|---:|---:|---:|---:|---:|");
    foreach (IGrouping<string, CrossLevelObjectImportRow> group in rows.GroupBy(row => row.DisplayName).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
    {
        int supported = CountStatus(group, "supported-lightweight-object");
        int needsRam = CountStatus(group, "needs-target-ram-capture");
        int missingPackage = CountStatus(group, "blocked-missing-target-actor-package");
        int missingSignature = CountStatus(group, "blocked-missing-template-signature");
        int recipeMapped = group.Count(row => !string.IsNullOrWhiteSpace(row.RecipeId));
        builder.AppendLine($"| {MarkdownCell(group.Key)} | {supported} | {needsRam} | {missingPackage} | {missingSignature} | {recipeMapped} |");
    }

    builder.AppendLine();
    builder.AppendLine("| Level | Key | Key Chest | Next Action |");
    builder.AppendLine("|---|---|---|---|");
    foreach (IGrouping<string, CrossLevelObjectImportRow> levelGroup in rows.GroupBy(row => row.TargetLevelName).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
    {
        CrossLevelObjectImportRow? key = levelGroup.FirstOrDefault(row => string.Equals(row.Family, "key", StringComparison.OrdinalIgnoreCase));
        CrossLevelObjectImportRow? lockedChest = levelGroup.FirstOrDefault(row => string.Equals(row.Family, "lockedChest", StringComparison.OrdinalIgnoreCase));
        string nextAction = PickUniversalChestNextAction(lockedChest, springChest: null);
        builder.AppendLine($"| {MarkdownCell(levelGroup.Key)} | {MarkdownCell(StatusWithRecipe(key))} | {MarkdownCell(StatusWithRecipe(lockedChest))} | {MarkdownCell(nextAction)} |");
    }

    return builder.ToString();
}

int UniversalChestSort(string family)
{
    return family switch
    {
        "key" => 0,
        "lockedChest" => 1,
        "springChest" => 2,
        _ => 9
    };
}

int CountStatus(IEnumerable<CrossLevelObjectImportRow> rows, string status)
{
    return rows.Count(row => string.Equals(row.SupportStatus, status, StringComparison.OrdinalIgnoreCase));
}

string StatusWithRecipe(CrossLevelObjectImportRow? row)
{
    if (row == null)
        return "missing";
    if (string.IsNullOrWhiteSpace(row.RecipeId))
        return row.SupportStatus;
    return $"{row.SupportStatus}; {row.RecipeId}";
}

string PickUniversalChestNextAction(CrossLevelObjectImportRow? lockedChest, CrossLevelObjectImportRow? springChest)
{
    if (lockedChest?.SupportStatus == "needs-target-ram-capture" || springChest?.SupportStatus == "needs-target-ram-capture")
        return "Capture before-clean RAM for actor pointer table.";
    if (lockedChest != null && string.IsNullOrWhiteSpace(lockedChest.RecipeId) && lockedChest.SupportStatus.Contains("actor-package", StringComparison.OrdinalIgnoreCase))
        return "Map Key Chest package import recipe.";
    if (springChest != null && string.IsNullOrWhiteSpace(springChest.RecipeId) && springChest.SupportStatus.Contains("actor-package", StringComparison.OrdinalIgnoreCase))
        return "Map Spring Chest package import recipe.";
    if (lockedChest != null && !string.IsNullOrWhiteSpace(lockedChest.RecipeId) && !string.Equals(lockedChest.RecipeStatus, "verified-image-write", StringComparison.OrdinalIgnoreCase))
        return "Run guarded Key Chest candidate test.";
    if (springChest != null && !string.IsNullOrWhiteSpace(springChest.RecipeId) && !string.Equals(springChest.RecipeStatus, "verified-image-write", StringComparison.OrdinalIgnoreCase))
        return "Run guarded Spring Chest candidate test.";
    if (lockedChest != null && string.IsNullOrWhiteSpace(lockedChest.RecipeId) && lockedChest.SupportStatus.Contains("template-signature", StringComparison.OrdinalIgnoreCase))
        return "Identify this level's native Key Chest signature/special-data.";
    if (springChest != null && string.IsNullOrWhiteSpace(springChest.RecipeId) && springChest.SupportStatus.Contains("template-signature", StringComparison.OrdinalIgnoreCase))
        return "Identify this level's native Spring Chest signature/special-data.";
    return "Ready or already native-supported.";
}

string MarkdownCell(string value)
{
    return (value ?? "").Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}

void ReportCrossLevelEditorTemplateReadiness()
{
    string templatePath = workspace.ResolveFile("spyro-object-templates.json");
    if (!File.Exists(templatePath))
        throw new FileNotFoundException("Missing cross-level object template catalog.", templatePath);

    using FileStream stream = File.OpenRead(templatePath);
    using JsonDocument document = JsonDocument.Parse(stream);
    JsonElement[] allTemplates = document.RootElement.GetProperty("templates")
        .EnumerateArray()
        .ToArray();
    JsonElement[] templates = allTemplates
        .Where(template => ReadJsonBool(template, "showInAddList"))
        .ToArray();

    JsonElement key = FindTemplate(templates, "common.key.peacekeepers.t78");
    JsonElement springChest = FindTemplate(allTemplates, CurrentStoneHillSpringTemplateId);
    JsonElement keyChest = FindTemplate(templates, "common.locked_chest.peacekeepers.t79");
    JsonElement greenWizard = FindTemplate(templates, "enemy.green_wizard.wizardpeak.t6");
    if (templates.Any(template => string.Equals(ReadJsonString(template, "id"), CurrentStoneHillSpringTemplateId, StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException("Editor Add Object templates should hide cross-level Spring Chest until it passes in-game.");

    LevelDefinition artisans = catalog.FindByKey("artisans") ?? throw new InvalidOperationException("Artisans level missing.");
    LevelDefinition toasty = catalog.FindByKey("toasty") ?? throw new InvalidOperationException("Toasty level missing.");
    LevelDefinition? stoneHillLevel = catalog.FindByKey("stonehill") ?? throw new InvalidOperationException("Stone Hill level missing.");

    CrossLevelTemplateLevelStatus artisansKey = ResolveTemplateStatus(artisans, key);
    CrossLevelTemplateLevelStatus artisansKeyChest = ResolveTemplateStatus(artisans, keyChest);
    CrossLevelTemplateLevelStatus stoneHillSpring = ResolveTemplateStatus(stoneHillLevel, springChest);
    CrossLevelTemplateLevelStatus artisansSpring = ResolveTemplateStatus(artisans, springChest);
    CrossLevelTemplateLevelStatus toastyWizard = ResolveTemplateStatus(toasty, greenWizard);
    CrossLevelTemplateLevelStatus stoneHillWizard = ResolveTemplateStatus(stoneHillLevel, greenWizard);
    bool hasSourceDiscForCandidateChecks = File.Exists(sourceImage);

    if (!artisansKey.Ready || !artisansKey.Placeable || !string.Equals(artisansKey.ShortLabel, "ready", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Editor readiness should expose cross-level keys as lightweight ready objects.");
    if (hasSourceDiscForCandidateChecks)
    {
        if (artisansKeyChest.Ready ||
            !artisansKeyChest.Placeable ||
            !string.Equals(artisansKeyChest.ShortLabel, "candidate here", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(artisansKeyChest.RecipeId, "artisans.peacekeepers.lockedChest.package.safeGapActorId.v4", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Editor readiness should expose the Artisans Key Chest as a guarded candidate until in-game evidence passes.");
        }
        bool artisansKeyChestBundleCandidate = artisansKey.Ready && artisansKeyChest.Placeable && !artisansKeyChest.Ready;
        if (!artisansKeyChestBundleCandidate)
            throw new InvalidOperationException("Editor readiness should allow the Artisans Key + Key Chest bundle as a candidate pair without marking the chest proven.");
        if (stoneHillSpring.Ready ||
            !stoneHillSpring.Placeable ||
            !string.Equals(stoneHillSpring.ShortLabel, "candidate here", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(stoneHillSpring.RecipeId))
        {
            throw new InvalidOperationException("Editor readiness should keep the Peace Keepers Spring Chest as a guarded Stone Hill candidate after the latest in-game failure.");
        }
        if (artisansSpring.Ready ||
            !artisansSpring.Placeable ||
            !string.Equals(artisansSpring.ShortLabel, "candidate here", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Editor readiness should keep the Artisans Spring Chest candidate placeable but not normal-export ready.");
        }
        if (toastyWizard.Ready ||
            !toastyWizard.Placeable ||
            !string.Equals(toastyWizard.ShortLabel, "candidate here", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(toastyWizard.RecipeId, "toasty.wizardpeak.greenWizard.package.overwriteUnused00EA.v2", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Editor readiness should expose the Toasty Green Wizard transform as a guarded candidate until in-game evidence passes.");
        }
        if (stoneHillWizard.Ready ||
            stoneHillWizard.Placeable ||
            !string.Equals(stoneHillWizard.ShortLabel, "preview here", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Editor readiness should guard Green Wizard in unmapped levels like Stone Hill.");
        }
    }
    else
    {
        Console.WriteLine("Cross-level editor readiness: source disc not found; guarded candidate recipe assertions skipped for tester package.");
    }
    if (!CrossLevelEditorTemplateSupport.CanTransform(MobyVisualKind.Actor, "enemyTransform") ||
        CrossLevelEditorTemplateSupport.CanTransform(MobyVisualKind.Chest, "enemyTransform") ||
        CrossLevelEditorTemplateSupport.CanTransform(MobyVisualKind.Actor, "lockedChest") ||
        !CrossLevelEditorTemplateSupport.CanTransform(MobyVisualKind.Chest, "lockedChest"))
    {
        throw new InvalidOperationException("Editor transform filtering should keep enemy transforms on actors and chest transforms on chests.");
    }

    Console.WriteLine("Cross-level editor readiness: Spring Chest is diagnostic-only; Artisans Key Chest and Toasty Green Wizard remain guarded candidate tests.");

    JsonElement FindTemplate(IEnumerable<JsonElement> templates, string id)
    {
        foreach (JsonElement template in templates)
        {
            if (string.Equals(ReadJsonString(template, "id"), id, StringComparison.OrdinalIgnoreCase))
                return template;
        }

        throw new InvalidOperationException($"Missing cross-level editor template {id}.");
    }

    CrossLevelTemplateLevelStatus ResolveTemplateStatus(LevelDefinition level, JsonElement template)
    {
        return CrossLevelEditorTemplateSupport.ResolveLevelStatus(
            level,
            ReadJsonString(template, "sourceLevelKey"),
            ReadJsonString(template, "family"),
            ReadJsonString(template, "addSupportStatus"),
            workspace.RootPath);
    }
}

async Task ReportSpringChestDependencyAudit()
{
    if (!File.Exists(sourceImage))
    {
        Console.WriteLine("Spring Chest dependency audit: source disc not found; skipping.");
        return;
    }

    string outDir = Path.Combine(workspace.RootPath, "_local", "smoke");
    Directory.CreateDirectory(outDir);
    CrossLevelSpringChestDependencyReport report = CrossLevelSpringChestDependencyAnalyzer.Build(workspace, catalog, sourceImage);
    string jsonPath = Path.Combine(outDir, "spring-chest-dependency-audit.json");
    string markdownPath = Path.Combine(outDir, "spring-chest-dependency-audit.md");
    await CrossLevelSpringChestDependencyAnalyzer.WriteAsync(report, jsonPath, markdownPath);

    SpringChestLevelDependencySummary? stoneHillSummary = report.Levels.FirstOrDefault(level =>
        string.Equals(level.LevelKey, "stonehill", StringComparison.OrdinalIgnoreCase));
    SpringChestLevelDependencySummary[] nativeSummaries = report.Levels
        .Where(level => !string.Equals(level.LevelKey, "stonehill", StringComparison.OrdinalIgnoreCase))
        .ToArray();
    if (stoneHillSummary == null || stoneHillSummary.SpringChestRecordCount != 0 || stoneHillSummary.ActorId0149RecordCount != 0)
        throw new InvalidOperationException("Stone Hill should have no native spring chest records while the import remains guarded.");
    if (nativeSummaries.Length < 3 || nativeSummaries.Any(level => level.SpringChestRecordCount == 0 || level.ActorId0149RecordCount == 0))
        throw new InvalidOperationException("Spring Chest dependency audit did not find native spring chest examples in Town Square, Peace Keepers, and Dry Canyon.");
    if (nativeSummaries.Any(level => level.DistinctSourceSpecialDataHashes.Count == 0 || level.DistinctRuntimeSpecialDataPointers.Count == 0))
        throw new InvalidOperationException("Native spring chest examples should carry source special data and runtime special-data pointers.");

    Console.WriteLine($"Spring Chest dependency audit: native examples={nativeSummaries.Sum(level => level.SpringChestRecordCount)}, Stone Hill native records=0, report={markdownPath}");
}

async Task ReportSpringChestInteractionDiagnostic()
{
    if (!File.Exists(sourceImage))
    {
        Console.WriteLine("Spring Chest interaction diagnostic: source disc not found; skipping.");
        return;
    }

    string outDir = Path.Combine(workspace.RootPath, "_local", "smoke");
    Directory.CreateDirectory(outDir);
    SpringChestInteractionDiagnosticReport report = SpringChestInteractionDiagnostic.Build(workspace, catalog, sourceImage);
    string jsonPath = Path.Combine(outDir, "spring-chest-interaction-diagnostic.json");
    string markdownPath = Path.Combine(outDir, "spring-chest-interaction-diagnostic.md");
    await SpringChestInteractionDiagnostic.WriteAsync(report, jsonPath, markdownPath);

    if (report.LatestCandidate.BehaviorCorrect == true)
        throw new InvalidOperationException("The guarded Stone Hill Spring Chest candidate should not be marked behavior-correct until it works in-game.");
    if (report.NativeClusters.Count == 0)
        throw new InvalidOperationException("Spring Chest interaction diagnostic did not find native Spring Chest clusters to compare.");

    int comparedPairs = report.RamPairDiagnostics.Count(pair => string.Equals(pair.Status, "compared", StringComparison.OrdinalIgnoreCase));
    Console.WriteLine($"Spring Chest interaction diagnostic: native clusters={report.NativeClusters.Count}, RAM pairs compared={comparedPairs}, report={markdownPath}");
}

bool ReadJsonBool(JsonElement element, string name, bool fallback = false)
{
    if (!element.TryGetProperty(name, out JsonElement property))
        return fallback;

    return property.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => bool.TryParse(property.GetString(), out bool value) ? value : fallback,
        _ => fallback
    };
}

bool HasConflictingObservedMetadata(JsonElement item, Moby moby)
{
    int type = ReadJsonInt32(item, "typeHex", -1);
    if (type >= 0 && type != moby.Type)
        return true;

    int sourceByte36 = ReadJsonInt32(item, "sourceByte36Hex", -1);
    if (sourceByte36 >= 0 && sourceByte36 != moby.SourceByte36)
        return true;

    int sourceByte4F = ReadJsonInt32(item, "sourceByte4FHex", -1);
    if (sourceByte4F >= 0 && sourceByte4F != moby.SourceByte4F)
        return true;

    int flag4B = ReadJsonInt32(item, "flag4BHex", -1);
    return flag4B >= 0 && flag4B != moby.Flag4B;
}

string LevelKeyFromObservationFile(string path)
{
    string name = Path.GetFileName(path);
    foreach (string suffix in new[] { "-moby-user-overrides.json", "-live-validation-overrides.json" })
    {
        if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return name[..^suffix.Length];
    }

    return "";
}

IEnumerable<string> EnumerateObservationFiles()
{
    foreach (string searchRoot in new[] { workspace.RootPath, Path.Combine(workspace.RootPath, "support") }.Where(Directory.Exists))
    {
        foreach (string path in Directory.GetFiles(searchRoot, "*-moby-user-overrides.json"))
            yield return path;
        foreach (string path in Directory.GetFiles(searchRoot, "*-live-validation-overrides.json"))
            yield return path;
    }
}

IdentityFingerprintParts ParseIdentityFingerprint(string fingerprint)
{
    Dictionary<string, string> values = fingerprint
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(part => part.Split('=', 2))
        .Where(parts => parts.Length == 2)
        .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);

    return new IdentityFingerprintParts(
        values.TryGetValue("type", out string? type) ? type : "",
        values.TryGetValue("b36", out string? b36) ? b36 : "",
        values.TryGetValue("f4A", out string? f4A) ? f4A : "",
        values.TryGetValue("f4B", out string? f4B) ? f4B : "",
        values.TryGetValue("b4F", out string? b4F) ? b4F : "");
}

List<(LevelDefinition Level, Moby Moby)> SelectBatchSamples(
    PrecisionIdentityGroup group,
    IReadOnlyList<(LevelDefinition Level, Moby Moby)> allMobys)
{
    return allMobys
        .Where(item => string.Equals(IdentityFingerprint(item.Moby), group.Key, StringComparison.OrdinalIgnoreCase))
        .GroupBy(item => item.Level.Key, StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(levelGroup => levelGroup.Count())
        .ThenBy(levelGroup => levelGroup.First().Level.DisplayName)
        .FirstOrDefault()?
        .OrderBy(item => item.Moby.TrueIndex)
        .Take(4)
        .ToList() ?? new List<(LevelDefinition Level, Moby Moby)>();
}

bool CanWriteIdentityEditBatch(string lane, IReadOnlyList<(LevelDefinition Level, Moby Moby)> samples)
{
    return !string.Equals(lane, "control/helper", StringComparison.OrdinalIgnoreCase) &&
        samples.Count > 0 &&
        samples.All(sample => sample.Moby.TrueIndex >= 0) &&
        samples.Select(sample => sample.Level.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1;
}

bool CanWriteControlClusterIdentityEditBatch(string lane, IReadOnlyList<(LevelDefinition Level, Moby Moby)> samples)
{
    return string.Equals(lane, "control/helper", StringComparison.OrdinalIgnoreCase) &&
        samples.Count > 0 &&
        samples.All(sample => sample.Moby.TrueIndex >= 0) &&
        samples.Select(sample => sample.Level.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1;
}

async Task<string?> WriteControlClusterIdentityEditBatch(
    string batchDir,
    int batchNumber,
    PrecisionIdentityGroup group,
    IReadOnlyList<(LevelDefinition Level, Moby Moby)> samples,
    IReadOnlyList<(LevelDefinition Level, Moby Moby)> allMobys)
{
    (LevelDefinition level, Moby target) = samples[0];
    string fingerprint = group.Key;
    List<Moby> anchors = allMobys
        .Where(item => string.Equals(item.Level.Key, level.Key, StringComparison.OrdinalIgnoreCase))
        .Where(item => item.Moby.TrueIndex != target.TrueIndex)
        .Where(item => !string.Equals(IdentityFingerprint(item.Moby), fingerprint, StringComparison.OrdinalIgnoreCase))
        .Where(item => IsExactIdentity(item.Moby))
        .Where(item => item.Moby.VisualKind is not MobyVisualKind.Control and not MobyVisualKind.Unknown)
        .Select(item => new
        {
            item.Moby,
            Distance = Math.Sqrt(DistanceSquared(item.Moby.OriginalPosition, target.OriginalPosition))
        })
        .Where(item => item.Distance <= 896)
        .OrderBy(item => item.Distance)
        .ThenBy(item => item.Moby.TrueIndex)
        .Take(3)
        .Select(item => item.Moby)
        .ToList();

    if (anchors.Count == 0)
        return null;

    Vector3f delta = new(384, 0, 96);
    List<Moby> edited = new() { target };
    edited.AddRange(anchors);
    List<(Moby Moby, Vector3f Position, string Label)> restore = edited
        .Select(moby => (moby, moby.Position, moby.Label))
        .ToList();

    try
    {
        target.Position = OffsetPosition(target.OriginalPosition, delta);
        target.Label = $"Control cluster {batchNumber}: {group.PreferredLabelForLevel(level.Key)}";
        for (int i = 0; i < anchors.Count; i++)
        {
            Moby anchor = anchors[i];
            anchor.Position = OffsetPosition(anchor.OriginalPosition, delta);
            anchor.Label = $"Cluster {batchNumber} anchor {i + 1}: {anchor.DisplayLabel}";
        }

        string editManifest = Path.Combine(batchDir, $"{level.Key}-identity-batch-{batchNumber:00}-control-cluster-native-edits.json");
        await MobyEditStore.SaveAsync(editManifest, edited, $"{level.DisplayName} control identity cluster {batchNumber}");
        return editManifest;
    }
    finally
    {
        foreach ((Moby moby, Vector3f position, string label) in restore)
        {
            moby.Position = position;
            moby.Label = label;
        }
    }
}

Vector3f OffsetPosition(Vector3f position, Vector3f delta)
{
    return new Vector3f(position.X + delta.X, position.Y + delta.Y, position.Z + delta.Z);
}

string IdentityFingerprint(Moby moby)
{
    return $"type=0x{moby.Type:X2} b36=0x{moby.SourceByte36:X2} f4A=0x{moby.Flag4A:X2} f4B=0x{moby.Flag4B:X2} b4F=0x{moby.SourceByte4F:X2}";
}

string IdentityTestLane(PrecisionIdentityGroup group)
{
    string text = $"{group.Label} {group.Kind} {group.Key}".ToLowerInvariant();
    if (text.Contains("special object 0x40") || text.Contains("special object 0x50") || text.Contains("special object 0x60") ||
        text.Contains("type=0x40") || text.Contains("type=0x50") || text.Contains("type=0x60"))
        return "flight/special object";
    if (text.Contains("nonvisual") || text.Contains("control") || text.Contains("helper") || text.Contains("type=0x00"))
        return "control/helper";
    if (text.Contains("actor") || text.Contains("enemy") || text.Contains("container") || text.Contains("f4b=0x10") || text.Contains("f4b=0x54") || text.Contains("f4b=0x55") || text.Contains("f4b=0x56"))
        return "actor or interactive object";
    if (text.Contains("scenery") || text.Contains("prop") || text.Contains("f4b=0xff"))
        return "visual scenery/prop";
    return "unknown visual candidate";
}

string IdentityTestRecipe(PrecisionIdentityGroup group, string lane)
{
    PrecisionIdentitySample first = group.Samples.FirstOrDefault() ?? new PrecisionIdentitySample("", "", -1, 0, 0, 0);
    return IdentityTestRecipeForSample(lane, first);
}

string IdentityTestRecipeForSample(string lane, PrecisionIdentitySample first)
{
    string target = first.TrueIndex >= 0 ? $"{first.LevelName} T{first.TrueIndex}" : "one sample";
    return lane switch
    {
        "visual scenery/prop" => $"Move {target} and one nearby same-fingerprint sample to a clear flat spot; if the same model appears twice, promote the fingerprint to that prop name.",
        "actor or interactive object" => $"Move {target} to a safe test row; watch model, movement, attack/pickup/chest behavior, and reward drop before naming it.",
        "control/helper" => $"Do not name from solo movement alone. Move {target} only with its nearest visible cluster and compare camera, portal, rescue, or trigger behavior.",
        "flight/special object" => $"Test {target} in its flight/special level and observe whether it is a gate, plane/train piece, timer number, arch, or route marker.",
        _ => $"Move {target} to a visible test row and capture one screenshot plus before/after note."
    };
}

bool IsExactIdentity(Moby moby)
{
    string text = $"{moby.Label} {moby.CandidateKind} {moby.Confidence} {moby.Evidence}";
    string lower = text.ToLowerInvariant();
    if (text.Contains("?", StringComparison.Ordinal) ||
        lower.Contains("unknown") ||
        lower.Contains("placeholder") ||
        lower.Contains(" candidate") ||
        lower.Contains("helper/system") ||
        lower.Contains("helper or engine"))
        return false;

    return moby.Confidence.Contains("byte-pattern", StringComparison.OrdinalIgnoreCase) ||
        moby.Confidence.Contains("user", StringComparison.OrdinalIgnoreCase) ||
        moby.Confidence.Contains("live", StringComparison.OrdinalIgnoreCase) ||
        moby.Confidence.Contains("observed", StringComparison.OrdinalIgnoreCase) ||
        moby.Confidence.Contains("validated", StringComparison.OrdinalIgnoreCase);
}

bool IsQuestionableIdentity(Moby moby)
{
    if (IsExactIdentity(moby))
        return false;

    string text = $"{moby.Label} {moby.CandidateKind} {moby.Confidence} {moby.Evidence}".ToLowerInvariant();
    return text.Contains("?") ||
        text.Contains("unknown") ||
        text.Contains("placeholder") ||
        text.Contains("helper/system") ||
        text.Contains("helper or engine") ||
        text.Contains(" candidate") ||
        text.Contains("needs live validation") ||
        text.Contains("needs per-level confirmation") ||
        text.Contains("exact model") ||
        text.Contains("until live-tested") ||
        text.Contains("visible object not confirmed") ||
        text.Contains("actor, container, or interactive object");
}

string EscapeMarkdown(string value)
{
    return value
        .Replace("|", "\\|", StringComparison.Ordinal)
        .Replace("\r", " ", StringComparison.Ordinal)
        .Replace("\n", " ", StringComparison.Ordinal);
}

void ReportSyntheticChestContentLink(string levelKey, List<Moby> mobys)
{
    Moby? chest = mobys.FirstOrDefault(moby => moby.IsChest);
    if (chest == null)
        return;

    int trueIndex = mobys.Max(moby => moby.TrueIndex) + 1;
    Moby added = new()
    {
        Index = mobys.Max(moby => moby.Index) + 1,
        TrueIndex = trueIndex,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(trueIndex),
        Position = chest.Position,
        OriginalPosition = chest.Position,
        Type = 0,
        OriginalType = 0,
        State = 0,
        OriginalState = 0,
        SourceByte36 = 0,
        OriginalSourceByte36 = 0,
        SourceByte37 = 0,
        SourceByte4F = 0,
        OriginalSourceByte4F = 0,
        Flag4A = 0xFF,
        Flag4B = GemValue.Purple.IdByte,
        OriginalFlag4B = GemValue.Purple.IdByte,
        Color = GemValue.Purple.Color,
        Label = $"Locked chest content: {GemValue.Purple.DisplayName}",
        OriginalLabel = $"Locked chest content: {GemValue.Purple.DisplayName}",
        IsAdded = true
    };
    mobys.Add(added);
    int repaired = MobyRelationshipRepair.RepairChestContentLinks(levelKey, mobys);
    bool linked = chest.Links.Any(link =>
        string.Equals(link.Kind, "chest contents", StringComparison.OrdinalIgnoreCase) &&
        link.TrueIndexes.Contains(added.TrueIndex));
    added.IsRemoved = true;
    MobyRelationshipRepair.RepairChestContentLinks(levelKey, mobys);
    bool removedLinked = chest.Links.Any(link =>
        string.Equals(link.Kind, "chest contents", StringComparison.OrdinalIgnoreCase) &&
        link.TrueIndexes.Contains(added.TrueIndex));
    Console.WriteLine($"{levelKey} synthetic chest content: repaired {repaired} link(s), added T{added.TrueIndex} linked={linked}, removedLinked={removedLinked}");
}

async Task ReportChestContentSourcePatch(string levelKey)
{
    if (!File.Exists(sourceImage))
        return;

    LevelDefinition? level = catalog.FindByKey(levelKey);
    if (level == null || !level.HasSourceTable)
        return;

    string mobyPath = Path.Combine(workspace.RootPath, "editor-cache", $"{levelKey}-mobys.json");
    if (!File.Exists(mobyPath))
        return;

    List<Moby> mobys = MobyLoader.LoadCached(mobyPath).ToList();
    MobyMetadataEnricher.Apply(workspace, levelKey, mobys);
    MobyRelationshipRepair.RepairChestContentLinks(levelKey, mobys);
    Moby? content = mobys.FirstOrDefault(moby => moby.IsChestContent && moby.TrueIndex >= 0 && moby.TrueIndex < level.SourceRecordCount);
    if (content == null)
    {
        Console.WriteLine($"{levelKey} chest content source patch: no source-table content marker found.");
        return;
    }

    int editedGemId = content.Gem.IdByte == GemValue.Purple.IdByte ? GemValue.Green.IdByte : GemValue.Purple.IdByte;
    Moby edited = new()
    {
        Index = content.Index,
        TrueIndex = content.TrueIndex,
        LegacyIndex = content.LegacyIndex,
        Position = content.Position,
        OriginalPosition = content.OriginalPosition,
        Type = content.Type,
        OriginalType = content.OriginalType,
        State = content.State,
        OriginalState = content.OriginalState,
        RuntimeAddress = content.RuntimeAddress,
        SpecialDataPointer = content.SpecialDataPointer,
        SourceByte36 = content.SourceByte36,
        OriginalSourceByte36 = content.OriginalSourceByte36,
        SourceByte37 = content.SourceByte37,
        SourceByte4F = content.SourceByte4F,
        OriginalSourceByte4F = content.OriginalSourceByte4F,
        Flag4A = content.Flag4A,
        Flag4B = editedGemId,
        OriginalFlag4B = content.OriginalFlag4B,
        Color = content.Color,
        Label = content.DisplayLabel,
        OriginalLabel = content.OriginalLabel,
        PatchStatus = "smoke",
        PatchLead = "contained-gem-id-byte"
    };

    Directory.CreateDirectory(Path.Combine(workspace.RootPath, "_local", "smoke"));
    string path = Path.Combine(workspace.RootPath, "_local", "smoke", $"{levelKey}-chest-content-source-byte-native-edits.json");
    await MobyEditStore.SaveAsync(path, [edited], $"{level.DisplayName} chest content smoke");
    MobySourcePatchPlan plan = MobySourcePatchExporter.BuildPlan(
        sourceImage,
        DiscImageLocator.FindCueForImage(sourceImage),
        Path.Combine(workspace.RootPath, "_local", "objects", $"{levelKey}-chest-content-smoke.bin"),
        Path.Combine(workspace.RootPath, "_local", "objects", $"{levelKey}-chest-content-smoke.cue"),
        level,
        path);
    MobySourcePatch? containedPatch = plan.Patches.FirstOrDefault(patch => patch.Kind.Contains("contained-gem", StringComparison.OrdinalIgnoreCase));
    Console.WriteLine($"{levelKey} chest content source patch: {plan.PatchCount} patch(es), contained byte patch={(containedPatch != null)}, T{content.TrueIndex}");
    await ReportAddedChestContentSourcePatch(level, levelKey, mobys);
}

async Task ReportAddedChestContentSourcePatch(LevelDefinition level, string levelKey, List<Moby> mobys)
{
    Moby? chest = mobys.FirstOrDefault(moby => moby.IsChest && moby.TrueIndex >= 0 && moby.TrueIndex < level.SourceRecordCount);
    if (chest == null)
    {
        Console.WriteLine($"{levelKey} added chest content source patch: no source-table chest found.");
        return;
    }

    int trueIndex = mobys.Max(moby => moby.TrueIndex) + 1;
    Moby added = new()
    {
        Index = mobys.Max(moby => moby.Index) + 1,
        TrueIndex = trueIndex,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(trueIndex),
        Position = chest.Position,
        OriginalPosition = chest.Position,
        Type = 0,
        OriginalType = 0,
        State = 0,
        OriginalState = 0,
        SourceByte36 = 0,
        OriginalSourceByte36 = 0,
        SourceByte37 = 0,
        SourceByte4F = 0,
        OriginalSourceByte4F = 0,
        Flag4A = 0xFF,
        Flag4B = GemValue.Yellow.IdByte,
        OriginalFlag4B = GemValue.Yellow.IdByte,
        Color = GemValue.Yellow.Color,
        Label = $"Locked chest content: {GemValue.Yellow.DisplayName}",
        OriginalLabel = $"Locked chest content: {GemValue.Yellow.DisplayName}",
        CandidateKind = "locked chest contained yellow reward marker",
        Confidence = "smoke",
        Evidence = "smoke",
        IsAdded = true
    };
    mobys.Add(added);
    MobyRelationshipRepair.RepairChestContentLinks(levelKey, mobys);

    string path = Path.Combine(workspace.RootPath, "_local", "smoke", $"{levelKey}-added-chest-content-native-edits.json");
    await MobyEditStore.SaveAsync(path, mobys, $"{level.DisplayName} added chest content smoke");
    MobySourcePatchPlan plan = MobySourcePatchExporter.BuildPlan(
        sourceImage,
        DiscImageLocator.FindCueForImage(sourceImage),
        Path.Combine(workspace.RootPath, "_local", "objects", $"{levelKey}-added-chest-content-smoke.bin"),
        Path.Combine(workspace.RootPath, "_local", "objects", $"{levelKey}-added-chest-content-smoke.cue"),
        level,
        path);
    bool hasRecordAppend = plan.Patches.Any(patch => patch.Kind == "moby-record-append");
    bool hasSpecialAppend = plan.Patches.Any(patch => patch.Kind == "moby-special-data-append");
    bool hasCount = plan.Patches.Any(patch => patch.Kind == "moby-source-count");
    Console.WriteLine($"{levelKey} added chest content source patch: {plan.PatchCount} patch(es), record={hasRecordAppend}, special={hasSpecialAppend}, count={hasCount}, skipped={plan.SkippedEdits.Count}");
}

async Task ReportLockedChestShellRewardGuard(string levelKey)
{
    if (!File.Exists(sourceImage))
        return;

    LevelDefinition? level = catalog.FindByKey(levelKey);
    if (level == null || !level.HasSourceTable)
        return;

    string mobyPath = Path.Combine(workspace.RootPath, "editor-cache", $"{levelKey}-mobys.json");
    if (!File.Exists(mobyPath))
        return;

    List<Moby> mobys = MobyLoader.LoadCached(mobyPath).ToList();
    MobyMetadataEnricher.Apply(workspace, levelKey, mobys);
    MobyRelationshipRepair.RepairChestContentLinks(levelKey, mobys);
    Moby? chest = mobys.FirstOrDefault(moby => moby.IsLockedChestShell && moby.TrueIndex >= 0 && moby.TrueIndex < level.SourceRecordCount);
    if (chest == null)
    {
        Console.WriteLine($"{levelKey} locked chest guard: no locked chest shell found.");
        return;
    }

    Moby edited = new()
    {
        Index = chest.Index,
        TrueIndex = chest.TrueIndex,
        LegacyIndex = chest.LegacyIndex,
        Position = chest.Position,
        OriginalPosition = chest.OriginalPosition,
        Type = chest.Type,
        OriginalType = chest.OriginalType,
        State = chest.State,
        OriginalState = chest.OriginalState,
        RuntimeAddress = chest.RuntimeAddress,
        SpecialDataPointer = chest.SpecialDataPointer,
        SourceByte36 = chest.SourceByte36,
        OriginalSourceByte36 = chest.OriginalSourceByte36,
        SourceByte37 = chest.SourceByte37,
        SourceByte4F = chest.SourceByte4F,
        OriginalSourceByte4F = chest.OriginalSourceByte4F,
        Flag4A = chest.Flag4A,
        Flag4B = chest.Flag4B == GemValue.Purple.IdByte ? GemValue.Green.IdByte : GemValue.Purple.IdByte,
        OriginalFlag4B = chest.OriginalFlag4B,
        Color = chest.Color,
        Label = chest.DisplayLabel,
        OriginalLabel = chest.OriginalLabel,
        CandidateKind = chest.CandidateKind,
        Confidence = "smoke",
        Evidence = "locked chest shell guard"
    };

    string path = Path.Combine(workspace.RootPath, "_local", "smoke", $"{levelKey}-locked-chest-shell-guard-native-edits.json");
    await MobyEditStore.SaveAsync(path, [edited], $"{level.DisplayName} locked chest shell guard");
    using FileStream stream = File.OpenRead(path);
    using JsonDocument document = JsonDocument.Parse(stream);
    JsonElement edit = document.RootElement.GetProperty("edits")[0];
    int sourceByteEditCount = edit.TryGetProperty("sourceByteEdits", out JsonElement sourceByteEdits) && sourceByteEdits.ValueKind == JsonValueKind.Array
        ? sourceByteEdits.GetArrayLength()
        : 0;
    if (sourceByteEditCount != 0)
        throw new InvalidOperationException($"{levelKey} locked chest shell wrote {sourceByteEditCount} source byte edit(s); shell reward bytes must stay untouched.");

    MobySourcePatchPlan plan = MobySourcePatchExporter.BuildPlan(
        sourceImage,
        DiscImageLocator.FindCueForImage(sourceImage),
        Path.Combine(workspace.RootPath, "_local", "objects", $"{levelKey}-locked-chest-shell-guard-smoke.bin"),
        Path.Combine(workspace.RootPath, "_local", "objects", $"{levelKey}-locked-chest-shell-guard-smoke.cue"),
        level,
        path);
    bool patchedShellReward = plan.Patches.Any(patch => patch.TrueIndex == chest.TrueIndex && string.Equals(patch.RecordOffset, "0x53", StringComparison.OrdinalIgnoreCase));
    if (patchedShellReward)
        throw new InvalidOperationException($"{levelKey} locked chest shell exported a +0x53 patch; edit the contained gem records instead.");

    Console.WriteLine($"{levelKey} locked chest shell guard: T{chest.TrueIndex} shell protected, source byte edits={sourceByteEditCount}, patch count={plan.PatchCount}");
}

void ReportDragonLinks(string levelKey)
{
    string mobyPath = Path.Combine(workspace.RootPath, "editor-cache", $"{levelKey}-mobys.json");
    if (!File.Exists(mobyPath))
        return;

    List<Moby> mobys = MobyLoader.LoadCached(mobyPath).ToList();
    MobyMetadataResult metadata = MobyMetadataEnricher.Apply(workspace, levelKey, mobys);
    List<MobyLink> dragonLinks = mobys
        .SelectMany(moby => moby.Links)
        .Where(link => string.Equals(link.Kind, "dragon scene", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(link.Kind, "dragon pedestal", StringComparison.OrdinalIgnoreCase))
        .GroupBy(link => link.Key, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.First())
        .ToList();
    List<string> broadVisibleDragonLinks = mobys
        .Where(IsDragonSelectionMoby)
        .Select(moby => new
        {
            Moby = moby,
            Links = MobyLinkTraversal.GetVisibleLinks(moby).ToList(),
            Visible = MobyLinkTraversal.GetVisibleLinkedTrueIndexes(moby)
        })
        .Where(item => item.Visible.Count > 3 || item.Links.Any(link => link.Name.Contains("cluster", StringComparison.OrdinalIgnoreCase)))
        .Select(item => $"T{item.Moby.TrueIndex}->{string.Join(",", item.Visible.Order())}")
        .ToList();
    if (broadVisibleDragonLinks.Count > 0)
        throw new InvalidOperationException($"{levelKey} dragon selection still exposes broad link cluster(s): {string.Join("; ", broadVisibleDragonLinks)}");

    int linksWithSupport = dragonLinks.Count(link => link.TrueIndexes.Count > 2);
    Console.WriteLine($"{levelKey} dragon links: {dragonLinks.Count} group(s), {linksWithSupport} with camera/helper support, {metadata.InferredLinkGroups} inferred link(s)");
}

bool IsDragonSelectionMoby(Moby moby)
{
    string text = $"{moby.Label} {moby.CandidateKind} {moby.Confidence} {moby.Evidence}";
    return text.Contains("dragon", StringComparison.OrdinalIgnoreCase) &&
        !text.Contains("pedestal", StringComparison.OrdinalIgnoreCase) &&
        !text.Contains("helper", StringComparison.OrdinalIgnoreCase) &&
        !text.Contains("camera", StringComparison.OrdinalIgnoreCase);
}

async Task ReportAddedMobyRoundTrip(List<Moby> sourceMobys)
{
    Directory.CreateDirectory(Path.Combine(workspace.RootPath, "_local", "smoke"));
    string path = Path.Combine(workspace.RootPath, "_local", "smoke", "moby-add-roundtrip-native-edits.json");
    int nextIndex = sourceMobys.Max(moby => moby.Index) + 1;
    int nextTrueIndex = sourceMobys.Max(moby => moby.TrueIndex) + 1;
    Moby added = new()
    {
        Index = nextIndex,
        TrueIndex = nextTrueIndex,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(nextTrueIndex),
        Position = new Vector3f(12, 34, 56),
        OriginalPosition = new Vector3f(12, 34, 56),
        Type = 0x18,
        OriginalType = 0x18,
        State = 0,
        OriginalState = 0,
        SourceByte36 = 0x55,
        OriginalSourceByte36 = 0x55,
        SourceByte4F = 0x03,
        OriginalSourceByte4F = 0x03,
        Flag4A = 0x40,
        Flag4B = 0xFF,
        OriginalFlag4B = 0xFF,
        Color = GemValue.Blue.Color,
        Label = "Smoke added blue gem",
        OriginalLabel = "Smoke added blue gem",
        PatchStatus = "smoke",
        PatchLead = "roundtrip",
        IsAdded = true
    };

    List<Moby> saveMobys = [added];
    await MobyEditStore.SaveAsync(path, saveMobys, "Smoke");

    List<Moby> loadedMobys = [];
    int applied = MobyEditStore.Load(path, loadedMobys);
    Moby? loadedAdded = loadedMobys.FirstOrDefault(moby => moby.TrueIndex == nextTrueIndex);
    Console.WriteLine(loadedAdded == null
        ? "Moby add roundtrip: failed"
        : $"Moby add roundtrip: {applied} edit(s), loaded {loadedAdded.DisplayLabel} at T{loadedAdded.TrueIndex}");
}

async Task ReportMobyIdentityBytePatchPlan(LevelDefinition level, List<Moby> sourceMobys)
{
    if (!File.Exists(sourceImage))
    {
        Console.WriteLine("Moby identity byte patch: source disc not found; skipping export smoke.");
        return;
    }

    Moby? donor = sourceMobys.FirstOrDefault(moby => moby.TrueIndex >= 0 && moby.TrueIndex < level.SourceRecordCount);
    if (donor == null)
    {
        Console.WriteLine("Moby identity byte patch: no source-table moby found.");
        return;
    }

    Moby edited = CloneMoby(donor);
    edited.SourceByte37 = (donor.SourceByte37 + 1) & 0xFF;
    edited.Flag4A = donor.Flag4A == 0x10 ? 0x20 : 0x10;
    edited.Label = $"{donor.DisplayLabel} identity-byte smoke";

    string path = Path.Combine(workspace.RootPath, "_local", "smoke", "moby-identity-byte-native-edits.json");
    await MobyEditStore.SaveAsync(path, [edited], "Identity byte smoke");
    MobySourcePatchPlan plan = MobySourcePatchExporter.BuildPlan(
        sourceImage,
        DiscImageLocator.FindCueForImage(sourceImage),
        Path.Combine(workspace.RootPath, "_local", "objects", "identity-byte-smoke.bin"),
        Path.Combine(workspace.RootPath, "_local", "objects", "identity-byte-smoke.cue"),
        level,
        path);
    bool has37 = plan.Patches.Any(patch => patch.TrueIndex == donor.TrueIndex && string.Equals(patch.RecordOffset, "0x37", StringComparison.OrdinalIgnoreCase));
    bool has52 = plan.Patches.Any(patch => patch.TrueIndex == donor.TrueIndex && string.Equals(patch.RecordOffset, "0x52", StringComparison.OrdinalIgnoreCase));
    if (!has37 || !has52)
        throw new InvalidOperationException($"Moby identity byte patch missing expected offsets: +0x37={has37}, +0x52={has52}.");

    Console.WriteLine($"Moby identity byte patch: T{donor.TrueIndex} writes +0x37 and +0x52 for transform/export support");
}

void AddSpringChestRuntimeArmedSourceByteEdits(string editManifestPath)
{
    JsonNode? root = JsonNode.Parse(File.ReadAllText(editManifestPath));
    JsonArray? edits = root?["edits"] as JsonArray;
    if (root == null || edits == null)
        throw new InvalidOperationException("Spring Chest runtime-armed manifest did not contain an edits array.");

    foreach (JsonNode? editNode in edits)
    {
        if (editNode is not JsonObject edit)
            continue;

        int sourceTrueIndex = edit["crossLevelTemplate"]?["sourceTrueIndex"]?.GetValue<int>() ?? -1;
        int runtimeVariant = sourceTrueIndex switch
        {
            70 => 0x01A6,
            71 => 0x01A7,
            _ => -1
        };
        if (runtimeVariant < 0)
            continue;

        JsonArray sourceByteEdits = edit["sourceByteEdits"] as JsonArray ?? [];
        edit["sourceByteEdits"] = sourceByteEdits;
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x34, runtimeVariant & 0xFF, "spring-runtime-variant-low");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x35, (runtimeVariant >> 8) & 0xFF, "spring-runtime-variant-high");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x25, 0xF0, "spring-live-prehit-byte-25");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x29, 0x10, "spring-live-prehit-byte-29");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x2D, 0x10, "spring-live-prehit-byte-2D");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x41, 0x00, "spring-live-prehit-byte-41");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x49, 0x01, "spring-runtime-armed-byte");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x4C, 0x30, "spring-live-prehit-byte-4C");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x4D, 0x00, "spring-live-prehit-byte-4D");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x52, 0x10, "spring-runtime-flag4A-byte");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x55, 0x20, "spring-live-prehit-byte-55");
    }

    File.WriteAllText(editManifestPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
}

void AddSpringChestBluePreHitSourceByteEdits(string editManifestPath)
{
    JsonNode? root = JsonNode.Parse(File.ReadAllText(editManifestPath));
    JsonArray? edits = root?["edits"] as JsonArray;
    if (root == null || edits == null)
        throw new InvalidOperationException("Spring Chest blue pre-hit manifest did not contain an edits array.");

    foreach (JsonNode? editNode in edits)
    {
        if (editNode is not JsonObject edit)
            continue;

        int sourceTrueIndex = edit["crossLevelTemplate"]?["sourceTrueIndex"]?.GetValue<int>() ?? -1;
        int runtimeVariant = sourceTrueIndex switch
        {
            70 => 0x01A6,
            71 => 0x01A7,
            _ => -1
        };
        if (runtimeVariant < 0)
            continue;

        JsonArray sourceByteEdits = edit["sourceByteEdits"] as JsonArray ?? [];
        edit["sourceByteEdits"] = sourceByteEdits;
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x34, runtimeVariant & 0xFF, "spring-runtime-variant-low");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x35, (runtimeVariant >> 8) & 0xFF, "spring-runtime-variant-high");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x52, 0x10, "spring-blue-prehit-flag4A-byte");
    }

    File.WriteAllText(editManifestPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
}

void AddSpringChestNativePreHitShellLifecycleSourceByteEdits(string editManifestPath)
{
    JsonNode? root = JsonNode.Parse(File.ReadAllText(editManifestPath));
    JsonArray? edits = root?["edits"] as JsonArray;
    if (root == null || edits == null)
        throw new InvalidOperationException("Spring Chest native pre-hit lifecycle manifest did not contain an edits array.");

    foreach (JsonNode? editNode in edits)
    {
        if (editNode is not JsonObject edit)
            continue;

        int sourceTrueIndex = edit["crossLevelTemplate"]?["sourceTrueIndex"]?.GetValue<int>() ?? -1;
        int runtimeVariant = sourceTrueIndex switch
        {
            70 => 0x01A6,
            71 => 0x01A7,
            113 => 0x01A6,
            _ => -1
        };
        if (runtimeVariant < 0)
            continue;

        JsonArray sourceByteEdits = edit["sourceByteEdits"] as JsonArray ?? [];
        edit["sourceByteEdits"] = sourceByteEdits;
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x34, runtimeVariant & 0xFF, "spring-runtime-variant-low");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x35, (runtimeVariant >> 8) & 0xFF, "spring-runtime-variant-high");
        if (sourceTrueIndex == 113)
        {
            AddOrReplaceSourceByteEdit(sourceByteEdits, 0x38, 0x00, "spring-native-d8-companion-w38-0");
            AddOrReplaceSourceByteEdit(sourceByteEdits, 0x39, 0x00, "spring-native-d8-companion-w38-1");
            AddOrReplaceSourceByteEdit(sourceByteEdits, 0x3A, 0x7F, "spring-native-d8-companion-w38-2");
            AddOrReplaceSourceByteEdit(sourceByteEdits, 0x3B, 0x00, "spring-native-d8-companion-w38-3");
            AddOrReplaceSourceByteEdit(sourceByteEdits, 0x48, 0x00, "spring-native-d8-companion-w48-0");
            AddOrReplaceSourceByteEdit(sourceByteEdits, 0x49, 0x00, "spring-native-d8-companion-w48-1");
            AddOrReplaceSourceByteEdit(sourceByteEdits, 0x4A, 0xFF, "spring-native-d8-companion-w48-2");
            AddOrReplaceSourceByteEdit(sourceByteEdits, 0x4B, 0x0C, "spring-native-d8-companion-w48-3");
            AddOrReplaceSourceByteEdit(sourceByteEdits, 0x4C, 0x00, "spring-native-d8-companion-w4C-0");
            AddOrReplaceSourceByteEdit(sourceByteEdits, 0x4D, 0x00, "spring-native-d8-companion-w4C-1");
            AddOrReplaceSourceByteEdit(sourceByteEdits, 0x4E, 0x00, "spring-native-d8-companion-w4C-2");
            AddOrReplaceSourceByteEdit(sourceByteEdits, 0x4F, 0x00, "spring-native-d8-companion-w4C-3");
            AddOrReplaceSourceByteEdit(sourceByteEdits, 0x50, 0x20, "spring-native-d8-companion-w50-0");
            AddOrReplaceSourceByteEdit(sourceByteEdits, 0x51, 0x00, "spring-native-d8-companion-w50-1");
            AddOrReplaceSourceByteEdit(sourceByteEdits, 0x52, 0x10, "spring-native-d8-companion-w50-2");
            AddOrReplaceSourceByteEdit(sourceByteEdits, 0x53, 0x55, "spring-native-d8-companion-w50-3");
            AddOrReplaceSourceByteEdit(sourceByteEdits, 0x54, 0x7F, "spring-native-d8-companion-w54-0");
            AddOrReplaceSourceByteEdit(sourceByteEdits, 0x55, 0x10, "spring-native-d8-companion-w54-1");
            AddOrReplaceSourceByteEdit(sourceByteEdits, 0x56, 0x00, "spring-native-d8-companion-w54-2");
            AddOrReplaceSourceByteEdit(sourceByteEdits, 0x57, 0x00, "spring-native-d8-companion-w54-3");
            continue;
        }

        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x38, 0x00, "spring-native-prehit-w38-0");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x39, 0x00, "spring-native-prehit-w38-1");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x3A, 0x7D, "spring-native-prehit-w38-2");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x3B, 0x00, "spring-native-prehit-w38-3");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x48, 0x00, "spring-native-prehit-w48-0");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x49, 0x00, "spring-native-prehit-w48-1");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x4A, 0xFF, "spring-native-prehit-w48-2");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x4B, 0x90, "spring-native-prehit-w48-3");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x4C, 0x80, "spring-native-prehit-w4C-0");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x4D, 0x0C, "spring-native-prehit-w4C-1");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x4E, 0xA0, "spring-native-prehit-w4C-2");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x4F, 0x00, "spring-native-prehit-w4C-3");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x50, 0x20, "spring-native-prehit-w50-0");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x51, 0x00, "spring-native-prehit-w50-1");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x52, 0xFF, "spring-native-prehit-w50-2");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x53, 0x55, "spring-native-prehit-w50-3");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x54, 0x7F, "spring-native-prehit-w54-0");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x55, 0x10, "spring-native-prehit-w54-1");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x56, 0x00, "spring-native-prehit-w54-2");
        AddOrReplaceSourceByteEdit(sourceByteEdits, 0x57, 0x00, "spring-native-prehit-w54-3");
    }

    File.WriteAllText(editManifestPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
}

void AddOrReplaceSourceByteEdit(JsonArray sourceByteEdits, int offset, int value, string field)
{
    for (int i = sourceByteEdits.Count - 1; i >= 0; i--)
    {
        if (sourceByteEdits[i]?["offset"]?.GetValue<int>() == offset)
            sourceByteEdits.RemoveAt(i);
    }

    sourceByteEdits.Add(new JsonObject
    {
        ["offset"] = offset,
        ["offsetHex"] = $"0x{offset:X2}",
        ["value"] = value,
        ["valueHex"] = $"0x{value:X2}",
        ["field"] = field
    });
}

async Task ReportCrossLevelMobyTemplateRoundTripAndPatch(LevelDefinition level, List<Moby> sourceMobys)
{
    Directory.CreateDirectory(Path.Combine(workspace.RootPath, "_local", "smoke"));
    string path = Path.Combine(workspace.RootPath, "_local", "smoke", "moby-cross-level-template-native-edits.json");
    int nextIndex = sourceMobys.Max(moby => moby.Index) + 1;
    int nextTrueIndex = sourceMobys.Max(moby => moby.TrueIndex) + 1;
    Moby existingTransformDonor = sourceMobys.First(moby => moby.TrueIndex >= 0 && moby.TrueIndex < level.SourceRecordCount);
    Vector3f position = existingTransformDonor.OriginalPosition;
    Moby key = new()
    {
        Index = nextIndex,
        TrueIndex = nextTrueIndex,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(nextTrueIndex),
        Position = position,
        OriginalPosition = position,
        Type = 0x18,
        OriginalType = 0x18,
        State = 0,
        OriginalState = 0,
        SourceByte36 = 0xAD,
        OriginalSourceByte36 = 0xAD,
        SourceByte37 = 0,
        SourceByte4F = 0x02,
        OriginalSourceByte4F = 0x02,
        Flag4A = 0x40,
        Flag4B = 0xFF,
        OriginalFlag4B = 0xFF,
        Color = GemValue.Yellow.Color,
        Label = "Key",
        OriginalLabel = "Key",
        PatchStatus = "supported-lightweight-object",
        PatchLead = "cross-level key smoke",
        CrossLevelTemplateId = "common.key.peacekeepers.t78",
        CrossLevelFamily = "key",
        CrossLevelSourceLevelKey = "PeaceKeepers",
        CrossLevelSourceLevelName = "Peace Keepers",
        CrossLevelSourceTrueIndex = 78,
        CrossLevelRequiredExporterFeature = "DirectSourceRecordAppend",
        IsAdded = true
    };
    Moby springController = new()
    {
        Index = nextIndex + 1,
        TrueIndex = nextTrueIndex + 1,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(nextTrueIndex + 1),
        Position = new Vector3f(position.X + 16, position.Y, position.Z),
        OriginalPosition = new Vector3f(position.X + 16, position.Y, position.Z),
        Type = 0x18,
        OriginalType = 0x18,
        State = 0,
        OriginalState = 0,
        SourceByte36 = 0xC2,
        OriginalSourceByte36 = 0xC2,
        SourceByte37 = 0x00,
        OriginalSourceByte37 = 0x00,
        SourceByte4F = 0,
        OriginalSourceByte4F = 0,
        Flag4A = 0xFF,
        Flag4B = 0x53,
        OriginalFlag4B = 0x53,
        Color = Moby.ColorForType(0x20),
        Label = "Spring Chest controller",
        OriginalLabel = "Spring Chest controller",
        CandidateKind = "Control",
        BehaviorNote = "Hidden paired controller for the visible Spring Chest shell.",
        PatchStatus = "experimental-actor-package-swap",
        PatchLead = "cross-level spring chest controller smoke",
        CrossLevelTemplateId = "common.spring_chest_controller.townsquare.t30",
        CrossLevelFamily = "springChest",
        CrossLevelSourceLevelKey = "TownSquare",
        CrossLevelSourceLevelName = "Town Square",
        CrossLevelSourceTrueIndex = 30,
        CrossLevelRequiredExporterFeature = "ActorPackageSwapIntoUnusedRoot",
        IsAdded = true
    };
    Moby springChest = new()
    {
        Index = nextIndex + 2,
        TrueIndex = nextTrueIndex + 2,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(nextTrueIndex + 2),
        Position = new Vector3f(position.X + 16, position.Y, position.Z),
        OriginalPosition = new Vector3f(position.X + 16, position.Y, position.Z),
        Type = 0x18,
        OriginalType = 0x18,
        State = 0,
        OriginalState = 0,
        SourceByte36 = 0x49,
        OriginalSourceByte36 = 0x49,
        SourceByte37 = 0x01,
        SourceByte4F = 0,
        OriginalSourceByte4F = 0,
        Flag4A = CurrentStoneHillSpringRecipeId.Contains("SourceNative", StringComparison.OrdinalIgnoreCase) || CurrentStoneHillSpringRecipeId.Contains("NativePreHit", StringComparison.OrdinalIgnoreCase) ? 0xFF : 0x10,
        Flag4B = 0x55,
        OriginalFlag4B = 0x55,
        Color = Moby.ColorForType(0x20),
        Label = "Spring Chest",
        OriginalLabel = "Spring Chest",
        PatchStatus = "experimental-actor-package-swap",
        PatchLead = "cross-level spring chest smoke",
        CrossLevelTemplateId = "common.spring_chest.townsquare.t82",
        CrossLevelFamily = "springChest",
        CrossLevelSourceLevelKey = "TownSquare",
        CrossLevelSourceLevelName = "Town Square",
        CrossLevelSourceTrueIndex = 82,
        CrossLevelRequiredExporterFeature = "ActorPackageSwapIntoUnusedRoot",
        IsAdded = true
    };
    bool useIsolatedPeaceKeepersSpringDonors = CurrentStoneHillSpringRecipeId.Contains("T91T131", StringComparison.OrdinalIgnoreCase);
    bool useContainedHelperPeaceKeepersSpringCluster = CurrentStoneHillSpringRecipeId.Contains("T80T83", StringComparison.OrdinalIgnoreCase);
    int peaceKeepersSpringPrimarySourceTrueIndex = useContainedHelperPeaceKeepersSpringCluster ? 80 : useIsolatedPeaceKeepersSpringDonors ? 91 : 70;
    int peaceKeepersSpringPairSourceTrueIndex = useContainedHelperPeaceKeepersSpringCluster ? 83 : useIsolatedPeaceKeepersSpringDonors ? 131 : 71;
    int peaceKeepersSpringPrimaryFlag4B = useIsolatedPeaceKeepersSpringDonors ? 0x54 : 0x55;
    string peaceKeepersSpringPairTemplateId = useIsolatedPeaceKeepersSpringDonors
        ? "common.spring_chest.peacekeepers.t131"
        : "common.spring_chest.peacekeepers.t71";
    Moby peaceKeepersSpringChest = new()
    {
        Index = nextIndex + 3,
        TrueIndex = nextTrueIndex + 3,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(nextTrueIndex + 3),
        Position = new Vector3f(position.X + 16, position.Y, position.Z),
        OriginalPosition = new Vector3f(position.X + 16, position.Y, position.Z),
        Type = 0x20,
        OriginalType = 0x20,
        State = 0,
        OriginalState = 0,
        SourceByte36 = 0x49,
        OriginalSourceByte36 = 0x49,
        SourceByte37 = 0x01,
        OriginalSourceByte37 = 0x01,
        SourceByte4F = 0,
        OriginalSourceByte4F = 0,
        Flag4A = CurrentStoneHillSpringRecipeId.Contains("SourceNative", StringComparison.OrdinalIgnoreCase) || CurrentStoneHillSpringRecipeId.Contains("NativePreHit", StringComparison.OrdinalIgnoreCase) ? 0xFF : 0x10,
        OriginalFlag4A = 0xFF,
        Flag4B = peaceKeepersSpringPrimaryFlag4B,
        OriginalFlag4B = peaceKeepersSpringPrimaryFlag4B,
        Color = Moby.ColorForType(0x20),
        Label = "Spring Chest",
        OriginalLabel = "Spring Chest",
        PatchStatus = "experimental-actor-package-swap",
        PatchLead = useIsolatedPeaceKeepersSpringDonors
            ? "cross-level Peace Keepers T91 isolated spring chest smoke using Stone Hill's unused 0x000E actor-root slot and source-native bytes"
            : "cross-level Peace Keepers T70 spring chest smoke using Stone Hill's unused 0x000E actor-root slot, native reward rows, and runtime-armed source bytes",
        CrossLevelTemplateId = CurrentStoneHillSpringTemplateId,
        CrossLevelFamily = "springChest",
        CrossLevelSourceLevelKey = "PeaceKeepers",
        CrossLevelSourceLevelName = "Peace Keepers",
        CrossLevelSourceTrueIndex = peaceKeepersSpringPrimarySourceTrueIndex,
        CrossLevelRequiredExporterFeature = "ActorPackageSwapIntoUnusedRoot",
        IsAdded = true
    };
    Moby peaceKeepersSpringChestClusterMate81 = new()
    {
        Index = nextIndex + 8,
        TrueIndex = nextTrueIndex + 8,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(nextTrueIndex + 8),
        Position = new Vector3f(position.X + 199f, position.Y + 103.0625f, position.Z),
        OriginalPosition = new Vector3f(position.X + 199f, position.Y + 103.0625f, position.Z),
        Type = 0x20,
        OriginalType = 0x20,
        State = 0,
        OriginalState = 0,
        SourceByte36 = 0x49,
        OriginalSourceByte36 = 0x49,
        SourceByte37 = 0x01,
        OriginalSourceByte37 = 0x01,
        SourceByte4F = 0,
        OriginalSourceByte4F = 0,
        Flag4A = 0xFF,
        OriginalFlag4A = 0xFF,
        Flag4B = 0x55,
        OriginalFlag4B = 0x55,
        Color = Moby.ColorForType(0x20),
        Label = "Spring Chest",
        OriginalLabel = "Spring Chest",
        PatchStatus = "experimental-actor-package-swap",
        PatchLead = "cross-level Peace Keepers T81 spring chest cluster mate for the T80-T83 contained-helper smoke",
        CrossLevelTemplateId = CurrentStoneHillSpringTemplateId,
        CrossLevelFamily = "springChest",
        CrossLevelSourceLevelKey = "PeaceKeepers",
        CrossLevelSourceLevelName = "Peace Keepers",
        CrossLevelSourceTrueIndex = 81,
        CrossLevelRequiredExporterFeature = "ActorPackageSwapIntoUnusedRoot",
        IsAdded = true
    };
    Moby peaceKeepersSpringChestClusterMate82 = new()
    {
        Index = nextIndex + 9,
        TrueIndex = nextTrueIndex + 9,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(nextTrueIndex + 9),
        Position = new Vector3f(position.X + 158.0625f, position.Y + 185f, position.Z),
        OriginalPosition = new Vector3f(position.X + 158.0625f, position.Y + 185f, position.Z),
        Type = 0x20,
        OriginalType = 0x20,
        State = 0,
        OriginalState = 0,
        SourceByte36 = 0x49,
        OriginalSourceByte36 = 0x49,
        SourceByte37 = 0x01,
        OriginalSourceByte37 = 0x01,
        SourceByte4F = 0,
        OriginalSourceByte4F = 0,
        Flag4A = 0xFF,
        OriginalFlag4A = 0xFF,
        Flag4B = 0x55,
        OriginalFlag4B = 0x55,
        Color = Moby.ColorForType(0x20),
        Label = "Spring Chest",
        OriginalLabel = "Spring Chest",
        PatchStatus = "experimental-actor-package-swap",
        PatchLead = "cross-level Peace Keepers T82 spring chest cluster mate for the T80-T83 contained-helper smoke",
        CrossLevelTemplateId = CurrentStoneHillSpringTemplateId,
        CrossLevelFamily = "springChest",
        CrossLevelSourceLevelKey = "PeaceKeepers",
        CrossLevelSourceLevelName = "Peace Keepers",
        CrossLevelSourceTrueIndex = 82,
        CrossLevelRequiredExporterFeature = "ActorPackageSwapIntoUnusedRoot",
        IsAdded = true
    };
    Moby peaceKeepersSpringChestPairMate = new()
    {
        Index = nextIndex + 6,
        TrueIndex = nextTrueIndex + 6,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(nextTrueIndex + 6),
        Position = useContainedHelperPeaceKeepersSpringCluster
            ? new Vector3f(position.X + 81.25f, position.Y + 14.125f, position.Z)
            : new Vector3f(position.X + 312.9375f, position.Y + 3.8125f, position.Z + 4.5625f),
        OriginalPosition = useContainedHelperPeaceKeepersSpringCluster
            ? new Vector3f(position.X + 81.25f, position.Y + 14.125f, position.Z)
            : new Vector3f(position.X + 312.9375f, position.Y + 3.8125f, position.Z + 4.5625f),
        Type = 0x20,
        OriginalType = 0x20,
        State = 0,
        OriginalState = 0,
        SourceByte36 = 0x49,
        OriginalSourceByte36 = 0x49,
        SourceByte37 = 0x01,
        OriginalSourceByte37 = 0x01,
        SourceByte4F = 0,
        OriginalSourceByte4F = 0,
        Flag4A = CurrentStoneHillSpringRecipeId.Contains("SourceNative", StringComparison.OrdinalIgnoreCase) || CurrentStoneHillSpringRecipeId.Contains("NativePreHit", StringComparison.OrdinalIgnoreCase) ? 0xFF : 0x10,
        OriginalFlag4A = 0xFF,
        Flag4B = useContainedHelperPeaceKeepersSpringCluster ? 0x54 : 0x55,
        OriginalFlag4B = useContainedHelperPeaceKeepersSpringCluster ? 0x54 : 0x55,
        Color = Moby.ColorForType(0x20),
        Label = "Spring Chest",
        OriginalLabel = "Spring Chest",
        PatchStatus = "experimental-actor-package-swap",
        PatchLead = useIsolatedPeaceKeepersSpringDonors
            ? "cross-level Peace Keepers T131 isolated spring chest companion for the T91 source-native spring chest smoke"
            : useContainedHelperPeaceKeepersSpringCluster
            ? "cross-level Peace Keepers T83 spring chest cluster mate for the T80-T83 contained-helper smoke"
            : "cross-level Peace Keepers T71 pair mate for the T70 runtime-armed spring chest smoke",
        CrossLevelTemplateId = peaceKeepersSpringPairTemplateId,
        CrossLevelFamily = "springChest",
        CrossLevelSourceLevelKey = "PeaceKeepers",
        CrossLevelSourceLevelName = "Peace Keepers",
        CrossLevelSourceTrueIndex = peaceKeepersSpringPairSourceTrueIndex,
        CrossLevelRequiredExporterFeature = "ActorPackageSwapIntoUnusedRoot",
        IsAdded = true
    };
    Moby stoneHillSpringController = new()
    {
        Index = nextIndex + 4,
        TrueIndex = nextTrueIndex + 4,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(nextTrueIndex + 4),
        Position = new Vector3f(position.X + 16, position.Y, position.Z),
        OriginalPosition = new Vector3f(position.X + 16, position.Y, position.Z),
        Type = 0x20,
        OriginalType = 0x20,
        State = 0,
        OriginalState = 0,
        SourceByte36 = 0xC2,
        OriginalSourceByte36 = 0xC2,
        SourceByte37 = 0x00,
        OriginalSourceByte37 = 0x00,
        SourceByte4F = 0,
        OriginalSourceByte4F = 0,
        Flag4A = 0x10,
        Flag4B = 0x55,
        OriginalFlag4B = 0x55,
        Color = Moby.ColorForType(0x20),
        Label = "Spring Chest local controller",
        OriginalLabel = "Spring Chest local controller",
        CandidateKind = "Control",
        BehaviorNote = "Local Stone Hill 0x00C2 controller paired with the imported Peace Keepers T70 Spring Chest shell for older helper candidates.",
        PatchStatus = "supported-lightweight-object",
        PatchLead = "local Stone Hill controller helper for cross-level Peace Keepers T70 runtime 0x01A6 spring chest smoke",
        CrossLevelTemplateId = "common.spring_chest_controller.stonehill.local00c2",
        CrossLevelFamily = "springChest",
        CrossLevelSourceLevelKey = "StoneHill",
        CrossLevelSourceLevelName = "Stone Hill",
        CrossLevelSourceTrueIndex = 30,
        CrossLevelRequiredExporterFeature = "DirectSourceRecordAppend",
        IsAdded = true
    };
    Moby peaceKeepersSpringD8Companion = new()
    {
        Index = nextIndex + 7,
        TrueIndex = nextTrueIndex + 7,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(nextTrueIndex + 7),
        Position = new Vector3f(position.X + 155.5f, position.Y + 56.9375f, position.Z - 4.625f),
        OriginalPosition = new Vector3f(position.X + 155.5f, position.Y + 56.9375f, position.Z - 4.625f),
        Type = 0x20,
        OriginalType = 0x20,
        State = 0,
        OriginalState = 0,
        SourceByte36 = 0xD8,
        OriginalSourceByte36 = 0xD8,
        SourceByte37 = 0x00,
        OriginalSourceByte37 = 0x00,
        SourceByte4F = 0,
        OriginalSourceByte4F = 0,
        Flag4A = 0x10,
        OriginalFlag4A = 0x10,
        Flag4B = 0x55,
        OriginalFlag4B = 0x55,
        Color = Moby.ColorForType(0x20),
        Label = "Spring Chest native companion",
        OriginalLabel = "Spring Chest native companion",
        CandidateKind = "Control",
        BehaviorNote = "Native Peace Keepers 0x00D8 companion row observed beside working Spring Chest T70/T71 in RAM.",
        PatchStatus = "experimental-actor-package-swap",
        PatchLead = "cross-level Peace Keepers T113 native 0x00D8 companion for T70 spring chest diagnostic",
        CrossLevelTemplateId = "common.spring_chest_companion.peacekeepers.t113",
        CrossLevelFamily = "springChest",
        CrossLevelSourceLevelKey = "PeaceKeepers",
        CrossLevelSourceLevelName = "Peace Keepers",
        CrossLevelSourceTrueIndex = 113,
        CrossLevelRequiredExporterFeature = "ActorPackageSwapIntoUnusedRoot",
        IsAdded = true
    };
    Moby wizardTransform = new()
    {
        Index = existingTransformDonor.Index,
        TrueIndex = existingTransformDonor.TrueIndex,
        LegacyIndex = existingTransformDonor.LegacyIndex,
        Position = existingTransformDonor.OriginalPosition,
        OriginalPosition = existingTransformDonor.OriginalPosition,
        Type = 0x20,
        OriginalType = existingTransformDonor.OriginalType,
        State = 0x01,
        OriginalState = existingTransformDonor.OriginalState,
        SourceByte36 = 0x1B,
        OriginalSourceByte36 = existingTransformDonor.OriginalSourceByte36,
        SourceByte37 = 0x01,
        OriginalSourceByte37 = existingTransformDonor.OriginalSourceByte37,
        SourceByte4F = 0x00,
        OriginalSourceByte4F = existingTransformDonor.OriginalSourceByte4F,
        Flag4A = 0x10,
        OriginalFlag4A = existingTransformDonor.OriginalFlag4A,
        Flag4B = 0x56,
        OriginalFlag4B = existingTransformDonor.OriginalFlag4B,
        Color = Moby.ColorForType(0x20),
        Label = "Green Wizard",
        OriginalLabel = existingTransformDonor.OriginalLabel,
        PatchStatus = "experimental-actor-package-import",
        PatchLead = "cross-level wizard transform smoke",
        CrossLevelTemplateId = "enemy.green_wizard.wizardpeak.t6",
        CrossLevelFamily = "enemyTransform",
        CrossLevelSourceLevelKey = "WizardPeak",
        CrossLevelSourceLevelName = "Wizard Peak",
        CrossLevelSourceTrueIndex = 6,
        CrossLevelRequiredExporterFeature = "ActorPackageImportAndPointerRebase"
    };

    await MobyEditStore.SaveAsync(path, [key, springController, springChest, wizardTransform], "Cross-level template smoke");
    using FileStream stream = File.OpenRead(path);
    using JsonDocument document = JsonDocument.Parse(stream);
    JsonElement keyEdit = document.RootElement.GetProperty("edits").EnumerateArray().First(edit => ReadJsonString(edit, "label") == "Key");
    if (!keyEdit.TryGetProperty("crossLevelTemplate", out JsonElement keyTemplate) ||
        ReadJsonString(keyTemplate, "id") != key.CrossLevelTemplateId ||
        ReadJsonString(keyTemplate, "family") != key.CrossLevelFamily ||
        ReadJsonInt32(keyTemplate, "sourceTrueIndex", -1) != key.CrossLevelSourceTrueIndex)
        throw new InvalidOperationException("Cross-level key template metadata was not saved.");

    if (!keyEdit.TryGetProperty("sourceByteEdits", out JsonElement sourceByteEdits) ||
        !sourceByteEdits.EnumerateArray().Any(edit => ReadJsonInt32(edit, "offset", -1) == 0x52 && ReadJsonInt32(edit, "value", -1) == 0x40))
        throw new InvalidOperationException("Cross-level key did not save its flag4A identity byte edit.");

    List<Moby> loadedMobys = [];
    int applied = MobyEditStore.Load(path, loadedMobys);
    Moby? loadedKey = loadedMobys.FirstOrDefault(moby => moby.CrossLevelTemplateId == key.CrossLevelTemplateId);
    if (loadedKey == null ||
        loadedKey.CrossLevelFamily != key.CrossLevelFamily ||
        loadedKey.CrossLevelSourceLevelKey != key.CrossLevelSourceLevelKey ||
        loadedKey.CrossLevelRequiredExporterFeature != key.CrossLevelRequiredExporterFeature)
        throw new InvalidOperationException("Cross-level key template metadata did not survive load.");

    if (File.Exists(sourceImage))
    {
        string objectsDir = Path.Combine(workspace.RootPath, "_local", "objects");
        string syntheticSpringEvidencePath = Path.Combine(objectsDir, "stonehill-spring-chest-zero-gap-candidate-smoke-pass.candidate-result.json");
        File.Delete(syntheticSpringEvidencePath);

        MobySourcePatchPlan plan = MobySourcePatchExporter.BuildPlan(
            sourceImage,
            DiscImageLocator.FindCueForImage(sourceImage),
            Path.Combine(workspace.RootPath, "_local", "objects", "cross-level-template-smoke.bin"),
            Path.Combine(workspace.RootPath, "_local", "objects", "cross-level-template-smoke.cue"),
            level,
            path);
        MobySourcePatch? keyAppend = plan.Patches.FirstOrDefault(patch =>
            string.Equals(patch.Kind, "moby-record-append", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(patch.MobyLabel, "Key", StringComparison.OrdinalIgnoreCase));
        if (keyAppend == null)
            throw new InvalidOperationException("Cross-level key did not produce a source-record append patch.");

        byte[] appendedBytes = ParseHexPreview(keyAppend.AfterHexPreview);
        if (appendedBytes.Length <= 0x53 ||
            appendedBytes[0x36] != 0xAD ||
            appendedBytes[0x52] != 0x40 ||
            appendedBytes[0x53] != 0xFF)
            throw new InvalidOperationException("Cross-level key append patch did not preserve key identity bytes.");

        bool skippedActorPackageChest = plan.SkippedEdits.Any(skip =>
            skip.Contains("common.spring_chest.townsquare.t82", StringComparison.OrdinalIgnoreCase) &&
            skip.Contains("ActorPackageSwapIntoUnusedRoot", StringComparison.OrdinalIgnoreCase));
        if (!skippedActorPackageChest)
            throw new InvalidOperationException("Stone Hill Town Square spring chest should be guarded after repeated normal-chest results.");

        MobyActorPackageImportPreview? springPreview = plan.PackageImportPreviews.FirstOrDefault(preview =>
            string.Equals(preview.TemplateId, "common.spring_chest.townsquare.t82", StringComparison.OrdinalIgnoreCase));
        if (springPreview == null ||
            !string.IsNullOrWhiteSpace(springPreview.RecipeId) ||
            springPreview.CanWriteImage ||
            !springPreview.GuardReason.Contains("No native actor-package recipe", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Stone Hill Town Square spring chest should be unmapped after all known Town Square routes failed.");
        }

        bool hasSpringAppend = plan.Patches.Any(patch =>
            string.Equals(patch.Kind, "moby-record-append", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(patch.MobyLabel, "Spring Chest", StringComparison.OrdinalIgnoreCase));
        bool hasSpringPackageCopy = plan.Patches.Count(patch =>
            string.Equals(patch.Kind, "actor-package-copy", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(patch.MobyLabel, "Spring Chest", StringComparison.OrdinalIgnoreCase)) == 3;
        bool hasSpringSpecialData = plan.Patches.Any(patch =>
            string.Equals(patch.Kind, "cross-level-moby-special-data-import", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(patch.MobyLabel, "Spring Chest", StringComparison.OrdinalIgnoreCase));
        if (hasSpringAppend || hasSpringPackageCopy || hasSpringSpecialData)
            throw new InvalidOperationException("Stone Hill spring chest guard still emitted package, special-data, or source-record patches.");

        string springCandidatePath = Path.Combine(workspace.RootPath, "_local", "objects", $"{CurrentStoneHillSpringCandidateSlug}-native-edits.json");
        IReadOnlyList<Moby> springCandidateMobys = useContainedHelperPeaceKeepersSpringCluster
            ? [peaceKeepersSpringChest, peaceKeepersSpringChestClusterMate81, peaceKeepersSpringChestClusterMate82, peaceKeepersSpringChestPairMate]
            : CurrentStoneHillSpringRecipeId.Contains("NativeD8Companion", StringComparison.OrdinalIgnoreCase)
            ? [peaceKeepersSpringD8Companion, peaceKeepersSpringChest]
            : CurrentStoneHillSpringRecipeId.Contains("LocalController", StringComparison.OrdinalIgnoreCase)
                ? [stoneHillSpringController, peaceKeepersSpringChest]
                : [peaceKeepersSpringChest, peaceKeepersSpringChestPairMate];
        await MobyEditStore.SaveAsync(springCandidatePath, springCandidateMobys, "Stone Hill Spring Chest Peace Keepers stack-safe helper candidate");
    if (CurrentStoneHillSpringRecipeId.Contains("StaticNativePreHitSource", StringComparison.OrdinalIgnoreCase) ||
        CurrentStoneHillSpringRecipeId.Contains("NativePreHitShellLifecycle", StringComparison.OrdinalIgnoreCase) ||
        CurrentStoneHillSpringRecipeId.Contains("ForcedNativePreHitLifecycle", StringComparison.OrdinalIgnoreCase))
        AddSpringChestNativePreHitShellLifecycleSourceByteEdits(springCandidatePath);
        else if (CurrentStoneHillSpringRecipeId.Contains("BluePreHit", StringComparison.OrdinalIgnoreCase))
            AddSpringChestBluePreHitSourceByteEdits(springCandidatePath);
        else if (!CurrentStoneHillSpringRecipeId.Contains("SourceNative", StringComparison.OrdinalIgnoreCase) &&
            !CurrentStoneHillSpringRecipeId.Contains("NativePreHit", StringComparison.OrdinalIgnoreCase))
            AddSpringChestRuntimeArmedSourceByteEdits(springCandidatePath);
        MobySourcePatchPlan springCandidatePlan = MobySourcePatchExporter.BuildPlan(
            sourceImage,
            DiscImageLocator.FindCueForImage(sourceImage),
            Path.Combine(workspace.RootPath, "_local", "objects", $"{CurrentStoneHillSpringCandidateSlug}.bin"),
            Path.Combine(workspace.RootPath, "_local", "objects", $"{CurrentStoneHillSpringCandidateSlug}.cue"),
            level,
            springCandidatePath,
            allowPlanOnlyActorPackageImports: true);

        MobyActorPackageImportPreview? springCandidatePreview = springCandidatePlan.PackageImportPreviews.FirstOrDefault(preview =>
            string.Equals(preview.TemplateId, CurrentStoneHillSpringTemplateId, StringComparison.OrdinalIgnoreCase));
        if (springCandidatePreview == null ||
            !string.Equals(springCandidatePreview.Family, "springChest", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(springCandidatePreview.RecipeId))
        {
            throw new InvalidOperationException("Stone Hill Peace Keepers Spring Chest diagnostic candidate did not map to a Spring Chest actor-package recipe.");
        }

        bool springCandidateIsGuardedDiagnostic =
            springCandidatePreview.GuardReason.Contains("disposable candidate mode", StringComparison.OrdinalIgnoreCase) ||
            springCandidatePreview.GuardReason.Contains("must pass a disposable candidate test", StringComparison.OrdinalIgnoreCase);
        if (!springCandidateIsGuardedDiagnostic)
        {
            throw new InvalidOperationException("Stone Hill Peace Keepers Spring Chest diagnostic candidate should remain a guarded candidate until in-game evidence proves the missing interaction is fixed.");
        }

        if (CurrentStoneHillSpringRecipeId.Contains("SafeRewardRefresh", StringComparison.OrdinalIgnoreCase) ||
            CurrentStoneHillSpringRecipeId.Contains("SafeOneShot", StringComparison.OrdinalIgnoreCase) ||
            CurrentStoneHillSpringRecipeId.Contains("SafeVisibleOneShotCleanup", StringComparison.OrdinalIgnoreCase) ||
            CurrentStoneHillSpringRecipeId.Contains("SafeLooseGemOneShotCleanup", StringComparison.OrdinalIgnoreCase) ||
            CurrentStoneHillSpringRecipeId.Contains("SafeLooseVisibleGemOneShotCleanup", StringComparison.OrdinalIgnoreCase) ||
            CurrentStoneHillSpringRecipeId.Contains("SafeLowVisibleGemOneShotCleanup", StringComparison.OrdinalIgnoreCase) ||
            CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemOneShotCleanup", StringComparison.OrdinalIgnoreCase) ||
            CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemReturnLoop", StringComparison.OrdinalIgnoreCase) ||
            CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemSpringArc", StringComparison.OrdinalIgnoreCase) ||
            CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemSnappyArc", StringComparison.OrdinalIgnoreCase) ||
            CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemEarlyParkArc", StringComparison.OrdinalIgnoreCase) ||
            CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHeightTuneArc", StringComparison.OrdinalIgnoreCase) ||
            CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHigherArc", StringComparison.OrdinalIgnoreCase) ||
            CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemFullReturnArc", StringComparison.OrdinalIgnoreCase) ||
            CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHangReturnArc", StringComparison.OrdinalIgnoreCase) ||
            CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemNoSparxHangReturnArc", StringComparison.OrdinalIgnoreCase) ||
            CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemEarlyPickupRearmHangReturnArc", StringComparison.OrdinalIgnoreCase) ||
            CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemUnlistedHangReturnArc", StringComparison.OrdinalIgnoreCase) ||
            CurrentStoneHillSpringRecipeId.Contains("SafeBlankScratchVisibleGemHangReturnArc", StringComparison.OrdinalIgnoreCase) ||
            CurrentStoneHillSpringRecipeId.Contains("SafeVisualScratchVisibleGemHangReturnArc", StringComparison.OrdinalIgnoreCase) ||
            CurrentStoneHillSpringRecipeId.Contains("SafeDormantVisualScratchVisibleGemHangReturnArc", StringComparison.OrdinalIgnoreCase))
        {
            MobySourcePatch? helperPayloadPatch = springCandidatePlan.Patches.FirstOrDefault(patch =>
                string.Equals(patch.Kind, "spring-chest-helper-payload", StringComparison.OrdinalIgnoreCase));
            if (helperPayloadPatch == null)
                throw new InvalidOperationException("Safe Spring Chest candidate did not emit a helper payload patch.");

            byte[] helperPayload = ParseHexPreview(helperPayloadPatch.AfterHexPreview);
            if (ContainsBytes(helperPayload, 0x04, 0x00, 0x09, 0xAD) ||
                ContainsBytes(helperPayload, 0x50, 0x00, 0x0A, 0xAD))
            {
                throw new InvalidOperationException("Safe Spring Chest helper regressed to the risky controller pointer/tail writes.");
            }

            bool looseGemShape =
                CurrentStoneHillSpringRecipeId.Contains("SafeLooseGemOneShotCleanup", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafeLooseVisibleGemOneShotCleanup", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafeLowVisibleGemOneShotCleanup", StringComparison.OrdinalIgnoreCase);
            bool preinitializedRewardRow =
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemOneShotCleanup", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemReturnLoop", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemSpringArc", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemSnappyArc", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemEarlyParkArc", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHeightTuneArc", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHigherArc", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemFullReturnArc", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHangReturnArc", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemNoSparxHangReturnArc", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemEarlyPickupRearmHangReturnArc", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemUnlistedHangReturnArc", StringComparison.OrdinalIgnoreCase);
            bool blankScratchRewardRow =
                CurrentStoneHillSpringRecipeId.Contains("SafeBlankScratchVisibleGemHangReturnArcPostReturnFullBlankScriptedMotionRearm", StringComparison.OrdinalIgnoreCase);
            bool visualScratchRewardRow =
                CurrentStoneHillSpringRecipeId.Contains("SafeVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankScriptedMotionRearm", StringComparison.OrdinalIgnoreCase);
            bool dormantVisualScratchRewardRow =
                CurrentStoneHillSpringRecipeId.Contains("SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeScriptedMotionRearm", StringComparison.OrdinalIgnoreCase);
            bool dormantVisualScratchNoValueRewardRow =
                CurrentStoneHillSpringRecipeId.Contains("SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeNoValueScriptedMotionRearm", StringComparison.OrdinalIgnoreCase);
            bool dormantVisualScratchVisualOnlyRewardRow =
                CurrentStoneHillSpringRecipeId.Contains("SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearm", StringComparison.OrdinalIgnoreCase);
            bool dormantVisualScratchTypeOnlyRewardRow =
                CurrentStoneHillSpringRecipeId.Contains("SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorTypeOnlyScriptedMotionRearm", StringComparison.OrdinalIgnoreCase);
            bool scratchRewardRow = blankScratchRewardRow || visualScratchRewardRow || dormantVisualScratchRewardRow || dormantVisualScratchNoValueRewardRow || dormantVisualScratchVisualOnlyRewardRow || dormantVisualScratchTypeOnlyRewardRow;
            bool postReturnUnlinkRearmCandidate =
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHangReturnArcPostReturnUnlinkRearm", StringComparison.OrdinalIgnoreCase);
            bool postReturnQuarantineRearmCandidate =
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHangReturnArcPostReturnQuarantineRearm", StringComparison.OrdinalIgnoreCase);
            bool postReturnFullQuarantineRearmCandidate =
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineRearm", StringComparison.OrdinalIgnoreCase);
            bool postReturnFullQuarantineDelayedCollectRearmCandidate =
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedCollectRearm", StringComparison.OrdinalIgnoreCase);
            bool postReturnFullQuarantineDelayedIdentityRearmCandidate =
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedIdentityRearm", StringComparison.OrdinalIgnoreCase);
            bool postReturnFullQuarantineScriptedMotionRearmCandidate =
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineScriptedMotionRearm", StringComparison.OrdinalIgnoreCase);
            bool postReturnFullBlankScriptedMotionRearmCandidate =
                CurrentStoneHillSpringRecipeId.Contains("SafeBlankScratchVisibleGemHangReturnArcPostReturnFullBlankScriptedMotionRearm", StringComparison.OrdinalIgnoreCase);
            bool postReturnIdentityBlankScriptedMotionRearmCandidate =
                CurrentStoneHillSpringRecipeId.Contains("SafeVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankScriptedMotionRearm", StringComparison.OrdinalIgnoreCase);
            bool postReturnIdentityBlankDonorShapeScriptedMotionRearmCandidate =
                CurrentStoneHillSpringRecipeId.Contains("SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeScriptedMotionRearm", StringComparison.OrdinalIgnoreCase);
            bool postReturnIdentityBlankDonorShapeNoValueScriptedMotionRearmCandidate =
                CurrentStoneHillSpringRecipeId.Contains("SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeNoValueScriptedMotionRearm", StringComparison.OrdinalIgnoreCase);
            bool postReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmCandidate =
                CurrentStoneHillSpringRecipeId.Contains("SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearm", StringComparison.OrdinalIgnoreCase);
            bool postReturnIdentityBlankDonorTypeOnlyScriptedMotionRearmCandidate =
                CurrentStoneHillSpringRecipeId.Contains("SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorTypeOnlyScriptedMotionRearm", StringComparison.OrdinalIgnoreCase);
            bool inertParkCandidate =
                !postReturnUnlinkRearmCandidate &&
                !postReturnQuarantineRearmCandidate &&
                !postReturnFullQuarantineRearmCandidate &&
                !postReturnFullQuarantineDelayedCollectRearmCandidate &&
                !postReturnFullQuarantineDelayedIdentityRearmCandidate &&
                !postReturnFullQuarantineScriptedMotionRearmCandidate &&
                !postReturnFullBlankScriptedMotionRearmCandidate &&
                !postReturnIdentityBlankScriptedMotionRearmCandidate &&
                !postReturnIdentityBlankDonorShapeScriptedMotionRearmCandidate &&
                !postReturnIdentityBlankDonorShapeNoValueScriptedMotionRearmCandidate &&
                !postReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmCandidate &&
                !postReturnIdentityBlankDonorTypeOnlyScriptedMotionRearmCandidate &&
                (CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHangReturnArcInertPark", StringComparison.OrdinalIgnoreCase) ||
                 CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHangReturnArcPostReturnUnlink", StringComparison.OrdinalIgnoreCase) ||
                 CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHangReturnArcPostReturnMonitor", StringComparison.OrdinalIgnoreCase) ||
                 CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHangReturnArcRewardQuarantine", StringComparison.OrdinalIgnoreCase));
            bool hasExpectedActivationByte = preinitializedRewardRow ||
                dormantVisualScratchRewardRow ||
                dormantVisualScratchNoValueRewardRow ||
                dormantVisualScratchVisualOnlyRewardRow ||
                dormantVisualScratchTypeOnlyRewardRow ||
                (looseGemShape
                    ? ContainsBytes(helperPayload, 0x00, 0x00, 0x09, 0x34, 0x49, 0x00, 0xC9, 0xA1)
                    : ContainsBytes(helperPayload, 0x49, 0x00, 0xC9, 0xA1));
            if (!hasExpectedActivationByte ||
                (!preinitializedRewardRow && !dormantVisualScratchRewardRow && !dormantVisualScratchNoValueRewardRow && !dormantVisualScratchVisualOnlyRewardRow && !dormantVisualScratchTypeOnlyRewardRow && !ContainsBytes(helperPayload, 0x4A, 0x00, 0xC9, 0xA1)) ||
                (!preinitializedRewardRow && !dormantVisualScratchVisualOnlyRewardRow && !dormantVisualScratchTypeOnlyRewardRow && !ContainsBytes(helperPayload, 0x54, 0x00, 0xC9, 0xAD)))
            {
                throw new InvalidOperationException("Safe Spring Chest helper is missing reward active/list bytes.");
            }

            if ((CurrentStoneHillSpringRecipeId.Contains("SafeOneShot", StringComparison.OrdinalIgnoreCase) ||
                 CurrentStoneHillSpringRecipeId.Contains("SafeVisibleOneShotCleanup", StringComparison.OrdinalIgnoreCase) ||
                 CurrentStoneHillSpringRecipeId.Contains("SafeLooseGemOneShotCleanup", StringComparison.OrdinalIgnoreCase) ||
                 CurrentStoneHillSpringRecipeId.Contains("SafeLooseVisibleGemOneShotCleanup", StringComparison.OrdinalIgnoreCase) ||
                 CurrentStoneHillSpringRecipeId.Contains("SafeLowVisibleGemOneShotCleanup", StringComparison.OrdinalIgnoreCase) ||
                 CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemOneShotCleanup", StringComparison.OrdinalIgnoreCase) ||
                 CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemReturnLoop", StringComparison.OrdinalIgnoreCase) ||
                 CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemSpringArc", StringComparison.OrdinalIgnoreCase) ||
                 CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemSnappyArc", StringComparison.OrdinalIgnoreCase) ||
                 CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemEarlyParkArc", StringComparison.OrdinalIgnoreCase) ||
                 CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHeightTuneArc", StringComparison.OrdinalIgnoreCase) ||
                 CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHigherArc", StringComparison.OrdinalIgnoreCase) ||
                 CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemFullReturnArc", StringComparison.OrdinalIgnoreCase) ||
                 CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHangReturnArc", StringComparison.OrdinalIgnoreCase) ||
                 CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemNoSparxHangReturnArc", StringComparison.OrdinalIgnoreCase) ||
                 CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemEarlyPickupRearmHangReturnArc", StringComparison.OrdinalIgnoreCase) ||
                 CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemUnlistedHangReturnArc", StringComparison.OrdinalIgnoreCase) ||
                 scratchRewardRow) &&
                !scratchRewardRow &&
                !helperPayloadPatch.Description.Contains("without refreshing the reward row", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Safe one-shot Spring Chest helper should not keep refreshing the reward row after the in-game infinite-gem result.");
            }

            if (CurrentStoneHillSpringRecipeId.Contains("SafeVisibleOneShotCleanup", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafeLooseGemOneShotCleanup", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafeLooseVisibleGemOneShotCleanup", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafeLowVisibleGemOneShotCleanup", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemOneShotCleanup", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemReturnLoop", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemSpringArc", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemSnappyArc", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemEarlyParkArc", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHeightTuneArc", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHigherArc", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemFullReturnArc", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHangReturnArc", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemNoSparxHangReturnArc", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemEarlyPickupRearmHangReturnArc", StringComparison.OrdinalIgnoreCase) ||
                CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemUnlistedHangReturnArc", StringComparison.OrdinalIgnoreCase) ||
                scratchRewardRow)
            {
                if (!preinitializedRewardRow &&
                    !scratchRewardRow &&
                    !helperPayloadPatch.Description.Contains("clears the reward runtime visibility word", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Safe visible one-shot Spring Chest helper should clear the reward visibility word after the invisible-gem result.");
                }

                if (!helperPayloadPatch.Description.Contains("fallback chest cleanup timer", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Safe visible one-shot Spring Chest helper should include a cleanup fallback after the invisible-gem result.");
                }

                if (!preinitializedRewardRow &&
                    !scratchRewardRow &&
                    (!ContainsBytes(helperPayload, 0x1C, 0x00, 0xC0, 0xAD) ||
                     !ContainsBytes(helperPayload, 0x04, 0x00, 0xE0, 0xAD)))
                {
                    throw new InvalidOperationException("Safe visible one-shot Spring Chest helper is missing the reward visibility clear or cleanup timer state write.");
                }

                if (preinitializedRewardRow &&
                    (!helperPayloadPatch.Description.Contains("preinitialized-reward", StringComparison.OrdinalIgnoreCase) ||
                     !helperPayloadPatch.Description.Contains("already-initialized reward row", StringComparison.OrdinalIgnoreCase) ||
                     (!inertParkCandidate && ContainsBytes(helperPayload, 0x1C, 0x00, 0xC0, 0xAD))))
                {
                    throw new InvalidOperationException("Safe preinitialized visible-gem one-shot Spring Chest helper should move an initialized reward row without clearing its runtime visibility word.");
                }

                if ((CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemReturnLoop", StringComparison.OrdinalIgnoreCase) ||
                     CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemSpringArc", StringComparison.OrdinalIgnoreCase) ||
                     CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemSnappyArc", StringComparison.OrdinalIgnoreCase) ||
                     CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemEarlyParkArc", StringComparison.OrdinalIgnoreCase) ||
                     CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHeightTuneArc", StringComparison.OrdinalIgnoreCase) ||
                     CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHigherArc", StringComparison.OrdinalIgnoreCase) ||
                     CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemFullReturnArc", StringComparison.OrdinalIgnoreCase) ||
                     CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHangReturnArc", StringComparison.OrdinalIgnoreCase) ||
                     CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemNoSparxHangReturnArc", StringComparison.OrdinalIgnoreCase) ||
                     CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemEarlyPickupRearmHangReturnArc", StringComparison.OrdinalIgnoreCase) ||
                     CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemUnlistedHangReturnArc", StringComparison.OrdinalIgnoreCase)) &&
                    (!helperPayloadPatch.Description.Contains("return-loop", StringComparison.OrdinalIgnoreCase) ||
                     (!helperPayloadPatch.Description.Contains("returns uncollected rewards after 0x96 frame", StringComparison.OrdinalIgnoreCase) &&
                      !helperPayloadPatch.Description.Contains("returns uncollected rewards after 0x50 frame", StringComparison.OrdinalIgnoreCase) &&
                      !helperPayloadPatch.Description.Contains("returns uncollected rewards after 0x38 frame", StringComparison.OrdinalIgnoreCase) &&
                      !helperPayloadPatch.Description.Contains("returns uncollected rewards after 0x1C frame", StringComparison.OrdinalIgnoreCase) &&
                      !helperPayloadPatch.Description.Contains("returns uncollected rewards after 0xC frame", StringComparison.OrdinalIgnoreCase)) ||
                     (!ContainsBytes(helperPayload, 0xF8, 0xFF, 0x29, 0x25) &&
                      !ContainsBytes(helperPayload, 0xB8, 0xFF, 0x29, 0x25) &&
                      !ContainsBytes(helperPayload, 0xD0, 0xFF, 0x29, 0x25) &&
                      !ContainsBytes(helperPayload, 0xD8, 0xFF, 0x29, 0x25) &&
                      !ContainsBytes(helperPayload, 0xE8, 0xFF, 0x29, 0x25)) ||
                     !ContainsBytes(helperPayload, 0x0C, 0x00, 0xC0, 0xAD) ||
                     !ContainsBytes(helperPayload, 0x10, 0x00, 0xC0, 0xAD) ||
                     !ContainsBytes(helperPayload, 0x04, 0x00, 0xE0, 0xAD)))
                {
                    throw new InvalidOperationException("Safe preinitialized visible-gem return-loop Spring Chest helper should lower/park uncollected rewards and reset helper state.");
                }

                bool springArcCandidate =
                    CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemSpringArc", StringComparison.OrdinalIgnoreCase) ||
                    CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemSnappyArc", StringComparison.OrdinalIgnoreCase) ||
                    CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemEarlyParkArc", StringComparison.OrdinalIgnoreCase) ||
                    CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHeightTuneArc", StringComparison.OrdinalIgnoreCase) ||
                    CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHigherArc", StringComparison.OrdinalIgnoreCase) ||
                    CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemFullReturnArc", StringComparison.OrdinalIgnoreCase) ||
                    CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemHangReturnArc", StringComparison.OrdinalIgnoreCase) ||
                    CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemNoSparxHangReturnArc", StringComparison.OrdinalIgnoreCase) ||
                    CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemEarlyPickupRearmHangReturnArc", StringComparison.OrdinalIgnoreCase) ||
                    CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemUnlistedHangReturnArc", StringComparison.OrdinalIgnoreCase);
                bool hasSpringArcDescription =
                    helperPayloadPatch.Description.Contains("raises the visible reward by 0x48 per frame for 0xC frame", StringComparison.OrdinalIgnoreCase) ||
                    helperPayloadPatch.Description.Contains("raises the visible reward by 0xC0 per frame for 0x4 frame", StringComparison.OrdinalIgnoreCase) ||
                    helperPayloadPatch.Description.Contains("raises the visible reward by 0x80 per frame for 0x3 frame", StringComparison.OrdinalIgnoreCase) ||
                    helperPayloadPatch.Description.Contains("raises the visible reward by 0xA0 per frame for 0x3 frame", StringComparison.OrdinalIgnoreCase);
                bool hasSpringArcZOffset =
                    helperPayloadPatch.Description.Contains("Z offset 0x240", StringComparison.OrdinalIgnoreCase) ||
                    helperPayloadPatch.Description.Contains("Z offset 0x280", StringComparison.OrdinalIgnoreCase) ||
                    helperPayloadPatch.Description.Contains("Z offset 0x300", StringComparison.OrdinalIgnoreCase) ||
                    helperPayloadPatch.Description.Contains("Z offset 0x380", StringComparison.OrdinalIgnoreCase) ||
                    helperPayloadPatch.Description.Contains("Z offset 0x480", StringComparison.OrdinalIgnoreCase);
                bool hasSpringArcZBytes =
                    ContainsBytes(helperPayload, 0x40, 0x02, 0x29, 0x25) ||
                    ContainsBytes(helperPayload, 0x80, 0x02, 0x29, 0x25) ||
                    ContainsBytes(helperPayload, 0x00, 0x03, 0x29, 0x25) ||
                    ContainsBytes(helperPayload, 0x80, 0x03, 0x29, 0x25) ||
                    ContainsBytes(helperPayload, 0x80, 0x04, 0x29, 0x25);
                bool hasSpringArcRiseBytes =
                    ContainsBytes(helperPayload, 0x48, 0x00, 0x29, 0x25) ||
                    ContainsBytes(helperPayload, 0xC0, 0x00, 0x29, 0x25) ||
                    ContainsBytes(helperPayload, 0x80, 0x00, 0x29, 0x25) ||
                    ContainsBytes(helperPayload, 0xA0, 0x00, 0x29, 0x25);
                if (springArcCandidate &&
                    (!helperPayloadPatch.Description.Contains("spring-arc", StringComparison.OrdinalIgnoreCase) ||
                     !hasSpringArcDescription ||
                     !hasSpringArcZOffset ||
                     !hasSpringArcZBytes ||
                     !hasSpringArcRiseBytes))
                {
                    throw new InvalidOperationException("Safe preinitialized visible-gem spring-arc Spring Chest helper should start low and raise the reward quickly before returning it.");
                }

                if (CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemNoSparxHangReturnArc", StringComparison.OrdinalIgnoreCase) &&
                    (!helperPayloadPatch.Description.Contains("masks the reward value byte while airborne", StringComparison.OrdinalIgnoreCase) ||
                     !ContainsBytes(helperPayload, 0x4F, 0x00, 0xC0, 0xA1) ||
                     !ContainsBytes(helperPayload, 0x4F, 0x00, 0xC9, 0xA1)))
                {
                    throw new InvalidOperationException("No-Sparx Spring Chest helper should mask the reward value byte while airborne and restore it when returned.");
                }

                if (CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemEarlyPickupRearmHangReturnArc", StringComparison.OrdinalIgnoreCase) &&
                    !helperPayloadPatch.Description.Contains("rearms instead of cleaning up if the reward is picked up early", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Early-pickup rearm Spring Chest helper should rearm instead of cleaning up if the reward disappears before the return window.");
                }

                if (CurrentStoneHillSpringRecipeId.Contains("SafePreinitializedVisibleGemUnlistedHangReturnArc", StringComparison.OrdinalIgnoreCase) &&
                    !helperPayloadPatch.Description.Contains("unlinks the reward from the active pickup list while airborne", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Unlisted Spring Chest helper should unlink the airborne reward from the active pickup list.");
                }

                if (inertParkCandidate &&
                    (!helperPayloadPatch.Description.Contains("unlinks the reward from the active pickup list while airborne", StringComparison.OrdinalIgnoreCase) ||
                     (!helperPayloadPatch.Description.Contains("parks the returned reward as an inert hidden row", StringComparison.OrdinalIgnoreCase) &&
                      !helperPayloadPatch.Description.Contains("parks the returned reward as a fully quarantined inert hidden row", StringComparison.OrdinalIgnoreCase))))
                {
                    throw new InvalidOperationException("Inert-park Spring Chest helper should unlink the reward and park it as an inert hidden row after return.");
                }

                if ((CurrentStoneHillSpringRecipeId.Contains("PersistentUnlink", StringComparison.OrdinalIgnoreCase) ||
                     CurrentStoneHillSpringRecipeId.Contains("PostReturnUnlink", StringComparison.OrdinalIgnoreCase) ||
                     CurrentStoneHillSpringRecipeId.Contains("PostReturnQuarantineRearm", StringComparison.OrdinalIgnoreCase) ||
                     CurrentStoneHillSpringRecipeId.Contains("PostReturnFullQuarantineRearm", StringComparison.OrdinalIgnoreCase) ||
                     CurrentStoneHillSpringRecipeId.Contains("PostReturnFullQuarantineDelayedCollectRearm", StringComparison.OrdinalIgnoreCase) ||
                     CurrentStoneHillSpringRecipeId.Contains("PostReturnFullQuarantineDelayedIdentityRearm", StringComparison.OrdinalIgnoreCase) ||
                     CurrentStoneHillSpringRecipeId.Contains("PostReturnFullQuarantineScriptedMotionRearm", StringComparison.OrdinalIgnoreCase) ||
                     CurrentStoneHillSpringRecipeId.Contains("PostReturnFullBlankScriptedMotionRearm", StringComparison.OrdinalIgnoreCase) ||
                     CurrentStoneHillSpringRecipeId.Contains("PostReturnIdentityBlankScriptedMotionRearm", StringComparison.OrdinalIgnoreCase) ||
                     CurrentStoneHillSpringRecipeId.Contains("PostReturnIdentityBlankDonorShapeScriptedMotionRearm", StringComparison.OrdinalIgnoreCase) ||
                     CurrentStoneHillSpringRecipeId.Contains("PostReturnIdentityBlankDonorShapeNoValueScriptedMotionRearm", StringComparison.OrdinalIgnoreCase) ||
                     CurrentStoneHillSpringRecipeId.Contains("PostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearm", StringComparison.OrdinalIgnoreCase) ||
                     CurrentStoneHillSpringRecipeId.Contains("PostReturnIdentityBlankDonorTypeOnlyScriptedMotionRearm", StringComparison.OrdinalIgnoreCase) ||
                     CurrentStoneHillSpringRecipeId.Contains("PostReturnMonitor", StringComparison.OrdinalIgnoreCase)) &&
                    !helperPayloadPatch.Description.Contains("keeps scanning for relisted pickup entries", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Persistent-unlink Spring Chest helper should keep scanning for relisted active pickup entries.");
                }

                if (CurrentStoneHillSpringRecipeId.Contains("PostReturnMonitor", StringComparison.OrdinalIgnoreCase) &&
                    !helperPayloadPatch.Description.Contains("keeps a post-return monitor active until the next hit", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Post-return monitor Spring Chest helper should keep watching the parked reward until the next hit.");
                }

                if (CurrentStoneHillSpringRecipeId.Contains("PostReturnUnlink", StringComparison.OrdinalIgnoreCase) &&
                    !helperPayloadPatch.Description.Contains("keeps a post-return unlink sweep active", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Post-return unlink Spring Chest helper should keep sweeping the active pickup list after the reward returns.");
                }

                if (CurrentStoneHillSpringRecipeId.Contains("PostReturnQuarantineRearm", StringComparison.OrdinalIgnoreCase) &&
                    !helperPayloadPatch.Description.Contains("restores the quarantined reward identity only when the chest is hit again", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Post-return quarantine rearm Spring Chest helper should restore the quarantined reward identity only on the next hit.");
                }

                if (CurrentStoneHillSpringRecipeId.Contains("PostReturnFullQuarantineRearm", StringComparison.OrdinalIgnoreCase) &&
                    !helperPayloadPatch.Description.Contains("fully clears the returned reward identity while parked", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Post-return full-quarantine rearm Spring Chest helper should fully clear the returned reward identity while parked.");
                }

                if (postReturnFullQuarantineDelayedIdentityRearmCandidate &&
                    (!helperPayloadPatch.Description.Contains("delays restoring the reward value until the gem has risen", StringComparison.OrdinalIgnoreCase) ||
                     !helperPayloadPatch.Description.Contains("delays restoring the reward type until the gem has risen", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("Delayed-identity Spring Chest helper should delay both the reward type and value until the gem rises.");
                }

                if (postReturnFullQuarantineScriptedMotionRearmCandidate &&
                    ((!CurrentStoneHillSpringRecipeId.Contains("ScriptedMotionRearmNoValueDelay", StringComparison.OrdinalIgnoreCase) &&
                      !helperPayloadPatch.Description.Contains("delays restoring the reward value until the gem has risen", StringComparison.OrdinalIgnoreCase)) ||
                     !helperPayloadPatch.Description.Contains("scripts the reward motion without waiting for the active pickup list", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("Scripted-motion Spring Chest helper should delay reward value and animate without waiting on the active pickup list.");
                }

                if (postReturnIdentityBlankDonorShapeScriptedMotionRearmCandidate &&
                    (!helperPayloadPatch.Description.Contains("donor standalone-gem shape", StringComparison.OrdinalIgnoreCase) ||
                     !helperPayloadPatch.Description.Contains("scripts the reward motion without waiting for the active pickup list", StringComparison.OrdinalIgnoreCase) ||
                     !helperPayloadPatch.Description.Contains("blanks the returned reward row while parked", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("Dormant visual-scratch Spring Chest helper should restore the donor standalone-gem shape only during pop and blank the parked row afterward.");
                }

                if (postReturnIdentityBlankDonorShapeNoValueScriptedMotionRearmCandidate &&
                    (!helperPayloadPatch.Description.Contains("donor standalone-gem shape", StringComparison.OrdinalIgnoreCase) ||
                     !helperPayloadPatch.Description.Contains("masks the reward value byte while airborne", StringComparison.OrdinalIgnoreCase) ||
                     !helperPayloadPatch.Description.Contains("scripts the reward motion without waiting for the active pickup list", StringComparison.OrdinalIgnoreCase) ||
                     !helperPayloadPatch.Description.Contains("blanks the returned reward row while parked", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("No-value dormant visual-scratch Spring Chest helper should render the donor gem shape while keeping the airborne reward non-collectible.");
                }

                if (postReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmCandidate &&
                    (!helperPayloadPatch.Description.Contains("donor standalone-gem shape", StringComparison.OrdinalIgnoreCase) ||
                     !helperPayloadPatch.Description.Contains("leaving the pickup/type words blank", StringComparison.OrdinalIgnoreCase) ||
                     !helperPayloadPatch.Description.Contains("scripts the reward motion without waiting for the active pickup list", StringComparison.OrdinalIgnoreCase) ||
                     !helperPayloadPatch.Description.Contains("blanks the returned reward row while parked", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("Visual-only dormant Spring Chest helper should keep donor color/shape bytes while leaving pickup/type words blank.");
                }

                if (postReturnIdentityBlankDonorTypeOnlyScriptedMotionRearmCandidate &&
                    (!helperPayloadPatch.Description.Contains("donor standalone-gem shape", StringComparison.OrdinalIgnoreCase) ||
                     !helperPayloadPatch.Description.Contains("leaving the final pickup tail word blank", StringComparison.OrdinalIgnoreCase) ||
                     !helperPayloadPatch.Description.Contains("scripts the reward motion without waiting for the active pickup list", StringComparison.OrdinalIgnoreCase) ||
                     !helperPayloadPatch.Description.Contains("blanks the returned reward row while parked", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("Type-only dormant Spring Chest helper should restore donor type/model word while leaving the final pickup tail word blank.");
                }

                if (postReturnFullBlankScriptedMotionRearmCandidate &&
                    (!helperPayloadPatch.Description.Contains("blank-scratch reward", StringComparison.OrdinalIgnoreCase) ||
                     !helperPayloadPatch.Description.Contains("rebuilds reward row", StringComparison.OrdinalIgnoreCase) ||
                     !helperPayloadPatch.Description.Contains("scripts the reward motion without waiting for the active pickup list", StringComparison.OrdinalIgnoreCase) ||
                     !helperPayloadPatch.Description.Contains("blanks the returned reward row while parked", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("Blank-scratch scripted-motion Spring Chest helper should rebuild the reward only during the pop and fully blank it while parked.");
                }

                if (postReturnIdentityBlankScriptedMotionRearmCandidate &&
                    (!helperPayloadPatch.Description.Contains("blank-scratch reward", StringComparison.OrdinalIgnoreCase) ||
                     !helperPayloadPatch.Description.Contains("rebuilds reward row", StringComparison.OrdinalIgnoreCase) ||
                     !helperPayloadPatch.Description.Contains("scripts the reward motion without waiting for the active pickup list", StringComparison.OrdinalIgnoreCase) ||
                     !helperPayloadPatch.Description.Contains("blanks the returned reward row while parked", StringComparison.OrdinalIgnoreCase) ||
                     !helperPayloadPatch.Description.Contains("preserving the donor gem visual scaffold", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("Visual-scratch scripted-motion Spring Chest helper should preserve the donor visual scaffold while blanking parked collectible identity.");
                }

            }

            if (CurrentStoneHillSpringRecipeId.Contains("SafeLooseGemOneShotCleanup", StringComparison.OrdinalIgnoreCase) &&
                (!helperPayloadPatch.Description.Contains("loose-gem row shape", StringComparison.OrdinalIgnoreCase) ||
                 !ContainsBytes(helperPayload, 0x40, 0xFF, 0x09, 0x3C, 0x20, 0x00, 0x29, 0x35)))
            {
                throw new InvalidOperationException("Safe loose-gem one-shot Spring Chest helper should use the loose-gem row shape after the invisible reward result.");
            }

            if (CurrentStoneHillSpringRecipeId.Contains("SafeLooseVisibleGemOneShotCleanup", StringComparison.OrdinalIgnoreCase) &&
                (!helperPayloadPatch.Description.Contains("visible-gem type word", StringComparison.OrdinalIgnoreCase) ||
                 !ContainsBytes(helperPayload, 0x40, 0xFF, 0x09, 0x3C, 0x18, 0x01, 0x29, 0x35)))
            {
                throw new InvalidOperationException("Safe loose-visible-gem one-shot Spring Chest helper should restore the visible-gem type word after the invisible reward result.");
            }

            if (CurrentStoneHillSpringRecipeId.Contains("SafeLowVisibleGemOneShotCleanup", StringComparison.OrdinalIgnoreCase) &&
                (!helperPayloadPatch.Description.Contains("Z offset 0x80", StringComparison.OrdinalIgnoreCase) ||
                 !ContainsBytes(helperPayload, 0x80, 0x00, 0x29, 0x25)))
            {
                throw new InvalidOperationException("Safe low-visible-gem one-shot Spring Chest helper should use the low reward Z offset after the hovering invisible reward result.");
            }
        }

        if (string.Equals(springCandidatePreview.RecipeId, CurrentStoneHillSpringRecipeId, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                bool containedHelperSpringCandidate = CurrentStoneHillSpringRecipeId.Contains("T80T83", StringComparison.OrdinalIgnoreCase);
                await WriteSyntheticCandidateEvidenceAsync(
                    syntheticSpringEvidencePath,
                    level,
                    CurrentStoneHillSpringRecipeId,
                    containedHelperSpringCandidate
                        ? "Spring Chest Peace Keepers T80-T83 contained-helper evidence-gated smoke"
                        : "Spring Chest Peace Keepers shared-native-special-cluster evidence-gated smoke",
                    templateId: CurrentStoneHillSpringTemplateId,
                    family: "springChest",
                    recipeStatus: "experimental-plan-only",
                    recipeFingerprint: CrossLevelCandidateRecipeFingerprint.Create(springCandidatePreview),
                    behaviorChecks: containedHelperSpringCandidate
                        ?
                        [
                            new CandidateBehaviorSmokeCheck("springchest-t80t83-contained-helper-boots", "Stone Hill boots past the flying/loading screen with native 0x0149 replacing unused 0x000E and Stone Hill's native 0x00C2 package preserved."),
                            new CandidateBehaviorSmokeCheck("springchest-t80t83-contained-helper-appears", "The Peace Keepers T80-T83 Spring Chest cluster appears at the placed location without loose green/blue reward gems spawning at load."),
                            new CandidateBehaviorSmokeCheck("springchest-t80t83-contained-helper-works", "Approaching, flaming, or charging the objects stays stable and the hidden contained-helper rows T118-T122 complete Spring Chest behavior.")
                        ]
                        :
                        [
                            new CandidateBehaviorSmokeCheck("springchest-t70t71-shared-native-special-cluster-boots", "Stone Hill boots past the flying/loading screen with native 0x0149 replacing unused 0x000E and Stone Hill's native 0x00C2 package preserved."),
                            new CandidateBehaviorSmokeCheck("springchest-t70t71-shared-native-special-cluster-appears", "The paired Peace Keepers T70/T71 Spring Chest records appear at the placed location without loose green/blue reward gems spawning at load."),
                            new CandidateBehaviorSmokeCheck("springchest-t70t71-shared-native-special-cluster-works", "Approaching, flaming, or charging the objects stays stable and the paired native spring records trigger Spring Chest behavior through their shared native special-data cluster.")
                        ]);
                MobySourcePatchPlan promotedByEvidencePlan = MobySourcePatchExporter.BuildPlan(
                    sourceImage,
                    DiscImageLocator.FindCueForImage(sourceImage),
                    Path.Combine(workspace.RootPath, "_local", "objects", "stonehill-spring-chest-peacekeepers-t70t71-shared-native-special-cluster-evidence-gated-smoke.bin"),
                    Path.Combine(workspace.RootPath, "_local", "objects", "stonehill-spring-chest-peacekeepers-t70t71-shared-native-special-cluster-evidence-gated-smoke.cue"),
                    level,
                    springCandidatePath);
                MobyActorPackageImportPreview? evidencePreview = promotedByEvidencePlan.PackageImportPreviews.FirstOrDefault(preview =>
                    string.Equals(preview.TemplateId, CurrentStoneHillSpringTemplateId, StringComparison.OrdinalIgnoreCase));
                if (evidencePreview == null ||
                    !evidencePreview.CanWriteImage ||
                    !evidencePreview.GuardReason.Contains("passed in-game evidence", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Stone Hill Peace Keepers Spring Chest recipe did not become writable after a passed in-game candidate result.");
                }
            }
            finally
            {
                File.Delete(syntheticSpringEvidencePath);
            }
        }

        if (exportCrossLevelObjectTest)
            await ExportAndVerifyMobyPatchImage(level, springCandidatePath, Path.Combine(workspace.RootPath, "_local", "objects", CurrentStoneHillSpringCandidateSlug), "Stone Hill Peace Keepers T80-T83 contained-helper no-helper Spring Chest candidate", allowPlanOnlyActorPackageImports: true);

        MobySourcePatch? sourceCountPatch = plan.Patches.FirstOrDefault(patch =>
            string.Equals(patch.Kind, "moby-source-count", StringComparison.OrdinalIgnoreCase));
        if (sourceCountPatch == null || BitConverter.ToInt32(ParseHexPreview(sourceCountPatch.AfterHexPreview), 0) != level.SourceRecordCount + 1)
            throw new InvalidOperationException("Stone Hill guarded cross-level add plan should only bump the source moby count for the safe key append.");

        if (exportCrossLevelObjectTest)
            await ExportAndVerifyMobyPatchImage(level, path, Path.Combine(workspace.RootPath, "_local", "objects", "cross-level-template-smoke-write"), "Stone Hill cross-level template");

        MobyActorPackageImportPreview? wizardPreview = plan.PackageImportPreviews.FirstOrDefault(preview =>
            string.Equals(preview.TemplateId, wizardTransform.CrossLevelTemplateId, StringComparison.OrdinalIgnoreCase));
        if (wizardPreview == null ||
            !string.Equals(wizardPreview.Family, "enemyTransform", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(wizardPreview.RecipeId) ||
            wizardPreview.CanWriteImage ||
            !wizardPreview.GuardReason.Contains("No native actor-package recipe", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cross-level wizard transform did not emit the expected guarded enemy-transform package preview.");
        }
    }

    Console.WriteLine($"Cross-level template roundtrip: {applied} edit(s), key metadata persisted, actor-package import guarded");
}

async Task ReportArtisansKeyChestPackagePreview()
{
    if (!File.Exists(sourceImage))
        return;

    LevelDefinition? artisans = catalog.FindByKey("artisans");
    if (artisans == null)
        return;

    Directory.CreateDirectory(Path.Combine(workspace.RootPath, "_local", "smoke"));
    string path = Path.Combine(workspace.RootPath, "_local", "smoke", "moby-artisans-key-chest-native-edits.json");
    Moby keyChest = new()
    {
        Index = artisans.SourceRecordCount,
        TrueIndex = artisans.SourceRecordCount,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(artisans.SourceRecordCount),
        Position = new Vector3f(0, 0, 0),
        OriginalPosition = new Vector3f(0, 0, 0),
        Type = 0x20,
        OriginalType = 0x20,
        State = 0,
        OriginalState = 0,
        SourceByte36 = 0xAE,
        OriginalSourceByte36 = 0xAE,
        SourceByte37 = 0,
        SourceByte4F = 0,
        OriginalSourceByte4F = 0,
        Flag4A = 0x10,
        Flag4B = 0x54,
        OriginalFlag4B = 0x54,
        Color = Moby.ColorForType(0x20),
        Label = "Key Chest",
        OriginalLabel = "Key Chest",
        PatchStatus = "experimental-actor-package-swap",
        PatchLead = "cross-level Artisans key chest package preview smoke",
        CrossLevelTemplateId = "common.locked_chest.peacekeepers.t79",
        CrossLevelFamily = "lockedChest",
        CrossLevelSourceLevelKey = "PeaceKeepers",
        CrossLevelSourceLevelName = "Peace Keepers",
        CrossLevelSourceTrueIndex = 79,
        CrossLevelRequiredExporterFeature = "ActorPackageSwapIntoUnusedRoot",
        IsAdded = true
    };
    Moby key = new()
    {
        Index = artisans.SourceRecordCount + 1,
        TrueIndex = artisans.SourceRecordCount + 1,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(artisans.SourceRecordCount + 1),
        Position = new Vector3f(-160, 0, 0),
        OriginalPosition = new Vector3f(-160, 0, 0),
        Type = 0x18,
        OriginalType = 0x18,
        State = 0,
        OriginalState = 0,
        SourceByte36 = 0xAD,
        OriginalSourceByte36 = 0xAD,
        SourceByte37 = 0,
        SourceByte4F = 0x02,
        OriginalSourceByte4F = 0x02,
        Flag4A = 0x40,
        Flag4B = 0xFF,
        OriginalFlag4B = 0xFF,
        Color = GemValue.Yellow.Color,
        Label = "Key",
        OriginalLabel = "Key",
        PatchStatus = "supported-lightweight-object",
        PatchLead = "cross-level Artisans key companion smoke",
        CrossLevelTemplateId = "common.key.peacekeepers.t78",
        CrossLevelFamily = "key",
        CrossLevelSourceLevelKey = "PeaceKeepers",
        CrossLevelSourceLevelName = "Peace Keepers",
        CrossLevelSourceTrueIndex = 78,
        CrossLevelRequiredExporterFeature = "DirectSourceRecordAppend",
        IsAdded = true
    };

    await MobyEditStore.SaveAsync(path, [keyChest, key], "Artisans key chest package preview smoke");
    MobySourcePatchPlan guardedPlan = MobySourcePatchExporter.BuildPlan(
        sourceImage,
        DiscImageLocator.FindCueForImage(sourceImage),
        Path.Combine(workspace.RootPath, "_local", "objects", "artisans-key-chest-preview-smoke.bin"),
        Path.Combine(workspace.RootPath, "_local", "objects", "artisans-key-chest-preview-smoke.cue"),
        artisans,
        path);
    MobyActorPackageImportPreview? guardedPreview = guardedPlan.PackageImportPreviews.FirstOrDefault(item =>
        string.Equals(item.TemplateId, "common.locked_chest.peacekeepers.t79", StringComparison.OrdinalIgnoreCase));
    if (guardedPreview == null ||
        guardedPreview.CanWriteImage ||
        !guardedPreview.GuardReason.Contains("must pass a disposable candidate test", StringComparison.OrdinalIgnoreCase) ||
        guardedPlan.Patches.Any(patch => string.Equals(patch.Kind, "actor-package-copy", StringComparison.OrdinalIgnoreCase)))
    {
        throw new InvalidOperationException("Artisans key chest should stay guarded in normal Create BIN until in-game evidence passes.");
    }

    MobySourcePatchPlan plan = MobySourcePatchExporter.BuildPlan(
        sourceImage,
        DiscImageLocator.FindCueForImage(sourceImage),
        Path.Combine(workspace.RootPath, "_local", "objects", "artisans-key-chest-preview-smoke.bin"),
        Path.Combine(workspace.RootPath, "_local", "objects", "artisans-key-chest-preview-smoke.cue"),
        artisans,
        path,
        allowPlanOnlyActorPackageImports: true);
    MobyActorPackageImportPreview? preview = plan.PackageImportPreviews.FirstOrDefault(item =>
        string.Equals(item.TemplateId, "common.locked_chest.peacekeepers.t79", StringComparison.OrdinalIgnoreCase));
    if (preview == null ||
        !string.Equals(preview.RecipeId, "artisans.peacekeepers.lockedChest.package.safeGapActorId.v4", StringComparison.OrdinalIgnoreCase) ||
        !preview.CanWriteImage ||
        !preview.GuardReason.Contains("disposable candidate mode", StringComparison.OrdinalIgnoreCase) ||
        preview.CopySegments.Count != 1 ||
        preview.RootEntries.Count != 1 ||
        preview.CopySegments.Any(segment => !segment.TargetBeforeIsZero) ||
        preview.RootEntries.Any(entry => !entry.RootSlotSafe || !entry.ActorIdSlotSafe))
    {
        throw new InvalidOperationException("Artisans key chest did not emit the expected writable actor-package preview.");
    }

    bool hasKeyChestAppend = plan.Patches.Any(patch =>
        string.Equals(patch.Kind, "moby-record-append", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(patch.MobyLabel, "Key Chest", StringComparison.OrdinalIgnoreCase));
    bool hasKeyAppend = plan.Patches.Any(patch =>
        string.Equals(patch.Kind, "moby-record-append", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(patch.MobyLabel, "Key", StringComparison.OrdinalIgnoreCase));
    bool hasKeyChestPackageCopy = plan.Patches.Any(patch =>
        string.Equals(patch.Kind, "actor-package-copy", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(patch.MobyLabel, "Key Chest", StringComparison.OrdinalIgnoreCase));
    bool hasKeyChestSpecialData = plan.Patches.Any(patch =>
        string.Equals(patch.Kind, "cross-level-moby-special-data-import", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(patch.MobyLabel, "Key Chest", StringComparison.OrdinalIgnoreCase));
    if (!hasKeyChestAppend || !hasKeyAppend || !hasKeyChestPackageCopy || !hasKeyChestSpecialData)
        throw new InvalidOperationException("Artisans key/chest writable plan is missing key, package, special-data, or source-record patches.");
    if (plan.SkippedEdits.Count != 0)
        throw new InvalidOperationException($"Artisans key/chest writable plan skipped {plan.SkippedEdits.Count} edit(s): {string.Join("; ", plan.SkippedEdits)}");

    MobySourcePatch? sourceCountPatch = plan.Patches.FirstOrDefault(patch =>
        string.Equals(patch.Kind, "moby-source-count", StringComparison.OrdinalIgnoreCase));
    if (sourceCountPatch == null || BitConverter.ToInt32(ParseHexPreview(sourceCountPatch.AfterHexPreview), 0) != artisans.SourceRecordCount + 2)
        throw new InvalidOperationException("Artisans key/chest writable plan did not bump the source moby count for both appended objects.");

    string syntheticEvidencePath = Path.Combine(workspace.RootPath, "_local", "objects", "artisans-key-chest-smoke-pass.candidate-result.json");
    try
    {
        await WriteSyntheticCandidateEvidenceAsync(
            syntheticEvidencePath,
            artisans,
            "artisans.peacekeepers.lockedChest.package.safeGapActorId.v4",
            "Artisans Key Chest evidence-gated smoke",
            templateId: "common.locked_chest.peacekeepers.t79",
            family: "lockedChest",
            recipeStatus: "experimental-image-write",
            recipeFingerprint: CrossLevelCandidateRecipeFingerprint.Create(preview),
            behaviorChecks:
            [
                new CandidateBehaviorSmokeCheck("key-visible-collectible", "Key is visible and collectible."),
                new CandidateBehaviorSmokeCheck("keychest-locked-before-key", "Key Chest stays locked before collecting the key."),
                new CandidateBehaviorSmokeCheck("keychest-opens-after-key", "Key Chest opens after collecting the key."),
                new CandidateBehaviorSmokeCheck("keychest-nearby-regression", "Nearby portals, dragons, and existing chests still behave normally.")
            ]);
        MobySourcePatchPlan promotedByEvidencePlan = MobySourcePatchExporter.BuildPlan(
            sourceImage,
            DiscImageLocator.FindCueForImage(sourceImage),
            Path.Combine(workspace.RootPath, "_local", "objects", "artisans-key-chest-evidence-gated-smoke.bin"),
            Path.Combine(workspace.RootPath, "_local", "objects", "artisans-key-chest-evidence-gated-smoke.cue"),
            artisans,
            path);
        MobyActorPackageImportPreview? evidencePreview = promotedByEvidencePlan.PackageImportPreviews.FirstOrDefault(item =>
            string.Equals(item.TemplateId, "common.locked_chest.peacekeepers.t79", StringComparison.OrdinalIgnoreCase));
        if (evidencePreview == null ||
            !evidencePreview.CanWriteImage ||
            !evidencePreview.GuardReason.Contains("passed in-game evidence", StringComparison.OrdinalIgnoreCase) ||
            promotedByEvidencePlan.SkippedEdits.Count != 0 ||
            promotedByEvidencePlan.Patches.Count(patch => string.Equals(patch.Kind, "actor-package-copy", StringComparison.OrdinalIgnoreCase)) != 1 ||
            promotedByEvidencePlan.Patches.Count(patch => string.Equals(patch.Kind, "moby-record-append", StringComparison.OrdinalIgnoreCase)) != 2 ||
            promotedByEvidencePlan.Patches.Count(patch => string.Equals(patch.Kind, "cross-level-moby-special-data-import", StringComparison.OrdinalIgnoreCase)) != 1)
        {
            throw new InvalidOperationException("Artisans Key Chest did not become writable after a passed in-game candidate result.");
        }
    }
    finally
    {
        File.Delete(syntheticEvidencePath);
    }

    if (exportCrossLevelObjectTest)
        await ExportAndVerifyMobyPatchImage(artisans, path, Path.Combine(workspace.RootPath, "_local", "objects", "artisans-key-chest-write-smoke"), "Artisans key chest", allowPlanOnlyActorPackageImports: true);

    Console.WriteLine($"Artisans key + key chest package write plan: {preview.RecipeId}, {preview.CopySegments[0].ByteLength} byte copy, root {preview.RootEntries[0].TargetRootSlot}");
}

async Task ReportToastyWizardPackageWritePlan()
{
    if (!File.Exists(sourceImage))
        return;

    LevelDefinition? toasty = catalog.FindByKey("toasty");
    if (toasty == null)
        return;

    Directory.CreateDirectory(Path.Combine(workspace.RootPath, "_local", "smoke"));
    string path = Path.Combine(workspace.RootPath, "_local", "smoke", "moby-toasty-wizard-transform-native-edits.json");
    Moby wizardDog = new()
    {
        Index = 0,
        TrueIndex = 0,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(0),
        Position = new Vector3f(0, 0, 0),
        OriginalPosition = new Vector3f(0, 0, 0),
        Type = 0x20,
        OriginalType = 0x20,
        State = 0x01,
        OriginalState = 0x00,
        SourceByte36 = 0x1B,
        OriginalSourceByte36 = 0x4F,
        SourceByte37 = 0x01,
        OriginalSourceByte37 = 0x00,
        SourceByte4F = 0x00,
        OriginalSourceByte4F = 0x00,
        Flag4A = 0x10,
        OriginalFlag4A = 0x10,
        Flag4B = 0x56,
        OriginalFlag4B = 0x53,
        Color = Moby.ColorForType(0x20),
        Label = "Green Wizard",
        OriginalLabel = "Dog",
        PatchStatus = "experimental-image-write",
        PatchLead = "cross-level Toasty dog-to-wizard package write smoke",
        CrossLevelTemplateId = "enemy.green_wizard.wizardpeak.t6",
        CrossLevelFamily = "enemyTransform",
        CrossLevelSourceLevelKey = "WizardPeak",
        CrossLevelSourceLevelName = "Wizard Peak",
        CrossLevelSourceTrueIndex = 6,
        CrossLevelRequiredExporterFeature = "ActorPackageImportAndPointerRebase"
    };

    await MobyEditStore.SaveAsync(path, [wizardDog], "Toasty wizard transform package write smoke");
    MobySourcePatchPlan guardedPlan = MobySourcePatchExporter.BuildPlan(
        sourceImage,
        DiscImageLocator.FindCueForImage(sourceImage),
        Path.Combine(workspace.RootPath, "_local", "objects", "toasty-wizard-transform-write-smoke.bin"),
        Path.Combine(workspace.RootPath, "_local", "objects", "toasty-wizard-transform-write-smoke.cue"),
        toasty,
        path);
    MobyActorPackageImportPreview? guardedPreview = guardedPlan.PackageImportPreviews.FirstOrDefault(item =>
        string.Equals(item.TemplateId, "enemy.green_wizard.wizardpeak.t6", StringComparison.OrdinalIgnoreCase));
    if (guardedPreview == null ||
        guardedPreview.CanWriteImage ||
        !guardedPreview.GuardReason.Contains("must pass a disposable candidate test", StringComparison.OrdinalIgnoreCase) ||
        guardedPlan.Patches.Any(patch => string.Equals(patch.Kind, "actor-package-copy", StringComparison.OrdinalIgnoreCase)))
    {
        throw new InvalidOperationException("Toasty wizard transform should stay guarded in normal Create BIN until in-game evidence passes.");
    }

    MobySourcePatchPlan plan = MobySourcePatchExporter.BuildPlan(
        sourceImage,
        DiscImageLocator.FindCueForImage(sourceImage),
        Path.Combine(workspace.RootPath, "_local", "objects", "toasty-wizard-transform-write-smoke.bin"),
        Path.Combine(workspace.RootPath, "_local", "objects", "toasty-wizard-transform-write-smoke.cue"),
        toasty,
        path,
        allowPlanOnlyActorPackageImports: true);

    MobyActorPackageImportPreview? preview = plan.PackageImportPreviews.FirstOrDefault(item =>
        string.Equals(item.TemplateId, "enemy.green_wizard.wizardpeak.t6", StringComparison.OrdinalIgnoreCase));
    if (preview == null ||
        !string.Equals(preview.RecipeId, "toasty.wizardpeak.greenWizard.package.overwriteUnused00EA.v2", StringComparison.OrdinalIgnoreCase) ||
        !string.Equals(preview.RecipeStatus, "experimental-image-write", StringComparison.OrdinalIgnoreCase) ||
        !preview.CanWriteImage ||
        !preview.GuardReason.Contains("disposable candidate mode", StringComparison.OrdinalIgnoreCase) ||
        preview.CopySegments.Count != 1 ||
        preview.RootEntries.Count != 1 ||
        preview.CopySegments.Any(segment => segment.TargetBeforeIsZero) ||
        preview.CopySegments.Any(segment => !string.Equals(segment.TargetSafety, "overwrite-unused-actor-package", StringComparison.OrdinalIgnoreCase)) ||
        preview.RootEntries.Any(entry => !entry.RootSlotSafe || !entry.ActorIdSlotSafe))
    {
        throw new InvalidOperationException("Toasty wizard transform did not emit the expected writable package plan.");
    }

    bool hasIdentityPatch = plan.Patches.Any(patch =>
        patch.TrueIndex == 0 &&
        (string.Equals(patch.RecordOffset, "0x36", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(patch.RecordOffset, "0x37", StringComparison.OrdinalIgnoreCase)));
    bool hasPackageCopy = plan.Patches.Any(patch =>
        string.Equals(patch.Kind, "actor-package-copy", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(patch.MobyLabel, "Green Wizard", StringComparison.OrdinalIgnoreCase));
    bool hasRootRegister = plan.Patches.Any(patch =>
        string.Equals(patch.Kind, "actor-package-root-register", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(patch.MobyLabel, "Green Wizard", StringComparison.OrdinalIgnoreCase));
    bool hasSpecialImport = plan.Patches.Any(patch =>
        string.Equals(patch.Kind, "cross-level-existing-moby-special-data-import", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(patch.MobyLabel, "Green Wizard", StringComparison.OrdinalIgnoreCase));
    bool hasSpecialPointer = plan.Patches.Any(patch =>
        string.Equals(patch.Kind, "cross-level-existing-moby-special-pointer", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(patch.MobyLabel, "Green Wizard", StringComparison.OrdinalIgnoreCase));
    bool skippedPackage = plan.SkippedEdits.Any(skip =>
        skip.Contains("enemy.green_wizard.wizardpeak.t6", StringComparison.OrdinalIgnoreCase) &&
        skip.Contains("ActorPackageImportAndPointerRebase", StringComparison.OrdinalIgnoreCase));
    if (!hasIdentityPatch || !hasPackageCopy || !hasRootRegister || !hasSpecialImport || !hasSpecialPointer || skippedPackage)
        throw new InvalidOperationException("Toasty wizard transform writable plan is missing package, identity, or special-data patches.");
    if (plan.SkippedEdits.Count != 0)
        throw new InvalidOperationException($"Toasty wizard transform writable plan skipped {plan.SkippedEdits.Count} edit(s): {string.Join("; ", plan.SkippedEdits)}");
    if (plan.Patches.Any(patch => string.Equals(patch.Kind, "moby-source-count", StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException("Toasty wizard transform should patch the existing dog record, not append a new source moby.");

    string mismatchedEvidencePath = Path.Combine(workspace.RootPath, "_local", "objects", "toasty-wizard-transform-mismatched-fingerprint-pass.candidate-result.json");
    try
    {
        await WriteSyntheticCandidateEvidenceAsync(
            mismatchedEvidencePath,
            toasty,
            "toasty.wizardpeak.greenWizard.package.overwriteUnused00EA.v2",
            "Synthetic Toasty Wizard pass with the wrong package fingerprint; normal export must not trust it.",
            templateId: "enemy.green_wizard.wizardpeak.t6",
            family: "enemyTransform",
            recipeStatus: "experimental-image-write",
            recipeFingerprint: "mismatched-fingerprint",
            behaviorChecks:
            [
                new CandidateBehaviorSmokeCheck("enemy-appears-as-wizard", "Transformed enemy appears as a Green Wizard.")
            ]);
        MobySourcePatchPlan mismatchedEvidencePlan = MobySourcePatchExporter.BuildPlan(
            sourceImage,
            DiscImageLocator.FindCueForImage(sourceImage),
            Path.Combine(workspace.RootPath, "_local", "objects", "toasty-wizard-transform-mismatched-fingerprint-gated-smoke.bin"),
            Path.Combine(workspace.RootPath, "_local", "objects", "toasty-wizard-transform-mismatched-fingerprint-gated-smoke.cue"),
            toasty,
            path);
        MobyActorPackageImportPreview? mismatchedEvidencePreview = mismatchedEvidencePlan.PackageImportPreviews.FirstOrDefault(item =>
            string.Equals(item.TemplateId, "enemy.green_wizard.wizardpeak.t6", StringComparison.OrdinalIgnoreCase));
        if (mismatchedEvidencePreview == null ||
            mismatchedEvidencePreview.CanWriteImage ||
            !mismatchedEvidencePreview.GuardReason.Contains("fingerprint changed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A Toasty Wizard pass with a mismatched package fingerprint should not promote normal Create BIN.");
        }
    }
    finally
    {
        File.Delete(mismatchedEvidencePath);
    }

    string evidenceOldPassPath = Path.Combine(workspace.RootPath, "_local", "objects", "toasty-wizard-transform-evidence-order-old-pass.candidate-result.json");
    string evidenceNewFailPath = Path.Combine(workspace.RootPath, "_local", "objects", "toasty-wizard-transform-evidence-order-new-fail.candidate-result.json");
    try
    {
        await WriteSyntheticCandidateEvidenceAsync(
            evidenceOldPassPath,
            toasty,
            "toasty.wizardpeak.greenWizard.package.overwriteUnused00EA.v2",
            "Older Toasty Wizard pass used only to prove newer failures re-guard the recipe.",
            templateId: "enemy.green_wizard.wizardpeak.t6",
            family: "enemyTransform",
            recipeStatus: "experimental-image-write",
            recipeFingerprint: CrossLevelCandidateRecipeFingerprint.Create(preview),
            behaviorChecks:
            [
                new CandidateBehaviorSmokeCheck("enemy-appears-as-wizard", "Transformed enemy appears as a Green Wizard.")
            ]);
        await WriteSyntheticCandidateEvidenceAsync(
            evidenceNewFailPath,
            toasty,
            "toasty.wizardpeak.greenWizard.package.overwriteUnused00EA.v2",
            "Newer Toasty Wizard failure used only to prove the evidence gate is time-aware.",
            templateId: "enemy.green_wizard.wizardpeak.t6",
            family: "enemyTransform",
            recipeStatus: "experimental-image-write",
            recipeFingerprint: CrossLevelCandidateRecipeFingerprint.Create(preview),
            behaviorChecks:
            [
                new CandidateBehaviorSmokeCheck("enemy-appears-as-wizard", "Transformed enemy appears as a Green Wizard.")
            ],
            passed: false);
        DateTime oldStamp = DateTime.UtcNow.AddMinutes(-10);
        DateTime newStamp = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(evidenceOldPassPath, oldStamp);
        File.SetLastWriteTimeUtc(evidenceNewFailPath, newStamp);

        MobySourcePatchPlan newerFailPlan = MobySourcePatchExporter.BuildPlan(
            sourceImage,
            DiscImageLocator.FindCueForImage(sourceImage),
            Path.Combine(workspace.RootPath, "_local", "objects", "toasty-wizard-transform-newer-fail-gated-smoke.bin"),
            Path.Combine(workspace.RootPath, "_local", "objects", "toasty-wizard-transform-newer-fail-gated-smoke.cue"),
            toasty,
            path);
        MobyActorPackageImportPreview? newerFailPreview = newerFailPlan.PackageImportPreviews.FirstOrDefault(item =>
            string.Equals(item.TemplateId, "enemy.green_wizard.wizardpeak.t6", StringComparison.OrdinalIgnoreCase));
        if (newerFailPreview == null ||
            newerFailPreview.CanWriteImage ||
            !newerFailPreview.GuardReason.Contains("must pass a disposable candidate test", StringComparison.OrdinalIgnoreCase) ||
            newerFailPlan.Patches.Any(patch => string.Equals(patch.Kind, "actor-package-copy", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Newer failed Toasty Wizard evidence should re-guard the recipe even if an older pass exists.");
        }

        File.SetLastWriteTimeUtc(evidenceOldPassPath, DateTime.UtcNow);
        File.SetLastWriteTimeUtc(evidenceNewFailPath, DateTime.UtcNow.AddMinutes(-10));
        MobySourcePatchPlan newerPassPlan = MobySourcePatchExporter.BuildPlan(
            sourceImage,
            DiscImageLocator.FindCueForImage(sourceImage),
            Path.Combine(workspace.RootPath, "_local", "objects", "toasty-wizard-transform-newer-pass-gated-smoke.bin"),
            Path.Combine(workspace.RootPath, "_local", "objects", "toasty-wizard-transform-newer-pass-gated-smoke.cue"),
            toasty,
            path);
        MobyActorPackageImportPreview? newerPassPreview = newerPassPlan.PackageImportPreviews.FirstOrDefault(item =>
            string.Equals(item.TemplateId, "enemy.green_wizard.wizardpeak.t6", StringComparison.OrdinalIgnoreCase));
        if (newerPassPreview == null ||
            !newerPassPreview.CanWriteImage ||
            !newerPassPreview.GuardReason.Contains("passed in-game evidence", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Newest passed Toasty Wizard evidence should promote the recipe after an older failure.");
        }
    }
    finally
    {
        File.Delete(evidenceOldPassPath);
        File.Delete(evidenceNewFailPath);
    }

    string syntheticEvidencePath = Path.Combine(workspace.RootPath, "_local", "objects", "toasty-wizard-transform-smoke-pass.candidate-result.json");
    try
    {
        await WriteSyntheticCandidateEvidenceAsync(
            syntheticEvidencePath,
            toasty,
            "toasty.wizardpeak.greenWizard.package.overwriteUnused00EA.v2",
            "Toasty Wizard evidence-gated smoke",
            templateId: "enemy.green_wizard.wizardpeak.t6",
            family: "enemyTransform",
            recipeStatus: "experimental-image-write",
            recipeFingerprint: CrossLevelCandidateRecipeFingerprint.Create(preview),
            behaviorChecks:
            [
                new CandidateBehaviorSmokeCheck("enemy-appears-as-wizard", "Transformed enemy appears as a Green Wizard."),
                new CandidateBehaviorSmokeCheck("enemy-defeatable", "Green Wizard can be defeated."),
                new CandidateBehaviorSmokeCheck("enemy-reward-sane", "Reward or collection behavior is sane."),
                new CandidateBehaviorSmokeCheck("enemy-nearby-regression", "Other Toasty enemies still behave normally.")
            ]);
        MobySourcePatchPlan promotedByEvidencePlan = MobySourcePatchExporter.BuildPlan(
            sourceImage,
            DiscImageLocator.FindCueForImage(sourceImage),
            Path.Combine(workspace.RootPath, "_local", "objects", "toasty-wizard-transform-evidence-gated-smoke.bin"),
            Path.Combine(workspace.RootPath, "_local", "objects", "toasty-wizard-transform-evidence-gated-smoke.cue"),
            toasty,
            path);
        MobyActorPackageImportPreview? evidencePreview = promotedByEvidencePlan.PackageImportPreviews.FirstOrDefault(item =>
            string.Equals(item.TemplateId, "enemy.green_wizard.wizardpeak.t6", StringComparison.OrdinalIgnoreCase));
        if (evidencePreview == null ||
            !evidencePreview.CanWriteImage ||
            !evidencePreview.GuardReason.Contains("passed in-game evidence", StringComparison.OrdinalIgnoreCase) ||
            promotedByEvidencePlan.SkippedEdits.Count != 0 ||
            promotedByEvidencePlan.Patches.Count(patch => string.Equals(patch.Kind, "actor-package-copy", StringComparison.OrdinalIgnoreCase)) != 1 ||
            promotedByEvidencePlan.Patches.Count(patch => string.Equals(patch.Kind, "actor-package-root-register", StringComparison.OrdinalIgnoreCase)) != 1 ||
            promotedByEvidencePlan.Patches.Count(patch => string.Equals(patch.Kind, "cross-level-existing-moby-special-data-import", StringComparison.OrdinalIgnoreCase)) != 1 ||
            promotedByEvidencePlan.Patches.Count(patch => string.Equals(patch.Kind, "cross-level-existing-moby-special-pointer", StringComparison.OrdinalIgnoreCase)) != 1 ||
            promotedByEvidencePlan.Patches.Any(patch => string.Equals(patch.Kind, "moby-source-count", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Toasty Green Wizard transform did not become writable after a passed in-game candidate result.");
        }
    }
    finally
    {
        File.Delete(syntheticEvidencePath);
    }

    if (exportCrossLevelObjectTest)
        await ExportAndVerifyMobyPatchImage(toasty, path, Path.Combine(workspace.RootPath, "_local", "objects", "toasty-wizard-transform-write-smoke"), "Toasty wizard transform", allowPlanOnlyActorPackageImports: true);

    Console.WriteLine($"Toasty wizard transform package write plan: {preview.RecipeId}, overwrite {preview.CopySegments[0].TargetStart}+0x{preview.CopySegments[0].ByteLength:X}, root {preview.RootEntries[0].TargetRootSlot}");
}

async Task ExportCurrentStoneHillSpringCandidateOnly(LevelDefinition level)
{
    if (!File.Exists(sourceImage))
        throw new FileNotFoundException("Could not find the source Spyro BIN image for the current Spring Chest candidate export.", sourceImage);

    string objectsDir = Path.Combine(workspace.RootPath, "_local", "objects");
    Directory.CreateDirectory(objectsDir);
    string nativeEditsPath = Path.Combine(objectsDir, $"{CurrentStoneHillSpringCandidateSlug}-native-edits.json");
    if (!File.Exists(nativeEditsPath))
    {
        string previousCandidatePath = CurrentStoneHillSpringRecipeId.Contains("LocalController", StringComparison.OrdinalIgnoreCase) ||
            CurrentStoneHillSpringRecipeId.Contains("NativeD8Companion", StringComparison.OrdinalIgnoreCase) ||
            CurrentStoneHillSpringRecipeId.Contains("NativeD8DataLocalC2Companion", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t70-local-controller-runtime01a6-safe-preinitialized-visiblegem-hangreturnarc-postreturn-full-quarantine-delayedcollect-rearm-stack-helper-candidate-native-edits.json")
            : Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t70t71-shared-native-special-cluster-contextonly-nohelper-candidate-native-edits.json");
        if (!File.Exists(previousCandidatePath))
            throw new FileNotFoundException("Could not find a reusable Stone Hill Spring Chest native edit manifest.", previousCandidatePath);
        File.Copy(previousCandidatePath, nativeEditsPath, overwrite: true);
    }
    if (CurrentStoneHillSpringRecipeId.Contains("NativeD8Companion", StringComparison.OrdinalIgnoreCase) ||
        CurrentStoneHillSpringRecipeId.Contains("NativeD8DataLocalC2Companion", StringComparison.OrdinalIgnoreCase))
        RewriteCurrentSpringCandidateAsNativeD8Companion(nativeEditsPath);
    if (CurrentStoneHillSpringRecipeId.Contains("T80T83", StringComparison.OrdinalIgnoreCase) ||
        CurrentStoneHillSpringRecipeId.Contains("T80Only", StringComparison.OrdinalIgnoreCase))
        RewriteCurrentSpringCandidateAsT80T83ContainedHelperCluster(nativeEditsPath);
    if (CurrentStoneHillSpringRecipeId.Contains("LinkedContainedGem", StringComparison.OrdinalIgnoreCase))
        AddSpringChestLinkedContainedGemEdit(nativeEditsPath);
    if (CurrentStoneHillSpringRecipeId.Contains("DryCanyon", StringComparison.OrdinalIgnoreCase))
        RewriteCurrentSpringCandidateAsDryCanyonT107(nativeEditsPath);
    if (CurrentStoneHillSpringRecipeId.Contains("StaticNativePreHitSource", StringComparison.OrdinalIgnoreCase) ||
        CurrentStoneHillSpringRecipeId.Contains("NativePreHitShellLifecycle", StringComparison.OrdinalIgnoreCase) ||
        CurrentStoneHillSpringRecipeId.Contains("ForcedNativePreHitLifecycle", StringComparison.OrdinalIgnoreCase))
        AddSpringChestNativePreHitShellLifecycleSourceByteEdits(nativeEditsPath);
    else if (CurrentStoneHillSpringRecipeId.Contains("BluePreHit", StringComparison.OrdinalIgnoreCase))
        AddSpringChestBluePreHitSourceByteEdits(nativeEditsPath);
    else if (!CurrentStoneHillSpringRecipeId.Contains("SourceNative", StringComparison.OrdinalIgnoreCase) &&
        !CurrentStoneHillSpringRecipeId.Contains("NativePreHit", StringComparison.OrdinalIgnoreCase))
        AddSpringChestRuntimeArmedSourceByteEdits(nativeEditsPath);
    PinCurrentSpringCandidateRecipe(nativeEditsPath);

    string outputPrefix = Path.Combine(objectsDir, CurrentStoneHillSpringCandidateSlug);
    MobySourcePatchPlan plan = MobySourcePatchExporter.BuildPlan(
        sourceImage,
        DiscImageLocator.FindCueForImage(sourceImage),
        $"{outputPrefix}.bin",
        $"{outputPrefix}.cue",
        level,
        nativeEditsPath,
        allowPlanOnlyActorPackageImports: true);
    MobyActorPackageImportPreview? preview = plan.PackageImportPreviews.FirstOrDefault(item =>
        string.Equals(item.TemplateId, CurrentStoneHillSpringTemplateId, StringComparison.OrdinalIgnoreCase));
    if (preview == null ||
        !string.Equals(preview.RecipeId, CurrentStoneHillSpringRecipeId, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("The current Stone Hill Spring Chest candidate did not resolve to the expected recipe.");
    }

    if (plan.Patches.Any(patch => patch.Kind.Contains("context", StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException("The reward-only Spring Chest candidate unexpectedly emitted copied context rows.");

    await ExportAndVerifyMobyPatchImage(
        level,
        nativeEditsPath,
        outputPrefix,
        CurrentStoneHillSpringRecipeId.Contains("DryCanyon", StringComparison.OrdinalIgnoreCase)
            ? "Stone Hill Dry Canyon T107 rebased native-helper Spring Chest candidate"
            : CurrentStoneHillSpringRecipeId.Contains("T80Only", StringComparison.OrdinalIgnoreCase)
            ? "Stone Hill Peace Keepers T80 single native Spring Chest candidate"
            : CurrentStoneHillSpringRecipeId.Contains("NoContainedHelperRows", StringComparison.OrdinalIgnoreCase)
            ? "Stone Hill Peace Keepers T80-T83 native no-contained-helper Spring Chest candidate"
            : "Stone Hill Peace Keepers T80-T83 native contained-helper Spring Chest candidate",
        allowPlanOnlyActorPackageImports: true);
}

void RewriteCurrentSpringCandidateAsDryCanyonT107(string nativeEditsPath)
{
    JsonNode? root = JsonNode.Parse(File.ReadAllText(nativeEditsPath));
    JsonArray? edits = root?["edits"] as JsonArray;
    if (root == null || edits == null)
        throw new InvalidOperationException("Spring Chest Dry Canyon manifest did not contain an edits array.");

    JsonObject? shell = edits
        .OfType<JsonObject>()
        .FirstOrDefault(edit => string.Equals(edit["crossLevelTemplate"]?["family"]?.GetValue<string>(), "springChest", StringComparison.OrdinalIgnoreCase));
    if (shell == null)
        throw new InvalidOperationException("Spring Chest Dry Canyon manifest did not contain a reusable spring edit.");

    int rawX = shell["rawEdited"]?["x"]?.GetValue<int>() ?? 149115;
    int rawY = shell["rawEdited"]?["y"]?.GetValue<int>() ?? 135229;
    int rawZ = shell["rawEdited"]?["z"]?.GetValue<int>() ?? 20480;

    JsonObject edit = JsonNode.Parse(shell.ToJsonString())!.AsObject();
    edit["index"] = 195;
    edit["trueIndex"] = 195;
    edit["label"] = "Spring Chest";
    edit["labelEdited"] = "Spring Chest";
    edit["patchLead"] = "cross-level Dry Canyon T107 native spring chest helper/root diagnostic";
    edit["typeHex"] = "0x20";
    edit["typeOriginalHex"] = "0x20";
    edit["typeEditedHex"] = "0x20";
    edit["sourceByte36Hex"] = "0x49";
    edit["sourceByte36OriginalHex"] = "0x49";
    edit["sourceByte36EditedHex"] = "0x49";
    edit["sourceByte37Hex"] = "0x01";
    edit["sourceByte37OriginalHex"] = "0x01";
    edit["sourceByte37EditedHex"] = "0x01";
    edit["flag4AHex"] = "0xFF";
    edit["flag4AOriginalHex"] = "0xFF";
    edit["flag4AEditedHex"] = "0xFF";
    edit["flag4BHex"] = "0x55";
    edit["flag4BOriginalHex"] = "0x55";
    edit["flag4BEditedHex"] = "0x55";
    edit["sourceByteEdits"] = new JsonArray
    {
        new JsonObject
        {
            ["offset"] = 0x52,
            ["offsetHex"] = "0x52",
            ["value"] = 0xFF,
            ["valueHex"] = "0xFF",
            ["field"] = "flag4A-identity-byte"
        }
    };
    edit["crossLevelTemplate"] = new JsonObject
    {
        ["id"] = "common.spring_chest.drycanyon.t107",
        ["family"] = "springChest",
        ["sourceLevelKey"] = "DryCanyon",
        ["sourceLevelName"] = "Dry Canyon",
        ["sourceTrueIndex"] = 107,
        ["addSupportStatus"] = "experimental-actor-package-swap",
        ["requiredExporterFeature"] = "ActorPackageSwapIntoUnusedRoot",
        ["recipeId"] = CurrentStoneHillSpringRecipeId
    };
    RewritePosition(edit, rawX, rawY, rawZ);

    JsonArray rewritten = [];
    rewritten.Add(edit);
    root["levelName"] = "Stone Hill Spring Chest Dry Canyon T107 helper-package candidate";
    root["editCount"] = 1;
    root["edits"] = rewritten;
    File.WriteAllText(nativeEditsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
}

void AddSpringChestLinkedContainedGemEdit(string nativeEditsPath)
{
    JsonNode? root = JsonNode.Parse(File.ReadAllText(nativeEditsPath));
    JsonArray? edits = root?["edits"] as JsonArray;
    if (root == null || edits == null || edits.Count == 0)
        throw new InvalidOperationException("Spring Chest linked contained-gem manifest did not contain a spring edit.");

    JsonObject? shell = edits
        .OfType<JsonObject>()
        .FirstOrDefault(edit => string.Equals(edit["crossLevelTemplate"]?["family"]?.GetValue<string>(), "springChest", StringComparison.OrdinalIgnoreCase));
    if (shell == null)
        throw new InvalidOperationException("Spring Chest linked contained-gem manifest did not contain the Spring Chest shell edit.");

    int shellTrueIndex = shell["trueIndex"]?.GetValue<int>() ?? 195;
    int gemTrueIndex = shellTrueIndex + 1;
    int rawX = shell["rawEdited"]?["x"]?.GetValue<int>() ?? 149115;
    int rawY = shell["rawEdited"]?["y"]?.GetValue<int>() ?? 135229;
    int rawZ = shell["rawEdited"]?["z"]?.GetValue<int>() ?? 20480;
    JsonObject position = new()
    {
        ["x"] = rawX / 16f,
        ["y"] = rawY / 16f,
        ["z"] = rawZ / 16f
    };
    JsonObject rawPosition = new()
    {
        ["x"] = rawX,
        ["y"] = rawY,
        ["z"] = rawZ
    };

    JsonObject gem = new()
    {
        ["editKind"] = "add",
        ["index"] = gemTrueIndex,
        ["trueIndex"] = gemTrueIndex,
        ["legacyIndex"] = null,
        ["label"] = "Spring Chest linked blue gem",
        ["labelOriginal"] = "Spring Chest linked blue gem",
        ["labelEdited"] = "Spring Chest linked blue gem",
        ["typeHex"] = "0x00",
        ["typeOriginalHex"] = "0x00",
        ["typeEditedHex"] = "0x00",
        ["stateHex"] = "0x00",
        ["stateOriginalHex"] = "0x00",
        ["stateEditedHex"] = "0x00",
        ["removed"] = false,
        ["added"] = true,
        ["runtimeAddress"] = "0x00000000",
        ["specialDataPointer"] = "0x00000000",
        ["sourceByte36Hex"] = "0x0D",
        ["sourceByte36OriginalHex"] = "0x0D",
        ["sourceByte36EditedHex"] = "0x0D",
        ["sourceByte37Hex"] = "0x00",
        ["sourceByte37OriginalHex"] = "0x00",
        ["sourceByte37EditedHex"] = "0x00",
        ["sourceByte4FHex"] = "0x00",
        ["sourceByte4FOriginalHex"] = "0x00",
        ["sourceByte4FEditedHex"] = "0x00",
        ["flag4AHex"] = "0xFF",
        ["flag4AOriginalHex"] = "0xFF",
        ["flag4AEditedHex"] = "0xFF",
        ["flag4BHex"] = "0x55",
        ["flag4BOriginalHex"] = "0x55",
        ["flag4BEditedHex"] = "0x55",
        ["patchStatus"] = "experimental-contained-gem-link",
        ["patchLead"] = "same-level contained-gem special-data link to imported Spring Chest",
        ["crossLevelTemplate"] = null,
        ["gem"] = new JsonObject
        {
            ["name"] = "Blue gem",
            ["value"] = 5,
            ["idByte"] = 0x55
        },
        ["sourceByteEdits"] = new JsonArray(),
        ["chestContentLinkEdit"] = new JsonObject
        {
            ["mode"] = "contained-gem-chest-link",
            ["chestIndex"] = shellTrueIndex,
            ["chestTrueIndex"] = shellTrueIndex,
            ["chestLabel"] = "Spring Chest",
            ["rawOffset"] = new JsonObject
            {
                ["x"] = 0,
                ["y"] = 0,
                ["z"] = 0
            },
            ["note"] = "Patch contained-gem special data +0x00 to the imported Spring Chest true index and keep the reward centered on the chest."
        },
        ["original"] = position.DeepClone(),
        ["edited"] = position.DeepClone(),
        ["rawOriginal"] = rawPosition.DeepClone(),
        ["rawEdited"] = rawPosition.DeepClone(),
        ["rawDelta"] = new JsonObject
        {
            ["x"] = 0,
            ["y"] = 0,
            ["z"] = 0
        }
    };

    edits.Add(gem);
    root["levelName"] = "Stone Hill Spring Chest Peace Keepers T80 linked contained-gem candidate";
    root["editCount"] = edits.Count;
    File.WriteAllText(nativeEditsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
}

void PinCurrentSpringCandidateRecipe(string nativeEditsPath)
{
    JsonNode? root = JsonNode.Parse(File.ReadAllText(nativeEditsPath));
    JsonArray? edits = root?["edits"] as JsonArray;
    if (root == null || edits == null)
        throw new InvalidOperationException("Spring Chest manifest did not contain an edits array.");

    foreach (JsonObject edit in edits.OfType<JsonObject>())
    {
        JsonObject? template = edit["crossLevelTemplate"] as JsonObject;
        if (template == null)
            continue;
        if (!string.Equals(template["family"]?.GetValue<string>(), "springChest", StringComparison.OrdinalIgnoreCase))
            continue;

        template["recipeId"] = CurrentStoneHillSpringRecipeId;
    }

    File.WriteAllText(nativeEditsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
}

void RewriteCurrentSpringCandidateAsT80T83ContainedHelperCluster(string nativeEditsPath)
{
    JsonNode? root = JsonNode.Parse(File.ReadAllText(nativeEditsPath));
    JsonArray? edits = root?["edits"] as JsonArray;
    if (root == null || edits == null)
        throw new InvalidOperationException("Spring Chest T80-T83 manifest did not contain an edits array.");

    JsonObject? shell = edits
        .OfType<JsonObject>()
        .FirstOrDefault(edit => string.Equals(edit["crossLevelTemplate"]?["family"]?.GetValue<string>(), "springChest", StringComparison.OrdinalIgnoreCase));
    if (shell == null)
        throw new InvalidOperationException("Spring Chest T80-T83 manifest did not contain a reusable spring edit.");

    int baseRawX = shell["rawEdited"]?["x"]?.GetValue<int>() ?? 149115;
    int baseRawY = shell["rawEdited"]?["y"]?.GetValue<int>() ?? 135229;
    int baseRawZ = shell["rawEdited"]?["z"]?.GetValue<int>() ?? 20480;
    (int SourceTrueIndex, int Flag4B, int DeltaX, int DeltaY, int DeltaZ, string TemplateId)[] cluster =
        CurrentStoneHillSpringRecipeId.Contains("T80Only", StringComparison.OrdinalIgnoreCase)
            ? [(80, 0x55, 0, 0, 0, "common.spring_chest.peacekeepers.t70")]
            :
            [
                (80, 0x55, 0, 0, 0, "common.spring_chest.peacekeepers.t70"),
                (81, 0x55, 3184, 1649, 0, "common.spring_chest.peacekeepers.t70"),
                (82, 0x55, 2529, 2960, 0, "common.spring_chest.peacekeepers.t70"),
                (83, 0x54, 1300, 226, 0, "common.spring_chest.peacekeepers.t71")
            ];

    JsonArray rewritten = [];
    for (int i = 0; i < cluster.Length; i++)
    {
        (int sourceTrueIndex, int flag4B, int deltaX, int deltaY, int deltaZ, string templateId) = cluster[i];
        JsonObject edit = JsonNode.Parse(shell.ToJsonString())!.AsObject();
        int rawX = baseRawX + deltaX;
        int rawY = baseRawY + deltaY;
        int rawZ = baseRawZ + deltaZ;
        edit["index"] = 195 + i;
        edit["trueIndex"] = 195 + i;
        edit["label"] = "Spring Chest";
        edit["labelEdited"] = "Spring Chest";
        edit["patchLead"] = $"cross-level Peace Keepers T{sourceTrueIndex} native spring chest cluster smoke with contained helper rows";
        edit["flag4AHex"] = "0xFF";
        edit["flag4AEditedHex"] = "0xFF";
        edit["flag4BHex"] = $"0x{flag4B:X2}";
        edit["flag4BOriginalHex"] = $"0x{flag4B:X2}";
        edit["flag4BEditedHex"] = $"0x{flag4B:X2}";
        edit["crossLevelTemplate"]!["id"] = templateId;
        edit["crossLevelTemplate"]!["sourceTrueIndex"] = sourceTrueIndex;
        edit["sourceByteEdits"] = new JsonArray
        {
            new JsonObject
            {
                ["offset"] = 0x52,
                ["offsetHex"] = "0x52",
                ["value"] = 0xFF,
                ["valueHex"] = "0xFF",
                ["field"] = "flag4A-identity-byte"
            }
        };
        RewritePosition(edit, rawX, rawY, rawZ);
        rewritten.Add(edit);
    }

    root["levelName"] = CurrentStoneHillSpringRecipeId.Contains("T80Only", StringComparison.OrdinalIgnoreCase)
        ? "Stone Hill Spring Chest Peace Keepers T80 single-row candidate"
        : "Stone Hill Spring Chest Peace Keepers T80-T83 contained-helper candidate";
    root["editCount"] = rewritten.Count;
    root["edits"] = rewritten;
    File.WriteAllText(nativeEditsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
}

void RewritePosition(JsonObject edit, int rawX, int rawY, int rawZ)
{
    JsonObject edited = new()
    {
        ["x"] = rawX / 16f,
        ["y"] = rawY / 16f,
        ["z"] = rawZ / 16f
    };
    JsonObject rawEdited = new()
    {
        ["x"] = rawX,
        ["y"] = rawY,
        ["z"] = rawZ
    };
    edit["edited"] = edited.DeepClone();
    edit["original"] = edited.DeepClone();
    edit["rawEdited"] = rawEdited.DeepClone();
    edit["rawOriginal"] = rawEdited.DeepClone();
    edit["rawDelta"] = new JsonObject
    {
        ["x"] = 0,
        ["y"] = 0,
        ["z"] = 0
    };
}

void RewriteCurrentSpringCandidateAsNativeD8Companion(string nativeEditsPath)
{
    JsonNode? root = JsonNode.Parse(File.ReadAllText(nativeEditsPath));
    JsonArray? edits = root?["edits"] as JsonArray;
    if (root == null || edits == null)
        throw new InvalidOperationException("Spring Chest native companion manifest did not contain an edits array.");

    JsonObject? shell = edits
        .OfType<JsonObject>()
        .FirstOrDefault(edit => string.Equals(edit["crossLevelTemplate"]?["id"]?.GetValue<string>(), CurrentStoneHillSpringTemplateId, StringComparison.OrdinalIgnoreCase));
    if (shell == null)
        throw new InvalidOperationException("Spring Chest native companion manifest did not contain the visible T70 shell edit.");

    JsonObject companion = JsonNode.Parse(shell.ToJsonString())!.AsObject();
    shell["sourceByteEdits"] = new JsonArray();
    companion["label"] = "Spring Chest native companion";
    companion["labelEdited"] = "Spring Chest native companion";
    companion["candidateKind"] = "Control";
    companion["behaviorNote"] = "Native Peace Keepers 0x00D8 companion row observed beside working Spring Chest T70/T71 in RAM.";
    companion["patchLead"] = "cross-level Peace Keepers T113 native 0x00D8 companion for T70 spring chest diagnostic";
    bool localC2Companion = CurrentStoneHillSpringRecipeId.Contains("NativeD8DataLocalC2Companion", StringComparison.OrdinalIgnoreCase);
    if (localC2Companion)
    {
        companion["label"] = "Spring Chest native-data local controller";
        companion["labelEdited"] = "Spring Chest native-data local controller";
        companion["behaviorNote"] = "Peace Keepers T113 companion data running through Stone Hill's native 0x00C2 chest/controller actor.";
        companion["patchLead"] = "cross-level Peace Keepers T113 native companion-data diagnostic using Stone Hill 0x00C2 actor";
    }
    companion["sourceByte36EditedHex"] = localC2Companion ? "0xC2" : "0xD8";
    companion["sourceByte37EditedHex"] = "0x00";
    companion["flag4AEditedHex"] = "0x10";
    companion["flag4BEditedHex"] = "0x55";
    companion["sourceByte4FEditedHex"] = "0x00";
    companion["sourceByteEdits"] = new JsonArray();
    companion["crossLevelTemplate"] = new JsonObject
    {
        ["id"] = localC2Companion
            ? "common.spring_chest_companion.peacekeepers.t113.localc2"
            : "common.spring_chest_companion.peacekeepers.t113",
        ["family"] = "springChest",
        ["sourceLevelKey"] = "PeaceKeepers",
        ["sourceLevelName"] = "Peace Keepers",
        ["sourceTrueIndex"] = 113,
        ["addSupportStatus"] = "experimental-actor-package-swap",
        ["requiredExporterFeature"] = "ActorPackageSwapIntoUnusedRoot"
    };

    OffsetNativeD8CompanionPosition(companion);
    edits.Clear();
    edits.Add(companion);
    edits.Add(shell);
    File.WriteAllText(nativeEditsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
}

void OffsetNativeD8CompanionPosition(JsonObject companion)
{
    if (companion["edited"] is JsonObject edited)
    {
        edited["x"] = (edited["x"]?.GetValue<float>() ?? 0) + 139.5f;
        edited["y"] = (edited["y"]?.GetValue<float>() ?? 0) + 56.9375f;
        edited["z"] = (edited["z"]?.GetValue<float>() ?? 0) - 4.625f;
    }
    if (companion["rawEdited"] is JsonObject rawEdited)
    {
        rawEdited["x"] = (rawEdited["x"]?.GetValue<int>() ?? 0) + 2232;
        rawEdited["y"] = (rawEdited["y"]?.GetValue<int>() ?? 0) + 911;
        rawEdited["z"] = (rawEdited["z"]?.GetValue<int>() ?? 0) - 74;
    }
}

async Task ExportAndVerifyMobyPatchImage(LevelDefinition level, string nativeEditsPath, string outputPrefix, string label, bool allowPlanOnlyActorPackageImports = false)
{
    MobySourcePatchResult result = await MobySourcePatchExporter.ExportAsync(new MobySourcePatchRequest(
        SourceImagePath: sourceImage,
        SourceCuePath: DiscImageLocator.FindCueForImage(sourceImage),
        OutputPrefix: outputPrefix,
        Level: level,
        NativeEditsPath: nativeEditsPath,
        WriteImage: true,
        AllowPlanOnlyActorPackageImports: allowPlanOnlyActorPackageImports));

    if (!result.WroteImage || !File.Exists(result.OutputImagePath))
        throw new InvalidOperationException($"{label} export did not write a disposable BIN image.");

    VerifyPatchBytes(result.OutputImagePath, result.Plan);
    string validationReport = await MobyCandidateValidationReportWriter.WriteAsync(result);
    string candidateResultPath = Path.ChangeExtension(result.OutputCuePath, ".candidate-result.json");
    if (!File.Exists(candidateResultPath))
        throw new InvalidOperationException($"{label} validation did not create a candidate result file.");
    int packagePatches = result.Plan.Patches.Count(patch => string.Equals(patch.Kind, "actor-package-copy", StringComparison.OrdinalIgnoreCase));
    string validationText = await File.ReadAllTextAsync(validationReport);
    if (packagePatches > 0)
    {
        if (!validationText.Contains("In-Game Evidence", StringComparison.Ordinal) ||
            !validationText.Contains("Actor Package Safety Detail", StringComparison.Ordinal) ||
            !validationText.Contains("Target safety", StringComparison.Ordinal) ||
            !validationText.Contains("Root safe", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{label} validation report did not include evidence and actor-package safety details.");
        }

        string syntheticRamPath = await WriteSyntheticCandidateBootRamAsync(result.Plan, label);
        CrossLevelCandidateRamVerification verification = CrossLevelCandidateRamVerifier.Verify(result.Plan, syntheticRamPath);
        if (!verification.BootedLevel || !verification.ObjectRecordsMatch || !verification.ActorRootsMatch)
            throw new InvalidOperationException($"{label} RAM verifier did not confirm the synthetic boot table: {verification.Summary}");

        string mismatchRamPath = await WriteSyntheticCandidateBootRamAsync(result.Plan, $"{label} mismatch");
        CorruptFirstCandidateMobyByte(mismatchRamPath, result.Plan);
        CrossLevelCandidateRamVerification mismatch = CrossLevelCandidateRamVerifier.Verify(result.Plan, mismatchRamPath);
        if (mismatch.ObjectRecordsMatch)
            throw new InvalidOperationException($"{label} RAM verifier did not reject a corrupted synthetic moby table.");
    }
    if (label.Contains("Artisans key chest", StringComparison.OrdinalIgnoreCase))
    {
        if (!validationText.Contains("Confirm the matching key is visible and collectible.", StringComparison.Ordinal) ||
            !validationText.Contains("Confirm nearby portals, dragons, and existing chests still behave normally.", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{label} validation report did not include the key/key-chest in-game checklist.");
        }
        if (result.Plan.Notes.Any(note => note.Contains("Stone Hill Spring Chest candidate exports", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"{label} plan should not include the Stone Hill Spring Chest helper note.");
    }
    if (label.Contains("Toasty wizard transform", StringComparison.OrdinalIgnoreCase))
    {
        if (!validationText.Contains("Confirm the defeated enemy's reward or collection behavior is sane.", StringComparison.Ordinal) ||
            !validationText.Contains("Confirm other enemies in the level still behave normally.", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{label} validation report did not include the enemy-transform in-game checklist.");
        }
        if (result.Plan.Notes.Any(note => note.Contains("Stone Hill Spring Chest candidate exports", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"{label} plan should not include the Stone Hill Spring Chest helper note.");
    }
    Console.WriteLine($"{label} disposable BIN export: {result.Plan.PatchCount} patch(es), package copies={packagePatches}, verified bytes in {Path.GetFileName(result.OutputImagePath)}, report={Path.GetFileName(validationReport)}");
}

async Task<string> WriteSyntheticCandidateBootRamAsync(MobySourcePatchPlan plan, string label)
{
    string outDir = Path.Combine(workspace.RootPath, "_local", "smoke");
    Directory.CreateDirectory(outDir);
    string path = Path.Combine(outDir, $"{SanitizeFileName(label)}-synthetic-candidate-boot-ram.bin");
    byte[] ram = new byte[2 * 1024 * 1024];
    int tableOffset = 0x100000;
    int expectedCount = Math.Max(plan.SourceRecordCount, plan.Patches.Where(patch => patch.TrueIndex >= 0).Select(patch => patch.TrueIndex + 1).DefaultIfEmpty(plan.SourceRecordCount).Max());
    BitConverter.GetBytes(0x80000000u + (uint)tableOffset).CopyTo(ram, 0x75828);
    BitConverter.GetBytes(0x80000000u + (uint)(tableOffset + expectedCount * plan.RecordStride)).CopyTo(ram, 0x7573C);

    foreach (MobySourcePatch patch in plan.Patches.Where(patch => patch.TrueIndex >= 0))
    {
        int recordOffset = tableOffset + patch.TrueIndex * plan.RecordStride;
        byte[] after = ParseHexPreview(patch.AfterHexPreview);
        if ((string.Equals(patch.Kind, "moby-record-append", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(patch.Kind, "spring-chest-reward-row-append", StringComparison.OrdinalIgnoreCase)) &&
            after.Length >= plan.RecordStride)
        {
            Array.Copy(after, 0, ram, recordOffset, plan.RecordStride);
        }
        else if (TryParseSmokeRecordOffset(patch.RecordOffset, out int byteOffset) && byteOffset >= 0 && byteOffset < plan.RecordStride)
        {
            for (int i = 0; i < after.Length && byteOffset + i < plan.RecordStride; i++)
                ram[recordOffset + byteOffset + i] = after[i];
        }
    }

    WriteSyntheticActorRootTable(ram, plan);

    await File.WriteAllBytesAsync(path, ram);
    return path;
}

void WriteSyntheticActorRootTable(byte[] ram, MobySourcePatchPlan plan)
{
    int rootBase = 0x90000;
    foreach (MobyActorPackageRootPreview root in plan.PackageImportPreviews.SelectMany(preview => preview.RootEntries))
    {
        int rootSlot = (int)ParseSmokeHexUInt32(root.TargetRootSlot);
        int rootIndex = root.RootIndex >= 0 ? root.RootIndex : (rootSlot - 0x50) / 4;
        uint targetRoot = ParseSmokeHexUInt32(root.TargetRoot);
        ushort actorId = (ushort)ParseSmokeHexUInt32(root.TargetActorId);
        int rootOffset = rootBase + rootIndex * 4;
        int actorOffset = rootBase + 0x100 + rootIndex * 2;
        BitConverter.GetBytes(targetRoot).CopyTo(ram, rootOffset);
        BitConverter.GetBytes(actorId).CopyTo(ram, actorOffset);
    }
}

void CorruptFirstCandidateMobyByte(string ramPath, MobySourcePatchPlan plan)
{
    byte[] ram = File.ReadAllBytes(ramPath);
    uint pointer = BitConverter.ToUInt32(ram, 0x75828);
    int tableOffset = (int)(pointer & 0x001FFFFF);
    MobySourcePatch? firstPatch = plan.Patches.FirstOrDefault(patch => patch.TrueIndex >= 0);
    if (firstPatch == null)
        return;
    ram[tableOffset + firstPatch.TrueIndex * plan.RecordStride + 0x36] ^= 0xFF;
    File.WriteAllBytes(ramPath, ram);
}

string SanitizeFileName(string value)
{
    StringBuilder builder = new();
    foreach (char c in value.ToLowerInvariant())
    {
        if (char.IsLetterOrDigit(c))
            builder.Append(c);
        else if (builder.Length == 0 || builder[^1] != '-')
            builder.Append('-');
    }
    return builder.ToString().Trim('-');
}

bool TryParseSmokeRecordOffset(string value, out int offset)
{
    offset = 0;
    if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        return false;
    return int.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out offset);
}

uint ParseSmokeHexUInt32(string value)
{
    string text = value.Trim();
    if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        text = text[2..];
    return uint.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}

async Task ReportCrossLevelCandidateValidationChecklist()
{
    string objectsDir = Path.Combine(workspace.RootPath, "_local", "objects");
    Directory.CreateDirectory(objectsDir);
    List<CandidateValidationSummary> summaries =
    [
        ReadCandidateValidationSummary(
            "CURRENT DIAGNOSTIC Stone Hill Spring Chest Peace Keepers T70 local-controller runtime-0x01A6 safe preinitialized visible-gem hang-return arc post-return full-quarantine scripted-motion rearm stack helper candidate",
            Path.Combine(objectsDir, $"{CurrentStoneHillSpringCandidateSlug}.moby-source-patch-plan.json"),
            [
                "Latest progress: v77 produced a first gem pop, but Sparx still flew underground toward the reward and the second hit animated without a visible gem.",
                "This diagnostic keeps the full post-return quarantine, blanks the reward value while airborne, and scripts reward motion without waiting for the active pickup list."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "FAILED Stone Hill Spring Chest Peace Keepers T70/T71 shared-cluster flag4A-0x10 rebased over-0x000E candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t70t71-sharedcluster-over000e-extended-cluster-flag10-rebased-candidate.moby-source-patch-plan.json"),
            [
                "Latest failed candidate: Stone Hill booted and the imported Peace Keepers T70/T71 pair appeared as two Spring Chests, but neither reacted to flame or charge and no gem popped.",
                "The current candidate keeps the shared special-data cluster but restores the clean Peace Keepers source byte +0x52 from 0x10 to 0xFF on both appended records."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "FAILED Stone Hill Spring Chest Peace Keepers T70/T71 pair extended-cluster flag4A-0x10 rebased over-0x000E candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t70t71-pair-over000e-extended-cluster-flag10-rebased-candidate.moby-source-patch-plan.json"),
            [
                "Latest failed candidate: Stone Hill booted and the imported Peace Keepers T70/T71 pair appeared as two Spring Chests, but neither reacted to flame or charge and no gem popped.",
                "The current candidate keeps the pair but preserves the native Peace Keepers shared-data relationship: T70 points at the copied spring cluster base and T71 points at base+0x14."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "FAILED Stone Hill Spring Chest Peace Keepers T70 native-00C2 extended-cluster flag4A-0x10 rebased over-0x000E candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t70-native00c2-over000e-extended-cluster-flag10-rebased-candidate.moby-source-patch-plan.json"),
            [
                "Latest failed candidate: Stone Hill loaded, the imported spring chest appeared, and the spring chest still did nothing when flamed or charged.",
                "Replacing Stone Hill's native 0x00C2 package changed a normal chest into a weird colored chest that still gave a blue gem, so the current candidate restores Stone Hill's native 0x00C2 package and tests the native T70/T71 spring pair instead."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "FAILED Stone Hill Spring Chest Peace Keepers T70 extended-cluster flag4A-0x10 rebased over-0x000E candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t70-over000e-extended-cluster-flag10-rebased-candidate.moby-source-patch-plan.json"),
            [
                "Latest failed candidate: Stone Hill loaded, the imported chest appeared, and approach/flame/charge did not crash.",
                "Flame and charge still did nothing, so the current candidate keeps the stable T70 0x0149 import but also imports the Peace Keepers native 0x00C2 chest/controller package."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "FAILED Stone Hill Spring Chest Peace Keepers T70 extended-cluster flag4A-0x10 over-0x000E candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t70-over000e-extended-cluster-flag10-candidate.moby-source-patch-plan.json"),
            [
                "Latest failed candidate: Stone Hill loaded, the imported chest appeared, and approach/flame/charge did not crash.",
                "Flame and charge still did nothing, so the larger shared spring-data cluster plus flag4A 0x10 is stable but not enough. The current candidate adds the one known 0x0149 internal package rebase."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "FAILED Stone Hill Spring Chest Peace Keepers T70 extended-special flag4A-0x10 over-0x000E candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t70-over000e-extended-special-flag10-candidate.moby-source-patch-plan.json"),
            [
                "Latest failed candidate: Stone Hill loaded, the imported chest appeared, and approach/flame/charge did not crash.",
                "Flame and charge still did nothing, so flag4A 0x10 plus the 0x100-byte extended special-data stream is stable but not enough. The current candidate widens that copy to the shared spring-data cluster."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "FAILED Stone Hill Spring Chest Peace Keepers T70 extended-special over-0x000E candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t70-over000e-extended-special-candidate.moby-source-patch-plan.json"),
            [
                "Latest failed candidate: Stone Hill loaded, the imported chest appeared, and approach/flame/charge did not crash.",
                "Flame and charge still did nothing and no gem popped, so the larger native special-data stream alone is stable but not enough to trigger Spring Chest behavior."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "FAILED Stone Hill Spring Chest Peace Keepers T70 runtime-0x01A6 arm-only over-0x000E candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t70-local-controller-over000e-runtime01a6-armonly-helper-candidate.moby-source-patch-plan.json"),
            [
                "Latest failed candidate: Stone Hill loaded, the imported chest appeared, and approach/flame/charge did not crash.",
                "Flame and charge still did nothing, so the local controller plus donor-specific runtime arm helper is stable but does not supply the missing spring trigger."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "FAILED Stone Hill Spring Chest Peace Keepers T70 native-only over-0x000E candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t70-over000e-nativeonly-candidate.moby-source-patch-plan.json"),
            [
                "Latest failed candidate: Stone Hill loaded, the imported chest appeared, and approach/flame/charge did not crash.",
                "Flame and charge still did nothing, so the latest candidate keeps this stable actor/root route but copies more of the donor's native special data."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "FAILED Stone Hill Spring Chest Peace Keepers T71 local-controller over-0x000E arm-only helper candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t71-local-controller-over000e-armonly-helper-candidate.moby-source-patch-plan.json"),
            [
                "Latest failed candidate: Stone Hill loaded, the imported chest appeared, and approach/flame/charge did not crash.",
                "Flame and charge still did nothing, so the local controller plus arm-only helper is stable but does not supply the missing spring trigger."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "PARTIAL Stone Hill Spring Chest Peace Keepers T71 local-controller over-0x000E stack hook-only candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t71-local-controller-over000e-stack-hookonly-candidate.moby-source-patch-plan.json"),
            [
                "Latest partial candidate: Stone Hill loaded, the object appeared, and approach/flame/charge did not crash.",
                "It did not change Spring Chest behavior, proving the pass-through hook itself is stable but field writes are still needed."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "FAILED Stone Hill Spring Chest Peace Keepers T71 local-controller over-0x000E fixed-helper candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t71-local-controller-over000e-fixed-helper-candidate.moby-source-patch-plan.json"),
            [
                "Latest failed candidate: Stone Hill loaded and the object appeared, but getting near the object crashed the game.",
                "This keeps helper/reward writes guarded while the next candidate isolates the EXE hook itself."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "PARTIAL Stone Hill Spring Chest Peace Keepers T71 local-controller over-0x000E no-helper candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t71-local-controller-over000e-norebase-sourceff-candidate.moby-source-patch-plan.json"),
            [
                "Latest partial candidate: Stone Hill loaded, the imported object appeared, and there was no crash.",
                "Flaming or charging did nothing, proving the package/root location is stable but behavior still needs the fixed helper/reward route."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "FAILED Stone Hill Spring Chest Peace Keepers T71 local-controller no-rebase first-empty-root source-FF candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t71-local-controller-first-empty-root-norebase-sourceff-candidate.moby-source-patch-plan.json"),
            [
                "Historical failed candidate: user reported a black-screen crash with a screeching audio bug.",
                "This proved adding a local Stone Hill 0x00C2 controller did not make the low-offset first-empty-root 0x0149 import safe."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "FAILED Stone Hill Spring Chest Peace Keepers T70 all-zero-special first-empty-root source-FF candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t70-first-empty-root-sourceff-candidate.moby-source-patch-plan.json"),
            [
                "Historical failed candidate: user reported a full in-game crash with severe audio blare rather than a simple freeze.",
                "This used the same first-empty root but copied the T70 all-zero-special donor with the old internal rebase, so the current candidate retests a byte-for-byte package copy instead."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "FAILED Stone Hill Spring Chest Peace Keepers T71 shell-only no-rebase first-empty-root source-FF candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t71-first-empty-root-norebase-sourceff-candidate.moby-source-patch-plan.json"),
            [
                "Historical failed candidate: user reported Spyro crashed to a black screen.",
                "This copied the native 0x0149 package byte-for-byte and registered the first empty root, but appended only the visible shell; the current candidate keeps the shell-only idea but moves it to the safer over-0x000E actor slot."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "FAILED Stone Hill Spring Chest Peace Keepers T71 first-empty-root source-FF candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t71-first-empty-root-sourceff-candidate.moby-source-patch-plan.json"),
            [
                "Historical failed candidate: Stone Hill loaded, the object appeared, and approaching it did not crash.",
                "Flaming the object/gem crashed the game with the old internal rebase, so the current candidate retests the same donor/root without rebasing package bytes."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "FAILED Stone Hill Spring Chest Peace Keepers T71 entry-hook alt-cave helper candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t71-entry-hookonly-altcave-candidate.moby-source-patch-plan.json"),
            [
                "Historical failed candidate: DuckStation stayed black at 0 FPS before the Universal/Insomniac logos.",
                "The entry-trampoline hook is not safe enough to keep testing; the current candidate returns to data-only actor-root registration."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "FAILED Stone Hill Spring Chest Peace Keepers T71 stack hook-only helper candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t71-stack-hookonly-helper-candidate.moby-source-patch-plan.json"),
            [
                "Boot Stone Hill without hanging on the flying/loading screen.",
                "Confirm whether the placed object appears at all.",
                "Approach and flame the chest to confirm the stack-save pass-through helper hook stays stable.",
                "This candidate writes no chest fields; expect normal chest behavior if the stack-save hook itself is safe."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "FAILED Stone Hill Spring Chest Peace Keepers T71 hook-only helper candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t71-hookonly-helper-candidate.moby-source-patch-plan.json"),
            [
                "Historical failed candidate: Stone Hill loaded, the object appeared, and approach did not crash.",
                "Flaming the object crashed the game even though the hook wrote no chest fields, so the next candidate saves RA on the stack instead of the payload scratch area."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "FAILED Stone Hill Spring Chest Peace Keepers T71 arm-only helper candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t71-armonly-helper-candidate.moby-source-patch-plan.json"),
            [
                "Historical failed candidate: Stone Hill loaded, the object appeared, and approaching it did not crash.",
                "Flaming the object crashed the game, narrowing the unsafe behavior to the helper's controller/shell field writes rather than base loading."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "PARTIAL Stone Hill Spring Chest Peace Keepers T71 reward-row no-helper candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t71-rewardrow-no-helper-candidate.moby-source-patch-plan.json"),
            [
                "Historical partial candidate: Stone Hill loaded, the object appeared, and flaming/approaching it did not crash with the hidden reward row present.",
                "It still behaved as a normal Flame/Charge chest and awarded a blue gem, proving the reward row is stable but does not solve Spring Chest behavior by itself."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "PARTIAL Stone Hill Spring Chest Peace Keepers T71 local-controller no-helper candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t71-local-controller-no-helper-candidate.moby-source-patch-plan.json"),
            [
                "Historical partial candidate: Stone Hill loaded, the object appeared, and flaming it did not crash.",
                "It still behaved as a normal chest and awarded a blue gem, proving the actor package plus local controller is stable while the Spring Chest helper/reward behavior remains unsolved."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "FAILED Stone Hill Spring Chest Peace Keepers T71 local-controller helper candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t71-local-controller-candidate.moby-source-patch-plan.json"),
            [
                "Historical failed candidate: Stone Hill booted and the object appeared, but it still looked like a normal Flame/Charge chest.",
                "The game crashed or hung in-game when Spyro approached to flame it, so the custom helper/reward path is now isolated behind the no-helper candidate."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "Stone Hill Spring Chest failed Peace Keepers T71 source-FF invisible candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t71-sourceff-candidate.moby-source-patch-plan.json"),
            [
                "Historical partial candidate: Stone Hill loaded, but the Spring Chest became completely invisible.",
                "This older build registered the actor at root slot 0xE8 while 0xE4 stayed empty, so the current source-FF retest uses the first empty contiguous root slot before judging the source flag."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "Stone Hill Spring Chest failed Peace Keepers T71 special-data source-10 candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-t71-special-candidate.moby-source-patch-plan.json"),
            [
                "Historical partial candidate: Stone Hill loaded, but the object behaved as a normal Flame/Charge chest and awarded one red gem.",
                "This proved the nonzero-special Peace Keepers T71 donor is still not enough when exported with source flag4A 0x10; the next candidate preserves the native source 0xFF byte."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "Stone Hill Spring Chest failed Peace Keepers T70 native-0149 candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-peacekeepers-native0149-candidate.moby-source-patch-plan.json"),
            [
                "Historical partial candidate: Stone Hill loaded, but the object behaved as a normal Flame/Charge chest and awarded one red gem.",
                "This proved the all-zero Peace Keepers T70 donor row is not enough to produce Spring Chest behavior in Stone Hill."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "Stone Hill Spring Chest failed controller-alias candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-controller-alias-candidate.moby-source-patch-plan.json"),
            [
                "Historical partial candidate: Stone Hill loaded, but the object behaved as a normal Flame/Charge chest and awarded one red gem.",
                "This proved the imported private 0x01FE controller route still did not produce Spring Chest behavior."
            ],
            requireNoActor01Fe: false),
        ReadCandidateValidationSummary(
            "Stone Hill Spring Chest failed full dependency-rebase candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-full-rebase-candidate.moby-source-patch-plan.json"),
            [
                "Historical partial candidate: Stone Hill loaded, but the object behaved as a normal flame/charge chest and awarded one red gem.",
                "This proved importing only helper/shell roots still leaves Stone Hill's native 0x00C2 chest actor in control."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "Stone Hill Spring Chest failed 0x0700 prefix candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-prefix0700-candidate.moby-source-patch-plan.json"),
            [
                "Historical partial candidate: Stone Hill loaded, but the object behaved as a normal flame/charge chest and awarded one red gem.",
                "This proved the first 0x0700 copied package bytes are safe to boot, but still not enough for Spring Chest behavior."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "Stone Hill Spring Chest failed 0x0600 prefix candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-prefix0600-candidate.moby-source-patch-plan.json"),
            [
                "Historical partial candidate: Stone Hill loaded, but the object behaved as a normal flame/charge chest and awarded one red gem.",
                "This proved the first 0x0600 copied package bytes are safe, but not enough for Spring Chest behavior."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "Stone Hill Spring Chest failed 0x0400 prefix candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-prefix0400-candidate.moby-source-patch-plan.json"),
            [
                "Historical partial candidate: Stone Hill loaded, but the object behaved as a normal flame/charge chest and awarded one red gem.",
                "This proved the first 0x0400 copied package bytes are safe, but not enough for Spring Chest behavior."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "Stone Hill Spring Chest failed 0x0100 prefix candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-prefix0100-candidate.moby-source-patch-plan.json"),
            [
                "Historical partial candidate: Stone Hill loaded, but the object behaved as a normal flame/charge chest and awarded one red gem.",
                "This proved the first 0x0100 copied package bytes are safe, but not enough for Spring Chest behavior."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "Stone Hill Spring Chest failed four-byte range probe",
            Path.Combine(objectsDir, "stonehill-spring-chest-range-probe-candidate.moby-source-patch-plan.json"),
            [
                "Historical partial candidate: Stone Hill loaded, but the object behaved as a normal flame/charge chest and awarded one red gem.",
                "This proved the first four copied package bytes are safe, but not enough for Spring Chest behavior."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "Stone Hill Spring Chest failed helper-copy-only candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-helper-copy-only-candidate.moby-source-patch-plan.json"),
            [
                "Historical failed candidate: Stone Hill hung on the flying/loading screen after copying only the first helper package segment.",
                "This narrows the next test to a four-byte range probe."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "Stone Hill Spring Chest failed copy-only candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-copy-only-candidate.moby-source-patch-plan.json"),
            [
                "Historical failed candidate: Stone Hill hung on the flying/loading screen after copying package bytes without roots.",
                "This points at the copied package bytes/ranges, not actor-root registration."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "Stone Hill Spring Chest failed records-only candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-records-only-candidate.moby-source-patch-plan.json"),
            [
                "Historical partial candidate: Stone Hill loaded, but the object behaved like a normal Charge/Flame chest with a red gem.",
                "This proved the imported records/special data are safe enough to load, but actor-package/root work is still needed for Spring Chest behavior."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "Stone Hill Spring Chest failed no-helper candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-no-helper-candidate.moby-source-patch-plan.json"),
            [
                "Historical failed candidate: Stone Hill hung on the flying/loading screen even after the custom helper/reward row was removed.",
                "Keep this exact recipe/fingerprint guarded while testing the range-probe isolation candidate."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "Stone Hill Spring Chest failed zero-gap helper candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-zero-gap-candidate.moby-source-patch-plan.json"),
            [
                "Historical failed candidate: Stone Hill hung on the flying/loading screen after the zero-gap package plus EXE helper was added.",
                "Keep this exact recipe/fingerprint guarded while testing the no-helper isolation candidate."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "Stone Hill Spring Chest failed native-root shell candidate",
            Path.Combine(objectsDir, "stonehill-spring-chest-preserve-00c2-candidate.moby-source-patch-plan.json"),
            [
                "Historical failed candidate: Stone Hill hung on the flying/loading screen.",
                "Keep this recipe guarded while testing the no-helper candidate."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "Stone Hill Spring Chest failed 00C2 replacement",
            Path.Combine(objectsDir, "stonehill-spring-chest-native-root-candidate.moby-source-patch-plan.json"),
            [
                "Historical failed candidate: Stone Hill hung on the flying/loading screen.",
                "Keep this recipe guarded unless a future dependency rebase explains and fixes the 0x00C2 replacement risk."
            ],
            requireNoActor01Fe: true),
        ReadCandidateValidationSummary(
            "Artisans Key + Key Chest",
            Path.Combine(objectsDir, "artisans-key-chest-write-smoke.moby-source-patch-plan.json"),
            [
                "Boot Artisans and confirm the added key is visible and collectible.",
                "Confirm the added Key Chest remains locked before collecting the key.",
                "Collect the key, open the Key Chest, and confirm it pays out normally.",
                "Confirm nearby portals/dragons/chests still behave normally after the new actor package is registered."
            ],
            requireNoActor01Fe: false),
        ReadCandidateValidationSummary(
            "Toasty Dog to Green Wizard",
            Path.Combine(objectsDir, "toasty-wizard-transform-write-smoke.moby-source-patch-plan.json"),
            [
                "Boot Toasty and go to the transformed enemy.",
                "Confirm the transformed dog now appears/behaves as a Green Wizard.",
                "Defeat it and confirm the reward/collection behavior is sane.",
                "Confirm other Toasty dogs and shepherds still behave normally."
            ],
            requireNoActor01Fe: false)
    ];

    string markdownPath = Path.Combine(objectsDir, "cross-level-candidate-validation.md");
    StringBuilder builder = new();
    builder.AppendLine("# Cross-level candidate validation");
    builder.AppendLine();
    builder.AppendLine("These are disposable test builds for cross-level moby work. The normal editor remains guarded for plan-only recipes until a test build is confirmed in-game.");
    builder.AppendLine();
    builder.AppendLine("| Candidate | CUE | Patches | Package copies | Appends | Special imports | Source count | Byte verified | In-game evidence | Status |");
    builder.AppendLine("|---|---|---:|---:|---:|---:|---|---|---|---|");
    foreach (CandidateValidationSummary summary in summaries)
    {
        builder.AppendLine($"| {summary.Name} | `{summary.CuePath}` | {summary.PatchCount} | {summary.PackageCopies} | {summary.RecordAppends} | {summary.SpecialImports} | {summary.SourceCountChange} | {summary.ByteVerified} | {summary.EvidenceStatus} | {summary.Status} |");
    }

    foreach (CandidateValidationSummary summary in summaries)
    {
        builder.AppendLine();
        builder.AppendLine($"## {summary.Name}");
        builder.AppendLine();
        builder.AppendLine($"- Plan: `{summary.PlanPath}`");
        builder.AppendLine($"- CUE: `{summary.CuePath}`");
        builder.AppendLine($"- BIN: `{summary.BinPath}`");
        builder.AppendLine($"- Result: `{summary.ResultPath}`");
        builder.AppendLine($"- Recipe: `{summary.RecipeId}` ({summary.RecipeStatus})");
        builder.AppendLine($"- Guard: {summary.GuardReason}");
        builder.AppendLine($"- Patch summary: {summary.PatchCount} total, {summary.TotalPatchedBytes} bytes, {summary.PackageCopies} package copy patch(es), {summary.RecordAppends} record append(s), {summary.SpecialImports} special-data import(s), source count {summary.SourceCountChange}.");
        builder.AppendLine($"- In-game evidence: {summary.EvidenceStatus}.");
        if (!string.IsNullOrWhiteSpace(summary.EvidenceNotes))
            builder.AppendLine($"- Evidence notes: {summary.EvidenceNotes}");
        if (!summary.NoActor01Fe)
            builder.AppendLine("- Warning: this candidate references actor `0x01FE`; this is allowed only for the current isolated controller-alias test and must stay guarded until in-game behavior passes.");
        builder.AppendLine("- In-game checks:");
        foreach (string check in summary.InGameChecks)
            builder.AppendLine($"  - [ ] {check}");
    }

    await File.WriteAllTextAsync(markdownPath, builder.ToString());
    Console.WriteLine($"Cross-level candidate validation checklist: {summaries.Count} candidate(s), report={markdownPath}");
}

void ReportCrossLevelCandidateLaunchers()
{
    string macPath = Path.Combine(workspace.RootPath, "Launch Cross-Level Candidate Tests.command");
    string windowsPath = Path.Combine(workspace.RootPath, "Launch Cross-Level Candidate Tests.bat");
    string expected = $"{CurrentStoneHillSpringCandidateSlug}.cue";
    string failedT70T71SharedFlag10 = "stonehill-spring-chest-peacekeepers-t70t71-sharedcluster-over000e-extended-cluster-flag10-rebased-candidate.cue";
    string failedT70T71Pair = "stonehill-spring-chest-peacekeepers-t70t71-pair-over000e-extended-cluster-flag10-rebased-candidate.cue";
    string failedT70Native00C2 = "stonehill-spring-chest-peacekeepers-t70-native00c2-over000e-extended-cluster-flag10-rebased-candidate.cue";
    string failedT70ExtendedClusterFlag10Rebased = "stonehill-spring-chest-peacekeepers-t70-over000e-extended-cluster-flag10-rebased-candidate.cue";
    string failedT70ExtendedClusterFlag10 = "stonehill-spring-chest-peacekeepers-t70-over000e-extended-cluster-flag10-candidate.cue";
    string failedT70ExtendedSpecialFlag10 = "stonehill-spring-chest-peacekeepers-t70-over000e-extended-special-flag10-candidate.cue";
    string failedT70ExtendedSpecial = "stonehill-spring-chest-peacekeepers-t70-over000e-extended-special-candidate.cue";
    string failedT70Runtime01A6ArmOnly = "stonehill-spring-chest-peacekeepers-t70-local-controller-over000e-runtime01a6-armonly-helper-candidate.cue";
    string failedT70NativeOnly = "stonehill-spring-chest-peacekeepers-t70-over000e-nativeonly-candidate.cue";
    string failedOver000EArmOnly = "stonehill-spring-chest-peacekeepers-t71-local-controller-over000e-armonly-helper-candidate.cue";
    string partialOver000EStackHook = "stonehill-spring-chest-peacekeepers-t71-local-controller-over000e-stack-hookonly-candidate.cue";
    string failedFixedHelper = "stonehill-spring-chest-peacekeepers-t71-local-controller-over000e-fixed-helper-candidate.cue";
    string partialOver000ENoHelper = "stonehill-spring-chest-peacekeepers-t71-local-controller-over000e-norebase-sourceff-candidate.cue";
    string failedLocalControllerFirstEmpty = "stonehill-spring-chest-peacekeepers-t71-local-controller-first-empty-root-norebase-sourceff-candidate.cue";
    string failedFirstEmptyT70 = "stonehill-spring-chest-peacekeepers-t70-first-empty-root-sourceff-candidate.cue";
    string failedFirstEmptyT71NoRebase = "stonehill-spring-chest-peacekeepers-t71-first-empty-root-norebase-sourceff-candidate.cue";
    string failedFirstEmptyT71 = "stonehill-spring-chest-peacekeepers-t71-first-empty-root-sourceff-candidate.cue";
    string failedEntryHook = "stonehill-spring-chest-peacekeepers-t71-entry-hookonly-altcave-candidate.cue";
    string failedStackHookOnly = "stonehill-spring-chest-peacekeepers-t71-stack-hookonly-helper-candidate.cue";
    string failedHookOnly = "stonehill-spring-chest-peacekeepers-t71-hookonly-helper-candidate.cue";
    string failedArmOnly = "stonehill-spring-chest-peacekeepers-t71-armonly-helper-candidate.cue";
    string partialRewardRow = "stonehill-spring-chest-peacekeepers-t71-rewardrow-no-helper-candidate.cue";
    string partialNoHelper = "stonehill-spring-chest-peacekeepers-t71-local-controller-no-helper-candidate.cue";
    string failedLocalController = "stonehill-spring-chest-peacekeepers-t71-local-controller-candidate.cue";
    string failedT71SourceFF = "stonehill-spring-chest-peacekeepers-t71-sourceff-candidate.cue";
    string failedT71Source10 = "stonehill-spring-chest-peacekeepers-t71-special-candidate.cue";
    string failedNative0149 = "stonehill-spring-chest-peacekeepers-native0149-candidate.cue";
    string failedAlias = "stonehill-spring-chest-controller-alias-candidate.cue";
    string failedPrefix = "stonehill-spring-chest-prefix0700-candidate.cue";
    string failedRoot = "stonehill-spring-chest-native-root-candidate.cue";

    if (!LauncherContains(macPath, expected) || !LauncherContains(windowsPath, expected.Replace('/', '\\')))
    {
        Console.WriteLine("Cross-level candidate launchers: stale Spring Chest diagnostic launcher detected; normal editor Add Object keeps cross-level Spring Chests hidden.");
        return;
    }

    ValidateLauncher(macPath, expected, failedT70T71SharedFlag10);
    ValidateLauncher(windowsPath, expected.Replace('/', '\\'), failedT70T71SharedFlag10.Replace('/', '\\'));
    ValidateLauncher(macPath, failedT70T71SharedFlag10, failedT70T71Pair);
    ValidateLauncher(windowsPath, failedT70T71SharedFlag10.Replace('/', '\\'), failedT70T71Pair.Replace('/', '\\'));
    ValidateLauncher(macPath, failedT70T71Pair, failedT70Native00C2);
    ValidateLauncher(windowsPath, failedT70T71Pair.Replace('/', '\\'), failedT70Native00C2.Replace('/', '\\'));
    ValidateLauncher(macPath, failedT70Native00C2, failedT70ExtendedClusterFlag10Rebased);
    ValidateLauncher(windowsPath, failedT70Native00C2.Replace('/', '\\'), failedT70ExtendedClusterFlag10Rebased.Replace('/', '\\'));
    ValidateLauncher(macPath, failedT70ExtendedClusterFlag10Rebased, failedT70ExtendedClusterFlag10);
    ValidateLauncher(windowsPath, failedT70ExtendedClusterFlag10Rebased.Replace('/', '\\'), failedT70ExtendedClusterFlag10.Replace('/', '\\'));
    ValidateLauncher(macPath, failedT70ExtendedClusterFlag10, failedT70ExtendedSpecialFlag10);
    ValidateLauncher(windowsPath, failedT70ExtendedClusterFlag10.Replace('/', '\\'), failedT70ExtendedSpecialFlag10.Replace('/', '\\'));
    ValidateLauncher(macPath, failedT70ExtendedSpecialFlag10, failedT70ExtendedSpecial);
    ValidateLauncher(windowsPath, failedT70ExtendedSpecialFlag10.Replace('/', '\\'), failedT70ExtendedSpecial.Replace('/', '\\'));
    ValidateLauncher(macPath, failedT70ExtendedSpecial, failedT70Runtime01A6ArmOnly);
    ValidateLauncher(windowsPath, failedT70ExtendedSpecial.Replace('/', '\\'), failedT70Runtime01A6ArmOnly.Replace('/', '\\'));
    ValidateLauncher(macPath, failedT70Runtime01A6ArmOnly, failedT70NativeOnly);
    ValidateLauncher(windowsPath, failedT70Runtime01A6ArmOnly.Replace('/', '\\'), failedT70NativeOnly.Replace('/', '\\'));
    ValidateLauncher(macPath, failedT70NativeOnly, failedOver000EArmOnly);
    ValidateLauncher(windowsPath, failedT70NativeOnly.Replace('/', '\\'), failedOver000EArmOnly.Replace('/', '\\'));
    ValidateLauncher(macPath, failedOver000EArmOnly, partialOver000EStackHook);
    ValidateLauncher(windowsPath, failedOver000EArmOnly.Replace('/', '\\'), partialOver000EStackHook.Replace('/', '\\'));
    ValidateLauncher(macPath, partialOver000EStackHook, failedFixedHelper);
    ValidateLauncher(windowsPath, partialOver000EStackHook.Replace('/', '\\'), failedFixedHelper.Replace('/', '\\'));
    ValidateLauncher(macPath, failedFixedHelper, partialOver000ENoHelper);
    ValidateLauncher(windowsPath, failedFixedHelper.Replace('/', '\\'), partialOver000ENoHelper.Replace('/', '\\'));
    ValidateLauncher(macPath, partialOver000ENoHelper, failedLocalControllerFirstEmpty);
    ValidateLauncher(windowsPath, partialOver000ENoHelper.Replace('/', '\\'), failedLocalControllerFirstEmpty.Replace('/', '\\'));
    ValidateLauncher(macPath, failedLocalControllerFirstEmpty, failedFirstEmptyT70);
    ValidateLauncher(windowsPath, failedLocalControllerFirstEmpty.Replace('/', '\\'), failedFirstEmptyT70.Replace('/', '\\'));
    ValidateLauncher(macPath, failedFirstEmptyT70, failedFirstEmptyT71NoRebase);
    ValidateLauncher(windowsPath, failedFirstEmptyT70.Replace('/', '\\'), failedFirstEmptyT71NoRebase.Replace('/', '\\'));
    ValidateLauncher(macPath, failedFirstEmptyT71NoRebase, failedFirstEmptyT71);
    ValidateLauncher(windowsPath, failedFirstEmptyT71NoRebase.Replace('/', '\\'), failedFirstEmptyT71.Replace('/', '\\'));
    ValidateLauncher(macPath, failedFirstEmptyT71, failedEntryHook);
    ValidateLauncher(windowsPath, failedFirstEmptyT71.Replace('/', '\\'), failedEntryHook.Replace('/', '\\'));
    ValidateLauncher(macPath, failedEntryHook, failedStackHookOnly);
    ValidateLauncher(windowsPath, failedEntryHook.Replace('/', '\\'), failedStackHookOnly.Replace('/', '\\'));
    ValidateLauncher(macPath, failedStackHookOnly, failedHookOnly);
    ValidateLauncher(windowsPath, failedStackHookOnly.Replace('/', '\\'), failedHookOnly.Replace('/', '\\'));
    ValidateLauncher(macPath, failedHookOnly, failedArmOnly);
    ValidateLauncher(windowsPath, failedHookOnly.Replace('/', '\\'), failedArmOnly.Replace('/', '\\'));
    ValidateLauncher(macPath, failedArmOnly, partialRewardRow);
    ValidateLauncher(windowsPath, failedArmOnly.Replace('/', '\\'), partialRewardRow.Replace('/', '\\'));
    ValidateLauncher(macPath, partialRewardRow, partialNoHelper);
    ValidateLauncher(windowsPath, partialRewardRow.Replace('/', '\\'), partialNoHelper.Replace('/', '\\'));
    ValidateLauncher(macPath, partialNoHelper, failedLocalController);
    ValidateLauncher(windowsPath, partialNoHelper.Replace('/', '\\'), failedLocalController.Replace('/', '\\'));
    ValidateLauncher(macPath, failedLocalController, failedT71SourceFF);
    ValidateLauncher(windowsPath, failedLocalController.Replace('/', '\\'), failedT71SourceFF.Replace('/', '\\'));
    ValidateLauncher(macPath, failedT71SourceFF, failedT71Source10);
    ValidateLauncher(windowsPath, failedT71SourceFF.Replace('/', '\\'), failedT71Source10.Replace('/', '\\'));
    ValidateLauncher(macPath, failedT71Source10, failedNative0149);
    ValidateLauncher(windowsPath, failedT71Source10.Replace('/', '\\'), failedNative0149.Replace('/', '\\'));
    ValidateLauncher(macPath, failedNative0149, failedAlias);
    ValidateLauncher(windowsPath, failedNative0149.Replace('/', '\\'), failedAlias.Replace('/', '\\'));
    ValidateLauncher(macPath, failedAlias, failedPrefix);
    ValidateLauncher(windowsPath, failedAlias.Replace('/', '\\'), failedPrefix.Replace('/', '\\'));
    ValidateLauncher(macPath, failedPrefix, failedRoot);
    ValidateLauncher(windowsPath, failedPrefix.Replace('/', '\\'), failedRoot.Replace('/', '\\'));
    Console.WriteLine("Cross-level candidate launchers: current Stone Hill Spring Chest candidate is first; failed and partial builds are retained for reference.");
}

bool LauncherContains(string path, string expected)
{
    return File.Exists(path) &&
        File.ReadAllText(path).IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
}

void ValidateLauncher(string path, string expected, string failed)
{
    if (!File.Exists(path))
        throw new InvalidOperationException($"Missing candidate launcher: {path}");

    string text = File.ReadAllText(path);
    int expectedIndex = text.IndexOf(expected, StringComparison.OrdinalIgnoreCase);
    int failedIndex = text.IndexOf(failed, StringComparison.OrdinalIgnoreCase);
    if (expectedIndex < 0)
        throw new InvalidOperationException($"{Path.GetFileName(path)} does not reference the expected Stone Hill Spring Chest candidate.");
    if (failedIndex >= 0 && failedIndex < expectedIndex)
        throw new InvalidOperationException($"{Path.GetFileName(path)} lists the failed Stone Hill Spring Chest candidate before the current candidate.");
}

CandidateValidationSummary ReadCandidateValidationSummary(string name, string planPath, IReadOnlyList<string> inGameChecks, bool requireNoActor01Fe)
{
    if (!File.Exists(planPath))
        return new CandidateValidationSummary(name, planPath, "", "", "", "", "", "", 0, 0, 0, 0, 0, "-", false, !requireNoActor01Fe, "missing", "", "missing plan", inGameChecks);

    using FileStream stream = File.OpenRead(planPath);
    using JsonDocument document = JsonDocument.Parse(stream);
    JsonElement root = document.RootElement;
    string binPath = ReadJsonString(root, "OutputImagePath");
    string cuePath = ReadJsonString(root, "OutputCuePath");
    string resultPath = Path.ChangeExtension(cuePath, ".candidate-result.json");
    int patchCount = ReadJsonInt32(root, "PatchCount");
    int totalPatchedBytes = ReadJsonInt32(root, "TotalPatchedBytes");
    int packageCopies = 0;
    int recordAppends = 0;
    int specialImports = 0;
    string sourceCountChange = "-";
    bool noActor01Fe = true;
    bool byteVerified = File.Exists(binPath) && File.Exists(cuePath);

    if (root.TryGetProperty("Patches", out JsonElement patches) && patches.ValueKind == JsonValueKind.Array)
    {
        foreach (JsonElement patch in patches.EnumerateArray())
        {
            string kind = ReadJsonString(patch, "Kind");
            if (string.Equals(kind, "actor-package-copy", StringComparison.OrdinalIgnoreCase))
                packageCopies++;
            if (string.Equals(kind, "moby-record-append", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, "spring-chest-reward-row-append", StringComparison.OrdinalIgnoreCase))
                recordAppends++;
            if (kind.Contains("special-data-import", StringComparison.OrdinalIgnoreCase))
                specialImports++;
            if (string.Equals(kind, "moby-source-count", StringComparison.OrdinalIgnoreCase))
            {
                int before = ParseHexInt32(ReadJsonString(patch, "BeforeHexPreview"));
                int after = ParseHexInt32(ReadJsonString(patch, "AfterHexPreview"));
                sourceCountChange = before >= 0 && after >= 0 ? $"{before}->{after}" : "patched";
            }
        }
    }

    string recipeId = "";
    string recipeStatus = "";
    string guardReason = "";
    if (root.TryGetProperty("PackageImportPreviews", out JsonElement previews) && previews.ValueKind == JsonValueKind.Array)
    {
        foreach (JsonElement preview in previews.EnumerateArray())
        {
            if (string.IsNullOrWhiteSpace(recipeId))
            {
                recipeId = ReadJsonString(preview, "RecipeId");
                recipeStatus = ReadJsonString(preview, "RecipeStatus");
                guardReason = ReadJsonString(preview, "GuardReason");
            }

            if (preview.TryGetProperty("RootEntries", out JsonElement roots) && roots.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement actorRoot in roots.EnumerateArray())
                {
                    if (string.Equals(ReadJsonString(actorRoot, "TargetActorId"), "0x01FE", StringComparison.OrdinalIgnoreCase))
                        noActor01Fe = false;
                }
            }
        }
    }

    if (requireNoActor01Fe && !noActor01Fe)
        byteVerified = false;

    CandidateInGameEvidence evidence = ReadCandidateInGameEvidence(resultPath);
    string status = byteVerified
        ? requireNoActor01Fe && !noActor01Fe
            ? "reject: 0x01FE alias present"
            : evidence.StatusClass switch
            {
                "passed" => "passed in-game evidence; candidate can be considered for promotion",
                "failed" => "failed in-game evidence; keep guarded",
                _ => "ready for emulator test"
            }
        : "missing BIN/CUE or failed static guard";

    return new CandidateValidationSummary(name, planPath, binPath, cuePath, resultPath, recipeId, recipeStatus, guardReason, patchCount, totalPatchedBytes, packageCopies, recordAppends, specialImports, sourceCountChange, byteVerified, noActor01Fe, evidence.DisplayStatus, evidence.Notes, status, inGameChecks);
}

CandidateInGameEvidence ReadCandidateInGameEvidence(string resultPath)
{
    if (string.IsNullOrWhiteSpace(resultPath) || !File.Exists(resultPath))
        return new CandidateInGameEvidence("missing", "missing result file", "");

    try
    {
        using FileStream stream = File.OpenRead(resultPath);
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement result = document.RootElement.TryGetProperty("result", out JsonElement resultElement) && resultElement.ValueKind == JsonValueKind.Object
            ? resultElement
            : document.RootElement;

        string status = ReadJsonString(result, "status", "untested");
        bool? bootedLevel = ReadJsonNullableBool(result, "bootedLevel");
        bool? objectAppeared = ReadJsonNullableBool(result, "objectAppeared");
        bool? mobyRecordsPresent = ReadJsonNullableBool(result, "mobyRecordsPresent");
        bool? actorRootsPresent = ReadJsonNullableBool(result, "actorRootsPresent");
        bool? behaviorCorrect = ReadJsonNullableBool(result, "behaviorCorrect");
        bool? noNearbyRegression = ReadJsonNullableBool(result, "noNearbyRegression");
        string notes = ReadJsonString(result, "notes");
        bool mobyRecordsPass = mobyRecordsPresent ?? objectAppeared == true;
        bool actorRootsPass = actorRootsPresent ?? objectAppeared == true;
        bool? behaviorChecksPassed = ReadBehaviorChecksPassed(document.RootElement);

        if (bootedLevel == false || objectAppeared == false || mobyRecordsPresent == false || actorRootsPresent == false || behaviorCorrect == false || behaviorChecksPassed == false || noNearbyRegression == false ||
            status.Contains("fail", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("reject", StringComparison.OrdinalIgnoreCase))
        {
            return new CandidateInGameEvidence("failed", "failed in-game evidence", notes);
        }

        if (bootedLevel == true && objectAppeared == true && mobyRecordsPass && actorRootsPass && behaviorCorrect == true && behaviorChecksPassed != false && noNearbyRegression == true &&
            (status.Contains("pass", StringComparison.OrdinalIgnoreCase) ||
             status.Contains("verified", StringComparison.OrdinalIgnoreCase) ||
             status.Contains("tested", StringComparison.OrdinalIgnoreCase)))
        {
            return new CandidateInGameEvidence("passed", "passed in-game evidence", notes);
        }

        return new CandidateInGameEvidence("untested", string.IsNullOrWhiteSpace(status) ? "untested" : status, notes);
    }
    catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
    {
        return new CandidateInGameEvidence("failed", "unreadable result file", ex.Message);
    }
}

async Task WriteSyntheticCandidateEvidenceAsync(
    string resultPath,
    LevelDefinition level,
    string recipeId,
    string notes,
    string templateId,
    string family,
    string recipeStatus,
    string recipeFingerprint,
    IReadOnlyList<CandidateBehaviorSmokeCheck> behaviorChecks,
    bool passed = true)
{
    Directory.CreateDirectory(Path.GetDirectoryName(resultPath) ?? ".");
    string status = passed ? "tested-pass" : "tested-fail";
    var result = new
    {
        generatedBy = "Spyro.Editor.Smoke",
        purpose = "Synthetic smoke evidence for exporter gating only.",
        candidate = new
        {
            levelKey = level.Key,
            levelName = level.DisplayName,
            recipes = new[]
            {
                new
                {
                    label = family,
                    templateId,
                    family,
                    recipeId,
                    recipeStatus,
                    recipeFingerprint
                }
            }
        },
        result = new
        {
            status,
            testedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            tester = "smoke",
            emulator = "DuckStation",
            bootedLevel = passed,
            objectAppeared = passed,
            mobyRecordsPresent = passed,
            actorRootsPresent = passed,
            behaviorCorrect = passed,
            noNearbyRegression = passed,
            notes
        },
        behaviorChecks = behaviorChecks.Select(check => new { id = check.Id, label = check.Label, passed = (bool?)passed }).ToArray()
    };
    await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
}

async Task EnsureSyntheticCandidateFailureAsync(string resultPath, LevelDefinition level, string recipeId, string notes)
{
    if (File.Exists(resultPath))
    {
        try
        {
            using FileStream existingStream = File.OpenRead(resultPath);
            using JsonDocument existing = JsonDocument.Parse(existingStream);
            JsonElement resultElement = existing.RootElement.TryGetProperty("result", out JsonElement nestedResult) && nestedResult.ValueKind == JsonValueKind.Object
                ? nestedResult
                : existing.RootElement;
            string status = ReadJsonString(resultElement, "status", "untested");
            if (!string.Equals(status, "untested", StringComparison.OrdinalIgnoreCase))
                return;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Spring Chest failed-evidence seed: replacing unreadable result file {Path.GetFileName(resultPath)} ({ex.Message}).");
        }
    }

    Directory.CreateDirectory(Path.GetDirectoryName(resultPath) ?? ".");
    var result = new
    {
        generatedBy = "Spyro.Editor.Smoke",
        purpose = "Synthetic smoke seed matching the user-reported in-game failure for this disposable cross-level moby candidate.",
        candidate = new
        {
            levelKey = level.Key,
            levelName = level.DisplayName,
            recipes = new[]
            {
                new
                {
                    label = "Spring Chest",
                    templateId = "common.spring_chest.townsquare.t82",
                    family = "springChest",
                    recipeId,
                    recipeStatus = "experimental-plan-only"
                }
            }
        },
        result = new
        {
            status = "tested-fail",
            testedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            tester = "user report",
            emulator = "DuckStation",
            bootedLevel = false,
            objectAppeared = false,
            mobyRecordsPresent = (bool?)null,
            actorRootsPresent = (bool?)null,
            behaviorCorrect = false,
            noNearbyRegression = false,
            notes
        },
        behaviorChecks = new[]
        {
            new { id = "springchest-appears", label = "Spring Chest appears at the placed location.", passed = false },
            new { id = "springchest-pops", label = "Breaking it uses Spring Chest pop behavior instead of normal chest or gem behavior.", passed = false },
            new { id = "springchest-normal-chest-regression", label = "A normal chest in the same level still breaks and rewards normally afterward.", passed = false }
        }
    };
    await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
}

async Task EnsureSyntheticSpringChestWrongBehaviorFailureAsync(string resultPath, LevelDefinition level, string recipeId, string notes)
{
    if (File.Exists(resultPath))
    {
        try
        {
            using FileStream existingStream = File.OpenRead(resultPath);
            using JsonDocument existing = JsonDocument.Parse(existingStream);
            JsonElement resultElement = existing.RootElement.TryGetProperty("result", out JsonElement nestedResult) && nestedResult.ValueKind == JsonValueKind.Object
                ? nestedResult
                : existing.RootElement;
            string status = ReadJsonString(resultElement, "status", "untested");
            if (!string.Equals(status, "untested", StringComparison.OrdinalIgnoreCase))
                return;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Spring Chest behavior-failure seed: replacing unreadable result file {Path.GetFileName(resultPath)} ({ex.Message}).");
        }
    }

    Directory.CreateDirectory(Path.GetDirectoryName(resultPath) ?? ".");
    var result = new
    {
        generatedBy = "Spyro.Editor.Smoke",
        purpose = "Synthetic smoke seed matching the user-reported in-game wrong-behavior result for this disposable cross-level moby candidate.",
        candidate = new
        {
            levelKey = level.Key,
            levelName = level.DisplayName,
            recipes = new[]
            {
                new
                {
                    label = "Spring Chest",
                    templateId = "common.spring_chest.townsquare.t82",
                    family = "springChest",
                    recipeId,
                    recipeStatus = "experimental-plan-only"
                }
            }
        },
        result = new
        {
            status = "tested-fail",
            testedAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            tester = "user report",
            emulator = "DuckStation",
            bootedLevel = true,
            objectAppeared = true,
            mobyRecordsPresent = (bool?)true,
            actorRootsPresent = (bool?)null,
            behaviorCorrect = false,
            noNearbyRegression = true,
            notes
        },
        behaviorChecks = new[]
        {
            new { id = "springchest-appears", label = "Spring Chest appears at the placed location.", passed = (bool?)true },
            new { id = "springchest-pops", label = "Breaking it uses Spring Chest pop behavior instead of normal chest or gem behavior.", passed = (bool?)false },
            new { id = "springchest-normal-chest-regression", label = "A normal chest in the same level still breaks and rewards normally afterward.", passed = (bool?)true }
        }
    };
    await File.WriteAllTextAsync(resultPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
}

bool? ReadBehaviorChecksPassed(JsonElement root)
{
    if (!root.TryGetProperty("behaviorChecks", out JsonElement checks) || checks.ValueKind != JsonValueKind.Array)
        return null;

    bool any = false;
    foreach (JsonElement check in checks.EnumerateArray())
    {
        if (check.ValueKind != JsonValueKind.Object)
            continue;
        any = true;
        bool? passed = ReadJsonNullableBool(check, "passed");
        if (passed == false)
            return false;
        if (passed == null)
            any = false;
    }
    return any ? true : null;
}

bool? ReadJsonNullableBool(JsonElement element, string name)
{
    if (!element.TryGetProperty(name, out JsonElement property))
        return null;

    return property.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null
    };
}

int ParseHexInt32(string hexPreview)
{
    try
    {
        byte[] bytes = ParseHexPreview(hexPreview);
        return bytes.Length >= 4 ? BitConverter.ToInt32(bytes, 0) : -1;
    }
    catch
    {
        return -1;
    }
}

void VerifyPatchBytes(string outputImagePath, MobySourcePatchPlan plan)
{
    SourceDiscLayout layout = DetectSourceDiscLayout(outputImagePath);
    using FileStream stream = File.OpenRead(outputImagePath);
    SourceDiscFileRecord? executable = null;
    foreach (MobySourcePatch patch in plan.Patches)
    {
        byte[] expected = ParseHexPreview(patch.AfterHexPreview);
        byte[] actual;
        if (TryParseWadPatchOffset(patch.WadRelativeOffset, out long wadOffset))
        {
            actual = ReadSourceWadBytes(stream, layout, wadOffset, expected.Length);
        }
        else if (TryParseExePatchOffset(patch.WadRelativeOffset, out long exeFileOffset))
        {
            executable ??= FindSourceExecutable(stream, layout);
            actual = ReadSourceFileBytes(stream, layout, executable.Lba, exeFileOffset, expected.Length);
        }
        else
        {
            actual = new byte[expected.Length];
            stream.Position = ParseFlexibleLong(patch.ImageOffset);
            int read = stream.Read(actual, 0, actual.Length);
            if (read != actual.Length)
                throw new InvalidOperationException($"{plan.LevelName} exported BIN does not contain expected {patch.Kind} bytes for {patch.MobyLabel} at {patch.ImageOffset}.");
        }

        if (!actual.SequenceEqual(expected))
            throw new InvalidOperationException($"{plan.LevelName} exported BIN does not contain expected {patch.Kind} bytes for {patch.MobyLabel} at {patch.ImageOffset}.");
    }
}

bool TryParseWadPatchOffset(string text, out long value)
{
    value = 0;
    string trimmed = (text ?? "").Trim();
    if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("exe:", StringComparison.OrdinalIgnoreCase))
        return false;
    value = ParseFlexibleLong(trimmed);
    return true;
}

bool TryParseExePatchOffset(string text, out long fileOffset)
{
    const uint exeDestination = 0x80010000;
    fileOffset = 0;
    string trimmed = (text ?? "").Trim();
    if (!trimmed.StartsWith("exe:", StringComparison.OrdinalIgnoreCase))
        return false;
    uint runtimeAddress = checked((uint)ParseFlexibleLong(trimmed[4..]));
    if (runtimeAddress < exeDestination)
        throw new InvalidOperationException($"EXE patch address is outside the executable range: {text}");

    fileOffset = 0x800 + ((long)runtimeAddress - exeDestination);
    return true;
}

async Task ReportExistingGemValueRoundTrip()
{
    Directory.CreateDirectory(Path.Combine(workspace.RootPath, "_local", "smoke"));
    string path = Path.Combine(workspace.RootPath, "_local", "smoke", "moby-existing-gem-value-native-edits.json");
    Moby original = new()
    {
        Index = 700,
        TrueIndex = 700,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(700),
        Position = new Vector3f(10, 20, 30),
        OriginalPosition = new Vector3f(10, 20, 30),
        Type = 0x18,
        OriginalType = 0x18,
        State = 0,
        OriginalState = 0,
        SourceByte36 = GemValue.Red.IdByte,
        OriginalSourceByte36 = GemValue.Red.IdByte,
        SourceByte4F = GemValue.Red.ValueByte,
        OriginalSourceByte4F = GemValue.Red.ValueByte,
        Flag4A = 0,
        Flag4B = GemValue.Red.IdByte,
        OriginalFlag4B = GemValue.Red.IdByte,
        Color = GemValue.Red.Color,
        Label = GemValue.Red.DisplayName,
        OriginalLabel = GemValue.Red.DisplayName,
        PatchStatus = "smoke",
        PatchLead = "existing-gem-value"
    };
    Moby edited = CloneMoby(original);
    edited.ApplyGem(GemValue.Purple);

    await MobyEditStore.SaveAsync(path, [edited], "Existing gem smoke");
    List<Moby> loadedMobys = [original];
    int applied = MobyEditStore.Load(path, loadedMobys);
    Moby loaded = loadedMobys[0];
    bool purpleBytes = loaded.SourceByte36 == GemValue.Purple.IdByte &&
        loaded.SourceByte4F == GemValue.Purple.ValueByte &&
        loaded.Gem == GemValue.Purple &&
        loaded.Color == GemValue.Purple.Color;
    Console.WriteLine(purpleBytes
        ? $"Existing gem value roundtrip: {applied} edit(s), red -> {loaded.Gem.DisplayName}, bytes 0x{loaded.SourceByte36:X2}/0x{loaded.SourceByte4F:X2}"
        : $"Existing gem value roundtrip: failed, bytes 0x{loaded.SourceByte36:X2}/0x{loaded.SourceByte4F:X2}");
}

Moby CloneMoby(Moby source)
{
    return new Moby
    {
        Index = source.Index,
        TrueIndex = source.TrueIndex,
        LegacyIndex = source.LegacyIndex,
        Position = source.Position,
        OriginalPosition = source.OriginalPosition,
        Type = source.Type,
        OriginalType = source.OriginalType,
        State = source.State,
        OriginalState = source.OriginalState,
        RuntimeAddress = source.RuntimeAddress,
        SpecialDataPointer = source.SpecialDataPointer,
        SourceByte36 = source.SourceByte36,
        OriginalSourceByte36 = source.OriginalSourceByte36,
        SourceByte37 = source.SourceByte37,
        OriginalSourceByte37 = source.OriginalSourceByte37,
        SourceByte4F = source.SourceByte4F,
        OriginalSourceByte4F = source.OriginalSourceByte4F,
        Flag4A = source.Flag4A,
        OriginalFlag4A = source.OriginalFlag4A,
        Flag4B = source.Flag4B,
        OriginalFlag4B = source.OriginalFlag4B,
        Color = source.Color,
        Label = source.Label,
        OriginalLabel = source.OriginalLabel,
        PatchStatus = source.PatchStatus,
        PatchLead = source.PatchLead
    };
}

async Task ReportRewardGemValueRoundTrip()
{
    Directory.CreateDirectory(Path.Combine(workspace.RootPath, "_local", "smoke"));
    string path = Path.Combine(workspace.RootPath, "_local", "smoke", "moby-reward-gem-value-native-edits.json");
    Moby original = new()
    {
        Index = 701,
        TrueIndex = 701,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(701),
        Position = new Vector3f(20, 30, 40),
        OriginalPosition = new Vector3f(20, 30, 40),
        Type = 0x20,
        OriginalType = 0x20,
        State = 0,
        OriginalState = 0,
        SourceByte36 = 0x86,
        OriginalSourceByte36 = 0x86,
        SourceByte4F = 0,
        OriginalSourceByte4F = 0,
        Flag4A = 0x10,
        Flag4B = GemValue.Red.IdByte,
        OriginalFlag4B = GemValue.Red.IdByte,
        Color = Moby.ColorForType(0x20),
        Label = "Smoke reward object (Red reward)",
        OriginalLabel = "Smoke reward object (Red reward)",
        PatchStatus = "smoke",
        PatchLead = "reward-gem-value"
    };
    Moby edited = CloneMoby(original);
    edited.Flag4B = GemValue.Purple.IdByte;

    await MobyEditStore.SaveAsync(path, [edited], "Reward gem smoke");
    List<Moby> loadedMobys = [original];
    int applied = MobyEditStore.Load(path, loadedMobys);
    Moby loaded = loadedMobys[0];
    bool purpleReward = loaded.SourceByte36 == 0x86 && loaded.Flag4B == GemValue.Purple.IdByte;
    Console.WriteLine(purpleReward
        ? $"Reward gem value roundtrip: {applied} edit(s), reward byte 0x{loaded.Flag4B:X2}"
        : $"Reward gem value roundtrip: failed, source 0x{loaded.SourceByte36:X2}, reward 0x{loaded.Flag4B:X2}");
}

async Task ReportMobySourcePatchPlan(LevelDefinition level, List<Moby> sourceMobys)
{
    if (!File.Exists(sourceImage))
    {
        Console.WriteLine("Stone Hill object source patch: source disc not found; skipping export smoke.");
        return;
    }

    Moby? donor = sourceMobys.FirstOrDefault(moby => moby.TrueIndex >= 0 && moby.TrueIndex < level.SourceRecordCount);
    Moby? removeDonor = sourceMobys.FirstOrDefault(moby => donor != null && moby.TrueIndex >= 0 && moby.TrueIndex < level.SourceRecordCount && moby.TrueIndex != donor.TrueIndex);
    if (donor == null || removeDonor == null)
    {
        Console.WriteLine("Stone Hill object source patch: not enough source-table mobys found.");
        return;
    }

    Directory.CreateDirectory(Path.Combine(workspace.RootPath, "_local", "smoke"));
    string path = Path.Combine(workspace.RootPath, "_local", "smoke", "moby-source-patch-native-edits.json");
    Moby moved = new()
    {
        Index = donor.Index,
        TrueIndex = donor.TrueIndex,
        LegacyIndex = donor.LegacyIndex,
        Position = new Vector3f(donor.OriginalPosition.X + 4, donor.OriginalPosition.Y - 2, donor.OriginalPosition.Z + 1),
        OriginalPosition = donor.OriginalPosition,
        Type = donor.Type,
        OriginalType = donor.OriginalType,
        State = donor.State,
        OriginalState = donor.OriginalState,
        RuntimeAddress = donor.RuntimeAddress,
        SpecialDataPointer = donor.SpecialDataPointer,
        SourceByte36 = donor.SourceByte36,
        OriginalSourceByte36 = donor.OriginalSourceByte36,
        SourceByte37 = donor.SourceByte37,
        SourceByte4F = donor.SourceByte4F,
        OriginalSourceByte4F = donor.OriginalSourceByte4F,
        Flag4A = donor.Flag4A,
        Flag4B = donor.Flag4B,
        OriginalFlag4B = donor.OriginalFlag4B,
        Color = donor.Color,
        Label = donor.DisplayLabel,
        OriginalLabel = donor.OriginalLabel,
        PatchStatus = donor.PatchStatus,
        PatchLead = donor.PatchLead
    };
    Moby removed = new()
    {
        Index = removeDonor.Index,
        TrueIndex = removeDonor.TrueIndex,
        LegacyIndex = removeDonor.LegacyIndex,
        Position = removeDonor.OriginalPosition,
        OriginalPosition = removeDonor.OriginalPosition,
        Type = removeDonor.Type,
        OriginalType = removeDonor.OriginalType,
        State = removeDonor.State,
        OriginalState = removeDonor.OriginalState,
        RuntimeAddress = removeDonor.RuntimeAddress,
        SpecialDataPointer = removeDonor.SpecialDataPointer,
        SourceByte36 = removeDonor.SourceByte36,
        OriginalSourceByte36 = removeDonor.OriginalSourceByte36,
        SourceByte37 = removeDonor.SourceByte37,
        SourceByte4F = removeDonor.SourceByte4F,
        OriginalSourceByte4F = removeDonor.OriginalSourceByte4F,
        Flag4A = removeDonor.Flag4A,
        Flag4B = removeDonor.Flag4B,
        OriginalFlag4B = removeDonor.OriginalFlag4B,
        Color = removeDonor.Color,
        Label = removeDonor.DisplayLabel,
        OriginalLabel = removeDonor.OriginalLabel,
        PatchStatus = removeDonor.PatchStatus,
        PatchLead = removeDonor.PatchLead,
        IsRemoved = true
    };
    int addedTrueIndex = sourceMobys.Max(moby => moby.TrueIndex) + 1;
    Moby added = new()
    {
        Index = sourceMobys.Max(moby => moby.Index) + 1,
        TrueIndex = addedTrueIndex,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(addedTrueIndex),
        Position = new Vector3f(donor.OriginalPosition.X + 8, donor.OriginalPosition.Y + 8, donor.OriginalPosition.Z),
        OriginalPosition = new Vector3f(donor.OriginalPosition.X + 8, donor.OriginalPosition.Y + 8, donor.OriginalPosition.Z),
        Type = 0x18,
        OriginalType = 0x18,
        State = 0,
        OriginalState = 0,
        SourceByte36 = 0x53,
        OriginalSourceByte36 = 0x53,
        SourceByte37 = 0,
        SourceByte4F = 0x01,
        OriginalSourceByte4F = 0x01,
        Flag4A = 0x40,
        Flag4B = 0xFF,
        OriginalFlag4B = 0xFF,
        Color = GemValue.Red.Color,
        Label = "Smoke added red gem",
        OriginalLabel = "Smoke added red gem",
        PatchStatus = "smoke",
        PatchLead = "append",
        IsAdded = true
    };

    await MobyEditStore.SaveAsync(path, [moved, removed, added], "Stone Hill smoke");
    MobySourcePatchPlan plan = MobySourcePatchExporter.BuildPlan(
        sourceImage,
        DiscImageLocator.FindCueForImage(sourceImage),
        Path.Combine(workspace.RootPath, "_local", "objects", "smoke.bin"),
        Path.Combine(workspace.RootPath, "_local", "objects", "smoke.cue"),
        level,
        path);
    Console.WriteLine($"Stone Hill object source patch: {plan.PatchCount} patches, {plan.TotalPatchedBytes} bytes for move T{donor.TrueIndex}, remove T{removeDonor.TrueIndex}, add T{addedTrueIndex}");
    await ReportStoneHillMultiGemAddPatch(level, sourceMobys);
    await ReportStoneHillNativeKeyChestPairPatch(level, sourceMobys);
}

async Task ReportStoneHillMultiGemAddPatch(LevelDefinition level, List<Moby> sourceMobys)
{
    Moby? donor = sourceMobys.FirstOrDefault(moby =>
        moby.TrueIndex >= 0 &&
        moby.TrueIndex < level.SourceRecordCount &&
        moby.Type == 0x18 &&
        moby.SourceByte37 == 0x00 &&
        moby.Flag4A == 0x40 &&
        moby.Flag4B == 0xFF &&
        GemValue.TryFromIdByte(moby.SourceByte36, out _));
    if (donor == null)
    {
        Console.WriteLine("Stone Hill multi-gem add source patch: loose gem donor missing; skipping.");
        return;
    }

    int nextIndex = sourceMobys.Max(moby => moby.Index) + 1;
    int nextTrueIndex = sourceMobys.Max(moby => moby.TrueIndex) + 1;
    GemValue[] gems = [GemValue.Red, GemValue.Green, GemValue.Blue];
    List<Moby> addedGems = new();
    for (int i = 0; i < gems.Length; i++)
    {
        GemValue gem = gems[i];
        Vector3f position = new(
            donor.OriginalPosition.X + 96 + (i * 96),
            donor.OriginalPosition.Y + 64,
            donor.OriginalPosition.Z);
        addedGems.Add(new Moby
        {
            Index = nextIndex + i,
            TrueIndex = nextTrueIndex + i,
            LegacyIndex = MobyLoader.GetLegacyAliasIndex(nextTrueIndex + i),
            Position = position,
            OriginalPosition = position,
            Type = 0x18,
            OriginalType = 0x18,
            State = 0,
            OriginalState = 0,
            SourceByte36 = gem.IdByte,
            OriginalSourceByte36 = gem.IdByte,
            SourceByte37 = 0,
            OriginalSourceByte37 = 0,
            SourceByte4F = gem.ValueByte,
            OriginalSourceByte4F = gem.ValueByte,
            Flag4A = 0x40,
            OriginalFlag4A = 0x40,
            Flag4B = 0xFF,
            OriginalFlag4B = 0xFF,
            Color = gem.Color,
            Label = $"Smoke added {gem.Name}",
            OriginalLabel = $"Smoke added {gem.Name}",
            PatchStatus = "smoke",
            PatchLead = "multi-append",
            IsAdded = true
        });
    }

    string path = Path.Combine(workspace.RootPath, "_local", "smoke", "stonehill-multi-gem-add-native-edits.json");
    await MobyEditStore.SaveAsync(path, addedGems, "Stone Hill multi-gem add");
    MobySourcePatchResult exportResult = await MobySourcePatchExporter.ExportAsync(new MobySourcePatchRequest(
        SourceImagePath: sourceImage,
        SourceCuePath: DiscImageLocator.FindCueForImage(sourceImage),
        OutputPrefix: Path.Combine(workspace.RootPath, "_local", "objects", "stonehill-multi-gem-add"),
        Level: level,
        NativeEditsPath: path,
        WriteImage: true));
    MobySourcePatchPlan plan = exportResult.Plan;
    if (!exportResult.WroteImage)
        throw new InvalidOperationException("Stone Hill multi-gem add did not write a disposable BIN.");
    VerifyPatchBytes(exportResult.OutputImagePath, plan);

    if (plan.SkippedEdits.Count != 0)
        throw new InvalidOperationException($"Stone Hill multi-gem add skipped edit(s): {string.Join("; ", plan.SkippedEdits)}");

    List<MobySourcePatch> appendPatches = plan.Patches
        .Where(patch => string.Equals(patch.Kind, "moby-record-append", StringComparison.OrdinalIgnoreCase))
        .OrderBy(patch => patch.TrueIndex)
        .ToList();
    if (appendPatches.Count != gems.Length)
        throw new InvalidOperationException($"Stone Hill multi-gem add wrote {appendPatches.Count} append record(s), expected {gems.Length}.");

    List<MobySourcePatch> specialClonePatches = plan.Patches
        .Where(patch => string.Equals(patch.Kind, "moby-special-data-clone", StringComparison.OrdinalIgnoreCase))
        .OrderBy(patch => patch.TrueIndex)
        .ToList();
    if (specialClonePatches.Count != gems.Length)
        throw new InvalidOperationException($"Stone Hill multi-gem add wrote {specialClonePatches.Count} special-data clone(s), expected {gems.Length}.");

    HashSet<uint> appendedSpecialDataOffsets = new();
    for (int i = 0; i < gems.Length; i++)
    {
        GemValue gem = gems[i];
        MobySourcePatch patch = appendPatches[i];
        byte[] after = ParseHexPreview(patch.AfterHexPreview);
        if (patch.TrueIndex != level.SourceRecordCount + i)
            throw new InvalidOperationException($"Stone Hill multi-gem add appended {patch.MobyLabel} at T{patch.TrueIndex}, expected T{level.SourceRecordCount + i}.");
        if (after.Length <= 0x53 ||
            after[0x50] != 0x18 ||
            after[0x36] != gem.IdByte ||
            after[0x37] != 0x00 ||
            after[0x4F] != gem.ValueByte ||
            after[0x52] != 0x40 ||
            after[0x53] != 0xFF)
        {
            throw new InvalidOperationException($"Stone Hill multi-gem add wrote incorrect source bytes for {gem.Name}.");
        }

        if (!specialClonePatches.Any(clone => clone.TrueIndex == patch.TrueIndex))
            throw new InvalidOperationException($"Stone Hill multi-gem add did not clone special data for appended T{patch.TrueIndex}.");
        uint specialDataOffset = BitConverter.ToUInt32(after, 0);
        if (specialDataOffset == 0 || !appendedSpecialDataOffsets.Add(specialDataOffset))
            throw new InvalidOperationException($"Stone Hill multi-gem add reused special-data offset 0x{specialDataOffset:X8} for appended T{patch.TrueIndex}.");
    }

    MobySourcePatch? sourceCountPatch = plan.Patches.FirstOrDefault(patch =>
        string.Equals(patch.Kind, "moby-source-count", StringComparison.OrdinalIgnoreCase));
    if (sourceCountPatch == null || BitConverter.ToInt32(ParseHexPreview(sourceCountPatch.AfterHexPreview), 0) != level.SourceRecordCount + gems.Length)
        throw new InvalidOperationException("Stone Hill multi-gem add did not increase the source moby count by all added gems.");

    MobySourcePatch? treasurePatch = plan.Patches.FirstOrDefault(patch =>
        string.Equals(patch.Kind, "level-treasure-total", StringComparison.OrdinalIgnoreCase));
    int expectedTreasureDelta = gems.Sum(gem => gem.Value);
    if (treasurePatch == null)
        throw new InvalidOperationException("Stone Hill multi-gem add did not update the in-game treasure target.");
    int beforeTreasure = BitConverter.ToUInt16(ParseHexPreview(treasurePatch.BeforeHexPreview), 0);
    int afterTreasure = BitConverter.ToUInt16(ParseHexPreview(treasurePatch.AfterHexPreview), 0);
    if (afterTreasure - beforeTreasure != expectedTreasureDelta)
        throw new InvalidOperationException($"Stone Hill multi-gem add changed treasure by {afterTreasure - beforeTreasure}, expected {expectedTreasureDelta}.");

    Console.WriteLine($"Stone Hill multi-gem add source patch: {appendPatches.Count} append(s), source count {level.SourceRecordCount}->{level.SourceRecordCount + gems.Length}, treasure +{expectedTreasureDelta}, BIN bytes verified");
}

async Task ReportStoneHillNativeKeyChestPairPatch(LevelDefinition level, List<Moby> sourceMobys)
{
    Moby? nativeKey = sourceMobys.FirstOrDefault(moby =>
        moby.TrueIndex >= 0 &&
        moby.TrueIndex < level.SourceRecordCount &&
        moby.SourceByte36 == 0xAD &&
        moby.SourceByte37 == 0x00);
    Moby? nativeKeyChest = sourceMobys.FirstOrDefault(moby =>
        moby.TrueIndex >= 0 &&
        moby.TrueIndex < level.SourceRecordCount &&
        moby.SourceByte36 == 0xAE &&
        moby.SourceByte37 == 0x00);
    Moby? positionDonor = nativeKeyChest ?? sourceMobys.FirstOrDefault(moby => moby.TrueIndex >= 0 && moby.TrueIndex < level.SourceRecordCount);
    if (nativeKey == null || nativeKeyChest == null || positionDonor == null)
    {
        Console.WriteLine("Stone Hill native key chest pair: native key/key-chest donor missing; skipping.");
        return;
    }

    int nextIndex = sourceMobys.Max(moby => moby.Index) + 1;
    int nextTrueIndex = sourceMobys.Max(moby => moby.TrueIndex) + 1;
    Vector3f keyChestPosition = new(positionDonor.OriginalPosition.X + 96, positionDonor.OriginalPosition.Y, positionDonor.OriginalPosition.Z);
    Moby key = new()
    {
        Index = nextIndex,
        TrueIndex = nextTrueIndex,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(nextTrueIndex),
        Position = new Vector3f(keyChestPosition.X - 160, keyChestPosition.Y, keyChestPosition.Z),
        OriginalPosition = new Vector3f(keyChestPosition.X - 160, keyChestPosition.Y, keyChestPosition.Z),
        Type = 0x18,
        OriginalType = 0x18,
        State = 0,
        OriginalState = 0,
        SourceByte36 = 0xAD,
        OriginalSourceByte36 = 0xAD,
        SourceByte37 = 0x00,
        SourceByte4F = 0x02,
        OriginalSourceByte4F = 0x02,
        Flag4A = 0x40,
        Flag4B = 0xFF,
        OriginalFlag4B = 0xFF,
        Color = GemValue.Yellow.Color,
        Label = "Stone Hill native Key",
        OriginalLabel = "Stone Hill native Key",
        PatchStatus = "native-clone",
        PatchLead = $"Same-level clone from Stone Hill key donor T{nativeKey.TrueIndex}.",
        IsAdded = true
    };
    Moby keyChest = new()
    {
        Index = nextIndex + 1,
        TrueIndex = nextTrueIndex + 1,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(nextTrueIndex + 1),
        Position = keyChestPosition,
        OriginalPosition = keyChestPosition,
        Type = 0x20,
        OriginalType = 0x20,
        State = 0,
        OriginalState = 0,
        SourceByte36 = 0xAE,
        OriginalSourceByte36 = 0xAE,
        SourceByte37 = 0x00,
        SourceByte4F = 0x00,
        OriginalSourceByte4F = 0x00,
        Flag4A = 0x10,
        Flag4B = 0x54,
        OriginalFlag4B = 0x54,
        Color = Moby.ColorForType(0x20),
        Label = "Stone Hill native Key Chest",
        OriginalLabel = "Stone Hill native Key Chest",
        PatchStatus = "native-clone",
        PatchLead = $"Same-level clone from Stone Hill Key Chest donor T{nativeKeyChest.TrueIndex}.",
        IsAdded = true
    };
    int? expectedKeyChestSector = null;
    string geometryPath = Path.Combine(workspace.RootPath, "editor-cache", $"{level.Key}-runtime-scene-editor-overlay.json");
    if (File.Exists(geometryPath))
    {
        GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(geometryPath);
        if (TryFindPlacementSector(geometry, keyChestPosition, out int placementSector))
            expectedKeyChestSector = placementSector;
    }

    string path = Path.Combine(workspace.RootPath, "_local", "smoke", "stonehill-native-key-chest-pair-native-edits.json");
    await MobyEditStore.SaveAsync(path, [key, keyChest], "Stone Hill native key chest pair");
    MobySourcePatchPlan plan = MobySourcePatchExporter.BuildPlan(
        sourceImage,
        DiscImageLocator.FindCueForImage(sourceImage),
        Path.Combine(workspace.RootPath, "_local", "objects", "stonehill-native-key-chest-pair.bin"),
        Path.Combine(workspace.RootPath, "_local", "objects", "stonehill-native-key-chest-pair.cue"),
        level,
        path);

    MobySourcePatch? keyPatch = plan.Patches.FirstOrDefault(patch =>
        string.Equals(patch.Kind, "moby-record-append", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(patch.MobyLabel, "Stone Hill native Key", StringComparison.OrdinalIgnoreCase));
    MobySourcePatch? keyChestPatch = plan.Patches.FirstOrDefault(patch =>
        string.Equals(patch.Kind, "moby-record-append", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(patch.MobyLabel, "Stone Hill native Key Chest", StringComparison.OrdinalIgnoreCase));
    byte[] keyBytes = keyPatch == null ? [] : ParseHexPreview(keyPatch.AfterHexPreview);
    byte[] keyChestBytes = keyChestPatch == null ? [] : ParseHexPreview(keyChestPatch.AfterHexPreview);
    if (keyBytes.Length <= 0x53 || keyBytes[0x36] != 0xAD || keyBytes[0x52] != 0x40)
        throw new InvalidOperationException("Stone Hill native key append did not preserve key identity bytes.");
    if (keyChestBytes.Length <= 0x53 ||
        keyChestBytes[0x36] != 0xAE ||
        keyChestBytes[0x37] != 0x00 ||
        keyChestBytes[0x52] != 0x10 ||
        keyChestBytes[0x53] != 0x54)
    {
        throw new InvalidOperationException("Stone Hill native Key Chest append did not preserve the 0x00AE key-chest identity bytes.");
    }
    if (expectedKeyChestSector is int expectedSector)
    {
        if (keyChestBytes.Length <= 0x4A || keyChestBytes[0x4A] != expectedSector)
            throw new InvalidOperationException($"Stone Hill native Key Chest append wrote sector byte 0x{(keyChestBytes.Length <= 0x4A ? -1 : keyChestBytes[0x4A]):X2}, expected 0x{expectedSector:X2}.");
        if (keyChestPatch == null || !keyChestPatch.Description.Contains($"Placement sector byte set to 0x{expectedSector:X2}", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Stone Hill native Key Chest append did not report its placement sector.");
    }
    if (plan.PackageImportPreviews.Any() || plan.Patches.Any(patch => patch.Kind.Contains("actor-package", StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException("Stone Hill native Key Chest pair should not need actor-package imports.");

    string sectorSummary = expectedKeyChestSector is int sector ? $", key chest sector=0x{sector:X2}" : "";
    Console.WriteLine($"Stone Hill native key chest pair: {plan.PatchCount} patch(es), key donor T{nativeKey.TrueIndex}, key chest donor T{nativeKeyChest.TrueIndex}{sectorSummary}, package imports=0");
}

async Task ReportPastedLooseGemPatch(string levelKey)
{
    if (!File.Exists(sourceImage))
    {
        Console.WriteLine($"{levelKey} pasted gem source patch: source disc not found; skipping export smoke.");
        return;
    }

    LevelDefinition? level = catalog.FindByKey(levelKey);
    if (level == null || !level.HasSourceTable)
    {
        Console.WriteLine($"{levelKey} pasted gem source patch: source table is not mapped; skipping.");
        return;
    }

    string mobyPath = Path.Combine(workspace.RootPath, "editor-cache", $"{levelKey}-mobys.json");
    if (!File.Exists(mobyPath))
    {
        Console.WriteLine($"{levelKey} pasted gem source patch: moby cache not found; skipping.");
        return;
    }

    List<Moby> sourceMobys = MobyLoader.LoadCached(mobyPath).ToList();
    Moby? donor = sourceMobys.FirstOrDefault(moby =>
        moby.TrueIndex >= 0 &&
        moby.TrueIndex < level.SourceRecordCount &&
        moby.Type == 0x18 &&
        moby.SourceByte37 == 0x00 &&
        moby.Flag4A == 0x40 &&
        moby.Flag4B == 0xFF &&
        GemValue.TryFromIdByte(moby.SourceByte36, out _));
    if (donor == null || !GemValue.TryFromIdByte(donor.SourceByte36, out GemValue copiedGem))
    {
        Console.WriteLine($"{level.DisplayName} pasted gem source patch: loose gem donor missing; skipping.");
        return;
    }

    bool isDarkHollow = string.Equals(levelKey, "darkhollow", StringComparison.OrdinalIgnoreCase);
    Moby placementAnchor = isDarkHollow
        ? sourceMobys.FirstOrDefault(moby => moby.TrueIndex == 59) ?? donor
        : donor;
    GemValue[] pastedGemValues = isDarkHollow
        ? [GemValue.Red, GemValue.Red, GemValue.Red, GemValue.Red, GemValue.Green, GemValue.Green, GemValue.Green, GemValue.Blue, GemValue.Blue, GemValue.Blue]
        : Enumerable.Repeat(copiedGem, 10).ToArray();
    Vector3f[] darkHollowPlatformPlacements = isDarkHollow
        ?
        [
            new(25646 / 16f, 56974 / 16f, 17920 / 16f),
            new(24578 / 16f, 55865 / 16f, 17920 / 16f),
            new(26565 / 16f, 55990 / 16f, 17920 / 16f),
            new(25581 / 16f, 55110 / 16f, 17920 / 16f),
            new(27366 / 16f, 63454 / 16f, 17152 / 16f),
            new(25611 / 16f, 63326 / 16f, 17152 / 16f),
            new(24074 / 16f, 64271 / 16f, 17152 / 16f),
            new(16932 / 16f, 64307 / 16f, 17152 / 16f),
            new(15480 / 16f, 65729 / 16f, 17152 / 16f),
            new(12966 / 16f, 65794 / 16f, 17152 / 16f)
        ]
        : [];
    int pastedCount = pastedGemValues.Length;
    if (isDarkHollow && darkHollowPlatformPlacements.Length != pastedCount)
        throw new InvalidOperationException("Dark Hollow pasted gem smoke placements do not match the gem count.");

    GeometryCandidate? placementGeometry = null;
    List<int> expectedPlacementSectors = new();
    if (isDarkHollow)
    {
        string geometryPath = Path.Combine(workspace.RootPath, "editor-cache", $"{levelKey}-runtime-scene-editor-overlay.json");
        if (!File.Exists(geometryPath))
            throw new InvalidOperationException($"{level.DisplayName} pasted gem source patch needs the terrain overlay to verify placement sectors.");
        placementGeometry = GeometryOverlayLoader.LoadFirstCandidate(geometryPath);
    }

    int nextIndex = sourceMobys.Max(moby => moby.Index) + 1;
    int nextTrueIndex = sourceMobys.Max(moby => moby.TrueIndex) + 1;
    List<Moby> pastedGems = new();
    for (int i = 0; i < pastedCount; i++)
    {
        GemValue gem = pastedGemValues[i];
        Vector3f position = isDarkHollow
            ? darkHollowPlatformPlacements[i]
            : new Vector3f(
                placementAnchor.OriginalPosition.X + (((i % 5) - 2) * 12),
                placementAnchor.OriginalPosition.Y + 24 + ((i / 5) * 12),
                placementAnchor.OriginalPosition.Z);
        if (placementGeometry != null)
        {
            if (!TryFindPlacementSector(placementGeometry, position, out int placementSector))
                throw new InvalidOperationException($"{level.DisplayName} pasted gem source patch could not find a terrain sector for placement {i + 1}.");
            expectedPlacementSectors.Add(placementSector);
        }

        pastedGems.Add(new Moby
        {
            Index = nextIndex + i,
            TrueIndex = nextTrueIndex + i,
            LegacyIndex = MobyLoader.GetLegacyAliasIndex(nextTrueIndex + i),
            Position = position,
            OriginalPosition = position,
            Type = donor.Type,
            OriginalType = donor.Type,
            State = donor.State,
            OriginalState = donor.State,
            SourceByte36 = gem.IdByte,
            OriginalSourceByte36 = gem.IdByte,
            SourceByte37 = donor.SourceByte37,
            OriginalSourceByte37 = donor.SourceByte37,
            SourceByte4F = gem.ValueByte,
            OriginalSourceByte4F = gem.ValueByte,
            Flag4A = donor.Flag4A,
            OriginalFlag4A = donor.Flag4A,
            Flag4B = donor.Flag4B,
            OriginalFlag4B = donor.Flag4B,
            Color = gem.Color,
            Label = string.Equals(gem.Name, copiedGem.Name, StringComparison.OrdinalIgnoreCase) ? $"Copy of {donor.DisplayLabel}" : $"Smoke added {gem.Name}",
            OriginalLabel = string.Equals(gem.Name, copiedGem.Name, StringComparison.OrdinalIgnoreCase) ? $"Copy of {donor.DisplayLabel}" : $"Smoke added {gem.Name}",
            PatchStatus = "smoke-copy-paste",
            PatchLead = $"Pasted from copied {donor.DisplayLabel}; exports as a new native source-table record.",
            IsAdded = true
        });
    }

    string path = Path.Combine(workspace.RootPath, "_local", "smoke", $"{levelKey}-pasted-gem-native-edits.json");
    await MobyEditStore.SaveAsync(path, pastedGems, $"{level.DisplayName} pasted gem add");
    MobySourcePatchResult exportResult = await MobySourcePatchExporter.ExportAsync(new MobySourcePatchRequest(
        SourceImagePath: sourceImage,
        SourceCuePath: DiscImageLocator.FindCueForImage(sourceImage),
        OutputPrefix: Path.Combine(workspace.RootPath, "_local", "objects", $"{levelKey}-pasted-gem-add"),
        Level: level,
        NativeEditsPath: path,
        WriteImage: true));
    MobySourcePatchPlan plan = exportResult.Plan;
    if (!exportResult.WroteImage)
        throw new InvalidOperationException($"{level.DisplayName} pasted gem source patch did not write a disposable BIN.");
    VerifyPatchBytes(exportResult.OutputImagePath, plan);

    if (plan.SkippedEdits.Count != 0)
        throw new InvalidOperationException($"{level.DisplayName} pasted gem source patch skipped edit(s): {string.Join("; ", plan.SkippedEdits)}");

    List<MobySourcePatch> appendPatches = plan.Patches
        .Where(patch => string.Equals(patch.Kind, "moby-record-append", StringComparison.OrdinalIgnoreCase))
        .OrderBy(patch => patch.TrueIndex)
        .ToList();
    if (appendPatches.Count != pastedCount)
        throw new InvalidOperationException($"{level.DisplayName} pasted gem source patch wrote {appendPatches.Count} append record(s), expected {pastedCount}.");

    List<MobySourcePatch> specialClonePatches = plan.Patches
        .Where(patch => string.Equals(patch.Kind, "moby-special-data-clone", StringComparison.OrdinalIgnoreCase))
        .OrderBy(patch => patch.TrueIndex)
        .ToList();
    if (specialClonePatches.Count != pastedCount)
        throw new InvalidOperationException($"{level.DisplayName} pasted gem source patch wrote {specialClonePatches.Count} special-data clone(s), expected {pastedCount}.");

    HashSet<uint> appendedSpecialDataOffsets = new();
    for (int i = 0; i < appendPatches.Count; i++)
    {
        MobySourcePatch patch = appendPatches[i];
        GemValue expectedGem = pastedGemValues[i];
        byte[] after = ParseHexPreview(patch.AfterHexPreview);
        if (patch.TrueIndex != level.SourceRecordCount + i)
            throw new InvalidOperationException($"{level.DisplayName} pasted gem source patch appended {patch.MobyLabel} at T{patch.TrueIndex}, expected T{level.SourceRecordCount + i}.");
        if (after.Length <= 0x53 ||
            after[0x50] != 0x18 ||
            after[0x36] != expectedGem.IdByte ||
            after[0x37] != 0x00 ||
            after[0x4F] != expectedGem.ValueByte ||
            after[0x52] != 0x40 ||
            after[0x53] != 0xFF)
        {
            throw new InvalidOperationException($"{level.DisplayName} pasted gem source patch wrote incorrect source bytes for {expectedGem.Name}.");
        }

        if (!specialClonePatches.Any(clone => clone.TrueIndex == patch.TrueIndex))
            throw new InvalidOperationException($"{level.DisplayName} pasted gem source patch did not clone special data for appended T{patch.TrueIndex}.");
        uint specialDataOffset = BitConverter.ToUInt32(after, 0);
        if (specialDataOffset == 0 || !appendedSpecialDataOffsets.Add(specialDataOffset))
            throw new InvalidOperationException($"{level.DisplayName} pasted gem source patch reused special-data offset 0x{specialDataOffset:X8} for appended T{patch.TrueIndex}.");

        if (expectedPlacementSectors.Count == pastedCount)
        {
            int expectedSector = expectedPlacementSectors[i];
            if (after.Length <= 0x4A || after[0x4A] != expectedSector)
                throw new InvalidOperationException($"{level.DisplayName} pasted gem source patch wrote sector byte 0x{(after.Length <= 0x4A ? -1 : after[0x4A]):X2} for appended T{patch.TrueIndex}, expected 0x{expectedSector:X2}.");
            if (!patch.Description.Contains($"Placement sector byte set to 0x{expectedSector:X2}", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"{level.DisplayName} pasted gem source patch did not report the placement sector for appended T{patch.TrueIndex}: {patch.Description}");
        }
    }

    MobySourcePatch? sourceCountPatch = plan.Patches.FirstOrDefault(patch =>
        string.Equals(patch.Kind, "moby-source-count", StringComparison.OrdinalIgnoreCase));
    if (sourceCountPatch == null || BitConverter.ToInt32(ParseHexPreview(sourceCountPatch.AfterHexPreview), 0) != level.SourceRecordCount + pastedCount)
        throw new InvalidOperationException($"{level.DisplayName} pasted gem source patch did not increase the source moby count by all pasted gems.");

    MobySourcePatch? treasurePatch = plan.Patches.FirstOrDefault(patch =>
        string.Equals(patch.Kind, "level-treasure-total", StringComparison.OrdinalIgnoreCase));
    int expectedTreasureDelta = pastedGemValues.Sum(gem => gem.Value);
    if (treasurePatch == null)
        throw new InvalidOperationException($"{level.DisplayName} pasted gem source patch did not update the in-game treasure target.");
    int beforeTreasure = BitConverter.ToUInt16(ParseHexPreview(treasurePatch.BeforeHexPreview), 0);
    int afterTreasure = BitConverter.ToUInt16(ParseHexPreview(treasurePatch.AfterHexPreview), 0);
    if (afterTreasure - beforeTreasure != expectedTreasureDelta)
        throw new InvalidOperationException($"{level.DisplayName} pasted gem source patch changed treasure by {afterTreasure - beforeTreasure}, expected {expectedTreasureDelta}.");

    string placementSectorSummary = expectedPlacementSectors.Count == pastedCount
        ? $", placement sectors {string.Join(",", expectedPlacementSectors.Select(sector => $"0x{sector:X2}"))}"
        : "";
    Console.WriteLine($"{level.DisplayName} pasted gem source patch: {appendPatches.Count} loose gem append(s), source count {level.SourceRecordCount}->{level.SourceRecordCount + pastedCount}, treasure +{expectedTreasureDelta}{placementSectorSummary}, BIN bytes verified");
}

async Task ReportMixedCopiedObjectAppendGuard(string levelKey)
{
    if (!File.Exists(sourceImage))
    {
        Console.WriteLine($"{levelKey} mixed copied object guard: source disc not found; skipping.");
        return;
    }

    LevelDefinition? level = catalog.FindByKey(levelKey);
    if (level == null || !level.HasSourceTable)
    {
        Console.WriteLine($"{levelKey} mixed copied object guard: source table is not mapped; skipping.");
        return;
    }

    string mobyPath = Path.Combine(workspace.RootPath, "editor-cache", $"{levelKey}-mobys.json");
    if (!File.Exists(mobyPath))
    {
        Console.WriteLine($"{levelKey} mixed copied object guard: moby cache not found; skipping.");
        return;
    }

    List<Moby> sourceMobys = MobyLoader.LoadCached(mobyPath).ToList();
    Moby? gemDonor = sourceMobys.FirstOrDefault(moby =>
        moby.TrueIndex >= 0 &&
        moby.TrueIndex < level.SourceRecordCount &&
        moby.Type == 0x18 &&
        moby.SourceByte37 == 0x00 &&
        moby.Flag4A == 0x40 &&
        moby.Flag4B == 0xFF &&
        GemValue.TryFromIdByte(moby.SourceByte36, out _));
    Moby? gnorcDonor = sourceMobys.FirstOrDefault(moby =>
        moby.TrueIndex >= 0 &&
        moby.TrueIndex < level.SourceRecordCount &&
        moby.Type == 0x20 &&
        moby.SourceByte36 == 0xA5 &&
        moby.Flag4A == 0x10);
    Moby? flameChestDonor = sourceMobys.FirstOrDefault(moby =>
        moby.TrueIndex >= 0 &&
        moby.TrueIndex < level.SourceRecordCount &&
        moby.Type == 0x20 &&
        moby.SourceByte36 == 0xC2 &&
        moby.Flag4A == 0x10);
    Moby? treeDonor = sourceMobys.FirstOrDefault(moby =>
        moby.TrueIndex >= 0 &&
        moby.TrueIndex < level.SourceRecordCount &&
        moby.Type == 0x20 &&
        moby.SourceByte36 == 0x7F &&
        moby.Flag4A == 0x10);
    if (gemDonor == null || !GemValue.TryFromIdByte(gemDonor.SourceByte36, out GemValue gemValue) || gnorcDonor == null || flameChestDonor == null || treeDonor == null)
    {
        Console.WriteLine($"{level.DisplayName} mixed copied object guard: required donor set not found; skipping.");
        return;
    }

    int nextIndex = sourceMobys.Max(moby => moby.Index) + 1;
    int nextTrueIndex = sourceMobys.Max(moby => moby.TrueIndex) + 1;
    List<Moby> copiedObjects =
    [
        CopyAsAddedMoby(gemDonor, nextIndex, nextTrueIndex, "Guard copied red gem", 0),
        CopyAsAddedMoby(gnorcDonor, nextIndex + 1, nextTrueIndex + 1, "Guard copied small gnorc", 1),
        CopyAsAddedMoby(flameChestDonor, nextIndex + 2, nextTrueIndex + 2, "Guard copied flame chest", 2),
        CopyAsAddedMoby(treeDonor, nextIndex + 3, nextTrueIndex + 3, "Guard copied tree", 3),
        CopyAsAddedMoby(gemDonor, nextIndex + 4, nextTrueIndex + 4, "Guard copied red gem 2", 4)
    ];

    string path = Path.Combine(workspace.RootPath, "_local", "smoke", $"{levelKey}-mixed-copy-guard-native-edits.json");
    await MobyEditStore.SaveAsync(path, copiedObjects, $"{level.DisplayName} mixed copied object guard");
    MobySourcePatchPlan plan = MobySourcePatchExporter.BuildPlan(
        sourceImage,
        DiscImageLocator.FindCueForImage(sourceImage),
        Path.Combine(workspace.RootPath, "_local", "objects", $"{levelKey}-mixed-copy-guard.bin"),
        Path.Combine(workspace.RootPath, "_local", "objects", $"{levelKey}-mixed-copy-guard.cue"),
        level,
        path);

    List<MobySourcePatch> appendPatches = plan.Patches
        .Where(patch => string.Equals(patch.Kind, "moby-record-append", StringComparison.OrdinalIgnoreCase))
        .OrderBy(patch => patch.TrueIndex)
        .ToList();
    if (appendPatches.Count != 2 || appendPatches.Any(patch => !patch.MobyLabel.Contains("gem", StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException($"{level.DisplayName} mixed copied object guard appended unsafe copied object(s).");
    if (plan.SkippedEdits.Count != 3)
        throw new InvalidOperationException($"{level.DisplayName} mixed copied object guard skipped {plan.SkippedEdits.Count} edit(s), expected 3 unsafe copied objects.");

    MobySourcePatch? sourceCountPatch = plan.Patches.FirstOrDefault(patch =>
        string.Equals(patch.Kind, "moby-source-count", StringComparison.OrdinalIgnoreCase));
    if (sourceCountPatch == null || BitConverter.ToInt32(ParseHexPreview(sourceCountPatch.AfterHexPreview), 0) != level.SourceRecordCount + 2)
        throw new InvalidOperationException($"{level.DisplayName} mixed copied object guard did not limit the source count to the exported gems.");

    MobySourcePatch? treasurePatch = plan.Patches.FirstOrDefault(patch =>
        string.Equals(patch.Kind, "level-treasure-total", StringComparison.OrdinalIgnoreCase));
    int expectedTreasureDelta = gemValue.Value * 2;
    if (treasurePatch == null)
        throw new InvalidOperationException($"{level.DisplayName} mixed copied object guard did not update treasure for exported gems.");
    int beforeTreasure = BitConverter.ToUInt16(ParseHexPreview(treasurePatch.BeforeHexPreview), 0);
    int afterTreasure = BitConverter.ToUInt16(ParseHexPreview(treasurePatch.AfterHexPreview), 0);
    if (afterTreasure - beforeTreasure != expectedTreasureDelta)
        throw new InvalidOperationException($"{level.DisplayName} mixed copied object guard changed treasure by {afterTreasure - beforeTreasure}, expected {expectedTreasureDelta}.");

    Console.WriteLine($"{level.DisplayName} mixed copied object guard: exported {appendPatches.Count} gem append(s), skipped {plan.SkippedEdits.Count} unsafe copied object(s), treasure +{expectedTreasureDelta}");
}

async Task ReportNativeSlotReusePatch(string levelKey)
{
    if (!File.Exists(sourceImage))
    {
        Console.WriteLine($"{levelKey} native slot reuse patch: source disc not found; skipping.");
        return;
    }

    LevelDefinition? level = catalog.FindByKey(levelKey);
    if (level == null || !level.HasSourceTable)
    {
        Console.WriteLine($"{levelKey} native slot reuse patch: source table is not mapped; skipping.");
        return;
    }

    string mobyPath = Path.Combine(workspace.RootPath, "editor-cache", $"{levelKey}-mobys.json");
    if (!File.Exists(mobyPath))
    {
        Console.WriteLine($"{levelKey} native slot reuse patch: moby cache not found; skipping.");
        return;
    }

    List<Moby> sourceMobys = MobyLoader.LoadCached(mobyPath).ToList();
    Moby? donor = sourceMobys.FirstOrDefault(moby =>
        moby.TrueIndex >= 0 &&
        moby.TrueIndex < level.SourceRecordCount &&
        moby.Type == 0x20 &&
        moby.SourceByte36 == 0xA6 &&
        moby.Flag4A == 0x10)
        ?? sourceMobys.FirstOrDefault(moby =>
            moby.TrueIndex >= 0 &&
            moby.TrueIndex < level.SourceRecordCount &&
            moby.Type == 0x20 &&
            moby.SourceByte36 == 0x73 &&
            moby.Flag4A == 0x10);
    Moby? target = sourceMobys.FirstOrDefault(moby =>
        donor != null &&
        moby.TrueIndex >= 0 &&
        moby.TrueIndex < level.SourceRecordCount &&
        moby.TrueIndex != donor.TrueIndex &&
        moby.Type == 0x20 &&
        moby.SourceByte36 == 0x7F &&
        moby.Flag4A == 0x10)
        ?? sourceMobys.FirstOrDefault(moby =>
            donor != null &&
            moby.TrueIndex >= 0 &&
            moby.TrueIndex < level.SourceRecordCount &&
            moby.TrueIndex != donor.TrueIndex &&
            moby.Type == 0x20);
    if (donor == null || target == null)
    {
        Console.WriteLine($"{level.DisplayName} native slot reuse patch: required donor/target objects not found; skipping.");
        return;
    }

    Vector3f placedPosition = new(target.OriginalPosition.X + 32f, target.OriginalPosition.Y + 16f, target.OriginalPosition.Z);
    Moby slotReuse = new()
    {
        Index = target.Index,
        TrueIndex = target.TrueIndex,
        LegacyIndex = target.LegacyIndex,
        Position = placedPosition,
        OriginalPosition = target.OriginalPosition,
        Type = donor.Type,
        OriginalType = target.Type,
        State = donor.State,
        OriginalState = target.State,
        SourceByte36 = donor.SourceByte36,
        OriginalSourceByte36 = target.SourceByte36,
        SourceByte37 = donor.SourceByte37,
        OriginalSourceByte37 = target.SourceByte37,
        SourceByte4F = donor.SourceByte4F,
        OriginalSourceByte4F = target.SourceByte4F,
        Flag4A = donor.Flag4A,
        OriginalFlag4A = target.Flag4A,
        Flag4B = donor.Flag4B,
        OriginalFlag4B = target.Flag4B,
        Color = donor.Color,
        Label = donor.DisplayLabel,
        OriginalLabel = target.DisplayLabel,
        CandidateKind = donor.CandidateKind,
        Confidence = "native-slot-reuse",
        Evidence = $"Smoke cloned same-level donor T{donor.TrueIndex}.",
        PatchStatus = "native-slot-reuse",
        PatchLead = $"Clone same-level donor T{donor.TrueIndex} into existing slot T{target.TrueIndex}, preserving this slot's placed position.",
        SourceCloneLevelKey = level.Key,
        SourceCloneLevelName = level.DisplayName,
        SourceCloneTrueIndex = donor.TrueIndex
    };

    string path = Path.Combine(workspace.RootPath, "_local", "smoke", $"{levelKey}-native-slot-reuse-native-edits.json");
    await MobyEditStore.SaveAsync(path, [slotReuse], $"{level.DisplayName} native slot reuse");
    MobySourcePatchResult exportResult = await MobySourcePatchExporter.ExportAsync(new MobySourcePatchRequest(
        SourceImagePath: sourceImage,
        SourceCuePath: DiscImageLocator.FindCueForImage(sourceImage),
        OutputPrefix: Path.Combine(workspace.RootPath, "_local", "objects", $"{levelKey}-native-slot-reuse"),
        Level: level,
        NativeEditsPath: path,
        WriteImage: true));
    MobySourcePatchPlan plan = exportResult.Plan;
    if (!exportResult.WroteImage)
        throw new InvalidOperationException($"{level.DisplayName} native slot reuse did not write a disposable BIN.");
    VerifyPatchBytes(exportResult.OutputImagePath, plan);
    if (plan.SkippedEdits.Count != 0)
        throw new InvalidOperationException($"{level.DisplayName} native slot reuse skipped edit(s): {string.Join("; ", plan.SkippedEdits)}");

    List<MobySourcePatch> slotClonePatches = plan.Patches
        .Where(patch => string.Equals(patch.Kind, "moby-record-slot-clone", StringComparison.OrdinalIgnoreCase))
        .ToList();
    if (slotClonePatches.Count != 1 || slotClonePatches[0].TrueIndex != target.TrueIndex)
        throw new InvalidOperationException($"{level.DisplayName} native slot reuse wrote {slotClonePatches.Count} slot clone patch(es), expected one at T{target.TrueIndex}.");
    if (plan.Patches.Any(patch => string.Equals(patch.Kind, "moby-record-append", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(patch.Kind, "moby-source-count", StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException($"{level.DisplayName} native slot reuse unexpectedly appended a new source record.");

    byte[] after = ParseHexPreview(slotClonePatches[0].AfterHexPreview);
    const int xOffset = 0x0C;
    const int yOffset = 0x10;
    const int zOffset = 0x14;
    const int typeOffset = 0x50;
    const int stateOffset = 0x51;
    if (after.Length < 0x58 ||
        after[typeOffset] != donor.Type ||
        after[stateOffset] != donor.State ||
        after[0x36] != donor.SourceByte36 ||
        after[0x37] != donor.SourceByte37 ||
        after[0x4F] != donor.SourceByte4F ||
        after[0x52] != donor.Flag4A ||
        after[0x53] != donor.Flag4B ||
        BitConverter.ToInt32(after, xOffset) != ToSmokeRawCoordinate(placedPosition.X) ||
        BitConverter.ToInt32(after, yOffset) != ToSmokeRawCoordinate(placedPosition.Y) ||
        BitConverter.ToInt32(after, zOffset) != ToSmokeRawCoordinate(placedPosition.Z))
    {
        throw new InvalidOperationException($"{level.DisplayName} native slot reuse did not clone donor identity while preserving the placed position.");
    }

    Console.WriteLine($"{level.DisplayName} native slot reuse patch: cloned donor T{donor.TrueIndex} into slot T{target.TrueIndex}, no append/count patch, BIN bytes verified");
}

int ToSmokeRawCoordinate(float value) => (int)Math.Round(value * 16f);

Moby CopyAsAddedMoby(Moby donor, int index, int trueIndex, string label, int offsetStep)
{
    Vector3f position = new(donor.Position.X + (offsetStep * 24), donor.Position.Y, donor.Position.Z);
    return new Moby
    {
        Index = index,
        TrueIndex = trueIndex,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(trueIndex),
        Position = position,
        OriginalPosition = position,
        Type = donor.Type,
        OriginalType = donor.Type,
        State = donor.State,
        OriginalState = donor.State,
        SourceByte36 = donor.SourceByte36,
        OriginalSourceByte36 = donor.SourceByte36,
        SourceByte37 = donor.SourceByte37,
        OriginalSourceByte37 = donor.SourceByte37,
        SourceByte4F = donor.SourceByte4F,
        OriginalSourceByte4F = donor.SourceByte4F,
        Flag4A = donor.Flag4A,
        OriginalFlag4A = donor.Flag4A,
        Flag4B = donor.Flag4B,
        OriginalFlag4B = donor.Flag4B,
        Color = donor.Color,
        Label = label,
        OriginalLabel = label,
        PatchStatus = "smoke-copy-paste",
        PatchLead = $"Pasted from copied {donor.DisplayLabel}.",
        IsAdded = true
    };
}

async Task ReportAllLevelLooseGemPlacementSectors()
{
    if (!File.Exists(sourceImage))
    {
        Console.WriteLine("All-level loose gem placement sectors: source disc not found; skipping.");
        return;
    }

    int tested = 0;
    int skipped = 0;
    foreach (LevelDefinition level in catalog.Levels.Where(level => level.HasSourceTable))
    {
        string mobyPath = Path.Combine(workspace.RootPath, "editor-cache", $"{level.Key}-mobys.json");
        string geometryPath = Path.Combine(workspace.RootPath, "editor-cache", $"{level.Key}-runtime-scene-editor-overlay.json");
        if (!File.Exists(mobyPath) || !File.Exists(geometryPath))
        {
            skipped++;
            continue;
        }

        List<Moby> sourceMobys = MobyLoader.LoadCached(mobyPath).ToList();
        Moby? donor = sourceMobys.FirstOrDefault(moby =>
            moby.TrueIndex >= 0 &&
            moby.TrueIndex < level.SourceRecordCount &&
            moby.Type == 0x18 &&
            moby.SourceByte37 == 0x00 &&
            moby.Flag4A == 0x40 &&
            moby.Flag4B == 0xFF &&
            GemValue.TryFromIdByte(moby.SourceByte36, out _));
        if (donor == null || !GemValue.TryFromIdByte(donor.SourceByte36, out GemValue copiedGem))
        {
            skipped++;
            continue;
        }

        GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(geometryPath);
        TerrainPolygon? placement = geometry.Polygons
            .Where(polygon => !polygon.IsTerrainRemoved && polygon.SectorIndex is >= 0 and <= 255)
            .FirstOrDefault(polygon => polygon.TryGetZ(polygon.Center.X, polygon.Center.Y, out _));
        if (placement == null || !placement.TryGetZ(placement.Center.X, placement.Center.Y, out float placementZ))
        {
            skipped++;
            continue;
        }

        Vector3f position = new(placement.Center.X, placement.Center.Y, placementZ);
        if (!TryFindPlacementSector(geometry, position, out int expectedSector))
        {
            skipped++;
            continue;
        }

        int nextIndex = sourceMobys.Max(moby => moby.Index) + 1;
        int nextTrueIndex = sourceMobys.Max(moby => moby.TrueIndex) + 1;
        Moby addedGem = new()
        {
            Index = nextIndex,
            TrueIndex = nextTrueIndex,
            LegacyIndex = MobyLoader.GetLegacyAliasIndex(nextTrueIndex),
            Position = position,
            OriginalPosition = position,
            Type = donor.Type,
            OriginalType = donor.Type,
            State = donor.State,
            OriginalState = donor.State,
            SourceByte36 = copiedGem.IdByte,
            OriginalSourceByte36 = copiedGem.IdByte,
            SourceByte37 = donor.SourceByte37,
            OriginalSourceByte37 = donor.SourceByte37,
            SourceByte4F = copiedGem.ValueByte,
            OriginalSourceByte4F = copiedGem.ValueByte,
            Flag4A = donor.Flag4A,
            OriginalFlag4A = donor.Flag4A,
            Flag4B = donor.Flag4B,
            OriginalFlag4B = donor.Flag4B,
            Color = copiedGem.Color,
            Label = $"Smoke all-level {copiedGem.Name}",
            OriginalLabel = $"Smoke all-level {copiedGem.Name}",
            PatchStatus = "smoke-all-level-placement-sector",
            PatchLead = $"Pasted from copied {donor.DisplayLabel}; verifies loose gem render sector export.",
            IsAdded = true
        };

        string path = Path.Combine(workspace.RootPath, "_local", "smoke", "all-level-gem-sector", $"{level.Key}-native-edits.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? workspace.RootPath);
        await MobyEditStore.SaveAsync(path, [addedGem], $"{level.DisplayName} all-level loose gem sector");
        string outputPrefix = Path.Combine(workspace.RootPath, "_local", "smoke", "all-level-gem-sector", $"{level.Key}-sector-gem");
        MobySourcePatchPlan plan = MobySourcePatchExporter.BuildPlan(
            sourceImage,
            DiscImageLocator.FindCueForImage(sourceImage),
            $"{outputPrefix}.bin",
            $"{outputPrefix}.cue",
            level,
            path);
        MobySourcePatch appendPatch = plan.Patches
            .Where(patch => string.Equals(patch.Kind, "moby-record-append", StringComparison.OrdinalIgnoreCase))
            .OrderBy(patch => patch.TrueIndex)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"{level.DisplayName} all-level loose gem sector check did not append a gem.");
        byte[] after = ParseHexPreview(appendPatch.AfterHexPreview);
        if (appendPatch.TrueIndex != level.SourceRecordCount)
            throw new InvalidOperationException($"{level.DisplayName} all-level loose gem sector appended T{appendPatch.TrueIndex}, expected T{level.SourceRecordCount}.");
        if (after.Length <= 0x4A || after[0x4A] != expectedSector)
            throw new InvalidOperationException($"{level.DisplayName} all-level loose gem sector wrote byte 0x{(after.Length <= 0x4A ? -1 : after[0x4A]):X2}, expected 0x{expectedSector:X2}.");
        if (!appendPatch.Description.Contains($"Placement sector byte set to 0x{expectedSector:X2}", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{level.DisplayName} all-level loose gem sector did not report sector 0x{expectedSector:X2}: {appendPatch.Description}");

        tested++;
    }

    Console.WriteLine($"All-level loose gem placement sectors: {tested} level(s) verified, {skipped} skipped without loose-gem/source geometry coverage");
}

bool TryFindPlacementSector(GeometryCandidate geometry, Vector3f position, out int sectorIndex)
{
    sectorIndex = -1;
    SmokeTerrainSectorCandidate? best = null;
    foreach (TerrainPolygon polygon in geometry.Polygons)
    {
        if (polygon.IsTerrainRemoved || polygon.SectorIndex is < 0 or > 255)
            continue;
        if (!polygon.TryGetZ(position.X, position.Y, out float terrainZ))
            continue;

        SmokeTerrainSectorCandidate candidate = new(polygon.SectorIndex, terrainZ, Math.Abs(terrainZ - position.Z));
        if (best == null ||
            candidate.DistanceToReference < best.Value.DistanceToReference - 0.001f ||
            Math.Abs(candidate.DistanceToReference - best.Value.DistanceToReference) <= 0.001f && candidate.Z > best.Value.Z)
        {
            best = candidate;
        }
    }

    if (best == null)
        return false;

    sectorIndex = best.Value.SectorIndex;
    return true;
}

void ReportTerrainColorFidelity()
{
    string cacheDir = Path.Combine(workspace.RootPath, "editor-cache");
    if (!Directory.Exists(cacheDir))
    {
        Console.WriteLine("Terrain color fidelity: editor cache not present; skipping.");
        return;
    }

    int checkedLevels = 0;
    int checkedFaces = 0;
    int lowestUniqueColorCount = int.MaxValue;
    string lowestUniqueColorLevel = "";
    foreach (string path in Directory.EnumerateFiles(cacheDir, "*-runtime-scene-editor-overlay.json").OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
    {
        string fileName = Path.GetFileName(path);
        string levelKey = fileName.Replace("-runtime-scene-editor-overlay.json", "", StringComparison.OrdinalIgnoreCase);
        GeometryCacheHealthIssue? healthIssue = GeometryCacheHealth.InspectOverlay(levelKey, path);
        if (healthIssue?.BlocksLoading == true)
            continue;

        GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(path);
        TerrainMaterialClassifier.Apply(levelKey, workspace.RootPath, geometry);
        int mismatchedColors = geometry.Polygons.Count(face => face.SurfaceColor != face.FaceColor);
        if (mismatchedColors != 0)
            throw new InvalidOperationException($"{levelKey} terrain preview replaced {mismatchedColors} captured face color(s) with generic material colors.");

        int uniqueColors = geometry.Polygons.Select(face => face.SurfaceColor).Distinct().Count();
        if (geometry.Polygons.Count >= 1000 && uniqueColors < 256)
            throw new InvalidOperationException($"{levelKey} terrain preview only has {uniqueColors} unique colors for {geometry.Polygons.Count} faces; this looks like a generic material fallback.");

        checkedLevels++;
        checkedFaces += geometry.Polygons.Count;
        if (uniqueColors < lowestUniqueColorCount)
        {
            lowestUniqueColorCount = uniqueColors;
            lowestUniqueColorLevel = levelKey;
        }
    }

    Console.WriteLine($"Terrain color fidelity: captured face colors preserved for {checkedLevels} level(s), {checkedFaces} face(s); lowest variety {lowestUniqueColorLevel}={lowestUniqueColorCount} color(s)");
}

async Task ReportTerrainMaterialBehaviorAudit()
{
    string cacheDir = Path.Combine(workspace.RootPath, "editor-cache");
    if (!Directory.Exists(cacheDir))
    {
        Console.WriteLine("Terrain audit: editor cache not present; skipping.");
        return;
    }

    List<TerrainLevelAudit> levels = new();
    List<GeometryCacheHealthIssue> skipped = new();
    foreach (string path in Directory.EnumerateFiles(cacheDir, "*-runtime-scene-editor-overlay.json").OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
    {
        string fileName = Path.GetFileName(path);
        string levelKey = fileName.Replace("-runtime-scene-editor-overlay.json", "", StringComparison.OrdinalIgnoreCase);
        GeometryCacheHealthIssue? healthIssue = GeometryCacheHealth.InspectOverlay(levelKey, path);
        if (healthIssue?.BlocksLoading == true)
        {
            skipped.Add(healthIssue);
            continue;
        }

        GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(path);
        TerrainMaterialClassifier.Apply(levelKey, workspace.RootPath, geometry);
        LevelDefinition? level = catalog.FindByKey(levelKey);
        List<TerrainTextureAudit> textures = geometry.Polygons
            .GroupBy(polygon => polygon.TextureId)
            .Select(group => BuildTerrainTextureAudit(group))
            .OrderByDescending(group => group.FaceCount)
            .ThenBy(group => group.TextureId)
            .ToList();

        IReadOnlyDictionary<string, int> surfaces = TerrainMaterialClassifier.CountSurfaces(geometry);
        IReadOnlyDictionary<string, int> behaviors = TerrainBehaviorClassifier.CountBehaviors(geometry);
        levels.Add(new TerrainLevelAudit
        {
            LevelKey = levelKey,
            DisplayName = level?.DisplayName ?? levelKey,
            FaceCount = geometry.Polygons.Count,
            TextureCount = textures.Count,
            SurfaceCounts = surfaces,
            BehaviorCounts = behaviors,
            HazardCandidateFaces = behaviors.TryGetValue("hazard-candidate", out int hazards) ? hazards : 0,
            TextureGroups = textures
        });
    }

    if (levels.Count == 0)
    {
        Console.WriteLine("Terrain audit: no runtime scene overlays found.");
        return;
    }

    List<TerrainLevelAudit> unknownSurfaceLevels = levels
        .Where(level => level.SurfaceCounts.ContainsKey("unknown"))
        .ToList();
    if (unknownSurfaceLevels.Count > 0)
    {
        string summary = string.Join("; ", unknownSurfaceLevels.Select(level => $"{level.DisplayName}={level.SurfaceCounts["unknown"]}"));
        throw new InvalidOperationException($"Terrain audit found unknown surface types: {summary}");
    }

    string outDir = Path.Combine(workspace.RootPath, "_local", "smoke");
    Directory.CreateDirectory(outDir);
    string jsonPath = Path.Combine(outDir, "terrain-material-behavior-audit.json");
    string markdownPath = Path.Combine(outDir, "terrain-material-behavior-audit.md");
    var report = new
    {
        generatedAt = DateTimeOffset.Now.ToString("s", CultureInfo.InvariantCulture),
        note = "Visual terrain materials and behavior candidates from portable runtime scene overlays. Hazard labels are candidates until the collision response/surface flag is decoded.",
        skipped,
        levels
    };
    JsonSerializerOptions options = new() { WriteIndented = true };
    await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, options));
    await File.WriteAllTextAsync(markdownPath, BuildTerrainAuditMarkdown(levels, skipped));
    string skippedSummary = skipped.Count == 0 ? "" : $", skipped {skipped.Count} unhealthy overlay(s)";
    Console.WriteLine($"Terrain audit: {levels.Count} levels, {levels.Sum(level => level.FaceCount)} faces, {levels.Sum(level => level.HazardCandidateFaces)} hazard candidate face(s){skippedSummary}");
    Console.WriteLine($"Terrain audit report: {markdownPath}");
}

async Task ReportGeometryCacheHealth()
{
    string cacheDir = Path.Combine(workspace.RootPath, "editor-cache");
    if (!Directory.Exists(cacheDir))
    {
        Console.WriteLine("Geometry cache health: editor-cache not found; skipping.");
        return;
    }

    List<GeometryCacheHealthIssue> issues = new();
    foreach (string path in Directory.EnumerateFiles(cacheDir, "*-runtime-scene-editor-overlay.json").OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
    {
        string fileName = Path.GetFileName(path);
        string levelKey = fileName.Replace("-runtime-scene-editor-overlay.json", "", StringComparison.OrdinalIgnoreCase);
        GeometryCacheHealthIssue? issue = GeometryCacheHealth.InspectOverlay(levelKey, path);
        if (issue != null)
            issues.Add(issue);
    }

    string outDir = Path.Combine(workspace.RootPath, "_local", "smoke");
    Directory.CreateDirectory(outDir);
    string jsonPath = Path.Combine(outDir, "geometry-cache-health.json");
    string markdownPath = Path.Combine(outDir, "geometry-cache-health.md");
    JsonSerializerOptions options = new() { WriteIndented = true };
    await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(new
    {
        generatedAt = DateTimeOffset.Now.ToString("s", CultureInfo.InvariantCulture),
        issues
    }, options));

    StringBuilder builder = new();
    builder.AppendLine("# Geometry Cache Health");
    builder.AppendLine();
    if (issues.Count == 0)
    {
        builder.AppendLine("No cache health issues detected.");
    }
    else
    {
        builder.AppendLine("| Level | Severity | Blocks loading | Message |");
        builder.AppendLine("|---|---|---|---|");
        foreach (GeometryCacheHealthIssue issue in issues)
            builder.AppendLine($"| {issue.LevelKey} | {issue.Severity} | {issue.BlocksLoading} | {issue.Message} |");
    }

    await File.WriteAllTextAsync(markdownPath, builder.ToString());
    Console.WriteLine(issues.Count == 0
        ? "Geometry cache health: no issues detected."
        : $"Geometry cache health: {issues.Count} issue(s), report {markdownPath}");
}

async Task ReportSourceSceneOverlayRecovery(bool repairCache)
{
    if (!File.Exists(sourceImage) || !File.Exists(wadAnalysis))
    {
        Console.WriteLine("Source scene recovery: source disc or WAD analysis not found; skipping.");
        return;
    }

    LevelDefinition? gnastyLoot = catalog.FindByKey("gnastysloot");
    if (gnastyLoot == null)
    {
        Console.WriteLine("Source scene recovery: Gnasty's Loot is missing from the level catalog.");
        return;
    }

    string outDir = Path.Combine(workspace.RootPath, "_local", "smoke");
    Directory.CreateDirectory(outDir);
    string stoneHillSourcePath = Path.Combine(outDir, "stonehill-source-scene-overlay.json");
    SourceSceneOverlayResult stoneHillResult = await SourceSceneOverlayExporter.ExportAsync(sourceImage, wadAnalysis, stoneHill, stoneHillSourcePath);
    GeometryCandidate stoneHillSourceGeometry = GeometryOverlayLoader.LoadFirstCandidate(stoneHillSourcePath);
    if (stoneHillSourceGeometry.Polygons.Count != 6659)
        throw new InvalidOperationException($"Stone Hill source recovery produced {stoneHillSourceGeometry.Polygons.Count} faces, expected 6659.");

    string gnastyOutput = repairCache
        ? Path.Combine(workspace.RootPath, "editor-cache", "gnastysloot-runtime-scene-editor-overlay.json")
        : Path.Combine(outDir, "gnastysloot-source-scene-overlay.json");
    SourceSceneOverlayResult gnastyResult = await SourceSceneOverlayExporter.ExportAsync(sourceImage, wadAnalysis, gnastyLoot, gnastyOutput);
    GeometryCacheHealthIssue? healthIssue = GeometryCacheHealth.InspectOverlay("gnastysloot", gnastyOutput);
    if (healthIssue?.BlocksLoading == true)
        throw new InvalidOperationException("Gnasty's Loot source recovery still matches the bad homeworld capture.");

    GeometryCandidate gnastyGeometry = GeometryOverlayLoader.LoadFirstCandidate(gnastyOutput);
    TerrainMaterialClassifier.Apply("gnastysloot", workspace.RootPath, gnastyGeometry);
    IReadOnlyDictionary<string, int> surfaces = TerrainMaterialClassifier.CountSurfaces(gnastyGeometry);
    Console.WriteLine(
        $"Source scene recovery: Stone Hill {stoneHillResult.SectorCount} sectors/{stoneHillSourceGeometry.Polygons.Count} faces; " +
        $"Gnasty's Loot {gnastyResult.SectorCount} sectors/{gnastyGeometry.Polygons.Count} faces" +
        (repairCache ? ", cache repaired" : ", cache preview only"));
    Console.WriteLine("Gnasty's Loot source surfaces: " + string.Join(", ", surfaces.Take(5).Select(pair => $"{pair.Key}={pair.Value}")));
    await ReportSourceDerivedTerrainPatchPlan(gnastyLoot, gnastyGeometry, outDir);
}

async Task ReportSourceDerivedTerrainPatchPlan(LevelDefinition level, GeometryCandidate geometry, string outDir)
{
    if (!File.Exists(sourceImage))
    {
        Console.WriteLine($"{level.DisplayName} source-derived terrain patch: source disc not found; skipping.");
        return;
    }

    string sourceSearchPath = Path.Combine(outDir, $"{level.Key}-source-derived-terrain-source-search-native.json");
    TerrainSourceSearchResult sourceSearch = await TerrainSourceSearchBuilder.BuildSourceDerivedAsync(new SourceDerivedTerrainSourceSearchRequest(
        SourceImagePath: sourceImage,
        OutputPath: sourceSearchPath,
        Level: level,
        Geometry: geometry));
    HashSet<string> sourceMappedKeys = ReadTerrainSourceKeys(sourceSearchPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
    TerrainPolygon[] sourceMappedCandidates = geometry.Polygons
        .Where(face =>
            face.SectorOffset >= 0 &&
            face.VertexIndexes.Count > 0 &&
            sourceMappedKeys.Contains(face.RuntimeKey))
        .ToArray();
    SourceDerivedCollisionProbeReport? collisionProbe = await ReportSourceDerivedCollisionProbe(level, geometry, outDir, sourceSearchPath, sourceSearch);
    HashSet<string> sourceDerivedCollisionKeys = (collisionProbe?.HitRuntimeKeys ?? Array.Empty<string>())
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    TerrainPolygon[] candidates = sourceMappedCandidates
        .OrderByDescending(face => sourceDerivedCollisionKeys.Contains(face.RuntimeKey))
        .ThenBy(face => face.SectorIndex)
        .ThenBy(face => face.FaceIndex)
        .Take(48)
        .ToArray();
    if (candidates.Length == 0)
    {
        Console.WriteLine($"{level.DisplayName} source-derived terrain patch: no source-mapped face found; skipping.");
        return;
    }

    string editsPath = Path.Combine(outDir, $"{level.Key}-source-derived-terrain-edits.json");
    float[] deltas = [4f, -4f, 8f, -8f];
    string lastFailure = "";
    bool movementProved = false;
    foreach (TerrainPolygon face in candidates)
    {
        foreach (float delta in deltas)
        {
            try
            {
                face.ApplyTerrainDeltaZ(delta);
                int editCount = await TerrainEditStore.SaveAsync(editsPath, [face], $"{level.DisplayName} source-derived terrain smoke");
                face.ResetTerrainEdit();
                TerrainPatchPlan plan = TerrainPatchExporter.BuildPlan(
                    sourceImage,
                    DiscImageLocator.FindCueForImage(sourceImage),
                    Path.Combine(outDir, $"{level.Key}-source-derived-terrain-smoke.bin"),
                    Path.Combine(outDir, $"{level.Key}-source-derived-terrain-smoke.cue"),
                    level,
                    "",
                    sourceSearchPath,
                    editsPath,
                    "");
                bool hasVisualPatch = plan.Patches.Any(patch =>
                    patch.RuntimeKey.Equals(face.RuntimeKey, StringComparison.OrdinalIgnoreCase) &&
                    patch.Kind.StartsWith("visual-", StringComparison.OrdinalIgnoreCase));
                bool hasCollisionPatch = plan.Patches.Any(patch => patch.Kind.StartsWith("collision-", StringComparison.OrdinalIgnoreCase));
                bool hasSourceDerivedNote = plan.Notes.Any(note => note.Contains("source-derived", StringComparison.OrdinalIgnoreCase));
                if (editCount == 1 && hasVisualPatch && hasCollisionPatch && hasSourceDerivedNote)
                {
                    Console.WriteLine($"{level.DisplayName} source-derived terrain patch: face {face.RuntimeKey}, dz={delta:+0;-0;0}, visual=True, collision=True, patches={plan.PatchCount}, sectors={sourceSearch.Report.MatchedSectorCount}/{sourceSearch.Report.SectorCount}");
                    movementProved = true;
                    break;
                }

                lastFailure = $"face={face.RuntimeKey}, dz={delta}, edits={editCount}, visual={hasVisualPatch}, collision={hasCollisionPatch}, sourceNote={hasSourceDerivedNote}, patches={plan.PatchCount}";
            }
            catch (Exception ex)
            {
                face.ResetTerrainEdit();
                lastFailure = $"face={face.RuntimeKey}, dz={delta}, {ex.Message}";
            }
        }

        if (movementProved)
            break;
    }

    if (!movementProved)
        throw new InvalidOperationException($"{level.DisplayName} source-derived terrain patch did not produce a visual+collision terrain patch. Last failure: {lastFailure}");

    Dictionary<int, int> sectorAppendSlack = BuildTerrainSectorAppendSlack(sourceSearch.Report);
    TerrainPolygon[] addCandidates = sourceMappedCandidates
        .Where(face =>
            string.Equals(face.Detail, "hp", StringComparison.OrdinalIgnoreCase) &&
            face.OriginalPoints.Count >= 3 &&
            face.VertexIndexes.Count <= face.OriginalPoints.Count &&
            sectorAppendSlack.ContainsKey(face.SectorOffset))
        .OrderByDescending(face => sourceDerivedCollisionKeys.Contains(face.RuntimeKey))
        .ThenByDescending(face => sectorAppendSlack[face.SectorOffset] >= RequiredIndependentAddCopySlack(face))
        .ThenByDescending(face => sectorAppendSlack[face.SectorOffset])
        .ThenBy(face => face.SectorIndex)
        .ThenBy(face => face.FaceIndex)
        .Take(32)
        .ToArray();
    if (addCandidates.Length == 0)
    {
        Console.WriteLine($"{level.DisplayName} source-derived add-copy terrain patch: no source-mapped high-detail sector was available for independent visible vertices.");
        return;
    }

    string addPath = Path.Combine(outDir, $"{level.Key}-source-derived-add-copy-terrain-edits.json");
    string addFailure = "";
    foreach (TerrainPolygon addFace in addCandidates)
    {
        float[] addCopyDeltas = addFace.OriginalZValues.Select((_, index) => 10f + (index * 3f)).ToArray();
        Vector2f[] addCopyXYDeltas = addFace.OriginalPoints.Select(_ => new Vector2f(64f, 24f)).ToArray();
        addFace.ApplyTerrainVertexDeltas(addCopyDeltas);
        addFace.ApplyTerrainVertexXYDeltas(addCopyXYDeltas);
        addFace.StageTerrainAddClone();
        int addEditCount = await TerrainEditStore.SaveAsync(addPath, [addFace], $"{level.DisplayName} source-derived add-copy terrain smoke");
        addFace.ResetTerrainEdit();
        TerrainPatchPlan addPlan = TerrainPatchExporter.BuildPlan(
            sourceImage,
            DiscImageLocator.FindCueForImage(sourceImage),
            Path.Combine(outDir, $"{level.Key}-source-derived-add-copy-terrain-smoke.bin"),
            Path.Combine(outDir, $"{level.Key}-source-derived-add-copy-terrain-smoke.cue"),
            level,
            "",
            sourceSearchPath,
            addPath,
            "");
        bool hasVertexCountPatch = addPlan.Patches.Any(patch => string.Equals(patch.Kind, "terrain-vertex-count-hp", StringComparison.OrdinalIgnoreCase));
        bool hasFaceCountPatch = addPlan.Patches.Any(patch => string.Equals(patch.Kind, "terrain-face-count-hp", StringComparison.OrdinalIgnoreCase));
        bool hasRepackPatch = addPlan.Patches.Any(patch => string.Equals(patch.Kind, "terrain-sector-repack-hp-add-copy", StringComparison.OrdinalIgnoreCase));
        bool hasCollisionAdd = addPlan.Patches.Any(patch => string.Equals(patch.Kind, "collision-triangle-add-copy", StringComparison.OrdinalIgnoreCase));
        bool hasLookupAdd = addPlan.Patches.Any(patch => string.Equals(patch.Kind, "collision-lookup-add-copy", StringComparison.OrdinalIgnoreCase));
        if (addEditCount == 1 && hasVertexCountPatch && hasFaceCountPatch && hasRepackPatch && hasCollisionAdd)
        {
            int addSlack = sectorAppendSlack[addFace.SectorOffset];
            int addRequired = RequiredIndependentAddCopySlack(addFace);
            Console.WriteLine($"{level.DisplayName} source-derived add-copy terrain patch: face {addFace.RuntimeKey}, slack={addSlack}/{addRequired}, independentVisual=True, collision=True, lookup={hasLookupAdd}, patches={addPlan.PatchCount}");
            return;
        }

        addFailure = $"face={addFace.RuntimeKey}, edits={addEditCount}, vertexCount={hasVertexCountPatch}, faceCount={hasFaceCountPatch}, repack={hasRepackPatch}, collision={hasCollisionAdd}, patches={addPlan.PatchCount}, skipped={string.Join(" | ", addPlan.SkippedEdits)}";
    }

    throw new InvalidOperationException($"{level.DisplayName} source-derived add-copy terrain patch failed to find a collision-backed candidate. Last failure: {addFailure}");
}

async Task<SourceDerivedCollisionProbeReport?> ReportSourceDerivedCollisionProbe(
    LevelDefinition level,
    GeometryCandidate geometry,
    string outDir,
    string sourceSearchPath,
    TerrainSourceSearchResult sourceSearch)
{
    if (!File.Exists(sourceImage))
    {
        Console.WriteLine($"{level.DisplayName} source-derived collision probe: source disc not found; skipping.");
        return null;
    }

    HashSet<string> sourceMappedKeys = ReadTerrainSourceKeys(sourceSearchPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
    TerrainPolygon[] candidates = geometry.Polygons
        .Where(face =>
            string.Equals(face.Detail, "hp", StringComparison.OrdinalIgnoreCase) &&
            face.Points.Count >= 3 &&
            face.ZValues.Length >= face.Points.Count &&
            sourceMappedKeys.Contains(face.RuntimeKey))
        .ToArray();

    Dictionary<CollisionWordTriple, SourceDerivedCollisionPattern> patterns = new();
    int encodedTriangleVariants = 0;
    foreach (TerrainPolygon polygon in candidates)
    {
        for (int i = 1; i < polygon.Points.Count - 1; i++)
        {
            foreach ((CollisionWordTriple triple, string pointKey) in EnumerateSourceDerivedCollisionPatterns(polygon, 0, i, i + 1))
            {
                encodedTriangleVariants++;
                if (patterns.TryGetValue(triple, out SourceDerivedCollisionPattern? existing))
                {
                    existing.DuplicateCount++;
                    continue;
                }

                patterns[triple] = new SourceDerivedCollisionPattern(polygon.RuntimeKey, i - 1, pointKey);
            }
        }
    }

    if (patterns.Count == 0)
    {
        Console.WriteLine($"{level.DisplayName} source-derived collision probe: no encodable source-derived visual triangles.");
        return null;
    }

    byte[] wad = ReadLogicalWadForSmoke(sourceImage);
    long scanStart = 0;
    long scanEnd = wad.Length;
    string scanScope = "full logical WAD";
    if (TryGetSmokeArchiveEntry(wadAnalysis, level, out SmokeArchiveEntry? archiveEntry) && archiveEntry != null)
    {
        scanStart = Math.Clamp(archiveEntry.Offset, 0, wad.Length);
        scanEnd = Math.Clamp(archiveEntry.Offset + archiveEntry.Size, scanStart, wad.Length);
        scanScope = $"WAD entry {archiveEntry.Index} 0x{scanStart:X}-0x{scanEnd:X}";
    }

    HashSet<CollisionWordTriple> hitPatterns = new();
    HashSet<int> hitOffsets = new();
    Dictionary<string, int> hitRuntimeKeys = new(StringComparer.OrdinalIgnoreCase);
    List<SourceDerivedCollisionHit> hitSamples = new();
    int totalHits = 0;
    for (int offset = (int)scanStart; offset + 12 <= scanEnd; offset += 4)
    {
        CollisionWordTriple triple = new(
            BinaryPrimitives.ReadUInt32LittleEndian(wad.AsSpan(offset, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(wad.AsSpan(offset + 4, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(wad.AsSpan(offset + 8, 4)));
        if (!patterns.TryGetValue(triple, out SourceDerivedCollisionPattern? pattern))
            continue;

        totalHits++;
        hitPatterns.Add(triple);
        hitOffsets.Add(offset);
        hitRuntimeKeys[pattern.RuntimeKey] = hitRuntimeKeys.TryGetValue(pattern.RuntimeKey, out int count) ? count + 1 : 1;
        if (hitSamples.Count < 40)
        {
            hitSamples.Add(new SourceDerivedCollisionHit(
                WadOffset: offset,
                RuntimeKey: pattern.RuntimeKey,
                TriangleIndex: pattern.TriangleIndex,
                PointKey: pattern.PointKey,
                DuplicatePatternCount: pattern.DuplicateCount));
        }
    }

    SourceDerivedCollisionBounds bounds = BuildSourceDerivedCollisionBounds(candidates);
    IReadOnlyList<SourceDerivedCollisionTableCandidate> tableCandidates = totalHits > 0
        ? BuildSourceDerivedCollisionTableCandidates(wad, (int)scanStart, (int)scanEnd, hitOffsets, bounds)
        : [];
    string[] lookupReferencedRuntimeKeys = BuildSourceDerivedLookupReferencedRuntimeKeys(wad, tableCandidates, patterns);
    string conclusion = totalHits > 0 && tableCandidates.Count > 0
        ? "Exact collision-shaped records were found for source-derived visual triangles, and plausible table spans were found around those hits. The best span/lookup candidates are now reported so exporter-side source collision index support can be targeted instead of guessed."
        : totalHits > 0
        ? "Exact collision-shaped records were found for source-derived visual triangles, but no strong contiguous table span was found yet. The offset samples still prove top-face collision patching, while side-wall/add-copy collision needs more table-boundary decoding."
        : "No exact collision-shaped records were found inside the level asset. Source-derived collision probably needs a table locator or another coordinate transform instead of a direct visual-triangle scan.";
    SourceDerivedCollisionProbeReport report = new()
    {
        LevelKey = level.Key,
        LevelName = level.DisplayName,
        ScanScope = scanScope,
        SourceSectors = sourceSearch.Report.SectorCount,
        MatchedSourceSectors = sourceSearch.Report.MatchedSectorCount,
        SourceMappedFaces = candidates.Length,
        EncodedTriangleVariants = encodedTriangleVariants,
        UniquePatternCount = patterns.Count,
        TotalHits = totalHits,
        UniquePatternHits = hitPatterns.Count,
        MatchedRuntimeKeys = hitRuntimeKeys.Count,
        LookupReferencedRuntimeKeys = lookupReferencedRuntimeKeys,
        Bounds = bounds,
        TableCandidates = tableCandidates,
        HitRuntimeKeys = hitRuntimeKeys.Keys
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray(),
        Conclusion = conclusion,
        Hits = hitSamples
    };

    string jsonPath = Path.Combine(outDir, $"{level.Key}-source-derived-collision-probe.json");
    string markdownPath = Path.Combine(outDir, $"{level.Key}-source-derived-collision-probe.md");
    await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    await File.WriteAllTextAsync(markdownPath, BuildSourceDerivedCollisionProbeMarkdown(report));
    string bestTable = tableCandidates.Count > 0
        ? $", bestTable=0x{tableCandidates[0].StartWadOffset:X}-0x{tableCandidates[0].EndWadOffset:X} hits={tableCandidates[0].HitCount}/{tableCandidates[0].TriangleCount} lookupWords={tableCandidates[0].LookupWordCount}"
        : "";
    Console.WriteLine($"{level.DisplayName} source-derived collision probe: hits={totalHits}, unique={hitPatterns.Count}/{patterns.Count}, faces={candidates.Length}{bestTable}, report={markdownPath}");
    return report;
}

async Task ReportSourceMobyRecovery(bool repairCache)
{
    if (!File.Exists(sourceImage))
    {
        Console.WriteLine("Source moby recovery: source disc not found; skipping.");
        return;
    }

    LevelDefinition? gnastyLoot = catalog.FindByKey("gnastysloot");
    if (gnastyLoot == null)
    {
        Console.WriteLine("Source moby recovery: Gnasty's Loot is missing from the level catalog.");
        return;
    }

    string outDir = Path.Combine(workspace.RootPath, "_local", "smoke");
    Directory.CreateDirectory(outDir);
    string outputPath = repairCache
        ? Path.Combine(workspace.RootPath, "editor-cache", "gnastysloot-mobys.json")
        : Path.Combine(outDir, "gnastysloot-source-mobys.json");

    SourceMobyCacheResult result = await SourceMobyCacheBuilder.BuildAsync(sourceImage, gnastyLoot, outputPath);
    if (result.MobyCount != gnastyLoot.SourceRecordCount)
        throw new InvalidOperationException($"Gnasty's Loot source moby recovery produced {result.MobyCount} records, expected {gnastyLoot.SourceRecordCount}.");
    if (result.TreasureValue != 2000)
        throw new InvalidOperationException($"Gnasty's Loot source moby recovery found {result.TreasureValue} treasure value, expected 2000.");

    List<Moby> mobys = MobyLoader.LoadCached(outputPath).ToList();
    MobyMetadataEnricher.Apply(workspace, "gnastysloot", mobys);
    if (!mobys.Any(moby => moby.DisplayLabel.Contains("thief", StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException("Gnasty's Loot source moby recovery did not promote any thief records.");

    Console.WriteLine(
        $"Source moby recovery: Gnasty's Loot {result.MobyCount} source mobys, treasure value {result.TreasureValue}" +
        (repairCache ? ", cache repaired" : ", cache preview only"));
    ReportIdentityCoverage("gnastysloot source", mobys);
}

void ReportBuiltInTerrainMaterialOverrides()
{
    List<(string LevelKey, int[] TextureIds, string Surface)> checks =
    [
        ("stonehill", [30], "sand"),
        ("stonehill", [32], "water"),
        ("artisans", [39, 64], "grass"),
        ("peacekeepers", [0, 1, 2], "water"),
        ("peacekeepers", [30, 31, 32, 33, 34, 35, 36], "cliff"),
        ("beastmakers", [41], "ooze"),
        ("icecavern", [3, 4, 11, 12, 14, 15, 16, 17, 18, 21, 23, 26, 32, 33, 34, 35, 36, 39, 40, 41, 42, 47, 50, 51, 52, 53, 54], "ice"),
        ("clifftown", [35, 36, 38], "lava"),
        ("jacques", [52, 53], "lava"),
        ("toasty", [50, 51, 52], "lava"),
        ("gnastysloot", [64, 14, 47, 3, 11], "water"),
        ("gnastysloot", [24], "lava"),
        ("gnastysloot", [68, 72, 127, 28, 27, 10, 61, 8, 26, 25], "grass"),
        ("gnastysloot", [38, 53, 45, 46, 41, 42, 43, 44, 39, 40, 30, 32, 33, 29, 34, 35, 36, 48, 50], "brick")
    ];

    List<string> summaries = new();
    foreach ((string levelKey, int[] textureIds, string surface) in checks)
    {
        string overlayPath = Path.Combine(workspace.RootPath, "editor-cache", $"{levelKey}-runtime-scene-editor-overlay.json");
        if (!File.Exists(overlayPath))
            continue;

        GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
        TerrainMaterialClassifier.Apply(levelKey, workspace.RootPath, geometry);
        foreach (int textureId in textureIds)
        {
            List<TerrainPolygon> faces = geometry.Polygons.Where(face => face.TextureId == textureId).ToList();
            if (faces.Count == 0)
                throw new InvalidOperationException($"{levelKey} terrain texture {textureId} was not present for built-in material override smoke.");

            bool allMatch = faces.All(face =>
                string.Equals(TerrainMaterialClassifier.NormalizeSurfaceName(face.Surface), surface, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(face.SurfaceSource, "override", StringComparison.OrdinalIgnoreCase));
            if (!allMatch)
                throw new InvalidOperationException($"{levelKey} terrain texture {textureId} was not classified as built-in {surface} override.");
        }

        summaries.Add($"{levelKey}:{surface} textures {string.Join(",", textureIds)}");
    }

    Console.WriteLine($"Built-in terrain material overrides: {string.Join("; ", summaries)}");
}

void ReportTerrainPalettePresetCoverage()
{
    string[] matchableSurfaces =
    [
        "grass",
        "water",
        "sand",
        "stone",
        "ground",
        "cliff",
        "brick",
        "ice",
        "lava",
        "ooze"
    ];

    List<string> failures = [];
    HashSet<string> presetIds = new(StringComparer.OrdinalIgnoreCase);
    foreach (TerrainPalettePreset preset in TerrainPalettePresetCatalog.Presets)
    {
        if (!presetIds.Add(preset.Id))
            failures.Add($"duplicate preset id {preset.Id}");

        string normalizedSurface = TerrainMaterialClassifier.NormalizeSurfaceName(preset.Surface);
        if (!matchableSurfaces.Contains(normalizedSurface, StringComparer.OrdinalIgnoreCase))
            failures.Add($"{preset.DisplayName} uses unsupported surface {preset.Surface}");
    }

    foreach (string surface in matchableSurfaces)
    {
        TerrainPalettePreset preset = TerrainPalettePresetCatalog.DefaultForSurface(surface);
        string presetSurface = TerrainMaterialClassifier.NormalizeSurfaceName(preset.Surface);
        if (!string.Equals(presetSurface, surface, StringComparison.OrdinalIgnoreCase))
            failures.Add($"{surface} resolves to {preset.DisplayName}/{preset.Surface}");
    }

    if (failures.Count > 0)
        throw new InvalidOperationException($"Terrain palette preset coverage failed: {string.Join("; ", failures)}");

    Console.WriteLine($"Terrain palette preset coverage: {matchableSurfaces.Length} matchable surface(s), {TerrainPalettePresetCatalog.Presets.Count} preset(s), all resolve directly");
}

void ReportTerrainTextureAssetRouting()
{
    List<string> summaries = new();
    foreach (string levelKey in new[] { "stonehill", "darkhollow", "clifftown", "toasty" })
    {
        LevelDefinition level = catalog.FindByKey(levelKey)
            ?? throw new InvalidOperationException($"{levelKey} is missing from the level catalog.");
        int textureAssetWadIndex = TerrainPatchExporter.TextureAssetWadIndexForLevel(level);
        if (textureAssetWadIndex != level.SourceWadEntry)
            throw new InvalidOperationException($"{levelKey} texture asset routing did not use the level WAD entry.");

        summaries.Add($"{levelKey}:asset {textureAssetWadIndex}");
    }

    Console.WriteLine($"Terrain texture asset routing: {string.Join("; ", summaries)}");
}

async Task ReportStoneHillCollisionMaterialAudit()
{
    string ramPath = TerrainPatchDataLocator.FindRamDump(workspace, "stonehill");
    string overlay = Path.Combine(workspace.RootPath, "editor-cache", "stonehill-runtime-scene-editor-overlay.json");
    if (!File.Exists(ramPath) || !File.Exists(overlay))
    {
        Console.WriteLine("Stone Hill collision/material audit: RAM dump or geometry overlay not found; skipping.");
        return;
    }

    GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(overlay);
    TerrainMaterialClassifier.Apply("stonehill", workspace.RootPath, geometry);
    SpyroCollisionTable collision = SpyroCollisionDecoder.Decode(await File.ReadAllBytesAsync(ramPath));
    Dictionary<string, List<TerrainPolygon>> faceTriangles = BuildVisualTriangleMap(geometry);

    Dictionary<string, int> collisionFlagCounts = new(StringComparer.OrdinalIgnoreCase);
    Dictionary<string, int> matchedFlagCounts = new(StringComparer.OrdinalIgnoreCase);
    Dictionary<string, int> materialByCollisionFlag = new(StringComparer.OrdinalIgnoreCase);
    Dictionary<string, CollisionMaterialExample> examples = new(StringComparer.OrdinalIgnoreCase);
    int matched = 0;
    foreach (SpyroCollisionTriangle triangle in collision.Triangles)
    {
        string flag = FormatHexValue(triangle.ZFlags, 4);
        Increment(collisionFlagCounts, flag);
        if (!faceTriangles.TryGetValue(CollisionTriangleKey(triangle), out List<TerrainPolygon>? faces))
            continue;

        matched++;
        Increment(matchedFlagCounts, flag);
        foreach (TerrainPolygon face in faces)
        {
            string material = TerrainMaterialClassifier.NormalizeSurfaceName(face.Surface);
            string key = $"{flag}|{material}";
            Increment(materialByCollisionFlag, key);
            examples.TryAdd(key, new CollisionMaterialExample(
                CollisionTriangle: triangle.Index,
                RuntimeKey: face.RuntimeKey,
                TextureId: face.TextureId,
                Surface: material,
                Word3: face.Word3,
                Word4: face.Word4,
                ZRange: $"{face.MinZ:0}..{face.MaxZ:0}"));
        }
    }

    StoneHillCollisionMaterialAudit report = new()
    {
        RamPath = ramPath,
        HeaderOffset = FormatHexValue((uint)collision.HeaderOffset, 0),
        TriangleOffset = FormatHexValue((uint)collision.TriangleOffset, 0),
        TriangleCount = collision.TriangleCount,
        MatchedVisualTriangles = matched,
        CollisionFlagCounts = SortedCounts(collisionFlagCounts),
        MatchedFlagCounts = SortedCounts(matchedFlagCounts),
        MaterialByCollisionFlag = SortedCounts(materialByCollisionFlag),
        Examples = examples
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
        Conclusion = collisionFlagCounts.Count == 1
            ? "All decoded Stone Hill collision triangles share the same preserved packed zFlags value. This means the known CollTri zFlags do not identify water/lava/solid behavior."
            : "Collision zFlags vary; compare material groups before promoting them to behavior labels."
    };

    string outDir = Path.Combine(workspace.RootPath, "_local", "smoke");
    Directory.CreateDirectory(outDir);
    string jsonPath = Path.Combine(outDir, "stonehill-collision-material-audit.json");
    string markdownPath = Path.Combine(outDir, "stonehill-collision-material-audit.md");
    JsonSerializerOptions options = new() { WriteIndented = true };
    await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, options));
    await File.WriteAllTextAsync(markdownPath, BuildCollisionMaterialAuditMarkdown(report));
    Console.WriteLine($"Stone Hill collision/material audit: {collision.TriangleCount} triangles, {matched} matched visual triangle(s), flags {FormatCounts(report.CollisionFlagCounts, 4)}");
    Console.WriteLine($"Stone Hill collision/material report: {markdownPath}");
}

async Task ReportTerrainBehaviorEvidence()
{
    string cacheDir = Path.Combine(workspace.RootPath, "editor-cache");
    if (!Directory.Exists(cacheDir))
    {
        Console.WriteLine("Terrain behavior evidence: editor-cache not found; skipping.");
        return;
    }

    List<TerrainBehaviorLevelEvidence> levels = new();
    List<TerrainControlProximitySample> nearestControls = new();
    foreach (string overlayPath in Directory.EnumerateFiles(cacheDir, "*-runtime-scene-editor-overlay.json").OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
    {
        string fileName = Path.GetFileName(overlayPath);
        string levelKey = fileName.Replace("-runtime-scene-editor-overlay.json", "", StringComparison.OrdinalIgnoreCase);
        if (GeometryCacheHealth.InspectOverlay(levelKey, overlayPath)?.BlocksLoading == true)
            continue;

        GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
        TerrainMaterialClassifier.Apply(levelKey, workspace.RootPath, geometry);
        LevelDefinition? level = catalog.FindByKey(levelKey);

        string mobyPath = Path.Combine(cacheDir, $"{levelKey}-mobys.json");
        List<Moby> mobys = File.Exists(mobyPath)
            ? MobyLoader.LoadCached(mobyPath).ToList()
            : new List<Moby>();
        if (mobys.Count > 0)
            MobyMetadataEnricher.Apply(workspace, levelKey, mobys);

        List<Moby> controls = mobys.Where(IsBehaviorControlMoby).ToList();
        List<TerrainPolygon> hazardFaces = geometry.Polygons.Where(IsHazardMaterial).ToList();
        List<TerrainPolygon> solidFaces = geometry.Polygons.Where(IsLikelySolidMaterial).ToList();
        TerrainFaceControlProximity hazardStats = BuildFaceControlProximity("hazard-candidate", hazardFaces, controls);
        TerrainFaceControlProximity solidStats = BuildFaceControlProximity("solid-candidate", solidFaces, controls);

        levels.Add(new TerrainBehaviorLevelEvidence
        {
            LevelKey = levelKey,
            DisplayName = level?.DisplayName ?? levelKey,
            FaceCount = geometry.Polygons.Count,
            MobyCount = mobys.Count,
            ControlMobyCount = controls.Count,
            HazardCandidateFaces = hazardFaces.Count,
            SolidCandidateFaces = solidFaces.Count,
            SurfaceCounts = TerrainMaterialClassifier.CountSurfaces(geometry),
            HazardControlProximity = hazardStats,
            SolidControlProximity = solidStats
        });

        foreach (Moby control in controls)
        {
            TerrainPolygon? nearestHazard = FindNearestFace(control.Position, hazardFaces, out double hazardDistance);
            if (nearestHazard == null)
                continue;

            TerrainPolygon? nearestSolid = FindNearestFace(control.Position, solidFaces, out double solidDistance);
            nearestControls.Add(new TerrainControlProximitySample
            {
                LevelKey = levelKey,
                DisplayName = level?.DisplayName ?? levelKey,
                ControlId = $"T{control.TrueIndex}",
                Label = control.DisplayLabel,
                CandidateKind = control.CandidateKind,
                TypeHex = $"0x{control.Type:X2}",
                SourceByte36Hex = $"0x{control.SourceByte36:X2}",
                Flag4AHex = $"0x{control.Flag4A:X2}",
                Flag4BHex = $"0x{control.Flag4B:X2}",
                SourceByte4FHex = $"0x{control.SourceByte4F:X2}",
                X = control.Position.X,
                Y = control.Position.Y,
                Z = control.Position.Z,
                NearestHazardDistance = Math.Round(hazardDistance, 2),
                NearestHazardZDelta = Math.Round(control.Position.Z - nearestHazard.AvgZ, 2),
                NearestHazardSurface = TerrainMaterialClassifier.NormalizeSurfaceName(nearestHazard.Surface),
                NearestHazardTextureId = nearestHazard.TextureId,
                NearestHazardRuntimeKey = nearestHazard.RuntimeKey,
                NearestSolidDistance = nearestSolid == null ? null : Math.Round(solidDistance, 2)
            });
        }
    }

    if (levels.Count == 0)
    {
        Console.WriteLine("Terrain behavior evidence: no runtime scene overlays found.");
        return;
    }

    nearestControls = nearestControls
        .OrderBy(sample => sample.NearestHazardDistance)
        .ThenBy(sample => sample.DisplayName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(sample => sample.ControlId, StringComparer.OrdinalIgnoreCase)
        .Take(80)
        .ToList();

    string collisionReportPath = Path.Combine(workspace.RootPath, "_local", "smoke", "stonehill-collision-material-audit.json");
    string collisionConclusion = TryReadCollisionConclusion(collisionReportPath);
    TerrainBehaviorEvidenceReport report = new()
    {
        GeneratedAt = DateTimeOffset.Now.ToString("s", CultureInfo.InvariantCulture),
        CollisionEvidence = collisionConclusion,
        Verdict = "Exact Spyro response behavior is not decoded yet. The current evidence proves visual material groups and proves that the currently decoded Stone Hill packed CollTri zFlags do not distinguish water from normal ground.",
        SurfaceTaxonomy = BuildSurfaceTaxonomy(),
        Levels = levels,
        NearestControlSamples = nearestControls,
        NextProofTests =
        [
            "Capture before/after RAM pairs while Spyro touches known water/lava/ooze and compare player state, health/lives, position reset, and nearby response tables.",
            "Run a texture-only swap test: make a water-looking face use a solid-looking texture without changing collision, then check whether Spyro behavior follows the texture or stays with the original position/table.",
            "Run a collision/response-table search around the decoded scene/collision pointer table, because packed CollTri coordinate zFlags are flat in Stone Hill.",
            "Move or disable nearby nonvisual control markers only in an experimental BIN after a close-control candidate is identified; use that to test whether hazards are control-volume driven."
        ]
    };

    string outDir = Path.Combine(workspace.RootPath, "_local", "smoke");
    Directory.CreateDirectory(outDir);
    string jsonPath = Path.Combine(outDir, "terrain-behavior-evidence.json");
    string markdownPath = Path.Combine(outDir, "terrain-behavior-evidence.md");
    JsonSerializerOptions options = new() { WriteIndented = true };
    await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, options));
    await File.WriteAllTextAsync(markdownPath, BuildTerrainBehaviorEvidenceMarkdown(report));
    int hazardFacesTotal = levels.Sum(level => level.HazardCandidateFaces);
    int closeHazardControls = nearestControls.Count(sample => sample.NearestHazardDistance <= 256);
    Console.WriteLine($"Terrain behavior evidence: {levels.Count} levels, {hazardFacesTotal} hazard candidate face(s), {closeHazardControls} control(s) within 256 units of a hazard candidate");
    Console.WriteLine($"Terrain behavior evidence report: {markdownPath}");
}

async Task ReportTerrainBehaviorRamPairComparerSmoke()
{
    byte[] before = BuildSyntheticTerrainBehaviorRam();
    byte[] after = before.ToArray();
    WriteUInt32(after, 0x78AD0, 0x00000009);
    WriteInt32(after, 0x78AD8, 0);
    WriteInt32(after, 0x78A60, 0x00003100);
    WriteInt32(after, 0x78BB8, 60);
    WriteInt32(after, 0x78BBC, 2);
    after[0xA0000 + (149 * 0x100)] = 0x7E;

    TerrainBehaviorRamPairReport report = TerrainBehaviorRamPairComparer.Compare(
        before,
        after,
        new TerrainBehaviorRamPairOptions(
            "stonehill",
            "synthetic-water-touch",
            [
                new TerrainBehaviorFocusControl(
                    TrueIndex: 149,
                    Label: "synthetic water control",
                    HazardSurface: "water",
                    HazardTextureId: 32,
                    HazardRuntimeKey: "72:2:hp",
                    HazardDistance: 0.18,
                    HazardZDelta: 32.5)
            ]));

    TerrainBehaviorFocusControlDiff focus = report.FocusControls.First();
    if (report.Before.DecodedMobys <= 0 ||
        report.WatchedRanges.First(range => range.Label == "Spyro player state").ChangedByteCount == 0 ||
        !report.SpyroChanges.Any(change => change.Field == "health") ||
        !report.SpyroChanges.Any(change => change.Field == "iFrames") ||
        focus.SpecialDataDiff.ChangedByteCount != 1 ||
        !report.Interpretation.Contains("direct player-response", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Synthetic terrain behavior RAM-pair comparison did not detect the expected response changes.");
    }

    string outDir = Path.Combine(workspace.RootPath, "_local", "smoke");
    Directory.CreateDirectory(outDir);
    string jsonPath = Path.Combine(outDir, "terrain-behavior-ram-pair-smoke.json");
    string markdownPath = Path.Combine(outDir, "terrain-behavior-ram-pair-smoke.md");
    JsonSerializerOptions options = new() { WriteIndented = true };
    await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, options));
    await File.WriteAllTextAsync(markdownPath, BuildTerrainBehaviorRamPairSmokeMarkdown(report));
    Console.WriteLine($"Terrain behavior RAM-pair comparer: synthetic proof passed, interpretation={report.Interpretation}");
}

async Task ReportTerrainBehaviorProofStoreSmoke()
{
    string tempRoot = Path.Combine(workspace.RootPath, "_local", "smoke", "terrain-behavior-proof-store");
    Directory.CreateDirectory(tempRoot);
    string levelKey = "syntheticterrainproof";
    string proofPath = TerrainBehaviorProofStore.ProofPath(tempRoot, levelKey);
    if (File.Exists(proofPath))
        File.Delete(proofPath);

    byte[] before = BuildSyntheticTerrainBehaviorRam();
    byte[] afterDamage = before.ToArray();
    WriteUInt32(afterDamage, 0x78AD0, 0x00000009);
    WriteInt32(afterDamage, 0x78BB8, 60);
    WriteInt32(afterDamage, 0x78BBC, 2);

    TerrainBehaviorRamPairReport damageReport = TerrainBehaviorRamPairComparer.Compare(
        before,
        afterDamage,
        new TerrainBehaviorRamPairOptions(levelKey, "water-touch", []));
    await TerrainBehaviorProofStore.RecordObservationAsync(tempRoot, levelKey, 32, "water", "1:0:hp", damageReport);
    await TerrainBehaviorProofStore.RecordObservationAsync(tempRoot, levelKey, 30, "stone", "1:1:hp", TerrainBehaviorRamPairComparer.Compare(
        before,
        before.ToArray(),
        new TerrainBehaviorRamPairOptions(levelKey, "stone-touch", [])));
    TerrainBehaviorProofFile proofFile = await TerrainBehaviorProofStore.RecordObservationAsync(tempRoot, levelKey, 32, "water", "1:2:hp", damageReport);
    TerrainBehaviorRule waterRule = proofFile.Rules.First(rule => rule.TextureId == 32);
    if (waterRule.Behavior != "damage-proven" || waterRule.Confidence != "proven")
        throw new InvalidOperationException("Terrain behavior proof store did not promote repeated water damage evidence.");

    GeometryCandidate geometry = new();
    geometry.Polygons.Add(new TerrainPolygon(
        [new Vector2f(0, 0), new Vector2f(64, 0), new Vector2f(64, 64), new Vector2f(0, 64)],
        [0, 0, 0, 0],
        32,
        1,
        0,
        "hp",
        ColorRgba.FromRgb(30, 80, 210)));
    TerrainMaterialClassifier.Apply(levelKey, tempRoot, geometry);
    if (geometry.Polygons[0].Behavior != "damage-proven")
        throw new InvalidOperationException("Terrain material classifier did not apply proven behavior evidence.");

    Console.WriteLine($"Terrain behavior proof store: {waterRule.Behavior}, observations={waterRule.ObservationCount}, solidControls={waterRule.SolidControlCount}");
}

async Task ReportTerrainBehaviorProofTargets()
{
    TerrainBehaviorProofReports reports = await TerrainBehaviorProofReportBuilder.BuildAsync(workspace, catalog);
    TerrainBehaviorProofReportPaths paths = await TerrainBehaviorProofReportBuilder.WriteAsync(reports, Path.Combine(workspace.RootPath, "_local", "smoke"));

    int hazardTargets = reports.Targets.Targets.Count(target => target.CaptureKind == "hazard-touch");
    int solidTargets = reports.Targets.Targets.Count(target => target.CaptureKind == "solid-control");
    Console.WriteLine($"Terrain behavior proof targets: {hazardTargets} hazard target(s), {solidTargets} solid-control target(s), report={paths.TargetsMarkdownPath}");
}

async Task ReportTerrainBehaviorProofSummary()
{
    TerrainBehaviorProofReports reports = await TerrainBehaviorProofReportBuilder.BuildAsync(workspace, catalog);
    TerrainBehaviorProofReportPaths paths = await TerrainBehaviorProofReportBuilder.WriteAsync(reports, Path.Combine(workspace.RootPath, "_local", "smoke"));

    int proven = reports.Summary.Surfaces.Count(surface => surface.ProofStatusRank >= 3);
    int observed = reports.Summary.Surfaces.Count(surface => surface.ProofStatusRank == 2);
    int missing = reports.Summary.Surfaces.Count(surface => surface.ProofStatusRank == 0);
    Console.WriteLine($"Terrain behavior proof summary: {proven} proven, {observed} observed, {missing} missing surface row(s), report={paths.SummaryMarkdownPath}");
}

async Task ReportCustomTerrainTextureManifestRoundTrip()
{
    string tempRoot = Path.Combine(workspace.RootPath, "_local", "smoke", "custom-texture-manifest");
    Directory.CreateDirectory(tempRoot);
    string stagedImagePath = Path.Combine(tempRoot, "stonehill-texture-032-smoke.png");
    await WriteSmokePngAsync(stagedImagePath, 64, 64);

    IReadOnlyList<CustomTerrainTextureImport> saved = await CustomTerrainTextureStore.AddOrReplaceAsync(
        tempRoot,
        "stonehill",
        "Stone Hill",
        32,
        stagedImagePath,
        "stonehill-texture-032-smoke.png",
        "both",
        64,
        "generated-palette",
        "Smoke palette",
        "#244C98",
        "#8CC8FF");
    IReadOnlyList<CustomTerrainTextureImport> loaded = CustomTerrainTextureStore.Load(tempRoot, "stonehill");
    if (saved.Count != 1 ||
        loaded.Count != 1 ||
        loaded[0].TextureId != 32 ||
        loaded[0].DescriptorTier != "both" ||
        loaded[0].SourceKind != "generated-palette" ||
        loaded[0].PaletteName != "Smoke palette" ||
        loaded[0].PaletteLowHex != "#244C98" ||
        loaded[0].PaletteHighHex != "#8CC8FF" ||
        loaded[0].PaletteHexColors?.Count != 2)
    {
        throw new InvalidOperationException("Custom terrain texture manifest roundtrip failed.");
    }

    Console.WriteLine($"Custom terrain texture manifest: roundtrip {loaded.Count} import(s), texture {loaded[0].TextureId}, {loaded[0].DescriptorTier}, {loaded[0].SourceKind}, {loaded[0].PaletteName}, {loaded[0].PaletteLowHex}->{loaded[0].PaletteHighHex}");

    string stoneHillOverlay = Path.Combine(workspace.RootPath, "editor-cache", "stonehill-runtime-scene-editor-overlay.json");
    if (File.Exists(stoneHillOverlay))
    {
        GeometryCandidate previewGeometry = GeometryOverlayLoader.LoadFirstCandidate(stoneHillOverlay);
        TerrainMaterialClassifier.Apply("stonehill", workspace.RootPath, previewGeometry);
        int previewedFaces = CustomTerrainTexturePreview.Apply(previewGeometry, loaded);
        TerrainPolygon? previewFace = previewGeometry.Polygons.FirstOrDefault(face => face.TextureId == 32);
        if (previewedFaces <= 0 ||
            previewFace == null ||
            previewFace.SurfaceColor.R != 88 ||
            previewFace.SurfaceColor.G != 138 ||
            previewFace.SurfaceColor.B != 203 ||
            !previewFace.SurfaceSource.Contains("custom palette", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Custom terrain texture preview color was not applied to matching faces.");
        }

        Console.WriteLine($"Custom terrain texture preview: {previewedFaces} face(s), texture 32 color #{previewFace.SurfaceColor.R:X2}{previewFace.SurfaceColor.G:X2}{previewFace.SurfaceColor.B:X2}");
    }

    string surfacePaletteRoot = Path.Combine(workspace.RootPath, "_local", "smoke", "surface-palette-manifest");
    Directory.CreateDirectory(surfacePaletteRoot);
    string surfaceImagePath = Path.Combine(surfacePaletteRoot, "stonehill-water-surface-palette.png");
    await WriteSmokePngAsync(surfaceImagePath, 64, 64);
    await CustomTerrainTextureStore.AddOrReplaceAsync(surfacePaletteRoot, "stonehill", "Stone Hill", 32, surfaceImagePath, "stonehill-water-032.png", "both", 64, "generated-surface-palette", "Clear water", "#144C8E", "#77C2F5");
    IReadOnlyList<CustomTerrainTextureImport> surfaceSaved = await CustomTerrainTextureStore.AddOrReplaceAsync(surfacePaletteRoot, "stonehill", "Stone Hill", 33, surfaceImagePath, "stonehill-water-033.png", "both", 64, "generated-surface-palette", "Clear water", "#144C8E", "#77C2F5");
    if (surfaceSaved.Count != 2 || surfaceSaved.Any(texture => texture.SourceKind != "generated-surface-palette" || texture.PaletteName != "Clear water" || texture.DescriptorTier != "both"))
        throw new InvalidOperationException("Surface terrain palette manifest roundtrip failed.");
    Console.WriteLine($"Surface terrain palette manifest: {surfaceSaved.Count} texture(s), preset {surfaceSaved[0].PaletteName}, tier {surfaceSaved[0].DescriptorTier}");

    if (File.Exists(sourceImage))
    {
        string extractedTexturePath = Path.Combine(tempRoot, "stonehill-texture-032-extracted-native.png");
        TerrainTextureImageExport? extractedTexture = await TerrainPatchExporter.TryExportTerrainTextureImageAsync(
            sourceImage,
            stoneHill,
            32,
            extractedTexturePath);
        if (extractedTexture == null)
            throw new InvalidOperationException("Native terrain texture extraction did not produce a texture image for Stone Hill texture 32.");

        Rgba32[] extractedPixels = PngRgbaImage.ReadRgba(extractedTexturePath, out int extractedWidth, out int extractedHeight);
        int visiblePixels = extractedPixels.Count(pixel => pixel.A >= 200);
        int uniqueVisibleColors = extractedPixels
            .Where(pixel => pixel.A >= 200)
            .Distinct()
            .Take(8)
            .Count();
        if (extractedWidth != extractedTexture.Width ||
            extractedHeight != extractedTexture.Height ||
            visiblePixels < Math.Min(256, extractedTexture.PixelCount) ||
            uniqueVisibleColors < 2 ||
            !File.Exists(extractedTexturePath))
        {
            throw new InvalidOperationException("Native terrain texture extraction produced an empty or invalid PNG.");
        }

        Console.WriteLine($"Native terrain texture extraction: texture {extractedTexture.TextureId}, {extractedTexture.DescriptorTier}, {extractedTexture.Width}x{extractedTexture.Height}, pixels={extractedTexture.PixelCount}, visible={visiblePixels}, colors>={uniqueVisibleColors}");
    }
    else
    {
        Console.WriteLine("Native terrain texture extraction: source image not found; skipping.");
    }

    string ramPath = TerrainPatchDataLocator.FindRamDump(workspace, "stonehill");
    string sourceSearchPath = Path.Combine(workspace.RootPath, "_local", "smoke", "stonehill-runtime-terrain-source-search-native.json");
    if (!File.Exists(sourceImage) || !File.Exists(ramPath) || !File.Exists(sourceSearchPath))
    {
        Console.WriteLine("Custom terrain texture patch plan: source image, RAM, or terrain source search not found; skipping.");
        return;
    }

    string emptyEditsPath = Path.Combine(tempRoot, "empty-terrain-edits.json");
    await File.WriteAllTextAsync(emptyEditsPath, "{\"edits\":[]}");
    TerrainPatchPlan plan = TerrainPatchExporter.BuildPlan(
        sourceImage,
        DiscImageLocator.FindCueForImage(sourceImage),
        Path.Combine(tempRoot, "custom-texture-smoke.bin"),
        Path.Combine(tempRoot, "custom-texture-smoke.cue"),
        stoneHill,
        ramPath,
        sourceSearchPath,
        emptyEditsPath,
        CustomTerrainTextureStore.ManifestPath(tempRoot, "stonehill"));
    if (plan.CustomTextureImportCount < 2 ||
        !plan.CustomTextureImports.Any(import => import.DescriptorTier == "hqDataClose") ||
        plan.CustomTextureBytePatchCount <= 0)
    {
        throw new InvalidOperationException("Custom terrain texture patch planning did not produce both normal and close-detail texture-page patches.");
    }
    if (plan.CustomTextureImports.Any(import => import.TexelBytesPerPixel != 1 || import.PaletteColorCount != 256 || import.SourcePaletteUniqueColorCount <= 1))
        throw new InvalidOperationException("Custom terrain texture patch planning did not prove the expected 8-bit indexed terrain texture-page format.");
    int sourceTexelMax = plan.CustomTextureImports.Max(import => import.SourceTexelMax);
    if (sourceTexelMax <= 15)
        throw new InvalidOperationException("Custom terrain texture source texels did not exceed 4-bit range; 8-bit terrain texture-page proof is too weak.");

    Console.WriteLine($"Custom terrain texture patch plan: {plan.CustomTextureImportCount} tier import(s), {plan.CustomTextureBytePatchCount} byte patch(es), close-detail={plan.CustomTextureImports.Any(import => import.DescriptorTier == "hqDataClose")}, format=8bpp-indexed, source-texel-max={sourceTexelMax}");

    TerrainPatchPlan customOnlyPlan = TerrainPatchExporter.BuildPlan(
        sourceImage,
        DiscImageLocator.FindCueForImage(sourceImage),
        Path.Combine(tempRoot, "custom-texture-no-ram-smoke.bin"),
        Path.Combine(tempRoot, "custom-texture-no-ram-smoke.cue"),
        stoneHill,
        Path.Combine(tempRoot, "missing-ram.bin"),
        Path.Combine(tempRoot, "missing-source-search.json"),
        emptyEditsPath,
        CustomTerrainTextureStore.ManifestPath(tempRoot, "stonehill"));
    if (customOnlyPlan.CustomTextureBytePatchCount <= 0 ||
        customOnlyPlan.Patches.Any(patch => !patch.Kind.StartsWith("custom-texture-", StringComparison.OrdinalIgnoreCase)))
    {
        throw new InvalidOperationException("Custom-only terrain recolor should patch texture pages without requiring RAM/source-search terrain face data.");
    }
    Console.WriteLine($"Custom terrain texture no-RAM patch plan: {customOnlyPlan.CustomTextureImportCount} tier import(s), {customOnlyPlan.CustomTextureBytePatchCount} byte patch(es), asset {customOnlyPlan.TextureAssetWadIndex}");

    TerrainPatchPlan surfacePlan = TerrainPatchExporter.BuildPlan(
        sourceImage,
        DiscImageLocator.FindCueForImage(sourceImage),
        Path.Combine(surfacePaletteRoot, "surface-palette-smoke.bin"),
        Path.Combine(surfacePaletteRoot, "surface-palette-smoke.cue"),
        stoneHill,
        ramPath,
        sourceSearchPath,
        emptyEditsPath,
        CustomTerrainTextureStore.ManifestPath(surfacePaletteRoot, "stonehill"));
    bool surfacePlanHasBothTiers = surfacePlan.CustomTextureImports
        .GroupBy(import => import.TextureId)
        .Count(group => group.Any(import => import.DescriptorTier == "hqData") &&
            group.Any(import => import.DescriptorTier == "hqDataClose")) >= 2;
    if (surfacePlan.CustomTextureImportCount < 4 ||
        !surfacePlanHasBothTiers ||
        surfacePlan.CustomTextureBytePatchCount <= 0 ||
        surfacePlan.CustomTextureImports.Any(import => import.TexelBytesPerPixel != 1 || import.PaletteColorCount != 256))
    {
        throw new InvalidOperationException("Surface terrain palette patch planning did not produce normal and close-detail texture-page patches for both selected textures.");
    }
    Console.WriteLine($"Surface terrain palette patch plan: {surfacePlan.CustomTextureImportCount} tier import(s), {surfacePlan.CustomTextureBytePatchCount} byte patch(es), textures={string.Join(",", surfacePlan.CustomTextureImports.Select(import => import.TextureId).Distinct().Order())}, format=8bpp-indexed");

    string switchStoneHillOverlay = Path.Combine(workspace.RootPath, "editor-cache", "stonehill-runtime-scene-editor-overlay.json");
    if (File.Exists(switchStoneHillOverlay))
    {
        GeometryCandidate switchGeometry = GeometryOverlayLoader.LoadFirstCandidate(switchStoneHillOverlay);
        TerrainMaterialClassifier.Apply("stonehill", workspace.RootPath, switchGeometry);
        TerrainPolygon sourceGrassFace = switchGeometry.Polygons.First(face =>
            string.Equals(TerrainMaterialClassifier.NormalizeSurfaceName(face.Surface), "grass", StringComparison.OrdinalIgnoreCase) &&
            face.TextureId != 32);
        sourceGrassFace.ApplyTextureOverride(32);
        string switchEditsPath = Path.Combine(surfacePaletteRoot, "stonehill-grass-to-water-terrain-edits.json");
        int switchEditCount = await TerrainEditStore.SaveAsync(switchEditsPath, switchGeometry.Polygons, "Stone Hill");
        TerrainPatchPlan switchPlan = TerrainPatchExporter.BuildPlan(
            sourceImage,
            DiscImageLocator.FindCueForImage(sourceImage),
            Path.Combine(surfacePaletteRoot, "surface-palette-switch-smoke.bin"),
            Path.Combine(surfacePaletteRoot, "surface-palette-switch-smoke.cue"),
            stoneHill,
            ramPath,
            sourceSearchPath,
            switchEditsPath,
            CustomTerrainTextureStore.ManifestPath(surfacePaletteRoot, "stonehill"));
        bool hasTextureIdPatch = switchPlan.Patches.Any(patch => string.Equals(patch.Kind, "texture-id-word3", StringComparison.OrdinalIgnoreCase));
        if (switchEditCount <= 0 || !hasTextureIdPatch || switchPlan.CustomTextureBytePatchCount <= 0)
        {
            throw new InvalidOperationException("Surface palette grass-to-water switch did not produce both terrain texture-id and texture-page patches.");
        }

        Console.WriteLine($"Surface palette texture switch patch plan: {switchEditCount} terrain edit(s), texture-id patch={hasTextureIdPatch}, custom texture patches={switchPlan.CustomTextureBytePatchCount}");
    }
}

async Task ReportImportedTerrainPaletteRoundTrip()
{
    string tempRoot = Path.Combine(workspace.RootPath, "_local", "smoke", "imported-terrain-palette");
    Directory.CreateDirectory(tempRoot);
    string palettePath = Path.Combine(tempRoot, "smoke-imported-terrain.gpl");
    await File.WriteAllTextAsync(
        palettePath,
        """
        GIMP Palette
        Name: Smoke imported terrain
        24 48 92 Deep imported shadow
        120 210 250 Bright imported highlight
        #366A9C
        """);

    TerrainPaletteImport palette = await TerrainPaletteImporter.ImportFileAsync(palettePath);
    if (palette.DisplayName != "smoke-imported-terrain" ||
        palette.ColorCount != 3 ||
        palette.Colors.Count != 3 ||
        palette.LowHex != "#18305C" ||
        palette.HighHex != "#78D2FA")
    {
        throw new InvalidOperationException($"Imported terrain palette parse failed: {palette.DisplayName}, {palette.ColorCount}, {palette.LowHex}->{palette.HighHex}");
    }

    string stagedImagePath = Path.Combine(tempRoot, "stonehill-texture-032-imported-palette.png");
    await TerrainTexturePngWriter.WritePaletteAsync(stagedImagePath, 64, 64, palette.Colors);
    AssertPngContainsApproxColor(stagedImagePath, ColorRgba.FromRgb(54, 106, 156), "imported terrain palette midtone");
    IReadOnlyList<CustomTerrainTextureImport> saved = await CustomTerrainTextureStore.AddOrReplaceAsync(
        tempRoot,
        "stonehill",
        "Stone Hill",
        32,
        stagedImagePath,
        Path.GetFileName(palettePath),
        "both",
        64,
        "imported-palette",
        palette.DisplayName,
        palette.LowHex,
        palette.HighHex,
        paletteHexColors: palette.Colors.Select(NormalizeSmokeHex).ToArray());

    CustomTerrainTextureImport import = saved.Single(texture => texture.TextureId == 32);
    if (import.SourceKind != "imported-palette" ||
        import.PaletteName != palette.DisplayName ||
        import.PaletteLowHex != palette.LowHex ||
        import.PaletteHighHex != palette.HighHex ||
        import.PaletteHexColors?.Count != 3)
    {
        throw new InvalidOperationException("Imported terrain palette manifest roundtrip failed.");
    }
    if (!CustomTerrainTexturePreview.TryGetPreviewColor(import, out ColorRgba importedPreview) ||
        Math.Abs(importedPreview.R - 54) > 1 ||
        Math.Abs(importedPreview.G - 106) > 1 ||
        Math.Abs(importedPreview.B - 156) > 1)
    {
        throw new InvalidOperationException("Imported terrain palette preview did not use the persisted midtone.");
    }

    Console.WriteLine($"Imported terrain palette: {palette.DisplayName}, colors={palette.ColorCount}, texture {import.TextureId}, {import.PaletteLowHex}->{import.PaletteHighHex}");

    TerrainPaletteImport pasted = TerrainPaletteImporter.ImportText(
        """
        #102040 #C0E0FF
        64 128 180 pasted midtone
        """,
        "Pasted smoke palette");
    if (pasted.DisplayName != "Pasted smoke palette" ||
        pasted.ColorCount != 3 ||
        pasted.Colors.Count != 3 ||
        pasted.LowHex != "#102040" ||
        pasted.HighHex != "#C0E0FF")
    {
        throw new InvalidOperationException($"Pasted terrain palette parse failed: {pasted.DisplayName}, {pasted.ColorCount}, {pasted.LowHex}->{pasted.HighHex}");
    }

    string pastedImagePath = Path.Combine(tempRoot, "stonehill-texture-033-pasted-palette.png");
    await TerrainTexturePngWriter.WritePaletteAsync(pastedImagePath, 64, 64, pasted.Colors);
    AssertPngContainsApproxColor(pastedImagePath, ColorRgba.FromRgb(64, 128, 180), "pasted terrain palette midtone");
    IReadOnlyList<CustomTerrainTextureImport> pastedSaved = await CustomTerrainTextureStore.AddOrReplaceAsync(
        tempRoot,
        "stonehill",
        "Stone Hill",
        33,
        pastedImagePath,
        "pasted-palette",
        "both",
        64,
        "pasted-palette",
        pasted.DisplayName,
        pasted.LowHex,
        pasted.HighHex,
        paletteHexColors: pasted.Colors.Select(NormalizeSmokeHex).ToArray());
    CustomTerrainTextureImport pastedImport = pastedSaved.Single(texture => texture.TextureId == 33);
    if (pastedImport.SourceKind != "pasted-palette" ||
        pastedImport.PaletteName != pasted.DisplayName ||
        pastedImport.PaletteLowHex != pasted.LowHex ||
        pastedImport.PaletteHighHex != pasted.HighHex ||
        pastedImport.PaletteHexColors?.Count != 3)
    {
        throw new InvalidOperationException("Pasted terrain palette manifest roundtrip failed.");
    }
    if (!CustomTerrainTexturePreview.TryGetPreviewColor(pastedImport, out ColorRgba pastedPreview) ||
        Math.Abs(pastedPreview.R - 64) > 1 ||
        Math.Abs(pastedPreview.G - 128) > 1 ||
        Math.Abs(pastedPreview.B - 180) > 1)
    {
        throw new InvalidOperationException("Pasted terrain palette preview did not use the persisted midtone.");
    }

    Console.WriteLine($"Pasted terrain palette: {pasted.DisplayName}, colors={pasted.ColorCount}, texture {pastedImport.TextureId}, {pastedImport.PaletteLowHex}->{pastedImport.PaletteHighHex}");

    IReadOnlyList<CustomTerrainTextureImport> copiedLookSaved = await CustomTerrainTextureStore.AddOrReplaceAsync(
        tempRoot,
        "stonehill",
        "Stone Hill",
        34,
        pastedImport.SourceImagePath,
        pastedImport.SourceImageName,
        pastedImport.DescriptorTier,
        pastedImport.TileSize,
        pastedImport.SourceKind,
        pastedImport.PaletteName,
        pastedImport.PaletteLowHex,
        pastedImport.PaletteHighHex,
        paletteHexColors: pastedImport.PaletteHexColors ?? Array.Empty<string>());
    CustomTerrainTextureImport copiedLookImport = copiedLookSaved.Single(texture => texture.TextureId == 34);
    if (copiedLookImport.SourceImagePath != pastedImport.SourceImagePath ||
        copiedLookImport.SourceImageName != pastedImport.SourceImageName ||
        copiedLookImport.DescriptorTier != pastedImport.DescriptorTier ||
        copiedLookImport.TileSize != pastedImport.TileSize ||
        copiedLookImport.SourceKind != pastedImport.SourceKind ||
        copiedLookImport.PaletteName != pastedImport.PaletteName ||
        copiedLookImport.PaletteLowHex != pastedImport.PaletteLowHex ||
        copiedLookImport.PaletteHighHex != pastedImport.PaletteHighHex ||
        copiedLookImport.PaletteHexColors?.SequenceEqual(pastedImport.PaletteHexColors ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase) != true)
    {
        throw new InvalidOperationException("Custom terrain look metadata paste did not preserve the imported palette fields.");
    }

    Console.WriteLine($"Custom terrain look metadata paste: texture {pastedImport.TextureId} -> {copiedLookImport.TextureId}, {copiedLookImport.PaletteName}, colors={copiedLookImport.PaletteHexColors?.Count ?? 0}");

    string filteredManifestPath = Path.Combine(tempRoot, "filtered", "active-custom-terrain-textures.json");
    await CustomTerrainTextureStore.SaveManifestAsync(
        filteredManifestPath,
        "stonehill",
        "Stone Hill",
        new[] { copiedLookImport });
    IReadOnlyList<CustomTerrainTextureImport> filteredImports = CustomTerrainTextureStore.LoadManifest(filteredManifestPath);
    if (filteredImports.Count != 1 ||
        filteredImports[0].TextureId != copiedLookImport.TextureId ||
        filteredImports.Any(import => import.TextureId == pastedImport.TextureId))
    {
        throw new InvalidOperationException("Filtered custom terrain texture manifest did not keep only the active copied texture import.");
    }

    Console.WriteLine($"Filtered custom terrain texture manifest: kept texture {copiedLookImport.TextureId}, omitted stale texture {pastedImport.TextureId}");

    string cleanupManifestPath = Path.Combine(tempRoot, "filtered", "cleanup-custom-terrain-textures.json");
    await CustomTerrainTextureStore.SaveManifestAsync(
        cleanupManifestPath,
        "stonehill",
        "Stone Hill",
        new[] { pastedImport, copiedLookImport });
    IReadOnlyList<CustomTerrainTextureImport> cleanupBefore = CustomTerrainTextureStore.LoadManifest(cleanupManifestPath);
    await CustomTerrainTextureStore.SaveManifestAsync(
        cleanupManifestPath,
        "stonehill",
        "Stone Hill",
        cleanupBefore.Where(import => import.TextureId == copiedLookImport.TextureId).ToArray());
    IReadOnlyList<CustomTerrainTextureImport> cleanupAfter = CustomTerrainTextureStore.LoadManifest(cleanupManifestPath);
    if (cleanupBefore.Count != 2 ||
        cleanupAfter.Count != 1 ||
        cleanupAfter[0].TextureId != copiedLookImport.TextureId ||
        cleanupAfter.Any(import => import.TextureId == pastedImport.TextureId))
    {
        throw new InvalidOperationException("Custom terrain unused-art cleanup did not remove the stale texture import.");
    }

    Console.WriteLine($"Custom terrain unused-art cleanup: {cleanupBefore.Count} import(s) -> {cleanupAfter.Count}, freed texture {pastedImport.TextureId}");

    string imagePalettePath = Path.Combine(tempRoot, "smoke-imported-terrain-image.png");
    await WritePaletteSwatchPngAsync(
        imagePalettePath,
        ColorRgba.FromRgb(24, 48, 88),
        ColorRgba.FromRgb(56, 104, 152),
        ColorRgba.FromRgb(120, 208, 248));
    TerrainPaletteImport imagePalette = await TerrainPaletteImporter.ImportFileAsync(imagePalettePath);
    if (imagePalette.DisplayName != "smoke-imported-terrain-image" ||
        imagePalette.ColorCount < 3 ||
        imagePalette.Colors.Count < 3 ||
        imagePalette.LowHex != "#183058" ||
        imagePalette.HighHex != "#78D0F8")
    {
        throw new InvalidOperationException($"Imported PNG terrain palette parse failed: {imagePalette.DisplayName}, {imagePalette.ColorCount}, {imagePalette.LowHex}->{imagePalette.HighHex}");
    }

    Console.WriteLine($"Imported PNG terrain palette: {imagePalette.DisplayName}, colors={imagePalette.ColorCount}, {imagePalette.LowHex}->{imagePalette.HighHex}");
}

void AssertPngContainsApproxColor(string path, ColorRgba expected, string label)
{
    Rgba32[] pixels = PngRgbaImage.ReadRgba(path, out _, out _);
    bool found = pixels.Any(pixel =>
        pixel.A >= 200 &&
        Math.Abs(pixel.R - expected.R) <= 10 &&
        Math.Abs(pixel.G - expected.G) <= 10 &&
        Math.Abs(pixel.B - expected.B) <= 10);
    if (!found)
        throw new InvalidOperationException($"Generated terrain texture did not preserve {label} near #{expected.R:X2}{expected.G:X2}{expected.B:X2}.");
}

string NormalizeSmokeHex(ColorRgba color)
{
    return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}

async Task ReportCrossLevelTerrainTexturePatchPlan()
{
    string[] levelKeys = catalog.Levels
        .Select(level => level.Key)
        .ToArray();
    string reportRoot = Path.Combine(workspace.RootPath, "_local", "smoke", "cross-level-terrain-textures");
    Directory.CreateDirectory(reportRoot);
    List<CrossLevelTerrainTextureRow> rows = new();
    if (!File.Exists(sourceImage))
    {
        rows.Add(new CrossLevelTerrainTextureRow("all", "All captured levels", "skipped", "", -1, -1, 0, 0, 0, 0, 0, 0, "", "Import a source BIN/CUE before running texture patch smoke."));
        WriteCrossLevelTerrainTextureReport(reportRoot, rows);
        Console.WriteLine("Cross-level terrain texture patch plans: source image not found; skipping.");
        return;
    }

    foreach (string levelKey in levelKeys)
    {
        LevelDefinition? level = catalog.FindByKey(levelKey);
        string overlayPath = Path.Combine(workspace.RootPath, "editor-cache", $"{levelKey}-runtime-scene-editor-overlay.json");
        string ramPath = TerrainPatchDataLocator.FindRamDump(workspace, levelKey);
        if (level == null)
        {
            rows.Add(new CrossLevelTerrainTextureRow(levelKey, level?.DisplayName ?? levelKey, "skipped", "missing overlay", -1, -1, 0, 0, 0, 0, 0, 0, "", "Build or load this level's terrain overlay before proving per-face texture swaps."));
            continue;
        }

        string tempRoot = Path.Combine(reportRoot, level.Key);
        Directory.CreateDirectory(tempRoot);
        SmokeTerrainGeometry? geometryInfo = await TryLoadTerrainGeometryForSmoke(level, overlayPath, ramPath, tempRoot);
        if (geometryInfo == null)
        {
            rows.Add(new CrossLevelTerrainTextureRow(level.Key, level.DisplayName, "skipped", "missing overlay", -1, -1, 0, 0, 0, 0, 0, 0, "", "Build/open this level's terrain map or select a BIN/CUE so the source-derived terrain map can be recovered."));
            continue;
        }

        GeometryCandidate geometry = geometryInfo.Geometry;
        ramPath = geometryInfo.RamPath;
        string sourceSearchPath = Path.Combine(tempRoot, $"{level.Key}-runtime-terrain-source-search-native.json");
        TerrainSmokeSourceSearch? sourceSearchInfo = await TryBuildTerrainSourceSearchForSmoke(level, geometry, ramPath, sourceSearchPath);
        if (sourceSearchInfo == null)
        {
            rows.Add(new CrossLevelTerrainTextureRow(level.Key, level.DisplayName, "skipped", "missing RAM capture or source-derived overlay", -1, -1, 0, 0, 0, 0, 0, 0, "", "Capture RAM or rebuild the cache with source-derived terrain offsets before proving per-face texture swaps."));
            continue;
        }

        TerrainSourceSearchResult sourceSearch = sourceSearchInfo.Result;
        HashSet<string> sourceMappedKeys = ReadTerrainSourceKeys(sourceSearchPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        TerrainTextureSlot[] slots = TerrainPatchExporter.InspectTextureSlots(sourceImage, level).ToArray();
        Dictionary<int, TerrainTextureSlot> slotByTextureId = slots.ToDictionary(slot => slot.TextureId);
        List<IGrouping<int, TerrainPolygon>> textureGroups = geometry.Polygons
            .Where(face => face.TextureId >= 0 &&
                face.SectorOffset >= 0 &&
                face.VertexIndexes.Count > 0 &&
                sourceMappedKeys.Contains(face.RuntimeKey))
            .GroupBy(face => face.TextureId)
            .OrderByDescending(group => group.Count())
            .ToList();
        IGrouping<int, TerrainPolygon>? sourceGroup = textureGroups.FirstOrDefault(group =>
            !IsKnownBadCustomTerrainTextureCandidate(level.Key, group.Key) &&
            slotByTextureId.TryGetValue(group.Key, out TerrainTextureSlot? slot) &&
            (slot.HasNormalDescriptors || slot.HasCloseDescriptors));
        int targetTextureId = textureGroups.Select(group => group.Key).FirstOrDefault(textureId => sourceGroup != null && textureId != sourceGroup.Key);
        if (sourceGroup == null || targetTextureId < 0 || targetTextureId == sourceGroup.Key)
        {
            rows.Add(new CrossLevelTerrainTextureRow(
                level.Key,
                level.DisplayName,
                "skipped",
                sourceGroup?.FirstOrDefault()?.RuntimeKey ?? "no swappable source-mapped texture",
                sourceGroup?.Key ?? -1,
                targetTextureId,
                sourceSearch.Report.SectorCount,
                sourceSearch.Report.MatchedSectorCount,
                0,
                0,
                0,
                0,
                "",
                "Need at least two source-mapped texture IDs and a decoded texture descriptor."));
            continue;
        }

        TerrainPolygon face = sourceGroup.First();
        int sourceTextureId = sourceGroup.Key;
        TerrainTextureSlot sourceSlot = slotByTextureId[sourceTextureId];
        string descriptorTierRequest = sourceSlot.HasNormalDescriptors && sourceSlot.HasCloseDescriptors
            ? "both"
            : sourceSlot.HasCloseDescriptors ? "hqDataClose" : "hqData";

        face.ApplyTextureOverride(targetTextureId);
        string textureSwapEditsPath = Path.Combine(tempRoot, $"{level.Key}-texture-swap-terrain-edits.json");
        int swapEditCount = await TerrainEditStore.SaveAsync(textureSwapEditsPath, [face], $"{level.DisplayName} texture swap smoke");
        face.ResetTerrainEdit();
        TerrainPatchPlan swapPlan = TerrainPatchExporter.BuildPlan(
            sourceImage,
            DiscImageLocator.FindCueForImage(sourceImage),
            Path.Combine(tempRoot, "texture-swap-smoke.bin"),
            Path.Combine(tempRoot, "texture-swap-smoke.cue"),
            level,
            sourceSearchInfo.PatchRamPath,
            sourceSearchPath,
            textureSwapEditsPath,
            "");
        int textureSwapPatchCount = swapPlan.Patches.Count(patch =>
            patch.RuntimeKey.Equals(face.RuntimeKey, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(patch.Kind, "texture-id-word3", StringComparison.OrdinalIgnoreCase));
        if (swapEditCount != 1 || textureSwapPatchCount <= 0)
            throw new InvalidOperationException($"{level.DisplayName} cross-level texture swap did not produce a texture-id patch for {face.RuntimeKey}.");

        TerrainPaletteImport palette = TerrainPaletteImporter.ImportText(
            """
            #182848
            50 96 146 cross level midtone
            #8CEBFF
            """,
            $"{level.DisplayName} cross-level imported palette");
        string stagedImagePath = Path.Combine(tempRoot, $"{level.Key}-texture-{sourceTextureId:000}-imported-palette.png");
        await TerrainTexturePngWriter.WritePaletteAsync(stagedImagePath, 64, 64, palette.Colors);
        IReadOnlyList<CustomTerrainTextureImport> saved = await CustomTerrainTextureStore.AddOrReplaceAsync(
            tempRoot,
            level.Key,
            level.DisplayName,
            sourceTextureId,
            stagedImagePath,
            "cross-level-imported-palette",
            descriptorTierRequest,
            64,
            "imported-palette",
            palette.DisplayName,
            palette.LowHex,
            palette.HighHex,
            paletteHexColors: palette.Colors.Select(NormalizeSmokeHex).ToArray());
        int previewedFaces = CustomTerrainTexturePreview.Apply(geometry, saved);
        if (previewedFaces <= 0)
            throw new InvalidOperationException($"{level.DisplayName} custom palette preview did not touch any texture {sourceTextureId} faces.");

        string emptyEditsPath = Path.Combine(tempRoot, "empty-terrain-edits.json");
        await File.WriteAllTextAsync(emptyEditsPath, "{\"edits\":[]}");
        TerrainPatchPlan customPlan = TerrainPatchExporter.BuildPlan(
            sourceImage,
            DiscImageLocator.FindCueForImage(sourceImage),
            Path.Combine(tempRoot, "custom-palette-smoke.bin"),
            Path.Combine(tempRoot, "custom-palette-smoke.cue"),
            level,
            sourceSearchInfo.PatchRamPath,
            sourceSearchPath,
            emptyEditsPath,
            CustomTerrainTextureStore.ManifestPath(tempRoot, level.Key));

        int expectedTextureAsset = TerrainPatchExporter.TextureAssetWadIndexForLevel(level);
        bool customPatchOk = customPlan.TextureAssetWadIndex == expectedTextureAsset &&
            customPlan.CustomTextureImportCount > 0 &&
            customPlan.CustomTextureBytePatchCount > 0 &&
            customPlan.CustomTextureImports.All(import => import.TexelBytesPerPixel == 1 && import.PaletteColorCount == 256);
        if (!customPatchOk)
        {
            rows.Add(new CrossLevelTerrainTextureRow(
                level.Key,
                level.DisplayName,
                "texture-swap-only",
                face.RuntimeKey,
                sourceTextureId,
                targetTextureId,
                sourceSearch.Report.SectorCount,
                sourceSearch.Report.MatchedSectorCount,
                textureSwapPatchCount,
                customPlan.CustomTextureBytePatchCount,
                customPlan.CustomTextureImportCount,
                previewedFaces,
                string.Join("+", customPlan.CustomTextureImports.Select(import => import.DescriptorTier).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)),
                $"Texture ID swap {sourceTextureId}->{targetTextureId} is patchable; custom imported palette did not pass full 8-bit patch gate. {FirstTerrainPlanSkip(customPlan)}"));
            continue;
        }

        string tiers = string.Join("+", customPlan.CustomTextureImports
            .Select(import => import.DescriptorTier)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase));
        rows.Add(new CrossLevelTerrainTextureRow(
            level.Key,
            level.DisplayName,
            "smoke-passed",
            face.RuntimeKey,
            sourceTextureId,
            targetTextureId,
            sourceSearch.Report.SectorCount,
            sourceSearch.Report.MatchedSectorCount,
            textureSwapPatchCount,
            customPlan.CustomTextureBytePatchCount,
            customPlan.CustomTextureImportCount,
            previewedFaces,
            tiers,
            $"Texture ID swap {sourceTextureId}->{targetTextureId}; custom imported palette patched WAD asset {customPlan.TextureAssetWadIndex}." +
            (sourceSearchInfo.SourceDerived ? " Source-derived terrain source map; no RAM capture required for this texture/art proof." : "")));
    }

    WriteCrossLevelTerrainTextureReport(reportRoot, rows);
    int passed = rows.Count(row => string.Equals(row.Status, "smoke-passed", StringComparison.OrdinalIgnoreCase));
    int textureSwapOnly = rows.Count(row => string.Equals(row.Status, "texture-swap-only", StringComparison.OrdinalIgnoreCase));
    int skipped = rows.Count(row => string.Equals(row.Status, "skipped", StringComparison.OrdinalIgnoreCase));
    Console.WriteLine($"Cross-level terrain texture patch plans: {passed} passed, {textureSwapOnly} texture-swap-only, {skipped} skipped, report={Path.Combine(reportRoot, "terrain-texture-readiness.md")}");
}

void WriteCrossLevelTerrainTextureReport(string reportRoot, IReadOnlyList<CrossLevelTerrainTextureRow> rows)
{
    string reportPath = Path.Combine(reportRoot, "terrain-texture-readiness.md");
    string jsonPath = Path.Combine(reportRoot, "terrain-texture-readiness.json");
    StringBuilder builder = new();
    builder.AppendLine("# Cross-Level Terrain Texture Readiness");
    builder.AppendLine();
    builder.AppendLine("This generated report checks whether captured levels can swap a source-mapped terrain face to another in-game texture and stage a custom imported palette into the level-local texture pages.");
    builder.AppendLine();
    builder.AppendLine("| Level | Status | Face | Texture swap | Source sectors | Swap patches | Custom patches | Imports | Preview faces | Tiers | Notes |");
    builder.AppendLine("|---|---|---|---|---:|---:|---:|---:|---:|---|---|");
    foreach (CrossLevelTerrainTextureRow row in rows)
    {
        string sectors = row.SourceSectorCount <= 0 ? "0/0" : $"{row.MatchedSectorCount}/{row.SourceSectorCount}";
        string swap = row.SourceTextureId >= 0 && row.TargetTextureId >= 0 ? $"{row.SourceTextureId}->{row.TargetTextureId}" : "";
        builder.AppendLine($"| {EscapeMarkdownCell(row.LevelName)} | {EscapeMarkdownCell(row.Status)} | {EscapeMarkdownCell(row.RuntimeKey)} | {EscapeMarkdownCell(swap)} | {sectors} | {row.TextureSwapPatchCount} | {row.CustomTexturePatchCount} | {row.CustomTextureImportCount} | {row.PreviewedFaceCount} | {EscapeMarkdownCell(row.DescriptorTiers)} | {EscapeMarkdownCell(row.Notes)} |");
    }

    File.WriteAllText(reportPath, builder.ToString());
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
    {
        GeneratedAtUtc = DateTimeOffset.UtcNow,
        Rows = rows
    }, new JsonSerializerOptions { WriteIndented = true }));
}

bool IsKnownBadCustomTerrainTextureCandidate(string levelKey, int textureId)
{
    return string.Equals(levelKey, "toasty", StringComparison.OrdinalIgnoreCase) && textureId == 52;
}

async Task<TerrainSmokeSourceSearch?> TryBuildTerrainSourceSearchForSmoke(
    LevelDefinition level,
    GeometryCandidate geometry,
    string ramPath,
    string sourceSearchPath)
{
    if (File.Exists(ramPath))
    {
        TerrainSourceSearchResult result = await TerrainSourceSearchBuilder.BuildAsync(new TerrainSourceSearchRequest(
            SourceImagePath: sourceImage,
            OutputPath: sourceSearchPath,
            Level: level,
            Geometry: geometry,
            RamPath: ramPath));
        return new TerrainSmokeSourceSearch(result, ramPath, HasRamCapture: true, SourceDerived: false);
    }

    if (!IsSourceDerivedGeometryForSmoke(geometry))
        return null;

    TerrainSourceSearchResult sourceDerivedResult = await TerrainSourceSearchBuilder.BuildSourceDerivedAsync(new SourceDerivedTerrainSourceSearchRequest(
        SourceImagePath: sourceImage,
        OutputPath: sourceSearchPath,
        Level: level,
        Geometry: geometry));
    return new TerrainSmokeSourceSearch(sourceDerivedResult, "", HasRamCapture: false, SourceDerived: true);
}

static bool IsSourceDerivedGeometryForSmoke(GeometryCandidate geometry)
{
    return geometry.Name.Contains("source-wad", StringComparison.OrdinalIgnoreCase) ||
        geometry.Polygons.Any(face => face.SectorOffset >= 0x800000);
}

async Task<SmokeTerrainGeometry?> TryLoadTerrainGeometryForSmoke(
    LevelDefinition level,
    string overlayPath,
    string ramPath,
    string tempRoot)
{
    GeometryCandidate? geometry = null;
    string activeOverlayPath = overlayPath;
    if (File.Exists(activeOverlayPath))
        geometry = GeometryOverlayLoader.LoadFirstCandidate(activeOverlayPath);

    if (!File.Exists(ramPath) && (geometry == null || !IsSourceDerivedGeometryForSmoke(geometry)))
    {
        string sourceDerivedPath = Path.Combine(tempRoot, $"{level.Key}-source-derived-runtime-scene-editor-overlay.json");
        if (await TryBuildSourceDerivedTerrainOverlayForSmoke(level, sourceDerivedPath))
        {
            activeOverlayPath = sourceDerivedPath;
            geometry = GeometryOverlayLoader.LoadFirstCandidate(activeOverlayPath);
        }
    }

    if (geometry == null)
        return null;

    TerrainMaterialClassifier.Apply(level.Key, workspace.RootPath, geometry);
    return new SmokeTerrainGeometry(
        geometry,
        activeOverlayPath,
        ramPath,
        SourceDerivedRecovered: !File.Exists(ramPath) && IsSourceDerivedGeometryForSmoke(geometry));
}

async Task<bool> TryBuildSourceDerivedTerrainOverlayForSmoke(LevelDefinition level, string outputPath)
{
    if (!File.Exists(sourceImage) || level.SourceWadEntry < 0)
        return false;

    try
    {
        string analysisPath = wadAnalysis;
        if (!File.Exists(analysisPath))
        {
            analysisPath = Path.Combine(workspace.RootPath, "spyro-wad-analysis.json");
            await WadAnalysisBuilder.BuildAsync(sourceImage, analysisPath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        SourceSceneOverlayExporter.Export(sourceImage, analysisPath, level, outputPath);
        return File.Exists(outputPath);
    }
    catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException or EndOfStreamException)
    {
        Console.WriteLine($"{level.DisplayName} source-derived terrain overlay recovery failed: {ex.Message}");
        return false;
    }
}

async Task ReportCrossLevelTerrainGeometryPatchPlan()
{
    string[] levelKeys = catalog.Levels
        .Select(level => level.Key)
        .ToArray();
    string reportRoot = Path.Combine(workspace.RootPath, "_local", "smoke", "cross-level-terrain-geometry");
    Directory.CreateDirectory(reportRoot);
    List<CrossLevelTerrainGeometryRow> rows = new();
    if (!File.Exists(sourceImage))
    {
        rows.Add(new CrossLevelTerrainGeometryRow("all", "All captured levels", "skipped", "source image not found", "", 0, 0, 0, 0, false, false, "Import a source BIN/CUE before running geometry patch smoke."));
        WriteCrossLevelTerrainGeometryReport(reportRoot, rows);
        Console.WriteLine("Cross-level terrain geometry patch plans: source image not found; skipping.");
        return;
    }

    foreach (string levelKey in levelKeys)
    {
        LevelDefinition? level = catalog.FindByKey(levelKey);
        string overlayPath = Path.Combine(workspace.RootPath, "editor-cache", $"{levelKey}-runtime-scene-editor-overlay.json");
        string ramPath = TerrainPatchDataLocator.FindRamDump(workspace, levelKey);
        if (level == null)
        {
            rows.Add(new CrossLevelTerrainGeometryRow(levelKey, level?.DisplayName ?? levelKey, "skipped", "missing overlay", "", 0, 0, 0, 0, false, false, "Build or load this level's terrain overlay before proving geometry patching."));
            continue;
        }

        string tempRoot = Path.Combine(reportRoot, level.Key);
        Directory.CreateDirectory(tempRoot);
        SmokeTerrainGeometry? geometryInfo = await TryLoadTerrainGeometryForSmoke(level, overlayPath, ramPath, tempRoot);
        if (geometryInfo == null)
        {
            rows.Add(new CrossLevelTerrainGeometryRow(level.Key, level.DisplayName, "skipped", "missing overlay", "", 0, 0, 0, 0, false, false, "Build/open this level's terrain map or select a BIN/CUE so the source-derived terrain map can be recovered."));
            continue;
        }

        GeometryCandidate geometry = geometryInfo.Geometry;
        ramPath = geometryInfo.RamPath;
        string sourceSearchPath = Path.Combine(reportRoot, $"{levelKey}-runtime-terrain-source-search-native.json");
        TerrainSmokeSourceSearch? sourceSearchInfo = await TryBuildTerrainSourceSearchForSmoke(level, geometry, ramPath, sourceSearchPath);
        if (sourceSearchInfo == null)
        {
            rows.Add(new CrossLevelTerrainGeometryRow(level.Key, level.DisplayName, "skipped", "missing RAM capture or source-derived overlay", "", 0, 0, 0, 0, false, false, "Capture RAM or rebuild the cache with source-derived terrain offsets before proving geometry patching."));
            continue;
        }

        TerrainSourceSearchResult sourceSearch = sourceSearchInfo.Result;
        HashSet<string> sourceMappedKeys = ReadTerrainSourceKeys(sourceSearchPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> collisionTriangleKeys = sourceSearchInfo.HasRamCapture
            ? SpyroCollisionDecoder.Decode(await File.ReadAllBytesAsync(ramPath)).Triangles
                .Select(CollisionTriangleKey)
                .ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> sourceDerivedCollisionKeys = sourceSearchInfo.SourceDerived
            ? (await ReportSourceDerivedCollisionProbe(level, geometry, reportRoot, sourceSearchPath, sourceSearch))?.HitRuntimeKeys
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        TerrainPolygon? polygon = geometry.Polygons.FirstOrDefault(face =>
                face.SectorOffset >= 0 &&
                face.VertexIndexes.Count > 0 &&
                sourceMappedKeys.Contains(face.RuntimeKey) &&
                (HasCollisionTriangleMatch(face, collisionTriangleKeys) || sourceDerivedCollisionKeys.Contains(face.RuntimeKey)))
            ?? geometry.Polygons.FirstOrDefault(face =>
                face.SectorOffset >= 0 &&
                face.VertexIndexes.Count > 0 &&
                sourceMappedKeys.Contains(face.RuntimeKey));
        if (polygon == null)
        {
            rows.Add(new CrossLevelTerrainGeometryRow(
                level.Key,
                level.DisplayName,
                "skipped",
                "no source-mapped terrain face found",
                "",
                sourceSearch.Report.SectorCount,
                sourceSearch.Report.MatchedSectorCount,
                0,
                0,
                false,
                false,
                "Source search needs a matching sector before geometry writeback can be proven."));
            continue;
        }

        string editPath = Path.Combine(reportRoot, $"{levelKey}-geometry-smoke-terrain-edits.json");
        const float DeltaZ = 6f;
        polygon.ApplyTerrainDeltaZ(DeltaZ);
        int editCount = await TerrainEditStore.SaveAsync(editPath, [polygon], $"{level.DisplayName} geometry smoke");
        polygon.ResetTerrainEdit();
        if (editCount != 1)
            throw new InvalidOperationException($"{level.DisplayName} cross-level terrain geometry smoke should save one edit.");

        TerrainPatchPlan plan = TerrainPatchExporter.BuildPlan(
            sourceImage,
            DiscImageLocator.FindCueForImage(sourceImage),
            Path.Combine(reportRoot, $"{levelKey}-geometry-smoke.bin"),
            Path.Combine(reportRoot, $"{levelKey}-geometry-smoke.cue"),
            level,
            sourceSearchInfo.PatchRamPath,
            sourceSearchPath,
            editPath,
            "");
        bool hasVisualPatch = plan.Patches.Any(patch =>
            patch.RuntimeKey.Equals(polygon.RuntimeKey, StringComparison.OrdinalIgnoreCase) &&
            patch.Kind.StartsWith("visual-", StringComparison.OrdinalIgnoreCase));
        bool hasCollisionPatch = plan.Patches.Any(patch => patch.Kind.StartsWith("collision-", StringComparison.OrdinalIgnoreCase));
        bool hadCollisionMatch = HasCollisionTriangleMatch(polygon, collisionTriangleKeys) ||
            sourceDerivedCollisionKeys.Contains(polygon.RuntimeKey) ||
            hasCollisionPatch;
        if (!hasVisualPatch)
            throw new InvalidOperationException($"{level.DisplayName} cross-level terrain geometry smoke did not produce a visual geometry patch.");
        string status = hasCollisionPatch || (!hadCollisionMatch && !sourceSearchInfo.SourceDerived)
            ? "smoke-passed"
            : "visual-only";
        string notes = hadCollisionMatch && hasCollisionPatch
            ? sourceSearchInfo.SourceDerived
                ? "Source-derived visual and exact collision geometry patches planned from WAD scene offsets."
                : "Visual and collision geometry patches planned."
            : sourceSearchInfo.SourceDerived
                ? "Source-derived visual geometry patch planned from WAD scene offsets; exact collision source bytes were not found for this face."
                : hadCollisionMatch
                ? $"Visual geometry patch planned, but the matching RAM collision triangle was not source-patched. {FirstTerrainPlanSkip(plan)}"
                : "Visual geometry patch planned; no matched collision triangle for this smoke face.";

        rows.Add(new CrossLevelTerrainGeometryRow(
            level.Key,
            level.DisplayName,
            status,
            polygon.RuntimeKey,
            $"Z +{DeltaZ:0}",
            sourceSearch.Report.SectorCount,
            sourceSearch.Report.MatchedSectorCount,
            plan.PatchCount,
            plan.Patches.Count(patch => patch.Kind.StartsWith("visual-", StringComparison.OrdinalIgnoreCase)),
            hadCollisionMatch,
            hasCollisionPatch,
            notes));
    }

    WriteCrossLevelTerrainGeometryReport(reportRoot, rows);
    int passed = rows.Count(row => string.Equals(row.Status, "smoke-passed", StringComparison.OrdinalIgnoreCase));
    int visualOnly = rows.Count(row => string.Equals(row.Status, "visual-only", StringComparison.OrdinalIgnoreCase));
    int skipped = rows.Count(row => string.Equals(row.Status, "skipped", StringComparison.OrdinalIgnoreCase));
    Console.WriteLine($"Cross-level terrain geometry patch plans: {passed} passed, {visualOnly} visual-only, {skipped} skipped, report={Path.Combine(reportRoot, "terrain-geometry-readiness.md")}");
}

string FirstTerrainPlanSkip(TerrainPatchPlan plan)
{
    return plan.SkippedEdits.FirstOrDefault(skip => skip.Contains("collision:", StringComparison.OrdinalIgnoreCase)) ??
        plan.SkippedEdits.FirstOrDefault() ??
        "No exporter skip detail was recorded.";
}

void WriteCrossLevelTerrainGeometryReport(string reportRoot, IReadOnlyList<CrossLevelTerrainGeometryRow> rows)
{
    string reportPath = Path.Combine(reportRoot, "terrain-geometry-readiness.md");
    string jsonPath = Path.Combine(reportRoot, "terrain-geometry-readiness.json");
    StringBuilder builder = new();
    builder.AppendLine("# Cross-Level Terrain Geometry Readiness");
    builder.AppendLine();
    builder.AppendLine("This generated report checks whether captured levels can save a real terrain height edit and produce patch-plan bytes against their own runtime scene sectors.");
    builder.AppendLine();
    builder.AppendLine("| Level | Status | Face | Edit | Source sectors | Patches | Collision | Notes |");
    builder.AppendLine("|---|---|---|---|---:|---:|---|---|");
    foreach (CrossLevelTerrainGeometryRow row in rows)
    {
        string sectors = row.SourceSectorCount <= 0 ? "0/0" : $"{row.MatchedSectorCount}/{row.SourceSectorCount}";
        string collision = row.HadCollisionMatch
            ? row.HasCollisionPatch ? "matched + patched" : "matched, not patched"
            : "not matched";
        builder.AppendLine($"| {EscapeMarkdownCell(row.LevelName)} | {EscapeMarkdownCell(row.Status)} | {EscapeMarkdownCell(row.RuntimeKey)} | {EscapeMarkdownCell(row.Edit)} | {sectors} | {row.PatchCount} | {EscapeMarkdownCell(collision)} | {EscapeMarkdownCell(row.Notes)} |");
    }

    File.WriteAllText(reportPath, builder.ToString());
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
    {
        GeneratedAtUtc = DateTimeOffset.UtcNow,
        Rows = rows
    }, new JsonSerializerOptions { WriteIndented = true }));
}

async Task ReportCrossLevelTerrainSideWallPatchPlan()
{
    string[] levelKeys = catalog.Levels
        .Select(level => level.Key)
        .ToArray();
    string reportRoot = Path.Combine(workspace.RootPath, "_local", "smoke", "cross-level-terrain-side-walls");
    Directory.CreateDirectory(reportRoot);
    List<CrossLevelTerrainSideWallRow> rows = new();
    if (!File.Exists(sourceImage))
    {
            rows.Add(new CrossLevelTerrainSideWallRow("all", "All captured levels", "skipped", "source image not found", -1, -1, -1, 0, 0, 0, 0, -1, 0, -1, -1, 0, -1, 0, 0, 0, 0, 0, 0, 0, 0, 0, "Import a source BIN/CUE before running terrain side-wall smoke."));
        WriteCrossLevelTerrainSideWallReport(reportRoot, rows);
        Console.WriteLine("Cross-level terrain side-wall patch plans: source image not found; skipping.");
        return;
    }

    foreach (string levelKey in levelKeys)
    {
        LevelDefinition? level = catalog.FindByKey(levelKey);
        string overlayPath = Path.Combine(workspace.RootPath, "editor-cache", $"{levelKey}-runtime-scene-editor-overlay.json");
        string ramPath = TerrainPatchDataLocator.FindRamDump(workspace, levelKey);
        if (level == null)
        {
            rows.Add(new CrossLevelTerrainSideWallRow(levelKey, level?.DisplayName ?? levelKey, "skipped", "missing overlay", -1, -1, -1, 0, 0, 0, 0, -1, 0, -1, -1, 0, -1, 0, 0, 0, 0, 0, 0, 0, 0, 0, "Build or load this level's terrain overlay before proving raised/lowered terrain side walls."));
            continue;
        }

        string tempRoot = Path.Combine(reportRoot, level.Key);
        Directory.CreateDirectory(tempRoot);
        SmokeTerrainGeometry? geometryInfo = await TryLoadTerrainGeometryForSmoke(level, overlayPath, ramPath, tempRoot);
        if (geometryInfo == null)
        {
            rows.Add(new CrossLevelTerrainSideWallRow(level.Key, level.DisplayName, "skipped", "missing overlay", -1, -1, -1, 0, 0, 0, 0, -1, 0, -1, -1, 0, -1, 0, 0, 0, 0, 0, 0, 0, 0, 0, "Build/open this level's terrain map or select a BIN/CUE so the source-derived terrain map can be recovered."));
            continue;
        }

        GeometryCandidate geometry = geometryInfo.Geometry;
        ramPath = geometryInfo.RamPath;
        string sourceSearchPath = Path.Combine(tempRoot, $"{level.Key}-runtime-terrain-source-search-native.json");
        TerrainSmokeSourceSearch? sourceSearchInfo = await TryBuildTerrainSourceSearchForSmoke(level, geometry, ramPath, sourceSearchPath);
        if (sourceSearchInfo == null)
        {
            rows.Add(new CrossLevelTerrainSideWallRow(level.Key, level.DisplayName, "skipped", "missing RAM capture or source-derived overlay", -1, -1, -1, 0, 0, 0, 0, -1, 0, -1, -1, 0, -1, 0, 0, 0, 0, 0, 0, 0, 0, 0, "Capture RAM or rebuild the cache with source-derived terrain offsets before proving raised/lowered terrain side walls."));
            continue;
        }

        TerrainSourceSearchResult sourceSearch = sourceSearchInfo.Result;
        HashSet<string> sourceMappedKeys = ReadTerrainSourceKeys(sourceSearchPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> collisionTriangleKeys = sourceSearchInfo.HasRamCapture
            ? SpyroCollisionDecoder.Decode(await File.ReadAllBytesAsync(ramPath)).Triangles
                .Select(CollisionTriangleKey)
                .ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        SourceDerivedCollisionProbeReport? sideWallCollisionProbe = sourceSearchInfo.SourceDerived
            ? await ReportSourceDerivedCollisionProbe(level, geometry, tempRoot, sourceSearchPath, sourceSearch)
            : null;
        HashSet<string> sourceDerivedCollisionKeys = sourceSearchInfo.SourceDerived
            ? (sideWallCollisionProbe?.LookupReferencedRuntimeKeys.Count > 0
                ? sideWallCollisionProbe.LookupReferencedRuntimeKeys
                : sideWallCollisionProbe?.HitRuntimeKeys ?? Array.Empty<string>())
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<int, TerrainSectorSourceLayout> sectorSourceLayouts = BuildTerrainSectorSourceLayouts(sourceSearch.Report);
        Dictionary<int, int> sectorAppendSlack = BuildTerrainSectorAppendSlack(sourceSearch.Report);
        SmokeAssetSubfileInfo? modelSubfileInfo = TryGetSmokeAssetSubfileInfo(wadAnalysis, level, 1);
        long chainTailEndWadOffset = sectorSourceLayouts.Values
            .Select(layout => layout.SectorEndWadOffset)
            .DefaultIfEmpty(-1)
            .Max();
        long modelTailSlackBytes = modelSubfileInfo != null && chainTailEndWadOffset >= 0
            ? Math.Max(0, modelSubfileInfo.AbsoluteWadOffset + modelSubfileInfo.SubfileSize - chainTailEndWadOffset)
            : 0;
        int[] textureIds = geometry.Polygons
            .Select(face => face.TextureId)
            .Where(textureId => textureId >= 0)
            .Distinct()
            .Order()
            .ToArray();
        int sideWallCandidateLimit = sourceSearchInfo.SourceDerived ? 384 : 24;
        IOrderedEnumerable<TerrainPolygon> orderedCandidates = geometry.Polygons
            .Where(face => IsStructurePatchCandidate(face, sourceMappedKeys) &&
                string.Equals(face.Detail, "hp", StringComparison.OrdinalIgnoreCase) &&
                (HasCollisionTriangleMatch(face, collisionTriangleKeys) || sourceDerivedCollisionKeys.Contains(face.RuntimeKey)))
            .OrderByDescending(face => sectorAppendSlack.TryGetValue(face.SectorOffset, out int slack) ? slack : -1);
        orderedCandidates = sourceSearchInfo.SourceDerived
            ? orderedCandidates.ThenByDescending(TerrainFaceAreaScore)
            : orderedCandidates.ThenByDescending(face => face.Points.Count);
        List<TerrainPolygon> candidates = orderedCandidates
            .ThenByDescending(face => face.Points.Count)
            .ThenBy(face => face.SectorIndex)
            .ThenBy(face => face.FaceIndex)
            .Take(sideWallCandidateLimit)
            .ToList();
        if (candidates.Count == 0)
        {
            rows.Add(new CrossLevelTerrainSideWallRow(level.Key, level.DisplayName, "skipped", "no collision-matched source face found", -1, -1, -1, sourceSearch.Report.SectorCount, sourceSearch.Report.MatchedSectorCount, 0, 0, -1, 0, -1, modelSubfileInfo?.AbsoluteWadOffset ?? -1, modelSubfileInfo?.SubfileSize ?? 0, chainTailEndWadOffset, modelTailSlackBytes, 0, 0, 0, 0, 0, 0, 0, 0, "Need a source-mapped high-detail face with matching collision before creating solid side walls."));
            continue;
        }

        CrossLevelTerrainSideWallRow? bestRow = null;
        int bestRank = -1;
        int bestSolidEdgeCount = -1;
        int bestDeficitBytes = int.MaxValue;
        int tested = 0;
        foreach (TerrainPolygon face in candidates)
        {
            tested++;
            int originalTextureId = face.TextureId;
            int targetTextureId = textureIds
                .Where(textureId => textureId != originalTextureId)
                .DefaultIfEmpty(-1)
                .First();
            if (targetTextureId < 0)
                continue;

            string editPath = Path.Combine(tempRoot, $"{level.Key}-side-wall-{SanitizeFileName(face.RuntimeKey)}-terrain-edits.json");
            face.ApplyTerrainDeltaZ(18f);
            face.ApplyTextureOverride(targetTextureId);
            int editCount = await TerrainEditStore.SaveAsync(editPath, [face], $"{level.DisplayName} side-wall smoke");
            face.ResetTerrainEdit();
            if (editCount != 1)
                throw new InvalidOperationException($"{level.DisplayName} side-wall smoke should save one terrain edit for {face.RuntimeKey}.");

            TerrainPatchPlan plan = TerrainPatchExporter.BuildPlan(
                sourceImage,
                DiscImageLocator.FindCueForImage(sourceImage),
                Path.Combine(tempRoot, $"side-wall-{SanitizeFileName(face.RuntimeKey)}-smoke.bin"),
                Path.Combine(tempRoot, $"side-wall-{SanitizeFileName(face.RuntimeKey)}-smoke.cue"),
                level,
                sourceSearchInfo.PatchRamPath,
                sourceSearchPath,
                editPath,
                "");
            int exposedEdgeCount = plan.TerrainSideWalls.Sum(summary => summary.ExposedEdgeCount);
            int solidEdgeCount = plan.TerrainSideWalls.Sum(summary => summary.EmittedFaceCount);
            int collisionTriangleCount = plan.TerrainSideWalls.Sum(summary => summary.CollisionTriangleCount);
            int skippedEdgeCount = plan.TerrainSideWalls.Sum(summary => summary.SkippedEdgeCount);
            bool sideWallTextureCoverage = TryGetSideWallAppendedTextureCoverage(plan, solidEdgeCount, targetTextureId, out int texturedFaceCount);
            bool sideWallSummaryTextureCoverage = plan.TerrainSideWalls.Any(summary => summary.TextureIds.Contains(targetTextureId));
            bool fullCoverage = exposedEdgeCount > 0 &&
                solidEdgeCount == exposedEdgeCount &&
                collisionTriangleCount >= solidEdgeCount * 2 &&
                sideWallTextureCoverage &&
                sideWallSummaryTextureCoverage;
            bool partialCoverage = solidEdgeCount > 0;
            int rank = fullCoverage
                ? 4
                : partialCoverage && collisionTriangleCount > 0 && sideWallTextureCoverage
                    ? 3
                    : partialCoverage
                        ? 2
                        : plan.PatchCount > 0
                            ? 1
                            : 0;
            string status = fullCoverage
                ? "smoke-passed"
                : partialCoverage
                    ? "side-wall-partial"
                    : exposedEdgeCount > 0
                        ? "blocked"
                        : "top-only";
            string sideWallNotes = string.Join(" | ", plan.TerrainSideWalls
                .SelectMany(summary => summary.SkipReasons)
                .Concat(plan.SkippedEdits.Where(skip => skip.Contains("side wall", StringComparison.OrdinalIgnoreCase) || skip.Contains("side-wall", StringComparison.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3));
            if (string.IsNullOrWhiteSpace(sideWallNotes))
                sideWallNotes = fullCoverage
                    ? "All exposed raised/lowered edges received textured wall faces and collision triangles."
                    : partialCoverage
                        ? "Some exposed edges received wall faces; remaining edges need more safe slack or collision replacement capacity."
                        : FirstTerrainPlanSkip(plan);

            TerrainSectorSourceLayout? sourceLayout = sectorSourceLayouts.TryGetValue(face.SectorOffset, out TerrainSectorSourceLayout? layout)
                ? layout
                : null;
            int sectorSlack = sourceLayout?.SlackBytes ?? (sectorAppendSlack.TryGetValue(face.SectorOffset, out int slackBytes) ? slackBytes : 0);
            long shiftStartWadOffset = sourceLayout?.NextWadOffset ?? -1;
            int shiftSuffixSectorCount = shiftStartWadOffset >= 0
                ? sectorSourceLayouts.Values.Count(layout => layout.WadOffset >= shiftStartWadOffset)
                : 0;
            long shiftSuffixBytes = shiftStartWadOffset >= 0 && chainTailEndWadOffset >= shiftStartWadOffset
                ? chainTailEndWadOffset - shiftStartWadOffset
                : 0;
            int requiredAppendBytes = plan.TerrainSideWalls
                .Select(summary => summary.RequiredAppendBytes)
                .DefaultIfEmpty(0)
                .Max();
            CrossLevelTerrainSideWallRow row = new(
                level.Key,
                level.DisplayName,
                status,
                face.RuntimeKey,
                face.SectorOffset,
                originalTextureId,
                targetTextureId,
                sourceSearch.Report.SectorCount,
                sourceSearch.Report.MatchedSectorCount,
                sectorSlack,
                requiredAppendBytes,
                sourceLayout?.WadOffset ?? -1,
                sourceLayout?.SizeBytes ?? 0,
                sourceLayout?.NextWadOffset ?? -1,
                modelSubfileInfo?.AbsoluteWadOffset ?? -1,
                modelSubfileInfo?.SubfileSize ?? 0,
                chainTailEndWadOffset,
                modelTailSlackBytes,
                shiftSuffixSectorCount,
                shiftSuffixBytes,
                exposedEdgeCount,
                solidEdgeCount,
                collisionTriangleCount,
                texturedFaceCount,
                skippedEdgeCount,
                plan.PatchCount,
                sideWallNotes);
            int rowDeficitBytes = row.AppendDeficitBytes;
            if (rank > bestRank ||
                (rank == bestRank && row.SolidEdgeCount > bestSolidEdgeCount) ||
                (rank == bestRank && row.SolidEdgeCount == bestSolidEdgeCount && rowDeficitBytes < bestDeficitBytes))
            {
                bestRank = rank;
                bestSolidEdgeCount = row.SolidEdgeCount;
                bestDeficitBytes = rowDeficitBytes;
                bestRow = row;
            }

            if (fullCoverage)
                break;
        }

        if (bestRow == null)
        {
            rows.Add(new CrossLevelTerrainSideWallRow(
                level.Key,
                level.DisplayName,
                "skipped",
                "no alternate texture candidate",
                -1,
                -1,
                -1,
                sourceSearch.Report.SectorCount,
                sourceSearch.Report.MatchedSectorCount,
                0,
                0,
                -1,
                0,
                -1,
                modelSubfileInfo?.AbsoluteWadOffset ?? -1,
                modelSubfileInfo?.SubfileSize ?? 0,
                chainTailEndWadOffset,
                modelTailSlackBytes,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                "Need at least two texture IDs before proving textured side-wall inheritance."));
        }
        else
        {
            rows.Add(bestRow with
            {
                Notes = $"{bestRow.Notes} Checked {tested}/{candidates.Count} candidate(s)."
            });
        }
    }

    WriteCrossLevelTerrainSideWallReport(reportRoot, rows);
    int passed = rows.Count(row => string.Equals(row.Status, "smoke-passed", StringComparison.OrdinalIgnoreCase));
    int partial = rows.Count(row => string.Equals(row.Status, "side-wall-partial", StringComparison.OrdinalIgnoreCase));
    int topOnly = rows.Count(row => string.Equals(row.Status, "top-only", StringComparison.OrdinalIgnoreCase));
    int skipped = rows.Count(row => string.Equals(row.Status, "skipped", StringComparison.OrdinalIgnoreCase));
    int blocked = rows.Count(row => string.Equals(row.Status, "blocked", StringComparison.OrdinalIgnoreCase));
    Console.WriteLine($"Cross-level terrain side-wall patch plans: {passed} passed, {partial} partial, {topOnly} top-only, {skipped} skipped, {blocked} blocked, report={Path.Combine(reportRoot, "terrain-side-wall-readiness.md")}");
}

void WriteCrossLevelTerrainSideWallReport(string reportRoot, IReadOnlyList<CrossLevelTerrainSideWallRow> rows)
{
    string reportPath = Path.Combine(reportRoot, "terrain-side-wall-readiness.md");
    string jsonPath = Path.Combine(reportRoot, "terrain-side-wall-readiness.json");
    StringBuilder builder = new();
    builder.AppendLine("# Cross-Level Terrain Side-Wall Readiness");
    builder.AppendLine();
    builder.AppendLine("This generated report checks whether captured levels can turn a raised/lowered terrain edit into textured vertical side faces with collision, instead of leaving a visual hole under the edited top face.");
    builder.AppendLine();
    builder.AppendLine("| Level | Status | Face | Sector | Texture | Source sectors | Slack | Required | Deficit | Side walls | Collision | Texture coverage | Patches | Notes |");
    builder.AppendLine("|---|---|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---|");
    foreach (CrossLevelTerrainSideWallRow row in rows)
    {
        string sectors = row.SourceSectorCount <= 0 ? "0/0" : $"{row.MatchedSectorCount}/{row.SourceSectorCount}";
        string sector = row.SectorOffset >= 0 ? $"0x{row.SectorOffset:X}" : "";
        string texture = row.OriginalTextureId >= 0 && row.TargetTextureId >= 0 ? $"{row.OriginalTextureId}->{row.TargetTextureId}" : "";
        string sideWalls = row.ExposedEdgeCount > 0 ? $"{row.SolidEdgeCount}/{row.ExposedEdgeCount}" : "0/0";
        string textureCoverage = row.SolidEdgeCount > 0 ? $"{row.TexturedFaceCount}/{row.SolidEdgeCount}" : "0/0";
        builder.AppendLine($"| {EscapeMarkdownCell(row.LevelName)} | {EscapeMarkdownCell(row.Status)} | {EscapeMarkdownCell(row.RuntimeKey)} | {EscapeMarkdownCell(sector)} | {EscapeMarkdownCell(texture)} | {sectors} | {row.SectorSlackBytes} | {row.RequiredAppendBytes} | {row.AppendDeficitBytes} | {EscapeMarkdownCell(sideWalls)} | {row.CollisionTriangleCount} | {EscapeMarkdownCell(textureCoverage)} | {row.PatchCount} | {EscapeMarkdownCell(row.Notes)} |");
    }

    File.WriteAllText(reportPath, builder.ToString());
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
    {
        GeneratedAtUtc = DateTimeOffset.UtcNow,
        Rows = rows
    }, new JsonSerializerOptions { WriteIndented = true }));
    WriteCrossLevelTerrainSideWallExpansionTargetReport(reportRoot, rows);
}

void WriteCrossLevelTerrainSideWallExpansionTargetReport(string reportRoot, IReadOnlyList<CrossLevelTerrainSideWallRow> rows)
{
    string reportPath = Path.Combine(reportRoot, "terrain-side-wall-expansion-targets.md");
    string jsonPath = Path.Combine(reportRoot, "terrain-side-wall-expansion-targets.json");
    CrossLevelTerrainSideWallRow[] targets = rows
        .Where(row => row.NeedsSectorExpansion)
        .OrderBy(row => row.AppendDeficitBytes)
        .ThenBy(row => row.LevelName, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    StringBuilder builder = new();
    builder.AppendLine("# Terrain Side-Wall Expansion Targets");
    builder.AppendLine();
    builder.AppendLine("These raised/lowered terrain smoke cases need more bytes than the edited scene sector has immediately available. When the current result is smoke-passed, the exporter is using a bounded shift of later scene sectors inside the same model subfile to make room for textured side walls and collision.");
    builder.AppendLine();
    if (targets.Length == 0)
    {
        builder.AppendLine("No sector expansion targets were found in the current side-wall smoke run.");
    }
    else
    {
        builder.AppendLine("| Level | Face | Sector | Source WAD | Sector end | Next sector | Model subfile | Tail slack | Shift scope | Slack | Required | Deficit | Current result | Next step |");
        builder.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|---|");
        foreach (CrossLevelTerrainSideWallRow row in targets)
        {
            string sector = row.SectorOffset >= 0 ? $"0x{row.SectorOffset:X}" : "";
            string sourceWad = row.SourceWadOffset >= 0 ? $"0x{row.SourceWadOffset:X}" : "";
            string sectorEnd = row.SourceWadOffset >= 0 && row.SectorSizeBytes > 0 ? $"0x{row.SourceSectorEndWadOffset:X}" : "";
            string nextSector = row.NextSourceWadOffset >= 0 ? $"0x{row.NextSourceWadOffset:X}" : "";
            string modelSubfile = row.ModelSubfileWadOffset >= 0
                ? $"0x{row.ModelSubfileWadOffset:X}+0x{row.ModelSubfileRelativeOffset:X}/0x{row.ModelSubfileSize:X}"
                : "";
            string shiftScope = row.ShiftSuffixSectorCount > 0
                ? $"{row.ShiftSuffixSectorCount} sector(s), {row.ShiftSuffixBytes} byte(s)"
                : "";
            string nextStep = row.CanShiftWithinModelSubfile
                ? string.Equals(row.Status, "smoke-passed", StringComparison.OrdinalIgnoreCase)
                    ? $"Exporter shifts {row.ShiftSuffixSectorCount} later scene sector(s) by {row.AppendDeficitBytes} byte(s) inside this level's model data and emits the side walls."
                    : $"Shift {row.ShiftSuffixSectorCount} later scene sector(s) by {row.AppendDeficitBytes} byte(s) inside this level's model data, then retry side-wall emission."
                : row.NextSourceWadOffset >= 0 && row.SourceSectorEndWadOffset + row.RequiredAppendBytes > row.NextSourceWadOffset
                    ? $"Would overlap the next mapped scene sector by {row.AppendDeficitBytes} byte(s); relocate the scene-sector chain or whole model subfile before writing side walls."
                    : row.ModelSubfileWadOffset < 0
                        ? "Locate this level's model subfile before planning sector relocation."
                        : row.SectorSlackBytes <= 0
                            ? "Relocate or grow the packed scene sector before appending wall vertices/faces."
                            : $"Grow this scene sector by at least {row.AppendDeficitBytes} byte(s), then retry side-wall emission.";
            builder.AppendLine($"| {EscapeMarkdownCell(row.LevelName)} | {EscapeMarkdownCell(row.RuntimeKey)} | {EscapeMarkdownCell(sector)} | {EscapeMarkdownCell(sourceWad)} | {EscapeMarkdownCell(sectorEnd)} | {EscapeMarkdownCell(nextSector)} | {EscapeMarkdownCell(modelSubfile)} | {row.ModelTailSlackBytes} | {EscapeMarkdownCell(shiftScope)} | {row.SectorSlackBytes} | {row.RequiredAppendBytes} | {row.AppendDeficitBytes} | {EscapeMarkdownCell(row.Status)} | {EscapeMarkdownCell(nextStep)} |");
        }
    }

    File.WriteAllText(reportPath, builder.ToString());
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
    {
        GeneratedAtUtc = DateTimeOffset.UtcNow,
        Rows = targets
    }, new JsonSerializerOptions { WriteIndented = true }));
}

async Task ReportCrossLevelTerrainStructurePatchPlan()
{
    string[] levelKeys = catalog.Levels
        .Select(level => level.Key)
        .ToArray();
    string reportRoot = Path.Combine(workspace.RootPath, "_local", "smoke", "cross-level-terrain-structure");
    Directory.CreateDirectory(reportRoot);
    List<CrossLevelTerrainStructureRow> rows = new();
    if (!File.Exists(sourceImage))
    {
        rows.Add(new CrossLevelTerrainStructureRow("all", "All captured levels", "skipped", "source image not found", 0, false, false, "", 0, 0, 0, false, false, false, "Import a source BIN/CUE before running structural terrain patch smoke."));
        WriteCrossLevelTerrainStructureReport(reportRoot, rows);
        Console.WriteLine("Cross-level terrain structure patch plans: source image not found; skipping.");
        return;
    }

    foreach (string levelKey in levelKeys)
    {
        LevelDefinition? level = catalog.FindByKey(levelKey);
        string overlayPath = Path.Combine(workspace.RootPath, "editor-cache", $"{levelKey}-runtime-scene-editor-overlay.json");
        string ramPath = TerrainPatchDataLocator.FindRamDump(workspace, levelKey);
        if (level == null)
        {
            rows.Add(new CrossLevelTerrainStructureRow(levelKey, level?.DisplayName ?? levelKey, "skipped", "missing overlay", 0, false, false, "", 0, 0, 0, false, false, false, "Build or load this level's terrain overlay before proving remove/add terrain structure patches."));
            continue;
        }

        string tempRoot = Path.Combine(reportRoot, level.Key);
        Directory.CreateDirectory(tempRoot);
        SmokeTerrainGeometry? geometryInfo = await TryLoadTerrainGeometryForSmoke(level, overlayPath, ramPath, tempRoot);
        if (geometryInfo == null)
        {
            rows.Add(new CrossLevelTerrainStructureRow(level.Key, level.DisplayName, "skipped", "missing overlay", 0, false, false, "", 0, 0, 0, false, false, false, "Build/open this level's terrain map or select a BIN/CUE so the source-derived terrain map can be recovered."));
            continue;
        }

        GeometryCandidate geometry = geometryInfo.Geometry;
        ramPath = geometryInfo.RamPath;
        string sourceSearchPath = Path.Combine(tempRoot, $"{level.Key}-runtime-terrain-source-search-native.json");
        TerrainSmokeSourceSearch? sourceSearchInfo = await TryBuildTerrainSourceSearchForSmoke(level, geometry, ramPath, sourceSearchPath);
        if (sourceSearchInfo == null)
        {
            rows.Add(new CrossLevelTerrainStructureRow(level.Key, level.DisplayName, "skipped", "missing RAM capture or source-derived overlay", 0, false, false, "", 0, 0, 0, false, false, false, "Capture RAM or rebuild the cache with source-derived terrain offsets before proving remove/add terrain structure patches."));
            continue;
        }

        TerrainSourceSearchResult sourceSearch = sourceSearchInfo.Result;
        HashSet<string> sourceMappedKeys = ReadTerrainSourceKeys(sourceSearchPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> collisionTriangleKeys = sourceSearchInfo.HasRamCapture
            ? SpyroCollisionDecoder.Decode(await File.ReadAllBytesAsync(ramPath)).Triangles
                .Select(CollisionTriangleKey)
                .ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> sourceDerivedCollisionKeys = sourceSearchInfo.SourceDerived
            ? (await ReportSourceDerivedCollisionProbe(level, geometry, tempRoot, sourceSearchPath, sourceSearch))?.HitRuntimeKeys
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<int, int> sectorAppendSlack = BuildTerrainSectorAppendSlack(sourceSearch.Report);

        TerrainPolygon? removeFace = geometry.Polygons
            .Where(face => IsStructurePatchCandidate(face, sourceMappedKeys))
            .OrderByDescending(face => HasCollisionTriangleMatch(face, collisionTriangleKeys) || sourceDerivedCollisionKeys.Contains(face.RuntimeKey))
            .ThenBy(face => face.SectorIndex)
            .ThenBy(face => face.FaceIndex)
            .FirstOrDefault();
        if (removeFace == null)
        {
            rows.Add(new CrossLevelTerrainStructureRow(
                level.Key,
                level.DisplayName,
                "skipped",
                "no source-mapped terrain face found",
                0,
                false,
                false,
                "",
                0,
                0,
                0,
                false,
                false,
                false,
                $"Source search matched {sourceSearch.Report.MatchedSectorCount}/{sourceSearch.Report.SectorCount} sectors, but no editable face key was available."));
            continue;
        }

        string removePath = Path.Combine(tempRoot, $"{level.Key}-remove-terrain-edits.json");
        removeFace.StageTerrainRemoval();
        int removeEditCount = await TerrainEditStore.SaveAsync(removePath, [removeFace], $"{level.DisplayName} remove terrain smoke");
        removeFace.ResetTerrainEdit();
        if (removeEditCount != 1)
            throw new InvalidOperationException($"{level.DisplayName} remove-face structural smoke should save one edit.");

        TerrainPatchPlan removePlan = TerrainPatchExporter.BuildPlan(
            sourceImage,
            DiscImageLocator.FindCueForImage(sourceImage),
            Path.Combine(tempRoot, "remove-terrain-smoke.bin"),
            Path.Combine(tempRoot, "remove-terrain-smoke.cue"),
            level,
            sourceSearchInfo.PatchRamPath,
            sourceSearchPath,
            removePath,
            "");
        bool hasRemoveVisualPatch = removePlan.Patches.Any(patch =>
            patch.RuntimeKey.Equals(removeFace.RuntimeKey, StringComparison.OrdinalIgnoreCase) &&
            patch.Kind.StartsWith("terrain-face-degenerate-", StringComparison.OrdinalIgnoreCase));
        bool hasRemoveCollisionPatch = removePlan.Patches.Any(patch =>
            patch.RuntimeKey.Equals(removeFace.RuntimeKey, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(patch.Kind, "collision-triangle-degenerate", StringComparison.OrdinalIgnoreCase));
        bool removeHadCollision = HasCollisionTriangleMatch(removeFace, collisionTriangleKeys) ||
            sourceDerivedCollisionKeys.Contains(removeFace.RuntimeKey) ||
            hasRemoveCollisionPatch;

        int addCopyCandidateLimit = sourceSearchInfo.SourceDerived ? 48 : 8;
        List<TerrainStructureAddCandidate> addCandidates = geometry.Polygons
            .Where(face => IsStructurePatchCandidate(face, sourceMappedKeys) &&
                string.Equals(face.Detail, "hp", StringComparison.OrdinalIgnoreCase) &&
                sectorAppendSlack.ContainsKey(face.SectorOffset))
            .Select(face => new TerrainStructureAddCandidate(
                Face: face,
                Slack: sectorAppendSlack[face.SectorOffset],
                Required: RequiredIndependentAddCopySlack(face),
                HasCollision: HasCollisionTriangleMatch(face, collisionTriangleKeys) || sourceDerivedCollisionKeys.Contains(face.RuntimeKey)))
            .OrderByDescending(item => item.HasCollision)
            .ThenByDescending(item => item.Slack >= item.Required)
            .ThenByDescending(item => item.Slack)
            .ThenBy(item => item.Face.SectorIndex)
            .ThenBy(item => item.Face.FaceIndex)
            .Take(addCopyCandidateLimit)
            .ToList();

        string addRuntimeKey = "";
        int addSlack = 0;
        int addRequiredSlack = 0;
        int addPatchCount = 0;
        bool addHasIndependentVertices = false;
        bool addHasCollisionPatch = false;
        bool addHasFallbackAppend = false;
        int addCandidatesTested = 0;
        string addNotes;
        if (addCandidates.Count == 0)
        {
            addNotes = sourceSearchInfo.SourceDerived
                ? "Source-derived structure smoke found no high-detail sector with a source-mapped add-copy candidate."
                : "No high-detail source-mapped terrain face was available for a visible add-copy test.";
        }
        else
        {
            TerrainPatchPlan? bestAddPlan = null;
            string bestNotes = "";
            foreach (var candidate in addCandidates)
            {
                addCandidatesTested++;
                TerrainPolygon addFace = candidate.Face;
                string addPath = Path.Combine(tempRoot, $"{level.Key}-add-copy-{SanitizeFileName(addFace.RuntimeKey)}-terrain-edits.json");
                float[] addCopyDeltas = addFace.OriginalZValues.Select((_, index) => 10f + (index * 3f)).ToArray();
                Vector2f[] addCopyXYDeltas = addFace.OriginalPoints.Select(_ => new Vector2f(64f, 24f)).ToArray();
                addFace.ApplyTerrainVertexDeltas(addCopyDeltas);
                addFace.ApplyTerrainVertexXYDeltas(addCopyXYDeltas);
                addFace.StageTerrainAddClone();
                int addEditCount = await TerrainEditStore.SaveAsync(addPath, [addFace], $"{level.DisplayName} add-copy terrain smoke");
                addFace.ResetTerrainEdit();
                if (addEditCount != 1)
                    throw new InvalidOperationException($"{level.DisplayName} add-copy structural smoke should save one edit.");

                TerrainPatchPlan addPlan = TerrainPatchExporter.BuildPlan(
                    sourceImage,
                    DiscImageLocator.FindCueForImage(sourceImage),
                    Path.Combine(tempRoot, $"add-copy-{SanitizeFileName(addFace.RuntimeKey)}-terrain-smoke.bin"),
                    Path.Combine(tempRoot, $"add-copy-{SanitizeFileName(addFace.RuntimeKey)}-terrain-smoke.cue"),
                    level,
                    sourceSearchInfo.PatchRamPath,
                    sourceSearchPath,
                    addPath,
                    "");
                bool candidateHasVertexCountPatch = addPlan.Patches.Any(patch => string.Equals(patch.Kind, "terrain-vertex-count-hp", StringComparison.OrdinalIgnoreCase));
                bool candidateHasFaceCountPatch = addPlan.Patches.Any(patch => string.Equals(patch.Kind, "terrain-face-count-hp", StringComparison.OrdinalIgnoreCase));
                bool candidateHasIndependentVertices = candidateHasVertexCountPatch &&
                    candidateHasFaceCountPatch &&
                    addPlan.Patches.Any(patch => string.Equals(patch.Kind, "terrain-sector-repack-hp-add-copy", StringComparison.OrdinalIgnoreCase));
                bool candidateHasCollisionPatch = addPlan.Patches.Any(patch => string.Equals(patch.Kind, "collision-triangle-add-copy", StringComparison.OrdinalIgnoreCase));
                bool candidateHasFallbackAppend = addPlan.Patches.Any(patch => string.Equals(patch.Kind, "terrain-face-append-hp", StringComparison.OrdinalIgnoreCase));
                int candidateRank = AddCopyCandidateRank(candidateHasIndependentVertices, candidateHasCollisionPatch, candidateHasFallbackAppend, addPlan.PatchCount);
                int bestRank = AddCopyCandidateRank(addHasIndependentVertices, addHasCollisionPatch, addHasFallbackAppend, addPatchCount);
                if (bestAddPlan == null || candidateRank > bestRank)
                {
                    addRuntimeKey = addFace.RuntimeKey;
                    addSlack = candidate.Slack;
                    addRequiredSlack = candidate.Required;
                    addPatchCount = addPlan.PatchCount;
                    addHasIndependentVertices = candidateHasIndependentVertices;
                    addHasCollisionPatch = candidateHasCollisionPatch;
                    addHasFallbackAppend = candidateHasFallbackAppend;
                    bestAddPlan = addPlan;
                    bestNotes = candidateHasIndependentVertices
                        ? candidateHasCollisionPatch
                            ? "Add-copy patches independent cloned vertices, a copied face, and copied collision."
                            : $"Add-copy patches independent visible cloned vertices, but collision was not added. {FirstTerrainPlanSkip(addPlan)}"
                        : $"Add-copy did not produce an independent structural patch. {FirstTerrainPlanSkip(addPlan)}";
                }

                if (candidateHasIndependentVertices && candidateHasCollisionPatch)
                    break;
            }

            addNotes = string.IsNullOrWhiteSpace(bestNotes)
                ? $"No add-copy candidate produced a structural patch after checking {addCandidatesTested}/{addCandidates.Count} candidate(s). {FirstTerrainPlanSkip(bestAddPlan!)}"
                : $"{bestNotes} Checked {addCandidatesTested}/{addCandidates.Count} add-copy candidate(s).";
        }

        bool removeOk = hasRemoveVisualPatch && (!removeHadCollision || hasRemoveCollisionPatch);
        bool addFull = addHasIndependentVertices && addHasCollisionPatch;
        bool addPartial = addHasIndependentVertices || addHasFallbackAppend;
        string status = removeOk && addFull
            ? "smoke-passed"
            : removeOk && addPartial
                ? "structure-partial"
                : hasRemoveVisualPatch
                    ? "remove-only"
                    : "blocked";
        string removeNotes = hasRemoveVisualPatch
            ? hasRemoveCollisionPatch
                ? sourceSearchInfo.SourceDerived
                    ? "Remove-face patches source-derived visible geometry and exact collision."
                    : "Remove-face patches visible geometry and matched collision."
                : removeHadCollision
                    ? $"Remove-face patches visible geometry only. {FirstTerrainPlanSkip(removePlan)}"
                    : sourceSearchInfo.SourceDerived
                        ? "Remove-face patches source-derived visible geometry only; exact collision source bytes were not found for this face."
                        : "Remove-face patches visible geometry; no matching collision triangle was selected."
            : $"Remove-face did not produce a visible degenerate patch. {FirstTerrainPlanSkip(removePlan)}";
        rows.Add(new CrossLevelTerrainStructureRow(
            level.Key,
            level.DisplayName,
            status,
            removeFace.RuntimeKey,
            removePlan.PatchCount,
            removeHadCollision,
            hasRemoveCollisionPatch,
            addRuntimeKey,
            addSlack,
            addRequiredSlack,
            addPatchCount,
            addHasIndependentVertices,
            addHasCollisionPatch,
            addHasFallbackAppend,
            $"{removeNotes} {addNotes}"));
    }

    WriteCrossLevelTerrainStructureReport(reportRoot, rows);
    int passed = rows.Count(row => string.Equals(row.Status, "smoke-passed", StringComparison.OrdinalIgnoreCase));
    int partial = rows.Count(row => string.Equals(row.Status, "structure-partial", StringComparison.OrdinalIgnoreCase));
    int removeOnly = rows.Count(row => string.Equals(row.Status, "remove-only", StringComparison.OrdinalIgnoreCase));
    int skipped = rows.Count(row => string.Equals(row.Status, "skipped", StringComparison.OrdinalIgnoreCase));
    int blocked = rows.Count(row => string.Equals(row.Status, "blocked", StringComparison.OrdinalIgnoreCase));
    Console.WriteLine($"Cross-level terrain structure patch plans: {passed} passed, {partial} partial, {removeOnly} remove-only, {skipped} skipped, {blocked} blocked, report={Path.Combine(reportRoot, "terrain-structure-readiness.md")}");
}

static bool IsStructurePatchCandidate(TerrainPolygon face, IReadOnlySet<string> sourceMappedKeys)
{
    return face.SectorOffset >= 0 &&
        face.FaceOffset >= 0 &&
        face.VertexIndexes.Count > 0 &&
        sourceMappedKeys.Contains(face.RuntimeKey);
}

void WriteCrossLevelTerrainStructureReport(string reportRoot, IReadOnlyList<CrossLevelTerrainStructureRow> rows)
{
    string reportPath = Path.Combine(reportRoot, "terrain-structure-readiness.md");
    string jsonPath = Path.Combine(reportRoot, "terrain-structure-readiness.json");
    StringBuilder builder = new();
    builder.AppendLine("# Cross-Level Terrain Structure Readiness");
    builder.AppendLine();
    builder.AppendLine("This generated report checks whether captured levels can stage native remove-face terrain patches and added-copy terrain patches against their own runtime scene sectors.");
    builder.AppendLine();
    builder.AppendLine("| Level | Status | Remove face | Remove patches | Remove collision | Add face | Add slack | Add patches | Add mode | Notes |");
    builder.AppendLine("|---|---|---|---:|---|---|---:|---:|---|---|");
    foreach (CrossLevelTerrainStructureRow row in rows)
    {
        string removeCollision = row.RemoveHadCollision
            ? row.RemoveHasCollisionPatch ? "matched + patched" : "matched, not patched"
            : "not matched";
        string addSlack = row.AddRequiredSlackBytes > 0 ? $"{row.AddSlackBytes}/{row.AddRequiredSlackBytes}" : "";
        string addMode = row.AddHasIndependentVertices
            ? row.AddHasCollisionPatch ? "independent + collision" : "independent visual"
            : row.AddHasFallbackAppend ? "append-only visual" : "";
        builder.AppendLine($"| {EscapeMarkdownCell(row.LevelName)} | {EscapeMarkdownCell(row.Status)} | {EscapeMarkdownCell(row.RemoveRuntimeKey)} | {row.RemovePatchCount} | {EscapeMarkdownCell(removeCollision)} | {EscapeMarkdownCell(row.AddRuntimeKey)} | {EscapeMarkdownCell(addSlack)} | {row.AddPatchCount} | {EscapeMarkdownCell(addMode)} | {EscapeMarkdownCell(row.Notes)} |");
    }

    File.WriteAllText(reportPath, builder.ToString());
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
    {
        GeneratedAtUtc = DateTimeOffset.UtcNow,
        Rows = rows
    }, new JsonSerializerOptions { WriteIndented = true }));
}

async Task WriteSmokePngAsync(string path, int width, int height)
{
    using MemoryStream raw = new();
    for (int y = 0; y < height; y++)
    {
        raw.WriteByte(0);
        for (int x = 0; x < width; x++)
        {
            raw.WriteByte((byte)(32 + ((x * 160) / Math.Max(1, width - 1))));
            raw.WriteByte((byte)(64 + ((y * 140) / Math.Max(1, height - 1))));
            raw.WriteByte((byte)(180 - ((x * 80) / Math.Max(1, width - 1))));
            raw.WriteByte(255);
        }
    }

    using MemoryStream compressed = new();
    await using (ZLibStream zlib = new(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        await zlib.WriteAsync(raw.ToArray());

    using MemoryStream png = new();
    png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
    WritePngChunk(png, "IHDR", BuildPngHeader(width, height));
    WritePngChunk(png, "IDAT", compressed.ToArray());
    WritePngChunk(png, "IEND", []);
    await File.WriteAllBytesAsync(path, png.ToArray());
}

async Task WritePaletteSwatchPngAsync(string path, params ColorRgba[] colors)
{
    const int width = 48;
    const int height = 16;
    using MemoryStream raw = new();
    for (int y = 0; y < height; y++)
    {
        raw.WriteByte(0);
        for (int x = 0; x < width; x++)
        {
            int swatch = x / 12;
            ColorRgba color = swatch == 0 || colors.Length == 0
                ? ColorRgba.FromRgb(255, 255, 255)
                : colors[Math.Clamp(swatch - 1, 0, colors.Length - 1)];
            raw.WriteByte(color.R);
            raw.WriteByte(color.G);
            raw.WriteByte(color.B);
            raw.WriteByte(255);
        }
    }

    using MemoryStream compressed = new();
    await using (ZLibStream zlib = new(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        await zlib.WriteAsync(raw.ToArray());

    using MemoryStream png = new();
    png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
    WritePngChunk(png, "IHDR", BuildPngHeader(width, height));
    WritePngChunk(png, "IDAT", compressed.ToArray());
    WritePngChunk(png, "IEND", []);
    await File.WriteAllBytesAsync(path, png.ToArray());
}

byte[] BuildPngHeader(int width, int height)
{
    byte[] header = new byte[13];
    WriteBigEndian(header, 0, width);
    WriteBigEndian(header, 4, height);
    header[8] = 8;
    header[9] = 6;
    return header;
}

void WritePngChunk(Stream stream, string type, byte[] data)
{
    byte[] typeBytes = Encoding.ASCII.GetBytes(type);
    byte[] length = new byte[4];
    WriteBigEndian(length, 0, data.Length);
    stream.Write(length);
    stream.Write(typeBytes);
    stream.Write(data);
    uint crc = Crc32(typeBytes, data);
    byte[] crcBytes = new byte[4];
    WriteBigEndian(crcBytes, 0, unchecked((int)crc));
    stream.Write(crcBytes);
}

void WriteBigEndian(byte[] bytes, int offset, int value)
{
    bytes[offset] = (byte)((value >> 24) & 0xFF);
    bytes[offset + 1] = (byte)((value >> 16) & 0xFF);
    bytes[offset + 2] = (byte)((value >> 8) & 0xFF);
    bytes[offset + 3] = (byte)(value & 0xFF);
}

uint Crc32(byte[] typeBytes, byte[] data)
{
    uint crc = 0xFFFFFFFFu;
    foreach (byte value in typeBytes.Concat(data))
    {
        crc ^= value;
        for (int i = 0; i < 8; i++)
            crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
    }

    return crc ^ 0xFFFFFFFFu;
}

Dictionary<string, List<TerrainPolygon>> BuildVisualTriangleMap(GeometryCandidate geometry)
{
    Dictionary<string, List<TerrainPolygon>> result = new(StringComparer.Ordinal);
    foreach (TerrainPolygon polygon in geometry.Polygons)
    {
        if (polygon.Points.Count < 3 || polygon.ZValues.Length < polygon.Points.Count)
            continue;

        for (int i = 1; i < polygon.Points.Count - 1; i++)
        {
            string key = VisualTriangleKey(polygon, 0, i, i + 1);
            if (!result.TryGetValue(key, out List<TerrainPolygon>? faces))
            {
                faces = new List<TerrainPolygon>();
                result[key] = faces;
            }

            faces.Add(polygon);
        }
    }

    return result;
}

bool HasCollisionTriangleMatch(TerrainPolygon polygon, IReadOnlySet<string> collisionTriangleKeys)
{
    if (polygon.Points.Count < 3 || polygon.ZValues.Length < polygon.Points.Count || collisionTriangleKeys.Count == 0)
        return false;

    for (int i = 1; i < polygon.Points.Count - 1; i++)
    {
        if (collisionTriangleKeys.Contains(VisualTriangleKey(polygon, 0, i, i + 1)))
            return true;
    }

    return false;
}

double TerrainFaceAreaScore(TerrainPolygon polygon)
{
    if (polygon.Points.Count < 3)
        return 0;

    double area = 0;
    for (int i = 0; i < polygon.Points.Count; i++)
    {
        Vector2f a = polygon.Points[i];
        Vector2f b = polygon.Points[(i + 1) % polygon.Points.Count];
        area += ((double)a.X * b.Y) - ((double)b.X * a.Y);
    }

    return Math.Abs(area) * 0.5;
}

int FindFirstCollisionMatchedVertexIndex(TerrainPolygon polygon, IReadOnlySet<string> collisionTriangleKeys)
{
    if (polygon.Points.Count < 3 || polygon.ZValues.Length < polygon.Points.Count || collisionTriangleKeys.Count == 0)
        return -1;

    for (int i = 1; i < polygon.Points.Count - 1; i++)
    {
        if (collisionTriangleKeys.Contains(VisualTriangleKey(polygon, 0, i, i + 1)))
            return 0;
    }

    return -1;
}

string VisualTriangleKey(TerrainPolygon polygon, int a, int b, int c)
{
    return string.Join("|", new[]
        {
            VisualPointKey(polygon.Points[a].X, polygon.Points[a].Y, polygon.ZValues[a]),
            VisualPointKey(polygon.Points[b].X, polygon.Points[b].Y, polygon.ZValues[b]),
            VisualPointKey(polygon.Points[c].X, polygon.Points[c].Y, polygon.ZValues[c])
        }
        .OrderBy(value => value, StringComparer.Ordinal));
}

string CollisionTriangleKey(SpyroCollisionTriangle triangle)
{
    return string.Join("|", triangle.Points
        .Select(point => CollisionPointKey(point.X, point.Y, point.Z))
        .OrderBy(value => value, StringComparer.Ordinal));
}

string VisualPointKey(float x, float y, float z)
{
    return $"{MathF.Round(x)},{MathF.Round(y)},{MathF.Round(z)}";
}

string CollisionPointKey(int x, int y, int z)
{
    return $"{x},{y},{z}";
}

IEnumerable<(CollisionWordTriple Triple, string PointKey)> EnumerateSourceDerivedCollisionPatterns(TerrainPolygon polygon, int a, int b, int c)
{
    SmokeCollisionPoint[] points =
    [
        new SmokeCollisionPoint((int)MathF.Round(polygon.Points[a].X), (int)MathF.Round(polygon.Points[a].Y), (int)MathF.Round(polygon.ZValues[a])),
        new SmokeCollisionPoint((int)MathF.Round(polygon.Points[b].X), (int)MathF.Round(polygon.Points[b].Y), (int)MathF.Round(polygon.ZValues[b])),
        new SmokeCollisionPoint((int)MathF.Round(polygon.Points[c].X), (int)MathF.Round(polygon.Points[c].Y), (int)MathF.Round(polygon.ZValues[c]))
    ];

    foreach (int[] order in SourceDerivedCollisionPointOrders)
    {
        foreach (uint zFlags in SourceDerivedCollisionZFlags)
        {
            if (TryBuildSourceDerivedCollisionWords(points, order, zFlags, out CollisionWordTriple triple, out string pointKey))
                yield return (triple, pointKey);
        }
    }
}

bool TryBuildSourceDerivedCollisionWords(
    IReadOnlyList<SmokeCollisionPoint> points,
    IReadOnlyList<int> order,
    uint zFlags,
    out CollisionWordTriple triple,
    out string pointKey)
{
    triple = default;
    pointKey = "";
    if (points.Count < 3 || order.Count < 3)
        return false;

    SmokeCollisionPoint p1 = points[order[0]];
    SmokeCollisionPoint p2 = points[order[1]];
    SmokeCollisionPoint p3 = points[order[2]];
    if (!TryEncodeSourceDerivedSigned9(p2.X - p1.X, out uint p2Dx) ||
        !TryEncodeSourceDerivedSigned9(p3.X - p1.X, out uint p3Dx) ||
        !TryEncodeSourceDerivedSigned9(p2.Y - p1.Y, out uint p2Dy) ||
        !TryEncodeSourceDerivedSigned9(p3.Y - p1.Y, out uint p3Dy))
    {
        return false;
    }

    int p2Dz = p2.Z - p1.Z;
    int p3Dz = p3.Z - p1.Z;
    if (p1.X < 0 || p1.X > 0x3FFF || p1.Y < 0 || p1.Y > 0x3FFF || p1.Z < 0 || p1.Z > 0x3FFF ||
        p2Dz < 0 || p2Dz > 0xFF || p3Dz < 0 || p3Dz > 0xFF)
    {
        return false;
    }

    uint xWord = (uint)(p1.X & 0x3FFF) | (p2Dx << 14) | (p3Dx << 23);
    uint yWord = (uint)(p1.Y & 0x3FFF) | (p2Dy << 14) | (p3Dy << 23);
    uint zWord = (zFlags & 0x0000C000u)
        | (uint)(p1.Z & 0x3FFF)
        | ((uint)p2Dz << 16)
        | ((uint)p3Dz << 24);
    triple = new CollisionWordTriple(xWord, yWord, zWord);
    pointKey = $"{p1.X},{p1.Y},{p1.Z}; {p2.X},{p2.Y},{p2.Z}; {p3.X},{p3.Y},{p3.Z}; flags=0x{zFlags:X4}";
    return true;
}

bool TryEncodeSourceDerivedSigned9(int value, out uint encoded)
{
    encoded = 0;
    if (value < -256 || value > 255)
        return false;

    encoded = (uint)(value < 0 ? value + 512 : value) & 0x1FFu;
    return true;
}

SourceDerivedCollisionBounds BuildSourceDerivedCollisionBounds(IReadOnlyList<TerrainPolygon> polygons)
{
    if (polygons.Count == 0)
        return new SourceDerivedCollisionBounds(0, 0, 0, 0, 0, 0);

    int minX = int.MaxValue;
    int minY = int.MaxValue;
    int minZ = int.MaxValue;
    int maxX = int.MinValue;
    int maxY = int.MinValue;
    int maxZ = int.MinValue;
    foreach (TerrainPolygon polygon in polygons)
    {
        int pointCount = Math.Min(polygon.Points.Count, polygon.ZValues.Length);
        for (int i = 0; i < pointCount; i++)
        {
            int x = (int)MathF.Round(polygon.Points[i].X);
            int y = (int)MathF.Round(polygon.Points[i].Y);
            int z = (int)MathF.Round(polygon.ZValues[i]);
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            minZ = Math.Min(minZ, z);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
            maxZ = Math.Max(maxZ, z);
        }
    }

    return new SourceDerivedCollisionBounds(minX, maxX, minY, maxY, minZ, maxZ);
}

IReadOnlyList<SourceDerivedCollisionTableCandidate> BuildSourceDerivedCollisionTableCandidates(
    byte[] wad,
    int scanStart,
    int scanEnd,
    IReadOnlySet<int> hitOffsets,
    SourceDerivedCollisionBounds bounds)
{
    if (hitOffsets.Count == 0 || scanStart < 0 || scanEnd <= scanStart)
        return [];

    int clampedStart = Math.Clamp(scanStart, 0, wad.Length);
    int clampedEnd = Math.Clamp(scanEnd, clampedStart, wad.Length);
    List<SourceDerivedCollisionTableCandidate> candidates = new();
    foreach (int residue in hitOffsets.Select(offset => PositiveModulo(offset, 12)).Distinct().Order())
    {
        int firstOffset = clampedStart + PositiveModulo(residue - PositiveModulo(clampedStart, 12), 12);
        int runStart = -1;
        int runCount = 0;
        int hitCount = 0;
        int firstHit = -1;
        int lastHit = -1;

        for (int offset = firstOffset; offset + 12 <= clampedEnd; offset += 12)
        {
            bool plausible = LooksLikeSourceDerivedCollisionRecord(wad, offset, bounds);
            if (!plausible)
            {
                AddSourceDerivedCollisionTableCandidate(candidates, wad, clampedStart, runStart, runCount, hitCount, firstHit, lastHit, residue);
                runStart = -1;
                runCount = 0;
                hitCount = 0;
                firstHit = -1;
                lastHit = -1;
                continue;
            }

            if (runStart < 0)
                runStart = offset;
            runCount++;
            if (hitOffsets.Contains(offset))
            {
                hitCount++;
                if (firstHit < 0)
                    firstHit = offset;
                lastHit = offset;
            }
        }

        AddSourceDerivedCollisionTableCandidate(candidates, wad, clampedStart, runStart, runCount, hitCount, firstHit, lastHit, residue);
    }

    return candidates
        .OrderByDescending(candidate => candidate.HitCount)
        .ThenByDescending(candidate => candidate.LookupWordCount)
        .ThenByDescending(candidate => candidate.TriangleCount)
        .Take(8)
        .ToArray();
}

string[] BuildSourceDerivedLookupReferencedRuntimeKeys(
    byte[] wad,
    IReadOnlyList<SourceDerivedCollisionTableCandidate> tableCandidates,
    IReadOnlyDictionary<CollisionWordTriple, SourceDerivedCollisionPattern> patterns)
{
    if (tableCandidates.Count == 0 || patterns.Count == 0)
        return [];

    HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase);
    foreach (SourceDerivedCollisionTableCandidate candidate in tableCandidates.Where(candidate =>
        candidate.LookupStartWadOffset >= 0 &&
        candidate.LookupEndWadOffset > candidate.LookupStartWadOffset &&
        candidate.EndWadOffset > candidate.StartWadOffset &&
        candidate.TriangleCount > 0))
    {
        int lookupStart = Math.Clamp(candidate.LookupStartWadOffset, 0, wad.Length);
        int lookupEnd = Math.Clamp(candidate.LookupEndWadOffset, lookupStart, wad.Length);
        for (int offset = lookupStart; offset + 2 <= lookupEnd; offset += 2)
        {
            int triangleIndex = BinaryPrimitives.ReadUInt16LittleEndian(wad.AsSpan(offset, 2)) & 0x7FFF;
            if (triangleIndex < 0 || triangleIndex >= candidate.TriangleCount)
                continue;

            int triangleOffset = candidate.StartWadOffset + (triangleIndex * 12);
            if (triangleOffset < 0 || triangleOffset + 12 > wad.Length)
                continue;

            CollisionWordTriple triple = new(
                BinaryPrimitives.ReadUInt32LittleEndian(wad.AsSpan(triangleOffset, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(wad.AsSpan(triangleOffset + 4, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(wad.AsSpan(triangleOffset + 8, 4)));
            if (patterns.TryGetValue(triple, out SourceDerivedCollisionPattern? pattern))
                keys.Add(pattern.RuntimeKey);
        }
    }

    return keys.Order(StringComparer.OrdinalIgnoreCase).ToArray();
}

void AddSourceDerivedCollisionTableCandidate(
    List<SourceDerivedCollisionTableCandidate> candidates,
    byte[] wad,
    int scanStart,
    int runStart,
    int runCount,
    int hitCount,
    int firstHit,
    int lastHit,
    int alignment)
{
    if (runStart < 0 || runCount < 8 || hitCount <= 0)
        return;

    SourceDerivedCollisionLookupRun lookup = FindSourceDerivedCollisionLookupRun(wad, scanStart, runStart, runCount);
    string confidence = hitCount >= 256 && lookup.WordCount >= 64
        ? "strong table + lookup candidate"
        : hitCount >= 64
        ? "strong table candidate"
        : hitCount >= 8
        ? "weak table candidate"
        : "sample-only candidate";

    candidates.Add(new SourceDerivedCollisionTableCandidate(
        StartWadOffset: runStart,
        EndWadOffset: runStart + (runCount * 12),
        TriangleCount: runCount,
        HitCount: hitCount,
        FirstHitWadOffset: firstHit,
        LastHitWadOffset: lastHit,
        AlignmentModulo12: alignment,
        LookupStartWadOffset: lookup.StartWadOffset,
        LookupEndWadOffset: lookup.EndWadOffset,
        LookupWordCount: lookup.WordCount,
        LookupGroupStartCount: lookup.GroupStartCount,
        LookupUniqueTriangleRefs: lookup.UniqueTriangleRefs,
        Confidence: confidence));
}

SourceDerivedCollisionLookupRun FindSourceDerivedCollisionLookupRun(byte[] wad, int scanStart, int tableStart, int triangleCount)
{
    if (triangleCount <= 0 || tableStart <= scanStart)
        return new SourceDerivedCollisionLookupRun(-1, -1, 0, 0, 0);

    int lookupScanStart = Math.Max(scanStart, tableStart - 0x40000);
    int runStart = -1;
    int wordCount = 0;
    int groupStarts = 0;
    HashSet<int> refs = new();
    SourceDerivedCollisionLookupRun best = new(-1, -1, 0, 0, 0);

    for (int offset = lookupScanStart; offset + 2 <= tableStart; offset += 2)
    {
        ushort word = BinaryPrimitives.ReadUInt16LittleEndian(wad.AsSpan(offset, 2));
        int triangleIndex = word & 0x7FFF;
        if (triangleIndex < triangleCount)
        {
            if (runStart < 0)
                runStart = offset;
            wordCount++;
            if ((word & 0x8000) != 0)
                groupStarts++;
            refs.Add(triangleIndex);
            continue;
        }

        best = BetterLookupRun(best, new SourceDerivedCollisionLookupRun(runStart, offset, wordCount, groupStarts, refs.Count));
        runStart = -1;
        wordCount = 0;
        groupStarts = 0;
        refs.Clear();
    }

    return BetterLookupRun(best, new SourceDerivedCollisionLookupRun(runStart, tableStart, wordCount, groupStarts, refs.Count));
}

SourceDerivedCollisionLookupRun BetterLookupRun(SourceDerivedCollisionLookupRun current, SourceDerivedCollisionLookupRun candidate)
{
    if (candidate.StartWadOffset < 0 || candidate.WordCount < 16)
        return current;
    if (current.StartWadOffset < 0)
        return candidate;
    if (candidate.WordCount != current.WordCount)
        return candidate.WordCount > current.WordCount ? candidate : current;
    if (candidate.GroupStartCount != current.GroupStartCount)
        return candidate.GroupStartCount > current.GroupStartCount ? candidate : current;
    return candidate.UniqueTriangleRefs > current.UniqueTriangleRefs ? candidate : current;
}

bool LooksLikeSourceDerivedCollisionRecord(byte[] wad, int offset, SourceDerivedCollisionBounds bounds)
{
    if (offset < 0 || offset + 12 > wad.Length)
        return false;

    CollisionWordTriple triple = new(
        BinaryPrimitives.ReadUInt32LittleEndian(wad.AsSpan(offset, 4)),
        BinaryPrimitives.ReadUInt32LittleEndian(wad.AsSpan(offset + 4, 4)),
        BinaryPrimitives.ReadUInt32LittleEndian(wad.AsSpan(offset + 8, 4)));
    DecodeSourceDerivedCollisionWord(triple.XWord, out int x1, out int x2, out int x3);
    DecodeSourceDerivedCollisionWord(triple.YWord, out int y1, out int y2, out int y3);
    int z1 = (int)(triple.ZWord & 0x3FFFu);
    int z2 = z1 + (int)((triple.ZWord >> 16) & 0xFFu);
    int z3 = z1 + (int)((triple.ZWord >> 24) & 0xFFu);
    if (!IsSourceDerivedCollisionPointInBounds(x1, y1, z1, bounds) ||
        !IsSourceDerivedCollisionPointInBounds(x2, y2, z2, bounds) ||
        !IsSourceDerivedCollisionPointInBounds(x3, y3, z3, bounds))
    {
        return false;
    }

    int area2 = ((x2 - x1) * (y3 - y1)) - ((x3 - x1) * (y2 - y1));
    int zSpan = Math.Max(z1, Math.Max(z2, z3)) - Math.Min(z1, Math.Min(z2, z3));
    return area2 != 0 || zSpan > 0;
}

void DecodeSourceDerivedCollisionWord(uint word, out int a, out int b, out int c)
{
    a = (int)(word & 0x3FFFu);
    b = a + DecodeSourceDerivedSigned9((word >> 14) & 0x1FFu);
    c = a + DecodeSourceDerivedSigned9((word >> 23) & 0x1FFu);
}

int DecodeSourceDerivedSigned9(uint value)
{
    value &= 0x1FFu;
    return value >= 0x100u ? (int)value - 0x200 : (int)value;
}

bool IsSourceDerivedCollisionPointInBounds(int x, int y, int z, SourceDerivedCollisionBounds bounds)
{
    const int xyMargin = 1024;
    const int zMargin = 2048;
    return x >= 0 && x <= 0x3FFF &&
        y >= 0 && y <= 0x3FFF &&
        z >= 0 && z <= 0x3FFF &&
        x >= bounds.MinX - xyMargin && x <= bounds.MaxX + xyMargin &&
        y >= bounds.MinY - xyMargin && y <= bounds.MaxY + xyMargin &&
        z >= bounds.MinZ - zMargin && z <= bounds.MaxZ + zMargin;
}

int PositiveModulo(int value, int divisor)
{
    int result = value % divisor;
    return result < 0 ? result + divisor : result;
}

string BuildSourceDerivedCollisionProbeMarkdown(SourceDerivedCollisionProbeReport report)
{
    StringBuilder builder = new();
    builder.AppendLine($"# Source-Derived Collision Probe: {report.LevelName}");
    builder.AppendLine();
    builder.AppendLine(report.Conclusion);
    builder.AppendLine();
    builder.AppendLine($"- Scan scope: `{report.ScanScope}`");
    builder.AppendLine($"- Source sectors matched: {report.MatchedSourceSectors}/{report.SourceSectors}");
    builder.AppendLine($"- Source-mapped faces tested: {report.SourceMappedFaces}");
    builder.AppendLine($"- Encoded triangle variants: {report.EncodedTriangleVariants}");
    builder.AppendLine($"- Unique encoded patterns: {report.UniquePatternCount}");
    builder.AppendLine($"- Exact WAD hits: {report.TotalHits}");
    builder.AppendLine($"- Unique hit patterns: {report.UniquePatternHits}");
    builder.AppendLine($"- Runtime keys with hits: {report.MatchedRuntimeKeys}");
    builder.AppendLine($"- Runtime keys referenced by lookup: {report.LookupReferencedRuntimeKeys.Count}");
    if (report.Bounds != null)
        builder.AppendLine($"- Visual bounds tested: X {report.Bounds.MinX}..{report.Bounds.MaxX}, Y {report.Bounds.MinY}..{report.Bounds.MaxY}, Z {report.Bounds.MinZ}..{report.Bounds.MaxZ}");
    if (report.HitRuntimeKeys.Count > 0)
        builder.AppendLine($"- Sample hit faces: {string.Join(", ", report.HitRuntimeKeys.Take(24).Select(key => $"`{key}`"))}");
    if (report.LookupReferencedRuntimeKeys.Count > 0)
        builder.AppendLine($"- Sample lookup-referenced faces: {string.Join(", ", report.LookupReferencedRuntimeKeys.Take(24).Select(key => $"`{key}`"))}");
    if (report.TableCandidates.Count > 0)
    {
        builder.AppendLine();
        builder.AppendLine("## Collision Table Candidates");
        builder.AppendLine();
        builder.AppendLine("| Span | Triangles | Exact hits | First hit | Last hit | Lookup span | Lookup words | Groups | Unique refs | Confidence |");
        builder.AppendLine("|---|---:|---:|---:|---:|---|---:|---:|---:|---|");
        foreach (SourceDerivedCollisionTableCandidate candidate in report.TableCandidates)
        {
            string lookupSpan = candidate.LookupStartWadOffset >= 0
                ? $"`0x{candidate.LookupStartWadOffset:X}-0x{candidate.LookupEndWadOffset:X}`"
                : "";
            builder.AppendLine($"| `0x{candidate.StartWadOffset:X}-0x{candidate.EndWadOffset:X}` | {candidate.TriangleCount} | {candidate.HitCount} | `0x{candidate.FirstHitWadOffset:X}` | `0x{candidate.LastHitWadOffset:X}` | {lookupSpan} | {candidate.LookupWordCount} | {candidate.LookupGroupStartCount} | {candidate.LookupUniqueTriangleRefs} | {candidate.Confidence} |");
        }
    }

    if (report.Hits.Count > 0)
    {
        builder.AppendLine();
        builder.AppendLine("## Sample Hits");
        builder.AppendLine();
        builder.AppendLine("| WAD offset | Face | Triangle | Duplicate patterns | Encoded points |");
        builder.AppendLine("|---:|---|---:|---:|---|");
        foreach (SourceDerivedCollisionHit hit in report.Hits)
            builder.AppendLine($"| `0x{hit.WadOffset:X}` | `{hit.RuntimeKey}` | {hit.TriangleIndex} | {hit.DuplicatePatternCount} | `{hit.PointKey}` |");
    }

    return builder.ToString();
}

string BuildCollisionMaterialAuditMarkdown(StoneHillCollisionMaterialAudit report)
{
    StringBuilder builder = new();
    builder.AppendLine("# Stone Hill Collision Material Audit");
    builder.AppendLine();
    builder.AppendLine(report.Conclusion);
    builder.AppendLine();
    builder.AppendLine($"- Collision header: `{report.HeaderOffset}`");
    builder.AppendLine($"- Collision triangle table: `{report.TriangleOffset}`");
    builder.AppendLine($"- Collision triangles: {report.TriangleCount}");
    builder.AppendLine($"- Matched visual triangles: {report.MatchedVisualTriangles}");
    builder.AppendLine();
    builder.AppendLine("## Collision Flag Counts");
    builder.AppendLine();
    builder.AppendLine("| zFlags | Triangles | Matched visual triangles |");
    builder.AppendLine("|---|---:|---:|");
    foreach ((string flag, int count) in report.CollisionFlagCounts)
    {
        report.MatchedFlagCounts.TryGetValue(flag, out int matched);
        builder.AppendLine($"| `{flag}` | {count} | {matched} |");
    }

    builder.AppendLine();
    builder.AppendLine("## Matched Material Counts");
    builder.AppendLine();
    builder.AppendLine("| zFlags + material | Matched faces | Example |");
    builder.AppendLine("|---|---:|---|");
    foreach ((string key, int count) in report.MaterialByCollisionFlag)
    {
        report.Examples.TryGetValue(key, out CollisionMaterialExample? example);
        string sample = example == null
            ? ""
            : $"tri {example.CollisionTriangle}, {example.RuntimeKey}, texture {example.TextureId}, {example.Word3}/{example.Word4}";
        builder.AppendLine($"| `{key}` | {count} | {sample} |");
    }

    builder.AppendLine();
    builder.AppendLine("## Working Result");
    builder.AppendLine();
    builder.AppendLine("The packed CollTri coordinate format is useful for proving terrain height edits and collision geometry, but Stone Hill's preserved zFlags do not separate water from normal ground. The next exact-behavior target is therefore not the known triangle zFlags; it is either another collision response table, level control data, or engine code that maps position/visual material to Spyro damage/death behavior.");
    return builder.ToString();
}

string BuildTerrainBehaviorEvidenceMarkdown(TerrainBehaviorEvidenceReport report)
{
    StringBuilder builder = new();
    builder.AppendLine("# Terrain Behavior Evidence");
    builder.AppendLine();
    builder.AppendLine(report.Verdict);
    builder.AppendLine();
    builder.AppendLine("## Current Proof");
    builder.AppendLine();
    builder.AppendLine("- Visual material labels come from texture color plus editor overrides.");
    builder.AppendLine($"- Collision evidence: {report.CollisionEvidence}");
    builder.AppendLine("- Palette and PNG texture import are editor/export features now, but they do not prove gameplay behavior by themselves.");
    builder.AppendLine();
    builder.AppendLine("## Surface Taxonomy");
    builder.AppendLine();
    builder.AppendLine("| Surface | Editor meaning | Current behavior label | Proof status |");
    builder.AppendLine("|---|---|---|---|");
    foreach (TerrainSurfaceTaxonomyRow row in report.SurfaceTaxonomy)
        builder.AppendLine($"| {row.Surface} | {row.EditorMeaning} | {row.CurrentBehaviorLabel} | {row.ProofStatus} |");

    builder.AppendLine();
    builder.AppendLine("## Control Proximity Scan");
    builder.AppendLine();
    builder.AppendLine("This checks whether nonvisual/control mobys sit near water/lava/ooze-looking faces. Close proximity is not proof, but it tells us whether hazard behavior may be stored in level control records instead of terrain triangles.");
    builder.AppendLine();
    builder.AppendLine("| Level | Faces | Hazard faces | Control mobys | Hazard nearest control | Solid nearest control |");
    builder.AppendLine("|---|---:|---:|---:|---|---|");
    foreach (TerrainBehaviorLevelEvidence level in report.Levels.OrderByDescending(level => level.HazardCandidateFaces).ThenBy(level => level.DisplayName, StringComparer.OrdinalIgnoreCase))
    {
        builder.AppendLine($"| {level.DisplayName} | {level.FaceCount} | {level.HazardCandidateFaces} | {level.ControlMobyCount} | {FormatProximity(level.HazardControlProximity)} | {FormatProximity(level.SolidControlProximity)} |");
    }

    builder.AppendLine();
    builder.AppendLine("## Closest Control Candidates To Hazard Terrain");
    builder.AppendLine();
    builder.AppendLine("| Level | Control | Label | Type/bytes | Hazard distance | Hazard face | Solid distance |");
    builder.AppendLine("|---|---|---|---|---:|---|---:|");
    foreach (TerrainControlProximitySample sample in report.NearestControlSamples.Take(32))
    {
        string bytes = $"{sample.TypeHex} b36 {sample.SourceByte36Hex} 4A {sample.Flag4AHex} 4B {sample.Flag4BHex} 4F {sample.SourceByte4FHex}";
        string solidDistance = sample.NearestSolidDistance.HasValue ? sample.NearestSolidDistance.Value.ToString("0.##", CultureInfo.InvariantCulture) : "";
        builder.AppendLine($"| {sample.DisplayName} | {sample.ControlId} | {sample.Label} | `{bytes}` | {sample.NearestHazardDistance:0.##} | {sample.NearestHazardSurface} tex {sample.NearestHazardTextureId} `{sample.NearestHazardRuntimeKey}` dz {sample.NearestHazardZDelta:0.##} | {solidDistance} |");
    }

    builder.AppendLine();
    builder.AppendLine("## Next Proof Tests");
    builder.AppendLine();
    foreach (string test in report.NextProofTests)
        builder.AppendLine($"- {test}");

    return builder.ToString();
}

static string FormatProximity(TerrainFaceControlProximity stats)
{
    if (stats.FaceCount == 0)
        return "no faces";
    if (stats.NearestControlFaceCount == 0)
        return "no controls";

    return $"median {stats.MedianNearestControlDistance:0.##}, <=256 {stats.Within256}, <=512 {stats.Within512}";
}

static string TryReadCollisionConclusion(string path)
{
    if (!File.Exists(path))
        return "Stone Hill collision audit has not been generated in this workspace.";

    try
    {
        using FileStream stream = File.OpenRead(path);
        using JsonDocument document = JsonDocument.Parse(stream);
        string conclusion = "";
        if (document.RootElement.TryGetProperty("Conclusion", out JsonElement pascalConclusion))
            conclusion = pascalConclusion.GetString() ?? pascalConclusion.ToString();
        else if (document.RootElement.TryGetProperty("conclusion", out JsonElement camelConclusion))
            conclusion = camelConclusion.GetString() ?? camelConclusion.ToString();
        return string.IsNullOrWhiteSpace(conclusion) ? "Stone Hill collision audit exists, but no conclusion field was found." : conclusion;
    }
    catch
    {
        return "Stone Hill collision audit exists, but could not be read.";
    }
}

static IReadOnlyList<TerrainSurfaceTaxonomyRow> BuildSurfaceTaxonomy()
{
    return
    [
        new TerrainSurfaceTaxonomyRow("grass", "Green walkable-looking terrain", "solid-candidate", "Visual/material proof only; exact response not decoded."),
        new TerrainSurfaceTaxonomyRow("sand", "Tan walkable-looking terrain", "solid-candidate", "Visual/material proof only; exact response not decoded."),
        new TerrainSurfaceTaxonomyRow("stone", "Gray stone or paved terrain", "solid-candidate", "Visual/material proof only; exact response not decoded."),
        new TerrainSurfaceTaxonomyRow("brick", "Castle brick or warm masonry terrain", "solid-candidate", "Visual/material proof only; exact response not decoded."),
        new TerrainSurfaceTaxonomyRow("ground", "Brown/dirt-like terrain", "solid-candidate", "Visual/material proof only; exact response not decoded."),
        new TerrainSurfaceTaxonomyRow("ice", "Blue icy walkable-looking terrain", "solid-candidate", "Visual/material proof only; exact response not decoded."),
        new TerrainSurfaceTaxonomyRow("cliff", "Steep rocky/wall-like terrain", "steep-or-wall-candidate", "Slope/height-spread candidate; slide/wall response not decoded."),
        new TerrainSurfaceTaxonomyRow("water", "Blue water-looking terrain", "hazard-candidate", "Visual/material proof only; Stone Hill CollTri zFlags do not separate it from ground."),
        new TerrainSurfaceTaxonomyRow("lava", "Red/orange hot-looking terrain", "hazard-candidate", "Color/material candidate; needs runtime player-response proof."),
        new TerrainSurfaceTaxonomyRow("ooze", "Green toxic-looking terrain", "hazard-candidate", "Ready as a material label, but exact Spyro response is not decoded.")
    ];
}

static TerrainFaceControlProximity BuildFaceControlProximity(string label, IReadOnlyList<TerrainPolygon> faces, IReadOnlyList<Moby> controls)
{
    if (faces.Count == 0)
        return new TerrainFaceControlProximity { Label = label, FaceCount = 0 };
    if (controls.Count == 0)
        return new TerrainFaceControlProximity { Label = label, FaceCount = faces.Count };

    List<double> distances = new();
    foreach (TerrainPolygon face in faces)
    {
        double nearest = controls.Min(control => DistanceXY(face.Center.X, face.Center.Y, control.Position.X, control.Position.Y));
        distances.Add(nearest);
    }

    distances.Sort();
    return new TerrainFaceControlProximity
    {
        Label = label,
        FaceCount = faces.Count,
        NearestControlFaceCount = distances.Count,
        MedianNearestControlDistance = Percentile(distances, 0.5),
        MinNearestControlDistance = distances[0],
        Within128 = distances.Count(distance => distance <= 128),
        Within256 = distances.Count(distance => distance <= 256),
        Within512 = distances.Count(distance => distance <= 512)
    };
}

static TerrainPolygon? FindNearestFace(Vector3f position, IReadOnlyList<TerrainPolygon> faces, out double distance)
{
    TerrainPolygon? nearest = null;
    distance = double.MaxValue;
    foreach (TerrainPolygon face in faces)
    {
        double current = DistanceXY(position.X, position.Y, face.Center.X, face.Center.Y);
        if (current >= distance)
            continue;

        distance = current;
        nearest = face;
    }

    return nearest;
}

static bool IsBehaviorControlMoby(Moby moby)
{
    if (moby.IsChest || moby.IsChestContent || moby.IsGemLike)
        return false;

    if (moby.VisualKind == MobyVisualKind.Control)
        return true;

    string text = $"{moby.DisplayLabel} {moby.CandidateKind} {moby.Evidence} {moby.BehaviorNote}".ToLowerInvariant();
    return text.Contains("trigger", StringComparison.Ordinal) ||
        text.Contains("camera", StringComparison.Ordinal) ||
        text.Contains("helper", StringComparison.Ordinal) ||
        text.Contains("marker", StringComparison.Ordinal) ||
        text.Contains("control", StringComparison.Ordinal) ||
        text.Contains("system", StringComparison.Ordinal);
}

static bool IsHazardMaterial(TerrainPolygon face)
{
    string surface = TerrainMaterialClassifier.NormalizeSurfaceName(face.Surface);
    return surface is "water" or "lava" or "ooze";
}

static bool IsLikelySolidMaterial(TerrainPolygon face)
{
    string surface = TerrainMaterialClassifier.NormalizeSurfaceName(face.Surface);
    return surface is "grass" or "sand" or "stone" or "ground" or "ice";
}

static double DistanceXY(float ax, float ay, float bx, float by)
{
    double dx = ax - bx;
    double dy = ay - by;
    return Math.Sqrt((dx * dx) + (dy * dy));
}

static double Percentile(IReadOnlyList<double> sorted, double percentile)
{
    if (sorted.Count == 0)
        return 0;
    if (sorted.Count == 1)
        return sorted[0];

    double index = (sorted.Count - 1) * Math.Clamp(percentile, 0, 1);
    int lower = (int)Math.Floor(index);
    int upper = (int)Math.Ceiling(index);
    if (lower == upper)
        return sorted[lower];

    double fraction = index - lower;
    return sorted[lower] + ((sorted[upper] - sorted[lower]) * fraction);
}

static byte[] BuildSyntheticTerrainBehaviorRam()
{
    byte[] ram = new byte[0x200000];
    int mobyTableOffset = 0x90000;
    WriteUInt32(ram, 0x7573C, 0x800B0000);
    WriteInt32(ram, 0x75750, 10);
    WriteInt32(ram, 0x75810, 2);
    WriteUInt32(ram, 0x75828, 0x80090000);
    WriteUInt16(ram, 0x75860, 1234);
    WriteUInt32(ram, 0x758B4, 0x0000000B);
    WriteUInt32(ram, 0x75930, 0x800A0000);
    WriteInt32(ram, 0x78A58, 0x00010000);
    WriteInt32(ram, 0x78A5C, 0x00020000);
    WriteInt32(ram, 0x78A60, 0x00003000);
    WriteUInt32(ram, 0x78AD0, 0x00000002);
    WriteUInt32(ram, 0x78AD4, 0x00000000);
    WriteInt32(ram, 0x78AD8, 12);
    WriteInt32(ram, 0x78BB4, 1);
    WriteInt32(ram, 0x78BB8, 0);
    WriteInt32(ram, 0x78BBC, 3);
    WriteInt32(ram, 0x78BAC, 0);

    for (int i = 0; i < 197; i++)
    {
        int offset = mobyTableOffset + (i * 0x58);
        WriteUInt32(ram, offset + 0x08, (uint)(0x800A0000 + (i * 0x100)));
        WriteInt32(ram, offset + 0x0C, 1000 + (i * 64));
        WriteInt32(ram, offset + 0x10, 2000 + (i * 32));
        WriteInt32(ram, offset + 0x14, 300 + (i * 8));
        ram[offset + 0x50] = i is 149 or 150 or 151 ? (byte)0x00 : (byte)0x20;
        ram[offset + 0x51] = 0x00;
        ram[offset + 0x52] = 0x10;
        ram[offset + 0x53] = 0xFF;
    }

    return ram;
}

static string BuildTerrainBehaviorRamPairSmokeMarkdown(TerrainBehaviorRamPairReport report)
{
    StringBuilder builder = new();
    builder.AppendLine("# Terrain Behavior RAM Pair Smoke");
    builder.AppendLine();
    builder.AppendLine(report.Interpretation);
    builder.AppendLine();
    builder.AppendLine($"- Decoded mobys: {report.Before.DecodedMobys}");
    builder.AppendLine($"- Spyro health: {report.Before.Spyro.Health} -> {report.After.Spyro.Health}");
    builder.AppendLine($"- Spyro i-frames: {report.Before.Spyro.IFrames} -> {report.After.Spyro.IFrames}");
    builder.AppendLine($"- Spyro changes: {string.Join(", ", report.SpyroChanges.Select(change => $"{change.Field}={change.Before}->{change.After}"))}");
    builder.AppendLine($"- Global changes: {report.GlobalChanges.Count}");
    builder.AppendLine($"- Watched range changes: {string.Join(", ", report.WatchedRanges.Select(range => $"{range.Label}={range.ChangedByteCount}"))}");
    builder.AppendLine($"- Focus controls: {string.Join(", ", report.FocusControls.Select(control => $"T{control.Focus.TrueIndex} special={control.SpecialDataDiff.ChangedByteCount}"))}");
    return builder.ToString();
}

static void WriteUInt16(byte[] bytes, int offset, ushort value)
{
    bytes[offset] = (byte)(value & 0xFF);
    bytes[offset + 1] = (byte)((value >> 8) & 0xFF);
}

static void WriteUInt32(byte[] bytes, int offset, uint value)
{
    bytes[offset] = (byte)(value & 0xFF);
    bytes[offset + 1] = (byte)((value >> 8) & 0xFF);
    bytes[offset + 2] = (byte)((value >> 16) & 0xFF);
    bytes[offset + 3] = (byte)((value >> 24) & 0xFF);
}

static void WriteInt32(byte[] bytes, int offset, int value)
{
    WriteUInt32(bytes, offset, unchecked((uint)value));
}

static IReadOnlyDictionary<string, int> SortedCounts(Dictionary<string, int> counts)
{
    return counts
        .OrderByDescending(pair => pair.Value)
        .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
}

static void Increment(Dictionary<string, int> counts, string key)
{
    counts[key] = counts.TryGetValue(key, out int count) ? count + 1 : 1;
}

TerrainTextureAudit BuildTerrainTextureAudit(IGrouping<int, TerrainPolygon> group)
{
    List<TerrainPolygon> faces = group.ToList();
    string dominantSurface = MostCommon(faces.Select(face => TerrainMaterialClassifier.NormalizeSurfaceName(face.Surface)));
    string dominantBehavior = MostCommon(faces.Select(face => face.Behavior));
    return new TerrainTextureAudit
    {
        TextureId = group.Key,
        FaceCount = faces.Count,
        DominantSurface = dominantSurface,
        SurfaceSource = MostCommon(faces.Select(face => face.SurfaceSource)),
        DominantBehavior = dominantBehavior,
        BehaviorConfidence = MostCommon(faces.Select(face => face.BehaviorConfidence)),
        MinZ = faces.Min(face => face.MinZ),
        MaxZ = faces.Max(face => face.MaxZ),
        AverageColor = AverageColorHex(faces),
        DepthSummary = TopCounts(faces.Where(face => face.FaceDepth >= 0).Select(face => face.FaceDepth.ToString(CultureInfo.InvariantCulture)), 4),
        FlipSummary = TopCounts(faces.Select(face => face.FaceFlip ? "flipped" : "normal"), 2),
        Word3Samples = faces.Select(face => face.Word3).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToList(),
        Word4Samples = faces.Select(face => face.Word4).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToList(),
        RuntimeKeySamples = faces.Select(face => face.RuntimeKey).Take(4).ToList()
    };
}

string BuildTerrainAuditMarkdown(IReadOnlyList<TerrainLevelAudit> levels, IReadOnlyList<GeometryCacheHealthIssue> skipped)
{
    StringBuilder builder = new();
    builder.AppendLine("# Terrain Material and Behavior Audit");
    builder.AppendLine();
    builder.AppendLine("This report separates visible material guesses from proven Spyro behavior. The current portable cache exposes terrain texture IDs, color, raw face words, depth, flip, and vertex data. It does not yet expose a decoded collision response flag, so water/lava/ooze are marked as hazard candidates rather than final truth.");
    builder.AppendLine();
    builder.AppendLine("## Current Terrain Types");
    builder.AppendLine();
    builder.AppendLine("- Visual materials currently recognized: grass, water, sand, stone, brick, ground, ice, cliff, lava candidate, ooze-ready, and unknown.");
    builder.AppendLine("- Behavior labels currently recognized: solid candidate, steep/wall candidate, hazard candidate, and unknown.");
    builder.AppendLine("- Exact hurt/kill/swim/slide behavior still needs the collision response field decoded. The older collision bridge can find packed collision triangles, but it does not yet identify response/material flags.");
    builder.AppendLine();
    if (skipped.Count > 0)
    {
        builder.AppendLine("## Skipped Cache Entries");
        builder.AppendLine();
        builder.AppendLine("| Level | Reason |");
        builder.AppendLine("|---|---|");
        foreach (GeometryCacheHealthIssue issue in skipped)
            builder.AppendLine($"| {issue.LevelKey} | {issue.Message} |");
        builder.AppendLine();
    }

    builder.AppendLine("## Level Summary");
    builder.AppendLine();
    builder.AppendLine("| Level | Faces | Textures | Top materials | Behavior candidates |");
    builder.AppendLine("|---|---:|---:|---|---|");
    foreach (TerrainLevelAudit level in levels.OrderBy(level => level.DisplayName, StringComparer.OrdinalIgnoreCase))
    {
        builder.AppendLine($"| {level.DisplayName} | {level.FaceCount} | {level.TextureCount} | {FormatCounts(level.SurfaceCounts, 5)} | {FormatCounts(level.BehaviorCounts, 4)} |");
    }

    builder.AppendLine();
    builder.AppendLine("## Hazard Candidate Texture Groups");
    builder.AppendLine();
    builder.AppendLine("| Level | Texture | Faces | Material | Z range | Depths | Word3 samples | Word4 samples |");
    builder.AppendLine("|---|---:|---:|---|---:|---|---|---|");
    foreach (TerrainLevelAudit level in levels.OrderBy(level => level.DisplayName, StringComparer.OrdinalIgnoreCase))
    {
        foreach (TerrainTextureAudit texture in level.TextureGroups.Where(group => string.Equals(group.DominantBehavior, "hazard-candidate", StringComparison.OrdinalIgnoreCase)).Take(12))
        {
            builder.AppendLine($"| {level.DisplayName} | {texture.TextureId} | {texture.FaceCount} | {TerrainMaterialClassifier.FormatSurface(texture.DominantSurface)} | {texture.MinZ:0}..{texture.MaxZ:0} | {FormatCounts(texture.DepthSummary, 4)} | {string.Join(", ", texture.Word3Samples)} | {string.Join(", ", texture.Word4Samples)} |");
        }
    }

    builder.AppendLine();
    builder.AppendLine("## Largest Texture Groups");
    builder.AppendLine();
    builder.AppendLine("| Level | Texture | Faces | Material | Behavior | Color | Z range | Raw samples |");
    builder.AppendLine("|---|---:|---:|---|---|---|---:|---|");
    foreach (TerrainLevelAudit level in levels.OrderBy(level => level.DisplayName, StringComparer.OrdinalIgnoreCase))
    {
        foreach (TerrainTextureAudit texture in level.TextureGroups.Take(5))
        {
            builder.AppendLine($"| {level.DisplayName} | {texture.TextureId} | {texture.FaceCount} | {TerrainMaterialClassifier.FormatSurface(texture.DominantSurface)} | {TerrainBehaviorClassifier.FormatBehavior(texture.DominantBehavior)} | {texture.AverageColor} | {texture.MinZ:0}..{texture.MaxZ:0} | {string.Join(" / ", texture.Word3Samples.Take(2).Concat(texture.Word4Samples.Take(2)))} |");
        }
    }

    return builder.ToString();
}

static string MostCommon(IEnumerable<string> values)
{
    return values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(group => group.Count())
        .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.Key)
        .FirstOrDefault() ?? "unknown";
}

static IReadOnlyDictionary<string, int> TopCounts(IEnumerable<string> values, int max)
{
    return values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(group => group.Count())
        .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
        .Take(max)
        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
}

static string FormatCounts(IReadOnlyDictionary<string, int> counts, int max)
{
    if (counts.Count == 0)
        return "none";

    return string.Join(", ", counts.Take(max).Select(pair => $"{pair.Key} {pair.Value}"));
}

static string AverageColorHex(IReadOnlyList<TerrainPolygon> faces)
{
    if (faces.Count == 0)
        return "#000000";

    int r = (int)Math.Round(faces.Average(face => face.FaceColor.R));
    int g = (int)Math.Round(faces.Average(face => face.FaceColor.G));
    int b = (int)Math.Round(faces.Average(face => face.FaceColor.B));
    return $"#{r:X2}{g:X2}{b:X2}";
}

async Task ReportTerrainPatchPlan(LevelDefinition level, GeometryCandidate geometry)
{
    string ramPath = TerrainPatchDataLocator.FindRamDump(workspace, level.Key);
    if (!File.Exists(sourceImage) || !File.Exists(ramPath))
    {
        Console.WriteLine("Stone Hill terrain source patch: source disc or RAM not found; skipping export smoke.");
        return;
    }

    string sourceSearchPath = Path.Combine(workspace.RootPath, "_local", "smoke", "stonehill-runtime-terrain-source-search-native.json");
    TerrainSourceSearchResult sourceSearch = await TerrainSourceSearchBuilder.BuildAsync(new TerrainSourceSearchRequest(
        SourceImagePath: sourceImage,
        OutputPath: sourceSearchPath,
        Level: level,
        Geometry: geometry,
        RamPath: ramPath));
    Console.WriteLine($"Stone Hill terrain source search: {sourceSearch.Report.MatchedSectorCount}/{sourceSearch.Report.SectorCount} sectors matched");
    string? sourceMappedRuntimeKey = ReadFirstTerrainSourceKey(sourceSearchPath);
    HashSet<string> sourceMappedKeys = ReadTerrainSourceKeys(sourceSearchPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
    Dictionary<int, int> sectorAppendSlack = BuildTerrainSectorAppendSlack(sourceSearch.Report);
    HashSet<string> collisionTriangleKeys = File.Exists(ramPath)
        ? SpyroCollisionDecoder.Decode(await File.ReadAllBytesAsync(ramPath)).Triangles
            .Select(CollisionTriangleKey)
            .ToHashSet(StringComparer.Ordinal)
        : new HashSet<string>(StringComparer.Ordinal);
    int sourceMappedCollisionFaces = geometry.Polygons.Count(face =>
        face.SectorOffset >= 0
        && face.VertexIndexes.Count > 0
        && sourceMappedKeys.Contains(face.RuntimeKey)
        && HasCollisionTriangleMatch(face, collisionTriangleKeys));
    Console.WriteLine($"Stone Hill terrain collision-source candidates: {sourceMappedCollisionFaces}");
    TerrainPolygon[] patchablePolygons = geometry.Polygons
        .Where(face =>
            face.SectorOffset >= 0
            && face.VertexIndexes.Count > 0
            && sourceMappedKeys.Contains(face.RuntimeKey)
            && HasCollisionTriangleMatch(face, collisionTriangleKeys))
        .Take(2)
        .ToArray();
    TerrainPolygon? polygon = patchablePolygons.FirstOrDefault()
        ?? geometry.Polygons.FirstOrDefault(face =>
        face.SectorOffset >= 0
        && face.VertexIndexes.Count > 0
        && sourceMappedKeys.Contains(face.RuntimeKey)
        && HasCollisionTriangleMatch(face, collisionTriangleKeys))
        ?? geometry.Polygons.FirstOrDefault(face =>
            face.SectorOffset >= 0
            && face.VertexIndexes.Count > 0
            && (sourceMappedRuntimeKey == null || string.Equals(face.RuntimeKey, sourceMappedRuntimeKey, StringComparison.OrdinalIgnoreCase)));
    if (polygon == null)
    {
        Console.WriteLine("Stone Hill terrain source patch: no patchable terrain face found.");
        return;
    }
    Console.WriteLine($"Stone Hill terrain source patch face: {polygon.RuntimeKey}, collisionMatch={HasCollisionTriangleMatch(polygon, collisionTriangleKeys)}");
    List<TerrainEditorReadinessRow> terrainReadinessRows = new();
    void AddTerrainReadiness(string feature, string status, string evidence, string remainingRisk)
    {
        terrainReadinessRows.Add(new TerrainEditorReadinessRow(feature, status, evidence, remainingRisk));
    }

    AddTerrainReadiness(
        "source map",
        "smoke-passed",
        $"{sourceSearch.Report.MatchedSectorCount}/{sourceSearch.Report.SectorCount} sectors matched; {sourceMappedCollisionFaces} collision-source candidates",
        "Only levels with RAM/source-search data can patch scene-sector terrain geometry.");

    Directory.CreateDirectory(Path.Combine(workspace.RootPath, "_local", "smoke"));
    string path = Path.Combine(workspace.RootPath, "_local", "smoke", "terrain-source-patch-edits.json");
    TerrainPolygon[] smoothPolygons = patchablePolygons.Length >= 2 ? patchablePolygons : [polygon];
    Dictionary<string, float[]> smoothDeltasByKey = new(StringComparer.OrdinalIgnoreCase);
    for (int faceIndex = 0; faceIndex < smoothPolygons.Length; faceIndex++)
    {
        TerrainPolygon face = smoothPolygons[faceIndex];
        float[] smoothDeltas = face.OriginalZValues.Select((_, index) => 8f + (faceIndex * 4f) + (index * 3f)).ToArray();
        smoothDeltasByKey[face.RuntimeKey] = smoothDeltas;
        face.ApplyTerrainVertexDeltas(smoothDeltas);
    }
    await TerrainEditStore.SaveAsync(path, smoothPolygons, "Stone Hill smoke");
    TerrainPatchPlan plan = TerrainPatchExporter.BuildPlan(
        sourceImage,
        DiscImageLocator.FindCueForImage(sourceImage),
        Path.Combine(workspace.RootPath, "_local", "terrain", "smoke.bin"),
        Path.Combine(workspace.RootPath, "_local", "terrain", "smoke.cue"),
        level,
        ramPath,
        sourceSearchPath,
        path,
        "");
    foreach (TerrainPolygon face in smoothPolygons)
        face.ResetTerrainEdit();
    int loadedSmoothEdits = TerrainEditStore.Load(path, smoothPolygons);
    bool loadedVertexShape = loadedSmoothEdits == smoothPolygons.Length &&
        smoothPolygons.All(face =>
            face.ZValues.Zip(face.OriginalZValues, (edited, original) => edited - original)
                .SequenceEqual(smoothDeltasByKey[face.RuntimeKey]));
    foreach (TerrainPolygon face in smoothPolygons)
        face.ResetTerrainEdit();
    if (!loadedVertexShape)
        throw new InvalidOperationException("Smooth terrain edit roundtrip did not preserve per-vertex Z values.");
    string[] missingVisualPatches = smoothPolygons
        .Where(face => !plan.Patches.Any(patch =>
            patch.RuntimeKey.Equals(face.RuntimeKey, StringComparison.OrdinalIgnoreCase) &&
            patch.Kind.StartsWith("visual-", StringComparison.OrdinalIgnoreCase)))
        .Select(face => face.RuntimeKey)
        .ToArray();
    if (missingVisualPatches.Length > 0)
        throw new InvalidOperationException($"Smooth terrain source patch did not produce visual vertex patches for: {string.Join(", ", missingVisualPatches)}.");

    string restoredPath = Path.Combine(workspace.RootPath, "_local", "smoke", "terrain-restored-edits.json");
    float[] restoreDeltas = smoothDeltasByKey[polygon.RuntimeKey];
    polygon.ApplyTerrainVertexDeltas(restoreDeltas);
    polygon.ApplyTerrainVertexDeltas(new float[restoreDeltas.Length]);
    int restoredEditCount = await TerrainEditStore.SaveAsync(restoredPath, [polygon], "Stone Hill restore smoke");
    foreach (TerrainPolygon face in smoothPolygons)
        face.ResetTerrainEdit();
    if (restoredEditCount != 0)
        throw new InvalidOperationException("Restored terrain should not leave a saved height edit behind.");

    int collisionPatchCount = plan.Patches.Count(patch => patch.Kind.StartsWith("collision-", StringComparison.OrdinalIgnoreCase));
    if (collisionPatchCount == 0)
        throw new InvalidOperationException("Stone Hill smooth terrain source patch did not produce a collision triangle patch for a collision-matched face.");
    string collisionSkip = plan.SkippedEdits.FirstOrDefault(skip =>
        skip.StartsWith("collision:", StringComparison.OrdinalIgnoreCase) ||
        skip.StartsWith($"{polygon.RuntimeKey}:", StringComparison.OrdinalIgnoreCase)) ?? "";
    string collisionText = collisionPatchCount > 0
        ? $", collision patches={collisionPatchCount}"
        : string.IsNullOrWhiteSpace(collisionSkip) ? ", collision patches=0" : $", collision patches=0 ({collisionSkip})";
    Console.WriteLine($"Stone Hill smooth terrain source patch: {plan.PatchCount} patches, {plan.TotalPatchedBytes} bytes for {string.Join(", ", smoothPolygons.Select(face => face.RuntimeKey))}{collisionText}, restore clears saved edits={restoredEditCount == 0}");
    AddTerrainReadiness(
        "soft terrain brush",
        "smoke-passed",
        $"{smoothPolygons.Length} face(s), {plan.PatchCount} visual/collision patch(es), per-vertex Z roundtrip",
        "Still needs in-game beta testing across more levels and slopes.");
    AddTerrainReadiness(
        "restore terrain",
        "smoke-passed",
        "Restored height edit saved 0 terrain edits",
        "Restore covers staged terrain height state; full patched-disc reset still depends on using a clean source image.");

    int pointIndex = FindFirstCollisionMatchedVertexIndex(polygon, collisionTriangleKeys);
    if (pointIndex < 0)
    {
        Console.WriteLine("Stone Hill single-point terrain patch: no collision-matched point found; skipping point smoke.");
        AddTerrainReadiness(
            "single terrain point",
            "skipped",
            "No collision-matched point found in this smoke source",
            "Run against a level/capture with matched collision before release-signing this path.");
    }
    else
    {
        string pointPath = Path.Combine(workspace.RootPath, "_local", "smoke", "terrain-single-point-edits.json");
        float[] pointDeltas = new float[polygon.OriginalZValues.Length];
        pointDeltas[pointIndex] = 6f;
        polygon.ApplyTerrainVertexDeltas(pointDeltas);
        int pointEditCount = await TerrainEditStore.SaveAsync(pointPath, [polygon], "Stone Hill single-point smoke");
        polygon.ResetTerrainEdit();
        if (pointEditCount != 1)
            throw new InvalidOperationException("Single-point terrain edit should save as one terrain edit.");

        TerrainPatchPlan pointPlan = TerrainPatchExporter.BuildPlan(
            sourceImage,
            DiscImageLocator.FindCueForImage(sourceImage),
            Path.Combine(workspace.RootPath, "_local", "terrain", "single-point-smoke.bin"),
            Path.Combine(workspace.RootPath, "_local", "terrain", "single-point-smoke.cue"),
            level,
            ramPath,
            sourceSearchPath,
            pointPath,
            "");
        bool hasSingleVisualPatch = pointPlan.Patches.Count(patch =>
            patch.RuntimeKey.Equals(polygon.RuntimeKey, StringComparison.OrdinalIgnoreCase) &&
            patch.Kind.StartsWith("visual-", StringComparison.OrdinalIgnoreCase)) == 1;
        bool hasPointCollisionPatch = pointPlan.Patches.Any(patch => string.Equals(patch.Kind, "collision-triangle", StringComparison.OrdinalIgnoreCase));
        if (!hasSingleVisualPatch)
            throw new InvalidOperationException("Single-point terrain edit should produce exactly one visual vertex patch.");
        if (!hasPointCollisionPatch)
            throw new InvalidOperationException("Single-point terrain edit should produce a matched collision triangle patch.");

        Console.WriteLine($"Stone Hill single-point terrain patch: face {polygon.RuntimeKey}, point {pointIndex + 1}, visual-one=True, collision=True, patches={pointPlan.PatchCount}");
        AddTerrainReadiness(
            "single terrain point Z",
            "smoke-passed",
            $"Face {polygon.RuntimeKey}, point {pointIndex + 1}, exactly one visual vertex patch plus collision patch",
            "Needs in-game feel checks for steep/edge cases.");

        string exactPointPath = Path.Combine(workspace.RootPath, "_local", "smoke", "terrain-exact-single-point-edits.json");
        const float ExactPointX = 8f;
        const float ExactPointY = 0f;
        const float ExactPointZ = 9f;
        float[] exactPointDeltas = new float[polygon.OriginalZValues.Length];
        exactPointDeltas[pointIndex] = ExactPointZ;
        Vector2f[] exactPointXYDeltas = polygon.OriginalPoints.Select(_ => new Vector2f(0, 0)).ToArray();
        exactPointXYDeltas[pointIndex] = new Vector2f(ExactPointX, ExactPointY);
        polygon.ApplyTerrainVertexDeltas(exactPointDeltas);
        polygon.ApplyTerrainVertexXYDeltas(exactPointXYDeltas);
        int exactPointEditCount = await TerrainEditStore.SaveAsync(exactPointPath, [polygon], "Stone Hill exact single-point smoke");
        polygon.ResetTerrainEdit();
        if (exactPointEditCount != 1)
            throw new InvalidOperationException("Exact single-point terrain edit should save as one terrain edit.");

        int loadedExactPointEdits = TerrainEditStore.Load(exactPointPath, [polygon]);
        float[] loadedExactPointDeltas = polygon.TerrainVertexDeltas().ToArray();
        Vector2f[] loadedExactPointXYDeltas = polygon.TerrainVertexXYDeltas().ToArray();
        bool loadedExactPoint = loadedExactPointEdits == 1;
        for (int i = 0; i < loadedExactPointDeltas.Length; i++)
        {
            float expected = i == pointIndex ? ExactPointZ : 0f;
            loadedExactPoint &= Math.Abs(loadedExactPointDeltas[i] - expected) <= 0.001f;
        }
        for (int i = 0; i < loadedExactPointXYDeltas.Length; i++)
        {
            float expectedX = i == pointIndex ? ExactPointX : 0f;
            float expectedY = i == pointIndex ? ExactPointY : 0f;
            loadedExactPoint &=
                Math.Abs(loadedExactPointXYDeltas[i].X - expectedX) <= 0.001f &&
                Math.Abs(loadedExactPointXYDeltas[i].Y - expectedY) <= 0.001f;
        }
        polygon.ResetTerrainEdit();
        if (!loadedExactPoint)
            throw new InvalidOperationException("Exact single-point terrain edit did not roundtrip through TerrainEditStore.");

        TerrainPatchPlan exactPointPlan = TerrainPatchExporter.BuildPlan(
            sourceImage,
            DiscImageLocator.FindCueForImage(sourceImage),
            Path.Combine(workspace.RootPath, "_local", "terrain", "exact-single-point-smoke.bin"),
            Path.Combine(workspace.RootPath, "_local", "terrain", "exact-single-point-smoke.cue"),
            level,
            ramPath,
            sourceSearchPath,
            exactPointPath,
            "");
        bool hasExactPointVisualPatch = exactPointPlan.Patches.Any(patch =>
            patch.RuntimeKey.Equals(polygon.RuntimeKey, StringComparison.OrdinalIgnoreCase) &&
            patch.Kind.StartsWith("visual-", StringComparison.OrdinalIgnoreCase));
        bool hasExactPointCollisionPatch = exactPointPlan.Patches.Any(patch => string.Equals(patch.Kind, "collision-triangle", StringComparison.OrdinalIgnoreCase));
        if (!hasExactPointVisualPatch || !hasExactPointCollisionPatch)
            throw new InvalidOperationException($"Exact single-point terrain edit did not produce required patches: visual={hasExactPointVisualPatch}, collision={hasExactPointCollisionPatch}.");

        Console.WriteLine($"Stone Hill exact single-point terrain patch: face {polygon.RuntimeKey}, point {pointIndex + 1}, x={ExactPointX:+0;-0;0}, y={ExactPointY:+0;-0;0}, z={ExactPointZ:+0;-0;0}, visual=True, collision=True, patches={exactPointPlan.PatchCount}");
        AddTerrainReadiness(
            "single terrain point XYZ",
            "smoke-passed",
            $"Face {polygon.RuntimeKey}, point {pointIndex + 1}, X/Y/Z roundtrip plus visual/collision patches",
            "Point XY edits are byte-encoded and should stay small until more sectors are exercised.");
    }

    string xyMovePath = Path.Combine(workspace.RootPath, "_local", "smoke", "terrain-xy-move-edits.json");
    (float Dx, float Dy)[] xyMoves = [(8f, 0f), (-8f, 0f), (0f, 8f), (0f, -8f)];
    TerrainPatchPlan? xyMovePlan = null;
    (float Dx, float Dy) appliedMove = default;
    string xyMoveFailure = "";
    foreach ((float dx, float dy) in xyMoves)
    {
        try
        {
            Vector2f[] xyDeltas = polygon.OriginalPoints.Select(_ => new Vector2f(dx, dy)).ToArray();
            polygon.ApplyTerrainVertexXYDeltas(xyDeltas);
            int xyMoveEditCount = await TerrainEditStore.SaveAsync(xyMovePath, [polygon], "Stone Hill XY move smoke");
            polygon.ResetTerrainEdit();
            if (xyMoveEditCount != 1)
                throw new InvalidOperationException("XY terrain move should save as one terrain edit.");

            int loadedXYMoveEdits = TerrainEditStore.Load(xyMovePath, [polygon]);
            bool loadedXYMove = loadedXYMoveEdits == 1 &&
                polygon.TerrainVertexXYDeltas().All(delta =>
                    Math.Abs(delta.X - dx) <= 0.001f &&
                    Math.Abs(delta.Y - dy) <= 0.001f);
            polygon.ResetTerrainEdit();
            if (!loadedXYMove)
                throw new InvalidOperationException("XY terrain move did not roundtrip through TerrainEditStore.");

            TerrainPatchPlan candidatePlan = TerrainPatchExporter.BuildPlan(
                sourceImage,
                DiscImageLocator.FindCueForImage(sourceImage),
                Path.Combine(workspace.RootPath, "_local", "terrain", "xy-move-smoke.bin"),
                Path.Combine(workspace.RootPath, "_local", "terrain", "xy-move-smoke.cue"),
                level,
                ramPath,
                sourceSearchPath,
                xyMovePath,
                "");
            bool hasXYVisualPatch = candidatePlan.Patches.Any(patch =>
                patch.RuntimeKey.Equals(polygon.RuntimeKey, StringComparison.OrdinalIgnoreCase) &&
                patch.Kind.StartsWith("visual-", StringComparison.OrdinalIgnoreCase));
            bool hasXYCollisionPatch = candidatePlan.Patches.Any(patch => string.Equals(patch.Kind, "collision-triangle", StringComparison.OrdinalIgnoreCase));
            if (!hasXYVisualPatch || !hasXYCollisionPatch)
                throw new InvalidOperationException($"XY terrain move did not produce required patches: visual={hasXYVisualPatch}, collision={hasXYCollisionPatch}.");

            xyMovePlan = candidatePlan;
            appliedMove = (dx, dy);
            break;
        }
        catch (Exception ex)
        {
            polygon.ResetTerrainEdit();
            xyMoveFailure = ex.Message;
        }
    }

    if (xyMovePlan == null)
        throw new InvalidOperationException($"Stone Hill XY terrain move patch did not find an encodable small move. Last failure: {xyMoveFailure}");

    Console.WriteLine($"Stone Hill XY terrain move patch: face {polygon.RuntimeKey}, dx={appliedMove.Dx:+0;-0;0}, dy={appliedMove.Dy:+0;-0;0}, visual=True, collision=True, patches={xyMovePlan.PatchCount}");
    AddTerrainReadiness(
        "move terrain face XY",
        "smoke-passed",
        $"Face {polygon.RuntimeKey}, dx={appliedMove.Dx:+0;-0;0}, dy={appliedMove.Dy:+0;-0;0}, visual/collision patches={xyMovePlan.PatchCount}",
        "Small moves are proven; larger moves may hit PS1 packed-coordinate limits.");

    string exactPlacementPath = Path.Combine(workspace.RootPath, "_local", "smoke", "terrain-exact-placement-edits.json");
    const float ExactPlacementX = 8f;
    const float ExactPlacementY = 0f;
    const float ExactPlacementZ = 18f;
    polygon.ApplyTerrainDeltaZ(ExactPlacementZ);
    polygon.ApplyTerrainVertexXYDeltas(polygon.OriginalPoints.Select(_ => new Vector2f(ExactPlacementX, ExactPlacementY)).ToArray());
    int exactPlacementEditCount = await TerrainEditStore.SaveAsync(exactPlacementPath, [polygon], "Stone Hill exact placement smoke");
    polygon.ResetTerrainEdit();
    if (exactPlacementEditCount != 1)
        throw new InvalidOperationException("Exact terrain placement should save as one terrain edit.");

    int loadedExactPlacementEdits = TerrainEditStore.Load(exactPlacementPath, [polygon]);
    bool loadedExactPlacement = loadedExactPlacementEdits == 1 &&
        polygon.TerrainVertexDeltas().All(delta => Math.Abs(delta - ExactPlacementZ) <= 0.001f) &&
        polygon.TerrainVertexXYDeltas().All(delta =>
            Math.Abs(delta.X - ExactPlacementX) <= 0.001f &&
            Math.Abs(delta.Y - ExactPlacementY) <= 0.001f);
    polygon.ResetTerrainEdit();
    if (!loadedExactPlacement)
        throw new InvalidOperationException("Exact terrain placement did not roundtrip through TerrainEditStore.");

    TerrainPatchPlan exactPlacementPlan = TerrainPatchExporter.BuildPlan(
        sourceImage,
        DiscImageLocator.FindCueForImage(sourceImage),
        Path.Combine(workspace.RootPath, "_local", "terrain", "exact-placement-smoke.bin"),
        Path.Combine(workspace.RootPath, "_local", "terrain", "exact-placement-smoke.cue"),
        level,
        ramPath,
        sourceSearchPath,
        exactPlacementPath,
        "");
    bool hasExactVisualPatch = exactPlacementPlan.Patches.Any(patch =>
        patch.RuntimeKey.Equals(polygon.RuntimeKey, StringComparison.OrdinalIgnoreCase) &&
        patch.Kind.StartsWith("visual-", StringComparison.OrdinalIgnoreCase));
    bool hasExactCollisionPatch = exactPlacementPlan.Patches.Any(patch => string.Equals(patch.Kind, "collision-triangle", StringComparison.OrdinalIgnoreCase));
    if (!hasExactVisualPatch || !hasExactCollisionPatch)
        throw new InvalidOperationException($"Exact terrain placement did not produce required patches: visual={hasExactVisualPatch}, collision={hasExactCollisionPatch}.");
            int sideWallExposedEdges = exactPlacementPlan.TerrainSideWalls.Sum(summary => summary.ExposedEdgeCount);
    int sideWallSolidEdges = exactPlacementPlan.TerrainSideWalls.Sum(summary => summary.EmittedFaceCount);
    int sideWallCollisionPatches = exactPlacementPlan.TerrainSideWalls.Sum(summary => summary.CollisionTriangleCount);
    string sideWallSkip = string.Join(" | ", exactPlacementPlan.SkippedEdits
        .Where(skip => skip.Contains("side wall", StringComparison.OrdinalIgnoreCase) || skip.Contains("side-wall", StringComparison.OrdinalIgnoreCase))
        .Take(3));
    Console.WriteLine($"Stone Hill exact terrain placement patch: face {polygon.RuntimeKey}, x={ExactPlacementX:+0;-0;0}, y=0, z=+{ExactPlacementZ:0}, visual=True, collision=True, sideWallSolidEdges={sideWallSolidEdges}/{sideWallExposedEdges}, sideWallCollision={sideWallCollisionPatches}, patches={exactPlacementPlan.PatchCount}{(string.IsNullOrWhiteSpace(sideWallSkip) ? "" : $", sideWallSkip={sideWallSkip}")}");
    AddTerrainReadiness(
        "move terrain face XYZ",
        "smoke-passed",
        $"Face {polygon.RuntimeKey}, X/Y/Z roundtrip plus visual/collision patches={exactPlacementPlan.PatchCount}",
        "Needs broader in-game testing across tall terrain and sector boundaries.");

    int waterTextureId = geometry.Polygons
        .Where(face => string.Equals(TerrainMaterialClassifier.NormalizeSurfaceName(face.Surface), "water", StringComparison.OrdinalIgnoreCase))
        .Select(face => face.TextureId)
        .Where(textureId => textureId >= 0)
        .GroupBy(textureId => textureId)
        .OrderByDescending(group => group.Count())
        .ThenBy(group => group.Key)
        .Select(group => group.Key)
        .FirstOrDefault(-1);
    TerrainPolygon? grassPolygon = geometry.Polygons.FirstOrDefault(face =>
        waterTextureId >= 0 &&
        face.SectorOffset >= 0 &&
        face.FaceOffset >= 0 &&
        sourceMappedKeys.Contains(face.RuntimeKey) &&
        string.Equals(TerrainMaterialClassifier.NormalizeSurfaceName(face.Surface), "grass", StringComparison.OrdinalIgnoreCase));
    if (grassPolygon != null)
    {
        string texturePath = Path.Combine(workspace.RootPath, "_local", "smoke", "terrain-texture-id-swap-edits.json");
        int originalTextureId = grassPolygon.TextureId;
        int originalTextureFaceCount = geometry.Polygons.Count(face => face.TextureId == originalTextureId);
        int waterTextureFaceCount = geometry.Polygons.Count(face => face.TextureId == waterTextureId);
        TerrainPolygon? waterSourceFace = geometry.Polygons.FirstOrDefault(face =>
            face.TextureId == waterTextureId &&
            string.Equals(TerrainMaterialClassifier.NormalizeSurfaceName(face.Surface), "water", StringComparison.OrdinalIgnoreCase));
        string copiedSurface = TerrainMaterialClassifier.NormalizeSurfaceName(waterSourceFace?.Surface ?? "water");
        ColorRgba copiedSurfaceColor = waterSourceFace?.SurfaceColor ?? ColorRgba.FromRgb(36, 76, 152);
        grassPolygon.ApplyTextureOverride(waterTextureId);
        grassPolygon.SetSurface(copiedSurface, copiedSurfaceColor, $"smoke copied look from texture {waterTextureId}");
        int textureSwapEditCount = await TerrainEditStore.SaveAsync(texturePath, [grassPolygon], "Stone Hill texture swap smoke");
        int remainingOriginalTextureFaces = geometry.Polygons.Count(face => face.TextureId == originalTextureId);
        int newWaterTextureFaceCount = geometry.Polygons.Count(face => face.TextureId == waterTextureId);
        TerrainPatchPlan texturePlan = TerrainPatchExporter.BuildPlan(
            sourceImage,
            DiscImageLocator.FindCueForImage(sourceImage),
            Path.Combine(workspace.RootPath, "_local", "terrain", "texture-swap-smoke.bin"),
            Path.Combine(workspace.RootPath, "_local", "terrain", "texture-swap-smoke.cue"),
            level,
            ramPath,
            sourceSearchPath,
            texturePath,
            "");
        bool hasTextureSwapPatch = texturePlan.Patches.Any(patch =>
            patch.RuntimeKey.Equals(grassPolygon.RuntimeKey, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(patch.Kind, "texture-id-word3", StringComparison.OrdinalIgnoreCase));
        if (textureSwapEditCount != 1 ||
            remainingOriginalTextureFaces != originalTextureFaceCount - 1 ||
            newWaterTextureFaceCount != waterTextureFaceCount + 1 ||
            !hasTextureSwapPatch)
        {
            throw new InvalidOperationException($"Stone Hill face-local in-game palette swap did not stay isolated: edits={textureSwapEditCount}, original={remainingOriginalTextureFaces}/{originalTextureFaceCount - 1}, target={newWaterTextureFaceCount}/{waterTextureFaceCount + 1}, patch={hasTextureSwapPatch}.");
        }
        string freshOverlayPath = Path.Combine(workspace.RootPath, "editor-cache", $"{level.Key}-runtime-scene-editor-overlay.json");
        if (!File.Exists(freshOverlayPath))
            freshOverlayPath = workspace.ResolveFile($"{level.Key}-runtime-scene-editor-overlay.json", "generated-research");
        GeometryCandidate freshGeometry = GeometryOverlayLoader.LoadFirstCandidate(freshOverlayPath);
        TerrainMaterialClassifier.Apply(level.Key, workspace.RootPath, freshGeometry);
        int loadedTextureSwapEdits = TerrainEditStore.Load(texturePath, freshGeometry.Polygons);
        TerrainPolygon? reloadedGrassFace = freshGeometry.Polygons.FirstOrDefault(face =>
            string.Equals(face.RuntimeKey, grassPolygon.RuntimeKey, StringComparison.OrdinalIgnoreCase));
        bool materialRoundtrip = loadedTextureSwapEdits == 1 &&
            reloadedGrassFace != null &&
            reloadedGrassFace.TextureId == waterTextureId &&
            string.Equals(TerrainMaterialClassifier.NormalizeSurfaceName(reloadedGrassFace.Surface), copiedSurface, StringComparison.OrdinalIgnoreCase) &&
            reloadedGrassFace.SurfaceColor == copiedSurfaceColor;
        if (!materialRoundtrip)
        {
            throw new InvalidOperationException($"Terrain look copy/paste material roundtrip failed: loaded={loadedTextureSwapEdits}, face={(reloadedGrassFace == null ? "missing" : reloadedGrassFace.RuntimeKey)}, texture={reloadedGrassFace?.TextureId}, surface={reloadedGrassFace?.Surface}.");
        }
        grassPolygon.ResetTerrainEdit();
        Console.WriteLine($"Stone Hill face-local in-game palette swap patch: grass face {grassPolygon.RuntimeKey}, texture {originalTextureId} shared={originalTextureFaceCount} -> water texture {waterTextureId}, texture-id patch=True, material-roundtrip=True, patches={texturePlan.PatchCount}");
        AddTerrainReadiness(
            "swap to in-game palette/art",
            "smoke-passed",
            $"Face {grassPolygon.RuntimeKey}, texture {originalTextureId} -> {waterTextureId}, texture-id patch=True, material-roundtrip=True",
            "This swaps texture ID/palette use; exact gameplay behavior remains separate from the material label.");
    }
    else
    {
        AddTerrainReadiness(
            "swap to in-game palette/art",
            "skipped",
            "No patchable grass-to-water face found in this smoke source",
            "Run against a cache with source-mapped grass and water textures.");
    }

    TerrainPolygon? sharedTexturePolygon = geometry.Polygons
        .Where(face => face.TextureId >= 0)
        .GroupBy(face => face.TextureId)
        .Where(group => group.Count() > 1)
        .OrderByDescending(group => group.Count())
        .ThenBy(group => group.Key)
        .Select(group => group.FirstOrDefault(face =>
            face.SectorOffset >= 0 &&
            face.FaceOffset >= 0 &&
            sourceMappedKeys.Contains(face.RuntimeKey)))
        .FirstOrDefault(face => face != null);
    if (sharedTexturePolygon != null)
    {
        int originalTextureId = sharedTexturePolygon.TextureId;
        int originalSharedCount = geometry.Polygons.Count(face => face.TextureId == originalTextureId);
        TerrainTextureSlot? localSlot = TerrainPatchExporter.FindUnusedTextureSlot(
            sourceImage,
            level,
            geometry.Polygons.Select(face => face.TextureId),
            preferBothDescriptorTiers: true);
        if (localSlot == null)
        {
            Console.WriteLine("Stone Hill single-face terrain recolor patch: no unused texture slot found; skipping.");
        }
        else
        {
            string faceLocalRoot = Path.Combine(workspace.RootPath, "_local", "smoke", "single-face-terrain-texture");
            Directory.CreateDirectory(faceLocalRoot);
            string faceLocalImage = Path.Combine(faceLocalRoot, $"stonehill-face-local-texture-{localSlot.TextureId:000}.png");
            await WriteSmokePngAsync(faceLocalImage, 64, 64);
            await CustomTerrainTextureStore.AddOrReplaceAsync(
                faceLocalRoot,
                level.Key,
                level.DisplayName,
                localSlot.TextureId,
                faceLocalImage,
                Path.GetFileName(faceLocalImage),
                "both",
                64,
                "generated-face-palette",
                "Face local smoke",
                "#285828",
                "#7CE87C");
            sharedTexturePolygon.ApplyTextureOverride(localSlot.TextureId);
            IReadOnlyList<CustomTerrainTextureImport> faceLocalImports = CustomTerrainTextureStore.Load(faceLocalRoot, level.Key);
            int previewedFaces = CustomTerrainTexturePreview.Apply(geometry, faceLocalImports);
            int remainingOriginalTextureFaces = geometry.Polygons.Count(face => face.TextureId == originalTextureId);
            string faceLocalEditsPath = Path.Combine(faceLocalRoot, "stonehill-face-local-terrain-edits.json");
            int faceLocalEditCount = await TerrainEditStore.SaveAsync(faceLocalEditsPath, [sharedTexturePolygon], "Stone Hill single-face local texture smoke");
            TerrainPatchPlan faceLocalPlan = TerrainPatchExporter.BuildPlan(
                sourceImage,
                DiscImageLocator.FindCueForImage(sourceImage),
                Path.Combine(faceLocalRoot, "single-face-texture-smoke.bin"),
                Path.Combine(faceLocalRoot, "single-face-texture-smoke.cue"),
                level,
                ramPath,
                sourceSearchPath,
                faceLocalEditsPath,
                CustomTerrainTextureStore.ManifestPath(faceLocalRoot, level.Key));
            bool hasTextureIdPatch = faceLocalPlan.Patches.Any(patch =>
                patch.RuntimeKey.Equals(sharedTexturePolygon.RuntimeKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(patch.Kind, "texture-id-word3", StringComparison.OrdinalIgnoreCase));
            bool hasBothCustomTextureTiers = faceLocalPlan.CustomTextureImports.Any(import => import.TextureId == localSlot.TextureId && import.DescriptorTier == "hqData") &&
                faceLocalPlan.CustomTextureImports.Any(import => import.TextureId == localSlot.TextureId && import.DescriptorTier == "hqDataClose");
            sharedTexturePolygon.ResetTerrainEdit();
            TerrainMaterialClassifier.Apply(level.Key, workspace.RootPath, geometry);
            if (faceLocalEditCount != 1 ||
                previewedFaces != 1 ||
                remainingOriginalTextureFaces != originalSharedCount - 1 ||
                !hasTextureIdPatch ||
                !hasBothCustomTextureTiers ||
                faceLocalPlan.CustomTextureBytePatchCount <= 0)
            {
                throw new InvalidOperationException($"Single-face terrain recolor did not stay isolated: edits={faceLocalEditCount}, preview={previewedFaces}, remainingOriginal={remainingOriginalTextureFaces}/{originalSharedCount - 1}, texturePatch={hasTextureIdPatch}, customTiers={hasBothCustomTextureTiers}, customPatches={faceLocalPlan.CustomTextureBytePatchCount}.");
            }

            Console.WriteLine($"Stone Hill single-face terrain recolor patch: face {sharedTexturePolygon.RuntimeKey}, texture {originalTextureId} shared={originalSharedCount} -> local texture {localSlot.TextureId}, texture-id patch=True, previewed={previewedFaces}, custom texture patches={faceLocalPlan.CustomTextureBytePatchCount}");
            AddTerrainReadiness(
                "custom single-face palette",
                "smoke-passed",
                $"Face {sharedTexturePolygon.RuntimeKey}, isolated local texture {localSlot.TextureId}, previewed={previewedFaces}, custom byte patches={faceLocalPlan.CustomTextureBytePatchCount}",
                "Custom art is patch-planned; final color fidelity still needs in-game visual checks.");

            TerrainPolygon? pasteTargetPolygon = geometry.Polygons.FirstOrDefault(face =>
                !ReferenceEquals(face, sharedTexturePolygon) &&
                face.TextureId >= 0 &&
                face.SectorOffset >= 0 &&
                face.FaceOffset >= 0 &&
                sourceMappedKeys.Contains(face.RuntimeKey));
            TerrainTextureSlot? pastedSlot = pasteTargetPolygon == null
                ? null
                : TerrainPatchExporter.FindUnusedTextureSlot(
                    sourceImage,
                    level,
                    geometry.Polygons.Select(face => face.TextureId).Append(localSlot.TextureId),
                    preferBothDescriptorTiers: true);
            if (pasteTargetPolygon == null || pastedSlot == null)
            {
                Console.WriteLine("Stone Hill paste custom face look patch: no second patchable face/local texture slot found; skipping.");
            }
            else
            {
                int pasteTargetOriginalTextureId = pasteTargetPolygon.TextureId;
                int pasteTargetOriginalTextureCount = geometry.Polygons.Count(face => face.TextureId == pasteTargetOriginalTextureId);
                IReadOnlyList<CustomTerrainTextureImport> copiedImports = CustomTerrainTextureStore.Load(faceLocalRoot, level.Key)
                    .Where(import => import.TextureId == localSlot.TextureId)
                    .ToArray();
                if (copiedImports.Count == 0)
                    throw new InvalidOperationException("Custom face look paste smoke could not find the source custom texture import.");

                foreach (CustomTerrainTextureImport import in copiedImports)
                {
                    await CustomTerrainTextureStore.AddOrReplaceAsync(
                        faceLocalRoot,
                        level.Key,
                        level.DisplayName,
                        pastedSlot.TextureId,
                        import.SourceImagePath,
                        import.SourceImageName,
                        import.DescriptorTier,
                        import.TileSize,
                        import.SourceKind,
                        import.PaletteName,
                        import.PaletteLowHex,
                        import.PaletteHighHex,
                        import.PaletteHexColors ?? Array.Empty<string>());
                }

                pasteTargetPolygon.ApplyTextureOverride(pastedSlot.TextureId);
                pasteTargetPolygon.SetSurface(sharedTexturePolygon.Surface, sharedTexturePolygon.SurfaceColor, $"smoke copied custom face look from {sharedTexturePolygon.RuntimeKey}");
                IReadOnlyList<CustomTerrainTextureImport> pastedImports = CustomTerrainTextureStore.Load(faceLocalRoot, level.Key);
                int pastedPreviewedFaces = CustomTerrainTexturePreview.Apply(geometry, pastedImports);
                int pasteRemainingOriginalTextureFaces = geometry.Polygons.Count(face => face.TextureId == pasteTargetOriginalTextureId);
                string pasteEditsPath = Path.Combine(faceLocalRoot, "stonehill-paste-custom-face-look-edits.json");
                int pasteEditCount = await TerrainEditStore.SaveAsync(pasteEditsPath, [pasteTargetPolygon], "Stone Hill paste custom face look smoke");
                TerrainPatchPlan pastePlan = TerrainPatchExporter.BuildPlan(
                    sourceImage,
                    DiscImageLocator.FindCueForImage(sourceImage),
                    Path.Combine(faceLocalRoot, "paste-custom-face-look-smoke.bin"),
                    Path.Combine(faceLocalRoot, "paste-custom-face-look-smoke.cue"),
                    level,
                    ramPath,
                    sourceSearchPath,
                    pasteEditsPath,
                    CustomTerrainTextureStore.ManifestPath(faceLocalRoot, level.Key));
                bool pasteHasTextureIdPatch = pastePlan.Patches.Any(patch =>
                    patch.RuntimeKey.Equals(pasteTargetPolygon.RuntimeKey, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(patch.Kind, "texture-id-word3", StringComparison.OrdinalIgnoreCase));
                bool pasteHasBothCustomTextureTiers = pastePlan.CustomTextureImports.Any(import => import.TextureId == pastedSlot.TextureId && import.DescriptorTier == "hqData") &&
                    pastePlan.CustomTextureImports.Any(import => import.TextureId == pastedSlot.TextureId && import.DescriptorTier == "hqDataClose");
                pasteTargetPolygon.ResetTerrainEdit();
                TerrainMaterialClassifier.Apply(level.Key, workspace.RootPath, geometry);
                if (pasteEditCount != 1 ||
                    pastedPreviewedFaces != 1 ||
                    pasteRemainingOriginalTextureFaces != pasteTargetOriginalTextureCount - 1 ||
                    !pasteHasTextureIdPatch ||
                    !pasteHasBothCustomTextureTiers ||
                    pastePlan.CustomTextureBytePatchCount <= 0)
                {
                    throw new InvalidOperationException($"Custom face look paste did not stay face-local: edits={pasteEditCount}, preview={pastedPreviewedFaces}, remainingOriginal={pasteRemainingOriginalTextureFaces}/{pasteTargetOriginalTextureCount - 1}, texturePatch={pasteHasTextureIdPatch}, customTiers={pasteHasBothCustomTextureTiers}, customPatches={pastePlan.CustomTextureBytePatchCount}.");
                }

                Console.WriteLine($"Stone Hill paste custom face look patch: face {pasteTargetPolygon.RuntimeKey}, texture {pasteTargetOriginalTextureId} -> local texture {pastedSlot.TextureId}, copied imports={copiedImports.Count}, texture-id patch=True, custom texture patches={pastePlan.CustomTextureBytePatchCount}");
                AddTerrainReadiness(
                    "paste custom face look",
                    "smoke-passed",
                    $"Face {pasteTargetPolygon.RuntimeKey}, copied custom import(s) to local texture {pastedSlot.TextureId}, custom byte patches={pastePlan.CustomTextureBytePatchCount}",
                    "This verifies patch planning; exact in-game color/texture appearance still needs visual testing.");
            }
        }
    }
    else
    {
        AddTerrainReadiness(
            "custom single-face palette",
            "skipped",
            "No shared, source-mapped texture face found in this smoke source",
            "Run against a level with an unused texture slot and shared texture face.");
    }

    string structuralPath = Path.Combine(workspace.RootPath, "_local", "smoke", "terrain-structural-edits.json");
    polygon.StageTerrainRemoval();
    int structuralEditCount = await TerrainEditStore.SaveAsync(structuralPath, [polygon], "Stone Hill structure smoke");
    polygon.ResetTerrainEdit();
    if (structuralEditCount != 1)
        throw new InvalidOperationException("Structural terrain removal should save as one terrain edit.");
    int loadedStructuralEdits = TerrainEditStore.Load(structuralPath, [polygon]);
    if (loadedStructuralEdits != 1 || !polygon.IsTerrainRemoved)
        throw new InvalidOperationException("Structural terrain removal did not roundtrip through TerrainEditStore.");
    TerrainPatchPlan structuralPlan = TerrainPatchExporter.BuildPlan(
        sourceImage,
        DiscImageLocator.FindCueForImage(sourceImage),
        Path.Combine(workspace.RootPath, "_local", "terrain", "structure-smoke.bin"),
        Path.Combine(workspace.RootPath, "_local", "terrain", "structure-smoke.cue"),
        level,
        ramPath,
        sourceSearchPath,
        structuralPath,
        "");
    polygon.ResetTerrainEdit();
    bool hasVisibleFaceRemoval = structuralPlan.Patches.Any(patch => patch.Kind.StartsWith("terrain-face-degenerate-", StringComparison.OrdinalIgnoreCase));
    bool hasCollisionRemoval = structuralPlan.Patches.Any(patch => string.Equals(patch.Kind, "collision-triangle-degenerate", StringComparison.OrdinalIgnoreCase));
    if (!hasVisibleFaceRemoval)
        throw new InvalidOperationException("Structural terrain removal should produce a visible face-degenerate patch.");
    if (!hasCollisionRemoval)
        throw new InvalidOperationException("Structural terrain removal should produce a matched collision-degenerate patch for the Stone Hill smoke face.");
    Console.WriteLine($"Stone Hill structural terrain removal patch: visible={hasVisibleFaceRemoval}, collision={hasCollisionRemoval}, patches={structuralPlan.PatchCount}");
    string preflightPrefix = Path.Combine(workspace.RootPath, "_local", "terrain", "preflight-smoke");
    string preflightBin = $"{preflightPrefix}.bin";
    string preflightCue = $"{preflightPrefix}.cue";
    string preflightPlan = $"{preflightPrefix}.terrain-patch-plan.json";
    File.Delete(preflightBin);
    File.Delete(preflightCue);
    File.Delete(preflightPlan);
    TerrainPatchResult preflightResult = await TerrainPatchExporter.ExportAsync(new TerrainPatchRequest(
        SourceImagePath: sourceImage,
        SourceCuePath: DiscImageLocator.FindCueForImage(sourceImage),
        OutputPrefix: preflightPrefix,
        Level: level,
        RamPath: ramPath,
        SourceSearchPath: sourceSearchPath,
        TerrainEditsPath: structuralPath,
        CustomTexturesPath: "",
        WriteImage: false));
    if (preflightResult.WroteImage || File.Exists(preflightBin) || File.Exists(preflightCue) || !File.Exists(preflightPlan) || preflightResult.Plan.PatchCount == 0)
        throw new InvalidOperationException("Terrain preflight should write only a patch plan, never a BIN/CUE.");
    Console.WriteLine($"Stone Hill terrain preflight no-write plan: patches={preflightResult.Plan.PatchCount}, wroteImage={preflightResult.WroteImage}, plan={Path.GetFileName(preflightResult.OutputPlanPath)}");
    AddTerrainReadiness(
        "remove terrain face",
        "smoke-passed",
        $"Face {polygon.RuntimeKey}, visible removal=True, collision removal=True, patches={structuralPlan.PatchCount}",
        "Removal neutralizes matched geometry; complex level holes still need playtesting.");

    TerrainPolygon? addCopyPolygon = geometry.Polygons.FirstOrDefault(face =>
        face.SectorOffset >= 0 &&
        face.VertexIndexes.Count > 0 &&
        string.Equals(face.Detail, "hp", StringComparison.OrdinalIgnoreCase) &&
        sourceMappedKeys.Contains(face.RuntimeKey) &&
        HasCollisionTriangleMatch(face, collisionTriangleKeys) &&
        sectorAppendSlack.TryGetValue(face.SectorOffset, out int slack) &&
        slack >= RequiredIndependentAddCopySlack(face));
    if (addCopyPolygon == null)
    {
        Console.WriteLine("Stone Hill structural terrain add-copy patch: no collision-matched sector slack found for independent copied vertices; skipping add smoke.");
        AddTerrainReadiness(
            "add copied terrain face",
            "skipped",
            "No collision-matched sector slack found in this smoke source",
            "Add-copy needs a source-mapped high-detail face plus enough same-model room for direct append or bounded sector shifting.");
    }
    else
    {
        string addCopyPath = Path.Combine(workspace.RootPath, "_local", "smoke", "terrain-add-copy-edits.json");
        int addCopySlack = sectorAppendSlack[addCopyPolygon.SectorOffset];
        int requiredAddCopySlack = RequiredIndependentAddCopySlack(addCopyPolygon);
        float[] addCopyDeltas = addCopyPolygon.OriginalZValues.Select((_, index) => 18f + (index * 5f)).ToArray();
        const float AddCopyXOffset = 96f;
        const float AddCopyYOffset = 32f;
        Vector2f[] addCopyXYDeltas = addCopyPolygon.OriginalPoints.Select(_ => new Vector2f(AddCopyXOffset, AddCopyYOffset)).ToArray();
        addCopyPolygon.ApplyTerrainVertexDeltas(addCopyDeltas);
        addCopyPolygon.ApplyTerrainVertexXYDeltas(addCopyXYDeltas);
        addCopyPolygon.StageTerrainAddClone();
        int addCopyEditCount = await TerrainEditStore.SaveAsync(addCopyPath, [addCopyPolygon], "Stone Hill add-copy smoke");
        addCopyPolygon.ResetTerrainEdit();
        if (addCopyEditCount != 1)
            throw new InvalidOperationException("Add-copy terrain should save as one terrain edit.");
        int loadedAddCopyEdits = TerrainEditStore.Load(addCopyPath, [addCopyPolygon]);
        bool loadedAddCopyOffset = loadedAddCopyEdits == 1 &&
            addCopyPolygon.IsTerrainAddClone &&
            addCopyPolygon.TerrainVertexXYDeltas().All(delta =>
                Math.Abs(delta.X - AddCopyXOffset) <= 0.001f &&
                Math.Abs(delta.Y - AddCopyYOffset) <= 0.001f);
        addCopyPolygon.ResetTerrainEdit();
        if (!loadedAddCopyOffset)
            throw new InvalidOperationException("Add-copy terrain should roundtrip the moved copied-vertex positions.");
        TerrainPatchPlan addCopyPlan = TerrainPatchExporter.BuildPlan(
            sourceImage,
            DiscImageLocator.FindCueForImage(sourceImage),
            Path.Combine(workspace.RootPath, "_local", "terrain", "add-copy-smoke.bin"),
            Path.Combine(workspace.RootPath, "_local", "terrain", "add-copy-smoke.cue"),
            level,
            ramPath,
            sourceSearchPath,
            addCopyPath,
            "");
        bool hasVertexCountPatch = addCopyPlan.Patches.Any(patch => string.Equals(patch.Kind, "terrain-vertex-count-hp", StringComparison.OrdinalIgnoreCase));
        bool hasFaceCountPatch = addCopyPlan.Patches.Any(patch => string.Equals(patch.Kind, "terrain-face-count-hp", StringComparison.OrdinalIgnoreCase));
        TerrainPatch? repackPatch = addCopyPlan.Patches.FirstOrDefault(patch => string.Equals(patch.Kind, "terrain-sector-repack-hp-add-copy", StringComparison.OrdinalIgnoreCase));
        bool hasCollisionAddCopyPatch = addCopyPlan.Patches.Any(patch => string.Equals(patch.Kind, "collision-triangle-add-copy", StringComparison.OrdinalIgnoreCase));
        bool hasOriginalVisualPatch = addCopyPlan.Patches.Any(patch =>
            patch.RuntimeKey.Equals(addCopyPolygon.RuntimeKey, StringComparison.OrdinalIgnoreCase) &&
            patch.Kind.StartsWith("visual-", StringComparison.OrdinalIgnoreCase));
        bool hasFallbackAppendPatch = addCopyPlan.Patches.Any(patch => string.Equals(patch.Kind, "terrain-face-append-hp", StringComparison.OrdinalIgnoreCase));
        if (!hasVertexCountPatch || !hasFaceCountPatch || repackPatch == null)
            throw new InvalidOperationException("Add-copy terrain should produce high-detail vertex-count, face-count, and sector-repack patches.");
        if (!hasCollisionAddCopyPatch)
            throw new InvalidOperationException("Collision-matched add-copy terrain should repurpose a same-cell degenerate collision triangle.");
        if (repackPatch.ByteLength <= 16)
            throw new InvalidOperationException("Add-copy terrain sector repack should write cloned vertices plus the copied face, not only a face record.");
        if (hasOriginalVisualPatch)
            throw new InvalidOperationException("Add-copy terrain height edits should be applied to the copied face, not patched back onto the source face.");
        if (hasFallbackAppendPatch)
            throw new InvalidOperationException("Add-copy terrain smoke selected a sector with enough slack, so it should not use the fallback append-only patch.");
        Console.WriteLine($"Stone Hill structural terrain add-copy patch: face {addCopyPolygon.RuntimeKey}, dx=+{AddCopyXOffset:0}, dy=+{AddCopyYOffset:0}, slack={addCopySlack}/{requiredAddCopySlack}, vertexCount={hasVertexCountPatch}, faceCount={hasFaceCountPatch}, repack=True, collisionAdd=True, originalVisual=False, patches={addCopyPlan.PatchCount}");
        AddTerrainReadiness(
            "add copied terrain face",
            "smoke-passed",
            $"Face {addCopyPolygon.RuntimeKey}, slack={addCopySlack}/{requiredAddCopySlack}, repack=True, collisionAdd=True, source face unchanged",
            "Packed sectors can now use bounded same-model shifting, but in-game beta testing is still needed around large copied structures.");

        string sideWallPath = Path.Combine(workspace.RootPath, "_local", "smoke", "terrain-side-wall-edits.json");
        int sideWallOriginalTextureId = addCopyPolygon.TextureId;
        int sideWallTextureId = waterTextureId >= 0 && waterTextureId != sideWallOriginalTextureId
            ? waterTextureId
            : geometry.Polygons
                .Select(face => face.TextureId)
                .Where(textureId => textureId >= 0 && textureId != sideWallOriginalTextureId)
                .Distinct()
                .Order()
                .FirstOrDefault(-1);
        if (sideWallTextureId < 0)
            throw new InvalidOperationException("Side-wall terrain texture smoke needs a second texture id to borrow.");
        addCopyPolygon.ApplyTerrainDeltaZ(18f);
        addCopyPolygon.ApplyTextureOverride(sideWallTextureId);
        int sideWallEditCount = await TerrainEditStore.SaveAsync(sideWallPath, [addCopyPolygon], "Stone Hill side-wall smoke");
        addCopyPolygon.ResetTerrainEdit();
        if (sideWallEditCount != 1)
            throw new InvalidOperationException("Side-wall terrain edit should save as one terrain edit.");
        TerrainPatchPlan sideWallPlan = TerrainPatchExporter.BuildPlan(
            sourceImage,
            DiscImageLocator.FindCueForImage(sourceImage),
            Path.Combine(workspace.RootPath, "_local", "terrain", "side-wall-smoke.bin"),
            Path.Combine(workspace.RootPath, "_local", "terrain", "side-wall-smoke.cue"),
            level,
            ramPath,
            sourceSearchPath,
            sideWallPath,
            "");
        int sideWallExposedEdgeCount = sideWallPlan.TerrainSideWalls.Sum(summary => summary.ExposedEdgeCount);
        int sideWallSolidEdgeCount = sideWallPlan.TerrainSideWalls.Sum(summary => summary.EmittedFaceCount);
        int sideWallCollisionPatchCount = sideWallPlan.TerrainSideWalls.Sum(summary => summary.CollisionTriangleCount);
        int sideWallSkippedEdgeCount = sideWallPlan.TerrainSideWalls.Sum(summary => summary.SkippedEdgeCount);
        string sideWallAddSkip = string.Join(" | ", sideWallPlan.SkippedEdits
            .Where(skip => skip.Contains("side wall", StringComparison.OrdinalIgnoreCase) || skip.Contains("side-wall", StringComparison.OrdinalIgnoreCase))
            .Take(3));
        bool sideWallFullCoverage = sideWallExposedEdgeCount > 0 &&
            sideWallSolidEdgeCount == sideWallExposedEdgeCount &&
            sideWallCollisionPatchCount >= sideWallSolidEdgeCount * 2;
        bool sideWallPartialCoverage = sideWallSolidEdgeCount > 0;
        bool sideWallTextureCoverage = TryGetSideWallAppendedTextureCoverage(sideWallPlan, sideWallSolidEdgeCount, sideWallTextureId, out int sideWallTexturedFaces);
        bool sideWallSummaryTextureCoverage = sideWallPlan.TerrainSideWalls.Any(summary => summary.TextureIds.Contains(sideWallTextureId));
        if (sideWallFullCoverage && !sideWallTextureCoverage)
            throw new InvalidOperationException($"Side-wall terrain texture smoke did not carry edited texture {sideWallTextureId} into all appended wall faces ({sideWallTexturedFaces}/{sideWallSolidEdgeCount}).");
        if (sideWallFullCoverage && !sideWallSummaryTextureCoverage)
            throw new InvalidOperationException($"Side-wall terrain summary did not report inherited texture {sideWallTextureId}.");
        Console.WriteLine($"Stone Hill terrain side-wall patch: face {addCopyPolygon.RuntimeKey}, dz=+18, texture {sideWallOriginalTextureId}->{sideWallTextureId}, sideWallSolidEdges={sideWallSolidEdgeCount}/{sideWallExposedEdgeCount}, sideWallCollision={sideWallCollisionPatchCount}, sideWallTextures={sideWallTexturedFaces}/{sideWallSolidEdgeCount}, sideWallSummaryTexture={sideWallSummaryTextureCoverage}, sideWallSkippedEdges={sideWallSkippedEdgeCount}, patches={sideWallPlan.PatchCount}{(string.IsNullOrWhiteSpace(sideWallAddSkip) ? "" : $", sideWallSkip={sideWallAddSkip}")}");
        AddTerrainReadiness(
            "solid terrain side walls",
            sideWallFullCoverage ? "smoke-passed" : sideWallPartialCoverage ? "partial" : "skipped",
            sideWallPartialCoverage
                ? $"Face {addCopyPolygon.RuntimeKey}, solid side-wall edges={sideWallSolidEdgeCount}/{sideWallExposedEdgeCount}, collision triangles={sideWallCollisionPatchCount}"
                : $"Face {addCopyPolygon.RuntimeKey}, no safe side wall emitted. {sideWallAddSkip}",
            sideWallFullCoverage
                ? "This face has full exposed-edge side-wall coverage in patch planning; still needs in-game playtest around the vertical sides."
                : "Side walls still require more collision lookup capacity; full collision-table relocation is the next requirement for all exposed edges.");
        AddTerrainReadiness(
            "textured terrain side walls",
            sideWallFullCoverage && sideWallTextureCoverage && sideWallSummaryTextureCoverage ? "smoke-passed" : sideWallPartialCoverage ? "partial" : "skipped",
            sideWallTextureCoverage && sideWallSummaryTextureCoverage
                ? $"Face {addCopyPolygon.RuntimeKey}, side-wall texture {sideWallOriginalTextureId}->{sideWallTextureId}, appended faces={sideWallTexturedFaces}/{sideWallSolidEdgeCount}, summary=True"
                : $"Face {addCopyPolygon.RuntimeKey}, side-wall texture coverage={sideWallTexturedFaces}/{sideWallSolidEdgeCount}, summary={sideWallSummaryTextureCoverage}. {sideWallAddSkip}",
            sideWallFullCoverage && sideWallTextureCoverage && sideWallSummaryTextureCoverage
                ? "The same saved height edit can also swap texture id and the generated wall faces inherit and report that edited texture."
                : "Side-wall texture coverage depends on emitted side-wall faces.");
    }

    WriteTerrainEditorReadinessReport(workspace.RootPath, terrainReadinessRows);
}

void WriteTerrainEditorReadinessReport(string workspaceRoot, IReadOnlyList<TerrainEditorReadinessRow> rows)
{
    string reportPath = Path.Combine(workspaceRoot, "_local", "smoke", "terrain-editor-readiness.md");
    Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
    int passed = rows.Count(row => string.Equals(row.Status, "smoke-passed", StringComparison.OrdinalIgnoreCase));
    int partial = rows.Count(row => string.Equals(row.Status, "partial", StringComparison.OrdinalIgnoreCase));
    int skipped = rows.Count(row => string.Equals(row.Status, "skipped", StringComparison.OrdinalIgnoreCase));
    StringBuilder builder = new();
    builder.AppendLine("# Terrain Editor Readiness");
    builder.AppendLine();
    builder.AppendLine("This file is generated by the smoke suite. It tracks the concrete terrain-editing paths that are currently proven by save/load and patch-planning checks.");
    builder.AppendLine();
    builder.AppendLine($"- Passed checks: {passed}");
    builder.AppendLine($"- Partial checks: {partial}");
    builder.AppendLine($"- Skipped checks: {skipped}");
    builder.AppendLine("- In-game validation is still a separate beta pass; this report proves editor persistence and patch planning, not final gameplay feel.");
    builder.AppendLine();
    builder.AppendLine("| Feature | Status | Evidence | Remaining risk |");
    builder.AppendLine("|---|---|---|---|");
    foreach (TerrainEditorReadinessRow row in rows)
    {
        builder.AppendLine($"| {EscapeMarkdownCell(row.Feature)} | {EscapeMarkdownCell(row.Status)} | {EscapeMarkdownCell(row.Evidence)} | {EscapeMarkdownCell(row.RemainingRisk)} |");
    }

    File.WriteAllText(reportPath, builder.ToString());
    Console.WriteLine($"Terrain editor readiness: {passed} passed, {partial} partial, {skipped} skipped, report={reportPath}");
}

static string EscapeMarkdownCell(string value)
{
    return (value ?? "").Replace("|", "\\|", StringComparison.Ordinal);
}

void ReportTerrainVertexSeamAlignerSmoke()
{
    TerrainPolygon left = new(
        [new Vector2f(0, 0), new Vector2f(64, 0), new Vector2f(0, 64)],
        [0, 0, 0],
        1,
        0,
        0,
        "hp",
        new ColorRgba(64, 128, 64, 255));
    TerrainPolygon right = new(
        [new Vector2f(64, 0), new Vector2f(64, 64), new Vector2f(0, 64)],
        [0, 0, 0],
        1,
        0,
        1,
        "hp",
        new ColorRgba(64, 128, 64, 255));

    Dictionary<TerrainPolygon, float[]> pending = new()
    {
        [left] = [0, 24, 8],
        [right] = [12, 0, 20]
    };
    TerrainVertexSeamAlignResult result = TerrainVertexSeamAligner.Align(pending);
    if (result.SyncedVertexGroups != 2 || result.AdjustedVertices != 4)
        throw new InvalidOperationException("Terrain seam aligner did not report the expected shared vertex adjustments.");
    if (Math.Abs(pending[left][1] - 18) > 0.001f ||
        Math.Abs(pending[right][0] - 18) > 0.001f ||
        Math.Abs(pending[left][2] - 14) > 0.001f ||
        Math.Abs(pending[right][2] - 14) > 0.001f)
        throw new InvalidOperationException("Terrain seam aligner did not average shared vertex deltas.");

    Dictionary<TerrainPolygon, float[]> safePending = new()
    {
        [left] = [0, 24, 8],
        [right] = [12, 0, 20]
    };
    Dictionary<TerrainPolygon, IReadOnlySet<int>> touchedOnly = new()
    {
        [left] = new HashSet<int> { 1, 2 },
        [right] = new HashSet<int> { 0 }
    };
    TerrainVertexSeamAlignResult safeResult = TerrainVertexSeamAligner.Align(safePending, touchedOnly);
    if (safeResult.SyncedVertexGroups != 1 || safeResult.AdjustedVertices != 2)
        throw new InvalidOperationException("Terrain seam aligner did not respect touched vertex filtering.");
    if (Math.Abs(safePending[left][1] - 18) > 0.001f ||
        Math.Abs(safePending[right][0] - 18) > 0.001f ||
        Math.Abs(safePending[left][2] - 8) > 0.001f ||
        Math.Abs(safePending[right][2] - 20) > 0.001f)
        throw new InvalidOperationException("Terrain seam aligner touched vertices outside the filtered brush set.");

    Console.WriteLine($"Terrain seam aligner: {result.SyncedVertexGroups} shared vertex group(s), {result.AdjustedVertices} adjusted vertex delta(s); filtered {safeResult.SyncedVertexGroups}/{safeResult.AdjustedVertices}");
}

void ReportTerrainBrushPathSamplerSmoke()
{
    IReadOnlyList<TerrainBrushPathSample> shortMove = TerrainBrushPathSampler.Build(
        new Vector2f(0, 0),
        new Vector2f(13, 0),
        14);
    if (shortMove.Count != 1 ||
        Math.Abs(shortMove[0].Center.X - 13) > 0.001 ||
        Math.Abs(shortMove[0].StrengthScale - (13.0 / 14.0)) > 0.001)
    {
        throw new InvalidOperationException("Terrain brush path sampler did not emit a partial-strength sub-step sample.");
    }

    TerrainBrushPathSample? ignoredJitter = TerrainBrushPathSampler.BuildFinalResidual(
        new Vector2f(0, 0),
        new Vector2f(3, 0),
        40);
    TerrainBrushPathSample? finalResidual = TerrainBrushPathSampler.BuildFinalResidual(
        new Vector2f(0, 0),
        new Vector2f(8, 0),
        40);
    if (ignoredJitter != null ||
        finalResidual == null ||
        Math.Abs(finalResidual.Value.Center.X - 8) > 0.001 ||
        Math.Abs(finalResidual.Value.StrengthScale - 0.2) > 0.001)
    {
        throw new InvalidOperationException("Terrain brush final residual sampler did not keep short release strokes smooth.");
    }

    IReadOnlyList<TerrainBrushPathSample> samples = TerrainBrushPathSampler.Build(
        new Vector2f(0, 0),
        new Vector2f(140, 0),
        40);
    if (samples.Count != 4 ||
        Math.Abs(samples[0].Center.X - 40) > 0.001 ||
        Math.Abs(samples[1].Center.X - 80) > 0.001 ||
        Math.Abs(samples[2].Center.X - 120) > 0.001 ||
        Math.Abs(samples[3].Center.X - 140) > 0.001 ||
        samples.Take(3).Any(sample => Math.Abs(sample.StrengthScale - 1.0) > 0.001) ||
        Math.Abs(samples[3].StrengthScale - 0.5) > 0.001)
    {
        throw new InvalidOperationException("Terrain brush path sampler did not produce distance-weighted samples.");
    }

    IReadOnlyList<TerrainBrushSmoothTarget> smoothTargets = TerrainBrushLocalSmoother.BuildLocalAverageTargets(
        [
            new TerrainBrushVertexSample(0, 0, new Vector2f(0, 0), 42),
            new TerrainBrushVertexSample(0, 1, new Vector2f(32, 0), 0),
            new TerrainBrushVertexSample(0, 2, new Vector2f(-32, 0), 0),
            new TerrainBrushVertexSample(1, 0, new Vector2f(0, 32), 4),
            new TerrainBrushVertexSample(1, 1, new Vector2f(180, 0), 88)
        ],
        new Vector2f(0, 0),
        72,
        80);
    TerrainBrushSmoothTarget centerTarget = smoothTargets.Single(target => target.FaceIndex == 0 && target.VertexIndex == 0);
    TerrainBrushSmoothTarget neighborTarget = smoothTargets.Single(target => target.FaceIndex == 0 && target.VertexIndex == 1);
    bool excludedOutsideBrush = smoothTargets.All(target => target.FaceIndex != 1 || target.VertexIndex != 1);
    if (centerTarget.TargetZ >= 12 ||
        centerTarget.TargetZ <= 6 ||
        neighborTarget.TargetZ <= 0 ||
        !excludedOutsideBrush ||
        centerTarget.NeighborCount < 4)
    {
        throw new InvalidOperationException("Terrain brush local smoother did not relax a spike toward nearby editable terrain.");
    }

    TerrainBrushPreviewStats editablePreview = TerrainBrushPreviewAnalyzer.Summarize(
        [TerrainBrushPreviewVertexKind.Editable, TerrainBrushPreviewVertexKind.Editable],
        clipped: false);
    TerrainBrushPreviewStats mixedPreview = TerrainBrushPreviewAnalyzer.Summarize(
        [TerrainBrushPreviewVertexKind.Editable, TerrainBrushPreviewVertexKind.VisualOnly, TerrainBrushPreviewVertexKind.VisualOnly],
        clipped: true);
    TerrainBrushPreviewStats visualOnlyPreview = TerrainBrushPreviewAnalyzer.Summarize(
        [TerrainBrushPreviewVertexKind.VisualOnly, TerrainBrushPreviewVertexKind.VisualOnly],
        clipped: false);
    if (TerrainBrushPreviewAnalyzer.FormatSummary(editablePreview) != "2 vertices" ||
        TerrainBrushPreviewAnalyzer.FormatSummary(mixedPreview) != "1 playable + 2+ visual" ||
        TerrainBrushPreviewAnalyzer.FormatSummary(visualOnlyPreview) != "2 visual-only")
    {
        throw new InvalidOperationException("Terrain brush preview analyzer did not summarize playable vs visual-only vertices.");
    }

    Console.WriteLine($"Terrain brush path sampler: short move strength {shortMove[0].StrengthScale:0.###}, {samples.Count} weighted sample(s), final strength {samples[^1].StrengthScale:0.###}, release residual {finalResidual.Value.StrengthScale:0.###}; local smooth spike {centerTarget.TargetZ:0.##}; preview {TerrainBrushPreviewAnalyzer.FormatSummary(mixedPreview)}");
}

void ReportTerrainCopyPlacementSmoke()
{
    Vector2f wideBeside = TerrainCopyPlacementSuggester.SuggestedBesideOffset(
        [
            new Vector2f(0, 0),
            new Vector2f(100, 0),
            new Vector2f(100, 50),
            new Vector2f(0, 50)
        ]);
    if (Math.Abs(wideBeside.X) > 0.001f ||
        Math.Abs(wideBeside.Y) < 190 ||
        Math.Abs(wideBeside.Y) > 202)
    {
        throw new InvalidOperationException("Terrain add-copy placement should offset a wide face outside one of its long edges.");
    }

    Vector2f tallBeside = TerrainCopyPlacementSuggester.SuggestedBesideOffset(
        [
            new Vector2f(0, 0),
            new Vector2f(40, 0),
            new Vector2f(40, 180),
            new Vector2f(0, 180)
        ]);
    if (tallBeside.X < 270 ||
        Math.Abs(tallBeside.Y) > 0.001f)
    {
        throw new InvalidOperationException("Terrain add-copy placement should offset a tall face beside its vertical edge.");
    }

    Vector2f fallback = TerrainCopyPlacementSuggester.SuggestedBesideOffset([new Vector2f(12, 8)]);
    if (Math.Abs(fallback.X - 64) > 0.001f || Math.Abs(fallback.Y) > 0.001f)
        throw new InvalidOperationException("Terrain add-copy placement should fall back to a safe default offset for degenerate faces.");

    Console.WriteLine($"Terrain add-copy placement: wide ({wideBeside.X:0.#},{wideBeside.Y:0.#}), tall ({tallBeside.X:0.#},{tallBeside.Y:0.#}), fallback ({fallback.X:0.#},{fallback.Y:0.#})");
}

void ReportTerrainTopSurfaceSnapSmoke()
{
    TerrainPolygon lower = new(
        [
            new Vector2f(0, 0),
            new Vector2f(100, 0),
            new Vector2f(100, 100),
            new Vector2f(0, 100)
        ],
        [0, 0, 0, 0],
        1,
        0,
        0,
        "hp",
        ColorRgba.FromRgb(64, 128, 64));
    TerrainPolygon upper = new(
        [
            new Vector2f(0, 0),
            new Vector2f(100, 0),
            new Vector2f(100, 100),
            new Vector2f(0, 100)
        ],
        [96, 96, 96, 96],
        1,
        0,
        1,
        "hp",
        ColorRgba.FromRgb(96, 160, 96));
    IReadOnlyList<TerrainPolygon> stacked = [lower, upper];

    if (!TerrainSnapper.TryFindZAt(stacked, 50, 50, 0, out float preferredZ, preferredTerrainIndex: 0) ||
        Math.Abs(preferredZ) > 0.001f)
    {
        throw new InvalidOperationException("Terrain snap should preserve the preferred face when top-surface snap is not requested.");
    }

    if (!TerrainSnapper.TryFindZAt(stacked, 50, 50, 0, out float topZ, preferredTerrainIndex: 0, preferTopSurface: true) ||
        Math.Abs(topZ - 96) > 0.001f)
    {
        throw new InvalidOperationException("Terrain top-surface snap should choose the highest overlapping face even when the preferred face is lower.");
    }

    Console.WriteLine($"Terrain top-surface snap: lower preferred Z {preferredZ:0.#}, top requested Z {topZ:0.#}");
}

string? ReadFirstTerrainSourceKey(string sourceSearchPath)
{
    using FileStream stream = File.OpenRead(sourceSearchPath);
    using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(stream);
    if (!document.RootElement.TryGetProperty("results", out System.Text.Json.JsonElement results)
        || results.ValueKind != System.Text.Json.JsonValueKind.Array)
        return null;

    foreach (System.Text.Json.JsonElement result in results.EnumerateArray())
    {
        string key = result.TryGetProperty("edit", out System.Text.Json.JsonElement edit)
            ? edit.GetString() ?? ""
            : "";
        if (!string.IsNullOrWhiteSpace(key))
            return key;
    }

    return null;
}

IEnumerable<string> ReadTerrainSourceKeys(string sourceSearchPath)
{
    if (!File.Exists(sourceSearchPath))
        yield break;

    using FileStream stream = File.OpenRead(sourceSearchPath);
    using JsonDocument document = JsonDocument.Parse(stream);
    if (!document.RootElement.TryGetProperty("results", out JsonElement results) || results.ValueKind != JsonValueKind.Array)
        yield break;

    foreach (JsonElement item in results.EnumerateArray())
    {
        string key = ReadJsonString(item, "edit");
        if (!string.IsNullOrWhiteSpace(key))
            yield return key;
    }
}

Dictionary<int, TerrainSectorSourceLayout> BuildTerrainSectorSourceLayouts(TerrainSourceSearchReport report)
{
    Dictionary<int, (long WadOffset, int Size)> sectors = new();
    foreach (TerrainSourceSearchEntry entry in report.Results)
    {
        if (entry.FullSectorHits.Count == 0)
            continue;

        int sectorOffset = (int)ParseFlexibleLong(entry.SectorOffset);
        if (sectors.ContainsKey(sectorOffset))
            continue;

        long wadOffset = ParseFlexibleLong(entry.FullSectorHits[0].WadOffset);
        sectors[sectorOffset] = (wadOffset, entry.SectorSizeBytes);
    }

    long[] wadOffsets = sectors.Values
        .Select(value => value.WadOffset)
        .Distinct()
        .OrderBy(value => value)
        .ToArray();
    Dictionary<int, TerrainSectorSourceLayout> result = new();
    foreach (KeyValuePair<int, (long WadOffset, int Size)> pair in sectors)
    {
        int sectorOffset = pair.Key;
        long wadOffset = pair.Value.WadOffset;
        int size = pair.Value.Size;
        long next = wadOffsets.FirstOrDefault(value => value > wadOffset);
        if (next <= 0)
        {
            result[sectorOffset] = new TerrainSectorSourceLayout(sectorOffset, wadOffset, size, -1, 0);
            continue;
        }

        long slack = next - (wadOffset + size);
        int slackBytes = slack <= 0 ? 0 : slack > int.MaxValue ? int.MaxValue : (int)slack;
        result[sectorOffset] = new TerrainSectorSourceLayout(sectorOffset, wadOffset, size, next, slackBytes);
    }

    return result;
}

Dictionary<int, int> BuildTerrainSectorAppendSlack(TerrainSourceSearchReport report) =>
    BuildTerrainSectorSourceLayouts(report).ToDictionary(pair => pair.Key, pair => pair.Value.SlackBytes);

bool TryGetSmokeArchiveEntry(string wadAnalysisPath, LevelDefinition level, out SmokeArchiveEntry? entry)
{
    entry = null;
    if (!File.Exists(wadAnalysisPath) || level.SourceWadEntry < 0)
        return false;

    try
    {
        using FileStream stream = File.OpenRead(wadAnalysisPath);
        using JsonDocument document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("entries", out JsonElement entries) ||
            entries.ValueKind != JsonValueKind.Array)
            return false;

        foreach (JsonElement item in entries.EnumerateArray())
        {
            int index = ReadJsonInt32(item, "index", -1);
            if (index != level.SourceWadEntry)
                continue;

            long offset = ReadJsonInt64(item, "offset", -1);
            long size = ReadJsonInt64(item, "size", -1);
            if (offset < 0 || size <= 0)
                return false;

            entry = new SmokeArchiveEntry(index, offset, size);
            return true;
        }
    }
    catch (IOException)
    {
        return false;
    }
    catch (InvalidOperationException)
    {
        return false;
    }
    catch (JsonException)
    {
        return false;
    }

    return false;
}

SmokeAssetSubfileInfo? TryGetSmokeAssetSubfileInfo(string wadAnalysisPath, LevelDefinition level, int subfileIndex)
{
    if (!File.Exists(wadAnalysisPath) || level.SourceWadEntry < 0)
        return null;

    try
    {
        using FileStream stream = File.OpenRead(wadAnalysisPath);
        using JsonDocument document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("entries", out JsonElement entries) ||
            entries.ValueKind != JsonValueKind.Array)
            return null;

        foreach (JsonElement entry in entries.EnumerateArray())
        {
            if (ReadJsonInt32(entry, "index", -1) != level.SourceWadEntry)
                continue;

            long assetOffset = ReadJsonInt64(entry, "offset", -1);
            long assetSize = ReadJsonInt64(entry, "size", -1);
            if (assetOffset < 0 || assetSize <= 0 ||
                !entry.TryGetProperty("level", out JsonElement levelElement) ||
                !levelElement.TryGetProperty("subfiles", out JsonElement subfiles) ||
                subfiles.ValueKind != JsonValueKind.Array)
                return null;

            foreach (JsonElement subfile in subfiles.EnumerateArray())
            {
                if (ReadJsonInt32(subfile, "index", -1) != subfileIndex)
                    continue;

                long subfileOffset = ReadJsonInt64(subfile, "offset", -1);
                long subfileSize = ReadJsonInt64(subfile, "size", -1);
                if (subfileOffset < 0 || subfileSize <= 0)
                    return null;

                return new SmokeAssetSubfileInfo(
                    AssetWadEntry: level.SourceWadEntry,
                    SubfileIndex: subfileIndex,
                    AssetWadOffset: assetOffset,
                    AssetSize: assetSize,
                    SubfileOffset: subfileOffset,
                    SubfileSize: subfileSize);
            }

            return null;
        }
    }
    catch (IOException)
    {
        return null;
    }
    catch (InvalidOperationException)
    {
        return null;
    }

    return null;
}

int RequiredIndependentAddCopySlack(TerrainPolygon face)
{
    IReadOnlyList<int> vertexIndexes = NormalizeSmokeVertexIndexes(face.VertexIndexes, face.OriginalZValues.Length);
    int uniqueVertexCount = vertexIndexes.Distinct().Count();
    return uniqueVertexCount <= 0 ? int.MaxValue : (uniqueVertexCount * 4) + 16;
}

int AddCopyCandidateRank(bool hasIndependentVertices, bool hasCollisionPatch, bool hasFallbackAppend, int patchCount)
{
    if (hasIndependentVertices && hasCollisionPatch)
        return 4;
    if (hasIndependentVertices)
        return 3;
    return patchCount > 0 ? 1 : 0;
}

IReadOnlyList<int> NormalizeSmokeVertexIndexes(IReadOnlyList<int> indexes, int zValueCount)
{
    if (indexes.Count <= zValueCount)
        return indexes;

    List<int> result = new();
    HashSet<int> seen = new();
    foreach (int index in indexes)
    {
        if (!seen.Add(index))
            continue;

        result.Add(index);
        if (result.Count == zValueCount)
            break;
    }

    return result.Count == zValueCount ? result : indexes.Take(zValueCount).ToArray();
}

sealed class WeakIdentityGroup
{
    public WeakIdentityGroup(string key)
    {
        Key = key;
    }

    public string Key { get; }
    public int Count { get; set; }
    public List<string> Levels { get; } = new();
    public List<string> Samples { get; } = new();
}

sealed class PrecisionIdentityGroup
{
    public PrecisionIdentityGroup(string key, string label, string kind, string evidence)
    {
        Key = key;
        Label = label;
        Kind = kind;
        Evidence = evidence;
    }

    public string Key { get; }
    public string Label { get; }
    public string Kind { get; }
    public string Evidence { get; }
    public int Count { get; set; }
    public HashSet<string> Levels { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<PrecisionIdentitySample> Samples { get; } = new();
    public Dictionary<string, int> LabelCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Dictionary<string, int>> LabelCountsByLevel { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string PreferredLabel => PreferredLabelFrom(LabelCounts, Label);
    public string LabelSummary => LabelCounts.Count <= 1 ? PreferredLabel : FormatLabelCounts(LabelCounts);

    public void AddOccurrence(LevelDefinition level, Moby moby)
    {
        Count++;
        Levels.Add(level.DisplayName);
        Increment(LabelCounts, moby.DisplayLabel);

        if (!LabelCountsByLevel.TryGetValue(level.Key, out Dictionary<string, int>? levelCounts))
        {
            levelCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            LabelCountsByLevel[level.Key] = levelCounts;
        }
        Increment(levelCounts, moby.DisplayLabel);

        if (Samples.Count < 8)
        {
            Samples.Add(new PrecisionIdentitySample(
                level.Key,
                level.DisplayName,
                moby.TrueIndex,
                moby.Position.X,
                moby.Position.Y,
                moby.Position.Z));
        }
    }

    public string PreferredLabelForLevel(string levelKey)
    {
        return LabelCountsByLevel.TryGetValue(levelKey, out Dictionary<string, int>? levelCounts)
            ? PreferredLabelFrom(levelCounts, PreferredLabel)
            : PreferredLabel;
    }

    public string PreferredLabelForSamples(IEnumerable<PrecisionIdentitySample> samples)
    {
        Dictionary<string, int> combined = new(StringComparer.OrdinalIgnoreCase);
        foreach (PrecisionIdentitySample sample in samples)
        {
            if (!LabelCountsByLevel.TryGetValue(sample.LevelKey, out Dictionary<string, int>? levelCounts))
                continue;

            foreach ((string label, int count) in levelCounts)
                combined[label] = combined.TryGetValue(label, out int existing) ? existing + count : count;
        }

        return combined.Count == 0 ? PreferredLabel : PreferredLabelFrom(combined, PreferredLabel);
    }

    private static void Increment(IDictionary<string, int> counts, string label)
    {
        string key = string.IsNullOrWhiteSpace(label) ? "Unlabeled" : label.Trim();
        counts[key] = counts.TryGetValue(key, out int count) ? count + 1 : 1;
    }

    private static string PreferredLabelFrom(IReadOnlyDictionary<string, int> counts, string fallback)
    {
        return counts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Key)
            .FirstOrDefault() ?? fallback;
    }

    private static string FormatLabelCounts(IReadOnlyDictionary<string, int> counts)
    {
        return string.Join("; ", counts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => $"{pair.Key} ({pair.Value})"));
    }
}

sealed class ObservedFingerprintGroup
{
    public ObservedFingerprintGroup(string key)
    {
        Key = key;
    }

    public string Key { get; }
    public Dictionary<string, int> LabelCounts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Samples { get; } = new();
    public int TotalObservations => LabelCounts.Values.Sum();
    public string LabelSummary => string.Join("; ", LabelCounts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).Select(pair => $"{pair.Key} ({pair.Value})"));

    public void Add(string label, string sample)
    {
        LabelCounts[label] = LabelCounts.TryGetValue(label, out int count) ? count + 1 : 1;
        if (Samples.Count < 16)
            Samples.Add(sample);
    }
}

sealed record LegacyIdentityEvidence(
    string LevelKey,
    string LevelName,
    int TrueIndex,
    string Label,
    string CandidateKind,
    string TypeHex,
    string SourceByte36Hex,
    string SpecialDataPointer,
    string Flag4AHex,
    string Flag4BHex,
    string SourceByte4FHex,
    string StateHex,
    string Fingerprint,
    string Source)
{
    public string Display => $"{LevelName}:T{TrueIndex} {Label} ({Source})";
}

sealed record IdentityTestBatch(
    int Number,
    int QuestionableCount,
    int ScopeQuestionableCount,
    string Fingerprint,
    string CurrentLabel,
    string Lane,
    string ProofStep,
    IReadOnlyList<PrecisionIdentitySample> Samples,
    string? EditManifestPath,
    IReadOnlyList<string> RosterLeads);

sealed record UnknownMobyTriageRow(
    string LevelKey,
    string LevelName,
    int TrueIndex,
    string Fingerprint,
    string CurrentRead,
    string Lane,
    string EvidenceState,
    string DirectLinks,
    string PointerFamily,
    string NearbyKnown,
    string NearbyUnknown,
    string SuggestedProof,
    string BatchPath,
    int Priority);

sealed record UnknownMobyClusterRow(
    string LevelKey,
    string LevelName,
    string ClusterKind,
    string ClusterKey,
    int UnknownCount,
    IReadOnlyList<string> UnknownRecords,
    IReadOnlyList<string> CurrentReads,
    IReadOnlyList<string> KnownAnchors,
    string RelationshipClue,
    string ProofMove,
    IReadOnlyList<string> BatchPaths,
    int Priority);

sealed record UnknownMobyClusterReviewRow(
    int Priority,
    string ClusterKind,
    string ClusterKey,
    string LevelKey,
    string LevelName,
    int UnknownCount,
    string UnknownRecords,
    string CurrentReads,
    string KnownAnchors,
    string RelationshipClue,
    string ProofMove,
    string BatchPaths,
    string ScreenshotFolder,
    string ResultStatus,
    string ClusterVerdict,
    string ObservedLabel,
    string EvidenceNotes,
    string PromoteScope);

sealed record UnknownMobyClusterPromotionCandidate(
    string Priority,
    string ClusterKind,
    string ClusterKey,
    string LevelKey,
    string LevelName,
    string UnknownCount,
    string UnknownRecords,
    string ClusterVerdict,
    string ObservedLabel,
    string Evidence,
    string ScreenshotFolder,
    string PromoteScope,
    string ResultStatus);

sealed record IdentityReleaseReviewRow(
    int Priority,
    string BatchKind,
    int BatchNumber,
    int QuestionableCount,
    int ScopeQuestionableCount,
    string Fingerprint,
    string LevelKey,
    string LevelName,
    string TrueIndexes,
    string CurrentLabel,
    string LikelyLane,
    string RosterLeads,
    string EvidenceState,
    string ProofStep,
    string EditManifestPath,
    string ScreenshotFolder,
    string ResultStatus,
    string ObservedLabel,
    string CandidateKind,
    string EvidenceNotes,
    string PromoteScope);

sealed record IdentityPromotionCandidate(
    string Fingerprint,
    string LevelKey,
    string TrueIndexes,
    string Label,
    string CandidateKind,
    string Confidence,
    string Evidence,
    string ScreenshotFolder,
    string PromoteScope,
    string ResultStatus);

sealed record IdentityFingerprintParts(
    string TypeHex,
    string SourceByte36Hex,
    string Flag4AHex,
    string Flag4BHex,
    string SourceByte4FHex);

sealed record RawSpecialDataSignature(
    string OffsetHex,
    int ByteLength,
    string Hash,
    string PrefixHex,
    IReadOnlyList<string> PointerRefs);

sealed record SourceSpecialDataSignature(
    string OffsetHex,
    int ByteLength,
    string Hash,
    string PrefixHex);

sealed record SourceSignatureExactEvidence(
    IReadOnlyList<string> Labels,
    IReadOnlyList<string> Samples)
{
    public static SourceSignatureExactEvidence Empty { get; } = new([], []);
}

sealed record SourceDiscLayout(int SectorSize, int UserOffset, int RootExtent, int RootLength);

sealed record SourceDiscFileRecord(string Name, int Lba, int Size);

sealed record RawSpecialDataRow(
    int SortRank,
    int Count,
    string LevelKey,
    string LevelName,
    string Fingerprint,
    string CurrentLabel,
    uint SpecialDataPointer,
    string RawRamPath,
    RawSpecialDataSignature? Signature,
    SourceSpecialDataSignature? SourceSignature,
    IReadOnlyList<string> SamePointerExactLabels,
    IReadOnlyList<string> SameSourceSignatureExactLabels,
    IReadOnlyList<string> SameSourceSignatureExactSamples,
    IReadOnlyList<string> Samples);

sealed record PrecisionIdentitySample(string LevelKey, string LevelName, int TrueIndex, float X, float Y, float Z)
{
    public string Display => $"{LevelName}:T{TrueIndex}";
}

sealed class TerrainBehaviorEvidenceReport
{
    public string GeneratedAt { get; init; } = "";
    public string Verdict { get; init; } = "";
    public string CollisionEvidence { get; init; } = "";
    public IReadOnlyList<TerrainSurfaceTaxonomyRow> SurfaceTaxonomy { get; init; } = [];
    public IReadOnlyList<TerrainBehaviorLevelEvidence> Levels { get; init; } = [];
    public IReadOnlyList<TerrainControlProximitySample> NearestControlSamples { get; init; } = [];
    public IReadOnlyList<string> NextProofTests { get; init; } = [];
}

sealed record TerrainSurfaceTaxonomyRow(
    string Surface,
    string EditorMeaning,
    string CurrentBehaviorLabel,
    string ProofStatus);

sealed class TerrainBehaviorLevelEvidence
{
    public string LevelKey { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public int FaceCount { get; init; }
    public int MobyCount { get; init; }
    public int ControlMobyCount { get; init; }
    public int HazardCandidateFaces { get; init; }
    public int SolidCandidateFaces { get; init; }
    public IReadOnlyDictionary<string, int> SurfaceCounts { get; init; } = new Dictionary<string, int>();
    public TerrainFaceControlProximity HazardControlProximity { get; init; } = new();
    public TerrainFaceControlProximity SolidControlProximity { get; init; } = new();
}

sealed class TerrainFaceControlProximity
{
    public string Label { get; init; } = "";
    public int FaceCount { get; init; }
    public int NearestControlFaceCount { get; init; }
    public double MinNearestControlDistance { get; init; }
    public double MedianNearestControlDistance { get; init; }
    public int Within128 { get; init; }
    public int Within256 { get; init; }
    public int Within512 { get; init; }
}

sealed class TerrainControlProximitySample
{
    public string LevelKey { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string ControlId { get; init; } = "";
    public string Label { get; init; } = "";
    public string CandidateKind { get; init; } = "";
    public string TypeHex { get; init; } = "";
    public string SourceByte36Hex { get; init; } = "";
    public string Flag4AHex { get; init; } = "";
    public string Flag4BHex { get; init; } = "";
    public string SourceByte4FHex { get; init; } = "";
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public double NearestHazardDistance { get; init; }
    public double NearestHazardZDelta { get; init; }
    public string NearestHazardSurface { get; init; } = "";
    public int NearestHazardTextureId { get; init; }
    public string NearestHazardRuntimeKey { get; init; } = "";
    public double? NearestSolidDistance { get; init; }
}

sealed class TerrainBehaviorProofTargetReport
{
    public string GeneratedAt { get; init; } = "";
    public string Summary { get; init; } = "";
    public IReadOnlyList<TerrainBehaviorProofTarget> Targets { get; init; } = [];
}

sealed class TerrainBehaviorProofSummaryReport
{
    public string GeneratedAt { get; init; } = "";
    public string Summary { get; init; } = "";
    public IReadOnlyList<TerrainBehaviorProofSurfaceSummary> Surfaces { get; init; } = [];
}

sealed class TerrainBehaviorProofSurfaceSummary
{
    public string LevelKey { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Surface { get; init; } = "";
    public int FaceCount { get; init; }
    public int TextureCount { get; init; }
    public string BehaviorSummary { get; init; } = "";
    public string ProofSummary { get; init; } = "";
    public int ProofStatusRank { get; init; }
    public string NextStep { get; init; } = "";
}

sealed class TerrainBehaviorProofTarget
{
    public string LevelKey { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string CaptureKind { get; init; } = "";
    public string Surface { get; init; } = "";
    public int TextureId { get; init; }
    public int TextureFaceCount { get; init; }
    public string RuntimeKey { get; init; } = "";
    public double X { get; init; }
    public double Y { get; init; }
    public double Z { get; init; }
    public string NearestControlId { get; init; } = "";
    public string NearestControlLabel { get; init; } = "";
    public double? NearestControlDistance { get; init; }
    public double? NearestControlZDelta { get; init; }
    public string NearestSolidOrHazardRuntimeKey { get; init; } = "";
    public double? NearestSolidOrHazardDistance { get; init; }
    public string ExistingProof { get; init; } = "";
    public int ProofStatusRank { get; init; }
    public string NextCapture { get; init; } = "";
}

sealed class TerrainLevelAudit
{
    public string LevelKey { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public int FaceCount { get; init; }
    public int TextureCount { get; init; }
    public IReadOnlyDictionary<string, int> SurfaceCounts { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> BehaviorCounts { get; init; } = new Dictionary<string, int>();
    public int HazardCandidateFaces { get; init; }
    public IReadOnlyList<TerrainTextureAudit> TextureGroups { get; init; } = [];
}

sealed class TerrainTextureAudit
{
    public int TextureId { get; init; }
    public int FaceCount { get; init; }
    public string DominantSurface { get; init; } = "unknown";
    public string SurfaceSource { get; init; } = "unclassified";
    public string DominantBehavior { get; init; } = "unknown";
    public string BehaviorConfidence { get; init; } = "unknown";
    public float MinZ { get; init; }
    public float MaxZ { get; init; }
    public string AverageColor { get; init; } = "#000000";
    public IReadOnlyDictionary<string, int> DepthSummary { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> FlipSummary { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<string> Word3Samples { get; init; } = [];
    public IReadOnlyList<string> Word4Samples { get; init; } = [];
    public IReadOnlyList<string> RuntimeKeySamples { get; init; } = [];
}

sealed record TerrainEditorReadinessRow(
    string Feature,
    string Status,
    string Evidence,
    string RemainingRisk);

sealed record TerrainSmokeSourceSearch(
    TerrainSourceSearchResult Result,
    string PatchRamPath,
    bool HasRamCapture,
    bool SourceDerived);

sealed record TerrainStructureAddCandidate(
    TerrainPolygon Face,
    int Slack,
    int Required,
    bool HasCollision);

readonly record struct SmokeCollisionPoint(int X, int Y, int Z);

readonly record struct SmokeTerrainSectorCandidate(int SectorIndex, float Z, float DistanceToReference);

readonly record struct CollisionWordTriple(uint XWord, uint YWord, uint ZWord);

sealed class SourceDerivedCollisionPattern(string runtimeKey, int triangleIndex, string pointKey)
{
    public string RuntimeKey { get; } = runtimeKey;
    public int TriangleIndex { get; } = triangleIndex;
    public string PointKey { get; } = pointKey;
    public int DuplicateCount { get; set; } = 1;
}

sealed record SourceDerivedCollisionHit(
    int WadOffset,
    string RuntimeKey,
    int TriangleIndex,
    string PointKey,
    int DuplicatePatternCount);

sealed record SourceDerivedCollisionBounds(
    int MinX,
    int MaxX,
    int MinY,
    int MaxY,
    int MinZ,
    int MaxZ);

sealed record SourceDerivedCollisionLookupRun(
    int StartWadOffset,
    int EndWadOffset,
    int WordCount,
    int GroupStartCount,
    int UniqueTriangleRefs);

sealed record SourceDerivedCollisionTableCandidate(
    int StartWadOffset,
    int EndWadOffset,
    int TriangleCount,
    int HitCount,
    int FirstHitWadOffset,
    int LastHitWadOffset,
    int AlignmentModulo12,
    int LookupStartWadOffset,
    int LookupEndWadOffset,
    int LookupWordCount,
    int LookupGroupStartCount,
    int LookupUniqueTriangleRefs,
    string Confidence);

sealed class SourceDerivedCollisionProbeReport
{
    public string LevelKey { get; init; } = "";
    public string LevelName { get; init; } = "";
    public string ScanScope { get; init; } = "";
    public int SourceSectors { get; init; }
    public int MatchedSourceSectors { get; init; }
    public int SourceMappedFaces { get; init; }
    public int EncodedTriangleVariants { get; init; }
    public int UniquePatternCount { get; init; }
    public int TotalHits { get; init; }
    public int UniquePatternHits { get; init; }
    public int MatchedRuntimeKeys { get; init; }
    public IReadOnlyList<string> LookupReferencedRuntimeKeys { get; init; } = [];
    public SourceDerivedCollisionBounds? Bounds { get; init; }
    public IReadOnlyList<SourceDerivedCollisionTableCandidate> TableCandidates { get; init; } = [];
    public IReadOnlyList<string> HitRuntimeKeys { get; init; } = [];
    public string Conclusion { get; init; } = "";
    public IReadOnlyList<SourceDerivedCollisionHit> Hits { get; init; } = [];
}

sealed record TerrainSectorSourceLayout(
    int SectorOffset,
    long WadOffset,
    int SizeBytes,
    long NextWadOffset,
    int SlackBytes)
{
    public long SectorEndWadOffset => WadOffset + SizeBytes;
}

sealed record SmokeArchiveEntry(int Index, long Offset, long Size);

sealed record SmokeAssetSubfileInfo(
    int AssetWadEntry,
    int SubfileIndex,
    long AssetWadOffset,
    long AssetSize,
    long SubfileOffset,
    long SubfileSize)
{
    public long AbsoluteWadOffset => AssetWadOffset + SubfileOffset;
}

sealed record CrossLevelTerrainTextureRow(
    string LevelKey,
    string LevelName,
    string Status,
    string RuntimeKey,
    int SourceTextureId,
    int TargetTextureId,
    int SourceSectorCount,
    int MatchedSectorCount,
    int TextureSwapPatchCount,
    int CustomTexturePatchCount,
    int CustomTextureImportCount,
    int PreviewedFaceCount,
    string DescriptorTiers,
    string Notes);

sealed record SmokeTerrainGeometry(
    GeometryCandidate Geometry,
    string OverlayPath,
    string RamPath,
    bool SourceDerivedRecovered);

sealed record CrossLevelTerrainGeometryRow(
    string LevelKey,
    string LevelName,
    string Status,
    string RuntimeKey,
    string Edit,
    int SourceSectorCount,
    int MatchedSectorCount,
    int PatchCount,
    int VisualPatchCount,
    bool HadCollisionMatch,
    bool HasCollisionPatch,
    string Notes);

sealed record CrossLevelTerrainSideWallRow(
    string LevelKey,
    string LevelName,
    string Status,
    string RuntimeKey,
    int SectorOffset,
    int OriginalTextureId,
    int TargetTextureId,
    int SourceSectorCount,
    int MatchedSectorCount,
    int SectorSlackBytes,
    int RequiredAppendBytes,
    long SourceWadOffset,
    int SectorSizeBytes,
    long NextSourceWadOffset,
    long ModelSubfileWadOffset,
    long ModelSubfileSize,
    long SceneChainTailEndWadOffset,
    long ModelTailSlackBytes,
    int ShiftSuffixSectorCount,
    long ShiftSuffixBytes,
    int ExposedEdgeCount,
    int SolidEdgeCount,
    int CollisionTriangleCount,
    int TexturedFaceCount,
    int SkippedEdgeCount,
    int PatchCount,
    string Notes)
{
    public int AppendDeficitBytes => Math.Max(0, RequiredAppendBytes - SectorSlackBytes);

    public bool NeedsSectorExpansion => ExposedEdgeCount > 0 && AppendDeficitBytes > 0;

    public bool CanShiftWithinModelSubfile => NeedsSectorExpansion &&
        ModelSubfileWadOffset >= 0 &&
        NextSourceWadOffset >= 0 &&
        ShiftSuffixSectorCount > 0 &&
        ModelTailSlackBytes >= AppendDeficitBytes;

    public long SourceSectorEndWadOffset => SourceWadOffset >= 0 ? SourceWadOffset + SectorSizeBytes : -1;

    public long ModelSubfileRelativeOffset => SourceWadOffset >= 0 && ModelSubfileWadOffset >= 0
        ? SourceWadOffset - ModelSubfileWadOffset
        : -1;
}

sealed record CrossLevelTerrainStructureRow(
    string LevelKey,
    string LevelName,
    string Status,
    string RemoveRuntimeKey,
    int RemovePatchCount,
    bool RemoveHadCollision,
    bool RemoveHasCollisionPatch,
    string AddRuntimeKey,
    int AddSlackBytes,
    int AddRequiredSlackBytes,
    int AddPatchCount,
    bool AddHasIndependentVertices,
    bool AddHasCollisionPatch,
    bool AddHasFallbackAppend,
    string Notes);

sealed class StoneHillCollisionMaterialAudit
{
    public string RamPath { get; init; } = "";
    public string HeaderOffset { get; init; } = "";
    public string TriangleOffset { get; init; } = "";
    public int TriangleCount { get; init; }
    public int MatchedVisualTriangles { get; init; }
    public IReadOnlyDictionary<string, int> CollisionFlagCounts { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> MatchedFlagCounts { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> MaterialByCollisionFlag { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, CollisionMaterialExample> Examples { get; init; } = new Dictionary<string, CollisionMaterialExample>();
    public string Conclusion { get; init; } = "";
}

sealed record CollisionMaterialExample(
    int CollisionTriangle,
    string RuntimeKey,
    int TextureId,
    string Surface,
    string Word3,
    string Word4,
    string ZRange);

sealed record UniversalNativeChestCloneExportReport(
    DateTimeOffset GeneratedAt,
    int KeyChestProofs,
    int SpringChestProofs,
    IReadOnlyList<UniversalNativeChestCloneExportRow> Rows);

sealed record UniversalNativeChestCloneExportRow(
    string LevelKey,
    string LevelName,
    string ObjectName,
    string ActorIdHex,
    int TrueIndex,
    int PatchCount,
    bool HasSpecialClone,
    string EditPath,
    string Status,
    string Notes);

sealed record CandidateValidationSummary(
    string Name,
    string PlanPath,
    string BinPath,
    string CuePath,
    string ResultPath,
    string RecipeId,
    string RecipeStatus,
    string GuardReason,
    int PatchCount,
    int TotalPatchedBytes,
    int PackageCopies,
    int RecordAppends,
    int SpecialImports,
    string SourceCountChange,
    bool ByteVerified,
    bool NoActor01Fe,
    string EvidenceStatus,
    string EvidenceNotes,
    string Status,
    IReadOnlyList<string> InGameChecks);

sealed record CandidateInGameEvidence(
    string StatusClass,
    string DisplayStatus,
    string Notes);

sealed record CandidateBehaviorSmokeCheck(
    string Id,
    string Label);
