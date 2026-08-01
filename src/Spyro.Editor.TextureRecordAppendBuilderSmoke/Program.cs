using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

const int ExpectedSourceTextureCount = 68;
const int ExpectedAppendedTextureId = 68;
const int DonorTextureId = 17;
const int MaterialTemplateTextureId = 55;

string workspaceRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
string sourceCuePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).cue");
string outputRoot = Path.Combine(workspaceRoot, "_local", "smoke", "texture-record-append-builder");
Directory.CreateDirectory(outputRoot);
foreach (string stale in Directory.EnumerateFiles(outputRoot))
    File.Delete(stale);
string wadAnalysisPath = Path.Combine(outputRoot, "source-bound-wad-analysis.json");
await WadAnalysisBuilder.BuildAsync(sourceImagePath, wadAnalysisPath);

string sourceSha256 = Sha256File(sourceImagePath);
long sourceLength = new FileInfo(sourceImagePath).Length;
LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition artisans = catalog.FindByKey("artisans")
    ?? throw new InvalidOperationException("Artisans is missing from the level catalog.");
LevelDefinition gnastysWorld = catalog.FindByKey("gnastysworld")
    ?? throw new InvalidOperationException("Gnasty's World is missing from the level catalog.");
NativeTerrainTextureRecordAppendCapacity artisansCapacity =
    NativeTerrainTextureRecordAppendBuilder.InspectCapacity(
        sourceImagePath,
        artisans,
        wadAnalysisPath);
Assert(
    artisansCapacity.CanAppendInsideCurrentSubfile &&
    artisansCapacity.HasExecutableWriter &&
    string.Equals(
        artisansCapacity.ExecutableWriter,
        "FixedTail",
        StringComparison.Ordinal) &&
    !artisansCapacity.CanExtendSubfileWithoutMovingLaterSubfiles &&
    !artisansCapacity.CanShiftLaterSubfilesInsideCurrentEntry &&
    artisansCapacity.Notes.Any(note =>
        note.Contains(
            "unsupported by every executable writer",
            StringComparison.OrdinalIgnoreCase)),
    "Capacity reporting made an unimplemented following-gap or later-subfile strategy look executable.");

bool researchBuilt = NativeTerrainTextureRecordAppendResearch.TryBuild(
    new NativeTerrainTextureRecordAppendResearchRequest(
        sourceImagePath,
        sourceCuePath,
        artisans,
        gnastysWorld,
        DonorTextureId),
    out NativeTerrainTextureRecordAppendResearchPlan? researchCandidate,
    out string researchFailure);
Assert(researchBuilt && researchCandidate != null,
    $"Independent packed-row fixture failed: {researchFailure}");
NativeTerrainTextureRecordAppendResearchPlan research = researchCandidate!;
Assert(research.SourceTextureCount == ExpectedSourceTextureCount &&
       research.AppendedTextureId == ExpectedAppendedTextureId,
    "The retail Artisans fixture no longer has the expected 68->69 layout.");

int appendedLowOffset = 8 + (ExpectedSourceTextureCount * NativeTerrainTextureRecordAppendBuilder.LowDetailRecordBytes);
int appendedHighOffset = research.OutputTextureComponentByteLength - NativeTerrainTextureRecordAppendBuilder.HighDetailRecordBytes;
byte[] packedLow = research.OutputLevelData
    .AsSpan(appendedLowOffset, NativeTerrainTextureRecordAppendBuilder.LowDetailRecordBytes)
    .ToArray();
byte[] packedHigh = research.OutputLevelData
    .AsSpan(appendedHighOffset, NativeTerrainTextureRecordAppendBuilder.HighDetailRecordBytes)
    .ToArray();
NativeTerrainTexturePackedAppendRecord packedRecord = new(
    StableEditId: "private-terrain-texture:artisans:eye:gnastysworld:17",
    DonorLevelKey: gnastysWorld.Key,
    DonorWadEntry: gnastysWorld.SourceWadEntry,
    DonorTextureId: DonorTextureId,
    MaterialTemplateTextureId: MaterialTemplateTextureId,
    LowDetailRow: packedLow,
    HighDetailRow: packedHigh,
    ExpectedLowDetailSha256: Sha256Bytes(packedLow),
    ExpectedHighDetailSha256: Sha256Bytes(packedHigh));
