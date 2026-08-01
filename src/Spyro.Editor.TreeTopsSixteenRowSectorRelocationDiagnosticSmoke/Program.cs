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
const int AppendedRowCount = 16;
const int OutputTextureCount =
    SourceTextureCount + AppendedRowCount;
const int FirstAppendedTextureId = SourceTextureCount;
const int ExpectedTextureComponentBytes = 0x3A40;
const int ExpectedOutputTextureComponentBytes = 0x45C0;
const int ExpectedLevelDataBytes = 0xA8000;
const int ExpectedOutputLevelDataBytes = 0xA9000;
const int ExpectedSourceUsedBytes = 0xA7CAC;
const int ExpectedOutputUsedBytes = 0xA882C;
const int ExpectedSourceZeroTailBytes = 0x354;
const int ExpectedOutputZeroTailBytes = 0x7D4;
const int ExpectedTableGrowthBytes = 0xB80;
const int ExpectedSectorGrowthBytes = 0x1000;
const int ExpectedAdditionalBytesNeeded = 0x82C;
const int ExpectedOriginalWadSize = 0x6927000;
const int ExpectedExpandedWadSize = 0x6928000;
const int ExpectedOriginalExecutableLba = 53875;
const int ExpectedRelocatedExecutableLba = 53877;
const long ExpectedTargetEntryWadOffset = 0x3D88000;
const int ExpectedTargetEntryBytes = 0x29C800;
const int ExpectedExpandedTargetEntryBytes = 0x29D800;
const long ExpectedLevelDataWadOffset = 0x3E62800;
const uint RetailSceneBase = 0x8016E4F4;
const int RetailSceneSize = 0x18000;
const uint RetailSceneEnd = RetailSceneBase + RetailSceneSize;
const uint FixedLowerPolygonBuffer = 0x80187BB0;
const uint ExpectedSixteenRowSceneEnd =
    RetailSceneEnd + ExpectedTableGrowthBytes;
const uint ExpectedSixteenRowPolygonMargin =
    FixedLowerPolygonBuffer - ExpectedSixteenRowSceneEnd;
const string ExpectedTextureComponentSha256 =
    "9E0E9BD9AFE845C882B6EF7CDE41F421898C7E9EA2F041CEACF7371F1971858A";
const string ExpectedLevelDataSha256 =
    "BCA21FDF3FDEB0D571DA93267EFCE9DECB209F227BD7F1A4A264E2CE5132046C";

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
string sourceSearchPath = Path.Combine(
    outputRoot,
    "treetops-count97-source-search.json");
string wadAnalysisPath = Path.Combine(
    outputRoot,
    "source-bound-wad-analysis.json");
string dormantPrefix = Path.Combine(
    outputRoot,
    "treetops-COUNT97-SIXTEEN-ROWS-T81-T96-CLONE-T12-UNREFERENCED-SECTOR-RELOCATION-CONTROL");
string dormantFastPrefix =
    dormantPrefix + "-FAST-ENTRY";
string visiblePrefix = Path.Combine(
    outputRoot,
    "treetops-COUNT97-SIXTEEN-ROWS-T81-T96-CLONE-T12-SIXTEEN-VISIBLE-FACES-SECTOR-RELOCATION-DIAGNOSTIC");
string visibleFastPrefix =
    visiblePrefix + "-FAST-ENTRY";
string editsPath =
    visiblePrefix + ".terrain-edits.json";
string proofPath = Path.Combine(
    outputRoot,
    "tree-tops-count97-sixteen-row-sector-relocation-diagnostic-proof.json");
string checklistPath = Path.Combine(
    outputRoot,
    "tree-tops-count97-sixteen-row-runtime-checklist.md");

Directory.CreateDirectory(outputRoot);
DeleteOutputs(
    dormantPrefix,
    dormantFastPrefix,
    visiblePrefix,
    visibleFastPrefix);
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
    File.Exists(sourceImagePath),
    $"Missing retail source BIN: {sourceImagePath}");
Assert(
    File.Exists(sourceCuePath),
    $"Missing retail source CUE: {sourceCuePath}");
Assert(
    File.Exists(overlayPath),
    $"Missing Tree Tops geometry overlay: {overlayPath}");
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
    sourceSha256.Equals(
        ExpectedSourceSha256,
        StringComparison.OrdinalIgnoreCase) &&
    sourceLength == ExpectedSourceLength,
    "Tree Tops count97 diagnostics are guarded to the exact clean USA retail BIN.");

LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition level = catalog.FindByKey("treetops")
    ?? throw new InvalidOperationException(
        "Tree Tops is missing from the level catalog.");
Assert(
    level.LevelId == ExpectedLevelId &&
    level.SourceWadEntry == ExpectedWadEntry,
    "Tree Tops level identity changed.");

NativeTerrainTextureRecordAppendSourceBinding binding =
    NativeTerrainTextureRecordAppendBuilder
        .InspectSourceBinding(
            sourceImagePath,
            level);
Assert(
    binding.Version ==
        NativeTerrainTextureRecordAppendBuilder
            .CurrentBindingVersion &&
    binding.ExpectedSourceTextureCount ==
        SourceTextureCount &&
    binding.ExpectedTextureComponentSha256.Equals(
        ExpectedTextureComponentSha256,
        StringComparison.OrdinalIgnoreCase) &&
    binding.ExpectedLevelDataSha256.Equals(
        ExpectedLevelDataSha256,
        StringComparison.OrdinalIgnoreCase),
    "Tree Tops count97 source binding changed.");

NativeTerrainTextureRecordAppendCapacity capacity =
    NativeTerrainTextureRecordAppendBuilder
        .InspectCapacityForRecordCount(
            sourceImagePath,
            level,
            AppendedRowCount,
            NativeTerrainTextureStructuralGrowthPolicy
                .NormalChecked,
            wadAnalysisPath);
Assert(
    capacity.SourceTextureCount ==
        SourceTextureCount &&
    capacity.RequestedRecordCount ==
        AppendedRowCount &&
    capacity.RequestedRecordGrowthBytes ==
        ExpectedTableGrowthBytes &&
    capacity.VerifiedLevelDataZeroTailBytes ==
        ExpectedSourceZeroTailBytes &&
    capacity.AdditionalBytesNeededForRequestedRecords ==
        ExpectedAdditionalBytesNeeded &&
    !capacity.CanAppendInsideCurrentSubfile &&
    capacity.WouldRequireTargetEntryGrowth &&
    capacity.RequiredSectorAlignedEntryGrowthBytes ==
        ExpectedSectorGrowthBytes &&
    capacity.SectorRelocationPlanVerified &&
    capacity.HasExecutableWriter &&
    capacity.ExecutableWriter.Equals(
        "SectorRelocation",
        StringComparison.Ordinal) &&
    capacity.StructuralGrowthPolicy ==
        NativeTerrainTextureStructuralGrowthPolicy
            .NormalChecked &&
    !capacity.StaticResearchOnly &&
    capacity.LevelDataByteLength ==
        ExpectedLevelDataBytes,
    "Tree Tops no longer exposes the exact normal-checked sixteen-row +0x1000 sector-relocation boundary.");

