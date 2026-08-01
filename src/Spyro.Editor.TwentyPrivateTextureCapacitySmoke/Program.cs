using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

const int RequestedPrivateRows = 20;

string workspaceRoot = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Directory.GetCurrentDirectory();
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
string sourceCuePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).cue");
string outputRoot = Path.Combine(
    workspaceRoot,
    "_local",
    "research",
    "twenty-private-texture-capacity");
Directory.CreateDirectory(outputRoot);
string wadAnalysisPath = Path.Combine(outputRoot, "source-bound-wad-analysis.json");
await WadAnalysisBuilder.EnsureCompatibleAsync(sourceImagePath, wadAnalysisPath);

LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition gnastysWorld = catalog.FindByKey("gnastysworld")
    ?? throw new InvalidOperationException("Gnasty's World is missing from the level catalog.");
NativeTerrainTextureRecordAppendSourceBinding binding =
    NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(
        sourceImagePath,
        gnastysWorld);
Assert(
    binding.ExpectedSourceTextureCount >= RequestedPrivateRows,
    "Gnasty's World no longer has twenty distinct retail donor rows for this capacity proof.");

NativeTerrainTextureRecordAppendCapacity capacity =
    NativeTerrainTextureRecordAppendBuilder.InspectCapacityForRecordCount(
        sourceImagePath,
        gnastysWorld,
        RequestedPrivateRows,
        wadAnalysisPath);
Assert(
    capacity.RequestedRecordCount == RequestedPrivateRows &&
    capacity.RequestedRecordGrowthBytes ==
        RequestedPrivateRows * NativeTerrainTextureRecordAppendBuilder.RecordGrowthBytes &&
    capacity.AdditionalBytesNeededForRequestedRecords ==
        capacity.RequestedRecordGrowthBytes - capacity.VerifiedLevelDataZeroTailBytes &&
    capacity.RequiredSectorAlignedEntryGrowthBytes == 0x1000 &&
    capacity.SectorRelocationPlanVerified &&
    capacity.WouldRequireTargetEntryGrowth,
    "The exact Gnasty's World twenty-row capacity no longer selects checked +0x1000 growth.");
int maximumRowsWithinCheckedStructuralGrowth =
    (capacity.VerifiedLevelDataZeroTailBytes +
     NativeTerrainTextureRecordAppendBuilder.MaximumCheckedSectorGrowthBytes) /
    NativeTerrainTextureRecordAppendBuilder.RecordGrowthBytes;
Assert(
    maximumRowsWithinCheckedStructuralGrowth == 24,
    "Gnasty's World's checked +0x1000 structural row ceiling changed from 24.");
NativeTerrainTextureRecordAppendCapacity firstUnsupportedCapacity =
    NativeTerrainTextureRecordAppendBuilder.InspectCapacityForRecordCount(
        sourceImagePath,
        gnastysWorld,
        maximumRowsWithinCheckedStructuralGrowth + 1,
        wadAnalysisPath);
Assert(
    firstUnsupportedCapacity.RequiredSectorAlignedEntryGrowthBytes == 0x1800 &&
    !firstUnsupportedCapacity.SectorRelocationPlanVerified &&
    firstUnsupportedCapacity.SectorRelocationFailure.Contains(
        "beyond the currently checked",
        StringComparison.OrdinalIgnoreCase),
    "The first three-sector Gnasty's World batch did not remain explicitly fail-closed.");

NativeTerrainTextureRelocationEdit[] privateRows = Enumerable
    .Range(0, RequestedPrivateRows)
    .Select(index => BuildEdit(
        binding,
        gnastysWorld,
        donorTextureId: index,
        materialTemplateTextureId: index,
        targetTextureId: binding.ExpectedSourceTextureCount + index))
    .ToArray();
string outputPrefix = Path.Combine(
    outputRoot,
    "Gnastys-World-20-private-rows-STATIC-ONLY-DO-NOT-RUN");
NativeTerrainTexturePrivateRecordBatchPlan plan =
    NativeTerrainTexturePrivateRecordBatchCompiler.BuildPlan(
        new NativeTerrainTexturePrivateRecordBatchRequest(
            sourceImagePath,
            sourceCuePath,
            outputPrefix,
            wadAnalysisPath,
            gnastysWorld,
            privateRows,
            OrdinaryLevelDataPatches: []));
