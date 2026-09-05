using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

internal sealed record Id65AuthoringRemotePadTerrainSetPhysicalCompileRequest(
    Id65AuthoringEditorDocument Document,
    Id65AuthoringCompositeLockedSource Source,
    UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan V3Template,
    Id65V2NativeTextureWitness TextureWitness,
    Id65MobyDependencyBundleDescriptor GrassBundle,
    Id65AuthoringTerrainSetLimits? TerrainLimits = null,
    Id65AuthoringCompositeCompilerLimits? CompositeLimits = null);

internal sealed record Id65AuthoringRemotePadTerrainSetPhysicalReadback(
    string TerrainManifestSha256,
    IReadOnlyList<string> ActiveTerrainUnitIds,
    string V3TerrainModelSha256,
    string T66TerrainModelSha256,
    string T66TextureComponentSha256,
    string V3TerrainEnvironmentSha256,
    string FinalT66EnvironmentSha256,
    string CollisionSha256,
    string V3TerrainProtectedComplementSha256,
    string FinalT66ProtectedComponentSetSha256,
    int UsedModelBytes,
    int ZeroTailBytes,
    int MandatoryBaseFaceModelOffset,
    int MandatoryBaseFaceTextureId,
    IReadOnlyList<Id65AuthoringTerrainUnitReadback> Units);

internal sealed class Id65CompiledAuthoringRemotePadTerrainSetPhysical
{
    private readonly byte[] _sourceRow80;
    private readonly byte[] _outputRow80;
    private readonly ReadOnlyCollection<Id65AuthoringCompositeOwnedRange> _ownedRanges;
    private readonly ReadOnlyCollection<Id65AuthoringCompositeOwnedRange> _inverseRanges;
    private readonly ReadOnlyCollection<string> _activeTerrainUnitIds;

    internal Id65CompiledAuthoringRemotePadTerrainSetPhysical(
        Id65AuthoringEditorDocument document,
        Id65RemotePadMobyKind kind,
        string positionId,
        byte[] sourceRow80,
        byte[] outputRow80,
        IEnumerable<Id65AuthoringCompositeOwnedRange> ownedRanges,
        IEnumerable<Id65AuthoringCompositeOwnedRange> inverseRanges,
        Id65AuthoringRemotePadMobyChoicePhysicalPlacementReadback placement,
        Id65AuthoringRemotePadTerrainSetPhysicalReadback terrain,
        string schema4BaselineDocumentSha256,
        string schema4BaselineOutputRow80Sha256,
        string schema4BaselineOutputModelSha256,
        string schema4BaselineDeterministicPlanSha256,
        int changedByteCount,
        string ownedRangeMapSha256,
        string transactionSha256,
        string protectedComplementSha256,
        string deterministicPlanSha256,
        string outputTexturePagesSha256,
        string landingSha256,
        string playerAnchorSha256,
        string targetOverlaySha256,
        string globalExecutableSha256)
    {
        Document = document;
        DocumentCanonicalSha256 = document.CanonicalSha256;
        MobyKind = kind;
        PositionId = positionId;
        _activeTerrainUnitIds = Array.AsReadOnly(
            document.ActiveTerrainUnitIds.ToArray());
        _sourceRow80 = sourceRow80.ToArray();
        _outputRow80 = outputRow80.ToArray();
        _ownedRanges = Array.AsReadOnly(
            ownedRanges.Select(item => item.DeepCopy()).ToArray());
        _inverseRanges = Array.AsReadOnly(
            inverseRanges.Select(item => item.DeepCopy()).ToArray());
        Placement = placement;
        Terrain = terrain with
        {
            ActiveTerrainUnitIds = Array.AsReadOnly(
                terrain.ActiveTerrainUnitIds.ToArray()),
            Units = Array.AsReadOnly(terrain.Units.ToArray())
        };
        SourceRow80Sha256 =
            Id65AuthoringRemotePadTerrainSetPhysicalCompiler.Hash(_sourceRow80);
        OutputRow80Sha256 =
            Id65AuthoringRemotePadTerrainSetPhysicalCompiler.Hash(_outputRow80);
        OutputModelSha256 = Id65AuthoringRemotePadTerrainSetPhysicalCompiler.Hash(
            _outputRow80.AsSpan(
                Id65AuthoringRemotePadCompositeCompiler.ModelOffset,
                Id65AuthoringRemotePadCompositeCompiler.ModelLength));
        Schema4BaselineDocumentSha256 = schema4BaselineDocumentSha256;
        Schema4BaselineOutputRow80Sha256 = schema4BaselineOutputRow80Sha256;
        Schema4BaselineOutputModelSha256 = schema4BaselineOutputModelSha256;
        Schema4BaselineDeterministicPlanSha256 =
            schema4BaselineDeterministicPlanSha256;
        ChangedByteCount = changedByteCount;
        OwnedRangeMapSha256 = ownedRangeMapSha256;
        TransactionSha256 = transactionSha256;
        ProtectedComplementSha256 = protectedComplementSha256;
        DeterministicPlanSha256 = deterministicPlanSha256;
        OutputTexturePagesSha256 = outputTexturePagesSha256;
        LandingSha256 = landingSha256;
        PlayerAnchorSha256 = playerAnchorSha256;
        TargetOverlaySha256 = targetOverlaySha256;
        GlobalExecutableSha256 = globalExecutableSha256;
    }

    public string ProfileId =>
        Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ProfileId;
    public int SchemaVersion =>
        Id65AuthoringRemotePadTerrainSetPhysicalCompiler.SchemaVersion;
    public int InputDocumentSchemaVersion => Document.SchemaVersion;
    public Id65AuthoringEditorDocument Document { get; }
    public string DocumentCanonicalSha256 { get; }
    public Id65RemotePadMobyKind MobyKind { get; }
    public string PositionId { get; }
    public IReadOnlyList<string> ActiveTerrainUnitIds => _activeTerrainUnitIds;
    public Id65AuthoringRemotePadMobyChoicePhysicalPlacementReadback Placement { get; }
    public Id65AuthoringRemotePadTerrainSetPhysicalReadback Terrain { get; }
    public string SourceRow80Sha256 { get; }
    public string OutputRow80Sha256 { get; }
    public string OutputModelSha256 { get; }
    public string Schema4BaselineDocumentSha256 { get; }
    public string Schema4BaselineOutputRow80Sha256 { get; }
    public string Schema4BaselineOutputModelSha256 { get; }
    public string Schema4BaselineDeterministicPlanSha256 { get; }
    public IReadOnlyList<Id65AuthoringCompositeOwnedRange> OwnedRanges => _ownedRanges;
    public IReadOnlyList<Id65AuthoringCompositeOwnedRange> InverseRanges => _inverseRanges;
    public int ChangedByteCount { get; }
    public string OwnedRangeMapSha256 { get; }
    public string TransactionSha256 { get; }
    public string ProtectedComplementSha256 { get; }
    public string DeterministicPlanSha256 { get; }
    public string OutputTexturePagesSha256 { get; }
    public string LandingSha256 { get; }
    public string PlayerAnchorSha256 { get; }
    public string TargetOverlaySha256 { get; }
    public string GlobalExecutableSha256 { get; }

