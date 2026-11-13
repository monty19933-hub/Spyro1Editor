using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Generation;

/// <summary>A face of one of the game's levels as the level stores it: corners in the game's own order (editor units),
/// texture, the colours it is painted with, and whether its front is its other side or both.</summary>
public sealed record SourceFace(Vector3f[] Corners, int TextureId, ColorRgba[] Near, bool Flip, bool TwoSided);

/// <summary>The terrain of the level a look is taken from, read from the user's own disc (the editor's level cache):
/// the generator lifts that level's towers, trees, rocks and arches out of it and stands them in the new level.</summary>
public sealed record LevelPropSource(string LevelKey, IReadOnlyList<SourceFace> Faces)
{
    public static LevelPropSource FromGeometry(string levelKey, GeometryCandidate geometry)
    {
        var faces = new List<SourceFace>();
        foreach (var p in geometry.Polygons)
        {
            if (p.IsNativeUntexturedSentinel || p.NativePrimitiveSemiTransparent || p.Points.Count < 3 || p.Points.Count > 4) continue;
            if (p.ZValues.Length != p.Points.Count) continue;
            // A triangle is stored as four corners with one doubled (CornerPointIndexes 0, 0, 1, 2): each of its three
            // points takes the paint of the first corner that is it.
            ColorRgba[] near;
            if (p.NearColors.Count == p.Points.Count) near = [.. p.NearColors];
            else if (p.Points.Count == 3 && p.NearColors.Count == 4 && p.CornerPointIndexes.Count == 4 && p.CornerPointIndexes.All(i => i >= 0 && i < 3) &&
                Enumerable.Range(0, 3).All(k => p.CornerPointIndexes.Contains(k)))
                near = [.. Enumerable.Range(0, 3).Select(k => p.NearColors[p.CornerPointIndexes.ToList().IndexOf(k)])];
            else continue;
            var corners = new Vector3f[p.Points.Count];
            for (int k = 0; k < corners.Length; k++) corners[k] = new(p.Points[k].X, p.Points[k].Y, p.ZValues[k]);
            faces.Add(new(corners, p.NativeTextureId, near, p.FaceFlip, (p.NativeFaceWord3 & 0x04) != 0));
        }
        return new(levelKey, faces);
    }
}

/// <summary>Something that stands on a level's ground: a tower, a tree, a rock, an arch. Its faces are the original
/// level's, about its own middle (x, y) and its lowest point (z).</summary>
internal sealed record LevelProp(IReadOnlyList<SourceFace> Faces, double Radius, double Height, IReadOnlySet<int> Textures)
{
    public bool Tall => Height >= 260;
}

