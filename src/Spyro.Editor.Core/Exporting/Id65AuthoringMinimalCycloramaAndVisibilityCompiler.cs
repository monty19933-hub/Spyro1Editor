using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

internal sealed record Id65AuthoringMinimalCycloramaAndVisibilityCompileRequest(
    Id65AuthoringEditorDocument Document,
    Id65AuthoringCompositeLockedSource Source,
    UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan V3Template,
    Id65V2NativeTextureWitness TextureWitness,
    Id65MobyDependencyBundleDescriptor GrassBundle,
    Id65AuthoringTerrainSetLimits? TerrainLimits = null,
    Id65AuthoringCompositeCompilerLimits? CompositeLimits = null);

internal sealed class Id65CompiledAuthoringMinimalCycloramaAndVisibility
{
    private readonly byte[] _schema7BaselineRow80;
    private readonly byte[] _outputRow80;
    private readonly ReadOnlyCollection<Id65AuthoringCompositeOwnedRange>
        _ownedRanges;
    private readonly ReadOnlyCollection<Id65AuthoringCompositeOwnedRange>
        _inverseRanges;

    internal Id65CompiledAuthoringMinimalCycloramaAndVisibility(
        Id65AuthoringEditorDocument document,
        byte[] schema7BaselineRow80,
        byte[] outputRow80,
        IEnumerable<Id65AuthoringCompositeOwnedRange> ownedRanges,
        IEnumerable<Id65AuthoringCompositeOwnedRange> inverseRanges,
        Spyro1SkyBlockLayout baselineSkyLayout,
        Spyro1SkyBlockLayout outputSkyLayout,
        int cycloramaModelOffset,
        int cycloramaRow80Offset,
        int occlusionRow80Offset,
        int visibilityDirectoryRow80Offset,
        string schema7DeterministicPlanSha256,
        string baselineVisibilityDirectorySha256,
        string outputVisibilityDirectorySha256,
        string occlusionPrefixSha256,
        string outputOcclusionSha256,
        string environmentSha256,
        string collisionSha256,
        string textureSha256,
        int ownedByteCount,
        int changedByteCount,
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
        BaselineSkyLayout = DeepCopyLayout(baselineSkyLayout);
        OutputSkyLayout = DeepCopyLayout(outputSkyLayout);
        CycloramaModelOffset = cycloramaModelOffset;
        CycloramaRow80Offset = cycloramaRow80Offset;
        CycloramaWadOffset = checked(
            Id65AuthoringBlankCycloramaCompiler.TargetDataWadOffset +
            cycloramaRow80Offset);
        OcclusionRow80Offset = occlusionRow80Offset;
        VisibilityDirectoryRow80Offset = visibilityDirectoryRow80Offset;
        VisibilityDirectoryWadOffset = checked(
            Id65AuthoringBlankCycloramaCompiler.TargetDataWadOffset +
            visibilityDirectoryRow80Offset);
        Schema7BaselineRow80Sha256 =
            Id65AuthoringMinimalCycloramaAndVisibilityCompiler.Hash(
                _schema7BaselineRow80);
        OutputRow80Sha256 =
            Id65AuthoringMinimalCycloramaAndVisibilityCompiler.Hash(
                _outputRow80);
        Schema7BaselineModelSha256 =
            Id65AuthoringMinimalCycloramaAndVisibilityCompiler.Hash(
                _schema7BaselineRow80.AsSpan(
                    Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset,
                    Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelLength));
        OutputModelSha256 =
            Id65AuthoringMinimalCycloramaAndVisibilityCompiler.Hash(
                _outputRow80.AsSpan(
                    Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset,
                    Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelLength));
        Schema7DeterministicPlanSha256 = schema7DeterministicPlanSha256;
        BaselineVisibilityDirectorySha256 =
            baselineVisibilityDirectorySha256;
        OutputVisibilityDirectorySha256 = outputVisibilityDirectorySha256;
        OcclusionPrefixSha256 = occlusionPrefixSha256;
        OutputOcclusionSha256 = outputOcclusionSha256;
        EnvironmentSha256 = environmentSha256;
        CollisionSha256 = collisionSha256;
        TextureSha256 = textureSha256;
        OwnedByteCount = ownedByteCount;
        ChangedByteCount = changedByteCount;
        OwnedRangeMapSha256 = ownedRangeMapSha256;
        TransactionSha256 = transactionSha256;
        ProtectedComplementSha256 = protectedComplementSha256;
        DeterministicPlanSha256 = deterministicPlanSha256;
    }

