using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

const string ExpectedInputSha256 =
    "CE30E0F9C402F595C85922D6EFDF8331E5B323A71DB4811490B907F4028F49DC";
const string PendingOutputSha256 =
    "PENDING_FIRST_GUARDED_RUN";
const string ExpectedOutputSha256 =
    "F06A50C81F72A1742B08E7A18E6116D5E6B5E77AC2D2301E49458D9FA627035B";
const long ExpectedImageLength = 661_547_040;
const int ExpectedSectorSize = 2352;
const int ExpectedUserOffset = 24;
const int ExpectedExecutableLba = 53878;
const int ExpectedExecutableSize = 0x66000;
const int ExpectedChangedByteCount = 4;
const long FastEntryExecutableOffset = 0x1E034;
const string ExpectedFastEntryHex =
    "1C 06 84 AF 88 06 80 AF";
const uint Count113SceneEnd = 0x80187BF4;
const uint LowerArena = 0x80187D30;
const uint HigherArena = 0x801A3C70;
const uint PolygonStride = HigherArena - LowerArena;
const uint Count113SceneMargin =
    LowerArena - Count113SceneEnd;

ExecutablePatch[] patches =
[
    new(
        0x4BF3C,
        0x8005B73C,
        "40 40 63 34",
        "C0 40 63 34",
        "normal arena lower-span constant -0x1BFC0 -> -0x1BF40"),
    new(
        0x0AE08,
        0x8001A608,
        "C0 BF 84 34",
        "40 BF 84 34",
        "normal polygon span constant 0x1BFC0 -> 0x1BF40"),
    new(
        0x0CEAC,
        0x8001C6AC,
        "C0 BF 63 34",
        "40 BF 63 34",
        "normal polygon span constant 0x1BFC0 -> 0x1BF40"),
    new(
        0x0F590,
        0x8001ED90,
        "C0 BF A5 34",
        "40 BF A5 34",
        "normal polygon span constant 0x1BFC0 -> 0x1BF40")
];

bool captureOutputHash = args.Any(argument =>
    argument.Equals(
        "--capture-output-hash",
        StringComparison.Ordinal));
string? rootArgument = args.FirstOrDefault(argument =>
    !argument.StartsWith(
        "--",
        StringComparison.Ordinal));
string workspaceRoot =
    ResolveWorkspaceRoot(rootArgument);
string outputRoot = Path.Combine(
    workspaceRoot,
    "_local",
    "research",
    "tree-tops-private-texture-load-freeze-diagnostics");
string inputPrefix = Path.Combine(
    outputRoot,
    "treetops-COUNT113-THIRTY-TWO-ROWS-T81-T112-CLONE-T12-THIRTY-TWO-VISIBLE-FACES-POLY-1BFC0-SECTOR-RELOCATION-DIAGNOSTIC-FAST-ENTRY");
string outputPrefix = Path.Combine(
    outputRoot,
    "treetops-COUNT113-POLY-BUFFER-1BF40-CONTROL-FAST-ENTRY");
string inputImagePath =
    inputPrefix + ".bin";
string inputCuePath =
    inputPrefix + ".cue";
string outputImagePath =
    outputPrefix + ".bin";
string outputCuePath =
    outputPrefix + ".cue";
string proofPath = Path.Combine(
    outputRoot,
    "tree-tops-count113-poly-buffer-1bf40-control-proof.json");
string checklistPath = Path.Combine(
    outputRoot,
    "tree-tops-count113-poly-buffer-1bf40-control-runtime-checklist.md");

Directory.CreateDirectory(outputRoot);
Assert(
    File.Exists(inputImagePath) &&
    File.Exists(inputCuePath),
    "The exact proven count113 BIN/CUE pair is missing.");
Assert(
    !ExpectedOutputSha256.Equals(
        PendingOutputSha256,
        StringComparison.Ordinal) ||
    captureOutputHash,
    "ExpectedOutputSha256 is not frozen. Use --capture-output-hash only for the first guarded generation, then freeze the hash and rerun normally.");
Assert(
    FileLength(inputImagePath) ==
        ExpectedImageLength &&
    Sha256File(inputImagePath).Equals(
        ExpectedInputSha256,
        StringComparison.OrdinalIgnoreCase),
    "The count113 input is not the exact runtime-reported artifact.");
