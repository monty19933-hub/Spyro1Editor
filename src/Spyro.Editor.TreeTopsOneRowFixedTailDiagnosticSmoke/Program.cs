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
const int ExpectedLevelId = 43;
const int ExpectedWadEntry = 52;
const int SourceTextureCount = 81;
const int SourceTextureId = 12;
const int NativeControlTextureId = 80;
const int AppendedTextureId = 81;
const int Count83AppendedRows = 2;
const int Count85AppendedRows = 4;
const int ExpectedTextureComponentBytes = 0x3A40;
const int ExpectedExpandedTextureComponentBytes = 0x3AF8;
const int ExpectedLevelDataBytes = 0xA8000;
const int ExpectedSourceUsedBytes = 0xA7CAC;
const int ExpectedOutputUsedBytes = 0xA7D64;
const int ExpectedSourceZeroTailBytes = 0x354;
const int ExpectedOutputZeroTailBytes = 0x29C;
const long ExpectedTargetEntryWadOffset = 0x3D88000;
const long ExpectedSourceFaceStartWadOffset = 0x3E674E4;
const long ExpectedSourceFaceWordWadOffset =
    ExpectedSourceFaceStartWadOffset + 8;
const long ExpectedOutputFaceStartWadOffset =
    ExpectedSourceFaceStartWadOffset +
    NativeTerrainTextureRecordAppendBuilder.RecordGrowthBytes;
const long ExpectedOutputFaceWordWadOffset =
    ExpectedSourceFaceWordWadOffset +
    NativeTerrainTextureRecordAppendBuilder.RecordGrowthBytes;
const uint ExpectedSourceFaceWord = 0xDE6E090C;
const uint ExpectedNativeControlFaceWord = 0xDE6E0950;
const uint ExpectedAppendedFaceWord = 0xDE6E0951;
const uint RetailSceneBase = 0x8016E4F4;
const int RetailSceneSize = 0x18000;
const uint RetailSceneEnd = RetailSceneBase + RetailSceneSize;
const uint FixedLowerPolygonBuffer = 0x80187BB0;
const uint ExpectedOneRowSceneEnd =
    RetailSceneEnd +
    NativeTerrainTextureRecordAppendBuilder.RecordGrowthBytes;
const uint ExpectedOneRowPolygonMargin =
    FixedLowerPolygonBuffer - ExpectedOneRowSceneEnd;
const uint ExpectedTwoRowSceneEnd =
    RetailSceneEnd +
    (Count83AppendedRows *
     NativeTerrainTextureRecordAppendBuilder.RecordGrowthBytes);
const uint ExpectedTwoRowPolygonMargin =
    FixedLowerPolygonBuffer - ExpectedTwoRowSceneEnd;
const uint ExpectedFourRowSceneEnd =
    RetailSceneEnd +
    (Count85AppendedRows *
     NativeTerrainTextureRecordAppendBuilder.RecordGrowthBytes);
const uint ExpectedFourRowPolygonMargin =
    FixedLowerPolygonBuffer - ExpectedFourRowSceneEnd;
const string ExpectedTextureComponentSha256 =
    "9E0E9BD9AFE845C882B6EF7CDE41F421898C7E9EA2F041CEACF7371F1971858A";
const string ExpectedLevelDataSha256 =
    "BCA21FDF3FDEB0D571DA93267EFCE9DECB209F227BD7F1A4A264E2CE5132046C";
const string RuntimeKey = "1:32:hp";
const string AllLevelsRetailCode =
    "Square, Square, Circle, Square, Left, Right, Left, Right, Circle, Up, Right, Down";

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
    "treetops-count82-source-search.json");
string wadAnalysisPath = Path.Combine(
    outputRoot,
    "source-bound-wad-analysis.json");
string count82Prefix = Path.Combine(
    outputRoot,
    "treetops-COUNT82-ONE-ROW-T81-CLONE-T12-FACE-1-32-HP-STATIC-DIAGNOSTIC");
string count82FastPrefix = count82Prefix + "-FAST-ENTRY";
string count83Prefix = Path.Combine(
    outputRoot,
    "treetops-COUNT83-TWO-ROWS-T81-T82-CLONE-T12-TWO-VISIBLE-FACES-FIXED-TAIL-DIAGNOSTIC");
string count83FastPrefix = count83Prefix + "-FAST-ENTRY";
string count85Prefix = Path.Combine(
    outputRoot,
    "treetops-COUNT85-FOUR-ROWS-T81-T84-CLONE-T12-FOUR-VISIBLE-FACES-FIXED-TAIL-DIAGNOSTIC");
string count85FastPrefix = count85Prefix + "-FAST-ENTRY";
string dormantCount82Prefix = Path.Combine(
    outputRoot,
    "treetops-COUNT82-ONE-ROW-T81-CLONE-T12-UNREFERENCED-STRUCTURAL-CONTROL");
string dormantCount82FastPrefix =
    dormantCount82Prefix + "-FAST-ENTRY";
string nativeControlPrefix = Path.Combine(
    outputRoot,
    "treetops-NATIVE-T80-FACE-1-32-HP-ONE-BYTE-CONTROL");
string nativeControlFastPrefix =
    nativeControlPrefix + "-FAST-ENTRY";
string count82EditsPath =
    count82Prefix + ".terrain-edits.json";
string count83EditsPath =
    count83Prefix + ".terrain-edits.json";
string count85EditsPath =
    count85Prefix + ".terrain-edits.json";
string nativeControlEditsPath =
    nativeControlPrefix + ".terrain-edits.json";
string proofPath = Path.Combine(
    outputRoot,
    "tree-tops-count82-one-row-fixed-tail-diagnostic-proof.json");
string checklistPath = Path.Combine(
    outputRoot,
    "tree-tops-count82-one-row-runtime-checklist.md");

Directory.CreateDirectory(outputRoot);
DeleteOutputs(
    count82Prefix,
    count82FastPrefix,
    count83Prefix,
    count83FastPrefix,
    count85Prefix,
    count85FastPrefix,
    dormantCount82Prefix,
    dormantCount82FastPrefix,
    nativeControlPrefix,
    nativeControlFastPrefix);
foreach (string stale in new[]
         {
             count82EditsPath,
             count83EditsPath,
             count85EditsPath,
             nativeControlEditsPath,
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
    $"Missing Tree Tops editor overlay: {overlayPath}");
await WadAnalysisBuilder.EnsureCompatibleAsync(
    sourceImagePath,
    wadAnalysisPath);
string sourcePhysicalPath = ResolvePhysicalPath(sourceImagePath);
string sourceSha256 = Sha256File(sourceImagePath);
long sourceLength = FileLength(sourceImagePath);
DateTime sourceWriteTime =
    File.GetLastWriteTimeUtc(sourcePhysicalPath);
Assert(
    sourceSha256.Equals(
        ExpectedSourceSha256,
        StringComparison.OrdinalIgnoreCase) &&
    sourceLength == ExpectedSourceLength,
    "Tree Tops one-row diagnostics are guarded to the exact clean USA retail BIN.");

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
    "Tree Tops texture-table or level-data source binding changed.");

NativeTerrainTextureRecordAppendCapacity capacity =
    NativeTerrainTextureRecordAppendBuilder.InspectCapacity(
        sourceImagePath,
        level);
Assert(
    capacity.SourceTextureCount == SourceTextureCount &&
    capacity.RequestedRecordCount == 1 &&
    capacity.VerifiedLevelDataZeroTailBytes ==
        ExpectedSourceZeroTailBytes &&
    capacity.RequestedRecordGrowthBytes ==
        NativeTerrainTextureRecordAppendBuilder.RecordGrowthBytes &&
    capacity.AdditionalBytesNeededForRequestedRecords == 0 &&
    capacity.CanAppendInsideCurrentSubfile &&
    capacity.HasExecutableWriter &&
    capacity.ExecutableWriter.Equals(
        "FixedTail",
        StringComparison.Ordinal) &&
    capacity.LevelDataByteLength == ExpectedLevelDataBytes,
    "Tree Tops no longer exposes the exact one-row FixedTail capacity.");

NativeTerrainTextureRuntimeControlAudit runtime =
    NativeTerrainTextureRuntimeControlScanner.Inspect(
        sourceImagePath,
        level);
Assert(
    runtime.Complete &&
    runtime.TextureCount == SourceTextureCount &&
    runtime.IsRuntimePersistentTarget(SourceTextureId) &&
    runtime.IsRuntimePersistentTarget(NativeControlTextureId) &&
    !runtime.ControlledTextureIds.Contains(SourceTextureId) &&
    !runtime.AnimationSourceTextureIds.Contains(SourceTextureId) &&
    !runtime.ControlledTextureIds.Contains(NativeControlTextureId) &&
    !runtime.AnimationSourceTextureIds.Contains(
        NativeControlTextureId),
    "T12 or native control T80 is no longer a runtime-static Tree Tops texture row.");

GeometryCandidate referenceGeometry =
    GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
TerrainPolygon referenceFace =
    referenceGeometry.Polygons.Single(face =>
        face.RuntimeKey.Equals(
            RuntimeKey,
            StringComparison.OrdinalIgnoreCase));
Assert(
    referenceFace.OriginalTextureId == SourceTextureId &&
    referenceFace.SectorIndex == 1 &&
    referenceFace.FaceIndex == 32 &&
    referenceFace.FaceOffset == ExpectedSourceFaceStartWadOffset &&
    referenceFace.HasCompleteNativeHighPolyFacePayload,
    "Tree Tops exact face 1:32:hp preimage changed.");

TerrainSourceSearchResult sourceSearch =
    await TerrainSourceSearchBuilder.BuildSourceDerivedAsync(
        new SourceDerivedTerrainSourceSearchRequest(
            sourceImagePath,
            sourceSearchPath,
            level,
            referenceGeometry));
TerrainSourceSearchEntry exactSearch =
    sourceSearch.Report.Results.Single(result =>
        result.Edit.Equals(
            RuntimeKey,
            StringComparison.OrdinalIgnoreCase));
Assert(
    exactSearch.FullSectorHits.Count == 1,
    "Tree Tops face 1:32:hp no longer has one exact source-sector hit.");

TerrainPatchPlan count82OrdinaryPlan =
    await BuildOneFacePlanAsync(
        referenceGeometry,
        referenceFace,
        AppendedTextureId,
        count82EditsPath,
        count82Prefix,
        "Tree Tops count82/T81 one-row fixed-tail diagnostic",
        sourceImagePath,
        sourceCuePath,
        sourceSearchPath,
        level);
