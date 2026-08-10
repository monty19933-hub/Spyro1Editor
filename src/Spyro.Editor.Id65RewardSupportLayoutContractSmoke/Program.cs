using System.Collections.ObjectModel;
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
Require(File.Exists(lockedImage), "The exact locked ID65 display-name BIN is missing.");

FileState lockedBefore = SnapshotFile(lockedImage);
DirectoryState directoryBefore = SnapshotDirectory(lockedDirectory);
Id65RewardCensusSourceSnapshot source = Id65RewardCensusContract.ReadLockedSource(lockedImage);
Id65RewardCensus census = Id65RewardCensusContract.Inspect(source);
Id65AuthoringSupportManifest emptySupport =
    Id65AuthoringSupportReplacementCompiler.CreateEmptyManifest();
Id65AuthoringSupportManifest coreSupport =
    Id65AuthoringSupportReplacementCompiler.AddCoreSupport(emptySupport);
Id65AuthoringSupportManifest fullSupport =
    Id65AuthoringSupportReplacementCompiler.AddEnemyBay(coreSupport);

Id65RewardSupportLayout first =
    Id65RewardSupportLayoutContract.Build(census, coreSupport, fullSupport);
Id65RewardSupportLayout repeat =
    Id65RewardSupportLayoutContract.Build(census, coreSupport, fullSupport);
Id65RewardSupportLayout canonicalReorder =
    Id65RewardSupportLayoutContract.Build(
        CanonicallyReordered(census),
        coreSupport,
        fullSupport);

Require(
    Id65RewardSupportLayoutContract.RequiredSmokeAssemblyName ==
        "Spyro.Editor.Id65RewardSupportLayoutContractSmoke" &&
    first.ProfileId == Id65RewardSupportLayoutContract.ProfileId &&
    first.Source.CensusProfileId == Id65RewardCensusContract.ProfileId &&
    first.Source.DeterministicCensusSha256 ==
        "b413e471d9fbfc13913bfc5948fefdd7316884a80be532bb635708f50063a491" &&
    first.Source.RewardRowsSha256 ==
        "a0078220e572b5ed440a447b5e2b20c62777541e8076cab3db66458c3d2d771f" &&
    first.Source.RewardRowCount == 81 && first.Source.RewardTotal == 200 &&
    first.Source.StationaryOwnerRowCount == 71 &&
    first.Source.StationaryOwnerRewardTotal == 171 &&
    first.Source.EnemyOwnerRowCount == 10 && first.Source.EnemyOwnerRewardTotal == 29 &&
    first.Source.DragonBundleCount == 4 && first.Source.RequiredEggTarget == 0 &&
    first.Source.ZeroEggReplacementRequired,
    "The direct frozen reward-census binding changed.");
Require(
    first.DeterministicLayoutSha256 == repeat.DeterministicLayoutSha256 &&
    first.DeterministicLayoutSha256 == canonicalReorder.DeterministicLayoutSha256,
    "The layout is not deterministic under canonical source reordering.");
Require(
    !ReferenceEquals(first, repeat) && !ReferenceEquals(first.Source, census) &&
    !ReferenceEquals(first.StationaryPlacements, census.RewardRows) &&
    !ReferenceEquals(first.DragonPrecincts, census.DragonBundles),
    "The layout retained a mutable source aggregate rather than canonical deep copies.");

Require(first.Profiles.Count == 2, "The exact two support profiles changed.");
Id65RewardSupportProfile core = first.Profiles[0];
Id65RewardSupportProfile full = first.Profiles[1];
Require(
    core.Id == "reward-support-core4-v1" &&
    core.TerrainSupportProfileId == "support-core-4-v1" &&
    core.TerrainSupportCanonicalSha256 == coreSupport.CanonicalSha256 &&
    core.Kind == Id65RewardSupportLayoutProfileKind.Core4 &&
    core.TerrainSectorCount == 4 && core.StationaryOwnerCapacity == 71 &&
    core.StationaryRewardCapacity == 171 && core.MobileEnemyReservationCapacity == 0 &&
    core.MobileEnemyRewardCapacity == 0 && core.CombinedOwnerCapacity == 71 &&
    core.CombinedRewardCapacity == 171 && core.CapacityOnly &&
    !core.Total200RuntimeAccepted && !core.RuntimeSafe && !core.PromotionAuthorized,
    "Core4 is not the strict 71-row/171 non-promotable capacity profile.");
