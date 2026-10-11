using System.Numerics;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

// Draw-buffer load: the terrain model (NativeDrawLoad) and the runtime guard (NativeDrawBufferGuard).
//   (no arguments)                       disc-free checks
//   --source <USA bin>                   also installs the guard into the retail executable (read only)
//   --model <env.bin> <spotload.json>    lab: the model at the emulator's own cameras against the bytes it measured
//   --worst <env.bin> <spots.json>       lab: the heaviest view from Spyro's spots (editor units)
int checks = 0, failed = 0;
bool verbose = Environment.GetEnvironmentVariable("VERBOSE") == "1";
void Check(bool ok, string what) { checks++; if (!ok) { failed++; Console.WriteLine("FAIL: " + what); } else if (verbose) Console.WriteLine("ok   " + what); }

if (args.Length >= 3 && args[0] == "--model")
{
    var sectors = NativeDrawLoad.Parse(File.ReadAllBytes(args[1]));
    using var doc = JsonDocument.Parse(File.ReadAllText(args[2]));
    double sumModel = 0, sumMeasured = 0; int n = 0; double worstRatio = 0, bestRatio = 9;
    var rows = new List<(int MLp, int MHp, int MSub, int Lp, int Hp, int Sub, int Nq, int Cq, int MNq, int MCq)>();
    foreach (var spot in doc.RootElement.GetProperty("spots").EnumerateArray())
        foreach (var row in spot.GetProperty("rows").EnumerateArray())
        {
            var cam = row.GetProperty("cam");
            short[] m = [.. cam.GetProperty("m1").EnumerateArray().Select(e => (short)e.GetInt32())];
            int[] pos = [.. cam.GetProperty("pos").EnumerateArray().Select(e => e.GetInt32())];
            var pose = NativeDrawLoad.Pose.FromGame(m, pos[0], pos[1], pos[2]);
            var load = NativeDrawLoad.Frame(sectors, pose, float.TryParse(Environment.GetEnvironmentVariable("NEAR"), out float nearDistance) ? nearDistance : NativeDrawLoad.NearDistance);
            long t0 = row.GetProperty("t0").GetInt64(), lp = row.GetProperty("lp").GetInt64(), hp = row.GetProperty("hp").GetInt64(), t1 = row.GetProperty("t1").GetInt64();
            int nq = row.TryGetProperty("nq", out var q) ? q.GetInt32() : -1, cq = row.TryGetProperty("cq", out var c) ? c.GetInt32() : -1;
            rows.Add((load.Far, load.Near, load.Subdivided, (int)(lp - t0), (int)(hp - lp), (int)(t1 - hp), nq, cq, load.NearQueue, load.CloseQueue));
        }
    foreach (var r in rows)
    {
        int model = r.MLp + r.MHp + r.MSub, measured = r.Lp + r.Hp + r.Sub;
        sumModel += model; sumMeasured += measured; n++;
        if (measured > 2000) { double ratio = (double)model / measured; worstRatio = Math.Max(worstRatio, ratio); bestRatio = Math.Min(bestRatio, ratio); }
    }
    Console.WriteLine($"frames {n}; model/measured total {sumModel / Math.Max(1, sumMeasured):0.000}; per-frame ratio {bestRatio:0.00}..{worstRatio:0.00}");
    Console.WriteLine($"far  model/measured {rows.Sum(r => (double)r.MLp) / Math.Max(1, rows.Sum(r => r.Lp)):0.000}; near {rows.Sum(r => (double)r.MHp) / Math.Max(1, rows.Sum(r => r.Hp)):0.000}; sub {rows.Sum(r => (double)r.MSub) / Math.Max(1, rows.Sum(r => r.Sub)):0.000}");
    Console.WriteLine($"max measured terrain {rows.Max(r => r.Lp + r.Hp + r.Sub)}, max model {rows.Max(r => r.MLp + r.MHp + r.MSub)}");
    var order = Environment.GetEnvironmentVariable("BYNEAR") == "1" ? rows.OrderByDescending(r => r.Hp - r.MHp) :
        Environment.GetEnvironmentVariable("BYNEARMAX") == "1" ? rows.OrderByDescending(r => r.Hp) : Environment.GetEnvironmentVariable("BYRATIO") == "1"
        ? rows.Where(r => r.Lp + r.Hp + r.Sub > 2000).OrderByDescending(r => (double)(r.MLp + r.MHp + r.MSub) / (r.Lp + r.Hp + r.Sub))
        : rows.OrderByDescending(r => r.Lp + r.Hp + r.Sub);
    foreach (var r in order.Take(int.TryParse(Environment.GetEnvironmentVariable("SHOW"), out int k) ? k : 8))
        Console.WriteLine($"  measured far {r.Lp} near {r.Hp} sub {r.Sub} (queues {r.Nq}/{r.Cq})  model far {r.MLp} near {r.MHp} sub {r.MSub} (queues {r.MNq}/{r.MCq})");
    return 0;
}
if (args.Length >= 2 && args[0] == "--scene")
{
    // lab: a project's scene compiled as Create BIN compiles its terrain, with the draw plan
    var doc = Spyro.Editor.Core.Editing.SceneDocument.Read(args[1]);
    var materials = doc.Surfaces.Where(s => s.Material != null).Select(s => s.Material!).Distinct().ToArray();
    var sw = System.Diagnostics.Stopwatch.StartNew();
    NativeAuthoredMeshRenderer.DrawPlanDisabled = Environment.GetEnvironmentVariable("NOPLAN") == "1";
    var result = NativeAuthoredMeshRenderer.Compile(doc, materials, []);
    var plan = NativeAuthoredMeshRenderer.LastDrawPlan;
    Console.WriteLine($"{doc.Name}: {doc.Surfaces.Count} surfaces compiled in {sw.ElapsedMilliseconds} ms; far meshes {NativeAuthoredMeshRenderer.LastFarReport}");
    if (plan is NativeAuthoredMeshRenderer.DrawPlan dp) Console.WriteLine("  " + dp.Describe());
    if (args.Length > 2) File.WriteAllBytes(args[2], result.Environment);
    return 0;
}
if (args.Length >= 4 && args[0] == "--patch-image")
{
    // lab: a copy of an exported image with the guard installed (level:near[,level:near...], optional reserve bytes)
    var image = Spyro.Editor.Core.Universal.UniversalDiscImage.Read(args[1]);
    var exe = image.Exe.ToArray();
    var levels = args[3].Split(',').Select(p => p.Split(':')).ToDictionary(p => int.Parse(p[0]), p => int.Parse(p[1]));
    int? reserve = args.Length > 4 ? int.Parse(args[4]) : null;
    NativeDrawBufferGuard.PatchExe(exe, levels, reserve);
    image.WriteImage(args[2], image.Wad, exe);
    File.WriteAllLines(Path.ChangeExtension(args[2], ".cue"), ["FILE \"" + Path.GetFileName(args[2]) + "\" BINARY", "  TRACK 01 MODE2/2352", "    INDEX 01 00:00:00"]);
    Console.WriteLine($"guard installed for {string.Join(", ", NativeDrawBufferGuard.CoveredLevels(exe).Select(k => $"{k.Key}:{k.Value}"))} -> {args[2]}");
    return 0;
}
if (args.Length >= 3 && args[0] == "--worst")
{
    var sectors = NativeDrawLoad.Parse(File.ReadAllBytes(args[1]));
    using var doc = JsonDocument.Parse(File.ReadAllText(args[2]));
    List<Vector3> spots = [.. doc.RootElement.EnumerateArray().Select(e => { var a = e.EnumerateArray().Select(x => (float)x.GetDouble()).ToArray(); return new Vector3(a[0], a[1], a[2]); })];
    float near = float.TryParse(Environment.GetEnvironmentVariable("NEAR"), out float nd) ? nd : NativeDrawLoad.NearDistance;
    var sw = System.Diagnostics.Stopwatch.StartNew();
    var cameras = NativeDrawLoad.Cameras(spots).ToList();
    var views = NativeDrawLoad.Survey(sectors, cameras, near);
    Console.WriteLine($"spots {spots.Count} poses {views.Length} in {sw.ElapsedMilliseconds} ms (near {near}):");
    foreach (var w in views.Take(3)) Console.WriteLine($"  {w.Load} total {w.Load.Total} at spot {w.Spot} eye {w.Pose.Eye} forward {w.Pose.Forward}");
    Console.WriteLine($"  max far {views.Max(v => v.Load.Far)} max near {views.Max(v => v.Load.Near)} max sub {views.Max(v => v.Load.Subdivided)}");
    return 0;
}

DrawLoadChecks.Model(Check);
DrawLoadChecks.Plan(Check);
DrawLoadChecks.Guard(Check);
bool source = args.Length >= 2 && args[0] == "--source";
if (source) DrawLoadChecks.Source(Check, args[1]);
string mode = source ? "SOURCE" : "FREE";
Console.WriteLine(failed == 0 ? $"DRAW_LOAD_{mode}_OK checks={checks}" : $"DRAW_LOAD_{mode}_FAILED {failed} of {checks}");
return failed == 0 ? 0 : 1;
