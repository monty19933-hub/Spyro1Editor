using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Sky;

/// <summary>How a panorama becomes a native sky: about how many triangles (the game's own skies use 250-4,300) and
/// how far to turn it (degrees, anticlockwise seen from above).</summary>
public sealed record SkyBuildOptions(int Triangles = SkyBuildOptions.DefaultTriangles, double TurnDegrees = 0)
{
    public const int MinTriangles = 1000, DefaultTriangles = 3000, MaxTriangles = 4000;
}

public sealed record SkyBuildResult(byte[] Block, int Triangles, int Vertices, int Colors, int Parts);

/// <summary>Builds a native Spyro 1 sky block from a panorama.
/// <para>The game draws a sky as untextured triangles around the camera (USA 0x8004EBA8..0x8004EFFC), part by part:</para>
/// <list type="bullet">
/// <item>A part is skipped when the depth of its centre is not more than its radius, so parts must be small: they are
/// kept within <see cref="PartRadius"/> of a sky <see cref="Radius"/> units away (the game's own parts reach about
/// 0.39 of their distance; 0.33 keeps parts drawn up to 52° off the view's middle).</item>
/// <item>Header: centre (+0 VX, +2 VY, +6 VZ), radius (+4), origin (+8 VX, +10 VY, +14 VZ), vertex count (+12),
/// triangle count (+16), colour count (+18), then 0xFFFFFFFF.</item>
/// <item>Each corner packs offsets from the origin: VZ = origin + bits 21-31, VX = origin − bits 10-20, VY = origin −
/// bits 0-9, in the camera axes VX = −Y, VY = −Z, VZ = X of the world.</item>
/// <item>Each triangle names three corners and three colours by byte offset (10 bits each, at most 256 per part).</item>
/// <item>Colours are R, G, B and the GPU command 0x30.</item>
/// <item>Triangles are drawn in order without back-face culling.</item>
/// </list>
/// <para>Corners are laid on rings of equal height, denser where the panorama changes most; neighbouring rings are joined
/// edge to edge, so the sky has no cracks.</para></summary>
public static class NativeSkyBuilder
{
    public const int Radius = 1000, PartRadius = 320, PartVertices = 160;
    /// <summary>The widest angle between neighbouring corners, so one triangle never outgrows a part.</summary>
    private const double MaxGap = Math.PI / 15;

