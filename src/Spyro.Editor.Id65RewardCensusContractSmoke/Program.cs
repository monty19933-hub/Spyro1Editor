using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

string root = FindRepositoryRoot(args.ElementAtOrDefault(0));
string lockedDirectory = Path.Combine(
    root,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-display-name");
string lockedImage = Path.Combine(
    lockedDirectory,
    "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE.bin");
string wrongBase = Path.Combine(
    root,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-physical-clone",
    "Unused-Level-65-Town-Square-independent-storage-RUNTIME-CANDIDATE.bin");
Require(File.Exists(lockedImage), "The exact locked ID65 display-name BIN is missing.");
Require(File.Exists(wrongBase), "The stale physical-clone wrong-base fixture is missing.");

DirectoryState directoryBefore = SnapshotDirectory(lockedDirectory);
FileState lockedBefore = SnapshotFile(lockedImage);
FileState wrongBefore = SnapshotFile(wrongBase);

Id65RewardCensusSourceSnapshot source = Id65RewardCensusContract.ReadLockedSource(lockedImage);
Id65RewardCensus first = Id65RewardCensusContract.Inspect(source);
Id65RewardCensus repeat = Id65RewardCensusContract.Inspect(source);
IndependentResult independent = IndependentlyInspect(source);

Require(
    first.ProfileId == Id65RewardCensusContract.ProfileId &&
    Id65RewardCensusContract.RequiredSmokeAssemblyName ==
        "Spyro.Editor.Id65RewardCensusContractSmoke" &&
    first.LockedSourceImageSha256 ==
        "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8" &&
    first.ObjectRecordCount == 107,
    "The reward census source/profile identity changed.");
Require(
    first.RewardRowCount == 81 && first.RewardTotal == 200 &&
    first.LooseGemRowCount == 43 && first.NativeDropRowCount == 38 &&
    first.ValueHistogram.OrderBy(pair => pair.Key).SequenceEqual(
        new Dictionary<int, int> { [1] = 30, [2] = 35, [5] = 12, [10] = 4 }),
    "The exact 81-row/200 reward census changed.");
Require(
    independent.RewardRows.Count == first.RewardRows.Count &&
    independent.RewardTotal == first.RewardTotal &&
    independent.LooseCount == first.LooseGemRowCount &&
    independent.DropCount == first.NativeDropRowCount &&
    independent.RewardRowsSha256 == first.Dependencies.RewardRowsSha256 &&
    independent.RewardPropertiesByteLength == first.Dependencies.RewardPropertiesByteLength &&
    independent.RewardPropertiesSha256 == first.Dependencies.RewardPropertiesSha256,
    "The smoke did not independently reproduce the reward row/property census.");

for (int index = 0; index < first.RewardRows.Count; index++)
{
    Id65RewardCensusRow actual = first.RewardRows[index];
    IndependentReward expected = independent.RewardRows[index];
    Require(
        actual.TrueIndex == expected.TrueIndex && actual.ActorClass == expected.ActorClass &&
        actual.RewardValue == expected.Value && actual.Encoding == expected.Encoding &&
        actual.OwnerSemantics == expected.OwnerSemantics && actual.AuthoredOwnerRow &&
        !actual.TransientRewardChildAddsAuthoredRow &&
        !actual.TransientRewardChildOwnsRetirementBit &&
        actual.TransientRewardChildRuntimeProofVerified ==
            (expected.OwnerSemantics != Id65RewardOwnerSemantics.StationaryChestCarrier) &&
        actual.RowSha256 == expected.RowSha256 &&
        actual.PropertiesSceneOffset == expected.PropertiesSceneOffset &&
        actual.PropertiesByteLength == expected.PropertiesByteLength &&
        actual.PropertiesSha256 == expected.PropertiesSha256 &&
        actual.RetirementByteIndex == expected.TrueIndex / 8 &&
        actual.RetirementBitMask == (byte)(1 << (expected.TrueIndex % 8)),
        $"Reward row T{expected.TrueIndex} did not match the independent decoder.");
}

const string ExpectedLedger =
    "0017:1=7\n" +
    "0017:2=0,1,5,8,11\n" +
    "0017:5=9\n" +
    "0017:10=2\n" +
    "0053:1=21,22,24,25,28,34,35,36,37,39,41,42,43,57,58,59,71,75,76,77,86,90\n" +
    "0054:2=23,29,38,47,53,54,55,60,61,72,73,74,78,87,89,93,94,95\n" +
    "0055:5=56,70,91\n" +
    "00C2:1=30,32,44,45,79\n" +
    "00C2:2=31,33,46,64\n" +
    "00C2:5=40,65,81\n" +
    "00C2:10=80\n" +
    "00C3:1=68\n" +
    "00C3:2=27,50,51,63,67,83,85\n" +
    "00C3:5=26,62,66,84\n" +
    "00C3:10=52\n" +
    "0149:5=82\n" +
    "0186:10=69\n" +
    "018B:1=10\n" +
    "018B:2=6";
string contractLedger = string.Join('\n', first.ClassValueIndexLedger.Select(entry =>
    $"{entry.ActorClass:X4}:{entry.RewardValue}={string.Join(',', entry.TrueIndexes)}"));
Require(contractLedger == ExpectedLedger && independent.Ledger == ExpectedLedger,
    "The exact class/value/index ledger changed.");

Id65RewardResidentDependencyClosure dependencies = first.Dependencies;
Require(
    dependencies.Id65DataSha256 == "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0" &&
    dependencies.Id65DataSha256 == independent.Id65DataSha256 &&
    dependencies.RetailTownSquareDataSha256 == independent.TownSquareDataSha256 &&
    dependencies.Id65DataIsExactTownSquareClone && independent.Id65DataIsTownSquareClone &&
    dependencies.ObjectTableSha256 == "2d5743b6895cb6142150812e06eb772b492ab21665edec239e17e98b9ed2af1d" &&
    dependencies.ObjectTableSha256 == independent.ObjectTableSha256 &&
    dependencies.RewardRowsSha256 == "a0078220e572b5ed440a447b5e2b20c62777541e8076cab3db66458c3d2d771f" &&
    dependencies.RewardPropertiesByteLength == 0xAF8 &&
    dependencies.RewardPropertiesSha256 == "cca79363953ac546425ba5fc0431185037225252786e156e1b37a795973f01bf" &&
    dependencies.RewardPropertiesInternalFixupCount == 18,
    "The exact reward data/row/property dependency closure changed.");
Require(
    dependencies.SceneSha256 == "63b5e699b3f175c0289a795c4f6f36786b9bc33ce608e1f1e9bd7bb4c04f09f6" &&
    dependencies.SceneSha256 == independent.SceneSha256 &&
    dependencies.ScenePointerFixupCount == 129 &&
    dependencies.ScenePointerFixupSha256 == "019e1b2c30b7e5f7f9a851175fe93940ea1cf148fbc37f8512781aaed6587dc2" &&
    dependencies.ScenePointerFixupSha256 == independent.FixupSha256 &&
    dependencies.ObjectRowPointerFixupCount == 107 &&
    dependencies.RewardRowPointerFixupCount == 81 &&
    dependencies.InternalScenePointerFixupCount == 22 &&
    independent.ObjectPointerFixupCount == 107 && independent.RewardPointerFixupCount == 81 &&
    independent.InternalPointerFixupCount == 22 && independent.RewardInternalFixupCount == 18,
    "The exact scene/fixup dependency closure changed.");
Require(
    dependencies.ActorSubfileSha256 == "a37b7a8e5e5e660befed91e63c699a6c6d4b25ff8406f68b08e418c00d2a711d" &&
    dependencies.ActorSubfileSha256 == independent.ActorSubfileSha256 &&
    dependencies.ActorRootHeaderSha256 == "f29e96e70f24dfb0cc0ceda02861f3832a918b74df6f8b6cb4cc5f8ff3720398" &&
    dependencies.ActorRootHeaderSha256 == independent.ActorRootHeaderSha256 &&
    dependencies.ActorPackages.Count == 10 &&
    dependencies.ActorPackages.Select(package => package.Sha256)
        .SequenceEqual(independent.PackageSha256s),
    "The exact resident actor-package dependency closure changed.");
Require(
    dependencies.Id65OverlaySha256 == "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5" &&
    dependencies.Id65OverlaySha256 == independent.Id65OverlaySha256 &&
    dependencies.Id65OverlayIsExactTownSquareClone && independent.Id65OverlayIsTownSquareClone &&
    dependencies.NativeTextureRecordCount == 66 &&
    dependencies.NativeTexturePagesSha256 == "5fb81c4ac63eb235a172c4f16f83ef08efb148a6f543ef6100ba21359ade408f" &&
    dependencies.NativeTexturePagesSha256 == independent.TexturePagesSha256 &&
    dependencies.NativeTextureComponentByteLength == 0x2F78 &&
    dependencies.NativeTextureComponentSha256 == "f0ee8ff0d2e8554418b1bb17de860fada7af5106b07bf4908dfee179aaee6e92" &&
    dependencies.NativeTextureComponentSha256 == independent.TextureComponentSha256 &&
    dependencies.TerrainModelSha256 == "1aa6950fe78e71ef4506d823fd33806c13a32838cdb00aadc4e35daffcf17f47" &&
    dependencies.TerrainModelSha256 == independent.ModelSha256 &&
    dependencies.ExecutableSha256 == "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442" &&
    dependencies.ExecutableSha256 == independent.ExecutableSha256,
    "The exact overlay/texture/SCUS dependency closure changed.");
Require(
    dependencies.PackageClosurePinned && dependencies.PropertyClosurePinned &&
    dependencies.FixupClosurePinned && dependencies.OverlayClosurePinned &&
    dependencies.TextureClosurePinned && dependencies.ExecutableClosurePinned &&
    dependencies.GlobalOrOverlayResidentClassesWithoutActorRoots.SequenceEqual(
        new ushort[] { 0x0053, 0x0054, 0x0055, 0x011E }),
    "A resident dependency pin or rootless class boundary changed.");

Require(first.DragonBundles.Count == 4 && independent.Dragons.Count == 4,
    "The exact four dragon bundles were not reproduced.");
for (int index = 0; index < first.DragonBundles.Count; index++)
{
    Id65RewardDragonBundle actual = first.DragonBundles[index];
    IndependentDragon expected = independent.Dragons[index];
    Require(
        actual.PedestalTrueIndex == expected.PedestalTrueIndex &&
        actual.DragonTrueIndex == expected.DragonTrueIndex &&
        actual.ContainerTrueIndex == expected.ContainerTrueIndex &&
        actual.PedestalRowSha256 == expected.PedestalRowSha256 &&
        actual.DragonRowSha256 == expected.DragonRowSha256 &&
        actual.ContainerRowSha256 == expected.ContainerRowSha256 &&
        actual.ThreeRowBundleSha256 == expected.BundleSha256 &&
        actual.CameraDataWadOffset == expected.CameraWadOffset &&
        actual.CameraDataSha256 == expected.CameraSha256 &&
        actual.SceneLinkWadOffset == expected.LinkWadOffset &&
        actual.SceneLinkSha256 == expected.LinkSha256 &&
        actual.CameraTrackWadOffset == expected.TrackWadOffset &&
        actual.CameraTrackByteLength == expected.TrackByteLength &&
        actual.CameraTrackFrameCount == expected.TrackFrameCount &&
        actual.CameraTrackSha256 == expected.TrackSha256 &&
        !actual.CameraPlacementResolved && !actual.RoutePlacementResolved &&
        !actual.DestinationSupportResolved,
        $"Dragon T{expected.DragonTrueIndex} did not match the independent row/camera/link/track fingerprint.");
}

Require(
    first.ZeroEgg.RequiredEggTarget == 0 && first.ZeroEgg.EggThiefTrueIndex == 88 &&
    first.ZeroEgg.EggThiefClass == 0x0021 && first.ZeroEgg.CarriedEggClass == 0x22 &&
    first.ZeroEgg.EggThiefRowSha256 == "dccfa0a9489f9d76c93958a1a43e64ea948262b51634d03ddc311cdf3ba1a09a" &&
    first.ZeroEgg.EggThiefPropertiesSceneOffset == 0x5AFC &&
    first.ZeroEgg.EggThiefPropertiesByteLength == 0x134 &&
    first.ZeroEgg.EggThiefPropertiesSha256 == "6213aceb752cb139ac669b392a3faf7441888306b363fb4cf3e2c63f45715136" &&
    first.ZeroEgg.PropertiesPointerFixupCount == 1 && first.ZeroEgg.InternalPropertiesFixupCount == 1 &&
    !first.ZeroEgg.LockedSourceAlreadySatisfiesZeroEgg && first.ZeroEgg.T88ReplacementRequired &&
    !first.ZeroEgg.WriterAuthorized && independent.ZeroEggVerified,
    "The exact T88 zero-egg requirement changed.");
Require(
    first.NonProgressionRetirementCandidates.Select(item => item.TrueIndex).SequenceEqual(new[] { 98, 99 }) &&
    first.NonProgressionRetirementCandidates.All(item =>
        item.ActorClass == 0x011E && !item.RetirementResolved && !item.RetirementAuthorized) &&
    first.NonProgressionRetirementCandidates.Select(item => item.RowSha256).SequenceEqual(
        new[]
        {
            "ace3bdc7adeb3bedbf004b82d08479e3a7b6ffd81b678aa37d28bb3147932e64",
            "96fa444f14942c9f7131166c33dee94af9480dd0d25e696d7a74a44d62f891c3"
        }) && independent.NonProgressionVerified,
    "The unresolved T98/T99 nonprogression retirement boundary changed.");
Require(
    first.RewardRetirementOwnerCandidates.Count == 81 &&
    first.RewardRetirementOwnerCandidates.Select(owner => owner.TrueIndex)
        .SequenceEqual(first.RewardRows.Select(row => row.TrueIndex)) &&
    first.RewardRetirementOwnerCandidates.All(owner =>
        owner.TrueIndex <= 255 && !owner.RetirementAuthorized) &&
    first.RetirementOwnerMaximumTrueIndex == 255 &&
    first.CurrentObjectTableWithinRetirementOwnerBound &&
    first.EveryRewardOwnerWithinRetirementOwnerBound && !first.RewardRetirementResolved,
    "The retirement-owner candidate mapping or <=255 bound changed.");
Id65RewardOwnerAggregate owners = first.OwnerAggregate;
Require(
    owners.StationaryOwnerRowCount == 71 && owners.StationaryOwnerRewardTotal == 171 &&
    owners.BehaviorEnemyOwnerRowCount == 10 && owners.BehaviorEnemyOwnerRewardTotal == 29 &&
    owners.BullOwnerRowCount == 8 && owners.BullOwnerRewardTotal == 26 &&
    owners.BullTrueIndexes.SequenceEqual(new[] { 0, 1, 2, 5, 7, 8, 9, 11 }) &&
    owners.TorroOwnerRowCount == 2 && owners.TorroOwnerRewardTotal == 3 &&
    owners.TorroTrueIndexes.SequenceEqual(new[] { 6, 10 }) &&
    owners.StationaryLooseGemOwnerRowCount == 43 && owners.StationaryLooseGemRewardTotal == 73 &&
    owners.StationaryChestCarrierRowCount == 28 && owners.StationaryChestCarrierRewardTotal == 98 &&
    owners.StationaryChestCarrierClasses.SequenceEqual(new ushort[] { 0x00C2, 0x00C3, 0x0149, 0x0186 }) &&
    owners.ChestCarrierRowOwnsRewardValue && owners.ChestCarrierRowOwnsRetirementBit &&
    !owners.TransientChildrenCountedAsAdditionalRows &&
    !owners.TransientChildrenOwnAdditionalRetirementBits &&
    !owners.RuntimeDuplicateAndChildBitProofVerified &&
    independent.StationaryOwnerRowCount == 71 && independent.StationaryOwnerRewardTotal == 171 &&
    independent.EnemyOwnerRowCount == 10 && independent.EnemyOwnerRewardTotal == 29 &&
    independent.ChestCarrierRowCount == 28 && independent.ChestCarrierRewardTotal == 98,
    "The 71/171 stationary, 10/29 enemy, or transient chest-child accounting changed.");
Require(
    first.Rejected448By480Placement == new Id65RewardRejectedPlacement(
        448,
        480,
        "rejected destination authoring coordinates",
        true,
        "No support, route, or camera acceptance evidence exists at 448x480.") &&
    !first.BullBehaviorResolved && !first.TorroBehaviorResolved &&
    !first.DestinationSupportResolved && !first.RoutePlacementResolved &&
    !first.DragonCameraPlacementResolved && first.UnresolvedAuthoringBlockers.Count == 10,
    "An unresolved destination/Bull/Torro/448x480 boundary changed.");
Require(
    first.UnresolvedAuthoringBlockers.Any(text => text.Contains("Bull", StringComparison.Ordinal) &&
        text.Contains("Torro", StringComparison.Ordinal)) &&
    first.UnresolvedAuthoringBlockers.Any(text => text.Contains("T98/T99", StringComparison.Ordinal)) &&
    first.UnresolvedAuthoringBlockers.Any(text => text.Contains("448x480", StringComparison.Ordinal)) &&
    first.UnresolvedAuthoringBlockers.Any(text => text.Contains("camera", StringComparison.OrdinalIgnoreCase)) &&
    first.UnresolvedAuthoringBlockers.Any(text => text.Contains("support", StringComparison.OrdinalIgnoreCase)) &&
    first.UnresolvedAuthoringBlockers.Any(text => text.Contains("route", StringComparison.OrdinalIgnoreCase)) &&
    first.UnresolvedAuthoringBlockers.Any(text => text.Contains("memory-card", StringComparison.OrdinalIgnoreCase)) &&
    first.UnresolvedAuthoringBlockers.Any(text => text.Contains("transient-child", StringComparison.OrdinalIgnoreCase)),
    "The fail-closed blocker disclosure is incomplete.");

Require(
    first.DeterministicCensusSha256 == repeat.DeterministicCensusSha256 &&
    first.DeterministicCensusSha256 ==
        "b413e471d9fbfc13913bfc5948fefdd7316884a80be532bb635708f50063a491" &&
    first.DeterministicCensusSha256 == Id65RewardCensusContract.ExpectedDeterministicCensusSha256,
    "The reward census is not deterministic.");
Require(
    first.LockedSourcePreserved && first.StaticReadOnly && !first.ProducesPatches &&
    !first.WritesBin && !first.WritesCue && !first.WriterAuthorized &&
    !first.PublisherAuthorized && !first.AppIntegrated && !first.NormalCreateBinEnabled &&
    !first.RuntimeVerified && !first.CollectionRuntimeVerified && !first.DeathRuntimeVerified &&
    !first.ReentryRuntimeVerified && !first.MemoryCardMatrixVerified && first.NoCardPersistenceClaim &&
    !first.PromotionAuthorized && !first.ReleaseAuthorized,
    "The read-only reward census authorized a writer, publisher, App, Create BIN, runtime, promotion, or release path.");

bool wrongBaseRejected = false;
try
{
    _ = Id65RewardCensusContract.ReadLockedSource(wrongBase);
}
catch (InvalidDataException exception) when (exception.Message.Contains("SHA-256", StringComparison.Ordinal))
{
    wrongBaseRejected = true;
}
Require(wrongBaseRejected, "A stale/wrong ID65 base was accepted.");

byte[] tamperedData = source.Id65Data.ToArray();
tamperedData[0x1D0000 + 0x170 + (21 * 0x58) + 0x36] ^= 1;
ExpectTamperReject(source with { Id65Data = tamperedData }, "reward-row tamper");
byte[] tamperedChestCarrier = source.Id65Data.ToArray();
tamperedChestCarrier[0x1D0000 + 0x170 + (30 * 0x58) + 0x53] = 0x54;
ExpectTamperReject(source with { Id65Data = tamperedChestCarrier },
    "chest-carrier transient-child accounting tamper");
byte[] tamperedOverlay = source.Id65Overlay.ToArray();
tamperedOverlay[0x100] ^= 1;
ExpectTamperReject(source with { Id65Overlay = tamperedOverlay }, "overlay tamper");
byte[] tamperedExecutable = source.Executable.ToArray();
tamperedExecutable[0x800] ^= 1;
ExpectTamperReject(source with { Executable = tamperedExecutable }, "SCUS tamper");
ExpectTamperReject(source with { SourceImageSha256 = new string('0', 64) }, "identity tamper");

Require(SnapshotFile(lockedImage) == lockedBefore, "The reward census changed the locked source BIN.");
Require(SnapshotFile(wrongBase) == wrongBefore, "The wrong-base reject changed its source BIN.");
Require(SameDirectoryState(SnapshotDirectory(lockedDirectory), directoryBefore),
    "The reward census changed any file or directory beside the locked source.");
Require(
    Directory.GetFiles(lockedDirectory, "*.bin", SearchOption.AllDirectories).Length ==
        directoryBefore.Files.Count(file => file.Path.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)) &&
    Directory.GetFiles(lockedDirectory, "*.cue", SearchOption.AllDirectories).Length ==
        directoryBefore.Files.Count(file => file.Path.EndsWith(".cue", StringComparison.OrdinalIgnoreCase)),
    "The read-only census emitted an output BIN or CUE.");

