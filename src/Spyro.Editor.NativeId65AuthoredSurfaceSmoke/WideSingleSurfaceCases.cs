using System.Buffers.Binary;
using System.Text;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

/// <summary>
/// Source-free geometry/grammar checks, not native execution, source authority,
/// player-footprint clearance or permission to widen an existing export profile.
/// The larger single rectangle must not silently widen schema4's tile admission.
/// </summary>
internal static class WideSingleSurfaceCases
{
    private const string TileId = "33333333333333333333333333333333";
    private static readonly ColorRgba FloorColor = new(0x50, 0x80, 0x70);
    private static readonly ColorRgba SkyColor = new(0x80, 0x60, 0x40);

    internal static void Run(Action<bool, string> check)
    {
        var baselineFloor = new NativeId65AuthoredFloor(8448, 8448, 8704, 2048, 2048, 0x708050, 0x406080);
        var baseline = NativeId65AuthoredSurfaceLayout.Compile(baselineFloor);
        var wideFloor = baselineFloor with { Width = 3072, Depth = 3072 };
        // First new success is the deliberate behavioral RED against the old2048 policy.
        var wide = NativeId65AuthoredSurfaceLayout.Compile(wideFloor);
        check(wide.ByteLength == 0x144 && wide.Components.Select(c => c.ByteLength)
            .SequenceEqual(new[] { 172, 32, 8, 96, 16 }),
            "3072 in the existing cell changes geometry, not component allocation or five-component grammar.");
        check(!wide.SourceCertified && !wide.PublicationApproved && !wide.RuntimeVerified &&
            !wide.IndependentLevelSupported && !wide.CompleteLevelResourcesProven,
            "Wider neutral bytes cannot issue source, runtime, publication or complete-resource authority.");

        // These literal packed words come from world/16 corners and signed9
        // deltas192/-192, not from production output or a new production golden.
        LiteralWords(wide, 0x02700270, 0x02200180,
            new uint[] { 0, 0x18000000, 0x00030000, 0x18030000 },
            new uint[] { 0x60000210, 0x00300210, 0x00000220, 0x005002D0, 0x60300210, 0x00000220 });
        var beyondOldCap = NativeId65AuthoredSurfaceLayout.Compile(baselineFloor with { Width = 2064, Depth = 2064 });
        //129 is intentionally odd: the native culling center floors half-units.
        LiteralWords(beyondOldCap, 0x02500250, 0x02200102,
            new uint[] { 0, 0x10200000, 0x00020400, 0x10220400 },
            new uint[] { 0x40800210, 0x00204210, 0x00000220, 0x005FC291, 0x40A04210, 0x00000220 });

        foreach (var floor in new[]
        {
            wideFloor, baselineFloor with { Width = 2064, Depth = 2064 },
            baselineFloor with { Width = 3072, Depth = 2064 },
            baselineFloor with { Width = 2064, Depth = 3072 },
            baselineFloor with { Width = 3072, Depth = 16 },
            baselineFloor with { Width = 16, Depth = 3072 }
        })
        {
            var actual = NativeId65AuthoredSurfaceLayout.Compile(floor);
            Geometry(actual, floor);
            Complement(baseline, actual);
            check(actual.CopyBundle().SequenceEqual(NativeId65AuthoredSurfaceLayout.Compile(floor).CopyBundle()),
                "Each wider or asymmetric rectangle is deterministic.");
        }

        // Exact closed-cell and world-corner bounds, including the final16-unit
        // step. Origin-only validation cannot stand in for all-corner validation.
        foreach (var floor in new[]
        {
            wideFloor with { X = 0, Y = 0, Z = 0 },
            wideFloor with { X = 9200, Y = 9200 }, // far corners12272, not12288
            wideFloor with { X = 259056, Y = 259056, Z = 262128 }
        }) Geometry(NativeId65AuthoredSurfaceLayout.Compile(floor), floor);
        foreach (var bad in new[]
        {
            wideFloor with { Width = 3088 }, wideFloor with { Depth = 3088 },
            wideFloor with { Width = 4080 }, wideFloor with { Depth = 4096 },
            wideFloor with { Width = 3071 }, wideFloor with { Depth = 3073 },
            wideFloor with { Width = 0 }, wideFloor with { Depth = -16 },
            wideFloor with { X = 9216 }, wideFloor with { Y = 9216 },
            wideFloor with { X = 259072 }, wideFloor with { Y = 259072 },
            wideFloor with { Z = 262144 }, wideFloor with { X = int.MaxValue },
            wideFloor with { Width = int.MaxValue }, wideFloor with { Depth = int.MaxValue }
        }) Reject(() => NativeId65AuthoredSurfaceLayout.Compile(bad),
            "Single-floor3072 policy, exact same-cell, aligned dimensions and complete world bounds refuse.");
        foreach (var bad in new[]
        {
            new NativeFlatSurfaceTile(262128, 16, 16, 16, 16, 0),
            new NativeFlatSurfaceTile(16, 262128, 16, 16, 16, 0)
        }) Reject(() => NativeFlatTileSurfaceLayout.Compile([bad], 0),
            "The shared producer independently refuses an unrepresentable far corner, not just the legacy cell restriction.");

        var document = Document(3072, 3072);
        byte[] canonical = NativeId65FreshDocumentCodec.SerializeCanonical(document);
        const string expectedDocument = "{\"kind\":\"spyro-native-fresh-intent\",\"schema\":1,\"floor\":{\"x\":8448,\"y\":8448,\"z\":8704,\"width\":3072,\"depth\":3072},\"spawn\":{\"x\":8960,\"y\":8960,\"heading\":64},\"display\":{\"floorRgb\":[80,128,112],\"skyRgb\":[128,96,64]},\"musicTrackId\":19}";
        check(canonical.SequenceEqual(Encoding.UTF8.GetBytes(expectedDocument)),
            "Larger single-floor intent uses the same exact schema1 JSON grammar and actual dimensions.");
        check(NativeId65FreshDocumentCodec.SerializeCanonical(NativeId65FreshDocumentCodec.Parse(canonical))
            .SequenceEqual(canonical) && document.SpawnZ == 9216 && document.SpawnX == 8960 && document.SpawnY == 8960,
            "Wider neutral document roundtrips without fabricating a different spawn or floor.");
        check(Document(2064, 3072).FloorWidth == 2064, "Document validation admits aligned dimensions beyond the old cap.");
        Reject(() => Document(3088, 3072), "Document validation retains the new explicit3072 maximum.");
        Reject(() => NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 3072, 3072,
            11520, 8960, 64, FloorColor, SkyColor, 19), "Far-edge spawn is not a strict interior point.");
        var top = NativeId65FreshDocumentIntent.Create(259056, 259056, 261616, 3072, 3072,
            259568, 259568, 64, FloorColor, SkyColor, 19);
        check(top.SpawnZ == 262128, "Maximum representable spawn remains exactly512 above the actual plane.");
        Reject(() => NativeId65FreshDocumentIntent.Create(259056, 259056, 261632, 3072, 3072,
            259568, 259568, 64, FloorColor, SkyColor, 19), "A valid surface cannot hide an overflowing derived spawn.");

