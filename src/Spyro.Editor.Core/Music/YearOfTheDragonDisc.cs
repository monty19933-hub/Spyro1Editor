using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.Core.Music;

/// <summary>A level song of Spyro: Year of the Dragon, by its level's index in the game's WAD order (Lost Fleet 20).</summary>
public sealed record YearOfTheDragonSong(int TrackId, string Name, int LevelIndex);

/// <summary>Read-only access to the user's Spyro: Year of the Dragon (USA, Rev 1) disc (SCUS-94467) for its music.
/// The game streams its music as XA in SPEECH.STR, the same format as Spyro the Dragon's (stereo, 37.8 kHz, 4-bit):
/// eight interleaved channels, each holding several songs back to back. The executable lists the 37 songs (start
/// sector in the file and a loop position) at 0x55B48 and, per level, its song at 0x5600C. A song is its start
/// sector's channel, every eighth sector from there, until the channel breaks off. Nothing is written to the disc.</summary>
public sealed class YearOfTheDragonDisc
{
    public const string BootName = "SCUS_944.67";
    public const int SongTableOffset = 0x55B48, LevelSongOffset = 0x5600C, Songs = 37, Levels = 37;
    /// <summary>SHA-256 of the two tables (37 eight-byte songs, then 37 level bytes) in the Rev 1 executable.</summary>
    public const string TablesSha256 = "2e237c6ab654c11364ccdf370ba009951bc996ff2407b35a4e0f02d27aa9818f";
    public static readonly YearOfTheDragonSong LostFleet = new(101, "Lost Fleet", 20);
    public static IReadOnlyList<YearOfTheDragonSong> LevelSongs { get; } = [LostFleet];

    private const int Channels = 8, StereoCoding = 0x01, AudioBit = 0x04;
    private readonly DiscLayout _layout;
    private readonly (int Lba, int Size) _speech;
    private readonly byte[] _songs, _levels;
    private readonly Dictionary<string, (int Lba, int Size)> _files;

    public YearOfTheDragonDisc(string imagePath)
    {
        ImagePath = imagePath;
        using var fs = File.OpenRead(imagePath);
        _layout = DiscImage.DetectLayout(fs);
        if (_layout.SectorSize != 2352) throw new InvalidDataException("The Spyro: Year of the Dragon disc must be a raw MODE2/2352 image (.bin/.cue).");
        var files = _files = new Dictionary<string, (int Lba, int Size)>(StringComparer.OrdinalIgnoreCase);
        byte[] root = DiscImage.ReadFileBytes(fs, _layout, _layout.RootExtent, 0, _layout.RootLength);
        for (int o = 0; o < root.Length;)
        {
            int length = root[o];
            if (length == 0) { o = (o / 2048 + 1) * 2048; continue; }
            if (length < 34 || o + length > root.Length) break;
            int nameLength = root[o + 32];
            string name = System.Text.Encoding.Latin1.GetString(root, o + 33, nameLength).Split(';')[0];
            files[name] = (BinaryPrimitives.ReadInt32LittleEndian(root.AsSpan(o + 2)), BinaryPrimitives.ReadInt32LittleEndian(root.AsSpan(o + 10)));
            o += length;
        }
        if (!files.TryGetValue("SYSTEM.CNF", out var cnf) || !files.TryGetValue(BootName, out var exe) || !files.TryGetValue("SPEECH.STR", out _speech) ||
            !System.Text.Encoding.ASCII.GetString(DiscImage.ReadFileBytes(fs, _layout, cnf.Lba, 0, cnf.Size)).Contains(BootName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("This is not the Spyro: Year of the Dragon (USA) disc (SCUS-94467).");
        byte[] tables = DiscImage.ReadFileBytes(fs, _layout, exe.Lba, SongTableOffset, LevelSongOffset + Levels - SongTableOffset);
        _songs = tables.AsSpan(0, Songs * 8).ToArray();
        _levels = tables.AsSpan(LevelSongOffset - SongTableOffset, Levels).ToArray();
        if (Convert.ToHexStringLower(SHA256.HashData([.. _songs, .. _levels])) != TablesSha256)
            throw new InvalidDataException("This Spyro: Year of the Dragon disc's music tables differ from the USA Rev 1 release.");
    }

    public string ImagePath { get; }

    /// <summary>The game's executable (SCUS_944.67), as stored.</summary>
    public byte[] Executable() => ReadFile(BootName);

    /// <summary>Entry <paramref name="index"/> of WAD.WAD (the levels and their sub-files), as stored.</summary>
    public byte[] WadEntry(int index)
    {
        byte[] header = ReadFile("WAD.WAD", 0, 0x800);
        if (index < 0 || index >= 0x100) throw new ArgumentOutOfRangeException(nameof(index));
        int offset = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(8 * index)), size = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(8 * index + 4));
        if (offset <= 0 || size <= 0 || (long)offset + size > _files["WAD.WAD"].Size) throw new InvalidDataException($"WAD entry {index} is not on this disc.");
        return ReadFile("WAD.WAD", offset, size);
    }

