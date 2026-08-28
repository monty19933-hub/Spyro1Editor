using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

internal sealed record Id65AuthoringRemotePadMobyChoicePhysicalCompileRequest(
    Id65AuthoringEditorDocument Document,
    Id65AuthoringCompositeLockedSource Source,
    Id65V2NativeTextureWitness TextureWitness,
    Id65MobyDependencyBundleDescriptor GrassBundle,
    Id65AuthoringCompositeCompilerLimits? Limits = null);

internal sealed record Id65AuthoringRemotePadMobyChoicePhysicalPlacementReadback(
    Id65RemotePadMobyKind Kind,
    string PositionId,
    int PositionOrdinal,
    int EditorX,
    int EditorY,
    int EditorZ,
    int RawX,
    int RawY,
    int RawZ,
    int? TargetTrueIndex,
    ushort? ActorId,
    byte? YawByte,
    string SupportHandle,
    int SupportCollisionTrueIndex,
    string? ExactRowSha256,
    bool Present,
    bool ReusesResidentRow);

internal sealed class Id65CompiledAuthoringRemotePadMobyChoicePhysical
{
    private readonly byte[] _sourceRow80;
    private readonly byte[] _outputRow80;
    private readonly ReadOnlyCollection<Id65AuthoringCompositeOwnedRange> _ownedRanges;
    private readonly ReadOnlyCollection<Id65AuthoringCompositeOwnedRange> _inverseRanges;
    private readonly ReadOnlyCollection<Id65AuthoringCompositeOwnedRange> _additionalOwnedRanges;

    internal Id65CompiledAuthoringRemotePadMobyChoicePhysical(
        Id65AuthoringEditorDocument document,
        Id65RemotePadMobyKind kind,
        string schema3SurrogateCanonicalSha256,
        byte[] sourceRow80,
        byte[] outputRow80,
        IEnumerable<Id65AuthoringCompositeOwnedRange> ownedRanges,
        IEnumerable<Id65AuthoringCompositeOwnedRange> inverseRanges,
        IEnumerable<Id65AuthoringCompositeOwnedRange> additionalOwnedRanges,
        Id65AuthoringRemotePadMobyChoicePhysicalPlacementReadback placement,
        string baselineOutputRow80Sha256,
        string baselineOutputModelSha256,
        string baselineOwnedRangeMapSha256,
        string baselineTransactionSha256,
        string baselineDeterministicPlanSha256,
        int baselineOwnedRangeCount,
        int changedByteCount,
        string ownedRangeMapSha256,
        string transactionSha256,
        string protectedComplementSha256,
        string deterministicPlanSha256,
        string sourceT21RowSha256,
        string outputT21RowSha256,
        string targetOverlaySha256,
        string globalExecutableSha256)
    {
        Document = document;
        DocumentCanonicalSha256 = document.CanonicalSha256;
        MobyKind = kind;
        Schema3SurrogateCanonicalSha256 = schema3SurrogateCanonicalSha256;
        StructuralTerrainPresent = document.Terrain.Count == 1;
        _sourceRow80 = sourceRow80.ToArray();
        _outputRow80 = outputRow80.ToArray();
        _ownedRanges = Array.AsReadOnly(
            ownedRanges.Select(item => item.DeepCopy()).ToArray());
        _inverseRanges = Array.AsReadOnly(
            inverseRanges.Select(item => item.DeepCopy()).ToArray());
        _additionalOwnedRanges = Array.AsReadOnly(
            additionalOwnedRanges.Select(item => item.DeepCopy()).ToArray());
        Placement = placement;
        SourceRow80Sha256 =
            Id65AuthoringRemotePadMobyChoicePhysicalCompiler.Hash(_sourceRow80);
        OutputRow80Sha256 =
            Id65AuthoringRemotePadMobyChoicePhysicalCompiler.Hash(_outputRow80);
        OutputModelSha256 = Id65AuthoringRemotePadMobyChoicePhysicalCompiler.Hash(
            _outputRow80.AsSpan(
                Id65AuthoringRemotePadCompositeCompiler.ModelOffset,
                Id65AuthoringRemotePadCompositeCompiler.ModelLength));
        BaselineOutputRow80Sha256 = baselineOutputRow80Sha256;
        BaselineOutputModelSha256 = baselineOutputModelSha256;
        BaselineOwnedRangeMapSha256 = baselineOwnedRangeMapSha256;
        BaselineTransactionSha256 = baselineTransactionSha256;
        BaselineDeterministicPlanSha256 = baselineDeterministicPlanSha256;
        BaselineOwnedRangeCount = baselineOwnedRangeCount;
        ChangedByteCount = changedByteCount;
        OwnedRangeMapSha256 = ownedRangeMapSha256;
        TransactionSha256 = transactionSha256;
        ProtectedComplementSha256 = protectedComplementSha256;
        DeterministicPlanSha256 = deterministicPlanSha256;
        SourceT21RowSha256 = sourceT21RowSha256;
        OutputT21RowSha256 = outputT21RowSha256;
        TargetOverlaySha256 = targetOverlaySha256;
        GlobalExecutableSha256 = globalExecutableSha256;
    }

