using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

const int SourceTextureId = 55;
const int AppendedTextureId = 68;
const int DonorTextureId = 17;
const long SelectedFaceWordWadOffset = 0x913FA4;

string workspaceRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
string sourceCuePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).cue");
string outputRoot = Path.Combine(workspaceRoot, "_local", "runtime", "private-terrain-texture-fixed-tail");
Directory.CreateDirectory(outputRoot);
foreach (string stale in Directory.EnumerateFiles(outputRoot))
    File.Delete(stale);

string sourceSha256 = Sha256File(sourceImagePath);
long sourceLength = new FileInfo(sourceImagePath).Length;
LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition artisans = catalog.FindByKey("artisans")
    ?? throw new InvalidOperationException("Artisans is missing from the level catalog.");
LevelDefinition gnastysWorld = catalog.FindByKey("gnastysworld")
    ?? throw new InvalidOperationException("Gnasty's World is missing from the level catalog.");

NativeTerrainTextureRecordAppendSourceBinding binding =
    NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(sourceImagePath, artisans);
Assert(binding.ExpectedSourceTextureCount == AppendedTextureId,
    $"The Artisans retail table no longer ends immediately before T{AppendedTextureId}.");

byte[] faceWordBefore = ReadWadBytes(sourceImagePath, SelectedFaceWordWadOffset, 4);
uint oldFaceWord = BinaryPrimitives.ReadUInt32LittleEndian(faceWordBefore);
Assert((oldFaceWord & 0x7F) == SourceTextureId,
    $"Artisans face 95:3:hp no longer uses expected source T{SourceTextureId}; word=0x{oldFaceWord:X8}.");
uint newFaceWord = (oldFaceWord & 0xFFFFFF80u) | AppendedTextureId;
byte[] faceWordAfter = new byte[4];
BinaryPrimitives.WriteUInt32LittleEndian(faceWordAfter, newFaceWord);
NativeTerrainTextureRecordExistingPatch facePatch = new(
    SelectedFaceWordWadOffset,
    faceWordBefore,
    faceWordAfter,
    "texture-id-word3-private-record",
    "95:3:hp");

NativeTerrainTextureFixedTailPrivateRecordRequest request = new(
    sourceImagePath,
    artisans,
    binding,
    Array.Empty<NativeTerrainTextureRelocationImport>(),
    [
        new NativeTerrainTexturePrivateSyntheticRecord(
            "private-terrain-texture:artisans:95:3:hp:gnastysworld:17",
            AppendedTextureId,
            gnastysWorld.Key,
            gnastysWorld.SourceWadEntry,
            DonorTextureId,
            SourceTextureId)
    ],
    [facePatch]);

NativeTerrainTextureFixedTailPrivateRecordPlan plan =
    NativeTerrainTextureFixedTailPrivateRecordComposer.BuildPlan(request);
Assert(plan.Append.SourceTextureCount == AppendedTextureId &&
       plan.Append.OutputTextureCount == AppendedTextureId + 1 &&
       plan.Append.Patch.IsFixedLength &&
       plan.FixedSubfileBoundaryPreserved &&
       plan.GlobalPackingProofComplete &&
       plan.OriginalRowsInstalledExactly &&
       plan.SyntheticRowsInstalledExactly &&
       plan.OrdinaryPatchesIncluded &&
       plan.PatchPreimagesVerified &&
       plan.CombinedPatchesDisjoint,
    "The fixed-tail combined plan omitted a required structural/global proof.");
Assert(plan.GlobalPacking.PackingProof.DescriptorPatches.Count == plan.GlobalPacking.OriginalDescriptorPatches.Count &&
       plan.GlobalPacking.TexturePagePatches.Count == plan.TexturePagePatchCount &&
       plan.CombinedPatches.Count == plan.TexturePagePatchCount + 1,
    "Synthetic global mode did not retain exact retail original-row preimages or collapse composed level data to one patch.");
Assert(plan.GeneratedOriginalRowPatches.Count == plan.GlobalPacking.OriginalMovableRecords.Count * 2 &&
       plan.OriginalRowProofs.All(item => item.InstalledExactly),
    "Not every original movable record was converted into complete LQ/HQ existing-row patches.");

NativeTerrainTextureRecordRebasedPatchProof rebasedFace = plan.Append.RebasedPatches
    .Single(item => item.RuntimeKey == facePatch.RuntimeKey && item.Kind == facePatch.Kind);
Assert(rebasedFace.SourceWadOffset == SelectedFaceWordWadOffset &&
       rebasedFace.OutputWadOffset == SelectedFaceWordWadOffset + NativeTerrainTextureRecordAppendBuilder.RecordGrowthBytes,
    "The selected face patch did not move by one exact 184-byte appended record.");
