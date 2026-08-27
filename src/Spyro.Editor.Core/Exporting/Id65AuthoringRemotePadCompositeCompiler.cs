using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

internal sealed record Id65AuthoringRemotePadCompositeCompileRequest(
    Id65AuthoringEditorDocument Document,
    Id65AuthoringCompositeLockedSource Source,
    Id65V2NativeTextureWitness TextureWitness,
    Id65MobyDependencyBundleDescriptor GrassBundle,
    Id65AuthoringCompositeCompilerLimits? Limits = null);

internal sealed record Id65AuthoringRemotePadCompositePlacementReadback(
    string PositionId,
    int PositionOrdinal,
    int EditorX,
    int EditorY,
    int EditorZ,
    int RawX,
    int RawY,
    int RawZ,
    int TargetTrueIndex,
    ushort ActorId,
    byte YawByte,
    string SupportHandle,
    int SupportCollisionTrueIndex,
    string RowSha256);

internal sealed record Id65AuthoringRemotePadCompositeFrozenPin(
    string PositionId,
    string OutputRow80Sha256,
    string OwnedRangeMapSha256,
    string TransactionSha256,
    string DeterministicPlanSha256);

internal sealed class Id65CompiledAuthoringRemotePadComposite
{
    private readonly byte[] _sourceRow80;
    private readonly byte[] _outputRow80;
    private readonly ReadOnlyCollection<Id65AuthoringCompositeOwnedRange> _ownedRanges;
    private readonly ReadOnlyCollection<Id65AuthoringCompositeOwnedRange> _inverseRanges;

    internal Id65CompiledAuthoringRemotePadComposite(
        Id65AuthoringEditorDocument document,
        byte[] sourceRow80,
        byte[] outputRow80,
        IEnumerable<Id65AuthoringCompositeOwnedRange> ownedRanges,
        IEnumerable<Id65AuthoringCompositeOwnedRange> inverseRanges,
        Id65AuthoringRemotePadCompositePlacementReadback placement,
        string baselineOutputRow80Sha256,
        string baselineDeterministicPlanSha256,
        int changedByteCount,
        string ownedRangeMapSha256,
        string transactionSha256,
        string protectedComplementSha256,
        string deterministicPlanSha256,
        string targetOverlaySha256,
        string globalExecutableSha256)
    {
        Document = document;
        DocumentCanonicalSha256 = document.CanonicalSha256;
        _sourceRow80 = sourceRow80.ToArray();
        _outputRow80 = outputRow80.ToArray();
        _ownedRanges = Array.AsReadOnly(
            ownedRanges.Select(item => item.DeepCopy()).ToArray());
        _inverseRanges = Array.AsReadOnly(
            inverseRanges.Select(item => item.DeepCopy()).ToArray());
        Placement = placement;
        SourceRow80Sha256 = Id65AuthoringRemotePadCompositeCompiler.Hash(_sourceRow80);
        OutputRow80Sha256 = Id65AuthoringRemotePadCompositeCompiler.Hash(_outputRow80);
        OutputModelSha256 = Id65AuthoringRemotePadCompositeCompiler.Hash(
            _outputRow80.AsSpan(
                Id65AuthoringRemotePadCompositeCompiler.ModelOffset,
                Id65AuthoringRemotePadCompositeCompiler.ModelLength));
        BaselineOutputRow80Sha256 = baselineOutputRow80Sha256;
        BaselineDeterministicPlanSha256 = baselineDeterministicPlanSha256;
        TargetOverlaySha256 = targetOverlaySha256;
        GlobalExecutableSha256 = globalExecutableSha256;
        ChangedByteCount = changedByteCount;
        OwnedRangeMapSha256 = ownedRangeMapSha256;
        TransactionSha256 = transactionSha256;
        ProtectedComplementSha256 = protectedComplementSha256;
        DeterministicPlanSha256 = deterministicPlanSha256;
    }

    public string ProfileId => Id65AuthoringRemotePadCompositeCompiler.ProfileId;
    public int SchemaVersion => Id65AuthoringRemotePadCompositeCompiler.SchemaVersion;
    public Id65AuthoringEditorDocument Document { get; }
    public string DocumentCanonicalSha256 { get; }
    public string CatalogCanonicalSha256 => Id65RemotePadGrassT107Catalog.CanonicalSha256;
    public string SourceRow80Sha256 { get; }
    public string OutputRow80Sha256 { get; }
    public string OutputModelSha256 { get; }
    public string BaselineOutputRow80Sha256 { get; }
    public string BaselineDeterministicPlanSha256 { get; }
    public string TargetOverlaySha256 { get; }
    public string GlobalExecutableSha256 { get; }
    public IReadOnlyList<Id65AuthoringCompositeOwnedRange> OwnedRanges => _ownedRanges;
    public IReadOnlyList<Id65AuthoringCompositeOwnedRange> InverseRanges => _inverseRanges;
    public Id65AuthoringRemotePadCompositePlacementReadback Placement { get; }
    public int ChangedByteCount { get; }
    public string OwnedRangeMapSha256 { get; }
    public string TransactionSha256 { get; }
    public string ProtectedComplementSha256 { get; }
    public string DeterministicPlanSha256 { get; }

