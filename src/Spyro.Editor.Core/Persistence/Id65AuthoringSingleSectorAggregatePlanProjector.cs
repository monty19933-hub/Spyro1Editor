using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Persistence;

internal sealed record Id65AuthoringSingleSectorAggregatePlanRequest(
    Id65AuthoringEditorDocumentStoreResult StoredDocument,
    string ExpectedEnvelopeCanonicalSha256,
    UnusedLevel65BlankLevelLabManifest LabManifest,
    Id65AuthoringEditorNativeInputProvider.InputSet NativeInputs,
    UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan V3Template);

internal enum Id65AuthoringSingleSectorAggregateRangeDomain
{
    NativeTexturePages,
    TerrainSetModel,
    Spawn,
    Grass,
    RedGem,
    CycloramaPalette,
    SingleSectorEnvironment,
    SingleSectorOcclusion
}

/// <summary>
/// One defensively copied leaf of the schema-7 WAD80 transaction. Leaves are
/// audit evidence for one indivisible plan; they do not authorize partial use.
/// </summary>
internal sealed class Id65AuthoringSingleSectorAggregateOwnedRange
{
    private readonly byte[] _before;
    private readonly byte[] _after;

    internal Id65AuthoringSingleSectorAggregateOwnedRange(
        string stableId,
        string ownerId,
        Id65AuthoringSingleSectorAggregateRangeDomain domain,
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
        BeforeSha256 = Id65AuthoringSingleSectorAggregatePlanProjector.Hash(
            _before);
        AfterSha256 = Id65AuthoringSingleSectorAggregatePlanProjector.Hash(
            _after);
    }

    public string StableId { get; }
    public string OwnerId { get; }
    public Id65AuthoringSingleSectorAggregateRangeDomain Domain { get; }
    public int TargetWadEntry => 80;
    public int DataRelativeOffset { get; }
    public long WadOffset => checked(
        Id65AuthoringSingleSectorAggregatePlanProjector.TargetDataWadOffset +
        DataRelativeOffset);
    public int ByteLength => _before.Length;
    public string BeforeSha256 { get; }
    public string AfterSha256 { get; }

    internal byte[] CopyBefore() => _before.ToArray();
    internal byte[] CopyAfter() => _after.ToArray();
    internal Id65AuthoringSingleSectorAggregateOwnedRange DeepCopy() => new(
        StableId,
        OwnerId,
        Domain,
        DataRelativeOffset,
        _before,
        _after);
    internal Id65AuthoringSingleSectorAggregateOwnedRange Invert() => new(
        StableId,
        OwnerId,
        Domain,
        DataRelativeOffset,
        _after,
        _before);
}

/// <summary>
/// Materializer-ready but authority-free schema-7 aggregate. It contains exact
/// source, schema-6 baseline, and output row copies plus complete forward and
/// inverse afterimage ledgers. It never writes or authorizes a candidate.
/// </summary>
internal sealed class Id65AuthoringSingleSectorAggregatePlan
{
    private readonly byte[] _sourceRow80;
    private readonly byte[] _schema6BaselineRow80;
    private readonly byte[] _outputRow80;
    private readonly ReadOnlyCollection<string> _activeTerrainUnitIds;
    private readonly ReadOnlyCollection<
        Id65AuthoringSingleSectorAggregateOwnedRange> _ownedRanges;
    private readonly ReadOnlyCollection<
        Id65AuthoringSingleSectorAggregateOwnedRange> _inverseRanges;
    private readonly ReadOnlyCollection<
        Id65AuthoringSingleSectorAggregateOwnedRange> _stageOwnedRanges;
    private readonly ReadOnlyCollection<
        Id65AuthoringSingleSectorAggregateOwnedRange> _stageInverseRanges;
    private readonly ReadOnlyCollection<Id65AuthoringEditorAggregateProtectedRange>
        _protectedRanges;
    private readonly ReadOnlyCollection<Id65AuthoringEditorAggregateAtomicGroup>
        _atomicGroups;
    private readonly ReadOnlyCollection<Id65ModelComponentRelocation>
        _componentRelocations;
    private readonly ReadOnlyCollection<Id65StableHandleRebase> _handleRebases;

