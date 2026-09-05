using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Persistence;

internal sealed record Id65AuthoringBlankCycloramaAggregatePlanRequest(
    Id65AuthoringEditorDocumentStoreResult StoredDocument,
    string ExpectedEnvelopeCanonicalSha256,
    UnusedLevel65BlankLevelLabManifest LabManifest,
    Id65AuthoringEditorNativeInputProvider.InputSet NativeInputs,
    UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan V3Template);

internal enum Id65AuthoringBlankCycloramaAggregateRangeDomain
{
    NativeTexturePages,
    TerrainSetModel,
    Spawn,
    Grass,
    RedGem,
    CycloramaPalette
}

/// <summary>
/// One defensive leaf of the schema-6 WAD80 transaction. The leaves are an
/// audit partition of one indivisible transaction and do not authorize partial
/// application.
/// </summary>
internal sealed class Id65AuthoringBlankCycloramaAggregateOwnedRange
{
    private readonly byte[] _before;
    private readonly byte[] _after;

    internal Id65AuthoringBlankCycloramaAggregateOwnedRange(
        string stableId,
        string ownerId,
        Id65AuthoringBlankCycloramaAggregateRangeDomain domain,
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
        BeforeSha256 = Id65AuthoringBlankCycloramaAggregatePlanProjector.Hash(
            _before);
        AfterSha256 = Id65AuthoringBlankCycloramaAggregatePlanProjector.Hash(
            _after);
    }

    public string StableId { get; }
    public string OwnerId { get; }
    public Id65AuthoringBlankCycloramaAggregateRangeDomain Domain { get; }
    public int TargetWadEntry => 80;
    public int DataRelativeOffset { get; }
    public long WadOffset =>
        Id65AuthoringBlankCycloramaAggregatePlanProjector.TargetDataWadOffset +
        DataRelativeOffset;
    public int ByteLength => _before.Length;
    public string BeforeSha256 { get; }
    public string AfterSha256 { get; }

    internal byte[] CopyBefore() => _before.ToArray();
    internal byte[] CopyAfter() => _after.ToArray();
    internal Id65AuthoringBlankCycloramaAggregateOwnedRange DeepCopy() => new(
        StableId,
        OwnerId,
        Domain,
        DataRelativeOffset,
        _before,
        _after);
    internal Id65AuthoringBlankCycloramaAggregateOwnedRange Invert() => new(
        StableId,
        OwnerId,
        Domain,
        DataRelativeOffset,
        _after,
        _before);
}

/// <summary>
/// Schema-6-specific physical destination boundary. WAD80 is the only writable
/// logical file; the listed retail, overlay, donor, and executable regions are
/// explicit preservation obligations.
/// </summary>
internal sealed record Id65AuthoringBlankCycloramaAggregateDestinationBinding(
    string SourceSnapshotProfileId,
    string SourceImageSha256,
    string LockedBaseProfileId,
    string LockedBaseImageSha256,
    string WadFileName,
    int TargetDataWadEntry,
    long TargetDataWadOffset,
    int TargetDataByteLength,
    string TargetDataDirectoryRowSha256,
    long TargetObjectTableWadOffset,
    long TargetObjectTableRelativeOffset,
    int ProtectedId65OverlayWadEntry,
    long ProtectedId65OverlayWadOffset,
    int ProtectedId65OverlayByteLength,
    int ProtectedTownSquareOverlayWadEntry,
    long ProtectedTownSquareOverlayWadOffset,
    int ProtectedTownSquareOverlayByteLength,
    int ProtectedTownSquareDataWadEntry,
    long ProtectedTownSquareDataWadOffset,
    int ProtectedTownSquareDataByteLength,
    string ProtectedTownSquareDataDirectoryRowSha256,
    int ProtectedArtisansOverlayWadEntry,
    long ProtectedArtisansOverlayWadOffset,
    int ProtectedArtisansOverlayByteLength,
    int ProtectedArtisansDataWadEntry,
    long ProtectedArtisansDataWadOffset,
    int ProtectedArtisansDataByteLength,
    int ProtectedExecutableLba,
    int ProtectedExecutableByteLength,
    string ProtectedExecutableSha256,
    int TextureDonorWadEntry,
    int GrassDonorWadEntry,
    bool PhysicalSnapshotVerified,
    bool Wad80SoleWritableDestination,
    bool Wad79Protected,
    bool RetailTownSquareWad15Protected,
    bool RetailTownSquareWad16Protected,
    bool RetailArtisansWad9Protected,
    bool RetailArtisansWad10Protected,
    bool ExecutableProtected,
    bool DonorLocationsReadOnlyProvenance,
    bool LinkedPrimarySkyCopiesNotFollowed,
    bool NativeSkyPatchExporterNotUsed,
    bool PhysicalDestinationRevalidationRequired);

/// <summary>
/// Immutable schema-6 aggregate transaction derived from one exact stored
/// document and provider-owned locked inputs. It carries guarded afterimages,
/// hashes, and preservation claims only; it owns no writer or runtime authority.
/// </summary>
internal sealed class Id65AuthoringBlankCycloramaAggregatePlan
{
    private readonly ReadOnlyCollection<
        Id65AuthoringBlankCycloramaAggregateOwnedRange> _ownedRanges;
    private readonly ReadOnlyCollection<
        Id65AuthoringBlankCycloramaAggregateOwnedRange> _inverseRanges;
    private readonly ReadOnlyCollection<Id65AuthoringEditorAggregateProtectedRange>
        _protectedRanges;
    private readonly ReadOnlyCollection<Id65AuthoringEditorAggregateAtomicGroup>
        _atomicGroups;
    private readonly ReadOnlyCollection<string> _activeTerrainUnitIds;
    private readonly Spyro1SkyBlockLayout _outputCycloramaLayout;

    internal Id65AuthoringBlankCycloramaAggregatePlan(
        object? projectorConstructionAuthority,
        Id65AuthoringBlankCycloramaAggregateDestinationBinding destination,
        Id65AuthoringEditorDocument document,
        string envelopeCanonicalSha256,
        string staticAuthoringProfileId,
        string staticAuthoringCatalogSha256,
        string staticAuthoringSceneGraphSha256,
        string staticAuthoringProjectionSha256,
        string staticAuthoringTerrainManifestSha256,
        string staticAuthoringCycloramaCatalogSha256,
        string nativeInputSetSha256,
        string v3DeterministicPlanSha256,
        string sourceRow80Sha256,
        string baselineRow80Sha256,
        string outputRow80Sha256,
        string baselineModelSha256,
        string outputModelSha256,
        string baselineDocumentCanonicalSha256,
        string baselineDeterministicPlanSha256,
        string presetId,
        string colorHex,
        int cycloramaModelOffset,
        int cycloramaRow80Offset,
        Spyro1SkyBlockLayout outputCycloramaLayout,
        string baselineCycloramaSha256,
        string outputCycloramaSha256,
        string geometrySha256,
        string nonPaletteSha256,
        string commandByteSha256,
        int cycloramaChangedRgbByteCount,
        string compilerOwnedRangeMapSha256,
        string compilerTransactionSha256,
        string compilerProtectedComplementSha256,
        string compilerDeterministicPlanSha256,
        IEnumerable<Id65AuthoringBlankCycloramaAggregateOwnedRange> ownedRanges,
        IEnumerable<Id65AuthoringBlankCycloramaAggregateOwnedRange> inverseRanges,
        IEnumerable<Id65AuthoringEditorAggregateProtectedRange> protectedRanges,
        IEnumerable<Id65AuthoringEditorAggregateAtomicGroup> atomicGroups,
        int guardedByteCount,
        int protectedByteCount,
        int changedByteCount,
        string protectedComplementSha256)
    {
        Id65AuthoringBlankCycloramaAggregatePlanProjector
            .RequireConstructionAuthority(projectorConstructionAuthority);
        Destination = destination;
        Document = Id65AuthoringEditorDocument.Parse(document.SerializeUtf8());
        DocumentCanonicalSha256 = Document.CanonicalSha256;
        _activeTerrainUnitIds = Array.AsReadOnly(
            Document.ActiveTerrainUnitIds.ToArray());
        EnvelopeCanonicalSha256 = envelopeCanonicalSha256;
        StaticAuthoringProfileId = staticAuthoringProfileId;
        StaticAuthoringCatalogSha256 = staticAuthoringCatalogSha256;
        StaticAuthoringSceneGraphSha256 = staticAuthoringSceneGraphSha256;
        StaticAuthoringProjectionSha256 = staticAuthoringProjectionSha256;
        StaticAuthoringTerrainManifestSha256 =
            staticAuthoringTerrainManifestSha256;
        StaticAuthoringCycloramaCatalogSha256 =
            staticAuthoringCycloramaCatalogSha256;
        NativeInputSetSha256 = nativeInputSetSha256;
        V3DeterministicPlanSha256 = v3DeterministicPlanSha256;
        SourceRow80Sha256 = sourceRow80Sha256;
        BaselineRow80Sha256 = baselineRow80Sha256;
        OutputRow80Sha256 = outputRow80Sha256;
        BaselineModelSha256 = baselineModelSha256;
        OutputModelSha256 = outputModelSha256;
        BaselineDocumentCanonicalSha256 = baselineDocumentCanonicalSha256;
        BaselineDeterministicPlanSha256 = baselineDeterministicPlanSha256;
        PresetId = presetId;
        ColorHex = colorHex;
        CycloramaModelOffset = cycloramaModelOffset;
        CycloramaRow80Offset = cycloramaRow80Offset;
        CycloramaWadOffset = checked(
            Id65AuthoringBlankCycloramaAggregatePlanProjector.TargetDataWadOffset +
            cycloramaRow80Offset);
        _outputCycloramaLayout = DeepCopyLayout(outputCycloramaLayout);
        BaselineCycloramaSha256 = baselineCycloramaSha256;
        OutputCycloramaSha256 = outputCycloramaSha256;
        GeometrySha256 = geometrySha256;
        NonPaletteSha256 = nonPaletteSha256;
        CommandByteSha256 = commandByteSha256;
        CycloramaChangedRgbByteCount = cycloramaChangedRgbByteCount;
        CompilerOwnedRangeMapSha256 = compilerOwnedRangeMapSha256;
        CompilerTransactionSha256 = compilerTransactionSha256;
        CompilerProtectedComplementSha256 = compilerProtectedComplementSha256;
        CompilerDeterministicPlanSha256 = compilerDeterministicPlanSha256;
        _ownedRanges = Array.AsReadOnly(
            ownedRanges.Select(item => item.DeepCopy()).ToArray());
        _inverseRanges = Array.AsReadOnly(
            inverseRanges.Select(item => item.DeepCopy()).ToArray());
        _protectedRanges = Array.AsReadOnly(protectedRanges.ToArray());
        _atomicGroups = Array.AsReadOnly(atomicGroups.ToArray());
        GuardedByteCount = guardedByteCount;
        ProtectedByteCount = protectedByteCount;
        ChangedByteCount = changedByteCount;
        ProtectedComplementSha256 = protectedComplementSha256;
        OwnedRangeMapSha256 =
            Id65AuthoringBlankCycloramaAggregatePlanProjector.HashOwnedRanges(
                _ownedRanges);
        InverseRangeMapSha256 =
            Id65AuthoringBlankCycloramaAggregatePlanProjector.HashOwnedRanges(
                _inverseRanges);
        ProtectedRangeMapSha256 =
            Id65AuthoringBlankCycloramaAggregatePlanProjector
                .HashProtectedRanges(_protectedRanges);
        AtomicGroupMapSha256 =
            Id65AuthoringBlankCycloramaAggregatePlanProjector
                .HashAtomicGroups(_atomicGroups);
        DeterministicAggregatePlanSha256 =
            Id65AuthoringBlankCycloramaAggregatePlanProjector
                .ComputeDeterministicPlanSha256(this);
    }

