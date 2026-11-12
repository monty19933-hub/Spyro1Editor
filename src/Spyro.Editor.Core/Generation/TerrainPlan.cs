using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Generation;

/// <summary>Smooth gradient noise from the generator's own random numbers (no library functions whose last digits differ
/// between machines), about -1..1.</summary>
internal sealed class TerrainNoise
{
    private readonly byte[] _perm = new byte[512];
    private readonly double[] _gx = new double[256], _gy = new double[256];

    public TerrainNoise(LevelRandom rnd)
    {
        var p = new byte[256];
        for (int i = 0; i < 256; i++) p[i] = (byte)i;
        rnd.Shuffle(p);
        for (int i = 0; i < 512; i++) _perm[i] = p[i & 255];
        for (int i = 0; i < 256; i++)
        {
            double x, y, l;
            do { x = rnd.NextDouble() * 2 - 1; y = rnd.NextDouble() * 2 - 1; l = x * x + y * y; } while (l > 1 || l < 0.01);
            l = Math.Sqrt(l);
            _gx[i] = x / l; _gy[i] = y / l;
        }
    }

    public double At(double x, double y)
    {
        double fx = Math.Floor(x), fy = Math.Floor(y);
        int ix = (int)fx & 255, iy = (int)fy & 255;
        double u = x - fx, v = y - fy;
        double Dot(int cx, int cy, double dx, double dy)
        {
            int g = _perm[_perm[(ix + cx) & 255] + ((iy + cy) & 255)];
            return _gx[g] * dx + _gy[g] * dy;
        }
        double su = u * u * u * (u * (u * 6 - 15) + 10), sv = v * v * v * (v * (v * 6 - 15) + 10);
        double a = Dot(0, 0, u, v), b = Dot(1, 0, u - 1, v), c = Dot(0, 1, u, v - 1), d = Dot(1, 1, u - 1, v - 1);
        double top = a + (b - a) * su, bottom = c + (d - c) * su;
        return Math.Clamp((top + (bottom - top) * sv) * 1.6, -1, 1);
    }

    /// <summary>Three octaves, about -1..1.</summary>
    public double Fbm(double x, double y)
    {
        return (At(x, y) + 0.5 * At(x * 2 + 17.3, y * 2 + 9.1) + 0.25 * At(x * 4 + 3.7, y * 4 + 41.9)) / 1.75;
    }
}

/// <summary>How the land outside the floors Spyro walks on is made: the look of a level's edge.</summary>
internal enum EnclosureKind
{
    /// <summary>Steep grassy banks rounding over into hilltops (Artisans, Stone Hill, Dark Hollow).</summary>
    Bank,
    /// <summary>Upright rock walls with a rim (Dry Canyon, Peace Keepers, Ice Cavern).</summary>
    Cliff,
    /// <summary>Banks running up into jagged peaks (Magic Crafters, Alpine Ridge, Dark Passage).</summary>
    Peaks,
    /// <summary>Open water round the land (Twilight Harbor, Gnorc Cove).</summary>
    Sea,
    /// <summary>Nothing: the land floats (Lofty Castle, Dream Weavers, Tree Tops).</summary>
    Sky,
}

/// <summary>A flat-sided outline (a rock shelf, a plateau, a terrace, a yard): a polygon round a middle that moves with
/// the level, its points relative to that middle.</summary>
internal sealed class TerrainHull
{
    public double X, Y;
    public readonly (double X, double Y)[] Points;

    public TerrainHull(double x, double y, (double X, double Y)[] points) { X = x; Y = y; Points = points; }

    /// <summary>Signed distance to the outline (negative inside).</summary>
    public double Sdf(double x, double y) => Distance(Points, x - X, y - Y);

    public double Radius => Points.Max(p => Math.Sqrt(p.X * p.X + p.Y * p.Y));