Assert(
    PolygonStride == 0x1BF40 &&
    Count113SceneMargin == 0x13C &&
    LowerArena == 0x80187D30 &&
    HigherArena == 0x801A3C70,
    "The 0x1BF40 arena or count113 margin arithmetic changed.");

DiscLayout layout =
    DetectLayout(inputImagePath);
Assert(
    layout.SectorSize ==
        ExpectedSectorSize &&
    layout.UserOffset ==
        ExpectedUserOffset,
    "The count113 input is not the expected raw 2352-byte-sector BIN.");
RootFile executable =
    ReadRootFiles(
        inputImagePath,
        layout)
        .Single(IsExecutable);
Assert(
    executable.Lba ==
        ExpectedExecutableLba &&
    executable.Size ==
        ExpectedExecutableSize,
    $"The input executable is {executable.Name} at LBA {executable.Lba} with size 0x{executable.Size:X}; expected LBA {ExpectedExecutableLba}, size 0x{ExpectedExecutableSize:X}.");

byte[] inputFastEntry =
    ReadDiscFileBytes(
        inputImagePath,
        layout,
        executable.Lba,
        FastEntryExecutableOffset,
        8);
Assert(
    inputFastEntry.SequenceEqual(
        ParseHex(ExpectedFastEntryHex)),
    "The proven count113 input no longer contains the exact eight-byte FAST-ENTRY patch.");

foreach (ExecutablePatch patch in patches)
{
    Assert(
        patch.FileOffset >= 0 &&
        patch.FileOffset +
            patch.Before.Length <=
            executable.Size &&
        patch.Before.Length ==
            patch.After.Length &&
        CountByteDifferences(
            patch.Before,
            patch.After) == 1,
        $"Patch at executable offset 0x{patch.FileOffset:X} is outside the executable or changes more than one byte.");
    byte[] actual =
        ReadDiscFileBytes(
            inputImagePath,
            layout,
            executable.Lba,
            patch.FileOffset,
            patch.Before.Length);
    Assert(
        actual.SequenceEqual(
            patch.Before),
        $"Patch preimage mismatch at executable offset 0x{patch.FileOffset:X}: expected {HexText(patch.Before)}, got {HexText(actual)}.");
}

string inputPhysicalPath =
    ResolvePhysicalPath(inputImagePath);
DateTime inputWriteTime =
    File.GetLastWriteTimeUtc(
        inputPhysicalPath);
long inputLength =
    FileLength(inputPhysicalPath);
string inputSha256 =
    Sha256File(inputPhysicalPath);

string temporaryImagePath =
    outputImagePath +
    $".tmp-{Guid.NewGuid():N}";
try
{
    DeleteIfExists(outputImagePath);
    DeleteIfExists(outputCuePath);
    DeleteIfExists(proofPath);
    DeleteIfExists(checklistPath);
    File.Copy(
        inputImagePath,
        temporaryImagePath,
        overwrite: false);
    using (FileStream output = new(
               temporaryImagePath,
               FileMode.Open,
               FileAccess.ReadWrite,
               FileShare.None))
    {
        foreach (ExecutablePatch patch in
                 patches)
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
                $"Temporary-image preimage mismatch at executable offset 0x{patch.FileOffset:X}.");
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
    DeleteIfExists(temporaryImagePath);
}

string inputCue =
    File.ReadAllText(
        inputCuePath,
        Encoding.ASCII);
string inputFileName =
    Path.GetFileName(inputImagePath);
Assert(
    inputCue.Contains(
        $"FILE \"{inputFileName}\" BINARY",
        StringComparison.Ordinal),
    "The input CUE does not reference its exact count113 BIN.");
string outputCue =
    inputCue.Replace(
        $"FILE \"{inputFileName}\" BINARY",
        $"FILE \"{Path.GetFileName(outputImagePath)}\" BINARY",
        StringComparison.Ordinal);
Assert(
    !outputCue.Contains(
        inputFileName,
        StringComparison.Ordinal) &&
    outputCue.Contains(
        Path.GetFileName(outputImagePath),
        StringComparison.Ordinal),
    "The output CUE filename rewrite was incomplete.");
File.WriteAllText(
    outputCuePath,
    outputCue,
    Encoding.ASCII);

Assert(
    FileLength(outputImagePath) ==
        ExpectedImageLength,
    "The 1BF40 control changed the physical BIN length.");
DiscLayout outputLayout =
    DetectLayout(outputImagePath);
