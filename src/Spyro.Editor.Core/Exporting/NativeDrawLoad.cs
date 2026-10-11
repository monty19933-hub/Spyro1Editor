using System.Buffers.Binary;
using System.Numerics;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Exporting;

/// <summary>The game's per-frame draw buffer and how much of it a level's terrain takes (USA executable).
/// <para>Each frame builds its GPU packets in one of two 0x1C000-byte buffers (0x80187BB0 and 0x801A3BB0, picked at
/// 0x8001ED5C from 0x80076EE0/+0x84 +0x70; the next packet is 0x800757B0, the buffer's end 0x80075780). The first ends
/// where the second begins (the one the GPU is drawing meanwhile) and the second at the ordering table's buckets
/// (0x801BFBB8): running past either end corrupts the GPU's list or the table, a hang or a crash. Objects, Spyro and
/// particles (drawn before the terrain, 0x80019698) and the passes after it stop at the end minus 0x400 (0x8004EE90,
/// 0x80057500); the terrain function (0x800258F0) never checks.</para>
/// <para>The terrain function: a sector pass (0x800258F0..0x800261A0) tests each sector's header sphere against the view
/// (culled when d+r &lt;= 0, 4(|x|-0.78r) &gt;= 3(d+0.59r) or 32(|y|-0.875r) &gt;= 17(d+0.44r); d depth, x/y across, editor
/// units) and lists it far (LP) when d+r+256 &gt; N and near (HP) when d-r &lt; N, N the near distance (0x800785CC in game
/// units: 0x8000, 2,048 units, set once at 0x80012CC4 and read only by these passes; header flags 0x8000 always far,
/// 0x2000 never far, 0x4000 never near). A built level's visibility list holds every sector (retail levels list each
/// area's). The far pass (0x80026200..0x80026788) draws each front-facing far face (NCLIP of corners 3,2,0 &gt; 0 or of
/// 1,2,0 &lt; 0; both sides with face flag 0x80) not wholly off one screen edge whose corners average at least N-256 deep
/// (none at N-512 or less, between when a corner reaches N-256; face bits 0-4 push it 64 units per step) and under
/// 10,240 (0x800785D0 = 0x28000 in play): 0x24 bytes a quad, 0x1C a triangle. The near pass (..0x80027688) draws each
/// front-facing near face (both sides with flag 4, or with a corner by the camera) whose corners average under N deep:
/// whole, 0x34 a textured quad, 0x28 a triangle (0x24 and 0x1C untextured), or, under 512 deep, queued for subdivision
/// (..0x8002A6B8: four pieces from the normal queue, sixteen from the close one, each 0x34 or 0x28).</para>
/// <para>Measured against the emulator (each frame's own camera, Toad Village, Sunny Villa, Seashell Shore: about
/// 1,600 frames) the model's terrain bytes were 0.996-1.04 of the game's on average, within 13% a frame.</para></summary>
public static class NativeDrawLoad
{
    public const int FrameBufferBytes = 0x1C000;
    /// <summary>What the passes drawn after the terrain may need, kept free by the runtime guard (<see cref="NativeDrawBufferGuard"/>).</summary>
    public const int AfterTerrainReserve = 0x6000;
    /// <summary>The terrain an editor-built level may draw whole (far faces and near faces not subdivided) in its
    /// heaviest view: the buffer less the guard's reserve, the objects', Spyro's and particles' share (retail levels drew
    /// up to 44 KB of those in one view) and room for the faces subdivided up close (up to 15 KB in retail views; the
    /// guard counts the queued ones before drawing more). Retail views drew up to 53 KB of terrain.</summary>
    public const int TerrainBudget = 0xD000;
    /// <summary>Terrain drawn whole beyond which the guard would trim it even in a frame with nothing else but Spyro and
    /// the HUD (12 KB): a level whose heaviest view draws more is refused.</summary>
    public const int TerrainLimit = FrameBufferBytes - AfterTerrainReserve - NativeDrawBufferGuard.Slack - 0x3000;
    public const float NearDistance = 2048, FarFaceNear = 1792, FarLimit = 10240;
    private const float H = 341, Ofx = 256, Ofy = 120, AspectY = 0.625f;

