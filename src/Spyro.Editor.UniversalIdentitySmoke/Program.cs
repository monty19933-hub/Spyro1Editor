using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

if (args.Length == 1 && args[0] == "--emit-egg-update")
{
    const uint address = 0x800A0000;
    Console.WriteLine(JsonSerializer.Serialize(new { address, classes = UniversalDragonflyEggs.Variants.Select(v => v.Class).ToArray(),
        code = Convert.ToHexString(UniversalSpecialBehaviors.DragonflyEggUpdateCode(UniversalDragonflyEggs.Variants.Select(v => v.Class).ToArray(), address)) }));
    return 0;
}

if (args.Length == 2 && args[0] == "--emit-progress")
{
    int total = int.Parse(args[1]);
    Console.WriteLine(JsonSerializer.Serialize(new {
        countAddress = UniversalEggProgress.CountAddress,
        percentAddress = UniversalEggProgress.PercentageAddress,
        count = Convert.ToHexString(UniversalEggProgress.CountRoutine(total)),
        percent = Convert.ToHexString(UniversalEggProgress.PercentageRoutine(total))
    }));
    return 0;
}

string root = Path.Combine(Path.GetTempPath(), "spyro-identity-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
int checks = 0;
void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
void Refuses(Action run, string message) { try { run(); } catch (InvalidDataException) { checks++; return; } throw new Exception(message); }
Moby Egg(int index) => new() { Index = index, TrueIndex = index, IsAdded = true,
    CrossLevelRequiredExporterFeature = UniversalMobyExport.RequiredExporterFeature,
    CrossLevelTemplateId = UniversalSpecialCatalog.TemplatePrefix + "dragonfly-egg-blue",
    CrossLevelSourceLevelKey = "japan", CrossLevelSourceTrueIndex = -1 };
try
{
    string path = Path.Combine(root, "toasty-native-edits.json");
    var a = Egg(200); var b = Egg(201); var c = Egg(202);
    await MobyEditStore.SaveAsync(path, [a,b,c], "Toasty");
    var first = UniversalPlacementReader.Read(path);
    Check(first.Select(p => p.PersistentSlot).SequenceEqual(new int?[] {0,1,2}), "first save assigns slots");
    var ids = first.Select(p => p.EditorId).ToArray();
    Check(ids.Distinct().Count() == 3, "placed eggs have unique identities");
    List<Moby> reopened = [];
    MobyEditStore.Load(path, reopened);
    Check(reopened.Select(m => m.UniversalPlacementId).SequenceEqual(ids), "save/reopen retains identities");
    await MobyEditStore.SaveAsync(path, [c,a], "Toasty");
    var removed = UniversalPlacementReader.Read(path);
    Check(removed.Count == 3 && removed[1].Retired, "deletion retains collected slot tombstone");
    Check(removed[0].EditorId == ids[0] && removed[2].EditorId == ids[2], "reorder preserves others' native slots");
    var copy = Egg(203); // a duplicate always receives a new identity
    await MobyEditStore.SaveAsync(path, [copy,c,a], "Toasty");
    var duplicate = UniversalPlacementReader.Read(path);
    Check(duplicate.Count == 4 && duplicate[3].EditorId == copy.UniversalPlacementId, "new copy uses a new lifetime slot");
    Check(duplicate[1].Retired && duplicate[3].PersistentSlot == 3, "retired bit never reused");
    c.CrossLevelTemplateId = UniversalSpecialCatalog.TemplatePrefix + "dragonfly-egg-red";
    await MobyEditStore.SaveAsync(path, [b,a,c,copy], "Toasty");
    var restored = UniversalPlacementReader.Read(path);
    Check(!restored[1].Retired && restored[1].EditorId == ids[1], "undo restores same identity");
    Check(restored[2].EditorId == ids[2] && restored[2].SpecialId == "dragonfly-egg-red", "colour keeps collection identity");
    var bad = Egg(203); bad.UniversalPlacementId = a.UniversalPlacementId;
    byte[] before = File.ReadAllBytes(path);
    Refuses(() => UniversalPlacementLedger.Reconcile(path, [a,bad]), "duplicate identities must be refused");
    try { await MobyEditStore.SaveAsync(path, [a], "Toasty", new CancellationToken(true)); throw new Exception("cancel expected"); }
    catch (OperationCanceledException) { Check(File.ReadAllBytes(path).SequenceEqual(before), "cancel preserves exact saved ledger and objects"); }
    await MobyEditStore.SaveAsync(path, [], "Toasty");
    Check(UniversalPlacementReader.Read(path).Count == 4 && UniversalPlacementReader.Read(path).All(p => p.Retired), "deleting all eggs preserves history and removes active total");
    UniversalNativeLayoutStore.Bind(root, "toasty", 180);
    Check(PortableProjectMigration.Classify("toasty-universal-layout-v1.json") == PortableProjectArtifactKind.ProjectEdit,
        "portable projects retain the binding that prevents collected-bit shifts");
    UniversalNativeLayoutStore.Validate(root, "toasty", 180);
    Refuses(() => UniversalNativeLayoutStore.Validate(root,"toasty",181), "prefix shift must not move saved bits");
    Refuses(() => new UniversalPlacementLedger(1,[new(a.UniversalPlacementId,1)]).Validate(), "sparse ledger refused");
    // Migrate a beta52 file using its original on-disk order BEFORE applying deletion/reordering.
    using var current = JsonDocument.Parse(before);
    var legacyEdits = current.RootElement.GetProperty("edits").EnumerateArray().Select(e =>
        e.EnumerateObject().Where(p => p.Name != "universalPlacementId").ToDictionary(p => p.Name,p => p.Value.Clone())).ToArray();
    string legacy = Path.Combine(root,"legacy-native-edits.json");
    File.WriteAllText(legacy,JsonSerializer.Serialize(new { edits = legacyEdits }));
    var baseline = UniversalPlacementReader.Read(legacy);
    List<Moby> legacyRows=[]; MobyEditStore.Load(legacy,legacyRows);
    await MobyEditStore.SaveAsync(legacy,legacyRows.Skip(1).Reverse(),"Toasty");
    var migrated = UniversalPlacementReader.Read(legacy);
    Check(migrated.Count == baseline.Count && migrated[0].Retired, "legacy deletion preserves prior exported order");
    Check(migrated.Select(p=>p.EditorId).SequenceEqual(baseline.Select(p=>p.EditorId)),"legacy identities preserved across migration");
    Console.WriteLine($"UNIVERSAL_IDENTITY_OK checks={checks} nativeRuntime=false cardWrites=false");
    return 0;
}
finally { Directory.Delete(root,true); }
