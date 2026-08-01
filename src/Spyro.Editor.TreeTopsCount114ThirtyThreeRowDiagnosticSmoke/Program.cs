using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

const string ExpectedInputSha256 =
    "F06A50C81F72A1742B08E7A18E6116D5E6B5E77AC2D2301E49458D9FA627035B";
const string ExpectedCount113BaseSha256 =
    "CE30E0F9C402F595C85922D6EFDF8331E5B323A71DB4811490B907F4028F49DC";
const string ExpectedOutputSha256 =
    "D667CB00C1926C4A102A3E8146911F61835B46A5851FD2ABE077FF78A87BDFDB";
const long ExpectedImageLength = 661_547_040;
const int ExpectedLevelId = 43;
const int ExpectedWadEntry = 52;
const int SourceTextureCount = 113;
const int OutputTextureCount = 114;
const int SourceTextureId = 12;
const int AppendedTextureId = 113;
const int ExpectedSourceTextureComponentBytes = 0x5140;
const int ExpectedOutputTextureComponentBytes = 0x51F8;
const int ExpectedLevelDataBytes = 0xA9800;
const int ExpectedSourceUsedBytes = 0xA93AC;
const int ExpectedOutputUsedBytes = 0xA9464;
const int ExpectedSourceZeroTailBytes = 0x454;
const int ExpectedOutputZeroTailBytes = 0x39C;
const int InheritedCount113GrowthBytes = 0x1700;
const int IncrementalGrowthBytes = 0xB8;
const long RetailFaceWordWadOffset = 0x3E68958;
const long CurrentFaceWordWadOffset =
    RetailFaceWordWadOffset + InheritedCount113GrowthBytes;
const long ExpectedOutputFaceWordWadOffset =
    CurrentFaceWordWadOffset + IncrementalGrowthBytes;
const uint ExpectedSourceFaceWord = 0xFF12760C;
const uint ExpectedOutputFaceWord = 0xFF127671;
const uint Count114SceneEnd = 0x80187CAC;
const uint LowerPolygonArena = 0x80187D30;
const uint Count114Margin = LowerPolygonArena - Count114SceneEnd;
const uint Count115SceneEnd = Count114SceneEnd + IncrementalGrowthBytes;
const uint Count115Overlap = Count115SceneEnd - LowerPolygonArena;
const string RuntimeKey = "4:69:hp";

string? rootArgument = args.FirstOrDefault(argument =>
    !argument.StartsWith("--", StringComparison.Ordinal));
string workspaceRoot = ResolveWorkspaceRoot(rootArgument);
string outputRoot = Path.Combine(
    workspaceRoot,
    "_local",
    "research",
    "tree-tops-private-texture-load-freeze-diagnostics");
string inputPrefix = Path.Combine(
    outputRoot,
    "treetops-COUNT113-POLY-BUFFER-1BF40-CONTROL-FAST-ENTRY");
string sourceImagePath = inputPrefix + ".bin";
string sourceCuePath = inputPrefix + ".cue";
string count113ProofPath = Path.Combine(
    outputRoot,
    "tree-tops-count113-thirty-two-row-poly-buffer-diagnostic-proof.json");
string outputPrefix = Path.Combine(
    outputRoot,
    "treetops-COUNT114-THIRTY-THREE-ROWS-T81-T113-CLONE-T12-THIRTY-THREE-VISIBLE-FACES-POLY-1BF40-INCREMENTAL-FIXED-TAIL-DIAGNOSTIC-FAST-ENTRY");
string proofPath = Path.Combine(
    outputRoot,
    "tree-tops-count114-thirty-three-row-poly-buffer-diagnostic-proof.json");
string checklistPath = Path.Combine(
    outputRoot,
    "tree-tops-count114-thirty-three-row-runtime-checklist.md");

Directory.CreateDirectory(outputRoot);
DeleteOutput(outputPrefix);
if (File.Exists(proofPath))
    File.Delete(proofPath);