    public string ProfileId =>
        Id65AuthoringMinimalCycloramaAndVisibilityCompiler.ProfileId;
    public int SchemaVersion =>
        Id65AuthoringMinimalCycloramaAndVisibilityCompiler.SchemaVersion;
    public string Schema7BaselineProfileId =>
        Id65AuthoringSingleSectorSubstrateCompiler.ProfileId;
    public Id65AuthoringEditorDocument Document { get; }
    public string DocumentCanonicalSha256 { get; }
    public IReadOnlyList<string> ActiveTerrainUnitIds =>
        Document.ActiveTerrainUnitIds;
    public Id65RemotePadMobyKind MobyKind =>
        Document.RemotePadMobyKind ?? throw new InvalidDataException(
            "The schema-8 result lost its Moby kind.");
    public string MobyPositionId =>
        Document.RemotePadMobyPositionId ?? throw new InvalidDataException(
            "The schema-8 result lost its Moby position.");
    public Spyro1SkyBlockLayout BaselineSkyLayout { get; }
    public Spyro1SkyBlockLayout OutputSkyLayout { get; }
    public int CycloramaModelOffset { get; }
    public int CycloramaRow80Offset { get; }
    public long CycloramaWadOffset { get; }
    public int CycloramaByteLength => OutputSkyLayout.ByteLength;
    public int OcclusionRow80Offset { get; }
    public int VisibilityDirectoryRow80Offset { get; }
    public long VisibilityDirectoryWadOffset { get; }
    public int VisibilityDirectoryByteLength =>
        Id65AuthoringMinimalCycloramaAndVisibilityCompiler
            .VisibilityDirectoryByteLength;
    public int ModelRow80Offset =>
        Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset;
    public int ModelByteLength =>
        Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelLength;
    public string Schema7BaselineRow80Sha256 { get; }
    public string OutputRow80Sha256 { get; }
    public string Schema7BaselineModelSha256 { get; }
    public string OutputModelSha256 { get; }
    public string Schema7DeterministicPlanSha256 { get; }
    public string BaselineCycloramaSha256 => BaselineSkyLayout.Sha256;
    public string OutputCycloramaSha256 => OutputSkyLayout.Sha256;
    public string OutputCycloramaGeometrySha256 =>
        OutputSkyLayout.GeometrySha256;
    public string BaselineVisibilityDirectorySha256 { get; }
    public string OutputVisibilityDirectorySha256 { get; }
    public string OcclusionPrefixSha256 { get; }
    public string OutputOcclusionSha256 { get; }
    public string EnvironmentSha256 { get; }
    public string CollisionSha256 { get; }
    public string TextureSha256 { get; }
    public int OwnedByteCount { get; }
    public int ChangedByteCount { get; }
    public int UnchangedOwnedByteCount => OwnedByteCount - ChangedByteCount;
    public IReadOnlyList<Id65AuthoringCompositeOwnedRange> OwnedRanges =>
        _ownedRanges;
    public IReadOnlyList<Id65AuthoringCompositeOwnedRange> InverseRanges =>
        _inverseRanges;
    public string OwnedRangeMapSha256 { get; }
    public string TransactionSha256 { get; }
    public string ProtectedComplementSha256 { get; }
    public string DeterministicPlanSha256 { get; }

