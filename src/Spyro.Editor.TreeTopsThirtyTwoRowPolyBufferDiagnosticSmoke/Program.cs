using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

const string ExpectedSourceSha256 =
    "FC866B2A02E010A6658F8AF2DE28BB3001EB33513E5924AF014E35643C6DEE37";
const long ExpectedSourceLength = 661_547_040;
const int WadLba = 37;
const int ExpectedLevelId = 43;
const int ExpectedWadEntry = 52;
const int SourceTextureCount = 81;
const int SourceTextureId = 12;
const int AppendedRowCount = 32;
const int OutputTextureCount = 113;
const int FirstAppendedTextureId = 81;
const int ExpectedTextureComponentBytes = 0x3A40;
const int ExpectedOutputTextureComponentBytes = 0x5140;
const int ExpectedLevelDataBytes = 0xA8000;
const int ExpectedOutputLevelDataBytes = 0xA9800;
const int ExpectedSourceUsedBytes = 0xA7CAC;
const int ExpectedOutputUsedBytes = 0xA93AC;
const int ExpectedSourceZeroTailBytes = 0x354;
const int ExpectedOutputZeroTailBytes = 0x454;
const int ExpectedTableGrowthBytes = 0x1700;
const int ExpectedSectorGrowthBytes = 0x1800;
const int ExpectedAdditionalBytesNeeded = 0x13AC;
const int ExpectedOriginalWadSize = 0x6927000;
const int ExpectedExpandedWadSize = 0x6928800;
const int ExpectedOriginalExecutableLba = 53875;
const int ExpectedRelocatedExecutableLba = 53878;
const int ExpectedExecutableSize = 0x66000;
const long ExpectedTargetEntryWadOffset = 0x3D88000;
const int ExpectedTargetEntryBytes = 0x29C800;
const int ExpectedExpandedTargetEntryBytes = 0x29E000;
const long ExpectedLevelDataWadOffset = 0x3E62800;
const uint RetailSceneBase = 0x8016E4F4;
const int RetailSceneSize = 0x18000;
const uint ExpectedSceneEnd =
    RetailSceneBase + RetailSceneSize + ExpectedTableGrowthBytes;
const uint OriginalLowerPolygonBuffer = 0x80187BB0;
const uint NewLowerPolygonBuffer = 0x80187C30;
const uint ExpectedPolygonMargin =
    NewLowerPolygonBuffer - ExpectedSceneEnd;
const string ExpectedTextureComponentSha256 =
    "9E0E9BD9AFE845C882B6EF7CDE41F421898C7E9EA2F041CEACF7371F1971858A";
const string ExpectedLevelDataSha256 =
    "BCA21FDF3FDEB0D571DA93267EFCE9DECB209F227BD7F1A4A264E2CE5132046C";

ExecutablePatch[] polygonBufferPatches =
[
    new(
        0x4BF3C,
        0x8005B73C,
        "00 40 63 34",
        "40 40 63 34",
        "normal arena lower-span constant -0x1C000 -> -0x1BFC0"),
    new(
        0x0AE08,
        0x8001A608,
        "00 C0 84 34",
        "C0 BF 84 34",
        "normal polygon span constant 0x1C000 -> 0x1BFC0"),
    new(
        0x0CEAC,
        0x8001C6AC,
        "00 C0 63 34",
        "C0 BF 63 34",
        "normal polygon span constant 0x1C000 -> 0x1BFC0"),
    new(
        0x0F590,
        0x8001ED90,
        "00 C0 A5 34",
        "C0 BF A5 34",
        "normal polygon span constant 0x1C000 -> 0x1BFC0")
];

FaceAssignment[] assignments =
[
    new("1:32:hp", 81, 0x3E674EC, 0xDE6E090C, 0x3E68BEC, 0xDE6E0951),
    new("1:33:hp", 82, 0x3E674FC, 0xDE6FFF0C, 0x3E68BFC, 0xDE6FFF52),
    new("2:0:hp", 83, 0x3E67608, 0xD63D490C, 0x3E68D08, 0xD63D4953),
    new("2:1:hp", 84, 0x3E67618, 0xD4DD490C, 0x3E68D18, 0xD4DD4954),
    new("2:2:hp", 85, 0x3E67628, 0xD4AFFE0C, 0x3E68D28, 0xD4AFFE55),
    new("3:0:hp", 86, 0x3E67884, 0x0030D80C, 0x3E68F84, 0x0030D856),
    new("3:1:hp", 87, 0x3E67894, 0x0E70C40C, 0x3E68F94, 0x0E70C457),
    new("3:2:hp", 88, 0x3E678A4, 0xF390030C, 0x3E68FA4, 0xF3900358),
    new("3:3:hp", 89, 0x3E678B4, 0x0050030C, 0x3E68FB4, 0x00500359),
    new("3:4:hp", 90, 0x3E678C4, 0xF1CFFF0C, 0x3E68FC4, 0xF1CFFF5A),
    new("3:5:hp", 91, 0x3E678D4, 0xF19F2E0C, 0x3E68FD4, 0xF19F2E5B),
    new("3:6:hp", 92, 0x3E678E4, 0x0010D50C, 0x3E68FE4, 0x0010D55C),
    new("3:7:hp", 93, 0x3E678F4, 0xFFF0010C, 0x3E68FF4, 0xFFF0015D),
    new("3:8:hp", 94, 0x3E67904, 0xFFBF180C, 0x3E69004, 0xFFBF185E),
    new("4:20:hp", 95, 0x3E68648, 0xF4BF8A0C, 0x3E69D48, 0xF4BF8A5F),
    new("4:21:hp", 96, 0x3E68658, 0x003FFF0C, 0x3E69D58, 0x003FFF60),
    new("4:22:hp", 97, 0x3E68668, 0x003FFF0C, 0x3E69D68, 0x003FFF61),
    new("4:23:hp", 98, 0x3E68678, 0x003FFF0C, 0x3E69D78, 0x003FFF62),
    new("4:24:hp", 99, 0x3E68688, 0x003F480C, 0x3E69D88, 0x003F4863),
    new("4:25:hp", 100, 0x3E68698, 0x003FFF0C, 0x3E69D98, 0x003FFF64),
    new("4:26:hp", 101, 0x3E686A8, 0x003FFF0C, 0x3E69DA8, 0x003FFF65),
    new("4:40:hp", 102, 0x3E68788, 0x00101F0C, 0x3E69E88, 0x00101F66),
    new("4:41:hp", 103, 0x3E68798, 0x02101D0C, 0x3E69E98, 0x02101D67),
    new("4:54:hp", 104, 0x3E68868, 0x0042890C, 0x3E69F68, 0x00428968),
    new("4:55:hp", 105, 0x3E68878, 0x021FFF0C, 0x3E69F78, 0x021FFF69),
    new("4:62:hp", 106, 0x3E688E8, 0x0020010C, 0x3E69FE8, 0x0020016A),
    new("4:63:hp", 107, 0x3E688F8, 0x0020000C, 0x3E69FF8, 0x0020006B),
    new("4:64:hp", 108, 0x3E68908, 0x0020010C, 0x3E6A008, 0x0020016C),
    new("4:65:hp", 109, 0x3E68918, 0x0022720C, 0x3E6A018, 0x0022726D),
    new("4:66:hp", 110, 0x3E68928, 0x0020010C, 0x3E6A028, 0x0020016E),
    new("4:67:hp", 111, 0x3E68938, 0x0022750C, 0x3E6A038, 0x0022756F),
    new("4:68:hp", 112, 0x3E68948, 0xFF30010C, 0x3E6A048, 0xFF300170)
];

string? rootArgument = args.FirstOrDefault(argument =>
    !argument.StartsWith("--", StringComparison.Ordinal));
string workspaceRoot = ResolveWorkspaceRoot(rootArgument);
string sourceImagePath = Path.Combine(
    workspaceRoot,
    "Spyro the Dragon (USA).bin");
string sourceCuePath = Path.Combine(
    workspaceRoot,
    "Spyro the Dragon (USA).cue");
string outputRoot = Path.Combine(
    workspaceRoot,
    "_local",
    "research",
    "tree-tops-private-texture-load-freeze-diagnostics");
string overlayPath = Path.Combine(
    workspaceRoot,
    "editor-cache",
    "treetops-runtime-scene-editor-overlay.json");
string wadAnalysisPath = Path.Combine(
    outputRoot,
    "source-bound-wad-analysis.json");
string sourceSearchPath = Path.Combine(
    outputRoot,
    "treetops-count113-source-search.json");
string intermediatePrefix = Path.Combine(
    outputRoot,
    "treetops-count113-structural-intermediate-delete-after-verification");
