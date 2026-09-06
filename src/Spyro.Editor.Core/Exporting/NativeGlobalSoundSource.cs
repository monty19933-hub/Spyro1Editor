using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeGlobalSoundSourceCertificate(string Id, int ExecutableOffset, int ByteLength, string Sha256);

public sealed class NativeGlobalSoundSourceLevel
{
    internal NativeGlobalSoundSourceLevel(int level, int entry, long offset, byte[] data, NativeLevelSoundLayout layout)
    {
        LevelId = level; DataWadEntry = entry; DataWadOffset = offset; DataByteLength = data.Length;
        DataSha256 = NativeSoundBankCodec.Hash(data); SoundDataOffset = layout.SoundOffset;
        BankDataOffset = layout.BankOffset; Sound = layout.Sound;
    }
    public int LevelId { get; }
    public int DataWadEntry { get; }
    public long DataWadOffset { get; }
    public int DataByteLength { get; }
    public string DataSha256 { get; }
    public int SoundDataOffset { get; }
    public int BankDataOffset { get; }
    public long SoundWadOffset => DataWadOffset + SoundDataOffset;
    public long BankWadOffset => DataWadOffset + BankDataOffset;
    public NativeSoundBank Sound { get; }
}

/// <summary>Issues detached sound plans only from the unchanged clean-USA image, never caller-declared hashes.</summary>
public sealed partial class NativeGlobalSoundSource
{
    public const string OriginalImageSha256 = NativeMobySourceInventory.OriginalImageSha256;
    public const long OriginalImageByteLength = NativeMobySourceInventory.OriginalImageByteLength;
    private static readonly IReadOnlyList<NativeGlobalSoundSourceCertificate> Certificates = Array.AsReadOnly<NativeGlobalSoundSourceCertificate>(
    [
        new("map-count-stride-and-SPU-rebase", 0x34F0, 0x68, "ecd95b895b0f1dd85b2aed1c926a6e5e818ac3c41ddaf38565e34459808f89a0"),
        new("level-SPU-bank-CD-read-and-fixed-upload", 0x5EB0, 0x230, "f3cdb7634a2e36f2329959401df8bc6006cd623bd2ea7233e4ca43ea53e42c15"),
        new("electric-player-global-selector-1F", 0x31844, 0x28, "d0e9d200cbf22489706549be06f9ae873e877c9be6eb1a3fb93b25503ab58721")
    ]);
    private NativeGlobalSoundSource(IEnumerable<NativeGlobalSoundSourceLevel> levels) => Levels = Array.AsReadOnly(levels.OrderBy(l => l.LevelId).ToArray());
    public string SourceImageSha256 => OriginalImageSha256;
    public IReadOnlyList<NativeGlobalSoundSourceLevel> Levels { get; }
    public IReadOnlyList<NativeGlobalSoundSourceCertificate> NativeCertificates => Certificates;

    public static async Task<NativeGlobalSoundSource> CaptureAsync(string originalImagePath, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalImagePath);
        string path = Path.GetFullPath(originalImagePath);
        await using FileStream image = Open(path);
        await RequireOriginalAsync(image, token);
        DiscLayout layout = RequireLayout(path, image);
        byte[] directory = DiscImage.ReadFileBytes(image, layout, 37, 0, 0x800);
        RequireHash(directory, "15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd", "WAD directory");
        byte[] executable = DiscImage.ReadFileBytes(image, layout, 53875, 0, 0x66000);
        foreach (var certificate in Certificates)
            RequireHash(executable.AsSpan(certificate.ExecutableOffset, certificate.ByteLength), certificate.Sha256, certificate.Id);
        List<NativeGlobalSoundSourceLevel> levels = [];
        for (int i = 0; i < 35; i++)
        {
            token.ThrowIfCancellationRequested();
            int id = (i / 6 + 1) * 10 + i % 6, entry = 10 + i * 2;
            uint offset = NativeLevelSoundLayout.Word(directory, entry * 8), length = NativeLevelSoundLayout.Word(directory, entry * 8 + 4);
            if (offset < 0x800 || offset % 0x800 != 0 || length < 0x2800 || length > 0x800000 ||
                length % 0x800 != 0 || offset + (long)length > 0x6927000)
                throw new InvalidDataException("Native sound level exceeds its exact WAD directory owner.");
            byte[] data = DiscImage.ReadFileBytes(image, layout, 37, offset, (int)length);
            levels.Add(new(id, entry, offset, data, new(data)));
        }
        await RequireOriginalAsync(image, token);
        return new(levels);
    }

    public async Task<NativeGlobalSoundMergePlan> CompileMergeAsync(string originalImagePath, int donorLevelId,
        int targetLevelId, IReadOnlyList<NativeGlobalSoundRequest> requests, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalImagePath);
        NativeGlobalSoundRequest[] snapshot = NativeGlobalSoundMergeLayout.Snapshot(requests);
        var donor = Levels.SingleOrDefault(l => l.LevelId == donorLevelId)
            ?? throw new InvalidDataException("Sound donor is not a captured native level.");
        var target = Levels.SingleOrDefault(l => l.LevelId == targetLevelId)
            ?? throw new InvalidDataException("Sound destination is not a captured native level; no independent ID65 ownership is implied.");
        string path = Path.GetFullPath(originalImagePath);
        await using FileStream image = Open(path);
        await RequireOriginalAsync(image, token);
        DiscLayout layout = RequireLayout(path, image);
        byte[] data = DiscImage.ReadFileBytes(image, layout, 37, target.DataWadOffset, target.DataByteLength);
        RequireHash(data, target.DataSha256, "captured target data");
        var merge = new NativeGlobalSoundMergeLayout(data, donor.Sound, snapshot, target.DataWadEntry, target.DataWadOffset);
        await RequireOriginalAsync(image, token);
        return new(this, donorLevelId, targetLevelId, merge);
    }
    private static FileStream Open(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    private static async Task RequireOriginalAsync(FileStream image, CancellationToken token)
    {
        if (image.Length != OriginalImageByteLength) throw new InvalidDataException("Native sound source has the wrong image length.");
        image.Position = 0;
        string hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(image, token));
        if (hash != OriginalImageSha256 || image.Length != OriginalImageByteLength || image.Position != OriginalImageByteLength)
            throw new InvalidDataException("Native sound source is not the unchanged original USA image.");
    }
    private static DiscLayout RequireLayout(string path, FileStream image)
    {
        DiscLayout layout = DiscImage.DetectLayout(path);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24) throw new InvalidDataException("Native sound source requires MODE2/2352 geometry.");
        var wad = DiscImage.FindRootFileRecord(image, layout, n => n.Equals("WAD.WAD", StringComparison.OrdinalIgnoreCase));
        var exe = DiscImage.FindRootFileRecord(image, layout, n => n.Equals("SCUS_942.28", StringComparison.OrdinalIgnoreCase));
        if (wad.Lba != 37 || wad.Size != 0x6927000 || exe.Lba != 53875 || exe.Size != 0x66000)
            throw new InvalidDataException("Native sound source ISO ownership changed.");
        return layout;
    }
    private static void RequireHash(ReadOnlySpan<byte> bytes, string hash, string label)
    {
        if (NativeSoundBankCodec.Hash(bytes) != hash) throw new InvalidDataException($"Native sound {label} differs from its source certificate.");
    }
}