    public string ProfileId =>
        Id65AuthoringBlankCycloramaAggregatePlanProjector.ProfileId;
    public int SchemaVersion =>
        Id65AuthoringBlankCycloramaAggregatePlanProjector.SchemaVersion;
    public int LevelId => 65;
    public int DataWadEntry => 80;
    public Id65AuthoringBlankCycloramaAggregateDestinationBinding Destination { get; }
    public Id65AuthoringEditorDocument Document { get; }
    public string DocumentCanonicalSha256 { get; }
    public IReadOnlyList<string> ActiveTerrainUnitIds => _activeTerrainUnitIds;
    public int TerrainUnitCount => _activeTerrainUnitIds.Count;
    public Id65RemotePadMobyKind MobyKind => Document.RemotePadMobyKind!.Value;
    public string MobyPositionId => Document.RemotePadMobyPositionId!;
    public string TerrainManifestSha256 => Document.TerrainSetManifestSha256!;
    public string EnvelopeCanonicalSha256 { get; }
    public string StaticAuthoringProfileId { get; }
    public string StaticAuthoringCatalogSha256 { get; }
    public string StaticAuthoringSceneGraphSha256 { get; }
    public string StaticAuthoringProjectionSha256 { get; }
    public string StaticAuthoringTerrainManifestSha256 { get; }
    public string StaticAuthoringCycloramaCatalogSha256 { get; }
    public string NativeInputSetSha256 { get; }
    public string V3DeterministicPlanSha256 { get; }
    public string SourceRow80Sha256 { get; }
    public string BaselineRow80Sha256 { get; }
    public string OutputRow80Sha256 { get; }
    public string BaselineModelSha256 { get; }
    public string OutputModelSha256 { get; }
    public string BaselineDocumentCanonicalSha256 { get; }
    public string BaselineDeterministicPlanSha256 { get; }
    public string PresetId { get; }
    public string ColorHex { get; }
    public int CycloramaModelOffset { get; }
    public int CycloramaRow80Offset { get; }
    public long CycloramaWadOffset { get; }
    public Spyro1SkyBlockLayout OutputCycloramaLayout =>
        DeepCopyLayout(_outputCycloramaLayout);
    public int CycloramaByteLength => _outputCycloramaLayout.ByteLength;
    public int PaletteWordCount => _outputCycloramaLayout.PaletteWordCount;
    public int PaletteOwnedRangeCount => _ownedRanges.Count(item =>
        item.Domain ==
            Id65AuthoringBlankCycloramaAggregateRangeDomain.CycloramaPalette);
    public string BaselineCycloramaSha256 { get; }
    public string OutputCycloramaSha256 { get; }
    public string GeometrySha256 { get; }
    public string NonPaletteSha256 { get; }
    public string CommandByteSha256 { get; }
    public int CycloramaChangedRgbByteCount { get; }
    public string CompilerOwnedRangeMapSha256 { get; }
    public string CompilerTransactionSha256 { get; }
    public string CompilerProtectedComplementSha256 { get; }
    public string CompilerDeterministicPlanSha256 { get; }
    public IReadOnlyList<Id65AuthoringBlankCycloramaAggregateOwnedRange>
        OwnedRanges => _ownedRanges;
    public IReadOnlyList<Id65AuthoringBlankCycloramaAggregateOwnedRange>
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
    public bool FullExactSchema6DocumentRequired => true;
    public bool SuppliedStoreResultMatchesExpectedEnvelopeIdentity => true;
    public bool StoreEnvelopeIdentityIndependentlyVerified => true;
    public bool StaticAuthoringProjectionRecomputedFromCanonicalDocument => true;
    public bool NativeInputGenerationReverified => true;
    public bool CompilesBlankCycloramaFromLockedInputs => true;
    public bool BuildsSchema5BaselineInternally => true;
    public bool EveryPaletteWordOwnedExactlyOnce => true;
    public bool PaletteCommandBytesPreserved => true;
    public bool FrozenCycloramaGeometryVerified => true;
    public bool CompleteRowOwnershipPartitionVerified => true;
    public bool ExactWholeRowInverseVerified => true;
    public bool OneAtomicTransactionRequired => true;
    public bool Wad80SoleWritableDestination => true;
    public bool Wad79Protected => true;
    public bool RetailTownSquareWad15And16Protected => true;
    public bool RetailArtisansWad9And10Protected => true;
    public bool ExecutableProtected => true;
    public bool CallsNativeSkyPatchExporter => false;
    public bool LinkedPrimarySkyCopiesTouched => false;
    public bool GenericExporterCompatible => false;
    public bool PartialRangeApplicationAuthorized => false;
    public bool ContainsOwnedAfterimageBytes => true;
    public bool ContainsCompleteCompositeAfterimage => false;
    public bool ContainsDiscImageBytes => false;
    public bool ContainsCueBytes => false;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool WritesBin => false;
    public bool WritesCue => false;
    public bool AppIntegrated => false;
    public bool UiIntegrated => false;
    public bool MaterializerIntegrated => false;
    public bool CreateBinEnabled => false;
    public bool NormalCreateBinEnabled => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool RuntimeProven => false;
    public bool ReleaseIntegrated => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;
    public bool CurrentOnDiskFreshnessVerified => false;
    public bool RequiresTrustedExclusiveWorkspaceDirectory => true;
    public bool HostileFilesystemRaceSafe => false;
    public bool CommonWorkspaceLeaseOwned => false;

    private static Spyro1SkyBlockLayout DeepCopyLayout(
        Spyro1SkyBlockLayout layout) => layout with
        {
            Parts = Array.AsReadOnly(
                layout.Parts.Select(item => item with { }).ToArray())
        };
}

/// <summary>
/// Recomputes one exact schema-6 document from its canonical store envelope,
/// provider-owned inputs, and the pure blank-cyclorama compiler, then flattens
/// the result into one WAD80-only transaction. It never follows retail sky links.
/// </summary>
internal static class Id65AuthoringBlankCycloramaAggregatePlanProjector
{
    public const string ProfileId =
        "id65-authoring-blank-cyclorama-aggregate-plan-schema6-in-memory-v1";
    public const int SchemaVersion = 1;
    public const int ExpectedRow80ByteLength = 0x2E2000;
    public const int ExpectedStateCount = 108;
    public const int ExpectedPhysicalOutputHashCount = 76;
    public const long TargetDataWadOffset = 0x6936800;

    private const int TargetDataWadEntry = 80;
    private const int Id65OverlayWadEntry = 79;
    private const long Id65OverlayWadOffset = 0x6927000;
    private const int OverlayByteLength = 0xF800;
    private const int TownSquareOverlayWadEntry = 15;
    private const long TownSquareOverlayWadOffset = 0x118E800;
    private const int TownSquareDataWadEntry = 16;
    private const long TownSquareDataWadOffset = 0x119E000;
    private const int ArtisansOverlayWadEntry = 9;
    private const long ArtisansOverlayWadOffset = 0x7F2800;
    private const int ArtisansOverlayByteLength = 0xE000;
    private const int ArtisansDataWadEntry = 10;
    private const long ArtisansDataWadOffset = 0x800800;
    private const int ArtisansDataByteLength = 0x383000;
    private const int ExecutableLba = 55_382;
    private const int ExecutableByteLength = 0x66000;
    private const int TextureDonorWadEntry = 70;
    private const int GrassDonorWadEntry = 10;
    private const string TerrainModelOwnerId =
        "model.remote-pad.schema5-terrain-set";
    private static readonly object PlanConstructionAuthority = new();

