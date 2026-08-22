using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

internal enum Id65RewardStagingProfileKind
{
    Core4Stationary171,
    Full8ReservationsOnly
}

internal sealed record Id65RewardStagingCapacity(
    int Row80ByteLength = 0x2E2000,
    int ModelByteCapacity = 0x94800,
    int MaximumObjectCount = 107,
    int MaximumActorRootCount = 38,
    int MaximumFixupCount = 0x81,
    int ActorSubfileEnd = 0x1D0000,
    int SceneSubfileEnd = 0x8800,
    int RetirementOwnerMaximumTrueIndex = 255,
    int CameraTrackEnd = 0x2E1FE8);

internal sealed record Id65RewardStagingStationaryIntent(
    int SlotIndex,
    int TrueIndex,
    ushort ActorClass,
    Id65RewardEncoding Encoding,
    Id65RewardOwnerSemantics OwnerSemantics,
    int RewardValue,
    string SourceRowSha256,
    uint PropertiesSceneOffset,
    int PropertiesByteLength,
    string PropertiesSha256,
    int RetirementByteIndex,
    byte RetirementBitMask,
    Id65RewardSupportRawPoint Point);

internal sealed record Id65RewardStagingDragonIntent(
    string Id,
    int PedestalTrueIndex,
    int DragonTrueIndex,
    int ContainerTrueIndex,
    string PedestalSourceRowSha256,
    string DragonSourceRowSha256,
    string ContainerSourceRowSha256,
    string SourceBundleSha256,
    Id65RewardSupportRawPoint Pedestal,
    Id65RewardSupportRawPoint Actor,
    Id65RewardSupportRawPoint ControlDestination,
    Id65RewardSupportRawPoint PrecinctMinimum,
    Id65RewardSupportRawPoint PrecinctMaximum,
    Id65RewardSupportRawPoint Translation,
    int CameraDataRelativeOffset,
    int CameraDataByteLength,
    string SourceCameraDataSha256,
    int SceneLinkRelativeOffset,
    int SceneLinkByteLength,
    string SourceSceneLinkSha256,
    int CameraTrackRelativeOffset,
    int CameraTrackByteLength,
    int CameraTrackFrameCount,
    string SourceCameraTrackSha256,
    int CutsceneIndex,
    int DragonNameIndex,
    int RunToAngle,
    int RunToRadius,
    int RunToAuxiliary);

internal sealed record Id65RewardStagingMobileReservation(
    string Id,
    Id65RewardSupportReservationKind Kind,
    int TrueIndex,
    ushort ActorClass,
    int RewardValue,
    string SourceRowSha256,
    uint PropertiesSceneOffset,
    int PropertiesByteLength,
    string PropertiesSha256,
    Id65RewardSupportRawPoint Point,
    bool ReservationOnly,
    bool RoutePinned,
    bool ActivationBoundsPinned,
    bool RuntimeAccepted);

/// <summary>
/// Immutable desired-state manifest. It owns identities and coordinates only;
/// it never contains a compiled support/composite/Moby afterimage, a path, a
/// publisher, or a row-80 buffer.
/// </summary>
internal sealed class Id65RewardStagingManifest
{
    private readonly ReadOnlyCollection<Id65RewardStagingStationaryIntent> _stationary;
    private readonly ReadOnlyCollection<Id65RewardStagingDragonIntent> _dragons;
    private readonly ReadOnlyCollection<Id65RewardStagingMobileReservation> _mobileReservations;

    internal Id65RewardStagingManifest(
        Id65RewardStagingProfileKind kind,
        string censusSha256,
        string layoutSha256,
        string supportManifestSha256,
        string grassBundleSha256,
        IEnumerable<Id65RewardStagingStationaryIntent> stationary,
        IEnumerable<Id65RewardStagingDragonIntent> dragons,
        IEnumerable<Id65RewardStagingMobileReservation> mobileReservations,
        bool authorMobileEnemies,
        string? declaredCanonicalJson = null,
        string? declaredCanonicalSha256 = null)
    {
        Kind = kind;
        CensusSha256 = censusSha256;
        LayoutSha256 = layoutSha256;
        SupportManifestSha256 = supportManifestSha256;
        GrassBundleSha256 = grassBundleSha256;
        _stationary = Array.AsReadOnly(stationary.OrderBy(item => item.TrueIndex).ToArray());
        _dragons = Array.AsReadOnly(dragons.OrderBy(item => item.DragonTrueIndex).ToArray());
        _mobileReservations = Array.AsReadOnly(mobileReservations.OrderBy(item => item.TrueIndex).ToArray());
        AuthorMobileEnemies = authorMobileEnemies;
        string canonical = RecomputeCanonicalJson();
        CanonicalJson = declaredCanonicalJson ?? canonical;
        CanonicalSha256 = declaredCanonicalSha256 ??
            Id65AuthoringRewardStagingCompiler.Hash(Encoding.UTF8.GetBytes(CanonicalJson));
    }

    public string ProfileId => Id65AuthoringRewardStagingCompiler.ProfileId;
    public int SchemaVersion => 1;
    public Id65RewardStagingProfileKind Kind { get; }
    public string CensusSha256 { get; }
    public string LayoutSha256 { get; }
    public string SupportManifestSha256 { get; }
    public string GrassBundleSha256 { get; }
    public IReadOnlyList<Id65RewardStagingStationaryIntent> Stationary => _stationary;
    public IReadOnlyList<Id65RewardStagingDragonIntent> Dragons => _dragons;
    public IReadOnlyList<Id65RewardStagingMobileReservation> MobileReservations => _mobileReservations;
    public bool AuthorMobileEnemies { get; }
    public string CanonicalJson { get; }
    public string CanonicalSha256 { get; }
    public bool ContainsCompiledSupportAfterimage => false;
    public bool ContainsCompiledCompositeAfterimage => false;
    public bool ContainsCompiledMobyAfterimage => false;
    public bool ContainsPublisher => false;

    internal string RecomputeCanonicalJson() => JsonSerializer.Serialize(new
    {
        profileId = ProfileId,
        schemaVersion = SchemaVersion,
        kind = Kind.ToString(),
        censusSha256 = CensusSha256,
        layoutSha256 = LayoutSha256,
        supportManifestSha256 = SupportManifestSha256,
        grassBundleSha256 = GrassBundleSha256,
        stationary = _stationary,
        dragons = _dragons,
        mobileReservations = _mobileReservations,
        authorMobileEnemies = AuthorMobileEnemies
    }, Id65AuthoringRewardStagingCompiler.CanonicalJsonOptions);
}

internal sealed class Id65RewardStagingLockedSource
{
    private readonly byte[] _row80;
    private readonly byte[] _targetOverlay;
    private readonly byte[] _globalExecutable;
    private readonly Id65V2NativeTextureWitness _textureWitness;

    internal Id65RewardStagingLockedSource(
        byte[] row80,
        byte[] targetOverlay,
        byte[] globalExecutable,
        Id65V2NativeTextureWitness textureWitness)
    {
        _row80 = row80.ToArray();
        _targetOverlay = targetOverlay.ToArray();
        _globalExecutable = globalExecutable.ToArray();
        _textureWitness = textureWitness;
        Row80Sha256 = Id65AuthoringRewardStagingCompiler.Hash(_row80);
        TargetOverlaySha256 = Id65AuthoringRewardStagingCompiler.Hash(_targetOverlay);
        GlobalExecutableSha256 = Id65AuthoringRewardStagingCompiler.Hash(_globalExecutable);
        TextureWitnessSha256 = textureWitness.CanonicalSha256;
    }

    public int Row80ByteLength => _row80.Length;
    public int TargetOverlayByteLength => _targetOverlay.Length;
    public int GlobalExecutableByteLength => _globalExecutable.Length;
    public string Row80Sha256 { get; }
    public string TargetOverlaySha256 { get; }
    public string GlobalExecutableSha256 { get; }
    public string TextureWitnessSha256 { get; }
    public bool ContainsPath => false;
    public bool ContainsCompiledSupportAfterimage => false;
    public bool ContainsCompiledCompositeAfterimage => false;
    public bool ContainsCompiledMobyAfterimage => false;
    public bool ContainsPublisher => false;

    internal byte[] CopyRow80() => _row80.ToArray();
    internal byte[] CopyTargetOverlay() => _targetOverlay.ToArray();
    internal byte[] CopyGlobalExecutable() => _globalExecutable.ToArray();
    internal Id65V2NativeTextureWitness TextureWitness => _textureWitness;
}

internal sealed record Id65RewardStagingCompileRequest(
    Id65RewardStagingLockedSource Source,
    Id65RewardStagingManifest Manifest,
    Id65RewardCensus Census,
    Id65RewardSupportLayout Layout,
    Id65AuthoringSupportManifest CoreSupportManifest,
    Id65AuthoringSupportManifest FullSupportManifest,
    Id65MobyDependencyBundleDescriptor GrassBundle,
    Id65RewardStagingCapacity? Capacity = null,
    bool AuthorMobileEnemies = false);

internal sealed class Id65RewardStagingOwnedRange
{
    private readonly byte[] _before;
    private readonly byte[] _after;

    internal Id65RewardStagingOwnedRange(
        string stableId,
        string ownerId,
        int dataRelativeOffset,
        byte[] before,
        byte[] after)
    {
        StableId = stableId;
        OwnerId = ownerId;
        DataRelativeOffset = dataRelativeOffset;
        _before = before.ToArray();
        _after = after.ToArray();
        BeforeSha256 = Id65AuthoringRewardStagingCompiler.Hash(_before);
        AfterSha256 = Id65AuthoringRewardStagingCompiler.Hash(_after);
    }

    public string StableId { get; }
    public string OwnerId { get; }
    public int DataRelativeOffset { get; }
    public int ByteLength => _before.Length;
    public string BeforeSha256 { get; }
    public string AfterSha256 { get; }
    internal byte[] CopyBefore() => _before.ToArray();
    internal byte[] CopyAfter() => _after.ToArray();
    internal Id65RewardStagingOwnedRange DeepCopy() =>
        new(StableId, OwnerId, DataRelativeOffset, _before, _after);
    internal Id65RewardStagingOwnedRange Invert() =>
        new(StableId, OwnerId, DataRelativeOffset, _after, _before);
}

internal sealed record Id65RewardStagingDiffRange(
    int DataRelativeOffset,
    int ByteLength,
    string OwnerStableId,
    string OwnerId,
    string BeforeSha256,
    string AfterSha256);

internal sealed record Id65RewardStagingStationaryReadback(
    int OwnerCount,
    int RewardTotal,
    int LooseGemOwnerCount,
    int LooseGemRewardTotal,
    int ChestCarrierOwnerCount,
    int ChestCarrierRewardTotal,
    string TrueIndexSequenceSha256,
    string SourceRowsSha256,
    string OutputRowsSha256,
    int PropertiesByteLength,
    string PropertiesSha256,
    bool OnlyXyzMutated,
    bool TrueIndexesPreserved,
    bool RetirementAddressesPreserved);

internal sealed record Id65RewardStagingDragonReadback(
    string Id,
    int PedestalTrueIndex,
    int DragonTrueIndex,
    int ContainerTrueIndex,
    string OutputPedestalRowSha256,
    string OutputDragonRowSha256,
    string OutputContainerRowSha256,
    string OutputBundleSha256,
    string OutputCameraDataSha256,
    string OutputCameraTrackSha256,
    int CameraTrackFrameCount,
    int OwnedByteCount,
    int ChangedByteCount,
    int DiffRangeCount,
    int RunToEndpointRawX,
    int RunToEndpointRawY,
    bool RunToEndpointInsideNamedPrecinct,
    bool RunToEndpointInsideCore4,
    bool UniformTranslationApplied,
    bool NonCoordinateBytesPreserved,
    bool SceneLinkPreserved);

internal sealed record Id65RewardStagingZeroEggReadback(
    int RequiredEggTarget,
    int ReplacedTrueIndex,
    ushort ActorClass,
    int ActorRootCountBefore,
    int ActorRootCountAfter,
    int ObjectCountBefore,
    int ObjectCountAfter,
    int FixupCountBefore,
    int FixupCountAfter,
    int ActorTailBytesRemaining,
    int SceneTailBytesRemaining,
    string OutputRowSha256,
    string ActorPackageSha256,
    string PropertiesSha256,
    string OrphanedPrivateBlockSha256,
    string ActiveFixupComponentSha256,
    bool OrphanedPrivateBlockPreserved,
    bool ActiveFixupsPreserved);

internal sealed record Id65RewardStagingStructuralReadback(
    string HeaderSha256,
    string ActorSubfileSha256,
    string SceneSubfileSha256,
    string ObjectTableSha256,
    string ModelSha256,
    string OutsideModelRowSha256,
    string OutsideTexturePagesAndModelRowSha256,
    int ObjectCount,
    int ActorRootCount,
    int FixupCount,
    int CoreSectorCount,
    int ActiveCollisionCellCount,
    int CollisionBlocksUsedBytes,
    int TextureCount,
    int HighestTextureId,
    bool OverlayPreserved,
    bool ExecutablePreserved);

internal sealed class Id65CompiledRewardStaging
{
    private readonly byte[] _sourceRow80;
    private readonly byte[] _outputRow80;
    private readonly byte[] _targetOverlay;
    private readonly byte[] _globalExecutable;
    private readonly ReadOnlyCollection<Id65RewardStagingOwnedRange> _ownedRanges;
    private readonly ReadOnlyCollection<Id65RewardStagingOwnedRange> _inverseRanges;
    private readonly ReadOnlyCollection<Id65RewardStagingDiffRange> _diffRanges;
    private readonly ReadOnlyCollection<Id65RewardStagingDragonReadback> _dragonReadback;