string finalPrefix = Path.Combine(
    outputRoot,
    "treetops-COUNT113-THIRTY-TWO-ROWS-T81-T112-CLONE-T12-THIRTY-TWO-VISIBLE-FACES-POLY-1BFC0-SECTOR-RELOCATION-DIAGNOSTIC-FAST-ENTRY");
string editsPath = finalPrefix + ".terrain-edits.json";
string proofPath = Path.Combine(
    outputRoot,
    "tree-tops-count113-thirty-two-row-poly-buffer-diagnostic-proof.json");
string checklistPath = Path.Combine(
    outputRoot,
    "tree-tops-count113-thirty-two-row-runtime-checklist.md");

Directory.CreateDirectory(outputRoot);
DeleteOutputs(intermediatePrefix, finalPrefix);
foreach (string stale in new[]
         {
             editsPath,
             proofPath,
             checklistPath
         })
{
    if (File.Exists(stale))
        File.Delete(stale);
}

Assert(
    File.Exists(sourceImagePath) &&
    File.Exists(sourceCuePath) &&
    File.Exists(overlayPath),
    "The exact retail BIN/CUE or Tree Tops overlay is missing.");
await WadAnalysisBuilder.EnsureCompatibleAsync(
    sourceImagePath,
    wadAnalysisPath);

string sourcePhysicalPath =
    ResolvePhysicalPath(sourceImagePath);
string sourceSha256 = Sha256File(sourceImagePath);
long sourceLength = FileLength(sourceImagePath);
DateTime sourceWriteTime =
    File.GetLastWriteTimeUtc(sourcePhysicalPath);
Assert(
    sourceLength == ExpectedSourceLength &&
    sourceSha256.Equals(
        ExpectedSourceSha256,
        StringComparison.OrdinalIgnoreCase),
    "This count113 diagnostic is guarded to the exact clean USA retail BIN.");

LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition level = catalog.FindByKey("treetops")
    ?? throw new InvalidOperationException(
        "Tree Tops is missing from the level catalog.");
Assert(
    level.LevelId == ExpectedLevelId &&
    level.SourceWadEntry == ExpectedWadEntry,
    "Tree Tops level identity changed.");

NativeTerrainTextureRecordAppendSourceBinding binding =
    NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(
        sourceImagePath,
        level);
Assert(
    binding.Version ==
        NativeTerrainTextureRecordAppendBuilder.CurrentBindingVersion &&
    binding.ExpectedSourceTextureCount == SourceTextureCount &&
    binding.ExpectedTextureComponentSha256.Equals(
        ExpectedTextureComponentSha256,
        StringComparison.OrdinalIgnoreCase) &&
    binding.ExpectedLevelDataSha256.Equals(
        ExpectedLevelDataSha256,
        StringComparison.OrdinalIgnoreCase),
    "Tree Tops count113 source binding changed.");

NativeTerrainTextureRecordAppendCapacity capacity =
    NativeTerrainTextureRecordAppendBuilder.InspectCapacityForRecordCount(
        sourceImagePath,
        level,
        AppendedRowCount,
        NativeTerrainTextureStructuralGrowthPolicy.FiftyRowStaticResearch,
        wadAnalysisPath);
Assert(
    capacity.SourceTextureCount == SourceTextureCount &&
    capacity.RequestedRecordCount == AppendedRowCount &&
    capacity.RequestedRecordGrowthBytes == ExpectedTableGrowthBytes &&
    capacity.VerifiedLevelDataZeroTailBytes == ExpectedSourceZeroTailBytes &&
    capacity.AdditionalBytesNeededForRequestedRecords ==
        ExpectedAdditionalBytesNeeded &&
    !capacity.CanAppendInsideCurrentSubfile &&
    capacity.WouldRequireTargetEntryGrowth &&
    capacity.RequiredSectorAlignedEntryGrowthBytes ==
        ExpectedSectorGrowthBytes &&
    capacity.SectorRelocationPlanVerified &&
    capacity.ExecutableWriter.Equals(
        "SectorRelocation",
        StringComparison.Ordinal) &&
    capacity.StaticResearchOnly &&
    capacity.LevelDataByteLength == ExpectedLevelDataBytes,
    "Tree Tops no longer exposes the exact count113 +0x1800 research boundary.");

NativeTerrainTextureRuntimeControlAudit sourceRuntime =
    NativeTerrainTextureRuntimeControlScanner.Inspect(
        sourceImagePath,
        level);
Assert(
    sourceRuntime.Complete &&
    sourceRuntime.TextureCount == SourceTextureCount &&
    sourceRuntime.IsRuntimePersistentTarget(SourceTextureId) &&
    !sourceRuntime.ControlledTextureIds.Contains(SourceTextureId) &&
    !sourceRuntime.AnimationSourceTextureIds.Contains(SourceTextureId),
    "Native Tree Tops T12 is no longer a static donor.");
Assert(
    assignments.Length == AppendedRowCount &&
    assignments.Select(item => item.RuntimeKey)
        .Distinct(StringComparer.OrdinalIgnoreCase).Count() ==
        AppendedRowCount &&
    assignments.Select(item => item.TargetTextureId)
        .SequenceEqual(
            Enumerable.Range(
                FirstAppendedTextureId,
                AppendedRowCount)),
    "The count113 matrix is not 32 unique native T12 faces mapped one-to-one to T81-T112.");

GeometryCandidate sourceGeometry =
    GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
foreach (FaceAssignment assignment in assignments)
{
    TerrainPolygon face =
        sourceGeometry.Polygons.Single(item =>
            item.RuntimeKey.Equals(
                assignment.RuntimeKey,
                StringComparison.OrdinalIgnoreCase));
    uint retailWord =
        BinaryPrimitives.ReadUInt32LittleEndian(
            ReadWadBytes(
                sourceImagePath,
                assignment.SourceFaceWordWadOffset,
                sizeof(uint)));
    Assert(
        face.OriginalTextureId == SourceTextureId &&
        face.HasCompleteNativeHighPolyFacePayload &&
        face.FaceOffset + 8 ==
            assignment.SourceFaceWordWadOffset &&
        retailWord == assignment.SourceFaceWord &&
        assignment.OutputFaceWordWadOffset ==
            assignment.SourceFaceWordWadOffset +
                ExpectedTableGrowthBytes &&
        assignment.OutputFaceWord ==
            ReplaceTextureId(
                assignment.SourceFaceWord,
                assignment.TargetTextureId),
        $"Tree Tops face {assignment.RuntimeKey} no longer matches its exact native T12 preimage.");
}

TerrainSourceSearchResult sourceSearch =
    await TerrainSourceSearchBuilder.BuildSourceDerivedAsync(
        new SourceDerivedTerrainSourceSearchRequest(
            sourceImagePath,
            sourceSearchPath,
            level,
            sourceGeometry));
foreach (FaceAssignment assignment in assignments)
{
    TerrainSourceSearchEntry exact =
        sourceSearch.Report.Results.Single(item =>
            item.Edit.Equals(
                assignment.RuntimeKey,
                StringComparison.OrdinalIgnoreCase));
    Assert(
        exact.FullSectorHits.Count == 1,
        $"Tree Tops face {assignment.RuntimeKey} no longer has exactly one source-sector hit.");
}

GeometryCandidate editedGeometry =
    GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
TerrainPolygon[] editedFaces = assignments
    .Select(assignment =>
    {
        TerrainPolygon face =
            editedGeometry.Polygons.Single(item =>
                item.RuntimeKey.Equals(
                    assignment.RuntimeKey,
                    StringComparison.OrdinalIgnoreCase));
        face.ApplyTextureOverride(
            assignment.TargetTextureId);
        return face;
    })
    .ToArray();
int saved = await TerrainEditStore.SaveAsync(
    editsPath,
    editedFaces,
    "Tree Tops count113/T81-T112 thirty-two-row polygon-buffer diagnostic");
Assert(
    saved == AppendedRowCount,
    $"Saved {saved} face edit(s), expected {AppendedRowCount}.");

TerrainPatchPlan ordinaryPlan =
    TerrainPatchExporter.BuildPlan(
        sourceImagePath,
        sourceCuePath,
        finalPrefix + "-not-written.bin",
        finalPrefix + "-not-written.cue",
        level,
        ramPath: "",
        sourceSearchPath,
        editsPath);
TerrainPatch[] ordinaryTexturePatches =
    ordinaryPlan.Patches
        .Where(patch => patch.Kind.Equals(
            "texture-id-word3",
            StringComparison.OrdinalIgnoreCase))
        .ToArray();
