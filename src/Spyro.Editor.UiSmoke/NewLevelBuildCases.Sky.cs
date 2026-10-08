using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

internal static partial class NewLevelBuildCases
{
    internal sealed class SceneBuilder(int dream, string? pictures = null)
    {
        public readonly List<AuthoredSurface> Surfaces = [];
        public AuthoredMaterial Mat(int texture, int wad = -1) => new(wad < 0 ? dream : wad, texture);
        /// <summary>A Dream Weavers texture slot carrying one of the level's own pictures (pictures/name.png).</summary>
        public AuthoredMaterial Picture(int carrier, string name) =>
            new(dream, carrier) { Image = Path.GetFullPath(Path.Combine(pictures ?? throw new InvalidOperationException("No pictures folder."), name + ".png")) };
        static string Id() => Guid.NewGuid().ToString("N");
        // Horizontal tile; counter-clockwise seen from above.
        public void Floor(float x0, float y0, float x1, float y1, float z, AuthoredMaterial? m, ColorRgba? c = null) =>
            Surfaces.Add(new(Id(), [new(x0, y0, z), new(x1, y0, z), new(x1, y1, z), new(x0, y1, z)], c ?? new(160, 160, 160), m));
        // Vertical wall from (ax,ay) to (bx,by), bottom z0 to top z1, corners bottom-a, bottom-b, top-b, top-a.
        public void Wall(float ax, float ay, float bx, float by, float z0, float z1, AuthoredMaterial? m, ColorRgba? c = null) =>
            Surfaces.Add(new(Id(), [new(ax, ay, z0), new(bx, by, z0), new(bx, by, z1), new(ax, ay, z1)], c ?? new(160, 160, 160), m));
        public void Raw(Vector3f[] v, AuthoredMaterial? m, ColorRgba? c = null) => Surfaces.Add(new(Id(), v, c ?? new(160, 160, 160), m));
    }

    internal static void StartBlank(MainWindow window, string key)
    {
        Load(window, key);
        if (window.FreshSceneForTesting != null) { Console.WriteLine($"{key} is already blank"); return; }
        var task = (Task)Invoke(window, "StartBlankCurrentLevelAsync")!;
        Wait(() => window.OwnedWindows.Any(w => w.Name == "StartBlankDialog"), "start blank dialog", 60);
        Click(Find<Button>(window.OwnedWindows.First(w => w.Name == "StartBlankDialog"), "StartBlankConfirm"));
        Wait(() => task.IsCompleted, "start blank", 600);
        Console.WriteLine("start blank: " + Status(window));
        if (window.FreshSceneForTesting == null) throw new InvalidOperationException("Start Blank did not produce a blank scene");
    }

    internal static void ApplyScene(MainWindow window, SceneDocument doc)
    {
        Invoke(window, "ApplyFreshScene", doc); Pump(20);
        Save(window);
        Console.WriteLine($"scene: {window.FreshSceneForTesting!.Surfaces.Count} faces, {window.FreshSceneForTesting.Objects.Count} objects, name {window.FreshSceneForTesting.Name}");
    }

    private static void BlankTrial(MainWindow window)
    {
        StartBlank(window, "townsquare");
        var catalog = Get<LevelCatalog>(window, "_retailCatalog");
        int dream = catalog.FindByKey("dreamweavers")!.SourceWadEntry;
        var b = new SceneBuilder(dream);
        const float cx = 3000, cy = 3000, z = 1600;
        for (int i = -1; i <= 1; i++) for (int j = -1; j <= 1; j++)
            b.Floor(cx - 120 + i * 240, cy - 120 + j * 240, cx + 120 + i * 240, cy + 120 + j * 240, z, b.Mat(4));
        b.Wall(3360, 2640, 3360, 2880, z, z + 240, b.Mat(36));          // order A
        b.Wall(3360, 3360, 3360, 3120, z, z + 240, b.Mat(36));          // order B (reversed)
        b.Floor(3360, 2880, 3600, 3120, z - 100, b.Mat(30));            // cloud step
        b.Floor(2880, 3360, 3120, 3600, z, b.Mat(0));                   // fringe tile, north
        var doc = window.FreshSceneForTesting! with
        {
            Name = "CLOUDSPIRE ISLES", Spawn = new(cx, cy, z + 32), Heading = 0,
            SkyColor = new(120, 150, 230), MusicTrack = 14, Surfaces = b.Surfaces
        };
        ApplyScene(window, doc);
        Console.WriteLine("NEW_LEVEL_BLANKTRIAL_OK");
    }
}

