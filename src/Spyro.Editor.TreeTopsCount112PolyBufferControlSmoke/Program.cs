using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

const string ExpectedInputSha256 =
    "6A69DFBB44658E6BF0AADB19E236E52835FEE3290E18EEEE052CFB43FBE15CE8";
const string ExpectedOutputSha256 =
    "8BF6E5879F94246F657975892E8D0057686D982233CF228B018A4BFA83C932B6";
const long ExpectedImageLength = 661_547_040;
const int ExpectedSectorSize = 2352;
const int ExpectedUserOffset = 24;
const int ExpectedExecutableLba = 53878;
const int ExpectedExecutableSize = 0x66000;
const int ExpectedChangedByteCount = 7;

ExecutablePatch[] patches =
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
    "treetops-COUNT112-THIRTY-ONE-ROWS-T81-T111-CLONE-T12-THIRTY-ONE-VISIBLE-FACES-SECTOR-RELOCATION-DIAGNOSTIC-FAST-ENTRY");
string outputPrefix = Path.Combine(
    outputRoot,
    "treetops-COUNT112-POLY-BUFFER-1BFC0-CONTROL-FAST-ENTRY");
string inputImagePath = inputPrefix + ".bin";
string inputCuePath = inputPrefix + ".cue";
string outputImagePath = outputPrefix + ".bin";
string outputCuePath = outputPrefix + ".cue";
string proofPath = Path.Combine(
    outputRoot,
    "tree-tops-count112-poly-buffer-1bfc0-control-proof.json");
string checklistPath = Path.Combine(
    outputRoot,
    "tree-tops-count112-poly-buffer-1bfc0-control-runtime-checklist.md");

Directory.CreateDirectory(outputRoot);
Assert(
    File.Exists(inputImagePath) &&
    File.Exists(inputCuePath),
    "The exact proven count112 visible FAST-ENTRY BIN/CUE pair is missing.");
Assert(
    new FileInfo(inputImagePath).Length ==
        ExpectedImageLength &&
    Sha256File(inputImagePath).Equals(
        ExpectedInputSha256,
        StringComparison.OrdinalIgnoreCase),
    "The count112 input is not the exact guarded runtime-reported FAST-ENTRY image.");

DiscLayout layout = DetectLayout(inputImagePath);
Assert(
    layout.SectorSize == ExpectedSectorSize &&
    layout.UserOffset == ExpectedUserOffset,
    "The guarded count112 input is not the expected raw 2352-byte-sector BIN.");
RootFile executable = ReadRootFiles(
        inputImagePath,
        layout)
    .Single(IsExecutable);
Assert(
    executable.Lba == ExpectedExecutableLba &&
    executable.Size == ExpectedExecutableSize,
    $"The input main executable is {executable.Name} at LBA {executable.Lba} " +
    $"with size 0x{executable.Size:X}; expected LBA {ExpectedExecutableLba}, size 0x{ExpectedExecutableSize:X}.");

foreach (ExecutablePatch patch in patches)
{
    Assert(
        patch.FileOffset >= 0 &&
        patch.FileOffset + patch.Before.Length <=
            executable.Size &&
        patch.Before.Length ==
            patch.After.Length,
        $"Patch at executable offset 0x{patch.FileOffset:X} is outside the main executable.");
    byte[] actual = ReadDiscFileBytes(
        inputImagePath,
        layout,
        executable.Lba,
        patch.FileOffset,
        patch.Before.Length);
    Assert(
        actual.SequenceEqual(patch.Before),
        $"Patch preimage mismatch at executable offset 0x{patch.FileOffset:X}: " +
        $"expected {HexText(patch.Before)}, got {HexText(actual)}.");
}

string inputPhysicalPath = Path.GetFullPath(inputImagePath);
DateTime inputWriteTime =
    File.GetLastWriteTimeUtc(inputPhysicalPath);
long inputLength =
    new FileInfo(inputPhysicalPath).Length;
string inputSha256 = Sha256File(inputPhysicalPath);

string temporaryImagePath =
    outputImagePath + $".tmp-{Guid.NewGuid():N}";