    /// <summary>Signed distance from a point to a polygon (any simple polygon; negative inside).</summary>
    public static double Distance((double X, double Y)[] v, double px, double py)
    {
        double d = (px - v[0].X) * (px - v[0].X) + (py - v[0].Y) * (py - v[0].Y), s = 1;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
        {
            double ex = v[j].X - v[i].X, ey = v[j].Y - v[i].Y, wx = px - v[i].X, wy = py - v[i].Y;
            double ee = ex * ex + ey * ey, t = ee <= 0 ? 0 : Math.Clamp((wx * ex + wy * ey) / ee, 0, 1);
            double bx = wx - ex * t, by = wy - ey * t;
            d = Math.Min(d, bx * bx + by * by);
            bool c1 = py >= v[i].Y, c2 = py < v[j].Y, c3 = ex * wy > ey * wx;
            if (c1 && c2 && c3 || !c1 && !c2 && !c3) s = -s;
        }
        return s * Math.Sqrt(d);
    }
}

/// <summary>A piece of floor: a room (a blob of overlapping discs), a path between rooms (a strip along a line), or a
/// part laid over them (a raised terrace, a pond). Where zones overlap the one with the higher priority is the floor;
/// floors of different heights meet in a wall.</summary>
internal sealed class TerrainZone
{
    public int Id;
    /// <summary>room, path, charge, bridge, stone, ledge, lookout, glide ledge, pond, islet (land in a pond), dell,
    /// terrace, rise (a ramp up onto a terrace), mound, mound top, summit.</summary>
    public string Kind = "room";
    public readonly List<(double X, double Y, double R)> Discs = [];
    /// <summary>Square-cornered parts (a castle's courtyards): middle, half sizes along and across the direction
    /// (<c>Cos</c>, <c>Sin</c>), and how round the corners are.</summary>
    public readonly List<(double X, double Y, double HalfX, double HalfY, double Cos, double Sin, double Round)> Boxes = [];
    /// <summary>Flat-sided parts: a room that is a rock shelf, a plateau, a long terrace or a yard.</summary>
    public readonly List<TerrainHull> Hulls = [];
    public readonly List<(double X, double Y)> Spine = [];
    public double HalfWidth;
    /// <summary>Floor height; a path runs from <see cref="Z"/> at its start to <see cref="Z1"/> at its end.</summary>
    public double Z, Z1;
    public int Priority = 1;
    /// <summary>How softly the discs run together.</summary>
    public double Blend = 140;
    /// <summary>How far the outline wanders (units), and the wander's wavelength.</summary>
    public double Wobble = 45, WobbleScale = 330;
    public double NoiseX, NoiseY;
    public SurfaceRole Floor = SurfaceRole.Ground;
    public AuthoredSurfaceKind? Liquid;
    /// <summary>The outline is a line of the mesh (a wall foot, a shore, a path's edge).</summary>
    public bool Crisp;
    /// <summary>The area this zone belongs to (rooms count as areas; paths and parts belong to one).</summary>
    public int Area = -1;
    /// <summary>The floor's own ripple is switched off (liquids, bridges).</summary>
    public bool Flat;
    /// <summary>Under a rock roof this far above the floor (0: open sky).</summary>
    public double Roof;
    public bool IsPath => Spine.Count > 0;

    public (double X, double Y) Centre => IsPath
        ? (Spine.Average(p => p.X), Spine.Average(p => p.Y))
        : Hulls.Count > 0 ? (Hulls[0].X, Hulls[0].Y) : Discs.Count > 0 ? (Discs[0].X, Discs[0].Y) : (Boxes[0].X, Boxes[0].Y);

    public double Sdf(double x, double y, TerrainNoise noise)
    {
        double d;
        if (IsPath) d = SpineDistance(x, y, out _) - HalfWidth;
        else
        {
            d = double.MaxValue;
            foreach (var hull in Hulls) d = TerrainPlan.SMin(d, hull.Sdf(x, y), Blend);
            foreach (var (cx, cy, r) in Discs)
            {
                double dx = x - cx, dy = y - cy;
                d = TerrainPlan.SMin(d, Math.Sqrt(dx * dx + dy * dy) - r, Blend);
            }
            foreach (var (cx, cy, hx, hy, c, s, round) in Boxes)
            {
                double dx = x - cx, dy = y - cy, u = Math.Abs(dx * c + dy * s) - (hx - round), v = Math.Abs(-dx * s + dy * c) - (hy - round);
                double outside = Math.Sqrt(Math.Max(u, 0) * Math.Max(u, 0) + Math.Max(v, 0) * Math.Max(v, 0));
                d = TerrainPlan.SMin(d, outside + Math.Min(Math.Max(u, v), 0) - round, Blend);
            }
        }
        if (Wobble > 0) d += Wobble * noise.At(x / WobbleScale + NoiseX, y / WobbleScale + NoiseY);
        return d;
    }