IReadOnlyList<NativeTerrainTextureRecordExistingPatch>
    count82OrdinaryPatches =
        NativeTerrainTexturePrivateRecordBatchCompiler
            .ConvertOrdinaryTerrainPlan(count82OrdinaryPlan);
NativeTerrainTextureRecordExistingPatch facePatch =
    count82OrdinaryPatches.Single();
Assert(
    facePatch.WadOffset == ExpectedSourceFaceWordWadOffset &&
    facePatch.Before.Length == sizeof(uint) &&
    facePatch.Before.SequenceEqual(
        UInt32Bytes(ExpectedSourceFaceWord)) &&
    facePatch.After.SequenceEqual(
        UInt32Bytes(ExpectedAppendedFaceWord)) &&
    CountByteDifferences(
        facePatch.Before,
        facePatch.After) == 1,
    "The count82 terrain edit is not the exact one-byte face 1:32:hp T12->T81 patch.");

bool researchBuilt =
    NativeTerrainTextureRecordAppendResearch.TryBuild(
        new NativeTerrainTextureRecordAppendResearchRequest(
            sourceImagePath,
            sourceCuePath,
            level,
            level,
            SourceTextureId),
        out NativeTerrainTextureRecordAppendResearchPlan?
            researchCandidate,
        out string researchFailure);
Assert(
    researchBuilt && researchCandidate != null,
    $"Independent same-level T12 clone fixture failed: {researchFailure}");
NativeTerrainTextureRecordAppendResearchPlan research =
    researchCandidate!;
Assert(
    research.SourceTextureCount == SourceTextureCount &&
    research.OutputTextureCount == AppendedTextureId + 1 &&
    research.SourceTextureComponentByteLength ==
        ExpectedTextureComponentBytes &&
    research.OutputTextureComponentByteLength ==
        ExpectedExpandedTextureComponentBytes &&
    research.GrowthByteCount ==
        NativeTerrainTextureRecordAppendBuilder.RecordGrowthBytes &&
    research.LevelDataByteLength == ExpectedLevelDataBytes &&
    research.SourceUsedByteLength == ExpectedSourceUsedBytes &&
    research.OutputUsedByteLength == ExpectedOutputUsedBytes &&
    research.SourceZeroTailByteCount ==
        ExpectedSourceZeroTailBytes &&
    research.OutputZeroTailByteCount ==
        ExpectedOutputZeroTailBytes &&
    research.SourceRuntimeControlsComplete &&
    research.DonorRuntimeControlsComplete &&
    research.AppendedTargetRuntimePersistent &&
    research.ExistingLowDetailRowsPreserved &&
    research.ExistingHighDetailRowsPreserved &&
    research.DonorRowsCopiedExactly &&
    research.SuffixPreservedExactly &&
    research.OutputZeroTailVerified &&
    research.OutputComponentChainReparsed,
    "The independent Tree Tops 81->82 fixed-size clone omitted a required structural proof.");

byte[] sourceT12Low = SliceLowRow(
    research.SourceLevelData,
    SourceTextureCount,
    SourceTextureId);
byte[] sourceT12High = SliceHighRow(
    research.SourceLevelData,
    SourceTextureCount,
    SourceTextureId);
byte[] appendedT81Low = SliceLowRow(
    research.OutputLevelData,
    AppendedTextureId + 1,
    AppendedTextureId);
byte[] appendedT81High = SliceHighRow(
    research.OutputLevelData,
    AppendedTextureId + 1,
    AppendedTextureId);
Assert(
    sourceT12Low.SequenceEqual(appendedT81Low) &&
    sourceT12High.SequenceEqual(appendedT81High),
    "The independent fixed-size fixture did not make T81 a byte-identical 184-byte clone of retail T12.");

NativeTerrainTexturePackedAppendRecord packedT81 = new(
    StableEditId:
        "research-only:treetops:dormant:T81:clone-native-T12",
    DonorLevelKey: level.Key,
    DonorWadEntry: level.SourceWadEntry,
    DonorTextureId: SourceTextureId,
    MaterialTemplateTextureId: SourceTextureId,
    LowDetailRow: appendedT81Low,
    HighDetailRow: appendedT81High,
    ExpectedLowDetailSha256: Sha256Bytes(appendedT81Low),
    ExpectedHighDetailSha256: Sha256Bytes(appendedT81High));
NativeTerrainTextureRecordAppendRequest dormantAppendRequest = new(
    sourceImagePath,
    level,
    binding,
    [packedT81],
    Array.Empty<NativeTerrainTextureRecordExistingPatch>());
bool dormantAppendBuilt =
    NativeTerrainTextureRecordAppendBuilder.TryBuild(
        dormantAppendRequest,
        out NativeTerrainTextureRecordAppendPlan?
            dormantAppendCandidate,
        out string dormantAppendFailure);
Assert(
    dormantAppendBuilt && dormantAppendCandidate != null,
    $"Tree Tops dormant count82 fixed-tail builder failed: {dormantAppendFailure}");
NativeTerrainTextureRecordAppendPlan dormantAppendPlan =
    dormantAppendCandidate!;
Assert(
    dormantAppendPlan.SourceTextureCount ==
        SourceTextureCount &&
    dormantAppendPlan.OutputTextureCount ==
        AppendedTextureId + 1 &&
    dormantAppendPlan.RecordCountAdded == 1 &&
    dormantAppendPlan.GrowthByteCount ==
        NativeTerrainTextureRecordAppendBuilder
            .RecordGrowthBytes &&
    dormantAppendPlan.LevelDataByteLength ==
        ExpectedLevelDataBytes &&
    dormantAppendPlan.OutputLevelDataByteLength ==
        ExpectedLevelDataBytes &&
    dormantAppendPlan.Patch.IsFixedLength &&
    dormantAppendPlan.FixedSubfileBoundaryPreserved &&
    dormantAppendPlan.ArchiveHeadersRequireNoFixup &&
    dormantAppendPlan.RuntimeTargetsPersistent &&
    dormantAppendPlan.ExistingRowsPreserved &&
    dormantAppendPlan.AppendedRowsCopiedExactly &&
    dormantAppendPlan.ShiftedComponentChainReparsed &&
    dormantAppendPlan.ShiftedTerrainSurfaceSemanticsVerified &&
    dormantAppendPlan.ZeroTailVerified &&
    dormantAppendPlan.ExistingPatchesRebasedExactly &&
    dormantAppendPlan.RebasedPatches.Count == 0 &&
    dormantAppendPlan.ResolvedRecords is
    [
        {
            AssignedTextureId: AppendedTextureId,
            DonorTextureId: SourceTextureId,
            MaterialTemplateTextureId: SourceTextureId
        }
    ] &&
    dormantAppendPlan.Patch.After.SequenceEqual(
        research.OutputLevelData),
    "The dormant Tree Tops one-row control changed its fixed-tail structure or introduced a terrain-face patch.");
Assert(
    SliceLowRow(
        dormantAppendPlan.Patch.After,
        dormantAppendPlan.OutputTextureCount,
        SourceTextureId)
        .SequenceEqual(
            SliceLowRow(
                dormantAppendPlan.Patch.After,
                dormantAppendPlan.OutputTextureCount,
                AppendedTextureId)) &&
    SliceHighRow(
        dormantAppendPlan.Patch.After,
        dormantAppendPlan.OutputTextureCount,
        SourceTextureId)
        .SequenceEqual(
            SliceHighRow(
                dormantAppendPlan.Patch.After,
                dormantAppendPlan.OutputTextureCount,
                AppendedTextureId)),
    "The dormant count82 control did not retain byte-identical T12/T81 records.");

NativeTerrainTextureRecordAppendExportResult
    dormantCount82Export =
        await NativeTerrainTextureRecordAppendBuilder
            .ExportAsync(
                dormantAppendPlan,
                sourceCuePath,
                dormantCount82Prefix);
Assert(
    dormantCount82Export.SourcePreimageVerified &&
    dormantCount82Export.ArchiveHeadersPreserved &&
    dormantCount82Export.ExactLevelDataReadbackVerified &&
    dormantCount82Export.RuntimeTargetReadbackVerified &&
    File.Exists(
        dormantCount82Export.OutputImagePath) &&
    File.Exists(
        dormantCount82Export.OutputCuePath),
    "The dormant count82 control omitted exact final-BIN/header/runtime-target readback.");
VerifyTextureCountAndFace(
    dormantCount82Export.OutputImagePath,
    level,
    AppendedTextureId + 1,
    ExpectedOutputFaceWordWadOffset,
    ExpectedSourceFaceWord);
string dormantFinalOverlayPath = Path.Combine(
    outputRoot,
    ".treetops-count82-dormant-final-bin-overlay.tmp.json");
if (File.Exists(dormantFinalOverlayPath))
    File.Delete(dormantFinalOverlayPath);
SourceSceneOverlayExporter.Export(
    dormantCount82Export.OutputImagePath,
    wadAnalysisPath,
    level,
    dormantFinalOverlayPath);
GeometryCandidate dormantFinalGeometry =
    GeometryOverlayLoader.LoadFirstCandidate(
        dormantFinalOverlayPath);
Dictionary<string, int> retailFaceTextures =
    referenceGeometry.Polygons.ToDictionary(
        face => face.RuntimeKey,
        face => face.OriginalTextureId,
        StringComparer.OrdinalIgnoreCase);
Assert(
    dormantFinalGeometry.Polygons.Count ==
        referenceGeometry.Polygons.Count &&
    dormantFinalGeometry.Polygons.All(face =>
        retailFaceTextures.TryGetValue(
            face.RuntimeKey,
            out int retailTextureId) &&
        face.OriginalTextureId == retailTextureId &&
        face.OriginalTextureId != AppendedTextureId),
    "Final dormant count82 geometry readback changed a terrain face texture ID or referenced T81.");
int dormantFinalBinTerrainFaceCount =
    dormantFinalGeometry.Polygons.Count;
File.Delete(dormantFinalOverlayPath);
IReadOnlyList<TerrainTextureSlot> dormantCount82Slots =
    TerrainPatchExporter.InspectTextureSlots(
        dormantCount82Export.OutputImagePath,
        level);
Assert(
    dormantCount82Slots[SourceTextureId]
        .CombinedTopologySignature.Equals(
            dormantCount82Slots[AppendedTextureId]
                .CombinedTopologySignature,
            StringComparison.Ordinal) &&
    dormantCount82Slots[SourceTextureId]
        .HasNormalDescriptors ==
        dormantCount82Slots[AppendedTextureId]
            .HasNormalDescriptors &&
    dormantCount82Slots[SourceTextureId]
        .HasCloseDescriptors ==
        dormantCount82Slots[AppendedTextureId]
            .HasCloseDescriptors,
    "Final dormant count82 decoder did not see T81 as the same native topology as T12.");