    public string ProfileId =>
        Id65AuthoringRemotePadMobyChoicePhysicalCompiler.ProfileId;
    public int SchemaVersion =>
        Id65AuthoringRemotePadMobyChoicePhysicalCompiler.SchemaVersion;
    public int InputDocumentSchemaVersion => Document.SchemaVersion;
    public Id65AuthoringEditorDocument Document { get; }
    public string DocumentCanonicalSha256 { get; }
    public Id65RemotePadMobyKind MobyKind { get; }
    public bool StructuralTerrainPresent { get; }
    public bool Schema3SurrogateGrassT107Present =>
        MobyKind == Id65RemotePadMobyKind.GrassT107;
    public string Schema3SurrogateCanonicalSha256 { get; }
    public string CatalogCanonicalSha256 =>
        Id65RemotePadMobyChoiceCatalog.CanonicalSha256;
    public string SourceRow80Sha256 { get; }
    public string OutputRow80Sha256 { get; }
    public string OutputModelSha256 { get; }
    public string BaselineOutputRow80Sha256 { get; }
    public string BaselineOutputModelSha256 { get; }
    public string BaselineOwnedRangeMapSha256 { get; }
    public string BaselineTransactionSha256 { get; }
    public string BaselineDeterministicPlanSha256 { get; }
    public int BaselineOwnedRangeCount { get; }
    public IReadOnlyList<Id65AuthoringCompositeOwnedRange> OwnedRanges => _ownedRanges;
    public IReadOnlyList<Id65AuthoringCompositeOwnedRange> InverseRanges => _inverseRanges;
    public IReadOnlyList<Id65AuthoringCompositeOwnedRange> AdditionalOwnedRanges =>
        _additionalOwnedRanges;
    public Id65AuthoringRemotePadMobyChoicePhysicalPlacementReadback Placement { get; }
    public int ChangedByteCount { get; }
    public string OwnedRangeMapSha256 { get; }
    public string TransactionSha256 { get; }
    public string ProtectedComplementSha256 { get; }
    public string DeterministicPlanSha256 { get; }
    public string SourceT21RowSha256 { get; }
    public string OutputT21RowSha256 { get; }
    public string TargetOverlaySha256 { get; }
    public string GlobalExecutableSha256 { get; }

    public bool PureInMemory => true;
    public bool BuildsFromLockedSourceThroughFrozenSchema3Baseline => true;
    public bool ExactInverseVerified => true;
    public bool EveryChangedByteOwned => true;
    public bool ProtectedComplementVerified => true;
    public bool OutputModelPreservedFromFrozenSchema3Baseline => true;
    public bool ContainsOutputRow80Bytes => true;
    public bool AcceptsCallerCompiledAfterimages => false;
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
/// Pure schema-4 row-80 compiler. None and Grass are projected through the
/// frozen schema-3 compiler without changing that compiler. Red Gem starts from
/// the same no-Grass schema-3 baseline and adds one exact, non-overlapping,
/// resident-T21 XYZ ownership range. No filesystem materializer, Create BIN,
/// runtime candidate, or release route is exposed.
/// </summary>
internal static class Id65AuthoringRemotePadMobyChoicePhysicalCompiler
{
    public const string ProfileId =
        "id65-authoring-remote-pad-moby-choice-schema4-row80-in-memory-p1";
    public const int SchemaVersion = 1;
    public const int Row80ByteLength = 0x2E2000;
    public const int ResidentT21RowOffset = 0x1D08A8;
    public const int ResidentT21CoordinateOffset =
        ResidentT21RowOffset + Id65RemotePadRedGemT21Catalog.RowCoordinateOffset;
    public const int ResidentT21CoordinateByteLength =
        Id65RemotePadRedGemT21Catalog.RowCoordinateByteLength;
    public const string RedGemOwnedRangeStableId =
        "placement.remote-pad.red-gem.t21.xyz";

