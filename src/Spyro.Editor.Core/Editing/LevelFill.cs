using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Editing;

/// <summary>Where "Fill In What the Level Needs" puts a built level's objects: its own dragons, egg thieves (each with a
/// running route), a Return Home vortex and gems. Everything goes on floor Spyro can walk to from the start: the
/// level's floors are laid on a 32-unit grid (several layers where floors overlap), walked from the start (a step or
/// jump up of at most <see cref="StepUp"/>, a drop of at most <see cref="StepDown"/>, over cracks and gaps of a cell,
/// and from an edge a jump and glide in a straight line to lower floor), and only open, flat spots are used.
/// Dragons spread out across the level, Return Home goes to the far end (the longest walk from the start), a thief to
/// open ground about halfway, and gems on the paths in between (or where the level's own game had its gems).</summary>
public static class LevelFill
{
    public const float Cell = 32, StepUp = 72, StepDown = 640;
    /// <summary>Units a glide carries Spyro for each unit he sinks (his glide sinks about 1 in 10; kept short of that).</summary>
    public const float GlideRatio = 8;

    /// <summary>A spot for one object: where it stands and the way it faces (yaw byte).</summary>
    public sealed record Spot(Vector3f Position, int Yaw);
    /// <summary>An egg thief and the points of its route (as many as its own route has).</summary>
    public sealed record ThiefSpot(Vector3f Position, int Yaw, IReadOnlyList<Vector3f> Route);
    public sealed record Plan(IReadOnlyList<Spot> Dragons, IReadOnlyList<ThiefSpot> Thieves, Spot? Exit,
        IReadOnlyList<(Vector3f Position, int Value)> Gems, int ReachableCells, IReadOnlyList<string> Notes)
    {
        /// <summary>Enemies, spread along the way from a little past the start to the far end.</summary>
        public IReadOnlyList<Spot> Enemies { get; init; } = [];
        /// <summary>Animals (Sparx's food), near the start and along the way.</summary>
        public IReadOnlyList<Spot> Animals { get; init; } = [];
        /// <summary>The level's floors, to check routes against (<see cref="FloorGrid.Holds"/>).</summary>
        public FloorGrid? Grid { get; init; }
    }

