using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

string root = FindRoot(args.ElementAtOrDefault(0));
string lockedBase = Path.Combine(
    root,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-display-name",
    "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE.bin");
string wrongBase = Path.Combine(
    root,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-physical-clone",
    "Unused-Level-65-Town-Square-independent-storage-RUNTIME-CANDIDATE.bin");
Require(File.Exists(lockedBase), "The exact locked display-name BIN is missing.");
Require(File.Exists(wrongBase), "The wrong-hash negative fixture is missing.");

string hashBefore = HashFile(lockedBase);
long lengthBefore = new FileInfo(lockedBase).Length;
DateTime writeBefore = File.GetLastWriteTimeUtc(lockedBase);
DirectorySnapshot directoryBefore = SnapshotDirectory(Path.GetDirectoryName(lockedBase)!);

UnusedLevel65ProgressionOwnershipContract first =
    UnusedLevel65ProgressionOwnershipInspector.Inspect(root, lockedBase);
UnusedLevel65ProgressionOwnershipContract second =
    UnusedLevel65ProgressionOwnershipInspector.Inspect(root, lockedBase);
JsonSerializerOptions json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
Require(JsonSerializer.Serialize(first, json) == JsonSerializer.Serialize(second, json),
    "Two progression ownership inspections were not deterministic.");

Require(
    first.ProfileId == UnusedLevel65ProgressionOwnershipInspector.ProfileId &&
    first.LockedBaseImageSha256 == "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8" &&
    first.ExecutableSha256 == "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442" &&
    first.RemoteBlankProfileId == UnusedLevel65RemoteBlankIsolationConstruction.ProfileId &&
    first.RemoteBlankOutputDataSha256 == "8d10aa62b134414aec80eec10d1fcede13bd55806ee13f6eb7c752f0fa1a9061" &&
    first.RemoteBlankOutputObjectTableSha256 == "f89347baf4f14739030499b21210b8ea46b44f9c4df02a1b58d39fde6ca1e64a",
    "The exact locked/remote substrate changed.");
Require(
    first.Executable.LogicalOffset == 0 && first.Executable.ByteLength == 0x66000 &&
    first.LockedRow80Data.LogicalOffset == 0x6936800 && first.LockedRow80Data.ByteLength == 0x2E2000 &&
    first.LockedRow80Data.Sha256 == "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0" &&
    first.RemoteRow80Data.Sha256 == first.RemoteBlankOutputDataSha256 && first.RemoteRow80Data.ImageOffset == -1 &&
    first.TownSquareObjectTable.LogicalOffset == 0x136E170 &&
    first.TownSquareObjectTable.Sha256 == "2d5743b6895cb6142150812e06eb772b492ab21665edec239e17e98b9ed2af1d" &&
    first.RemoteObjectTable.LogicalOffset == 0x6B06970 && first.RemoteObjectTable.ByteLength == 107 * 0x58 &&
    first.RemoteObjectTable.Sha256 == first.RemoteBlankOutputObjectTableSha256,
    "The exact row-80/object-table ownership changed.");

UnusedLevel65ProgressionMusicOwnership music = first.Music;
Require(
    music.ContinuousIndex == 35 && music.MappingTable.LogicalOffset == 0x5F79C &&
    music.InitialSlot.LogicalOffset == 0x5F828 && music.InitialSlot.ImageOffset == 0x7CA7130 &&
    music.InitialSlot.Hex == "13000000" && music.InitialTrackId == 19 &&
    music.InitialConsumer.LogicalOffset == 0x6504 &&
    music.InitialConsumer.Sha256 == "7698774b00c290b327fe980ea76a6aec22c36ada856351a3f85b32368529d106" &&
    music.LateAlternateTable.LogicalOffset == 0x5F85C && music.LateAlternateLevelRows == 35 &&
    music.LateAlternateValuesPerRow == 3 && music.LateConsumer.LogicalOffset == 0x1C594 &&
    music.WouldBeRow35Collision.LogicalOffset == 0x5FA00 &&
    music.WouldBeRow35Collision.Hex == "60EA0000684A010028BB0100" &&
    music.WouldBeRow35PetexaLbas.SequenceEqual(new[] { 60_000, 84_584, 113_448 }) &&
    music.InitialSlotIndependent && !music.LongPlayRowPresent &&
    !music.FourByteInitialOnlyTransactionSafe && !music.RuntimeOwnershipProven,
    "The initial/late music ownership closure changed.");

