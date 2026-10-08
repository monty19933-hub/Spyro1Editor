using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

/// <summary>WUMPA VILLAGE: a Crash Bandicoot: Warped style medieval village built as a blank level (Dark Hollow's
/// slot) with Crash 3's own textures (pictures made by work/crash3-20261006/tools/wumpa_pictures.py from the user's
/// disc, one PNG per material name). Shared by UiSmoke --new-level (wumpa-build) and CrashLevelSmoke --village. Editor units; ground at z 600; north is +y. Areas: the village green and
/// start plaza (centre south), the market square (east), the toad pond (west, sunken), the castle (north) with
/// gatehouse, courtyard, ramparts and keep, and windmill hill (north-east) reached by a ramp or by gliding from
/// the keep. An outer wall keeps Spyro in. Faces are visible from the right-hand side of a->b (walls) or from
/// above when counter-clockwise (floors); a quad's corners 2-3 are its texture's bottom edge.</summary>
internal static class WumpaVillage
{
    public const string Name = "WUMPA VILLAGE";
    public const float Ground = 600;
    public static readonly Vector3f Spawn = new(3200, 2000, Ground + 32);

    public const int MusicTrack = 26;   // Town Square
    /// <summary>Material name -> carrier texture in Dark Hollow (non-mirrored, outside shared groups); its picture
    /// is &lt;name&gt;.png in the pictures folder. Walls standing on the ground (castle, cottage, brick, barn, windmill,
    /// hillwall) carry Crash's tufted base course; their upper courses use the plain variant.</summary>
    public static readonly (string Name, int Carrier)[] Materials =
    [
        ("grass", 3), ("path", 4), ("plaza", 5), ("courtyard", 6), ("castle", 7), ("castlewalk", 8), ("slit", 9),
        ("cottage", 10), ("door", 11), ("thatch", 12), ("awning", 13), ("planks", 16), ("fence", 17), ("posts", 18),
        ("pond", 19), ("awningedge", 20), ("hillwall", 21), ("stonetop", 23), ("brick", 26), ("castleplain", 27),
        ("plaster", 28), ("market", 30), ("wellwater", 33), ("barn", 34), ("windmill", 35), ("stoneside", 36),
        ("brickplain", 37), ("windmillplain", 39),
    ];
    /// <summary>Upper courses of a based wall show the plain picture (the base belongs at the foot only).</summary>
    private static readonly Dictionary<string, string> Plain = new()
    {
        ["castle"] = "castleplain", ["cottage"] = "plaster", ["brick"] = "brickplain",
        ["windmill"] = "windmillplain", ["hillwall"] = "stoneside",
    };
    /// <summary>The pond: the game's own water, which costs Spyro a life (he cannot swim).</summary>
    private static readonly AuthoredSurfaceKind PondKind = AuthoredSurfaceKind.Water;

    /// <summary>The level as a scene for a blank Dark Hollow (<paramref name="blank"/> keeps its target and ledger).</summary>
    public static SceneDocument Scene(SceneDocument blank, int darkHollowWad, int skyWad, string pictures)
    {
        var materials = Materials.ToDictionary(m => m.Name,
            m => new AuthoredMaterial(darkHollowWad, m.Carrier) { Image = Path.GetFullPath(Path.Combine(pictures, m.Name + ".png")) });
        return blank with
        {
            Name = Name, Spawn = Spawn, Heading = 64, SkyColor = new(240, 150, 90), MusicTrack = MusicTrack,
            Surfaces = Build(n => materials[n]), SkySourceWadEntry = skyWad,
        };
    }

