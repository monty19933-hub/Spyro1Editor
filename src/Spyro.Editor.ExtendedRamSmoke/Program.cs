using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Universal;

// "Emulator only (8 MB RAM)": the executable patch (NativeExtendedRam), the split RAM budget (GameRam) and the project
// setting (GameMemorySettings).
//   (no arguments)                               disc-free checks
//   --source <USA bin> <catalog workspace>       also: installed into the retail executable (read only), every retail
//                                                level against both layouts, and Tree Tops with imported enemies
//                                                (refused with 2 MB, composed with 8 MB)
//   --patch-image <in.bin> <out.bin>             lab: a copy of an exported image with the scene placement installed
int checks = 0, failed = 0;
bool verbose = Environment.GetEnvironmentVariable("VERBOSE") == "1";
void Check(bool ok, string what) { checks++; if (!ok) { failed++; Console.WriteLine("FAIL: " + what); } else if (verbose) Console.WriteLine("ok   " + what); }
bool Throws<T>(Action action, Func<T, bool>? message = null) where T : Exception
{
    try { action(); return false; }
    catch (T e) { return message?.Invoke(e) ?? true; }
}

if (args.Length >= 3 && args[0] == "--patch-image")
{
    var image = UniversalDiscImage.Read(args[1]);
    var exe = image.Exe.ToArray();
    NativeExtendedRam.PatchExe(exe);
    image.WriteImage(args[2], image.Wad, exe);
    File.WriteAllLines(Path.ChangeExtension(args[2], ".cue"), ["FILE \"" + Path.GetFileName(args[2]) + "\" BINARY", "  TRACK 01 MODE2/2352", "    INDEX 01 00:00:00"]);
    Console.WriteLine("8 MB scene placement installed -> " + args[2]);
    return 0;
}

// ---- 1. The check's words, independently spelled out (the research prototype's code).
uint[] inGame = [0x00441021, 0x3C048000, 0x8C840080, 0x3C018020, 0x8C210080, 0, 0x10810002, 0, 0x3C028020, 0x03E00008, 0];
uint[] startup = [0x00431021, 0x3C038000, 0x8C630080, 0x3C018020, 0x8C210080, 0, 0x10610002, 0, 0x3C028020, 0x03E00008, 0xACA20000];
Check(NativeExtendedRam.Check(4, false).SequenceEqual(inGame), "in-game check: addu v0,v0,a0; kernel word; word 2 MB higher; beq; lui v0,0x8020; jr ra");
Check(NativeExtendedRam.Check(3, true).SequenceEqual(startup), "start-up check: the same with v1, and sw v0,(a1) in jr's delay slot");
byte[] cave = NativeExtendedRam.CaveImage();
Check(cave.Length == 0x90 && Enumerable.Range(0, 22).All(i => BinaryPrimitives.ReadUInt32LittleEndian(cave.AsSpan(4 * i)) == (i < 11 ? inGame[i] : startup[i - 11])) &&
    cave.AsSpan(88).IndexOfAnyExcept((byte)0) < 0, "the routine holds both checks (22 words) and zeros to its end (0x90 bytes)");
const uint JalInGame = 0x0C01758A, JalStartup = 0x0C017595;