    public bool PureInMemory => true;
    public bool ExactSchema5DocumentRequired => true;
    public bool BuildsEveryTargetFromLockedV3Source => true;
    public bool CallerAfterimagesAccepted => false;
    public bool IndependentTerrainSemanticReadbackVerified => true;
    public bool MobyPlacementReadbackVerified => true;
    public bool NativeT66TexturePreserved => true;
    public bool ExactSpawnPreserved => true;
    public bool OverlayAndExecutablePreserved => true;
    public bool ExactInverseVerified => true;
    public bool EveryChangedByteOwned => true;
    public bool ProtectedComplementVerified => true;
    public bool ContainsOutputRow80Bytes => true;
    public bool AcceptsAggregatePlans => false;
    public bool AcceptsMaterializedCandidates => false;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool WritesCue => false;
    public bool AppIntegrated => false;
    public bool UiIntegrated => false;
    public bool AggregateMaterializerEnabled => false;
    public bool CandidateMaterializerEnabled => false;
    public bool PhysicalMaterializationEnabled => false;
    public bool CreateBinEnabled => false;
    public bool NormalCreateBinEnabled => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool RuntimeProven => false;
    public bool ReleaseIntegrated => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;

    internal byte[] CopySourceRow80() => _sourceRow80.ToArray();
    internal byte[] CopyOutputRow80() => _outputRow80.ToArray();
}

/// <summary>
/// Pure in-memory schema-5 compiler for the closed terrain-set x Moby-choice
/// product. Each result is derived from the exact locked row and exact v3
/// provider input. It never accepts or stacks a caller-owned compiled row.
/// </summary>
internal static class Id65AuthoringRemotePadTerrainSetPhysicalCompiler
{
    public const string ProfileId =
        "id65-authoring-remote-pad-terrain-set-schema5-row80-in-memory-p1";
    public const int SchemaVersion = 1;
    public const int ExpectedStateCount = 108;
    public const int Row80ByteLength =
        Id65AuthoringRemotePadMobyChoicePhysicalCompiler.Row80ByteLength;
    public const int ModelOffset =
        Id65AuthoringRemotePadCompositeCompiler.ModelOffset;
    public const int ModelLength =
        Id65AuthoringRemotePadCompositeCompiler.ModelLength;
    public const int T66TextureComponentLength =
        Id65V2NativeTextureCompositionCompiler.OutputTextureComponentBytes;
    private const int V3TextureComponentLength =
        Id65V2NativeTextureCompositionCompiler.SourceTextureComponentBytes;
    private const string ModelOwnerId = "model.remote-pad.schema5-terrain-set";
    public const string ResidentT21XyzOwnedRangeStableId =
        "schema5.row80.1d08b4";

    public static Id65CompiledAuthoringRemotePadTerrainSetPhysical Compile(
        Id65AuthoringRemotePadTerrainSetPhysicalCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Document);
        ArgumentNullException.ThrowIfNull(request.Source);
        ArgumentNullException.ThrowIfNull(request.V3Template);
        ArgumentNullException.ThrowIfNull(request.TextureWitness);
        ArgumentNullException.ThrowIfNull(request.GrassBundle);

        (Id65AuthoringEditorDocument document,
            Id65RemotePadMobyKind kind,
            Id65RemotePadGrassT107Position position,
            Id65AuthoringTerrainSetManifest manifest) =
            ValidateDocument(request.Document);
        ValidateLockedV3Provider(request.Source, request.V3Template);

        Id65CompiledAuthoringTerrainSet emptyTerrain =
            Id65AuthoringTerrainSetCompiler.CompileTarget(
                Id65AuthoringTerrainSetCatalog.Create(),
                request.V3Template,
                request.TerrainLimits);
        Id65CompiledAuthoringTerrainSet targetTerrain =
            Id65AuthoringTerrainSetCompiler.CompileTarget(
                manifest,
                request.V3Template,
                request.TerrainLimits);
        ValidateTerrainCompiles(emptyTerrain, targetTerrain, manifest);
        Id65AuthoringCompositeCompilerLimits effectiveCompositeLimits =
            request.CompositeLimits ?? new();
        int finalUsedModelBytes = checked(
            targetTerrain.Capacity.UsedModelBytes +
            Id65V2NativeTextureCompositionCompiler.RecordGrowthBytes);
        if (effectiveCompositeLimits.ModelByteCapacity < ModelLength ||
            finalUsedModelBytes >
                effectiveCompositeLimits.MaximumOutputUsedModelBytes)
        {
            throw new InvalidDataException(
                "The schema-5 final T66 terrain model exceeds the requested composite model capacity.");
        }

        Id65AuthoringEditorDocument schema4BaselineDocument =
            Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceExact(
                kind,
                position.PositionId,
                includeOptionalTerrain: false);
        Id65CompiledAuthoringRemotePadMobyChoicePhysical schema4Baseline =
            Id65AuthoringRemotePadMobyChoicePhysicalCompiler.Compile(new(
                schema4BaselineDocument,
                request.Source,
                request.TextureWitness,
                request.GrassBundle,
                request.CompositeLimits));
        ValidateSchema4Baseline(
            schema4Baseline,
            schema4BaselineDocument,
            request.Source,
            kind,
            position);

