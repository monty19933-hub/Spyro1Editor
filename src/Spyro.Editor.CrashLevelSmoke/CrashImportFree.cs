using System.Buffers.Binary;
using Spyro.Editor.Core.Crash;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;

/// <summary>Disc-free checks of the Crash Bandicoot: Warped scenery/zone decoders and the level importer, on a synthetic
/// level built here byte by byte in the release's formats: a 6,400-unit square zone whose collision octree (8 x 8 columns)
/// walls in a 6x6 walkable middle with one pit; a floor of quads, a wall, a triangle pair and a see-through card; Crash's
/// start.</summary>
static class CrashImportFree
{
    const uint PageId = 0x0C8A64F3;   // any EID; the page dictionary is keyed by its name
    const uint SceneryId = 0x0C8A6413, ZoneId = 0x0C8A6417;

    public static async Task<int> RunAsync()
    {
        int checks = 0;
        void Check(bool ok, string what) { checks++; if (!ok) throw new Exception("check failed: " + what); }

        // ---- the texture page: two 32x32 4-bpp regions; palette at (0, 100): colour 0 transparent, 1.. a ramp ----
        byte[] page = new byte[0x10000];
        void Half(int x, int y, ushort v) => BinaryPrimitives.WriteUInt16LittleEndian(page.AsSpan(y * 512 + x * 2), v);
        for (int i = 1; i < 16; i++) Half(i, 100, (ushort)(i * 2 | (i * 2) << 5 | 8 << 10));
        for (int v = 0; v < 32; v++)
            for (int u = 0; u < 64; u += 4)
            {
                int Index(int uu) => uu < 32 ? 1 + (uu + v) % 15 : (uu - 32 < 8 && v < 32 ? 3 : 0);   // second region: 75% transparent
                Half(u / 4, v, (ushort)(Index(u) | Index(u + 1) << 4 | Index(u + 2) << 8 | Index(u + 3) << 12));
            }
        static byte[] TextureInfo(int u0, int u1, int v0, int v1, (int U, int V)[] corners)
        {
            byte[] b = new byte[12];
            b[0] = (byte)corners[0].U; b[1] = (byte)corners[0].V; b[4] = (byte)corners[1].U; b[5] = (byte)corners[1].V;
            b[8] = (byte)corners[2].U; b[9] = (byte)corners[2].V; b[10] = (byte)corners[3].U; b[11] = (byte)corners[3].V;
            b[2] = 0 | (100 & 3) << 6; b[3] = 100 >> 2;   // palette x 0, y 100
            b[6] = 0 | 3 << 5;                            // segment 0, opaque, 4 bpp
            b[7] = 0;                                     // page 0 of the scenery's list
            return b;
        }
        // Texture 1: whole region, corners in quad Z order TL, TR, BL, BR. Texture 2: the see-through region.
        var tex1 = TextureInfo(0, 31, 0, 31, [(0, 0), (31, 0), (0, 31), (31, 31)]);
        var tex2 = TextureInfo(32, 63, 0, 31, [(32, 0), (63, 0), (32, 31), (63, 31)]);
        // Triangle textures: halves TL-BL-BR... (corners 0-2 used) and TR-BL? : a triangle pair across the TR-BL diagonal.
        var tri1 = TextureInfo(0, 31, 0, 31, [(31, 0), (0, 0), (0, 31), (0, 31)]);    // TR, TL, BL
        var tri2 = TextureInfo(0, 31, 0, 31, [(31, 0), (0, 31), (31, 31), (31, 31)]); // TR, BL, BR

        // ---- scenery: vertices (Crash units, multiples of 16), faces ----
        List<(int X, int Y, int Z)> verts = [];
        int V(int x, int y, int z) { int k = verts.IndexOf((x, y, z)); if (k >= 0) return k; verts.Add((x, y, z)); return verts.Count - 1; }
        List<(int[] C, int Tex)> quads = [], tris = [];
        // Floor 8x8 quads of 800 at height 1008 (Z order: (x0,z0), (x1,z0), (x0,z1), (x1,z1)), the triangle pair replaces one.
        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 8; j++)
            {
                int x0 = i * 800, x1 = x0 + 800, z0 = j * 800, z1 = z0 + 800;
                if (i == 3 && j == 4) continue;
                quads.Add(([V(x0, 1008, z0), V(x1, 1008, z0), V(x0, 1008, z1), V(x1, 1008, z1)], 1));
            }
        // The triangle pair over cell (3, 4): TR=(3200, z 3200) TL=(2400,3200) BL=(2400,4000) BR=(3200,4000) (u grows with
        // x, v with z); shared edge TR-BL.
        int pTR = V(3200, 1008, 3200), pTL = V(2400, 1008, 3200), pBL = V(2400, 1008, 4000), pBR = V(3200, 1008, 4000);
        tris.Add(([pTR, pTL, pBL], 3)); tris.Add(([pTR, pBL, pBR], 4));
        // A wall on the floor (x 3200, z 1600..2000), 400 high; a see-through card beside it.
        quads.Add(([V(3200, 1008, 1600), V(3200, 1008, 2000), V(3200, 1408, 1600), V(3200, 1408, 2000)], 1));
        quads.Add(([V(2800, 1008, 2400), V(2800, 1008, 2608), V(2800, 1216, 2400), V(2800, 1216, 2608)], 2));
        // An untextured quad at the bottom of the pit (Crash's dark pit floors).
        quads.Add(([V(4000, 512, 4000), V(4800, 512, 4000), V(4000, 512, 4800), V(4800, 512, 4800)], 0));
        int nv = verts.Count, nt = tris.Count, nq = quads.Count;
        byte[] vertexItem = new byte[nv * 6];
        for (int i = 0; i < nv; i++)
        {
            var (x, y, z) = verts[i];
            BinaryPrimitives.WriteUInt32LittleEndian(vertexItem.AsSpan((nv - 1 - i) * 4), (uint)((x / 16) << 4 | (y / 16) << 4 << 16));
            BinaryPrimitives.WriteUInt16LittleEndian(vertexItem.AsSpan(nv * 4 + 2 * i), (ushort)((z / 16) << 4));   // colour index 0
        }
        byte[] triItem = new byte[nt * 6];
        for (int i = 0; i < nt; i++)
        {
            var (c, t) = tris[i];
            BinaryPrimitives.WriteUInt32LittleEndian(triItem.AsSpan((nt - 1 - i) * 4), (uint)(c[0] << 8 | c[1] << 20 | 0));
            BinaryPrimitives.WriteUInt16LittleEndian(triItem.AsSpan(nt * 4 + 2 * i), (ushort)(c[2] << 4 | t));
        }
        byte[] quadItem = new byte[nq * 8];
        for (int i = 0; i < nq; i++)
        {
            var (c, t) = quads[i];
            BinaryPrimitives.WriteUInt32LittleEndian(quadItem.AsSpan(8 * i), (uint)(c[0] << 8 | c[1] << 20));
            BinaryPrimitives.WriteUInt32LittleEndian(quadItem.AsSpan(8 * i + 4), (uint)(c[2] << 8 | c[3] << 20 | t));
        }
        byte[] header = new byte[76];
        int[] counts = [0, 0, 0, 0, nv, nt, nq, 4, 1, 0, 1];
        for (int k = 0; k < counts.Length; k++) BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4 * k), counts[k]);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(44), PageId);
        var scenery = CrashScenery.Read(new CrashEntry(CrashNsf.Name(SceneryId), CrashScenery.EntryType,
            [header, vertexItem, triItem, quadItem, [.. tex1, .. tex2, .. tri1, .. tri2], [128, 128, 128, 0], []]));
        Check(scenery.Vertices.Count == nv && scenery.Vertices[0] == new CrashPoint(verts[0].X, verts[0].Y, verts[0].Z) &&
              scenery.Vertices[^1] == new CrashPoint(verts[^1].X, verts[^1].Y, verts[^1].Z), "vertices: xy words read in reverse, z in order");
        Check(scenery.Faces.Count == nt + nq && scenery.Faces[0].Corners.SequenceEqual(tris[0].C) && scenery.Faces[0].Texture == 2 &&
              scenery.Faces[nt].Quad && scenery.Faces[nt].Corners.SequenceEqual(quads[0].C) && scenery.Faces[nt].Texture == 0, "triangles reversed, quads in order, 1-based textures");
        Check(scenery.Faces[nt].Outline.SequenceEqual(new[] { quads[0].C[0], quads[0].C[1], quads[0].C[3], quads[0].C[2] }), "quad outline 0, 1, 3, 2");
        Check(scenery.VertexColours.All(c => c == new ColorRgba(128, 128, 128)) && scenery.Pages[0] == CrashNsf.Name(PageId), "colours and page list");
        var image = CrashTextureImage.Read(page, scenery.Textures[1]);
        Check(image.Width == 32 && image.Height == 32 && Math.Abs(image.Transparent - 0.75) < 0.01, "see-through region decoded (colour 0 transparent)");

        // ---- the zone: octree depths x 3, y 1, z 3 over (0, -1000, 0) 6400 x 4000 x 6400: 8x8 columns of a lower and an
        // upper half. Border columns are walls (5), the middle 6x6 ground (3) with air above, column (5, 5) a pit. ----
        int Leaf(int ix, int iz, int layer)
        {
            if (ix == 0 || iz == 0 || ix == 7 || iz == 7) return 5;
            if (ix == 5 && iz == 5) return layer == 0 ? 6153 : 0;
            return layer == 0 ? 3 : 0;
        }
        List<ushort> nodes = [];   // 16-bit words from byte 0x24
        // A node over columns [x0, x0+n) x [z0, z0+n) of layers [y0, y0+m): a leaf value when all leaves agree, else the
        // byte offset of its children (x most significant, then y, then z; an axis splits while its depth lasts).
        int Node(int x0, int y0, int z0, int n, int m)
        {
            var values = (from ix in Enumerable.Range(x0, n) from iy in Enumerable.Range(y0, m) from iz in Enumerable.Range(z0, n) select Leaf(ix, iz, iy)).Distinct().ToList();
            if (values.Count == 1) return values[0];
            int nx2 = n > 1 ? 2 : 1, ny2 = m > 1 ? 2 : 1;
            int at = nodes.Count; for (int k = 0; k < nx2 * ny2 * nx2; k++) nodes.Add(0);
            int c = 0;
            for (int bx = 0; bx < nx2; bx++)
                for (int by = 0; by < ny2; by++)
                    for (int bz = 0; bz < nx2; bz++, c++)
                        nodes[at + c] = (ushort)Node(x0 + bx * n / nx2, y0 + by * m / ny2, z0 + bz * n / nx2, n / nx2, m / ny2);
            return 0x24 + 2 * at;
        }
        int root = Node(0, 0, 0, 8, 2);
        byte[] tree = new byte[0x24 + 2 * nodes.Count + 2];
        void Box(int at, int v) => BinaryPrimitives.WriteInt32LittleEndian(tree.AsSpan(at), v);
        Box(0, 0); Box(4, -1000); Box(8, 0); Box(12, 6400); Box(16, 4000); Box(20, 6400);
        void S(int at, int v) => BinaryPrimitives.WriteUInt16LittleEndian(tree.AsSpan(at), (ushort)v);
        S(0x1C, root); S(0x1E, 3); S(0x20, 1); S(0x22, 3);
        for (int k = 0; k < nodes.Count; k++) S(0x24 + 2 * k, nodes[k]);
        var collision = new CrashZoneCollision(tree);
        var leaves = collision.Leaves().ToList();
        double wallVolume = leaves.Where(l => l.Value == 5).Sum(l => l.W * l.H * l.D), cell = 800.0 * 800 * 2000;
        Check(Math.Abs(wallVolume - 28 * 2 * cell) < 1 && leaves.Where(l => l.IsGround).Sum(l => l.W * l.D) == 35 * 800.0 * 800 && leaves.Count(l => l.IsDeadly) == 1,
            "octree leaves: 28 wall columns, 35 ground cells, one pit (empty space is no leaf)");
        var column = collision.Column(1200, 1200);
        Check(column.Count == 1 && column[0].IsGround && column[0].Top == 1000 && column[0].H == 2000, "column through a ground cell: x most significant, then y, then z");
        Check(collision.Column(4400, 4400).Single().IsDeadly && collision.Column(4400, 4400).Single().Flags == 6 && collision.Column(400, 4400).All(l => l.Value == 5),
            "pit leaf (kind 4, flags 6) and a wall column");
        byte[] entity = new byte[92];
        BinaryPrimitives.WriteInt32LittleEndian(entity, 92); BinaryPrimitives.WriteInt32LittleEndian(entity.AsSpan(12), 4);
        (int Id, int At)[] props = [(0x2C, 48), (0x4B, 64), (0xA9, 76), (0xAA, 84)];
        for (int i = 0; i < 4; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(entity.AsSpan(16 + 8 * i), (ushort)props[i].Id);
            BinaryPrimitives.WriteUInt16LittleEndian(entity.AsSpan(18 + 8 * i), (ushort)(props[i].At - 12));
        }
        System.Text.Encoding.ASCII.GetBytes("obj_willy").CopyTo(entity, 52);
        BinaryPrimitives.WriteInt32LittleEndian(entity.AsSpan(64), 1);
        BinaryPrimitives.WriteInt16LittleEndian(entity.AsSpan(68), 2000); BinaryPrimitives.WriteInt16LittleEndian(entity.AsSpan(70), 2008); BinaryPrimitives.WriteInt16LittleEndian(entity.AsSpan(72), 2000);
        BinaryPrimitives.WriteInt32LittleEndian(entity.AsSpan(80), 0);
        byte[] zoneHead = new byte[4 + 48];
        BinaryPrimitives.WriteInt32LittleEndian(zoneHead, 1); BinaryPrimitives.WriteUInt32LittleEndian(zoneHead.AsSpan(4), SceneryId);
        var zone = CrashZone.Read(new CrashEntry(CrashNsf.Name(ZoneId), CrashZone.EntryType, [zoneHead, tree, entity]));
        Check(zone.Scenery.Single() == scenery.Name && zone.Entities.Single() is { Name: "obj_willy", IsPlayerStart: true } e &&
              e.Positions.Single() == new CrashPoint(2000, 1008, 2000), "zone scenery list and Crash's start (positions relative to the zone)");
        var data = new CrashLevelData(new Dictionary<string, CrashScenery> { [scenery.Name] = scenery }, new Dictionary<string, CrashZone> { [zone.Name] = zone },
            new Dictionary<string, byte[]> { [CrashNsf.Name(PageId)] = page });
        Check(data.Main.Single() == scenery.Name && data.MainZones.Single() == zone && data.PlayerStart == new CrashPoint(2000, 1008, 2000), "main area, zones, start");

        // ---- the importer ----
        string pictures = Path.Combine(Path.GetTempPath(), "crash-import-free-" + Guid.NewGuid().ToString("N"));
        try
        {
            var level = new CrashLevel("Test Field", CrashLevelKind.Level, "S0/TEST.NSF", new string('0', 64), "synthetic");
            var carriers = Enumerable.Range(0, 8).Select(i => new CrashLevelPictures.Carrier(28, i, 4096)).ToList();
            var binding = new SceneTargetBinding("icecavern", 23, 28, 27, Spyro.Editor.Core.Universal.UniversalSourceLibrary.OriginalUsaSha256, 228);
            var result = await CrashLevelImport.BuildAsync(data, level, binding, carriers, null,
                new CrashLevelImportOptions { PictureFolder = pictures, CheckTextureMemory = false }, killHeight: 1000);
            var doc = result.Scene; var report = result.Report;
            Check(report.MergedPairs == 1 && doc.Surfaces.Count == 63 + 1 + 1 + 1 && report.LeftOut.GetValueOrDefault("see-through decorations (grass tufts, bunting)") == 1,
                "63 floor quads, the triangle pair joined into one quad, the wall and the untextured pit bottom kept, the see-through card left out");
            Check(report.Sectors == doc.Surfaces.Select(s => s.Sector).Distinct().Count() && report.Sectors == report.NativeSectors && report.Sectors >= 2,
                "sectors at most 640 units across, each compiled as one native sector");
            Check(doc.Surfaces.Min(s => s.Vertices.Min(v => v.Z)) >= 1000 + CrashLevelImport.KillHeightMargin && report.Lift > 0, "raised above the kill height");
            // Floors face up (anticlockwise from above, not reversed); the wall shows the side with lower ground (both sides
            // are the same floor here, so it faces the path); two-sided only for see-through faces.
            static Vector3f N(Vector3f[] v) { var u = new Vector3f(v[1].X - v[0].X, v[1].Y - v[0].Y, v[1].Z - v[0].Z); var w = new Vector3f(v[2].X - v[0].X, v[2].Y - v[0].Y, v[2].Z - v[0].Z); return new(u.Y * w.Z - u.Z * w.Y, u.Z * w.X - u.X * w.Z, u.X * w.Y - u.Y * w.X); }
            var floors = doc.Surfaces.Where(s => s.Vertices.Select(v => v.Z).Distinct().Count() == 1).ToList();
            Check(floors.Count == 65 && floors.All(s => (N(s.Vertices).Z > 0) != s.FrontReversed), "every floor shows its top");
            static bool Steep(AuthoredSurface s) { var n = N(s.Vertices); return Math.Abs(n.Z) < 0.5 * Math.Sqrt(n.X * n.X + n.Y * n.Y + n.Z * n.Z); }
            Console.WriteLine($"  floors two-sided: {floors.Count(s => s.TwoSided)} of {floors.Count}; steep {doc.Surfaces.Count(Steep)}, two-sided {doc.Surfaces.Where(Steep).Count(s => s.TwoSided)}");
            Check(doc.Surfaces.Where(Steep).All(s => s.TwoSided) && floors.Count(s => s.TwoSided) <= 2 && doc.FarMeshes!.All(m => m.TwoSided),
                "walls drawn from both sides, as Crash draws them (and the distant meshes); floors one-sided, looking up (the draw buffer)");
            // Quad corners show the picture's TR, TL, BL, BR: Crash's u grows with x and v with z, and editor y = -z, so
            // TR is the corner with the largest x and y.
            var floorQuad = floors.First(s => s.Vertices.Length == 4);
            var q = floorQuad.Vertices;
            Check(q[0].X > q[1].X && q[0].Y == q[1].Y && q[2].Y < q[1].Y && q[3].X > q[2].X || report.Turn != 0, "quad corner order TR, TL, BL, BR");
            // Walls: around the walkable middle (800..5600 Crash units, 24 cell sides); a deadly floor over the pit.
            // Collision-only faces: the walls first, then pit floors, catch floors and skirts.
            var walls = doc.CollisionFaces!.Take(report.WallFaces).ToList();
            var pits = doc.CollisionFaces!.Where(c => c.Kind == AuthoredSurfaceKind.DeadlyWater).ToList();
            Check(walls.Count >= 4 && walls.All(c => c.Kind == null && c.Vertices.Select(v => (v.X, v.Y)).Distinct().Count() == 2) && pits.Count == 1,
                "invisible walls (upright) and one deadly pit floor");
            Check(report.Skirts > 0 && doc.CollisionFaces!.Count(c => c.Kind == null && c.Vertices.Select(v => (v.X, v.Y)).Distinct().Count() == 2) == report.WallFaces + report.Skirts,
                "skirts under the open edges of the raised floors (the wall-top card left out, the triangle pair's quad)");
            double wallLength = report.WallLength, side = 800 * CrashLevelImportOptions.CrateScale;
            Check(Math.Abs(wallLength - 24 * side) < side * 0.6, $"walls run around the walkable middle (24 cell sides): {wallLength:0} vs {24 * side:0}");
            Check(walls.All(w => w.Vertices.Max(v => v.Z) - w.Vertices.Min(v => v.Z) <= 250 && Math.Max(Math.Abs(w.Vertices[0].X - w.Vertices[1].X), Math.Abs(w.Vertices[0].Y - w.Vertices[1].Y)) <= 250),
                "wall pieces within one collision piece (they stay upright)");
            // Each wall is solid from the walkable side: seen from the walkable middle its corners turn anticlockwise.
            var middle = Centre(doc.Surfaces.Where(s => s.Vertices.Length == 4).SelectMany(s => s.Vertices));
            Check(walls.All(w => { var n = N(w.Vertices); var c = Centre(w.Vertices); return n.X * (middle.X - c.X) + n.Y * (middle.Y - c.Y) > 0; }), "walls face the walkable ground");
            var pit = pits[0].Vertices;
            Check(pit.Max(v => v.Z) < floors.Max(s => s.Vertices[0].Z) && Centre(pit) is var pc && Math.Abs(pc.X - Centre(floors.SelectMany(s => s.Vertices)).X) < 2000, "pit floor below the ground");
            Check(Math.Abs(doc.Spawn.X - result.Report.Route.First().X) < 20 && doc.Spawn.Z > floors.Max(s => s.Vertices[0].Z), "start at Crash's start, above the floor");
            Check(report.KeptPictures == 2 && report.Pictures == 2 && result.Materials.All(m => m.Image is { } path && File.Exists(path)), "two pictures (the card's was left out), written");
            var plainFace = doc.Surfaces.Single(s => s.Vertices.All(v => v.Z == doc.Surfaces.Min(f => f.Vertices.Min(w => w.Z))));
            var plainPng = PngRgbaImage.ReadRgba(plainFace.Material!.Image!, out int pw, out int ph);
            Check(pw == 64 && ph == 64 && plainPng.All(p => p == new Rgba32(128, 128, 128, 255)), "the untextured face's picture is plain 128 grey (shows its vertex colours)");
            var png = PngRgbaImage.ReadRgba(doc.Surfaces[0].Material!.Image!, out pw, out ph);
            Check(pw == 64 && ph == 64 && png.All(p => p.A == 255) && png.Distinct().Count() > 4, "the textured picture is 64x64 and opaque");
            Check(result.ApplyTo(SceneDocument.New("BLANK") with { TargetBinding = binding }) is var applied && applied.CollisionFaces == doc.CollisionFaces &&
                  applied.Surfaces == doc.Surfaces && applied.Name == "TEST FIELD", "ApplyTo keeps the walls, faces and name");
            // The compiled level keeps the walls and the pit's deadly record; a face without collision adds none.
            var withoutCollision = doc with { Surfaces = [.. doc.Surfaces.Select((s, i) => i == 0 ? s with { NoCollision = true } : s)] };
            var materials = result.Materials;
            int Triangles(SceneDocument d) { byte[] b = NativeAuthoredMesh.Compile(d, materials); int at = 0; for (int i = 0; i < 3; i++) at += BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at)); return BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at + 4)); }
            int all = Triangles(doc), fewer = Triangles(withoutCollision), noWalls = Triangles(doc with { CollisionFaces = null });
            Check(fewer == all - 2 && noWalls < all - walls.Count, $"collision: a face without collision drops its 2 triangles ({all} -> {fewer}), the walls add theirs ({noWalls})");
            Check(doc.GroundKinds().Contains(AuthoredSurfaceKind.DeadlyWater), "the pit's water reaches the liquid splash check");
            // Near colours modulate the texture near and far (the face pass's first near-tier table is filled from the far
            // colours), so they are equal; every sector draws its own untextured distant mesh, kept by Derive.
            Check(doc.Surfaces.All(s => s.FarColors!.SequenceEqual(s.NearColors!)) && doc.FarMeshes!.Count == report.Sectors &&
                  doc.FarMeshes.All(m => m.Always && m.Vertices.Length <= 64) && doc.FarMeshes.All(m => m.Colors.Any(c => c != new ColorRgba(128, 128, 128))), "far colours = near colours; one always-drawn distant mesh per sector in the pictures' colour");
            _ = NativeAuthoredMesh.Compile(doc, result.Materials);
            Check(NativeAuthoredMeshRenderer.LastFarReport.Supplied == report.NativeSectors && NativeAuthoredMeshRenderer.LastFarReport.Simplified == 0, "every compiled sector draws its distant mesh");
            Check(NativeAuthoredFarColors.Derive(doc, _ => new ColorRgba(10, 20, 30)).Surfaces.SequenceEqual(doc.Surfaces), "Derive keeps the far colours of sectors with their own distant mesh");
            var json = System.Text.Json.JsonSerializer.Deserialize<SceneDocument>(doc.Serialize())!;
            Check(json.CollisionFaces!.Count == doc.CollisionFaces!.Count && json.CollisionFaces.Count(c => c.Kind != null) == 1 &&
                  System.Text.Json.JsonSerializer.Deserialize<SceneDocument>(withoutCollision.Serialize())!.Surfaces.Count(s => s.NoCollision) == 1, "JSON keeps walls, kinds and NoCollision");
            // A start whose floor is too narrow gets a landing pad; the doorway arrival check counts undrawn floors.
            Check(report.SpawnSource.Contains("Crash's start", StringComparison.Ordinal), "start source reported");
            NativeAuthoredSpawn.RequirePortalArrivalSupported(doc);
            checks++;
            // Options: no walls, no pits -> no collision faces and the pit is walled in instead of open.
            var bare = await CrashLevelImport.BuildAsync(data, level, binding, carriers, null,
                new CrashLevelImportOptions { PictureFolder = pictures, CheckTextureMemory = false, InvisibleWalls = false, DeadlyPits = false, DrawBothSides = false, Skirts = false, CatchFloors = false }, killHeight: 1000);
            Check(bare.Scene.Surfaces.Count(s => s.TwoSided) == 1 && bare.Scene.Surfaces.Single(s => s.TwoSided).Vertices.All(v => v.X >= 4000 * CrashLevelImportOptions.CrateScale) &&
                  bare.Scene.FarMeshes!.All(m => !m.TwoSided), "one-sided faces: only the floor over the pit bottom (an overhang) is two-sided");
            Check((bare.Scene.CollisionFaces ?? []).All(c => c.Kind == null && c.Vertices.Select(v => v.Z).Distinct().Count() == 1) && bare.Report.WallFaces == 0 &&
                  bare.Scene.Surfaces.All(s => !s.NoCollision), "without walls: no wall faces and every face keeps its collision");
            // Refusals: a zone range outside the path, a face budget below the zones' own faces.
            await Refused(() => CrashLevelImport.BuildAsync(data, level, binding, carriers, null, new CrashLevelImportOptions { PictureFolder = pictures, CheckTextureMemory = false, FirstZone = 1 }), "zone range");
            await Refused(() => CrashLevelImport.BuildAsync(data, level, binding, carriers, null, new CrashLevelImportOptions { PictureFolder = pictures, CheckTextureMemory = false, FaceBudget = 5 }), "face budget");
            checks += 2;
        }
        finally { if (Directory.Exists(pictures)) Directory.Delete(pictures, true); }

        // ---- the catalogue ----
        Check(CrashLevels.Find("toad village") == CrashLevels.All[0] && CrashLevels.Find("S0/S000000B.NSF") == CrashLevels.All[0] && CrashLevels.Find("Tiny Tiger").File == "S0/S0000006.NSF" &&
              CrashLevels.All.All(l => l.Sha256.Length == 64 && l.SpyroName.Length <= 19) && CrashLevels.All[0].SpyroName == "TOAD VILLAGE" &&
              CrashLevels.All.Select(l => l.File).Distinct().Count() == CrashLevels.All.Count, "catalogue: names, files, SHA-256s");
        Console.WriteLine($"CRASH_LEVEL_IMPORT_FREE_OK checks={checks}");
        return checks;
    }

    static Vector3f Centre(IEnumerable<Vector3f> points) { var p = points.ToList(); return new(p.Average(v => v.X), p.Average(v => v.Y), p.Average(v => v.Z)); }
    static async Task Refused(Func<Task> run, string what)
    {
        try { await run(); }
        catch (CrashLevelImportException) { return; }
        throw new Exception("check failed: refusal " + what);
    }
}

