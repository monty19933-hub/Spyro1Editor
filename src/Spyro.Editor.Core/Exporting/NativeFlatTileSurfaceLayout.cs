using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

/// <summary>One ordered, untextured, axis-aligned native flat tile in world units.</summary>
/// <param name="FloorRgb">Raw native color word 0x00BBGGRR, not display hex 0xRRGGBB.</param>
internal sealed record NativeFlatSurfaceTile(int X, int Y, int Z, int Width, int Depth, uint FloorRgb);

/// <summary>
/// Detached geometry only: no source certificate, data/body owner, scene,
/// materials, executable, saved document, publication or gameplay authority.
/// Tile order defines sector order and each tile's two collision row identities.
/// </summary>
internal sealed class NativeFlatTileSurfaceSnapshot
{
    private readonly byte[] _bytes;

    internal NativeFlatTileSurfaceSnapshot(byte[] bytes, NativeFlatSurfaceTile[] tiles,
        uint skyRgb, NativeCollisionIndexSnapshot index)
    {
        _bytes = bytes.ToArray();
        Tiles = Array.AsReadOnly(tiles.ToArray());
        SkyRgb = skyRgb;
        CollisionIndex = index;
        List<NativeId65SurfaceComponent> components = [];
        int offset = 0;
        foreach (var kind in Enum.GetValues<NativeId65SurfaceComponentKind>())
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(_bytes.AsSpan(offset)));
            components.Add(new(kind, offset, length, Convert.ToHexStringLower(SHA256.HashData(_bytes.AsSpan(offset, length)))));
            offset += length;
        }
        Components = Array.AsReadOnly(components.ToArray());
    }

    public IReadOnlyList<NativeFlatSurfaceTile> Tiles { get; }
    public uint SkyRgb { get; }
    public IReadOnlyList<NativeId65SurfaceComponent> Components { get; }
    public NativeCollisionIndexSnapshot CollisionIndex { get; }
    public byte[] CopyBundle() => _bytes.ToArray();
    public byte[] CopyComponent(NativeId65SurfaceComponentKind kind)
    {
        var component = Components.SingleOrDefault(c => c.Kind == kind)
            ?? throw new ArgumentOutOfRangeException(nameof(kind));
        return _bytes.AsSpan(component.BlockOffset, component.ByteLength).ToArray();
    }
}

/// <summary>
/// Canonical producer/readback for complete detached flat-tile surface components.
/// Each tile keeps the established 156-byte sector grammar plus its own 4-byte
/// prefetch guard. It is not a general mesh codec or a larger-level export policy.
/// </summary>
internal static class NativeFlatTileSurfaceLayout
{
    private const int MaximumTileCount = 255; // FF terminates native visibility lists.
    private const int SectorSpan = 160;
    private const int MaximumBundleLength = 0x20000;

    internal static NativeFlatTileSurfaceSnapshot Compile(IEnumerable<NativeFlatSurfaceTile> tiles, uint skyRgb)
    {
        var owned = CaptureIntent(tiles, skyRgb);
        return DecodeAndValidate(Encode(owned, skyRgb));
    }

