using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

internal sealed record Id65AuthoringMinimalCycloramaGeometryCompileRequest(
    Id65AuthoringEditorDocument Document,
    Id65AuthoringCompositeLockedSource Source,
    UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan V3Template,
    Id65V2NativeTextureWitness TextureWitness,
    Id65MobyDependencyBundleDescriptor GrassBundle,
    Id65AuthoringTerrainSetLimits? TerrainLimits = null,
    Id65AuthoringCompositeCompilerLimits? CompositeLimits = null);

internal sealed class Id65CompiledAuthoringMinimalCycloramaGeometry
{
    private readonly byte[] _schema7BaselineRow80;
    private readonly byte[] _outputRow80;
    private readonly ReadOnlyCollection<Id65AuthoringCompositeOwnedRange>
        _ownedRanges;
    private readonly ReadOnlyCollection<Id65AuthoringCompositeOwnedRange>
        _inverseRanges;

    internal Id65CompiledAuthoringMinimalCycloramaGeometry(
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
        string collisionSha256,
        string textureSha256,
        string codeOwnedGeometrySha256,
        string zeroTrailingCapacitySha256,
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
        BaselineLayout = DeepCopyLayout(baselineLayout);
        OutputLayout = DeepCopyLayout(outputLayout);
        CycloramaModelOffset = cycloramaModelOffset;
        CycloramaRow80Offset = cycloramaRow80Offset;
        CycloramaWadOffset = checked(
            Id65AuthoringBlankCycloramaCompiler.TargetDataWadOffset +
            cycloramaRow80Offset);
        Schema7BaselineRow80Sha256 =
            Id65AuthoringMinimalCycloramaGeometryCompiler.Hash(
                _schema7BaselineRow80);
        OutputRow80Sha256 =
            Id65AuthoringMinimalCycloramaGeometryCompiler.Hash(_outputRow80);
        Schema7BaselineModelSha256 =
            Id65AuthoringMinimalCycloramaGeometryCompiler.Hash(
                _schema7BaselineRow80.AsSpan(
                    Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset,
                    Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelLength));
        OutputModelSha256 =
            Id65AuthoringMinimalCycloramaGeometryCompiler.Hash(
                _outputRow80.AsSpan(
                    Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset,
                    Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelLength));
        Schema7DeterministicPlanSha256 = schema7DeterministicPlanSha256;
        EnvironmentSha256 = environmentSha256;
        OcclusionSha256 = occlusionSha256;
        CollisionSha256 = collisionSha256;
        TextureSha256 = textureSha256;
        CodeOwnedGeometrySha256 = codeOwnedGeometrySha256;
        ZeroTrailingCapacitySha256 = zeroTrailingCapacitySha256;
        ChangedByteCount = changedByteCount;
        OwnedRangeMapSha256 = ownedRangeMapSha256;
        TransactionSha256 = transactionSha256;
        ProtectedComplementSha256 = protectedComplementSha256;
        DeterministicPlanSha256 = deterministicPlanSha256;
    }

