using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

const int RequestedPrivateRows = 50;
const int ExpectedSourceTextureCount = 32;
const int WadLba = 37;

string workspaceRoot = ResolveWorkspaceRoot(args.FirstOrDefault());
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
string sourceCuePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).cue");
string overlayPath = Path.Combine(
    workspaceRoot,
    "editor-cache",
    "gnastysworld-runtime-scene-editor-overlay.json");
string outputRoot = Path.Combine(
    workspaceRoot,
    "_local",
    "research",
    "fifty-private-texture-final-bin");
string outputPrefix = Path.Combine(
    outputRoot,
    "Gnastys-World-50-private-face-swaps-RUNTIME-PROVEN");
string editsPath = Path.Combine(
    outputRoot,
    "Gnastys-World-50-private-face-swaps.terrain-edits.json");
string sourceSearchPath = Path.Combine(
    outputRoot,
    "Gnastys-World-retail-source-search.json");
string wadAnalysisPath = Path.Combine(
    outputRoot,
    "source-bound-wad-analysis.json");
string proofPath = outputPrefix + "-proof.json";

Directory.CreateDirectory(outputRoot);
foreach (string path in new[]
         {
             outputPrefix + ".bin",
             outputPrefix + ".cue",
             editsPath,
             sourceSearchPath,
             proofPath
         })
{
    if (File.Exists(path))
        File.Delete(path);
}

Assert(File.Exists(sourceImagePath), $"Missing retail source BIN: {sourceImagePath}");
Assert(File.Exists(sourceCuePath), $"Missing retail source CUE: {sourceCuePath}");
Assert(File.Exists(overlayPath), $"Missing Gnasty's World geometry overlay: {overlayPath}");
await WadAnalysisBuilder.EnsureCompatibleAsync(sourceImagePath, wadAnalysisPath);

string sourceSha256 = Sha256File(sourceImagePath);
long sourceLength = FileLength(sourceImagePath);
DateTime sourceWriteTime = File.GetLastWriteTimeUtc(sourceImagePath);
LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition gnastysWorld = catalog.FindByKey("gnastysworld")
    ?? throw new InvalidOperationException("Gnasty's World is missing from the level catalog.");
NativeTerrainTextureRecordAppendSourceBinding binding =
    NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(
        sourceImagePath,
        gnastysWorld);
Assert(
    binding.ExpectedSourceTextureCount == ExpectedSourceTextureCount,
    $"Gnasty's World retail texture count changed from {ExpectedSourceTextureCount} to {binding.ExpectedSourceTextureCount}.");

NativeTerrainTextureRecordAppendCapacity normalCapacity =
    NativeTerrainTextureRecordAppendBuilder.InspectCapacityForRecordCount(
        sourceImagePath,
        gnastysWorld,
        RequestedPrivateRows,
        wadAnalysisPath);
NativeTerrainTextureRecordAppendCapacity staticCapacity =
    NativeTerrainTextureRecordAppendBuilder.InspectCapacityForRecordCount(
        sourceImagePath,
        gnastysWorld,
        RequestedPrivateRows,
        NativeTerrainTextureStructuralGrowthPolicy.FiftyRowStaticResearch,
        wadAnalysisPath);
NativeTerrainTextureRecordAppendCapacity runtimeCapacity =
    NativeTerrainTextureRecordAppendBuilder.InspectCapacityForRecordCount(
        sourceImagePath,
        gnastysWorld,
        RequestedPrivateRows,
        NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenExtended,
        wadAnalysisPath);
Assert(
    !normalCapacity.HasExecutableWriter &&
    normalCapacity.RequiredSectorAlignedEntryGrowthBytes == 0x2800 &&
    staticCapacity.HasExecutableWriter &&
    staticCapacity.SectorRelocationPlanVerified &&
    staticCapacity.RequiredSectorAlignedEntryGrowthBytes == 0x2800 &&
    staticCapacity.StaticResearchOnly &&
    runtimeCapacity.HasExecutableWriter &&
    runtimeCapacity.SectorRelocationPlanVerified &&
    runtimeCapacity.RequiredSectorAlignedEntryGrowthBytes == 0x2800 &&
    !runtimeCapacity.StaticResearchOnly,
    "The fifty-row candidate did not stay blocked under default normal policy while passing exact +0x2800 static and runtime-proven planning.");

GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
TerrainSourceSearchResult sourceSearch =
    await TerrainSourceSearchBuilder.BuildSourceDerivedAsync(
        new SourceDerivedTerrainSourceSearchRequest(
            sourceImagePath,
            sourceSearchPath,
            gnastysWorld,
            geometry));
