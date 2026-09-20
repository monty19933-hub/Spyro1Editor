using System.Buffers.Binary;
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
    private const int BaseColor = 0xFF;

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
        int[] materialIds = CaptureFaces(orderedFaceMaterials, input.Surface.Tiles.Count * 2,
            capturedRecords.Length);
        int growth = capturedRecords.Length * NativeStaticTerrainTextureLayout.RecordByteLength;
        if (before.Length + growth > MaximumDataByteLength)
            throw new InvalidDataException("Complete tiled material data exceeds1000; no geometry, binding or retained tail is omitted.");

        int[] faceOffsets = ReadFaceMaterialOffsets(before, input);
        byte[] metadata = NativeTerrainMaterialTableCodec.Encode(capturedRecords);
        if (metadata.Length != UntexturedMetadataByteLength + growth)
            throw new InvalidDataException("Tiled metadata does not describe the complete ordered static material table.");
        byte[] output = new byte[before.Length + growth];
        metadata.CopyTo(output, 0);
        before.AsSpan(UntexturedMetadataByteLength).CopyTo(output.AsSpan(metadata.Length));
        for (int i = 0; i < faceOffsets.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(faceOffsets[i] + growth), materialIds[i]);

        // Only these four-byte face words may differ in the translated tail.
        // The following flags, geometry, collision-relative pointers, guards,
        // visibility, sky, portal, particle and sound bytes remain unchanged.
        int nextFace = 0;
        for (int at = UntexturedMetadataByteLength; at < before.Length; at++)
        {
            if (nextFace < faceOffsets.Length && at == faceOffsets[nextFace])
            {
                at += 3;
                nextFace++;
            }
            else if (output[at + growth] != before[at])
                throw new InvalidDataException("Tiled material composition changed an unowned translated data byte.");
        }
        if (nextFace != materialIds.Length)
            throw new InvalidDataException("Tiled material composition did not cover every ordered face owner.");
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

    private static int[] CaptureFaces(IEnumerable<int> faces, int expectedCount, int recordCount)
    {
        List<int> captured = [];
        foreach (int face in faces)
        {
            if (captured.Count == expectedCount)
                throw new InvalidDataException("Tiled material bindings exceed the actual complete face count.");
            if (face != BaseColor && (face < 0 || face >= recordCount))
                throw new InvalidDataException("Tiled face material is neither an exact ordered table index nor base-color FF.");
            captured.Add(face);
        }
        if (captured.Count != expectedCount)
            throw new InvalidDataException("Tiled material bindings must name both faces of every actual tile.");
        return captured.ToArray();
    }

    private static int[] ReadFaceMaterialOffsets(ReadOnlySpan<byte> data,
        NativeId65TiledNormalDataLayout input)
    {
        var environment = input.Layout.Components[(int)NativeId65SurfaceComponentKind.Environment];
        ReadOnlySpan<byte> bytes = data.Slice(environment.Offset, environment.ByteLength);
        int tileCount = input.Surface.Tiles.Count;
        int[] result = new int[tileCount * 2];
        // DecodeUntextured validated the WHOLE canonical bundle first, including
        // directory entries and the last tile. Now use actual pointer spans and
        // header table counts, never starter-level fixed face offsets98/A8.
        for (int tile = 0; tile < tileCount; tile++)
        {
            int sector = 4 + Word(bytes, 8 + 4 * tile);
            int end = tile + 1 == tileCount ? bytes.Length : 4 + Word(bytes, 8 + 4 * (tile + 1));
            var header = Id65EnvironmentSectorHeaderContract.ReadPointerSpan(
                bytes[sector..end], end - sector, requireExactEnd: false, "tiled material sector");
            if (header.HighDetailFaceCount != 2 || header.PointerGapByteLength != 4)
                throw new InvalidDataException("Tiled material faces lost their complete canonical guarded sector owner.");
            int faceStart = Id65EnvironmentSectorHeaderContract.HeaderByteLength +
                4 * header.HighDetailVertexWordOffset + 4 * header.HighDetailVertexCount +
                8 * header.HighDetailColorCount;
            for (int face = 0; face < header.HighDetailFaceCount; face++)
            {
                int material = sector + faceStart + face * 16 + 8;
                if (Word(bytes, material) != BaseColor || Word(bytes, material + 4) != 0x80)
                    throw new InvalidDataException("Tiled face requires its complete base-color/direct-primitive preimage.");
                result[tile * 2 + face] = environment.Offset + material;
            }
        }
        return result;
    }

    private static int Word(ReadOnlySpan<byte> bytes, int at) =>
        BinaryPrimitives.ReadInt32LittleEndian(bytes[at..]);
}
