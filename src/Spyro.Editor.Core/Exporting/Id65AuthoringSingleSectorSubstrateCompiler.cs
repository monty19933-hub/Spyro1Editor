using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

internal sealed record Id65AuthoringSingleSectorSubstrateCompileRequest(
    Id65AuthoringEditorDocument Document,
    Id65AuthoringCompositeLockedSource Source,
    UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan V3Template,
    Id65V2NativeTextureWitness TextureWitness,
    Id65MobyDependencyBundleDescriptor GrassBundle,
    Id65AuthoringTerrainSetLimits? TerrainLimits = null,
    Id65AuthoringCompositeCompilerLimits? CompositeLimits = null);

internal sealed record Id65AuthoringSingleSectorEnvironmentReadback(
    int ModelOffset,
    int Row80Offset,
    long WadOffset,
    int ComponentByteLength,
    int LogicalSourceSectorIndex,
    int PhysicalOutputSectorIndex,
    int SourceSectorRelativeOffset,
    int OutputSectorRelativeOffset,
    int SectorByteLength,
    int PointerGapByteLength,
    int LowDetailVertexCount,
    int LowDetailColorCount,
    int LowDetailFaceCount,
    int HighDetailVertexCount,
    int HighDetailColorCount,
    int HighDetailFaceCount,
    string BaselineEnvironmentSha256,
    string OutputEnvironmentSha256,
    string RetainedSectorSha256,
    string ZeroGapSha256);

internal sealed record Id65AuthoringSingleSectorOcclusionReadback(
    int ModelOffset,
    int Row80Offset,
    long WadOffset,
    int ComponentByteLength,
    int GroupCount,
    int OwnerGroupIndex,
    int LogicalSourceSectorIndex,
    int PhysicalOutputSectorIndex,
    int ChangedByteCount,
    string Schema6OcclusionSha256,
    string OutputOcclusionSha256,
    string OpaqueTailSha256);

internal sealed class Id65CompiledAuthoringSingleSectorSubstrate
{
    private readonly byte[] _sourceRow80;
    private readonly byte[] _schema6BaselineRow80;
    private readonly byte[] _outputRow80;
    private readonly ReadOnlyCollection<string> _activeTerrainUnitIds;
    private readonly ReadOnlyCollection<Id65AuthoringCompositeOwnedRange>
        _ownedRanges;
    private readonly ReadOnlyCollection<Id65AuthoringCompositeOwnedRange>
        _inverseRanges;
    private readonly ReadOnlyCollection<Id65AuthoringCompositeOwnedRange>
        _stageOwnedRanges;
    private readonly ReadOnlyCollection<Id65AuthoringCompositeOwnedRange>
        _stageInverseRanges;
    private readonly ReadOnlyCollection<Id65ModelComponentRelocation>
        _componentRelocations;
    private readonly ReadOnlyCollection<Id65StableHandleRebase>
        _handleRebases;

    internal Id65CompiledAuthoringSingleSectorSubstrate(
        Id65AuthoringEditorDocument document,
        byte[] sourceRow80,
        byte[] schema6BaselineRow80,
        byte[] outputRow80,
        IEnumerable<Id65AuthoringCompositeOwnedRange> ownedRanges,
        IEnumerable<Id65AuthoringCompositeOwnedRange> inverseRanges,
        IEnumerable<Id65AuthoringCompositeOwnedRange> stageOwnedRanges,
        IEnumerable<Id65AuthoringCompositeOwnedRange> stageInverseRanges,
        IEnumerable<Id65ModelComponentRelocation> componentRelocations,
        IEnumerable<Id65StableHandleRebase> handleRebases,
        Id65AuthoringSingleSectorEnvironmentReadback environment,
        Id65AuthoringSingleSectorOcclusionReadback occlusion,
        string schema6DeterministicPlanSha256,
        string outputTextureComponentSha256,
        string outputCycloramaSha256,
        string collisionSha256,
        int changedByteCount,
        int stageChangedByteCount,
        string ownedRangeMapSha256,
        string transactionSha256,
        string protectedComplementSha256,
        string stageOwnedRangeMapSha256,
        string stageTransactionSha256,
        string stageProtectedComplementSha256,
        string componentRelocationMapSha256,
        string handleRebaseMapSha256,
        string deterministicPlanSha256)
    {
        Document = Id65AuthoringEditorDocument.Parse(document.SerializeUtf8());
        DocumentCanonicalSha256 = Document.CanonicalSha256;
        _activeTerrainUnitIds = Array.AsReadOnly(
            Document.ActiveTerrainUnitIds.ToArray());
        _sourceRow80 = sourceRow80.ToArray();
        _schema6BaselineRow80 = schema6BaselineRow80.ToArray();
        _outputRow80 = outputRow80.ToArray();
        _ownedRanges = Array.AsReadOnly(
            ownedRanges.Select(item => item.DeepCopy()).ToArray());
        _inverseRanges = Array.AsReadOnly(
            inverseRanges.Select(item => item.DeepCopy()).ToArray());
        _stageOwnedRanges = Array.AsReadOnly(
            stageOwnedRanges.Select(item => item.DeepCopy()).ToArray());
        _stageInverseRanges = Array.AsReadOnly(
            stageInverseRanges.Select(item => item.DeepCopy()).ToArray());
        _componentRelocations = Array.AsReadOnly(
            componentRelocations.Select(item => item with { }).ToArray());
        _handleRebases = Array.AsReadOnly(
            handleRebases.Select(item => item with { }).ToArray());
        Environment = environment with { };
        Occlusion = occlusion with { };
        SourceRow80Sha256 =
            Id65AuthoringSingleSectorSubstrateCompiler.Hash(_sourceRow80);
        Schema6BaselineRow80Sha256 =
            Id65AuthoringSingleSectorSubstrateCompiler.Hash(
                _schema6BaselineRow80);
        OutputRow80Sha256 =
            Id65AuthoringSingleSectorSubstrateCompiler.Hash(_outputRow80);
        Schema6BaselineModelSha256 =
            Id65AuthoringSingleSectorSubstrateCompiler.Hash(
                _schema6BaselineRow80.AsSpan(
                    Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset,
                    Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelLength));
        OutputModelSha256 =
            Id65AuthoringSingleSectorSubstrateCompiler.Hash(
                _outputRow80.AsSpan(
                    Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset,
                    Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelLength));
        Schema6DeterministicPlanSha256 = schema6DeterministicPlanSha256;
        OutputTextureComponentSha256 = outputTextureComponentSha256;
        OutputCycloramaSha256 = outputCycloramaSha256;
        CollisionSha256 = collisionSha256;
        ChangedByteCount = changedByteCount;
        StageChangedByteCount = stageChangedByteCount;
        OwnedRangeMapSha256 = ownedRangeMapSha256;
        TransactionSha256 = transactionSha256;
        ProtectedComplementSha256 = protectedComplementSha256;
        StageOwnedRangeMapSha256 = stageOwnedRangeMapSha256;
        StageTransactionSha256 = stageTransactionSha256;
        StageProtectedComplementSha256 = stageProtectedComplementSha256;
        ComponentRelocationMapSha256 = componentRelocationMapSha256;
        HandleRebaseMapSha256 = handleRebaseMapSha256;
        DeterministicPlanSha256 = deterministicPlanSha256;
    }