UnusedLevel65ProgressionTotalsOwnership totals = first.Totals;
Require(
    totals.DragonTable.LogicalOffset == 0x5FC14 && totals.DragonSlot.LogicalOffset == 0x5FC37 &&
    totals.DragonSlot.Hex == "00" && totals.DragonTarget == 0 &&
    totals.TreasureTable.LogicalOffset == 0x5FC38 && totals.TreasureSlot.LogicalOffset == 0x5FC7E &&
    totals.TreasureSlot.ImageOffset == 0x7CA7586 && totals.TreasureSlot.Hex == "0000" && totals.TreasureTarget == 0 &&
    totals.EggTable.LogicalOffset == 0x5FC80 && totals.EggTargetRowCount == 20 &&
    totals.DragonConsumer.LogicalOffset == 0xC918 && totals.TreasureConsumer.LogicalOffset == 0xC8CC &&
    totals.EggConsumerBounds.LogicalOffset == 0xCB18 &&
    totals.DragonSlotIndependent && totals.TreasureSlotIndependent && !totals.EggSlotPresent &&
    totals.SmallestDragonTreasurePatchBytes == 3 && totals.ZeroEggProfileRequired &&
    !totals.RemoteObjectCensusClosed && !totals.RuntimeOwnershipProven,
    "The exact totals ownership closure changed.");

UnusedLevel65ProgressionExitOwnership exit = first.Exit;
Require(
    exit.PortalGuardPath.LogicalOffset == 0x47904 && exit.GuardInstructionFileOffset == 0x47910 &&
    exit.GuardInstructionHex == "41004228" && exit.CurrentUpperExclusive == 65 &&
    exit.RequiredUpperExclusive == 66 && exit.GuardLogicalPatchBytes == 1 &&
    exit.SourcePortalTable.LogicalOffset == 0x6AA8D8C && exit.RemotePortalTable.LogicalOffset == 0x6AA8E00 &&
    exit.RemotePortalTable.ImageOffset == -1 && exit.RemotePortalCount == 0 &&
    exit.TownSquareReturnHomeVisible.Sha256 == "a47e1a323dc7da67ca08239e7da9fcc1eaddf08680b5663eee1564e405271fd4" &&
    exit.RemoteReturnHomeVisible.Sha256 == exit.TownSquareReturnHomeVisible.Sha256 &&
    exit.TownSquareReturnHomeHelper.Sha256 == "046f14f9b75af2c7bc9fb15dd3fa478b12ded01971214ada3de53879c3cf0abc" &&
    exit.RemoteReturnHomeHelper.Sha256 == exit.TownSquareReturnHomeHelper.Sha256 &&
    exit.ReturnHomeSceneDistanceFromRemotePad > 11_746 && exit.ReturnHomeSceneDistanceFromRemotePad < 11_747 &&
    exit.GnastyWorldDestinationLevelIds.SequenceEqual(new[] { 62, 63, 64, 61 }) &&
    exit.ReturnHomeRowsAreTownSquareByteClones && exit.ReturnHomePairMetadataExistsForTownSquare &&
    !exit.ReturnHomePairMetadataExistsForLab && !exit.GnastyWorldHasDestination65 &&
    !exit.OneByteGuardTransactionSafe && !exit.ExitRuntimeOwnershipProven &&
    !exit.ReturnHomeRuntimeOwnershipProven,
    "The exact exit/Return Home ownership closure changed.");

UnusedLevel65ProgressionDeathOwnership death = first.Death;
Require(
    death.DeathLifeRoutine.LogicalOffset == 0x1D05C && death.DeathLifeRoutine.ImageOffset == 0x7C5AB74 &&
    death.DeathLifeRoutine.Sha256 == "37dee82fe6e38eaa24e03ab0cf8fdf3226b10e33857a475fd16376f7748f30ad" &&
    death.DirectCallers.Select(edge => edge.CallerFileOffset).SequenceEqual(new long[] { 0x33710, 0x3ACD8 }) &&
    death.DirectCallers.All(edge => edge.InstructionHex == "17B2000C" && edge.CalleeRuntimeAddress == 0x8002C85C) &&
    death.NewGameResetRoutine.LogicalOffset == 0x2E04 &&
    death.NewGameResetDirectCallers.Select(edge => edge.CallerFileOffset).SequenceEqual(new long[] { 0x2FA8 }) &&
    death.LivesRuntimeAddress == 0x8007582C && death.DeathStateRuntimeAddress == 0x800757D8 &&
    !death.DeathCallsNewGameReset && !death.DeathWritesPerLevelProgressArrays && death.StaticRetentionSupported &&
    !death.RemoteDeathPlaneRuntimeVerified && !death.RemoteDeathRespawnRuntimeVerified,
    "The exact death/reset static-vs-runtime boundary changed.");