    /// <summary>Distance to the path's line, and how far along it the nearest point is (0-1).</summary>
    public double SpineDistance(double x, double y, out double along)
    {
        double best = double.MaxValue, total = 0, at = 0, run = 0;
        for (int k = 0; k + 1 < Spine.Count; k++)
        {
            double ax = Spine[k].X, ay = Spine[k].Y, bx = Spine[k + 1].X, by = Spine[k + 1].Y;
            double sx = bx - ax, sy = by - ay, len2 = sx * sx + sy * sy, len = Math.Sqrt(len2);
            double t = len2 <= 0 ? 0 : Math.Clamp(((x - ax) * sx + (y - ay) * sy) / len2, 0, 1);
            double px = ax + sx * t - x, py = ay + sy * t - y, d = Math.Sqrt(px * px + py * py);
            if (d < best) { best = d; at = run + len * t; }
            run += len;
        }
        total = run;
        along = total <= 0 ? 0 : at / total;
        return best;
    }

    /// <summary>The radius of the biggest round or square part.</summary>
    public double Radius => Math.Max(Math.Max(Discs.Count > 0 ? Discs.Max(d => d.R) : 0, Boxes.Count > 0 ? Boxes.Max(b => Math.Max(b.HalfX, b.HalfY)) : 0),
        Hulls.Count > 0 ? Hulls.Max(h => h.Radius) : 0);

    public double Length
    {
        get
        {
            double run = 0;
            for (int k = 0; k + 1 < Spine.Count; k++)
                run += Math.Sqrt((Spine[k + 1].X - Spine[k].X) * (Spine[k + 1].X - Spine[k].X) + (Spine[k + 1].Y - Spine[k].Y) * (Spine[k + 1].Y - Spine[k].Y));
            return run;
        }
    }

    /// <summary>The share of a path, at each end, that stays level before the ramp starts.</summary>
    public double RampFrom = 0.14, RampTo = 0.86;

    /// <summary>The floor's height before the level's ripple: a room is level, a path ramps between its ends.</summary>
    public double FloorAt(double x, double y)
    {
        if (!IsPath || Z == Z1) return Z;
        SpineDistance(x, y, out double t);
        return Z + (Z1 - Z) * Ramp((t - RampFrom) / (RampTo - RampFrom));
    }

    /// <summary>0-1 for 0-1, straight in the middle and eased over the first and last 15%: a ramp without a sharp edge
    /// whose steepest part is only 1.18 times its average.</summary>
    public static double Ramp(double e)
    {
        const double a = 0.15;
        e = Math.Clamp(e, 0, 1);
        double peak = 1 / (1 - a);
        if (e < a) return peak * e * e / (2 * a);
        if (e > 1 - a) { double r = 1 - e; return 1 - peak * r * r / (2 * a); }
        return peak * (e - a / 2);
    }
}

/// <summary>The level as a field: floors (<see cref="TerrainZone"/>), what encloses them, and the height, surface and light
/// of every point. <see cref="TerrainMesher"/> turns it into faces.</summary>
internal sealed class TerrainPlan
{
    /// <summary>Layers that are not zones: the enclosure outside the floors, open water, nothing, and floor that fills
    /// the rounded corners where zones meet.</summary>
    public const int Enclosure = -1, Sea = -2, None = -3, Fill = -4;

