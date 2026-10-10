using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

/// <summary>Cloudspire Isles: the cloud stairs and stepping clouds were single flat tiles floating in the air. Each
/// becomes a solid block: every outside edge of a step hangs a cloud side down to the block's base (or, toward a lower
/// step, a riser down to that step), and the base is closed. The Sky Tower's open bottom is closed too. Works on the
/// level's current faces (after retiling), so sides follow the split tile edges.</summary>
internal static partial class NewLevelBuildCases
{
    private static readonly (string Name, float X0, float Y0, float X1, float Y1, float Base)[] CloudspireBlocks =
    [
        ("stair to the Sky Tower", 4290, 3360, 4530, 3960, 1500),
        ("stair from the garden", 3120, 3360, 3360, 3760, 1500),
        ("stair to the Egg Islet", 3360, 4240, 3600, 4740, 1450),
        ("stepping cloud 1", 3430, 2880, 3670, 3120, 1530),
        ("stepping cloud 2", 3740, 2880, 3980, 3120, 1530),
        ("stepping cloud 3", 2880, 2330, 3120, 2570, 1530),
    ];

    private static void Solidify(MainWindow window)
    {
        Load(window, "townsquare");
        var doc = window.FreshSceneForTesting ?? throw new InvalidOperationException("townsquare is not a built level");
        List<AuthoredSurface> added = [];
        ColorRgba lit = new(150, 150, 150), under = new(118, 122, 150);
        static bool Same(Vector3f a, Vector3f b) => Math.Abs(a.X - b.X) < 0.5f && Math.Abs(a.Y - b.Y) < 0.5f;
        static float Area(Vector3f[] v) { float s = 0; for (int i = 0; i < v.Length; i++) { var a = v[i]; var b = v[(i + 1) % v.Length]; s += a.X * b.Y - b.X * a.Y; } return s / 2; }
        foreach (var block in CloudspireBlocks)
        {
            bool Inside(Vector3f p) => p.X >= block.X0 - 0.5f && p.X <= block.X1 + 0.5f && p.Y >= block.Y0 - 0.5f && p.Y <= block.Y1 + 0.5f;
            var tops = doc.Surfaces.Where(s => s.Vertices.Length >= 3 && s.Vertices.All(Inside) && s.Vertices.All(v => Math.Abs(v.Z - s.Vertices[0].Z) < 0.5f)).ToList();
            if (tops.Count == 0) throw new InvalidOperationException($"{block.Name}: no tiles");
            if (tops.Any(s => s.Vertices[0].Z <= block.Base)) throw new InvalidOperationException($"{block.Name}: a tile is below the base");
            var material = tops.Select(s => s.Material).FirstOrDefault(m => m != null);
            // Edges of each tile, counter-clockwise seen from above (tile on the left).
            var edges = tops.SelectMany(s =>
            {
                var v = Area(s.Vertices) >= 0 ? s.Vertices : s.Vertices.Reverse().ToArray();
                return Enumerable.Range(0, v.Length).Select(i => (A: v[i], B: v[(i + 1) % v.Length], Z: v[0].Z));
            }).ToList();
            int sides = 0, risers = 0;
            foreach (var e in edges)
            {
                // Inner edge between two tiles of one step: nothing to hang.
                if (edges.Any(o => o.Z == e.Z && Same(o.A, e.B) && Same(o.B, e.A))) continue;
                var next = edges.Where(o => o.Z != e.Z && (Same(o.A, e.B) && Same(o.B, e.A) || Same(o.A, e.A) && Same(o.B, e.B))).ToList();
                if (next.Any(o => o.Z > e.Z)) continue;                       // the higher step hangs the riser
                float bottom = next.Count > 0 ? next.Max(o => o.Z) : block.Base;
                if (next.Count > 0) risers++; else sides++;
                Vector3f bt = new(e.B.X, e.B.Y, e.Z), at = new(e.A.X, e.A.Y, e.Z), ab = new(e.A.X, e.A.Y, bottom), bb = new(e.B.X, e.B.Y, bottom);
                ColorRgba low = next.Count > 0 ? lit : under;
                added.Add(new(Guid.NewGuid().ToString("N"), [bt, at, ab, bb], lit, material, [lit, lit, low, low], [lit, lit, low, low]));
            }
            // The base: each tile again at the base height, facing down.
            foreach (var s in tops)
            {
                var v = (Area(s.Vertices) >= 0 ? s.Vertices.Reverse() : s.Vertices).Select(p => new Vector3f(p.X, p.Y, block.Base)).ToArray();
                added.Add(new(Guid.NewGuid().ToString("N"), v, under, material, [.. v.Select(_ => under)], [.. v.Select(_ => under)]));
            }
            Console.WriteLine($"{block.Name}: {tops.Count} tiles, {sides} sides, {risers} risers, base {block.Base}");
        }
        // The Sky Tower: brick walls from 1600 to its top at 1840, open underneath.
        var tower = doc.Surfaces.Where(s => s.Vertices.All(p => p.X >= 4289.5f && p.X <= 4530.5f && p.Y >= 3959.5f && p.Y <= 4200.5f && Math.Abs(p.Z - 1840) < 0.5f)).ToList();
        if (tower.Count == 0) throw new InvalidOperationException("Sky Tower top not found");
        foreach (var s in tower)
        {
            var v = (Area(s.Vertices) >= 0 ? s.Vertices.Reverse() : s.Vertices).Select(p => new Vector3f(p.X, p.Y, 1600)).ToArray();
            added.Add(new(Guid.NewGuid().ToString("N"), v, under, s.Material, [.. v.Select(_ => under)], [.. v.Select(_ => under)]));
        }
        if (doc.Surfaces.Count + added.Count > SceneDocument.MaxSurfaces) throw new InvalidOperationException("too many faces");
        Console.WriteLine($"faces {doc.Surfaces.Count} + {added.Count}");
        ApplyScene(window, doc with { Surfaces = [.. doc.Surfaces, .. added] });
        Console.WriteLine("NEW_LEVEL_SOLIDIFY_OK");
    }
}
