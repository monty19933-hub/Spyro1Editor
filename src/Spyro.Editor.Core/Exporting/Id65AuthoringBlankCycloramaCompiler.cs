using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

internal sealed record Id65AuthoringBlankCycloramaCompileRequest(
    Id65AuthoringEditorDocument Document,
    Id65AuthoringCompositeLockedSource Source,
    UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan V3Template,
    Id65V2NativeTextureWitness TextureWitness,
    Id65MobyDependencyBundleDescriptor GrassBundle,
    Id65AuthoringTerrainSetLimits? TerrainLimits = null,
    Id65AuthoringCompositeCompilerLimits? CompositeLimits = null);

internal sealed class Id65CompiledAuthoringBlankCyclorama
{
    private readonly byte[] _sourceRow80;
    private readonly byte[] _baselineRow80;
    private readonly byte[] _outputRow80;
    private readonly ReadOnlyCollection<string> _activeTerrainUnitIds;
    private readonly ReadOnlyCollection<Id65AuthoringCompositeOwnedRange>
        _ownedRanges;
    private readonly ReadOnlyCollection<Id65AuthoringCompositeOwnedRange>
        _inverseRanges;

    internal Id65CompiledAuthoringBlankCyclorama(
        Id65AuthoringEditorDocument document,
        string presetId,
        string colorHex,
        byte[] sourceRow80,
        byte[] baselineRow80,
        byte[] outputRow80,
        IEnumerable<Id65AuthoringCompositeOwnedRange> ownedRanges,
        IEnumerable<Id65AuthoringCompositeOwnedRange> inverseRanges,
        Spyro1SkyBlockLayout baselineLayout,
        Spyro1SkyBlockLayout outputLayout,
        int cycloramaModelOffset,
        int cycloramaRow80Offset,
        int visibilityOcclusionModelOffset,
        int visibilityOcclusionRow80Offset,
        string baselineVisibilityOcclusionSha256,
        string outputVisibilityOcclusionSha256,
        string visibilityOpaqueTailSha256,
        int inheritedVisibleSectorCount,
        int blankVisibilityChangedByteCount,
        string baselineDocumentCanonicalSha256,
        int changedByteCount,
        int cycloramaChangedRgbByteCount,
        string baselineDeterministicPlanSha256,
        string ownedRangeMapSha256,
        string transactionSha256,
        string protectedComplementSha256,
        string nonPaletteSha256,
        string commandByteSha256,
        string deterministicPlanSha256)
    {
        Document = document;
        DocumentCanonicalSha256 = document.CanonicalSha256;
        PresetId = presetId;
        ColorHex = colorHex;
        _activeTerrainUnitIds = Array.AsReadOnly(
            document.ActiveTerrainUnitIds.ToArray());
        _sourceRow80 = sourceRow80.ToArray();
        _baselineRow80 = baselineRow80.ToArray();
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
        VisibilityOcclusionModelOffset = visibilityOcclusionModelOffset;
        VisibilityOcclusionRow80Offset = visibilityOcclusionRow80Offset;
        VisibilityOcclusionWadOffset = checked(
            Id65AuthoringBlankCycloramaCompiler.TargetDataWadOffset +
            visibilityOcclusionRow80Offset);
        BaselineVisibilityOcclusionSha256 =
            baselineVisibilityOcclusionSha256;
        OutputVisibilityOcclusionSha256 = outputVisibilityOcclusionSha256;
        VisibilityOpaqueTailSha256 = visibilityOpaqueTailSha256;
        InheritedVisibleSectorCount = inheritedVisibleSectorCount;
        BlankVisibilityChangedByteCount = blankVisibilityChangedByteCount;
        SourceRow80Sha256 = Id65AuthoringBlankCycloramaCompiler.Hash(_sourceRow80);
        BaselineRow80Sha256 =
            Id65AuthoringBlankCycloramaCompiler.Hash(_baselineRow80);
        OutputRow80Sha256 = Id65AuthoringBlankCycloramaCompiler.Hash(_outputRow80);
        BaselineModelSha256 = Id65AuthoringBlankCycloramaCompiler.Hash(
            _baselineRow80.AsSpan(
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset,
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelLength));
        OutputModelSha256 = Id65AuthoringBlankCycloramaCompiler.Hash(
            _outputRow80.AsSpan(
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset,
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelLength));
        ChangedByteCount = changedByteCount;
        CycloramaChangedRgbByteCount = cycloramaChangedRgbByteCount;
        BaselineDocumentCanonicalSha256 = baselineDocumentCanonicalSha256;
        BaselineDeterministicPlanSha256 = baselineDeterministicPlanSha256;
        OwnedRangeMapSha256 = ownedRangeMapSha256;
        TransactionSha256 = transactionSha256;
        ProtectedComplementSha256 = protectedComplementSha256;
        NonPaletteSha256 = nonPaletteSha256;
        CommandByteSha256 = commandByteSha256;
        DeterministicPlanSha256 = deterministicPlanSha256;
    }