    // Objects: (what, class, source level, x, y, height of the surface there). "crash:<id>" are the Crash boxes and
    // enemies (class unused), "egg:<colour>" dragonfly eggs, "sign:<text>" the sign. 65 objects (Dark Hollow keeps
    // 71 free save rows after Bandicoot Keep) worth 200 treasure, enemy drops included.
    private const int Red = 0x53, Green = 0x54, Blue = 0x55, Gold = 0x56;
    public static readonly (string What, int Class, string Source, float X, float Y, float Z)[] Objects =
    [
        // Start plaza and the north path.
        ("sign:WELCOME TO\nWUMPA VILLAGE", 0x1D0, "japan", 3200, 2230, 600),
        ("crash:crash-crate-red", 0, "crash", 2980, 1800, 600), ("crash:crash-crate", 0, "crash", 3420, 1800, 600),
        ("gem", Red, "artisans", 3200, 2650, 600), ("gem", Red, "artisans", 3200, 3050, 600), ("gem", Red, "artisans", 3200, 3450, 600),
        ("prop", 0x163, "artisans", 2330, 2150, 600),
        // Village green and the cottages.
        ("gem", Green, "artisans", 2600, 2300, 600), ("gem", Green, "artisans", 2560, 3120, 600),
        ("gem", Green, "artisans", 3900, 3250, 600), ("gem", Green, "artisans", 3950, 2100, 600),
        ("crash:crash-crate-green", 0, "crash", 2340, 2480, 600), ("crash:crash-crate-green", 0, "crash", 2880, 3650, 600),
        ("crash:crash-crate-blue", 0, "crash", 2380, 3200, 600),
        // Toad pond: frogs, gems on the stepping stones, a dragonfly egg on the last stone.
        ("crash:crash-frog", 0, "crash", 1620, 1960, 520), ("crash:crash-frog", 0, "crash", 1900, 2760, 520), ("crash:crash-frog", 0, "crash", 1540, 2900, 520),
        ("gem", Blue, "townsquare", 1608, 2128, 560), ("gem", Gold, "clifftown", 1928, 2528, 560),
        ("egg:dragonfly-egg-pink", 0x1E9, "japan", 1688, 2688, 560),
        ("gem", Red, "artisans", 1320, 1320, 600), ("gem", Red, "artisans", 1320, 3600, 600),
        // Market square: gems on the stall decks, crates, TNT and a Nitro by the bakery, a knight on patrol.
        ("gem", Blue, "townsquare", 4704, 2904, 640), ("gem", Blue, "townsquare", 5184, 2904, 640), ("gem", Blue, "townsquare", 5664, 2904, 640),
        ("gem", Red, "artisans", 4600, 1500, 600), ("gem", Red, "artisans", 5400, 2500, 600),
        ("crash:crash-crate-blue", 0, "crash", 4500, 1300, 600), ("crash:crash-crate-blue", 0, "crash", 5900, 1300, 600),
        ("crash:crash-crate-gold", 0, "crash", 4700, 2600, 600), ("crash:crash-crate", 0, "crash", 4560, 2250, 600),
        ("crash:crash-tnt", 0, "crash", 5300, 1900, 600), ("crash:crash-tnt", 0, "crash", 5750, 2450, 600),
        ("crash:crash-nitro", 0, "crash", 5420, 1760, 600),
        ("crash:crash-knight", 0, "crash", 5200, 2300, 600),
        // Castle: knights at the gate and on the north rampart, the wizard and a dragonfly egg on the keep.
        ("crash:crash-knight", 0, "crash", 3050, 4400, 600), ("crash:crash-knight", 0, "crash", 3350, 4400, 600),
        ("crash:crash-knight", 0, "crash", 2800, 5376, 900),
        ("crash:crash-wizard", 0, "crash", 3680, 5200, 1020),
        ("gem", Gold, "clifftown", 2656, 4384, 640),
        ("gem", Green, "artisans", 2528, 4400, 656), ("gem", Green, "artisans", 2528, 4750, 749), ("gem", Green, "artisans", 2528, 5100, 843),
        ("gem", Red, "artisans", 2500, 4032, 900), ("gem", Red, "artisans", 3000, 4032, 900), ("gem", Red, "artisans", 3700, 4032, 900),
        ("gem", Blue, "townsquare", 4032, 4700, 900), ("gem", Blue, "townsquare", 2368, 4600, 900),
        ("crash:crash-crate-gold", 0, "crash", 3600, 5350, 1020), ("egg:dragonfly-egg-gold", 0x1E9, "japan", 3760, 5000, 1020),
        ("prop", 0x0AC, "artisans", 3790, 5390, 1020),
        // Windmill hill: a wizard by the windmill, a frog, the purple crate in the far corner, a dragonfly egg behind the mill.
        ("crash:crash-wizard", 0, "crash", 4900, 4300, 760), ("crash:crash-frog", 0, "crash", 5600, 5000, 760),
        ("gem", Gold, "clifftown", 5700, 5300, 760), ("gem", Blue, "townsquare", 4800, 4000, 760), ("gem", Blue, "townsquare", 5600, 4000, 760),
        ("gem", Green, "artisans", 4800, 3450, 683), ("gem", Green, "artisans", 5600, 3450, 683),
        ("egg:dragonfly-egg-blue", 0x1E9, "japan", 5250, 4900, 760), ("crash:crash-crate-purple", 0, "crash", 5850, 5450, 760),
        // The field behind the castle.
        ("crash:crash-knight", 0, "crash", 1700, 4700, 600), ("gem", Gold, "clifftown", 1400, 5400, 600),
        ("gem", Green, "artisans", 1700, 4200, 600), ("crash:crash-crate", 0, "crash", 2000, 5000, 600),
        ("crash:crash-crate-blue", 0, "crash", 1500, 4800, 600),
    ];