if (File.Exists(checklistPath))
    File.Delete(checklistPath);

Assert(
    File.Exists(sourceImagePath) &&
    File.Exists(sourceCuePath) &&
    File.Exists(count113ProofPath),
    "The exact proven count113/1BF40 input or its structural proof is missing.");
string sourceSha256 = Sha256File(sourceImagePath);
Assert(
    sourceSha256.Equals(
        ExpectedInputSha256,
        StringComparison.OrdinalIgnoreCase) &&
    new FileInfo(sourceImagePath).Length == ExpectedImageLength,
    "The count114 diagnostic is guarded to the exact runtime-proven count113/1BF40 BIN.");
Assert(
    File.ReadAllText(sourceCuePath).Contains(
        Path.GetFileName(sourceImagePath),
        StringComparison.OrdinalIgnoreCase),
    "The count113 input CUE does not target the checked input BIN.");

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
    binding.TargetWadEntry == ExpectedWadEntry,
    "The exact count113 input no longer exposes the expected bound texture table.");

NativeTerrainTextureRecordAppendResearchRequest fixtureRequest = new(
    sourceImagePath,
    sourceCuePath,
    level,
    level,
    SourceTextureId);
bool fixtureBuilt =
    NativeTerrainTextureRecordAppendResearch.TryBuild(
        fixtureRequest,
        out NativeTerrainTextureRecordAppendResearchPlan? fixtureCandidate,
        out string fixtureFailure);
Assert(
    fixtureBuilt && fixtureCandidate != null,
    $"The independent count113-to-count114 fixed-tail fixture failed: {fixtureFailure}");
NativeTerrainTextureRecordAppendResearchPlan fixture =
    fixtureCandidate!;
Assert(
    fixture.SourceTextureCount == SourceTextureCount &&
    fixture.OutputTextureCount == OutputTextureCount &&
    fixture.SourceTextureComponentByteLength ==
        ExpectedSourceTextureComponentBytes &&
    fixture.OutputTextureComponentByteLength ==
        ExpectedOutputTextureComponentBytes &&
    fixture.GrowthByteCount == IncrementalGrowthBytes &&
    fixture.LevelDataByteLength == ExpectedLevelDataBytes &&
    fixture.SourceUsedByteLength == ExpectedSourceUsedBytes &&
    fixture.OutputUsedByteLength == ExpectedOutputUsedBytes &&
    fixture.SourceZeroTailByteCount == ExpectedSourceZeroTailBytes &&
    fixture.OutputZeroTailByteCount == ExpectedOutputZeroTailBytes &&
    fixture.AppendedTextureId == AppendedTextureId &&
    fixture.ExistingLowDetailRowsPreserved &&
    fixture.ExistingHighDetailRowsPreserved &&
    fixture.DonorRowsCopiedExactly &&
    fixture.SuffixPreservedExactly &&
    fixture.OutputZeroTailVerified &&
    fixture.OutputComponentChainReparsed,
    "The count114 fixture changed its exact fixed-tail capacity contract.");

byte[] sourceT12Low = SliceLowRow(
    fixture.SourceLevelData,
    SourceTextureCount,
    SourceTextureId);
byte[] sourceT12High = SliceHighRow(
    fixture.SourceLevelData,
    SourceTextureCount,
    SourceTextureId);
byte[] fixtureT113Low = SliceLowRow(
    fixture.OutputLevelData,
    OutputTextureCount,
    AppendedTextureId);
byte[] fixtureT113High = SliceHighRow(
    fixture.OutputLevelData,
    OutputTextureCount,
    AppendedTextureId);
Assert(
    sourceT12Low.SequenceEqual(fixtureT113Low) &&
    sourceT12High.SequenceEqual(fixtureT113High),
    "The independent fixture did not make T113 a complete byte-identical T12 clone.");

