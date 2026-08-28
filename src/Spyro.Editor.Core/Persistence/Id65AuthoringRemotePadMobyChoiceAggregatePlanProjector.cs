using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.Core.Persistence;

internal sealed record Id65AuthoringRemotePadMobyChoiceAggregatePlanRequest(
    Id65AuthoringEditorDocumentStoreResult StoredDocument,
    string ExpectedEnvelopeCanonicalSha256,
    UnusedLevel65BlankLevelLabManifest LabManifest,
    Id65AuthoringEditorNativeInputProvider.InputSet NativeInputs);

internal enum Id65AuthoringRemotePadMobyChoiceAggregateRangeDomain
{
    NativeTexturePages,
    CompositeModel,
    Spawn,
    Grass,
    RedGem
}

/// <summary>
/// One defensively copied leaf of the schema-4 aggregate transaction. These
/// leaves are descriptive parts of one indivisible in-memory plan and do not
/// authorize partial application or materialization.
/// </summary>
internal sealed class Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange
{
    private readonly byte[] _before;
    private readonly byte[] _after;

    internal Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange(
        string stableId,
        string ownerId,
        Id65AuthoringRemotePadMobyChoiceAggregateRangeDomain domain,
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
        BeforeSha256 = Id65AuthoringRemotePadMobyChoiceAggregatePlanProjector.Hash(
            _before);
        AfterSha256 = Id65AuthoringRemotePadMobyChoiceAggregatePlanProjector.Hash(
            _after);
    }

    public string StableId { get; }
    public string OwnerId { get; }
    public Id65AuthoringRemotePadMobyChoiceAggregateRangeDomain Domain { get; }
    public int TargetWadEntry => 80;
    public int DataRelativeOffset { get; }
    public long WadOffset => 0x6936800L + DataRelativeOffset;
    public int ByteLength => _before.Length;
    public string BeforeSha256 { get; }
    public string AfterSha256 { get; }

    internal byte[] CopyBefore() => _before.ToArray();
    internal byte[] CopyAfter() => _after.ToArray();
    internal Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange DeepCopy() =>
        new(StableId, OwnerId, Domain, DataRelativeOffset, _before, _after);
    internal Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange Invert() =>
        new(StableId, OwnerId, Domain, DataRelativeOffset, _after, _before);
}

/// <summary>
/// One immutable schema-4 WAD80 transaction derived from a supplied exact store
/// result, its expected envelope identity, and provider-owned native inputs. It
/// contains only owned range afterimages and hash/readback evidence; no
/// filesystem or release authority.
/// </summary>
internal sealed class Id65AuthoringRemotePadMobyChoiceAggregatePlan
{
    private readonly ReadOnlyCollection<
        Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange> _ownedRanges;
    private readonly ReadOnlyCollection<
        Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange> _inverseRanges;
    private readonly ReadOnlyCollection<Id65AuthoringEditorAggregateProtectedRange>
        _protectedRanges;
    private readonly ReadOnlyCollection<Id65AuthoringEditorAggregateAtomicGroup>
        _atomicGroups;