internal static partial class NewLevelBuildCases
{
    internal const float T = 240;
    // Island tops are Dream Weavers grass whose edge tiles puff into cloud (the level's own fringe pictures, made from
    // Dream Weavers' grass and cloud), or pale stone; every outside edge hangs a cloud side. Walls and sides are
    // drawn on the right-hand side of a->b, so island edges run counter-clockwise.
    internal static void Island(SceneBuilder b, float x0, float y0, int nx, int ny, float z, bool grass, int seed, int stoneTex = 35, bool cap = true)
    {
        var rnd = new Random(seed);
        ColorRgba under = new(118, 122, 150);
        // Soft light and shade across the top so it does not read as one flat colour.
        ColorRgba Top(Vector3f p) { int v = 146 + (int)(10 * Math.Sin(p.X * 0.011 + p.Y * 0.007) + 6 * Math.Sin(p.X * 0.005 - p.Y * 0.013)); return new((byte)v, (byte)v, (byte)Math.Min(255, v + 4)); }
        ColorRgba top = new(150, 150, 150);
        for (int i = 0; i < nx; i++) for (int j = 0; j < ny; j++)
        {
            float a = x0 + i * T, c = y0 + j * T, bb = a + T, d = c + T;
            Vector3f p00 = new(a, c, z), p10 = new(bb, c, z), p11 = new(bb, d, z), p01 = new(a, d, z);
            Vector3f[] corners = [p00, p10, p11, p01];
            AuthoredMaterial m; int rot = 0;
            bool s = j == 0, n = j == ny - 1, w = i == 0, e = i == nx - 1;
            if (!grass) m = b.Mat(stoneTex);
            else if (!(s || n || w || e) || nx == 1 && ny == 1) m = b.Mat(4);   // inner tiles and one-tile islets: plain grass
            else if ((s || n) && (w || e))
            {
                // Corner: the picture puffs along its bottom and right edges, turned so that corner is the island's.
                m = b.Picture(1, "fringecorner");
                rot = s ? (w ? 2 : 3) : (w ? 1 : 0);
            }
            else
            {
                m = b.Picture(0, "fringe");   // its cloud edge is the texture's bottom (corners 2-3)
                rot = n ? 0 : s ? 2 : w ? 1 : 3;
            }
            Vector3f[] v = Enumerable.Range(0, 4).Select(k => corners[(k + rot) % 4]).ToArray();
            b.Surfaces.Add(new(Guid.NewGuid().ToString("N"), v, top, m, v.Select(Top).ToArray(), v.Select(Top).ToArray()));
        }
        // Underside: an inverted, closed frustum of cloud like a floating island. Each outside edge tile hangs one
        // planar quad whose bottom edge is pulled in toward the middle; neighbours share their bottom corners.
        // Quad corners 2-3 are the texture's bottom edge, so the cloud's puffy edge hangs at the bottom.
        float inset = Math.Min(233, Math.Min(nx, ny) * T / 2 - 10), depth = 250;
        float bx0 = x0 + inset, by0 = y0 + inset, bx1 = x0 + nx * T - inset, by1 = y0 + ny * T - inset, zb = z - depth;
        Vector3f Under(float x, float y) => new(Math.Clamp(x, bx0, bx1), Math.Clamp(y, by0, by1), zb);
        List<(Vector3f A, Vector3f B)> edges = [];
        for (int i = 0; i < nx; i++) edges.Add((new(x0 + i * T, y0, z), new(x0 + (i + 1) * T, y0, z)));
        for (int j = 0; j < ny; j++) edges.Add((new(x0 + nx * T, y0 + j * T, z), new(x0 + nx * T, y0 + (j + 1) * T, z)));
        for (int i = nx; i > 0; i--) edges.Add((new(x0 + i * T, y0 + ny * T, z), new(x0 + (i - 1) * T, y0 + ny * T, z)));
        for (int j = ny; j > 0; j--) edges.Add((new(x0, y0 + j * T, z), new(x0, y0 + (j - 1) * T, z)));
        foreach (var (A, B) in edges)
        {
            Vector3f a = Under(A.X, A.Y), bb = Under(B.X, B.Y);
            b.Surfaces.Add(new(Guid.NewGuid().ToString("N"), [B, A, a, bb], top, b.Mat(rnd.Next(2) == 0 ? 30 : 31),
                [top, top, under, under], [top, top, under, under]));
        }
        // Close the bottom when the remaining hole is a real face (at most 255 units, one collision pair).
        if (cap && bx1 - bx0 >= 64 && by1 - by0 >= 64)
            b.Surfaces.Add(new(Guid.NewGuid().ToString("N"), [new(bx0, by0, zb), new(bx0, by1, zb), new(bx1, by1, zb), new(bx1, by0, zb)],
                under, b.Mat(31), null, null));
    }