try
{
    if (File.Exists(outputImagePath))
        File.Delete(outputImagePath);
    if (File.Exists(outputCuePath))
        File.Delete(outputCuePath);
    if (File.Exists(proofPath))
        File.Delete(proofPath);
    if (File.Exists(checklistPath))
        File.Delete(checklistPath);

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
        foreach (ExecutablePatch patch in patches)
        {
            long physicalOffset = ToPhysicalDiscOffset(
                layout,
                executable.Lba,
                patch.FileOffset);
            output.Position = physicalOffset;
            byte[] actual = new byte[patch.Before.Length];
            output.ReadExactly(actual);
            Assert(
                actual.SequenceEqual(patch.Before),
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
    if (File.Exists(temporaryImagePath))
        File.Delete(temporaryImagePath);
}

string inputCue = File.ReadAllText(
    inputCuePath,
    Encoding.ASCII);
string inputFileName =
    Path.GetFileName(inputImagePath);
Assert(
    inputCue.Contains(
        $"FILE \"{inputFileName}\" BINARY",
        StringComparison.Ordinal),
    "The guarded input CUE does not reference its exact count112 FAST-ENTRY BIN.");
string outputCue = inputCue.Replace(
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
    new FileInfo(outputImagePath).Length ==
        ExpectedImageLength,
    "The poly-buffer control changed the physical BIN length.");
DiscLayout outputLayout =
    DetectLayout(outputImagePath);
Assert(
    outputLayout == layout,
    "The poly-buffer control changed the disc layout.");
RootFile outputExecutable = ReadRootFiles(
        outputImagePath,
        outputLayout)
    .Single(IsExecutable);
Assert(
    outputExecutable == executable,
    "The poly-buffer control changed the ISO9660 executable record.");

foreach (ExecutablePatch patch in patches)
{
    byte[] actual = ReadDiscFileBytes(
        outputImagePath,
        outputLayout,
        outputExecutable.Lba,
        patch.FileOffset,
        patch.After.Length);
    Assert(
        actual.SequenceEqual(patch.After),
        $"Final readback mismatch at executable offset 0x{patch.FileOffset:X}.");
}

long[] expectedPhysicalDifferences = patches
    .SelectMany(patch =>
        Enumerable.Range(0, patch.Before.Length)
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
    "The final BIN differs from the proven count112 input outside the exact seven bytes in the four guarded executable instructions.");

string outputSha256 =
    Sha256File(outputImagePath);
Assert(
    outputSha256.Equals(
        ExpectedOutputSha256,
        StringComparison.OrdinalIgnoreCase),
    $"The exact output hash changed: expected {ExpectedOutputSha256}, got {outputSha256}.");

AssertInputPreserved();

var proof = new
{
    GeneratedAtUtc =
        DateTimeOffset.UtcNow,
    Status =
        "PASSED OFFLINE; DUCKSTATION RUNTIME RESULT PENDING",
    ResearchOnly = true,
    Count112Only = true,
    Count113 = false,
    NormalCreateBinIntegrated = false,
    NormalCreateBinAuthorized = false,
    DuckStationRuntimeProven = false,
    FlightStagesOrResultScreensProven = false,
    Input = new
    {
        ImagePath = inputImagePath,
        CuePath = inputCuePath,
        Sha256 = inputSha256,
        Length = inputLength,
        Count = 112,
        AppendedRows = 31,
        Recipe =
            "Exact runtime-reported visible count112 FAST-ENTRY diagnostic"
    },
    Output = new
    {
        ImagePath = outputImagePath,
        CuePath = outputCuePath,
        Sha256 = outputSha256,
        Length =
            new FileInfo(outputImagePath).Length,
        ChangedBytes =
            actualPhysicalDifferences.Length,
        ExecutableName =
            outputExecutable.Name,
        ExecutableLba =
            outputExecutable.Lba,
        ExecutableSize =
            $"0x{outputExecutable.Size:X}"
    },
    PolygonBufferControl = new
    {
        OriginalStride =
            "0x1C000",
        CandidateStride =
            "0x1BFC0",
        ImmediateSpanReductionBytes =
            "0x40",
        EffectiveLowerArenaMovementBytes =
            "0x80",
        OriginalLowerArena =
            "0x80187BB0",
        CandidateLowerArena =
            "0x80187C30",
        Count112ExistingSceneMargin =
            "0x74",
        Count113SceneEnd =
            "0x80187BF4",
        Count113ArithmeticMarginAgainstCandidateLowerArena =
            "0x3C",
        Count113StillNotConstructed =
            true,
        Scope =
            "Only the four source-bound normal-play main-executable instructions were changed. WAD, level data, models, scene, textures, FAST-ENTRY patch, and ISO layout are otherwise byte-identical."
    },
    Patches = patches.Select(patch => new
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
        ExactSevenByteWholeImageDiff = true,
        ExactOutputHash = outputSha256,
        SourcePreserved = true,
        CueTargetsOutputBin = true
    },
    RuntimeBoundary =
        "This is a count112 normal-play poly-buffer-size control only. Avoid all flight stages and flight-result screens. The 0x40 immediate-span reduction moves the relevant lower arena by 0x80 because the allocator applies that span twice. A successful Tree Tops load would still prove only this control, not count113, a thirty-second row, 50 slots, arbitrary payloads, or normal release."
};
File.WriteAllText(
    proofPath,
    JsonSerializer.Serialize(
        proof,
        new JsonSerializerOptions
        {
            WriteIndented = true
        }) +
    Environment.NewLine);

string checklist = $"""
    # Tree Tops count112 poly-buffer 0x1BFC0 control

    Research-only DuckStation diagnostic. This is **count112**, not count113. It starts from the exact already-reported-loading visible count112 FAST-ENTRY BIN and changes only four guarded normal-play executable instructions, reducing the normal polygon-buffer span from `0x1C000` to `0x1BFC0` (`0x40` bytes).

    ## Do not test

    - Do not enter any flight stage.
    - Do not enter or trigger any flight-result screen.
    - Those paths are explicitly unproven by this four-instruction control.

    ## Artifact

    - CUE: `{outputCuePath}`
    - BIN SHA-256: `{outputSha256}`
    - Input SHA-256: `{inputSha256}`
    - Whole-image difference: exactly {ExpectedChangedByteCount} byte(s), confined to four executable instructions.

    ## FAST-ENTRY code

    1. Open Inventory with **Select**.
    2. Enter **R1, R2, L1, L2, R1, L1, R2, L2**.
    3. Press **Right**, then **Triangle** to enter Tree Tops.

    ## Test

    1. Fully stop the current game and cold-boot the CUE above.
    2. Use the FAST-ENTRY code and enter Tree Tops.
    3. Confirm the flight-in completes, music continues, Spyro can move, and the opening thief/monkey activity is normal.
    4. Collect several gems, attack nearby actors, open Inventory, pause/unpause, die once, and reload Tree Tops.
    5. If it freezes or blinks, note the last visible frame and whether music continues. Stop there.

    ## Boundary

    Passing proves only that the exact normal-play `0x1C000 -> 0x1BFC0` polygon-buffer reduction remains compatible with the proven count112 recipe. It does not prove count113, a thirty-second texture row, arbitrary textures, 50 slots, flight/result paths, or normal Create BIN promotion.

    The instruction immediate changes by `0x40`, while the allocator applies that span twice and moves the relevant lower arena from `0x80187BB0` to `0x80187C30` (`+0x80`). A separate count113 image would end its scene at `0x80187BF4`, leaving an arithmetic margin of `0x3C`; this control intentionally does not construct or validate that row-32 path.
    """;
File.WriteAllText(
    checklistPath,
    checklist +
    Environment.NewLine);

Console.WriteLine(
    "Tree Tops count112 poly-buffer 0x1BFC0 control: PASSED OFFLINE");
Console.WriteLine(
    $"- CUE: {outputCuePath}");
Console.WriteLine(
    $"- BIN: {outputImagePath}");
Console.WriteLine(
    $"- SHA-256: {outputSha256}");
Console.WriteLine(
    $"- Diff: exactly {actualPhysicalDifferences.Length} byte(s) across four guarded executable instructions.");
Console.WriteLine(
    $"- Proof: {proofPath}");
Console.WriteLine(
    $"- Checklist: {checklistPath}");
Console.WriteLine(
    "- Research only; count112, not count113. Avoid flight stages and flight-result screens.");

void AssertInputPreserved()
{
    Assert(
        new FileInfo(inputPhysicalPath).Length ==
            inputLength &&
        File.GetLastWriteTimeUtc(
            inputPhysicalPath) ==
            inputWriteTime &&
        Sha256File(inputPhysicalPath).Equals(
            inputSha256,
            StringComparison.OrdinalIgnoreCase),
        "The smoke changed the immutable proven count112 input.");
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
        "Could not detect the input image layout.");
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
    for (int offset = 0;
         offset < root.Length;)
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

static long[] FindPhysicalDifferences(
    string beforePath,
    string afterPath)
{
    Assert(
        new FileInfo(beforePath).Length ==
            new FileInfo(afterPath).Length,
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
    while (absolute < before.Length)
    {
        int count = checked(
            (int)Math.Min(
                beforeBuffer.Length,
                before.Length - absolute));
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

static string Sha256File(
    string path)
{
    using FileStream stream =
        File.OpenRead(path);
    return Convert.ToHexString(
        SHA256.HashData(stream));
}

static uint ReadUInt32(
    byte[] bytes,
    int offset) =>
    BinaryPrimitives.ReadUInt32LittleEndian(
        bytes.AsSpan(
            offset,
            sizeof(uint)));

static string ResolveWorkspaceRoot(
    string? explicitRoot)
{
    if (!string.IsNullOrWhiteSpace(
            explicitRoot))
    {
        return Path.GetFullPath(
            explicitRoot);
    }
    DirectoryInfo? current =
        new(AppContext.BaseDirectory);
    while (current is not null)
    {
        if (Directory.Exists(
                Path.Combine(
                    current.FullName,
                    "src")) &&
            Directory.Exists(
                Path.Combine(
                    current.FullName,
                    "_local")))
        {
            return current.FullName;
        }
        current =
            current.Parent;
    }
    return Directory.GetCurrentDirectory();
}

static string HexText(
    byte[] bytes) =>
    string.Join(
        " ",
        bytes.Select(value =>
            value.ToString("X2")));

static void Assert(
    bool condition,
    string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

sealed record ExecutablePatch(
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

readonly record struct DiscLayout(
    int SectorSize,
    int UserOffset,
    int RootLba,
    int RootLength);

sealed record RootFile(
    string Name,
    int Lba,
    int Size);
