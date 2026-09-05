using System.Buffers.Binary;
using System.Text;
using Spyro.Editor.Core.Exporting;

string root = Path.Combine(Path.GetTempPath(), $"spyro-disc-read-{Guid.NewGuid():N}");
Directory.CreateDirectory(root);
List<string> failures = [];
int checks = 0;
try
{
    foreach ((int size, int offset) in new[] { (2048, 0), (2352, 24), (2336, 8) })
    {
        Run($"{size} exact crossing-sector reads", () => WithFixture(size, offset, null, (stream, layout) =>
        {
            byte[] read = DiscImage.ReadFileBytes(stream, layout, 22, 2045, 2051);
            Require(read.Select((value, index) => value == (byte)((index + 2045) * 31 + 17)).All(value => value), "Logical read skipped or included physical overhead.");
            Require(DiscImage.ReadFileBytes(stream, layout, 25, 0, 0).Length == 0, "Empty EOF read failed.");
        }));
        Run($"{size} odd/even filename lengths plus sector padding", () => WithFixture(size, offset, null, (stream, layout) =>
        {
            Require(DiscImage.FindRootFileRecord(stream, layout, name => name == "A").Lba == 22, "Odd-length file record was rejected.");
            Require(DiscImage.FindRootFileRecord(stream, layout, name => name == "BB").Size == 4096, "Even-length padded record or zero-sector padding was rejected.");
        }));
        Run($"{size} negative file offset", () => RejectRead(size, offset, 22, -1, 1));
        Run($"{size} negative LBA", () => RejectRead(size, offset, -1, 0, 1));
        Run($"{size} negative length", () => RejectRead(size, offset, 22, 0, -1));
        Run($"{size} overlarge LBA", () => RejectRead(size, offset, int.MaxValue, 0, 1));
        Run($"{size} overflow offset", () => RejectRead(size, offset, 22, long.MaxValue, 2));
        Run($"{size} read length beyond image", () => RejectRead(size, offset, 24, 2047, 2));
        Run($"{size} huge read rejects before allocation", () => WithFixture(size, offset, null, (stream, layout) =>
        {
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            ExpectRejected(() => DiscImage.ReadFileBytes(stream, layout, 22, 0, int.MaxValue));
            Require(GC.GetAllocatedBytesForCurrentThread() - allocatedBefore < 64 * 1024,
                "An invalid large read allocated its requested buffer before validating the extent.");
        }));
        Run($"{size} malformed later name length", () => RejectDirectory(size, offset, logical => logical[21 * 2048 + 32] = 250));
        Run($"{size} mismatched file LBA endian", () => RejectDirectory(size, offset, logical => logical[21 * 2048 + 9] ^= 1));
        Run($"{size} mismatched file length endian", () => RejectDirectory(size, offset, logical => logical[21 * 2048 + 17] ^= 1));
        Run($"{size} out-of-image file extent", () => RejectDirectory(size, offset, logical => Both(logical.AsSpan(21 * 2048 + 2), 24)));
        Run($"{size} directory-record truncation", () => RejectDirectory(size, offset, logical => Both(logical.AsSpan(16 * 2048 + 166), 2048 + 35)));
        Run($"{size} mismatched root LBA endian", () => RejectLayout(size, offset, logical => logical[16 * 2048 + 165] ^= 1));
        Run($"{size} mismatched root length endian", () => RejectLayout(size, offset, logical => logical[16 * 2048 + 173] ^= 1));
        Run($"{size} unsigned root overflow", () => RejectLayout(size, offset, logical => Both(logical.AsSpan(16 * 2048 + 158), uint.MaxValue)));
        Run($"{size} unsigned root length overflow", () => RejectLayout(size, offset, logical => Both(logical.AsSpan(16 * 2048 + 166), uint.MaxValue)));
        Run($"{size} huge root length", () => RejectLayout(size, offset, logical => Both(logical.AsSpan(16 * 2048 + 166), 0x7FFF0000)));
        Run($"{size} invalid logical block size", () => RejectLayout(size, offset, logical => logical[16 * 2048 + 128] = 1));
    }
    if (failures.Count != 0) throw new InvalidOperationException($"{failures.Count}/{checks} failed:\n" + string.Join('\n', failures));
    Console.WriteLine($"DISC_IMAGE_READ_BOUNDS_SMOKE_OK checks={checks}; exact logical reads, checked bounds, streamed ISO directory, endian/name/extent validation; source-free");
}
finally { Directory.Delete(root, recursive: true); }