Console.WriteLine("PASS: exact ID65 reward census is static, read-only, and fail-closed.");
Console.WriteLine("Rewards: 81 rows / 200 total; loose 43, native drops 38; exact class/value/index ledger pinned.");
Console.WriteLine("Dragons: four exact three-row bundles with row/camera/link/container/track fingerprints pinned.");
Console.WriteLine("Dependencies: packages, properties, 129 fixups, Town Square overlay, 66 textures, and SCUS pinned.");
Console.WriteLine("Zero eggs: T88 replacement required; Bull/Torro and T98/T99 retirement remain unresolved.");
Console.WriteLine("Placement: support/route/camera unresolved; 448x480 rejected; retirement owner maximum T255.");
Console.WriteLine($"censusSha256={first.DeterministicCensusSha256}");
Console.WriteLine("Artifacts written: 0 BIN, 0 CUE; writer/publisher/App/Create BIN/runtime/release/promotion all false.");

static IndependentResult IndependentlyInspect(Id65RewardCensusSourceSnapshot source)
{
    const long dataWadOffset = 0x6936800;
    const int sceneOffset = 0x1D0000;
    const int sceneLength = 0x8800;
    const int tableOffset = sceneOffset + 0x170;
    const int stride = 0x58;
    byte[] data = source.Id65Data.ToArray();
    byte[] townSquare = source.RetailTownSquareData.ToArray();
    byte[] scene = data.AsSpan(sceneOffset, sceneLength).ToArray();
    byte[][] rows = Enumerable.Range(0, 107)
        .Select(index => data.AsSpan(tableOffset + (index * stride), stride).ToArray())
        .ToArray();
    int[] pointers = rows.Select(row => checked((int)ReadUInt32(row, 0))).Distinct().Order().ToArray();
    Require(pointers.Length == 107, "Independent property-pointer census is not one-to-one.");

    List<IndependentReward> rewards = [];
    List<byte[]> rewardRows = [];
    List<byte[]> rewardProperties = [];
    foreach ((byte[] row, int trueIndex) in rows.Select((row, index) => (row, index)))
    {
        (Id65RewardEncoding Encoding, int Value)? reward = DecodeReward(row);
        if (reward == null)
            continue;
        int pointer = checked((int)ReadUInt32(row, 0));
        int pointerIndex = Array.BinarySearch(pointers, pointer);
        int next = pointerIndex + 1 < pointers.Length ? pointers[pointerIndex + 1] : 0x7F28;
        byte[] properties = scene.AsSpan(pointer, next - pointer).ToArray();
        ushort actorClass = ReadUInt16(row, 0x36);
        rewards.Add(new(
            trueIndex,
            actorClass,
            reward.Value.Encoding,
            ClassifyOwner(actorClass),
            reward.Value.Value,
            Hash(row),
            checked((uint)pointer),
            properties.Length,
            Hash(properties)));
        rewardRows.Add(row);
        rewardProperties.Add(properties);
    }
    string ledger = string.Join('\n', rewards
        .GroupBy(row => (row.ActorClass, row.Value))
        .OrderBy(group => group.Key.ActorClass)
        .ThenBy(group => group.Key.Value)
        .Select(group => $"{group.Key.ActorClass:X4}:{group.Key.Value}={string.Join(',', group.Select(row => row.TrueIndex).Order())}"));

    byte[] fixups = scene.AsSpan(0x7F28, 4 + (129 * 4)).ToArray();
    Require(ReadInt32(fixups, 0) == 129, "Independent fixup count changed.");
    int[] fixupFields = Enumerable.Range(0, 129)
        .Select(index => ReadInt32(fixups, 4 + (index * 4)))
        .ToArray();
    int objectPointerFixups = Enumerable.Range(0, 107)
        .Count(index => fixupFields.Count(value => value == 0x170 + (index * stride)) == 1);
    int rewardPointerFixups = rewards.Count(row =>
        fixupFields.Count(value => value == 0x170 + (row.TrueIndex * stride)) == 1);
    int rewardInternalFixups = fixupFields.Count(value => rewards.Any(row =>
        value >= row.PropertiesSceneOffset && value < row.PropertiesSceneOffset + row.PropertiesByteLength));

    IndependentPackageSpec[] packageSpecs =
    [
        new(0x0017, 4, "3b5a1a3a1cedd3c67445f6dab9999287ce2f0c6d717a6a2473f9b7e28de4315c"),
        new(0x014B, 5, "e92ef8e3a4086c670d90e90871ce7eec2659dce00a2a3fcf3ce42319e3e35f66"),
        new(0x018B, 6, "288c02cbf9c1332e026822cd5ff2e1c1ab1bd2ba4756134697060398beb098ae"),
        new(0x00C3, 10, "b4df3f979bd8bbda73a94225a6a1202feb4942d3d823a2a86be0f2cea2b3942d"),
        new(0x00C2, 11, "06bcdfa6dbe9b1848b69e7fc14f7247d2ff2fb78e1e8203926eba7851f6e8da7"),
        new(0x0186, 12, "6ecb4e774fe27c79e2d27239a912a56de7b8c2ec3ba93d64fe9040208eae06d5"),
        new(0x0149, 13, "963d4758a0044d5bccbe0d2d137e7d152ebf35b1cbdf04e2cc6c02423eebb0a4"),
        new(0x0021, 14, "bdf32e953dd985712eef54e7f0fa4dcd1ab8fb7b336aa8ecedd73237092228a8"),
        new(0x006E, 16, "9a8abc39810277ca58be7c2e59b6dcd215e066cef13872d38b84cb9e6790322f"),
        new(0x00FA, 35, "bd1a8146c9484443d9efcb9c7e638e9fbc4f1e88e7fcaad48311cd965f73effb")
    ];
    int[] roots = Enumerable.Range(0, 64).Select(index => ReadInt32(data, 0x50 + (index * 4))).ToArray();
    ushort[] actorIds = Enumerable.Range(0, 64).Select(index => ReadUInt16(data, 0x150 + (index * 2))).ToArray();
    Require(Array.IndexOf(roots, 0) == 37, "Independent actor-root count changed.");
    List<string> packageHashes = [];
    foreach (IndependentPackageSpec package in packageSpecs)
    {
        Require(actorIds[package.RootIndex] == package.ActorClass,
            $"Independent package class 0x{package.ActorClass:X4} changed.");
        int end = roots[package.RootIndex + 1];
        string hash = Hash(data.AsSpan(roots[package.RootIndex], end - roots[package.RootIndex]));
        Require(hash == package.Sha256, $"Independent package 0x{package.ActorClass:X4} hash changed.");
        packageHashes.Add(hash);
    }

    IndependentDragonSpec[] dragonSpecs =
    [
        new(3, 12, 102, 0x6B0BB68, "337089cca99f7cdb7b185dab6a976915275a6f6c0a5e2a3caa9c314f804010c4", 0x6B0C654, "938be65ab4af758a0e697963eaf301a641c98f6d9b58983f9ee0e20c6009bbe2", 0x6B5656C, 0x2220, "badcecba3c34777d781e3416b78fecf508edb5da108649c631f0a81670e71757"),
        new(4, 13, 103, 0x6B0BBBC, "14ffe69959cb0bbc459786fa231a3c6fbb3131e7a342a0bb3886f824ec091f6a", 0x6B0C67C, "54a3b76ada73128be8a4f9bcc4fde587f9dbb1d776ffdcc3163a6e836465f14c", 0x6B99060, 0x1EF0, "438f71d29ee0d046a6a024af622dd0d1347099a2c4be40400714a48b5d842053"),
        new(48, 49, 104, 0x6B0BF6C, "0617613f84e44d2f6972e848b63744c232f4b350b480fabe265b43ad621574d1", 0x6B0C6A4, "efd9553bc155eb898b149a4429abe1640142253b6b01c1bc7d008a0108859185", 0x6BF4CE8, 0x2B68, "a3b707b312d131ba405d92d82fe9dd240975f6bdeb6416051eb0b8061f861c4e"),
        new(100, 101, 105, 0x6B0C600, "443ab95f3cbcbbfcc2f0918c4556e3c306d20ceafa7f882b76b150e30828348e", 0x6B0C6CC, "5fd648b140288a52b3e5518683576d49f5f3bee0502557898a013b4eebfa83d8", 0x6C17D68, 0xA80, "a61145a17e20759a292fc508fb43453728341c5bb2c65f7f286c294349895156")
    ];
    int[] actorIndexes = rows.Select((row, index) => (row, index))
        .Where(item => ReadUInt16(item.row, 0x36) == 0x00FA)
        .Select(item => item.index).ToArray();
    int[] pedestalIndexes = rows.Select((row, index) => (row, index))
        .Where(item => ReadUInt16(item.row, 0x36) == 0x014B)
        .Select(item => item.index).ToArray();
    int[] containerIndexes = rows.Select((row, index) => (row, index))
        .Where(item => ReadUInt16(item.row, 0x36) == 0x006E)
        .Select(item => item.index).ToArray();
    Require(actorIndexes.SequenceEqual(new[] { 12, 13, 49, 101 }) &&
            pedestalIndexes.SequenceEqual(new[] { 3, 4, 48, 100 }) &&
            containerIndexes.SequenceEqual(new[] { 102, 103, 104, 105 }),
        "Independent dragon triple census changed.");
    List<IndependentDragon> dragons = [];
    foreach (IndependentDragonSpec spec in dragonSpecs)
    {
        byte[] pedestal = rows[spec.PedestalTrueIndex];
        byte[] dragon = rows[spec.DragonTrueIndex];
        byte[] container = rows[spec.ContainerTrueIndex];
        int cameraPointer = checked((int)ReadUInt32(dragon, 0));
        int linkPointer = checked((int)ReadUInt32(container, 0));
        byte[] camera = scene.AsSpan(cameraPointer, 0x44).ToArray();
        byte[] link = scene.AsSpan(linkPointer, 0x28).ToArray();
        int cutsceneIndex = ReadInt32(camera, 0x18);
        int cutsceneOffset = checked((int)ReadUInt32(data, 0x20 + (cutsceneIndex * 8)));
        int trackRelative = checked((int)ReadUInt32(data, cutsceneOffset + 0x1C));
        int trackLength = checked((int)ReadUInt32(data, cutsceneOffset + 0x20));
        int trackOffset = cutsceneOffset + trackRelative;
        long cameraWadOffset = dataWadOffset + sceneOffset + cameraPointer;
        long linkWadOffset = dataWadOffset + sceneOffset + linkPointer;
        long trackWadOffset = dataWadOffset + trackOffset;
        Require(cameraWadOffset == spec.CameraWadOffset && Hash(camera) == spec.CameraSha256 &&
                linkWadOffset == spec.LinkWadOffset && Hash(link) == spec.LinkSha256 &&
                trackWadOffset == spec.TrackWadOffset && trackLength == spec.TrackByteLength &&
                Hash(data.AsSpan(trackOffset, trackLength)) == spec.TrackSha256,
            $"Independent dragon T{spec.DragonTrueIndex} component hash changed.");
        dragons.Add(new(
            spec.PedestalTrueIndex,
            spec.DragonTrueIndex,
            spec.ContainerTrueIndex,
            Hash(pedestal),
            Hash(dragon),
            Hash(container),
            Hash(Join([pedestal, dragon, container])),
            cameraWadOffset,
            Hash(camera),
            linkWadOffset,
            Hash(link),
            trackWadOffset,
            trackLength,
            trackLength / 24,
            Hash(data.AsSpan(trackOffset, trackLength))));
    }

    byte[] t88 = rows[88];
    int t88Pointer = checked((int)ReadUInt32(t88, 0));
    int t88Next = pointers[Array.BinarySearch(pointers, t88Pointer) + 1];
    bool zeroEggVerified = ReadUInt16(t88, 0x36) == 0x0021 && t88[0x53] == 0x22 &&
        Hash(t88) == "dccfa0a9489f9d76c93958a1a43e64ea948262b51634d03ddc311cdf3ba1a09a" &&
        t88Pointer == 0x5AFC && t88Next - t88Pointer == 0x134 &&
        Hash(scene.AsSpan(t88Pointer, t88Next - t88Pointer)) ==
            "6213aceb752cb139ac669b392a3faf7441888306b363fb4cf3e2c63f45715136";
    bool nonProgressionVerified = new[] { 98, 99 }.All(index =>
        ReadUInt16(rows[index], 0x36) == 0x011E &&
        (index == 98
            ? Hash(rows[index]) == "ace3bdc7adeb3bedbf004b82d08479e3a7b6ffd81b678aa37d28bb3147932e64"
            : Hash(rows[index]) == "96fa444f14942c9f7131166c33dee94af9480dd0d25e696d7a74a44d62f891c3"));

    byte[] model = data.AsSpan(0xDE800, 0x94800).ToArray();
    return new(
        rewards,
        rewards.Sum(row => row.Value),
        rewards.Count(row => row.Encoding == Id65RewardEncoding.LooseGem),
        rewards.Count(row => row.Encoding == Id65RewardEncoding.NativeDrop),
        rewards.Count(row => row.OwnerSemantics is Id65RewardOwnerSemantics.StationaryLooseGem or
            Id65RewardOwnerSemantics.StationaryChestCarrier),
        rewards.Where(row => row.OwnerSemantics is Id65RewardOwnerSemantics.StationaryLooseGem or
            Id65RewardOwnerSemantics.StationaryChestCarrier).Sum(row => row.Value),
        rewards.Count(row => row.OwnerSemantics is Id65RewardOwnerSemantics.BullEnemy or
            Id65RewardOwnerSemantics.TorroEnemy),
        rewards.Where(row => row.OwnerSemantics is Id65RewardOwnerSemantics.BullEnemy or
            Id65RewardOwnerSemantics.TorroEnemy).Sum(row => row.Value),
        rewards.Count(row => row.OwnerSemantics == Id65RewardOwnerSemantics.StationaryChestCarrier),
        rewards.Where(row => row.OwnerSemantics == Id65RewardOwnerSemantics.StationaryChestCarrier)
            .Sum(row => row.Value),
        ledger,
        Hash(Join(rewardRows)),
        Join(rewardProperties).Length,
        Hash(Join(rewardProperties)),
        Hash(data),
        Hash(townSquare),
        data.AsSpan().SequenceEqual(townSquare),
        Hash(Join(rows)),
        Hash(scene),
        Hash(fixups),
        objectPointerFixups,
        rewardPointerFixups,
        129 - objectPointerFixups,
        rewardInternalFixups,
        Hash(data.AsSpan(0x173000, 0x5D000)),
        Hash(data.AsSpan(0, 0x200)),
        packageHashes,
        Hash(source.Id65Overlay.Span),
        source.Id65Overlay.Span.SequenceEqual(source.RetailTownSquareOverlay.Span),
        Hash(data.AsSpan(0x800, 0xDE000)),
        Hash(model.AsSpan(0, 0x2F78)),
        Hash(model),
        Hash(source.Executable.Span),
        dragons,
        zeroEggVerified,
        nonProgressionVerified);
}