Assert(
    ordinaryPlan.Patches.Count == AppendedRowCount &&
    ordinaryTexturePatches.Length == AppendedRowCount &&
    ordinaryPlan.SkippedEdits.All(item =>
        item.Equals(
            "collision: source-derived exact triangle scan found no matching source collision records for the edited terrain face(s).",
            StringComparison.Ordinal)),
    "The ordinary Tree Tops plan was not exactly 32 source-bound texture-word patches.");

IReadOnlyList<NativeTerrainTextureRecordExistingPatch>
    visibleFacePatches =
        NativeTerrainTexturePrivateRecordBatchCompiler
            .ConvertOrdinaryTerrainPlan(ordinaryPlan);
foreach (FaceAssignment assignment in assignments)
{
    NativeTerrainTextureRecordExistingPatch patch =
        visibleFacePatches.Single(item =>
            item.RuntimeKey.Equals(
                assignment.RuntimeKey,
                StringComparison.OrdinalIgnoreCase));
    Assert(
        patch.WadOffset ==
            assignment.SourceFaceWordWadOffset &&
        patch.Before.SequenceEqual(
            UInt32Bytes(assignment.SourceFaceWord)) &&
        patch.After.SequenceEqual(
            UInt32Bytes(assignment.OutputFaceWord)) &&
        CountByteDifferences(
            patch.Before,
            patch.After) == 1,
        $"Tree Tops {assignment.RuntimeKey} is not the exact one-byte T12-to-T{assignment.TargetTextureId} patch.");
}

bool donorFixtureBuilt =
    NativeTerrainTextureRecordAppendResearch.TryBuild(
        new NativeTerrainTextureRecordAppendResearchRequest(
            sourceImagePath,
            sourceCuePath,
            level,
            level,
            SourceTextureId),
        out NativeTerrainTextureRecordAppendResearchPlan?
            donorFixtureCandidate,
        out string donorFixtureFailure);
Assert(
    donorFixtureBuilt &&
    donorFixtureCandidate != null,
    $"Independent native-T12 donor fixture failed: {donorFixtureFailure}");
NativeTerrainTextureRecordAppendResearchPlan donorFixture =
    donorFixtureCandidate!;
byte[] sourceT12Low = SliceLowRow(
    donorFixture.SourceLevelData,
    SourceTextureCount,
    SourceTextureId);
byte[] sourceT12High = SliceHighRow(
    donorFixture.SourceLevelData,
    SourceTextureCount,
    SourceTextureId);
Assert(
    sourceT12Low.Length ==
        NativeTerrainTextureRecordAppendBuilder.LowDetailRecordBytes &&
    sourceT12High.Length ==
        NativeTerrainTextureRecordAppendBuilder.HighDetailRecordBytes,
    "The T12 fixture did not expose one complete 184-byte native row.");

NativeTerrainTexturePackedAppendRecord[] packedRows =
    assignments.Select(assignment =>
        new NativeTerrainTexturePackedAppendRecord(
            StableEditId:
                $"research-only:treetops:count113:{assignment.RuntimeKey}:T{assignment.TargetTextureId}:clone-native-T12",
            DonorLevelKey: level.Key,
            DonorWadEntry: level.SourceWadEntry,
            DonorTextureId: SourceTextureId,
            MaterialTemplateTextureId: SourceTextureId,
            LowDetailRow: sourceT12Low.ToArray(),
            HighDetailRow: sourceT12High.ToArray(),
            ExpectedLowDetailSha256:
                Sha256Bytes(sourceT12Low),
            ExpectedHighDetailSha256:
                Sha256Bytes(sourceT12High)))
        .ToArray();

NativeTerrainTextureRecordSectorRelocationRequest request =
    new(
        sourceImagePath,
        sourceCuePath,
        intermediatePrefix,
        wadAnalysisPath,
        level,
        binding,
        packedRows,
        visibleFacePatches,
        Array.Empty<
            NativeTerrainTextureRecordRelocationExternalPatch>(),
        StructuralGrowthPolicy:
            NativeTerrainTextureStructuralGrowthPolicy
                .FiftyRowStaticResearch);
NativeTerrainTextureRecordSectorRelocationPlan plan =
    NativeTerrainTextureRecordSectorRelocationComposer
        .BuildPlan(request);
ValidateStructuralPlan(plan);

NativeTerrainTextureStructuralGrowthPolicy
    resolvedNormalReleasePolicy =
        AppendedPrivateTerrainTexturePromotionProfileRegistry
            .ResolveNormalReleaseStructuralGrowthPolicy(
                binding,
                AppendedRowCount);
bool normalCreateBinAuthorized =
    AppendedPrivateTerrainTexturePromotionProfileRegistry
        .TryAuthorizeNormalRelease(
            binding,
            NativeTerrainTexturePrivateImageWriterKind
                .SectorRelocation,
            AppendedRowCount,
            NativeTerrainTextureStructuralGrowthPolicy
                .FiftyRowStaticResearch,
            out AppendedPrivateTerrainTexturePromotionProfile?
                normalCreateBinProfile,
            out string normalCreateBinGateReason);
Assert(
    resolvedNormalReleasePolicy ==
        NativeTerrainTextureStructuralGrowthPolicy
            .NormalChecked &&
    !normalCreateBinAuthorized &&
    normalCreateBinProfile == null &&
    normalCreateBinGateReason.Contains(
        "No exact appended-private promotion profile",
        StringComparison.OrdinalIgnoreCase),
    "The count113 research policy escaped into normal Create BIN authorization.");

NativeTerrainTextureRecordSectorRelocationExportResult
    structuralExport =
        await NativeTerrainTextureRecordSectorRelocationComposer
            .ExportAsync(request);
Assert(
    File.Exists(structuralExport.OutputImagePath) &&
    File.Exists(structuralExport.OutputCuePath) &&
    structuralExport.SourceImagePreserved &&
    structuralExport.RelocatedLevelDataReadbackVerified &&
    structuralExport.RelocatedExternalPatchReadbackVerified &&
    structuralExport.RuntimeTargetReadbackVerified &&
    structuralExport.AtomicRenameCompleted,
    "The count113 structural export omitted an atomic final-BIN/readback proof.");
ArtifactReadback structuralReadback =
    VerifyStructuralArtifact(
        structuralExport.OutputImagePath,
        plan);

CombinedFastProof final =
    await BuildCombinedFastEntryCloneAsync(
        structuralExport.OutputImagePath,
        structuralExport.OutputCuePath,
        finalPrefix,
        plan,
        polygonBufferPatches);
AssertRetailSourcePreserved();

