using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

const int SourceTextureId = 55;
const int PrivateTextureId = 68;
const int DonorTextureId = 17;
const string TargetFaceRuntimeKey = "95:3:hp";

string workspaceRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
string sourceCuePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).cue");
string overlayPath = Path.Combine(workspaceRoot, "editor-cache", "artisans-runtime-scene-editor-overlay.json");
string outputRoot = Path.Combine(workspaceRoot, "_local", "research", "fixed-tail-private-texture-planner-compose");
Directory.CreateDirectory(outputRoot);

string sourceShaBefore = Sha256File(sourceImagePath);
long sourceLengthBefore = OpenedFileLength(sourceImagePath);
LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition artisans = catalog.FindByKey("artisans")
    ?? throw new InvalidOperationException("Artisans is missing from the level catalog.");
LevelDefinition gnastysWorld = catalog.FindByKey("gnastysworld")
    ?? throw new InvalidOperationException("Gnasty's World is missing from the level catalog.");
NativeTerrainTextureRecordAppendSourceBinding binding =
    NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(sourceImagePath, artisans);
Assert(binding.ExpectedSourceTextureCount == PrivateTextureId,
    $"Artisans retail table no longer ends immediately before T{PrivateTextureId}.");

// Ask the normal terrain planner for the exact source-bound retail face word.
// No intermediate candidate BIN is written or used as composer input.
GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
TerrainPolygon targetFace = geometry.Polygons.Single(face =>
    face.RuntimeKey.Equals(TargetFaceRuntimeKey, StringComparison.OrdinalIgnoreCase));
Assert(targetFace.OriginalTextureId == SourceTextureId,
    $"Artisans {TargetFaceRuntimeKey} now starts at T{targetFace.OriginalTextureId}, expected T{SourceTextureId}.");
targetFace.ApplyTextureOverride(PrivateTextureId);
string terrainEditsPath = Path.Combine(outputRoot, "Artisans-retail-eye-T55-to-T68.json");
string sourceSearchPath = Path.Combine(outputRoot, "Artisans-retail-source-search.json");
Assert(await TerrainEditStore.SaveAsync(
        terrainEditsPath,
        [targetFace],
        "Retail Artisans face 95:3:hp T55 -> private T68") == 1,
    "Could not save the one-face retail terrain edit.");
TerrainSourceSearchResult sourceSearch = await TerrainSourceSearchBuilder.BuildSourceDerivedAsync(
    new SourceDerivedTerrainSourceSearchRequest(sourceImagePath, sourceSearchPath, artisans, geometry));
Assert(sourceSearch.Report.Results.Single(result =>
        result.Edit.Equals(TargetFaceRuntimeKey, StringComparison.OrdinalIgnoreCase)).FullSectorHits.Count == 1,
    "Artisans face 95:3:hp did not resolve to exactly one retail source sector.");
TerrainPatchPlan ordinaryPlan = TerrainPatchExporter.BuildPlan(
    sourceImagePath,
    sourceCuePath,
    Path.Combine(outputRoot, "not-written.bin"),
    Path.Combine(outputRoot, "not-written.cue"),
    artisans,
    "",
    sourceSearchPath,
    terrainEditsPath);
Assert(ordinaryPlan.SkippedEdits.All(item =>
        item.Equals(
            "collision: source-derived exact triangle scan found no matching source collision records for the edited terrain face(s).",
            StringComparison.Ordinal)),
    $"Normal terrain planner reported an unexpected skip: {string.Join(" | ", ordinaryPlan.SkippedEdits)}");
TerrainPatch plannedFace = ordinaryPlan.Patches.Single();
Assert(plannedFace.Kind.Equals("texture-id-word3", StringComparison.OrdinalIgnoreCase) &&
       plannedFace.RuntimeKey.Equals(TargetFaceRuntimeKey, StringComparison.OrdinalIgnoreCase) &&
       plannedFace.ByteLength == 4,
    "Normal terrain planner did not return exactly one four-byte eye texture-id patch.");
long retailFaceWadOffset = ParseOffset(plannedFace.WadRelativeOffset);
byte[] retailFaceBefore = ParseHexBytes(plannedFace.BeforeHexPreview);
byte[] retailFaceAfter = ParseHexBytes(plannedFace.AfterHexPreview);
uint beforeWord = BinaryPrimitives.ReadUInt32LittleEndian(retailFaceBefore);
uint afterWord = BinaryPrimitives.ReadUInt32LittleEndian(retailFaceAfter);
Assert(beforeWord == 0x01200237 && (beforeWord & 0x7F) == SourceTextureId,
    $"Retail planner read stale/wrong face bytes 0x{beforeWord:X8}; expected 0x01200237/T55.");
