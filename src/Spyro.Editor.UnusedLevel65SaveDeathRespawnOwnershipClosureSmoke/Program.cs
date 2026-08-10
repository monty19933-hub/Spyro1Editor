using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

const string ExpectedContractJsonSha256 =
    "f924ad745a09e4530f7694b0a6cd522f789ee1acee8cf685edb1aab0f3626060";

string root = FindRoot(args.ElementAtOrDefault(0));
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
string foundation = Path.Combine(
    root,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-full-authoring-foundation-native-membership",
    "Unused-Level-65-Full-Authoring-Foundation-HP-LP-45deg-NATIVE-MEMBERSHIP-RUNTIME-CANDIDATE.bin");
string runtimeEvidence = Path.Combine(
    root,
    "docs",
    "runtime-evidence",
    "unused-level-65-remote-blank-isolation-no-landing-game-over-2026-08-10.json");

Require(File.Exists(locked), "The exact locked display-name BIN is missing.");
Require(File.Exists(remote), "The exact remote-blank BIN is missing.");
Require(File.Exists(foundation), "The exact full-authoring foundation BIN is missing.");
Require(File.Exists(runtimeEvidence), "The exact runtime-failure evidence JSON is missing.");

string[] observedFiles = [locked, remote, foundation, runtimeEvidence];
Dictionary<string, FileStamp> before = observedFiles.ToDictionary(path => path, SnapshotFile);
string[] observedDirectories = observedFiles.Select(path => Path.GetDirectoryName(path)!)
    .Distinct(StringComparer.Ordinal).ToArray();
Dictionary<string, DirectoryStamp> directoriesBefore = observedDirectories.ToDictionary(
    path => path,
    SnapshotDirectory);

UnusedLevel65SaveDeathRespawnOwnershipClosureContract first =
    UnusedLevel65SaveDeathRespawnOwnershipClosure.Inspect(root, locked, remote, foundation);
UnusedLevel65SaveDeathRespawnOwnershipClosureContract second =
    UnusedLevel65SaveDeathRespawnOwnershipClosure.Inspect(root, locked, remote, foundation);

JsonSerializerOptions json = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
};
string firstJson = JsonSerializer.Serialize(first, json);
string secondJson = JsonSerializer.Serialize(second, json);
Require(firstJson == secondJson, "Two read-only ownership inspections were not deterministic.");
string contractHash = Hash(Encoding.UTF8.GetBytes(firstJson));
Require(contractHash == ExpectedContractJsonSha256, "The complete closure-contract JSON hash changed.");

Require(
    first.ProfileId == UnusedLevel65SaveDeathRespawnOwnershipClosure.ProfileId &&
    first.LockedDisplayNameImageSha256 ==
        "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8" &&
    first.RemoteBlankImageSha256 ==
        "8020947d4ab5e7e4b0eac4bc6409ff3d6f11212b0e118bbf007d870812014a8e" &&
    first.FoundationImageSha256 ==
        "92e4046ce4d14771ebb70a72c2a024b8e76f5575e38771f7067ff2b4303ac222" &&
    first.ExecutableSha256 ==
        "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442" &&
    first.RemoteRow80DataSha256 ==
        "8d10aa62b134414aec80eec10d1fcede13bd55806ee13f6eb7c752f0fa1a9061" &&
    first.RemoteObjectTableSha256 ==
        "f89347baf4f14739030499b21210b8ea46b44f9c4df02a1b58d39fde6ca1e64a" &&
    first.RemoteRow80MatchesFrozenConstruction &&
    first.RemoteExecutableMatchesLockedDisplayNameExecutable,
    "The exact locked/remote/foundation substrate changed.");

