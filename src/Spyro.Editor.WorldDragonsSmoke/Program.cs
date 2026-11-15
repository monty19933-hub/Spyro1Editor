using System.Buffers.Binary;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

// Dragons in a new world (2.6): dragons of a level of the game stand in a level of the new world as guests
// (UniversalDragonGuests) and the game's dragon totals follow the dragons its levels hold (NativeDragonTotals).
//   (no arguments)        source-free: the totals' arithmetic, the completion percentage run instruction by instruction
//                         for 60-99 dragons with 12-20 eggs, the dragons counted in made-up levels, the doorway to
//                         Gnasty's Loot (the game's own test and the dragonfly eggs'), the limits
//   --source <USA bin>    on the USA game (read only): its 79 statues and 80 dragons where the game counts them, every
//                         statue's rescue scene, nothing written for the game as it is, dragons added and taken out
//                         (levels swapped) with and without egg thieves changed, the guests' plan and its refusals, and
//                         a meadow composed with Stone Hill's and High Caves' dragons as a level of a new world
//   --survey <USA bin>    every level's dragons as the game has them (read only, prints)
//   --inspect <game bin>  the dragons in the levels of a game the editor made (prints)
int checks = 0, failed = 0;
bool verbose = Environment.GetEnvironmentVariable("VERBOSE") == "1";
void Check(bool ok, string what) { checks++; if (!ok) { failed++; Console.WriteLine("FAIL " + what); } else if (verbose) Console.WriteLine("ok   " + what); }
bool Throws(Action a, string containing)
{
    try { a(); return false; }
    catch (InvalidDataException e) { if (!e.Message.Contains(containing, StringComparison.Ordinal)) { Console.WriteLine("  (message: " + e.Message + ")"); return false; } return true; }
}
int I(byte[] b, int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
static string Root()
{
    for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        if (File.Exists(Path.Combine(d.FullName, "spyro-level-catalog.json"))) return d.FullName;
    throw new InvalidOperationException("The repository root (spyro-level-catalog.json) was not found.");
}
int Finish(string marker)
{
    if (failed > 0) { Console.WriteLine($"WORLD_DRAGONS_FAILED checks={checks} failed={failed}"); return 1; }
    Console.WriteLine($"{marker} checks={checks}");
    return 0;
}

if (args.Length >= 2 && args[0] == "--survey")
{
    var catalog = LevelCatalog.Load(Root());
    var lib = new UniversalSourceLibrary(args[1], catalog);
    byte[] exe = lib.Disc.Exe;
    int total = 0;
    var keys = catalog.Levels.Where(l => l.LevelId is >= 10 and <= 64 && l.LevelId % 10 <= 4).OrderBy(l => l.LevelId).ToList();
    foreach (var level in keys)
    {
        var (_, data, overlay) = lib.Get(level.Key);
        int rows = data.Scene().RowCount;
        var sf = data.Subfiles();
        int index = (level.LevelId / 10 - 1) * 6 + level.LevelId % 10;
        int table = exe[0x5FC14 + index];
        var regs = data.Registrations();
        var statue = regs.FirstOrDefault(r => r.ClassId == 250);
        var sw = UniversalDispatchAnalysis.FindUpdateSwitch(overlay);
        string Handled(int c) => UniversalDispatchAnalysis.Handles(overlay, sw, c) ? "code" : "-";
        string Model(int c) => regs.Any(r => r.ClassId == c) ? $"model {data.Package(c).Length}" : lib.SharedClasses.Contains(c) ? "shared" : "-";
        Console.WriteLine($"{level.LevelId} {level.Key}: rows {rows}, subfiles {sf.Count} (streams {Math.Max(0, sf.Count - 4)}: {string.Join(",", sf.Skip(4).Select(f => f.Length))}), table {table}, models base..statue {(statue == null ? -1 : statue.Root - sf[2].Offset)} of {sf[2].Length}");
        Console.WriteLine($"   0xFA {Handled(0xFA)}/{Model(0xFA)}  0x6E {Handled(0x6E)}/{Model(0x6E)}  0xFB {Handled(0xFB)}/{Model(0xFB)}  0x14B {Handled(0x14B)}/{Model(0x14B)}  0x14C {Handled(0x14C)}/{Model(0x14C)}  0x14D {Handled(0x14D)}/{Model(0x14D)}");
        Console.WriteLine($"   registration order: {string.Join(" ", regs.OrderBy(r => r.Root).Select(r => $"{r.ClassId:X}"))}");
        for (int r = 0; r < rows; r++)
        {
            byte[] row = data.Row(r);
            int c = BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(0x36));
            if (c != 0xFA) continue;
            var p = data.CaptureRow(level.Key, r).Properties;
            total++;
            var family = UniversalDragonRows.Family(data, level.Key, r);
            {
                int number = I(p, 0x18);
                if (number >= 0 && number < sf.Count - 4)
                {
                    byte[] stream = data.Bytes.AsSpan(sf[4 + number].Offset, sf[4 + number].Length).ToArray();
                    int prefix = I(stream, 12);
                    var models = NativeDragonRescueCamera.Models(stream);
                    var (trackAt, trackLength) = NativeDragonRescueCamera.Track(stream);
                    Console.WriteLine($"      scene {number}: {stream.Length} bytes, voice {I(stream, 8)}, dragon model {models[0].Length}, Spyro's {models[1].Length}, camera {trackLength / 24} frames, films this statue {NativeDragonRescueCamera.Films(stream, p)}, room needed {Math.Max(UniversalLevelData.Align(prefix, 2048), UniversalLevelData.Align(stream.Length - prefix, 2048))}");
                }
                else Console.WriteLine($"      scene {number}: NOT IN THE LEVEL");
            }
            Console.WriteLine($"   statue row {r} at ({I(row, 0x0C) / 16},{I(row, 0x10) / 16},{I(row, 0x14) / 16}) yaw {row[0x46]} props {p.Length}: stream {(p.Length >= 0x1C ? I(p, 0x18) : -9)} +1C {(p.Length >= 0x20 ? I(p, 0x1C) : -9)} pedestal {(p.Length >= 0x24 ? I(p, 0x20) : -9)} +24 {(p.Length >= 0x28 ? I(p, 0x24) : -9)} +28 {(p.Length >= 0x2C ? I(p, 0x28) : -9)} dist {(p.Length >= 0x30 ? I(p, 0x2C) : -9)} +30 {(p.Length >= 0x34 ? I(p, 0x30) : -9)} +34 {(p.Length >= 0x38 ? I(p, 0x34) : -9)} name {(p.Length >= 0x3C ? I(p, 0x38) : -9)} family [{string.Join(",", family.Select(f => $"{f}:{BinaryPrimitives.ReadUInt16LittleEndian(data.Row(f).AsSpan(0x36)):X}"))}]");
        }
    }
    Console.WriteLine($"statues {total}");
    // Which levels' dragon code is the same as which (classes compared with the editor's own equivalence).
    foreach (int c in new[] { 0xFA, 0x6E, 0xFB, 0x14B, 0x14C, 0x14D })
    {
        List<List<string>> groups = [];
        foreach (var level in keys)
        {
            var (_, data, overlay) = lib.Get(level.Key);
            var sw = UniversalDispatchAnalysis.FindUpdateSwitch(overlay);
            if (!UniversalDispatchAnalysis.Handles(overlay, sw, c) && !data.Registrations().Any(r => r.ClassId == c)) continue;
            var home = groups.FirstOrDefault(g => { var (_, d2, o2) = lib.Get(g[0]); return UniversalSourceIdentity.Equivalent(d2, o2, data, overlay, c, lib.SharedClasses, out _); });
            if (home == null) groups.Add([level.Key]); else home.Add(level.Key);
        }
        Console.WriteLine($"class 0x{c:X}: {groups.Count} kinds: " + string.Join(" | ", groups.Select(g => string.Join(",", g))));
        if (groups.Count > 1)
        {
            var (_, d1, o1) = lib.Get(groups[0][0]);
            foreach (var g in groups.Skip(1))
            {
                var (_, d2, o2) = lib.Get(g[0]);
                UniversalSourceIdentity.Equivalent(d1, o1, d2, o2, c, lib.SharedClasses, out string why);
                Console.WriteLine($"     {groups[0][0]} vs {g[0]}: {why}");
            }
        }
    }
    // The same, by code alone (the models are per-level copies textured from each level's own pictures).
    foreach (int c in new[] { 0xFA, 0x6E, 0xFB, 0x14B, 0x14C, 0x14D })
    {
        List<(string Hash, List<string> Keys, int Bytes)> groups = [];
        var reference = lib.Get("townsquare").Overlay;
        foreach (var level in keys)
        {
            var (_, data, overlay) = lib.Get(level.Key);
            var sw = UniversalDispatchAnalysis.FindUpdateSwitch(overlay);
            if (!UniversalDispatchAnalysis.Handles(overlay, sw, c)) continue;
            string hash; int bytes = 0;
            try { byte[] code = UniversalBehaviorBundleBuilder.Build(reference, [("identity", overlay, [c])], 0x80100000).Code; hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(code))[..12]; bytes = code.Length; }
            catch (InvalidDataException e) { hash = "unbuilt: " + e.Message; }
            var home = groups.FirstOrDefault(g => g.Hash == hash);
            if (home.Keys == null) groups.Add((hash, [level.Key], bytes)); else home.Keys.Add(level.Key);
        }
        Console.WriteLine($"code 0x{c:X}: {groups.Count} kinds: " + string.Join(" | ", groups.Select(g => $"[{g.Hash} {g.Bytes}b] " + string.Join(",", g.Keys))));
    }
    return 0;
}
if (args.Length >= 2 && args[0] == "--inspect")
{
    // --inspect <game bin> [first entry]: the dragons in the levels of a game the editor made (its world's levels are WAD
    // entries 102 on: overlay, data; the game's own 9+2i, 10+2i).
    string bin = args[1];
    byte[] exe = UniversalDiscImage.Read(bin).Exe;
    void Level(string name, int overlayEntry, int dataEntry)
    {
        byte[] bytes;
        try { bytes = NativeLevelWindowDisc.ReadEntry(bin, dataEntry); } catch (Exception e) when (e is InvalidDataException or ArgumentException or IndexOutOfRangeException) { return; }
        if (bytes.Length < 0x1000) return;
        var data = new UniversalLevelData(bytes);
        var sf = data.Subfiles(); int rows = data.Scene().RowCount;
        var regs = data.Registrations().OrderBy(r => r.Root).ToList();
        var statue = regs.FirstOrDefault(r => r.ClassId == 250);
        int statues = Enumerable.Range(0, rows).Count(r => BinaryPrimitives.ReadUInt16LittleEndian(data.Row(r).AsSpan(0x36)) == 0xFA);
        if (statues == 0 && sf.Count <= 4) { Console.WriteLine($"{name}: no dragons ({rows} rows)"); return; }
        Console.WriteLine($"{name}: {rows} rows, {statues} statues, scenes {string.Join(",", sf.Skip(4).Select(f => f.Length))}, models {sf[2].Length} (below the statue {(statue == null ? -1 : statue.Root - sf[2].Offset)}), scene part {sf[3].Length}, spare slots {UniversalMobyComposer.ArenaSlots(data)}");
        Console.WriteLine($"   models in order: {string.Join(" ", regs.Select(r => $"{r.ClassId:X}"))}");
        for (int r = 0; r < rows; r++)
        {
            byte[] row = data.Row(r);
            int c = BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(0x36));
            if (c is not (0xFA or 0x6E or 0x14B or 0x14C or 0x14D)) continue;
            int props = I(row, 0);
            byte[] p = props > 0 ? data.Bytes.AsSpan(sf[3].Offset + props, 0x54).ToArray() : new byte[0x54];
            string more = c == 0xFA ? $"scene {I(p, 0x18)} pedestal row {I(p, 0x20)} name {I(p, 0x38)} camera ({I(p, 0) / 16},{I(p, 4) / 16},{I(p, 8) / 16})"
                : c == 0x6E ? $"statue row {I(p, 0)} pedestal row {I(p, 4)}" : "";
            Console.WriteLine($"   row {r} class {c:X} at ({I(row, 0x0C) / 16},{I(row, 0x10) / 16},{I(row, 0x14) / 16}) {more}");
        }
        for (int n = 4; n < sf.Count; n++)
        {
            byte[] stream = data.Bytes.AsSpan(sf[n].Offset, sf[n].Length).ToArray();
            var (at, length) = NativeDragonRescueCamera.Track(stream);
            Console.WriteLine($"   scene {n - 4}: {stream.Length} bytes, first camera ({I(stream, at) / 16},{I(stream, at + 4) / 16},{I(stream, at + 8) / 16})");
        }
    }
    var catalog = LevelCatalog.Load(Root());
    foreach (var level in catalog.Levels.Where(l => l.LevelId is >= 10 and <= 64 && l.LevelId % 10 <= 4).OrderBy(l => l.LevelId))
    {
        byte[] original = UniversalDiscImage.Read(bin).EntryBytes(level.SourceWadEntry);
        if (args.Length > 2 && args[2] == "all") Level(level.DisplayName, level.SourceWadEntry - 1, level.SourceWadEntry);
    }
    for (int e = 102; e < 112; e += 2) Level($"world level in entries {e}/{e + 1}", e, e + 1);
    {
        // The level window's records (found through its New Game hook: lui t0; jal; addiu t0,t0 names them).
        uint Word(uint a) => BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan((int)(a - 0x8000F800)));
        uint hook = Word(0x80012764);
        if (hook >> 26 == 3 && hook != 0x0C005A45)
        {
            uint routine = 0x80000000 | (hook & 0x03FFFFFF) << 2, stats = 0;
            for (uint a = routine; a < routine + 0x60; a += 4)
                if (Word(a) >> 16 == 0x3C08 && Word(a + 4) >> 26 == 3 && Word(a + 8) >> 16 == 0x2508)
                    stats = (Word(a) & 0xFFFF) * 0x10000 + (uint)(short)(Word(a + 8) & 0xFFFF);
            if (stats != 0)
                for (int record = 1; record <= 5; record++)
                {
                    var r = exe.AsSpan((int)(stats - 0x8000F800) + 40 * record, 40);
                    if (BinaryPrimitives.ReadInt32LittleEndian(r[4..]) == 0) continue;
                    Console.WriteLine($"level window record {record} (level {69 + record}): {r[37]} dragons, {BinaryPrimitives.ReadUInt16LittleEndian(r[34..])} treasure");
                }
        }
        var totals = NativeDragonTotals.Read(exe, UniversalDiscImage.Read(bin).EntryBytes(NativeDragonTotals.OverlayEntry(30)));
        Console.WriteLine($"the doorway to Gnasty's Loot asks for {totals.Gate} dragons; the percentage divides by {totals.Divisor}");
    }
    Console.WriteLine("dragon totals of the game's 36 places: " + string.Join(" ", exe.AsSpan(0x5FC14, 36).ToArray()) + $" (sum {exe.AsSpan(0x5FC14, 36).ToArray().Sum(b => b)})");
    Console.WriteLine("look-at guard: " + (NativeLookAtGuard.IsGuarded(exe) ? "in the game (a look-at command with nothing to look at is passed over)" : NativeLookAtGuard.IsOwn(exe) ? "not in the game (the game's own words)" : "other words there"));
    return 0;
}
if (args.Length >= 2 && args[0] == "--source") return await SourceAsync(args[1]);
if (args.Length != 0) { Console.Error.WriteLine("usage: WorldDragonsSmoke [--source <USA.bin> | --survey <USA.bin> | --inspect <game.bin> [all]]"); return 2; }