/// <summary>Source QA with the user's Crash Bandicoot: Warped disc (CRASH_DISC, or the workspace's configured one): every
/// catalogued level decodes to the face counts the Python prototype found (export_crash.py / analyze.py, 2026-10-10), and
/// Toad Village imports whole for Ice Cavern within the game's limits (no Spyro disc needed: carriers are stand-ins and
/// texture memory is not checked). Without a Crash disc it reports the skip.</summary>
static class CrashImportDisc
{
    // Main-area faces, zones along the path and vertex extent (x, y, z ranges) as analyze.py found them.
    static readonly Dictionary<string, (int MainFaces, int MainZones, int[] Extent)> Expected = new()
    {
        ["Toad Village"] = (9385, 27, [17392, 99600, 2000, 18160, 238432, 409200]),
        ["Gee Wiz"] = (13489, 37, [41392, 123648, 192, 16368, 160400, 400192]),
        ["Double Header"] = (13148, 36, [82800, 166192, 192, 16368, 158592, 391792]),
        ["Warp Room"] = (4480, 7, [-15888, 15680, -6592, 8400, -15968, 16800]),
        ["Dingodile's Arena"] = (660, 4, [-8048, 5808, -144, 4752, -6800, 6288]),
        ["N. Tropy's Arena"] = (3025, 8, [-6000, 6000, -10800, 50000, -8528, 8400]),
        ["Tiny Tiger's Arena"] = (891, 1, [-8256, 8240, -7600, 9600, -10800, 4000]),
        ["N. Cortex's Arena"] = (486, 1, [-4800, 4800, -5200, 4800, -6000, 3200]),
    };

