using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

internal enum Id65RewardSupportLayoutProfileKind
{
    Core4,
    Full8
}

internal enum Id65RewardSupportReservationKind
{
    Bull,
    Torro
}

internal sealed record Id65RewardSupportWorldPoint(
    decimal X,
    decimal Y,
    decimal Z);

internal sealed record Id65RewardSupportRawPoint(
    int X,
    int Y,
    int Z)
{
    public Id65RewardSupportWorldPoint ToWorld() =>
        Id65RewardSupportCoordinates.RawToWorld(this);
}

/// <summary>
/// Exact ID65 authoring coordinate transform. X/Y authoring coordinates are
/// translated by +752 world units; Z remains in the shared scene space.
/// All public conversions reject values that cannot round-trip at 1/16 world
/// unit precision.
/// </summary>
internal static class Id65RewardSupportCoordinates
{
    public const int RawUnitsPerWorldUnit = 16;
    public const int AuthoringTranslationWorld = 752;
    public const int AuthoringTranslationRaw =
        AuthoringTranslationWorld * RawUnitsPerWorldUnit;
    public const int GroundWorldZ = 512;
    public const int GroundRawZ = GroundWorldZ * RawUnitsPerWorldUnit;

    public static Id65RewardSupportRawPoint WorldToRaw(Id65RewardSupportWorldPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);
        return new(
            ToExactRaw(point.X, nameof(point.X)),
            ToExactRaw(point.Y, nameof(point.Y)),
            ToExactRaw(point.Z, nameof(point.Z)));
    }

    public static Id65RewardSupportWorldPoint RawToWorld(Id65RewardSupportRawPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);
        return new(
            point.X / (decimal)RawUnitsPerWorldUnit,
            point.Y / (decimal)RawUnitsPerWorldUnit,
            point.Z / (decimal)RawUnitsPerWorldUnit);
    }

    public static Id65RewardSupportRawPoint LocalToScene(Id65RewardSupportRawPoint local)
    {
        ArgumentNullException.ThrowIfNull(local);
        return new(
            checked(local.X + AuthoringTranslationRaw),
            checked(local.Y + AuthoringTranslationRaw),
            local.Z);
    }

    public static Id65RewardSupportRawPoint SceneToLocal(Id65RewardSupportRawPoint scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        return new(
            checked(scene.X - AuthoringTranslationRaw),
            checked(scene.Y - AuthoringTranslationRaw),
            scene.Z);
    }

    private static int ToExactRaw(decimal value, string label)
    {
        decimal scaled;
        try
        {
            scaled = checked(value * RawUnitsPerWorldUnit);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(label, value,
                "The coordinate is outside the exact raw-coordinate range.");
        }

        if (decimal.Truncate(scaled) != scaled || scaled < int.MinValue || scaled > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(label, value,
                "ID65 support coordinates must round-trip exactly at 1/16 world-unit precision.");
        }

        return decimal.ToInt32(scaled);
    }
}

internal sealed record Id65RewardSupportEnvelope(
    string Id,
    Id65RewardSupportRawPoint Minimum,
    Id65RewardSupportRawPoint Maximum,
    int WidthRaw,
    int HeightRaw)
{
    public decimal WidthWorld =>
        WidthRaw / (decimal)Id65RewardSupportCoordinates.RawUnitsPerWorldUnit;
    public decimal HeightWorld =>
        HeightRaw / (decimal)Id65RewardSupportCoordinates.RawUnitsPerWorldUnit;

    public bool ContainsXy(Id65RewardSupportRawPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);
        return point.X >= Minimum.X && point.X <= Maximum.X &&
            point.Y >= Minimum.Y && point.Y <= Maximum.Y;
    }
}

internal sealed record Id65RewardSupportOwnerIdentity(
    int TrueIndex,
    ushort ActorClass,
    Id65RewardEncoding Encoding,
    Id65RewardOwnerSemantics OwnerSemantics,
    int RewardValue,
    string RowSha256,
    uint PropertiesSceneOffset,
    int PropertiesByteLength,
    string PropertiesSha256,
    int RetirementByteIndex,
    byte RetirementBitMask,
    bool TransientRewardChildRuntimeProofVerified);

internal sealed record Id65RewardSupportSourceIdentity(
    string CensusProfileId,
    string LockedSourceImageSha256,
    string DeterministicCensusSha256,
    string RewardRowsSha256,
    int ObjectRecordCount,
    int RewardRowCount,
    int RewardTotal,
    int StationaryOwnerRowCount,
    int StationaryOwnerRewardTotal,
    int EnemyOwnerRowCount,
    int EnemyOwnerRewardTotal,
    int DragonBundleCount,
    int RequiredEggTarget,
    bool ZeroEggReplacementRequired);

internal sealed record Id65RewardSupportProfile(
    string Id,
    string TerrainSupportProfileId,
    string TerrainSupportCanonicalSha256,
    Id65RewardSupportLayoutProfileKind Kind,
    Id65RewardSupportEnvelope Envelope,
    int TerrainSectorCount,
    int StationaryOwnerCapacity,
    int StationaryRewardCapacity,
    int MobileEnemyReservationCapacity,
    int MobileEnemyRewardCapacity,
    int CombinedOwnerCapacity,
    int CombinedRewardCapacity,
    bool CapacityOnly,
    bool Total200RuntimeAccepted,
    bool RuntimeSafe,
    bool PromotionAuthorized);

internal sealed record Id65RewardSupportLandingApron(
    string LandingRecordId,
    string PlayerAnchorId,
    int PlayerAnchorTrueIndex,
    Id65RewardSupportRawPoint Landing,
    Id65RewardSupportRawPoint PlayerAnchor,
    Id65RewardSupportRawPoint ApronMinimum,
    Id65RewardSupportRawPoint ApronMaximum,
    int ApronHalfExtentWorld,
    int ApronHalfExtentRaw,
    int MinimumLandingEdgeMarginRaw,
    bool LandingAndPlayerXyUnchanged,
    bool EntireApronInsideCore4);

internal sealed record Id65RewardSupportStationarySlot(
    int SlotIndex,
    int Row,
    int Column,
    Id65RewardSupportRawPoint Point,
    bool Spare);

internal sealed record Id65RewardSupportStationaryPlacement(
    Id65RewardSupportStationarySlot Slot,
    Id65RewardSupportOwnerIdentity Owner,
    bool DesiredStateOnly,
    bool RuntimeAccepted);

internal sealed record Id65RewardSupportDragonIdentity(
    int PedestalTrueIndex,
    int DragonTrueIndex,
    int ContainerTrueIndex,
    string ThreeRowBundleSha256,
    string CameraDataSha256,
    string SceneLinkSha256,
    string CameraTrackSha256,
    int CutsceneIndex,
    int DragonNameIndex,
    bool SourceCameraPlacementResolved,
    bool SourceRoutePlacementResolved,
    bool SourceDestinationSupportResolved);

internal sealed record Id65RewardSupportDragonPrecinct(
    string Id,
    Id65RewardSupportDragonIdentity Dragon,
    Id65RewardSupportRawPoint Pedestal,
    Id65RewardSupportRawPoint Actor,
    Id65RewardSupportRawPoint ControlDestination,
    Id65RewardSupportEnvelope PrecinctEnvelope,
    Id65RewardSupportEnvelope PlatformLocalEnvelope,
    Id65RewardSupportEnvelope ActorRelativeEnvelope,
    int OuterEdgeMinimumMarginRaw,
    int SectorSeamMinimumMarginRaw,
    bool DesiredSupportPinned,
    bool CameraRuntimeVerified,
    bool RouteRuntimeVerified,
    bool DestinationRuntimeVerified);

internal sealed record Id65RewardSupportEnemyReservation(
    string Id,
    Id65RewardSupportReservationKind Kind,
    Id65RewardSupportOwnerIdentity Owner,
    Id65RewardSupportRawPoint Point,
    bool ReservationOnly,
    bool RoutePinned,
    bool ActivationBoundsPinned,
    bool RuntimeAccepted);

internal sealed record Id65RewardSupportZeroEggBoundary(
    int RequiredEggTarget,
    int EggThiefTrueIndex,
    ushort EggThiefClass,
    byte CarriedEggClass,
    string EggThiefRowSha256,
    string EggThiefPropertiesSha256,
    bool ReplacementRequired,
    bool WriterAuthorized);

internal sealed record Id65RewardSupportRuntimeGates(
    bool Core4StationaryCollectionVerified,
    bool DragonCameraPlacementVerified,
    bool DragonRoutePlacementVerified,
    bool MobileEnemyRoutesVerified,
    bool MobileEnemyActivationBoundsVerified,
    bool ChestTransientChildDuplicateAndBitVerified,
    bool CollectionRuntimeVerified,
    bool DeathRuntimeVerified,
    bool ReentryRuntimeVerified,
    bool MemoryCardMatrixVerified,
    bool Total200RuntimeAccepted,
    bool RuntimeSafetyAuthorized);

internal sealed record Id65RewardSupportRejectedClaim(
    string Id,
    bool Rejected,
    string Reason);

internal sealed record Id65RewardSupportDragonSeparation(
    string FirstDragonId,
    string SecondDragonId,
    int GapXRaw,
    int GapYRaw,
    long DistanceSquaredRaw);

internal sealed record Id65RewardSupportStaticSpacing(
    int NearestLandingSlotIndex,
    long NearestLandingSlotDistanceSquaredRaw,
    decimal NearestLandingSlotDistanceWorldRounded2,
    int NearestStationaryDragonSlotIndex,
    string NearestStationaryDragonId,
    int NearestStationaryDragonGapRaw,
    decimal NearestStationaryDragonGapWorld,
    int StationaryMinimumOuterEdgeMarginRaw,
    int StationaryMinimumSectorSeamMarginRaw,
    int DragonMinimumOuterEdgeMarginRaw,
    int DragonMinimumSectorSeamMarginRaw,
    bool EveryStationarySlotOutsideLandingApron,
    bool EveryStationarySlotOutsideDragonEnvelopes,
    bool DragonEnvelopesPairwiseSeparated);

