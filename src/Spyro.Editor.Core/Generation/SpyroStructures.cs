using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Generation;

/// <summary>What is built on the land rather than being land: the round towers with pointed roofs that stand at the
/// mouths of paths and round courtyards in the game's own levels.</summary>
internal static class SpyroStructures
{
    /// <summary>A round tower: <paramref name="sides"/> upright faces in bands, a cone roof wider than the wall. Its
    /// foot is sunk into the ground it stands on.</summary>
    public static IEnumerable<TerrainPiece> Tower(double cx, double cy, double foot, double top, double radius, double roof, int sides = 8)
    {
        var ring = new (double X, double Y)[sides];
        for (int k = 0; k < sides; k++)
        {
            double a = 2 * 3.14159265358979 * (k + 0.5) / sides;
            ring[k] = (SpyroPlanner.Cos(a), SpyroPlanner.Sin(a));
        }
        Vector3f P(int k, double r, double z) => new((float)Math.Round(cx + ring[k % sides].X * r), (float)Math.Round(cy + ring[k % sides].Y * r), (float)Math.Round(z));
        int bands = Math.Max(1, (int)Math.Round((top - foot) / 170));
        for (int b = 0; b < bands; b++)
        {
            double z0 = foot + (top - foot) * b / bands, z1 = foot + (top - foot) * (b + 1) / bands;
            double l0 = 0.25 + 0.5 * b / bands, l1 = 0.25 + 0.5 * (b + 1) / bands;
            for (int k = 0; k < sides; k++)
            {
                // Seen from outside the corners turn anticlockwise: top right is the earlier corner of the ring.
                double side = 0.82 + 0.18 * ring[k].X;
                var piece = TerrainMesher.Clean([P(k + 1, radius, z1), P(k, radius, z1), P(k, radius, z0), P(k + 1, radius, z0)], SurfaceRole.Brick,
                    [l1 * side, l1 * side, l0 * side, l0 * side], null, TerrainPlan.None, true);
                if (piece != null) yield return piece;
            }
        }
        var tip = new Vector3f((float)Math.Round(cx), (float)Math.Round(cy), (float)Math.Round(top + roof));
        for (int k = 0; k < sides; k++)
        {
            double side = 0.75 + 0.25 * ring[k].X;
            var piece = TerrainMesher.Clean([tip, P(k, radius * 1.22, top - 8), P(k + 1, radius * 1.22, top - 8)], SurfaceRole.Roof, [0.95 * side, 0.55 * side, 0.55 * side], null, TerrainPlan.None, true);
            if (piece != null) yield return piece with { Collides = false };
        }
    }

    /// <summary>Towers either side of where each path leaves a room, standing on the bank; more round a castle's yards.</summary>
    public static List<TerrainPiece> Build(PlannedLevel L, LevelStyle style, LevelRandom rnd)
    {
        var plan = L.Plan; var pieces = new List<TerrainPiece>();
        if (plan.Kind is EnclosureKind.Sea or EnclosureKind.Sky || style is LevelStyle.Canyon or LevelStyle.Caverns) return pieces;
        bool castle = style == LevelStyle.Castle;
        var placed = new List<(double X, double Y)>();
        void Stand(double x, double y, double tall)
        {
            if (placed.Any(p => (p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y) < 330 * 330)) return;
            double land = plan.Land(x, y);
            if (land < 70) return;
            placed.Add((x, y));
            double ground = plan.Height(TerrainPlan.Enclosure, x, y), floor = plan.Base(x, y);
            double top = Math.Max(ground + 150, floor + tall);
            pieces.AddRange(Tower(x, y, ground - 60, top, castle ? 112 : 100, castle ? 150 : 200));
        }
        foreach (var zone in plan.Zones)
        {
            if (zone.Kind != "path" || zone.Roof > 0) continue;
            int count = 0;
            // Where the path's line leaves each room: a tower on the bank either side, at most at one end of each path
            // (and at both ends in a castle).
            foreach (bool end in new[] { false, true })
            {
                if (!castle && count > 0) break;
                if (!castle && !rnd.Chance(0.7)) continue;
                var s = zone.Spine;
                for (int k = 1; k < s.Count; k++)
                {
                    int i = end ? s.Count - 1 - k : k, before = end ? i + 1 : i - 1;
                    bool inRoom = plan.Zones.Any(z => z.Kind == "room" && z.Sdf(s[i].X, s[i].Y, plan.Noise) < 0);
                    if (inRoom) continue;
                    double tx = s[i].X - s[before].X, ty = s[i].Y - s[before].Y, tl = Math.Sqrt(tx * tx + ty * ty);
                    if (tl <= 0) break;
                    double off = zone.HalfWidth + (castle ? 150 : 170), tall = plan.Rise + Range(rnd, 110, 200);
                    Stand(s[i].X - ty / tl * off, s[i].Y + tx / tl * off, tall);
                    Stand(s[i].X + ty / tl * off, s[i].Y - tx / tl * off, tall);
                    count++;
                    break;
                }
            }
        }
        if (pieces.Count > 0) L.Count("tower", placed.Count);
        return pieces;
    }

    private static double Range(LevelRandom rnd, double a, double b) => a + (b - a) * rnd.NextDouble();
}
