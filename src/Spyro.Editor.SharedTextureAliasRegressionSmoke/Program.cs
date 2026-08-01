using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

const int TargetTextureId = 5;
const int DonorTextureId = 10;

string workspaceRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
string sourceCuePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).cue");
LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition artisans = catalog.FindByKey("artisans")
    ?? throw new InvalidOperationException("Artisans is missing from the level catalog.");
LevelDefinition donorLevel = catalog.FindByKey("gnastysworld")
    ?? throw new InvalidOperationException("Gnasty's World is missing from the level catalog.");

NativeTerrainTextureInPlaceTransplantRequest inPlaceRequest = new(
    TargetTextureId,
    donorLevel.SourceWadEntry,
    DonorTextureId);
NativeTerrainTextureInPlaceTransplantSourceProof inPlaceProof =
    NativeTerrainTextureInPlaceTransplantBuilder.InspectSourceProof(
        sourceImagePath,
        artisans,
        inPlaceRequest);
bool inPlaceBuilt = NativeTerrainTextureInPlaceTransplantBuilder.TryBuild(
    sourceImagePath,
    artisans,
    inPlaceRequest,
    inPlaceProof,
    out NativeTerrainTextureInPlaceTransplantPlan? inPlacePlan,
    out string inPlaceFailure);
Assert(!inPlaceBuilt && inPlacePlan == null &&
       inPlaceFailure.Contains("target texture-page byte 0x37F", StringComparison.OrdinalIgnoreCase) &&
       inPlaceFailure.Contains("normal-0 pixel (31,0)", StringComparison.OrdinalIgnoreCase) &&
       inPlaceFailure.Contains("normal-1 pixel (0,0)", StringComparison.OrdinalIgnoreCase),
    $"Artisans T5 <- Gnasty's World T10 no longer reproduces the screenshot's exact in-place alias conflict: {inPlaceFailure}");

int[] isolatedClosure = NativeTexturePageOwnershipScanner
    .FindTerrainTextureStorageOverlapClosure(sourceImagePath, artisans, [TargetTextureId])
    .ToArray();
Assert(isolatedClosure.SequenceEqual([TargetTextureId]),
    $"Artisans T5 overlap closure changed: {string.Join(",", isolatedClosure)}.");
NativeTerrainTextureRuntimeControlAudit isolatedRuntime =
    NativeTerrainTextureRuntimeControlScanner.Inspect(sourceImagePath, artisans);
Assert(isolatedRuntime.Complete && isolatedClosure.All(isolatedRuntime.IsRuntimePersistentTarget),
    "Artisans T5 is no longer a runtime-persistent relocation target.");
Assert(NativeTexturePageOwnershipScanner.TryBuildRelocationOwnershipProof(
        sourceImagePath,
        artisans,
        isolatedClosure,
        out NativeTexturePageRelocationOwnershipProofResult? isolatedOwnership,
        out string isolatedOwnershipFailure) &&
    isolatedOwnership != null,
    $"Artisans T5 relocation ownership proof failed: {isolatedOwnershipFailure}");
NativeTerrainTextureRelocationExportPlan isolatedRelocation = NativeTerrainTextureRelocationComposer.BuildPlan(
    new NativeTerrainTextureRelocationExportRequest(
        sourceImagePath,
        sourceCuePath,
        Path.Combine(Path.GetTempPath(), "spyro-editor-artisans-t5-from-gnastysworld-t10-alias-regression"),
        artisans,
        [Donor(TargetTextureId, DonorTextureId)],
        isolatedOwnership!.Proof,
        WriteImage: false));
NativeTerrainTextureRelocationPlan isolatedPlan = isolatedRelocation.Relocation;
Assert(isolatedRelocation.RuntimeTargetsPersistent &&
       isolatedPlan.ExactDonorIndexedPixelsVerified &&
       isolatedPlan.ExactDonorPalettesVerified &&
       isolatedPlan.LowDetailAliasPreserved &&
       isolatedPlan.LogicalReadbackVerified &&
       isolatedPlan.ProtectedStorageVerified &&
       isolatedPlan.TargetDescriptorMaterialPolicyVerified &&
       isolatedPlan.Imports.Any(import =>
           import.TargetTextureId == TargetTextureId &&
           import.DonorWadEntry == donorLevel.SourceWadEntry &&
           import.DonorTextureId == DonorTextureId),
    "Artisans T5 <- Gnasty's World T10 byte-private fallback omitted a required safety/readback proof.");
Console.WriteLine(
    $"Artisans exact screenshot conflict reproduced; isolated relocation PASS " +
    $"({isolatedPlan.AllocatedPixelByteCount + isolatedPlan.AllocatedPaletteByteCount:N0} bytes).\n  {inPlaceFailure}");
Dictionary<int, NativeTerrainTextureRelocationImport> observedStagedImports = new()
{
    [10] = Donor(10, 9),
    [22] = Donor(22, 29),
    [48] = Donor(48, 23),
    [52] = Donor(52, 7),
    [54] = Donor(54, 23),
    [55] = Donor(55, 17),
    [56] = Donor(56, 16)
};
Dictionary<int, NativeTerrainTextureRelocationImport> requestedBatch =
    observedStagedImports.ToDictionary(pair => pair.Key, pair => pair.Value);