    internal const int FlagAlwaysFar = 0x8000, FlagNeverNear = 0x4000, FlagNeverFar = 0x2000;

    /// <summary>One sector as the game reads it.</summary>
    internal sealed class Sector
    {
        internal Vector3 Centre;
        internal float Radius;
        internal int Flags;
        internal int Index;
        // Near faces: corners (4 per face; a triangle repeats its last), the authored corner order.
        internal Vector3[] Near = [];
        internal int[] NearCorners = [];
        internal int[] NearFlags = [];
        internal bool[] NearTextured = [];
        // Far faces.
        internal Vector3[] Far = [];
        internal int[] FarCorners = [];
        internal int[] FarFlags = [];
        /// <summary>The far table's vertex count (the header's byte 16): never 0 (<see cref="NativeAuthoredMeshRenderer"/> Simplify).</summary>
        internal int FarVertices;
        internal int NearFaces => NearCorners.Length;
        internal int FarFaces => FarCorners.Length;
    }

    /// <summary>A camera: eye in editor units, forward, right and screen-down unit vectors.</summary>
    public readonly record struct Pose(Vector3 Eye, Vector3 Forward, Vector3 Right, Vector3 Down)
    {
        /// <summary>Spyro's camera behind a point, looking along a heading (radians, 0 = +x) and pitch (radians, up positive).</summary>
        public static Pose Looking(Vector3 eye, double heading, double pitch)
        {
            var f = new Vector3((float)(Math.Cos(pitch) * Math.Cos(heading)), (float)(Math.Cos(pitch) * Math.Sin(heading)), (float)Math.Sin(pitch));
            var r = Vector3.Normalize(new Vector3((float)Math.Sin(heading), (float)-Math.Cos(heading), 0));
            var down = Vector3.Cross(f, r);
            if (down.Z > 0) down = -down;
            return new(eye, f, r, down);
        }
        /// <summary>The game's camera (0x80076DD0: rotation +0x14 as 3x3 int16, position +0x28 in game units).</summary>
        public static Pose FromGame(short[] m, int gx, int gy, int gz)
        {
            // The sector pass rotates (camy-cy, camz-cz, cx-camx) by the matrix: rows give depth (2), across (0), down (1).
            static Vector3 Row(short[] m, int r) => Vector3.Normalize(new Vector3(m[3 * r + 2], -m[3 * r], -m[3 * r + 1]));
            return new(new Vector3(gx / 16f, gy / 16f, gz / 16f), Row(m, 2), Row(m, 0), Row(m, 1));
        }
    }

    /// <summary>Bytes one frame's terrain writes: far pass, near faces drawn whole, subdivided faces.</summary>
    public readonly record struct Load(int Far, int Near, int Subdivided, int NearQueue, int CloseQueue)
    {
        public int Total => Far + Near + Subdivided;
        /// <summary>The faces drawn whole: what the draw plan can change (subdivision happens within 512 units, bounded by
        /// the queues).</summary>
        public int Whole => Far + Near;
        public static Load operator +(Load a, Load b) => new(a.Far + b.Far, a.Near + b.Near, a.Subdivided + b.Subdivided, a.NearQueue + b.NearQueue, a.CloseQueue + b.CloseQueue);
    }