Assert(
    outputLayout == layout,
    "The 1BF40 control changed the disc layout.");
RootFile outputExecutable =
    ReadRootFiles(
        outputImagePath,
        outputLayout)
        .Single(IsExecutable);
Assert(
    outputExecutable == executable,
    "The 1BF40 control changed the ISO9660 executable record.");

foreach (ExecutablePatch patch in patches)
{
    byte[] actual =
        ReadDiscFileBytes(
            outputImagePath,
            outputLayout,
            outputExecutable.Lba,
            patch.FileOffset,
            patch.After.Length);
    Assert(
        actual.SequenceEqual(
            patch.After),
        $"Final readback mismatch at executable offset 0x{patch.FileOffset:X}.");
}
byte[] outputFastEntry =
    ReadDiscFileBytes(
        outputImagePath,
        outputLayout,
        outputExecutable.Lba,
        FastEntryExecutableOffset,
        inputFastEntry.Length);
Assert(
    outputFastEntry.SequenceEqual(
        inputFastEntry),
    "The 1BF40 control changed FAST-ENTRY.");

long[] expectedPhysicalDifferences =
    patches
        .SelectMany(patch =>
            Enumerable.Range(
                    0,
                    patch.Before.Length)
                .Where(index =>
                    patch.Before[index] !=
                    patch.After[index])
                .Select(index =>
                    ToPhysicalDiscOffset(
                        layout,
                        executable.Lba,
                        patch.FileOffset +
                            index)))
        .Order()
        .ToArray();
long[] actualPhysicalDifferences =
    FindPhysicalDifferences(
        inputImagePath,
        outputImagePath);
Assert(
    expectedPhysicalDifferences.Length ==
        ExpectedChangedByteCount &&
    actualPhysicalDifferences.SequenceEqual(
        expectedPhysicalDifferences),
    "The final BIN differs from count113 outside the exact four normal-play executable bytes.");

string outputSha256 =
    Sha256File(outputImagePath);
if (!captureOutputHash)
{
    Assert(
        outputSha256.Equals(
            ExpectedOutputSha256,
            StringComparison.OrdinalIgnoreCase),
        $"Deterministic output SHA-256 changed: expected {ExpectedOutputSha256}, got {outputSha256}.");
}
AssertInputPreserved();

var proof = new
{
    GeneratedAtUtc =
        DateTimeOffset.UtcNow,
    Status = captureOutputHash
        ? "PASSED FIRST GUARDED GENERATION; OUTPUT SHA MUST BE FROZEN AND NORMAL RERUN COMPLETED"
        : "PASSED OFFLINE WITH FROZEN DETERMINISTIC SHA; DUCKSTATION RUNTIME RESULT PENDING",
    ResearchOnly = true,
    Count113Only = true,
    Count114 = false,
    NormalCreateBinIntegrated = false,
    NormalCreateBinAuthorized = false,
    DuckStationRuntimeProven = false,
    Input = new
    {
        ImagePath = inputImagePath,
        CuePath = inputCuePath,
        Sha256 = inputSha256,
        Length = inputLength,
        RuntimeEvidence =
            "Interactive user report: running, music, enemies, camera movement, and Inventory worked in this exact count113 input."
    },
    Output = new
    {
        ImagePath = outputImagePath,
        CuePath = outputCuePath,
        Sha256 = outputSha256,
        Length =
            FileLength(outputImagePath),
        ChangedBytes =
            actualPhysicalDifferences.Length,
        DeterministicShaFrozen =
            !captureOutputHash,
        ExecutableName =
            outputExecutable.Name,
        ExecutableLba =
            outputExecutable.Lba,
        ExecutableSize =
            $"0x{outputExecutable.Size:X}"
    },
    PolygonArena = new
    {
        PriorStride = "0x1BFC0",
        CandidateStride = "0x1BF40",
        ReductionBytes = "0x80",
        Count113SceneEnd =
            $"0x{Count113SceneEnd:X8}",
        LowerArena =
            $"0x{LowerArena:X8}",
        HigherArena =
            $"0x{HigherArena:X8}",
        Count113SceneMargin =
            $"0x{Count113SceneMargin:X}"
    },
    Patches = patches.Select(patch =>
        new
        {
            ExecutableFileOffset =
                $"0x{patch.FileOffset:X}",
            RuntimePc =
                $"0x{patch.RuntimePc:X8}",
            Before =
                HexText(patch.Before),
            After =
                HexText(patch.After),
            patch.Purpose,
            PhysicalChangedOffsets =
                Enumerable.Range(
                        0,
                        patch.Before.Length)
                    .Where(index =>
                        patch.Before[index] !=
                        patch.After[index])
                    .Select(index =>
                        $"0x{ToPhysicalDiscOffset(layout, executable.Lba, patch.FileOffset + index):X}")
        }),
    Verification = new
    {
        ExactInputHash = true,
        ExactInputLength = true,
        ExactExecutableLba = true,
        EveryInstructionPreimage = true,
        EveryInstructionReadback = true,
        ExactFourByteWholeImageDiff = true,
        ExactOutputHash =
            outputSha256,
        InputPreserved = true,
        FastEntryPreserved = true,
        CueTargetsOutputBin = true,
        WadLevelFacesTexturesUnchanged = true,
        SaveFairyPatched = false,
        FlightOrResultPathPatched = false,
        CoreOrReleaseIntegrated = false
    },
    RuntimeBoundary =
        "This is a count113 normal-play polygon-buffer control only. Avoid Save Fairy and all flight stages/result screens. It does not prove count114, arbitrary payloads, 50 slots, persistence/Build Safety, normal Create BIN, or release safety."
};
File.WriteAllText(
    proofPath,
    JsonSerializer.Serialize(
        proof,
        new JsonSerializerOptions
        {
            WriteIndented = true
        }) +
    Environment.NewLine,
    Encoding.UTF8);