/// <summary>
/// Immutable desired-state layout for the exact frozen ID65 reward census.
/// It records capacity and reservations only; it never owns model/scene bytes,
/// a row-80 patch, a filesystem path, or publication state.
/// </summary>
internal sealed class Id65RewardSupportLayout
{
    private readonly ReadOnlyCollection<Id65RewardSupportProfile> _profiles;
    private readonly ReadOnlyCollection<Id65RewardSupportStationarySlot> _stationarySlots;
    private readonly ReadOnlyCollection<Id65RewardSupportStationaryPlacement> _stationaryPlacements;
    private readonly ReadOnlyCollection<Id65RewardSupportDragonPrecinct> _dragonPrecincts;
    private readonly ReadOnlyCollection<Id65RewardSupportDragonSeparation> _dragonSeparations;
    private readonly ReadOnlyCollection<Id65RewardSupportEnemyReservation> _enemyReservations;
    private readonly ReadOnlyCollection<Id65RewardSupportRejectedClaim> _rejectedClaims;

    internal Id65RewardSupportLayout(
        string profileId,
        Id65RewardSupportSourceIdentity source,
        IEnumerable<Id65RewardSupportProfile> profiles,
        Id65RewardSupportEnvelope coreEnvelope,
        Id65RewardSupportEnvelope fullEnvelope,
        Id65RewardSupportEnvelope enemyBayEnvelope,
        Id65RewardSupportLandingApron landingApron,
        IEnumerable<Id65RewardSupportStationarySlot> stationarySlots,
        IEnumerable<Id65RewardSupportStationaryPlacement> stationaryPlacements,
        IEnumerable<Id65RewardSupportDragonPrecinct> dragonPrecincts,
        IEnumerable<Id65RewardSupportDragonSeparation> dragonSeparations,
        IEnumerable<Id65RewardSupportEnemyReservation> enemyReservations,
        Id65RewardSupportZeroEggBoundary zeroEgg,
        Id65RewardSupportRuntimeGates runtimeGates,
        Id65RewardSupportStaticSpacing staticSpacing,
        IEnumerable<Id65RewardSupportRejectedClaim> rejectedClaims,
        string deterministicLayoutSha256)
    {
        ProfileId = profileId;
        Source = source;
        _profiles = Array.AsReadOnly(profiles.ToArray());
        CoreEnvelope = coreEnvelope;
        FullEnvelope = fullEnvelope;
        EnemyBayEnvelope = enemyBayEnvelope;
        LandingApron = landingApron;
        _stationarySlots = Array.AsReadOnly(stationarySlots.ToArray());
        _stationaryPlacements = Array.AsReadOnly(stationaryPlacements.ToArray());
        _dragonPrecincts = Array.AsReadOnly(dragonPrecincts.ToArray());
        _dragonSeparations = Array.AsReadOnly(dragonSeparations.ToArray());
        _enemyReservations = Array.AsReadOnly(enemyReservations.ToArray());
        ZeroEgg = zeroEgg;
        RuntimeGates = runtimeGates;
        StaticSpacing = staticSpacing;
        _rejectedClaims = Array.AsReadOnly(rejectedClaims.ToArray());
        DeterministicLayoutSha256 = deterministicLayoutSha256;
    }

    public string ProfileId { get; }
    public Id65RewardSupportSourceIdentity Source { get; }
    public IReadOnlyList<Id65RewardSupportProfile> Profiles => _profiles;
    public Id65RewardSupportEnvelope CoreEnvelope { get; }
    public Id65RewardSupportEnvelope FullEnvelope { get; }
    public Id65RewardSupportEnvelope EnemyBayEnvelope { get; }
    public Id65RewardSupportLandingApron LandingApron { get; }
    public IReadOnlyList<Id65RewardSupportStationarySlot> StationarySlots => _stationarySlots;
    public IReadOnlyList<Id65RewardSupportStationaryPlacement> StationaryPlacements => _stationaryPlacements;
    public IReadOnlyList<Id65RewardSupportDragonPrecinct> DragonPrecincts => _dragonPrecincts;
    public IReadOnlyList<Id65RewardSupportDragonSeparation> DragonSeparations => _dragonSeparations;
    public IReadOnlyList<Id65RewardSupportEnemyReservation> EnemyReservations => _enemyReservations;
    public Id65RewardSupportZeroEggBoundary ZeroEgg { get; }
    public Id65RewardSupportRuntimeGates RuntimeGates { get; }
    public Id65RewardSupportStaticSpacing StaticSpacing { get; }
    public IReadOnlyList<Id65RewardSupportRejectedClaim> RejectedClaims => _rejectedClaims;
    public string DeterministicLayoutSha256 { get; }

    public bool LockedSourcePreserved => true;
    public bool StaticReadOnly => true;
    public bool DesiredStateOnly => true;
    public bool ProducesRow80Patches => false;
    public bool WritesFilesystem => false;
    public bool WritesBin => false;
    public bool WritesCue => false;
    public bool WriterAuthorized => false;
    public bool PublisherAuthorized => false;
    public bool AppIntegrated => false;
    public bool NormalCreateBinEnabled => false;
    public bool RuntimeVerified => false;
    public bool PromotionAuthorized => false;
    public bool ReleaseAuthorized => false;
}

/// <summary>
/// Pure in-memory desired-state contract joining the frozen 81/200 reward
/// census to exact +752 Core4 and Full8 support coordinates. Core4 owns only
/// the 71 stationary rows (171 treasure). Full8 adds ten mobile-owner
/// reservations (29 treasure) as capacity, never as runtime acceptance.
/// </summary>
internal static class Id65RewardSupportLayoutContract
{
    public const string ProfileId =
        "id65-reward-support-layout-static-read-only-v1";
    public const string RequiredSmokeAssemblyName =
        "Spyro.Editor.Id65RewardSupportLayoutContractSmoke";
    public const string CoreLayoutId = "reward-support-core4-v1";
    public const string FullLayoutId = "reward-support-full8-v1";
    public const string CoreTerrainSupportProfileId = "support-core-4-v1";
    public const string FullTerrainSupportProfileId = "support-full-8-v1";
    public const string ExpectedDeterministicLayoutSha256 =
        "5637ee969cb43431383341a246c2c4b7ed73eccad3073008ce6e58e7648305f5";

    private const int CoreMinimumWorld = 272;
    private const int CoreMaximumWorld = 1_232;
    private const int FullMaximumWorldX = 2_192;
    private const int StationaryColumns = 12;
    private const int StationaryRows = 6;
    private const int StationarySlotCount = StationaryColumns * StationaryRows;
    private const int StationaryAssignedCount = 71;
    private const int StationaryRewardTotal = 171;
    private const int EnemyReservationCount = 10;
    private const int EnemyRewardTotal = 29;
    private static readonly DragonLayoutSpec[] DragonSpecs =
    [
        new("dragon-a", 3, 12, 102,
            new(9_216, 16_002, 8_192),
            new(9_216, 16_032, 8_315),
            new(9_216, 16_032, 9_216),
            new(8_010, 13_005, 8_192),
            new(10_411, 16_032, 9_216)),
        new("dragon-b", 4, 13, 103,
            new(13_322, 19_146, 8_192),
            new(13_312, 19_136, 8_315),
            new(13_312, 19_136, 9_216),
            new(13_312, 16_820, 8_192),
            new(16_112, 19_146, 9_216)),
        new("dragon-c", 48, 49, 104,
            new(10_752, 17_664, 8_192),
            new(10_752, 17_664, 8_315),
            new(10_752, 17_664, 9_216),
            new(7_626, 16_803, 8_192),
            new(10_752, 19_148, 9_216)),
        new("dragon-d", 100, 101, 105,
            new(15_360, 16_032, 8_192),
            new(15_360, 16_032, 8_315),
            new(15_360, 16_032, 9_216),
            new(13_859, 12_551, 8_192),
            new(15_360, 16_032, 9_216))
    ];

    private static readonly EnemyLayoutSpec[] EnemySpecs =
    [
        new("bull-t0", 0, Id65RewardSupportReservationKind.Bull, 21_632, 6_528),
        new("bull-t1", 1, Id65RewardSupportReservationKind.Bull, 25_472, 6_528),
        new("bull-t2", 2, Id65RewardSupportReservationKind.Bull, 29_312, 6_528),
        new("bull-t5", 5, Id65RewardSupportReservationKind.Bull, 33_152, 6_528),
        new("bull-t7", 7, Id65RewardSupportReservationKind.Bull, 21_632, 10_368),
        new("bull-t8", 8, Id65RewardSupportReservationKind.Bull, 25_472, 10_368),
        new("bull-t9", 9, Id65RewardSupportReservationKind.Bull, 29_312, 10_368),
        new("bull-t11", 11, Id65RewardSupportReservationKind.Bull, 33_152, 10_368),
        new("torro-t6", 6, Id65RewardSupportReservationKind.Torro, 21_632, 15_872),
        new("torro-t10", 10, Id65RewardSupportReservationKind.Torro, 25_472, 15_872)
    ];

