using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

string root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
string locked = Path.Combine(
    root,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-display-name",
    "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE.bin");
string remote = Path.Combine(
    root,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-remote-blank-isolation",
    "Unused-Level-65-Remote-Blank-Isolation-Sector216-RUNTIME-CANDIDATE.bin");

Require(Directory.Exists(root), "Workspace root is missing.");
Require(File.Exists(locked), "Exact locked display-name BIN is missing.");
Require(File.Exists(remote), "Exact remote-blank runtime candidate BIN is missing.");
FileStamp lockedBefore = Stamp(locked);
FileStamp remoteBefore = Stamp(remote);

UnusedLevel65MusicOwnershipClosureContract first =
    UnusedLevel65MusicOwnershipClosure.Inspect(root, locked, remote);
UnusedLevel65MusicOwnershipClosureContract second =
    UnusedLevel65MusicOwnershipClosure.Inspect(root, locked, remote);
Require(Stamp(locked) == lockedBefore && Stamp(remote) == remoteBefore,
    "Read-only music inspection changed an input image.");

string firstJson = JsonSerializer.Serialize(first);
string secondJson = JsonSerializer.Serialize(second);
Require(firstJson == secondJson, "Music ownership inspection is not deterministic.");
string contractSha256 = Hash(Encoding.UTF8.GetBytes(firstJson));

Require(
    first.ProfileId == "unused-level-65-music-slot35-branchless-long-play-static-clean-usa-v1" &&
    first.LockedDisplayNameImageSha256 == "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8" &&
    first.RemoteBlankImageSha256 == "8020947d4ab5e7e4b0eac4bc6409ff3d6f11212b0e118bbf007d870812014a8e" &&
    first.ExecutableSha256 == "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442" &&
    first.OutputExecutableSha256 == "30d7721b6b46b9753827ddee249bc2591dc9fc1b5a76db71a4ebffe146cc9f8c" &&
    first.RemoteBlankOutputDataSha256 == "8d10aa62b134414aec80eec10d1fcede13bd55806ee13f6eb7c752f0fa1a9061",
    "Frozen music-closure identities changed.");

Require(
    first.Executable.Name == "SCUS_942.28" &&
    first.Executable.Lba == 55_382 && first.Executable.ByteLength == 0x66000 &&
    first.ContinuousLevelIndex == 35 && first.WitnessTrackId == 26 &&
    first.WitnessTrackName == "Town Square",
    "Executable or witness identity changed.");

int[] expectedPeteXaLbas = [60_000, 84_584, 113_448, 147_184, 184_808, 226_384];
int[] expectedPeteXaSizes = [50_348_032, 59_113_472, 69_091_328, 77_053_952, 85_147_648, 76_038_144];
Require(
    first.PeteXaFiles.Count == 6 &&
    first.PeteXaFiles.Select(file => file.Name).SequenceEqual(Enumerable.Range(0, 6).Select(index => $"PETEXA{index}.STR")) &&
    first.PeteXaFiles.Select(file => file.Lba).SequenceEqual(expectedPeteXaLbas) &&
    first.PeteXaFiles.Select(file => file.ByteLength).SequenceEqual(expectedPeteXaSizes),
    "PETEXA directory ownership changed.");

Require(
    first.MappingTable.FileOffset == 0x5F79C && first.MappingTable.ByteLength == 48 * 4 &&
    first.MappingTable.RuntimeAddress == 0x8006EF9C &&
    first.MappingTable.Sha256 == "a4be62ad38b6c793831d9cdcc3d92d24278e18df4fab19ddc678ed29e191e5f5" &&
    first.InitialSlot.FileOffset == 0x5F828 && first.InitialSlot.ImageOffset == 0x7CA7130 &&
    first.InitialSlot.RuntimeAddress == 0x8006F028 && first.InitialSlot.Hex == "13000000" &&
    first.InitialConsumer.FileOffset == 0x6504 && first.InitialConsumer.RuntimeAddress == 0x80015D04 &&
    first.InitialConsumer.Sha256 == "7698774b00c290b327fe980ea76a6aec22c36ada856351a3f85b32368529d106",
    "Initial music table/consumer ownership changed.");

Require(
    first.LateAlternateTable.FileOffset == 0x5F85C && first.LateAlternateTable.RuntimeAddress == 0x8006F05C &&
    first.LateAlternateTable.ByteLength == 35 * 3 * 4 && first.LateAlternateRowCount == 35 &&
    first.LateAlternateValuesPerRow == 3 &&
    first.LateAlternateTable.Sha256 == "9ba86a52d7b508d9a376ec819bbcee27ba9f46553c50dae1ce3fd16432555a2c" &&
    first.ReusedRow34.FileOffset == 0x5F9F4 && first.ReusedRow34.RuntimeAddress == 0x8006F1F4 &&
    first.ReusedRow34.Hex == "220000002200000022000000" &&
    first.PeteXaLbaTable.FileOffset == 0x5FA00 && first.PeteXaLbaTable.RuntimeAddress == 0x8006F200 &&
    first.PeteXaLbaTable.Hex == "60EA0000684A010028BB0100F03E0200E8D1020050740300" &&
    first.PeteXaLbaTable.Sha256 == "9fe9ac8b1cc132bb1ca1e43f3fca869d527447209b6729cbd8590b3771091f01",
    "Late-table/PETEXA adjacency changed.");

