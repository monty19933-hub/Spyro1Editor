using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

// Egg thieves (2.3): the game's egg totals follow the thieves a game holds (NativeEggTotals), and a thief's run can be
// changed where he can run it (EggThiefRun). Default: source-free (the totals' arithmetic, the new completion routine
// run instruction by instruction, the egg counters and the doorway to Gnasty's Loot rewritten, the run rule on made-up
// ground). --source <USA.bin>: on the USA game, the twelve thieves found where the game counts them, nothing written for
// it, the game's own completion routine giving the same as the new one for twelve eggs, every level's egg counter, and
// thieves added and taken out (levels swapped for others) setting every total.

int checks = 0, failed = 0;
void Check(bool ok, string what)
{
    checks++;
    if (!ok) { failed++; Console.WriteLine("FAIL " + what); }
}

if (args.Length >= 2 && args[0] == "--source")
{
    Source(args[1]);
    Finish("EGG_THIEF_SOURCE_OK");
    return;
}
if (args.Length != 0) { Console.Error.WriteLine("usage: EggThiefSmoke [--source <USA.bin>]"); Environment.Exit(2); }

// ---- Totals ----
Check(NativeEggTotals.Divisor(12) == 12000 && NativeEggTotals.Divisor(13) == 12100 && NativeEggTotals.Divisor(11) == 11900, "divisor follows the eggs");
for (int total = 0; total <= NativeEggTotals.MaxTotal; total++)
{
    Check(NativeEggTotals.Percent(12000, 80, total, total) == 100, $"everything collected is 100% with {total} eggs");
    if (total > 0) Check(NativeEggTotals.Percent(12000, 80, total - 1, total) == 99, $"one egg short is 99% with {total} eggs");
    Check(NativeEggTotals.Percent(14000, 80, total, total) == 120, $"Gnasty's Loot treasure still counts past 100 with {total} eggs");
    var routine = NativeEggTotals.PercentageRoutine(total);
    Check(routine.Length == NativeEggTotals.PercentageWords, $"the routine fits the game's own ({total})");
    foreach (var (gems, dragons, eggs) in new[] { (0, 0, 0), (12000, 80, total), (5000, 37, total / 2), (12001, 80, total), (13999, 3, 0), (100, 1, Math.Min(total, 1)) })
    {
        uint got = Mips.Run(routine, NativeEggTotals.Percentage, gems, dragons, eggs);
        Check(got == (uint)NativeEggTotals.Percent(gems, dragons, eggs, total), $"routine for {total} eggs at {gems}/{dragons}/{eggs}: {got}");
    }
}
Check(NativeEggTotals.RowStepFor(12) == 27 && NativeEggTotals.RowStepFor(17) == 27, "the egg row keeps the game's spacing up to 17");
Check(NativeEggTotals.RowStepFor(30) < 27 && NativeEggTotals.RowStepFor(60) >= 6 && 24 + 59 * NativeEggTotals.RowStepFor(60) <= 480, "a long egg row stays on the screen");
Check(UniversalEggUnlocks.PortalGate().SequenceEqual(UniversalEggUnlocks.PortalGate(12)), "dragonfly doorway default is twelve thief eggs");
Check(UniversalEggProgress.PercentageRoutine(3).SequenceEqual(UniversalEggProgress.PercentageRoutine(3, 12)), "dragonfly completion default is twelve thief eggs");
Check(!UniversalEggProgress.PercentageRoutine(3).SequenceEqual(UniversalEggProgress.PercentageRoutine(3, 13)), "dragonfly completion counts added thief eggs");