    public readonly List<TerrainZone> Zones = [];
    /// <summary>Strips painted over the floors they cross (trodden paths): a surface, not a floor of their own.</summary>
    public readonly List<TerrainZone> Decals = [];
    public required TerrainNoise Noise;
    public EnclosureKind Kind = EnclosureKind.Bank;
    /// <summary>How high the enclosure stands over the floor, how far out it reaches that height (banks), how far the top
    /// runs on behind it, and how rough it is.</summary>
    public double Rise = 260, BankWidth = 270, CapWidth = 240, Rough = 26, Spikes;
    /// <summary>How much of the banks' steep foot is grass rather than bare rock (-1 none, 1 all).</summary>
    public double RockShare = -0.1;
    /// <summary>Water level and how far the water runs out from the land (<see cref="EnclosureKind.Sea"/>).</summary>
    public double SeaLevel, SeaWidth = 900;
    /// <summary>Land meets the water in a beach this wide (0: a quay wall).</summary>
    public double Beach = 170;
    /// <summary>The floors' ripple (units) and its wavelength.</summary>
    public double Ripple = 14, RippleScale = 640;
    /// <summary>How softly zones run together at the enclosure's foot.</summary>
    public double Fillet = 60;
    public SurfaceRole LiquidRole = SurfaceRole.Water;
    public AuthoredSurfaceKind LiquidKind = AuthoredSurfaceKind.Water;

    public static double SMin(double a, double b, double k)
    {
        if (k <= 0) return Math.Min(a, b);
        double h = Math.Max(k - Math.Abs(a - b), 0) / k;
        return Math.Min(a, b) - h * h * k * 0.25;
    }
    public static double Smooth(double t) { t = Math.Clamp(t, 0, 1); return t * t * (3 - 2 * t); }

    public TerrainZone Add(TerrainZone z)
    {
        z.Id = Zones.Count;
        z.NoiseX = 13.7 * (z.Id + 1); z.NoiseY = 7.3 * (z.Id + 1);
        Zones.Add(z);
        return z;
    }

    /// <summary>Distance to the land Spyro walks on (negative inside): every solid zone, run together softly.</summary>
    public double Land(double x, double y)
    {
        double d = double.MaxValue;
        foreach (var z in Zones)
        {
            if (z.Kind == "pond") continue;
            d = SMin(d, z.Sdf(x, y, Noise), Fillet);
        }
        return d;
    }

    /// <summary>The zone whose floor a point is on (the highest priority, then the deepest inside), or -1.</summary>
    public int ZoneAt(double x, double y)
    {
        int best = -1; double bestD = 0; int bestP = int.MinValue;
        foreach (var z in Zones)
        {
            double d = z.Sdf(x, y, Noise);
            if (d >= 0) continue;
            if (z.Priority > bestP || z.Priority == bestP && d < bestD) { best = z.Id; bestD = d; bestP = z.Priority; }
        }
        return best;
    }

    public double TrimDistance => Kind switch
    {
        EnclosureKind.Bank or EnclosureKind.Peaks => BankWidth + CapWidth,
        EnclosureKind.Cliff => CapWidth,
        EnclosureKind.Sea => SeaWidth,
        _ => 0,
    };

    /// <summary>What a point is: a zone's floor, fill, the enclosure, water or nothing.</summary>
    public int Classify(double x, double y)
    {
        int zone = ZoneAt(x, y);
        if (zone >= 0) return zone;
        double d = Land(x, y);
        if (d <= 0) return Fill;
        return Kind switch
        {
            EnclosureKind.Sky => None,
            EnclosureKind.Sea => d < SeaWidth ? Sea : None,
            _ => d < TrimDistance ? Enclosure : None,
        };
    }

    /// <summary>The floor height all round a point: the zones' floors weighted by nearness (a zone's own floor inside it).</summary>
    public double Base(double x, double y)
    {
        double sum = 0, weights = 0;
        foreach (var z in Zones)
        {
            // Water, stepping stones, bridges and what is built on the floor (houses, castle walls) lie on the land; they
            // do not shape it.
            if (z.Kind is "pond" or "stone" or "bridge" or "house" or "rampart") continue;
            double d = Math.Max(z.Sdf(x, y, Noise), 0) + 40;
            double w = 1 / (d * d * d * d);
            sum += w * z.FloorAt(x, y); weights += w;
        }
        return weights <= 0 ? 0 : sum / weights;
    }