    public bool BuildsFromLockedSourceThroughFrozenLegacyBaseline => true;
    public bool PlacementDerivedFromCanonicalDocumentAndCatalog => true;
    public bool AcceptsCallerCompiledAfterimages => false;
    public bool AcceptsAggregatePlans => false;
    public bool AcceptsMaterializedCandidates => false;
    public bool AppliesOneNonOverlappingLedger => true;
    public bool ExactInverseVerified => true;
    public bool ContainsOutputRow80Bytes => true;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool WritesCue => false;
    public bool AppIntegrated => false;
    public bool UiIntegrated => false;
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
/// Smallest schema-3 native compiler slice. It invokes the frozen legacy
/// structural/T66/T107-only compiler as a baseline, replaces only the owned T107
/// row with the exact catalog-derived row, and independently rebuilds and
/// verifies its own locked-source ledger and inverse. It has no persistence,
/// candidate, runtime, Create BIN, or release surface.
/// </summary>
internal static class Id65AuthoringRemotePadCompositeCompiler
{
    public const string ProfileId =
        "id65-authoring-remote-pad-composite-schema3-in-memory-e1";
    public const int SchemaVersion = 1;
    public const int ExpectedOwnedRangeCount = 7_960;
    public const int ExpectedTextureOwnedRangeCount = 7_949;
    public const int ExpectedGrassOwnedRangeCount = 8;
    public const string ExpectedOutputModelSha256 =
        Id65AuthoringCompositeCompiler.ExpectedCombinedModelSha256;
    public const string ExpectedCatalogCanonicalSha256 =
        Id65RemotePadGrassT107Catalog.ExpectedCanonicalSha256;
    public const string ExpectedBaselineOutputRow80Sha256 =
        "dc6dd888b9b62390516fcb1665d80f045b9822b033bfc7c5211c48a3dbbdc4b6";
    public const string ExpectedBaselineDeterministicPlanSha256 =
        "9f7d2f8e0b279b1e957570d0ddd4d8d6bbcf2a932048b3283a8c8c40db9188f2";
    public const int ExpectedChangedByteCount = 921_108;
    public const string ExpectedProtectedComplementSha256 =
        "2211814c105583eecf0d1e3f259ce8dd867a2c4a3988ac77c2c1541adf4fc5a4";

