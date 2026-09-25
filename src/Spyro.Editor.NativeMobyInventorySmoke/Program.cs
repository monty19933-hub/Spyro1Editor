using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

int checks = 0;
if (args.Length == 3 && args[0] == "--source")
{
    var inventory = NativeMobySourceInventory.Capture(args[1], LevelCatalog.Load(args[2]));
    SharedModelCases.VerifySource(inventory.SharedModels, Check);
    Check(inventory.Levels.Count == 35, "all retail levels");
    Check(inventory.Levels.Sum(l => l.Rows.Count) == 4877, "every placed native row retained");
    Check(inventory.Levels.Sum(l => l.Registrations.Count) == 1403, "all local registrations retained");
    Check(inventory.Classes.Count == 321, "union of placed and registered classes");
    Check(inventory.Classes.Count(c => c.PlacedRowCount > 0) == 205, "placed class union");
    Check(inventory.Classes.Count(c => c.RegistrationCount > 0) == 298, "registration class union");
    Check(inventory.Classes.Count(c => c.PlacedRowCount == 0) == 116, "root-only resources not hidden");
    Check(inventory.Levels.SelectMany(l => l.Rows).Count(r => !r.HasLocalModelRegistration) == 1990,
        "no local root does not erase keys, controllers or other special rows");
    Check(inventory.Levels.SelectMany(l => l.Rows).Count(r => r.PropertiesLocation == NativeMobyPropertiesLocation.ComponentEnd) == 17,
        "native one-past properties pointers retain unresolved ownership");
    Check(inventory.Levels.SelectMany(l => l.Rows).All(r => r.PropertiesPointer != 0 && r.AssociatedWord == 0),
        "all source pointer words captured independently");
    Check(inventory.DescribeDestination(0xAE, "townsquare").LocalModelStatus == NativeMobyLocalModelStatus.Absent,
        "original Town has no chest model; do not borrow the candidate's proof");
    Check(inventory.DescribeDestination(0xAD, "townsquare").LocalModelStatus == NativeMobyLocalModelStatus.Absent,
        "native key rendering cannot be inferred from registration absence");
    Check(inventory.DescribeDestination(0x11B, "magiccrafters").LocalModelStatus == NativeMobyLocalModelStatus.Present,
        "resident Wizard root discovered without family allowlist");
    Check(!inventory.DescribeDestination(0x11B, "magiccrafters").NativeImportApproved,
        "resident root alone is not import approval");
    Check(inventory.Levels.SelectMany(l => l.Rows).Select(r => r.SourceVariantId).Distinct().Count() == 4877,
        "every donor instance has a separate source identity");
    ExpectInvalid(() => inventory.DescribeDestination(ushort.MaxValue, "townsquare"));
    ExpectInvalid(() => inventory.DescribeDestination(0xAE, "unused65"));
    var catalogue = new NativeMobyCatalogue(inventory);
    Check(catalogue.Search().Count == 4877, "usable catalogue retains every placed source variant");
    Check(catalogue.Search(view: NativeMobyCatalogueView.Resources).Count == 1457, "local and shared resource browsing is separate from placed templates");
    Check(catalogue.Search(view: NativeMobyCatalogueView.Resources).Count(e => e.IsSpecialPlayerStream) == 35,
        "all special player streams explicitly labelled");
    Check(catalogue.Search().Select(e => e.SourceVariantId).Distinct().Count() == 4877, "catalogue never deduplicates display names");
    var wizard = catalogue.Search("0x011B", "wizardpeak").First();
    Check(catalogue.DescribeTarget(wizard, "magiccrafters").LocalModelStatus == NativeMobyLocalModelStatus.Present,
        "catalogue can inspect a different retail destination");
    Check(!catalogue.DescribeTarget(wizard, "magiccrafters").NativeImportApproved, "residency is never approval");
    Check(!catalogue.DescribeTarget(wizard, "unusedlevel65blank").RetailTargetInspected,
        "blank lab does not borrow retail residency");
    Check(catalogue.DescribeTarget(wizard, "townsquare").GenericRegistrationTailSlots == 27, "physical tail capacity excludes special slot zero");
    Check(catalogue.Search("a definitely nonexistent actor phrase").Count == 0, "search has meaningful no-result state");
    Check(catalogue.Search("wizard", "wizardpeak").All(e => e.SourceLevelKey == "wizardpeak"), "source filter remains independent from destination");
    var shared = catalogue.Search(sourceLevelKey: NativeMobyCatalogue.SharedSourceKey, view: NativeMobyCatalogueView.Resources);
    Check(shared.Count == 54 && shared.All(e => e.IsSharedResource && e.SourceLevelId == null && e.SourceWadEntry == null &&
        e.TrueIndex == null && !e.NativeImportApproved), "shared resources never pretend to belong to a level or placed row");
    Check(catalogue.Search(sourceLevelKey: NativeMobyCatalogue.SharedSourceKey).Count == 0,
        "shared models are never placed donor templates");
    var egg = shared.Single(e => e.ActorId == 0x22);
    Check(egg.SharedRootPeteOffset == 0x3382C && egg.RegistrationSlot == 3 &&
        egg.Label == "Retail dragon egg model" && !egg.Label.Contains("dragonfly", StringComparison.OrdinalIgnoreCase),
        "ordinary retail egg identity stays distinct from unverified Japanese dragonfly eggs");
    var eggTarget = catalogue.DescribeTarget(egg, "townsquare");
    Check(eggTarget.SharedModelRegistered && eggTarget.LocalModelStatus == NativeMobyLocalModelStatus.Absent &&
        !eggTarget.DependencyClosureEvaluated && !eggTarget.NativeImportApproved,
        "shared discovery is reported independently of local residency, behavior and import approval");
    Check(!egg.MatchesExistingTemplate("townsquare", 0, 0, 0, 0x22, 0, 0, 0, 0) &&
        catalogue.Search(view: NativeMobyCatalogueView.All).Select(e => e.SourceVariantId).Distinct().Count() == 6334,
        "all distinct observations retained; a shared root cannot select an existing template");
    var otherCatalogue = new NativeMobyCatalogue(inventory);
    ExpectInvalid(() => otherCatalogue.DescribeTarget(wizard, "magiccrafters"));
    Console.WriteLine($"NATIVE_MOBY_INVENTORY_SOURCE_SMOKE_OK checks={checks} levels=35 rows=4877 classes=321");
    return;
}
if (args.Length != 0) throw new ArgumentException("Use no arguments or --source <original.bin> <catalog-workspace>.");
SharedModelCases.Run(Check);