Require(
    full.Id == "reward-support-full8-v1" &&
    full.TerrainSupportProfileId == "support-full-8-v1" &&
    full.TerrainSupportCanonicalSha256 == fullSupport.CanonicalSha256 &&
    full.Kind == Id65RewardSupportLayoutProfileKind.Full8 &&
    full.TerrainSectorCount == 8 && full.StationaryOwnerCapacity == 71 &&
    full.StationaryRewardCapacity == 171 && full.MobileEnemyReservationCapacity == 10 &&
    full.MobileEnemyRewardCapacity == 29 && full.CombinedOwnerCapacity == 81 &&
    full.CombinedRewardCapacity == 200 && full.CapacityOnly &&
    !full.Total200RuntimeAccepted && !full.RuntimeSafe && !full.PromotionAuthorized,
    "Full8 is not the strict reservation-only 81-row/200 capacity profile.");
RequireEnvelope(first.CoreEnvelope, "core4-envelope", 4_352, 4_352, 19_712, 19_712, 15_360, 15_360);
RequireEnvelope(first.FullEnvelope, "full8-envelope", 4_352, 4_352, 35_072, 19_712, 30_720, 15_360);
RequireEnvelope(first.EnemyBayEnvelope, "full8-enemy-bay-envelope", 19_712, 4_352, 35_072, 19_712, 15_360, 15_360);
Require(
    coreSupport.AuthoredTranslationWorld == 752 &&
    coreSupport.AuthoredTranslationRaw == 12_032 &&
    fullSupport.AuthoredTranslationWorld == 752 &&
    fullSupport.AuthoredTranslationRaw == 12_032 &&
    coreSupport.Sectors.Count == 4 && fullSupport.Sectors.Count == 8 &&
    coreSupport.Sectors.Min(item => item.BoundsMinimum.X) == 272 &&
    coreSupport.Sectors.Max(item => item.BoundsMaximum.X) == 1_232 &&
    fullSupport.Sectors.Max(item => item.BoundsMaximum.X) == 2_192,
    "The actual immutable support manifests lost their +752/envelope geometry.");

Id65RewardSupportLandingApron landing = first.LandingApron;
Require(
    landing.LandingRecordId == "spawn.remote-pad" &&
    landing.PlayerAnchorId == "moby.player-anchor.t92" &&
    landing.PlayerAnchorTrueIndex == 92 &&
    landing.Landing == new Id65RewardSupportRawPoint(6_153, 6_154, 8_550) &&
    landing.PlayerAnchor == new Id65RewardSupportRawPoint(6_153, 6_154, 8_704) &&
    landing.ApronMinimum == new Id65RewardSupportRawPoint(5_641, 5_642, 8_192) &&
    landing.ApronMaximum == new Id65RewardSupportRawPoint(6_665, 6_666, 8_192) &&
    landing.ApronHalfExtentWorld == 32 && landing.ApronHalfExtentRaw == 512 &&
    landing.MinimumLandingEdgeMarginRaw == 1_801 &&
    landing.MinimumLandingEdgeMarginRaw > 112 * 16 &&
    landing.LandingAndPlayerXyUnchanged && landing.EntireApronInsideCore4 &&
    landing.PlayerAnchor.Z - landing.Landing.Z == 154,
    "The unchanged v2 landing record, separate T92 anchor, or exact apron changed.");

