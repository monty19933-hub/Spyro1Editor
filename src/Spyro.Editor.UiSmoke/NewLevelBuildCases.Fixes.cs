using System.Text.Json;
using Spyro.Editor.Core.Primitives;
using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

/// <summary>Objects added to a built level through the Add Object browser, as a user would: ADD_LEVEL names the
/// level, ADD_OBJECTS a JSON list of {source, row, x, y, z}. Group members the editor adds with their root (a
/// vortex's companion) come along and are not listed.</summary>
internal static partial class NewLevelBuildCases
{
    private static void AddObjects(MainWindow window)
    {
        string level = Environment.GetEnvironmentVariable("ADD_LEVEL") ?? throw new InvalidOperationException("Set ADD_LEVEL.");
        string objectsPath = Environment.GetEnvironmentVariable("ADD_OBJECTS") ?? throw new InvalidOperationException("Set ADD_OBJECTS.");
        // ADD_LEVEL: a retail level key, or "world:71" for a level of the project's new world (70-74).
        if (level.StartsWith("world:", StringComparison.Ordinal))
        {
            int id = int.Parse(level[6..]);
            var open = (Task)Invoke(window, "OpenWorldLevelAsync", id)!;
            Wait(() => open.IsCompleted, "open world level " + id, 120);
            if (open.IsFaulted) throw open.Exception!;
            if (window.FreshSceneForTesting?.WorldLevelId != id) throw new InvalidOperationException($"World level {id} did not open.");
        }
        else Load(window, level);
        var (browser, entries) = OpenBrowserReporting(window); browser.Close(); Pump(10);
        var lib = Library(window);
        var japanese = (UniversalJapaneseSource?)Invoke(window, "ResolveUniversalJapaneseSource");
        var variants = entries.SelectMany(e => e.Variants.Select(v => (Entry: e, Variant: v))).ToArray();
        // ADD_LIST=<level key>: print that level's placeable rows (to choose rows for ADD_OBJECTS).
        if (Environment.GetEnvironmentVariable("ADD_LIST") is { Length: > 0 } listed)
            foreach (var (entry, variant) in variants.Where(p => p.Variant.SourceLevelKey == listed && p.Variant.Extractable).OrderBy(p => p.Variant.SourceRow))
                Console.WriteLine($"  row {variant.SourceRow,3} class 0x{variant.ClassId:X3} {entry.Name} ({entry.Category}/{entry.SubCategory})");
        foreach (var o in JsonDocument.Parse(File.ReadAllText(objectsPath)).RootElement.EnumerateArray())
        {
            string source = o.GetProperty("source").GetString()!; int row = o.GetProperty("row").GetInt32();
            float x = o.GetProperty("x").GetSingle(), y = o.GetProperty("y").GetSingle(), z = o.GetProperty("z").GetSingle();
            var pick = variants.FirstOrDefault(p => p.Variant.SourceLevelKey == source && p.Variant.SourceRow == row);
            if (pick.Entry == null) throw new InvalidOperationException($"{source}:{row} is not in the Add Object browser");
            var before = Mobys(window).ToHashSet();
            SetField(window, "_selectedMoby", null);
            var add = (Task)Invoke(window, "AddUniversalMobyAsync", lib, pick.Entry, pick.Variant, false, japanese)!;
            Wait(() => add.IsCompleted, $"add {source}:{row}", 120);
            if (add.IsFaulted) throw add.Exception!;
            var added = Mobys(window).Where(m => !before.Contains(m)).ToList();
            if (added.Count == 0) throw new InvalidOperationException($"not added: {source}:{row}: {Status(window)}");
            Moby moby = added[0];
            SetField(window, "_selectedMoby", moby);
            Invoke(window, "NudgeSelectedMoby", x - moby.Position.X, y - moby.Position.Y, z - moby.Position.Z);
            foreach (var m in added)
                Console.WriteLine($"  {m.DisplayLabel,-34} class 0x{pick.Variant.ClassId:X3} ({m.Position.X:0},{m.Position.Y:0},{m.Position.Z:0})");
        }
        Console.WriteLine($"objects {Mobys(window).Count(m => m.IsAdded && !m.IsRemoved)}");
        Save(window);
        Console.WriteLine("NEW_LEVEL_ADD_OBJECTS_OK");
    }