    /// <summary>What to place: <paramref name="dragons"/> statues, <paramref name="thiefRoutes"/> (one entry per egg thief:
    /// the number of points its route has), a Return Home when <paramref name="exit"/>, and gems worth about
    /// <paramref name="gemTotal"/> (none when 0) at <paramref name="gemSpots"/> when given (the level's own game's gem
    /// places), else spread along the walkable floor, at most <paramref name="maxGems"/> of them (the level's free save rows).
    /// <paramref name="occupied"/>: objects already in the level, kept clear.</summary>
    public static Plan Make(SceneDocument scene, int dragons, IReadOnlyList<int> thiefRoutes, bool exit, int gemTotal,
        IReadOnlyList<Vector3f>? gemSpots = null, IReadOnlyList<Vector3f>? occupied = null, int maxGems = int.MaxValue,
        int enemies = 0, int animals = 0)
    {
        var grid = new FloorGrid(scene);
        var walk = grid.Walk(scene.Spawn);
        List<string> notes = [];
        if (walk.Count == 0) return new([], [], null, [], 0, ["No floor under the start, so nothing was placed."]);
        var spawn = scene.Spawn;
        // Open, flat spots (a statue's or vortex's footprint) that are not on special ground.
        var open = walk.Keys.Where(c => grid.IsOpen(c, 3)).ToList();
        if (open.Count == 0) { notes.Add("No open, flat floor is reachable from the start."); open = [.. walk.Keys]; }
        List<Vector3f> taken = [.. occupied ?? [], spawn];
        bool Clear(Vector3f p, float d) => taken.All(t => Dist2(t, p) >= d * d);
        int far = walk.Values.Max();

        Spot? exitSpot = null;
        if (exit)
        {
            // The far end: the longest walk from the start (among the farthest tenth, the most open).
            var end = open.Where(c => walk[c] >= far * 0.9).OrderByDescending(c => grid.Openness(c)).ThenByDescending(c => walk[c]).FirstOrDefault(open.MaxBy(c => walk[c]));
            var p = grid.Point(end);
            exitSpot = new(p, Facing(p, spawn));
            taken.Add(p);
        }

        List<ThiefSpot> thieves = [];
        foreach (int nodes in thiefRoutes)
        {
            // Open ground about halfway, roomy enough for a loop of about 6 cells' radius.
            var roomy = open.Where(c => grid.Openness(c) >= 6 && Clear(grid.Point(c), 600)).ToList();
            var fallback = open.Where(c => Clear(grid.Point(c), 400)).ToList();
            if (roomy.Count == 0 && fallback.Count == 0) { notes.Add("No room for an egg thief's run."); break; }
            var pick = roomy.Count > 0 ? roomy.MinBy(c => Math.Abs(walk[c] - far * 0.5)) : fallback.MaxBy(c => grid.Openness(c));
            if (roomy.Count == 0) notes.Add("The egg thief's ground is small: check its route (Edit Route…).");
            var p = grid.Point(pick);
            float radius = Math.Clamp(grid.Openness(pick) * Cell * 0.8f, 96, 320);
            var route = Enumerable.Range(0, Math.Max(1, nodes)).Select(i =>
            {
                double a = 2 * Math.PI * i / Math.Max(1, nodes);
                float x = p.X + radius * (float)Math.Cos(a), y = p.Y + radius * (float)Math.Sin(a);
                return grid.FloorNear(x, y, p.Z) is { } z ? new Vector3f(x, y, z) : p;
            }).ToList();
            thieves.Add(new(p, Facing(p, spawn), route));
            taken.Add(p);
        }

        // Dragons: spread out (each as far as possible from the start and the others), on open floor.
        List<Spot> statues = [];
        for (int i = 0; i < dragons; i++)
        {
            // Room for the rescue too: its camera stands about 250 units off, so open floor well round the statue first.
            var spaced = open.Where(c => Clear(grid.Point(c), 360) && grid.Openness(c, 5) >= 5).ToList();
            if (spaced.Count == 0) spaced = open.Where(c => Clear(grid.Point(c), 360)).ToList();
            if (spaced.Count == 0)
            {
                spaced = open.Where(c => Clear(grid.Point(c), 160)).ToList();
                if (spaced.Count > 0) notes.Add("Some dragons stand closer together than usual: the level has little open floor.");
            }
            if (spaced.Count == 0) { notes.Add($"Only room for {statues.Count} of {dragons} dragons."); break; }
            var best = spaced.MaxBy(c => taken.Min(t => Dist2(t, grid.Point(c))) + Cell * Cell * walk[c]);
            var p = grid.Point(best);
            statues.Add(new(p, Facing(p, spawn)));
            taken.Add(p);
        }

        // Enemies along the way (not at the start), animals from the start on; each well apart from the rest.
        List<Spot> Along(int count, double from, float spacing)
        {
            // Evenly along the walk: the k-th goes about k/count of the way from `from` to the far end, on open floor
            // well apart from everything already placed (closer together only if the level is small).
            List<Spot> spots = [];
            var cells = walk.Keys.Where(c => walk[c] >= far * from && grid.IsOpen(c, 2)).ToList();
            if (cells.Count == 0 || count <= 0) return spots;
            for (float gap = spacing; spots.Count < count && gap >= 160; gap *= 0.75f)
                for (int k = 0; k < count && spots.Count < count; k++)
                {
                    double target = far * (from + (1 - from) * (k + 0.5) / count);
                    var pick = cells.Where(c => Clear(grid.Point(c), gap)).OrderBy(c => Math.Abs(walk[c] - target)).Take(1).ToList();
                    if (pick.Count == 0 || Math.Abs(walk[pick[0]] - target) > far * 0.5 / count + 8) continue;
                    var p = grid.Point(pick[0]);
                    spots.Add(new(p, Facing(p, spawn)));
                    taken.Add(p);
                }
            return spots;
        }
        var foes = enemies > 0 ? Along(enemies, 0.15, 520) : [];
        var food = animals > 0 ? Along(animals, 0.0, 420) : [];
        if (foes.Count < enemies) notes.Add($"Room for {foes.Count} of {enemies} enemies.");

        List<(Vector3f, int)> gems = [];
        if (gemTotal > 0)
        {
            List<Vector3f> places;
            if (gemSpots is { Count: > 0 })
                places = [.. gemSpots.Where(g => Clear(g, 120)).Select(g => grid.FloorNear(g.X, g.Y, g.Z + 64) is { } z && Math.Abs(z - g.Z) < 160 ? g with { Z = z } : g)];
            else
            {
                // Along the walkable floor: every so often on the way out from the start.
                int count = Math.Clamp(gemTotal / 6, 8, 70);
                var cells = walk.Keys.Where(c => grid.IsOpen(c, 1)).ToList();
                places = [];
                float spacing = 400;
                while (places.Count < count && spacing >= 96)
                {
                    foreach (var c in cells.OrderBy(c => walk[c]))
                    {
                        var p = grid.Point(c);
                        if (places.Count >= count) break;
                        if (Clear(p, 160) && places.All(q => Dist2(q, p) >= spacing * spacing)) places.Add(p);
                    }
                    spacing *= 0.7f;
                }
            }
            if (places.Count > maxGems)
            {
                // Fewer, evenly picked places (in order of the walk out from the start), worth the same in all.
                var order = places.OrderBy(p => walk.TryGetValue(grid.Nearest(p), out int d) ? d : int.MaxValue).ToList();
                places = [.. Enumerable.Range(0, Math.Max(0, maxGems)).Select(i => order[(int)((long)i * order.Count / Math.Max(1, maxGems))])];
                notes.Add($"Only {maxGems} gems fit the level's save rows, so each is worth more.");
            }
            gems.AddRange(Values(places, gemTotal, spawn));
        }
        return new(statues, thieves, exitSpot, gems, walk.Count, notes) { Enemies = foes, Animals = food, Grid = grid };
    }