static (Id65RewardEncoding Encoding, int Value)? DecodeReward(byte[] row)
{
    int loose = row[0x36] switch
    {
        0x53 when row[0x4F] == 1 => 1,
        0x54 when row[0x4F] == 2 => 2,
        0x55 when row[0x4F] == 3 => 5,
        0x56 when row[0x4F] == 4 => 10,
        0x57 when row[0x4F] == 5 => 25,
        _ => 0
    };
    if (row[0x50] == 0x18 && loose > 0)
        return (Id65RewardEncoding.LooseGem, loose);
    int drop = row[0x53] switch
    {
        0x53 => 1,
        0x54 => 2,
        0x55 => 5,
        0x56 => 10,
        0x57 => 25,
        _ => 0
    };
    return drop == 0 ? null : (Id65RewardEncoding.NativeDrop, drop);
}

static Id65RewardOwnerSemantics ClassifyOwner(ushort actorClass) => actorClass switch
{
    0x0017 => Id65RewardOwnerSemantics.BullEnemy,
    0x018B => Id65RewardOwnerSemantics.TorroEnemy,
    0x00C2 or 0x00C3 or 0x0149 or 0x0186 => Id65RewardOwnerSemantics.StationaryChestCarrier,
    0x0053 or 0x0054 or 0x0055 => Id65RewardOwnerSemantics.StationaryLooseGem,
    _ => throw new InvalidDataException($"Independent reward class 0x{actorClass:X4} has no owner semantics.")
};