Id65RewardSupportRawPoint worldRoundTrip = Id65RewardSupportCoordinates.WorldToRaw(
    new Id65RewardSupportWorldPoint(832.625m, 1_196.625m, 512m));
Require(
    worldRoundTrip == new Id65RewardSupportRawPoint(13_322, 19_146, 8_192) &&
    worldRoundTrip.ToWorld() == new Id65RewardSupportWorldPoint(832.625m, 1_196.625m, 512m),
    "The exact 1/16 world/raw inverse changed.");
Id65RewardSupportRawPoint local = Id65RewardSupportCoordinates.SceneToLocal(first.CoreEnvelope.Minimum);
Require(
    local == new Id65RewardSupportRawPoint(-7_680, -7_680, 8_192) &&
    Id65RewardSupportCoordinates.LocalToScene(local) == first.CoreEnvelope.Minimum,
    "The exact +752 scene/platform-local inverse changed.");
ExpectArgumentReject(
    () => Id65RewardSupportCoordinates.WorldToRaw(new(0.01m, 0m, 0m)),
    "non-1/16 coordinate");
ExpectArgumentReject(
    () => Id65RewardSupportCoordinates.WorldToRaw(new(decimal.MaxValue, 0m, 0m)),
    "overflow coordinate");

Require(first.StationarySlots.Count == 72 && first.StationaryPlacements.Count == 71,
    "The 72-slot/71-placement lattice changed.");
for (int index = 0; index < first.StationarySlots.Count; index++)
{
    Id65RewardSupportStationarySlot slot = first.StationarySlots[index];
    int row = index / 12;
    int column = index % 12;
    Require(
        slot.SlotIndex == index && slot.Row == row && slot.Column == column &&
        slot.Point == new Id65RewardSupportRawPoint(
            5_584 + (1_152 * column),
            11_344 - (1_152 * row),
            8_192) && slot.Spare == (index == 71),
        $"Stationary lattice slot {index} changed.");
}
int[] expectedStationaryIndexes = census.RewardRows
    .Where(row => row.OwnerSemantics is
        Id65RewardOwnerSemantics.StationaryLooseGem or
        Id65RewardOwnerSemantics.StationaryChestCarrier)
    .Select(row => row.TrueIndex)
    .Order()
    .ToArray();
Require(
    first.StationaryPlacements.Select(item => item.Owner.TrueIndex)
        .SequenceEqual(expectedStationaryIndexes) &&
    first.StationaryPlacements.Sum(item => item.Owner.RewardValue) == 171 &&
    first.StationaryPlacements.All(item =>
        item.DesiredStateOnly && !item.RuntimeAccepted && !item.Slot.Spare),
    "The deterministic stationary row-to-slot mapping or strict 171 total changed.");

(string Id, int Pedestal, int Dragon, int Container, Id65RewardSupportRawPoint PedestalPoint,
    Id65RewardSupportRawPoint ActorPoint, Id65RewardSupportRawPoint ControlPoint,
    Id65RewardSupportRawPoint Minimum, Id65RewardSupportRawPoint Maximum,
    Id65RewardSupportRawPoint ActorMinimum, Id65RewardSupportRawPoint ActorMaximum,
    int OuterMargin, int SeamMargin)[] dragonExpected =