    public static SkyBuildResult Build(SkyPanorama panorama, SkyBuildOptions options)
    {
        int budget = Math.Clamp(options.Triangles, SkyBuildOptions.MinTriangles, SkyBuildOptions.MaxTriangles);
        var importance = panorama.LevelNear(512);
        double turn = options.TurnDegrees;

        // Ring heights: evenly spaced in angle, plus extra rings where the colour changes from row to row.
        int rings = Math.Max(8, (int)Math.Round(Math.Sqrt(budget / 2.55)));
        double[] rowChange = RowChange(importance);
        double mean = Math.Max(1e-6, rowChange.Average());
        double[] density = rowChange.Select(d => 1 + 2 * Math.Min(6, d / mean)).ToArray();
        // No gap wider than MaxGap between rings (or a ring and a pole): small triangles keep parts small.
        List<double> elevations = [];
        double previous = Math.PI / 2;
        foreach (double e in Place(density, rings, offset: 1, ends: false).Select(f => Math.PI / 2 - Math.PI * f).Append(-Math.PI / 2))
        {
            int pieces = (int)Math.Ceiling((previous - e) / MaxGap - 1e-9);
            for (int k = 1; k < pieces; k++) elevations.Add(previous - (previous - e) * k / pieces);
            if (e > -Math.PI / 2) elevations.Add(e);
            previous = e;
        }

        // Corners per ring: the ring's length, plus extra where the colour changes around it; never further apart than
        // MaxGap along the ring.
        var around = elevations.Select(e => AroundChange(panorama, e, turn)).ToArray();
        double meanAround = Math.Max(1e-6, around.SelectMany(a => a).Average());
        double[] weight = elevations.Select((e, k) => Math.Cos(e) * (1 + Math.Min(4, around[k].Average() / meanAround))).ToArray();
        int[] minimum = elevations.Select(e => Math.Max(3, (int)Math.Ceiling(2 * Math.PI * Math.Cos(e) / MaxGap))).ToArray();
        int[] counts = Share(weight, budget / 2, minimum);

        List<SkyVector> corners = [new(0, 0, 1)];
        List<int[]> ringCorners = [];
        for (int k = 0; k < elevations.Count; k++)
        {
            double[] dens = Floor(around[k].Select(c => 1 + 3 * Math.Min(6, c / meanAround)).ToArray(), 2 * Math.PI * Math.Cos(elevations[k]) / (counts[k] * MaxGap));
            double[] fractions = Place(dens, counts[k], offset: (k % 2) * 0.5, ends: true);
            int[] ids = new int[counts[k]];
            for (int i = 0; i < counts[k]; i++)
            {
                double azimuth = 2 * Math.PI * fractions[i];
                ids[i] = corners.Count;
                corners.Add(new(Math.Cos(elevations[k]) * Math.Cos(azimuth), Math.Cos(elevations[k]) * Math.Sin(azimuth), Math.Sin(elevations[k])));
            }
            ringCorners.Add(ids);
        }
        int bottom = corners.Count;
        corners.Add(new(0, 0, -1));

        List<(int A, int B, int C)> triangles = [];
        Fan(0, ringCorners[0], corners, triangles);
        for (int k = 0; k + 1 < ringCorners.Count; k++)
            Strip(ringCorners[k], ringCorners[k + 1], corners, triangles);
        Fan(bottom, ringCorners[^1], corners, triangles);

        // Corner colours: the panorama averaged over the corner's share of the sky.
        double[] spacing = new double[corners.Count];
        Array.Fill(spacing, double.MaxValue);
        foreach (var (a, b, c) in triangles)
            foreach ((int p, int q) in new[] { (a, b), (b, c), (c, a) })
            {
                double angle = Math.Acos(Math.Clamp(corners[p].Dot(corners[q]), -1, 1));
                spacing[p] = Math.Min(spacing[p], angle); spacing[q] = Math.Min(spacing[q], angle);
            }
        var colors = corners.Select((d, i) =>
        {
            var (r, g, b) = panorama.Sample(d, 0.75 * spacing[i], turn);
            return (SkyPanorama.Byte(r), SkyPanorama.Byte(g), SkyPanorama.Byte(b));
        }).ToArray();
        var points = corners.Select(d => (X: (int)Math.Round(d.X * Radius), Y: (int)Math.Round(d.Y * Radius), Z: (int)Math.Round(d.Z * Radius))).ToArray();

        var parts = Partition(triangles, corners, points);
        var background = panorama.LowerAverage();
        byte[] block = Encode(parts, points, colors, background);
        if (!Spyro1SkyBlockAnalyzer.IsValidStandaloneSkyBlock(block))
            throw new InvalidDataException("The built sky is not a valid native sky block.");
        return new SkyBuildResult(block, triangles.Count, parts.Sum(p => p.Vertices.Count), parts.Sum(p => p.Vertices.Select(i => colors[i]).Distinct().Count()), parts.Count);
    }

    /// <summary>How much the colour changes from each row of the panorama to the next (averaged across the row).</summary>
    private static double[] RowChange((int Width, int Height, float[] Rgb) level)
    {
        var change = new double[level.Height];
        for (int y = 0; y < level.Height; y++)
        {
            int y2 = Math.Min(level.Height - 1, y + 1);
            double sum = 0;
            for (int x = 0; x < level.Width; x++)
                for (int k = 0; k < 3; k++)
                    sum += Math.Abs(level.Rgb[(y2 * level.Width + x) * 3 + k] - level.Rgb[(y * level.Width + x) * 3 + k]);
            change[y] = sum / level.Width;
        }
        return Smooth(change, 2, wrap: false);
    }

    /// <summary>How much the colour changes along the ring at <paramref name="elevation"/>, at 512 points from azimuth 0.</summary>
    private static double[] AroundChange(SkyPanorama panorama, double elevation, double turn)
    {
        const int n = 512;
        var colors = new (float R, float G, float B)[n];
        double footprint = 2 * Math.PI / n * Math.Max(0.05, Math.Cos(elevation));
        for (int i = 0; i < n; i++)
        {
            double azimuth = 2 * Math.PI * i / n;
            colors[i] = panorama.Sample(new(Math.Cos(elevation) * Math.Cos(azimuth), Math.Cos(elevation) * Math.Sin(azimuth), Math.Sin(elevation)), footprint, turn);
        }
        var change = new double[n];
        for (int i = 0; i < n; i++)
        {
            var a = colors[i]; var b = colors[(i + 1) % n];
            change[i] = Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
        }
        return Smooth(change, 3, wrap: true);
    }

