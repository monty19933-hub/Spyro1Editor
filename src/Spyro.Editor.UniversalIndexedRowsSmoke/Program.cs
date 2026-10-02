using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Universal;

int checks = 0;
void Check(bool ok, string label) { checks++; if (!ok) throw new InvalidOperationException(label); }
void Refused(Action action, string label) { bool refused = false; try { action(); } catch (InvalidDataException) { refused = true; } Check(refused, label); }
if (args.Length != 3) throw new ArgumentException("Supply original USA image, catalog workspace, and new report path.");
UniversalSourceLibrary.RequireOriginal(args[0]);
var lib = new UniversalSourceLibrary(args[0], LevelCatalog.Load(args[1])); var s = lib.Get("gnorccove");
byte[] originalData = s.Data.Bytes.ToArray(), originalOverlay = s.Overlay.Bytes.ToArray();
UniversalRowTemplate Capture(int row) => UniversalIndexedRows.Capture(s.Data, s.Overlay, s.Data.CaptureRow("gnorccove", row));
var cf = Enumerable.Range(0, s.Data.Scene().RowCount).Where(r => BinaryPrimitives.ReadUInt16LittleEndian(s.Data.Row(r).AsSpan(0x36)) == 0xCF).Select(Capture).ToArray();
Check(cf.Length == 11, "All eleven original placed CF rows enrolled");
foreach (var t in cf)
{
    var link = t.SourceRowIndices.Single();
    Check(link.Offset == 0x10 && link.SourceRow == BinaryPrimitives.ReadInt32LittleEndian(t.Properties.AsSpan(0x10)), "Metadata owns exact original row integer");
    Check(link.SourceClass == BinaryPrimitives.ReadUInt16LittleEndian(s.Data.Row(link.SourceRow).AsSpan(0x36)), "Exact target class recorded");
    var target = Capture(link.SourceRow); var resolved = UniversalIndexedRows.Resolve(t, new[] { target, t });
    Check(resolved.ImportedRowIndices.Single().PlacementIndex == 0, "Target resolved by source identity after ordering");
    Refused(() => UniversalIndexedRows.Resolve(t, new[] { t }), "Missing target refuses");
    Refused(() => UniversalIndexedRows.Resolve(t, new[] { t, target, target }), "Duplicate target refuses");
    Refused(() => UniversalIndexedRows.Resolve(t, new[] { t, target with { SourceLevelKey = "stonehill" } }), "Same row integer from another source refuses");
    byte[] wrongClass = target.Row.ToArray(); BinaryPrimitives.WriteUInt16LittleEndian(wrongClass.AsSpan(0x36), 0x18);
    Refused(() => UniversalIndexedRows.Resolve(t, new[] { t, target with { Row = wrongClass } }), "Changed target class refuses");
    byte[] inactive = t.Properties.ToArray(); BinaryPrimitives.WriteInt32LittleEndian(inactive.AsSpan(0x10), -1);
    Refused(() => UniversalIndexedRows.Capture(s.Data, s.Overlay, t with { Properties = inactive }), "Placed row cannot borrow spawned minus-one sentinel");
}
var family = Enumerable.Range(0, s.Data.Scene().RowCount).Where(r => BinaryPrimitives.ReadUInt16LittleEndian(s.Data.Row(r).AsSpan(0x36)) is 0xC7 or 0xC9 or 0xCA or 0xCF).Select(Capture).ToArray();
Check(family.Length == 48, "All48 reciprocal Gnorc placed index owners enrolled");
foreach (var t in family)
{
    var link = t.SourceRowIndices.Single(); var other = Capture(link.SourceRow);
    Check(other.SourceRowIndices.Single().SourceRow == t.SourceRowIndex, "Every original linked relationship is reciprocal");
    var resolved = UniversalIndexedRows.Resolve(t, new[] { t, other });
    Check(resolved.ImportedRowIndices.Single().PlacementIndex == 1, "Cycles resolve from unique source identities");
    Refused(() => UniversalIndexedRows.Resolve(t, new[] { t }), "All family missing targets refuse");
}
Check(Capture(0).SourceRowIndices.Count == 0, "Unrelated class properties remain outside enrollment");
var owner = cf[0]; var targetRaw = Capture(owner.SourceRowIndices.Single().SourceRow);
var targetRow = UniversalIndexedRows.Resolve(targetRaw, new[] { owner, targetRaw });
foreach (uint pc in new uint[] { 0x80081738, 0x8008173C, 0x80081740, 0x80081758, 0x80081764, 0x800819FC, 0x80081A04, 0x80081A1C, 0x80081A28, 0x80085C04, 0x80085C28, 0x80085C40, 0x80081734, 0x80085C44 })
{
    byte[] mutated = originalOverlay.ToArray(); mutated[(int)(pc - UniversalOverlay.Base)] ^= 1;
    var overlay = new UniversalOverlay(mutated, s.Overlay.Bindings);
    Refused(() => UniversalIndexedRows.Capture(s.Data, overlay, owner), "Changed producer/consumer/load-delay/incoming witness refuses");
}
foreach (uint global in new[] { UniversalExeBindings.UpdateGlobal, UniversalExeBindings.InitGlobal, UniversalExeBindings.CopyBufferGlobal })
{
    var bindings = s.Overlay.Bindings.ToDictionary(x => x.Key, x => x.Value); var originalBinding = bindings[global];
    bindings[global] = originalBinding with { Value = originalBinding.Value + 4 };
    Refused(() => UniversalIndexedRows.Capture(s.Data, new UniversalOverlay(originalOverlay.ToArray(), bindings), owner), "Changed source callback binding refuses");
}
byte[] mutatedData = originalData.ToArray(); mutatedData[0x200] ^= 1;
Refused(() => UniversalIndexedRows.Capture(new(mutatedData), s.Overlay, owner), "Changed source data refuses");
Refused(() => UniversalIndexedRows.Capture(s.Data, s.Overlay, owner with { SourceRowIndex = 35 }), "Template/source row identity mutation refuses");