    public string ProfileId =>
        Id65AuthoringMinimalCycloramaGeometryCompiler.ProfileId;
    public int SchemaVersion =>
        Id65AuthoringMinimalCycloramaGeometryCompiler.SchemaVersion;
    public string Schema7BaselineProfileId =>
        Id65AuthoringSingleSectorSubstrateCompiler.ProfileId;
    public Id65AuthoringEditorDocument Document { get; }
    public string DocumentCanonicalSha256 { get; }
    public IReadOnlyList<string> ActiveTerrainUnitIds =>
        Document.ActiveTerrainUnitIds;
    public Id65RemotePadMobyKind MobyKind =>
        Document.RemotePadMobyKind ?? throw new InvalidDataException(
            "The minimal cyclorama result lost its Moby kind.");
    public string MobyPositionId =>
        Document.RemotePadMobyPositionId ?? throw new InvalidDataException(
            "The minimal cyclorama result lost its Moby position.");
    public Spyro1SkyBlockLayout BaselineLayout { get; }
    public Spyro1SkyBlockLayout OutputLayout { get; }
    public int CycloramaModelOffset { get; }
    public int CycloramaRow80Offset { get; }
    public long CycloramaWadOffset { get; }
    public int CycloramaByteLength => OutputLayout.ByteLength;
    public int ModelRow80Offset =>
        Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset;
    public int ModelByteLength =>
        Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelLength;
    public string Schema7BaselineRow80Sha256 { get; }
    public string OutputRow80Sha256 { get; }
    public string Schema7BaselineModelSha256 { get; }
    public string OutputModelSha256 { get; }
    public string Schema7DeterministicPlanSha256 { get; }
    public string BaselineCycloramaSha256 => BaselineLayout.Sha256;
    public string OutputCycloramaSha256 => OutputLayout.Sha256;
    public string CodeOwnedGeometrySha256 { get; }
    public string ZeroTrailingCapacitySha256 { get; }
    public string EnvironmentSha256 { get; }
    public string OcclusionSha256 { get; }
    public string CollisionSha256 { get; }
    public string TextureSha256 { get; }
    public int ChangedByteCount { get; }
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
    public bool FixedCycloramaCapacityPreserved => true;
    public bool DownstreamComponentOffsetsPreserved => true;
    public bool StandaloneSkyReparseVerified => true;
    public bool CodeOwnedMinimalGeometryVerified => true;
    public bool DegenerateZeroGeometryEncoded => true;
    public bool StructuralExperimentOnly => true;
    public bool PreservesSchema7SkyPartOcclusionDirectoryExactly => true;
    public bool SkyPartOcclusionDirectoryRewritten => false;
    public bool SkyPartOcclusionCompatibilityVerified => false;
    public bool RequiresSkyPartOcclusionRewriteBeforeRuntime => true;
    public bool RuntimeVisibilityProven => false;
    public bool EnvironmentPreservedExactly => true;
    public bool OcclusionPreservedExactly => true;
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
/// Pure in-memory schema-7 p2 stage for the focused Tile1 / Grass T107 /
/// Center runtime state. It accepts only the exact schema-7 p1 single-sector
/// substrate and replaces its fixed-capacity inherited 35-part cyclorama with
/// one code-owned degenerate triangle. The block retains its exact 0x84E4
/// capacity so every downstream component remains at the same address.
/// Environment, occlusion, collision, texture, spawn, and Moby bytes are
/// protected complements and remain byte-exact schema-7 p1 output.
/// </summary>
internal static class Id65AuthoringMinimalCycloramaGeometryCompiler
{
    public const string ProfileId =
        "id65-authoring-schema7-single-sector-minimal-cyclorama-in-memory-p2";
    public const int SchemaVersion = 1;
    public const string OwnerId = "schema7.cyclorama.minimal-geometry";
    public const int CycloramaByteLength =
        Id65AuthoringBlankCycloramaCompiler.ExpectedCycloramaByteLength;
    public const int MinimalUsedByteLength = 72;
    public const int MinimalPartCount = 1;
    public const int MinimalVertexCount = 3;
    public const int MinimalColorCount = 3;
    public const int MinimalPolygonCount = 1;
    public const int MinimalPaletteWordCount = 4;
    public const int MinimalTrailingByteLength =
        CycloramaByteLength - MinimalUsedByteLength;
    public const int PartOffset = 16;
    public const int VertexTableOffset = 40;
    public const int ColorTableOffset = 52;
    public const int PolygonTableOffset = 64;
    public const byte BackgroundCommandByte = 0x00;
    public const byte PartColorCommandByte = 0x30;
    public const int ExpectedChangedByteCount = 31_760;
    public const string ExpectedOutputCycloramaSha256 =
        "161dfc0421aa0f8c6c41c345f2907e90fdba4a96385b609e04e23fe70680be28";
    public const string ExpectedOutputGeometrySha256 =
        "4ca6eed94ea67b34d7a36bd8fc8d0f80bd30acd55df1244f5cad29cc25f0aca2";
    public const string ExpectedSchema7Tile1GrassCenterRow80Sha256 =
        "3689e111b027ce07fac6fe0b0f6b1e9a1a3868f4a852b0928678645e7045d819";
    public const string ExpectedSchema7Tile1EnvironmentSha256 =
        "765ca3b55cb72e53305cee6d425802d998ad760661657a4bcc0a2769615ede07";
    public const string ExpectedSchema7Tile1CollisionSha256 =
        "0f6507872d7f29fefd3f70cebcedc8e47b145d08a1cc0ebc48eb47e56ed7cfcb";

    public static Id65CompiledAuthoringMinimalCycloramaGeometry Compile(
        Id65AuthoringMinimalCycloramaGeometryCompileRequest request)
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