var proof = new
{
    GeneratedAtUtc = DateTimeOffset.UtcNow,
    Status =
        "PASSED OFFLINE; COUNT113 THIRTY-TWO-ROW VISIBLE POLY-1BFC0 FAST-ENTRY RUNTIME RESULT PENDING",
    ResearchOnly = true,
    StaticResearchOnly = true,
    NormalCreateBinIntegrated = false,
    NormalCreateBinAuthorized = false,
    DuckStationRuntimeProven = false,
    RuntimePrerequisite = new
    {
        Count112PolygonBufferControlRecorded = true,
        RecordedAt = "2026-07-30",
        Artifact =
            "treetops-COUNT112-POLY-BUFFER-1BFC0-CONTROL-FAST-ENTRY.cue",
        Sha256 =
            "8BF6E5879F94246F657975892E8D0057686D982233CF228B018A4BFA83C932B6",
        Result = "Tree Tops loaded successfully.",
        EvidenceScope =
            "Successful level load only. No movement, actor, music-continuity, interaction, stress, death/reload, persistence, or flight-stage claim."
    },
    Source = new
    {
        Path = sourceImagePath,
        PhysicalPath = sourcePhysicalPath,
        Sha256 = sourceSha256,
        Length = sourceLength,
        Preserved = true,
        binding.Version,
        binding.TargetLevelKey,
        binding.TargetWadEntry,
        binding.ExpectedSourceTextureCount,
        binding.ExpectedTextureComponentSha256,
        binding.ExpectedLevelDataSha256
    },
    Capacity = new
    {
        SourceTextureCount,
        OutputTextureCount,
        AppendedRowCount,
        TableGrowthBytes = $"0x{ExpectedTableGrowthBytes:X}",
        TextureComponentBytes =
            $"0x{ExpectedTextureComponentBytes:X}->0x{ExpectedOutputTextureComponentBytes:X}",
        UsedBytes =
            $"0x{ExpectedSourceUsedBytes:X}->0x{ExpectedOutputUsedBytes:X}",
        LevelDataBytes =
            $"0x{ExpectedLevelDataBytes:X}->0x{ExpectedOutputLevelDataBytes:X}",
        ZeroTailBytes =
            $"0x{ExpectedSourceZeroTailBytes:X}->0x{ExpectedOutputZeroTailBytes:X}",
        AdditionalBytesNeeded =
            $"0x{ExpectedAdditionalBytesNeeded:X}",
        SectorGrowthBytes =
            $"0x{ExpectedSectorGrowthBytes:X}",
        SceneEnd = $"0x{ExpectedSceneEnd:X8}",
        OriginalLowerPolygonBuffer =
            $"0x{OriginalLowerPolygonBuffer:X8}",
        NewLowerPolygonBuffer =
            $"0x{NewLowerPolygonBuffer:X8}",
        NewMarginBytes =
            $"0x{ExpectedPolygonMargin:X}",
        Policy =
            capacity.StructuralGrowthPolicy.ToString(),
        NormalCreateBinGateReason =
            normalCreateBinGateReason
    },
    Rows = assignments.Select(item =>
        new
        {
            item.TargetTextureId,
            CloneOf = SourceTextureId,
            CompleteBytes =
                NativeTerrainTextureRecordAppendBuilder
                    .RecordGrowthBytes,
            LowSha256 =
                Sha256Bytes(sourceT12Low),
            HighSha256 =
                Sha256Bytes(sourceT12High),
            ByteIdenticalToNativeT12 = true
        }),
    Faces = assignments.Select(item =>
        new
        {
            item.RuntimeKey,
            SourceTextureId,
            item.TargetTextureId,
            SourceWadOffset =
                $"0x{item.SourceFaceWordWadOffset:X}",
            OutputWadOffset =
                $"0x{item.OutputFaceWordWadOffset:X}",
            SourceWord =
                $"0x{item.SourceFaceWord:X8}",
            OutputWord =
                $"0x{item.OutputFaceWord:X8}",
            ChangedBytes = 1
        }),
    PolygonBufferPatches =
        polygonBufferPatches.Select(item =>
            new
            {
                FileOffset =
                    $"0x{item.FileOffset:X}",
                RuntimePc =
                    $"0x{item.RuntimePc:X8}",
                Before = HexText(item.Before),
                After = HexText(item.After),
                item.Purpose
            }),
    Output = new
    {
        final.ImagePath,
        final.CuePath,
        final.Sha256,
        final.StructuralToFinalByteDifferences,
        ExpectedPolygonPatchChangedBytes = 7,
        ExpectedFastEntryChangedBytes = 8,
        structuralReadback.SourceTexturePagesSha256,
        structuralReadback.OutputTexturePagesSha256,
        TexturePagesUnchanged = true,
        ExactStructuralReadback = true,
        ExactExecutablePatchReadback = true,
        ExactCueTarget = true
    },
    RuntimeBoundary =
        "This visible count113 FAST-ENTRY diagnostic is the first 32-row Tree Tops candidate. It does not prove movement/stress, arbitrary payloads, 50 slots, persistence/Build Safety, flight-stage safety, or normal Create BIN."
};
await File.WriteAllTextAsync(
    proofPath,
    JsonSerializer.Serialize(
        proof,
        new JsonSerializerOptions
        {
            WriteIndented = true
        }) +
    Environment.NewLine);

string checklist = $"""
    # Tree Tops count113 thirty-two-row polygon-buffer runtime checklist

    Research only. This diagnostic installs exactly 32 complete byte-identical native T12 rows as T81-T112 and maps 32 distinct native T12 faces one-to-one. It uses the proven normal-play `0x1BFC0` polygon-buffer control plus the existing eight-byte FAST-ENTRY patch. Normal Create BIN remains fail-closed.

    ## Recorded prerequisite

    - `treetops-COUNT112-POLY-BUFFER-1BFC0-CONTROL-FAST-ENTRY.cue`
    - SHA-256: `8BF6E5879F94246F657975892E8D0057686D982233CF228B018A4BFA83C932B6`
    - User report on 2026-07-30: Tree Tops loaded successfully.
    - Evidence is strictly successful level load only; no movement or stress claim.

    ## Candidate

    - `{Path.GetFileName(final.CuePath)}`
    - SHA-256: `{final.Sha256}`

    ## FAST-ENTRY sequence

    1. Fully stop the running game and cold-boot the candidate CUE.
    2. Open Inventory with **Select**.
    3. Enter **R1, R2, L1, L2, R1, L1, R2, L2**.
    4. Press **Right**, then **Triangle** to enter Tree Tops.
    5. First report whether the level loads. If it freezes, note screen blinking and whether music began, then stop.
    6. Only after a successful load, move Spyro and observe nearby actors/music separately so the evidence scopes remain distinct.

    ## Exact offline construction

    - Texture count: 81 -> 113.
    - Rows: T81-T112, all complete 0xB8-byte native T12 clones.
    - Faces: 32 distinct exact native HP T12 faces, including new face `4:68:hp` -> T112.
    - Table growth: +0x{ExpectedTableGrowthBytes:X}.
    - Texture component: 0x{ExpectedTextureComponentBytes:X} -> 0x{ExpectedOutputTextureComponentBytes:X}.
    - Used bytes: 0x{ExpectedSourceUsedBytes:X} -> 0x{ExpectedOutputUsedBytes:X}.
    - Level data: 0x{ExpectedLevelDataBytes:X} -> 0x{ExpectedOutputLevelDataBytes:X}.
    - Tail: 0x{ExpectedOutputZeroTailBytes:X}.
    - WAD/entry structural growth: +0x{ExpectedSectorGrowthBytes:X}; executable LBA {ExpectedOriginalExecutableLba} -> {ExpectedRelocatedExecutableLba}.
    - Scene end: 0x{ExpectedSceneEnd:X8}; new lower arena: 0x{NewLowerPolygonBuffer:X8}; margin: 0x{ExpectedPolygonMargin:X}.
    - External texture-page/global-repack patches: zero.
    - Normal Create BIN authorization: false.
    - Avoid flight stages and flight-result screens.
    """;
await File.WriteAllTextAsync(
    checklistPath,
    checklist + Environment.NewLine);

DeleteOutputs(intermediatePrefix);
Assert(
    !File.Exists(intermediatePrefix + ".bin") &&
    !File.Exists(intermediatePrefix + ".cue") &&
    !File.Exists(intermediatePrefix + ".terrain-patch-plan.json"),
    "A transient count113 structural artifact remained after final verification.");
AssertRetailSourcePreserved();

Console.WriteLine(
    "Tree Tops count113 thirty-two-row polygon-buffer diagnostic: PASSED OFFLINE");
Console.WriteLine(
    $"- CUE: {final.CuePath}");
Console.WriteLine(
    $"- BIN SHA-256: {final.Sha256}");
Console.WriteLine(
    $"- Proof: {proofPath}");
Console.WriteLine(
    $"- Checklist: {checklistPath}");
Console.WriteLine(
    "- Research only; runtime result pending. Avoid all flight stages and flight-result screens.");

void AssertRetailSourcePreserved()
{
    Assert(
        FileLength(sourceImagePath) == sourceLength &&
        File.GetLastWriteTimeUtc(sourcePhysicalPath) ==
            sourceWriteTime &&
        Sha256File(sourceImagePath).Equals(
            sourceSha256,
            StringComparison.OrdinalIgnoreCase),
        "The count113 diagnostic changed the immutable retail source.");
}

