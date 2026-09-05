using System.Buffers.Binary;
using System.Text;

internal static class GapFixture
{
    internal static byte[] Image(int sectorSize)
    {
        byte[] logical = new byte[64 * 2048];
        Span<byte> pvd = logical.AsSpan(16 * 2048, 2048);
        pvd[0] = pvd[6] = 1; "CD001"u8.CopyTo(pvd[1..]);
        BinaryPrimitives.WriteUInt16LittleEndian(pvd[128..], 2048);
        BinaryPrimitives.WriteUInt16BigEndian(pvd[130..], 2048);
        Both(pvd, 80, 64); Both(pvd, 132, 10);
        BinaryPrimitives.WriteUInt32LittleEndian(pvd[140..], 18);
        BinaryPrimitives.WriteUInt32BigEndian(pvd[148..], 19);
        Record(pvd[156..], "\0", 20, 2048, directory: true);
        Span<byte> terminator = logical.AsSpan(17 * 2048, 2048);
        terminator[0] = 255; terminator[6] = 1; "CD001"u8.CopyTo(terminator[1..]);
        Span<byte> littlePath = logical.AsSpan(18 * 2048, 10), bigPath = logical.AsSpan(19 * 2048, 10);
        littlePath[0] = bigPath[0] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(littlePath[2..], 20); littlePath[6] = 1;
        BinaryPrimitives.WriteUInt32BigEndian(bigPath[2..], 20); bigPath[7] = 1;
        Span<byte> root = logical.AsSpan(20 * 2048, 2048);
        int cursor = Record(root, "\0", 20, 2048, directory: true);
        cursor += Record(root[cursor..], "\u0001", 20, 2048, directory: true);
        cursor += Record(root[cursor..], "WAD.WAD;1", 37, 0x3000);
        cursor += Record(root[cursor..], "SCUS_942.28;1", 43, 0x1000);
        _ = Record(root[cursor..], "PETEXA0.STR;1", 48, 0x2000);
        Span<byte> wad = logical.AsSpan(37 * 2048, 0x3000);
        Word(wad, 0, 0x800); Word(wad, 4, 0x1800);
        Word(wad, 8, 0x2000); Word(wad, 12, 0x1000);
        Word(wad, 0x800, 0x800); Word(wad, 0x804, 0x800);
        Word(wad, 0x808, 0x1000); Word(wad, 0x80C, 0x800);
        for (int i = 0; i < 0x800; i++) { wad[0x1000 + i] = (byte)(i % 251); wad[0x1800 + i] = (byte)(i % 239); }
        Span<byte> executable = logical.AsSpan(43 * 2048, 0x1000);
        "PS-X EXE"u8.CopyTo(executable);
        Word(executable, 0x18, 0x80010000); Word(executable, 0x1C, 0x800);
        for (int i = 0x800; i < executable.Length; i++) executable[i] = (byte)(i % 233);
        if (sectorSize == 2048) return logical;
        byte[] image = new byte[64 * sectorSize];
        for (int lba = 0; lba < 64; lba++)
        {
            byte[] raw = new byte[2352];
            raw.AsSpan(1, 10).Fill(255);
            int frame = lba + 150;
            raw[12] = Bcd(frame / 4500); raw[13] = Bcd(frame / 75 % 60); raw[14] = Bcd(frame % 75); raw[15] = 2;
            raw[18] = raw[22] = (byte)(lba is >= 45 and < 48 ? 0x20 : lba is 42 or 44 ? 0x89 : 0x08);
            logical.AsSpan(lba * 2048, 2048).CopyTo(raw.AsSpan(24));
            Rebuild(raw);
            raw.AsSpan(sectorSize == 2352 ? 0 : 16, sectorSize).CopyTo(image.AsSpan(lba * sectorSize));
        }
        return image;
    }

    internal static void CheckForm1(byte[] image, int sectorSize, int lba, Action<bool, string> check)
        => CheckPhysicalSector(image.AsSpan(lba * sectorSize, sectorSize), sectorSize, lba, check);

    internal static void CheckPhysicalSector(ReadOnlySpan<byte> physical, int sectorSize, int lba, Action<bool, string> check)
    {
        if (sectorSize == 2048) return;
        byte[] actual = new byte[2352];
        physical.CopyTo(actual.AsSpan(sectorSize == 2352 ? 0 : 16));
        check((actual[18] & 0x20) == 0 && actual.AsSpan(16, 4).SequenceEqual(actual.AsSpan(20, 4)),
            "Promoted sector did not have duplicated Form1 subheaders.");
        if (sectorSize == 2352)
        {
            int frame = lba + 150;
            check(actual[0] == 0 && actual[11] == 0 && actual.AsSpan(1, 10).IndexOfAnyExcept((byte)255) < 0 &&
                  actual[12] == Bcd(frame / 4500) && actual[13] == Bcd(frame / 75 % 60) && actual[14] == Bcd(frame % 75) && actual[15] == 2,
                "Promoted sector lost its exact sync/MSF/mode header.");
        }
        byte[] expected = actual.ToArray(); Rebuild(expected);
        check(expected.AsSpan(2072).SequenceEqual(actual.AsSpan(2072)), "Promoted sector failed independent EDC/ECC arithmetic.");
    }