Assert((afterWord & 0x7F) == PrivateTextureId &&
       (afterWord & 0xFFFFFF80u) == (beforeWord & 0xFFFFFF80u),
    "Normal terrain planner changed more than the eye word's seven-bit texture id.");

NativeTerrainTextureRecordExistingPatch ordinaryFacePatch = new(
    retailFaceWadOffset,
    retailFaceBefore,
    retailFaceAfter,
    plannedFace.Kind,
    plannedFace.RuntimeKey);
NativeTerrainTextureFixedTailPrivateRecordPlan plan =
    NativeTerrainTextureFixedTailPrivateRecordComposer.BuildPlan(
        new NativeTerrainTextureFixedTailPrivateRecordRequest(
            sourceImagePath,
            artisans,
            binding,
            ExistingRecordOverrides: [],
            SyntheticRecords:
            [
                new NativeTerrainTexturePrivateSyntheticRecord(
                    "appended-private-terrain-texture:T068",
                    PrivateTextureId,
                    gnastysWorld.Key,
                    gnastysWorld.SourceWadEntry,
                    DonorTextureId,
                    MaterialTemplateTextureId: SourceTextureId)
            ],
            OrdinaryLevelDataPatches: [ordinaryFacePatch]));
Assert(Path.GetFullPath(plan.SourceImagePath) == Path.GetFullPath(sourceImagePath),
    "Composer plan is not bound directly to the retail BIN.");
Assert(plan.SourceBindingVerified && plan.GlobalPackingProofComplete &&
       plan.OriginalRowsInstalledExactly && plan.SyntheticRowsInstalledExactly &&
       plan.OrdinaryPatchesIncluded && plan.PatchPreimagesVerified &&
       plan.CombinedPatchesDisjoint && plan.FixedSubfileBoundaryPreserved,
    "Composer omitted one or more atomic/static proof gates.");
Assert(plan.Append.SourceTextureCount == PrivateTextureId &&
       plan.Append.OutputTextureCount == PrivateTextureId + 1 &&
       plan.Append.ResolvedRecords is [{ AssignedTextureId: PrivateTextureId }],
    "Composed table did not grow from 68 to 69 with private T68.");
NativeTerrainTextureRecordRebasedPatchProof rebasedFace = plan.Append.RebasedPatches.Single(item =>
    item.Kind.Equals(plannedFace.Kind, StringComparison.OrdinalIgnoreCase) &&
    item.RuntimeKey.Equals(TargetFaceRuntimeKey, StringComparison.OrdinalIgnoreCase));
Assert(rebasedFace.SourceWadOffset == retailFaceWadOffset &&
       rebasedFace.OutputWadOffset == retailFaceWadOffset + NativeTerrainTextureRecordAppendBuilder.RecordGrowthBytes,
    "Retail eye word was not rebased by exactly one 184-byte texture record.");
Assert(plan.CombinedPatches.Count == plan.TexturePagePatchCount + 1,
    "Atomic plan must contain global page patches plus one complete level-data patch.");

string outputPrefix = Path.Combine(
    outputRoot,
    "Artisans-RETAIL-ATOMIC-T68-Gnasty-T17-eye-95-3-hp-RUNTIME-CANDIDATE");
NativeTerrainTextureFixedTailPrivateRecordExportResult exported =
    await NativeTerrainTextureFixedTailPrivateRecordComposer.ExportAsync(plan, sourceCuePath, outputPrefix);
Assert(exported.SourceImagePreserved && exported.ExactPatchReadbackVerified &&
       exported.RuntimeTargetReadbackVerified && exported.GlobalLogicalReadbackVerified &&
       exported.AtomicRenameCompleted,
    "Composer export omitted source, patch, runtime-target, logical, or atomic-write proof.");
Assert(OpenedFileLength(exported.OutputImagePath) == sourceLengthBefore &&
       Sha256File(sourceImagePath).Equals(sourceShaBefore, StringComparison.OrdinalIgnoreCase),
    "Atomic export changed retail source length/hash or final image length.");
string recordAgentCandidatePath = Path.Combine(
    workspaceRoot,
    "_local",
    "runtime",
    "private-terrain-texture-fixed-tail",
    "Artisans-Face-95-3-HP-T68-From-GnastysWorld-T17-RUNTIME-CANDIDATE.bin");
Assert(File.Exists(recordAgentCandidatePath),
    "Run Spyro.Editor.FixedTailPrivateRecordSmoke first so the independent candidate can be byte-compared.");