    internal Id65AuthoringSingleSectorAggregatePlan(
        object? constructionAuthority,
        Id65AuthoringBlankCycloramaAggregatePlan schema6Plan,
        Id65CompiledAuthoringSingleSectorSubstrate compiled,
        byte[] sourceRow80,
        byte[] schema6BaselineRow80,
        byte[] outputRow80,
        IEnumerable<Id65AuthoringSingleSectorAggregateOwnedRange> ownedRanges,
        IEnumerable<Id65AuthoringSingleSectorAggregateOwnedRange> inverseRanges,
        IEnumerable<Id65AuthoringSingleSectorAggregateOwnedRange> stageOwnedRanges,
        IEnumerable<Id65AuthoringSingleSectorAggregateOwnedRange> stageInverseRanges,
        IEnumerable<Id65AuthoringEditorAggregateProtectedRange> protectedRanges,
        IEnumerable<Id65AuthoringEditorAggregateAtomicGroup> atomicGroups,
        int guardedByteCount,
        int protectedByteCount,
        string protectedComplementSha256,
        string stageProtectedComplementSha256)
    {
        Id65AuthoringSingleSectorAggregatePlanProjector
            .RequireConstructionAuthority(constructionAuthority);
        Destination = schema6Plan.Destination;
        Document = Id65AuthoringEditorDocument.Parse(
            schema6Plan.Document.SerializeUtf8());
        DocumentCanonicalSha256 = Document.CanonicalSha256;
        _activeTerrainUnitIds = Array.AsReadOnly(
            Document.ActiveTerrainUnitIds.ToArray());
        EnvelopeCanonicalSha256 = schema6Plan.EnvelopeCanonicalSha256;
        StaticAuthoringProfileId = schema6Plan.StaticAuthoringProfileId;
        StaticAuthoringCatalogSha256 =
            schema6Plan.StaticAuthoringCatalogSha256;
        StaticAuthoringSceneGraphSha256 =
            schema6Plan.StaticAuthoringSceneGraphSha256;
        StaticAuthoringProjectionSha256 =
            schema6Plan.StaticAuthoringProjectionSha256;
        StaticAuthoringTerrainManifestSha256 =
            schema6Plan.StaticAuthoringTerrainManifestSha256;
        StaticAuthoringCycloramaCatalogSha256 =
            schema6Plan.StaticAuthoringCycloramaCatalogSha256;
        NativeInputSetSha256 = schema6Plan.NativeInputSetSha256;
        V3DeterministicPlanSha256 = schema6Plan.V3DeterministicPlanSha256;
        Schema6AggregatePlanSha256 =
            schema6Plan.DeterministicAggregatePlanSha256;
        Schema6CompilerPlanSha256 = schema6Plan.CompilerDeterministicPlanSha256;
        _sourceRow80 = sourceRow80.ToArray();
        _schema6BaselineRow80 = schema6BaselineRow80.ToArray();
        _outputRow80 = outputRow80.ToArray();
        SourceRow80Sha256 =
            Id65AuthoringSingleSectorAggregatePlanProjector.Hash(_sourceRow80);
        Schema6BaselineRow80Sha256 =
            Id65AuthoringSingleSectorAggregatePlanProjector.Hash(
                _schema6BaselineRow80);
        OutputRow80Sha256 =
            Id65AuthoringSingleSectorAggregatePlanProjector.Hash(_outputRow80);
        Schema6BaselineModelSha256 = compiled.Schema6BaselineModelSha256;
        OutputModelSha256 = compiled.OutputModelSha256;
        OutputTextureComponentSha256 = compiled.OutputTextureComponentSha256;
        OutputCycloramaSha256 = compiled.OutputCycloramaSha256;
        CollisionSha256 = compiled.CollisionSha256;
        Environment = compiled.Environment with { };
        Occlusion = compiled.Occlusion with { };
        CompilerOwnedRangeMapSha256 = compiled.OwnedRangeMapSha256;
        CompilerTransactionSha256 = compiled.TransactionSha256;
        CompilerProtectedComplementSha256 =
            compiled.ProtectedComplementSha256;
        CompilerStageOwnedRangeMapSha256 = compiled.StageOwnedRangeMapSha256;
        CompilerStageTransactionSha256 = compiled.StageTransactionSha256;
        CompilerStageProtectedComplementSha256 =
            compiled.StageProtectedComplementSha256;
        CompilerComponentRelocationMapSha256 =
            compiled.ComponentRelocationMapSha256;
        CompilerHandleRebaseMapSha256 = compiled.HandleRebaseMapSha256;
        CompilerDeterministicPlanSha256 = compiled.DeterministicPlanSha256;
        _ownedRanges = Array.AsReadOnly(
            ownedRanges.Select(item => item.DeepCopy()).ToArray());
        _inverseRanges = Array.AsReadOnly(
            inverseRanges.Select(item => item.DeepCopy()).ToArray());
        _stageOwnedRanges = Array.AsReadOnly(
            stageOwnedRanges.Select(item => item.DeepCopy()).ToArray());
        _stageInverseRanges = Array.AsReadOnly(
            stageInverseRanges.Select(item => item.DeepCopy()).ToArray());
        _protectedRanges = Array.AsReadOnly(protectedRanges.ToArray());
        _atomicGroups = Array.AsReadOnly(atomicGroups.ToArray());
        _componentRelocations = Array.AsReadOnly(
            compiled.ComponentRelocations.Select(item => item with { }).ToArray());
        _handleRebases = Array.AsReadOnly(
            compiled.HandleRebases.Select(item => item with { }).ToArray());
        GuardedByteCount = guardedByteCount;
        ProtectedByteCount = protectedByteCount;
        ChangedByteCount = compiled.ChangedByteCount;
        StageChangedByteCount = compiled.StageChangedByteCount;
        ProtectedComplementSha256 = protectedComplementSha256;
        StageProtectedComplementSha256 = stageProtectedComplementSha256;
        OwnedRangeMapSha256 =
            Id65AuthoringSingleSectorAggregatePlanProjector.HashOwnedRanges(
                _ownedRanges);
        InverseRangeMapSha256 =
            Id65AuthoringSingleSectorAggregatePlanProjector.HashOwnedRanges(
                _inverseRanges);
        StageOwnedRangeMapSha256 =
            Id65AuthoringSingleSectorAggregatePlanProjector.HashOwnedRanges(
                _stageOwnedRanges);
        StageInverseRangeMapSha256 =
            Id65AuthoringSingleSectorAggregatePlanProjector.HashOwnedRanges(
                _stageInverseRanges);
        ProtectedRangeMapSha256 =
            Id65AuthoringSingleSectorAggregatePlanProjector.HashProtectedRanges(
                _protectedRanges);
        AtomicGroupMapSha256 =
            Id65AuthoringSingleSectorAggregatePlanProjector.HashAtomicGroups(
                _atomicGroups);
        ComponentRelocationMapSha256 =
            CompilerComponentRelocationMapSha256;
        HandleRebaseMapSha256 =
            Id65AuthoringSingleSectorAggregatePlanProjector.HashHandleRebases(
                _handleRebases);
        DeterministicAggregatePlanSha256 =
            Id65AuthoringSingleSectorAggregatePlanProjector
                .ComputeDeterministicPlanSha256(this);
    }

    public string ProfileId =>
        Id65AuthoringSingleSectorAggregatePlanProjector.ProfileId;
    public int SchemaVersion =>
        Id65AuthoringSingleSectorAggregatePlanProjector.SchemaVersion;
    public int LevelId => 65;
    public int DataWadEntry => 80;
    public Id65AuthoringBlankCycloramaAggregateDestinationBinding Destination { get; }
    public Id65AuthoringEditorDocument Document { get; }
    public string DocumentCanonicalSha256 { get; }
    public IReadOnlyList<string> ActiveTerrainUnitIds => _activeTerrainUnitIds;
    public int TerrainUnitCount => _activeTerrainUnitIds.Count;
    public Id65RemotePadMobyKind MobyKind => Document.RemotePadMobyKind!.Value;
    public string MobyPositionId => Document.RemotePadMobyPositionId!;
    public string EnvelopeCanonicalSha256 { get; }
    public string StaticAuthoringProfileId { get; }
    public string StaticAuthoringCatalogSha256 { get; }
    public string StaticAuthoringSceneGraphSha256 { get; }
    public string StaticAuthoringProjectionSha256 { get; }
    public string StaticAuthoringTerrainManifestSha256 { get; }
    public string StaticAuthoringCycloramaCatalogSha256 { get; }
    public string NativeInputSetSha256 { get; }
    public string V3DeterministicPlanSha256 { get; }
    public string Schema6AggregatePlanSha256 { get; }
    public string Schema6CompilerPlanSha256 { get; }
    public string SourceRow80Sha256 { get; }
    public string Schema6BaselineRow80Sha256 { get; }
    public string OutputRow80Sha256 { get; }
    public string Schema6BaselineModelSha256 { get; }
    public string OutputModelSha256 { get; }
    public string OutputTextureComponentSha256 { get; }
    public string OutputCycloramaSha256 { get; }
    public string CollisionSha256 { get; }
    public Id65AuthoringSingleSectorEnvironmentReadback Environment { get; }
    public Id65AuthoringSingleSectorOcclusionReadback Occlusion { get; }
    public IReadOnlyList<Id65AuthoringSingleSectorAggregateOwnedRange>
        OwnedRanges => _ownedRanges;
    public IReadOnlyList<Id65AuthoringSingleSectorAggregateOwnedRange>
        InverseRanges => _inverseRanges;
    public IReadOnlyList<Id65AuthoringSingleSectorAggregateOwnedRange>
        StageOwnedRanges => _stageOwnedRanges;
    public IReadOnlyList<Id65AuthoringSingleSectorAggregateOwnedRange>
        StageInverseRanges => _stageInverseRanges;
    public IReadOnlyList<Id65AuthoringEditorAggregateProtectedRange>
        ProtectedRanges => _protectedRanges;
    public IReadOnlyList<Id65AuthoringEditorAggregateAtomicGroup>
        AtomicGroups => _atomicGroups;
    public IReadOnlyList<Id65ModelComponentRelocation> ComponentRelocations =>
        _componentRelocations;
    public IReadOnlyList<Id65StableHandleRebase> HandleRebases => _handleRebases;
    public int OwnedRangeCount => _ownedRanges.Count;
    public int InverseRangeCount => _inverseRanges.Count;
    public int StageOwnedRangeCount => _stageOwnedRanges.Count;
    public int StageInverseRangeCount => _stageInverseRanges.Count;
    public int ProtectedRangeCount => _protectedRanges.Count;
    public int GuardedByteCount { get; }
    public int ProtectedByteCount { get; }
    public int ChangedByteCount { get; }
    public int StageChangedByteCount { get; }
    public string CompilerOwnedRangeMapSha256 { get; }
    public string CompilerTransactionSha256 { get; }
    public string CompilerProtectedComplementSha256 { get; }
    public string CompilerStageOwnedRangeMapSha256 { get; }
    public string CompilerStageTransactionSha256 { get; }
    public string CompilerStageProtectedComplementSha256 { get; }
    public string CompilerComponentRelocationMapSha256 { get; }
    public string CompilerHandleRebaseMapSha256 { get; }
    public string CompilerDeterministicPlanSha256 { get; }
    public string OwnedRangeMapSha256 { get; }
    public string InverseRangeMapSha256 { get; }
    public string StageOwnedRangeMapSha256 { get; }
    public string StageInverseRangeMapSha256 { get; }
    public string ProtectedRangeMapSha256 { get; }
    public string ProtectedComplementSha256 { get; }
    public string StageProtectedComplementSha256 { get; }
    public string AtomicGroupMapSha256 { get; }
    public string ComponentRelocationMapSha256 { get; }
    public string HandleRebaseMapSha256 { get; }
    public string DeterministicAggregatePlanSha256 { get; }

