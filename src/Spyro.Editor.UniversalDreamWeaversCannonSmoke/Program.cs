using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

// Dream Weavers' resize cannon, its gunner and the fools that name it by row number (UniversalDreamWeaversCannonRows),
// checked on the original USA disc: the source facts, the required pair, group binding, native removals, and real
// compositions into Town Square (no renumbering; the Cloudspire Isles layout of the UiSmoke step cloudspire-cannon),
// Doctor Shemp (renumbers every class of the set) and Dream Weavers itself. Arguments: ORIGINAL_USA_BIN CATALOG_WORKSPACE
// NEW_REPORT_PATH. The image is only read. Without arguments: the disc-free checks in SourceFree.cs.
int checks = 0;
void Check(bool ok, string what) { checks++; if (!ok) throw new InvalidOperationException(what); }
void Refused(Action action, string what)
{
    checks++;
    try { action(); } catch (InvalidDataException) { return; }
    throw new InvalidOperationException("Not refused: " + what);
}
int I(byte[] b, int at) => BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));

if (args.Length == 0)
{
    Console.WriteLine($"UNIVERSAL_DREAMWEAVERS_CANNON_FREE_OK checks={SourceFree.Run()}");
    return;
}
if (args.Length != 3) throw new ArgumentException("Supply the original USA image, the catalog workspace and a new report path.");
string report = Path.GetFullPath(args[2]);
if (File.Exists(report)) throw new IOException("Keep the existing report: " + report);
UniversalSourceLibrary.RequireOriginal(args[0]);
var catalog = LevelCatalog.Load(args[1]);
var lib = new UniversalSourceLibrary(args[0], catalog);
const string Key = UniversalDreamWeaversCannonRows.SourceKey;
var dw = lib.Get(Key);
UniversalRowTemplate Capture(int row) => UniversalIndexedRows.Capture(dw.Data, dw.Overlay, dw.Data.CaptureRow(Key, row));

// 1. The source facts the mapping rests on.
UniversalDreamWeaversCannonRows.Prove(dw.Data, dw.Overlay); checks++;
var rows = Enumerable.Range(0, dw.Data.Scene().RowCount).Select(r => dw.Data.CaptureRow(Key, r)).ToArray();
Check(rows.Count(t => t.ClassId == 0x5B) == 1 && rows[26].ClassId == 0x5B, "one cannon, row 26");
var fools = rows.Where(t => t.ClassId is 0x5C or 0x82).ToArray();
Check(fools.Where(t => I(t.Properties, 0x34) == 1).Select(t => t.SourceRowIndex).SequenceEqual([27]), "one gunner, row 27");
Check(fools.Where(t => t.SourceRowIndex != 27).Select(t => t.SourceRowIndex).Order().SequenceEqual(UniversalDreamWeaversCannonRows.FoolRows.Order()), "FoolRows lists every other fool");
Check(fools.All(t => I(t.Properties, 0x40) == 26) && I(rows[26].Properties, 4) == 27, "every fool names the cannon; the cannon names its gunner");
foreach (var t in fools.Where(t => t.ClassId == 0x82))
    foreach (int o in UniversalDreamWeaversCannonRows.PartnerOffsets)
        Check(I(t.Properties, o) == -1 || rows[I(t.Properties, o)].ClassId == 0x82, "Armored Fool partners are Armored Fools");
Check(new[] { 20, 21 }.All(r => rows[r].ClassId == 0x82 && I(rows[r].Properties, 0x3C) == 0 && I(rows[r].Properties, 0x10) == 1), "rows 20/21 are big Armored Fools that never call");
Check(UniversalDreamWeaversCannonRows.Describe(dw.Data, 20).StartsWith("Starts big; never calls") && UniversalDreamWeaversCannonRows.Describe(dw.Data, 17).Contains("Starts small; with a Resize cannon in its group it calls the cannon once Spyro comes within 62 units") &&
    UniversalDreamWeaversCannonRows.Describe(dw.Data, 26) == "", "the browser describes each fool");