UniversalLevelData Fixture()
{
    const int so = 0x2000, mob = 0xA4, mobSize = 0x1000, count = 4, prop = mob + mobSize;
    byte[] data = new byte[0x4000]; void Put(int at, int value) => BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(at), value);
    for (int i = 0; i < 4; i++) { Put(i * 8, 0x800 + i * 0x800); Put(i * 8 + 4, i == 3 ? 0x2000 : 0x800); }
    int cursor = so + 0x88; for (int i = 0; i < 7; i++) { Put(cursor, 4); cursor += 4; }
    Put(so + mob, mobSize); Put(so + mob + 4, count); Put(so + prop, 4 + count * 24);
    for (int i = 0; i < count; i++) { int row = so + mob + 8 + i * UniversalLevelData.RowStride; Put(row, prop + 4 + i * 24); BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(row + 0x36), 0x18); }
    int pod = prop + 4 + count * 24; Put(so + pod, 4); Put(so + pod + 4, 4); Put(so + pod + 8, count);
    for (int i = 0; i < count; i++) Put(so + pod + 12 + i * 4, mob + 8 + i * UniversalLevelData.RowStride);
    return new(data);
}
List<object> readbacks = [];
foreach (bool reversed in new[] { false, true })
foreach (int[] removed in new[] { Array.Empty<int>(), new[] { 1, 3 }, new[] { 2, 3 }, new[] { 3 }, new[] { 0, 1, 2, 3 } })
{
    var dest = Fixture(); var templates = reversed ? new[] { targetRow, owner } : new[] { owner, targetRow };
    var resolved = templates.Select(t => UniversalIndexedRows.Resolve(t, templates)).ToArray();
    UniversalRowRemoval.RemoveOwnedPropertiesAndClearRows(dest, removed);
    var appended = dest.AppendRows(resolved.Select(t => (t.Row, t)).ToArray(), removed);
    int ownerPlacement = reversed ? 1 : 0, targetPlacement = reversed ? 0 : 1;
    int physicalOwner = appended.NewRowIndices[ownerPlacement], physicalTarget = appended.NewRowIndices[targetPlacement];
    var recaptured = dest.CaptureRow("fixture", physicalOwner);
    Check(BinaryPrimitives.ReadInt32LittleEndian(recaptured.Properties.AsSpan(0x10)) == physicalTarget, "Readback stores final physical target after removal/reorder");
    Check(physicalTarget != owner.SourceRowIndices.Single().SourceRow, "No accidental original numeric target used");
    var sc = dest.Scene(); int props = BinaryPrimitives.ReadInt32LittleEndian(dest.Row(physicalOwner));
    Check(!sc.Fixups.Contains(props + 0x10), "Integer link never entered scene pointer fixups");
    Check(BinaryPrimitives.ReadUInt16LittleEndian(dest.Row(physicalTarget).AsSpan(0x36)) == targetRow.ClassId, "Readback exact imported target class preserved");
    readbacks.Add(new { reversed, removed, physicalOwner, physicalTarget });
}
List<object> familyReadbacks = [];
foreach (var actor in family)
foreach (bool reverse in new[] { false, true })
{
    var other = Capture(actor.SourceRowIndices.Single().SourceRow);
    var pair = reverse ? new[] { other, actor } : new[] { actor, other };
    var resolved = pair.Select(t => UniversalIndexedRows.Resolve(t, pair)).ToArray();
    var finalRows = resolved.Select((t, i) => { byte[] row = t.Row.ToArray(); BinaryPrimitives.WriteUInt16LittleEndian(row.AsSpan(0x36), (ushort)(0x1E0 + i)); return (Row: row, Template: t); }).ToArray();
    var dest = Fixture(); int[] removed = [1, 3]; UniversalRowRemoval.RemoveOwnedPropertiesAndClearRows(dest, removed);
    var appended = dest.AppendRows(finalRows, removed);
    for (int i = 0; i < pair.Length; i++)
    {
        var link = pair[i].SourceRowIndices.Single(); var readback = dest.CaptureRow("fixture", appended.NewRowIndices[i]);
        int targetPlacement = 1 - i;
        Check(BinaryPrimitives.ReadInt32LittleEndian(readback.Properties.AsSpan(link.Offset)) == appended.NewRowIndices[targetPlacement], "All family reciprocal integer readbacks use final physical slots");
        Check(BinaryPrimitives.ReadUInt16LittleEndian(dest.Row(appended.NewRowIndices[targetPlacement]).AsSpan(0x36)) == 0x1E0 + targetPlacement, "Mapped destination class separate from authenticated source target class");
        int props = BinaryPrimitives.ReadInt32LittleEndian(dest.Row(appended.NewRowIndices[i]));
        Check(!dest.Scene().Fixups.Contains(props + link.Offset), "All family integer fields remain outside scene fixups");
    }
    familyReadbacks.Add(new { actor.SourceRowIndex, actor.ClassId, reverse, NewRows = appended.NewRowIndices });
}
var rootPlacement = new UniversalMobyPlacement("gnorccove", 34, 10000, 20000, 30000, Yaw: 77, EditorId: Guid.NewGuid().ToString("N"), PersistentSlot: 0);
var groupA = UniversalPlacementGroups.Expand(lib.Get, rootPlacement);
var groupB = UniversalPlacementGroups.Expand(lib.Get, rootPlacement with { EditorId = Guid.NewGuid().ToString("N"), PersistentSlot = 2 });
Check(groupA.Count == 2 && groupB.Count == 2, "Expansion follows reciprocal cycles once");
Check(groupA[0].Yaw == 77 && groupA[0].EditorId == rootPlacement.EditorId && groupA[1].Yaw == targetRaw.Row[0x46], "Expansion preserves root identity/yaw and companion native yaw");
Check(!string.IsNullOrEmpty(groupA[0].LinkGroupId) && groupA.All(p => p.LinkGroupId == groupA[0].LinkGroupId) && groupB[0].LinkGroupId != groupA[0].LinkGroupId, "Independent copies get distinct scoped groups");
Check(groupA.Select(p => p.PersistentSlot).SequenceEqual(new int?[] { 0, 1 }) && groupB.Select(p => p.PersistentSlot).SequenceEqual(new int?[] { 2, 3 }), "Expansion reserves contiguous child lifetime slots");
Check(groupA[1].X - groupA[0].X == BinaryPrimitives.ReadInt32LittleEndian(targetRaw.Row.AsSpan(0x0C)) - BinaryPrimitives.ReadInt32LittleEndian(owner.Row.AsSpan(0x0C)), "Expansion preserves companion relative coordinates");
var independent = groupA.Concat(groupB).ToArray();
var groupedTemplates = independent.Select(p => Capture(p.SourceRowIndex) with { LinkGroupId = p.LinkGroupId }).ToArray();
var groupedResolved = groupedTemplates.Select(t => UniversalIndexedRows.Resolve(t, groupedTemplates)).ToArray();
Check(groupedResolved[0].ImportedRowIndices.Single().PlacementIndex == 1 && groupedResolved[2].ImportedRowIndices.Single().PlacementIndex == 3, "Identical source pairs resolve within their own group");
var groupedDest = Fixture(); var groupedOutput = groupedDest.AppendRows(groupedResolved.Select(t => (t.Row, t)).ToArray());
for (int i = 0; i < groupedResolved.Length; i++)
{
    int other = i ^ 1; int offset = groupedResolved[i].SourceRowIndices.Single().Offset;
    Check(BinaryPrimitives.ReadInt32LittleEndian(groupedDest.CaptureRow("fixture", groupedOutput.NewRowIndices[i]).Properties.AsSpan(offset)) == groupedOutput.NewRowIndices[other], "Duplicated pair physical readback never crosses group");
}
Refused(() => UniversalIndexedRows.Resolve(groupedTemplates[0], new[] { groupedTemplates[0], groupedTemplates[3] }), "Matching numeric target in a different group refuses");
Refused(() => UniversalIndexedRows.Resolve(groupedTemplates[0] with { LinkGroupId = null }, groupedTemplates), "Legacy ungrouped duplicate source targets remain ambiguous");
Check(UniversalPlacementGroups.Expand(lib.Get, rootPlacement with { SourceRowIndex = 0 }).Single().LinkGroupId == null, "Unindexed single keeps legacy ungrouped identity");
string evidenceFolder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[2]))!, "group-persistence-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(evidenceFolder);
string nativePath = Path.Combine(evidenceFolder, "group-native-edits.json");
var mobys = independent.Select((p, i) => new Moby { Index = i, TrueIndex = i, IsAdded = true,
    Position = new Vector3f(p.X / 16f, p.Y / 16f, p.Z / 16f), OriginalPosition = new Vector3f(p.X / 16f, p.Y / 16f, p.Z / 16f),
    UniversalPlacementId = p.EditorId!, UniversalLinkGroupId = p.LinkGroupId!,
    CrossLevelTemplateId = $"universal:gnorccove:{p.SourceRowIndex}", CrossLevelSourceLevelKey = p.SourceLevelKey,
    CrossLevelSourceTrueIndex = p.SourceRowIndex, CrossLevelRequiredExporterFeature = UniversalMobyExport.RequiredExporterFeature }).ToList();