HashSet<string> exactSourceKeys = sourceSearch.Report.Results
    .Where(result => result.FullSectorHits.Count == 1)
    .Select(result => result.Edit)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
TerrainPolygon[][] facesByTexture = geometry.Polygons
    .Where(face =>
        face.OriginalTextureId >= 0 &&
        face.OriginalTextureId < binding.ExpectedSourceTextureCount &&
        face.HasCompleteNativeHighPolyFacePayload &&
        face.SectorOffset >= 0 &&
        face.FaceOffset >= 0 &&
        exactSourceKeys.Contains(face.RuntimeKey))
    .GroupBy(face => face.OriginalTextureId)
    .OrderBy(group => group.Key)
    .Select(group => group
        .OrderBy(face => face.SectorIndex)
        .ThenBy(face => face.FaceIndex)
        .ToArray())
    .ToArray();

List<TerrainPolygon> selectedFaces = [];
for (int ordinal = 0;
     selectedFaces.Count < RequestedPrivateRows;
     ordinal++)
{
    int before = selectedFaces.Count;
    foreach (TerrainPolygon[] group in facesByTexture)
    {
        if (ordinal < group.Length)
            selectedFaces.Add(group[ordinal]);
        if (selectedFaces.Count == RequestedPrivateRows)
            break;
    }
    if (selectedFaces.Count == before)
        break;
}
Assert(
    selectedFaces.Count == RequestedPrivateRows &&
    selectedFaces.Select(face => face.RuntimeKey)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count() == RequestedPrivateRows,
    "Gnasty's World does not expose fifty unique exact source-bound HP faces for the runtime-proven candidate.");

Dictionary<int, int> materialOrdinals = [];
List<PrivateRowContract> contracts = [];
for (int index = 0; index < selectedFaces.Count; index++)
{
    TerrainPolygon face = selectedFaces[index];
    int materialTextureId = face.OriginalTextureId;
    int materialOrdinal = materialOrdinals.GetValueOrDefault(materialTextureId);
    materialOrdinals[materialTextureId] = materialOrdinal + 1;
    int donorTextureId =
        (materialTextureId + materialOrdinal) %
        binding.ExpectedSourceTextureCount;
    int assignedTextureId = binding.ExpectedSourceTextureCount + index;
    face.ApplyTextureOverride(assignedTextureId);
    contracts.Add(new PrivateRowContract(
        face.RuntimeKey,
        materialTextureId,
        donorTextureId,
        assignedTextureId));
}
Assert(
    contracts.Select(contract =>
            (contract.DonorTextureId, contract.MaterialTemplateTextureId))
        .Distinct()
        .Count() == RequestedPrivateRows,
    "The fifty-row runtime-proven corpus contains a duplicate donor/material contract.");

int savedFaceCount = await TerrainEditStore.SaveAsync(
    editsPath,
    selectedFaces,
    "Gnasty's World fifty independent private-face assignments; exact runtime-proven clean-USA profile");
Assert(
    savedFaceCount == RequestedPrivateRows,
    $"Saved {savedFaceCount}/{RequestedPrivateRows} private-face assignments.");

TerrainPatchPlan ordinaryTerrainPlan = TerrainPatchExporter.BuildPlan(
    sourceImagePath,
    sourceCuePath,
    outputPrefix + "-not-written.bin",
    outputPrefix + "-not-written.cue",
    gnastysWorld,
    ramPath: "",
    sourceSearchPath,
    editsPath);
TerrainPatch[] facePatches = ordinaryTerrainPlan.Patches
    .Where(patch => patch.Kind.Equals(
        "texture-id-word3",
        StringComparison.OrdinalIgnoreCase))
    .ToArray();
Assert(
    ordinaryTerrainPlan.Patches.Count == RequestedPrivateRows &&
    facePatches.Length == RequestedPrivateRows &&
    facePatches.Select(patch => patch.RuntimeKey)
        .ToHashSet(StringComparer.OrdinalIgnoreCase)
        .SetEquals(contracts.Select(contract => contract.RuntimeKey)),
    "The terrain planner did not emit exactly fifty isolated native face texture-ID patches.");
Assert(
    ordinaryTerrainPlan.SkippedEdits.All(item =>
        item.Equals(
            "collision: source-derived exact triangle scan found no matching source collision records for the edited terrain face(s).",
            StringComparison.Ordinal)),
    $"The terrain planner reported an unexpected skip: {string.Join(" | ", ordinaryTerrainPlan.SkippedEdits)}");

