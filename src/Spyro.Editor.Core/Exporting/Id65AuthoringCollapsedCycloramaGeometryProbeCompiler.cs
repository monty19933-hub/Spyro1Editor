using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

internal sealed record Id65AuthoringCollapsedCycloramaGeometryProbeCompileRequest(
    Id65AuthoringEditorDocument Document,
    Id65AuthoringCompositeLockedSource Source,
    UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan V3Template,
    Id65V2NativeTextureWitness TextureWitness,
    Id65MobyDependencyBundleDescriptor GrassBundle,
    Id65AuthoringTerrainSetLimits? TerrainLimits = null,
    Id65AuthoringCompositeCompilerLimits? CompositeLimits = null);

internal sealed class Id65CompiledAuthoringCollapsedCycloramaGeometryProbe
{
    private readonly byte[] _schema7BaselineRow80;
    private readonly byte[] _outputRow80;
    private readonly ReadOnlyCollection<Id65AuthoringCompositeOwnedRange>
        _ownedRanges;
    private readonly ReadOnlyCollection<Id65AuthoringCompositeOwnedRange>
        _inverseRanges;

    internal Id65CompiledAuthoringCollapsedCycloramaGeometryProbe(
        Id65AuthoringEditorDocument document,
        byte[] schema7BaselineRow80,
        byte[] outputRow80,
        IEnumerable<Id65AuthoringCompositeOwnedRange> ownedRanges,
        IEnumerable<Id65AuthoringCompositeOwnedRange> inverseRanges,
        Spyro1SkyBlockLayout baselineLayout,
        Spyro1SkyBlockLayout outputLayout,
        int cycloramaModelOffset,
        int cycloramaRow80Offset,
        string schema7DeterministicPlanSha256,
        string environmentSha256,
        string occlusionSha256,
        string occlusionTailSha256,
        string collisionSha256,
        string textureSha256,
        int candidateCollapsedVertexWordCount,
        int changedVertexWordCount,
        int changedLogicalByteCount,
        int polygonChangedByteCount,
        int occlusionChangedByteCount,
        int occlusionTailChangedByteCount,
        string ownedRangeMapSha256,
        string transactionSha256,
        string protectedComplementSha256,
        string deterministicPlanSha256)
    {
        Document = Id65AuthoringEditorDocument.Parse(document.SerializeUtf8());
        DocumentCanonicalSha256 = Document.CanonicalSha256;
        _schema7BaselineRow80 = schema7BaselineRow80.ToArray();
        _outputRow80 = outputRow80.ToArray();
        _ownedRanges = Array.AsReadOnly(
            ownedRanges.Select(item => item.DeepCopy()).ToArray());
        _inverseRanges = Array.AsReadOnly(
            inverseRanges.Select(item => item.DeepCopy()).ToArray());
        BaselineLayout = DeepCopyLayout(baselineLayout);
        OutputLayout = DeepCopyLayout(outputLayout);
        CycloramaModelOffset = cycloramaModelOffset;
        CycloramaRow80Offset = cycloramaRow80Offset;
        CycloramaWadOffset = checked(
            Id65AuthoringBlankCycloramaCompiler.TargetDataWadOffset +
            cycloramaRow80Offset);
        Schema7BaselineRow80Sha256 =
            Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.Hash(
                _schema7BaselineRow80);
        OutputRow80Sha256 =
            Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.Hash(
                _outputRow80);
        Schema7BaselineModelSha256 =
            Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.Hash(
                _schema7BaselineRow80.AsSpan(
                    Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset,
                    Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelLength));
        OutputModelSha256 =
            Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.Hash(
                _outputRow80.AsSpan(
                    Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset,
                    Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelLength));
        BaselineCycloramaSha256 =
            Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.Hash(
                _schema7BaselineRow80.AsSpan(
                    cycloramaRow80Offset,
                    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                        .CycloramaByteLength));
        OutputCycloramaSha256 =
            Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.Hash(
                _outputRow80.AsSpan(
                    cycloramaRow80Offset,
                    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
                        .CycloramaByteLength));
        OutputGeometrySha256 = outputLayout.GeometrySha256.ToLowerInvariant();
        Schema7DeterministicPlanSha256 = schema7DeterministicPlanSha256;
        EnvironmentSha256 = environmentSha256;
        OcclusionSha256 = occlusionSha256;
        OcclusionTailSha256 = occlusionTailSha256;
        CollisionSha256 = collisionSha256;
        TextureSha256 = textureSha256;
        CandidateCollapsedVertexWordCount = candidateCollapsedVertexWordCount;
        ChangedVertexWordCount = changedVertexWordCount;
        ChangedLogicalByteCount = changedLogicalByteCount;
        PolygonChangedByteCount = polygonChangedByteCount;
        OcclusionChangedByteCount = occlusionChangedByteCount;
        OcclusionTailChangedByteCount = occlusionTailChangedByteCount;
        OwnedRangeMapSha256 = ownedRangeMapSha256;
        TransactionSha256 = transactionSha256;
        ProtectedComplementSha256 = protectedComplementSha256;
        DeterministicPlanSha256 = deterministicPlanSha256;
    }

