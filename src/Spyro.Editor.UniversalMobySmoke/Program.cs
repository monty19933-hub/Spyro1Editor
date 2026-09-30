using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

// Universal Moby import checks and candidate tooling.
//   (no args)                        source-free unit checks
//   --survey <orig.bin> <workspace> [out.json]
//   --compose <orig.bin> <workspace> <out.bin> <dest> <src:row:x:y:z[:yaw]|special:id:x:y:z[:yaw[:LINE_1|LINE_2]]>... [--japan <jp.bin>] [--test-demo-level]
int failures = 0, checks = 0;
void Check(bool ok, string what) { checks++; if (!ok) { failures++; Console.WriteLine("FAIL " + what); } }

if (args.Length == 0)
{
    SourceFreeChecks();
    Console.WriteLine($"{checks} checks, {failures} failures");
    if (failures == 0) Console.WriteLine("UNIVERSAL_MOBY_SOURCE_FREE_OK");
    return failures == 0 ? 0 : 1;
}

if (args[0] == "--egg-row-capacity")
{
    for (int n = 7; n < 200; n++)
    {
        try { UniversalSpecialBehaviors.DragonflyEggInventoryRoutine(Enumerable.Range(0, n).Select(i => (0, i, 0)).ToList()); }
        catch (InvalidDataException) { Console.WriteLine($"max {n - 1}"); break; }
    }
    return 0;
}
if (args[0] == "--egg-bbox")
{
    var jp = new UniversalDiscImage[0];
    var src = UniversalDiscImage.ReadAny(args[1], "SCPS_100.83");
    var art = new UniversalLevelData(src.EntryBytes(UniversalDragonflyEggs.JapaneseArtisansDataEntry));
    foreach (var (label, pkg) in new[] { ("original", art.Package(UniversalDragonflyEggs.JapaneseEggClass)), ("eyes", UniversalEggEyes.AddEyes(art.Package(UniversalDragonflyEggs.JapaneseEggClass))) })
        foreach (var f in UniversalEggEyes.DebugFrames(pkg))
            { if (label == "eyes") Console.WriteLine(string.Join(" ", f.Skip(82).Select(v => $"({v.X},{v.Y},{v.Z})")));
            Console.WriteLine($"{label} frame: n={f.Count} x[{f.Min(v => v.X)},{f.Max(v => v.X)}] y[{f.Min(v => v.Y)},{f.Max(v => v.Y)}] z[{f.Min(v => v.Z)},{f.Max(v => v.Z)}] scale={pkg[0x3C + 5]} shift={pkg[0x3C + 6]}"); }
    return 0;
}
var catalog = LevelCatalog.Load(args[2]);
if (args[0] == "--compose")
{
    var lib = new UniversalSourceLibrary(args[1], catalog);
    string outBin = args[3], dest = args[4];
    bool demo = args.Contains("--test-demo-level");
    int ji = Array.IndexOf(args, "--japan");
    string? japanPath = ji >= 0 ? args[ji + 1] : null;
    var flagValues = new HashSet<int> { Array.IndexOf(args, "--japan") + 1, Array.IndexOf(args, "--remove") + 1 };
    var placements = args.Skip(5).Where((a, i) => !a.StartsWith("--") && !flagValues.Contains(i + 5)).Select(a =>
    {
        var p = a.Split(':');
        if (p[0] == "special")
            return new UniversalMobyPlacement("japan", -1, int.Parse(p[2]), int.Parse(p[3]), int.Parse(p[4]),
                p.Length > 5 && p[5].Length > 0 ? Convert.ToInt32(p[5], 16) : null, SpecialId: p[1],
                Text: p.Length > 6 ? p[6].Replace('_', ' ').Split('|') : null);
        return new UniversalMobyPlacement(p[0], int.Parse(p[1]), int.Parse(p[2]), int.Parse(p[3]), int.Parse(p[4]),
            p.Length > 5 ? Convert.ToInt32(p[5], 16) : null);
    }).ToList();
    var japanese = japanPath != null ? new UniversalJapaneseSource(japanPath) : null;
    int ri = Array.IndexOf(args, "--remove");
    IReadOnlyCollection<int>? removedRows = ri >= 0 ? args[ri + 1].Split(',').Select(int.Parse).ToList() : null;
    string input = args[1];
    if (demo)
    {
        // Diagnostic-only: route the attract demo into the destination so an automated
        // emulator session can reach the level. Never used by the editor's Create BIN.
        var d = UniversalDiscImage.Read(args[1]);
        byte[] exe = d.Exe.ToArray();
        int lvl = catalog.FindByKey(dest)!.LevelId;
        int o = (int)(0x8006EE7C - UniversalExeBindings.ExeBase);
        for (int i = 0; i < 4; i++) BinaryPrimitives.WriteInt32LittleEndian(exe.AsSpan(o + 4 * i), lvl);
        input = outBin + ".demo-src.bin";
        d.WriteImage(input, d.Wad, exe);
    }
    var result = UniversalMobyExport.Apply(lib, input, outBin, catalog,
        new Dictionary<string, IReadOnlyList<UniversalMobyPlacement>> { [dest] = placements }, japanese: japanese,
        removedRowsByLevel: removedRows != null ? new Dictionary<string, IReadOnlyCollection<int>> { [dest] = removedRows } : null);
    if (demo) File.Delete(input);
    File.WriteAllText(Path.ChangeExtension(outBin, ".cue"), $"FILE \"{Path.GetFileName(outBin)}\" BINARY\r\n  TRACK 01 MODE2/2352\r\n    INDEX 01 00:00:00\r\n");
    var c = result.Levels[0].Compose;
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        c.DestinationKey, c.Ram, c.ArenaSlotsRemaining, DroppedModelClasses = c.DroppedModelClasses.Select(x => $"0x{x:X3}"), c.DroppedModelBytes, c.VramBytesUsed, c.NewRowIndices, c.Warnings,
        Classes = c.Classes.Select(x => new { Class = $"0x{x.ClassId:X3}", x.SourceLevelKey, x.ModelImported, x.BehaviorImported, x.ModelBytes, x.Textures, x.Sounds, x.DependencyOnly }),
        c.Behavior, result.OutputImageSha256, result.WadGrowthBytes
    }, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