UnusedLevel65ProgressionSaveOwnership save = first.Save;
Require(
    save.ChecksumRoutine.LogicalOffset == 0x49D6C && save.DeserializeRoutine.LogicalOffset == 0x49D94 &&
    save.SerializeRoutine.LogicalOffset == 0x4A064 && save.SerializeRoutine.ImageOffset == 0x7C8E65C &&
    save.DeserializePerLevelLoop.Sha256 == "69bea210fee604fb283fd4043153ce6439865b22d0040006111d74fd9722800a" &&
    save.SerializePerLevelLoop.Sha256 == "404cbf4ee735b108193c9ea301e3ebceac4671e18e752af36d0b21a8e65d031d" &&
    save.SerializeDirectCallers.Select(edge => edge.CallerFileOffset).SequenceEqual(new long[] { 0x229F4 }) &&
    save.ChecksumDirectCallers.Select(edge => edge.CallerFileOffset).SequenceEqual(new long[] { 0x22AF8, 0x4A034, 0x4A224 }) &&
    save.DeserializeDirectCallerCount == 0 && save.SaveStructByteLength == 0x600 &&
    save.ChecksummedByteLength == 0x58C && save.ChecksumFieldOffset == 0x58C &&
    save.ObjectRetirementBitsPerLevel == 256 && save.RemoteObjectRecordCount == 107 &&
    save.Index35NonEggRowsStaticallyRoundTrip && !save.Index35EggRowPresent &&
    save.CurrentRemoteObjectTableFitsRetirementBitmap && !save.FullAuthoringObjectCapacityUnbounded &&
    !save.DeserializeCallerClosureResolved && !save.MemoryCardRuntimeOwnershipProven,
    "The exact save ownership closure changed.");
RequireField(save, "visited", 36, 0x63, 0x80078E9B, true);
RequireField(save, "unresolved-second-per-level-byte-table", 36, 0x87, 0x8007A6CB, true);
RequireField(save, "dragons", 36, 0xAB, 0x80077364, true);
RequireField(save, "treasure", 36, 0xF2, 0x800774AC, true);
RequireField(save, "eggs", 18, null, null, false);
RequireField(save, "object-retirement-bitmaps", 36, 0x56C, 0x80077D68, true);

UnusedLevel65ProgressionAlias initialAlias = first.CrossLevelAliases.Single(alias => alias.Domain == "initial-music");
UnusedLevel65ProgressionAlias dragonAlias = first.CrossLevelAliases.Single(alias => alias.Domain == "dragon-target");
UnusedLevel65ProgressionAlias treasureAlias = first.CrossLevelAliases.Single(alias => alias.Domain == "treasure-target");
Require(
    initialAlias.RetailLevelIds.SequenceEqual(new[] { 64 }) && initialAlias.RetailLevelNames.SequenceEqual(new[] { "Gnasty's Loot" }) &&
    initialAlias.SharesOnlyValue && !initialAlias.SharesPhysicalStorage &&
    dragonAlias.RetailLevelIds.SequenceEqual(new[] { 15, 25, 35, 45, 55, 63, 64 }) &&
    treasureAlias.RetailLevelIds.Count == 0 && !treasureAlias.SharesOnlyValue &&
    first.CrossLevelAliases.Single(alias => alias.Domain == "return-home-visible").RetailLevelIds.SequenceEqual(new[] { 13 }) &&
    first.CrossLevelAliases.Single(alias => alias.Domain == "portal-table").RetailLevelIds.SequenceEqual(new[] { 13 }) &&
    first.CrossLevelAliases.Single(alias => alias.Domain == "save-index-35").RetailLevelIds.Count == 0,
    "The complete cross-level alias ledger changed.");