requestedBatch[TargetTextureId] = Donor(TargetTextureId, DonorTextureId);

HashSet<int> requiredFallbackTargets = [TargetTextureId];
foreach ((int targetTextureId, NativeTerrainTextureRelocationImport import) in observedStagedImports)
{
    NativeTerrainTextureInPlaceTransplantRequest request = new(
        targetTextureId,
        import.DonorWadEntry,
        import.DonorTextureId);
    NativeTerrainTextureInPlaceTransplantSourceProof proof =
        NativeTerrainTextureInPlaceTransplantBuilder.InspectSourceProof(sourceImagePath, artisans, request);
    if (!NativeTerrainTextureInPlaceTransplantBuilder.TryBuild(
            sourceImagePath,
            artisans,
            request,
            proof,
            out NativeTerrainTextureInPlaceTransplantPlan? plan,
            out _) ||
        plan == null)
    {
        requiredFallbackTargets.Add(targetTextureId);
    }
}

int[] promotionCandidates = observedStagedImports.Keys
    .Where(textureId => !requiredFallbackTargets.Contains(textureId))
    .Order()
    .ToArray();
NativeTerrainTexturePromotionAttemptSet promotionAttempts =
    NativeTerrainTextureRelocationAllocator.BuildBoundedPromotionAttempts(promotionCandidates);
List<string> batchFailures = [];
NativeTerrainTextureRelocationExportPlan? observedBatchPlan = null;
int[] observedBatchClosure = [];
foreach (int[] promotedTargets in promotionAttempts.Attempts)
{
    int[] closure = NativeTexturePageOwnershipScanner.FindTerrainTextureStorageOverlapClosure(
            sourceImagePath,
            artisans,
            requiredFallbackTargets.Concat(promotedTargets).Distinct().Order().ToArray())
        .ToArray();
    NativeTerrainTextureRuntimeControlAudit runtime =
        NativeTerrainTextureRuntimeControlScanner.Inspect(sourceImagePath, artisans);
    int[] controlled = closure.Where(textureId => !runtime.IsRuntimePersistentTarget(textureId)).ToArray();
    if (controlled.Length > 0)
    {
        batchFailures.Add($"[{string.Join(",", closure)}] runtime controlled {string.Join(",", controlled)}");
        continue;
    }
    if (!NativeTexturePageOwnershipScanner.TryBuildRelocationOwnershipProof(
            sourceImagePath,
            artisans,
            closure,
            out NativeTexturePageRelocationOwnershipProofResult? ownership,
            out string ownershipFailure) ||
        ownership == null)
    {
        batchFailures.Add($"[{string.Join(",", closure)}] ownership {ownershipFailure}");
        continue;
    }
    NativeTerrainTextureRelocationImport[] imports = closure
        .Select(textureId => requestedBatch.TryGetValue(textureId, out NativeTerrainTextureRelocationImport? import)
            ? import
            : new NativeTerrainTextureRelocationImport(
                textureId,
                artisans.SourceWadEntry,
                textureId,
                NativeTerrainTextureRelocationEditStore.CompleteDescriptorTier,
                PreserveTargetDescriptorMaterial: true))
        .ToArray();
    try
    {
        observedBatchPlan = NativeTerrainTextureRelocationComposer.BuildPlan(
            new NativeTerrainTextureRelocationExportRequest(
                sourceImagePath,
                sourceCuePath,
                Path.Combine(Path.GetTempPath(), "spyro-editor-artisans-observed-shared-texture-alias-regression"),
                artisans,
                imports,
                ownership.Proof,
                WriteImage: false));
        observedBatchClosure = closure;
        break;
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or IOException or OverflowException)
    {
        batchFailures.Add($"[{string.Join(",", closure)}] relocation {ex.Message}");
    }
}

Assert(observedBatchPlan != null,
    "The exact Artisans T5 <- Gnasty's World T10 request failed when composed with the seven already-staged cross-level records: " +
    string.Join(" | ", batchFailures.Take(8)));
Assert(observedBatchPlan!.Relocation.Imports.Any(import =>
        import.TargetTextureId == TargetTextureId &&
        import.DonorWadEntry == donorLevel.SourceWadEntry &&
        import.DonorTextureId == DonorTextureId),
    "The successful observed-state batch lost the newly requested T5 <- Gnasty's World T10 record.");
Console.WriteLine(
    $"Artisans observed seven-edit state + T5 <- Gnasty's World T10: relocation PASS " +
    $"(required {string.Join(",", requiredFallbackTargets.Order())}; closure {string.Join(",", observedBatchClosure)})." );

Console.WriteLine("Shared texture alias regression smoke passed.");

NativeTerrainTextureRelocationImport Donor(int targetTextureId, int donorTextureId) => new(
    targetTextureId,
    donorLevel.SourceWadEntry,
    donorTextureId,
    NativeTerrainTextureRelocationEditStore.CompleteDescriptorTier,
    PreserveTargetDescriptorMaterial: true);

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
