using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.Core.Persistence;

internal sealed record Id65AuthoringRemotePadTerrainSetAggregatePlanRequest(
    Id65AuthoringEditorDocumentStoreResult StoredDocument,
    string ExpectedEnvelopeCanonicalSha256,
    UnusedLevel65BlankLevelLabManifest LabManifest,
    Id65AuthoringEditorNativeInputProvider.InputSet NativeInputs,
    UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan V3Template);

internal enum Id65AuthoringRemotePadTerrainSetAggregateRangeDomain
{
    NativeTexturePages,
    TerrainSetModel,
    Spawn,
    Grass,
    RedGem
}

/// <summary>
/// One defensively copied leaf of the schema-5 aggregate transaction. These
/// leaves are descriptive parts of one indivisible in-memory plan and do not
/// authorize partial application or materialization.
/// </summary>
internal sealed class Id65AuthoringRemotePadTerrainSetAggregateOwnedRange
{
    private readonly byte[] _before;
    private readonly byte[] _after;

    internal Id65AuthoringRemotePadTerrainSetAggregateOwnedRange(
        string stableId,
        string ownerId,
        Id65AuthoringRemotePadTerrainSetAggregateRangeDomain domain,
        int dataRelativeOffset,
        byte[] before,
        byte[] after)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        StableId = stableId;
        OwnerId = ownerId;
        Domain = domain;
        DataRelativeOffset = dataRelativeOffset;
        _before = before.ToArray();
        _after = after.ToArray();
        BeforeSha256 = Id65AuthoringRemotePadTerrainSetAggregatePlanProjector.Hash(
            _before);
        AfterSha256 = Id65AuthoringRemotePadTerrainSetAggregatePlanProjector.Hash(
            _after);
    }

    public string StableId { get; }
    public string OwnerId { get; }
    public Id65AuthoringRemotePadTerrainSetAggregateRangeDomain Domain { get; }
    public int TargetWadEntry => 80;
    public int DataRelativeOffset { get; }
    public long WadOffset => 0x6936800L + DataRelativeOffset;
    public int ByteLength => _before.Length;
    public string BeforeSha256 { get; }
    public string AfterSha256 { get; }

    internal byte[] CopyBefore() => _before.ToArray();
    internal byte[] CopyAfter() => _after.ToArray();
    internal Id65AuthoringRemotePadTerrainSetAggregateOwnedRange DeepCopy() =>
        new(StableId, OwnerId, Domain, DataRelativeOffset, _before, _after);
    internal Id65AuthoringRemotePadTerrainSetAggregateOwnedRange Invert() =>
        new(StableId, OwnerId, Domain, DataRelativeOffset, _after, _before);
}

/// <summary>
/// One immutable schema-5 WAD80 transaction derived from a supplied exact store
/// result, its expected envelope identity, and provider-owned native inputs. It
/// contains only owned range afterimages and hash/readback evidence; no
/// filesystem or release authority.
/// </summary>
internal sealed class Id65AuthoringRemotePadTerrainSetAggregatePlan
{
    private readonly ReadOnlyCollection<
        Id65AuthoringRemotePadTerrainSetAggregateOwnedRange> _ownedRanges;
    private readonly ReadOnlyCollection<
        Id65AuthoringRemotePadTerrainSetAggregateOwnedRange> _inverseRanges;
    private readonly ReadOnlyCollection<Id65AuthoringEditorAggregateProtectedRange>
        _protectedRanges;
    private readonly ReadOnlyCollection<Id65AuthoringEditorAggregateAtomicGroup>
        _atomicGroups;
    private readonly ReadOnlyCollection<string> _activeTerrainUnitIds;

    internal Id65AuthoringRemotePadTerrainSetAggregatePlan(
        Id65AuthoringEditorAggregateDestinationBinding destination,
        Id65AuthoringEditorDocument document,
        string envelopeCanonicalSha256,
        string staticAuthoringProfileId,
        string staticAuthoringCatalogSha256,
        string staticAuthoringSceneGraphSha256,
        string staticAuthoringProjectionSha256,
        string staticAuthoringTerrainManifestSha256,
        string nativeInputSetSha256,
        string v3DeterministicPlanSha256,
        string sourceRow80Sha256,
        string outputRow80Sha256,
        string outputModelSha256,
        string outputTexturePagesSha256,
        string landingSha256,
        string playerAnchorSha256,
        string targetOverlaySha256,
        string globalExecutableSha256,
        string schema4BaselineDocumentSha256,
        string schema4BaselineOutputRow80Sha256,
        string schema4BaselineOutputModelSha256,
        string schema4BaselineDeterministicPlanSha256,
        string compilerOwnedRangeMapSha256,
        string compilerTransactionSha256,
        string compilerDeterministicPlanSha256,
        Id65AuthoringRemotePadMobyChoicePhysicalPlacementReadback placement,
        Id65AuthoringRemotePadTerrainSetPhysicalReadback terrain,
        IEnumerable<Id65AuthoringRemotePadTerrainSetAggregateOwnedRange> ownedRanges,
        IEnumerable<Id65AuthoringRemotePadTerrainSetAggregateOwnedRange> inverseRanges,
        IEnumerable<Id65AuthoringEditorAggregateProtectedRange> protectedRanges,
        IEnumerable<Id65AuthoringEditorAggregateAtomicGroup> atomicGroups,
        int guardedByteCount,
        int protectedByteCount,
        int changedByteCount,
        string ownedRangeMapSha256,
        string inverseRangeMapSha256,
        string protectedRangeMapSha256,
        string protectedComplementSha256,
        string atomicGroupMapSha256,
        string deterministicAggregatePlanSha256)
    {
        Destination = destination;
        Document = Id65AuthoringEditorDocument.Parse(document.SerializeUtf8());
        DocumentCanonicalSha256 = Document.CanonicalSha256;
        TerrainManifestSha256 = Document.TerrainSetManifestSha256!;
        _activeTerrainUnitIds = Array.AsReadOnly(
            Document.ActiveTerrainUnitIds.ToArray());
        MobyKind = Document.RemotePadMobyKind!.Value;
        EnvelopeCanonicalSha256 = envelopeCanonicalSha256;
        StaticAuthoringProfileId = staticAuthoringProfileId;
        StaticAuthoringCatalogSha256 = staticAuthoringCatalogSha256;
        StaticAuthoringSceneGraphSha256 = staticAuthoringSceneGraphSha256;
        StaticAuthoringProjectionSha256 = staticAuthoringProjectionSha256;
        StaticAuthoringTerrainManifestSha256 =
            staticAuthoringTerrainManifestSha256;
        NativeInputSetSha256 = nativeInputSetSha256;
        V3DeterministicPlanSha256 = v3DeterministicPlanSha256;
        SourceRow80Sha256 = sourceRow80Sha256;
        OutputRow80Sha256 = outputRow80Sha256;
        OutputModelSha256 = outputModelSha256;
        OutputTexturePagesSha256 = outputTexturePagesSha256;
        LandingSha256 = landingSha256;
        PlayerAnchorSha256 = playerAnchorSha256;
        TargetOverlaySha256 = targetOverlaySha256;
        GlobalExecutableSha256 = globalExecutableSha256;
        Schema4BaselineDocumentSha256 = schema4BaselineDocumentSha256;
        Schema4BaselineOutputRow80Sha256 = schema4BaselineOutputRow80Sha256;
        Schema4BaselineOutputModelSha256 = schema4BaselineOutputModelSha256;
        Schema4BaselineDeterministicPlanSha256 =
            schema4BaselineDeterministicPlanSha256;
        CompilerOwnedRangeMapSha256 = compilerOwnedRangeMapSha256;
        CompilerTransactionSha256 = compilerTransactionSha256;
        CompilerDeterministicPlanSha256 = compilerDeterministicPlanSha256;
        Placement = placement;
        Terrain = terrain with
        {
            ActiveTerrainUnitIds = Array.AsReadOnly(
                terrain.ActiveTerrainUnitIds.ToArray()),
            Units = Array.AsReadOnly(terrain.Units.ToArray())
        };
        _ownedRanges = Array.AsReadOnly(
            ownedRanges.Select(item => item.DeepCopy()).ToArray());
        _inverseRanges = Array.AsReadOnly(
            inverseRanges.Select(item => item.DeepCopy()).ToArray());
        _protectedRanges = Array.AsReadOnly(protectedRanges.ToArray());
        _atomicGroups = Array.AsReadOnly(atomicGroups.ToArray());
        GuardedByteCount = guardedByteCount;
        ProtectedByteCount = protectedByteCount;
        ChangedByteCount = changedByteCount;
        OwnedRangeMapSha256 = ownedRangeMapSha256;
        InverseRangeMapSha256 = inverseRangeMapSha256;
        ProtectedRangeMapSha256 = protectedRangeMapSha256;
        ProtectedComplementSha256 = protectedComplementSha256;
        AtomicGroupMapSha256 = atomicGroupMapSha256;
        DeterministicAggregatePlanSha256 = deterministicAggregatePlanSha256;
    }