    private static readonly Id65AuthoringRemotePadCompositeFrozenPin[] FrozenPinArray =
    [
        new(
            Id65RemotePadGrassT107Catalog.NorthWestPositionId,
            "0701cb4de1ceca00143976ef6a7dfbc1da4d528a020cfad616169a3a06a60e60",
            "6e5d2972456c2394a37e399e85f22b3907cbaa7f24be0763084831134f199fd4",
            "d627fd48ad49919d651cb2a51e6cd94a59ed625d0f4bed07371e996da40a6e58",
            "7b3b65899b8021d9409d0b0880e7edf082042ee4b1cf4ccbcfb7898fc560cc34"),
        new(
            Id65RemotePadGrassT107Catalog.NorthPositionId,
            "097517a1d19c6f8bd947401fcaa159062caed8e552f757777c58aaac8cdaf550",
            "48e7cdfa42563ccb05901f72d4034f87b839989bbd315bfc7366e01f7a872920",
            "00d3adabdf22a53fcd4fb5967851e7be01f0c957f930f90b9af395266d3a23f3",
            "6a6117cef892b0afc3859c0dde8649de5958ab559d4be2f8981ef6c3db8cc4fc"),
        new(
            Id65RemotePadGrassT107Catalog.NorthEastPositionId,
            "48e6069d5f764a851dca4f65e3d978130c52c2dcb27f2879eaeadd87597d62b2",
            "3917ad93d90995685d0a628bcedd2fcbfc004fbb102833a87a8f023079035651",
            "7884a62b32e92e87073d5211656aa857d9c92bdf6ba3513b3ac6b202fa4927f1",
            "1bbf278985bcd8a6bf0733a0c58b9e4707139f0b01d1ee67646a9076c191d837"),
        new(
            Id65RemotePadGrassT107Catalog.WestPositionId,
            "2c09d59715026b062f1c2d570a28fc42d787e940870bb8622cfa3353417eb161",
            "7865dbf42c1cde3d7c0119a4752616f75b97df25c747408bce4125ee13fe547d",
            "e55baec98264eea330bdf4fa46692ef3be5533a430875a5064827434aa014920",
            "30f55463af79c7cfddee6dfe3286bcc9efd97bb5ee595a6ab563bd5619c8e765"),
        new(
            Id65RemotePadGrassT107Catalog.CenterPositionId,
            "5deb3c24686fba84f4d25c30eec5f1bc6159df2e2165b98b31f7c32279c10f80",
            "7b8a11a3fc877b4f0c8ad2cde9b81609a65832e506ca67bb65ff2a5719c6e63a",
            "e1f4068875d2e85312d8267989c0951aefa88c762f2261ef5906e25176a23964",
            "296fb94633c4b4837377f3f326f583a37573210ec1eaef069e55a660d94af7f6"),
        new(
            Id65RemotePadGrassT107Catalog.EastPositionId,
            "00f5869e8d1398f4a0264eef9b52332a0a90ff66254995639eba09f2d00063f2",
            "352a9347a9d0ca10235643279b240db4926c1712479b957010690d2f75f71738",
            "f68150e955005443a0b90229a07aae5135bfe7d53d5d68773ba9893aa729c35a",
            "ae5a8f6fec549108ff23d17c9649a86b6d02d1cbb7a4f89e2c192273ab954021"),
        new(
            Id65RemotePadGrassT107Catalog.SouthWestPositionId,
            "4041dd88c2e31a4bc425d1a929b2e763959963530197ca46893b5a2f69adedba",
            "3d8d5e61e2c3d74d183ebfcab8bcce8da097735c95f7e0fb8cd10725aa55b0a7",
            "37e5f47d8aaf01efcdc51a46a4cb1311337aac1dd97c02c00d306f0d6aed21f3",
            "7bd2aec0a530a3d5a4a38b1c49207caa871647032f4937fdfee0bda0286c7fd1"),
        new(
            Id65RemotePadGrassT107Catalog.SouthPositionId,
            "95c1b2f746ce5c212978dd0a2af1e13cac71ef1026c01765d215384c7dd20776",
            "0f454af09ad10b1f20435be73aadb5ecd790d77f0eb841e292afd2b7bcc2e0e5",
            "0d426962c724a6733500c2292d048e5a3998b6e9c9fc13b1a8201e5e4eb563ba",
            "19680e124d5963afbc2c786f2639c49a983c517c816174cf11e2069d99eb9bf3"),
        new(
            Id65RemotePadGrassT107Catalog.SouthEastPositionId,
            "a6d4f09522dfafc046c2f61ad031034ff7eef4d32a79a8c1bad1a8e13f22fcd3",
            "8bf320049d91e2504ddcf241e43d0f641fe66419797d26109a7f7b7548e19a13",
            "9729ca15da30bbaec97ed8612891b57d035c130bf45265ce67eb11973ca3a458",
            "2b04bf1c9679bd24aea14fab97dacdb6bbd597b20d223399529b2dc0eefad1f4")
    ];

    private static readonly ReadOnlyDictionary<
        string,
        Id65AuthoringRemotePadCompositeFrozenPin> FrozenPins =
        new(FrozenPinArray.ToDictionary(item => item.PositionId, StringComparer.Ordinal));

    private const int Row80ByteLength = 0x2E2000;
    private const int TexturePagesOffset = 0x800;
    private const int TexturePagesLength = 0xDE000;
    internal const int ModelOffset = 0xDE800;
    internal const int ModelLength = 0x94800;
    private const int RecordLength = 0x58;
    private const int SceneOffset = 0x1D0000;
    private const int ObjectCountOffset = SceneOffset + 0x16C;
    private const int ObjectTableOffset = SceneOffset + 0x170;
    private const int T88Offset = ObjectTableOffset + (88 * RecordLength);
    private const int T107Offset = ObjectTableOffset + (107 * RecordLength);
    private const int FixupCountOffset = SceneOffset + 0x7F28;
    private const int FixupListOffset = FixupCountOffset + 4;
    private const int FixupAppendOffset = SceneOffset + 0x8130;
    private const int PropertiesOffset = SceneOffset + 0x8138;
    private const int Root37Offset = 0xE4;
    private const int ActorId37Offset = 0x19A;
    private const int PackageOffset = 0x1CFA44;
    private const int PackageLength = 0x174;
    private const string LegacyT107RowSha256 =
        "d42e49248278759d2c2020bba4d4c4db18817bf120d73829a2bbba7dddcef9fb";

    static Id65AuthoringRemotePadCompositeCompiler()
    {
        if (FrozenPinArray.Length != 9 ||
            FrozenPins.Count != 9 ||
            !FrozenPinArray.Select(item => item.PositionId).SequenceEqual(
                Id65RemotePadGrassT107Catalog.Positions.Select(item => item.PositionId)) ||
            FrozenPinArray.Any(item =>
                item.OutputRow80Sha256.Length != 64 ||
                item.OwnedRangeMapSha256.Length != 64 ||
                item.TransactionSha256.Length != 64 ||
                item.DeterministicPlanSha256.Length != 64))
        {
            throw new InvalidDataException(
                "The frozen nine-position schema-3 result contract changed.");
        }
    }