    private sealed class B(Func<string, AuthoredMaterial> mat)
    {
        public readonly List<AuthoredSurface> S = [];
        private static string Id() => Guid.NewGuid().ToString("N");
        // Warm late-afternoon shading: soft light and dark patches on the ground, darker wall bottoms.
        private static ColorRgba C(int v) => new((byte)Math.Clamp(v + 6, 0, 255), (byte)Math.Clamp(v, 0, 255), (byte)Math.Clamp(v - 10, 0, 255));
        private static int Noise(float x, float y) => (int)((Math.Sin(x * 0.0131 + y * 0.0071) + Math.Sin(x * 0.0057 - y * 0.0113)) * 13);
        public void Quad(Vector3f[] v, string m, ColorRgba[]? colors = null)
        {
            var c = colors ?? v.Select(p => C(114 + Noise(p.X, p.Y))).ToArray();
            S.Add(new(Id(), v, c[0], mat(m), c, c) { Kind = m == "pond" ? PondKind : null });
        }
        /// <summary>Sunlight on a wall from its facing: the evening sun is low in the south-west.</summary>
        private static int Sun(float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay, len = MathF.Sqrt(dx * dx + dy * dy);
            if (len == 0) return 0;
            float nx = dy / len, ny = -dx / len;   // the visible side is on the right of a->b
            return (int)MathF.Round(22 * (nx * -0.8f + ny * -0.6f));
        }
        /// <summary>Floor rectangle split into tiles of at most <paramref name="tile"/>, counter-clockwise.</summary>
        public void Floor(float x0, float y0, float x1, float y1, float z, string m, float tile = 0)
        {
            if (tile == 0) tile = m == "grass" ? 320 : 240;   // open grass takes larger tiles (fewer close-up faces)
            int nx = Math.Max(1, (int)Math.Ceiling((x1 - x0) / tile)), ny = Math.Max(1, (int)Math.Ceiling((y1 - y0) / tile));
            for (int i = 0; i < nx; i++) for (int j = 0; j < ny; j++)
            {
                float a = R(x0 + (x1 - x0) * i / nx), b = R(x0 + (x1 - x0) * (i + 1) / nx), c = R(y0 + (y1 - y0) * j / ny), d = R(y0 + (y1 - y0) * (j + 1) / ny);
                Quad([new(a, c, z), new(b, c, z), new(b, d, z), new(a, d, z)], m);
            }
        }
        /// <summary>Ramp rising from z0 at y0 to z1 at y1 (or along x when <paramref name="alongX"/>).</summary>
        public void Ramp(float x0, float y0, float x1, float y1, float z0, float z1, string m, bool alongX = false, float tile = 240)
        {
            int nx = Math.Max(1, (int)Math.Ceiling((x1 - x0) / tile)), ny = Math.Max(1, (int)Math.Ceiling((y1 - y0) / tile));
            float Z(float x, float y) => R(alongX ? z0 + (z1 - z0) * (x - x0) / (x1 - x0) : z0 + (z1 - z0) * (y - y0) / (y1 - y0));
            for (int i = 0; i < nx; i++) for (int j = 0; j < ny; j++)
            {
                float a = R(x0 + (x1 - x0) * i / nx), b = R(x0 + (x1 - x0) * (i + 1) / nx), c = R(y0 + (y1 - y0) * j / ny), d = R(y0 + (y1 - y0) * (j + 1) / ny);
                Quad([new(a, c, Z(a, c)), new(b, c, Z(b, c)), new(b, d, Z(b, d)), new(a, d, Z(a, d))], m);
            }
        }
        /// <summary>Vertical wall from a to b (visible on the right of a->b), split into panels of at most
        /// <paramref name="panel"/> wide and <paramref name="course"/> high; texture upright.</summary>
        public void Wall(float ax, float ay, float bx, float by, float z0, float z1, string m, float panel = 0, float course = 320, int shade = 0)
        {
            if (panel == 0) panel = m == "castle" ? 320 : 240;   // the long castle walls take wider panels
            float len = MathF.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            int n = Math.Max(1, (int)Math.Ceiling(len / panel)), k = Math.Max(1, (int)Math.Ceiling((z1 - z0) / course));
            for (int i = 0; i < n; i++) for (int j = 0; j < k; j++)
            {
                float t0 = (float)i / n, t1 = (float)(i + 1) / n;
                float xa = R(ax + (bx - ax) * t0), ya = R(ay + (by - ay) * t0), xb = R(ax + (bx - ax) * t1), yb = R(ay + (by - ay) * t1);
                float za = R(z0 + (z1 - z0) * j / k), zb = R(z0 + (z1 - z0) * (j + 1) / k);
                int sun = shade + Sun(ax, ay, bx, by);
                ColorRgba lo = C(96 + sun + (int)(za - z0) / 8), hi = C(136 + sun);
                // Corners (top-b, top-a, bottom-a, bottom-b): same facing as (bottom-a, bottom-b, top-b, top-a), bottom edge at 2-3.
                Quad([new(xb, yb, zb), new(xa, ya, zb), new(xa, ya, za), new(xb, yb, za)], j > 0 && Plain.TryGetValue(m, out var plain) ? plain : m, [hi, hi, lo, lo]);
            }
        }
        /// <summary>Closed box: four outward walls and a top.</summary>
        public void Box(float x0, float y0, float x1, float y1, float z0, float z1, string wall, string? top, float panel = 240)
        {
            Wall(x0, y0, x1, y0, z0, z1, wall, panel); Wall(x1, y0, x1, y1, z0, z1, wall, panel);
            Wall(x1, y1, x0, y1, z0, z1, wall, panel); Wall(x0, y1, x0, y0, z0, z1, wall, panel);
            if (top != null) Floor(x0, y0, x1, y1, z1, top);
        }
        /// <summary>Gable roof over a rectangle, ridge along x (or y), eaves overhanging by <paramref name="eave"/>.</summary>
        public void Gable(float x0, float y0, float x1, float y1, float zEave, float zRidge, string roof, string gableEnd, bool ridgeAlongX = true, float eave = 24)
        {
            if (ridgeAlongX)
            {
                float ym = R((y0 + y1) / 2), ex0 = x0 - eave, ex1 = x1 + eave, ey0 = y0 - eave, ey1 = y1 + eave;
                Quad([new(ex0, ey0, zEave), new(ex1, ey0, zEave), new(ex1, ym, zRidge), new(ex0, ym, zRidge)], roof);   // south slope
                Quad([new(ex1, ey1, zEave), new(ex0, ey1, zEave), new(ex0, ym, zRidge), new(ex1, ym, zRidge)], roof);   // north slope
                Tri(new(x0, y1, zEave), new(x0, y0, zEave), new(x0, ym, zRidge), gableEnd);                             // west end
                Tri(new(x1, y0, zEave), new(x1, y1, zEave), new(x1, ym, zRidge), gableEnd);                             // east end
            }
            else
            {
                float xm = R((x0 + x1) / 2), ex0 = x0 - eave, ex1 = x1 + eave, ey0 = y0 - eave, ey1 = y1 + eave;
                Quad([new(ex1, ey0, zEave), new(ex1, ey1, zEave), new(xm, ey1, zRidge), new(xm, ey0, zRidge)], roof);   // east slope
                Quad([new(ex0, ey1, zEave), new(ex0, ey0, zEave), new(xm, ey0, zRidge), new(xm, ey1, zRidge)], roof);   // west slope
                Tri(new(x0, y0, zEave), new(x1, y0, zEave), new(xm, y0, zRidge), gableEnd);                             // south end
                Tri(new(x1, y1, zEave), new(x0, y1, zEave), new(xm, y1, zRidge), gableEnd);                             // north end
            }
        }
        /// <summary>Wall from a to b whose top slopes from <paramref name="topA"/> to <paramref name="topB"/> (a ramp's
        /// side); panels that reach the base at one end become triangles.</summary>
        public void SlopedWall(float ax, float ay, float bx, float by, float zBase, float topA, float topB, string m, float panel = 240)
        {
            float len = MathF.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            int n = Math.Max(1, (int)Math.Ceiling(len / panel));
            for (int i = 0; i < n; i++)
            {
                float t0 = (float)i / n, t1 = (float)(i + 1) / n;
                Vector3f ta = new(R(ax + (bx - ax) * t0), R(ay + (by - ay) * t0), R(topA + (topB - topA) * t0)), tb = new(R(ax + (bx - ax) * t1), R(ay + (by - ay) * t1), R(topA + (topB - topA) * t1));
                Vector3f ba = ta with { Z = zBase }, bb = tb with { Z = zBase };
                if (ta.Z <= zBase) Tri(tb, ba, bb, m); else if (tb.Z <= zBase) Tri(tb, ta, ba, m); else Quad([tb, ta, ba, bb], m);
            }
        }
        /// <summary>Upward-facing triangle (corners put in counter-clockwise order).</summary>
        public void FloorTri(Vector3f a, Vector3f b, Vector3f c, string m)
        {
            float cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
            if (cross < 0) (b, c) = (c, b);
            var col = new[] { a, b, c }.Select(p => C(114 + Noise(p.X, p.Y))).ToArray();
            S.Add(new(Id(), [a, b, c], col[0], mat(m), col, col));
        }
        /// <summary>A bank's corner piece: the inner corner at <paramref name="zi"/>, the other three at <paramref name="zo"/>.</summary>
        public void CornerSlope(float xi, float yi, float xo, float yo, float zi, float zo, string m)
        {
            FloorTri(new(xi, yi, zi), new(xo, yi, zo), new(xo, yo, zo), m);
            FloorTri(new(xi, yi, zi), new(xo, yo, zo), new(xi, yo, zo), m);
        }
        public void Tri(Vector3f a, Vector3f b, Vector3f c, string m)
        {
            var col = new[] { C(120), C(120), C(150) };
            S.Add(new(Id(), [a, b, c], col[0], mat(m), col, col));
        }
        /// <summary>A cottage: plaster walls with a door on the south side, gable roof.</summary>
        public void Cottage(float x0, float y0, float w, float d, float h, string roof, bool ridgeAlongX)
        {
            float x1 = x0 + w, y1 = y0 + d, z0 = Ground, z1 = Ground + h;
            float dx0 = R(x0 + w / 2 - 48), dx1 = R(x0 + w / 2 + 48);
            Wall(x0, y0, dx0, y0, z0, z1, "cottage"); Wall(dx0, y0, dx1, y0, z0, z0 + 112, "door"); Wall(dx0, y0, dx1, y0, z0 + 112, z1, "plaster");
            Wall(dx1, y0, x1, y0, z0, z1, "cottage");
            Wall(x1, y0, x1, y1, z0, z1, "cottage"); Wall(x1, y1, x0, y1, z0, z1, "cottage"); Wall(x0, y1, x0, y0, z0, z1, "cottage");
            Gable(x0, y0, x1, y1, z1, R(z1 + Math.Min(w, d) * 0.55f), roof, "plaster", ridgeAlongX);
        }
        /// <summary>Wooden fence: low boards seen from both sides.</summary>
        public void Fence(float ax, float ay, float bx, float by)
        {
            Wall(ax, ay, bx, by, Ground, Ground + 48, "fence", 240, 48); Wall(bx, by, ax, ay, Ground, Ground + 48, "fence", 240, 48);
        }
        public static float R(float v) => MathF.Round(v);
    }