NativeTerrainTextureRecordAppendSourceBinding binding =
    NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(sourceImagePath, artisans);
NativeTerrainTextureRecordAppendRequest request = new(
    sourceImagePath,
    artisans,
    binding,
    [packedRecord],
    Array.Empty<NativeTerrainTextureRecordExistingPatch>());

bool built = NativeTerrainTextureRecordAppendBuilder.TryBuild(
    request,
    out NativeTerrainTextureRecordAppendPlan? candidate,
    out string failure);
Assert(built && candidate != null, $"Production append builder failed: {failure}");
NativeTerrainTextureRecordAppendPlan plan = candidate!;
Assert(plan.Patch.After.AsSpan().SequenceEqual(research.OutputLevelData),
    "Builder output differs from the independent structural fixture.");
Assert(plan.SourceBinding.ExpectedSourceTextureCount == ExpectedSourceTextureCount &&
       plan.SourceBinding.ExpectedTextureComponentSha256.Length == 64 &&
       plan.SourceBinding.ExpectedLevelDataSha256.Length == 64 &&
       plan.OutputTextureComponentSha256.Length == 64,
    "Saved-edit source/output hash identity is incomplete.");
Assert(plan.SevenBitTextureIdsVerified &&
       plan.FixedSubfileBoundaryPreserved &&
       plan.ArchiveHeadersRequireNoFixup &&
       plan.RuntimeTargetsPersistent &&
       plan.ExistingRowsPreserved &&
       plan.AppendedRowsCopiedExactly &&
       plan.ShiftedComponentChainReparsed &&
       plan.ShiftedTerrainSurfaceSemanticsVerified &&
       plan.ZeroTailVerified &&
       plan.ExistingPatchesRebasedExactly,
    "Builder omitted a required structural proof.");
Assert(plan.ResolvedRecords.Count == 1 &&
       plan.ResolvedRecords[0].StableEditId == packedRecord.StableEditId &&
       plan.ResolvedRecords[0].AssignedTextureId == ExpectedAppendedTextureId &&
       plan.ResolvedRecords[0].MaterialTemplateTextureId == MaterialTemplateTextureId,
    "Stable edit identity, assigned T68, or material-template identity changed.");

NativeTerrainTextureRecordAppendComponentProof environment = research.Components
    .First(component => component.Name == "environment");
long oldEnvironmentWadOffset = research.LevelDataWadOffset + environment.SourceOffset + 4;
byte environmentBefore = research.SourceLevelData[environment.SourceOffset + 4];
byte environmentAfter = (byte)(environmentBefore ^ 0x01);
NativeTerrainTextureRecordExistingPatch existingPatch = new(
    oldEnvironmentWadOffset,
    [environmentBefore],
    [environmentAfter],
    "existing-level-data-smoke",
    "environment-byte-smoke");
bool rebasedBuilt = NativeTerrainTextureRecordAppendBuilder.TryBuild(
    request with { ExistingLevelDataPatches = [existingPatch] },
    out NativeTerrainTextureRecordAppendPlan? rebasedCandidate,
    out string rebasedFailure);
Assert(rebasedBuilt && rebasedCandidate != null,
    $"Existing level-data patch could not be rebased: {rebasedFailure}");
NativeTerrainTextureRecordAppendPlan rebased = rebasedCandidate!;
Assert(rebased.RebasedPatches.Count == 1 &&
       rebased.RebasedPatches[0].SourceWadOffset == oldEnvironmentWadOffset &&
       rebased.RebasedPatches[0].OutputWadOffset == oldEnvironmentWadOffset + NativeTerrainTextureRecordAppendBuilder.RecordGrowthBytes,
    "A suffix patch did not shift by exactly one 184-byte record.");
int rebasedRelativeOffset = checked((int)(rebased.RebasedPatches[0].OutputWadOffset - rebased.LevelDataWadOffset));
Assert(rebased.Patch.After[rebasedRelativeOffset] == environmentAfter,
    "The existing patch after-byte was lost during structural composition.");