    public bool PureInMemory => true;
    public bool FullExactSchema6DocumentRequired => true;
    public bool Schema6P2AggregateBaselineRecomputed => true;
    public bool ProducesSchema7SingleSectorSubstrate => true;
    public bool CompleteLockedSourceTransactionCarried => true;
    public bool ExactSchema6StageDeltaCarried => true;
    public bool ContainsSourceRow80Bytes => true;
    public bool ContainsSchema6BaselineRow80Bytes => true;
    public bool ContainsOutputRow80Bytes => true;
    public bool CompleteRowOwnershipPartitionVerified => true;
    public bool ExactWholeRowInverseVerified => true;
    public bool ExactSchema6StageInverseVerified => true;
    public bool OneAtomicTransactionRequired => true;
    public bool PartialRangeApplicationAuthorized => false;
    public bool Wad80SoleWritableDestination => true;
    public bool CollisionPreservedExactly => true;
    public bool InheritedCollisionLookupsRetired => false;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool WritesBin => false;
    public bool WritesCue => false;
    public bool MaterializerIntegrated => false;
    public bool CreateBinEnabled => false;
    public bool NormalCreateBinEnabled => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool RuntimeProven => false;
    public bool ReleaseIntegrated => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;

    internal byte[] CopySourceRow80() => _sourceRow80.ToArray();
    internal byte[] CopySchema6BaselineRow80() =>
        _schema6BaselineRow80.ToArray();
    internal byte[] CopyOutputRow80() => _outputRow80.ToArray();
}

/// <summary>
/// Revalidates the exact schema-6 stored aggregate, compiles the schema-7
/// single-sector substrate from locked inputs, and projects both ledgers into
/// one immutable WAD80-only transaction. There is no filesystem authority.
/// </summary>
internal static class Id65AuthoringSingleSectorAggregatePlanProjector
{
    public const string ProfileId =
        "id65-authoring-single-sector-substrate-aggregate-plan-schema7-in-memory-v1";
    public const int SchemaVersion = 1;
    public const int ExpectedStateCount = 108;
    public const int ExpectedRow80ByteLength = 0x2E2000;
    public const long TargetDataWadOffset = 0x6936800;

    private const string TerrainModelOwnerId =
        "model.remote-pad.schema5-terrain-set";
    private static readonly object PlanConstructionAuthority = new();

