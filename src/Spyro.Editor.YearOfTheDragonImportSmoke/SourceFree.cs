using System.Buffers.Binary;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Text;
using Spyro.Editor.Core.YearOfTheDragon;

/// <summary>The importer on a synthetic Year of the Dragon level (no disc): the level table's names, the WAD layout
/// decoder (textures, sectors with near and far tables, ground kinds, collision, objects), the face rules, ground
/// kinds, texture merging, sector grouping under the 127-sector limit, the start and the compile.</summary>
static class SourceFree
{
    static int checks;
    static void Check(bool ok, string what) { if (!ok) throw new InvalidOperationException("check failed: " + what); checks++; }

    public static int Run()
    {
        Levels();
        Names();
        var data = YearOfTheDragonLevelData.Parse(YearOfTheDragonLevels.All[1], Synthetic(sectors: 130));
        Decoder(data);
        Faces();
        Kinds();
        Import(data).GetAwaiter().GetResult();
        KillHeights();
        Console.WriteLine($"YOTD_IMPORT_FREE_OK checks={checks}");
        return 0;
    }

    /// <summary>Create BIN lowers a rebuilt level's kill height 256 under its lowest face, never under 64 and never above the
    /// slot's own (the thorough cases are NativeKillHeightSmoke's).</summary>
    static void KillHeights()
    {
        byte[] usa = new byte[NativeKillHeights.TableOffset + NativeKillHeights.Count * 4];
        for (int i = 0; i < NativeKillHeights.Count; i++)
            BinaryPrimitives.WriteInt32LittleEndian(usa.AsSpan(NativeKillHeights.TableOffset + 4 * i), NativeKillHeights.UsaWorldUnits[i]);
        Check(NativeKillHeights.Index(10) == 0 && NativeKillHeights.Index(23) == 9 && NativeKillHeights.Index(65) == 35, "table index (world - 1) x 6 + level");
        SceneDocument Floor(float z) => SceneDocument.New("T") with
        {
            Spawn = new(100, 100, z + 40),
            Surfaces = [new("f", [new(0, 0, z), new(200, 0, z), new(200, 200, z), new(0, 200, z)], new(128, 128, 128), null)],
        };
        byte[] exe = usa.ToArray();
        Check(NativeKillHeights.LowerFor(usa, exe, 23, Floor(652)) == 396 && NativeKillHeights.Read(exe, 23) == 396, "Molten Crater's floors (652) in Ice Cavern (1,000): lowered to 396");
        exe = usa.ToArray();
        Check(NativeKillHeights.LowerFor(usa, exe, 23, Floor(2000)) == null && NativeKillHeights.Read(exe, 23) == 1000, "never raised");
        Check(NativeKillHeights.LowerFor(usa, exe, 10, Floor(20)) == null && NativeKillHeights.Read(exe, 10) == 32 && NativeKillHeights.Minimum(usa, 23) == 64,
            "Artisans keeps its own 32; nothing goes under 64");
        bool refused = false;
        try { NativeKillHeights.Index(16); } catch (ArgumentOutOfRangeException) { refused = true; }
        Check(refused, "no kill height for a level that does not exist");
    }