    public string ProfileId =>
        Id65AuthoringRemotePadTerrainSetAggregatePlanProjector.ProfileId;
    public int SchemaVersion =>
        Id65AuthoringRemotePadTerrainSetAggregatePlanProjector.SchemaVersion;
    public int LevelId => 65;
    public int DataWadEntry => 80;
    public Id65AuthoringEditorAggregateDestinationBinding Destination { get; }
    public Id65AuthoringEditorDocument Document { get; }
    public string DocumentCanonicalSha256 { get; }
    public string TerrainManifestSha256 { get; }
    public IReadOnlyList<string> ActiveTerrainUnitIds => _activeTerrainUnitIds;
    public int TerrainUnitCount => _activeTerrainUnitIds.Count;
    public Id65RemotePadMobyKind MobyKind { get; }
    public string EnvelopeCanonicalSha256 { get; }
    public string StaticAuthoringProfileId { get; }
    public string StaticAuthoringCatalogSha256 { get; }
    public string StaticAuthoringSceneGraphSha256 { get; }
    public string StaticAuthoringProjectionSha256 { get; }
    public string StaticAuthoringTerrainManifestSha256 { get; }
    public string NativeInputSetSha256 { get; }
    public string V3DeterministicPlanSha256 { get; }
    public string CatalogCanonicalSha256 =>
        Id65RemotePadMobyChoiceCatalog.CanonicalSha256;
    public string? SelectedExactRowSha256 =>
        Id65RemotePadMobyChoiceCatalog.GetSelectedExactRowSha256(
            MobyKind,
            Placement.PositionId);
    public string SourceRow80Sha256 { get; }
    public string OutputRow80Sha256 { get; }
    public string OutputModelSha256 { get; }
    public string OutputTexturePagesSha256 { get; }
    public string LandingSha256 { get; }
    public string PlayerAnchorSha256 { get; }
    public string TargetOverlaySha256 { get; }
    public string GlobalExecutableSha256 { get; }
    public string Schema4BaselineDocumentSha256 { get; }
    public string Schema4BaselineOutputRow80Sha256 { get; }
    public string Schema4BaselineOutputModelSha256 { get; }
    public string Schema4BaselineDeterministicPlanSha256 { get; }
    public string CompilerOwnedRangeMapSha256 { get; }
    public string CompilerTransactionSha256 { get; }
    public string CompilerDeterministicPlanSha256 { get; }
    public Id65AuthoringRemotePadMobyChoicePhysicalPlacementReadback Placement { get; }
    public Id65AuthoringRemotePadTerrainSetPhysicalReadback Terrain { get; }
    public IReadOnlyList<Id65AuthoringRemotePadTerrainSetAggregateOwnedRange>
        OwnedRanges => _ownedRanges;
    public IReadOnlyList<Id65AuthoringRemotePadTerrainSetAggregateOwnedRange>
        InverseRanges => _inverseRanges;
    public IReadOnlyList<Id65AuthoringEditorAggregateProtectedRange>
        ProtectedRanges => _protectedRanges;
    public IReadOnlyList<Id65AuthoringEditorAggregateAtomicGroup>
        AtomicGroups => _atomicGroups;
    public int OwnedRangeCount => _ownedRanges.Count;
    public int InverseRangeCount => _inverseRanges.Count;
    public int ProtectedRangeCount => _protectedRanges.Count;
    public int GuardedByteCount { get; }
    public int ProtectedByteCount { get; }
    public int ChangedByteCount { get; }
    public string OwnedRangeMapSha256 { get; }
    public string InverseRangeMapSha256 { get; }
    public string ProtectedRangeMapSha256 { get; }
    public string ProtectedComplementSha256 { get; }
    public string AtomicGroupMapSha256 { get; }
    public string DeterministicAggregatePlanSha256 { get; }

    public bool PureInMemory => true;
    public bool FullExactSchema5DocumentRequired => true;
    public bool SuppliedStoreResultMatchesExpectedEnvelopeIdentity => true;
    public bool StaticAuthoringProjectionRecomputedFromCanonicalDocument => true;
    public bool StoreEnvelopeIdentityIndependentlyVerified => true;
    public bool StaticAuthoringProjectionRecomputedFromStoreBytes => false;
    public bool NativeInputGenerationReverified => true;
    public bool LockedSourceOverlayExecutableIdentityPreserved => true;
    public bool ExactPhysicalDestinationBindingPinned => true;
    public bool PhysicalDestinationReadbackVerified => true;
    public bool FourTopologyTerrainSetBindingVerified => true;
    public bool FinalT66TerrainSemanticReadbackVerified => true;
    public bool RepairedT13995SupportBindingVerified => true;
    public bool RemotePadMobyChoiceCatalogBindingVerified => true;
    public bool GrassT88ExcludedVerified => true;
    public bool MutuallyExclusiveMobyChoiceVerified => true;
    public bool ResidentT21XyzOnlyOwnershipVerified => true;
    public bool ProtectedComplementVerified => true;
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
    public bool PersistenceEnabled => false;
    public bool WriterAuthorized => false;
    public bool AggregateMaterializerEnabled => false;
    public bool CandidateMaterializerEnabled => false;
    public bool PhysicalMaterializationEnabled => false;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool WritesBin => false;
    public bool WritesCue => false;
    public bool AppIntegrated => false;
    public bool UiIntegrated => false;
    public bool CreateBinEnabled => false;
    public bool NormalCreateBinEnabled => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool RuntimeProven => false;
    public bool SaveAuthorized => false;
    public bool SavePersistenceProven => false;
    public bool TreasureTotal200Proven => false;
    public bool MusicLongPlayProven => false;
    public bool ExitDestination65Proven => false;
    public bool ReleaseIntegrated => false;
    public bool ReleaseAuthorized => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;
    public bool CurrentOnDiskFreshnessVerified => false;
    public bool RequiresTrustedExclusiveWorkspaceDirectory => true;
    public bool HostileFilesystemRaceSafe => false;
    public bool CommonWorkspaceLeaseOwned => false;
}

/// <summary>
/// Rebinds one supplied schema-5 document store result and its expected envelope
/// identity to a validated provider-owned input generation and the pure schema-5
/// physical compiler. It does not establish current on-disk freshness, returns
/// an in-memory transaction only, and exposes no writer route.
/// </summary>
internal static class Id65AuthoringRemotePadTerrainSetAggregatePlanProjector
{
    public const string ProfileId =
        "id65-authoring-remote-pad-terrain-set-aggregate-plan-schema5-in-memory-v1";
    public const int SchemaVersion = 1;
    public const int ExpectedRow80ByteLength = 0x2E2000;
    public const int ExpectedStateCount = 108;
    public const int ExpectedPhysicalOutputHashCount = 76;

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
    private const string TerrainModelOwnerId =
        "model.remote-pad.schema5-terrain-set";