if (args[0] == "--demo-patch")
{
    // Diagnostic-only: route the attract demo of an already exported image into one level.
    var d = UniversalDiscImage.Read(args[1]);
    byte[] exe = d.Exe.ToArray();
    int lvl = catalog.FindByKey(args[4])!.LevelId;
    int o = (int)(0x8006EE7C - UniversalExeBindings.ExeBase);
    for (int i = 0; i < 4; i++) BinaryPrimitives.WriteInt32LittleEndian(exe.AsSpan(o + 4 * i), lvl);
    d.WriteImage(args[3], d.Wad, exe);
    File.WriteAllText(Path.ChangeExtension(args[3], ".cue"), $"FILE \"{Path.GetFileName(args[3])}\" BINARY\r\n  TRACK 01 MODE2/2352\r\n    INDEX 01 00:00:00\r\n");
    return 0;
}
if (args[0] == "--droppable")
{
    // Diagnostic: for each locally modelled class in a level, what removing all of its rows would free.
    var lib = new UniversalSourceLibrary(args[1], catalog);
    var (lvl, data, ov) = lib.Get(args[3]);
    var sc = data.Scene();
    var rows = Enumerable.Range(0, sc.RowCount).GroupBy(i => (int)BinaryPrimitives.ReadUInt16LittleEndian(data.Row(i).AsSpan(0x36)))
        .ToDictionary(g => g.Key, g => g.ToList());
    foreach (var reg in data.Registrations().OrderByDescending(r => data.Package(r.ClassId).Length))
    {
        if (!rows.TryGetValue(reg.ClassId, out var rs)) { Console.WriteLine($"0x{reg.ClassId:X3} {data.Package(reg.ClassId).Length,7} B  no rows"); continue; }
        var (drop, _) = UniversalMobyComposer.DroppableClasses(new UniversalLevelData(data.Bytes.ToArray()), ov, lib.Disc.Exe.ToArray(), lib.SharedClasses, rs, new HashSet<int>());
        Console.WriteLine($"0x{reg.ClassId:X3} {data.Package(reg.ClassId).Length,7} B  rows {string.Join(",", rs)}  {(drop.Contains(reg.ClassId) ? "droppable" : "kept (spawned/referenced)")}");
    }
    return 0;
}
if (args[0] == "--survey")
{
    var lib = new UniversalSourceLibrary(args[1], catalog);
    var survey = UniversalMobySurvey.Run(lib, catalog);
    File.WriteAllText(args.Length > 3 ? args[3] : "universal-survey.json", JsonSerializer.Serialize(survey, new JsonSerializerOptions { WriteIndented = true }));
    foreach (var g in survey.GroupBy(s => s.Outcome)) Console.WriteLine($"{g.Key}: {g.Count()}");
    return 0;
}
if (args[0] == "--spawn")
{
    foreach (var key in args.Skip(3))
    {
        var lvl = catalog.FindByKey(key)!;
        var l = Spyro.Editor.Core.Exporting.FlyInLandingLocator.Locate(args[1], lvl);
        Console.WriteLine(JsonSerializer.Serialize(new { key, l }));
    }
    return 0;
}
if (args[0] == "--debug-class")
{
    var lib = new UniversalSourceLibrary(args[1], catalog);
    var o = lib.Get(args[3]).Overlay; int cls = Convert.ToInt32(args[4], 16);
    var sw = UniversalDispatchAnalysis.FindUpdateSwitch(o);
    var t = UniversalDispatchAnalysis.Trace(o, sw.TreeStart, cls, sw.Seeds, sw.ClassRegister);
    Console.WriteLine($"ts {sw.TreeStart:X8} creg {sw.ClassRegister} stop {t.Stop:X8} seq {t.Sequence.Count} default {sw.DefaultStop:X8}");
    try { var r = UniversalBehaviorBundleBuilder.Build(o, [(args[3], o, [cls])], 0x80090000); Console.WriteLine($"ok {r.Code.Length}"); }
    catch (Exception e) { Console.WriteLine(e.Message); }
    return 0;
}
Console.WriteLine("unknown mode"); return 2;