void Run(string name, Action action)
{
    checks++;
    try { action(); } catch (Exception ex) { failures.Add($"{name}: {ex.Message}"); }
}

void RejectRead(int size, int offset, int lba, long fileOffset, int length) =>
    WithFixture(size, offset, null, (stream, layout) => ExpectRejected(() => DiscImage.ReadFileBytes(stream, layout, lba, fileOffset, length)));

void RejectDirectory(int size, int offset, Action<byte[]> corrupt) =>
    WithFixture(size, offset, corrupt, (stream, layout) => ExpectRejected(() => DiscImage.FindRootFileRecord(stream, layout, name => name.StartsWith("BB", StringComparison.Ordinal))));

void RejectLayout(int size, int offset, Action<byte[]> corrupt)
{
    string path = WriteFixture(size, offset, corrupt);
    ExpectRejected(() => DiscImage.DetectLayout(path));
}

void WithFixture(int size, int offset, Action<byte[]>? corrupt, Action<FileStream, DiscLayout> action)
{
    string path = WriteFixture(size, offset, corrupt);
    DiscLayout layout = DiscImage.DetectLayout(path);
    using FileStream stream = File.OpenRead(path);
    action(stream, layout);
}

string WriteFixture(int size, int offset, Action<byte[]>? corrupt)
{
    byte[] logical = new byte[25 * 2048];
    Span<byte> pvd = logical.AsSpan(16 * 2048, 2048);
    pvd[0] = 1;
    Encoding.ASCII.GetBytes("CD001").CopyTo(pvd[1..]);
    pvd[6] = 1;
    BinaryPrimitives.WriteUInt16LittleEndian(pvd[128..], 2048);
    BinaryPrimitives.WriteUInt16BigEndian(pvd[130..], 2048);
    Record(pvd[156..], "\0", 20, 4096);
    Record(logical.AsSpan(20 * 2048), "A;1", 22, 4096);
    Record(logical.AsSpan(21 * 2048), "BB;1", 22, 4096);
    for (int i = 0; i < 3 * 2048; i++) logical[22 * 2048 + i] = (byte)(i * 31 + 17);
    corrupt?.Invoke(logical);
    byte[] image = new byte[25 * size];
    for (int sector = 0; sector < 25; sector++) logical.AsSpan(sector * 2048, 2048).CopyTo(image.AsSpan(sector * size + offset));
    string path = Path.Combine(root, $"{Guid.NewGuid():N}.bin");
    File.WriteAllBytes(path, image);
    return path;
}

static void Record(Span<byte> destination, string name, uint lba, uint length)
{
    byte[] identifier = Encoding.ASCII.GetBytes(name);
    destination[0] = checked((byte)(33 + identifier.Length + (identifier.Length % 2 == 0 ? 1 : 0)));
    Both(destination[2..], lba);
    Both(destination[10..], length);
    destination[28] = destination[31] = 1;
    destination[32] = (byte)identifier.Length;
    identifier.CopyTo(destination[33..]);
}

static void Both(Span<byte> destination, uint value)
{
    BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
    BinaryPrimitives.WriteUInt32BigEndian(destination[4..], value);
}

static void ExpectRejected(Action action)
{
    bool rejected = false;
    try { action(); }
    catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException or EndOfStreamException) { rejected = true; }
    Require(rejected, "Malformed/beyond-image input was accepted.");
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