UnusedLevel65MemoryCardDeserializeDispatchProof dispatch = first.MemoryCardDispatch;
Require(
    dispatch.WadEntryIndex == 2 &&
    dispatch.DirectoryRow.LogicalOffset == 0x10 &&
    dispatch.DirectoryRow.Hex == "00B8050000380000" &&
    dispatch.DirectoryRow.Sha256 == "40b57312b49d05375186e2579bfa1e0248464826d59b234403be0f0cbdf810a1" &&
    dispatch.Overlay.LogicalOffset == 0x5B800 && dispatch.Overlay.ImageOffset == 0x7E558 &&
    dispatch.Overlay.ByteLength == 0x3800 &&
    dispatch.Overlay.Sha256 == "8ee66fe644407bf15f93af1615045fbaca17fe3daa9f3b67f38bee2277a94c7c" &&
    dispatch.Header.LogicalOffset == 0x5B800 && dispatch.Header.ByteLength == 0x9C &&
    dispatch.Header.Sha256 == "79e96efbe41a96e329c572ebb7fa6c68d72ac6f030ede8783a0a3be952cdc513" &&
    dispatch.HeaderRuntimeBase == 0x8007AA38 &&
    dispatch.StatePointerTable.LogicalOffset == 0x5B81C &&
    dispatch.StatePointerTable.Sha256 == "76076ce45bcfadceb8278173107cccc0e1cc15d89fd7d25ada68f692e5a8c049" &&
    dispatch.StatePointerTableRuntimeAddress == 0x8007AA54 &&
    dispatch.MainMenuStateRuntimeAddress == 0x80078D88 && dispatch.StateCount == 16,
    "The exact memory-card entry/header substrate changed.");

uint[] expectedTargets =
[
    0x8007B0E8, 0x8007B314, 0x8007B558, 0x8007B64C,
    0x8007B7AC, 0x8007B928, 0x8007B928, 0x8007BA80,
    0x8007BAFC, 0x8007BBD4, 0x8007BCB4, 0x8007BDA0,
    0x8007C0E0, 0x8007C1BC, 0x8007CC48, 0x8007C304
];
Require(dispatch.StateTargets.SequenceEqual(expectedTargets), "The exact 16-state overlay pointer table changed.");
Require(
    dispatch.IndirectDispatcher.LogicalOffset == 0x5BE80 &&
    dispatch.IndirectDispatcher.ImageOffset == 0x7EBD8 &&
    dispatch.IndirectDispatcher.RuntimeAddress == 0x8007B6CC &&
    dispatch.IndirectDispatcher.Sha256 ==
        "21adc7ed8b750d5e61a24d098b5e0e10ffcae4fbed1e6d0917fc12541ff5c421" &&
    dispatch.SelectedLoadState == 14 && dispatch.SelectedLoadHandlerRuntimeAddress == 0x8007CC48 &&
    dispatch.SelectedLoadHandler.LogicalOffset == 0x5D3FC &&
    dispatch.SelectedLoadHandler.ByteLength == 0x704 &&
    dispatch.SelectedLoadHandler.Sha256 ==
        "c23198765beacadbee0c4d88d2f30aab061aa66c06fa3fdb285c8599a5bdf060" &&
    dispatch.SelectedSlotIndexRuntimeAddress == 0x80078D8C &&
    dispatch.SelectedSlotCallWindow.LogicalOffset == 0x5D7C4 &&
    dispatch.SelectedSlotCallWindow.RuntimeAddress == 0x8007D010 &&
    dispatch.SelectedSlotCallWindow.Sha256 ==
        "ac1d42cb6d9de6a930823ba5fdf1017cb81aa769001e04351cb68eaefdc1a07d" &&
    dispatch.DeserializeCall.LogicalOffset == 0x5D7FC &&
    dispatch.DeserializeCall.ImageOffset == 0x808E4 &&
    dispatch.DeserializeCall.CallerRuntimeAddress == 0x8007D048 &&
    dispatch.DeserializeCall.CalleeRuntimeAddress == 0x80059594 &&
    dispatch.DeserializeCall.InstructionHex == "6565010C" && dispatch.DeserializeCall.DirectJal &&
    dispatch.ResidentDeserializeDirectCallerCount == 0 &&
    dispatch.OverlayDeserializeDirectCallerCount == 1 &&
    dispatch.ExactRemoteRawJalBytePatternCount == 1 &&
    dispatch.DispatcherBoundsStateBelow16 && dispatch.DispatcherUsesHeaderPointerTable &&
    dispatch.DispatcherJumpsThroughSelectedPointer && dispatch.SelectedSlotBufferPointerLoadedBeforeCall &&
    dispatch.IndirectDeserializeClosureResolved,
    "The selected-slot indirect deserialize callgraph changed.");

