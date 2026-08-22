using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Music;

string root = Path.GetFullPath(args.FirstOrDefault() ?? Directory.GetCurrentDirectory());
string lockedBase = Path.Combine(
    root,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-display-name",
    "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE.bin");
string foundation = Path.Combine(
    root,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-full-authoring-foundation-native-membership",
    "Unused-Level-65-Full-Authoring-Foundation-HP-LP-45deg-NATIVE-MEMBERSHIP-RUNTIME-CANDIDATE.bin");
string physicalClone = Path.Combine(
    root,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-physical-clone",
    "Unused-Level-65-Town-Square-independent-storage-RUNTIME-CANDIDATE.bin");

RequireFile(lockedBase, "locked display-name base");
RequireFile(foundation, "foundation candidate");
RequireFile(physicalClone, "wrong-hash negative fixture");
string lockedHashBefore = await HashFileAsync(lockedBase);
string foundationHashBefore = await HashFileAsync(foundation);
DirectorySnapshot candidateDirectoryBefore = SnapshotDirectory(Path.GetDirectoryName(foundation)!);

UnusedLevel65StandaloneBehaviorOwnershipContract first =
    await UnusedLevel65StandaloneBehaviorOwnershipInspector.InspectAsync(
        root,
        lockedBase,
        foundation);
UnusedLevel65StandaloneBehaviorOwnershipContract second =
    await UnusedLevel65StandaloneBehaviorOwnershipInspector.InspectAsync(
        root,
        lockedBase,
        foundation);

JsonSerializerOptions jsonOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
};
string firstJson = JsonSerializer.Serialize(first, jsonOptions);
string secondJson = JsonSerializer.Serialize(second, jsonOptions);
Require(firstJson == secondJson, "Two independent ownership inspections were not deterministic.");

Require(
    first.ProfileId == UnusedLevel65StandaloneBehaviorOwnershipInspector.ProfileId &&
    first.LockedBaseImageSha256 == "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8" &&
    first.FoundationImageSha256 == "92e4046ce4d14771ebb70a72c2a024b8e76f5575e38771f7067ff2b4303ac222" &&
    first.ExecutableSha256 == "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442",
    "The exact ownership substrate hashes changed.");
Require(
    first.DirectoryRow79And80.LogicalOffset == 0x278 &&
    first.DirectoryRow79And80.Hex == "0070920600F800000068930600202E00" &&
    first.Overlay.LogicalOffset == 0x6927000 &&
    first.Overlay.Sha256 == "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5" &&
    first.LockedBaseData.Sha256 == "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0" &&
    first.FoundationData.Sha256 == "eb5ca8459300392de12246e57770a57447f7bd64ccf3e978137d2362d7694160" &&
    first.ObjectTable.ByteLength == 9_416 &&
    first.ObjectTable.Sha256 == "2d5743b6895cb6142150812e06eb772b492ab21665edec239e17e98b9ed2af1d",
    "The exact independent WAD/object ownership pins changed.");

Require(
    first.Bootstrap.WarpDispatchPointer.LogicalOffset == 0x1CA8 &&
    first.Bootstrap.WarpDispatchPointer.Hex == "98A80580" &&
    first.Bootstrap.WarpDispatchRuntimeAddress == 0x8005A898 &&
    first.Bootstrap.WarpTableUpperBound.LogicalOffset == 0x1DF44 &&
    first.Bootstrap.WarpTableUpperBound.Hex == "3800422C" &&
    first.Bootstrap.WarpTableUpperBoundImmediate == 0x38 &&
    first.Bootstrap.HiddenWarpBootstrapStores.LogicalOffset == 0x1E034 &&
    first.Bootstrap.HiddenWarpBootstrapStores.Hex == "1C0684AF880680AF" &&
    first.Bootstrap.NonFlightLevelIdentity.LogicalOffset == 0x40C0 &&
    first.Bootstrap.NonFlightLevelIdentity.Hex == "005E023C" &&
    first.Bootstrap.ExistingBootstrapAcceptsId65 &&
    !first.Bootstrap.ExistingBootstrapIsStandaloneBehaviorOwnership,
    "The exact inherited ID65 bootstrap contract changed.");