    public string ProfileId => Id65AuthoringBlankCycloramaCompiler.ProfileId;
    public int SchemaVersion => Id65AuthoringBlankCycloramaCompiler.SchemaVersion;
    public int InputDocumentSchemaVersion => Document.SchemaVersion;
    public Id65AuthoringEditorDocument Document { get; }
    public string DocumentCanonicalSha256 { get; }
    public string PresetId { get; }
    public string ColorHex { get; }
    public IReadOnlyList<string> ActiveTerrainUnitIds => _activeTerrainUnitIds;
    public Id65RemotePadMobyKind MobyKind =>
        Document.RemotePadMobyKind ?? throw new InvalidDataException(
            "The compiled blank cyclorama lost its schema-6 Moby kind.");
    public string MobyPositionId =>
        Document.RemotePadMobyPositionId ?? throw new InvalidDataException(
            "The compiled blank cyclorama lost its schema-6 Moby position.");
    public Spyro1SkyBlockLayout BaselineLayout { get; }
    public Spyro1SkyBlockLayout OutputLayout { get; }
    public int CycloramaModelOffset { get; }
    public int CycloramaRow80Offset { get; }
    public long CycloramaWadOffset { get; }
    public int VisibilityOcclusionModelOffset { get; }
    public int VisibilityOcclusionRow80Offset { get; }
    public long VisibilityOcclusionWadOffset { get; }
    public int VisibilityOcclusionByteLength =>
        Id65AuthoringBlankCycloramaCompiler.ExpectedVisibilityOcclusionByteLength;
    public string BaselineVisibilityOcclusionSha256 { get; }
    public string OutputVisibilityOcclusionSha256 { get; }
    public string VisibilityOpaqueTailSha256 { get; }
    public int InheritedVisibleSectorCount { get; }
    public int VisibleAuthoredSectorIndex =>
        Id65AuthoringBlankCycloramaCompiler.AuthoredEnvironmentSectorIndex;
    public int VisibleSectorCount => 1;
    public int BlankVisibilityChangedByteCount { get; }
    public int CycloramaByteLength => OutputLayout.ByteLength;
    public int PaletteWordCount => OutputLayout.PaletteWordCount;
    public int PaletteOwnedRangeCount => _ownedRanges.Count(item =>
        item.OwnerId == Id65AuthoringBlankCycloramaCompiler.PaletteOwnerId);
    public string SourceRow80Sha256 { get; }
    public string BaselineRow80Sha256 { get; }
    public string OutputRow80Sha256 { get; }
    public string BaselineModelSha256 { get; }
    public string OutputModelSha256 { get; }
    public string BaselineCycloramaSha256 => BaselineLayout.Sha256;
    public string OutputCycloramaSha256 => OutputLayout.Sha256;
    public string GeometrySha256 => OutputLayout.GeometrySha256;
    public int ChangedByteCount { get; }
    public int CycloramaChangedRgbByteCount { get; }
    public IReadOnlyList<Id65AuthoringCompositeOwnedRange> OwnedRanges =>
        _ownedRanges;
    public IReadOnlyList<Id65AuthoringCompositeOwnedRange> InverseRanges =>
        _inverseRanges;
    public string BaselineDeterministicPlanSha256 { get; }
    public string BaselineDocumentCanonicalSha256 { get; }
    public string OwnedRangeMapSha256 { get; }
    public string TransactionSha256 { get; }
    public string ProtectedComplementSha256 { get; }
    public string NonPaletteSha256 { get; }
    public string CommandByteSha256 { get; }
    public string DeterministicPlanSha256 { get; }

    public bool PureInMemory => true;
    public bool ExactSchema6DocumentRequired => true;
    public bool ReadsBlankCycloramaIntentFromDocument => true;
    public bool CodeOwnedBlankSlateV1IntentRequired => true;
    public bool BuildsSchema5BaselineFromLockedInputs => true;
    public bool CallerBaselineAccepted => false;
    public bool StandaloneSkyReparseVerified => true;
    public bool FrozenGeometryVerified => true;
    public bool EveryPaletteWordOwnedExactlyOnce => true;
    public bool PaletteCommandBytesPreserved => true;
    // This remains a cyclorama-specific assertion. The separate, explicitly
    // owned visibility map is intentionally changed below.
    public bool EveryNonPaletteBytePreserved => true;
    public bool AuthoredOnlyVisibilityMapVerified => true;
    public bool InheritedEnvironmentVisibilitySuppressed => true;
    public bool VisibilityOpaqueTailPreserved => true;
    public bool InheritedEnvironmentBytesRetired => false;
    public bool InheritedCollisionLookupsRetired => false;
    public bool ExactInverseVerified => true;
    public bool EveryChangedByteOwned => true;
    public bool ProtectedComplementVerified => true;
    public bool ContainsOutputRow80Bytes => true;
    public bool CallsNativeSkyPatchExporter => false;
    public bool LinkedPrimarySkyCopiesTouched => false;
    public bool RetailArtisansTouched => false;
    public bool RetailTownSquareTouched => false;
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
    internal byte[] CopyBaselineRow80() => _baselineRow80.ToArray();
    internal byte[] CopyOutputRow80() => _outputRow80.ToArray();

    private static Spyro1SkyBlockLayout DeepCopyLayout(
        Spyro1SkyBlockLayout layout) => layout with
        {
            Parts = Array.AsReadOnly(
                layout.Parts.Select(item => item with { }).ToArray())
        };
}

/// <summary>
/// Pure in-memory schema-6 compiler for the first code-owned blank cyclorama
/// intent. It projects the document's terrain/Moby/T66 portion to the exact
/// schema-5 physical baseline, changes the RGB bytes of the standalone
/// cyclorama's 727 parsed palette words, and replaces the fixed WAD80
/// environment-visibility directory with an authored-only group containing
/// sector 216. It never follows linked retail sky copies. Environment bytes
/// and inherited collision lookups remain present but are not claimed retired.
/// </summary>
internal static class Id65AuthoringBlankCycloramaCompiler
{
    public const string ProfileId =
        "id65-authoring-schema6-blank-cyclorama-visibility-in-memory-p2";
    public const int SchemaVersion = 1;
    public const int ExpectedStateCount = 108;
    public const string BlankSlateV1PresetId =
        Id65AuthoringBlankCycloramaCatalog.BlankSlatePresetId;
    public const string BlankSlateV1ColorHex =
        Id65AuthoringBlankCycloramaCatalog.BlankSlateColorHex;
    public const byte BlankSlateV1Red = 0x26;
    public const byte BlankSlateV1Green = 0x34;
    public const byte BlankSlateV1Blue = 0x47;
    public const string PaletteOwnerId = "schema6.cyclorama.palette";
    public const int AuthoredEnvironmentSectorIndex = 216;
    public const int ExpectedVisibilityOcclusionByteLength = 0x974;
    public const int ExpectedVisibilityEnvironmentPortionByteLength = 0x71C;
    public const int ExpectedVisibilityGroupCount = 16;
    public const int ExpectedInheritedVisibleSectorCount = 124;
    public const int ExpectedBaselineVisibleSectorCount = 125;
    public const int ExpectedOutputVisibleSectorCount = 1;
    public const int VisibilityOpaqueTailOffset = 0x720;
    public const int VisibilityOpaqueTailByteLength = 0x254;
    public const string ExpectedBaselineVisibilityOcclusionSha256 =
        "34c97013761d4ec0833745c3be420a0c416b1a7d6912070af5ce76ce00135ccd";
    public const string ExpectedOutputVisibilityOcclusionSha256 =
        "d920dacd4c6e2587f1f675bf320b5e25fba4e086e7916865954266dec4b6c144";
    public const string ExpectedVisibilityOpaqueTailSha256 =
        "660d21b9b9c402c46f27e3cdf4810733f2788251406654b8a6854342f9669c41";
    public const int ExpectedCycloramaByteLength = 0x84E4;
    public const int ExpectedPartCount = 35;
    public const int ExpectedVertexCount = 1915;
    public const int ExpectedColorCount = 726;
    public const int ExpectedPolygonCount = 2808;
    public const int ExpectedPaletteWordCount = 727;
    public const int ExpectedTrailingBytes = 0;
    public const string ExpectedSourceCycloramaSha256 =
        "8E8C62273AB0D4EA691A40409D5E4BE77FE578CFEBCFB2E3C6BB374CAD7D40BB";
    public const string ExpectedGeometrySha256 =
        "405BACB0D5F058C8FF876E14C41FCD7C6C6A0C57535BD451FFA49C5FB82E4B9A";
    public const int EmptyTerrainCycloramaModelOffset = 0x8B9D4;
    public const int OneTerrainCycloramaModelOffset = 0x8BA04;
    public const int TwoTerrainCycloramaModelOffset = 0x8BA30;
    public const int EmptyTerrainCycloramaRow80Offset = 0x16A1D4;
    public const int OneTerrainCycloramaRow80Offset = 0x16A204;
    public const int TwoTerrainCycloramaRow80Offset = 0x16A230;
    public const long TargetDataWadOffset = 0x6936800;
    public const long EmptyTerrainCycloramaWadOffset = 0x6AA09D4;
    public const long OneTerrainCycloramaWadOffset = 0x6AA0A04;
    public const long TwoTerrainCycloramaWadOffset = 0x6AA0A30;
    public const long RetailTownSquareCycloramaWadOffset = 0x13080A8;
    public const long RetailArtisansLinkedCycloramaWadOffset = 0x96A7EC;

