using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

internal static class NoCallSources
{
    internal static int Run(UniversalSourceLibrary library, LevelCatalog catalog, string reportPath)
    {
        int checks = 0;
        var reports = new List<object>();
        void Check(bool value, string label) { checks++; if (!value) throw new InvalidOperationException(label); }
        foreach (var level in catalog.Levels.Where(l => l.SourceWadEntry > 0 && !UniversalNativePlayerDamageCalls.CertifiedSources.ContainsKey(l.Key)))
        {
            var source = library.Get(level.Key);
            var classes = source.Data.Registrations().Select(r => r.ClassId).Distinct().ToArray();
            var part = UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source.Overlay, classes, "no_sphere_" + level.Key).Part;
            var proof = UniversalNativePlayerDamageCalls.AnalyzeSource(level.Key, source.Overlay, part, null);
            Check(proof.Calls.Count == 0 && proof.DamageMask == 0, "Unchanged actual no-call source does not require executable or damage dependencies");
            Check(proof.AnimationIds(library.Disc.Exe).Count == 0, "No-call sources add no native sphere reaction imports");
            Check(!part.Items.Any(i => i.Word.HasValue && UniversalMips.IsJal(i.Word.Value) && UniversalMips.BranchTarget(i.Word.Value, i.SourcePc ?? i.Orig ?? UniversalOverlay.Base) == UniversalNativePlayerDamageCalls.Helper), "Independent copied instruction scan finds no sphere helper calls");
            reports.Add(new { source = level.Key, classes, proof.DamageMask, proof.Calls });
            Console.WriteLine("NO_NATIVE_DAMAGE_CALL_OK source=" + level.Key);
        }
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new { checks, sources = reports.Count, reports }, new JsonSerializerOptions { WriteIndented = true }));
        return checks;
    }
}