// ---------------- the totals' arithmetic ----------------
Check(NativeDragonTotals.RetailPerLevel.Count == 36 && NativeDragonTotals.RetailPerLevel.Sum() == 80, "the game's table of dragons per level adds up to eighty");
Check(NativeDragonTotals.Divisor(0, 80) == 12000 && NativeDragonTotals.Divisor(0, 81) == 12060 && NativeDragonTotals.Divisor(1, 81) == 12160 && NativeDragonTotals.Divisor(0, 76) == 11760,
    "the percentage divides by 12,000, 60 more a dragon past eighty and 100 more an egg past twelve");
Check(NativeDragonTotals.MaxTotal == 99 && UniversalDragonGuests.MaxStatues == 6 && UniversalDragonGuests.RescueSpareSlots == 64, "the limits: 99 dragons in a game, 6 in a level, 64 spare object slots for a rescue");
Check(UniversalDragonGuests.SpareSlotsNeeded(0) == 64 && UniversalDragonGuests.SpareSlotsNeeded(10) == 74 && UniversalDragonGuests.SpareSlotsNeeded(27) == 91 && UniversalDragonGuests.SpareSlotsNeeded(200) == 91,
    "spare slots for a rescue: its 48 objects, one for everything that drops something and 16 more; never more than the game's own levels keep (91)");
Check(UniversalDragonGuests.SpareSlotRefusal("LEVEL ONE", Made.Level(2, others: 10), 66) == null && UniversalDragonGuests.SpareSlotRefusal("LEVEL ONE", Made.Level(2, others: 10), 65) is { } full &&
      full.Contains("66 must stay free, 65 would", StringComparison.Ordinal) && full.Contains("Take 1 objects out", StringComparison.Ordinal), "a level too full for a rescue and what its objects drop says so with the numbers");
foreach (int eggs in new[] { 12, 13, 20 })
    for (int total = 60; total <= NativeDragonTotals.MaxTotal; total++)
    {
        Check(NativeDragonTotals.Percent(12000, total, eggs, total, eggs) == 100, $"everything collected is 100% with {total} dragons and {eggs} eggs");
        Check(NativeDragonTotals.Percent(12000, total - 1, eggs, total, eggs) == 99, $"one dragon short is 99% with {total} dragons and {eggs} eggs");
        Check(NativeDragonTotals.Percent(14000, total, eggs, total, eggs) == 120, $"Gnasty's Loot treasure still counts past 100 with {total} dragons");
    }

// ---------------- made-up levels: statues counted ----------------
Check(NativeDragonTotals.StatuesIn(Made.Level(0)) == 0 && NativeDragonTotals.StatuesIn(Made.Level(3)) == 3 && NativeDragonTotals.StatuesIn(Made.Level(6, others: 40)) == 6, "a level's dragons are its statues");
Check(NativeDragonTotals.StatuesIn(Made.Level(2, hidden: 1)) == 2, "a statue put out of the way is not in the level");
Check(NativeDragonTotals.StatuesIn(new byte[64]) == 0 && NativeDragonTotals.StatuesIn([]) == 0, "what is no level has no dragons");