    public static Id65AuthoringSingleSectorAggregatePlan Project(
        Id65AuthoringSingleSectorAggregatePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.StoredDocument);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            request.ExpectedEnvelopeCanonicalSha256);
        ArgumentNullException.ThrowIfNull(request.LabManifest);
        ArgumentNullException.ThrowIfNull(request.NativeInputs);
        ArgumentNullException.ThrowIfNull(request.V3Template);

        Id65AuthoringBlankCycloramaAggregatePlan schema6Plan =
            Id65AuthoringBlankCycloramaAggregatePlanProjector.Project(new(
                request.StoredDocument,
                request.ExpectedEnvelopeCanonicalSha256,
                request.LabManifest,
                request.NativeInputs,
                request.V3Template));

        Id65CompiledAuthoringSingleSectorSubstrate compiled =
            Id65AuthoringSingleSectorSubstrateCompiler.Compile(new(
                schema6Plan.Document,
                request.NativeInputs.LockedSource,
                request.V3Template,
                request.NativeInputs.PrivateTextureWitness,
                request.NativeInputs.GrassBundle));
        ValidateCompiled(schema6Plan, compiled);

        byte[] source = compiled.CopySourceRow80();
        byte[] schema6Baseline = compiled.CopySchema6BaselineRow80();
        byte[] output = compiled.CopyOutputRow80();
        Id65AuthoringSingleSectorAggregateOwnedRange[] owned =
            compiled.OwnedRanges.Select(ToAggregateRange).ToArray();
        Id65AuthoringSingleSectorAggregateOwnedRange[] inverse = owned
            .Reverse().Select(item => item.Invert()).ToArray();
        Id65AuthoringSingleSectorAggregateOwnedRange[] stageOwned =
            compiled.StageOwnedRanges.Select(ToAggregateRange).ToArray();
        Id65AuthoringSingleSectorAggregateOwnedRange[] stageInverse = stageOwned
            .Reverse().Select(item => item.Invert()).ToArray();

        ValidateCompilerLedgers(compiled, source, schema6Baseline, output);
        TransactionPartition partition = ValidateAndBuildPartition(
            source,
            output,
            owned,
            "locked-source aggregate");
        TransactionPartition stagePartition = ValidateAndBuildPartition(
            schema6Baseline,
            output,
            stageOwned,
            "schema-6 stage aggregate");
        ValidateInverse(source, output, inverse, "locked-source aggregate");
        ValidateInverse(
            schema6Baseline,
            output,
            stageInverse,
            "schema-6 stage aggregate");
        RequireRangeSequenceEquivalent(compiled.InverseRanges, inverse);
        RequireRangeSequenceEquivalent(compiled.StageInverseRanges, stageInverse);

        if (partition.ProtectedComplementSha256 !=
                compiled.ProtectedComplementSha256 ||
            stagePartition.ProtectedComplementSha256 !=
                compiled.StageProtectedComplementSha256 ||
            partition.ChangedByteCount != compiled.ChangedByteCount ||
            stagePartition.ChangedByteCount != compiled.StageChangedByteCount)
        {
            throw new InvalidDataException(
                "The schema-7 aggregate partition diverged from the compiler ledger.");
        }

        Id65AuthoringEditorAggregateAtomicGroup[] atomicGroups =
            BuildAtomicGroups(owned);
        Id65AuthoringSingleSectorAggregatePlan plan = new(
            PlanConstructionAuthority,
            schema6Plan,
            compiled,
            source,
            schema6Baseline,
            output,
            owned,
            inverse,
            stageOwned,
            stageInverse,
            partition.ProtectedRanges,
            atomicGroups,
            partition.GuardedByteCount,
            partition.ProtectedByteCount,
            partition.ProtectedComplementSha256,
            stagePartition.ProtectedComplementSha256);
        ValidatePlan(plan);
        return plan;
    }

    internal static void RequireConstructionAuthority(object? authority)
    {
        if (!ReferenceEquals(authority, PlanConstructionAuthority))
        {
            throw new InvalidDataException(
                "Only the schema-7 single-sector aggregate projector may construct a plan.");
        }
    }

    internal static byte[] ApplyExact(
        Id65AuthoringSingleSectorAggregatePlan plan,
        byte[] exactRow80,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(exactRow80);
        ValidatePlan(plan);
        return ApplyExactCore(
            exactRow80,
            reverse ? plan.InverseRanges : plan.OwnedRanges,
            reverse ? plan.OutputRow80Sha256 : plan.SourceRow80Sha256,
            reverse ? plan.SourceRow80Sha256 : plan.OutputRow80Sha256,
            "schema-7 aggregate");
    }

    internal static byte[] ApplyStageExact(
        Id65AuthoringSingleSectorAggregatePlan plan,
        byte[] exactRow80,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(exactRow80);
        ValidatePlan(plan);
        return ApplyExactCore(
            exactRow80,
            reverse ? plan.StageInverseRanges : plan.StageOwnedRanges,
            reverse ? plan.OutputRow80Sha256 :
                plan.Schema6BaselineRow80Sha256,
            reverse ? plan.Schema6BaselineRow80Sha256 :
                plan.OutputRow80Sha256,
            "schema-7 stage aggregate");
    }

    private static void ValidateCompiled(
        Id65AuthoringBlankCycloramaAggregatePlan schema6Plan,
        Id65CompiledAuthoringSingleSectorSubstrate compiled)
    {
        byte[] expectedSchema6 =
            Id65AuthoringBlankCycloramaAggregatePlanProjector.ApplyExact(
                schema6Plan,
                compiled.CopySourceRow80(),
                reverse: false);
        byte[] compiledSchema6 = compiled.CopySchema6BaselineRow80();
        if (!compiled.Document.SerializeUtf8().SequenceEqual(
                schema6Plan.Document.SerializeUtf8()) ||
            compiled.ProfileId !=
                Id65AuthoringSingleSectorSubstrateCompiler.ProfileId ||
            compiled.SchemaVersion !=
                Id65AuthoringSingleSectorSubstrateCompiler.SchemaVersion ||
            compiled.InputDocumentSchemaVersion !=
                Id65AuthoringEditorDocument
                    .RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion ||
            compiled.DocumentCanonicalSha256 !=
                schema6Plan.DocumentCanonicalSha256 ||
            !compiled.ActiveTerrainUnitIds.SequenceEqual(
                schema6Plan.ActiveTerrainUnitIds) ||
            compiled.MobyKind != schema6Plan.MobyKind ||
            compiled.MobyPositionId != schema6Plan.MobyPositionId ||
            compiled.SourceRow80Sha256 != schema6Plan.SourceRow80Sha256 ||
            compiled.Schema6BaselineRow80Sha256 !=
                schema6Plan.OutputRow80Sha256 ||
            !compiledSchema6.SequenceEqual(expectedSchema6) ||
            compiled.Schema6BaselineModelSha256 !=
                schema6Plan.OutputModelSha256 ||
            compiled.Schema6DeterministicPlanSha256 !=
                schema6Plan.CompilerDeterministicPlanSha256 ||
            !string.Equals(
                compiled.OutputCycloramaSha256,
                schema6Plan.OutputCycloramaSha256,
                StringComparison.OrdinalIgnoreCase) ||
            compiled.Environment.ModelOffset !=
                Id65AuthoringSingleSectorSubstrateCompiler
                    .EnvironmentModelOffset ||
            compiled.Environment.Row80Offset !=
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset +
                compiled.Environment.ModelOffset ||
            compiled.Environment.WadOffset !=
                TargetDataWadOffset + compiled.Environment.Row80Offset ||
            compiled.Environment.LogicalSourceSectorIndex !=
                Id65AuthoringSingleSectorSubstrateCompiler
                    .LogicalSourceSectorIndex ||
            compiled.Environment.PhysicalOutputSectorIndex !=
                Id65AuthoringSingleSectorSubstrateCompiler
                    .PhysicalOutputSectorIndex ||
            compiled.Environment.PointerGapByteLength !=
                Id65AuthoringSingleSectorSubstrateCompiler
                    .EnvironmentZeroGapByteLength ||
            compiled.Environment.ZeroGapSha256 !=
                Id65AuthoringSingleSectorSubstrateCompiler
                    .ExpectedZeroGapSha256 ||
            compiled.Occlusion.WadOffset !=
                TargetDataWadOffset + compiled.Occlusion.Row80Offset ||
            compiled.Occlusion.GroupCount != 16 ||
            compiled.Occlusion.OwnerGroupIndex != 0 ||
            compiled.Occlusion.LogicalSourceSectorIndex !=
                Id65AuthoringSingleSectorSubstrateCompiler
                    .LogicalSourceSectorIndex ||
            compiled.Occlusion.PhysicalOutputSectorIndex !=
                Id65AuthoringSingleSectorSubstrateCompiler
                    .PhysicalOutputSectorIndex ||
            compiled.Occlusion.Schema6OcclusionSha256 !=
                Id65AuthoringSingleSectorSubstrateCompiler
                    .ExpectedSchema6OcclusionSha256 ||
            compiled.Occlusion.OutputOcclusionSha256 !=
                Id65AuthoringSingleSectorSubstrateCompiler
                    .ExpectedOutputOcclusionSha256 ||
            compiled.Occlusion.OpaqueTailSha256 !=
                Id65AuthoringBlankCycloramaCompiler
                    .ExpectedVisibilityOpaqueTailSha256 ||
            compiled.OwnedRanges.Count == 0 ||
            compiled.InverseRanges.Count != compiled.OwnedRanges.Count ||
            compiled.StageOwnedRanges.Count == 0 ||
            compiled.StageInverseRanges.Count !=
                compiled.StageOwnedRanges.Count ||
            compiled.ComponentRelocations.Count == 0 ||
            compiled.HandleRebases.Count == 0 ||
            !CanonicalHashes(
                compiled.OutputRow80Sha256,
                compiled.OutputModelSha256,
                compiled.OutputTextureComponentSha256,
                compiled.CollisionSha256,
                compiled.OwnedRangeMapSha256,
                compiled.TransactionSha256,
                compiled.ProtectedComplementSha256,
                compiled.StageOwnedRangeMapSha256,
                compiled.StageTransactionSha256,
                compiled.StageProtectedComplementSha256,
                compiled.ComponentRelocationMapSha256,
                compiled.HandleRebaseMapSha256,
                compiled.DeterministicPlanSha256) ||
            !IsSha256(compiled.OutputCycloramaSha256) ||
            compiled.ChangedByteCount <= 0 ||
            compiled.StageChangedByteCount <= 0 ||
            !compiled.PureInMemory ||
            !compiled.ExactSchema6DocumentRequired ||
            !compiled.BuildsExactSchema6P2BaselineFromLockedInputs ||
            compiled.CallerAfterimagesAccepted ||
            !compiled.ProducesSchema7SingleSectorSubstrate ||
            !compiled.FixedComponentCapacitiesPreserved ||
            !compiled.DownstreamComponentOffsetsPreserved ||
            !compiled.LogicalSector216RebasedToPhysicalSector0 ||
            !compiled.EnvironmentSectorCountIsOne ||
            !compiled.EnvironmentPointerGapZeroed ||
            !compiled.AuthoredOnlyOcclusionRebasedToSector0 ||
            !compiled.InheritedEnvironmentBytesRetired ||
            compiled.InheritedCollisionLookupsRetired ||
            !compiled.CollisionPreservedExactly ||
            !compiled.NativeT66TexturePreserved ||
            !compiled.MandatoryBaseFaceT66Preserved ||
            !compiled.TerrainSectorBytesPreservedExactly ||
            !compiled.CycloramaPreservedExactly ||
            !compiled.SpawnAndMobyBytesPreservedExactly ||
            !compiled.ExactInverseVerified ||
            !compiled.ExactSchema6StageInverseVerified ||
            !compiled.EveryChangedByteOwned ||
            !compiled.ProtectedComplementVerified ||
            !compiled.ContainsOutputRow80Bytes ||
            compiled.WritesFileSystem || compiled.WritesDiscImage ||
            compiled.WritesCue || compiled.MaterializerIntegrated ||
            compiled.CreateBinEnabled || compiled.RuntimeCandidateAuthorized ||
            compiled.RuntimeProven || compiled.ReleaseIntegrated ||
            compiled.PromotionAuthorized || compiled.Publishable)
        {
            throw new InvalidDataException(
                "The single-sector compiler result or authority boundary changed.");
        }
    }

    private static void ValidateCompilerLedgers(
        Id65CompiledAuthoringSingleSectorSubstrate compiled,
        byte[] source,
        byte[] schema6Baseline,
        byte[] output)
    {
        if (HashCompilerOwnedRanges(compiled.OwnedRanges) !=
                compiled.OwnedRangeMapSha256 ||
            HashCompilerTransaction(compiled.OwnedRanges) !=
                compiled.TransactionSha256 ||
            HashProtectedBytes(source, compiled.OwnedRanges) !=
                compiled.ProtectedComplementSha256 ||
            HashCompilerOwnedRanges(compiled.StageOwnedRanges) !=
                compiled.StageOwnedRangeMapSha256 ||
            HashCompilerTransaction(compiled.StageOwnedRanges) !=
                compiled.StageTransactionSha256 ||
            HashProtectedBytes(schema6Baseline, compiled.StageOwnedRanges) !=
                compiled.StageProtectedComplementSha256 ||
            HashHandleRebases(compiled.HandleRebases) !=
                compiled.HandleRebaseMapSha256 ||
            Hash(source) != compiled.SourceRow80Sha256 ||
            Hash(schema6Baseline) != compiled.Schema6BaselineRow80Sha256 ||
            Hash(output) != compiled.OutputRow80Sha256 ||
            CountChangedBytes(source, output) != compiled.ChangedByteCount ||
            CountChangedBytes(schema6Baseline, output) !=
                compiled.StageChangedByteCount)
        {
            throw new InvalidDataException(
                "The schema-7 compiler ledger hashes did not survive aggregate projection.");
        }
    }

    private static Id65AuthoringSingleSectorAggregateOwnedRange ToAggregateRange(
        Id65AuthoringCompositeOwnedRange range)
    {
        long end = range.DataRelativeOffset + (long)range.ByteLength;
        int textureStart =
            Id65V2NativeTextureCompositionCompiler
                .TexturePagesDataRelativeOffset;
        int textureEnd = checked(textureStart +
            Id65V2NativeTextureCompositionCompiler.TexturePagesByteLength);
        Id65AuthoringSingleSectorAggregateRangeDomain domain;
        if (range.OwnerId ==
            Id65AuthoringSingleSectorSubstrateCompiler.EnvironmentOwnerId)
        {
            domain = Id65AuthoringSingleSectorAggregateRangeDomain
                .SingleSectorEnvironment;
        }
        else if (range.OwnerId ==
            Id65AuthoringSingleSectorSubstrateCompiler.OcclusionOwnerId)
        {
            domain = Id65AuthoringSingleSectorAggregateRangeDomain
                .SingleSectorOcclusion;
        }
        else if (range.OwnerId == Id65AuthoringBlankCycloramaCompiler.PaletteOwnerId)
        {
            domain = Id65AuthoringSingleSectorAggregateRangeDomain
                .CycloramaPalette;
        }
        else if (range.DataRelativeOffset >= textureStart && end <= textureEnd)
        {
            domain = Id65AuthoringSingleSectorAggregateRangeDomain
                .NativeTexturePages;
        }
        else if (range.OwnerId == TerrainModelOwnerId)
        {
            domain = Id65AuthoringSingleSectorAggregateRangeDomain
                .TerrainSetModel;
        }
        else if (range.OwnerId == "composite.v3-spawn")
        {
            domain = Id65AuthoringSingleSectorAggregateRangeDomain.Spawn;
        }
        else if (range.OwnerId == Id65RemotePadRedGemT21Catalog.PlacementId)
        {
            domain = Id65AuthoringSingleSectorAggregateRangeDomain.RedGem;
        }
        else if (range.OwnerId is
                     Id65AuthoringMobyDependencyBundleCompiler
                         .ArtisansGrassBundleId or
                     Id65RemotePadGrassT107Catalog.PlacementId or
                     "allocation.rows" or "allocation.fixups")
        {
            domain = Id65AuthoringSingleSectorAggregateRangeDomain.Grass;
        }
        else
        {
            throw new InvalidDataException(
                "Schema-7 range " + range.StableId +
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
        IReadOnlyList<Id65AuthoringSingleSectorAggregateOwnedRange> ranges,
        string label)
    {
        if (source.Length != ExpectedRow80ByteLength ||
            output.Length != ExpectedRow80ByteLength ||
            ranges.Count == 0 ||
            ranges.Select(item => item.StableId)
                .Distinct(StringComparer.Ordinal).Count() != ranges.Count)
        {
            throw new InvalidDataException(
                "The " + label + " is empty, ambiguous, or not a full WAD80 row.");
        }
        List<Id65AuthoringEditorAggregateProtectedRange> protectedRanges = [];
        int cursor = 0;
        int guarded = 0;
        int changed = 0;
        foreach (Id65AuthoringSingleSectorAggregateOwnedRange range in ranges)
        {
            if (range.DataRelativeOffset < cursor || range.ByteLength <= 0 ||
                range.DataRelativeOffset + (long)range.ByteLength > source.Length ||
                range.CopyBefore().Length != range.ByteLength ||
                range.CopyAfter().Length != range.ByteLength ||
                Hash(range.CopyBefore()) != range.BeforeSha256 ||
                Hash(range.CopyAfter()) != range.AfterSha256 ||
                !source.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(range.CopyBefore()) ||
                !output.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(range.CopyAfter()) ||
                range.CopyBefore().SequenceEqual(range.CopyAfter()))
            {
                throw new InvalidDataException(
                    "A " + label + " range lost bounds, ordering, preimage, or change ownership.");
            }
            if (cursor < range.DataRelativeOffset)
            {
                int length = range.DataRelativeOffset - cursor;
                if (!source.AsSpan(cursor, length)
                    .SequenceEqual(output.AsSpan(cursor, length)))
                {
                    throw new InvalidDataException(
                        "The " + label + " changed an unowned protected byte.");
                }
                protectedRanges.Add(new(
                    cursor,
                    length,
                    Hash(source.AsSpan(cursor, length))));
            }
            guarded = checked(guarded + range.ByteLength);
            changed = checked(changed + CountChangedBytes(
                range.CopyBefore(),
                range.CopyAfter()));
            cursor = checked(range.DataRelativeOffset + range.ByteLength);
        }
        if (cursor < source.Length)
        {
            int length = source.Length - cursor;
            if (!source.AsSpan(cursor, length)
                .SequenceEqual(output.AsSpan(cursor, length)))
            {
                throw new InvalidDataException(
                    "The " + label + " changed its final protected bytes.");
            }
            protectedRanges.Add(new(
                cursor,
                length,
                Hash(source.AsSpan(cursor, length))));
        }
        if (changed != CountChangedBytes(source, output))
            throw new InvalidDataException(
                "The " + label + " does not own every changed byte.");
        int protectedCount = source.Length - guarded;
        if (protectedRanges.Sum(item => item.ByteLength) != protectedCount)
            throw new InvalidDataException(
                "The " + label + " ownership partition does not cover WAD80.");
        return new(
            Array.AsReadOnly(protectedRanges.ToArray()),
            guarded,
            protectedCount,
            changed,
            HashProtectedBytes(source, ranges));
    }

    private static void ValidateInverse(
        byte[] source,
        byte[] output,
        IReadOnlyList<Id65AuthoringSingleSectorAggregateOwnedRange> inverse,
        string label)
    {
        byte[] restored = ApplyAllRanges(output, inverse);
        if (!restored.SequenceEqual(source))
            throw new InvalidDataException(
                "The " + label + " inverse did not restore its exact preimage.");
    }

    private static void RequireRangeSequenceEquivalent(
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> compilerRanges,
        IReadOnlyList<Id65AuthoringSingleSectorAggregateOwnedRange> ranges)
    {
        if (compilerRanges.Count != ranges.Count)
            throw new InvalidDataException(
                "The compiler and aggregate inverse range counts differ.");
        for (int index = 0; index < ranges.Count; index++)
        {
            Id65AuthoringCompositeOwnedRange compiler = compilerRanges[index];
            Id65AuthoringSingleSectorAggregateOwnedRange aggregate = ranges[index];
            if (compiler.StableId != aggregate.StableId ||
                compiler.OwnerId != aggregate.OwnerId ||
                compiler.DataRelativeOffset != aggregate.DataRelativeOffset ||
                compiler.ByteLength != aggregate.ByteLength ||
                compiler.BeforeSha256 != aggregate.BeforeSha256 ||
                compiler.AfterSha256 != aggregate.AfterSha256 ||
                !compiler.CopyBefore().SequenceEqual(aggregate.CopyBefore()) ||
                !compiler.CopyAfter().SequenceEqual(aggregate.CopyAfter()))
            {
                throw new InvalidDataException(
                    "A compiler inverse range changed during aggregate projection.");
            }
        }
    }

    private static Id65AuthoringEditorAggregateAtomicGroup[] BuildAtomicGroups(
        IReadOnlyList<Id65AuthoringSingleSectorAggregateOwnedRange> ranges)
    {
        string stableIdSetSha256 = Hash(Encoding.UTF8.GetBytes(string.Join(
            '\n',
            ranges.Select(item => item.StableId)
                .Order(StringComparer.Ordinal))));
        return
        [
            new(
                "schema7.single-sector-substrate.wad80",
                ranges.Count,
                stableIdSetSha256,
                AllRangesRequired: true,
                IndependentlyApplicable: false)
        ];
    }

    private static byte[] ApplyExactCore(
        byte[] input,
        IReadOnlyList<Id65AuthoringSingleSectorAggregateOwnedRange> ranges,
        string expectedInputSha256,
        string expectedOutputSha256,
        string label)
    {
        if (input.Length != ExpectedRow80ByteLength ||
            Hash(input) != expectedInputSha256)
        {
            throw new InvalidDataException(
                "The " + label + " whole-row preimage changed.");
        }
        byte[] output = ApplyAllRanges(input, ranges);
        if (Hash(output) != expectedOutputSha256)
            throw new InvalidDataException(
                "The " + label + " whole-row output changed.");
        return output;
    }

    private static byte[] ApplyAllRanges(
        byte[] input,
        IReadOnlyList<Id65AuthoringSingleSectorAggregateOwnedRange> ranges)
    {
        byte[] output = input.ToArray();
        foreach (Id65AuthoringSingleSectorAggregateOwnedRange range in ranges)
        {
            if (!output.AsSpan(range.DataRelativeOffset, range.ByteLength)
                .SequenceEqual(range.CopyBefore()))
            {
                throw new InvalidDataException(
                    "Schema-7 aggregate range " + range.StableId +
                    " lost its exact preimage.");
            }
            range.CopyAfter().CopyTo(output, range.DataRelativeOffset);
        }
        return output;
    }

    private static void ValidatePlan(
        Id65AuthoringSingleSectorAggregatePlan plan)
    {
        byte[] source = plan.CopySourceRow80();
        byte[] schema6Baseline = plan.CopySchema6BaselineRow80();
        byte[] output = plan.CopyOutputRow80();
        TransactionPartition partition = ValidateAndBuildPartition(
            source,
            output,
            plan.OwnedRanges,
            "retained locked-source aggregate");
        TransactionPartition stagePartition = ValidateAndBuildPartition(
            schema6Baseline,
            output,
            plan.StageOwnedRanges,
            "retained schema-6 stage aggregate");
        if (plan.ProfileId != ProfileId || plan.SchemaVersion != SchemaVersion ||
            plan.LevelId != 65 || plan.DataWadEntry != 80 ||
            plan.DocumentCanonicalSha256 != plan.Document.CanonicalSha256 ||
            plan.Document.SchemaVersion !=
                Id65AuthoringEditorDocument
                    .RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion ||
            plan.SourceRow80Sha256 != Hash(source) ||
            plan.Schema6BaselineRow80Sha256 != Hash(schema6Baseline) ||
            plan.OutputRow80Sha256 != Hash(output) ||
            plan.OwnedRangeCount != plan.InverseRangeCount ||
            plan.StageOwnedRangeCount != plan.StageInverseRangeCount ||
            plan.OwnedRangeMapSha256 != HashOwnedRanges(plan.OwnedRanges) ||
            plan.InverseRangeMapSha256 != HashOwnedRanges(plan.InverseRanges) ||
            plan.StageOwnedRangeMapSha256 !=
                HashOwnedRanges(plan.StageOwnedRanges) ||
            plan.StageInverseRangeMapSha256 !=
                HashOwnedRanges(plan.StageInverseRanges) ||
            plan.ProtectedRangeMapSha256 !=
                HashProtectedRanges(plan.ProtectedRanges) ||
            plan.AtomicGroupMapSha256 != HashAtomicGroups(plan.AtomicGroups) ||
            plan.HandleRebaseMapSha256 !=
                HashHandleRebases(plan.HandleRebases) ||
            plan.HandleRebaseMapSha256 != plan.CompilerHandleRebaseMapSha256 ||
            plan.GuardedByteCount != partition.GuardedByteCount ||
            plan.ProtectedByteCount != partition.ProtectedByteCount ||
            plan.ChangedByteCount != partition.ChangedByteCount ||
            plan.StageChangedByteCount != stagePartition.ChangedByteCount ||
            plan.ProtectedComplementSha256 !=
                partition.ProtectedComplementSha256 ||
            plan.StageProtectedComplementSha256 !=
                stagePartition.ProtectedComplementSha256 ||
            plan.CompilerProtectedComplementSha256 !=
                plan.ProtectedComplementSha256 ||
            plan.CompilerStageProtectedComplementSha256 !=
                plan.StageProtectedComplementSha256 ||
            plan.OwnedRanges.Count(item => item.Domain ==
                Id65AuthoringSingleSectorAggregateRangeDomain
                    .SingleSectorEnvironment) == 0 ||
            plan.OwnedRanges.Count(item => item.Domain ==
                Id65AuthoringSingleSectorAggregateRangeDomain
                    .SingleSectorOcclusion) == 0 ||
            plan.StageOwnedRanges.Any(item => item.Domain is not
                Id65AuthoringSingleSectorAggregateRangeDomain
                    .SingleSectorEnvironment and not
                Id65AuthoringSingleSectorAggregateRangeDomain
                    .SingleSectorOcclusion) ||
            plan.AtomicGroups.Count != 1 ||
            plan.AtomicGroups[0].OwnedRangeCount != plan.OwnedRangeCount ||
            !CanonicalHashes(
                plan.EnvelopeCanonicalSha256,
                plan.StaticAuthoringProjectionSha256,
                plan.NativeInputSetSha256,
                plan.V3DeterministicPlanSha256,
                plan.Schema6AggregatePlanSha256,
                plan.Schema6CompilerPlanSha256,
                plan.OutputModelSha256,
                plan.CollisionSha256,
                plan.CompilerOwnedRangeMapSha256,
                plan.CompilerTransactionSha256,
                plan.CompilerStageOwnedRangeMapSha256,
                plan.CompilerStageTransactionSha256,
                plan.CompilerDeterministicPlanSha256,
                plan.OwnedRangeMapSha256,
                plan.InverseRangeMapSha256,
                plan.StageOwnedRangeMapSha256,
                plan.StageInverseRangeMapSha256,
                plan.ProtectedRangeMapSha256,
                plan.AtomicGroupMapSha256,
                plan.DeterministicAggregatePlanSha256) ||
            plan.DeterministicAggregatePlanSha256 !=
                ComputeDeterministicPlanSha256(plan) ||
            !plan.PureInMemory || !plan.ProducesSchema7SingleSectorSubstrate ||
            !plan.CompleteLockedSourceTransactionCarried ||
            !plan.ExactSchema6StageDeltaCarried ||
            !plan.ContainsSourceRow80Bytes ||
            !plan.ContainsSchema6BaselineRow80Bytes ||
            !plan.ContainsOutputRow80Bytes ||
            plan.PartialRangeApplicationAuthorized ||
            plan.InheritedCollisionLookupsRetired ||
            !plan.CollisionPreservedExactly || plan.WritesFileSystem ||
            plan.WritesDiscImage || plan.WritesBin || plan.WritesCue ||
            plan.MaterializerIntegrated || plan.CreateBinEnabled ||
            plan.RuntimeCandidateAuthorized || plan.RuntimeProven ||
            plan.ReleaseIntegrated || plan.PromotionAuthorized ||
            plan.Publishable)
        {
            throw new InvalidDataException(
                "The retained schema-7 aggregate plan changed identity or authority.");
        }
        ValidateInverse(source, output, plan.InverseRanges,
            "retained locked-source aggregate");
        ValidateInverse(schema6Baseline, output, plan.StageInverseRanges,
            "retained schema-6 stage aggregate");
    }

    internal static string HashOwnedRanges(
        IEnumerable<Id65AuthoringSingleSectorAggregateOwnedRange> ranges) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', ranges.Select(item =>
            $"{item.StableId}|{item.OwnerId}|{item.Domain}|" +
            $"{item.DataRelativeOffset:X8}|{item.ByteLength:X8}|" +
            $"{item.BeforeSha256}|{item.AfterSha256}"))));

    internal static string HashProtectedRanges(
        IEnumerable<Id65AuthoringEditorAggregateProtectedRange> ranges) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', ranges.Select(item =>
            $"{item.DataRelativeOffset:X8}|{item.ByteLength:X8}|" +
            item.SegmentSha256))));

    internal static string HashAtomicGroups(
        IEnumerable<Id65AuthoringEditorAggregateAtomicGroup> groups) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', groups.Select(item =>
            $"{item.GroupId}|{item.OwnedRangeCount}|{item.StableIdSetSha256}|" +
            $"{item.AllRangesRequired}|{item.IndependentlyApplicable}"))));

    internal static string HashHandleRebases(
        IEnumerable<Id65StableHandleRebase> values)
    {
        StringBuilder text = new();
        foreach (Id65StableHandleRebase item in values.OrderBy(
                     item => item.StableId,
                     StringComparer.Ordinal))
        {
            text.Append(item.StableId).Append('|')
                .Append(item.Kind).Append('|')
                .Append(Address(item.Source)).Append('|')
                .Append(Address(item.Output)).Append('\n');
        }
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static string HashCompilerOwnedRanges(
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges)
    {
        StringBuilder canonical = new();
        foreach (Id65AuthoringCompositeOwnedRange range in ranges)
        {
            canonical.Append(range.StableId).Append('|')
                .Append(range.OwnerId).Append('|')
                .Append(range.DataRelativeOffset.ToString(
                    CultureInfo.InvariantCulture)).Append('|')
                .Append(range.ByteLength.ToString(
                    CultureInfo.InvariantCulture)).Append('|')
                .Append(range.BeforeSha256).Append('|')
                .Append(range.AfterSha256).Append('\n');
        }
        return Hash(Encoding.UTF8.GetBytes(canonical.ToString()));
    }

    private static string HashCompilerTransaction(
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges)
    {
        using IncrementalHash hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (Id65AuthoringCompositeOwnedRange range in ranges)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(string.Join('|',
            [
                range.StableId,
                range.OwnerId,
                range.DataRelativeOffset.ToString(CultureInfo.InvariantCulture),
                range.ByteLength.ToString(CultureInfo.InvariantCulture)
            ]) + "\n"));
            hash.AppendData(range.CopyBefore());
            hash.AppendData(range.CopyAfter());
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static string HashProtectedBytes(
        byte[] bytes,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges)
    {
        using IncrementalHash hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int cursor = 0;
        foreach (Id65AuthoringCompositeOwnedRange range in ranges)
        {
            if (range.DataRelativeOffset > cursor)
            {
                hash.AppendData(bytes.AsSpan(
                    cursor,
                    range.DataRelativeOffset - cursor));
            }
            cursor = range.DataRelativeOffset + range.ByteLength;
        }
        if (cursor < bytes.Length) hash.AppendData(bytes.AsSpan(cursor));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static string HashProtectedBytes(
        byte[] bytes,
        IReadOnlyList<Id65AuthoringSingleSectorAggregateOwnedRange> ranges)
    {
        using IncrementalHash hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int cursor = 0;
        foreach (Id65AuthoringSingleSectorAggregateOwnedRange range in ranges)
        {
            if (range.DataRelativeOffset > cursor)
            {
                hash.AppendData(bytes.AsSpan(
                    cursor,
                    range.DataRelativeOffset - cursor));
            }
            cursor = range.DataRelativeOffset + range.ByteLength;
        }
        if (cursor < bytes.Length) hash.AppendData(bytes.AsSpan(cursor));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    internal static string ComputeDeterministicPlanSha256(
        Id65AuthoringSingleSectorAggregatePlan plan) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n',
        [
            ProfileId,
            SchemaVersion.ToString(CultureInfo.InvariantCulture),
            plan.DocumentCanonicalSha256,
            plan.EnvelopeCanonicalSha256,
            plan.StaticAuthoringProjectionSha256,
            plan.NativeInputSetSha256,
            plan.V3DeterministicPlanSha256,
            plan.Schema6AggregatePlanSha256,
            plan.Schema6CompilerPlanSha256,
            plan.SourceRow80Sha256,
            plan.Schema6BaselineRow80Sha256,
            plan.OutputRow80Sha256,
            plan.Schema6BaselineModelSha256,
            plan.OutputModelSha256,
            plan.Environment.OutputEnvironmentSha256,
            plan.Environment.RetainedSectorSha256,
            plan.Environment.ZeroGapSha256,
            plan.Occlusion.OutputOcclusionSha256,
            plan.CollisionSha256,
            plan.ChangedByteCount.ToString(CultureInfo.InvariantCulture),
            plan.StageChangedByteCount.ToString(CultureInfo.InvariantCulture),
            plan.CompilerOwnedRangeMapSha256,
            plan.CompilerTransactionSha256,
            plan.CompilerProtectedComplementSha256,
            plan.CompilerStageOwnedRangeMapSha256,
            plan.CompilerStageTransactionSha256,
            plan.CompilerStageProtectedComplementSha256,
            plan.CompilerComponentRelocationMapSha256,
            plan.CompilerHandleRebaseMapSha256,
            plan.CompilerDeterministicPlanSha256,
            plan.OwnedRangeMapSha256,
            plan.InverseRangeMapSha256,
            plan.StageOwnedRangeMapSha256,
            plan.StageInverseRangeMapSha256,
            plan.ProtectedRangeMapSha256,
            plan.ProtectedComplementSha256,
            plan.StageProtectedComplementSha256,
            plan.AtomicGroupMapSha256,
            plan.HandleRebaseMapSha256,
            plan.Destination.TargetDataWadOffset.ToString(
                "X", CultureInfo.InvariantCulture),
            plan.Destination.TargetDataDirectoryRowSha256,
            plan.Destination.ProtectedExecutableSha256
        ])));

    private static int CountChangedBytes(
        ReadOnlySpan<byte> before,
        ReadOnlySpan<byte> after)
    {
        if (before.Length != after.Length)
            throw new InvalidDataException(
                "Schema-7 aggregate changed-byte inputs differ in length.");
        int changed = 0;
        for (int index = 0; index < before.Length; index++)
        {
            if (before[index] != after[index]) changed++;
        }
        return changed;
    }

    private static string Address(Id65LogicalAddress? address) =>
        address.HasValue
            ? string.Create(CultureInfo.InvariantCulture,
                $"{address.Value.Space}:{address.Value.Primary}:{address.Value.Secondary}")
            : "none";

    private static bool CanonicalHashes(params string[] values) =>
        values.All(IsCanonicalSha256);

    private static bool IsCanonicalSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f' or
                >= 'A' and <= 'F');

    internal static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private sealed record TransactionPartition(
        IReadOnlyList<Id65AuthoringEditorAggregateProtectedRange> ProtectedRanges,
        int GuardedByteCount,
        int ProtectedByteCount,
        int ChangedByteCount,
        string ProtectedComplementSha256);
}
