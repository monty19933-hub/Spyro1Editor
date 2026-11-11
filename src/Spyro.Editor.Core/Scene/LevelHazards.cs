using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Scene;

/// <summary>Where Spyro sinks in one of the game's levels: the collision triangles whose ground kind is water, lava or
/// ooze (special surface type 0). The game keeps this on the collision ground, not on the drawn tiles: Beast Makers'
/// swamp, for one, is plain drawn floor over water collision. An egg thief can run there; Spyro can't follow.</summary>
public sealed class LevelHazards
{
    private readonly List<(Vector3f A, Vector3f B, Vector3f C, float X0, float Y0, float X1, float Y1)> _liquid = [];

    public static LevelHazards None { get; } = new([]);

    public int LiquidTriangles => _liquid.Count;
    /// <summary>For study: the liquid ground's extent and its height range (editor units).</summary>
    public string Describe() => _liquid.Count == 0 ? "none" :
        $"x {_liquid.Min(t => t.X0):0}..{_liquid.Max(t => t.X1):0}, y {_liquid.Min(t => t.Y0):0}..{_liquid.Max(t => t.Y1):0}, " +
        $"z {_liquid.Min(t => MathF.Min(t.A.Z, MathF.Min(t.B.Z, t.C.Z))):0}..{_liquid.Max(t => MathF.Max(t.A.Z, MathF.Max(t.B.Z, t.C.Z))):0}; first {_liquid[0].A} {_liquid[0].B} {_liquid[0].C}";

    private LevelHazards(IEnumerable<(Vector3f A, Vector3f B, Vector3f C)> liquid)
    {
        foreach (var (a, b, c) in liquid)
            _liquid.Add((a, b, c, MathF.Min(a.X, MathF.Min(b.X, c.X)), MathF.Min(a.Y, MathF.Min(b.Y, c.Y)),
                MathF.Max(a.X, MathF.Max(b.X, c.X)), MathF.Max(a.Y, MathF.Max(b.Y, c.Y))));
    }

    /// <summary>The level's liquid ground, read from its data (the collision points are in editor units, as the tiles').</summary>
    public static LevelHazards From(UniversalLevelData data, string name)
    {
        var (offset, length) = data.Subfiles()[1];
        var surfaces = PortalSourceDataLocator.ParseTerrainSurfaces(data.Bytes.AsSpan(offset, length).ToArray(), offset, "", name);
        static Vector3f P(PortalSourcePoint p) => new(p.X, p.Y, p.Z);
        return new(surfaces.CollisionSurfaceTriangles.Where(t => t.HasSpecialSurface && t.SurfaceType == 0).Select(t => (P(t.P1), P(t.P2), P(t.P3))));
    }

    /// <summary>Whether (x, y) lies over liquid ground within <paramref name="tolerance"/> of height <paramref name="z"/>.</summary>
    public bool IsLiquid(float x, float y, float z, float tolerance = 48)
    {
        foreach (var t in _liquid)
        {
            if (x < t.X0 - 0.5f || x > t.X1 + 0.5f || y < t.Y0 - 0.5f || y > t.Y1 + 0.5f) continue;
            if (!Barycentric(t.A, t.B, t.C, x, y, out float u, out float v, out float w)) continue;
            float h = u * t.A.Z + v * t.B.Z + w * t.C.Z;
            if (MathF.Abs(h - z) <= tolerance) return true;
        }
        return false;
    }

    private static bool Barycentric(Vector3f a, Vector3f b, Vector3f c, float x, float y, out float u, out float v, out float w)
    {
        float d = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
        u = v = w = 0;
        if (MathF.Abs(d) < 1e-6f) return false;
        u = ((b.Y - c.Y) * (x - c.X) + (c.X - b.X) * (y - c.Y)) / d;
        v = ((c.Y - a.Y) * (x - c.X) + (a.X - c.X) * (y - c.Y)) / d;
        w = 1 - u - v;
        const float e = -0.001f;
        return u >= e && v >= e && w >= e;
    }
}