Assert(
    plan.WriterKind == NativeTerrainTexturePrivateImageWriterKind.SectorRelocation &&
    plan.FixedTailPlan == null &&
    plan.SectorRelocationPlan != null &&
    plan.AppendedPrivateEdits.Count == RequestedPrivateRows &&
    plan.AppendedPrivateEdits.Select(edit => edit.TargetTextureId).SequenceEqual(
        Enumerable.Range(binding.ExpectedSourceTextureCount, RequestedPrivateRows)) &&
    plan.SectorRelocationPlan.Structural.SectorGrowthBytes == 0x1000 &&
    plan.SectorRelocationPlan.Structural.Append.OutputTextureCount ==
        binding.ExpectedSourceTextureCount + RequestedPrivateRows &&
    plan.SectorRelocationPlan.Structural.ExpandedWadSize ==
        plan.SectorRelocationPlan.Structural.OriginalWadSize + 0x1000 &&
    plan.SectorRelocationPlan.Structural.RelocatedExecutableLba ==
        plan.SectorRelocationPlan.Structural.OriginalExecutableLba + 2,
    "The twenty-row compiler did not retain contiguous ids and exact +0x1000 WAD/executable growth.");

bool normalAuthorized =
    AppendedPrivateTerrainTexturePromotionProfileRegistry.TryAuthorizeNormalRelease(
        binding,
        plan.WriterKind,
        RequestedPrivateRows,
        out AppendedPrivateTerrainTexturePromotionProfile? promotionProfile,
        out string normalGateReason);
Assert(
    !normalAuthorized && promotionProfile == null,
    "The static twenty-row research plan was accidentally promoted to normal Create BIN.");

string sourceSha256 = Sha256File(sourceImagePath);
long sourceLength = new FileInfo(sourceImagePath).Length;
NativeTerrainTexturePrivateRecordBatchExportResult exported =
    await NativeTerrainTexturePrivateRecordBatchCompiler.ExportAsync(plan);
Assert(
    exported.SourceImagePreserved &&
    exported.ExactReadbackVerified &&
    exported.RuntimeTargetReadbackVerified &&
    exported.GlobalLogicalReadbackVerified &&
    exported.AtomicRenameCompleted &&
    File.Exists(exported.OutputImagePath) &&
    File.Exists(exported.OutputCuePath),
    "The twenty-row static candidate omitted a structural or logical final-BIN readback proof.");

IReadOnlyList<TerrainTextureSlot> slots =
    TerrainPatchExporter.InspectTextureSlots(exported.OutputImagePath, gnastysWorld);
Assert(
    slots.Count == binding.ExpectedSourceTextureCount + RequestedPrivateRows &&
    slots.Skip(binding.ExpectedSourceTextureCount)
        .Select(slot => slot.TextureId)
        .SequenceEqual(Enumerable.Range(binding.ExpectedSourceTextureCount, RequestedPrivateRows)),
    "The final texture-table decoder did not retain all twenty contiguous private rows.");

NativeTerrainTextureGlobalRepackPackedRecord[] expectedRows =
    plan.SectorRelocationPlan!.GlobalPacking.OriginalMovableRecords
        .Concat(plan.SectorRelocationPlan.GlobalPacking.SyntheticRecords)
        .OrderBy(row => row.TargetTextureId)
        .ToArray();
bool logicalReadback =
    NativeTerrainTextureGlobalRepackerResearch.TryVerifyCandidateLogicalReadback(
        exported.OutputImagePath,
        gnastysWorld,
        expectedRows,
        sourceImagePath,
        out string logicalFailure);
Assert(
    logicalReadback &&
    plan.SectorRelocationPlan.GlobalPacking.SyntheticRecords.Count ==
        RequestedPrivateRows,
    $"Explicit immutable-source logical readback failed: {logicalFailure}");
Assert(
    string.Equals(Sha256File(sourceImagePath), sourceSha256, StringComparison.OrdinalIgnoreCase) &&
    new FileInfo(sourceImagePath).Length == sourceLength,
    "The twenty-row capacity smoke modified its immutable retail source.");