    public string ProfileId =>
        Id65AuthoringSingleSectorSubstrateCompiler.ProfileId;
    public int SchemaVersion =>
        Id65AuthoringSingleSectorSubstrateCompiler.SchemaVersion;
    public string Schema6ProfileId =>
        Id65AuthoringBlankCycloramaCompiler.ProfileId;
    public int InputDocumentSchemaVersion => Document.SchemaVersion;
    public Id65AuthoringEditorDocument Document { get; }
    public string DocumentCanonicalSha256 { get; }
    public IReadOnlyList<string> ActiveTerrainUnitIds =>
        _activeTerrainUnitIds;
    public Id65RemotePadMobyKind MobyKind =>
        Document.RemotePadMobyKind ?? throw new InvalidDataException(
            "The single-sector result lost its schema-6 Moby kind.");
    public string MobyPositionId =>
        Document.RemotePadMobyPositionId ?? throw new InvalidDataException(
            "The single-sector result lost its schema-6 Moby position.");
    public Id65AuthoringSingleSectorEnvironmentReadback Environment { get; }
    public Id65AuthoringSingleSectorOcclusionReadback Occlusion { get; }
    public IReadOnlyList<Id65AuthoringCompositeOwnedRange> OwnedRanges =>
        _ownedRanges;
    public IReadOnlyList<Id65AuthoringCompositeOwnedRange> InverseRanges =>
        _inverseRanges;
    public IReadOnlyList<Id65AuthoringCompositeOwnedRange> StageOwnedRanges =>
        _stageOwnedRanges;
    public IReadOnlyList<Id65AuthoringCompositeOwnedRange> StageInverseRanges =>
        _stageInverseRanges;
    public IReadOnlyList<Id65ModelComponentRelocation> ComponentRelocations =>
        _componentRelocations;
    public IReadOnlyList<Id65StableHandleRebase> HandleRebases =>
        _handleRebases;
    public string SourceRow80Sha256 { get; }
    public string Schema6BaselineRow80Sha256 { get; }
    public string OutputRow80Sha256 { get; }
    public string Schema6BaselineModelSha256 { get; }
    public string OutputModelSha256 { get; }
    public string Schema6DeterministicPlanSha256 { get; }
    public string OutputTextureComponentSha256 { get; }
    public string OutputCycloramaSha256 { get; }
    public string CollisionSha256 { get; }
    public int ChangedByteCount { get; }
    public int StageChangedByteCount { get; }
    public string OwnedRangeMapSha256 { get; }
    public string TransactionSha256 { get; }
    public string ProtectedComplementSha256 { get; }
    public string StageOwnedRangeMapSha256 { get; }
    public string StageTransactionSha256 { get; }
    public string StageProtectedComplementSha256 { get; }
    public string ComponentRelocationMapSha256 { get; }
    public string HandleRebaseMapSha256 { get; }
    public string DeterministicPlanSha256 { get; }
    public long Row80WadOffset =>
        Id65AuthoringBlankCycloramaCompiler.TargetDataWadOffset;
    public int Row80ByteLength => _outputRow80.Length;
    public int ModelRow80Offset =>
        Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset;
    public int ModelByteLength =>
        Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelLength;

    public bool PureInMemory => true;
    public bool ExactSchema6DocumentRequired => true;
    public bool BuildsExactSchema6P2BaselineFromLockedInputs => true;
    public bool AcceptsExactSchema6P2CompiledInput => true;
    public bool CallerSchema6AfterimageAccepted => false;
    public bool CallerAfterimagesAccepted => false;
    public bool ProducesSchema7SingleSectorSubstrate => true;
    public bool FixedComponentCapacitiesPreserved => true;
    public bool DownstreamComponentOffsetsPreserved => true;
    public bool LogicalSector216RebasedToPhysicalSector0 => true;
    public bool EnvironmentSectorCountIsOne => true;
    public bool EnvironmentPointerGapZeroed => true;
    public bool AuthoredOnlyOcclusionRebasedToSector0 => true;
    public bool InheritedEnvironmentBytesRetired => true;
    public bool InheritedCollisionLookupsRetired => false;
    public bool CollisionPreservedExactly => true;
    public bool NativeT66TexturePreserved => true;
    public bool MandatoryBaseFaceT66Preserved => true;
    public bool TerrainSectorBytesPreservedExactly => true;
    public bool CycloramaPreservedExactly => true;
    public bool SpawnAndMobyBytesPreservedExactly => true;
    public bool ExactInverseVerified => true;
    public bool ExactSchema6StageInverseVerified => true;
    public bool EveryChangedByteOwned => true;
    public bool ProtectedComplementVerified => true;
    public bool ContainsOutputRow80Bytes => true;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool WritesCue => false;
    public bool AppIntegrated => false;
    public bool UiIntegrated => false;
    public bool DocumentPersistenceIntegrated => false;
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
    internal byte[] CopySchema6BaselineModel() =>
        _schema6BaselineRow80.AsSpan(
            Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset,
            Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelLength)
        .ToArray();
    internal byte[] CopyOutputModel() => _outputRow80.AsSpan(
        Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset,
        Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelLength)
        .ToArray();
    internal byte[] CopySchema6BaselineEnvironment() =>
        _schema6BaselineRow80.AsSpan(
            Environment.Row80Offset,
            Environment.ComponentByteLength).ToArray();
    internal byte[] CopyOutputEnvironment() => _outputRow80.AsSpan(
        Environment.Row80Offset,
        Environment.ComponentByteLength).ToArray();
    internal byte[] CopySchema6BaselineOcclusion() =>
        _schema6BaselineRow80.AsSpan(
            Occlusion.Row80Offset,
            Occlusion.ComponentByteLength).ToArray();
    internal byte[] CopyOutputOcclusion() => _outputRow80.AsSpan(
        Occlusion.Row80Offset,
        Occlusion.ComponentByteLength).ToArray();
}

/// <summary>
/// Pure in-memory schema-7 substrate stage. It always rebuilds the exact
/// schema-6 p2 blank-cyclorama/visibility row from locked inputs, then changes
/// only the fixed-capacity environment and environment-occlusion components.
/// The exact authored sector is copied from logical sector 216 to physical
/// sector 0; the remaining environment capacity is zeroed. Collision and all
/// other bytes remain byte-exact schema-6 output.
/// </summary>
internal static class Id65AuthoringSingleSectorSubstrateCompiler
{
    public const string ProfileId =
        "id65-authoring-schema7-single-sector-substrate-in-memory-p1";
    public const int SchemaVersion = 1;
    public const int ExpectedStateCount = 108;
    public const string EnvironmentOwnerId =
        "schema7.environment.single-sector";
    public const string OcclusionOwnerId =
        "schema7.occlusion.sector-zero";
    public const int LogicalSourceSectorIndex = 216;
    public const int PhysicalOutputSectorIndex = 0;
    public const int EnvironmentModelOffset = 0x3030;
    public const int SourceSectorRelativeOffset = 0x284A8;
    public const int SourceSectorPointerValue = 0x284A4;
    public const int OutputSectorRelativeOffset = 0x0C;
    public const int OutputSectorPointerValue = 8;
    public const int EnvironmentZeroGapByteLength = 0x2849C;
    public const string ExpectedZeroGapSha256 =
        "ef5a381bc5e5f798a040ed170c007ae1005d863e5ad00bc91df77469797ceb10";
    public const string ExpectedSchema6OcclusionSha256 =
        Id65AuthoringBlankCycloramaCompiler.ExpectedOutputVisibilityOcclusionSha256;
    public const string ExpectedOutputOcclusionSha256 =
        "be8c21196b0c22c8439264d934c407bd5eafd07e17ffce061e659a184e3a950d";
    public const int OcclusionSectorByteOffset = 0x4C;