FastEntryProof dormantCount82Fast =
    await BuildFastEntryCloneAsync(
        dormantCount82Export.OutputImagePath,
        dormantCount82Export.OutputCuePath,
        dormantCount82FastPrefix,
        level,
        expectedTextureCount:
            AppendedTextureId + 1,
        expectedFaceWordOffset:
            ExpectedOutputFaceWordWadOffset,
        expectedFaceWord:
            ExpectedSourceFaceWord);

NativeTerrainTextureRecordAppendRequest appendRequest = new(
    sourceImagePath,
    level,
    binding,
    [packedT81],
    count82OrdinaryPatches);
bool appendBuilt =
    NativeTerrainTextureRecordAppendBuilder.TryBuild(
        appendRequest,
        out NativeTerrainTextureRecordAppendPlan? appendCandidate,
        out string appendFailure);
Assert(
    appendBuilt && appendCandidate != null,
    $"Tree Tops count82 fixed-tail builder failed: {appendFailure}");
NativeTerrainTextureRecordAppendPlan appendPlan =
    appendCandidate!;
NativeTerrainTextureRecordRebasedPatchProof rebasedFace =
    appendPlan.RebasedPatches.Single();
Assert(
    appendPlan.TargetEntryWadOffset ==
        ExpectedTargetEntryWadOffset &&
    appendPlan.LevelDataByteLength ==
        ExpectedLevelDataBytes &&
    appendPlan.OutputLevelDataByteLength ==
        ExpectedLevelDataBytes &&
    appendPlan.SourceTextureCount == SourceTextureCount &&
    appendPlan.OutputTextureCount ==
        AppendedTextureId + 1 &&
    appendPlan.SourceTextureComponentByteLength ==
        ExpectedTextureComponentBytes &&
    appendPlan.OutputTextureComponentByteLength ==
        ExpectedExpandedTextureComponentBytes &&
    appendPlan.RecordCountAdded == 1 &&
    appendPlan.GrowthByteCount ==
        NativeTerrainTextureRecordAppendBuilder.RecordGrowthBytes &&
    appendPlan.SourceUsedByteLength ==
        ExpectedSourceUsedBytes &&
    appendPlan.OutputUsedByteLength ==
        ExpectedOutputUsedBytes &&
    appendPlan.SourceZeroTailByteCount ==
        ExpectedSourceZeroTailBytes &&
    appendPlan.OutputZeroTailByteCount ==
        ExpectedOutputZeroTailBytes &&
    appendPlan.Patch.IsFixedLength &&
    appendPlan.FixedSubfileBoundaryPreserved &&
    appendPlan.ArchiveHeadersRequireNoFixup &&
    appendPlan.RuntimeTargetsPersistent &&
    appendPlan.ExistingRowsPreserved &&
    appendPlan.AppendedRowsCopiedExactly &&
    appendPlan.ShiftedComponentChainReparsed &&
    appendPlan.ShiftedTerrainSurfaceSemanticsVerified &&
    appendPlan.ZeroTailVerified &&
    appendPlan.ExistingPatchesRebasedExactly &&
    appendPlan.ResolvedRecords is
    [
        {
            AssignedTextureId: AppendedTextureId,
            DonorTextureId: SourceTextureId,
            MaterialTemplateTextureId: SourceTextureId
        }
    ] &&
    rebasedFace.SourceWadOffset ==
        ExpectedSourceFaceWordWadOffset &&
    rebasedFace.OutputWadOffset ==
        ExpectedOutputFaceWordWadOffset,
    "The Tree Tops one-row fixed-tail plan changed its exact count, size, tail, row, or face-rebase contract.");

Assert(
    ExpectedSourceFaceStartWadOffset +
        NativeTerrainTextureRecordAppendBuilder.RecordGrowthBytes ==
        ExpectedOutputFaceStartWadOffset &&
    ExpectedOutputFaceStartWadOffset -
        appendPlan.TargetEntryWadOffset == 0xDF59C &&
    ExpectedOneRowSceneEnd == 0x801865AC &&
    ExpectedOneRowPolygonMargin == 0x1604 &&
    ExpectedOneRowSceneEnd < FixedLowerPolygonBuffer,
    "The one-row scene-end/polygon-buffer boundary proof changed.");
Assert(
    SliceLowRow(
        appendPlan.Patch.After,
        appendPlan.OutputTextureCount,
        SourceTextureId)
        .SequenceEqual(
            SliceLowRow(
                appendPlan.Patch.After,
                appendPlan.OutputTextureCount,
                AppendedTextureId)) &&
    SliceHighRow(
        appendPlan.Patch.After,
        appendPlan.OutputTextureCount,
        SourceTextureId)
        .SequenceEqual(
            SliceHighRow(
                appendPlan.Patch.After,
                appendPlan.OutputTextureCount,
                AppendedTextureId)),
    "The composed count82 image did not retain byte-identical T12/T81 records.");
Assert(
    CountByteDifferences(
        research.OutputLevelData,
        appendPlan.Patch.After) == 1,
    "The count82 plan differs from the independent dormant one-row structure by more than the selected face's one texture byte.");
Assert(
    CountByteDifferences(
        dormantAppendPlan.Patch.After,
        appendPlan.Patch.After) == 1,
    "The visible and unreferenced count82 plans do not differ by exactly the selected face's one texture byte.");

NativeTerrainTextureRecordAppendExportResult count82Export =
    await NativeTerrainTextureRecordAppendBuilder.ExportAsync(
        appendPlan,
        sourceCuePath,
        count82Prefix);
Assert(
    count82Export.SourcePreimageVerified &&
    count82Export.ArchiveHeadersPreserved &&
    count82Export.ExactLevelDataReadbackVerified &&
    count82Export.RuntimeTargetReadbackVerified &&
    File.Exists(count82Export.OutputImagePath) &&
    File.Exists(count82Export.OutputCuePath),
    "The count82 diagnostic omitted exact final-BIN/header/runtime-target readback.");
VerifyTextureCountAndFace(
    count82Export.OutputImagePath,
    level,
    AppendedTextureId + 1,
    ExpectedOutputFaceWordWadOffset,
    ExpectedAppendedFaceWord);
IReadOnlyList<TerrainTextureSlot> count82Slots =
    TerrainPatchExporter.InspectTextureSlots(
        count82Export.OutputImagePath,
        level);
Assert(
    count82Slots[SourceTextureId]
        .CombinedTopologySignature.Equals(
            count82Slots[AppendedTextureId]
                .CombinedTopologySignature,
            StringComparison.Ordinal) &&
    count82Slots[SourceTextureId].HasNormalDescriptors ==
        count82Slots[AppendedTextureId]
            .HasNormalDescriptors &&
    count82Slots[SourceTextureId].HasCloseDescriptors ==
        count82Slots[AppendedTextureId]
            .HasCloseDescriptors,
    "Final count82 decoder did not see T81 as the same native topology as T12.");

FastEntryProof count82Fast =
    await BuildFastEntryCloneAsync(
        count82Export.OutputImagePath,
        count82Export.OutputCuePath,
        count82FastPrefix,
        level,
        expectedTextureCount: AppendedTextureId + 1,
        expectedFaceWordOffset:
            ExpectedOutputFaceWordWadOffset,
        expectedFaceWord:
            ExpectedAppendedFaceWord);
Assert(
    CountFileByteDifferences(
        dormantCount82Export.OutputImagePath,
        count82Export.OutputImagePath) == 1 &&
    CountFileByteDifferences(
        dormantCount82Fast.ImagePath,
        count82Fast.ImagePath) == 1,
    "The dormant and visible count82 final BINs do not differ by exactly one terrain texture byte.");

CumulativeFaceAssignment[] cumulativeFaceAssignments =
[
    new(
        "1:32:hp",
        SourceTextureId,
        81,
        0x3E674EC,
        0xDE6E090C),
    new(
        "1:33:hp",
        SourceTextureId,
        82,
        0x3E674FC,
        0xDE6FFF0C),
    new(
        "2:0:hp",
        SourceTextureId,
        83,
        0x3E67608,
        0xD63D490C),
    new(
        "2:1:hp",
        SourceTextureId,
        84,
        0x3E67618,
        0xD4DD490C)
];
GeometryCandidate cumulativeSourceGeometry =
    GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
foreach (CumulativeFaceAssignment assignment in
         cumulativeFaceAssignments)
{
    TerrainPolygon face =
        cumulativeSourceGeometry.Polygons.Single(item =>
            item.RuntimeKey.Equals(
                assignment.RuntimeKey,
                StringComparison.OrdinalIgnoreCase));
    Assert(
        face.OriginalTextureId ==
            assignment.SourceTextureId &&
        face.HasCompleteNativeHighPolyFacePayload &&
        face.FaceOffset + 8 ==
            assignment.SourceFaceWordWadOffset &&
        UInt32Bytes(assignment.SourceFaceWord)
            .SequenceEqual(
                ReadWadBytes(
                    sourceImagePath,
                    assignment.SourceFaceWordWadOffset,
                    sizeof(uint))),
        $"Tree Tops cumulative face {assignment.RuntimeKey} no longer matches its exact retail T{assignment.SourceTextureId} preimage.");
}

CumulativeFixedTailProof count83 =
    await BuildCumulativeFixedTailDiagnosticAsync(
        Count83AppendedRows,
        cumulativeFaceAssignments
            .Take(Count83AppendedRows)
            .ToArray(),
        count83EditsPath,
        count83Prefix,
        count83FastPrefix);
CumulativeFixedTailProof count85 =
    await BuildCumulativeFixedTailDiagnosticAsync(
        Count85AppendedRows,
        cumulativeFaceAssignments
            .Take(Count85AppendedRows)
            .ToArray(),
        count85EditsPath,
        count85Prefix,
        count85FastPrefix);

GeometryCandidate controlGeometry =
    GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
TerrainPolygon controlFace =
    controlGeometry.Polygons.Single(face =>
        face.RuntimeKey.Equals(
            RuntimeKey,
            StringComparison.OrdinalIgnoreCase));
TerrainPatchPlan nativeControlPlan =
    await BuildOneFacePlanAsync(
        controlGeometry,
        controlFace,
        NativeControlTextureId,
        nativeControlEditsPath,
        nativeControlPrefix,
        "Tree Tops native T80 one-byte texture control",
        sourceImagePath,
        sourceCuePath,
        sourceSearchPath,
        level);