void SourceFreeChecks()
{
    // Encode/decode round trips.
    uint j = UniversalMips.EncodeJ(3, 0x8007AF94);
    Check(UniversalMips.BranchTarget(j, 0x80084A18) == 0x8007AF94, "jal target round trip");
    uint b = UniversalMips.WithBranchTarget(UniversalMips.EncodeI(4, 3, 2, 0), 0x80001000, 0x80000F00);
    Check(UniversalMips.BranchTarget(b, 0x80001000) == 0x80000F00, "backward branch round trip");
    Check(UniversalMips.Hi(0x8007FFF8) == 0x8008 && UniversalMips.Lo(0x8007FFF8) == 0xFFF8, "hi/lo carry");
    // Symbolic evaluation of a compiled compare tree.
    var s = new UniversalMipsState(new Dictionary<int, uint> { [3] = 0x22 });
    s.Apply(UniversalMips.EncodeI(10, 3, 2, 0x4D));   // slti v0,v1,0x4d
    Check(s[2] == 1, "slti evaluates");
    Check(s.Condition(UniversalMips.EncodeI(5, 2, 0, 4)) == true, "bnez v0 taken");
    s.Apply(UniversalMips.EncodeI(35, 29, 2, 0x10));   // lw kills v0
    Check(s[2] == null, "loads are unknown");
    // Synthetic overlay: tiny update function with a compare tree and two cases.
    SyntheticOverlayChecks();
}

