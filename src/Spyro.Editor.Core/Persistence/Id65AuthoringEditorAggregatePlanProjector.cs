using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Persistence;

internal enum Id65AuthoringEditorAggregateRangeDomain
{
    NativeTexturePages,
    CompositeModel,
    Spawn,
    Grass
}

internal enum Id65AuthoringEditorAggregateCompiledTreatment
{
    AuthorExact,
    PreserveLockedBase
}

internal enum Id65AuthoringEditorAggregateEditPolicy
{
    Blocked
}

internal sealed record Id65AuthoringEditorAggregateDestinationBinding(
    string SourceSnapshotProfileId,
    string SourceImageSha256,
    string LockedBaseProfileId,
    string LockedBaseImageSha256,
    string WadFileName,
    int TargetOverlayWadEntry,
    long TargetOverlayWadOffset,
    int TargetOverlayByteLength,
    int TargetDataWadEntry,
    long TargetDataWadOffset,
    int TargetDataByteLength,
    string TargetDataDirectoryRowSha256,
    long TargetObjectTableWadOffset,
    long TargetObjectTableRelativeOffset,
    int RejectedRetailOverlayWadEntry,
    long RejectedRetailOverlayWadOffset,
    int RejectedRetailDataWadEntry,
    long RejectedRetailDataWadOffset,
    string RejectedRetailDataDirectoryRowSha256,
    int TextureDonorWadEntry,
    int GrassDonorWadEntry,
    bool PhysicalSnapshotVerified,
    bool RetailTownSquareDestinationRejected,
    bool DonorLocationsReadOnlyProvenance,
    bool PhysicalDestinationRevalidationRequired);

internal sealed record Id65AuthoringEditorAggregateProtectedRange(
    int DataRelativeOffset,
    int ByteLength,
    string SegmentSha256);

internal sealed record Id65AuthoringEditorAggregateProtectedModelComponent(
    string StableId,
    int SourceRelativeOffset,
    int SourceByteLength,
    int OutputRelativeOffset,
    int OutputByteLength,
    string ContentSha256);

internal sealed record Id65AuthoringEditorAggregateBaselineOwnership(
    string StableId,
    string Kind,
    Id65LogicalAddress Source,
    Id65LogicalAddress Output,
    Id65AuthoringEditorAggregateCompiledTreatment CompiledTreatment,
    Id65AuthoringEditorAggregateEditPolicy EditorEditPolicy);

internal sealed record Id65AuthoringEditorAggregateAtomicGroup(
    string GroupId,
    int OwnedRangeCount,
    string StableIdSetSha256,
    bool AllRangesRequired,
    bool IndependentlyApplicable);

internal sealed record Id65AuthoringEditorAggregatePlanRequest(
    Id65AuthoringEditorDocumentStoreResult StoredDocument,
    UnusedLevel65BlankLevelLabManifest LabManifest,
    Id65AuthoringEditorNativeInputProvider.InputSet NativeInputs)
{
    internal Id65AuthoringEditorAggregateBoundSource BoundSource =>
        NativeInputs.BoundSource;
    internal Id65V2NativeTextureWitness PrivateTextureWitness =>
        NativeInputs.PrivateTextureWitness;
    internal Id65MobyDependencyBundleDescriptor GrassBundle =>
        NativeInputs.GrassBundle;
}

/// <summary>
/// Exact row-80/overlay/executable capture tied to the physically inspected
/// locked image directory entries. The factory owns the physical census read so
/// callers cannot substitute declared snapshot metadata or an unrelated raw row.
/// </summary>
internal sealed class Id65AuthoringEditorAggregateBoundSource
{
    private readonly Id65AuthoringCompositeLockedSource _lockedSource;

    private Id65AuthoringEditorAggregateBoundSource(
        string sourceImageSha256,
        Id65AuthoringCompositeLockedSource lockedSource)
    {
        SourceImageSha256 = sourceImageSha256;
        _lockedSource = lockedSource;
    }

    public string SnapshotProfileId => Id65RewardCensusContract.ProfileId;
    public string SourceImageSha256 { get; }
    public int SectorSize => 2352;
    public int UserDataOffset => 24;
    public int WadLba => 37;
    public int WadByteLength => 0x6C18800;
    public int ExecutableLba => 55_382;
    public int TargetOverlayWadEntry => 79;
    public long TargetOverlayWadOffset => 0x6927000;
    public int TargetDataWadEntry => 80;
    public long TargetDataWadOffset => 0x6936800;
    public string TargetDataDirectoryRowSha256 =>
        "3c6ae9564c3f6fc794bbae9670ee0dff7bd9a7bdc0f96c3c206c752551d8c09a";
    public int RejectedRetailOverlayWadEntry => 15;
    public long RejectedRetailOverlayWadOffset => 0x118E800;
    public int RejectedRetailDataWadEntry => 16;
    public long RejectedRetailDataWadOffset => 0x119E000;
    public string RejectedRetailDataDirectoryRowSha256 =>
        "ba8329f16f9caed1bed39c6b794968ff95160adbfd2480c5d4bbb6fc7545f77b";
    public bool PhysicalDirectorySnapshotVerified => true;
    public bool RetailCloneComparedAndExcluded => true;
    public bool CurrentOnDiskFreshnessVerified => false;

    internal Id65AuthoringCompositeLockedSource LockedSource => _lockedSource;

    public static Id65AuthoringEditorAggregateBoundSource Capture(string lockedImagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockedImagePath);
        return CaptureVerifiedSnapshot(Id65RewardCensusContract.ReadLockedSource(lockedImagePath));
    }

    private static Id65AuthoringEditorAggregateBoundSource CaptureVerifiedSnapshot(
        Id65RewardCensusSourceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        byte[] executable = snapshot.Executable.ToArray();
        byte[] id65Overlay = snapshot.Id65Overlay.ToArray();
        byte[] id65Data = snapshot.Id65Data.ToArray();
        byte[] retailOverlay = snapshot.RetailTownSquareOverlay.ToArray();
        byte[] retailData = snapshot.RetailTownSquareData.ToArray();
        if (string.IsNullOrWhiteSpace(snapshot.SourceImagePath) ||
            !Path.IsPathFullyQualified(snapshot.SourceImagePath) ||
            snapshot.SourceImageSha256 != Id65RewardCensusContract.LockedSourceImageSha256 ||
            snapshot.SectorSize != 2352 || snapshot.UserDataOffset != 24 ||
            snapshot.WadLba != 37 || snapshot.WadByteLength != 0x6C18800 ||
            snapshot.ExecutableLba != 55_382 || executable.Length != 0x66000 ||
            id65Overlay.Length != 0xF800 || id65Data.Length != 0x2E2000 ||
            retailOverlay.Length != 0xF800 || retailData.Length != 0x2E2000 ||
            Id65AuthoringEditorAggregatePlanProjector.Hash(executable) !=
                Id65RewardCensusContract.ExecutableSha256 ||
            Id65AuthoringEditorAggregatePlanProjector.Hash(id65Overlay) !=
                Id65RewardCensusContract.Id65OverlaySha256 ||
            Id65AuthoringEditorAggregatePlanProjector.Hash(id65Data) !=
                Id65RewardCensusContract.Id65DataSha256 ||
            !id65Overlay.SequenceEqual(retailOverlay) || !id65Data.SequenceEqual(retailData))
        {
            throw new InvalidDataException(
                "The aggregate bound source is not the exact physically mapped WAD80 snapshot with retail WAD15/16 comparison.");
        }
        Id65AuthoringCompositeLockedSource locked =
            Id65AuthoringCompositeCompiler.CaptureLockedSource(id65Data, id65Overlay, executable);
        return new(snapshot.SourceImageSha256, locked);
    }
}

/// <summary>
/// One defensive copy of a single leaf in the exact aggregate transaction.
/// These leaves are descriptive parts of one indivisible transaction. They are
/// not an authorization to apply a subset of the plan.
/// </summary>
internal sealed class Id65AuthoringEditorAggregateOwnedRange
{
    private readonly byte[] _before;
    private readonly byte[] _after;

    internal Id65AuthoringEditorAggregateOwnedRange(
        string stableId,
        string ownerId,
        Id65AuthoringEditorAggregateRangeDomain domain,
        int dataRelativeOffset,
        byte[] before,
        byte[] after)
    {
        StableId = stableId;
        OwnerId = ownerId;
        Domain = domain;
        DataRelativeOffset = dataRelativeOffset;
        _before = before.ToArray();
        _after = after.ToArray();
        BeforeSha256 = Id65AuthoringEditorAggregatePlanProjector.Hash(_before);
        AfterSha256 = Id65AuthoringEditorAggregatePlanProjector.Hash(_after);
    }

    public string StableId { get; }
    public string OwnerId { get; }
    public Id65AuthoringEditorAggregateRangeDomain Domain { get; }
    public int TargetWadEntry => 80;
    public int DataRelativeOffset { get; }
    public long WadOffset => 0x6936800L + DataRelativeOffset;
    public int ByteLength => _before.Length;
    public string BeforeSha256 { get; }
    public string AfterSha256 { get; }