    static void Levels()
    {
        var all = YearOfTheDragonLevels.All;
        Check(all.Count == 37 && all.Select((l, i) => l.Index == i && l.WadEntry == 97 + 2 * i).All(x => x), "37 levels, data entry 97 + 2 x index");
        Check(YearOfTheDragonLevels.Find("lost fleet") is { Index: 20, WadEntry: 137 } && YearOfTheDragonLevels.Find("LOSTFLEET").Index == 20 && YearOfTheDragonLevels.Find("20").Name == "Lost Fleet", "find by name or index");
        Check(YearOfTheDragonLevels.Find("Sunrise Spring").Kind == YearOfTheDragonLevelKind.Homeworld && YearOfTheDragonLevels.Find("Honey Speedway").Kind == YearOfTheDragonLevelKind.Speedway, "kinds");
        Check(all.Count(l => l.Kind == YearOfTheDragonLevelKind.Level) == 23 && all.Count(l => l.Kind == YearOfTheDragonLevelKind.Homeworld) == 4, "23 normal levels and 4 homeworlds");
        Check(YearOfTheDragonLevels.Find("Sgt. Byrd's Base").SpyroName == "SGT BYRD'S BASE" && YearOfTheDragonLevels.Find("Agent 9's Lab").SpyroName == "AGENT NINE'S LAB" &&
              YearOfTheDragonLevels.Find("Midnight Mountain Home").SpyroName == "MIDNIGHT MOUNTAIN", "Spyro the Dragon names");
        Check(all.All(l => TextTargetCatalog.IsSafeReplacement(l.SpyroName, 19)), "every name is a safe level name");
        bool refused = false;
        try { YearOfTheDragonLevels.Find("Peace Keepers"); } catch (ArgumentException) { refused = true; }
        Check(refused, "an unknown name is refused");
        Check(YearOfTheDragonLevelImport.Slug("Sgt. Byrd's Base") == "sgt-byrd-s-base", "picture folder slug");
    }