    public static async Task RunAsync(string? workspace)
    {
        string disc = Environment.GetEnvironmentVariable("CRASH_DISC") is { Length: > 0 } d ? d
            : workspace != null ? CrashWarpedDiscLocator.Find(new Spyro.Editor.Core.Workspace.EditorWorkspace(workspace)) : "";
        if (disc.Length == 0 || !File.Exists(disc)) { Console.WriteLine("CRASH_LEVEL_IMPORT_DISC_OK skipped (no Crash Bandicoot: Warped disc: set CRASH_DISC)"); return; }
        int checks = 0;
        void Check(bool ok, string what) { checks++; if (!ok) throw new Exception("check failed: " + what); }
        var crash = new CrashWarpedDisc(disc);
        foreach (var level in CrashLevels.All)
        {
            var data = CrashLevels.Read(crash, level);
            int faces = data.MainFaces().Count();
            Console.WriteLine($"{level.Name}: {data.Scenery.Count} scenery, {data.Zones.Count} zones, main area {faces} faces in {data.Main.Count} scenery, {data.MainZones.Count} zones, start {data.PlayerStart}");
            var v = data.MainFaces().SelectMany(f => f.Face.Corners.Select(i => f.Scenery.Vertices[i])).ToList();
            int[] extent = [v.Min(p => p.X), v.Max(p => p.X), v.Min(p => p.Y), v.Max(p => p.Y), v.Min(p => p.Z), v.Max(p => p.Z)];
            Check(faces == Expected[level.Name].MainFaces && data.MainZones.Count == Expected[level.Name].MainZones && extent.SequenceEqual(Expected[level.Name].Extent),
                $"{level.Name} main area: faces, zones and extent ({string.Join(",", extent)})");
        }
        var toad = CrashLevels.Read(crash, CrashLevels.Find("Toad Village"));
        Check(toad.PlayerStart == new CrashPoint(48546, 3199, 407199) && toad.MainZones[0].Name == "00_bZ" && toad.MainZones[^1].Name == "26_bZ", "Toad Village start and zones");
        Check(toad.MainZones.All(z => z.Collision.Leaves().Any(l => l.Value == 5) && z.Collision.Leaves().Any(l => l.IsGround)), "every Toad Village zone has walls and ground");
        string pictures = Path.Combine(Path.GetTempPath(), "crash-import-disc-" + Guid.NewGuid().ToString("N"));
        try
        {
            var carriers = Enumerable.Range(0, 127).Select(i => new CrashLevelPictures.Carrier(28, i, 4096)).ToList();
            var binding = new Spyro.Editor.Core.Editing.SceneTargetBinding("icecavern", 23, 28, 27, Spyro.Editor.Core.Universal.UniversalSourceLibrary.OriginalUsaSha256, 228);
            var result = await CrashLevelImport.BuildAsync(toad, CrashLevels.Find("Toad Village"), binding, carriers, null,
                new CrashLevelImportOptions { PictureFolder = pictures, CheckTextureMemory = false }, killHeight: 1000);
            foreach (string line in result.Report.Lines()) Console.WriteLine("  " + line);
            var r = result.Report;
            Check(r.DrawnFaces <= 7000 && r.Faces > 6500 && r.Sectors <= 120 && r.NativeSectors == r.Sectors, "Toad Village fits the face budget and sectors");
            Check(r.TerrainBytes <= Spyro.Editor.Core.Exporting.NativeAuthoredMesh.MaxSurfaceBytes && r.CollisionTriangles is > 0 and <= 0x7FFF, "terrain data and collision within the game's limits");
            Check(r.WallFaces > 500 && r.PitFloors > 0 && r.PlayableCells > 50000 && r.RouteLength is > 18000 and < 24000, "walls, pits, walkable ground and Crash's way (about 21,000 units)");
            Check(r.Skirts > 300 && r.CatchFloors > 0 && r.NoCollisionFaces > 1000, "skirts, catch floors and collision-free backdrop");
            Check(r.Turn != 0 && r.Extent.X <= 16383 && r.Extent.Y <= 16383 && r.Scale == CrashLevelImportOptions.CrateScale, "whole level at crate scale, turned to fit");
            Check(r.SpawnSource.StartsWith("Crash's start", StringComparison.Ordinal), "starts at Crash's start");
        }
        finally { if (Directory.Exists(pictures)) Directory.Delete(pictures, true); }
        Console.WriteLine($"CRASH_LEVEL_IMPORT_DISC_OK checks={checks}");
    }
}