UnusedLevel65SaveSchemaClosureProof save = first.SaveSchema;
Require(
    save.ProgressionProfileId == UnusedLevel65ProgressionOwnershipInspector.ProfileId &&
    save.SaveStructByteLength == 0x600 && save.ChecksummedByteLength == 0x58C &&
    save.ChecksumFieldOffset == 0x58C &&
    save.DeserializeRoutine.LogicalOffset == 0x49D94 &&
    save.DeserializeRoutine.Sha256 == "ff2bcfe124d934838348cb79807d36cdacc3d82f3cf6773d007c4aa0555e2ae6" &&
    save.SerializeRoutine.LogicalOffset == 0x4A064 &&
    save.SerializeRoutine.Sha256 == "b8674b556d8f4154886306731ee50c9812f2cac1d9a224756c110be693d19056" &&
    save.DeserializeLivesClampWindow.LogicalOffset == 0x49E90 &&
    save.DeserializeLivesClampWindow.Sha256 ==
        "096384065349a0ba4a9bf7ed181b87a41e86664fbb41606b32fb950bf2e8ea05" &&
    save.SerializeLivesWindow.LogicalOffset == 0x4A108 &&
    save.SerializeLivesWindow.Sha256 ==
        "4f2da0c0f44e8e0803d5b24f0453b72f586c0a49115d68c4c4996abadb204735" &&
    save.NewGameLivesInitializeWindow.LogicalOffset == 0x2EF0 &&
    save.NewGameLivesInitializeWindow.Sha256 ==
        "00eefa408983e286a787d801292126d65cca60e1abc82a09234bd19e81d4c0e9" &&
    save.LivesSaveOffset == 0x0B && save.LivesRuntimeAddress == 0x8007582C &&
    save.MinimumLivesAfterDeserialize == 4 && save.NewGameInitialLives == 4 &&
    save.RetirementBitsPerLevel == 256 && save.LevelId65EncodesInCurrentLevelByte &&
    save.AllIndex35NonEggRowsRoundTrip && !save.EggIndex35RowPresent &&
    save.OpaqueSecondPerLevelBytePreservedWithoutSemanticClaim &&
    save.DeserializeRestoresAtLeastFourLives && save.SerializePersistsLivesByte &&
    save.NewGamePathInitializesFourLives && save.MemoryCardDeserializeCallgraphClosed &&
    !save.PhysicalMemoryCardRuntimeAccepted,
    "The exact save/lives static closure changed.");
RequireSaveField(save, "current-level-id", 0x000, 1, 1, 0x000, 0x8007596C, true);
RequireSaveField(save, "lives", 0x00B, 1, 1, 0x00B, 0x8007582C, true);
RequireSaveField(save, "visited", 0x040, 1, 36, 0x063, 0x80078E9B, true);
RequireSaveField(save, "opaque-second-per-level-byte", 0x064, 1, 36, 0x087, 0x8007A6CB, true);
RequireSaveField(save, "dragons", 0x088, 1, 36, 0x0AB, 0x80077364, true);
RequireSaveField(save, "treasure", 0x0AC, 2, 36, 0x0F2, 0x800774AC, true);
RequireSaveField(save, "eggs", 0x0F4, 1, 18, null, null, false);
RequireSaveField(save, "object-retirement-bitmap", 0x10C, 0x20, 36, 0x56C, 0x80077D68, true);

