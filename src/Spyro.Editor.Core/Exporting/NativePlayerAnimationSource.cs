using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativePlayerAnimationSourceCertificate(string Id, long ExecutableOffset, int ByteLength, string Sha256);

public sealed class NativePlayerAnimationSourceLevel
{
    internal NativePlayerAnimationSourceLevel(int levelId, int entry, long wadOffset, int dataLength, string dataHash,
        int modelOffset, int modelLength, int streamOffset, NativePlayerAnimationStream stream)
    {
        LevelId = levelId; DataWadEntry = entry; DataWadOffset = wadOffset; DataByteLength = dataLength;
        DataSha256 = dataHash; ModelDataOffset = modelOffset; ModelByteLength = modelLength;
        StreamDataOffset = streamOffset; Stream = stream;
    }
    public int LevelId { get; }
    public int DataWadEntry { get; }
    public long DataWadOffset { get; }
    public int DataByteLength { get; }
    public string DataSha256 { get; }
    public int ModelDataOffset { get; }
    public int ModelByteLength { get; }
    public int StreamDataOffset { get; }
    public long StreamWadOffset => DataWadOffset + StreamDataOffset;
    public NativePlayerAnimationStream Stream { get; }
}

/// <summary>
/// Exact clean-USA source issuer. Captures all 35 level-local streams and the
/// shared player geometry/slots. Merge rebinds the actual open source before and
/// after reading the destination; a caller hash, catalog or candidate is not authority.
/// </summary>
public sealed class NativePlayerAnimationSource
{
    public const string OriginalImageSha256 = NativeMobySourceInventory.OriginalImageSha256;
    public const long OriginalImageByteLength = NativeMobySourceInventory.OriginalImageByteLength;
    public const long SharedPlayerPackageWadOffset = 0x7B9000;
    public const int SharedPlayerPackageByteLength = 0x32E1C;
    private const int SharedGeometryPackageOffset = 0xAA8;
    private const string SharedPackageHash = "46f570bf0ad2d433f8b42874ae40a3e5d7155d7206dddd1cd08a2d4be16e0e76";
    private static readonly IReadOnlyList<NativePlayerAnimationSourceCertificate> Certificates =
        Array.AsReadOnly<NativePlayerAnimationSourceCertificate>(
        [
            new("native-player-stream-loader", 0x3A30, 0x1B0, "0cca46f0e44c16b8adea088e3081637ec5b04a512dc2c8eac494f9d16732cefb"),
            new("native-player-state-animation-table", 0x5CC70, 0x2D, "9f502dce2b68cb8497072c5c9c92d3f914710cdbbcbc9c25c86cbe29a4241e0b"),
            new("native-player-frame-pointer-consumer", 0x14328, 0x68, "caab994e5b0f5cba19fce5b2e7c969bcfbe79aa60f760c8e76cdb48960ac6b42"),
            new("native-player-frame-sound-selector-consumers", 0x3AE38, 0x10C, "85b931efc214ddfbe44e3a893e036bbceb09e7a15e0e2007d333a5ba323132ca")
        ]);
    private readonly byte[] _geometry;
    private NativePlayerAnimationSource(IEnumerable<NativePlayerAnimationSourceLevel> levels, byte[] geometry, IEnumerable<int> sharedIds)
    {
        Levels = Array.AsReadOnly(levels.OrderBy(l => l.LevelId).ToArray());
        _geometry = geometry.ToArray(); SharedAnimationIds = Array.AsReadOnly(sharedIds.Order().ToArray());
        SharedGeometrySha256 = NativePlayerAnimationStreamCodec.Hash(_geometry);
    }
    public string SourceImageSha256 => OriginalImageSha256;
    public string SharedPlayerPackageSha256 => SharedPackageHash;
    public string SharedGeometrySha256 { get; }
    public IReadOnlyList<int> SharedAnimationIds { get; }
    public IReadOnlyList<NativePlayerAnimationSourceLevel> Levels { get; }
    public IReadOnlyList<NativePlayerAnimationSourceCertificate> NativeCertificates => Certificates;