NativeTerrainTexturePackedAppendRecord packedT113 = new(
    StableEditId:
        "research-only:treetops:count114:4:69:hp:T113:clone-native-T12",
    DonorLevelKey: level.Key,
    DonorWadEntry: level.SourceWadEntry,
    DonorTextureId: SourceTextureId,
    MaterialTemplateTextureId: SourceTextureId,
    LowDetailRow: sourceT12Low.ToArray(),
    HighDetailRow: sourceT12High.ToArray(),
    ExpectedLowDetailSha256: Sha256Bytes(sourceT12Low),
    ExpectedHighDetailSha256: Sha256Bytes(sourceT12High));
NativeTerrainTextureRecordExistingPatch facePatch = new(
    CurrentFaceWordWadOffset,
    UInt32Bytes(ExpectedSourceFaceWord),
    UInt32Bytes(ExpectedOutputFaceWord),
    "texture-id-word3",
    RuntimeKey);
NativeTerrainTextureRecordAppendRequest appendRequest = new(
    sourceImagePath,
    level,
    binding,
    [packedT113],
    [facePatch]);
bool appendBuilt =
    NativeTerrainTextureRecordAppendBuilder.TryBuild(
        appendRequest,
        out NativeTerrainTextureRecordAppendPlan? appendCandidate,
        out string appendFailure);
Assert(
    appendBuilt && appendCandidate != null,
    $"The count114 fixed-tail append failed: {appendFailure}");
NativeTerrainTextureRecordAppendPlan plan = appendCandidate!;
NativeTerrainTextureRecordRebasedPatchProof rebasedFace =
    plan.RebasedPatches.Single();
Assert(
    plan.SourceTextureCount == SourceTextureCount &&
    plan.OutputTextureCount == OutputTextureCount &&
    plan.SourceTextureComponentByteLength ==
        ExpectedSourceTextureComponentBytes &&
    plan.OutputTextureComponentByteLength ==
        ExpectedOutputTextureComponentBytes &&
    plan.RecordCountAdded == 1 &&
    plan.GrowthByteCount == IncrementalGrowthBytes &&
    plan.LevelDataByteLength == ExpectedLevelDataBytes &&
    plan.OutputLevelDataByteLength == ExpectedLevelDataBytes &&
    plan.SourceUsedByteLength == ExpectedSourceUsedBytes &&
    plan.OutputUsedByteLength == ExpectedOutputUsedBytes &&
    plan.SourceZeroTailByteCount == ExpectedSourceZeroTailBytes &&
    plan.OutputZeroTailByteCount == ExpectedOutputZeroTailBytes &&
    plan.Patch.IsFixedLength &&
    plan.FixedSubfileBoundaryPreserved &&
    plan.ArchiveHeadersRequireNoFixup &&
    plan.RuntimeTargetsPersistent &&
    plan.ExistingRowsPreserved &&
    plan.AppendedRowsCopiedExactly &&
    plan.ShiftedComponentChainReparsed &&
    plan.ShiftedTerrainSurfaceSemanticsVerified &&
    plan.ZeroTailVerified &&
    plan.ExistingPatchesRebasedExactly &&
    rebasedFace.SourceWadOffset == CurrentFaceWordWadOffset &&
    rebasedFace.OutputWadOffset == ExpectedOutputFaceWordWadOffset,
    "The count114 plan changed its exact count, size, tail, row, or face-rebase contract.");

AssertWord(
    plan.Patch.Before,
    plan.LevelDataWadOffset,
    CurrentFaceWordWadOffset,
    ExpectedSourceFaceWord,
    "count113 face 4:69 source preimage");
AssertWord(
    plan.Patch.After,
    plan.LevelDataWadOffset,
    ExpectedOutputFaceWordWadOffset,
    ExpectedOutputFaceWord,
    "count114 face 4:69 output readback");

FaceContract[] inheritedFaces =
    LoadCount113FaceContracts(count113ProofPath);
Assert(
    inheritedFaces.Length == 32,
    "The count113 proof no longer contains exactly 32 inherited visible face assignments.");