static void ExpectTamperReject(Id65RewardCensusSourceSnapshot source, string label)
{
    bool rejected = false;
    try
    {
        _ = Id65RewardCensusContract.Inspect(source);
    }
    catch (InvalidDataException)
    {
        rejected = true;
    }
    Require(rejected, $"The {label} was accepted.");
}

static string FindRepositoryRoot(string? requested)
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

static DirectoryState SnapshotDirectory(string path)
{
    FileState[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
        .Select(SnapshotFile)
        .OrderBy(file => file.Path, StringComparer.Ordinal)
        .ToArray();
    string[] directories = Directory.GetDirectories(path, "*", SearchOption.AllDirectories)
        .Select(directory => Path.GetRelativePath(path, directory))
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();
    return new(files, directories);
}

static bool SameDirectoryState(DirectoryState left, DirectoryState right) =>
    left.Files.SequenceEqual(right.Files) && left.Directories.SequenceEqual(right.Directories);

static FileState SnapshotFile(string path)
{
    FileInfo info = new(path);
    return new(path, info.Length, info.LastWriteTimeUtc, HashFile(path));
}

static string HashFile(string path)
{
    using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}

static byte[] Join(IEnumerable<byte[]> values)
{
    byte[][] arrays = values.ToArray();
    byte[] result = new byte[arrays.Sum(array => array.Length)];
    int cursor = 0;
    foreach (byte[] array in arrays)
    {
        array.CopyTo(result, cursor);
        cursor += array.Length;
    }
    return result;
}

static string Hash(ReadOnlySpan<byte> value) =>
    Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

static int ReadInt32(ReadOnlySpan<byte> value, int offset) =>
    BinaryPrimitives.ReadInt32LittleEndian(value.Slice(offset, 4));

static uint ReadUInt32(ReadOnlySpan<byte> value, int offset) =>
    BinaryPrimitives.ReadUInt32LittleEndian(value.Slice(offset, 4));

static ushort ReadUInt16(ReadOnlySpan<byte> value, int offset) =>
    BinaryPrimitives.ReadUInt16LittleEndian(value.Slice(offset, 2));

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record FileState(string Path, long Length, DateTime LastWriteTimeUtc, string Sha256);
internal sealed record DirectoryState(IReadOnlyList<FileState> Files, IReadOnlyList<string> Directories);
internal sealed record IndependentReward(
    int TrueIndex,
    ushort ActorClass,
    Id65RewardEncoding Encoding,
    Id65RewardOwnerSemantics OwnerSemantics,
    int Value,
    string RowSha256,
    uint PropertiesSceneOffset,
    int PropertiesByteLength,
    string PropertiesSha256);
internal sealed record IndependentPackageSpec(ushort ActorClass, int RootIndex, string Sha256);
internal sealed record IndependentDragonSpec(
    int PedestalTrueIndex,
    int DragonTrueIndex,
    int ContainerTrueIndex,
    long CameraWadOffset,
    string CameraSha256,
    long LinkWadOffset,
    string LinkSha256,
    long TrackWadOffset,
    int TrackByteLength,
    string TrackSha256);
internal sealed record IndependentDragon(
    int PedestalTrueIndex,
    int DragonTrueIndex,
    int ContainerTrueIndex,
    string PedestalRowSha256,
    string DragonRowSha256,
    string ContainerRowSha256,
    string BundleSha256,
    long CameraWadOffset,
    string CameraSha256,
    long LinkWadOffset,
    string LinkSha256,
    long TrackWadOffset,
    int TrackByteLength,
    int TrackFrameCount,
    string TrackSha256);
internal sealed record IndependentResult(
    IReadOnlyList<IndependentReward> RewardRows,
    int RewardTotal,
    int LooseCount,
    int DropCount,
    int StationaryOwnerRowCount,
    int StationaryOwnerRewardTotal,
    int EnemyOwnerRowCount,
    int EnemyOwnerRewardTotal,
    int ChestCarrierRowCount,
    int ChestCarrierRewardTotal,
    string Ledger,
    string RewardRowsSha256,
    int RewardPropertiesByteLength,
    string RewardPropertiesSha256,
    string Id65DataSha256,
    string TownSquareDataSha256,
    bool Id65DataIsTownSquareClone,
    string ObjectTableSha256,
    string SceneSha256,
    string FixupSha256,
    int ObjectPointerFixupCount,
    int RewardPointerFixupCount,
    int InternalPointerFixupCount,
    int RewardInternalFixupCount,
    string ActorSubfileSha256,
    string ActorRootHeaderSha256,
    IReadOnlyList<string> PackageSha256s,
    string Id65OverlaySha256,
    bool Id65OverlayIsTownSquareClone,
    string TexturePagesSha256,
    string TextureComponentSha256,
    string ModelSha256,
    string ExecutableSha256,
    IReadOnlyList<IndependentDragon> Dragons,
    bool ZeroEggVerified,
    bool NonProgressionVerified);