    public static Id65RewardSupportLayout Build(
        Id65RewardCensus census,
        Id65AuthoringSupportManifest coreSupportManifest,
        Id65AuthoringSupportManifest fullSupportManifest)
    {
        CanonicalSource canonical = CanonicalizeAndRequire(census);
        Id65RewardSupportEnvelope coreEnvelope = BuildEnvelope(
            "core4-envelope", CoreMinimumWorld, CoreMinimumWorld,
            CoreMaximumWorld, CoreMaximumWorld);
        Id65RewardSupportEnvelope fullEnvelope = BuildEnvelope(
            "full8-envelope", CoreMinimumWorld, CoreMinimumWorld,
            FullMaximumWorldX, CoreMaximumWorld);
        Id65RewardSupportEnvelope enemyBayEnvelope = BuildEnvelope(
            "full8-enemy-bay-envelope", CoreMaximumWorld, CoreMinimumWorld,
            FullMaximumWorldX, CoreMaximumWorld);
        (string CoreCanonicalSha256, string FullCanonicalSha256) supportIdentities =
            RequireTerrainSupportManifests(
                coreSupportManifest,
                fullSupportManifest,
                coreEnvelope,
                fullEnvelope);

        Id65RewardSupportProfile[] profiles =
        [
            new(
                CoreLayoutId,
                CoreTerrainSupportProfileId,
                supportIdentities.CoreCanonicalSha256,
                Id65RewardSupportLayoutProfileKind.Core4,
                coreEnvelope,
                TerrainSectorCount: 4,
                StationaryOwnerCapacity: StationaryAssignedCount,
                StationaryRewardCapacity: StationaryRewardTotal,
                MobileEnemyReservationCapacity: 0,
                MobileEnemyRewardCapacity: 0,
                CombinedOwnerCapacity: StationaryAssignedCount,
                CombinedRewardCapacity: StationaryRewardTotal,
                CapacityOnly: true,
                Total200RuntimeAccepted: false,
                RuntimeSafe: false,
                PromotionAuthorized: false),
            new(
                FullLayoutId,
                FullTerrainSupportProfileId,
                supportIdentities.FullCanonicalSha256,
                Id65RewardSupportLayoutProfileKind.Full8,
                fullEnvelope,
                TerrainSectorCount: 8,
                StationaryOwnerCapacity: StationaryAssignedCount,
                StationaryRewardCapacity: StationaryRewardTotal,
                MobileEnemyReservationCapacity: EnemyReservationCount,
                MobileEnemyRewardCapacity: EnemyRewardTotal,
                CombinedOwnerCapacity: 81,
                CombinedRewardCapacity: 200,
                CapacityOnly: true,
                Total200RuntimeAccepted: false,
                RuntimeSafe: false,
                PromotionAuthorized: false)
        ];

        Id65RewardSupportLandingApron landingApron = BuildLandingApron(coreEnvelope);
        Id65RewardSupportStationarySlot[] slots = BuildStationarySlots(coreEnvelope);
        Id65RewardSupportStationaryPlacement[] stationaryPlacements = canonical.StationaryOwners
            .Select((owner, index) => new Id65RewardSupportStationaryPlacement(
                slots[index], owner, DesiredStateOnly: true, RuntimeAccepted: false))
            .ToArray();
        Id65RewardSupportDragonPrecinct[] dragons = BuildDragonPrecincts(
            canonical.Dragons, coreEnvelope);
        Id65RewardSupportDragonSeparation[] dragonSeparations =
            BuildDragonSeparations(dragons);
        Id65RewardSupportEnemyReservation[] enemies = BuildEnemyReservations(
            canonical.EnemyOwners, enemyBayEnvelope, fullEnvelope);
        Id65RewardSupportStaticSpacing staticSpacing = BuildStaticSpacing(
            slots, landingApron, dragons, dragonSeparations, coreEnvelope);
        Id65RewardSupportZeroEggBoundary zeroEgg = canonical.ZeroEgg;
        Id65RewardSupportRuntimeGates gates = new(
            Core4StationaryCollectionVerified: false,
            DragonCameraPlacementVerified: false,
            DragonRoutePlacementVerified: false,
            MobileEnemyRoutesVerified: false,
            MobileEnemyActivationBoundsVerified: false,
            ChestTransientChildDuplicateAndBitVerified: false,
            CollectionRuntimeVerified: false,
            DeathRuntimeVerified: false,
            ReentryRuntimeVerified: false,
            MemoryCardMatrixVerified: false,
            Total200RuntimeAccepted: false,
            RuntimeSafetyAuthorized: false);
        Id65RewardSupportRejectedClaim[] rejectedClaims = BuildRejectedClaims();

        string deterministicHash = ComputeDeterministicHash(
            canonical.Source,
            profiles,
            coreEnvelope,
            fullEnvelope,
            enemyBayEnvelope,
            landingApron,
            slots,
            stationaryPlacements,
            dragons,
            dragonSeparations,
            enemies,
            zeroEgg,
            gates,
            staticSpacing,
            rejectedClaims);

        Id65RewardSupportLayout result = new(
            ProfileId,
            canonical.Source,
            profiles,
            coreEnvelope,
            fullEnvelope,
            enemyBayEnvelope,
            landingApron,
            slots,
            stationaryPlacements,
            dragons,
            dragonSeparations,
            enemies,
            zeroEgg,
            gates,
            staticSpacing,
            rejectedClaims,
            deterministicHash);
        ValidateReadback(result);
        return result;
    }

    public static void ValidateReadback(Id65RewardSupportLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (layout.ProfileId != ProfileId ||
            layout.Source.CensusProfileId != Id65RewardCensusContract.ProfileId ||
            layout.Source.DeterministicCensusSha256 !=
                Id65RewardCensusContract.ExpectedDeterministicCensusSha256)
        {
            throw new InvalidDataException("The reward-support source/profile readback changed.");
        }

        RequireEnvelope(layout.CoreEnvelope, "core4-envelope",
            4_352, 4_352, 19_712, 19_712, 15_360, 15_360);
        RequireEnvelope(layout.FullEnvelope, "full8-envelope",
            4_352, 4_352, 35_072, 19_712, 30_720, 15_360);
        RequireEnvelope(layout.EnemyBayEnvelope, "full8-enemy-bay-envelope",
            19_712, 4_352, 35_072, 19_712, 15_360, 15_360);

        if (layout.Profiles.Count != 2 ||
            layout.Profiles[0] is not
            {
                Id: CoreLayoutId,
                TerrainSupportProfileId: CoreTerrainSupportProfileId,
                Kind: Id65RewardSupportLayoutProfileKind.Core4,
                TerrainSectorCount: 4,
                StationaryOwnerCapacity: 71,
                StationaryRewardCapacity: 171,
                MobileEnemyReservationCapacity: 0,
                MobileEnemyRewardCapacity: 0,
                CombinedOwnerCapacity: 71,
                CombinedRewardCapacity: 171,
                CapacityOnly: true,
                Total200RuntimeAccepted: false,
                RuntimeSafe: false,
                PromotionAuthorized: false
            } ||
            layout.Profiles[1] is not
            {
                Id: FullLayoutId,
                TerrainSupportProfileId: FullTerrainSupportProfileId,
                Kind: Id65RewardSupportLayoutProfileKind.Full8,
                TerrainSectorCount: 8,
                StationaryOwnerCapacity: 71,
                StationaryRewardCapacity: 171,
                MobileEnemyReservationCapacity: 10,
                MobileEnemyRewardCapacity: 29,
                CombinedOwnerCapacity: 81,
                CombinedRewardCapacity: 200,
                CapacityOnly: true,
                Total200RuntimeAccepted: false,
                RuntimeSafe: false,
                PromotionAuthorized: false
            })
        {
            throw new InvalidDataException("The exact Core4/Full8 capacity boundary changed.");
        }
        RequireStoredTerrainSupportProfiles(layout.Profiles);

        RequireLanding(layout.LandingApron, layout.CoreEnvelope);
        RequireStationarySlots(layout.StationarySlots, layout.CoreEnvelope);
        if (layout.StationaryPlacements.Count != 71 ||
            layout.StationaryPlacements.Sum(item => item.Owner.RewardValue) != 171 ||
            !layout.StationaryPlacements.Select(item => item.Owner.TrueIndex)
                .SequenceEqual(layout.StationaryPlacements.Select(item => item.Owner.TrueIndex).Order()) ||
            layout.StationaryPlacements.Select(item => item.Slot.SlotIndex)
                .Distinct().Count() != 71 ||
            layout.StationaryPlacements.Any(item =>
                item.Slot.Spare || !item.DesiredStateOnly || item.RuntimeAccepted ||
                item.Owner.OwnerSemantics is not
                    (Id65RewardOwnerSemantics.StationaryLooseGem or
                     Id65RewardOwnerSemantics.StationaryChestCarrier)))
        {
            throw new InvalidDataException("The deterministic 71-row/171 stationary placement readback changed.");
        }

        RequireDragons(layout.DragonPrecincts, layout.CoreEnvelope);
        RequireDragonSeparations(layout.DragonSeparations);
        RequireStaticSpacing(layout.StaticSpacing);
        RequireEnemies(layout.EnemyReservations, layout.EnemyBayEnvelope, layout.FullEnvelope);
        if (layout.ZeroEgg is not
            {
                RequiredEggTarget: 0,
                EggThiefTrueIndex: 88,
                EggThiefClass: 0x0021,
                CarriedEggClass: 0x22,
                ReplacementRequired: true,
                WriterAuthorized: false
            })
        {
            throw new InvalidDataException("The exact zero-egg/T88 boundary changed.");
        }

        if (layout.RuntimeGates is not
            {
                Core4StationaryCollectionVerified: false,
                DragonCameraPlacementVerified: false,
                DragonRoutePlacementVerified: false,
                MobileEnemyRoutesVerified: false,
                MobileEnemyActivationBoundsVerified: false,
                ChestTransientChildDuplicateAndBitVerified: false,
                CollectionRuntimeVerified: false,
                DeathRuntimeVerified: false,
                ReentryRuntimeVerified: false,
                MemoryCardMatrixVerified: false,
                Total200RuntimeAccepted: false,
                RuntimeSafetyAuthorized: false
            } || layout.RejectedClaims.Count != 10 ||
            layout.RejectedClaims.Any(item => !item.Rejected))
        {
            throw new InvalidDataException("A reward-support runtime gate or rejection boundary opened.");
        }

        if (!layout.LockedSourcePreserved || !layout.StaticReadOnly || !layout.DesiredStateOnly ||
            layout.ProducesRow80Patches || layout.WritesFilesystem || layout.WritesBin ||
            layout.WritesCue || layout.WriterAuthorized || layout.PublisherAuthorized ||
            layout.AppIntegrated || layout.NormalCreateBinEnabled || layout.RuntimeVerified ||
            layout.PromotionAuthorized || layout.ReleaseAuthorized)
        {
            throw new InvalidDataException("The desired-state layout authorized a writer, runtime, publisher, or release path.");
        }

        string computed = ComputeDeterministicHash(
            layout.Source,
            layout.Profiles,
            layout.CoreEnvelope,
            layout.FullEnvelope,
            layout.EnemyBayEnvelope,
            layout.LandingApron,
            layout.StationarySlots,
            layout.StationaryPlacements,
            layout.DragonPrecincts,
            layout.DragonSeparations,
            layout.EnemyReservations,
            layout.ZeroEgg,
            layout.RuntimeGates,
            layout.StaticSpacing,
            layout.RejectedClaims);
        if (!computed.Equals(layout.DeterministicLayoutSha256, StringComparison.Ordinal))
            throw new InvalidDataException("The reward-support deterministic readback hash changed.");
        if (!computed.Equals(ExpectedDeterministicLayoutSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"The reward-support deterministic SHA-256 is {computed}, expected {ExpectedDeterministicLayoutSha256}.");
        }
    }

