using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

int checks = 0;
void Check(bool condition, string label) { checks++; if (!condition) throw new InvalidOperationException(label); }
uint I(int op, int rs, int rt, int imm) => UniversalMips.EncodeI(op, rs, rt, imm);
(UniversalOverlay Overlay, HashSet<uint> Owned) Fixture(uint[] words)
{
    byte[] bytes = new byte[words.Length * 4];
    for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), words[i]);
    var bindings = new Dictionary<uint, UniversalExeBinding>();
    foreach (uint g in new[] { UniversalExeBindings.UpdateGlobal, UniversalExeBindings.InitGlobal, UniversalExeBindings.CopyBufferGlobal }) bindings[g] = new(g, 0, 0, UniversalOverlay.Base);
    return (new(bytes, bindings), Enumerable.Range(0, words.Length).Select(i => UniversalOverlay.Base + (uint)i * 4).ToHashSet());
}
uint[] Words(int gap = 14)
{
    uint[] w = new uint[gap + 5]; w[0] = I(9, 0, 4, 0x1A6);
    w[gap - 3] = I(15, 0, 8, 0x8007); w[gap - 2] = I(35, 8, 25, 0x58CC);
    w[gap + 1] = (25u << 21) | (31u << 11) | 9; w[gap + 3] = UniversalMips.JrRa;
    return w;
}
bool Refused(uint[] words, Action<HashSet<uint>>? cut = null)
{
    var f = Fixture(words); cut?.Invoke(f.Owned);
    try { UniversalClassRemapping.ConstantSpawns(f.Overlay, f.Owned); return false; }
    catch (InvalidDataException) { return true; }
}
var baseline = Fixture(Words()); byte[] original = baseline.Overlay.Bytes.ToArray();
var found = UniversalClassRemapping.ConstantSpawns(baseline.Overlay, baseline.Owned).Single();
Check(found.ConstantPc == UniversalOverlay.Base && found.CallPc == UniversalOverlay.Base + 60 && found.ClassId == 0x1A6, "Direct producer60bytes before exact source callback recovered");
Check(baseline.Overlay.Bytes.SequenceEqual(original), "Producer discovery leaves source instructions unchanged");
var ori = Words(); ori[0] = I(13, 0, 4, 0x1A6); Check(!Refused(ori), "Exact zero-base ORI producer admitted");
var shortWords = Words(8); Check(!Refused(shortWords), "Existing short producer remains admitted");
Check(Refused(Words(16)), "Producer68bytes before call stays outside bounded extension");
foreach (int bad in new[] { 0, 0x200, 0xFFFF }) { var w = Words(); w[0] = I(9, 0, 4, bad); Check(Refused(w), "Out-of-range child class refused"); }
var delayedCallback = Words(); delayedCallback[14] = delayedCallback[12]; delayedCallback[12] = 0; Check(Refused(delayedCallback), "Callback load delay prevents extended recovery");
var loaded = Words(); loaded[9] = I(35, 16, 4, 0); Check(Refused(loaded), "Mutable property load is not a constant class");
var alias = Words(); alias[8] = (4u << 21) | (4u << 11) | 0x21; Check(Refused(alias), "Non-direct producer is not rewritten by value coincidence");
var call = Words(); call[8] = UniversalMips.EncodeJ(3, 0x80010000); Check(Refused(call), "Caller-clobbering call boundary refused");
var branch = Words(); branch[8] = I(4, 16, 0, 1); Check(Refused(branch), "Branch-entry continuation cannot borrow predecessor constant");
Check(Refused(Words(), s => s.Remove(UniversalOverlay.Base + 16)), "Missing captured instruction breaks longer producer ownership");
var incoming = Words().Concat(new uint[] { I(4, 0, 0, -17), 0 }).ToArray(); Check(Refused(incoming), "Incoming control target within producer range refuses lexical-only inference");
var unknown = Words().Concat(new uint[] { (27u << 21) | 8, 0 }).ToArray(); Check(Refused(unknown), "Unknown indirect entry declines extended provenance");
var wrongGlobal = Words(); wrongGlobal[12] = I(35, 8, 25, 0x58E4); Check(UniversalClassRemapping.ConstantSpawns(Fixture(wrongGlobal).Overlay, Fixture(wrongGlobal).Owned).Count == 0, "Particle callback is not a class spawn");

