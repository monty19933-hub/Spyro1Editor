using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

int checks = 0;
if (args.Length == 3 && args[0] == "--source")
{
    var inventory = NativeMobySourceInventory.Capture(args[1], LevelCatalog.Load(args[2]));
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
    Console.WriteLine($"NATIVE_MOBY_INVENTORY_SOURCE_SMOKE_OK checks={checks} levels=35 rows=4877 classes=321");
    return;
}
if (args.Length != 0) throw new ArgumentException("Use no arguments or --source <original.bin> <catalog-workspace>.");

byte[] data = Fixture();
var level = Parse(data);
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