    internal Id65CompiledRewardStaging(
        Id65RewardStagingManifest manifest,
        byte[] sourceRow80,
        byte[] outputRow80,
        byte[] targetOverlay,
        byte[] globalExecutable,
        IEnumerable<Id65RewardStagingOwnedRange> ownedRanges,
        IEnumerable<Id65RewardStagingDiffRange> diffRanges,
        int changedByteCount,
        string diffManifestSha256,
        string ownedRangeMapSha256,
        string inverseRangeMapSha256,
        string transactionSha256,
        string protectedComplementSha256,
        string descriptorFullFieldSha256,
        string deterministicPlanSha256,
        Id65RewardStagingStationaryReadback stationary,
        IEnumerable<Id65RewardStagingDragonReadback> dragons,
        Id65RewardStagingZeroEggReadback zeroEgg,
        Id65RewardStagingStructuralReadback structure)
    {
        Manifest = manifest;
        _sourceRow80 = sourceRow80.ToArray();
        _outputRow80 = outputRow80.ToArray();
        _targetOverlay = targetOverlay.ToArray();
        _globalExecutable = globalExecutable.ToArray();
        _ownedRanges = Array.AsReadOnly(ownedRanges.Select(item => item.DeepCopy()).ToArray());
        _inverseRanges = Array.AsReadOnly(_ownedRanges.Reverse().Select(item => item.Invert()).ToArray());
        _diffRanges = Array.AsReadOnly(diffRanges.ToArray());
        ChangedByteCount = changedByteCount;
        DiffManifestSha256 = diffManifestSha256;
        OwnedRangeMapSha256 = ownedRangeMapSha256;
        InverseRangeMapSha256 = inverseRangeMapSha256;
        TransactionSha256 = transactionSha256;
        ProtectedComplementSha256 = protectedComplementSha256;
        DescriptorFullFieldSha256 = descriptorFullFieldSha256;
        DeterministicPlanSha256 = deterministicPlanSha256;
        Stationary = stationary;
        _dragonReadback = Array.AsReadOnly(dragons.ToArray());
        ZeroEgg = zeroEgg;
        Structure = structure;
        SourceRow80Sha256 = Id65AuthoringRewardStagingCompiler.Hash(_sourceRow80);
        OutputRow80Sha256 = Id65AuthoringRewardStagingCompiler.Hash(_outputRow80);
        TargetOverlaySha256 = Id65AuthoringRewardStagingCompiler.Hash(_targetOverlay);
        GlobalExecutableSha256 = Id65AuthoringRewardStagingCompiler.Hash(_globalExecutable);
    }

    public string ProfileId => Id65AuthoringRewardStagingCompiler.ProfileId;
    public Id65RewardStagingManifest Manifest { get; }
    public string ManifestSha256 => Manifest.CanonicalSha256;
    public string SourceRow80Sha256 { get; }
    public string OutputRow80Sha256 { get; }
    public string TargetOverlaySha256 { get; }
    public string GlobalExecutableSha256 { get; }
    public IReadOnlyList<Id65RewardStagingOwnedRange> OwnedRanges => _ownedRanges;
    public IReadOnlyList<Id65RewardStagingOwnedRange> InverseRanges => _inverseRanges;
    public IReadOnlyList<Id65RewardStagingDiffRange> DiffRanges => _diffRanges;
    public int OwnedLeafCount => _ownedRanges.Count;
    public int GuardedByteCount => _ownedRanges.Sum(item => item.ByteLength);
    public int ChangedByteCount { get; }
    public int DiffRangeCount => _diffRanges.Count;
    public string DiffManifestSha256 { get; }
    public string OwnedRangeMapSha256 { get; }
    public string InverseRangeMapSha256 { get; }
    public string TransactionSha256 { get; }
    public string ProtectedComplementSha256 { get; }
    public string DescriptorFullFieldSha256 { get; }
    public string DeterministicPlanSha256 { get; }
    public Id65RewardStagingStationaryReadback Stationary { get; }
    public IReadOnlyList<Id65RewardStagingDragonReadback> Dragons => _dragonReadback;
    public Id65RewardStagingZeroEggReadback ZeroEgg { get; }
    public Id65RewardStagingStructuralReadback Structure { get; }

    public bool BuildsDirectlyFromLockedSourceOnce => true;
    public bool AcceptsCompiledSupportAfterimage => false;
    public bool AcceptsCompiledCompositeAfterimage => false;
    public bool AcceptsCompiledMobyAfterimage => false;
    public bool CallsRetiredPublisher => false;
    public bool WriterAuthorized => false;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool WritesBin => false;
    public bool WritesCue => false;
    public bool EnemyRoutesProven => false;
    public bool ChestChildBehaviorProven => false;
    public bool RuntimeProven => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool AllDragonRoutesContainedByNamedPrecincts => false;
    public bool TreasureTotal200Proven => false;
    public bool SavePersistenceProven => false;
    public bool SaveAuthorized => false;
    public bool MusicLongPlayProven => false;
    public bool ExitDestination65Proven => false;
    public bool PromotionAuthorized => false;
    public bool AppIntegrated => false;
    public bool CreateBinEnabled => false;
    public bool NormalCreateBinEnabled => false;
    public bool ReleaseAuthorized => false;
    public bool Publishable => false;
    public bool Full8Excluded => true;
    public bool ExecutableMutationExcluded => true;
    public bool AfterimageStackingAuthorized => false;
    public bool ExactInverseVerified => true;
    public bool ProtectedComplementVerified => true;
    public bool DeterministicReadbackVerified => true;

    internal byte[] CopySourceRow80() => _sourceRow80.ToArray();
    internal byte[] CopyOutputRow80() => _outputRow80.ToArray();
    internal byte[] CopyTargetOverlay() => _targetOverlay.ToArray();
    internal byte[] CopyGlobalExecutable() => _globalExecutable.ToArray();
}

/// <summary>
/// One bounded, pure in-memory authoring transaction for the exact locked ID65
/// source. Core4 stages T66, replacement support, the established landing/T92,
/// ZeroEgg Grass, four translated dragon bundles, and 71 stationary owners.
/// Full8 remains a non-compilable reservation/capacity boundary.
/// </summary>
internal static class Id65AuthoringRewardStagingCompiler
{
    public const string ProfileId =
        "id65-authoring-reward-staging-locked-source-core4-static-in-memory-v1";
    public const string RequiredSmokeAssemblyName =
        "Spyro.Editor.Id65AuthoringRewardStagingCompilerSmoke";

    public const int ExpectedOwnedLeafCount = 9_313;
    public const int ExpectedGuardedByteCount = 1_027_212;
    public const int ExpectedStationaryOwnerCount = 71;
    public const int ExpectedStationaryRewardTotal = 171;
    public const int ExpectedMobileReservationCount = 10;
    public const int ExpectedMobileReservationRewardTotal = 29;
    public const int ExpectedTrackFrameCount = 1_269;
    public const string ExpectedSupportExternalLedgerSha256 =
        "3c235d791578ce6496d975d99868d487813ee1343ce5ddd15f31cd9dd79dcaf3";

    public const string ExpectedStationarySourceRowsSha256 =
        "17e150c2af25ff795c360d991ce7e7a310bbbc0d4a25be52dcf8ae19c98d8e45";
    public const string ExpectedStationaryOutputRowsSha256 =
        "e18f65b059cf3a6565e148d7ba412312192e8cef95ae6f85e57ecb1d982658ac";
    public const string ExpectedStationaryPropertiesSha256 =
        "b3359204b546f6c777f1f5966fc3049a79bb9258a57e8482233d6910ec3bbb5f";
    public const string ExpectedDragonOutputRowsSha256 =
        "f6ecfb0fcc79d9dd0a916715886dadfc0db83db51a88853845372a5b8fdf4534";
    public const string ExpectedZeroEggRowSha256 =
        "4228cf9bf97a8309f26b9f1303174e116916e2f9633388f2346a654e97cdb56f";
    public const string ExpectedGrassPackageSha256 =
        "90ca71a190c4567817d728753f25df667d56515e1721d24e19e6da9dc07edcc3";
    public const string ExpectedGrassPropertiesSha256 =
        "9eeeff662fd5b77dbc35de8ed01e0d1fd149cee49126625b69f65553c4b7c20b";
    public const string ExpectedPrivateThiefBlockSha256 =
        "6213aceb752cb139ac669b392a3faf7441888306b363fb4cf3e2c63f45715136";
    public const string ExpectedOutsideModelRowSha256 =
        "8cd0de05ad6ee0f3d0c34002ad077730811f8e5eb17b1b28ea7f31370106aa37";
    public const string ExpectedOutsideTexturePagesAndModelRowSha256 =
        "d0eaa6a7216ef2d3c6b560b4d3e80fa9d291d95662d286f30e2a08e8c743d09e";

    // Filled from the first executed direct compiler pass, then frozen by the
    // dedicated smoke before this slice is released.
    public const string ExpectedCoreManifestSha256 =
        "fe4f41fe1523d933d2a1c11bb8a121de06335b9ce622d41e2109fbb6c0fbc200";
    public const string ExpectedGrassDescriptorFullFieldSha256 =
        "1848e15c38f12ab5ec47177755854d9bf7d6dadf25711a1a428d8b72ad7a9059";
    public const string ExpectedOutputRow80Sha256 =
        "749267fc4635b25111b68a7c1b58282d4787efc83e2ddf3413e31fa3385f2755";
    public const string ExpectedDiffManifestSha256 =
        "eeab2116daeaad75b4adaf409e308684cf081881b06f28c3c2008090345cf922";
    public const string ExpectedOwnedRangeMapSha256 =
        "320da2ee310b39283d0f3a4526ca4928f3a9483524a1944e14126ed1339f3e47";
    public const string ExpectedInverseRangeMapSha256 =
        "13768a15f6aa5b5e451d1ebbced6bc24f93169d3f274b3f198fd789e3d59ec15";
    public const string ExpectedTransactionSha256 =
        "af53d20c84e8b71d1d93bafecbf0b105f4215f3f15d5dbaab621430e84338bbc";
    public const string ExpectedProtectedComplementSha256 =
        "c72370b6d0998ab37f42729be510a7b89d1c41629dfe773ee6c89579aa95e9e7";
    public const string ExpectedDeterministicPlanSha256 =
        "18911d8229ba7383c9480dfef1d237071f0a8d9717d85c471b6b44c504f5d404";
    public const int ExpectedChangedByteCount = 972_520;
    public const int ExpectedDiffRangeCount = 42_011;