Require(
    first.LateConsumer.FileOffset == 0x1C57C && first.LateConsumer.RuntimeAddress == 0x8002BD7C &&
    first.LateConsumer.ByteLength == 0x8C &&
    first.LateConsumer.Sha256 == "4ccb43171ed02fb55a71a013fde64c40370683821630be5fd2975115a26bfd33" &&
    first.RandomCallee.FileOffset == 0x52F2C && first.RandomCallee.RuntimeAddress == 0x8006272C &&
    first.RandomCallee.ByteLength == 0x30 &&
    first.RandomCallee.Sha256 == "8bcf455ef340cfd7846fd7937a88671fbf165aa56526545a4912547c8d6bcfd4",
    "Late consumer or exact PRNG callee changed.");

Require(first.References.Count == 4, "Music reference count changed.");
Require(
    first.References.Select(reference => reference.FileOffset).SequenceEqual(new[] { 0x651C, 0x1C5EC, 0x1C5A0, 0x2D80 }) &&
    first.References.Select(reference => reference.InstructionHex).SequenceEqual(
        new[] { "9CEF228C", "9CEF228C", "5CF08424", "00F2E724" }),
    "Music/PETEXA table reference closure changed.");

int[] expectedInitialCallsites = [0x1E7E8, 0x1FB04, 0x1FB80, 0x22D3C, 0x23534, 0x23904, 0x23960, 0x23CBC];
int[] expectedLateCallsites = [0x6D94, 0x1EB40, 0x2350C, 0x2353C, 0x238B0, 0x23A88, 0x23EA0, 0x2407C];
Require(
    first.InitialConsumerCallsites.Count == 8 && first.LateConsumerCallsites.Count == 8 &&
    first.InitialConsumerCallsites.Select(call => call.FileOffset).SequenceEqual(expectedInitialCallsites) &&
    first.LateConsumerCallsites.Select(call => call.FileOffset).SequenceEqual(expectedLateCallsites) &&
    first.InitialConsumerCallsites.All(call => call.DirectJal && call.TargetRuntimeAddress == 0x80015370 &&
        call.InstructionAndDelayHex == "DC54000C01000424") &&
    first.LateConsumerCallsites.All(call => call.DirectJal && call.TargetRuntimeAddress == 0x8002BBE0 &&
        call.InstructionAndDelayHex == "F8AE000C00000000"),
    "Initial/late direct-call graph changed.");

Require(first.WitnessPatches.Count == 5, "Witness patch count changed.");
int[] expectedPatchOffsets = [0x1C57C, 0x1C584, 0x1C594, 0x1C5D8, 0x5F828];
int[] expectedPatchLengths = [4, 4, 32, 8, 4];
int[] expectedChangedBytes = [4, 4, 25, 6, 1];
long[] expectedPatchImageOffsets = [0x7C59E34, 0x7C59E3C, 0x7C59E4C, 0x7C59E90, 0x7CA7130];
Require(
    first.WitnessPatches.Select(patch => patch.FileOffset).SequenceEqual(expectedPatchOffsets) &&
    first.WitnessPatches.Select(patch => patch.ByteLength).SequenceEqual(expectedPatchLengths) &&
    first.WitnessPatches.Select(patch => patch.ChangedByteCount).SequenceEqual(expectedChangedBytes) &&
    first.WitnessPatches.Select(patch => patch.ImageOffset).SequenceEqual(expectedPatchImageOffsets) &&
    first.WitnessPatches.Select(patch => patch.RawSectorLba).SequenceEqual(new[] { 55_438, 55_438, 55_438, 55_438, 55_573 }),
    "Bounded witness patch locations changed.");
Require(
    first.WitnessPatches[0].BeforeHex == "00000000" && first.WitnessPatches[0].AfterHex == "0780083C" &&
    first.WitnessPatches[1].BeforeHex == "00000000" && first.WitnessPatches[1].AfterHex == "6459098D" &&
    first.WitnessPatches[2].AfterHex == "2300262D2118260150F00425402803002128A300802805002128A4000100C638" &&
    first.WitnessPatches[3].AfterHex == "21104600C858038D" &&
    first.WitnessPatches[4].BeforeHex == "13000000" && first.WitnessPatches[4].AfterHex == "1A000000",
    "Branchless transaction encoding changed.");