    private double RippleAt(double x, double y) => Ripple * Noise.Fbm(x / RippleScale + 31, y / RippleScale + 77);

    /// <summary>How much higher than usual the enclosure stands at a point: where a high place stands near (a hilltop,
    /// a terrace, a room a storey up), high enough that a jump and glide from it does not carry Spyro over the top and
    /// out of the level. <paramref name="floor"/>: the floor height all round the point (<see cref="Base"/>).</summary>
    public double Lift(double x, double y, double floor)
    {
        double lift = 0;
        foreach (var z in Zones)
        {
            if (z.Liquid != null || z.IsPath || z.Kind is "stone" or "house") continue;
            double high = z.Z - floor;
            if (high <= 60) continue;
            // A held jump's rise, a margin, less what a glide sinks on the way from the high place's edge.
            lift = Math.Max(lift, high + 81 + 40 - Math.Max(0, z.Sdf(x, y, Noise) - 100) / 3.3 - Rise);
        }
        return lift;
    }

    /// <summary>The enclosure's height over the floor <paramref name="d"/> units outside it.</summary>
    /// <param name="lift">Added to the enclosure's usual rise (<see cref="Lift"/>).</param>
    public double Profile(double d, double x, double y, double lift = 0)
    {
        if (d <= 0) return 0;
        double rough = Rough * Noise.Fbm(x / 240 + 3, y / 240 + 8), Rise = this.Rise + lift;
        switch (Kind)
        {
            case EnclosureKind.Cliff:
                return Rise + rough + 30 * Math.Min(1, d / Math.Max(1, CapWidth));
            case EnclosureKind.Peaks:
            {
                double s = Smooth(d / BankWidth);
                double ridge = 1 - Math.Abs(Noise.At(x / 210 + 50, y / 210 + 20));
                return Rise * s + rough * s + Spikes * ridge * ridge * Smooth((d - BankWidth * 0.55) / (BankWidth * 0.6));
            }
            default:
            {
                // Steep from the floor's edge (the lower band, often bare rock), a gentler shoulder rounding over to the
                // top; behind the top the hill rolls on and falls away.
                double u = Math.Clamp(d / BankWidth, 0, 1), t = Math.Clamp((d - BankWidth) / Math.Max(1, CapWidth), 0, 1);
                double s = u < BankSplit ? 0.72 * u / BankSplit : 0.72 + 0.28 * (1 - (1 - (u - BankSplit) / (1 - BankSplit)) * (1 - (u - BankSplit) / (1 - BankSplit)));
                return Rise * s + rough * Math.Min(1, d / 140) + 30 * 4 * t * (1 - t) - 70 * t * t;
            }
        }
    }

    /// <summary>The share of a bank's width taken by its steep lower band.</summary>
    public const double BankSplit = 0.45;

    public double Height(int layer, double x, double y)
    {
        switch (layer)
        {
            case Sea: return SeaLevel;
            case None: return 0;
            case Fill: return Shore(Base(x, y) + RippleAt(x, y), x, y);
            // A corner on the land's very edge is still the enclosure's: a cliff stands its full height there.
            case Enclosure:
            {
                double floor = Base(x, y);
                return floor + RippleAt(x, y) + Profile(Math.Max(Land(x, y), 0.01), x, y, Kind is EnclosureKind.Sea or EnclosureKind.Sky ? 0 : Lift(x, y, floor));
            }
        }
        var z = Zones[layer];
        if (z.Liquid != null || z.Flat) return z.FloorAt(x, y);
        double h = z.FloorAt(x, y) + RippleAt(x, y);
        // A pond's shore: the floor round it dips to the water.
        foreach (var pond in Zones)
        {
            if (pond.Kind != "pond" || pond.Area != z.Area || z.Kind == "islet") continue;
            double d = pond.Sdf(x, y, Noise);
            if (d < 90) h += (pond.Z - h) * Smooth(1 - d / 90);
        }
        return z.Kind is "stone" or "bridge" ? h : Shore(h, x, y);
    }

