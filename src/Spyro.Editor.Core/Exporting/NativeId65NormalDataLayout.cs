using System.Buffers.Binary;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Bounded neutral geometry of the canonical untextured data chain. Actual
/// private upstream issuers supply provenance; neither a length nor this parser
/// certifies source, complete dependencies, live memory or publication.
/// </summary>
internal sealed class NativeId65NormalDataLayout
{
    private NativeId65NormalDataLayout(int used, int surfaceLength)
    {
        UsedByteLength = used;
        SurfaceByteLength = surfaceLength;
    }

    internal int UsedByteLength { get; }
    internal int SurfaceByteLength { get; }
    internal int CollisionTreeByteLength => SurfaceByteLength - 300;
    internal int SectorByteLength => 0x1000;

    internal static void RequireBoundedUsedLength(int length)
    {
        // Cells0..63 on three axes yield96 lengths. This range deliberately
        // includes fixed textured lengthA38: it is NEVER a profile test.
        if (length is < 0x8BC or > 0xA38 || (length & 3) != 0)
            throw new InvalidDataException("Normal data length is outside the bounded aligned component geometry.");
    }

    internal static NativeId65NormalDataLayout DecodeUntextured(ReadOnlySpan<byte> data)
    {
        RequireBoundedUsedLength(data.Length); // Before any slicing/allocation.
        if (Word(data, 0) != 8 || Word(data, 4) != 0)
            throw new InvalidDataException("Untextured data requires its complete zero texture-metadata owner.");
        int cursor = 8;
        for (int component = 0; component < 5; component++)
            cursor = checked(cursor + ComponentLength(data, cursor));
        int surfaceLength = cursor - 8;
        // Canonical re-encoding validates all five components, triangle winding,
        // reachability, relative pointers, guards and surface padding.
        _ = NativeId65AuthoredSurfaceLayout.DecodeAndValidate(data.Slice(8, surfaceLength));
        if (cursor > data.Length - 4 || Word(data, cursor) != 0)
            throw new InvalidDataException("Canonical fresh data requires the raw zero-portal count owner.");
        cursor += 4;
        if (ComponentLength(data, cursor) != 0x80 || Word(data, cursor + 4) != 10)
            throw new InvalidDataException("Canonical fresh data lost its complete ten-record particle allocation.");
        cursor += 0x80;
        if (ComponentLength(data, cursor) != 0x6F8 || Word(data, cursor + 0x104) != 76)
            throw new InvalidDataException("Canonical fresh data lost its complete retained sound-descriptor allocation.");
        cursor += 0x6F8;
        if (cursor != data.Length || data.Length != 0x784 + surfaceLength)
            throw new InvalidDataException("Native data used end differs from its complete owned component chain.");
        return new(data.Length, surfaceLength);
    }

    private static int ComponentLength(ReadOnlySpan<byte> data, int offset)
    {
        if (offset < 0 || offset > data.Length - 4)
            throw new InvalidDataException("Native component length field escaped the data owner.");
        int length = Word(data, offset);
        if (length < 4 || (length & 3) != 0 || offset + (long)length > data.Length)
            throw new InvalidDataException("Native component escaped its bounded aligned data owner.");
        return length;
    }

    private static int Word(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
}