        var materials = NativeFreshFloorMaterialsIntent.Create(
            NativeFreshFloorMaterialIntent.BaseFloorColor(), NativeFreshFloorMaterialIntent.BaseFloorColor());
        check(NativeFreshFlatTileIntent.Create(TileId, 8448, 8448, 8704, 2048, 2048, FloorColor, materials).Width == 2048,
            "Existing schema4 boundary remains supported.");
        foreach (int width in new[] { 2064, 3072 })
        {
            Reject(() => NativeFreshFlatTileIntent.Create(TileId, 8448, 8448, 8704, width, 2048, FloorColor, materials),
                "Schema4 width admission remains explicitly2048 despite shared packing support.");
            Reject(() => NativeFreshFlatTileIntent.Create(TileId, 8448, 8448, 8704, 2048, width, FloorColor, materials),
                "Schema4 depth admission remains explicitly2048 despite shared packing support.");
            var legacy = NativeFreshLevelDocumentIntent.Create(Document(width, width), []);
            byte[] before = NativeFreshLevelDocumentCodec.SerializeCanonical(legacy);
            Reject(() => legacy.UpgradeToTiled(TileId), "An explicit upgrade cannot bypass schema4's preserved tile cap.");
            check(NativeFreshLevelDocumentCodec.SerializeCanonical(legacy).SequenceEqual(before),
                "Refused tiled upgrade preserves the complete actual legacy document.");
        }

