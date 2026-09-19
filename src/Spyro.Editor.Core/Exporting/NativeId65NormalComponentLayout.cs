using System.Buffers.Binary;

namespace Spyro.Editor.Core.Exporting;

internal readonly record struct NativeId65NormalDataSpan(int Offset, int ByteLength);
internal readonly record struct NativeId65NormalComponentSpan(
    NativeId65SurfaceComponentKind Kind, int Offset, int ByteLength);

/// <summary>
/// Neutral structural walk of untextured metadata, five surface components,
/// zero portals and the complete retained particle/sound allocations. Offsets
/// are data-relative; collision pointers retain their native collision+4 base.
/// This does not validate surface intent or confer source/resource authority.
/// </summary>
internal sealed class NativeId65NormalComponentLayout
{
    // The packed flat-tile producer bounds its complete surface to 0x20000.
    // The remaining owners are exactly 8 + 4 + 0x80 + 0x6F8 bytes. These are
    // parser allocation bounds, not legacy admission or a larger body policy.
    private const int MaximumSurfaceByteLength = 0x20000;
    private const int NonSurfaceByteLength = 0x784;
    private const int MinimumByteLength = NonSurfaceByteLength + 5 * 4;

    private NativeId65NormalComponentLayout(NativeId65NormalComponentSpan[] components,
        NativeId65NormalDataSpan portal, NativeId65NormalDataSpan particles,
        NativeId65NormalDataSpan sound, int used, int treeOffset, int treeLength)
    {
        Components = Array.AsReadOnly(components);
        Portal = portal;
        Particles = particles;
        Sound = sound;
        UsedByteLength = used;
        SurfaceByteLength = portal.Offset - SurfaceOffset;
        CollisionTreeOffset = treeOffset;
        CollisionTreeByteLength = treeLength;
    }

    internal IReadOnlyList<NativeId65NormalComponentSpan> Components { get; }
    internal NativeId65NormalDataSpan Portal { get; }
    internal NativeId65NormalDataSpan Particles { get; }
    internal NativeId65NormalDataSpan Sound { get; }
    internal int SurfaceOffset => 8;
    internal int SurfaceByteLength { get; }
    internal int UsedByteLength { get; }
    internal int SectorByteLength => (UsedByteLength + 0x7FF) & ~0x7FF;
    internal int CollisionTreeOffset { get; }
    internal int CollisionTreeByteLength { get; }

    internal static NativeId65NormalComponentLayout Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < MinimumByteLength || data.Length > MaximumSurfaceByteLength + NonSurfaceByteLength ||
            (data.Length & 3) != 0)
            throw new InvalidDataException("Normal component data exceeds its bounded aligned used owner.");
        if (Word(data, 0) != 8 || Word(data, 4) != 0)
            throw new InvalidDataException("Untextured data requires its complete zero texture-metadata owner.");

        var components = new NativeId65NormalComponentSpan[5];
        int cursor = 8;
        for (int i = 0; i < components.Length; i++)
        {
            int length = ComponentLength(data, cursor);
            components[i] = new((NativeId65SurfaceComponentKind)i, cursor, length);
            cursor += length; // ComponentLength proves this lies inside data.
        }
        if (cursor - 8 > MaximumSurfaceByteLength)
            throw new InvalidDataException("Surface components exceed their bounded packed owner.");
        if (cursor > data.Length - 4 || Word(data, cursor) != 0)
            throw new InvalidDataException("Normal data requires its raw zero-portal count owner.");
        var portal = new NativeId65NormalDataSpan(cursor, 4);
        cursor += portal.ByteLength;
        if (ComponentLength(data, cursor) != 0x80 || Word(data, cursor + 4) != 10)
            throw new InvalidDataException("Normal data lost its complete ten-record particle allocation.");
        var particles = new NativeId65NormalDataSpan(cursor, 0x80);
        cursor += particles.ByteLength;
        if (ComponentLength(data, cursor) != 0x6F8 || Word(data, cursor + 0x104) != 76)
            throw new InvalidDataException("Normal data lost its complete retained sound-descriptor allocation.");
        var sound = new NativeId65NormalDataSpan(cursor, 0x6F8);
        cursor += sound.ByteLength;
        if (cursor != data.Length)
            throw new InvalidDataException("Native data used end differs from its complete owned component chain.");

        var collision = components[(int)NativeId65SurfaceComponentKind.Collision];
        if (collision.ByteLength < 32)
            throw new InvalidDataException("Collision component has no complete native pointer header.");
        ReadOnlySpan<byte> bytes = data.Slice(collision.Offset, collision.ByteLength);
        // Bound and order all five target starts, without claiming canonical
        // geometry, tree reachability or triangle ownership. Variant-specific
        // surface decoders provide those stricter proofs after this walk.
        Span<int> starts = stackalloc int[5];
        for (int i = 0; i < starts.Length; i++)
        {
            long target = 4L + BinaryPrimitives.ReadUInt32LittleEndian(bytes[(12 + i * 4)..]);
            if (target < 32 || target >= bytes.Length || (target & 3) != 0 ||
                (i > 0 && target <= starts[i - 1]))
                throw new InvalidDataException("Native collision pointer escaped or overlapped its ordered component storage.");
            starts[i] = (int)target;
        }
        return new(components, portal, particles, sound, cursor,
            collision.Offset + starts[0], starts[1] - starts[0]);
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