await MobyEditStore.SaveAsync(nativePath, mobys, "group-fixture");
var nativePlacements = UniversalPlacementReader.Read(nativePath);
Check(nativePlacements.Select(p => p.LinkGroupId).SequenceEqual(independent.Select(p => p.LinkGroupId)), "Native-edits placement reader preserves link groups");
List<Moby> loaded = []; MobyEditStore.Load(nativePath, loaded);
Check(loaded.Select(m => m.UniversalLinkGroupId).SequenceEqual(mobys.Select(m => m.UniversalLinkGroupId)), "Native-edits Moby reload preserves link groups");
string freshPath = Path.Combine(evidenceFolder, "group-fresh.scene.json");
var fresh = SceneDocument.New("Index Group") with { Objects = mobys.Select(m => new AuthoredObject(m.UniversalPlacementId, m)).ToArray() };
await fresh.SaveAsync(freshPath); var freshLoaded = SceneDocument.Read(freshPath).ProjectObjects();
Check(freshLoaded.Select(m => m.UniversalLinkGroupId).SequenceEqual(mobys.Select(m => m.UniversalLinkGroupId)), "Fresh document serializes each group identity");
var resolvedOwner = UniversalIndexedRows.Resolve(owner, new[] { owner, targetRow });
Refused(() => Fixture().AppendRows([(resolvedOwner.Row, resolvedOwner)]), "Final allocation missing target refuses");
var changed = resolvedOwner with { ImportedRowIndices = [resolvedOwner.ImportedRowIndices.Single() with { PlacementIndex = 0 }] };
Refused(() => Fixture().AppendRows([(changed.Row, changed), (targetRow.Row, targetRow)]), "Final allocation wrong target identity refuses");
var badProperties = resolvedOwner.Properties.ToArray(); BinaryPrimitives.WriteInt32LittleEndian(badProperties.AsSpan(0x10), 0);
Refused(() => Fixture().AppendRows([(resolvedOwner.Row, resolvedOwner with { Properties = badProperties }), (targetRow.Row, targetRow)]), "Index changed after enrollment refuses");
Refused(() => Fixture().AppendRows([(owner.Row, owner), (targetRow.Row, targetRow)]), "Unresolved source metadata cannot be appended");
Refused(() => Fixture().AppendRows([(resolvedOwner.Row, resolvedOwner), (targetRow.Row, targetRow), (targetRow.Row, targetRow)]), "Target duplicated after resolution refuses");
Refused(() => UniversalIndexedRows.RequireNativeRemoval("gnorccove", s.Data, s.Data, s.Overlay, new HashSet<int> { 32 }), "Surviving native row34 cannot inherit a reused target32 slot");
UniversalIndexedRows.RequireNativeRemoval("gnorccove", s.Data, s.Data, s.Overlay, new HashSet<int> { 32, 34 });
Check(true, "Removing native index owner and target together passes");
UniversalIndexedRows.RequireNativeRemoval("gnorccove", s.Data, s.Data, s.Overlay, new HashSet<int> { 0 });
Check(true, "Unrelated native removal preserves indexed targets");
var alteredNative = new UniversalLevelData(originalData.ToArray()); var sceneLayout = alteredNative.Scene();
int sceneStart = alteredNative.Subfiles()[3].Offset; int ownerProps = BinaryPrimitives.ReadInt32LittleEndian(alteredNative.Row(34));
alteredNative.Put32(sceneStart + ownerProps + 0x10, -1);
Refused(() => UniversalIndexedRows.RequireNativeRemoval("gnorccove", alteredNative, s.Data, s.Overlay, new HashSet<int> { 32 }), "Changed native index cannot borrow initialized sentinel during removal");
var hiddenOwner = new UniversalLevelData(originalData.ToArray()); hiddenOwner.SetRowClass(34, 0x18);
Refused(() => UniversalIndexedRows.RequireNativeRemoval("gnorccove", hiddenOwner, s.Data, s.Overlay, new HashSet<int> { 32 }), "Staged class change cannot hide native indexed owner");
Check(s.Data.Bytes.SequenceEqual(originalData) && s.Overlay.Bytes.SequenceEqual(originalOverlay), "Original source immutable after capture/mutation/readback");
UniversalSourceLibrary.RequireOriginal(args[0]);
File.WriteAllText(args[2], JsonSerializer.Serialize(new { checks, placedRows = cf.Length, reciprocalRows = family.Length, dataSha256 = UniversalIndexedRows.GnorcDataSha256, overlaySha256 = UniversalIndexedRows.GnorcOverlaySha256, readbacks, familyReadbacks, owners = family.Select(t => new { t.SourceRowIndex, t.ClassId, Links = t.SourceRowIndices }), Runtime = "not-tested", WrittenDiscReadback = "not-tested" }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"UNIVERSAL_INDEXED_ROWS_SOURCE_OK checks={checks} placedRows={cf.Length} readbacks={readbacks.Count}");
