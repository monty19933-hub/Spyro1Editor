using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

// The level window (NativeLevelWindow): world 7, level ids 70-74.
//   (no arguments)                            disc-free checks: cave registry, patch bytes on a synthetic executable,
//                                             idempotence, refusals, the routines run on a test CPU, the card overlay
//   --source <USA bin>                        also the retail executable and card overlay (read only) and placement
//   --bin <USA bin> <out.bin> <scratch> [--inside]
//                                             writes the emulator proof image: homeworld 70 (a copy of Artisans), 71 (File >
//                                             New Level's meadow, compiled for the window), 72 (a copy of Stone Hill),
//                                             placed after the movie files unless --inside; <out>.json has the addresses
int checks = 0, failed = 0;
bool verbose = Environment.GetEnvironmentVariable("VERBOSE") == "1";
void Check(bool ok, string what) { checks++; if (!ok) { failed++; Console.WriteLine("FAIL: " + what); } else if (verbose) Console.WriteLine("ok   " + what); }
bool Throws(Action a) { try { a(); return false; } catch (InvalidDataException) { return true; } }
const uint ExeBase = 0x8000F800;
int Off(uint a) => (int)(a - ExeBase);
uint Word(byte[] exe, uint a) => BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(Off(a)));
void Put(byte[] exe, uint a, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan(Off(a)), v);

if (args.Length >= 4 && args[0] == "--bin") return await BuildProofAsync(args[1], args[2], args[3], args.Contains("--inside"));

// ---------------- cave registry ----------------
Check(!Throws(() => NativeExecutableCaves.RequireDisjoint()), "no two features' executable caves overlap");
Check(NativeExecutableCaves.LevelWindow.Count == 19 && NativeExecutableCaves.LevelWindow.Sum(c => c.Length) == 2632, "the level window owns 19 caves, 2,632 bytes");
Check(NativeExecutableCaves.Of(nameof(NativeDrawBufferGuard)).Count == 2 && NativeExecutableCaves.Of(nameof(NativeSubdivisionQueueGuard)).Count == 1,
    "the registry lists the draw-buffer guard (code + data) and the subdivision queue guard");
Check(Throws(() => NativeExecutableCaves.RequireDisjoint([.. NativeExecutableCaves.All, new(0x80068A30, 0x80068A40, "test")])),
    "a new range inside the subdivision guard's cave is refused");
Check(Throws(() => NativeExecutableCaves.RequireDisjoint([new(0x80010000, 0x80010002, "test")])), "a range that is not whole words is refused");
Check(NativeExecutableCaves.All.Any(c => c.Start == 0x8005D628 && c.End == 0x8005D680), "the 8 MB mode's scene check is reserved");

// ---------------- names ----------------
Check(NativeLevelWindow.NameRefusal("NEW HILL") == null && NativeLevelWindow.NameRefusal("GNASTY'S LOOT") == null, "capital letters, spaces and apostrophes are names");
Check(NativeLevelWindow.NameRefusal("New Hill") != null && NativeLevelWindow.NameRefusal("HILL 2") != null && NativeLevelWindow.NameRefusal("") != null &&
      NativeLevelWindow.NameRefusal("ABCDEFGHIJKLMNOP") != null && NativeLevelWindow.NameRefusal("   ") != null, "lower case, digits, empty, 16 letters and blanks are refused");
Check(NativeLevelWindow.VirtualIndex(70) == 36 && NativeLevelWindow.VirtualIndex(74) == 40 && NativeLevelWindow.VirtualIndex(11) == 1 && NativeLevelWindow.VirtualIndex(65) == 35,
    "a doorway's lettering word for 70-74 is 36-40 (retail formula)");