IReadOnlyList<NativeTerrainTextureRecordExistingPatch>
    nativeControlPatches =
        NativeTerrainTexturePrivateRecordBatchCompiler
            .ConvertOrdinaryTerrainPlan(nativeControlPlan);
NativeTerrainTextureRecordExistingPatch nativeControlPatch =
    nativeControlPatches.Single();
Assert(
    nativeControlPatch.WadOffset ==
        ExpectedSourceFaceWordWadOffset &&
    nativeControlPatch.Before.SequenceEqual(
        UInt32Bytes(ExpectedSourceFaceWord)) &&
    nativeControlPatch.After.SequenceEqual(
        UInt32Bytes(ExpectedNativeControlFaceWord)) &&
    CountByteDifferences(
        nativeControlPatch.Before,
        nativeControlPatch.After) == 1,
    "The native T80 control is not the exact one-byte face 1:32:hp T12->T80 patch.");
TerrainPatchResult nativeControlExport =
    await TerrainPatchExporter.ExportAsync(
        new TerrainPatchRequest(
            sourceImagePath,
            sourceCuePath,
            nativeControlPrefix,
            level,
            RamPath: "",
            sourceSearchPath,
            nativeControlEditsPath,
            CustomTexturesPath: "",
            WriteImage: true));
Assert(
    nativeControlExport.WroteImage &&
    File.Exists(nativeControlExport.OutputImagePath) &&
    File.Exists(nativeControlExport.OutputCuePath) &&
    nativeControlExport.Plan.Patches.Count == 1,
    "The native T80 control did not write exactly one terrain patch.");
Assert(
    CountFileByteDifferences(
        sourceImagePath,
        nativeControlExport.OutputImagePath) == 1,
    "The native T80 control BIN differs from retail by more than one byte.");
VerifyTextureCountAndFace(
    nativeControlExport.OutputImagePath,
    level,
    SourceTextureCount,
    ExpectedSourceFaceWordWadOffset,
    ExpectedNativeControlFaceWord);

FastEntryProof nativeControlFast =
    await BuildFastEntryCloneAsync(
        nativeControlExport.OutputImagePath,
        nativeControlExport.OutputCuePath,
        nativeControlFastPrefix,
        level,
        expectedTextureCount: SourceTextureCount,
        expectedFaceWordOffset:
            ExpectedSourceFaceWordWadOffset,
        expectedFaceWord:
            ExpectedNativeControlFaceWord);
int nativeControlFastRetailDifferences =
    CountFileByteDifferences(
        sourceImagePath,
        nativeControlFast.ImagePath);
Assert(
    nativeControlFastRetailDifferences == 9,
    $"The native T80 fast-entry BIN differs from retail by {nativeControlFastRetailDifferences} byte(s), expected one terrain byte plus eight guarded warp bytes.");

AssertRetailSourcePreserved();