        byte[] source = request.Source.CopyRow80();
        byte[] baselineOutput = schema4Baseline.CopyOutputRow80();
        byte[] baselineModel = baselineOutput.AsSpan(ModelOffset, ModelLength).ToArray();
        (byte[] finalModel, int mandatoryFaceOffset) = ComposeT66TerrainModel(
            baselineModel,
            emptyTerrain,
            targetTerrain);
        byte[] output = baselineOutput.ToArray();
        finalModel.CopyTo(output, ModelOffset);

        byte[] finalSemanticV3View = BuildFinalSemanticV3View(
            finalModel,
            targetTerrain);
        IReadOnlyList<Id65AuthoringTerrainUnitReadback> independentUnits =
            Id65AuthoringTerrainSetCompiler.DecodeOutputSemantics(
                manifest,
                finalSemanticV3View);
        ValidateCombinedOutput(
            source,
            baselineOutput,
            output,
            finalModel,
            targetTerrain,
            independentUnits,
            schema4Baseline,
            kind,
            position,
            mandatoryFaceOffset);

        List<Id65AuthoringCompositeOwnedRange> ledger = BuildOwnedLedger(
            source,
            output,
            schema4Baseline.OwnedRanges);
        ValidateLedger(source, output, ledger, kind, position.PositionId);
        Id65AuthoringCompositeOwnedRange[] inverse = ledger
            .Reverse<Id65AuthoringCompositeOwnedRange>()
            .Select(item => new Id65AuthoringCompositeOwnedRange(
                item.StableId,
                item.OwnerId,
                item.DataRelativeOffset,
                item.CopyAfter(),
                item.CopyBefore()))
            .ToArray();
        byte[] applied = ApplyOwnedRanges(source, ledger);
        byte[] rolledBack = ApplyOwnedRanges(applied, inverse);
        if (!applied.SequenceEqual(output) || !rolledBack.SequenceEqual(source))
            throw new InvalidDataException(
                "The schema-5 combined row transaction failed its exact inverse.");

        string protectedComplementSha256 = HashProtectedComplement(source, ledger);
        RequireHash(
            HashProtectedComplement(output, ledger),
            protectedComplementSha256,
            "schema-5 combined protected complement");
        string ownedRangeMapSha256 = HashOwnedRanges(ledger);
        string transactionSha256 = HashTransaction(ledger);
        int changedByteCount = CountChangedBytes(source, output);
        string outputTexturePagesSha256 = Hash(output.AsSpan(
            Id65V2NativeTextureCompositionCompiler.TexturePagesDataRelativeOffset,
            Id65V2NativeTextureCompositionCompiler.TexturePagesByteLength));
        string landingSha256 = Hash(output.AsSpan(
            UnusedLevel65RemoteBlankIsolationConstruction.LandingDataRelativeOffset,
            0x10));
        string playerAnchorSha256 = Hash(output.AsSpan(
            UnusedLevel65RemoteBlankIsolationConstruction.PlayerAnchorDataRelativeOffset,
            Id65RemotePadRedGemT21Catalog.RowByteLength));
        RequireHash(
            outputTexturePagesSha256,
            Id65V2NativeTextureCompositionCompiler.ExpectedOutputTexturePagesSha256,
            "schema-5 native T66 texture pages");
        RequireHash(
            landingSha256,
            request.V3Template.Spawn.OutputLandingSha256,
            "schema-5 landing spawn");
        RequireHash(
            playerAnchorSha256,
            request.V3Template.Spawn.OutputPlayerAnchorSha256,
            "schema-5 player-anchor spawn");
        string deterministicPlanSha256 = Hash(Encoding.UTF8.GetBytes(string.Join('\n',
        [
            ProfileId,
            SchemaVersion.ToString(CultureInfo.InvariantCulture),
            document.CanonicalSha256,
            manifest.CanonicalSha256,
            Id65RemotePadMobyChoiceCatalog.CanonicalSha256,
            Id65RemotePadMobyChoiceCatalog.GetKindId(kind),
            position.PositionId,
            request.V3Template.DeterministicPlanSha256,
            emptyTerrain.DeterministicPlanSha256,
            targetTerrain.DeterministicPlanSha256,
            schema4Baseline.DeterministicPlanSha256,
            Hash(source),
            Hash(output),
            Hash(finalModel),
            changedByteCount.ToString(CultureInfo.InvariantCulture),
            ledger.Count.ToString(CultureInfo.InvariantCulture),
            ownedRangeMapSha256,
            transactionSha256,
            protectedComplementSha256,
            outputTexturePagesSha256,
            landingSha256,
            playerAnchorSha256,
            request.Source.TargetOverlaySha256,
            request.Source.GlobalExecutableSha256
        ])));

        Id65AuthoringRemotePadTerrainSetPhysicalReadback terrainReadback = new(
            manifest.CanonicalSha256,
            Array.AsReadOnly(document.ActiveTerrainUnitIds.ToArray()),
            targetTerrain.ModelSha256,
            Hash(finalModel),
            Hash(finalModel.AsSpan(0, T66TextureComponentLength)),
            targetTerrain.EnvironmentSha256,
            HashFinalEnvironment(finalModel, targetTerrain),
            targetTerrain.CollisionSha256,
            targetTerrain.ProtectedComplementSha256,
            HashFinalProtectedComponentSet(finalModel, targetTerrain),
            targetTerrain.Capacity.UsedModelBytes +
                Id65V2NativeTextureCompositionCompiler.RecordGrowthBytes,
            targetTerrain.Capacity.ZeroTailBytes -
                Id65V2NativeTextureCompositionCompiler.RecordGrowthBytes,
            mandatoryFaceOffset,
            Id65AuthoringEditorDocument.PrivateMandatoryBaseFaceTextureId,
            Array.AsReadOnly(independentUnits.ToArray()));
        Id65CompiledAuthoringRemotePadTerrainSetPhysical compiled = new(
            document,
            kind,
            position.PositionId,
            source,
            output,
            ledger,
            inverse,
            schema4Baseline.Placement,
            terrainReadback,
            schema4BaselineDocument.CanonicalSha256,
            schema4Baseline.OutputRow80Sha256,
            schema4Baseline.OutputModelSha256,
            schema4Baseline.DeterministicPlanSha256,
            changedByteCount,
            ownedRangeMapSha256,
            transactionSha256,
            protectedComplementSha256,
            deterministicPlanSha256,
            outputTexturePagesSha256,
            landingSha256,
            playerAnchorSha256,
            request.Source.TargetOverlaySha256,
            request.Source.GlobalExecutableSha256);