// ---- 2. Both hook sites run on a test CPU, on a 2 MB console (mirror) and with 8 MB.
const uint Kernel = 0x3C1A0000; // the first word of the kernel's exception handler
(uint V0, uint Size, uint V1, uint Stored, List<uint> Writes) Site(bool startupSite, bool mirror, uint extendedWord)
{
    var cpu = new CheckCpu(mirror);
    cpu.Put(NativeExtendedRam.Cave, Enumerable.Range(0, 36).Select(i => BinaryPrimitives.ReadUInt32LittleEndian(cave.AsSpan(4 * i))));
    cpu.Put(NativeExtendedRam.KernelProbe, Kernel);
    if (!mirror) cpu.Put(NativeExtendedRam.ExtendedProbe, extendedWord);
    uint models = 0x8012C000, size = 0x47000;
    cpu.Regs[2] = models; cpu.Regs[31] = 0x8001DEAD; cpu.Regs[1] = 0x8008FFFF; cpu.Regs[16] = 0x51515151;
    if (startupSite)
    {
        cpu.Put(NativeExtendedRam.StartupHook, [JalStartup, 0]);
        cpu.Regs[3] = size; cpu.Regs[4] = 0x80076B90; cpu.Regs[5] = NativeExtendedRam.SceneAddressGlobal;
        cpu.Run(NativeExtendedRam.StartupHook, NativeExtendedRam.StartupStore + 4);
    }
    else
    {
        cpu.Put(NativeExtendedRam.InGameHook, [JalInGame, 0x24630001]);   // its delay slot: addiu v1,v1,1
        cpu.Regs[4] = size; cpu.Regs[3] = 7;
        cpu.Run(NativeExtendedRam.InGameHook, NativeExtendedRam.InGameHook + 8);
    }
    Check(cpu.Regs[16] == 0x51515151 && cpu.Regs[5] == (startupSite ? NativeExtendedRam.SceneAddressGlobal : 0),
        "the check leaves every register but v0, at, the size register and ra as it was");
    return (cpu.Regs[2], startupSite ? cpu.Regs[3] : cpu.Regs[4], cpu.Regs[3], cpu.Get(NativeExtendedRam.SceneAddressGlobal), cpu.Writes);
}
foreach (bool startupSite in new[] { false, true })
{
    string name = startupSite ? "start-up loader" : "in-game loader";
    var ps = Site(startupSite, mirror: true, 0);
    Check(ps.V0 == 0x8012C000 + 0x47000, $"{name}, 2 MB console: the scene stays right after the models (0x{ps.V0:X8})");
    Check(startupSite ? ps.Stored == ps.V0 && ps.Writes.SequenceEqual([NativeExtendedRam.SceneAddressGlobal]) : ps.Writes.Count == 0,
        $"{name}, 2 MB: " + (startupSite ? "the scene address is stored once, at 0x800785E4" : "nothing is written to memory"));
    foreach (uint word in new uint[] { 0, 0x1C200000, 0x04200000, 0xFFFFFFFF })
    {
        var em = Site(startupSite, mirror: false, word);
        Check(em.V0 == GameRam.ExtendedSceneBase, $"{name}, 8 MB (word 0x{word:X8} at 0x80200080): the scene goes to 0x80200000");
        Check(startupSite ? em.Stored == GameRam.ExtendedSceneBase : em.Writes.Count == 0, $"{name}, 8 MB: " + (startupSite ? "stored at 0x800785E4" : "no memory write"));
    }
    var same = Site(startupSite, mirror: false, Kernel);
    Check(same.V0 == 0x8012C000 + 0x47000, $"{name}: memory there that reads exactly like the kernel word counts as a mirror (scene stays low)");
    if (!startupSite) Check(Site(false, true, 0).V1 == 8, "in-game loader: the hook's own delay slot (addiu v1,v1,1) still runs");
}

// ---- 3. Installing: refusals, recognition of its own patch, idempotence (synthetic executables; --source does the real one).
uint Word(byte[] exe, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan((int)(address - UniversalExeBindings.ExeBase)));
void Put(byte[] exe, uint address, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan((int)(address - UniversalExeBindings.ExeBase)), value);
var zero = new byte[0x66000];
Check(Throws<InvalidDataException>(() => NativeExtendedRam.PatchExe(zero), e => e.Message.Contains("level loaders")) && zero.All(b => b == 0),
    "an executable without the retail loader words is refused and left as it was");
var hooksOnly = new byte[0x66000];
foreach (var (at, word) in new (uint, uint)[] { (0x80015B4C, 0x8C635864), (0x80015B50, 0x00441021), (0x80015B54, 0x24630001), (0x80015B58, 0x3C018008),
    (0x80015B5C, 0xAC2285E4), (0x800149B0, 0x8C846B90), (0x800149B4, 0x00431021), (0x800149B8, 0xACA20000), (0x800149BC, 0x3C028007) }) Put(hooksOnly, at, word);
var hooksBefore = hooksOnly.ToArray();
Check(Throws<InvalidDataException>(() => NativeExtendedRam.PatchExe(hooksOnly), e => e.Message.Contains("stores its check")) && hooksOnly.SequenceEqual(hooksBefore),
    "the retail loader words with a different routine at 0x8005D628 are refused and left as they were");