    public static IReadOnlyList<AuthoredSurface> Build(Func<string, AuthoredMaterial> mat)
    {
        var b = new B(mat);
        const float G = Ground;
        // --- Ground: outer bounds 1200..6000 x 1200..5600; the pond is sunken, the castle courtyard and the hill sit on top.
        // Start plaza (cobbles) and the paths.
        b.Floor(2912, 1712, 3488, 2288, G, "plaza", 192);
        b.Floor(3104, 2288, 3296, 3968, G, "path", 224);                  // north path to the castle gate
        b.Floor(3488, 1904, 4400, 2096, G, "path", 228);                  // east path to the market
        b.Floor(2208, 1904, 2912, 2096, G, "path", 235);                  // west path to the pond
        // Village green (grass) around the paths.
        b.Floor(2208, 1200, 2912, 1904, G, "grass"); b.Floor(2912, 1200, 3488, 1712, G, "grass"); b.Floor(3488, 1200, 4400, 1904, G, "grass");
        b.Floor(2208, 2096, 2912, 3968, G, "grass"); b.Floor(2912, 2288, 3104, 3968, G, "grass"); b.Floor(3296, 2288, 3488, 3968, G, "grass");
        b.Floor(3488, 2096, 4400, 3968, G, "grass");
        // Flower beds by the plaza.


        // --- Market square (east): cobbles, a raised stall row with wooden decks and red awnings.
        b.Floor(4400, 1200, 6000, 3200, G, "market", 300);
        foreach (float sx in new[] { 4560f, 5040f, 5520f })
        {
            b.Box(sx, 2800, sx + 288, 3008, G, G + 40, "planks", "planks", 288);                 // deck you can hop onto
            b.Floor(sx - 16, 2784, sx + 304, 3024, G + 200, "awning", 320);                     // awning top (stand on it after a jump from the deck? 160 up: glide only)
            b.Wall(sx - 16, 2784, sx + 304, 2784, G + 176, G + 200, "awningedge", 320, 24);         // awning front edge
            b.Box(sx, 2992, sx + 24, 3008, G + 40, G + 200, "posts", null, 24);                    // back posts
            b.Box(sx + 264, 2992, sx + 288, 3008, G + 40, G + 200, "posts", null, 24);
        }
        // A brick bakery with a chimney on the market's south side.
        b.Box(5520, 1360, 5840, 1680, G, G + 192, "brick", null);
        b.Gable(5520, 1360, 5840, 1680, G + 192, G + 352, "thatch", "brickplain", false);
        b.Box(5744, 1584, 5808, 1648, G + 260, G + 420, "brickplain", "brickplain", 64);

        // --- Toad pond (west): sunken basin with lily-pad water (the game's water: it costs Spyro a life), grass banks
        // sloping down, stepping stones to hop across.
        const float P = G - 80;
        b.Floor(1440, 1680, 2048, 3008, P, "pond", 228);
        b.Ramp(1440, 3008, 2048, 3248, P, G, "grass");                                            // north bank up
        b.Ramp(1440, 1440, 2048, 1680, G, P, "grass");                                            // south bank down
        b.Ramp(2048, 1680, 2208, 3008, P, G, "grass", alongX: true, tile: 228);                    // east bank up to the green
        b.Ramp(1200, 1680, 1440, 3008, G, P, "grass", alongX: true, tile: 228);                    // west bank
        b.CornerSlope(1440, 1680, 1200, 1440, P, G, "grass"); b.CornerSlope(2048, 1680, 2208, 1440, P, G, "grass");
        b.CornerSlope(1440, 3008, 1200, 3248, P, G, "grass"); b.CornerSlope(2048, 3008, 2208, 3248, P, G, "grass");
        b.Floor(1200, 3248, 2208, 3968, G, "grass"); b.Floor(1200, 1200, 2208, 1440, G, "grass");
        foreach (var (x, y) in new[] { (1560f, 2080f), (1720f, 2280f), (1880f, 2480f), (1640f, 2640f) })
            b.Box(x, y, x + 96, y + 96, P, P + 40, "stoneside", "stonetop", 96);

        // --- Cottages around the green.
        b.Cottage(2400, 2560, 288, 256, 160, "thatch", true);
        b.Cottage(3680, 2560, 256, 320, 160, "thatch", false);
        b.Cottage(2448, 1360, 256, 256, 144, "thatch", true);
        b.Cottage(3728, 1360, 288, 224, 160, "thatch", true);
        b.Cottage(2480, 3280, 320, 288, 176, "thatch", true);
        // Fences along the paths.
        b.Fence(2944, 2448, 3088, 2448); b.Fence(3312, 2448, 3456, 2448);
        b.Fence(3536, 2160, 3536, 2400);
        // A well on the green (stone ring as a low box with a water top you can stand on; only the pond is deadly).
        b.Box(3584, 2160, 3680, 2256, G, G + 48, "stoneside", "wellwater", 96);

        // --- Castle (north): a ring of thick walls (walkable tops at z 900) around a courtyard, a gate in the
        // south wall, a ramp up to the west rampart and a keep on the north wall reached by steps on the rampart.
        const float WT = G + 300, Y0 = 3968, Y1 = 4096, CX0 = 2304, CX1 = 4096, CY1 = 5440, IX0 = CX0 + 128, IX1 = CX1 - 128, IY1 = CY1 - 128;
        const float GX0 = 3104, GX1 = 3296, GT = G + 160;
        const float KX0 = 3520, KX1 = 3840, KY0 = 4880, KT = G + 420, PT = WT + 48, SX = KX0 - 144;
        // Outer faces, rising 48 above the walkways as a parapet (the keep and its steps stand in the north wall),
        // then the courtyard faces.
        b.Wall(CX0, Y0, GX0, Y0, G, PT, "castle", 240, 348); b.Wall(GX1, Y0, CX1, Y0, G, PT, "castle", 240, 348); b.Wall(GX0, Y0, GX1, Y0, GT, PT, "castleplain", 192, 188);
        b.Wall(CX1, Y0, CX1, CY1, G, PT, "castle", 240, 348); b.Wall(CX0, CY1, CX0, Y0, G, PT, "castle", 240, 348);
        b.Wall(CX1, CY1, KX1, CY1, G, PT, "castle", 240, 348); b.Wall(KX1, CY1, KX0, CY1, G, WT, "castle"); b.Wall(KX0, CY1, SX, CY1, G, WT, "castle");
        b.Wall(SX, CY1, CX0, CY1, G, PT, "castle", 240, 348);
        // The parapet's inner faces, seen from the walkways.
        b.Wall(CX1, Y0, CX0, Y0, WT, PT, "castleplain", 600, 48); b.Wall(CX0, Y0, CX0, CY1, WT, PT, "castleplain", 600, 48);
        b.Wall(CX0, CY1, SX, CY1, WT, PT, "castleplain", 600, 48); b.Wall(KX1, CY1, CX1, CY1, WT, PT, "castleplain", 600, 48);
        b.Wall(CX1, CY1, CX1, Y0, WT, PT, "castleplain", 600, 48);
        b.Wall(IX1, Y1, GX1, Y1, G, WT, "castle"); b.Wall(GX0, Y1, IX0, Y1, G, WT, "castle"); b.Wall(GX1, Y1, GX0, Y1, GT, WT, "castleplain", 192);
        b.Wall(IX0, Y1, IX0, IY1, G, WT, "castle"); b.Wall(IX0, IY1, KX0, IY1, G, WT, "castle"); b.Wall(KX1, IY1, IX1, IY1, G, WT, "castle");
        b.Wall(IX1, IY1, IX1, Y1, G, WT, "castle");
        // Gate passage: sides, ceiling, floor.
        b.Wall(GX0, Y0, GX0, Y1, G, GT, "castle", 128); b.Wall(GX1, Y1, GX1, Y0, G, GT, "castle", 128);
        b.Quad([new(GX0, Y1, GT), new(GX1, Y1, GT), new(GX1, Y0, GT), new(GX0, Y0, GT)], "castleplain");
        b.Floor(GX0, Y0, GX1, Y1, G, "courtyard", 192);
        // Walkways.
        b.Floor(CX0, Y0, CX1, Y1, WT, "castlewalk", 300); b.Floor(CX0, Y1, IX0, CY1, WT, "castlewalk", 300); b.Floor(IX1, Y1, CX1, CY1, WT, "castlewalk", 300);
        b.Floor(IX0, IY1, SX, CY1, WT, "castlewalk"); b.Floor(KX1, IY1, IX1, CY1, WT, "castlewalk");
        // Arrow slits on the front face (decals just in front of the wall).
        foreach (float x in new[] { 2560f, 2880f, 3520f, 3840f }) b.Wall(x, Y0 - 2, x + 96, Y0 - 2, G + 120, G + 216, "slit", 96, 96);
        // Courtyard.
        b.Floor(IX0, Y1, IX1, IY1, G, "courtyard", 320);
        // Ramp up to the west rampart (inside, along the west wall): 600 -> 900 over 1120, its open side facing east.
        b.Ramp(IX0, Y1 + 96, IX0 + 192, IY1, G, WT, "planks", tile: 280);
        b.SlopedWall(IX0 + 192, Y1 + 96, IX0 + 192, IY1, G, G, WT, "fence", 280);
        // Keep: a tower standing in the north wall, 120 above the walkways; three steps on the walkway west of it.
        b.Wall(KX0, KY0, KX1, KY0, G, KT, "castle", 320, 420); b.Wall(KX1, KY0, KX1, IY1, G, KT, "castle", 320, 420); b.Wall(KX0, IY1, KX0, KY0, G, KT, "castle", 320, 420);
        b.Wall(KX1, IY1, KX1, CY1, WT, KT, "castleplain", 128); b.Wall(KX0, CY1, KX0, IY1, WT, KT, "castleplain", 128); b.Wall(KX1, CY1, KX0, CY1, WT, KT, "castleplain", 320);
        b.Floor(KX0, KY0, KX1, CY1, KT, "castlewalk", 320);
        for (int k = 0; k < 3; k++)
        {
            float sx0 = SX + 48 * k, sx1 = sx0 + 48, top = WT + 40 * (k + 1), below = WT + 40 * k;
            b.Wall(sx0, IY1, sx1, IY1, WT, top, "castleplain", 48); b.Wall(sx1, CY1, sx0, CY1, WT, top, "castleplain", 48);
            b.Wall(sx0, CY1, sx0, IY1, below, top, "castleplain", 128); b.Floor(sx0, IY1, sx1, CY1, top, "castlewalk", 128);
        }
        // Courtyard well of hay and a stable.
        b.Box(2560, 4320, 2752, 4448, G, G + 40, "thatch", "thatch", 192);
        b.Box(2576, 4800, 2896, 5104, G, G + 176, "barn", null, 320);
        b.Gable(2576, 4800, 2896, 5104, G + 176, G + 320, "thatch", "planks", false);

        // --- Windmill hill (north-east): a long grass ramp from the market rises 600 -> 760, hilltop plateau.
        const float H = G + 160;
        b.Ramp(4400, 3200, 6000, 3680, G, H, "grass");
        b.Floor(4400, 3680, 6000, 5600, H, "grass");
        b.Wall(4400, 5600, 4400, 3680, G, H, "hillwall");                    // hill's west face (seen from the castle side)
        b.SlopedWall(4400, 3680, 4400, 3200, G, H, G, "hillwall");           // and the ramp's side
        b.Floor(4096, 3968, 4400, 5440, G, "grass");                         // strip between castle and hill
        // Windmill tower: stone with a thatch cap, a door on the south.
        b.Box(5120, 4480, 5376, 4736, H, H + 384, "windmill", "thatch", 128);
        b.Wall(5200, 4478, 5296, 4478, H, H + 112, "door", 96, 112);
        // Sails: two crossed wooden boards on the south face (decor).
        b.Wall(5072, 4470, 5424, 4470, H + 300, H + 340, "planks", 352, 40);
        b.Wall(5228, 4470, 5268, 4470, H + 140, H + 500, "planks", 40, 360);

        // --- Outer wall: inward faces around 1200..6000 x 1200..5600, 320 high, plus the north strip behind the castle.
        float OX0 = 1200, OY0 = 1200, OX1 = 6000, OY1 = 5600;
        b.Floor(OX0, 3968, CX0, OY1, G, "grass"); b.Floor(CX0, CY1, 4400, OY1, G, "grass");
        // The north wall behind the castle is taller: from the keep Spyro could otherwise glide over it.
        b.Wall(OX0, OY0, OX0, OY1, G, G + 320, "castle"); b.Wall(OX0, OY1, CX0, OY1, G, G + 320, "castle"); b.Wall(CX0, OY1, 4400, OY1, G, G + 600, "castle");
        b.Wall(4400, OY1, OX1, OY1, H, H + 320, "castle"); b.Wall(OX1, OY1, OX1, 3680, H, H + 320, "castle");
        b.Wall(OX1, 3680, OX1, 3200, G, H + 320, "castle"); b.Wall(OX1, 3200, OX1, OY0, G, G + 320, "castle");
        b.Wall(OX1, OY0, OX0, OY0, G, G + 320, "castle");
        return b.S;
    }
}