    public static Id65AuthoringBlankCycloramaAggregatePlan Project(
        Id65AuthoringBlankCycloramaAggregatePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.StoredDocument);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            request.ExpectedEnvelopeCanonicalSha256);
        ArgumentNullException.ThrowIfNull(request.LabManifest);
        ArgumentNullException.ThrowIfNull(request.NativeInputs);
        ArgumentNullException.ThrowIfNull(request.V3Template);
        Id65AuthoringEditorNativeInputProvider.ValidateForConsumption(
            request.NativeInputs);

        ValidateExactLabManifest(request.LabManifest);
        Id65AuthoringEditorDocument document =
            Id65AuthoringEditorDocumentStore
                .ValidateRemotePadMobyChoiceTerrainSetBlankCycloramaStoreResultForAggregate(
                    request.StoredDocument,
                    request.ExpectedEnvelopeCanonicalSha256,
                    request.LabManifest);
        ValidateInputIdentity(request, document);

        Id65CompiledAuthoringBlankCyclorama compiled =
            Id65AuthoringBlankCycloramaCompiler.Compile(new(
                document,
                request.NativeInputs.LockedSource,
                request.V3Template,
                request.NativeInputs.PrivateTextureWitness,
                request.NativeInputs.GrassBundle));
        ValidateCompiled(document, compiled);

        byte[] source = compiled.CopySourceRow80();
        byte[] output = compiled.CopyOutputRow80();
        Id65AuthoringBlankCycloramaAggregateOwnedRange[] owned =
            compiled.OwnedRanges.Select(ToAggregateRange).ToArray();
        int[] paletteOffsets = BuildPaletteOffsets(
            compiled.OutputLayout,
            compiled.CycloramaRow80Offset);
        TransactionPartition partition = ValidateAndBuildPartition(
            source,
            output,
            owned,
            compiled.MobyKind,
            compiled.CycloramaRow80Offset,
            compiled.OutputLayout.ByteLength,
            paletteOffsets,
            compiled.CommandByteSha256);
        if (partition.ProtectedComplementSha256 !=
                compiled.ProtectedComplementSha256)
        {
            throw new InvalidDataException(
                "The schema-6 aggregate protected complement changed from the blank-cyclorama compiler.");
        }

        Id65AuthoringBlankCycloramaAggregateOwnedRange[] inverse = owned
            .Reverse()
            .Select(item => item.Invert())
            .ToArray();
        ValidateInverse(source, output, inverse, owned.Length);
        Id65AuthoringEditorAggregateAtomicGroup[] atomicGroups =
            BuildAtomicGroups(owned, compiled.MobyKind);
        Id65AuthoringBlankCycloramaAggregateDestinationBinding destination =
            BuildDestinationBinding(
                request.LabManifest,
                request.NativeInputs.BoundSource);

        Id65AuthoringBlankCycloramaAggregatePlan plan = new(
            PlanConstructionAuthority,
            destination,
            document,
            request.StoredDocument.EnvelopeCanonicalSha256,
            request.StoredDocument.StaticAuthoringProfileId,
            request.StoredDocument.StaticAuthoringCatalogSha256,
            request.StoredDocument.StaticAuthoringSceneGraphSha256,
            request.StoredDocument.StaticAuthoringProjectionSha256,
            request.StoredDocument.StaticAuthoringTerrainManifestSha256,
            request.StoredDocument.StaticAuthoringCycloramaCatalogSha256,
            request.NativeInputs.CanonicalInputSetSha256,
            request.V3Template.DeterministicPlanSha256,
            compiled.SourceRow80Sha256,
            compiled.BaselineRow80Sha256,
            compiled.OutputRow80Sha256,
            compiled.BaselineModelSha256,
            compiled.OutputModelSha256,
            compiled.BaselineDocumentCanonicalSha256,
            compiled.BaselineDeterministicPlanSha256,
            compiled.PresetId,
            compiled.ColorHex,
            compiled.CycloramaModelOffset,
            compiled.CycloramaRow80Offset,
            compiled.OutputLayout,
            compiled.BaselineCycloramaSha256,
            compiled.OutputCycloramaSha256,
            compiled.GeometrySha256,
            compiled.NonPaletteSha256,
            compiled.CommandByteSha256,
            compiled.CycloramaChangedRgbByteCount,
            compiled.OwnedRangeMapSha256,
            compiled.TransactionSha256,
            compiled.ProtectedComplementSha256,
            compiled.DeterministicPlanSha256,
            owned,
            inverse,
            partition.ProtectedRanges,
            atomicGroups,
            partition.GuardedByteCount,
            partition.ProtectedByteCount,
            compiled.ChangedByteCount,
            partition.ProtectedComplementSha256);
        ValidatePlan(plan);
        return plan;
    }

    internal static void RequireConstructionAuthority(object? authority)
    {
        if (!ReferenceEquals(authority, PlanConstructionAuthority))
        {
            throw new InvalidDataException(
                "Only the schema-6 aggregate projector may construct an aggregate plan.");
        }
    }

    internal static byte[] ApplyExact(
        Id65AuthoringBlankCycloramaAggregatePlan plan,
        byte[] exactRow80,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(exactRow80);
        ValidatePlan(plan);
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
                "The schema-6 aggregate whole-row preimage changed.");
        }
        IReadOnlyList<Id65AuthoringBlankCycloramaAggregateOwnedRange> ranges =
            reverse ? plan.InverseRanges : plan.OwnedRanges;
        byte[] output = ApplyAllRanges(exactRow80, ranges);
        if (Hash(output) != expectedOutputHash)
        {
            throw new InvalidDataException(
                "The schema-6 aggregate whole-row result changed.");
        }
        return output;
    }

    internal static void ValidatePartitionForSmoke(
        byte[] source,
        byte[] output,
        IEnumerable<Id65AuthoringBlankCycloramaAggregateOwnedRange> ranges,
        Id65RemotePadMobyKind kind,
        Spyro1SkyBlockLayout outputLayout,
        int cycloramaRow80Offset,
        string commandByteSha256)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(ranges);
        ArgumentNullException.ThrowIfNull(outputLayout);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandByteSha256);
        Id65AuthoringBlankCycloramaAggregateOwnedRange[] owned = ranges
            .Select(item => item.DeepCopy())
            .ToArray();
        int[] paletteOffsets = BuildPaletteOffsets(
            outputLayout,
            cycloramaRow80Offset);
        TransactionPartition partition = ValidateAndBuildPartition(
            source,
            output,
            owned,
            kind,
            cycloramaRow80Offset,
            outputLayout.ByteLength,
            paletteOffsets,
            commandByteSha256);
        ValidateInverse(
            source,
            output,
            owned.Reverse().Select(item => item.Invert()).ToArray(),
            owned.Length);
        _ = BuildAtomicGroups(owned, kind);
        if (partition.GuardedByteCount + partition.ProtectedByteCount !=
            ExpectedRow80ByteLength)
        {
            throw new InvalidDataException(
                "The schema-6 aggregate partition does not cover WAD80.");
        }
    }

    internal static void ValidatePinnedCycloramaOffsetsForSmoke(
        IReadOnlyList<string> activeTerrainUnitIds,
        int cycloramaModelOffset,
        int cycloramaRow80Offset,
        long cycloramaWadOffset) =>
        ValidatePinnedCycloramaOffsets(
            activeTerrainUnitIds,
            cycloramaModelOffset,
            cycloramaRow80Offset,
            cycloramaWadOffset);

    private static void ValidateInputIdentity(
        Id65AuthoringBlankCycloramaAggregatePlanRequest request,
        Id65AuthoringEditorDocument document)
    {
        Id65AuthoringEditorAggregateBoundSource bound =
            request.NativeInputs.BoundSource;
        Id65AuthoringCompositeLockedSource source =
            request.NativeInputs.LockedSource;
        Id65AuthoringTerrainSetManifest terrainManifest =
            Id65AuthoringTerrainSetCatalog.Create(
                document.ActiveTerrainUnitIds);
        UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan v3 =
            request.V3Template;
        if (bound.SnapshotProfileId != Id65RewardCensusContract.ProfileId ||
            bound.SourceImageSha256 != request.LabManifest.LockedBaseImageSha256 ||
            bound.TargetDataWadEntry != TargetDataWadEntry ||
            bound.TargetDataWadOffset != TargetDataWadOffset ||
            bound.RejectedRetailDataWadEntry != TownSquareDataWadEntry ||
            bound.RejectedRetailDataWadOffset != TownSquareDataWadOffset ||
            !bound.PhysicalDirectorySnapshotVerified ||
            !bound.RetailCloneComparedAndExcluded ||
            bound.CurrentOnDiskFreshnessVerified ||
            request.NativeInputs.CanonicalInputSetSha256 !=
                Id65AuthoringEditorNativeInputProvider
                    .ExpectedCanonicalInputSetSha256 ||
            source.Row80ByteLength != ExpectedRow80ByteLength ||
            source.Row80Sha256 !=
                Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 ||
            source.TargetOverlayByteLength != OverlayByteLength ||
            source.TargetOverlaySha256 !=
                Id65AuthoringCompositeCompiler.ExpectedTargetOverlaySha256 ||
            source.GlobalExecutableByteLength != ExecutableByteLength ||
            source.GlobalExecutableSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedGlobalExecutableSha256 ||
            document.SchemaVersion != Id65AuthoringEditorDocument
                .RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion ||
            document.ProfileId != Id65AuthoringEditorDocument
                .RemotePadMobyChoiceTerrainSetBlankCycloramaDocumentProfileId ||
            !document.UsesRemotePadMobyChoice ||
            !document.UsesRemotePadTerrainSet ||
            !document.UsesBlankCyclorama ||
            document.UsesRemotePadT107Grid ||
            document.TerrainSetManifestSha256 != terrainManifest.CanonicalSha256 ||
            document.PrivateTextureWitnessSha256 !=
                request.NativeInputs.PrivateTextureWitnessSha256 ||
            document.MobyBundleSha256 !=
                request.NativeInputs.GrassBundleSha256 ||
            v3.TargetWadEntry != TargetDataWadEntry ||
            v3.DataWadOffset != TargetDataWadOffset ||
            v3.DataByteLength != ExpectedRow80ByteLength ||
            v3.SourceImageSha256 != bound.SourceImageSha256 ||
            v3.ExecutableSha256 != source.GlobalExecutableSha256 ||
            !v3.SourceData.SequenceEqual(source.CopyRow80()) ||
            v3.DeterministicPlanSha256 !=
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction
                    .ExpectedDeterministicPlanSha256 ||
            !v3.ByteInverseVerified || !v3.ProtectedSubfilesPreserved ||
            v3.SourceImageMutationPossible ||
            v3.DisposableRuntimeCandidateAuthorized ||
            v3.PromotionAuthorized || v3.NormalCreateBinEnabled)
        {
            throw new InvalidDataException(
                "The schema-6 aggregate locked source, manifest, document, or v3 identity changed.");
        }
    }

    private static void ValidateCompiled(
        Id65AuthoringEditorDocument document,
        Id65CompiledAuthoringBlankCyclorama compiled)
    {
        Id65AuthoringEditorDocument baseline =
            Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetExact(
                document.RemotePadMobyKind!.Value,
                document.RemotePadMobyPositionId!,
                document.ActiveTerrainUnitIds);
        int expectedModelOffset = document.ActiveTerrainUnitIds.Count switch
        {
            0 => Id65AuthoringBlankCycloramaCompiler
                .EmptyTerrainCycloramaModelOffset,
            1 => Id65AuthoringBlankCycloramaCompiler
                .OneTerrainCycloramaModelOffset,
            2 => Id65AuthoringBlankCycloramaCompiler
                .TwoTerrainCycloramaModelOffset,
            _ => -1
        };
        if (!compiled.Document.SerializeUtf8().SequenceEqual(
                document.SerializeUtf8()) ||
            compiled.ProfileId != Id65AuthoringBlankCycloramaCompiler.ProfileId ||
            compiled.SchemaVersion !=
                Id65AuthoringBlankCycloramaCompiler.SchemaVersion ||
            compiled.InputDocumentSchemaVersion !=
                Id65AuthoringEditorDocument
                    .RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion ||
            compiled.DocumentCanonicalSha256 != document.CanonicalSha256 ||
            compiled.BaselineDocumentCanonicalSha256 != baseline.CanonicalSha256 ||
            compiled.SourceRow80Sha256 !=
                Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 ||
            !IsCanonicalSha256(compiled.BaselineRow80Sha256) ||
            !IsCanonicalSha256(compiled.OutputRow80Sha256) ||
            !IsCanonicalSha256(compiled.BaselineModelSha256) ||
            !IsCanonicalSha256(compiled.OutputModelSha256) ||
            compiled.PresetId !=
                Id65AuthoringBlankCycloramaCatalog.BlankSlatePresetId ||
            compiled.ColorHex !=
                Id65AuthoringBlankCycloramaCatalog.BlankSlateColorHex ||
            compiled.CycloramaModelOffset != expectedModelOffset ||
            compiled.CycloramaRow80Offset !=
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset +
                    expectedModelOffset ||
            compiled.CycloramaWadOffset !=
                TargetDataWadOffset + compiled.CycloramaRow80Offset ||
            compiled.CycloramaByteLength !=
                Id65AuthoringBlankCycloramaCompiler.ExpectedCycloramaByteLength ||
            compiled.PaletteWordCount !=
                Id65AuthoringBlankCycloramaCompiler.ExpectedPaletteWordCount ||
            compiled.PaletteOwnedRangeCount !=
                Id65AuthoringBlankCycloramaCompiler.ExpectedPaletteWordCount ||
            compiled.BaselineCycloramaSha256 !=
                Id65AuthoringBlankCycloramaCompiler
                    .ExpectedSourceCycloramaSha256 ||
            compiled.GeometrySha256 !=
                Id65AuthoringBlankCycloramaCompiler.ExpectedGeometrySha256 ||
            !IsSha256(compiled.OutputCycloramaSha256) ||
            !IsCanonicalSha256(compiled.NonPaletteSha256) ||
            !IsCanonicalSha256(compiled.CommandByteSha256) ||
            !IsCanonicalSha256(compiled.OwnedRangeMapSha256) ||
            !IsCanonicalSha256(compiled.TransactionSha256) ||
            !IsCanonicalSha256(compiled.ProtectedComplementSha256) ||
            !IsCanonicalSha256(compiled.DeterministicPlanSha256) ||
            compiled.OwnedRanges.Count <=
                Id65AuthoringBlankCycloramaCompiler.ExpectedPaletteWordCount ||
            compiled.InverseRanges.Count != compiled.OwnedRanges.Count ||
            compiled.ChangedByteCount <= 0 ||
            compiled.CycloramaChangedRgbByteCount <= 0 ||
            !compiled.PureInMemory || !compiled.ExactSchema6DocumentRequired ||
            !compiled.BuildsSchema5BaselineFromLockedInputs ||
            !compiled.StandaloneSkyReparseVerified ||
            !compiled.FrozenGeometryVerified ||
            !compiled.EveryPaletteWordOwnedExactlyOnce ||
            !compiled.PaletteCommandBytesPreserved ||
            !compiled.EveryNonPaletteBytePreserved ||
            !compiled.ExactInverseVerified ||
            !compiled.EveryChangedByteOwned ||
            !compiled.ProtectedComplementVerified ||
            compiled.CallsNativeSkyPatchExporter ||
            compiled.LinkedPrimarySkyCopiesTouched ||
            compiled.RetailArtisansTouched || compiled.RetailTownSquareTouched ||
            compiled.WritesFileSystem || compiled.WritesDiscImage ||
            compiled.WritesCue || compiled.CreateBinEnabled ||
            compiled.RuntimeCandidateAuthorized || compiled.ReleaseIntegrated ||
            compiled.Publishable)
        {
            throw new InvalidDataException(
                "The blank-cyclorama compiler result or authority boundary changed.");
        }
    }

    private static Id65AuthoringBlankCycloramaAggregateOwnedRange
        ToAggregateRange(Id65AuthoringCompositeOwnedRange range)
    {
        long end = range.DataRelativeOffset + (long)range.ByteLength;
        int textureStart =
            Id65V2NativeTextureCompositionCompiler
                .TexturePagesDataRelativeOffset;
        int textureEnd = checked(textureStart +
            Id65V2NativeTextureCompositionCompiler.TexturePagesByteLength);
        Id65AuthoringBlankCycloramaAggregateRangeDomain domain;
        if (range.OwnerId == Id65AuthoringBlankCycloramaCompiler.PaletteOwnerId)
        {
            domain = Id65AuthoringBlankCycloramaAggregateRangeDomain
                .CycloramaPalette;
        }
        else if (range.DataRelativeOffset >= textureStart && end <= textureEnd)
        {
            domain = Id65AuthoringBlankCycloramaAggregateRangeDomain
                .NativeTexturePages;
        }
        else if (range.OwnerId == TerrainModelOwnerId)
        {
            domain = Id65AuthoringBlankCycloramaAggregateRangeDomain
                .TerrainSetModel;
        }
        else if (range.OwnerId == "composite.v3-spawn")
        {
            domain = Id65AuthoringBlankCycloramaAggregateRangeDomain.Spawn;
        }
        else if (range.OwnerId == Id65RemotePadRedGemT21Catalog.PlacementId)
        {
            domain = Id65AuthoringBlankCycloramaAggregateRangeDomain.RedGem;
        }
        else if (range.OwnerId is
                     Id65AuthoringMobyDependencyBundleCompiler
                         .ArtisansGrassBundleId or
                     Id65RemotePadGrassT107Catalog.PlacementId or
                     "allocation.rows" or "allocation.fixups")
        {
            domain = Id65AuthoringBlankCycloramaAggregateRangeDomain.Grass;
        }
        else
        {
            throw new InvalidDataException(
                "Schema-6 physical range " + range.StableId +
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

    private static int[] BuildPaletteOffsets(
        Spyro1SkyBlockLayout layout,
        int cycloramaRow80Offset)
    {
        int[] offsets =
        [
            checked(cycloramaRow80Offset + layout.BackgroundColorOffset),
            .. layout.Parts.SelectMany(part =>
                Enumerable.Range(0, part.ColorCount).Select(index => checked(
                    cycloramaRow80Offset + part.ColorTableOffset + index * 4)))
        ];
        int[] sorted = offsets.Order().Distinct().ToArray();
        if (layout.ByteLength !=
                Id65AuthoringBlankCycloramaCompiler.ExpectedCycloramaByteLength ||
            layout.PaletteWordCount !=
                Id65AuthoringBlankCycloramaCompiler.ExpectedPaletteWordCount ||
            offsets.Length !=
                Id65AuthoringBlankCycloramaCompiler.ExpectedPaletteWordCount ||
            sorted.Length != offsets.Length ||
            !offsets.SequenceEqual(sorted) ||
            sorted.Any(offset => offset < cycloramaRow80Offset ||
                offset + 4 > cycloramaRow80Offset + layout.ByteLength))
        {
            throw new InvalidDataException(
                "The schema-6 aggregate cyclorama palette map changed.");
        }
        return sorted;
    }

    private static TransactionPartition ValidateAndBuildPartition(
        byte[] source,
        byte[] output,
        IReadOnlyList<Id65AuthoringBlankCycloramaAggregateOwnedRange> owned,
        Id65RemotePadMobyKind kind,
        int cycloramaRow80Offset,
        int cycloramaByteLength,
        IReadOnlyList<int> paletteOffsets,
        string commandByteSha256)
    {
        if (source.Length != ExpectedRow80ByteLength ||
            output.Length != ExpectedRow80ByteLength ||
            owned.Count == 0 ||
            !IsCanonicalSha256(commandByteSha256) ||
            owned.Select(item => item.StableId)
                .Distinct(StringComparer.Ordinal).Count() != owned.Count)
        {
            throw new InvalidDataException(
                "The schema-6 aggregate transaction dimensions changed.");
        }

        Id65AuthoringBlankCycloramaAggregateOwnedRange[] paletteRanges = owned
            .Where(item => item.Domain ==
                Id65AuthoringBlankCycloramaAggregateRangeDomain
                    .CycloramaPalette)
            .ToArray();
        if (paletteRanges.Length !=
                Id65AuthoringBlankCycloramaCompiler.ExpectedPaletteWordCount ||
            !paletteRanges.Select(item => item.StableId).SequenceEqual(
                Enumerable.Range(
                    0,
                    Id65AuthoringBlankCycloramaCompiler.ExpectedPaletteWordCount)
                    .Select(index => $"schema6.cyclorama.palette.{index:D4}")) ||
            !paletteRanges.Select(item => item.DataRelativeOffset)
                .SequenceEqual(paletteOffsets) ||
            paletteRanges.Any(item =>
                !ValidatePaletteRangeIdentity(
                    item,
                    cycloramaRow80Offset,
                    cycloramaByteLength)) ||
            Hash(paletteRanges.Select(item => item.CopyAfter()[3]).ToArray()) !=
                commandByteSha256 ||
            HasGrassT107Range(owned) !=
                (kind == Id65RemotePadMobyKind.GrassT107) ||
            !HasExactResidentT21XyzOwnership(owned, kind))
        {
            throw new InvalidDataException(
                "The schema-6 aggregate palette, Moby, or command-byte identity changed.");
        }

        List<Id65AuthoringEditorAggregateProtectedRange> protectedRanges = [];
        int cursor = 0;
        int guarded = 0;
        int changed = 0;
        for (int index = 0; index < owned.Count; index++)
        {
            Id65AuthoringBlankCycloramaAggregateOwnedRange range = owned[index];
            byte[] before = range.CopyBefore();
            byte[] after = range.CopyAfter();
            if (range.TargetWadEntry != TargetDataWadEntry ||
                range.WadOffset != TargetDataWadOffset +
                    range.DataRelativeOffset ||
                range.DataRelativeOffset < cursor || range.ByteLength <= 0 ||
                range.DataRelativeOffset + (long)range.ByteLength > source.Length ||
                before.Length != range.ByteLength ||
                after.Length != range.ByteLength ||
                Hash(before) != range.BeforeSha256 ||
                Hash(after) != range.AfterSha256 ||
                !ValidateOwnedRangeIdentity(
                    range,
                    cycloramaRow80Offset,
                    cycloramaByteLength) ||
                !source.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(before) ||
                !output.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(after) ||
                before.SequenceEqual(after))
            {
                throw new InvalidDataException(
                    "Schema-6 aggregate range " + range.StableId +
                    " lost destination, order, semantic, preimage, or afterimage identity.");
            }
            if (cursor < range.DataRelativeOffset)
            {
                int length = range.DataRelativeOffset - cursor;
                if (!source.AsSpan(cursor, length).SequenceEqual(
                        output.AsSpan(cursor, length)))
                {
                    throw new InvalidDataException(
                        "The schema-6 aggregate changed a protected gap.");
                }
                protectedRanges.Add(new(
                    cursor,
                    length,
                    Hash(source.AsSpan(cursor, length))));
            }
            cursor = checked(range.DataRelativeOffset + range.ByteLength);
            guarded = checked(guarded + range.ByteLength);
            changed = checked(changed + CountChangedBytes(before, after));
        }
        if (cursor < source.Length)
        {
            int length = source.Length - cursor;
            if (!source.AsSpan(cursor, length).SequenceEqual(
                    output.AsSpan(cursor, length)))
            {
                throw new InvalidDataException(
                    "The schema-6 aggregate changed its protected tail.");
            }
            protectedRanges.Add(new(
                cursor,
                length,
                Hash(source.AsSpan(cursor, length))));
        }
        if (!ApplyAllRanges(source, owned).SequenceEqual(output))
        {
            throw new InvalidDataException(
                "The schema-6 aggregate ranges do not reproduce the compiler output.");
        }
        int protectedBytes = source.Length - guarded;
        if (changed != CountChangedBytes(source, output) ||
            changed > guarded || protectedBytes < 0 ||
            protectedRanges.Count == 0 ||
            guarded + protectedBytes != ExpectedRow80ByteLength)
        {
            throw new InvalidDataException(
                "The schema-6 aggregate guarded/protected partition changed.");
        }
        return new(
            protectedRanges.AsReadOnly(),
            guarded,
            protectedBytes,
            HashProtectedBytes(source, owned));
    }

    private static bool ValidateOwnedRangeIdentity(
        Id65AuthoringBlankCycloramaAggregateOwnedRange range,
        int cycloramaRow80Offset,
        int cycloramaByteLength)
    {
        byte[] before = range.CopyBefore();
        byte[] after = range.CopyAfter();
        long end = range.DataRelativeOffset + (long)range.ByteLength;
        int textureStart = Id65V2NativeTextureCompositionCompiler
            .TexturePagesDataRelativeOffset;
        int textureEnd = checked(textureStart +
            Id65V2NativeTextureCompositionCompiler.TexturePagesByteLength);
        int modelStart =
            Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset;
        int modelEnd = checked(modelStart +
            Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelLength);
        int redGemStart = Id65AuthoringRemotePadMobyChoicePhysicalCompiler
            .ResidentT21CoordinateOffset;
        int redGemEnd = checked(redGemStart +
            Id65AuthoringRemotePadMobyChoicePhysicalCompiler
                .ResidentT21CoordinateByteLength);
        int changed = CountChangedBytes(before, after);
        bool domainMatches = range.Domain switch
        {
            Id65AuthoringBlankCycloramaAggregateRangeDomain.NativeTexturePages =>
                range.DataRelativeOffset >= textureStart && end <= textureEnd,
            Id65AuthoringBlankCycloramaAggregateRangeDomain.TerrainSetModel =>
                range.OwnerId == TerrainModelOwnerId &&
                range.DataRelativeOffset >= modelStart && end <= modelEnd,
            Id65AuthoringBlankCycloramaAggregateRangeDomain.Spawn =>
                range.OwnerId == "composite.v3-spawn",
            Id65AuthoringBlankCycloramaAggregateRangeDomain.Grass =>
                range.OwnerId is
                    Id65AuthoringMobyDependencyBundleCompiler
                        .ArtisansGrassBundleId or
                    Id65RemotePadGrassT107Catalog.PlacementId or
                    "allocation.rows" or "allocation.fixups",
            Id65AuthoringBlankCycloramaAggregateRangeDomain.RedGem =>
                range.StableId ==
                    Id65AuthoringRemotePadTerrainSetPhysicalCompiler
                        .ResidentT21XyzOwnedRangeStableId &&
                range.OwnerId == Id65RemotePadRedGemT21Catalog.PlacementId &&
                range.DataRelativeOffset == redGemStart && end == redGemEnd,
            Id65AuthoringBlankCycloramaAggregateRangeDomain.CycloramaPalette =>
                ValidatePaletteRangeIdentity(
                    range,
                    cycloramaRow80Offset,
                    cycloramaByteLength),
            _ => false
        };
        bool stableIdMatches = range.Domain ==
                Id65AuthoringBlankCycloramaAggregateRangeDomain.CycloramaPalette
            ? range.StableId.StartsWith(
                "schema6.cyclorama.palette.",
                StringComparison.Ordinal)
            : range.StableId.StartsWith("schema5.row80.", StringComparison.Ordinal);
        return domainMatches && stableIdMatches &&
            range.TargetWadEntry == TargetDataWadEntry &&
            range.DataRelativeOffset >= 0 && range.ByteLength > 0 &&
            end <= ExpectedRow80ByteLength &&
            range.WadOffset == TargetDataWadOffset + range.DataRelativeOffset &&
            before.Length == range.ByteLength && after.Length == range.ByteLength &&
            Hash(before) == range.BeforeSha256 &&
            Hash(after) == range.AfterSha256 &&
            changed > 0 &&
            (range.Domain is
                 Id65AuthoringBlankCycloramaAggregateRangeDomain.RedGem or
                 Id65AuthoringBlankCycloramaAggregateRangeDomain.CycloramaPalette ||
             changed == range.ByteLength);
    }

    private static bool ValidatePaletteRangeIdentity(
        Id65AuthoringBlankCycloramaAggregateOwnedRange range,
        int cycloramaRow80Offset,
        int cycloramaByteLength)
    {
        byte[] after = range.CopyAfter();
        return range.OwnerId == Id65AuthoringBlankCycloramaCompiler.PaletteOwnerId &&
            range.Domain ==
                Id65AuthoringBlankCycloramaAggregateRangeDomain.CycloramaPalette &&
            range.ByteLength == 4 &&
            range.DataRelativeOffset >= cycloramaRow80Offset &&
            range.DataRelativeOffset + 4 <=
                cycloramaRow80Offset + cycloramaByteLength &&
            after.Length == 4 &&
            after[0] == Id65AuthoringBlankCycloramaCompiler.BlankSlateV1Red &&
            after[1] == Id65AuthoringBlankCycloramaCompiler.BlankSlateV1Green &&
            after[2] == Id65AuthoringBlankCycloramaCompiler.BlankSlateV1Blue;
    }

    private static bool HasGrassT107Range(
        IReadOnlyList<Id65AuthoringBlankCycloramaAggregateOwnedRange> owned) =>
        owned.Any(item => item.OwnerId ==
            Id65RemotePadGrassT107Catalog.PlacementId);

    private static bool HasExactResidentT21XyzOwnership(
        IReadOnlyList<Id65AuthoringBlankCycloramaAggregateOwnedRange> owned,
        Id65RemotePadMobyKind kind)
    {
        Id65AuthoringBlankCycloramaAggregateOwnedRange[] related = owned
            .Where(item => item.Domain ==
                Id65AuthoringBlankCycloramaAggregateRangeDomain.RedGem ||
                item.OwnerId == Id65RemotePadRedGemT21Catalog.PlacementId ||
                item.StableId ==
                    Id65AuthoringRemotePadTerrainSetPhysicalCompiler
                        .ResidentT21XyzOwnedRangeStableId)
            .ToArray();
        return kind == Id65RemotePadMobyKind.RedGemT21
            ? related.Length == 1 && related[0].ByteLength ==
                Id65AuthoringRemotePadMobyChoicePhysicalCompiler
                    .ResidentT21CoordinateByteLength
            : related.Length == 0;
    }

    private static Id65AuthoringEditorAggregateAtomicGroup[] BuildAtomicGroups(
        IReadOnlyList<Id65AuthoringBlankCycloramaAggregateOwnedRange> owned,
        Id65RemotePadMobyKind kind)
    {
        Id65AuthoringEditorAggregateAtomicGroup Create(
            string id,
            Func<Id65AuthoringBlankCycloramaAggregateOwnedRange, bool> predicate)
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
                Id65AuthoringBlankCycloramaAggregateRangeDomain
                    .NativeTexturePages or
                Id65AuthoringBlankCycloramaAggregateRangeDomain
                    .TerrainSetModel),
            Create("landing-t92", item => item.Domain ==
                Id65AuthoringBlankCycloramaAggregateRangeDomain.Spawn),
            Create("artisans-grass-remote-pad-t107", item => item.Domain ==
                Id65AuthoringBlankCycloramaAggregateRangeDomain.Grass),
            Create("resident-red-gem-remote-pad-t21", item => item.Domain ==
                Id65AuthoringBlankCycloramaAggregateRangeDomain.RedGem),
            Create("blank-cyclorama-palette", item => item.Domain ==
                Id65AuthoringBlankCycloramaAggregateRangeDomain
                    .CycloramaPalette)
        ];
        if (groups[0].OwnedRangeCount <= 0 ||
            groups[1].OwnedRangeCount <= 0 ||
            groups[2].OwnedRangeCount <= 0 ||
            groups[3].OwnedRangeCount !=
                (kind == Id65RemotePadMobyKind.RedGemT21 ? 1 : 0) ||
            groups[4].OwnedRangeCount !=
                Id65AuthoringBlankCycloramaCompiler.ExpectedPaletteWordCount ||
            groups.Sum(item => item.OwnedRangeCount) != owned.Count ||
            groups.Any(item =>
                !item.AllRangesRequired || item.IndependentlyApplicable))
        {
            throw new InvalidDataException(
                "The schema-6 aggregate five-group atomic partition changed.");
        }
        return groups;
    }

    private static Id65AuthoringBlankCycloramaAggregateDestinationBinding
        BuildDestinationBinding(
            UnusedLevel65BlankLevelLabManifest manifest,
            Id65AuthoringEditorAggregateBoundSource bound)
    {
        Id65AuthoringBlankCycloramaAggregateDestinationBinding result = new(
            bound.SnapshotProfileId,
            bound.SourceImageSha256,
            manifest.LockedBaseProfileId,
            manifest.LockedBaseImageSha256,
            "WAD.WAD",
            TargetDataWadEntry,
            TargetDataWadOffset,
            ExpectedRow80ByteLength,
            bound.TargetDataDirectoryRowSha256,
            manifest.ObjectTableWadOffset,
            manifest.ObjectTableRelativeOffset,
            Id65OverlayWadEntry,
            Id65OverlayWadOffset,
            OverlayByteLength,
            TownSquareOverlayWadEntry,
            TownSquareOverlayWadOffset,
            OverlayByteLength,
            TownSquareDataWadEntry,
            TownSquareDataWadOffset,
            ExpectedRow80ByteLength,
            bound.RejectedRetailDataDirectoryRowSha256,
            ArtisansOverlayWadEntry,
            ArtisansOverlayWadOffset,
            ArtisansOverlayByteLength,
            ArtisansDataWadEntry,
            ArtisansDataWadOffset,
            ArtisansDataByteLength,
            ExecutableLba,
            ExecutableByteLength,
            Id65RewardCensusContract.ExecutableSha256,
            TextureDonorWadEntry,
            GrassDonorWadEntry,
            PhysicalSnapshotVerified: true,
            Wad80SoleWritableDestination: true,
            Wad79Protected: true,
            RetailTownSquareWad15Protected: true,
            RetailTownSquareWad16Protected: true,
            RetailArtisansWad9Protected: true,
            RetailArtisansWad10Protected: true,
            ExecutableProtected: true,
            DonorLocationsReadOnlyProvenance: true,
            LinkedPrimarySkyCopiesNotFollowed: true,
            NativeSkyPatchExporterNotUsed: true,
            PhysicalDestinationRevalidationRequired: true);
        ValidateDestination(result);
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
                UnusedLevel65BlankLevelLabProfileRegistry
                    .WorkspaceLayoutRelativeRoot ||
            manifest.DisplayName !=
                UnusedLevel65BlankLevelLabProfileRegistry.Profile.DisplayName ||
            manifest.LevelId != 65 || manifest.DataWadEntry != TargetDataWadEntry ||
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
                UnusedLevel65BlankLevelLabProfileRegistry.Profile
                    .PhysicalCloneProfileId ||
            manifest.PhysicalCloneImageSha256 !=
                UnusedLevel65BlankLevelLabProfileRegistry.Profile
                    .PhysicalCloneImageSha256 ||
            manifest.PhysicalCloneEvidence !=
                UnusedLevel65BlankLevelLabProfileRegistry.PhysicalCloneEvidence ||
            manifest.LockedBaseProfileId !=
                UnusedLevel65BlankLevelLabProfileRegistry.Profile
                    .LockedBaseProfileId ||
            manifest.LockedBaseImageSha256 !=
                UnusedLevel65BlankLevelLabProfileRegistry.Profile
                    .LockedBaseImageSha256 ||
            manifest.LockedBaseEvidence !=
                UnusedLevel65BlankLevelLabProfileRegistry.LockedBaseEvidence ||
            manifest.Capabilities !=
                UnusedLevel65BlankLevelLabProfileRegistry.Capabilities ||
            !manifest.ExternalGameDataRequired || manifest.GameDataEmbedded ||
            manifest.PromotionAuthorized || manifest.NormalCreateBinEnabled)
        {
            throw new InvalidDataException(
                "The schema-6 aggregate lab manifest is not the exact WAD80 workspace.");
        }
    }

    private static void ValidateDestination(
        Id65AuthoringBlankCycloramaAggregateDestinationBinding binding)
    {
        if (binding.SourceSnapshotProfileId !=
                Id65RewardCensusContract.ProfileId ||
            binding.SourceImageSha256 !=
                Id65RewardCensusContract.LockedSourceImageSha256 ||
            binding.LockedBaseProfileId !=
                UnusedLevel65BlankLevelLabProfileRegistry.Profile
                    .LockedBaseProfileId ||
            binding.LockedBaseImageSha256 != binding.SourceImageSha256 ||
            binding.WadFileName != "WAD.WAD" ||
            binding.TargetDataWadEntry != TargetDataWadEntry ||
            binding.TargetDataWadOffset != TargetDataWadOffset ||
            binding.TargetDataByteLength != ExpectedRow80ByteLength ||
            binding.TargetDataDirectoryRowSha256 !=
                "3c6ae9564c3f6fc794bbae9670ee0dff7bd9a7bdc0f96c3c206c752551d8c09a" ||
            binding.TargetObjectTableWadOffset !=
                UnusedLevel65BlankLevelLabProfileRegistry.ObjectTableWadOffset ||
            binding.TargetObjectTableRelativeOffset !=
                UnusedLevel65BlankLevelLabProfileRegistry
                    .ObjectTableRelativeOffset ||
            binding.TargetObjectTableWadOffset -
                binding.TargetObjectTableRelativeOffset != TargetDataWadOffset ||
            binding.ProtectedId65OverlayWadEntry != Id65OverlayWadEntry ||
            binding.ProtectedId65OverlayWadOffset != Id65OverlayWadOffset ||
            binding.ProtectedId65OverlayByteLength != OverlayByteLength ||
            binding.ProtectedTownSquareOverlayWadEntry !=
                TownSquareOverlayWadEntry ||
            binding.ProtectedTownSquareOverlayWadOffset !=
                TownSquareOverlayWadOffset ||
            binding.ProtectedTownSquareOverlayByteLength != OverlayByteLength ||
            binding.ProtectedTownSquareDataWadEntry != TownSquareDataWadEntry ||
            binding.ProtectedTownSquareDataWadOffset != TownSquareDataWadOffset ||
            binding.ProtectedTownSquareDataByteLength !=
                ExpectedRow80ByteLength ||
            binding.ProtectedTownSquareDataDirectoryRowSha256 !=
                "ba8329f16f9caed1bed39c6b794968ff95160adbfd2480c5d4bbb6fc7545f77b" ||
            binding.ProtectedArtisansOverlayWadEntry != ArtisansOverlayWadEntry ||
            binding.ProtectedArtisansOverlayWadOffset != ArtisansOverlayWadOffset ||
            binding.ProtectedArtisansOverlayByteLength !=
                ArtisansOverlayByteLength ||
            binding.ProtectedArtisansDataWadEntry != ArtisansDataWadEntry ||
            binding.ProtectedArtisansDataWadOffset != ArtisansDataWadOffset ||
            binding.ProtectedArtisansDataByteLength != ArtisansDataByteLength ||
            binding.ProtectedExecutableLba != ExecutableLba ||
            binding.ProtectedExecutableByteLength != ExecutableByteLength ||
            binding.ProtectedExecutableSha256 !=
                Id65RewardCensusContract.ExecutableSha256 ||
            binding.TextureDonorWadEntry != TextureDonorWadEntry ||
            binding.GrassDonorWadEntry != GrassDonorWadEntry ||
            !binding.PhysicalSnapshotVerified ||
            !binding.Wad80SoleWritableDestination || !binding.Wad79Protected ||
            !binding.RetailTownSquareWad15Protected ||
            !binding.RetailTownSquareWad16Protected ||
            !binding.RetailArtisansWad9Protected ||
            !binding.RetailArtisansWad10Protected ||
            !binding.ExecutableProtected ||
            !binding.DonorLocationsReadOnlyProvenance ||
            !binding.LinkedPrimarySkyCopiesNotFollowed ||
            !binding.NativeSkyPatchExporterNotUsed ||
            !binding.PhysicalDestinationRevalidationRequired)
        {
            throw new InvalidDataException(
                "The schema-6 WAD80-only destination binding changed.");
        }
    }

    private static void ValidatePlan(
        Id65AuthoringBlankCycloramaAggregatePlan plan)
    {
        Id65AuthoringEditorDocument exact =
            Id65AuthoringEditorDocument
                .CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
                    plan.MobyKind,
                    plan.MobyPositionId,
                    plan.ActiveTerrainUnitIds);
        Id65AuthoringEditorDocument baseline =
            Id65AuthoringEditorDocument
                .CreateRemotePadMobyChoiceTerrainSetExact(
                    plan.MobyKind,
                    plan.MobyPositionId,
                    plan.ActiveTerrainUnitIds);
        Id65AuthoringTerrainSetManifest manifest =
            Id65AuthoringTerrainSetCatalog.Create(
                plan.ActiveTerrainUnitIds);
        int[] paletteOffsets = BuildPaletteOffsets(
            plan.OutputCycloramaLayout,
            plan.CycloramaRow80Offset);
        ValidatePinnedCycloramaOffsets(
            plan.ActiveTerrainUnitIds,
            plan.CycloramaModelOffset,
            plan.CycloramaRow80Offset,
            plan.CycloramaWadOffset);
        Id65AuthoringBlankCycloramaAggregateOwnedRange[] paletteRanges =
            plan.OwnedRanges.Where(item => item.Domain ==
                Id65AuthoringBlankCycloramaAggregateRangeDomain
                    .CycloramaPalette).ToArray();
        Id65AuthoringEditorAggregateAtomicGroup[] expectedGroups =
            BuildAtomicGroups(plan.OwnedRanges, plan.MobyKind);
        bool inverseExact = plan.InverseRanges.Count == plan.OwnedRanges.Count;
        for (int index = 0; inverseExact && index < plan.OwnedRanges.Count; index++)
        {
            Id65AuthoringBlankCycloramaAggregateOwnedRange forward =
                plan.OwnedRanges[index];
            Id65AuthoringBlankCycloramaAggregateOwnedRange inverse =
                plan.InverseRanges[plan.InverseRanges.Count - index - 1];
            inverseExact = forward.StableId == inverse.StableId &&
                forward.OwnerId == inverse.OwnerId &&
                forward.Domain == inverse.Domain &&
                forward.DataRelativeOffset == inverse.DataRelativeOffset &&
                forward.ByteLength == inverse.ByteLength &&
                forward.BeforeSha256 == inverse.AfterSha256 &&
                forward.AfterSha256 == inverse.BeforeSha256 &&
                forward.CopyBefore().SequenceEqual(inverse.CopyAfter()) &&
                forward.CopyAfter().SequenceEqual(inverse.CopyBefore());
        }

        if (plan.ProfileId != ProfileId || plan.SchemaVersion != SchemaVersion ||
            plan.LevelId != 65 || plan.DataWadEntry != TargetDataWadEntry ||
            !plan.Document.SerializeUtf8().SequenceEqual(exact.SerializeUtf8()) ||
            plan.DocumentCanonicalSha256 != exact.CanonicalSha256 ||
            plan.Document.SchemaVersion != Id65AuthoringEditorDocument
                .RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion ||
            plan.Document.ProfileId != Id65AuthoringEditorDocument
                .RemotePadMobyChoiceTerrainSetBlankCycloramaDocumentProfileId ||
            !plan.Document.UsesBlankCyclorama ||
            plan.TerrainManifestSha256 != manifest.CanonicalSha256 ||
            plan.StaticAuthoringTerrainManifestSha256 != manifest.CanonicalSha256 ||
            plan.StaticAuthoringCycloramaCatalogSha256 !=
                Id65AuthoringBlankCycloramaCatalog.CanonicalSha256 ||
            plan.StaticAuthoringProfileId != Id65AuthoringEditorDocumentStore
                .RemotePadMobyChoiceTerrainSetBlankCycloramaStaticAuthoringProfileId ||
            plan.BaselineDocumentCanonicalSha256 != baseline.CanonicalSha256 ||
            plan.PresetId != Id65AuthoringBlankCycloramaCatalog.BlankSlatePresetId ||
            plan.ColorHex != Id65AuthoringBlankCycloramaCatalog.BlankSlateColorHex ||
            plan.SourceRow80Sha256 !=
                Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 ||
            plan.NativeInputSetSha256 != Id65AuthoringEditorNativeInputProvider
                .ExpectedCanonicalInputSetSha256 ||
            plan.V3DeterministicPlanSha256 !=
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction
                    .ExpectedDeterministicPlanSha256 ||
            plan.BaselineCycloramaSha256 !=
                Id65AuthoringBlankCycloramaCompiler.ExpectedSourceCycloramaSha256 ||
            plan.GeometrySha256 !=
                Id65AuthoringBlankCycloramaCompiler.ExpectedGeometrySha256 ||
            plan.OutputCycloramaLayout.GeometrySha256 != plan.GeometrySha256 ||
            plan.OutputCycloramaLayout.Sha256 != plan.OutputCycloramaSha256 ||
            plan.CycloramaByteLength !=
                Id65AuthoringBlankCycloramaCompiler.ExpectedCycloramaByteLength ||
            plan.PaletteWordCount !=
                Id65AuthoringBlankCycloramaCompiler.ExpectedPaletteWordCount ||
            plan.PaletteOwnedRangeCount !=
                Id65AuthoringBlankCycloramaCompiler.ExpectedPaletteWordCount ||
            !paletteRanges.Select(item => item.DataRelativeOffset)
                .SequenceEqual(paletteOffsets) ||
            Hash(paletteRanges.Select(item => item.CopyAfter()[3]).ToArray()) !=
                plan.CommandByteSha256 ||
            plan.OwnedRangeCount <=
                Id65AuthoringBlankCycloramaCompiler.ExpectedPaletteWordCount ||
            plan.InverseRangeCount != plan.OwnedRangeCount ||
            plan.ProtectedRangeCount <= 0 || !inverseExact ||
            plan.AtomicGroups.Count != 5 ||
            !plan.AtomicGroups.SequenceEqual(expectedGroups) ||
            plan.AtomicGroups[4].GroupId != "blank-cyclorama-palette" ||
            plan.AtomicGroups[4].OwnedRangeCount !=
                Id65AuthoringBlankCycloramaCompiler.ExpectedPaletteWordCount ||
            plan.GuardedByteCount !=
                plan.OwnedRanges.Sum(item => item.ByteLength) ||
            plan.ProtectedByteCount !=
                plan.ProtectedRanges.Sum(item => item.ByteLength) ||
            plan.GuardedByteCount + plan.ProtectedByteCount !=
                ExpectedRow80ByteLength ||
            plan.ChangedByteCount != CountChangedBytes(plan.OwnedRanges) ||
            plan.ChangedByteCount <= 0 ||
            plan.ChangedByteCount > plan.GuardedByteCount ||
            !RangesCoverWholeRow(plan.OwnedRanges, plan.ProtectedRanges) ||
            plan.OwnedRanges.Any(item => !ValidateOwnedRangeIdentity(
                item,
                plan.CycloramaRow80Offset,
                plan.CycloramaByteLength)) ||
            plan.ProtectedRanges.Any(item => item.ByteLength <= 0 ||
                !IsCanonicalSha256(item.SegmentSha256)) ||
            plan.CompilerProtectedComplementSha256 !=
                plan.ProtectedComplementSha256 ||
            !IsSha256(plan.OutputCycloramaSha256) ||
            plan.OwnedRangeMapSha256 != HashOwnedRanges(plan.OwnedRanges) ||
            plan.InverseRangeMapSha256 != HashOwnedRanges(plan.InverseRanges) ||
            plan.ProtectedRangeMapSha256 !=
                HashProtectedRanges(plan.ProtectedRanges) ||
            plan.AtomicGroupMapSha256 != HashAtomicGroups(plan.AtomicGroups) ||
            plan.DeterministicAggregatePlanSha256 !=
                ComputeDeterministicPlanSha256(plan) ||
            !AllPlanHashesCanonical(plan))
        {
            throw new InvalidDataException(
                "The schema-6 aggregate plan identity or ownership partition changed.");
        }
        ValidateDestination(plan.Destination);
        if (!plan.PureInMemory || !plan.FullExactSchema6DocumentRequired ||
            !plan.SuppliedStoreResultMatchesExpectedEnvelopeIdentity ||
            !plan.StoreEnvelopeIdentityIndependentlyVerified ||
            !plan.StaticAuthoringProjectionRecomputedFromCanonicalDocument ||
            !plan.NativeInputGenerationReverified ||
            !plan.CompilesBlankCycloramaFromLockedInputs ||
            !plan.BuildsSchema5BaselineInternally ||
            !plan.EveryPaletteWordOwnedExactlyOnce ||
            !plan.PaletteCommandBytesPreserved ||
            !plan.FrozenCycloramaGeometryVerified ||
            !plan.CompleteRowOwnershipPartitionVerified ||
            !plan.ExactWholeRowInverseVerified ||
            !plan.OneAtomicTransactionRequired ||
            !plan.Wad80SoleWritableDestination || !plan.Wad79Protected ||
            !plan.RetailTownSquareWad15And16Protected ||
            !plan.RetailArtisansWad9And10Protected ||
            !plan.ExecutableProtected || plan.CallsNativeSkyPatchExporter ||
            plan.LinkedPrimarySkyCopiesTouched ||
            plan.GenericExporterCompatible ||
            plan.PartialRangeApplicationAuthorized ||
            !plan.ContainsOwnedAfterimageBytes ||
            plan.ContainsCompleteCompositeAfterimage ||
            plan.ContainsDiscImageBytes || plan.ContainsCueBytes ||
            plan.WritesFileSystem || plan.WritesDiscImage || plan.WritesBin ||
            plan.WritesCue || plan.AppIntegrated || plan.UiIntegrated ||
            plan.MaterializerIntegrated || plan.CreateBinEnabled ||
            plan.NormalCreateBinEnabled || plan.RuntimeCandidateAuthorized ||
            plan.RuntimeProven || plan.ReleaseIntegrated ||
            plan.PromotionAuthorized || plan.Publishable ||
            plan.CurrentOnDiskFreshnessVerified ||
            !plan.RequiresTrustedExclusiveWorkspaceDirectory ||
            plan.HostileFilesystemRaceSafe || plan.CommonWorkspaceLeaseOwned)
        {
            throw new InvalidDataException(
                "The schema-6 aggregate authority boundary changed.");
        }
    }

    private static void ValidatePinnedCycloramaOffsets(
        IReadOnlyList<string> activeTerrainUnitIds,
        int cycloramaModelOffset,
        int cycloramaRow80Offset,
        long cycloramaWadOffset)
    {
        ArgumentNullException.ThrowIfNull(activeTerrainUnitIds);
        (int Model, int Row80, long Wad) expected =
            activeTerrainUnitIds.Count switch
            {
                0 => (
                    Id65AuthoringBlankCycloramaCompiler
                        .EmptyTerrainCycloramaModelOffset,
                    Id65AuthoringBlankCycloramaCompiler
                        .EmptyTerrainCycloramaRow80Offset,
                    Id65AuthoringBlankCycloramaCompiler
                        .EmptyTerrainCycloramaWadOffset),
                1 => (
                    Id65AuthoringBlankCycloramaCompiler
                        .OneTerrainCycloramaModelOffset,
                    Id65AuthoringBlankCycloramaCompiler
                        .OneTerrainCycloramaRow80Offset,
                    Id65AuthoringBlankCycloramaCompiler
                        .OneTerrainCycloramaWadOffset),
                2 => (
                    Id65AuthoringBlankCycloramaCompiler
                        .TwoTerrainCycloramaModelOffset,
                    Id65AuthoringBlankCycloramaCompiler
                        .TwoTerrainCycloramaRow80Offset,
                    Id65AuthoringBlankCycloramaCompiler
                        .TwoTerrainCycloramaWadOffset),
                _ => throw new InvalidDataException(
                    "The schema-6 aggregate terrain topology exceeds two units.")
            };
        if (cycloramaModelOffset != expected.Model ||
            cycloramaRow80Offset != expected.Row80 ||
            cycloramaWadOffset != expected.Wad ||
            cycloramaRow80Offset !=
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler.ModelOffset +
                    cycloramaModelOffset ||
            cycloramaWadOffset != TargetDataWadOffset + cycloramaRow80Offset ||
            cycloramaWadOffset is
                Id65AuthoringBlankCycloramaCompiler
                    .RetailTownSquareCycloramaWadOffset or
                Id65AuthoringBlankCycloramaCompiler
                    .RetailArtisansLinkedCycloramaWadOffset)
        {
            throw new InvalidDataException(
                "The schema-6 aggregate cyclorama topology offset changed or entered a retail sky address.");
        }
    }

    private static bool AllPlanHashesCanonical(
        Id65AuthoringBlankCycloramaAggregatePlan plan) =>
        new[]
        {
            plan.EnvelopeCanonicalSha256,
            plan.StaticAuthoringCatalogSha256,
            plan.StaticAuthoringSceneGraphSha256,
            plan.StaticAuthoringProjectionSha256,
            plan.StaticAuthoringTerrainManifestSha256,
            plan.StaticAuthoringCycloramaCatalogSha256,
            plan.SourceRow80Sha256,
            plan.BaselineRow80Sha256,
            plan.OutputRow80Sha256,
            plan.BaselineModelSha256,
            plan.OutputModelSha256,
            plan.BaselineDeterministicPlanSha256,
            plan.NonPaletteSha256,
            plan.CommandByteSha256,
            plan.CompilerOwnedRangeMapSha256,
            plan.CompilerTransactionSha256,
            plan.CompilerProtectedComplementSha256,
            plan.CompilerDeterministicPlanSha256,
            plan.OwnedRangeMapSha256,
            plan.InverseRangeMapSha256,
            plan.ProtectedRangeMapSha256,
            plan.ProtectedComplementSha256,
            plan.AtomicGroupMapSha256,
            plan.DeterministicAggregatePlanSha256
        }.All(IsCanonicalSha256);

    private static void ValidateInverse(
        byte[] source,
        byte[] output,
        IReadOnlyList<Id65AuthoringBlankCycloramaAggregateOwnedRange> inverse,
        int expectedRangeCount)
    {
        if (expectedRangeCount <= 0 || inverse.Count != expectedRangeCount ||
            !ApplyAllRanges(output, inverse).SequenceEqual(source))
        {
            throw new InvalidDataException(
                "The schema-6 aggregate inverse transaction changed.");
        }
    }

    private static byte[] ApplyAllRanges(
        byte[] input,
        IReadOnlyList<Id65AuthoringBlankCycloramaAggregateOwnedRange> ranges)
    {
        byte[] output = input.ToArray();
        foreach (Id65AuthoringBlankCycloramaAggregateOwnedRange range in ranges)
        {
            if (range.DataRelativeOffset < 0 || range.ByteLength <= 0 ||
                range.DataRelativeOffset + (long)range.ByteLength > output.Length ||
                !output.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(range.CopyBefore()))
            {
                throw new InvalidDataException(
                    "Schema-6 aggregate range " + range.StableId +
                    " lost its exact transactional preimage.");
            }
            range.CopyAfter().CopyTo(output, range.DataRelativeOffset);
        }
        return output;
    }

    private static bool RangesCoverWholeRow(
        IReadOnlyList<Id65AuthoringBlankCycloramaAggregateOwnedRange> owned,
        IReadOnlyList<Id65AuthoringEditorAggregateProtectedRange> protectedRanges)
    {
        (int Offset, int Length)[] partition = owned
            .Select(item => (item.DataRelativeOffset, item.ByteLength))
            .Concat(protectedRanges.Select(item =>
                (item.DataRelativeOffset, item.ByteLength)))
            .OrderBy(item => item.DataRelativeOffset)
            .ToArray();
        long cursor = 0;
        foreach ((int offset, int length) in partition)
        {
            if (offset != cursor || length <= 0) return false;
            cursor += length;
            if (cursor > ExpectedRow80ByteLength) return false;
        }
        return cursor == ExpectedRow80ByteLength;
    }

    private static int CountChangedBytes(
        ReadOnlySpan<byte> before,
        ReadOnlySpan<byte> after)
    {
        if (before.Length != after.Length)
        {
            throw new InvalidDataException(
                "Schema-6 aggregate changed-byte inputs differ in length.");
        }
        int count = 0;
        for (int index = 0; index < before.Length; index++)
        {
            if (before[index] != after[index]) count++;
        }
        return count;
    }

    private static int CountChangedBytes(
        IEnumerable<Id65AuthoringBlankCycloramaAggregateOwnedRange> ranges)
    {
        int count = 0;
        foreach (Id65AuthoringBlankCycloramaAggregateOwnedRange range in ranges)
        {
            count = checked(count + CountChangedBytes(
                range.CopyBefore(),
                range.CopyAfter()));
        }
        return count;
    }

    internal static string HashOwnedRanges(
        IEnumerable<Id65AuthoringBlankCycloramaAggregateOwnedRange> ranges) =>
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

    private static string HashProtectedBytes(
        byte[] source,
        IReadOnlyList<Id65AuthoringBlankCycloramaAggregateOwnedRange> ranges)
    {
        using IncrementalHash hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int cursor = 0;
        foreach (Id65AuthoringBlankCycloramaAggregateOwnedRange range in ranges)
        {
            if (cursor < range.DataRelativeOffset)
            {
                hash.AppendData(source.AsSpan(
                    cursor,
                    range.DataRelativeOffset - cursor));
            }
            cursor = checked(range.DataRelativeOffset + range.ByteLength);
        }
        if (cursor < source.Length) hash.AppendData(source.AsSpan(cursor));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    internal static string ComputeDeterministicPlanSha256(
        Id65AuthoringBlankCycloramaAggregatePlan plan) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n',
        [
            ProfileId,
            SchemaVersion.ToString(CultureInfo.InvariantCulture),
            plan.DocumentCanonicalSha256,
            plan.EnvelopeCanonicalSha256,
            plan.StaticAuthoringProjectionSha256,
            plan.StaticAuthoringTerrainManifestSha256,
            plan.StaticAuthoringCycloramaCatalogSha256,
            plan.NativeInputSetSha256,
            plan.V3DeterministicPlanSha256,
            plan.BaselineDocumentCanonicalSha256,
            plan.BaselineDeterministicPlanSha256,
            plan.PresetId,
            plan.ColorHex,
            plan.SourceRow80Sha256,
            plan.BaselineRow80Sha256,
            plan.OutputRow80Sha256,
            plan.BaselineModelSha256,
            plan.OutputModelSha256,
            plan.CycloramaModelOffset.ToString(CultureInfo.InvariantCulture),
            plan.CycloramaRow80Offset.ToString(CultureInfo.InvariantCulture),
            plan.BaselineCycloramaSha256,
            plan.OutputCycloramaSha256,
            plan.GeometrySha256,
            plan.NonPaletteSha256,
            plan.CommandByteSha256,
            plan.CycloramaChangedRgbByteCount.ToString(
                CultureInfo.InvariantCulture),
            plan.ChangedByteCount.ToString(CultureInfo.InvariantCulture),
            plan.CompilerOwnedRangeMapSha256,
            plan.CompilerTransactionSha256,
            plan.CompilerProtectedComplementSha256,
            plan.CompilerDeterministicPlanSha256,
            plan.OwnedRangeMapSha256,
            plan.InverseRangeMapSha256,
            plan.ProtectedRangeMapSha256,
            plan.ProtectedComplementSha256,
            plan.AtomicGroupMapSha256,
            Hash(Encoding.UTF8.GetBytes(string.Join(
                '\n',
                plan.ActiveTerrainUnitIds))),
            plan.Destination.TargetDataWadOffset.ToString(
                "X",
                CultureInfo.InvariantCulture),
            plan.Destination.TargetDataDirectoryRowSha256,
            plan.Destination.ProtectedTownSquareDataDirectoryRowSha256,
            plan.Destination.ProtectedExecutableSha256
        ])));

    internal static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static bool IsCanonicalSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or
                >= 'a' and <= 'f' or
                >= 'A' and <= 'F');

    private sealed record TransactionPartition(
        IReadOnlyList<Id65AuthoringEditorAggregateProtectedRange> ProtectedRanges,
        int GuardedByteCount,
        int ProtectedByteCount,
        string ProtectedComplementSha256);
}