NativeTerrainTextureRuntimeControlAudit sourceRuntime =
    NativeTerrainTextureRuntimeControlScanner.Inspect(
        sourceImagePath,
        level);
Assert(
    sourceRuntime.Complete &&
    sourceRuntime.TextureCount ==
        SourceTextureCount &&
    sourceRuntime.IsRuntimePersistentTarget(
        SourceTextureId) &&
    !sourceRuntime.ControlledTextureIds.Contains(
        SourceTextureId) &&
    !sourceRuntime.AnimationSourceTextureIds.Contains(
        SourceTextureId),
    "Native T12 is no longer a static Tree Tops donor.");

FaceAssignment[] assignments =
[
    new(
        "1:32:hp",
        81,
        0x3E674EC,
        0xDE6E090C,
        0x3E6806C,
        0xDE6E0951),
    new(
        "1:33:hp",
        82,
        0x3E674FC,
        0xDE6FFF0C,
        0x3E6807C,
        0xDE6FFF52),
    new(
        "2:0:hp",
        83,
        0x3E67608,
        0xD63D490C,
        0x3E68188,
        0xD63D4953),
    new(
        "2:1:hp",
        84,
        0x3E67618,
        0xD4DD490C,
        0x3E68198,
        0xD4DD4954),
    new(
        "2:2:hp",
        85,
        0x3E67628,
        0xD4AFFE0C,
        0x3E681A8,
        0xD4AFFE55),
    new(
        "3:0:hp",
        86,
        0x3E67884,
        0x0030D80C,
        0x3E68404,
        0x0030D856),
    new(
        "3:1:hp",
        87,
        0x3E67894,
        0x0E70C40C,
        0x3E68414,
        0x0E70C457),
    new(
        "3:2:hp",
        88,
        0x3E678A4,
        0xF390030C,
        0x3E68424,
        0xF3900358),
    new(
        "3:3:hp",
        89,
        0x3E678B4,
        0x0050030C,
        0x3E68434,
        0x00500359),
    new(
        "3:4:hp",
        90,
        0x3E678C4,
        0xF1CFFF0C,
        0x3E68444,
        0xF1CFFF5A),
    new(
        "3:5:hp",
        91,
        0x3E678D4,
        0xF19F2E0C,
        0x3E68454,
        0xF19F2E5B),
    new(
        "3:6:hp",
        92,
        0x3E678E4,
        0x0010D50C,
        0x3E68464,
        0x0010D55C),
    new(
        "3:7:hp",
        93,
        0x3E678F4,
        0xFFF0010C,
        0x3E68474,
        0xFFF0015D),
    new(
        "3:8:hp",
        94,
        0x3E67904,
        0xFFBF180C,
        0x3E68484,
        0xFFBF185E),
    new(
        "4:20:hp",
        95,
        0x3E68648,
        0xF4BF8A0C,
        0x3E691C8,
        0xF4BF8A5F),
    new(
        "4:21:hp",
        96,
        0x3E68658,
        0x003FFF0C,
        0x3E691D8,
        0x003FFF60)
];
Assert(
    assignments.Length == AppendedRowCount &&
    assignments
        .Select(item => item.RuntimeKey)
        .Distinct(
            StringComparer.OrdinalIgnoreCase)
        .Count() == AppendedRowCount &&
    assignments
        .Select(item => item.TargetTextureId)
        .SequenceEqual(
            Enumerable.Range(
                FirstAppendedTextureId,
                AppendedRowCount)),
    "The count97 face assignment matrix is not sixteen unique native T12 faces mapped one-to-one to T81-T96.");

GeometryCandidate sourceGeometry =
    GeometryOverlayLoader.LoadFirstCandidate(
        overlayPath);
foreach (FaceAssignment assignment in assignments)
{
    TerrainPolygon face =
        sourceGeometry.Polygons.Single(item =>
            item.RuntimeKey.Equals(
                assignment.RuntimeKey,
                StringComparison.OrdinalIgnoreCase));
    Assert(
        face.OriginalTextureId ==
            SourceTextureId &&
        face.HasCompleteNativeHighPolyFacePayload &&
        face.FaceOffset + 8 ==
            assignment.SourceFaceWordWadOffset &&
        BinaryPrimitives.ReadUInt32LittleEndian(
            ReadWadBytes(
                sourceImagePath,
                assignment.SourceFaceWordWadOffset,
                sizeof(uint))) ==
            assignment.SourceFaceWord &&
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
    await TerrainSourceSearchBuilder
        .BuildSourceDerivedAsync(
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
        $"Tree Tops face {assignment.RuntimeKey} no longer has one exact source-sector hit.");
}

GeometryCandidate editedGeometry =
    GeometryOverlayLoader.LoadFirstCandidate(
        overlayPath);
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
    "Tree Tops count97/T81-T96 sixteen-row sector-relocation diagnostic");
Assert(
    saved == AppendedRowCount,
    $"Saved {saved} count97 face edit(s), expected {AppendedRowCount}.");
TerrainPatchPlan ordinaryPlan =
    TerrainPatchExporter.BuildPlan(
        sourceImagePath,
        sourceCuePath,
        visiblePrefix + "-not-written.bin",
        visiblePrefix + "-not-written.cue",
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
    ordinaryPlan.Patches.Count ==
        AppendedRowCount &&
    ordinaryTexturePatches.Length ==
        AppendedRowCount &&
    ordinaryPlan.SkippedEdits.All(item =>
        item.Equals(
            "collision: source-derived exact triangle scan found no matching source collision records for the edited terrain face(s).",
            StringComparison.Ordinal)),
    "The ordinary Tree Tops plan was not exactly sixteen source-bound texture-word patches.");