    private static double[] Smooth(double[] values, int radius, bool wrap)
    {
        var result = new double[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            double sum = 0; int n = 0;
            for (int d = -radius; d <= radius; d++)
            {
                int j = i + d;
                if (wrap) j = ((j % values.Length) + values.Length) % values.Length;
                else if (j < 0 || j >= values.Length) continue;
                sum += values[j]; n++;
            }
            result[i] = sum / n;
        }
        return result;
    }

    /// <summary><paramref name="count"/> positions (fractions 0-1) spread so each holds an equal share of the density.
    /// With <paramref name="ends"/> false they lie strictly between 0 and 1 (rings between the poles); otherwise they
    /// go round a circle starting <paramref name="offset"/> of a share along.</summary>
    private static double[] Place(double[] density, int count, double offset, bool ends)
    {
        var cumulative = new double[density.Length + 1];
        for (int i = 0; i < density.Length; i++) cumulative[i + 1] = cumulative[i] + density[i];
        double total = cumulative[^1];
        var result = new double[count];
        int cell = 0;
        for (int k = 0; k < count; k++)
        {
            double share = ends ? (k + offset) / count : (k + offset) / (count + 1);
            double target = share * total;
            while (cell < density.Length - 1 && cumulative[cell + 1] < target) cell++;
            double within = density[cell] > 0 ? (target - cumulative[cell]) / density[cell] : 0;
            result[k] = (cell + Math.Clamp(within, 0, 1)) / density.Length;
        }
        return result;
    }

    /// <summary>Whole numbers in proportion to <paramref name="weights"/>, each at least its minimum, adding up to
    /// <paramref name="total"/> (or to the minimums, when those already need more).</summary>
    private static int[] Share(double[] weights, int total, int[] minimum)
    {
        double sum = weights.Sum();
        int[] counts = weights.Select((w, i) => Math.Max(minimum[i], (int)Math.Floor(total * w / sum))).ToArray();
        var order = weights.Select((w, i) => (Remainder: total * w / sum - Math.Floor(total * w / sum), i)).OrderByDescending(p => p.Remainder).Select(p => p.i).ToList();
        for (int k = 0; counts.Sum() < total; k++) counts[order[k % order.Count]]++;
        while (counts.Sum() > total)
        {
            int i = Enumerable.Range(0, counts.Length).Where(j => counts[j] > minimum[j]).OrderByDescending(j => counts[j] - total * weights[j] / sum).FirstOrDefault(-1);
            if (i < 0) break;
            counts[i]--;
        }
        return counts;
    }

    /// <summary>The density raised evenly until its thinnest stretch is at least <paramref name="share"/> of its mean
    /// (share = the even spacing over the widest spacing allowed), so no two placed points end up too far apart.</summary>
    private static double[] Floor(double[] density, double share)
    {
        if (share >= 1) return density.Select(_ => 1.0).ToArray();
        double mean = density.Average(), min = density.Min();
        double lift = Math.Max(0, (share * mean - min) / (1 - share));
        return density.Select(d => d + lift).ToArray();
    }

    private static double Azimuth(SkyVector d) { double a = Math.Atan2(d.Y, d.X); return a < 0 ? a + 2 * Math.PI : a; }

    private static void Fan(int pole, int[] ring, List<SkyVector> corners, List<(int, int, int)> triangles)
    {
        for (int i = 0; i < ring.Length; i++) triangles.Add((pole, ring[i], ring[(i + 1) % ring.Length]));
    }