string checklist = $"""
    # Tree Tops count113 polygon-buffer 1BF40 control runtime checklist

    Research only. This clones the exact runtime-reported count113 image and changes only four normal-play main-executable bytes, reducing the polygon stride from `0x1BFC0` to `0x1BF40`.

    ## Candidate

    - `{Path.GetFileName(outputCuePath)}`
    - BIN SHA-256: `{outputSha256}`

    ## Exact boundary

    - Count113 scene end: `0x{Count113SceneEnd:X8}`.
    - Lower arena: `0x{LowerArena:X8}`.
    - Higher arena: `0x{HigherArena:X8}`.
    - Arena stride: `0x{PolygonStride:X}`.
    - Scene-to-lower-arena margin: `0x{Count113SceneMargin:X}`.
    - Whole-image difference from proven count113: exactly four bytes.
    - WAD, level data, faces, textures, and FAST-ENTRY: byte-identical.

    ## FAST-ENTRY sequence

    1. Fully stop the running game and cold-boot the candidate CUE.
    2. Open Inventory with **Select**.
    3. Enter **R1, R2, L1, L2, R1, L1, R2, L2**.
    4. Press **Right**, then **Triangle** to enter Tree Tops.
    5. First report whether Tree Tops loads. If it freezes, note screen blinking and whether music begins, then stop.

    Do **not** enter Save Fairy. Avoid every flight stage and every flight-result screen. This artifact does not build or prove count114.
    """;
File.WriteAllText(
    checklistPath,
    checklist + Environment.NewLine,
    Encoding.UTF8);

AssertInputPreserved();
Console.WriteLine(
    captureOutputHash
        ? "Tree Tops count113 1BF40 control: PASSED FIRST GUARDED GENERATION"
        : "Tree Tops count113 1BF40 control: PASSED OFFLINE WITH FROZEN SHA");
Console.WriteLine(
    $"- CUE: {outputCuePath}");
Console.WriteLine(
    $"- BIN SHA-256: {outputSha256}");
Console.WriteLine(
    $"- Diff: exactly {actualPhysicalDifferences.Length} byte(s) across four guarded normal-play executable instructions.");
Console.WriteLine(
    $"- Arena: 0x{LowerArena:X8}->0x{HigherArena:X8}; count113 margin 0x{Count113SceneMargin:X}.");
Console.WriteLine(
    $"- Proof: {proofPath}");
Console.WriteLine(
    $"- Checklist: {checklistPath}");
Console.WriteLine(
    "- Research only. Avoid Save Fairy and all flight/result paths; count114 was not built.");