NativeTerrainTextureRecordAppendSourceBinding staleBinding = binding with
{
    ExpectedSourceTextureCount = binding.ExpectedSourceTextureCount - 1
};
bool staleBuilt = NativeTerrainTextureRecordAppendBuilder.TryBuild(
    request with { SourceBinding = staleBinding },
    out NativeTerrainTextureRecordAppendPlan? stalePlan,
    out string staleFailure);
Assert(!staleBuilt && stalePlan == null && staleFailure.Contains("preimage", StringComparison.OrdinalIgnoreCase),
    $"Stale saved-edit preimage was not rejected: {staleFailure}");

bool duplicateBuilt = NativeTerrainTextureRecordAppendBuilder.TryBuild(
    request with { Records = [packedRecord, packedRecord] },
    out NativeTerrainTextureRecordAppendPlan? duplicatePlan,
    out string duplicateFailure);
Assert(!duplicateBuilt && duplicatePlan == null && duplicateFailure.Contains("duplicate", StringComparison.OrdinalIgnoreCase),
    $"Duplicate stable edit identity was not rejected: {duplicateFailure}");

NativeTerrainTexturePackedAppendRecord[] overflowRecords = Enumerable.Range(0, 61)
    .Select(index => packedRecord with { StableEditId = $"seven-bit-overflow:{index}" })
    .ToArray();
bool overflowBuilt = NativeTerrainTextureRecordAppendBuilder.TryBuild(
    request with { Records = overflowRecords },
    out NativeTerrainTextureRecordAppendPlan? overflowPlan,
    out string overflowFailure);
Assert(!overflowBuilt && overflowPlan == null && overflowFailure.Contains("seven-bit", StringComparison.OrdinalIgnoreCase),
    $"T128 overflow was not rejected: {overflowFailure}");

string outputPrefix = Path.Combine(outputRoot, "Artisans-Append-T68-STRUCTURAL-DO-NOT-RUN");
NativeTerrainTextureRecordAppendExportResult exported =
    await NativeTerrainTextureRecordAppendBuilder.ExportAsync(plan, sourceCuePath, outputPrefix);
Assert(exported.SourcePreimageVerified &&
       exported.ArchiveHeadersPreserved &&
       exported.ExactLevelDataReadbackVerified &&
       exported.RuntimeTargetReadbackVerified &&
       File.Exists(exported.OutputImagePath) &&
       File.Exists(exported.OutputCuePath),
    "Atomic exporter omitted source/header/level-data/runtime readback proof.");
IReadOnlyList<TerrainTextureSlot> decoded = TerrainPatchExporter.InspectTextureSlots(
    exported.OutputImagePath,
    artisans);
Assert(decoded.Count == ExpectedSourceTextureCount + 1 && decoded[^1].TextureId == ExpectedAppendedTextureId,
    "Normal decoder did not read appended T68 from the final BIN.");

bool sourceOverwriteRejected = false;
try
{
    string sourcePrefix = sourceImagePath[..^Path.GetExtension(sourceImagePath).Length];
    await NativeTerrainTextureRecordAppendBuilder.ExportAsync(plan, sourceCuePath, sourcePrefix);
}
catch (InvalidOperationException ex) when (ex.Message.Contains("must not overwrite", StringComparison.OrdinalIgnoreCase))
{
    sourceOverwriteRejected = true;
}
Assert(sourceOverwriteRejected, "Atomic exporter did not reject a source-overwrite destination.");
Assert(Sha256File(sourceImagePath).Equals(sourceSha256, StringComparison.OrdinalIgnoreCase) &&
       new FileInfo(sourceImagePath).Length == sourceLength,
    "Structural smoke modified the retail source BIN.");