foreach (FaceContract face in inheritedFaces)
{
    AssertWord(
        plan.Patch.Before,
        plan.LevelDataWadOffset,
        face.CurrentWadOffset,
        face.Word,
        $"count113 inherited face {face.RuntimeKey}");
    AssertWord(
        plan.Patch.After,
        plan.LevelDataWadOffset,
        face.CurrentWadOffset + IncrementalGrowthBytes,
        face.Word,
        $"count114 inherited face {face.RuntimeKey}");
}

Assert(
    SliceLowRow(
        plan.Patch.After,
        OutputTextureCount,
        SourceTextureId).SequenceEqual(
            SliceLowRow(
                plan.Patch.After,
                OutputTextureCount,
                AppendedTextureId)) &&
    SliceHighRow(
        plan.Patch.After,
        OutputTextureCount,
        SourceTextureId).SequenceEqual(
            SliceHighRow(
                plan.Patch.After,
                OutputTextureCount,
                AppendedTextureId)),
    "The composed count114 image does not retain byte-identical T12/T113 records.");
Assert(
    Count114Margin == 0x84 &&
    Count115Overlap == 0x34,
    "The count114/count115 polygon-arena boundary arithmetic changed.");

bool normalCreateBinAuthorized =
    AppendedPrivateTerrainTexturePromotionProfileRegistry
        .TryAuthorizeNormalRelease(
            binding,
            NativeTerrainTexturePrivateImageWriterKind.FixedTail,
            1,
            out AppendedPrivateTerrainTexturePromotionProfile?
                normalProfile,
            out string normalGateReason);
Assert(
    !normalCreateBinAuthorized &&
    normalProfile == null &&
    normalGateReason.Contains(
        "No exact appended-private promotion profile",
        StringComparison.OrdinalIgnoreCase),
    "The research-only count114 derivative escaped into normal Create BIN authorization.");

NativeTerrainTextureRecordAppendExportResult exported =
    await NativeTerrainTextureRecordAppendBuilder.ExportAsync(
        plan,
        sourceCuePath,
        outputPrefix);
Assert(
    exported.SourcePreimageVerified &&
    exported.ArchiveHeadersPreserved &&
    exported.ExactLevelDataReadbackVerified &&
    exported.RuntimeTargetReadbackVerified &&
    File.Exists(exported.OutputImagePath) &&
    File.Exists(exported.OutputCuePath),
    "The count114 export omitted exact final-BIN/header/runtime-target readback.");
Assert(
    Sha256File(sourceImagePath).Equals(
        ExpectedInputSha256,
        StringComparison.OrdinalIgnoreCase),
    "The proven count113/1BF40 input was modified.");
Assert(
    File.ReadAllText(exported.OutputCuePath).Contains(
        Path.GetFileName(exported.OutputImagePath),
        StringComparison.OrdinalIgnoreCase),
    "The count114 CUE does not target the checked output BIN.");
if (!string.IsNullOrWhiteSpace(ExpectedOutputSha256))
{
    Assert(
        exported.OutputImageSha256.Equals(
            ExpectedOutputSha256,
            StringComparison.OrdinalIgnoreCase),
        "The deterministic count114 output SHA-256 changed.");
}

NativeTerrainTextureRuntimeControlAudit finalRuntime =
    NativeTerrainTextureRuntimeControlScanner.Inspect(
        exported.OutputImagePath,
        level);
Assert(
    finalRuntime.Complete &&
    finalRuntime.TextureCount == OutputTextureCount &&
    finalRuntime.IsRuntimePersistentTarget(AppendedTextureId),
    "The final count114 BIN failed runtime-control readback for T113.");
IReadOnlyList<TerrainTextureSlot> finalSlots =
    TerrainPatchExporter.InspectTextureSlots(
        exported.OutputImagePath,
        level);
