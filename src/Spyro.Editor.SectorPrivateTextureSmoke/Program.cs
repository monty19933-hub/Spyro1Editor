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

const int DonorTextureId = 17;

string workspaceRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
string sourceCuePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).cue");
string overlayPath = Path.Combine(workspaceRoot, "editor-cache", "stonehill-runtime-scene-editor-overlay.json");
string outputRoot = Path.Combine(workspaceRoot, "_local", "research", "sector-private-texture-planner-compose");
Directory.CreateDirectory(outputRoot);
foreach (string stale in Directory.EnumerateFiles(outputRoot))
    File.Delete(stale);
string wadAnalysisPath = Path.Combine(outputRoot, "source-bound-wad-analysis.json");
await WadAnalysisBuilder.BuildAsync(sourceImagePath, wadAnalysisPath);

string sourceShaBefore = Sha256File(sourceImagePath);
long sourceLengthBefore = OpenedFileLength(sourceImagePath);
LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition stoneHill = catalog.FindByKey("stonehill")
    ?? throw new InvalidOperationException("Stone Hill is missing from the level catalog.");
LevelDefinition gnastysWorld = catalog.FindByKey("gnastysworld")
    ?? throw new InvalidOperationException("Gnasty's World is missing from the level catalog.");
NativeTerrainTextureRecordAppendSourceBinding binding =
    NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(sourceImagePath, stoneHill);
int privateTextureId = binding.ExpectedSourceTextureCount;
Assert(privateTextureId == 41, $"Stone Hill retail texture count changed from 41 to {privateTextureId}.");

GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
string sourceSearchPath = Path.Combine(outputRoot, "Stone-Hill-retail-source-search.json");
TerrainSourceSearchResult sourceSearch = await TerrainSourceSearchBuilder.BuildSourceDerivedAsync(
    new SourceDerivedTerrainSourceSearchRequest(sourceImagePath, sourceSearchPath, stoneHill, geometry));
HashSet<string> exactSourceKeys = sourceSearch.Report.Results
    .Where(result => result.FullSectorHits.Count == 1)
    .Select(result => result.Edit)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
TerrainPolygon targetFace = geometry.Polygons
    .Where(face => face.OriginalTextureId >= 0 &&
                   face.HasCompleteNativeHighPolyFacePayload &&
                   exactSourceKeys.Contains(face.RuntimeKey))
    .OrderBy(face => face.SectorIndex)
    .ThenBy(face => face.FaceIndex)
    .FirstOrDefault()
    ?? throw new InvalidOperationException("Stone Hill has no exact-source textured HP face for the selected-face smoke.");
int sourceTextureId = targetFace.OriginalTextureId;
targetFace.ApplyTextureOverride(privateTextureId);
string terrainEditsPath = Path.Combine(outputRoot, "Stone-Hill-selected-face-to-private-T41.json");
Assert(await TerrainEditStore.SaveAsync(
        terrainEditsPath,
        [targetFace],
        $"Retail Stone Hill face {targetFace.RuntimeKey} T{sourceTextureId} -> private T{privateTextureId}") == 1,
    "Could not save the one-face Stone Hill retail edit.");

TerrainPatchPlan ordinaryPlan = TerrainPatchExporter.BuildPlan(
    sourceImagePath,
    sourceCuePath,
    Path.Combine(outputRoot, "not-written.bin"),
    Path.Combine(outputRoot, "not-written.cue"),
    stoneHill,
    "",
    sourceSearchPath,
    terrainEditsPath);
TerrainPatch[] facePatches = ordinaryPlan.Patches
    .Where(patch => patch.RuntimeKey.Equals(targetFace.RuntimeKey, StringComparison.OrdinalIgnoreCase) &&
                    patch.Kind.Equals("texture-id-word3", StringComparison.OrdinalIgnoreCase))
    .ToArray();
Assert(facePatches.Length == 1,
    $"Normal terrain planner returned {facePatches.Length} texture-id patches for selected face {targetFace.RuntimeKey}.");
Assert(ordinaryPlan.Patches.Count == 1,
    $"The isolated texture-id edit unexpectedly produced {ordinaryPlan.Patches.Count} ordinary patches.");
TerrainPatch plannedFace = facePatches[0];
long retailFaceWadOffset = ParseOffset(plannedFace.WadRelativeOffset);
byte[] retailFaceBefore = ParseHexBytes(plannedFace.BeforeHexPreview);
byte[] retailFaceAfter = ParseHexBytes(plannedFace.AfterHexPreview);
Assert(retailFaceBefore.Length == 4 && retailFaceAfter.Length == 4,
    "Normal terrain planner did not return one four-byte native face word.");