    /// <summary>Joins two rings (corners in order of azimuth) with one triangle per corner of either ring, walking both
    /// rings round together from the upper ring's first corner.</summary>
    private static void Strip(int[] upper, int[] lower, List<SkyVector> corners, List<(int, int, int)> triangles)
    {
        double start = Azimuth(corners[upper[0]]);
        double Along(int id) { double a = Azimuth(corners[id]) - start; return a - 2 * Math.PI * Math.Floor(a / (2 * Math.PI)); }
        // Angles along each ring from the start, increasing; the lower ring starts at its corner nearest the start.
        var u = new double[upper.Length + 1];
        for (int i = 0; i < upper.Length; i++) u[i] = Along(upper[i]);
        u[0] = 0; u[upper.Length] = 2 * Math.PI;
        int j0 = Enumerable.Range(0, lower.Length).OrderBy(j => Math.Min(Along(lower[j]), 2 * Math.PI - Along(lower[j]))).First();
        var l = new double[lower.Length + 1];
        l[0] = Along(lower[j0]) > Math.PI ? Along(lower[j0]) - 2 * Math.PI : Along(lower[j0]);
        for (int j = 1; j <= lower.Length; j++)
        {
            double step = Along(lower[(j0 + j) % lower.Length]) - Along(lower[(j0 + j - 1) % lower.Length]);
            l[j] = l[j - 1] + (step - 2 * Math.PI * Math.Floor(step / (2 * Math.PI)));
        }
        l[lower.Length] = l[0] + 2 * Math.PI;
        int a = 0, b = 0;
        while (a < upper.Length || b < lower.Length)
        {
            int top = upper[a % upper.Length], low = lower[(j0 + b) % lower.Length];
            bool advanceUpper = b == lower.Length || (a < upper.Length && u[a + 1] <= l[b + 1]);
            if (advanceUpper) { triangles.Add((top, low, upper[(a + 1) % upper.Length])); a++; }
            else { triangles.Add((top, low, lower[(j0 + b + 1) % lower.Length])); b++; }
        }
    }

    private sealed record Part(List<(int A, int B, int C)> Triangles, List<int> Vertices);

    /// <summary>Groups triangles into parts small enough for the game's culling and packing: halves (by height or
    /// azimuth, whichever is wider) until each part fits.</summary>
    private static List<Part> Partition(List<(int A, int B, int C)> triangles, List<SkyVector> corners, (int X, int Y, int Z)[] points)
    {
        List<Part> parts = [];
        Split(triangles, corners, points, parts, depth: 0);
        if (parts.Count > 254) throw new InvalidDataException("The sky needs more parts than one level can list; use fewer triangles.");
        return parts;
    }

    private static void Split(List<(int A, int B, int C)> group, List<SkyVector> corners, (int X, int Y, int Z)[] points, List<Part> parts, int depth)
    {
        var vertices = group.SelectMany(t => new[] { t.A, t.B, t.C }).Distinct().ToList();
        if (vertices.Count <= PartVertices && Sphere(vertices, points).Radius <= PartRadius || group.Count == 1)
        {
            if (vertices.Count > 256) throw new InvalidDataException("A sky part has more than 256 corners.");
            parts.Add(new Part(group, vertices));
            return;
        }
        if (depth > 24) throw new InvalidDataException("The sky could not be divided into parts the game can draw.");
        // Halves across the group's longest extent (triangle middles), so parts stay round and few.
        var middles = group.Select(t => (Triangle: t, Middle: corners[t.A] + corners[t.B] + corners[t.C])).ToList();
        double Span(Func<SkyVector, double> axis) => middles.Max(m => axis(m.Middle)) - middles.Min(m => axis(m.Middle));
        Func<SkyVector, double> along = new Func<SkyVector, double>[] { v => v.X, v => v.Y, v => v.Z }.OrderByDescending(Span).First();
        // Cut at the middle of that extent (even-sized parts in space), or at the median when one side would be empty.
        double low = middles.Min(m => along(m.Middle)), high = middles.Max(m => along(m.Middle)), cut = (low + high) / 2;
        var first = middles.Where(m => along(m.Middle) < cut).Select(m => m.Triangle).ToList();
        var second = middles.Where(m => along(m.Middle) >= cut).Select(m => m.Triangle).ToList();
        if (first.Count == 0 || second.Count == 0)
        {
            var ordered = middles.OrderBy(m => along(m.Middle)).Select(m => m.Triangle).ToList();
            first = ordered.Take(ordered.Count / 2).ToList(); second = ordered.Skip(ordered.Count / 2).ToList();
        }
        Split(first, corners, points, parts, depth + 1);
        Split(second, corners, points, parts, depth + 1);
    }

