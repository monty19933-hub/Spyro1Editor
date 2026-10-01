using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Spyro.Editor.Core.Universal;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeFreshPlacementArenaSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>Extends only the source-issued fresh zero-actor scene's empty Moby
/// arena. This is not a general retail scene relocation or a capacity waiver.</summary>
internal static class NativeFreshPlacementArena
{
    internal static byte[] Prepare(byte[] originalBody, int activePlacements)
    {
        if (activePlacements is < 0 or > 256)
            throw new InvalidDataException("Fresh placement count exceeds the native 256-row save limit.");
        var body = new UniversalLevelData(originalBody.ToArray());
        var file = body.Subfiles()[3];
        var canonical = NativeId65FreshSceneLayout.DecodeCanonical(body.Bytes.AsSpan(file.Offset, file.Length));
        if (canonical.NativeNominalCapacity != UniversalMobyComposer.MinimumDynamicSlots)
            throw new InvalidDataException("Fresh placement preparation requires the original zero-actor16-slot scene.");
        // The issuer's 0x58 row reserve accommodates the first placement without
        // changing any output byte. Further rows need their own complete storage.
        if (activePlacements <= 1) return body.Bytes;
        var layout = body.Scene();
        int added = checked((activePlacements - 1) * UniversalLevelData.RowStride);
        int insert = layout.PropertiesComponent;
        byte[] scene = new byte[checked(layout.UsedEnd + added)];
        body.Bytes.AsSpan(file.Offset, insert).CopyTo(scene);
        body.Bytes.AsSpan(file.Offset + insert, layout.UsedEnd - insert).CopyTo(scene.AsSpan(insert + added));
        BinaryPrimitives.WriteInt32LittleEndian(scene.AsSpan(layout.MobyComponent), checked(layout.MobyComponentSize + added));
        // Canonical decoding authenticated every zero row/property/bucket byte,
        // the empty pod list, zero fixups and sector padding. No scene-relative
        // pointer exists to repair. Native scene loader derives the following
        // component bases from their lengths (not fixed offsets).
        body.ReplaceScene(scene);
        var final = body.Scene();
        if (final.RowCount != 0 || final.Fixups.Count != 0 ||
            final.PropertiesComponent != insert + added ||
            final.PodComponent != layout.PodComponent + added ||
            final.CollisionComponent != layout.CollisionComponent + added ||
            final.FixupsOffset != layout.FixupsOffset + added ||
            (final.ArenaEnd - final.RowsOffset - activePlacements * UniversalLevelData.RowStride) / 0x70 != UniversalMobyComposer.MinimumDynamicSlots)
            throw new InvalidDataException("Fresh actor arena relocation lost an owned component or dynamic reserve.");
        return body.Bytes;
    }
}
