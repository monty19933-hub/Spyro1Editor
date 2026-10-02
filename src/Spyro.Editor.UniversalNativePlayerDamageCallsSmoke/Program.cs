using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

if (args.Length is not (3 or 4)) throw new ArgumentException("Supply original USA BIN, catalog workspace, and report output.");
int checks = 0;
void Check(bool success, string label) { checks++; if (!success) throw new InvalidOperationException(label); }
void Refused(Action action, string label) { bool refused = false; try { action(); } catch (InvalidDataException) { refused = true; } Check(refused, label); }
UniversalSourceLibrary.RequireOriginal(args[0]);
var lib = new UniversalSourceLibrary(args[0], LevelCatalog.Load(args[1])); var s = lib.Get("gnorccove"); var exe = lib.Disc.Exe;
var part = UniversalBehaviorBundleBuilder.BuildPrunedUpdate(s.Overlay, [0x9A, 0xCA, 0x97, 0x98, 0x99, 0x190], "damage_smoke").Part;
var result = UniversalNativePlayerDamageCalls.AnalyzeGnorc(s.Overlay, part, exe);
Check(result.Calls.Count == 2 && result.DamageMask == 0xC0096, "Both actual surviving native sphere calls enrolled");
Check(result.Calls[0] == new UniversalNativePlayerDamageCall(0x80081550, 0x80081540, 0xC0086, 0x8004E3B4), "Explosion's changing radius is separate from exact constant damage argument");
Check(result.Calls[1].DamageMask == 0x10 && result.Calls[1].ArgumentPc == 0x80082250, "JAL delay slot damage argument enrolled");
Check(result.AnimationIds(exe).Contains(29), "Native state map requests missing observed reaction29");
Console.WriteLine("ANIMATIONS " + string.Join(',', result.AnimationIds(exe)));
UniversalAsmPart Clone() { var copy = new UniversalAsmPart(); copy.Items.AddRange(part.Items); return copy; }
foreach (uint start in new uint[] { 0x80081538, 0x80082248 })
{
    uint call = start == 0x80081538 ? 0x80081550u : 0x8008224Cu;
    for (uint at = start; at <= call + 4; at += 4)
    {
        uint pc = at; int index = part.Items.FindIndex(i => (i.SourcePc ?? i.Orig) == pc);
        var changed = Clone(); changed.Items[index] = changed.Items[index] with { Word = changed.Items[index].Word ^ 1 };
        Refused(() => UniversalNativePlayerDamageCalls.AnalyzeGnorc(s.Overlay, changed, exe), "Changed copied argument/delay word refuses");
        var absent = Clone(); absent.Items.RemoveAt(index);
        if (pc == call) Check(UniversalNativePlayerDamageCalls.AnalyzeGnorc(s.Overlay, absent, exe).Calls.Count == 1, "Absent native call adds no dependency");
        else Refused(() => UniversalNativePlayerDamageCalls.AnalyzeGnorc(s.Overlay, absent, exe), "Pruned packet word refuses");
        var foreign = Clone(); foreign.Items[index] = foreign.Items[index] with { SourcePc = pc + 0x100 };
        Refused(() => UniversalNativePlayerDamageCalls.AnalyzeGnorc(s.Overlay, foreign, exe), $"Forged copied source identity refuses {pc:X8} index={index}");
        var fix = Clone(); fix.Items[index] = fix.Items[index] with { Fix = new(UniversalFixKind.AddrLo, 0x8007FFFF) };
        Refused(() => UniversalNativePlayerDamageCalls.AnalyzeGnorc(s.Overlay, fix, exe), "Later packet relocation mutation refuses");
        var mutatedSource = s.Overlay.Bytes.ToArray(); mutatedSource[(int)(pc - UniversalOverlay.Base)] ^= 1;
        Refused(() => UniversalNativePlayerDamageCalls.AnalyzeGnorc(new UniversalOverlay(mutatedSource, s.Overlay.Bindings), part, exe), "Changed complete original source refuses");
    }
}
for (int n = 0; n < 0xE0; n++)
{
    var altered = exe.ToArray(); altered[(int)(UniversalNativePlayerDamageCalls.Helper - UniversalExeBindings.ExeBase) + n] ^= 1;
    Refused(() => UniversalNativePlayerDamageCalls.AnalyzeGnorc(s.Overlay, part, altered), "Every helper byte is independently authenticated");
}
foreach (uint global in new uint[] { UniversalExeBindings.UpdateGlobal, UniversalExeBindings.InitGlobal, UniversalExeBindings.CopyBufferGlobal })
{
    var bindings = s.Overlay.Bindings.ToDictionary(); bindings[global] = bindings[global] with { Value = bindings[global].Value + 4 };
    Refused(() => UniversalNativePlayerDamageCalls.AnalyzeGnorc(new UniversalOverlay(s.Overlay.Bytes.ToArray(), bindings), part, exe), "Changed source callback refuses");
}
var empty = new UniversalAsmPart(); Check(UniversalNativePlayerDamageCalls.AnalyzeGnorc(s.Overlay, empty, null).DamageMask == 0, "No surviving call adds no dependency");
var removed = Clone(); removed.Items.RemoveAll(i => (i.SourcePc ?? i.Orig) is >= 0x80081538 and <= 0x80081554);
Check(UniversalNativePlayerDamageCalls.AnalyzeGnorc(s.Overlay, removed, exe).DamageMask == 0x10, "Whole absent explosion packet adds no explosion dependency");
var duplicate = Clone(); var packet = part.Items.Where(i => (i.SourcePc ?? i.Orig) is >= 0x80081538 and <= 0x80081554).ToArray();
duplicate.Items.AddRange(packet); Check(UniversalNativePlayerDamageCalls.AnalyzeGnorc(s.Overlay, duplicate, exe).Calls.Count == 2, "Multiple intact copied packets deduplicate report");
var brokenCopy = Clone(); brokenCopy.Items.AddRange(packet.Skip(1));
Refused(() => UniversalNativePlayerDamageCalls.AnalyzeGnorc(s.Overlay, brokenCopy, exe), "Intact original elsewhere cannot bless partial copied occurrence");
var unknown = Clone(); unknown.Emit(UniversalMips.EncodeJ(3, UniversalNativePlayerDamageCalls.Helper), orig: 0x80081558);
Refused(() => UniversalNativePlayerDamageCalls.AnalyzeGnorc(s.Overlay, unknown, exe), "Unknown surviving native call refuses");
Refused(() => UniversalNativePlayerDamageCalls.AnalyzeGnorc(s.Overlay, part, null), "Missing executable refuses actual calls");
Refused(() => UniversalNativePlayerDamageCalls.AnalyzeGnorc(s.Overlay, part, exe[..^1]), "Partial executable refuses");

