using System.Buffers.Binary;
using System.Collections;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;

internal static class FlatTileSurfaceCases
{
    internal static void Run(Action<bool, string> require)
    {
        NativeFlatSurfaceTile[] tiles =
        [
            new(8192, 8192, 8704, 2048, 2048, 0x708050),
            new(10240, 8192, 8704, 2048, 2048, 0x123456),
            new(12288, 8192, 8704, 2048, 2048, 0xABCDEF)
        ];
        var surface = NativeFlatTileSurfaceLayout.Compile(tiles, 0x406080);
        byte[] bytes = surface.CopyBundle();
        require(surface.Tiles.SequenceEqual(tiles) && surface.SkyRgb == 0x406080 &&
            surface.CollisionIndex.TriangleCount == 6, "Ordered tiles retain their independent geometry and colors.");
        require(bytes.Length == 724 && surface.Components.Select(c => c.ByteLength)
            .SequenceEqual(new[] { 500, 32, 8, 168, 16 }),
            "Three real sectors and six collision triangles determine every component length.");
        require(NativeFlatTileSurfaceLayout.DecodeAndValidate(bytes).Tiles.SequenceEqual(tiles),
            "Whole tiled native surface independently recovers authored intent.");
        require(surface.CollisionIndex.Cells.Count == 2 &&
            surface.CollisionIndex.Cells[new(2, 2, 2)].OrderedTriangleIndexes.SequenceEqual(new[] { 3, 2, 1, 0 }) &&
            surface.CollisionIndex.Cells[new(3, 2, 2)].OrderedTriangleIndexes.SequenceEqual(new[] { 5, 4, 3, 2 }),
            "Middle tile's exact closed boundary is present on BOTH sides of the native cell seam.");

        byte[] environment = surface.CopyComponent(NativeId65SurfaceComponentKind.Environment);
        require(U(environment, 0) == 500 && U(environment, 4) == 3 &&
            new[] { U(environment, 8), U(environment, 12), U(environment, 16) }.SequenceEqual(new uint[] { 16, 176, 336 }),
            "Actual sector directory uses component+4 bases with independent guarded sector spans.");
        for (int i = 0; i < tiles.Length; i++)
        {
            int sector = 20 + 160 * i;
            var tile = tiles[i];
            require(environment.AsSpan(sector + 156, 4).IndexOfAnyExcept((byte)0) < 0,
                "Every sector, including interior sectors, owns a zero speculative face-read guard.");
            var header = Id65EnvironmentSectorHeaderContract.ReadPointerSpan(environment.AsSpan(sector, 160),
                160, false, "tiled fixture");
            require(header.ExactSectorByteLength == 156 && header.PointerGapByteLength == 4,
                "Existing native sector grammar validates each newly generated sector.");
            require(U(environment, sector) == (uint)(((tile.X / 16 + 64) << 16) | (tile.Y / 16 + 64)) &&
                U(environment, sector + 4) == (uint)((tile.Z / 16 << 16) | 256) &&
                U(environment, sector + 24) == uint.MaxValue, "Per-tile render culling and disabled scene consumers.");
            uint[] vertices = [0, 128u << 21, 128u << 10, (128u << 21) | (128u << 10)];
            for (int vertex = 0; vertex < 4; vertex++)
            {
                require(U(environment, sector + 28 + 4 * vertex) == vertices[vertex] &&
                    U(environment, sector + 76 + 4 * vertex) == vertices[vertex], "All LP/HP corners share exact tile geometry.");
                foreach (int table in new[] { 44, 92, 108 })
                    require(U(environment, sector + table + 4 * vertex) == tile.FloorRgb,
                        "Independent tile color reaches all three native color tables.");
            }
            foreach (int face in new[] { 0, 1 })
                require(U(environment, sector + 132 + 16 * face) == 0xFF &&
                    U(environment, sector + 136 + 16 * face) == 0x80,
                    "No texture or subdivision dependency is invented by tiled geometry.");
        }
        byte[] visibility = surface.CopyComponent(NativeId65SurfaceComponentKind.Visibility);
        require(visibility.SequenceEqual(new byte[]
        {
            32,0,0,0, 16,0,0,0, 1,0,0,0, 12,0,0,0,
            0,1,2,255, 1,0,0,0, 8,0,0,0, 255,255,255,255
        }), "One exact native visibility group enumerates every generated sector and no sky parts.");
        byte[] collision = surface.CopyComponent(NativeId65SurfaceComponentKind.Collision);
        int triangleStart = 4 + (int)U(collision, 20), assignments = 4 + (int)U(collision, 24);
        int flags = 4 + (int)U(collision, 28);
        require(U(collision, 4) == 6 && U(collision, 8) == 6 && assignments == triangleStart + 72 &&
            flags == assignments + 8 && collision.AsSpan(assignments, 8).IndexOfAnyExcept((byte)0) < 0 &&
            collision.AsSpan(flags, 8).SequenceEqual(new byte[] { 63,63,63,63,63,63,0,0 }),
            "All native collision rows own group-zero visibility, dry-land flags, and explicit alignment.");
        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            var a = (t.X, t.Y, t.Z); var b = (t.X + t.Width, t.Y, t.Z);
            var c = (t.X, t.Y + t.Depth, t.Z); var d = (t.X + t.Width, t.Y + t.Depth, t.Z);
            require(Triangle(collision, triangleStart + 24 * i).SequenceEqual(new[] { a, c, b }) &&
                Triangle(collision, triangleStart + 24 * i + 12).SequenceEqual(new[] { b, c, d }),
                "Decoded signed-delta collision corners match both actual visible triangles with native upward winding.");
        }
        // Independent software geometry oracle, NOT execution of the native
        // collision routine: broad-phase over-inclusion must not imply that a
        // triangle is an infinite plane. Resolve actual decoded corners first.
        require(FloorHits(12272, 9216, 8704).SequenceEqual(new[] { 3 }) &&
            FloorHits(12288, 9216, 8704).SequenceEqual(new[] { 4, 3 }) &&
            FloorHits(12304, 9216, 8704).SequenceEqual(new[] { 4 }),
            "Independent finite-triangle oracle covers seam-minus16, seam, seam-plus16 with adjacent real faces.");
        require(FloorHits(9000, 11000, 8704).Length == 0 && FloorHits(14500, 9216, 8704).Length == 0 &&
            FloorHits(9000, 9216, 8720).Length == 0,
            "Candidates in the same coarse cell do not become floor outside the actual rectangle or plane.");