        byte[] frozen = wide.CopyBundle();
        int triangleStart = CollisionStart(wide) + 4 + (int)U(frozen, CollisionStart(wide) + 20);
        foreach (int at in new[] { 12, 16, 44, 48, 52, 92, 96, 100,
            triangleStart, triangleStart + 4, triangleStart + 8, triangleStart + 12,
            triangleStart + 16, triangleStart + 20 })
        {
            byte[] corrupt = frozen.ToArray(); corrupt[at] ^= 0x80;
            Reject(() => NativeId65AuthoredSurfaceLayout.DecodeAndValidate(corrupt),
                "Independent culling/LP/HP/collision corruption cannot produce conflicting visual and physical floors.");
        }
        for (int i = 0; i < frozen.Length; i++)
        {
            byte[] corrupt = frozen.ToArray(); corrupt[i] ^= 1;
            Reject(() => wide.RequireExactBundle(corrupt), "Every wider owned byte, including protected complement, is bound.");
        }
        byte[] copy = wide.CopyBundle(); copy[12] ^= 1;
        byte[] environmentCopy = wide.CopyComponent(NativeId65SurfaceComponentKind.Environment); environmentCopy[12] ^= 1;
        check(wide.CopyBundle().SequenceEqual(frozen), "Wider surface and component copies are detached.");
        Console.WriteLine("NATIVE_ID65_WIDE_SINGLE_SURFACE_FREE_OK maxExtent=3072 schema4MaxExtent=2048 nativeExecution=false exportApproval=false");

        void LiteralWords(NativeId65AuthoredSurfaceBundle value, uint center, uint culling,
            uint[] vertices, uint[] triangles)
        {
            byte[] environment = value.CopyComponent(NativeId65SurfaceComponentKind.Environment);
            check(U(environment, 12) == center && U(environment, 16) == culling &&
                U(environment, 20) == 0x02100210 && U(environment, 24) == 0x02200000,
                "Independent literal culling and origin words preserve native units and packed axes.");
            for (int i = 0; i < vertices.Length; i++)
                check(U(environment, 40 + 4 * i) == vertices[i] && U(environment, 88 + 4 * i) == vertices[i],
                    "LP and HP each match the independently specified literal wider corners.");
            byte[] collision = value.CopyComponent(NativeId65SurfaceComponentKind.Collision);
            int start = 4 + (int)U(collision, 20);
            for (int i = 0; i < triangles.Length; i++)
                check(U(collision, start + 4 * i) == triangles[i],
                    "Collision fields preserve exact positive and negative signed9 deltas without masking overflow.");
        }

        void Geometry(NativeId65AuthoredSurfaceBundle value, NativeId65AuthoredFloor floor)
        {
            byte[] bytes = value.CopyBundle();
            check(NativeId65AuthoredSurfaceLayout.DecodeAndValidate(bytes) == floor,
                "Strict whole-surface readback retains the exact wider rectangle.");
            var expected = new[] { (floor.X, floor.Y, floor.Z), (floor.X + floor.Width, floor.Y, floor.Z),
                (floor.X, floor.Y + floor.Depth, floor.Z), (floor.X + floor.Width, floor.Y + floor.Depth, floor.Z) };
            for (int i = 0; i < 4; i++)
                foreach (int table in new[] { 40, 88 })
                {
                    uint raw = U(bytes, table + 4 * i);
                    var point = (floor.X + (int)(raw >> 21) * 16,
                        floor.Y + (int)((raw >> 10) & 2047) * 16, floor.Z + (int)(raw & 1023) * 16);
                    check(point == expected[i], "Independent LP/HP unpacking resolves actual world corners.");
                }
            byte[] collision = value.CopyComponent(NativeId65SurfaceComponentKind.Collision);
            int start = 4 + (int)U(collision, 20);
            var first = Triangle(collision, start); var second = Triangle(collision, start + 12);
            check(first.SequenceEqual(new[] { expected[0], expected[2], expected[1] }) &&
                second.SequenceEqual(new[] { expected[1], expected[2], expected[3] }),
                "Signed9 collision unpacking matches all visible corners in native upward winding.");
            check(Hit(floor.X + floor.Width / 2, floor.Y + floor.Depth / 2, floor.Z, first, second) &&
                !Hit(floor.X - 1, floor.Y, floor.Z, first, second) &&
                !Hit(floor.X + floor.Width + 1, floor.Y + floor.Depth, floor.Z, first, second) &&
                !Hit(floor.X, floor.Y - 1, floor.Z, first, second) &&
                !Hit(floor.X + floor.Width, floor.Y + floor.Depth + 1, floor.Z, first, second) &&
                !Hit(floor.X, floor.Y, floor.Z + 16, first, second),
                "Software finite-triangle test admits actual support only; coarse cell membership is not an infinite floor.");
            int blocks = 4 + (int)U(collision, 16);
            var index = NativeCollisionIndexCodec.Decode(collision.AsSpan(32, blocks - 32),
                collision.AsSpan(blocks, start - blocks), 6, 2);
            check(index.Cells.Count == 1 && index.Cells.TryGetValue(new(floor.X >> 12, floor.Y >> 12, floor.Z >> 12), out var cell) &&
                cell.OrderedTriangleIndexes.SequenceEqual(new[] { 1, 0 }),
                "Both actual triangles remain reachable in exactly one original native Z/Y/X cell.");
        }