Require(
    first.Spawn.Landing.LogicalOffset == 0x6B06800 &&
    first.Spawn.Landing.Hex == "29E901009A8801006621000000004000" &&
    first.Spawn.Landing.Sha256 == "ac6446f7f11382b1f5d9c4f73c17281cda8db6251b14ace4e237c3c3d9f1896e" &&
    first.Spawn.PlayerAnchorTrueIndex == 92 &&
    first.Spawn.PlayerAnchor.LogicalOffset == 0x6B08910 &&
    first.Spawn.PlayerAnchor.Sha256 == "4987c539f3178c555da26e4ec2f6cdc25094a27382b9465c9845846a39679bb2" &&
    first.Spawn.LandingAndPlayerShareXy &&
    first.Spawn.PlayerZAboveLanding == 154 &&
    !first.Spawn.GenericFlyInWriterCouplesPlayerAnchor &&
    !first.Spawn.DeathRespawnRuntimeVerified &&
    first.Spawn.ResetColdBootRestorationRuntimeVerified,
    "The inherited landing/T92 ownership boundary changed.");

Require(
    first.Music.ContinuousLevelIndex == 35 &&
    first.Music.MappingTable.LogicalOffset == 0x5F79C &&
    first.Music.MappingTable.Sha256 == "a4be62ad38b6c793831d9cdcc3d92d24278e18df4fab19ddc678ed29e191e5f5" &&
    first.Music.InitialSlot.LogicalOffset == 0x5F828 &&
    first.Music.InitialSlot.Hex == "13000000" &&
    first.Music.CurrentTrackId == 19 &&
    first.Music.TownSquareTrackId == 26 &&
    first.Music.LateAlternateRowCount == 35 &&
    first.Music.LateAlternateTable.LogicalOffset == 0x5F85C &&
    first.Music.LateAlternateTable.Sha256 == "9ba86a52d7b508d9a376ec819bbcee27ba9f46553c50dae1ce3fd16432555a2c" &&
    !first.Music.HasLateAlternateRow &&
    !first.Music.GenericEditorAcceptsInitialAndLongPlayOwnership &&
    !first.Music.ExtendedSessionRuntimeVerified,
    "The initial/late music ownership boundary changed.");

Require(
    first.Totals.DragonTable.LogicalOffset == 0x5FC14 &&
    first.Totals.DragonTable.Sha256 == "369c57dff04b1b608adb95b2b42688a0380f61ccf5445446e423d1badbc03699" &&
    first.Totals.DragonSlot.LogicalOffset == 0x5FC37 &&
    first.Totals.DragonTarget == 0 &&
    first.Totals.TreasureTable.LogicalOffset == 0x5FC38 &&
    first.Totals.TreasureTable.Sha256 == "641867a8c3d0c8af833f4ce678cd27ff7520e7ddafa4cecfd19c7a388f625ee6" &&
    first.Totals.TreasureSlot.LogicalOffset == 0x5FC7E &&
    first.Totals.TreasureTarget == 0 &&
    first.Totals.EggTable.LogicalOffset == 0x5FC80 &&
    first.Totals.EggTableRowCount == 20 &&
    first.Totals.EggTable.Sha256 == "6d4de8ed9543f9dc758c3824ac0e4bebcf75f681de15770355b7b8820e6fd41b" &&
    !first.Totals.HasEggSlot &&
    first.Totals.LegacyRetailTreasureSlot35ImageOffset == 0x7945FF6 &&
    first.Totals.RelocatedExecutableTreasureSlot35ImageOffset == 0x7CA7586 &&
    !first.Totals.GenericTreasureWriterTargetsRelocatedExecutable &&
    first.Totals.InitialProfileMustForbidEggs &&
    !first.Totals.AuthoredTotalsRuntimeVerified,
    "The exact total-table ownership boundary changed.");