    public string ProfileId =>
        Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.ProfileId;
    public int SchemaVersion =>
        Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.SchemaVersion;
    public string Schema7BaselineProfileId =>
        Id65AuthoringSingleSectorSubstrateCompiler.ProfileId;
    public Id65AuthoringEditorDocument Document { get; }
    public string DocumentCanonicalSha256 { get; }
    public IReadOnlyList<string> ActiveTerrainUnitIds =>
        Document.ActiveTerrainUnitIds;
    public Id65RemotePadMobyKind MobyKind =>
        Document.RemotePadMobyKind ?? throw new InvalidDataException(
            "The collapsed-cyclorama probe lost its Moby kind.");
    public string MobyPositionId =>
        Document.RemotePadMobyPositionId ?? throw new InvalidDataException(
            "The collapsed-cyclorama probe lost its Moby position.");
    public Spyro1SkyBlockLayout BaselineLayout { get; }
    public Spyro1SkyBlockLayout OutputLayout { get; }
    public int CycloramaModelOffset { get; }
    public int CycloramaRow80Offset { get; }
    public long CycloramaWadOffset { get; }
    public int CycloramaByteLength =>
        Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
            .CycloramaByteLength;
    public int ModelRow80Offset =>
        Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset;
    public int ModelByteLength =>
        Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelLength;
    public string Schema7BaselineRow80Sha256 { get; }
    public string OutputRow80Sha256 { get; }
    public string Schema7BaselineModelSha256 { get; }
    public string OutputModelSha256 { get; }
    public string BaselineCycloramaSha256 { get; }
    public string OutputCycloramaSha256 { get; }
    public string OutputGeometrySha256 { get; }
    public string Schema7DeterministicPlanSha256 { get; }
    public string EnvironmentSha256 { get; }
    public string OcclusionSha256 { get; }
    public string OcclusionTailSha256 { get; }
    public string CollisionSha256 { get; }
    public string TextureSha256 { get; }
    public int CandidateCollapsedVertexWordCount { get; }
    public int ChangedVertexWordCount { get; }
    public int ChangedLogicalByteCount { get; }
    public int ChangedByteCount => ChangedLogicalByteCount;
    public int PolygonChangedByteCount { get; }
    public int OcclusionChangedByteCount { get; }
    public int OcclusionTailChangedByteCount { get; }
    public IReadOnlyList<Id65AuthoringCompositeOwnedRange> OwnedRanges =>
        _ownedRanges;
    public IReadOnlyList<Id65AuthoringCompositeOwnedRange> InverseRanges =>
        _inverseRanges;
    public string OwnedRangeMapSha256 { get; }
    public string TransactionSha256 { get; }
    public string ProtectedComplementSha256 { get; }
    public string DeterministicPlanSha256 { get; }

    public bool PureInMemory => true;
    public bool TransitionalProbeOnly => true;
    public bool FullyOriginalCyclorama => false;
    public bool ExactTile1GrassCenterSchema7BaselineRequired => true;
    public bool CallerSchema7AfterimageAccepted => false;
    public bool FixedCycloramaCapacityPreserved => true;
    public bool AllThirtyFivePartPointersPreservedExactly => true;
    public bool AllThirtyFivePartHeadersPreservedExactly => true;
    public bool PartCountsPreservedExactly => true;
    public bool ColorTablesPreservedExactly => true;
    public bool PolygonTablesPreservedExactly => true;
    public bool FirstVertexWordsPreservedExactly => true;
    public bool RemainingVertexWordsCollapsedPerPart => true;
    public bool DownstreamComponentOffsetsPreserved => true;
    public bool EnvironmentPreservedExactly => true;
    public bool EntireOcclusionComponentPreservedExactly => true;
    public bool SkyVisibilityDirectoryPreservedExactly => true;
    public bool OcclusionOpaqueTailPreservedExactly => true;
    public bool CollisionPreservedExactly => true;
    public bool TexturePreservedExactly => true;
    public bool SpawnAndMobyBytesPreservedExactly => true;
    public bool ExactInverseVerified => true;
    public bool EveryChangedByteOwned => true;
    public bool OwnedRangesContainOnlyChangedBytes => true;
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

    internal byte[] CopySchema7BaselineRow80() =>
        _schema7BaselineRow80.ToArray();
    internal byte[] CopyOutputRow80() => _outputRow80.ToArray();
    internal byte[] CopySchema7BaselineCyclorama() =>
        _schema7BaselineRow80.AsSpan(
            CycloramaRow80Offset,
            CycloramaByteLength).ToArray();
    internal byte[] CopyOutputCyclorama() => _outputRow80.AsSpan(
        CycloramaRow80Offset,
        CycloramaByteLength).ToArray();

    private static Spyro1SkyBlockLayout DeepCopyLayout(
        Spyro1SkyBlockLayout layout) => layout with
        {
            Parts = Array.AsReadOnly(
                layout.Parts.Select(item => item with { }).ToArray())
        };
}

