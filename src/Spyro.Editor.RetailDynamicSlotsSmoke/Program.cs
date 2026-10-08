using System.Buffers.Binary;
using Spyro.Editor.Core.Universal;

// Spare object slots (the Moby arena's dynamic reserve) in edited retail levels.
// No arguments: synthetic scenes only (source-free gate). With ORIGINAL_USA_BIN_COPY CATALOG_WORKSPACE OUTPUT_DIR
// [--bin]: real retail levels from that image (see SourceCases.cs).
int checks = 0;
void Check(bool ok, string name) { checks++; if (!ok) throw new InvalidOperationException(name); }
int I(byte[] b, int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
const int Slot = UniversalLevelData.ArenaSlotBytes;
Check(Slot == 0x70, "one spare slot is a 0x58 row and a 0x18 record");

// --- Synthetic retail-shaped level: subfiles 0..2 of one sector, the scene (3), and one more subfile (4) after it.
// Scene: 0x88 header, seven components, Moby component (rows + arena), properties, pods, collision, fixups.
(byte[] Level, Layout L) Build(int arenaBytes, byte[]? tail)
{
    List<byte> s = [.. Enumerable.Range(0, 0x88).Select(i => (byte)(0xA0 ^ i))];
    void Word(int v) { var w = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(w, v); s.AddRange(w); }
    for (int i = 0; i < 7; i++) { Word(8); Word(0x1000 + i); }
    int mob = s.Count, rows = mob + 8, count = 6;
    Word(8 + count * 0x58 + arenaBytes); Word(count);
    // Rows: enemy with a gem reward, basket with a butterfly, visible red gem, removed (inert) row, teal gem, plain scenery.
    (int Class, byte Reward)[] kinds = [(0x40, 0x54), (0x0D, 0x10), (0x53, 0x00), (UniversalMobyComposer.InertClass, 0x54), (0x1CF, 0x55), (0x30, 0xFF)];
    var rowBytes = new byte[count * 0x58];
    for (int r = 0; r < count; r++)
    {
        var row = rowBytes.AsSpan(r * 0x58, 0x58);
        for (int k = 4; k < 0x58; k++) row[k] = (byte)(r * 7 + k);
        BinaryPrimitives.WriteUInt16LittleEndian(row[0x36..], (ushort)kinds[r].Class);
        row[0x53] = kinds[r].Reward;
        BinaryPrimitives.WriteInt32LittleEndian(row, 0);
    }
    s.AddRange(rowBytes); s.AddRange(new byte[arenaBytes]);
    int props = s.Count; Word(4 + 3 * 16);
    int block0 = s.Count, block1 = block0 + 16, block2 = block1 + 16;
    for (int k = 0; k < 48; k++) s.Add((byte)(0x50 + k));
    int pod = s.Count; Word(12); Word(0x0D0D0D0D); Word(0x0E0E0E0E);
    int col = s.Count; Word(8); Word(0x0C0C0C0C);
    // Fixed-up pointers: rows 0, 1 and 5 own blocks 0, 1, 2; block 0 links block 1 and a field of row 2; block 1 points
    // into the pod component.
    (int At, int Target)[] fixups = [(rows, block0), (rows + 0x58, block1), (rows + 5 * 0x58, block2),
        (block0 + 4, block1), (block0 + 8, rows + 2 * 0x58 + 0x0C), (block1, pod + 4)];
    byte[] scene = [.. s];
    foreach (var (at, target) in fixups) BinaryPrimitives.WriteInt32LittleEndian(scene.AsSpan(at), target);
    s = [.. scene];
    int fix = s.Count; Word(fixups.Length); foreach (var (at, _) in fixups) Word(at);
    int used = s.Count;
    if (tail != null) s.AddRange(tail);
    int sceneLength = UniversalLevelData.Align(s.Count + (tail == null ? 600 : 0), 0x800);
    byte[] level = new byte[0x800 + 3 * 0x800 + sceneLength + 0x800];
    for (int i = 0; i < 3; i++) { BinaryPrimitives.WriteInt32LittleEndian(level.AsSpan(8 * i), 0x800 * (i + 1)); BinaryPrimitives.WriteInt32LittleEndian(level.AsSpan(8 * i + 4), 0x800); }
    BinaryPrimitives.WriteInt32LittleEndian(level.AsSpan(24), 0x2000); BinaryPrimitives.WriteInt32LittleEndian(level.AsSpan(28), sceneLength);
    BinaryPrimitives.WriteInt32LittleEndian(level.AsSpan(32), 0x2000 + sceneLength); BinaryPrimitives.WriteInt32LittleEndian(level.AsSpan(36), 0x800);
    s.ToArray().CopyTo(level, 0x2000);
    level.AsSpan(0x2000 + sceneLength, 0x800).Fill(0x44);
    return (level, new(mob, rows, count, props, pod, col, fix, used, fixups.Select(f => f.At).ToArray()));
}

// The scene loader's view (independent of UniversalLevelData.Scene): component lengths walked from 0x88.
(int Arena, int Mob, int Rows, int Props, int Pod, int Col, int Fix, int Used, int[] Fixups) Walk(byte[] level)
{
    int so = I(level, 24); byte[] s = level.AsSpan(so).ToArray(); int p = 0x88;
    for (int i = 0; i < 7; i++) p += I(s, p);
    int mob = p, count = I(s, p + 4); p += I(s, p);
    int props = p; p += I(s, p); int pod = p; p += I(s, p); int col = p; p += I(s, p);
    int n = I(s, p); int[] f = Enumerable.Range(0, n).Select(i => I(s, p + 4 + 4 * i)).ToArray();
    return ((props - (mob + 8 + count * 0x58)) / Slot, mob, count, props, pod, col, p, p + 4 + 4 * n, f);
}

byte[] SceneOf(byte[] level) => level.AsSpan(I(level, 24), I(level, 28)).ToArray();

void RequireMoved(byte[] before, byte[] after, int slots, string name)
{
    var a = Walk(before); var b = Walk(after); int growth = slots * Slot;
    byte[] x = SceneOf(before), y = SceneOf(after);
    int insert = a.Props;
    int Move(int v) => v >= insert ? v + growth : v;
    Check(b.Rows == a.Rows && b.Arena == a.Arena + slots, name + ": rows kept, arena grown by " + slots);
    Check(b.Props == a.Props + growth && b.Pod == a.Pod + growth && b.Col == a.Col + growth && b.Fix == a.Fix + growth,
        name + ": every following component moved by the growth");
    Check(b.Fixups.SequenceEqual(a.Fixups.Select(Move)), name + ": fixup table names the moved fields");
    Check(y.AsSpan(insert, growth).IndexOfAnyExcept((byte)0) < 0, name + ": new slots are empty");
    var fixedAt = a.Fixups.ToHashSet();
    // Every byte but the fixed-up words and the Moby component's length keeps its value at its moved place.
    var changed = a.Fixups.Append(a.Mob).SelectMany(f => Enumerable.Range(f, 4)).ToHashSet();
    int differing = Enumerable.Range(0, a.Fix).Count(i => !changed.Contains(i) && x[i] != y[Move(i)]);
    Check(differing == 0, $"{name}: every other byte kept at its moved place ({differing} differ)");
    Check(I(y, a.Mob) == I(x, a.Mob) + growth, name + ": Moby component length grown");
    foreach (int f in a.Fixups)
    {
        int t = I(x, f), u = I(y, Move(f));
        Check(u == Move(t), $"{name}: pointer at 0x{f:X} follows its target");
        if (!fixedAt.Contains(t)) Check(I(x, t) == I(y, u), $"{name}: pointer at 0x{f:X} still reaches the same bytes");
    }
    Check(I(y, Move(a.Fixups[4])) == I(x, a.Fixups[4]), name + ": a pointer into the rows is unchanged");
    // The subfile after the scene keeps its bytes at its new offset; the scene stays sector-rounded.
    Check(I(after, 28) % 0x800 == 0 && I(after, 32) == I(after, 24) + I(after, 28) && I(after, 36) == 0x800 &&
        after.AsSpan(I(after, 32), 0x800).IndexOfAnyExcept((byte)0x44) < 0, name + ": later subfile moved intact");
    Check(UniversalMobyComposer.ArenaSlots(new UniversalLevelData(after)) == b.Arena, name + ": composer reads the loader's capacity");
}

// 1. Growth moves properties, pods, collision and fixups; pointers follow; rows and the header stay.
var (plain, layout) = Build(20 * Slot + 0x30, null);
var grown = new UniversalLevelData(plain.ToArray()); grown.GrowArena(71);
RequireMoved(plain, grown.Bytes, 71, "plain scene");
Check(Walk(grown.Bytes).Arena == 91, "twenty slots grown to the retail 91");
Check(new UniversalLevelData(grown.Bytes).Scene().RowCount == 6, "row count unchanged");

// 2. A scene with bytes after its fixup table (Stone Hill, Doctor Shemp, Wizard Peak, Icy Flight have such tails).
byte[] tailBytes = Enumerable.Range(0, 300).Select(i => (byte)(i * 3 + 1)).ToArray();
var (tailed, tailedLayout) = Build(20 * Slot + 0x30, tailBytes);
var grownTail = new UniversalLevelData(tailed.ToArray()); grownTail.GrowArena(30);
RequireMoved(tailed, grownTail.Bytes, 30, "scene with a tail");
var tailScene = SceneOf(grownTail.Bytes);
Check(tailScene.AsSpan(tailedLayout.Used + 30 * Slot, tailBytes.Length).SequenceEqual(tailBytes), "tail kept after the moved fixup table");

// 3. Drop-capable rows: the enemy and the basket; not gems, teal gems, removed rows or rows without a reward.
Check(UniversalMobyComposer.DropCapableRows(new UniversalLevelData(plain)) == 2, "two of six rows can drop something");
Check(UniversalMobyComposer.DropCapableRows(grown) == 2, "growth does not change what can drop");

// 4. Required slots: one per dropper plus 16, at least 16, never more than the level's own retail reserve.
Check(UniversalMobyComposer.RequiredDynamicSlots(91, 2) == 18 && UniversalMobyComposer.RequiredDynamicSlots(91, 0) == 16 &&
    UniversalMobyComposer.RequiredDynamicSlots(91, 40) == 56 && UniversalMobyComposer.RequiredDynamicSlots(91, 103) == 91 &&
    UniversalMobyComposer.RequiredDynamicSlots(182, 108) == 124 && UniversalMobyComposer.RequiredDynamicSlots(272, 120) == 136,
    "required spare slots");

// 5. Restoring the reserve within the RAM room (the scene is loaded last; it grows by whole sectors).
int length = I(plain, 28), used = layout.Used;
int Expect(long room, int want) => (int)Math.Max(0, Math.Min(want, (length + Math.Max(0, room) / 0x800 * 0x800 - used) / Slot));
foreach (long room in new long[] { 1 << 20, 0x2800, 0x1000, 0x800, 0x7FF, 0 })
{
    var (data, added) = UniversalMobyComposer.RestoreDynamicSlots(new UniversalLevelData(plain.ToArray()), 91, room);
    Check(added == Expect(room, 71), $"room 0x{room:X}: {added} slots added, expected {Expect(room, 71)}");
    Check(data.Subfiles()[3].Length - length <= room && UniversalMobyComposer.ArenaSlots(data) == 20 + added, $"room 0x{room:X}: within RAM");
    if (added > 0) RequireMoved(plain, data.Bytes, added, $"room 0x{room:X}");
}
Check(UniversalMobyComposer.RestoreDynamicSlots(new UniversalLevelData(plain.ToArray()), 91, -1).Added == 0, "no growth when RAM is already short");
Check(UniversalMobyComposer.RestoreDynamicSlots(new UniversalLevelData(plain.ToArray()), 20, 1 << 20).Added == 0 &&
    UniversalMobyComposer.RestoreDynamicSlots(grown, 91, 1 << 20).Added == 0, "no growth when the reserve is already kept");
var (tailData, tailAdded) = UniversalMobyComposer.RestoreDynamicSlots(new UniversalLevelData(tailed.ToArray()), 91, 0x800);
Check(tailAdded > 0 && tailData.Subfiles()[3].Length - I(tailed, 28) <= 0x800, "a kept tail counts against the RAM room");
RequireMoved(tailed, tailData.Bytes, tailAdded, "tail within RAM");

// 6. The Bandicoot Keep case on a retail-sized arena (0x2800 bytes): 49 rows appended through AppendRows leave 52
// slots; restoring keeps the appended rows and their properties.
var (retail, _) = Build(0x2800, null);
var keep = new UniversalLevelData(retail.ToArray());
Check(UniversalMobyComposer.ArenaSlots(keep) == 91, "retail-sized arena holds 91");
byte[] props16 = new byte[16]; props16[12] = 0x77;
var appended = Enumerable.Range(0, 49).Select(i =>
{
    byte[] row = new byte[0x58]; BinaryPrimitives.WriteUInt16LittleEndian(row.AsSpan(0x36), 0x40); row[0x53] = 0x54; row[0x10] = (byte)i;
    return (row, new UniversalRowTemplate("synthetic", -1, row, props16.ToArray(), [(4, 12)], 0));
}).ToArray();
var (_, _, newRows) = keep.AppendRows(appended);
Check(newRows.SequenceEqual(Enumerable.Range(6, 49)) && UniversalMobyComposer.ArenaSlots(keep) == 52, "49 added rows leave 52 slots");
byte[] beforeRestore = keep.Bytes.ToArray();
var (kept, keptAdded) = UniversalMobyComposer.RestoreDynamicSlots(keep, 91, 1 << 20);
Check(keptAdded == 39 && UniversalMobyComposer.ArenaSlots(kept) == 91, "39 slots restore the retail 91");
RequireMoved(beforeRestore, kept.Bytes, 39, "after AppendRows");
Check(UniversalMobyComposer.DropCapableRows(kept) == 51, "49 added droppers counted with the level's own two");
for (int r = 6; r < 55; r++)
{
    byte[] row = kept.Row(r); var sc = kept.Scene(); int p = I(row, 0);
    var s = SceneOf(kept.Bytes);
    Check(p >= sc.PropertiesComponent + 4 && p < sc.PropertiesEnd && I(s, p + 4) == p + 12 && s[p + 12] == 0x77 && row[0x10] == r - 6,
        $"added row {r} keeps its own moved properties");
}
var sceneKept = kept.Scene();
Check(sceneKept.Fixups.All(f => f + 4 <= sceneKept.UsedEnd && I(SceneOf(kept.Bytes), f) < sceneKept.UsedEnd), "every pointer stays inside the scene");

if (args.Length == 0) { Console.WriteLine($"RETAIL_DYNAMIC_SLOTS_FREE_OK checks={checks}"); return; }
checks = await SourceCases.Run(args, checks);
Console.WriteLine($"RETAIL_DYNAMIC_SLOTS_SOURCE_OK checks={checks}");

internal sealed record Layout(int Mob, int Rows, int Count, int Props, int Pod, int Col, int Fix, int Used, int[] Fixups);