Assert(
    finalSlots.Count == OutputTextureCount &&
    finalSlots[SourceTextureId].CombinedTopologySignature.Equals(
        finalSlots[AppendedTextureId].CombinedTopologySignature,
        StringComparison.Ordinal) &&
    finalSlots[SourceTextureId].HasNormalDescriptors ==
        finalSlots[AppendedTextureId].HasNormalDescriptors &&
    finalSlots[SourceTextureId].HasCloseDescriptors ==
        finalSlots[AppendedTextureId].HasCloseDescriptors,
    "The final decoder did not see T113 as the same native topology as T12.");

var proof = new
{
    GeneratedAtUtc = DateTimeOffset.UtcNow,
    Status =
        "PASSED OFFLINE; COUNT114 THIRTY-THREE-ROW VISIBLE POLY-1BF40 FAST-ENTRY RUNTIME RESULT PENDING",
    ResearchOnly = true,
    NormalCreateBinIntegrated = false,
    NormalCreateBinAuthorized = false,
    DuckStationRuntimeProven = false,
    Input = new
    {
        ImagePath = sourceImagePath,
        CuePath = sourceCuePath,
        Sha256 = sourceSha256,
        Length = ExpectedImageLength,
        RuntimeEvidence =
            "Interactive user report on 2026-08-01: Tree Tops loaded; movement, music, enemies, camera movement, and Inventory worked."
    },
    Capacity = new
    {
        SourceTextureCount,
        OutputTextureCount,
        AppendedRowsInherited = 32,
        AppendedRowsAdded = 1,
        TotalAppendedRows = 33,
        TextureComponentBytes =
            $"0x{ExpectedSourceTextureComponentBytes:X}->0x{ExpectedOutputTextureComponentBytes:X}",
        UsedBytes =
            $"0x{ExpectedSourceUsedBytes:X}->0x{ExpectedOutputUsedBytes:X}",
        LevelDataBytes = $"0x{ExpectedLevelDataBytes:X}",
        ZeroTailBytes =
            $"0x{ExpectedSourceZeroTailBytes:X}->0x{ExpectedOutputZeroTailBytes:X}",
        IncrementalGrowthBytes = $"0x{IncrementalGrowthBytes:X}",
        FurtherSectorGrowth = false
    },
    PolygonArena = new
    {
        Stride = "0x1BF40",
        Count114SceneEnd = $"0x{Count114SceneEnd:X8}",
        LowerArena = $"0x{LowerPolygonArena:X8}",
        Count114Margin = $"0x{Count114Margin:X}",
        Count115SceneEnd = $"0x{Count115SceneEnd:X8}",
        Count115Overlap = $"0x{Count115Overlap:X}",
        Count114IsExactCeilingForThisStride = true
    },
    NewRow = new
    {
        TextureId = AppendedTextureId,
        CloneOf = SourceTextureId,
        CompleteBytes = IncrementalGrowthBytes,
        LowSha256 = Sha256Bytes(sourceT12Low),
        HighSha256 = Sha256Bytes(sourceT12High),
        ByteIdenticalToNativeT12 = true
    },
    NewFace = new
    {
        RuntimeKey,
        SourceTextureId,
        TargetTextureId = AppendedTextureId,
        RetailWadOffset = $"0x{RetailFaceWordWadOffset:X}",
        Count113WadOffset = $"0x{CurrentFaceWordWadOffset:X}",
        Count114WadOffset = $"0x{ExpectedOutputFaceWordWadOffset:X}",
        SourceWord = $"0x{ExpectedSourceFaceWord:X8}",
        OutputWord = $"0x{ExpectedOutputFaceWord:X8}",
        Points = new[]
        {
            new { X = 7149, Y = 4673, Z = 1728 },
            new { X = 7223, Y = 4646, Z = 1856 }
        }
    },
    InheritedFaces = inheritedFaces.Select(face => new
    {
        face.RuntimeKey,
        Count113WadOffset = $"0x{face.CurrentWadOffset:X}",
        Count114WadOffset =
            $"0x{face.CurrentWadOffset + IncrementalGrowthBytes:X}",
        Word = $"0x{face.Word:X8}",
        Preserved = true
    }),
    Output = new
    {
        ImagePath = exported.OutputImagePath,
        CuePath = exported.OutputCuePath,
        Sha256 = exported.OutputImageSha256,
        Length = new FileInfo(exported.OutputImagePath).Length,
        ExactLevelDataReadback =
            exported.ExactLevelDataReadbackVerified,
        ExactRuntimeTargetReadback =
            exported.RuntimeTargetReadbackVerified,
        CueTargetsOutputBin = true,
        SourcePreserved = true
    },
    Verification = new
    {
        ExactInputHash = true,
        ExactInputLength = true,
        ExactCount113FaceMatrixReadback = true,
        AllThirtyTwoInheritedFacesRebasedByB8 = true,
        ExactNewFacePreimage = true,
        ExactNewFaceReadback = true,
        T113IsCompleteT12Clone = true,
        ComponentChainReparsed = true,
        ZeroTailVerified = true,
        ArchiveHeadersPreserved = true,
        FastEntryInheritedFromExactInput = true,
        Polygon1BF40InheritedFromExactInput = true,
        CoreOrReleaseIntegrated = false
    },
    NormalCreateBinGateReason = normalGateReason,
    RuntimeBoundary =
        "This is the exact count114/33-row Tree Tops boundary at polygon stride 0x1BF40. Avoid Save Fairy and all flight/result paths. It does not prove arbitrary payloads, texture-page repacking, persistence, normal Create BIN, Beta V4, or release safety. Count115 overlaps this polygon arena and must not be generated with the same stride."
};
await File.WriteAllTextAsync(
    proofPath,
    JsonSerializer.Serialize(
        proof,
        new JsonSerializerOptions
        {
            WriteIndented = true
        }));
