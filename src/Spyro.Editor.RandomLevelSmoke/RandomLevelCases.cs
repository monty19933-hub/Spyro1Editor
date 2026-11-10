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

    /// <summary>How far (on the map) <paramref name="p"/> is from the nearest upright face that stands between Spyro's
    /// knees and his head there.</summary>
    public static float WallDistance(SceneDocument scene, Spyro.Editor.Core.Primitives.Vector3f p)
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

    public static async Task<int> Run(string repo, string? maps, int seedsPerStyle, string? only = null)
    {
        var catalog = LevelCatalog.Load(repo);
        var source = LookSource();
        int checks = 0, failures = 0;
        void Check(bool ok, string what) { checks++; if (!ok) { failures++; Console.WriteLine("FAIL " + what); } }
        var fingerprints = new HashSet<string>();
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
                        var floors = new LevelFill.FloorGrid(home.Scene);
                        foreach (var door in home.Doorways)
                        {
                            var (fx, fy) = DoorwayShapes.Facing(door.Heading);
                            var walk = Enumerable.Range(0, 13).Select(n => new Spyro.Editor.Core.Primitives.Vector3f(door.Position.X - (float)fx * n * 60, door.Position.Y - (float)fy * n * 60, door.Position.Z)).ToList();
                            Check(floors.Holds(walk, 30), $"{style} {seed} homeworld doorway at {door.Position}: no level floor in front of it");
                            float wall = walk.Min(q => WallDistance(home.Scene, q));
                            Check(wall >= 50, $"{style} {seed} homeworld doorway at {door.Position}: a wall {wall:0} units from its way in");
                        }
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
                                for (int n = 0; n <= 11; n++)
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
        Console.WriteLine(failures == 0 ? $"RANDOM_LEVEL_SMOKE_OK checks={checks}" : $"RANDOM_LEVEL_SMOKE_FAILED failures={failures} checks={checks}");
        return failures == 0 ? 0 : 1;
    }
}