UnusedLevel65PassiveMobySaveProfileProof passive = first.PassiveMobyProfile;
Require(
    passive.MobyProfileId == UnusedLevel65MobyDependencyBundleFoundation.ProfileId &&
    passive.MobyContractSha256 == "8ec952c5972d7e2a37e5e89f8ec2be26144635b92b58cba85f745f7d05a76d4f" &&
    passive.FoundationImageSha256 == first.FoundationImageSha256 &&
    passive.DonorId == "artisans:t121:actor-01f5:grass-untextured-no-update-v2" &&
    passive.DonorLevel == "Artisans" && passive.DonorTrueIndex == 121 &&
    passive.DonorIdentity == "Grass" && passive.ActorId == 0x01F5 &&
    passive.ExistingDestinationObjectCount == 107 && passive.WitnessDestinationTrueIndex == 107 &&
    passive.WitnessDestinationObjectCount == 108 && passive.CompleteAppendRowCapacity == 116 &&
    passive.HighestCompleteAppendTrueIndex == 222 && passive.RetirementBitmapBits == 256 &&
    passive.DonorDispatchTraceSha256 ==
        "ffdacdf97f86f1aed4505e0869ee2a9bfbdaad92025704fd33d1d4bfb9ba6d88" &&
    passive.TargetDispatchTraceSha256 ==
        "cb3c5bf3677e27833294c86b83ea12e6a1d5a91fd8cd155aa548f1f59f02809f" &&
    !passive.DonorIsDragon && !passive.DonorIsPortal && !passive.DonorIsThief &&
    !passive.DonorIsCollectible && !passive.DonorIsTotalsLinked && !passive.DonorHasRewardDrop &&
    !passive.DonorHasLegacySpecialData && passive.ActorPackageIsUntextured &&
    !passive.ClassControllerRequired && !passive.SoundRequired && !passive.ParticleRequired &&
    !passive.DynamicSpawnRequired && !passive.DynamicNativeLinkRequired &&
    passive.WitnessFitsRetirementBitmap && passive.WholePinnedAppendRowAllocatorFitsRetirementBitmap &&
    passive.NoEggPassiveSaveProfileStaticallyClosed && passive.SupportsOnlyPinnedPassiveWitness &&
    !passive.SupportsEggs && !passive.SupportsCollectiblesOrRewards &&
    !passive.SupportsAllGameMobys && !passive.PassiveMobyRuntimeAccepted,
    "The narrow no-egg/passive-Moby save profile changed.");

UnusedLevel65DeathRespawnStaticProof death = first.DeathRespawn;
Require(
    death.DeathLifeRoutine.LogicalOffset == 0x1D05C && death.DeathLifeRoutine.ImageOffset == 0x7C5AB74 &&
    death.DeathLifeRoutine.Sha256 == "37dee82fe6e38eaa24e03ab0cf8fdf3226b10e33857a475fd16376f7748f30ad" &&
    death.DeathDirectCallers.Select(edge => edge.LogicalOffset).SequenceEqual(new long[] { 0x33710, 0x3ACD8 }) &&
    death.DeathDirectCallers.All(edge => edge.CalleeRuntimeAddress == 0x8002C85C && edge.DirectJal) &&
    death.CommonDeathPlaneWindow.LogicalOffset == 0x3ACD8 &&
    death.CommonDeathPlaneWindow.Sha256 == "03faf5a908bea39386de4d8ae5786e2588864c6def27ac903e55dfcc083c9474" &&
    death.PlayerZRuntimeAddress == 0x80078A60 && death.DeathPlaneZThreshold == 0x400 &&
    death.MainStateDispatch.LogicalOffset == 0x24084 &&
    death.MainStateDispatch.Sha256 == "f3198ff1e90533ccfdfb2bcfc50250dbdf2f07665d2679f51ed933a447918034" &&
    death.StateFourAndFiveHandlerCall.LogicalOffset == 0x240F0 &&
    death.StateFourAndFiveHandlerCall.CalleeRuntimeAddress == 0x8002EDF0 &&
    death.StateFourAndFiveHandlerPrefix.LogicalOffset == 0x1F5F0 &&
    death.StateFourAndFiveHandlerPrefix.Sha256 ==
        "a2b26dd0e3f148fbbf47af89e391ed3089c162e02998ad12eb1d9d5cc6f963de" &&
    death.StateFourRespawnDelayTicks == 16 &&
    death.StateFourResetCall.LogicalOffset == 0x1F67C &&
    death.StateFourResetCall.CalleeRuntimeAddress == 0x8002C8A4 &&
    death.StateFourReloadCall.LogicalOffset == 0x1F684 &&
    death.StateFourReloadCall.CalleeRuntimeAddress == 0x800144C8 &&
    death.RespawnResetRoutine.LogicalOffset == 0x1D0A4 &&
    death.RespawnResetRoutine.Sha256 == "940ee2a2e463342503bb1990c3cb7159d655939d8c5f7041bcb7f7b6c39b5a92" &&
    death.RespawnResetDirectCallers.Select(edge => edge.LogicalOffset)
        .SequenceEqual(new long[] { 0x1F67C, 0x1FB9C }) &&
    death.SameLevelReloadRoutine.LogicalOffset == 0x4CC8 &&
    death.SameLevelReloadRoutine.Sha256 == "fae8117d233b5838e56ab55c326c6d86e6a078a9642ef252bb395e6edaec8990" &&
    death.CommonLevelLoaderCall.LogicalOffset == 0x4D20 &&
    death.CommonLevelLoaderCall.CalleeRuntimeAddress == 0x8001364C &&
    death.ContinuousLevelIndexRuntimeAddress == 0x80075964 &&
    death.PositiveLivesDeathState == 4 && death.ZeroLivesDeathState == 5 &&
    death.PositiveLivesAreDecremented && death.ZeroLivesSelectsGameOver &&
    death.DeathClearsStateTimers && !death.DeathWritesPerLevelProgress &&
    !death.RespawnResetWritesPerLevelProgress && death.StateFourReloadUsesContinuousLevelIndex &&
    death.StaticDeathRespawnCallgraphClosed && !death.RemoteSpawnLandingRuntimeAccepted &&
    !death.RemoteDeathRespawnRuntimeAccepted,
    "The exact death-plane/state-4/state-5/reload callgraph changed.");