uint beforeWord = BinaryPrimitives.ReadUInt32LittleEndian(retailFaceBefore);
uint afterWord = BinaryPrimitives.ReadUInt32LittleEndian(retailFaceAfter);
Assert((beforeWord & 0x7F) == sourceTextureId &&
       (afterWord & 0x7F) == privateTextureId &&
       (afterWord & 0xFFFFFF80u) == (beforeWord & 0xFFFFFF80u),
    "Normal terrain planner changed more than the selected face's seven-bit texture id.");

NativeTerrainTextureRecordExistingPatch ordinaryFacePatch = new(
    retailFaceWadOffset,
    retailFaceBefore,
    retailFaceAfter,
    plannedFace.Kind,
    plannedFace.RuntimeKey);
string outputPrefix = Path.Combine(
    outputRoot,
    $"Stone-Hill-face-{targetFace.SectorIndex}-{targetFace.FaceIndex}-T{privateTextureId}-from-Gnasty-T{DonorTextureId}-STATIC-ONLY-DO-NOT-RUN");
NativeTerrainTextureSectorPrivateRecordRequest request = new(
    sourceImagePath,
    sourceCuePath,
    outputPrefix,
    wadAnalysisPath,
    stoneHill,
    binding,
    ExistingRecordOverrides: [],
    SyntheticRecords:
    [
        new NativeTerrainTexturePrivateSyntheticRecord(
            $"appended-private-terrain-texture:T{privateTextureId:D3}",
            privateTextureId,
            gnastysWorld.Key,
            gnastysWorld.SourceWadEntry,
            DonorTextureId,
            MaterialTemplateTextureId: sourceTextureId)
    ],
    OrdinaryLevelDataPatches: [ordinaryFacePatch]);

byte[] staleFaceBefore = ordinaryFacePatch.Before.ToArray();
staleFaceBefore[0] ^= 0x80;
bool staleFaceRejected = !NativeTerrainTextureSectorPrivateRecordComposer.TryBuild(
    request with
    {
        OrdinaryLevelDataPatches =
        [
            ordinaryFacePatch with { Before = staleFaceBefore }
        ]
    },
    out _,
    out string staleFaceFailure);
Assert(staleFaceRejected && !string.IsNullOrWhiteSpace(staleFaceFailure),
    "A selected-face patch with a stale/non-retail preimage was accepted by the sector-private composer.");

NativeTerrainTextureSectorPrivateRecordPlan plan =
    NativeTerrainTextureSectorPrivateRecordComposer.BuildPlan(request);
Assert(plan.SourceBindingVerified && plan.GlobalPackingProofComplete &&
       plan.OriginalRowsInstalledExactly && plan.SyntheticRowsInstalledExactly &&
       plan.OrdinaryPatchesIncludedAndRelocated &&
       plan.TexturePagePatchesIncludedAndRelocated &&
       plan.StructuralSectorGrowthVerified &&
       plan.RequiresDuckStationRuntimeProof,
    "Sector-private composer omitted one or more source/global/row/face/page/structural proof gates.");
Assert(plan.Structural.Append.SourceTextureCount == privateTextureId &&
       plan.Structural.Append.OutputTextureCount == privateTextureId + 1 &&
       plan.Structural.Append.ResolvedRecords is [{ AssignedTextureId: 41 }],
    "Stone Hill table did not grow from T0..T40 to T0..T41.");
Assert(plan.Structural.SectorGrowthBytes == 0x800 &&
       plan.Structural.ExpandedWadSize == plan.Structural.OriginalWadSize + 0x800 &&
       plan.Structural.RelocatedExecutableLba == plan.Structural.OriginalExecutableLba + 1,
    "Stone Hill did not retain exact one-sector WAD/executable relocation.");
Assert(plan.GeneratedOriginalRowPatches.Count == plan.GlobalPacking.OriginalMovableRecords.Count * 2 &&
       plan.OriginalRowProofs.All(item => item.InstalledExactly),
    "Not every original movable Stone Hill record was installed as exact complete LQ/HQ rows.");
NativeTerrainTextureRecordRebasedPatchProof rebasedFace = plan.OrdinaryRelocatedPatchProofs.Single();
Assert(rebasedFace.SourceWadOffset == retailFaceWadOffset &&
       rebasedFace.OutputWadOffset ==
           plan.Structural.RelocatedLevelDataWadOffset +
           (retailFaceWadOffset - plan.Structural.OriginalLevelDataWadOffset) +
           NativeTerrainTextureRecordAppendBuilder.RecordGrowthBytes,
    "Selected face did not rebase from its retail preimage to the expanded/relocated level-data suffix.");