IReadOnlyList<NativeTerrainTextureRecordExistingPatch>
    visibleFacePatches =
        NativeTerrainTexturePrivateRecordBatchCompiler
            .ConvertOrdinaryTerrainPlan(
                ordinaryPlan);
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
            UInt32Bytes(
                assignment.SourceFaceWord)) &&
        patch.After.SequenceEqual(
            UInt32Bytes(
                assignment.OutputFaceWord)) &&
        CountByteDifferences(
            patch.Before,
            patch.After) == 1,
        $"Tree Tops {assignment.RuntimeKey} is not the exact one-byte T12->T{assignment.TargetTextureId} patch.");
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
NativeTerrainTextureRecordAppendResearchPlan
    donorFixture = donorFixtureCandidate!;
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
        NativeTerrainTextureRecordAppendBuilder
            .LowDetailRecordBytes &&
    sourceT12High.Length ==
        NativeTerrainTextureRecordAppendBuilder
            .HighDetailRecordBytes,
    "The native T12 fixture did not expose one complete 184-byte row.");

NativeTerrainTexturePackedAppendRecord[] packedRows =
    assignments.Select(assignment =>
        new NativeTerrainTexturePackedAppendRecord(
            StableEditId:
                $"research-only:treetops:count97:{assignment.RuntimeKey}:T{assignment.TargetTextureId}:clone-native-T12",
            DonorLevelKey: level.Key,
            DonorWadEntry: level.SourceWadEntry,
            DonorTextureId: SourceTextureId,
            MaterialTemplateTextureId:
                SourceTextureId,
            LowDetailRow:
                sourceT12Low.ToArray(),
            HighDetailRow:
                sourceT12High.ToArray(),
            ExpectedLowDetailSha256:
                Sha256Bytes(sourceT12Low),
            ExpectedHighDetailSha256:
                Sha256Bytes(sourceT12High)))
        .ToArray();

NativeTerrainTextureRecordSectorRelocationRequest
    dormantRequest = new(
        sourceImagePath,
        sourceCuePath,
        dormantPrefix,
        wadAnalysisPath,
        level,
        binding,
        packedRows,
        Array.Empty<
            NativeTerrainTextureRecordExistingPatch>(),
        Array.Empty<
            NativeTerrainTextureRecordRelocationExternalPatch>(),
        StructuralGrowthPolicy:
            NativeTerrainTextureStructuralGrowthPolicy
                .NormalChecked);
NativeTerrainTextureRecordSectorRelocationRequest
    visibleRequest = dormantRequest with
    {
        OutputPrefix = visiblePrefix,
        ExistingLevelDataPatches =
            visibleFacePatches
    };

NativeTerrainTextureRecordSectorRelocationPlan
    dormantPlan =
        NativeTerrainTextureRecordSectorRelocationComposer
            .BuildPlan(dormantRequest);
NativeTerrainTextureRecordSectorRelocationPlan
    visiblePlan =
        NativeTerrainTextureRecordSectorRelocationComposer
            .BuildPlan(visibleRequest);
ValidateStructuralPlan(
    dormantPlan,
    expectedFacePatchCount: 0);
ValidateStructuralPlan(
    visiblePlan,
    expectedFacePatchCount: AppendedRowCount);
Assert(
    dormantPlan.RelocatedExternalPatches.Count == 0 &&
    visiblePlan.RelocatedExternalPatches.Count == 0 &&
    CountByteDifferences(
        dormantPlan.Append.Patch.After,
        visiblePlan.Append.Patch.After) ==
        AppendedRowCount,
    "The dormant/visible count97 plans are not page-patch-free or do not differ by exactly sixteen face bytes.");
foreach (FaceAssignment assignment in assignments)
{
    NativeTerrainTextureRecordRebasedPatchProof rebased =
        visiblePlan.RelocatedLevelDataPatchProofs
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
        $"Tree Tops {assignment.RuntimeKey} did not rebase to its exact count97 output offset.");
}

NativeTerrainTextureRecordSectorRelocationExportResult
    dormantExport =
        await NativeTerrainTextureRecordSectorRelocationComposer
            .ExportAsync(dormantRequest);
VerifyExportFlags(
    dormantExport,
    "dormant count97");
ArtifactReadback dormantReadback =
    VerifyFinalArtifact(
        dormantExport.OutputImagePath,
        dormantPlan,
        assignments.Select(item =>
            new FaceExpectation(
                item.RuntimeKey,
                item.OutputFaceWordWadOffset,
                item.SourceFaceWord))
            .ToArray());

NativeTerrainTextureRecordSectorRelocationExportResult
    visibleExport =
        await NativeTerrainTextureRecordSectorRelocationComposer
            .ExportAsync(visibleRequest);
VerifyExportFlags(
    visibleExport,
    "visible count97");
ArtifactReadback visibleReadback =
    VerifyFinalArtifact(
        visibleExport.OutputImagePath,
        visiblePlan,
        assignments.Select(item =>
            new FaceExpectation(
                item.RuntimeKey,
                item.OutputFaceWordWadOffset,
                item.OutputFaceWord))
            .ToArray());

Assert(
    dormantReadback.TexturePagesSha256.Equals(
        visibleReadback.TexturePagesSha256,
        StringComparison.Ordinal) &&
    dormantReadback.TexturePagesSha256.Equals(
        dormantReadback.SourceTexturePagesSha256,
        StringComparison.Ordinal) &&
    CountFileByteDifferences(
        dormantExport.OutputImagePath,
        visibleExport.OutputImagePath) ==
        AppendedRowCount,
    "The two count97 final BINs changed texture pages or differ by more than the sixteen visible face bytes.");

FastEntryProof dormantFast =
    await BuildFastEntryCloneAsync(
        dormantExport.OutputImagePath,
        dormantExport.OutputCuePath,
        dormantFastPrefix,
        dormantPlan,
        assignments.Select(item =>
            new FaceExpectation(
                item.RuntimeKey,
                item.OutputFaceWordWadOffset,
                item.SourceFaceWord))
            .ToArray());
FastEntryProof visibleFast =
    await BuildFastEntryCloneAsync(
        visibleExport.OutputImagePath,
        visibleExport.OutputCuePath,
        visibleFastPrefix,
        visiblePlan,
        assignments.Select(item =>
            new FaceExpectation(
                item.RuntimeKey,
                item.OutputFaceWordWadOffset,
                item.OutputFaceWord))
            .ToArray());
Assert(
    CountFileByteDifferences(
        dormantFast.ImagePath,
        visibleFast.ImagePath) ==
        AppendedRowCount,
    "The two count97 fast-entry BINs differ by more than the sixteen selected face bytes.");

AssertRetailSourcePreserved();