// ---------------- a made-up game: the totals written ----------------
{
    var game = new Made.Game();
    // The game as it is: nothing written.
    byte[] exe = game.Exe(); Dictionary<int, byte[]> replaced = [];
    Check(NativeDragonTotals.ApplyForExport(exe, game.Exe(), replaced, game.Entry, game.Original) == null && exe.AsSpan().SequenceEqual(game.Exe()) && replaced.Count == 0,
        "a game with its own eighty dragons: nothing written");
    // A new world with three levels of 2, 0 and 1 dragons: 83.
    game.Levels[103] = Made.Level(2); game.Levels[105] = Made.Level(0); game.Levels[107] = Made.Level(1);
    var applied = NativeDragonTotals.ApplyForExport(exe, game.Exe(), replaced, game.Entry, game.Original, [103, 105, 107]);
    var read = NativeDragonTotals.Read(exe, replaced.TryGetValue(69, out var gate) ? gate : game.Entry(69));
    Check(applied is { Total: 83, TableChanged: false, PercentageChanged: true, GateChanged: true } && applied.Count.Beyond.SequenceEqual([2, 0, 1]), "a world's three dragons: 83 in the game");
    Check(read.Gate == 83 && read.Divisor == 12180 && read.PerLevel.SequenceEqual(NativeDragonTotals.RetailPerLevel.Select(v => (byte)v)), "the doorway asks 83, the percentage divides by 12,180, the game's own levels keep their numbers");
    Check(replaced.Keys.SequenceEqual([69]) && replaced[69].Where((b, i) => b != game.Entry(69)[i]).Count() == 1, "one byte of Gnasty's World's doorway changes");
    uint[] routine = Made.Words(exe, NativeEggTotals.Percentage, NativeEggTotals.PercentageWords);
    foreach (var (gems, dragons, eggs) in new[] { (0, 0, 0), (12000, 83, 12), (12000, 82, 12), (12000, 80, 12), (5000, 41, 6), (12001, 83, 12), (14000, 83, 12), (100, 1, 1) })
        Check(Mips.Run(routine, NativeEggTotals.Percentage, gems, dragons, eggs) == (uint)NativeDragonTotals.Percent(gems, dragons, eggs, 83), $"the routine for 83 dragons at {gems}/{dragons}/{eggs}");
    Check(Mips.Run(routine, NativeEggTotals.Percentage, 12000, 83, 12) == 100 && Mips.Run(routine, NativeEggTotals.Percentage, 12000, 80, 12) == 98, "83 of 83 is 100%; the game's own eighty alone are 98%");
    // Again: the same.
    byte[] again = [.. exe]; Dictionary<int, byte[]> replacedAgain = new(replaced);
    var second = NativeDragonTotals.ApplyForExport(again, game.Exe(), replacedAgain, game.Entry, game.Original, [103, 105, 107]);
    Check(second is { Total: 83, TableChanged: false, PercentageChanged: false, GateChanged: false } && again.AsSpan().SequenceEqual(exe), "applying again changes nothing");
    // The look-at guard goes into a game with a dragon in a level the editor added, once.
    Check(applied is { LookAtGuarded: true } && NativeLookAtGuard.IsGuarded(exe) && NativeLookAtGuard.IsOwn(game.Exe()) && second is { LookAtGuarded: false },
        "the world's dragons bring the look-at guard (written once)");
    {
        // Without the world's levels the same executable gets the game's own words back.
        byte[] back = [.. exe]; Dictionary<int, byte[]> r = new(replaced);
        var undone = NativeDragonTotals.ApplyForExport(back, game.Exe(), r, game.Entry, game.Original);
        Check(undone is { Total: 80, LookAtGuarded: true } && NativeLookAtGuard.IsOwn(back) && NativeDragonTotals.Read(back, r[69]) is { Gate: 80, Divisor: 12000 }, "the world taken out again: eighty dragons and the game's own look-at words");
    }
    // Every count from 60 to 99 (a world of up to four levels, or levels of the game rebuilt without their dragons):
    // the routine as written gives the arithmetic.
    for (int total = 60; total <= NativeDragonTotals.MaxTotal; total++)
    {
        var g = new Made.Game(); byte[] e = g.Exe(); Dictionary<int, byte[]> r = [];
        List<int> beyond = [];
        if (total > 80)
            for (int left = total - 80, entry = 103; left > 0; left -= Math.Min(6, left), entry += 2) { g.Levels[entry] = Made.Level(Math.Min(6, left)); beyond.Add(entry); }
        else
            // Places emptied in turn until that many are left (the last one keeps what remains).
            for (int place = 0, left = 80 - total; left > 0 && place < 36; place++)
            {
                int had = NativeDragonTotals.RetailPerLevel[place];
                if (had == 0 || place == 30) continue;
                int take = Math.Min(had, left);
                g.Changed[NativeDragonTotals.DataEntry(place)] = Made.Level(had - take, others: 50);
                left -= take;
            }
        var a = NativeDragonTotals.ApplyForExport(e, g.Exe(), r, g.Entry, g.Original, beyond);
        if (total == 80) { Check(a == null && e.AsSpan().SequenceEqual(g.Exe()), "eighty: nothing written"); continue; }
        uint[] code = Made.Words(e, NativeEggTotals.Percentage, NativeEggTotals.PercentageWords);
        Check(a?.Total == total && NativeDragonTotals.Read(e, r[69]).Gate == total && NativeDragonTotals.Read(e, r[69]).PerLevel.Sum(b => b) == Math.Min(total, 80), $"{total} dragons: the doorway asks {total}");
        Check(Mips.Run(code, NativeEggTotals.Percentage, 12000, total, 12) == 100 && Mips.Run(code, NativeEggTotals.Percentage, 12000, total - 1, 12) == 99, $"{total} dragons: all of them is 100%, one short 99%");
    }
    // Levels of the game rebuilt: each line in the inventory follows its statues.
    {
        var g = new Made.Game(); byte[] e = g.Exe(); Dictionary<int, byte[]> r = [];
        g.Changed[NativeDragonTotals.DataEntry(1)] = Made.Level(6);                    // Stone Hill's place with six statues (it had four)
        g.Changed[NativeDragonTotals.DataEntry(2)] = Made.Level(0);                    // Dark Hollow's place without dragons (it had three)
        g.Changed[NativeDragonTotals.DataEntry(30)] = Made.Level(1, others: 9);        // Gnasty's World changed, its statue kept: still two dragons
        g.Changed[NativeDragonTotals.DataEntry(8)] = Made.Level(3, others: 9);         // Cliff Town changed, its three kept
        var a = NativeDragonTotals.ApplyForExport(e, g.Exe(), r, g.Entry, g.Original);
        var t = NativeDragonTotals.Read(e, r[69]);
        Check(a is { Total: 79, TableChanged: true } && t.PerLevel[1] == 6 && t.PerLevel[2] == 0 && t.PerLevel[30] == 2 && t.PerLevel[8] == 3 && t.Gate == 79 && t.Divisor == 11940,
            "levels rebuilt with more, fewer and the same statues: each line follows, the game has 79");
        Check(a is { LookAtGuarded: true } && NativeLookAtGuard.IsGuarded(e), "a level of the game rebuilt with dragons brings the look-at guard");
        Check(e.Where((b, i) => b != g.Exe()[i] && !Look.Within(i)).Count() <= 2 + 4 * NativeEggTotals.PercentageWords, "the executable changes only in the table, the percentage and the guard");
        // Levels rebuilt without dragons only: no guard.
        var bare = new Made.Game(); byte[] eb = bare.Exe(); Dictionary<int, byte[]> rb = [];
        bare.Changed[NativeDragonTotals.DataEntry(2)] = Made.Level(0);
        Check(NativeDragonTotals.ApplyForExport(eb, bare.Exe(), rb, bare.Entry, bare.Original) is { Total: 77, LookAtGuarded: false } && NativeLookAtGuard.IsOwn(eb), "a level rebuilt without dragons: no guard");
    }
    // Too many.
    {
        var g = new Made.Game(); g.Levels[103] = Made.Level(6); g.Levels[105] = Made.Level(6); g.Levels[107] = Made.Level(6); g.Levels[109] = Made.Level(2);
        Check(Throws(() => NativeDragonTotals.ApplyForExport(g.Exe(), g.Exe(), new Dictionary<int, byte[]>(), g.Entry, g.Original, [103, 105, 107, 109]), "a game can hold 99"), "a hundred dragons are refused in plain words");
        g.Levels[109] = Made.Level(1);
        Check(NativeDragonTotals.ApplyForExport(g.Exe(), g.Exe(), new Dictionary<int, byte[]>(), g.Entry, g.Original, [103, 105, 107, 109])?.Total == 99, "ninety-nine are not");
    }
    // The percentage the egg thieves left (13 eggs) and the one the dragonfly eggs use.
    {
        var g = new Made.Game(); g.Levels[103] = Made.Level(4);
        byte[] e = g.Exe(); Made.Put(e, NativeEggTotals.Percentage, NativeEggTotals.PercentageRoutine(13)); Made.Put(e, NativeEggTotals.RowBound, [0x2A62000D]);
        Dictionary<int, byte[]> r = [];
        var a = NativeDragonTotals.ApplyForExport(e, g.Exe(), r, g.Entry, g.Original, [103]);
        uint[] code = Made.Words(e, NativeEggTotals.Percentage, NativeEggTotals.PercentageWords);
        Check(a is { Total: 84, PercentageChanged: true } && NativeDragonTotals.Read(e, r[69]).Divisor == 12340, "with thirteen eggs and 84 dragons the percentage divides by 12,340");
        Check(Mips.Run(code, NativeEggTotals.Percentage, 12000, 84, 13) == 100 && Mips.Run(code, NativeEggTotals.Percentage, 12000, 84, 12) == 99 && Mips.Run(code, NativeEggTotals.Percentage, 12000, 83, 13) == 99, "all of both is 100%, one of either short 99%");
        Check(code.Where((w, i) => w != NativeEggTotals.PercentageRoutine(13)[i]).Count() == 1, "only the divisor differs from the egg thieves' routine");
        byte[] fly = g.Exe(); byte[] dragonfly = UniversalEggProgress.PercentageRoutine(5, 13);
        dragonfly.CopyTo(fly, (int)(NativeEggTotals.Percentage - UniversalExeBindings.ExeBase)); Made.Put(fly, NativeEggTotals.RowBound, [0x2A62000D]);
        Dictionary<int, byte[]> r2 = [];
        var b = NativeDragonTotals.ApplyForExport(fly, g.Exe(), r2, g.Entry, g.Original, [103]);
        uint[] flyCode = Made.Words(fly, NativeEggTotals.Percentage, dragonfly.Length / 4), flyOwn = Made.Words(dragonfly, 0, dragonfly.Length / 4, 0);
        Check(b is { Total: 84, PercentageChanged: true } && (flyCode[11] & 0xFFFF) == 84 && (flyCode[40] & 0xFFFF) == 12000 + 100 * 6 + 240, "with dragonfly eggs: 84 dragons asked for, the divisor grown by 240");
        Check(flyCode.Where((w, i) => w != flyOwn[i]).Count() == 2 && (flyOwn[11] & 0xFFFF) == 80 && (flyOwn[40] & 0xFFFF) == 12600, "only those two words differ from the dragonfly eggs' routine");
        byte[] strange = g.Exe(); Made.Put(strange, NativeEggTotals.Percentage, [0x12345678]);
        Check(Throws(() => NativeDragonTotals.ApplyForExport(strange, g.Exe(), new Dictionary<int, byte[]>(), g.Entry, g.Original, [103]), "is not the USA game's own"), "a percentage the editor does not know is refused");
    }
}