[
    ("dragon-a", 3, 12, 102, new(9_216, 16_002, 8_192), new(9_216, 16_032, 8_315),
        new(9_216, 16_032, 9_216), new(8_010, 13_005, 8_192), new(10_411, 16_032, 9_216),
        new(-1_206, -3_027, -123), new(1_195, 0, 901), 3_658, 973),
    ("dragon-b", 4, 13, 103, new(13_322, 19_146, 8_192), new(13_312, 19_136, 8_315),
        new(13_312, 19_136, 9_216), new(13_312, 16_820, 8_192), new(16_112, 19_146, 9_216),
        new(0, -2_316, -123), new(2_800, 10, 901), 566, 1_280),
    ("dragon-c", 48, 49, 104, new(10_752, 17_664, 8_192), new(10_752, 17_664, 8_315),
        new(10_752, 17_664, 9_216), new(7_626, 16_803, 8_192), new(10_752, 19_148, 9_216),
        new(-3_126, -861, -123), new(0, 1_484, 901), 564, 1_280),
    ("dragon-d", 100, 101, 105, new(15_360, 16_032, 8_192), new(15_360, 16_032, 8_315),
        new(15_360, 16_032, 9_216), new(13_859, 12_551, 8_192), new(15_360, 16_032, 9_216),
        new(-1_501, -3_481, -123), new(0, 0, 901), 3_680, 519)
];
Require(first.DragonPrecincts.Count == dragonExpected.Length,
    "The exact four dragon precinct count changed.");
for (int index = 0; index < dragonExpected.Length; index++)
{
    var expected = dragonExpected[index];
    Id65RewardSupportDragonPrecinct actual = first.DragonPrecincts[index];
    Require(
        actual.Id == expected.Id &&
        actual.Dragon.PedestalTrueIndex == expected.Pedestal &&
        actual.Dragon.DragonTrueIndex == expected.Dragon &&
        actual.Dragon.ContainerTrueIndex == expected.Container &&
        actual.Pedestal == expected.PedestalPoint && actual.Actor == expected.ActorPoint &&
        actual.ControlDestination == expected.ControlPoint &&
        actual.PrecinctEnvelope.Minimum == expected.Minimum &&
        actual.PrecinctEnvelope.Maximum == expected.Maximum &&
        Id65RewardSupportCoordinates.LocalToScene(actual.PlatformLocalEnvelope.Minimum) == expected.Minimum &&
        Id65RewardSupportCoordinates.LocalToScene(actual.PlatformLocalEnvelope.Maximum) == expected.Maximum &&
        actual.ActorRelativeEnvelope.Minimum == expected.ActorMinimum &&
        actual.ActorRelativeEnvelope.Maximum == expected.ActorMaximum &&
        actual.OuterEdgeMinimumMarginRaw == expected.OuterMargin &&
        actual.SectorSeamMinimumMarginRaw == expected.SeamMargin &&
        actual.DesiredSupportPinned && !actual.CameraRuntimeVerified &&
        !actual.RouteRuntimeVerified && !actual.DestinationRuntimeVerified,
        $"Dragon precinct {expected.Id} changed.");
}
Id65RewardSupportDragonSeparation[] separationExpected =
[
    new("dragon-a", "dragon-b", 2_901, 788, 9_036_745),
    new("dragon-a", "dragon-c", 0, 771, 594_441),
    new("dragon-a", "dragon-d", 3_448, 0, 11_888_704),
    new("dragon-b", "dragon-c", 2_560, 0, 6_553_600),
    new("dragon-b", "dragon-d", 0, 788, 620_944),
    new("dragon-c", "dragon-d", 3_107, 771, 10_247_890)
];
Require(first.DragonSeparations.SequenceEqual(separationExpected),
    "The exact six pairwise dragon-envelope separations changed.");
Require(
    first.StaticSpacing == new Id65RewardSupportStaticSpacing(
        60, 648_661, 50.34m, 8, "dragon-d", 1_207, 75.4375m,
        1_232, 464, 564, 519, true, true, true),
    "The exact landing/lattice/dragon edge-and-seam spacing proof changed.");