    public static Id65AuthoringRemotePadCompositeFrozenPin GetRequiredFrozenPin(
        string positionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(positionId);
        if (!FrozenPins.TryGetValue(
                positionId,
                out Id65AuthoringRemotePadCompositeFrozenPin? pin))
        {
            throw new KeyNotFoundException(
                "Unknown frozen schema-3 remote-pad result position " + positionId + ".");
        }
        return pin;
    }

    public static Id65CompiledAuthoringRemotePadComposite Compile(
        Id65AuthoringRemotePadCompositeCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Document);
        ArgumentNullException.ThrowIfNull(request.Source);
        ArgumentNullException.ThrowIfNull(request.TextureWitness);
        ArgumentNullException.ThrowIfNull(request.GrassBundle);

        (Id65AuthoringEditorDocument document,
            Id65RemotePadGrassT107Position position,
            Id65MobyPlacementIntent catalogPlacement) =
            ValidateDocument(request.Document);

        Id65MobyPlacementIntent legacyPlacement =
            Id65AuthoringMobyDependencyBundleCompiler.CreateT107Placement();
        Id65TerrainTriangleIntent triangle =
            Id65AuthoringTerrainMutationCompiler.CreateConcreteTwoTileWitness();
        Id65AuthoringCompositeManifest baselineManifest =
            Id65AuthoringCompositeCompiler.CreateManifest(
                triangle,
                request.TextureWitness,
                request.GrassBundle,
                [legacyPlacement],
                Id65AuthoringEditorDocument.PrivateMandatoryBaseFaceTextureId);
        Id65CompiledAuthoringComposite baseline =
            Id65AuthoringCompositeCompiler.Compile(
                new(
                    request.Source,
                    baselineManifest,
                    request.TextureWitness,
                    [request.GrassBundle],
                    [legacyPlacement],
                    request.Limits));
        ValidateBaseline(baseline, baselineManifest, request.Source);