var installed = new byte[0x66000];
cave.CopyTo(installed, (int)(NativeExtendedRam.Cave - UniversalExeBindings.ExeBase));
Put(installed, NativeExtendedRam.InGameHook, JalInGame); Put(installed, NativeExtendedRam.StartupHook, JalStartup); Put(installed, NativeExtendedRam.StartupStore, 0);
var installedBefore = installed.ToArray();
NativeExtendedRam.PatchExe(installed);
Check(NativeExtendedRam.IsInstalled(installed) && installed.SequenceEqual(installedBefore), "an executable with this editor's patch is recognised; installing again changes nothing");
var tampered = installed.ToArray(); Put(tampered, NativeExtendedRam.Cave + 32, 0x3C028021);
Check(Throws<InvalidDataException>(() => NativeExtendedRam.PatchExe(tampered), e => e.Message.Contains("differs from this editor's")), "a changed check is refused");
var storeBack = installed.ToArray(); Put(storeBack, NativeExtendedRam.StartupStore, 0xACA20000);
Check(Throws<InvalidDataException>(() => NativeExtendedRam.PatchExe(storeBack)), "a start-up hook whose store came back is refused");
Check(!NativeExtendedRam.IsInstalled(hooksBefore), "the retail words are not mistaken for an installed patch");

// ---- 4. The budgets. The 2 MB arithmetic is exactly the checks the editor made before (one budget, the whole load);
// with 8 MB what loads before the scene ends below the draw buffers and the scene fits 0x80200000..0x80400000.
const uint Limit = GameRam.LowLimit;
Check(Limit == 0x801877B0 && GameRam.ExtendedSceneBase == 0x80200000 && GameRam.ExtendedSceneTop == 0x80400000 && GameRam.ExtendedSceneCapacity == 0x200000,
    "limit 0x801877B0 (draw buffers 0x80187BB0 less 1 KB); scene room 0x80200000..0x80400000 (2 MB)");
int identical = 0, total = 0;
foreach (uint end in new uint[] { 0x80100000, Limit - 0x10000, Limit - 1, Limit, Limit + 1, Limit + 0x324, Limit + 0x18000, 0x801FF000 })
foreach (uint scene in new uint[] { 0x800, 0x8000, 0x18000, 0x28800 })
foreach (uint original in new uint[] { 0x80150000, Limit, Limit + 0x144, end })
{
    total++;
    bool same = GameRam.Fits(end, scene, false) == (end <= Limit) &&
        GameRam.ModelRoom(end, scene, false) == (int)(Limit - end) && GameRam.SceneRoom(end, scene, false) == (long)Limit - end &&
        GameRam.Shortfall(end, scene, false) == (long)end - Limit &&
        GameRam.Exceeds(end, scene, original, scene, false) == (end > Limit && end > original);
    if (same) identical++;
}
Check(identical == total, $"2 MB: fits, room, shortfall and the no-growth rule equal the old expressions in {identical} of {total} cases");
// A Tree Tops-like level: 4.8 KB left with 2 MB, its 96 KB scene leaves the low memory with 8 MB.
uint tt = 0x801864F4, ttScene = 0x18000;
Check(GameRam.Fits(tt, ttScene, false) && GameRam.ModelRoom(tt, ttScene, false) == 4796, "Tree Tops as shipped: fits 2 MB with 4,796 bytes to spare");
uint grown = tt + 0xC000;
Check(!GameRam.Fits(grown, ttScene, false) && GameRam.Fits(grown, ttScene, true), "48 KB more models: refused with 2 MB, fits with 8 MB");
Check(GameRam.ModelRoom(grown, ttScene, true) == 4796 + 0x18000 - 0xC000 && GameRam.SceneRoom(grown, ttScene, true) == 0x200000 - 0x18000,
    "8 MB: the room below the draw buffers gains the scene's 96 KB; the scene has 2 MB less its own size");
Check(GameRam.Shortfall(grown, ttScene, false) == 0xC000 - 4796 && GameRam.Shortfall(grown, ttScene, true) < 0, "shortfall in each mode");
Check(!GameRam.Fits(Limit + 0x18000 + 1, 0x18000, true) && GameRam.Shortfall(Limit + 0x18000 + 1, 0x18000, true) == 1,
    "8 MB: models one byte past the limit are still refused (models never move)");
Check(!GameRam.Fits(Limit - 0x10000 + 0x200800, 0x200800, true) && GameRam.Shortfall(0x80300000, 0x200800, true) == 0x800,
    "8 MB: a scene larger than 2 MB is refused");
Check(GameRam.Exceeds(grown, ttScene, tt, ttScene, false) && !GameRam.Exceeds(grown, ttScene, tt, ttScene, true) &&
    !GameRam.Exceeds(Limit + 0x144, 0x19000, Limit + 0x144, 0x19000, false), "growth rule: a retail level already past the margin keeps its end");