byte[] data = Fixture();
var level = Parse(data);
var catalogueEntries = NativeMobyCatalogue.DescribeLevel(level);
Check(catalogueEntries.Count == 4, "two rows plus two separate resource observations");
ExpectInvalid(() => NativeMobyCatalogue.SearchEntries(catalogueEntries, new string('x', 257), null, NativeMobyCatalogueView.All));
ExpectInvalid(() => NativeMobyCatalogue.SearchEntries(catalogueEntries, null, null, (NativeMobyCatalogueView)(-1)));
Check(NativeMobyCatalogue.SearchEntries(catalogueEntries, "", null, NativeMobyCatalogueView.All).Count == 4,
    "all view is the exact union without hiding unplaced resources");
Check(NativeMobyCatalogue.SearchEntries(catalogueEntries, "0x0149 fixture", null, NativeMobyCatalogueView.PlacedRows).Count == 1,
    "token search matches full native class and source level");
Check(NativeMobyCatalogue.SearchEntries(catalogueEntries, "0x0149", "elsewhere", NativeMobyCatalogueView.PlacedRows).Count == 0,
    "source-level filter cannot leak matches");
Check(catalogueEntries.All(e => !e.NativeImportApproved), "catalogue labels grant no export authority");
Check(catalogueEntries.Where(e => e.Kind != NativeMobyCatalogueEntryKind.PlacedRow).All(e =>
    !e.MatchesExistingTemplate("fixture", 0, 0x20, 0, 0x49, 1, 0, 0, 0)), "resource-only entries cannot select a placed template");
