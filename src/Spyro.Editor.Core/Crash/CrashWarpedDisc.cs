using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.Core.Crash;

/// <summary>Read-only access to the user's Crash Bandicoot: Warped (USA) disc (SCUS-94244). Level files are
/// S0..S3/*.NSF: 64 KB chunks (magic 0x1234; type 0 holds entries, type 1 is one texture page). Entries
/// (magic 0x0100FFFF) carry an ID, a type and a list of items. Nothing is written to the disc.</summary>
public sealed class CrashWarpedDisc
{
    public const string BootName = "SCUS_942.44";
    /// <summary>The level file whose texture page Cr10T holds every box face (crate, TNT, Nitro).</summary>
    public const string BoxLevelFile = "S0/S000000A.NSF";
    public const string BoxLevelSha256 = "96e5d5208e64528a2976bef0a7a9d33b530d1cb77c03d4c9f6684ab77dea59ba";
    public const int ChunkBytes = 0x10000;

    private readonly Dictionary<string, (int Lba, int Size)> _files;
    private readonly DiscLayout _layout;

    public CrashWarpedDisc(string imagePath)
    {
        ImagePath = imagePath;
        using var fs = File.OpenRead(imagePath);
        _layout = DiscImage.DetectLayout(fs);
        _files = new(StringComparer.OrdinalIgnoreCase);
        Walk(fs, _layout.RootExtent, _layout.RootLength, "", 0);
        if (!_files.TryGetValue("SYSTEM.CNF", out var cnf) ||
            !System.Text.Encoding.ASCII.GetString(DiscImage.ReadFileBytes(fs, _layout, cnf.Lba, 0, cnf.Size)).Contains(BootName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("This is not the Crash Bandicoot: Warped (USA) disc (SCUS-94244).");
    }

    public string ImagePath { get; }
    public IReadOnlyCollection<string> Files => _files.Keys;

    public byte[] ReadFile(string name)
    {
        if (!_files.TryGetValue(name, out var f)) throw new FileNotFoundException($"The Crash disc has no file {name}.");
        using var fs = File.OpenRead(ImagePath);
        return DiscImage.ReadFileBytes(fs, _layout, f.Lba, 0, f.Size);
    }

    /// <summary>The box level file, checked against the known original bytes.</summary>
    public CrashNsf BoxLevel()
    {
        byte[] bytes = ReadFile(BoxLevelFile);
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != BoxLevelSha256)
            throw new InvalidDataException("The Crash disc's box level file differs from the original Crash Bandicoot: Warped (USA) release.");
        return new CrashNsf(bytes);
    }

    /// <summary>A level file checked against the original bytes (so converted art is always the same).</summary>
    public CrashNsf Level(string file, string sha256)
    {
        byte[] bytes = ReadFile(file);
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != sha256)
            throw new InvalidDataException($"The Crash disc's {file} differs from the original Crash Bandicoot: Warped (USA) release.");
        return new CrashNsf(bytes);
    }

    private void Walk(Stream fs, int lba, int size, string prefix, int depth)
    {
        if (depth > 8) return;
        byte[] dir = DiscImage.ReadFileBytes(fs, _layout, lba, 0, size);
        for (int o = 0; o < dir.Length;)
        {
            int length = dir[o];
            if (length == 0) { o = (o / 2048 + 1) * 2048; continue; }
            if (length < 34 || o + length > dir.Length) break;
            int extent = BinaryPrimitives.ReadInt32LittleEndian(dir.AsSpan(o + 2));
            int bytes = BinaryPrimitives.ReadInt32LittleEndian(dir.AsSpan(o + 10));
            bool folder = (dir[o + 25] & 2) != 0;
            int nameLength = dir[o + 32];
            if (nameLength > 0 && o + 33 + nameLength <= dir.Length)
            {
                var raw = dir.AsSpan(o + 33, nameLength);
                if (!(nameLength == 1 && raw[0] <= 1))
                {
                    string name = System.Text.Encoding.Latin1.GetString(raw).Split(';')[0];
                    if (folder) Walk(fs, extent, bytes, prefix + name + "/", depth + 1);
                    else _files[prefix + name] = (extent, bytes);
                }
            }
            o += length;
        }
    }
}

/// <summary>One Crash 3 level file: its entries by name and its texture pages by name.</summary>
public sealed class CrashNsf
{
    private const string EidChars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_!";
    private readonly Dictionary<string, CrashEntry> _entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> _pages = new(StringComparer.Ordinal);

