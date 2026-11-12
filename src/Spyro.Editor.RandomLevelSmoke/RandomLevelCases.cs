using System.Diagnostics;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Generation;
using Spyro.Editor.Core.Levels;

/// <summary>Random Level generator checks without the disc: every style over several seeds and sizes is generated and
/// compiled as Create BIN compiles it; reachability, the game's limits, determinism and variety are checked.</summary>
internal static class RandomLevelCases
{
    private static string OtherProcessFingerprint(long seed, LevelStyle style, LevelSize size, string repo)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!) { RedirectStandardOutput = true, UseShellExecute = false };
        string dll = typeof(RandomLevelCases).Assembly.Location;
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!) == "dotnet") info.ArgumentList.Add(dll);
        foreach (string a in new[] { "--fingerprint", seed.ToString(), style.ToString(), size.ToString(), repo }) info.ArgumentList.Add(a);
        using var process = Process.Start(info)!;
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output.Split('\n').FirstOrDefault(l => l.StartsWith("FINGERPRINT ", StringComparison.Ordinal))?[12..].Trim() ?? "none";
    }

    /// <summary>How far (on the map) <paramref name="p"/> is from the nearest upright face, measured to the part of
    /// it that stands between Spyro's knees and his head there (a face that only passes under the floor or overhead
    /// does not count).</summary>
    public static float WallDistance(SceneDocument scene, Spyro.Editor.Core.Primitives.Vector3f p)
    {
        float best = float.MaxValue, lo = p.Z + 20, hi = p.Z + 90;
        var part = new List<Spyro.Editor.Core.Primitives.Vector3f>(8); var next = new List<Spyro.Editor.Core.Primitives.Vector3f>(8);
        foreach (var s in scene.Surfaces)
        {
            var v = s.Vertices;
            if (v.Max(q => q.Z) <= lo || v.Min(q => q.Z) >= hi) continue;
            float nx = 0, ny = 0, nz = 0;
            for (int k = 0; k < v.Length; k++)
            {
                var a = v[k]; var b = v[(k + 1) % v.Length];
                nx += (a.Y - b.Y) * (a.Z + b.Z); ny += (a.Z - b.Z) * (a.X + b.X); nz += (a.X - b.X) * (a.Y + b.Y);
            }
            float len = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
            if (len <= 0 || Math.Abs(nz) / len >= 0.6f) continue;
            // The face cut down to the band of height that matters.
            part.Clear(); part.AddRange(v);
            foreach (var (level, above) in new[] { (lo, true), (hi, false) })
            {
                next.Clear();
                for (int k = 0; k < part.Count; k++)
                {
                    var a = part[k]; var b = part[(k + 1) % part.Count];
                    bool ina = above ? a.Z >= level : a.Z <= level, inb = above ? b.Z >= level : b.Z <= level;
                    if (ina) next.Add(a);
                    if (ina != inb) { float t = (level - a.Z) / (b.Z - a.Z); next.Add(new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, level)); }
                }
                (part, next) = (next, part);
            }
            for (int k = 0; k < part.Count; k++)
            {
                var a = part[k]; var b = part[(k + 1) % part.Count];
                float dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
                float t = l2 <= 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1);
                float ex = a.X + t * dx - p.X, ey = a.Y + t * dy - p.Y;
                best = Math.Min(best, MathF.Sqrt(ex * ex + ey * ey));
            }
        }
        return best;
    }

    /// <summary>The wall distance as study/objects.py measures it in the game's own levels (to any upright face whose
    /// height span reaches between Spyro's knees and his head, wherever along the face that is), so that
    /// <c>--scatter-facts</c> sets like beside like.</summary>
    public static float WallDistanceAsStudied(SceneDocument scene, Spyro.Editor.Core.Primitives.Vector3f p)
    {
        float best = float.MaxValue;
        foreach (var s in scene.Surfaces)
        {
            var v = s.Vertices;
            if (v.Max(q => q.Z) <= p.Z + 20 || v.Min(q => q.Z) >= p.Z + 90) continue;
            float nx = 0, ny = 0, nz = 0;
            for (int k = 0; k < v.Length; k++)
            {
                var a = v[k]; var b = v[(k + 1) % v.Length];
                nx += (a.Y - b.Y) * (a.Z + b.Z); ny += (a.Z - b.Z) * (a.X + b.X); nz += (a.X - b.X) * (a.Y + b.Y);
            }
            float len = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
            if (len <= 0 || Math.Abs(nz) / len >= 0.6f) continue;
            for (int k = 0; k < v.Length; k++)
            {
                var a = v[k]; var b = v[(k + 1) % v.Length];
                float dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
                float t = l2 <= 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1);
                float ex = a.X + t * dx - p.X, ey = a.Y + t * dy - p.Y;
                best = Math.Min(best, MathF.Sqrt(ex * ex + ey * ey));
            }
        }
        return best;
    }

    /// <summary>RANDOM_LOOK_CACHE: a project's editor-cache folder, so the looks' own towers, trees and rocks stand in
    /// the levels (without it they get built towers only, and the checks need no disc).</summary>
    public static Func<string, LevelPropSource?>? LookSource() => Environment.GetEnvironmentVariable("RANDOM_LOOK_CACHE") is { Length: > 0 } cache
        ? key => File.Exists(Path.Combine(cache, key + "-runtime-scene-editor-overlay.json"))
            ? LevelPropSource.FromGeometry(key, Spyro.Editor.Core.Scene.GeometryOverlayLoader.LoadFirstCandidate(Path.Combine(cache, key + "-runtime-scene-editor-overlay.json")))
            : null
        : null;

    /// <summary>Variety: the seed decides how a level is laid out, how many rooms it has, how big they are and how high
    /// the level climbs. Two dozen seeds of every kind (not compiled; the checks above compile theirs) must differ in
    /// all of these, every floor of every one must be reachable, every layout must turn up, and every layout can be
    /// asked for by name. Homeworlds vary the same way and still have a place for each of their five doorways.</summary>
    public static void Variety(LevelCatalog catalog, Func<string, LevelPropSource?>? source, Action<bool, string> check, int seeds = 24, bool print = true)
    {
        static T At<T>(List<T> sorted, double q) => sorted[Math.Min(sorted.Count - 1, (int)(q * sorted.Count))];
        var seen = new Dictionary<string, int>();
        List<RandomLevelResult> Make(Func<int, RandomLevelOptions> options, int count)
        {
            var made = new RandomLevelResult?[count];
            // The generator's working state is per thread: levels are made side by side.
            Parallel.For(0, count, k =>
            {
                try { made[k] = RandomLevelGenerator.Generate(options(k), catalog, compile: false, source: source); }
                catch (Exception e) when (e is InvalidDataException or InvalidOperationException or ArgumentException) { }
            });
            return [.. made.OfType<RandomLevelResult>()];
        }
        string Line(string what, List<RandomLevelResult> made)
        {
            var rooms = made.Select(r => r.Report.Areas).Order().ToList(); var heights = made.Select(r => r.Report.HeightSpan).Order().ToList(); var faces = made.Select(r => r.Report.Faces).Order().ToList();
            return $"variety {what,-18} rooms {rooms[0]}-{At(rooms, .5)}-{rooms[^1]}, height {heights[0]}-{At(heights, .5)}-{heights[^1]}, faces {faces[0]}-{At(faces, .5)}-{faces[^1]}; " +
                string.Join(", ", made.GroupBy(r => r.Report.Layout).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key} {g.Count()}"));
        }
        foreach (var style in Enum.GetValues<LevelStyle>())
        {
            var made = Make(k => new RandomLevelOptions { Seed = 7000 + (int)style * 100 + k, Style = style }, seeds);
            check(made.Count == seeds, $"variety {style}: {seeds - made.Count} of {seeds} seeds made no level");
            if (made.Count == 0) continue;
            if (print) Console.WriteLine(Line(style.ToString(), made));
            var layouts = made.GroupBy(r => r.Report.Layout).ToDictionary(g => g.Key, g => g.Count());
            foreach (var (layout, n) in layouts) seen[layout] = seen.GetValueOrDefault(layout) + n;
            var rooms = made.Select(r => r.Report.Areas).Order().ToList(); var heights = made.Select(r => r.Report.HeightSpan).Order().ToList(); var faces = made.Select(r => r.Report.Faces).Order().ToList();
            bool low = style is LevelStyle.Islands;
            check(layouts.Count >= 4 && layouts.Values.Max() <= seeds / 2, $"variety {style}: layouts {string.Join(", ", layouts.Select(l => $"{l.Key} {l.Value}"))}");
            check(rooms[^1] - rooms[0] >= 5 && rooms[^1] >= 1.6 * rooms[0], $"variety {style}: rooms only from {rooms[0]} to {rooms[^1]}");
            check(heights[^1] >= 2.5 * Math.Max(1, heights[0]) && At(heights, .5) >= (low ? 150 : 300) && heights[^1] >= (low ? 300 : 600), $"variety {style}: heights {heights[0]}, {At(heights, .5)}, {heights[^1]}");
            check(faces[^1] >= 1.6 * faces[0], $"variety {style}: faces only from {faces[0]} to {faces[^1]}");
            var unreachable = made.Where(r => r.Report.RequiredFloors == 0 || r.Report.Unreached + r.Report.NoWayBack > 0.03 * r.Report.RequiredFloors).Select(r => r.Report.Seed).ToList();
            check(unreachable.Count == 0, $"variety {style}: floors out of reach in seeds {string.Join(", ", unreachable)}");
            check(made.Select(r => r.Scene.Fingerprint()).Distinct().Count() == made.Count, $"variety {style}: two seeds made the same level");
            check(made.All(r => r.Report.Faces <= RandomLevelGenerator.FaceTarget(LevelSize.Medium) + SpyroStructuresFaces), $"variety {style}: a level of {faces[^1]} faces");
        }
        check(SpyroPlannerLayouts.All(l => seen.GetValueOrDefault(l) >= 5), $"variety: layouts over all kinds {string.Join(", ", seen.OrderBy(l => l.Key, StringComparer.Ordinal).Select(l => $"{l.Key} {l.Value}"))}");
        // Every layout by name, in every size.
        foreach (string layout in SpyroPlannerLayouts)
        {
            var made = Make(k => new RandomLevelOptions { Seed = 7900 + k, Style = k % 2 == 0 ? LevelStyle.Valley : LevelStyle.Castle, Size = (LevelSize)(k % 3), Layout = layout }, 6);
            check(made.Count == 6 && made.Count(r => r.Report.Layout == layout) >= 5 && made.All(r => r.Report.Unreached + r.Report.NoWayBack <= 0.03 * r.Report.RequiredFloors),
                $"variety: asked for {layout}, got {string.Join(", ", made.Select(r => r.Report.Layout))}");
        }
        // Sizes: a large level has more floor than a small one of the same seed, as a rule.
        var small = Make(k => new RandomLevelOptions { Seed = 7950 + k, Style = LevelStyle.Valley, Size = LevelSize.Small }, 12);
        var large = Make(k => new RandomLevelOptions { Seed = 7950 + k, Style = LevelStyle.Valley, Size = LevelSize.Large }, 12);
        check(small.Count == 12 && large.Count == 12 && large.Average(r => r.Report.Faces) >= 1.35 * small.Average(r => r.Report.Faces),
            $"variety: large levels average {(large.Count > 0 ? large.Average(r => r.Report.Faces) : 0):0} faces, small ones {(small.Count > 0 ? small.Average(r => r.Report.Faces) : 0):0}");
        // Homeworlds.
        foreach (var style in new[] { LevelStyle.Valley, LevelStyle.Castle, LevelStyle.Islands, LevelStyle.Canyon, LevelStyle.Caverns, LevelStyle.SkyIslands })
        {
            var homes = Make(k => new RandomLevelOptions { Seed = 7600 + (int)style * 100 + k, Style = style, Doorways = 5 }, 12);
            if (print && homes.Count > 0) Console.WriteLine(Line("homeworld " + style, homes));
            var kinds = homes.Select(r => r.Report.Layout).Distinct().ToList();
            check(homes.Count == 12 && kinds.Count >= (style == LevelStyle.Caverns ? 2 : 3), $"variety {style} homeworlds: layouts {string.Join(", ", kinds)}");
            check(homes.All(r => r.Doorways.Count == 5), $"variety {style} homeworlds: doorway places {string.Join(", ", homes.Select(r => r.Doorways.Count))}");
            check(homes.All(r => r.Report.Unreached + r.Report.NoWayBack <= 0.03 * r.Report.RequiredFloors), $"variety {style} homeworlds: floors out of reach");
            // Every doorway place of every one of them keeps the doorway rules (three checks a doorway, as one).
            foreach (var home in homes)
            {
                List<string> wrong = [];
                DoorwayFloors(home, $"{style} {home.Report.Seed}", (ok, problem) => { if (!ok) wrong.Add(problem); });
                check(wrong.Count == 0, string.Join("; ", wrong));
            }
        }
    }

    /// <summary>Every doorway place of a homeworld has level floor both ways while its room lasts (Spyro comes out in
    /// front of a doorway, or with some homeworlds' doorways just behind it), floor of some kind on to 720 in front (a
    /// path's slope), and no wall near the way in and out.</summary>
    public static void DoorwayFloors(RandomLevelResult home, string what, Action<bool, string> check)
    {
        var floors = new LevelFill.FloorGrid(home.Scene);
        foreach (var door in home.Doorways)
        {
            var (fx, fy) = DoorwayShapes.Facing(door.Heading);
            var walk = Enumerable.Range(-(int)(Math.Min(door.ClearBehind, 420) / 60), (int)(door.ClearFront / 60) + (int)(Math.Min(door.ClearBehind, 420) / 60) + 1)
                .Select(n => new Spyro.Editor.Core.Primitives.Vector3f(door.Position.X - (float)fx * n * 60, door.Position.Y - (float)fy * n * 60, door.Position.Z)).ToList();
            check(door.ClearFront >= 480 && door.ClearBehind >= 200 && floors.Holds(walk, 30), $"{what} homeworld doorway at {door.Position}: no level floor in front of it ({door.ClearFront:0}) or behind it ({door.ClearBehind:0})");
            var beyond = Enumerable.Range(0, 13).Select(n => new Spyro.Editor.Core.Primitives.Vector3f(door.Position.X - (float)fx * n * 60, door.Position.Y - (float)fy * n * 60, door.Position.Z)).ToList();
            check(floors.Holds(beyond, 200), $"{what} homeworld doorway at {door.Position}: no floor 720 in front of it");
            float wall = walk.Min(q => WallDistance(home.Scene, q));
            var worst = walk.MinBy(q => WallDistance(home.Scene, q));
            check(wall >= 50, $"{what} homeworld doorway at {door.Position} (heading {door.Heading}, clear {door.ClearFront:0} in front, {door.ClearBehind:0} behind{(door.Plaza ? ", in the plaza" : "")}): a wall {wall:0} units from its way in at {worst}");
        }
    }

    /// <summary>Random objects as the card asks for them at a treasure and difficulty: every kind of chest, the way
    /// out, three dragons and a thief.</summary>
    public static LevelScatter.Plan Scatter(SceneDocument scene, long seed, int treasure, LevelDifficulty difficulty, bool highGems = false) =>
        LevelScatter.Make(scene, new LevelScatter.Request(seed, LevelScatter.EnemiesFor(treasure, difficulty), LevelScatter.AnimalsFor(difficulty), treasure, 236)
        {
            Exit = true, Dragons = 3, ThiefRoutes = [6], ChestKinds = LevelScatter.Chests.ToHashSet(), Difficulty = difficulty, TealGems = highGems, RainbowGems = highGems,
            Scenery = new Dictionary<LevelScatter.Kind, int> { [LevelScatter.Kind.Tree] = 8, [LevelScatter.Kind.Light] = 8, [LevelScatter.Kind.WallLight] = 5, [LevelScatter.Kind.Plant] = 10 },
        }, scene.GemSpots);

    /// <summary>The kind names study/objects2.py groups objects by.</summary>
    public static string StudyKind(LevelScatter.Kind kind) => kind switch
    {
        LevelScatter.Kind.Enemy => "enemy", LevelScatter.Kind.Animal => "fodder", LevelScatter.Kind.Exit => "exit", LevelScatter.Kind.Dragon => "dragon", LevelScatter.Kind.Thief => "thief",
        LevelScatter.Kind.Key => "key", LevelScatter.Kind.LifeChest => "chest:life", LevelScatter.Kind.LockedChest => "chest:locked", LevelScatter.Kind.Egg => "egg",
        LevelScatter.Kind.Tree => "prop:tree", LevelScatter.Kind.Light or LevelScatter.Kind.WallLight => "prop:light", LevelScatter.Kind.Plant => "prop:plant", _ => "chest:any",
    };

    private static readonly string[] SpyroPlannerLayouts = ["journey", "loop", "hub", "arena", "climb", "gorge", "web", "field", "plateaus", "terraces", "courts"];
    /// <summary>The faces a level's towers and trees may add to its land.</summary>
    private const int SpyroStructuresFaces = 900;

    public static async Task<int> Run(string repo, string? maps, int seedsPerStyle, string? only = null)
    {
        var catalog = LevelCatalog.Load(repo);
        var source = LookSource();
        int checks = 0, failures = 0;
        void Check(bool ok, string what) { checks++; if (!ok) { failures++; Console.WriteLine("FAIL " + what); } }
        var fingerprints = new HashSet<string>();
        double highAway = 0, looseAway = 0; int highCount = 0, looseCount = 0;
        var styles = Enum.GetValues<LevelStyle>();
        var sizes = Enum.GetValues<LevelSize>();
        var themesSeen = new HashSet<string>();
        if (maps != null) Directory.CreateDirectory(maps);
        if (only == null)
        {
            // Every doorway shape at every size and turn: the destination's sky, which the game draws in the opening,
            // fits its frame: the frame's front begins at the opening's edge all the way round (but the floor), and
            // covers next to none of the opening (a keystone and the footings lap a few units over it).
            static bool Inside(List<(double U, double V)> polygon, double u, double v)
            {
                bool inside = false;
                for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
                    if (polygon[i].V > v != polygon[j].V > v && u < polygon[i].U + (polygon[j].U - polygon[i].U) * (v - polygon[i].V) / (polygon[j].V - polygon[i].V)) inside = !inside;
                return inside;
            }
            foreach (var shape in DoorwayShapes.All)
                foreach (double scale in new[] { 0.75, 0.9, 1.0, 1.15, 1.3, 1.5 })
                    foreach (int heading in new[] { 0, 37, 64, 150, 233 })
                    {
                        var at = new Spyro.Editor.Core.Primitives.Vector3f(4000, 4000, 500);
                        var (fx, fy) = DoorwayShapes.Facing(heading);
                        var opening = DoorwayShapes.Opening(shape, scale).ToList();
                        double depth = shape.FrameDepth * scale;
                        var fronts = DoorwayShapes.Frame(shape, scale, at, heading).Where(f => f.Part == DoorwayFramePart.Face &&
                            f.Corners.All(c => (c.X - at.X) * fx + (c.Y - at.Y) * fy < -depth + 1.5))
                            .Select(f => f.Corners.Select(c => (U: -(c.X - at.X) * fy + (c.Y - at.Y) * fx, V: (double)(c.Z - at.Z))).ToList()).ToList();
                        int samples = 0, covered = 0;
                        double width = opening.Max(q => q.U), height = opening.Max(q => q.V);
                        for (double u = -width; u <= width; u += 4)
                            for (double v = 2; v <= height; v += 4)
                            {
                                if (!Inside(opening, u, v)) continue;
                                // Three units in from the opening's edge: the frame's corners are whole units, so its
                                // inner edge may lie a unit to either side of the opening's.
                                bool edge = false;
                                for (int e = 0; e + 1 < opening.Count && !edge; e++)
                                {
                                    var a = opening[e]; var b = opening[e + 1];
                                    double du = b.U - a.U, dv = b.V - a.V, l = Math.Sqrt(du * du + dv * dv);
                                    edge = ((u - a.U) * -dv + (v - a.V) * du) / l < 3;
                                }
                                if (edge) continue;
                                samples++;
                                if (fronts.Any(f => Inside(f, u, v))) covered++;
                            }
                        int open = 0;
                        for (int e = 0; e + 1 < opening.Count; e++)
                        {
                            var a = opening[e]; var b = opening[e + 1];
                            double du = b.U - a.U, dv = b.V - a.V, l = Math.Sqrt(du * du + dv * dv), mu = (a.U + b.U) / 2 + dv / l * 4, mv = (a.V + b.V) / 2 - du / l * 4;
                            if (mv > 3 && !fronts.Any(f => Inside(f, mu, mv))) open++;
                        }
                        Check(DoorwayShapes.Check(opening) == null && samples > 200 && covered <= 0.02 * samples && open == 0,
                            $"doorway {shape.Id} at {scale} heading {heading}: the frame covers {covered} of {samples} of the opening and leaves {open} of its edges open");
                    }
        }
        foreach (var style in styles.Where(x => only == null || x.ToString() == only))
            for (int k = 0; k < seedsPerStyle; k++)
            {
                long seed = 1000 + (int)style * 100 + k;
                var options = new RandomLevelOptions { Seed = seed, Style = style, Size = sizes[k % sizes.Length],
                    ChargePaths = Environment.GetEnvironmentVariable("RANDOM_CHARGE") == "1" ? true : null };
                var watch = Stopwatch.StartNew();
                RandomLevelResult r;
                try { r = RandomLevelGenerator.Generate(options, catalog, source: source); }
                catch (Exception e) when (e is InvalidDataException or InvalidOperationException or ArgumentException)
                { Check(false, $"{style} seed {seed}: {e.Message}"); continue; }
                long ms = watch.ElapsedMilliseconds;
                var rep = r.Report;
                themesSeen.Add(rep.Theme);
                Console.WriteLine($"{style,-10} seed {seed} {options.Size,-6} {rep.Theme,-9} {rep.Faces,5} faces {rep.Materials,2} tex areas {rep.Areas,2} " +
                    $"req {rep.RequiredFloors,6} unreached {rep.Unreached,4} noback {rep.NoWayBack,4} traps {rep.Traps,4} glideOnly {rep.GlideOnlyAreas} tries {rep.Attempts} {ms} ms | {rep.Features}");
                Console.WriteLine("    " + string.Join(", ", rep.Counts.Select(c => $"{c.Key} {c.Value}")) + (rep.DrawPlan != null ? " | " + rep.DrawPlan : ""));
                foreach (var note in rep.Notes) Console.WriteLine("    note: " + note);
                try { r.Scene.Validate(); Check(true, ""); } catch (InvalidDataException e) { Check(false, $"{style} {seed} validate: {e.Message}"); }
                Check(rep.Faces <= SceneDocument.MaxSurfaces, $"{style} {seed} faces {rep.Faces}");
                Check(rep.Materials <= 127, $"{style} {seed} materials {rep.Materials}");
                double bad = rep.RequiredFloors == 0 ? 1 : (rep.Unreached + rep.NoWayBack) / (double)rep.RequiredFloors;
                Check(rep.RequiredFloors > 0 && bad <= 0.03, $"{style} {seed} reachability: {rep.Unreached} unreached, {rep.NoWayBack} without a way back of {rep.RequiredFloors}");
                float lowest = r.Scene.Surfaces.Min(s => s.Vertices.Min(v => v.Z));
                Check(lowest >= 64 + 32, $"{style} {seed} lowest face {lowest}");
                Check(NewWorldDocument.IsValidName(r.Scene.Name), $"{style} {seed} name '{r.Scene.Name}'");
                try { NativeAuthoredSpawn.RequirePortalArrivalSupported(r.Scene); Check(true, ""); } catch (InvalidDataException e) { Check(false, $"{style} {seed} spawn: {e.Message}"); }
                Check(fingerprints.Add(r.Scene.Fingerprint()), $"{style} {seed} same level as another seed");
                if (k == 0)
                {
                    // The same level again in this process, and in a process of its own (cells kept in hash sets
                    // enumerate in a different order in every process; the level must not depend on it).
                    var again = RandomLevelGenerator.Generate(options, catalog, source: source);
                    string other = OtherProcessFingerprint(seed, style, options.Size, repo);
                    Check(again.Scene.Fingerprint() == r.Scene.Fingerprint() && other == r.Scene.Fingerprint(), $"{style} {seed} not the same level every time ({other})");
                }
                if (k == 0)
                {
                    // Fill In What the Level Needs works on it: dragons, a way home and gems find floor to stand on.
                    var fill = LevelFill.Make(r.Scene, 3, [4], true, 300, r.Scene.GemSpots);
                    Check(fill.Dragons.Count == 3 && fill.Exit != null, $"{style} {seed} Fill In placed {fill.Dragons.Count} dragons, exit {fill.Exit != null}: {string.Join(" ", fill.Notes)}");
                    Check(fill.Gems.Count >= 10 && fill.ReachableCells > 200, $"{style} {seed} Fill In placed {fill.Gems.Count} gems over {fill.ReachableCells} cells");
                    // Random objects: the way out, dragons, a thief, enemies, animals, every kind of chest, a key,
                    // dragonfly eggs and gems find places; the treasure is exactly what was asked for, in every
                    // colour; a seed gives the same arrangement again and another seed another.
                    var want = new LevelScatter.Request(seed, 10, 5, 300, 200)
                    { Dragons = 3, ThiefRoutes = [6], Exit = true, ChestKinds = LevelScatter.Chests.ToHashSet(), Eggs = 2 };
                    var scatter = LevelScatter.Make(r.Scene, want, r.Scene.GemSpots);
                    int Of(LevelScatter.Kind kind) => scatter.Of(kind).Count();
                    Check(Of(LevelScatter.Kind.Exit) == 1 && Of(LevelScatter.Kind.Dragon) == 3 && Of(LevelScatter.Kind.Thief) == 1 && Of(LevelScatter.Kind.Enemy) >= 5 &&
                        Of(LevelScatter.Kind.Animal) >= 2 && Of(LevelScatter.Kind.Basket) >= 2 && Of(LevelScatter.Kind.StrongChest) >= 1 && Of(LevelScatter.Kind.LifeChest) == 1 &&
                        Of(LevelScatter.Kind.LockedChest) == Of(LevelScatter.Kind.Key) && Of(LevelScatter.Kind.Egg) == 2 && scatter.Gems.Count >= 12,
                        $"{style} {seed} random objects: {string.Join(", ", scatter.Items.GroupBy(i => i.Kind).Select(g => g.Count() + " " + g.Key))}, {scatter.Gems.Count} gems in {scatter.Rooms} rooms: {string.Join(" ", scatter.Notes)}");
                    Check(scatter.Treasure == 300, $"{style} {seed} random objects' treasure is {scatter.Treasure}, not 300: {string.Join(" ", scatter.Notes)}");
                    Check(LevelScatter.Colours.All(c => scatter.Gems.Any(g => g.Value == c)), $"{style} {seed} gem colours: {string.Join(",", scatter.Gems.Select(g => g.Value).Distinct().Order())}");
                    string Shape(LevelScatter.Plan plan) => string.Join(";", plan.Items.Select(x => x.Kind + "@" + x.Position + "=" + x.Reward).Concat(plan.Gems.Select(g => g.Position + "=" + g.Value)));
                    Check(Shape(LevelScatter.Make(r.Scene, want, r.Scene.GemSpots)) == Shape(scatter), $"{style} {seed} random objects differ for the same seed");
                    Check(Shape(LevelScatter.Make(r.Scene, want with { Seed = seed + 1 }, r.Scene.GemSpots)) != Shape(scatter), $"{style} {seed} random objects the same for another seed");
                    var all = scatter.Items.Select(x => x.Position).Concat(scatter.Gems.Select(g => g.Position)).ToList();
                    Check(scatter.Grid != null && scatter.Grid.Holds(all, 200), $"{style} {seed} random objects off the floor");
                    // Nothing stands in a wall, a tower or a tree: every upright face keeps its distance.
                    float tight = all.Count == 0 ? 0 : all.Min(q => WallDistance(r.Scene, q));
                    Check(tight >= 20, $"{style} {seed} a random object stands {tight:0} units from a wall");
                    Check(scatter.Of(LevelScatter.Kind.Enemy).All(e => MathF.Sqrt((e.Position.X - r.Scene.Spawn.X) * (e.Position.X - r.Scene.Spawn.X) + (e.Position.Y - r.Scene.Spawn.Y) * (e.Position.Y - r.Scene.Spawn.Y)) >= 400),
                        $"{style} {seed} an enemy stands at the start");
                    // Small and odd totals come out exact too.
                    foreach (int treasure in new[] { 57, 123, 500 })
                    {
                        var other = LevelScatter.Make(r.Scene, want with { Treasure = treasure, Seed = seed + treasure }, r.Scene.GemSpots);
                        Check(other.Treasure == treasure, $"{style} {seed} treasure {other.Treasure}, not {treasure}: {string.Join(" ", other.Notes)}");
                    }
                    // Scenery: trees, torches, wall lamps and flowers find places on the floor (wall lamps on a wall
                    // over it), none where Spyro arrives or on top of anything else, and the treasure is as exact with
                    // them as without.
                    {
                        var withScenery = want with { Scenery = new Dictionary<LevelScatter.Kind, int> { [LevelScatter.Kind.Tree] = 6, [LevelScatter.Kind.Light] = 6, [LevelScatter.Kind.WallLight] = 4, [LevelScatter.Kind.Plant] = 8 } };
                        var dressed = LevelScatter.Make(r.Scene, withScenery, r.Scene.GemSpots);
                        var scenery = dressed.Items.Where(i => i.Kind is LevelScatter.Kind.Tree or LevelScatter.Kind.Light or LevelScatter.Kind.WallLight or LevelScatter.Kind.Plant).ToList();
                        var others = dressed.Items.Except(scenery).Select(i => i.Position).Concat(dressed.Gems.Select(g => g.Position)).ToList();
                        // Apart on the floor; things a storey apart in height are apart too.
                        float Flat(Spyro.Editor.Core.Primitives.Vector3f a, Spyro.Editor.Core.Primitives.Vector3f b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z) / 4);
                        Check(scenery.Count >= 10 && scenery.Count(i => i.Kind == LevelScatter.Kind.Plant) >= 3 && scenery.Count(i => i.Kind == LevelScatter.Kind.Tree) >= 1 &&
                            scenery.Count(i => i.Kind == LevelScatter.Kind.Tree) <= 6 && scenery.Count(i => i.Kind == LevelScatter.Kind.Plant) <= 8,
                            $"{style} {seed} scenery: {string.Join(", ", scenery.GroupBy(i => i.Kind).Select(g => g.Count() + " " + g.Key))}");
                        Check(dressed.Treasure == 300 && dressed.Grid!.Holds(scenery.Select(i => i.Position), 200), $"{style} {seed} scenery: treasure {dressed.Treasure}, or a piece of it off the floor");
                        Check(scenery.All(i => Flat(i.Position, r.Scene.Spawn) >= 250) && scenery.Where(i => i.Kind != LevelScatter.Kind.WallLight).All(i => others.All(o => Flat(o, i.Position) >= 60)),
                            $"{style} {seed} scenery stands where Spyro arrives or on another object: " + string.Join("; ", scenery.Where(i => Flat(i.Position, r.Scene.Spawn) < 250 || i.Kind != LevelScatter.Kind.WallLight && others.Any(o => Flat(o, i.Position) < 60))
                                .Select(i => $"{i.Kind} at {i.Position.X:0},{i.Position.Y:0},{i.Position.Z:0}, {Flat(i.Position, r.Scene.Spawn):0} from the start, {others.Min(o => Flat(o, i.Position)):0} from {dressed.Items.Except(scenery).OrderBy(o => Flat(o.Position, i.Position)).First().Kind}")));
                        // What stands on the floor keeps off the walls like everything else; a wall lamp is at a wall.
                        var standing = scenery.Where(i => i.Kind != LevelScatter.Kind.WallLight).ToList();
                        float close = standing.Count == 0 ? 99 : standing.Min(i => WallDistance(r.Scene, i.Position));
                        var lamps = scenery.Where(i => i.Kind == LevelScatter.Kind.WallLight).ToList();
                        float lampWall = lamps.Count == 0 ? 0 : lamps.Max(i => WallDistance(r.Scene, i.Position with { Z = i.Position.Z - 90 }));
                        Check(close >= 20 && lampWall <= 48, $"{style} {seed} scenery: a tree or a torch {close:0} from a wall, a wall lamp {lampWall:0} from its wall");
                        Check(Shape(LevelScatter.Make(r.Scene, withScenery, r.Scene.GemSpots)) == Shape(dressed), $"{style} {seed} scenery differs for the same seed");
                    }
                    // Teal (50) and rainbow (100) gems: off unless asked for; asked for, a rare few take their part of
                    // the treasure, each alone at a hard place far from the start, and the total is still exact.
                    Check(scatter.HighGems.Count == 0, $"{style} {seed} teal or rainbow gems that were not asked for");
                    foreach (var (treasure, teal, rainbow) in new[] { (300, true, true), (500, true, true), (1000, true, true), (300, true, false), (300, false, true), (120, true, true) })
                    {
                        var rich = LevelScatter.Make(r.Scene, want with { Treasure = treasure, TealGems = teal, RainbowGems = rainbow, Seed = seed + 7 * treasure }, r.Scene.GemSpots);
                        var (teals, rainbows) = LevelScatter.HighGemsFor(treasure, teal, rainbow);
                        float Flat(Spyro.Editor.Core.Primitives.Vector3f a, Spyro.Editor.Core.Primitives.Vector3f b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
                        Check(rich.Treasure == treasure && rich.HighGems.Count(g => g.Value == 50) == teals && rich.HighGems.Count(g => g.Value == 100) == rainbows && rich.HighGems.All(g => g.Value is 50 or 100),
                            $"{style} {seed} treasure {treasure} with teal {teal} rainbow {rainbow}: {rich.Treasure}, gems {string.Join(",", rich.HighGems.Select(g => g.Value))} (wanted {teals} teal, {rainbows} rainbow): {string.Join(" ", rich.Notes)}");
                        Check(rich.HighGems.All(g => Flat(g.Position, r.Scene.Spawn) >= 480 && rich.Gems.All(o => Flat(o.Position, g.Position) >= 150) && rich.Items.All(o => Flat(o.Position, g.Position) >= 120)) &&
                            rich.Grid!.Holds(rich.HighGems.Select(g => g.Position), 40) && rich.HighGems.All(g => WallDistance(r.Scene, g.Position) >= 20),
                            $"{style} {seed} treasure {treasure}: a teal or rainbow gem is not alone on open floor away from the start");
                        if (treasure == 1000)
                        {
                            // The hardest places: further from the start than the level's loose gems are as a rule.
                            double Away(Spyro.Editor.Core.Primitives.Vector3f p) => Flat(p, r.Scene.Spawn);
                            highAway += rich.HighGems.Sum(g => Away(g.Position)); highCount += rich.HighGems.Count;
                            looseAway += rich.Gems.Sum(g => Away(g.Position)); looseCount += rich.Gems.Count;
                        }
                    }
                    // An easy and a Kaizo arrangement: the treasure is still exact, and no enemy stands at the start.
                    foreach (var hard in new[] { LevelDifficulty.Easy, LevelDifficulty.Kaizo })
                    {
                        var other = LevelScatter.Make(r.Scene, want with { Difficulty = hard, Enemies = hard == LevelDifficulty.Kaizo ? 24 : 6, Animals = hard == LevelDifficulty.Kaizo ? 0 : 8 }, r.Scene.GemSpots);
                        int foes = other.Of(LevelScatter.Kind.Enemy).Count();
                        Check(other.Treasure == 300 && foes >= (hard == LevelDifficulty.Kaizo ? 8 : 3) && (hard != LevelDifficulty.Kaizo || !other.Of(LevelScatter.Kind.Animal).Any()) &&
                            other.Of(LevelScatter.Kind.Enemy).All(e => MathF.Sqrt((e.Position.X - r.Scene.Spawn.X) * (e.Position.X - r.Scene.Spawn.X) + (e.Position.Y - r.Scene.Spawn.Y) * (e.Position.Y - r.Scene.Spawn.Y)) >= 400),
                            $"{style} {seed} {hard} objects: treasure {other.Treasure}, {foes} enemies: {string.Join(" ", other.Notes)}");
                    }
                    // The joins the in-game route checks play are in the report.
                    Check(rep.Links.Count >= rep.Areas - 1, $"{style} {seed} {rep.Links.Count} links for {rep.Areas} areas");
                }
                if (k == 0)
                {
                    // A homeworld of this seed: a place for each of five doorways, on the floor, with open level floor in
                    // front for Spyro to come back out onto; every floor still reachable; with the doorways built (a new
                    // world's: four levels and the way back) it is a level the game can hold; random objects keep out of
                    // the doorways' way.
                    try
                    {
                        var home = RandomLevelGenerator.Generate(options with { Doorways = 5 }, catalog, source: source);
                        var h = home.Report;
                        Console.WriteLine($"    homeworld {h.Faces,5} faces areas {h.Areas,2} doorways {home.Doorways.Count} req {h.RequiredFloors,5} unreached {h.Unreached} noback {h.NoWayBack} | {string.Join(", ", h.Counts.Select(c => $"{c.Key} {c.Value}"))}");
                        Check(home.Doorways.Count == 5, $"{style} {seed} homeworld has {home.Doorways.Count} doorway places: {string.Join(" ", h.Notes)}");
                        Check(h.RequiredFloors > 0 && (h.Unreached + h.NoWayBack) <= 0.03 * h.RequiredFloors, $"{style} {seed} homeworld: {h.Unreached} unreached, {h.NoWayBack} without a way back of {h.RequiredFloors}");
                        DoorwayFloors(home, $"{style} {seed}", Check);
                        var destinations = new[] { 71, 72, 73, 74 }.Select(id => new DoorwayDestination(id, "LEVEL " + (id - 70), false, null))
                            .Append(new DoorwayDestination(10, "Artisans", false, SceneDoorways.WayBackNote)).ToArray();
                        var (built, added, problems) = SceneDoorways.AddAll(home.Scene with { WorldLevelId = NewWorldDocument.Homeworld }, home.Doorways, destinations,
                            LevelThemes.DoorwayShape(h.Theme), home.DoorwayMaterial!);
                        Check(added.Count == home.Doorways.Count && built.Portals?.Count == added.Count, $"{style} {seed} homeworld built {added.Count} doorways: {string.Join(" ", problems)}");
                        Check(built.Portals!.All(d => d.FrameFaces is { Count: > 20 }), $"{style} {seed} a homeworld doorway has no frame");
                        // Each doorway in a look that suits the level: one of the look's shapes, a size of its own, framed in
                        // the level's own stone; and it has room where it stands (no wall within its frame's reach).
                        var suited = LevelThemes.DoorwayShapes(h.Theme).Select(x => x.Shape.Id).ToHashSet();
                        Check(home.Doorways.All(d => d.Shape != null && suited.Contains(d.Shape) && d.Scale is >= 0.9f and <= 1.3f && d.Face != null && home.Materials.Contains(d.Face) && d.FaceTint != null),
                            $"{style} {seed} homeworld doorway looks: {string.Join(", ", home.Doorways.Select(d => $"{d.Shape} x{d.Scale}"))}");
                        Check(built.Portals!.Zip(home.Doorways.OrderBy(d => d.Position.X)).Count() == home.Doorways.Count && built.Portals!.All(d => home.Doorways.Any(x => x.Position == d.Position && x.Shape == d.Shape)),
                            $"{style} {seed} a homeworld doorway was not built in its own look");
                        float reach = float.MaxValue;
                        foreach (var d in built.Portals!)
                        {
                            var shape = DoorwayShapes.Find(d.Shape)!;
                            var (fx, fy) = DoorwayShapes.Facing(d.Heading);
                            float half = (float)((shape.Width / 2 + shape.FrameWidth + 16) * d.SizeScale);
                            foreach (float side in new[] { -half, 0, half })
                                reach = Math.Min(reach, WallDistance(home.Scene, new(d.Position.X - (float)fy * side, d.Position.Y + (float)fx * side, d.Position.Z)));
                        }
                        Check(reach >= 24, $"{style} {seed} a homeworld doorway's frame stands {reach:0} units from a wall");
                        Console.WriteLine($"    doorways: {string.Join(", ", built.Portals!.Select(d => $"{d.DestinationLevelId} {d.Shape} x{d.SizeScale:0.00}"))}");
                        try { NativeAuthoredMesh.Compile(built, [.. built.Surfaces.Select(f => f.Material).OfType<AuthoredMaterial>().Distinct()]); Check(true, ""); }
                        catch (InvalidDataException e) { Check(false, $"{style} {seed} homeworld with its doorways: {e.Message}"); }
                        var things = LevelScatter.Make(built, new LevelScatter.Request(seed, 6, 5, 200, 150) { ChestKinds = LevelScatter.Chests.ToHashSet(), Exit = true }, built.GemSpots);
                        var stand = things.Items.Select(x => x.Position).Concat(things.Gems.Select(g => g.Position)).ToList();
                        float nearest = float.MaxValue;
                        foreach (var door in built.Portals!)
                        {
                            var (fx, fy) = DoorwayShapes.Facing(door.Heading);
                            foreach (var q in stand)
                                for (int n = -4; n <= 11; n++)
                                    nearest = Math.Min(nearest, MathF.Sqrt(MathF.Pow(q.X - (door.Position.X - (float)fx * n * 60), 2) + MathF.Pow(q.Y - (door.Position.Y - (float)fy * n * 60), 2)));
                        }
                        Check(stand.Count >= 20 && nearest >= 60, $"{style} {seed} homeworld: {stand.Count} random objects, the nearest {nearest:0} units from a doorway's way in");
                    }
                    catch (Exception e) when (e is InvalidDataException or InvalidOperationException or ArgumentException) { Check(false, $"{style} {seed} homeworld: {e.Message}"); }
                }
                if (k == 1)
                {
                    // Every difficulty of this seed is a level the game can hold whose every floor is reachable, and
                    // each is another level.
                    foreach (var difficulty in new[] { LevelDifficulty.Easy, LevelDifficulty.Hard, LevelDifficulty.Kaizo })
                    {
                        try
                        {
                            var other = RandomLevelGenerator.Generate(options with { Difficulty = difficulty }, catalog);
                            var o = other.Report;
                            Console.WriteLine($"    {difficulty,-6} {o.Faces,5} faces areas {o.Areas,2} req {o.RequiredFloors,5} unreached {o.Unreached} noback {o.NoWayBack} | {string.Join(", ", o.Counts.Select(c => $"{c.Key} {c.Value}"))}");
                            Check(o.RequiredFloors > 0 && (o.Unreached + o.NoWayBack) <= 0.03 * o.RequiredFloors, $"{style} {seed} {difficulty}: {o.Unreached} unreached, {o.NoWayBack} without a way back of {o.RequiredFloors}");
                            Check(other.Scene.Fingerprint() != r.Scene.Fingerprint(), $"{style} {seed} {difficulty} is the medium level");
                        }
                        catch (Exception e) when (e is InvalidDataException or InvalidOperationException or ArgumentException) { Check(false, $"{style} {seed} {difficulty}: {e.Message}"); }
                    }
                }
                if (maps != null)
                {
                    // Three times larger, so it can be looked at.
                    int w = r.MapWidth * 3, h = r.MapHeight * 3;
                    var big = new Rgba32[w * h];
                    for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) big[y * w + x] = r.Map[(y / 3) * r.MapWidth + x / 3];
                    await TerrainTexturePngWriter.WriteRgbaAsync(Path.Combine(maps, $"{style}-{seed}.png"), w, h, big);
                }
            }
        if (only == null) Check(themesSeen.Count >= 4, $"only {themesSeen.Count} themes across the styles");
        if (only == null)
        {
            Check(highCount >= 12 && highAway / highCount >= 1.15 * looseAway / Math.Max(1, looseCount),
                $"teal and rainbow gems lie {highAway / Math.Max(1, highCount):0} from the start on average, loose gems {looseAway / Math.Max(1, looseCount):0}: not the harder places");
            // How many of them a treasure gets.
            (int, bool, bool, int, int)[] allotments = [(99, true, false, 0, 0), (100, true, false, 1, 0), (300, true, false, 1, 0), (1000, true, false, 4, 0), (199, false, true, 0, 0), (200, false, true, 0, 1),
                (700, false, true, 0, 2), (2000, false, true, 0, 3), (149, true, true, 0, 0), (150, true, true, 1, 0), (299, true, true, 1, 0), (300, true, true, 1, 1), (500, true, true, 1, 1), (800, true, true, 2, 2),
                (2000, true, true, 3, 2), (500, false, false, 0, 0)];
            foreach (var (treasure, teal, rainbow, teals, rainbows) in allotments)
                Check(LevelScatter.HighGemsFor(treasure, teal, rainbow) == (teals, rainbows), $"a treasure of {treasure} (teal {teal}, rainbow {rainbow}) gets {LevelScatter.HighGemsFor(treasure, teal, rainbow)}, not ({teals}, {rainbows})");
        }
        if (only == null) Variety(catalog, source, Check);
        Console.WriteLine(failures == 0 ? $"RANDOM_LEVEL_SMOKE_OK checks={checks}" : $"RANDOM_LEVEL_SMOKE_FAILED failures={failures} checks={checks}");
        return failures == 0 ? 0 : 1;
    }
}