    // A square pillar (footprint w x w) with brick walls facing out and a stone cap.
    internal static void Pillar(SceneBuilder b, float x, float y, float w, float z0, float z1, int brick = 36)
    {
        ColorRgba lit = new(150, 150, 150), dark = new(90, 85, 100);
        void Wall(float ax, float ay, float bx, float by) => b.Surfaces.Add(new(Guid.NewGuid().ToString("N"),
            [new(ax, ay, z0), new(bx, by, z0), new(bx, by, z1), new(ax, ay, z1)], lit, b.Mat(brick), [dark, dark, lit, lit], [dark, dark, lit, lit]));
        Wall(x, y, x + w, y); Wall(x + w, y, x + w, y + w); Wall(x + w, y + w, x, y + w); Wall(x, y + w, x, y);
        b.Floor(x, y, x + w, y + w, z1, b.Mat(35), lit);
    }

    internal static SceneDocument Cloudspire(SceneDocument blank, int dream, int? skyWad = null, string? pictures = null)
    {
        var b = new SceneBuilder(dream, pictures);
        ColorRgba lit = new(150, 150, 150);
        // A  Sunrise Isle (start)
        Island(b, 2640, 2640, 3, 3, 1600, true, 1);
        // stepping clouds A -> B and A -> S
        b.Floor(3430, 2880, 3670, 3120, 1600, b.Mat(30), lit);
        b.Floor(3740, 2880, 3980, 3120, 1600, b.Mat(31), lit);
        b.Floor(2880, 2330, 3120, 2570, 1600, b.Mat(30), lit);
        // B  Brick Bastion, gate pillars at its west entrance
        Island(b, 4050, 2640, 3, 3, 1600, false, 2);
        Pillar(b, 4080, 2780, 60, 1600, 1800);
        Pillar(b, 4080, 3160, 60, 1600, 1800);
        // cloud stair B -> Tower: 6 steps of 40 rising north
        for (int k = 1; k <= 6; k++) b.Floor(4290, 3360 + (k - 1) * 100, 4530, 3360 + k * 100, 1600 + 40 * k, b.Mat(k % 2 == 0 ? 30 : 31), lit);
        // T  Sky Tower top, brick walls below
        b.Floor(4290, 3960, 4530, 4200, 1840, b.Mat(35), lit);
        {
            ColorRgba dark = new(90, 85, 100);
            void Wall(float ax, float ay, float bx, float by) => b.Surfaces.Add(new(Guid.NewGuid().ToString("N"),
                [new(ax, ay, 1600), new(bx, by, 1600), new(bx, by, 1840), new(ax, ay, 1840)], lit, b.Mat(36), [dark, dark, lit, lit], [dark, dark, lit, lit]));
            Wall(4290, 3960, 4530, 3960); Wall(4530, 3960, 4530, 4200); Wall(4530, 4200, 4290, 4200); Wall(4290, 4200, 4290, 3960);
        }
        // D  Cloud Garden: a straight glide from the tower lands here (Spyro's glide sinks about 1 in 10). A raised
        // pool of Dream Weavers water (a still frame: its own water texture animates) is the game's water: it costs
        // Spyro a life.
        Island(b, 3120, 3760, 3, 2, 1790, true, 3);
        {
            const float px0 = 3410, py0 = 3930, px1 = 3550, py1 = 4070, pz = 1814;
            ColorRgba dark = new(100, 100, 120);
            void Rim(float ax, float ay, float bx, float by) => b.Surfaces.Add(new(Guid.NewGuid().ToString("N"),
                [new(bx, by, pz), new(ax, ay, pz), new(ax, ay, 1790), new(bx, by, 1790)], lit, b.Mat(35), [lit, lit, dark, dark], [lit, lit, dark, dark]));
            Rim(px0, py0, px1, py0); Rim(px1, py0, px1, py1); Rim(px1, py1, px0, py1); Rim(px0, py1, px0, py0);
            b.Surfaces.Add(new(Guid.NewGuid().ToString("N"), [new(px0, py0, pz), new(px1, py0, pz), new(px1, py1, pz), new(px0, py1, pz)],
                lit, b.Picture(2, "water")) { Kind = AuthoredSurfaceKind.Water });
        }
        // cloud stair from the garden down to Sunrise Isle: 5 steps of 38
        for (int k = 1; k <= 5; k++) b.Floor(3120, 3760 - k * 80, 3360, 3760 - (k - 1) * 80, 1790 - 38 * k, b.Mat(k % 2 == 0 ? 30 : 31), lit);
        // E  Egg Islet, a cloud stair below the garden
        Island(b, 3360, 4740, 1, 1, 1550, true, 4);
        int[] depths = [84, 83, 83, 83, 83, 84]; float yy = 4740;
        for (int k = 1; k <= 6; k++) { b.Floor(3360, yy - depths[k - 1], 3600, yy, 1550 + 40 * k, b.Mat(k % 2 == 0 ? 31 : 30), lit); yy -= depths[k - 1]; }
        // S  South Meadow
        Island(b, 2640, 1780, 3, 2, 1600, true, 5);
        return blank with
        {
            Name = "CLOUDSPIRE ISLES", Spawn = new(3000, 3000, 1632), Heading = 0,
            SkyColor = new(130, 150, 235), MusicTrack = 14, Surfaces = b.Surfaces, SkySourceWadEntry = skyWad
        };
    }

