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
        /// <summary>The group each gem lies in (one number per gem of <see cref="Gems"/>): a group's gems are one
        /// colour where the total allows (<see cref="Values"/>).</summary>
        public IReadOnlyList<int> GemGroups { get; init; } = [];
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

        /// <summary>Whether a wall rises within <paramref name="cells"/> grid cells of <paramref name="p"/> (the game's
        /// gems and chests mostly lie within a hundred units of one).</summary>
        public bool Within(Vector3f p, int cells)
        {
            int x = (int)MathF.Floor(p.X / LevelFill.Cell), y = (int)MathF.Floor(p.Y / LevelFill.Cell);
            for (int dx = -cells; dx <= cells; dx++)
                for (int dy = -cells; dy <= cells; dy++)
                    if (dx * dx + dy * dy <= cells * cells + cells && _spans.TryGetValue((x + dx, y + dy), out var list) && list.Any(e => e.Hi > p.Z + 30 && e.Lo < p.Z + 90)) return true;
            return false;
        }

        /// <summary>Free of walls: not in one, not against one. <paramref name="small"/>: a gem, which may lie right at
        /// a wall's foot (only where it lies itself must be clear).</summary>
        public bool Open(Vector3f p, bool small = false) => !(small ? At(p) : Near(p)) && !Inside(p);

        /// <summary>A wall where <paramref name="p"/> is or within about a gem's width of it.</summary>
        private bool At(Vector3f p)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    float reach = dx != 0 && dy != 0 ? 17 : 24;
                    if (_spans.TryGetValue(((int)MathF.Floor((p.X + dx * reach) / LevelFill.Cell), (int)MathF.Floor((p.Y + dy * reach) / LevelFill.Cell)), out var list) &&
                        list.Any(e => e.Hi > p.Z + 14 && e.Lo < p.Z + 100)) return true;
                }
            return false;
        }
    }

    private static float Dist2(Vector3f a, Vector3f b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z) / 4;
    private static double Range(LevelRandom rnd, double a, double b) => a + (b - a) * rnd.NextDouble();
    public static readonly int[] Colours = [1, 2, 5, 10, 25];

    /// <summary>The game's own figures, measured over its thirty levels on foot (study/objects.py): of a level's
    /// treasure 31% lies loose, 46% is in chests and 23% comes from enemies; for every hundred of treasure a level has
    /// about ten chests (with the extra lives and the locked chest), seven enemies and sixteen loose gems worth a little over two apiece (62% red, 24% green, 10%
    /// blue, 3% gold, 1% pink).</summary>
    public const double LooseShare = 0.31, ChestsPerHundred = 9.2, EnemiesPerHundred = 6.7, LooseGemValue = 2.13;

    /// <summary>The enemies a level of this treasure has in the game, more or fewer by the difficulty.</summary>
    public static int EnemiesFor(int treasure, LevelDifficulty difficulty) => Math.Clamp((int)Math.Round(Math.Max(0, treasure) / 100.0 * difficulty switch
    {
        LevelDifficulty.Easy => 4.5, LevelDifficulty.Hard => 9.5, LevelDifficulty.Kaizo => 13, _ => EnemiesPerHundred,
    }), 3, 40);

    /// <summary>The animals for Sparx (a level of the game has about five): plenty when easy, none in Kaizo.</summary>
    public static int AnimalsFor(LevelDifficulty difficulty) => difficulty switch { LevelDifficulty.Easy => 8, LevelDifficulty.Hard => 3, LevelDifficulty.Kaizo => 0, _ => 5 };

    /// <summary>What a kind of chest, or an enemy, gives in the game: gems and how often (of a hundred).</summary>
    private static IReadOnlyList<(int Gems, double Weight)> Gives(Kind kind) => kind switch
    {
        Kind.Basket => [(1, 4), (2, 45), (5, 38), (10, 11), (25, 2)],
        Kind.StrongChest => [(1, 3), (2, 44), (5, 42), (10, 10), (25, 1)],
        Kind.SpringChest => [(1, 3), (2, 26), (5, 42), (10, 24), (25, 5)],
        Kind.FlameChest => [(1, 8), (2, 23), (5, 35), (10, 32), (25, 2)],
        Kind.Firework => [(1, 6), (2, 3), (5, 44), (10, 25), (25, 22)],
        Kind.LockedChest => [(2, 40), (5, 30), (10, 10), (25, 20)],
        _ => [(1, 20), (2, 35), (5, 35), (10, 10)],
    };

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
        // The floor by the walls: within about a hundred units of a wall that rises beside it (where the level has few
        // such walls, as islands do, the floor near its edges as well).
        var byWall = order.Where(c => open[c] >= 1 && walk[c] > 10 && upright.Within(point[c], 3)).ToList();
        if (byWall.Count < 60) byWall = [.. byWall.Concat(edges).Distinct()];
        var nearWall = byWall.ToHashSet();
        // Right by them: about two steps from the wall, where most of the game's chests stand.
        var hardBy = byWall.Where(c => upright.Within(point[c], 2)).ToList();
        if (hardBy.Count < 40) hardBy = byWall;

        // A maker's place, on the plain floor under it (none there: not used).
        Vector3f? OnFloor(Vector3f g)
        {
            var floors = grid.Floors(g.X, g.Y).Where(f => f.Plain && Math.Abs(f.Z - g.Z) <= 120).ToList();
            return floors.Count == 0 ? null : g with { Z = floors.MinBy(f => Math.Abs(f.Z - g.Z)).Z };
        }
        var places0 = hints?.Select(OnFloor).Where(p => p != null).Select(p => p!.Value).ToList() ?? [];
        List<Vector3f> taken = [.. occupied ?? [], spawn];
        // A homeworld's doorways: nothing stands in one, nor on the floor in front where Spyro walks in and comes back
        // out; enemies keep well away from them altogether.
        List<Vector3f> doorways = [];
        foreach (var door in scene.Portals ?? [])
        {
            var (fx, fy) = DoorwayShapes.Facing(door.Heading);
            for (float t = 0; t <= 720; t += 120) doorways.Add(new(door.Position.X - (float)fx * t, door.Position.Y - (float)fy * t, door.Position.Z));
        }
        taken.AddRange(doorways);
        bool OffDoorways(Vector3f p) => doorways.All(q => Dist2(q, p) >= 560 * 560);
        bool Clear(Vector3f p, float d, bool small = false) => taken.All(t => Dist2(t, p) >= d * d) && upright.Open(p, small);
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
        int total = Math.Max(0, request.Treasure);
        // How far enemies keep from a room's way in and from the start, how far apart guards stand on the paths, how many
        // wait together in a room, how much of the treasure enemies and chests hold, and how much better the gems at
        // hard places are.
        var (enemyGap, startGap, guardGap, together, looseShare, bias) = request.Difficulty switch
        {
            LevelDifficulty.Easy => (260f, 560f, 480f, 3, 0.4, 0.8),
            LevelDifficulty.Hard => (190f, 480f, 340f, 5, 0.26, 1.3),
            LevelDifficulty.Kaizo => (150f, 440f, 280f, 6, 0.2, 1.6),
            _ => (220f, 520f, 420f, 4, LooseShare, 1.0),
        };
        // How much of everything: what was asked for while the level's save rows hold it, else everything in proportion
        // (fewer objects then carry the same treasure). A dragon takes three rows, a locked chest and its key four.
        int wantChests = (int)Math.Round(total / 100.0 * ChestsPerHundred);
        int wantGems = (int)Math.Round(total * looseShare / LooseGemValue);
        int fixedRows = (request.Exit ? 1 : 0) + request.ThiefRoutes.Count + 3 * request.Dragons + request.Eggs + (request.ChestKinds.Contains(Kind.LifeChest) ? 1 : 0) + (request.ChestKinds.Contains(Kind.LockedChest) ? 4 : 0);
        int wantAll = request.Enemies + request.Animals + wantChests + wantGems;
        double fit = wantAll <= 0 ? 1 : Math.Clamp((rows - fixedRows) / (double)wantAll, 0, 1);
        if (fit < 1)
        {
            wantChests = (int)(wantChests * fit);
            if (fit < 0.75) notes.Add($"The level has save rows for about {(int)Math.Round(fit * 100)}% of what a level of the game with this treasure holds, so there is less of everything and each thing gives more.");
        }

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

        // Enemies: in the rooms past the first, in ones, twos and threes (now and then a pack) a few steps apart, facing
        // the way in; the deeper rooms first, big rooms more than one group; then the odd one standing guard on a path.
        var beyond = rooms.Where(r => r != first).ToList();
        if (beyond.Count == 0 && first != null) beyond = [first];
        // A room's floor out to its walls (enemies stand nearer the walls than the middle).
        var fringeOf = fringe.GroupBy(f => f.Value).ToDictionary(g => g.Key, g => g.Select(f => f.Key).OrderBy(c => c.X).ThenBy(c => c.Y).ThenBy(c => c.Layer).ToList());
        int enemies = Math.Min((int)Math.Round(request.Enemies * fit), Math.Max(0, rows / 3));
        if (enemies > 0 && beyond.Count > 0)
        {
            int left = enemies, forPaths = (int)Math.Round(enemies * 0.2);
            for (int round = 0; left > forPaths && round < 4; round++)
                for (int k = beyond.Count - 1; k >= 0 && left > forPaths; k--)
                {
                    var room = beyond[k];
                    // A second group only in a big room.
                    if (round > 0 && room.Cells.Count < 500 * round) continue;
                    int want = Math.Min(left - forPaths, Math.Min(together + 1, rnd.Pick<int>([(1, 34.0), (2, 30.0), (3, 18.0), (4, 9.0), (5, 5.0), (6, 4.0)])));
                    // Not in the doorway: Spyro gets to step in first.
                    var cells = room.Cells.Concat(fringeOf.GetValueOrDefault(room, [])).Where(c => open[c] >= 2 && Dist2(point[c], room.EntryPoint) >= enemyGap * enemyGap && Dist2(point[c], spawn) >= startGap * startGap && OffDoorways(point[c])).ToList();
                    LevelFill.FloorGrid.Place? anchor = null;
                    for (int n = 0; n < want; n++)
                    {
                        // The first of a group well clear of other groups; the rest a few steps from it.
                        var near = anchor is { } a ? cells.Where(c => Dist2(point[c], point[a]) <= 250 * 250 && Clear(point[c], 118)).ToList() : [.. cells.Where(c => Clear(point[c], 330))];
                        if (Pick(near, c => nearWall.Contains(c) ? 6.0 : 1.0) is not { } spot) break;
                        anchor ??= spot;
                        Put(Kind.Enemy, point[spot], room.EntryPoint); left--;
                    }
                }
            // On the paths: narrow floor between rooms, well apart.
            var paths = order.Where(c => !roomOf.ContainsKey(c) && open[c] is >= 1 and <= 3 && walk[c] > 20 && Dist2(point[c], spawn) >= 600 * 600 && OffDoorways(point[c])).ToList();
            for (int tries = 0; left > 0 && tries < 4 * enemies; tries++)
            {
                if (Pick([.. paths.Where(c => Clear(point[c], guardGap))], c => 1.0) is not { } spot) break;
                Put(Kind.Enemy, point[spot], spawn); left--;
            }
            // Any still to place: wherever a room has space.
            for (int tries = 0; left > 0 && tries < 3 * enemies; tries++)
            {
                var room = rnd.Pick(beyond);
                if (Pick([.. room.Cells.Where(c => open[c] >= 2 && Dist2(point[c], spawn) >= startGap * startGap && OffDoorways(point[c]) && Clear(point[c], 150))], c => 1.0) is not { } spot) continue;
                Put(Kind.Enemy, point[spot], room.EntryPoint); left--;
            }
            if (left > 0) notes.Add($"Room for {enemies - left} of {enemies} enemies.");
            rows -= enemies - left;
        }

        // Animals: little groups in the open, the first where Spyro starts.
        int animals = Math.Min((int)Math.Round(request.Animals * fit), Math.Max(0, rows / 4)), placedAnimals = 0;
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

        // Chests. As many as the game's levels have for their treasure (about ten for every hundred gems), mostly
        // baskets and strong chests, in ones, twos and threes a chest's width apart, most of them by the walls.
        Vector3f Facing(LevelFill.FloorGrid.Place c) => roomOf.TryGetValue(c, out var inRoom) ? inRoom.EntryPoint : fringe.TryGetValue(c, out var beside) ? beside.MiddlePoint : spawn;
        // A tucked-away place: a nook's middle, the end of a walk, or failing those the floor by a far wall.
        LevelFill.FloorGrid.Place? Hidden(float clear)
        {
            var from = nooks.Select(r => r.Middle).Concat(ends).Where(c => Clear(point[c], clear)).ToList();
            if (from.Count == 0) from = [.. edges.Where(c => walk[c] >= far * 0.4 && Clear(point[c], clear))];
            return Pick(from, c => 1.0 + walk[c] / 32.0);
        }
        bool Has(Kind kind) => request.ChestKinds.Contains(kind);
        for (int n = 0; Has(Kind.LifeChest) && n < (total >= 400 ? 2 : 1) && rows > 0; n++)
            if (Hidden(260) is { } life) { Put(Kind.LifeChest, point[life], Facing(life)); rows--; }
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
        // The kinds in the game's own proportions (of every 100 chests: 47 baskets, 27 strong, 16 spring, 7 three-flame,
        // 4 firework), one of each kind first.
        var common = new[] { (Kind.Basket, 47.0), (Kind.StrongChest, 27.0), (Kind.SpringChest, 16.0), (Kind.FlameChest, 7.0), (Kind.Firework, 4.0) }.Where(k => Has(k.Item1)).ToList();
        int chests = common.Count == 0 ? 0 : Math.Min(wantChests, Math.Max(0, rows - 1));
        List<Kind> kindsToPlace = [];
        if (chests > 0)
        {
            // Each kind its share of the chests (one at least while there are chests for it), the rest by chance.
            double whole = common.Sum(k => k.Item2);
            foreach (var (kind, weight) in common)
                for (int n = 0; n < Math.Max(1, (int)(chests * weight / whole)) && kindsToPlace.Count < chests; n++) kindsToPlace.Add(kind);
            while (kindsToPlace.Count < chests) kindsToPlace.Add(rnd.Pick<Kind>(common));
        }
        // Like with like: the game's groups are mostly of one kind.
        kindsToPlace = [.. kindsToPlace.OrderBy(k => k)];
        int placedChests = 0;
        var inTheOpen = order.Where(c => open[c] >= 3 && walk[c] > 12).ToList();
        for (int tries = 0; placedChests < kindsToPlace.Count && tries < 8 * kindsToPlace.Count + 20; tries++)
        {
            // Three groups in four stand by a wall or where a walk ends; the rest out in the open.
            var from = rnd.Chance(0.8) ? (rnd.Chance(0.15) && ends.Count > 0 ? ends : hardBy) : inTheOpen;
            float apart = tries < 4 * kindsToPlace.Count ? 190 : 130;
            if (Pick([.. from.Where(c => Clear(point[c], apart))], c => 1.0 + walk[c] / 96.0) is not { } anchor) continue;
            int group = Math.Min(kindsToPlace.Count - placedChests, rnd.Pick<int>([(1, 50.0), (2, 30.0), (3, 14.0), (4, 6.0)]));
            var last = anchor;
            for (int n = 0; n < group; n++)
            {
                // The next one a chest's width along from the last (the game's chests stand about 120 apart).
                var near = n == 0 ? [anchor] : order.Where(c => open[c] >= 1 && Math.Abs(point[c].Z - point[anchor].Z) <= 12 && Dist2(point[c], point[last]) is >= 92 * 92 and <= 136 * 136 && Clear(point[c], 90)).ToList();
                if (Pick(near, c => 1.0) is not { } spot) break;
                Put(kindsToPlace[placedChests++], point[spot], Facing(spot)); last = spot;
            }
        }
        rows -= placedChests;
        if (placedChests < chests) notes.Add($"Room for {placedChests} of {chests} chests.");

        // Dragonfly eggs: out of the way, one to a place, well apart.
        for (int n = 0; n < request.Eggs && rows > 0; n++)
        {
            var away = places0.Where(h => Clear(h, 260) && Dist2(h, spawn) >= 500 * 500).ToList();
            if (away.Count > 0 && rnd.Chance(0.5)) { Put(Kind.Egg, rnd.Pick(away), spawn); rows--; }
            else if (Hidden(300) is { } spot) { Put(Kind.Egg, point[spot], spawn); rows--; }
            else if (Pick([.. order.Where(c => open[c] >= 1 && Clear(point[c], 300))], c => 1.0 + walk[c] / 64.0) is { } any) { Put(Kind.Egg, point[any], spawn); rows--; }
            else { notes.Add($"Room for {n} of {request.Eggs} dragonfly eggs."); break; }
        }

        // What chests and enemies give: drawn as the game's own give (measured over its levels: a basket a green or a
        // blue, a strong chest the same, spring and three-flame chests blue or gold, a firework blue to pink; an
        // enemy a green or a blue, now and then a red or a gold).
        List<(Vector3f, int)> gems = [];
        List<double> hardness = [];
        List<int> groupOf = [];
        var givers = items.Select((item, i) => (item, i)).Where(p => p.item.Kind is Kind.Enemy or Kind.Basket or Kind.StrongChest or Kind.FlameChest or Kind.Firework or Kind.SpringChest or Kind.LockedChest).ToList();
        var reward = givers.ToDictionary(p => p.i, p => rnd.Pick<int>(Gives(p.item.Kind)));
        int Given() => reward.Values.Sum();
        // The loose gems' share of the treasure: about a third in the game, less when the level is hard; never more than
        // the gems the level has rows for can carry at about a blue apiece.
        int gemRows = Math.Max(0, Math.Min(rows, 140));
        double low = looseShare * 0.7 * total, most = Math.Min(looseShare * 1.35 * total, Math.Max(4.0 * gemRows, low));
        // Too much given for the treasure asked for: the biggest come down a colour at a time (never below a red).
        for (int guard = 0; guard < 2000 && total - Given() < low; guard++)
        {
            int key = reward.Where(p => p.Value > 1).OrderByDescending(p => p.Value).ThenBy(p => p.Key).Select(p => p.Key).DefaultIfEmpty(-1).First();
            if (key < 0) break;
            reward[key] = Colours[Array.IndexOf(Colours, reward[key]) - 1];
        }
        // Too little: the smallest go up a colour while that still leaves the loose gems their share.
        for (int guard = 0; guard < 2000 && total - Given() > most; guard++)
        {
            int key = reward.Where(p => p.Value < 25).OrderBy(p => p.Value).ThenBy(p => p.Key).Select(p => p.Key).DefaultIfEmpty(-1).First();
            if (key < 0) break;
            int next = Colours[Array.IndexOf(Colours, reward[key]) + 1];
            if (total - Given() - (next - reward[key]) < low) break;
            reward[key] = next;
        }
        foreach (var (i, value) in reward) items[i] = items[i] with { Reward = value };
        int loose = total - Given();
        if (loose > 0 && rows > 0)
        {
            // As many loose gems as the game has for that much: mostly reds and greens, a little over two apiece.
            int want = Math.Clamp((int)Math.Round(loose / LooseGemValue), Math.Min(loose, 5), gemRows);
            // Few enough that one of every colour fits the total (a pink, a gold, a blue and a green cost 38 more than reds).
            if (loose >= 48) want = Math.Min(want, Math.Max(5, loose - 38));
            if (loose > 25 * want) notes.Add($"Only {want} gems fit the level's save rows: the treasure comes to {Given() + 25 * want}, not {total}.");
            List<(Vector3f P, double Hard, int Group)> places = [];
            int groups = 0;
            bool Free(Vector3f p) => Clear(p, 84, small: true) && places.All(q => Dist2(q.P, p) >= 72 * 72);
            // Groups keep apart, so each reads as its own.
            bool Apart(Vector3f p, int own, float d) => places.All(q => q.Group == own || Dist2(q.P, p) >= d * d);
            double Far(Vector3f p) => Math.Sqrt(Dist2(p, spawn));
            // A group of gems from a place: the game's gems lie in ones (two groups in five), twos and threes, now and
            // then four or five, about a hundred apart, in a line along the way or across it.
            int Group(Vector3f at, double hard, int most = 5, (float X, float Y)? along = null)
            {
                if (!Free(at) || !Apart(at, -1, 215)) return 0;
                int size = Math.Min(Math.Min(most, want - places.Count), rnd.Pick<int>([(1, 36.0), (2, 26.0), (3, 20.0), (4, 10.0), (5, 8.0)]));
                if (size <= 0) return 0;
                int id = groups++;
                places.Add((at, hard, id));
                if (size == 1) return 1;
                double turn = rnd.NextDouble() * 6.2832;
                var (dx, dy) = along ?? ((float)SpyroPlanner.Cos(turn), (float)SpyroPlanner.Sin(turn));
                if (along != null && rnd.Chance(0.3)) (dx, dy) = (-dy, dx);
                float step = (float)Range(rnd, 76, 132);
                // Out one way from the first gem, then the other, while the floor stays level and free: along the way
                // or across it, whichever holds more of the group (by a wall that is along the wall).
                List<Vector3f> Line(float ux, float uy)
                {
                    List<Vector3f> line = [];
                    foreach (int sign in new[] { 1, -1 })
                        for (int k = 1; line.Count < size - 1; k++)
                        {
                            float x = at.X + sign * k * step * ux, y = at.Y + sign * k * step * uy;
                            if (grid.FloorNear(x, y, at.Z + 30) is not { } z || Math.Abs(z - at.Z) > 20 || !grid.Floors(x, y).Any(f => f.Plain && Math.Abs(f.Z - z) < 4)) break;
                            var p = new Vector3f(x, y, z);
                            if (!Free(p) || !Apart(p, id, 190) || line.Any(q => Dist2(q, p) < 72 * 72)) break;
                            line.Add(p);
                        }
                    return line;
                }
                var first1 = Line(dx, dy);
                var best = first1.Count >= size - 1 ? first1 : Line(-dy, dx) is { } other && other.Count > first1.Count ? other : first1;
                foreach (var p in best) places.Add((p, hard, id));
                return 1 + best.Count;
            }
            // The way the walk runs at a place (towards the start), for trails.
            (float X, float Y)? Way(LevelFill.FloorGrid.Place c)
            {
                var back = Around(c).Where(x => walk[x] < walk[c]).OrderBy(x => walk[x]).ThenBy(x => x.X).ThenBy(x => x.Y).Cast<LevelFill.FloorGrid.Place?>().FirstOrDefault();
                if (back is not { } b) return null;
                float dx = point[b].X - point[c].X, dy = point[b].Y - point[c].Y, l = MathF.Sqrt(dx * dx + dy * dy);
                return l <= 0 ? null : (dx / l, dy / l);
            }
            // The maker's places first (ledges, lookouts, stones, paths): the harder to reach, the better the gems.
            if (places0.Count > 0)
            {
                // Most of the high places (they are what a jump or a glide is for), a few of the plain ones.
                double keep = Range(rnd, 0.6, 0.85);
                foreach (var h in places0.OrderBy(g => g.X).ThenBy(g => g.Y).ThenBy(g => g.Z))
                {
                    if (places.Count >= want) break;
                    var under = grid.Nearest(h);
                    bool high = !open.TryGetValue(under, out int o) || o <= 3;
                    if (!rnd.Chance(high ? keep : 0.18)) continue;
                    Group(h, high ? 3 : 1.5, high ? 3 : 5, walk.ContainsKey(under) ? Way(under) : null);
                }
            }
            // Then about the level as the game's lie: half of them within a hundred units of a wall, the rest along
            // the paths and out in the rooms.
            // Right at the walls' feet, where a good half of the game's gems lie.
            var gemsByWall = order.Where(c => walk[c] > 10 && upright.Within(point[c], 2)).ToList();
            if (gemsByWall.Count < 40) gemsByWall = hardBy;
            var onPaths = order.Where(c => !roomOf.ContainsKey(c) && open[c] >= 2 && walk[c] > 10).ToList();
            var inRooms = order.Where(c => roomOf.ContainsKey(c) && open[c] >= 3 && walk[c] > 6).ToList();
            for (int tries = 0; places.Count < want && tries < 12 * want + 40; tries++)
            {
                double roll = rnd.NextDouble();
                var from = roll < 0.5 && gemsByWall.Count > 0 ? gemsByWall : roll < 0.7 && byWall.Count > 0 ? byWall : roll < 0.86 && onPaths.Count > 0 ? onPaths : inRooms.Count > 0 ? inRooms : byWall;
                if (from.Count == 0) break;
                var c = rnd.Pick(from);
                if (tries >= 8 * want && places.Any(q => Dist2(q.P, point[c]) < 160 * 160)) continue;
                Group(point[c], from == gemsByWall || from == byWall ? 1.6 : from == onPaths ? 1.2 : 1, 5, Way(c));
            }
            // Still short: more along the walk, closer together each time round.
            for (float spacing = 215; places.Count < want && spacing >= 84; spacing *= 0.8f)
                foreach (var c in order.Where(c => open[c] >= 1).OrderBy(c => walk[c]).ThenBy(c => c.X).ThenBy(c => c.Y))
                {
                    if (places.Count >= want) break;
                    if (places.All(q => Dist2(q.P, point[c]) >= spacing * spacing) && Clear(point[c], 100)) places.Add((point[c], open[c] <= 2 ? 2 : 1, groups++));
                }
            // Farther from the start counts as harder too; a harder arrangement leans further towards the hard places.
            double reach = Math.Max(1, places.Count == 0 ? 1 : places.Max(q => Far(q.P)));
            double Hard((Vector3f P, double Hard, int Group) q) { double h = q.Hard * (1 + Far(q.P) / reach); return h * Math.Max(0.3, 1 + (bias - 1) * (h - 1)); }
            var weights = places.Select(Hard).ToList();
            var groupIds = places.Select(q => q.Group).ToList();
            var values = Values(weights, groupIds, loose, rnd);
            for (int i = 0; i < places.Count; i++) if (values[i] > 0) { gems.Add((places[i].P, values[i])); hardness.Add(weights[i]); groupOf.Add(groupIds[i]); }
            if (places.Count > 0 && values.Sum() != loose && loose <= 25 * places.Count) notes.Add($"The treasure comes to {Given() + values.Sum()}, not {total}.");
        }
        else if (total > 0 && rows <= 0) notes.Add("The level has no save rows left for gems.");
        return new(items, gems, rooms.Count, notes) { Grid = grid, GemHardness = hardness, GemGroups = groupOf };
    }

    /// <summary>Gem colours for places in groups that come to <paramref name="total"/> exactly (<see cref="Mix"/>), laid
    /// out as the game lays them: a group's gems are one colour wherever the colours divide that way, and the better
    /// colours go to the harder groups, the best of all to a gem that lies alone at the hardest place.</summary>
    /// <param name="group">The group of each place (any numbers; one per place of <paramref name="hard"/>).</param>
    public static int[] Values(IReadOnlyList<double> hard, IReadOnlyList<int> group, int total, LevelRandom rnd)
    {
        int n = hard.Count;
        var mixed = Mix(hard, total, rnd);
        if (group.Count != n || n == 0) return mixed;
        // The colours Mix chose, best first, dealt to the groups hardest first: a colour goes to a group it can fill
        // (or finish) before it is split over one it cannot.
        var groups = Enumerable.Range(0, n).Where(i => mixed[i] > 0 || total >= n).GroupBy(i => group[i])
            .Select(g => (Places: g.OrderByDescending(i => hard[i]).ThenBy(i => i).ToList(), Hard: g.Max(i => hard[i]), Id: g.Key))
            .OrderByDescending(g => g.Hard).ThenBy(g => g.Id).ToList();
        var free = groups.Select(g => g.Places.Count).ToList();
        var value = new int[n];
        foreach (int colour in Colours.Reverse())
        {
            int count = mixed.Count(v => v == colour);
            while (count > 0)
            {
                int at = Enumerable.Range(0, groups.Count).Where(k => free[k] > 0 && free[k] <= count).Cast<int?>().FirstOrDefault()
                    ?? Enumerable.Range(0, groups.Count).Where(k => free[k] > 0).Cast<int?>().FirstOrDefault() ?? -1;
                if (at < 0) break;
                int take = Math.Min(free[at], count);
                for (int k = 0; k < take; k++) value[groups[at].Places[groups[at].Places.Count - free[at] + k]] = colour;
                free[at] -= take; count -= take;
            }
        }
        return value.Sum() == mixed.Sum() ? value : mixed;
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