    internal byte[] CopyBefore() => _before.ToArray();
    internal byte[] CopyAfter() => _after.ToArray();
    internal Id65AuthoringEditorAggregateOwnedRange DeepCopy() =>
        new(StableId, OwnerId, Domain, DataRelativeOffset, _before, _after);
    internal Id65AuthoringEditorAggregateOwnedRange Invert() =>
        new(StableId, OwnerId, Domain, DataRelativeOffset, _after, _before);
}

/// <summary>
/// Pure, in-memory projection of one exact, full ID65 editor document. It
/// carries the complete atomic owned-range transaction and destination binding
/// but owns no filesystem, app, disc writer, runtime, or release authority.
/// </summary>
internal sealed class Id65AuthoringEditorAggregatePlan
{
    private readonly ReadOnlyCollection<Id65AuthoringEditorAggregateOwnedRange> _ownedRanges;
    private readonly ReadOnlyCollection<Id65AuthoringEditorAggregateOwnedRange> _inverseRanges;
    private readonly ReadOnlyCollection<Id65AuthoringEditorAggregateProtectedRange> _protectedRanges;
    private readonly ReadOnlyCollection<Id65AuthoringEditorAggregateProtectedModelComponent>
        _protectedModelComponents;
    private readonly ReadOnlyCollection<Id65StableHandleRebase> _handleRebases;
    private readonly ReadOnlyCollection<Id65MobyAssetRelocation> _mobyAssetRelocations;
    private readonly ReadOnlyCollection<Id65MobyPlacementReadback> _mobyPlacements;
    private readonly ReadOnlyCollection<Id65AuthoringEditorAggregateBaselineOwnership>
        _baselineOwnership;
    private readonly ReadOnlyCollection<Id65AuthoringEditorAggregateAtomicGroup> _atomicGroups;

    internal Id65AuthoringEditorAggregatePlan(
        Id65AuthoringEditorAggregateDestinationBinding destination,
        string documentCanonicalSha256,
        string compositeManifestSha256,
        string sourceRow80Sha256,
        string outputRow80Sha256,
        string sourceModelSha256,
        string outputModelSha256,
        string targetOverlaySha256,
        string globalExecutableSha256,
        string textureWitnessSha256,
        string grassBundleSha256,
        IEnumerable<Id65AuthoringEditorAggregateOwnedRange> ownedRanges,
        IEnumerable<Id65AuthoringEditorAggregateOwnedRange> inverseRanges,
        IEnumerable<Id65AuthoringEditorAggregateProtectedRange> protectedRanges,
        IEnumerable<Id65AuthoringEditorAggregateProtectedModelComponent> protectedModelComponents,
        IEnumerable<Id65StableHandleRebase> handleRebases,
        IEnumerable<Id65MobyAssetRelocation> mobyAssetRelocations,
        IEnumerable<Id65MobyPlacementReadback> mobyPlacements,
        IEnumerable<Id65AuthoringEditorAggregateBaselineOwnership> baselineOwnership,
        IEnumerable<Id65AuthoringEditorAggregateAtomicGroup> atomicGroups,
        Id65AuthoringCompositeTerrainReadback terrain,
        Id65AuthoringCompositeTextureReadback texture,
        Id65AuthoringCompositeCapacityReadback capacity,
        int guardedByteCount,
        int protectedByteCount,
        int changedByteCount,
        int diffRangeCount,
        string diffManifestSha256,
        string ownedRangeMapSha256,
        string inverseRangeMapSha256,
        string protectedRangeMapSha256,
        string protectedComplementSha256,
        string protectedModelComponentMapSha256,
        string relocationMapSha256,
        string rebaseMapSha256,
        string mobyAssetRelocationMapSha256,
        string transactionSha256,
        string compositeDeterministicPlanSha256,
        string semanticReadbackSha256,
        string baselineOwnershipSha256,
        string atomicGroupMapSha256,
        string deterministicAggregatePlanSha256)
    {
        Destination = destination;
        DocumentCanonicalSha256 = documentCanonicalSha256;
        CompositeManifestSha256 = compositeManifestSha256;
        SourceRow80Sha256 = sourceRow80Sha256;
        OutputRow80Sha256 = outputRow80Sha256;
        SourceModelSha256 = sourceModelSha256;
        OutputModelSha256 = outputModelSha256;
        TargetOverlaySha256 = targetOverlaySha256;
        GlobalExecutableSha256 = globalExecutableSha256;
        TextureWitnessSha256 = textureWitnessSha256;
        GrassBundleSha256 = grassBundleSha256;
        _ownedRanges = Array.AsReadOnly(ownedRanges.Select(item => item.DeepCopy()).ToArray());
        _inverseRanges = Array.AsReadOnly(inverseRanges.Select(item => item.DeepCopy()).ToArray());
        _protectedRanges = Array.AsReadOnly(protectedRanges.ToArray());
        _protectedModelComponents = Array.AsReadOnly(protectedModelComponents.ToArray());
        _handleRebases = Array.AsReadOnly(handleRebases.ToArray());
        _mobyAssetRelocations = Array.AsReadOnly(mobyAssetRelocations.ToArray());
        _mobyPlacements = Array.AsReadOnly(mobyPlacements.ToArray());
        _baselineOwnership = Array.AsReadOnly(baselineOwnership.ToArray());
        _atomicGroups = Array.AsReadOnly(atomicGroups.ToArray());
        Terrain = terrain;
        Texture = texture;
        Capacity = capacity;
        GuardedByteCount = guardedByteCount;
        ProtectedByteCount = protectedByteCount;
        ChangedByteCount = changedByteCount;
        DiffRangeCount = diffRangeCount;
        DiffManifestSha256 = diffManifestSha256;
        OwnedRangeMapSha256 = ownedRangeMapSha256;
        InverseRangeMapSha256 = inverseRangeMapSha256;
        ProtectedRangeMapSha256 = protectedRangeMapSha256;
        ProtectedComplementSha256 = protectedComplementSha256;
        ProtectedModelComponentMapSha256 = protectedModelComponentMapSha256;
        RelocationMapSha256 = relocationMapSha256;
        RebaseMapSha256 = rebaseMapSha256;
        MobyAssetRelocationMapSha256 = mobyAssetRelocationMapSha256;
        TransactionSha256 = transactionSha256;
        CompositeDeterministicPlanSha256 = compositeDeterministicPlanSha256;
        SemanticReadbackSha256 = semanticReadbackSha256;
        BaselineOwnershipSha256 = baselineOwnershipSha256;
        AtomicGroupMapSha256 = atomicGroupMapSha256;
        DeterministicAggregatePlanSha256 = deterministicAggregatePlanSha256;
    }

    public string ProfileId => Id65AuthoringEditorAggregatePlanProjector.ProfileId;
    public int LevelId => UnusedLevel65BlankLevelLabProfileRegistry.LevelId;
    public int DataWadEntry => UnusedLevel65BlankLevelLabProfileRegistry.DataWadEntry;
    public Id65AuthoringEditorAggregateDestinationBinding Destination { get; }
    public string DocumentCanonicalSha256 { get; }
    public string CompositeManifestSha256 { get; }
    public string SourceRow80Sha256 { get; }
    public string OutputRow80Sha256 { get; }
    public string SourceModelSha256 { get; }
    public string OutputModelSha256 { get; }
    public string TargetOverlaySha256 { get; }
    public string GlobalExecutableSha256 { get; }
    public string TextureWitnessSha256 { get; }
    public string GrassBundleSha256 { get; }
    public IReadOnlyList<Id65AuthoringEditorAggregateOwnedRange> OwnedRanges => _ownedRanges;
    public IReadOnlyList<Id65AuthoringEditorAggregateOwnedRange> InverseRanges => _inverseRanges;
    public IReadOnlyList<Id65AuthoringEditorAggregateProtectedRange> ProtectedRanges => _protectedRanges;
    public IReadOnlyList<Id65AuthoringEditorAggregateProtectedModelComponent>
        ProtectedModelComponents => _protectedModelComponents;
    public IReadOnlyList<Id65StableHandleRebase> HandleRebases => _handleRebases;
    public IReadOnlyList<Id65MobyAssetRelocation> MobyAssetRelocations => _mobyAssetRelocations;
    public IReadOnlyList<Id65MobyPlacementReadback> MobyPlacements => _mobyPlacements;
    public IReadOnlyList<Id65AuthoringEditorAggregateBaselineOwnership>
        BaselineOwnership => _baselineOwnership;
    public IReadOnlyList<Id65AuthoringEditorAggregateAtomicGroup> AtomicGroups => _atomicGroups;
    public Id65AuthoringCompositeTerrainReadback Terrain { get; }
    public Id65AuthoringCompositeTextureReadback Texture { get; }
    public Id65AuthoringCompositeCapacityReadback Capacity { get; }
    public int OwnedRangeCount => _ownedRanges.Count;
    public int InverseRangeCount => _inverseRanges.Count;
    public int ProtectedRangeCount => _protectedRanges.Count;
    public int GuardedByteCount { get; }
    public int ProtectedByteCount { get; }
    public int ChangedByteCount { get; }
    public int DiffRangeCount { get; }
    public string DiffManifestSha256 { get; }
    public string OwnedRangeMapSha256 { get; }
    public string InverseRangeMapSha256 { get; }
    public string ProtectedRangeMapSha256 { get; }
    public string ProtectedComplementSha256 { get; }
    public string ProtectedModelComponentMapSha256 { get; }
    public string RelocationMapSha256 { get; }
    public string RebaseMapSha256 { get; }
    public string MobyAssetRelocationMapSha256 { get; }
    public string TransactionSha256 { get; }
    public string CompositeDeterministicPlanSha256 { get; }
    public string SemanticReadbackSha256 { get; }
    public string BaselineOwnershipSha256 { get; }
    public string AtomicGroupMapSha256 { get; }
    public string DeterministicAggregatePlanSha256 { get; }