void ValidateStructuralPlan(
    NativeTerrainTextureRecordSectorRelocationPlan
        structuralPlan)
{
    NativeTerrainTextureRecordAppendPlan append =
        structuralPlan.Append;
    Assert(
        structuralPlan.SourceImageSha256.Equals(
            sourceSha256,
            StringComparison.OrdinalIgnoreCase) &&
        structuralPlan.TargetLevelKey.Equals(
            LevelCatalog.NormalizeKey(level.Key),
            StringComparison.OrdinalIgnoreCase) &&
        structuralPlan.TargetWadEntry ==
            ExpectedWadEntry &&
        structuralPlan.SectorGrowthBytes ==
            ExpectedSectorGrowthBytes &&
        structuralPlan.OriginalWadSize ==
            ExpectedOriginalWadSize &&
        structuralPlan.ExpandedWadSize ==
            ExpectedExpandedWadSize &&
        structuralPlan.OriginalExecutableLba ==
            ExpectedOriginalExecutableLba &&
        structuralPlan.RelocatedExecutableLba ==
            ExpectedRelocatedExecutableLba &&
        structuralPlan.OriginalTargetEntryWadOffset ==
            ExpectedTargetEntryWadOffset &&
        structuralPlan.RelocatedTargetEntryWadOffset ==
            ExpectedTargetEntryWadOffset &&
        structuralPlan.OriginalLevelDataWadOffset ==
            ExpectedLevelDataWadOffset &&
        structuralPlan.RelocatedLevelDataWadOffset ==
            ExpectedLevelDataWadOffset &&
        structuralPlan.AppendSourceBindingVerified &&
        structuralPlan.ExistingLevelDataPatchesConsumed &&
        structuralPlan.ExternalPatchPreimagesVerified &&
        structuralPlan.RelocationOffsetsVerified &&
        structuralPlan.RequiresTexturePageProof &&
        structuralPlan.RequiresDuckStationRuntimeProof &&
        structuralPlan.RelocatedExternalPatches.Count == 0 &&
        structuralPlan.StructuralGrowthPolicy ==
            NativeTerrainTextureStructuralGrowthPolicy
                .FiftyRowStaticResearch &&
        structuralPlan.StaticResearchOnly,
        "The count113 sector relocation changed its exact source, WAD, entry, LBA, or no-external-patch contract.");
    Assert(
        append.TargetEntryWadOffset ==
            ExpectedTargetEntryWadOffset &&
        append.TargetEntryByteLength ==
            ExpectedTargetEntryBytes &&
        append.LevelDataWadOffset ==
            ExpectedLevelDataWadOffset &&
        append.LevelDataByteLength ==
            ExpectedLevelDataBytes &&
        append.OutputLevelDataByteLength ==
            ExpectedOutputLevelDataBytes &&
        append.SourceTextureCount ==
            SourceTextureCount &&
        append.OutputTextureCount ==
            OutputTextureCount &&
        append.SourceTextureComponentByteLength ==
            ExpectedTextureComponentBytes &&
        append.OutputTextureComponentByteLength ==
            ExpectedOutputTextureComponentBytes &&
        append.RecordCountAdded ==
            AppendedRowCount &&
        append.GrowthByteCount ==
            ExpectedTableGrowthBytes &&
        append.SourceUsedByteLength ==
            ExpectedSourceUsedBytes &&
        append.OutputUsedByteLength ==
            ExpectedOutputUsedBytes &&
        append.SourceZeroTailByteCount ==
            ExpectedSourceZeroTailBytes &&
        append.OutputZeroTailByteCount ==
            ExpectedOutputZeroTailBytes &&
        !append.Patch.IsFixedLength &&
        append.Patch.SourceByteLength ==
            ExpectedLevelDataBytes &&
        append.Patch.OutputByteLength ==
            ExpectedOutputLevelDataBytes &&
        !append.FixedSubfileBoundaryPreserved &&
        !append.ArchiveHeadersRequireNoFixup &&
        append.RuntimeTargetsPersistent &&
        append.ExistingRowsPreserved &&
        append.AppendedRowsCopiedExactly &&
        append.ShiftedComponentChainReparsed &&
        append.ShiftedTerrainSurfaceSemanticsVerified &&
        append.ZeroTailVerified &&
        append.ExistingPatchesRebasedExactly &&
        append.RequiresDuckStationRuntimeProof &&
        append.StructuralGrowthPolicy ==
            NativeTerrainTextureStructuralGrowthPolicy
                .FiftyRowStaticResearch &&
        append.StaticResearchOnly &&
        append.ResolvedRecords.Count ==
            AppendedRowCount &&
        append.RebasedPatches.Count ==
            AppendedRowCount &&
        structuralPlan.RelocatedLevelDataPatchProofs.Count ==
            AppendedRowCount,
        "The count113 append changed its exact row, component, tail, or face-rebase contract.");
    Assert(
        append.ShiftedComponents.Count > 0 &&
        append.ShiftedComponents.All(item =>
            item.OutputOffset ==
                item.SourceOffset +
                    ExpectedTableGrowthBytes &&
            item.ByteLength > 0 &&
            item.ComposedSourceSha256.Equals(
                item.OutputSha256,
                StringComparison.OrdinalIgnoreCase)),
        "A count113 suffix component was not shifted by +0x1700 byte-identically except for its composed face patches.");
    Assert(
        append.ResolvedRecords
            .Select(item => item.AssignedTextureId)
            .SequenceEqual(
                Enumerable.Range(
                    FirstAppendedTextureId,
                    AppendedRowCount)) &&
        append.ResolvedRecords.All(item =>
            item.DonorLevelKey.Equals(
                LevelCatalog.NormalizeKey(level.Key),
                StringComparison.OrdinalIgnoreCase) &&
            item.DonorWadEntry ==
                level.SourceWadEntry &&
            item.DonorTextureId ==
                SourceTextureId &&
            item.MaterialTemplateTextureId ==
                SourceTextureId &&
            item.LowDetailSha256.Equals(
                Sha256Bytes(sourceT12Low),
                StringComparison.OrdinalIgnoreCase) &&
            item.HighDetailSha256.Equals(
                Sha256Bytes(sourceT12High),
                StringComparison.OrdinalIgnoreCase)),
        "The count113 plan did not assign exactly T81-T112 as complete native T12 clones.");
    foreach (FaceAssignment assignment in assignments)
    {
        NativeTerrainTextureRecordRebasedPatchProof
            rebased =
                structuralPlan
                    .RelocatedLevelDataPatchProofs
                    .Single(item =>
                        item.RuntimeKey.Equals(
                            assignment.RuntimeKey,
                            StringComparison.OrdinalIgnoreCase));
        Assert(
            rebased.SourceWadOffset ==
                assignment.SourceFaceWordWadOffset &&
            rebased.OutputWadOffset ==
                assignment.OutputFaceWordWadOffset &&
            rebased.ByteLength == sizeof(uint),
            $"Face {assignment.RuntimeKey} did not rebase from its exact source to output word.");
    }
    for (int textureId = 0;
         textureId < SourceTextureCount;
         textureId++)
    {
        Assert(
            SliceLowRow(
                donorFixture.SourceLevelData,
                SourceTextureCount,
                textureId)
                .SequenceEqual(
                    SliceLowRow(
                        append.Patch.After,
                        OutputTextureCount,
                        textureId)) &&
            SliceHighRow(
                donorFixture.SourceLevelData,
                SourceTextureCount,
                textureId)
                .SequenceEqual(
                    SliceHighRow(
                        append.Patch.After,
                        OutputTextureCount,
                        textureId)),
            $"Native Tree Tops T{textureId} was overridden while building count113.");
    }
    for (int textureId = FirstAppendedTextureId;
         textureId < OutputTextureCount;
         textureId++)
    {
        Assert(
            SliceLowRow(
                append.Patch.After,
                OutputTextureCount,
                textureId)
                .SequenceEqual(sourceT12Low) &&
            SliceHighRow(
                append.Patch.After,
                OutputTextureCount,
                textureId)
                .SequenceEqual(sourceT12High),
            $"Appended Tree Tops T{textureId} is not a complete byte-identical T12 clone.");
    }
    Assert(
        ExpectedSceneEnd == 0x80187BF4 &&
        NewLowerPolygonBuffer == 0x80187C30 &&
        ExpectedPolygonMargin == 0x3C &&
        ExpectedSceneEnd <
            NewLowerPolygonBuffer &&
        ExpectedSceneEnd >
            OriginalLowerPolygonBuffer,
        "The count113 scene-end/new-polygon-buffer margin changed.");
}