IReadOnlyList<NativeTerrainTextureRecordExistingPatch> ordinaryPatches =
    NativeTerrainTexturePrivateRecordBatchCompiler.ConvertOrdinaryTerrainPlan(
        ordinaryTerrainPlan);
Assert(
    ordinaryPatches.Count == RequestedPrivateRows &&
    ordinaryPatches.All(patch =>
        patch.Before.Length == sizeof(uint) &&
        patch.After.Length == sizeof(uint) &&
        patch.Kind.Equals("texture-id-word3", StringComparison.OrdinalIgnoreCase)),
    "The private compiler did not preserve fifty exact four-byte face patches.");
foreach (PrivateRowContract contract in contracts)
{
    NativeTerrainTextureRecordExistingPatch patch = ordinaryPatches.Single(item =>
        item.RuntimeKey.Equals(contract.RuntimeKey, StringComparison.OrdinalIgnoreCase));
    uint beforeWord = BinaryPrimitives.ReadUInt32LittleEndian(patch.Before);
    uint afterWord = BinaryPrimitives.ReadUInt32LittleEndian(patch.After);
    Assert(
        (beforeWord & 0x7F) == contract.MaterialTemplateTextureId &&
        (afterWord & 0x7F) == contract.AssignedTextureId &&
        (afterWord & 0xFFFFFF80u) == (beforeWord & 0xFFFFFF80u),
        $"Face {contract.RuntimeKey} changed more than its native seven-bit texture ID.");
}

NativeTerrainTextureRelocationEdit[] privateRows = contracts
    .Select(contract => BuildEdit(
        binding,
        gnastysWorld,
        contract.DonorTextureId,
        contract.MaterialTemplateTextureId,
        contract.AssignedTextureId))
    .ToArray();
NativeTerrainTexturePrivateRecordBatchPlan plan =
    NativeTerrainTexturePrivateRecordBatchCompiler.BuildPlan(
        new NativeTerrainTexturePrivateRecordBatchRequest(
            sourceImagePath,
            sourceCuePath,
            outputPrefix,
            wadAnalysisPath,
            gnastysWorld,
            privateRows,
            ordinaryPatches,
            StructuralGrowthPolicy:
                NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenExtended));
Assert(
    plan.WriterKind == NativeTerrainTexturePrivateImageWriterKind.SectorRelocation &&
    plan.FixedTailPlan == null &&
    plan.SectorRelocationPlan != null &&
    !plan.StaticResearchOnly &&
    plan.AppendedPrivateEdits.Count == RequestedPrivateRows &&
    plan.OrdinaryLevelDataPatches.Count == RequestedPrivateRows,
    "The fifty-row compiler did not select the exact runtime-proven extended structural writer.");

NativeTerrainTextureSectorPrivateRecordPlan sectorPlan =
    plan.SectorRelocationPlan!;
Assert(
    sectorPlan.SourceBindingVerified &&
    sectorPlan.GlobalPackingProofComplete &&
    sectorPlan.OriginalRowsInstalledExactly &&
    sectorPlan.SyntheticRowsInstalledExactly &&
    sectorPlan.OrdinaryPatchesIncludedAndRelocated &&
    sectorPlan.TexturePagePatchesIncludedAndRelocated &&
    sectorPlan.StructuralSectorGrowthVerified &&
    sectorPlan.Structural.SectorGrowthBytes == 0x2800 &&
    sectorPlan.Structural.RelocatedExecutableLba ==
        sectorPlan.Structural.OriginalExecutableLba + 5 &&
    sectorPlan.Structural.Append.OutputTextureCount ==
        binding.ExpectedSourceTextureCount + RequestedPrivateRows &&
    sectorPlan.OrdinaryRelocatedPatchProofs.Count == RequestedPrivateRows &&
    !sectorPlan.StaticResearchOnly,
    "The extended sector composer omitted a binding, packing, row, face, page, growth, or runtime-profile proof.");

NativeTerrainTextureStructuralGrowthPolicy resolvedNormalPolicy =
    AppendedPrivateTerrainTexturePromotionProfileRegistry
        .ResolveNormalReleaseStructuralGrowthPolicy(
            binding,
            RequestedPrivateRows);
Assert(
    resolvedNormalPolicy ==
        NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenExtended,
    "The exact Gnasty's World retail binding did not resolve to RuntimeProvenExtended.");