    private const int EmptyEnvironmentLength = 0x28518;
    private const int BaseSectorLength = 0x70;
    private const int PerTerrainEnvironmentGrowth = 0x20;
    private const int T66TextureComponentLength =
        Id65V2NativeTextureCompositionCompiler.OutputTextureComponentBytes;
    private const int ModelLength =
        Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelLength;
    private const int Row80Length =
        Id65AuthoringRemotePadTerrainSetPhysicalCompiler.Row80ByteLength;

    public static Id65CompiledAuthoringSingleSectorSubstrate Compile(
        Id65AuthoringSingleSectorSubstrateCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Document);
        ArgumentNullException.ThrowIfNull(request.Source);
        ArgumentNullException.ThrowIfNull(request.V3Template);
        ArgumentNullException.ThrowIfNull(request.TextureWitness);
        ArgumentNullException.ThrowIfNull(request.GrassBundle);
        if (request.Document.SchemaVersion !=
                Id65AuthoringEditorDocument
                    .RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion ||
            !request.Document.UsesBlankCyclorama)
        {
            throw new InvalidDataException(
                "The single-sector substrate requires the exact schema-6 blank document family.");
        }

        Id65CompiledAuthoringBlankCyclorama schema6 =
            Id65AuthoringBlankCycloramaCompiler.Compile(new(
                request.Document,
                request.Source,
                request.V3Template,
                request.TextureWitness,
                request.GrassBundle,
                request.TerrainLimits,
                request.CompositeLimits));