    /// <summary>A part's culling sphere in the game's camera axes (VX = −Y, VY = −Z, VZ = X), close to the smallest that
    /// holds its corners (radius rounded up).</summary>
    private static (int VX, int VY, int VZ, int Radius) Sphere(List<int> vertices, (int X, int Y, int Z)[] points)
    {
        var g = vertices.Select(i => new SkyVector(-points[i].Y, -points[i].Z, points[i].X)).ToList();
        // Ritter's sphere: from the two points farthest apart, grown to take in any point outside.
        SkyVector Farthest(SkyVector from) => g.OrderByDescending(p => (p - from).Length).First();
        SkyVector a = Farthest(g[0]), b = Farthest(a);
        SkyVector centre = (a + b) * 0.5; double radius = (b - a).Length / 2;
        foreach (var p in g)
        {
            double d = (p - centre).Length;
            if (d <= radius) continue;
            double grown = (radius + d) / 2;
            centre = centre + (p - centre) * ((grown - radius) / d);
            radius = grown;
        }
        int cx = (int)Math.Round(centre.X), cy = (int)Math.Round(centre.Y), cz = (int)Math.Round(centre.Z);
        double r = g.Max(p => (p - new SkyVector(cx, cy, cz)).Length);
        return (cx, cy, cz, (int)Math.Ceiling(r) + 1);
    }

    private static byte[] Encode(List<Part> parts, (int X, int Y, int Z)[] points, (byte R, byte G, byte B)[] colors, (byte R, byte G, byte B) background)
    {
        List<byte[]> encoded = [];
        foreach (var part in parts)
        {
            var g = part.Vertices.Select(i => (VX: -points[i].Y, VY: -points[i].Z, VZ: points[i].X)).ToList();
            int ox = g.Max(p => p.VX), oy = g.Max(p => p.VY), oz = g.Min(p => p.VZ);
            var palette = part.Vertices.Select(i => colors[i]).Distinct().ToList();
            if (palette.Count > 256) throw new InvalidDataException("A sky part has more than 256 colours.");
            var vertexIndex = part.Vertices.Select((v, i) => (v, i)).ToDictionary(p => p.v, p => p.i);
            var colorIndex = palette.Select((c, i) => (c, i)).ToDictionary(p => p.c, p => p.i);
            var sphere = Sphere(part.Vertices, points);
            byte[] s = new byte[24 + 4 * g.Count + 4 * palette.Count + 8 * part.Triangles.Count];
            void H(int at, int value) => BinaryPrimitives.WriteInt16LittleEndian(s.AsSpan(at), checked((short)value));
            H(0, sphere.VX); H(2, sphere.VY); H(4, sphere.Radius); H(6, sphere.VZ);
            H(8, ox); H(10, oy); BinaryPrimitives.WriteUInt16LittleEndian(s.AsSpan(12), (ushort)g.Count); H(14, oz);
            BinaryPrimitives.WriteUInt16LittleEndian(s.AsSpan(16), (ushort)part.Triangles.Count);
            BinaryPrimitives.WriteUInt16LittleEndian(s.AsSpan(18), (ushort)palette.Count);
            BinaryPrimitives.WriteUInt32LittleEndian(s.AsSpan(20), uint.MaxValue);
            int at = 24;
            foreach (var p in g)
            {
                int f21 = p.VZ - oz, f10 = ox - p.VX, f0 = oy - p.VY;
                if (f21 is < 0 or > 2047 || f10 is < 0 or > 2047 || f0 is < 0 or > 1023)
                    throw new InvalidDataException("A sky part is too large for the game's packed corners.");
                BinaryPrimitives.WriteUInt32LittleEndian(s.AsSpan(at), (uint)((f21 << 21) | (f10 << 10) | f0)); at += 4;
            }
            foreach (var c in palette) { s[at] = c.R; s[at + 1] = c.G; s[at + 2] = c.B; s[at + 3] = 0x30; at += 4; }
            foreach (var (a, b, c) in part.Triangles)
            {
                uint V(int corner) => (uint)(vertexIndex[corner] * 4);
                uint C(int corner) => (uint)(colorIndex[colors[corner]] * 4);
                BinaryPrimitives.WriteUInt32LittleEndian(s.AsSpan(at), V(a) | V(b) << 10 | V(c) << 20);
                BinaryPrimitives.WriteUInt32LittleEndian(s.AsSpan(at + 4), C(a) | C(b) << 10 | C(c) << 20);
                at += 8;
            }
            encoded.Add(s);
        }
        int headerLength = 12 + 4 * parts.Count;
        byte[] block = new byte[headerLength + encoded.Sum(e => e.Length)];
        BinaryPrimitives.WriteUInt32LittleEndian(block, (uint)block.Length);
        block[4] = background.R; block[5] = background.G; block[6] = background.B;
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(8), (uint)parts.Count);
        int offset = headerLength;
        for (int i = 0; i < encoded.Count; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(12 + 4 * i), (uint)(offset - 4));
            encoded[i].CopyTo(block, offset);
            offset += encoded[i].Length;
        }
        return block;
    }
}