    private static void SkyBuild(MainWindow window)
    {
        StartBlank(window, "townsquare");
        var catalog = Get<LevelCatalog>(window, "_retailCatalog");
        string skyKey = Environment.GetEnvironmentVariable("CLOUDSPIRE_SKY") ?? "dreamweavers";
        var doc = Cloudspire(window.FreshSceneForTesting!, catalog.FindByKey("dreamweavers")!.SourceWadEntry,
            skyKey == "none" ? null : catalog.FindByKey(skyKey)!.SourceWadEntry,
            Path.Combine(Workspace(window).RootPath, "custom-terrain-textures", "cloudspire"));
        Console.WriteLine($"faces {doc.Surfaces.Count}, textured {doc.Surfaces.Count(s => s.Material != null)}");
        ApplyScene(window, doc);
        Console.WriteLine("NEW_LEVEL_SKYBUILD_OK");
    }
}

internal static partial class NewLevelBuildCases
{
    // (what, class, source level or special id, x, y, island z)
    private static readonly (string What, int Class, string Source, float X, float Y, float Z)[] SkyObjects =
    [
        ("sign", 0x1D0, "japan", 3130, 3150, 1600),
        // Sunrise Isle and the stepping clouds: red gems
        ("gem", 0x53, "artisans", 2760, 2760, 1600), ("gem", 0x53, "artisans", 2760, 3240, 1600),
        ("gem", 0x53, "artisans", 3240, 2760, 1600), ("gem", 0x53, "artisans", 2880, 3000, 1600),
        ("gem", 0x56, "clifftown", 3000, 3240, 1600), ("gem", 0x54, "artisans", 3240, 3240, 1600),
        ("gem", 0x54, "artisans", 3550, 3000, 1600), ("gem", 0x54, "artisans", 3860, 3000, 1600), ("gem", 0x53, "artisans", 3000, 2450, 1600),
        // Brick Bastion
        ("enemy", 0x082, "dreamweavers", 4330, 2840, 1600), ("enemy", 0x082, "dreamweavers", 4520, 3160, 1600),
        ("gem", 0x55, "townsquare", 4250, 2700, 1600), ("gem", 0x55, "townsquare", 4250, 3300, 1600),
        ("gem", 0x55, "townsquare", 4600, 2760, 1600), ("gem", 0x55, "townsquare", 4600, 3240, 1600),
        ("gem", 0x56, "clifftown", 4410, 3000, 1600),
        ("prop", 0x040, "dreamweavers", 4110, 2810, 1800), ("prop", 0x040, "dreamweavers", 4110, 3190, 1800),
        ("egg:dragonfly-egg-pink", 0x1E9, "japan", 4720, 3000, 1600),
        // cloud stair to the Sky Tower
        ("gem", 0x54, "artisans", 4410, 3510, 1680), ("gem", 0x54, "artisans", 4410, 3710, 1760),
        // Sky Tower top
        ("egg:dragonfly-egg-gold", 0x1E9, "japan", 4360, 4080, 1840), ("gem", 0x57, "doctorshemp", 4470, 4080, 1840),
        // Cloud Garden
        ("enemy", 0x05C, "dreamweavers", 3300, 3900, 1790), ("enemy", 0x05C, "dreamweavers", 3700, 4100, 1790),
        ("enemy", 0x034, "loftycastle", 3480, 4150, 1790),
        ("gem", 0x54, "artisans", 3200, 3820, 1790), ("gem", 0x54, "artisans", 3760, 3820, 1790),
        ("gem", 0x54, "artisans", 3200, 4180, 1790), ("gem", 0x54, "artisans", 3760, 4180, 1790),
        ("prop", 0x150, "artisans", 3300, 4180, 1790), ("prop", 0x150, "artisans", 3660, 3850, 1790),
        // stair down to the Egg Islet
        ("gem", 0x54, "artisans", 3480, 4615, 1630), ("gem", 0x54, "artisans", 3480, 4365, 1750),
        // Egg Islet
        ("egg:dragonfly-egg-blue", 0x1E9, "japan", 3420, 4860, 1550), ("gem", 0x57, "doctorshemp", 3540, 4860, 1550),
        // South Meadow
        ("sheep", 0x00A, "artisans", 2760, 2100, 1600), ("sheep", 0x00A, "artisans", 3000, 1880, 1600), ("sheep", 0x00A, "artisans", 3240, 2120, 1600),
        ("enemy", 0x03E, "loftycastle", 2850, 1950, 1600), ("enemy", 0x03E, "loftycastle", 3150, 2160, 1600),
        ("enemy", 0x034, "loftycastle", 3000, 2060, 1600),
        ("gem", 0x56, "clifftown", 2700, 1840, 1600), ("gem", 0x56, "clifftown", 3300, 1840, 1600), ("gem", 0x56, "clifftown", 3000, 2200, 1600),
        ("gem", 0x57, "doctorshemp", 3300, 2200, 1600),
        ("egg:dragonfly-egg-purple", 0x1E9, "japan", 2700, 2200, 1600),
    ];