    private static CanonicalSource CanonicalizeAndRequire(Id65RewardCensus census)
    {
        ArgumentNullException.ThrowIfNull(census);
        if (census.ProfileId != Id65RewardCensusContract.ProfileId ||
            census.LockedSourceImageSha256 != Id65RewardCensusContract.LockedSourceImageSha256 ||
            census.DeterministicCensusSha256 !=
                Id65RewardCensusContract.ExpectedDeterministicCensusSha256 ||
            census.ObjectRecordCount != 107 || census.RewardRowCount != 81 ||
            census.RewardTotal != 200 || census.RewardRows.Count != 81 ||
            census.DragonBundles.Count != 4)
        {
            throw new InvalidDataException("The frozen ID65 reward census identity/count boundary changed.");
        }

        Id65RewardSupportOwnerIdentity[] owners = census.RewardRows
            .OrderBy(row => row.TrueIndex)
            .Select(CloneOwner)
            .ToArray();
        if (owners.Select(item => item.TrueIndex).Distinct().Count() != 81 ||
            owners.Sum(item => item.RewardValue) != 200 ||
            owners.Any(item => item.TrueIndex < 0 || item.TrueIndex > 255 ||
                item.RetirementByteIndex != item.TrueIndex / 8 ||
                item.RetirementBitMask != (byte)(1 << (item.TrueIndex % 8)) ||
                item.RewardValue is not (1 or 2 or 5 or 10)))
        {
            throw new InvalidDataException("The canonical reward-owner rows or retirement bits changed.");
        }

        Id65RewardSupportOwnerIdentity[] stationary = owners
            .Where(item => item.OwnerSemantics is
                Id65RewardOwnerSemantics.StationaryLooseGem or
                Id65RewardOwnerSemantics.StationaryChestCarrier)
            .ToArray();
        Id65RewardSupportOwnerIdentity[] enemies = owners
            .Where(item => item.OwnerSemantics is
                Id65RewardOwnerSemantics.BullEnemy or
                Id65RewardOwnerSemantics.TorroEnemy)
            .ToArray();
        if (stationary.Length != StationaryAssignedCount ||
            stationary.Sum(item => item.RewardValue) != StationaryRewardTotal ||
            enemies.Length != EnemyReservationCount ||
            enemies.Sum(item => item.RewardValue) != EnemyRewardTotal ||
            owners.Length != stationary.Length + enemies.Length)
        {
            throw new InvalidDataException("The frozen 71/171 stationary and 10/29 enemy split changed.");
        }

        Id65RewardOwnerAggregate aggregate = census.OwnerAggregate;
        if (aggregate.StationaryOwnerRowCount != 71 ||
            aggregate.StationaryOwnerRewardTotal != 171 ||
            aggregate.BehaviorEnemyOwnerRowCount != 10 ||
            aggregate.BehaviorEnemyOwnerRewardTotal != 29 ||
            aggregate.BullOwnerRowCount != 8 || aggregate.BullOwnerRewardTotal != 26 ||
            !aggregate.BullTrueIndexes.Order().SequenceEqual(new[] { 0, 1, 2, 5, 7, 8, 9, 11 }) ||
            aggregate.TorroOwnerRowCount != 2 || aggregate.TorroOwnerRewardTotal != 3 ||
            !aggregate.TorroTrueIndexes.Order().SequenceEqual(new[] { 6, 10 }) ||
            aggregate.StationaryLooseGemOwnerRowCount != 43 ||
            aggregate.StationaryLooseGemRewardTotal != 73 ||
            aggregate.StationaryChestCarrierRowCount != 28 ||
            aggregate.StationaryChestCarrierRewardTotal != 98 ||
            !aggregate.ChestCarrierRowOwnsRewardValue ||
            !aggregate.ChestCarrierRowOwnsRetirementBit ||
            aggregate.TransientChildrenCountedAsAdditionalRows ||
            aggregate.TransientChildrenOwnAdditionalRetirementBits ||
            aggregate.RuntimeDuplicateAndChildBitProofVerified)
        {
            throw new InvalidDataException("The reward-owner aggregate or transient-child boundary changed.");
        }

        int[] expectedEnemyIndexes = EnemySpecs.Select(item => item.TrueIndex).Order().ToArray();
        if (!enemies.Select(item => item.TrueIndex).Order().SequenceEqual(expectedEnemyIndexes) ||
            enemies.Count(item => item.OwnerSemantics == Id65RewardOwnerSemantics.BullEnemy) != 8 ||
            enemies.Count(item => item.OwnerSemantics == Id65RewardOwnerSemantics.TorroEnemy) != 2)
        {
            throw new InvalidDataException("The exact Bull/Torro reservation-owner set changed.");
        }

        Id65RewardSupportDragonIdentity[] dragons = census.DragonBundles
            .OrderBy(item => item.DragonTrueIndex)
            .Select(CloneDragon)
            .ToArray();
        for (int index = 0; index < DragonSpecs.Length; index++)
        {
            DragonLayoutSpec expected = DragonSpecs[index];
            Id65RewardSupportDragonIdentity actual = dragons[index];
            if (actual.PedestalTrueIndex != expected.PedestalTrueIndex ||
                actual.DragonTrueIndex != expected.DragonTrueIndex ||
                actual.ContainerTrueIndex != expected.ContainerTrueIndex ||
                actual.SourceCameraPlacementResolved || actual.SourceRoutePlacementResolved ||
                actual.SourceDestinationSupportResolved)
            {
                throw new InvalidDataException($"Dragon precinct {expected.Id} source identity changed.");
            }
        }

        Id65RewardZeroEggRequirement sourceZeroEgg = census.ZeroEgg;
        if (sourceZeroEgg.RequiredEggTarget != 0 || sourceZeroEgg.EggThiefTrueIndex != 88 ||
            sourceZeroEgg.EggThiefClass != 0x0021 || sourceZeroEgg.CarriedEggClass != 0x22 ||
            sourceZeroEgg.LockedSourceAlreadySatisfiesZeroEgg ||
            !sourceZeroEgg.T88ReplacementRequired || sourceZeroEgg.WriterAuthorized)
        {
            throw new InvalidDataException("The frozen T88 zero-egg requirement changed.");
        }
        Id65RewardSupportZeroEggBoundary zeroEgg = new(
            sourceZeroEgg.RequiredEggTarget,
            sourceZeroEgg.EggThiefTrueIndex,
            sourceZeroEgg.EggThiefClass,
            sourceZeroEgg.CarriedEggClass,
            sourceZeroEgg.EggThiefRowSha256,
            sourceZeroEgg.EggThiefPropertiesSha256,
            sourceZeroEgg.T88ReplacementRequired,
            WriterAuthorized: false);

        if (!census.LockedSourcePreserved || !census.StaticReadOnly || census.ProducesPatches ||
            census.WritesBin || census.WritesCue || census.WriterAuthorized ||
            census.PublisherAuthorized || census.AppIntegrated || census.NormalCreateBinEnabled ||
            census.RuntimeVerified || census.RewardRetirementResolved ||
            census.BullBehaviorResolved || census.TorroBehaviorResolved ||
            census.DestinationSupportResolved || census.RoutePlacementResolved ||
            census.DragonCameraPlacementResolved || census.CollectionRuntimeVerified ||
            census.DeathRuntimeVerified || census.ReentryRuntimeVerified ||
            census.MemoryCardMatrixVerified || !census.NoCardPersistenceClaim ||
            census.PromotionAuthorized || census.ReleaseAuthorized)
        {
            throw new InvalidDataException("The frozen census opened an authoring, runtime, persistence, or release gate.");
        }

        if (census.RewardRetirementOwnerCandidates.Count != 81 ||
            census.RewardRetirementOwnerCandidates.Any(item => item.RetirementAuthorized) ||
            !census.RewardRetirementOwnerCandidates.Select(item => item.TrueIndex).Order()
                .SequenceEqual(owners.Select(item => item.TrueIndex)) ||
            census.RetirementOwnerMaximumTrueIndex != 255 ||
            !census.CurrentObjectTableWithinRetirementOwnerBound ||
            !census.EveryRewardOwnerWithinRetirementOwnerBound)
        {
            throw new InvalidDataException("The frozen reward retirement-owner boundary changed.");
        }

        Id65RewardSupportSourceIdentity source = new(
            census.ProfileId,
            census.LockedSourceImageSha256,
            census.DeterministicCensusSha256,
            census.Dependencies.RewardRowsSha256,
            census.ObjectRecordCount,
            census.RewardRowCount,
            census.RewardTotal,
            stationary.Length,
            stationary.Sum(item => item.RewardValue),
            enemies.Length,
            enemies.Sum(item => item.RewardValue),
            dragons.Length,
            zeroEgg.RequiredEggTarget,
            zeroEgg.ReplacementRequired);
        return new(source, stationary, enemies, dragons, zeroEgg);
    }

    private static Id65RewardSupportOwnerIdentity CloneOwner(Id65RewardCensusRow row)
    {
        if (!row.AuthoredOwnerRow || row.TransientRewardChildAddsAuthoredRow ||
            row.TransientRewardChildOwnsRetirementBit ||
            (row.OwnerSemantics == Id65RewardOwnerSemantics.StationaryChestCarrier &&
             row.TransientRewardChildRuntimeProofVerified) ||
            (row.OwnerSemantics != Id65RewardOwnerSemantics.StationaryChestCarrier &&
             !row.TransientRewardChildRuntimeProofVerified))
        {
            throw new InvalidDataException($"Reward owner T{row.TrueIndex} opened a transient-child boundary.");
        }
        return new(
            row.TrueIndex,
            row.ActorClass,
            row.Encoding,
            row.OwnerSemantics,
            row.RewardValue,
            row.RowSha256,
            row.PropertiesSceneOffset,
            row.PropertiesByteLength,
            row.PropertiesSha256,
            row.RetirementByteIndex,
            row.RetirementBitMask,
            row.TransientRewardChildRuntimeProofVerified);
    }

    private static Id65RewardSupportDragonIdentity CloneDragon(Id65RewardDragonBundle value) =>
        new(
            value.PedestalTrueIndex,
            value.DragonTrueIndex,
            value.ContainerTrueIndex,
            value.ThreeRowBundleSha256,
            value.CameraDataSha256,
            value.SceneLinkSha256,
            value.CameraTrackSha256,
            value.CutsceneIndex,
            value.DragonNameIndex,
            value.CameraPlacementResolved,
            value.RoutePlacementResolved,
            value.DestinationSupportResolved);