// ---------------- the doorway to Gnasty's Loot ----------------
{
    byte[] own = Made.GnastysWorld();
    int at = NativeDragonTotals.LootGateWord(own);
    Check(at == (int)(NativeDragonTotals.LootGate - UniversalOverlay.Base) && NativeDragonTotals.SetLootGate(own, 80) == null, "the game's own doorway asks eighty: left alone");
    Check(NativeDragonTotals.SetLootGate(own, 85) is { } more && BinaryPrimitives.ReadUInt32LittleEndian(more.AsSpan(at)) == 0x28420055 && more.Where((b, i) => b != own[i]).Count() == 1, "85 dragons: one byte of its test changes");
    Check(NativeDragonTotals.LootGateWord(new byte[at + 64]) == -1 && NativeDragonTotals.SetLootGate(new byte[at + 64], 85) == null, "an overlay without the doorway is left alone");
    // The egg thieves' own word of the same doorway is untouched, and the eggs' pass still finds its own.
    Check(NativeEggTotals.SetLootGate(NativeDragonTotals.SetLootGate(own, 85)!, 13) is { } both && NativeDragonTotals.Read(Made.Game.Blank(), both).Gate == 85 && NativeEggTotals.Read(Made.Game.Blank(), both).Gate == 13,
        "dragons and eggs set the same doorway side by side");
    // With dragonfly eggs the doorway's test was rewritten: the dragons' word is found there, and the eggs' pass
    // still knows its own rewritten test with the dragons set.
    int portal = (int)(UniversalEggUnlocks.PortalAddress - UniversalOverlay.Base);
    byte[] rewritten = new byte[portal + 0x80]; UniversalEggUnlocks.PortalGate().CopyTo(rewritten, portal);
    Check(NativeDragonTotals.LootGateWord(rewritten) == portal + 32 && NativeDragonTotals.SetLootGate(rewritten, 84) is { } set && (BinaryPrimitives.ReadUInt32LittleEndian(set.AsSpan(portal + 32)) & 0xFFFF) == 84 &&
          set.Where((b, i) => b != rewritten[i]).Count() == 1, "with dragonfly eggs the rewritten doorway asks for the dragons too");
    byte[] withDragons = NativeDragonTotals.SetLootGate(rewritten, 84)!;
    Check(NativeEggTotals.SetLootGate(withDragons, 14) is { } eggsToo && eggsToo.AsSpan(portal, 72).SequenceEqual(UniversalEggUnlocks.PortalGate(14)),
        "the eggs' pass still knows its rewritten doorway after the dragons were set (it writes its own, the dragons are set again after it)");
    Check(NativeEggTotals.SetLootGate(withDragons, 12) == null, "and leaves it alone when the eggs are as they were");
}

// ---------------- the look-at guard ----------------
{
    // The ten words run both ways (v0 0x80000000, a2 the camera's mode word, v1 what to look at, a0 how).
    const uint target = 0x80201234, how = 0x80078668;
    var own = Look.Run(NativeLookAtGuard.Own, target, how); var guarded = Look.Run(NativeLookAtGuard.Guarded, target, how);
    (uint, uint)[] told = [(0x80076EA4, 0), (0x80076E90, 0x8000000A), (0x80076EA0, target), (0x80076EA8, how)];
    Check(own.Exit == NativeLookAtGuard.Onward && own.Stores.SequenceEqual(told), "the game's own look-at words: the camera gets its mode, what to look at and how, then on");
    Check(guarded.Exit == NativeLookAtGuard.Onward && guarded.Stores.SequenceEqual(told), "the guard with something to look at: the same four words written, the same way on");
    var nothing = Look.Run(NativeLookAtGuard.Guarded, 0, 0); var ownNothing = Look.Run(NativeLookAtGuard.Own, 0, 0);
    Check(nothing.Exit == NativeLookAtGuard.Onward && nothing.Stores.Count == 0, "the guard with nothing to look at: the camera is told nothing, on as before");
    Check(ownNothing.Stores.Contains((0x80076EA0, 0u)) && ownNothing.Stores.Contains((0x80076E90, 0x8000000Au)), "the game's own words tell the camera to look at nothing (the stop after a rescue)");
    Check(NativeLookAtGuard.Own.Count == 10 && NativeLookAtGuard.Guarded.Count == 10 && NativeLookAtGuard.Site + 4 * 10 == 0x80040248, "ten words for ten: nothing moves around them");
    byte[] exe = Made.Game.Blank(); byte[] first = [.. exe];
    Check(NativeLookAtGuard.IsOwn(exe) && !NativeLookAtGuard.IsGuarded(exe) && NativeLookAtGuard.Apply(exe) && NativeLookAtGuard.IsGuarded(exe) && !NativeLookAtGuard.Apply(exe), "written over the game's own words, once");
    Check(Enumerable.Range(0, exe.Length).Where(i => exe[i] != first[i]).All(Look.Within), "nothing outside the ten words changes");
    Check(NativeLookAtGuard.Restore(exe) && exe.AsSpan().SequenceEqual(first) && !NativeLookAtGuard.Restore(exe), "taken out again: the game's own words");
    byte[] strange = Made.Game.Blank(); Made.Put(strange, NativeLookAtGuard.Site + 8, [0x12345678]); byte[] kept = [.. strange];
    Check(!NativeLookAtGuard.Apply(strange) && !NativeLookAtGuard.Restore(strange) && strange.AsSpan().SequenceEqual(kept), "words that are not the game's own are left alone");
    Check(!NativeLookAtGuard.Apply(new byte[16]) && !NativeLookAtGuard.IsGuarded(new byte[16]), "what is no executable is left alone");
}
return Finish("WORLD_DRAGONS_FREE_OK");