    // Round 4: the Fat Bats flee and never return, so ground enemies take their place; scenery is added.
    // Only these are added: every object ever placed keeps a save row, and re-adding all would overflow them.
    private static readonly (string What, int Class, string Source, float X, float Y, float Z)[] SkyUpdates =
    [
        ("enemy", 0x07A, "darkpassage", 2850, 1950, 1600), ("enemy", 0x032, "darkpassage", 3150, 2160, 1600),
        ("prop", 0x163, "artisans", 2710, 2710, 1600), ("prop", 0x164, "artisans", 2710, 3290, 1600),
        ("prop", 0x156, "artisans", 3290, 3290, 1600), ("prop", 0x156, "artisans", 3810, 4000, 1790),
        ("prop", 0x162, "artisans", 2700, 2010, 1600), ("prop", 0x156, "artisans", 3300, 2010, 1600),
        ("prop", 0x0AC, "artisans", 4500, 4170, 1840), ("chest", 0x1A5, "artisans", 4720, 2700, 1600),
    ];

    private static void SkyUpdateStep(MainWindow window)
    {
        Load(window, "townsquare");
        if (window.FreshSceneForTesting == null) throw new InvalidOperationException("Town Square is not blank");
        foreach (var bat in Mobys(window).Where(m => m.IsAdded && !m.IsRemoved && m.DisplayLabel.StartsWith("Fat Bat")).ToList())
        { SetField(window, "_selectedMoby", bat); Invoke(window, "RemoveSelectedMoby"); Console.WriteLine("  removed " + bat.DisplayLabel); }
        AddSkyObjects(window, SkyUpdates, removeAll: false);
        Console.WriteLine("NEW_LEVEL_SKYUPDATE_OK");
    }

