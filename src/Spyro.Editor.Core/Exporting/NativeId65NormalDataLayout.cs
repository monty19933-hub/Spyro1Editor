namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Bounded neutral geometry of the canonical untextured data chain. Actual
/// private upstream issuers supply provenance; neither a length nor this parser
/// certifies source, complete dependencies, live memory or publication.
/// </summary>
internal sealed class NativeId65NormalDataLayout
{
    private readonly NativeId65NormalComponentLayout _components;

    private NativeId65NormalDataLayout(NativeId65NormalComponentLayout components) => _components = components;

    internal int UsedByteLength => _components.UsedByteLength;
    internal int SurfaceByteLength => _components.SurfaceByteLength;
    internal int CollisionTreeByteLength => _components.CollisionTreeByteLength;
    internal int SectorByteLength => _components.SectorByteLength;

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
        var components = NativeId65NormalComponentLayout.Decode(data);
        // Canonical re-encoding validates all five components, triangle winding,
        // reachability, relative pointers, guards and surface padding.
        _ = NativeId65AuthoredSurfaceLayout.DecodeAndValidate(
            data.Slice(components.SurfaceOffset, components.SurfaceByteLength));
        return new(components);
    }
}