    /// <summary>Beside open water the land runs down to it in a beach.</summary>
    private double Shore(double h, double x, double y)
    {
        if (Kind != EnclosureKind.Sea || Beach <= 0) return h;
        double d = -Land(x, y);
        return d >= Beach ? h : SeaLevel + (h - SeaLevel) * Smooth(d / Beach);
    }

    public AuthoredSurfaceKind? KindOf(int layer) => layer == Sea ? LiquidKind : layer >= 0 ? Zones[layer].Liquid : null;

    /// <summary>The surface of a face: <paramref name="nz"/> is how far it faces up (1 flat, 0 upright).</summary>
    public SurfaceRole Role(int layer, double x, double y, double nz)
    {
        switch (layer)
        {
            case Sea: return LiquidRole;
            case Enclosure:
                if (Kind == EnclosureKind.Cliff) return CapRole;
                if (Kind == EnclosureKind.Peaks && Land(x, y) > BankWidth * 0.6) return nz < 0.8 ? SurfaceRole.Cliff : SurfaceRole.Rock;
                // A bank is bare rock in stretches, grass in others; over its top, grass.
                return Land(x, y) < BankWidth && Noise.At(x / 520 + 70, y / 520 + 33) > RockShare ? SurfaceRole.Cliff : SurfaceRole.GroundAlt;
            case Fill: return Kind == EnclosureKind.Sea && -Land(x, y) < Beach * 0.45 ? SurfaceRole.Sand : SurfaceRole.Ground;
        }
        var z = Zones[layer];
        if (z.Liquid != null) return z.Floor;
        if (Kind == EnclosureKind.Sea && Beach > 0 && z.Kind is "room" or "path" && -Land(x, y) < Beach * 0.45) return SurfaceRole.Sand;
        if (z.Kind is "room" or "path")
            foreach (var decal in Decals)
                if (decal.Sdf(x, y, Noise) < 0) return decal.Floor;
        return z.Floor;
    }

    /// <summary>The surface of a wall between a lower and a higher layer; its top band has the fringe of the floor above.</summary>
    public SurfaceRole WallRole(int low, int high, bool top, double tall)
    {
        if (high >= 0 && Zones[high].Kind == "bridge") return SurfaceRole.Wood;
        // A house's walls and a castle wall are built of the look's stone or brick, top to foot.
        if (high >= 0 && Zones[high].Kind is "house" or "rampart") return SurfaceRole.Brick;
        return top ? WallTop : WallFace;
    }

    /// <summary>The surfaces of walls (their top band and the rest), of the enclosure's top, and of rock ceilings.</summary>
    public SurfaceRole WallTop = SurfaceRole.CliffTop, WallFace = SurfaceRole.Cliff, CapRole = SurfaceRole.Rock;

    /// <summary>How high the rock roof is over a layer's floor at a point (0: open sky): a roof's height over the floor
    /// grows from its edge inwards, so caves are domes and tunnels low.</summary>
    public double RoofAt(int layer, double x, double y)
    {
        if (layer < 0 || Zones[layer].Roof <= 0) return 0;
        double inside = double.MaxValue;
        foreach (var z in Zones)
            if (z.Roof > 0 && z.Kind != "pond") inside = SMin(inside, z.Sdf(x, y, Noise), Fillet);
        return Zones[layer].Roof + 0.42 * Math.Clamp(-inside - 60, 0, 420);
    }

    /// <summary>The point under a floating piece of land its underside runs down to.</summary>
    public (double X, double Y, double Z) Apex(int layer, double x, double y)
    {
        var z = layer >= 0 ? Zones[layer] : Zones.Where(c => c.Kind == "room").MinBy(c => c.Sdf(x, y, Noise))!;
        if (z.Kind is not "room" && z.Area >= 0 && z.Kind is not ("bridge" or "stone" or "path"))
            z = Zones.First(c => c.Kind == "room" && c.Area == z.Area);
        if (z.IsPath) return (x, y, z.FloorAt(x, y) - 40);
        double r = z.Radius;
        return (z.Centre.X, z.Centre.Y, z.Z - (z.Kind == "stone" ? 110 : 150 + 0.55 * r));
    }