int outputFaceRelative = checked((int)(rebasedFace.OutputWadOffset - plan.Append.LevelDataWadOffset));
uint plannedFaceWord = BinaryPrimitives.ReadUInt32LittleEndian(plan.Append.Patch.After.AsSpan(outputFaceRelative, 4));
Assert((plannedFaceWord & 0x7F) == AppendedTextureId &&
       (plannedFaceWord & 0xFFFFFF80u) == (oldFaceWord & 0xFFFFFF80u),
    "The planned face word did not change only the seven-bit texture id from T55 to T68.");

int sourceSuffixStart = plan.Append.SourceTextureComponentByteLength;
int outputSuffixStart = plan.Append.OutputTextureComponentByteLength;
ReadOnlySpan<byte> sourceSuffix = plan.Append.Patch.Before.AsSpan(
    sourceSuffixStart,
    plan.Append.SourceUsedByteLength - sourceSuffixStart);
ReadOnlySpan<byte> outputSuffix = plan.Append.Patch.After.AsSpan(
    outputSuffixStart,
    plan.Append.OutputUsedByteLength - outputSuffixStart);
Assert(sourceSuffix.Length == outputSuffix.Length,
    "The shifted Artisans level-data suffix changed length.");
List<int> suffixDifferences = [];
for (int index = 0; index < sourceSuffix.Length; index++)
{
    if (sourceSuffix[index] != outputSuffix[index])
        suffixDifferences.Add(index);
}
int expectedSuffixDifference = checked((int)(SelectedFaceWordWadOffset - plan.Append.LevelDataWadOffset) - sourceSuffixStart);
Assert(suffixDifferences.SequenceEqual([expectedSuffixDifference]),
    $"Expected only face 95:3:hp's texture-id byte to differ in the shifted suffix, found {suffixDifferences.Count} difference(s).");

string outputPrefix = Path.Combine(
    outputRoot,
    "Artisans-Face-95-3-HP-T68-From-GnastysWorld-T17-RUNTIME-CANDIDATE");
NativeTerrainTextureFixedTailPrivateRecordExportResult exported =
    await NativeTerrainTextureFixedTailPrivateRecordComposer.ExportAsync(
        plan,
        sourceCuePath,
        outputPrefix);
Assert(exported.SourceImagePreserved &&
       exported.ExactPatchReadbackVerified &&
       exported.RuntimeTargetReadbackVerified &&
       exported.GlobalLogicalReadbackVerified &&
       exported.AtomicRenameCompleted &&
       File.Exists(exported.OutputImagePath) &&
       File.Exists(exported.OutputCuePath),
    "The fixed-tail candidate omitted final-BIN, logical, runtime-target, source-preservation, or atomic-write proof.");

byte[] finalFaceWordBytes = ReadWadBytes(exported.OutputImagePath, rebasedFace.OutputWadOffset, 4);
uint finalFaceWord = BinaryPrimitives.ReadUInt32LittleEndian(finalFaceWordBytes);
Assert((finalFaceWord & 0x7F) == AppendedTextureId &&
       (finalFaceWord & 0xFFFFFF80u) == (oldFaceWord & 0xFFFFFF80u),
    "The final BIN did not retain the isolated T55->T68 face assignment.");
IReadOnlyList<TerrainTextureSlot> decoded = TerrainPatchExporter.InspectTextureSlots(
    exported.OutputImagePath,
    artisans);
Assert(decoded.Count == AppendedTextureId + 1 && decoded[^1].TextureId == AppendedTextureId,
    "The ordinary final-BIN decoder did not read appended Artisans T68.");
Assert(Sha256File(sourceImagePath).Equals(sourceSha256, StringComparison.OrdinalIgnoreCase) &&
       new FileInfo(sourceImagePath).Length == sourceLength,
    "Fixed-tail private-record smoke modified the retail source BIN.");
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

Console.WriteLine("Fixed-tail private terrain texture record composer smoke passed.");
Console.WriteLine($"Global pack: {plan.GlobalPacking.OriginalMovableRecords.Count} original movable rows + {plan.GlobalPacking.SyntheticRecords.Count} synthetic row; {plan.TexturePagePatchCount:N0} page patches.");
Console.WriteLine($"Texture table: T0..T{plan.Append.SourceTextureCount - 1} -> T0..T{plan.Append.OutputTextureCount - 1}; fixed level-data boundary retained.");
Console.WriteLine($"Face 95:3:hp: WAD 0x{rebasedFace.SourceWadOffset:X} T{SourceTextureId} -> relocated WAD 0x{rebasedFace.OutputWadOffset:X} T{AppendedTextureId}; every other suffix byte preserved.");
Console.WriteLine($"DuckStation-proven runtime BIN: {exported.OutputImagePath}");
Console.WriteLine($"DuckStation-proven runtime CUE: {exported.OutputCuePath}");
Console.WriteLine("Exact one-row static/final-BIN proof and four-record normal-release promotion profile both match.");

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
        if (signature[0] == 1 && System.Text.Encoding.ASCII.GetString(signature[1..]) == "CD001")
            return (sectorSize, userOffset);
    }
    throw new InvalidDataException("Could not detect the smoke image's ISO9660 sector layout.");
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