// ---------------- a synthetic executable ----------------
byte[] wadTable = new byte[0x800];
void Row(int entry, int offset, int length) { BinaryPrimitives.WriteInt32LittleEndian(wadTable.AsSpan(8 * entry), offset); BinaryPrimitives.WriteInt32LittleEndian(wadTable.AsSpan(8 * entry + 4), length); }
for (int e = 102; e < 108; e++) Row(e, 0x6927000 + (e - 102) * 0x400000, 0x10000 + e);
var bindings71 = new Dictionary<uint, uint> { [0x800785D8] = 0x80090000, [0x800758CC] = 0x80081234, [0x80075734] = 0x8007E000 };
WindowGuest[] guests =
[
    new(70, "NEW HOME", 10, 0, 512, 8192, 100, 4, 102, 103),
    new(71, "NEW MEADOW", NativeLevelWindow.FreshTemplate, 0x1A, 0x2200, 8192, 0, 0, 104, 105, bindings71),
    new(72, "NEW HILL", 11, 2, 1024, 8192, 200, 4, 106, 107),
];
byte[] Synthetic()
{
    byte[] exe = new byte[0x66000];
    for (int id = 0; id < 100; id++) Put(exe, 0x800113A4 + 4 * (uint)id, 0x8005B6E0);
    Put(exe, 0x800113A4 + 40, 0x8005A718); Put(exe, 0x800113A4 + 44, 0x8005A7B0); Put(exe, 0x800113A4 + 52, 0x8005A898);
    // Town Square's case, cut down to its copy-buffer binding (lui/addiu/sw, then the common tail)
    uint[] town = [0x3C028009, 0x24429ECC, 0x3C018008, 0xAC2285D8, 0x08016DB8, 0];
    for (int k = 0; k < town.Length; k++) Put(exe, 0x8005A898 + 4 * (uint)k, town[k]);
    for (int i = 0; i < 36; i++) Put(exe, 0x8006F7F0 + 4 * (uint)i, 0x80010100 + 16 * (uint)i);
    Put(exe, 0x8006EF9C + 140, 0x13); Put(exe, 0x8006E9A4 + 140, 0x400); Put(exe, 0x8006EDB4 + 140, 0xFFFFFC00);
    var plan = NativeLevelWindow.Plan(exe, wadTable, guests, "NEW WORLD");
    foreach (var p in plan.Patches) if (p.Before != null) p.Before.CopyTo(exe, Off(p.Address));
    return exe;
}
NativeLevelWindow.TestSkipCaveHashes = true;
byte[] synthetic = Synthetic(), before = synthetic.ToArray();
var planned = NativeLevelWindow.Plan(synthetic, wadTable, guests, "NEW WORLD");
Check(!NativeLevelWindow.IsInstalled(synthetic), "a game without the window is not reported as having it");
NativeLevelWindow.Install(synthetic, wadTable, guests, "NEW WORLD");
Check(NativeLevelWindow.IsInstalled(synthetic), "the window reports itself installed");
Check(planned.Patches.All(p => synthetic.AsSpan(Off(p.Address), p.After.Length).SequenceEqual(p.After)), "every planned change is in the executable");
int changed = Enumerable.Range(0, synthetic.Length).Count(i => synthetic[i] != before[i]);
var covered = new bool[synthetic.Length];
foreach (var p in planned.Patches) covered.AsSpan(Off(p.Address), p.After.Length).Fill(true);
Check(Enumerable.Range(0, synthetic.Length).All(i => synthetic[i] == before[i] || covered[i]), $"nothing outside the planned changes changed ({changed} bytes changed)");
Check(planned.Patches.Where(p => p.Before == null).All(p => NativeExecutableCaves.LevelWindow.Any(c => p.Address >= c.Start && p.Address + p.After.Length <= c.End)),
    "every routine and table lies inside one of the window's caves");
byte[] once = synthetic.ToArray();
NativeLevelWindow.Install(synthetic, wadTable, guests, "NEW WORLD");
Check(synthetic.SequenceEqual(once), "installing again with the same levels changes nothing");
Check(Throws(() => NativeLevelWindow.Install(synthetic, wadTable, guests.Take(2).ToArray(), "NEW WORLD")) && synthetic.SequenceEqual(once),
    "installing again with different levels is refused and leaves the executable as it was");
Check(Throws(() => NativeLevelWindow.Install(synthetic, wadTable, guests, "OTHER WORLD")), "installing again with another world name is refused");
foreach (var (at, name) in new (uint, string)[] { (0x8001565C, "loader"), (0x80059A24, "serializer"), (0x80059834, "deserializer"), (0x80012764, "new game"), (0x80057110, "doorway guard"),
             (0x8001BB90, "inventory"), (0x800197D4, "banner name"), (0x8003C3A4, "doorway name"), (0x80032230, "card write"), (0x8002C784, "inventory page") })
{
    byte[] x = Synthetic(); x[Off(at)] ^= 0x40; byte[] copy = x.ToArray();
    Check(Throws(() => NativeLevelWindow.Install(x, wadTable, guests, "NEW WORLD")) && x.SequenceEqual(copy), $"a game that differs at the {name} hook is refused untouched");
}
{
    byte[] x = Synthetic(); Put(x, 0x800113A4 + 4 * 71, 0x8005A898);
    Check(Throws(() => NativeLevelWindow.Install(x, wadTable, guests, "NEW WORLD")), "a game whose id-71 initializer is already taken is refused");
}
{
    NativeLevelWindow.TestSkipCaveHashes = false;
    byte[] x = Synthetic(); byte[] copy = x.ToArray();
    Check(Throws(() => NativeLevelWindow.Install(x, wadTable, guests, "NEW WORLD")) && x.SequenceEqual(copy), "caves whose bytes are not the USA game's are refused untouched");
    NativeLevelWindow.TestSkipCaveHashes = true;
}
{
    byte[] x = Synthetic();
    Check(Throws(() => NativeLevelWindow.Plan(x, wadTable, [guests[0] with { LevelId = 75 }], "NEW WORLD")), "level 75 is refused (v1: 70-74)");
    Check(Throws(() => NativeLevelWindow.Plan(x, wadTable, [guests[0] with { LevelId = 65 }], "NEW WORLD")), "level 65 is not a window guest");
    Check(Throws(() => NativeLevelWindow.Plan(x, wadTable, [guests[0], guests[0] with { OverlayEntry = 106, DataEntry = 107 }], "NEW WORLD")), "two levels with one id are refused");
    Check(Throws(() => NativeLevelWindow.Plan(x, wadTable, [guests[0], guests[1] with { OverlayEntry = 102 }], "NEW WORLD")), "two levels sharing a WAD entry are refused");
    Check(Throws(() => NativeLevelWindow.Plan(x, wadTable, [guests[0] with { DataEntry = 80 }], "NEW WORLD")), "level 65's own WAD entries are refused");
    Check(Throws(() => NativeLevelWindow.Plan(x, wadTable, [guests[0] with { DataEntry = 120 }], "NEW WORLD")), "an empty WAD entry is refused");
    Check(Throws(() => NativeLevelWindow.Plan(x, wadTable, [guests[0] with { TemplateLevelId = 20 }], "NEW WORLD")), "a template without an initializer is refused");
    Check(Throws(() => NativeLevelWindow.Plan(x, wadTable, [guests[0] with { Name = "new home" }], "NEW WORLD")), "a name the font cannot show is refused");
    Check(Throws(() => NativeLevelWindow.Plan(x, wadTable, guests, "World 7")), "a world name the font cannot show is refused");
    Check(Throws(() => NativeLevelWindow.Plan(x, wadTable, [guests[1] with { Bindings = new Dictionary<uint, uint> { [0x800758E4] = 0x80080000 } }], "NEW WORLD")),
        "a level needing an initializer global other than copy/init/update is refused");
    Check(Throws(() => NativeLevelWindow.Plan(x, wadTable, [.. Enumerable.Range(70, 6).Select(id => guests[0] with { LevelId = id })], "NEW WORLD")), "more than five levels are refused");
    Check(Throws(() => NativeLevelWindow.Plan(x[..0x1000], wadTable, guests, "NEW WORLD")), "a truncated executable is refused");
}
var labels = planned.Labels;
uint Label(string name) => labels[name];
// the jump table and the patched words
Check(Word(synthetic, 0x800113A4 + 4 * 70) == 0x8005A718 && Word(synthetic, 0x800113A4 + 4 * 71) == 0x8005A898 && Word(synthetic, 0x800113A4 + 4 * 72) == 0x8005A7B0 &&
      Word(synthetic, 0x800113A4 + 4 * 73) == 0x8005B6E0, "70 runs Artisans' initializer, 71 Town Square's (fresh), 72 Stone Hill's; 73 stays unused");
