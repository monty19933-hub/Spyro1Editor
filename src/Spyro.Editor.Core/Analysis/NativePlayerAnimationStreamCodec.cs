using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Analysis;

public enum NativePlayerAnimationReferenceDomain { LocalFrameData, SharedPlayerGeometry }
public enum NativePlayerAnimationReferenceEncoding { ByteOffset, PackedFrameByteOffset }

/// <summary>A checked reference start, not proof of the variable-length frame consumer's complete read extent.</summary>
public sealed record NativePlayerAnimationReference(int BlobFieldOffset, NativePlayerAnimationReferenceDomain Domain,
    NativePlayerAnimationReferenceEncoding Encoding, uint EncodedOffset, int TargetOffset, int MinimumTargetBytes);

/// <summary>Source representation; UpperByte is not interpreted as a proven sound dependency here.</summary>
public sealed record NativePlayerAnimationFrame(int Index, uint PackedSourceWord)
{
    public uint LocalByteOffset => PackedSourceWord & 0x1FFFFF;
    public uint LoaderPreservedBits => PackedSourceWord & 0xFFE00000;
    public byte UpperByte => (byte)(PackedSourceWord >> 24);
}

public sealed class NativePlayerAnimationRecord
{
    private readonly byte[] _bytes;
    internal NativePlayerAnimationRecord(int streamOffset, ReadOnlySpan<byte> bytes,
        IEnumerable<NativePlayerAnimationReference> references)
    {
        _bytes = bytes.ToArray(); StreamOffset = streamOffset;
        AnimationId = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        FrameCount = BinaryPrimitives.ReadInt16LittleEndian(bytes[8..]);
        var frames = new NativePlayerAnimationFrame[FrameCount];
        for (int i = 0; i < frames.Length; i++)
            frames[i] = new(i, BinaryPrimitives.ReadUInt32LittleEndian(bytes[(8 + 0x24 + 4 * i)..]));
        Frames = Array.AsReadOnly(frames);
        References = Array.AsReadOnly(references.ToArray());
        Sha256 = NativePlayerAnimationStreamCodec.Hash(bytes);
    }
    public int AnimationId { get; }
    public int StreamOffset { get; }
    public int ByteLength => _bytes.Length;
    public int BlobByteLength => _bytes.Length - 8;
    public int FrameCount { get; }
    public IReadOnlyList<NativePlayerAnimationFrame> Frames { get; }
    public string Sha256 { get; }
    public IReadOnlyList<NativePlayerAnimationReference> References { get; }
    public byte[] CopyRecord() => _bytes.ToArray();
}

/// <summary>Structural decoding only; no source-ROM, runtime-fit or publication authority.</summary>
public sealed class NativePlayerAnimationStream
{
    private readonly byte[] _bytes;
    internal NativePlayerAnimationStream(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> geometry,
        IEnumerable<NativePlayerAnimationRecord> records)
    {
        _bytes = bytes.ToArray(); Records = Array.AsReadOnly(records.ToArray());
        Sha256 = NativePlayerAnimationStreamCodec.Hash(bytes);
        SharedGeometrySha256 = NativePlayerAnimationStreamCodec.Hash(geometry);
        SharedGeometryByteLength = geometry.Length;
    }
    public IReadOnlyList<NativePlayerAnimationRecord> Records { get; }
    public int ByteLength => _bytes.Length;
    public int TerminatorOffset => _bytes.Length - 4;
    public string Sha256 { get; }
    public string SharedGeometrySha256 { get; }
    public int SharedGeometryByteLength { get; }
    public bool FramePayloadDecodingProven => false;
    public bool RuntimeFitProven => false;
    public byte[] Encode() => _bytes.ToArray();
}

public static class NativePlayerAnimationStreamCodec
{
    public const int AnimationSlotCount = 46;
    public const int MaximumStreamBytes = 0x200000;
    public const int MaximumSharedGeometryBytes = 0x200000;