    public bool PureInMemory => true;
    public bool ExactTile1GrassCenterSchema7BaselineRequired => true;
    public bool CallerSchema7AfterimageAccepted => false;
    public bool ProducesSchema8AtomicPhysicalStage => true;
    public bool ExactlyTwoFullOwnedRanges => true;
    public bool CycloramaAndVisibilityWrittenAtomically => true;
    public bool FixedCycloramaCapacityPreserved => true;
    public bool DownstreamComponentOffsetsPreserved => true;
    public bool StandaloneSkyReparseVerified => true;
    public bool CodeOwnedMinimalGeometryVerified => true;
    public bool CodeOwnedSkyPartVisibilityVerified => true;
    public bool EveryVisibilityGroupContainsOnlyPartZero => true;
    public bool NoVisibilityReferenceExceedsPartCount => true;
    public bool OcclusionPrefixPreservedExactly => true;
    public bool EnvironmentPreservedExactly => true;
    public bool CollisionPreservedExactly => true;
    public bool TexturePreservedExactly => true;
    public bool SpawnAndMobyBytesPreservedExactly => true;
    public bool ExactInverseVerified => true;
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
    public bool RuntimeVisibilityProven => false;
    public bool ReleaseIntegrated => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;

    internal byte[] CopySchema7BaselineRow80() =>
        _schema7BaselineRow80.ToArray();
    internal byte[] CopyOutputRow80() => _outputRow80.ToArray();
    internal byte[] CopyOutputCyclorama() => _outputRow80.AsSpan(
        CycloramaRow80Offset,
        CycloramaByteLength).ToArray();
    internal byte[] CopyBaselineVisibilityDirectory() =>
        _schema7BaselineRow80.AsSpan(
            VisibilityDirectoryRow80Offset,
            VisibilityDirectoryByteLength).ToArray();
    internal byte[] CopyOutputVisibilityDirectory() => _outputRow80.AsSpan(
        VisibilityDirectoryRow80Offset,
        VisibilityDirectoryByteLength).ToArray();

    private static Spyro1SkyBlockLayout DeepCopyLayout(
        Spyro1SkyBlockLayout layout) => layout with
        {
            Parts = Array.AsReadOnly(
                layout.Parts.Select(item => item with { }).ToArray())
        };
}

/// <summary>
/// Pure in-memory schema-8 physical stage for the exact Tile1 / Grass T107 /
/// Center schema-7 p1 substrate. It atomically replaces the fixed-capacity
/// cyclorama with the code-owned one-part sky and replaces only the 0x254-byte
/// sky-part visibility directory with 16 exact [part 0, terminator] groups.
/// The occlusion prefix and every non-owned component remain byte-exact.
/// This compiler authorizes no disc, UI, runtime, or release path.
/// </summary>
internal static class Id65AuthoringMinimalCycloramaAndVisibilityCompiler
{
    public const string ProfileId =
        "id65-authoring-schema8-minimal-cyclorama-and-sky-visibility-in-memory-p1";
    public const int SchemaVersion = 1;
    public const string VisibilityOwnerId =
        "schema8.sky-part-visibility.directory";
    public const string CycloramaOwnerId =
        "schema8.cyclorama.minimal-geometry";
    public const int SkyPartCount = 1;
    public const int VisibilityGroupCount = 16;
    public const int VisibilityDirectoryOcclusionRelativeOffset = 0x720;
    public const int VisibilityDirectoryByteLength = 0x254;
    public const int VisibilityPointerTableOffset = 4;
    public const int VisibilityGroupDataOffset = 0x44;
    public const int VisibilityGroupByteLength = 2;
    public const int ExpectedVisibilityDirectoryRow80Offset = 0x10A488;
    public const int ExpectedOwnedRangeCount = 2;
    public const int ExpectedOwnedByteCount = 34_616;
    public const int ExpectedChangedByteCount = 32_294;
    public const int ExpectedUnchangedOwnedByteCount = 2_322;
    public const string ExpectedSchema7Tile1GrassCenterRow80Sha256 =
        Id65AuthoringMinimalCycloramaGeometryCompiler
            .ExpectedSchema7Tile1GrassCenterRow80Sha256;
    public const string ExpectedSchema7OcclusionSha256 =
        Id65AuthoringSingleSectorSubstrateCompiler
            .ExpectedOutputOcclusionSha256;
    public const string ExpectedOutputVisibilityDirectorySha256 =
        "3b637120c85f2911983d4cb273660795d6aec926eb659288a96b5a2e2a34d44b";
    public const string ExpectedOutputOcclusionSha256 =
        "ff1ceedfabe5db461bfae794589e1e44eaed981cdb1c6ad7998c18523d80f608";
    public const string ExpectedOutputCycloramaSha256 =
        Id65AuthoringMinimalCycloramaGeometryCompiler
            .ExpectedOutputCycloramaSha256;
    public const string ExpectedOutputCycloramaGeometrySha256 =
        Id65AuthoringMinimalCycloramaGeometryCompiler
            .ExpectedOutputGeometrySha256;
    public const string ExpectedOutputRow80Sha256 =
        "2e0ce4e5faa6249ec1a372db51ea42c4598558a2972b970f988ec1e14b61c91a";