        // Alternate row/height and a corner touching four cells exercise more
        // than the horizontal example without claiming swept or gameplay safety.
        var corner = new NativeFlatSurfaceTile(4080, 4080, 4080, 32, 32, 1);
        var cornerSurface = NativeFlatTileSurfaceLayout.Compile(new[] { corner }, 0);
        require(cornerSurface.CollisionIndex.Cells.Keys.OrderBy(c => c.Y).ThenBy(c => c.X)
            .SequenceEqual(new[] { new NativeCollisionCell(0,0,0), new(1,0,0), new(0,1,0), new(1,1,0) }),
            "Closed tile bounds conservatively include each of four corner cells.");
        var stacked = NativeFlatTileSurfaceLayout.Compile(new[] { corner, corner with { Z = 8192 } }, 0);
        require(stacked.CollisionIndex.Cells.Count == 8, "Separate flat elevations own distinct Z branches.");
        var four = NativeFlatTileSurfaceLayout.Compile(tiles.Append(tiles[2] with { X = 14336 }), 0);
        byte[] v4 = four.CopyComponent(NativeId65SurfaceComponentKind.Visibility);
        require(v4.Length == 36 && U(v4, 4) == 20 &&
            v4.AsSpan(16, 8).SequenceEqual(new byte[] { 0,1,2,3,255,255,255,255 }),
            "Fourth sector relocates sky visibility using aligned directory-owned bytes.");
        var maximumTiles = Enumerable.Range(0, 255).Select(i => new NativeFlatSurfaceTile(16 + 32 * i, 16, 16, 16, 16, (uint)i)).ToArray();
        var maximum = NativeFlatTileSurfaceLayout.Compile(maximumTiles, 0xFFFFFF);
        byte[] vmax = maximum.CopyComponent(NativeId65SurfaceComponentKind.Visibility);
        require(maximum.Tiles.Count == 255 && vmax.Length == 284 && vmax[270] == 254 && vmax[271] == 255,
            "Largest non-sentinel byte sector index is representable without truncating the visibility list.");