(string Id, int TrueIndex, Id65RewardSupportReservationKind Kind, int X, int Y)[] enemyExpected =
[
    ("bull-t0", 0, Id65RewardSupportReservationKind.Bull, 21_632, 6_528),
    ("bull-t1", 1, Id65RewardSupportReservationKind.Bull, 25_472, 6_528),
    ("bull-t2", 2, Id65RewardSupportReservationKind.Bull, 29_312, 6_528),
    ("bull-t5", 5, Id65RewardSupportReservationKind.Bull, 33_152, 6_528),
    ("bull-t7", 7, Id65RewardSupportReservationKind.Bull, 21_632, 10_368),
    ("bull-t8", 8, Id65RewardSupportReservationKind.Bull, 25_472, 10_368),
    ("bull-t9", 9, Id65RewardSupportReservationKind.Bull, 29_312, 10_368),
    ("bull-t11", 11, Id65RewardSupportReservationKind.Bull, 33_152, 10_368),
    ("torro-t6", 6, Id65RewardSupportReservationKind.Torro, 21_632, 15_872),
    ("torro-t10", 10, Id65RewardSupportReservationKind.Torro, 25_472, 15_872)
];
Require(first.EnemyReservations.Count == enemyExpected.Length,
    "The exact ten mobile-enemy reservation count changed.");
for (int index = 0; index < enemyExpected.Length; index++)
{
    var expected = enemyExpected[index];
    Id65RewardSupportEnemyReservation actual = first.EnemyReservations[index];
    Id65RewardCensusRow sourceOwner = census.RewardRows.Single(row => row.TrueIndex == expected.TrueIndex);
    Require(
        actual.Id == expected.Id && actual.Kind == expected.Kind &&
        actual.Owner.TrueIndex == sourceOwner.TrueIndex &&
        actual.Owner.ActorClass == sourceOwner.ActorClass &&
        actual.Owner.Encoding == sourceOwner.Encoding &&
        actual.Owner.OwnerSemantics == sourceOwner.OwnerSemantics &&
        actual.Owner.RewardValue == sourceOwner.RewardValue &&
        actual.Owner.RowSha256 == sourceOwner.RowSha256 &&
        actual.Owner.PropertiesSceneOffset == sourceOwner.PropertiesSceneOffset &&
        actual.Owner.PropertiesByteLength == sourceOwner.PropertiesByteLength &&
        actual.Owner.PropertiesSha256 == sourceOwner.PropertiesSha256 &&
        actual.Owner.RetirementByteIndex == sourceOwner.RetirementByteIndex &&
        actual.Owner.RetirementBitMask == sourceOwner.RetirementBitMask &&
        actual.Owner.TransientRewardChildRuntimeProofVerified ==
            sourceOwner.TransientRewardChildRuntimeProofVerified &&
        actual.Point == new Id65RewardSupportRawPoint(expected.X, expected.Y, 8_192) &&
        actual.ReservationOnly && !actual.RoutePinned &&
        !actual.ActivationBoundsPinned && !actual.RuntimeAccepted,
        $"Enemy reservation {expected.Id} or its complete owner identity changed.");
}
Require(first.EnemyReservations.Sum(item => item.Owner.RewardValue) == 29,
    "The reservation-only mobile-enemy capacity is not exactly 29.");

Require(
    first.ZeroEgg.RequiredEggTarget == 0 && first.ZeroEgg.EggThiefTrueIndex == 88 &&
    first.ZeroEgg.EggThiefClass == 0x0021 && first.ZeroEgg.CarriedEggClass == 0x22 &&
    first.ZeroEgg.ReplacementRequired && !first.ZeroEgg.WriterAuthorized,
    "The exact zero-egg/T88 replacement boundary changed.");
Require(
    !first.RuntimeGates.Core4StationaryCollectionVerified &&
    !first.RuntimeGates.DragonCameraPlacementVerified &&
    !first.RuntimeGates.DragonRoutePlacementVerified &&
    !first.RuntimeGates.MobileEnemyRoutesVerified &&
    !first.RuntimeGates.MobileEnemyActivationBoundsVerified &&
    !first.RuntimeGates.ChestTransientChildDuplicateAndBitVerified &&
    !first.RuntimeGates.CollectionRuntimeVerified && !first.RuntimeGates.DeathRuntimeVerified &&
    !first.RuntimeGates.ReentryRuntimeVerified && !first.RuntimeGates.MemoryCardMatrixVerified &&
    !first.RuntimeGates.Total200RuntimeAccepted && !first.RuntimeGates.RuntimeSafetyAuthorized &&
    first.RejectedClaims.Count == 10 && first.RejectedClaims.All(item => item.Rejected),
    "A route/activation/chest-child/runtime/200-total gate opened.");
