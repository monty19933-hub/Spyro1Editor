using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

string workspaceRoot = ResolveWorkspaceRoot(args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal)));
string reportRoot = Path.Combine(workspaceRoot, "_local", "smoke", "terrain-catalog-safety");
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
string sourceCuePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).cue");
Directory.CreateDirectory(reportRoot);

if (args.Contains("--artisans-ground-water-only", StringComparer.OrdinalIgnoreCase))
{
    await RunArtisansGroundToWaterRuntimeAsync();
    return;
}

if (args.Contains("--artisans-dragon-metal-only", StringComparer.OrdinalIgnoreCase))
{
    await ArtisansDragonMetalSmoke.RunAsync(workspaceRoot);
    return;
}

List<string> failures = [];
LevelCatalog levelCatalog = LevelCatalog.Load(workspaceRoot);
IReadOnlyList<LevelDefinition> levels = LevelRealmCatalog.OrderLevels(levelCatalog.Levels);
if (levels.Count != 35)
    failures.Add($"Expected 35 catalog levels, found {levels.Count}.");
if (!File.Exists(sourceImagePath))
    failures.Add($"Missing source BIN: {sourceImagePath}");
if (!File.Exists(sourceCuePath))
    failures.Add($"Missing source CUE: {sourceCuePath}");

Dictionary<string, int> expectedRealmCounts = new(StringComparer.OrdinalIgnoreCase)
{
    ["artisans"] = 6,
    ["peacekeepers"] = 6,
    ["magiccrafters"] = 6,
    ["beastmakers"] = 6,
    ["dreamweavers"] = 6,
    ["gnastysworld"] = 5
};
Dictionary<string, int> actualRealmCounts = levels
    .Select(level => LevelRealmCatalog.TryGetPosition(level, out LevelRealmPosition position)
        ? position.Realm.Key
        : "unmapped")
    .GroupBy(key => key, StringComparer.OrdinalIgnoreCase)
    .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
foreach ((string realmKey, int expected) in expectedRealmCounts)
{
    int actual = actualRealmCounts.GetValueOrDefault(realmKey);
    if (actual != expected)
        failures.Add($"Realm {realmKey} expected {expected} levels, found {actual}.");
}
if (actualRealmCounts.Keys.Any(key => !expectedRealmCounts.ContainsKey(key)))
    failures.Add($"Unexpected realm grouping(s): {string.Join(", ", actualRealmCounts.Keys.Where(key => !expectedRealmCounts.ContainsKey(key)))}.");

List<TerrainTextureCatalogLevelInput> textureInputs = [];
List<LevelCoverageRow> coverageRows = [];
Dictionary<string, GeometryCandidate> geometries = new(StringComparer.OrdinalIgnoreCase);
Dictionary<string, NativeTerrainSurfaceLevelCatalog> nativeSurfaceCatalogs = new(StringComparer.OrdinalIgnoreCase);
Dictionary<string, IReadOnlyList<TerrainTextureSlot>> textureSlotsByLevel = new(StringComparer.OrdinalIgnoreCase);
Dictionary<string, NativeTerrainTextureRuntimeControlAudit> runtimeControlsByLevel = new(StringComparer.OrdinalIgnoreCase);
if (failures.Count == 0)
{
    foreach (LevelDefinition level in levels)
    {
        string overlayPath = Path.Combine(workspaceRoot, "editor-cache", $"{level.Key}-runtime-scene-editor-overlay.json");
        if (!File.Exists(overlayPath))
        {
            failures.Add($"Missing cached overlay for {level.DisplayName}: {overlayPath}");
            continue;
        }

        try
        {
            GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
            geometries[level.Key] = geometry;
            IReadOnlyList<TerrainTextureSlot> slots = TerrainPatchExporter.InspectTextureSlots(sourceImagePath, level);
            textureSlotsByLevel[level.Key] = slots;
            NativeTerrainTextureRuntimeControlAudit runtimeControlAudit = NativeTerrainTextureRuntimeControlScanner.Inspect(sourceImagePath, level);
            runtimeControlsByLevel[level.Key] = runtimeControlAudit;
            if (!runtimeControlAudit.Complete)
                failures.Add($"{level.DisplayName} runtime texture-control audit is incomplete: {string.Join("; ", runtimeControlAudit.SafetyBlockers)}");
            if (runtimeControlAudit.TextureCount != slots.Count)
                failures.Add($"{level.DisplayName} runtime-control audit decoded {runtimeControlAudit.TextureCount} records, but the texture catalog decoded {slots.Count}.");
            textureInputs.Add(new TerrainTextureCatalogLevelInput
            {
                Level = level,
                Geometry = geometry,
                TextureSlots = slots,
                RuntimeControlAudit = runtimeControlAudit
            });

            NativeTerrainSurfaceLevelCatalog nativeCatalog = NativeTerrainSurfaceCatalogBuilder.Build(sourceImagePath, level, geometry);
            nativeSurfaceCatalogs[level.Key] = nativeCatalog;
            HashSet<string> variantFaceKeys = nativeCatalog.TextureVariants
                .SelectMany(variant => variant.RuntimeKeys)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            TerrainPolygon[] originalFaces = geometry.Polygons
                .Where(face => !face.IsTerrainRemoved && face.OriginalTextureId >= 0)
                .ToArray();
            int groupedVariantFaces = 0;
            int explicitResidualFaces = 0;
            foreach (TerrainPolygon face in originalFaces)
            {
                if (variantFaceKeys.Contains(face.RuntimeKey))
                {
                    groupedVariantFaces++;
                    continue;
                }

                NativeTerrainFaceSurfaceBinding? binding = nativeCatalog.FindFace(face.RuntimeKey);
                if (binding != null && !string.IsNullOrWhiteSpace(binding.ReadinessNote))
                {
                    explicitResidualFaces++;
                    continue;
                }

                failures.Add($"{level.DisplayName} {face.RuntimeKey} texture {face.OriginalTextureId} is neither in a native donor variant nor covered by an explicit residual readiness note.");
            }

            coverageRows.Add(new LevelCoverageRow(
                level.Key,
                level.DisplayName,
                originalFaces.Length,
                originalFaces.Select(face => face.OriginalTextureId).Distinct().Count(),
                nativeCatalog.TextureVariants.Count,
                groupedVariantFaces,
                explicitResidualFaces));
        }
        catch (Exception ex)
        {
            failures.Add($"{level.DisplayName} catalog decode failed: {ex.Message}");
        }
    }
}

