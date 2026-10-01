using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

int checks = 0;
void Check(bool condition, string label) { checks++; if (!condition) throw new InvalidOperationException(label); }
int Group(byte[] row) => BinaryPrimitives.ReadInt16LittleEndian(row.AsSpan(0x34));
void Hole(byte[] row)
{
    Check(Group(row) == -1, "Unoccupied row uses native constructor's signed unlinked group");
    byte[] expected = new byte[88]; expected[0x34] = expected[0x35] = 255; expected[0x36] = 254; expected[0x37] = 1; expected[0x53] = 255;
    Check(row.SequenceEqual(expected), "Unoccupied row has no links, properties, actors, or reward beyond its explicit sentinels");
}
// Minimal owned scene fixture: four rows, no property links, padded fixed components.
byte[] fixture = new byte[0x2800];
for (int i = 0; i < 4; i++) { BinaryPrimitives.WriteInt32LittleEndian(fixture.AsSpan(i * 8), 0x800 + i * 0x800); BinaryPrimitives.WriteInt32LittleEndian(fixture.AsSpan(i * 8 + 4), 0x800); }
int sceneBase = 0x2000, cursor = sceneBase + 0x88;
for (int i = 0; i < 7; i++, cursor += 4) BinaryPrimitives.WriteInt32LittleEndian(fixture.AsSpan(cursor), 4);
int rows = cursor + 8; BinaryPrimitives.WriteInt32LittleEndian(fixture.AsSpan(cursor), 8 + 4 * 88); BinaryPrimitives.WriteInt32LittleEndian(fixture.AsSpan(cursor + 4), 4);
for (int i = 0; i < 4; i++) { BinaryPrimitives.WriteInt16LittleEndian(fixture.AsSpan(rows + i * 88 + 0x34), (short)(10 + i)); fixture[rows + i * 88 + 0x36] = 10; }
cursor += 8 + 4 * 88;
for (int i = 0; i < 3; i++, cursor += 4) BinaryPrimitives.WriteInt32LittleEndian(fixture.AsSpan(cursor), 4);
var synthetic = new UniversalLevelData((byte[])fixture.Clone());
UniversalRowRemoval.RemoveOwnedPropertiesAndClearRows(synthetic, [1, 3]); Hole(synthetic.Row(1)); Hole(synthetic.Row(3));
Check(synthetic.Row(0).SequenceEqual(fixture.AsSpan(rows, 88).ToArray()) && synthetic.Row(2).SequenceEqual(fixture.AsSpan(rows + 176, 88).ToArray()), "Surviving synthetic actor bytes stay exact");
var zero = UniversalRowRemoval.Allocate(synthetic, [1, 3], 0); Check(zero.FinalRowCount == 3 && zero.UnoccupiedRows.SequenceEqual([1]), "Trailing hole trims while interior hole remains");
var one = UniversalRowRemoval.Allocate(synthetic, [1, 3], 1); Check(one.NewRows.SequenceEqual([1]) && one.FinalRowCount == 3 && one.UnoccupiedRows.Count == 0, "Reused first hole becomes an active actor slot");
var two = UniversalRowRemoval.Allocate(synthetic, [1, 3], 2); Check(two.NewRows.SequenceEqual([1, 3]) && two.FinalRowCount == 4, "Second hole reuse preserves physical survivor indices");

if (args.Length == 3)
{
    UniversalSourceLibrary.RequireOriginal(args[0]);
    var lib = new UniversalSourceLibrary(args[0], LevelCatalog.Load(args[1])); var src = lib.Get("artisans");
    byte[] original = (byte[])src.Data.Bytes.Clone(); var data = new UniversalLevelData((byte[])original.Clone());
    int[] removed = [29, 30, 32, 33, 94, 109, 113, 121, 124];
    var before = removed.ToDictionary(i => i, data.Row);
    UniversalRowRemoval.RemoveOwnedPropertiesAndClearRows(data, removed);
    foreach (int r in removed) Hole(data.Row(r));
    for (int r = 0; r < data.Scene().RowCount; r++) if (!removed.Contains(r)) Check(Group(data.Row(r)) == Group(src.Data.Row(r)), $"Source survivor {r} group remains exact");
    var allocation = UniversalRowRemoval.Allocate(data, removed, 1);
    Check(allocation.NewRows.SequenceEqual([29]) && allocation.UnoccupiedRows.SequenceEqual(removed.Skip(1)), "Exact failed export leaves row30 and seven other unoccupied holes after one import");
    var sf = data.Subfiles(); var old = src.Data.Subfiles();
    for (int i = 0; i < sf.Count; i++) if (i != 3) Check(data.Bytes.AsSpan(sf[i].Offset, sf[i].Length).SequenceEqual(original.AsSpan(old[i].Offset, old[i].Length)), "Deletion preserves each non-scene subfile");
    Check(src.Data.Bytes.SequenceEqual(original), "Authenticated source data immutable");
    byte[] Window(uint pc, int length) => lib.Disc.Exe.AsSpan((int)(pc - UniversalExeBindings.ExeBase), length).ToArray();
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
    File.WriteAllText(args[2], JsonSerializer.Serialize(new {
        sourceImageSha256 = UniversalSourceLibrary.OriginalUsaSha256,
        sourceExecutableSha256 = Convert.ToHexStringLower(SHA256.HashData(lib.Disc.Exe)),
        coreSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(UniversalRowRemoval).Assembly.Location))),
        killAddress = 0x80052568u, kill = Convert.ToHexString(Window(0x80052568, 0x64)),
        simpleConstructorAddress = 0x800529CCu, simpleConstructor = Convert.ToHexString(Window(0x800529CC, 0x18)),
        animatedConstructorSentinelAddress = 0x80052700u, animatedConstructorSentinel = Convert.ToHexString(Window(0x80052700, 0xC)),
        rows = removed.Select(r => new { rowIndex = r, before = Convert.ToHexString(before[r]), after = Convert.ToHexString(data.Row(r)), unoccupied = allocation.UnoccupiedRows.Contains(r) }),
        checks
    }, new JsonSerializerOptions { WriteIndented = true }));
}
else if (args.Length != 0) throw new ArgumentException("Arguments: ORIGINAL_USA_BIN CATALOG_WORKSPACE EMITTED_NATIVE_FIXTURE_JSON; no args runs synthetic allocation checks only.");
Console.WriteLine($"UNIVERSAL_ROW_REMOVAL_CHECKS_OK checks={checks} sourceVerified={args.Length == 3}");