var proof = new
{
    GeneratedAtUtc = DateTimeOffset.UtcNow,
    Status =
        "COUNT97 SIXTEEN-ROW VISIBLE FAST-ENTRY CUE REPORTED LOADING WITHOUT CRASH; EXACT VISIBLE RECIPE RUNTIME-PROVEN; COUNT97 DORMANT, 31+, AND NORMAL CREATE BIN UNPROVEN",
    ResearchOnly = true,
    NormalCreateBinIntegrated = false,
    DuckStationRuntimeProven = true,
    ReportedDuckStationPrerequisites = new
    {
        ReportedAt = "2026-07-30",
        GenuineCount83TwoRowsLoaded = true,
        GenuineCount85FourRowsLoaded = true,
        GenuineCount86FiveRowsDormantLoaded = true,
        GenuineCount86FiveRowsVisibleLoaded = true,
        GenuineCount89EightRowsVisibleLoaded = true,
        GenuineCount97SixteenRowsVisibleLoaded = true,
        EvidenceScope =
            "Interactive user reports prove only the exact earlier recipes and the exact visible count97/sixteen-row cumulative research recipe."
    },
    RuntimeEvidence = new
    {
        Count97ResultRecorded = true,
        RecordedAtUtc =
            "2026-07-31T00:21:09Z",
        ReportedAt =
            "2026-07-30",
        EvidenceType =
            "interactive-user-report-not-automated-emulator-capture",
        VisibleFastEntryLoaded = true,
        VisibleFastEntryCrashed = false,
        DormantFastEntryTested = false,
        PriorExactVisibleCount89Loaded = true,
        SixteenCompleteNativeT12CloneRowsRuntimeProven = true,
        SixteenVisibleNativeT12FacesRuntimeProven = true,
        ExactPlus1000SectorRelocationRuntimeProven = true,
        ExactScope =
            "Tree Tops count97 visible candidate only: sixteen complete byte-identical native-T12 rows T81-T96, zero external page/global-repack patches, sixteen distinct native T12 faces mapped one-to-one, +0xB80 table growth, and exact +0x1000 structural relocation.",
        ArbitraryOrCrossLevelTexturePayloadsProven = false,
        TexturePageAllocationOrRepackingProven = false,
        SixteenAppendedRowsProven = true,
        ThirtyOneOrMoreAppendedRowsProven = false,
        FiftySlotsProven = false,
        PersistenceAndBuildSafetyIntegrationProven = false,
        NormalCreateBinProven = false
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
        TableGrowthBytes =
            $"0x{ExpectedTableGrowthBytes:X}",
        NativeZeroTailBytes =
            $"0x{ExpectedSourceZeroTailBytes:X}",
        AdditionalBytesNeeded =
            $"0x{ExpectedAdditionalBytesNeeded:X}",
        SectorGrowthBytes =
            $"0x{ExpectedSectorGrowthBytes:X}",
        Writer = capacity.ExecutableWriter,
        Policy =
            capacity.StructuralGrowthPolicy.ToString()
    },
    StructuralReadback = new
    {
        WAD =
            $"0x{visiblePlan.OriginalWadSize:X}->0x{visiblePlan.ExpandedWadSize:X}",
        ExecutableLba =
            $"{visiblePlan.OriginalExecutableLba}->{visiblePlan.RelocatedExecutableLba}",
        TargetEntryWadOffset =
            $"0x{visiblePlan.OriginalTargetEntryWadOffset:X}->0x{visiblePlan.RelocatedTargetEntryWadOffset:X}",
        TargetEntryBytes =
            $"0x{ExpectedTargetEntryBytes:X}->0x{ExpectedExpandedTargetEntryBytes:X}",
        LevelDataWadOffset =
            $"0x{visiblePlan.OriginalLevelDataWadOffset:X}->0x{visiblePlan.RelocatedLevelDataWadOffset:X}",
        LevelDataBytes =
            $"0x{visiblePlan.Append.Patch.SourceByteLength:X}->0x{visiblePlan.Append.Patch.OutputByteLength:X}",
        TextureComponentBytes =
            $"0x{visiblePlan.Append.SourceTextureComponentByteLength:X}->0x{visiblePlan.Append.OutputTextureComponentByteLength:X}",
        UsedBytes =
            $"0x{visiblePlan.Append.SourceUsedByteLength:X}->0x{visiblePlan.Append.OutputUsedByteLength:X}",
        ZeroTailBytes =
            $"0x{visiblePlan.Append.SourceZeroTailByteCount:X}->0x{visiblePlan.Append.OutputZeroTailByteCount:X}",
        SceneEnd =
            $"0x{ExpectedSixteenRowSceneEnd:X8}",
        PolygonBufferMargin =
            $"0x{ExpectedSixteenRowPolygonMargin:X}",
        NestedHeaderReadbackVerified = true,
        LaterWadEntriesShiftedExactly = true,
        ExecutableRootRecordReadbackVerified = true,
        DormantShiftedSuffixByteIdentical = true,
        VisibleShiftedSuffixPreservedExceptSixteenSourceBoundFaceBytes = true
    },
    TextureRows = assignments.Select(item =>
        new
        {
            TextureId = item.TargetTextureId,
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
            Source = "T12",
            Target =
                $"T{item.TargetTextureId}",
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
    PageAndOverrideBoundary = new
    {
        ExternalTexturePagePatches = 0,
        GlobalTexturePageRepack = false,
        OriginalRowOverrides = 0,
        NativeAnimationRowOverrides = false,
        NativeT3T5T9T11Overrides = false,
        SourceAndDormantTexturePagesSha256 =
            dormantReadback.TexturePagesSha256,
        SourceAndVisibleTexturePagesSha256 =
            visibleReadback.TexturePagesSha256
    },
    Dormant = new
    {
        dormantExport.OutputImagePath,
        dormantExport.OutputCuePath,
        dormantExport.OutputImageSha256,
        dormantFast.ImagePath,
        dormantFast.CuePath,
        dormantFast.Sha256,
        dormantFast.ControlByteDifferences,
        TerrainFacePatches = 0,
        ExactFinalReadback = true
    },
    Visible = new
    {
        visibleExport.OutputImagePath,
        visibleExport.OutputCuePath,
        visibleExport.OutputImageSha256,
        visibleFast.ImagePath,
        visibleFast.CuePath,
        visibleFast.Sha256,
        visibleFast.ControlByteDifferences,
        TerrainFacePatches =
            AppendedRowCount,
        ExactFinalReadback = true
    },
    Comparison = new
    {
        DormantToVisibleBinByteDifferences =
            AppendedRowCount,
        DormantFastToVisibleFastByteDifferences =
            AppendedRowCount,
        EachControlToFastEntryByteDifferences = 8
    },
    RuntimeBoundary =
        "The visible count97 fast-entry CUE loaded without a crash. This proves only the exact sixteen-T12-clone/sixteen-face/+0xB80-table/+0x1000-relocation recipe; the dormant count97 control, 31+ rows, arbitrary payloads, and normal Create BIN remain unproven."
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
    # Tree Tops count97 sixteen-row sector-relocation runtime checklist

    Research only. This is the next capacity escalation after the exact visible count89/eight-row sector-relocation recipe loaded. It installs exactly sixteen complete T12-clone rows (T81-T96), not the old 46-row batch.

    ## Proven prerequisites

    - Genuine count83/two-row fast-entry CUE: user reported loaded on 2026-07-30.
    - Genuine count85/four-row fast-entry CUE: user reported loaded on 2026-07-30.
    - Genuine count86/five-row dormant and visible fast-entry CUEs: user reported both loaded on 2026-07-30.
    - Genuine count89/eight-row visible fast-entry CUE: user reported loaded on 2026-07-30. Its separate dormant control remains untested.

    ## Count97 result

    - `{Path.GetFileName(visibleFast.CuePath)}`: user reported Tree Tops loaded without a crash on 2026-07-30.
    - `{Path.GetFileName(dormantFast.CuePath)}`: optional dormant-control result pending.

    ## FAST-ENTRY level-select code — repeat for every CUE

    1. Open Inventory with **Select**.
    2. Enter **R1, R2, L1, L2, R1, L1, R2, L2**.
    3. Press **Right**, then **Triangle** to enter Tree Tops.
    4. This code only works in these **FAST-ENTRY diagnostics**.

    Inventory with Select; R1,R2,L1,L2,R1,L1,R2,L2; then Right, Triangle to Tree Tops. This only works in FAST-ENTRY diagnostics.

    ## Runtime test order

    1. Fully stop the currently running game and cold-boot `{Path.GetFileName(visibleFast.CuePath)}`.
    2. Use the FAST-ENTRY level-select code printed above.
    3. Confirm Tree Tops loads, its music continues, Spyro can move, and nearby actors behave normally.
    4. The sixteen selected native T12 faces intentionally look unchanged: they now reference byte-identical T81-T96 clones. The signal is loading and stable gameplay, not a visual color change.
    5. If the image freezes, note whether the screen blinks and whether music begins. Stop there.
    6. If the optional dormant control is tested later, cold-boot `{Path.GetFileName(dormantFast.CuePath)}` and repeat the complete FAST-ENTRY sequence and observations.

    ## Exact construction

    - Texture count: 81 -> 97.
    - Texture rows: exactly sixteen appended complete T12 clones.
    - Table growth: +0x{ExpectedTableGrowthBytes:X}.
    - Structural growth: +0x{ExpectedSectorGrowthBytes:X} to level data, target entry, and WAD.
    - Executable LBA: {ExpectedOriginalExecutableLba} -> {ExpectedRelocatedExecutableLba}.
    - Texture-page/global-repack writes: zero.
    - Dormant/visible difference: exactly sixteen terrain texture bytes.
    - Fast-entry difference from its corresponding control: exactly eight guarded executable bytes.

    The reported visible pass proves only this exact sixteen-row Tree Tops recipe. It does not prove the separate count97 dormant control, 31 or more appended rows, arbitrary or cross-level texture payloads, the requested 50-slot capacity, persistence/Build Safety integration, or normal Create BIN.
    """;
await File.WriteAllTextAsync(
    checklistPath,
    checklist + Environment.NewLine);

Console.WriteLine(
    "Tree Tops count97 sixteen-row sector-relocation diagnostics: PASSED OFFLINE; VISIBLE FAST-ENTRY REPORTED LOADED WITHOUT CRASH");
Console.WriteLine(
    $"- Source: {sourceImagePath}");
Console.WriteLine(
    $"- Rows: exactly T81-T96, each a complete byte-identical native T12 clone.");
Console.WriteLine(
    $"- Structural growth: table +0x{ExpectedTableGrowthBytes:X}, WAD +0x{ExpectedSectorGrowthBytes:X}, executable LBA {ExpectedOriginalExecutableLba}->{ExpectedRelocatedExecutableLba}.");
Console.WriteLine(
    "- External/page/global-repack patches: 0.");
Console.WriteLine(
    $"- Dormant fast-entry CUE: {dormantFast.CuePath}");
Console.WriteLine(
    $"- Dormant fast-entry SHA: {dormantFast.Sha256}");
Console.WriteLine(
    $"- Visible fast-entry CUE: {visibleFast.CuePath}");
Console.WriteLine(
    $"- Visible fast-entry SHA: {visibleFast.Sha256}");
Console.WriteLine(
    $"- Proof: {proofPath}");
Console.WriteLine(
    $"- Checklist: {checklistPath}");
Console.WriteLine(
    "- FAST-ENTRY checklist (repeat for every CUE): Inventory with Select; R1,R2,L1,L2,R1,L1,R2,L2; then Right, Triangle to Tree Tops. This only works in FAST-ENTRY diagnostics.");
Console.WriteLine(
    "- Runtime scope: only the exact visible 16-row/16-face/+0xB80-table/+0x1000-relocation recipe is proven; dormant count97, 31+, arbitrary payloads, 50 slots, persistence/Build Safety, and normal Create BIN remain unproven.");

void AssertRetailSourcePreserved()
{
    Assert(
        FileLength(sourceImagePath) ==
            sourceLength &&
        File.GetLastWriteTimeUtc(sourcePhysicalPath) ==
            sourceWriteTime &&
        Sha256File(sourceImagePath).Equals(
            sourceSha256,
            StringComparison.OrdinalIgnoreCase),
        "The count97 diagnostic changed the immutable retail source.");
}

void ValidateStructuralPlan(
    NativeTerrainTextureRecordSectorRelocationPlan plan,
    int expectedFacePatchCount)
{
    NativeTerrainTextureRecordAppendPlan append =
        plan.Append;
    Assert(
        plan.SourceImageSha256.Equals(
            sourceSha256,
            StringComparison.OrdinalIgnoreCase) &&
        plan.TargetLevelKey.Equals(
            LevelCatalog.NormalizeKey(level.Key),
            StringComparison.OrdinalIgnoreCase) &&
        plan.TargetWadEntry ==
            ExpectedWadEntry &&
        plan.SectorGrowthBytes ==
            ExpectedSectorGrowthBytes &&
        plan.OriginalWadSize ==
            ExpectedOriginalWadSize &&
        plan.ExpandedWadSize ==
            ExpectedExpandedWadSize &&
        plan.OriginalExecutableLba ==
            ExpectedOriginalExecutableLba &&
        plan.RelocatedExecutableLba ==
            ExpectedRelocatedExecutableLba &&
        plan.OriginalTargetEntryWadOffset ==
            ExpectedTargetEntryWadOffset &&
        plan.RelocatedTargetEntryWadOffset ==
            ExpectedTargetEntryWadOffset &&
        plan.OriginalLevelDataWadOffset ==
            ExpectedLevelDataWadOffset &&
        plan.RelocatedLevelDataWadOffset ==
            ExpectedLevelDataWadOffset &&
        plan.AppendSourceBindingVerified &&
        plan.ExistingLevelDataPatchesConsumed &&
        plan.ExternalPatchPreimagesVerified &&
        plan.RelocationOffsetsVerified &&
        plan.RequiresTexturePageProof &&
        plan.RequiresDuckStationRuntimeProof &&
        plan.RelocatedExternalPatches.Count == 0 &&
        plan.StructuralGrowthPolicy ==
            NativeTerrainTextureStructuralGrowthPolicy
                .NormalChecked &&
        !plan.StaticResearchOnly,
        "The count97 sector relocation changed its source, WAD, entry, LBA, policy, or no-page-patch contract.");
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
                .NormalChecked &&
        !append.StaticResearchOnly &&
        append.ResolvedRecords.Count ==
            AppendedRowCount &&
        append.RebasedPatches.Count ==
            expectedFacePatchCount &&
        plan.RelocatedLevelDataPatchProofs.Count ==
            expectedFacePatchCount,
        "The count97 append changed its exact row, component, suffix, tail, or face-rebase contract.");
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
        $"A count97 suffix component did not shift by +0x{ExpectedTableGrowthBytes:X} byte-identically.");
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
        "The count97 plan did not assign exactly T81-T96 as complete native T12 clones.");
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
            $"Native Tree Tops T{textureId} was overridden while building count97.");
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
        ExpectedSixteenRowSceneEnd ==
            0x80187074 &&
        ExpectedSixteenRowPolygonMargin ==
            0xB3C &&
        ExpectedSixteenRowSceneEnd <
            FixedLowerPolygonBuffer,
        "The count97 scene-end/polygon-buffer margin changed.");
}

void VerifyExportFlags(
    NativeTerrainTextureRecordSectorRelocationExportResult
        export,
    string label)
{
    Assert(
        File.Exists(export.OutputImagePath) &&
        File.Exists(export.OutputCuePath) &&
        export.SourceImagePreserved &&
        export.RelocatedLevelDataReadbackVerified &&
        export.RelocatedExternalPatchReadbackVerified &&
        export.RuntimeTargetReadbackVerified &&
        export.AtomicRenameCompleted,
        $"The {label} export omitted an atomic final-BIN/readback proof.");
}

ArtifactReadback VerifyFinalArtifact(
    string imagePath,
    NativeTerrainTextureRecordSectorRelocationPlan plan,
    IReadOnlyList<FaceExpectation> expectedFaces)
{
    DiscLayoutInfo sourceLayout =
        DetectLayout(sourceImagePath);
    DiscLayoutInfo outputLayout =
        DetectLayout(imagePath);
    Assert(
        sourceLayout.SectorSize ==
            outputLayout.SectorSize &&
        sourceLayout.UserOffset ==
            outputLayout.UserOffset &&
        FileLength(imagePath) ==
            sourceLength,
        "The count97 final image changed the disc layout or physical image length.");

    RootFileRecord sourceWad =
        ReadRootFiles(
            sourceImagePath,
            sourceLayout)
            .Single(IsWadRecord);
    RootFileRecord outputWad =
        ReadRootFiles(
            imagePath,
            outputLayout)
            .Single(IsWadRecord);
    RootFileRecord sourceExecutable =
        ReadRootFiles(
            sourceImagePath,
            sourceLayout)
            .Single(IsExecutableRecord);
    RootFileRecord outputExecutable =
        ReadRootFiles(
            imagePath,
            outputLayout)
            .Single(IsExecutableRecord);
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
            outputExecutable.Size &&
        sourceExecutable.Name.Equals(
            outputExecutable.Name,
            StringComparison.OrdinalIgnoreCase),
        "The final ISO root did not read back exact WAD growth and executable relocation.");

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
    Assert(
        sourceEntries.Length ==
            outputEntries.Length,
        "The final WAD header changed its entry count.");
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
        "The final WAD entry 52 header did not read back exact +0x1000 growth.");
    foreach (ArchiveRecord sourceEntry in
             sourceEntries)
    {
        ArchiveRecord outputEntry =
            outputEntries.Single(item =>
                item.Index == sourceEntry.Index);
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
        Assert(
            outputEntry.Offset ==
                expectedOffset &&
            outputEntry.Size ==
                expectedSize,
            $"WAD entry {sourceEntry.Index} did not preserve its exact size/offset relocation.");
    }
    Assert(
        outputEntries.Max(item =>
            item.EndExclusive) ==
            ExpectedExpandedWadSize,
        "The final WAD entries do not balance to the expanded WAD size.");

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
    Assert(
        sourceSubfiles.Length ==
            outputSubfiles.Length,
        "Tree Tops' nested subfile count changed.");
    ArchiveRecord sourceLevelData =
        sourceSubfiles.Single(item =>
            item.Index == 1);
    ArchiveRecord outputLevelData =
        outputSubfiles.Single(item =>
            item.Index == 1);
    Assert(
        sourceTarget.Offset +
            sourceLevelData.Offset ==
            ExpectedLevelDataWadOffset &&
        outputTarget.Offset +
            outputLevelData.Offset ==
            ExpectedLevelDataWadOffset &&
        sourceLevelData.Size ==
            ExpectedLevelDataBytes &&
        outputLevelData.Size ==
            ExpectedOutputLevelDataBytes,
        "Tree Tops' final nested level-data header did not retain its base and exact +0x1000 length.");
    foreach (ArchiveRecord sourceSubfile in
             sourceSubfiles)
    {
        ArchiveRecord outputSubfile =
            outputSubfiles.Single(item =>
                item.Index ==
                    sourceSubfile.Index);
        long expectedOffset =
            sourceSubfile.Index != 1 &&
            sourceSubfile.Offset >=
                sourceLevelData.EndExclusive
                ? sourceSubfile.Offset +
                    ExpectedSectorGrowthBytes
                : sourceSubfile.Offset;
        long expectedSize =
            sourceSubfile.Index == 1
                ? sourceSubfile.Size +
                    ExpectedSectorGrowthBytes
                : sourceSubfile.Size;
        Assert(
            outputSubfile.Offset ==
                expectedOffset &&
            outputSubfile.Size ==
                expectedSize,
            $"Tree Tops nested subfile {sourceSubfile.Index} did not preserve its exact size/offset relocation.");
    }

    byte[] sourceBoundLevelData = ReadWadBytes(
        sourceImagePath,
        plan.OriginalLevelDataWadOffset,
        plan.Append.Patch.SourceByteLength);
    Assert(
        sourceBoundLevelData.SequenceEqual(
            plan.Append.Patch.Before),
        "The immutable retail Tree Tops level-data preimage no longer exactly matches the append plan.");
    byte[] finalLevelData = ReadWadBytes(
        imagePath,
        plan.RelocatedLevelDataWadOffset,
        plan.Append.Patch.OutputByteLength);
    Assert(
        finalLevelData.SequenceEqual(
            plan.Append.Patch.After),
        "The final level-data subfile does not exactly match the composed count97 payload.");
    int sourceToOutputSuffixByteDifferences = 0;
    HashSet<string> mappedExpectedFaces =
        new(StringComparer.OrdinalIgnoreCase);
    foreach (NativeTerrainTextureRecordShiftedComponentProof
             component in plan.Append.ShiftedComponents)
    {
        byte[] sourceBytes = ReadWadBytes(
            sourceImagePath,
            plan.OriginalLevelDataWadOffset +
                component.SourceOffset,
            component.ByteLength);
        byte[] outputBytes = ReadWadBytes(
            imagePath,
            plan.RelocatedLevelDataWadOffset +
                component.OutputOffset,
            component.ByteLength);
        byte[] expectedComposed =
            sourceBytes.ToArray();
        foreach (FaceExpectation expected in expectedFaces)
        {
            long outputRelativeOffset =
                expected.WadOffset -
                plan.RelocatedLevelDataWadOffset;
            if (outputRelativeOffset <
                    component.OutputOffset ||
                outputRelativeOffset +
                    sizeof(uint) >
                    component.OutputOffset +
                    component.ByteLength)
            {
                continue;
            }
            int componentLocalOffset = checked(
                (int)(outputRelativeOffset -
                      component.OutputOffset));
            BinaryPrimitives.WriteUInt32LittleEndian(
                expectedComposed.AsSpan(
                    componentLocalOffset,
                    sizeof(uint)),
                expected.Word);
            Assert(
                mappedExpectedFaces.Add(
                    expected.RuntimeKey),
                $"Expected face {expected.RuntimeKey} mapped into more than one shifted component.");
        }
        string sourceComponentSha256 =
            Sha256Bytes(sourceBytes);
        string expectedComposedSha256 =
            Sha256Bytes(expectedComposed);
        string outputComponentSha256 =
            Sha256Bytes(outputBytes);
        sourceToOutputSuffixByteDifferences +=
            CountByteDifferences(
                sourceBytes,
                outputBytes);
        Assert(
            outputBytes.SequenceEqual(
                expectedComposed) &&
            expectedComposedSha256.Equals(
                component.ComposedSourceSha256,
                StringComparison.OrdinalIgnoreCase) &&
            outputComponentSha256.Equals(
                component.OutputSha256,
                StringComparison.OrdinalIgnoreCase),
            $"Shifted suffix component {component.Name} failed composed/final readback: " +
            $"source offset 0x{component.SourceOffset:X}, output offset 0x{component.OutputOffset:X}, " +
            $"length 0x{component.ByteLength:X}, raw source {sourceComponentSha256}, " +
            $"expected composed {expectedComposedSha256}, raw output {outputComponentSha256}, planned source {component.ComposedSourceSha256}, " +
            $"planned output {component.OutputSha256}.");
    }
    Assert(
        mappedExpectedFaces.Count ==
            expectedFaces.Count,
        $"Only {mappedExpectedFaces.Count} of {expectedFaces.Count} expected face word(s) mapped into the shifted-component chain.");
    int expectedSuffixByteDifferences =
        expectedFaces.Sum(expected =>
        {
            FaceAssignment source =
                assignments.Single(item =>
                    item.RuntimeKey.Equals(
                        expected.RuntimeKey,
                        StringComparison.OrdinalIgnoreCase));
            return CountByteDifferences(
                UInt32Bytes(source.SourceFaceWord),
                UInt32Bytes(expected.Word));
        });
    Assert(
        sourceToOutputSuffixByteDifferences ==
            expectedSuffixByteDifferences,
        $"Shifted suffix differs from retail by {sourceToOutputSuffixByteDifferences} byte(s), expected exactly {expectedSuffixByteDifferences} selected face byte(s).");

    ArchiveRecord sourceTexturePages =
        sourceSubfiles.Single(item =>
            item.Index == 0);
    ArchiveRecord outputTexturePages =
        outputSubfiles.Single(item =>
            item.Index == 0);
    Assert(
        sourceTexturePages.Size ==
            outputTexturePages.Size,
        "Tree Tops texture-pages subfile size changed.");
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
        "The count97 diagnostic changed Tree Tops texture-page bytes despite declaring no page/global repack.");

    IReadOnlyList<TerrainTextureSlot> slots =
        TerrainPatchExporter.InspectTextureSlots(
            imagePath,
            level);
    Assert(
        slots.Count ==
            OutputTextureCount &&
        slots[^1].TextureId ==
            OutputTextureCount - 1,
        $"The final count97 decoder found {slots.Count} texture rows.");
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
            $"Final T{textureId} did not decode as the same native topology as T12.");
    }
    foreach (FaceExpectation expected in
             expectedFaces)
    {
        uint actual =
            BinaryPrimitives.ReadUInt32LittleEndian(
                ReadWadBytes(
                    imagePath,
                    expected.WadOffset,
                    sizeof(uint)));
        Assert(
            actual == expected.Word,
            $"Final {expected.RuntimeKey} word at WAD 0x{expected.WadOffset:X} is 0x{actual:X8}, expected 0x{expected.Word:X8}.");
    }
    NativeTerrainTextureRuntimeControlAudit runtime =
        NativeTerrainTextureRuntimeControlScanner.Inspect(
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
        "The final count97 image failed runtime-target readback for T81-T96.");
    return new ArtifactReadback(
        sourceTexturePagesSha256,
        outputTexturePagesSha256,
        sourceWad.Size,
        outputWad.Size,
        sourceExecutable.Lba,
        outputExecutable.Lba);
}

async Task<FastEntryProof> BuildFastEntryCloneAsync(
    string controlImagePath,
    string controlCuePath,
    string outputPrefix,
    NativeTerrainTextureRecordSectorRelocationPlan plan,
    IReadOnlyList<FaceExpectation> expectedFaces)
{
    string outputImagePath =
        outputPrefix + ".bin";
    string outputCuePath =
        outputPrefix + ".cue";
    if (File.Exists(outputImagePath))
        File.Delete(outputImagePath);
    if (File.Exists(outputCuePath))
        File.Delete(outputCuePath);
    await Task.Run(() =>
        File.Copy(
            controlImagePath,
            outputImagePath,
            overwrite: true));
    await File.WriteAllTextAsync(
        outputCuePath,
        BuildCueTextForImage(
            controlCuePath,
            Path.GetFileName(outputImagePath)),
        Encoding.ASCII);
    MobySourcePatch fastEntryPatch =
        TestLevelWarpPatch
            .ApplyToDisposableDiagnosticImage(
                outputImagePath,
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
        "Guarded Tree Tops fast-entry patch changed.");
    int controlDifferences =
        CountFileByteDifferences(
            controlImagePath,
            outputImagePath);
    Assert(
        controlDifferences == 8,
        $"Fast-entry clone differs from its count97 control by {controlDifferences} byte(s), expected exactly eight guarded executable bytes.");
    _ = VerifyFinalArtifact(
        outputImagePath,
        plan,
        expectedFaces);
    return new FastEntryProof(
        outputImagePath,
        outputCuePath,
        Sha256File(outputImagePath),
        controlDifferences);
}

static bool IsWadRecord(RootFileRecord record) =>
    record.Name.Equals(
        "WAD.WAD",
        StringComparison.OrdinalIgnoreCase) ||
    record.Name.Equals(
        "WAD",
        StringComparison.OrdinalIgnoreCase);

static bool IsExecutableRecord(
    RootFileRecord record)
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

static IReadOnlyList<RootFileRecord> ReadRootFiles(
    string imagePath,
    DiscLayoutInfo layout)
{
    byte[] root = ReadDiscBytes(
        imagePath,
        layout,
        layout.RootExtent,
        0,
        layout.RootLength);
    List<RootFileRecord> records = [];
    int offset = 0;
    while (offset < root.Length)
    {
        int recordLength = root[offset];
        if (recordLength == 0)
        {
            offset =
                ((offset / 2048) + 1) * 2048;
            continue;
        }
        if (recordLength < 34 ||
            offset + recordLength >
                root.Length)
        {
            break;
        }
        int nameLength = root[offset + 32];
        string name = Encoding.ASCII.GetString(
            root,
            offset + 33,
            nameLength);
        if (name is not "\0" and not "\u0001")
        {
            name = name.Replace(
                ";1",
                "",
                StringComparison.OrdinalIgnoreCase);
            records.Add(
                new RootFileRecord(
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

static DiscLayoutInfo DetectLayout(
    string imagePath)
{
    using FileStream stream =
        File.OpenRead(imagePath);
    byte[] pvd = new byte[2048];
    foreach ((int sectorSize, int userOffset) in
             new[]
             {
                 (2048, 0),
                 (2352, 24),
                 (2336, 8)
             })
    {
        long offset =
            (16L * sectorSize) + userOffset;
        if (offset + pvd.Length >
            stream.Length)
        {
            continue;
        }
        stream.Position = offset;
        stream.ReadExactly(pvd);
        if (pvd[0] == 1 &&
            Encoding.ASCII.GetString(
                pvd,
                1,
                5) == "CD001")
        {
            return new DiscLayoutInfo(
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
    }
    throw new InvalidDataException(
        "Could not detect the diagnostic image's ISO9660 layout.");
}

static byte[] ReadDiscBytes(
    string imagePath,
    DiscLayoutInfo layout,
    int fileLba,
    long fileOffset,
    int length)
{
    byte[] result = new byte[length];
    using FileStream stream =
        File.OpenRead(imagePath);
    int written = 0;
    long absolute = fileOffset;
    while (written < length)
    {
        int sectorOffset =
            checked((int)(absolute % 2048));
        int sector =
            fileLba +
            checked((int)(absolute / 2048));
        int count = Math.Min(
            length - written,
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
        absolute += count;
    }
    return result;
}

static byte[] ReadWadBytes(
    string imagePath,
    long wadOffset,
    int length)
{
    DiscLayoutInfo layout =
        DetectLayout(imagePath);
    return ReadDiscBytes(
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
        $"T{textureId} is outside count {textureCount}.");
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
        $"T{textureId} is outside count {textureCount}.");
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
    byte[] bytes = new byte[sizeof(uint)];
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
        int firstRead = first.Read(
            firstBuffer,
            0,
            firstBuffer.Length);
        int secondRead = second.Read(
            secondBuffer,
            0,
            secondBuffer.Length);
        Assert(
            firstRead == secondRead,
            "BIN comparison streams returned different read lengths.");
        if (firstRead == 0)
            return differences;
        differences += CountByteDifferences(
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
    string current = Path.GetFullPath(
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

static string Sha256File(string path)
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

static long FileLength(string path)
{
    using FileStream stream =
        File.OpenRead(path);
    return stream.Length;
}

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
        throw new InvalidOperationException(message);
}

internal sealed record FaceAssignment(
    string RuntimeKey,
    int TargetTextureId,
    long SourceFaceWordWadOffset,
    uint SourceFaceWord,
    long OutputFaceWordWadOffset,
    uint OutputFaceWord);

internal sealed record FaceExpectation(
    string RuntimeKey,
    long WadOffset,
    uint Word);

internal sealed record FastEntryProof(
    string ImagePath,
    string CuePath,
    string Sha256,
    int ControlByteDifferences);

internal sealed record ArtifactReadback(
    string SourceTexturePagesSha256,
    string TexturePagesSha256,
    int OriginalWadSize,
    int ExpandedWadSize,
    int OriginalExecutableLba,
    int RelocatedExecutableLba);

internal sealed record DiscLayoutInfo(
    int SectorSize,
    int UserOffset,
    int RootExtent,
    int RootLength);

internal sealed record RootFileRecord(
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