    /// <summary>Gem colours worth about <paramref name="total"/> over the places: farther from the start, worth more. Teal
    /// (50) and rainbow (100) gems come in only when the places are too few for the total in the game's own colours.</summary>
    public static IEnumerable<(Vector3f Position, int Value)> Values(IReadOnlyList<Vector3f> places, int total, Vector3f start)
    {
        int[] colours = total > 25 * places.Count * 0.8 ? [1, 2, 5, 10, 25, 50, 100] : [1, 2, 5, 10, 25];
        if (places.Count == 0) yield break;
        var far = places.Select(p => MathF.Sqrt(Dist2(p, start))).ToArray();
        float max = Math.Max(1, far.Max());
        var weight = far.Select(d => 1 + 2 * d / max).ToArray();
        float sum = weight.Sum();
        int[] value = [.. weight.Select(w => colours.MinBy(c => Math.Abs(c - total * w / sum)))];
        // Nudge one gem at a time toward the total (up where it's short, down where it's over).
        for (int guard = 0; guard < 4 * places.Count && value.Sum() != total; guard++)
        {
            int diff = total - value.Sum(), best = -1, bestNext = 0, bestGap = Math.Abs(diff);
            for (int i = 0; i < value.Length; i++)
            {
                int k = Array.IndexOf(colours, value[i]);
                int next = diff > 0 ? (k + 1 < colours.Length ? colours[k + 1] : -1) : (k > 0 ? colours[k - 1] : -1);
                if (next < 0) continue;
                int gap = Math.Abs(diff - (next - value[i]));
                if (gap < bestGap || gap == bestGap && best >= 0 && (diff > 0 ? far[i] > far[best] : far[i] < far[best])) { best = i; bestNext = next; bestGap = gap; }
            }
            if (best < 0) break;
            value[best] = bestNext;
        }
        // Still off by a little: one gem, or two, of another colour makes it exact.
        int off = total - value.Sum();
        if (off != 0)
        {
            bool done = false;
            for (int i = 0; i < value.Length && !done; i++)
                foreach (int c in colours)
                    if (c - value[i] == off) { value[i] = c; done = true; break; }
            for (int i = 0; i < value.Length && !done; i++)
                for (int j = i + 1; j < value.Length && !done; j++)
                    foreach (int a in colours)
                    {
                        int b = off - (a - value[i]) + value[j];
                        if (Array.IndexOf(colours, b) >= 0) { value[i] = a; value[j] = b; done = true; break; }
                    }
        }
        for (int i = 0; i < places.Count; i++) yield return (places[i], value[i]);
    }

    /// <summary>An object's route (<paramref name="route"/>: its points as it came, around <paramref name="home"/>) fitted
    /// to <paramref name="spot"/>: the same shape, shrunk until it fits. A walking route stays on plain floor within 64
    /// units of the spot's height; a flying one (every point well above its owner) stays over the level's floors at
    /// its own height above them. Null when even a quarter-size route doesn't fit.</summary>
    public static IReadOnlyList<Vector3f>? FitRoute(FloorGrid grid, Vector3f home, IReadOnlyList<Vector3f> route, Vector3f spot)
    {
        bool flying = route.All(p => p.Z > home.Z + 150);
        foreach (float scale in new[] { 1f, 0.75f, 0.5f, 0.35f, 0.25f })
        {
            List<Vector3f> fitted = [];
            foreach (var p in route)
            {
                float x = spot.X + (p.X - home.X) * scale, y = spot.Y + (p.Y - home.Y) * scale;
                if (flying)
                {
                    if (grid.Floors(x, y).Count == 0) break;
                    fitted.Add(new(x, y, spot.Z + (p.Z - home.Z)));
                    continue;
                }
                if (grid.FloorNear(x, y, spot.Z + 64) is not { } z || Math.Abs(z - spot.Z) > 64 ||
                    !grid.Floors(x, y).Any(f => f.Plain && Math.Abs(f.Z - z) < 1)) break;
                fitted.Add(new(x, y, z));
            }
            if (fitted.Count == route.Count) return fitted;
        }
        return null;
    }