UnusedLevel65RemoteBlankRuntimeFailureObservation failure = first.RuntimeFailure;
Require(
    failure.EvidenceArtifactRelativePath ==
        "docs/runtime-evidence/unused-level-65-remote-blank-isolation-no-landing-game-over-2026-08-10.json" &&
    failure.EvidenceArtifactSha256 ==
        "08de9812ccecf5512e7d1453ad009a6303cd90da9e0ab82e9e132d9a15fcf9c3" &&
    failure.Date == "2026-08-10" && failure.ImageSha256 == first.RemoteBlankImageSha256 &&
    failure.TerminalResult.Contains("GAME OVER", StringComparison.Ordinal) &&
    failure.ApproximateSecondsToGameOver == 8 && failure.CardsDisabled && failure.CheatsDisabled &&
    failure.SaveStateDisabled && !failure.SpyroLanded && !failure.SpawnRuntimePassed &&
    !failure.DeathRespawnRuntimePassed && !failure.InitialLivesRuntimeValueCaptured &&
    !failure.DeathInvocationCountCaptured && !failure.RuntimeRootCauseDiscriminated &&
    failure.ObservationHasMachineReadableEvidenceArtifact &&
    failure.FoundationControlUsedSameSelectorProcedure && failure.FoundationControlLivesDisplayed == 4 &&
    failure.FoundationControlPlayerStayedAlive && failure.RepeatedFailedRespawnsCouldExhaustLives &&
    failure.StaticExplanationBoundary.Contains("no live-RAM capture", StringComparison.Ordinal),
    "The immutable failure-only runtime observation changed or became an acceptance claim.");

Require(
    first.RemainingRuntimeBlockers.Count == 5 &&
    first.RemainingRuntimeBlockers.Any(value => value.Contains("failed to land", StringComparison.Ordinal)) &&
    first.RemainingRuntimeBlockers.Any(value => value.Contains("RAM", StringComparison.Ordinal)) &&
    first.RemainingRuntimeBlockers.Any(value => value.Contains("memory-card", StringComparison.Ordinal)) &&
    first.RemainingRuntimeBlockers.Any(value => value.Contains("arbitrary game Mobys", StringComparison.Ordinal)) &&
    first.StaticReadbackOnly && !first.WritesBin && !first.WritesCue && !first.WriterAuthorized &&
    !first.AppIntegrated && !first.NormalCreateBinEnabled && !first.ReleasePublicationAuthorized &&
    !first.RuntimePromotionAuthorized,
    "The failure-only/no-publication boundary changed.");

