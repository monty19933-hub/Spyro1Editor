using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Neutral bounded composition of complete static records and ordered face
/// bindings into canonical flat-tile data. This neither allocates texture VRAM
/// nor authenticates records, documents, retained tails or a native resource
/// revision. Existing source-issued tiled/legacy admission remains separate.
/// </summary>
internal static class NativeId65TiledMaterialDataLayout
{
    private const int MaximumRecordCount = 128; // Direct-LQ indexes, not masked IDs.
    private const int MaximumDataByteLength = 0x1000;
    private const int UntexturedMetadataByteLength = 8;

    internal static byte[] Compose(ReadOnlySpan<byte> untexturedData,
        IEnumerable<NativeStaticTerrainTextureLayout> records,
        IEnumerable<int> orderedFaceMaterials, int nativeClip)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(orderedFaceMaterials);
        if (nativeClip != 0x8000)
            throw new InvalidDataException("Tiled direct-LQ materials require the exact admitted native clip8000.");
        if (untexturedData.Length > MaximumDataByteLength)
            throw new InvalidDataException("Tiled material input already exceeds the complete two-sector data allocation.");

        // Capture before invoking any caller-owned enumerator. Its callbacks
        // may mutate caller buffers, but cannot change this validated revision.
        byte[] before = untexturedData.ToArray();
        var input = NativeId65TiledNormalDataLayout.DecodeUntextured(before);
        NativeStaticTerrainTextureLayout[] capturedRecords = CaptureRecords(records);
        byte[] boundSurface = NativeFlatTileMaterialBinding.Bind(input.Surface.CopyBundle(),
            orderedFaceMaterials, capturedRecords.Length);
        int growth = capturedRecords.Length * NativeStaticTerrainTextureLayout.RecordByteLength;
        if (before.Length + growth > MaximumDataByteLength)
            throw new InvalidDataException("Complete tiled material data exceeds1000; no geometry, binding or retained tail is omitted.");

        byte[] metadata = NativeTerrainMaterialTableCodec.Encode(capturedRecords);
        if (metadata.Length != UntexturedMetadataByteLength + growth)
            throw new InvalidDataException("Tiled metadata does not describe the complete ordered static material table.");
        byte[] output = new byte[before.Length + growth];
        metadata.CopyTo(output, 0);
        before.AsSpan(UntexturedMetadataByteLength).CopyTo(output.AsSpan(metadata.Length));
        boundSurface.CopyTo(output, input.Layout.SurfaceOffset + growth);
        // The shared binder proves the complete surface complement. Preserve
        // this P0 owner's actual portal/particle/sound suffix independently;
        // it is never substituted for a family resource tail.
        int tail = input.Layout.SurfaceOffset + input.Layout.SurfaceByteLength;
        if (!output.AsSpan(tail + growth).SequenceEqual(before.AsSpan(tail)))
            throw new InvalidDataException("Tiled material composition changed an unowned retained data suffix.");
        return output;
    }

    private static NativeStaticTerrainTextureLayout[] CaptureRecords(
        IEnumerable<NativeStaticTerrainTextureLayout> records)
    {
        List<NativeStaticTerrainTextureLayout> captured = [];
        foreach (var record in records)
        {
            if (captured.Count == MaximumRecordCount || record is null)
                throw new InvalidDataException("Tiled material records exceed their bounded non-null direct-LQ table.");
            // Decoded layouts are sealed immutable owners; later replacement
            // of a caller collection element cannot swap the captured record.
            captured.Add(record);
        }
        return captured.ToArray();
    }

}