// =====================================================================================================================
async Task<int> SourceAsync(string usa)
{
    var catalog = LevelCatalog.Load(Root());
    var donors = new UniversalSourceLibrary(usa, catalog);
    var disc = donors.Disc; byte[] exe = disc.Exe;
    int Off(uint a) => (int)(a - UniversalExeBindings.ExeBase);
    var levels = catalog.Levels.Where(l => l.LevelId is >= 10 and <= 64 && l.LevelId % 10 <= 4).OrderBy(l => l.LevelId).ToList();

    // ---- the game's dragons where the game counts them ----
    int statues = 0;
    foreach (var level in levels)
    {
        var data = donors.Get(level.Key).Data; int index = (level.LevelId / 10 - 1) * 6 + level.LevelId % 10;
        var own = LevelCompletionCheck.OriginalRows(data).Dragons; statues += own.Count;
        Check(NativeDragonTotals.StatuesIn(data.Bytes) == own.Count && own.Count == NativeDragonTotals.RetailPerLevel[index] - (level.Key == "gnastysworld" ? 1 : 0),
            $"{level.DisplayName}: {own.Count} statues for the {NativeDragonTotals.RetailPerLevel[index]} dragons the game counts there");
        Check(exe[Off(NativeDragonTotals.LevelTotals) + index] == NativeDragonTotals.RetailPerLevel[index], $"{level.DisplayName}: the game's table");
        var scenes = data.Subfiles().Skip(4).ToList();
        foreach (int row in own)
        {
            byte[] p = data.CaptureRow(level.Key, row).Properties;
            int number = p.Length >= 0x1C ? I(p, 0x18) : -1;
            bool ok = p.Length == 84 && I(p, 0x24) == -1 && number >= 0 && number < scenes.Count;
            Check(ok && NativeDragonRescueCamera.Films(data.Bytes.AsSpan(scenes[number].Offset, scenes[number].Length), p), $"{level.DisplayName} row {row}: its statue names its rescue scene, filmed from the statue's camera");
            int room = UniversalDragonGuests.SceneRoom(data, level.Key, row);
            Check(room is >= 0x8000 and <= 0x48000 && room % 2048 == 0, $"{level.DisplayName} row {row}: its scene needs {room} bytes while it plays");
            var family = UniversalDragonRows.Family(data, level.Key, row);
            Check(family.Count == 3 && family[0] == row, $"{level.DisplayName} row {row}: a statue, a pedestal and a fairy");
        }
        Check(scenes.Count <= UniversalDragonGuests.MaxStatues, $"{level.DisplayName}: at most six rescue scenes");
    }
    Check(statues == 79, "79 statues in the game (Gnasty's World's stands for two dragons)");
    for (int index = 0; index < 36; index++)
        if (levels.All(l => (l.LevelId / 10 - 1) * 6 + l.LevelId % 10 != index)) Check(NativeDragonTotals.RetailPerLevel[index] == 0 && exe[Off(NativeDragonTotals.LevelTotals) + index] == 0, $"place {index} (a flight level or none) has no dragons");
    var count = NativeDragonTotals.Count(disc.EntryBytes, disc.EntryBytes);
    Check(count.IsRetail && count.Total == 80, "the game's eighty dragons counted from its levels");
    var read = NativeDragonTotals.Read(exe, disc.EntryBytes(69));
    Check(read.Gate == 80 && read.Divisor == 12000 && read.PerLevel.Sum(b => b) == 80, "the game's doorway (80) and percentage (12,000)");
    {
        byte[] copy = [.. exe]; Dictionary<int, byte[]> none = [];
        Check(NativeDragonTotals.ApplyForExport(copy, exe, none, disc.EntryBytes, disc.EntryBytes) == null && copy.AsSpan().SequenceEqual(exe) && none.Count == 0, "the unedited game: nothing written");
    }

    // ---- dragons added and taken out: a level's data swapped for another's ----
    byte[] Swapped(int e, Dictionary<int, int> swap) => disc.EntryBytes(swap.TryGetValue(e, out int other) ? other : e);
    uint[] Routine(byte[] e) => Made.Words(e, NativeEggTotals.Percentage, NativeEggTotals.PercentageWords);
    {
        // Dark Hollow's place (3 dragons) holds Stone Hill's data (4 statues): 81.
        Dictionary<int, int> swap = new() { [NativeDragonTotals.DataEntry(2)] = NativeDragonTotals.DataEntry(1) };
        byte[] e = [.. exe]; Dictionary<int, byte[]> r = [];
        var a = NativeDragonTotals.ApplyForExport(e, exe, r, x => Swapped(x, swap), disc.EntryBytes);
        var t = NativeDragonTotals.Read(e, r.TryGetValue(69, out var g) ? g : disc.EntryBytes(69));
        Check(a is { Total: 81, TableChanged: true, PercentageChanged: true, GateChanged: true } && t.PerLevel[2] == 4 && t.Gate == 81 && t.Divisor == 12060, "a dragon more in Dark Hollow's place: 81 everywhere");
        Check(r.Keys.SequenceEqual([69]) && e.Where((b, i) => b != exe[i] && !Look.Within(i)).Count() <= 1 + 4 * NativeEggTotals.PercentageWords, "only the table, the percentage, the doorway and the look-at guard change");
        Check(NativeLookAtGuard.IsOwn(exe) && a is { LookAtGuarded: true } && NativeLookAtGuard.IsGuarded(e), "the game has the look-at words the guard knows; a changed level with dragons brings the guard");
        var random = new Random(11);
        for (int i = 0; i < 200; i++)
        {
            int gems = i < 4 ? new[] { 0, 12000, 12001, 14000 }[i] : random.Next(0, 14001), dragons = random.Next(0, 82), eggs = random.Next(0, 13);
            Check(Mips.Run(Routine(e), NativeEggTotals.Percentage, gems, dragons, eggs) == (uint)NativeDragonTotals.Percent(gems, dragons, eggs, 81), $"the percentage for 81 dragons at {gems}/{dragons}/{eggs}");
        }
        Check(Mips.Run(Routine(e), NativeEggTotals.Percentage, 12000, 81, 12) == 100, "81 of 81 is 100%");
        // The eggs' pass over it: Stone Hill's data in Dark Hollow's place brings Stone Hill's thief with it (13 eggs).
        Dictionary<int, byte[]> r2 = new(r);
        var eggsAfter = NativeEggTotals.ApplyForExport(e, exe, r2, x => Swapped(x, swap));
        Check(eggsAfter?.Total == 13 && NativeEggTotals.Read(e, r2[69]).Divisor == 12100, "the eggs' pass after the dragons': it counts its 13 eggs and writes its own routine");
        var dragonsAgain = NativeDragonTotals.ApplyForExport(e, exe, r2, x => Swapped(x, swap), disc.EntryBytes);
        var both = NativeDragonTotals.Read(e, r2[69]);
        Check(dragonsAgain is { Total: 81 } && both.Divisor == 12160 && both.Gate == 81 && NativeEggTotals.Read(e, r2[69]).Gate == 13, "then the dragons' again: 12,160, the doorway asks 81 dragons and 13 eggs");
        Check(Mips.Run(Routine(e), NativeEggTotals.Percentage, 12000, 81, 13) == 100 && Mips.Run(Routine(e), NativeEggTotals.Percentage, 12000, 80, 13) == 99 && Mips.Run(Routine(e), NativeEggTotals.Percentage, 12000, 81, 12) == 99,
            "81 dragons and 13 eggs: all of both is 100%");
        // Both passes once more: nothing moves.
        byte[] before = [.. e];
        NativeEggTotals.ApplyForExport(e, exe, r2, x => Swapped(x, swap));
        NativeDragonTotals.ApplyForExport(e, exe, r2, x => Swapped(x, swap), disc.EntryBytes);
        Check(e.AsSpan().SequenceEqual(before), "both passes again: the same executable");
        // Back to the game's own levels on that executable: the game's own routine, table and doorway.
        Dictionary<int, byte[]> r3 = new(r2);
        NativeEggTotals.ApplyForExport(e, exe, r3, disc.EntryBytes);
        NativeDragonTotals.ApplyForExport(e, exe, r3, disc.EntryBytes, disc.EntryBytes);
        Check(e.AsSpan().SequenceEqual(exe) && r3[69].AsSpan().SequenceEqual(disc.EntryBytes(69)), "back to the game's own levels: the game's own executable and doorway");
    }
    {
        // Stone Hill's place holds Gnasty Gnorc's data (no statues): 76.
        Dictionary<int, int> swap = new() { [NativeDragonTotals.DataEntry(1)] = NativeDragonTotals.DataEntry(33) };
        byte[] e = [.. exe]; Dictionary<int, byte[]> r = [];
        var a = NativeDragonTotals.ApplyForExport(e, exe, r, x => Swapped(x, swap), disc.EntryBytes);
        var t = NativeDragonTotals.Read(e, r[69]);
        Check(a is { Total: 76 } && t.PerLevel[1] == 0 && t.Gate == 76 && t.Divisor == 11760 && Mips.Run(Routine(e), NativeEggTotals.Percentage, 12000, 76, 12) == 100, "Stone Hill's place without dragons: 76 everywhere, all of them 100%");
        Check(a is { LookAtGuarded: false } && NativeLookAtGuard.IsOwn(e), "no dragon in a changed level: no look-at guard");
    }
    {
        // A new world's levels (here: copies of the game's level data) are counted past the 36 places.
        byte[] e = [.. exe]; Dictionary<int, byte[]> r = new() { [103] = disc.EntryBytes(NativeDragonTotals.DataEntry(0)), [105] = disc.EntryBytes(NativeDragonTotals.DataEntry(14)) };
        var a = NativeDragonTotals.ApplyForExport(e, exe, r, disc.EntryBytes, disc.EntryBytes, [103, 105]);
        Check(a is { Total: 87, TableChanged: false } && a.Count.Beyond.SequenceEqual([4, 3]) && NativeDragonTotals.Read(e, r[69]).Gate == 87, "a world with Artisans' four and High Caves' three: 87, the game's own table as it was");
        Check(a is { LookAtGuarded: true } && NativeLookAtGuard.IsGuarded(e), "a world's dragons bring the look-at guard");
        // No jump of the game lands inside the words the guard rewrites (all ten are reached from the top only).
        int text = exe.Length / 4, into = 0;
        for (int k = 0x200; k < text; k++)
        {
            uint w = BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(4 * k)), at = UniversalExeBindings.ExeBase + (uint)(4 * k), op = w >> 26; uint? to = null;
            if (op is 4 or 5 or 6 or 7 || (op == 1 && (w >> 16 & 31) is 0 or 1 or 16 or 17)) to = (uint)(at + 4 + ((short)(w & 0xFFFF) << 2));
            else if (op is 2 or 3) to = (at & 0xF0000000) | ((w & 0x03FFFFFF) << 2);
            if (to is { } t && t > NativeLookAtGuard.Site && t < NativeLookAtGuard.Site + 40 && (at < NativeLookAtGuard.Site - 0x18 || at >= NativeLookAtGuard.Site + 40)) into++;
        }
        Check(into == 0, "no jump of the game lands inside the look-at words");
    }

    // ---- the guests' plan ----
    UniversalMobyPlacement[] Dragon(string key, int row, int x, int y, int z = 544) =>
        [.. UniversalPlacementGroups.Expand(donors.Get, new UniversalMobyPlacement(key, row, x * 16, y * 16, z * 16))];
    int Statue(string key, int nth) => LevelCompletionCheck.OriginalRows(donors.Get(key).Data).Dragons[nth];
    int ClassOf(string key, int row) => BinaryPrimitives.ReadUInt16LittleEndian(donors.Get(key).Data.Row(row).AsSpan(0x36));
    var stoneHill = Dragon("stonehill", Statue("stonehill", 0), 2300, 2900).Concat(Dragon("stonehill", Statue("stonehill", 3), 2300, 2300)).ToArray();
    {
        var plan = UniversalDragonGuests.Plan("LEVEL ONE", stoneHill, donors)!;
        Check(plan.SourceKey == "stonehill" && plan.Statues == 2 && plan.Streams.Count == 2 && plan.Released.SetEquals([0xFA, 0x6E, 0xFB]) && plan.Dependencies.SequenceEqual([(0xFB, "stonehill")]),
            "Stone Hill's two dragons: two scenes, the statue, fairy and splinters under their own numbers");
        var source = donors.Get("stonehill").Data;
        var planned = plan.Placements.Where(x => ClassOf("stonehill", x.SourceRowIndex) == 0xFA).ToList();
        for (int n = 0; n < 2; n++)
        {
            var p = planned[n];
            byte[] row = source.Row(p.SourceRowIndex), properties = source.CaptureRow("stonehill", p.SourceRowIndex).Properties;
            Check(p.PropertyWordOverrides != null && p.PropertyWordOverrides[UniversalDragonGuests.StreamWord] == n, $"statue {n + 1} names scene {n}");
            byte[] moved = NativeDragonRescueCamera.Move(properties.ToArray(), row, p.X, p.Y, p.Z, p.Yaw ?? row[0x46]);
            Check(NativeDragonRescueCamera.Films(plan.Streams[n], moved) && !NativeDragonRescueCamera.Films(plan.Streams[n], properties), $"scene {n} is filmed where statue {n + 1} now stands");
            var original = source.Subfiles()[4 + I(properties, 0x18)];
            Check(NativeDragonRescueCamera.SameButCamera(source.Bytes.AsSpan(original.Offset, original.Length), plan.Streams[n]), $"scene {n} is the dragon's own but for the camera");
        }
        Check(plan.Placements.Count == stoneHill.Length && plan.Placements.Count(p => p.PropertyWordOverrides != null) == 2, "the other objects are as they were given");
        Check(UniversalDragonGuests.Plan("ARTISANS", stoneHill, donors, "stonehill") == null, "in a level built on their own level's place they are not guests");
        Check(UniversalDragonGuests.Plan("LEVEL ONE", [new UniversalMobyPlacement("stonehill", 0, 0, 0, 0)], donors) == null, "a level without dragons has no plan");
        Check(Throws(() => UniversalDragonGuests.Plan("LEVEL ONE", [.. stoneHill, .. Dragon("highcaves", Statue("highcaves", 0), 2600, 2600)], donors), "all come from one level of the game"), "dragons of two levels are refused in plain words");
        Check(Throws(() => UniversalDragonGuests.Plan("LEVEL ONE", stoneHill.Take(2).ToArray(), donors), "is not whole"), "a dragon without its fairy or pedestal is refused");
        var seven = Enumerable.Range(0, 7).SelectMany(n => Dragon("darkpassage", Statue("darkpassage", n % 5), 2200 + 150 * n, 2300)).ToArray();
        Check(Throws(() => UniversalDragonGuests.Plan("LEVEL ONE", seven, donors), "a level can hold 6"), "seven dragons are refused");
        Check(UniversalDragonGuests.Plan("LEVEL ONE", seven.Take(18).ToArray(), donors) is { Statues: 6, Streams.Count: 6 }, "six are not (one of them twice: each statue has its own copy of the scene)");
    }

    // ---- composed: a meadow as a level of a new world with guests ----
    string scratch = Path.Combine(Path.GetTempPath(), "spyro-world-dragons-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(scratch);
    try
    {
        var meadow = StarterLevel.Create("LEVEL ONE", catalog) with { WorldLevelId = 71 };
        var compilation = await NativeFreshMainCompilation.CaptureAsync(usa, meadow, Path.Combine(scratch, "world-71-host.bin"));
        void Composed(string what, UniversalMobyPlacement[] placements, string key, int dragons, int pedestalClass)
        {
            var result = compilation.Compose(donors, placements);
            var data = new UniversalLevelData(result.LevelData);
            var sf = data.Subfiles(); var regs = data.Registrations().OrderBy(r => r.Root).ToList();
            Check(sf.Count == 4 + dragons && NativeDragonTotals.StatuesIn(result.LevelData) == dragons, $"{what}: {dragons} statues and as many rescue scenes");
            if (verbose) Console.WriteLine($"   {what}: models {string.Join(" ", regs.Select(r => $"{r.ClassId:X}@{r.Root - sf[2].Offset}"))} of {sf[2].Length}");
            Check(regs.FindIndex(r => r.ClassId == 0xFA) >= 0 && regs.FindIndex(r => r.ClassId == 0xFB) > regs.FindIndex(r => r.ClassId == 0xFA), $"{what}: the splinters' model comes after the statue's (the scene loads below the statue's)");
            var budget = UniversalNativeDragonRescuePrefix.MeasureGuests(what, data, result.Executable);
            Check(budget is { Deficit: 0 } && budget.StreamCount == dragons && budget.RequiredPrefix > 0 && budget.AvailablePrefix >= budget.RequiredPrefix, $"{what}: room below the statue's model for the largest scene ({budget?.AvailablePrefix} of {budget?.RequiredPrefix})");
            int rows = data.Scene().RowCount, seen = 0;
            for (int r = 0; r < rows; r++)
            {
                byte[] row = data.Row(r); int c = BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(0x36));
                byte[] p = I(row, 0) > 0 ? data.Bytes.AsSpan(sf[3].Offset + I(row, 0), 0x54).ToArray() : new byte[0x54];
                int Class(int other) => other >= 0 && other < rows ? BinaryPrimitives.ReadUInt16LittleEndian(data.Row(other).AsSpan(0x36)) : -1;
                if (c == 0xFA)
                {
                    int scene = I(p, 0x18), pedestal = Class(I(p, 0x20));
                    Check(scene == seen && pedestal > 0 && pedestal != 0xFA && pedestal != 0x6E && I(p, 0x24) == -1, $"{what}: statue {seen + 1} names scene {seen} and its pedestal's row");
                    Check(scene >= 0 && scene < dragons && NativeDragonRescueCamera.Films(data.Bytes.AsSpan(sf[4 + scene].Offset, sf[4 + scene].Length), p), $"{what}: scene {seen} starts at statue {seen + 1}'s camera");
                    Check(Math.Abs(I(p, 0) - I(row, 0x0C)) < 0x4000 && Math.Abs(I(p, 4) - I(row, 0x10)) < 0x4000, $"{what}: that camera is near the statue");
                    seen++;
                }
                else if (c == 0x6E)
                {
                    int pedestal = Class(I(p, 4));
                    Check(Class(I(p, 0)) == 0xFA && pedestal > 0 && pedestal != 0xFA, $"{what}: a fairy names her statue's and pedestal's rows");
                }
            }
            Check(seen == dragons, $"{what}: every statue was looked at");
            Check(result.Classes.Any(i => i.SourceLevelKey == key && i.ClassId == 0xFA && i.DestinationClassId == 0xFA && i.BehaviorImported && i.ModelImported) &&
                  result.Classes.Any(i => i.SourceLevelKey == key && i.ClassId == 0xFB && i.DestinationClassId == 0xFB && i.BehaviorImported) &&
                  result.Classes.Any(i => i.SourceLevelKey == key && i.ClassId == 0x6E && i.DestinationClassId == 0x6E && i.BehaviorImported),
                $"{what}: the statue, the splinters and the fairy keep their numbers and run {key}'s code");
            Check(result.Classes.Any(i => i.SourceLevelKey == key && i.ClassId == pedestalClass && i.ModelImported), $"{what}: the pedestal's model came along");
            NativeNewWorldExport.RequireOnlyWindowChanges(what, compilation.CopyExecutable(), result.Executable, NativeLevelWindow.FreshTemplate, NativeLevelWindow.WindowIndex);
            Check(true, $"{what}: the executable changes only where the level window carries it");
            int models = 0, textured = 0;
            for (int n = 4; n < sf.Count; n++)
                foreach (var (at, length) in NativeDragonRescueCamera.Models(data.Bytes.AsSpan(sf[n].Offset, sf[n].Length)))
                {
                    models++;
                    if (UniversalAssetImport.TextureDescriptorOffsets(data.Bytes.AsSpan(sf[n].Offset + at, length).ToArray()).Any()) textured++;
                }
            Check(models == 2 * dragons && textured >= dragons, $"{what}: every scene has its dragon's and Spyro's models, textured");
        }
        Composed("Stone Hill's two dragons in a meadow", stoneHill, "stonehill", 2, 0x14B);
        Composed("High Caves' dragon in a meadow", Dragon("highcaves", Statue("highcaves", 0), 2300, 2900), "highcaves", 1, 0x14D);
        Composed("Dry Canyon's four dragons in a meadow", [.. Enumerable.Range(0, 4).SelectMany(n => Dragon("drycanyon", Statue("drycanyon", n), 2200 + 100 * n, 2200 + 250 * n))], "drycanyon", 4, 0x14C);
        Check(Throws(() => compilation.Compose(donors, [.. stoneHill, .. Dragon("highcaves", Statue("highcaves", 0), 2600, 2600)]), "all come from one level of the game"), "Create BIN refuses dragons of two levels in one level");
        // 195 objects and a dragon: a rescue would have too few spare object slots.
        var crowd = Enumerable.Range(0, 195).Select(n => new UniversalMobyPlacement("stonehill", 0, (2100 + 5 * n) * 16, 2100 * 16, 560 * 16)).ToArray();
        Check(Throws(() => compilation.Compose(donors, [.. crowd, .. Dragon("highcaves", Statue("highcaves", 0), 2300, 2900)]), "with a dragon in it a level can hold 192"), "a level too full for a rescue's splinters is refused in plain words");

        {
            // 170 objects, 167 of them baskets (each drops a gem): the game's own reserve of 91 spare slots is needed, 86 are left.
            var stone = donors.Get("stonehill").Data;
            int basket = Enumerable.Range(0, stone.Scene().RowCount).First(r => ClassOf("stonehill", r) == 0xC2);
            var baskets = Enumerable.Range(0, 167).Select(n => new UniversalMobyPlacement("stonehill", basket, (2100 + 8 * n) * 16, (2100 + 60 * (n % 20)) * 16, 560 * 16)).ToArray();
            Check(Throws(() => compilation.Compose(donors, [.. baskets, .. Dragon("highcaves", Statue("highcaves", 0), 2300, 2900)]), "91 must stay free, 86 would"), "170 objects that drop things and a dragon: refused with the numbers");
            var fewer = compilation.Compose(donors, [.. baskets.Take(162), .. Dragon("highcaves", Statue("highcaves", 0), 2300, 2900)]);
            Check(fewer.ArenaSlotsRemaining == 91 && NativeDragonTotals.StatuesIn(fewer.LevelData) == 1, "165 objects and a dragon: the game's own 91 spare slots, composed");
        }
        // The world's homeworld (built on Artisans' place): Artisans' own dragons as before, another level's as guests.
        var template = catalog.FindByKey(NativeNewWorldExport.TemplateKey)!;
        int prefix = donors.Get(template.Key).Data.Scene().RowCount;
        var home = StarterLevel.Create("DRAGON SHORES", catalog) with { WorldLevelId = 70,
            TargetBinding = new(template.Key, template.LevelId, template.SourceWadEntry, template.SourceWadEntry - 1, UniversalSourceLibrary.OriginalUsaSha256, prefix) };
        var worldCatalog = new LevelCatalog([.. catalog.Levels, NewWorldDocument.Target(70, home.Name)]);
        var compiled = await NativeClearedLevelCompilation.CaptureAsync(usa, home, worldCatalog, Path.Combine(scratch, "world-70-host.bin"), default, _ => null);
        byte[] hostExe = NativeClearedRetailBinding.Prepare(exe, exe.ToArray(), template);
        {
            var own = Dragon("artisans", Statue("artisans", 0), 2300, 2900);
            var result = compiled.ComposeWithExecutable(donors, own, hostExe, null);
            var data = new UniversalLevelData(result.LevelData);
            Check(NativeDragonTotals.StatuesIn(result.LevelData) == 1 && data.Subfiles().Count == 4 + 5, "the homeworld with one of Artisans' dragons: its statue and Artisans' five scenes, as a rebuilt Artisans has them");
            NativeNewWorldExport.RequireOnlyWindowChanges("homeworld", hostExe, result.Executable, template.LevelId, 0);
            Check(true, "the homeworld's executable changes only where the level window carries it");
        }
        {
            var guests = Dragon("wizardpeak", Statue("wizardpeak", 1), 2300, 2900);
            var result = compiled.ComposeWithExecutable(donors, guests, hostExe, null);
            var data = new UniversalLevelData(result.LevelData);
            var regs = data.Registrations().OrderBy(r => r.Root).ToList();
            var room = UniversalNativeDragonRescuePrefix.MeasureGuests("homeworld", data, result.Executable);
            if (verbose) Console.WriteLine($"   homeworld guest: statues {NativeDragonTotals.StatuesIn(result.LevelData)}, subfiles {data.Subfiles().Count}, models {string.Join(" ", regs.Select(r => $"{r.ClassId:X}@{r.Root - data.Subfiles()[2].Offset}"))} of {data.Subfiles()[2].Length}, room {room?.AvailablePrefix} of {room?.RequiredPrefix}");
            Check(NativeDragonTotals.StatuesIn(result.LevelData) == 1 && data.Subfiles().Count == 5 && regs.FindIndex(r => r.ClassId == 0xFB) > regs.FindIndex(r => r.ClassId == 0xFA) && regs.FindIndex(r => r.ClassId == 0xFA) >= 0,
                "the homeworld with a Wizard Peak dragon as a guest: one scene, the splinters' model after the statue's");
            Check(room is { Deficit: 0, StreamCount: 1 } && room.RequiredPrefix == UniversalDragonGuests.SceneRoom(donors.Get("wizardpeak").Data, "wizardpeak", Statue("wizardpeak", 1)), "room for its scene below the statue's model");
            Check(result.Classes.Any(i => i.SourceLevelKey == "wizardpeak" && i.ClassId == 0xFA && i.DestinationClassId == 0xFA && i.BehaviorImported), "Wizard Peak's code frees it");
            NativeNewWorldExport.RequireOnlyWindowChanges("homeworld", hostExe, result.Executable, template.LevelId, 0);
            Check(true, "with a guest the homeworld's executable changes only where the level window carries it");
        }
        Check(Throws(() => compiled.ComposeWithExecutable(donors, [.. Dragon("artisans", Statue("artisans", 0), 2300, 2900), .. Dragon("wizardpeak", Statue("wizardpeak", 1), 2300, 2300)], hostExe, null),
            "all come from one level of the game"), "Artisans' own dragons and another level's together are refused in the homeworld");
        {
            // A level rebuilt in Artisans' own place (no world): its own dragon as before, another level's refused in plain words.
            var rebuilt = StarterLevel.Create("LOST FLEET", catalog) with { TargetBinding = home.TargetBinding };
            var plain = await NativeClearedLevelCompilation.CaptureAsync(usa, rebuilt, catalog, Path.Combine(scratch, "artisans-host.bin"), default, _ => null);
            Check(Throws(() => plain.ComposeWithExecutable(donors, Dragon("wizardpeak", Statue("wizardpeak", 1), 2300, 2900), hostExe, null), "can free only Artisans' own dragons"),
                "a level rebuilt in Artisans' place refuses another level's dragon (it would stop the game at the rescue)");
            var kept = plain.ComposeWithExecutable(donors, Dragon("artisans", Statue("artisans", 2), 2300, 2900), hostExe, null);
            Check(NativeDragonTotals.StatuesIn(kept.LevelData) == 1 && new UniversalLevelData(kept.LevelData).Subfiles().Count == 9, "and keeps Artisans' own as before");
        }
    }
    finally { try { Directory.Delete(scratch, true); } catch (IOException) { } }
    return Finish("WORLD_DRAGONS_SOURCE_OK");
}