Require(
    first.LockedSourcePreserved && first.StaticReadOnly && first.DesiredStateOnly &&
    !first.ProducesRow80Patches && !first.WritesFilesystem && !first.WritesBin &&
    !first.WritesCue && !first.WriterAuthorized && !first.PublisherAuthorized &&
    !first.AppIntegrated && !first.NormalCreateBinEnabled && !first.RuntimeVerified &&
    !first.PromotionAuthorized && !first.ReleaseAuthorized,
    "The desired-state contract opened a row-80/filesystem/publisher/runtime/release path.");

ExpectInvalid(() => Id65RewardSupportLayoutContract.Build(
    census with { ProfileId = "wrong-profile" }, coreSupport, fullSupport), "profile tamper");
ExpectInvalid(() => Id65RewardSupportLayoutContract.Build(
    census with { DeterministicCensusSha256 = new string('0', 64) }, coreSupport, fullSupport),
    "census pin tamper");
Id65RewardCensusRow chest = census.RewardRows.First(row =>
    row.OwnerSemantics == Id65RewardOwnerSemantics.StationaryChestCarrier);
ExpectInvalid(() => Id65RewardSupportLayoutContract.Build(
    census with
    {
        RewardRows = census.RewardRows
            .Select(row => row.TrueIndex == chest.TrueIndex
                ? row with { TransientRewardChildRuntimeProofVerified = true }
                : row)
            .ToArray()
    }, coreSupport, fullSupport), "chest-child proof forgery");
ExpectInvalid(() => Id65RewardSupportLayoutContract.Build(
    census with { RuntimeVerified = true }, coreSupport, fullSupport), "runtime gate forgery");
ExpectInvalid(() => Id65RewardSupportLayoutContract.Build(
    census with { WritesBin = true }, coreSupport, fullSupport), "BIN writer forgery");
ExpectInvalid(() => Id65RewardSupportLayoutContract.Build(
    census with { ZeroEgg = census.ZeroEgg with { RequiredEggTarget = 1 } }, coreSupport, fullSupport),
    "zero-egg forgery");
ExpectInvalid(() => Id65RewardSupportLayoutContract.Build(
    census with
    {
        RewardRetirementOwnerCandidates = census.RewardRetirementOwnerCandidates
            .Select((item, index) => index == 0 ? item with { RetirementAuthorized = true } : item)
            .ToArray()
    }, coreSupport, fullSupport), "retirement authorization forgery");
ExpectInvalid(() => Id65RewardSupportLayoutContract.Build(census, fullSupport, coreSupport),
    "support manifest kind swap");
Id65AuthoringSupportManifest forgedCore = ForgeManifestCanonicalSha(coreSupport, new string('0', 64));
ExpectInvalid(() => Id65RewardSupportLayoutContract.Build(census, forgedCore, fullSupport),
    "support manifest canonical identity forgery");