    public static NativePlayerAnimationStream Decode(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> sharedGeometry)
    {
        if (bytes.Length < 4 || bytes.Length > MaximumStreamBytes || bytes.Length % 4 != 0 ||
            sharedGeometry.Length < 4 || sharedGeometry.Length > MaximumSharedGeometryBytes || sharedGeometry.Length % 4 != 0)
            throw new InvalidDataException("A player-animation stream requires bounded aligned stream and shared-geometry owners.");
        List<NativePlayerAnimationRecord> records = [];
        HashSet<int> ids = [];
        int cursor = 0;
        while (true)
        {
            if (bytes.Length - cursor < 4)
                throw new InvalidDataException("The player-animation stream has no complete terminator.");
            int id = BinaryPrimitives.ReadInt32LittleEndian(bytes[cursor..]);
            if (id == -1)
            {
                if (cursor != bytes.Length - 4)
                    throw new InvalidDataException("Bytes follow the player-animation stream terminator inside its declared owner.");
                return new(bytes, sharedGeometry, records);
            }
            if (id < 0 || id >= AnimationSlotCount || !ids.Add(id))
                throw new InvalidDataException("Player-animation IDs must be distinct native slots 0..45.");
            if (bytes.Length - cursor < 8)
                throw new InvalidDataException("The player-animation record length is truncated.");
            uint rawLength = Word(bytes, cursor + 4);
            if (rawLength < 0x28 || rawLength > bytes.Length - cursor - 8L || rawLength % 4 != 0)
                throw new InvalidDataException("A player-animation blob exceeds its aligned record owner.");
            int length = (int)rawLength;
            ReadOnlySpan<byte> blob = bytes.Slice(cursor + 8, length);
            int frames = BinaryPrimitives.ReadInt16LittleEndian(blob);
            if (frames <= 0 || blob[4] != 1)
                throw new InvalidDataException("Only the source-proved type-1 player-animation encoding with positive signed frame count is supported.");
            int localBase = checked(0x24 + frames * 4);
            if (localBase > length - 4)
                throw new InvalidDataException("The player-animation frame table leaves no bounded local data owner.");
            List<NativePlayerAnimationReference> references = [];
            Add(0x10, NativePlayerAnimationReferenceDomain.LocalFrameData,
                NativePlayerAnimationReferenceEncoding.ByteOffset, Word(blob, 0x10), localBase, length, 4, 4);
            Add(0x14, NativePlayerAnimationReferenceDomain.SharedPlayerGeometry,
                NativePlayerAnimationReferenceEncoding.ByteOffset, Word(blob, 0x14), 0, sharedGeometry.Length, 4, 4);
            Add(0x18, NativePlayerAnimationReferenceDomain.SharedPlayerGeometry,
                NativePlayerAnimationReferenceEncoding.ByteOffset, Word(blob, 0x18), 0, sharedGeometry.Length, 4, 4);
            for (int frame = 0; frame < frames; frame++)
            {
                int field = 0x24 + frame * 4;
                // 80013378 masks source byte offsets; 80013380 shifts the rebased
                // address right once. Upper frame flags are not pointer bits.
                Add(field, NativePlayerAnimationReferenceDomain.LocalFrameData,
                    NativePlayerAnimationReferenceEncoding.PackedFrameByteOffset,
                    Word(blob, field) & 0x1FFFFF, localBase, length, 2, 2);
            }
            records.Add(new(cursor, bytes.Slice(cursor, length + 8), references));
            cursor = checked(cursor + length + 8);

            void Add(int field, NativePlayerAnimationReferenceDomain domain,
                NativePlayerAnimationReferenceEncoding encoding, uint offset, int basis, int ownerLength,
                int minimumBytes, int alignment)
            {
                long target = basis + (long)offset;
                if (offset % alignment != 0 || target > ownerLength - (long)minimumBytes)
                    throw new InvalidDataException($"Player animation {id} field +0x{field:X} leaves its {domain} owner or is misaligned.");
                references.Add(new(field, domain, encoding, offset, (int)target, minimumBytes));
            }
        }
    }

    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static uint Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
}