    public static Id65CompiledAuthoringBlankCyclorama Compile(
        Id65AuthoringBlankCycloramaCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Document);
        ArgumentNullException.ThrowIfNull(request.Source);
        ArgumentNullException.ThrowIfNull(request.V3Template);
        ArgumentNullException.ThrowIfNull(request.TextureWitness);
        ArgumentNullException.ThrowIfNull(request.GrassBundle);
        if (!request.Document.UsesBlankCyclorama ||
            request.Document.SchemaVersion !=
                Id65AuthoringEditorDocument
                    .RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion ||
            request.Document.BlankCyclorama is null)
        {
            throw new InvalidDataException(
                "The blank-cyclorama compiler requires the exact schema-6 document family.");
        }
        Id65AuthoringBlankCycloramaCatalog.RequireExact(
            request.Document.BlankCyclorama);
        Id65AuthoringBlankCycloramaIntent intent =
            request.Document.BlankCyclorama;
        if (intent.PresetId != BlankSlateV1PresetId ||
            intent.ColorHex != BlankSlateV1ColorHex)
        {
            throw new InvalidDataException(
                "The schema-6 document lost its code-owned blank-slate-v1 intent.");
        }

        Id65AuthoringEditorDocument schema5BaselineDocument =
            Id65AuthoringEditorDocument
                .CreateRemotePadMobyChoiceTerrainSetExact(
                    request.Document.RemotePadMobyKind ??
                        throw new InvalidDataException(
                            "The schema-6 document lost its Moby choice."),
                    request.Document.RemotePadMobyPositionId ??
                        throw new InvalidDataException(
                            "The schema-6 document lost its Moby position."),
                    request.Document.ActiveTerrainUnitIds);