    private byte[] ReadFile(string name, int offset = 0, int length = -1)
    {
        if (!_files.TryGetValue(name, out var file)) throw new InvalidDataException($"{name} is missing from the Spyro: Year of the Dragon disc.");
        using var fs = File.OpenRead(ImagePath);
        return DiscImage.ReadFileBytes(fs, _layout, file.Lba, offset, length < 0 ? file.Size - offset : length);
    }

    /// <summary>The song a level plays, as XA sector payloads (2,324 bytes each, Form 2 data without EDC).</summary>
    public byte[][] LevelSong(YearOfTheDragonSong song)
    {
        int number = _levels[song.LevelIndex];
        int start = BinaryPrimitives.ReadInt32LittleEndian(_songs.AsSpan(8 * number));
        using var fs = File.OpenRead(ImagePath);
        byte[] raw = new byte[2352];
        fs.Seek((long)(_speech.Lba + start) * 2352, SeekOrigin.Begin);
        fs.ReadExactly(raw);
        int channel = raw[17];
        List<byte[]> sectors = [];
        for (int k = start; k < _speech.Size / 2048; k += Channels)
        {
            fs.Seek((long)(_speech.Lba + k) * 2352, SeekOrigin.Begin);
            fs.ReadExactly(raw);
            if (raw[17] != channel || (raw[18] & AudioBit) == 0 || raw[19] != StereoCoding) break;
            sectors.Add(raw.AsSpan(24, XaAudio.SectorDataBytes).ToArray());
            if ((raw[18] & 0x80) != 0) break;
        }
        if (sectors.Count < 75) throw new InvalidDataException($"{song.Name}'s music is not where the game keeps it.");
        return [.. sectors];
    }
}

public static class YearOfTheDragonDiscLocator
{
    private static readonly string[] Names = ["Spyro - Year of the Dragon (USA) (Rev 1).bin", "Spyro - Year of the Dragon (USA) (Rev 1) (Track 1).bin"];
    private static readonly Dictionary<(string, long, DateTime), bool> Verified = [];

    public static string ConfigPath(EditorWorkspace workspace) => Path.Combine(workspace.RootPath, "_local", "settings", "year-of-the-dragon-disc.json");

    public static bool IsUsable(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        var info = new FileInfo(path);
        var key = (info.FullName, info.Length, info.LastWriteTimeUtc);
        lock (Verified) if (Verified.TryGetValue(key, out bool hit)) return hit;
        bool ok;
        try { _ = new YearOfTheDragonDisc(path); ok = true; }
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
        if (!IsUsable(path)) throw new InvalidDataException("That file is not the Spyro: Year of the Dragon (USA, Rev 1) disc image (SCUS-94467).");
        string cfg = ConfigPath(workspace);
        Directory.CreateDirectory(Path.GetDirectoryName(cfg)!);
        File.WriteAllText(cfg, JsonSerializer.Serialize(new { selectedAt = DateTime.Now.ToString("o"), imagePath = Path.GetFullPath(path) },
            new JsonSerializerOptions { WriteIndented = true }));
    }
}