/// <summary>Made-up levels and a made-up game for the source-free checks (no byte of the game's).</summary>
static class Made
{
    private static void W(byte[] b, int at, int v) => BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(at), v);

    /// <summary>A level file with <paramref name="statues"/> dragon statues among <paramref name="others"/> other objects
    /// (and <paramref name="hidden"/> statues put out of the way): four subfiles, the fourth a scene the game's reader walks.</summary>
    public static byte[] Level(int statues, int others = 2, int hidden = 0)
    {
        int rows = statues + others + hidden;
        byte[] scene = new byte[0x88 + 7 * 4 + 8 + rows * 0x58 + 16];
        int p = 0x88;
        for (int i = 0; i < 7; i++) { W(scene, p, 4); p += 4; }
        W(scene, p, 8 + rows * 0x58); W(scene, p + 4, rows);
        for (int r = 0; r < rows; r++)
        {
            int row = p + 8 + r * 0x58;
            BinaryPrimitives.WriteUInt16LittleEndian(scene.AsSpan(row + 0x36), (ushort)(r < statues + hidden ? 0xFA : 0x53));
            W(scene, row + 0x0C, r >= statues && r < statues + hidden ? -500000 : 1000 * (r + 1));
        }
        p += 8 + rows * 0x58;
        W(scene, p, 4); W(scene, p + 4, 4); W(scene, p + 8, 4); W(scene, p + 12, 0);
        byte[] data = new byte[0x800 * 4 + scene.Length];
        for (int i = 0; i < 3; i++) { W(data, 8 * i, 0x800 * (i + 1)); W(data, 8 * i + 4, 0x800); }
        W(data, 24, 0x800 * 4); W(data, 28, scene.Length);
        scene.CopyTo(data, 0x800 * 4);
        return data;
    }

    /// <summary>An overlay as long as Gnasty's World's with the words of its doorway's dragon and egg tests.</summary>
    public static byte[] GnastysWorld()
    {
        int gate = (int)(NativeDragonTotals.LootGate - UniversalOverlay.Base);
        byte[] overlay = new byte[gate + 0x60];
        foreach (var (offset, word) in new[] { (-12, 0x3C028007u), (-8, 0x8C425750u), (-4, 0u), (0, 0x28420050u), (12, 0x3C028007u), (16, 0x8C425810u), (20, 0u), (24, 0x2842000Cu) })
            BinaryPrimitives.WriteUInt32LittleEndian(overlay.AsSpan(gate + offset), word);
        return overlay;
    }

    public static void Put(byte[] exe, uint address, uint[] words)
    {
        for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan((int)(address - UniversalExeBindings.ExeBase) + 4 * i), words[i]);
    }

    public static uint[] Words(byte[] bytes, uint address, int count, uint? origin = null) =>
        [.. Enumerable.Range(0, count).Select(i => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan((int)(address - (origin ?? UniversalExeBindings.ExeBase)) + 4 * i)))];

    /// <summary>A game: an executable as long as the game's with its table of dragons, its egg row's length and words
    /// standing for its own percentage; 36 levels that are as the "disc" has them unless changed; a world's levels.</summary>
    public sealed class Game
    {
        public Dictionary<int, byte[]> Levels { get; } = new() { [69] = GnastysWorld() };
        public Dictionary<int, byte[]> Changed { get; } = [];
        private static readonly Dictionary<int, byte[]> Disc = Enumerable.Range(0, 36).ToDictionary(NativeDragonTotals.DataEntry, i => Level(NativeDragonTotals.RetailPerLevel[i] - (i == 30 ? 1 : 0), others: 3 + i));

        public static byte[] Blank()
        {
            byte[] exe = new byte[0x66000];
            for (int i = 0; i < 36; i++) exe[(int)(NativeDragonTotals.LevelTotals - UniversalExeBindings.ExeBase) + i] = (byte)NativeDragonTotals.RetailPerLevel[i];
            Put(exe, NativeEggTotals.RowBound, [0x2A62000C]);
            Put(exe, NativeLookAtGuard.Site, [.. NativeLookAtGuard.Own]);
            Put(exe, NativeEggTotals.Percentage, [.. Enumerable.Range(0, NativeEggTotals.PercentageWords).Select(i => 0x00000021u + ((uint)i << 11))]);
            return exe;
        }
        public byte[] Exe() => Blank();
        public byte[] Original(int entry) => Disc.TryGetValue(entry, out var level) ? level : Levels.TryGetValue(entry, out var other) ? other : throw new KeyNotFoundException();
        public byte[] Entry(int entry) => Changed.TryGetValue(entry, out var changed) ? changed : Original(entry);
    }
}