        byte[] source = baseline.CopySourceRow80();
        byte[] baselineOutput = baseline.CopyOutputRow80();
        byte[] catalogRow =
            BuildCatalogRow(request.GrassBundle, catalogPlacement, position);
        List<Id65AuthoringCompositeOwnedRange> ledger =
            RebuildLedger(baseline, source, catalogRow);
        ValidateLedger(source, ledger);

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
                "The schema-3 remote-pad transaction did not invert exactly.");
        }

        VerifyReadback(source, baselineOutput, output, catalogRow, position);
        string protectedSource = HashProtectedComplement(source, ledger);
        RequireHash(
            HashProtectedComplement(output, ledger),
            protectedSource,
            "schema-3 protected complement");
        string ownedHash = HashOwnedRanges(ledger);
        string transactionHash = HashTransaction(ledger);
        int changedByteCount = CountChangedBytes(source, output);
        string deterministicHash = Hash(Encoding.UTF8.GetBytes(string.Join('\n',
        [
            ProfileId,
            SchemaVersion.ToString(CultureInfo.InvariantCulture),
            document.CanonicalSha256,
            Id65RemotePadGrassT107Catalog.CanonicalSha256,
            position.PositionId,
            position.RowSha256,
            request.TextureWitness.CanonicalSha256,
            request.GrassBundle.CanonicalSha256,
            baseline.ManifestSha256,
            baseline.OutputRow80Sha256,
            baseline.DeterministicPlanSha256,
            Hash(source),
            Hash(output),
            Hash(output.AsSpan(ModelOffset, ModelLength)),
            changedByteCount.ToString(CultureInfo.InvariantCulture),
            ownedHash,
            transactionHash,
            protectedSource
        ])));

        Id65AuthoringRemotePadCompositeFrozenPin frozen =
            GetRequiredFrozenPin(position.PositionId);
        RequireHash(
            baseline.OutputRow80Sha256,
            ExpectedBaselineOutputRow80Sha256,
            "schema-3 frozen legacy baseline row-80");
        RequireHash(
            baseline.DeterministicPlanSha256,
            ExpectedBaselineDeterministicPlanSha256,
            "schema-3 frozen legacy baseline plan");
        if (changedByteCount != ExpectedChangedByteCount)
        {
            throw new InvalidDataException(
                "The schema-3 changed-byte count changed from " +
                ExpectedChangedByteCount + " to " + changedByteCount + ".");
        }
        RequireHash(
            protectedSource,
            ExpectedProtectedComplementSha256,
            "schema-3 protected complement pin");
        RequireHash(Hash(output), frozen.OutputRow80Sha256,
            "schema-3 position output row-80");
        RequireHash(ownedHash, frozen.OwnedRangeMapSha256,
            "schema-3 position owned-range map");
        RequireHash(transactionHash, frozen.TransactionSha256,
            "schema-3 position transaction");
        RequireHash(deterministicHash, frozen.DeterministicPlanSha256,
            "schema-3 position deterministic plan");

        Id65AuthoringRemotePadCompositePlacementReadback readback = new(
            position.PositionId,
            position.Ordinal,
            position.EditorX,
            position.EditorY,
            position.EditorZ,
            position.RawX,
            position.RawY,
            position.RawZ,
            Id65RemotePadGrassT107Catalog.TargetTrueIndex,
            Id65RemotePadGrassT107Catalog.ActorId,
            Id65RemotePadGrassT107Catalog.YawByte,
            Id65RemotePadGrassT107Catalog.SupportHandle,
            Id65RemotePadGrassT107Catalog.SupportCollisionTrueIndex,
            position.RowSha256);
        Id65CompiledAuthoringRemotePadComposite compiled = new(
            document,
            source,
            output,
            ledger,
            inverse,
            readback,
            baseline.OutputRow80Sha256,
            baseline.DeterministicPlanSha256,
            changedByteCount,
            ownedHash,
            transactionHash,
            protectedSource,
            deterministicHash,
            request.Source.TargetOverlaySha256,
            request.Source.GlobalExecutableSha256);

        byte[] applied =
            ApplyTransactional(compiled, compiled.CopySourceRow80(), reverse: false);
        byte[] inverseApplied =
            ApplyTransactional(compiled, applied, reverse: true);
        if (!applied.SequenceEqual(output) || !inverseApplied.SequenceEqual(source))
        {
            throw new InvalidDataException(
                "The schema-3 compiled transaction failed its exact round trip.");
        }
        return compiled;
    }

    public static byte[] ApplyTransactional(
        Id65CompiledAuthoringRemotePadComposite compiled,
        byte[] input,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(compiled);
        ArgumentNullException.ThrowIfNull(input);
        byte[] expected =
            reverse ? compiled.CopyOutputRow80() : compiled.CopySourceRow80();
        if (!input.SequenceEqual(expected))
        {
            throw new InvalidDataException(
                reverse
                    ? "The schema-3 remote-pad rollback afterimage changed."
                    : "The schema-3 remote-pad locked-source preimage changed.");
        }
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges =
            reverse ? compiled.InverseRanges : compiled.OwnedRanges;
        byte[] output = ApplyOwnedRanges(input, ranges);
        byte[] expectedOutput =
            reverse ? compiled.CopySourceRow80() : compiled.CopyOutputRow80();
        if (!output.SequenceEqual(expectedOutput))
        {
            throw new InvalidDataException(
                "The schema-3 remote-pad transaction output changed.");
        }
        return output;
    }

    private static (
        Id65AuthoringEditorDocument Document,
        Id65RemotePadGrassT107Position Position,
        Id65MobyPlacementIntent Placement) ValidateDocument(
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
            supplied.MandatoryBaseFaceTextureId !=
                reparsed.MandatoryBaseFaceTextureId ||
            supplied.T107RemotePadPositionId !=
                reparsed.T107RemotePadPositionId)
        {
            throw new InvalidDataException(
                "The supplied schema-3 document disagrees with its canonical reparse.");
        }
        if (reparsed.SchemaVersion !=
                Id65AuthoringEditorDocument.RemotePadGridSchemaVersion ||
            reparsed.ProfileId !=
                Id65AuthoringEditorDocument.RemotePadGridDocumentProfileId ||
            !reparsed.UsesRemotePadT107Grid ||
            reparsed.MandatoryBaseFaceTextureId !=
                Id65AuthoringEditorDocument.PrivateMandatoryBaseFaceTextureId ||
            reparsed.Terrain.Count != 1 ||
            reparsed.MobyPlacements.Count != 1 ||
            reparsed.MobyPlacements[0].Id !=
                Id65RemotePadGrassT107Catalog.PlacementId)
        {
            throw new InvalidDataException(
                "E1 supports only schema-3 terrain ON, T66, T107 ON, and T88 OFF.");
        }

        string positionId = reparsed.T107RemotePadPositionId!;
        Id65RemotePadGrassT107Position position =
            Id65RemotePadGrassT107Catalog.GetRequiredPosition(positionId);
        Id65AuthoringEditorDocument expected =
            Id65AuthoringEditorDocument.CreateRemotePadGridExact(
                position.PositionId,
                includeOptionalTerrain: true,
                placementIds: [Id65RemotePadGrassT107Catalog.PlacementId],
                mandatoryBaseFaceTextureId:
                    Id65AuthoringEditorDocument.PrivateMandatoryBaseFaceTextureId);
        if (reparsed.CanonicalJson != expected.CanonicalJson ||
            reparsed.CanonicalSha256 != expected.CanonicalSha256 ||
            Id65RemotePadGrassT107Catalog.CanonicalSha256 !=
                ExpectedCatalogCanonicalSha256 ||
            !Id65RemotePadGrassT107Catalog.IsStrictlyInsideSupportTriangle(
                position.EditorX,
                position.EditorY,
                position.EditorZ) ||
            Id65RemotePadGrassT107Catalog.RuntimeAcceptancePassed ||
            Id65RemotePadGrassT107Catalog.RuntimeCandidateAuthorized ||
            Id65RemotePadGrassT107Catalog.NormalCreateBinAuthorized ||
            Id65RemotePadGrassT107Catalog.ReleasePublicationAuthorized)
        {
            throw new InvalidDataException(
                "The schema-3 remote-pad document or frozen catalog identity changed.");
        }

        Id65MobyPlacementIntent placement =
            Id65RemotePadGrassT107Catalog.CreateRemotePadT107Placement(
                position.PositionId);
        if (placement.Id != Id65RemotePadGrassT107Catalog.PlacementId ||
            placement.AtomicGroupId != Id65RemotePadGrassT107Catalog.AtomicGroupId ||
            placement.BundleId != Id65RemotePadGrassT107Catalog.BundleId ||
            placement.RowTemplateId != Id65RemotePadGrassT107Catalog.RowTemplateId ||
            placement.RowPolicy != Id65MobyRowAllocationPolicy.AppendContiguous ||
            placement.TargetTrueIndex !=
                Id65RemotePadGrassT107Catalog.TargetTrueIndex ||
            placement.CoordinatePolicy !=
                Id65MobyCoordinatePolicy.AuthorExact ||
            placement.CoordinatesOrDelta !=
                new Id65AuthoringPoint(
                    position.RawX,
                    position.RawY,
                    position.RawZ) ||
            placement.YawByte != Id65RemotePadGrassT107Catalog.YawByte ||
            placement.SupportHandle != Id65RemotePadGrassT107Catalog.SupportHandle)
        {
            throw new InvalidDataException(
                "The catalog-derived schema-3 placement identity changed.");
        }
        return (reparsed, position, placement);
    }

    private static void ValidateBaseline(
        Id65CompiledAuthoringComposite baseline,
        Id65AuthoringCompositeManifest manifest,
        Id65AuthoringCompositeLockedSource source)
    {
        Id65AuthoringCompositeOwnedRange[] t107 = baseline.OwnedRanges
            .Where(item => item.StableId == "placement.t107")
            .ToArray();
        if (baseline.ProfileId != Id65AuthoringCompositeCompiler.ProfileId ||
            baseline.Manifest != manifest ||
            baseline.ManifestSha256 != manifest.CanonicalSha256 ||
            baseline.SourceRow80Sha256 !=
                Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 ||
            baseline.SourceModelSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedLockedModelSha256 ||
            baseline.OutputModelSha256 != ExpectedOutputModelSha256 ||
            baseline.TargetOverlaySha256 != source.TargetOverlaySha256 ||
            baseline.GlobalExecutableSha256 != source.GlobalExecutableSha256 ||
            baseline.OwnedRanges.Count != ExpectedOwnedRangeCount ||
            baseline.OwnedRanges.Count(item => item.StableId.StartsWith(
                "texture.page.", StringComparison.Ordinal)) !=
                    ExpectedTextureOwnedRangeCount ||
            baseline.OwnedRanges.Count(item => item.StableId == "model.final") != 1 ||
            baseline.OwnedRanges.Count(item => item.OwnerId == "composite.v3-spawn") != 2 ||
            baseline.OwnedRanges.Any(item => item.StableId == "placement.t88") ||
            t107.Length != 1 ||
            t107[0].DataRelativeOffset != T107Offset ||
            t107[0].ByteLength != RecordLength ||
            t107[0].AfterSha256 != LegacyT107RowSha256 ||
            baseline.MobyPlacements.Count != 1 ||
            baseline.MobyPlacements[0].TargetTrueIndex != 107 ||
            baseline.MobyPlacements[0].RowSha256 != LegacyT107RowSha256 ||
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
                "The frozen structural, T66, T107-only legacy baseline changed.");
        }
    }

    private static byte[] BuildCatalogRow(
        Id65MobyDependencyBundleDescriptor bundle,
        Id65MobyPlacementIntent placement,
        Id65RemotePadGrassT107Position position)
    {
        if (bundle.Id != Id65RemotePadGrassT107Catalog.BundleId ||
            bundle.CanonicalSha256 !=
                Id65RemotePadGrassT107Catalog.BundleCanonicalSha256 ||
            bundle.Rows.Count != 1 ||
            bundle.Rows[0].Id != Id65RemotePadGrassT107Catalog.RowTemplateId ||
            bundle.Rows[0].ActorId != Id65RemotePadGrassT107Catalog.ActorId)
        {
            throw new InvalidDataException(
                "The exact catalog Grass bundle or row template changed.");
        }

        byte[] row = Convert.FromHexString(bundle.Rows[0].ExactHex);
        if (row.Length != RecordLength ||
            Hash(row) != bundle.Rows[0].Sha256)
        {
            throw new InvalidDataException(
                "The exact Grass row-template bytes changed.");
        }
        WriteInt32(row, 0, 0x8138);
        Id65AuthoringPoint coordinates = placement.CoordinatesOrDelta!.Value;
        WriteInt32(row, 0x0C, coordinates.X);
        WriteInt32(row, 0x10, coordinates.Y);
        WriteInt32(row, 0x14, coordinates.Z);
        row[0x47] = placement.YawByte!.Value;
        RequireHash(Hash(row), position.RowSha256, "catalog-derived T107 row");
        return row;
    }

    private static List<Id65AuthoringCompositeOwnedRange> RebuildLedger(
        Id65CompiledAuthoringComposite baseline,
        byte[] source,
        byte[] catalogRow)
    {
        List<Id65AuthoringCompositeOwnedRange> ranges = [];
        foreach (Id65AuthoringCompositeOwnedRange range in baseline.OwnedRanges)
        {
            if (range.StableId == "placement.t107")
            {
                byte[] before =
                    source.AsSpan(T107Offset, RecordLength).ToArray();
                if (!before.SequenceEqual(range.CopyBefore()))
                {
                    throw new InvalidDataException(
                        "The legacy T107 ownership preimage changed.");
                }
                ranges.Add(new(
                    "placement.remote-pad.t107",
                    Id65RemotePadGrassT107Catalog.PlacementId,
                    T107Offset,
                    before,
                    catalogRow));
            }
            else
            {
                ranges.Add(range.DeepCopy());
            }
        }
        return ranges;
    }

    private static void ValidateLedger(
        byte[] source,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ledger)
    {
        string[] grassStableIds =
        [
            "asset.root37",
            "asset.actor-id37",
            "asset.package.01f5",
            "scene.object-count",
            "placement.remote-pad.t107",
            "scene.fixup-count",
            "scene.fixup.t107-properties",
            "asset.properties.grass"
        ];
        Id65AuthoringCompositeOwnedRange[] grass = ledger
            .Where(item =>
                grassStableIds.Contains(item.StableId, StringComparer.Ordinal))
            .ToArray();
        if (ledger.Count != ExpectedOwnedRangeCount ||
            ledger.Select(item => item.StableId)
                .Distinct(StringComparer.Ordinal).Count() != ledger.Count ||
            ledger.Count(item => item.StableId.StartsWith(
                "texture.page.", StringComparison.Ordinal)) !=
                    ExpectedTextureOwnedRangeCount ||
            ledger.Count(item => item.StableId == "model.final") != 1 ||
            ledger.Count(item => item.OwnerId == "composite.v3-spawn") != 2 ||
            ledger.Any(item => item.StableId == "placement.t88") ||
            grass.Length != ExpectedGrassOwnedRangeCount ||
            !grass.Select(item => (item.DataRelativeOffset, item.ByteLength))
                .SequenceEqual(new[]
                {
                    (0xE4, 4),
                    (0x19A, 2),
                    (0x1CFA44, 0x174),
                    (0x1D016C, 4),
                    (0x1D2638, 0x58),
                    (0x1D7F28, 4),
                    (0x1D8130, 4),
                    (0x1D8138, 8)
                }))
        {
            throw new InvalidDataException(
                "The schema-3 ownership ledger partition changed.");
        }

        for (int index = 0; index < ledger.Count; index++)
        {
            Id65AuthoringCompositeOwnedRange range = ledger[index];
            byte[] before = range.CopyBefore();
            byte[] after = range.CopyAfter();
            if (range.DataRelativeOffset < 0 ||
                range.ByteLength <= 0 ||
                range.DataRelativeOffset + (long)range.ByteLength >
                    Row80ByteLength ||
                before.Length != range.ByteLength ||
                after.Length != range.ByteLength ||
                Hash(before) != range.BeforeSha256 ||
                Hash(after) != range.AfterSha256 ||
                !source.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(before) ||
                before.SequenceEqual(after) ||
                index > 0 &&
                    RangesOverlap(
                        ledger[index - 1].DataRelativeOffset,
                        ledger[index - 1].ByteLength,
                        range.DataRelativeOffset,
                        range.ByteLength))
            {
                throw new InvalidDataException(
                    "Schema-3 owned range " + range.StableId +
                    " lost bounds, hash, preimage, or nonoverlap identity.");
            }
        }
    }

    private static void VerifyReadback(
        byte[] source,
        byte[] baselineOutput,
        byte[] output,
        byte[] catalogRow,
        Id65RemotePadGrassT107Position position)
    {
        if (source.Length != Row80ByteLength ||
            baselineOutput.Length != Row80ByteLength ||
            output.Length != Row80ByteLength ||
            !output.AsSpan(T107Offset, RecordLength).SequenceEqual(catalogRow))
        {
            throw new InvalidDataException(
                "The schema-3 row-80 or T107 readback length changed.");
        }
        for (int index = 0; index < output.Length; index++)
        {
            if ((index < T107Offset ||
                    index >= T107Offset + RecordLength) &&
                output[index] != baselineOutput[index])
            {
                throw new InvalidDataException(
                    "The schema-3 wrapper changed a byte outside the owned T107 row.");
            }
        }

        RequireHash(Hash(output.AsSpan(ModelOffset, ModelLength)),
            ExpectedOutputModelSha256,
            "schema-3 structural T66 model");
        RequireHash(
            Hash(output.AsSpan(TexturePagesOffset, TexturePagesLength)),
            Id65V2NativeTextureCompositionCompiler.ExpectedOutputTexturePagesSha256,
            "schema-3 T66 pages");
        RequireHash(
            Hash(output.AsSpan(T107Offset, RecordLength)),
            position.RowSha256,
            "schema-3 remote-pad T107 row");
        if (!source.AsSpan(T88Offset, RecordLength)
                .SequenceEqual(output.AsSpan(T88Offset, RecordLength)) ||
            ReadUInt32(output, Root37Offset) != 0x1CFA44 ||
            ReadUInt16(output, ActorId37Offset) !=
                Id65RemotePadGrassT107Catalog.ActorId ||
            ReadInt32(output, ObjectCountOffset) != 108 ||
            ReadInt32(output, FixupCountOffset) != 0x82 ||
            ReadInt32(output, FixupAppendOffset) != 0x2638 ||
            ReadInt32(output, T107Offset) != 0x8138 ||
            ReadInt32(output, T107Offset + 0x0C) != position.RawX ||
            ReadInt32(output, T107Offset + 0x10) != position.RawY ||
            ReadInt32(output, T107Offset + 0x14) != position.RawZ ||
            ReadUInt16(output, T107Offset + 0x36) !=
                Id65RemotePadGrassT107Catalog.ActorId ||
            output[T107Offset + 0x47] !=
                Id65RemotePadGrassT107Catalog.YawByte ||
            CountFixup(output, 0x1FB0) != 1 ||
            CountFixup(output, 0x5AFC) != 1 ||
            CountFixup(output, 0x2638) != 1)
        {
            throw new InvalidDataException(
                "The schema-3 T88 exclusion, Grass, or fixup readback changed.");
        }
        RequireHash(Hash(output.AsSpan(PackageOffset, PackageLength)),
            "90ca71a190c4567817d728753f25df667d56515e1721d24e19e6da9dc07edcc3",
            "schema-3 Grass package");
        RequireHash(Hash(output.AsSpan(PropertiesOffset, 8)),
            "9eeeff662fd5b77dbc35de8ed01e0d1fd149cee49126625b69f65553c4b7c20b",
            "schema-3 Grass properties");

        int collisionOffset = ModelOffset + 0x2BF0C;
        int supportTriangleOffset =
            collisionOffset + 4 + 0x1E400 +
            (Id65RemotePadGrassT107Catalog.SupportCollisionTrueIndex * 12);
        if (Convert.ToHexString(output.AsSpan(supportTriangleOffset, 12)) !=
            Id65RemotePadGrassT107Catalog.SupportCollisionPackedHex)
        {
            throw new InvalidDataException(
                "The schema-3 remote-pad support collision identity changed.");
        }
    }

    private static int CountFixup(byte[] row80, int fieldOffset)
    {
        int count = ReadInt32(row80, FixupCountOffset);
        int matches = 0;
        for (int index = 0; index < count; index++)
        {
            if (ReadInt32(row80, FixupListOffset + (index * 4)) == fieldOffset)
                matches++;
        }
        return matches;
    }

    private static byte[] ApplyOwnedRanges(
        byte[] input,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges)
    {
        byte[] output = input.ToArray();
        foreach (Id65AuthoringCompositeOwnedRange range in ranges)
        {
            byte[] before = range.CopyBefore();
            if (!output.AsSpan(range.DataRelativeOffset, range.ByteLength)
                .SequenceEqual(before))
            {
                throw new InvalidDataException(
                    "Schema-3 transactional range " + range.StableId +
                    " lost its exact preimage.");
            }
            range.CopyAfter().CopyTo(output, range.DataRelativeOffset);
        }
        return output;
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
                "Schema-3 changed-byte inputs have different lengths.");
        int changed = 0;
        for (int index = 0; index < source.Length; index++)
        {
            if (source[index] != output[index])
                changed++;
        }
        return changed;
    }

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

    private static int ReadInt32(ReadOnlySpan<byte> bytes, int offset)
    {
        if (offset < 0 || offset + 4 > bytes.Length)
            throw new InvalidDataException("A 32-bit read is out of range.");
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset)
    {
        if (offset < 0 || offset + 4 > bytes.Length)
            throw new InvalidDataException(
                "A 32-bit unsigned read is out of range.");
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, int offset)
    {
        if (offset < 0 || offset + 2 > bytes.Length)
            throw new InvalidDataException(
                "A 16-bit read is out of range.");
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, 2));
    }

    private static void WriteInt32(Span<byte> bytes, int offset, int value)
    {
        if (offset < 0 || offset + 4 > bytes.Length)
            throw new InvalidDataException("A 32-bit write is out of range.");
        BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(offset, 4), value);
    }

    internal static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void RequireHash(string actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                label + " SHA-256 changed: expected " + expected +
                ", got " + actual + ".");
        }
    }
}