bool runtimeProfileAuthorized =
    AppendedPrivateTerrainTexturePromotionProfileRegistry.TryAuthorizeNormalRelease(
        binding,
        plan.WriterKind,
        RequestedPrivateRows,
        NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenExtended,
        out AppendedPrivateTerrainTexturePromotionProfile? promotionProfile,
        out string runtimeProfileGateReason);
Assert(
    runtimeProfileAuthorized &&
    promotionProfile != null &&
    promotionProfile.Id.Equals(
        "terrain-private-gnastysworld-clean-usa-sector-fifty-v1",
        StringComparison.Ordinal) &&
    promotionProfile.RuntimeProven &&
    promotionProfile.RuntimeProvenMaxAppendedRecords == RequestedPrivateRows &&
    promotionProfile.StructuralGrowthPolicy ==
        NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenExtended &&
    promotionProfile.RuntimeProofOutputSha256.Equals(
        AppendedPrivateTerrainTexturePromotionProfileRegistry
            .GnastysWorldFiftyRecordOutputSha256,
        StringComparison.OrdinalIgnoreCase),
    $"The exact runtime-proven Gnasty's World profile was not authorized: {runtimeProfileGateReason}");

bool defaultNormalAuthorized =
    AppendedPrivateTerrainTexturePromotionProfileRegistry.TryAuthorizeNormalRelease(
        binding,
        plan.WriterKind,
        RequestedPrivateRows,
        NativeTerrainTextureStructuralGrowthPolicy.NormalChecked,
        out AppendedPrivateTerrainTexturePromotionProfile? defaultNormalProfile,
        out string defaultNormalGateReason);
Assert(
    !defaultNormalAuthorized && defaultNormalProfile == null,
    "The Gnasty's World profile accepted the mismatched default-normal structural policy.");

bool staticAuthorized =
    AppendedPrivateTerrainTexturePromotionProfileRegistry.TryAuthorizeNormalRelease(
        binding,
        plan.WriterKind,
        RequestedPrivateRows,
        NativeTerrainTextureStructuralGrowthPolicy.FiftyRowStaticResearch,
        out AppendedPrivateTerrainTexturePromotionProfile? staticProfile,
        out string staticGateReason);
Assert(
    !staticAuthorized && staticProfile == null,
    "The Gnasty's World profile accepted the static-research structural policy for normal Create BIN.");

bool fiftyOneAuthorized =
    AppendedPrivateTerrainTexturePromotionProfileRegistry.TryAuthorizeNormalRelease(
        binding,
        plan.WriterKind,
        RequestedPrivateRows + 1,
        NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenExtended,
        out AppendedPrivateTerrainTexturePromotionProfile? fiftyOneProfile,
        out string fiftyOneGateReason);
Assert(
    !fiftyOneAuthorized &&
    fiftyOneProfile != null &&
    fiftyOneGateReason.Contains("at most 50", StringComparison.OrdinalIgnoreCase),
    "The Gnasty's World runtime profile did not reject appended row 51.");

NativeTerrainTexturePrivateRecordBatchExportResult exported =
    await NativeTerrainTexturePrivateRecordBatchCompiler.ExportAsync(plan);
Assert(
    exported.SourceImagePreserved &&
    exported.ExactReadbackVerified &&
    exported.RuntimeTargetReadbackVerified &&
    exported.GlobalLogicalReadbackVerified &&
    exported.AtomicRenameCompleted &&
    exported.OutputImageSha256.Equals(
        AppendedPrivateTerrainTexturePromotionProfileRegistry
            .GnastysWorldFiftyRecordOutputSha256,
        StringComparison.OrdinalIgnoreCase) &&
    File.Exists(exported.OutputImagePath) &&
    File.Exists(exported.OutputCuePath),
    "The fifty-row final BIN omitted a source, structural, face, logical, atomic-write, or registered runtime-output proof.");

IReadOnlyList<TerrainTextureSlot> slots =
    TerrainPatchExporter.InspectTextureSlots(exported.OutputImagePath, gnastysWorld);
Assert(
    slots.Count == binding.ExpectedSourceTextureCount + RequestedPrivateRows &&
    slots.Skip(binding.ExpectedSourceTextureCount)
        .Select(slot => slot.TextureId)
        .SequenceEqual(Enumerable.Range(
            binding.ExpectedSourceTextureCount,
            RequestedPrivateRows)) &&
    slots.Skip(binding.ExpectedSourceTextureCount)
        .All(slot => slot.HasNormalDescriptors && slot.HasCloseDescriptors),
    "The final BIN did not decode fifty complete contiguous private texture rows.");

