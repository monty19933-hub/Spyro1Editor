using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

string temporaryRoot = Path.Combine(Path.GetTempPath(), $"spyro-disc-integrity-{Guid.NewGuid():N}");
Directory.CreateDirectory(temporaryRoot);
List<string> failures = [];
int checks = 0;
try
{
    foreach ((int size, int offset) in new[] { (2352, 24), (2336, 8), (2048, 0) })
    {
        Run($"{size} single-sector partial write", () => CheckWrites(size, offset, [(31, 17)]));
        Run($"{size} crossing-sector partial write", () => CheckWrites(size, offset, [(2043, 2063)]));
        Run($"{size} sequential exporter writes", () => CheckWrites(size, offset, [(19, 13), (44, 12), (2050, 37)]));
        Run($"{size} empty write", () => CheckWrites(size, offset, [(0, 0)]));
        Run($"{size} negative logical offset", () => CheckRejected(size, offset, -1, 4));
        Run($"{size} past image", () => CheckRejected(size, offset, 4 * 2048 - 1, 2));
        Run($"{size} arithmetic overflow", () => CheckRejected(size, offset, long.MaxValue, 2));
        Run($"{size} negative file LBA", () => CheckRejected(size, offset, 0, 2, fileLba: -1));
        Run($"{size} excessive file LBA", () => CheckRejected(size, offset, 0, 2, fileLba: int.MaxValue));
        Run($"{size} truncated last sector", () => CheckRejected(size, offset, 3 * 2048 - 1, 2, truncate: true));
    }
    foreach ((int size, int offset) in new[] { (2352, 24), (2336, 8) })
    {
        Run($"{size} later Form2 sector rejects whole write", () => CheckRejected(size, offset, 2043, 2063,
            bytes => { bytes[3 * size + offset - 6] |= 0x20; bytes[3 * size + offset - 2] |= 0x20; }));
        Run($"{size} later mismatched subheader rejects whole write", () => CheckRejected(size, offset, 2043, 2063,
            bytes => bytes[3 * size + offset - 7] ^= 1));
    }
    Run("2352 Mode1 rejects before write", () => CheckRejected(2352, 24, 19, 7,
        bytes => bytes[2352 + 15] = 1));
    Run("unsupported layout rejects before write", () => CheckRejected(2352, 16, 19, 7));
    Run("2352 later wrong MSF rejects whole write", () => CheckRejected(2352, 24, 2043, 2063,
        bytes => bytes[3 * 2352 + 14] = 0x10));
    Run("2352 corrupt sync byte rejects before write", () => CheckRejected(2352, 24, 19, 7,
        bytes => bytes[2352 + 1] = 0));
    Run("2352 later corrupt sync end rejects whole write", () => CheckRejected(2352, 24, 2043, 2063,
        bytes => bytes[3 * 2352 + 11] = 0xFF));

    if (failures.Count > 0)
        throw new InvalidOperationException($"{failures.Count}/{checks} failed:\n" + string.Join('\n', failures));
    Console.WriteLine($"DISC_IMAGE_WRITE_INTEGRITY_SMOKE_OK checks={checks}; exact payload/complement, raw integrity, sequential writes, atomic boundary rejection; no game assets");
    if (args.Length == 1)
        await CheckRealMusicAsync(args[0]);
}
finally
{
    Directory.Delete(temporaryRoot, recursive: true);
}

