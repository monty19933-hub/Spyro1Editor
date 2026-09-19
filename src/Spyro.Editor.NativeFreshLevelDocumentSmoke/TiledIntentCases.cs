using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Exporting;
using System.Collections;
using System.Reflection;
using static Cases;

internal static class TiledIntentCases
{
    internal const string TileId = "00000000000000000000000000000100";
    internal static NativeFreshFloorMaterialsIntent Colors() => NativeFreshFloorMaterialsIntent.Create(
        NativeFreshFloorMaterialIntent.BaseFloorColor(), NativeFreshFloorMaterialIntent.BaseFloorColor());
    internal static NativeFreshFlatTileIntent Tile(int index, int x, int y = 8448, int z = 8704) =>
        NativeFreshFlatTileIntent.Create((256 + index).ToString("x32"), x, y, z, 2048, 2048,
            new ColorRgba(80, 128, 112), Colors());

    internal static void Run()
    {
        foreach (var legacy in new[] { Document(Wizard()), MaterialCodecCases.Value(), MobyIntentCases.Value(true) })
        {
            string before = Serialize(legacy);
            var upgraded = legacy.UpgradeToTiled(TileId);
            Check(upgraded.SchemaVersion == 4 && upgraded.LegacyContent == null && upgraded.TiledContent != null &&
                upgraded.Content is NativeFreshTiledLevelContent && legacy.Content is NativeFreshLegacyLevelContent &&
                ReferenceEquals(legacy.LegacyContent!.Base, legacy.Base), "closed explicit legacy-versus-tiled content");
            Check(upgraded.TiledContent!.Tiles.Count == 1 && upgraded.TiledContent.Tiles[0].EditorId == TileId &&
                upgraded.SpawnX == legacy.Base.SpawnX && upgraded.SpawnY == legacy.Base.SpawnY &&
                upgraded.SpawnZ == legacy.Base.SpawnZ && upgraded.Heading == legacy.Base.Heading &&
                upgraded.SkyColor == legacy.Base.SkyColor && upgraded.MusicTrackId == legacy.Base.MusicTrackId,
                "explicit upgrade retains actual floor, spawn and common settings");
            var tile = upgraded.TiledContent.Tiles[0];
            Check(tile.X == legacy.Base.FloorX && tile.Y == legacy.Base.FloorY && tile.Z == legacy.Base.FloorZ &&
                tile.Width == legacy.Base.FloorWidth && tile.Depth == legacy.Base.FloorDepth && tile.FloorColor == legacy.Base.FloorColor &&
                (legacy.FloorMaterials == null ? tile.Materials.Triangle0.Tag == "base-floor-color" && tile.Materials.Triangle1.Tag == "base-floor-color"
                    : ReferenceEquals(tile.Materials, legacy.FloorMaterials)),
                "upgrade maps every actual legacy geometry/color/material value without a placeholder Base");
            Check(upgraded.Placements.SequenceEqual(legacy.Placements) && Serialize(legacy) == before &&
                ReferenceEquals(upgraded.UpgradeForNativeMobys(), upgraded), "upgrade preserves old bytes and cannot downgrade schema4");
            Reject(() => _ = upgraded.Base, "tiled aggregate has no fabricated legacy Base");
            Check(!Serialize(upgraded).Contains("\"base\"") && !Serialize(upgraded).Contains("\"floorMaterials\"") &&
                Serialize(Parse(Serialize(upgraded))) == Serialize(upgraded), "schema4 owns no competing base/material pair and roundtrips");
        }

        var tiled = NativeFreshTiledLevelContent.Create(new[] { Tile(0, 8448), Tile(1, 10496), Tile(2, 12544) },
            13056, 8960, 9216, 64, new ColorRgba(128, 96, 64), 19);
        var document = NativeFreshLevelDocumentIntent.CreateTiled(tiled,
            new[] { Wizard(x: 13056, ex: 13568) });
        Check(document.SchemaVersion == 4 && document.SpawnX == 13056 &&
            tiled.ContainsInteriorXY(10496, 8960) && tiled.ContainsInteriorXY(12544, 8960) &&
            !tiled.ContainsInteriorXY(8448, 8960) && !tiled.ContainsInteriorXY(14592, 8960),
            "three native tiles admit internal seams and spawn/route beyond the original floor only");
        Check(document.WithPlacements([]).SchemaVersion == 4 &&
            ReferenceEquals(document.WithPlacements([]).TiledContent, tiled), "placement edits retain actual tiled content");
        Check(Serialize(Parse(Serialize(document))) == Serialize(document), "three-tile canonical document roundtrip");
        Check(tiled.WithSettings(10496, 8960, 9216, 255, new(1, 2, 3), 19).SpawnX == 10496,
            "actual spawn can stand exactly on a full internal same-plane seam");
        var generated = NativeFlatTileSurfaceLayout.Compile(tiled.Tiles.Select(t =>
            new NativeFlatSurfaceTile(t.X, t.Y, t.Z, t.Width, t.Depth, 0x708050)), 0x406080);
        Check(generated.Tiles.Count == 3 && generated.CollisionIndex.Cells.Keys.Select(c => c.X).Distinct().Count() == 2 &&
            generated.CollisionIndex.Cells.Values.SelectMany(c => c.OrderedTriangleIndexes).Distinct().Count() == 6,
            "document tiles are accepted by the actual shared producer with six reachable triangles across two cells");

        foreach (Type type in new[] { typeof(NativeFreshFlatTileIntent), typeof(NativeFreshTiledLevelContent), typeof(NativeFreshLegacyLevelContent) })
            Check(type.IsSealed && type.GetConstructors().Length == 0 && type.GetProperties().All(p => p.SetMethod == null),
                "new tile and content values have no unchecked constructor or mutable properties");
        Check(typeof(NativeFreshLevelContent).IsAbstract && typeof(NativeFreshLevelContent)
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).All(c => !c.IsFamily && !c.IsFamilyOrAssembly),
            "content union cannot be externally subclassed");
        foreach (Type type in new[] { typeof(NativeFreshFlatTileIntent), typeof(NativeFreshTiledLevelContent) })
            Check(type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).All(m => m.Name == "Create"),
                "new content factories expose no source capture or publication authority");
        var mutable = new List<NativeFreshFlatTileIntent>(tiled.Tiles);
        var owned = tiled.WithTiles(mutable); mutable.Clear();
        Check(owned.Tiles.Count == 3 && ReferenceEquals(owned.Tiles[0], tiled.Tiles[0]) &&
            owned.Tiles is IList<NativeFreshFlatTileIntent> { IsReadOnly: true }, "ordered immutable tiles retain values but not caller collection");
        Reject(() => document.WithContent(Base(), []), "schema4 cannot be downgraded through the legacy edit seam");
        Reject(() => document.WithFloorMaterials(Colors()), "schema4 has no competing top-level floor-material edit");
        Reject(() => Document().WithTiledContent(tiled), "legacy content requires an explicit upgrade");
        Reject(() => document.UpgradeToTiled(TileId), "already-tiled upgrade cannot silently ignore a supplied editor ID");
        var changed = tiled.WithSettings(8960, 8960, 9216, 13, new(1, 2, 3), 47);
        var changedDocument = document.WithTiledContent(changed);
        Check(ReferenceEquals(changedDocument.TiledContent, changed) && changedDocument.Heading == 13 &&
            changedDocument.SkyColor == new ColorRgba(1, 2, 3) && changedDocument.MusicTrackId == 47 &&
            document.SpawnX == 13056, "common settings have one active authority and edits preserve original content");
        var narrow = NativeFreshTiledLevelContent.Create([Tile(0, 8448)], 8960, 8960, 9216, 64, new(128, 96, 64), 19);
        string beforeFailure = Serialize(document);
        Reject(() => document.WithTiledContent(narrow), "surface changes revalidate all unchanged placements");
        Check(Serialize(document) == beforeFailure, "failed aggregate edit leaves every original byte unchanged");

        foreach (int height in new[] { 0, 1, 8704, 8909, 262128 })
            Check(document.WithPlacements([Wizard(x: 13056, ex: 13568, z: height)]).Placements.Count == 1,
                "tiled Wizard containment remains XY-only, without an inferred height policy");
        var moby = MobyIntentCases.Moby(x: 12544, y: 8961, z: 0);
        Check(document.WithPlacements([moby]).SchemaVersion == 4 &&
            document.WithPlacements([moby]).Placements[0] == moby, "native-Moby XY accepts internal seams without ground inference");
        foreach (var point in new[] { (8448, 8960), (14592, 8960), (8960, 8448), (8960, 10496) })
            Reject(() => document.WithPlacements([MobyIntentCases.Moby(x: point.Item1, y: point.Item2)]),
                "tiled placements refuse each outer edge");
        Reject(() => document.WithPlacements([null!]), "null tiled placement");
        Reject(() => document.WithPlacements([moby, moby]), "duplicate tiled placement IDs");
        Check(document.WithPlacements(Enumerable.Range(1, 128).Select(i =>
            MobyIntentCases.Moby(i.ToString("x32"), x: 12544))).Placements.Count == 128,
            "schema4 retains the existing128-placement authoring budget");
        Reject(() => document.WithPlacements(Enumerable.Range(1, 129).Select(i =>
            MobyIntentCases.Moby(i.ToString("x32"), x: 12544))), "schema4 cannot bypass placement enumeration cap");

        UnionCases(narrow, document);
        BoundsCases(narrow, tiled);
    }

    private static void UnionCases(NativeFreshTiledLevelContent narrow, NativeFreshLevelDocumentIntent document)
    {
        var four = narrow.WithTiles([Tile(0, 8448), Tile(1, 10496), Tile(2, 8448, 10496), Tile(3, 10496, 10496)]);
        Check(four.ContainsInteriorXY(10496, 10496), "four-way seam intersection is interior to actual union");
        var corner = narrow.WithTiles([Tile(0, 8448), Tile(1, 10496, 10496)]);
        Check(!corner.ContainsInteriorXY(10496, 10496), "corner-only tile contact is not interior");
        var junction = narrow.WithTiles([Tile(0, 8448), SizedTile(1, 8448, 10496, 1024, 2048), SizedTile(2, 9472, 10496, 1024, 2048)]);
        Check(junction.ContainsInteriorXY(9472, 10496), "fully covered T junction is interior");
        var ringTiles = Enumerable.Range(0, 9).Where(i => i != 4).Select(i =>
            SizedTile(i, 8448 + i % 3 * 512, 8448 + i / 3 * 512, 512, 512)).ToArray();
        var ring = NativeFreshTiledLevelContent.Create(ringTiles, 8704, 8704, 9216, 64, new(128, 96, 64), 19);
        Check(!ring.ContainsInteriorXY(9216, 9216) && !ring.ContainsInteriorXY(8960, 9216) &&
            ring.ContainsInteriorXY(8960, 8704), "union rejects holes and their boundary while admitting covered seams");
        Reject(() => NativeFreshLevelDocumentIntent.CreateTiled(ring, [MobyIntentCases.Moby(x: 9216, y: 9216)]), "placement cannot use union bounding-box hole");
        Check(NativeFreshLevelDocumentIntent.CreateTiled(ring, [Wizard(x: 8704, y: 9216, ex: 9728, ey: 9216)]).Placements.Count == 1,
            "route admission validates endpoints only and does not invent corridor coverage across a hole");
        var elevations = narrow.WithTiles([Tile(0, 8448), Tile(1, 10496, z: 8720)]);
        Check(elevations.ContainsInteriorXY(10496, 8960), "neutral placement XY union does not infer tile elevation");
        Reject(() => elevations.WithSettings(10496, 8960, 9216, 64, elevations.SkyColor, 19),
            "spawn seam must be fully covered by tiles at its actual matching plane");
        Check(elevations.WithSettings(11008, 8960, 9232, 64, elevations.SkyColor, 19).SpawnZ == 9232,
            "spawn height follows matching tile plane, not first tile or fabricated base");
        Check(narrow.WithTiles([Tile(0, 8448), Tile(1, 8448, z: 8720)]).Tiles.Count == 2,
            "overlapping XY at different planes is supported neutral geometry");
        Reject(() => narrow.WithTiles([Tile(0, 8448), Tile(1, 10000)]), "shared producer rejects overlapping coplanar interiors");
        Reject(() => document.WithTiledContent(ring), "whole-document replacement rechecks outside placements against actual union");
    }

    private static void BoundsCases(NativeFreshTiledLevelContent narrow, NativeFreshTiledLevelContent tiled)
    {
        foreach (string? id in new[] { null, "", new string('0', 32), TileId[..31], Id.ToUpperInvariant() })
            Reject(() => NativeFreshFlatTileIntent.Create(id!, 8448, 8448, 8704, 2048, 2048, new(1, 2, 3), Colors()), "tile stable-ID grammar");
        foreach (var geometry in new[] { (-16, 8448, 8704, 16, 16), (8449, 8448, 8704, 16, 16),
            (8448, 8448, 8705, 16, 16), (8448, 8448, 8704, 0, 16), (8448, 8448, 8704, 2064, 16),
            (8448, 8448, 8704, 16, 17), (262128, 8448, 8704, 16, 16) })
            Reject(() => NativeFreshFlatTileIntent.Create(TileId, geometry.Item1, geometry.Item2, geometry.Item3,
                geometry.Item4, geometry.Item5, new(1, 2, 3), Colors()), "tile uses shared packed native geometry limits");
        Reject(() => NativeFreshFlatTileIntent.Create(TileId, 8448, 8448, 8704, 16, 16, new(1, 2, 3, 254), Colors()), "nonopaque tile color");
        Reject(() => NativeFreshFlatTileIntent.Create(TileId, 8448, 8448, 8704, 16, 16, new(1, 2, 3), null!), "missing tile material pair");
        Reject(() => narrow.WithTiles([]), "empty tiled surface");
        Reject(() => narrow.WithTiles(null!), "null tile collection");
        Reject(() => narrow.WithTiles([null!]), "null tile");
        Reject(() => narrow.WithTiles([Tile(0, 8448), Tile(0, 10496)]), "tile IDs unique independently of geometry");
        foreach (var spawn in new[] { (-1, 8960, 9216, 64), (262144, 8960, 9216, 64), (8960, -1, 9216, 64),
            (8960, 8960, -1, 64), (8960, 8960, 262144, 64), (8960, 8960, 9216, -1), (8960, 8960, 9216, 256),
            (8960, 8960, 9215, 64), (8960, 8960, 9217, 64), (8448, 8960, 9216, 64), (10496, 8960, 9216, 64) })
            Reject(() => narrow.WithSettings(spawn.Item1, spawn.Item2, spawn.Item3, spawn.Item4, narrow.SkyColor, 19),
                "actual spawn bounds, heading, exact plane offset and union interior");
        Reject(() => narrow.WithSettings(8960, 8960, 9216, 64, new(1, 2, 3, 1), 19), "nonopaque sky");
        Reject(() => narrow.WithSettings(8960, 8960, 9216, 64, narrow.SkyColor, -1), "unknown music track");
        foreach (int count in new[] { 0, -1, int.MaxValue })
        {
            var input = new ReportedTiles(tiled.Tiles, count, 4);
            Check(tiled.WithTiles(input).Tiles.Count == 3 && input.Steps == 3 && input.Starts == 1 && input.Disposed,
                "tile capture enumerates once without trusting Count and disposes input");
        }
        var small = Enumerable.Range(0, 256).Select(i => SizedTile(i, 8448 + i * 16, 8448, 16, 16)).ToArray();
        var maximum = NativeFreshTiledLevelContent.Create(small.Take(255), 8449, 8449, 9216, 64, narrow.SkyColor, 19);
        Check(maximum.Tiles.Count == NativeFreshTiledLevelContent.MaximumTiles, "actual255 ordered native tile limit");
        var overflow = new ReportedTiles(small, 0, 256);
        Reject(() => maximum.WithTiles(overflow), "256th native tile refused without unbounded materialization");
        Check(overflow.Steps == 256 && overflow.Disposed && maximum.Tiles.Count == 255,
            "tile count refusal stops and disposes at independent limit without changing prior content");
        var reversed = tiled.WithTiles(tiled.Tiles.Reverse());
        Check(reversed.Tiles.Select(t => t.EditorId).SequenceEqual(tiled.Tiles.Reverse().Select(t => t.EditorId)),
            "tile order and IDs are authored identities, never sorted or regenerated");
    }

    internal static NativeFreshFlatTileIntent SizedTile(int index, int x, int y, int width, int depth) =>
        NativeFreshFlatTileIntent.Create((256 + index).ToString("x32"), x, y, 8704, width, depth, new(80, 128, 112), Colors());

    private sealed class ReportedTiles(IEnumerable<NativeFreshFlatTileIntent> values, int count, int maximumSteps)
        : IReadOnlyCollection<NativeFreshFlatTileIntent>
    {
        public int Count => count;
        internal int Steps, Starts;
        internal bool Disposed;
        public IEnumerator<NativeFreshFlatTileIntent> GetEnumerator()
        {
            if (++Starts != 1) throw new InvalidOperationException("Caller tiles enumerated twice.");
            try
            {
                foreach (var value in values)
                {
                    if (++Steps > maximumSteps) throw new InvalidOperationException("Tile enumeration exceeded guard.");
                    yield return value;
                }
            }
            finally { Disposed = true; }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