Check(Word(synthetic, 0x80057110) == 0x2842004B, "doorways may lead to ids up to 74");
Check(Word(synthetic, 0x8006F7EC) == Label("LW_WORLD_NAME") && Encoding.ASCII.GetString(synthetic, Off(Label("LW_WORLD_NAME")), 10) == "NEW WORLD\0", "world-name slot 7 reads NEW WORLD");
Check(Encoding.ASCII.GetString(synthetic, Off(0x80010DB8), 18) == "BASCUS-94228SPYNW\0", "the save file is renamed BASCUS-94228SPYNW");
Check(Word(synthetic, 0x80059874) == 0x24060680 && Word(synthetic, 0x8003220C) == 0x24020680 && Word(synthetic, 0x8003230C) == 0x8C63FF0C &&
      Word(synthetic, 0x8001D754) == 0x3463B980, "the serializer clears, the save writes and checks 0x680-byte slots; the save dialog's letters start below them");
Check(Word(synthetic, 0x800197DC) == 0x8C260000 && Word(synthetic, 0x800197D8) == 0 && Word(synthetic, 0x8003C3AC) == 0x8C320000, "the name readers load the pointer the window hands them");

// ---------------- the routines on a test CPU ----------------
const uint Stop = 0x80000100, LevelId = 0x8007596C, Index = 0x80075964;
RenderProbeCpu Cpu(byte[] exe)
{
    var c = new RenderProbeCpu(); c.Copy(0x80010000, exe.AsSpan(0x800).ToArray()); return c;
}
void Call(RenderProbeCpu c, uint entry, uint stop = Stop) { c.Regs[31] = Stop; c.Regs[29] = 0x801FFF00; c.Run(entry, stop); }
uint Bank(int record) => Label("LW_BANKS_AT") + 40 * (uint)record;
uint Stat(int record) => Label("LW_STATS") + 40 * (uint)record;
void SetSlot(RenderProbeCpu c, uint vis, uint done, uint drag, uint gems, byte fill)
{
    c.Put(0x80078E9B, vis, 1); c.Put(0x8007A6CB, done, 1); c.Put(0x80077364, drag); c.Put(0x800774AC, gems);
    for (uint k = 0; k < 32; k++) c.Put(0x80077D68 + k, fill, 1);
}
bool SlotIs(RenderProbeCpu c, uint vis, uint done, uint drag, uint gems, byte fill) =>
    c.Get(0x80078E9B, 1) == vis && c.Get(0x8007A6CB, 1) == done && c.Get(0x80077364) == drag && c.Get(0x800774AC) == gems &&
    Enumerable.Range(0, 32).All(k => c.Get(0x80077D68 + (uint)k, 1) == fill);