var fullPart = UniversalBehaviorBundleBuilder.BuildPrunedUpdate(s.Overlay, [0xC9, 0xCA, 0xCF, 0x138], "damage_all_smoke").Part;
var allResult = UniversalNativePlayerDamageCalls.AnalyzeGnorc(s.Overlay, fullPart, exe);
Check(allResult.Calls.Count == 6 && allResult.DamageMask == 0xC0096, "All six original Gnorc constant-mask helper producers certified");
foreach (var proof in allResult.Calls)
{
    uint call = proof.CallPc;
    uint start = call switch { 0x80080F9C => 0x80080F74, 0x80080FEC => 0x80080FE8, 0x80081550 => 0x80081538, 0x80081E84 => 0x80081E6C, 0x8008224C => 0x80082248, _ => 0x8008409C };
    UniversalAsmPart FullCopy() { var copy = new UniversalAsmPart(); copy.Items.AddRange(fullPart.Items); return copy; }
    for (uint interior = start + 4; interior <= call + 4; interior += 4)
    {
        uint target = interior;
        foreach (var kind in new[] { UniversalFixKind.Branch, UniversalFixKind.Jump })
        {
            var incoming = FullCopy(); incoming.Emit(UniversalMips.EncodeJ(2, target), new(kind, target));
            Refused(() => UniversalNativePlayerDamageCalls.AnalyzeGnorc(s.Overlay, incoming, exe), "New outside branch/jump cannot skip argument producer");
        }
        var raw = FullCopy(); raw.Emit(UniversalMips.EncodeJ(2, target));
        Refused(() => UniversalNativePlayerDamageCalls.AnalyzeGnorc(s.Overlay, raw, exe), "New raw jump cannot skip argument producer");
        var table = FullCopy(); table.Tables["mutant_table"] = [target];
        Refused(() => UniversalNativePlayerDamageCalls.AnalyzeGnorc(s.Overlay, table, exe), "New copied jump table cannot skip argument producer");
        var label = FullCopy(); int targetIndex = label.Items.FindIndex(i => (i.SourcePc ?? i.Orig) == target);
        label.Items.Insert(targetIndex, new(null, default, null, "mutant_label"));
        label.Emit(UniversalMips.EncodeJ(2, 0), new(UniversalFixKind.JumpLabel, Label: "mutant_label"));
        Refused(() => UniversalNativePlayerDamageCalls.AnalyzeGnorc(s.Overlay, label, exe), "New copied label reference cannot skip argument producer");
        int wordIndex = fullPart.Items.FindIndex(i => (i.SourcePc ?? i.Orig) == target);
        var wrongWord = FullCopy(); wrongWord.Items[wordIndex] = wrongWord.Items[wordIndex] with { Word = wrongWord.Items[wordIndex].Word ^ 1 };
        Refused(() => UniversalNativePlayerDamageCalls.AnalyzeGnorc(s.Overlay, wrongWord, exe), "All newly enrolled source packet words authenticated in copies");
    }
}
checks += SourceFamilies.Run(lib, Path.Combine(Path.GetDirectoryName(args[2])!, "all-source-families.json"));
checks += NoCallSources.Run(lib, LevelCatalog.Load(args[1]), Path.Combine(Path.GetDirectoryName(args[2])!, "no-call-sources.json"));
if (args.Length == 4 && args[3] == "--compose") checks += DestinationComposes.Run(lib, Path.Combine(Path.GetDirectoryName(args[2])!, "destination-composes.json"));
UniversalSourceLibrary.RequireOriginal(args[0]);
File.WriteAllText(args[2], JsonSerializer.Serialize(new { checks, result.DamageMask, result.Calls, allCalls = allResult.Calls, animations = result.AnimationIds(exe), helperSha256 = UniversalNativePlayerDamageCalls.HelperSha256, sourceSha256 = Convert.ToHexStringLower(SHA256.HashData(s.Overlay.Bytes)) }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"NATIVE_PLAYER_DAMAGE_CALLS_OK checks={checks}");

// Audit original source classes separately: unknown helper packets remain refusals.
var sw = UniversalDispatchAnalysis.FindUpdateSwitch(s.Overlay);
var ownerClasses = s.Data.Registrations().Select(r => r.ClassId).Distinct().Order().ToArray();
var reach = ownerClasses.ToDictionary(c => c, c => {
    var tr = UniversalDispatchAnalysis.Trace(s.Overlay, sw.TreeStart, c, sw.Seeds, sw.ClassRegister);
    return UniversalDispatchAnalysis.Closure(s.Overlay, sw.Function, [tr.Stop], new HashSet<uint> { sw.TreeStart }).Instructions;
});
var allCalls = new List<object>();
for (uint pc = s.Overlay.CodeStart; pc < s.Overlay.Init; pc += 4)
{
    uint word = s.Overlay.Word(pc);
    if (!UniversalMips.IsJal(word) || UniversalMips.BranchTarget(word, pc) != UniversalNativePlayerDamageCalls.Helper) continue;
    allCalls.Add(new { pc = $"{pc:X8}", enrolled = allResult.Calls.Any(c => c.CallPc == pc), sourceClasses = reach.Where(k => k.Value.Contains(pc)).Select(k => $"{k.Key:X3}").ToArray(), packet = Enumerable.Range(0, 10).Select(n => new { pc = $"{pc - 32 + (uint)n * 4:X8}", word = $"{s.Overlay.Word(pc - 32 + (uint)n * 4):X8}" }).ToArray() });
}
File.WriteAllText(Path.Combine(Path.GetDirectoryName(args[2])!, "uncovered-source-calls.json"), JsonSerializer.Serialize(allCalls, new JsonSerializerOptions { WriteIndented = true }));