    /// <summary>The Add Object browser, reporting what it shows while its catalogue loads.</summary>
    private static (Window Browser, UniversalMobyCatalogEntry[] Entries) OpenBrowserReporting(MainWindow window)
    {
        Console.WriteLine("opening Add object…");
        Click(Find<Button>(window, "UniversalMobyLibraryButton"));
        Wait(() => window.OwnedWindows.Any(w => w.Title!.StartsWith("Add object", StringComparison.Ordinal)), "browser", 60);
        Window browser = window.OwnedWindows.Last(w => w.Title!.StartsWith("Add object", StringComparison.Ordinal));
        ListBox list = Find<ListBox>(browser, "UniversalMobyList");
        int Count() => list.ItemsSource?.Cast<object>().Count() ?? 0;
        for (int minute = 0; minute < 15 && Count() <= 150; minute++)
        {
            try { Wait(() => Count() > 150, "catalogue", 60); }
            catch (InvalidOperationException)
            {
                string Text(string name) { try { return Find<TextBlock>(browser, name).Text ?? ""; } catch { return "?"; } }
                Console.WriteLine($"  after {minute + 1} min: {Count()} entries; summary '{Text("UniversalMobySummary")}'; empty '{Text("UniversalMobyEmpty")}'; title '{browser.Title}'");
            }
        }
        if (Count() <= 150) throw new InvalidOperationException("The Add Object catalogue did not load.");
        return (browser, list.ItemsSource!.Cast<UniversalMobyCatalogEntry>().ToArray());
    }