// 2. The required pair: cannon +4 -> gunner, gunner +40 -> cannon. Fools have no required link.
var cannon = Capture(26); var gunner = Capture(27);
Check(cannon.SourceRowIndices.SequenceEqual([new UniversalSourceRowIndex(4, 27, 0x5C)]), "cannon +4 names the gunner");
Check(gunner.SourceRowIndices.SequenceEqual([new UniversalSourceRowIndex(0x40, 26, 0x5B)]), "gunner +40 names the cannon");
foreach (int r in UniversalDreamWeaversCannonRows.FoolRows) Check(Capture(r).SourceRowIndices.Count == 0, $"fool row {r} has no required link");
foreach (uint pc in new uint[] { 0x8007F558, 0x8007F66C, 0x8007FADC, 0x8007FC54, 0x8008032C, 0x80080C50, 0x800843C8, 0x80084460, 0x800804C0, 0x80084518, 0x80080290 })
{
    byte[] code = dw.Overlay.Bytes.ToArray(); code[(int)(pc - UniversalOverlay.Base)] ^= 1;
    Refused(() => UniversalIndexedRows.Capture(dw.Data, new UniversalOverlay(code, dw.Overlay.Bindings), dw.Data.CaptureRow(Key, 26)), $"changed code at {pc:X8}");
}
byte[] changedData = dw.Data.Bytes.ToArray(); changedData[0x200] ^= 1;
Refused(() => UniversalIndexedRows.Capture(new UniversalLevelData(changedData), dw.Overlay, dw.Data.CaptureRow(Key, 26)), "changed level data");
byte[] otherGunner = rows[26].Properties.ToArray(); BinaryPrimitives.WriteInt32LittleEndian(otherGunner, 28);
Refused(() => UniversalIndexedRows.Capture(dw.Data, dw.Overlay, rows[26] with { Properties = otherGunner }), "a cannon naming another fool");

// 3. Adding the cannon (or its gunner) brings the pair; a fool comes alone.
foreach (int root in new[] { 26, 27 })
{
    var g = UniversalPlacementGroups.Expand(lib.Get, new UniversalMobyPlacement(Key, root, 70000, 50000, 25600, EditorId: "root" + root, PersistentSlot: 0));
    Check(g.Count == 2 && g.Select(p => p.SourceRowIndex).Order().SequenceEqual([26, 27]) &&
          !string.IsNullOrEmpty(g[0].LinkGroupId) && g.All(p => p.LinkGroupId == g[0].LinkGroupId), $"row {root} expands to the cannon and its gunner");
}
foreach (int r in UniversalDreamWeaversCannonRows.FoolRows)
    Check(UniversalPlacementGroups.Expand(lib.Get, new UniversalMobyPlacement(Key, r, 70000, 50000, 25600)).Single().LinkGroupId == null, $"fool row {r} is added alone");

// 4. Binding by group.
var ts = lib.Get("townsquare");
HashSet<int> none = [];
UniversalMobyPlacement P(int row, string? group, int slot) => new(Key, row, 70000 + slot * 512, 50000, 25600, EditorId: "p" + slot, PersistentSlot: slot) { LinkGroupId = group };
UniversalMobyPlacement[] set = [P(26, "g", 0), P(27, "g", 1), P(19, "g", 2), P(18, "g", 3), P(20, "g", 4), P(13, null, 5), P(19, null, 6), P(12, "other", 7)];
UniversalRowTemplate Bind(int i, string destination = "townsquare", UniversalLevelData? data = null, IReadOnlySet<int>? removed = null) =>
    UniversalDreamWeaversCannonRows.Bind(Capture(set[i].SourceRowIndex), set[i], set, destination, data ?? ts.Data, removed ?? none, dw.Data);
var b19 = Bind(2);
Check(b19.SourceRowIndices.SequenceEqual([new(0x40, 26, 0x5B), new(0x54, 18, 0x82)]) && I(b19.Properties, 0x58) == -1,
    "a fool in the cannon's group names the cannon and its partner there; the absent partner is cleared");