        void Complement(NativeId65AuthoredSurfaceBundle before, NativeId65AuthoredSurfaceBundle after)
        {
            byte[] original = before.CopyBundle(), bytes = after.CopyBundle();
            int c = CollisionStart(after), t = c + 4 + (int)U(bytes, c + 20);
            // Only center/radius, LP/HP vertices and two collision triangles may
            // differ. In particular source-free topology, owners and guards do not.
            bool[] geometry = new bool[original.Length];
            foreach (var (at, length) in new[] { (12, 8), (40, 16), (88, 16), (t, 24) })
                Array.Fill(geometry, true, at, length);
            check(bytes.Length == original.Length && Enumerable.Range(0, bytes.Length)
                .Where(i => !geometry[i]).All(i => bytes[i] == original[i]),
                "Complete nongeometry complement is byte-for-byte unchanged, not just selected headers.");
            check(before.Owners.Select(o => (o.Id, o.BlockOffset, o.ByteLength)).SequenceEqual(
                after.Owners.Select(o => (o.Id, o.BlockOffset, o.ByteLength))) && before.References.SequenceEqual(after.References),
                "All physical owner extents and native pointer/index bindings remain the same.");
            foreach (var kind in new[] { NativeId65SurfaceComponentKind.Visibility,
                NativeId65SurfaceComponentKind.SpecialSurfaces, NativeId65SurfaceComponentKind.Cyclorama })
                check(before.CopyComponent(kind).SequenceEqual(after.CopyComponent(kind)), "Complete retained component equality: " + kind);
            check(after.CopyComponent(NativeId65SurfaceComponentKind.Visibility).SequenceEqual(new byte[]
            { 32,0,0,0, 16,0,0,0, 1,0,0,0, 12,0,0,0, 0,255,255,255, 1,0,0,0, 8,0,0,0, 255,255,255,255 }),
                "Literal visibility still enumerates only environment0 and no sky parts.");
        }

        void Reject(Action action, string message)
        {
            try { action(); }
            catch (InvalidDataException) { check(true, message); return; }
            throw new InvalidOperationException("Expected exact InvalidDataException: " + message);
        }
    }

    private static NativeId65FreshDocumentIntent Document(int width, int depth) =>
        NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, width, depth,
            8960, 8960, 64, FloorColor, SkyColor, 19);
    private static int CollisionStart(NativeId65AuthoredSurfaceBundle surface) =>
        surface.Components.Single(c => c.Kind == NativeId65SurfaceComponentKind.Collision).BlockOffset;
    private static uint U(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static int Signed9(uint raw) => ((int)raw & 511) is var value && value >= 256 ? value - 512 : value;
    private static (int X, int Y, int Z)[] Triangle(byte[] bytes, int at)
    {
        uint x = U(bytes, at), y = U(bytes, at + 4), z = U(bytes, at + 8);
        int ox = (int)(x & 16383) * 16, oy = (int)(y & 16383) * 16, oz = (int)(z & 16383) * 16;
        return [(ox, oy, oz), (ox + Signed9(x >> 14) * 16, oy + Signed9(y >> 14) * 16, oz + (int)((z >> 16) & 255) * 16),
            (ox + Signed9(x >> 23) * 16, oy + Signed9(y >> 23) * 16, oz + (int)(z >> 24) * 16)];
    }
    private static bool Hit(int x, int y, int z, params (int X, int Y, int Z)[][] triangles) => triangles.Any(points =>
        points.All(p => p.Z == z) && Enumerable.Range(0, 3).All(i =>
        {
            var a = points[i]; var b = points[(i + 1) % 3];
            return (long)(b.X - a.X) * (y - a.Y) - (long)(b.Y - a.Y) * (x - a.X) <= 0;
        }));
}
