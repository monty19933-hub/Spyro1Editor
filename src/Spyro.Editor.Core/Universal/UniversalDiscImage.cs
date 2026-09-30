using System.Buffers.Binary;
using System.Text;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Universal;

/// <summary>Reads WAD.WAD and the executable from a MODE2/2352 Spyro image and rewrites both,
/// growing the WAD and moving the executable when level entries grow. Every rewritten sector
/// receives a fresh Form 1 header, EDC and ECC.</summary>
public sealed class UniversalDiscImage
{
    private const int RawSector = 2352, UserOffset = 24, User = 2048;

    private UniversalDiscImage(string path, int wadLba, byte[] wad, int exeLba, byte[] exe, int nextFileLba)
    { Path = path; WadLba = wadLba; Wad = wad; ExeLba = exeLba; Exe = exe; NextFileLba = nextFileLba; }

    public string Path { get; }
    public int WadLba { get; }
    public byte[] Wad { get; }
    public int ExeLba { get; }
    public byte[] Exe { get; }
    public int NextFileLba { get; }

    public static UniversalDiscImage Read(string imagePath) => Read(imagePath, "SCUS_942.28", requireAdjacentExecutable: true);

    /// <summary>Read-only access to another regional image (the executable need not follow the WAD).</summary>
    public static UniversalDiscImage ReadAny(string imagePath, string executableName) => Read(imagePath, executableName, requireAdjacentExecutable: false);

    private static UniversalDiscImage Read(string imagePath, string executableName, bool requireAdjacentExecutable)
    {
        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        if (layout.SectorSize != RawSector || layout.UserOffset != UserOffset)
            throw new InvalidDataException("Universal Moby export requires a MODE2/2352 BIN image.");
        using FileStream fs = new(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var files = RootFiles(fs);
        var wad = files.Single(f => f.Name.StartsWith("WAD.WAD", StringComparison.OrdinalIgnoreCase));
        var exe = files.Single(f => f.Name.StartsWith(executableName, StringComparison.OrdinalIgnoreCase));
        int next = files.Where(f => f.Lba > exe.Lba).Select(f => f.Lba).DefaultIfEmpty(int.MaxValue).Min();
        if (requireAdjacentExecutable && exe.Lba != wad.Lba + (wad.Size + User - 1) / User)
            throw new InvalidDataException("The executable does not directly follow WAD.WAD in this image.");
        return new(imagePath, wad.Lba, DiscImage.ReadFileBytes(fs, layout, wad.Lba, 0, wad.Size), exe.Lba,
            DiscImage.ReadFileBytes(fs, layout, exe.Lba, 0, exe.Size), next);
    }

    public (int Offset, int Length) Entry(int index) => (
        (int)BinaryPrimitives.ReadUInt32LittleEndian(Wad.AsSpan(index * 8)),
        (int)BinaryPrimitives.ReadUInt32LittleEndian(Wad.AsSpan(index * 8 + 4)));

    public byte[] EntryBytes(int index) { var (o, l) = Entry(index); return Wad.AsSpan(o, l).ToArray(); }

    /// <summary>Rebuilds WAD.WAD with replaced entries, keeping every other entry byte-identical and in order.</summary>
    public byte[] RebuildWad(IReadOnlyDictionary<int, byte[]> replacements)
    {
        var entries = Enumerable.Range(0, 256).Select(Entry).ToArray();
        var order = Enumerable.Range(0, 256).Where(i => entries[i] != (0, 0) || replacements.ContainsKey(i))
            .OrderBy(i => entries[i].Offset).ThenBy(i => i).ToList();
        using MemoryStream m = new();
        m.Write(new byte[0x800]);
        var table = new (int Offset, int Length)[256];
        foreach (int i in order)
        {
            byte[] data = replacements.TryGetValue(i, out var r) ? r : Wad.AsSpan(entries[i].Offset, entries[i].Length).ToArray();
            table[i] = ((int)m.Position, data.Length);
            m.Write(data);
            m.Write(new byte[UniversalLevelData.Align(data.Length, 0x800) - data.Length]);
        }
        byte[] wad = m.ToArray();
        Wad.AsSpan(0, 0x800).CopyTo(wad);
        for (int i = 0; i < 256; i++)
        {
            if (entries[i] == (0, 0) && !replacements.ContainsKey(i)) continue;
            BinaryPrimitives.WriteInt32LittleEndian(wad.AsSpan(i * 8), table[i].Offset);
            BinaryPrimitives.WriteInt32LittleEndian(wad.AsSpan(i * 8 + 4), table[i].Length);
        }
        return wad;
    }

    /// <summary>Writes a new image: copy of this image with the new WAD at its LBA and the executable after it.</summary>
    public void WriteImage(string outputPath, byte[] wad, byte[] exe)
    {
        if (wad.Length % User != 0) throw new InvalidDataException("WAD image must be sector aligned.");
        int wadSectors = wad.Length / User, exeLba = WadLba + wadSectors, exeSectors = (exe.Length + User - 1) / User;
        if (exeLba + exeSectors > NextFileLba)
            throw new InvalidDataException("The grown WAD and executable exceed the free disc space before the movie files.");
        if (!string.Equals(System.IO.Path.GetFullPath(outputPath), System.IO.Path.GetFullPath(Path), StringComparison.Ordinal))
            File.Copy(Path, outputPath, overwrite: true);
        using FileStream fs = new(outputPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        WriteFile(fs, WadLba, wad);
        WriteFile(fs, exeLba, exe);
        // Old executable sectors beyond the new EXE become plain Form 1 zero sectors.
        int oldEnd = ExeLba + (Exe.Length + User - 1) / User;
        for (int lba = exeLba + exeSectors; lba < oldEnd; lba++) WriteSector(fs, lba, new byte[User], false);
        PatchRootRecords(fs, wad.Length, exeLba, exe.Length);
    }

    private static void WriteFile(FileStream fs, int lba, byte[] data)
    {
        int n = (data.Length + User - 1) / User;
        byte[] user = new byte[User];
        for (int i = 0; i < n; i++)
        {
            Array.Clear(user);
            data.AsSpan(i * User, Math.Min(User, data.Length - i * User)).CopyTo(user);
            WriteSector(fs, lba + i, user, i == n - 1);
        }
    }

    private static void WriteSector(FileStream fs, int lba, ReadOnlySpan<byte> user, bool lastOfFile)
    {
        byte[] s = new byte[RawSector];
        for (int k = 1; k < 11; k++) s[k] = 0xFF;
        int abs = lba + 150;
        s[12] = Bcd(abs / 75 / 60); s[13] = Bcd(abs / 75 % 60); s[14] = Bcd(abs % 75); s[15] = 2;
        byte sub = (byte)(lastOfFile ? 0x89 : 0x08);
        s[18] = sub; s[22] = sub;
        user.CopyTo(s.AsSpan(UserOffset));
        Mode2Form1.Finish(s);
        fs.Position = (long)lba * RawSector;
        fs.Write(s);
    }

    private static byte Bcd(int v) => (byte)(((v / 10) << 4) | (v % 10));

    private static List<(string Name, int Lba, int Size, int RecordOffset)> RootFiles(Stream fs)
    {
        byte[] pvd = ReadUser(fs, 16);
        int rootLba = BinaryPrimitives.ReadInt32LittleEndian(pvd.AsSpan(156 + 2));
        int rootLen = BinaryPrimitives.ReadInt32LittleEndian(pvd.AsSpan(156 + 10));
        List<(string, int, int, int)> list = [];
        for (int sec = 0; sec < (rootLen + User - 1) / User; sec++)
        {
            byte[] d = ReadUser(fs, rootLba + sec);
            int p = 0;
            while (p < User && d[p] != 0)
            {
                int l = d[p], nl = d[p + 32];
                string name = Encoding.ASCII.GetString(d, p + 33, nl);
                list.Add((name, BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(p + 2)), BinaryPrimitives.ReadInt32LittleEndian(d.AsSpan(p + 10)), (rootLba + sec) * User + p));
                p += l;
            }
        }
        return list;
    }

    private static byte[] ReadUser(Stream fs, int lba)
    {
        byte[] b = new byte[User];
        fs.Position = (long)lba * RawSector + UserOffset;
        fs.ReadExactly(b);
        return b;
    }

    private static void PatchRootRecords(FileStream fs, int wadSize, int exeLba, int exeSize)
    {
        var files = RootFiles(fs);
        foreach (int lba in files.Select(f => f.RecordOffset / User).Distinct())
        {
            byte[] d = ReadUser(fs, lba);
            foreach (var f in files.Where(f => f.RecordOffset / User == lba))
            {
                int p = f.RecordOffset % User;
                if (f.Name.StartsWith("WAD.WAD", StringComparison.OrdinalIgnoreCase)) Both(d, p + 10, wadSize);
                if (f.Name.StartsWith("SCUS_942.28", StringComparison.OrdinalIgnoreCase)) { Both(d, p + 2, exeLba); Both(d, p + 10, exeSize); }
            }
            fs.Position = (long)lba * RawSector;
            byte[] raw = new byte[RawSector]; fs.ReadExactly(raw);
            d.CopyTo(raw, UserOffset);
            Mode2Form1.Finish(raw);
            fs.Position = (long)lba * RawSector; fs.Write(raw);
        }
    }

    private static void Both(byte[] b, int o, int v)
    {
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(o), v);
        BinaryPrimitives.WriteInt32BigEndian(b.AsSpan(o + 4), v);
    }