NativeTerrainTextureSectorPrivateRecordExportResult exported =
    await NativeTerrainTextureSectorPrivateRecordComposer.ExportAsync(plan);
Assert(exported.SourceImagePreserved && exported.ExactStructuralReadbackVerified &&
       exported.OrdinaryRelocatedPatchReadbackVerified &&
       exported.RuntimeTargetReadbackVerified && exported.GlobalLogicalReadbackVerified &&
       exported.AtomicRenameCompleted &&
       File.Exists(exported.OutputImagePath) && File.Exists(exported.OutputCuePath),
    "Sector-private export omitted final structural, face, table, logical, source, or atomic-write proof.");

byte[] finalFaceBytes = ReadWadBytes(exported.OutputImagePath, rebasedFace.OutputWadOffset, 4);
uint finalFaceWord = BinaryPrimitives.ReadUInt32LittleEndian(finalFaceBytes);
Assert(finalFaceWord == afterWord && (finalFaceWord & 0x7F) == privateTextureId,
    $"Final relocated face word is 0x{finalFaceWord:X8}, expected exact planner output 0x{afterWord:X8}/T{privateTextureId}.");
Assert(ReadWadBytes(sourceImagePath, retailFaceWadOffset, 4).SequenceEqual(retailFaceBefore),
    "Retail selected-face word changed during sector-private export.");
IReadOnlyList<TerrainTextureSlot> slots = TerrainPatchExporter.InspectTextureSlots(
    exported.OutputImagePath,
    stoneHill);
Assert(slots.Count == privateTextureId + 1 &&
       slots.Single(slot => slot.TextureId == privateTextureId) is
           { HasNormalDescriptors: true, HasCloseDescriptors: true },
    "Final BIN did not reparse Stone Hill private T41 as a complete native record.");
Assert(Sha256File(sourceImagePath).Equals(sourceShaBefore, StringComparison.OrdinalIgnoreCase) &&
       OpenedFileLength(sourceImagePath) == sourceLengthBefore,
    "Sector-private smoke modified the retail source BIN.");

string reportPath = outputPrefix + "-static-proof.json";
await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new
{
    Status = "static-proof-passed-runtime-test-not-performed",
    RuntimeClaim = false,
    RetailSource = new { Path = sourceImagePath, Sha256 = sourceShaBefore, Unchanged = true },
    Output = new { Bin = exported.OutputImagePath, Cue = exported.OutputCuePath, exported.OutputImageSha256 },
    Face = new
    {
        targetFace.RuntimeKey,
        SourceTextureId = sourceTextureId,
        TargetTextureId = privateTextureId,
        RetailWord = $"0x{beforeWord:X8}",
        FinalWord = $"0x{finalFaceWord:X8}",
        RetailWadOffset = $"0x{rebasedFace.SourceWadOffset:X}",
        RelocatedWadOffset = $"0x{rebasedFace.OutputWadOffset:X}"
    },
    Donor = new { Level = gnastysWorld.DisplayName, TextureId = DonorTextureId },
    plan.TexturePagePatchCount,
    plan.TexturePagePatchedByteCount,
    OriginalRows = plan.GlobalPacking.OriginalMovableRecords.Count,
    SyntheticRows = plan.GlobalPacking.SyntheticRecords.Count,
    SectorGrowthBytes = plan.Structural.SectorGrowthBytes,
    ExecutableLba = new
    {
        Original = plan.Structural.OriginalExecutableLba,
        Relocated = plan.Structural.RelocatedExecutableLba
    },
    ExactFinalReadback = true,
    DuckStationRuntimeProof = false
}, new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine("Stone Hill sector-private cross-level texture smoke: PASSED");
Console.WriteLine($"Selected face {targetFace.RuntimeKey}: T{sourceTextureId} -> private T{privateTextureId} from Gnasty's World T{DonorTextureId}.");
Console.WriteLine($"Face WAD offset: 0x{rebasedFace.SourceWadOffset:X} -> 0x{rebasedFace.OutputWadOffset:X}; exact final word 0x{finalFaceWord:X8}.");
Console.WriteLine($"WAD growth: +0x{plan.Structural.SectorGrowthBytes:X}; executable LBA {plan.Structural.OriginalExecutableLba}->{plan.Structural.RelocatedExecutableLba}.");
Console.WriteLine($"STATIC-ONLY DO-NOT-RUN BIN: {exported.OutputImagePath}");
Console.WriteLine($"STATIC-ONLY DO-NOT-RUN CUE: {exported.OutputCuePath}");
Console.WriteLine($"Static proof report: {reportPath}");
Console.WriteLine("No DuckStation runtime claim is made by this smoke.");

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