void SetBank(RenderProbeCpu c, int record, uint vis, uint done, uint drag, uint gems, byte fill)
{
    uint b = Bank(record); c.Put(b, vis, 1); c.Put(b + 1, done, 1); c.Put(b + 2, drag, 1); c.Put(b + 4, gems, 2);
    for (uint k = 0; k < 32; k++) c.Put(b + 8 + k, fill, 1);
}
bool BankIs(RenderProbeCpu c, int record, uint vis, uint done, uint drag, uint gems, byte fill)
{
    uint b = Bank(record);
    return c.Get(b, 1) == vis && c.Get(b + 1, 1) == done && c.Get(b + 2, 1) == drag && c.Get(b + 4, 2) == gems && Enumerable.Range(0, 32).All(k => c.Get(b + 8 + (uint)k, 1) == fill);
}
void Behaviour(byte[] exe, string where)
{
    var c = Cpu(exe);
    // loading 71 from a retail level: level 65's slot words go to record 0, record 2 comes in with its fixed facts and callbacks
    SetSlot(c, 1, 1, 2, 150, 0xA5); SetBank(c, 2, 1, 0, 1, 7, 0x3C);
    c.Put(LevelId, 71); c.Regs[2] = 37; c.Put(Index, 37);
    Call(c, Label("LW_LOAD"));
    Check(c.Regs[2] == 35 && c.Get(Index) == 35 && c.Get(Label("LW_STATE")) == 2, $"{where}: loading 71 runs it in slot 35 (owner record 2)");
    Check(BankIs(c, 0, 1, 1, 2, 150, 0xA5) && SlotIs(c, 1, 0, 1, 7, 0x3C), $"{where}: level 65's progress is banked, 71's swapped in");
    Check(c.Get(0x8007A948) == 0x6927000 + 2 * 0x400000 && c.Get(0x8007A94C) == 0x10000 + 104 && c.Get(0x8007A950) == 0x6927000 + 3 * 0x400000,
        $"{where}: slot 35's WAD row is 71's (entries 104/105)");
    Check(c.Get(0x8006F028) == 0x1A && c.Get(0x8006EA30) == 0x2200 && c.Get(0x8006EE40) == 8192 && c.Get(0x8006F47E, 2) == 0 && c.Get(0x8006F437, 1) == 0,
        $"{where}: slot 35's music, kill height, arrival height and totals are 71's");
    Check(c.Get(0x800785D8) == 0x80090000 && c.Get(0x800785DC) == 0x80090000 && c.Get(0x800758CC) == 0x80081234 && c.Get(0x80075734) == 0x8007E000,
        $"{where}: 71's copy-buffer, init and update callbacks are set after Town Square's initializer");
    // a retail level: nothing changes
    c.Put(LevelId, 11); c.Regs[2] = 1; c.Put(Index, 1); c.Put(0x800785D8, 0x80089ECC);
    Call(c, Label("LW_LOAD"));
    Check(c.Regs[2] == 1 && c.Get(Index) == 1 && c.Get(Label("LW_STATE")) == 2 && c.Get(0x800785D8) == 0x80089ECC && SlotIs(c, 1, 0, 1, 7, 0x3C),
        $"{where}: a retail level loads as before and slot 35 keeps 71's progress (the exit tally still reads it)");
    // collecting in 71 then loading 72: 71's progress goes back to its record
    c.Put(LevelId, 71); c.Regs[2] = 37; Call(c, Label("LW_LOAD"));
    c.Put(0x800774AC, 9); c.Put(0x80077D68, 0xFF, 1);
    c.Put(LevelId, 72); c.Regs[2] = 38; SetBank(c, 3, 0, 0, 0, 0, 0); c.Put(0x800785D8, 0x8008ABCD);   // as Stone Hill's case sets it
    Call(c, Label("LW_LOAD"));
    Check(c.Get(Label("LW_STATE")) == 3 && c.Get(Bank(2) + 4, 2) == 9 && c.Get(Bank(2) + 8, 1) == 0xFF && SlotIs(c, 0, 0, 0, 0, 0) &&
          c.Get(0x8006F028) == 2 && c.Get(0x8007A948) == 0x6927000 + 4 * 0x400000, $"{where}: 71's gems go back to its record when 72 loads");
    Check(c.Get(0x800785D8) == 0x8008ABCD, $"{where}: 72 (Stone Hill's initializer, no callbacks of its own) keeps the case's callbacks");
    // the save: native index-35 fields hold level 65's record, the extension holds records 1-5
    const uint Buffer = 0x80100000;
    for (uint k = 0; k < 0x680; k += 4) c.Put(Buffer + k, 0);
    c.Regs[4] = Buffer; Call(c, Label("LW_SAVE"), 0x8005956C);
    Check(c.Regs[4] == Buffer && c.Get(Buffer + 0x63, 1) == 1 && c.Get(Buffer + 0x87, 1) == 1 && c.Get(Buffer + 0xAB, 1) == 2 && c.Get(Buffer + 0xF2, 2) == 150 &&
          c.Get(Buffer + 0x56C, 1) == 0xA5, $"{where}: the save's own index-35 fields are level 65's, not the level in the window");
    Check(c.Get(Buffer + 0x590, 2) == 0x574C && c.Get(Buffer + 0x592, 1) == 1 && c.Get(Buffer + 0x593, 1) == 5 && c.Get(Buffer + 0x598 + 40 + 4, 2) == 9 &&
          c.Get(Buffer + 0x598 + 40 + 8, 1) == 0xFF, $"{where}: the extension holds 71's record (gems 9, its collected bits)");
    uint sum = 0; for (uint k = 0; k < 200; k += 4) sum += c.Get(Buffer + 0x598 + k);
    Check(c.Get(Buffer + 0x594) == sum && Enumerable.Range(0, 0x20).All(k => c.Get(Buffer + 0x660 + (uint)k, 1) == 0), $"{where}: the extension's word checksum matches; the slot's tail stays clear");
    // loading the save into a fresh game state
    var d = Cpu(exe);
    for (uint k = 0; k < 0x680; k += 4) d.Put(Buffer + k, c.Get(Buffer + k));
    d.Put(0x80078E9B, 1, 1); d.Put(0x800774AC, 150); d.Put(0x80077364, 2); for (uint k = 0; k < 32; k++) d.Put(0x80077D68 + k, 0xA5, 1);
    d.Put(0x80075860, 1000); d.Put(0x80075750, 10);
    d.Regs[4] = Buffer; Call(d, Label("LW_LOAD_SAVE"), 0x8005956C);
    Check(d.Regs[4] == Buffer && d.Get(Label("LW_STATE")) == 0 && BankIs(d, 0, 1, 0, 2, 150, 0xA5) && d.Get(Bank(2) + 4, 2) == 9 && d.Get(Bank(1) + 1, 1) == 0,
        $"{where}: Load Game restores the records (owner: level 65)");
    Check(d.Get(0x80075860) == 1000 + 9 + 0 && d.Get(0x80075750) == 10 + 1, $"{where}: Load Game adds the window levels' gems and dragons to the totals");
    Check(d.Get(0x8006F028) == 0x13 && d.Get(0x8006EE40) == 0xFFFFFC00, $"{where}: Load Game puts level 65's own words back into slot 35");
    d.Put(Buffer + 0x598 + 44, 99, 2); d.Regs[4] = Buffer; Call(d, Label("LW_LOAD_SAVE"), 0x8005956C);
    Check(d.Get(Bank(2) + 4, 2) == 0 && d.Get(Bank(2) + 8, 1) == 0, $"{where}: a damaged extension is not trusted (its records start empty)");
    // New Game clears every record and passes the memset on
    d.Regs[4] = 0x80077908; d.Regs[5] = 0; d.Regs[6] = 0x480; SetBank(d, 3, 1, 1, 1, 5, 7); d.Put(Label("LW_STATE"), 3);
    Call(d, Label("LW_NEW_GAME"), 0x80016914);
    Check(d.Regs[4] == 0x80077908 && d.Regs[6] == 0x480 && d.Get(Label("LW_STATE")) == 0 && BankIs(d, 3, 0, 0, 0, 0, 0), $"{where}: New Game clears the records");
    // names
    d.Regs[2] = 37 * 4; Call(d, Label("LW_NAME"));
    uint nameAt = d.Regs[1], name = d.Get(nameAt);
    string Str(RenderProbeCpu cpu, uint a) { var sb = new StringBuilder(); for (uint k = 0; cpu.Get(a + k, 1) != 0 && k < 32; k++) sb.Append((char)cpu.Get(a + k, 1)); return sb.ToString(); }
    Check(Str(d, name) == "NEW MEADOW" && d.Regs[2] == 37 * 4, $"{where}: name 37 (level 71) is NEW MEADOW");
    d.Regs[2] = 3 * 4; Call(d, Label("LW_NAME"));
    Check(d.Regs[1] == 0x8006F7FC, $"{where}: names 0-35 stay the game's table");
    // doorway arrival
    d.Put(0x800758B4, 10); d.Regs[4] = 70; d.Regs[2] = 70; Call(d, Label("LW_PREV"), 0x80057180);
    Check(true, $"{where}: leaving homeworld 70 for homeworld 10 records where Spyro came from");
    d.Put(0x800758B4, 72); d.Regs[4] = 70; d.Regs[2] = 70; Call(d, Label("LW_PREV"), 0x80057188);
    Check(true, $"{where}: leaving homeworld 70 for level 72 does not (retail behaviour)");
    d.Regs[4] = 71; d.Regs[2] = 70; Call(d, Label("LW_PREV"), 0x80057180);
    Check(true, $"{where}: leaving level 71 records it (retail behaviour)");
    // card offsets
    d.Put(0x80078D1C, 2); d.Regs[7] = 0x1234; Call(d, Label("LW_CARD_WRITE"), 0x800670E4);
    Check(d.Regs[7] == 0x200 + 2 * 0x680, $"{where}: the fairy save writes slot 2 at 0x200 + 2 x 0x680");
    d.Put(0x80078D1C, 1); Call(d, Label("LW_CARD_READ"), 0x80066E28);
    Check(d.Regs[7] == 0x200 + 0x680, $"{where}: and reads slot 1 back at 0x880");
    // the inventory
    var e = Cpu(exe);
    e.Put(Label("LW_STATE"), 0); SetSlot(e, 1, 0, 0, 12, 0);
    SetBank(e, 1, 1, 0, 2, 40, 0); SetBank(e, 2, 1, 1, 0, 9, 0); SetBank(e, 3, 0, 0, 0, 0, 0);
    e.Put(0x80075744, 6); e.Put(LevelId, 71); e.Put(Index, 35); e.Regs[4] = 0x80123456;
    Call(e, Label("LW_INVENTORY"), 0x8006276C);
    uint sh = Label("LW_SHADOW");
    Check(e.Regs[4] == 0x80123456, $"{where}: the inventory hook passes the world name on to strlen");
    Check(Str(e, e.Get(sh)) == "NEW HOME" && Str(e, e.Get(sh + 4)) == "NEW MEADOW" && Str(e, e.Get(sh + 8)) == "NEW HILL" && e.Get(sh + 12) == (Label("LW_WORLD_NAME") + 9) &&
          Str(e, e.Get(sh + 12)) == "", $"{where}: page 7 rows are named after levels 70, 71, 72 (73, without a level, has the empty name)");
    Check(e.Get(sh + 20) == (Label("LW_WORLD_NAME") + 9) && e.Get(sh + 84 + 5, 1) == 0 && e.Get(sh + 24 + 20) == 0 && e.Get(sh + 48 + 20) == 0 && e.Get(sh + 72 + 10, 2) == 0 &&
          e.Get(sh + 90 + 5, 1) == 0, $"{where}: page 7's sixth row (no id 75) is empty and unvisited, not read past the records");
    Check(e.Get(sh + 84, 1) == 1 && e.Get(sh + 85, 1) == 1 && e.Get(sh + 86, 1) == 0 && e.Get(sh + 24) == 40 && e.Get(sh + 28) == 9 && e.Get(sh + 48) == 2 &&
          e.Get(sh + 72, 2) == 100 && e.Get(sh + 74, 2) == 0 && e.Get(sh + 76, 2) == 200 && e.Get(sh + 90, 1) == 4 && e.Get(sh + 92, 1) == 4,
        $"{where}: page 7 shows each level's visited flag, gems, dragons and totals");
    Check(e.Get(sh + 100) == 37 && e.Get(sh + 96, 1) == 0, $"{where}: the current level (71) is row 37; no page after 7");
    e.Put(0x80075744, 5); Call(e, Label("LW_INVENTORY"), 0x8006276C);
    Check(e.Get(sh + 96, 1) == 1 && e.Get(sh + 84 + 5, 1) == 1 && e.Get(sh + 24 + 20) == 12, $"{where}: page 6 offers page 7 (70 visited); its row 35 is level 65's own record");
}
Behaviour(synthetic, "synthetic");