void SyntheticOverlayChecks()
{
    uint B = UniversalOverlay.Base;
    List<uint> code = [];
    void E(uint w) => code.Add(w);
    // f: prologue
    E(UniversalMips.EncodeI(9, 29, 29, -0x20));                 // 0 addiu sp,-0x20
    E(UniversalMips.EncodeI(43, 29, 31, 0x1C));                 // 1 sw ra
    E(UniversalMips.EncodeI(35, 16, 19, 0));                    // 2 lw s3,(s0)   loop:
    E(0);                                                       // 3 nop
    E(UniversalMips.EncodeI(0x21, 19, 3, 0x36));                // 4 lh v1,0x36(s3)  <- class load
    E(0);                                                       // 5 nop
    E(UniversalMips.EncodeI(9, 0, 2, 0x10));                    // 6 li v0,0x10
    E(UniversalMips.EncodeI(4, 3, 2, 5));                       // 7 beq v1,v0,+5 -> 13
    E(UniversalMips.EncodeI(9, 0, 2, 0x22));                    // 8 li v0,0x22 (delay)
    E(UniversalMips.EncodeI(4, 3, 2, 5));                       // 9 beq v1,v0,+5 -> 15
    E(0);                                                       // 10 nop
    E(UniversalMips.EncodeJ(2, B + 17 * 4));                    // 11 j cont
    E(0);                                                       // 12
    E(UniversalMips.EncodeI(9, 0, 4, 0x111));                   // 13 case 0x10: li a0,0x111
    E(UniversalMips.EncodeJ(2, B + 17 * 4));                    // 14 j cont   (delay 15 shared)
    E(UniversalMips.EncodeI(9, 0, 5, 0x222));                   // 15 case 0x22: li a1,0x222
    E(0);                                                       // 16
    E(UniversalMips.EncodeI(5, 19, 0, -16));                    // 17 cont: bnez s3, loop(2)
    E(UniversalMips.EncodeI(9, 16, 16, 4));                     // 18 addiu s0,4
    E(UniversalMips.EncodeI(35, 29, 31, 0x1C));                 // 19 lw ra
    E(0);                                                       // 20
    E(UniversalMips.JrRa);                                      // 21
    E(UniversalMips.EncodeI(9, 29, 29, 0x20));                  // 22
    // init function (whole copy)
    uint init = B + (uint)code.Count * 4;
    E(UniversalMips.JrRa); E(0);
    byte[] ov = new byte[code.Count * 4 + 64];
    for (int i = 0; i < code.Count; i++) BinaryPrimitives.WriteUInt32LittleEndian(ov.AsSpan(i * 4), code[i]);
    var bindings = new Dictionary<uint, UniversalExeBinding>
    {
        [UniversalExeBindings.UpdateGlobal] = new(UniversalExeBindings.UpdateGlobal, 0, 0, B),
        [UniversalExeBindings.InitGlobal] = new(UniversalExeBindings.InitGlobal, 0, 0, init),
        [UniversalExeBindings.CopyBufferGlobal] = new(UniversalExeBindings.CopyBufferGlobal, 0, 0, B + (uint)ov.Length),
    };
    var o = new UniversalOverlay(ov, bindings);
    var sw = UniversalDispatchAnalysis.FindUpdateSwitch(o);
    Check(sw.TreeStart == B + 20 && sw.ClassRegister == 3, "synthetic switch located after class load");
    Check(UniversalDispatchAnalysis.Handles(o, sw, 0x10) && UniversalDispatchAnalysis.Handles(o, sw, 0x22), "short inlined cases are recognised as handled");
    Check(!UniversalDispatchAnalysis.Handles(o, sw, 0x33) && !UniversalDispatchAnalysis.Handles(o, sw, 0x1FF), "unlisted classes fall to the default");
    var bundle = UniversalBehaviorBundleBuilder.Build(o, [("src", o, [0x22])], 0x80090000);
    Check(bundle.UpdateWrapper == 0x80090000 + 12, "wrapper follows the 3-word sign-hook preamble");
    Check(BinaryPrimitives.ReadUInt32LittleEndian(bundle.Code) == 0 &&
          BinaryPrimitives.ReadUInt32LittleEndian(bundle.Code.AsSpan(4)) == UniversalMips.JrRa, "sign-less bundle has no marker and a returning stub");
    // wrapper epilogue must separate lw ra from jr ra
    uint w5 = BinaryPrimitives.ReadUInt32LittleEndian(bundle.Code.AsSpan(9 * 4));
    uint w6 = BinaryPrimitives.ReadUInt32LittleEndian(bundle.Code.AsSpan(10 * 4));
    Check(UniversalMips.Op(w5) == 35 && w6 == 0, "wrapper honours load delay before jr ra");
    // Every branch inside the bundle must land inside the bundle or on a known engine address.
    uint end = 0x80090000 + (uint)bundle.Code.Length;
    for (int i = 0; i < bundle.Code.Length / 4; i++)
    {
        uint w = BinaryPrimitives.ReadUInt32LittleEndian(bundle.Code.AsSpan(i * 4)), pc = 0x80090000 + (uint)i * 4;
        if (UniversalMips.IsPcRelative(w)) Check(UniversalMips.BranchTarget(w, pc) is var t && t >= 0x80090000 && t < end, $"branch at {pc:X8} stays in bundle");
        if (UniversalMips.Op(w) == 2) Check(UniversalMips.BranchTarget(w, pc) is var t2 && ((t2 >= 0x80090000 && t2 < end) || t2 == init), $"jump at {pc:X8} resolves");
    }
    // Case 0x10 (not imported) must be absent: li a0,0x111 must not appear.
    bool has111 = false;
    for (int i = 0; i < bundle.Code.Length / 4; i++) if (BinaryPrimitives.ReadUInt32LittleEndian(bundle.Code.AsSpan(i * 4)) == UniversalMips.EncodeI(9, 0, 4, 0x111)) has111 = true;
    Check(!has111, "non-imported case is pruned");

    // Signs and dragonfly eggs.
    var signed = UniversalBehaviorBundleBuilder.Build(o, [], 0x80090000, [0x1F2], [UniversalDragonflyEggs.SignClass]);
    Check(BinaryPrimitives.ReadUInt32LittleEndian(signed.Code) == UniversalSpecialBehaviors.SignHookMarker, "bundle with signs carries the marker");
    uint stub = BinaryPrimitives.ReadUInt32LittleEndian(signed.Code.AsSpan(4));
    Check(UniversalMips.Op(stub) == 2 && UniversalMips.BranchTarget(stub, 0x80090004) > signed.UpdateWrapper &&
          UniversalMips.BranchTarget(stub, 0x80090004) < 0x80090000 + (uint)signed.Code.Length, "sign stub jumps to the bundle's sign routine");
    uint[] hook = UniversalSpecialBehaviors.SignHookRoutine();
    Check(hook.Length * 4 <= UniversalSpecialBehaviors.SignHookHostBytes, "render hook fits the unused host routine");
    Check(hook.Contains(UniversalMips.EncodeJ(3, 0x80019300)), "render hook still calls the native gem HUD");
    Check(hook.Contains(UniversalMips.EncodeI(35, 2, 3, -12)), "render hook reads the marker before the level's update function");
    bool encodeOk = true;
    foreach (var (x, y, z) in new[] { (0, 0, 0), (-150, 220, -300), (190, -271, -2), (-190, 287, -358), (57, -3, -121) })
    {
        uint w = UniversalEggEyes.EncodeAbsolute(x, y, z);
        int dx = (int)w >> 21, dy = ((int)w << 10) >> 21, dz = ((int)w << 20) >> 19;
        encodeOk &= Math.Abs(dx - x) <= 1 && Math.Abs(dy - y) <= 2 && Math.Abs(dz - z) <= 3 && (w & 1) == 0;
    }
    Check(encodeOk, "egg eye vertices encode within a few units in the native absolute vertex format");
    var (slotCode, slotData) = UniversalSpecialBehaviors.DragonflyEggInventoryRoutine([(4, 59, 0), (4, 60, 1), (12, 200, 6)]);
    Check(slotCode.Contains(UniversalMips.EncodeJ(2, 0x8001844C)) && slotCode.Contains(UniversalMips.EncodeJ(3, 0x8001919C)) &&
          slotCode.Contains(UniversalMips.EncodeJ(3, 0x800168DC)) && slotData[2] == (6u << 24 | 12u << 16 | 200) && slotData[0] == (4u << 16 | 59),
          "egg row draws coloured still eggs with eyes, keeps the divider line, and stores level/row/colour per egg");
    Check(slotData.Length == 3 + UniversalDragonflyEggs.Variants.Length + 2 && slotData[^2] == 0x252F6425, "egg data holds the tint table and K/Y format");
    var many = Enumerable.Range(0, UniversalSpecialBehaviors.MaxDragonflyEggs).Select(i => (i % 30, i, i % 7)).ToList();
    var (countCode, countData) = UniversalSpecialBehaviors.DragonflyEggInventoryRoutine(many);
    Check(countCode.Contains(UniversalMips.EncodeJ(3, 0x80062FD4)) && UniversalSpecialBehaviors.MaxDragonflyEggs >= 60 &&
          UniversalSpecialBehaviors.EggInventoryData + countData.Length * 4 <= UniversalSpecialBehaviors.CaveEnd, "the maximum number of eggs fits (one egg plus a K/Y count)");
    Check(UniversalSpecialBehaviors.SignHookHost + UniversalSpecialBehaviors.SignHookRoutine().Length * 4 <= UniversalSpecialBehaviors.EggInventoryData, "sign hook and egg data do not overlap");
    byte[] text = UniversalSpecialBehaviors.EncodeSignText(["Hello, world!", "line two", "x"]);
    Check(text.Length == 84 && System.Text.Encoding.ASCII.GetString(text, 0, 13) == "HELLO, WORLD!" && text[13] == 0 &&
          System.Text.Encoding.ASCII.GetString(text, 28, 8) == "LINE TWO" && text[56] == (byte)'X', "sign text encodes three capitalised lines");
    Check(UniversalSpecialBehaviors.NormalizeSignLine("it's ~ok~ " + new string('a', 40)).Length == UniversalSpecialBehaviors.SignLineMaxChars &&
          UniversalSpecialBehaviors.NormalizeSignLine("it's").SequenceEqual("ITS"), "unsupported characters are dropped and lines capped");
    Check(UniversalPlacementReader.SplitSignText("A\r\nB\nC\nD").SequenceEqual(["A", "B", "C"]), "sign text keeps three lines");
    var specialEntries = UniversalSpecialCatalog.Entries(true);
    Check(specialEntries.Count == 2 && specialEntries[0].Variants.Count == UniversalDragonflyEggs.Variants.Length &&
          specialEntries.All(e => e.RestrictionReason == null) && UniversalSpecialCatalog.Entries(false).All(e => e.RestrictionReason != null),
          "egg and sign catalogue entries (restricted without the Japanese disc)");
    Check(specialEntries[0].SubCategory == "Dragonfly Eggs" && specialEntries[1].SubCategory == "Signs", "egg/sign categories");
    Check(UniversalDragonflyEggs.Variants.Select(v => v.Class).Append(UniversalDragonflyEggs.SignClass).Distinct().Count() == 8, "distinct special classes");
    string tmp = Path.Combine(Path.GetTempPath(), $"universal-reader-{Guid.NewGuid():N}.json");
    File.WriteAllText(tmp, JsonSerializer.Serialize(new { edits = new object[] {
        new { added = true, removed = false, trueIndex = 60, signText = "HI\nTHERE", rawEdited = new { x = 1, y = 2, z = 3 },
              crossLevelTemplate = new { id = "special:sign", sourceLevelKey = "japan", sourceTrueIndex = 173, requiredExporterFeature = UniversalMobyExport.RequiredExporterFeature } },
        new { added = true, removed = true, trueIndex = 61, rawEdited = new { x = 1, y = 2, z = 3 },
              crossLevelTemplate = new { id = "special:dragonfly-egg-red", sourceLevelKey = "japan", sourceTrueIndex = 193, requiredExporterFeature = UniversalMobyExport.RequiredExporterFeature } } } }));
    var read = UniversalPlacementReader.Read(tmp);
    File.Delete(tmp);
    Check(read.Count == 1 && read[0].SpecialId == "sign" && read[0].Text!.SequenceEqual(["HI", "THERE"]) && read[0].X == 1, "placement reader restores special id and sign text; deleted rows skipped");
}