/// <summary>The look-at words run: v0 0x80000000 and a2 0x80076E90 as the game has them there; the words written to memory in
/// order and where the code goes on.</summary>
static class Look
{
    public static bool Within(int exeOffset) =>
        exeOffset >= (int)(NativeLookAtGuard.Site - UniversalExeBindings.ExeBase) && exeOffset < (int)(NativeLookAtGuard.Site - UniversalExeBindings.ExeBase) + 40;

    public static (uint Exit, List<(uint Address, uint Value)> Stores) Run(IReadOnlyList<uint> words, uint lookAt, uint how)
    {
        uint[] r = new uint[32]; r[2] = 0x80000000; r[3] = lookAt; r[4] = how; r[6] = 0x80076E90;
        List<(uint, uint)> stores = [];
        uint pc = NativeLookAtGuard.Site; uint? jump = null;
        for (int steps = 0; steps < 40; steps++)
        {
            if (pc < NativeLookAtGuard.Site || pc >= NativeLookAtGuard.Site + 4 * (uint)words.Count) return (pc, stores);
            uint w = words[(int)(pc - NativeLookAtGuard.Site) / 4], here = pc; pc = jump ?? pc + 4; jump = null;
            int op = (int)(w >> 26), rs = (int)(w >> 21 & 31), rt = (int)(w >> 16 & 31); short imm = (short)(w & 0xFFFF);
            switch (op)
            {
                case 0 when w == 0: break;
                case 2: jump = (here & 0xF0000000) | ((w & 0x03FFFFFF) << 2); break;
                case 4: if (r[rs] == r[rt]) jump = (uint)(here + 4 + (imm << 2)); break;
                case 0x0D: if (rt != 0) r[rt] = r[rs] | (w & 0xFFFF); break;
                case 0x0F: if (rt != 0) r[rt] = (w & 0xFFFF) << 16; break;
                case 0x2B: stores.Add((r[rs] + (uint)imm, r[rt])); break;
                default: throw new InvalidOperationException($"look-at word {w:X8}");
            }
        }
        throw new InvalidOperationException("the look-at words do not end");
    }
}