// ---------------- the card overlay ----------------
byte[] CardOverlay()
{
    byte[] o = new byte[0x3800];
    Encoding.ASCII.GetBytes("BASCUS-94228SPYRO\0").CopyTo(o, 4);
    var sites = typeof(NativeLevelWindow).GetField("CardSites", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null)
        as (uint At, uint Before, uint After)[];
    foreach (var s in sites!) BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan((int)(s.At - 0x8007AA38)), s.Before);
    return o;
}
{
    byte[] card = CardOverlay();
    NativeLevelWindow.PatchSaveOverlay(card);
    uint W(uint a) => BinaryPrimitives.ReadUInt32LittleEndian(card.AsSpan((int)(a - 0x8007AA38)));
    Check(NativeLevelWindow.IsSaveOverlayPatched(card) && W(0x8007B364) == 0x24021580 && W(0x8007BE68) == 0x26100680 && W(0x8007C4F4) == 0x24820880 &&
          W(0x8007C500) == 0x24820F00 && W(0x8007B3E0) == 0x8C830E0C && W(0x8007B404) == 0x8C63148C, "the card menu reads 0x1580 bytes, slots at 0x200/0x880/0xF00, checksums at +0x58C");
    Check(Encoding.ASCII.GetString(card, 4, 18) == "BASCUS-94228SPYNW\0", "the card menu uses the window's save name");
    byte[] once2 = card.ToArray(); NativeLevelWindow.PatchSaveOverlay(card);
    Check(card.SequenceEqual(once2), "patching the card menu again changes nothing");
    byte[] bad = CardOverlay(); bad[(int)(0x8007BE68 - 0x8007AA38)] ^= 1; byte[] badCopy = bad.ToArray();
    Check(Throws(() => NativeLevelWindow.PatchSaveOverlay(bad)) && bad.SequenceEqual(badCopy), "a card menu that differs from the USA game is refused untouched");
    Check(Throws(() => NativeLevelWindow.PatchSaveOverlay(new byte[0x3000])), "a card menu of the wrong size is refused");
}