TerrainTextureCatalog? textureCatalog = null;
if (textureInputs.Count > 0)
{
    textureCatalog = TerrainTextureCatalogBuilder.Build(textureInputs);
    if (textureCatalog.Levels.Count != levels.Count)
        failures.Add($"TerrainTextureCatalogBuilder returned {textureCatalog.Levels.Count}/{levels.Count} levels.");

    Dictionary<(string LevelKey, int TextureId), TerrainTextureCatalogEntry> entryByPair = textureCatalog.Entries
        .ToDictionary(entry => (LevelCatalog.NormalizeKey(entry.LevelKey), entry.TextureId));
    foreach (LevelCoverageRow row in coverageRows)
    {
        GeometryCandidate geometry = geometries[row.LevelKey];
        foreach (IGrouping<int, TerrainPolygon> textureGroup in geometry.Polygons
                     .Where(face => !face.IsTerrainRemoved && face.OriginalTextureId >= 0)
                     .GroupBy(face => face.OriginalTextureId))
        {
            var pair = (LevelCatalog.NormalizeKey(row.LevelKey), textureGroup.Key);
            if (!entryByPair.TryGetValue(pair, out TerrainTextureCatalogEntry? entry))
            {
                failures.Add($"{row.LevelName} original texture {textureGroup.Key} is missing from the public terrain texture catalog.");
                continue;
            }

            int expectedFaces = textureGroup.Count();
            if (entry.FaceCount != expectedFaces)
                failures.Add($"{row.LevelName} texture {textureGroup.Key} catalog face count is {entry.FaceCount}, expected {expectedFaces}.");
        }

        if (!textureSlotsByLevel.TryGetValue(row.LevelKey, out IReadOnlyList<TerrainTextureSlot>? slots))
            continue;
        foreach (TerrainTextureSlot slot in slots)
        {
            var pair = (LevelCatalog.NormalizeKey(row.LevelKey), slot.TextureId);
            if (!entryByPair.TryGetValue(pair, out TerrainTextureCatalogEntry? entry))
            {
                failures.Add($"{row.LevelName} native texture record {slot.TextureId} is missing from the public terrain texture catalog.");
                continue;
            }

            bool expectedCrossLevelCandidate = slot.HasNormalDescriptors && slot.HasCloseDescriptors;
            if (entry.Readiness.CrossLevelArt.CanApply != expectedCrossLevelCandidate)
            {
                failures.Add(
                    $"{row.LevelName} texture {slot.TextureId} cross-level candidate readiness is {entry.Readiness.CrossLevelArt.CanApply}, expected {expectedCrossLevelCandidate} from its complete normal/close tiers.");
            }
            if (entry.Readiness.CustomArt.CanApply)
                failures.Add($"{row.LevelName} texture {slot.TextureId} incorrectly enabled the obsolete custom PNG writer.");
            if (entry.FaceCount == 0 && entry.Readiness.ResidentArt.CanApply)
                failures.Add($"{row.LevelName} unreferenced texture record {slot.TextureId} was selectable but incorrectly claimed runtime-proven resident art.");
            if (!runtimeControlsByLevel.TryGetValue(row.LevelKey, out NativeTerrainTextureRuntimeControlAudit? runtimeControlAudit))
            {
                failures.Add($"{row.LevelName} texture {slot.TextureId} has no source-bound runtime-control audit.");
                continue;
            }

            bool expectedPersistent = runtimeControlAudit.IsRuntimePersistentTarget(slot.TextureId);
            if (entry.Readiness.TargetRuntime.CanPersist != expectedPersistent)
            {
                failures.Add($"{row.LevelName} texture {slot.TextureId} runtime persistence is {entry.Readiness.TargetRuntime.CanPersist}, expected {expectedPersistent}: {entry.Readiness.TargetRuntime.Note}");
            }
        }
    }


    TerrainTextureCatalogEntry[] catalogOrder = textureCatalog.Entries.ToArray();
    TerrainTextureCatalogEntry[] expectedOrder = catalogOrder
        .OrderBy(entry => entry.RealmPosition?.GlobalOrder ?? int.MaxValue)
        .ThenBy(entry => entry.Level.LevelId)
        .ThenBy(entry => entry.TextureId)
        .ThenBy(entry => entry.Level.DisplayName, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    if (!catalogOrder.SequenceEqual(expectedOrder))
        failures.Add("Terrain texture catalog entries are not stably organized by realm, level, then original texture ID.");

    Dictionary<TerrainTextureRecordRole, int> roleCounts = textureCatalog.Entries
        .GroupBy(entry => entry.Readiness.RecordRole)
        .ToDictionary(group => group.Key, group => group.Count());
    Dictionary<TerrainTextureRecordRole, int> expectedRoleCounts = new()
    {
        [TerrainTextureRecordRole.FaceReferencedStatic] = 1959,
        [TerrainTextureRecordRole.ControlledFaceDestination] = 66,
        [TerrainTextureRecordRole.ControlledDestinationWithoutFace] = 4,
        [TerrainTextureRecordRole.AnimationSourceDiagnostic] = 22,
        [TerrainTextureRecordRole.NativeUnreferencedStatic] = 19
    };
    foreach ((TerrainTextureRecordRole role, int expectedCount) in expectedRoleCounts)
    {
        if (roleCounts.GetValueOrDefault(role) != expectedCount)
            failures.Add($"Terrain texture role {role} has {roleCounts.GetValueOrDefault(role)} record(s), expected {expectedCount}.");
    }
    if (roleCounts.Where(pair => !expectedRoleCounts.ContainsKey(pair.Key)).Sum(pair => pair.Value) != 0)
        failures.Add("The source-bound terrain texture role partition contains unexpected Unknown/GeometryOnly records.");

    HashSet<string> expectedNativeUnreferenced =
    [
        "beastmakers:10", "beastmakers:13", "beastmakers:15", "beastmakers:16", "beastmakers:17",
        "beastmakers:19", "beastmakers:25", "beastmakers:27", "beastmakers:28", "beastmakers:34",
        "beastmakers:39", "beastmakers:43", "beastmakers:48", "beastmakers:50", "beastmakers:53",
        "beastmakers:54", "beastmakers:58", "icyflight:11", "peacekeepers:11"
    ];
    HashSet<string> actualNativeUnreferenced = textureCatalog.Entries
        .Where(entry => entry.Readiness.IsNativeUnreferencedStatic)
        .Select(entry => $"{LevelCatalog.NormalizeKey(entry.LevelKey)}:{entry.TextureId}")
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    if (!actualNativeUnreferenced.SetEquals(expectedNativeUnreferenced))
    {
        failures.Add(
            $"The exact 19 native-unreferenced static donors changed. Missing: {string.Join(", ", expectedNativeUnreferenced.Except(actualNativeUnreferenced))}; extra: {string.Join(", ", actualNativeUnreferenced.Except(expectedNativeUnreferenced))}.");
    }
    TerrainTextureCatalogEntry? controlledWithoutFace = textureCatalog.Entries.FirstOrDefault(entry =>
        entry.LevelKey.Equals("beastmakers", StringComparison.OrdinalIgnoreCase) && entry.TextureId == 52);
    if (controlledWithoutFace?.Readiness.RecordRole != TerrainTextureRecordRole.ControlledDestinationWithoutFace)
        failures.Add("Beast Makers texture 52 was not kept out of the art-only donor set as a controlled destination without a face.");
    TerrainTextureCatalogEntry? animationSourceOnly = textureCatalog.Entries.FirstOrDefault(entry =>
        entry.LevelKey.Equals("highcaves", StringComparison.OrdinalIgnoreCase) && entry.TextureId == 3);
    if (animationSourceOnly?.Readiness.RecordRole != TerrainTextureRecordRole.AnimationSourceDiagnostic)
        failures.Add("High Caves texture 3 was not kept out of the art-only donor set as an animation-source diagnostic.");

    LevelDefinition? artisansLevel = levelCatalog.FindByKey("artisans");
    NativeTerrainTextureRuntimeControlAudit? stoneHillAudit = runtimeControlsByLevel.GetValueOrDefault("stonehill");
    bool mismatchedSourceAuditRejected = false;
    if (artisansLevel != null && stoneHillAudit != null)
    {
        try
        {
            TerrainTextureCatalogBuilder.Build([
                new TerrainTextureCatalogLevelInput
                {
                    Level = artisansLevel,
                    RuntimeControlAudit = stoneHillAudit
                }
            ]);
        }
        catch (ArgumentException ex) when (ex.Message.Contains("bound to WAD entry", StringComparison.OrdinalIgnoreCase))
        {
            mismatchedSourceAuditRejected = true;
        }
    }
    if (!mismatchedSourceAuditRejected)
        failures.Add("The terrain catalog accepted a runtime-control audit bound to a different level WAD entry.");
}

BatchBehaviorGuardResult batchBehaviorGuard = RunBatchBehaviorGuard();
if (!batchBehaviorGuard.Passed)
    failures.Add(batchBehaviorGuard.Note);

TerrainTextureArtReadiness provenArt = new(TerrainTextureArtPath.ResidentNativeTexture, true, "proven art");
TerrainTextureArtReadiness blockedArt = new(TerrainTextureArtPath.CrossLevelNativeRelocation, false, "blocked art");
TerrainTextureTargetRuntimeReadiness stableTarget = new(true, "stable target");
TerrainTextureTargetRuntimeReadiness controlledTarget = new(false, "animation-controlled target");
TerrainTextureAtomicApplyReadiness allReady = new(
    TerrainTextureApplyScope.SharedTextureRecord, 12, 3, provenArt, stableTarget, true, 3, 5, "complete properties");
TerrainTextureAtomicApplyReadiness artBlocked = allReady with { Art = blockedArt };
TerrainTextureAtomicApplyReadiness runtimeBlocked = allReady with { TargetRuntime = controlledTarget };
TerrainTextureAtomicApplyReadiness propertyBlocked = allReady with { SurfacePropertiesReady = false };
TerrainTextureAtomicApplyReadiness partialCoverage = allReady with { SurfacePropertyFaceCount = 2 };
TerrainTextureAtomicApplyReadiness preserveTargetReady = allReady with
{
    SurfacePropertiesReady = false,
    SurfacePropertyFaceCount = 0,
    SurfacePropertyTriangleCount = 0,
    SurfacePropertyMode = TerrainTextureSurfacePropertyMode.PreserveTargetNativeSurface,
    SurfacePropertyNote = "preserve target native surface"
};
bool atomicReadinessModelPassed = allReady.CanApply &&
    !artBlocked.CanApply &&
    !runtimeBlocked.CanApply &&
    !propertyBlocked.CanApply &&
    !partialCoverage.CanApply &&
    preserveTargetReady.CanApply;
if (!atomicReadinessModelPassed)
    failures.Add("The public atomic readiness model did not independently gate art/runtime/property transfer while allowing explicit preserve-target art-only mode.");

BehaviorRoundTripResult behaviorRoundTrip = await RunBehaviorRoundTripAsync();
if (!behaviorRoundTrip.Passed)
    failures.Add(behaviorRoundTrip.Note);

AtomicGuardResult atomicGuard = await RunAtomicGuardAsync();
if (!atomicGuard.Passed)
    failures.Add(atomicGuard.Note);

string markdownReportPath = Path.Combine(reportRoot, "terrain-catalog-safety.md");
string jsonReportPath = Path.Combine(reportRoot, "terrain-catalog-safety.json");
string realmSummary = string.Join(", ", LevelRealmCatalog.Realms.Select(realm =>
    $"{realm.DisplayName} {actualRealmCounts.GetValueOrDefault(realm.Key)}/{expectedRealmCounts.GetValueOrDefault(realm.Key)}"));
int originalFaceCount = coverageRows.Sum(row => row.OriginalFaceCount);
int originalPairCount = coverageRows.Sum(row => row.OriginalTextureCount);
int nativeTextureRecordCount = textureSlotsByLevel.Values.Sum(slots => slots.Count);
int unreferencedNativeTextureRecordCount = textureCatalog?.Entries.Count(entry => entry.FaceCount == 0) ?? 0;
int nativeUnreferencedStaticTextureRecordCount = textureCatalog?.Entries.Count(entry => entry.Readiness.IsNativeUnreferencedStatic) ?? 0;
int geometryOnlyTextureIdCount = textureCatalog?.Entries.Count(entry => !entry.Readiness.HasNativeTextureRecord) ?? 0;
int animationControlledTextureCount = runtimeControlsByLevel.Values.Sum(audit => audit.AnimatedTextureIds.Count);
int scrollingControlledTextureCount = runtimeControlsByLevel.Values.Sum(audit => audit.ScrollingTextureIds.Count);
int runtimePersistentTextureCount = textureCatalog?.Entries.Count(entry => entry.Readiness.TargetRuntime.CanPersist) ?? 0;
if (animationControlledTextureCount != 44)
    failures.Add($"Expected 44 animation-controlled texture records, found {animationControlledTextureCount}.");
if (scrollingControlledTextureCount != 26)
    failures.Add($"Expected 26 scrolling-controlled texture records, found {scrollingControlledTextureCount}.");
TerrainTextureCatalogEntry? artisansScrolling = textureCatalog?.Entries.FirstOrDefault(entry =>
    entry.LevelKey.Equals("artisans", StringComparison.OrdinalIgnoreCase) && entry.TextureId == 23);
if (artisansScrolling?.Readiness.TargetRuntime.CanPersist != false ||
    !(artisansScrolling?.Readiness.TargetRuntime.Note.Contains("scrolling-descriptor", StringComparison.OrdinalIgnoreCase) ?? false))
{
    failures.Add("Artisans texture 23 was not exposed as a source-bound scrolling-controlled target.");
}
TerrainTextureCatalogEntry? artisansStable = textureCatalog?.Entries.FirstOrDefault(entry =>
    entry.LevelKey.Equals("artisans", StringComparison.OrdinalIgnoreCase) && entry.TextureId == 22);
if (artisansStable?.Readiness.TargetRuntime.CanPersist != true)
    failures.Add("Artisans texture 22 was not exposed as a runtime-persistent target.");
int donorVariantFaceCount = coverageRows.Sum(row => row.GroupedVariantFaceCount);
int residualFaceCount = coverageRows.Sum(row => row.ExplicitResidualFaceCount);
StringBuilder markdown = new();
markdown.AppendLine("# Terrain Catalog Safety Smoke");
markdown.AppendLine();
markdown.AppendLine($"Status: **{(failures.Count == 0 ? "PASSED" : "FAILED")}**");
markdown.AppendLine();
markdown.AppendLine($"- Real source catalog: {levels.Count}/35 levels; {realmSummary}.");
markdown.AppendLine($"- Original texture coverage: {originalPairCount} level/texture pairs across {originalFaceCount} faces; {donorVariantFaceCount} faces grouped into native donor variants and {residualFaceCount} faces retained with explicit readiness residuals.");
markdown.AppendLine($"- Selectable ID coverage: {nativeTextureRecordCount} decoded native records plus {geometryOnlyTextureIdCount} geometry-only IDs; {unreferencedNativeTextureRecordCount} face-less records remain visible, with exactly {nativeUnreferencedStaticTextureRecordCount} genuinely native-unreferenced static art-only donors and 26 controlled/animation-source rows still blocked.");
markdown.AppendLine($"- Runtime target persistence: {runtimePersistentTextureCount} selectable IDs pass; {animationControlledTextureCount} full-record animation destinations and {scrollingControlledTextureCount} scrolling destinations are independently blocked from shared replacement.");
markdown.AppendLine($"- Shared-target property batch: {(batchBehaviorGuard.Passed ? "passed" : "failed")} — {batchBehaviorGuard.Note}");
markdown.AppendLine($"- Atomic readiness model: {(atomicReadinessModelPassed ? "passed" : "failed")} — art and runtime persistence remain required; face-backed donors require complete property coverage, while exact native-unreferenced static records use explicit preserve-target mode.");
markdown.AppendLine($"- Surface behavior persistence: {(behaviorRoundTrip.Passed ? "passed" : "failed")} — {behaviorRoundTrip.Note}");
markdown.AppendLine($"- Atomic export guard: {(atomicGuard.Passed ? "passed" : "failed")} — {atomicGuard.Note}");
markdown.AppendLine();
markdown.AppendLine("| Level | Original faces | Original textures | Native variants | Variant faces | Explicit residual faces |");
markdown.AppendLine("|---|---:|---:|---:|---:|---:|");
foreach (LevelCoverageRow row in coverageRows)
    markdown.AppendLine($"| {EscapeMarkdown(row.LevelName)} | {row.OriginalFaceCount} | {row.OriginalTextureCount} | {row.NativeVariantCount} | {row.GroupedVariantFaceCount} | {row.ExplicitResidualFaceCount} |");
if (failures.Count > 0)
{
    markdown.AppendLine();
    markdown.AppendLine("## Failures");
    markdown.AppendLine();
    foreach (string failure in failures)
        markdown.AppendLine($"- {failure}");
}
await File.WriteAllTextAsync(markdownReportPath, markdown.ToString());
await File.WriteAllTextAsync(jsonReportPath, JsonSerializer.Serialize(new
{
    GeneratedAtUtc = DateTimeOffset.UtcNow,
    Passed = failures.Count == 0,
    SourceImagePath = sourceImagePath,
    LevelCount = levels.Count,
    RealmCounts = actualRealmCounts,
    OriginalLevelTexturePairCount = originalPairCount,
    NativeTextureRecordCount = nativeTextureRecordCount,
    GeometryOnlyTextureIdCount = geometryOnlyTextureIdCount,
    RuntimePersistentTextureCount = runtimePersistentTextureCount,
    AnimationControlledTextureCount = animationControlledTextureCount,
    ScrollingControlledTextureCount = scrollingControlledTextureCount,
    UnreferencedNativeTextureRecordCount = unreferencedNativeTextureRecordCount,
    NativeUnreferencedStaticTextureRecordCount = nativeUnreferencedStaticTextureRecordCount,
    TextureRecordRoleCounts = textureCatalog?.Entries.GroupBy(entry => entry.Readiness.RecordRole).ToDictionary(group => group.Key.ToString(), group => group.Count()),
    OriginalFaceCount = originalFaceCount,
    NativeDonorVariantFaceCount = donorVariantFaceCount,
    ExplicitResidualFaceCount = residualFaceCount,
    BatchBehaviorGuard = batchBehaviorGuard,
    AtomicReadinessModelPassed = atomicReadinessModelPassed,
    BehaviorRoundTrip = behaviorRoundTrip,
    AtomicGuard = atomicGuard,
    Levels = coverageRows,
    Failures = failures
}, new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine($"Terrain catalog safety: {(failures.Count == 0 ? "PASSED" : "FAILED")}");
Console.WriteLine($"Levels: {levels.Count}/35; original level/texture pairs: {originalPairCount}; faces: {originalFaceCount}.");
Console.WriteLine($"Native texture records: {nativeTextureRecordCount}; unreferenced but selectable: {unreferencedNativeTextureRecordCount}.");
Console.WriteLine($"Native variant faces: {donorVariantFaceCount}; explicit residual faces: {residualFaceCount}.");
Console.WriteLine($"Report: {markdownReportPath}");
if (failures.Count > 0)
    throw new InvalidOperationException($"Terrain catalog safety smoke failed with {failures.Count} issue(s). See {markdownReportPath}.");

BatchBehaviorGuardResult RunBatchBehaviorGuard()
{
    foreach (LevelDefinition level in levels)
    {
        if (!geometries.TryGetValue(level.Key, out GeometryCandidate? geometry) ||
            !nativeSurfaceCatalogs.TryGetValue(level.Key, out NativeTerrainSurfaceLevelCatalog? catalog))
        {
            continue;
        }

        foreach (IGrouping<int, TerrainPolygon> group in geometry.Polygons
                     .Where(face => !face.IsTerrainRemoved && face.OriginalTextureId >= 0)
                     .GroupBy(face => face.OriginalTextureId)
                     .OrderBy(group => group.Key))
        {
            string[] runtimeKeys = group.Select(face => face.RuntimeKey).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            NativeTerrainFaceSurfaceBinding[] bindings = runtimeKeys
                .Select(runtimeKey => catalog.FindFace(runtimeKey))
                .Where(binding => binding != null)
                .Cast<NativeTerrainFaceSurfaceBinding>()
                .ToArray();
            bool completeOrdinaryCoverage = bindings.Length == runtimeKeys.Length && bindings.All(binding =>
                (binding.HasExactTriangleMapping && binding.NativeTriangles.Count > 0) ||
                (binding.MatchedVisualTriangleCount == 0 && !binding.HasAnyCollisionCandidate));
            if (!completeOrdinaryCoverage || bindings.All(binding => binding.NativeTriangles.Count == 0))
                continue;

            NativeTerrainSurfaceBatchTransferReadiness batch = catalog.EvaluateBatchTransfer(
                runtimeKeys,
                NativeTerrainSurfaceSignature.Ordinary,
                crossLevel: false);
            int expectedTriangleCount = bindings
                .SelectMany(binding => binding.NativeTriangles)
                .Select(triangle => triangle.TriangleIndex)
                .Distinct()
                .Count();
            NativeTerrainSurfaceBatchTransferReadiness missingFaceBatch = catalog.EvaluateBatchTransfer(
                runtimeKeys.Append("terrain-catalog-smoke:missing-face"),
                NativeTerrainSurfaceSignature.Ordinary,
                crossLevel: false);

            bool specialCapacityChecked = false;
            bool specialCapacityPassed = true;
            NativeTerrainFaceSurfaceBinding[] exactBindings = bindings
                .Where(binding => binding.HasExactTriangleMapping && binding.NativeTriangles.Count > 0)
                .ToArray();
            PortalSpecialSurfaceRecord? descriptor = catalog.Source.SpecialSurfaces.FirstOrDefault();
            if (descriptor != null && exactBindings.Length == bindings.Length)
            {
                NativeTerrainSurfaceSignature signature = new(descriptor.Type, descriptor.Param1, descriptor.Param2);
                NativeTerrainSurfaceBatchTransferReadiness specialBatch = catalog.EvaluateBatchTransfer(runtimeKeys, signature, crossLevel: false);
                int expectedPromotions = bindings
                    .SelectMany(binding => binding.NativeTriangles)
                    .GroupBy(triangle => triangle.TriangleIndex)
                    .Select(group => group.First())
                    .Count(triangle => triangle.FlagWadOffset < 0);
                bool capacityAllows = expectedPromotions <= catalog.Source.FlagPromotionCapacity.AdditionalFlagCapacity;
                specialCapacityChecked = true;
                specialCapacityPassed = specialBatch.PromotionCount == expectedPromotions && specialBatch.CanApply == capacityAllows;
            }

            bool passed = batch.CanApply &&
                batch.TargetFaceCount == runtimeKeys.Length &&
                batch.ReadyFaceCount == runtimeKeys.Length &&
                batch.TargetTriangleCount == expectedTriangleCount &&
                !missingFaceBatch.CanApply &&
                missingFaceBatch.TargetFaceCount == runtimeKeys.Length + 1 &&
                missingFaceBatch.ReadyFaceCount == runtimeKeys.Length &&
                missingFaceBatch.BlockedRuntimeKeys.Contains("terrain-catalog-smoke:missing-face", StringComparer.OrdinalIgnoreCase) &&
                specialCapacityPassed;
            string note = passed
                ? $"{level.DisplayName} texture {group.Key}: {runtimeKeys.Length} face(s) reduced to {expectedTriangleCount} unique collision triangle(s); one unmapped face blocked the whole batch{(specialCapacityChecked ? ", and combined promotion capacity matched" : "")}."
                : $"{level.DisplayName} texture {group.Key}: batch={batch.CanApply}/{batch.ReadyFaceCount}/{batch.TargetFaceCount}/{batch.TargetTriangleCount}, missing={missingFaceBatch.CanApply}/{missingFaceBatch.ReadyFaceCount}/{missingFaceBatch.TargetFaceCount}, capacity={specialCapacityChecked}/{specialCapacityPassed}.";
            return new BatchBehaviorGuardResult(passed, note, level.Key, group.Key, runtimeKeys.Length, expectedTriangleCount);
        }
    }

    return new BatchBehaviorGuardResult(false, "No real level/texture group had complete native mappings for the shared-target batch smoke.", "", -1, 0, 0);
}

async Task<BehaviorRoundTripResult> RunBehaviorRoundTripAsync()
{
    const string levelKey = "artisans";
    if (!geometries.TryGetValue(levelKey, out GeometryCandidate? sourceGeometry))
        return new BehaviorRoundTripResult(false, "Artisans geometry was unavailable for the persistence check.", "");

    string overlayPath = Path.Combine(workspaceRoot, "editor-cache", $"{levelKey}-runtime-scene-editor-overlay.json");
    GeometryCandidate reloadGeometry = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
    TerrainPolygon? sourceFace = sourceGeometry.Polygons.FirstOrDefault(face => face.OriginalTextureId >= 0);
    if (sourceFace == null)
        return new BehaviorRoundTripResult(false, "Artisans has no terrain face for the persistence check.", "");

    TerrainSurfaceBehaviorEdit expected = new(
        SurfaceType: 0,
        Param1: 1,
        Param2: 2,
        SourceLevelKey: "gnastysworld",
        SourceRuntimeKey: "34:3:hp",
        Label: "smoke lethal ooze donor");
    sourceFace.ApplySurfaceBehaviorEdit(expected);
    string editsPath = Path.Combine(reportRoot, "behavior-roundtrip-terrain-edits.json");
    int savedCount = await TerrainEditStore.SaveAsync(editsPath, [sourceFace], "Artisans terrain catalog smoke");
    sourceFace.ResetTerrainEdit();
    int loadedCount = TerrainEditStore.Load(editsPath, reloadGeometry.Polygons);
    TerrainSurfaceBehaviorEdit? actual = reloadGeometry.Polygons
        .FirstOrDefault(face => face.RuntimeKey.Equals(sourceFace.RuntimeKey, StringComparison.OrdinalIgnoreCase))?
        .SurfaceBehaviorEdit;
    bool passed = savedCount == 1 && loadedCount == 1 && actual == expected;
    string note = passed
        ? $"all six fields survived save/reload on {sourceFace.RuntimeKey}."
        : $"expected one intact edit on {sourceFace.RuntimeKey}; saved {savedCount}, loaded {loadedCount}, actual {actual}.";
    return new BehaviorRoundTripResult(passed, note, editsPath);
}

async Task<AtomicGuardResult> RunAtomicGuardAsync()
{
    const string levelKey = "artisans";
    LevelDefinition? level = levelCatalog.FindByKey(levelKey);
    if (level == null)
        return new AtomicGuardResult(false, "Artisans was unavailable for the atomic export check.", "", "", "");

    TerrainTextureSlot? targetSlot = TerrainPatchExporter.InspectTextureSlots(sourceImagePath, level)
        .FirstOrDefault(slot => slot.HasNormalDescriptors || slot.HasCloseDescriptors);
    if (targetSlot == null)
        return new AtomicGuardResult(false, "Artisans has no writable native texture slot for the atomic export check.", "", "", "");

    string emptyEditsPath = Path.Combine(reportRoot, "atomic-invalid-empty-terrain-edits.json");
    await File.WriteAllTextAsync(emptyEditsPath, "{\"edits\":[]}");
    CustomTerrainTextureImport nativeImport = new(
        TextureId: targetSlot.TextureId,
        SourceImagePath: "missing-native-preview.png",
        SourceImageName: "missing-native-preview.png",
        DescriptorTier: targetSlot.HasNormalDescriptors && targetSlot.HasCloseDescriptors
            ? "both"
            : targetSlot.HasNormalDescriptors ? "hqData" : "hqDataClose",
        TileSize: targetSlot.HasCloseDescriptors && !targetSlot.HasNormalDescriptors ? 128 : 64,
        SourceKind: "borrowed-cross-level-texture-art",
        SourceLevelKey: "gnastysworld",
        SourceTextureId: -1,
        SourceRuntimeKey: "34:3:hp",
        SourceWadEntry: -1);
    CustomTerrainTextureImport customPngImport = new(
        TextureId: targetSlot.TextureId,
        SourceImagePath: "missing-custom.png",
        SourceImageName: "missing-custom.png",
        DescriptorTier: "both",
        TileSize: 64,
        SourceKind: "imported-image");

    async Task<(bool Passed, string PlanPath, string ManifestPath, string ExceptionMessage, string Evidence)> RunBlockedImportAsync(
        CustomTerrainTextureImport import,
        string slug,
        string expectedReason)
    {
        string manifestPath = Path.Combine(reportRoot, $"{slug}.json");
        await CustomTerrainTextureStore.SaveManifestAsync(manifestPath, level.Key, level.DisplayName, [import]);
        string outputPrefix = Path.Combine(reportRoot, slug);
        string outputImagePath = $"{outputPrefix}.bin";
        string outputCuePath = $"{outputPrefix}.cue";
        string outputPlanPath = $"{outputPrefix}.terrain-patch-plan.json";
        await File.WriteAllBytesAsync(outputImagePath, [0x42]);
        await File.WriteAllTextAsync(outputCuePath, "stale cue");
        if (File.Exists(outputPlanPath))
            File.Delete(outputPlanPath);

        string exceptionMessage = "";
        try
        {
            await TerrainPatchExporter.ExportAsync(new TerrainPatchRequest(
                SourceImagePath: sourceImagePath,
                SourceCuePath: sourceCuePath,
                OutputPrefix: outputPrefix,
                Level: level,
                RamPath: "",
                SourceSearchPath: "",
                TerrainEditsPath: emptyEditsPath,
                CustomTexturesPath: manifestPath,
                WriteImage: true));
        }
        catch (InvalidOperationException ex)
        {
            exceptionMessage = ex.Message;
        }

        string planText = File.Exists(outputPlanPath)
            ? await File.ReadAllTextAsync(outputPlanPath)
            : "";
        bool passed = !string.IsNullOrWhiteSpace(exceptionMessage) &&
            exceptionMessage.Contains("stopped before writing a BIN", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(outputPlanPath) &&
            planText.Contains("[atomic terrain swap blocked]", StringComparison.Ordinal) &&
            planText.Contains(expectedReason, StringComparison.OrdinalIgnoreCase) &&
            !File.Exists(outputImagePath) &&
            !File.Exists(outputCuePath);
        string evidence = $"exception={(!string.IsNullOrWhiteSpace(exceptionMessage))}, plan={File.Exists(outputPlanPath)}, BIN={File.Exists(outputImagePath)}, CUE={File.Exists(outputCuePath)}, reason={planText.Contains(expectedReason, StringComparison.OrdinalIgnoreCase)}";
        return (passed, outputPlanPath, manifestPath, exceptionMessage, evidence);
    }

    var native = await RunBlockedImportAsync(
        nativeImport,
        "atomic-blocked-native-cross-level",
        "source-hash-bound runtime ownership closure");
    var custom = await RunBlockedImportAsync(
        customPngImport,
        "atomic-blocked-custom-png",
        "obsolete 0x800-row and fixed 4-bpp close-detail writer");
    bool passed = native.Passed && custom.Passed;
    string note = passed
        ? "native cross-level and custom PNG legacy paths were blocked before BIN/CUE creation; both stale output pairs were removed and both retained plans name their independent blockers."
        : $"native guard: {native.Evidence}; custom PNG guard: {custom.Evidence}.";
    return new AtomicGuardResult(
        passed,
        note,
        $"{native.PlanPath} | {custom.PlanPath}",
        $"{native.ManifestPath} | {custom.ManifestPath}",
        $"{native.ExceptionMessage} | {custom.ExceptionMessage}");
}

async Task RunArtisansGroundToWaterRuntimeAsync()
{
    const string levelKey = "artisans";
    const string runtimeKey = "47:6:hp";
    const int expectedOriginalTextureId = 65;
    const int waterTextureId = 27;
    const string waterNearColorHex = "#21A1F9";
    const string waterFarColorHex = "#1E8AD4";
    const string runtimeLandmark = "starting-courtyard grass under the first red gem in the three-gem line between the two tulip patches";
    NativeTerrainSurfaceSignature waterSignature = new(0, 0, 0);
    string runtimeReportRoot = Path.Combine(workspaceRoot, "_local", "smoke", "artisans-ground-to-water-runtime");
    Directory.CreateDirectory(runtimeReportRoot);
    string markdownPath = Path.Combine(runtimeReportRoot, "artisans-ground-to-water-runtime.md");
    string jsonPath = Path.Combine(runtimeReportRoot, "artisans-ground-to-water-runtime.json");
    string overlayPath = Path.Combine(workspaceRoot, "editor-cache", "artisans-runtime-scene-editor-overlay.json");
    string editsPath = Path.Combine(runtimeReportRoot, "artisans-ground-to-water-terrain-edits.json");
    string sourceSearchPath = Path.Combine(runtimeReportRoot, "artisans-source-derived-terrain-source-search-native.json");
    string outputPrefix = Path.Combine(runtimeReportRoot, "SpyroEditor-Artisans-GroundToDamagingWater");
    string outputImagePath = $"{outputPrefix}.bin";
    string outputCuePath = $"{outputPrefix}.cue";
    string outputPlanPath = $"{outputPrefix}.terrain-patch-plan.json";
    foreach (string stalePath in new[] { outputImagePath, outputCuePath, outputPlanPath })
    {
        if (File.Exists(stalePath))
            File.Delete(stalePath);
    }

    LevelCatalog focusedCatalog = LevelCatalog.Load(workspaceRoot);
    LevelDefinition level = focusedCatalog.FindByKey(levelKey)
        ?? throw new InvalidOperationException("The Artisans level definition is missing.");
    if (!File.Exists(sourceImagePath) || !File.Exists(sourceCuePath) || !File.Exists(overlayPath))
        throw new FileNotFoundException("The focused Artisans runtime candidate needs the real BIN/CUE and cached Artisans overlay.");
    const long rejectedInterleavedFarColorOffset = 0x904D14;
    byte[] rejectedInterleavedFarColorBefore = ReadLogicalWadBytes(
        sourceImagePath,
        rejectedInterleavedFarColorOffset,
        4);

    GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
    TerrainPolygon face = geometry.Polygons.Single(candidate =>
        candidate.RuntimeKey.Equals(runtimeKey, StringComparison.OrdinalIgnoreCase));
    AssertFocused(face.OriginalTextureId == expectedOriginalTextureId,
        $"{runtimeKey} original texture is {face.OriginalTextureId}, expected {expectedOriginalTextureId}.");
    AssertFocused(face.OriginalPoints.Count == 4,
        $"{runtimeKey} has {face.OriginalPoints.Count} points, expected one two-triangle quad.");

    NativeTerrainSurfaceSourceData sourceSurfaces = PortalSourceDataLocator.LocateTerrainSurfaces(sourceImagePath, level);
    NativeTerrainSurfaceLevelCatalog sourceNativeCatalog = NativeTerrainSurfaceCatalogBuilder.Build(level, geometry, sourceSurfaces);
    NativeTerrainTextureSurfaceVariant waterVariant = sourceNativeCatalog.TextureVariants.Single(variant =>
        variant.TextureId == waterTextureId && variant.Signature == waterSignature);
    NativeTerrainFaceSurfaceBinding sourceTargetBinding = sourceNativeCatalog.FindFace(runtimeKey)
        ?? throw new InvalidOperationException($"{runtimeKey} is missing from the Artisans native terrain-surface catalog.");
    AssertFocused(sourceTargetBinding.HasExactTriangleMapping && sourceTargetBinding.NativeTriangles.Count == 2,
        $"{runtimeKey} must resolve to exactly two native collision triangles before staging.");
    NativeTerrainSurfaceTransferReadiness readiness = sourceNativeCatalog.EvaluateTransfer(runtimeKey, waterSignature, crossLevel: false);
    AssertFocused(readiness.CanApply && readiness.TargetTriangleCount == 2,
        $"{runtimeKey} water transfer is not ready: {readiness.Note}");
    PortalSpecialSurfaceRecord descriptorZero = sourceSurfaces.SpecialSurfaces.Single(surface => surface.Index == 0);
    AssertFocused(descriptorZero.Type == 0 && descriptorZero.Param1 == 0 && descriptorZero.Param2 == 0,
        "Artisans native descriptor 0 is not damaging water type 0, params 0/0.");

    int promotedTriangleCount = sourceTargetBinding.NativeTriangles
        .Where(triangle => triangle.FlagWadOffset < 0)
        .Select(triangle => triangle.TriangleIndex)
        .Distinct()
        .Count();
    AssertFocused(promotedTriangleCount > 0,
        $"{runtimeKey} does not exercise beyond-count collision flag promotion.");

    TerrainPolygon visualDonor = geometry.Polygons.Single(candidate =>
        candidate.RuntimeKey.Equals("38:105:hp", StringComparison.OrdinalIgnoreCase));
    AssertFocused(NativeTerrainTextureVisualInspector.TryInspectSourceImage(
            sourceImagePath,
            level.Key,
            visualDonor,
            out TerrainTextureVisualEdit inspectedVisual,
            out string visualError),
        $"Could not inspect the native horizontal pool visual: {visualError}");
    AssertFocused(inspectedVisual.SourceTextureId == waterTextureId &&
        inspectedVisual.UniqueCornerPairCount == 1 &&
        ToColorHex(inspectedVisual.Corner0.NearColor).Equals(waterNearColorHex, StringComparison.OrdinalIgnoreCase) &&
        ToColorHex(inspectedVisual.Corner0.FarColor).Equals(waterFarColorHex, StringComparison.OrdinalIgnoreCase),
        $"Native horizontal pool visual decoded texture {inspectedVisual.SourceTextureId}, near {ToColorHex(inspectedVisual.Corner0.NearColor)}, far {ToColorHex(inspectedVisual.Corner0.FarColor)}.");
    AssertFocused(NativeTerrainTextureVisualInspector.TryGetWritableColorSlotCapacity(
            sourceImagePath,
            face,
            out int privateTintCapacity,
            out string privateTintError) && privateTintCapacity >= inspectedVisual.UniqueCornerPairCount,
        $"Focused target lacks private tint capacity: {privateTintError}");

    TerrainTextureVisualEdit stagedVisual = inspectedVisual with
    {
        Label = "native horizontal pool art with source-bound near/fade tint"
    };
    face.ApplyTextureOverride(waterTextureId);
    face.ApplyTextureVisualEdit(stagedVisual);
    TerrainSurfaceBehaviorEdit stagedBehavior = new(
        SurfaceType: waterSignature.SurfaceType,
        Param1: waterSignature.Param1,
        Param2: waterSignature.Param2,
        SourceLevelKey: level.Key,
        SourceRuntimeKey: waterVariant.RepresentativeRuntimeKey,
        Label: waterSignature.Label);
    face.ApplySurfaceBehaviorEdit(stagedBehavior);
    int savedEditCount = await TerrainEditStore.SaveAsync(
        editsPath,
        [face],
        "Artisans starting-courtyard grass to native damaging water runtime candidate");
    AssertFocused(savedEditCount == 1, $"Expected one saved terrain edit, found {savedEditCount}.");

    GeometryCandidate savedEditReadbackGeometry = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
    int loadedEditCount = TerrainEditStore.Load(editsPath, savedEditReadbackGeometry.Polygons);
    TerrainPolygon savedEditReadback = savedEditReadbackGeometry.Polygons.Single(candidate =>
        candidate.RuntimeKey.Equals(runtimeKey, StringComparison.OrdinalIgnoreCase));
    AssertFocused(loadedEditCount == 1 &&
        savedEditReadback.TextureId == waterTextureId &&
        savedEditReadback.TextureVisualEdit == stagedVisual &&
        savedEditReadback.SurfaceBehaviorEdit == stagedBehavior,
        "The staged texture, native visual, and damaging-water property did not survive TerrainEditStore readback.");

    TerrainSourceSearchResult sourceSearch = await TerrainSourceSearchBuilder.BuildSourceDerivedAsync(
        new SourceDerivedTerrainSourceSearchRequest(
            SourceImagePath: sourceImagePath,
            OutputPath: sourceSearchPath,
            Level: level,
            Geometry: geometry));
    TerrainSourceSearchEntry mappedFace = sourceSearch.Report.Results.Single(entry =>
        entry.Edit.Equals(runtimeKey, StringComparison.OrdinalIgnoreCase));
    AssertFocused(mappedFace.FullSectorHits.Count == 1,
        $"{runtimeKey} source-derived sector mapping returned {mappedFace.FullSectorHits.Count} hits.");

    TerrainPatchResult result = await TerrainPatchExporter.ExportAsync(new TerrainPatchRequest(
        SourceImagePath: sourceImagePath,
        SourceCuePath: sourceCuePath,
        OutputPrefix: outputPrefix,
        Level: level,
        RamPath: "",
        SourceSearchPath: sourceSearchPath,
        TerrainEditsPath: editsPath,
        CustomTexturesPath: "",
        WriteImage: true));
    AssertFocused(result.WroteImage && File.Exists(outputImagePath) && File.Exists(outputCuePath),
        "TerrainPatchExporter did not create the focused BIN/CUE.");
    AssertFocused(result.Plan.SkippedEdits.Count == 0,
        $"Focused export contains skipped edits: {string.Join(" | ", result.Plan.SkippedEdits)}");

    TerrainPatch texturePatch = result.Plan.Patches.Single(patch =>
        patch.Kind.Equals("texture-id-word3", StringComparison.OrdinalIgnoreCase) &&
        patch.RuntimeKey.Equals(runtimeKey, StringComparison.OrdinalIgnoreCase));
    TerrainPatch colorIndexPatch = result.Plan.Patches.Single(patch =>
        patch.Kind.Equals("texture-visual-color-indices", StringComparison.OrdinalIgnoreCase) &&
        patch.RuntimeKey.Equals(runtimeKey, StringComparison.OrdinalIgnoreCase));
    TerrainPatch nearColorPatch = result.Plan.Patches.Single(patch =>
        patch.Kind.Equals("texture-visual-near-color", StringComparison.OrdinalIgnoreCase) &&
        patch.RuntimeKey.Equals(runtimeKey, StringComparison.OrdinalIgnoreCase));
    TerrainPatch farColorPatch = result.Plan.Patches.Single(patch =>
        patch.Kind.Equals("texture-visual-far-color", StringComparison.OrdinalIgnoreCase) &&
        patch.RuntimeKey.Equals(runtimeKey, StringComparison.OrdinalIgnoreCase));
    TerrainPatch promotionPatch = result.Plan.Patches.Single(patch =>
        patch.Kind.Equals("collision-surface-flag-promotion", StringComparison.OrdinalIgnoreCase));
    byte[] textureAfter = HexToBytesFocused(texturePatch.AfterHexPreview);
    AssertFocused(textureAfter.Length == 4 &&
        (BinaryPrimitives.ReadUInt32LittleEndian(textureAfter) & 0x7F) == waterTextureId,
        "The planned texture-id word does not select Artisans texture 27.");
    AssertFocused(textureAfter.SequenceEqual(new byte[] { 0x1B, 0xFE, 0x4F, 0x0B }),
        $"The planar-water texture word is {Convert.ToHexString(textureAfter)}, expected 1BFE4F0B with all target control bits preserved.");
    AssertFocused(ReadLogicalWadBytes(outputImagePath, ParseHexLong(texturePatch.WadRelativeOffset), textureAfter.Length)
            .SequenceEqual(textureAfter),
        "The output BIN texture-id bytes do not match the retained patch plan.");
    AssertFocused(ParseHexLong(colorIndexPatch.WadRelativeOffset) == 0x904D80 &&
        HexToBytesFocused(colorIndexPatch.AfterHexPreview).SequenceEqual(new byte[] { 0x10, 0x10, 0x10, 0x10 }),
        "The focused face did not bind all four corners to its private color slot 16.");
    AssertFocused(ParseHexLong(nearColorPatch.WadRelativeOffset) == 0x904D18 &&
        HexToBytesFocused(nearColorPatch.AfterHexPreview).SequenceEqual(new byte[] { 0x21, 0xA1, 0xF9, 0x00 }),
        "The private near tint is not the byte-exact native pool color.");
    AssertFocused(ParseHexLong(farColorPatch.WadRelativeOffset) == 0x904CD4 &&
        HexToBytesFocused(farColorPatch.AfterHexPreview).SequenceEqual(new byte[] { 0x1E, 0x8A, 0xD4, 0x00 }),
        "The private fade tint is not the byte-exact native pool color.");
    foreach (TerrainPatch visualPatch in new[] { colorIndexPatch, nearColorPatch, farColorPatch })
    {
        byte[] visualAfter = HexToBytesFocused(visualPatch.AfterHexPreview);
        AssertFocused(ReadLogicalWadBytes(outputImagePath, ParseHexLong(visualPatch.WadRelativeOffset), visualAfter.Length)
                .SequenceEqual(visualAfter),
            $"The output BIN {visualPatch.Kind} bytes do not match the retained patch plan.");
    }
    AssertFocused(result.Plan.Patches.All(patch =>
        ParseHexLong(patch.WadRelativeOffset) + patch.ByteLength <= rejectedInterleavedFarColorOffset ||
        ParseHexLong(patch.WadRelativeOffset) >= rejectedInterleavedFarColorOffset + rejectedInterleavedFarColorBefore.Length),
        "The focused plan still overlaps the rejected interleaved high-detail color bytes at 0x904D14.");
    AssertFocused(ReadLogicalWadBytes(
            outputImagePath,
            rejectedInterleavedFarColorOffset,
            rejectedInterleavedFarColorBefore.Length)
        .SequenceEqual(rejectedInterleavedFarColorBefore),
        "The rejected interleaved high-detail color bytes at 0x904D14 changed in the output BIN.");
    byte[] promotionAfter = HexToBytesFocused(promotionPatch.AfterHexPreview);
    AssertFocused(ReadLogicalWadBytes(outputImagePath, ParseHexLong(promotionPatch.WadRelativeOffset), promotionAfter.Length)
            .SequenceEqual(promotionAfter),
        "The output BIN collision promotion bytes do not match the retained patch plan.");

    NativeTerrainSurfaceSourceData outputSurfaces = PortalSourceDataLocator.LocateTerrainSurfaces(outputImagePath, level);
    GeometryCandidate outputGeometry = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
    NativeTerrainSurfaceLevelCatalog outputNativeCatalog = NativeTerrainSurfaceCatalogBuilder.Build(level, outputGeometry, outputSurfaces);
    NativeTerrainFaceSurfaceBinding outputTargetBinding = outputNativeCatalog.FindFace(runtimeKey)
        ?? throw new InvalidOperationException($"{runtimeKey} disappeared after reparsing the output BIN.");
    PortalSpecialSurfaceRecord outputDescriptorZero = outputSurfaces.SpecialSurfaces.Single(surface => surface.Index == 0);
    bool bothTrianglesResolveToDescriptorZero = outputTargetBinding.HasExactTriangleMapping &&
        outputTargetBinding.NativeTriangles.Count == 2 &&
        outputTargetBinding.NativeTriangles.All(triangle =>
            triangle.FlagWadOffset >= 0 &&
            triangle.SurfaceIndex == 0 &&
            triangle.SurfaceType == 0 &&
            triangle.Param1 == 0 &&
            triangle.Param2 == 0);
    bool specialSurfaceTableUnchanged = sourceSurfaces.SpecialSurfaces
        .Select(surface => (surface.Index, surface.Type, surface.Param1, surface.Param2))
        .SequenceEqual(outputSurfaces.SpecialSurfaces.Select(surface =>
            (surface.Index, surface.Type, surface.Param1, surface.Param2)));
    List<string> structuralFailures = [];
    if (outputSurfaces.LevelDataByteLength != sourceSurfaces.LevelDataByteLength)
        structuralFailures.Add($"level-data length {sourceSurfaces.LevelDataByteLength}->{outputSurfaces.LevelDataByteLength}");
    if (outputSurfaces.Collision.TriangleCount != sourceSurfaces.Collision.TriangleCount)
        structuralFailures.Add($"triangle count {sourceSurfaces.Collision.TriangleCount}->{outputSurfaces.Collision.TriangleCount}");
    if (outputSurfaces.Collision.FlagCount != sourceSurfaces.Collision.FlagCount + promotedTriangleCount)
        structuralFailures.Add($"flag count {sourceSurfaces.Collision.FlagCount}->{outputSurfaces.Collision.FlagCount}, expected +{promotedTriangleCount}");
    if (outputSurfaces.CollisionSurfaceTriangles.Count != outputSurfaces.Collision.TriangleCount)
        structuralFailures.Add($"decoded triangles {outputSurfaces.CollisionSurfaceTriangles.Count}/{outputSurfaces.Collision.TriangleCount}");
    int uniqueOutputTriangleIndexes = outputSurfaces.CollisionSurfaceTriangles
        .Select(triangle => triangle.TriangleIndex)
        .Distinct()
        .Count();
    if (uniqueOutputTriangleIndexes != outputSurfaces.Collision.TriangleCount)
        structuralFailures.Add($"unique triangle indexes {uniqueOutputTriangleIndexes}/{outputSurfaces.Collision.TriangleCount}");
    if (outputDescriptorZero.Type != 0 || outputDescriptorZero.Param1 != 0 || outputDescriptorZero.Param2 != 0)
        structuralFailures.Add($"descriptor 0 became {outputDescriptorZero.Type}/{outputDescriptorZero.Param1}/{outputDescriptorZero.Param2}");
    if (!specialSurfaceTableUnchanged)
        structuralFailures.Add("special-surface table changed");
    long sourceImageLength = ReadFileLength(sourceImagePath);
    long outputImageLength = ReadFileLength(outputImagePath);
    if (outputImageLength != sourceImageLength)
        structuralFailures.Add($"BIN size {sourceImageLength}->{outputImageLength}");
    bool structurallyValid = structuralFailures.Count == 0;
    AssertFocused(bothTrianglesResolveToDescriptorZero,
        "Output reparse did not resolve both target triangles to native damaging-water descriptor 0.");
    AssertFocused(structurallyValid,
        $"Output collision/surface structural validation failed after promotion: {string.Join("; ", structuralFailures)}.");

    string center = $"({face.Center.X:0.##}, {face.Center.Y:0.##}, {face.AvgZ:0.##})";
    string[] sourceTriangleIndexes = sourceTargetBinding.NativeTriangles
        .Select(triangle => triangle.TriangleIndex.ToString())
        .ToArray();
    string[] outputTriangleIndexes = outputTargetBinding.NativeTriangles
        .Select(triangle => triangle.TriangleIndex.ToString())
        .ToArray();
    string markdown = $"""
        # Artisans Ground to Damaging Water Runtime Candidate

        Status: **PASSED**

        - Target face: `{runtimeKey}`, center `{center}`, original Artisans texture {expectedOriginalTextureId}.
        - Runtime landmark: {runtimeLandmark}.
        - Donor: Artisans texture {waterTextureId}, `{waterSignature.Label}`, representative face `{waterVariant.RepresentativeRuntimeKey}`.
        - Export: horizontal pool texture-id patch, face-private near/fade tint patches, and `collision-surface-flag-promotion` are present; collision flags {sourceSurfaces.Collision.FlagCount}->{outputSurfaces.Collision.FlagCount}.
        - Visual isolation: all four corners use private sector color slot 16; nearby terrain retains its original shared color slots.
        - Reparse: both target triangles resolve to descriptor 0 (`type=0`, `param1=0`, `param2=0`); source indexes {string.Join(",", sourceTriangleIndexes)}, output indexes {string.Join(",", outputTriangleIndexes)}.
        - Structure: triangle count {sourceSurfaces.Collision.TriangleCount} preserved, level-data length {sourceSurfaces.LevelDataByteLength} preserved, special-surface table preserved, output BIN size preserved.

        Runtime artifact: `{outputCuePath}`
        """;
    await File.WriteAllTextAsync(markdownPath, markdown);
    await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(new
    {
        GeneratedAtUtc = DateTimeOffset.UtcNow,
        Passed = true,
        Level = level.DisplayName,
        RuntimeKey = runtimeKey,
        Center = new { X = face.Center.X, Y = face.Center.Y, Z = face.AvgZ },
        RuntimeLandmark = runtimeLandmark,
        OriginalTextureId = expectedOriginalTextureId,
        DonorTextureId = waterTextureId,
        DonorRuntimeKey = waterVariant.RepresentativeRuntimeKey,
        Surface = new { DescriptorIndex = 0, Type = 0, Param1 = 0, Param2 = 0, Label = waterSignature.Label },
        SourceTriangleIndexes = sourceTargetBinding.NativeTriangles.Select(triangle => triangle.TriangleIndex).ToArray(),
        OutputTriangleIndexes = outputTargetBinding.NativeTriangles.Select(triangle => triangle.TriangleIndex).ToArray(),
        PromotedTriangleCount = promotedTriangleCount,
        SourceFlagCount = sourceSurfaces.Collision.FlagCount,
        OutputFlagCount = outputSurfaces.Collision.FlagCount,
        TriangleCount = outputSurfaces.Collision.TriangleCount,
        LevelDataByteLength = outputSurfaces.LevelDataByteLength,
        TexturePatchKind = texturePatch.Kind,
        ColorIndexPatchKind = colorIndexPatch.Kind,
        NearColorPatchKind = nearColorPatch.Kind,
        FarColorPatchKind = farColorPatch.Kind,
        WaterNearColor = waterNearColorHex,
        WaterFarColor = waterFarColorHex,
        PromotionPatchKind = promotionPatch.Kind,
        PromotionDescription = promotionPatch.Description,
        BothTargetTrianglesResolveToDescriptorZero = bothTrianglesResolveToDescriptorZero,
        StructurallyValid = structurallyValid,
        TerrainEditsPath = editsPath,
        SourceSearchPath = sourceSearchPath,
        OutputImagePath = outputImagePath,
        OutputCuePath = outputCuePath,
        OutputPlanPath = outputPlanPath
    }, new JsonSerializerOptions { WriteIndented = true }));

    Console.WriteLine("Artisans ground->damaging-water runtime candidate: PASSED");
    Console.WriteLine($"Face: {runtimeKey}; center: {center}; texture {expectedOriginalTextureId}->{waterTextureId}; flags {sourceSurfaces.Collision.FlagCount}->{outputSurfaces.Collision.FlagCount}.");
    Console.WriteLine($"Landmark: {runtimeLandmark}.");
    Console.WriteLine($"CUE: {outputCuePath}");
    Console.WriteLine($"Report: {markdownPath}");
}

static void AssertFocused(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static string ToColorHex(ColorRgba color) =>
    $"#{color.R:X2}{color.G:X2}{color.B:X2}";

static long ParseHexLong(string text)
{
    string value = (text ?? "").Trim();
    if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        value = value[2..];
    return Convert.ToInt64(value, 16);
}

static byte[] HexToBytesFocused(string text)
{
    string compact = new((text ?? "").Where(Uri.IsHexDigit).ToArray());
    if ((compact.Length & 1) != 0)
        throw new InvalidDataException("Patch hex text has an odd number of digits.");
    return Convert.FromHexString(compact);
}

static byte[] ReadLogicalWadBytes(string imagePath, long wadOffset, int length)
{
    const int wadLba = 37;
    (int sectorSize, int userOffset) = DetectDiscLayout(imagePath);
    byte[] result = new byte[length];
    using FileStream stream = File.OpenRead(imagePath);
    int written = 0;
    long logicalOffset = wadOffset;
    while (written < length)
    {
        int sectorOffset = (int)(logicalOffset % 2048);
        long sector = wadLba + (logicalOffset / 2048);
        int readLength = Math.Min(2048 - sectorOffset, length - written);
        stream.Position = (sector * sectorSize) + userOffset + sectorOffset;
        int read = stream.Read(result, written, readLength);
        if (read != readLength)
            throw new EndOfStreamException("Could not read retained WAD patch bytes from the output image.");
        written += read;
        logicalOffset += read;
    }
    return result;
}

static long ReadFileLength(string path)
{
    using FileStream stream = File.OpenRead(path);
    return stream.Length;
}

static (int SectorSize, int UserOffset) DetectDiscLayout(string imagePath)
{
    using FileStream stream = File.OpenRead(imagePath);
    byte[] marker = new byte[6];
    foreach ((int sectorSize, int userOffset) in new[] { (2048, 0), (2352, 24), (2336, 8) })
    {
        long pvd = (16L * sectorSize) + userOffset;
        if (pvd + 6 > stream.Length)
            continue;
        stream.Position = pvd;
        if (stream.Read(marker) == marker.Length && marker[0] == 1 &&
            Encoding.ASCII.GetString(marker.AsSpan(1)) == "CD001")
        {
            return (sectorSize, userOffset);
        }
    }
    throw new InvalidDataException("Could not identify the output disc image sector layout.");
}

static string ResolveWorkspaceRoot(string? argument)
{
    if (!string.IsNullOrWhiteSpace(argument))
    {
        string explicitPath = Path.GetFullPath(argument);
        if (File.Exists(Path.Combine(explicitPath, "spyro-level-catalog.json")))
            return explicitPath;
    }

    DirectoryInfo? current = new(AppContext.BaseDirectory);
    while (current != null)
    {
        if (File.Exists(Path.Combine(current.FullName, "spyro-level-catalog.json")))
            return current.FullName;
        current = current.Parent;
    }

    throw new DirectoryNotFoundException("Pass the Spyro editor workspace root containing spyro-level-catalog.json.");
}

static string EscapeMarkdown(string value) => (value ?? "").Replace("|", "\\|", StringComparison.Ordinal);

internal sealed record LevelCoverageRow(
    string LevelKey,
    string LevelName,
    int OriginalFaceCount,
    int OriginalTextureCount,
    int NativeVariantCount,
    int GroupedVariantFaceCount,
    int ExplicitResidualFaceCount);

internal sealed record BehaviorRoundTripResult(bool Passed, string Note, string EditsPath);

internal sealed record BatchBehaviorGuardResult(
    bool Passed,
    string Note,
    string LevelKey,
    int TextureId,
    int FaceCount,
    int UniqueTriangleCount);

internal sealed record AtomicGuardResult(bool Passed, string Note, string PlanPath, string ManifestPath, string ExceptionMessage);