var b18 = Bind(3);
Check(b18.SourceRowIndices.SequenceEqual([new(0x40, 26, 0x5B)]) && I(b18.Properties, 0x54) == -1, "partner 17 is not in the group");
var b20 = Bind(4);
Check(b20.SourceRowIndices.SequenceEqual([new(0x40, 26, 0x5B)]) && b20.Properties.SequenceEqual(rows[20].Properties) && b20.OwnRowIndexOffsets.Count == 0, "row 20 joins the cannon unchanged");
Check(Bind(0).Properties.SequenceEqual(rows[26].Properties) && Bind(1).Properties.SequenceEqual(rows[27].Properties), "the pair binds unchanged");
foreach (int i in new[] { 5, 6, 7 })
{
    var n = Bind(i); var source = rows[set[i].SourceRowIndex];
    Check(n.SourceRowIndices.Count == 0 && I(n.Properties, 0x3C) == 0 && n.OwnRowIndexOffsets.SequenceEqual([0x40]), $"fool {set[i].SourceRowIndex} without a cannon never calls and names itself");
    Check(n.ClassId != 0x82 || I(n.Properties, 0x54) == -1 && I(n.Properties, 0x58) == -1, "and forgets its partners");
    Check(Enumerable.Range(0, source.Properties.Length / 4).Select(w => w * 4).Where(o => I(source.Properties, o) != I(n.Properties, o)).All(o => o is 0x3C or 0x40 or 0x54 or 0x58),
        "only the call, cannon and partner words change");
}
var own = Bind(5, Key, dw.Data);
Check(own.Properties.SequenceEqual(rows[13].Properties) && own.SourceRowIndices.Count == 0 && own.OwnRowIndexOffsets.Count == 0, "a copy in Dream Weavers keeps the level's own cannon");
var own19 = Bind(6, Key, dw.Data, new HashSet<int> { 17 });
Check(I(own19.Properties, 0x54) == 18 && I(own19.Properties, 0x58) == -1 && I(own19.Properties, 0x3C) == 1000, "a copy in Dream Weavers loses a removed native partner only");
var noCannon = Bind(5, Key, dw.Data, new HashSet<int> { 26, 27 });
Check(I(noCannon.Properties, 0x3C) == 0 && noCannon.OwnRowIndexOffsets.Count == 1, "a copy in Dream Weavers without its cannon never calls");
Refused(() => UniversalDreamWeaversCannonRows.Bind(cannon, P(26, null, 0), [P(26, null, 0), P(27, null, 1)], "townsquare", ts.Data, none, dw.Data), "an ungrouped cannon");
Refused(() => UniversalDreamWeaversCannonRows.Bind(cannon, P(26, "g", 0), [P(26, "g", 0)], "townsquare", ts.Data, none, dw.Data), "a cannon without its gunner");
Refused(() => UniversalDreamWeaversCannonRows.Bind(gunner, P(27, "g", 1), [P(27, "g", 1)], "townsquare", ts.Data, none, dw.Data), "a gunner without its cannon");
Refused(() => UniversalDreamWeaversCannonRows.Bind(gunner, P(27, "g", 1), [P(26, "g", 0), P(27, "g", 1), P(27, "g", 2)], "townsquare", ts.Data, none, dw.Data), "two gunners");

// 5. Removing Dream Weavers' own rows.
void Remove(params int[] removed) => UniversalIndexedRows.RequireNativeRemoval(Key, dw.Data, dw.Data, dw.Overlay, removed.ToHashSet());
Refused(() => Remove(26), "the cannon under its gunner and the calling fools");
Refused(() => Remove(27), "the gunner under its cannon");
Refused(() => Remove(26, 27), "the pair under calling fools");
Refused(() => Remove(17), "partner 17 under calling fools 18/19");
Remove(20, 21); checks++;
Remove([.. fools.Select(t => t.SourceRowIndex), 26]); checks++;