    // Drives the level-settings Skybox picker and button as a user would; nothing is saved.
    private static void SkyPickerStep(MainWindow window)
    {
        Load(window, "townsquare");
        if (window.FreshSceneForTesting == null) throw new InvalidOperationException("Town Square is not blank");
        var panel = Get<StackPanel>(window, "_freshSettingsPanel");
        var picker = Find<ComboBox>(panel, "FreshMainSkySource");
        var apply = Find<Button>(panel, "FreshMainSetSkySource");
        var levels = picker.ItemsSource!.Cast<object>().ToList();
        Console.WriteLine($"picker: {levels.Count} choices, selected {picker.SelectedItem}, flight offered {levels.OfType<LevelDefinition>().Any(l => l.Key.EndsWith("flight"))}");
        var donor = levels.OfType<LevelDefinition>().First(l => l.Key == "clifftown");
        picker.SelectedItem = donor; Pump(); Click(apply);
        Console.WriteLine($"after Use this skybox: SkySourceWadEntry {window.FreshSceneForTesting!.SkySourceWadEntry} (Cliff Town is {donor.SourceWadEntry}); status: {Status(window)}");
        picker.SelectedItem = levels[0]; Pump(); Click(apply);
        Console.WriteLine($"after plain color: SkySourceWadEntry {window.FreshSceneForTesting!.SkySourceWadEntry?.ToString() ?? "none"}; status: {Status(window)}");
        Console.WriteLine("NEW_LEVEL_SKYPICKER_OK");
    }

    // Dark Passage enemies cannot export beside recoloured dragons (both own code near 0x80017188), so the
    // meadow uses a Tin Soldier (Haunted Towers) and a Bull (Town Square's own) instead.
    private static void SkyUpdate2Step(MainWindow window)
    {
        Load(window, "townsquare");
        if (window.FreshSceneForTesting == null) throw new InvalidOperationException("Town Square is not blank");
        foreach (var m in Mobys(window).Where(m => m.IsAdded && !m.IsRemoved && (m.DisplayLabel.StartsWith("Lamp Fool") || m.DisplayLabel.StartsWith("Puppy"))).ToList())
        { SetField(window, "_selectedMoby", m); Invoke(window, "RemoveSelectedMoby"); Console.WriteLine("  removed " + m.DisplayLabel); }
        AddSkyObjects(window, [("enemy", 0x0CB, "hauntedtowers", 2850, 1950, 1600), ("enemy", 0x017, "townsquare", 3150, 2160, 1600)], removeAll: false);
        Console.WriteLine("NEW_LEVEL_SKYUPDATE2_OK");
    }

    // A Tin Soldier cannot export beside the project's save-row compaction (its helper proof covers that code),
    // so the meadow keeps the Bull and gets a Lamp Fool; Dark Passage enemies now export beside recoloured dragons.
    private static void SkyUpdate3Step(MainWindow window)
    {
        Load(window, "townsquare");
        if (window.FreshSceneForTesting == null) throw new InvalidOperationException("Town Square is not blank");
        foreach (var m in Mobys(window).Where(m => m.IsAdded && !m.IsRemoved && m.DisplayLabel.StartsWith("Tin Soldier")).ToList())
        { SetField(window, "_selectedMoby", m); Invoke(window, "RemoveSelectedMoby"); Console.WriteLine("  removed " + m.DisplayLabel); }
        AddSkyObjects(window, [("enemy", 0x07A, "darkpassage", 2850, 1950, 1600)], removeAll: false);
        Console.WriteLine("NEW_LEVEL_SKYUPDATE3_OK");
    }