await File.WriteAllTextAsync(
    checklistPath,
    $"""
    # Tree Tops count114 thirty-three-row runtime checklist

    Research only. This starts from the exact user-tested count113/`0x1BF40` control and appends one complete native T12 clone as T113. Face `4:69:hp` is the only newly assigned face; the previous 32 assignments are preserved and rebased by exactly `0xB8`.

    ## Candidate

    - `{Path.GetFileName(exported.OutputCuePath)}`
    - BIN SHA-256: `{exported.OutputImageSha256}`

    ## Exact boundary

    - Texture count: 113 -> 114.
    - Total appended rows: 33 (`T81` through `T113`).
    - Count114 scene end: `0x{Count114SceneEnd:X8}`.
    - Lower arena: `0x{LowerPolygonArena:X8}`.
    - Scene-to-arena margin: `0x{Count114Margin:X}`.
    - Count115 would overlap this arena by `0x{Count115Overlap:X}`; do not extend this stride further.
    - Existing expanded level-data size remains `0x{ExpectedLevelDataBytes:X}`; no further WAD or executable relocation occurs.

    ## FAST-ENTRY sequence

    1. Fully stop the running game and cold-boot this exact CUE.
    2. Open Inventory with **Select**.
    3. Enter **R1, R2, L1, L2, R1, L1, R2, L2**.
    4. Press **Right**, then **Triangle** to enter Tree Tops.
    5. Report first whether Tree Tops loads. If it does, check movement, music, enemies, camera movement, and Inventory.

    Do **not** enter Save Fairy. Avoid every flight stage and flight-result screen. This artifact is not normal Create BIN or Beta V4 promotion.
    """);

Console.WriteLine(
    "Tree Tops count114 thirty-three-row diagnostic: PASSED OFFLINE");
Console.WriteLine(exported.OutputCuePath);
Console.WriteLine(exported.OutputImageSha256);