ArtifactReadback VerifyStructuralArtifact(
    string imagePath,
    NativeTerrainTextureRecordSectorRelocationPlan
        structuralPlan)
{
    DiscLayout sourceLayout =
        DetectLayout(sourceImagePath);
    DiscLayout outputLayout =
        DetectLayout(imagePath);
    Assert(
        sourceLayout.SectorSize == 2352 &&
        sourceLayout.UserOffset == 24 &&
        outputLayout == sourceLayout &&
        FileLength(imagePath) == sourceLength,
        "The count113 artifact changed the expected raw-disc layout or physical length.");
    RootFile sourceWad =
        ReadRootFiles(
            sourceImagePath,
            sourceLayout)
            .Single(IsWad);
    RootFile outputWad =
        ReadRootFiles(
            imagePath,
            outputLayout)
            .Single(IsWad);
    RootFile sourceExecutable =
        ReadRootFiles(
            sourceImagePath,
            sourceLayout)
            .Single(IsExecutable);
    RootFile outputExecutable =
        ReadRootFiles(
            imagePath,
            outputLayout)
            .Single(IsExecutable);
    Assert(
        sourceWad.Lba == WadLba &&
        outputWad.Lba == WadLba &&
        sourceWad.Size ==
            ExpectedOriginalWadSize &&
        outputWad.Size ==
            ExpectedExpandedWadSize &&
        sourceExecutable.Lba ==
            ExpectedOriginalExecutableLba &&
        outputExecutable.Lba ==
            ExpectedRelocatedExecutableLba &&
        sourceExecutable.Size ==
            ExpectedExecutableSize &&
        outputExecutable.Size ==
            ExpectedExecutableSize &&
        sourceExecutable.Name.Equals(
            outputExecutable.Name,
            StringComparison.OrdinalIgnoreCase),
        "The count113 ISO root did not read back exact WAD growth and executable relocation.");

    ArchiveRecord[] sourceEntries =
        ReadArchiveRecords(
            sourceImagePath,
            sourceWad.Size,
            archiveWadOffset: 0);
    ArchiveRecord[] outputEntries =
        ReadArchiveRecords(
            imagePath,
            outputWad.Size,
            archiveWadOffset: 0);
    ArchiveRecord sourceTarget =
        sourceEntries.Single(item =>
            item.Index == ExpectedWadEntry);
    ArchiveRecord outputTarget =
        outputEntries.Single(item =>
            item.Index == ExpectedWadEntry);
    Assert(
        sourceTarget.Offset ==
            ExpectedTargetEntryWadOffset &&
        sourceTarget.Size ==
            ExpectedTargetEntryBytes &&
        outputTarget.Offset ==
            ExpectedTargetEntryWadOffset &&
        outputTarget.Size ==
            ExpectedExpandedTargetEntryBytes,
        "WAD entry 52 did not read back exact +0x1800 growth.");
    Assert(
        sourceEntries.Length == outputEntries.Length &&
        sourceEntries.All(sourceEntry =>
        {
            ArchiveRecord outputEntry =
                outputEntries.Single(item =>
                    item.Index ==
                        sourceEntry.Index);
            long expectedOffset =
                sourceEntry.Offset >=
                    sourceTarget.EndExclusive
                    ? sourceEntry.Offset +
                        ExpectedSectorGrowthBytes
                    : sourceEntry.Offset;
            long expectedSize =
                sourceEntry.Index ==
                    ExpectedWadEntry
                    ? sourceEntry.Size +
                        ExpectedSectorGrowthBytes
                    : sourceEntry.Size;
            return
                outputEntry.Offset ==
                    expectedOffset &&
                outputEntry.Size ==
                    expectedSize;
        }),
        "A WAD entry changed outside the exact entry-52 growth/later-entry relocation contract.");

    ArchiveRecord[] sourceSubfiles =
        ReadArchiveRecords(
            sourceImagePath,
            sourceTarget.Size,
            sourceTarget.Offset);
    ArchiveRecord[] outputSubfiles =
        ReadArchiveRecords(
            imagePath,
            outputTarget.Size,
            outputTarget.Offset);
    ArchiveRecord sourceTexturePages =
        sourceSubfiles.Single(item =>
            item.Index == 0);
    ArchiveRecord outputTexturePages =
        outputSubfiles.Single(item =>
            item.Index == 0);
    ArchiveRecord sourceLevelData =
        sourceSubfiles.Single(item =>
            item.Index == 1);
    ArchiveRecord outputLevelData =
        outputSubfiles.Single(item =>
            item.Index == 1);
    Assert(
        sourceSubfiles.Length ==
            outputSubfiles.Length &&
        sourceTarget.Offset +
            sourceLevelData.Offset ==
            ExpectedLevelDataWadOffset &&
        outputTarget.Offset +
            outputLevelData.Offset ==
            ExpectedLevelDataWadOffset &&
        sourceLevelData.Size ==
            ExpectedLevelDataBytes &&
        outputLevelData.Size ==
            ExpectedOutputLevelDataBytes &&
        sourceTexturePages.Size ==
            outputTexturePages.Size,
        "Tree Tops nested texture/level-data headers changed outside the expected +0x1800 contract.");

    byte[] finalLevelData = ReadWadBytes(
        imagePath,
        structuralPlan.RelocatedLevelDataWadOffset,
        structuralPlan.Append.Patch.OutputByteLength);
    Assert(
        finalLevelData.SequenceEqual(
            structuralPlan.Append.Patch.After),
        "The final count113 level-data bytes do not exactly match the composed payload.");
    string sourceTexturePagesSha256 =
        Sha256Bytes(
            ReadWadBytes(
                sourceImagePath,
                sourceTarget.Offset +
                    sourceTexturePages.Offset,
                checked(
                    (int)sourceTexturePages.Size)));
    string outputTexturePagesSha256 =
        Sha256Bytes(
            ReadWadBytes(
                imagePath,
                outputTarget.Offset +
                    outputTexturePages.Offset,
                checked(
                    (int)outputTexturePages.Size)));
    Assert(
        sourceTexturePagesSha256.Equals(
            outputTexturePagesSha256,
            StringComparison.Ordinal),
        "The count113 diagnostic changed texture-page bytes.");

    IReadOnlyList<TerrainTextureSlot> slots =
        TerrainPatchExporter.InspectTextureSlots(
            imagePath,
            level);
    Assert(
        slots.Count == OutputTextureCount &&
        slots[^1].TextureId ==
            OutputTextureCount - 1,
        $"The final decoder found {slots.Count} rows, expected {OutputTextureCount}.");
    foreach (int textureId in
             Enumerable.Range(
                 FirstAppendedTextureId,
                 AppendedRowCount))
    {
        Assert(
            slots[SourceTextureId]
                .CombinedTopologySignature.Equals(
                    slots[textureId]
                        .CombinedTopologySignature,
                    StringComparison.Ordinal) &&
            slots[SourceTextureId]
                .HasNormalDescriptors ==
                slots[textureId]
                    .HasNormalDescriptors &&
            slots[SourceTextureId]
                .HasCloseDescriptors ==
                slots[textureId]
                    .HasCloseDescriptors,
            $"Final T{textureId} did not decode with native T12 topology.");
    }
    foreach (FaceAssignment assignment in assignments)
    {
        uint actual =
            BinaryPrimitives.ReadUInt32LittleEndian(
                ReadWadBytes(
                    imagePath,
                    assignment.OutputFaceWordWadOffset,
                    sizeof(uint)));
        Assert(
            actual ==
                assignment.OutputFaceWord,
            $"Final {assignment.RuntimeKey} word was 0x{actual:X8}, expected 0x{assignment.OutputFaceWord:X8}.");
    }
    NativeTerrainTextureRuntimeControlAudit runtime =
        NativeTerrainTextureRuntimeControlScanner
            .Inspect(
                imagePath,
                level);
    Assert(
        runtime.Complete &&
        runtime.TextureCount ==
            OutputTextureCount &&
        Enumerable.Range(
                FirstAppendedTextureId,
                AppendedRowCount)
            .All(runtime.IsRuntimePersistentTarget),
        "The final count113 artifact failed runtime-target readback for T81-T112.");
    return new ArtifactReadback(
        sourceTexturePagesSha256,
        outputTexturePagesSha256);
}