bool wrongLockedHashRejected = false;
try
{
    _ = UnusedLevel65SaveDeathRespawnOwnershipClosure.Inspect(root, remote, remote, foundation);
}
catch (InvalidDataException ex) when (ex.Message.Contains("SHA-256", StringComparison.Ordinal))
{
    wrongLockedHashRejected = true;
}
Require(wrongLockedHashRejected, "A wrong locked display-name BIN was accepted.");

bool missingRemoteRejected = false;
try
{
    _ = UnusedLevel65SaveDeathRespawnOwnershipClosure.Inspect(
        root,
        locked,
        Path.Combine(root, "_local", "definitely-missing-id65-save-death-remote.bin"),
        foundation);
}
catch (FileNotFoundException)
{
    missingRemoteRejected = true;
}
Require(missingRemoteRejected, "A missing remote-blank BIN was accepted.");

Dictionary<string, FileStamp> after = observedFiles.ToDictionary(path => path, SnapshotFile);
Dictionary<string, DirectoryStamp> directoriesAfter = observedDirectories.ToDictionary(
    path => path,
    SnapshotDirectory);
Require(before.OrderBy(pair => pair.Key).SequenceEqual(after.OrderBy(pair => pair.Key)),
    "The read-only smoke changed an input/evidence file.");
Require(directoriesBefore.OrderBy(pair => pair.Key).SequenceEqual(directoriesAfter.OrderBy(pair => pair.Key)),
    "The read-only smoke changed an input/evidence directory.");

Console.WriteLine("PASS: ID65 save/death/respawn ownership is statically closed for one strict no-egg/passive witness.");
Console.WriteLine($"Contract JSON SHA-256: {contractHash}");
Console.WriteLine("Memory card: entry-2 state 14 indirectly dispatches exactly one direct JAL to deserialize 0x80059594.");
Console.WriteLine("Lives: save+0x0B round-trips; deserialize and new game establish >=4, while death state 5 requires zero.");
Console.WriteLine("Runtime rejection: the exact remote BIN never landed and exhausted into GAME OVER; mechanism remains inferred.");
Console.WriteLine("Runtime/App/Create BIN/release/promotion: false; artifacts written: 0 BIN, 0 CUE, 0 evidence files.");

static void RequireSaveField(
    UnusedLevel65SaveSchemaClosureProof save,
    string name,
    int saveOffset,
    int byteLength,
    int rows,
    int? index35Offset,
    uint? index35Address,
    bool present)
{
    UnusedLevel65SaveFieldClosure field = save.Fields.Single(value => value.Name == name);
    Require(
        field.SaveOffset == saveOffset && field.ByteLength == byteLength && field.RowCount == rows &&
        field.Index35SaveOffset == index35Offset && field.Index35RuntimeAddress == index35Address &&
        field.Index35StoragePresent == present,
        $"Save-field policy {name} changed.");
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

static FileStamp SnapshotFile(string path)
{
    FileInfo info = new(path);
    return new(
        info.Length,
        info.LastWriteTimeUtc.Ticks,
        ReadMode(path),
        HashFile(path));
}

static DirectoryStamp SnapshotDirectory(string path)
{
    string[] entries = Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories)
        .Select(entry =>
        {
            string relative = Path.GetRelativePath(path, entry);
            if (File.Exists(entry))
            {
                FileInfo file = new(entry);
                return $"F|{relative}|{file.Length}|{file.LastWriteTimeUtc.Ticks}|{ReadMode(entry)}";
            }
            DirectoryInfo directory = new(entry);
            return $"D|{relative}|{directory.LastWriteTimeUtc.Ticks}|{ReadMode(entry)}";
        })
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();
    return new(string.Join('\n', entries));
}

static string HashFile(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}

static UnixFileMode ReadMode(string path) =>
    OperatingSystem.IsWindows() ? default : File.GetUnixFileMode(path);

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

internal sealed record FileStamp(long ByteLength, long LastWriteTicks, UnixFileMode Mode, string Sha256);

internal sealed record DirectoryStamp(string Entries);
