using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

if (args.Length != 3) throw new ArgumentException("<original USA BIN> <catalog workspace> <new report JSON>");
if (File.Exists(args[2])) throw new IOException("Preserve existing receipts; choose a new report path.");
UniversalSourceLibrary.RequireOriginal(args[0]);
var catalog = LevelCatalog.Load(args[1]);
var lib = new UniversalSourceLibrary(args[0], catalog);
int checks = 0;
void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
var receipts = new List<object>();
var town = lib.Get("townsquare").Data;
Check(town.Scene().RowCount == 107, "Original Town Square row inventory");
for (int row = 0; row < town.Scene().RowCount; row++)
    Check(town.Row(row)[0x4A] == 0xFF, "Retail unrestricted visibility sentinel " + row);

// The exact invisible Firework chest fixture, including hole reuse and model retirement.
var donor = lib.Get("hauntedtowers");
var target = lib.Get("peacekeepers");
byte[] donorBefore = donor.Data.Bytes.ToArray(), targetBefore = target.Data.Bytes.ToArray();
Check(donor.Data.Row(142)[0x4A] == 0x8C, "Authenticated Firework donor sector");
UniversalComposeResult? first = null;
foreach (int? staleSector in new int?[] { null, 0, 0x8C, 0xFF })
{
    var placement = new UniversalMobyPlacement("hauntedtowers", 142, 121633, 101837, 54733,
        RowByteOverrides: staleSector is int sector ? new Dictionary<int, byte> { [0x4A] = (byte)sector } : null);
    var removed = new HashSet<int> { 2, 11, 13, 35 };
    var result = Compose("peacekeepers", placement, removed);
    var output = new UniversalLevelData(result.LevelData);
    byte[] emitted = output.Row(result.NewRowIndices.Single());
    Check(result.NewRowIndices.Single() == 2, "Firework uses the freed physical row");
    Check(emitted[0x4A] == 0xFF, "Donor and stale overrides cannot hide the placed chest");
    Check(BinaryPrimitives.ReadUInt16LittleEndian(emitted.AsSpan(0x36)) == 0x138, "Firework class retained");
    Check(result.DroppedModelClasses.Contains(0xD6), "Removed Cannon Gnorc models free capacity");
    for (int row = 0; row < target.Data.Scene().RowCount; row++)
        if (!removed.Contains(row))
            Check(output.Row(row).AsSpan(0x0C).SequenceEqual(target.Data.Row(row).AsSpan(0x0C)),
                "Existing actor scalar bytes and visibility remain exact " + row);
    if (first is not null)
    {
        Check(result.LevelData.SequenceEqual(first.LevelData), "All obsolete sector overrides produce identical level bytes");
        Check(result.Overlay.SequenceEqual(first.Overlay), "Sector override does not change behavior");
        Check(result.Executable.SequenceEqual(first.Executable), "Sector override does not change executable");
    }
    first ??= result;
    receipts.Add(new { destination = "peacekeepers", staleSector, rows = result.NewRowIndices, result.Ram,
        dataSha256 = Convert.ToHexStringLower(SHA256.HashData(result.LevelData)) });
}

// New placements within an original level also need to survive a changed position/terrain graph.
// Retain ordinary resident behavior and prove the untouched destination rows keep their sectors.
foreach (string key in new[] { "artisans", "darkhollow", "townsquare", "peacekeepers" })
{
    var source = lib.Get(key);
    int row = Enumerable.Range(0, source.Data.Scene().RowCount)
        .First(i => BinaryPrimitives.ReadUInt16LittleEndian(source.Data.Row(i).AsSpan(0x36)) is >= 0x53 and <= 0x57);
    byte[] original = source.Data.Row(row);
    var placement = new UniversalMobyPlacement(key, row,
        BinaryPrimitives.ReadInt32LittleEndian(original.AsSpan(12)) + 512,
        BinaryPrimitives.ReadInt32LittleEndian(original.AsSpan(16)),
        BinaryPrimitives.ReadInt32LittleEndian(original.AsSpan(20)),
        RowByteOverrides: new Dictionary<int, byte> { [0x4A] = 0x8C });
    var result = Compose(key, placement, []);
    var output = new UniversalLevelData(result.LevelData);
    Check(output.Row(result.NewRowIndices.Single())[0x4A] == 0xFF, key + " moved clone visibility");
    Check(result.Overlay.SequenceEqual(source.Overlay.Bytes.ToArray()), key + " native behavior unchanged");
    for (int i = 0; i < source.Data.Scene().RowCount; i++)
        Check(output.Row(i).AsSpan(12).SequenceEqual(source.Data.Row(i).AsSpan(12)), key + " native actor unchanged " + i);
    receipts.Add(new { destination = key, sourceRow = row, rows = result.NewRowIndices });
}
Check(donor.Data.Bytes.SequenceEqual(donorBefore), "Original donor immutable");
Check(target.Data.Bytes.SequenceEqual(targetBefore), "Original target immutable");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
File.WriteAllText(args[2], JsonSerializer.Serialize(new { checks, receipts }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"UNIVERSAL_PLACEMENT_VISIBILITY_OK checks={checks}");

UniversalComposeResult Compose(string key, UniversalMobyPlacement placement, IReadOnlyCollection<int> removed)
{
    var s = lib.Get(key);
    return UniversalMobyComposer.Compose(s.Level, s.Data.Bytes, s.Overlay.Bytes.ToArray(), lib.Disc.Exe,
        lib.Get, lib.SharedClasses, UniversalMobyExport.DefaultProtectedVram(args[0], s.Level).ToArray(),
        [placement], removedRows: removed, sharedModelPackage: lib.SharedModelPackage,
        playerAnimationSource: lib.PlayerAnimations);
}
