using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Editing;

/// <summary>One part of a doorway's frame: the faces of its front and back (<see cref="Face"/>) or its sides, top and
/// inside (<see cref="Side"/>). A frame keeps one texture per part unless the user repaints single faces.</summary>
public enum DoorwayFramePart { Face, Side }

/// <summary>A frame face before it becomes a scene face: corners (editor units, anticlockwise seen from the side it
/// shows), which part of the frame it belongs to, and its shade (128 = the texture as it is).</summary>
public sealed record DoorwayFrameFace(Vector3f[] Corners, DoorwayFramePart Part, ColorRgba Shade);

/// <summary>A doorway shape: the opening Spyro walks into (the outline the game draws the destination's sky inside) and
/// the frame built around it.
/// <para>The opening is given in the doorway's own plane, in editor units at size 1: U runs along the doorway (to the
/// right seen from the side Spyro walks in from), V up, (0, 0) is the bottom middle. The points go anticlockwise seen
/// from the far side (the way the game's own doorways list them, USA renderer 0x80050E68..0x80051F88): bottom right,
/// up the right side, over the top, down to bottom left; the last-to-first edge is the floor.</para>
/// <para>What the game allows (USA 1.0): at most 10 points (the projected-point buffer 0x80078DD8 holds 10 and the edge
/// table 0x80077EA0 holds 10 edges and its end mark), and a convex outline (the sky is clipped by every edge's
/// half-plane, GTE NCLIP at 0x8004F7E8).</para></summary>
public sealed record DoorwayShape(string Id, string Name, string Description, IReadOnlyList<(double U, double V)> Opening,
    double FrameWidth, double FrameDepth, bool Keystone, bool Bases, bool Lintel)
{
    public override string ToString() => Name;
    /// <summary>The opening's width and height at size 1 (editor units).</summary>
    public double Width => Opening.Max(p => p.U) - Opening.Min(p => p.U);
    public double Height => Opening.Max(p => p.V);
}

/// <summary>The predetermined doorway shapes: each one's opening (written into the doorway record, so the destination's
/// sky shows exactly inside it) and its arch, made of ordinary scene faces that the Ground card and Choose Texture can
/// repaint.</summary>
public static class DoorwayShapes
{
    /// <summary>The game draws a doorway's opening from at most this many points (USA 0x80078DD8 / 0x80077EA0).</summary>
    public const int MaxOutlinePoints = 10;
    public const double MinScale = 0.75, MaxScale = 1.5;
    /// <summary>The frame's posts reach this far below the floor, so no gap shows on uneven ground.</summary>
    public const double PostFooting = 8;

    public static readonly DoorwayShape StoneArch = new("stone-arch", "Stone Arch",
        "A round-topped stone arch, like the game's own doorways, with a keystone and footings.",
        Arch(64, 128, 64, 6), 24, 20, Keystone: true, Bases: true, Lintel: false);
    public static readonly DoorwayShape PointedArch = new("pointed-arch", "Pointed Arch",
        "A tall arch that meets in a point at the top.", Pointed(64, 100, 3), 20, 18, Keystone: true, Bases: true, Lintel: false);
    public static readonly DoorwayShape SquareGate = new("square-gate", "Square Gate",
        "Two posts and a flat beam with a ledge on top.", [(64, 0), (64, 176), (-64, 176), (-64, 0)], 24, 20, Keystone: false, Bases: true, Lintel: true);
    public static readonly DoorwayShape RoundRing = new("round-ring", "Round Ring",
        "A ring set into the ground.", Ring(96, 80, 10), 20, 16, Keystone: true, Bases: false, Lintel: false);
    public static readonly DoorwayShape WideArch = new("wide-arch", "Wide Arch",
        "A low, wide arch, half as wide again as the others.", Arch(96, 112, 64, 6), 24, 20, Keystone: true, Bases: true, Lintel: false);
    public static readonly DoorwayShape HexGate = new("hex-gate", "Hex Gate",
        "A six-sided doorway.", [(48, 0), (96, 83), (48, 166), (-48, 166), (-96, 83), (-48, 0)], 20, 18, Keystone: true, Bases: false, Lintel: false);

    public static IReadOnlyList<DoorwayShape> All { get; } = [StoneArch, PointedArch, SquareGate, RoundRing, WideArch, HexGate];