    /// <summary>The sectors of an environment block ([size][count][count x (sector offset - 4)][sectors], as
    /// <see cref="NativeAuthoredMeshRenderer.Compile"/> writes it and the game loads it).</summary>
    internal static List<Sector> Parse(ReadOnlySpan<byte> env)
    {
        int count = (int)U(env, 4);
        List<Sector> sectors = new(count);
        for (int i = 0; i < count; i++)
        {
            int at = (int)U(env, 8 + 4 * i) + 4;
            var s = env[at..];
            uint w0 = U(s, 0), w1 = U(s, 4), w2 = U(s, 8), w3 = U(s, 12);
            int lv = s[16], lc = s[17], lf = s[18], v = s[20], c = s[21], f = s[22], hpWords = s[23];
            float ox = w2 >> 16, oy = w2 & 0xFFFF, oz = w3 >> 16;
            Vector3 Unpack(uint p) => new(ox + (p >> 21), oy + ((p >> 10) & 0x7FF), oz + (p & 0x3FF));
            var sector = new Sector { Centre = new(w0 >> 16, w0 & 0xFFFF, w1 >> 16), Radius = w1 & 0x1FFF, Flags = (int)(w1 & 0xE000), Index = i, FarVertices = lv };
            // Far tables from 0x1C: vertices, colours, faces (8 bytes: corners at bits 26, 20, 14, 8; flags in the low byte).
            var farV = new Vector3[lv];
            for (int k = 0; k < lv; k++) farV[k] = Unpack(U(s, 28 + 4 * k));
            int lpf = 28 + 4 * lv + 4 * lc;
            List<Vector3> far = []; List<int> farCorners = [], farFlags = [];
            for (int k = 0; k < lf; k++)
            {
                uint w = U(s, lpf + 8 * k);
                int a = (int)(w >> 26) & 63, b = (int)(w >> 20) & 63, cc = (int)(w >> 14) & 63, d = (int)(w >> 8) & 63;
                if (a >= lv || b >= lv || cc >= lv || d >= lv) continue;
                // Authored order: quad (bits 14, 8, 20, 26), triangle (14, 20, 26).
                Vector3[] q = cc == d ? [farV[cc], farV[b], farV[a], farV[a]] : [farV[cc], farV[d], farV[b], farV[a]];
                // Word 1 bit 2: a draw-mode packet (0x0C bytes) goes first (semi-transparent faces).
                far.AddRange(q); farCorners.Add(cc == d ? 3 : 4); farFlags.Add((int)(w & 0xFF) | ((U(s, lpf + 8 * k + 4) & 4) != 0 ? 0x100 : 0));
            }
            sector.Far = [.. far]; sector.FarCorners = [.. farCorners]; sector.FarFlags = [.. farFlags];
            // Near tables at 0x1C + 4 x the header's word offset: vertices, two colour tables, faces (16 bytes).
            int hpv = 28 + 4 * hpWords, hpf = hpv + 4 * v + 8 * c;
            var nearV = new Vector3[v];
            for (int k = 0; k < v; k++) nearV[k] = Unpack(U(s, hpv + 4 * k));
            List<Vector3> near = []; List<int> nearCorners = [], nearFlags = []; List<bool> textured = [];
            for (int k = 0; k < f; k++)
            {
                uint w = U(s, hpf + 16 * k);
                int b3 = (int)(w >> 24), b2 = (int)(w >> 16) & 255, b1 = (int)(w >> 8) & 255, b0 = (int)w & 255;
                if (b3 >= v || b2 >= v || b1 >= v || b0 >= v) continue;
                bool tri = b1 == b0;
                // Authored order: quad bytes 0..3, triangle bytes 1..3; the triangle's depth weights its byte-1 corner twice.
                Vector3[] q = tri ? [nearV[b1], nearV[b2], nearV[b3], nearV[b3]] : [nearV[b0], nearV[b1], nearV[b2], nearV[b3]];
                near.AddRange(q); nearCorners.Add(tri ? 3 : 4);
                nearFlags.Add(s[hpf + 16 * k + 12]); textured.Add(s[hpf + 16 * k + 8] != 0xFF);
            }
            sector.Near = [.. near]; sector.NearCorners = [.. nearCorners]; sector.NearFlags = [.. nearFlags]; sector.NearTextured = [.. textured];
            sectors.Add(sector);
        }
        return sectors;
    }