static FaceContract[] LoadCount113FaceContracts(
    string proofPath)
{
    using JsonDocument document =
        JsonDocument.Parse(File.ReadAllText(proofPath));
    JsonElement root = document.RootElement;
    string proofSha = root
        .GetProperty("Output")
        .GetProperty("Sha256")
        .GetString() ?? "";
    Assert(
        proofSha.Equals(
            ExpectedCount113BaseSha256,
            StringComparison.OrdinalIgnoreCase),
        "The count113 structural proof no longer names the exact base inherited by the 1BF40 control.");
    return root
        .GetProperty("Faces")
        .EnumerateArray()
        .Select(face => new FaceContract(
            face.GetProperty("RuntimeKey").GetString() ?? "",
            ParseHexInt64(
                face.GetProperty("OutputWadOffset")
                    .GetString() ?? ""),
            ParseHexUInt32(
                face.GetProperty("OutputWord")
                    .GetString() ?? "")))
        .ToArray();
}

static byte[] SliceLowRow(
    byte[] levelData,
    int textureCount,
    int textureId)
{
    Assert(
        textureId >= 0 && textureId < textureCount,
        "Low-detail texture row id is outside the table.");
    return levelData
        .AsSpan(
            8 +
            (textureId *
             NativeTerrainTextureRecordAppendBuilder
                 .LowDetailRecordBytes),
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
        "High-detail texture row id is outside the table.");
    int highStart =
        8 +
        (textureCount *
         NativeTerrainTextureRecordAppendBuilder
             .LowDetailRecordBytes);
    return levelData
        .AsSpan(
            highStart +
            (textureId *
             NativeTerrainTextureRecordAppendBuilder
                 .HighDetailRecordBytes),
            NativeTerrainTextureRecordAppendBuilder
                .HighDetailRecordBytes)
        .ToArray();
}

static void AssertWord(
    byte[] levelData,
    long levelDataWadOffset,
    long wordWadOffset,
    uint expected,
    string label)
{
    long relativeLong =
        wordWadOffset - levelDataWadOffset;
    Assert(
        relativeLong >= 0 &&
        relativeLong + sizeof(uint) <= levelData.Length,
        $"{label} is outside level data.");
    uint actual =
        BinaryPrimitives.ReadUInt32LittleEndian(
            levelData.AsSpan(
                checked((int)relativeLong),
                sizeof(uint)));
    Assert(
        actual == expected,
        $"{label} changed: expected 0x{expected:X8}, found 0x{actual:X8}.");
}

static byte[] UInt32Bytes(
    uint value)
{
    byte[] bytes = new byte[sizeof(uint)];
    BinaryPrimitives.WriteUInt32LittleEndian(
        bytes,
        value);
    return bytes;
}

static long ParseHexInt64(
    string value) =>
    long.Parse(
        value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? value[2..]
            : value,
        NumberStyles.HexNumber,
        CultureInfo.InvariantCulture);

static uint ParseHexUInt32(
    string value) =>
    uint.Parse(
        value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? value[2..]
            : value,
        NumberStyles.HexNumber,
        CultureInfo.InvariantCulture);

static string ResolveWorkspaceRoot(
    string? candidate)
{
    string current = Path.GetFullPath(
        candidate ?? Directory.GetCurrentDirectory());
    for (DirectoryInfo? directory = new(current);
         directory != null;
         directory = directory.Parent)
    {
        if (Directory.Exists(
                Path.Combine(
                    directory.FullName,
                    "src",
                    "Spyro.Editor.Core")) &&
            File.Exists(
                Path.Combine(
                    directory.FullName,
                    "spyro-level-catalog.json")))
        {
            return directory.FullName;
        }
    }
    return current;
}

static string Sha256File(
    string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream));
}

static string Sha256Bytes(
    ReadOnlySpan<byte> bytes) =>
    Convert.ToHexString(SHA256.HashData(bytes));

static void DeleteOutput(
    string prefix)
{
    foreach (string extension in new[] { ".bin", ".cue" })
    {
        string path = prefix + extension;
        if (File.Exists(path))
            File.Delete(path);
    }
}

static void Assert(
    bool condition,
    string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record FaceContract(
    string RuntimeKey,
    long CurrentWadOffset,
    uint Word);