Require(
    first.Exit.PortalIdGuard.LogicalOffset == 0x47910 &&
    first.Exit.PortalIdGuard.Hex == "41004228" &&
    first.Exit.CurrentPortalUpperExclusive == 65 &&
    first.Exit.RequiredPortalUpperExclusive == 66 &&
    first.Exit.ReturnHomeVisibleRow.LogicalOffset == 0x6B08A70 &&
    first.Exit.ReturnHomeVisibleRow.Sha256 == "a47e1a323dc7da67ca08239e7da9fcc1eaddf08680b5663eee1564e405271fd4" &&
    first.Exit.ReturnHomeHelperRow.LogicalOffset == 0x6B08AC8 &&
    first.Exit.ReturnHomeHelperRow.Sha256 == "046f14f9b75af2c7bc9fb15dd3fa478b12ded01971214ada3de53879c3cf0abc" &&
    !first.Exit.ReturnHomeBehaviorLinkLoadedForLabKey &&
    first.Exit.LockedBasePortalTableWadOffset == 0x6AA8D8C &&
    first.Exit.Id65PortalTableWadOffset == 0x6AA8DBC &&
    first.Exit.Id65PortalTable.Hex == "00000000" &&
    first.Exit.Id65PortalTable.Sha256 == "df3f619804a92fdb4057192dc43dd748ea778adc52bc498ce80524c014b81119" &&
    first.Exit.Id65PortalCount == 0 &&
    first.Exit.GnastyWorldPortalTableWadOffset == 0x52F280C &&
    first.Exit.GnastyWorldDestinationLevelIds.SequenceEqual(new[] { 62, 63, 64, 61 }) &&
    first.Exit.CheckedInGnastyWorldDestinationLevelIds.SequenceEqual(new[] { 61, 62, 63, 64 }) &&
    !first.Exit.GnastyWorldHasDestination65 &&
    !first.Exit.RetailGnastyWorldMutationAuthorized &&
    !first.Exit.ExitLevelRuntimeVerified &&
    !first.Exit.ReturnHomeRuntimeVerified,
    "The Return Home/exit ownership boundary changed.");

Require(
    first.Save.NewGameResetRoutine.LogicalOffset == 0x2E04 &&
    first.Save.NewGameResetRoutine.ByteLength == 0x178 &&
    first.Save.NewGameResetRoutine.Sha256 == "57e807bf638fe6394f46cc7c673f5fc27e8910fc8fce0acee60761c51548538f" &&
    first.Save.DeathLifeLossRoutine.LogicalOffset == 0x1D05C &&
    first.Save.DeathLifeLossRoutine.ByteLength == 0x48 &&
    first.Save.DeathLifeLossRoutine.Sha256 == "37dee82fe6e38eaa24e03ab0cf8fdf3226b10e33857a475fd16376f7748f30ad" &&
    first.Save.DeathLifeLossRuntimeAddress == 0x8002C85C &&
    first.Save.DeathCallSiteRuntimeAddress == 0x8004A4D8 &&
    first.Save.LivesRuntimeAddress == 0x8007582C &&
    first.Save.DeathStateRuntimeAddress == 0x800757D8 &&
    !first.Save.DeathCallsFullNewGameReset &&
    !first.Save.DeathClearsIndex35PerLevelState &&
    first.Save.DeathStateRetentionStaticallySupported &&
    first.Save.ChecksumRoutine.LogicalOffset == 0x49D6C &&
    first.Save.ChecksumRoutine.Sha256 == "d83b069d73f991a0ea4b1a318759f6ea66c5606de361242bab29d02c840824db" &&
    first.Save.DeserializeRoutine.LogicalOffset == 0x49D94 &&
    first.Save.DeserializeRoutine.Sha256 == "ff2bcfe124d934838348cb79807d36cdacc3d82f3cf6773d007c4aa0555e2ae6" &&
    first.Save.SerializeRoutine.LogicalOffset == 0x4A064 &&
    first.Save.SerializeRoutine.Sha256 == "b8674b556d8f4154886306731ee50c9812f2cac1d9a224756c110be693d19056" &&
    first.Save.CombinedSaveCode.ByteLength == 0x4DC &&
    first.Save.CombinedSaveCode.Sha256 == "34acb0e3b5a518dc4b9f0147a60a4018288ae16fda5a3fa56db6cfb89aac999a" &&
    first.Save.SaveStructByteLength == 0x600 &&
    first.Save.ChecksummedByteLength == 0x58C &&
    first.Save.ChecksumFieldOffset == 0x58C &&
    first.Save.ResetObjectRetirementRuntimeAddress == 0x80077908 &&
    first.Save.ResetObjectRetirementByteLength == 0x480 &&
    first.Save.CurrentLevelIdRuntimeAddresses.SequenceEqual(new uint[] { 0x800758B4, 0x8007596C }) &&
    first.Save.VisitedSlotRuntimeAddress == 0x80078E9B &&
    first.Save.Index35SchemaRoundTripStaticallyVerified &&
    !first.Save.Index35EggOwnershipPresent &&
    !first.Save.MemoryCardInsertionRuntimeVerified &&
    !first.Save.SaveReloadRuntimeVerified &&
    !first.Save.DeathRespawnRuntimeVerified &&
    !first.Save.IndependentSerializedSlotOwnershipProven,
    "The exact save-schema/static-vs-runtime boundary changed.");