string proofPath = outputPrefix + "-static-proof.json";
await File.WriteAllTextAsync(
    proofPath,
    JsonSerializer.Serialize(
        new
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Scope = "Static final-BIN capacity/readback only; no DuckStation runtime claim and no normal-release promotion.",
            Target = new
            {
                gnastysWorld.Key,
                gnastysWorld.DisplayName,
                gnastysWorld.SourceWadEntry
            },
            Source = new
            {
                Path = sourceImagePath,
                Sha256 = sourceSha256,
                binding.ExpectedSourceTextureCount,
                binding.ExpectedTextureComponentSha256,
                binding.ExpectedLevelDataSha256
            },
            Capacity = new
            {
                capacity.RequestedRecordCount,
                capacity.RequestedRecordGrowthBytes,
                capacity.VerifiedLevelDataZeroTailBytes,
                capacity.AdditionalBytesNeededForRequestedRecords,
                capacity.RequiredSectorAlignedEntryGrowthBytes,
                capacity.SectorRelocationPlanVerified,
                capacity.AvailableIsoGrowthBytes,
                MaximumRowsWithinCheckedStructuralGrowth =
                    maximumRowsWithinCheckedStructuralGrowth,
                FirstUnsupportedRowCount = firstUnsupportedCapacity.RequestedRecordCount,
                FirstUnsupportedSectorGrowthBytes =
                    firstUnsupportedCapacity.RequiredSectorAlignedEntryGrowthBytes
            },
            TexturePagePacking = new
            {
                plan.SectorRelocationPlan.GlobalPacking.PackingProof.ProtectedByteCount,
                plan.SectorRelocationPlan.GlobalPacking.PackingProof.AllocatedByteCount,
                plan.SectorRelocationPlan.GlobalPacking.PackingProof.FreeByteCount,
                plan.SectorRelocationPlan.GlobalPacking.PackingProof.PackingStrategy
            },
            Output = new
            {
                exported.OutputImagePath,
                exported.OutputCuePath,
                exported.OutputImageSha256,
                TextureCount = slots.Count,
                AppendedTextureIds = Enumerable
                    .Range(binding.ExpectedSourceTextureCount, RequestedPrivateRows)
                    .ToArray()
            },
            Proof = new
            {
                exported.SourceImagePreserved,
                exported.ExactReadbackVerified,
                StaticRuntimeControlReadback = exported.RuntimeTargetReadbackVerified,
                exported.GlobalLogicalReadbackVerified,
                ExplicitImmutableSourceLogicalReadback = logicalReadback,
                exported.AtomicRenameCompleted,
                NormalReleaseAuthorized = normalAuthorized,
                NormalGateReason = normalGateReason,
                TerrainFaceAssignmentCount = 0,
                DuckStationRuntimeProven = false
            }
        },
        new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine("Twenty private terrain-texture row capacity smoke passed.");
Console.WriteLine(
    $"- Gnasty's World: T{binding.ExpectedSourceTextureCount}..T{binding.ExpectedSourceTextureCount + RequestedPrivateRows - 1}");
Console.WriteLine(
    $"- Level-data/WAD growth: +0x{capacity.RequiredSectorAlignedEntryGrowthBytes:X}; executable LBA +2");
Console.WriteLine($"- Exact static candidate: {exported.OutputImagePath}");
Console.WriteLine($"- Static proof: {proofPath}");
Console.WriteLine("- Normal Create BIN remains blocked; DuckStation runtime proof is not claimed.");

static NativeTerrainTextureRelocationEdit BuildEdit(
    NativeTerrainTextureRecordAppendSourceBinding binding,
    LevelDefinition donor,
    int donorTextureId,
    int materialTemplateTextureId,
    int targetTextureId) =>
    new(
        targetTextureId,
        donor.Key,
        donor.DisplayName,
        donor.SourceWadEntry,
        donorTextureId,
        NativeTerrainTextureRelocationEditStore.BuildTextureRecordProvenanceKey(
            donor.Key,
            donorTextureId),
        NativeTerrainTextureRelocationEditStore.CompleteDescriptorTier,
        "",
        "",
        "2026-07-25T20:00:00Z",
        NativeTerrainTextureRelocationApplyMode.ArtOnlyPreserveTarget,
        NativeTerrainTextureTargetRecordKind.AppendedPrivate,
        binding.TargetWadEntry,
        binding.SourceImageSha256,
        binding.ExpectedSourceTextureCount,
        binding.ExpectedTextureComponentSha256,
        binding.ExpectedLevelDataSha256,
        materialTemplateTextureId,
        NativeTerrainTextureRelocationEditStore.BuildAppendedPrivateRecordId(targetTextureId));

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static string Sha256File(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream));
}
