using System.Reflection;
using System.Text.Json;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

/// <summary>Lost Fleet's holes (research 2026-10-10). LOSTFLEET_FACES is the lab's faces.json (Year of the Dragon's near
/// faces in editor units with their flags), the disc is YOTD_DISC or the workspace's configured Year of the Dragon disc.
/// <list type="bullet">
/// <item>Sides: a face with Year of the Dragon's flip bit (0x04) shows its other side (<see cref="AuthoredSurface.FrontReversed"/>);
/// one with its double bit (0x08, only the acid) is two-sided. Faces not from Lost Fleet keep theirs.</item>
/// <item>Faces the lab dropped because two corners coincide but three distinct corners remain are added (texture,
/// ground kind and sector as the level's other faces of that texture).</item>
/// <item>Every sector gets Year of the Dragon's own distant mesh (<see cref="SceneDocument.FarMeshes"/>) in place of the
/// compiler's simplified copy.</item>
/// </list></summary>
internal static partial class NewLevelBuildCases
{
    private sealed record LostFleetFace(int[][] v, bool tri, int tex, bool flip, bool @double, int[][] c, int[][] fc, int region);

    private static LostFleetFace[] LostFleetFaces()
    {
        string path = Environment.GetEnvironmentVariable("LOSTFLEET_FACES") ?? throw new InvalidOperationException("Set LOSTFLEET_FACES to the Lost Fleet lab's faces.json.");
        return JsonSerializer.Deserialize<LostFleetFace[]>(File.ReadAllText(path))!;
    }

    /// <summary>A face's corners without repeats (a triangle's first corner and any corner equal to the previous one),
    /// in order, with their indices in the record.</summary>
    private static int[] LostFleetCorners(LostFleetFace f)
    {
        List<int> keep = [];
        for (int k = f.tri ? 1 : 0; k < f.v.Length; k++)
            if (keep.Count == 0 || !f.v[keep[^1]].SequenceEqual(f.v[k])) keep.Add(k);
        if (keep.Count > 1 && f.v[keep[0]].SequenceEqual(f.v[keep[^1]])) keep.RemoveAt(keep.Count - 1);
        return keep.Distinct(new CornerComparer(f)).Count() == keep.Count ? [.. keep] : [];
    }
    private sealed class CornerComparer(LostFleetFace f) : IEqualityComparer<int>
    {
        public bool Equals(int a, int b) => f.v[a].SequenceEqual(f.v[b]);
        public int GetHashCode(int a) => HashCode.Combine(f.v[a][0], f.v[a][1], f.v[a][2]);
    }
    private static string LostFleetKey(IEnumerable<(int, int, int)> points) => string.Join(";", points.Order());

    /// <summary>Sets <see cref="AuthoredSurface.TwoSided"/> and <see cref="AuthoredSurface.FrontReversed"/> from Year of the
    /// Dragon's flags on every face that came from Lost Fleet (matched by its corners); returns the faces changed.</summary>
    private static (AuthoredSurface[] Surfaces, int Reversed, int TwoSided, int Changed) LostFleetSides(IReadOnlyList<AuthoredSurface> surfaces, LostFleetFace[] faces)
    {
        Dictionary<string, LostFleetFace> byKey = [];
        foreach (var f in faces)
        {
            var corners = LostFleetCorners(f);
            if (corners.Length >= 3) byKey.TryAdd(LostFleetKey(corners.Select(k => (f.v[k][0], f.v[k][1], f.v[k][2]))), f);
        }
        int reversed = 0, twoSided = 0, changed = 0;
        var result = surfaces.Select(s =>
        {
            if (!byKey.TryGetValue(LostFleetKey(s.Vertices.Select(p => ((int)p.X, (int)p.Y, (int)p.Z))), out var f)) return s;
            if (f.flip) reversed++;
            if (f.@double) twoSided++;
            if (s.FrontReversed == f.flip && s.TwoSided == f.@double) return s;
            changed++;
            return s with { FrontReversed = f.flip, TwoSided = f.@double };
        }).ToArray();
        return (result, reversed, twoSided, changed);
    }