    public static Id65CompiledAuthoringMinimalCycloramaAndVisibility Compile(
        Id65AuthoringMinimalCycloramaAndVisibilityCompileRequest request)
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

    public static Id65CompiledAuthoringMinimalCycloramaAndVisibility
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
            cyclorama.OutputByteLength !=
                Id65AuthoringMinimalCycloramaGeometryCompiler
                    .CycloramaByteLength ||
            cyclorama.OutputSha256 != schema7.OutputCycloramaSha256)
        {
            throw new InvalidDataException(
                "The exact schema-7 cyclorama physical binding changed.");
        }
        byte[] baselineCyclorama = baselineRow80.AsSpan(
            cycloramaRow80Offset,
            cyclorama.OutputByteLength).ToArray();
        Spyro1SkyBlockLayout baselineSkyLayout =
            Spyro1SkyBlockAnalyzer.AnalyzeStandaloneSkyBlock(
                baselineCyclorama);
        byte[] outputCyclorama =
            Id65AuthoringMinimalCycloramaGeometryCompiler
                .BuildCodeOwnedMinimalCyclorama();
        Spyro1SkyBlockLayout outputSkyLayout =
            Spyro1SkyBlockAnalyzer.AnalyzeStandaloneSkyBlock(
                outputCyclorama);
        ValidateOutputSky(outputCyclorama, outputSkyLayout);
        outputCyclorama.CopyTo(outputRow80, cycloramaRow80Offset);

        int occlusionRow80Offset = schema7.Occlusion.Row80Offset;
        int visibilityDirectoryRow80Offset = checked(
            occlusionRow80Offset +
            VisibilityDirectoryOcclusionRelativeOffset);
        if (schema7.Occlusion.ComponentByteLength !=
                Id65AuthoringBlankCycloramaCompiler
                    .ExpectedVisibilityOcclusionByteLength ||
            visibilityDirectoryRow80Offset !=
                ExpectedVisibilityDirectoryRow80Offset ||
            visibilityDirectoryRow80Offset + VisibilityDirectoryByteLength !=
                occlusionRow80Offset + schema7.Occlusion.ComponentByteLength)
        {
            throw new InvalidDataException(
                "The exact schema-7 sky-part visibility physical binding changed.");
        }
        byte[] baselineDirectory = baselineRow80.AsSpan(
            visibilityDirectoryRow80Offset,
            VisibilityDirectoryByteLength).ToArray();
        byte[] outputDirectory = BuildCodeOwnedSkyVisibilityDirectory();
        RequireCodeOwnedSkyVisibilityDirectory(
            outputDirectory,
            outputSkyLayout.PartCount);
        outputDirectory.CopyTo(outputRow80, visibilityDirectoryRow80Offset);

        byte[] baselineOcclusion = baselineRow80.AsSpan(
            occlusionRow80Offset,
            schema7.Occlusion.ComponentByteLength).ToArray();
        byte[] outputOcclusion = outputRow80.AsSpan(
            occlusionRow80Offset,
            schema7.Occlusion.ComponentByteLength).ToArray();
        RequireHash(
            Hash(baselineOcclusion),
            ExpectedSchema7OcclusionSha256,
            "schema-7 occlusion baseline");
        RequireHash(
            Hash(outputOcclusion),
            ExpectedOutputOcclusionSha256,
            "schema-8 output occlusion");
        string occlusionPrefixSha256 = Hash(outputOcclusion.AsSpan(
            0,
            VisibilityDirectoryOcclusionRelativeOffset));
        if (!baselineOcclusion.AsSpan(
                0,
                VisibilityDirectoryOcclusionRelativeOffset).SequenceEqual(
                    outputOcclusion.AsSpan(
                        0,
                        VisibilityDirectoryOcclusionRelativeOffset)))
        {
            throw new InvalidDataException(
                "The schema-8 stage changed the protected occlusion prefix.");
        }

        VerifyOnlyAtomicSpansChanged(
            baselineRow80,
            outputRow80,
            visibilityDirectoryRow80Offset,
            VisibilityDirectoryByteLength,
            cycloramaRow80Offset,
            cyclorama.OutputByteLength);
        VerifyProtectedComponents(schema7, baselineRow80, outputRow80);

        Id65AuthoringCompositeOwnedRange[] ranges =
        [
            new(
                "schema8.row80.sky-part-visibility.directory",
                VisibilityOwnerId,
                visibilityDirectoryRow80Offset,
                baselineDirectory,
                outputDirectory),
            new(
                "schema8.row80.cyclorama.minimal-geometry",
                CycloramaOwnerId,
                cycloramaRow80Offset,
                baselineCyclorama,
                outputCyclorama)
        ];
        Id65AuthoringCompositeOwnedRange[] inverse = ranges
            .Reverse()
            .Select(item => new Id65AuthoringCompositeOwnedRange(
                item.StableId,
                item.OwnerId,
                item.DataRelativeOffset,
                item.CopyAfter(),
                item.CopyBefore()))
            .ToArray();
        (int ownedByteCount, int changedByteCount) = ValidateLedger(
            baselineRow80,
            outputRow80,
            ranges);
        if (ownedByteCount != ExpectedOwnedByteCount ||
            changedByteCount != ExpectedChangedByteCount ||
            ownedByteCount - changedByteCount !=
                ExpectedUnchangedOwnedByteCount)
        {
            throw new InvalidDataException(
                "The schema-8 owned-byte or changed-byte accounting changed.");
        }
        string ownedRangeMapSha256 = HashOwnedRanges(ranges);
        string transactionSha256 = HashTransaction(ranges);
        string protectedComplementSha256 = HashProtectedComplement(
            baselineRow80,
            ranges);
        RequireHash(
            HashProtectedComplement(outputRow80, ranges),
            protectedComplementSha256,
            "schema-8 protected complement");

        string outputRow80Sha256 = Hash(outputRow80);
        RequireHash(
            outputRow80Sha256,
            ExpectedOutputRow80Sha256,
            "schema-8 focused output row");
        string outputModelSha256 = Hash(outputRow80.AsSpan(
            schema7.ModelRow80Offset,
            schema7.ModelByteLength));
        string environmentSha256 = Hash(outputRow80.AsSpan(
            schema7.Environment.Row80Offset,
            schema7.Environment.ComponentByteLength));
        Id65ModelComponentRelocation collision =
            schema7.ComponentRelocations.Single(item =>
                item.StableId == "component.collision");
        string collisionSha256 = Hash(outputRow80.AsSpan(
            schema7.ModelRow80Offset + collision.OutputRelativeOffset,
            collision.OutputByteLength));
        Id65ModelComponentRelocation texture =
            schema7.ComponentRelocations.Single(item =>
                item.StableId == "component.texture");
        string textureSha256 = Hash(outputRow80.AsSpan(
            schema7.ModelRow80Offset + texture.OutputRelativeOffset,
            texture.OutputByteLength));
        string deterministicPlanSha256 = Hash(Encoding.UTF8.GetBytes(
            string.Join('\n',
            [
                ProfileId,
                SchemaVersion.ToString(CultureInfo.InvariantCulture),
                schema7.DocumentCanonicalSha256,
                schema7.ProfileId,
                schema7.DeterministicPlanSha256,
                schema7.OutputRow80Sha256,
                outputRow80Sha256,
                outputModelSha256,
                cycloramaRow80Offset.ToString(CultureInfo.InvariantCulture),
                outputSkyLayout.Sha256,
                outputSkyLayout.GeometrySha256,
                visibilityDirectoryRow80Offset.ToString(
                    CultureInfo.InvariantCulture),
                Hash(baselineDirectory),
                Hash(outputDirectory),
                occlusionPrefixSha256,
                Hash(outputOcclusion),
                environmentSha256,
                collisionSha256,
                textureSha256,
                ranges.Length.ToString(CultureInfo.InvariantCulture),
                ownedByteCount.ToString(CultureInfo.InvariantCulture),
                changedByteCount.ToString(CultureInfo.InvariantCulture),
                ownedRangeMapSha256,
                transactionSha256,
                protectedComplementSha256
            ])));

        Id65CompiledAuthoringMinimalCycloramaAndVisibility compiled = new(
            schema7.Document,
            baselineRow80,
            outputRow80,
            ranges,
            inverse,
            baselineSkyLayout,
            outputSkyLayout,
            cycloramaModelOffset,
            cycloramaRow80Offset,
            occlusionRow80Offset,
            visibilityDirectoryRow80Offset,
            schema7.DeterministicPlanSha256,
            Hash(baselineDirectory),
            Hash(outputDirectory),
            occlusionPrefixSha256,
            Hash(outputOcclusion),
            environmentSha256,
            collisionSha256,
            textureSha256,
            ownedByteCount,
            changedByteCount,
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
                "The schema-8 atomic transaction failed its exact inverse.");
        }
        return compiled;
    }

    public static byte[] ApplySchema7StageTransactional(
        Id65CompiledAuthoringMinimalCycloramaAndVisibility compiled,
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
                ? "The schema-8 atomic rollback afterimage changed."
                : "The exact schema-7 atomic preimage changed.");
        }
        byte[] output = ApplyRanges(
            input,
            reverse ? compiled.InverseRanges : compiled.OwnedRanges);
        byte[] expectedOutput = reverse
            ? compiled.CopySchema7BaselineRow80()
            : compiled.CopyOutputRow80();
        if (!output.SequenceEqual(expectedOutput))
            throw new InvalidDataException(
                "The schema-8 atomic transaction output changed.");
        return output;
    }

    internal static byte[] BuildCodeOwnedSkyVisibilityDirectory()
    {
        byte[] bytes = Enumerable.Repeat(
            (byte)0xFF,
            VisibilityDirectoryByteLength).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(
            bytes.AsSpan(0, 4),
            VisibilityGroupCount);
        for (int group = 0; group < VisibilityGroupCount; group++)
        {
            int pointer = checked(
                VisibilityGroupDataOffset +
                group * VisibilityGroupByteLength);
            BinaryPrimitives.WriteInt32LittleEndian(
                bytes.AsSpan(
                    VisibilityPointerTableOffset + group * 4,
                    4),
                pointer);
            bytes[pointer] = 0;
            bytes[pointer + 1] = 0xFF;
        }
        return bytes;
    }

    internal static void RequireCodeOwnedSkyVisibilityDirectory(
        ReadOnlySpan<byte> bytes,
        int skyPartCount)
    {
        if (bytes.Length != VisibilityDirectoryByteLength ||
            skyPartCount != SkyPartCount ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(0, 4)) !=
                VisibilityGroupCount)
        {
            throw new InvalidDataException(
                "The schema-8 sky-part visibility header changed.");
        }
        for (int group = 0; group < VisibilityGroupCount; group++)
        {
            int pointer = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(
                VisibilityPointerTableOffset + group * 4,
                4));
            int expectedPointer = checked(
                VisibilityGroupDataOffset +
                group * VisibilityGroupByteLength);
            if (pointer != expectedPointer || pointer < 0 ||
                pointer >= bytes.Length)
            {
                throw new InvalidDataException(
                    "A schema-8 sky-part visibility pointer changed.");
            }
            List<int> parts = [];
            int cursor = pointer;
            while (cursor < bytes.Length && bytes[cursor] != 0xFF)
            {
                int part = bytes[cursor++];
                if (part < 0 || part >= skyPartCount)
                    throw new InvalidDataException(
                        "A schema-8 sky-part visibility group references a part outside the one-part sky.");
                parts.Add(part);
            }
            if (cursor >= bytes.Length || !parts.SequenceEqual([0]))
                throw new InvalidDataException(
                    "A schema-8 sky-part visibility group is not exactly [0].");
        }
        int usedEnd = checked(
            VisibilityGroupDataOffset +
            VisibilityGroupCount * VisibilityGroupByteLength);
        if (bytes.Slice(usedEnd).IndexOfAnyExcept((byte)0xFF) >= 0)
            throw new InvalidDataException(
                "The schema-8 sky-part visibility padding changed.");
        RequireHash(
            Hash(bytes),
            ExpectedOutputVisibilityDirectorySha256,
            "schema-8 sky-part visibility directory");
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
                "The schema-8 compiler is bound only to the exact Tile1 / Grass T107 / Center document projection.");
        }
    }

    private static void ValidateSchema7Baseline(
        Id65CompiledAuthoringSingleSectorSubstrate schema7)
    {
        RequireExactDocument(schema7.Document);
        if (schema7.ProfileId !=
                Id65AuthoringSingleSectorSubstrateCompiler.ProfileId ||
            schema7.OutputRow80Sha256 !=
                ExpectedSchema7Tile1GrassCenterRow80Sha256 ||
            schema7.Environment.OutputEnvironmentSha256 !=
                Id65AuthoringMinimalCycloramaGeometryCompiler
                    .ExpectedSchema7Tile1EnvironmentSha256 ||
            schema7.Occlusion.OutputOcclusionSha256 !=
                ExpectedSchema7OcclusionSha256 ||
            schema7.CollisionSha256 !=
                Id65AuthoringMinimalCycloramaGeometryCompiler
                    .ExpectedSchema7Tile1CollisionSha256 ||
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
                "The schema-8 compiler requires the exact guarded Tile1 / Grass T107 / Center schema-7 p1 output.");
        }
    }

    private static void ValidateOutputSky(
        byte[] bytes,
        Spyro1SkyBlockLayout layout)
    {
        if (bytes.Length !=
                Id65AuthoringMinimalCycloramaGeometryCompiler
                    .CycloramaByteLength ||
            layout.PartCount != SkyPartCount ||
            layout.VertexCount !=
                Id65AuthoringMinimalCycloramaGeometryCompiler
                    .MinimalVertexCount ||
            layout.ColorCount !=
                Id65AuthoringMinimalCycloramaGeometryCompiler
                    .MinimalColorCount ||
            layout.PolygonCount !=
                Id65AuthoringMinimalCycloramaGeometryCompiler
                    .MinimalPolygonCount ||
            layout.TrailingBytes !=
                Id65AuthoringMinimalCycloramaGeometryCompiler
                    .MinimalTrailingByteLength)
        {
            throw new InvalidDataException(
                "The schema-8 code-owned one-part sky layout changed.");
        }
        RequireHash(
            layout.Sha256,
            ExpectedOutputCycloramaSha256,
            "schema-8 minimal cyclorama");
        RequireHash(
            layout.GeometrySha256,
            ExpectedOutputCycloramaGeometrySha256,
            "schema-8 minimal cyclorama geometry");
    }

    private static void VerifyProtectedComponents(
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
                if (before.SequenceEqual(after))
                    throw new InvalidDataException(
                        "The schema-8 stage did not replace the cyclorama.");
            }
            else if (component.StableId == "component.occlusion")
            {
                if (!before.Slice(
                        0,
                        VisibilityDirectoryOcclusionRelativeOffset)
                    .SequenceEqual(after.Slice(
                        0,
                        VisibilityDirectoryOcclusionRelativeOffset)) ||
                    before.Slice(VisibilityDirectoryOcclusionRelativeOffset)
                        .SequenceEqual(after.Slice(
                            VisibilityDirectoryOcclusionRelativeOffset)))
                {
                    throw new InvalidDataException(
                        "The schema-8 stage did not isolate the occlusion-directory replacement.");
                }
            }
            else if (!before.SequenceEqual(after))
            {
                throw new InvalidDataException(
                    "The schema-8 stage changed protected " +
                    component.StableId + ".");
            }
        }
    }

    private static void VerifyOnlyAtomicSpansChanged(
        byte[] baseline,
        byte[] output,
        int directoryOffset,
        int directoryLength,
        int skyOffset,
        int skyLength)
    {
        if (baseline.Length != output.Length)
            throw new InvalidDataException(
                "The schema-8 row capacity changed.");
        int directoryEnd = checked(directoryOffset + directoryLength);
        int skyEnd = checked(skyOffset + skyLength);
        for (int index = 0; index < baseline.Length; index++)
        {
            bool owned = index >= directoryOffset && index < directoryEnd ||
                index >= skyOffset && index < skyEnd;
            if (!owned && baseline[index] != output[index])
                throw new InvalidDataException(
                    "The schema-8 stage changed bytes outside its two atomic spans.");
        }
    }

    private static (int OwnedBytes, int ChangedBytes) ValidateLedger(
        byte[] baseline,
        byte[] output,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges)
    {
        if (ranges.Count != ExpectedOwnedRangeCount ||
            ranges.Select(item => item.StableId)
                .Distinct(StringComparer.Ordinal).Count() != ranges.Count ||
            ranges[0].OwnerId != VisibilityOwnerId ||
            ranges[0].ByteLength != VisibilityDirectoryByteLength ||
            ranges[1].OwnerId != CycloramaOwnerId ||
            ranges[1].ByteLength !=
                Id65AuthoringMinimalCycloramaGeometryCompiler
                    .CycloramaByteLength)
        {
            throw new InvalidDataException(
                "The schema-8 atomic ledger lost its exact two full ranges.");
        }
        int owned = 0;
        int changed = 0;
        for (int index = 0; index < ranges.Count; index++)
        {
            Id65AuthoringCompositeOwnedRange range = ranges[index];
            byte[] before = range.CopyBefore();
            byte[] after = range.CopyAfter();
            if (range.DataRelativeOffset < 0 || range.ByteLength <= 0 ||
                range.DataRelativeOffset + (long)range.ByteLength >
                    baseline.Length ||
                !baseline.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(before) ||
                !output.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(after) ||
                before.SequenceEqual(after) ||
                index > 0 && ranges[index - 1].DataRelativeOffset +
                    ranges[index - 1].ByteLength > range.DataRelativeOffset)
            {
                throw new InvalidDataException(
                    "A schema-8 full owned range lost its exact binding.");
            }
            owned = checked(owned + range.ByteLength);
            changed = checked(changed + CountChangedBytes(before, after));
        }
        if (changed != CountChangedBytes(baseline, output))
            throw new InvalidDataException(
                "The schema-8 ranges do not own every changed byte.");
        return (owned, changed);
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
                throw new InvalidDataException(
                    "Schema-8 range " + range.StableId +
                    " lost its exact preimage.");
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
                hash.AppendData(bytes.AsSpan(
                    cursor,
                    range.DataRelativeOffset - cursor));
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
            throw new InvalidDataException(
                "Schema-8 changed-byte inputs differ in length.");
        int changed = 0;
        for (int index = 0; index < before.Length; index++)
            if (before[index] != after[index]) changed++;
        return changed;
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
}