        if (!ApplyTransactional(compiled, compiled.CopySourceRow80(), reverse: false)
                .SequenceEqual(output) ||
            !ApplyTransactional(compiled, compiled.CopyOutputRow80(), reverse: true)
                .SequenceEqual(source))
        {
            throw new InvalidDataException(
                "The schema-5 public in-memory transaction failed its exact round trip.");
        }
        return compiled;
    }

    public static byte[] ApplyTransactional(
        Id65CompiledAuthoringRemotePadTerrainSetPhysical compiled,
        byte[] input,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(compiled);
        ArgumentNullException.ThrowIfNull(input);
        byte[] expectedInput = reverse
            ? compiled.CopyOutputRow80()
            : compiled.CopySourceRow80();
        if (!input.SequenceEqual(expectedInput))
            throw new InvalidDataException(reverse
                ? "The schema-5 rollback afterimage changed."
                : "The schema-5 locked-source preimage changed.");
        byte[] output = ApplyOwnedRanges(
            input,
            reverse ? compiled.InverseRanges : compiled.OwnedRanges);
        byte[] expectedOutput = reverse
            ? compiled.CopySourceRow80()
            : compiled.CopyOutputRow80();
        if (!output.SequenceEqual(expectedOutput))
            throw new InvalidDataException(
                "The schema-5 in-memory transaction output changed.");
        return output;
    }

    private static (
        Id65AuthoringEditorDocument Document,
        Id65RemotePadMobyKind Kind,
        Id65RemotePadGrassT107Position Position,
        Id65AuthoringTerrainSetManifest Manifest) ValidateDocument(
            Id65AuthoringEditorDocument supplied)
    {
        Id65AuthoringEditorDocument reparsed =
            Id65AuthoringEditorDocument.Parse(supplied.SerializeUtf8());
        if (supplied.CanonicalJson != reparsed.CanonicalJson ||
            supplied.CanonicalSha256 != reparsed.CanonicalSha256 ||
            supplied.ContentSha256 != reparsed.ContentSha256 ||
            supplied.SchemaVersion != reparsed.SchemaVersion ||
            supplied.ProfileId != reparsed.ProfileId ||
            supplied.RemotePadMobyKind != reparsed.RemotePadMobyKind ||
            supplied.RemotePadMobyPositionId != reparsed.RemotePadMobyPositionId ||
            !supplied.ActiveTerrainUnitIds.SequenceEqual(
                reparsed.ActiveTerrainUnitIds))
        {
            throw new InvalidDataException(
                "The supplied schema-5 document disagrees with its canonical reparse.");
        }
        if (reparsed.SchemaVersion !=
                Id65AuthoringEditorDocument.RemotePadMobyChoiceTerrainSetSchemaVersion ||
            reparsed.ProfileId !=
                Id65AuthoringEditorDocument.RemotePadMobyChoiceTerrainSetDocumentProfileId ||
            !reparsed.UsesRemotePadMobyChoice ||
            !reparsed.UsesRemotePadTerrainSet ||
            reparsed.UsesRemotePadT107Grid ||
            reparsed.RemotePadMobyKind is null ||
            reparsed.RemotePadMobyPositionId is null ||
            reparsed.MandatoryBaseFaceTextureId !=
                Id65AuthoringEditorDocument.PrivateMandatoryBaseFaceTextureId ||
            reparsed.Terrain.Count > 2 ||
            reparsed.MobyPlacements.Count > 1 ||
            reparsed.MobyPlacements.Any(item =>
                item.Id == Id65AuthoringMobyDependencyBundleCompiler.T88PlacementId) ||
            reparsed.ContainsNativePayloadBytes ||
            reparsed.ContainsCompiledAfterimage ||
            reparsed.WritesFileSystem ||
            reparsed.WritesDiscImage ||
            reparsed.WritesCue ||
            reparsed.AppIntegrated ||
            reparsed.CreateBinEnabled ||
            reparsed.RuntimeCandidateAuthorized ||
            reparsed.ReleaseIntegrated ||
            reparsed.Publishable)
        {
            throw new InvalidDataException(
                "The physical terrain-set compiler accepts only exact schema-5 T66 fresh-ID65 documents with T88 excluded.");
        }
        Id65RemotePadMobyKind kind = reparsed.RemotePadMobyKind.Value;
        string positionId = Id65RemotePadMobyChoiceCatalog.GetRequiredPositionId(
            reparsed.RemotePadMobyPositionId);
        Id65AuthoringTerrainSetManifest manifest =
            Id65AuthoringTerrainSetCatalog.Create(reparsed.ActiveTerrainUnitIds);
        Id65AuthoringEditorDocument expected =
            Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetExact(
                kind,
                positionId,
                manifest.ActiveUnits.Select(item => item.Id));
        if (reparsed.CanonicalJson != expected.CanonicalJson ||
            reparsed.CanonicalSha256 != expected.CanonicalSha256 ||
            reparsed.TerrainSetManifestSha256 != manifest.CanonicalSha256 ||
            Id65RemotePadMobyChoiceCatalog.CanonicalSha256 !=
                Id65RemotePadMobyChoiceCatalog.ExpectedCanonicalSha256 ||
            Id65RemotePadMobyChoiceCatalog.RuntimeAcceptancePassed ||
            Id65RemotePadMobyChoiceCatalog.RuntimeCandidateAuthorized ||
            Id65RemotePadMobyChoiceCatalog.PhysicalMaterializationEnabled ||
            Id65RemotePadMobyChoiceCatalog.NormalCreateBinAuthorized ||
            Id65RemotePadMobyChoiceCatalog.ReleasePublicationAuthorized)
        {
            throw new InvalidDataException(
                "The exact schema-5 document or fail-closed catalogs changed.");
        }
        return (
            reparsed,
            kind,
            Id65RemotePadGrassT107Catalog.GetRequiredPosition(positionId),
            manifest);
    }

    private static void ValidateLockedV3Provider(
        Id65AuthoringCompositeLockedSource source,
        UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan v3)
    {
        byte[] locked = source.CopyRow80();
        byte[] applied =
            UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction
                .ApplyTransactional(v3, v3.SourceData.ToArray(), reverse: false);
        byte[] rolledBack =
            UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction
                .ApplyTransactional(v3, v3.OutputData.ToArray(), reverse: true);
        if (source.Row80ByteLength != Row80ByteLength ||
            source.Row80Sha256 != Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 ||
            v3.ProfileId !=
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.ProfileId ||
            v3.SourceData.Length != Row80ByteLength ||
            v3.OutputData.Length != Row80ByteLength ||
            v3.SourceDataSha256 !=
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction
                    .ExpectedSourceDataSha256 ||
            v3.OutputDataSha256 !=
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction
                    .ExpectedOutputDataSha256 ||
            v3.OutputModelSha256 !=
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction
                    .ExpectedOutputModelSha256 ||
            Hash(v3.SourceData) != v3.SourceDataSha256 ||
            Hash(v3.OutputData) != v3.OutputDataSha256 ||
            !v3.SourceData.SequenceEqual(locked) ||
            !applied.SequenceEqual(v3.OutputData) ||
            !rolledBack.SequenceEqual(v3.SourceData) ||
            !v3.ByteInverseVerified ||
            !v3.ProtectedSubfilesPreserved ||
            v3.SourceImageMutationPossible ||
            v3.DisposableRuntimeCandidateAuthorized ||
            v3.PromotionAuthorized ||
            v3.NormalCreateBinEnabled)
        {
            throw new InvalidDataException(
                "The schema-5 physical compiler requires the exact locked v3 source/provider proof.");
        }
        RequireHash(
            Hash(v3.OutputData.AsSpan(ModelOffset, ModelLength)),
            v3.OutputModelSha256,
            "v3 provider output model");
    }

    private static void ValidateTerrainCompiles(
        Id65CompiledAuthoringTerrainSet empty,
        Id65CompiledAuthoringTerrainSet target,
        Id65AuthoringTerrainSetManifest manifest)
    {
        if (empty.Manifest.ActiveUnits.Count != 0 ||
            empty.ModelSha256 != Id65AuthoringTerrainSetCompiler.ExpectedV3ModelSha256 ||
            target.ManifestSha256 != manifest.CanonicalSha256 ||
            target.Units.Count != manifest.ActiveUnits.Count ||
            target.Capacity.UsedModelBytes + target.Capacity.ZeroTailBytes != ModelLength ||
            target.Capacity.ZeroTailBytes <
                Id65V2NativeTextureCompositionCompiler.RecordGrowthBytes ||
            !target.ImmutableLockedV3SourceVerified ||
            !target.SinglePassTargetRebuildVerified ||
            !target.ExactHpLpRowsVerified ||
            !target.NegativeCollisionWindingVerified ||
            !target.CollisionCellTreeReadbackVerified ||
            !target.ProtectedComplementPreserved ||
            target.WritesFileSystem ||
            target.WritesDiscImage ||
            target.MaterializerIntegrated ||
            target.CreateBinEnabled ||
            target.RuntimeCandidateAuthorized ||
            target.ReleaseIntegrated ||
            target.Publishable)
        {
            throw new InvalidDataException(
                "The exact four-state terrain compiler contract changed.");
        }
    }

    private static void ValidateSchema4Baseline(
        Id65CompiledAuthoringRemotePadMobyChoicePhysical baseline,
        Id65AuthoringEditorDocument expectedDocument,
        Id65AuthoringCompositeLockedSource source,
        Id65RemotePadMobyKind kind,
        Id65RemotePadGrassT107Position position)
    {
        if (baseline.InputDocumentSchemaVersion !=
                Id65AuthoringEditorDocument.RemotePadMobyChoiceSchemaVersion ||
            baseline.DocumentCanonicalSha256 != expectedDocument.CanonicalSha256 ||
            baseline.StructuralTerrainPresent ||
            baseline.MobyKind != kind ||
            baseline.Placement.Kind != kind ||
            baseline.Placement.PositionId != position.PositionId ||
            baseline.SourceRow80Sha256 != source.Row80Sha256 ||
            baseline.OutputModelSha256 !=
                Id65V2NativeTextureCompositionCompiler.ExpectedOutputModelSha256 ||
            baseline.TargetOverlaySha256 != source.TargetOverlaySha256 ||
            baseline.GlobalExecutableSha256 != source.GlobalExecutableSha256 ||
            !baseline.PureInMemory ||
            !baseline.ExactInverseVerified ||
            !baseline.EveryChangedByteOwned ||
            baseline.WritesFileSystem ||
            baseline.WritesDiscImage ||
            baseline.PhysicalMaterializationEnabled ||
            baseline.CreateBinEnabled ||
            baseline.RuntimeCandidateAuthorized ||
            baseline.ReleaseIntegrated ||
            baseline.Publishable)
        {
            throw new InvalidDataException(
                "The exact schema-4 no-terrain Moby baseline contract changed.");
        }
    }

    private static (byte[] Model, int MandatoryFaceOffset) ComposeT66TerrainModel(
        byte[] schema4BaseModel,
        Id65CompiledAuthoringTerrainSet emptyTerrain,
        Id65CompiledAuthoringTerrainSet targetTerrain)
    {
        IReadOnlyList<Id65AuthoringTerrainSetCompiler.ComponentState> empty =
            emptyTerrain.CopyComponents();
        IReadOnlyList<Id65AuthoringTerrainSetCompiler.ComponentState> target =
            targetTerrain.CopyComponents();
        string[] expectedNames =
        [
            "texture", "environment", "occlusion", "special-surface",
            "collision", "cyclorama", "portal-table", "particles", "sound"
        ];
        if (schema4BaseModel.Length != ModelLength ||
            empty.Count != expectedNames.Length ||
            target.Count != expectedNames.Length ||
            !empty.Select(item => item.Name).SequenceEqual(expectedNames) ||
            !target.Select(item => item.Name).SequenceEqual(expectedNames) ||
            empty[0].ByteLength != V3TextureComponentLength ||
            target[0].ByteLength != V3TextureComponentLength ||
            Hash(schema4BaseModel.AsSpan(0, T66TextureComponentLength)) !=
                Id65V2NativeTextureCompositionCompiler
                    .ExpectedOutputTextureComponentSha256)
        {
            throw new InvalidDataException(
                "The terrain/T66 component composition topology changed.");
        }

        byte[] emptyEnvironment = empty.Single(item => item.Name == "environment").Bytes;
        byte[] targetEnvironment = target.Single(item => item.Name == "environment").Bytes.ToArray();
        byte[] t66BaseEnvironment = schema4BaseModel.AsSpan(
            T66TextureComponentLength,
            emptyEnvironment.Length).ToArray();
        byte[] v3BaseHighFace = emptyEnvironment.AsSpan(emptyEnvironment.Length - 16, 16).ToArray();
        byte[] t66BaseHighFace = t66BaseEnvironment.AsSpan(
            t66BaseEnvironment.Length - 16,
            16).ToArray();
        int faceChangedBytes = CountChangedBytes(v3BaseHighFace, t66BaseHighFace);
        if (!EqualOutsideSpan(
                emptyEnvironment,
                t66BaseEnvironment,
                emptyEnvironment.Length - 16,
                16) ||
            faceChangedBytes != 1 ||
            (BinaryPrimitives.ReadUInt32LittleEndian(v3BaseHighFace.AsSpan(8, 4)) & 0x7F) != 25 ||
            (BinaryPrimitives.ReadUInt32LittleEndian(t66BaseHighFace.AsSpan(8, 4)) & 0x7F) != 66)
        {
            throw new InvalidDataException(
                "The canonical T25-to-T66 mandatory base-face delta changed.");
        }
        int faceRelative = FindUnique(targetEnvironment, v3BaseHighFace);
        t66BaseHighFace.CopyTo(targetEnvironment, faceRelative);

        byte[] model = new byte[ModelLength];
        schema4BaseModel.AsSpan(0, T66TextureComponentLength).CopyTo(model);
        int cursor = T66TextureComponentLength;
        foreach (Id65AuthoringTerrainSetCompiler.ComponentState component in target.Skip(1))
        {
            byte[] bytes = component.Name == "environment"
                ? targetEnvironment
                : component.Bytes;
            bytes.CopyTo(model, cursor);
            cursor += bytes.Length;
        }
        int expectedUsed = targetTerrain.Capacity.UsedModelBytes +
            Id65V2NativeTextureCompositionCompiler.RecordGrowthBytes;
        if (cursor != expectedUsed || model.AsSpan(cursor).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException(
                "The combined T66 terrain model used/tail boundary changed.");
        if (manifestIsEmpty(targetTerrain) && !model.SequenceEqual(schema4BaseModel))
            throw new InvalidDataException(
                "The empty terrain-set composition differs from the frozen schema-4 T66 model.");
        return (model, T66TextureComponentLength + faceRelative + 8);

        static bool manifestIsEmpty(Id65CompiledAuthoringTerrainSet compiled) =>
            compiled.Manifest.ActiveUnits.Count == 0;
    }

    private static void ValidateCombinedOutput(
        byte[] source,
        byte[] baseline,
        byte[] output,
        byte[] finalModel,
        Id65CompiledAuthoringTerrainSet targetTerrain,
        IReadOnlyList<Id65AuthoringTerrainUnitReadback> independentUnits,
        Id65CompiledAuthoringRemotePadMobyChoicePhysical schema4Baseline,
        Id65RemotePadMobyKind kind,
        Id65RemotePadGrassT107Position position,
        int mandatoryFaceOffset)
    {
        if (source.Length != Row80ByteLength ||
            baseline.Length != Row80ByteLength ||
            output.Length != Row80ByteLength ||
            finalModel.Length != ModelLength ||
            !EqualOutsideSpan(baseline, output, ModelOffset, ModelLength) ||
            !output.AsSpan(ModelOffset, ModelLength).SequenceEqual(finalModel) ||
            !output.AsSpan(
                    Id65V2NativeTextureCompositionCompiler.TexturePagesDataRelativeOffset,
                    Id65V2NativeTextureCompositionCompiler.TexturePagesByteLength)
                .SequenceEqual(baseline.AsSpan(
                    Id65V2NativeTextureCompositionCompiler.TexturePagesDataRelativeOffset,
                    Id65V2NativeTextureCompositionCompiler.TexturePagesByteLength)) ||
            Hash(finalModel.AsSpan(0, T66TextureComponentLength)) !=
                Id65V2NativeTextureCompositionCompiler
                    .ExpectedOutputTextureComponentSha256 ||
            (BinaryPrimitives.ReadUInt32LittleEndian(
                finalModel.AsSpan(mandatoryFaceOffset, 4)) & 0x7F) != 66 ||
            independentUnits.Count != targetTerrain.Units.Count ||
            !independentUnits.SequenceEqual(targetTerrain.Units) ||
            schema4Baseline.Placement.Kind != kind ||
            schema4Baseline.Placement.PositionId != position.PositionId)
        {
            throw new InvalidDataException(
                "The composed schema-5 terrain/Moby row failed exact independent readback.");
        }
        string selectedRow = Id65RemotePadMobyChoiceCatalog.GetSelectedExactRowSha256(
            kind,
            position.PositionId) ?? "none";
        string actualSelectedRow = kind switch
        {
            Id65RemotePadMobyKind.None => "none",
            Id65RemotePadMobyKind.GrassT107 => Hash(output.AsSpan(
                0x1D0000 + 0x170 + (107 * 0x58),
                0x58)),
            Id65RemotePadMobyKind.RedGemT21 => Hash(output.AsSpan(
                Id65AuthoringRemotePadMobyChoicePhysicalCompiler.ResidentT21RowOffset,
                Id65RemotePadRedGemT21Catalog.RowByteLength)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        if (selectedRow != actualSelectedRow)
            throw new InvalidDataException(
                "The composed schema-5 output changed the exact selected Moby row.");
    }

    private static byte[] BuildFinalSemanticV3View(
        byte[] finalModel,
        Id65CompiledAuthoringTerrainSet targetTerrain)
    {
        byte[] targetV3 = targetTerrain.CopyModel();
        int v3Used = targetTerrain.Capacity.UsedModelBytes;
        int t66Used = checked(v3Used +
            Id65V2NativeTextureCompositionCompiler.RecordGrowthBytes);
        if (targetV3.Length != ModelLength || finalModel.Length != ModelLength ||
            v3Used < V3TextureComponentLength || t66Used > ModelLength)
        {
            throw new InvalidDataException(
                "The final semantic-view model bounds changed.");
        }
        byte[] semanticV3 = new byte[ModelLength];
        targetV3.AsSpan(0, V3TextureComponentLength).CopyTo(semanticV3);
        finalModel.AsSpan(
                T66TextureComponentLength,
                t66Used - T66TextureComponentLength)
            .CopyTo(semanticV3.AsSpan(V3TextureComponentLength));
        if (semanticV3.AsSpan(v3Used).IndexOfAnyExcept((byte)0) >= 0 ||
            CountChangedBytes(targetV3, semanticV3) != 1)
        {
            throw new InvalidDataException(
                "The reconstructed final T66 terrain semantics differ from the canonical v3 target by more than the one mandatory T25-to-T66 base-face binding.");
        }
        return semanticV3;
    }

    private static string HashFinalEnvironment(
        byte[] finalModel,
        Id65CompiledAuthoringTerrainSet targetTerrain)
    {
        Id65AuthoringTerrainSetCompiler.ComponentState environment =
            targetTerrain.CopyComponents().Single(item => item.Name == "environment");
        return Hash(finalModel.AsSpan(
            T66TextureComponentLength,
            environment.ByteLength));
    }

    private static string HashFinalProtectedComponentSet(
        byte[] finalModel,
        Id65CompiledAuthoringTerrainSet targetTerrain)
    {
        string[] protectedNames =
        [
            "texture", "occlusion", "special-surface", "cyclorama",
            "portal-table", "particles", "sound"
        ];
        IReadOnlyList<Id65AuthoringTerrainSetCompiler.ComponentState> components =
            targetTerrain.CopyComponents();
        StringBuilder proof = new();
        foreach (string name in protectedNames)
        {
            if (name == "texture")
            {
                string textureHash = Hash(finalModel.AsSpan(
                    0,
                    T66TextureComponentLength));
                proof.Append(name).Append('|')
                    .Append(T66TextureComponentLength).Append('|')
                    .Append(textureHash).Append('\n');
                continue;
            }
            Id65AuthoringTerrainSetCompiler.ComponentState component =
                components.Single(item => item.Name == name);
            int finalOffset = checked(component.Offset +
                Id65V2NativeTextureCompositionCompiler.RecordGrowthBytes);
            ReadOnlySpan<byte> finalBytes = finalModel.AsSpan(
                finalOffset,
                component.ByteLength);
            if (!finalBytes.SequenceEqual(component.Bytes))
                throw new InvalidDataException(
                    "The final T66 protected terrain component " + name +
                    " changed instead of relocating intact.");
            proof.Append(name).Append('|').Append(component.ByteLength).Append('|')
                .Append(Hash(finalBytes)).Append('\n');
        }
        return Hash(Encoding.UTF8.GetBytes(proof.ToString()));
    }

    private static List<Id65AuthoringCompositeOwnedRange> BuildOwnedLedger(
        byte[] source,
        byte[] output,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> schema4Ranges)
    {
        List<Id65AuthoringCompositeOwnedRange> ranges = [];
        Id65AuthoringCompositeOwnedRange[] residentT21XyzRanges = schema4Ranges
            .Where(item => item.StableId ==
                Id65AuthoringRemotePadMobyChoicePhysicalCompiler
                    .RedGemOwnedRangeStableId)
            .ToArray();
        if (residentT21XyzRanges.Length > 1)
        {
            throw new InvalidDataException(
                "The schema-4 ownership ledger contains duplicate resident-T21 XYZ identities.");
        }
        Id65AuthoringCompositeOwnedRange? residentT21Xyz =
            residentT21XyzRanges.SingleOrDefault();
        if (residentT21Xyz is not null &&
            (residentT21Xyz.OwnerId != Id65RemotePadRedGemT21Catalog.PlacementId ||
             residentT21Xyz.DataRelativeOffset !=
                Id65AuthoringRemotePadMobyChoicePhysicalCompiler
                    .ResidentT21CoordinateOffset ||
             residentT21Xyz.ByteLength !=
                Id65AuthoringRemotePadMobyChoicePhysicalCompiler
                    .ResidentT21CoordinateByteLength))
        {
            throw new InvalidDataException(
                "The schema-4 resident-T21 ownership range lost its exact XYZ identity.");
        }
        int offset = 0;
        while (offset < source.Length)
        {
            if (residentT21Xyz is not null &&
                offset == residentT21Xyz.DataRelativeOffset)
            {
                int xyzLength = residentT21Xyz.ByteLength;
                ranges.Add(new(
                    ResidentT21XyzOwnedRangeStableId,
                    residentT21Xyz.OwnerId,
                    offset,
                    source.AsSpan(offset, xyzLength).ToArray(),
                    output.AsSpan(offset, xyzLength).ToArray()));
                offset = checked(offset + xyzLength);
                continue;
            }
            if (source[offset] == output[offset])
            {
                offset++;
                continue;
            }
            string owner = ResolveOwner(offset, schema4Ranges);
            int start = offset++;
            while (offset < source.Length &&
                   source[offset] != output[offset] &&
                   ResolveOwner(offset, schema4Ranges) == owner)
            {
                offset++;
            }
            int length = offset - start;
            ranges.Add(new(
                "schema5.row80." + start.ToString("x6", CultureInfo.InvariantCulture),
                owner,
                start,
                source.AsSpan(start, length).ToArray(),
                output.AsSpan(start, length).ToArray()));
        }
        return ranges;
    }

    private static string ResolveOwner(
        int offset,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> schema4Ranges)
    {
        if (offset >= ModelOffset && offset < ModelOffset + ModelLength)
            return ModelOwnerId;
        Id65AuthoringCompositeOwnedRange? owner = schema4Ranges.FirstOrDefault(item =>
            offset >= item.DataRelativeOffset &&
            offset < item.DataRelativeOffset + item.ByteLength);
        return owner?.OwnerId ?? throw new InvalidDataException(
            "A non-model schema-5 changed byte is not owned by the canonical schema-4 Moby/T66 baseline.");
    }

    private static void ValidateLedger(
        byte[] source,
        byte[] output,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ledger,
        Id65RemotePadMobyKind kind,
        string positionId)
    {
        Id65AuthoringCompositeOwnedRange[] residentT21Xyz = ledger
            .Where(item =>
                item.StableId == ResidentT21XyzOwnedRangeStableId ||
                item.OwnerId == Id65RemotePadRedGemT21Catalog.PlacementId)
            .ToArray();
        byte[] exactSourceT21 = Convert.FromHexString(
            Id65RemotePadRedGemT21Catalog.SourceRowExactHex);
        byte[] exactOutputT21 =
            Id65RemotePadRedGemT21Catalog.CreateExactRowBytes(positionId);
        byte[] exactSourceXyz = exactSourceT21.AsSpan(
            Id65RemotePadRedGemT21Catalog.RowCoordinateOffset,
            Id65RemotePadRedGemT21Catalog.RowCoordinateByteLength).ToArray();
        byte[] exactOutputXyz = exactOutputT21.AsSpan(
            Id65RemotePadRedGemT21Catalog.RowCoordinateOffset,
            Id65RemotePadRedGemT21Catalog.RowCoordinateByteLength).ToArray();
        if (ledger.Count == 0 ||
            ledger.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count() !=
                ledger.Count ||
            residentT21Xyz.Length !=
                (kind == Id65RemotePadMobyKind.RedGemT21 ? 1 : 0) ||
            residentT21Xyz.Any(item =>
                item.StableId != ResidentT21XyzOwnedRangeStableId ||
                item.DataRelativeOffset !=
                    Id65AuthoringRemotePadMobyChoicePhysicalCompiler
                        .ResidentT21CoordinateOffset ||
                item.ByteLength !=
                    Id65AuthoringRemotePadMobyChoicePhysicalCompiler
                        .ResidentT21CoordinateByteLength ||
                !item.CopyBefore().AsSpan().SequenceEqual(exactSourceXyz) ||
                !item.CopyAfter().AsSpan().SequenceEqual(exactOutputXyz)))
        {
            throw new InvalidDataException(
                "The schema-5 combined ownership ledger is empty, has duplicate identities, or lost exact resident-T21 XYZ ownership.");
        }
        int changed = 0;
        for (int index = 0; index < ledger.Count; index++)
        {
            Id65AuthoringCompositeOwnedRange range = ledger[index];
            if (range.DataRelativeOffset < 0 || range.ByteLength <= 0 ||
                range.DataRelativeOffset + (long)range.ByteLength > source.Length ||
                !source.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(range.CopyBefore()) ||
                !output.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(range.CopyAfter()) ||
                range.CopyBefore().SequenceEqual(range.CopyAfter()) ||
                Hash(range.CopyBefore()) != range.BeforeSha256 ||
                Hash(range.CopyAfter()) != range.AfterSha256 ||
                index > 0 && ledger[index - 1].DataRelativeOffset +
                    ledger[index - 1].ByteLength > range.DataRelativeOffset)
            {
                throw new InvalidDataException(
                    "A schema-5 combined owned range lost bounds, preimage, afterimage, hash, or nonoverlap identity.");
            }
            int rangeChanged = CountChangedBytes(
                range.CopyBefore(),
                range.CopyAfter());
            if (rangeChanged <= 0 ||
                range.OwnerId != Id65RemotePadRedGemT21Catalog.PlacementId &&
                rangeChanged != range.ByteLength)
            {
                throw new InvalidDataException(
                    "A schema-5 combined owned range contains unowned padding outside the exact resident-T21 XYZ field.");
            }
            changed = checked(changed + rangeChanged);
        }
        if (changed != CountChangedBytes(source, output))
            throw new InvalidDataException(
                "The schema-5 ledger does not account for every changed byte.");
    }

    private static byte[] ApplyOwnedRanges(
        byte[] input,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges)
    {
        byte[] output = input.ToArray();
        foreach (Id65AuthoringCompositeOwnedRange range in ranges)
        {
            if (!output.AsSpan(range.DataRelativeOffset, range.ByteLength)
                .SequenceEqual(range.CopyBefore()))
            {
                throw new InvalidDataException(
                    "Schema-5 transactional range " + range.StableId +
                    " lost its exact preimage.");
            }
            range.CopyAfter().CopyTo(output, range.DataRelativeOffset);
        }
        return output;
    }

    private static int FindUnique(byte[] haystack, byte[] needle)
    {
        int found = -1;
        for (int offset = 0; offset <= haystack.Length - needle.Length; offset++)
        {
            if (!haystack.AsSpan(offset, needle.Length).SequenceEqual(needle))
                continue;
            if (found >= 0)
                throw new InvalidDataException(
                    "The canonical base HP face is not unique in the terrain-set environment.");
            found = offset;
        }
        if (found < 0)
            throw new InvalidDataException(
                "The canonical base HP face is missing from the terrain-set environment.");
        return found;
    }

    private static bool EqualOutsideSpan(
        byte[] left,
        byte[] right,
        int offset,
        int length) =>
        left.Length == right.Length &&
        left.AsSpan(0, offset).SequenceEqual(right.AsSpan(0, offset)) &&
        left.AsSpan(offset + length).SequenceEqual(right.AsSpan(offset + length));

    private static int CountChangedBytes(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
            throw new InvalidDataException("Changed-byte inputs have different lengths.");
        int count = 0;
        for (int index = 0; index < left.Length; index++)
            if (left[index] != right[index]) count++;
        return count;
    }

    private static string HashOwnedRanges(
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges)
    {
        StringBuilder canonical = new();
        foreach (Id65AuthoringCompositeOwnedRange range in ranges)
        {
            canonical.Append(range.StableId).Append('|')
                .Append(range.OwnerId).Append('|')
                .Append(range.DataRelativeOffset.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(range.ByteLength.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(range.BeforeSha256).Append('|')
                .Append(range.AfterSha256).Append('\n');
        }
        return Hash(Encoding.UTF8.GetBytes(canonical.ToString()));
    }

    private static string HashTransaction(
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
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
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int cursor = 0;
        foreach (Id65AuthoringCompositeOwnedRange range in ranges)
        {
            if (range.DataRelativeOffset > cursor)
                hash.AppendData(bytes.AsSpan(cursor, range.DataRelativeOffset - cursor));
            cursor = range.DataRelativeOffset + range.ByteLength;
        }
        if (cursor < bytes.Length)
            hash.AppendData(bytes.AsSpan(cursor));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static void RequireHash(string actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new InvalidDataException(
                "The exact " + label + " SHA-256 changed: " + actual + ".");
    }

    internal static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));
}