    public static DoorwayShape? Find(string? id) => All.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));

    /// <summary>Jambs <paramref name="spring"/> high, then a half ellipse <paramref name="halfWidth"/> wide and
    /// <paramref name="rise"/> high in <paramref name="segments"/> steps.</summary>
    private static (double, double)[] Arch(double halfWidth, double spring, double rise, int segments)
    {
        List<(double, double)> points = [(halfWidth, 0)];
        for (int k = 0; k <= segments; k++)
        {
            double a = Math.PI * k / segments;
            points.Add((Clean(halfWidth * Math.Cos(a)), Clean(spring + rise * Math.Sin(a))));
        }
        points.Add((-halfWidth, 0));
        return [.. points];
    }

    /// <summary>An equilateral pointed arch: each side an arc whose centre is the other side's springing point.</summary>
    private static (double, double)[] Pointed(double halfWidth, double spring, int steps)
    {
        double r = 2 * halfWidth;
        List<(double, double)> right = [];
        for (int k = 0; k <= steps; k++)
        {
            double a = Math.PI / 3 * k / steps;
            right.Add((Clean(-halfWidth + r * Math.Cos(a)), Clean(spring + r * Math.Sin(a))));
        }
        List<(double, double)> points = [(halfWidth, 0), .. right];
        points.AddRange(right.Take(steps).Reverse().Select(p => (-p.Item1, p.Item2)));
        points.Add((-halfWidth, 0));
        return [.. points];
    }

    /// <summary>A circle of <paramref name="radius"/> whose centre is <paramref name="centre"/> above the floor, cut flat by
    /// the floor, as <paramref name="count"/> points.</summary>
    private static (double, double)[] Ring(double radius, double centre, int count)
    {
        double low = Math.Asin(-centre / radius), high = Math.PI - low, step = (high - low) / (count - 1);
        return Enumerable.Range(0, count).Select(k =>
        {
            double a = low + step * k;
            return (Clean(radius * Math.Cos(a)), Clean(centre + radius * Math.Sin(a)));
        }).ToArray();
    }

    private static double Clean(double v) => Math.Round(v, 3);

    /// <summary>The opening's points at <paramref name="scale"/> in the doorway's plane (U along, V up).</summary>
    public static IReadOnlyList<(double U, double V)> Opening(DoorwayShape shape, double scale) =>
        shape.Opening.Select(p => (p.U * scale, p.V * scale)).ToArray();

    /// <summary>The direction Spyro walks in (unit, horizontal) for a heading of 0-255 (0 = +X, 64 = +Y).</summary>
    public static (double X, double Y) Facing(int heading) => (Math.Cos(heading * Math.PI / 128), Math.Sin(heading * Math.PI / 128));

    /// <summary>A point of the doorway's plane in the level (editor units): <paramref name="position"/> is the bottom
    /// middle, U runs along (-facing.Y, facing.X), V up, D along the facing.</summary>
    public static (double X, double Y, double Z) Place(Vector3f position, int heading, double u, double v, double d = 0)
    {
        var (nx, ny) = Facing(heading);
        return (position.X - ny * u + nx * d, position.Y + nx * u + ny * d, position.Z + v);
    }

    /// <summary>The opening in the level (editor units, not rounded), in the record's order.</summary>
    public static IReadOnlyList<(double X, double Y, double Z)> OutlineInLevel(DoorwayShape shape, double scale, Vector3f position, int heading) =>
        Opening(shape, scale).Select(p => Place(position, heading, p.U, p.V)).ToArray();

    /// <summary>Is <paramref name="opening"/> something the game can draw: 3-10 points, a flat floor edge (the first and
    /// last points at V 0), strictly convex and anticlockwise. Null when it is, else why not.</summary>
    public static string? Check(IReadOnlyList<(double U, double V)> opening)
    {
        int n = opening.Count;
        if (n < 3) return "A doorway opening needs at least three points.";
        if (n > MaxOutlinePoints) return $"The game draws a doorway opening from at most {MaxOutlinePoints} points; this one has {n}.";
        if (Math.Abs(opening[0].V) > 1e-6 || Math.Abs(opening[^1].V) > 1e-6 || opening[0].U <= opening[^1].U)
            return "A doorway opening starts at its bottom right and ends at its bottom left, on the floor.";
        for (int i = 0; i < n; i++)
        {
            var a = opening[i]; var b = opening[(i + 1) % n]; var c = opening[(i + 2) % n];
            double cross = (b.U - a.U) * (c.V - b.V) - (b.V - a.V) * (c.U - b.U);
            if (cross <= 1e-9) return "The game can only show the destination's sky through a convex opening (no corner may point inwards).";
        }
        return null;
    }

    /// <summary>The frame of a doorway: a border <see cref="DoorwayShape.FrameWidth"/> wide around every side of the opening
    /// but the floor, <see cref="DoorwayShape.FrameDepth"/> to each side of it, mitred at the corners and cut into pieces
    /// about two and a half widths long (each scene face shows its texture once), with a keystone, footings and a ledge as
    /// the shape has them. Corners are whole editor units.</summary>
    public static IReadOnlyList<DoorwayFrameFace> Frame(DoorwayShape shape, double scale, Vector3f position, int heading)
    {
        double w = shape.FrameWidth * scale, depth = shape.FrameDepth * scale;
        var opening = Opening(shape, scale);
        string? problem = Check(opening);
        if (problem != null) throw new InvalidDataException(problem);
        var faces = new List<DoorwayFrameFace>();
        var (nx, ny) = Facing(heading);
        ColorRgba light = new(128, 128, 128), top = new(140, 140, 140), shade = new(96, 96, 96);
        Vector3f P(double u, double v, double d)
        {
            var (x, y, z) = Place(position, heading, u, v, d);
            return new((float)Math.Round(x), (float)Math.Round(y), (float)Math.Round(z));
        }
        (double X, double Y, double Z) Dir(double du, double dv, double dd) => (-ny * du + nx * dd, nx * du + ny * dd, dv);
        void Quad(DoorwayFramePart part, ColorRgba colour, (double X, double Y, double Z) outward, params Vector3f[] corners)
        {
            if (corners.Distinct().Count() < 4) return;
            faces.Add(new(Orient(corners, outward), part, colour));
        }

        // The border: the chain of opening points from bottom right to bottom left, offset outwards (mitred).
        int n = opening.Count;
        var inner = opening.ToArray();
        var normals = new (double U, double V)[n - 1];
        for (int k = 0; k < n - 1; k++)
        {
            double du = inner[k + 1].U - inner[k].U, dv = inner[k + 1].V - inner[k].V, l = Math.Sqrt(du * du + dv * dv);
            normals[k] = (dv / l, -du / l);                       // outward for an anticlockwise outline
        }
        var outer = new (double U, double V)[n];
        for (int k = 0; k < n; k++)
        {
            (double U, double V) dir;
            if (k == 0) dir = normals[0];
            else if (k == n - 1) dir = normals[n - 2];
            else
            {
                var a = normals[k - 1]; var b = normals[k];
                double mu = a.U + b.U, mv = a.V + b.V, ml = Math.Sqrt(mu * mu + mv * mv);
                mu /= ml; mv /= ml;
                double cos = mu * a.U + mv * a.V;
                dir = (mu / cos, mv / cos);
            }
            outer[k] = (inner[k].U + dir.U * w, inner[k].V + dir.V * w);
        }
        // The posts reach below the floor, along their own slope (a slanting side stays on its edge).
        double footing = PostFooting * scale;
        foreach (var (k, next) in new[] { (0, 1), (n - 1, n - 2) })
        {
            double eu = inner[next].U - inner[k].U, ev = inner[next].V - inner[k].V, el = Math.Sqrt(eu * eu + ev * ev);
            if (ev / el < 0.3) { eu = 0; ev = 1; }
            double t = footing / ev;
            inner[k] = (inner[k].U - eu * t, inner[k].V - ev * t);
            outer[k] = (outer[k].U - eu * t, outer[k].V - ev * t);
        }
        for (int k = 0; k + 1 < n; k++)
        {
            var (i0, i1, o0, o1) = (inner[k], inner[k + 1], outer[k], outer[k + 1]);
            double len = Math.Sqrt(Math.Pow(i1.U - i0.U, 2) + Math.Pow(i1.V - i0.V, 2));
            int pieces = Math.Max(1, (int)Math.Round(len / (2.5 * w)));
            var normal = normals[k];
            for (int q = 0; q < pieces; q++)
            {
                (double U, double V) L((double U, double V) a, (double U, double V) b, double t) => (a.U + (b.U - a.U) * t, a.V + (b.V - a.V) * t);
                double t0 = q / (double)pieces, t1 = (q + 1) / (double)pieces;
                var a0 = L(i0, i1, t0); var a1 = L(i0, i1, t1); var b0 = L(o0, o1, t0); var b1 = L(o0, o1, t1);
                Quad(DoorwayFramePart.Face, light, Dir(0, 0, -1), P(a0.U, a0.V, -depth), P(b0.U, b0.V, -depth), P(b1.U, b1.V, -depth), P(a1.U, a1.V, -depth));
                Quad(DoorwayFramePart.Face, light, Dir(0, 0, 1), P(a0.U, a0.V, depth), P(b0.U, b0.V, depth), P(b1.U, b1.V, depth), P(a1.U, a1.V, depth));
                Quad(DoorwayFramePart.Side, normal.V > 0.5 ? top : light, Dir(normal.U, normal.V, 0),
                    P(b0.U, b0.V, -depth), P(b0.U, b0.V, depth), P(b1.U, b1.V, depth), P(b1.U, b1.V, -depth));
                Quad(DoorwayFramePart.Side, shade, Dir(-normal.U, -normal.V, 0),
                    P(a0.U, a0.V, -depth), P(a0.U, a0.V, depth), P(a1.U, a1.V, depth), P(a1.U, a1.V, -depth));
            }
        }

        // A block (U from u0 to u1, V from v0 to v1, d to each side of the doorway): front, back, top, both ends, and
        // its underside when it hangs over open space.
        void Box(double u0, double u1, double v0, double v1, double d, bool bottom)
        {
            Quad(DoorwayFramePart.Face, light, Dir(0, 0, -1), P(u0, v0, -d), P(u1, v0, -d), P(u1, v1, -d), P(u0, v1, -d));
            Quad(DoorwayFramePart.Face, light, Dir(0, 0, 1), P(u0, v0, d), P(u1, v0, d), P(u1, v1, d), P(u0, v1, d));
            Quad(DoorwayFramePart.Side, top, Dir(0, 1, 0), P(u0, v1, -d), P(u1, v1, -d), P(u1, v1, d), P(u0, v1, d));
            if (bottom) Quad(DoorwayFramePart.Side, shade, Dir(0, -1, 0), P(u0, v0, -d), P(u1, v0, -d), P(u1, v0, d), P(u0, v0, d));
            Quad(DoorwayFramePart.Side, light, Dir(-1, 0, 0), P(u0, v0, -d), P(u0, v1, -d), P(u0, v1, d), P(u0, v0, d));
            Quad(DoorwayFramePart.Side, light, Dir(1, 0, 0), P(u1, v0, -d), P(u1, v1, -d), P(u1, v1, d), P(u1, v0, d));
        }
        // Footings: blocks round the foot of each post, standing a little proud of it on every side (and 2 units into the
        // opening, so their faces never lie on the post's).
        if (shape.Bases)
        {
            double proud = 6 * scale, height = 22 * scale, inset = 2 * scale;
            double right = opening[0].U, left = opening[^1].U;
            Box(right - inset, right + w + proud, -footing, height, depth + proud, bottom: false);
            Box(left - w - proud, left + inset, -footing, height, depth + proud, bottom: false);
        }
        // A keystone at the top of the opening, standing proud of the arch and above it.
        if (shape.Keystone)
        {
            double apex = opening.Max(p => p.V), half = 10 * scale;
            Box(-half, half, apex - 6 * scale, apex + w + 6 * scale, depth + 4 * scale, bottom: true);
        }
        // A ledge across the top of a gate, wider than its posts.
        if (shape.Lintel)
        {
            double topV = opening.Max(p => p.V) + w, reach = opening.Max(p => p.U) + w + 10 * scale;
            Box(-reach, reach, topV, topV + 12 * scale, depth + 6 * scale, bottom: true);
        }
        return faces;
    }

    /// <summary>The corners in the order that shows the face towards <paramref name="outward"/>: anticlockwise seen from
    /// there (a scene face's front is the side its corners turn anticlockwise from).</summary>
    public static Vector3f[] Orient(Vector3f[] corners, (double X, double Y, double Z) outward)
    {
        double ax = corners[1].X - corners[0].X, ay = corners[1].Y - corners[0].Y, az = corners[1].Z - corners[0].Z;
        double bx = corners[2].X - corners[0].X, by = corners[2].Y - corners[0].Y, bz = corners[2].Z - corners[0].Z;
        double cx = ay * bz - az * by, cy = az * bx - ax * bz, cz = ax * by - ay * bx;
        if (corners.Length == 4 && Math.Abs(cx) + Math.Abs(cy) + Math.Abs(cz) < 1e-9)
        {
            // The first three corners are in a line (a rounded sliver): use the other diagonal.
            bx = corners[3].X - corners[0].X; by = corners[3].Y - corners[0].Y; bz = corners[3].Z - corners[0].Z;
            cx = ay * bz - az * by; cy = az * bx - ax * bz; cz = ax * by - ay * bx;
        }
        return cx * outward.X + cy * outward.Y + cz * outward.Z >= 0 ? corners : [.. corners.Reverse()];
    }
}