    public CrashNsf(byte[] bytes)
    {
        for (int c = 0; c + CrashWarpedDisc.ChunkBytes <= bytes.Length; c += CrashWarpedDisc.ChunkBytes)
        {
            var chunk = bytes.AsSpan(c, CrashWarpedDisc.ChunkBytes);
            if (BinaryPrimitives.ReadUInt16LittleEndian(chunk) != 0x1234) continue;
            int type = BinaryPrimitives.ReadUInt16LittleEndian(chunk[2..]);
            if (type == 1) { _pages[Name(BinaryPrimitives.ReadUInt32LittleEndian(chunk[4..]))] = chunk.ToArray(); continue; }
            if (type is not (0 or 3)) continue;     // 0: ordinary entries, 3: sound effects
            int count = BinaryPrimitives.ReadInt32LittleEndian(chunk[8..]);
            if (count is < 0 or > 1024) continue;
            for (int i = 0; i < count; i++)
            {
                int start = BinaryPrimitives.ReadInt32LittleEndian(chunk[(16 + 4 * i)..]), end = BinaryPrimitives.ReadInt32LittleEndian(chunk[(20 + 4 * i)..]);
                if (start < 0 || end > chunk.Length || end - start < 16) continue;
                var e = chunk[start..end];
                if (BinaryPrimitives.ReadUInt32LittleEndian(e) != 0x0100FFFF) continue;
                uint id = BinaryPrimitives.ReadUInt32LittleEndian(e[4..]);
                int entryType = BinaryPrimitives.ReadInt32LittleEndian(e[8..]), items = BinaryPrimitives.ReadInt32LittleEndian(e[12..]);
                if (items is < 0 or > 256 || 16 + 4 * (items + 1) > e.Length) continue;
                List<byte[]> list = [];
                for (int k = 0; k < items; k++)
                {
                    int a = BinaryPrimitives.ReadInt32LittleEndian(e[(16 + 4 * k)..]), b = BinaryPrimitives.ReadInt32LittleEndian(e[(20 + 4 * k)..]);
                    if (a < 0 || b > e.Length || b < a) { list.Clear(); break; }
                    list.Add(e[a..b].ToArray());
                }
                _entries[Name(id)] = new CrashEntry(Name(id), entryType, list);
            }
        }
    }

    public static string Name(uint id) =>
        new(new[] { 25, 19, 13, 7, 1 }.Select(shift => EidChars[(int)(id >> shift) & 63]).ToArray());

    public CrashEntry Entry(string name) => _entries.TryGetValue(name, out var e) ? e
        : throw new InvalidDataException($"The Crash level file has no entry {name}.");
    public byte[] Page(string name) => _pages.TryGetValue(name, out var p) ? p
        : throw new InvalidDataException($"The Crash level file has no texture page {name}.");
    public IEnumerable<CrashEntry> Entries => _entries.Values;
    public bool Has(string name, int type) => _entries.TryGetValue(name, out var e) && e.Type == type;

    /// <summary>A sound effect (entry type 12, one item of SPU ADPCM) as a native one-shot: frames up to the one
    /// flagged as the end; the trailing silent loop frame Crash adds is dropped. Crash plays these at 11025 Hz.</summary>
    public byte[] Sound(string name)
    {
        var entry = Entry(name);
        if (entry.Type != 12 || entry.Items.Count != 1) throw new InvalidDataException($"{name} is not a Crash sound effect.");
        byte[] data = entry.Items[0];
        for (int frame = 0; frame + 16 <= data.Length; frame += 16)
        {
            byte flags = data[frame + 1];
            if ((data[frame] >> 4) > 4 || (data[frame] & 15) > 12) throw new InvalidDataException($"{name} has an unsupported ADPCM frame.");
            if (flags == 1) return data[..(frame + 16)];
            if (flags != 0) break;
        }
        throw new InvalidDataException($"{name} is not a simple one-shot sound.");
    }
    public IReadOnlyDictionary<string, byte[]> Pages => _pages;

    /// <summary>The distinct 4-bpp texture regions of a model (TGEO, item 3 = 12-byte GPU texture records), in
    /// first-use order. Every box face is one 32x32 region of the shared box page.</summary>
    public IReadOnlyList<CrashTexture> ModelTextures(string model, string page)
    {
        var entry = Entry(model);
        if (entry.Type != 2 || entry.Items.Count < 4) throw new InvalidDataException($"{model} is not a Crash model.");
        byte[] pixels = Page(page), records = entry.Items[3];
        List<CrashTexture> result = [];
        HashSet<string> seen = [];
        for (int k = 0; k + 12 <= records.Length; k += 12)
        {
            var r = records.AsSpan(k, 12);
            // A face mapped with a different corner order (a flipped copy) counts as its own texture.
            string key = string.Join(",", new[] { (r[0], r[1]), (r[4], r[5]), (r[8], r[9]), (r[10], r[11]) }.Order()) + "/" + BinaryPrimitives.ReadUInt16LittleEndian(r[2..]);
            var t = CrashTexture.Read(pixels, r);
            if (t != null && seen.Add(key)) result.Add(t);
        }
        return result;
    }
}