Assert(Sha256File(exported.OutputImagePath).Equals(
        Sha256File(recordAgentCandidatePath),
        StringComparison.OrdinalIgnoreCase),
    "Normal-planner and hardcoded-retail-word composer candidates differ byte-for-byte.");

byte[] finalFaceBytes = ReadWadBytes(exported.OutputImagePath, rebasedFace.OutputWadOffset, 4);
uint finalFaceWord = BinaryPrimitives.ReadUInt32LittleEndian(finalFaceBytes);
Assert(finalFaceWord == afterWord && (finalFaceWord & 0x7F) == PrivateTextureId,
    $"Final rebased eye word is 0x{finalFaceWord:X8}, expected exact planner output 0x{afterWord:X8}/T68.");
Assert(ReadWadBytes(sourceImagePath, retailFaceWadOffset, 4).SequenceEqual(retailFaceBefore),
    "Retail eye word no longer matches its original source-bound T55 bytes.");
IReadOnlyList<TerrainTextureSlot> slots = TerrainPatchExporter.InspectTextureSlots(exported.OutputImagePath, artisans);
Assert(slots.Count == PrivateTextureId + 1 &&
       slots.Single(slot => slot.TextureId == PrivateTextureId) is
           { HasNormalDescriptors: true, HasCloseDescriptors: true },
    "Final BIN did not reparse private T68 as one complete native texture record.");
NativeTerrainTextureRuntimeControlAudit runtime =
    NativeTerrainTextureRuntimeControlScanner.Inspect(exported.OutputImagePath, artisans);
Assert(runtime.Complete && runtime.TextureCount == PrivateTextureId + 1 &&
       runtime.IsRuntimePersistentTarget(PrivateTextureId),
    "Final BIN runtime-control scanner did not prove private T68 persistent.");

// Regression: the immutable donor source is semantically necessary when one
// final record consumes the retail form of another record that is overridden
// in the same batch. Using the candidate itself as donor would inspect the
// overridden T55 and reject the correct appended T68; retail-source readback
// must instead accept it.
NativeTerrainTextureFixedTailPrivateRecordPlan immutableDonorPlan =
    NativeTerrainTextureFixedTailPrivateRecordComposer.BuildPlan(
        new NativeTerrainTextureFixedTailPrivateRecordRequest(
            sourceImagePath,
            artisans,
            binding,
            ExistingRecordOverrides:
            [
                new NativeTerrainTextureRelocationImport(
                    SourceTextureId,
                    gnastysWorld.SourceWadEntry,
                    DonorTextureId,
                    NativeTerrainTextureRelocationEditStore.CompleteDescriptorTier,
                    PreserveTargetDescriptorMaterial: true)
            ],
            SyntheticRecords:
            [
                new NativeTerrainTexturePrivateSyntheticRecord(
                    "immutable-donor-regression:T068",
                    PrivateTextureId,
                    artisans.Key,
                    artisans.SourceWadEntry,
                    SourceTextureId,
                    MaterialTemplateTextureId: SourceTextureId)
            ],
            OrdinaryLevelDataPatches: []));
NativeTerrainTextureFixedTailPrivateRecordExportResult immutableDonorExport =
    await NativeTerrainTextureFixedTailPrivateRecordComposer.ExportAsync(
        immutableDonorPlan,
        sourceCuePath,
        Path.Combine(outputRoot, "Artisans-immutable-retail-donor-regression"));
NativeTerrainTextureGlobalRepackPackedRecord[] immutableDonorRows =
    immutableDonorPlan.GlobalPacking.OriginalMovableRecords
        .Concat(immutableDonorPlan.GlobalPacking.SyntheticRecords)
        .OrderBy(row => row.TargetTextureId)
        .ToArray();
Assert(!NativeTerrainTextureGlobalRepackerResearch.TryVerifyCandidateLogicalReadback(
        immutableDonorExport.OutputImagePath,
        artisans,
        immutableDonorRows,
        out string candidateSelfFailure) &&
       candidateSelfFailure.Contains($"T{PrivateTextureId}", StringComparison.OrdinalIgnoreCase),
    "The immutable-donor regression did not expose candidate-self donor contamination at appended T68.");
Assert(NativeTerrainTextureGlobalRepackerResearch.TryVerifyCandidateLogicalReadback(
        immutableDonorExport.OutputImagePath,
        artisans,
        immutableDonorRows,
        sourceImagePath,
        out string immutableDonorFailure),
    $"The correct immutable retail donor readback failed: {immutableDonorFailure}");

