using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeStaticTerrainTextureEvidence(string Owner, long SourceWadOffset,
    int ByteLength, string Sha256);

/// <summary>
/// Private exact-original issuer for a static native terrain record and its
/// original GPU-domain bytes. Selection integers and catalog metadata alone
/// carry no authority. No target atlas, record ID, behavior or output is issued.
/// </summary>
public sealed class NativeStaticTerrainTextureSource
{
    private readonly byte[] _vram;
    private NativeStaticTerrainTextureSource(int entry, int id, long sourceOffset, byte[] data,
        NativeTerrainTextureRuntimeControlAudit controls)
    {
        int page = Part(data, 0, 0x80000), metadata = Part(data, 8, 8), scene = Part(data, 24, 0x98);
        int pageLength = checked((int)Word(data, 4)), modelLength = checked((int)Word(data, 12));
        int sceneLength = checked((int)Word(data, 28));
        int count = checked((int)Word(data, metadata + 4));
        if (count is < 1 or > 128 || id >= count || Word(data, metadata) != 8 + count * RecordByteLength ||
            Word(data, metadata) > modelLength || page + (long)pageLength > metadata ||
            metadata + (long)modelLength > Word(data, 16))
            throw new InvalidDataException("Actual native level/texture component owners overlap, escape, or change their native count/stride.");
        SourceDataSha256 = Hash(data); SourceSceneSha256 = Hash(data.AsSpan(scene, sceneLength));
        if (!controls.Complete || controls.TargetWadEntry != entry || controls.TextureCount != count ||
            !string.Equals(controls.SceneSha256, SourceSceneSha256, StringComparison.OrdinalIgnoreCase) ||
            !controls.IsRuntimePersistentTarget(id) || controls.AnimationSourceTextureIds.Contains(id))
            throw new InvalidDataException("Selected native record has unclosed animation/scroll/source-frame semantics or stale scene evidence.");
        int lo = metadata + 8 + id * 16, hi = metadata + 8 + count * 16 + id * 168;
        byte[] record = new byte[RecordByteLength];
        data.AsSpan(lo, 16).CopyTo(record); data.AsSpan(hi, 168).CopyTo(record.AsSpan(16));
        Layout = NativeStaticTerrainTextureLayout.Decode(record);
        _vram = data.AsSpan(page, 0x80000).ToArray();
        // Decode/extract every actual rectangle now; a later caller receives
        // immutable source resources, not a deferred out-of-range promise.
        foreach (var descriptor in Layout.Descriptors)
        { _ = descriptor.CopyPixels(_vram); _ = descriptor.CopyPalette(_vram); }
        SourceWadEntry = entry; SourceTextureId = id;
        NativeAnimationControlCount = controls.AnimationControls.Count;
        NativeScrollingControlCount = controls.ScrollingControls.Count;
        Evidence = Array.AsReadOnly(new[]
        {
            E("whole-donor-level", sourceOffset, data),
            E("actual-donor-scene-controls-and-consumers", sourceOffset + scene, data.AsSpan(scene, sceneLength)),
            E("whole-native-terrain-metadata", sourceOffset + metadata, data.AsSpan(metadata, 8 + count * RecordByteLength)),
            E("native-lq-record", sourceOffset + lo, data.AsSpan(lo, 16)),
            E("native-hq-record", sourceOffset + hi, data.AsSpan(hi, 168)),
            E("addressable-source-GPU-upload", sourceOffset + page, _vram)
        });
        Handle = $"{ProfileId}:{SourceImageSha256}:wad-{entry}:data-{SourceDataSha256}:t-{id}:record-{Layout.RecordSha256}";
    }
    private const int RecordByteLength = NativeStaticTerrainTextureLayout.RecordByteLength;
    public const string ProfileId = "native-static-terrain-record-canonical-orientation0-clean-usa-v1";
    public string SourceImageSha256 => NativeMobySourceInventory.OriginalImageSha256;
    public int SourceWadEntry { get; }
    public int SourceTextureId { get; }
    public string SourceDataSha256 { get; }
    public string SourceSceneSha256 { get; }
    public string Handle { get; }
    public NativeStaticTerrainTextureLayout Layout { get; }
    public IReadOnlyList<NativeStaticTerrainTextureEvidence> Evidence { get; }
    public int NativeAnimationControlCount { get; }
    public int NativeScrollingControlCount { get; }
    public bool SourceCertified => true;
    public bool StaticRecordProven => true;
    public bool DestinationAllocationProven => false;
    public bool RuntimeVerified => false;
    public bool PublicationApproved => false;
    public byte[] CopySourceVram() => _vram.ToArray();
    public void RequireExactSourceRecord(ReadOnlySpan<byte> record)
    {
        if (!record.SequenceEqual(Layout.CopyRecord()))
            throw new InvalidDataException("A changed or caller-authored record cannot replace original source-issued texture bytes.");
    }

    public static NativeStaticTerrainTextureSource Capture(string originalImagePath, int sourceWadEntry, int nativeTextureId)
    {
        if (nativeTextureId is < 0 or >= 128) throw new ArgumentOutOfRangeException(nameof(nativeTextureId));
        // Actual35 retail data entries; their intervening overlays and ID65 do
        // not acquire donor authority by containing a superficially valid row.
        if (sourceWadEntry < 10 || sourceWadEntry > 78 || sourceWadEntry % 2 != 0)
            throw new ArgumentOutOfRangeException(nameof(sourceWadEntry));
        ArgumentException.ThrowIfNullOrWhiteSpace(originalImagePath);
        using FileStream image = new(originalImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        RequireOriginal(image);
        var layout = DiscImage.DetectLayout(originalImagePath);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24)
            throw new InvalidDataException("Native terrain source requires original MODE2/2352 sector ownership.");
        byte[] header = DiscImage.ReadFileBytes(image, layout, 37, 0, 0x800);
        long offset = Word(header, sourceWadEntry * 8); uint length = Word(header, sourceWadEntry * 8 + 4);
        if (offset < 0x800 || length is < 32 or > 0x800000)
            throw new InvalidDataException("Native donor entry budget must be validated before copying payloads.");
        byte[] data = DiscImage.ReadFileBytes(image, layout, 37, offset, checked((int)length));
        var controls = NativeTerrainTextureRuntimeControlScanner.Inspect(originalImagePath, sourceWadEntry);
        var result = new NativeStaticTerrainTextureSource(sourceWadEntry, nativeTextureId, offset, data, controls);
        RequireOriginal(image);
        return result;
    }
    private static int Part(ReadOnlySpan<byte> data, int field, int minimumLength)
    {
        uint offset = Word(data, field), length = Word(data, field + 4);
        if (offset < 0x800 || length < minimumLength || offset + (long)length > data.Length)
            throw new InvalidDataException("Native donor subfile range is not owned by the exact level payload.");
        return checked((int)offset);
    }
    private static void RequireOriginal(FileStream image)
    {
        if (image.Length != NativeMobySourceInventory.OriginalImageByteLength)
            throw new InvalidDataException("Native texture source length changed.");
        image.Position = 0;
        if (Convert.ToHexStringLower(SHA256.HashData(image)) != NativeMobySourceInventory.OriginalImageSha256 ||
            image.Length != NativeMobySourceInventory.OriginalImageByteLength || image.Position != image.Length)
            throw new InvalidDataException("Native textures require the complete unchanged original source image.");
    }
    private static NativeStaticTerrainTextureEvidence E(string owner, long at, ReadOnlySpan<byte> bytes) => new(owner, at, bytes.Length, Hash(bytes));
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static uint Word(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
}
