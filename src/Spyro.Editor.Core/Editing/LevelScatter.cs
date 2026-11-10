using Spyro.Editor.Core.Generation;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Editing;

/// <summary>Random objects for a level you built, placed the way the game's levels place theirs: the way out at the far
/// end, dragons spread through the rooms, an egg thief with room to run, enemies waiting in the rooms beyond the first
/// (facing the way Spyro comes in) and on the paths between, animals grazing in little groups, baskets and chests by
/// the walls and in far corners, an extra life and a locked chest tucked away with its key somewhere else, and gems in
/// trails along the way, in rings in the open and on the ledges and lookouts. The gems, with what the chests and
/// enemies give, come to exactly the treasure asked for, in every colour. Nothing is put inside a tower or against a
/// wall, nor in a homeworld's doorways or on the floor in front of them. Every press of Randomize is another seed,
/// so another arrangement; the same seed gives the same one. Like <see cref="LevelFill"/> it reads the level's floor,
/// so it works on any built level.</summary>
public static class LevelScatter
{
    /// <summary>What a placed thing is.</summary>
    public enum Kind
    {
        Dragon, Exit, Thief, Enemy, Animal,
        /// <summary>A basket or vase: flame or charge opens it.</summary>
        Basket,
        /// <summary>A strong chest: only a charge opens it.</summary>
        StrongChest,
        /// <summary>A chest that takes three flames.</summary>
        FlameChest,
        Firework, SpringChest, LifeChest, LockedChest, Key, Egg,
    }

    /// <summary>The chests <see cref="Make"/> may use (the level's object set has them).</summary>
    public static readonly IReadOnlyList<Kind> Chests = [Kind.Basket, Kind.StrongChest, Kind.FlameChest, Kind.Firework, Kind.SpringChest, Kind.LifeChest, Kind.LockedChest];

    /// <summary>How much to place. <paramref name="Treasure"/>: the level's gems in all, counted as the game counts them
    /// (red 1, green 2, blue 5, gold 10, pink 25), with what chests and enemies give. <paramref name="MaxObjects"/>: the
    /// level's free save rows (every object takes one, a dragon three).</summary>
    public sealed record Request(long Seed, int Enemies, int Animals, int Treasure, int MaxObjects)
    {
        public int Dragons { get; init; }
        /// <summary>One entry per egg thief: the number of points its run has.</summary>
        public IReadOnlyList<int> ThiefRoutes { get; init; } = [];
        public bool Exit { get; init; }
        /// <summary>The kinds of chest to use (<see cref="Chests"/>); a locked chest comes with its key.</summary>
        public IReadOnlySet<Kind> ChestKinds { get; init; } = new HashSet<Kind>();
        public int Eggs { get; init; }
        /// <summary>How hard the arrangement is: harder ones have enemies closer to where Spyro comes into a room and more
        /// of them together, more of the treasure behind enemies and chests, and the best gems at the hardest places.
        /// (How many enemies and animals, and whether there is an extra life, is the caller's to choose.)</summary>
        public LevelDifficulty Difficulty { get; init; } = LevelDifficulty.Medium;
    }

    /// <summary>One placed thing: where, which way it faces, what it gives when opened or beaten (gems, 0: nothing), and
    /// for a thief its run.</summary>
    public sealed record Item(Kind Kind, Vector3f Position, int Yaw, int Reward = 0)
    {
        public IReadOnlyList<Vector3f>? Route { get; init; }
    }

    public sealed record Plan(IReadOnlyList<Item> Items, IReadOnlyList<(Vector3f Position, int Value)> Gems, int Rooms, IReadOnlyList<string> Notes)
    {
        /// <summary>The level's floors, to check creatures' routes against (<see cref="LevelFill.FitRoute"/>).</summary>
        public LevelFill.FloorGrid? Grid { get; init; }
        public IEnumerable<Item> Of(Kind kind) => Items.Where(i => i.Kind == kind);
        /// <summary>How hard each gem's place is to reach (one weight per gem of <see cref="Gems"/>), for
        /// <see cref="Mix"/> when the gems must come to another total after all.</summary>
        public IReadOnlyList<double> GemHardness { get; init; } = [];
        /// <summary>The gems the plan comes to: loose ones and what chests and enemies give.</summary>
        public int Treasure => Gems.Sum(g => g.Value) + Items.Sum(i => i.Reward);
    }

    /// <summary>A stretch of open floor: its cells, the most open one (its middle), where the walk from the start first
    /// reaches it (its way in), and how far that walk is.</summary>
    private sealed class Room
    {
        public readonly List<LevelFill.FloorGrid.Place> Cells = [];
        public LevelFill.FloorGrid.Place Middle, Entry;
        public int Depth;
        public Vector3f MiddlePoint, EntryPoint;
    }