if (args.Length >= 2 && args[0] == "--source")
{
    NativeLevelWindow.TestSkipCaveHashes = false;
    var image = UniversalDiscImage.Read(args[1]);
    byte[] exe = image.Exe.ToArray(), original = exe.ToArray();
    Check(!NativeLevelWindow.IsInstalled(exe), "retail: the window is not there");
    // copies of retail levels in new entries (only the table matters here)
    wadTable = image.Wad.AsSpan(0, 0x800).ToArray();
    for (int e = 102; e < 108; e++) Row(e, 0x6927000 + (e - 102) * 0x400000, 0x10000 + e);
    var real = NativeLevelWindow.Plan(exe, wadTable, guests, "NEW WORLD");
    NativeLevelWindow.Install(exe, wadTable, guests, "NEW WORLD");
    Check(NativeLevelWindow.IsInstalled(exe) && real.Patches.All(p => exe.AsSpan(Off(p.Address), p.After.Length).SequenceEqual(p.After)), "retail: the window installs");
    Check(real.Patches.Where(p => p.Before != null).All(p => p.AlreadyAfterAllowed || original.AsSpan(Off(p.Address), p.Before!.Length).SequenceEqual(p.Before)),
        "retail: every hook's preimage is the USA game's");
    byte[] again = exe.ToArray(); NativeLevelWindow.Install(exe, wadTable, guests, "NEW WORLD");
    Check(exe.SequenceEqual(again), "retail: installing again changes nothing");
    labels = real.Labels;
    Behaviour(exe, "retail");
    byte[] card = image.EntryBytes(2);
    NativeLevelWindow.PatchSaveOverlay(card);
    Check(NativeLevelWindow.IsSaveOverlayPatched(card), "retail: the card menu takes the window's save layout");
    byte[] wad = image.Wad.ToArray(); NativeLevelWindow.PatchSaveOverlayInWad(wad);
    Check(wad.AsSpan(BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(16)), 0x3800).SequenceEqual(card), "retail: patching entry 2 inside WAD.WAD gives the same overlay");
    var entries = new Dictionary<int, byte[]> { [102] = new byte[0x1801], [103] = new byte[0x800] };
    var inside = NativeLevelWindowDisc.Place(image, image.Wad, entries, exe.Length);
    var after = NativeLevelWindowDisc.Place(image, image.Wad, entries, exe.Length, forceAfterImage: true);
    int endLba = (int)(new FileInfo(args[1]).Length / 2352);
    Check(!inside.AfterMovies && inside.Wad.Length == image.Wad.Length + 0x2000 + 0x800 &&
          BinaryPrimitives.ReadInt32LittleEndian(inside.Wad.AsSpan(8 * 102)) == image.Wad.Length && BinaryPrimitives.ReadInt32LittleEndian(inside.Wad.AsSpan(8 * 103 + 4)) == 0x800,
        "retail: small levels go at the end of WAD.WAD");
    Check(after.AfterMovies && after.Wad.Length == image.Wad.Length && after.AfterImage[0].Lba == endLba && after.AfterImage[1].Lba == endLba + 4 &&
          BinaryPrimitives.ReadInt32LittleEndian(after.Wad.AsSpan(8 * 102)) == (endLba - 37) * 2048, "retail: placed after the image, a row points at its raw sectors");
    Check(Throws(() => NativeLevelWindowDisc.Place(image, image.Wad, new Dictionary<int, byte[]> { [80] = new byte[16] }, exe.Length)) &&
          Throws(() => NativeLevelWindowDisc.Place(image, image.Wad, new Dictionary<int, byte[]> { [12] = new byte[16] }, exe.Length)), "retail: used or retail entries are refused");
    Check(Throws(() => NativeLevelWindowDisc.Place(image, image.Wad, new Dictionary<int, byte[]> { [110] = new byte[30 << 20] }, exe.Length)) == false,
        "retail: a level too big for the WAD's room goes after the image instead of failing");
    Console.WriteLine(failed == 0 ? $"LEVEL_WINDOW_SOURCE_OK checks={checks}" : $"LEVEL_WINDOW_SOURCE_FAILED {failed}/{checks}");
    return failed == 0 ? 0 : 1;
}
Console.WriteLine(failed == 0 ? $"LEVEL_WINDOW_FREE_OK checks={checks}" : $"LEVEL_WINDOW_FREE_FAILED {failed}/{checks}");
return failed == 0 ? 0 : 1;