var catalogueRow = catalogueEntries.Single(e => e.Kind == NativeMobyCatalogueEntryKind.PlacedRow && e.TrueIndex == 0);
Check(catalogueRow.MatchesExistingTemplate("fixture", 0, 0x20, 0, 0x49, 1, 0, 0, 0), "exact existing template metadata can match its source row");
Check(!catalogueRow.MatchesExistingTemplate("fixture", 1, 0x20, 0, 0x49, 1, 0, 0, 0), "same class never substitutes another source row");
Check(!catalogueRow.MatchesExistingTemplate("fixture", 0, 0x20, 1, 0x49, 1, 0, 0, 0), "different state cannot borrow template match");
byte[] sameClassFixture = Fixture();
BinaryPrimitives.WriteUInt16LittleEndian(sameClassFixture.AsSpan(0x800 + 0xC8 + 88 + 0x36), 0x149);
var sameClassEntries = NativeMobyCatalogue.DescribeLevel(Parse(sameClassFixture)).Where(e => e.Kind == NativeMobyCatalogueEntryKind.PlacedRow).ToArray();
Check(sameClassEntries.Length == 2 && sameClassEntries[0].SourceVariantId != sameClassEntries[1].SourceVariantId,
    "same-class named variants retain distinct native ownership");
Check(level.Rows.Count == 2 && level.Registrations.Count == 2, "all rows and roots");
Check(level.Rows[0].ActorId == 0x149 && level.Rows[1].ActorId == 0x249, "full 16-bit classes");
Check(level.Rows[0].HasLocalModelRegistration && !level.Rows[1].HasLocalModelRegistration, "rootless row retained");
Check(level.Registrations.Any(r => r.ActorId == 0x26), "unplaced registered resource retained");
Check(level.Rows[0].PropertiesLocation == NativeMobyPropertiesLocation.InsideComponent, "owned pointer start");
Check(level.Rows[1].PropertiesLocation == NativeMobyPropertiesLocation.ComponentEnd, "end pointer is not an owned payload");
Check(level.Rows[1].AssociatedWord == 0x80001234, "associated word is not mislabelled properties");
Check(level.ReservedMobyArenaByteLength == 88, "arena separate from active rows");
Check(level.Rows[0].RecordSha256 == Hash(data.AsSpan(0x800 + 0xC8, 88)), "exact record binding");
Check(level.Rows[0].SourceVariantId != level.Rows[1].SourceVariantId, "per-instance identity");
byte[] copied = level.Rows[0].CopyRecord(); copied[0x36] = 0;
Check(level.Rows[0].CopyRecord()[0x36] == 0x49, "snapshot does not expose writable bytes");
data[0x800 + 0xC8 + 0x36] = 0;
Check(level.Rows[0].CopyRecord()[0x36] == 0x49, "source-array mutation cannot alter detached snapshot");
Check(Parse(Fixture()).SourceDataSha256 == level.SourceDataSha256, "deterministic capture");
foreach (int component in Enumerable.Range(0, 7))
{
    int offset = 0x800 + 0x88 + component * 8;
    Mutate(offset, 0, "zero sized preceding component");
    Mutate(offset, uint.MaxValue, "overflowing preceding component");
    Mutate(offset, 9, "unaligned preceding component");
}
Mutate(0x800 + 0xC0, 8, "rows do not fit their owner component");
Mutate(0x800 + 0xC4, uint.MaxValue, "unbounded count");
Mutate(0x18, 0xFFF, "scene alignment/ownership");
Mutate(0x1C, 0x1000, "scene out of bounds");
Mutate(0x50, 0x800, "model root cannot belong to scene");
Mutate(0x54, 0x710, "model registration alias is not silently collapsed");
Mutate(0x5C, 0x750, "registration after terminator needs valid class/storage contract");
byte[] bad = Fixture(); BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(0x152), 0x149);
ExpectInvalid(() => Parse(bad));
bad = Fixture(); BinaryPrimitives.WriteUInt16LittleEndian(bad.AsSpan(0x154), 9);
ExpectInvalid(() => Parse(bad));
bad = Fixture(); Put(bad, 0x800 + 0xC8, 0);
Check(Parse(bad).Rows[0].PropertiesLocation == NativeMobyPropertiesLocation.Zero, "zero recorded without claiming schema closure");
bad = Fixture(); Put(bad, 0x800 + 0xC8, uint.MaxValue);
Check(Parse(bad).Rows[0].PropertiesLocation == NativeMobyPropertiesLocation.OutsideComponent,
    "unknown pointer provenance retained as blocker, not fabricated payload");