    /// <summary>The yaw byte that turns an object at <paramref name="from"/> to face <paramref name="to"/>.</summary>
    public static int Facing(Vector3f from, Vector3f to) =>
        (int)Math.Round(Math.Atan2(to.Y - from.Y, to.X - from.X) / (2 * Math.PI) * 256) & 0xFF;

    private static float Dist2(Vector3f a, Vector3f b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);

    /// <summary>Floors on a grid: per cell the heights of the floors above it (faces turned more than about 40° up), and
    /// whether that floor is plain ground (no water, lava or other special ground).</summary>
    public sealed class FloorGrid
    {
        private readonly Dictionary<(int, int), List<(float Z, bool Plain)>> _cells = [];
        public FloorGrid(SceneDocument scene)
        {
            foreach (var s in scene.Surfaces)
            {
                var v = s.Vertices;
                if (v.Length < 3) continue;
                for (int t = 1; t + 1 < v.Length; t++) Add(v[0], v[t], v[t + 1], s.Kind == null, s.TwoSided ? 0 : s.FrontReversed ? -1 : 1);
            }
            foreach (var list in _cells.Values) list.Sort((a, b) => a.Z.CompareTo(b.Z));
        }
        /// <summary><paramref name="side"/>: 1 the front is the anticlockwise side, -1 the clockwise side, 0 both
        /// (a floor is a face whose shown side looks up).</summary>
        private void Add(Vector3f a, Vector3f b, Vector3f c, bool plain, int side)
        {
            float ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z, vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z;
            float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
            float len = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
            if (len == 0 || (side == 0 ? Math.Abs(nz) : side * nz) / len < 0.75f) return;
            int x0 = (int)MathF.Floor(Math.Min(a.X, Math.Min(b.X, c.X)) / Cell), x1 = (int)MathF.Floor(Math.Max(a.X, Math.Max(b.X, c.X)) / Cell);
            int y0 = (int)MathF.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y)) / Cell), y1 = (int)MathF.Floor(Math.Max(a.Y, Math.Max(b.Y, c.Y)) / Cell);
            for (int i = x0; i <= x1; i++)
                for (int j = y0; j <= y1; j++)
                {
                    float px = (i + 0.5f) * Cell, py = (j + 0.5f) * Cell;
                    // Barycentric inside test, then the plane's height.
                    float d = ux * vy - uy * vx;
                    if (d == 0) continue;
                    float s = ((px - a.X) * vy - (py - a.Y) * vx) / d, t = (ux * (py - a.Y) - uy * (px - a.X)) / d;
                    if (s < -0.001f || t < -0.001f || s + t > 1.001f) continue;
                    float z = a.Z + s * uz + t * vz;
                    if (!_cells.TryGetValue((i, j), out var list)) _cells[(i, j)] = list = [];
                    int near = list.FindIndex(e => Math.Abs(e.Z - z) < 4);
                    if (near < 0) list.Add((z, plain));
                    else if (!plain) list[near] = (list[near].Z, false);
                }
        }

        /// <summary>A floor in a cell: the cell and the floor's index there (lowest first).</summary>
        public readonly record struct Place(int X, int Y, int Layer);

        public Vector3f Point(Place p) => new((p.X + 0.5f) * Cell, (p.Y + 0.5f) * Cell, _cells[(p.X, p.Y)][p.Layer].Z);

        /// <summary>The floor place under <paramref name="p"/> nearest its height (layer 0 when the cell has none).</summary>
        public Place Nearest(Vector3f p)
        {
            int x = (int)MathF.Floor(p.X / Cell), y = (int)MathF.Floor(p.Y / Cell);
            if (!_cells.TryGetValue((x, y), out var list) || list.Count == 0) return new(x, y, 0);
            int best = 0;
            for (int i = 1; i < list.Count; i++) if (Math.Abs(list[i].Z - p.Z) < Math.Abs(list[best].Z - p.Z)) best = i;
            return new(x, y, best);
        }

        /// <summary>Whether every point has plain floor under it within <paramref name="tolerance"/> units of its height (a
        /// route that stays on the ground).</summary>
        public bool Holds(IEnumerable<Vector3f> points, float tolerance = 96) =>
            points.All(p => Floors(p.X, p.Y).Any(f => f.Plain && Math.Abs(f.Z - p.Z) <= tolerance));

        /// <summary>The floors over (x, y), lowest first, and whether each is plain ground.</summary>
        public IReadOnlyList<(float Z, bool Plain)> Floors(float x, float y) =>
            _cells.TryGetValue(((int)MathF.Floor(x / Cell), (int)MathF.Floor(y / Cell)), out var list) ? list : [];

        /// <summary>The floor at (x, y) nearest below <paramref name="z"/> (or nearest at all), or null.</summary>
        public float? FloorNear(float x, float y, float z)
        {
            if (!_cells.TryGetValue(((int)MathF.Floor(x / Cell), (int)MathF.Floor(y / Cell)), out var list) || list.Count == 0) return null;
            var below = list.Where(e => e.Z <= z + 8).ToList();
            return below.Count > 0 ? below[^1].Z : list.MinBy(e => Math.Abs(e.Z - z)).Z;
        }

        private Place? At(int x, int y, float z, float up, float down)
        {
            if (!_cells.TryGetValue((x, y), out var list)) return null;
            int best = -1;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Plain && list[i].Z <= z + up && list[i].Z >= z - down) best = i;   // the highest it can reach
            return best < 0 ? null : new Place(x, y, best);
        }

        /// <summary>Every floor walkable from <paramref name="start"/>, with its walking distance in cells.</summary>
        public Dictionary<Place, int> Walk(Vector3f start)
        {
            Dictionary<Place, int> seen = [];
            var first = At((int)MathF.Floor(start.X / Cell), (int)MathF.Floor(start.Y / Cell), start.Z, 8, 400);
            if (first is not { } s) return seen;
            var queue = new Queue<Place>();
            seen[s] = 0; queue.Enqueue(s);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                float z = _cells[(c.X, c.Y)][c.Layer].Z;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        // The next cell, or (over a crack or a gap Spyro jumps) the one after it when the next has no floor.
                        int step = 1;
                        var n = At(c.X + dx, c.Y + dy, z, StepUp, StepDown);
                        if (n == null && !_cells.ContainsKey((c.X + dx, c.Y + dy))) { n = At(c.X + 2 * dx, c.Y + 2 * dy, z, StepUp, StepDown); step = 2; }
                        if (n is not { } next)
                        {
                            // An edge (nothing next to it but a drop, a pit or special ground): jump and glide straight on,
                            // landing on the first plain floor under the glide; anything higher on the way is a wall.
                            next = default; step = 0;
                            for (int k = 1; k <= 48; k++)
                            {
                                float height = z + StepUp - k * Cell / GlideRatio;
                                var under = _cells.TryGetValue((c.X + k * dx, c.Y + k * dy), out var list) ? list : [];
                                if (under.Any(e => e.Z > height)) break;
                                if (k >= 2 && At(c.X + k * dx, c.Y + k * dy, height, 0, height - (z - StepDown)) is { } land) { next = land; step = k; break; }
                                if (height < z - StepDown) break;
                            }
                            if (step == 0) continue;
                        }
                        if (seen.ContainsKey(next)) continue;
                        seen[next] = seen[c] + step;
                        queue.Enqueue(next);
                    }
            }
            return seen;
        }

        /// <summary>Plain floor within <paramref name="radius"/> cells all round, within 12 units of this one's height.</summary>
        public bool IsOpen(Place p, int radius) => Openness(p, radius) >= radius;

        /// <summary>How many cells out (up to <paramref name="limit"/>) the floor stays plain and level all round.</summary>
        public int Openness(Place p, int limit = 10)
        {
            float z = _cells[(p.X, p.Y)][p.Layer].Z;
            if (!_cells[(p.X, p.Y)][p.Layer].Plain) return 0;
            for (int r = 1; r <= limit; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r || dx * dx + dy * dy > r * r + r) continue;
                        if (!_cells.TryGetValue((p.X + dx, p.Y + dy), out var list) || !list.Any(e => e.Plain && Math.Abs(e.Z - z) <= 12) ||
                            list.Any(e => e.Z > z + 12 && e.Z < z + 160))   // a ledge or roof just overhead is not open
                            return r - 1;
                    }
            return limit;
        }
    }
}