    public bool FullExactDocumentRequired => true;
    public bool ExactPhysicalDestinationBindingPinned => true;
    public bool PhysicalDestinationReadbackVerified => true;
    public bool CurrentOnDiskFreshnessVerified => false;
    public bool StoreEnvelopeIdentityIndependentlyVerified => false;
    public bool StoreProjectionHashesReverified => true;
    public bool HighPolySemanticBindingVerified => true;
    public bool LowPolySemanticBindingVerified => true;
    public bool CollisionMembershipVerified => true;
    public bool OcclusionMembershipVerified => true;
    public bool PrivateT66BindingVerified => true;
    public bool GrassT88StaticBindingVerified => true;
    public bool GrassT107StaticBindingVerified => true;
    public bool SpawnAndT92ExactBindingVerified => true;
    public bool ProtectedComplementVerified => true;
    public bool ProtectedModelComponentsVerified => true;
    public bool ExactInverseVerified => true;
    public bool OneAtomicTransactionRequired => true;
    public bool ContainsOwnedAfterimageBytes => true;
    public bool ContainsCompleteCompositeAfterimage => false;
    public bool ContainsDiscImageBytes => false;
    public bool ContainsCueBytes => false;
    public bool PartialRangeApplicationAuthorized => false;
    public bool CrossSliceCompositionSupported => false;
    public bool AfterimageStackingAuthorized => false;
    public bool GenericExporterCompatible => false;
    public bool RetailWriterCompatible => false;
    public bool RetailLevelMutationAuthorized => false;
    public bool RuntimeMobyAcceptanceProven => false;
    public bool RuntimeProven => false;
    public bool PersistenceEnabled => false;
    public bool WriterAuthorized => false;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool WritesBin => false;
    public bool WritesCue => false;
    public bool AppIntegrated => false;
    public bool UiIntegrated => false;
    public bool CreateBinEnabled => false;
    public bool NormalCreateBinEnabled => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool SaveAuthorized => false;
    public bool SavePersistenceProven => false;
    public bool TreasureTotal200Proven => false;
    public bool MusicLongPlayProven => false;
    public bool ExitDestination65Proven => false;
    public bool ReleaseIntegrated => false;
    public bool ReleaseAuthorized => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;
    public bool RequiresTrustedExclusiveWorkspaceDirectory => true;
    public bool HostileFilesystemRaceSafe => false;
    public bool CommonWorkspaceLeaseOwned => false;
}

/// <summary>
/// Reprojects a genuine store readback into a complete, immutable ID65
/// aggregate transaction. The output remains an offline proof and cannot write
/// a workspace, BIN/CUE, retail row, app project, or release artifact.
/// </summary>
internal static class Id65AuthoringEditorAggregatePlanProjector
{
    public const string ProfileId =
        "id65-authoring-editor-aggregate-plan-full-exact-static-in-memory-v1";
    public const string ExpectedFullDocumentCanonicalSha256 =
        "8f27bfc3f97238402c0b7dd59a081335ae19df1acfaefa9575d9ca7110dce808";
    public const string ExpectedFullDocumentContentSha256 =
        "9d853ccfcf18627a46dc730844efc9eeeda28ee9a0d53c70ae3b7144652b4c3b";
    public const int ExpectedRow80ByteLength = 0x2E2000;
    public const int ExpectedOwnedRangeCount = 7_961;
    public const int ExpectedTexturePageRangeCount = 7_949;
    public const int ExpectedProtectedRangeCount = 7_962;
    public const int ExpectedGuardedByteCount = 1_011_040;
    public const int ExpectedProtectedByteCount = 2_011_808;
    public const int ExpectedProtectedModelComponentCount = 5;
    public const int ExpectedHandleRebaseCount = 31;
    public const int ExpectedMobyAssetRelocationCount = 5;
    public const int ExpectedMobyPlacementCount = 2;
    public const string ExpectedInverseRangeMapSha256 =
        "53b90cfc4e5e2f47d35a7d89e262f72019792aa75042126d06c0e773e2714403";
    public const string ExpectedProtectedRangeMapSha256 =
        "99bc34839258714970e7e1814dbeea5ff29253cc667a991f4fea733e2da713ea";
    public const string ExpectedProtectedComplementSha256 =
        "df136489ddcd940fb8b1d815654ce8bfdbca131514afa250a4e68d665a1f3492";
    public const string ExpectedProtectedModelComponentMapSha256 =
        "9fe8013390fa78e992e5bd3991c10a193462d33a8e50984a56298bcbead71e8e";
    public const string ExpectedSemanticReadbackSha256 =
        "cad3f37a8c566a793dce35723f9b709a6ef65ac1132ea01bac4bc441bf800786";
    public const string ExpectedBaselineOwnershipSha256 =
        "7bbdb7f6e95fa9cfd4c30df356528200b62243d9dd9e3448a06b48e20480ece4";
    public const string ExpectedAtomicGroupMapSha256 =
        "9250dc3e10e598247a1b1104c9f7a12f50cc3131fc1f8d307d5ec6efca9e4793";
    public const string ExpectedDeterministicAggregatePlanSha256 =
        "4955c54b3eb7ea5c1871205e559f5d5dda0766613e55aa0de988b92e896d5c21";

    private const int TargetOverlayWadEntry = 79;
    private const long TargetOverlayWadOffset = 0x6927000;
    private const int TargetOverlayByteLength = 0xF800;
    private const long TargetDataWadOffset = 0x6936800;
    private const int RetailTownSquareOverlayWadEntry = 15;
    private const long RetailTownSquareOverlayWadOffset = 0x118E800;
    private const int RetailTownSquareDataWadEntry = 16;
    private const long RetailTownSquareDataWadOffset = 0x119E000;
    private const int TextureDonorWadEntry = 70;
    private const int GrassDonorWadEntry = 10;
    private const int ModelOffset = 0xDE800;
    private const int ModelByteLength = 0x94800;

    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly string[] ExactMobyRangeStableIds =
    [
        "asset.root37",
        "asset.actor-id37",
        "asset.package.01f5",
        "scene.object-count",
        "placement.t88",
        "placement.t107",
        "scene.fixup-count",
        "scene.fixup.t107-properties",
        "asset.properties.grass"
    ];

    public static Id65AuthoringEditorAggregatePlan Project(
        Id65AuthoringEditorAggregatePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.StoredDocument);
        ArgumentNullException.ThrowIfNull(request.LabManifest);
        ArgumentNullException.ThrowIfNull(request.NativeInputs);
        Id65AuthoringEditorNativeInputProvider.ValidateForConsumption(request.NativeInputs);
        ArgumentNullException.ThrowIfNull(request.BoundSource);
        ArgumentNullException.ThrowIfNull(request.PrivateTextureWitness);
        ArgumentNullException.ThrowIfNull(request.GrassBundle);

        ValidateExactLabManifest(request.LabManifest);
        Id65AuthoringEditorDocument document = ValidateStoredDocument(request.StoredDocument);
        ValidateInputIdentity(request, document);

        Id65AuthoringEditorProjection projection =
            Id65AuthoringEditorDocumentProjector.Project(new(
                document,
                request.BoundSource.LockedSource,
                request.PrivateTextureWitness,
                request.GrassBundle));
        Id65CompiledAuthoringComposite compiled = projection.Compiled;
        ValidateFreshProjection(request.StoredDocument, projection, compiled);

        byte[] source = compiled.CopySourceRow80();
        byte[] output = compiled.CopyOutputRow80();
        Id65AuthoringEditorAggregateOwnedRange[] owned = compiled.OwnedRanges
            .Select(ToAggregateRange)
            .ToArray();
        TransactionPartition partition = ValidateAndBuildPartition(source, output, owned);
        Id65AuthoringEditorAggregateOwnedRange[] inverse = owned
            .Reverse()
            .Select(item => item.Invert())
            .ToArray();
        ValidateInverse(source, output, inverse);