UnusedLevel65SaveSchemaField visited = first.Save.Schema.Single(field => field.Name == "visited");
UnusedLevel65SaveSchemaField secondTable = first.Save.Schema.Single(field => field.Name == "unresolved-second-per-level-byte-table");
UnusedLevel65SaveSchemaField dragons = first.Save.Schema.Single(field => field.Name == "dragons");
UnusedLevel65SaveSchemaField treasure = first.Save.Schema.Single(field => field.Name == "treasure");
UnusedLevel65SaveSchemaField eggs = first.Save.Schema.Single(field => field.Name == "eggs");
UnusedLevel65SaveSchemaField retirement = first.Save.Schema.Single(field => field.Name == "object-retirement-bitmaps");
Require(
    visited.RowCount == 36 && visited.Index35SaveOffset == 0x063 && visited.Index35RuntimeAddress == 0x80078E9B &&
    secondTable.RowCount == 36 && secondTable.Index35SaveOffset == 0x087 && secondTable.Index35RuntimeAddress == 0x8007A6CB &&
    dragons.RowCount == 36 && dragons.Index35SaveOffset == 0x0AB && dragons.Index35RuntimeAddress == 0x80077364 &&
    treasure.RowCount == 36 && treasure.Index35SaveOffset == 0x0F2 && treasure.Index35RuntimeAddress == 0x800774AC &&
    eggs.RowCount == 18 && !eggs.HasIndex35Storage && eggs.Index35SaveOffset == null &&
    retirement.RowCount == 36 && retirement.ElementByteLength == 0x20 && retirement.Index35SaveOffset == 0x56C && retirement.Index35RuntimeAddress == 0x80077D68,
    "One exact index-35 save schema field changed.");

Require(
    first.EditorBoundary.LabCatalogIsMemoryOnly &&
    !first.EditorBoundary.LabObjectMutationAvailable &&
    !first.EditorBoundary.LabPortalRoutingAvailable &&
    !first.EditorBoundary.LabSaveOwnershipAvailable &&
    !first.EditorBoundary.LabMusicMutationAvailable &&
    !first.EditorBoundary.LabNormalCreateBinAvailable &&
    first.EditorBoundary.RetailAllSavedEditsSkipsLab &&
    !first.EditorBoundary.LabMobyMetadataUsesTownSquareBehaviorLinks &&
    !first.EditorBoundary.LabLoadsFlyInLandingEditorControl &&
    first.EditorBoundary.ExactGenericBlockers.Count == 5,
    "The fail-closed editor boundary changed.");
Require(
    first.PromotionOrder.Select(stage => stage.Order).SequenceEqual(Enumerable.Range(1, 6)) &&
    first.PromotionOrder.All(stage => !stage.MutationWriterAuthorized && !stage.PromotionAuthorized) &&
    first.LockedBasePreserved && first.FoundationPreserved && first.StaticReadbackOnly &&
    !first.WritesBin && !first.WritesCue && !first.NormalCreateBinEnabled && !first.PromotionAuthorized,
    "The staged ownership/promotion boundary changed.");

bool musicRejected = false;
try
{
    _ = LevelMusicPatchExporter.BuildBatchPlan(
        foundation,
        Path.Combine(root, "_local", "never-written-ID65-music.bin"),
        Path.Combine(root, "_local", "never-written-ID65-music.cue"),
        [new LevelMusicReplacement(UnusedLevel65BlankLevelLabProfileRegistry.Definition, 26)]);
}
catch (InvalidOperationException ex) when (
    ex.Message.Contains("does not have an editable level-music slot", StringComparison.Ordinal))
{
    musicRejected = true;
}
Require(musicRejected, "The generic music exporter did not fail closed for missing late-alternate row 35.");