// 6. Town Square: the UiSmoke step's Cloudspire Isles cannon set (editor units, x16 in the game) beside fools without a
// cannon, composed with every native row removed (as Start Blank leaves it).
const int IslandMinX = 4052, IslandMaxX = 4768, IslandMinY = 2640, IslandMaxY = 3360;
(int X, int Y, int Z) CannonAt = (4290, 3000, 1600), GunnerAt = (4196, 3000, 1600), Fool20At = (4140, 2990, 1600), Fool21At = (4143, 2888, 1600);
var tsHost = UniversalCompositionHost.Capture(args[0], catalog.FindByKey("townsquare")!);
int tsRows = tsHost.DestinationData.Scene().RowCount;
var pair = UniversalPlacementGroups.Expand(lib.Get, new UniversalMobyPlacement(Key, 26, CannonAt.X * 16, CannonAt.Y * 16, CannonAt.Z * 16, Yaw: 0, EditorId: "cannon", PersistentSlot: 0));
string cannonGroup = pair[0].LinkGroupId!;
List<UniversalMobyPlacement> layout =
[
    pair[0], pair[1] with { X = GunnerAt.X * 16, Y = GunnerAt.Y * 16, Z = GunnerAt.Z * 16 },
    new(Key, 20, Fool20At.X * 16, Fool20At.Y * 16, Fool20At.Z * 16, EditorId: "fool20", PersistentSlot: 2) { LinkGroupId = cannonGroup },
    new(Key, 21, Fool21At.X * 16, Fool21At.Y * 16, Fool21At.Z * 16, EditorId: "fool21", PersistentSlot: 3) { LinkGroupId = cannonGroup },
    new(Key, 12, 3300 * 16, 3900 * 16, 1790 * 16, EditorId: "winged1", PersistentSlot: 4),
    new(Key, 12, 3700 * 16, 4100 * 16, 1790 * 16, EditorId: "winged2", PersistentSlot: 5),
    new(Key, 13, 3000 * 16, 3000 * 16, 1600 * 16, EditorId: "armored", PersistentSlot: 6),
];
var (composed, error) = tsHost.DryRun(lib, layout, removedRows: Enumerable.Range(0, tsRows).ToArray());
Check(composed != null, "Town Square composes the cannon set: " + error);
var tsData = new UniversalLevelData(composed!.LevelData);
int[] at = [.. composed.NewRowIndices];
byte[] Props(int i) => tsData.CaptureRow("townsquare", at[i]).Properties;
Check(I(Props(0), 4) == at[1] && I(Props(1), 0x40) == at[0], "the cannon and its gunner name each other's new rows");
Check(I(Props(2), 0x40) == at[0] && I(Props(3), 0x40) == at[0] && I(Props(2), 0x3C) == 0 && I(Props(3), 0x3C) == 0, "fools 20/21 name the cannon and never call");
foreach (int i in new[] { 4, 5, 6 })
    Check(I(Props(i), 0x3C) == 0 && I(Props(i), 0x40) == at[i] && (layout[i].SourceRowIndex != 13 || I(Props(i), 0x54) == -1 && I(Props(i), 0x58) == -1),
        $"{layout[i].EditorId}: no cannon in its group, never calls, names only itself");
Check(composed.RestoredIndexedObjectLinks == 4, "four row links restored");
Check(tsData.Row(at[0])[0x46] == 0, "the cannon faces +x (yaw 0), at the fools' patrol line");
var dwClasses = composed.Classes.Where(c => c.SourceLevelKey == Key).ToArray();
Check(new[] { 0x5B, 0x5C, 0x82, 0x68, 0x4D }.All(c => dwClasses.Any(x => x.ClassId == c)) && dwClasses.All(c => c.DestinationClassId == c.ClassId),
    "Town Square keeps Dream Weavers' class numbers, so the cannon's own class tests (0xFA, 0x6E, 0x5C, 0x82) still match");
Check(dwClasses.Single(c => c.ClassId == 0x5B).BehaviorImported && dwClasses.Single(c => c.ClassId == 0x68).BehaviorImported, "cannon and cannonball behaviour copied");
var tsBehavior = composed.Behavior.Single(b => b.SourceLevelKey == Key);
Check(!tsBehavior.LinkedClassReadSites.Intersect(UniversalDreamWeaversCannonRows.CannonClassReads).Any(), "no class translation needed in Town Square");
List<object> moved = [];
foreach (int i in new[] { 2, 3 })
{
    var source = rows[layout[i].SourceRowIndex]; byte[] props = Props(i);
    int[] delta = [layout[i].X - I(source.Row, 0x0C), layout[i].Y - I(source.Row, 0x10), layout[i].Z - I(source.Row, 0x14)];
    var points = UniversalPropertyCoordinates.Points(source);
    Check(points.Count == 3, "trigger centre and two path points");
    foreach (int o in points)
    {
        int x = I(props, o), y = I(props, o + 4), z = I(props, o + 8);
        Check(x == I(source.Properties, o) + delta[0] && y == I(source.Properties, o + 4) + delta[1] && z == I(source.Properties, o + 8) + delta[2], "a point moves with its fool");
        Check(x / 16 > IslandMinX && x / 16 < IslandMaxX && y / 16 > IslandMinY && y / 16 < IslandMaxY, $"fool {layout[i].SourceRowIndex} point +{o:X} stays on the island ({x / 16},{y / 16})");
        moved.Add(new { Fool = layout[i].SourceRowIndex, Offset = o, X = x / 16, Y = y / 16, Z = z / 16 });
    }
}
const int Orbit = 0x5DC / 16; // the gunner keeps this far from the cannon
Check(CannonAt.X - Orbit > IslandMinX + 30 && CannonAt.X + Orbit < IslandMaxX - 30 && CannonAt.Y - Orbit > IslandMinY + 30 && CannonAt.Y + Orbit < IslandMaxY - 30,
    "the gunner's circle round the cannon stays on the island");