    /// <summary>The level's upright faces (walls, towers, trunks, the sides of rocks): what stands on a floor without
    /// being one. The floor grid sees only floors, so the ground under a tower looks as open as a meadow.</summary>
    private sealed class Uprights
    {
        private readonly record struct Tri(Vector3f A, Vector3f B, Vector3f C, float Nx, float Ny, bool Sided);
        private readonly Dictionary<(int, int), List<(float Lo, float Hi)>> _spans = [];
        private readonly Dictionary<int, List<Tri>> _rows = [], _columns = [];
        private readonly Dictionary<Vector3f, bool> _inside = [];
        private const float Band = 64, Reach = 700;

        public Uprights(SceneDocument scene)
        {
            var own = new Dictionary<(int, int), (float Lo, float Hi)>();
            foreach (var s in scene.Surfaces)
            {
                var v = s.Vertices;
                for (int t = 1; t + 1 < v.Length; t++)
                {
                    Vector3f a = v[0], b = v[t], c = v[t + 1];
                    float ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z, vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z;
                    float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                    float len = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
                    if (len < 1 || Math.Abs(nz) / len >= 0.6f) continue;
                    // Every column of the floor grid the face passes through, and how high it stands there.
                    own.Clear();
                    int steps = Math.Max(1, (int)MathF.Ceiling(Math.Max(MathF.Sqrt(ux * ux + uy * uy + uz * uz), MathF.Sqrt(vx * vx + vy * vy + vz * vz)) / 12));
                    for (int i = 0; i <= steps; i++)
                        for (int j = 0; i + j <= steps; j++)
                        {
                            float f = (float)i / steps, g = (float)j / steps, z = a.Z + f * uz + g * vz;
                            var key = ((int)MathF.Floor((a.X + f * ux + g * vx) / LevelFill.Cell), (int)MathF.Floor((a.Y + f * uy + g * vy) / LevelFill.Cell));
                            own[key] = own.TryGetValue(key, out var span) ? (Math.Min(span.Lo, z), Math.Max(span.Hi, z)) : (z, z);
                        }
                    foreach (var (key, span) in own)
                    {
                        if (!_spans.TryGetValue(key, out var list)) _spans[key] = list = [];
                        list.Add(span);
                    }
                    int side = s.TwoSided ? 0 : s.FrontReversed ? -1 : 1;
                    var tri = new Tri(a, b, c, side * nx, side * ny, side != 0);
                    void Index(Dictionary<int, List<Tri>> bands, float lo, float hi)
                    {
                        for (int k = (int)MathF.Floor(lo / Band); k <= (int)MathF.Floor(hi / Band); k++)
                        {
                            if (!bands.TryGetValue(k, out var list)) bands[k] = list = [];
                            list.Add(tri);
                        }
                    }
                    Index(_rows, Math.Min(a.Y, Math.Min(b.Y, c.Y)), Math.Max(a.Y, Math.Max(b.Y, c.Y)));
                    Index(_columns, Math.Min(a.X, Math.Min(b.X, c.X)), Math.Max(a.X, Math.Max(b.X, c.X)));
                }
            }
        }

        /// <summary>Whether something upright stands at <paramref name="p"/> or right next to it, from Spyro's knees to
        /// over his head (the wall below a ledge he stands on does not count).</summary>
        public bool Near(Vector3f p)
        {
            int x = (int)MathF.Floor(p.X / LevelFill.Cell), y = (int)MathF.Floor(p.Y / LevelFill.Cell);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (_spans.TryGetValue((x + dx, y + dy), out var list) && list.Any(e => e.Hi > p.Z + 14 && e.Lo < p.Z + 100)) return true;
            return false;
        }

        /// <summary>Whether <paramref name="p"/> is inside something solid (a tower, a rock): looking north, south, east
        /// and west from there, the nearest wall shows its back.</summary>
        public bool Inside(Vector3f p)
        {
            if (_inside.TryGetValue(p, out bool known)) return known;
            float z = p.Z + 40;
            int behind = 0;
            foreach (var (alongX, sign) in new[] { (true, 1f), (true, -1f), (false, 1f), (false, -1f) })
            {
                float u = alongX ? p.Y : p.X, from = alongX ? p.X : p.Y, best = Reach;
                bool back = false;
                if (!(alongX ? _rows : _columns).TryGetValue((int)MathF.Floor(u / Band), out var tris)) continue;
                foreach (var t in tris)
                {
                    // The face seen along the ray (is the ray's line inside it?), then how far along the ray it stands.
                    float au = alongX ? t.A.Y : t.A.X, bu = (alongX ? t.B.Y : t.B.X) - au, cu = (alongX ? t.C.Y : t.C.X) - au;
                    float bz = t.B.Z - t.A.Z, cz = t.C.Z - t.A.Z, d = bu * cz - cu * bz;
                    if (Math.Abs(d) < 0.001f) continue;
                    float f = ((u - au) * cz - cu * (z - t.A.Z)) / d, g = (bu * (z - t.A.Z) - (u - au) * bz) / d;
                    if (f < 0 || g < 0 || f + g > 1) continue;
                    float aw = alongX ? t.A.X : t.A.Y, bw = alongX ? t.B.X : t.B.Y, cw = alongX ? t.C.X : t.C.Y;
                    float along = (aw + f * (bw - aw) + g * (cw - aw) - from) * sign;
                    if (along <= 0 || along >= best) continue;
                    best = along;
                    back = t.Sided && (alongX ? t.Nx : t.Ny) * sign > 0;
                }
                if (back) behind++;
            }
            return _inside[p] = behind >= 3;
        }