// ---- 5. The mode: project choice, export scope (flows into tasks), report and the Create BIN sentence.
Check(GameRam.Mode == GameMemoryMode.PlayStation2MB && !GameRam.Extended && GameRam.Current == null, "the default is the PlayStation's 2 MB");
GameRam.SetProjectMode(GameMemoryMode.Emulator8MB);
Check(GameRam.Extended, "the open project's choice applies outside an export");
using (var scope = GameRam.Use(GameMemoryMode.PlayStation2MB))
{
    Check(!GameRam.Extended && GameRam.Current == scope, "an export's scope wins over the project choice");
    Check(NativeExtendedRam.ApplyForExport(installedBefore.ToArray(), _ => [], []).Count == 0 && !scope.Installed, "the 2 MB export step changes nothing");
}
GameRam.SetProjectMode(GameMemoryMode.PlayStation2MB);
using (var outer = GameRam.Use(GameMemoryMode.Emulator8MB))
{
    bool inTask = Task.Run(() => GameRam.Extended).Result;
    bool afterAwait = Task.Run(async () => { await Task.Yield(); return GameRam.Extended; }).Result;
    Check(GameRam.Extended && inTask && afterAwait, "an 8 MB export scope reaches tasks and awaits");
    using (GameRam.Use(GameMemoryMode.PlayStation2MB)) Check(!GameRam.Extended, "a nested scope applies inside");
    Check(GameRam.Extended && GameRam.Current == outer, "and the outer one again after it");
    Check(NativeExtendedRam.ExportNote(outer) == "", "nothing is said before the game is written");
    Check(Throws<InvalidDataException>(() => GameRam.RequirePlayStationSceneAddress("A feature"), e => e.Message.Contains("PlayStation (2 MB)")),
        "features that check the scene address refuse 8 MB in plain words");
    Check(Throws<InvalidDataException>(() => NativeCollectibleSaveSchedulerKernel.CreateFixture(), e => e.Message.Contains("Memory")),
        "the collectible save scheduler (scene address 0x8016F554 in its game code) refuses 8 MB");
}
Check(!GameRam.Extended && GameRam.Current == null, "disposing the scope restores the 2 MB default");
Check(NativeCollectibleSaveSchedulerKernel.CreateFixture().ExpectedGlobals.Any(g => g.Address == 0x800785E4), "with 2 MB it builds as before");
using (var report = GameRam.Use(GameMemoryMode.Emulator8MB))
{
    report.Record(["Tree Tops"]);
    string note = NativeExtendedRam.ExportNote(report);
    Check(report.Installed && note.Contains("needs 8 MB RAM") && note.Contains("Tree Tops does not fit 2 MB") && note.Contains("Enable 8MB RAM"),
        "a level past 2 MB: the game needs 8 MB, named: " + note);
    report.Record([]);
    Check(NativeExtendedRam.ExportNote(report).Contains("also works on a normal PlayStation"), "every level fits 2 MB: the game works on both");
}

// ---- 6. The project setting.
string folder = Directory.CreateTempSubdirectory("spyro-memory-mode-").FullName;
try
{
    string file = GameMemorySettings.PathFor(folder);
    Check(GameMemorySettings.Load(folder) == GameMemoryMode.PlayStation2MB && !File.Exists(file), "no file: PlayStation (2 MB)");
    GameMemorySettings.Save(folder, GameMemoryMode.Emulator8MB);
    Check(GameMemorySettings.Load(folder) == GameMemoryMode.Emulator8MB && File.ReadAllText(file).Contains("\"emulator-8mb\""), "8 MB is saved as memory-mode.json");
    GameMemorySettings.Save(folder, GameMemoryMode.PlayStation2MB);
    Check(!File.Exists(file) && Directory.GetFiles(folder).Length == 0, "choosing 2 MB again removes the file (the project is as it was)");
    File.WriteAllText(file, "{ not json");
    Check(Throws<Exception>(() => GameMemorySettings.Load(folder)) && File.ReadAllText(file) == "{ not json", "a damaged file is reported, not guessed at");
    File.WriteAllText(file, "{\"memory\":\"16mb\"}");
    Check(Throws<InvalidDataException>(() => GameMemorySettings.Load(folder), e => e.Message.Contains("unknown")), "an unknown choice is reported");
}
finally { Directory.Delete(folder, recursive: true); }

bool source = args.Length >= 3 && args[0] == "--source";
if (source) SourceCases.Run(Check, args[1], args[2]);
string mode = source ? "SOURCE" : "FREE";
Console.WriteLine(failed == 0 ? $"EXTENDED_RAM_{mode}_OK checks={checks}" : $"EXTENDED_RAM_{mode}_FAILED {failed} of {checks}");
return failed == 0 ? 0 : 1;