    static Id65AuthoringRemotePadMobyChoicePhysicalCompiler()
    {
        if (ResidentT21RowOffset != checked((int)(
                Id65RemotePadRedGemT21Catalog.LockedId65ResidentRowWadOffset -
                Id65RemotePadRedGemT21Catalog.LockedId65DataWadOffset)) ||
            Id65RemotePadRedGemT21Catalog.RowByteLength != 0x58 ||
            ResidentT21CoordinateByteLength != 0x0C ||
            ResidentT21CoordinateOffset != 0x1D08B4 ||
            ResidentT21RowOffset + Id65RemotePadRedGemT21Catalog.RowByteLength >
                Row80ByteLength)
        {
            throw new InvalidDataException(
                "The frozen resident-T21 row-80 address contract changed.");
        }
    }

    public static Id65CompiledAuthoringRemotePadMobyChoicePhysical Compile(
        Id65AuthoringRemotePadMobyChoicePhysicalCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Document);
        ArgumentNullException.ThrowIfNull(request.Source);
        ArgumentNullException.ThrowIfNull(request.TextureWitness);
        ArgumentNullException.ThrowIfNull(request.GrassBundle);

        (Id65AuthoringEditorDocument document,
            Id65RemotePadMobyKind kind,
            Id65RemotePadGrassT107Position position,
            bool structuralTerrainPresent) = ValidateDocument(request.Document);

        bool surrogateGrassPresent = kind == Id65RemotePadMobyKind.GrassT107;
        Id65AuthoringEditorDocument schema3Surrogate =
            Id65AuthoringEditorDocument.CreateRemotePadGridExact(
                position.PositionId,
                includeOptionalTerrain: structuralTerrainPresent,
                placementIds: surrogateGrassPresent
                    ? [Id65RemotePadGrassT107Catalog.PlacementId]
                    : [],
                mandatoryBaseFaceTextureId:
                    Id65AuthoringEditorDocument.PrivateMandatoryBaseFaceTextureId);
        Id65CompiledAuthoringRemotePadComposite baseline =
            Id65AuthoringRemotePadCompositeCompiler.Compile(new(
                schema3Surrogate,
                request.Source,
                request.TextureWitness,
                request.GrassBundle,
                request.Limits));
        ValidateBaseline(
            baseline,
            schema3Surrogate,
            request.Source,
            structuralTerrainPresent,
            surrogateGrassPresent);

        byte[] source = baseline.CopySourceRow80();
        byte[] baselineOutput = baseline.CopyOutputRow80();
        byte[] sourceT21Row = source.AsSpan(
            ResidentT21RowOffset,
            Id65RemotePadRedGemT21Catalog.RowByteLength).ToArray();
        byte[] exactSourceT21Row =
            Convert.FromHexString(Id65RemotePadRedGemT21Catalog.SourceRowExactHex);
        RequireHash(
            Hash(exactSourceT21Row),
            Id65RemotePadRedGemT21Catalog.SourceRowSha256,
            "catalog resident-T21 source row");
        if (!sourceT21Row.SequenceEqual(exactSourceT21Row) ||
            !baselineOutput.AsSpan(
                    ResidentT21RowOffset,
                    Id65RemotePadRedGemT21Catalog.RowByteLength)
                .SequenceEqual(exactSourceT21Row))
        {
            throw new InvalidDataException(
                "The locked source or frozen schema-3 baseline no longer preserves the full resident T21 row.");
        }