        Id65AuthoringTerrainSetManifest manifest =
            Id65AuthoringTerrainSetCatalog.Create(
                request.Document.ActiveTerrainUnitIds);
        Id65CompiledAuthoringTerrainSet terrain =
            Id65AuthoringTerrainSetCompiler.CompileTarget(
                manifest,
                request.V3Template,
                request.TerrainLimits);
        return CompileFromSchema6(schema6, terrain);
    }

    /// <summary>
    /// Applies only the schema-7 substrate delta to an already compiled exact
    /// schema-6 p2 row. The terrain semantic provider supplies stable logical
    /// handles only; no caller-provided afterimage is accepted.
    /// </summary>
    public static Id65CompiledAuthoringSingleSectorSubstrate CompileFromSchema6(
        Id65CompiledAuthoringBlankCyclorama schema6,
        Id65CompiledAuthoringTerrainSet terrain)
    {
        ArgumentNullException.ThrowIfNull(schema6);
        ArgumentNullException.ThrowIfNull(terrain);
        ValidateSchema6Baseline(schema6);
        Id65AuthoringEditorDocument document = schema6.Document;
        Id65AuthoringTerrainSetManifest manifest =
            Id65AuthoringTerrainSetCatalog.Create(
                document.ActiveTerrainUnitIds);
        if (terrain.ManifestSha256 != manifest.CanonicalSha256 ||
            !terrain.Units.Select(item => item.UnitId)
                .SequenceEqual(document.ActiveTerrainUnitIds) ||
            !terrain.ExactHpLpRowsVerified ||
            !terrain.NegativeCollisionWindingVerified ||
            !terrain.CollisionCellTreeReadbackVerified ||
            !terrain.SingleOcclusionGroupOwnershipVerified ||
            !terrain.StableIdsAndRebaseMapVerified ||
            terrain.WritesFileSystem || terrain.WritesDiscImage ||
            terrain.CreateBinEnabled || terrain.RuntimeCandidateAuthorized ||
            terrain.ReleaseIntegrated || terrain.Publishable)
        {
            throw new InvalidDataException(
                "The single-sector stage lost its exact terrain semantic provider.");
        }

        byte[] sourceRow80 = schema6.CopySourceRow80();
        byte[] schema6Row80 = schema6.CopyOutputRow80();
        byte[] outputRow80 = schema6Row80.ToArray();
        int activeCount = document.ActiveTerrainUnitIds.Count;
        int expectedEnvironmentLength = checked(
            EmptyEnvironmentLength +
            activeCount * PerTerrainEnvironmentGrowth);
        int expectedSectorLength = checked(
            BaseSectorLength +
            activeCount * PerTerrainEnvironmentGrowth);
        int environmentRow80Offset = checked(
            Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset +
            EnvironmentModelOffset);
        byte[] baselineEnvironment = schema6Row80.AsSpan(
            environmentRow80Offset,
            expectedEnvironmentLength).ToArray();
        (byte[] outputEnvironment,
            Id65AuthoringSingleSectorEnvironmentReadback environmentReadback) =
            BuildSingleSectorEnvironment(
                baselineEnvironment,
                environmentRow80Offset,
                expectedSectorLength);
        outputEnvironment.CopyTo(outputRow80, environmentRow80Offset);

        int occlusionModelOffset = schema6.VisibilityOcclusionModelOffset;
        int occlusionRow80Offset = schema6.VisibilityOcclusionRow80Offset;
        byte[] baselineOcclusion = schema6Row80.AsSpan(
            occlusionRow80Offset,
            Id65AuthoringBlankCycloramaCompiler
                .ExpectedVisibilityOcclusionByteLength).ToArray();
        (byte[] outputOcclusion,
            Id65AuthoringSingleSectorOcclusionReadback occlusionReadback) =
            BuildSectorZeroOcclusion(
                baselineOcclusion,
                occlusionModelOffset,
                occlusionRow80Offset);
        outputOcclusion.CopyTo(outputRow80, occlusionRow80Offset);

        VerifyOnlyStageComponentsChanged(
            schema6Row80,
            outputRow80,
            environmentRow80Offset,
            expectedEnvironmentLength,
            occlusionRow80Offset,
            outputOcclusion.Length);
        byte[] schema6Model = schema6Row80.AsSpan(
            Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset,
            ModelLength).ToArray();
        byte[] outputModel = outputRow80.AsSpan(
            Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset,
            ModelLength).ToArray();
        ComponentLayout[] schema6Components = ParseModelComponents(
            schema6Model,
            activeCount,
            expectedEnvironmentLength,
            schema6.CycloramaModelOffset);
        ComponentLayout[] outputComponents = ParseModelComponents(
            outputModel,
            activeCount,
            expectedEnvironmentLength,
            schema6.CycloramaModelOffset);
        Id65ModelComponentRelocation[] relocations = BuildRelocations(
            schema6Components,
            outputComponents);
        ValidatePreservedComponents(
            schema6,
            schema6Row80,
            outputRow80,
            schema6Components,
            outputComponents,
            environmentReadback,
            occlusionReadback);

        Id65StableHandleRebase[] handleRebases = BuildHandleRebases(
            terrain.CopyAddresses());
        ValidateHandleRebases(handleRebases);
        string handleRebaseMapSha256 = HashHandleRebases(handleRebases);
        string componentRelocationMapSha256 = HashRelocations(relocations);

        Id65AuthoringCompositeOwnedRange[] stageRanges = BuildStageLedger(
            schema6Row80,
            outputRow80,
            environmentRow80Offset,
            expectedEnvironmentLength,
            occlusionRow80Offset,
            outputOcclusion.Length);
        Id65AuthoringCompositeOwnedRange[] stageInverse =
            InvertRanges(stageRanges);
        ValidateLedger(schema6Row80, outputRow80, stageRanges, "schema-7 stage");
        Id65AuthoringCompositeOwnedRange[] directRanges = BuildDirectLedger(
            sourceRow80,
            outputRow80,
            schema6.OwnedRanges,
            environmentRow80Offset,
            expectedEnvironmentLength,
            occlusionRow80Offset,
            outputOcclusion.Length);
        Id65AuthoringCompositeOwnedRange[] directInverse =
            InvertRanges(directRanges);
        ValidateLedger(sourceRow80, outputRow80, directRanges, "schema-7 direct");

        int changedByteCount = CountChangedBytes(sourceRow80, outputRow80);
        int stageChangedByteCount = CountChangedBytes(schema6Row80, outputRow80);
        string ownedRangeMapSha256 = HashOwnedRanges(directRanges);
        string transactionSha256 = HashTransaction(directRanges);
        string protectedComplementSha256 = HashProtectedComplement(
            sourceRow80,
            directRanges);
        RequireHash(
            HashProtectedComplement(outputRow80, directRanges),
            protectedComplementSha256,
            "schema-7 direct protected complement");
        string stageOwnedRangeMapSha256 = HashOwnedRanges(stageRanges);
        string stageTransactionSha256 = HashTransaction(stageRanges);
        string stageProtectedComplementSha256 = HashProtectedComplement(
            schema6Row80,
            stageRanges);
        RequireHash(
            HashProtectedComplement(outputRow80, stageRanges),
            stageProtectedComplementSha256,
            "schema-7 stage protected complement");

        string outputTextureComponentSha256 = Hash(outputModel.AsSpan(
            0,
            T66TextureComponentLength));
        RequireHash(
            outputTextureComponentSha256,
            Id65V2NativeTextureCompositionCompiler
                .ExpectedOutputTextureComponentSha256,
            "schema-7 T66 texture component");
        string outputCycloramaSha256 = Hash(outputRow80.AsSpan(
            schema6.CycloramaRow80Offset,
            schema6.CycloramaByteLength));
        RequireHash(
            outputCycloramaSha256,
            schema6.OutputCycloramaSha256,
            "schema-7 preserved cyclorama");
        ComponentLayout collision = outputComponents.Single(
            item => item.Name == "collision");
        string collisionSha256 = collision.Sha256;

        string deterministicPlanSha256 = Hash(Encoding.UTF8.GetBytes(
            string.Join('\n',
            [
                ProfileId,
                SchemaVersion.ToString(CultureInfo.InvariantCulture),
                document.CanonicalSha256,
                schema6.ProfileId,
                schema6.DeterministicPlanSha256,
                schema6.OutputRow80Sha256,
                schema6.OutputModelSha256,
                Hash(outputRow80),
                Hash(outputModel),
                environmentReadback.OutputEnvironmentSha256,
                environmentReadback.RetainedSectorSha256,
                environmentReadback.ZeroGapSha256,
                occlusionReadback.OutputOcclusionSha256,
                collisionSha256,
                outputTextureComponentSha256,
                outputCycloramaSha256,
                changedByteCount.ToString(CultureInfo.InvariantCulture),
                stageChangedByteCount.ToString(CultureInfo.InvariantCulture),
                directRanges.Length.ToString(CultureInfo.InvariantCulture),
                stageRanges.Length.ToString(CultureInfo.InvariantCulture),
                ownedRangeMapSha256,
                transactionSha256,
                protectedComplementSha256,
                stageOwnedRangeMapSha256,
                stageTransactionSha256,
                stageProtectedComplementSha256,
                componentRelocationMapSha256,
                handleRebaseMapSha256
            ])));

        Id65CompiledAuthoringSingleSectorSubstrate compiled = new(
            document,
            sourceRow80,
            schema6Row80,
            outputRow80,
            directRanges,
            directInverse,
            stageRanges,
            stageInverse,
            relocations,
            handleRebases,
            environmentReadback,
            occlusionReadback,
            schema6.DeterministicPlanSha256,
            outputTextureComponentSha256,
            outputCycloramaSha256,
            collisionSha256,
            changedByteCount,
            stageChangedByteCount,
            ownedRangeMapSha256,
            transactionSha256,
            protectedComplementSha256,
            stageOwnedRangeMapSha256,
            stageTransactionSha256,
            stageProtectedComplementSha256,
            componentRelocationMapSha256,
            handleRebaseMapSha256,
            deterministicPlanSha256);
        if (!ApplyTransactional(compiled, compiled.CopySourceRow80(), false)
                .SequenceEqual(outputRow80) ||
            !ApplyTransactional(compiled, compiled.CopyOutputRow80(), true)
                .SequenceEqual(sourceRow80) ||
            !ApplySchema6StageTransactional(
                compiled,
                compiled.CopySchema6BaselineRow80(),
                false).SequenceEqual(outputRow80) ||
            !ApplySchema6StageTransactional(
                compiled,
                compiled.CopyOutputRow80(),
                true).SequenceEqual(schema6Row80))
        {
            throw new InvalidDataException(
                "The single-sector direct or schema-6-stage transaction failed its exact inverse.");
        }
        return compiled;
    }

    public static byte[] ApplyTransactional(
        Id65CompiledAuthoringSingleSectorSubstrate compiled,
        byte[] input,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(compiled);
        ArgumentNullException.ThrowIfNull(input);
        byte[] expected = reverse
            ? compiled.CopyOutputRow80()
            : compiled.CopySourceRow80();
        if (!input.SequenceEqual(expected))
        {
            throw new InvalidDataException(reverse
                ? "The single-sector direct rollback afterimage changed."
                : "The single-sector direct locked-source preimage changed.");
        }
        return ApplyRanges(
            input,
            reverse ? compiled.InverseRanges : compiled.OwnedRanges);
    }

    public static byte[] ApplySchema6StageTransactional(
        Id65CompiledAuthoringSingleSectorSubstrate compiled,
        byte[] input,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(compiled);
        ArgumentNullException.ThrowIfNull(input);
        byte[] expected = reverse
            ? compiled.CopyOutputRow80()
            : compiled.CopySchema6BaselineRow80();
        if (!input.SequenceEqual(expected))
        {
            throw new InvalidDataException(reverse
                ? "The single-sector stage rollback afterimage changed."
                : "The exact schema-6 p2 stage preimage changed.");
        }
        return ApplyRanges(
            input,
            reverse
                ? compiled.StageInverseRanges
                : compiled.StageOwnedRanges);
    }

    private static void ValidateSchema6Baseline(
        Id65CompiledAuthoringBlankCyclorama schema6)
    {
        if (schema6.ProfileId != Id65AuthoringBlankCycloramaCompiler.ProfileId ||
            schema6.InputDocumentSchemaVersion !=
                Id65AuthoringEditorDocument
                    .RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion ||
            !schema6.Document.UsesBlankCyclorama ||
            schema6.DocumentCanonicalSha256 !=
                schema6.Document.CanonicalSha256 ||
            schema6.SourceRow80Sha256 != Hash(schema6.CopySourceRow80()) ||
            schema6.OutputRow80Sha256 != Hash(schema6.CopyOutputRow80()) ||
            schema6.OutputVisibilityOcclusionSha256 !=
                ExpectedSchema6OcclusionSha256 ||
            schema6.VisibleAuthoredSectorIndex != LogicalSourceSectorIndex ||
            schema6.VisibleSectorCount != 1 ||
            !schema6.AuthoredOnlyVisibilityMapVerified ||
            !schema6.InheritedEnvironmentVisibilitySuppressed ||
            !schema6.VisibilityOpaqueTailPreserved ||
            schema6.InheritedEnvironmentBytesRetired ||
            schema6.InheritedCollisionLookupsRetired ||
            !schema6.ExactInverseVerified ||
            !schema6.EveryChangedByteOwned ||
            !schema6.ProtectedComplementVerified ||
            schema6.WritesFileSystem || schema6.WritesDiscImage ||
            schema6.CreateBinEnabled || schema6.RuntimeCandidateAuthorized ||
            schema6.ReleaseIntegrated || schema6.Publishable)
        {
            throw new InvalidDataException(
                "The single-sector compiler requires the exact guarded schema-6 p2 output.");
        }
    }

    private static (byte[] Output,
        Id65AuthoringSingleSectorEnvironmentReadback Readback)
        BuildSingleSectorEnvironment(
            byte[] baseline,
            int row80Offset,
            int expectedSectorLength)
    {
        if (baseline.Length != ReadInt32(baseline, 0) ||
            ReadInt32(baseline, 4) != LogicalSourceSectorIndex + 1 ||
            ReadInt32(baseline, 8 + LogicalSourceSectorIndex * 4) !=
                SourceSectorPointerValue ||
            SourceSectorRelativeOffset + expectedSectorLength !=
                baseline.Length)
        {
            throw new InvalidDataException(
                "The exact 217-sector schema-6 environment layout changed.");
        }
        byte[] sector = baseline.AsSpan(
            SourceSectorRelativeOffset,
            expectedSectorLength).ToArray();
        Id65EnvironmentSectorHeaderReadback sourceHeader =
            Id65EnvironmentSectorHeaderContract.ReadExactSector(
                sector,
                "schema-7 retained logical sector 216");
        byte[] output = new byte[baseline.Length];
        WriteInt32(output, 0, output.Length);
        WriteInt32(output, 4, 1);
        WriteInt32(output, 8, OutputSectorPointerValue);
        sector.CopyTo(output, OutputSectorRelativeOffset);
        Id65EnvironmentSectorHeaderReadback outputHeader =
            Id65EnvironmentSectorHeaderContract.ReadPointerSpan(
                output.AsSpan(OutputSectorRelativeOffset),
                output.Length - OutputSectorRelativeOffset,
                requireExactEnd: false,
                "schema-7 physical sector 0");
        ReadOnlySpan<byte> gap = output.AsSpan(
            OutputSectorRelativeOffset + expectedSectorLength);
        if (outputHeader.ExactSectorByteLength != expectedSectorLength ||
            outputHeader.PointerGapByteLength !=
                EnvironmentZeroGapByteLength ||
            gap.Length != EnvironmentZeroGapByteLength ||
            gap.IndexOfAnyExcept((byte)0) >= 0 ||
            !output.AsSpan(OutputSectorRelativeOffset, expectedSectorLength)
                .SequenceEqual(sector) ||
            sourceHeader.LowDetailVertexCount !=
                outputHeader.LowDetailVertexCount ||
            sourceHeader.LowDetailColorCount !=
                outputHeader.LowDetailColorCount ||
            sourceHeader.LowDetailFaceCount !=
                outputHeader.LowDetailFaceCount ||
            sourceHeader.HighDetailVertexCount !=
                outputHeader.HighDetailVertexCount ||
            sourceHeader.HighDetailColorCount !=
                outputHeader.HighDetailColorCount ||
            sourceHeader.HighDetailFaceCount !=
                outputHeader.HighDetailFaceCount)
        {
            throw new InvalidDataException(
                "The fixed-capacity single-sector environment failed exact semantic readback.");
        }
        string gapHash = Hash(gap);
        RequireHash(gapHash, ExpectedZeroGapSha256, "schema-7 zero pointer gap");
        int hpFaceStart = checked(
            Id65EnvironmentSectorHeaderContract.HeaderByteLength +
            sourceHeader.LowDetailVertexCount * 4 +
            sourceHeader.LowDetailColorCount * 4 +
            sourceHeader.LowDetailFaceCount * 8 +
            sourceHeader.HighDetailVertexCount * 4 +
            sourceHeader.HighDetailColorCount * 8);
        if (sourceHeader.HighDetailFaceCount <= 0 ||
            (ReadUInt32(sector, hpFaceStart + 8) & 0x7F) != 66)
        {
            throw new InvalidDataException(
                "The retained authored sector lost its mandatory T66 base face.");
        }
        int modelOffset = EnvironmentModelOffset;
        return (output, new(
            modelOffset,
            row80Offset,
            checked(Id65AuthoringBlankCycloramaCompiler.TargetDataWadOffset +
                row80Offset),
            output.Length,
            LogicalSourceSectorIndex,
            PhysicalOutputSectorIndex,
            SourceSectorRelativeOffset,
            OutputSectorRelativeOffset,
            expectedSectorLength,
            EnvironmentZeroGapByteLength,
            outputHeader.LowDetailVertexCount,
            outputHeader.LowDetailColorCount,
            outputHeader.LowDetailFaceCount,
            outputHeader.HighDetailVertexCount,
            outputHeader.HighDetailColorCount,
            outputHeader.HighDetailFaceCount,
            Hash(baseline),
            Hash(output),
            Hash(sector),
            gapHash));
    }

    private static (byte[] Output,
        Id65AuthoringSingleSectorOcclusionReadback Readback)
        BuildSectorZeroOcclusion(
            byte[] schema6,
            int modelOffset,
            int row80Offset)
    {
        Id65AuthoringBlankCycloramaCompiler
            .RequireAuthoredOnlyVisibilityOcclusion(schema6);
        if (schema6[OcclusionSectorByteOffset] !=
                checked((byte)LogicalSourceSectorIndex) ||
            schema6[OcclusionSectorByteOffset + 1] != 0xFF)
        {
            throw new InvalidDataException(
                "The schema-6 p2 occlusion owner is no longer the one exact sector-216 byte.");
        }
        byte[] output = schema6.ToArray();
        output[OcclusionSectorByteOffset] = PhysicalOutputSectorIndex;
        RequireSingleSectorOcclusion(output);
        for (int index = 0; index < output.Length; index++)
        {
            if (index != OcclusionSectorByteOffset &&
                output[index] != schema6[index])
            {
                throw new InvalidDataException(
                    "The schema-7 occlusion rebase changed more than sector 216 to sector 0.");
            }
        }
        int changed = CountChangedBytes(schema6, output);
        if (changed != 1)
            throw new InvalidDataException(
                "The schema-7 occlusion rebase must change exactly one byte.");
        return (output, new(
            modelOffset,
            row80Offset,
            checked(Id65AuthoringBlankCycloramaCompiler.TargetDataWadOffset +
                row80Offset),
            output.Length,
            16,
            0,
            LogicalSourceSectorIndex,
            PhysicalOutputSectorIndex,
            changed,
            Hash(schema6),
            Hash(output),
            Hash(output.AsSpan(
                Id65AuthoringBlankCycloramaCompiler.VisibilityOpaqueTailOffset,
                Id65AuthoringBlankCycloramaCompiler.VisibilityOpaqueTailByteLength))));
    }

    internal static void RequireSingleSectorOcclusion(ReadOnlySpan<byte> bytes)
    {
        byte[] component = bytes.ToArray();
        if (component.Length !=
                Id65AuthoringBlankCycloramaCompiler
                    .ExpectedVisibilityOcclusionByteLength ||
            Hash(component) != ExpectedOutputOcclusionSha256 ||
            ReadInt32(component, 0) != component.Length ||
            ReadInt32(component, 4) !=
                Id65AuthoringBlankCycloramaCompiler
                    .ExpectedVisibilityEnvironmentPortionByteLength ||
            ReadInt32(component, 8) != 16 ||
            ReadInt32(component, 12) != 0x48 ||
            component[OcclusionSectorByteOffset] != PhysicalOutputSectorIndex ||
            component[OcclusionSectorByteOffset + 1] != 0xFF ||
            Hash(component.AsSpan(
                Id65AuthoringBlankCycloramaCompiler.VisibilityOpaqueTailOffset,
                Id65AuthoringBlankCycloramaCompiler.VisibilityOpaqueTailByteLength)) !=
                Id65AuthoringBlankCycloramaCompiler
                    .ExpectedVisibilityOpaqueTailSha256)
        {
            throw new InvalidDataException(
                "The schema-7 sector-zero occlusion component lost its exact identity.");
        }
        int environmentEnd = checked(4 + ReadInt32(component, 4));
        for (int group = 0; group < 16; group++)
        {
            int offset = checked(4 + ReadInt32(component, 12 + group * 4));
            if (offset < 76 || offset >= environmentEnd)
                throw new InvalidDataException(
                    "A schema-7 occlusion group pointer escaped its directory.");
            List<int> sectors = [];
            while (offset < environmentEnd && component[offset] != 0xFF)
            {
                int sector = component[offset++];
                if (sector != PhysicalOutputSectorIndex)
                    throw new InvalidDataException(
                        "A schema-7 occlusion group references a non-owned physical sector.");
                sectors.Add(sector);
            }
            if (offset >= environmentEnd ||
                group == 0 && !sectors.SequenceEqual([PhysicalOutputSectorIndex]) ||
                group > 0 && sectors.Count != 0)
            {
                throw new InvalidDataException(
                    "The schema-7 authored-only occlusion group ownership changed.");
            }
        }
    }

    private static ComponentLayout[] ParseModelComponents(
        byte[] model,
        int activeTerrainCount,
        int expectedEnvironmentLength,
        int expectedCycloramaOffset)
    {
        if (model.Length != ModelLength)
            throw new InvalidDataException(
                "The schema-7 model capacity changed.");
        List<ComponentLayout> components = [];
        int cursor = 0;
        foreach (string name in new[]
                 {
                     "texture", "environment", "occlusion",
                     "special-surface", "collision", "cyclorama"
                 })
        {
            int length = ReadInt32(model, cursor);
            if (length < 4 || (length & 3) != 0 ||
                cursor + (long)length > model.Length)
            {
                throw new InvalidDataException(
                    $"The schema-7 {name} component length is invalid.");
            }
            components.Add(new(
                name,
                cursor,
                length,
                Hash(model.AsSpan(cursor, length))));
            cursor += length;
        }
        int portalOffset = cursor;
        if (ReadInt32(model, portalOffset) != 0)
            throw new InvalidDataException(
                "The schema-7 portal table is not empty.");
        components.Add(new(
            "portal-table",
            portalOffset,
            4,
            Hash(model.AsSpan(portalOffset, 4))));
        cursor += 4;
        foreach (string name in new[] { "particles", "sound" })
        {
            int length = ReadInt32(model, cursor);
            if (length < 4 || (length & 3) != 0 ||
                cursor + (long)length > model.Length)
            {
                throw new InvalidDataException(
                    $"The schema-7 {name} component length is invalid.");
            }
            components.Add(new(
                name,
                cursor,
                length,
                Hash(model.AsSpan(cursor, length))));
            cursor += length;
        }
        if (model.AsSpan(cursor).IndexOfAnyExcept((byte)0) >= 0 ||
            components[0].Offset != 0 ||
            components[0].ByteLength != T66TextureComponentLength ||
            components[1].Offset != EnvironmentModelOffset ||
            components[1].ByteLength != expectedEnvironmentLength ||
            components[2].Offset != 0x2B548 + activeTerrainCount * 0x20 ||
            components[5].Offset != expectedCycloramaOffset)
        {
            throw new InvalidDataException(
                "The fixed-capacity schema-7 model component topology changed.");
        }
        return components.ToArray();
    }

    private static Id65ModelComponentRelocation[] BuildRelocations(
        IReadOnlyList<ComponentLayout> source,
        IReadOnlyList<ComponentLayout> output)
    {
        if (!source.Select(item => item.Name)
            .SequenceEqual(output.Select(item => item.Name)))
        {
            throw new InvalidDataException(
                "The schema-7 component identity sequence changed.");
        }
        Id65ModelComponentRelocation[] result = source.Zip(output)
            .Select(pair => new Id65ModelComponentRelocation(
                "component." + pair.First.Name,
                pair.First.Offset,
                pair.First.ByteLength,
                pair.Second.Offset,
                pair.Second.ByteLength,
                pair.First.Sha256,
                pair.Second.Sha256,
                pair.First.Sha256 == pair.Second.Sha256))
            .ToArray();
        if (result.Any(item =>
                item.SourceRelativeOffset != item.OutputRelativeOffset ||
                item.SourceByteLength != item.OutputByteLength) ||
            !result.Where(item => !item.ContentsPreserved)
                .Select(item => item.StableId)
                .SequenceEqual(
                    ["component.environment", "component.occlusion"]))
        {
            throw new InvalidDataException(
                "The schema-7 stage relocated or changed an unexpected model component.");
        }
        return result;
    }

    private static void ValidatePreservedComponents(
        Id65CompiledAuthoringBlankCyclorama schema6,
        byte[] schema6Row80,
        byte[] outputRow80,
        IReadOnlyList<ComponentLayout> sourceComponents,
        IReadOnlyList<ComponentLayout> outputComponents,
        Id65AuthoringSingleSectorEnvironmentReadback environment,
        Id65AuthoringSingleSectorOcclusionReadback occlusion)
    {
        if (schema6Row80.Length != Row80Length ||
            outputRow80.Length != Row80Length ||
            environment.ComponentByteLength !=
                sourceComponents.Single(item => item.Name == "environment")
                    .ByteLength ||
            occlusion.ComponentByteLength !=
                sourceComponents.Single(item => item.Name == "occlusion")
                    .ByteLength ||
            environment.Row80Offset !=
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset +
                    environment.ModelOffset ||
            occlusion.Row80Offset !=
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset +
                    occlusion.ModelOffset)
        {
            throw new InvalidDataException(
                "The schema-7 environment/occlusion physical binding changed.");
        }
        foreach (string name in new[]
                 {
                     "texture", "special-surface", "collision", "cyclorama",
                     "portal-table", "particles", "sound"
                 })
        {
            ComponentLayout before = sourceComponents.Single(
                item => item.Name == name);
            ComponentLayout after = outputComponents.Single(
                item => item.Name == name);
            if (before.Offset != after.Offset ||
                before.ByteLength != after.ByteLength ||
                before.Sha256 != after.Sha256)
            {
                throw new InvalidDataException(
                    $"The schema-7 stage changed protected component {name}.");
            }
        }
        if (!schema6Row80.AsSpan(
                schema6.CycloramaRow80Offset,
                schema6.CycloramaByteLength).SequenceEqual(
                    outputRow80.AsSpan(
                        schema6.CycloramaRow80Offset,
                        schema6.CycloramaByteLength)))
        {
            throw new InvalidDataException(
                "The schema-7 stage changed the blank schema-6 cyclorama.");
        }
    }

    private static Id65StableHandleRebase[] BuildHandleRebases(
        IReadOnlyDictionary<string,
            (string Kind, Id65LogicalAddress Address)> addresses)
    {
        return addresses.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(pair => new Id65StableHandleRebase(
                pair.Key,
                pair.Value.Kind,
                pair.Value.Address,
                RebaseAddress(pair.Value.Address)))
            .ToArray();
    }

    private static Id65LogicalAddress RebaseAddress(
        Id65LogicalAddress address) => address switch
        {
            { Space: "scene-sector", Primary: LogicalSourceSectorIndex } =>
                address with { Primary = PhysicalOutputSectorIndex },
            { Space: "terrain-tile", Primary: LogicalSourceSectorIndex } =>
                address with { Primary = PhysicalOutputSectorIndex },
            { Space: "occlusion-group", Secondary: LogicalSourceSectorIndex } =>
                address with { Secondary = PhysicalOutputSectorIndex },
            _ => address
        };

    private static void ValidateHandleRebases(
        IReadOnlyList<Id65StableHandleRebase> rebases)
    {
        if (rebases.Count == 0 ||
            rebases.Select(item => item.StableId)
                .Distinct(StringComparer.Ordinal).Count() != rebases.Count ||
            !rebases.Any(item =>
                item.Source?.Space == "scene-sector" &&
                item.Source?.Primary == LogicalSourceSectorIndex &&
                item.Output?.Primary == PhysicalOutputSectorIndex) ||
            !rebases.Any(item =>
                item.Source?.Space == "occlusion-group" &&
                item.Source?.Secondary == LogicalSourceSectorIndex &&
                item.Output?.Secondary == PhysicalOutputSectorIndex))
        {
            throw new InvalidDataException(
                "The schema-7 stable handles lost their sector-216 to sector-0 rebase.");
        }
        foreach (Id65StableHandleRebase item in rebases)
        {
            if (!item.Source.HasValue || !item.Output.HasValue)
                throw new InvalidDataException(
                    "A schema-7 stable handle became unresolved.");
            Id65LogicalAddress expected = RebaseAddress(item.Source.Value);
            if (item.Output.Value != expected)
                throw new InvalidDataException(
                    $"Schema-7 stable handle {item.StableId} has the wrong physical address.");
        }
    }

    private static Id65AuthoringCompositeOwnedRange[] BuildStageLedger(
        byte[] baseline,
        byte[] output,
        int environmentOffset,
        int environmentLength,
        int occlusionOffset,
        int occlusionLength)
    {
        List<Id65AuthoringCompositeOwnedRange> result = [];
        AppendDiffRuns(
            result,
            baseline,
            output,
            environmentOffset,
            environmentLength,
            "schema7.stage.environment.",
            EnvironmentOwnerId);
        AppendDiffRuns(
            result,
            baseline,
            output,
            occlusionOffset,
            occlusionLength,
            "schema7.stage.occlusion.",
            OcclusionOwnerId);
        return result.OrderBy(item => item.DataRelativeOffset).ToArray();
    }

    private static Id65AuthoringCompositeOwnedRange[] BuildDirectLedger(
        byte[] source,
        byte[] output,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> schema6Ranges,
        int environmentOffset,
        int environmentLength,
        int occlusionOffset,
        int occlusionLength)
    {
        (int Start, int End)[] replacementSpans =
        [
            (environmentOffset, checked(environmentOffset + environmentLength)),
            (occlusionOffset, checked(occlusionOffset + occlusionLength))
        ];
        List<Id65AuthoringCompositeOwnedRange> result = [];
        foreach (Id65AuthoringCompositeOwnedRange baseline in schema6Ranges)
        {
            List<(int Start, int End)> residuals =
                [(baseline.DataRelativeOffset,
                  checked(baseline.DataRelativeOffset + baseline.ByteLength))];
            foreach ((int spanStart, int spanEnd) in replacementSpans)
            {
                List<(int Start, int End)> next = [];
                foreach ((int start, int end) in residuals)
                {
                    if (end <= spanStart || start >= spanEnd)
                    {
                        next.Add((start, end));
                        continue;
                    }
                    if (start < spanStart) next.Add((start, spanStart));
                    if (end > spanEnd) next.Add((spanEnd, end));
                }
                residuals = next;
            }
            bool unchangedShape = residuals.Count == 1 &&
                residuals[0].Start == baseline.DataRelativeOffset &&
                residuals[0].End ==
                    baseline.DataRelativeOffset + baseline.ByteLength;
            int part = 0;
            foreach ((int start, int end) in residuals)
            {
                int length = end - start;
                byte[] before = source.AsSpan(start, length).ToArray();
                byte[] after = output.AsSpan(start, length).ToArray();
                if (before.SequenceEqual(after)) continue;
                result.Add(new(
                    unchangedShape
                        ? baseline.StableId
                        : baseline.StableId + ".schema7-residual-" +
                          part.ToString("D3", CultureInfo.InvariantCulture),
                    baseline.OwnerId,
                    start,
                    before,
                    after));
                part++;
            }
        }
        AppendDiffRuns(
            result,
            source,
            output,
            environmentOffset,
            environmentLength,
            "schema7.row80.environment.",
            EnvironmentOwnerId);
        AppendDiffRuns(
            result,
            source,
            output,
            occlusionOffset,
            occlusionLength,
            "schema7.row80.occlusion.",
            OcclusionOwnerId);
        return result
            .OrderBy(item => item.DataRelativeOffset)
            .ThenBy(item => item.StableId, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AppendDiffRuns(
        ICollection<Id65AuthoringCompositeOwnedRange> result,
        byte[] source,
        byte[] output,
        int offset,
        int length,
        string stablePrefix,
        string ownerId)
    {
        int end = checked(offset + length);
        int cursor = offset;
        int part = 0;
        while (cursor < end)
        {
            while (cursor < end && source[cursor] == output[cursor]) cursor++;
            if (cursor == end) break;
            int start = cursor++;
            while (cursor < end && source[cursor] != output[cursor]) cursor++;
            int runLength = cursor - start;
            result.Add(new(
                stablePrefix + part.ToString("D3", CultureInfo.InvariantCulture),
                ownerId,
                start,
                source.AsSpan(start, runLength).ToArray(),
                output.AsSpan(start, runLength).ToArray()));
            part++;
        }
        if (part == 0)
            throw new InvalidDataException(
                $"Owned stage span {stablePrefix} produced no changed bytes.");
    }

    private static Id65AuthoringCompositeOwnedRange[] InvertRanges(
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges) => ranges
        .Reverse()
        .Select(item => new Id65AuthoringCompositeOwnedRange(
            item.StableId,
            item.OwnerId,
            item.DataRelativeOffset,
            item.CopyAfter(),
            item.CopyBefore()))
        .ToArray();

    private static void ValidateLedger(
        byte[] source,
        byte[] output,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges,
        string label)
    {
        if (ranges.Count == 0 ||
            ranges.Select(item => item.StableId)
                .Distinct(StringComparer.Ordinal).Count() != ranges.Count)
        {
            throw new InvalidDataException(
                $"The {label} ledger has no unique owned ranges.");
        }
        int changed = 0;
        for (int index = 0; index < ranges.Count; index++)
        {
            Id65AuthoringCompositeOwnedRange range = ranges[index];
            byte[] before = range.CopyBefore();
            byte[] after = range.CopyAfter();
            if (range.DataRelativeOffset < 0 || range.ByteLength <= 0 ||
                range.DataRelativeOffset + (long)range.ByteLength > source.Length ||
                source.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(before) == false ||
                output.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(after) == false ||
                before.SequenceEqual(after) ||
                Hash(before) != range.BeforeSha256 ||
                Hash(after) != range.AfterSha256 ||
                index > 0 &&
                    ranges[index - 1].DataRelativeOffset +
                        ranges[index - 1].ByteLength >
                    range.DataRelativeOffset)
            {
                throw new InvalidDataException(
                    $"A {label} range lost bounds, preimage, hash, or nonoverlap.");
            }
            changed = checked(changed + CountChangedBytes(before, after));
        }
        if (changed != CountChangedBytes(source, output))
            throw new InvalidDataException(
                $"The {label} ledger does not own every changed byte.");
    }

    private static void VerifyOnlyStageComponentsChanged(
        byte[] source,
        byte[] output,
        int environmentOffset,
        int environmentLength,
        int occlusionOffset,
        int occlusionLength)
    {
        if (source.Length != output.Length)
            throw new InvalidDataException(
                "The schema-7 row length changed.");
        for (int index = 0; index < source.Length; index++)
        {
            bool owned = index >= environmentOffset &&
                    index < environmentOffset + environmentLength ||
                index >= occlusionOffset &&
                    index < occlusionOffset + occlusionLength;
            if (!owned && source[index] != output[index])
                throw new InvalidDataException(
                    "The schema-7 stage changed bytes outside environment/occlusion.");
        }
    }

    private static byte[] ApplyRanges(
        byte[] input,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges)
    {
        byte[] output = input.ToArray();
        foreach (Id65AuthoringCompositeOwnedRange range in ranges)
        {
            if (!output.AsSpan(
                    range.DataRelativeOffset,
                    range.ByteLength).SequenceEqual(range.CopyBefore()))
            {
                throw new InvalidDataException(
                    "Single-sector range " + range.StableId +
                    " lost its exact preimage.");
            }
            range.CopyAfter().CopyTo(output, range.DataRelativeOffset);
        }
        return output;
    }

    private static string HashOwnedRanges(
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

    private static string HashTransaction(
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

    private static string HashProtectedComplement(
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

    private static string HashRelocations(
        IEnumerable<Id65ModelComponentRelocation> values)
    {
        StringBuilder text = new();
        foreach (Id65ModelComponentRelocation item in values.OrderBy(
                     item => item.StableId,
                     StringComparer.Ordinal))
        {
            text.Append(item.StableId).Append('|')
                .Append(item.SourceRelativeOffset.ToString(
                    "X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(item.SourceByteLength).Append('|')
                .Append(item.OutputRelativeOffset.ToString(
                    "X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(item.OutputByteLength).Append('|')
                .Append(item.SourceSha256).Append('|')
                .Append(item.OutputSha256).Append('|')
                .Append(item.ContentsPreserved ? '1' : '0').Append('\n');
        }
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static string HashHandleRebases(
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

    private static string Address(Id65LogicalAddress? address) =>
        address.HasValue
            ? string.Create(CultureInfo.InvariantCulture,
                $"{address.Value.Space}:{address.Value.Primary}:{address.Value.Secondary}")
            : "none";

    private static int CountChangedBytes(
        ReadOnlySpan<byte> before,
        ReadOnlySpan<byte> after)
    {
        if (before.Length != after.Length)
            throw new InvalidDataException(
                "Single-sector changed-byte inputs differ in length.");
        int changed = 0;
        for (int index = 0; index < before.Length; index++)
        {
            if (before[index] != after[index]) changed++;
        }
        return changed;
    }

    private static int ReadInt32(ReadOnlySpan<byte> bytes, int offset)
    {
        RequireRange(bytes, offset, 4, "32-bit value");
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset)
    {
        RequireRange(bytes, offset, 4, "unsigned 32-bit value");
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
    }

    private static void WriteInt32(Span<byte> bytes, int offset, int value)
    {
        RequireRange(bytes, offset, 4, "32-bit output value");
        BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(offset, 4), value);
    }

    private static void RequireRange(
        ReadOnlySpan<byte> bytes,
        int offset,
        int length,
        string label)
    {
        if (offset < 0 || length < 0 || offset + (long)length > bytes.Length)
            throw new InvalidDataException(
                $"The schema-7 {label} range is outside its buffer.");
    }

    private static void RequireHash(
        string actual,
        string expected,
        string label)
    {
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"The {label} SHA-256 is {actual}, expected {expected}.");
    }

    internal static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private sealed record ComponentLayout(
        string Name,
        int Offset,
        int ByteLength,
        string Sha256);
}