Require(
    first.Transactions.Count == 5 &&
    first.Transactions.Single(item => item.Domain == "music").MinimumLogicalPatchBytes == 4 &&
    first.Transactions.Single(item => item.Domain == "totals").MinimumLogicalPatchBytes == 3 &&
    first.Transactions.Single(item => item.Domain == "exit-return-home").MinimumLogicalPatchBytes == 1 &&
    first.Transactions.Single(item => item.Domain == "death-respawn").MinimumLogicalPatchBytes == 0 &&
    first.Transactions.Single(item => item.Domain == "death-respawn").StructurallyClosed &&
    first.Transactions.Where(item => item.Domain != "death-respawn").All(item => !item.StructurallyClosed) &&
    first.Transactions.All(item => !item.WriterAuthorized && !item.PromotionAuthorized) &&
    first.CombinedTransactionBlockers.Count == 5,
    "The smallest-transaction/blocker decisions changed.");
Require(
    first.LockedImagePreserved && first.RemotePlanIsInMemoryOnly && first.ExecutableExcludedFromRemotePlan &&
    first.StaticReadbackOnly && !first.WritesBin && !first.WritesCue &&
    !first.StandaloneCombinedWriterAuthorized && !first.NormalCreateBinEnabled && !first.PromotionAuthorized,
    "The static-only fail-closed boundary changed.");

bool wrongHashRejected = false;
try
{
    _ = UnusedLevel65ProgressionOwnershipInspector.Inspect(root, wrongBase);
}
catch (InvalidDataException ex) when (ex.Message.Contains("SHA-256", StringComparison.Ordinal))
{
    wrongHashRejected = true;
}
Require(wrongHashRejected, "A stale/wrong ID65 base was accepted.");

string hashAfter = HashFile(lockedBase);
DirectorySnapshot directoryAfter = SnapshotDirectory(Path.GetDirectoryName(lockedBase)!);
Require(hashBefore == hashAfter && lengthBefore == new FileInfo(lockedBase).Length &&
        writeBefore == File.GetLastWriteTimeUtc(lockedBase) && directoryBefore == directoryAfter,
    "The static ownership smoke changed the locked candidate or its directory.");

Console.WriteLine("PASS: ID65 progression ownership is mapped on the exact remote-blank construction without writes.");
Console.WriteLine("Music: slot 35 is distinct, but long-play row 35 collides with PETEXA0-2; initial-only patch is unsafe.");
Console.WriteLine("Totals: dragon/gem slots are distinct (3-byte pair); eggs have no target/save row; census is open.");
Console.WriteLine("Exit: <65 guard is one logical byte, but T96/T97 remain remote Town Square clones and Gnasty has no route 65.");
Console.WriteLine("Death: two common callers retain per-level state statically; remote death-plane/respawn runtime gate is pending.");
Console.WriteLine("Save: index 35 round-trips visited/unknown/dragon/gem/256 retirement bits; eggs/card/caller closure remain open.");
Console.WriteLine("Artifacts written: 0 BIN, 0 CUE; combined writer, normal Create BIN, and promotion remain false.");

static void RequireField(
    UnusedLevel65ProgressionSaveOwnership save,
    string name,
    int rows,
    int? offset,
    uint? address,
    bool present)
{
    UnusedLevel65ProgressionSaveField field = save.Fields.Single(item => item.Name == name);
    Require(field.RowCount == rows && field.Index35SaveOffset == offset &&
            field.Index35RuntimeAddress == address && field.Index35StoragePresent == present,
        $"Save field {name} changed.");
}

static string FindRoot(string? requested)
{
    string current = Path.GetFullPath(requested ?? Directory.GetCurrentDirectory());
    while (!File.Exists(Path.Combine(current, "spyro-level-catalog.json")))
    {
        DirectoryInfo? parent = Directory.GetParent(current);
        if (parent == null)
            throw new DirectoryNotFoundException("Could not find the Spyro Editor repository root.");
        current = parent.FullName;
    }
    return current;
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static string HashFile(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}

static DirectorySnapshot SnapshotDirectory(string path)
{
    string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
        .Select(file => $"{Path.GetRelativePath(path, file)}|{new FileInfo(file).Length}|{HashFile(file)}")
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();
    string[] directories = Directory.GetDirectories(path, "*", SearchOption.AllDirectories)
        .Select(directory => Path.GetRelativePath(path, directory))
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();
    return new(string.Join('\n', files), string.Join('\n', directories));
}

internal sealed record DirectorySnapshot(string Files, string Directories);