    internal Id65AuthoringRemotePadMobyChoiceAggregatePlan(
        Id65AuthoringEditorAggregateDestinationBinding destination,
        Id65AuthoringEditorDocument document,
        string envelopeCanonicalSha256,
        string staticAuthoringProjectionSha256,
        string nativeInputSetSha256,
        string sourceRow80Sha256,
        string outputRow80Sha256,
        string outputModelSha256,
        string sourceT21RowSha256,
        string outputT21RowSha256,
        string targetOverlaySha256,
        string globalExecutableSha256,
        string baselineOutputRow80Sha256,
        string baselineOutputModelSha256,
        string baselineDeterministicPlanSha256,
        string compilerOwnedRangeMapSha256,
        string compilerTransactionSha256,
        string compilerDeterministicPlanSha256,
        Id65AuthoringRemotePadMobyChoicePhysicalPlacementReadback placement,
        IEnumerable<Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange> ownedRanges,
        IEnumerable<Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange> inverseRanges,
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
        StructuralTerrainPresent = Document.Terrain.Count == 1;
        MobyKind = Document.RemotePadMobyKind!.Value;
        EnvelopeCanonicalSha256 = envelopeCanonicalSha256;
        StaticAuthoringProjectionSha256 = staticAuthoringProjectionSha256;
        NativeInputSetSha256 = nativeInputSetSha256;
        SourceRow80Sha256 = sourceRow80Sha256;
        OutputRow80Sha256 = outputRow80Sha256;
        OutputModelSha256 = outputModelSha256;
        SourceT21RowSha256 = sourceT21RowSha256;
        OutputT21RowSha256 = outputT21RowSha256;
        TargetOverlaySha256 = targetOverlaySha256;
        GlobalExecutableSha256 = globalExecutableSha256;
        BaselineOutputRow80Sha256 = baselineOutputRow80Sha256;
        BaselineOutputModelSha256 = baselineOutputModelSha256;
        BaselineDeterministicPlanSha256 = baselineDeterministicPlanSha256;
        CompilerOwnedRangeMapSha256 = compilerOwnedRangeMapSha256;
        CompilerTransactionSha256 = compilerTransactionSha256;
        CompilerDeterministicPlanSha256 = compilerDeterministicPlanSha256;
        Placement = placement;
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
        Id65AuthoringRemotePadMobyChoiceAggregatePlanProjector.ProfileId;
    public int SchemaVersion =>
        Id65AuthoringRemotePadMobyChoiceAggregatePlanProjector.SchemaVersion;
    public int LevelId => 65;
    public int DataWadEntry => 80;
    public Id65AuthoringEditorAggregateDestinationBinding Destination { get; }
    public Id65AuthoringEditorDocument Document { get; }
    public string DocumentCanonicalSha256 { get; }
    public bool StructuralTerrainPresent { get; }
    public Id65RemotePadMobyKind MobyKind { get; }
    public string EnvelopeCanonicalSha256 { get; }
    public string StaticAuthoringProjectionSha256 { get; }
    public string NativeInputSetSha256 { get; }
    public string CatalogCanonicalSha256 =>
        Id65RemotePadMobyChoiceCatalog.CanonicalSha256;
    public string? SelectedExactRowSha256 =>
        Id65RemotePadMobyChoiceCatalog.GetSelectedExactRowSha256(
            MobyKind,
            Placement.PositionId);
    public string SourceRow80Sha256 { get; }
    public string OutputRow80Sha256 { get; }
    public string OutputModelSha256 { get; }
    public string SourceT21RowSha256 { get; }
    public string OutputT21RowSha256 { get; }
    public string TargetOverlaySha256 { get; }
    public string GlobalExecutableSha256 { get; }
    public string BaselineOutputRow80Sha256 { get; }
    public string BaselineOutputModelSha256 { get; }
    public string BaselineDeterministicPlanSha256 { get; }
    public string CompilerOwnedRangeMapSha256 { get; }
    public string CompilerTransactionSha256 { get; }
    public string CompilerDeterministicPlanSha256 { get; }
    public Id65AuthoringRemotePadMobyChoicePhysicalPlacementReadback Placement { get; }
    public IReadOnlyList<Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange>
        OwnedRanges => _ownedRanges;
    public IReadOnlyList<Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange>
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
    public bool FullExactSchema4DocumentRequired => true;
    public bool SuppliedStoreResultMatchesExpectedEnvelopeIdentity => true;
    public bool StaticAuthoringProjectionRecomputedFromCanonicalDocument => true;
    public bool StoreEnvelopeIdentityIndependentlyVerified => true;
    public bool StaticAuthoringProjectionRecomputedFromStoreBytes => false;
    public bool NativeInputGenerationReverified => true;
    public bool LockedSourceOverlayExecutableIdentityPreserved => true;
    public bool ExactPhysicalDestinationBindingPinned => true;
    public bool PhysicalDestinationReadbackVerified => true;
    public bool T66StructuralTerrainBindingVerified => true;
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
/// Rebinds one supplied schema-4 document store result and its expected envelope
/// identity to a validated provider-owned input generation and the pure schema-4
/// physical compiler. It does not establish current on-disk freshness, returns
/// an in-memory transaction only, and exposes no writer route.
/// </summary>
internal static class Id65AuthoringRemotePadMobyChoiceAggregatePlanProjector
{
    public const string ProfileId =
        "id65-authoring-remote-pad-moby-choice-aggregate-plan-schema4-in-memory-v1";
    public const int SchemaVersion = 1;
    public const int ExpectedRow80ByteLength = 0x2E2000;
    public const int ExpectedTexturePageRangeCount = 7_949;
    public const int ExpectedOwnedRangeCountNone = 7_956;
    public const int ExpectedOwnedRangeCountGrass = 7_960;
    public const int ExpectedOwnedRangeCountRedGem = 7_957;
    public const int ExpectedGuardedByteCountNone = 1_010_852;
    public const int ExpectedGuardedByteCountGrass = 1_010_952;
    public const int ExpectedGuardedByteCountRedGem = 1_010_864;
    public const int ExpectedProtectedRangeCountNone = 7_957;
    public const int ExpectedProtectedRangeCountGrass = 7_961;
    public const int ExpectedProtectedRangeCountRedGem = 7_958;

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

    public static Id65AuthoringRemotePadMobyChoiceAggregatePlan Project(
        Id65AuthoringRemotePadMobyChoiceAggregatePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.StoredDocument);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            request.ExpectedEnvelopeCanonicalSha256);
        ArgumentNullException.ThrowIfNull(request.LabManifest);
        ArgumentNullException.ThrowIfNull(request.NativeInputs);
        Id65AuthoringEditorNativeInputProvider.ValidateForConsumption(
            request.NativeInputs);

        ValidateExactLabManifest(request.LabManifest);
        Id65AuthoringEditorDocument document = ValidateStoredDocument(
            request.StoredDocument,
            request.ExpectedEnvelopeCanonicalSha256,
            request.LabManifest);
        ValidateInputIdentity(request, document);

        Id65CompiledAuthoringRemotePadMobyChoicePhysical compiled =
            Id65AuthoringRemotePadMobyChoicePhysicalCompiler.Compile(new(
                document,
                request.NativeInputs.LockedSource,
                request.NativeInputs.PrivateTextureWitness,
                request.NativeInputs.GrassBundle));
        ValidateCompiled(document, compiled);

        byte[] source = compiled.CopySourceRow80();
        byte[] output = compiled.CopyOutputRow80();
        Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange[] owned =
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
                "The schema-4 aggregate protected complement changed from the physical compiler.");
        }
        Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange[] inverse = owned
            .Reverse()
            .Select(item => item.Invert())
            .ToArray();
        ValidateInverse(source, output, inverse, compiled.MobyKind);
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
            compiled.SourceT21RowSha256,
            compiled.OutputT21RowSha256,
            compiled.TargetOverlaySha256,
            compiled.GlobalExecutableSha256,
            compiled.BaselineOutputRow80Sha256,
            compiled.BaselineOutputModelSha256,
            compiled.BaselineDeterministicPlanSha256,
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

        Id65AuthoringRemotePadMobyChoiceAggregatePlan plan = new(
            destination,
            document,
            request.StoredDocument.EnvelopeCanonicalSha256,
            request.StoredDocument.StaticAuthoringProjectionSha256,
            request.NativeInputs.CanonicalInputSetSha256,
            compiled.SourceRow80Sha256,
            compiled.OutputRow80Sha256,
            compiled.OutputModelSha256,
            compiled.SourceT21RowSha256,
            compiled.OutputT21RowSha256,
            compiled.TargetOverlaySha256,
            compiled.GlobalExecutableSha256,
            compiled.BaselineOutputRow80Sha256,
            compiled.BaselineOutputModelSha256,
            compiled.BaselineDeterministicPlanSha256,
            compiled.OwnedRangeMapSha256,
            compiled.TransactionSha256,
            compiled.DeterministicPlanSha256,
            compiled.Placement,
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
        Id65AuthoringRemotePadMobyChoiceAggregatePlan plan,
        byte[] exactRow80,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(exactRow80);
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
                "The schema-4 aggregate transaction whole-row preimage changed.");
        }
        IReadOnlyList<Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange> ranges =
            reverse ? plan.InverseRanges : plan.OwnedRanges;
        byte[] output = ApplyAllRanges(exactRow80, ranges);
        if (Hash(output) != expectedOutputHash)
        {
            throw new InvalidDataException(
                "The schema-4 aggregate transaction whole-row result changed.");
        }
        return output;
    }

    internal static void ValidatePartitionForSmoke(
        byte[] source,
        byte[] output,
        IEnumerable<Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange> ranges,
        Id65RemotePadMobyKind kind)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(ranges);
        Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange[] owned = ranges
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
            kind);
        if (partition.GuardedByteCount + partition.ProtectedByteCount !=
            ExpectedRow80ByteLength)
        {
            throw new InvalidDataException(
                "The schema-4 aggregate partition does not cover row 80.");
        }
    }

    private static Id65AuthoringEditorDocument ValidateStoredDocument(
        Id65AuthoringEditorDocumentStoreResult stored,
        string expectedEnvelopeCanonicalSha256,
        UnusedLevel65BlankLevelLabManifest manifest)
    {
        Id65AuthoringEditorDocument reparsed =
            Id65AuthoringEditorDocument.Parse(stored.Document.SerializeUtf8());
        if (!reparsed.UsesRemotePadMobyChoice ||
            reparsed.UsesRemotePadTerrainSet ||
            reparsed.SchemaVersion !=
                Id65AuthoringEditorDocument.RemotePadMobyChoiceSchemaVersion ||
            reparsed.ProfileId !=
                Id65AuthoringEditorDocument.RemotePadMobyChoiceDocumentProfileId ||
            !reparsed.RemotePadMobyKind.HasValue ||
            reparsed.RemotePadMobyPositionId is not string positionId ||
            reparsed.MandatoryBaseFaceTextureId !=
                Id65AuthoringEditorDocument.PrivateMandatoryBaseFaceTextureId ||
            reparsed.Terrain.Count > 1)
        {
            throw new NotSupportedException(
                "The schema-4 aggregate plan supports only the exact T66 " +
                "remote-pad Moby-choice family with optional structural terrain.");
        }
        Id65AuthoringEditorDocument exact =
            Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceExact(
                reparsed.RemotePadMobyKind.Value,
                positionId,
                includeOptionalTerrain: reparsed.Terrain.Count == 1);
        string sceneGraphSha256 = reparsed.Terrain.Count == 1
            ? Id65FreshLevelSceneProjector.ExpectedStructuralGraphCanonicalSha256
            : Id65FreshLevelSceneProjector.ExpectedCanonicalSha256;
        string recomputedStaticProjectionSha256 =
            RecomputeStaticAuthoringProjectionSha256(reparsed, sceneGraphSha256);
        string recomputedEnvelopeCanonicalSha256 =
            RecomputeEnvelopeCanonicalSha256(
                manifest,
                reparsed,
                sceneGraphSha256,
                recomputedStaticProjectionSha256);
        if (!reparsed.SerializeUtf8().SequenceEqual(exact.SerializeUtf8()) ||
            stored.DocumentCanonicalSha256 != reparsed.CanonicalSha256 ||
            stored.EnvelopeSchemaVersion !=
                Id65AuthoringEditorDocumentStore.RemotePadMobyChoiceEnvelopeSchemaVersion ||
            !IsCanonicalSha256(stored.EnvelopeCanonicalSha256) ||
            !IsCanonicalSha256(expectedEnvelopeCanonicalSha256) ||
            stored.EnvelopeCanonicalSha256 != expectedEnvelopeCanonicalSha256 ||
            stored.EnvelopeCanonicalSha256 != recomputedEnvelopeCanonicalSha256 ||
            stored.ProjectionContractId !=
                Id65AuthoringEditorDocumentStore.RemotePadMobyChoiceProjectionContractId ||
            stored.CompositeProfileId.Length != 0 ||
            stored.CompositeManifestSha256.Length != 0 ||
            stored.CompositeOutputRow80Sha256.Length != 0 ||
            stored.CompositeDeterministicPlanSha256.Length != 0 ||
            stored.StaticAuthoringProfileId !=
                Id65AuthoringEditorDocumentStore.RemotePadMobyChoiceStaticAuthoringProfileId ||
            stored.StaticAuthoringCatalogSha256 !=
                Id65RemotePadMobyChoiceCatalog.CanonicalSha256 ||
            stored.StaticAuthoringSceneGraphSha256 != sceneGraphSha256 ||
            stored.StaticAuthoringProjectionSha256 !=
                recomputedStaticProjectionSha256 ||
            stored.StaticAuthoringTerrainManifestSha256.Length != 0 ||
            !stored.HasStaticAuthoringProjection ||
            stored.HasCompiledCompositeProjection ||
            stored.ContainsCompositeAfterimage ||
            stored.ContainsDiscImageBytes || stored.ContainsCueBytes ||
            stored.WritesDiscImage || stored.WritesCue || stored.AppIntegrated ||
            stored.CreateBinEnabled || stored.NormalCreateBinEnabled ||
            stored.RuntimeCandidateAuthorized || stored.ReleaseIntegrated ||
            stored.PromotionAuthorized || stored.Publishable ||
            !stored.RequiresTrustedExclusiveWorkspaceDirectory ||
            stored.HostileFilesystemRaceSafe || stored.CommonWorkspaceLeaseOwned)
        {
            throw new InvalidDataException(
                "The supplied schema-4 store result does not match the expected exact static envelope identity.");
        }
        return reparsed;
    }

    private static string RecomputeEnvelopeCanonicalSha256(
        UnusedLevel65BlankLevelLabManifest manifest,
        Id65AuthoringEditorDocument document,
        string sceneGraphSha256,
        string staticProjectionSha256)
    {
        byte[] content = WriteExactSchema4Envelope(
            manifest,
            document,
            sceneGraphSha256,
            staticProjectionSha256,
            envelopeContentSha256: null);
        string contentSha256 = Hash(content);
        return Hash(WriteExactSchema4Envelope(
            manifest,
            document,
            sceneGraphSha256,
            staticProjectionSha256,
            contentSha256));
    }

    private static byte[] WriteExactSchema4Envelope(
        UnusedLevel65BlankLevelLabManifest manifest,
        Id65AuthoringEditorDocument document,
        string sceneGraphSha256,
        string staticProjectionSha256,
        string? envelopeContentSha256)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter json = new(buffer, new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = false
        }))
        {
            json.WriteStartObject();
            json.WriteString(
                "format",
                Id65AuthoringEditorDocumentStore.EnvelopeFormat);
            json.WriteNumber(
                "schemaVersion",
                Id65AuthoringEditorDocumentStore
                    .RemotePadMobyChoiceEnvelopeSchemaVersion);
            json.WriteString("workspaceKey", manifest.WorkspaceKey);
            json.WriteNumber(
                "workspaceLayoutVersion",
                manifest.WorkspaceLayoutVersion);
            json.WriteString(
                "workspaceLayoutRelativeRoot",
                manifest.WorkspaceLayoutRelativeRoot);
            json.WriteString("labProfileId", manifest.ProfileId);
            json.WriteNumber("labProfileVersion", manifest.ProfileVersion);
            json.WriteString(
                "lockedBaseProfileId",
                manifest.LockedBaseProfileId);
            json.WriteString(
                "lockedBaseImageSha256",
                manifest.LockedBaseImageSha256);
            json.WriteString("documentProfileId", document.ProfileId);
            json.WriteString(
                "documentCanonicalSha256",
                document.CanonicalSha256);
            json.WriteString(
                "projectionContractId",
                Id65AuthoringEditorDocumentStore
                    .RemotePadMobyChoiceProjectionContractId);
            json.WriteString(
                "staticAuthoringProfileId",
                Id65AuthoringEditorDocumentStore
                    .RemotePadMobyChoiceStaticAuthoringProfileId);
            json.WriteString(
                "staticAuthoringCatalogSha256",
                Id65RemotePadMobyChoiceCatalog.CanonicalSha256);
            json.WriteString(
                "staticAuthoringSceneGraphSha256",
                sceneGraphSha256);
            json.WriteString(
                "staticAuthoringProjectionSha256",
                staticProjectionSha256);
            if (envelopeContentSha256 is not null)
            {
                json.WriteString(
                    "envelopeContentSha256",
                    envelopeContentSha256);
            }
            json.WritePropertyName("document");
            using (JsonDocument parsedDocument =
                   JsonDocument.Parse(document.SerializeUtf8()))
            {
                parsedDocument.RootElement.WriteTo(json);
            }
            json.WriteEndObject();
        }
        return buffer.ToArray();
    }

    private static string RecomputeStaticAuthoringProjectionSha256(
        Id65AuthoringEditorDocument document,
        string sceneGraphSha256)
    {
        Id65RemotePadMobyChoice choice = Id65RemotePadMobyChoiceCatalog
            .GetRequiredChoice(document.RemotePadMobyKind!.Value);
        string exactPositionId = Id65RemotePadMobyChoiceCatalog
            .GetRequiredPositionId(document.RemotePadMobyPositionId!);
        Id65RemotePadGrassT107Position position =
            Id65RemotePadGrassT107Catalog.GetRequiredPosition(exactPositionId);
        if (Id65RemotePadMobyChoiceCatalog.CanonicalSha256 !=
                Id65RemotePadMobyChoiceCatalog.ExpectedCanonicalSha256 ||
            Id65RemotePadGrassT107Catalog.SupportHandle !=
                Id65FreshLevelSceneProjector.CollisionStableId ||
            Id65RemotePadRedGemT21Catalog.SupportHandle !=
                Id65FreshLevelSceneProjector.CollisionStableId ||
            Id65RemotePadGrassT107Catalog.SupportCollisionTrueIndex !=
                Id65FreshLevelSceneProjector.CollisionTriangleIndex ||
            Id65RemotePadRedGemT21Catalog.SupportCollisionTrueIndex !=
                Id65FreshLevelSceneProjector.CollisionTriangleIndex ||
            !Id65RemotePadGrassT107Catalog.IsStrictlyInsideSupportTriangle(
                position.EditorX,
                position.EditorY,
                position.EditorZ) ||
            document.MobyPlacements.Count !=
                (choice.Kind == Id65RemotePadMobyKind.None ? 0 : 1))
        {
            throw new InvalidDataException(
                "The schema-4 document lost its exact choice catalog or T13995 support binding.");
        }

        using MemoryStream buffer = new();
        using (Utf8JsonWriter json = new(buffer, new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = false
        }))
        {
            json.WriteStartObject();
            json.WriteString(
                "profileId",
                Id65AuthoringEditorDocumentStore.RemotePadMobyChoiceStaticAuthoringProfileId);
            json.WriteNumber("schemaVersion", 2);
            json.WriteString(
                "projectionContractId",
                Id65AuthoringEditorDocumentStore.RemotePadMobyChoiceProjectionContractId);
            json.WriteString("documentProfileId", document.ProfileId);
            json.WriteString("documentCanonicalSha256", document.CanonicalSha256);
            json.WriteString("documentContentSha256", document.ContentSha256);
            json.WriteString("catalogProfileId", Id65RemotePadMobyChoiceCatalog.ProfileId);
            json.WriteString(
                "catalogCanonicalSha256",
                Id65RemotePadMobyChoiceCatalog.CanonicalSha256);
            json.WriteString("mobyKind", choice.KindId);
            json.WriteString("positionId", position.PositionId);
            string? selectedRowSha256 = Id65RemotePadMobyChoiceCatalog
                .GetSelectedExactRowSha256(choice.Kind, position.PositionId);
            if (selectedRowSha256 is null)
                json.WriteNull("selectedExactRowSha256");
            else
                json.WriteString("selectedExactRowSha256", selectedRowSha256);
            if (choice.PlacementId is null)
                json.WriteNull("placementId");
            else
                json.WriteString("placementId", choice.PlacementId);
            if (choice.TargetTrueIndex.HasValue)
                json.WriteNumber("targetTrueIndex", choice.TargetTrueIndex.Value);
            else
                json.WriteNull("targetTrueIndex");
            json.WriteString("sceneProfileId", Id65FreshLevelSceneProjector.ProfileId);
            json.WriteNumber(
                "sceneSchemaVersion",
                Id65FreshLevelSceneProjector.SchemaVersion);
            json.WriteString("sceneGraphCanonicalSha256", sceneGraphSha256);
            json.WriteString(
                "supportHandle",
                Id65FreshLevelSceneProjector.CollisionStableId);
            json.WriteNumber(
                "supportCollisionTrueIndex",
                Id65FreshLevelSceneProjector.CollisionTriangleIndex);
            json.WriteBoolean("structuralTerrainPresent", document.Terrain.Count == 1);
            json.WriteNumber(
                "mandatoryBaseFaceTextureId",
                document.MandatoryBaseFaceTextureId);
            json.WriteNumber("mobyPlacementCount", document.MobyPlacements.Count);
            json.WriteBoolean("grassAtT88Present", false);
            json.WriteBoolean("compiledAfterimagePresent", false);
            json.WriteBoolean("physicalMaterializationEnabled", false);
            json.WriteBoolean("runtimeCandidateAuthorized", false);
            json.WriteBoolean("normalCreateBinEnabled", false);
            json.WriteBoolean("releaseAuthorized", false);
            json.WriteEndObject();
        }
        return Hash(buffer.ToArray());
    }

    private static void ValidateInputIdentity(
        Id65AuthoringRemotePadMobyChoiceAggregatePlanRequest request,
        Id65AuthoringEditorDocument document)
    {
        Id65AuthoringEditorAggregateBoundSource bound =
            request.NativeInputs.BoundSource;
        Id65AuthoringCompositeLockedSource source =
            request.NativeInputs.LockedSource;
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
            document.MobyBundleSha256 != request.NativeInputs.GrassBundleSha256)
        {
            throw new InvalidDataException(
                "The schema-4 aggregate source, overlay, executable, T66 witness, or Moby bundle changed.");
        }
    }

    private static void ValidateCompiled(
        Id65AuthoringEditorDocument document,
        Id65CompiledAuthoringRemotePadMobyChoicePhysical compiled)
    {
        Id65RemotePadMobyKind kind = document.RemotePadMobyKind!.Value;
        bool terrainPresent = document.Terrain.Count == 1;
        bool grassPresent = kind == Id65RemotePadMobyKind.GrassT107;
        string positionId = document.RemotePadMobyPositionId!;
        Id65RemotePadGrassT107Position position =
            Id65RemotePadGrassT107Catalog.GetRequiredPosition(positionId);
        Id65AuthoringRemotePadCompositeStateFrozenPin baseline =
            Id65AuthoringRemotePadCompositeCompiler.GetRequiredStateFrozenPin(
                terrainPresent,
                grassPresent,
                positionId);
        int expectedOwned = ExpectedOwnedRangeCount(kind);
        int expectedAdditional = kind == Id65RemotePadMobyKind.RedGemT21 ? 1 : 0;
        string expectedOutputT21 = kind == Id65RemotePadMobyKind.RedGemT21
            ? Id65RemotePadRedGemT21Catalog.GetRequiredPosition(positionId)
                .OutputRowSha256
            : Id65RemotePadRedGemT21Catalog.SourceRowSha256;
        if (compiled.BaselineOutputRow80Sha256 != baseline.OutputRow80Sha256 ||
            compiled.BaselineOutputModelSha256 != baseline.OutputModelSha256 ||
            compiled.BaselineDeterministicPlanSha256 !=
                baseline.DeterministicPlanSha256 ||
            compiled.BaselineOwnedRangeCount != baseline.OwnedRangeCount ||
            compiled.OutputModelSha256 != baseline.OutputModelSha256)
        {
            throw new InvalidDataException(
                "The schema-4 aggregate frozen schema-3 baseline identity changed " +
                "(row=" +
                (compiled.BaselineOutputRow80Sha256 == baseline.OutputRow80Sha256) +
                ", baseline-model=" +
                (compiled.BaselineOutputModelSha256 == baseline.OutputModelSha256) +
                ", plan=" +
                (compiled.BaselineDeterministicPlanSha256 ==
                    baseline.DeterministicPlanSha256) +
                ", count=" +
                (compiled.BaselineOwnedRangeCount == baseline.OwnedRangeCount) +
                ", output-model=" +
                (compiled.OutputModelSha256 == baseline.OutputModelSha256) + ").");
        }
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
                "The schema-4 aggregate physical placement readback changed.");
        }
        if (!compiled.Document.SerializeUtf8().SequenceEqual(document.SerializeUtf8()) ||
            compiled.ProfileId !=
                Id65AuthoringRemotePadMobyChoicePhysicalCompiler.ProfileId ||
            compiled.SchemaVersion !=
                Id65AuthoringRemotePadMobyChoicePhysicalCompiler.SchemaVersion ||
            compiled.InputDocumentSchemaVersion !=
                Id65AuthoringEditorDocument.RemotePadMobyChoiceSchemaVersion ||
            compiled.DocumentCanonicalSha256 != document.CanonicalSha256 ||
            compiled.MobyKind != kind ||
            compiled.StructuralTerrainPresent != terrainPresent ||
            compiled.Schema3SurrogateGrassT107Present != grassPresent ||
            compiled.CatalogCanonicalSha256 !=
                Id65RemotePadMobyChoiceCatalog.ExpectedCanonicalSha256 ||
            compiled.SourceRow80Sha256 !=
                Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 ||
            compiled.SourceT21RowSha256 != Id65RemotePadRedGemT21Catalog.SourceRowSha256 ||
            compiled.OutputT21RowSha256 != expectedOutputT21 ||
            compiled.TargetOverlaySha256 !=
                Id65AuthoringCompositeCompiler.ExpectedTargetOverlaySha256 ||
            compiled.GlobalExecutableSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedGlobalExecutableSha256 ||
            compiled.BaselineOutputRow80Sha256 != baseline.OutputRow80Sha256 ||
            compiled.BaselineOutputModelSha256 != baseline.OutputModelSha256 ||
            compiled.BaselineDeterministicPlanSha256 !=
                baseline.DeterministicPlanSha256 ||
            compiled.BaselineOwnedRangeCount != baseline.OwnedRangeCount ||
            compiled.OutputModelSha256 != baseline.OutputModelSha256 ||
            compiled.OwnedRanges.Count != expectedOwned ||
            compiled.InverseRanges.Count != expectedOwned ||
            compiled.AdditionalOwnedRanges.Count != expectedAdditional ||
            compiled.Placement.Kind != kind ||
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
                    positionId) ||
            !compiled.PureInMemory ||
            !compiled.BuildsFromLockedSourceThroughFrozenSchema3Baseline ||
            !compiled.ExactInverseVerified ||
            !compiled.EveryChangedByteOwned ||
            !compiled.ProtectedComplementVerified ||
            !compiled.OutputModelPreservedFromFrozenSchema3Baseline ||
            !compiled.ContainsOutputRow80Bytes ||
            compiled.AcceptsCallerCompiledAfterimages ||
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
                "The schema-4 physical compiler result or authority boundary changed.");
        }
    }

    private static Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange
        ToAggregateRange(Id65AuthoringCompositeOwnedRange range)
    {
        Id65AuthoringRemotePadMobyChoiceAggregateRangeDomain domain =
            range.StableId switch
            {
                Id65AuthoringRemotePadMobyChoicePhysicalCompiler
                    .RedGemOwnedRangeStableId =>
                        Id65AuthoringRemotePadMobyChoiceAggregateRangeDomain.RedGem,
                _ when range.StableId.StartsWith(
                    "texture.page.",
                    StringComparison.Ordinal) =>
                        Id65AuthoringRemotePadMobyChoiceAggregateRangeDomain
                            .NativeTexturePages,
                "model.final" =>
                    Id65AuthoringRemotePadMobyChoiceAggregateRangeDomain.CompositeModel,
                _ when range.OwnerId == "composite.v3-spawn" =>
                    Id65AuthoringRemotePadMobyChoiceAggregateRangeDomain.Spawn,
                _ => Id65AuthoringRemotePadMobyChoiceAggregateRangeDomain.Grass
            };
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
        IReadOnlyList<Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange> owned,
        Id65RemotePadMobyKind kind)
    {
        int expectedOwned = ExpectedOwnedRangeCount(kind);
        int expectedGuarded = ExpectedGuardedByteCount(kind);
        int expectedProtected = ExpectedRow80ByteLength - expectedGuarded;
        int expectedProtectedRanges = ExpectedProtectedRangeCount(kind);
        if (source.Length != ExpectedRow80ByteLength ||
            output.Length != ExpectedRow80ByteLength ||
            owned.Count != expectedOwned ||
            HasGrassT107Range(owned) !=
                (kind == Id65RemotePadMobyKind.GrassT107) ||
            HasRedGemRange(owned) !=
                (kind == Id65RemotePadMobyKind.RedGemT21) ||
            owned.Select(item => item.StableId)
                .Distinct(StringComparer.Ordinal).Count() != owned.Count)
        {
            throw new InvalidDataException(
                "The schema-4 aggregate transaction dimensions changed.");
        }

        List<Id65AuthoringEditorAggregateProtectedRange> protectedRanges = [];
        int cursor = 0;
        int guarded = 0;
        for (int index = 0; index < owned.Count; index++)
        {
            Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange range = owned[index];
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
                !source.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(before) ||
                !output.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(after) ||
                before.SequenceEqual(after))
            {
                throw new InvalidDataException(
                    "Schema-4 aggregate range " + range.StableId +
                    " lost destination, order, hash, preimage, or afterimage identity.");
            }
            if (cursor < range.DataRelativeOffset)
            {
                int length = range.DataRelativeOffset - cursor;
                if (!source.AsSpan(cursor, length).SequenceEqual(
                        output.AsSpan(cursor, length)))
                {
                    throw new InvalidDataException(
                        "The schema-4 aggregate changed a protected gap.");
                }
                protectedRanges.Add(new(
                    cursor,
                    length,
                    Hash(source.AsSpan(cursor, length))));
            }
            cursor = checked(range.DataRelativeOffset + range.ByteLength);
            guarded = checked(guarded + range.ByteLength);
        }
        if (cursor < source.Length)
        {
            int length = source.Length - cursor;
            if (!source.AsSpan(cursor, length).SequenceEqual(
                    output.AsSpan(cursor, length)))
            {
                throw new InvalidDataException(
                    "The schema-4 aggregate changed its protected tail.");
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
                "The schema-4 aggregate owned ranges do not reproduce the compiler output.");
        }
        int protectedBytes = source.Length - guarded;
        if (guarded != expectedGuarded ||
            protectedBytes != expectedProtected ||
            protectedRanges.Count != expectedProtectedRanges)
        {
            throw new InvalidDataException(
                "The schema-4 aggregate guarded/protected partition changed.");
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
        IReadOnlyList<Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange> inverse,
        Id65RemotePadMobyKind kind)
    {
        if (inverse.Count != ExpectedOwnedRangeCount(kind) ||
            !ApplyAllRanges(output, inverse).SequenceEqual(source))
        {
            throw new InvalidDataException(
                "The schema-4 aggregate inverse transaction changed.");
        }
    }

    private static byte[] ApplyAllRanges(
        byte[] input,
        IReadOnlyList<Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange> ranges)
    {
        byte[] output = input.ToArray();
        foreach (Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange range in ranges)
        {
            byte[] before = range.CopyBefore();
            if (!output.AsSpan(range.DataRelativeOffset, range.ByteLength)
                .SequenceEqual(before))
            {
                throw new InvalidDataException(
                    "Schema-4 aggregate range " + range.StableId +
                    " lost its exact transactional preimage.");
            }
            range.CopyAfter().CopyTo(output, range.DataRelativeOffset);
        }
        return output;
    }

    private static Id65AuthoringEditorAggregateAtomicGroup[] BuildAtomicGroups(
        IReadOnlyList<Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange> owned,
        Id65RemotePadMobyKind kind)
    {
        Id65AuthoringEditorAggregateAtomicGroup Create(
            string id,
            Func<Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange, bool> predicate)
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
                Id65AuthoringRemotePadMobyChoiceAggregateRangeDomain
                    .NativeTexturePages or
                Id65AuthoringRemotePadMobyChoiceAggregateRangeDomain
                    .CompositeModel),
            Create("landing-t92", item => item.Domain ==
                Id65AuthoringRemotePadMobyChoiceAggregateRangeDomain.Spawn),
            Create("artisans-grass-remote-pad-t107", item => item.Domain ==
                Id65AuthoringRemotePadMobyChoiceAggregateRangeDomain.Grass),
            Create("resident-red-gem-remote-pad-t21", item => item.Domain ==
                Id65AuthoringRemotePadMobyChoiceAggregateRangeDomain.RedGem)
        ];
        int grassCount = kind == Id65RemotePadMobyKind.GrassT107 ? 8 : 4;
        int redGemCount = kind == Id65RemotePadMobyKind.RedGemT21 ? 1 : 0;
        if (!groups.Select(item => item.OwnedRangeCount)
                .SequenceEqual([7_950, 2, grassCount, redGemCount]) ||
            groups.Any(item =>
                !item.AllRangesRequired || item.IndependentlyApplicable))
        {
            throw new InvalidDataException(
                "The schema-4 aggregate atomic-group partition changed.");
        }
        return groups;
    }

    private static bool HasGrassT107Range(
        IReadOnlyList<Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange> owned) =>
        owned.Any(item => item.StableId == "placement.remote-pad.t107");

    private static bool HasRedGemRange(
        IReadOnlyList<Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange> owned) =>
        owned.Any(item => item.StableId ==
            Id65AuthoringRemotePadMobyChoicePhysicalCompiler.RedGemOwnedRangeStableId);

    private static int ExpectedOwnedRangeCount(Id65RemotePadMobyKind kind) =>
        kind switch
        {
            Id65RemotePadMobyKind.None => ExpectedOwnedRangeCountNone,
            Id65RemotePadMobyKind.GrassT107 => ExpectedOwnedRangeCountGrass,
            Id65RemotePadMobyKind.RedGemT21 => ExpectedOwnedRangeCountRedGem,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    private static int ExpectedGuardedByteCount(Id65RemotePadMobyKind kind) =>
        kind switch
        {
            Id65RemotePadMobyKind.None => ExpectedGuardedByteCountNone,
            Id65RemotePadMobyKind.GrassT107 => ExpectedGuardedByteCountGrass,
            Id65RemotePadMobyKind.RedGemT21 => ExpectedGuardedByteCountRedGem,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    private static int ExpectedProtectedRangeCount(Id65RemotePadMobyKind kind) =>
        kind switch
        {
            Id65RemotePadMobyKind.None => ExpectedProtectedRangeCountNone,
            Id65RemotePadMobyKind.GrassT107 => ExpectedProtectedRangeCountGrass,
            Id65RemotePadMobyKind.RedGemT21 => ExpectedProtectedRangeCountRedGem,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

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
                "The schema-4 aggregate physical destination binding changed.");
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
                "The schema-4 aggregate lab manifest is not the exact WAD80 workspace.");
        }
    }

    private static void ValidatePlan(
        Id65AuthoringRemotePadMobyChoiceAggregatePlan plan)
    {
        Id65RemotePadMobyKind kind = plan.MobyKind;
        int expectedOwned = ExpectedOwnedRangeCount(kind);
        int expectedGuarded = ExpectedGuardedByteCount(kind);
        int expectedProtectedRanges = ExpectedProtectedRangeCount(kind);
        int expectedGrass = kind == Id65RemotePadMobyKind.GrassT107 ? 8 : 4;
        int expectedRed = kind == Id65RemotePadMobyKind.RedGemT21 ? 1 : 0;
        if (plan.ProfileId != ProfileId || plan.SchemaVersion != SchemaVersion ||
            plan.LevelId != 65 || plan.DataWadEntry != 80 ||
            plan.Document.SchemaVersion !=
                Id65AuthoringEditorDocument.RemotePadMobyChoiceSchemaVersion ||
            plan.DocumentCanonicalSha256 != plan.Document.CanonicalSha256 ||
            plan.StructuralTerrainPresent != (plan.Document.Terrain.Count == 1) ||
            plan.MobyKind != plan.Document.RemotePadMobyKind ||
            plan.Placement.Kind != kind ||
            plan.CatalogCanonicalSha256 !=
                Id65RemotePadMobyChoiceCatalog.ExpectedCanonicalSha256 ||
            plan.SourceRow80Sha256 !=
                Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 ||
            plan.SourceT21RowSha256 != Id65RemotePadRedGemT21Catalog.SourceRowSha256 ||
            plan.TargetOverlaySha256 !=
                Id65AuthoringCompositeCompiler.ExpectedTargetOverlaySha256 ||
            plan.GlobalExecutableSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedGlobalExecutableSha256 ||
            plan.NativeInputSetSha256 !=
                Id65AuthoringEditorNativeInputProvider.ExpectedCanonicalInputSetSha256 ||
            !IsCanonicalSha256(plan.EnvelopeCanonicalSha256) ||
            !IsCanonicalSha256(plan.StaticAuthoringProjectionSha256) ||
            plan.OwnedRangeCount != expectedOwned ||
            plan.InverseRangeCount != expectedOwned ||
            plan.ProtectedRangeCount != expectedProtectedRanges ||
            plan.OwnedRanges.Count(item => item.Domain ==
                Id65AuthoringRemotePadMobyChoiceAggregateRangeDomain
                    .NativeTexturePages) != ExpectedTexturePageRangeCount ||
            plan.OwnedRanges.Count(item => item.Domain ==
                Id65AuthoringRemotePadMobyChoiceAggregateRangeDomain
                    .CompositeModel) != 1 ||
            plan.OwnedRanges.Count(item => item.Domain ==
                Id65AuthoringRemotePadMobyChoiceAggregateRangeDomain.Spawn) != 2 ||
            plan.OwnedRanges.Count(item => item.Domain ==
                Id65AuthoringRemotePadMobyChoiceAggregateRangeDomain.Grass) !=
                    expectedGrass ||
            plan.OwnedRanges.Count(item => item.Domain ==
                Id65AuthoringRemotePadMobyChoiceAggregateRangeDomain.RedGem) !=
                    expectedRed ||
            plan.AtomicGroups.Count != 4 ||
            plan.GuardedByteCount != expectedGuarded ||
            plan.ProtectedByteCount != ExpectedRow80ByteLength - expectedGuarded ||
            plan.GuardedByteCount + plan.ProtectedByteCount !=
                ExpectedRow80ByteLength ||
            !IsCanonicalSha256(plan.ProtectedComplementSha256) ||
            !IsCanonicalSha256(plan.OutputRow80Sha256) ||
            !IsCanonicalSha256(plan.OutputModelSha256) ||
            !IsCanonicalSha256(plan.OwnedRangeMapSha256) ||
            !IsCanonicalSha256(plan.InverseRangeMapSha256) ||
            !IsCanonicalSha256(plan.ProtectedRangeMapSha256) ||
            !IsCanonicalSha256(plan.AtomicGroupMapSha256) ||
            !IsCanonicalSha256(plan.DeterministicAggregatePlanSha256) ||
            !plan.PureInMemory ||
            !plan.FullExactSchema4DocumentRequired ||
            !plan.SuppliedStoreResultMatchesExpectedEnvelopeIdentity ||
            !plan.StaticAuthoringProjectionRecomputedFromCanonicalDocument ||
            !plan.StoreEnvelopeIdentityIndependentlyVerified ||
            plan.StaticAuthoringProjectionRecomputedFromStoreBytes ||
            !plan.NativeInputGenerationReverified ||
            !plan.LockedSourceOverlayExecutableIdentityPreserved ||
            !plan.ExactPhysicalDestinationBindingPinned ||
            !plan.PhysicalDestinationReadbackVerified ||
            !plan.T66StructuralTerrainBindingVerified ||
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
                "The schema-4 aggregate plan identity or authority boundary changed.");
        }
    }

    private static string HashOwnedRanges(
        IEnumerable<Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange> ranges) =>
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
        IReadOnlyList<Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange> ranges)
    {
        using IncrementalHash hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int cursor = 0;
        foreach (Id65AuthoringRemotePadMobyChoiceAggregateOwnedRange range in ranges)
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
