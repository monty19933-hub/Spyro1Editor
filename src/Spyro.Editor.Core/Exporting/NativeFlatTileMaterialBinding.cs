using System.Buffers.Binary;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Source-neutral face-word binding shared by tiled P0 and family composition.
/// It owns only material words in a complete canonical flat-tile surface, not
/// texture records, resource tails, source identity or export admission.
/// </summary>
internal static class NativeFlatTileMaterialBinding
{
    internal static byte[] Bind(ReadOnlySpan<byte> canonicalSurface,
        IEnumerable<int> orderedFaces, int recordCount)
    {
        ArgumentNullException.ThrowIfNull(orderedFaces);
        RequireLength(canonicalSurface.Length);
        if (recordCount is < 0 or > 128)
            throw new InvalidDataException("Flat-tile materials require a bounded direct-LQ table of0..128 records.");
        // Capture and validate before invoking a caller-owned enumerator.
        byte[] before = canonicalSurface.ToArray();
        var surface = NativeFlatTileSurfaceLayout.DecodeAndValidate(before);
        int[] fields = ReadFields(before, surface);
        List<int> faces = [];
        foreach (int face in orderedFaces)
        {
            if (faces.Count == fields.Length || (face != 255 && (face < 0 || face >= recordCount)))
                throw new InvalidDataException("Every flat-tile face requires one exact table index or base-color FF; no truncation or masking.");
            faces.Add(face);
        }
        if (faces.Count != fields.Length)
            throw new InvalidDataException("Material bindings must cover both faces of every actual tile.");
        byte[] bound = before.ToArray();
        for (int i = 0; i < fields.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(bound.AsSpan(fields[i]), faces[i]);
        byte[] inverse = bound.ToArray();
        foreach (int at in fields) BinaryPrimitives.WriteInt32LittleEndian(inverse.AsSpan(at), 255);
        if (!inverse.AsSpan().SequenceEqual(before))
            throw new InvalidDataException("Face binding changed the complete geometry/flag/guard complement.");
        return bound;
    }

    internal static byte[] RevertExact(ReadOnlySpan<byte> canonicalSurface,
        ReadOnlySpan<byte> boundSurface, IEnumerable<int> orderedFaces, int recordCount)
    {
        // Both owners must precede any callback, including material iteration.
        RequireLength(canonicalSurface.Length);
        if (boundSurface.Length != canonicalSurface.Length)
            throw new InvalidDataException("Inverse requires two complete equally sized surface owners.");
        byte[] before = canonicalSurface.ToArray(), bound = boundSurface.ToArray();
        if (!Bind(before, orderedFaces, recordCount).AsSpan().SequenceEqual(bound))
            throw new InvalidDataException("Inverse requires the entire exact bound surface, not only material words.");
        return before;
    }

    private static int[] ReadFields(byte[] bytes, NativeFlatTileSurfaceSnapshot surface)
    {
        var environment = surface.Components.Single(c => c.Kind == NativeId65SurfaceComponentKind.Environment);
        int count = surface.Tiles.Count;
        int[] fields = new int[count * 2];
        for (int tile = 0; tile < count; tile++)
        {
            int start = checked(environment.BlockOffset + 4 + Word(bytes, environment.BlockOffset + 8 + tile * 4));
            int end = tile + 1 == count ? environment.BlockOffset + environment.ByteLength :
                checked(environment.BlockOffset + 4 + Word(bytes, environment.BlockOffset + 8 + (tile + 1) * 4));
            var header = Id65EnvironmentSectorHeaderContract.ReadPointerSpan(bytes.AsSpan(start, end - start),
                end - start, requireExactEnd: false, "flat-tile material sector");
            if (header.HighDetailFaceCount != 2 || header.PointerGapByteLength != 4)
                throw new InvalidDataException("Flat-tile material fields require the complete two-face guarded sector.");
            int faceStart = start + Id65EnvironmentSectorHeaderContract.HeaderByteLength +
                4 * header.HighDetailVertexWordOffset + 4 * header.HighDetailVertexCount + 8 * header.HighDetailColorCount;
            for (int face = 0; face < 2; face++)
            {
                int at = faceStart + face * 16 + 8;
                if (at < start || at > end - 8 || Word(bytes, at) != 255 || Word(bytes, at + 4) != 0x80)
                    throw new InvalidDataException("Flat-tile binding requires its canonical FF/80 primitive preimage.");
                fields[tile * 2 + face] = at;
            }
        }
        if (fields.Distinct().Count() != fields.Length)
            throw new InvalidDataException("Flat-tile material fields must be disjoint complete words.");
        return fields;
    }

    private static int Word(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadInt32LittleEndian(bytes[at..]);

    private static void RequireLength(int length)
    {
        if (length is < 296 or > 0x20000 || (length & 3) != 0)
            throw new InvalidDataException("Flat-tile bindings require the bounded complete five-component surface.");
    }
}