// Egg counters: a made-up overlay with two copies of the egg's count of itself.
uint[] counter = [0x3C058007, 0x8CA55964, 0x3C028007, 0x24426FE8, 0x00052880, 0x00A22821];
byte[] overlay = new byte[0x200];
foreach (int at in new[] { 0x40, 0x120 }) for (int k = 0; k < counter.Length; k++) BinaryPrimitives.WriteUInt32LittleEndian(overlay.AsSpan(at + 4 * k), counter[k]);
Check(NativeEggTotals.EggCounters(overlay) == (2, 0), "both egg counters found");
var redirected = NativeEggTotals.Redirect(overlay, 5);
Check(redirected != null && NativeEggTotals.EggCounters(redirected) == (0, 2), "both counted in the spare place");
Check(redirected != null && BinaryPrimitives.ReadUInt32LittleEndian(redirected.AsSpan(0x44)) == 0x24050005, "addiu a1, zero, 5 in place of the level index");
Check(redirected != null && NativeEggTotals.Redirect(redirected, 5) == null, "redirecting twice changes nothing");
Check(redirected != null && NativeEggTotals.Redirect(redirected, 11) is { } again && NativeEggTotals.EggCounters(again) == (0, 2) && BinaryPrimitives.ReadUInt32LittleEndian(again.AsSpan(0x44)) == 0x2405000B, "another spare place replaces the first");
Check(overlay.AsSpan(0x44, 4).SequenceEqual(new byte[] { 0x64, 0x59, 0xA5, 0x8C }), "the overlay given is not changed");
bool threw = false;
try { NativeEggTotals.Redirect(overlay, 18); } catch (ArgumentOutOfRangeException) { threw = true; }
Check(threw, "a spare place must be one of the eighteen");

// The doorway to Gnasty's Loot.
byte[] gnasty = new byte[(int)(NativeEggTotals.LootGate - UniversalOverlay.Base) + 0x40];
int gate = (int)(NativeEggTotals.LootGate - UniversalOverlay.Base);
foreach (var (offset, word) in new[] { (-12, 0x3C028007u), (-8, 0x8C425810u), (-4, 0u), (0, 0x2842000Cu) }) BinaryPrimitives.WriteUInt32LittleEndian(gnasty.AsSpan(gate + offset), word);
Check(NativeEggTotals.SetLootGate(gnasty, 12) == null, "twelve eggs: the doorway is left alone");
Check(NativeEggTotals.SetLootGate(gnasty, 13) is { } gated && BinaryPrimitives.ReadUInt32LittleEndian(gated.AsSpan(gate)) == 0x2842000D &&
      gated.Where((b, i) => b != gnasty[i]).Count() == 1, "thirteen eggs: one byte of the doorway's test changes");
Check(NativeEggTotals.SetLootGate(new byte[gate + 4], 13) == null, "an overlay without the doorway is left alone");
byte[] rewritten = new byte[(int)(UniversalEggUnlocks.PortalAddress - UniversalOverlay.Base) + 0x80];
UniversalEggUnlocks.PortalGate().CopyTo(rewritten, (int)(UniversalEggUnlocks.PortalAddress - UniversalOverlay.Base));
Check(NativeEggTotals.SetLootGate(rewritten, 14) is { } portal && portal.AsSpan((int)(UniversalEggUnlocks.PortalAddress - UniversalOverlay.Base), 72).SequenceEqual(UniversalEggUnlocks.PortalGate(14)),
    "with dragonfly eggs the rewritten doorway asks for the thieves' eggs too");

// ---- Runs on made-up ground ----
static TerrainPolygon Quad(float x0, float y0, float x1, float y1, float z) =>
    new([new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)], [z, z, z, z], 0, 0, 0, "", new ColorRgba(0, 0, 0));