    private static void LostFleetHoles(MainWindow window)
    {
        var faces = LostFleetFaces();
        string disc = Environment.GetEnvironmentVariable("YOTD_DISC") is { Length: > 0 } configured ? configured
            : Spyro.Editor.Core.Music.YearOfTheDragonDiscLocator.Find(Workspace(window));
        if (disc.Length == 0) throw new InvalidOperationException("Set YOTD_DISC (or run the yotd-disc step first).");
        var farMeshes = Spyro.Editor.Core.YearOfTheDragon.YearOfTheDragonTerrain.LostFleetFarMeshes(new Spyro.Editor.Core.Music.YearOfTheDragonDisc(disc));

        Load(window, "artisans");
        var doc = window.FreshSceneForTesting ?? throw new InvalidOperationException("Artisans is not a built level");

        // 1. Faces the lab dropped (two corners at one place) that still have three corners.
        Dictionary<string, AuthoredSurface> sceneByKey = [];
        foreach (var s in doc.Surfaces) sceneByKey.TryAdd(LostFleetKey(s.Vertices.Select(p => ((int)p.X, (int)p.Y, (int)p.Z))), s);
        var present = sceneByKey.Keys.ToHashSet();
        Dictionary<int, AuthoredSurface> sample = [];
        foreach (var f in faces)
        {
            var corners = LostFleetCorners(f);
            if (corners.Length < 3) continue;
            if (sceneByKey.TryGetValue(LostFleetKey(corners.Select(k => (f.v[k][0], f.v[k][1], f.v[k][2]))), out var match) && match.Sector == f.region)
                sample.TryAdd(f.tex, match);
        }
        List<AuthoredSurface> added = [];
        foreach (var f in faces)
        {
            var corners = LostFleetCorners(f);
            if (corners.Length < 3 || !sample.TryGetValue(f.tex, out var like)) continue;
            var points = corners.Select(k => new Vector3f(f.v[k][0], f.v[k][1], f.v[k][2])).ToArray();
            if (!present.Add(LostFleetKey(corners.Select(k => (f.v[k][0], f.v[k][1], f.v[k][2]))))) continue;
            var a = points[0]; var b = points[1]; var c = points[2];
            float nx = (b.Y - a.Y) * (c.Z - a.Z) - (b.Z - a.Z) * (c.Y - a.Y), ny = (b.Z - a.Z) * (c.X - a.X) - (b.X - a.X) * (c.Z - a.Z), nz = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
            if (nx == 0 && ny == 0 && nz == 0) continue;
            ColorRgba Colour(int[] x) => new((byte)x[0], (byte)x[1], (byte)x[2]);
            var near = corners.Select(k => Colour(f.c[k])).ToArray();
            added.Add(new AuthoredSurface(Guid.NewGuid().ToString("N"), points, near[0], like.Material, near, [.. corners.Select(k => Colour(f.fc[k]))])
                { Kind = like.Kind, Sector = f.region });
            Console.WriteLine($"  added face of sector {f.region} (texture {f.tex}): {string.Join(" ", points.Select(p => $"({p.X:0},{p.Y:0},{p.Z:0})"))}");
        }

        // 2. Sides.
        var (surfaces, reversed, twoSided, changed) = LostFleetSides([.. doc.Surfaces, .. added], faces);
        Console.WriteLine($"lost fleet faces: {reversed} shown from their other side, {twoSided} two-sided (acid); {changed} faces changed");

        // 3. Distant meshes for the sectors the scene has.
        var sectors = surfaces.Where(s => s.Sector != null).Select(s => s.Sector!.Value).ToHashSet();
        var far = farMeshes.Where(m => sectors.Contains(m.Sector)).ToArray();
        Console.WriteLine($"distant meshes: {far.Length} of {farMeshes.Count} Year of the Dragon sectors ({far.Sum(m => m.Faces.Count)} faces, " +
            $"{far.Count(m => m.Vertices.Length > 64)} over 64 corners, {far.Count(m => m.Colors.Length > 64)} over 64 colours)");

        var updated = doc with { Surfaces = surfaces, FarMeshes = far };
        updated.Validate();
        ReportLostFleetCompile(updated);
        ApplyScene(window, updated);
        var saved = SceneDocument.Read(SceneDocument.PathForTarget(Workspace(window).RootPath, "artisans"));
        int savedReversed = saved.Surfaces.Count(s => s.FrontReversed), savedTwoSided = saved.Surfaces.Count(s => s.TwoSided);
        if (savedReversed != surfaces.Count(s => s.FrontReversed) || savedTwoSided != surfaces.Count(s => s.TwoSided) || saved.FarMeshes?.Count != far.Length)
            throw new InvalidOperationException($"The saved scene lost face sides or distant meshes ({savedReversed} reversed, {savedTwoSided} two-sided, {saved.FarMeshes?.Count ?? 0} distant meshes).");
        Console.WriteLine($"saved: {saved.Surfaces.Count} faces ({added.Count} added), {savedReversed} reversed, {savedTwoSided} two-sided, {saved.FarMeshes!.Count} distant meshes");
        Console.WriteLine("NEW_LEVEL_LOSTFLEET_HOLES_OK");
    }

    /// <summary>Compiles the terrain as Create BIN will (without the texture pictures) and reports the sectors and far meshes.</summary>
    private static void ReportLostFleetCompile(SceneDocument doc)
    {
        var core = typeof(SceneDocument).Assembly;
        var mesh = core.GetType("Spyro.Editor.Core.Exporting.NativeAuthoredMesh", true)!;
        var renderer = core.GetType("Spyro.Editor.Core.Exporting.NativeAuthoredMeshRenderer", true)!;
        var materials = doc.Surfaces.Where(s => s.Material != null).Select(s => s.Material!).Distinct().ToArray();
        var compile = mesh.GetMethod("Compile", BindingFlags.Static | BindingFlags.NonPublic, [typeof(SceneDocument), typeof(IReadOnlyList<AuthoredMaterial>)])!;
        byte[] bundle = (byte[])compile.Invoke(null, [doc, materials])!;
        object Report(string name) => renderer.GetProperty(name, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var sectors = ((int Sectors, int QueueLoad, string Where))Report("LastReport");
        var far = ((int Supplied, int Simplified))Report("LastFarReport");
        Console.WriteLine($"compiled: {bundle.Length} bytes, {sectors.Sectors} sectors; far meshes: {far.Supplied} Year of the Dragon, {far.Simplified} simplified");
    }
}