    /// <summary>Compose into an already edited private destination using the captured original donor.
    /// The universal compositor remains responsible for final RAM fit and disc readback.</summary>
    internal NativePlayerAnimationMergeLayout MergeCaptured(ReadOnlySpan<byte> destination, int donorLevelId,
        IReadOnlyList<int> animationIds)
    {
        var donor = Levels.SingleOrDefault(l => l.LevelId == donorLevelId)
            ?? throw new InvalidDataException("Player-animation donor is not a captured retail level.");
        return new(destination, donor.Stream, _geometry, SharedAnimationIds, animationIds);
    }

    public static async Task<NativePlayerAnimationSource> CaptureAsync(string originalImagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalImagePath);
        string path = Path.GetFullPath(originalImagePath);
        await using FileStream image = Open(path);
        await RequireOriginalAsync(image, cancellationToken);
        DiscLayout layout = RequireLayout(path, image);
        byte[] directory = DiscImage.ReadFileBytes(image, layout, 37, 0, 0x800);
        RequireHash(directory, "15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd", "WAD directory");
        byte[] executable = DiscImage.ReadFileBytes(image, layout, 53875, 0, 0x66000);
        foreach (var certificate in Certificates)
            RequireHash(executable.AsSpan((int)certificate.ExecutableOffset, certificate.ByteLength), certificate.Sha256, certificate.Id);
        byte[] residentHeader = DiscImage.ReadFileBytes(image, layout, 37, 0x7B8800, 0x800);
        if (Word(residentHeader, 0) != 0x39C48 || Word(residentHeader, 4) != 0x800 ||
            Word(residentHeader, 8) != 0 || Word(residentHeader, 12) != 0x3361C)
            throw new InvalidDataException("Shared player package ownership differs from the native resident table.");
        byte[] package = DiscImage.ReadFileBytes(image, layout, 37, SharedPlayerPackageWadOffset, SharedPlayerPackageByteLength);
        RequireHash(package, SharedPackageHash, "shared player model");
        if (Word(package, 0) != 46 || Word(package, 0x34) != SharedGeometryPackageOffset)
            throw new InvalidDataException("Shared player slot count or geometry base differs from the native loader domain.");
        List<int> shared = [];
        for (int id = 0; id < 46; id++)
        {
            uint root = Word(package, 0x38 + id * 4);
            if (root == uint.MaxValue) continue;
            if (root % 4 != 0 || root < 0xF0 || root + 0x24L > package.Length)
                throw new InvalidDataException("Shared player animation root exceeds its native package owner.");
            shared.Add(id);
        }
        byte[] geometry = package[SharedGeometryPackageOffset..];
        List<NativePlayerAnimationSourceLevel> levels = [];
        for (int i = 0; i < 35; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int levelId = (i / 6 + 1) * 10 + i % 6, entry = 10 + i * 2;
            uint offset = Word(directory, entry * 8), length = Word(directory, entry * 8 + 4);
            if (offset < 0x800 || offset % 0x800 != 0 || length < 0x2800 || length > 0x800000 ||
                length % 0x800 != 0 || offset + (long)length > 0x6927000)
                throw new InvalidDataException("A retail player-animation level exceeds its WAD directory owner.");
            byte[] data = DiscImage.ReadFileBytes(image, layout, 37, offset, (int)length);
            int modelOffset = Integer(data, 0x10), modelLength = Integer(data, 0x14);
            _ = new NativeLevelDataGrowthLayout(data.AsSpan(0, 0x800), data.Length,
                [new(0, 37, checked(offset + (long)modelOffset + modelLength), entry, modelLength, 0, [], 2, false)]);
            int streamOffset = Integer(data, 0x50), end = Integer(data, 0x54);
            if (BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0x150)) != 0 || end <= streamOffset)
                throw new InvalidDataException("The retail player stream has no special class-0 / next-root ownership.");
            var stream = NativePlayerAnimationStreamCodec.Decode(data.AsSpan(streamOffset, end - streamOffset), geometry);
            if (!stream.Encode().AsSpan().SequenceEqual(data.AsSpan(streamOffset, end - streamOffset)) ||
                stream.Records.Any(r => shared.Contains(r.AnimationId)))
                throw new InvalidDataException("The retail player stream does not round-trip or conflicts with a shared resident slot.");
            levels.Add(new(levelId, entry, offset, (int)length, NativePlayerAnimationStreamCodec.Hash(data),
                modelOffset, modelLength, streamOffset, stream));
        }
        await RequireOriginalAsync(image, cancellationToken);
        return new(levels, geometry, shared);
    }

    public async Task<NativePlayerAnimationMergePlan> CompileMergeAsync(string originalImagePath, int donorLevelId,
        int targetLevelId, IReadOnlyList<int> animationIds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalImagePath);
        ArgumentNullException.ThrowIfNull(animationIds);
        // Snapshot requests before asynchronous work; no caller mutation can swap
        // an ID after source verification or change the plan fingerprint.
        if (animationIds.Count == 0 || animationIds.Count > 46)
            throw new InvalidDataException("A player-animation merge requires 1..46 distinct IDs.");
        int[] requested = animationIds.ToArray();
        if (requested.Any(i => i < 0 || i >= 46) || requested.Distinct().Count() != requested.Length)
            throw new InvalidDataException("A player-animation merge requires distinct native IDs 0..45.");
        var donor = Levels.SingleOrDefault(l => l.LevelId == donorLevelId)
            ?? throw new InvalidDataException("Player-animation donor is not a captured retail level.");
        var target = Levels.SingleOrDefault(l => l.LevelId == targetLevelId)
            ?? throw new InvalidDataException("Player-animation destination is not a captured retail level; ID65 is not independently owned.");
        string path = Path.GetFullPath(originalImagePath);
        await using FileStream image = Open(path);
        await RequireOriginalAsync(image, cancellationToken);
        DiscLayout layout = RequireLayout(path, image);
        byte[] data = DiscImage.ReadFileBytes(image, layout, 37, target.DataWadOffset, target.DataByteLength);
        RequireHash(data, target.DataSha256, "captured destination level");
        var merged = new NativePlayerAnimationMergeLayout(data, donor.Stream, _geometry, SharedAnimationIds,
            requested, target.DataWadEntry, target.DataWadOffset);
        await RequireOriginalAsync(image, cancellationToken);
        return new(this, donorLevelId, targetLevelId, merged);
    }

    private static FileStream Open(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    private static DiscLayout RequireLayout(string path, FileStream image)
    {
        DiscLayout layout = DiscImage.DetectLayout(path);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24)
            throw new InvalidDataException("Player-animation source must use native MODE2/2352 geometry.");
        var wad = DiscImage.FindRootFileRecord(image, layout, n => n.Equals("WAD.WAD", StringComparison.OrdinalIgnoreCase));
        var exe = DiscImage.FindRootFileRecord(image, layout, n => n.Equals("SCUS_942.28", StringComparison.OrdinalIgnoreCase));
        if (wad.Lba != 37 || wad.Size != 0x6927000 || exe.Lba != 53875 || exe.Size != 0x66000)
            throw new InvalidDataException("Player-animation source ISO owners differ from the exact retail certificate.");
        return layout;
    }
    private static async Task RequireOriginalAsync(FileStream image, CancellationToken token)
    {
        if (image.Length != OriginalImageByteLength)
            throw new InvalidDataException("Player-animation source has the wrong image length.");
        image.Position = 0;
        string hash = Convert.ToHexString(await SHA256.HashDataAsync(image, token)).ToLowerInvariant();
        if (hash != OriginalImageSha256 || image.Length != OriginalImageByteLength || image.Position != OriginalImageByteLength)
            throw new InvalidDataException("Player-animation source is not the unchanged original USA image.");
    }
    private static void RequireHash(ReadOnlySpan<byte> bytes, string expected, string label)
    {
        if (NativePlayerAnimationStreamCodec.Hash(bytes) != expected)
            throw new InvalidDataException($"Player-animation {label} no longer matches its source certificate.");
    }
    private static uint Word(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadUInt32LittleEndian(b[o..]);
    private static int Integer(ReadOnlySpan<byte> b, int o)
    {
        uint value = Word(b, o);
        if (value > int.MaxValue) throw new InvalidDataException("Player-animation owner exceeds bounded signed geometry.");
        return (int)value;
    }
}