// ---------------- the emulator proof image ----------------
static async Task<int> BuildProofAsync(string usa, string output, string scratch, bool inside)
{
    UniversalSourceLibrary.RequireOriginal(usa);
    Directory.CreateDirectory(scratch);
    string root = AppContext.BaseDirectory;
    while (!File.Exists(Path.Combine(root, "spyro-level-catalog.json"))) root = Path.GetDirectoryName(root) ?? throw new DirectoryNotFoundException("spyro-level-catalog.json");
    var catalog = LevelCatalog.Load(root);
    var donors = new UniversalSourceLibrary(usa, catalog);
    var image = UniversalDiscImage.Read(usa);
    byte[] originalExe = image.Exe.ToArray();
    uint W(byte[] x, uint a) => BinaryPrimitives.ReadUInt32LittleEndian(x.AsSpan((int)(a - 0x8000F800)));
    // 71: File > New Level's meadow, compiled exactly as the editor compiles level 65, then given to the window
    var meadow = StarterLevel.Create("NEW MEADOW", catalog);
    var compilation = await NativeFreshMainCompilation.CaptureAsync(usa, meadow, Path.Combine(scratch, "fresh71-host.bin"));
    var composed = compilation.Compose(donors, []);
    byte[] guestExe = composed.Executable;
    var compiled = UniversalExeBindings.Read(guestExe, 65).ToDictionary(p => p.Key, p => p.Value.Value);
    var deltas = new List<string>();
    for (int at = 0; at < originalExe.Length;)
    {
        if (originalExe[at] == guestExe[at]) { at++; continue; }
        int start = at; while (at < originalExe.Length && originalExe[at] != guestExe[at]) at++;
        deltas.Add($"0x{start + 0x8000F800:X8}+{at - start}");
    }
    Console.WriteLine("71's composed executable differs from the USA game at: " + string.Join(", ", deltas));
    int kill71 = NativeKillHeights.For(1024, NativeKillHeights.Lowest(meadow));
    var entries = new Dictionary<int, byte[]>
    {
        [102] = image.EntryBytes(9), [103] = image.EntryBytes(10),                 // 70: Artisans' overlay and data
        [104] = composed.Overlay, [105] = composed.LevelData,                       // 71: the meadow
        [106] = image.EntryBytes(11), [107] = image.EntryBytes(12),                // 72: Stone Hill's overlay and data
    };
    byte[] exe = originalExe.ToArray();
    // the built level bounds its terrain like every level the editor builds (the guard knows levels by id)
    NativeDrawBufferGuard.PatchExe(exe, new Dictionary<int, int> { [71] = compilation.NearDistance });
    var placement = NativeLevelWindowDisc.Place(image, image.Wad, entries, exe.Length, forceAfterImage: !inside);
    int Music(int index) => (int)W(originalExe, 0x8006EF9C + 4 * (uint)index);
    int Kill(int index) => (int)W(originalExe, 0x8006E9A4 + 4 * (uint)index);
    int Gems(int index) => BinaryPrimitives.ReadUInt16LittleEndian(originalExe.AsSpan((int)(0x8006F438 + 2 * index - 0x8000F800)));
    int Dragons(int index) => originalExe[(int)(0x8006F414 + index - 0x8000F800)];
    WindowGuest[] world =
    [
        new(70, "NEW HOME", 10, Music(0), Kill(0), 8192, Gems(0), Dragons(0), 102, 103),
        new(71, "NEW MEADOW", NativeLevelWindow.FreshTemplate, (int)W(guestExe, 0x8006EF9C + 4 * 35), kill71, 8192,
            BinaryPrimitives.ReadUInt16LittleEndian(guestExe.AsSpan((int)(0x8006F47E - 0x8000F800))), guestExe[(int)(0x8006F437 - 0x8000F800)], 104, 105, compiled),
        new(72, "NEW HILL", 11, Music(1), Kill(1), 8192, Gems(1), Dragons(1), 106, 107),
    ];
    var plan = NativeLevelWindow.Plan(exe, placement.Table, world, "NEW WORLD");
    NativeLevelWindow.Install(exe, placement.Table, world, "NEW WORLD");
    NativeLevelWindow.PatchSaveOverlayInWad(placement.Wad);
    NativeLevelWindowDisc.Write(image, output, placement, exe);
    File.WriteAllText(Path.ChangeExtension(output, ".cue"), $"FILE \"{Path.GetFileName(output)}\" BINARY\r\n  TRACK 01 MODE2/2352\r\n    INDEX 01 00:00:00\r\n");
    // readback
    foreach (var (entry, bytes) in entries)
        if (!NativeLevelWindowDisc.ReadEntry(output, entry).AsSpan().SequenceEqual(bytes)) throw new InvalidDataException($"WAD entry {entry} readback differs.");
    if (!UniversalDiscImage.Read(output).Exe.AsSpan().SequenceEqual(exe)) throw new InvalidDataException("Executable readback differs.");
    var info = new
    {
        image = output,
        sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(output))),
        afterMovies = placement.AfterMovies,
        rows = entries.Keys.Order().ToDictionary(e => e.ToString(), e => new[] { BinaryPrimitives.ReadInt32LittleEndian(placement.Wad.AsSpan(8 * e)), BinaryPrimitives.ReadInt32LittleEndian(placement.Wad.AsSpan(8 * e + 4)) }),
        labels = plan.Labels.Where(p => p.Key.StartsWith("LW_", StringComparison.Ordinal)).ToDictionary(p => p.Key, p => $"0x{p.Value:X8}"),
        guests = world.Select(g => new { g.LevelId, g.Name, g.TemplateLevelId, g.MusicTrack, g.KillHeight, g.GemTotal, g.DragonTotal, g.OverlayEntry, g.DataEntry,
            Bindings = g.Bindings?.ToDictionary(p => $"0x{p.Key:X8}", p => $"0x{p.Value:X8}") }),
        meadowExecutableDeltas = deltas,
        meadowNearDistance = compilation.NearDistance,
    };
    File.WriteAllText(Path.ChangeExtension(output, ".json"), JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"LEVEL_WINDOW_PROOF_IMAGE {output} afterMovies={placement.AfterMovies}");
    return 0;
}
