using System.Buffers.Binary;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Finds a place for the way into the new world by itself (the Fix It For Me repair of Create BIN's "…needs the
/// way into your world there"): in one of the game's own homeworlds, level ground that Spyro can walk to from where he
/// arrives, with room for the doorway's frame, for Spyro to walk in and to come back out in front (the game sets him down
/// up to some 770 units in front of a doorway), clear of walls, things overhead, water, the homeworld's objects and its own
/// doorways. The best place comes first: near where Spyro arrives, its front turned towards him.</summary>
public static class WorldEntranceSpots
{
    /// <param name="Position">The bottom middle of the opening (whole editor units).</param>
    /// <param name="Heading">The way Spyro walks in, 0-255.</param>
    /// <param name="FromArrival">Units from where Spyro arrives to the ground in front of the doorway.</param>
    /// <param name="Tightness">0: all the room asked for; 1-3: less room in front and beside (still what the game needs).</param>
    public sealed record Spot(Vector3f Position, int Heading, double FromArrival, int Tightness);

    /// <summary>The room a doorway is given at each tightness: beside the frame, in front, behind, how level the ground under
    /// the frame and in the rest of the walk must be.</summary>
    /// <param name="Others">How far in front of the homeworld's own doorways the new one keeps clear: all the way Spyro
    /// comes out of them, or (the tightest, for a homeworld as small as Gnasty's World, whose own doorways' ways out
    /// cross in the middle) their doorsteps only.</param>
    private sealed record Room(float Beside, float Front, float Behind, float FootRise, float GroundRise, float Others);
    private static readonly Room[] Rooms = [new(70, 880, 250, 8, 20, WayOut), new(45, 860, 220, 12, 28, WayOut), new(30, 840, 200, 16, 36, WayOut), new(24, 840, 190, 16, 40, 360)];
    /// <summary>Spyro gets up a ledge of 64 units with a jump (measured) and down any drop a homeworld has.</summary>
    private const float Step = 64, Sample = 40, Headroom = 90, ClimbRise = 64, Drop = 400, Knee = 34;
    /// <summary>The doorstep's depth in front of the doorway, half the width of the path beyond it, and how far that
    /// path may lead down.</summary>
    private const float Doorstep = 300, Path = 60, PathDrop = 200;
    /// <summary>How far in front of a doorway the game sets Spyro down at most (763 units measured in Artisans, less
    /// elsewhere), with a margin; and what a place costs for each step of tightness (as much as being that many units
    /// further from where Spyro arrives).</summary>
    private const float WayOut = 880, TightnessCost = 600;
    private const int MostChecks = 20000;

    /// <summary>Where Spyro arrives in a homeworld (editor units, on the floor): by balloon (the game's landing table), in
    /// Artisans in front of the first doorway, where a new game starts.</summary>
    public static Vector3f Arrival(UniversalSourceLibrary originals, LevelCatalog catalog, string homeworldKey)
    {
        var level = catalog.FindByKey(homeworldKey) ?? throw new InvalidDataException("The level catalog has no " + homeworldKey + ".");
        if (string.Equals(homeworldKey, "artisans", StringComparison.OrdinalIgnoreCase))
        {
            var t = NativeWorldEntrance.Template(originals, catalog, homeworldKey);
            var (bx, by, bz) = t.BottomMiddle;
            return new((float)(bx / 16 - Math.Cos(t.WalkInAngle) * 500), (float)(by / 16 - Math.Sin(t.WalkInAngle) * 500), (float)(bz / 16));
        }
        byte[] exe = originals.Disc.Exe;
        int world = level.LevelId / 10 - 1;
        int At(uint address) => BinaryPrimitives.ReadInt32LittleEndian(exe.AsSpan(checked((int)(address - UniversalExeBindings.ExeBase))));
        return new(At(NativeBalloonTravel.StepOffTable + 8 * (uint)world) / 16f, At(NativeBalloonTravel.StepOffTable + 8 * (uint)world + 4) / 16f,
            At(NativeBalloonTravel.LandingTable + 16 * (uint)world + 8) / 16f - NativeBalloonTravel.LandingAbove);
    }

    /// <summary>Places for a doorway of <paramref name="shape"/> in the game's own <paramref name="homeworldKey"/>, best
    /// first, at least 400 units apart (empty: the homeworld has no such ground).</summary>
    public static IReadOnlyList<Spot> Find(UniversalSourceLibrary originals, LevelCatalog catalog, string homeworldKey, DoorwayShape shape,
        double scale = 1, int most = 6)
    {
        if (!NewWorldDocument.HomeworldKeys.Contains(homeworldKey)) throw new InvalidDataException("The way into a new world goes in one of the game's six homeworlds.");
        var level = catalog.FindByKey(homeworldKey) ?? throw new InvalidDataException("The level catalog has no " + homeworldKey + ".");
        var data = originals.Get(homeworldKey).Data;
        var ground = new Ground(NativeWorldEntrance.CollisionTriangles(data, level.DisplayName));

        // The frame's own size, from the shape standing at the origin and walked into towards +X.
        var frame = DoorwayShapes.Frame(shape, scale, new Vector3f(0, 0, 0), 0).SelectMany(f => f.Corners).ToArray();
        float frameHalf = frame.Max(c => MathF.Abs(c.Y)), frameTop = frame.Max(c => c.Z);

        // What stands in the homeworld: its objects (gems only matter under the frame) and its own doorways' walks.
        var things = new List<(float X, float Y, float Z, bool Small)>();
        int rows = data.Scene().RowCount;
        for (int n = 0; n < rows; n++)
        {
            byte[] row = data.Row(n);
            int cls = BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(0x36));
            if (cls == 0xFFFF) continue;
            things.Add((BinaryPrimitives.ReadInt32LittleEndian(row.AsSpan(0x0C)) / 16f, BinaryPrimitives.ReadInt32LittleEndian(row.AsSpan(0x10)) / 16f,
                BinaryPrimitives.ReadInt32LittleEndian(row.AsSpan(0x14)) / 16f, cls is >= 0x53 and <= 0x57));
        }
        var doorways = NativeHubPortals.Templates(originals.ImagePath, catalog, homeworldKey).Values
            .Select(t => (X: (float)(t.BottomMiddle.X / 16), Y: (float)(t.BottomMiddle.Y / 16), Dx: (float)Math.Cos(t.WalkInAngle), Dy: (float)Math.Sin(t.WalkInAngle))).ToArray();

        // Where Spyro arrives, on the floor; and every place he can walk to from there.
        Vector3f arrival = Arrival(originals, catalog, homeworldKey);
        if (!ground.Floor(arrival.X, arrival.Y, arrival.Z + 200, out float az, out _, out _))
        {
            var near = ground.Standable().OrderBy(p => Sq(p.X - arrival.X) + Sq(p.Y - arrival.Y) + Sq(p.Z - arrival.Z)).FirstOrDefault();
            arrival = new(near.X, near.Y, near.Z); az = near.Z;
        }
        arrival = new(MathF.Round(arrival.X / Step) * Step, MathF.Round(arrival.Y / Step) * Step, az);
        if (!ground.Floor(arrival.X, arrival.Y, az + 60, out az, out _, out _)) return [];
        arrival = new(arrival.X, arrival.Y, az);
        things.Add((arrival.X, arrival.Y, arrival.Z, false));
        // He also reaches every doorway's doorstep (homeworlds are islands he glides between).
        var starts = new List<Vector3f> { arrival };
        foreach (var t in NativeHubPortals.Templates(originals.ImagePath, catalog, homeworldKey).Values)
        {
            float sx = MathF.Round((float)(t.BottomMiddle.X / 16 - Math.Cos(t.WalkInAngle) * 300) / Step) * Step, sy = MathF.Round((float)(t.BottomMiddle.Y / 16 - Math.Sin(t.WalkInAngle) * 300) / Step) * Step;
            if (ground.Floor(sx, sy, (float)(t.BottomMiddle.Z / 16) + 120, out float sz, out bool special, out _) && !special) starts.Add(new(sx, sy, sz));
        }
        var reached = Walk(ground, starts);

        // Every reached place in sixteen turns, the likeliest first: near the arrival (not on top of it), on its level,
        // the doorway's front towards it.
        var candidates = new List<(float X, float Y, float Z, int Heading, double Cost, double From)>();
        foreach (var (x, y, z) in reached)
            for (int heading = 0; heading < 256; heading += 16)
            {
                double a = heading * Math.PI / 128, dx = Math.Cos(a), dy = Math.Sin(a);
                double fx = x - dx * 350, fy = y - dy * 350, from = Math.Sqrt(Sq(fx - arrival.X) + Sq(fy - arrival.Y));
                double seen = Math.Atan2(y - arrival.Y, x - arrival.X), off = Math.Abs(Math.IEEERemainder(seen - a, 2 * Math.PI)) * 180 / Math.PI;
                double cost = Math.Max(0, 500 - from) * 3 + Math.Max(0, from - 500) + off * 4 + Math.Abs(z - arrival.Z) * 2;
                candidates.Add((x, y, z, heading, cost, from));
            }
        candidates.Sort((p, q) => p.Cost != q.Cost ? p.Cost.CompareTo(q.Cost) : p.X != q.X ? p.X.CompareTo(q.X) : p.Y != q.Y ? p.Y.CompareTo(q.Y) : p.Heading.CompareTo(q.Heading));

        // The best places: each candidate at the roomiest tightness it has (the tightest asks least, so a candidate that
        // fails it has no room at all); a tighter fit costs as much as a longer walk.
        var fits = new List<(Spot Spot, double Cost)>();
        double worstKept = double.MaxValue;
        int checks = 0;
        foreach (var c in candidates)
        {
            if (c.Cost >= worstKept || checks >= MostChecks) break;
            if (fits.Any(f => f.Cost <= c.Cost && Sq(f.Spot.Position.X - c.X) + Sq(f.Spot.Position.Y - c.Y) < 400 * 400)) continue;
            checks++;
            bool Has(int tightness) => Fits(ground, Rooms[tightness], MathF.Max(frameHalf + Rooms[tightness].Beside, tightness == 0 ? 230 : 0), frameHalf,
                frameTop + Headroom, c.X, c.Y, c.Z, c.Heading, things, doorways);
            if (!Has(Rooms.Length - 1)) continue;
            int tightness = Rooms.Length - 1;
            for (int t = 0; t < Rooms.Length - 1; t++) if (Has(t)) { tightness = t; break; }
            fits.Add((new(new Vector3f(c.X, c.Y, MathF.Round(c.Z)), c.Heading, c.From, tightness), c.Cost + tightness * TightnessCost));
            var kept = Apart(fits, most);
            if (kept.Count >= most) worstKept = kept[^1].Cost;
        }
        return [.. Apart(fits, most).Select(f => f.Spot)];
    }

    /// <summary>The cheapest of <paramref name="fits"/> that stand at least 400 units apart.</summary>
    private static List<(Spot Spot, double Cost)> Apart(List<(Spot Spot, double Cost)> fits, int most)
    {
        var kept = new List<(Spot Spot, double Cost)>();
        foreach (var f in fits.OrderBy(f => f.Cost))
        {
            if (kept.Count >= most) break;
            if (kept.All(k => Sq(k.Spot.Position.X - f.Spot.Position.X) + Sq(k.Spot.Position.Y - f.Spot.Position.Y) >= 400 * 400)) kept.Add(f);
        }
        return kept;
    }

    private static double Sq(double v) => v * v;

    /// <summary>Whether a doorway at (x, y, z) walked into along <paramref name="heading"/> has its room (t: along the walk
    /// in, negative in front of the doorway; u: across).
    /// <list type="bullet">
    /// <item>Under the frame the ground is level and plain.</item>
    /// <item>The doorstep (<see cref="Doorstep"/> units in front to <see cref="Room.Behind"/> behind, the frame's width and
    /// <see cref="Room.Beside"/> more) is plain, nearly level ground with no wall through it, nothing overhead, no object
    /// and none of the homeworld's own doorways' walks.</item>
    /// <item>Further in front, as far as the game sets Spyro down (<see cref="Room.Front"/>), a path as wide as Spyro has
    /// plain ground that may slope or step down but not up, and no wall at his height.</item>
    /// </list></summary>
    private static bool Fits(Ground ground, Room room, float half, float frameHalf, float top, float x, float y, float z, int heading,
        List<(float X, float Y, float Z, bool Small)> things, (float X, float Y, float Dx, float Dy)[] doorways)
    {
        double a = heading * Math.PI / 128;
        float dx = (float)Math.Cos(a), dy = (float)Math.Sin(a);
        for (float t = -room.Front; t <= room.Behind + 0.5f; t += Sample)
        {
            bool doorstep = t >= -Doorstep;
            float wide = doorstep ? half : Path, before = float.NaN;
            for (float u = -wide; u <= wide + 0.5f; u += Sample)
            {
                float px = x + dx * t - dy * u, py = y + dy * t + dx * u;
                if (px < 64 || py < 64 || px > 16383 - 64 || py > 16383 - 64) return false;
                if (!ground.Floor(px, py, z + 40, out float fz, out bool special, out float up) || special) return false;
                if (doorstep)
                {
                    bool foot = MathF.Abs(u) <= frameHalf + 20 && MathF.Abs(t) <= 60;
                    if (up < 0.93f || MathF.Abs(fz - z) > (foot ? room.FootRise : room.GroundRise)) return false;
                }
                else if (up < 0.8f || fz > z + room.GroundRise || fz < z - PathDrop || !float.IsNaN(before) && MathF.Abs(fz - before) > 48) return false;
                before = fz;
                // The homeworld's own doorways: off their doorsteps (at most tightnesses, off all the way Spyro comes out
                // of them), and the frame never in the line he comes out along: a doorway takes him in from behind too.
                bool opening = MathF.Abs(t) <= 130 && MathF.Abs(u) <= frameHalf + 30;
                foreach (var d in doorways)
                {
                    float qx = px - d.X, qy = py - d.Y, dt = qx * d.Dx + qy * d.Dy, du = -qx * d.Dy + qy * d.Dx;
                    if (dt >= -room.Others && dt <= 280 && MathF.Abs(du) <= 260) return false;
                    if (opening && dt >= -WayOut && dt <= 280 && MathF.Abs(du) <= 170) return false;
                }
            }
        }
        // Nothing standing on the doorstep or the path, no gem under the frame.
        foreach (var o in things)
        {
            if (MathF.Abs(o.Z - z) > 600) continue;
            float qx = o.X - x, qy = o.Y - y, t = qx * dx + qy * dy, u = -qx * dy + qy * dx;
            if (o.Small ? MathF.Abs(u) <= frameHalf + 40 && MathF.Abs(t) <= 90
                : t >= -Doorstep - 60 ? t <= room.Behind + 60 && MathF.Abs(u) <= half + 60 : t >= -room.Front - 60 && MathF.Abs(u) <= Path + 80) return false;
        }
        // No wall through the doorstep or across the path, and nothing over the doorstep.
        float reach = MathF.Max(room.Front, room.Behind) + half;
        return !ground.Any(x - reach, y - reach, x + reach, y + reach, (p, q, r) =>
        {
            if (MathF.Max(p.Z, MathF.Max(q.Z, r.Z)) <= z + 26 || MathF.Min(p.Z, MathF.Min(q.Z, r.Z)) >= z + top) return false;
            const int n = 8;
            for (int i = 0; i <= n; i++)
                for (int j = 0; i + j <= n; j++)
                {
                    float b = i / (float)n, c = j / (float)n, w = 1 - b - c;
                    float sx = p.X * w + q.X * b + r.X * c - x, sy = p.Y * w + q.Y * b + r.Y * c - y, sz = p.Z * w + q.Z * b + r.Z * c;
                    if (sz <= z + 26 || sz >= z + top) continue;
                    float t = sx * dx + sy * dy, u = -sx * dy + sy * dx;
                    if (t >= -Doorstep ? t <= room.Behind && MathF.Abs(u) <= half : t >= -room.Front && MathF.Abs(u) <= Path && sz < z + 120) return true;
                }
            return false;
        });
    }

    /// <summary>Every place on the 64-unit grid Spyro can get to from <paramref name="starts"/> on foot: plain ground, no
    /// ledge up of more than a jump between neighbours, no wall between them at knee height above the higher of the two.</summary>
    private static List<(float X, float Y, float Z)> Walk(Ground ground, IReadOnlyList<Vector3f> starts)
    {
        var seen = new HashSet<(int, int, int)>();
        var list = new List<(float X, float Y, float Z)>();
        var queue = new Queue<(float X, float Y, float Z)>();
        void Reach(float x, float y, float z) { if (seen.Add(((int)MathF.Round(x / Step), (int)MathF.Round(y / Step), (int)MathF.Round(z / 32)))) { list.Add((x, y, z)); queue.Enqueue((x, y, z)); } }
        foreach (var start in starts) Reach(start.X, start.Y, start.Z);
        while (queue.Count > 0)
        {
            var (x, y, z) = queue.Dequeue();
            foreach (var (ox, oy) in new[] { (Step, 0f), (-Step, 0f), (0f, Step), (0f, -Step) })
            {
                float nx = x + ox, ny = y + oy;
                if (nx < 0 || ny < 0 || nx > 16383 || ny > 16383) continue;
                if (!ground.Floor(nx, ny, z + ClimbRise + 8, out float nz, out bool special, out float up) || special || up < 0.6f || nz - z > ClimbRise || z - nz > Drop) continue;
                float over = MathF.Max(z, nz) + Knee;
                if (ground.Wall(new(x, y, over), new(nx, ny, over))) continue;
                Reach(nx, ny, nz);
            }
        }
        return list;
    }

    /// <summary>The homeworld's collision triangles in a 128-unit grid.</summary>
    private sealed class Ground
    {
        private const float Cell = 128;
        private readonly (Vector3f A, Vector3f B, Vector3f C, bool Flagged)[] _t;
        private readonly float[] _up;
        private readonly Dictionary<(int, int), List<int>> _cells = [];
        private readonly int[] _stamp;
        private int _round;

        public Ground(IReadOnlyList<(Vector3f A, Vector3f B, Vector3f C, bool Flagged)> triangles)
        {
            _t = [.. triangles]; _up = new float[_t.Length]; _stamp = new int[_t.Length];
            for (int n = 0; n < _t.Length; n++)
            {
                var (a, b, c, _) = _t[n];
                float ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z, vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z;
                float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx, length = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
                _up[n] = length < 1e-3f ? 0 : MathF.Abs(nz) / length;
                if (length < 1e-3f) continue;
                for (int gy = (int)MathF.Floor(MathF.Min(a.Y, MathF.Min(b.Y, c.Y)) / Cell); gy <= (int)MathF.Floor(MathF.Max(a.Y, MathF.Max(b.Y, c.Y)) / Cell); gy++)
                    for (int gx = (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X)) / Cell); gx <= (int)MathF.Floor(MathF.Max(a.X, MathF.Max(b.X, c.X)) / Cell); gx++)
                    {
                        if (!_cells.TryGetValue((gx, gy), out var list)) _cells.Add((gx, gy), list = []);
                        list.Add(n);
                    }
            }
        }

        /// <summary>The highest ground at (x, y) not above <paramref name="from"/>.</summary>
        public bool Floor(float x, float y, float from, out float z, out bool flagged, out float up)
        {
            z = float.MinValue; flagged = false; up = 0;
            if (!_cells.TryGetValue(((int)MathF.Floor(x / Cell), (int)MathF.Floor(y / Cell)), out var list)) return false;
            foreach (int n in list)
            {
                if (_up[n] < 0.5f) continue;
                var (a, b, c, f) = _t[n];
                float d = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
                if (MathF.Abs(d) < 1e-4f) continue;
                float w1 = ((b.Y - c.Y) * (x - c.X) + (c.X - b.X) * (y - c.Y)) / d, w2 = ((c.Y - a.Y) * (x - c.X) + (a.X - c.X) * (y - c.Y)) / d, w3 = 1 - w1 - w2;
                if (w1 < -0.001f || w2 < -0.001f || w3 < -0.001f) continue;
                float h = a.Z * w1 + b.Z * w2 + c.Z * w3;
                if (h > from || h <= z) continue;
                z = h; flagged = f; up = _up[n];
            }
            return z > float.MinValue;
        }

        /// <summary>A point on every piece of plain ground (for the nearest ground to a place).</summary>
        public IEnumerable<(float X, float Y, float Z)> Standable()
        {
            for (int n = 0; n < _t.Length; n++)
                if (_up[n] >= 0.93f && !_t[n].Flagged)
                    yield return ((_t[n].A.X + _t[n].B.X + _t[n].C.X) / 3, (_t[n].A.Y + _t[n].B.Y + _t[n].C.Y) / 3, (_t[n].A.Z + _t[n].B.Z + _t[n].C.Z) / 3);
        }

        /// <summary>Whether <paramref name="test"/> holds for a triangle near the box.</summary>
        public bool Any(float x0, float y0, float x1, float y1, Func<Vector3f, Vector3f, Vector3f, bool> test)
        {
            _round++;
            for (int gy = (int)MathF.Floor(y0 / Cell); gy <= (int)MathF.Floor(y1 / Cell); gy++)
                for (int gx = (int)MathF.Floor(x0 / Cell); gx <= (int)MathF.Floor(x1 / Cell); gx++)
                {
                    if (!_cells.TryGetValue((gx, gy), out var list)) continue;
                    foreach (int n in list)
                    {
                        if (_stamp[n] == _round) continue;
                        _stamp[n] = _round;
                        if (test(_t[n].A, _t[n].B, _t[n].C)) return true;
                    }
                }
            return false;
        }

        /// <summary>Whether a steep triangle stands between two points.</summary>
        public bool Wall(Vector3f p, Vector3f q)
        {
            _round++;
            for (int gy = (int)MathF.Floor(MathF.Min(p.Y, q.Y) / Cell); gy <= (int)MathF.Floor(MathF.Max(p.Y, q.Y) / Cell); gy++)
                for (int gx = (int)MathF.Floor(MathF.Min(p.X, q.X) / Cell); gx <= (int)MathF.Floor(MathF.Max(p.X, q.X) / Cell); gx++)
                {
                    if (!_cells.TryGetValue((gx, gy), out var list)) continue;
                    foreach (int n in list)
                    {
                        if (_stamp[n] == _round || _up[n] >= 0.7f) continue;
                        _stamp[n] = _round;
                        if (Crosses(p, q, _t[n].A, _t[n].B, _t[n].C)) return true;
                    }
                }
            return false;
        }

        private static bool Crosses(Vector3f p, Vector3f q, Vector3f a, Vector3f b, Vector3f c)
        {
            float dx = q.X - p.X, dy = q.Y - p.Y, dz = q.Z - p.Z;
            float e1x = b.X - a.X, e1y = b.Y - a.Y, e1z = b.Z - a.Z, e2x = c.X - a.X, e2y = c.Y - a.Y, e2z = c.Z - a.Z;
            float hx = dy * e2z - dz * e2y, hy = dz * e2x - dx * e2z, hz = dx * e2y - dy * e2x;
            float det = e1x * hx + e1y * hy + e1z * hz;
            if (MathF.Abs(det) < 1e-5f) return false;
            float f = 1 / det, sx = p.X - a.X, sy = p.Y - a.Y, sz = p.Z - a.Z;
            float u = f * (sx * hx + sy * hy + sz * hz);
            if (u < -0.01f || u > 1.01f) return false;
            float qx = sy * e1z - sz * e1y, qy = sz * e1x - sx * e1z, qz = sx * e1y - sy * e1x;
            float v = f * (dx * qx + dy * qy + dz * qz);
            if (v < -0.01f || u + v > 1.01f) return false;
            float t = f * (e2x * qx + e2y * qy + e2z * qz);
            return t >= 0 && t <= 1;
        }
    }
}