    public static Id65AuthoringRemotePadTerrainSetAggregatePlan Project(
        Id65AuthoringRemotePadTerrainSetAggregatePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.StoredDocument);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            request.ExpectedEnvelopeCanonicalSha256);
        ArgumentNullException.ThrowIfNull(request.LabManifest);
        ArgumentNullException.ThrowIfNull(request.NativeInputs);
        ArgumentNullException.ThrowIfNull(request.V3Template);
        Id65AuthoringEditorNativeInputProvider.ValidateForConsumption(
            request.NativeInputs);

        ValidateExactLabManifest(request.LabManifest);
        Id65AuthoringEditorDocument document = ValidateStoredDocument(
            request.StoredDocument,
            request.ExpectedEnvelopeCanonicalSha256,
            request.LabManifest);
        ValidateInputIdentity(request, document);

        Id65CompiledAuthoringRemotePadTerrainSetPhysical compiled =
            Id65AuthoringRemotePadTerrainSetPhysicalCompiler.Compile(new(
                document,
                request.NativeInputs.LockedSource,
                request.V3Template,
                request.NativeInputs.PrivateTextureWitness,
                request.NativeInputs.GrassBundle));
        ValidateCompiled(request, document, compiled);

        byte[] source = compiled.CopySourceRow80();
        byte[] output = compiled.CopyOutputRow80();
        Id65AuthoringRemotePadTerrainSetAggregateOwnedRange[] owned =
            compiled.OwnedRanges.Select(ToAggregateRange).ToArray();
        TransactionPartition partition = ValidateAndBuildPartition(
            source,
            output,
            owned,
            compiled.MobyKind);
        if (partition.ProtectedComplementSha256 !=
            compiled.ProtectedComplementSha256)
        {
            throw new InvalidDataException(
                "The schema-5 aggregate protected complement changed from the physical compiler.");
        }
        Id65AuthoringRemotePadTerrainSetAggregateOwnedRange[] inverse = owned
            .Reverse()
            .Select(item => item.Invert())
            .ToArray();
        ValidateInverse(source, output, inverse, owned.Length);
        Id65AuthoringEditorAggregateAtomicGroup[] atomicGroups =
            BuildAtomicGroups(owned, compiled.MobyKind);
        Id65AuthoringEditorAggregateDestinationBinding destination =
            BuildDestinationBinding(
                request.LabManifest,
                request.NativeInputs.BoundSource);

        string ownedHash = HashOwnedRanges(owned);
        string inverseHash = HashOwnedRanges(inverse);
        string protectedHash = HashProtectedRanges(partition.ProtectedRanges);
        string atomicHash = HashAtomicGroups(atomicGroups);
        string selectedRowIdentity =
            Id65RemotePadMobyChoiceCatalog.GetSelectedExactRowSha256(
                compiled.MobyKind,
                compiled.Placement.PositionId) ?? "none";
        string aggregateHash = Hash(Encoding.UTF8.GetBytes(string.Join('\n',
        [
            ProfileId,
            SchemaVersion.ToString(CultureInfo.InvariantCulture),
            request.StoredDocument.EnvelopeCanonicalSha256,
            request.StoredDocument.StaticAuthoringProjectionSha256,
            request.NativeInputs.CanonicalInputSetSha256,
            document.CanonicalSha256,
            Id65RemotePadMobyChoiceCatalog.CanonicalSha256,
            Id65RemotePadMobyChoiceCatalog.GetKindId(compiled.MobyKind),
            compiled.Placement.PositionId,
            selectedRowIdentity,
            compiled.SourceRow80Sha256,
            compiled.OutputRow80Sha256,
            compiled.OutputModelSha256,
            compiled.Terrain.TerrainManifestSha256,
            Hash(Encoding.UTF8.GetBytes(string.Join(
                '\n',
                compiled.ActiveTerrainUnitIds))),
            compiled.OutputTexturePagesSha256,
            compiled.LandingSha256,
            compiled.PlayerAnchorSha256,
            compiled.TargetOverlaySha256,
            compiled.GlobalExecutableSha256,
            compiled.Schema4BaselineDocumentSha256,
            compiled.Schema4BaselineOutputRow80Sha256,
            compiled.Schema4BaselineOutputModelSha256,
            compiled.Schema4BaselineDeterministicPlanSha256,
            request.V3Template.DeterministicPlanSha256,
            compiled.ChangedByteCount.ToString(CultureInfo.InvariantCulture),
            compiled.OwnedRangeMapSha256,
            compiled.TransactionSha256,
            compiled.ProtectedComplementSha256,
            compiled.DeterministicPlanSha256,
            ownedHash,
            inverseHash,
            protectedHash,
            partition.ProtectedComplementSha256,
            atomicHash,
            destination.TargetDataWadEntry.ToString(CultureInfo.InvariantCulture),
            destination.TargetDataWadOffset.ToString("X", CultureInfo.InvariantCulture),
            destination.TargetDataDirectoryRowSha256,
            destination.SourceImageSha256,
            destination.RejectedRetailDataWadEntry.ToString(
                CultureInfo.InvariantCulture),
            destination.RejectedRetailDataWadOffset.ToString(
                "X",
                CultureInfo.InvariantCulture),
            destination.RejectedRetailDataDirectoryRowSha256
        ])));

        Id65AuthoringRemotePadTerrainSetAggregatePlan plan = new(
            destination,
            document,
            request.StoredDocument.EnvelopeCanonicalSha256,
            request.StoredDocument.StaticAuthoringProfileId,
            request.StoredDocument.StaticAuthoringCatalogSha256,
            request.StoredDocument.StaticAuthoringSceneGraphSha256,
            request.StoredDocument.StaticAuthoringProjectionSha256,
            request.StoredDocument.StaticAuthoringTerrainManifestSha256,
            request.NativeInputs.CanonicalInputSetSha256,
            request.V3Template.DeterministicPlanSha256,
            compiled.SourceRow80Sha256,
            compiled.OutputRow80Sha256,
            compiled.OutputModelSha256,
            compiled.OutputTexturePagesSha256,
            compiled.LandingSha256,
            compiled.PlayerAnchorSha256,
            compiled.TargetOverlaySha256,
            compiled.GlobalExecutableSha256,
            compiled.Schema4BaselineDocumentSha256,
            compiled.Schema4BaselineOutputRow80Sha256,
            compiled.Schema4BaselineOutputModelSha256,
            compiled.Schema4BaselineDeterministicPlanSha256,
            compiled.OwnedRangeMapSha256,
            compiled.TransactionSha256,
            compiled.DeterministicPlanSha256,
            compiled.Placement,
            compiled.Terrain,
            owned,
            inverse,
            partition.ProtectedRanges,
            atomicGroups,
            partition.GuardedByteCount,
            partition.ProtectedByteCount,
            compiled.ChangedByteCount,
            ownedHash,
            inverseHash,
            protectedHash,
            partition.ProtectedComplementSha256,
            atomicHash,
            aggregateHash);
        ValidatePlan(plan);
        return plan;
    }

    internal static byte[] ApplyExact(
        Id65AuthoringRemotePadTerrainSetAggregatePlan plan,
        byte[] exactRow80,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(exactRow80);
        ValidatePlan(plan);
        string expectedInputHash = reverse
            ? plan.OutputRow80Sha256
            : plan.SourceRow80Sha256;
        string expectedOutputHash = reverse
            ? plan.SourceRow80Sha256
            : plan.OutputRow80Sha256;
        if (exactRow80.Length != ExpectedRow80ByteLength ||
            Hash(exactRow80) != expectedInputHash)
        {
            throw new InvalidDataException(
                "The schema-5 aggregate transaction whole-row preimage changed.");
        }
        IReadOnlyList<Id65AuthoringRemotePadTerrainSetAggregateOwnedRange> ranges =
            reverse ? plan.InverseRanges : plan.OwnedRanges;
        byte[] output = ApplyAllRanges(exactRow80, ranges);
        if (Hash(output) != expectedOutputHash)
        {
            throw new InvalidDataException(
                "The schema-5 aggregate transaction whole-row result changed.");
        }
        return output;
    }

    internal static void ValidatePartitionForSmoke(
        byte[] source,
        byte[] output,
        IEnumerable<Id65AuthoringRemotePadTerrainSetAggregateOwnedRange> ranges,
        Id65RemotePadMobyKind kind)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(ranges);
        Id65AuthoringRemotePadTerrainSetAggregateOwnedRange[] owned = ranges
            .Select(item => item.DeepCopy())
            .ToArray();
        TransactionPartition partition = ValidateAndBuildPartition(
            source,
            output,
            owned,
            kind);
        ValidateInverse(
            source,
            output,
            owned.Reverse().Select(item => item.Invert()).ToArray(),
            owned.Length);
        if (partition.GuardedByteCount + partition.ProtectedByteCount !=
            ExpectedRow80ByteLength)
        {
            throw new InvalidDataException(
                "The schema-5 aggregate partition does not cover row 80.");
        }
    }

    private static Id65AuthoringEditorDocument ValidateStoredDocument(
        Id65AuthoringEditorDocumentStoreResult stored,
        string expectedEnvelopeCanonicalSha256,
        UnusedLevel65BlankLevelLabManifest manifest) =>
        Id65AuthoringEditorDocumentStore
            .ValidateRemotePadMobyChoiceTerrainSetStoreResultForAggregate(
                stored,
                expectedEnvelopeCanonicalSha256,
                manifest);

    private static void ValidateInputIdentity(
        Id65AuthoringRemotePadTerrainSetAggregatePlanRequest request,
        Id65AuthoringEditorDocument document)
    {
        Id65AuthoringEditorAggregateBoundSource bound =
            request.NativeInputs.BoundSource;
        Id65AuthoringCompositeLockedSource source =
            request.NativeInputs.LockedSource;
        UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan v3 =
            request.V3Template;
        Id65AuthoringTerrainSetManifest terrainManifest =
            Id65AuthoringTerrainSetCatalog.Create(document.ActiveTerrainUnitIds);
        byte[] lockedRow80 = source.CopyRow80();
        if (bound.SnapshotProfileId != Id65RewardCensusContract.ProfileId ||
            bound.SourceImageSha256 != request.LabManifest.LockedBaseImageSha256 ||
            bound.TargetDataWadEntry != 80 ||
            bound.TargetDataWadOffset != TargetDataWadOffset ||
            bound.RejectedRetailDataWadEntry != 16 ||
            bound.RejectedRetailDataWadOffset != RetailTownSquareDataWadOffset ||
            !bound.PhysicalDirectorySnapshotVerified ||
            !bound.RetailCloneComparedAndExcluded ||
            bound.CurrentOnDiskFreshnessVerified ||
            request.NativeInputs.CanonicalInputSetSha256 !=
                Id65AuthoringEditorNativeInputProvider.ExpectedCanonicalInputSetSha256 ||
            source.Row80ByteLength != ExpectedRow80ByteLength ||
            source.TargetOverlayByteLength != TargetOverlayByteLength ||
            source.GlobalExecutableByteLength != 0x66000 ||
            source.Row80Sha256 !=
                Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 ||
            source.TargetOverlaySha256 !=
                Id65AuthoringCompositeCompiler.ExpectedTargetOverlaySha256 ||
            source.GlobalExecutableSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedGlobalExecutableSha256 ||
            request.NativeInputs.PrivateTextureWitnessSha256 !=
                Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256 ||
            request.NativeInputs.GrassBundleSha256 !=
                Id65RemotePadGrassT107Catalog.BundleCanonicalSha256 ||
            document.PrivateTextureWitnessSha256 !=
                request.NativeInputs.PrivateTextureWitnessSha256 ||
            document.MobyBundleSha256 != request.NativeInputs.GrassBundleSha256 ||
            document.SchemaVersion != Id65AuthoringEditorDocument
                .RemotePadMobyChoiceTerrainSetSchemaVersion ||
            document.ProfileId != Id65AuthoringEditorDocument
                .RemotePadMobyChoiceTerrainSetDocumentProfileId ||
            !document.UsesRemotePadTerrainSet ||
            document.TerrainSetManifestSha256 != terrainManifest.CanonicalSha256 ||
            !document.ActiveTerrainUnitIds.SequenceEqual(
                terrainManifest.ActiveUnits.Select(item => item.Id)) ||
            v3.ProfileId !=
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction
                    .ProfileId ||
            v3.SourceImageSha256 != bound.SourceImageSha256 ||
            v3.ExecutableSha256 != source.GlobalExecutableSha256 ||
            v3.TargetWadEntry != 80 ||
            v3.DataWadOffset != TargetDataWadOffset ||
            v3.DataByteLength != ExpectedRow80ByteLength ||
            v3.SourceData.Length != ExpectedRow80ByteLength ||
            v3.OutputData.Length != ExpectedRow80ByteLength ||
            v3.SourceDataSha256 !=
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction
                    .ExpectedSourceDataSha256 ||
            v3.OutputDataSha256 !=
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction
                    .ExpectedOutputDataSha256 ||
            v3.OutputModelSha256 !=
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction
                    .ExpectedOutputModelSha256 ||
            v3.DeterministicPlanSha256 !=
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction
                    .ExpectedDeterministicPlanSha256 ||
            Hash(v3.SourceData) != v3.SourceDataSha256 ||
            Hash(v3.OutputData) != v3.OutputDataSha256 ||
            !v3.SourceData.SequenceEqual(lockedRow80) ||
            !v3.ByteInverseVerified || !v3.ProtectedSubfilesPreserved ||
            v3.SourceImageMutationPossible ||
            v3.DisposableRuntimeCandidateAuthorized ||
            v3.PromotionAuthorized || v3.NormalCreateBinEnabled)
        {
            throw new InvalidDataException(
                "The schema-5 aggregate source, v3 provider plan, terrain manifest, T66 witness, or Moby bundle changed.");
        }
    }

    private static void ValidateCompiled(
        Id65AuthoringRemotePadTerrainSetAggregatePlanRequest request,
        Id65AuthoringEditorDocument document,
        Id65CompiledAuthoringRemotePadTerrainSetPhysical compiled)
    {
        Id65RemotePadMobyKind kind = document.RemotePadMobyKind!.Value;
        string positionId = document.RemotePadMobyPositionId!;
        Id65RemotePadGrassT107Position position =
            Id65RemotePadGrassT107Catalog.GetRequiredPosition(positionId);
        Id65AuthoringTerrainSetManifest manifest =
            Id65AuthoringTerrainSetCatalog.Create(document.ActiveTerrainUnitIds);
        Id65AuthoringEditorDocument schema4Baseline =
            Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceExact(
                kind,
                positionId,
                includeOptionalTerrain: false);
        if (compiled.Placement.Kind != kind ||
            compiled.Placement.PositionId != position.PositionId ||
            compiled.Placement.PositionOrdinal != position.Ordinal ||
            compiled.Placement.EditorX != position.EditorX ||
            compiled.Placement.EditorY != position.EditorY ||
            compiled.Placement.EditorZ != position.EditorZ ||
            compiled.Placement.RawX != position.RawX ||
            compiled.Placement.RawY != position.RawY ||
            compiled.Placement.RawZ != position.RawZ ||
            compiled.Placement.Present != (kind != Id65RemotePadMobyKind.None) ||
            compiled.Placement.ReusesResidentRow !=
                (kind == Id65RemotePadMobyKind.RedGemT21) ||
            compiled.Placement.SupportHandle !=
                Id65FreshLevelSceneProjector.CollisionStableId ||
            compiled.Placement.SupportCollisionTrueIndex !=
                Id65FreshLevelSceneProjector.CollisionTriangleIndex ||
            compiled.Placement.ExactRowSha256 !=
                Id65RemotePadMobyChoiceCatalog.GetSelectedExactRowSha256(
                    kind,
                    positionId))
        {
            throw new InvalidDataException(
                "The schema-5 aggregate physical placement readback changed.");
        }
        if (!compiled.Document.SerializeUtf8().SequenceEqual(document.SerializeUtf8()) ||
            compiled.ProfileId !=
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ProfileId ||
            compiled.SchemaVersion !=
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.SchemaVersion ||
            compiled.InputDocumentSchemaVersion !=
                Id65AuthoringEditorDocument
                    .RemotePadMobyChoiceTerrainSetSchemaVersion ||
            compiled.DocumentCanonicalSha256 != document.CanonicalSha256 ||
            compiled.MobyKind != kind ||
            compiled.PositionId != positionId ||
            !compiled.ActiveTerrainUnitIds.SequenceEqual(
                document.ActiveTerrainUnitIds) ||
            compiled.SourceRow80Sha256 !=
                Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 ||
            compiled.TargetOverlaySha256 !=
                Id65AuthoringCompositeCompiler.ExpectedTargetOverlaySha256 ||
            compiled.GlobalExecutableSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedGlobalExecutableSha256 ||
            compiled.Schema4BaselineDocumentSha256 !=
                schema4Baseline.CanonicalSha256 ||
            compiled.OwnedRanges.Count == 0 ||
            compiled.InverseRanges.Count != compiled.OwnedRanges.Count ||
            compiled.ChangedByteCount <= 0 ||
            !IsCanonicalSha256(compiled.OutputRow80Sha256) ||
            !IsCanonicalSha256(compiled.OutputModelSha256) ||
            !IsCanonicalSha256(compiled.Schema4BaselineOutputRow80Sha256) ||
            !IsCanonicalSha256(compiled.Schema4BaselineOutputModelSha256) ||
            !IsCanonicalSha256(
                compiled.Schema4BaselineDeterministicPlanSha256) ||
            !IsCanonicalSha256(compiled.OwnedRangeMapSha256) ||
            !IsCanonicalSha256(compiled.TransactionSha256) ||
            !IsCanonicalSha256(compiled.ProtectedComplementSha256) ||
            !IsCanonicalSha256(compiled.DeterministicPlanSha256) ||
            compiled.OutputTexturePagesSha256 !=
                Id65V2NativeTextureCompositionCompiler
                    .ExpectedOutputTexturePagesSha256 ||
            compiled.LandingSha256 != request.V3Template.Spawn.OutputLandingSha256 ||
            compiled.PlayerAnchorSha256 !=
                request.V3Template.Spawn.OutputPlayerAnchorSha256 ||
            compiled.Terrain.TerrainManifestSha256 != manifest.CanonicalSha256 ||
            !compiled.Terrain.ActiveTerrainUnitIds.SequenceEqual(
                document.ActiveTerrainUnitIds) ||
            !compiled.Terrain.Units.Select(item => item.UnitId).SequenceEqual(
                document.ActiveTerrainUnitIds) ||
            compiled.Terrain.T66TerrainModelSha256 != compiled.OutputModelSha256 ||
            compiled.Terrain.T66TextureComponentSha256 !=
                Id65V2NativeTextureCompositionCompiler
                    .ExpectedOutputTextureComponentSha256 ||
            compiled.Terrain.MandatoryBaseFaceTextureId !=
                Id65AuthoringEditorDocument.PrivateMandatoryBaseFaceTextureId ||
            compiled.Terrain.MandatoryBaseFaceModelOffset < 0 ||
            compiled.Terrain.MandatoryBaseFaceModelOffset >=
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelLength ||
            compiled.Terrain.UsedModelBytes + compiled.Terrain.ZeroTailBytes !=
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelLength ||
            !IsCanonicalSha256(compiled.Terrain.V3TerrainModelSha256) ||
            !IsCanonicalSha256(compiled.Terrain.V3TerrainEnvironmentSha256) ||
            !IsCanonicalSha256(compiled.Terrain.FinalT66EnvironmentSha256) ||
            !IsCanonicalSha256(compiled.Terrain.CollisionSha256) ||
            !IsCanonicalSha256(
                compiled.Terrain.V3TerrainProtectedComplementSha256) ||
            !IsCanonicalSha256(
                compiled.Terrain.FinalT66ProtectedComponentSetSha256) ||
            !compiled.PureInMemory ||
            !compiled.ExactSchema5DocumentRequired ||
            !compiled.BuildsEveryTargetFromLockedV3Source ||
            compiled.CallerAfterimagesAccepted ||
            !compiled.IndependentTerrainSemanticReadbackVerified ||
            !compiled.MobyPlacementReadbackVerified ||
            !compiled.NativeT66TexturePreserved ||
            !compiled.ExactSpawnPreserved ||
            !compiled.OverlayAndExecutablePreserved ||
            !compiled.ExactInverseVerified ||
            !compiled.EveryChangedByteOwned ||
            !compiled.ProtectedComplementVerified ||
            !compiled.ContainsOutputRow80Bytes ||
            compiled.AcceptsAggregatePlans ||
            compiled.AcceptsMaterializedCandidates ||
            compiled.WritesFileSystem || compiled.WritesDiscImage ||
            compiled.WritesCue || compiled.AppIntegrated || compiled.UiIntegrated ||
            compiled.AggregateMaterializerEnabled ||
            compiled.CandidateMaterializerEnabled ||
            compiled.PhysicalMaterializationEnabled || compiled.CreateBinEnabled ||
            compiled.NormalCreateBinEnabled ||
            compiled.RuntimeCandidateAuthorized || compiled.RuntimeProven ||
            compiled.ReleaseIntegrated || compiled.PromotionAuthorized ||
            compiled.Publishable)
        {
            throw new InvalidDataException(
                "The schema-5 physical compiler result or authority boundary changed.");
        }
    }

    private static Id65AuthoringRemotePadTerrainSetAggregateOwnedRange
        ToAggregateRange(Id65AuthoringCompositeOwnedRange range)
    {
        long end = range.DataRelativeOffset + (long)range.ByteLength;
        int textureStart =
            Id65V2NativeTextureCompositionCompiler.TexturePagesDataRelativeOffset;
        int textureEnd = checked(textureStart +
            Id65V2NativeTextureCompositionCompiler.TexturePagesByteLength);
        Id65AuthoringRemotePadTerrainSetAggregateRangeDomain domain;
        if (range.DataRelativeOffset >= textureStart && end <= textureEnd)
        {
            domain = Id65AuthoringRemotePadTerrainSetAggregateRangeDomain
                .NativeTexturePages;
        }
        else if (range.OwnerId == TerrainModelOwnerId)
        {
            domain = Id65AuthoringRemotePadTerrainSetAggregateRangeDomain
                .TerrainSetModel;
        }
        else if (range.OwnerId == "composite.v3-spawn")
        {
            domain = Id65AuthoringRemotePadTerrainSetAggregateRangeDomain.Spawn;
        }
        else if (range.OwnerId == Id65RemotePadRedGemT21Catalog.PlacementId)
        {
            domain = Id65AuthoringRemotePadTerrainSetAggregateRangeDomain.RedGem;
        }
        else if (range.OwnerId is
                     Id65AuthoringMobyDependencyBundleCompiler
                         .ArtisansGrassBundleId or
                     Id65RemotePadGrassT107Catalog.PlacementId or
                     "allocation.rows" or "allocation.fixups")
        {
            domain = Id65AuthoringRemotePadTerrainSetAggregateRangeDomain.Grass;
        }
        else
        {
            throw new InvalidDataException(
                "Schema-5 physical range " + range.StableId +
                " has an unrecognized aggregate owner " + range.OwnerId + ".");
        }
        return new(
            range.StableId,
            range.OwnerId,
            domain,
            range.DataRelativeOffset,
            range.CopyBefore(),
            range.CopyAfter());
    }

    private static TransactionPartition ValidateAndBuildPartition(
        byte[] source,
        byte[] output,
        IReadOnlyList<Id65AuthoringRemotePadTerrainSetAggregateOwnedRange> owned,
        Id65RemotePadMobyKind kind)
    {
        if (source.Length != ExpectedRow80ByteLength ||
            output.Length != ExpectedRow80ByteLength ||
            owned.Count == 0 ||
            HasGrassT107Range(owned) !=
                (kind == Id65RemotePadMobyKind.GrassT107) ||
            !HasExactResidentT21XyzOwnership(owned, kind) ||
            owned.Select(item => item.StableId)
                .Distinct(StringComparer.Ordinal).Count() != owned.Count)
        {
            throw new InvalidDataException(
                "The schema-5 aggregate transaction dimensions changed.");
        }

        List<Id65AuthoringEditorAggregateProtectedRange> protectedRanges = [];
        int cursor = 0;
        int guarded = 0;
        int changed = 0;
        for (int index = 0; index < owned.Count; index++)
        {
            Id65AuthoringRemotePadTerrainSetAggregateOwnedRange range = owned[index];
            byte[] before = range.CopyBefore();
            byte[] after = range.CopyAfter();
            if (range.TargetWadEntry != 80 ||
                range.WadOffset != TargetDataWadOffset + range.DataRelativeOffset ||
                range.DataRelativeOffset < cursor || range.ByteLength <= 0 ||
                range.DataRelativeOffset + (long)range.ByteLength > source.Length ||
                before.Length != range.ByteLength ||
                after.Length != range.ByteLength ||
                Hash(before) != range.BeforeSha256 ||
                Hash(after) != range.AfterSha256 ||
                !ValidateOwnedRangeIdentity(range) ||
                !source.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(before) ||
                !output.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(after) ||
                before.SequenceEqual(after))
            {
                throw new InvalidDataException(
                    "Schema-5 aggregate range " + range.StableId +
                    " lost destination, order, hash, preimage, or afterimage identity.");
            }
            if (cursor < range.DataRelativeOffset)
            {
                int length = range.DataRelativeOffset - cursor;
                if (!source.AsSpan(cursor, length).SequenceEqual(
                        output.AsSpan(cursor, length)))
                {
                    throw new InvalidDataException(
                        "The schema-5 aggregate changed a protected gap.");
                }
                protectedRanges.Add(new(
                    cursor,
                    length,
                    Hash(source.AsSpan(cursor, length))));
            }
            cursor = checked(range.DataRelativeOffset + range.ByteLength);
            guarded = checked(guarded + range.ByteLength);
            changed = checked(changed + CountChangedBytes(before, after));
        }
        if (cursor < source.Length)
        {
            int length = source.Length - cursor;
            if (!source.AsSpan(cursor, length).SequenceEqual(
                    output.AsSpan(cursor, length)))
            {
                throw new InvalidDataException(
                    "The schema-5 aggregate changed its protected tail.");
            }
            protectedRanges.Add(new(
                cursor,
                length,
                Hash(source.AsSpan(cursor, length))));
        }
        byte[] rebuilt = ApplyAllRanges(source, owned);
        if (!rebuilt.SequenceEqual(output))
        {
            throw new InvalidDataException(
                "The schema-5 aggregate owned ranges do not reproduce the compiler output.");
        }
        int protectedBytes = source.Length - guarded;
        if (changed != CountChangedBytes(source, output) ||
            changed > guarded ||
            protectedBytes < 0 ||
            protectedRanges.Count == 0 ||
            guarded + protectedBytes != ExpectedRow80ByteLength)
        {
            throw new InvalidDataException(
                "The schema-5 aggregate guarded/protected partition changed.");
        }
        return new(
            protectedRanges.AsReadOnly(),
            guarded,
            protectedBytes,
            HashProtectedBytes(source, owned));
    }

    private static void ValidateInverse(
        byte[] source,
        byte[] output,
        IReadOnlyList<Id65AuthoringRemotePadTerrainSetAggregateOwnedRange> inverse,
        int expectedRangeCount)
    {
        if (expectedRangeCount <= 0 || inverse.Count != expectedRangeCount ||
            !ApplyAllRanges(output, inverse).SequenceEqual(source))
        {
            throw new InvalidDataException(
                "The schema-5 aggregate inverse transaction changed.");
        }
    }

    private static byte[] ApplyAllRanges(
        byte[] input,
        IReadOnlyList<Id65AuthoringRemotePadTerrainSetAggregateOwnedRange> ranges)
    {
        byte[] output = input.ToArray();
        foreach (Id65AuthoringRemotePadTerrainSetAggregateOwnedRange range in ranges)
        {
            byte[] before = range.CopyBefore();
            if (!output.AsSpan(range.DataRelativeOffset, range.ByteLength)
                .SequenceEqual(before))
            {
                throw new InvalidDataException(
                    "Schema-5 aggregate range " + range.StableId +
                    " lost its exact transactional preimage.");
            }
            range.CopyAfter().CopyTo(output, range.DataRelativeOffset);
        }
        return output;
    }

    private static Id65AuthoringEditorAggregateAtomicGroup[] BuildAtomicGroups(
        IReadOnlyList<Id65AuthoringRemotePadTerrainSetAggregateOwnedRange> owned,
        Id65RemotePadMobyKind kind)
    {
        Id65AuthoringEditorAggregateAtomicGroup Create(
            string id,
            Func<Id65AuthoringRemotePadTerrainSetAggregateOwnedRange, bool> predicate)
        {
            string[] stableIds = owned.Where(predicate)
                .Select(item => item.StableId)
                .ToArray();
            return new(
                id,
                stableIds.Length,
                Hash(Encoding.UTF8.GetBytes(string.Join('\n', stableIds))),
                AllRangesRequired: true,
                IndependentlyApplicable: false);
        }
        Id65AuthoringEditorAggregateAtomicGroup[] groups =
        [
            Create("v3-terrain-private-t66", item => item.Domain is
                Id65AuthoringRemotePadTerrainSetAggregateRangeDomain
                    .NativeTexturePages or
                Id65AuthoringRemotePadTerrainSetAggregateRangeDomain
                    .TerrainSetModel),
            Create("landing-t92", item => item.Domain ==
                Id65AuthoringRemotePadTerrainSetAggregateRangeDomain.Spawn),
            Create("artisans-grass-remote-pad-t107", item => item.Domain ==
                Id65AuthoringRemotePadTerrainSetAggregateRangeDomain.Grass),
            Create("resident-red-gem-remote-pad-t21", item => item.Domain ==
                Id65AuthoringRemotePadTerrainSetAggregateRangeDomain.RedGem)
        ];
        int terrainCount = groups[0].OwnedRangeCount;
        int spawnCount = groups[1].OwnedRangeCount;
        int grassCount = groups[2].OwnedRangeCount;
        int redGemCount = groups[3].OwnedRangeCount;
        if (terrainCount <= 0 || spawnCount <= 0 || grassCount <= 0 ||
            redGemCount !=
                (kind == Id65RemotePadMobyKind.RedGemT21 ? 1 : 0) ||
            groups.Sum(item => item.OwnedRangeCount) != owned.Count ||
            groups.Any(item =>
                !item.AllRangesRequired || item.IndependentlyApplicable))
        {
            throw new InvalidDataException(
                "The schema-5 aggregate atomic-group partition changed.");
        }
        return groups;
    }

    private static bool HasGrassT107Range(
        IReadOnlyList<Id65AuthoringRemotePadTerrainSetAggregateOwnedRange> owned) =>
        owned.Any(item => item.OwnerId ==
            Id65RemotePadGrassT107Catalog.PlacementId);

    private static bool HasExactResidentT21XyzOwnership(
        IReadOnlyList<Id65AuthoringRemotePadTerrainSetAggregateOwnedRange> owned,
        Id65RemotePadMobyKind kind)
    {
        Id65AuthoringRemotePadTerrainSetAggregateOwnedRange[] related = owned
            .Where(item =>
                item.StableId ==
                    Id65AuthoringRemotePadTerrainSetPhysicalCompiler
                        .ResidentT21XyzOwnedRangeStableId ||
                item.OwnerId == Id65RemotePadRedGemT21Catalog.PlacementId ||
                item.Domain ==
                    Id65AuthoringRemotePadTerrainSetAggregateRangeDomain.RedGem)
            .ToArray();
        if (kind != Id65RemotePadMobyKind.RedGemT21)
            return related.Length == 0;
        return related.Length == 1 && IsExactResidentT21XyzRange(related[0]);
    }

    private static bool IsExactResidentT21XyzRange(
        Id65AuthoringRemotePadTerrainSetAggregateOwnedRange range) =>
        range.StableId ==
            Id65AuthoringRemotePadTerrainSetPhysicalCompiler
                .ResidentT21XyzOwnedRangeStableId &&
        range.OwnerId == Id65RemotePadRedGemT21Catalog.PlacementId &&
        range.Domain ==
            Id65AuthoringRemotePadTerrainSetAggregateRangeDomain.RedGem &&
        range.DataRelativeOffset ==
            Id65AuthoringRemotePadMobyChoicePhysicalCompiler
                .ResidentT21CoordinateOffset &&
        range.ByteLength ==
            Id65AuthoringRemotePadMobyChoicePhysicalCompiler
                .ResidentT21CoordinateByteLength;

    private static Id65AuthoringEditorAggregateDestinationBinding
        BuildDestinationBinding(
            UnusedLevel65BlankLevelLabManifest manifest,
            Id65AuthoringEditorAggregateBoundSource bound)
    {
        Id65AuthoringEditorAggregateDestinationBinding result = new(
            bound.SnapshotProfileId,
            bound.SourceImageSha256,
            manifest.LockedBaseProfileId,
            manifest.LockedBaseImageSha256,
            "WAD.WAD",
            TargetOverlayWadEntry,
            TargetOverlayWadOffset,
            TargetOverlayByteLength,
            manifest.DataWadEntry,
            TargetDataWadOffset,
            ExpectedRow80ByteLength,
            bound.TargetDataDirectoryRowSha256,
            manifest.ObjectTableWadOffset,
            manifest.ObjectTableRelativeOffset,
            RetailTownSquareOverlayWadEntry,
            RetailTownSquareOverlayWadOffset,
            RetailTownSquareDataWadEntry,
            RetailTownSquareDataWadOffset,
            bound.RejectedRetailDataDirectoryRowSha256,
            TextureDonorWadEntry,
            GrassDonorWadEntry,
            PhysicalSnapshotVerified: true,
            RetailTownSquareDestinationRejected: true,
            DonorLocationsReadOnlyProvenance: true,
            PhysicalDestinationRevalidationRequired: true);
        if (result.SourceSnapshotProfileId != Id65RewardCensusContract.ProfileId ||
            result.SourceImageSha256 != manifest.LockedBaseImageSha256 ||
            result.TargetDataWadEntry != 80 ||
            result.TargetDataWadOffset != TargetDataWadOffset ||
            result.TargetDataByteLength != ExpectedRow80ByteLength ||
            result.TargetDataDirectoryRowSha256 !=
                "3c6ae9564c3f6fc794bbae9670ee0dff7bd9a7bdc0f96c3c206c752551d8c09a" ||
            result.TargetObjectTableWadOffset -
                result.TargetObjectTableRelativeOffset != TargetDataWadOffset ||
            result.RejectedRetailDataWadEntry != 16 ||
            result.RejectedRetailDataWadOffset != RetailTownSquareDataWadOffset ||
            result.RejectedRetailDataDirectoryRowSha256 !=
                "ba8329f16f9caed1bed39c6b794968ff95160adbfd2480c5d4bbb6fc7545f77b" ||
            result.TargetDataWadOffset == result.RejectedRetailDataWadOffset ||
            !result.PhysicalSnapshotVerified ||
            !result.RetailTownSquareDestinationRejected ||
            !result.DonorLocationsReadOnlyProvenance ||
            !result.PhysicalDestinationRevalidationRequired)
        {
            throw new InvalidDataException(
                "The schema-5 aggregate physical destination binding changed.");
        }
        return result;
    }

    private static void ValidateExactLabManifest(
        UnusedLevel65BlankLevelLabManifest manifest)
    {
        if (manifest.ManifestSchemaVersion !=
                UnusedLevel65BlankLevelLabProfileRegistry.ManifestSchemaVersion ||
            manifest.ProfileId !=
                UnusedLevel65BlankLevelLabProfileRegistry.ProfileId ||
            manifest.ProfileVersion !=
                UnusedLevel65BlankLevelLabProfileRegistry.ProfileVersion ||
            manifest.WorkspaceKey != UnusedLevel65BlankLevelLabProfileRegistry.Key ||
            manifest.WorkspaceLayoutVersion !=
                UnusedLevel65BlankLevelLabProfileRegistry.WorkspaceLayoutVersion ||
            manifest.WorkspaceLayoutRelativeRoot !=
                UnusedLevel65BlankLevelLabProfileRegistry.WorkspaceLayoutRelativeRoot ||
            manifest.DisplayName !=
                UnusedLevel65BlankLevelLabProfileRegistry.Profile.DisplayName ||
            manifest.LevelId != 65 || manifest.DataWadEntry != 80 ||
            manifest.ObjectTableWadOffset !=
                UnusedLevel65BlankLevelLabProfileRegistry.ObjectTableWadOffset ||
            manifest.ObjectTableRelativeOffset !=
                UnusedLevel65BlankLevelLabProfileRegistry.ObjectTableRelativeOffset ||
            manifest.ObjectRecordCount !=
                UnusedLevel65BlankLevelLabProfileRegistry.ObjectRecordCount ||
            manifest.LockedBaseImageRelativePath !=
                "locked-base/unusedlevel65blank-locked-base-v1.bin" ||
            manifest.LockedBaseCueRelativePath !=
                "locked-base/unusedlevel65blank-locked-base-v1.cue" ||
            manifest.AuthoredEditsRelativePath != "authored-edits" ||
            manifest.CleanSourceImageSha256 !=
                UnusedLevel65BlankLevelLabProfileRegistry.CleanUsaImageSha256 ||
            manifest.PhysicalCloneProfileId !=
                UnusedLevel65BlankLevelLabProfileRegistry.Profile.PhysicalCloneProfileId ||
            manifest.PhysicalCloneImageSha256 !=
                UnusedLevel65BlankLevelLabProfileRegistry.Profile.PhysicalCloneImageSha256 ||
            manifest.PhysicalCloneEvidence !=
                UnusedLevel65BlankLevelLabProfileRegistry.PhysicalCloneEvidence ||
            manifest.LockedBaseProfileId !=
                UnusedLevel65BlankLevelLabProfileRegistry.Profile.LockedBaseProfileId ||
            manifest.LockedBaseImageSha256 !=
                UnusedLevel65BlankLevelLabProfileRegistry.Profile.LockedBaseImageSha256 ||
            manifest.LockedBaseEvidence !=
                UnusedLevel65BlankLevelLabProfileRegistry.LockedBaseEvidence ||
            manifest.Capabilities !=
                UnusedLevel65BlankLevelLabProfileRegistry.Capabilities ||
            !manifest.ExternalGameDataRequired || manifest.GameDataEmbedded ||
            manifest.PromotionAuthorized || manifest.NormalCreateBinEnabled)
        {
            throw new InvalidDataException(
                "The schema-5 aggregate lab manifest is not the exact WAD80 workspace.");
        }
    }

    private static void ValidatePlan(
        Id65AuthoringRemotePadTerrainSetAggregatePlan plan)
    {
        Id65RemotePadMobyKind kind = plan.MobyKind;
        Id65AuthoringTerrainSetManifest manifest =
            Id65AuthoringTerrainSetCatalog.Create(plan.ActiveTerrainUnitIds);
        Id65AuthoringEditorDocument schema4Baseline =
            Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceExact(
                kind,
                plan.Placement.PositionId,
                includeOptionalTerrain: false);
        Id65RemotePadGrassT107Position position =
            Id65RemotePadGrassT107Catalog.GetRequiredPosition(
                plan.Placement.PositionId);
        int textureRanges = plan.OwnedRanges.Count(item => item.Domain ==
            Id65AuthoringRemotePadTerrainSetAggregateRangeDomain
                .NativeTexturePages);
        int modelRanges = plan.OwnedRanges.Count(item => item.Domain ==
            Id65AuthoringRemotePadTerrainSetAggregateRangeDomain
                .TerrainSetModel);
        int spawnRanges = plan.OwnedRanges.Count(item => item.Domain ==
            Id65AuthoringRemotePadTerrainSetAggregateRangeDomain.Spawn);
        int grassRanges = plan.OwnedRanges.Count(item => item.Domain ==
            Id65AuthoringRemotePadTerrainSetAggregateRangeDomain.Grass);
        int redGemRanges = plan.OwnedRanges.Count(item => item.Domain ==
            Id65AuthoringRemotePadTerrainSetAggregateRangeDomain.RedGem);
        Id65AuthoringEditorAggregateAtomicGroup[] expectedGroups =
            BuildAtomicGroups(plan.OwnedRanges, kind);
        bool inverseExact = plan.InverseRanges.Count == plan.OwnedRanges.Count;
        for (int index = 0; inverseExact && index < plan.OwnedRanges.Count; index++)
        {
            Id65AuthoringRemotePadTerrainSetAggregateOwnedRange forward =
                plan.OwnedRanges[index];
            Id65AuthoringRemotePadTerrainSetAggregateOwnedRange inverse =
                plan.InverseRanges[plan.InverseRanges.Count - index - 1];
            inverseExact =
                forward.StableId == inverse.StableId &&
                forward.OwnerId == inverse.OwnerId &&
                forward.Domain == inverse.Domain &&
                forward.DataRelativeOffset == inverse.DataRelativeOffset &&
                forward.ByteLength == inverse.ByteLength &&
                forward.BeforeSha256 == inverse.AfterSha256 &&
                forward.AfterSha256 == inverse.BeforeSha256 &&
                forward.CopyBefore().SequenceEqual(inverse.CopyAfter()) &&
                forward.CopyAfter().SequenceEqual(inverse.CopyBefore());
        }
        if (plan.ProfileId != ProfileId || plan.SchemaVersion != SchemaVersion ||
            plan.LevelId != 65 || plan.DataWadEntry != 80 ||
            plan.Document.SchemaVersion !=
                Id65AuthoringEditorDocument
                    .RemotePadMobyChoiceTerrainSetSchemaVersion ||
            plan.Document.ProfileId != Id65AuthoringEditorDocument
                .RemotePadMobyChoiceTerrainSetDocumentProfileId ||
            !plan.Document.UsesRemotePadMobyChoice ||
            !plan.Document.UsesRemotePadTerrainSet ||
            plan.Document.UsesRemotePadT107Grid ||
            plan.DocumentCanonicalSha256 != plan.Document.CanonicalSha256 ||
            plan.Document.TerrainSetManifestSha256 != manifest.CanonicalSha256 ||
            plan.TerrainManifestSha256 != manifest.CanonicalSha256 ||
            plan.StaticAuthoringTerrainManifestSha256 != manifest.CanonicalSha256 ||
            !plan.ActiveTerrainUnitIds.SequenceEqual(
                manifest.ActiveUnits.Select(item => item.Id)) ||
            plan.TerrainUnitCount != manifest.ActiveUnits.Count ||
            plan.MobyKind != plan.Document.RemotePadMobyKind ||
            plan.Placement.Kind != kind ||
            plan.Placement.PositionId != position.PositionId ||
            plan.Placement.PositionOrdinal != position.Ordinal ||
            plan.Placement.EditorX != position.EditorX ||
            plan.Placement.EditorY != position.EditorY ||
            plan.Placement.EditorZ != position.EditorZ ||
            plan.Placement.RawX != position.RawX ||
            plan.Placement.RawY != position.RawY ||
            plan.Placement.RawZ != position.RawZ ||
            plan.Placement.Present != (kind != Id65RemotePadMobyKind.None) ||
            plan.Placement.ReusesResidentRow !=
                (kind == Id65RemotePadMobyKind.RedGemT21) ||
            plan.SelectedExactRowSha256 !=
                Id65RemotePadMobyChoiceCatalog.GetSelectedExactRowSha256(
                    kind,
                    position.PositionId) ||
            plan.CatalogCanonicalSha256 !=
                Id65RemotePadMobyChoiceCatalog.ExpectedCanonicalSha256 ||
            plan.StaticAuthoringProfileId != Id65AuthoringEditorDocumentStore
                .RemotePadMobyChoiceTerrainSetStaticAuthoringProfileId ||
            !IsCanonicalSha256(plan.StaticAuthoringCatalogSha256) ||
            !IsCanonicalSha256(plan.StaticAuthoringSceneGraphSha256) ||
            !IsCanonicalSha256(plan.StaticAuthoringProjectionSha256) ||
            plan.SourceRow80Sha256 !=
                Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 ||
            plan.OutputTexturePagesSha256 !=
                Id65V2NativeTextureCompositionCompiler
                    .ExpectedOutputTexturePagesSha256 ||
            plan.TargetOverlaySha256 !=
                Id65AuthoringCompositeCompiler.ExpectedTargetOverlaySha256 ||
            plan.GlobalExecutableSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedGlobalExecutableSha256 ||
            plan.NativeInputSetSha256 !=
                Id65AuthoringEditorNativeInputProvider.ExpectedCanonicalInputSetSha256 ||
            plan.V3DeterministicPlanSha256 !=
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction
                    .ExpectedDeterministicPlanSha256 ||
            plan.Schema4BaselineDocumentSha256 !=
                schema4Baseline.CanonicalSha256 ||
            !IsCanonicalSha256(plan.Schema4BaselineOutputRow80Sha256) ||
            !IsCanonicalSha256(plan.Schema4BaselineOutputModelSha256) ||
            !IsCanonicalSha256(
                plan.Schema4BaselineDeterministicPlanSha256) ||
            plan.OutputModelSha256 != plan.Terrain.T66TerrainModelSha256 ||
            plan.Terrain.TerrainManifestSha256 != manifest.CanonicalSha256 ||
            !plan.Terrain.ActiveTerrainUnitIds.SequenceEqual(
                plan.ActiveTerrainUnitIds) ||
            !plan.Terrain.Units.Select(item => item.UnitId).SequenceEqual(
                plan.ActiveTerrainUnitIds) ||
            plan.Terrain.UsedModelBytes + plan.Terrain.ZeroTailBytes !=
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelLength ||
            plan.Terrain.MandatoryBaseFaceTextureId !=
                Id65AuthoringEditorDocument.PrivateMandatoryBaseFaceTextureId ||
            plan.Terrain.MandatoryBaseFaceModelOffset < 0 ||
            plan.Terrain.MandatoryBaseFaceModelOffset >=
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelLength ||
            plan.Terrain.T66TextureComponentSha256 !=
                Id65V2NativeTextureCompositionCompiler
                    .ExpectedOutputTextureComponentSha256 ||
            !IsCanonicalSha256(plan.Terrain.V3TerrainModelSha256) ||
            !IsCanonicalSha256(plan.Terrain.V3TerrainEnvironmentSha256) ||
            !IsCanonicalSha256(plan.Terrain.FinalT66EnvironmentSha256) ||
            !IsCanonicalSha256(plan.Terrain.CollisionSha256) ||
            !IsCanonicalSha256(
                plan.Terrain.V3TerrainProtectedComplementSha256) ||
            !IsCanonicalSha256(
                plan.Terrain.FinalT66ProtectedComponentSetSha256) ||
            !IsCanonicalSha256(plan.EnvelopeCanonicalSha256) ||
            plan.OwnedRangeCount <= 0 ||
            plan.InverseRangeCount != plan.OwnedRangeCount ||
            plan.ProtectedRangeCount <= 0 ||
            textureRanges <= 0 || modelRanges <= 0 || spawnRanges <= 0 ||
            grassRanges <= 0 ||
            redGemRanges !=
                (kind == Id65RemotePadMobyKind.RedGemT21 ? 1 : 0) ||
            !HasExactResidentT21XyzOwnership(plan.OwnedRanges, kind) ||
            !HasExactResidentT21XyzPayload(plan) ||
            plan.AtomicGroups.Count != 4 ||
            !plan.AtomicGroups.SequenceEqual(expectedGroups) ||
            !inverseExact ||
            plan.GuardedByteCount < plan.ChangedByteCount ||
            CountChangedBytes(plan.OwnedRanges) != plan.ChangedByteCount ||
            plan.GuardedByteCount - plan.ChangedByteCount !=
                CountResidentT21UnchangedGuardedBytes(plan.OwnedRanges) ||
            plan.GuardedByteCount !=
                plan.OwnedRanges.Sum(item => item.ByteLength) ||
            plan.ProtectedByteCount !=
                plan.ProtectedRanges.Sum(item => item.ByteLength) ||
            plan.GuardedByteCount + plan.ProtectedByteCount !=
                ExpectedRow80ByteLength ||
            !RangesCoverWholeRow(plan.OwnedRanges, plan.ProtectedRanges) ||
            plan.OwnedRanges.Any(item => !ValidateOwnedRangeIdentity(item)) ||
            plan.ProtectedRanges.Any(item =>
                item.ByteLength <= 0 ||
                !IsCanonicalSha256(item.SegmentSha256)) ||
            !IsCanonicalSha256(plan.ProtectedComplementSha256) ||
            !IsCanonicalSha256(plan.OutputRow80Sha256) ||
            !IsCanonicalSha256(plan.OutputModelSha256) ||
            !IsCanonicalSha256(plan.LandingSha256) ||
            !IsCanonicalSha256(plan.PlayerAnchorSha256) ||
            !IsCanonicalSha256(plan.CompilerOwnedRangeMapSha256) ||
            !IsCanonicalSha256(plan.CompilerTransactionSha256) ||
            !IsCanonicalSha256(plan.CompilerDeterministicPlanSha256) ||
            !IsCanonicalSha256(plan.OwnedRangeMapSha256) ||
            !IsCanonicalSha256(plan.InverseRangeMapSha256) ||
            !IsCanonicalSha256(plan.ProtectedRangeMapSha256) ||
            !IsCanonicalSha256(plan.AtomicGroupMapSha256) ||
            !IsCanonicalSha256(plan.DeterministicAggregatePlanSha256) ||
            plan.OwnedRangeMapSha256 != HashOwnedRanges(plan.OwnedRanges) ||
            plan.InverseRangeMapSha256 != HashOwnedRanges(plan.InverseRanges) ||
            plan.ProtectedRangeMapSha256 !=
                HashProtectedRanges(plan.ProtectedRanges) ||
            plan.AtomicGroupMapSha256 != HashAtomicGroups(plan.AtomicGroups) ||
            plan.Destination.SourceSnapshotProfileId !=
                Id65RewardCensusContract.ProfileId ||
            plan.Destination.SourceImageSha256 !=
                Id65RewardCensusContract.LockedSourceImageSha256 ||
            plan.Destination.TargetOverlayWadEntry != TargetOverlayWadEntry ||
            plan.Destination.TargetOverlayWadOffset != TargetOverlayWadOffset ||
            plan.Destination.TargetOverlayByteLength !=
                TargetOverlayByteLength ||
            plan.Destination.TargetDataWadEntry != 80 ||
            plan.Destination.TargetDataWadOffset != TargetDataWadOffset ||
            plan.Destination.TargetDataByteLength != ExpectedRow80ByteLength ||
            plan.Destination.RejectedRetailOverlayWadEntry !=
                RetailTownSquareOverlayWadEntry ||
            plan.Destination.RejectedRetailOverlayWadOffset !=
                RetailTownSquareOverlayWadOffset ||
            plan.Destination.RejectedRetailDataWadEntry !=
                RetailTownSquareDataWadEntry ||
            plan.Destination.RejectedRetailDataWadOffset !=
                RetailTownSquareDataWadOffset ||
            plan.Destination.TargetDataWadOffset ==
                plan.Destination.RejectedRetailDataWadOffset ||
            !plan.Destination.PhysicalSnapshotVerified ||
            !plan.Destination.RetailTownSquareDestinationRejected ||
            !plan.Destination.DonorLocationsReadOnlyProvenance ||
            !plan.Destination.PhysicalDestinationRevalidationRequired ||
            !plan.PureInMemory ||
            !plan.FullExactSchema5DocumentRequired ||
            !plan.SuppliedStoreResultMatchesExpectedEnvelopeIdentity ||
            !plan.StaticAuthoringProjectionRecomputedFromCanonicalDocument ||
            !plan.StoreEnvelopeIdentityIndependentlyVerified ||
            plan.StaticAuthoringProjectionRecomputedFromStoreBytes ||
            !plan.NativeInputGenerationReverified ||
            !plan.LockedSourceOverlayExecutableIdentityPreserved ||
            !plan.ExactPhysicalDestinationBindingPinned ||
            !plan.PhysicalDestinationReadbackVerified ||
            !plan.FourTopologyTerrainSetBindingVerified ||
            !plan.FinalT66TerrainSemanticReadbackVerified ||
            !plan.RepairedT13995SupportBindingVerified ||
            !plan.RemotePadMobyChoiceCatalogBindingVerified ||
            !plan.GrassT88ExcludedVerified ||
            !plan.MutuallyExclusiveMobyChoiceVerified ||
            !plan.ResidentT21XyzOnlyOwnershipVerified ||
            !plan.ProtectedComplementVerified || !plan.ExactInverseVerified ||
            !plan.OneAtomicTransactionRequired ||
            !plan.ContainsOwnedAfterimageBytes ||
            plan.ContainsCompleteCompositeAfterimage ||
            plan.ContainsDiscImageBytes || plan.ContainsCueBytes ||
            plan.PartialRangeApplicationAuthorized ||
            plan.CrossSliceCompositionSupported ||
            plan.AfterimageStackingAuthorized || plan.GenericExporterCompatible ||
            plan.RetailWriterCompatible || plan.RetailLevelMutationAuthorized ||
            plan.PersistenceEnabled || plan.WriterAuthorized ||
            plan.AggregateMaterializerEnabled || plan.CandidateMaterializerEnabled ||
            plan.PhysicalMaterializationEnabled || plan.WritesFileSystem ||
            plan.WritesDiscImage || plan.WritesBin || plan.WritesCue ||
            plan.AppIntegrated || plan.UiIntegrated || plan.CreateBinEnabled ||
            plan.NormalCreateBinEnabled || plan.RuntimeCandidateAuthorized ||
            plan.RuntimeProven || plan.SaveAuthorized || plan.SavePersistenceProven ||
            plan.TreasureTotal200Proven || plan.MusicLongPlayProven ||
            plan.ExitDestination65Proven || plan.ReleaseIntegrated ||
            plan.ReleaseAuthorized || plan.PromotionAuthorized || plan.Publishable ||
            plan.CurrentOnDiskFreshnessVerified ||
            !plan.RequiresTrustedExclusiveWorkspaceDirectory ||
            plan.HostileFilesystemRaceSafe || plan.CommonWorkspaceLeaseOwned)
        {
            throw new InvalidDataException(
                "The schema-5 aggregate plan identity or authority boundary changed.");
        }
    }

    private static bool ValidateOwnedRangeIdentity(
        Id65AuthoringRemotePadTerrainSetAggregateOwnedRange range)
    {
        byte[] before = range.CopyBefore();
        byte[] after = range.CopyAfter();
        int actualChangedBytes = CountChangedBytes(before, after);
        long end = range.DataRelativeOffset + (long)range.ByteLength;
        int redGemXyzStart = Id65AuthoringRemotePadMobyChoicePhysicalCompiler
            .ResidentT21CoordinateOffset;
        int redGemXyzEnd = checked(redGemXyzStart +
            Id65AuthoringRemotePadMobyChoicePhysicalCompiler
                .ResidentT21CoordinateByteLength);
        int textureStart =
            Id65V2NativeTextureCompositionCompiler.TexturePagesDataRelativeOffset;
        int textureEnd = checked(textureStart +
            Id65V2NativeTextureCompositionCompiler.TexturePagesByteLength);
        int modelStart = Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset;
        int modelEnd = checked(modelStart +
            Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelLength);
        bool domainMatches = range.Domain switch
        {
            Id65AuthoringRemotePadTerrainSetAggregateRangeDomain
                    .NativeTexturePages =>
                range.DataRelativeOffset >= textureStart && end <= textureEnd,
            Id65AuthoringRemotePadTerrainSetAggregateRangeDomain
                    .TerrainSetModel =>
                range.OwnerId == TerrainModelOwnerId &&
                range.DataRelativeOffset >= modelStart && end <= modelEnd,
            Id65AuthoringRemotePadTerrainSetAggregateRangeDomain.Spawn =>
                range.OwnerId == "composite.v3-spawn",
            Id65AuthoringRemotePadTerrainSetAggregateRangeDomain.Grass =>
                range.OwnerId is
                    Id65AuthoringMobyDependencyBundleCompiler
                        .ArtisansGrassBundleId or
                    Id65RemotePadGrassT107Catalog.PlacementId or
                    "allocation.rows" or "allocation.fixups",
            Id65AuthoringRemotePadTerrainSetAggregateRangeDomain.RedGem =>
                IsExactResidentT21XyzRange(range) &&
                range.DataRelativeOffset == redGemXyzStart &&
                end == redGemXyzEnd,
            _ => false
        };
        return domainMatches &&
            range.StableId.StartsWith("schema5.row80.", StringComparison.Ordinal) &&
            range.TargetWadEntry == 80 &&
            range.DataRelativeOffset >= 0 && range.ByteLength > 0 &&
            end <= ExpectedRow80ByteLength &&
            range.WadOffset == TargetDataWadOffset + range.DataRelativeOffset &&
            before.Length == range.ByteLength && after.Length == range.ByteLength &&
            Hash(before) == range.BeforeSha256 &&
            Hash(after) == range.AfterSha256 &&
            actualChangedBytes > 0 &&
            (range.Domain ==
                Id65AuthoringRemotePadTerrainSetAggregateRangeDomain.RedGem ||
             actualChangedBytes == range.ByteLength);
    }

    private static bool HasExactResidentT21XyzPayload(
        Id65AuthoringRemotePadTerrainSetAggregatePlan plan)
    {
        Id65AuthoringRemotePadTerrainSetAggregateOwnedRange[] related =
            plan.OwnedRanges.Where(item =>
                item.StableId ==
                    Id65AuthoringRemotePadTerrainSetPhysicalCompiler
                        .ResidentT21XyzOwnedRangeStableId ||
                item.OwnerId == Id65RemotePadRedGemT21Catalog.PlacementId ||
                item.Domain ==
                    Id65AuthoringRemotePadTerrainSetAggregateRangeDomain.RedGem)
            .ToArray();
        if (plan.MobyKind != Id65RemotePadMobyKind.RedGemT21)
            return related.Length == 0;
        if (related.Length != 1 || !IsExactResidentT21XyzRange(related[0]))
            return false;

        byte[] exactSourceT21 = Convert.FromHexString(
            Id65RemotePadRedGemT21Catalog.SourceRowExactHex);
        byte[] exactOutputT21 = Id65RemotePadRedGemT21Catalog.CreateExactRowBytes(
            plan.Placement.PositionId);
        ReadOnlySpan<byte> exactSourceXyz = exactSourceT21.AsSpan(
            Id65RemotePadRedGemT21Catalog.RowCoordinateOffset,
            Id65RemotePadRedGemT21Catalog.RowCoordinateByteLength);
        ReadOnlySpan<byte> exactOutputXyz = exactOutputT21.AsSpan(
            Id65RemotePadRedGemT21Catalog.RowCoordinateOffset,
            Id65RemotePadRedGemT21Catalog.RowCoordinateByteLength);
        return related[0].CopyBefore().AsSpan().SequenceEqual(exactSourceXyz) &&
            related[0].CopyAfter().AsSpan().SequenceEqual(exactOutputXyz);
    }

    private static bool RangesCoverWholeRow(
        IReadOnlyList<Id65AuthoringRemotePadTerrainSetAggregateOwnedRange> owned,
        IReadOnlyList<Id65AuthoringEditorAggregateProtectedRange> protectedRanges)
    {
        (int Offset, int Length)[] partition = owned
            .Select(item => (item.DataRelativeOffset, item.ByteLength))
            .Concat(protectedRanges.Select(item =>
                (item.DataRelativeOffset, item.ByteLength)))
            .OrderBy(item => item.DataRelativeOffset)
            .ToArray();
        long cursor = 0;
        foreach ((int offset, int length) in partition)
        {
            if (offset != cursor || length <= 0)
                return false;
            cursor += length;
            if (cursor > ExpectedRow80ByteLength)
                return false;
        }
        return cursor == ExpectedRow80ByteLength;
    }

    private static int CountChangedBytes(byte[] before, byte[] after)
    {
        if (before.Length != after.Length)
        {
            throw new InvalidDataException(
                "The schema-5 aggregate changed-byte inputs differ in length.");
        }
        int count = 0;
        for (int index = 0; index < before.Length; index++)
        {
            if (before[index] != after[index])
                count++;
        }
        return count;
    }

    private static int CountChangedBytes(
        IEnumerable<Id65AuthoringRemotePadTerrainSetAggregateOwnedRange> ranges)
    {
        int count = 0;
        foreach (Id65AuthoringRemotePadTerrainSetAggregateOwnedRange range in ranges)
        {
            count = checked(count + CountChangedBytes(
                range.CopyBefore(),
                range.CopyAfter()));
        }
        return count;
    }

    private static int CountResidentT21UnchangedGuardedBytes(
        IEnumerable<Id65AuthoringRemotePadTerrainSetAggregateOwnedRange> ranges)
    {
        int unchanged = 0;
        foreach (Id65AuthoringRemotePadTerrainSetAggregateOwnedRange range in
                 ranges.Where(item => item.Domain ==
                     Id65AuthoringRemotePadTerrainSetAggregateRangeDomain.RedGem))
        {
            unchanged = checked(unchanged + range.ByteLength - CountChangedBytes(
                range.CopyBefore(),
                range.CopyAfter()));
        }
        return unchanged;
    }

    private static string HashOwnedRanges(
        IEnumerable<Id65AuthoringRemotePadTerrainSetAggregateOwnedRange> ranges) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', ranges.Select(item =>
            $"{item.StableId}|{item.OwnerId}|{item.Domain}|" +
            $"{item.DataRelativeOffset:X8}|{item.ByteLength:X8}|" +
            $"{item.BeforeSha256}|{item.AfterSha256}"))));

    private static string HashProtectedRanges(
        IEnumerable<Id65AuthoringEditorAggregateProtectedRange> ranges) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', ranges.Select(item =>
            $"{item.DataRelativeOffset:X8}|{item.ByteLength:X8}|" +
            item.SegmentSha256))));

    private static string HashAtomicGroups(
        IEnumerable<Id65AuthoringEditorAggregateAtomicGroup> groups) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', groups.Select(item =>
            $"{item.GroupId}|{item.OwnedRangeCount}|{item.StableIdSetSha256}|" +
            $"{item.AllRangesRequired}|{item.IndependentlyApplicable}"))));

    private static string HashProtectedBytes(
        byte[] source,
        IReadOnlyList<Id65AuthoringRemotePadTerrainSetAggregateOwnedRange> ranges)
    {
        using IncrementalHash hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int cursor = 0;
        foreach (Id65AuthoringRemotePadTerrainSetAggregateOwnedRange range in ranges)
        {
            if (cursor < range.DataRelativeOffset)
            {
                hash.AppendData(source.AsSpan(
                    cursor,
                    range.DataRelativeOffset - cursor));
            }
            cursor = checked(range.DataRelativeOffset + range.ByteLength);
        }
        if (cursor < source.Length)
            hash.AppendData(source.AsSpan(cursor));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

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
