using System.Text.Json;
using Spyro.Editor.Core.Universal;

internal static class SourceFamilies
{
    internal static int Run(UniversalSourceLibrary library, string reportPath)
    {
        int checks = 0; var reports = new List<object>();
        void Check(bool value, string label) { checks++; if (!value) throw new InvalidOperationException(label); }
        void Refuse(Action action, string label) { bool denied = false; try { action(); } catch (InvalidDataException) { denied = true; } Check(denied, label); }
        foreach (var family in UniversalNativePlayerDamageCalls.CertifiedSources)
        {
            string key = family.Key; var source = library.Get(key).Overlay;
            var classes = library.Get(key).Data.Registrations().Select(r => r.ClassId).Distinct().ToArray();
            var part = UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source, classes, "damage_" + key).Part;
            UniversalAsmPart Copy() { var copy = new UniversalAsmPart(); copy.Items.AddRange(part.Items); return copy; }
            var proof = UniversalNativePlayerDamageCalls.AnalyzeSource(key, source, part, library.Disc.Exe);
            Check(proof.Calls.Count == family.Value.Sites.Count, "All actual source family calls enrolled " + key);
            Check(proof.DamageMask == family.Value.Sites.Aggregate(0u, (mask, site) => mask | site.Mask), "Exact independent mask union " + key);
            Check(UniversalNativePlayerDamageCalls.AnalyzeSource(key, source, new(), null).Calls.Count == 0, "No actual source call adds no dependency");
            foreach (var site in family.Value.Sites)
            {
                var scalar = new UniversalAsmPart();
                for (int n = 0; n < site.Words.Count; n++) scalar.Emit(site.Words[n], orig: site.Start + (uint)n * 4);
                Check(UniversalNativePlayerDamageCalls.AnalyzeSource(key, source, scalar, library.Disc.Exe).DamageMask == site.Mask, "Individual surviving call retains only its exact mask");
                for (int n = 0; n < site.Words.Count; n++)
                {
                    uint pc = site.Start + (uint)n * 4; int index = part.Items.FindIndex(i => (i.SourcePc ?? i.Orig) == pc);
                    var altered = Copy(); altered.Items[index] = altered.Items[index] with { Word = altered.Items[index].Word ^ 1 };
                    Refuse(() => UniversalNativePlayerDamageCalls.AnalyzeSource(key, source, altered, library.Disc.Exe), "Every source family copied argument/delay word authenticated");
                    var bytes = source.Bytes.ToArray(); bytes[(int)(pc - UniversalOverlay.Base)] ^= 1;
                    Refuse(() => UniversalNativePlayerDamageCalls.AnalyzeSource(key, new(bytes, source.Bindings), part, library.Disc.Exe), "Every source family complete overlay authenticated");
                    var missing = Copy(); missing.Items.RemoveAt(index);
                    if (pc == site.Call) Check(UniversalNativePlayerDamageCalls.AnalyzeSource(key, source, missing, library.Disc.Exe).Calls.Count == proof.Calls.Count - 1, "Absent call not enrolled");
                    else Refuse(() => UniversalNativePlayerDamageCalls.AnalyzeSource(key, source, missing, library.Disc.Exe), "Missing source family argument/delay refuses");
                    var alias = Copy(); alias.Items[index] = alias.Items[index] with { SourcePc = pc + 0x1000 };
                    Refuse(() => UniversalNativePlayerDamageCalls.AnalyzeSource(key, source, alias, library.Disc.Exe), "Forged source argument/call identity refuses");
                    var relocation = Copy(); relocation.Items[index] = relocation.Items[index] with { Fix = new(UniversalFixKind.AddrLo, 0x8007FFFF) };
                    Refuse(() => UniversalNativePlayerDamageCalls.AnalyzeSource(key, source, relocation, library.Disc.Exe), "Changed copied source packet relocation refuses");
                    if (n == 0) continue;
                    foreach (var kind in new[] { UniversalFixKind.Branch, UniversalFixKind.Jump })
                    {
                        var incoming = Copy(); incoming.Emit(UniversalMips.EncodeJ(2, pc), new(kind, pc));
                        Refuse(() => UniversalNativePlayerDamageCalls.AnalyzeSource(key, source, incoming, library.Disc.Exe), "New source family packet incoming edge refuses");
                    }
                    var raw = Copy(); raw.Emit(UniversalMips.EncodeJ(2, pc));
                    Refuse(() => UniversalNativePlayerDamageCalls.AnalyzeSource(key, source, raw, library.Disc.Exe), "Raw source family packet incoming jump refuses");
                    var table = Copy(); table.Tables["mutant_table"] = [pc];
                    Refuse(() => UniversalNativePlayerDamageCalls.AnalyzeSource(key, source, table, library.Disc.Exe), "Source family copied jump table incoming refuses");
                    var label = Copy(); label.Items.Insert(index, new(null, default, null, "mutant_label")); label.Emit(UniversalMips.EncodeJ(2, 0), new(UniversalFixKind.JumpLabel, Label: "mutant_label"));
                    Refuse(() => UniversalNativePlayerDamageCalls.AnalyzeSource(key, source, label, library.Disc.Exe), "Actual source family copied label incoming refuses");
                }
                var partial = Copy(); partial.Items.AddRange(scalar.Items.Take(scalar.Items.Count - 1));
                Refuse(() => UniversalNativePlayerDamageCalls.AnalyzeSource(key, source, partial, library.Disc.Exe), "Complete source packet elsewhere cannot bless partial copied packet");
                var duplicate = Copy(); duplicate.Items.AddRange(scalar.Items);
                Check(UniversalNativePlayerDamageCalls.AnalyzeSource(key, source, duplicate, library.Disc.Exe).Calls.Count == proof.Calls.Count, "Intact source packet duplicate preserves union");
            }
            foreach (uint global in new[] { UniversalExeBindings.UpdateGlobal, UniversalExeBindings.InitGlobal, UniversalExeBindings.CopyBufferGlobal })
            {
                var bindings = source.Bindings.ToDictionary(); bindings[global] = bindings[global] with { Value = bindings[global].Value + 4 };
                Refuse(() => UniversalNativePlayerDamageCalls.AnalyzeSource(key, new(source.Bytes.ToArray(), bindings), part, library.Disc.Exe), "All source callback owners authenticated");
            }
            Refuse(() => UniversalNativePlayerDamageCalls.AnalyzeSource("unsupported-family", source, part, library.Disc.Exe), "Unsupported source identity does not borrow packet proof");
            var unknown = Copy(); unknown.Emit(UniversalMips.EncodeJ(3, UniversalNativePlayerDamageCalls.Helper), orig: source.Init);
            Refuse(() => UniversalNativePlayerDamageCalls.AnalyzeSource(key, source, unknown, library.Disc.Exe), "Unknown source call cannot borrow another proof");
            reports.Add(new { source = key, proof.DamageMask, proof.Calls, animationIds = proof.AnimationIds(library.Disc.Exe) });
            Console.WriteLine($"FAMILY_NATIVE_DAMAGE_OK source={key} calls={proof.Calls.Count} animations={string.Join(',', proof.AnimationIds(library.Disc.Exe))}");
        }
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new { checks, families = reports.Count, calls = UniversalNativePlayerDamageCalls.CertifiedSources.Values.Sum(owner => owner.Sites.Count), reports }, new JsonSerializerOptions { WriteIndented = true }));
        return checks;
    }
}