    internal static class Mode2Form1
    {
        private static readonly byte[] F = new byte[256], B = new byte[256];
        private static readonly uint[] E = new uint[256];
        static Mode2Form1()
        {
            for (uint i = 0; i < 256; i++)
            {
                uint j = (i << 1) ^ ((i & 0x80) != 0 ? 0x11Du : 0);
                F[i] = (byte)j; B[i ^ (byte)j] = (byte)i;
                uint e = i;
                for (int k = 0; k < 8; k++) e = (e >> 1) ^ ((e & 1) != 0 ? 0xD8018001u : 0);
                E[i] = e;
            }
        }
        private static void Block(byte[] s, int src, int maj, int min, int mm, int mi, int dst)
        {
            int size = maj * min;
            for (int major = 0; major < maj; major++)
            {
                int index = (major >> 1) * mm + (major & 1);
                byte a = 0, b = 0;
                for (int minor = 0; minor < min; minor++)
                {
                    byte t = s[src + index]; index += mi; if (index >= size) index -= size;
                    a ^= t; b ^= t; a = F[a];
                }
                a = B[F[a] ^ b];
                s[dst + major] = a; s[dst + major + maj] = (byte)(a ^ b);
            }
        }
        public static void Finish(byte[] s)
        {
            uint edc = 0;
            for (int i = 16; i < 2072; i++) edc = (edc >> 8) ^ E[(edc ^ s[i]) & 0xFF];
            BinaryPrimitives.WriteUInt32LittleEndian(s.AsSpan(2072), edc);
            Span<byte> h = stackalloc byte[4]; s.AsSpan(12, 4).CopyTo(h); s.AsSpan(12, 4).Clear();
            Block(s, 12, 86, 24, 2, 86, 0x81C); Block(s, 12, 52, 43, 86, 88, 0x8C8);
            h.CopyTo(s.AsSpan(12));
        }
    }
}