/// <summary>
/// Guarded schema-7 p2 runtime probe for the exact Tile1 / Grass T107 /
/// Center row. It retains the inherited 35-part cyclorama structure and
/// collapses each part by copying that part's first packed vertex word over
/// its remaining vertices. This is deliberately transitional evidence, not
/// a fully original cyclorama authoring format. The entire occlusion
/// component, including its opaque sky-visibility tail, remains byte-exact.
/// </summary>
internal static class Id65AuthoringCollapsedCycloramaGeometryProbeCompiler
{
    public const string ProfileId =
        "id65-authoring-schema7-collapsed-cyclorama-geometry-transitional-probe-p2";
    public const int SchemaVersion = 1;
    public const string OwnerId =
        "schema7.p2-probe.cyclorama.collapsed-vertex-bytes";
    public const int CycloramaByteLength =
        Id65AuthoringBlankCycloramaCompiler.ExpectedCycloramaByteLength;
    public const int ExpectedPartCount = 35;
    public const int ExpectedVertexCount = 1_915;
    public const int ExpectedColorCount = 726;
    public const int ExpectedPolygonCount = 2_808;
    public const int ExpectedCandidateCollapsedVertexWordCount = 1_880;
    public const int ExpectedChangedVertexWordCount = 1_879;
    public const int ExpectedChangedLogicalByteCount = 7_367;
    public const int ExpectedPolygonChangedByteCount = 0;
    public const int ExpectedOcclusionChangedByteCount = 0;
    public const int ExpectedOcclusionTailChangedByteCount = 0;
    public const string ExpectedSchema7Tile1GrassCenterRow80Sha256 =
        "3689e111b027ce07fac6fe0b0f6b1e9a1a3868f4a852b0928678645e7045d819";
    public const string ExpectedSchema7CycloramaSha256 =
        "a9da1343ef6493cd46ab57d84ae78f27495affc564bcc9464396752914648ac2";
    public const string ExpectedOutputCycloramaSha256 =
        "6024a1bb86c4132998c835c7ef87f18bc90add554371871239a1e99e26475de9";
    public const string ExpectedOutputGeometrySha256 =
        "f3e5d6c705733f8db639bea5de4682ec186d80e84b2e1c1691df495b2dee0a89";
    public const string ExpectedOutputRow80Sha256 =
        "478850353edc16945cf9498c38aa3de4716a52b1531aa89108fcdbfbe610edf6";
    public const string ExpectedSchema7Tile1EnvironmentSha256 =
        "765ca3b55cb72e53305cee6d425802d998ad760661657a4bcc0a2769615ede07";
    public const string ExpectedSchema7Tile1CollisionSha256 =
        "0f6507872d7f29fefd3f70cebcedc8e47b145d08a1cc0ebc48eb47e56ed7cfcb";