int[] expectedSelectableTracks = Enumerable.Range(0, 48).Where(track => track is not 30 and not 35).ToArray();
Require(
    first.SelectableTrackProofs.Count == 46 &&
    first.SelectableTrackProofs.Select(proof => proof.TrackId).SequenceEqual(expectedSelectableTracks) &&
    first.SelectableTrackProofs.All(proof => proof.Selectable && proof.InitialAndLongPlayMatch &&
        proof.InitialResolvedTrackId == proof.TrackId &&
        proof.LongPlayResolvedTrackIds.SequenceEqual(new[] { proof.TrackId, proof.TrackId, proof.TrackId })) &&
    first.ReservedTrackIds.SequenceEqual(new[] { 30, 35 }),
    "All-selectable-track static closure changed.");
Require(
    first.RetailOutcomeMatrixSha256 == "2eccb7780680d77c660ff3dc307d2c85d3e0857d5aca6a0d6a9b22e277b60750" &&
    first.Id65WitnessOutcomeMatrixSha256 == "313a9e020668e3ac27ca6d19a1b16dcf4ee03aeae79eb23cfcf0a2d42ae642a5" &&
    first.SupportedTrackIdSetSha256 == "911f9c937d4119c3817f023a68774877fd56dd978b78600a14d36bc2e7294e0c",
    "Music outcome matrices changed.");

Require(
    first.Mode2Impact.SectorSize == 2352 && first.Mode2Impact.UserOffset == 24 &&
    first.Mode2Impact.LogicalPatchWindowBytes == 52 && first.Mode2Impact.ChangedLogicalBytes == 40 &&
    first.Mode2Impact.MinimumRawSectorCount == 2 &&
    first.Mode2Impact.AffectedRawSectorLbas.SequenceEqual(new[] { 55_438, 55_573 }) &&
    first.Mode2Impact.AffectedRawSectorImageOffsets.SequenceEqual(new long[] { 55_438L * 2352, 55_573L * 2352 }) &&
    first.Mode2Impact.AllAffectedSectorsAreMode2Form1 && first.Mode2Impact.EdcEccRebuildRequired &&
    first.Mode2Impact.FileExtentUnchanged && first.Mode2Impact.IsoDirectoryUnchanged &&
    first.Mode2Impact.XaAudioSectorsUnchanged,
    "Two-sector MODE2 impact changed.");

Require(
    first.LockedAndRemoteExecutablesIdentical && first.RemoteBlankOutputMatchesStaticPlan &&
    first.RemoteBlankConstructionExcludesExecutable && first.InitialSlotIndependent &&
    first.NoLateTableExtension && first.PeteXaLbaTablePreserved && first.PeteXaDirectoryPreserved &&
    first.RandomCalleePreservesT0AndT1 && first.RetailInitialAndLongPlayOutcomesPreserved &&
    first.Id65InitialAndLongPlayResolveThroughSlot35 && first.AllSelectableBuiltInTracksStaticallyClosed &&
    first.MinimumTwoSectorTransactionAchieved && first.StaticTransactionClosed &&
    first.GenericLevelMusicExporterStillRejectsId65,
    "Static music-closure proof flags changed.");
Require(
    !first.WritesBin && !first.WritesCue && !first.WriterAuthorized && !first.RuntimeProofComplete &&
    !first.AppEnabled && !first.NormalCreateBinEnabled && !first.PromotionAuthorized &&
    first.RuntimeGates.Count == 5 && first.Notes.Count == 6,
    "Fail-closed music promotion boundary changed.");

string missing = Path.Combine(root, "_local", "v5-stone-hill-level-replacement", "missing-music-proof.bin");
Require(!File.Exists(missing), "Negative fixture path unexpectedly exists.");
bool missingRejected = false;
try
{
    _ = UnusedLevel65MusicOwnershipClosure.Inspect(root, missing, remote);
}
catch (FileNotFoundException)
{
    missingRejected = true;
}
Require(missingRejected && !File.Exists(missing), "Missing input did not fail closed without writes.");

Console.WriteLine("PASS: ID65 music ownership has a bounded branchless static transaction for all selectable built-in tracks.");
Console.WriteLine($"Contract SHA-256: {contractSha256}");
Console.WriteLine("Initial slot: SCUS 0x5F828 (index 35); witness Town Square track 26.");
Console.WriteLine("Late path: ID65 reuses row 34 [34,34,34], promotes only its source index to 35, then uses the unchanged mapping lookup.");
Console.WriteLine("Retail: all 35 x 3 initial/late outcomes preserved exactly; PETEXA0-5 directory/LBA/audio ownership preserved.");
Console.WriteLine("MODE2: 52-byte patch windows / 40 changed logical bytes across exact SCUS LBAs 55438 and 55573; future writer must rebuild EDC/ECC.");
Console.WriteLine("No BIN/CUE was written; writer, App, normal Create BIN, runtime proof, and promotion remain disabled.");

static FileStamp Stamp(string path)
{
    FileInfo file = new(path);
    return new(file.Length, file.LastWriteTimeUtc, file.CreationTimeUtc);
}

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

readonly record struct FileStamp(long Length, DateTime LastWriteTimeUtc, DateTime CreationTimeUtc);