bool pinFrozen = Id65RewardSupportLayoutContract.ExpectedDeterministicLayoutSha256 != "UNFROZEN";
Console.WriteLine($"derivedLayoutSha256={first.DeterministicLayoutSha256}");
Console.WriteLine($"coreSupportManifestSha256={coreSupport.CanonicalSha256}");
Console.WriteLine($"fullSupportManifestSha256={fullSupport.CanonicalSha256}");
if (pinFrozen)
{
    Require(
        first.DeterministicLayoutSha256 ==
            Id65RewardSupportLayoutContract.ExpectedDeterministicLayoutSha256,
        "The frozen deterministic layout pin changed.");
    Id65RewardSupportLayoutContract.ValidateReadback(first);

    Id65RewardCensusRow enemyZero = census.RewardRows.Single(row => row.TrueIndex == 0);
    foreach ((string Label, Func<Id65RewardCensusRow, Id65RewardCensusRow> Tamper) in new[]
    {
        ("enemy encoding", (Func<Id65RewardCensusRow, Id65RewardCensusRow>)(row => row with { Encoding = Id65RewardEncoding.LooseGem })),
        ("enemy row hash", row => row with { RowSha256 = new string('0', 64) }),
        ("enemy properties hash", row => row with { PropertiesSha256 = new string('0', 64) }),
        ("enemy retirement bit", row => row with { RetirementBitMask = 0x80 })
    })
    {
        ExpectInvalid(() => Id65RewardSupportLayoutContract.Build(
            census with
            {
                RewardRows = census.RewardRows
                    .Select(row => row.TrueIndex == enemyZero.TrueIndex ? Tamper(row) : row)
                    .ToArray()
            }, coreSupport, fullSupport), $"{Label} census forgery");
    }

    Id65RewardSupportEnemyReservation[] forgedEnemies = first.EnemyReservations.ToArray();
    forgedEnemies[0] = forgedEnemies[0] with
    {
        Owner = forgedEnemies[0].Owner with { RowSha256 = new string('f', 64) }
    };
    Id65RewardSupportLayout forgedReadback = new(
        first.ProfileId,
        first.Source,
        first.Profiles,
        first.CoreEnvelope,
        first.FullEnvelope,
        first.EnemyBayEnvelope,
        first.LandingApron,
        first.StationarySlots,
        first.StationaryPlacements,
        first.DragonPrecincts,
        first.DragonSeparations,
        forgedEnemies,
        first.ZeroEgg,
        first.RuntimeGates,
        first.StaticSpacing,
        first.RejectedClaims,
        first.DeterministicLayoutSha256);
    ExpectInvalid(() => Id65RewardSupportLayoutContract.ValidateReadback(forgedReadback),
        "complete enemy owner readback forgery");
}

Require(SnapshotFile(lockedImage) == lockedBefore,
    "The pure layout contract changed the locked source BIN.");
Require(SnapshotDirectory(lockedDirectory) == directoryBefore,
    "The pure layout contract changed the locked source directory.");

if (!pinFrozen)
{
    Console.WriteLine("DERIVATION PASS: layout invariants passed; freeze the derived layout SHA and rerun.");
    return;
}

Console.WriteLine("PASS: exact ID65 reward-support layout is deterministic, read-only, and fail-closed.");
Console.WriteLine("Core4: four sectors; 71 stationary owners / 171 only; four exact dragon footprints.");
Console.WriteLine("Full8: eight sectors; ten Bull/Torro reservations / 29 capacity; 200 is not runtime accepted.");
Console.WriteLine("Spacing: 72 lattice slots outside landing apron and dragon footprints; exact edge/seam gaps pinned.");
Console.WriteLine("Artifacts written: 0 row-80 patches, 0 BIN, 0 CUE; App/Create BIN/runtime/release all false.");

static Id65RewardCensus CanonicallyReordered(Id65RewardCensus census)
{
    Id65RewardOwnerAggregate aggregate = census.OwnerAggregate with
    {
        BullTrueIndexes = census.OwnerAggregate.BullTrueIndexes.Reverse().ToArray(),
        TorroTrueIndexes = census.OwnerAggregate.TorroTrueIndexes.Reverse().ToArray(),
        StationaryChestCarrierClasses = census.OwnerAggregate.StationaryChestCarrierClasses.Reverse().ToArray()
    };
    IReadOnlyDictionary<int, int> histogram = new ReadOnlyDictionary<int, int>(
        census.ValueHistogram.Reverse().ToDictionary(item => item.Key, item => item.Value));
    return census with
    {
        RewardRows = census.RewardRows.Reverse().ToArray(),
        ClassValueIndexLedger = census.ClassValueIndexLedger.Reverse().ToArray(),
        ValueHistogram = histogram,
        OwnerAggregate = aggregate,
        DragonBundles = census.DragonBundles.Reverse().ToArray(),
        RewardRetirementOwnerCandidates = census.RewardRetirementOwnerCandidates.Reverse().ToArray(),
        NonProgressionRetirementCandidates = census.NonProgressionRetirementCandidates.Reverse().ToArray(),
        UnresolvedAuthoringBlockers = census.UnresolvedAuthoringBlockers.Reverse().ToArray()
    };
}