async Task<CombinedFastProof>
    BuildCombinedFastEntryCloneAsync(
        string structuralImagePath,
        string structuralCuePath,
        string outputPrefix,
        NativeTerrainTextureRecordSectorRelocationPlan
            structuralPlan,
        IReadOnlyList<ExecutablePatch>
            executablePatches)
{
    string outputImagePath =
        outputPrefix + ".bin";
    string outputCuePath =
        outputPrefix + ".cue";
    string temporaryImagePath =
        outputImagePath +
        $".tmp-{Guid.NewGuid():N}";
    if (File.Exists(outputImagePath))
        File.Delete(outputImagePath);
    if (File.Exists(outputCuePath))
        File.Delete(outputCuePath);
    try
    {
        await Task.Run(() =>
            File.Copy(
                structuralImagePath,
                temporaryImagePath,
                overwrite: false));

        MobySourcePatch fastEntryPatch =
            TestLevelWarpPatch
                .ApplyToDisposableDiagnosticImage(
                    temporaryImagePath,
                    level);
        Assert(
            TestLevelWarpPatch.TryGetTargetLevelId(
                fastEntryPatch,
                out int targetLevelId) &&
            targetLevelId ==
                ExpectedLevelId &&
            fastEntryPatch.BeforeHexPreview ==
                "37 B6 00 08 00 00 00 00" &&
            fastEntryPatch.AfterHexPreview ==
                "1C 06 84 AF 88 06 80 AF" &&
            fastEntryPatch.ByteLength == 8,
            "The guarded Tree Tops FAST-ENTRY patch changed.");

        DiscLayout layout =
            DetectLayout(temporaryImagePath);
        RootFile executable =
            ReadRootFiles(
                temporaryImagePath,
                layout)
                .Single(IsExecutable);
        Assert(
            executable.Name.Equals(
                TestLevelWarpPatch
                    .SupportedExecutableName,
                StringComparison.OrdinalIgnoreCase) &&
            executable.Lba ==
                ExpectedRelocatedExecutableLba &&
            executable.Size ==
                ExpectedExecutableSize,
            "The combined count113 patch did not find the exact relocated USA executable.");
        foreach (ExecutablePatch patch in
                 executablePatches)
        {
            byte[] actual =
                ReadDiscFileBytes(
                    temporaryImagePath,
                    layout,
                    executable.Lba,
                    patch.FileOffset,
                    patch.Before.Length);
            Assert(
                actual.SequenceEqual(
                    patch.Before),
                $"Polygon-buffer preimage failed at SCUS+0x{patch.FileOffset:X}: expected {HexText(patch.Before)}, got {HexText(actual)}.");
        }
        using (FileStream output = new(
                   temporaryImagePath,
                   FileMode.Open,
                   FileAccess.ReadWrite,
                   FileShare.None))
        {
            foreach (ExecutablePatch patch in
                     executablePatches)
            {
                long physicalOffset =
                    ToPhysicalDiscOffset(
                        layout,
                        executable.Lba,
                        patch.FileOffset);
                output.Position = physicalOffset;
                byte[] actual =
                    new byte[patch.Before.Length];
                output.ReadExactly(actual);
                Assert(
                    actual.SequenceEqual(
                        patch.Before),
                    $"Temporary-image polygon preimage changed at SCUS+0x{patch.FileOffset:X}.");
                output.Position = physicalOffset;
                output.Write(patch.After);
            }
            output.Flush(flushToDisk: true);
        }
        File.Move(
            temporaryImagePath,
            outputImagePath);
    }
    finally
    {
        if (File.Exists(temporaryImagePath))
            File.Delete(temporaryImagePath);
    }

    await File.WriteAllTextAsync(
        outputCuePath,
        BuildCueTextForImage(
            structuralCuePath,
            Path.GetFileName(outputImagePath)),
        Encoding.ASCII);

    DiscLayout finalLayout =
        DetectLayout(outputImagePath);
    RootFile finalExecutable =
        ReadRootFiles(
            outputImagePath,
            finalLayout)
            .Single(IsExecutable);
    foreach (ExecutablePatch patch in
             executablePatches)
    {
        byte[] actual =
            ReadDiscFileBytes(
                outputImagePath,
                finalLayout,
                finalExecutable.Lba,
                patch.FileOffset,
                patch.After.Length);
        Assert(
            actual.SequenceEqual(
                patch.After),
            $"Final polygon-buffer readback failed at SCUS+0x{patch.FileOffset:X}.");
    }
    byte[] finalFastEntry =
        ReadDiscFileBytes(
            outputImagePath,
            finalLayout,
            finalExecutable.Lba,
            checked(
                (int)TestLevelWarpPatch
                    .ExecutableFileOffset),
            8);
    Assert(
        finalFastEntry.SequenceEqual(
            Convert.FromHexString(
                "1C0684AF880680AF")),
        "Final FAST-ENTRY readback failed.");
    int differences =
        CountFileByteDifferences(
            structuralImagePath,
            outputImagePath);
    Assert(
        differences == 15,
        $"Combined count113 candidate differs from its structural image by {differences} byte(s), expected exact 8-byte FAST-ENTRY plus 7 changed polygon bytes.");
    _ = VerifyStructuralArtifact(
        outputImagePath,
        structuralPlan);
    string cue = await File.ReadAllTextAsync(
        outputCuePath,
        Encoding.ASCII);
    Assert(
        cue.Contains(
            $"FILE \"{Path.GetFileName(outputImagePath)}\" BINARY",
            StringComparison.Ordinal) &&
        !cue.Contains(
            Path.GetFileName(structuralImagePath),
            StringComparison.Ordinal),
        "The final CUE does not target only the count113 BIN.");
    return new CombinedFastProof(
        outputImagePath,
        outputCuePath,
        Sha256File(outputImagePath),
        differences);
}

static bool IsWad(RootFile record) =>
    record.Name.Equals(
        "WAD.WAD",
        StringComparison.OrdinalIgnoreCase) ||
    record.Name.Equals(
        "WAD",
        StringComparison.OrdinalIgnoreCase);

static bool IsExecutable(RootFile record)
{
    string upper =
        record.Name.ToUpperInvariant();
    return
        upper.StartsWith(
            "SCUS",
            StringComparison.Ordinal) ||
        upper.StartsWith(
            "SLUS",
            StringComparison.Ordinal) ||
        upper.StartsWith(
            "SLES",
            StringComparison.Ordinal) ||
        upper.StartsWith(
            "SCES",
            StringComparison.Ordinal) ||
        upper.EndsWith(
            ".EXE",
            StringComparison.Ordinal);
}

static IReadOnlyList<RootFile> ReadRootFiles(
    string imagePath,
    DiscLayout layout)
{
    byte[] root = ReadDiscFileBytes(
        imagePath,
        layout,
        layout.RootLba,
        0,
        layout.RootLength);
    List<RootFile> records = [];
    int offset = 0;
    while (offset < root.Length)
    {
        int recordLength = root[offset];
        if (recordLength == 0)
        {
            offset =
                ((offset / 2048) + 1) *
                2048;
            continue;
        }
        if (recordLength < 34 ||
            offset + recordLength >
                root.Length)
        {
            break;
        }
        int nameLength =
            root[offset + 32];
        string name =
            Encoding.ASCII.GetString(
                root,
                offset + 33,
                nameLength)
            .Replace(
                ";1",
                "",
                StringComparison.OrdinalIgnoreCase);
        if (name is not "\0" and not "\u0001")
        {
            records.Add(
                new RootFile(
                    name,
                    checked(
                        (int)ReadUInt32(
                            root,
                            offset + 2)),
                    checked(
                        (int)ReadUInt32(
                            root,
                            offset + 10))));
        }
        offset += recordLength;
    }
    Assert(
        records.Count > 0,
        "The ISO9660 root exposed no files.");
    return records;
}

static ArchiveRecord[] ReadArchiveRecords(
    string imagePath,
    long archiveSize,
    long archiveWadOffset)
{
    byte[] first = ReadWadBytes(
        imagePath,
        archiveWadOffset,
        8);
    long firstDataOffset =
        ReadUInt32(first, 0);
    Assert(
        firstDataOffset > 0 &&
        firstDataOffset <= archiveSize &&
        firstDataOffset <= int.MaxValue,
        $"Archive at WAD 0x{archiveWadOffset:X} has an invalid first-data offset.");
    byte[] header = ReadWadBytes(
        imagePath,
        archiveWadOffset,
        checked((int)firstDataOffset));
    List<ArchiveRecord> records = [];
    for (int offset = 0;
         offset <= header.Length - 8;
         offset += 8)
    {
        long fileOffset =
            ReadUInt32(header, offset);
        long fileSize =
            ReadUInt32(header, offset + 4);
        if (fileOffset == 0 &&
            fileSize == 0)
        {
            continue;
        }
        if (fileOffset <
                firstDataOffset ||
            fileSize <= 0 ||
            fileOffset + fileSize >
                archiveSize)
        {
            continue;
        }
        records.Add(
            new ArchiveRecord(
                offset / 8,
                fileOffset,
                fileSize));
    }
    Assert(
        records.Count > 0,
        $"Archive at WAD 0x{archiveWadOffset:X} exposed no valid entries.");
    return records.ToArray();
}

static DiscLayout DetectLayout(
    string imagePath)
{
    using FileStream stream =
        File.OpenRead(imagePath);
    foreach ((int sectorSize, int userOffset) in
             new[]
             {
                 (2048, 0),
                 (2352, 24),
                 (2336, 8)
             })
    {
        long pvdOffset =
            (16L * sectorSize) +
            userOffset;
        if (pvdOffset + 2048 >
            stream.Length)
        {
            continue;
        }
        byte[] pvd = new byte[2048];
        stream.Position = pvdOffset;
        stream.ReadExactly(pvd);
        if (pvd[0] != 1 ||
            Encoding.ASCII.GetString(
                pvd,
                1,
                5) != "CD001")
        {
            continue;
        }
        return new DiscLayout(
            sectorSize,
            userOffset,
            checked(
                (int)ReadUInt32(
                    pvd,
                    158)),
            checked(
                (int)ReadUInt32(
                    pvd,
                    166)));
    }
    throw new InvalidDataException(
        "Could not detect the diagnostic image layout.");
}

