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

    public static async Task<int> Run(string repo, string? maps, int seedsPerStyle, string? only = null)
    {
        var catalog = LevelCatalog.Load(repo);
        int checks = 0, failures = 0;
        void Check(bool ok, string what) { checks++; if (!ok) { failures++; Console.WriteLine("FAIL " + what); } }
        var fingerprints = new HashSet<string>();
        var styles = Enum.GetValues<LevelStyle>();
        var sizes = Enum.GetValues<LevelSize>();
        var themesSeen = new HashSet<string>();
        if (maps != null) Directory.CreateDirectory(maps);
        foreach (var style in styles.Where(x => only == null || x.ToString() == only))
            for (int k = 0; k < seedsPerStyle; k++)
            {
                long seed = 1000 + (int)style * 100 + k;
                var options = new RandomLevelOptions { Seed = seed, Style = style, Size = sizes[k % sizes.Length],
                    ChargePaths = Environment.GetEnvironmentVariable("RANDOM_CHARGE") == "1" ? true : null };
                var watch = Stopwatch.StartNew();
                RandomLevelResult r;
                try { r = RandomLevelGenerator.Generate(options, catalog); }
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
                    var again = RandomLevelGenerator.Generate(options, catalog);
                    string other = OtherProcessFingerprint(seed, style, options.Size, repo);
                    Check(again.Scene.Fingerprint() == r.Scene.Fingerprint() && other == r.Scene.Fingerprint(), $"{style} {seed} not the same level every time ({other})");
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