string mainWindow = File.ReadAllText(Path.Combine(root, "src", "Spyro.Editor.App", "Views", "MainWindow.cs"));
string labWindow = File.ReadAllText(Path.Combine(root, "src", "Spyro.Editor.App", "Views", "MainWindow.Id65BlankLab.cs"));
string mobyExporter = File.ReadAllText(Path.Combine(root, "src", "Spyro.Editor.Core", "Exporting", "MobySourcePatchExporter.cs"));
Require(
    mainWindow.Contains("if (UnusedLevel65BlankLevelLabProfileRegistry.IsLabLevel(level))\n                continue;", StringComparison.Ordinal) &&
    mainWindow.Contains("ID65 Lab music save refused", StringComparison.Ordinal) &&
    labWindow.Contains("UnusedLevel65BlankLevelLabProfileRegistry.Key,\n                mobys", StringComparison.Ordinal) &&
    mobyExporter.Contains("private const long TreasureTotalTableImageOffset = 0x7945FB0;", StringComparison.Ordinal),
    "One source-level generic ID65 fail-closed/stale-address guard changed.");
Require(
    UnusedLevel65BlankLevelLabProfileRegistry.Capabilities.PortalExitRouting == UnusedLevel65BlankLevelLabCapabilityState.Unavailable &&
    UnusedLevel65BlankLevelLabProfileRegistry.Capabilities.SaveOwnership == UnusedLevel65BlankLevelLabCapabilityState.Unavailable &&
    UnusedLevel65BlankLevelLabProfileRegistry.Capabilities.ObjectMutation == UnusedLevel65BlankLevelLabCapabilityState.Unavailable &&
    UnusedLevel65BlankLevelLabProfileRegistry.Capabilities.NormalCreateBin == UnusedLevel65BlankLevelLabCapabilityState.Unavailable,
    "The public Lab capability profile no longer fails closed for behavior ownership.");

bool wrongHashRejected = false;
try
{
    _ = await UnusedLevel65StandaloneBehaviorOwnershipInspector.InspectAsync(
        root,
        physicalClone,
        foundation);
}
catch (InvalidDataException ex) when (ex.Message.Contains("SHA-256", StringComparison.Ordinal))
{
    wrongHashRejected = true;
}
Require(wrongHashRejected, "A stale/wrong ID65 base was accepted by the ownership contract.");

string lockedHashAfter = await HashFileAsync(lockedBase);
string foundationHashAfter = await HashFileAsync(foundation);
DirectorySnapshot candidateDirectoryAfter = SnapshotDirectory(Path.GetDirectoryName(foundation)!);
Require(
    lockedHashBefore == lockedHashAfter &&
    foundationHashBefore == foundationHashAfter &&
    candidateDirectoryBefore == candidateDirectoryAfter,
    "Static ownership inspection changed an exact candidate or its directory.");
Require(
    !File.Exists(Path.Combine(root, "_local", "never-written-ID65-music.bin")) &&
    !File.Exists(Path.Combine(root, "_local", "never-written-ID65-music.cue")),
    "The rejected generic music check wrote an artifact.");

Console.WriteLine("PASS: exact ID65 standalone behavior ownership contract is deterministic and read-only.");
Console.WriteLine($"Locked base SHA-256: {lockedHashAfter}");
Console.WriteLine($"Foundation SHA-256: {foundationHashAfter}");
Console.WriteLine("Spawn: inherited landing/T92 exact; coupled authored writer + death/respawn gate required.");
Console.WriteLine("Music: initial slot 35=track 19; late row 35 missing; generic editor rejects.");
Console.WriteLine("Totals: dragon/gem slot 35=0; executable egg row 35 and save egg row 35 missing.");
Console.WriteLine("Exit: guard is <65; T96/T97 retained; Gnasty's World has only 61/62/63/64.");
Console.WriteLine("Save: 36-slot visited/dragon/gem/retirement layout statically round-trips index 35; card/death proof pending.");
Console.WriteLine("Artifacts written: 0 BIN, 0 CUE, 0 manifest; normal Create BIN/promotion remain false.");

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void RequireFile(string path, string label)
{
    if (!File.Exists(path))
        throw new FileNotFoundException($"Missing {label}.", path);
}

static async Task<string> HashFileAsync(string path)
{
    await using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
}

static DirectorySnapshot SnapshotDirectory(string path)
{
    string[] directories = Directory.GetDirectories(path, "*", SearchOption.AllDirectories)
        .Select(value => Path.GetRelativePath(path, value))
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();
    string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
        .Select(value => $"{Path.GetRelativePath(path, value)}|{new FileInfo(value).Length}|{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(value))).ToLowerInvariant()}")
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();
    return new DirectorySnapshot(
        string.Join('\n', directories),
        string.Join('\n', files));
}

internal sealed record DirectorySnapshot(string Directories, string Files);