LocalDiscLayout outputLayout = DetectDiscLayout(exported.OutputImagePath);
using FileStream outputImage = File.OpenRead(exported.OutputImagePath);
foreach (PrivateRowContract contract in contracts)
{
    NativeTerrainTextureRecordExistingPatch ordinary = ordinaryPatches.Single(item =>
        item.RuntimeKey.Equals(contract.RuntimeKey, StringComparison.OrdinalIgnoreCase));
    NativeTerrainTextureRecordRebasedPatchProof relocated =
        sectorPlan.OrdinaryRelocatedPatchProofs.Single(item =>
            item.RuntimeKey.Equals(contract.RuntimeKey, StringComparison.OrdinalIgnoreCase));
    byte[] finalBytes = ReadDiscFileBytes(
        outputImage,
        outputLayout,
        WadLba,
        relocated.OutputWadOffset,
        sizeof(uint));
    uint finalWord = BinaryPrimitives.ReadUInt32LittleEndian(finalBytes);
    Assert(
        finalWord == BinaryPrimitives.ReadUInt32LittleEndian(ordinary.After) &&
        (finalWord & 0x7F) == contract.AssignedTextureId,
        $"Final face {contract.RuntimeKey} did not retain private T{contract.AssignedTextureId}.");
}

NativeTerrainTextureGlobalRepackPackedRecord[] expectedRows =
    sectorPlan.GlobalPacking.OriginalMovableRecords
        .Concat(sectorPlan.GlobalPacking.SyntheticRecords)
        .OrderBy(row => row.TargetTextureId)
        .ToArray();
bool explicitLogicalReadback =
    NativeTerrainTextureGlobalRepackerResearch.TryVerifyCandidateLogicalReadback(
        exported.OutputImagePath,
        gnastysWorld,
        expectedRows,
        sourceImagePath,
        out string logicalFailure);
Assert(
    explicitLogicalReadback &&
    sectorPlan.GlobalPacking.SyntheticRecords.Count == RequestedPrivateRows,
    $"Immutable-source logical readback failed: {logicalFailure}");
Assert(
    FileLength(sourceImagePath) == sourceLength &&
    File.GetLastWriteTimeUtc(sourceImagePath) == sourceWriteTime &&
    string.Equals(Sha256File(sourceImagePath), sourceSha256, StringComparison.OrdinalIgnoreCase),
    "The fifty-row final-BIN smoke changed its immutable retail source.");

await File.WriteAllTextAsync(
    proofPath,
    JsonSerializer.Serialize(
        new
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Status = "runtime-proven exact clean-USA profile / promoted for normal Beta V4",
            RequestedPrivateRows,
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
                Length = sourceLength,
                Preserved = true,
                binding.ExpectedSourceTextureCount,
                binding.ExpectedTextureComponentSha256,
                binding.ExpectedLevelDataSha256
            },
            Output = new
            {
                Bin = exported.OutputImagePath,
                Cue = exported.OutputCuePath,
                exported.OutputImageSha256,
                FinalTextureCount = slots.Count
            },
            Structure = new
            {
                Policy = plan.StructuralGrowthPolicy.ToString(),
                plan.StaticResearchOnly,
                sectorPlan.Structural.SectorGrowthBytes,
                sectorPlan.Structural.OriginalExecutableLba,
                sectorPlan.Structural.RelocatedExecutableLba,
                sectorPlan.GlobalPacking.PackingProof.ProtectedByteCount,
                sectorPlan.GlobalPacking.PackingProof.AllocatedByteCount,
                sectorPlan.GlobalPacking.PackingProof.FreeByteCount,
                sectorPlan.GlobalPacking.PackingProof.PackingStrategy
            },
            Contracts = contracts,
            Proof = new
            {
                FaceAssignmentCount = ordinaryPatches.Count,
                PrivateRecordCount = privateRows.Length,
                exported.SourceImagePreserved,
                exported.ExactReadbackVerified,
                NativeRuntimeTargetByteReadbackVerified =
                    exported.RuntimeTargetReadbackVerified,
                exported.GlobalLogicalReadbackVerified,
                ExplicitImmutableSourceLogicalReadback =
                    explicitLogicalReadback,
                exported.AtomicRenameCompleted,
                ResolvedNormalStructuralPolicy =
                    resolvedNormalPolicy.ToString(),
                NormalReleaseAuthorized = runtimeProfileAuthorized,
                RuntimeProfileGateReason = runtimeProfileGateReason,
                MismatchedDefaultNormalAuthorized = defaultNormalAuthorized,
                MismatchedDefaultNormalGateReason = defaultNormalGateReason,
                StaticResearchPolicyAuthorizedForNormalRelease =
                    staticAuthorized,
                StaticResearchPolicyGateReason = staticGateReason,
                Row51Authorized = fiftyOneAuthorized,
                Row51GateReason = fiftyOneGateReason,
                RegisteredRuntimeOutputSha256 =
                    promotionProfile!.RuntimeProofOutputSha256,
                DuckStationRuntimeProven = true
            }
        },
        new JsonSerializerOptions { WriteIndented = true }) +
    Environment.NewLine);