static byte[] ReadDiscFileBytes(
    string imagePath,
    DiscLayout layout,
    int fileLba,
    long fileOffset,
    int length)
{
    byte[] result =
        new byte[length];
    using FileStream stream =
        File.OpenRead(imagePath);
    int written = 0;
    long cursor = fileOffset;
    while (written < result.Length)
    {
        int sectorOffset =
            checked(
                (int)(cursor % 2048));
        int sector =
            fileLba +
            checked(
                (int)(cursor / 2048));
        int count =
            Math.Min(
                result.Length - written,
                2048 - sectorOffset);
        stream.Position =
            ((long)sector *
             layout.SectorSize) +
            layout.UserOffset +
            sectorOffset;
        stream.ReadExactly(
            result.AsSpan(
                written,
                count));
        written += count;
        cursor += count;
    }
    return result;
}

static long ToPhysicalDiscOffset(
    DiscLayout layout,
    int fileLba,
    long fileOffset)
{
    int sector =
        fileLba +
        checked(
            (int)(fileOffset / 2048));
    int sectorOffset =
        checked(
            (int)(fileOffset % 2048));
    return
        ((long)sector *
         layout.SectorSize) +
        layout.UserOffset +
        sectorOffset;
}

static byte[] ReadWadBytes(
    string imagePath,
    long wadOffset,
    int length)
{
    DiscLayout layout =
        DetectLayout(imagePath);
    return ReadDiscFileBytes(
        imagePath,
        layout,
        WadLba,
        wadOffset,
        length);
}

static byte[] SliceLowRow(
    byte[] levelData,
    int textureCount,
    int textureId)
{
    Assert(
        textureId >= 0 &&
        textureId < textureCount,
        $"T{textureId} is outside texture count {textureCount}.");
    int offset = checked(
        8 +
        (textureId *
         NativeTerrainTextureRecordAppendBuilder
             .LowDetailRecordBytes));
    return levelData.AsSpan(
            offset,
            NativeTerrainTextureRecordAppendBuilder
                .LowDetailRecordBytes)
        .ToArray();
}

static byte[] SliceHighRow(
    byte[] levelData,
    int textureCount,
    int textureId)
{
    Assert(
        textureId >= 0 &&
        textureId < textureCount,
        $"T{textureId} is outside texture count {textureCount}.");
    int offset = checked(
        8 +
        (textureCount *
         NativeTerrainTextureRecordAppendBuilder
             .LowDetailRecordBytes) +
        (textureId *
         NativeTerrainTextureRecordAppendBuilder
             .HighDetailRecordBytes));
    return levelData.AsSpan(
            offset,
            NativeTerrainTextureRecordAppendBuilder
                .HighDetailRecordBytes)
        .ToArray();
}

static uint ReplaceTextureId(
    uint sourceWord,
    int targetTextureId)
{
    Assert(
        targetTextureId is >= 0 and < 128,
        $"T{targetTextureId} is outside the native seven-bit face field.");
    return
        (sourceWord & ~0x7Fu) |
        (uint)targetTextureId;
}

static byte[] UInt32Bytes(uint value)
{
    byte[] bytes =
        new byte[sizeof(uint)];
    BinaryPrimitives.WriteUInt32LittleEndian(
        bytes,
        value);
    return bytes;
}

static uint ReadUInt32(
    byte[] bytes,
    int offset) =>
    BinaryPrimitives.ReadUInt32LittleEndian(
        bytes.AsSpan(
            offset,
            sizeof(uint)));

static string BuildCueTextForImage(
    string sourceCuePath,
    string outputBinFileName)
{
    string[] lines =
        File.ReadAllLines(sourceCuePath);
    int fileLineCount = 0;
    for (int index = 0;
         index < lines.Length;
         index++)
    {
        if (!lines[index]
                .TrimStart()
                .StartsWith(
                    "FILE ",
                    StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }
        lines[index] =
            $"FILE \"{outputBinFileName}\" BINARY";
        fileLineCount++;
    }
    Assert(
        fileLineCount == 1,
        $"Expected one FILE line in {sourceCuePath}, found {fileLineCount}.");
    return
        string.Join(
            Environment.NewLine,
            lines) +
        Environment.NewLine;
}

static int CountFileByteDifferences(
    string firstPath,
    string secondPath)
{
    using FileStream first =
        File.OpenRead(firstPath);
    using FileStream second =
        File.OpenRead(secondPath);
    Assert(
        first.Length == second.Length,
        "Cannot compare differently sized BINs.");
    byte[] firstBuffer =
        new byte[1024 * 1024];
    byte[] secondBuffer =
        new byte[firstBuffer.Length];
    int differences = 0;
    while (true)
    {
        int firstRead =
            first.Read(
                firstBuffer,
                0,
                firstBuffer.Length);
        int secondRead =
            second.Read(
                secondBuffer,
                0,
                secondBuffer.Length);
        Assert(
            firstRead == secondRead,
            "BIN comparison streams returned different read lengths.");
        if (firstRead == 0)
            return differences;
        differences +=
            CountByteDifferences(
                firstBuffer.AsSpan(
                    0,
                    firstRead),
                secondBuffer.AsSpan(
                    0,
                    secondRead));
    }
}

static int CountByteDifferences(
    ReadOnlySpan<byte> first,
    ReadOnlySpan<byte> second)
{
    Assert(
        first.Length == second.Length,
        "Cannot compare differently sized byte spans.");
    int differences = 0;
    for (int index = 0;
         index < first.Length;
         index++)
    {
        if (first[index] != second[index])
            differences++;
    }
    return differences;
}

static string ResolveWorkspaceRoot(
    string? candidate)
{
    string current =
        Path.GetFullPath(
            candidate ??
            Directory.GetCurrentDirectory());
    for (DirectoryInfo? directory =
             new(current);
         directory != null;
         directory = directory.Parent)
    {
        if (File.Exists(
                Path.Combine(
                    directory.FullName,
                    "Spyro the Dragon (USA).bin")) &&
            Directory.Exists(
                Path.Combine(
                    directory.FullName,
                    "src",
                    "Spyro.Editor.Core")))
        {
            return directory.FullName;
        }
    }
    return current;
}

static string ResolvePhysicalPath(
    string path)
{
    FileInfo info =
        new(Path.GetFullPath(path));
    return info.ResolveLinkTarget(
               returnFinalTarget: true)
               ?.FullName ??
           info.FullName;
}

static string Sha256File(
    string path)
{
    using FileStream stream =
        File.OpenRead(path);
    return Convert.ToHexString(
        SHA256.HashData(stream));
}

static string Sha256Bytes(
    ReadOnlySpan<byte> bytes) =>
    Convert.ToHexString(
        SHA256.HashData(bytes));

static long FileLength(
    string path)
{
    using FileStream stream =
        File.OpenRead(path);
    return stream.Length;
}

static string HexText(
    byte[] bytes) =>
    string.Join(
        " ",
        bytes.Select(value =>
            value.ToString("X2")));

static void DeleteOutputs(
    params string[] prefixes)
{
    foreach (string prefix in prefixes)
    {
        foreach (string extension in new[]
                 {
                     ".bin",
                     ".cue",
                     ".terrain-patch-plan.json"
                 })
        {
            string path =
                prefix + extension;
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}

static void Assert(
    bool condition,
    string message)
{
    if (!condition)
        throw new InvalidOperationException(
            message);
}

internal sealed record FaceAssignment(
    string RuntimeKey,
    int TargetTextureId,
    long SourceFaceWordWadOffset,
    uint SourceFaceWord,
    long OutputFaceWordWadOffset,
    uint OutputFaceWord);

internal sealed record ExecutablePatch(
    int FileOffset,
    uint RuntimePc,
    byte[] Before,
    byte[] After,
    string Purpose)
{
    public ExecutablePatch(
        int fileOffset,
        uint runtimePc,
        string beforeHex,
        string afterHex,
        string purpose)
        : this(
            fileOffset,
            runtimePc,
            Convert.FromHexString(
                beforeHex.Replace(
                    " ",
                    "",
                    StringComparison.Ordinal)),
            Convert.FromHexString(
                afterHex.Replace(
                    " ",
                    "",
                    StringComparison.Ordinal)),
            purpose)
    {
    }
}

internal sealed record CombinedFastProof(
    string ImagePath,
    string CuePath,
    string Sha256,
    int StructuralToFinalByteDifferences);

internal sealed record ArtifactReadback(
    string SourceTexturePagesSha256,
    string OutputTexturePagesSha256);

internal readonly record struct DiscLayout(
    int SectorSize,
    int UserOffset,
    int RootLba,
    int RootLength);

internal sealed record RootFile(
    string Name,
    int Lba,
    int Size);

internal sealed record ArchiveRecord(
    int Index,
    long Offset,
    long Size)
{
    public long EndExclusive =>
        checked(Offset + Size);
}