/// <summary>Six data components, portal sky owners, particles, then sound. No heuristic byte search.</summary>
internal sealed class NativeLevelSoundLayout
{
    internal NativeLevelSoundLayout(ReadOnlySpan<byte> data)
    {
        if (data.Length < 0x2800 || data.Length > 0x800000) throw new InvalidDataException("Native sound level length is outside its bounded domain.");
        int model = Integer(data, 0x10), modelLength = Integer(data, 0x14);
        _ = new NativeLevelDataGrowthLayout(data[..0x800], data.Length, [new(0, 37, model + (long)modelLength, 0, modelLength, 0, [], 2, false)]);
        VramOffset = Integer(data, 0); VramLength = Integer(data, 4);
        if (VramLength <= 0x80000) throw new InvalidDataException("Native level has no SPU bank after its exact 0x80000-byte VRAM owner.");
        BankOffset = VramOffset + 0x80000;
        DataOffset = Integer(data, 8); DataLength = Integer(data, 12);
        int cursor = DataOffset, end = checked(DataOffset + DataLength);
        for (int i = 0; i < 6; i++) cursor += Component(data, cursor, end);
        Range(cursor, 4, end); uint portals = Word(data, cursor); cursor += 4;
        if (portals > 256) throw new InvalidDataException("Native sound portal traversal exceeds its work budget.");
        for (int i = 0; i < portals; i++)
        {
            Range(cursor, 8, end); uint points = Word(data, cursor + 4);
            if (points is 0 or > 4096) throw new InvalidDataException("Native portal point count is outside its bounded owner.");
            int prefix = checked(0x34 + (int)points * 12);
            Range(cursor, prefix, end); cursor += prefix;
            cursor += Component(data, cursor, end);
        }
        ParticleOffset = cursor;
        cursor += Component(data, cursor, end); // particle component
        SoundOffset = cursor; int soundLength = Component(data, cursor, end);
        Sound = NativeSoundBankCodec.Decode(data.Slice(cursor, soundLength), data.Slice(BankOffset, VramLength - 0x80000));
    }
    internal int VramOffset { get; }
    internal int VramLength { get; }
    internal int BankOffset { get; }
    internal int DataOffset { get; }
    internal int DataLength { get; }
    internal int SoundOffset { get; }
    internal int ParticleOffset { get; }
    internal int SoundEnd => SoundOffset + Sound.ComponentByteLength;
    internal NativeSoundBank Sound { get; }
    private static int Component(ReadOnlySpan<byte> data, int offset, int end)
    {
        Range(offset, 4, end); int length = Integer(data, offset);
        if (length < 4 || length % 4 != 0) throw new InvalidDataException("Native sound traversal found a malformed component length.");
        Range(offset, length, end); return length;
    }
    private static void Range(int offset, int length, int end)
    {
        if (offset < 0 || length < 0 || offset % 4 != 0 || offset + (long)length > end)
            throw new InvalidDataException("Native sound component reference exceeds its owner.");
    }
    internal static uint Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    internal static int Integer(ReadOnlySpan<byte> bytes, int offset)
    {
        uint value = Word(bytes, offset);
        return value <= int.MaxValue ? (int)value : throw new InvalidDataException("Native sound offset exceeds bounded signed geometry.");
    }
}