    /// <summary>Lost Fleet, second pass: every face takes Year of the Dragon's sides (LOSTFLEET_FACES is the lab's
    /// faces.json; flag 0x04 shows the face's other side, flag 0x08, set only on the acid, draws both; see
    /// <see cref="LostFleetSides"/>), and every gem stands as high above the ground under it as retail gems do (measured
    /// in GEM_LIFT_LEVEL, default Toasty, an unedited retail level). The lostfleet-holes step does the rest of the
    /// 2026-10-10 hole fixes.</summary>
    private static void LostFleetFix(MainWindow window)
    {
        string facesPath = Environment.GetEnvironmentVariable("LOSTFLEET_FACES") ?? throw new InvalidOperationException("Set LOSTFLEET_FACES.");
        var tryZ = typeof(MainWindow).GetMethod("TryFindTerrainZAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        float? Ground(float x, float y, float refZ)
        {
            var args = new object?[] { x, y, refZ, 0f, -1, false };
            return (bool)tryZ.Invoke(window, args)! ? (float)args[3]! : null;
        }
        string probe = Environment.GetEnvironmentVariable("GEM_LIFT_LEVEL") ?? "toasty";
        Load(window, probe);
        var lifts = Mobys(window).Where(m => m.IsVisibleGem && !m.IsAdded && !m.IsRemoved)
            .Select(m => m.Position.Z - Ground(m.Position.X, m.Position.Y, m.Position.Z + 8)).OfType<float>().Order().ToList();
        if (lifts.Count < 10) throw new InvalidOperationException($"Only {lifts.Count} gems measured in {probe}.");
        float lift = MathF.Round(lifts[lifts.Count / 2]);
        Console.WriteLine($"{probe}: {lifts.Count} gems, lift above the ground {lifts[0]:0}..{lifts[^1]:0}, median {lift}");

        Load(window, "artisans");
        var doc = window.FreshSceneForTesting ?? throw new InvalidOperationException("Artisans is not a built level");
        var (surfaces, reversed, twoSided, changed) = LostFleetSides(doc.Surfaces, LostFleetFaces());
        Console.WriteLine($"lost fleet faces: {reversed} shown from their other side, {twoSided} two-sided (acid); {changed} changed");
        ApplyScene(window, doc with { Surfaces = surfaces });

        int moved = 0;
        foreach (var gem in Mobys(window).Where(m => m.IsAdded && !m.IsRemoved && m.IsVisibleGem).ToList())
        {
            if (Ground(gem.Position.X, gem.Position.Y, gem.Position.Z + 8) is not float ground) { Console.WriteLine($"  no ground under {gem.DisplayLabel} at ({gem.Position.X:0},{gem.Position.Y:0})"); continue; }
            float dz = ground + lift - gem.Position.Z;
            if (Math.Abs(dz) < 1) continue;
            SetField(window, "_selectedMoby", gem);
            Invoke(window, "NudgeSelectedMoby", 0f, 0f, dz);
            Console.WriteLine($"  {gem.DisplayLabel,-20} ({gem.Position.X:0},{gem.Position.Y:0}) z {gem.Position.Z - dz:0} -> {gem.Position.Z:0} (ground {ground:0})");
            moved++;
        }
        Console.WriteLine($"gems moved: {moved}");
        Save(window);
        Console.WriteLine("NEW_LEVEL_LOSTFLEET_FIX_OK");
    }

    /// <summary>Lost Fleet's own creatures in place of Artisans' gnorcs: each gnorc is removed and a Lost Fleet creature
    /// (Spyro: Year of the Dragon) is added where it stood through the Add Object browser - the crab where the
    /// gnorc with the blue gem (5) stood, rhynocs (green gems) at the others - so the level's treasure is unchanged.</summary>
    private static void LostFleetCreatures(MainWindow window)
    {
        Load(window, "artisans");
        var gnorcs = Mobys(window).Where(m => m.IsAdded && !m.IsRemoved && m.CrossLevelSourceLevelKey == "artisans" && m.Type == 0x72).ToList();
        if (gnorcs.Count == 0) gnorcs = Mobys(window).Where(m => m.IsAdded && !m.IsRemoved && m.DisplayLabel.Contains("Gnorc", StringComparison.OrdinalIgnoreCase)).ToList();
        if (gnorcs.Count != 4) throw new InvalidOperationException($"Expected Lost Fleet's 4 gnorcs, found {gnorcs.Count}: {string.Join(", ", gnorcs.Select(g => g.DisplayLabel))}");
        int before = Mobys(window).Where(m => m.IsAdded && !m.IsRemoved).Sum(m => m.TreasureValue);
        var spots = gnorcs.Select(g => (g.DisplayLabel, g.Position, Reward: g.RewardGem.Value, Yaw: g.YawByte)).ToList();
        foreach (var g in gnorcs) { SetField(window, "_selectedMoby", g); Invoke(window, "RemoveSelectedMoby"); }
        var (browser, entries) = OpenBrowserReporting(window); browser.Close(); Pump(10);
        var lib = Library(window);
        var japanese = (UniversalJapaneseSource?)Invoke(window, "ResolveUniversalJapaneseSource");
        var rhynoc = entries.Single(e => e.ClassId == UniversalCrashEnemies.Rhynoc.Class && e.Default.SourceLevelKey == UniversalCrashEnemies.YotdSourceKey);
        var crab = entries.Single(e => e.ClassId == UniversalCrashEnemies.Crab.Class && e.Default.SourceLevelKey == UniversalCrashEnemies.YotdSourceKey);
        if (rhynoc.RestrictionReason != null) throw new InvalidOperationException("Lost Fleet's creatures are locked: " + rhynoc.RestrictionReason);
        int crabAt = spots.FindIndex(s => s.Reward == 5);
        if (crabAt < 0) throw new InvalidOperationException("No gnorc carried the blue gem: " + string.Join(", ", spots.Select(s => s.Reward)));
        for (int i = 0; i < spots.Count; i++)
        {
            var entry = i == crabAt ? crab : rhynoc;
            var added = Mobys(window).ToHashSet();
            SetField(window, "_selectedMoby", null);
            var add = (Task)Invoke(window, "AddUniversalMobyAsync", lib, entry, entry.Default, false, japanese)!;
            Wait(() => add.IsCompleted, "add " + entry.Name, 180);
            if (add.IsFaulted) throw add.Exception!;
            var moby = Mobys(window).FirstOrDefault(m => !added.Contains(m)) ?? throw new InvalidOperationException($"not added: {entry.Name}: {Status(window)}");
            SetField(window, "_selectedMoby", moby);
            var p = spots[i].Position;
            Invoke(window, "NudgeSelectedMoby", p.X - moby.Position.X, p.Y - moby.Position.Y, p.Z - moby.Position.Z);
            moby.YawByte = spots[i].Yaw;
            Console.WriteLine($"  {spots[i].DisplayLabel} -> {moby.DisplayLabel} at ({moby.Position.X:0},{moby.Position.Y:0},{moby.Position.Z:0}) gem {moby.TreasureValue}");
        }
        int after = Mobys(window).Where(m => m.IsAdded && !m.IsRemoved).Sum(m => m.TreasureValue);
        Console.WriteLine($"editor-known treasure {before} -> {after}");
        Save(window);
        Console.WriteLine("NEW_LEVEL_LOSTFLEET_CREATURES_OK");
    }

    /// <summary>Smaller tiles for a built level (RETILE_LEVEL; RETILE_MAX, default 130 units): every tile whose sides are
    /// longer is split into a grid of tiles at most that long, each showing the tile's whole picture, so the texture is
    /// drawn finer and swims less near the camera (what WumpaVillage's Detail does for Wumpa). The points along a side
    /// are computed from its endpoints in a fixed order, so two tiles sharing that side split it at the same points.
    /// Edge pictures (a border drawn along one side) must not be repeated inside: RETILE_STRIP names pictures whose
    /// border runs along corners 0-1 or 3-2 (split only along that border, so every piece keeps its slice of it) and
    /// RETILE_KEEP pictures that stay whole (a border on two sides); the sides they keep whole stay whole in their
    /// neighbours too.</summary>
    private static void Retile(MainWindow window)
    {
        string level = Environment.GetEnvironmentVariable("RETILE_LEVEL") ?? throw new InvalidOperationException("Set RETILE_LEVEL.");
        float max = float.Parse(Environment.GetEnvironmentVariable("RETILE_MAX") ?? "130", System.Globalization.CultureInfo.InvariantCulture);
        Load(window, level);
        var doc = window.FreshSceneForTesting ?? throw new InvalidOperationException(level + " is not a built level");
        static float Len(Vector3f a, Vector3f b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
        static Vector3f Lerp(Vector3f a, Vector3f b, float t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
        static Vector3f Round(Vector3f v) => new(MathF.Round(v.X), MathF.Round(v.Y), MathF.Round(v.Z));
        static bool Before(Vector3f a, Vector3f b) => a.X != b.X ? a.X < b.X : a.Y != b.Y ? a.Y < b.Y : a.Z < b.Z;
        // Point i of n along side a-b, the same whichever way round the side is walked.
        static Vector3f OnSide(Vector3f a, Vector3f b, int i, int n) => Before(a, b) ? Round(Lerp(a, b, (float)i / n)) : Round(Lerp(b, a, (float)(n - i) / n));
        static ColorRgba Mix(ColorRgba a, ColorRgba b, ColorRgba c, ColorRgba d, float s, float t)
        {
            byte C(byte w, byte x, byte y, byte z) => (byte)Math.Clamp(MathF.Round((w * (1 - s) + x * s) * (1 - t) + (z * (1 - s) + y * s) * t), 0, 255);
            return new(C(a.R, b.R, c.R, d.R), C(a.G, b.G, c.G, d.G), C(a.B, b.B, c.B, d.B), C(a.A, b.A, c.A, d.A));
        }
        // A side's number of pieces must agree with the opposite side of its tile (the grid) and with the same side of
        // the neighbouring tile (no cracks): sides are grouped across both links and a group takes the count its
        // longest side needs.
        Dictionary<(Vector3f, Vector3f), (Vector3f, Vector3f)> parent = [];
        static (Vector3f, Vector3f) Side(Vector3f a, Vector3f b) => Before(a, b) ? (a, b) : (b, a);
        (Vector3f, Vector3f) Find((Vector3f, Vector3f) x)
        {
            if (!parent.TryGetValue(x, out var p)) { parent[x] = x; return x; }
            return p.Equals(x) ? x : parent[x] = Find(p);
        }
        void Union((Vector3f, Vector3f) a, (Vector3f, Vector3f) b) { var ra = Find(a); var rb = Find(b); if (!ra.Equals(rb)) parent[ra] = rb; }
        foreach (var f in doc.Surfaces.Where(f => f.Vertices.Length == 4))
        {
            var v = f.Vertices;
            Union(Side(v[0], v[1]), Side(v[3], v[2]));
            Union(Side(v[0], v[3]), Side(v[1], v[2]));
        }
        Dictionary<(Vector3f, Vector3f), int> pieces = [];
        foreach (var side in parent.Keys.ToList())
        {
            var root = Find(side);
            int n = Math.Max(1, (int)MathF.Ceiling(Len(side.Item1, side.Item2) / max));
            pieces[root] = Math.Max(pieces.GetValueOrDefault(root, 1), n);
        }
        HashSet<string> Names(string variable) => [.. (Environment.GetEnvironmentVariable(variable) ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)];
        var strip = Names("RETILE_STRIP"); var keep = Names("RETILE_KEEP");
        string Picture(Spyro.Editor.Core.Editing.AuthoredSurface f) => Path.GetFileName(f.Material?.Image ?? "");
        foreach (var f in doc.Surfaces.Where(f => f.Vertices.Length == 4))
        {
            var v = f.Vertices;
            if (strip.Contains(Picture(f)) || keep.Contains(Picture(f))) pieces[Find(Side(v[0], v[3]))] = 1;
            if (keep.Contains(Picture(f))) pieces[Find(Side(v[0], v[1]))] = 1;
        }
        List<Spyro.Editor.Core.Editing.AuthoredSurface> result = [];
        int split = 0;
        foreach (var f in doc.Surfaces)
        {
            var v = f.Vertices;
            if (v.Length != 4) { result.Add(f); continue; }
            // Corners 0-1 and 3-2 run along s, 0-3 and 1-2 along t.
            int ns = pieces[Find(Side(v[0], v[1]))], nt = pieces[Find(Side(v[0], v[3]))];
            if (ns <= 1 && nt <= 1) { result.Add(f); continue; }
            Vector3f P(int i, int j)
            {
                if (j == 0) return OnSide(v[0], v[1], i, ns);
                if (j == nt) return OnSide(v[3], v[2], i, ns);
                if (i == 0) return OnSide(v[0], v[3], j, nt);
                if (i == ns) return OnSide(v[1], v[2], j, nt);
                float s = (float)i / ns, t = (float)j / nt;
                return Round(Lerp(Lerp(v[0], v[1], s), Lerp(v[3], v[2], s), t));
            }
            var near = f.NearColors ?? [f.Color, f.Color, f.Color, f.Color];
            var far = f.FarColors ?? [f.Color, f.Color, f.Color, f.Color];
            for (int j = 0; j < nt; j++)
                for (int i = 0; i < ns; i++)
                {
                    (int I, int J)[] k = [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)];
                    var pts = k.Select(c => P(c.I, c.J)).ToArray();
                    var nc = k.Select(c => Mix(near[0], near[1], near[2], near[3], (float)c.I / ns, (float)c.J / nt)).ToArray();
                    var fc = k.Select(c => Mix(far[0], far[1], far[2], far[3], (float)c.I / ns, (float)c.J / nt)).ToArray();
                    result.Add(f with { Id = Guid.NewGuid().ToString("N"), Vertices = pts, NearColors = nc, FarColors = fc, Color = nc[0] });
                }
            split++;
        }
        Console.WriteLine($"{level}: {split} of {doc.Surfaces.Count} tiles split, {doc.Surfaces.Count} -> {result.Count} tiles (at most {max} units)");
        ApplyScene(window, doc with { Surfaces = result });
        Console.WriteLine("NEW_LEVEL_RETILE_OK");
    }
}