static Id65AuthoringSupportManifest ForgeManifestCanonicalSha(
    Id65AuthoringSupportManifest source,
    string canonicalSha256) =>
    new(
        source.ProfileId,
        source.Kind,
        source.LockedSubstrateSectorCount,
        source.RetiresLockedSector217,
        source.ClearsInheritedCollisionLookups,
        source.CollisionTriangleCount,
        source.CollisionAssignment,
        source.AuthoredTranslationWorld,
        source.TextureWitnessSha256,
        source.TextureIntentId,
        source.Sectors,
        source.Seams,
        source.CollisionCells,
        source.OcclusionGroup0Sectors,
        source.CollisionTreeSha256,
        source.CollisionBlocksSha256,
        source.CollisionComponentSha256,
        source.CollisionBlocksUsedBytes,
        source.OcclusionSha256,
        source.OcclusionOpaqueTailSha256,
        source.Layout,
        source.CanonicalJson,
        canonicalSha256);

static void RequireEnvelope(
    Id65RewardSupportEnvelope actual,
    string id,
    int minimumX,
    int minimumY,
    int maximumX,
    int maximumY,
    int widthRaw,
    int heightRaw)
{
    Require(
        actual.Id == id && actual.Minimum == new Id65RewardSupportRawPoint(minimumX, minimumY, 8_192) &&
        actual.Maximum == new Id65RewardSupportRawPoint(maximumX, maximumY, 8_192) &&
        actual.WidthRaw == widthRaw && actual.HeightRaw == heightRaw,
        $"Envelope {id} changed.");
}

static void ExpectInvalid(Action action, string label)
{
    bool rejected = false;
    try
    {
        action();
    }
    catch (InvalidDataException)
    {
        rejected = true;
    }
    Require(rejected, $"The {label} was accepted.");
}

static void ExpectArgumentReject(Action action, string label)
{
    bool rejected = false;
    try
    {
        action();
    }
    catch (ArgumentOutOfRangeException)
    {
        rejected = true;
    }
    Require(rejected, $"The {label} was accepted.");
}

static FileState SnapshotFile(string path) =>
    new(
        new FileInfo(path).Length,
        File.GetLastWriteTimeUtc(path),
        Hash(File.ReadAllBytes(path)));

static DirectoryState SnapshotDirectory(string path) =>
    new(
        string.Join('\n',
            Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(file =>
                    $"F|{Path.GetRelativePath(path, file)}|{new FileInfo(file).Length}|{File.GetLastWriteTimeUtc(file).Ticks}")
                .Concat(Directory.GetDirectories(path, "*", SearchOption.AllDirectories)
                    .Select(directory => $"D|{Path.GetRelativePath(path, directory)}")
                    .Order(StringComparer.Ordinal))));

static string Hash(ReadOnlySpan<byte> value) =>
    Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

static string FindRepositoryRoot(string? explicitRoot)
{
    if (!string.IsNullOrWhiteSpace(explicitRoot))
        return Path.GetFullPath(explicitRoot);
    DirectoryInfo? current = new(AppContext.BaseDirectory);
    while (current != null)
    {
        if (Directory.Exists(Path.Combine(current.FullName, "src", "Spyro.Editor.Core")))
            return current.FullName;
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the repository root.");
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record FileState(long Length, DateTime LastWriteUtc, string Sha256);
internal sealed record DirectoryState(string Canonical);