async Task CheckRealMusicAsync(string sourcePath)
{
    string beforeHash = await HashAsync(sourcePath);
    string outputPrefix = Path.Combine(temporaryRoot, "music-acceptance");
    LevelMusicBatchPatchResult result = await LevelMusicPatchExporter.ExportBatchAsync(new LevelMusicBatchPatchRequest(
        sourcePath, DiscImageLocator.FindCueForImage(sourcePath), outputPrefix,
        [new(new LevelDefinition { Key = "artisans", DisplayName = "Artisans", LevelId = 10 }, 2),
         new(new LevelDefinition { Key = "stonehill", DisplayName = "Stone Hill", LevelId = 11 }, 17),
         new(new LevelDefinition { Key = "darkhollow", DisplayName = "Dark Hollow", LevelId = 12 }, 0),
         new(new LevelDefinition { Key = "townsquare", DisplayName = "Town Square", LevelId = 13 }, 31)], true));
    Require(result.WroteImage && result.Plan.EditedLevelCount == 4, "Actual music exporter did not publish four edited levels.");
    DiscLayout layout = DiscImage.DetectLayout(result.OutputImagePath);
    Require(layout.SectorSize == 2352 && layout.UserOffset == 24, "Actual acceptance expects the selected raw2352 source.");
    HashSet<long> payloadOffsets = [];
    HashSet<int> sectors = [];
    using (FileStream output = File.OpenRead(result.OutputImagePath))
    {
        foreach (LevelMusicPatch patch in result.Plan.Patches)
        {
            byte[] expected = patch.AfterHexPreview.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(value => Convert.ToByte(value, 16)).ToArray();
            byte[] actual = DiscImage.ReadFileBytes(output, layout, result.Plan.ExeLba, patch.ExeFileOffset, expected.Length);
            Require(actual.AsSpan().SequenceEqual(expected), $"Actual music patch {patch.Kind} differs from its plan.");
            for (int index = 0; index < expected.Length; index++)
            {
                long physical = DiscImage.ConvertFileOffsetToImageOffset(layout, result.Plan.ExeLba, patch.ExeFileOffset + index);
                payloadOffsets.Add(physical);
                sectors.Add(checked((int)(physical / 2352)));
            }
        }
        Require(RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(output, layout, sectors.Select(lba => (lba, 1))) == sectors.Count,
            "Actual music output failed raw-sector integrity.");
    }
    long changedBytes = 0;
    using (FileStream source = File.OpenRead(sourcePath))
    using (FileStream output = File.OpenRead(result.OutputImagePath))
    {
        Require(source.Length == output.Length, "Music export changed physical image length.");
        byte[] sourceChunk = new byte[128 * 1024];
        byte[] outputChunk = new byte[sourceChunk.Length];
        for (long position = 0; position < source.Length; position += sourceChunk.Length)
        {
            int count = (int)Math.Min(sourceChunk.Length, source.Length - position);
            source.ReadExactly(sourceChunk.AsSpan(0, count));
            output.ReadExactly(outputChunk.AsSpan(0, count));
            if (sourceChunk.AsSpan(0, count).SequenceEqual(outputChunk.AsSpan(0, count))) continue;
            for (int index = 0; index < count; index++)
            {
                if (sourceChunk[index] == outputChunk[index]) continue;
                long physical = position + index;
                bool parity = sectors.Contains(checked((int)(physical / 2352))) && physical % 2352 >= 2072;
                Require(payloadOffsets.Contains(physical) || parity, $"Actual music export changed an unrelated byte at {physical:X}.");
                changedBytes++;
            }
        }
    }
    string afterHash = await HashAsync(sourcePath);
    Require(beforeHash == afterHash, "Actual music export changed its source image.");
    string outputHash = await HashAsync(result.OutputImagePath);
    Console.WriteLine($"REAL_SOURCE_MUSIC_INTEGRITY_OK levels=4 patches={result.Plan.PatchCount} sectors={sectors.Count} changedBytes={changedBytes} sourceSha256={beforeHash} outputSha256={outputHash}; exact source complement preserved; temporary output removed on exit");

    static async Task<string> HashAsync(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream));
    }
}

void Run(string name, Action action)
{
    checks++;
    try { action(); }
    catch (Exception ex) { failures.Add($"{name}: {ex.Message}"); }
}

void CheckWrites(int size, int userOffset, (long Offset, int Count)[] writes)
{
    byte[] original = BuildImage(size);
    byte[] expected = original.ToArray();
    string path = Path.Combine(temporaryRoot, $"{Guid.NewGuid():N}.bin");
    File.WriteAllBytes(path, original);
    DiscLayout layout = new(size, userOffset, 0, 0);
    using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite))
    {
        foreach ((long logicalOffset, int count) in writes)
        {
            byte[] replacement = Enumerable.Range(0, count).Select(index => (byte)(index * 17 + 0x91)).ToArray();
            DiscImage.WriteFileBytes(stream, layout, 1, logicalOffset, replacement);
            for (int index = 0; index < count; index++)
            {
                long absolute = logicalOffset + index;
                expected[(1 + absolute / 2048) * size + userOffset + absolute % 2048] = replacement[index];
            }
            if (size != 2048 && count > 0)
            {
                for (long sector = 1 + logicalOffset / 2048; sector <= 1 + (logicalOffset + count - 1) / 2048; sector++)
                    RebuildExpected(expected.AsSpan(checked((int)sector * size), size), sector);
            }
        }
        stream.Flush();
    }
    byte[] actual = File.ReadAllBytes(path);
    if (size != 2048)
    {
        for (int sector = 0; sector < 5; sector++)
        {
            int rawStart = sector * size;
            int storedEdcOffset = rawStart + userOffset + 2048;
            uint stored = BinaryPrimitives.ReadUInt32LittleEndian(actual.AsSpan(storedEdcOffset, 4));
            uint calculated = ReferenceEdc(actual.AsSpan(rawStart + userOffset - 8, 2056));
            Require(stored == calculated, $"sector {sector} has stale EDC {stored:X8}, expected {calculated:X8}");
        }
    }
    Require(actual.AsSpan().SequenceEqual(expected), "physical readback differs from exact payload plus regenerated integrity; an untouched byte or parity differs");
}