public sealed record CrashEntry(string Name, int Type, IReadOnlyList<byte[]> Items);

/// <summary>A 4-bpp texture region with its 16-colour palette. Rows hold two texels per byte (low nibble first).
/// A texture page is 256 halfwords wide and 128 rows tall.</summary>
public sealed record CrashTexture(int U, int V, int Width, int Height, int ClutX, int ClutY, byte[] Texels, byte[] Clut)
{
    public static CrashTexture? Read(byte[] page, ReadOnlySpan<byte> record)
    {
        ushort clut = BinaryPrimitives.ReadUInt16LittleEndian(record[2..]), tpage = BinaryPrimitives.ReadUInt16LittleEndian(record[6..]);
        if ((tpage >> 7 & 3) != 0) return null;
        int[] us = [record[0], record[4], record[8], record[10]], vs = [record[1], record[5], record[9], record[11]];
        int u0 = us.Min(), u1 = us.Max(), v0 = vs.Min(), v1 = vs.Max(), w = u1 - u0 + 1, h = v1 - v0 + 1;
        int tx = (tpage & 15) * 64, cx = (clut & 63) * 16, cy = clut >> 6 & 0x1FF;
        if (w % 2 != 0 || tx + (u1 >> 2) >= 256 || v1 >= 128 || cx + 16 > 256 || cy >= 128) return null;
        ushort Half(int x, int y) => BinaryPrimitives.ReadUInt16LittleEndian(page.AsSpan(y * 512 + x * 2));
        int Index(int u, int v) => Half(tx + u / 4, v) >> (u % 4 * 4) & 15;
        byte[] texels = new byte[w / 2 * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x += 2)
                texels[y * (w / 2) + x / 2] = (byte)(Index(u0 + x, v0 + y) | Index(u0 + x + 1, v0 + y) << 4);
        byte[] palette = new byte[32];
        for (int i = 0; i < 16; i++) BinaryPrimitives.WriteUInt16LittleEndian(palette.AsSpan(i * 2), Half(cx + i, cy));
        return new(u0, v0, w, h, cx, cy, texels, palette);
    }
}

/// <summary>Finds and remembers the user's Crash Bandicoot: Warped disc (read only).</summary>
public static class CrashWarpedDiscLocator
{
    private static readonly string[] Names = ["Crash Bandicoot - Warped (USA).bin", "Crash Bandicoot - Warped (USA) (Track 1).bin"];
    private static readonly Dictionary<(string, long, DateTime), bool> Verified = [];

    public static string ConfigPath(EditorWorkspace workspace) => Path.Combine(workspace.RootPath, "_local", "settings", "crash-warped-disc.json");

    public static bool IsUsable(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        var info = new FileInfo(path);
        var key = (info.FullName, info.Length, info.LastWriteTimeUtc);
        lock (Verified) if (Verified.TryGetValue(key, out bool hit)) return hit;
        bool ok;
        try { new CrashWarpedDisc(path).BoxLevel(); ok = true; }
        catch (Exception e) when (e is InvalidDataException or IOException or FileNotFoundException or UnauthorizedAccessException) { ok = false; }
        lock (Verified) Verified[key] = ok;
        return ok;
    }

    /// <summary>Configured disc if still present, else a conventionally named disc near the workspace; "" if none.</summary>
    public static string Find(EditorWorkspace workspace)
    {
        try
        {
            string cfg = ConfigPath(workspace);
            if (File.Exists(cfg))
            {
                using var doc = JsonDocument.Parse(File.ReadAllBytes(cfg));
                if (doc.RootElement.TryGetProperty("imagePath", out var p) && p.GetString() is { } s && File.Exists(s)) return s;
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
        foreach (string path in WorkspaceArtifactSearchPolicy.EnumerateCandidateRoots(workspace).SelectMany(r => Names.Select(n => Path.Combine(r, n))))
            if (File.Exists(path)) return path;
        return "";
    }

    public static void Configure(EditorWorkspace workspace, string path)
    {
        if (!IsUsable(path)) throw new InvalidDataException("That file is not the Crash Bandicoot: Warped (USA) disc image (SCUS-94244), or its box level file differs from the original.");
        string cfg = ConfigPath(workspace);
        Directory.CreateDirectory(Path.GetDirectoryName(cfg)!);
        File.WriteAllText(cfg, JsonSerializer.Serialize(new { selectedAt = DateTime.Now.ToString("o"), imagePath = Path.GetFullPath(path) },
            new JsonSerializerOptions { WriteIndented = true }));
    }
}