Console.WriteLine("Fifty private terrain-texture final-BIN smoke passed.");
Console.WriteLine($"- Final BIN: {exported.OutputImagePath}");
Console.WriteLine($"- Final CUE: {exported.OutputCuePath}");
Console.WriteLine($"- Runtime promotion proof: {proofPath}");
Console.WriteLine("- 50 exact private rows + 50 exact face IDs + immutable-donor logical readback passed.");
Console.WriteLine("- Exact clean-USA Gnasty's World RuntimeProvenExtended promotion is authorized at 50 rows.");
Console.WriteLine("- Default-normal, static-research, and row-51 mismatches remain rejected.");

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
        "2026-07-26T20:00:00Z",
        NativeTerrainTextureRelocationApplyMode.ArtOnlyPreserveTarget,
        NativeTerrainTextureTargetRecordKind.AppendedPrivate,
        binding.TargetWadEntry,
        binding.SourceImageSha256,
        binding.ExpectedSourceTextureCount,
        binding.ExpectedTextureComponentSha256,
        binding.ExpectedLevelDataSha256,
        materialTemplateTextureId,
        NativeTerrainTextureRelocationEditStore.BuildAppendedPrivateRecordId(
            targetTextureId));

static LocalDiscLayout DetectDiscLayout(string path)
{
    using FileStream stream = File.OpenRead(path);
    foreach ((int sectorSize, int userOffset) in new[]
             {
                 (2048, 0),
                 (2352, 24),
                 (2336, 8)
             })
    {
        long offset = (16L * sectorSize) + userOffset;
        if (stream.Length < offset + 2048)
            continue;
        byte[] buffer = new byte[2048];
        stream.Position = offset;
        if (stream.Read(buffer, 0, buffer.Length) != buffer.Length)
            continue;
        if (buffer[0] == 1 &&
            Encoding.ASCII.GetString(buffer, 1, 5) == "CD001")
        {
            return new LocalDiscLayout(sectorSize, userOffset);
        }
    }
    throw new InvalidOperationException("Could not detect the final BIN sector layout.");
}

static byte[] ReadDiscFileBytes(
    FileStream stream,
    LocalDiscLayout layout,
    int fileLba,
    long fileOffset,
    int length)
{
    byte[] result = new byte[length];
    int remaining = length;
    int written = 0;
    long absolute = fileOffset;
    while (remaining > 0)
    {
        int sectorOffset = (int)(absolute % 2048);
        int sector = checked(fileLba + (int)(absolute / 2048));
        int toRead = Math.Min(2048 - sectorOffset, remaining);
        stream.Position =
            ((long)sector * layout.SectorSize) +
            layout.UserOffset +
            sectorOffset;
        if (stream.Read(result, written, toRead) != toRead)
            throw new EndOfStreamException("Could not read the requested final-BIN bytes.");
        written += toRead;
        remaining -= toRead;
        absolute += toRead;
    }
    return result;
}

static string ResolveWorkspaceRoot(string? candidate)
{
    string current = Path.GetFullPath(candidate ?? Directory.GetCurrentDirectory());
    for (DirectoryInfo? directory = new(current);
         directory != null;
         directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "Spyro the Dragon (USA).bin")) &&
            Directory.Exists(Path.Combine(directory.FullName, "src", "Spyro.Editor.Core")))
        {
            return directory.FullName;
        }
    }
    return current;
}

static string Sha256File(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream));
}

static long FileLength(string path)
{
    using FileStream stream = File.OpenRead(path);
    return stream.Length;
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record PrivateRowContract(
    string RuntimeKey,
    int MaterialTemplateTextureId,
    int DonorTextureId,
    int AssignedTextureId);

internal sealed record LocalDiscLayout(
    int SectorSize,
    int UserOffset);