    private static void SkyObjectsStep(MainWindow window)
    {
        Load(window, "townsquare");
        if (window.FreshSceneForTesting == null) throw new InvalidOperationException("Town Square is not blank");
        AddSkyObjects(window, SkyObjects, removeAll: true);
        Console.WriteLine("NEW_LEVEL_SKYOBJECTS_OK");
    }

    private static void AddSkyObjects(MainWindow window, (string What, int Class, string Source, float X, float Y, float Z)[] objects, bool removeAll)
    {
        var (browser, entries) = OpenBrowser(window); browser.Close(); Pump(10);
        var lib = Library(window);
        var japanese = (Spyro.Editor.Core.Universal.UniversalJapaneseSource?)Invoke(window, "ResolveUniversalJapaneseSource");
        var tryZ = typeof(MainWindow).GetMethod("TryFindTerrainZAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        float? Ground(float x, float y, float refZ)
        {
            var args = new object?[] { x, y, refZ, 0f, -1, false };
            return (bool)tryZ.Invoke(window, args)! ? (float)args[3]! : null;
        }
        if (removeAll)
            foreach (var m in Mobys(window).Where(m => m.IsAdded && !m.IsRemoved).ToList())
            { SetField(window, "_selectedMoby", m); Invoke(window, "RemoveSelectedMoby"); }
        foreach (var o in objects)
        {
            UniversalMobyCatalogEntry entry; UniversalMobyVariant variant;
            if (o.What.StartsWith("crash:"))
                (entry, variant) = entries.SelectMany(e => e.Variants.Select(v => (e, v)))
                    .First(p => Spyro.Editor.Core.Universal.UniversalSpecialCatalog.SpecialIdForClass(p.v.ClassId) == o.What[6..]);
            else
            {
                entry = entries.First(e => e.ClassId == o.Class && e.SourceLevels.Contains(o.Source));
                variant = entry.Variants.FirstOrDefault(v => v.SourceLevelKey == o.Source) ?? entry.Default;
            }
            string? eggColour = o.What.StartsWith("egg:") ? o.What[4..] : null;
            if (eggColour != null) variant = entry.Variants.First(v => Spyro.Editor.Core.Universal.UniversalSpecialCatalog.SpecialIdForClass(v.ClassId) == eggColour);
            var before = Mobys(window).ToHashSet();
            SetField(window, "_selectedMoby", null);
            var add = (Task)Invoke(window, "AddUniversalMobyAsync", lib, entry, variant, false, japanese)!;
            Wait(() => add.IsCompleted, "add " + o.What, 120);
            if (add.IsFaulted) throw add.Exception!;
            var added = Mobys(window).Where(m => !before.Contains(m)).ToList();
            if (added.Count == 0) { Console.WriteLine($"  NOT added {o.What} 0x{o.Class:X}: {Status(window)}"); continue; }
            Moby moby = added[0];
            float lift = moby.Position.Z - (Ground(moby.Position.X, moby.Position.Y, moby.Position.Z) ?? moby.Position.Z);
            float ground = Ground(o.X, o.Y, o.Z + 20) ?? o.Z;
            SetField(window, "_selectedMoby", moby);
            Invoke(window, "NudgeSelectedMoby", o.X - moby.Position.X, o.Y - moby.Position.Y, ground + Math.Max(lift, 0) - moby.Position.Z + 0.001f);
            if (eggColour != null) Invoke(window, "SetUniversalEggColour", moby, eggColour);
            if (o.What == "sign") Invoke(window, "SetUniversalSignText", moby, "WELCOME TO\nCLOUDSPIRE ISLES");
            if (o.What.StartsWith("sign:")) Invoke(window, "SetUniversalSignText", moby, o.What[5..].Replace("\\n", "\n"));
            Console.WriteLine($"  {moby.DisplayLabel,-34} ({moby.Position.X:0},{moby.Position.Y:0},{moby.Position.Z:0}) ground {ground:0} gem {moby.TreasureValue} +{added.Count - 1} linked");
        }
        int treasure = Mobys(window).Where(m => m.IsAdded && !m.IsRemoved).Sum(m => m.TreasureValue);
        Console.WriteLine($"objects {Mobys(window).Count(m => m.IsAdded && !m.IsRemoved)}, editor-known treasure {treasure}");
        Save(window);
    }
}