        List<Id65AuthoringCompositeOwnedRange> ledger = baseline.OwnedRanges
            .Select(item => item.DeepCopy())
            .ToList();
        List<Id65AuthoringCompositeOwnedRange> additional = [];
        byte[]? selectedExactRow = null;
        if (kind == Id65RemotePadMobyKind.RedGemT21)
        {
            selectedExactRow =
                Id65RemotePadRedGemT21Catalog.CreateExactRowBytes(position.PositionId);
            RequireHash(
                Hash(selectedExactRow),
                Id65RemotePadRedGemT21Catalog
                    .GetRequiredPosition(position.PositionId).OutputRowSha256,
                "schema-4 selected resident-T21 output row");
            Id65AuthoringCompositeOwnedRange redGemCoordinates = new(
                RedGemOwnedRangeStableId,
                Id65RemotePadRedGemT21Catalog.PlacementId,
                ResidentT21CoordinateOffset,
                source.AsSpan(
                    ResidentT21CoordinateOffset,
                    ResidentT21CoordinateByteLength).ToArray(),
                selectedExactRow.AsSpan(
                    Id65RemotePadRedGemT21Catalog.RowCoordinateOffset,
                    ResidentT21CoordinateByteLength).ToArray());
            if (ledger.Any(item => RangesOverlap(
                    item.DataRelativeOffset,
                    item.ByteLength,
                    redGemCoordinates.DataRelativeOffset,
                    redGemCoordinates.ByteLength)))
            {
                throw new InvalidDataException(
                    "The resident-T21 XYZ range overlaps the frozen schema-3 ownership ledger.");
            }
            ledger.Add(redGemCoordinates);
            additional.Add(redGemCoordinates.DeepCopy());
        }
        ledger = ledger
            .OrderBy(item => item.DataRelativeOffset)
            .ThenBy(item => item.StableId, StringComparer.Ordinal)
            .ToList();
        ValidateLedger(source, ledger, baseline, kind);

        byte[] output = ApplyOwnedRanges(source, ledger);
        Id65AuthoringCompositeOwnedRange[] inverse = ledger
            .Reverse<Id65AuthoringCompositeOwnedRange>()
            .Select(item => new Id65AuthoringCompositeOwnedRange(
                item.StableId,
                item.OwnerId,
                item.DataRelativeOffset,
                item.CopyAfter(),
                item.CopyBefore()))
            .ToArray();
        byte[] rolledBack = ApplyOwnedRanges(output, inverse);
        if (!rolledBack.SequenceEqual(source))
        {
            throw new InvalidDataException(
                "The schema-4 row-80 transaction did not invert exactly.");
        }

        VerifyOutput(
            source,
            baselineOutput,
            output,
            exactSourceT21Row,
            selectedExactRow,
            baseline,
            kind);
        VerifyEveryChangedByteOwned(source, output, ledger);
        string protectedSource = HashProtectedComplement(source, ledger);
        RequireHash(
            HashProtectedComplement(output, ledger),
            protectedSource,
            "schema-4 protected complement");
        string ownedRangeMapSha256 = HashOwnedRanges(ledger);
        string transactionSha256 = HashTransaction(ledger);
        int changedByteCount = CountChangedBytes(source, output);
        string selectedRowIdentity =
            Id65RemotePadMobyChoiceCatalog.GetSelectedExactRowSha256(
                kind,
                position.PositionId) ?? "none";
        string deterministicPlanSha256 = Hash(Encoding.UTF8.GetBytes(string.Join('\n',
        [
            ProfileId,
            SchemaVersion.ToString(CultureInfo.InvariantCulture),
            document.CanonicalSha256,
            Id65RemotePadMobyChoiceCatalog.CanonicalSha256,
            Id65RemotePadMobyChoiceCatalog.GetKindId(kind),
            position.PositionId,
            selectedRowIdentity,
            schema3Surrogate.CanonicalSha256,
            baseline.OutputRow80Sha256,
            baseline.OutputModelSha256,
            baseline.OwnedRangeMapSha256,
            baseline.TransactionSha256,
            baseline.DeterministicPlanSha256,
            Hash(source),
            Hash(output),
            changedByteCount.ToString(CultureInfo.InvariantCulture),
            ledger.Count.ToString(CultureInfo.InvariantCulture),
            additional.Count.ToString(CultureInfo.InvariantCulture),
            ownedRangeMapSha256,
            transactionSha256,
            protectedSource
        ])));

        Id65AuthoringRemotePadMobyChoicePhysicalPlacementReadback readback =
            BuildPlacementReadback(kind, position);
        Id65CompiledAuthoringRemotePadMobyChoicePhysical compiled = new(
            document,
            kind,
            schema3Surrogate.CanonicalSha256,
            source,
            output,
            ledger,
            inverse,
            additional,
            readback,
            baseline.OutputRow80Sha256,
            baseline.OutputModelSha256,
            baseline.OwnedRangeMapSha256,
            baseline.TransactionSha256,
            baseline.DeterministicPlanSha256,
            baseline.OwnedRanges.Count,
            changedByteCount,
            ownedRangeMapSha256,
            transactionSha256,
            protectedSource,
            deterministicPlanSha256,
            Hash(sourceT21Row),
            Hash(output.AsSpan(
                ResidentT21RowOffset,
                Id65RemotePadRedGemT21Catalog.RowByteLength)),
            request.Source.TargetOverlaySha256,
            request.Source.GlobalExecutableSha256);