        Id65CompiledAuthoringRemotePadTerrainSetPhysical baseline =
            Id65AuthoringRemotePadTerrainSetPhysicalCompiler.Compile(new(
                schema5BaselineDocument,
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
        ValidateBaseline(
            request,
            schema5BaselineDocument,
            baseline,
            terrain,
            manifest);

        Id65AuthoringTerrainSetCompiler.ComponentState cyclorama =
            terrain.CopyComponents().Single(item => item.Name == "cyclorama");
        Id65AuthoringTerrainSetCompiler.ComponentState occlusion =
            terrain.CopyComponents().Single(item => item.Name == "occlusion");
        int cycloramaModelOffset = checked(
            cyclorama.Offset +
            Id65V2NativeTextureCompositionCompiler.RecordGrowthBytes);
        int cycloramaRow80Offset = checked(
            Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset +
            cycloramaModelOffset);
        ValidatePinnedOffsets(
            manifest.ActiveUnits.Count,
            cycloramaModelOffset,
            cycloramaRow80Offset);
        int visibilityOcclusionModelOffset = checked(
            occlusion.Offset +
            Id65V2NativeTextureCompositionCompiler.RecordGrowthBytes);
        int visibilityOcclusionRow80Offset = checked(
            Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset +
            visibilityOcclusionModelOffset);
        ValidateVisibilityOcclusionOffsets(
            manifest.ActiveUnits.Count,
            visibilityOcclusionModelOffset,
            visibilityOcclusionRow80Offset);

        byte[] sourceRow80 = baseline.CopySourceRow80();
        byte[] baselineRow80 = baseline.CopyOutputRow80();
        byte[] baselineCyclorama = baselineRow80.AsSpan(
            cycloramaRow80Offset,
            cyclorama.ByteLength).ToArray();
        if (!baselineCyclorama.SequenceEqual(cyclorama.Bytes) ||
            !string.Equals(
                HashUpper(baselineCyclorama),
                ExpectedSourceCycloramaSha256,
                StringComparison.Ordinal) ||
            !string.Equals(
                cyclorama.Sha256,
                ExpectedSourceCycloramaSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The relocated schema-5 cyclorama lost its exact locked Town Square source identity.");
        }

        Spyro1SkyBlockLayout baselineLayout =
            Spyro1SkyBlockAnalyzer.AnalyzeStandaloneSkyBlock(
                baselineCyclorama);
        ValidateLayout(baselineLayout, requireSourceHash: true);
        int[] paletteLocalOffsets = BuildPaletteWordOffsets(baselineLayout);
        byte[] outputRow80 = baselineRow80.ToArray();
        byte[] baselineVisibilityOcclusion = baselineRow80.AsSpan(
            visibilityOcclusionRow80Offset,
            occlusion.ByteLength).ToArray();
        if (occlusion.ByteLength != ExpectedVisibilityOcclusionByteLength ||
            !baselineVisibilityOcclusion.SequenceEqual(occlusion.Bytes) ||
            Hash(baselineVisibilityOcclusion) !=
                ExpectedBaselineVisibilityOcclusionSha256)
        {
            throw new InvalidDataException(
                "The relocated schema-5 visibility occlusion lost its exact inherited-sector identity.");
        }
        (byte[] outputVisibilityOcclusion,
            int inheritedVisibleSectorCount,
            string visibilityOpaqueTailSha256) =
            BuildAuthoredOnlyVisibilityOcclusion(
                baselineVisibilityOcclusion);
        outputVisibilityOcclusion.CopyTo(
            outputRow80,
            visibilityOcclusionRow80Offset);
        byte[] outputCyclorama = baselineCyclorama.ToArray();
        foreach (int offset in paletteLocalOffsets)
        {
            if (outputCyclorama[offset] == BlankSlateV1Red &&
                outputCyclorama[offset + 1] == BlankSlateV1Green &&
                outputCyclorama[offset + 2] == BlankSlateV1Blue)
            {
                throw new InvalidDataException(
                    "A baseline cyclorama RGB tuple already equals the blank-slate-v1 color.");
            }
            outputCyclorama[offset] = BlankSlateV1Red;
            outputCyclorama[offset + 1] = BlankSlateV1Green;
            outputCyclorama[offset + 2] = BlankSlateV1Blue;
        }
        outputCyclorama.CopyTo(outputRow80, cycloramaRow80Offset);

        Spyro1SkyBlockLayout outputLayout =
            Spyro1SkyBlockAnalyzer.AnalyzeStandaloneSkyBlock(outputCyclorama);
        ValidateLayout(outputLayout, requireSourceHash: false);
        VerifyPaletteOnlyMutation(
            baselineCyclorama,
            outputCyclorama,
            paletteLocalOffsets);
        VerifyBlankBackgroundMutationEnvelope(
            baselineRow80,
            outputRow80,
            visibilityOcclusionRow80Offset,
            outputVisibilityOcclusion.Length,
            cycloramaRow80Offset,
            paletteLocalOffsets);
        int[] paletteRow80Offsets = paletteLocalOffsets
            .Select(offset => checked(cycloramaRow80Offset + offset))
            .ToArray();

        List<Id65AuthoringCompositeOwnedRange> ledger = BuildOwnedLedger(
            sourceRow80,
            outputRow80,
            baseline.OwnedRanges,
            paletteRow80Offsets,
            visibilityOcclusionRow80Offset,
            outputVisibilityOcclusion.Length);
        ValidateOwnedLedger(
            sourceRow80,
            outputRow80,
            ledger,
            paletteRow80Offsets,
            visibilityOcclusionRow80Offset,
            outputVisibilityOcclusion.Length);
        Id65AuthoringCompositeOwnedRange[] inverse = ledger
            .Reverse<Id65AuthoringCompositeOwnedRange>()
            .Select(item => new Id65AuthoringCompositeOwnedRange(
                item.StableId,
                item.OwnerId,
                item.DataRelativeOffset,
                item.CopyAfter(),
                item.CopyBefore()))
            .ToArray();
        byte[] applied = ApplyRanges(sourceRow80, ledger);
        byte[] rolledBack = ApplyRanges(applied, inverse);
        if (!applied.SequenceEqual(outputRow80) ||
            !rolledBack.SequenceEqual(sourceRow80))
        {
            throw new InvalidDataException(
                "The blank-cyclorama owned transaction failed its exact inverse.");
        }

        string ownedRangeMapSha256 = HashOwnedRanges(ledger);
        string transactionSha256 = HashTransaction(ledger);
        string protectedComplementSha256 =
            HashProtectedComplement(sourceRow80, ledger);
        if (HashProtectedComplement(outputRow80, ledger) !=
            protectedComplementSha256)
        {
            throw new InvalidDataException(
                "The blank-cyclorama locked-row protected complement changed.");
        }
        // This final-row identity intentionally includes the separately owned
        // authored-only visibility map while excluding only palette RGB bytes.
        string nonPaletteSha256 = HashOutsideRgb(
            outputRow80,
            paletteRow80Offsets);
        string commandByteSha256 = HashCommandBytes(
            baselineRow80,
            paletteRow80Offsets);
        if (HashCommandBytes(outputRow80, paletteRow80Offsets) !=
            commandByteSha256)
        {
            throw new InvalidDataException(
                "A blank-cyclorama palette command byte changed.");
        }
        int changedByteCount = CountChangedBytes(sourceRow80, outputRow80);
        int cycloramaChangedRgbByteCount = CountChangedBytes(
            baselineCyclorama,
            outputCyclorama);
        int blankVisibilityChangedByteCount = CountChangedBytes(
            baselineVisibilityOcclusion,
            outputVisibilityOcclusion);
        if (cycloramaChangedRgbByteCount <= 0 ||
            cycloramaChangedRgbByteCount > ExpectedPaletteWordCount * 3 ||
            blankVisibilityChangedByteCount <= 0)
        {
            throw new InvalidDataException(
                "The blank-cyclorama RGB or authored-visibility mutation cardinality changed.");
        }
        string deterministicPlanSha256 = Hash(Encoding.UTF8.GetBytes(
            string.Join('\n',
            [
                ProfileId,
                SchemaVersion.ToString(CultureInfo.InvariantCulture),
                request.Document.CanonicalSha256,
                intent.PresetId,
                intent.ColorHex,
                schema5BaselineDocument.CanonicalSha256,
                baseline.DeterministicPlanSha256,
                manifest.CanonicalSha256,
                cycloramaModelOffset.ToString(CultureInfo.InvariantCulture),
                cycloramaRow80Offset.ToString(CultureInfo.InvariantCulture),
                visibilityOcclusionModelOffset.ToString(
                    CultureInfo.InvariantCulture),
                visibilityOcclusionRow80Offset.ToString(
                    CultureInfo.InvariantCulture),
                Hash(baselineVisibilityOcclusion),
                Hash(outputVisibilityOcclusion),
                visibilityOpaqueTailSha256,
                inheritedVisibleSectorCount.ToString(
                    CultureInfo.InvariantCulture),
                AuthoredEnvironmentSectorIndex.ToString(
                    CultureInfo.InvariantCulture),
                baselineLayout.Sha256,
                outputLayout.Sha256,
                outputLayout.GeometrySha256,
                changedByteCount.ToString(CultureInfo.InvariantCulture),
                cycloramaChangedRgbByteCount.ToString(CultureInfo.InvariantCulture),
                blankVisibilityChangedByteCount.ToString(
                    CultureInfo.InvariantCulture),
                ledger.Count.ToString(CultureInfo.InvariantCulture),
                ownedRangeMapSha256,
                transactionSha256,
                protectedComplementSha256,
                nonPaletteSha256,
                commandByteSha256,
                Hash(outputRow80)
            ])));

        Id65CompiledAuthoringBlankCyclorama compiled = new(
            request.Document,
            intent.PresetId,
            intent.ColorHex,
            sourceRow80,
            baselineRow80,
            outputRow80,
            ledger,
            inverse,
            baselineLayout,
            outputLayout,
            cycloramaModelOffset,
            cycloramaRow80Offset,
            visibilityOcclusionModelOffset,
            visibilityOcclusionRow80Offset,
            Hash(baselineVisibilityOcclusion),
            Hash(outputVisibilityOcclusion),
            visibilityOpaqueTailSha256,
            inheritedVisibleSectorCount,
            blankVisibilityChangedByteCount,
            schema5BaselineDocument.CanonicalSha256,
            changedByteCount,
            cycloramaChangedRgbByteCount,
            baseline.DeterministicPlanSha256,
            ownedRangeMapSha256,
            transactionSha256,
            protectedComplementSha256,
            nonPaletteSha256,
            commandByteSha256,
            deterministicPlanSha256);
        if (!ApplyTransactional(
                compiled,
                compiled.CopySourceRow80(),
                reverse: false).SequenceEqual(outputRow80) ||
            !ApplyTransactional(
                compiled,
                compiled.CopyOutputRow80(),
                reverse: true).SequenceEqual(sourceRow80))
        {
            throw new InvalidDataException(
                "The blank-cyclorama public transaction failed its exact round trip.");
        }
        return compiled;
    }

    public static byte[] ApplyTransactional(
        Id65CompiledAuthoringBlankCyclorama compiled,
        byte[] input,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(compiled);
        ArgumentNullException.ThrowIfNull(input);
        byte[] expectedInput = reverse
            ? compiled.CopyOutputRow80()
            : compiled.CopySourceRow80();
        if (!input.SequenceEqual(expectedInput))
        {
            throw new InvalidDataException(reverse
                ? "The blank-cyclorama rollback afterimage changed."
                : "The blank-cyclorama locked-source preimage changed.");
        }
        byte[] output = ApplyRanges(
            input,
            reverse ? compiled.InverseRanges : compiled.OwnedRanges);
        byte[] expectedOutput = reverse
            ? compiled.CopySourceRow80()
            : compiled.CopyOutputRow80();
        if (!output.SequenceEqual(expectedOutput))
        {
            throw new InvalidDataException(
                "The blank-cyclorama transaction output changed.");
        }
        return output;
    }

    private static void ValidateBaseline(
        Id65AuthoringBlankCycloramaCompileRequest request,
        Id65AuthoringEditorDocument schema5BaselineDocument,
        Id65CompiledAuthoringRemotePadTerrainSetPhysical baseline,
        Id65CompiledAuthoringTerrainSet terrain,
        Id65AuthoringTerrainSetManifest manifest)
    {
        if (request.Document.SchemaVersion !=
                Id65AuthoringEditorDocument
                    .RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion ||
            !request.Document.UsesBlankCyclorama ||
            schema5BaselineDocument.SchemaVersion !=
                Id65AuthoringEditorDocument
                    .RemotePadMobyChoiceTerrainSetSchemaVersion ||
            baseline.DocumentCanonicalSha256 !=
                schema5BaselineDocument.CanonicalSha256 ||
            !baseline.ActiveTerrainUnitIds.SequenceEqual(
                request.Document.ActiveTerrainUnitIds) ||
            baseline.Terrain.TerrainManifestSha256 != manifest.CanonicalSha256 ||
            terrain.ManifestSha256 != manifest.CanonicalSha256 ||
            baseline.SourceRow80Sha256 != request.Source.Row80Sha256 ||
            !baseline.PureInMemory ||
            !baseline.BuildsEveryTargetFromLockedV3Source ||
            !baseline.ExactInverseVerified ||
            !baseline.EveryChangedByteOwned ||
            baseline.WritesFileSystem || baseline.WritesDiscImage ||
            baseline.CreateBinEnabled || baseline.RuntimeCandidateAuthorized ||
            baseline.ReleaseIntegrated || baseline.Publishable ||
            terrain.WritesFileSystem || terrain.WritesDiscImage ||
            terrain.MaterializerIntegrated || terrain.CreateBinEnabled ||
            terrain.RuntimeCandidateAuthorized || terrain.ReleaseIntegrated ||
            terrain.Publishable)
        {
            throw new InvalidDataException(
                "The blank-cyclorama compiler lost its exact pure schema-5 locked-input baseline.");
        }
    }

    private static void ValidatePinnedOffsets(
        int activeTerrainUnitCount,
        int modelOffset,
        int row80Offset)
    {
        (int ExpectedModel, int ExpectedRow, long ExpectedWad) expected =
            activeTerrainUnitCount switch
            {
                0 => (EmptyTerrainCycloramaModelOffset,
                    EmptyTerrainCycloramaRow80Offset,
                    EmptyTerrainCycloramaWadOffset),
                1 => (OneTerrainCycloramaModelOffset,
                    OneTerrainCycloramaRow80Offset,
                    OneTerrainCycloramaWadOffset),
                2 => (TwoTerrainCycloramaModelOffset,
                    TwoTerrainCycloramaRow80Offset,
                    TwoTerrainCycloramaWadOffset),
                _ => throw new InvalidDataException(
                    "The schema-5 terrain topology exceeds two units.")
            };
        if (modelOffset != expected.ExpectedModel ||
            row80Offset != expected.ExpectedRow ||
            TargetDataWadOffset + row80Offset != expected.ExpectedWad ||
            expected.ExpectedWad is RetailTownSquareCycloramaWadOffset or
                RetailArtisansLinkedCycloramaWadOffset ||
            row80Offset !=
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset +
                    modelOffset)
        {
            throw new InvalidDataException(
                "The schema-6 blank-cyclorama relocation offset changed or entered a retail sky address: " +
                $"active={activeTerrainUnitCount}, model=0x{modelOffset:X}/0x{expected.ExpectedModel:X}, " +
                $"row80=0x{row80Offset:X}/0x{expected.ExpectedRow:X}, " +
                $"wad=0x{TargetDataWadOffset + row80Offset:X}/0x{expected.ExpectedWad:X}.");
        }
    }

    private static void ValidateVisibilityOcclusionOffsets(
        int activeTerrainUnitCount,
        int modelOffset,
        int row80Offset)
    {
        if (activeTerrainUnitCount is < 0 or > 2)
            throw new InvalidDataException(
                "The schema-6 visibility topology exceeds two terrain units.");
        int expectedModel = checked(0x2B548 + activeTerrainUnitCount * 0x20);
        int expectedRow = checked(
            Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset +
            expectedModel);
        long wadOffset = checked(TargetDataWadOffset + row80Offset);
        if (modelOffset != expectedModel || row80Offset != expectedRow ||
            wadOffset != TargetDataWadOffset + expectedRow ||
            wadOffset is RetailTownSquareCycloramaWadOffset or
                RetailArtisansLinkedCycloramaWadOffset)
        {
            throw new InvalidDataException(
                "The schema-6 authored-only visibility occlusion offset changed or entered a retail address: " +
                $"active={activeTerrainUnitCount}, model=0x{modelOffset:X}/0x{expectedModel:X}, " +
                $"row80=0x{row80Offset:X}/0x{expectedRow:X}, wad=0x{wadOffset:X}.");
        }
    }

    private static (byte[] Output, int InheritedVisibleSectorCount,
        string OpaqueTailSha256) BuildAuthoredOnlyVisibilityOcclusion(
        byte[] baseline)
    {
        if (baseline.Length != ExpectedVisibilityOcclusionByteLength ||
            BinaryPrimitives.ReadInt32LittleEndian(baseline.AsSpan(0, 4)) !=
                ExpectedVisibilityOcclusionByteLength ||
            BinaryPrimitives.ReadInt32LittleEndian(baseline.AsSpan(4, 4)) !=
                ExpectedVisibilityEnvironmentPortionByteLength ||
            BinaryPrimitives.ReadInt32LittleEndian(baseline.AsSpan(8, 4)) !=
                ExpectedVisibilityGroupCount ||
            Hash(baseline) != ExpectedBaselineVisibilityOcclusionSha256)
        {
            throw new InvalidDataException(
                "The exact schema-5 inherited visibility directory changed.");
        }

        IReadOnlyList<int>[] baselineGroups = ParseVisibilityGroups(
            baseline,
            AuthoredEnvironmentSectorIndex + 1);
        int[] inheritedVisible = baselineGroups[0]
            .Where(index => index != AuthoredEnvironmentSectorIndex)
            .ToArray();
        if (baselineGroups[0].Count != ExpectedBaselineVisibleSectorCount ||
            baselineGroups[0][^1] != AuthoredEnvironmentSectorIndex ||
            inheritedVisible.Length != ExpectedInheritedVisibleSectorCount ||
            inheritedVisible.Distinct().Count() != inheritedVisible.Length ||
            baselineGroups[0].Count(index =>
                index == AuthoredEnvironmentSectorIndex) != 1)
        {
            throw new InvalidDataException(
                "The exact inherited group-0 visibility membership changed.");
        }

        byte[] opaqueTail = baseline.AsSpan(
            VisibilityOpaqueTailOffset,
            VisibilityOpaqueTailByteLength).ToArray();
        string opaqueTailSha256 = Hash(opaqueTail);
        if (opaqueTailSha256 != ExpectedVisibilityOpaqueTailSha256)
            throw new InvalidDataException(
                "The fixed occlusion opaque tail changed before authored-only visibility projection.");

        byte[] output = Enumerable.Repeat(
            (byte)0xFF,
            ExpectedVisibilityOcclusionByteLength).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(
            output.AsSpan(0, 4),
            ExpectedVisibilityOcclusionByteLength);
        BinaryPrimitives.WriteInt32LittleEndian(
            output.AsSpan(4, 4),
            ExpectedVisibilityEnvironmentPortionByteLength);
        BinaryPrimitives.WriteInt32LittleEndian(
            output.AsSpan(8, 4),
            ExpectedVisibilityGroupCount);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(12, 4), 0x48);
        output[0x4C] = checked((byte)AuthoredEnvironmentSectorIndex);
        int nextRelative = 0x4A;
        for (int group = 1; group < ExpectedVisibilityGroupCount; group++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(
                output.AsSpan(12 + group * 4, 4),
                nextRelative++);
        }
        opaqueTail.CopyTo(output, VisibilityOpaqueTailOffset);

        IReadOnlyList<int>[] outputGroups = ParseVisibilityGroups(
            output,
            AuthoredEnvironmentSectorIndex + 1);
        if (!outputGroups[0].SequenceEqual(
                new[] { AuthoredEnvironmentSectorIndex }) ||
            outputGroups.Skip(1).Any(group => group.Count != 0) ||
            !output.AsSpan(
                    VisibilityOpaqueTailOffset,
                    VisibilityOpaqueTailByteLength)
                .SequenceEqual(opaqueTail) ||
            Hash(output) != ExpectedOutputVisibilityOcclusionSha256)
        {
            throw new InvalidDataException(
                "The authored-only visibility directory failed exact semantic readback.");
        }
        RequireAuthoredOnlyVisibilityOcclusion(output);
        return (output, inheritedVisible.Length, opaqueTailSha256);
    }

    internal static void RequireAuthoredOnlyVisibilityOcclusion(
        ReadOnlySpan<byte> bytes)
    {
        byte[] component = bytes.ToArray();
        if (component.Length != ExpectedVisibilityOcclusionByteLength ||
            Hash(component) != ExpectedOutputVisibilityOcclusionSha256 ||
            BinaryPrimitives.ReadInt32LittleEndian(component.AsSpan(0, 4)) !=
                ExpectedVisibilityOcclusionByteLength ||
            BinaryPrimitives.ReadInt32LittleEndian(component.AsSpan(4, 4)) !=
                ExpectedVisibilityEnvironmentPortionByteLength ||
            BinaryPrimitives.ReadInt32LittleEndian(component.AsSpan(8, 4)) !=
                ExpectedVisibilityGroupCount ||
            Hash(component.AsSpan(
                VisibilityOpaqueTailOffset,
                VisibilityOpaqueTailByteLength)) !=
                ExpectedVisibilityOpaqueTailSha256)
        {
            throw new InvalidDataException(
                "The authored-only visibility component lost its exact header, output, or opaque-tail identity.");
        }
        IReadOnlyList<int>[] groups = ParseVisibilityGroups(
            component,
            AuthoredEnvironmentSectorIndex + 1);
        if (!groups[0].SequenceEqual(
                new[] { AuthoredEnvironmentSectorIndex }) ||
            groups.Skip(1).Any(group => group.Count != 0))
        {
            throw new InvalidDataException(
                "The authored-only visibility component exposed an inherited environment sector.");
        }
    }

    private static IReadOnlyList<int>[] ParseVisibilityGroups(
        byte[] component,
        int sectorCount)
    {
        int environmentEnd = checked(
            4 + BinaryPrimitives.ReadInt32LittleEndian(
                component.AsSpan(4, 4)));
        int groupCount = BinaryPrimitives.ReadInt32LittleEndian(
            component.AsSpan(8, 4));
        if (component.Length != ExpectedVisibilityOcclusionByteLength ||
            environmentEnd != VisibilityOpaqueTailOffset ||
            groupCount != ExpectedVisibilityGroupCount ||
            12 + groupCount * 4 > environmentEnd)
        {
            throw new InvalidDataException(
                "The visibility group directory header is invalid.");
        }
        IReadOnlyList<int>[] groups = new IReadOnlyList<int>[groupCount];
        for (int group = 0; group < groupCount; group++)
        {
            int relative = BinaryPrimitives.ReadInt32LittleEndian(
                component.AsSpan(12 + group * 4, 4));
            int offset = checked(4 + relative);
            if (offset < 12 + groupCount * 4 || offset >= environmentEnd)
                throw new InvalidDataException(
                    "A visibility group points outside its directory.");
            List<int> sectors = [];
            while (offset < environmentEnd && component[offset] != 0xFF)
            {
                int sector = component[offset++];
                if (sector < 0 || sector >= sectorCount)
                    throw new InvalidDataException(
                        "A visibility group references an invalid environment sector.");
                sectors.Add(sector);
            }
            if (offset >= environmentEnd)
                throw new InvalidDataException(
                    "A visibility group lacks its exact terminator.");
            groups[group] = sectors;
        }
        return groups;
    }

    private static void ValidateLayout(
        Spyro1SkyBlockLayout layout,
        bool requireSourceHash)
    {
        if (layout.Index != 0 || layout.BlockOffset != 0 ||
            layout.ByteLength != ExpectedCycloramaByteLength ||
            layout.EndOffsetExclusive != ExpectedCycloramaByteLength ||
            layout.PartCount != ExpectedPartCount ||
            layout.VertexCount != ExpectedVertexCount ||
            layout.ColorCount != ExpectedColorCount ||
            layout.PolygonCount != ExpectedPolygonCount ||
            layout.PaletteWordCount != ExpectedPaletteWordCount ||
            layout.TrailingBytes != ExpectedTrailingBytes ||
            layout.Parts.Count != ExpectedPartCount ||
            !string.Equals(
                layout.GeometrySha256,
                ExpectedGeometrySha256,
                StringComparison.Ordinal) ||
            requireSourceHash && !string.Equals(
                layout.Sha256,
                ExpectedSourceCycloramaSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The standalone ID65 cyclorama layout or frozen geometry identity changed.");
        }
    }

    private static int[] BuildPaletteWordOffsets(
        Spyro1SkyBlockLayout layout)
    {
        int[] offsets =
        [
            layout.BackgroundColorOffset,
            .. layout.Parts.SelectMany(part =>
                Enumerable.Range(0, part.ColorCount)
                    .Select(index => part.ColorTableOffset + (index * 4)))
        ];
        int[] sorted = offsets
            .Order()
            .Distinct()
            .ToArray();
        if (offsets.Length != ExpectedPaletteWordCount ||
            sorted.Length != ExpectedPaletteWordCount ||
            sorted.Any(offset =>
                offset < 0 || offset + 4 > layout.ByteLength ||
                (offset & 3) != 0))
        {
            throw new InvalidDataException(
                "The standalone cyclorama palette-word map changed.");
        }
        return sorted;
    }

    private static void VerifyPaletteOnlyMutation(
        byte[] baseline,
        byte[] output,
        IReadOnlyList<int> paletteLocalOffsets)
    {
        if (baseline.Length != ExpectedCycloramaByteLength ||
            output.Length != ExpectedCycloramaByteLength)
        {
            throw new InvalidDataException(
                "The blank-cyclorama RGB verification received the wrong block length.");
        }
        bool[] mutableRgb = new bool[baseline.Length];
        foreach (int localOffset in paletteLocalOffsets)
        {
            int offset = localOffset;
            mutableRgb[offset] = true;
            mutableRgb[offset + 1] = true;
            mutableRgb[offset + 2] = true;
            if (output[offset] != BlankSlateV1Red ||
                output[offset + 1] != BlankSlateV1Green ||
                output[offset + 2] != BlankSlateV1Blue ||
                output[offset + 3] != baseline[offset + 3])
            {
                throw new InvalidDataException(
                    "A blank-cyclorama palette word lost its exact RGB/command contract.");
            }
        }
        for (int index = 0; index < baseline.Length; index++)
        {
            if (!mutableRgb[index] && baseline[index] != output[index])
            {
                throw new InvalidDataException(
                    "The blank-cyclorama compiler changed a nonpalette byte.");
            }
        }
    }

    private static void VerifyBlankBackgroundMutationEnvelope(
        byte[] baseline,
        byte[] output,
        int visibilityOcclusionOffset,
        int visibilityOcclusionLength,
        int cycloramaOffset,
        IReadOnlyList<int> paletteLocalOffsets)
    {
        if (baseline.Length != output.Length ||
            visibilityOcclusionOffset < 0 ||
            visibilityOcclusionLength != ExpectedVisibilityOcclusionByteLength ||
            visibilityOcclusionOffset + visibilityOcclusionLength >
                baseline.Length)
        {
            throw new InvalidDataException(
                "The blank-background mutation envelope is out of bounds.");
        }
        bool[] mutable = new bool[baseline.Length];
        Array.Fill(
            mutable,
            true,
            visibilityOcclusionOffset,
            visibilityOcclusionLength);
        foreach (int localOffset in paletteLocalOffsets)
        {
            int offset = checked(cycloramaOffset + localOffset);
            mutable[offset] = true;
            mutable[offset + 1] = true;
            mutable[offset + 2] = true;
        }
        for (int index = 0; index < baseline.Length; index++)
        {
            if (!mutable[index] && baseline[index] != output[index])
            {
                throw new InvalidDataException(
                    "The schema-6 blank-background stage changed a byte outside its visibility/palette envelope.");
            }
        }
    }

    private static List<Id65AuthoringCompositeOwnedRange> BuildOwnedLedger(
        byte[] source,
        byte[] output,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> baselineRanges,
        IReadOnlyList<int> paletteOffsets,
        int visibilityOcclusionOffset,
        int visibilityOcclusionLength)
    {
        (int Start, int End)[] replacementSpans =
            new[]
            {
                (visibilityOcclusionOffset,
                    checked(visibilityOcclusionOffset +
                        visibilityOcclusionLength))
            }
            .Concat(paletteOffsets.Select(offset =>
                (offset, checked(offset + 4))))
            .OrderBy(item => item.Item1)
            .ToArray();
        List<Id65AuthoringCompositeOwnedRange> ranges = [];
        foreach (Id65AuthoringCompositeOwnedRange baseline in baselineRanges)
        {
            int start = baseline.DataRelativeOffset;
            int end = checked(start + baseline.ByteLength);
            int cursor = start;
            List<(int Start, int Length)> residuals = [];
            foreach ((int replacementStart, int replacementEnd) in
                     replacementSpans)
            {
                if (replacementEnd <= cursor) continue;
                if (replacementStart >= end) break;
                if (replacementStart > cursor)
                {
                    residuals.Add((
                        cursor,
                        Math.Min(replacementStart, end) - cursor));
                }
                cursor = Math.Max(cursor, Math.Min(replacementEnd, end));
                if (cursor >= end) break;
            }
            if (cursor < end)
                residuals.Add((cursor, end - cursor));

            bool unchangedShape = residuals.Count == 1 &&
                residuals[0].Start == start &&
                residuals[0].Length == baseline.ByteLength;
            int part = 0;
            foreach ((int residualStart, int length) in residuals)
            {
                byte[] before = source.AsSpan(residualStart, length).ToArray();
                byte[] after = output.AsSpan(residualStart, length).ToArray();
                if (before.SequenceEqual(after)) continue;
                string stableId = unchangedShape
                    ? baseline.StableId
                    : baseline.StableId + ".blank-cyclorama-part-" +
                        part.ToString("D3", CultureInfo.InvariantCulture);
                ranges.Add(new(
                    stableId,
                    baseline.OwnerId,
                    residualStart,
                    before,
                    after));
                part++;
            }
        }
        int visibilityEnd = checked(
            visibilityOcclusionOffset + visibilityOcclusionLength);
        int visibilityCursor = visibilityOcclusionOffset;
        int visibilityPart = 0;
        while (visibilityCursor < visibilityEnd)
        {
            while (visibilityCursor < visibilityEnd &&
                   source[visibilityCursor] == output[visibilityCursor])
            {
                visibilityCursor++;
            }
            if (visibilityCursor == visibilityEnd) break;
            int start = visibilityCursor;
            while (visibilityCursor < visibilityEnd &&
                   source[visibilityCursor] != output[visibilityCursor])
            {
                visibilityCursor++;
            }
            int length = visibilityCursor - start;
            ranges.Add(new(
                "schema5.row80.blank-visibility." +
                    visibilityPart.ToString("D3", CultureInfo.InvariantCulture),
                "model.remote-pad.schema5-terrain-set",
                start,
                source.AsSpan(start, length).ToArray(),
                output.AsSpan(start, length).ToArray()));
            visibilityPart++;
        }
        if (visibilityPart == 0)
            throw new InvalidDataException(
                "The authored-only visibility projection produced no owned row delta.");
        for (int index = 0; index < paletteOffsets.Count; index++)
        {
            int offset = paletteOffsets[index];
            byte[] before = source.AsSpan(offset, 4).ToArray();
            byte[] after = output.AsSpan(offset, 4).ToArray();
            if (before.SequenceEqual(after))
            {
                throw new InvalidDataException(
                    "A code-owned blank-cyclorama palette word produced no locked-row transaction delta.");
            }
            ranges.Add(new(
                "schema6.cyclorama.palette." +
                    index.ToString("D4", CultureInfo.InvariantCulture),
                PaletteOwnerId,
                offset,
                before,
                after));
        }
        return ranges
            .OrderBy(item => item.DataRelativeOffset)
            .ThenBy(item => item.StableId, StringComparer.Ordinal)
            .ToList();
    }

    private static void ValidateOwnedLedger(
        byte[] source,
        byte[] output,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ledger,
        IReadOnlyList<int> paletteOffsets,
        int visibilityOcclusionOffset,
        int visibilityOcclusionLength)
    {
        Id65AuthoringCompositeOwnedRange[] paletteRanges = ledger
            .Where(item => item.OwnerId == PaletteOwnerId)
            .ToArray();
        Id65AuthoringCompositeOwnedRange[] visibilityRanges = ledger
            .Where(item => item.StableId.StartsWith(
                "schema5.row80.blank-visibility.",
                StringComparison.Ordinal))
            .ToArray();
        int visibilityEnd = checked(
            visibilityOcclusionOffset + visibilityOcclusionLength);
        if (ledger.Count == 0 ||
            ledger.Select(item => item.StableId)
                .Distinct(StringComparer.Ordinal).Count() != ledger.Count ||
            paletteRanges.Length != ExpectedPaletteWordCount ||
            !paletteRanges.Select(item => item.DataRelativeOffset)
                .SequenceEqual(paletteOffsets) ||
            !paletteRanges.Select(item => item.StableId).SequenceEqual(
                Enumerable.Range(0, ExpectedPaletteWordCount).Select(index =>
                    "schema6.cyclorama.palette." +
                    index.ToString("D4", CultureInfo.InvariantCulture))) ||
            paletteRanges.Any(item => item.ByteLength != 4) ||
            visibilityRanges.Length == 0 ||
            visibilityRanges.Any(item =>
                item.OwnerId != "model.remote-pad.schema5-terrain-set" ||
                item.DataRelativeOffset < visibilityOcclusionOffset ||
                item.DataRelativeOffset + item.ByteLength > visibilityEnd))
        {
            throw new InvalidDataException(
                "The blank-cyclorama ledger lost unique or exact palette ownership.");
        }
        int changed = 0;
        for (int index = 0; index < ledger.Count; index++)
        {
            Id65AuthoringCompositeOwnedRange range = ledger[index];
            byte[] before = range.CopyBefore();
            byte[] after = range.CopyAfter();
            if (range.DataRelativeOffset < 0 || range.ByteLength <= 0 ||
                range.DataRelativeOffset + (long)range.ByteLength > source.Length ||
                !source.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(before) ||
                !output.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(after) ||
                before.SequenceEqual(after) ||
                Hash(before) != range.BeforeSha256 ||
                Hash(after) != range.AfterSha256 ||
                index > 0 &&
                    ledger[index - 1].DataRelativeOffset +
                        ledger[index - 1].ByteLength >
                    range.DataRelativeOffset)
            {
                throw new InvalidDataException(
                    "A blank-cyclorama owned range lost bounds, identity, hash, or nonoverlap.");
            }
            changed = checked(changed + CountChangedBytes(before, after));
        }
        if (changed != CountChangedBytes(source, output))
        {
            throw new InvalidDataException(
                "The blank-cyclorama ledger does not own every changed byte.");
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
                    "Blank-cyclorama range " + range.StableId +
                    " lost its exact preimage.");
            }
            range.CopyAfter().CopyTo(output, range.DataRelativeOffset);
        }
        return output;
    }

    private static int CountChangedBytes(
        ReadOnlySpan<byte> before,
        ReadOnlySpan<byte> after)
    {
        if (before.Length != after.Length)
            throw new InvalidDataException(
                "Blank-cyclorama changed-byte inputs differ in length.");
        int changed = 0;
        for (int index = 0; index < before.Length; index++)
        {
            if (before[index] != after[index]) changed++;
        }
        return changed;
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
        if (cursor < bytes.Length)
            hash.AppendData(bytes.AsSpan(cursor));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static string HashOutsideRgb(
        byte[] bytes,
        IReadOnlyList<int> paletteOffsets)
    {
        using IncrementalHash hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int cursor = 0;
        foreach (int offset in paletteOffsets)
        {
            if (offset > cursor)
                hash.AppendData(bytes.AsSpan(cursor, offset - cursor));
            cursor = offset + 3;
        }
        if (cursor < bytes.Length)
            hash.AppendData(bytes.AsSpan(cursor));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private static string HashCommandBytes(
        byte[] bytes,
        IReadOnlyList<int> paletteOffsets)
    {
        byte[] commands = paletteOffsets
            .Select(offset => bytes[offset + 3])
            .ToArray();
        return Hash(commands);
    }

    internal static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string HashUpper(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));
}