        Id65AuthoringEditorAggregateProtectedModelComponent[] protectedModel =
            BuildProtectedModelComponents(compiled);
        Id65AuthoringEditorAggregateBaselineOwnership[] baseline =
            BuildBaselineOwnership(document, compiled);
        Id65AuthoringEditorAggregateAtomicGroup[] atomicGroups = BuildAtomicGroups(owned);
        Id65AuthoringEditorAggregateDestinationBinding destination =
            BuildDestinationBinding(request.LabManifest, request.BoundSource);

        string ownedHash = HashOwnedRanges(owned);
        string inverseHash = HashOwnedRanges(inverse);
        string protectedRangeHash = HashProtectedRanges(partition.ProtectedRanges);
        string protectedModelHash = HashProtectedModelComponents(protectedModel);
        string baselineHash = HashBaselineOwnership(baseline);
        string atomicGroupHash = HashAtomicGroups(atomicGroups);
        string semanticHash = Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            compiled.Terrain,
            compiled.Texture,
            compiled.Capacity,
            compiled.HandleRebases,
            compiled.MobyAssetRelocations,
            compiled.MobyPlacements
        }, CanonicalJsonOptions)));
        string aggregateHash = Hash(Encoding.UTF8.GetBytes(string.Join('\n',
        [
            ProfileId,
            document.CanonicalSha256,
            request.StoredDocument.CompositeManifestSha256,
            compiled.SourceRow80Sha256,
            compiled.OutputRow80Sha256,
            compiled.SourceModelSha256,
            compiled.OutputModelSha256,
            compiled.TargetOverlaySha256,
            compiled.GlobalExecutableSha256,
            request.PrivateTextureWitness.CanonicalSha256,
            request.GrassBundle.CanonicalSha256,
            destination.TargetDataWadEntry.ToString(CultureInfo.InvariantCulture),
            destination.TargetDataWadOffset.ToString("X", CultureInfo.InvariantCulture),
            destination.TargetDataDirectoryRowSha256,
            destination.SourceImageSha256,
            destination.RejectedRetailDataWadEntry.ToString(CultureInfo.InvariantCulture),
            destination.RejectedRetailDataWadOffset.ToString("X", CultureInfo.InvariantCulture),
            destination.RejectedRetailDataDirectoryRowSha256,
            owned.Length.ToString(CultureInfo.InvariantCulture),
            partition.GuardedByteCount.ToString(CultureInfo.InvariantCulture),
            partition.ProtectedByteCount.ToString(CultureInfo.InvariantCulture),
            compiled.ChangedByteCount.ToString(CultureInfo.InvariantCulture),
            compiled.DiffRangeCount.ToString(CultureInfo.InvariantCulture),
            compiled.DiffManifestSha256,
            ownedHash,
            inverseHash,
            protectedRangeHash,
            partition.ProtectedComplementSha256,
            protectedModelHash,
            compiled.RelocationMapSha256,
            compiled.RebaseMapSha256,
            compiled.MobyAssetRelocationMapSha256,
            compiled.TransactionSha256,
            compiled.DeterministicPlanSha256,
            semanticHash,
            baselineHash,
            atomicGroupHash
        ])));

        Id65AuthoringEditorAggregatePlan plan = new(
            destination,
            document.CanonicalSha256,
            compiled.ManifestSha256,
            compiled.SourceRow80Sha256,
            compiled.OutputRow80Sha256,
            compiled.SourceModelSha256,
            compiled.OutputModelSha256,
            compiled.TargetOverlaySha256,
            compiled.GlobalExecutableSha256,
            request.PrivateTextureWitness.CanonicalSha256,
            request.GrassBundle.CanonicalSha256,
            owned,
            inverse,
            partition.ProtectedRanges,
            protectedModel,
            compiled.HandleRebases,
            compiled.MobyAssetRelocations,
            compiled.MobyPlacements,
            baseline,
            atomicGroups,
            compiled.Terrain,
            compiled.Texture,
            compiled.Capacity,
            partition.GuardedByteCount,
            partition.ProtectedByteCount,
            compiled.ChangedByteCount,
            compiled.DiffRangeCount,
            compiled.DiffManifestSha256,
            ownedHash,
            inverseHash,
            protectedRangeHash,
            partition.ProtectedComplementSha256,
            protectedModelHash,
            compiled.RelocationMapSha256,
            compiled.RebaseMapSha256,
            compiled.MobyAssetRelocationMapSha256,
            compiled.TransactionSha256,
            compiled.DeterministicPlanSha256,
            semanticHash,
            baselineHash,
            atomicGroupHash,
            aggregateHash);
        ValidatePlan(plan);
        return plan;
    }

    internal static byte[] ApplyExact(
        Id65AuthoringEditorAggregatePlan plan,
        byte[] exactRow80,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(exactRow80);
        string expectedInputHash = reverse ? plan.OutputRow80Sha256 : plan.SourceRow80Sha256;
        string expectedOutputHash = reverse ? plan.SourceRow80Sha256 : plan.OutputRow80Sha256;
        if (exactRow80.Length != ExpectedRow80ByteLength || Hash(exactRow80) != expectedInputHash)
            throw new InvalidDataException("The aggregate transaction exact whole-row preimage changed.");
        IReadOnlyList<Id65AuthoringEditorAggregateOwnedRange> ranges = reverse
            ? plan.InverseRanges
            : plan.OwnedRanges;
        byte[] output = ApplyAllRanges(exactRow80, ranges);
        if (Hash(output) != expectedOutputHash)
            throw new InvalidDataException("The aggregate transaction exact whole-row result changed.");
        return output;
    }

    internal static void ValidatePartitionForSmoke(
        byte[] source,
        byte[] output,
        IEnumerable<Id65AuthoringEditorAggregateOwnedRange> ownedRanges)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(ownedRanges);
        Id65AuthoringEditorAggregateOwnedRange[] owned = ownedRanges
            .Select(item => item.DeepCopy())
            .ToArray();
        TransactionPartition partition = ValidateAndBuildPartition(source, output, owned);
        ValidateInverse(source, output, owned.Reverse().Select(item => item.Invert()).ToArray());
        if (partition.GuardedByteCount + partition.ProtectedByteCount != ExpectedRow80ByteLength)
            throw new InvalidDataException("The aggregate transaction partition does not cover row 80 exactly.");
    }

    private static void ValidateExactLabManifest(UnusedLevel65BlankLevelLabManifest manifest)
    {
        if (manifest.ManifestSchemaVersion != UnusedLevel65BlankLevelLabProfileRegistry.ManifestSchemaVersion ||
            manifest.ProfileId != UnusedLevel65BlankLevelLabProfileRegistry.ProfileId ||
            manifest.ProfileVersion != UnusedLevel65BlankLevelLabProfileRegistry.ProfileVersion ||
            manifest.WorkspaceKey != UnusedLevel65BlankLevelLabProfileRegistry.Key ||
            manifest.WorkspaceLayoutVersion !=
                UnusedLevel65BlankLevelLabProfileRegistry.WorkspaceLayoutVersion ||
            manifest.WorkspaceLayoutRelativeRoot !=
                UnusedLevel65BlankLevelLabProfileRegistry.WorkspaceLayoutRelativeRoot ||
            manifest.DisplayName != UnusedLevel65BlankLevelLabProfileRegistry.Profile.DisplayName ||
            manifest.LevelId != UnusedLevel65BlankLevelLabProfileRegistry.LevelId ||
            manifest.DataWadEntry != UnusedLevel65BlankLevelLabProfileRegistry.DataWadEntry ||
            manifest.ObjectTableWadOffset != UnusedLevel65BlankLevelLabProfileRegistry.ObjectTableWadOffset ||
            manifest.ObjectTableRelativeOffset !=
                UnusedLevel65BlankLevelLabProfileRegistry.ObjectTableRelativeOffset ||
            manifest.ObjectRecordCount != UnusedLevel65BlankLevelLabProfileRegistry.ObjectRecordCount ||
            manifest.LockedBaseImageRelativePath != "locked-base/unusedlevel65blank-locked-base-v1.bin" ||
            manifest.LockedBaseCueRelativePath != "locked-base/unusedlevel65blank-locked-base-v1.cue" ||
            manifest.AuthoredEditsRelativePath != "authored-edits" ||
            manifest.CleanSourceImageSha256 != UnusedLevel65BlankLevelLabProfileRegistry.CleanUsaImageSha256 ||
            manifest.PhysicalCloneProfileId !=
                UnusedLevel65BlankLevelLabProfileRegistry.Profile.PhysicalCloneProfileId ||
            manifest.PhysicalCloneImageSha256 !=
                UnusedLevel65BlankLevelLabProfileRegistry.Profile.PhysicalCloneImageSha256 ||
            manifest.PhysicalCloneEvidence != UnusedLevel65BlankLevelLabProfileRegistry.PhysicalCloneEvidence ||
            manifest.LockedBaseProfileId !=
                UnusedLevel65BlankLevelLabProfileRegistry.Profile.LockedBaseProfileId ||
            manifest.LockedBaseImageSha256 !=
                UnusedLevel65BlankLevelLabProfileRegistry.Profile.LockedBaseImageSha256 ||
            manifest.LockedBaseEvidence != UnusedLevel65BlankLevelLabProfileRegistry.LockedBaseEvidence ||
            manifest.Capabilities != UnusedLevel65BlankLevelLabProfileRegistry.Capabilities ||
            !manifest.ExternalGameDataRequired || manifest.GameDataEmbedded ||
            manifest.PromotionAuthorized || manifest.NormalCreateBinEnabled)
        {
            throw new InvalidDataException(
                "The aggregate plan lab manifest is stale, path-divergent, promoted, or not the exact ID65 WAD80 workspace.");
        }
    }

    private static Id65AuthoringEditorDocument ValidateStoredDocument(
        Id65AuthoringEditorDocumentStoreResult stored)
    {
        Id65AuthoringEditorDocument exact = Id65AuthoringEditorDocument.CreateExact(
            includeOptionalTerrain: true,
            placementIds:
            [
                Id65AuthoringMobyDependencyBundleCompiler.T88PlacementId,
                Id65AuthoringMobyDependencyBundleCompiler.T107PlacementId
            ]);
        Id65AuthoringEditorDocument reparsed =
            Id65AuthoringEditorDocument.Parse(stored.Document.SerializeUtf8());
        if (!reparsed.SerializeUtf8().SequenceEqual(exact.SerializeUtf8()) ||
            reparsed.CanonicalSha256 != ExpectedFullDocumentCanonicalSha256 ||
            reparsed.ContentSha256 != ExpectedFullDocumentContentSha256 ||
            stored.DocumentCanonicalSha256 != reparsed.CanonicalSha256 ||
            !IsCanonicalSha256(stored.EnvelopeCanonicalSha256) ||
            stored.ProjectionContractId != Id65AuthoringEditorDocumentStore.ProjectionContractId ||
            stored.CompositeProfileId != Id65AuthoringCompositeCompiler.ProfileId ||
            stored.CompositeManifestSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedCombinedManifestSha256 ||
            stored.CompositeOutputRow80Sha256 !=
                Id65AuthoringCompositeCompiler.ExpectedCombinedOutputRow80Sha256 ||
            stored.CompositeDeterministicPlanSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedCombinedDeterministicPlanSha256 ||
            stored.ContainsCompositeAfterimage || stored.ContainsDiscImageBytes || stored.ContainsCueBytes ||
            stored.WritesDiscImage || stored.WritesCue || stored.AppIntegrated ||
            stored.CreateBinEnabled || stored.NormalCreateBinEnabled ||
            stored.RuntimeCandidateAuthorized || stored.ReleaseIntegrated ||
            stored.PromotionAuthorized || stored.Publishable ||
            !stored.RequiresTrustedExclusiveWorkspaceDirectory || stored.HostileFilesystemRaceSafe ||
            stored.CommonWorkspaceLeaseOwned)
        {
            throw new InvalidDataException(
                "The aggregate plan requires one genuine full-document store readback with exact frozen composite pins.");
        }
        return reparsed;
    }

    private static void ValidateInputIdentity(
        Id65AuthoringEditorAggregatePlanRequest request,
        Id65AuthoringEditorDocument document)
    {
        Id65AuthoringCompositeLockedSource source = request.BoundSource.LockedSource;
        if (request.BoundSource.SnapshotProfileId != Id65RewardCensusContract.ProfileId ||
            request.BoundSource.SourceImageSha256 != request.LabManifest.LockedBaseImageSha256 ||
            request.BoundSource.TargetDataWadEntry != 80 ||
            request.BoundSource.TargetDataWadOffset != 0x6936800 ||
            request.BoundSource.RejectedRetailDataWadEntry != 16 ||
            request.BoundSource.RejectedRetailDataWadOffset != 0x119E000 ||
            !request.BoundSource.PhysicalDirectorySnapshotVerified ||
            !request.BoundSource.RetailCloneComparedAndExcluded ||
            request.BoundSource.CurrentOnDiskFreshnessVerified ||
            request.NativeInputs.CanonicalInputSetSha256 !=
                Id65AuthoringEditorNativeInputProvider.ExpectedCanonicalInputSetSha256 ||
            source.Row80ByteLength != ExpectedRow80ByteLength ||
            source.TargetOverlayByteLength != TargetOverlayByteLength ||
            source.GlobalExecutableByteLength != 0x66000 ||
            source.Row80Sha256 != Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 ||
            source.TargetOverlaySha256 !=
                Id65AuthoringCompositeCompiler.ExpectedTargetOverlaySha256 ||
            source.GlobalExecutableSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedGlobalExecutableSha256 ||
            request.PrivateTextureWitness.CanonicalSha256 !=
                Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256 ||
            request.PrivateTextureWitness.DonorWadEntry != TextureDonorWadEntry ||
            request.PrivateTextureWitness.PrivateTextureId !=
                Id65V2NativeTextureCompositionCompiler.PrivateTextureId ||
            request.GrassBundle.CanonicalSha256 !=
                Id65AuthoringMobyDependencyBundleCompiler.ExpectedArtisansGrassBundleSha256 ||
            document.PrivateTextureWitnessSha256 != request.PrivateTextureWitness.CanonicalSha256 ||
            document.MobyBundleSha256 != request.GrassBundle.CanonicalSha256)
        {
            throw new InvalidDataException(
                "The aggregate plan source, T66 witness, or Grass descriptor identity changed.");
        }
    }

    private static void ValidateFreshProjection(
        Id65AuthoringEditorDocumentStoreResult stored,
        Id65AuthoringEditorProjection projection,
        Id65CompiledAuthoringComposite compiled)
    {
        if (!projection.Document.SerializeUtf8().SequenceEqual(stored.Document.SerializeUtf8()) ||
            projection.DocumentCanonicalSha256 != stored.DocumentCanonicalSha256 ||
            !projection.DocumentIdentityVerified || !projection.TerrainHandlesVerified ||
            !projection.CollisionMembershipVerified || !projection.OcclusionMembershipVerified ||
            !projection.PrivateTextureBindingVerified || !projection.MobyPlacementBindingVerified ||
            !projection.BaselineOwnershipHandlesVerified || !projection.SemanticReadbackVerified ||
            !projection.ExactFactoriesOnly || projection.WritesFileSystem || projection.WritesDiscImage ||
            projection.WritesCue || projection.AppIntegrated || projection.CreateBinEnabled ||
            projection.NormalCreateBinEnabled || projection.RuntimeCandidateAuthorized ||
            projection.ReleaseIntegrated || projection.PromotionAuthorized || projection.Publishable ||
            compiled.ProfileId != stored.CompositeProfileId ||
            compiled.ManifestSha256 != stored.CompositeManifestSha256 ||
            compiled.OutputRow80Sha256 != stored.CompositeOutputRow80Sha256 ||
            compiled.DeterministicPlanSha256 != stored.CompositeDeterministicPlanSha256 ||
            compiled.SourceRow80Sha256 != Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 ||
            compiled.SourceModelSha256 != Id65AuthoringCompositeCompiler.ExpectedLockedModelSha256 ||
            compiled.OutputModelSha256 != Id65AuthoringCompositeCompiler.ExpectedCombinedModelSha256 ||
            compiled.ChangedByteCount != Id65AuthoringCompositeCompiler.ExpectedCombinedChangedByteCount ||
            compiled.DiffRangeCount != Id65AuthoringCompositeCompiler.ExpectedCombinedDiffRangeCount ||
            compiled.DiffManifestSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedCombinedDiffManifestSha256 ||
            compiled.OwnedRangeMapSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedCombinedOwnedRangeMapSha256 ||
            compiled.RelocationMapSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedCombinedRelocationMapSha256 ||
            compiled.RebaseMapSha256 != Id65AuthoringCompositeCompiler.ExpectedCombinedRebaseMapSha256 ||
            compiled.MobyAssetRelocationMapSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedCombinedMobyAssetRelocationMapSha256 ||
            compiled.TransactionSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedCombinedTransactionSha256 ||
            compiled.TargetOverlaySha256 != Id65AuthoringCompositeCompiler.ExpectedTargetOverlaySha256 ||
            compiled.GlobalExecutableSha256 != Id65AuthoringCompositeCompiler.ExpectedGlobalExecutableSha256 ||
            compiled.OwnedRanges.Count != ExpectedOwnedRangeCount ||
            compiled.HandleRebases.Count != ExpectedHandleRebaseCount ||
            compiled.MobyAssetRelocations.Count != ExpectedMobyAssetRelocationCount ||
            compiled.MobyPlacements.Count != ExpectedMobyPlacementCount ||
            !compiled.BuildsDirectlyFromLockedSource || !compiled.SinglePassCompositeRebuildOwned ||
            compiled.AcceptsCompiledSliceAfterimages || compiled.AcceptsSlicePlans ||
            compiled.CrossSliceCompositionSupported || !compiled.AppliesOneNonOverlappingLedger ||
            !compiled.ExactInverseVerified || !compiled.DeterministicReadbackRequired ||
            !compiled.ProtectedRetailEntriesPreserved || !compiled.TargetOverlayPreservedExact ||
            !compiled.GlobalExecutablePreservedExact || !compiled.ExecutableMutationExcluded ||
            compiled.InverseIsAuthoringInput || compiled.WritesFileSystem || compiled.WritesDiscImage ||
            compiled.WritesCue || compiled.RetiredPublisherCalled || compiled.AppIntegrated ||
            compiled.CreateBinEnabled || compiled.NormalCreateBinEnabled ||
            compiled.RuntimeCandidateAuthorized || compiled.ReleaseIntegrated ||
            compiled.PromotionAuthorized || compiled.Publishable)
        {
            throw new InvalidDataException(
                "The fresh aggregate projection lost a full-composite pin, semantic proof, or fail-closed boundary.");
        }
    }

    private static Id65AuthoringEditorAggregateOwnedRange ToAggregateRange(
        Id65AuthoringCompositeOwnedRange source)
    {
        Id65AuthoringEditorAggregateRangeDomain domain = source.StableId switch
        {
            string id when id.StartsWith("texture.page.", StringComparison.Ordinal) =>
                Id65AuthoringEditorAggregateRangeDomain.NativeTexturePages,
            "model.final" => Id65AuthoringEditorAggregateRangeDomain.CompositeModel,
            "spawn.landing-xy" or "spawn.t92-player-anchor-xy" =>
                Id65AuthoringEditorAggregateRangeDomain.Spawn,
            string id when ExactMobyRangeStableIds.Contains(id, StringComparer.Ordinal) =>
                Id65AuthoringEditorAggregateRangeDomain.Grass,
            _ => throw new InvalidDataException(
                $"Composite range '{source.StableId}' has no exact aggregate domain.")
        };
        return new(
            source.StableId,
            source.OwnerId,
            domain,
            source.DataRelativeOffset,
            source.CopyBefore(),
            source.CopyAfter());
    }

    private static TransactionPartition ValidateAndBuildPartition(
        byte[] source,
        byte[] output,
        IReadOnlyList<Id65AuthoringEditorAggregateOwnedRange> owned)
    {
        if (source.Length != ExpectedRow80ByteLength || output.Length != ExpectedRow80ByteLength)
            throw new InvalidDataException("The aggregate transaction row-80 lengths changed.");
        if (owned.Count != ExpectedOwnedRangeCount ||
            owned.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count() != owned.Count ||
            HashOwnedRanges(owned) !=
                Id65AuthoringCompositeCompiler.ExpectedCombinedOwnedRangeMapSha256)
            throw new InvalidDataException("The aggregate transaction owned-range identity set changed.");

        int guardedByteCount = 0;
        int cursor = 0;
        List<Id65AuthoringEditorAggregateProtectedRange> protectedRanges = [];
        using IncrementalHash complementHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (int index = 0; index < owned.Count; index++)
        {
            Id65AuthoringEditorAggregateOwnedRange range = owned[index];
            if (string.IsNullOrWhiteSpace(range.StableId) || string.IsNullOrWhiteSpace(range.OwnerId) ||
                range.DataRelativeOffset < cursor || range.ByteLength <= 0 ||
                range.DataRelativeOffset + (long)range.ByteLength > source.Length ||
                range.TargetWadEntry != 80 ||
                range.WadOffset != TargetDataWadOffset + range.DataRelativeOffset)
                throw new InvalidDataException("The aggregate transaction ranges overlap, are unsorted, or are out of bounds.");
            if (range.DataRelativeOffset > cursor)
            {
                int length = range.DataRelativeOffset - cursor;
                ReadOnlySpan<byte> before = source.AsSpan(cursor, length);
                ReadOnlySpan<byte> after = output.AsSpan(cursor, length);
                if (!before.SequenceEqual(after))
                    throw new InvalidDataException($"The protected aggregate complement changed at row+0x{cursor:X}.");
                string segmentSha256 = Hash(before);
                protectedRanges.Add(new(cursor, length, segmentSha256));
                complementHash.AppendData(before);
            }

            byte[] beforeBytes = range.CopyBefore();
            byte[] afterBytes = range.CopyAfter();
            if (beforeBytes.Length != range.ByteLength || afterBytes.Length != range.ByteLength ||
                range.BeforeSha256 != Hash(beforeBytes) || range.AfterSha256 != Hash(afterBytes) ||
                !source.AsSpan(range.DataRelativeOffset, range.ByteLength).SequenceEqual(beforeBytes) ||
                !output.AsSpan(range.DataRelativeOffset, range.ByteLength).SequenceEqual(afterBytes) ||
                beforeBytes.SequenceEqual(afterBytes))
            {
                throw new InvalidDataException(
                    $"Aggregate range '{range.StableId}' lost its exact nonredundant before/after identity.");
            }
            ValidateRangeDomain(range);
            guardedByteCount += range.ByteLength;
            cursor = checked(range.DataRelativeOffset + range.ByteLength);
        }
        if (cursor < source.Length)
        {
            int length = source.Length - cursor;
            ReadOnlySpan<byte> before = source.AsSpan(cursor, length);
            ReadOnlySpan<byte> after = output.AsSpan(cursor, length);
            if (!before.SequenceEqual(after))
                throw new InvalidDataException($"The protected aggregate complement changed at row+0x{cursor:X}.");
            string segmentSha256 = Hash(before);
            protectedRanges.Add(new(cursor, length, segmentSha256));
            complementHash.AppendData(before);
        }
        if (protectedRanges.Any(item => item.ByteLength <= 0) ||
            protectedRanges.Zip(protectedRanges.Skip(1), (left, right) =>
                    left.DataRelativeOffset + (long)left.ByteLength >= right.DataRelativeOffset)
                .Any(value => value))
            throw new InvalidDataException("The aggregate protected complement is not a maximal disjoint partition.");

        byte[] applied = ApplyAllRanges(source, owned);
        if (!applied.SequenceEqual(output))
            throw new InvalidDataException("The complete aggregate transaction did not reproduce the exact output.");
        int protectedByteCount = protectedRanges.Sum(item => item.ByteLength);
        if (guardedByteCount + protectedByteCount != source.Length)
            throw new InvalidDataException("The aggregate owned/protected partition does not cover row 80 exactly.");
        return new(
            protectedRanges.ToArray(),
            guardedByteCount,
            protectedByteCount,
            Convert.ToHexStringLower(complementHash.GetHashAndReset()));
    }

    private static void ValidateRangeDomain(Id65AuthoringEditorAggregateOwnedRange range)
    {
        bool valid = range.Domain switch
        {
            Id65AuthoringEditorAggregateRangeDomain.NativeTexturePages =>
                range.StableId.StartsWith("texture.page.", StringComparison.Ordinal) &&
                range.DataRelativeOffset >= 0x800 &&
                range.DataRelativeOffset + (long)range.ByteLength <= ModelOffset,
            Id65AuthoringEditorAggregateRangeDomain.CompositeModel =>
                range.StableId == "model.final" &&
                range.OwnerId == "composite.v3-terrain-t66" &&
                range.DataRelativeOffset == ModelOffset && range.ByteLength == ModelByteLength,
            Id65AuthoringEditorAggregateRangeDomain.Spawn =>
                (range.StableId == "spawn.landing-xy" && range.DataRelativeOffset == 0x1D0000 &&
                 range.ByteLength == 8) ||
                (range.StableId == "spawn.t92-player-anchor-xy" &&
                 range.DataRelativeOffset == 0x1D211C && range.ByteLength == 8),
            Id65AuthoringEditorAggregateRangeDomain.Grass =>
                ValidateExactGrassRange(range),
            _ => false
        };
        if (!valid)
            throw new InvalidDataException($"Aggregate range '{range.StableId}' crossed its exact semantic domain.");
    }

    private static bool ValidateExactGrassRange(Id65AuthoringEditorAggregateOwnedRange range)
    {
        (int Offset, int Length) expected = range.StableId switch
        {
            "asset.root37" => (0xE4, 4),
            "asset.actor-id37" => (0x19A, 2),
            "asset.package.01f5" => (0x1CFA44, 0x174),
            "scene.object-count" => (0x1D016C, 4),
            "placement.t88" => (0x1D1FB0, 0x58),
            "placement.t107" => (0x1D2638, 0x58),
            "scene.fixup-count" => (0x1D7F28, 4),
            "scene.fixup.t107-properties" => (0x1D8130, 4),
            "asset.properties.grass" => (0x1D8138, 8),
            _ => (-1, -1)
        };
        return range.DataRelativeOffset == expected.Offset && range.ByteLength == expected.Length;
    }

    private static byte[] ApplyAllRanges(
        byte[] input,
        IReadOnlyList<Id65AuthoringEditorAggregateOwnedRange> ranges)
    {
        byte[] output = input.ToArray();
        foreach (Id65AuthoringEditorAggregateOwnedRange range in ranges)
        {
            byte[] expected = range.CopyBefore();
            byte[] replacement = range.CopyAfter();
            if (!output.AsSpan(range.DataRelativeOffset, range.ByteLength).SequenceEqual(expected))
                throw new InvalidDataException(
                    $"Aggregate range '{range.StableId}' lost its exact whole-transaction preimage.");
            replacement.CopyTo(output, range.DataRelativeOffset);
        }
        return output;
    }

    private static void ValidateInverse(
        byte[] source,
        byte[] output,
        IReadOnlyList<Id65AuthoringEditorAggregateOwnedRange> inverse)
    {
        if (inverse.Count != ExpectedOwnedRangeCount)
            throw new InvalidDataException("The aggregate inverse range count changed.");
        byte[] restored = ApplyAllRanges(output, inverse);
        if (!restored.SequenceEqual(source))
            throw new InvalidDataException("The aggregate inverse did not restore the exact locked row.");
    }

    private static Id65AuthoringEditorAggregateProtectedModelComponent[]
        BuildProtectedModelComponents(Id65CompiledAuthoringComposite compiled)
    {
        string[] expectedIds =
        [
            "component.special-surface",
            "component.cyclorama",
            "component.portal-table",
            "component.particles",
            "component.sound"
        ];
        byte[] source = compiled.CopySourceModel();
        byte[] output = compiled.CopyOutputModel();
        Id65AuthoringEditorAggregateProtectedModelComponent[] result = compiled.Relocations
            .Where(item => item.ContentsPreserved)
            .Select(item =>
            {
                if (item.SourceByteLength != item.OutputByteLength ||
                    item.SourceRelativeOffset < 0 || item.OutputRelativeOffset < 0 ||
                    item.SourceRelativeOffset + (long)item.SourceByteLength > source.Length ||
                    item.OutputRelativeOffset + (long)item.OutputByteLength > output.Length)
                    throw new InvalidDataException($"Protected model component '{item.StableId}' is out of bounds.");
                ReadOnlySpan<byte> before = source.AsSpan(item.SourceRelativeOffset, item.SourceByteLength);
                ReadOnlySpan<byte> after = output.AsSpan(item.OutputRelativeOffset, item.OutputByteLength);
                if (!before.SequenceEqual(after) || Hash(before) != item.SourceSha256 ||
                    item.SourceSha256 != item.OutputSha256)
                    throw new InvalidDataException($"Protected model component '{item.StableId}' changed.");
                return new Id65AuthoringEditorAggregateProtectedModelComponent(
                    item.StableId,
                    item.SourceRelativeOffset,
                    item.SourceByteLength,
                    item.OutputRelativeOffset,
                    item.OutputByteLength,
                    item.SourceSha256);
            })
            .ToArray();
        if (result.Length != ExpectedProtectedModelComponentCount ||
            !result.Select(item => item.StableId).SequenceEqual(expectedIds))
            throw new InvalidDataException("The protected whole-model inner component set changed.");
        return result;
    }

    private static Id65AuthoringEditorAggregateBaselineOwnership[] BuildBaselineOwnership(
        Id65AuthoringEditorDocument document,
        Id65CompiledAuthoringComposite compiled)
    {
        (string StableId, string Kind, Id65AuthoringEditorAggregateCompiledTreatment Treatment)[] expected =
        [
            (document.BaselineHandles.Spawn, "landing-record",
                Id65AuthoringEditorAggregateCompiledTreatment.AuthorExact),
            (document.BaselineHandles.Music, "music-slot",
                Id65AuthoringEditorAggregateCompiledTreatment.PreserveLockedBase),
            (document.BaselineHandles.Totals, "totals-slot",
                Id65AuthoringEditorAggregateCompiledTreatment.PreserveLockedBase),
            (document.BaselineHandles.Exit, "exit-slot",
                Id65AuthoringEditorAggregateCompiledTreatment.PreserveLockedBase),
            (document.BaselineHandles.Save, "save-slot",
                Id65AuthoringEditorAggregateCompiledTreatment.PreserveLockedBase)
        ];
        return expected.Select(item =>
        {
            Id65StableHandleRebase rebase = compiled.HandleRebases.Single(value =>
                value.StableId == item.StableId);
            if (rebase.Kind != item.Kind || rebase.Source is not Id65LogicalAddress source ||
                rebase.Output is not Id65LogicalAddress output || source != output)
                throw new InvalidDataException($"Baseline ownership '{item.StableId}' changed.");
            return new Id65AuthoringEditorAggregateBaselineOwnership(
                item.StableId,
                item.Kind,
                source,
                output,
                item.Treatment,
                Id65AuthoringEditorAggregateEditPolicy.Blocked);
        }).ToArray();
    }

    private static Id65AuthoringEditorAggregateAtomicGroup[] BuildAtomicGroups(
        IReadOnlyList<Id65AuthoringEditorAggregateOwnedRange> owned)
    {
        Id65AuthoringEditorAggregateAtomicGroup Create(
            string groupId,
            Func<Id65AuthoringEditorAggregateOwnedRange, bool> predicate)
        {
            string[] ids = owned.Where(predicate).Select(item => item.StableId).ToArray();
            return new(
                groupId,
                ids.Length,
                Hash(Encoding.UTF8.GetBytes(string.Join('\n', ids))),
                AllRangesRequired: true,
                IndependentlyApplicable: false);
        }
        Id65AuthoringEditorAggregateAtomicGroup[] groups =
        [
            Create("v3-terrain-private-t66",
                item => item.Domain is Id65AuthoringEditorAggregateRangeDomain.NativeTexturePages or
                    Id65AuthoringEditorAggregateRangeDomain.CompositeModel),
            Create("landing-t92",
                item => item.Domain == Id65AuthoringEditorAggregateRangeDomain.Spawn),
            Create("artisans-grass-t88-t107",
                item => item.Domain == Id65AuthoringEditorAggregateRangeDomain.Grass)
        ];
        if (!groups.Select(item => item.OwnedRangeCount).SequenceEqual([7_950, 2, 9]) ||
            groups.Any(item => !item.AllRangesRequired || item.IndependentlyApplicable))
            throw new InvalidDataException("The aggregate semantic atomic-group partition changed.");
        return groups;
    }

    private static Id65AuthoringEditorAggregateDestinationBinding BuildDestinationBinding(
        UnusedLevel65BlankLevelLabManifest manifest,
        Id65AuthoringEditorAggregateBoundSource boundSource)
    {
        Id65AuthoringEditorAggregateDestinationBinding result = new(
            boundSource.SnapshotProfileId,
            boundSource.SourceImageSha256,
            manifest.LockedBaseProfileId,
            manifest.LockedBaseImageSha256,
            "WAD.WAD",
            TargetOverlayWadEntry,
            TargetOverlayWadOffset,
            TargetOverlayByteLength,
            manifest.DataWadEntry,
            TargetDataWadOffset,
            ExpectedRow80ByteLength,
            boundSource.TargetDataDirectoryRowSha256,
            manifest.ObjectTableWadOffset,
            manifest.ObjectTableRelativeOffset,
            RetailTownSquareOverlayWadEntry,
            RetailTownSquareOverlayWadOffset,
            RetailTownSquareDataWadEntry,
            RetailTownSquareDataWadOffset,
            boundSource.RejectedRetailDataDirectoryRowSha256,
            TextureDonorWadEntry,
            GrassDonorWadEntry,
            PhysicalSnapshotVerified: true,
            RetailTownSquareDestinationRejected: true,
            DonorLocationsReadOnlyProvenance: true,
            PhysicalDestinationRevalidationRequired: true);
        if (result.SourceSnapshotProfileId != Id65RewardCensusContract.ProfileId ||
            result.SourceImageSha256 != manifest.LockedBaseImageSha256 ||
            result.TargetDataWadEntry != 80 || result.TargetDataWadOffset != 0x6936800 ||
            result.TargetDataByteLength != ExpectedRow80ByteLength ||
            result.TargetDataDirectoryRowSha256 !=
                "3c6ae9564c3f6fc794bbae9670ee0dff7bd9a7bdc0f96c3c206c752551d8c09a" ||
            result.TargetObjectTableWadOffset - result.TargetObjectTableRelativeOffset !=
                result.TargetDataWadOffset ||
            result.RejectedRetailDataWadEntry != 16 ||
            result.RejectedRetailDataWadOffset != 0x119E000 ||
            result.RejectedRetailDataDirectoryRowSha256 !=
                "ba8329f16f9caed1bed39c6b794968ff95160adbfd2480c5d4bbb6fc7545f77b" ||
            result.TargetDataWadOffset == result.RejectedRetailDataWadOffset ||
            !result.PhysicalSnapshotVerified || !result.RetailTownSquareDestinationRejected ||
            !result.DonorLocationsReadOnlyProvenance ||
            !result.PhysicalDestinationRevalidationRequired)
        {
            throw new InvalidDataException("The aggregate physical ID65 destination binding changed.");
        }
        return result;
    }

    private static void ValidatePlan(Id65AuthoringEditorAggregatePlan plan)
    {
        if (plan.ProfileId != ProfileId || plan.LevelId != 65 || plan.DataWadEntry != 80 ||
            plan.DocumentCanonicalSha256 != ExpectedFullDocumentCanonicalSha256 ||
            plan.CompositeManifestSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedCombinedManifestSha256 ||
            plan.SourceRow80Sha256 != Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 ||
            plan.OutputRow80Sha256 !=
                Id65AuthoringCompositeCompiler.ExpectedCombinedOutputRow80Sha256 ||
            plan.SourceModelSha256 != Id65AuthoringCompositeCompiler.ExpectedLockedModelSha256 ||
            plan.OutputModelSha256 != Id65AuthoringCompositeCompiler.ExpectedCombinedModelSha256 ||
            plan.TargetOverlaySha256 != Id65AuthoringCompositeCompiler.ExpectedTargetOverlaySha256 ||
            plan.GlobalExecutableSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedGlobalExecutableSha256 ||
            plan.TextureWitnessSha256 != Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256 ||
            plan.GrassBundleSha256 !=
                Id65AuthoringMobyDependencyBundleCompiler.ExpectedArtisansGrassBundleSha256 ||
            plan.OwnedRangeCount != ExpectedOwnedRangeCount ||
            plan.InverseRangeCount != ExpectedOwnedRangeCount ||
            plan.ProtectedRangeCount != ExpectedProtectedRangeCount ||
            plan.OwnedRanges.Count(item =>
                item.Domain == Id65AuthoringEditorAggregateRangeDomain.NativeTexturePages) !=
                ExpectedTexturePageRangeCount ||
            plan.ProtectedModelComponents.Count != ExpectedProtectedModelComponentCount ||
            plan.HandleRebases.Count != ExpectedHandleRebaseCount ||
            plan.MobyAssetRelocations.Count != ExpectedMobyAssetRelocationCount ||
            !plan.MobyPlacements.Select(item => item.TargetTrueIndex).SequenceEqual([88, 107]) ||
            plan.BaselineOwnership.Count != 5 || plan.AtomicGroups.Count != 3 ||
            plan.GuardedByteCount != ExpectedGuardedByteCount ||
            plan.ProtectedByteCount != ExpectedProtectedByteCount ||
            plan.GuardedByteCount + plan.ProtectedByteCount != ExpectedRow80ByteLength ||
            plan.ChangedByteCount != Id65AuthoringCompositeCompiler.ExpectedCombinedChangedByteCount ||
            plan.DiffRangeCount != Id65AuthoringCompositeCompiler.ExpectedCombinedDiffRangeCount ||
            plan.DiffManifestSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedCombinedDiffManifestSha256 ||
            plan.OwnedRangeMapSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedCombinedOwnedRangeMapSha256 ||
            plan.RelocationMapSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedCombinedRelocationMapSha256 ||
            plan.RebaseMapSha256 != Id65AuthoringCompositeCompiler.ExpectedCombinedRebaseMapSha256 ||
            plan.MobyAssetRelocationMapSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedCombinedMobyAssetRelocationMapSha256 ||
            plan.TransactionSha256 != Id65AuthoringCompositeCompiler.ExpectedCombinedTransactionSha256 ||
            plan.CompositeDeterministicPlanSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedCombinedDeterministicPlanSha256 ||
            plan.InverseRangeMapSha256 != ExpectedInverseRangeMapSha256 ||
            plan.ProtectedRangeMapSha256 != ExpectedProtectedRangeMapSha256 ||
            plan.ProtectedComplementSha256 != ExpectedProtectedComplementSha256 ||
            plan.ProtectedModelComponentMapSha256 !=
                ExpectedProtectedModelComponentMapSha256 ||
            plan.SemanticReadbackSha256 != ExpectedSemanticReadbackSha256 ||
            plan.BaselineOwnershipSha256 != ExpectedBaselineOwnershipSha256 ||
            plan.AtomicGroupMapSha256 != ExpectedAtomicGroupMapSha256 ||
            plan.DeterministicAggregatePlanSha256 !=
                ExpectedDeterministicAggregatePlanSha256 ||
            !plan.FullExactDocumentRequired || !plan.ExactPhysicalDestinationBindingPinned ||
            !plan.PhysicalDestinationReadbackVerified || plan.CurrentOnDiskFreshnessVerified ||
            plan.StoreEnvelopeIdentityIndependentlyVerified || !plan.StoreProjectionHashesReverified ||
            !plan.HighPolySemanticBindingVerified || !plan.LowPolySemanticBindingVerified ||
            !plan.CollisionMembershipVerified || !plan.OcclusionMembershipVerified ||
            !plan.PrivateT66BindingVerified || !plan.GrassT88StaticBindingVerified ||
            !plan.GrassT107StaticBindingVerified || !plan.SpawnAndT92ExactBindingVerified ||
            !plan.ProtectedComplementVerified || !plan.ProtectedModelComponentsVerified ||
            !plan.ExactInverseVerified || !plan.OneAtomicTransactionRequired ||
            !plan.ContainsOwnedAfterimageBytes || plan.ContainsCompleteCompositeAfterimage ||
            plan.ContainsDiscImageBytes || plan.ContainsCueBytes ||
            plan.PartialRangeApplicationAuthorized || plan.CrossSliceCompositionSupported ||
            plan.AfterimageStackingAuthorized || plan.GenericExporterCompatible ||
            plan.RetailWriterCompatible || plan.RetailLevelMutationAuthorized ||
            plan.RuntimeMobyAcceptanceProven || plan.RuntimeProven || plan.PersistenceEnabled ||
            plan.WriterAuthorized ||
            plan.WritesFileSystem || plan.WritesDiscImage || plan.WritesBin || plan.WritesCue ||
            plan.AppIntegrated || plan.UiIntegrated || plan.CreateBinEnabled ||
            plan.NormalCreateBinEnabled || plan.RuntimeCandidateAuthorized || plan.SaveAuthorized ||
            plan.SavePersistenceProven || plan.TreasureTotal200Proven || plan.MusicLongPlayProven ||
            plan.ExitDestination65Proven || plan.ReleaseIntegrated || plan.ReleaseAuthorized ||
            plan.PromotionAuthorized || plan.Publishable ||
            !plan.RequiresTrustedExclusiveWorkspaceDirectory || plan.HostileFilesystemRaceSafe ||
            plan.CommonWorkspaceLeaseOwned)
        {
            throw new InvalidDataException("The aggregate plan identity or fail-closed capability boundary changed.");
        }
    }

    private static string HashOwnedRanges(
        IEnumerable<Id65AuthoringEditorAggregateOwnedRange> ranges) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', ranges.Select(item =>
            $"{item.StableId}|{item.OwnerId}|{item.DataRelativeOffset:X8}|{item.ByteLength:X8}|" +
            $"{item.BeforeSha256}|{item.AfterSha256}"))));

    private static string HashProtectedRanges(
        IEnumerable<Id65AuthoringEditorAggregateProtectedRange> ranges) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', ranges.Select(item =>
            $"{item.DataRelativeOffset:X8}|{item.ByteLength:X8}|{item.SegmentSha256}"))));

    private static string HashProtectedModelComponents(
        IEnumerable<Id65AuthoringEditorAggregateProtectedModelComponent> components) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', components.Select(item =>
            $"{item.StableId}|{item.SourceRelativeOffset:X8}|{item.SourceByteLength:X8}|" +
            $"{item.OutputRelativeOffset:X8}|{item.OutputByteLength:X8}|{item.ContentSha256}"))));

    private static string HashBaselineOwnership(
        IEnumerable<Id65AuthoringEditorAggregateBaselineOwnership> values) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', values.Select(item =>
            $"{item.StableId}|{item.Kind}|{Address(item.Source)}|{Address(item.Output)}|" +
            $"{item.CompiledTreatment}|{item.EditorEditPolicy}"))));

    private static string HashAtomicGroups(
        IEnumerable<Id65AuthoringEditorAggregateAtomicGroup> groups) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', groups.Select(item =>
            $"{item.GroupId}|{item.OwnedRangeCount}|{item.StableIdSetSha256}|" +
            $"{item.AllRangesRequired}|{item.IndependentlyApplicable}"))));

    private static string Address(Id65LogicalAddress value) =>
        $"{value.Space}:{value.Primary}:{value.Secondary}";

    internal static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static bool IsCanonicalSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private sealed record TransactionPartition(
        IReadOnlyList<Id65AuthoringEditorAggregateProtectedRange> ProtectedRanges,
        int GuardedByteCount,
        int ProtectedByteCount,
        string ProtectedComplementSha256);
}