    internal static void Rebuild(Span<byte> raw)
    {
        if ((raw[18] & 0x20) != 0)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(raw[2348..], Edc(raw.Slice(16, 2332)));
            return;
        }
        BinaryPrimitives.WriteUInt32LittleEndian(raw[2072..], Edc(raw.Slice(16, 2056)));
        byte[] address = raw.Slice(12, 4).ToArray(); raw.Slice(12, 4).Clear();
        Ecc(raw[12..], 86, 24, 2, 86, raw.Slice(2076, 172));
        Ecc(raw[12..], 52, 43, 86, 88, raw.Slice(2248, 104)); address.CopyTo(raw[12..]);
    }
    private static uint Edc(ReadOnlySpan<byte> bytes)
    {
        uint value = 0;
        foreach (byte item in bytes) { value ^= item; for (int bit = 0; bit < 8; bit++) value = value >> 1 ^ ((value & 1) == 0 ? 0 : 0xD8018001u); }
        return value;
    }
    private static void Ecc(ReadOnlySpan<byte> source, int majors, int minors, int multiplier, int increment, Span<byte> output)
    {
        for (int major = 0; major < majors; major++)
        {
            int offset = major / 2 * multiplier + (major & 1); byte sum = 0, accumulated = 0;
            for (int minor = 0; minor < minors; minor++)
            { byte item = source[offset]; sum ^= item; accumulated = Multiply((byte)(accumulated ^ item), 2); offset = (offset + increment) % (majors * minors); }
            byte first = Multiply((byte)(Multiply(accumulated, 2) ^ sum), 0xF4);
            output[major] = first; output[major + majors] = (byte)(first ^ sum);
        }
    }
    private static byte Multiply(byte left, byte right)
    {
        int value = left, result = 0;
        for (int bits = right; bits != 0; bits >>= 1)
        { if ((bits & 1) != 0) result ^= value; value <<= 1; if ((value & 0x100) != 0) value ^= 0x11D; }
        return (byte)result;
    }
    internal static int Record(Span<byte> bytes, string name, int lba, int length, bool directory = false)
    {
        byte[] identifier = Encoding.ASCII.GetBytes(name);
        int size = 33 + identifier.Length + (identifier.Length % 2 == 0 ? 1 : 0);
        bytes[0] = (byte)size; Both(bytes, 2, lba); Both(bytes, 10, length);
        bytes[25] = (byte)(directory ? 2 : 0); bytes[28] = 1; bytes[31] = 1; bytes[32] = (byte)identifier.Length;
        identifier.CopyTo(bytes[33..]); return size;
    }
    internal static void Both(Span<byte> bytes, int offset, int value)
    { BinaryPrimitives.WriteInt32LittleEndian(bytes[offset..], value); BinaryPrimitives.WriteInt32BigEndian(bytes[(offset + 4)..], value); }
    private static void Word(Span<byte> bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes[offset..], value);
    private static byte Bcd(int value) => (byte)(value / 10 * 16 + value % 10);

    internal static void EditSector(byte[] image, int sectorSize, int lba, Action<byte[]> edit)
    {
        byte[] raw = new byte[2352];
        int skip = sectorSize == 2352 ? 0 : sectorSize == 2336 ? 16 : 24;
        image.AsSpan(lba * sectorSize, sectorSize).CopyTo(raw.AsSpan(skip));
        edit(raw);
        if (sectorSize != 2048) Rebuild(raw);
        raw.AsSpan(skip, sectorSize).CopyTo(image.AsSpan(lba * sectorSize));
    }

    internal static void AddDirectory(byte[] image, int sectorSize, int childFileLba = 55)
    {
        EditSector(image, sectorSize, 20, raw => Record(raw.AsSpan(24 + 202), "SOURCE", 24, 2048, directory: true));
        EditSector(image, sectorSize, 24, raw =>
        {
            Span<byte> data = raw.AsSpan(24, 2048); data.Clear();
            int offset = Record(data, "\0", 24, 2048, directory: true);
            offset += Record(data[offset..], "\u0001", 20, 2048, directory: true);
            Record(data[offset..], "LIVE.DAT;1", childFileLba, 2048);
        });
        EditSector(image, sectorSize, 16, raw => Both(raw.AsSpan(24), 132, 24));
        foreach (bool big in new[] { false, true })
            EditSector(image, sectorSize, big ? 19 : 18, raw =>
            {
                Span<byte> row = raw.AsSpan(24 + 10, 14); row.Clear(); row[0] = 6;
                if (big) { BinaryPrimitives.WriteUInt32BigEndian(row[2..], 24); row[7] = 1; }
                else { BinaryPrimitives.WriteUInt32LittleEndian(row[2..], 24); row[6] = 1; }
                "SOURCE"u8.CopyTo(row[8..]);
            });
    }
}