    public static Id65CompiledAuthoringCollapsedCycloramaGeometryProbe Compile(
        Id65AuthoringCollapsedCycloramaGeometryProbeCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Document);
        ArgumentNullException.ThrowIfNull(request.Source);
        ArgumentNullException.ThrowIfNull(request.V3Template);
        ArgumentNullException.ThrowIfNull(request.TextureWitness);
        ArgumentNullException.ThrowIfNull(request.GrassBundle);
        RequireExactDocument(request.Document);
        Id65CompiledAuthoringSingleSectorSubstrate schema7 =
            Id65AuthoringSingleSectorSubstrateCompiler.Compile(new(
                request.Document,
                request.Source,
                request.V3Template,
                request.TextureWitness,
                request.GrassBundle,
                request.TerrainLimits,
                request.CompositeLimits));
        return CompileFromSingleSector(schema7);
    }

    public static Id65CompiledAuthoringCollapsedCycloramaGeometryProbe
        CompileFromSingleSector(
            Id65CompiledAuthoringSingleSectorSubstrate schema7)
    {
        ArgumentNullException.ThrowIfNull(schema7);
        ValidateSchema7Baseline(schema7);

        byte[] baselineRow80 = schema7.CopyOutputRow80();
        byte[] outputRow80 = baselineRow80.ToArray();
        Id65ModelComponentRelocation cyclorama =
            schema7.ComponentRelocations.Single(item =>
                item.StableId == "component.cyclorama");
        int cycloramaModelOffset = cyclorama.OutputRelativeOffset;
        int cycloramaRow80Offset = checked(
            schema7.ModelRow80Offset + cycloramaModelOffset);
        if (cycloramaModelOffset !=
                Id65AuthoringBlankCycloramaCompiler
                    .OneTerrainCycloramaModelOffset ||
            cycloramaRow80Offset !=
                Id65AuthoringBlankCycloramaCompiler
                    .OneTerrainCycloramaRow80Offset ||
            cyclorama.OutputByteLength != CycloramaByteLength ||
            !HashEquals(
                schema7.OutputCycloramaSha256,
                ExpectedSchema7CycloramaSha256) ||
            !HashEquals(cyclorama.OutputSha256, ExpectedSchema7CycloramaSha256))
        {
            throw new InvalidDataException(
                "The exact Tile1 schema-7 cyclorama binding changed.");
        }

        byte[] baselineCyclorama = baselineRow80.AsSpan(
            cycloramaRow80Offset,
            CycloramaByteLength).ToArray();
        Spyro1SkyBlockLayout baselineLayout =
            Spyro1SkyBlockAnalyzer.AnalyzeStandaloneSkyBlock(
                baselineCyclorama);
        ValidateBaselineLayout(baselineCyclorama, baselineLayout);

        byte[] outputCyclorama = baselineCyclorama.ToArray();
        int candidateCollapsedVertexWordCount = 0;
        int changedVertexWordCount = 0;
        foreach (Spyro1SkyPartLayout part in baselineLayout.Parts)
        {
            byte[] firstVertexWord = outputCyclorama.AsSpan(
                part.VertexTableOffset,
                4).ToArray();
            for (int vertex = 1; vertex < part.VertexCount; vertex++)
            {
                candidateCollapsedVertexWordCount++;
                int vertexOffset = checked(
                    part.VertexTableOffset + vertex * 4);
                if (!outputCyclorama.AsSpan(vertexOffset, 4)
                    .SequenceEqual(firstVertexWord))
                {
                    changedVertexWordCount++;
                }
                firstVertexWord.CopyTo(outputCyclorama, vertexOffset);
            }
        }

        Spyro1SkyBlockLayout outputLayout =
            Spyro1SkyBlockAnalyzer.AnalyzeStandaloneSkyBlock(outputCyclorama);
        (int polygonChangedByteCount, int changedLogicalByteCount) =
            ValidateCollapsedLayout(
                baselineCyclorama,
                outputCyclorama,
                baselineLayout,
                outputLayout,
                candidateCollapsedVertexWordCount,
                changedVertexWordCount);
        RequireHash(
            Hash(outputCyclorama),
            ExpectedOutputCycloramaSha256,
            "collapsed-cyclorama output");
        RequireHash(
            outputLayout.GeometrySha256,
            ExpectedOutputGeometrySha256,
            "collapsed-cyclorama geometry");

        outputCyclorama.CopyTo(outputRow80, cycloramaRow80Offset);
        RequireHash(
            Hash(outputRow80),
            ExpectedOutputRow80Sha256,
            "collapsed-cyclorama row 80 output");

        ProtectedComponentReadback protectedReadback =
            VerifyProtectedComponents(schema7, baselineRow80, outputRow80);
        Id65AuthoringCompositeOwnedRange[] ranges = BuildLedger(
            baselineRow80,
            outputRow80,
            baselineLayout,
            cycloramaRow80Offset);
        Id65AuthoringCompositeOwnedRange[] inverse = ranges
            .Reverse()
            .Select(item => new Id65AuthoringCompositeOwnedRange(
                item.StableId,
                item.OwnerId,
                item.DataRelativeOffset,
                item.CopyAfter(),
                item.CopyBefore()))
            .ToArray();
        ValidateLedger(
            baselineRow80,
            outputRow80,
            baselineLayout,
            cycloramaRow80Offset,
            ranges,
            changedLogicalByteCount);

        string ownedRangeMapSha256 = HashOwnedRanges(ranges);
        string transactionSha256 = HashTransaction(ranges);
        string protectedComplementSha256 = HashProtectedComplement(
            baselineRow80,
            ranges);
        RequireHash(
            HashProtectedComplement(outputRow80, ranges),
            protectedComplementSha256,
            "collapsed-cyclorama protected complement");

        string outputModelSha256 = Hash(outputRow80.AsSpan(
            schema7.ModelRow80Offset,
            schema7.ModelByteLength));
        string deterministicPlanSha256 = Hash(Encoding.UTF8.GetBytes(
            string.Join('\n',
            [
                ProfileId,
                SchemaVersion.ToString(CultureInfo.InvariantCulture),
                schema7.DocumentCanonicalSha256,
                schema7.ProfileId,
                schema7.DeterministicPlanSha256,
                schema7.OutputRow80Sha256,
                Hash(outputRow80),
                outputModelSha256,
                baselineLayout.Sha256.ToLowerInvariant(),
                outputLayout.Sha256.ToLowerInvariant(),
                outputLayout.GeometrySha256.ToLowerInvariant(),
                candidateCollapsedVertexWordCount.ToString(
                    CultureInfo.InvariantCulture),
                changedVertexWordCount.ToString(CultureInfo.InvariantCulture),
                changedLogicalByteCount.ToString(CultureInfo.InvariantCulture),
                polygonChangedByteCount.ToString(CultureInfo.InvariantCulture),
                protectedReadback.EnvironmentSha256,
                protectedReadback.OcclusionSha256,
                protectedReadback.OcclusionTailSha256,
                protectedReadback.CollisionSha256,
                protectedReadback.TextureSha256,
                ranges.Length.ToString(CultureInfo.InvariantCulture),
                ownedRangeMapSha256,
                transactionSha256,
                protectedComplementSha256
            ])));

        Id65CompiledAuthoringCollapsedCycloramaGeometryProbe compiled = new(
            schema7.Document,
            baselineRow80,
            outputRow80,
            ranges,
            inverse,
            baselineLayout,
            outputLayout,
            cycloramaModelOffset,
            cycloramaRow80Offset,
            schema7.DeterministicPlanSha256,
            protectedReadback.EnvironmentSha256,
            protectedReadback.OcclusionSha256,
            protectedReadback.OcclusionTailSha256,
            protectedReadback.CollisionSha256,
            protectedReadback.TextureSha256,
            candidateCollapsedVertexWordCount,
            changedVertexWordCount,
            changedLogicalByteCount,
            polygonChangedByteCount,
            protectedReadback.OcclusionChangedByteCount,
            protectedReadback.OcclusionTailChangedByteCount,
            ownedRangeMapSha256,
            transactionSha256,
            protectedComplementSha256,
            deterministicPlanSha256);
        if (!ApplySchema7StageTransactional(
                compiled,
                compiled.CopySchema7BaselineRow80(),
                reverse: false).SequenceEqual(outputRow80) ||
            !ApplySchema7StageTransactional(
                compiled,
                compiled.CopyOutputRow80(),
                reverse: true).SequenceEqual(baselineRow80))
        {
            throw new InvalidDataException(
                "The collapsed-cyclorama probe failed its exact inverse.");
        }
        return compiled;
    }

    public static byte[] ApplySchema7StageTransactional(
        Id65CompiledAuthoringCollapsedCycloramaGeometryProbe compiled,
        byte[] input,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(compiled);
        ArgumentNullException.ThrowIfNull(input);
        byte[] expected = reverse
            ? compiled.CopyOutputRow80()
            : compiled.CopySchema7BaselineRow80();
        if (!input.SequenceEqual(expected))
        {
            throw new InvalidDataException(reverse
                ? "The collapsed-cyclorama rollback afterimage changed."
                : "The exact schema-7 p1 preimage changed.");
        }
        byte[] output = ApplyRanges(
            input,
            reverse ? compiled.InverseRanges : compiled.OwnedRanges);
        byte[] expectedOutput = reverse
            ? compiled.CopySchema7BaselineRow80()
            : compiled.CopyOutputRow80();
        if (!output.SequenceEqual(expectedOutput))
        {
            throw new InvalidDataException(
                "The collapsed-cyclorama transaction output changed.");
        }
        return output;
    }

    private static void RequireExactDocument(
        Id65AuthoringEditorDocument document)
    {
        if (document.SchemaVersion !=
                Id65AuthoringEditorDocument
                    .RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion ||
            !document.UsesBlankCyclorama ||
            document.RemotePadMobyKind != Id65RemotePadMobyKind.GrassT107 ||
            document.RemotePadMobyPositionId !=
                Id65RemotePadGrassT107Catalog.CenterPositionId ||
            !document.ActiveTerrainUnitIds.SequenceEqual(
                [Id65AuthoringTerrainSetCatalog.Tile1Id]))
        {
            throw new InvalidDataException(
                "The collapsed-cyclorama probe is bound only to the exact Tile1 / Grass T107 / Center schema-6 document projection.");
        }
    }

    private static void ValidateSchema7Baseline(
        Id65CompiledAuthoringSingleSectorSubstrate schema7)
    {
        RequireExactDocument(schema7.Document);
        if (schema7.ProfileId !=
                Id65AuthoringSingleSectorSubstrateCompiler.ProfileId ||
            !HashEquals(
                schema7.OutputRow80Sha256,
                ExpectedSchema7Tile1GrassCenterRow80Sha256) ||
            !HashEquals(
                schema7.OutputCycloramaSha256,
                ExpectedSchema7CycloramaSha256) ||
            !HashEquals(
                schema7.Environment.OutputEnvironmentSha256,
                ExpectedSchema7Tile1EnvironmentSha256) ||
            !HashEquals(
                schema7.Occlusion.OutputOcclusionSha256,
                Id65AuthoringSingleSectorSubstrateCompiler
                    .ExpectedOutputOcclusionSha256) ||
            !HashEquals(
                schema7.CollisionSha256,
                ExpectedSchema7Tile1CollisionSha256) ||
            !schema7.PureInMemory ||
            !schema7.ProducesSchema7SingleSectorSubstrate ||
            !schema7.InheritedEnvironmentBytesRetired ||
            !schema7.CollisionPreservedExactly ||
            !schema7.CycloramaPreservedExactly ||
            !schema7.ExactInverseVerified ||
            !schema7.EveryChangedByteOwned ||
            !schema7.ProtectedComplementVerified ||
            schema7.WritesFileSystem || schema7.WritesDiscImage ||
            schema7.CreateBinEnabled || schema7.RuntimeCandidateAuthorized ||
            schema7.ReleaseIntegrated || schema7.Publishable)
        {
            throw new InvalidDataException(
                "The collapsed-cyclorama probe requires the exact guarded Tile1 / Grass T107 / Center schema-7 p1 output.");
        }
    }

    private static void ValidateBaselineLayout(
        byte[] baseline,
        Spyro1SkyBlockLayout layout)
    {
        if (baseline.Length != CycloramaByteLength ||
            layout.Index != 0 || layout.BlockOffset != 0 ||
            layout.ByteLength != CycloramaByteLength ||
            layout.EndOffsetExclusive != CycloramaByteLength ||
            layout.PartCount != ExpectedPartCount ||
            layout.VertexCount != ExpectedVertexCount ||
            layout.ColorCount != ExpectedColorCount ||
            layout.PolygonCount != ExpectedPolygonCount ||
            layout.Parts.Count != ExpectedPartCount ||
            !HashEquals(layout.Sha256, ExpectedSchema7CycloramaSha256) ||
            !HashEquals(Hash(baseline), ExpectedSchema7CycloramaSha256) ||
            layout.Parts.Any(part => part.VertexCount <= 0))
        {
            throw new InvalidDataException(
                "The exact inherited 35-part schema-7 cyclorama changed.");
        }
    }

    private static (int PolygonChangedBytes, int ChangedLogicalBytes)
        ValidateCollapsedLayout(
            byte[] baseline,
            byte[] output,
            Spyro1SkyBlockLayout baselineLayout,
            Spyro1SkyBlockLayout outputLayout,
            int candidateCollapsedVertexWordCount,
            int changedVertexWordCount)
    {
        if (baseline.Length != CycloramaByteLength ||
            output.Length != CycloramaByteLength ||
            baselineLayout.PartCount != outputLayout.PartCount ||
            baselineLayout.VertexCount != outputLayout.VertexCount ||
            baselineLayout.ColorCount != outputLayout.ColorCount ||
            baselineLayout.PolygonCount != outputLayout.PolygonCount ||
            baselineLayout.PaletteWordCount != outputLayout.PaletteWordCount ||
            baselineLayout.TrailingBytes != outputLayout.TrailingBytes ||
            baselineLayout.BackgroundColor != outputLayout.BackgroundColor ||
            baselineLayout.BackgroundColorOffset !=
                outputLayout.BackgroundColorOffset ||
            baselineLayout.Parts.Count != outputLayout.Parts.Count)
        {
            throw new InvalidDataException(
                "The collapsed-cyclorama probe changed the sky structure.");
        }

        bool[] mutableVertexByte = new bool[CycloramaByteLength];
        int independentlyChangedVertexWords = 0;
        int polygonChangedBytes = 0;
        for (int partIndex = 0;
             partIndex < baselineLayout.Parts.Count;
             partIndex++)
        {
            Spyro1SkyPartLayout beforePart = baselineLayout.Parts[partIndex];
            Spyro1SkyPartLayout afterPart = outputLayout.Parts[partIndex];
            if (beforePart != afterPart)
            {
                throw new InvalidDataException(
                    "Collapsed-cyclorama part structure changed at part " +
                    partIndex.ToString(CultureInfo.InvariantCulture) + ".");
            }
            ReadOnlySpan<byte> firstVertex = output.AsSpan(
                beforePart.VertexTableOffset,
                4);
            if (!baseline.AsSpan(beforePart.VertexTableOffset, 4)
                    .SequenceEqual(firstVertex))
            {
                throw new InvalidDataException(
                    "The first vertex word changed at cyclorama part " +
                    partIndex.ToString(CultureInfo.InvariantCulture) + ".");
            }
            for (int vertex = 1; vertex < beforePart.VertexCount; vertex++)
            {
                int offset = checked(
                    beforePart.VertexTableOffset + vertex * 4);
                for (int index = 0; index < 4; index++)
                    mutableVertexByte[offset + index] = true;
                if (!output.AsSpan(offset, 4).SequenceEqual(firstVertex))
                {
                    throw new InvalidDataException(
                        "A collapsed vertex does not equal its part's first packed word.");
                }
                if (!baseline.AsSpan(offset, 4).SequenceEqual(firstVertex))
                    independentlyChangedVertexWords++;
            }
            polygonChangedBytes += CountChangedBytes(
                baseline.AsSpan(
                    beforePart.PolygonTableOffset,
                    beforePart.PolygonCount * 8),
                output.AsSpan(
                    beforePart.PolygonTableOffset,
                    beforePart.PolygonCount * 8));
        }

        int changedLogicalBytes = 0;
        for (int index = 0; index < baseline.Length; index++)
        {
            if (baseline[index] == output[index]) continue;
            changedLogicalBytes++;
            if (!mutableVertexByte[index])
            {
                throw new InvalidDataException(
                    "The collapsed-cyclorama probe changed a non-owned sky byte.");
            }
        }
        if (candidateCollapsedVertexWordCount !=
                ExpectedCandidateCollapsedVertexWordCount ||
            changedVertexWordCount != ExpectedChangedVertexWordCount ||
            independentlyChangedVertexWords !=
                ExpectedChangedVertexWordCount ||
            changedLogicalBytes != ExpectedChangedLogicalByteCount ||
            polygonChangedBytes != ExpectedPolygonChangedByteCount)
        {
            throw new InvalidDataException(
                "The collapsed-cyclorama vertex or polygon diff census changed.");
        }
        return (polygonChangedBytes, changedLogicalBytes);
    }

    private static ProtectedComponentReadback VerifyProtectedComponents(
        Id65CompiledAuthoringSingleSectorSubstrate schema7,
        byte[] baseline,
        byte[] output)
    {
        foreach (Id65ModelComponentRelocation component in
                 schema7.ComponentRelocations)
        {
            int offset = checked(
                schema7.ModelRow80Offset + component.OutputRelativeOffset);
            ReadOnlySpan<byte> before = baseline.AsSpan(
                offset,
                component.OutputByteLength);
            ReadOnlySpan<byte> after = output.AsSpan(
                offset,
                component.OutputByteLength);
            if (component.StableId == "component.cyclorama")
            {
                if (before.SequenceEqual(after) ||
                    !HashEquals(Hash(before), ExpectedSchema7CycloramaSha256) ||
                    !HashEquals(Hash(after), ExpectedOutputCycloramaSha256))
                {
                    throw new InvalidDataException(
                        "The probe did not transform the exact cyclorama.");
                }
            }
            else if (!before.SequenceEqual(after))
            {
                throw new InvalidDataException(
                    "The probe changed protected " + component.StableId + ".");
            }
        }

        ReadOnlySpan<byte> baselineOcclusion = baseline.AsSpan(
            schema7.Occlusion.Row80Offset,
            schema7.Occlusion.ComponentByteLength);
        ReadOnlySpan<byte> outputOcclusion = output.AsSpan(
            schema7.Occlusion.Row80Offset,
            schema7.Occlusion.ComponentByteLength);
        Id65AuthoringSingleSectorSubstrateCompiler
            .RequireSingleSectorOcclusion(outputOcclusion);
        int occlusionChangedByteCount = CountChangedBytes(
            baselineOcclusion,
            outputOcclusion);
        ReadOnlySpan<byte> baselineOcclusionTail = baselineOcclusion.Slice(
            Id65AuthoringBlankCycloramaCompiler.VisibilityOpaqueTailOffset,
            Id65AuthoringBlankCycloramaCompiler.VisibilityOpaqueTailByteLength);
        ReadOnlySpan<byte> outputOcclusionTail = outputOcclusion.Slice(
            Id65AuthoringBlankCycloramaCompiler.VisibilityOpaqueTailOffset,
            Id65AuthoringBlankCycloramaCompiler.VisibilityOpaqueTailByteLength);
        int occlusionTailChangedByteCount = CountChangedBytes(
            baselineOcclusionTail,
            outputOcclusionTail);
        string occlusionTailSha256 = Hash(outputOcclusionTail);
        if (occlusionChangedByteCount != ExpectedOcclusionChangedByteCount ||
            occlusionTailChangedByteCount !=
                ExpectedOcclusionTailChangedByteCount ||
            !HashEquals(
                occlusionTailSha256,
                Id65AuthoringBlankCycloramaCompiler
                    .ExpectedVisibilityOpaqueTailSha256))
        {
            throw new InvalidDataException(
                "The probe changed the occlusion component or its sky-visibility tail.");
        }

        Id65ModelComponentRelocation collision =
            schema7.ComponentRelocations.Single(item =>
                item.StableId == "component.collision");
        Id65ModelComponentRelocation texture =
            schema7.ComponentRelocations.Single(item =>
                item.StableId == "component.texture");
        string environmentSha256 = Hash(output.AsSpan(
            schema7.Environment.Row80Offset,
            schema7.Environment.ComponentByteLength));
        string occlusionSha256 = Hash(outputOcclusion);
        string collisionSha256 = Hash(output.AsSpan(
            schema7.ModelRow80Offset + collision.OutputRelativeOffset,
            collision.OutputByteLength));
        string textureSha256 = Hash(output.AsSpan(
            schema7.ModelRow80Offset + texture.OutputRelativeOffset,
            texture.OutputByteLength));
        if (!HashEquals(
                environmentSha256,
                ExpectedSchema7Tile1EnvironmentSha256) ||
            !HashEquals(
                occlusionSha256,
                Id65AuthoringSingleSectorSubstrateCompiler
                    .ExpectedOutputOcclusionSha256) ||
            !HashEquals(collisionSha256, ExpectedSchema7Tile1CollisionSha256) ||
            !HashEquals(textureSha256, schema7.OutputTextureComponentSha256))
        {
            throw new InvalidDataException(
                "A protected schema-7 component identity changed.");
        }
        return new(
            environmentSha256,
            occlusionSha256,
            occlusionTailSha256,
            collisionSha256,
            textureSha256,
            occlusionChangedByteCount,
            occlusionTailChangedByteCount);
    }

    private static Id65AuthoringCompositeOwnedRange[] BuildLedger(
        byte[] baseline,
        byte[] output,
        Spyro1SkyBlockLayout layout,
        int cycloramaRow80Offset)
    {
        List<Id65AuthoringCompositeOwnedRange> ranges = [];
        foreach (Spyro1SkyPartLayout part in layout.Parts)
        {
            for (int vertex = 1; vertex < part.VertexCount; vertex++)
            {
                int relativeWordOffset = checked(
                    part.VertexTableOffset + vertex * 4);
                int absoluteWordOffset = checked(
                    cycloramaRow80Offset + relativeWordOffset);
                int cursor = 0;
                int run = 0;
                while (cursor < 4)
                {
                    while (cursor < 4 &&
                           baseline[absoluteWordOffset + cursor] ==
                               output[absoluteWordOffset + cursor])
                    {
                        cursor++;
                    }
                    if (cursor == 4) break;
                    int start = cursor++;
                    while (cursor < 4 &&
                           baseline[absoluteWordOffset + cursor] !=
                               output[absoluteWordOffset + cursor])
                    {
                        cursor++;
                    }
                    int length = cursor - start;
                    int offset = absoluteWordOffset + start;
                    ranges.Add(new(
                        "schema7.p2-probe.cyclorama.part." +
                            part.Index.ToString("D2", CultureInfo.InvariantCulture) +
                            ".vertex." +
                            vertex.ToString("D4", CultureInfo.InvariantCulture) +
                            ".run." +
                            run.ToString("D2", CultureInfo.InvariantCulture),
                        OwnerId,
                        offset,
                        baseline.AsSpan(offset, length).ToArray(),
                        output.AsSpan(offset, length).ToArray()));
                    run++;
                }
            }
        }
        return ranges.ToArray();
    }

    private static void ValidateLedger(
        byte[] baseline,
        byte[] output,
        Spyro1SkyBlockLayout layout,
        int cycloramaRow80Offset,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges,
        int expectedChangedByteCount)
    {
        if (ranges.Count == 0 ||
            ranges.Select(item => item.StableId)
                .Distinct(StringComparer.Ordinal).Count() != ranges.Count)
        {
            throw new InvalidDataException(
                "The collapsed-cyclorama owned ledger is empty or duplicated.");
        }
        bool[] allowed = new bool[baseline.Length];
        foreach (Spyro1SkyPartLayout part in layout.Parts)
        {
            int start = checked(
                cycloramaRow80Offset + part.VertexTableOffset + 4);
            int length = checked((part.VertexCount - 1) * 4);
            Array.Fill(allowed, true, start, length);
        }
        bool[] owned = new bool[baseline.Length];
        int ownedChangedBytes = 0;
        for (int index = 0; index < ranges.Count; index++)
        {
            Id65AuthoringCompositeOwnedRange range = ranges[index];
            byte[] before = range.CopyBefore();
            byte[] after = range.CopyAfter();
            if (range.OwnerId != OwnerId || range.ByteLength <= 0 ||
                range.ByteLength > 4 || range.DataRelativeOffset < 0 ||
                range.DataRelativeOffset + (long)range.ByteLength >
                    baseline.Length ||
                !baseline.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(before) ||
                !output.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(after) ||
                before.Length != after.Length ||
                before.Where((value, byteIndex) =>
                    value == after[byteIndex]).Any() ||
                index > 0 && ranges[index - 1].DataRelativeOffset +
                    ranges[index - 1].ByteLength > range.DataRelativeOffset)
            {
                throw new InvalidDataException(
                    "A collapsed-cyclorama owned byte range changed identity.");
            }
            for (int byteIndex = 0; byteIndex < range.ByteLength; byteIndex++)
            {
                int absolute = range.DataRelativeOffset + byteIndex;
                if (!allowed[absolute] || owned[absolute])
                {
                    throw new InvalidDataException(
                        "A collapsed-cyclorama owned range escaped or overlapped its vertex bytes.");
                }
                owned[absolute] = true;
                ownedChangedBytes++;
            }
        }
        for (int index = 0; index < baseline.Length; index++)
        {
            if ((baseline[index] != output[index]) != owned[index])
            {
                throw new InvalidDataException(
                    "The collapsed-cyclorama ledger does not exactly own every changed byte.");
            }
        }
        if (ownedChangedBytes != expectedChangedByteCount ||
            ownedChangedBytes != ExpectedChangedLogicalByteCount)
        {
            throw new InvalidDataException(
                "The collapsed-cyclorama owned changed-byte count changed.");
        }
    }

    private static byte[] ApplyRanges(
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
                    "Collapsed-cyclorama range " + range.StableId +
                    " lost its exact preimage.");
            }
            range.CopyAfter().CopyTo(output, range.DataRelativeOffset);
        }
        return output;
    }

    private static string HashOwnedRanges(
        IEnumerable<Id65AuthoringCompositeOwnedRange> ranges)
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
        IEnumerable<Id65AuthoringCompositeOwnedRange> ranges)
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

    private static int CountChangedBytes(
        ReadOnlySpan<byte> before,
        ReadOnlySpan<byte> after)
    {
        if (before.Length != after.Length)
        {
            throw new InvalidDataException(
                "Collapsed-cyclorama changed-byte inputs differ in length.");
        }
        int changed = 0;
        for (int index = 0; index < before.Length; index++)
        {
            if (before[index] != after[index]) changed++;
        }
        return changed;
    }

    private static void RequireHash(
        string actual,
        string expected,
        string label)
    {
        if (!HashEquals(actual, expected))
        {
            throw new InvalidDataException(
                $"The {label} SHA-256 is {actual}, expected {expected}.");
        }
    }

    private static bool HashEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    internal static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private sealed record ProtectedComponentReadback(
        string EnvironmentSha256,
        string OcclusionSha256,
        string OcclusionTailSha256,
        string CollisionSha256,
        string TextureSha256,
        int OcclusionChangedByteCount,
        int OcclusionTailChangedByteCount);
}