AppendedPrivateTerrainTexturePromotionProfile promotionProfile =
    AppendedPrivateTerrainTexturePromotionProfileRegistry.Profiles.Single(profile =>
        profile.NormalizedTargetLevelKey == artisans.Key &&
        profile.WriterKind == NativeTerrainTexturePrivateImageWriterKind.FixedTail);
Assert(promotionProfile.RuntimeProven &&
       promotionProfile.RuntimeProvenMaxAppendedRecords == 4 &&
       promotionProfile.StaticProofOutputSha256.Equals(
           exported.OutputImageSha256,
           StringComparison.OrdinalIgnoreCase),
    "The exact one-row Artisans readback baseline no longer matches the four-record normal-release profile.");

string reportPath = outputPrefix + "-runtime-proof.json";
await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new
{
    Status = "runtime-proven-normal-release-up-to-four-private-records",
    RetailSource = new { Path = sourceImagePath, Sha256 = sourceShaBefore, Unchanged = true },
    Output = new { Bin = exported.OutputImagePath, Cue = exported.OutputCuePath, exported.OutputImageSha256 },
    Face = new
    {
        RuntimeKey = TargetFaceRuntimeKey,
        RetailWord = $"0x{beforeWord:X8}",
        FinalWord = $"0x{finalFaceWord:X8}",
        RetailWadOffset = $"0x{rebasedFace.SourceWadOffset:X}",
        FinalWadOffset = $"0x{rebasedFace.OutputWadOffset:X}"
    },
    Donor = new { Level = gnastysWorld.DisplayName, TextureId = DonorTextureId },
    ByteIdenticalToHardcodedRetailWordCandidate = true,
    plan.TexturePagePatchCount,
    CombinedPatchCount = plan.CombinedPatches.Count,
    ExactFinalReadback = true,
    DuckStationRuntimeProof = true,
    promotionProfile.EvidenceId,
    promotionProfile.RuntimeProvenMaxAppendedRecords
}, new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine("Normal-planner + fixed-tail private texture atomic smoke: PASSED");
Console.WriteLine($"Retail face {TargetFaceRuntimeKey}: 0x{beforeWord:X8}/T55 at WAD 0x{rebasedFace.SourceWadOffset:X}.");
Console.WriteLine($"Final face: 0x{finalFaceWord:X8}/T68 at WAD 0x{rebasedFace.OutputWadOffset:X} (+184).");
Console.WriteLine($"DuckStation TEST CUE: {exported.OutputCuePath}");
Console.WriteLine($"Runtime promotion report: {reportPath}");
Console.WriteLine("Immutable retail donor regression: candidate-self rejected, retail source accepted.");
Console.WriteLine("The exact Artisans writer is enabled in normal Create BIN for up to four appended private texture records.");

static long ParseOffset(string value)
{
    string trimmed = value.Trim();
    return trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
        ? long.Parse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
        : long.Parse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture);
}

static byte[] ParseHexBytes(string value) => value
    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(part => Convert.ToByte(part, 16))
    .ToArray();

static byte[] ReadWadBytes(string imagePath, long wadOffset, int length)
{
    (int sectorSize, int userOffset) = DetectLayout(imagePath);
    byte[] result = new byte[length];
    using FileStream stream = File.OpenRead(imagePath);
    int written = 0;
    long fileOffset = wadOffset;
    while (written < length)
    {
        int sectorOffset = checked((int)(fileOffset % 2048));
        int sector = checked(37 + (int)(fileOffset / 2048));
        int count = Math.Min(length - written, 2048 - sectorOffset);
        stream.Position = ((long)sector * sectorSize) + userOffset + sectorOffset;
        stream.ReadExactly(result.AsSpan(written, count));
        written += count;
        fileOffset += count;
    }
    return result;
}

static (int SectorSize, int UserOffset) DetectLayout(string imagePath)
{
    using FileStream stream = File.OpenRead(imagePath);
    Span<byte> signature = stackalloc byte[6];
    foreach ((int sectorSize, int userOffset) in new[] { (2048, 0), (2352, 24), (2336, 8) })
    {
        long offset = (16L * sectorSize) + userOffset;
        if (offset + signature.Length > stream.Length)
            continue;
        stream.Position = offset;
        stream.ReadExactly(signature);
        if (signature[0] == 1 && Encoding.ASCII.GetString(signature[1..]) == "CD001")
            return (sectorSize, userOffset);
    }
    throw new InvalidDataException("Could not detect the smoke image's ISO9660 sector layout.");
}

static string Sha256File(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream));
}

static long OpenedFileLength(string path)
{
    using FileStream stream = File.OpenRead(path);
    return stream.Length;
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