foreach (var p in layout.Take(4)) Check(p.X / 16 > IslandMinX && p.X / 16 < IslandMaxX && p.Y / 16 > IslandMinY && p.Y / 16 < IslandMaxY, "the set stands on the island");

// 7. Doctor Shemp runs its own classes 0x4D/0x5B/0x5C/0x68/0x82, so the whole set is renumbered there: the cannon's two
// class reads (dragons skipped, fools aimed higher) are translated back to Dream Weavers' numbers.
var dsHost = UniversalCompositionHost.Capture(args[0], catalog.FindByKey("doctorshemp")!);
int dsRows = dsHost.DestinationData.Scene().RowCount;
var dsPair = UniversalPlacementGroups.Expand(lib.Get, new UniversalMobyPlacement(Key, 26, 40000, 40000, 8000, EditorId: "ds-cannon", PersistentSlot: 0));
List<UniversalMobyPlacement> dsLayout = [.. dsPair, new(Key, 20, 36000, 40000, 8000, EditorId: "ds-fool", PersistentSlot: 2) { LinkGroupId = dsPair[0].LinkGroupId }];
var (shemp, shempError) = dsHost.DryRun(lib, dsLayout, removedRows: Enumerable.Range(0, dsRows).ToArray());
Check(shemp != null, "Doctor Shemp composes the cannon set: " + shempError);
var shempClasses = shemp!.Classes.Where(c => c.SourceLevelKey == Key).ToArray();
Check(new[] { 0x5B, 0x5C, 0x82, 0x68 }.All(c => shempClasses.Single(x => x.ClassId == c).DestinationClassId != c), "Doctor Shemp renumbers the cannon, the fools and the cannonball");
Check(shemp.Behavior.Single(b => b.SourceLevelKey == Key).LinkedClassReadSites.Intersect(UniversalDreamWeaversCannonRows.CannonClassReads).Count() == 2,
    "both cannon class reads translated back to Dream Weavers' numbers");
var dsData = new UniversalLevelData(shemp.LevelData);
Check(I(dsData.CaptureRow("doctorshemp", shemp.NewRowIndices[2]).Properties, 0x40) == shemp.NewRowIndices[0], "the Doctor Shemp fool names its cannon");

// 8. A copy inside Dream Weavers keeps calling the level's own cannon.
var dwHost = UniversalCompositionHost.Capture(args[0], catalog.FindByKey(Key)!);
var (ownLevel, ownError) = dwHost.DryRun(lib, [new UniversalMobyPlacement(Key, 13, 100000, 66000, 13296, EditorId: "own", PersistentSlot: 0)]);
Check(ownLevel != null, "Dream Weavers composes its own fool: " + ownError);
var ownProps = new UniversalLevelData(ownLevel!.LevelData).CaptureRow(Key, ownLevel.NewRowIndices[0]).Properties;
Check(I(ownProps, 0x40) == 26 && I(ownProps, 0x3C) == 11000, "the own-level copy still calls row 26, the native cannon");

File.WriteAllText(report, JsonSerializer.Serialize(new
{
    checks,
    townSquare = new { rows = at, composed.RestoredIndexedObjectLinks, classes = dwClasses.Select(c => $"{c.ClassId:X3}->{c.DestinationClassId:X3}"), movedPoints = moved },
    doctorShemp = new { classes = shempClasses.Select(c => $"{c.ClassId:X3}->{c.DestinationClassId:X3}"), sites = shemp.Behavior.Single(b => b.SourceLevelKey == Key).LinkedClassReadSites.Select(s => $"{s:X8}") },
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"UNIVERSAL_DREAMWEAVERS_CANNON_SOURCE_OK checks={checks}");