        byte[] applied = ApplyTransactional(
            compiled,
            compiled.CopySourceRow80(),
            reverse: false);
        byte[] inverseApplied = ApplyTransactional(
            compiled,
            applied,
            reverse: true);
        if (!applied.SequenceEqual(output) || !inverseApplied.SequenceEqual(source))
        {
            throw new InvalidDataException(
                "The schema-4 compiled row-80 transaction failed its exact round trip.");
        }
        return compiled;
    }

    public static byte[] ApplyTransactional(
        Id65CompiledAuthoringRemotePadMobyChoicePhysical compiled,
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
                ? "The schema-4 row-80 rollback afterimage changed."
                : "The schema-4 row-80 locked-source preimage changed.");
        }
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges = reverse
            ? compiled.InverseRanges
            : compiled.OwnedRanges;
        byte[] output = ApplyOwnedRanges(input, ranges);
        byte[] expectedOutput = reverse
            ? compiled.CopySourceRow80()
            : compiled.CopyOutputRow80();
        if (!output.SequenceEqual(expectedOutput))
        {
            throw new InvalidDataException(
                "The schema-4 row-80 transaction output changed.");
        }
        return output;
    }

    private static (
        Id65AuthoringEditorDocument Document,
        Id65RemotePadMobyKind Kind,
        Id65RemotePadGrassT107Position Position,
        bool StructuralTerrainPresent) ValidateDocument(
        Id65AuthoringEditorDocument supplied)
    {
        byte[] canonical = supplied.SerializeUtf8();
        Id65AuthoringEditorDocument reparsed =
            Id65AuthoringEditorDocument.Parse(canonical);
        if (supplied.CanonicalJson != reparsed.CanonicalJson ||
            supplied.CanonicalSha256 != reparsed.CanonicalSha256 ||
            supplied.ContentSha256 != reparsed.ContentSha256 ||
            supplied.SchemaVersion != reparsed.SchemaVersion ||
            supplied.ProfileId != reparsed.ProfileId ||
            supplied.RemotePadMobyKind != reparsed.RemotePadMobyKind ||
            supplied.RemotePadMobyPositionId != reparsed.RemotePadMobyPositionId)
        {
            throw new InvalidDataException(
                "The supplied schema-4 document disagrees with its canonical reparse.");
        }
        if (reparsed.SchemaVersion !=
                Id65AuthoringEditorDocument.RemotePadMobyChoiceSchemaVersion ||
            reparsed.ProfileId !=
                Id65AuthoringEditorDocument.RemotePadMobyChoiceDocumentProfileId ||
            !reparsed.UsesRemotePadMobyChoice ||
            reparsed.UsesRemotePadT107Grid ||
            reparsed.RemotePadMobyKind is null ||
            reparsed.RemotePadMobyPositionId is null ||
            reparsed.MandatoryBaseFaceTextureId !=
                Id65RemotePadMobyChoiceCatalog.MandatoryBaseFaceTextureId ||
            reparsed.Terrain.Count > 1 ||
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
                "P1 supports only exact schema-4 T66 None/Grass T107/Red Gem T21 documents with optional structural terrain and T88 excluded.");
        }

        Id65RemotePadMobyKind kind = reparsed.RemotePadMobyKind.Value;
        string positionId = Id65RemotePadMobyChoiceCatalog.GetRequiredPositionId(
            reparsed.RemotePadMobyPositionId);
        bool structuralTerrainPresent = reparsed.Terrain.Count == 1;
        Id65AuthoringEditorDocument expected =
            Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceExact(
                kind,
                positionId,
                includeOptionalTerrain: structuralTerrainPresent);
        if (reparsed.CanonicalJson != expected.CanonicalJson ||
            reparsed.CanonicalSha256 != expected.CanonicalSha256 ||
            Id65RemotePadMobyChoiceCatalog.CanonicalSha256 !=
                Id65RemotePadMobyChoiceCatalog.ExpectedCanonicalSha256 ||
            Id65RemotePadMobyChoiceCatalog.RuntimeAcceptancePassed ||
            Id65RemotePadMobyChoiceCatalog.RuntimeCandidateAuthorized ||
            Id65RemotePadMobyChoiceCatalog.PhysicalMaterializationEnabled ||
            Id65RemotePadMobyChoiceCatalog.NormalCreateBinAuthorized ||
            Id65RemotePadMobyChoiceCatalog.ReleasePublicationAuthorized)
        {
            throw new InvalidDataException(
                "The exact schema-4 document or fail-closed choice catalog changed.");
        }
        return (
            reparsed,
            kind,
            Id65RemotePadGrassT107Catalog.GetRequiredPosition(positionId),
            structuralTerrainPresent);
    }

    private static void ValidateBaseline(
        Id65CompiledAuthoringRemotePadComposite baseline,
        Id65AuthoringEditorDocument surrogate,
        Id65AuthoringCompositeLockedSource source,
        bool structuralTerrainPresent,
        bool surrogateGrassPresent)
    {
        int expectedRangeCount = surrogateGrassPresent
            ? Id65AuthoringRemotePadCompositeCompiler.ExpectedOwnedRangeCount
            : Id65AuthoringRemotePadCompositeCompiler.ExpectedOwnedRangeCountWithoutT107;
        string expectedModelSha256 = structuralTerrainPresent
            ? Id65AuthoringRemotePadCompositeCompiler.ExpectedOutputModelSha256
            : Id65V2NativeTextureCompositionCompiler.ExpectedOutputModelSha256;
        if (baseline.ProfileId != Id65AuthoringRemotePadCompositeCompiler.ProfileId ||
            baseline.Document.CanonicalSha256 != surrogate.CanonicalSha256 ||
            baseline.Document.SchemaVersion !=
                Id65AuthoringEditorDocument.RemotePadGridSchemaVersion ||
            baseline.StructuralTerrainPresent != structuralTerrainPresent ||
            baseline.GrassT107Present != surrogateGrassPresent ||
            baseline.Placement.Present != surrogateGrassPresent ||
            baseline.SourceRow80Sha256 !=
                Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 ||
            baseline.OutputModelSha256 != expectedModelSha256 ||
            baseline.TargetOverlaySha256 != source.TargetOverlaySha256 ||
            baseline.GlobalExecutableSha256 != source.GlobalExecutableSha256 ||
            baseline.OwnedRanges.Count != expectedRangeCount ||
            baseline.InverseRanges.Count != expectedRangeCount ||
            baseline.WritesFileSystem ||
            baseline.WritesDiscImage ||
            baseline.WritesCue ||
            baseline.AppIntegrated ||
            baseline.CreateBinEnabled ||
            baseline.NormalCreateBinEnabled ||
            baseline.RuntimeCandidateAuthorized ||
            baseline.ReleaseIntegrated ||
            baseline.PromotionAuthorized ||
            baseline.Publishable)
        {
            throw new InvalidDataException(
                "The frozen schema-3 surrogate baseline contract changed.");
        }
    }

    private static void ValidateLedger(
        byte[] source,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ledger,
        Id65CompiledAuthoringRemotePadComposite baseline,
        Id65RemotePadMobyKind kind)
    {
        int expectedAdditional = kind == Id65RemotePadMobyKind.RedGemT21 ? 1 : 0;
        if (ledger.Count != baseline.OwnedRanges.Count + expectedAdditional ||
            ledger.Select(item => item.StableId)
                .Distinct(StringComparer.Ordinal).Count() != ledger.Count ||
            ledger.Count(item => item.StableId == RedGemOwnedRangeStableId) !=
                expectedAdditional ||
            ledger.Count(item => item.OwnerId ==
                Id65RemotePadRedGemT21Catalog.PlacementId) != expectedAdditional)
        {
            throw new InvalidDataException(
                "The schema-4 ownership ledger did not add exactly the selected Red Gem XYZ range.");
        }

        for (int index = 0; index < ledger.Count; index++)
        {
            Id65AuthoringCompositeOwnedRange range = ledger[index];
            byte[] before = range.CopyBefore();
            byte[] after = range.CopyAfter();
            if (range.DataRelativeOffset < 0 ||
                range.ByteLength <= 0 ||
                range.DataRelativeOffset + (long)range.ByteLength > Row80ByteLength ||
                before.Length != range.ByteLength ||
                after.Length != range.ByteLength ||
                Hash(before) != range.BeforeSha256 ||
                Hash(after) != range.AfterSha256 ||
                !source.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(before) ||
                before.SequenceEqual(after) ||
                index > 0 && RangesOverlap(
                    ledger[index - 1].DataRelativeOffset,
                    ledger[index - 1].ByteLength,
                    range.DataRelativeOffset,
                    range.ByteLength))
            {
                throw new InvalidDataException(
                    "Schema-4 owned range " + range.StableId +
                    " lost bounds, hash, source preimage, change, or nonoverlap identity.");
            }
        }

        Id65AuthoringCompositeOwnedRange? redGem = ledger.SingleOrDefault(
            item => item.StableId == RedGemOwnedRangeStableId);
        if (kind == Id65RemotePadMobyKind.RedGemT21 &&
            (redGem is null ||
             redGem.OwnerId != Id65RemotePadRedGemT21Catalog.PlacementId ||
             redGem.DataRelativeOffset != ResidentT21CoordinateOffset ||
             redGem.ByteLength != ResidentT21CoordinateByteLength))
        {
            throw new InvalidDataException(
                "The schema-4 Red Gem ownership range is not exact resident-T21 XYZ-only ownership.");
        }
    }

    private static void VerifyOutput(
        byte[] source,
        byte[] baselineOutput,
        byte[] output,
        byte[] exactSourceT21Row,
        byte[]? selectedExactRow,
        Id65CompiledAuthoringRemotePadComposite baseline,
        Id65RemotePadMobyKind kind)
    {
        if (source.Length != Row80ByteLength ||
            baselineOutput.Length != Row80ByteLength ||
            output.Length != Row80ByteLength ||
            Hash(output.AsSpan(
                Id65AuthoringRemotePadCompositeCompiler.ModelOffset,
                Id65AuthoringRemotePadCompositeCompiler.ModelLength)) !=
                    baseline.OutputModelSha256)
        {
            throw new InvalidDataException(
                "The schema-4 row-80 length or frozen schema-3 model preservation changed.");
        }

        if (kind != Id65RemotePadMobyKind.RedGemT21)
        {
            if (selectedExactRow is not null ||
                !output.SequenceEqual(baselineOutput) ||
                !output.AsSpan(
                        ResidentT21RowOffset,
                        Id65RemotePadRedGemT21Catalog.RowByteLength)
                    .SequenceEqual(exactSourceT21Row))
            {
                throw new InvalidDataException(
                    "Schema-4 None/Grass changed the frozen schema-3 result or resident T21 row.");
            }
            return;
        }

        if (selectedExactRow is null ||
            selectedExactRow.Length != Id65RemotePadRedGemT21Catalog.RowByteLength ||
            !output.AsSpan(
                    ResidentT21RowOffset,
                    Id65RemotePadRedGemT21Catalog.RowByteLength)
                .SequenceEqual(selectedExactRow) ||
            !EqualOutsideSpan(
                baselineOutput,
                output,
                ResidentT21CoordinateOffset,
                ResidentT21CoordinateByteLength) ||
            !exactSourceT21Row.AsSpan(
                    0,
                    Id65RemotePadRedGemT21Catalog.RowCoordinateOffset)
                .SequenceEqual(selectedExactRow.AsSpan(
                    0,
                    Id65RemotePadRedGemT21Catalog.RowCoordinateOffset)) ||
            !exactSourceT21Row.AsSpan(
                    Id65RemotePadRedGemT21Catalog.RowCoordinateOffset +
                        ResidentT21CoordinateByteLength)
                .SequenceEqual(selectedExactRow.AsSpan(
                    Id65RemotePadRedGemT21Catalog.RowCoordinateOffset +
                        ResidentT21CoordinateByteLength)))
        {
            throw new InvalidDataException(
                "The schema-4 Red Gem output is not the exact catalog row with XYZ-only mutation.");
        }
    }

    private static Id65AuthoringRemotePadMobyChoicePhysicalPlacementReadback
        BuildPlacementReadback(
            Id65RemotePadMobyKind kind,
            Id65RemotePadGrassT107Position position)
    {
        (int? TargetTrueIndex, ushort? ActorId, byte? YawByte,
            string? ExactRowSha256, bool Present, bool ReusesResidentRow) values =
            kind switch
            {
                Id65RemotePadMobyKind.None =>
                    (null, null, null, null, false, false),
                Id65RemotePadMobyKind.GrassT107 =>
                    (Id65RemotePadGrassT107Catalog.TargetTrueIndex,
                     Id65RemotePadGrassT107Catalog.ActorId,
                     Id65RemotePadGrassT107Catalog.YawByte,
                     position.RowSha256,
                     true,
                     false),
                Id65RemotePadMobyKind.RedGemT21 =>
                    (Id65RemotePadRedGemT21Catalog.TargetTrueIndex,
                     Id65RemotePadRedGemT21Catalog.ActorClass,
                     null,
                     Id65RemotePadRedGemT21Catalog
                         .GetRequiredPosition(position.PositionId).OutputRowSha256,
                     true,
                     true),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
        return new(
            kind,
            position.PositionId,
            position.Ordinal,
            position.EditorX,
            position.EditorY,
            position.EditorZ,
            position.RawX,
            position.RawY,
            position.RawZ,
            values.TargetTrueIndex,
            values.ActorId,
            values.YawByte,
            Id65RemotePadGrassT107Catalog.SupportHandle,
            Id65RemotePadGrassT107Catalog.SupportCollisionTrueIndex,
            values.ExactRowSha256,
            values.Present,
            values.ReusesResidentRow);
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
                    "Schema-4 transactional range " + range.StableId +
                    " lost its exact preimage.");
            }
            range.CopyAfter().CopyTo(output, range.DataRelativeOffset);
        }
        return output;
    }

    private static void VerifyEveryChangedByteOwned(
        byte[] source,
        byte[] output,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges)
    {
        if (source.Length != output.Length)
            throw new InvalidDataException(
                "Schema-4 changed-byte inputs have different lengths.");
        int rangeIndex = 0;
        for (int offset = 0; offset < source.Length; offset++)
        {
            while (rangeIndex < ranges.Count &&
                   ranges[rangeIndex].DataRelativeOffset +
                        ranges[rangeIndex].ByteLength <= offset)
            {
                rangeIndex++;
            }
            bool owned = rangeIndex < ranges.Count &&
                ranges[rangeIndex].DataRelativeOffset <= offset &&
                offset < ranges[rangeIndex].DataRelativeOffset +
                    ranges[rangeIndex].ByteLength;
            if (source[offset] != output[offset] && !owned)
            {
                throw new InvalidDataException(
                    "Schema-4 changed byte 0x" +
                    offset.ToString("X", CultureInfo.InvariantCulture) +
                    " escaped the ownership ledger.");
            }
        }
    }

    private static string HashOwnedRanges(
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges)
    {
        StringBuilder text = new();
        foreach (Id65AuthoringCompositeOwnedRange range in ranges)
        {
            text.Append(range.StableId).Append('|')
                .Append(range.OwnerId).Append('|')
                .Append(range.DataRelativeOffset.ToString(
                    "X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(range.ByteLength).Append('|')
                .Append(range.BeforeSha256).Append('|')
                .Append(range.AfterSha256).Append('\n');
        }
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static string HashTransaction(
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges)
    {
        using IncrementalHash hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (Id65AuthoringCompositeOwnedRange range in ranges)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(
                range.StableId + "\n" + range.OwnerId + "\n"));
            hash.AppendData(UInt32(checked((uint)range.DataRelativeOffset)));
            hash.AppendData(UInt32(checked((uint)range.ByteLength)));
            hash.AppendData(range.CopyBefore());
            hash.AppendData(range.CopyAfter());
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
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
            if (cursor < range.DataRelativeOffset)
            {
                hash.AppendData(bytes.AsSpan(
                    cursor,
                    range.DataRelativeOffset - cursor));
            }
            cursor = checked(range.DataRelativeOffset + range.ByteLength);
        }
        if (cursor < bytes.Length)
            hash.AppendData(bytes.AsSpan(cursor));
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static int CountChangedBytes(
        ReadOnlySpan<byte> source,
        ReadOnlySpan<byte> output)
    {
        if (source.Length != output.Length)
            throw new InvalidDataException(
                "Schema-4 changed-byte inputs have different lengths.");
        int changed = 0;
        for (int index = 0; index < source.Length; index++)
        {
            if (source[index] != output[index])
                changed++;
        }
        return changed;
    }

    private static bool EqualOutsideSpan(
        byte[] left,
        byte[] right,
        int offset,
        int length) =>
        left.Length == right.Length &&
        offset >= 0 &&
        length >= 0 &&
        offset + (long)length <= left.Length &&
        left.AsSpan(0, offset).SequenceEqual(right.AsSpan(0, offset)) &&
        left.AsSpan(offset + length).SequenceEqual(
            right.AsSpan(offset + length));

    private static bool RangesOverlap(
        int leftOffset,
        int leftLength,
        int rightOffset,
        int rightLength) =>
        leftOffset < rightOffset + rightLength &&
        rightOffset < leftOffset + leftLength;

    private static byte[] UInt32(uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }

    internal static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void RequireHash(
        string actual,
        string expected,
        string label)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                label + " SHA-256 changed: expected " + expected +
                ", got " + actual + ".");
        }
    }
}