void CheckRejected(int size, int userOffset, long logicalOffset, int count, Action<byte[]>? corrupt = null,
    int fileLba = 1, bool truncate = false)
{
    byte[] original = BuildImage(size);
    corrupt?.Invoke(original);
    if (truncate) original = original[..^1];
    string path = Path.Combine(temporaryRoot, $"{Guid.NewGuid():N}.bin");
    File.WriteAllBytes(path, original);
    bool rejected = false;
    using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite))
    {
        try { DiscImage.WriteFileBytes(stream, new DiscLayout(size, userOffset, 0, 0), fileLba, logicalOffset, new byte[count]); }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException or EndOfStreamException)
        { rejected = true; }
        stream.Flush();
    }
    Require(rejected, "invalid write was accepted");
    Require(File.ReadAllBytes(path).AsSpan().SequenceEqual(original), "rejected write mutated or extended the image");
}

static byte[] BuildImage(int size)
{
    byte[] image = new byte[5 * size];
    for (int sector = 0; sector < 5; sector++)
    {
        byte[] raw = new byte[2352];
        raw.AsSpan(1, 10).Fill(0xFF);
        raw[13] = 2;
        raw[14] = (byte)sector;
        raw[15] = 2;
        raw[16] = raw[20] = 1;
        raw[18] = raw[22] = 8;
        for (int i = 0; i < 2048; i++) raw[24 + i] = (byte)(sector * 47 + i * 23 + 7);
        raw = RawMode2Form1SectorIntegrity.RebuildRawSectorCopy(raw, sector);
        raw.AsSpan(size == 2352 ? 0 : size == 2336 ? 16 : 24, size).CopyTo(image.AsSpan(sector * size));
    }
    return image;
}

static void RebuildExpected(Span<byte> sector, long lba)
{
    byte[] raw = new byte[2352];
    if (sector.Length == 2352) sector.CopyTo(raw);
    else
    {
        raw.AsSpan(1, 10).Fill(0xFF);
        raw[13] = 2;
        raw[14] = (byte)lba;
        raw[15] = 2;
        sector.CopyTo(raw.AsSpan(16));
    }
    BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(2072, 4), ReferenceEdc(raw.AsSpan(16, 2056)));
    byte[] address = raw.AsSpan(12, 4).ToArray();
    raw.AsSpan(12, 4).Clear();
    ReferenceEcc(raw.AsSpan(12), 86, 24, 2, 86, raw.AsSpan(2076, 172));
    ReferenceEcc(raw.AsSpan(12), 52, 43, 86, 88, raw.AsSpan(2248, 104));
    address.CopyTo(raw, 12);
    raw.AsSpan(sector.Length == 2352 ? 0 : 16).CopyTo(sector);
}

// Independent finite-field arithmetic oracle instead of the production
// forward/backward lookup tables. In GF(256)/0x11D, 0xF4 is the inverse of 3.
static void ReferenceEcc(ReadOnlySpan<byte> source, int majors, int minors, int multiplier, int increment, Span<byte> parity)
{
    for (int major = 0; major < majors; major++)
    {
        int offset = (major / 2) * multiplier + (major & 1);
        byte sum = 0;
        byte accumulated = 0;
        for (int minor = 0; minor < minors; minor++)
        {
            byte value = source[offset];
            sum ^= value;
            accumulated = Multiply((byte)(accumulated ^ value), 2);
            offset = (offset + increment) % (majors * minors);
        }
        byte first = Multiply((byte)(Multiply(accumulated, 2) ^ sum), 0xF4);
        parity[major] = first;
        parity[major + majors] = (byte)(first ^ sum);
    }

    static byte Multiply(byte a, byte b)
    {
        int result = 0;
        int value = a;
        for (int mask = b; mask != 0; mask >>= 1)
        {
            if ((mask & 1) != 0) result ^= value;
            value <<= 1;
            if ((value & 0x100) != 0) value ^= 0x11D;
        }
        return (byte)result;
    }
}

// Deliberately independent bitwise EDC oracle, without the production lookup table.
static uint ReferenceEdc(ReadOnlySpan<byte> bytes)
{
    uint edc = 0;
    foreach (byte value in bytes)
    {
        edc ^= value;
        for (int bit = 0; bit < 8; bit++) edc = (edc >> 1) ^ ((edc & 1) != 0 ? 0xD8018001u : 0u);
    }
    return edc;
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