/// <summary>Just enough of the R3000 to run the completion routine: the three totals at 0x80075860 (gems), 0x80075750
/// (dragons) and 0x80075810 (eggs); returns v0 at jr ra.</summary>
static class Mips
{
    public static uint Run(uint[] code, uint at, int gems, int dragons, int eggs)
    {
        uint[] r = new uint[32]; uint hi = 0, lo = 0; uint pc = at; uint? next = null;
        uint Load(uint a) => a switch { 0x80075860 => (uint)gems, 0x80075750 => (uint)dragons, 0x80075810 => (uint)eggs, _ => throw new InvalidOperationException($"read 0x{a:X8}") };
        for (int guard = 0; guard < 400; guard++)
        {
            uint w = code[(pc - at) / 4]; uint here = pc; pc = next ?? pc + 4; next = null;
            int op = (int)(w >> 26), rs = (int)(w >> 21 & 31), rt = (int)(w >> 16 & 31), rd = (int)(w >> 11 & 31), sh = (int)(w >> 6 & 31), fn = (int)(w & 63);
            short imm = (short)(w & 0xFFFF);
            void Set(int reg, uint v) { if (reg != 0) r[reg] = v; }
            switch (op)
            {
                case 0:
                    switch (fn)
                    {
                        case 0x00: Set(rd, r[rt] << sh); break;
                        case 0x02: Set(rd, r[rt] >> sh); break;
                        case 0x03: Set(rd, (uint)((int)r[rt] >> sh)); break;
                        case 0x08:
                            if (rs != 31) throw new InvalidOperationException("jr");
                            uint d = code[(pc - at) / 4];
                            if (d != 0)
                            {
                                if (d >> 26 == 0 && (d & 63) == 0x21) Set((int)(d >> 11 & 31), r[d >> 21 & 31] + r[d >> 16 & 31]);
                                else throw new InvalidOperationException("delay slot");
                            }
                            return r[2];
                        case 0x10: Set(rd, hi); break;
                        case 0x12: Set(rd, lo); break;
                        case 0x18: { long p = (long)(int)r[rs] * (int)r[rt]; lo = (uint)p; hi = (uint)(p >> 32); break; }
                        case 0x1A: { int a = (int)r[rs], b = (int)r[rt]; lo = (uint)(a / b); hi = (uint)(a % b); break; }
                        case 0x21: Set(rd, r[rs] + r[rt]); break;
                        case 0x23: Set(rd, r[rs] - r[rt]); break;
                        default: throw new InvalidOperationException($"op 0 fn {fn:X} at {here:X8}");
                    }
                    break;
                case 0x05: if (r[rs] != r[rt]) next = (uint)(pc + (imm << 2)); break;
                case 0x04: if (r[rs] == r[rt]) next = (uint)(pc + (imm << 2)); break;
                case 0x09: Set(rt, r[rs] + (uint)imm); break;
                case 0x0A: Set(rt, (int)r[rs] < imm ? 1u : 0u); break;
                case 0x0D: Set(rt, r[rs] | (w & 0xFFFF)); break;
                case 0x0F: Set(rt, (w & 0xFFFF) << 16); break;
                case 0x23: Set(rt, Load(r[rs] + (uint)imm)); break;
                default: throw new InvalidOperationException($"op {op:X} at {here:X8}");
            }
        }
        throw new InvalidOperationException("no return");
    }
}