    private static (string CoreCanonicalSha256, string FullCanonicalSha256)
        RequireTerrainSupportManifests(
            Id65AuthoringSupportManifest core,
            Id65AuthoringSupportManifest full,
            Id65RewardSupportEnvelope coreEnvelope,
            Id65RewardSupportEnvelope fullEnvelope)
    {
        ArgumentNullException.ThrowIfNull(core);
        ArgumentNullException.ThrowIfNull(full);
        Id65AuthoringSupportManifest expectedCore =
            Id65AuthoringSupportReplacementCompiler.AddCoreSupport(
                Id65AuthoringSupportReplacementCompiler.CreateEmptyManifest());
        Id65AuthoringSupportManifest expectedFull =
            Id65AuthoringSupportReplacementCompiler.AddEnemyBay(expectedCore);
        RequireTerrainSupportManifest(
            core, expectedCore, Id65SupportProfileKind.Core4,
            CoreTerrainSupportProfileId, coreEnvelope, 4);
        RequireTerrainSupportManifest(
            full, expectedFull, Id65SupportProfileKind.Full8,
            FullTerrainSupportProfileId, fullEnvelope, 8);
        return (core.CanonicalSha256, full.CanonicalSha256);
    }

    private static void RequireTerrainSupportManifest(
        Id65AuthoringSupportManifest actual,
        Id65AuthoringSupportManifest expected,
        Id65SupportProfileKind kind,
        string profileId,
        Id65RewardSupportEnvelope envelope,
        int sectorCount)
    {
        string recomputedJson = actual.RecomputeCanonicalJson();
        string recomputedSha = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(recomputedJson))).ToLowerInvariant();
        if (actual.Kind != kind || actual.ProfileId != profileId ||
            actual.AuthoredTranslationWorld !=
                Id65RewardSupportCoordinates.AuthoringTranslationWorld ||
            actual.AuthoredTranslationRaw !=
                Id65RewardSupportCoordinates.AuthoringTranslationRaw ||
            actual.Sectors.Count != sectorCount ||
            actual.CanonicalJson != recomputedJson || actual.CanonicalSha256 != recomputedSha ||
            actual.CanonicalJson != expected.CanonicalJson ||
            actual.CanonicalSha256 != expected.CanonicalSha256 ||
            actual.RetiresLockedSector217 is false ||
            actual.ClearsInheritedCollisionLookups is false)
        {
            throw new InvalidDataException($"The exact immutable {profileId} manifest identity changed.");
        }

        int minimumX = actual.Sectors.Min(item => item.BoundsMinimum.X);
        int minimumY = actual.Sectors.Min(item => item.BoundsMinimum.Y);
        int maximumX = actual.Sectors.Max(item => item.BoundsMaximum.X);
        int maximumY = actual.Sectors.Max(item => item.BoundsMaximum.Y);
        if (minimumX * Id65RewardSupportCoordinates.RawUnitsPerWorldUnit != envelope.Minimum.X ||
            minimumY * Id65RewardSupportCoordinates.RawUnitsPerWorldUnit != envelope.Minimum.Y ||
            maximumX * Id65RewardSupportCoordinates.RawUnitsPerWorldUnit != envelope.Maximum.X ||
            maximumY * Id65RewardSupportCoordinates.RawUnitsPerWorldUnit != envelope.Maximum.Y ||
            actual.Sectors.Any(item =>
                item.SectorIndex < 0 || item.SectorIndex >= sectorCount ||
                item.BoundsMinimum.Z != Id65RewardSupportCoordinates.GroundWorldZ ||
                item.BoundsMaximum.Z != Id65RewardSupportCoordinates.GroundWorldZ) ||
            !actual.Sectors.Select(item => item.SectorIndex)
                .SequenceEqual(Enumerable.Range(0, sectorCount)))
        {
            throw new InvalidDataException($"The {profileId} sector envelope/+752 geometry changed.");
        }
    }

    private static void RequireStoredTerrainSupportProfiles(
        IReadOnlyList<Id65RewardSupportProfile> profiles)
    {
        Id65AuthoringSupportManifest expectedCore =
            Id65AuthoringSupportReplacementCompiler.AddCoreSupport(
                Id65AuthoringSupportReplacementCompiler.CreateEmptyManifest());
        Id65AuthoringSupportManifest expectedFull =
            Id65AuthoringSupportReplacementCompiler.AddEnemyBay(expectedCore);
        if (profiles.Count != 2 ||
            profiles[0].TerrainSupportCanonicalSha256 != expectedCore.CanonicalSha256 ||
            profiles[1].TerrainSupportCanonicalSha256 != expectedFull.CanonicalSha256)
        {
            throw new InvalidDataException(
                "The stored Core4/Full8 immutable support-manifest identities changed.");
        }
    }

    private static Id65RewardSupportDragonSeparation[] BuildDragonSeparations(
        IReadOnlyList<Id65RewardSupportDragonPrecinct> dragons)
    {
        List<Id65RewardSupportDragonSeparation> result = [];
        for (int first = 0; first < dragons.Count; first++)
        for (int second = first + 1; second < dragons.Count; second++)
        {
            Id65RewardSupportEnvelope a = dragons[first].PrecinctEnvelope;
            Id65RewardSupportEnvelope b = dragons[second].PrecinctEnvelope;
            int gapX = Math.Max(Math.Max(b.Minimum.X - a.Maximum.X,
                a.Minimum.X - b.Maximum.X), 0);
            int gapY = Math.Max(Math.Max(b.Minimum.Y - a.Maximum.Y,
                a.Minimum.Y - b.Maximum.Y), 0);
            long squared = checked(((long)gapX * gapX) + ((long)gapY * gapY));
            result.Add(new(
                dragons[first].Id,
                dragons[second].Id,
                gapX,
                gapY,
                squared));
        }
        return result.ToArray();
    }

    private static Id65RewardSupportStaticSpacing BuildStaticSpacing(
        IReadOnlyList<Id65RewardSupportStationarySlot> slots,
        Id65RewardSupportLandingApron landing,
        IReadOnlyList<Id65RewardSupportDragonPrecinct> dragons,
        IReadOnlyList<Id65RewardSupportDragonSeparation> separations,
        Id65RewardSupportEnvelope coreEnvelope)
    {
        (long DistanceSquared, Id65RewardSupportStationarySlot Slot) nearestLanding = slots
            .Select(slot =>
            {
                long dx = slot.Point.X - landing.Landing.X;
                long dy = slot.Point.Y - landing.Landing.Y;
                return (checked((dx * dx) + (dy * dy)), slot);
            })
            .OrderBy(item => item.Item1)
            .First();
        (long DistanceSquared, int GapRaw, Id65RewardSupportStationarySlot Slot,
            Id65RewardSupportDragonPrecinct Dragon) nearestDragon = slots
            .SelectMany(slot => dragons.Select(dragon =>
            {
                int dx = DistanceFromCoordinateToInterval(
                    slot.Point.X,
                    dragon.PrecinctEnvelope.Minimum.X,
                    dragon.PrecinctEnvelope.Maximum.X);
                int dy = DistanceFromCoordinateToInterval(
                    slot.Point.Y,
                    dragon.PrecinctEnvelope.Minimum.Y,
                    dragon.PrecinctEnvelope.Maximum.Y);
                long squared = checked(((long)dx * dx) + ((long)dy * dy));
                int raw = squared == (long)Math.Max(dx, dy) * Math.Max(dx, dy)
                    ? Math.Max(dx, dy)
                    : -1;
                return (squared, raw, slot, dragon);
            }))
            .OrderBy(item => item.squared)
            .First();
        int stationaryOuter = slots.Min(slot => new[]
        {
            slot.Point.X - coreEnvelope.Minimum.X,
            coreEnvelope.Maximum.X - slot.Point.X,
            slot.Point.Y - coreEnvelope.Minimum.Y,
            coreEnvelope.Maximum.Y - slot.Point.Y
        }.Min());
        int stationarySeam = slots.Min(slot => Math.Min(
            Math.Abs(slot.Point.X - Id65RewardSupportCoordinates.AuthoringTranslationRaw),
            Math.Abs(slot.Point.Y - Id65RewardSupportCoordinates.AuthoringTranslationRaw)));
        bool outsideApron = slots.All(slot => !ContainsXy(landing.ApronMinimum, landing.ApronMaximum, slot.Point));
        bool outsideDragons = slots.All(slot =>
            dragons.All(dragon => !dragon.PrecinctEnvelope.ContainsXy(slot.Point)));
        return new(
            nearestLanding.Slot.SlotIndex,
            nearestLanding.DistanceSquared,
            Math.Round(
                (decimal)Math.Sqrt(nearestLanding.DistanceSquared) /
                    Id65RewardSupportCoordinates.RawUnitsPerWorldUnit,
                2,
                MidpointRounding.AwayFromZero),
            nearestDragon.Slot.SlotIndex,
            nearestDragon.Dragon.Id,
            nearestDragon.GapRaw,
            nearestDragon.GapRaw /
                (decimal)Id65RewardSupportCoordinates.RawUnitsPerWorldUnit,
            stationaryOuter,
            stationarySeam,
            dragons.Min(item => item.OuterEdgeMinimumMarginRaw),
            dragons.Min(item => item.SectorSeamMinimumMarginRaw),
            outsideApron,
            outsideDragons,
            separations.Count == 6 && separations.All(item => item.DistanceSquaredRaw > 0));
    }

    private static int DistanceFromIntervalToCoordinate(int minimum, int maximum, int coordinate) =>
        coordinate < minimum ? minimum - coordinate :
        coordinate > maximum ? coordinate - maximum : 0;

    private static int DistanceFromCoordinateToInterval(int coordinate, int minimum, int maximum) =>
        coordinate < minimum ? minimum - coordinate :
        coordinate > maximum ? coordinate - maximum : 0;

    private static bool ContainsXy(
        Id65RewardSupportRawPoint minimum,
        Id65RewardSupportRawPoint maximum,
        Id65RewardSupportRawPoint point) =>
        point.X >= minimum.X && point.X <= maximum.X &&
        point.Y >= minimum.Y && point.Y <= maximum.Y;

    private static Id65RewardSupportRawPoint AddRelative(
        Id65RewardSupportRawPoint origin,
        Id65RewardSupportRawPoint relative) =>
        new(
            checked(origin.X + relative.X),
            checked(origin.Y + relative.Y),
            checked(origin.Z + relative.Z));

    private static Id65RewardSupportEnvelope BuildEnvelope(
        string id,
        int minimumWorldX,
        int minimumWorldY,
        int maximumWorldX,
        int maximumWorldY) =>
        new(
            id,
            new(
                checked(minimumWorldX * Id65RewardSupportCoordinates.RawUnitsPerWorldUnit),
                checked(minimumWorldY * Id65RewardSupportCoordinates.RawUnitsPerWorldUnit),
                Id65RewardSupportCoordinates.GroundRawZ),
            new(
                checked(maximumWorldX * Id65RewardSupportCoordinates.RawUnitsPerWorldUnit),
                checked(maximumWorldY * Id65RewardSupportCoordinates.RawUnitsPerWorldUnit),
                Id65RewardSupportCoordinates.GroundRawZ),
            checked((maximumWorldX - minimumWorldX) *
                Id65RewardSupportCoordinates.RawUnitsPerWorldUnit),
            checked((maximumWorldY - minimumWorldY) *
                Id65RewardSupportCoordinates.RawUnitsPerWorldUnit));

    private static Id65RewardSupportLandingApron BuildLandingApron(
        Id65RewardSupportEnvelope coreEnvelope)
    {
        Id65RewardSupportRawPoint landing = new(6_153, 6_154, 8_550);
        Id65RewardSupportRawPoint player = new(6_153, 6_154, 8_704);
        const int halfExtentWorld = 32;
        const int halfExtentRaw = halfExtentWorld * Id65RewardSupportCoordinates.RawUnitsPerWorldUnit;
        Id65RewardSupportRawPoint minimum = new(
            landing.X - halfExtentRaw,
            landing.Y - halfExtentRaw,
            Id65RewardSupportCoordinates.GroundRawZ);
        Id65RewardSupportRawPoint maximum = new(
            landing.X + halfExtentRaw,
            landing.Y + halfExtentRaw,
            Id65RewardSupportCoordinates.GroundRawZ);
        int edgeMargin = new[]
        {
            landing.X - coreEnvelope.Minimum.X,
            coreEnvelope.Maximum.X - landing.X,
            landing.Y - coreEnvelope.Minimum.Y,
            coreEnvelope.Maximum.Y - landing.Y
        }.Min();
        return new(
            LandingRecordId: "spawn.remote-pad",
            PlayerAnchorId: "moby.player-anchor.t92",
            PlayerAnchorTrueIndex: 92,
            landing,
            player,
            minimum,
            maximum,
            halfExtentWorld,
            halfExtentRaw,
            edgeMargin,
            LandingAndPlayerXyUnchanged: true,
            EntireApronInsideCore4: coreEnvelope.ContainsXy(minimum) && coreEnvelope.ContainsXy(maximum));
    }

    private static Id65RewardSupportStationarySlot[] BuildStationarySlots(
        Id65RewardSupportEnvelope coreEnvelope)
    {
        Id65RewardSupportStationarySlot[] slots = Enumerable.Range(0, StationarySlotCount)
            .Select(index =>
            {
                int row = index / StationaryColumns;
                int column = index % StationaryColumns;
                Id65RewardSupportRawPoint point = new(
                    5_584 + (1_152 * column),
                    11_344 - (1_152 * row),
                    Id65RewardSupportCoordinates.GroundRawZ);
                return new Id65RewardSupportStationarySlot(
                    index, row, column, point, Spare: index == StationarySlotCount - 1);
            })
            .ToArray();
        if (slots.Any(slot => !coreEnvelope.ContainsXy(slot.Point)))
            throw new InvalidDataException("A stationary lattice slot escaped the Core4 envelope.");
        return slots;
    }

    private static Id65RewardSupportDragonPrecinct[] BuildDragonPrecincts(
        IReadOnlyList<Id65RewardSupportDragonIdentity> source,
        Id65RewardSupportEnvelope coreEnvelope)
    {
        Dictionary<int, Id65RewardSupportDragonIdentity> byDragon =
            source.ToDictionary(item => item.DragonTrueIndex);
        Id65RewardSupportDragonPrecinct[] result = DragonSpecs.Select(spec =>
        {
            Id65RewardSupportDragonIdentity identity = byDragon[spec.DragonTrueIndex];
            Id65RewardSupportEnvelope envelope = new(
                $"{spec.Id}-precinct-envelope",
                spec.EnvelopeMinimum with { },
                spec.EnvelopeMaximum with { },
                spec.EnvelopeMaximum.X - spec.EnvelopeMinimum.X,
                spec.EnvelopeMaximum.Y - spec.EnvelopeMinimum.Y);
            Id65RewardSupportRawPoint localMinimum =
                Id65RewardSupportCoordinates.SceneToLocal(spec.EnvelopeMinimum);
            Id65RewardSupportRawPoint localMaximum =
                Id65RewardSupportCoordinates.SceneToLocal(spec.EnvelopeMaximum);
            Id65RewardSupportEnvelope platformLocalEnvelope = new(
                $"{spec.Id}-platform-local-envelope",
                localMinimum,
                localMaximum,
                localMaximum.X - localMinimum.X,
                localMaximum.Y - localMinimum.Y);
            Id65RewardSupportRawPoint actorRelativeMinimum = new(
                spec.EnvelopeMinimum.X - spec.Actor.X,
                spec.EnvelopeMinimum.Y - spec.Actor.Y,
                spec.EnvelopeMinimum.Z - spec.Actor.Z);
            Id65RewardSupportRawPoint actorRelativeMaximum = new(
                spec.EnvelopeMaximum.X - spec.Actor.X,
                spec.EnvelopeMaximum.Y - spec.Actor.Y,
                spec.EnvelopeMaximum.Z - spec.Actor.Z);
            Id65RewardSupportEnvelope actorRelativeEnvelope = new(
                $"{spec.Id}-actor-relative-envelope",
                actorRelativeMinimum,
                actorRelativeMaximum,
                actorRelativeMaximum.X - actorRelativeMinimum.X,
                actorRelativeMaximum.Y - actorRelativeMinimum.Y);
            int outerEdgeMargin = new[]
            {
                envelope.Minimum.X - coreEnvelope.Minimum.X,
                coreEnvelope.Maximum.X - envelope.Maximum.X,
                envelope.Minimum.Y - coreEnvelope.Minimum.Y,
                coreEnvelope.Maximum.Y - envelope.Maximum.Y
            }.Min();
            int seamMargin = Math.Min(
                DistanceFromIntervalToCoordinate(
                    envelope.Minimum.X, envelope.Maximum.X,
                    Id65RewardSupportCoordinates.AuthoringTranslationRaw),
                DistanceFromIntervalToCoordinate(
                    envelope.Minimum.Y, envelope.Maximum.Y,
                    Id65RewardSupportCoordinates.AuthoringTranslationRaw));
            return new Id65RewardSupportDragonPrecinct(
                spec.Id,
                identity,
                spec.Pedestal with { },
                spec.Actor with { },
                spec.Control with { },
                envelope,
                platformLocalEnvelope,
                actorRelativeEnvelope,
                outerEdgeMargin,
                seamMargin,
                DesiredSupportPinned: true,
                CameraRuntimeVerified: false,
                RouteRuntimeVerified: false,
                DestinationRuntimeVerified: false);
        }).ToArray();
        if (result.Any(item =>
            !coreEnvelope.ContainsXy(item.Pedestal) ||
            !coreEnvelope.ContainsXy(item.Actor) ||
            !coreEnvelope.ContainsXy(item.ControlDestination) ||
            !coreEnvelope.ContainsXy(item.PrecinctEnvelope.Minimum) ||
            !coreEnvelope.ContainsXy(item.PrecinctEnvelope.Maximum)))
        {
            throw new InvalidDataException("A dragon precinct escaped the Core4 envelope.");
        }
        return result;
    }

    private static Id65RewardSupportEnemyReservation[] BuildEnemyReservations(
        IReadOnlyList<Id65RewardSupportOwnerIdentity> source,
        Id65RewardSupportEnvelope enemyBayEnvelope,
        Id65RewardSupportEnvelope fullEnvelope)
    {
        Dictionary<int, Id65RewardSupportOwnerIdentity> byIndex =
            source.ToDictionary(item => item.TrueIndex);
        Id65RewardSupportEnemyReservation[] result = EnemySpecs.Select(spec =>
        {
            Id65RewardSupportOwnerIdentity owner = byIndex[spec.TrueIndex];
            Id65RewardOwnerSemantics required = spec.Kind == Id65RewardSupportReservationKind.Bull
                ? Id65RewardOwnerSemantics.BullEnemy
                : Id65RewardOwnerSemantics.TorroEnemy;
            if (owner.OwnerSemantics != required)
                throw new InvalidDataException($"Enemy reservation {spec.Id} changed owner semantics.");
            return new Id65RewardSupportEnemyReservation(
                spec.Id,
                spec.Kind,
                owner,
                new(spec.RawX, spec.RawY, Id65RewardSupportCoordinates.GroundRawZ),
                ReservationOnly: true,
                RoutePinned: false,
                ActivationBoundsPinned: false,
                RuntimeAccepted: false);
        }).ToArray();
        if (result.Any(item =>
            !enemyBayEnvelope.ContainsXy(item.Point) || !fullEnvelope.ContainsXy(item.Point)))
        {
            throw new InvalidDataException("An enemy reservation escaped the Full8 enemy-bay envelope.");
        }
        return result;
    }

    private static Id65RewardSupportRejectedClaim[] BuildRejectedClaims() =>
    [
        new("legacy-448x480-placement", true,
            "The rejected 448x480 destination is not evidence for support, route, or camera safety."),
        new("core4-total-200", true,
            "Core4 contains only 71 stationary owner rows totaling 171."),
        new("full8-total-200-runtime-acceptance", true,
            "Full8 reaches 200 only as 171 stationary capacity plus 29 mobile-enemy reservations."),
        new("mobile-enemy-route-acceptance", true,
            "Bull and Torro route placement has not been pinned or run."),
        new("mobile-enemy-activation-acceptance", true,
            "Bull and Torro activation bounds have not been pinned or run."),
        new("chest-transient-child-acceptance", true,
            "Transient chest-child duplicate and retirement-bit behavior is unverified."),
        new("dragon-camera-runtime-acceptance", true,
            "The four desired dragon precincts do not prove camera, route, or cutscene runtime behavior."),
        new("runtime-safe", true,
            "No emulator collection, death, re-entry, or save matrix has passed for this desired state."),
        new("row80-or-filesystem-writer", true,
            "This contract has no row-80 bytes, patch plan, path, BIN, CUE, or filesystem writer."),
        new("normal-create-bin-release-promotion", true,
            "App integration, normal Create BIN, promotion, and release remain unauthorized.")
    ];

    private static void RequireEnvelope(
        Id65RewardSupportEnvelope actual,
        string id,
        int minimumRawX,
        int minimumRawY,
        int maximumRawX,
        int maximumRawY,
        int widthRaw,
        int heightRaw)
    {
        if (actual.Id != id || actual.Minimum.X != minimumRawX ||
            actual.Minimum.Y != minimumRawY ||
            actual.Minimum.Z != Id65RewardSupportCoordinates.GroundRawZ ||
            actual.Maximum.X != maximumRawX || actual.Maximum.Y != maximumRawY ||
            actual.Maximum.Z != Id65RewardSupportCoordinates.GroundRawZ ||
            actual.WidthRaw != widthRaw || actual.HeightRaw != heightRaw)
        {
            throw new InvalidDataException($"Support envelope {id} changed.");
        }
    }

    private static void RequireLanding(
        Id65RewardSupportLandingApron actual,
        Id65RewardSupportEnvelope coreEnvelope)
    {
        if (actual.LandingRecordId != "spawn.remote-pad" ||
            actual.PlayerAnchorId != "moby.player-anchor.t92" ||
            actual.PlayerAnchorTrueIndex != 92 ||
            actual.Landing != new Id65RewardSupportRawPoint(6_153, 6_154, 8_550) ||
            actual.PlayerAnchor != new Id65RewardSupportRawPoint(6_153, 6_154, 8_704) ||
            actual.ApronMinimum != new Id65RewardSupportRawPoint(5_641, 5_642, 8_192) ||
            actual.ApronMaximum != new Id65RewardSupportRawPoint(6_665, 6_666, 8_192) ||
            actual.ApronHalfExtentWorld != 32 || actual.ApronHalfExtentRaw != 512 ||
            actual.MinimumLandingEdgeMarginRaw != 1_801 ||
            actual.MinimumLandingEdgeMarginRaw <= 112 * 16 ||
            !actual.LandingAndPlayerXyUnchanged || !actual.EntireApronInsideCore4 ||
            actual.Landing.Z - Id65RewardSupportCoordinates.GroundRawZ != 358 ||
            actual.PlayerAnchor.Z - Id65RewardSupportCoordinates.GroundRawZ != 512 ||
            actual.PlayerAnchor.Z - actual.Landing.Z != 154 ||
            !coreEnvelope.ContainsXy(actual.ApronMinimum) ||
            !coreEnvelope.ContainsXy(actual.ApronMaximum))
        {
            throw new InvalidDataException("The unchanged v2 landing/T92 32-world apron changed.");
        }
    }

    private static void RequireStationarySlots(
        IReadOnlyList<Id65RewardSupportStationarySlot> slots,
        Id65RewardSupportEnvelope coreEnvelope)
    {
        if (slots.Count != StationarySlotCount || slots.Count(item => item.Spare) != 1 ||
            !slots[StationarySlotCount - 1].Spare ||
            slots.Select(item => (item.Point.X, item.Point.Y)).Distinct().Count() != StationarySlotCount)
        {
            throw new InvalidDataException("The deterministic 72-slot stationary lattice changed.");
        }
        for (int index = 0; index < slots.Count; index++)
        {
            Id65RewardSupportStationarySlot slot = slots[index];
            int row = index / StationaryColumns;
            int column = index % StationaryColumns;
            if (slot.SlotIndex != index || slot.Row != row || slot.Column != column ||
                slot.Point != new Id65RewardSupportRawPoint(
                    5_584 + (1_152 * column),
                    11_344 - (1_152 * row),
                    8_192) || !coreEnvelope.ContainsXy(slot.Point))
            {
                throw new InvalidDataException($"Stationary lattice slot {index} changed.");
            }
        }
    }

    private static void RequireDragons(
        IReadOnlyList<Id65RewardSupportDragonPrecinct> dragons,
        Id65RewardSupportEnvelope coreEnvelope)
    {
        if (dragons.Count != DragonSpecs.Length)
            throw new InvalidDataException("The four dragon precincts changed.");
        for (int index = 0; index < DragonSpecs.Length; index++)
        {
            DragonLayoutSpec expected = DragonSpecs[index];
            Id65RewardSupportDragonPrecinct actual = dragons[index];
            if (actual.Id != expected.Id ||
                actual.Dragon.PedestalTrueIndex != expected.PedestalTrueIndex ||
                actual.Dragon.DragonTrueIndex != expected.DragonTrueIndex ||
                actual.Dragon.ContainerTrueIndex != expected.ContainerTrueIndex ||
                actual.Pedestal != expected.Pedestal || actual.Actor != expected.Actor ||
                actual.ControlDestination != expected.Control ||
                actual.PrecinctEnvelope.Minimum != expected.EnvelopeMinimum ||
                actual.PrecinctEnvelope.Maximum != expected.EnvelopeMaximum ||
                actual.PrecinctEnvelope.WidthRaw != expected.EnvelopeMaximum.X - expected.EnvelopeMinimum.X ||
                actual.PrecinctEnvelope.HeightRaw != expected.EnvelopeMaximum.Y - expected.EnvelopeMinimum.Y ||
                Id65RewardSupportCoordinates.LocalToScene(actual.PlatformLocalEnvelope.Minimum) !=
                    expected.EnvelopeMinimum ||
                Id65RewardSupportCoordinates.LocalToScene(actual.PlatformLocalEnvelope.Maximum) !=
                    expected.EnvelopeMaximum ||
                AddRelative(actual.Actor, actual.ActorRelativeEnvelope.Minimum) !=
                    expected.EnvelopeMinimum ||
                AddRelative(actual.Actor, actual.ActorRelativeEnvelope.Maximum) !=
                    expected.EnvelopeMaximum ||
                !actual.DesiredSupportPinned || actual.CameraRuntimeVerified ||
                actual.RouteRuntimeVerified || actual.DestinationRuntimeVerified ||
                !actual.PrecinctEnvelope.ContainsXy(actual.Pedestal) ||
                !actual.PrecinctEnvelope.ContainsXy(actual.Actor) ||
                !actual.PrecinctEnvelope.ContainsXy(actual.ControlDestination) ||
                !coreEnvelope.ContainsXy(actual.PrecinctEnvelope.Minimum) ||
                !coreEnvelope.ContainsXy(actual.PrecinctEnvelope.Maximum))
            {
                throw new InvalidDataException($"Dragon precinct {expected.Id} readback changed.");
            }
        }
    }

    private static void RequireDragonSeparations(
        IReadOnlyList<Id65RewardSupportDragonSeparation> actual)
    {
        Id65RewardSupportDragonSeparation[] expected =
        [
            new("dragon-a", "dragon-b", 2_901, 788, 9_036_745),
            new("dragon-a", "dragon-c", 0, 771, 594_441),
            new("dragon-a", "dragon-d", 3_448, 0, 11_888_704),
            new("dragon-b", "dragon-c", 2_560, 0, 6_553_600),
            new("dragon-b", "dragon-d", 0, 788, 620_944),
            new("dragon-c", "dragon-d", 3_107, 771, 10_247_890)
        ];
        if (!actual.SequenceEqual(expected) || actual.Any(item => item.DistanceSquaredRaw <= 0))
            throw new InvalidDataException("The exact pairwise dragon-envelope separation changed.");
    }

    private static void RequireStaticSpacing(Id65RewardSupportStaticSpacing actual)
    {
        if (actual != new Id65RewardSupportStaticSpacing(
            NearestLandingSlotIndex: 60,
            NearestLandingSlotDistanceSquaredRaw: 648_661,
            NearestLandingSlotDistanceWorldRounded2: 50.34m,
            NearestStationaryDragonSlotIndex: 8,
            NearestStationaryDragonId: "dragon-d",
            NearestStationaryDragonGapRaw: 1_207,
            NearestStationaryDragonGapWorld: 75.4375m,
            StationaryMinimumOuterEdgeMarginRaw: 1_232,
            StationaryMinimumSectorSeamMarginRaw: 464,
            DragonMinimumOuterEdgeMarginRaw: 564,
            DragonMinimumSectorSeamMarginRaw: 519,
            EveryStationarySlotOutsideLandingApron: true,
            EveryStationarySlotOutsideDragonEnvelopes: true,
            DragonEnvelopesPairwiseSeparated: true))
        {
            throw new InvalidDataException(
                "The exact landing/lattice/dragon edge-and-seam spacing proof changed.");
        }
    }

    private static void RequireEnemies(
        IReadOnlyList<Id65RewardSupportEnemyReservation> enemies,
        Id65RewardSupportEnvelope enemyBayEnvelope,
        Id65RewardSupportEnvelope fullEnvelope)
    {
        if (enemies.Count != EnemySpecs.Length ||
            enemies.Sum(item => item.Owner.RewardValue) != EnemyRewardTotal)
        {
            throw new InvalidDataException("The ten enemy reservations or 29-capacity total changed.");
        }
        for (int index = 0; index < EnemySpecs.Length; index++)
        {
            EnemyLayoutSpec expected = EnemySpecs[index];
            Id65RewardSupportEnemyReservation actual = enemies[index];
            if (actual.Id != expected.Id || actual.Kind != expected.Kind ||
                actual.Owner.TrueIndex != expected.TrueIndex ||
                actual.Point != new Id65RewardSupportRawPoint(
                    expected.RawX, expected.RawY, Id65RewardSupportCoordinates.GroundRawZ) ||
                !actual.ReservationOnly || actual.RoutePinned ||
                actual.ActivationBoundsPinned || actual.RuntimeAccepted ||
                !enemyBayEnvelope.ContainsXy(actual.Point) || !fullEnvelope.ContainsXy(actual.Point))
            {
                throw new InvalidDataException($"Enemy reservation {expected.Id} readback changed.");
            }
        }
    }

    private static string ComputeDeterministicHash(
        Id65RewardSupportSourceIdentity source,
        IReadOnlyList<Id65RewardSupportProfile> profiles,
        Id65RewardSupportEnvelope coreEnvelope,
        Id65RewardSupportEnvelope fullEnvelope,
        Id65RewardSupportEnvelope enemyBayEnvelope,
        Id65RewardSupportLandingApron landing,
        IReadOnlyList<Id65RewardSupportStationarySlot> slots,
        IReadOnlyList<Id65RewardSupportStationaryPlacement> placements,
        IReadOnlyList<Id65RewardSupportDragonPrecinct> dragons,
        IReadOnlyList<Id65RewardSupportDragonSeparation> dragonSeparations,
        IReadOnlyList<Id65RewardSupportEnemyReservation> enemies,
        Id65RewardSupportZeroEggBoundary zeroEgg,
        Id65RewardSupportRuntimeGates gates,
        Id65RewardSupportStaticSpacing staticSpacing,
        IReadOnlyList<Id65RewardSupportRejectedClaim> rejectedClaims)
    {
        StringBuilder value = new();
        Add(ProfileId);
        Add(source.CensusProfileId);
        Add(source.LockedSourceImageSha256);
        Add(source.DeterministicCensusSha256);
        Add(source.RewardRowsSha256);
        Add($"transform|{Id65RewardSupportCoordinates.AuthoringTranslationRaw}|{Id65RewardSupportCoordinates.GroundRawZ}");
        AddEnvelope(coreEnvelope);
        AddEnvelope(fullEnvelope);
        AddEnvelope(enemyBayEnvelope);
        foreach (Id65RewardSupportProfile profile in profiles)
        {
            Add($"profile|{profile.Id}|{profile.TerrainSupportProfileId}|{profile.TerrainSupportCanonicalSha256}|{profile.Kind}|{profile.TerrainSectorCount}|" +
                $"{profile.StationaryOwnerCapacity}|{profile.StationaryRewardCapacity}|" +
                $"{profile.MobileEnemyReservationCapacity}|{profile.MobileEnemyRewardCapacity}|" +
                $"{profile.CombinedOwnerCapacity}|{profile.CombinedRewardCapacity}|" +
                $"{B(profile.CapacityOnly)}|{B(profile.Total200RuntimeAccepted)}|{B(profile.RuntimeSafe)}|{B(profile.PromotionAuthorized)}");
        }
        Add($"landing|{landing.LandingRecordId}|{Point(landing.Landing)}|player|{landing.PlayerAnchorId}|T{landing.PlayerAnchorTrueIndex}|{Point(landing.PlayerAnchor)}|" +
            $"apron|{Point(landing.ApronMinimum)}|{Point(landing.ApronMaximum)}|{landing.MinimumLandingEdgeMarginRaw}");
        foreach (Id65RewardSupportStationarySlot slot in slots)
            Add($"slot|{slot.SlotIndex}|{slot.Row}|{slot.Column}|{Point(slot.Point)}|{B(slot.Spare)}");
        foreach (Id65RewardSupportStationaryPlacement placement in placements)
        {
            Id65RewardSupportOwnerIdentity owner = placement.Owner;
            Add($"stationary|{placement.Slot.SlotIndex}|T{owner.TrueIndex}|{owner.ActorClass:X4}|" +
                $"{owner.OwnerSemantics}|{owner.Encoding}|{owner.RewardValue}|{owner.RowSha256}|" +
                $"{owner.PropertiesSceneOffset:X}|{owner.PropertiesByteLength:X}|{owner.PropertiesSha256}|" +
                $"{owner.RetirementByteIndex}|{owner.RetirementBitMask:X2}|{B(owner.TransientRewardChildRuntimeProofVerified)}");
        }
        foreach (Id65RewardSupportDragonPrecinct dragon in dragons)
        {
            Add($"dragon|{dragon.Id}|{dragon.Dragon.PedestalTrueIndex}|{dragon.Dragon.DragonTrueIndex}|" +
                $"{dragon.Dragon.ContainerTrueIndex}|{dragon.Dragon.ThreeRowBundleSha256}|" +
                $"{dragon.Dragon.CameraDataSha256}|{dragon.Dragon.SceneLinkSha256}|" +
                $"{dragon.Dragon.CameraTrackSha256}|{dragon.Dragon.CutsceneIndex}|{dragon.Dragon.DragonNameIndex}|" +
                $"{Point(dragon.Pedestal)}|{Point(dragon.Actor)}|{Point(dragon.ControlDestination)}|" +
                $"{Point(dragon.PrecinctEnvelope.Minimum)}|{Point(dragon.PrecinctEnvelope.Maximum)}|" +
                $"{Point(dragon.PlatformLocalEnvelope.Minimum)}|{Point(dragon.PlatformLocalEnvelope.Maximum)}|" +
                $"{Point(dragon.ActorRelativeEnvelope.Minimum)}|{Point(dragon.ActorRelativeEnvelope.Maximum)}|" +
                $"{dragon.OuterEdgeMinimumMarginRaw}|{dragon.SectorSeamMinimumMarginRaw}|false|false|false");
        }
        foreach (Id65RewardSupportDragonSeparation separation in dragonSeparations)
            Add($"dragon-gap|{separation.FirstDragonId}|{separation.SecondDragonId}|{separation.GapXRaw}|{separation.GapYRaw}|{separation.DistanceSquaredRaw}");
        foreach (Id65RewardSupportEnemyReservation enemy in enemies)
        {
            Id65RewardSupportOwnerIdentity owner = enemy.Owner;
            Add($"enemy|{enemy.Id}|{enemy.Kind}|T{enemy.Owner.TrueIndex}|{enemy.Owner.ActorClass:X4}|" +
                $"{owner.OwnerSemantics}|{owner.Encoding}|{owner.RewardValue}|{owner.RowSha256}|" +
                $"{owner.PropertiesSceneOffset:X}|{owner.PropertiesByteLength:X}|{owner.PropertiesSha256}|" +
                $"{owner.RetirementByteIndex}|{owner.RetirementBitMask:X2}|{B(owner.TransientRewardChildRuntimeProofVerified)}|" +
                $"{Point(enemy.Point)}|true|false|false|false");
        }
        Add($"zero-egg|{zeroEgg.RequiredEggTarget}|T{zeroEgg.EggThiefTrueIndex}|" +
            $"{zeroEgg.EggThiefClass:X4}|{zeroEgg.CarriedEggClass:X2}|{zeroEgg.EggThiefRowSha256}|" +
            $"{zeroEgg.EggThiefPropertiesSha256}|{B(zeroEgg.ReplacementRequired)}|{B(zeroEgg.WriterAuthorized)}");
        Add($"gates|{B(gates.Core4StationaryCollectionVerified)}|{B(gates.DragonCameraPlacementVerified)}|" +
            $"{B(gates.DragonRoutePlacementVerified)}|{B(gates.MobileEnemyRoutesVerified)}|" +
            $"{B(gates.MobileEnemyActivationBoundsVerified)}|{B(gates.ChestTransientChildDuplicateAndBitVerified)}|" +
            $"{B(gates.CollectionRuntimeVerified)}|{B(gates.DeathRuntimeVerified)}|{B(gates.ReentryRuntimeVerified)}|" +
            $"{B(gates.MemoryCardMatrixVerified)}|{B(gates.Total200RuntimeAccepted)}|{B(gates.RuntimeSafetyAuthorized)}");
        Add($"spacing|{staticSpacing.NearestLandingSlotIndex}|{staticSpacing.NearestLandingSlotDistanceSquaredRaw}|" +
            $"{staticSpacing.NearestLandingSlotDistanceWorldRounded2.ToString(CultureInfo.InvariantCulture)}|" +
            $"{staticSpacing.NearestStationaryDragonSlotIndex}|{staticSpacing.NearestStationaryDragonId}|" +
            $"{staticSpacing.NearestStationaryDragonGapRaw}|{staticSpacing.NearestStationaryDragonGapWorld.ToString(CultureInfo.InvariantCulture)}|" +
            $"{staticSpacing.StationaryMinimumOuterEdgeMarginRaw}|{staticSpacing.StationaryMinimumSectorSeamMarginRaw}|" +
            $"{staticSpacing.DragonMinimumOuterEdgeMarginRaw}|{staticSpacing.DragonMinimumSectorSeamMarginRaw}|" +
            $"{B(staticSpacing.EveryStationarySlotOutsideLandingApron)}|" +
            $"{B(staticSpacing.EveryStationarySlotOutsideDragonEnvelopes)}|{B(staticSpacing.DragonEnvelopesPairwiseSeparated)}");
        foreach (Id65RewardSupportRejectedClaim rejected in rejectedClaims)
            Add($"reject|{rejected.Id}|{B(rejected.Rejected)}|{rejected.Reason}");
        Add("outputs|row80=false|filesystem=false|bin=false|cue=false|writer=false|publisher=false|app=false|create-bin=false|runtime=false|promotion=false|release=false");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.ToString())))
            .ToLowerInvariant();

        void Add(object item) => value.Append(item).Append('\n');
        void AddEnvelope(Id65RewardSupportEnvelope item) =>
            Add($"envelope|{item.Id}|{Point(item.Minimum)}|{Point(item.Maximum)}|{item.WidthRaw}|{item.HeightRaw}");
        static string Point(Id65RewardSupportRawPoint point) =>
            string.Create(CultureInfo.InvariantCulture, $"{point.X},{point.Y},{point.Z}");
        static int B(bool flag) => flag ? 1 : 0;
    }

    private sealed record CanonicalSource(
        Id65RewardSupportSourceIdentity Source,
        IReadOnlyList<Id65RewardSupportOwnerIdentity> StationaryOwners,
        IReadOnlyList<Id65RewardSupportOwnerIdentity> EnemyOwners,
        IReadOnlyList<Id65RewardSupportDragonIdentity> Dragons,
        Id65RewardSupportZeroEggBoundary ZeroEgg);

    private sealed record DragonLayoutSpec(
        string Id,
        int PedestalTrueIndex,
        int DragonTrueIndex,
        int ContainerTrueIndex,
        Id65RewardSupportRawPoint Pedestal,
        Id65RewardSupportRawPoint Actor,
        Id65RewardSupportRawPoint Control,
        Id65RewardSupportRawPoint EnvelopeMinimum,
        Id65RewardSupportRawPoint EnvelopeMaximum);

    private sealed record EnemyLayoutSpec(
        string Id,
        int TrueIndex,
        Id65RewardSupportReservationKind Kind,
        int RawX,
        int RawY);
}