    public static Id65CompiledAuthoringMinimalCycloramaGeometry
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
            schema7.OutputCycloramaSha256 != cyclorama.OutputSha256)
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
        if (baselineLayout.PartCount !=
                Id65AuthoringBlankCycloramaCompiler.ExpectedPartCount ||
            baselineLayout.VertexCount !=
                Id65AuthoringBlankCycloramaCompiler.ExpectedVertexCount ||
            baselineLayout.PolygonCount !=
                Id65AuthoringBlankCycloramaCompiler.ExpectedPolygonCount ||
            !string.Equals(
                baselineLayout.Sha256,
                schema7.OutputCycloramaSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The schema-7 p1 inherited cyclorama identity changed.");
        }

        byte[] outputCyclorama = BuildCodeOwnedMinimalCyclorama();
        Spyro1SkyBlockLayout outputLayout =
            Spyro1SkyBlockAnalyzer.AnalyzeStandaloneSkyBlock(outputCyclorama);
        ValidateMinimalLayout(outputCyclorama, outputLayout);
        RequireHash(
            outputLayout.Sha256,
            ExpectedOutputCycloramaSha256,
            "code-owned minimal cyclorama");
        RequireHash(
            outputLayout.GeometrySha256,
            ExpectedOutputGeometrySha256,
            "code-owned minimal cyclorama geometry");
        outputCyclorama.CopyTo(outputRow80, cycloramaRow80Offset);
        VerifyOnlyCycloramaChanged(
            baselineRow80,
            outputRow80,
            cycloramaRow80Offset,
            CycloramaByteLength);
        VerifyProtectedComponents(schema7, baselineRow80, outputRow80);

        Id65AuthoringCompositeOwnedRange[] ranges = BuildLedger(
            baselineRow80,
            outputRow80,
            cycloramaRow80Offset,
            CycloramaByteLength);
        Id65AuthoringCompositeOwnedRange[] inverse = ranges
            .Reverse()
            .Select(item => new Id65AuthoringCompositeOwnedRange(
                item.StableId,
                item.OwnerId,
                item.DataRelativeOffset,
                item.CopyAfter(),
                item.CopyBefore()))
            .ToArray();
        ValidateLedger(baselineRow80, outputRow80, ranges);
        int changedByteCount = CountChangedBytes(
            baselineRow80,
            outputRow80);
        if (changedByteCount != ExpectedChangedByteCount)
            throw new InvalidDataException(
                "The code-owned minimal cyclorama changed-byte count changed.");
        string ownedRangeMapSha256 = HashOwnedRanges(ranges);
        string transactionSha256 = HashTransaction(ranges);
        string protectedComplementSha256 = HashProtectedComplement(
            baselineRow80,
            ranges);
        RequireHash(
            HashProtectedComplement(outputRow80, ranges),
            protectedComplementSha256,
            "minimal cyclorama protected complement");

        string environmentSha256 = Hash(outputRow80.AsSpan(
            schema7.Environment.Row80Offset,
            schema7.Environment.ComponentByteLength));
        string occlusionSha256 = Hash(outputRow80.AsSpan(
            schema7.Occlusion.Row80Offset,
            schema7.Occlusion.ComponentByteLength));
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
        string zeroTrailingCapacitySha256 = Hash(outputCyclorama.AsSpan(
            MinimalUsedByteLength,
            MinimalTrailingByteLength));
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
                cycloramaModelOffset.ToString(CultureInfo.InvariantCulture),
                cycloramaRow80Offset.ToString(CultureInfo.InvariantCulture),
                baselineLayout.Sha256,
                outputLayout.Sha256,
                outputLayout.GeometrySha256,
                zeroTrailingCapacitySha256,
                environmentSha256,
                occlusionSha256,
                collisionSha256,
                textureSha256,
                changedByteCount.ToString(CultureInfo.InvariantCulture),
                ranges.Length.ToString(CultureInfo.InvariantCulture),
                ownedRangeMapSha256,
                transactionSha256,
                protectedComplementSha256
            ])));

        Id65CompiledAuthoringMinimalCycloramaGeometry compiled = new(
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
            environmentSha256,
            occlusionSha256,
            collisionSha256,
            textureSha256,
            outputLayout.GeometrySha256,
            zeroTrailingCapacitySha256,
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
                "The minimal-cyclorama transaction failed its exact inverse.");
        }
        return compiled;
    }

    public static byte[] ApplySchema7StageTransactional(
        Id65CompiledAuthoringMinimalCycloramaGeometry compiled,
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
                ? "The minimal-cyclorama rollback afterimage changed."
                : "The exact schema-7 p1 preimage changed.");
        }
        byte[] output = ApplyRanges(
            input,
            reverse ? compiled.InverseRanges : compiled.OwnedRanges);
        byte[] expectedOutput = reverse
            ? compiled.CopySchema7BaselineRow80()
            : compiled.CopyOutputRow80();
        if (!output.SequenceEqual(expectedOutput))
            throw new InvalidDataException(
                "The minimal-cyclorama transaction output changed.");
        return output;
    }

    internal static byte[] BuildCodeOwnedMinimalCyclorama()
    {
        byte[] bytes = new byte[CycloramaByteLength];
        BinaryPrimitives.WriteInt32LittleEndian(
            bytes.AsSpan(0, 4),
            CycloramaByteLength);
        WritePaletteWord(bytes, 4, BackgroundCommandByte);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8, 4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12, 4), 12);
        BinaryPrimitives.WriteUInt16LittleEndian(
            bytes.AsSpan(PartOffset + 12, 2),
            MinimalVertexCount);
        BinaryPrimitives.WriteUInt16LittleEndian(
            bytes.AsSpan(PartOffset + 16, 2),
            MinimalPolygonCount);
        BinaryPrimitives.WriteUInt16LittleEndian(
            bytes.AsSpan(PartOffset + 18, 2),
            MinimalColorCount);
        BinaryPrimitives.WriteUInt16LittleEndian(
            bytes.AsSpan(PartOffset + 20, 2),
            0xFFFF);
        BinaryPrimitives.WriteUInt16LittleEndian(
            bytes.AsSpan(PartOffset + 22, 2),
            0xFFFF);
        for (int index = 0; index < MinimalColorCount; index++)
            WritePaletteWord(
                bytes,
                ColorTableOffset + index * 4,
                PartColorCommandByte);
        return bytes;
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
                "The minimal cyclorama p2 compiler is bound only to the exact Tile1 / Grass T107 / Center schema-6 document projection.");
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
                ExpectedSchema7Tile1EnvironmentSha256 ||
            schema7.Occlusion.OutputOcclusionSha256 !=
                Id65AuthoringSingleSectorSubstrateCompiler
                    .ExpectedOutputOcclusionSha256 ||
            schema7.CollisionSha256 !=
                ExpectedSchema7Tile1CollisionSha256 ||
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
                "The minimal cyclorama compiler requires the exact guarded Tile1 / Grass T107 / Center schema-7 p1 output.");
        }
    }

    private static void ValidateMinimalLayout(
        byte[] bytes,
        Spyro1SkyBlockLayout layout)
    {
        if (bytes.Length != CycloramaByteLength ||
            layout.Index != 0 || layout.BlockOffset != 0 ||
            layout.ByteLength != CycloramaByteLength ||
            layout.EndOffsetExclusive != CycloramaByteLength ||
            layout.PartCount != MinimalPartCount ||
            layout.VertexCount != MinimalVertexCount ||
            layout.ColorCount != MinimalColorCount ||
            layout.PolygonCount != MinimalPolygonCount ||
            layout.PaletteWordCount != MinimalPaletteWordCount ||
            layout.TrailingBytes != MinimalTrailingByteLength ||
            layout.Parts.Count != 1 ||
            layout.Parts[0].PartOffset != PartOffset ||
            layout.Parts[0].VertexTableOffset != VertexTableOffset ||
            layout.Parts[0].ColorTableOffset != ColorTableOffset ||
            layout.Parts[0].PolygonTableOffset != PolygonTableOffset ||
            bytes.AsSpan(VertexTableOffset, MinimalVertexCount * 4)
                .IndexOfAnyExcept((byte)0) >= 0 ||
            bytes.AsSpan(PolygonTableOffset, MinimalPolygonCount * 8)
                .IndexOfAnyExcept((byte)0) >= 0 ||
            bytes.AsSpan(MinimalUsedByteLength)
                .IndexOfAnyExcept((byte)0) >= 0)
        {
            throw new InvalidDataException(
                "The code-owned minimal cyclorama lost its exact degenerate geometry contract.");
        }
        int[] partPaletteOffsets = layout.Parts.SelectMany(part =>
                Enumerable.Range(0, part.ColorCount)
                    .Select(index => part.ColorTableOffset + index * 4))
            .ToArray();
        int background = layout.BackgroundColorOffset;
        if (partPaletteOffsets.Length != MinimalColorCount ||
            bytes[background] !=
                Id65AuthoringBlankCycloramaCompiler.BlankSlateV1Red ||
            bytes[background + 1] !=
                Id65AuthoringBlankCycloramaCompiler.BlankSlateV1Green ||
            bytes[background + 2] !=
                Id65AuthoringBlankCycloramaCompiler.BlankSlateV1Blue ||
            bytes[background + 3] != BackgroundCommandByte ||
            partPaletteOffsets.Any(offset =>
                bytes[offset] !=
                    Id65AuthoringBlankCycloramaCompiler.BlankSlateV1Red ||
                bytes[offset + 1] !=
                    Id65AuthoringBlankCycloramaCompiler.BlankSlateV1Green ||
                bytes[offset + 2] !=
                    Id65AuthoringBlankCycloramaCompiler.BlankSlateV1Blue ||
                bytes[offset + 3] != PartColorCommandByte))
        {
            throw new InvalidDataException(
                "The code-owned minimal cyclorama palette contract changed.");
        }
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
                if (before.SequenceEqual(after) ||
                    Hash(before) != component.OutputSha256)
                    throw new InvalidDataException(
                        "The minimal stage did not replace the exact schema-7 cyclorama.");
            }
            else if (!before.SequenceEqual(after))
            {
                throw new InvalidDataException(
                    "The minimal stage changed protected " +
                    component.StableId + ".");
            }
        }
    }

    private static void VerifyOnlyCycloramaChanged(
        byte[] baseline,
        byte[] output,
        int offset,
        int length)
    {
        if (baseline.Length != output.Length)
            throw new InvalidDataException(
                "The minimal-cyclorama row capacity changed.");
        int end = checked(offset + length);
        for (int index = 0; index < baseline.Length; index++)
        {
            if ((index < offset || index >= end) &&
                baseline[index] != output[index])
                throw new InvalidDataException(
                    "The minimal stage changed bytes outside the cyclorama.");
        }
    }

    private static Id65AuthoringCompositeOwnedRange[] BuildLedger(
        byte[] baseline,
        byte[] output,
        int offset,
        int length)
    {
        List<Id65AuthoringCompositeOwnedRange> ranges = [];
        int end = checked(offset + length);
        int cursor = offset;
        int part = 0;
        while (cursor < end)
        {
            while (cursor < end && baseline[cursor] == output[cursor])
                cursor++;
            if (cursor == end) break;
            int start = cursor++;
            while (cursor < end && baseline[cursor] != output[cursor])
                cursor++;
            int runLength = cursor - start;
            ranges.Add(new(
                "schema7.p2.cyclorama.diff." +
                    part.ToString("D4", CultureInfo.InvariantCulture),
                OwnerId,
                start,
                baseline.AsSpan(start, runLength).ToArray(),
                output.AsSpan(start, runLength).ToArray()));
            part++;
        }
        return ranges.ToArray();
    }

    private static void ValidateLedger(
        byte[] baseline,
        byte[] output,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges)
    {
        if (ranges.Count == 0 ||
            ranges.Select(item => item.StableId)
                .Distinct(StringComparer.Ordinal).Count() != ranges.Count)
            throw new InvalidDataException(
                "The minimal-cyclorama ledger is empty or duplicated.");
        int changed = 0;
        for (int index = 0; index < ranges.Count; index++)
        {
            Id65AuthoringCompositeOwnedRange range = ranges[index];
            byte[] before = range.CopyBefore();
            byte[] after = range.CopyAfter();
            if (range.OwnerId != OwnerId || range.ByteLength <= 0 ||
                range.DataRelativeOffset < 0 ||
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
                    "A minimal-cyclorama owned range lost its exact binding.");
            }
            changed = checked(changed + CountChangedBytes(before, after));
        }
        if (changed != CountChangedBytes(baseline, output))
            throw new InvalidDataException(
                "The minimal-cyclorama ledger does not own every changed byte.");
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
                    "Minimal-cyclorama range " + range.StableId +
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
                "Minimal-cyclorama changed-byte inputs differ in length.");
        int changed = 0;
        for (int index = 0; index < before.Length; index++)
            if (before[index] != after[index]) changed++;
        return changed;
    }

    private static void WritePaletteWord(
        byte[] bytes,
        int offset,
        byte commandByte)
    {
        bytes[offset] =
            Id65AuthoringBlankCycloramaCompiler.BlankSlateV1Red;
        bytes[offset + 1] =
            Id65AuthoringBlankCycloramaCompiler.BlankSlateV1Green;
        bytes[offset + 2] =
            Id65AuthoringBlankCycloramaCompiler.BlankSlateV1Blue;
        bytes[offset + 3] = commandByte;
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