        /// <summary>Free of walls: not in one, not against one.</summary>
        public bool Open(Vector3f p) => !Near(p) && !Inside(p);
    }

    private static float Dist2(Vector3f a, Vector3f b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z) / 4;
    private static double Range(LevelRandom rnd, double a, double b) => a + (b - a) * rnd.NextDouble();
    public static readonly int[] Colours = [1, 2, 5, 10, 25];

    /// <param name="hints">Good places for gems the level's maker left (<see cref="SceneDocument.GemSpots"/>: a generated
    /// level's paths, ledges, lookouts and stepping stones; an imported level's own gem places).</param>
    /// <param name="occupied">Objects already in the level, kept clear.</param>
    public static Plan Make(SceneDocument scene, Request request, IReadOnlyList<Vector3f>? hints = null, IReadOnlyList<Vector3f>? occupied = null)
    {
        var grid = new LevelFill.FloorGrid(scene);
        var upright = new Uprights(scene);
        var walk = grid.Walk(scene.Spawn);
        List<string> notes = [];
        if (walk.Count == 0) return new([], [], 0, ["No floor under the start, so nothing was placed."]) { Grid = grid };
        var rnd = new LevelRandom(request.Seed);
        var spawn = scene.Spawn;
        float cell = LevelFill.Cell;
        // In a fixed order (the walk's own order differs from run to run), so a seed always gives the same level.
        var order = walk.Keys.OrderBy(c => c.X).ThenBy(c => c.Y).ThenBy(c => c.Layer).ToList();
        var open = order.ToDictionary(c => c, c => grid.Openness(c, 8));
        var point = order.ToDictionary(c => c, grid.Point);
        var byColumn = order.GroupBy(c => (c.X, c.Y)).ToDictionary(g => g.Key, g => g.ToList());
        int far = walk.Values.Max();
        IEnumerable<LevelFill.FloorGrid.Place> Around(LevelFill.FloorGrid.Place c)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0 || !byColumn.TryGetValue((c.X + dx, c.Y + dy), out var list)) continue;
                    foreach (var n in list) if (Math.Abs(point[n].Z - point[c].Z) <= 40) yield return n;
                }
        }

        // Rooms: stretches of floor open at least four cells all round, joined cell to cell.
        var rooms = new List<Room>();
        var roomOf = new Dictionary<LevelFill.FloorGrid.Place, Room>();
        foreach (var start in order)
        {
            if (open[start] < 4 || roomOf.ContainsKey(start)) continue;
            var room = new Room();
            var queue = new Queue<LevelFill.FloorGrid.Place>();
            queue.Enqueue(start); roomOf[start] = room;
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                room.Cells.Add(c);
                foreach (var n in Around(c))
                    if (open[n] >= 4 && !roomOf.ContainsKey(n)) { roomOf[n] = room; queue.Enqueue(n); }
            }
            if (room.Cells.Count < 30) { foreach (var c in room.Cells) roomOf.Remove(c); continue; }
            room.Cells.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.Layer.CompareTo(b.Layer));
            float mx = room.Cells.Average(c => point[c].X), my = room.Cells.Average(c => point[c].Y);
            room.Middle = room.Cells.OrderByDescending(c => open[c]).ThenBy(c => (point[c].X - mx) * (point[c].X - mx) + (point[c].Y - my) * (point[c].Y - my)).First();
            room.Entry = room.Cells.OrderBy(c => walk[c]).First();
            room.Depth = walk[room.Middle];
            room.MiddlePoint = point[room.Middle]; room.EntryPoint = point[room.Entry];
            rooms.Add(room);
        }
        rooms.Sort((a, b) => a.Depth.CompareTo(b.Depth));
        var first = rooms.Count == 0 ? null : rooms.MinBy(r => Dist2(r.MiddlePoint, spawn));
        if (rooms.Count == 0) notes.Add("The level has no open floor to speak of, so things stand where they fit.");
        // A room's fringe: the floor between its open middle and its walls.
        var fringe = new Dictionary<LevelFill.FloorGrid.Place, Room>();
        var spread = new Queue<(LevelFill.FloorGrid.Place Cell, int Steps)>();
        foreach (var room in rooms) foreach (var c in room.Cells) spread.Enqueue((c, 0));
        while (spread.Count > 0)
        {
            var (c, steps) = spread.Dequeue();
            var room = roomOf.TryGetValue(c, out var own) ? own : fringe[c];
            if (steps >= 6) continue;
            foreach (var n in Around(c))
                if (!roomOf.ContainsKey(n) && !fringe.ContainsKey(n)) { fringe[n] = room; spread.Enqueue((n, steps + 1)); }
        }
        // Where a walk ends (a far corner, the top of a ledge), the floor by rooms' walls, and small rooms off the way (nooks).
        var ends = order.Where(c => open[c] >= 1 && walk[c] > 30 && Around(c).All(n => walk[n] <= walk[c])).ToList();
        var edges = order.Where(c => open[c] is 1 or 2 && fringe.ContainsKey(c) && walk[c] > 24).ToList();
        var nooks = rooms.Where(r => r != first && r.Cells.Count < 260).ToList();

        // A maker's place, on the plain floor under it (none there: not used).
        Vector3f? OnFloor(Vector3f g)
        {
            var floors = grid.Floors(g.X, g.Y).Where(f => f.Plain && Math.Abs(f.Z - g.Z) <= 120).ToList();
            return floors.Count == 0 ? null : g with { Z = floors.MinBy(f => Math.Abs(f.Z - g.Z)).Z };
        }
        var places0 = hints?.Select(OnFloor).Where(p => p != null).Select(p => p!.Value).ToList() ?? [];
        List<Vector3f> taken = [.. occupied ?? [], spawn];
        // A homeworld's doorways: nothing stands in one, nor on the floor in front where Spyro walks in and comes back out.
        foreach (var door in scene.Portals ?? [])
        {
            var (fx, fy) = DoorwayShapes.Facing(door.Heading);
            for (float t = 0; t <= 720; t += 120) taken.Add(new(door.Position.X - (float)fx * t, door.Position.Y - (float)fy * t, door.Position.Z));
        }
        bool Clear(Vector3f p, float d) => taken.All(t => Dist2(t, p) >= d * d) && upright.Open(p);
        T? Pick<T>(IReadOnlyList<T> items, Func<T, double> weight) where T : struct =>
            items.Count == 0 ? null : rnd.Pick<T>([.. items.Select(i => (i, Math.Max(0.0001, weight(i))))]);
        List<Item> items = [];
        Item Put(Kind kind, Vector3f p, Vector3f facing, int reward = 0)
        {
            var item = new Item(kind, p, LevelFill.Facing(p, facing), reward);
            items.Add(item); taken.Add(p);
            return item;
        }
        int rows = request.MaxObjects;
        // How far enemies keep from a room's way in and from the start, how far apart guards stand on the paths, how many
        // wait together in a room, how much of the treasure enemies and chests hold, and how much better the gems at
        // hard places are.
        var (enemyGap, startGap, guardGap, together, held, bias) = request.Difficulty switch
        {
            LevelDifficulty.Easy => (260f, 560f, 480f, 3, 0.45, 0.8),
            LevelDifficulty.Hard => (190f, 480f, 340f, 5, 0.6, 1.3),
            LevelDifficulty.Kaizo => (150f, 440f, 280f, 6, 0.65, 1.6),
            _ => (220f, 520f, 420f, 4, 0.55, 1.0),
        };

        // The way out: in the room farthest along (or the one before it), on its far side.
        if (request.Exit && rows > 0)
        {
            var last = rooms.Count == 0 ? null : rooms.Count > 2 && rnd.Chance(0.25) ? rooms[^2] : rooms[^1];
            var cells = (last?.Cells ?? order).Where(c => open[c] >= 3 && Clear(point[c], 260)).ToList();
            if (cells.Count == 0) cells = [.. order.Where(c => open[c] >= 2 && walk[c] >= far * 0.8)];
            int deep = cells.Count == 0 ? 0 : cells.Max(c => walk[c]);
            if (Pick([.. cells.Where(c => walk[c] >= deep - 6)], c => open[c]) is { } spot) { Put(Kind.Exit, point[spot], last?.EntryPoint ?? spawn); rows--; }
            else notes.Add("No open floor for Return Home.");
        }

        // An egg thief: roomy open ground past the first room, with a loop to run.
        foreach (int nodes in request.ThiefRoutes)
        {
            if (rows <= 0) break;
            var roomy = order.Where(c => open[c] >= 6 && (first == null || !first.Cells.Contains(c) || rooms.Count == 1) && Clear(point[c], 520)).ToList();
            if (roomy.Count == 0) roomy = [.. order.Where(c => open[c] >= 4 && Clear(point[c], 400))];
            if (Pick(roomy, c => open[c]) is not { } spot) { notes.Add("No room for an egg thief's run."); break; }
            var p = point[spot];
            float radius = Math.Clamp(open[spot] * cell * 0.8f, 96, 320);
            double turn = rnd.NextDouble() * 6.2832;
            var route = Enumerable.Range(0, Math.Max(1, nodes)).Select(i =>
            {
                double a = turn + 6.2832 * i / Math.Max(1, nodes);
                float x = p.X + radius * (float)SpyroPlanner.Cos(a), y = p.Y + radius * (float)SpyroPlanner.Sin(a);
                return grid.FloorNear(x, y, p.Z) is { } z && upright.Open(new(x, y, z)) ? new Vector3f(x, y, z) : p;
            }).ToList();
            items.Add(new Item(Kind.Thief, p, LevelFill.Facing(p, spawn)) { Route = route });
            taken.Add(p); rows--;
        }

        // Dragons: one to a room while there are rooms, in the open with space for the rescue.
        if (request.Dragons > 0)
        {
            var turns = rooms.ToList();
            rnd.Shuffle(turns);
            for (int n = 0; n < request.Dragons && rows >= 3; n++)
            {
                var room = turns.Count > 0 ? turns[n % turns.Count] : null;
                var cells = (room?.Cells ?? order).Where(c => open[c] >= 5 && Clear(point[c], 360)).ToList();
                if (cells.Count == 0) cells = [.. order.Where(c => open[c] >= 4 && Clear(point[c], 300))];
                if (Pick(cells, c => (double)open[c] * open[c]) is not { } spot) { notes.Add($"Only room for {n} of {request.Dragons} dragons."); break; }
                Put(Kind.Dragon, point[spot], room?.EntryPoint ?? spawn); rows -= 3;
            }
        }

        // Enemies: in each room past the first, a few together about its middle, facing the way in; then the odd one
        // standing guard on a path. More rooms than enemies: the deeper rooms first.
        var beyond = rooms.Where(r => r != first).ToList();
        if (beyond.Count == 0 && first != null) beyond = [first];
        int enemies = Math.Min(request.Enemies, Math.Max(0, rows / 3));
        if (enemies > 0 && beyond.Count > 0)
        {
            int left = enemies;
            var share = beyond.Select(r => Math.Sqrt(r.Cells.Count)).ToList();
            double all = share.Sum();
            var counts = share.Select(s => Math.Max(1, (int)Math.Round(enemies * 0.8 * s / all))).ToList();
            for (int k = beyond.Count - 1; k >= 0 && left > 0; k--)
            {
                var room = beyond[k];
                int want = Math.Min(left, Math.Min(together, counts[k]));
                // Not in the doorway: Spyro gets to step in first.
                var cells = room.Cells.Where(c => open[c] >= 2 && Dist2(point[c], room.EntryPoint) >= enemyGap * enemyGap && Dist2(point[c], spawn) >= startGap * startGap).ToList();
                LevelFill.FloorGrid.Place? anchor = null;
                for (int n = 0; n < want; n++)
                {
                    var near = anchor is { } a ? cells.Where(c => Dist2(point[c], point[a]) <= 300 * 300).ToList() : cells;
                    if (Pick([.. near.Where(c => Clear(point[c], 170))], c => (double)open[c] * open[c]) is not { } spot) break;
                    anchor ??= spot;
                    Put(Kind.Enemy, point[spot], room.EntryPoint); left--;
                }
            }
            // On the paths: narrow floor between rooms, well apart.
            var paths = order.Where(c => !roomOf.ContainsKey(c) && open[c] is >= 1 and <= 3 && walk[c] > 20 && Dist2(point[c], spawn) >= 600 * 600).ToList();
            for (int tries = 0; left > 0 && tries < 4 * enemies; tries++)
            {
                if (Pick([.. paths.Where(c => Clear(point[c], guardGap))], c => 1.0) is not { } spot) break;
                Put(Kind.Enemy, point[spot], spawn); left--;
            }
            if (left > 0) notes.Add($"Room for {enemies - left} of {enemies} enemies.");
            rows -= enemies - left;
        }

        // Animals: little groups in the open, the first where Spyro starts.
        int animals = Math.Min(request.Animals, Math.Max(0, rows / 4)), placedAnimals = 0;
        for (int group = 0; placedAnimals < animals && group < 2 * animals && rooms.Count > 0; group++)
        {
            var room = group == 0 && first != null ? first : rnd.Pick(rooms);
            if (Pick([.. room.Cells.Where(c => open[c] >= 3 && Dist2(point[c], spawn) >= 180 * 180 && Clear(point[c], 200))], c => 1.0) is not { } anchor) continue;
            int size = Math.Min(animals - placedAnimals, rnd.Next(2, 4));
            for (int n = 0; n < size; n++)
            {
                var near = room.Cells.Where(c => open[c] >= 1 && Dist2(point[c], point[anchor]) <= 160 * 160 && Clear(point[c], 90)).ToList();
                if (Pick(near, c => 1.0) is not { } spot) break;
                items.Add(new Item(Kind.Animal, point[spot], rnd.Next(256)));
                taken.Add(point[spot]); placedAnimals++;
            }
        }
        rows -= placedAnimals;

        // Chests. How many of each goes by the size of the level; what each gives is settled with the treasure below.
        Vector3f Facing(LevelFill.FloorGrid.Place c) => roomOf.TryGetValue(c, out var inRoom) ? inRoom.EntryPoint : fringe.TryGetValue(c, out var beside) ? beside.MiddlePoint : spawn;
        // A tucked-away place: a nook's middle, the end of a walk, or failing those the floor by a far wall.
        LevelFill.FloorGrid.Place? Hidden(float clear)
        {
            var from = nooks.Select(r => r.Middle).Concat(ends).Where(c => Clear(point[c], clear)).ToList();
            if (from.Count == 0) from = [.. edges.Where(c => walk[c] >= far * 0.4 && Clear(point[c], clear))];
            return Pick(from, c => 1.0 + walk[c] / 32.0);
        }
        bool Has(Kind kind) => request.ChestKinds.Contains(kind);
        int size0 = Math.Max(1, rooms.Count);
        if (Has(Kind.LifeChest) && rows > 0 && Hidden(260) is { } life) { Put(Kind.LifeChest, point[life], Facing(life)); rows--; }
        if (Has(Kind.LockedChest) && rows > 4 && Hidden(260) is { } locked)
        {
            var chest = Put(Kind.LockedChest, point[locked], Facing(locked));
            // Its key: somewhere else altogether, as far off as the level allows.
            var away = places0.Where(h => Dist2(h, chest.Position) >= 900 * 900 && Clear(h, 160)).ToList();
            var spots = ends.Concat(edges).Where(c => Dist2(point[c], chest.Position) >= 900 * 900 && Clear(point[c], 200)).ToList();
            if (spots.Count == 0) spots = [.. order.Where(c => open[c] >= 1 && Dist2(point[c], chest.Position) >= 500 * 500 && Clear(point[c], 200))];
            if (away.Count > 0 && rnd.Chance(0.6)) { Put(Kind.Key, rnd.Pick(away), spawn); rows -= 3; }
            else if (Pick(spots, c => 1.0 + Math.Sqrt(Dist2(point[c], chest.Position)) / 400) is { } key) { Put(Kind.Key, point[key], spawn); rows -= 3; }
            else { items.Remove(chest); taken.Remove(chest.Position); notes.Add("No place for a locked chest's key, so no locked chest."); }
        }
        foreach (var (kind, count, clear) in new[]
        {
            (Kind.StrongChest, Math.Clamp(size0 / 2, 1, 3), 240f), (Kind.FlameChest, size0 >= 4 ? 1 : 0, 240f), (Kind.Firework, size0 >= 3 ? 1 : 0, 240f),
            (Kind.SpringChest, size0 >= 5 ? 1 : 0, 240f),
        })
            for (int n = 0; Has(kind) && n < count && rows > 0; n++)
                // By a wall or where a walk ends; closer to other things when the level is tight.
                for (int tries = 0; tries < 4; tries++)
                {
                    var from = tries % 2 == (rnd.Chance(0.4) ? 0 : 1) ? ends : edges;
                    float room = clear * (tries < 2 ? 1 : 0.6f);
                    if (Pick([.. from.Where(c => Clear(point[c], room))], c => 1.0 + walk[c] / 64.0) is not { } spot) continue;
                    Put(kind, point[spot], Facing(spot)); rows--;
                    break;
                }
        // Baskets and vases stand in twos and threes by the walls.
        if (Has(Kind.Basket))
        {
            int baskets = Math.Min(Math.Clamp(size0 + 2, 3, 9), rows / 3), placed = 0;
            for (int tries = 0; placed < baskets && tries < 6 * baskets; tries++)
            {
                if (Pick([.. edges.Where(c => Clear(point[c], 220))], c => 1.0) is not { } anchor) break;
                int group = Math.Min(baskets - placed, rnd.Next(1, 4));
                for (int n = 0; n < group; n++)
                {
                    var near = n == 0 ? [anchor] : edges.Concat(order.Where(c => open[c] is 2 or 3)).Where(c => Dist2(point[c], point[anchor]) <= 150 * 150 && Clear(point[c], 84)).ToList();
                    if (Pick(near, c => 1.0) is not { } spot) break;
                    Put(Kind.Basket, point[spot], Facing(spot)); placed++;
                }
            }
            rows -= placed;
        }

        // Dragonfly eggs: out of the way, one to a place, well apart.
        for (int n = 0; n < request.Eggs && rows > 0; n++)
        {
            var away = places0.Where(h => Clear(h, 260) && Dist2(h, spawn) >= 500 * 500).ToList();
            if (away.Count > 0 && rnd.Chance(0.5)) { Put(Kind.Egg, rnd.Pick(away), spawn); rows--; }
            else if (Hidden(300) is { } spot) { Put(Kind.Egg, point[spot], spawn); rows--; }
            else if (Pick([.. order.Where(c => open[c] >= 1 && Clear(point[c], 300))], c => 1.0 + walk[c] / 64.0) is { } any) { Put(Kind.Egg, point[any], spawn); rows--; }
            else { notes.Add($"Room for {n} of {request.Eggs} dragonfly eggs."); break; }
        }

        // The treasure. What chests and enemies give is at most a little over half of it; loose gems carry the rest.
        List<(Vector3f, int)> gems = [];
        List<double> hardness = [];
        int total = Math.Max(0, request.Treasure);
        var givers = items.Select((item, i) => (item, i)).Where(p => p.item.Kind is Kind.Enemy or Kind.Basket or Kind.StrongChest or Kind.FlameChest or Kind.Firework or Kind.SpringChest or Kind.LockedChest).ToList();
        var reward = givers.ToDictionary(p => p.i, p => p.item.Kind switch
        {
            Kind.Enemy => rnd.Chance(0.7) ? 1 : 2, Kind.Basket => rnd.Chance(0.6) ? 5 : rnd.Chance(0.5) ? 2 : 10, Kind.StrongChest => rnd.Chance(0.6) ? 10 : 25,
            Kind.FlameChest => 25, Kind.Firework => 10, Kind.SpringChest => 10, _ => 25,
        });
        int Given() => reward.Values.Sum();
        // Too much for the treasure asked for: the biggest come down a colour at a time (to nothing when it is very small).
        for (int guard = 0; guard < 400 && Given() > total * held; guard++)
        {
            int key = reward.OrderByDescending(p => p.Value).ThenBy(p => p.Key).Select(p => p.Key).DefaultIfEmpty(-1).First();
            if (key < 0) break;
            int k = Array.IndexOf(Colours, reward[key]);
            if (k <= 0) break;   // every giver is down to a red: they keep that
            reward[key] = Colours[k - 1];
        }
        foreach (var (i, value) in reward) items[i] = items[i] with { Reward = value };
        int loose = total - Given();
        if (loose > 0 && rows > 0)
        {
            // About one gem for every three and a half of the treasure, as the game's levels have, within the level's rows.
            int want = Math.Clamp((int)Math.Round(loose / 3.5), Math.Min(loose, 5), Math.Min(rows, 140));
            if (loose > 25 * want) notes.Add($"Only {want} gems fit the level's save rows: the treasure comes to {Given() + 25 * want}, not {total}.");
            List<(Vector3f P, double Hard)> places = [];
            bool Free(Vector3f p) => Clear(p, 96) && places.All(q => Dist2(q.P, p) >= 70 * 70);
            double Far(Vector3f p) => Math.Sqrt(Dist2(p, spawn));
            // The maker's places first (ledges, lookouts, stones, paths): the harder to reach, the better the gem.
            if (places0.Count > 0)
            {
                double keep = Range(rnd, 0.55, 0.85);
                foreach (var p in places0.OrderBy(g => g.X).ThenBy(g => g.Y).ThenBy(g => g.Z))
                {
                    if (!rnd.Chance(keep)) continue;
                    var under = grid.Nearest(p);
                    bool high = !open.TryGetValue(under, out int o) || o <= 3;
                    if (Free(p)) places.Add((p, high ? 3 : 1.5));
                }
            }
            // A trail: from a room's way in, back along the walk towards the start, a gem every three cells.
            foreach (var room in rooms.Where(r => r != first))
            {
                if (!rnd.Chance(places0.Count > 0 ? 0.5 : 0.9)) continue;
                var c = room.Entry;
                int length = rnd.Next(4, 8);
                for (int n = 0; n < length * 3; n++)
                {
                    var back = Around(c).Where(x => walk[x] < walk[c]).OrderBy(x => walk[x]).ThenBy(x => x.X).ThenBy(x => x.Y).Cast<LevelFill.FloorGrid.Place?>().FirstOrDefault();
                    if (back is not { } step) break;
                    c = step;
                    if (n % 3 == 2 && Free(point[c])) places.Add((point[c], 1));
                }
            }
            // A ring round the middle of a room or two.
            foreach (var room in rooms.Where(r => r.Cells.Count >= 200))
            {
                if (!rnd.Chance(0.4)) continue;
                int count = rnd.Next(5, 8);
                double turn = rnd.NextDouble() * 6.2832, radius = Math.Min(open[room.Middle], 5) * cell * 0.9;
                for (int n = 0; n < count; n++)
                {
                    double a = turn + 6.2832 * n / count;
                    float x = room.MiddlePoint.X + (float)(radius * SpyroPlanner.Cos(a)), y = room.MiddlePoint.Y + (float)(radius * SpyroPlanner.Sin(a));
                    if (grid.FloorNear(x, y, room.MiddlePoint.Z + 40) is { } z && Math.Abs(z - room.MiddlePoint.Z) <= 24 && Free(new(x, y, z))) places.Add((new(x, y, z), 1));
                }
            }
            // Still short: more along the walk, closer together each time round.
            for (float spacing = 360; places.Count < want && spacing >= 90; spacing *= 0.75f)
                foreach (var c in order.Where(c => open[c] >= 1).OrderBy(c => walk[c]).ThenBy(c => c.X).ThenBy(c => c.Y))
                {
                    if (places.Count >= want) break;
                    if (places.All(q => Dist2(q.P, point[c]) >= spacing * spacing) && Clear(point[c], 120)) places.Add((point[c], open[c] <= 2 ? 2 : 1));
                }
            if (places.Count > want)
            {
                rnd.Shuffle(places);
                places = [.. places.Take(want)];
            }
            // Farther from the start counts as harder too; a harder arrangement leans further towards the hard places.
            double reach = Math.Max(1, places.Count == 0 ? 1 : places.Max(q => Far(q.P)));
            double Hard((Vector3f P, double Hard) q) { double h = q.Hard * (1 + Far(q.P) / reach); return h * Math.Max(0.3, 1 + (bias - 1) * (h - 1)); }
            var values = Mix([.. places.Select(Hard)], loose, rnd);
            for (int i = 0; i < places.Count; i++) if (values[i] > 0) { gems.Add((places[i].P, values[i])); hardness.Add(Hard(places[i])); }
            if (places.Count > 0 && values.Sum() != loose && loose <= 25 * places.Count) notes.Add($"The treasure comes to {Given() + values.Sum()}, not {total}.");
        }
        else if (total > 0 && rows <= 0) notes.Add("The level has no save rows left for gems.");
        return new(items, gems, rooms.Count, notes) { Grid = grid, GemHardness = hardness };
    }

    /// <summary>Gem colours for places (red 1, green 2, blue 5, gold 10, pink 25) that come to <paramref name="total"/>
    /// exactly whenever that can be done, with every colour among them when the total allows: mostly reds and greens, the
    /// better colours at the harder places (<paramref name="hard"/>, one weight per place).</summary>
    public static int[] Mix(IReadOnlyList<double> hard, int total, LevelRandom rnd)
    {
        int n = hard.Count;
        var value = new int[n];
        if (n == 0 || total <= 0) return value;
        // Hardest first.
        var byHard = Enumerable.Range(0, n).OrderByDescending(i => hard[i]).ThenBy(i => i).ToArray();
        // Fewer gems than places: a red at the hardest ones.
        if (total < n) { for (int i = 0; i < total; i++) value[byHard[i]] = 1; return value; }
        Array.Fill(value, 1);
        int spare = total - n;
        // One of every colour while the total allows it, the best at the hardest places (the rest start as reds).
        int[] best = [25, 10, 5, 2];
        bool every = n >= Colours.Length && spare >= best.Sum(c => c - 1);
        if (every) for (int k = 0; k < best.Length; k++) { value[byHard[k]] = best[k]; spare -= best[k] - 1; }
        int Next(int v) => Colours[Array.IndexOf(Colours, v) + 1];
        // A gem may go up a colour when what is left pays for it and (keeping every colour) its own colour stays.
        bool May(int i, bool keep) => value[i] < 25 && Next(value[i]) - value[i] <= spare && (!keep || !every || value.Count(v => v == value[i]) > 1);
        // The rest of the total: a gem at a time goes up a colour, harder places more often; greens and blues are
        // common, golds fewer, pinks rare.
        for (int guard = 0; guard < 60 * n && spare > 0; guard++)
        {
            var can = Enumerable.Range(0, n).Where(i => May(i, true)).ToList();
            if (can.Count == 0) break;
            int pick = rnd.Pick<int>([.. can.Select(i => (i, Math.Max(0.01, hard[i]) * (value[i] switch { 1 => 6.0, 2 => 2.5, 5 => 1.0, _ => 0.35 })))]);
            int next = Next(value[pick]);
            spare -= next - value[pick]; value[pick] = next;
        }
        // A little left that no gem could take without losing a colour: exactness comes first.
        for (int guard = 0; guard < 8 * n && spare > 0; guard++)
        {
            int pick = byHard.Cast<int?>().FirstOrDefault(i => May(i!.Value, false)) ?? -1;
            if (pick < 0) break;
            int next = Next(value[pick]);
            spare -= next - value[pick]; value[pick] = next;
        }
        // Still a little left (every step up costs more than it): one gem goes up and one or two others come down a
        // colour to pay the difference.
        int Down(int v) => Colours[Array.IndexOf(Colours, v) - 1];
        for (int guard = 0; guard < 6 && spare > 0; guard++)
        {
            bool done = false;
            for (int i = 0; i < n && !done; i++)
            {
                if (value[i] >= 25) continue;
                int owed = Next(value[i]) - value[i] - spare;
                if (owed <= 0) continue;
                for (int j = 0; j < n && !done; j++)
                {
                    if (j == i || value[j] <= 1) continue;
                    int first = value[j] - Down(value[j]);
                    if (first == owed) { value[j] = Down(value[j]); value[i] = Next(value[i]); spare = 0; done = true; break; }
                    for (int k = j + 1; k < n && !done; k++)
                    {
                        if (k == i || value[k] <= 1 || first + value[k] - Down(value[k]) != owed) continue;
                        value[j] = Down(value[j]); value[k] = Down(value[k]); value[i] = Next(value[i]); spare = 0; done = true;
                    }
                }
            }
            if (!done) break;
        }
        return value;
    }
}