internal static class LevelProps
{
    /// <summary>The level's free-standing pieces: groups of faces that are not floor, joined corner to corner, with the
    /// floor faces that lie wholly on them (a tower's top), small enough round and tall enough to be a thing rather than
    /// the land, and made only of textures a built level can carry (<paramref name="usable"/>).</summary>
    public static List<LevelProp> Extract(LevelPropSource source, Func<int, bool> usable)
    {
        var faces = source.Faces;
        static double Up(SourceFace f)
        {
            double nx = 0, ny = 0, nz = 0; var p = f.Corners;
            for (int k = 0; k < p.Length; k++)
            {
                var a = p[k]; var b = p[(k + 1) % p.Length];
                nx += (a.Y - b.Y) * (double)(a.Z + b.Z); ny += (a.Z - b.Z) * (double)(a.X + b.X); nz += (a.X - b.X) * (double)(a.Y + b.Y);
            }
            double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            return len <= 0 ? 0 : (f.Flip ? -nz : nz) / len;
        }
        var floor = new bool[faces.Count];
        var parent = new int[faces.Count];
        for (int i = 0; i < faces.Count; i++) { parent[i] = i; floor[i] = Up(faces[i]) >= 0.72; }
        int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
        var owner = new Dictionary<(int, int, int), int>();
        static (int, int, int) Key(Vector3f v) => ((int)MathF.Round(v.X), (int)MathF.Round(v.Y), (int)MathF.Round(v.Z));
        for (int i = 0; i < faces.Count; i++)
        {
            if (floor[i]) continue;
            foreach (var v in faces[i].Corners)
            {
                var key = Key(v);
                if (owner.TryGetValue(key, out int other)) { int a = Find(i), b = Find(other); if (a != b) parent[a] = b; }
                else owner[key] = i;
            }
        }
        var groups = new Dictionary<int, List<int>>();
        for (int i = 0; i < faces.Count; i++)
        {
            if (floor[i]) continue;
            int root = Find(i);
            if (!groups.TryGetValue(root, out var list)) groups[root] = list = [];
            list.Add(i);
        }
        for (int i = 0; i < faces.Count; i++)
        {
            if (!floor[i]) continue;
            int root = -1; bool whole = true;
            foreach (var v in faces[i].Corners)
            {
                if (!owner.TryGetValue(Key(v), out int o)) { whole = false; break; }
                int r = Find(o);
                if (root >= 0 && r != root) { whole = false; break; }
                root = r;
            }
            if (whole && root >= 0) groups[root].Add(i);
        }
        var props = new List<LevelProp>();
        foreach (var (_, list) in groups.OrderBy(g => g.Key))
        {
            if (list.Count < 6 || list.Count > 130) continue;
            // Mostly floor: a platform or a small island, not a thing that stands on the ground.
            if (list.Count(i => floor[i]) > 0.3 * list.Count) continue;
            var corners = list.SelectMany(i => faces[i].Corners).ToList();
            float x0 = corners.Min(v => v.X), x1 = corners.Max(v => v.X), y0 = corners.Min(v => v.Y), y1 = corners.Max(v => v.Y), z0 = corners.Min(v => v.Z), z1 = corners.Max(v => v.Z);
            if (Math.Max(x1 - x0, y1 - y0) > 620 || z1 - z0 < 90 || Math.Min(x1 - x0, y1 - y0) < 40) continue;
            var textures = list.Select(i => faces[i].TextureId).ToHashSet();
            if (!textures.All(usable)) continue;
            float cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
            var moved = list.Select(i => faces[i] with { Corners = [.. faces[i].Corners.Select(v => new Vector3f(v.X - cx, v.Y - cy, v.Z - z0))] }).ToList();
            props.Add(new(moved, Math.Max(x1 - x0, y1 - y0) / 2, z1 - z0, textures));
        }
        return props;
    }

    /// <summary>The prop stood at a point, turned by an angle, its foot sunk <paramref name="sink"/> units into the ground.</summary>
    public static IEnumerable<TerrainPiece> Place(LevelProp prop, double x, double y, double ground, double angle, double sink)
    {
        double c = SpyroPlanner.Cos(angle), s = SpyroPlanner.Sin(angle);
        foreach (var f in prop.Faces)
        {
            // The game's own faces start at their texture's bottom left (bottom left, bottom right, top right, top
            // left); a built level's start at the top right (top right, top left, bottom left, bottom right). A
            // triangle's doubled corner is its texture's bottom; a built level's triangle shows top left, bottom left,
            // bottom right, so it starts at the game triangle's last corner.
            int[] order = f.Corners.Length == 4 ? [2, 3, 0, 1] : [2, 0, 1];
            var corners = order.Select(k => f.Corners[k]).Select(v => new Vector3f((float)Math.Round(x + v.X * c - v.Y * s), (float)Math.Round(y + v.X * s + v.Y * c), (float)Math.Round(ground - sink + v.Z))).ToArray();
            if (corners.Distinct().Count() != corners.Length) continue;
            if (Degenerate(corners)) continue;
            yield return new TerrainPiece(corners, SurfaceRole.Brick, [.. corners.Select(_ => 0.5)], null, TerrainPlan.None, true)
            { Texture = f.TextureId, Paint = [.. order.Select(k => f.Near[k])], TwoSided = f.TwoSided, FrontReversed = f.Flip };
        }
    }

    private static bool Degenerate(Vector3f[] c)
    {
        static bool Flat(Vector3f a, Vector3f b, Vector3f d)
        {
            double ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z, vx = d.X - a.X, vy = d.Y - a.Y, vz = d.Z - a.Z;
            double x = uy * vz - uz * vy, y = uz * vx - ux * vz, z = ux * vy - uy * vx;
            return x * x + y * y + z * z < 4;
        }
        return Flat(c[0], c[1], c[2]) || c.Length == 4 && Flat(c[0], c[2], c[3]);
    }
}