List<TerrainPolygon> ground = [Quad(0, 0, 4000, 4000, 100)];
var owner = new Vector3f(2000, 2000, 100);
List<Vector3f> Loop(float r, float z = 132) => [.. Enumerable.Range(0, 8).Select(i => new Vector3f(2000 + r * MathF.Cos(i * MathF.PI / 4), 2000 + r * MathF.Sin(i * MathF.PI / 4), z))];
Check(EggThiefRun.Walk(ground, owner, Loop(500), 0).Ok, "a loop on flat ground is good");
Check(EggThiefRun.Judge(ground, owner, Loop(500), 0, null).Count == 0, "an added thief's loop on flat ground is good");
Check(EggThiefRun.Judge(ground, owner, Loop(500, 900), 0, Loop(500), owner).Count == 0, "a point's height alone changes nothing (the game does not use it)");
var off = Loop(500); off[3] = new Vector3f(6000, 2000, 132);
var offProblems = EggThiefRun.Judge(ground, owner, off, 0, Loop(500), owner);
Check(offProblems.Count > 0 && offProblems.All(p => p.NoGround) && offProblems.All(p => p.Touches(3)), "a point off the map: no ground, on its own legs");
Check(offProblems.Count > 0 && offProblems[0].Text.Contains("point 3 to point 4") || offProblems.Any(p => p.Text.Contains("to point 4")), "the leg is named by the points' numbers");
// A raised block from x 2300 to 3000 (y 1000 to 3000), with the floor round it (not under it).
List<TerrainPolygon> Block(float z) => [Quad(0, 0, 2300, 4000, 100), Quad(3000, 0, 4000, 4000, 100), Quad(2300, 0, 3000, 1000, 100), Quad(2300, 3000, 3000, 4000, 100), Quad(2300, 1000, 3000, 3000, z)];
List<TerrainPolygon> step = Block(220);
var onStep = Loop(500);
Check(EggThiefRun.Walk(step, owner, onStep, 0).Ok, "a step of 120 is one a thief takes");
List<TerrainPolygon> wall = Block(320);
var stopped = EggThiefRun.Judge(wall, owner, Loop(500), 0, null);
Check(stopped.Count > 0 && stopped.Any(p => p.Text.Contains("step up of 220")), "a step of 220 is not");
Check(EggThiefRun.Judge(wall, owner, Loop(500), 0, Loop(500), owner).Count == 0, "the same step on legs the game's own run has is not held against an unmoved run");
var nudged = Loop(500); nudged[0] = new Vector3f(nudged[0].X - 20, nudged[0].Y, 132);
Check(EggThiefRun.Judge(wall, owner, nudged, 0, Loop(500), owner).Count == 0, "moving a point keeps a step its leg already had");
Check(EggThiefRun.Walk([.. Block(320), Quad(2300, 1000, 3000, 3000, 100)], owner, Loop(500), 0).Ok, "a floor under the block (a bridge) is one he runs under");
var into = Loop(250); into[0] = new Vector3f(2600, 2000, 132);
Check(EggThiefRun.Judge(wall, owner, into, 0, Loop(250), owner).Any(p => p.Touches(0)), "moving a point up a step its leg did not have is refused");
List<TerrainPolygon> gap = [Quad(0, 0, 2100, 4000, 100), Quad(2200, 0, 4000, 4000, 100)];
var across = EggThiefRun.Walk(gap, owner, Loop(500), 0);
Check(!across.Ok && across.Problems.Any(p => p.NoGround), "a gap is no ground");
List<TerrainPolygon> walls = [.. ground, new([new(2100, 1500), new(2110, 1500), new(2110, 2500), new(2100, 2500)], [100, 400, 400, 100], 0, 0, 0, "", new ColorRgba(0, 0, 0))];
Check(EggThiefRun.Walk(walls, owner, Loop(300), 0).Ok, "a wall face is not taken for floor");
Check(EggThiefRun.TryPlace(ground, 1000, 1000, 100, out var placed) && placed.Z == 100 + EggThiefRun.PointHeight, "a moved point sits 32 above the floor");
Check(!EggThiefRun.TryPlace(ground, 9000, 9000, 100, out _), "no floor, no place");
Check(EggThiefRun.Walk(ground, new Vector3f(9000, 9000, 100), Loop(500), 0).Problems.Any(p => p.From == -1), "a thief standing off the ground is a problem");
Check(EggThiefRun.Judge(ground, new Vector3f(2100, 2000, 100), Loop(500), 0, Loop(500), owner).Count == 0, "moving the thief on the ground is good");
Check(EggThiefRun.Climb == 93.75f && EggThiefRun.Drop == 162.25f, "the game's probe: 1500 up, 0x1000 down");
Finish("EGG_THIEF_FREE_OK");