Dictionary<string, int> expectedBlockedZeroTails = new(StringComparer.OrdinalIgnoreCase)
{
    ["stonehill"] = 152,
    ["darkhollow"] = 176,
    ["peacekeepers"] = 20,
    ["mistybog"] = 116,
    ["loftycastle"] = 52
};
NativeTerrainTextureRecordAppendCapacity[] blockedCapacities = expectedBlockedZeroTails
    .Select(pair =>
    {
        LevelDefinition level = catalog.FindByKey(pair.Key)
            ?? throw new InvalidOperationException($"{pair.Key} is missing from the level catalog.");
        NativeTerrainTextureRecordAppendCapacity capacity =
            NativeTerrainTextureRecordAppendBuilder.InspectCapacity(sourceImagePath, level, wadAnalysisPath);
        Assert(capacity.VerifiedLevelDataZeroTailBytes == pair.Value,
            $"{level.DisplayName}'s append-capacity preimage changed from {pair.Value} to {capacity.VerifiedLevelDataZeroTailBytes} bytes.");
        Assert(capacity.WouldRequireTargetEntryGrowth &&
               capacity.RequiredSectorAlignedEntryGrowthBytes == 0x800 &&
               capacity.SectorRelocationPlanVerified &&
               capacity.AvailableIsoGrowthBytes >= 0x800 &&
               capacity.HasExecutableWriter &&
               string.Equals(
                   capacity.ExecutableWriter,
                   "SectorRelocation",
                   StringComparison.Ordinal) &&
               !capacity.CanExtendSubfileWithoutMovingLaterSubfiles &&
               !capacity.CanShiftLaterSubfilesInsideCurrentEntry &&
               capacity.Notes.Any(note =>
                   note.Contains(
                       "unsupported by every executable writer",
                       StringComparison.OrdinalIgnoreCase)),
            $"{level.DisplayName}'s required one-sector WAD relocation was not independently proven: {capacity.SectorRelocationFailure}");
        return capacity;
    })
    .ToArray();

Console.WriteLine("Native terrain texture record append builder smoke passed.");
Console.WriteLine($"Identity: {packedRecord.StableEditId} -> T{plan.ResolvedRecords[0].AssignedTextureId}; material template T{MaterialTemplateTextureId}.");
Console.WriteLine($"Structure: {plan.SourceTextureCount}->{plan.OutputTextureCount}; +{plan.GrowthByteCount} bytes; {plan.ShiftedComponents.Count} suffix component(s) reparsed.");
Console.WriteLine($"Rebase: WAD 0x{oldEnvironmentWadOffset:X}->0x{rebased.RebasedPatches[0].OutputWadOffset:X}.");
Console.WriteLine($"Atomic final BIN: {exported.OutputImagePath}");
foreach (NativeTerrainTextureRecordAppendCapacity capacity in blockedCapacities)
{
    Console.WriteLine(
        $"Capacity {capacity.LevelName}: tail={capacity.VerifiedLevelDataZeroTailBytes}, need+={capacity.AdditionalBytesNeededForOneRecord}, " +
        $"next=T{capacity.FollowingSubfileIndex?.ToString() ?? "none"}@0x{capacity.FollowingSubfileRelativeOffset:X}, " +
        $"gap={capacity.ImmediateFollowingGapBytes}/zero={capacity.ImmediateFollowingZeroBytes}, later={capacity.SubsequentSubfileCount}, " +
        $"entry-tail={capacity.TargetEntryTrailingBytes}/zero={capacity.TargetEntryTrailingZeroBytes}, " +
        $"outer-gap={capacity.OuterFollowingEntryGapBytes}/zero={capacity.OuterFollowingEntryZeroBytes}, " +
        $"gap-candidate={capacity.ImmediateGapExtensionStorageCandidate}/supported={capacity.CanExtendSubfileWithoutMovingLaterSubfiles}, " +
        $"shift-candidate={capacity.LaterNestedSubfileShiftStorageCandidate}/supported={capacity.CanShiftLaterSubfilesInsideCurrentEntry}, " +
        $"writer={capacity.ExecutableWriter}, grow-entry={capacity.WouldRequireTargetEntryGrowth}, " +
        $"sector-growth=0x{capacity.RequiredSectorAlignedEntryGrowthBytes:X}, relocation={capacity.SectorRelocationPlanVerified}, iso-headroom={capacity.AvailableIsoGrowthBytes?.ToString("N0") ?? "unknown"}." +
        (string.IsNullOrWhiteSpace(capacity.SectorRelocationFailure) ? "" : $" failure={capacity.SectorRelocationFailure}"));
}

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

static string Sha256Bytes(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexString(SHA256.HashData(bytes));