    /// <summary>How brightly a point of a layer is lit (0 the level's darkest paint, 1 its lightest): open ground is
    /// bright with slow drifts, the foot of a wall is in shade, banks lighten as they climb.</summary>
    public double Light(int layer, double x, double y)
    {
        double drift = Noise.Fbm(x / 760 + 90, y / 760 + 14);
        switch (layer)
        {
            case Sea:
            {
                double d = Land(x, y);
                return Math.Clamp(0.78 - 0.5 * Smooth(d / 520) + 0.12 * drift, 0, 1);
            }
            case Enclosure:
            {
                double d = Land(x, y), h = Rise <= 0 ? 1 : Math.Clamp(Profile(d, x, y) / Rise, 0, 1.3);
                return Math.Clamp(0.22 + 0.5 * h + 0.14 * drift, 0, 1);
            }
        }
        if (layer >= 0 && Zones[layer].Liquid != null)
        {
            double d = -Zones[layer].Sdf(x, y, Noise);
            return Math.Clamp(0.8 - 0.45 * Smooth(d / 260) + 0.1 * drift, 0, 1);
        }
        double inside = -Land(x, y);
        double shade = Kind is EnclosureKind.Sea or EnclosureKind.Sky ? 0 : 0.3 * (1 - Smooth(inside / 230));
        double roof = layer >= 0 && Zones[layer].Roof > 0 ? 0.3 : 0;
        return Math.Clamp(0.66 + 0.26 * drift - shade - roof, 0, 1);
    }

    /// <summary>The lines the mesh follows: the land's edge, crisp zones' outlines, and the enclosure's far edge.</summary>
    public IEnumerable<(Func<double, double, double> Field, double Weight)> Curves()
    {
        yield return ((x, y) => Land(x, y), 1);
        foreach (var z in Zones)
            if (z.Crisp) { var zone = z; yield return ((x, y) => zone.Sdf(x, y, Noise), 1.1); }
        // A room's outline where a path runs into it: the line where the path's surface ends.
        foreach (var z in Zones)
            if (z.Kind == "room" && !z.Crisp) { var zone = z; yield return ((x, y) => zone.Sdf(x, y, Noise), 2); }
        foreach (var d in Decals) { var decal = d; yield return ((x, y) => decal.Sdf(x, y, Noise), 1.6); }
        if (Kind is EnclosureKind.Bank or EnclosureKind.Peaks)
            yield return ((x, y) => Land(x, y) - BankWidth, 1.8);
        if (Kind is not EnclosureKind.Sky)
            yield return ((x, y) => Land(x, y) - TrimDistance, 3);
    }

    public (double X0, double Y0, double X1, double Y1) Bounds()
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        foreach (var z in Zones)
        {
            foreach (var (cx, cy, r) in z.Discs) { x0 = Math.Min(x0, cx - r); y0 = Math.Min(y0, cy - r); x1 = Math.Max(x1, cx + r); y1 = Math.Max(y1, cy + r); }
            foreach (var b in z.Boxes) { double r = Math.Sqrt(b.HalfX * b.HalfX + b.HalfY * b.HalfY); x0 = Math.Min(x0, b.X - r); y0 = Math.Min(y0, b.Y - r); x1 = Math.Max(x1, b.X + r); y1 = Math.Max(y1, b.Y + r); }
            foreach (var h in z.Hulls)
                foreach (var p in h.Points) { x0 = Math.Min(x0, h.X + p.X); y0 = Math.Min(y0, h.Y + p.Y); x1 = Math.Max(x1, h.X + p.X); y1 = Math.Max(y1, h.Y + p.Y); }
            foreach (var (sx, sy) in z.Spine) { x0 = Math.Min(x0, sx - z.HalfWidth); y0 = Math.Min(y0, sy - z.HalfWidth); x1 = Math.Max(x1, sx + z.HalfWidth); y1 = Math.Max(y1, sy + z.HalfWidth); }
        }
        double m = TrimDistance + 160;
        return (x0 - m, y0 - m, x1 + m, y1 + m);
    }
}