    internal static NativeFlatTileSurfaceSnapshot DecodeAndValidate(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is < 296 or > MaximumBundleLength || (bytes.Length & 3) != 0)
            throw new InvalidDataException("Flat-tile surface is outside its bounded five-component grammar.");
        uint countWord = U(bytes, 4);
        if (countWord is < 1 or > MaximumTileCount)
            throw new InvalidDataException("Flat-tile environment count exceeds its byte-indexed visibility grammar.");
        int count = (int)countWord;
        int environmentLength = 8 + count * (4 + SectorSpan);
        int visibilityLength = 28 + Align4(count + 1);
        int collision = environmentLength + visibilityLength + 8;
        if (collision + 32 + 16 > bytes.Length || U(bytes, 0) != environmentLength ||
            U(bytes, environmentLength) != visibilityLength || U(bytes, collision - 8) != 8)
            throw new InvalidDataException("Flat-tile component lengths do not partition their exact owners.");
        uint collisionLength = U(bytes, collision);
        if (collisionLength < 68 || collisionLength > MaximumBundleLength ||
            collision + (long)collisionLength + 16 != bytes.Length)
            throw new InvalidDataException("Flat-tile collision component consumes foreign or missing storage.");
        int sky = collision + (int)collisionLength;
        if (U(bytes, sky) != 16)
            throw new InvalidDataException("Flat-tile sky must include its exact zero-part prefetch guard.");
        NativeFlatSurfaceTile[] tiles = new NativeFlatSurfaceTile[count];
        for (int i = 0; i < count; i++)
        {
            // Recover from independently known canonical sector spans. Do not
            // dereference untrusted caller directories before complete readback.
            int sector = 8 + 4 * count + i * SectorSpan;
            tiles[i] = new((int)(U(bytes, sector + 8) >> 16) * 16,
                (int)(U(bytes, sector + 8) & 65535) * 16,
                (int)(U(bytes, sector + 12) >> 16) * 16,
                (int)(U(bytes, sector + 32) >> 21) * 16,
                (int)((U(bytes, sector + 36) >> 10) & 2047) * 16,
                U(bytes, sector + 44));
            var header = Id65EnvironmentSectorHeaderContract.ReadPointerSpan(
                bytes.Slice(sector, SectorSpan), SectorSpan, false, "generated flat tile");
            if (header.ExactSectorByteLength != 156 || header.PointerGapByteLength != 4)
                throw new InvalidDataException("Flat tile has missing, additional or unguarded sector tables.");
        }
        uint skyRgb = U(bytes, sky + 4);
        _ = CaptureIntent(tiles, skyRgb);
        if (!bytes.SequenceEqual(Encode(tiles, skyRgb)))
            throw new InvalidDataException("Flat-tile geometry, pointers, visibility, collision membership, guards or padding are not canonical.");
        ReadOnlySpan<byte> c = bytes.Slice(collision, (int)collisionLength);
        int blocks = 4 + (int)U(c, 16), triangles = 4 + (int)U(c, 20);
        var expected = BuildIndex(tiles);
        var index = NativeCollisionIndexCodec.Decode(c.Slice(32, blocks - 32),
            c.Slice(blocks, triangles - blocks), expected.UsedBlockBytes, count * 2);
        if (index.Cells.Count != expected.Cells.Count || expected.Cells.Any(pair =>
            !index.Cells.TryGetValue(pair.Key, out var actual) ||
            !actual.OrderedTriangleIndexes.SequenceEqual(pair.Value.OrderedTriangleIndexes)))
            throw new InvalidDataException("Generated tile triangles are not reachable in their complete native cells.");
        return new(bytes.ToArray(), tiles, skyRgb, index);
    }

    private static NativeFlatSurfaceTile[] CaptureIntent(IEnumerable<NativeFlatSurfaceTile> tiles, uint skyRgb)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        if (skyRgb > 0xFFFFFF)
            throw new InvalidDataException("Flat-tile sky requires a raw native RGB24 word.");
        List<NativeFlatSurfaceTile> owned = [];
        // Explicitly enumerate before allocation: caller Count/indexer/CopyTo
        // are not trusted. FF is reserved, so only sector indexes 0..254 exist.
        foreach (var tile in tiles)
        {
            if (owned.Count >= MaximumTileCount || tile == null)
                throw new InvalidDataException("Missing or excessive flat tiles.");
            if (tile.X is < 0 or > 262128 || tile.Y is < 0 or > 262128 || tile.Z is < 0 or > 262128 ||
                (tile.X & 15) != 0 || (tile.Y & 15) != 0 || (tile.Z & 15) != 0 ||
                tile.Width is < 16 or > 2048 || tile.Depth is < 16 or > 2048 ||
                (tile.Width & 15) != 0 || (tile.Depth & 15) != 0 || tile.FloorRgb > 0xFFFFFF ||
                tile.X + tile.Width > 262128 || tile.Y + tile.Depth > 262128)
                throw new InvalidDataException("Flat-tile corners/extents/colors are outside exact native packed geometry.");
            // Shared edges are valid; overlapping coplanar interiors would
            // introduce duplicate land. Different flat elevations stay distinct.
            if (owned.Any(p => p.Z == tile.Z && p.X < tile.X + tile.Width && tile.X < p.X + p.Width &&
                p.Y < tile.Y + tile.Depth && tile.Y < p.Y + p.Depth))
                throw new InvalidDataException("Flat-tile coplanar interiors overlap.");
            owned.Add(tile);
        }
        if (owned.Count == 0)
            throw new InvalidDataException("A flat-tile surface must contain at least one tile.");
        return owned.ToArray();
    }

    private static byte[] Encode(NativeFlatSurfaceTile[] tiles, uint skyRgb)
    {
        int count = tiles.Length;
        byte[] environment = new byte[8 + count * (4 + SectorSpan)];
        W(environment, 0, (uint)environment.Length);
        W(environment, 4, (uint)count);
        for (int i = 0; i < count; i++)
        {
            int sector = 8 + 4 * count + i * SectorSpan;
            W(environment, 8 + 4 * i, (uint)(sector - 4));
            EncodeSector(environment.AsSpan(sector, SectorSpan), tiles[i]);
        }
        int listLength = Align4(count + 1), skyVisibility = 16 + listLength;
        byte[] visibility = new byte[skyVisibility + 12];
        W(visibility, 0, (uint)visibility.Length);
        W(visibility, 4, (uint)(skyVisibility - 4));
        W(visibility, 8, 1);
        W(visibility, 12, 12);
        visibility.AsSpan(16, listLength).Fill(0xFF);
        for (int i = 0; i < count; i++) visibility[16 + i] = (byte)i;
        W(visibility, skyVisibility, 1);
        W(visibility, skyVisibility + 4, 8);
        visibility.AsSpan(skyVisibility + 8, 4).Fill(0xFF);
        byte[] special = new byte[8];
        W(special, 0, 8);

        var index = BuildIndex(tiles);
        byte[] tree = index.CopyTree(), blocks = index.CopyBlocks();
        int triangleCount = count * 2, triangles = 32 + tree.Length + blocks.Length;
        int assignments = triangles + triangleCount * 12, flags = assignments + Align4(triangleCount);
        byte[] collision = new byte[flags + Align4(triangleCount)];
        W(collision, 0, (uint)collision.Length);
        W(collision, 4, (uint)triangleCount);
        W(collision, 8, (uint)triangleCount);
        W(collision, 12, 28);
        W(collision, 16, (uint)(28 + tree.Length));
        W(collision, 20, (uint)(triangles - 4));
        W(collision, 24, (uint)(assignments - 4));
        W(collision, 28, (uint)(flags - 4));
        tree.CopyTo(collision, 32);
        blocks.CopyTo(collision, 32 + tree.Length);
        for (int i = 0; i < count; i++)
        {
            var tile = tiles[i];
            int x = tile.X / 16, y = tile.Y / 16, z = tile.Z / 16;
            int width = tile.Width / 16, depth = tile.Depth / 16, at = triangles + i * 24;
            // A,C,B and B,C,D: native upward land winding is negative XY.
            W(collision, at, PackAxis(x, 0, width));
            W(collision, at + 4, PackAxis(y, depth, 0));
            W(collision, at + 8, (uint)z);
            W(collision, at + 12, PackAxis(x + width, -width, 0));
            W(collision, at + 16, PackAxis(y, depth, depth));
            W(collision, at + 20, (uint)z);
        }
        collision.AsSpan(flags, triangleCount).Fill(0x3F);
        byte[] sky = new byte[16];
        W(sky, 0, 16);
        W(sky, 4, skyRgb); // Zero parts plus an owned speculative pointer word.
        return environment.Concat(visibility).Concat(special).Concat(collision).Concat(sky).ToArray();
    }

    private static NativeCollisionIndexSnapshot BuildIndex(NativeFlatSurfaceTile[] tiles)
    {
        Dictionary<NativeCollisionCell, List<int>> cells = [];
        // Descending order is stable native triangle identity. Both triangles
        // cover a tile's conservative closed AABB, not an infinite floor plane.
        // This broad-phase membership does not certify native candidate-loop
        // ordering or runtime traversal. Include a vertex on the next4096
        // boundary there as well, not just the tile-origin cell.
        for (int i = tiles.Length - 1; i >= 0; i--)
        {
            var tile = tiles[i];
            for (int y = tile.Y >> 12; y <= (tile.Y + tile.Depth) >> 12; y++)
                for (int x = tile.X >> 12; x <= (tile.X + tile.Width) >> 12; x++)
                {
                    var cell = new NativeCollisionCell(x, y, tile.Z >> 12);
                    if (!cells.TryGetValue(cell, out var rows)) cells.Add(cell, rows = []);
                    rows.Add(i * 2 + 1); rows.Add(i * 2);
                }
        }
        return NativeCollisionIndexCodec.Create(cells.Select(pair =>
            new KeyValuePair<NativeCollisionCell, IReadOnlyList<int>>(pair.Key, pair.Value)), tiles.Length * 2);
    }

    private static void EncodeSector(Span<byte> sector, NativeFlatSurfaceTile tile)
    {
        int x = tile.X / 16, y = tile.Y / 16, z = tile.Z / 16;
        int width = tile.Width / 16, depth = tile.Depth / 16;
        W(sector, 0, (uint)(((x + width / 2) << 16) | (y + depth / 2)));
        // Conservative render culling only, not player/Moby swept-path safety.
        W(sector, 4, (uint)((z << 16) | (width + depth)));
        W(sector, 8, (uint)((x << 16) | y));
        W(sector, 12, (uint)(z << 16));
        new byte[] { 4, 4, 2, 0, 4, 4, 2, 12 }.CopyTo(sector[16..]);
        W(sector, 24, uint.MaxValue); // Four signed scene indexes: -1 means unused.
        uint[] vertices = [0, (uint)width << 21, (uint)depth << 10, ((uint)width << 21) | ((uint)depth << 10)];
        for (int i = 0; i < 4; i++)
        {
            W(sector, 28 + i * 4, vertices[i]); W(sector, 76 + i * 4, vertices[i]);
            W(sector, 44 + i * 4, tile.FloorRgb);
            W(sector, 92 + i * 4, tile.FloorRgb); W(sector, 108 + i * 4, tile.FloorRgb);
        }
        int[][] faces = [[0, 1, 2], [1, 3, 2]];
        for (int i = 0; i < faces.Length; i++)
        {
            int a = faces[i][0], b = faces[i][1], c = faces[i][2];
            uint low = (uint)((a << 26) | (b << 20) | (c << 14) | (c << 8));
            uint high = (uint)(a | (a << 8) | (b << 16) | (c << 24));
            W(sector, 60 + i * 8, low); W(sector, 64 + i * 8, low);
            W(sector, 124 + i * 16, high); W(sector, 128 + i * 16, high);
            W(sector, 132 + i * 16, 0xFF); // No texture reference.
            W(sector, 136 + i * 16, 0x80); // Existing direct primitive path only.
        }
        Id65EnvironmentSectorHeaderContract.Stamp(sector[..156]);
        // +156..159 remains a separate owned zero guard, including between tiles.
    }

    private static uint PackAxis(int origin, int deltaB, int deltaC)
    {
        if (origin is < 0 or > 16383 || deltaB is < -256 or > 255 || deltaC is < -256 or > 255)
            throw new InvalidDataException("Native collision axis cannot represent the flat tile exactly.");
        return (uint)origin | (((uint)deltaB & 511) << 14) | (((uint)deltaC & 511) << 23);
    }
    private static int Align4(int value) => (value + 3) & ~3;
    private static uint U(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static void W(Span<byte> bytes, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes[at..], value);
}