    /// <summary>The terrain bytes the game writes for one camera, with the level's near distance (editor units: 2,048 in
    /// retail levels; the guard can set a shorter one for a built level).</summary>
    internal static Load Frame(IReadOnlyList<Sector> sectors, in Pose pose, float near = NearDistance)
    {
        Load total = default;
        foreach (var s in sectors) total += SectorLoad(s, pose, near);
        return total;
    }

    internal static Load SectorLoad(Sector s, in Pose p, float near = NearDistance)
    {
        var v = s.Centre - p.Eye;
        float d = Vector3.Dot(v, p.Forward), x = Math.Abs(Vector3.Dot(v, p.Right)), y = Math.Abs(Vector3.Dot(v, p.Down)), r = s.Radius;
        if (d + r <= 0 || 4 * (x - 0.78125f * r) >= 3 * (d + 0.59375f * r) || 32 * (y - 0.875f * r) >= 17 * (d + 0.4375f * r)) return default;
        bool inside = d - r > 0 && 4 * (x + 0.78125f * r) < 3 * (d - 0.59375f * r) && 32 * (y + 0.875f * r) < 17 * (d - 0.4375f * r);
        Load load = default;
        if ((s.Flags & FlagNeverFar) == 0 && ((s.Flags & FlagAlwaysFar) != 0 || d + r + 256 > near)) load += FarFaces(s, p, !inside, near - 256);
        if ((s.Flags & FlagNeverNear) == 0 && d - r < near) load += NearFaces(s, p, !inside, d - r < 256, 16 * near);
        return load;
    }

    /// <summary>Screen-edge bits as the passes set them (1: y &lt;= 0, 2: y &gt;= 256, 0xC: x off [0, 512)), from the GTE's
    /// projection: SZ = depth x <paramref name="scale"/> (4 near, 1 far) saturating at 0, the divide saturating (about 2)
    /// under H/2, the screen position saturating at +-1024. A point at or behind the camera lands off screen unless it is
    /// right in front of it.</summary>
    private static int Edges(in Pose p, Vector3 c, float scale, out float z, out bool error, out Vector2 screen)
    {
        var v = c - p.Eye;
        z = Vector3.Dot(v, p.Forward);
        float sz = Math.Clamp(scale * z, 0, 65535), q = sz > H / 2 ? H / sz : 2;
        float rx = Ofx + q * scale * Vector3.Dot(v, p.Right), ry = Ofy + q * scale * AspectY * Vector3.Dot(v, p.Down);
        error = sz <= H / 2 || rx is < -1024 or > 1023 || ry is < -1024 or > 1023;
        float sx = Math.Clamp(rx, -1024, 1023), sy = Math.Clamp(ry, -1024, 1023);
        screen = new(sx, sy);
        return (sy <= 0 ? 1 : 0) | (sy >= 256 ? 2 : 0) | (sx < 0 || sx >= 512 ? 0xC : 0);
    }

    /// <summary>The GTE's NCLIP of three projected corners (negative: the authored front faces the camera).</summary>
    private static float Nclip(Vector2 a, Vector2 b, Vector2 c) => (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);