    internal static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        WriteIndented = false
    };

    private const int Row80ByteLength = 0x2E2000;
    private const int OverlayByteLength = 0xF800;
    private const int ExecutableByteLength = 0x66000;
    private const int TexturePagesOffset = 0x800;
    private const int ModelOffset = 0xDE800;
    private const int ModelByteLength = 0x94800;
    private const int ActorSubfileOffset = 0x173000;
    private const int ActorSubfileByteLength = 0x5D000;
    private const int SceneOffset = 0x1D0000;
    private const int SceneByteLength = 0x8800;
    private const int ObjectCountOffset = SceneOffset + 0x16C;
    private const int ObjectTableOffset = SceneOffset + 0x170;
    private const int ObjectRecordByteLength = 0x58;
    private const int ObjectRecordCount = 107;
    private const int LandingXyOffset = SceneOffset;
    private const int T92XyOffset = SceneOffset + 0x211C;
    private const int Root37Offset = 0xE4;
    private const int ActorId37Offset = 0x19A;
    private const int GrassPackageOffset = 0x1CFA44;
    private const int GrassPackageByteLength = 0x174;
    private const int T88RowOffset = ObjectTableOffset + (88 * ObjectRecordByteLength);
    private const int FixupCountOffset = SceneOffset + 0x7F28;
    private const int FixupActiveByteLength = 0x208;
    private const int GrassPropertiesOffset = SceneOffset + 0x8138;
    private const int PrivateThiefBlockOffset = SceneOffset + 0x5AFC;
    private const int PrivateThiefBlockByteLength = 0x134;
    private const uint GrassPackagePointer = 0x1CFA44;
    private const uint GrassPropertiesPointer = 0x8138;
    private const ushort GrassActorId = 0x01F5;
    private const long Id65DataWadOffset = 0x6936800;

    private static readonly string[] ExpectedDragonPedestalOutputRowSha256 =
    [
        "fe220ab0d3964d54b66a6b976cded50acd7eeaa4ab1ffad897c414b8de9fc6fd",
        "26fe57b9c59a5534a2069cf519ab7a0db452e36240db1c467a010e9b97fbdc1a",
        "a092f6d8d26effdeabbd914ff1af9745905185ce444b6f4a3a72034b79e6b210",
        "ee2a0cad24802baa6f184f1c707fc0aa64680fcda36db74fa36491491d6fa355"
    ];

    private static readonly string[] ExpectedDragonActorOutputRowSha256 =
    [
        "43d17e3914c60627072310c46c467f84ace9b251c33208f879687ed44f8c527a",
        "13123beb1f0919c52126925f1c6c209cd0c51bfc80f6ae89d467ebeecf334519",
        "0f62a97af0e628988b7fabdd1b7baba46b453b2b523fb1cd4228e0de4123fa7f",
        "50eb87e93923df00a0ab47d7c30d293dc0821a22973b46480bc67f536c768672"
    ];

    private static readonly string[] ExpectedDragonContainerOutputRowSha256 =
    [
        "a1e3d8d4d8175009b1f41782142841ef48d655e9294da7b7b06156f98d859bc2",
        "6bbf9cb15b81977c1b9f10ce7322d6775adce795f12eaa77c2093ca83ea4775a",
        "55a56e52b115742a9002d45385feaf1cc8da1e3b1d321e8712b4b9e0a7193e6a",
        "ef809ccbe0c40ea46dd7639007aa988cc72df3d59e9c22925dabb9d93f6c4800"
    ];

    private static readonly string[] ExpectedDragonOutputBundleSha256 =
    [
        "cdf9e32a9ce82eed4ce96e49c241bbc235f93892bfb6c691c6af67260da2bb24",
        "f86c601a61b02f7cfb42cc27caf1cb91bb14710413d0ea1b98b0e1299bd4c3bf",
        "0b743802a6265abbc07af35c14f1aced574fe7e0b707d4334918bc394c272a96",
        "20636c293a652fe121e7d14bd7bc82d28b7bdee8552bc57082f43359cbff8d5c"
    ];

    private static readonly string[] ExpectedDragonOutputCameraSha256 =
    [
        "83da63c07f703b28afb1c637ec9900d99382ace28b99d41beebcab13dc663fed",
        "4c5ed7dcfa73216a09c7c46625e9670956906cfdc5191e00693d9b5ca076817a",
        "84f683f07d1fec708117035c80c3165de7584759adb55dc87cfbc6a4dc3ef21f",
        "64c4fa20ca3cf0658351e4acb164c34a8feb01a8b53951ae85f564415a606683"
    ];

    private static readonly string[] ExpectedDragonOutputTrackSha256 =
    [
        "af27ee5ca3c7b7ad4940cbd8f3d22dabf326adce9867aab3ce66c5ea05c1c1e3",
        "43b0bdf07ddfcd84cdf2d4133512a56c39c3a20ff7cd3ff415c65556a46faa57",
        "09c3b0bb65c709eb195ef3005f3d380c8993b34e1a21d8a800364fc894ce0344",
        "1caf57d1bbe5ca9230dc9264f93e471f2d39f8b21cc5d359d5223c63089f60c5"
    ];

    public static Id65RewardStagingLockedSource CaptureLockedSource(
        ReadOnlySpan<byte> row80,
        ReadOnlySpan<byte> targetOverlay,
        ReadOnlySpan<byte> globalExecutable,
        Id65V2NativeTextureWitness textureWitness)
    {
        ArgumentNullException.ThrowIfNull(textureWitness);
        if (row80.Length != Row80ByteLength || targetOverlay.Length != OverlayByteLength ||
            globalExecutable.Length != ExecutableByteLength)
        {
            throw new InvalidDataException("The locked reward-staging row/overlay/executable envelope changed.");
        }
        RequireHash(Hash(row80), Id65RewardCensusContract.Id65DataSha256, "locked ID65 row 80");
        RequireHash(Hash(targetOverlay), Id65RewardCensusContract.Id65OverlaySha256, "locked ID65 overlay");
        RequireHash(Hash(globalExecutable), Id65RewardCensusContract.ExecutableSha256, "locked executable");

        // Capture validates the complete 67-row/7,949-leaf T66 projection from
        // raw source. It produces no desired state or compiled transition.
        _ = Id65AuthoringSupportReplacementCompiler.CaptureLockedSource(row80, textureWitness);
        return new(row80.ToArray(), targetOverlay.ToArray(), globalExecutable.ToArray(), textureWitness);
    }

    public static Id65RewardStagingManifest CreateCore4Manifest(
        Id65RewardCensus census,
        Id65RewardSupportLayout layout,
        Id65AuthoringSupportManifest coreSupportManifest,
        Id65AuthoringSupportManifest fullSupportManifest,
        Id65MobyDependencyBundleDescriptor grassBundle) =>
        CreateManifest(
            Id65RewardStagingProfileKind.Core4Stationary171,
            census,
            layout,
            coreSupportManifest,
            fullSupportManifest,
            grassBundle,
            authorMobileEnemies: false);

    public static Id65RewardStagingManifest CreateFull8ReservationsOnlyManifest(
        Id65RewardCensus census,
        Id65RewardSupportLayout layout,
        Id65AuthoringSupportManifest coreSupportManifest,
        Id65AuthoringSupportManifest fullSupportManifest,
        Id65MobyDependencyBundleDescriptor grassBundle) =>
        CreateManifest(
            Id65RewardStagingProfileKind.Full8ReservationsOnly,
            census,
            layout,
            coreSupportManifest,
            fullSupportManifest,
            grassBundle,
            authorMobileEnemies: false);

    public static Id65CompiledRewardStaging Compile(Id65RewardStagingCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequestEnvelope(request);
        byte[] lockedRow80 = request.Source.CopyRow80();
        byte[] overlay = request.Source.CopyTargetOverlay();
        byte[] executable = request.Source.CopyGlobalExecutable();
        Id65AuthoringSupportLockedSource supportSource =
            Id65AuthoringSupportReplacementCompiler.CaptureLockedSource(
                lockedRow80, request.Source.TextureWitness);
        Id65SupportDesiredStateProjection support =
            Id65AuthoringSupportReplacementCompiler.BuildCore4DesiredStateProjection(
                supportSource, request.CoreSupportManifest);
        ValidateSupportProjection(support, lockedRow80);

        List<Id65RewardStagingOwnedRange> ranges = [];
        AddSupportRanges(ranges, lockedRow80, support);
        AddZeroEggRanges(ranges, lockedRow80, request.GrassBundle);
        AddStationaryRanges(ranges, lockedRow80, request.Manifest);
        AddDragonRanges(ranges, lockedRow80, request.Manifest);
        Id65RewardStagingOwnedRange[] owned = ValidateOwnedRanges(lockedRow80, ranges);
        byte[] output = ApplyOwnedRanges(lockedRow80, owned, reverse: false);
        byte[] inverse = ApplyOwnedRanges(output, owned, reverse: true);
        if (!inverse.SequenceEqual(lockedRow80))
            throw new InvalidDataException("The reward-staging inverse did not restore the exact locked source.");

        Id65RewardStagingDiffRange[] diff = BuildOwnedDiffRanges(lockedRow80, output, owned);
        int changedBytes = diff.Sum(item => item.ByteLength);
        ValidateDiffCoverage(lockedRow80, output, owned, diff, changedBytes);
        string protectedComplementSha256 = ValidateProtectedComplement(lockedRow80, output, owned);
        string diffHash = HashDiffRanges(diff);
        string ownedHash = HashOwnedRanges(owned);
        Id65RewardStagingOwnedRange[] inverseRanges = owned.Reverse().Select(item => item.Invert()).ToArray();
        string inverseHash = HashOwnedRanges(inverseRanges);
        string transactionHash = HashTransaction(owned);

        Id65RewardStagingStationaryReadback stationary =
            BuildStationaryReadback(lockedRow80, output, request.Manifest);
        Id65RewardStagingDragonReadback[] dragons =
            BuildDragonReadback(lockedRow80, output, request.Manifest);
        Id65RewardStagingZeroEggReadback zeroEgg = BuildZeroEggReadback(lockedRow80, output);
        Id65RewardStagingStructuralReadback structure =
            BuildStructuralReadback(lockedRow80, output, overlay, executable, support);
        string descriptorFullFieldSha256 = Hash(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(request.GrassBundle, CanonicalJsonOptions)));
        string deterministicHash = Hash(Encoding.UTF8.GetBytes(string.Join('\n',
        [
            ProfileId,
            request.Manifest.CanonicalSha256,
            request.Census.DeterministicCensusSha256,
            request.Layout.DeterministicLayoutSha256,
            request.CoreSupportManifest.CanonicalSha256,
            support.CanonicalSha256,
            support.TexturePageDescriptorMapSha256,
            support.DesiredLeafMapSha256,
            support.ReadbackSha256,
            support.CapacitySha256,
            ExpectedSupportExternalLedgerSha256,
            request.GrassBundle.CanonicalSha256,
            descriptorFullFieldSha256,
            Hash(lockedRow80),
            Hash(output),
            Hash(overlay),
            Hash(executable),
            owned.Length.ToString(CultureInfo.InvariantCulture),
            owned.Sum(item => item.ByteLength).ToString(CultureInfo.InvariantCulture),
            changedBytes.ToString(CultureInfo.InvariantCulture),
            diff.Length.ToString(CultureInfo.InvariantCulture),
            diffHash,
            ownedHash,
            inverseHash,
            transactionHash,
            protectedComplementSha256,
            Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(stationary, CanonicalJsonOptions))),
            Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dragons, CanonicalJsonOptions))),
            Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(zeroEgg, CanonicalJsonOptions))),
            Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(structure, CanonicalJsonOptions)))
        ])));

        Id65CompiledRewardStaging compiled = new(
            request.Manifest,
            lockedRow80,
            output,
            overlay,
            executable,
            owned,
            diff,
            changedBytes,
            diffHash,
            ownedHash,
            inverseHash,
            transactionHash,
            protectedComplementSha256,
            descriptorFullFieldSha256,
            deterministicHash,
            stationary,
            dragons,
            zeroEgg,
            structure);
        ValidateCompiled(compiled, support);
        if (!ApplyTransactional(compiled, compiled.CopySourceRow80(), reverse: false)
                .SequenceEqual(compiled.CopyOutputRow80()) ||
            !ApplyTransactional(compiled, compiled.CopyOutputRow80(), reverse: true)
                .SequenceEqual(compiled.CopySourceRow80()))
        {
            throw new InvalidDataException("The compiled reward-staging transaction did not round-trip exactly.");
        }
        return compiled;
    }

    internal static byte[] ApplyTransactional(
        Id65CompiledRewardStaging compiled,
        byte[] input,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(compiled);
        ArgumentNullException.ThrowIfNull(input);
        string expectedHash = reverse ? compiled.OutputRow80Sha256 : compiled.SourceRow80Sha256;
        if (input.Length != Row80ByteLength || Hash(input) != expectedHash)
        {
            throw new InvalidDataException(
                $"The reward-staging {(reverse ? "afterimage" : "locked-source preimage")} changed.");
        }
        byte[] output = reverse
            ? ApplyOwnedRanges(input, compiled.OwnedRanges, reverse: true)
            : ApplyOwnedRanges(input, compiled.OwnedRanges, reverse: false);
        string outputHash = reverse ? compiled.SourceRow80Sha256 : compiled.OutputRow80Sha256;
        RequireHash(Hash(output), outputHash, reverse ? "rolled-back locked row" : "reward-staging afterimage");
        return output;
    }

    internal static void ValidateOwnershipForSmoke(
        ReadOnlySpan<byte> source,
        ReadOnlySpan<byte> output,
        IEnumerable<Id65RewardStagingOwnedRange> ranges)
    {
        byte[] before = source.ToArray();
        byte[] after = output.ToArray();
        Id65RewardStagingOwnedRange[] owned = ValidateOwnedRanges(before, ranges);
        Id65RewardStagingDiffRange[] diff = BuildOwnedDiffRanges(before, after, owned);
        ValidateDiffCoverage(before, after, owned, diff, diff.Sum(item => item.ByteLength));
        _ = ValidateProtectedComplement(before, after, owned);
    }

    internal static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static Id65RewardStagingManifest CreateManifest(
        Id65RewardStagingProfileKind kind,
        Id65RewardCensus census,
        Id65RewardSupportLayout layout,
        Id65AuthoringSupportManifest coreSupportManifest,
        Id65AuthoringSupportManifest fullSupportManifest,
        Id65MobyDependencyBundleDescriptor grassBundle,
        bool authorMobileEnemies)
    {
        ArgumentNullException.ThrowIfNull(census);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(coreSupportManifest);
        ArgumentNullException.ThrowIfNull(fullSupportManifest);
        ArgumentNullException.ThrowIfNull(grassBundle);
        Id65RewardSupportLayout canonical = Id65RewardSupportLayoutContract.Build(
            census, coreSupportManifest, fullSupportManifest);
        RequireEquivalentLayout(layout, canonical);
        ValidateGrassBundle(grassBundle, targetOverlay: null, globalExecutable: null);

        Id65RewardStagingStationaryIntent[] stationary = layout.StationaryPlacements
            .OrderBy(item => item.Owner.TrueIndex)
            .Select(item => new Id65RewardStagingStationaryIntent(
                item.Slot.SlotIndex,
                item.Owner.TrueIndex,
                item.Owner.ActorClass,
                item.Owner.Encoding,
                item.Owner.OwnerSemantics,
                item.Owner.RewardValue,
                item.Owner.RowSha256,
                item.Owner.PropertiesSceneOffset,
                item.Owner.PropertiesByteLength,
                item.Owner.PropertiesSha256,
                item.Owner.RetirementByteIndex,
                item.Owner.RetirementBitMask,
                item.Slot.Point))
            .ToArray();

        Id65RewardStagingDragonIntent[] dragons = BuildDragonIntents(census, layout);
        Id65RewardStagingMobileReservation[] mobile = layout.EnemyReservations
            .OrderBy(item => item.Owner.TrueIndex)
            .Select(item => new Id65RewardStagingMobileReservation(
                item.Id,
                item.Kind,
                item.Owner.TrueIndex,
                item.Owner.ActorClass,
                item.Owner.RewardValue,
                item.Owner.RowSha256,
                item.Owner.PropertiesSceneOffset,
                item.Owner.PropertiesByteLength,
                item.Owner.PropertiesSha256,
                item.Point,
                item.ReservationOnly,
                item.RoutePinned,
                item.ActivationBoundsPinned,
                item.RuntimeAccepted))
            .ToArray();

        string supportSha = kind == Id65RewardStagingProfileKind.Core4Stationary171
            ? coreSupportManifest.CanonicalSha256
            : fullSupportManifest.CanonicalSha256;
        Id65RewardStagingManifest manifest = new(
            kind,
            census.DeterministicCensusSha256,
            layout.DeterministicLayoutSha256,
            supportSha,
            grassBundle.CanonicalSha256,
            stationary,
            dragons,
            mobile,
            authorMobileEnemies);
        ValidateManifest(manifest, census, layout, coreSupportManifest, fullSupportManifest, grassBundle);
        return manifest;
    }

    private static Id65RewardStagingDragonIntent[] BuildDragonIntents(
        Id65RewardCensus census,
        Id65RewardSupportLayout layout)
    {
        Id65RewardDragonBundle[] sources = census.DragonBundles.OrderBy(item => item.DragonTrueIndex).ToArray();
        Id65RewardSupportDragonPrecinct[] destinations = layout.DragonPrecincts
            .OrderBy(item => item.Dragon.DragonTrueIndex).ToArray();
        if (sources.Length != 4 || destinations.Length != 4)
            throw new InvalidDataException("The exact four-dragon source/layout boundary changed.");
        Id65RewardStagingDragonIntent[] result = new Id65RewardStagingDragonIntent[4];
        for (int index = 0; index < result.Length; index++)
        {
            Id65RewardDragonBundle source = sources[index];
            Id65RewardSupportDragonPrecinct destination = destinations[index];
            if (source.PedestalTrueIndex != destination.Dragon.PedestalTrueIndex ||
                source.DragonTrueIndex != destination.Dragon.DragonTrueIndex ||
                source.ContainerTrueIndex != destination.Dragon.ContainerTrueIndex)
            {
                throw new InvalidDataException("A source/layout dragon identity no longer joins exactly.");
            }
            Id65RewardSupportRawPoint translation = new(
                checked(destination.Actor.X - ReadSourceDragonCoordinate(census, source.DragonTrueIndex, 0)),
                checked(destination.Actor.Y - ReadSourceDragonCoordinate(census, source.DragonTrueIndex, 1)),
                checked(destination.Actor.Z - ReadSourceDragonCoordinate(census, source.DragonTrueIndex, 2)));
            result[index] = new(
                destination.Id,
                source.PedestalTrueIndex,
                source.DragonTrueIndex,
                source.ContainerTrueIndex,
                source.PedestalRowSha256,
                source.DragonRowSha256,
                source.ContainerRowSha256,
                source.ThreeRowBundleSha256,
                destination.Pedestal,
                destination.Actor,
                destination.ControlDestination,
                destination.PrecinctEnvelope.Minimum,
                destination.PrecinctEnvelope.Maximum,
                translation,
                checked((int)(source.CameraDataWadOffset - Id65DataWadOffset)),
                source.CameraDataByteLength,
                source.CameraDataSha256,
                checked((int)(source.SceneLinkWadOffset - Id65DataWadOffset)),
                source.SceneLinkByteLength,
                source.SceneLinkSha256,
                checked((int)(source.CameraTrackWadOffset - Id65DataWadOffset)),
                source.CameraTrackByteLength,
                source.CameraTrackFrameCount,
                source.CameraTrackSha256,
                source.CutsceneIndex,
                source.DragonNameIndex,
                source.RunToAngle,
                source.RunToRadius,
                source.RunToAuxiliary);
        }
        return result;
    }

    private static int ReadSourceDragonCoordinate(Id65RewardCensus census, int trueIndex, int axis)
    {
        // The census pins the row identity but intentionally does not retain row
        // bytes. Exact source coordinates are frozen below and rechecked against
        // locked row bytes during compilation.
        (int X, int Y, int Z) source = trueIndex switch
        {
            12 => (128_829, 111_882, 11_387),
            13 => (116_275, 129_823, 9_805),
            49 => (134_083, 129_638, 11_899),
            101 => (129_638, 123_085, 13_835),
            _ => throw new InvalidDataException($"Unexpected dragon actor T{trueIndex}.")
        };
        _ = census;
        return axis switch { 0 => source.X, 1 => source.Y, 2 => source.Z, _ => throw new ArgumentOutOfRangeException(nameof(axis)) };
    }

    private static void ValidateSupportProjection(
        Id65SupportDesiredStateProjection support,
        byte[] lockedRow80)
    {
        ArgumentNullException.ThrowIfNull(support);
        if (support.ProfileId != Id65AuthoringSupportReplacementCompiler.DesiredProjectionProfileId ||
            support.Kind != Id65SupportProfileKind.Core4 || !support.DirectLockedSourceDerived ||
            support.ContainsFullAuthoredRow80 || support.ContainsCompiledAfterimage ||
            support.AfterimageStackingAuthorized || support.ContainsSlicePlan || support.ContainsPath ||
            support.WritesFileSystem || support.WritesDiscImage || support.WritesCue ||
            support.PublisherCalled || support.WriterAuthorized || support.AppIntegrated ||
            support.CreateBinEnabled || support.NormalCreateBinEnabled ||
            support.RuntimeCandidateAuthorized || support.RuntimeAccepted ||
            support.ReleaseIntegrated || support.PromotionAuthorized || support.Publishable ||
            !support.ExecutableMutationExcluded || !support.Full8Excluded ||
            support.LockedRow80Sha256 != Id65RewardCensusContract.Id65DataSha256 ||
            support.TextureWitnessSha256 != Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256 ||
            support.TextureProjectionSha256 != Id65AuthoringSupportReplacementCompiler.ExpectedTextureProjectionSha256 ||
            support.CoreManifestSha256 != Id65AuthoringSupportReplacementCompiler.ExpectedCoreManifestSha256 ||
            support.CoreModelByteLength != ModelByteLength ||
            support.CoreModelSha256 != Id65AuthoringSupportReplacementCompiler.ExpectedCoreModelSha256 ||
            support.CoreRow80CrossCheckSha256 != Id65AuthoringSupportReplacementCompiler.ExpectedCoreRow80Sha256 ||
            support.TexturePageDescriptorMapSha256 !=
                Id65AuthoringSupportReplacementCompiler.ExpectedCoreDesiredPageDescriptorMapSha256 ||
            support.DesiredLeafMapSha256 !=
                Id65AuthoringSupportReplacementCompiler.ExpectedCoreDesiredLeafMapSha256 ||
            support.ReadbackSha256 !=
                Id65AuthoringSupportReplacementCompiler.ExpectedCoreDesiredReadbackSha256 ||
            support.CapacitySha256 !=
                Id65AuthoringSupportReplacementCompiler.ExpectedCoreDesiredCapacitySha256 ||
            support.CanonicalSha256 !=
                Id65AuthoringSupportReplacementCompiler.ExpectedCoreDesiredProjectionSha256 ||
            support.TexturePageDescriptorCount != Id65AuthoringSupportReplacementCompiler.ExpectedTexturePagePatchCount ||
            support.TexturePageChangedByteCount != Id65AuthoringSupportReplacementCompiler.ExpectedTexturePageChangedByteCount ||
            support.DesiredLeaves.Count != 2 || support.Readback.Kind != Id65SupportProfileKind.Core4 ||
            support.Readback.SectorCount != 4 || support.Readback.ActiveCollisionCellCount != 16 ||
            support.Readback.CollisionBlocksUsedBytes != 0x82 || support.Readback.UsedModelBytes != 0x6C454 ||
            support.Readback.ZeroTailBytes != 0x283AC || support.Readback.RuntimeAccepted ||
            support.Capacity.OutputSectorCount != 4 || support.Capacity.OutputCollisionBlocksUsedBytes != 0x82 ||
            support.Capacity.TextureCount != 67 || support.Capacity.HighestTextureId != 66)
        {
            throw new InvalidDataException("The direct Core4 support desired-state projection changed or opened a gate.");
        }
        RequireHash(Hash(support.CopyCoreModel()), support.CoreModelSha256, "copied desired Core4 model");
        Id65SupportDesiredLeaf[] leaves = support.CopyDesiredLeaves().OrderBy(item => item.DataRelativeOffset).ToArray();
        if (leaves[0].DataRelativeOffset != LandingXyOffset || leaves[0].ByteLength != 8 ||
            leaves[1].DataRelativeOffset != T92XyOffset || leaves[1].ByteLength != 8 ||
            !leaves.All(item => lockedRow80.AsSpan(item.DataRelativeOffset, item.ByteLength)
                .SequenceEqual(item.CopyLockedPreimage())))
        {
            throw new InvalidDataException("The desired landing/T92 support leaves changed.");
        }
        if (HashSupportExternalLedger(support, lockedRow80) != ExpectedSupportExternalLedgerSha256)
            throw new InvalidDataException("The frozen external Core4 support ledger proof changed.");
    }

    private static string HashSupportExternalLedger(
        Id65SupportDesiredStateProjection support,
        byte[] lockedRow80)
    {
        List<(string StableId, string OwnerId, int Offset, int Length, string Before, string After)> ledger = [];
        Id65V2NativeTexturePagePatch[] pages = support.CopyTexturePageDescriptors()
            .OrderBy(item => item.RelativeOffset).ToArray();
        for (int index = 0; index < pages.Length; index++)
        {
            Id65V2NativeTexturePagePatch page = pages[index];
            ledger.Add(($"texture-page.{index:D4}", "support.desired.t66-pages",
                TexturePagesOffset + page.RelativeOffset, page.ByteLength,
                Hash(page.CopyBefore()), Hash(page.CopyAfter())));
        }
        byte[] model = support.CopyCoreModel();
        ledger.Add(("support.model.core4", "support.desired.core4-model", ModelOffset,
            model.Length, Hash(lockedRow80.AsSpan(ModelOffset, model.Length)), Hash(model)));
        foreach (Id65SupportDesiredLeaf leaf in support.CopyDesiredLeaves())
        {
            ledger.Add((leaf.StableId, "support.desired.placement", leaf.DataRelativeOffset,
                leaf.ByteLength, Hash(leaf.CopyLockedPreimage()), Hash(leaf.CopyDesiredBytes())));
        }
        if (ledger.Count != 7_952 || ledger.Sum(item => item.Length) != 1_010_466)
            throw new InvalidDataException("The external support ledger cardinality changed.");
        StringBuilder text = new();
        foreach (var item in ledger.OrderBy(item => item.Offset))
            text.Append(item.StableId).Append('|').Append(item.OwnerId).Append('|')
                .Append(item.Offset.ToString("X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(item.Length).Append('|').Append(item.Before).Append('|').Append(item.After).Append('\n');
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static void AddSupportRanges(
        List<Id65RewardStagingOwnedRange> ranges,
        byte[] lockedRow80,
        Id65SupportDesiredStateProjection support)
    {
        Id65V2NativeTexturePagePatch[] pages = support.CopyTexturePageDescriptors()
            .OrderBy(item => item.RelativeOffset).ToArray();
        if (pages.Length != Id65AuthoringSupportReplacementCompiler.ExpectedTexturePagePatchCount ||
            pages.Sum(item => item.ByteLength) != Id65AuthoringSupportReplacementCompiler.ExpectedTexturePageChangedByteCount)
        {
            throw new InvalidDataException("The T66 desired-page ownership cardinality changed.");
        }
        for (int index = 0; index < pages.Length; index++)
        {
            Id65V2NativeTexturePagePatch page = pages[index];
            int offset = checked(TexturePagesOffset + page.RelativeOffset);
            byte[] before = page.CopyBefore();
            byte[] after = page.CopyAfter();
            if (Hash(before) != page.BeforeSha256 || Hash(after) != page.AfterSha256 ||
                !lockedRow80.AsSpan(offset, before.Length).SequenceEqual(before))
            {
                throw new InvalidDataException($"T66 desired page {index} lost its locked-source preimage.");
            }
            AddRange(ranges, lockedRow80, $"texture.t66.page.{index:D4}", page.Owner, offset, after, before);
        }
        AddRange(
            ranges,
            lockedRow80,
            "support.core4.model",
            "support.core4.desired-state",
            ModelOffset,
            support.CopyCoreModel());
        foreach (Id65SupportDesiredLeaf leaf in support.CopyDesiredLeaves().OrderBy(item => item.DataRelativeOffset))
        {
            AddRange(
                ranges,
                lockedRow80,
                $"support.core4.{leaf.StableId}",
                "support.core4.desired-state",
                leaf.DataRelativeOffset,
                leaf.CopyDesiredBytes(),
                leaf.CopyLockedPreimage());
        }
    }

    private static void AddZeroEggRanges(
        List<Id65RewardStagingOwnedRange> ranges,
        byte[] lockedRow80,
        Id65MobyDependencyBundleDescriptor grassBundle)
    {
        byte[] package = Convert.FromHexString(grassBundle.ActorPackages.Single().ExactHex);
        byte[] properties = Convert.FromHexString(grassBundle.Properties.Single().ExactHex);
        byte[] donorRow = Convert.FromHexString(grassBundle.Rows.Single().ExactHex);
        byte[] outputRow = donorRow.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(outputRow, GrassPropertiesPointer);
        lockedRow80.AsSpan(T88RowOffset + 0x0C, 12).CopyTo(outputRow.AsSpan(0x0C, 12));
        RequireHash(Hash(lockedRow80.AsSpan(T88RowOffset, ObjectRecordByteLength)),
            "dccfa0a9489f9d76c93958a1a43e64ea948262b51634d03ddc311cdf3ba1a09a",
            "locked T88 thief row");
        RequireHash(Hash(outputRow), ExpectedZeroEggRowSha256, "desired T88 Grass row");
        RequireHash(Hash(lockedRow80.AsSpan(PrivateThiefBlockOffset, PrivateThiefBlockByteLength)),
            ExpectedPrivateThiefBlockSha256, "orphaned thief private block");

        AddRange(ranges, lockedRow80, "zeroegg.root37", "zeroegg.grass", Root37Offset,
            UInt32(GrassPackagePointer));
        AddRange(ranges, lockedRow80, "zeroegg.actor-id37", "zeroegg.grass", ActorId37Offset,
            UInt16(GrassActorId));
        AddRange(ranges, lockedRow80, "zeroegg.package.01f5", "zeroegg.grass", GrassPackageOffset,
            package);
        AddRange(ranges, lockedRow80, "zeroegg.row.t88", "zeroegg.grass", T88RowOffset,
            outputRow);
        AddRange(ranges, lockedRow80, "zeroegg.properties.01f5", "zeroegg.grass", GrassPropertiesOffset,
            properties);
    }

    private static void AddStationaryRanges(
        List<Id65RewardStagingOwnedRange> ranges,
        byte[] lockedRow80,
        Id65RewardStagingManifest manifest)
    {
        int expectedSlot = 0;
        foreach (Id65RewardStagingStationaryIntent placement in manifest.Stationary)
        {
            int rowOffset = checked(ObjectTableOffset + (placement.TrueIndex * ObjectRecordByteLength));
            ReadOnlySpan<byte> row = lockedRow80.AsSpan(rowOffset, ObjectRecordByteLength);
            if (placement.SlotIndex != expectedSlot++ || placement.TrueIndex is < 0 or > 255 ||
                placement.RetirementByteIndex != placement.TrueIndex / 8 ||
                placement.RetirementBitMask != (byte)(1 << (placement.TrueIndex % 8)) ||
                Hash(row) != placement.SourceRowSha256 ||
                BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(0x36, 2)) != placement.ActorClass ||
                BinaryPrimitives.ReadUInt32LittleEndian(row) != placement.PropertiesSceneOffset ||
                placement.OwnerSemantics is not
                    (Id65RewardOwnerSemantics.StationaryLooseGem or
                     Id65RewardOwnerSemantics.StationaryChestCarrier))
            {
                throw new InvalidDataException($"Stationary reward T{placement.TrueIndex} identity or retirement address changed.");
            }
            int propertiesOffset = checked(SceneOffset + (int)placement.PropertiesSceneOffset);
            RequireHash(Hash(lockedRow80.AsSpan(propertiesOffset, placement.PropertiesByteLength)),
                placement.PropertiesSha256, $"stationary T{placement.TrueIndex} properties");
            byte[] xyz = EncodePoint(placement.Point);
            AddRange(
                ranges,
                lockedRow80,
                $"reward.stationary.t{placement.TrueIndex}.xyz",
                placement.OwnerSemantics == Id65RewardOwnerSemantics.StationaryLooseGem
                    ? "reward.stationary.loose-gem"
                    : "reward.stationary.chest-carrier",
                rowOffset + 0x0C,
                xyz);
        }
    }

    private static void AddDragonRanges(
        List<Id65RewardStagingOwnedRange> ranges,
        byte[] lockedRow80,
        Id65RewardStagingManifest manifest)
    {
        int totalFrames = 0;
        for (int bundleIndex = 0; bundleIndex < manifest.Dragons.Count; bundleIndex++)
        {
            Id65RewardStagingDragonIntent dragon = manifest.Dragons[bundleIndex];
            int[] trueIndexes = [dragon.PedestalTrueIndex, dragon.DragonTrueIndex, dragon.ContainerTrueIndex];
            Id65RewardSupportRawPoint[] points = [dragon.Pedestal, dragon.Actor, dragon.ControlDestination];
            string[] sourceHashes =
                [dragon.PedestalSourceRowSha256, dragon.DragonSourceRowSha256, dragon.ContainerSourceRowSha256];
            ushort[] classes = [0x014B, 0x00FA, 0x006E];
            byte[][] sourceRows = trueIndexes.Select(index =>
                lockedRow80.AsSpan(ObjectTableOffset + (index * ObjectRecordByteLength), ObjectRecordByteLength).ToArray())
                .ToArray();
            for (int rowIndex = 0; rowIndex < 3; rowIndex++)
            {
                byte[] row = sourceRows[rowIndex];
                RequireHash(Hash(row), sourceHashes[rowIndex], $"{dragon.Id} source row {rowIndex}");
                if (BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(0x36, 2)) != classes[rowIndex])
                    throw new InvalidDataException($"{dragon.Id} source class trio changed.");
                Id65RewardSupportRawPoint translated = Translate(ReadPoint(row, 0x0C), dragon.Translation);
                if (translated != points[rowIndex])
                    throw new InvalidDataException($"{dragon.Id} row {rowIndex} does not share one uniform translation.");
                AddRange(
                    ranges,
                    lockedRow80,
                    $"reward.{dragon.Id}.row.t{trueIndexes[rowIndex]}.xyz",
                    $"reward.{dragon.Id}.bundle",
                    ObjectTableOffset + (trueIndexes[rowIndex] * ObjectRecordByteLength) + 0x0C,
                    EncodePoint(points[rowIndex]));
            }
            RequireHash(Hash(Join(sourceRows)), dragon.SourceBundleSha256, $"{dragon.Id} source three-row bundle");

            if (dragon.CameraDataByteLength != 0x44 ||
                dragon.SceneLinkByteLength != 0x28 ||
                dragon.CameraTrackByteLength != checked(dragon.CameraTrackFrameCount * 0x18) ||
                dragon.CameraTrackRelativeOffset + dragon.CameraTrackByteLength > Row80ByteLength)
            {
                throw new InvalidDataException($"{dragon.Id} camera/link/track capacity changed.");
            }
            RequireHash(Hash(lockedRow80.AsSpan(dragon.CameraDataRelativeOffset, dragon.CameraDataByteLength)),
                dragon.SourceCameraDataSha256, $"{dragon.Id} source camera data");
            RequireHash(Hash(lockedRow80.AsSpan(dragon.SceneLinkRelativeOffset, dragon.SceneLinkByteLength)),
                dragon.SourceSceneLinkSha256, $"{dragon.Id} source scene link");
            RequireHash(Hash(lockedRow80.AsSpan(dragon.CameraTrackRelativeOffset, dragon.CameraTrackByteLength)),
                dragon.SourceCameraTrackSha256, $"{dragon.Id} source camera track");

            AddRange(
                ranges,
                lockedRow80,
                $"reward.{dragon.Id}.camera.xyz",
                $"reward.{dragon.Id}.camera",
                dragon.CameraDataRelativeOffset,
                EncodePoint(Translate(ReadPoint(lockedRow80, dragon.CameraDataRelativeOffset), dragon.Translation)));
            for (int frame = 0; frame < dragon.CameraTrackFrameCount; frame++)
            {
                int offset = checked(dragon.CameraTrackRelativeOffset + (frame * 0x18));
                AddRange(
                    ranges,
                    lockedRow80,
                    $"reward.{dragon.Id}.track.frame-{frame:D3}.xyz",
                    $"reward.{dragon.Id}.camera-track",
                    offset,
                    EncodePoint(Translate(ReadPoint(lockedRow80, offset), dragon.Translation)));
            }
            totalFrames += dragon.CameraTrackFrameCount;
        }
        if (totalFrames != ExpectedTrackFrameCount)
            throw new InvalidDataException("The four dragon camera tracks no longer total 1,269 frames.");
    }

    private static void AddRange(
        List<Id65RewardStagingOwnedRange> ranges,
        byte[] lockedSource,
        string stableId,
        string ownerId,
        int offset,
        byte[] after,
        byte[]? declaredBefore = null)
    {
        if (string.IsNullOrWhiteSpace(stableId) || string.IsNullOrWhiteSpace(ownerId) ||
            offset < 0 || after.Length == 0 || offset + (long)after.Length > lockedSource.Length)
            throw new InvalidDataException("A reward-staging owned range escaped its exact envelope.");
        byte[] before = lockedSource.AsSpan(offset, after.Length).ToArray();
        if (declaredBefore is not null && !before.SequenceEqual(declaredBefore))
            throw new InvalidDataException($"Reward range `{stableId}` lost its declared locked preimage.");
        if (before.SequenceEqual(after))
            throw new InvalidDataException($"Reward range `{stableId}` became redundant.");
        ranges.Add(new(stableId, ownerId, offset, before, after));
    }

    private static byte[] UInt32(uint value)
    {
        byte[] output = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(output, value);
        return output;
    }

    private static byte[] UInt16(ushort value)
    {
        byte[] output = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(output, value);
        return output;
    }

    private static byte[] EncodePoint(Id65RewardSupportRawPoint point)
    {
        byte[] output = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(0, 4), point.X);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(4, 4), point.Y);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(8, 4), point.Z);
        return output;
    }

    private static Id65RewardSupportRawPoint ReadPoint(byte[] bytes, int offset) => new(
        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4)),
        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + 4, 4)),
        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + 8, 4)));

    private static Id65RewardSupportRawPoint Translate(
        Id65RewardSupportRawPoint source,
        Id65RewardSupportRawPoint delta) => new(
        checked(source.X + delta.X),
        checked(source.Y + delta.Y),
        checked(source.Z + delta.Z));

    private static byte[] Join(IEnumerable<byte[]> values)
    {
        byte[][] arrays = values.ToArray();
        byte[] output = new byte[arrays.Sum(item => item.Length)];
        int cursor = 0;
        foreach (byte[] value in arrays)
        {
            value.CopyTo(output, cursor);
            cursor += value.Length;
        }
        return output;
    }

    private static Id65RewardStagingOwnedRange[] ValidateOwnedRanges(
        byte[] lockedSource,
        IEnumerable<Id65RewardStagingOwnedRange> supplied)
    {
        Id65RewardStagingOwnedRange[] ranges = supplied
            .OrderBy(item => item.DataRelativeOffset)
            .ThenBy(item => item.StableId, StringComparer.Ordinal)
            .ToArray();
        if (ranges.Length != ExpectedOwnedLeafCount ||
            ranges.Sum(item => item.ByteLength) != ExpectedGuardedByteCount ||
            ranges.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count() != ranges.Length)
        {
            throw new InvalidDataException("The 9,313-leaf/1,027,212-byte reward ownership ledger changed.");
        }
        for (int index = 0; index < ranges.Length; index++)
        {
            Id65RewardStagingOwnedRange range = ranges[index];
            byte[] before = range.CopyBefore();
            byte[] after = range.CopyAfter();
            if (range.DataRelativeOffset < 0 || range.ByteLength <= 0 ||
                range.DataRelativeOffset + (long)range.ByteLength > lockedSource.Length ||
                before.Length != range.ByteLength || after.Length != range.ByteLength ||
                Hash(before) != range.BeforeSha256 || Hash(after) != range.AfterSha256 ||
                before.SequenceEqual(after) ||
                !lockedSource.AsSpan(range.DataRelativeOffset, range.ByteLength).SequenceEqual(before))
            {
                throw new InvalidDataException($"Owned range `{range.StableId}` lost its exact locked preimage/output.");
            }
            if (index > 0 &&
                ranges[index - 1].DataRelativeOffset + (long)ranges[index - 1].ByteLength >
                    range.DataRelativeOffset)
            {
                throw new InvalidDataException(
                    $"Owned ranges `{ranges[index - 1].StableId}` and `{range.StableId}` overlap.");
            }
        }
        if (ranges.Count(item => item.StableId.StartsWith("texture.t66.page.", StringComparison.Ordinal)) != 7_949 ||
            ranges.Count(item => item.StableId == "support.core4.model") != 1 ||
            ranges.Count(item => item.StableId.StartsWith("support.core4.", StringComparison.Ordinal) &&
                item.StableId != "support.core4.model") != 2 ||
            ranges.Count(item => item.StableId.StartsWith("zeroegg.", StringComparison.Ordinal)) != 5 ||
            ranges.Count(item => item.StableId.StartsWith("reward.stationary.", StringComparison.Ordinal)) != 71 ||
            ranges.Count(item => item.StableId.Contains(".row.t", StringComparison.Ordinal) &&
                item.StableId.StartsWith("reward.dragon-", StringComparison.Ordinal)) != 12 ||
            ranges.Count(item => item.StableId.EndsWith(".camera.xyz", StringComparison.Ordinal)) != 4 ||
            ranges.Count(item => item.StableId.Contains(".track.frame-", StringComparison.Ordinal)) != 1_269)
        {
            throw new InvalidDataException("A semantic owner category changed within the exact 9,313-leaf ledger.");
        }
        return ranges;
    }

    private static byte[] ApplyOwnedRanges(
        byte[] input,
        IReadOnlyList<Id65RewardStagingOwnedRange> ranges,
        bool reverse)
    {
        byte[] output = input.ToArray();
        IEnumerable<Id65RewardStagingOwnedRange> ordered = reverse
            ? ranges.Reverse()
            : ranges;
        foreach (Id65RewardStagingOwnedRange range in ordered)
        {
            byte[] expected = reverse ? range.CopyAfter() : range.CopyBefore();
            byte[] replacement = reverse ? range.CopyBefore() : range.CopyAfter();
            if (!output.AsSpan(range.DataRelativeOffset, range.ByteLength).SequenceEqual(expected))
            {
                throw new InvalidDataException(
                    $"Owned range `{range.StableId}` lost its exact {(reverse ? "afterimage" : "locked-source preimage")}.");
            }
            replacement.CopyTo(output, range.DataRelativeOffset);
        }
        return output;
    }

    private static Id65RewardStagingDiffRange[] BuildOwnedDiffRanges(
        byte[] source,
        byte[] output,
        IReadOnlyList<Id65RewardStagingOwnedRange> owned)
    {
        List<Id65RewardStagingDiffRange> result = [];
        foreach (Id65RewardStagingOwnedRange owner in owned)
        {
            int local = 0;
            while (local < owner.ByteLength)
            {
                int absolute = owner.DataRelativeOffset + local;
                if (source[absolute] == output[absolute])
                {
                    local++;
                    continue;
                }
                int start = local++;
                while (local < owner.ByteLength &&
                    source[owner.DataRelativeOffset + local] != output[owner.DataRelativeOffset + local])
                {
                    local++;
                }
                int length = local - start;
                int offset = owner.DataRelativeOffset + start;
                result.Add(new(
                    offset,
                    length,
                    owner.StableId,
                    owner.OwnerId,
                    Hash(source.AsSpan(offset, length)),
                    Hash(output.AsSpan(offset, length))));
            }
        }
        return result.OrderBy(item => item.DataRelativeOffset)
            .ThenBy(item => item.OwnerStableId, StringComparer.Ordinal).ToArray();
    }

    private static void ValidateDiffCoverage(
        byte[] source,
        byte[] output,
        IReadOnlyList<Id65RewardStagingOwnedRange> owned,
        IReadOnlyList<Id65RewardStagingDiffRange> diff,
        int changedByteCount)
    {
        if (source.Length != output.Length || changedByteCount != CountChangedBytes(source, output) ||
            diff.Sum(item => item.ByteLength) != changedByteCount ||
            diff.Any(item => item.ByteLength <= 0 ||
                Hash(source.AsSpan(item.DataRelativeOffset, item.ByteLength)) != item.BeforeSha256 ||
                Hash(output.AsSpan(item.DataRelativeOffset, item.ByteLength)) != item.AfterSha256))
        {
            throw new InvalidDataException("The reward diff manifest changed or does not cover every changed byte.");
        }
        foreach (Id65RewardStagingOwnedRange owner in owned)
        {
            if (!source.AsSpan(owner.DataRelativeOffset, owner.ByteLength).SequenceEqual(owner.CopyBefore()) ||
                !output.AsSpan(owner.DataRelativeOffset, owner.ByteLength).SequenceEqual(owner.CopyAfter()))
            {
                throw new InvalidDataException(
                    $"Owned range `{owner.StableId}` does not bind the exact declared before/afterimage bytes.");
            }
        }
        bool[] claimed = new bool[source.Length];
        foreach (Id65RewardStagingDiffRange range in diff)
        {
            Id65RewardStagingOwnedRange[] candidates = owned.Where(owner =>
                range.DataRelativeOffset >= owner.DataRelativeOffset &&
                range.DataRelativeOffset + (long)range.ByteLength <=
                    owner.DataRelativeOffset + (long)owner.ByteLength &&
                owner.StableId == range.OwnerStableId && owner.OwnerId == range.OwnerId).ToArray();
            if (candidates.Length != 1)
                throw new InvalidDataException($"Diff range at 0x{range.DataRelativeOffset:X} has no unique owner.");
            for (int index = range.DataRelativeOffset; index < range.DataRelativeOffset + range.ByteLength; index++)
            {
                if (claimed[index] || source[index] == output[index])
                    throw new InvalidDataException("A diff byte was duplicated or did not actually change.");
                claimed[index] = true;
            }
        }
        for (int index = 0; index < source.Length; index++)
        {
            if ((source[index] != output[index]) != claimed[index])
                throw new InvalidDataException($"Changed byte at row+0x{index:X} escaped exact diff ownership.");
        }
    }

    private static string ValidateProtectedComplement(
        byte[] source,
        byte[] output,
        IReadOnlyList<Id65RewardStagingOwnedRange> owned)
    {
        bool[] guarded = new bool[source.Length];
        foreach (Id65RewardStagingOwnedRange range in owned)
            Array.Fill(guarded, true, range.DataRelativeOffset, range.ByteLength);
        using IncrementalHash sourceHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using IncrementalHash outputHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int cursor = 0;
        while (cursor < source.Length)
        {
            if (guarded[cursor])
            {
                cursor++;
                continue;
            }
            int start = cursor++;
            while (cursor < source.Length && !guarded[cursor])
                cursor++;
            int length = cursor - start;
            if (!source.AsSpan(start, length).SequenceEqual(output.AsSpan(start, length)))
                throw new InvalidDataException($"The protected complement changed at row+0x{start:X}.");
            sourceHash.AppendData(source, start, length);
            outputHash.AppendData(output, start, length);
        }
        string before = Convert.ToHexString(sourceHash.GetHashAndReset()).ToLowerInvariant();
        string after = Convert.ToHexString(outputHash.GetHashAndReset()).ToLowerInvariant();
        if (before != after)
            throw new InvalidDataException("The protected-complement hashes differ.");
        return before;
    }

    private static int CountChangedBytes(byte[] source, byte[] output)
    {
        if (source.Length != output.Length)
            throw new InvalidDataException("Diff byte counts require equal source/output lengths.");
        int count = 0;
        for (int index = 0; index < source.Length; index++)
            if (source[index] != output[index]) count++;
        return count;
    }

    private static string HashDiffRanges(IEnumerable<Id65RewardStagingDiffRange> ranges) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', ranges.Select(item =>
            $"{item.DataRelativeOffset:X8}|{item.ByteLength}|{item.OwnerStableId}|{item.OwnerId}|{item.BeforeSha256}|{item.AfterSha256}"))));

    private static string HashOwnedRanges(IEnumerable<Id65RewardStagingOwnedRange> ranges) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', ranges.Select(item =>
            $"{item.StableId}|{item.OwnerId}|{item.DataRelativeOffset:X8}|{item.ByteLength}|{item.BeforeSha256}|{item.AfterSha256}"))));

    private static string HashTransaction(IEnumerable<Id65RewardStagingOwnedRange> ranges) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', ranges.Select(item =>
            $"{item.DataRelativeOffset:X8}|{item.ByteLength:X8}|{item.StableId}|{Hash(item.CopyBefore())}|{Hash(item.CopyAfter())}"))));

    private static Id65RewardStagingStationaryReadback BuildStationaryReadback(
        byte[] source,
        byte[] output,
        Id65RewardStagingManifest manifest)
    {
        List<byte[]> sourceRows = [];
        List<byte[]> outputRows = [];
        List<byte[]> properties = [];
        bool onlyXyz = true;
        foreach (Id65RewardStagingStationaryIntent item in manifest.Stationary)
        {
            int rowOffset = ObjectTableOffset + (item.TrueIndex * ObjectRecordByteLength);
            byte[] before = source.AsSpan(rowOffset, ObjectRecordByteLength).ToArray();
            byte[] after = output.AsSpan(rowOffset, ObjectRecordByteLength).ToArray();
            sourceRows.Add(before);
            outputRows.Add(after);
            properties.Add(output.AsSpan(SceneOffset + (int)item.PropertiesSceneOffset,
                item.PropertiesByteLength).ToArray());
            onlyXyz &= before.AsSpan(0, 0x0C).SequenceEqual(after.AsSpan(0, 0x0C)) &&
                before.AsSpan(0x18).SequenceEqual(after.AsSpan(0x18)) &&
                after.AsSpan(0x0C, 12).SequenceEqual(EncodePoint(item.Point)) &&
                source.AsSpan(SceneOffset + (int)item.PropertiesSceneOffset, item.PropertiesByteLength)
                    .SequenceEqual(output.AsSpan(SceneOffset + (int)item.PropertiesSceneOffset,
                        item.PropertiesByteLength));
        }
        byte[] joinedProperties = Join(properties);
        Id65RewardStagingStationaryReadback result = new(
            manifest.Stationary.Count,
            manifest.Stationary.Sum(item => item.RewardValue),
            manifest.Stationary.Count(item =>
                item.OwnerSemantics == Id65RewardOwnerSemantics.StationaryLooseGem),
            manifest.Stationary.Where(item =>
                item.OwnerSemantics == Id65RewardOwnerSemantics.StationaryLooseGem).Sum(item => item.RewardValue),
            manifest.Stationary.Count(item =>
                item.OwnerSemantics == Id65RewardOwnerSemantics.StationaryChestCarrier),
            manifest.Stationary.Where(item =>
                item.OwnerSemantics == Id65RewardOwnerSemantics.StationaryChestCarrier).Sum(item => item.RewardValue),
            Hash(Encoding.UTF8.GetBytes(string.Join(',', manifest.Stationary.Select(item => item.TrueIndex)))),
            Hash(Join(sourceRows)),
            Hash(Join(outputRows)),
            joinedProperties.Length,
            Hash(joinedProperties),
            onlyXyz,
            manifest.Stationary.Select(item => item.TrueIndex).SequenceEqual(
                manifest.Stationary.Select(item => item.TrueIndex).Order()),
            manifest.Stationary.All(item =>
                item.RetirementByteIndex == item.TrueIndex / 8 &&
                item.RetirementBitMask == (byte)(1 << (item.TrueIndex % 8))));
        if (result.OwnerCount != 71 || result.RewardTotal != 171 ||
            result.LooseGemOwnerCount != 43 || result.LooseGemRewardTotal != 73 ||
            result.ChestCarrierOwnerCount != 28 || result.ChestCarrierRewardTotal != 98 ||
            result.SourceRowsSha256 != ExpectedStationarySourceRowsSha256 ||
            result.OutputRowsSha256 != ExpectedStationaryOutputRowsSha256 ||
            result.PropertiesByteLength != 0x5D4 ||
            result.PropertiesSha256 != ExpectedStationaryPropertiesSha256 ||
            !result.OnlyXyzMutated || !result.TrueIndexesPreserved ||
            !result.RetirementAddressesPreserved)
        {
            throw new InvalidDataException("The 71-owner/171 stationary semantic readback changed.");
        }
        return result;
    }

    private static Id65RewardStagingDragonReadback[] BuildDragonReadback(
        byte[] source,
        byte[] output,
        Id65RewardStagingManifest manifest)
    {
        int[] expectedChanged = [2_576, 2_672, 3_269, 928];
        int[] expectedRuns = [1_104, 1_002, 1_401, 348];
        (int X, int Y)[] expectedEndpoints =
            [(9_216, 13_472), (15_628, 16_819), (8_295, 17_664), (15_360, 12_551)];
        bool[] expectedPrecinctContainment = [true, false, true, true];
        List<byte[]> allOutputRows = [];
        Id65RewardStagingDragonReadback[] result = new Id65RewardStagingDragonReadback[4];
        for (int bundleIndex = 0; bundleIndex < manifest.Dragons.Count; bundleIndex++)
        {
            Id65RewardStagingDragonIntent item = manifest.Dragons[bundleIndex];
            int[] trueIndexes = [item.PedestalTrueIndex, item.DragonTrueIndex, item.ContainerTrueIndex];
            byte[][] sourceRows = trueIndexes.Select(index =>
                source.AsSpan(ObjectTableOffset + (index * ObjectRecordByteLength), ObjectRecordByteLength).ToArray())
                .ToArray();
            byte[][] outputRows = trueIndexes.Select(index =>
                output.AsSpan(ObjectTableOffset + (index * ObjectRecordByteLength), ObjectRecordByteLength).ToArray())
                .ToArray();
            allOutputRows.AddRange(outputRows);
            bool uniform = true;
            bool nonCoordinates = true;
            int changed = 0;
            int runs = 0;
            for (int rowIndex = 0; rowIndex < 3; rowIndex++)
            {
                uniform &= ReadPoint(outputRows[rowIndex], 0x0C) ==
                    Translate(ReadPoint(sourceRows[rowIndex], 0x0C), item.Translation);
                nonCoordinates &= sourceRows[rowIndex].AsSpan(0, 0x0C)
                        .SequenceEqual(outputRows[rowIndex].AsSpan(0, 0x0C)) &&
                    sourceRows[rowIndex].AsSpan(0x18).SequenceEqual(outputRows[rowIndex].AsSpan(0x18));
                AddLeafDiff(sourceRows[rowIndex].AsSpan(0x0C, 12), outputRows[rowIndex].AsSpan(0x0C, 12),
                    ref changed, ref runs);
            }

            ReadOnlySpan<byte> sourceCamera = source.AsSpan(item.CameraDataRelativeOffset, item.CameraDataByteLength);
            ReadOnlySpan<byte> outputCamera = output.AsSpan(item.CameraDataRelativeOffset, item.CameraDataByteLength);
            uniform &= ReadPoint(output, item.CameraDataRelativeOffset) ==
                Translate(ReadPoint(source, item.CameraDataRelativeOffset), item.Translation);
            nonCoordinates &= sourceCamera.Slice(0x0C).SequenceEqual(outputCamera.Slice(0x0C));
            AddLeafDiff(sourceCamera.Slice(0, 12), outputCamera.Slice(0, 12), ref changed, ref runs);

            ReadOnlySpan<byte> sourceTrack = source.AsSpan(item.CameraTrackRelativeOffset, item.CameraTrackByteLength);
            ReadOnlySpan<byte> outputTrack = output.AsSpan(item.CameraTrackRelativeOffset, item.CameraTrackByteLength);
            for (int frame = 0; frame < item.CameraTrackFrameCount; frame++)
            {
                int offset = frame * 0x18;
                uniform &= ReadPoint(outputTrack.Slice(offset, 12).ToArray(), 0) ==
                    Translate(ReadPoint(sourceTrack.Slice(offset, 12).ToArray(), 0), item.Translation);
                nonCoordinates &= sourceTrack.Slice(offset + 12, 12)
                    .SequenceEqual(outputTrack.Slice(offset + 12, 12));
                AddLeafDiff(sourceTrack.Slice(offset, 12), outputTrack.Slice(offset, 12), ref changed, ref runs);
            }

            DragonRunToEndpoint endpoint = DragonRescueRunTo.DecodeEndpoint(
                item.Actor.X, item.Actor.Y, item.RunToAngle, item.RunToRadius);
            bool insidePrecinct = endpoint.RawX >= item.PrecinctMinimum.X &&
                endpoint.RawX <= item.PrecinctMaximum.X &&
                endpoint.RawY >= item.PrecinctMinimum.Y &&
                endpoint.RawY <= item.PrecinctMaximum.Y;
            bool insideCore = endpoint.RawX >= 4_352 && endpoint.RawX <= 19_712 &&
                endpoint.RawY >= 4_352 && endpoint.RawY <= 19_712;
            bool linkPreserved = source.AsSpan(item.SceneLinkRelativeOffset, item.SceneLinkByteLength)
                .SequenceEqual(output.AsSpan(item.SceneLinkRelativeOffset, item.SceneLinkByteLength));
            result[bundleIndex] = new(
                item.Id,
                item.PedestalTrueIndex,
                item.DragonTrueIndex,
                item.ContainerTrueIndex,
                Hash(outputRows[0]),
                Hash(outputRows[1]),
                Hash(outputRows[2]),
                Hash(Join(outputRows)),
                Hash(outputCamera),
                Hash(outputTrack),
                item.CameraTrackFrameCount,
                checked((item.CameraTrackFrameCount + 4) * 12),
                changed,
                runs,
                endpoint.RawX,
                endpoint.RawY,
                insidePrecinct,
                insideCore,
                uniform,
                nonCoordinates,
                linkPreserved);
            Id65RewardStagingDragonReadback readback = result[bundleIndex];
            if (readback.OutputPedestalRowSha256 != ExpectedDragonPedestalOutputRowSha256[bundleIndex] ||
                readback.OutputDragonRowSha256 != ExpectedDragonActorOutputRowSha256[bundleIndex] ||
                readback.OutputContainerRowSha256 != ExpectedDragonContainerOutputRowSha256[bundleIndex] ||
                readback.OutputBundleSha256 != ExpectedDragonOutputBundleSha256[bundleIndex] ||
                readback.OutputCameraDataSha256 != ExpectedDragonOutputCameraSha256[bundleIndex] ||
                readback.OutputCameraTrackSha256 != ExpectedDragonOutputTrackSha256[bundleIndex] ||
                readback.ChangedByteCount != expectedChanged[bundleIndex] ||
                readback.DiffRangeCount != expectedRuns[bundleIndex] ||
                readback.RunToEndpointRawX != expectedEndpoints[bundleIndex].X ||
                readback.RunToEndpointRawY != expectedEndpoints[bundleIndex].Y ||
                readback.RunToEndpointInsideNamedPrecinct != expectedPrecinctContainment[bundleIndex] ||
                !readback.RunToEndpointInsideCore4 || !readback.UniformTranslationApplied ||
                !readback.NonCoordinateBytesPreserved || !readback.SceneLinkPreserved)
            {
                throw new InvalidDataException($"{item.Id} translated bundle/camera/route readback changed.");
            }
        }
        if (result.Sum(item => item.OwnedByteCount) != 15_420 ||
            result.Sum(item => item.ChangedByteCount) != 9_445 ||
            result.Sum(item => item.DiffRangeCount) != 3_855 ||
            result.All(item => item.RunToEndpointInsideNamedPrecinct) ||
            result.Count(item => !item.RunToEndpointInsideNamedPrecinct) != 1 ||
            result.Single(item => !item.RunToEndpointInsideNamedPrecinct).Id != "dragon-b" ||
            Hash(Join(allOutputRows)) != ExpectedDragonOutputRowsSha256)
        {
            throw new InvalidDataException("The four-dragon aggregate or rejected named-precinct route claim changed.");
        }
        return result;
    }

    private static void AddLeafDiff(
        ReadOnlySpan<byte> source,
        ReadOnlySpan<byte> output,
        ref int changed,
        ref int runs)
    {
        if (source.Length != output.Length)
            throw new InvalidDataException("A dragon diff leaf changed length.");
        bool inRun = false;
        for (int index = 0; index < source.Length; index++)
        {
            bool differs = source[index] != output[index];
            if (differs)
            {
                changed++;
                if (!inRun) runs++;
            }
            inRun = differs;
        }
    }

    private static Id65RewardStagingZeroEggReadback BuildZeroEggReadback(
        byte[] source,
        byte[] output)
    {
        string sourceFixups = Hash(source.AsSpan(FixupCountOffset, FixupActiveByteLength));
        string outputFixups = Hash(output.AsSpan(FixupCountOffset, FixupActiveByteLength));
        string sourceOrphan = Hash(source.AsSpan(PrivateThiefBlockOffset, PrivateThiefBlockByteLength));
        string outputOrphan = Hash(output.AsSpan(PrivateThiefBlockOffset, PrivateThiefBlockByteLength));
        Id65RewardStagingZeroEggReadback result = new(
            RequiredEggTarget: 0,
            ReplacedTrueIndex: 88,
            ActorClass: BinaryPrimitives.ReadUInt16LittleEndian(
                output.AsSpan(T88RowOffset + 0x36, 2)),
            ActorRootCountBefore: 37,
            ActorRootCountAfter: 38,
            ObjectCountBefore: BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(ObjectCountOffset, 4)),
            ObjectCountAfter: BinaryPrimitives.ReadInt32LittleEndian(output.AsSpan(ObjectCountOffset, 4)),
            FixupCountBefore: BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(FixupCountOffset, 4)),
            FixupCountAfter: BinaryPrimitives.ReadInt32LittleEndian(output.AsSpan(FixupCountOffset, 4)),
            ActorTailBytesRemaining: SceneOffset - (GrassPackageOffset + GrassPackageByteLength),
            SceneTailBytesRemaining: SceneOffset + SceneByteLength - (GrassPropertiesOffset + 8),
            OutputRowSha256: Hash(output.AsSpan(T88RowOffset, ObjectRecordByteLength)),
            ActorPackageSha256: Hash(output.AsSpan(GrassPackageOffset, GrassPackageByteLength)),
            PropertiesSha256: Hash(output.AsSpan(GrassPropertiesOffset, 8)),
            OrphanedPrivateBlockSha256: outputOrphan,
            ActiveFixupComponentSha256: outputFixups,
            OrphanedPrivateBlockPreserved: sourceOrphan == outputOrphan,
            ActiveFixupsPreserved: sourceFixups == outputFixups);
        if (result.RequiredEggTarget != 0 || result.ReplacedTrueIndex != 88 ||
            result.ActorClass != GrassActorId || result.ActorRootCountBefore != 37 ||
            result.ActorRootCountAfter != 38 || result.ObjectCountBefore != ObjectRecordCount ||
            result.ObjectCountAfter != ObjectRecordCount || result.FixupCountBefore != 0x81 ||
            result.FixupCountAfter != 0x81 || result.ActorTailBytesRemaining != 0x448 ||
            result.SceneTailBytesRemaining != 0x6C0 || result.OutputRowSha256 != ExpectedZeroEggRowSha256 ||
            result.ActorPackageSha256 != ExpectedGrassPackageSha256 ||
            result.PropertiesSha256 != ExpectedGrassPropertiesSha256 ||
            result.OrphanedPrivateBlockSha256 != ExpectedPrivateThiefBlockSha256 ||
            result.ActiveFixupComponentSha256 != Id65RewardCensusContract.ScenePointerFixupSha256 ||
            !result.OrphanedPrivateBlockPreserved || !result.ActiveFixupsPreserved ||
            BinaryPrimitives.ReadUInt32LittleEndian(output.AsSpan(Root37Offset, 4)) != GrassPackagePointer ||
            BinaryPrimitives.ReadUInt16LittleEndian(output.AsSpan(ActorId37Offset, 2)) != GrassActorId)
        {
            throw new InvalidDataException("The ZeroEgg root/package/T88/properties/fixup readback changed.");
        }
        return result;
    }

    private static Id65RewardStagingStructuralReadback BuildStructuralReadback(
        byte[] source,
        byte[] output,
        byte[] overlay,
        byte[] executable,
        Id65SupportDesiredStateProjection support)
    {
        Id65RewardStagingStructuralReadback result = new(
            Hash(output.AsSpan(0, 0x200)),
            Hash(output.AsSpan(ActorSubfileOffset, ActorSubfileByteLength)),
            Hash(output.AsSpan(SceneOffset, SceneByteLength)),
            Hash(output.AsSpan(ObjectTableOffset, ObjectRecordCount * ObjectRecordByteLength)),
            Hash(output.AsSpan(ModelOffset, ModelByteLength)),
            Hash(Join(
            [
                output.AsSpan(0, ModelOffset).ToArray(),
                output.AsSpan(ModelOffset + ModelByteLength).ToArray()
            ])),
            Hash(Join(
            [
                output.AsSpan(0, TexturePagesOffset).ToArray(),
                output.AsSpan(ActorSubfileOffset).ToArray()
            ])),
            BinaryPrimitives.ReadInt32LittleEndian(output.AsSpan(ObjectCountOffset, 4)),
            ActorRootCount: 38,
            BinaryPrimitives.ReadInt32LittleEndian(output.AsSpan(FixupCountOffset, 4)),
            support.Readback.SectorCount,
            support.Readback.ActiveCollisionCellCount,
            support.Readback.CollisionBlocksUsedBytes,
            support.Capacity.TextureCount,
            support.Capacity.HighestTextureId,
            OverlayPreserved: Hash(overlay) == Id65RewardCensusContract.Id65OverlaySha256,
            ExecutablePreserved: Hash(executable) == Id65RewardCensusContract.ExecutableSha256);
        if (result.HeaderSha256 != "6157dc52dbf53f744378aa5577209751dc75eb397401504ba23a28f1d5dabd71" ||
            result.ActorSubfileSha256 != "e5e0f898a2d9487d56da9b28c1fb53706df219380ce5831f79334b06ebd1118f" ||
            result.SceneSubfileSha256 != "09f343a9fc670e5709ea8d28fd347546bce46480a23d1636ff07d054b2426438" ||
            result.ObjectTableSha256 != "b2c3fc1e2857485f53adf03e679246a9f761f62a034a416a75d654f1520f3372" ||
            result.ModelSha256 != Id65AuthoringSupportReplacementCompiler.ExpectedCoreModelSha256 ||
            result.OutsideModelRowSha256 != ExpectedOutsideModelRowSha256 ||
            result.OutsideTexturePagesAndModelRowSha256 !=
                ExpectedOutsideTexturePagesAndModelRowSha256 ||
            result.ObjectCount != 107 || result.ActorRootCount != 38 || result.FixupCount != 0x81 ||
            result.CoreSectorCount != 4 || result.ActiveCollisionCellCount != 16 ||
            result.CollisionBlocksUsedBytes != 0x82 || result.TextureCount != 67 ||
            result.HighestTextureId != 66 || !result.OverlayPreserved || !result.ExecutablePreserved ||
            !source.AsSpan(PrivateThiefBlockOffset, PrivateThiefBlockByteLength)
                .SequenceEqual(output.AsSpan(PrivateThiefBlockOffset, PrivateThiefBlockByteLength)))
        {
            throw new InvalidDataException(
                $"The final Core4 reward structural readback changed: header={result.HeaderSha256}; " +
                $"actor={result.ActorSubfileSha256}; scene={result.SceneSubfileSha256}; " +
                $"objects={result.ObjectTableSha256}; model={result.ModelSha256}; " +
                $"outside-model={result.OutsideModelRowSha256}; " +
                $"outside-texture-pages-and-model={result.OutsideTexturePagesAndModelRowSha256}.");
        }
        return result;
    }

    private static void ValidateCompiled(
        Id65CompiledRewardStaging compiled,
        Id65SupportDesiredStateProjection support)
    {
        if (compiled.ProfileId != ProfileId ||
            compiled.Manifest.Kind != Id65RewardStagingProfileKind.Core4Stationary171 ||
            compiled.SourceRow80Sha256 != Id65RewardCensusContract.Id65DataSha256 ||
            compiled.TargetOverlaySha256 != Id65RewardCensusContract.Id65OverlaySha256 ||
            compiled.GlobalExecutableSha256 != Id65RewardCensusContract.ExecutableSha256 ||
            compiled.OwnedLeafCount != ExpectedOwnedLeafCount ||
            compiled.GuardedByteCount != ExpectedGuardedByteCount ||
            compiled.OwnedRanges.Count != compiled.InverseRanges.Count ||
            HashOwnedRanges(compiled.OwnedRanges) != compiled.OwnedRangeMapSha256 ||
            HashOwnedRanges(compiled.InverseRanges) != compiled.InverseRangeMapSha256 ||
            HashTransaction(compiled.OwnedRanges) != compiled.TransactionSha256 ||
            support.CanonicalSha256 != Id65AuthoringSupportReplacementCompiler.ExpectedCoreDesiredProjectionSha256 ||
            compiled.Dragons.Count != 4 || compiled.Dragons.Sum(item => item.OwnedByteCount) != 15_420 ||
            compiled.Dragons.Sum(item => item.ChangedByteCount) != 9_445 ||
            compiled.Dragons.Sum(item => item.DiffRangeCount) != 3_855 ||
            compiled.Dragons.All(item => item.RunToEndpointInsideNamedPrecinct) ||
            compiled.Dragons.Count(item => !item.RunToEndpointInsideNamedPrecinct) != 1 ||
            compiled.Dragons.Single(item => !item.RunToEndpointInsideNamedPrecinct).Id != "dragon-b" ||
            !compiled.Dragons.All(item => item.RunToEndpointInsideCore4) ||
            !compiled.BuildsDirectlyFromLockedSourceOnce || compiled.AcceptsCompiledSupportAfterimage ||
            compiled.AcceptsCompiledCompositeAfterimage || compiled.AcceptsCompiledMobyAfterimage ||
            compiled.CallsRetiredPublisher || compiled.WriterAuthorized || compiled.WritesFileSystem ||
            compiled.WritesDiscImage || compiled.WritesBin || compiled.WritesCue ||
            compiled.EnemyRoutesProven || compiled.ChestChildBehaviorProven || compiled.RuntimeProven ||
            compiled.RuntimeCandidateAuthorized || compiled.AllDragonRoutesContainedByNamedPrecincts ||
            compiled.TreasureTotal200Proven || compiled.SavePersistenceProven || compiled.SaveAuthorized ||
            compiled.MusicLongPlayProven || compiled.ExitDestination65Proven ||
            compiled.PromotionAuthorized || compiled.AppIntegrated || compiled.CreateBinEnabled ||
            compiled.NormalCreateBinEnabled || compiled.ReleaseAuthorized || compiled.Publishable ||
            !compiled.Full8Excluded || !compiled.ExecutableMutationExcluded ||
            compiled.AfterimageStackingAuthorized || !compiled.ExactInverseVerified ||
            !compiled.ProtectedComplementVerified || !compiled.DeterministicReadbackVerified)
        {
            throw new InvalidDataException("The compiled reward-staging identity or fixed-false safety boundary changed.");
        }
        RequireFrozenString(compiled.ManifestSha256, ExpectedCoreManifestSha256, "Core4 reward manifest");
        RequireFrozenString(compiled.DescriptorFullFieldSha256,
            ExpectedGrassDescriptorFullFieldSha256, "Grass descriptor full-field identity");
        RequireFrozenString(compiled.OutputRow80Sha256, ExpectedOutputRow80Sha256, "Core4 reward output row 80");
        RequireFrozenInt(compiled.ChangedByteCount, ExpectedChangedByteCount, "Core4 changed-byte count");
        RequireFrozenInt(compiled.DiffRangeCount, ExpectedDiffRangeCount, "Core4 diff-range count");
        RequireFrozenString(compiled.DiffManifestSha256, ExpectedDiffManifestSha256, "Core4 diff manifest");
        RequireFrozenString(compiled.OwnedRangeMapSha256, ExpectedOwnedRangeMapSha256, "Core4 owned-range map");
        RequireFrozenString(compiled.InverseRangeMapSha256, ExpectedInverseRangeMapSha256, "Core4 inverse map");
        RequireFrozenString(compiled.TransactionSha256, ExpectedTransactionSha256, "Core4 transaction");
        RequireFrozenString(compiled.ProtectedComplementSha256,
            ExpectedProtectedComplementSha256, "Core4 protected complement");
        RequireFrozenString(compiled.DeterministicPlanSha256,
            ExpectedDeterministicPlanSha256, "Core4 deterministic plan");
    }

    private static void RequireFrozenString(string actual, string expected, string label)
        => RequireHash(actual, expected, label);

    private static void RequireFrozenInt(int actual, int expected, string label)
    {
        if (actual != expected)
            throw new InvalidDataException($"The {label} is {actual}, expected {expected}.");
    }

    private static void ValidateRequestEnvelope(Id65RewardStagingCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request.Source);
        ArgumentNullException.ThrowIfNull(request.Manifest);
        ArgumentNullException.ThrowIfNull(request.Census);
        ArgumentNullException.ThrowIfNull(request.Layout);
        ArgumentNullException.ThrowIfNull(request.CoreSupportManifest);
        ArgumentNullException.ThrowIfNull(request.FullSupportManifest);
        ArgumentNullException.ThrowIfNull(request.GrassBundle);
        Id65RewardStagingCapacity limits = request.Capacity ?? new();
        ValidateLockedSource(request.Source);
        ValidateCapacity(limits);
        Id65RewardSupportLayout canonical = Id65RewardSupportLayoutContract.Build(
            request.Census, request.CoreSupportManifest, request.FullSupportManifest);
        RequireEquivalentLayout(request.Layout, canonical);
        ValidateGrassBundle(
            request.GrassBundle,
            request.Source.CopyTargetOverlay(),
            request.Source.CopyGlobalExecutable());
        ValidateManifest(
            request.Manifest,
            request.Census,
            request.Layout,
            request.CoreSupportManifest,
            request.FullSupportManifest,
            request.GrassBundle);
        if (request.AuthorMobileEnemies || request.Manifest.AuthorMobileEnemies)
            throw new InvalidDataException("Mobile enemy authoring is fail-closed; Full8 contains reservations only.");
        if (request.Manifest.Kind != Id65RewardStagingProfileKind.Core4Stationary171)
            throw new InvalidDataException("Full8ReservationsOnly is desired capacity, not a compilable authoring profile.");
    }

    private static void ValidateLockedSource(Id65RewardStagingLockedSource source)
    {
        if (source.Row80ByteLength != Row80ByteLength || source.TargetOverlayByteLength != OverlayByteLength ||
            source.GlobalExecutableByteLength != ExecutableByteLength ||
            source.Row80Sha256 != Id65RewardCensusContract.Id65DataSha256 ||
            source.TargetOverlaySha256 != Id65RewardCensusContract.Id65OverlaySha256 ||
            source.GlobalExecutableSha256 != Id65RewardCensusContract.ExecutableSha256 ||
            source.TextureWitnessSha256 != Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256)
        {
            throw new InvalidDataException("The captured locked source identity or no-afterimage envelope changed.");
        }
        byte[] row = source.CopyRow80();
        byte[] overlay = source.CopyTargetOverlay();
        byte[] executable = source.CopyGlobalExecutable();
        RequireHash(Hash(row), Id65RewardCensusContract.Id65DataSha256, "copied locked row 80");
        RequireHash(Hash(overlay), Id65RewardCensusContract.Id65OverlaySha256, "copied locked overlay");
        RequireHash(Hash(executable), Id65RewardCensusContract.ExecutableSha256, "copied locked executable");
        _ = Id65AuthoringSupportReplacementCompiler.CaptureLockedSource(row, source.TextureWitness);
    }

    private static void ValidateCapacity(Id65RewardStagingCapacity capacity)
    {
        if (capacity.Row80ByteLength != Row80ByteLength || capacity.ModelByteCapacity != ModelByteLength ||
            capacity.MaximumObjectCount != ObjectRecordCount || capacity.MaximumActorRootCount != 38 ||
            capacity.MaximumFixupCount != 0x81 || capacity.ActorSubfileEnd != SceneOffset ||
            capacity.SceneSubfileEnd != SceneByteLength || capacity.RetirementOwnerMaximumTrueIndex != 255 ||
            capacity.CameraTrackEnd != 0x2E1FE8)
        {
            throw new InvalidDataException("Reward-staging capacity must equal the frozen Core4 envelope exactly.");
        }
    }

    private static void ValidateManifest(
        Id65RewardStagingManifest manifest,
        Id65RewardCensus census,
        Id65RewardSupportLayout layout,
        Id65AuthoringSupportManifest coreSupportManifest,
        Id65AuthoringSupportManifest fullSupportManifest,
        Id65MobyDependencyBundleDescriptor grassBundle)
    {
        string recomputed = manifest.RecomputeCanonicalJson();
        string recomputedHash = Hash(Encoding.UTF8.GetBytes(recomputed));
        string supportHash = manifest.Kind == Id65RewardStagingProfileKind.Core4Stationary171
            ? coreSupportManifest.CanonicalSha256
            : fullSupportManifest.CanonicalSha256;
        if (manifest.ProfileId != ProfileId || manifest.SchemaVersion != 1 ||
            manifest.CanonicalJson != recomputed || manifest.CanonicalSha256 != recomputedHash ||
            manifest.CensusSha256 != census.DeterministicCensusSha256 ||
            manifest.CensusSha256 != Id65RewardCensusContract.ExpectedDeterministicCensusSha256 ||
            manifest.LayoutSha256 != layout.DeterministicLayoutSha256 ||
            manifest.LayoutSha256 != Id65RewardSupportLayoutContract.ExpectedDeterministicLayoutSha256 ||
            manifest.SupportManifestSha256 != supportHash ||
            manifest.GrassBundleSha256 != grassBundle.CanonicalSha256 ||
            manifest.GrassBundleSha256 != Id65AuthoringMobyDependencyBundleCompiler.ExpectedArtisansGrassBundleSha256 ||
            manifest.Stationary.Count != ExpectedStationaryOwnerCount ||
            manifest.Stationary.Sum(item => item.RewardValue) != ExpectedStationaryRewardTotal ||
            manifest.Dragons.Count != 4 ||
            manifest.MobileReservations.Count != ExpectedMobileReservationCount ||
            manifest.MobileReservations.Sum(item => item.RewardValue) != ExpectedMobileReservationRewardTotal ||
            manifest.MobileReservations.Any(item => !item.ReservationOnly || item.RoutePinned ||
                item.ActivationBoundsPinned || item.RuntimeAccepted) ||
            manifest.AuthorMobileEnemies)
        {
            throw new InvalidDataException("The canonical reward-staging manifest or fail-closed boundary changed.");
        }
        Id65RewardStagingManifest expected = CreateManifestWithoutRecursiveValidation(
            manifest.Kind, census, layout, coreSupportManifest, fullSupportManifest, grassBundle);
        if (manifest.CanonicalJson != expected.CanonicalJson || manifest.CanonicalSha256 != expected.CanonicalSha256)
            throw new InvalidDataException("The reward-staging descriptor values differ from the exact desired layout.");
    }

    private static Id65RewardStagingManifest CreateManifestWithoutRecursiveValidation(
        Id65RewardStagingProfileKind kind,
        Id65RewardCensus census,
        Id65RewardSupportLayout layout,
        Id65AuthoringSupportManifest coreSupportManifest,
        Id65AuthoringSupportManifest fullSupportManifest,
        Id65MobyDependencyBundleDescriptor grassBundle)
    {
        Id65RewardStagingStationaryIntent[] stationary = layout.StationaryPlacements
            .OrderBy(item => item.Owner.TrueIndex)
            .Select(item => new Id65RewardStagingStationaryIntent(
                item.Slot.SlotIndex, item.Owner.TrueIndex, item.Owner.ActorClass, item.Owner.Encoding,
                item.Owner.OwnerSemantics, item.Owner.RewardValue, item.Owner.RowSha256,
                item.Owner.PropertiesSceneOffset, item.Owner.PropertiesByteLength, item.Owner.PropertiesSha256,
                item.Owner.RetirementByteIndex, item.Owner.RetirementBitMask, item.Slot.Point))
            .ToArray();
        Id65RewardStagingMobileReservation[] mobile = layout.EnemyReservations
            .OrderBy(item => item.Owner.TrueIndex)
            .Select(item => new Id65RewardStagingMobileReservation(
                item.Id, item.Kind, item.Owner.TrueIndex, item.Owner.ActorClass, item.Owner.RewardValue,
                item.Owner.RowSha256, item.Owner.PropertiesSceneOffset, item.Owner.PropertiesByteLength,
                item.Owner.PropertiesSha256, item.Point, item.ReservationOnly, item.RoutePinned,
                item.ActivationBoundsPinned, item.RuntimeAccepted))
            .ToArray();
        return new(
            kind,
            census.DeterministicCensusSha256,
            layout.DeterministicLayoutSha256,
            kind == Id65RewardStagingProfileKind.Core4Stationary171
                ? coreSupportManifest.CanonicalSha256
                : fullSupportManifest.CanonicalSha256,
            grassBundle.CanonicalSha256,
            stationary,
            BuildDragonIntents(census, layout),
            mobile,
            authorMobileEnemies: false);
    }

    private static void RequireEquivalentLayout(
        Id65RewardSupportLayout supplied,
        Id65RewardSupportLayout canonical)
    {
        Id65RewardSupportLayoutContract.ValidateReadback(supplied);
        if (supplied.DeterministicLayoutSha256 != canonical.DeterministicLayoutSha256 ||
            JsonSerializer.Serialize(supplied, CanonicalJsonOptions) !=
                JsonSerializer.Serialize(canonical, CanonicalJsonOptions))
        {
            throw new InvalidDataException("The supplied reward-support layout differs from its canonical rebuild.");
        }
    }

    private static void ValidateGrassBundle(
        Id65MobyDependencyBundleDescriptor bundle,
        byte[]? targetOverlay,
        byte[]? globalExecutable)
    {
        if (bundle.Id != Id65AuthoringMobyDependencyBundleCompiler.ArtisansGrassBundleId ||
            bundle.SchemaVersion != 1 ||
            bundle.CanonicalSha256 != Id65AuthoringMobyDependencyBundleCompiler.ExpectedArtisansGrassBundleSha256 ||
            Hash(Encoding.UTF8.GetBytes(bundle.CanonicalJson)) != bundle.CanonicalSha256 ||
            bundle.Rows.Count != 1 || bundle.Properties.Count != 1 || bundle.ActorPackages.Count != 1 ||
            bundle.ActorRoots.Count != 1 || bundle.Fixups.Count != 1 || bundle.ExternalDependencies.Count != 2)
        {
            throw new InvalidDataException("The exact Artisans Grass bundle identity or cardinality changed.");
        }
        Id65MobyRowTemplate row = bundle.Rows.Single();
        Id65MobyPropertiesAsset properties = bundle.Properties.Single();
        Id65MobyActorPackageAsset package = bundle.ActorPackages.Single();
        Id65MobyActorRootRequirement root = bundle.ActorRoots.Single();
        Id65MobySceneFixupPolicy fixup = bundle.Fixups.Single();
        if (row.Id != Id65AuthoringMobyDependencyBundleCompiler.ArtisansGrassRowTemplateId ||
            row.SourceLevelKey != "artisans" || row.SourceTrueIndex != 121 || row.ActorId != GrassActorId ||
            row.PropertiesAssetId != Id65AuthoringMobyDependencyBundleCompiler.ArtisansGrassPropertiesAssetId ||
            Convert.FromHexString(row.ExactHex).Length != ObjectRecordByteLength ||
            Hash(Convert.FromHexString(row.ExactHex)) != row.Sha256 ||
            row.ScenePointerFields.Count != 1 || row.ScenePointerFields[0] != new Id65MobyFieldSpan(0, 4, "scene-relative-properties") ||
            row.MutablePlacementFields.Count != 2 ||
            row.MutablePlacementFields[0] != new Id65MobyFieldSpan(0x0C, 12, "raw-xyz") ||
            row.MutablePlacementFields[1] != new Id65MobyFieldSpan(0x47, 1, "yaw") ||
            properties.Id != Id65AuthoringMobyDependencyBundleCompiler.ArtisansGrassPropertiesAssetId ||
            properties.Alignment != 4 || properties.InternalScenePointerFieldOffsets.Count != 0 ||
            Hash(Convert.FromHexString(properties.ExactHex)) != ExpectedGrassPropertiesSha256 ||
            properties.Sha256 != ExpectedGrassPropertiesSha256 ||
            package.Id != Id65AuthoringMobyDependencyBundleCompiler.ArtisansGrassPackageAssetId ||
            package.ActorId != GrassActorId || package.SourceRootIndex != 22 || package.Alignment != 4 ||
            package.EntryRelativePointerFieldOffsets.Count != 0 ||
            Convert.FromHexString(package.ExactHex).Length != GrassPackageByteLength ||
            Hash(Convert.FromHexString(package.ExactHex)) != ExpectedGrassPackageSha256 ||
            package.Sha256 != ExpectedGrassPackageSha256 ||
            root.ActorId != GrassActorId || root.PackageAssetId != package.Id ||
            root.AllocationPolicy != Id65MobyAssetAllocationPolicy.InstallDeduplicated || root.ResidentRootIndex.HasValue ||
            !fixup.RegisterRowPropertiesFields || fixup.RegisterInternalPropertiesFields ||
            !fixup.PreserveExistingOrder || !fixup.RejectDuplicateFields)
        {
            throw new InvalidDataException("A full-field Grass row/property/package/root/fixup identity changed.");
        }
        Id65MobyExternalDependencyIntent overlayDependency = bundle.ExternalDependencies.Single(item =>
            item.Kind == Id65MobyExternalDependencyKind.TargetOverlay);
        Id65MobyExternalDependencyIntent executableDependency = bundle.ExternalDependencies.Single(item =>
            item.Kind == Id65MobyExternalDependencyKind.GlobalExecutable);
        if (overlayDependency.Policy != Id65MobyExternalDependencyPolicy.PreserveExact ||
            overlayDependency.ByteLength != OverlayByteLength ||
            overlayDependency.PreimageSha256 != Id65RewardCensusContract.Id65OverlaySha256 ||
            executableDependency.Policy != Id65MobyExternalDependencyPolicy.PreserveExact ||
            executableDependency.ByteLength != ExecutableByteLength ||
            executableDependency.PreimageSha256 != Id65RewardCensusContract.ExecutableSha256)
        {
            throw new InvalidDataException("Grass overlay/executable preserve-exact dependencies changed.");
        }
        if (targetOverlay is not null)
            RequireHash(Hash(targetOverlay), overlayDependency.PreimageSha256, "Grass target-overlay dependency");
        if (globalExecutable is not null)
            RequireHash(Hash(globalExecutable), executableDependency.PreimageSha256, "Grass executable dependency");
    }

    private static void RequireHash(string actual, string expected, string label)
    {
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The {label} SHA-256 is {actual}, expected {expected}.");
    }
}