    static void Names()
    {
        byte[] exe = new byte[0x60000];
        int at = 0x10000;
        for (int i = 0; i < 37; i++)
        {
            byte[] name = System.Text.Encoding.Latin1.GetBytes(YearOfTheDragonLevels.All[i].Name);
            name.CopyTo(exe, at);
            BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(YearOfTheDragonLevels.NameTableOffset + 4 * i), (uint)(0x80010000 + at - 0x800));
            at += name.Length + 1;
        }
        Check(YearOfTheDragonLevels.ReadNames(exe).SequenceEqual(YearOfTheDragonLevels.All.Select(l => l.Name)), "the executable's name table reads");
    }

    // ---------------------------------------------------------------- synthetic level
    const int OriginX = 1000, OriginY = 1000, OriginZ = 900, Tile = 96, Columns = 13;
    static void W32(List<byte> b, uint v) { Span<byte> t = stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(t, v); b.AddRange(t.ToArray()); }
    static void W16(List<byte> b, int v) { b.Add((byte)v); b.Add((byte)(v >> 8)); }

    /// <summary>A level file: <paramref name="sectors"/> one-tile sectors (a 13-wide floor of 96-unit tiles at z 1000;
    /// sector 0 also has a triangle, a quad with a repeated corner, an empty face, a two-sided and a reversed face and
    /// a far mesh; the last sector's tile is hot ground drawn green), a far-only sector, three textures (two alike),
    /// two ground kinds, collision with the hot tile's two triangles and a water triangle, and an object list.</summary>
    static byte[] Synthetic(int sectors)
    {
        // Sub-file 0: texture memory. Page x 512 (page 8), 8-bit; palette at (512, 500). Texture t's pixels are index t.
        byte[] vram = new byte[0x80000 + 0x100];
        void Px(int x, int y, ushort c) => BinaryPrimitives.WriteUInt16LittleEndian(vram.AsSpan(2 * (y * 512 + x - 512)), c);
        Px(512, 500, 0x7FFF);              // index 0: white
        Px(513, 500, 0x7C1F & 0x7FFF);     // index 1: magenta-ish
        Px(514, 500, 0x7FFF);              // index 2: white (like texture 0)
        Px(515, 500, (ushort)(20 | 20 << 5 | 20 << 10)); // index 3: grey (texture 3, tinted green by its colours)
        for (int t = 0; t < 4; t++)
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 16; x++) Px(512 + t * 16 + x, y, (ushort)(t | t << 8));

        // Sub-file 1: textures, sectors, ground kinds.
        List<byte> sub1 = [];
        int textures = 4;
        W32(sub1, (uint)(8 + textures * 48)); W32(sub1, (uint)textures);
        for (int t = 0; t < textures; t++)
            for (int k = 0; k < 6; k++)
            {
                int u = t * 32;   // 8-bit: two texels per 16-bit pixel
                sub1.Add((byte)u); sub1.Add(0); W16(sub1, 500 << 6 | 32); sub1.Add((byte)(u + 31)); sub1.Add(31); sub1.Add(0x88); sub1.Add(0);
            }
        List<byte[]> blocks = [];
        for (int s = 0; s < sectors; s++) blocks.Add(Sector(s, s == sectors - 1));
        blocks.Add(FarOnly());
        List<byte> list = [];
        W32(list, 4); W32(list, (uint)blocks.Count);
        int cursor = 8 + 4 * blocks.Count;
        foreach (var b in blocks) { W32(list, (uint)cursor); cursor += b.Length; }
        foreach (var b in blocks) list.AddRange(b);
        W32(sub1, (uint)(list.Count + 4)); sub1.AddRange(list);
        // Ground kinds: 0 hot (1, 0); 1 deadly sink (0, 1, 0).
        List<byte> kinds = [];
        W32(kinds, 2); W32(kinds, 12); W32(kinds, 20);
        W32(kinds, 1); W32(kinds, 0); W32(kinds, 0); W32(kinds, 1); W32(kinds, 0);
        W32(sub1, (uint)(kinds.Count + 4)); sub1.AddRange(kinds);

        // Sub-file 2: collision. The hot tile (last sector) as two triangles with kind 0, one water triangle on tile 1.
        var (hx, hy) = TileAt(sectors - 1);
        List<(int[] A, int[] B, int[] C, int Flag, bool Water)> tris =
        [
            ([hx, hy, 1000], [hx + Tile, hy, 1000], [hx + Tile, hy + Tile, 1000], 0x80, false),
            ([hx, hy, 1000], [hx + Tile, hy + Tile, 1000], [hx, hy + Tile, 1000], 0xC0, false),
        ];
        var (wx, wy) = TileAt(1);
        tris.Add(([wx, wy, 1000], [wx + Tile, wy, 1000], [wx + Tile, wy + Tile, 1000], 0x3F, true));
        tris.Add(([wx, wy, 1000], [wx - 40, wy + 200, 1000], [wx + 200, wy - 250, 1004], 0x3F, false));   // signed deltas
        List<byte> coll = [];
        int cells = 0x40, tri = 0x80, flags = tri + 12 * tris.Count;
        W32(coll, (uint)tris.Count); W32(coll, 2); W32(coll, 0); W32(coll, 0x24); W32(coll, (uint)cells); W32(coll, (uint)tri); W32(coll, (uint)flags);
        while (coll.Count < tri) coll.Add(0);
        foreach (var (a, b, c, _, water) in tris)
        {
            uint X(int k) => (uint)(a[k] & 0x3FFF) | (uint)((b[k] - a[k]) & 0x1FF) << 14 | (uint)((c[k] - a[k]) & 0x1FF) << 23;
            W32(coll, X(0)); W32(coll, X(1));
            W32(coll, (uint)(a[2] & 0x3FFF) | (water ? 0x4000u : 0) | (uint)(b[2] - a[2]) << 16 | (uint)(c[2] - a[2]) << 24);
        }
        coll.Add((byte)tris[0].Flag); coll.Add((byte)tris[1].Flag);
        while (coll.Count % 4 != 0) coll.Add(0);
        List<byte> sub2 = [0, 0, 0, 0];
        W32(sub2, (uint)(coll.Count + 4)); sub2.AddRange(coll);

        // Sub-file 3: objects.
        List<byte> sub3 = [];
        W32(sub3, 8 + 6 * 0x58); W32(sub3, 6);
        for (int i = 0; i < 6; i++)
        {
            byte[] r = new byte[0x58];
            BinaryPrimitives.WriteInt32LittleEndian(r.AsSpan(0xC), (OriginX + 100 * i) * 16);
            BinaryPrimitives.WriteInt32LittleEndian(r.AsSpan(0x10), (OriginY + 50) * 16);
            BinaryPrimitives.WriteInt32LittleEndian(r.AsSpan(0x14), 1000 * 16);
            BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(0x36), (ushort)(1002 + i));
            r[0x46] = (byte)(10 * i);
            sub3.AddRange(r);
        }

        byte[][] subs = [vram, [.. sub1], [.. sub2], [.. sub3]];
        int size = 8 * 18 + subs.Sum(s => (s.Length + 3) & ~3);
        byte[] entry = new byte[size];
        int offset = 8 * 18;
        for (int k = 0; k < subs.Length; k++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(entry.AsSpan(8 * k), offset);
            BinaryPrimitives.WriteInt32LittleEndian(entry.AsSpan(8 * k + 4), subs[k].Length);
            subs[k].CopyTo(entry, offset); offset += (subs[k].Length + 3) & ~3;
        }
        return entry;
    }

    static (int X, int Y) TileAt(int s) => (OriginX + (s % Columns) * Tile, OriginY + (s / Columns) * Tile);

    static byte[] Sector(int s, bool hot)
    {
        var (x, y) = TileAt(s);
        bool extra = s == 0;
        // Near vertices (relative to the sector's origin, which is the tile's corner; z offsets even).
        List<(int X, int Y, int Z)> v = [(0, 0, 100), (Tile, 0, 100), (Tile, Tile, 100), (0, Tile, 100)];
        if (extra) v.AddRange([(20, 20, 140), (60, 20, 140), (40, 60, 140), (70, 70, 160)]);
        List<(byte R, byte G, byte B)> near = [(128, 128, 128), (100, 100, 100)], far = [(10, 20, 30), (40, 50, 60)];
        if (hot) { near = [(40, 200, 40), (40, 200, 40)]; far = [(0, 90, 0), (0, 90, 0)]; }
        List<(int[] Vi, int[] Ci, int Tex, int Flags)> faces = [([0, 1, 2, 3], [0, 1, 0, 1], hot ? 3 : s % 3, 0)];
        if (extra)
        {
            faces.Add(([4, 4, 5, 6], [0, 0, 1, 0], 1, 0x08));      // triangle 5,6 after the repeated first corner; two-sided
            faces.Add(([4, 5, 5, 7], [0, 1, 1, 0], 2, 0x04));      // quad with a repeated corner -> triangle 4,5,7; reversed
            faces.Add(([4, 4, 4, 5], [0, 0, 0, 0], 0, 0));         // empty
        }
        List<byte> b = [];
        W16(b, 0); W16(b, 0); W16(b, 0); W16(b, 0);       // centre and flags (heights in steps of 16)
        W16(b, y); W16(b, x); W16(b, 0); W16(b, OriginZ);  // origin y, x, -, z
        int fv = extra ? 4 : 0, fc = extra ? 2 : 0, ff = extra ? 1 : 0;
        b.AddRange([(byte)fv, (byte)fc, (byte)ff, 0, (byte)v.Count, (byte)near.Count, (byte)faces.Count, (byte)(fv + fc + 2 * ff)]);
        while (b.Count < 0x1C) b.Add(0);
        if (extra)
        {
            foreach (var p in v.Take(4)) W32(b, (uint)(p.X << 21 | p.Y << 10 | p.Z / 2));
            W32(b, 0x00FF0000); W32(b, 0x0000FF00);
            // One far quad 0,1,2,3 (corners at bits 10, 17, 24, 3), colours 0,1,0,1.
            W32(b, (uint)(0 << 10 | 1 << 17 | 2 << 24 | 3 << 3)); W32(b, (uint)(0 << 11 | 1 << 18 | 0 << 25 | 1 << 4));
        }
        foreach (var p in v) W32(b, (uint)(p.X << 21 | p.Y << 10 | p.Z / 2));
        foreach (var c in near.Concat(far)) { b.Add(c.R); b.Add(c.G); b.Add(c.B); b.Add(0); }
        foreach (var (vi, ci, tex, flags) in faces)
        {
            b.AddRange(vi.Select(k => (byte)k)); b.AddRange(ci.Select(k => (byte)k)); b.AddRange([0, 0, 0, 0]);
            b.Add((byte)tex); b.Add((byte)flags); b.Add(0); b.Add(0);
        }
        return [.. b];
    }

    static byte[] FarOnly()
    {
        List<byte> b = [];
        W16(b, 0); W16(b, 0); W16(b, 0); W16(b, 0); W16(b, 5000); W16(b, 5000); W16(b, 0); W16(b, 900);
        b.AddRange([3, 1, 1, 0, 0, 0, 0, 0]);
        while (b.Count < 0x1C) b.Add(0);
        W32(b, 0); W32(b, 100u << 21); W32(b, 100u << 10); W32(b, 0x808080);
        W32(b, (uint)(0 << 10 | 1 << 17 | 2 << 24 | 0 << 3)); W32(b, 0);
        return [.. b];
    }

    static void Decoder(YearOfTheDragonLevelData d)
    {
        Check(d.Textures.Count == 4 && d.Textures[1].Quads[2].Depth == 8 && !d.Textures[0].SeeThrough, "texture records");
        Check(d.Sectors.Count == 131 && d.Sectors[^1].FarOnly && d.Sectors[^1].Far is { Faces.Count: 1 }, "sector list with a far-only sector");
        var s0 = d.Sectors[0];
        Check(s0.NearFaces.Count == 4 && s0.NearFaces[0].Corners[2] == new Vector3f(OriginX + Tile, OriginY + Tile, 1000), "near corners in editor units (origin + offset, heights x2)");
        Check(s0.NearFaces[1].Triangle && s0.NearFaces[1].TwoSided && s0.NearFaces[2].FrontReversed && !s0.NearFaces[2].Triangle, "triangle and side flags");
        // A triangle's first colour slot is replaced by slot 1, as the game draws it.
        Check(s0.NearFaces[1].Near[0] == s0.NearFaces[1].Near[1] && s0.NearFaces[0].Far[1] == new ColorRgba(40, 50, 60), "colour slots and far colours");
        Check(s0.Far is { } far && far.Faces.Count == 1 && far.Faces[0].Corners.SequenceEqual([0, 3, 1, 2]) && far.Vertices[2] == new Vector3f(OriginX + Tile, OriginY + Tile, 1000),
            "far quad in the scene's corner order 0,3,1,2");
        Check(d.GroundKinds.Count == 2 && d.GroundKinds[0].Type == 1 && d.GroundKinds[1] is { Type: 0, Parameters: [1, 0] }, "ground kinds");
        Check(d.Collision.Count == 4 && d.Collision[0].Kind == 0 && d.Collision[0].Sound == 2 && d.Collision[1].Sound == 3 && d.Collision[2] is { Kind: 0x3F, Water: true }, "collision kinds, sounds and the water bit");
        var (wx, wy) = TileAt(1);
        Check(d.Collision[3].B == new Vector3f(wx - 40, wy + 200, 1000) && d.Collision[3].C == new Vector3f(wx + 200, wy - 250, 1004), "collision deltas are signed (x, y) and unsigned (z)");
        Check(d.Objects.Count == 6 && d.Objects[2] is { Class: 1004, Heading: 20 } && d.Objects[2].Position == new Vector3f(OriginX + 200, OriginY + 50, 1000), "object list");
        var picture = d.Picture(1);
        Check(picture.Length == 64 * 64 && picture.All(c => c == picture[0]) && picture[0].R == 255 && picture[0].G == 0, "a texture's 64x64 picture from its quarters");
    }

    static void Faces()
    {
        Vector3f a = new(0, 0, 0), b = new(10, 0, 0), c = new(10, 10, 0), e = new(0, 10, 0);
        ColorRgba[] k = [new(1, 1, 1), new(1, 1, 1), new(1, 1, 1), new(1, 1, 1)];
        YotdNearFace F(Vector3f[] v, bool tri) => new(0, v, k, k, 0, 0, tri);
        Check(F([a, b, c, e], false).DrawnCorners().SequenceEqual([0, 1, 2, 3]), "a quad keeps its corners");
        Check(F([a, a, c, e], true).DrawnCorners().SequenceEqual([1, 2, 3]), "a triangle drops its repeated first corner");
        Check(F([a, b, b, e], false).DrawnCorners().SequenceEqual([0, 1, 3]), "a repeated corner is dropped");
        Check(F([a, b, c, a], false).DrawnCorners().SequenceEqual([0, 1, 2]), "a corner equal to the first is dropped across the wrap");
        Check(F([a, b, a, b], false).DrawnCorners().Length == 0 && F([a, a, a, b], false).DrawnCorners().Length == 0, "fewer than three different corners draw nothing");
    }

    static void Kinds()
    {
        Check(YearOfTheDragonLevelImport.Map(new(0, [0, 0]), null) == AuthoredSurfaceKind.Water && YearOfTheDragonLevelImport.Map(new(0, [1, 0]), null) == AuthoredSurfaceKind.DeadlyWater &&
              YearOfTheDragonLevelImport.Map(new(0, [1, 1]), null) == AuthoredSurfaceKind.DeadlyLava, "sink is Spyro the Dragon's hazard record");
        Check(YearOfTheDragonLevelImport.Map(new(1, [0]), (40, 160, 40)) == AuthoredSurfaceKind.DeadlyOoze && YearOfTheDragonLevelImport.Map(new(1, [0]), (200, 80, 20)) == AuthoredSurfaceKind.DeadlyLava &&
              YearOfTheDragonLevelImport.Map(new(1, [0]), (200, 80, 20), hotIsDeadly: false) == AuthoredSurfaceKind.Lava, "hot ground is lava, or ooze when green");
        Check(YearOfTheDragonLevelImport.Map(new(2, []), null) == AuthoredSurfaceKind.Supercharge && YearOfTheDragonLevelImport.Map(new(3, [0]), null) == null &&
              YearOfTheDragonLevelImport.Map(new(6, [1]), null) == null, "supercharge maps; triggers and doorways do not");
    }

    static async Task Import(YearOfTheDragonLevelData d)
    {
        string folder = Path.Combine(Path.GetTempPath(), "yotd-import-free-" + Guid.NewGuid().ToString("N"));
        try
        {
            // Four carriers for four textures, and a texture memory that holds three pictures: texture 0 (white, like 2)
            // folds into 2; textures 1 (the water tile's) and 3 (hot ground) are special ground and never merged.
            YearOfTheDragonLevelImport.Carrier[] carriers = [new(10, 1, 100), new(10, 2, 100), new(12, 3, 200), new(12, 4, 200)];
            int tested = 0;
            var result = await YearOfTheDragonLevelImport.BuildAsync(d, null, carriers, m => { tested++; return (m.Count <= 3, 60000 - m.Count, null); },
                new() { PictureFolder = folder, TextureMemoryReserve = 1000 });
            var r = result.Report;
            var scene = result.Scene;
            Check(r.YotdFaces == 133 && r.Faces == 132 && r.RepairedFaces == 1 && r.EmptyFaces == 1 && r.FarOnlySectors == 1, "faces: one repaired, one empty");
            Check(r.Textures == 4 && r.KeptTextures == 3 && r.Merges.Count == 1 && r.Merges[0] == 2 && tested == 2 && r.FreeTextureBytes == 59997, "textures merged until they fit the memory");
            Check(result.Materials.Count == 3 && result.Materials.All(m => File.Exists(m.Image)), "kept pictures written");
            Check(scene.Surfaces.Count(s => s.Kind == AuthoredSurfaceKind.DeadlyOoze) == 1 && scene.Surfaces.Count(s => s.Kind == AuthoredSurfaceKind.Water) == 1, "hot green tile is deadly ooze, the water tile is water");
            Check(r.SectorGroups <= 127 && r.NativeSectors <= 127 && scene.Surfaces.Select(s => s.Sector).Distinct().Count() == r.SectorGroups, "130 sectors grouped under the game's 127");
            Check(scene.Surfaces.Count(s => s.TwoSided) == 1 && scene.Surfaces.Count(s => s.FrontReversed) == 1, "sides");
            Check(scene.FarMeshes is { Count: 1 } && scene.FarMeshes[0].Sector == scene.Surfaces.First(s => s.Vertices.Contains(new Vector3f(OriginX, OriginY, 1000))).Sector, "the far mesh follows its sector's group");
            NativeAuthoredSpawn.RequirePortalArrivalSupported(scene);
            var anchor = d.Objects.First(o => o.Class == 1002).Position;
            Check(scene.Spawn.Z == 1032 && r.SpawnSource.Contains("start object") && Math.Abs(scene.Spawn.X - anchor.X) <= 1024 && Math.Abs(scene.Spawn.Y - anchor.Y) <= 1024,
                "start on the open floor nearest the level's start object");
            var centred = YearOfTheDragonLevelImport.FindSpawn(scene, null);
            Check(centred.How.Contains("centre") && centred.Spawn.Z == 1032, "without a start object, the open floor nearest the centre");
            Check(r.TerrainBytes > 0 && scene.Name == "SUNNY VILLA", "compiled");
            Check(d.SkyBackground == null && r.Warnings.Any(w => w.Contains("no sky block")) && scene.SkyColor == SceneDocument.New().SkyColor, "no sky block: the plain colour stays, reported");
            // A replaced level that kills Spyro below 1,200 lifts the level until its lowest face (1,000) is 64 above that.
            var lifted = await YearOfTheDragonLevelImport.BuildAsync(d, null, carriers, m => (m.Count <= 3, 60000 - m.Count, null), new() { PictureFolder = folder, TextureMemoryReserve = 1000 }, killHeight: 1200);
            Check(lifted.Report.Lift == 264 && lifted.Scene.Surfaces.Min(s => s.Vertices.Min(v => v.Z)) == 1264 && lifted.Scene.Spawn.Z == 1296 &&
                  lifted.Scene.FarMeshes![0].Vertices.Min(v => v.Z) == 1264 && lifted.Report.Warnings.Any(w => w.StartsWith("Raised 264")), "lifted above the replaced level's kill height");
            // Deterministic: the same import gives the same scene.
            var again = await YearOfTheDragonLevelImport.BuildAsync(d, null, carriers, m => (m.Count <= 3, 60000 - m.Count, null), new() { PictureFolder = folder, TextureMemoryReserve = 1000 });
            Check(again.Scene.Surfaces.Select(s => s.Id).SequenceEqual(scene.Surfaces.Select(s => s.Id)), "stable face identities");
            // A level whose special ground needs more textures than carriers, or whose textures cannot be merged into the
            // memory, is refused, not cut.
            bool refused = false;
            try { await YearOfTheDragonLevelImport.BuildAsync(d, null, carriers[..1], null, new() { PictureFolder = folder }); }
            catch (YearOfTheDragonImportException) { refused = true; }
            Check(refused, "too few carriers is refused");
            refused = false;
            try { await YearOfTheDragonLevelImport.BuildAsync(d, null, carriers, m => (m.Count <= 2, 1, null), new() { PictureFolder = folder }); }
            catch (YearOfTheDragonImportException e) { refused = e.Message.Contains("special ground"); }
            Check(refused, "textures that cannot fit are refused");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}