void AssertInputPreserved()
{
    Assert(
        FileLength(inputPhysicalPath) ==
            inputLength &&
        File.GetLastWriteTimeUtc(
            inputPhysicalPath) ==
            inputWriteTime &&
        Sha256File(inputPhysicalPath).Equals(
            inputSha256,
            StringComparison.OrdinalIgnoreCase),
        "The 1BF40 control changed its exact count113 input.");
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
        byte[] pvd =
            new byte[2048];
        stream.Position =
            pvdOffset;
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
        "Could not detect the input image layout.");
}

static IReadOnlyList<RootFile> ReadRootFiles(
    string imagePath,
    DiscLayout layout)
{
    byte[] root =
        ReadDiscFileBytes(
            imagePath,
            layout,
            layout.RootLba,
            0,
            layout.RootLength);
    List<RootFile> records = [];
    int offset = 0;
    while (offset < root.Length)
    {
        int recordLength =
            root[offset];
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
        offset +=
            recordLength;
    }
    Assert(
        records.Count > 0,
        "The ISO9660 root exposed no files.");
    return records;
}

static bool IsExecutable(
    RootFile record)
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
            "SCES",
            StringComparison.Ordinal) ||
        upper.StartsWith(
            "SLES",
            StringComparison.Ordinal) ||
        upper.EndsWith(
            ".EXE",
            StringComparison.Ordinal);
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
    long cursor =
        fileOffset;
    while (written <
           result.Length)
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
                result.Length -
                    written,
                2048 -
                    sectorOffset);
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

static long[] FindPhysicalDifferences(
    string beforePath,
    string afterPath)
{
    Assert(
        FileLength(beforePath) ==
            FileLength(afterPath),
        "Cannot compare differently sized BINs.");
    List<long> differences = [];
    using FileStream before =
        File.OpenRead(beforePath);
    using FileStream after =
        File.OpenRead(afterPath);
    byte[] beforeBuffer =
        new byte[1024 * 1024];
    byte[] afterBuffer =
        new byte[beforeBuffer.Length];
    long absolute = 0;
    while (absolute <
           before.Length)
    {
        int count =
            checked(
                (int)Math.Min(
                    beforeBuffer.Length,
                    before.Length -
                        absolute));
        before.ReadExactly(
            beforeBuffer.AsSpan(
                0,
                count));
        after.ReadExactly(
            afterBuffer.AsSpan(
                0,
                count));
        for (int index = 0;
             index < count;
             index++)
        {
            if (beforeBuffer[index] !=
                afterBuffer[index])
            {
                differences.Add(
                    absolute +
                    index);
            }
        }
        absolute += count;
    }
    return differences.ToArray();
}

static int CountByteDifferences(
    ReadOnlySpan<byte> before,
    ReadOnlySpan<byte> after)
{
    Assert(
        before.Length ==
            after.Length,
        "Cannot compare differently sized byte spans.");
    int differences = 0;
    for (int index = 0;
         index < before.Length;
         index++)
    {
        if (before[index] !=
            after[index])
        {
            differences++;
        }
    }
    return differences;
}

static string Sha256File(
    string path)
{
    using FileStream stream =
        File.OpenRead(path);
    return Convert.ToHexString(
        SHA256.HashData(stream));
}

static long FileLength(
    string path)
{
    using FileStream stream =
        File.OpenRead(path);
    return stream.Length;
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
         directory =
             directory.Parent)
    {
        if (Directory.Exists(
                Path.Combine(
                    directory.FullName,
                    "src",
                    "Spyro.Editor.Core")) &&
            Directory.Exists(
                Path.Combine(
                    directory.FullName,
                    "_local")))
        {
            return directory.FullName;
        }
    }
    return current;
}

static byte[] ParseHex(
    string value) =>
    Convert.FromHexString(
        value.Replace(
            " ",
            "",
            StringComparison.Ordinal));

static string HexText(
    byte[] bytes) =>
    string.Join(
        " ",
        bytes.Select(value =>
            value.ToString("X2")));

static uint ReadUInt32(
    byte[] bytes,
    int offset) =>
    BinaryPrimitives.ReadUInt32LittleEndian(
        bytes.AsSpan(
            offset,
            sizeof(uint)));

static void DeleteIfExists(
    string path)
{
    if (File.Exists(path))
        File.Delete(path);
}

static void Assert(
    bool condition,
    string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(
            message);
    }
}

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

internal readonly record struct DiscLayout(
    int SectorSize,
    int UserOffset,
    int RootLba,
    int RootLength);

internal sealed record RootFile(
    string Name,
    int Lba,
    int Size);