        byte[] before = surface.CopyBundle();
        tiles[0] = tiles[0] with { FloorRgb = 0 };
        bytes[0] ^= 1; environment[0] ^= 1;
        require(surface.CopyBundle().SequenceEqual(before) && surface.Tiles[0].FloorRgb == 0x708050,
            "Caller collection and returned component/bundle buffers cannot change frozen geometry.");
        require(!NativeFlatTileSurfaceLayout.Compile(surface.Tiles.Reverse(), surface.SkyRgb).CopyBundle().SequenceEqual(before),
            "Tile order is a deliberate native sector and face identity, not a sorted coordinate set.");
        Reject(() => NativeFlatTileSurfaceLayout.Compile(null!, 0), "Null tiles refuse.");
        Reject(() => NativeFlatTileSurfaceLayout.Compile(Array.Empty<NativeFlatSurfaceTile>(), 0), "Empty surface refuses.");
        Reject(() => NativeFlatTileSurfaceLayout.Compile(new NativeFlatSurfaceTile[] { null! }, 0), "Null tile refuses.");
        Reject(() => NativeFlatTileSurfaceLayout.Compile(maximumTiles.Append(maximumTiles[0] with { X = 9000 }), 0),
            "Sector count stops before the FF visibility sentinel.");
        Reject(() => NativeFlatTileSurfaceLayout.Compile(new[] { corner, corner }, 0), "Duplicate coplanar tile refuses.");
        Reject(() => NativeFlatTileSurfaceLayout.Compile(new[] { corner, corner with { X = 4096 } }, 0),
            "Overlapping coplanar interiors refuse ambiguous duplicate land.");
        foreach (var bad in new[]
        {
            corner with { X = -16 }, corner with { Y = 1 }, corner with { Z = 262144 },
            corner with { Width = 0 }, corner with { Depth = 3088 }, corner with { Width = int.MaxValue },
            corner with { X = 262128 }, corner with { Y = 262128 }, corner with { FloorRgb = 0x1000000 }
        }) Reject(() => NativeFlatTileSurfaceLayout.Compile(new[] { bad }, 0), "Unrepresentable native corners/extents/colors refuse.");
        Reject(() => NativeFlatTileSurfaceLayout.Compile(new[] { corner }, 0x1000000), "Unsupported sky high bits refuse.");
        var extreme = corner with { X = 262112, Y = 262112, Z = 262128, Width = 16, Depth = 16 };
        require(NativeFlatTileSurfaceLayout.Compile(new[] { extreme }, 0).Tiles[0] == extreme,
            "All-corner world bounds admit exact maximum representable vertices.");
        var liar = new LyingTiles(surface.Tiles);
        require(NativeFlatTileSurfaceLayout.Compile(liar, surface.SkyRgb).CopyBundle().SequenceEqual(before),
            "Compilation enumerates tiles without trusting caller Count, indexer or CopyTo.");
        int yielded = 0;
        Reject(() => NativeFlatTileSurfaceLayout.Compile(Unending(), 0), "Unbounded outer input stops before allocation growth.");
        require(yielded == 256, "Outer tile enumeration has an explicit native-count bound.");

        foreach (int at in new[] { 0, 4, 8, 12, 16, 20 + 156, 180 + 156, 340 + 156, 500 + 4, 500 + 16,
            500 + 19, 540 + 12, 540 + 16, 540 + triangleStart, 540 + assignments, 540 + flags + 6, 724 - 4 })
        {
            byte[] corrupt = before.ToArray(); corrupt[at] ^= 0x80;
            Reject(() => NativeFlatTileSurfaceLayout.DecodeAndValidate(corrupt),
                "Noncanonical directory, guard, seam, winding, visibility, flags or tail refuses.");
        }
        foreach (var bad in new[] { before[..^1], before.Concat(new byte[4]).ToArray(), new byte[0], new byte[0x20004] })
            Reject(() => NativeFlatTileSurfaceLayout.DecodeAndValidate(bad), "Incomplete or unowned component bytes refuse.");

        IEnumerable<NativeFlatSurfaceTile> Unending()
        {
            while (true) { yielded++; yield return new(16 + yielded * 32, 16, 16, 16, 16, 0); }
        }
        int[] FloorHits(int x, int y, int z)
        {
            if (!surface.CollisionIndex.Cells.TryGetValue(new(x >> 12, y >> 12, z >> 12), out var binding)) return [];
            return binding.OrderedTriangleIndexes.Where(row =>
            {
                var points = Triangle(collision, triangleStart + row * 12);
                if (points.Any(p => p.Item3 != z)) return false;
                for (int edge = 0; edge < 3; edge++)
                {
                    var a = points[edge]; var b = points[(edge + 1) % 3];
                    if ((long)(b.Item1 - a.Item1) * (y - a.Item2) -
                        (long)(b.Item2 - a.Item2) * (x - a.Item1) > 0) return false;
                }
                return true;
            }).ToArray();
        }
        void Reject(Action action, string message)
        {
            try { action(); }
            catch (Exception e) when (e is InvalidDataException or ArgumentException) { require(true, message); return; }
            throw new InvalidOperationException(message);
        }
    }

    private static (int, int, int)[] Triangle(byte[] bytes, int at)
    {
        uint x = U(bytes, at), y = U(bytes, at + 4), z = U(bytes, at + 8);
        int ox = (int)(x & 16383) * 16, oy = (int)(y & 16383) * 16, oz = (int)(z & 16383) * 16;
        return [(ox,oy,oz), (ox + Signed9(x >> 14) * 16, oy + Signed9(y >> 14) * 16, oz + (int)((z >> 16) & 255) * 16),
            (ox + Signed9(x >> 23) * 16, oy + Signed9(y >> 23) * 16, oz + (int)(z >> 24) * 16)];
    }
    private static int Signed9(uint word)
    {
        int value = (int)word & 511;
        return value >= 256 ? value - 512 : value;
    }
    private static uint U(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
    private sealed class LyingTiles(IReadOnlyList<NativeFlatSurfaceTile> tiles) : IReadOnlyList<NativeFlatSurfaceTile>
    {
        public int Count => throw new InvalidOperationException("Do not trust caller Count.");
        public NativeFlatSurfaceTile this[int index] => throw new InvalidOperationException("Do not trust caller indexer.");
        public IEnumerator<NativeFlatSurfaceTile> GetEnumerator() => tiles.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