if (args.Length is 3 or 4)
{
    UniversalSourceLibrary.RequireOriginal(args[0]); var catalog = LevelCatalog.Load(args[1]); var lib = new UniversalSourceLibrary(args[0], catalog);
    List<object> owners = []; HashSet<uint> sourcePcs = []; int inspected = 0;
    foreach (var level in catalog.Levels.Where(l => l.SourceWadEntry > 0))
    {
        var s = lib.Get(level.Key); var sw = UniversalDispatchAnalysis.FindUpdateSwitch(s.Overlay);
        if (!UniversalDispatchAnalysis.Handles(s.Overlay, sw, 0x1A5)) continue;
        var trace = UniversalDispatchAnalysis.Trace(s.Overlay, sw.TreeStart, 0x1A5, sw.Seeds, sw.ClassRegister);
        var def = UniversalDispatchAnalysis.Trace(s.Overlay, sw.TreeStart, UniversalDispatchAnalysis.SentinelClass, sw.Seeds, sw.ClassRegister);
        var owned = UniversalDispatchAnalysis.Closure(s.Overlay, sw.Function, new[] { sw.Function, trace.Stop, def.Stop }, new HashSet<uint> { sw.TreeStart }).Instructions;
        var spawns = UniversalClassRemapping.ConstantSpawns(s.Overlay, owned).Where(c => c.ClassId == 0x1A6 && c.CallPc - c.ConstantPc == 60).ToArray();
        if (spawns.Length == 0) continue;
        Check(spawns.Length == 1, "Exactly one longer direct child producer per captured source class1A5"); inspected++;
        var spawn = spawns.Single(); Check(s.Overlay.Word(spawn.ConstantPc) == 0x240401A6, "Native producer is exact ADDIU a0,zero,1A6");
        Check(UniversalDependencyAnalysis.Calls(s.Overlay, owned).Any(c => c.CallPc == spawn.CallPc && c.Argument0 == 0x1A6 && c.Global == UniversalExeBindings.InitGlobal), "Independent fenced callback/argument proof agrees");
        var cut = owned.ToHashSet(); cut.Remove(spawn.ConstantPc);
        bool denied = false; try { UniversalClassRemapping.ConstantSpawns(s.Overlay, cut); } catch (InvalidDataException) { denied = true; }
        Check(denied, "Removing the original producer refuses real source remapping");
        var dest = lib.Get("toasty"); byte[] immutable = s.Overlay.Bytes.ToArray();
        // This disassembly check preserves other source callback IDs; complete asset
        // enrollment is verified separately by the real Compose rescan below.
        var namespaceMap = Enumerable.Range(1, 0x1FF).ToDictionary(c => c, c => c);
        namespaceMap[0x1A5] = 0x1FD; namespaceMap[0x1A6] = 0x1FC;
        var maps = new Dictionary<string, IReadOnlyDictionary<int, int>> { [level.Key] = namespaceMap };
        var bundle = UniversalBehaviorBundleBuilder.Build(dest.Overlay, [(level.Key, s.Overlay, (IReadOnlyList<int>)new[] { 0x1A5 })], dest.Overlay.CopyBuffer, classMaps: maps);
        Check(Enumerable.Range(0, bundle.Code.Length / 4).Any(i => BinaryPrimitives.ReadUInt32LittleEndian(bundle.Code.AsSpan(i * 4)) == I(9, 0, 4, 0x1FC)), "Assembled source namespace patches the actual child producer to its mapped class");
        Check(s.Overlay.Bytes.SequenceEqual(immutable), "Actual source overlay immutable after mapped bundle construction");
        sourcePcs.Add(spawn.CallPc);
        owners.Add(new { Source = level.Key, spawn.CallPc, spawn.ConstantPc, spawn.ClassId, BundleBytes = bundle.Code.Length });
    }
    Check(inspected == 21, "All21 authenticated native source owners covered");
    List<object> cells = []; Dictionary<string, List<(long Offset, int Length)>> protection = [];
    if (args.Length == 4)
    {
        using var matrixFile = File.OpenRead(args[3]); using var zip = new GZipStream(matrixFile, CompressionMode.Decompress); using var matrix = JsonDocument.Parse(zip);
        var variants = matrix.RootElement.GetProperty("Variants").EnumerateArray().ToDictionary(v => v.GetProperty("VariantId").GetString()!);
        var old = matrix.RootElement.GetProperty("Cells").EnumerateArray().Where(c => c.GetProperty("Detail").ValueKind == JsonValueKind.String && sourcePcs.Any(pc => c.GetProperty("Detail").GetString()!.Contains($"0x{pc:X8}"))).ToArray();
        Check(old.Length == 584, "Exact584 old blocked cells selected by authenticated native call sites");
        foreach (var cell in old)
        {
            string destination = cell.GetProperty("Destination").GetString()!, variantId = cell.GetProperty("VariantId").GetString()!; var v = variants[variantId]; string key = v.GetProperty("SourceLevelKey").GetString()!; int sourceRow = v.GetProperty("SourceRow").GetInt32();
            var src = lib.Get(key); var dst = lib.Get(destination); byte[] row = src.Data.Row(sourceRow);
            if (!protection.ContainsKey(destination)) protection[destination] = UniversalMobyExport.DefaultProtectedVram(args[0], dst.Level).ToList();
            try
            {
                var result = UniversalMobyComposer.Compose(dst.Level, dst.Data.Bytes, dst.Overlay.Bytes.ToArray(), lib.Disc.Exe, lib.Get, lib.SharedClasses, protection[destination], [new(key, sourceRow, BitConverter.ToInt32(row, 12), BitConverter.ToInt32(row, 16), BitConverter.ToInt32(row, 20))], sharedModelPackage: lib.SharedModelPackage, playerAnimationSource: lib.PlayerAnimations);
                cells.Add(new { Destination = destination, VariantId = variantId, Outcome = "composed", RuntimeStatus = "not-tested", result.Ram, result.ParticleDependencies, result.UnresolvedObjectLinks });
            }
            catch (Exception e) when (e is InvalidDataException or InvalidOperationException or IOException or ArgumentException)
            {
                Check(!e.Message.Contains("dynamic child-spawn argument"), "Original bounded producer refusal is closed; other resource/ownership limits remain explicit");
                cells.Add(new { Destination = destination, VariantId = variantId, Outcome = "failed", RuntimeStatus = "not-tested", Detail = e.Message });
            }
            if (cells.Count % 64 == 0) Console.Error.WriteLine($"Affected cells {cells.Count}/{old.Length}");
        }
    }
    File.WriteAllText(args[2], JsonSerializer.Serialize(new { Evidence = "source/static composition; all runtime not-tested", CoreSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(UniversalClassRemapping).Assembly.Location))), SourceImageSha256 = UniversalSourceLibrary.OriginalUsaSha256, Checks = checks, SourceOwners = owners, AffectedCells = cells }, new JsonSerializerOptions { WriteIndented = true }));
}
else if (args.Length != 0) throw new ArgumentException("Arguments: ORIGINAL_USA_BIN CATALOG_WORKSPACE REPORT_JSON [OLD_MATRIX_JSON_GZ]; no args runs synthetic refusal/producer tests.");
Console.WriteLine($"UNIVERSAL_SPAWN_PRODUCER_CHECKS_OK checks={checks} sourceVerified={args.Length >= 3}");