var wrongCatalog = Definition() with { SourceRecordCount = 3 };
ExpectInvalid(() => NativeMobySourceInventory.DecodeLevel(Fixture(), wrongCatalog, 0x10000));
wrongCatalog = Definition() with { SourceTableRelativeOffset = "0x08CC", SourceTableWadOffset = "0x108CC" };
ExpectInvalid(() => NativeMobySourceInventory.DecodeLevel(Fixture(), wrongCatalog, 0x10000));
wrongCatalog = Definition() with { SourceTableWadOffset = "-1" };
ExpectInvalid(() => NativeMobySourceInventory.DecodeLevel(Fixture(), wrongCatalog, 0x10000));
ExpectInvalid(() => Parse(new byte[32]));
// Catalog rejection precedes opening any image; these cases remain source-free.
var catalogRows = Enumerable.Range(0, 35).Select(i => Definition() with
{
    Key = "fixture" + i, LevelId = (i / 6 + 1) * 10 + i % 6, SourceWadEntry = 10 + i * 2
}).ToArray();
ExpectInvalid(() => NativeMobySourceInventory.Capture("unused-no-image-needed.bin", new LevelCatalog(catalogRows.Take(34))));
ExpectInvalid(() => NativeMobySourceInventory.Capture("unused-no-image-needed.bin", new LevelCatalog(
    catalogRows.Select((l, i) => i == 0 ? l with { SourceWadEntry = 12 } : l))));
ExpectInvalid(() => NativeMobySourceInventory.Capture("unused-no-image-needed.bin", new LevelCatalog(
    catalogRows.Select((l, i) => i == 0 ? l with { LevelId = 16 } : l))));
Check(!level.NativeImportApproved, "inventory is discovery, never a writer certificate");
Console.WriteLine($"NATIVE_MOBY_INVENTORY_SMOKE_OK checks={checks}");

void Check(bool ok, string message) { checks++; if (!ok) throw new InvalidOperationException(message); }
void ExpectInvalid(Action action) { try { action(); } catch (InvalidDataException) { checks++; return; } throw new InvalidOperationException("Malformed inventory input accepted."); }
void Mutate(int offset, uint value, string reason) { byte[] changed = Fixture(); Put(changed, offset, value); ExpectInvalid(() => Parse(changed)); }
static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
static void Put(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
static LevelDefinition Definition() => new() { Key = "fixture", DisplayName = "Fixture", LevelId = 13,
    SourceWadEntry = 16, SourceTableWadOffset = "0x108C8", SourceTableRelativeOffset = "0x8C8", SourceRecordCount = 2 };
static NativeMobySourceLevel Parse(byte[] bytes) => NativeMobySourceInventory.DecodeLevel(bytes, Definition(), 0x10000);
static byte[] Fixture()
{
    byte[] data = new byte[0x1000];
    Put(data, 0x10, 0x700); Put(data, 0x14, 0x100);
    Put(data, 0x18, 0x800); Put(data, 0x1C, 0x800);
    Put(data, 0x50, 0x710); Put(data, 0x54, 0x730);
    BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x150), 0x149);
    BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x152), 0x26);
    for (int i = 0; i < 7; i++) Put(data, 0x800 + 0x88 + i * 8, 8);
    Put(data, 0x800 + 0xC0, 8 + 3 * 88); Put(data, 0x800 + 0xC4, 2);
    int properties = 0xC0 + 8 + 3 * 88;
    Put(data, 0x800 + properties, 20);
    for (int i = 0; i < 2; i++)
    {
        int row = 0x800 + 0xC8 + i * 88;
        Put(data, row, checked((uint)(properties + (i == 0 ? 4 : 20))));
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(row + 0x36), (ushort)(0x149 + i * 0x100));
        data[row + 0x50] = 0x20;
    }
    Put(data, 0x800 + 0xC8 + 88 + 8, 0x80001234);
    return data;
}