var proof = new
{
    GeneratedAtUtc = DateTimeOffset.UtcNow,
    Status =
        "COUNT82 RUNTIME REPORTED LOADING; COUNT83/COUNT85 OFFLINE STATIC AND FINAL-BIN READBACK PASSED; NEW RUNTIME TESTS PENDING",
    ResearchOnly = true,
    NormalCreateBinIntegrated = false,
    DuckStationRuntimeProven = false,
    ReportedDuckStationPrerequisites = new
    {
        ReportedAt = "2026-07-29",
        NativeT80ControlLoaded = true,
        DormantCount82Loaded = true,
        VisibleCount82T81Loaded = true,
        EvidenceScope =
            "Interactive user report; this authorizes only the next cumulative research tests and does not promote Tree Tops to normal Create BIN."
    },
    Source = new
    {
        Path = sourceImagePath,
        PhysicalPath = sourcePhysicalPath,
        Sha256 = sourceSha256,
        Length = sourceLength,
        Preserved = true
    },
    Target = new
    {
        level.Key,
        level.DisplayName,
        level.LevelId,
        level.SourceWadEntry,
        binding.ExpectedSourceTextureCount,
        binding.ExpectedTextureComponentSha256,
        binding.ExpectedLevelDataSha256,
        Face = RuntimeKey,
        SourceFaceStartWadOffset =
            $"0x{ExpectedSourceFaceStartWadOffset:X}",
        SourceFaceWordWadOffset =
            $"0x{ExpectedSourceFaceWordWadOffset:X}",
        OutputFaceStartWadOffset =
            $"0x{ExpectedOutputFaceStartWadOffset:X}",
        OutputFaceWordWadOffset =
            $"0x{ExpectedOutputFaceWordWadOffset:X}"
    },
    Count82OneRow = new
    {
        SourceTextureCount =
            appendPlan.SourceTextureCount,
        OutputTextureCount =
            appendPlan.OutputTextureCount,
        SourceTextureId,
        AppendedTextureId,
        CloneBytes =
            NativeTerrainTextureRecordAppendBuilder
                .RecordGrowthBytes,
        T81ByteIdenticalToT12 = true,
        TextureComponent =
            $"0x{appendPlan.SourceTextureComponentByteLength:X}->0x{appendPlan.OutputTextureComponentByteLength:X}",
        LevelDataBytes =
            $"0x{appendPlan.LevelDataByteLength:X}",
        UsedBytes =
            $"0x{appendPlan.SourceUsedByteLength:X}->0x{appendPlan.OutputUsedByteLength:X}",
        ZeroTailBytes =
            $"0x{appendPlan.SourceZeroTailByteCount:X}->0x{appendPlan.OutputZeroTailByteCount:X}",
        FaceWord =
            $"0x{ExpectedSourceFaceWord:X8}->0x{ExpectedAppendedFaceWord:X8}",
        FacePatchChangedByteCount = 1,
        FixedSubfileBoundaryPreserved =
            appendPlan.FixedSubfileBoundaryPreserved,
        ExactFinalBinReadbackVerified =
            count82Export.ExactLevelDataReadbackVerified,
        RuntimeTargetStaticReadbackVerified =
            count82Export.RuntimeTargetReadbackVerified,
        ControlImagePath =
            count82Export.OutputImagePath,
        ControlCuePath =
            count82Export.OutputCuePath,
        ControlSha256 =
            count82Export.OutputImageSha256,
        FastEntryImagePath =
            count82Fast.ImagePath,
        FastEntryCuePath =
            count82Fast.CuePath,
        FastEntrySha256 =
            count82Fast.Sha256,
        FastEntryControlByteDifferences =
            count82Fast.ControlByteDifferences
    },
    Count82DormantOneRow = new
    {
        SourceTextureCount =
            dormantAppendPlan.SourceTextureCount,
        OutputTextureCount =
            dormantAppendPlan.OutputTextureCount,
        SourceTextureId,
        AppendedTextureId,
        CloneBytes =
            NativeTerrainTextureRecordAppendBuilder
                .RecordGrowthBytes,
        T81ByteIdenticalToT12 = true,
        TerrainFacePatches = 0,
        NoTerrainFaceReferencesT81 = true,
        FinalBinTerrainFaceCount =
            dormantFinalBinTerrainFaceCount,
        FinalBinFaceTextureIdsPreserved = true,
        ExactIndependentStructuralFixtureMatch =
            dormantAppendPlan.Patch.After
                .SequenceEqual(
                    research.OutputLevelData),
        VisibleCandidateByteDifferences = 1,
        TextureComponent =
            $"0x{dormantAppendPlan.SourceTextureComponentByteLength:X}->0x{dormantAppendPlan.OutputTextureComponentByteLength:X}",
        LevelDataBytes =
            $"0x{dormantAppendPlan.LevelDataByteLength:X}",
        UsedBytes =
            $"0x{dormantAppendPlan.SourceUsedByteLength:X}->0x{dormantAppendPlan.OutputUsedByteLength:X}",
        ZeroTailBytes =
            $"0x{dormantAppendPlan.SourceZeroTailByteCount:X}->0x{dormantAppendPlan.OutputZeroTailByteCount:X}",
        AnchorFaceWordPreserved =
            $"0x{ExpectedSourceFaceWord:X8}",
        FixedSubfileBoundaryPreserved =
            dormantAppendPlan
                .FixedSubfileBoundaryPreserved,
        ExactFinalBinReadbackVerified =
            dormantCount82Export
                .ExactLevelDataReadbackVerified,
        RuntimeTargetStaticReadbackVerified =
            dormantCount82Export
                .RuntimeTargetReadbackVerified,
        ControlImagePath =
            dormantCount82Export.OutputImagePath,
        ControlCuePath =
            dormantCount82Export.OutputCuePath,
        ControlSha256 =
            dormantCount82Export.OutputImageSha256,
        FastEntryImagePath =
            dormantCount82Fast.ImagePath,
        FastEntryCuePath =
            dormantCount82Fast.CuePath,
        FastEntrySha256 =
            dormantCount82Fast.Sha256,
        FastEntryControlByteDifferences =
            dormantCount82Fast.ControlByteDifferences
    },
    Count83TwoRows = new
    {
        count83.AppendedRowCount,
        count83.OutputTextureCount,
        GrowthBytes =
            $"0x{count83.GrowthBytes:X}",
        LevelDataBytes =
            $"0x{ExpectedLevelDataBytes:X}",
        OutputUsedBytes =
            $"0x{count83.OutputUsedBytes:X}",
        OutputZeroTailBytes =
            $"0x{count83.OutputZeroTailBytes:X}",
        count83.SceneEnd,
        count83.PolygonBufferMargin,
        FixedTailOnly = true,
        PreinstalledRows = 0,
        CompleteByteIdenticalNativeT12Rows =
            count83.AppendedRowCount,
        OneMatchingFacePerAppendedRow = true,
        FaceAssignments =
            count83.FaceAssignments.Select(item =>
                new
                {
                    item.RuntimeKey,
                    Source =
                        $"T{item.SourceTextureId}",
                    Target =
                        $"T{item.TargetTextureId}",
                    SourceFaceWordWadOffset =
                        $"0x{item.SourceFaceWordWadOffset:X}",
                    SourceFaceWord =
                        $"0x{item.SourceFaceWord:X8}",
                    OutputFaceWordWadOffset =
                        $"0x{item.SourceFaceWordWadOffset + count83.GrowthBytes:X}",
                    OutputFaceWord =
                        $"0x{ReplaceTextureId(item.SourceFaceWord, item.TargetTextureId):X8}"
                }),
        count83.DormantFixtureVerified,
        count83.DormantToVisibleByteDifferences,
        count83.ExactFinalBinReadbackVerified,
        count83.RuntimeTargetReadbackVerified,
        count83.OutputImagePath,
        count83.OutputCuePath,
        count83.OutputImageSha256,
        count83.FastEntryImagePath,
        count83.FastEntryCuePath,
        count83.FastEntrySha256,
        count83.FastEntryControlByteDifferences
    },
    Count85FourRows = new
    {
        count85.AppendedRowCount,
        count85.OutputTextureCount,
        GrowthBytes =
            $"0x{count85.GrowthBytes:X}",
        LevelDataBytes =
            $"0x{ExpectedLevelDataBytes:X}",
        OutputUsedBytes =
            $"0x{count85.OutputUsedBytes:X}",
        OutputZeroTailBytes =
            $"0x{count85.OutputZeroTailBytes:X}",
        count85.SceneEnd,
        count85.PolygonBufferMargin,
        FixedTailOnly = true,
        PreinstalledRows = 0,
        CompleteByteIdenticalNativeT12Rows =
            count85.AppendedRowCount,
        OneMatchingFacePerAppendedRow = true,
        FaceAssignments =
            count85.FaceAssignments.Select(item =>
                new
                {
                    item.RuntimeKey,
                    Source =
                        $"T{item.SourceTextureId}",
                    Target =
                        $"T{item.TargetTextureId}",
                    SourceFaceWordWadOffset =
                        $"0x{item.SourceFaceWordWadOffset:X}",
                    SourceFaceWord =
                        $"0x{item.SourceFaceWord:X8}",
                    OutputFaceWordWadOffset =
                        $"0x{item.SourceFaceWordWadOffset + count85.GrowthBytes:X}",
                    OutputFaceWord =
                        $"0x{ReplaceTextureId(item.SourceFaceWord, item.TargetTextureId):X8}"
                }),
        count85.DormantFixtureVerified,
        count85.DormantToVisibleByteDifferences,
        count85.ExactFinalBinReadbackVerified,
        count85.RuntimeTargetReadbackVerified,
        count85.OutputImagePath,
        count85.OutputCuePath,
        count85.OutputImageSha256,
        count85.FastEntryImagePath,
        count85.FastEntryCuePath,
        count85.FastEntrySha256,
        count85.FastEntryControlByteDifferences
    },
    SceneBoundary = new
    {
        RetailSceneBase =
            $"0x{RetailSceneBase:X8}",
        RetailSceneSize =
            $"0x{RetailSceneSize:X}",
        RetailSceneEnd =
            $"0x{RetailSceneEnd:X8}",
        OneRowSceneEnd =
            $"0x{ExpectedOneRowSceneEnd:X8}",
        TwoRowSceneEnd =
            $"0x{ExpectedTwoRowSceneEnd:X8}",
        FourRowSceneEnd =
            $"0x{ExpectedFourRowSceneEnd:X8}",
        FixedLowerPolygonBuffer =
            $"0x{FixedLowerPolygonBuffer:X8}",
        OneRowRemainingMarginBytes =
            $"0x{ExpectedOneRowPolygonMargin:X}",
        TwoRowRemainingMarginBytes =
            $"0x{ExpectedTwoRowPolygonMargin:X}",
        FourRowRemainingMarginBytes =
            $"0x{ExpectedFourRowPolygonMargin:X}",
        AllBelowPolygonBuffer =
            ExpectedOneRowSceneEnd <
                FixedLowerPolygonBuffer &&
            ExpectedTwoRowSceneEnd <
                FixedLowerPolygonBuffer &&
            ExpectedFourRowSceneEnd <
                FixedLowerPolygonBuffer
    },
    NativeT80Control = new
    {
        SourceTextureCount,
        OutputTextureCount = SourceTextureCount,
        StructuralGrowthBytes = 0,
        NativeControlTextureId,
        Face = RuntimeKey,
        FaceWordWadOffset =
            $"0x{ExpectedSourceFaceWordWadOffset:X}",
        FaceWord =
            $"0x{ExpectedSourceFaceWord:X8}->0x{ExpectedNativeControlFaceWord:X8}",
        RetailBinChangedByteCount = 1,
        ControlImageByteLength =
            FileLength(
                nativeControlExport.OutputImagePath),
        MatchesRetailImageByteLength =
            FileLength(
                nativeControlExport.OutputImagePath) ==
            sourceLength,
        ControlImagePath =
            nativeControlExport.OutputImagePath,
        ControlCuePath =
            nativeControlExport.OutputCuePath,
        ControlSha256 =
            Sha256File(
                nativeControlExport.OutputImagePath),
        FastEntryImagePath =
            nativeControlFast.ImagePath,
        FastEntryCuePath =
            nativeControlFast.CuePath,
        FastEntrySha256 =
            nativeControlFast.Sha256,
        FastEntryControlByteDifferences =
            nativeControlFast.ControlByteDifferences,
        FastEntryRetailByteDifferences =
            nativeControlFastRetailDifferences
    },
    TestConvenience = new
    {
        FastEntryActivation =
            TestLevelWarpPatch.ActivationSequence,
        FastEntryTarget =
            TestLevelWarpPatch.TargetSelectionText(
                level.LevelId),
        RetailAllLevelsCode =
            AllLevelsRetailCode
    },
    Conclusions = new[]
    {
        "The count82 candidate keeps Tree Tops' WAD entry and fixed 0xA8000 level-data subfile unchanged in size.",
        "Exactly one 184-byte T81 record is appended and is byte-identical to retail Tree Tops T12.",
        "The new dormant count82 control appends that same one T81 row but changes zero terrain faces and matches the independent fixed-tail structural fixture byte-for-byte.",
        "The dormant and visible count82 final BINs differ by exactly one terrain texture byte, both before and after the identical fast-entry patch.",
        "Exactly one terrain texture byte changes on exact face 1:32:hp: T12 to T81.",
        "The loaded one-row scene end remains 0x1604 bytes below the fixed lower polygon buffer.",
        "The genuine count83 candidate appends only two complete T12 clones as T81-T82 and assigns exactly two matching T12 faces; it does not preinstall any dormant 46-row baseline.",
        "The genuine count85 candidate appends only four complete T12 clones as T81-T84 and assigns exactly four matching T12 faces; it does not preinstall any dormant 46-row baseline.",
        "Each cumulative candidate differs from an independently composed same-row dormant FixedTail fixture by exactly one face texture byte per appended row.",
        "Count83 and count85 preserve the fixed 0xA8000 level-data subfile, retain 0x154C and 0x13DC bytes respectively below the fixed lower polygon buffer, and passed exact final-BIN geometry and texture-row readback.",
        "The native T80 control changes the same face from T12 to T80 with no structural growth and exactly one retail BIN byte changed.",
        "The native T80, dormant count82, and visible count82 candidates were reported loading in DuckStation. Count83 and count85 each include a separate guarded eight-byte fast-entry clone and still require runtime evidence.",
        "Nothing in this smoke is connected to normal project persistence, Build Safety, or Create BIN."
    }
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
    # Tree Tops fixed-tail cumulative runtime checklist

    All images are disposable exact-USA research diagnostics. Tree Tops remains blocked from normal Create BIN.

    ## Confirmed prerequisite

    On 2026-07-29, the native T80 control, dormant count82 structural control, and visible count82/T81 one-face candidate were each reported loading in DuckStation. That clears the one-row discriminator only. It does not promote Tree Tops to normal Create BIN.

    ## Next test order

    1. Cold-boot `{Path.GetFileName(count83.FastEntryCuePath)}`.
    2. Start a new game and open Inventory.
    3. Enter `{TestLevelWarpPatch.ActivationSequence}`.
    4. Press `{TestLevelWarpPatch.TargetSelectionText(level.LevelId)}` to load Tree Tops.
    5. Record whether Tree Tops loads, freezes during flight, blinks, or reaches the level music.
    6. If count83 loads, cold-boot `{Path.GetFileName(count85.FastEntryCuePath)}` and repeat the same steps.
    7. Record the count85 result separately. Stop here even if it loads; do not jump to the historical 46-row candidate.

    ## Genuine count83 test

    - Source count: 81. Output count: 83.
    - It appends exactly two complete 184-byte rows: T81 and T82 are byte-identical LQ/HQ clones of native Tree Tops T12.
    - It changes exactly two matching source faces: `1:32:hp` T12->T81 and `1:33:hp` T12->T82.
    - It consumes exactly `0x170` bytes of the existing verified zero tail and does not grow the WAD entry or level-data subfile.
    - The final BIN passed exact row, face, geometry, header, and runtime-target readback.
    - The fast-entry clone differs from its verified control by exactly eight guarded executable bytes.

    ## Genuine count85 test

    - Source count: 81. Output count: 85.
    - It appends exactly four complete 184-byte rows: T81 through T84 are byte-identical LQ/HQ clones of native Tree Tops T12.
    - It changes exactly four matching source faces: `1:32:hp`, `1:33:hp`, `2:0:hp`, and `2:1:hp`, mapped one-to-one to T81 through T84.
    - It consumes exactly `0x2E0` bytes of the existing verified zero tail, leaves `0x74` zero bytes, and does not grow the WAD entry or level-data subfile.
    - The final BIN passed exact row, face, geometry, header, and runtime-target readback.
    - The fast-entry clone differs from its verified control by exactly eight guarded executable bytes.

    Neither candidate contains the earlier 46-row structure, the four animation-source diagnostic rows, a WAD relocation, or any normal-editor promotion.

    ## Retail all-level convenience code

    If you prefer a normal retail-code flow, pause the game and enter:

    `{AllLevelsRetailCode}`

    Do not use an old per-level save state for either cold-load comparison.
    """;
await File.WriteAllTextAsync(
    checklistPath,
    checklist + Environment.NewLine);

Console.WriteLine(
    "Tree Tops count82/count83/count85 fixed-tail diagnostic smoke passed.");
Console.WriteLine(
    $"- Dormant count82 CUE: {dormantCount82Export.OutputCuePath}");
Console.WriteLine(
    $"- Dormant count82 fast-entry CUE: {dormantCount82Fast.CuePath}");
Console.WriteLine(
    $"- Dormant count82 fast-entry SHA: {dormantCount82Fast.Sha256}");
Console.WriteLine(
    $"- Count82 CUE: {count82Export.OutputCuePath}");
Console.WriteLine(
    $"- Count82 fast-entry CUE: {count82Fast.CuePath}");
Console.WriteLine(
    $"- Count82 fast-entry SHA: {count82Fast.Sha256}");
Console.WriteLine(
    $"- Count83 two-row fast-entry CUE: {count83.FastEntryCuePath}");
Console.WriteLine(
    $"- Count83 two-row fast-entry SHA: {count83.FastEntrySha256}");
Console.WriteLine(
    $"- Count85 four-row fast-entry CUE: {count85.FastEntryCuePath}");
Console.WriteLine(
    $"- Count85 four-row fast-entry SHA: {count85.FastEntrySha256}");
Console.WriteLine(
    $"- Native T80 control CUE: {nativeControlExport.OutputCuePath}");
Console.WriteLine(
    $"- Native T80 fast-entry CUE: {nativeControlFast.CuePath}");
Console.WriteLine(
    $"- Native T80 fast-entry SHA: {nativeControlFast.Sha256}");
Console.WriteLine(
    $"- Scene ends: count82 0x{ExpectedOneRowSceneEnd:X8}, count83 0x{ExpectedTwoRowSceneEnd:X8}, count85 0x{ExpectedFourRowSceneEnd:X8}.");
Console.WriteLine($"- Proof: {proofPath}");
Console.WriteLine($"- Checklist: {checklistPath}");
Console.WriteLine(
    "- Count83/count85 are offline/static research only; normal Create BIN remains unchanged and their DuckStation runtime proof remains pending.");

void AssertRetailSourcePreserved()
{
    Assert(
        FileLength(sourceImagePath) == sourceLength &&
        File.GetLastWriteTimeUtc(sourcePhysicalPath) ==
            sourceWriteTime &&
        Sha256File(sourceImagePath).Equals(
            sourceSha256,
            StringComparison.OrdinalIgnoreCase),
        "Tree Tops diagnostics changed the immutable retail source.");
}

async Task<CumulativeFixedTailProof>
    BuildCumulativeFixedTailDiagnosticAsync(
        int appendedRowCount,
        IReadOnlyList<CumulativeFaceAssignment>
            assignments,
        string editsPath,
        string outputPrefix,
        string fastOutputPrefix)
{
    Assert(
        appendedRowCount is 2 or 4 &&
        assignments.Count == appendedRowCount &&
        assignments
            .Select(item => item.RuntimeKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == appendedRowCount &&
        assignments
            .Select(item => item.TargetTextureId)
            .SequenceEqual(
                Enumerable.Range(
                    SourceTextureCount,
                    appendedRowCount)) &&
        assignments.All(item =>
            item.SourceTextureId == SourceTextureId),
        "Tree Tops cumulative fixed-tail diagnostics must map one unique native-T12 face to each sequential appended row.");

    NativeTerrainTextureRecordAppendCapacity
        batchCapacity =
            NativeTerrainTextureRecordAppendBuilder
                .InspectCapacityForRecordCount(
                    sourceImagePath,
                    level,
                    appendedRowCount,
                    wadAnalysisPath);
    int expectedGrowth = checked(
        appendedRowCount *
        NativeTerrainTextureRecordAppendBuilder
            .RecordGrowthBytes);
    Assert(
        batchCapacity.SourceTextureCount ==
            SourceTextureCount &&
        batchCapacity.RequestedRecordCount ==
            appendedRowCount &&
        batchCapacity.RequestedRecordGrowthBytes ==
            expectedGrowth &&
        batchCapacity.VerifiedLevelDataZeroTailBytes ==
            ExpectedSourceZeroTailBytes &&
        batchCapacity.AdditionalBytesNeededForRequestedRecords ==
            0 &&
        batchCapacity.CanAppendInsideCurrentSubfile &&
        batchCapacity.HasExecutableWriter &&
        batchCapacity.ExecutableWriter.Equals(
            "FixedTail",
            StringComparison.Ordinal) &&
        batchCapacity.StructuralGrowthPolicy ==
            NativeTerrainTextureStructuralGrowthPolicy
                .NormalChecked &&
        !batchCapacity.StaticResearchOnly &&
        batchCapacity.LevelDataByteLength ==
            ExpectedLevelDataBytes,
        $"Tree Tops no longer exposes the exact {appendedRowCount}-row normal-checked FixedTail writer.");

    GeometryCandidate geometry =
        GeometryOverlayLoader.LoadFirstCandidate(
            overlayPath);
    TerrainPatchPlan ordinaryPlan =
        await BuildMultiFacePlanAsync(
            geometry,
            assignments,
            editsPath,
            outputPrefix,
            $"Tree Tops count{SourceTextureCount + appendedRowCount}/{appendedRowCount}-row cumulative fixed-tail diagnostic",
            sourceImagePath,
            sourceCuePath,
            sourceSearchPath,
            level);
    IReadOnlyList<NativeTerrainTextureRecordExistingPatch>
        ordinaryPatches =
            NativeTerrainTexturePrivateRecordBatchCompiler
                .ConvertOrdinaryTerrainPlan(
                    ordinaryPlan);
    Assert(
        ordinaryPatches.Count == appendedRowCount,
        $"Tree Tops count{SourceTextureCount + appendedRowCount} did not produce exactly one terrain patch per appended row.");
    foreach (CumulativeFaceAssignment assignment in
             assignments)
    {
        NativeTerrainTextureRecordExistingPatch
            patch = ordinaryPatches.Single(item =>
                item.RuntimeKey.Equals(
                    assignment.RuntimeKey,
                    StringComparison.OrdinalIgnoreCase));
        uint expectedTargetWord =
            ReplaceTextureId(
                assignment.SourceFaceWord,
                assignment.TargetTextureId);
        Assert(
            patch.WadOffset ==
                assignment.SourceFaceWordWadOffset &&
            patch.Before.SequenceEqual(
                UInt32Bytes(
                    assignment.SourceFaceWord)) &&
            patch.After.SequenceEqual(
                UInt32Bytes(
                    expectedTargetWord)) &&
            CountByteDifferences(
                patch.Before,
                patch.After) == 1,
            $"Tree Tops {assignment.RuntimeKey} is not the exact one-byte T{SourceTextureId}->T{assignment.TargetTextureId} face patch.");
    }

    NativeTerrainTexturePackedAppendRecord[]
        packedRows = assignments
            .Select(assignment =>
                new NativeTerrainTexturePackedAppendRecord(
                    StableEditId:
                        $"research-only:treetops:count{SourceTextureCount + appendedRowCount}:{assignment.RuntimeKey}:T{assignment.TargetTextureId}:clone-native-T{SourceTextureId}",
                    DonorLevelKey: level.Key,
                    DonorWadEntry:
                        level.SourceWadEntry,
                    DonorTextureId:
                        SourceTextureId,
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

    NativeTerrainTextureRecordAppendRequest
        dormantRequest = new(
            sourceImagePath,
            level,
            binding,
            packedRows,
            Array.Empty<
                NativeTerrainTextureRecordExistingPatch>());
    bool dormantBuilt =
        NativeTerrainTextureRecordAppendBuilder.TryBuild(
            dormantRequest,
            out NativeTerrainTextureRecordAppendPlan?
                dormantCandidate,
            out string dormantFailure);
    Assert(
        dormantBuilt && dormantCandidate != null,
        $"Tree Tops count{SourceTextureCount + appendedRowCount} dormant fixed-tail fixture failed: {dormantFailure}");
    NativeTerrainTextureRecordAppendPlan
        dormantPlan = dormantCandidate!;

    NativeTerrainTextureRecordAppendRequest request =
        dormantRequest with
        {
            ExistingLevelDataPatches =
                ordinaryPatches
        };
    bool built =
        NativeTerrainTextureRecordAppendBuilder.TryBuild(
            request,
            out NativeTerrainTextureRecordAppendPlan?
                candidate,
            out string failure);
    Assert(
        built && candidate != null,
        $"Tree Tops count{SourceTextureCount + appendedRowCount} cumulative FixedTail builder failed: {failure}");
    NativeTerrainTextureRecordAppendPlan plan =
        candidate!;
    int expectedOutputCount =
        SourceTextureCount + appendedRowCount;
    int expectedOutputUsed =
        ExpectedSourceUsedBytes + expectedGrowth;
    int expectedOutputTail =
        ExpectedSourceZeroTailBytes - expectedGrowth;
    Assert(
        plan.TargetEntryWadOffset ==
            ExpectedTargetEntryWadOffset &&
        plan.LevelDataByteLength ==
            ExpectedLevelDataBytes &&
        plan.OutputLevelDataByteLength ==
            ExpectedLevelDataBytes &&
        plan.SourceTextureCount ==
            SourceTextureCount &&
        plan.OutputTextureCount ==
            expectedOutputCount &&
        plan.SourceTextureComponentByteLength ==
            ExpectedTextureComponentBytes &&
        plan.OutputTextureComponentByteLength ==
            ExpectedTextureComponentBytes +
                expectedGrowth &&
        plan.RecordCountAdded == appendedRowCount &&
        plan.GrowthByteCount == expectedGrowth &&
        plan.SourceUsedByteLength ==
            ExpectedSourceUsedBytes &&
        plan.OutputUsedByteLength ==
            expectedOutputUsed &&
        plan.SourceZeroTailByteCount ==
            ExpectedSourceZeroTailBytes &&
        plan.OutputZeroTailByteCount ==
            expectedOutputTail &&
        plan.Patch.IsFixedLength &&
        plan.Patch.Before.Length ==
            ExpectedLevelDataBytes &&
        plan.Patch.After.Length ==
            ExpectedLevelDataBytes &&
        plan.FixedSubfileBoundaryPreserved &&
        plan.ArchiveHeadersRequireNoFixup &&
        plan.RuntimeTargetsPersistent &&
        plan.ExistingRowsPreserved &&
        plan.AppendedRowsCopiedExactly &&
        plan.ShiftedComponentChainReparsed &&
        plan.ShiftedTerrainSurfaceSemanticsVerified &&
        plan.ZeroTailVerified &&
        plan.ExistingPatchesRebasedExactly &&
        plan.StructuralGrowthPolicy ==
            NativeTerrainTextureStructuralGrowthPolicy
                .NormalChecked &&
        !plan.StaticResearchOnly &&
        plan.ResolvedRecords.Count ==
            appendedRowCount &&
        plan.RebasedPatches.Count ==
            appendedRowCount,
        $"Tree Tops count{expectedOutputCount} changed its exact FixedTail count, size, tail, or rebase contract.");
    Assert(
        dormantPlan.RecordCountAdded ==
            appendedRowCount &&
        dormantPlan.SourceTextureCount ==
            SourceTextureCount &&
        dormantPlan.OutputTextureCount ==
            expectedOutputCount &&
        dormantPlan.RebasedPatches.Count == 0 &&
        dormantPlan.Patch.IsFixedLength &&
        dormantPlan.FixedSubfileBoundaryPreserved &&
        dormantPlan.ArchiveHeadersRequireNoFixup &&
        CountByteDifferences(
            dormantPlan.Patch.After,
            plan.Patch.After) ==
            appendedRowCount,
        $"Tree Tops count{expectedOutputCount} visible plan does not differ from its same-row dormant FixedTail fixture by exactly {appendedRowCount} face byte(s).");

    for (int index = 0;
         index < appendedRowCount;
         index++)
    {
        CumulativeFaceAssignment assignment =
            assignments[index];
        NativeTerrainTextureRecordAppendResolvedRecord
            resolved = plan.ResolvedRecords[index];
        NativeTerrainTextureRecordRebasedPatchProof
            rebased = plan.RebasedPatches.Single(item =>
                item.RuntimeKey.Equals(
                    assignment.RuntimeKey,
                    StringComparison.OrdinalIgnoreCase));
        Assert(
            resolved.AssignedTextureId ==
                assignment.TargetTextureId &&
            resolved.DonorLevelKey.Equals(
                LevelCatalog.NormalizeKey(level.Key),
                StringComparison.OrdinalIgnoreCase) &&
            resolved.DonorWadEntry ==
                level.SourceWadEntry &&
            resolved.DonorTextureId ==
                SourceTextureId &&
            resolved.MaterialTemplateTextureId ==
                SourceTextureId &&
            rebased.SourceWadOffset ==
                assignment.SourceFaceWordWadOffset &&
            rebased.OutputWadOffset ==
                assignment.SourceFaceWordWadOffset +
                    expectedGrowth &&
            SliceLowRow(
                plan.Patch.After,
                expectedOutputCount,
                assignment.TargetTextureId)
                .SequenceEqual(sourceT12Low) &&
            SliceHighRow(
                plan.Patch.After,
                expectedOutputCount,
                assignment.TargetTextureId)
                .SequenceEqual(sourceT12High),
            $"Tree Tops count{expectedOutputCount} did not keep T{assignment.TargetTextureId} as a byte-identical complete T{SourceTextureId} donor row or did not rebase its matching face exactly.");
    }

    uint expectedSceneEnd =
        RetailSceneEnd +
        (uint)expectedGrowth;
    uint expectedMargin =
        FixedLowerPolygonBuffer -
        expectedSceneEnd;
    Assert(
        expectedSceneEnd <
            FixedLowerPolygonBuffer &&
        (appendedRowCount != Count83AppendedRows ||
         expectedSceneEnd == ExpectedTwoRowSceneEnd &&
         expectedMargin ==
            ExpectedTwoRowPolygonMargin) &&
        (appendedRowCount != Count85AppendedRows ||
         expectedSceneEnd == ExpectedFourRowSceneEnd &&
         expectedMargin ==
            ExpectedFourRowPolygonMargin),
        $"Tree Tops count{expectedOutputCount} crossed or changed the fixed lower polygon-buffer boundary.");

    NativeTerrainTextureRecordAppendExportResult
        export =
            await NativeTerrainTextureRecordAppendBuilder
                .ExportAsync(
                    plan,
                    sourceCuePath,
                    outputPrefix);
    Assert(
        export.SourcePreimageVerified &&
        export.ArchiveHeadersPreserved &&
        export.ExactLevelDataReadbackVerified &&
        export.RuntimeTargetReadbackVerified &&
        File.Exists(export.OutputImagePath) &&
        File.Exists(export.OutputCuePath),
        $"Tree Tops count{expectedOutputCount} omitted exact final-BIN, header, or runtime-target readback.");

    FaceWordExpectation[] expectedFaces =
        assignments
            .Select(assignment =>
                new FaceWordExpectation(
                    assignment.RuntimeKey,
                    assignment.SourceFaceWordWadOffset +
                        expectedGrowth,
                    ReplaceTextureId(
                        assignment.SourceFaceWord,
                        assignment.TargetTextureId)))
            .ToArray();
    VerifyTextureCountAndFaces(
        export.OutputImagePath,
        level,
        expectedOutputCount,
        expectedFaces);

    IReadOnlyList<TerrainTextureSlot> slots =
        TerrainPatchExporter.InspectTextureSlots(
            export.OutputImagePath,
            level);
    foreach (CumulativeFaceAssignment assignment in
             assignments)
    {
        Assert(
            slots[SourceTextureId]
                .CombinedTopologySignature.Equals(
                    slots[assignment.TargetTextureId]
                        .CombinedTopologySignature,
                    StringComparison.Ordinal) &&
            slots[SourceTextureId]
                .HasNormalDescriptors ==
                slots[assignment.TargetTextureId]
                    .HasNormalDescriptors &&
            slots[SourceTextureId]
                .HasCloseDescriptors ==
                slots[assignment.TargetTextureId]
                    .HasCloseDescriptors,
            $"Final count{expectedOutputCount} decoder did not see T{assignment.TargetTextureId} as the same complete native topology as T{SourceTextureId}.");
    }

    string finalOverlayPath = Path.Combine(
        outputRoot,
        $".treetops-count{expectedOutputCount}-final-bin-overlay.tmp.json");
    if (File.Exists(finalOverlayPath))
        File.Delete(finalOverlayPath);
    try
    {
        SourceSceneOverlayExporter.Export(
            export.OutputImagePath,
            wadAnalysisPath,
            level,
            finalOverlayPath);
        GeometryCandidate finalGeometry =
            GeometryOverlayLoader.LoadFirstCandidate(
                finalOverlayPath);
        Dictionary<string, int>
            expectedFaceTextures =
                cumulativeSourceGeometry.Polygons
                    .ToDictionary(
                        item => item.RuntimeKey,
                        item => item.OriginalTextureId,
                        StringComparer.OrdinalIgnoreCase);
        foreach (CumulativeFaceAssignment assignment in
                 assignments)
        {
            expectedFaceTextures[
                assignment.RuntimeKey] =
                assignment.TargetTextureId;
        }
        Assert(
            finalGeometry.Polygons.Count ==
                cumulativeSourceGeometry
                    .Polygons.Count &&
            finalGeometry.Polygons.All(item =>
                expectedFaceTextures.TryGetValue(
                    item.RuntimeKey,
                    out int expectedTextureId) &&
                item.OriginalTextureId ==
                    expectedTextureId) &&
            assignments.All(assignment =>
                finalGeometry.Polygons.Single(item =>
                    item.RuntimeKey.Equals(
                        assignment.RuntimeKey,
                        StringComparison.OrdinalIgnoreCase))
                    .OriginalTextureId ==
                    assignment.TargetTextureId),
            $"Tree Tops count{expectedOutputCount} final geometry readback changed a non-target face or missed a cumulative target.");
    }
    finally
    {
        if (File.Exists(finalOverlayPath))
            File.Delete(finalOverlayPath);
    }

    FaceWordExpectation anchor =
        expectedFaces[0];
    FastEntryProof fast =
        await BuildFastEntryCloneAsync(
            export.OutputImagePath,
            export.OutputCuePath,
            fastOutputPrefix,
            level,
            expectedOutputCount,
            anchor.WadOffset,
            anchor.ExpectedWord);
    VerifyTextureCountAndFaces(
        fast.ImagePath,
        level,
        expectedOutputCount,
        expectedFaces);
    AssertRetailSourcePreserved();

    return new CumulativeFixedTailProof(
        appendedRowCount,
        expectedOutputCount,
        expectedGrowth,
        expectedOutputUsed,
        expectedOutputTail,
        $"0x{expectedSceneEnd:X8}",
        $"0x{expectedMargin:X}",
        assignments.ToArray(),
        DormantFixtureVerified: true,
        CountByteDifferences(
            dormantPlan.Patch.After,
            plan.Patch.After),
        export.OutputImagePath,
        export.OutputCuePath,
        export.OutputImageSha256,
        export.ExactLevelDataReadbackVerified,
        export.RuntimeTargetReadbackVerified,
        fast.ImagePath,
        fast.CuePath,
        fast.Sha256,
        fast.ControlByteDifferences);
}

static async Task<TerrainPatchPlan> BuildOneFacePlanAsync(
    GeometryCandidate sourceGeometry,
    TerrainPolygon sourceFace,
    int targetTextureId,
    string editsPath,
    string outputPrefix,
    string label,
    string sourceImagePath,
    string sourceCuePath,
    string sourceSearchPath,
    LevelDefinition level)
{
    GeometryCandidate geometry = sourceGeometry;
    TerrainPolygon face = geometry.Polygons.Single(item =>
        item.RuntimeKey.Equals(
            sourceFace.RuntimeKey,
            StringComparison.OrdinalIgnoreCase));
    face.ApplyTextureOverride(targetTextureId);
    int saved = await TerrainEditStore.SaveAsync(
        editsPath,
        [face],
        label);
    Assert(
        saved == 1,
        $"{label}: did not save exactly one face edit.");
    TerrainPatchPlan plan = TerrainPatchExporter.BuildPlan(
        sourceImagePath,
        sourceCuePath,
        outputPrefix + "-not-written.bin",
        outputPrefix + "-not-written.cue",
        level,
        ramPath: "",
        sourceSearchPath,
        editsPath);
    TerrainPatch[] texturePatches = plan.Patches
        .Where(patch => patch.Kind.Equals(
            "texture-id-word3",
            StringComparison.OrdinalIgnoreCase))
        .ToArray();
    Assert(
        plan.Patches.Count == 1 &&
        texturePatches is
        [
            {
                RuntimeKey: RuntimeKey,
                ByteLength: sizeof(uint)
            }
        ] &&
        plan.SkippedEdits.All(item =>
            item.Equals(
                "collision: source-derived exact triangle scan found no matching source collision records for the edited terrain face(s).",
                StringComparison.Ordinal)),
        $"{label}: ordinary terrain plan was not exactly one source-bound texture word.");
    return plan;
}

static async Task<TerrainPatchPlan> BuildMultiFacePlanAsync(
    GeometryCandidate sourceGeometry,
    IReadOnlyList<CumulativeFaceAssignment>
        assignments,
    string editsPath,
    string outputPrefix,
    string label,
    string sourceImagePath,
    string sourceCuePath,
    string sourceSearchPath,
    LevelDefinition level)
{
    TerrainPolygon[] editedFaces =
        assignments
            .Select(assignment =>
            {
                TerrainPolygon face =
                    sourceGeometry.Polygons.Single(item =>
                        item.RuntimeKey.Equals(
                            assignment.RuntimeKey,
                            StringComparison.OrdinalIgnoreCase));
                Assert(
                    face.OriginalTextureId ==
                        assignment.SourceTextureId &&
                    face.HasCompleteNativeHighPolyFacePayload,
                    $"{label}: face {assignment.RuntimeKey} is not a complete matching native T{assignment.SourceTextureId} HP face.");
                face.ApplyTextureOverride(
                    assignment.TargetTextureId);
                return face;
            })
            .ToArray();
    int saved = await TerrainEditStore.SaveAsync(
        editsPath,
        editedFaces,
        label);
    Assert(
        saved == assignments.Count,
        $"{label}: saved {saved} face edit(s), expected {assignments.Count}.");
    TerrainPatchPlan plan =
        TerrainPatchExporter.BuildPlan(
            sourceImagePath,
            sourceCuePath,
            outputPrefix + "-not-written.bin",
            outputPrefix + "-not-written.cue",
            level,
            ramPath: "",
            sourceSearchPath,
            editsPath);
    TerrainPatch[] texturePatches =
        plan.Patches
            .Where(patch => patch.Kind.Equals(
                "texture-id-word3",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
    HashSet<string> expectedKeys =
        assignments
            .Select(item => item.RuntimeKey)
            .ToHashSet(
                StringComparer.OrdinalIgnoreCase);
    Assert(
        plan.Patches.Count == assignments.Count &&
        texturePatches.Length == assignments.Count &&
        texturePatches.All(patch =>
            patch.ByteLength == sizeof(uint) &&
            expectedKeys.Contains(
                patch.RuntimeKey)) &&
        texturePatches
            .Select(patch => patch.RuntimeKey)
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .Count() == assignments.Count &&
        plan.SkippedEdits.All(item =>
            item.Equals(
                "collision: source-derived exact triangle scan found no matching source collision records for the edited terrain face(s).",
                StringComparison.Ordinal)),
        $"{label}: ordinary terrain plan was not exactly one source-bound texture word per cumulative face.");
    return plan;
}

static async Task<FastEntryProof> BuildFastEntryCloneAsync(
    string controlImagePath,
    string controlCuePath,
    string outputPrefix,
    LevelDefinition level,
    int expectedTextureCount,
    long expectedFaceWordOffset,
    uint expectedFaceWord)
{
    string outputImagePath = outputPrefix + ".bin";
    string outputCuePath = outputPrefix + ".cue";
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
        TestLevelWarpPatch.ApplyToDisposableDiagnosticImage(
            outputImagePath,
            level);
    Assert(
        TestLevelWarpPatch.TryGetTargetLevelId(
            fastEntryPatch,
            out int targetLevelId) &&
        targetLevelId == ExpectedLevelId &&
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
        $"Fast-entry clone differs from its control by {controlDifferences} byte(s), expected exactly eight guarded executable bytes.");
    VerifyTextureCountAndFace(
        outputImagePath,
        level,
        expectedTextureCount,
        expectedFaceWordOffset,
        expectedFaceWord);
    return new FastEntryProof(
        outputImagePath,
        outputCuePath,
        Sha256File(outputImagePath),
        controlDifferences);
}

static void VerifyTextureCountAndFaces(
    string imagePath,
    LevelDefinition level,
    int expectedTextureCount,
    IReadOnlyList<FaceWordExpectation>
        expectedFaces)
{
    IReadOnlyList<TerrainTextureSlot> slots =
        TerrainPatchExporter.InspectTextureSlots(
            imagePath,
            level);
    Assert(
        slots.Count == expectedTextureCount &&
        slots[^1].TextureId ==
            expectedTextureCount - 1,
        $"Final BIN decoded {slots.Count} texture row(s), expected {expectedTextureCount}.");
    Assert(
        expectedFaces.Count > 0 &&
        expectedFaces
            .Select(item => item.RuntimeKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == expectedFaces.Count &&
        expectedFaces
            .Select(item => item.WadOffset)
            .Distinct()
            .Count() == expectedFaces.Count,
        "Final cumulative face readback received duplicate or empty expectations.");
    foreach (FaceWordExpectation expected in
             expectedFaces)
    {
        uint faceWord =
            BinaryPrimitives.ReadUInt32LittleEndian(
                ReadWadBytes(
                    imagePath,
                    expected.WadOffset,
                    sizeof(uint)));
        Assert(
            faceWord == expected.ExpectedWord,
            $"Final {expected.RuntimeKey} face word at WAD 0x{expected.WadOffset:X} is 0x{faceWord:X8}, expected 0x{expected.ExpectedWord:X8}.");
    }
}

static void VerifyTextureCountAndFace(
    string imagePath,
    LevelDefinition level,
    int expectedTextureCount,
    long faceWordWadOffset,
    uint expectedFaceWord)
{
    IReadOnlyList<TerrainTextureSlot> slots =
        TerrainPatchExporter.InspectTextureSlots(
            imagePath,
            level);
    Assert(
        slots.Count == expectedTextureCount &&
        slots[^1].TextureId == expectedTextureCount - 1,
        $"Final BIN decoded {slots.Count} texture row(s), expected {expectedTextureCount}.");
    uint faceWord = BinaryPrimitives.ReadUInt32LittleEndian(
        ReadWadBytes(
            imagePath,
            faceWordWadOffset,
            sizeof(uint)));
    Assert(
        faceWord == expectedFaceWord,
        $"Final face word at WAD 0x{faceWordWadOffset:X} is 0x{faceWord:X8}, expected 0x{expectedFaceWord:X8}.");
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

static byte[] SliceLowRow(
    byte[] levelData,
    int textureCount,
    int textureId)
{
    Assert(
        textureId >= 0 && textureId < textureCount,
        $"T{textureId} is outside count {textureCount}.");
    int offset = checked(
        8 +
        (textureId *
         NativeTerrainTextureRecordAppendBuilder
             .LowDetailRecordBytes));
    return levelData
        .AsSpan(
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
        textureId >= 0 && textureId < textureCount,
        $"T{textureId} is outside count {textureCount}.");
    int offset = checked(
        8 +
        (textureCount *
         NativeTerrainTextureRecordAppendBuilder
             .LowDetailRecordBytes) +
        (textureId *
         NativeTerrainTextureRecordAppendBuilder
             .HighDetailRecordBytes));
    return levelData
        .AsSpan(
            offset,
            NativeTerrainTextureRecordAppendBuilder
                .HighDetailRecordBytes)
        .ToArray();
}

static byte[] UInt32Bytes(uint value)
{
    byte[] bytes = new byte[sizeof(uint)];
    BinaryPrimitives.WriteUInt32LittleEndian(
        bytes,
        value);
    return bytes;
}

static byte[] ReadWadBytes(
    string imagePath,
    long wadOffset,
    int length)
{
    (int sectorSize, int userOffset) =
        DetectLayout(imagePath);
    byte[] result = new byte[length];
    using FileStream stream = File.OpenRead(imagePath);
    int written = 0;
    long fileOffset = wadOffset;
    while (written < length)
    {
        int sectorOffset = checked(
            (int)(fileOffset % 2048));
        int sector = checked(
            37 +
            (int)(fileOffset / 2048));
        int count = Math.Min(
            length - written,
            2048 - sectorOffset);
        stream.Position =
            ((long)sector * sectorSize) +
            userOffset +
            sectorOffset;
        stream.ReadExactly(
            result.AsSpan(written, count));
        written += count;
        fileOffset += count;
    }
    return result;
}

static (int SectorSize, int UserOffset) DetectLayout(
    string imagePath)
{
    using FileStream stream = File.OpenRead(imagePath);
    Span<byte> signature = stackalloc byte[6];
    foreach ((int sectorSize, int userOffset) in
             new[]
             {
                 (2048, 0),
                 (2352, 24),
                 (2336, 8)
             })
    {
        long offset =
            (16L * sectorSize) +
            userOffset;
        if (offset + signature.Length >
            stream.Length)
        {
            continue;
        }
        stream.Position = offset;
        stream.ReadExactly(signature);
        if (signature[0] == 1 &&
            Encoding.ASCII.GetString(
                signature[1..]) == "CD001")
        {
            return (sectorSize, userOffset);
        }
    }
    throw new InvalidDataException(
        "Could not detect the diagnostic image's ISO9660 sector layout.");
}

static string BuildCueTextForImage(
    string sourceCuePath,
    string outputBinFileName)
{
    string[] lines = File.ReadAllLines(sourceCuePath);
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
    return string.Join(
               Environment.NewLine,
               lines) +
           Environment.NewLine;
}

static int CountFileByteDifferences(
    string firstPath,
    string secondPath)
{
    using FileStream first = File.OpenRead(firstPath);
    using FileStream second = File.OpenRead(secondPath);
    Assert(
        first.Length == second.Length,
        "Cannot compare differently sized BINs.");
    byte[] firstBuffer = new byte[1024 * 1024];
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
            firstBuffer.AsSpan(0, firstRead),
            secondBuffer.AsSpan(0, secondRead));
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

static string ResolveWorkspaceRoot(string? candidate)
{
    string current = Path.GetFullPath(
        candidate ??
        Directory.GetCurrentDirectory());
    for (DirectoryInfo? directory = new(current);
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

static string ResolvePhysicalPath(string path)
{
    FileInfo info = new(Path.GetFullPath(path));
    return info.ResolveLinkTarget(
               returnFinalTarget: true)
               ?.FullName ??
           info.FullName;
}

static string Sha256File(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(
        SHA256.HashData(stream));
}

static string Sha256Bytes(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexString(
        SHA256.HashData(bytes));

static long FileLength(string path)
{
    using FileStream stream = File.OpenRead(path);
    return stream.Length;
}

static void DeleteOutputs(params string[] prefixes)
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
            string path = prefix + extension;
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record FastEntryProof(
    string ImagePath,
    string CuePath,
    string Sha256,
    int ControlByteDifferences);

internal sealed record CumulativeFaceAssignment(
    string RuntimeKey,
    int SourceTextureId,
    int TargetTextureId,
    long SourceFaceWordWadOffset,
    uint SourceFaceWord);

internal sealed record FaceWordExpectation(
    string RuntimeKey,
    long WadOffset,
    uint ExpectedWord);

internal sealed record CumulativeFixedTailProof(
    int AppendedRowCount,
    int OutputTextureCount,
    int GrowthBytes,
    int OutputUsedBytes,
    int OutputZeroTailBytes,
    string SceneEnd,
    string PolygonBufferMargin,
    IReadOnlyList<CumulativeFaceAssignment>
        FaceAssignments,
    bool DormantFixtureVerified,
    int DormantToVisibleByteDifferences,
    string OutputImagePath,
    string OutputCuePath,
    string OutputImageSha256,
    bool ExactFinalBinReadbackVerified,
    bool RuntimeTargetReadbackVerified,
    string FastEntryImagePath,
    string FastEntryCuePath,
    string FastEntrySha256,
    int FastEntryControlByteDifferences);