    private static Load FarFaces(Sector s, in Pose p, bool clip, float farFaceNear)
    {
        int bytes = 0;
        Span<Vector2> xy = stackalloc Vector2[4];
        for (int k = 0; k < s.FarFaces; k++)
        {
            int n = s.FarCorners[k], flags = s.FarFlags[k], all = 0xF; float sum = 0, zmax = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                int e = Edges(p, s.Far[4 * k + i], 1, out float z, out _, out xy[i]);
                all &= e; float cz = Math.Max(0, z); sum += cz; zmax = Math.Max(zmax, cz);
            }
            // A triangle's depth weights its first corner twice.
            if (n == 3) sum += Math.Max(0, Vector3.Dot(s.Far[4 * k] - p.Eye, p.Forward));
            if (clip && all != 0) continue;
            // Front: NCLIP(3,2,0) &gt; 0 or NCLIP(1,2,0) &lt; 0 (a triangle: NCLIP(2,1,0) &gt; 0); flag 0x80 both sides.
            if ((flags & 0x80) == 0 && !(n == 4 ? Nclip(xy[3], xy[2], xy[0]) > 0 || Nclip(xy[1], xy[2], xy[0]) < 0 : Nclip(xy[2], xy[1], xy[0]) > 0)) continue;
            // Average depth under 10,240; from the near distance less 256 (1,792; less 64 per push step) drawn, 256 nearer
            // not, between when a corner reaches it.
            float push = 64 * (flags & 0x1F), depth = sum / 4 + push;
            if (sum / 4 >= FarLimit) continue;
            if (depth < farFaceNear && (depth <= farFaceNear - 256 || zmax + push < farFaceNear)) continue;
            bytes += (n == 4 ? 0x24 : 0x1C) + ((flags & 0x100) != 0 ? 0x0C : 0);
        }
        return new(bytes, 0, 0, 0, 0);
    }

    private static Load NearFaces(Sector s, in Pose p, bool clip, bool close, float nearSum)
    {
        int whole = 0, sub = 0, nq = 0, cq = 0;
        Span<float> z = stackalloc float[4];
        Span<Vector2> xy = stackalloc Vector2[4];
        for (int k = 0; k < s.NearFaces; k++)
        {
            int n = s.NearCorners[k], flags = s.NearFlags[k], all = 0xF; bool atCamera = false;
            for (int i = 0; i < n; i++)
            {
                // In a sector within 256 units, a corner under 384 deep whose projection the GTE flags as an error
                // (0x80026A0C..0x80026AD4) lets the face skip its facing test.
                int e = Edges(p, s.Near[4 * k + i], 4, out z[i], out bool error, out xy[i]);
                all &= e; if (close && 4 * z[i] < 0x600 && error) atCamera = true;
            }
            if (clip && all != 0) continue;
            if (!atCamera && (flags & 0x04) == 0)
            {
                // Front: NCLIP(3,2,0) &gt;= 0 or NCLIP(1,2,0) &lt;= 0 (a triangle: NCLIP(2,1,0) &gt;= 0), on the saturated
                // screen positions; flag 2 negates the first test and turns the second.
                bool reversed = (flags & 0x02) != 0;
                bool shows;
                if (n == 3) { float t = Nclip(xy[2], xy[1], xy[0]); shows = (reversed ? -t : t) >= 0; }
                else
                {
                    float first = Nclip(xy[3], xy[2], xy[0]), second = Nclip(xy[1], xy[2], xy[0]);
                    shows = (reversed ? -first : first) >= 0 || (reversed ? second >= 0 : second <= 0);
                }
                if (!shows) continue;
            }
            // SZ: four per unit, from 0 (at or behind the camera); a triangle weights its first corner twice.
            float sum = 0; bool closeCorner = false;
            for (int i = 0; i < n; i++) { float sz = Math.Clamp(4 * z[i], 0, 65535); sum += sz; if (sz < 0x140) closeCorner = true; }
            if (n == 3) sum += Math.Clamp(4 * z[0], 0, 65535);
            if (sum >= nearSum) continue;
            bool textured = s.NearTextured[k];
            int piece = n == 4 ? (textured ? 0x34 : 0x24) : (textured ? 0x28 : 0x1C);
            bool direct = sum >= 0x2000 || (flags & 0x80) != 0 || !textured;
            if (!direct && closeCorner && sum <= 0) continue;
            if (!direct && !closeCorner && (flags & 0x40) != 0) direct = true;
            if (direct) { whole += piece; continue; }
            if (closeCorner && (flags & 0x40) == 0) { sub += 16 * piece; cq++; }
            else { sub += 4 * piece; nq++; }
        }
        return new(0, whole, sub, nq, cq);
    }

    /// <summary>A camera the survey tried and what the terrain drew from it.</summary>
    public readonly record struct View(Pose Pose, Vector3 Spot, Load Load);

    /// <summary>Spyro's camera at each spot (where Spyro stands, editor units): 155 units behind him and 40 above (the
    /// game's: 154 and 39 measured), looking 14 degrees down (the game's), level and 20 degrees up, every 30 degrees
    /// round; and from 200 units higher (a jump or glide).</summary>
    internal static IEnumerable<(Pose Pose, Vector3 Spot)> Cameras(IReadOnlyList<Vector3> spots, int headings = 12)
    {
        double[] pitches = [-14 * Math.PI / 180, 0, 20 * Math.PI / 180];
        float[] lifts = [40, 240];
        foreach (var spot in spots)
            foreach (float lift in lifts)
                for (int h = 0; h < headings; h++)
                {
                    double a = h * 2 * Math.PI / headings;
                    var eye = spot + new Vector3((float)(-155 * Math.Cos(a)), (float)(-155 * Math.Sin(a)), lift);
                    foreach (double pitch in pitches) yield return (Pose.Looking(eye, a, pitch), spot);
                }
    }

    /// <summary>Every camera's load (in parallel), heaviest first (by the faces drawn whole).</summary>
    internal static View[] Survey(IReadOnlyList<Sector> sectors, IReadOnlyList<(Pose Pose, Vector3 Spot)> cameras, float near = NearDistance)
    {
        var views = new View[cameras.Count];
        Parallel.For(0, cameras.Count, i => views[i] = new(cameras[i].Pose, cameras[i].Spot, Frame(sectors, cameras[i].Pose, near)));
        Array.Sort(views, (a, b) => b.Load.Whole != a.Load.Whole ? b.Load.Whole.CompareTo(a.Load.Whole) : b.Load.Total.CompareTo(a.Load.Total));
        return views;
    }

    /// <summary>The heaviest view from Spyro's spots.</summary>
    internal static View WorstView(IReadOnlyList<Sector> sectors, IReadOnlyList<Vector3> spots, float near = NearDistance)
    {
        var all = Survey(sectors, [.. Cameras(spots)], near);
        return all.Length == 0 ? default : all[0];
    }

    /// <summary>Where Spyro can stand: the middle of every upward-facing face he collides with (drawn or not), one per
    /// cell of <paramref name="cell"/> units (and 96 high), and the start.</summary>
    internal static List<Vector3> Spots(Spyro.Editor.Core.Editing.SceneDocument doc, float cell = 160)
    {
        HashSet<(int, int, int)> seen = [];
        List<Vector3> spots = [new(doc.Spawn.X, doc.Spawn.Y, doc.Spawn.Z)];
        void Add(Vector3f[] v)
        {
            if (v.Length < 3) return;
            double nx = 0, ny = 0, nz = 0;
            for (int i = 0; i < v.Length; i++) { var a = v[i]; var b = v[(i + 1) % v.Length]; nx += (a.Y - b.Y) * (a.Z + b.Z); ny += (a.Z - b.Z) * (a.X + b.X); nz += (a.X - b.X) * (a.Y + b.Y); }
            double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            if (len == 0 || Math.Abs(nz) / len < 0.5) return;
            var c = new Vector3(v.Average(p => p.X), v.Average(p => p.Y), v.Average(p => p.Z));
            if (seen.Add(((int)(c.X / cell), (int)(c.Y / cell), (int)(c.Z / 96)))) spots.Add(c);
        }
        foreach (var s in doc.Surfaces.Where(s => !s.NoCollision)) Add(s.Vertices);
        foreach (var c in doc.CollisionFaces ?? []) Add(c.Vertices);
        return spots;
    }

    private static uint U(ReadOnlySpan<byte> b, int at) => BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
}