void Source(string image)
{
    var disc = UniversalDiscImage.Read(image);
    byte[] exe = disc.Exe;
    int Off(uint a) => (int)(a - UniversalExeBindings.ExeBase);
    var count = NativeEggTotals.Count(disc.EntryBytes);
    Check(count.PerLevel.SequenceEqual(NativeEggTotals.RetailPerLevel) && count.Total == 12 && count.IsRetail, "the game's twelve thieves where the game counts them: " + string.Join(",", count.PerLevel));
    Check(exe.AsSpan(Off(NativeEggTotals.LevelTotals), 18).SequenceEqual(NativeEggTotals.RetailPerLevel.Take(18).Select(v => (byte)v).ToArray()), "the game's table of eggs per level");
    var read = NativeEggTotals.Read(exe, disc.EntryBytes(69));
    Check(read.Row == 12 && read.Step == 27 && read.Gate == 12 && read.Divisor == 12000, "the game's row, spacing, doorway and percentage");
    // Nothing is written for the game as it is.
    byte[] copy = [.. exe]; Dictionary<int, byte[]> none = [];
    Check(NativeEggTotals.ApplyForExport(copy, exe, none, disc.EntryBytes) == null && copy.AsSpan().SequenceEqual(exe) && none.Count == 0, "the unedited game: nothing written");
    // The game's own completion routine and the new one agree for twelve eggs.
    uint[] own = Words(exe.AsSpan(Off(NativeEggTotals.Percentage), 4 * NativeEggTotals.PercentageWords).ToArray());
    uint[] made = NativeEggTotals.PercentageRoutine(12);
    var random = new Random(7);
    for (int i = 0; i < 400; i++)
    {
        int gems = i < 4 ? new[] { 0, 12000, 12001, 14000 }[i] : random.Next(0, 14001), dragons = random.Next(0, 81), eggs = random.Next(0, 13);
        Check(Mips.Run(own, NativeEggTotals.Percentage, gems, dragons, eggs) == Mips.Run(made, NativeEggTotals.Percentage, gems, dragons, eggs), $"the game's percentage and the new one at {gems}/{dragons}/{eggs}");
    }
    for (int level = 0; level < 35; level++)
    {
        byte[] o = disc.EntryBytes(NativeEggTotals.OverlayEntry(level));
        Check(NativeEggTotals.EggCounters(o) == (1, 0), $"level {level} counts its eggs once");
        Check(NativeEggTotals.Redirect(o, 5) is { } r && NativeEggTotals.EggCounters(r) == (0, 1), $"level {level}'s egg can be counted in the spare place");
    }
    // Thieves added and taken out: a level's data swapped for another's.
    byte[] Swapped(int e, Dictionary<int, int> swap) => disc.EntryBytes(swap.TryGetValue(e, out int other) ? other : e);
    (byte[] Exe, Dictionary<int, byte[]> Replaced, EggTotalsApplied? Applied) Apply(Dictionary<int, int> swap)
    {
        byte[] e = [.. exe]; Dictionary<int, byte[]> replaced = [];
        var applied = NativeEggTotals.ApplyForExport(e, exe, replaced, entry => Swapped(entry, swap));
        return (e, replaced, applied);
    }
    // Dark Hollow (index 2) holds a thief (Stone Hill's data): 13 eggs.
    var added = Apply(new() { [NativeEggTotals.DataEntry(2)] = NativeEggTotals.DataEntry(1) });
    var r13 = NativeEggTotals.Read(added.Exe, added.Replaced.TryGetValue(69, out var g13) ? g13 : disc.EntryBytes(69));
    Check(added.Applied?.Total == 13 && r13.PerLevel[2] == 1 && r13.Row == 13 && r13.Divisor == 12100 && r13.Gate == 13, "a thief added in Dark Hollow: 13 everywhere");
    Check(added.Applied?.Bucket == -1 && added.Replaced.Keys.Order().SequenceEqual([69]), "only the doorway's overlay changes");
    Check(added.Exe.Where((b, i) => b != exe[i]).Count() <= 2 + 3 + 4 * NativeEggTotals.PercentageWords, "the executable changes only in the totals");
    Check(Mips.Run(Words(added.Exe.AsSpan(Off(NativeEggTotals.Percentage), 4 * NativeEggTotals.PercentageWords).ToArray()), NativeEggTotals.Percentage, 12000, 80, 13) == 100, "13 eggs collected is 100%");
    var again = NativeEggTotals.ApplyForExport(added.Exe, exe, added.Replaced, entry => Swapped(entry, new() { [NativeEggTotals.DataEntry(2)] = NativeEggTotals.DataEntry(1) }));
    Check(again?.Total == 13 && NativeEggTotals.Read(added.Exe, added.Replaced[69]).Gate == 13, "applying again keeps the same totals");
    // Beast Makers (index 18, past the first three worlds) holds a thief: counted in the spare place.
    var later = Apply(new() { [NativeEggTotals.DataEntry(18)] = NativeEggTotals.DataEntry(1) });
    Check(later.Applied?.Total == 13 && later.Applied.Bucket == 5 && later.Applied.Count.Unlisted == 1, "a thief in Beast Makers: 13, counted in Sunny Flight's spare place");
    Check(later.Replaced.TryGetValue(NativeEggTotals.OverlayEntry(18), out var bm) && NativeEggTotals.EggCounters(bm) == (0, 1), "Beast Makers' egg counts in the spare place");
    Check(NativeEggTotals.Read(later.Exe, later.Replaced[69]).PerLevel[5] == 0, "the spare place shows no egg places on its line");
    // Stone Hill's thief taken out: 11.
    var fewer = Apply(new() { [NativeEggTotals.DataEntry(1)] = NativeEggTotals.DataEntry(2) });
    var r11 = NativeEggTotals.Read(fewer.Exe, fewer.Replaced[69]);
    Check(fewer.Applied?.Total == 11 && r11.PerLevel[1] == 0 && r11.Row == 11 && r11.Divisor == 11900 && r11.Gate == 11, "a thief taken out: 11 everywhere");
    Check(Mips.Run(Words(fewer.Exe.AsSpan(Off(NativeEggTotals.Percentage), 4 * NativeEggTotals.PercentageWords).ToArray()), NativeEggTotals.Percentage, 12000, 80, 11) == 100, "11 eggs collected is 100%");
    // Back to the game's twelve on an executable that had 13: the game's own routine and totals again.
    byte[] back = [.. added.Exe];
    Check(NativeEggTotals.ApplyForExport(back, exe, new Dictionary<int, byte[]>(), disc.EntryBytes) is { Total: 12 } && back.AsSpan(Off(NativeEggTotals.Percentage), 4 * NativeEggTotals.PercentageWords).SequenceEqual(exe.AsSpan(Off(NativeEggTotals.Percentage), 4 * NativeEggTotals.PercentageWords)) &&
          back.AsSpan(Off(NativeEggTotals.LevelTotals), 18).SequenceEqual(exe.AsSpan(Off(NativeEggTotals.LevelTotals), 18)), "back to twelve: the game's own routine and table");
    // Thieves are found by their model, whatever class number a level gives them.
    for (int level = 0; level < 35; level++)
        Check(NativeEggTotals.ThievesIn(disc.EntryBytes(NativeEggTotals.DataEntry(level))) == NativeEggTotals.RetailPerLevel[level], $"level {level}'s thieves");
}

void Finish(string marker)
{
    if (failed > 0) { Console.WriteLine($"EGG_THIEF_FAILED checks={checks} failed={failed}"); Environment.Exit(1); }
    Console.WriteLine($"{marker} checks={checks}");
}

static uint[] Words(byte[] bytes) => [.. Enumerable.Range(0, bytes.Length / 4).Select(i => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4 * i)))];

/// <summary>Just enough of the R3000 to run the completion routines: the three totals at 0x80075860 (gems), 0x80075750
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
                        case 0x08: if (rs == 31) { uint d = code[(pc - at) / 4]; if (d != 0) RunDelay(d); return r[2]; } throw new InvalidOperationException("jr");
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
            void RunDelay(uint d)
            {
                // move v0, v1 (addu) in the delay slot of jr ra.
                if (d >> 26 == 0 && (d & 63) == 0x21) Set((int)(d >> 11 & 31), r[d >> 21 & 31] + r[d >> 16 & 31]);
                else throw new InvalidOperationException("delay slot");
            }
        }
        throw new InvalidOperationException("no return");
    }
}
