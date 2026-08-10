using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

internal enum Id65SupportProfileKind
{
    Empty,
    Core4,
    Full8
}

internal enum Id65SupportTransitionKind
{
    AddCoreSupport,
    AddEnemyBay,
    RemoveEnemyBay,
    RemoveCore
}

internal sealed record Id65SupportFrozenTransitionPins(
    int ChangedByteCount,
    int DiffRangeCount,
    string DiffManifestSha256,
    string OwnedRangeMapSha256,
    string RelocationMapSha256,
    string RebaseMapSha256,
    string TransactionSha256,
    string DeterministicPlanSha256);

internal sealed record Id65SupportVertexIntent(
    string Id,
    string LowDetailHandle,
    string HighDetailHandle,
    Id65AuthoringPoint Point,
    string EncodedWordHex);

internal sealed class Id65SupportFaceIntent
{
    private readonly ReadOnlyCollection<Id65AuthoringPoint> _collisionPoints;
    private readonly ReadOnlyCollection<Id65AuthoringCollisionCell> _collisionCells;

    internal Id65SupportFaceIntent(
        string id,
        string direction,
        string lowDetailHandle,
        string highDetailHandle,
        string hpLpPairId,
        string lowDetailHex,
        string highDetailHex,
        string collisionHandle,
        int collisionTriangleIndex,
        IEnumerable<Id65AuthoringPoint> collisionPoints,
        long renderNormalZ,
        long collisionNormalZ,
        IEnumerable<Id65AuthoringCollisionCell> collisionCells)
    {
        Id = id;
        Direction = direction;
        LowDetailHandle = lowDetailHandle;
        HighDetailHandle = highDetailHandle;
        HpLpPairId = hpLpPairId;
        LowDetailHex = lowDetailHex;
        HighDetailHex = highDetailHex;
        CollisionHandle = collisionHandle;
        CollisionTriangleIndex = collisionTriangleIndex;
        _collisionPoints = Array.AsReadOnly(collisionPoints.ToArray());
        RenderNormalZ = renderNormalZ;
        CollisionNormalZ = collisionNormalZ;
        _collisionCells = Array.AsReadOnly(collisionCells.ToArray());
    }

    public string Id { get; }
    public string Direction { get; }
    public string LowDetailHandle { get; }
    public string HighDetailHandle { get; }
    public string HpLpPairId { get; }
    public string LowDetailHex { get; }
    public string HighDetailHex { get; }
    public string CollisionHandle { get; }
    public int CollisionTriangleIndex { get; }
    public IReadOnlyList<Id65AuthoringPoint> CollisionPoints => _collisionPoints;
    public long RenderNormalZ { get; }
    public long CollisionNormalZ { get; }
    public IReadOnlyList<Id65AuthoringCollisionCell> CollisionCells => _collisionCells;
}

internal sealed class Id65SupportSectorIntent
{
    private readonly ReadOnlyCollection<Id65SupportVertexIntent> _vertices;
    private readonly ReadOnlyCollection<Id65SupportFaceIntent> _faces;

    internal Id65SupportSectorIntent(
        string id,
        int sectorIndex,
        Id65AuthoringPoint center,
        Id65AuthoringPoint origin,
        Id65AuthoringPoint boundsMinimum,
        Id65AuthoringPoint boundsMaximum,
        int radius,
        string exactHeaderHex,
        string exactSectorSha256,
        IEnumerable<Id65SupportVertexIntent> vertices,
        IEnumerable<Id65SupportFaceIntent> faces)
    {
        Id = id;
        SectorIndex = sectorIndex;
        Center = center;
        Origin = origin;
        BoundsMinimum = boundsMinimum;
        BoundsMaximum = boundsMaximum;
        Radius = radius;
        ExactHeaderHex = exactHeaderHex;
        ExactSectorSha256 = exactSectorSha256;
        _vertices = Array.AsReadOnly(vertices.ToArray());
        _faces = Array.AsReadOnly(faces.ToArray());
    }

    public string Id { get; }
    public int SectorIndex { get; }
    public Id65AuthoringPoint Center { get; }
    public Id65AuthoringPoint Origin { get; }
    public Id65AuthoringPoint BoundsMinimum { get; }
    public Id65AuthoringPoint BoundsMaximum { get; }
    public int Radius { get; }
    public string ExactHeaderHex { get; }
    public string ExactSectorSha256 { get; }
    public IReadOnlyList<Id65SupportVertexIntent> Vertices => _vertices;
    public IReadOnlyList<Id65SupportFaceIntent> Faces => _faces;
}

internal sealed record Id65SupportSeamIntent(
    string Id,
    int FirstSectorIndex,
    string FirstEdge,
    int SecondSectorIndex,
    string SecondEdge,
    Id65AuthoringPoint Start,
    Id65AuthoringPoint End,
    bool LowDetailExact,
    bool HighDetailExact,
    bool TjunctionFree);

internal sealed class Id65SupportCollisionCellIntent
{
    private readonly ReadOnlyCollection<int> _triangleIndexes;

    internal Id65SupportCollisionCellIntent(
        Id65AuthoringCollisionCell cell,
        int treePointerByteOffset,
        int blockWordOffset,
        IEnumerable<int> triangleIndexes)
    {
        Cell = cell;
        TreePointerByteOffset = treePointerByteOffset;
        BlockWordOffset = blockWordOffset;
        _triangleIndexes = Array.AsReadOnly(triangleIndexes.ToArray());
    }

    public Id65AuthoringCollisionCell Cell { get; }
    public int TreePointerByteOffset { get; }
    public int BlockWordOffset { get; }
    public IReadOnlyList<int> TriangleIndexes => _triangleIndexes;
}

internal sealed record Id65SupportStateLayout(
    int TextureOffset,
    int TextureByteLength,
    int EnvironmentOffset,
    int EnvironmentByteLength,
    int OcclusionOffset,
    int OcclusionByteLength,
    int SpecialSurfaceOffset,
    int SpecialSurfaceByteLength,
    int CollisionOffset,
    int CollisionByteLength,
    int CycloramaOffset,
    int CycloramaByteLength,
    int PortalOffset,
    int PortalByteLength,
    int ParticlesOffset,
    int ParticlesByteLength,
    int SoundOffset,
    int SoundByteLength,
    int UsedModelBytes,
    int ZeroTailBytes);

/// <summary>
/// Declarative identity of one state in the bounded support-retirement graph.
/// It contains geometry and native semantic facts, never model bytes, a prior
/// compiler result, a path, or publisher state.
/// </summary>
internal sealed class Id65AuthoringSupportManifest
{
    private readonly ReadOnlyCollection<Id65SupportSectorIntent> _sectors;
    private readonly ReadOnlyCollection<Id65SupportSeamIntent> _seams;
    private readonly ReadOnlyCollection<Id65SupportCollisionCellIntent> _collisionCells;
    private readonly ReadOnlyCollection<int> _occlusionGroup0Sectors;

    internal Id65AuthoringSupportManifest(
        string profileId,
        Id65SupportProfileKind kind,
        int lockedSubstrateSectorCount,
        bool retiresLockedSector217,
        bool clearsInheritedCollisionLookups,
        int collisionTriangleCount,
        int collisionAssignment,
        int authoredTranslationWorld,
        string textureWitnessSha256,
        string textureIntentId,
        IEnumerable<Id65SupportSectorIntent> sectors,
        IEnumerable<Id65SupportSeamIntent> seams,
        IEnumerable<Id65SupportCollisionCellIntent> collisionCells,
        IEnumerable<int> occlusionGroup0Sectors,
        string collisionTreeSha256,
        string collisionBlocksSha256,
        string collisionComponentSha256,
        int collisionBlocksUsedBytes,
        string occlusionSha256,
        string occlusionOpaqueTailSha256,
        Id65SupportStateLayout layout,
        string? declaredCanonicalJson = null,
        string? declaredCanonicalSha256 = null)
    {
        ProfileId = profileId;
        Kind = kind;
        LockedSubstrateSectorCount = lockedSubstrateSectorCount;
        RetiresLockedSector217 = retiresLockedSector217;
        ClearsInheritedCollisionLookups = clearsInheritedCollisionLookups;
        CollisionTriangleCount = collisionTriangleCount;
        CollisionAssignment = collisionAssignment;
        AuthoredTranslationWorld = authoredTranslationWorld;
        TextureWitnessSha256 = textureWitnessSha256;
        TextureIntentId = textureIntentId;
        _sectors = Array.AsReadOnly(sectors.ToArray());
        _seams = Array.AsReadOnly(seams.ToArray());
        _collisionCells = Array.AsReadOnly(collisionCells.ToArray());
        _occlusionGroup0Sectors = Array.AsReadOnly(occlusionGroup0Sectors.ToArray());
        CollisionTreeSha256 = collisionTreeSha256;
        CollisionBlocksSha256 = collisionBlocksSha256;
        CollisionComponentSha256 = collisionComponentSha256;
        CollisionBlocksUsedBytes = collisionBlocksUsedBytes;
        OcclusionSha256 = occlusionSha256;
        OcclusionOpaqueTailSha256 = occlusionOpaqueTailSha256;
        Layout = layout;
        string canonical = RecomputeCanonicalJson();
        CanonicalJson = declaredCanonicalJson ?? canonical;
        CanonicalSha256 = declaredCanonicalSha256 ??
            Id65AuthoringSupportReplacementCompiler.Hash(Encoding.UTF8.GetBytes(CanonicalJson));
    }

    public string ProfileId { get; }
    public Id65SupportProfileKind Kind { get; }
    public int LockedSubstrateSectorCount { get; }
    public bool RetiresLockedSector217 { get; }
    public bool ClearsInheritedCollisionLookups { get; }
    public int CollisionTriangleCount { get; }
    public int CollisionAssignment { get; }
    public int AuthoredTranslationWorld { get; }
    public int AuthoredTranslationRaw => checked(AuthoredTranslationWorld << 4);
    public string TextureWitnessSha256 { get; }
    public string TextureIntentId { get; }
    public IReadOnlyList<Id65SupportSectorIntent> Sectors => _sectors;
    public IReadOnlyList<Id65SupportSeamIntent> Seams => _seams;
    public IReadOnlyList<Id65SupportCollisionCellIntent> CollisionCells => _collisionCells;
    public IReadOnlyList<int> OcclusionGroup0Sectors => _occlusionGroup0Sectors;
    public string CollisionTreeSha256 { get; }
    public string CollisionBlocksSha256 { get; }
    public string CollisionComponentSha256 { get; }
    public int CollisionBlocksUsedBytes { get; }
    public string OcclusionSha256 { get; }
    public string OcclusionOpaqueTailSha256 { get; }
    public Id65SupportStateLayout Layout { get; }
    public string CanonicalJson { get; }
    public string CanonicalSha256 { get; }

    internal string RecomputeCanonicalJson()
    {
        object Point(Id65AuthoringPoint point) => new { point.X, point.Y, point.Z };
        object Cell(Id65AuthoringCollisionCell cell) => new { cell.X, cell.Y, cell.Z };
        return JsonSerializer.Serialize(new
        {
            profileId = ProfileId,
            kind = Kind.ToString(),
            lockedSubstrateSectorCount = LockedSubstrateSectorCount,
            retiresLockedSector217 = RetiresLockedSector217,
            clearsInheritedCollisionLookups = ClearsInheritedCollisionLookups,
            collisionTriangleCount = CollisionTriangleCount,
            collisionAssignment = CollisionAssignment,
            authoredTranslationWorld = AuthoredTranslationWorld,
            textureWitnessSha256 = TextureWitnessSha256,
            textureIntentId = TextureIntentId,
            sectors = _sectors.Select(sector => new
            {
                sector.Id,
                sector.SectorIndex,
                center = Point(sector.Center),
                origin = Point(sector.Origin),
                boundsMinimum = Point(sector.BoundsMinimum),
                boundsMaximum = Point(sector.BoundsMaximum),
                sector.Radius,
                sector.ExactHeaderHex,
                sector.ExactSectorSha256,
                vertices = sector.Vertices.Select(vertex => new
                {
                    vertex.Id,
                    vertex.LowDetailHandle,
                    vertex.HighDetailHandle,
                    point = Point(vertex.Point),
                    vertex.EncodedWordHex
                }),
                faces = sector.Faces.Select(face => new
                {
                    face.Id,
                    face.Direction,
                    face.LowDetailHandle,
                    face.HighDetailHandle,
                    face.HpLpPairId,
                    face.LowDetailHex,
                    face.HighDetailHex,
                    face.CollisionHandle,
                    face.CollisionTriangleIndex,
                    collisionPoints = face.CollisionPoints.Select(Point),
                    face.RenderNormalZ,
                    face.CollisionNormalZ,
                    collisionCells = face.CollisionCells.Select(Cell)
                })
            }),
            seams = _seams.Select(seam => new
            {
                seam.Id,
                seam.FirstSectorIndex,
                seam.FirstEdge,
                seam.SecondSectorIndex,
                seam.SecondEdge,
                start = Point(seam.Start),
                end = Point(seam.End),
                seam.LowDetailExact,
                seam.HighDetailExact,
                seam.TjunctionFree
            }),
            collisionCells = _collisionCells.Select(cell => new
            {
                cell = Cell(cell.Cell),
                cell.TreePointerByteOffset,
                cell.BlockWordOffset,
                triangleIndexes = cell.TriangleIndexes
            }),
            occlusionGroup0Sectors = _occlusionGroup0Sectors,
            collisionTreeSha256 = CollisionTreeSha256,
            collisionBlocksSha256 = CollisionBlocksSha256,
            collisionComponentSha256 = CollisionComponentSha256,
            collisionBlocksUsedBytes = CollisionBlocksUsedBytes,
            occlusionSha256 = OcclusionSha256,
            occlusionOpaqueTailSha256 = OcclusionOpaqueTailSha256,
            layout = Layout
        }, new JsonSerializerOptions { WriteIndented = false });
    }
}

internal sealed class Id65AuthoringSupportLockedSource
{
    private readonly byte[] _row80;
    private readonly ReadOnlyCollection<Id65V2NativeTexturePackedRow> _textureRows;
    private readonly ReadOnlyCollection<Id65V2NativeTexturePagePatch> _pagePatches;

    internal Id65AuthoringSupportLockedSource(
        byte[] row80,
        IEnumerable<Id65V2NativeTexturePackedRow> textureRows,
        IEnumerable<Id65V2NativeTexturePagePatch> pagePatches,
        string textureWitnessSha256)
    {
        _row80 = row80.ToArray();
        _textureRows = Array.AsReadOnly(textureRows.Select(item => item.DeepCopy()).ToArray());
        _pagePatches = Array.AsReadOnly(pagePatches.Select(item => item.DeepCopy()).ToArray());
        Row80Sha256 = Id65AuthoringSupportReplacementCompiler.Hash(_row80);
        TextureWitnessSha256 = textureWitnessSha256;
        TextureProjectionSha256 = Id65AuthoringSupportReplacementCompiler.ComputeTextureProjectionCanonical(
            _textureRows,
            _pagePatches);
    }

    public int Row80ByteLength => _row80.Length;
    public string Row80Sha256 { get; }
    public string TextureWitnessSha256 { get; }
    public string TextureProjectionSha256 { get; }
    public bool ContainsPath => false;
    public bool ContainsCompiledAfterimage => false;
    public bool ContainsSlicePlan => false;
    public bool WritesFileSystem => false;

    internal byte[] CopyRow80() => _row80.ToArray();
    internal IReadOnlyList<Id65V2NativeTexturePackedRow> CopyTextureRows() =>
        _textureRows.Select(item => item.DeepCopy()).ToArray();
    internal IReadOnlyList<Id65V2NativeTexturePagePatch> CopyPagePatches() =>
        _pagePatches.Select(item => item.DeepCopy()).ToArray();
}

internal sealed class Id65SupportDesiredLeaf
{
    private readonly byte[] _lockedPreimage;
    private readonly byte[] _desiredBytes;

    internal Id65SupportDesiredLeaf(
        string stableId,
        Id65LogicalAddress logicalAddress,
        int dataRelativeOffset,
        byte[] lockedPreimage,
        byte[] desiredBytes)
    {
        if (lockedPreimage.Length == 0 || lockedPreimage.Length != desiredBytes.Length)
            throw new ArgumentException("A support desired leaf must have equal nonempty preimage/output bytes.");
        StableId = stableId;
        LogicalAddress = logicalAddress;
        DataRelativeOffset = dataRelativeOffset;
        _lockedPreimage = lockedPreimage.ToArray();
        _desiredBytes = desiredBytes.ToArray();
        LockedPreimageSha256 = Id65AuthoringSupportReplacementCompiler.Hash(_lockedPreimage);
        DesiredSha256 = Id65AuthoringSupportReplacementCompiler.Hash(_desiredBytes);
    }

    public string StableId { get; }
    public Id65LogicalAddress LogicalAddress { get; }
    public int DataRelativeOffset { get; }
    public int ByteLength => _lockedPreimage.Length;
    public string LockedPreimageSha256 { get; }
    public string DesiredSha256 { get; }

    internal byte[] CopyLockedPreimage() => _lockedPreimage.ToArray();
    internal byte[] CopyDesiredBytes() => _desiredBytes.ToArray();
    internal Id65SupportDesiredLeaf DeepCopy() =>
        new(StableId, LogicalAddress, DataRelativeOffset, _lockedPreimage, _desiredBytes);
}

/// <summary>
/// Immutable desired-state leaves for composing Core4 directly from the exact
/// locked row. This deliberately exposes neither a complete row-80 afterimage
/// nor a transition/slice plan; an owning composite must merge these typed
/// leaves into its own single locked-source transaction.
/// </summary>
internal sealed class Id65SupportDesiredStateProjection
{
    private readonly byte[] _coreModel;
    private readonly ReadOnlyCollection<Id65V2NativeTexturePagePatch> _texturePageDescriptors;
    private readonly ReadOnlyCollection<Id65SupportDesiredLeaf> _desiredLeaves;

    internal Id65SupportDesiredStateProjection(
        string lockedRow80Sha256,
        string textureWitnessSha256,
        string textureProjectionSha256,
        string coreManifestSha256,
        byte[] coreModel,
        string coreRow80CrossCheckSha256,
        IEnumerable<Id65V2NativeTexturePagePatch> texturePageDescriptors,
        int texturePageChangedByteCount,
        string texturePageDescriptorMapSha256,
        IEnumerable<Id65SupportDesiredLeaf> desiredLeaves,
        string desiredLeafMapSha256,
        Id65SupportStateReadback readback,
        string readbackSha256,
        Id65SupportCapacityReadback capacity,
        string capacitySha256,
        string canonicalSha256)
    {
        LockedRow80Sha256 = lockedRow80Sha256;
        TextureWitnessSha256 = textureWitnessSha256;
        TextureProjectionSha256 = textureProjectionSha256;
        CoreManifestSha256 = coreManifestSha256;
        _coreModel = coreModel.ToArray();
        CoreModelSha256 = Id65AuthoringSupportReplacementCompiler.Hash(_coreModel);
        CoreRow80CrossCheckSha256 = coreRow80CrossCheckSha256;
        _texturePageDescriptors = Array.AsReadOnly(
            texturePageDescriptors.Select(item => item.DeepCopy()).ToArray());
        TexturePageChangedByteCount = texturePageChangedByteCount;
        TexturePageDescriptorMapSha256 = texturePageDescriptorMapSha256;
        _desiredLeaves = Array.AsReadOnly(desiredLeaves.Select(item => item.DeepCopy()).ToArray());
        DesiredLeafMapSha256 = desiredLeafMapSha256;
        Readback = readback;
        ReadbackSha256 = readbackSha256;
        Capacity = capacity;
        CapacitySha256 = capacitySha256;
        CanonicalSha256 = canonicalSha256;
    }

    public string ProfileId => Id65AuthoringSupportReplacementCompiler.DesiredProjectionProfileId;
    public Id65SupportProfileKind Kind => Id65SupportProfileKind.Core4;
    public string LockedRow80Sha256 { get; }
    public string TextureWitnessSha256 { get; }
    public string TextureProjectionSha256 { get; }
    public string CoreManifestSha256 { get; }
    public int CoreModelByteLength => _coreModel.Length;
    public string CoreModelSha256 { get; }
    public string CoreRow80CrossCheckSha256 { get; }
    public IReadOnlyList<Id65V2NativeTexturePagePatch> TexturePageDescriptors =>
        _texturePageDescriptors;
    public int TexturePageDescriptorCount => _texturePageDescriptors.Count;
    public int TexturePageChangedByteCount { get; }
    public string TexturePageDescriptorMapSha256 { get; }
    public IReadOnlyList<Id65SupportDesiredLeaf> DesiredLeaves => _desiredLeaves;
    public string DesiredLeafMapSha256 { get; }
    public Id65SupportStateReadback Readback { get; }
    public string ReadbackSha256 { get; }
    public Id65SupportCapacityReadback Capacity { get; }
    public string CapacitySha256 { get; }
    public string CanonicalSha256 { get; }

    public bool DirectLockedSourceDerived => true;
    public bool ContainsFullAuthoredRow80 => false;
    public bool ContainsCompiledAfterimage => false;
    public bool AfterimageStackingAuthorized => false;
    public bool ContainsSlicePlan => false;
    public bool ContainsPath => false;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool WritesCue => false;
    public bool PublisherCalled => false;
    public bool WriterAuthorized => false;
    public bool AppIntegrated => false;
    public bool CreateBinEnabled => false;
    public bool NormalCreateBinEnabled => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool RuntimeAccepted => false;
    public bool ReleaseIntegrated => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;
    public bool ExecutableMutationExcluded => true;
    public bool Full8Excluded => true;

    internal byte[] CopyCoreModel() => _coreModel.ToArray();
    internal IReadOnlyList<Id65V2NativeTexturePagePatch> CopyTexturePageDescriptors() =>
        _texturePageDescriptors.Select(item => item.DeepCopy()).ToArray();
    internal IReadOnlyList<Id65SupportDesiredLeaf> CopyDesiredLeaves() =>
        _desiredLeaves.Select(item => item.DeepCopy()).ToArray();
}

internal sealed record Id65AuthoringSupportCompilerLimits(
    int ModelByteCapacity = 0x94800,
    int MaximumOutputUsedModelBytes = 0x94800,
    int MaximumSceneSectorCount = 217,
    int MaximumSectorByteLength = 0xC8,
    int MaximumSectorFaceCount = 255,
    int MaximumCollisionTriangleCount = 19_808,
    int CollisionTreeCapacityBytes = 0x6A60,
    int CollisionBlockCapacityBytes = 0x17984,
    int MaximumNativeTextureId = 127);

internal sealed class Id65SupportOwnedRange
{
    private readonly byte[] _before;
    private readonly byte[] _after;

    internal Id65SupportOwnedRange(
        string stableId,
        string ownerId,
        int dataRelativeOffset,
        byte[] before,
        byte[] after)
    {
        StableId = stableId;
        OwnerId = ownerId;
        DataRelativeOffset = dataRelativeOffset;
        _before = before.ToArray();
        _after = after.ToArray();
        BeforeSha256 = Id65AuthoringSupportReplacementCompiler.Hash(_before);
        AfterSha256 = Id65AuthoringSupportReplacementCompiler.Hash(_after);
    }

    public string StableId { get; }
    public string OwnerId { get; }
    public int DataRelativeOffset { get; }
    public int ByteLength => _before.Length;
    public string BeforeSha256 { get; }
    public string AfterSha256 { get; }
    internal byte[] CopyBefore() => _before.ToArray();
    internal byte[] CopyAfter() => _after.ToArray();
    internal Id65SupportOwnedRange DeepCopy() =>
        new(StableId, OwnerId, DataRelativeOffset, _before, _after);
    internal Id65SupportOwnedRange Invert() =>
        new(StableId, OwnerId, DataRelativeOffset, _after, _before);
}

internal sealed record Id65SupportDiffRange(
    int DataRelativeOffset,
    int ByteLength,
    string OwnerStableId,
    string BeforeSha256,
    string AfterSha256);

internal sealed record Id65SupportCapacityReadback(
    int ModelByteCapacity,
    int SourceUsedModelBytes,
    int OutputUsedModelBytes,
    int SourceZeroTailBytes,
    int OutputZeroTailBytes,
    int SourceSectorCount,
    int OutputSectorCount,
    int SourceCollisionBlocksUsedBytes,
    int OutputCollisionBlocksUsedBytes,
    int CollisionTriangleCount,
    int CollisionTreeCapacityBytes,
    int CollisionBlockCapacityBytes,
    int TextureCount,
    int HighestTextureId,
    int NativeObservedMinimumSectorCount)
{
    internal Id65SupportCapacityReadback Invert() => new(
        ModelByteCapacity,
        OutputUsedModelBytes,
        SourceUsedModelBytes,
        OutputZeroTailBytes,
        SourceZeroTailBytes,
        OutputSectorCount,
        SourceSectorCount,
        OutputCollisionBlocksUsedBytes,
        SourceCollisionBlocksUsedBytes,
        CollisionTriangleCount,
        CollisionTreeCapacityBytes,
        CollisionBlockCapacityBytes,
        TextureCount,
        HighestTextureId,
        NativeObservedMinimumSectorCount);
}

internal sealed record Id65SupportStateReadback(
    Id65SupportProfileKind Kind,
    string Row80Sha256,
    string ModelSha256,
    string EnvironmentSha256,
    string CollisionSha256,
    string CollisionTreeSha256,
    string CollisionBlocksSha256,
    string OcclusionSha256,
    int SectorCount,
    int CollisionTriangleCount,
    int ActiveCollisionCellCount,
    int CollisionBlocksUsedBytes,
    int UsedModelBytes,
    int ZeroTailBytes,
    bool InheritedActiveLeavesCleared,
    bool ExactHpLpPairing,
    bool ExactSeams,
    bool ProtectedComponentsPreserved,
    bool RuntimeAccepted);

internal sealed class Id65CompiledSupportReplacement
{
    private readonly byte[] _sourceRow80;
    private readonly byte[] _outputRow80;
    private readonly byte[] _sourceModel;
    private readonly byte[] _outputModel;
    private readonly ReadOnlyCollection<Id65SupportOwnedRange> _ownedRanges;
    private readonly ReadOnlyCollection<Id65SupportDiffRange> _diffRanges;
    private readonly ReadOnlyCollection<Id65ModelComponentRelocation> _relocations;
    private readonly ReadOnlyCollection<Id65StableHandleRebase> _rebases;

    internal Id65CompiledSupportReplacement(
        Id65SupportTransitionKind transition,
        Id65AuthoringSupportManifest sourceManifest,
        Id65AuthoringSupportManifest outputManifest,
        byte[] sourceRow80,
        byte[] outputRow80,
        byte[] sourceModel,
        byte[] outputModel,
        IEnumerable<Id65SupportOwnedRange> ownedRanges,
        IEnumerable<Id65SupportDiffRange> diffRanges,
        IEnumerable<Id65ModelComponentRelocation> relocations,
        IEnumerable<Id65StableHandleRebase> rebases,
        Id65SupportCapacityReadback capacity,
        Id65SupportStateReadback sourceState,
        Id65SupportStateReadback outputState,
        int changedByteCount,
        string diffManifestSha256,
        string ownedRangeMapSha256,
        string relocationMapSha256,
        string rebaseMapSha256,
        string transactionSha256,
        string deterministicPlanSha256)
    {
        Transition = transition;
        SourceManifest = sourceManifest;
        OutputManifest = outputManifest;
        _sourceRow80 = sourceRow80.ToArray();
        _outputRow80 = outputRow80.ToArray();
        _sourceModel = sourceModel.ToArray();
        _outputModel = outputModel.ToArray();
        SourceRow80Sha256 = Id65AuthoringSupportReplacementCompiler.Hash(_sourceRow80);
        OutputRow80Sha256 = Id65AuthoringSupportReplacementCompiler.Hash(_outputRow80);
        SourceModelSha256 = Id65AuthoringSupportReplacementCompiler.Hash(_sourceModel);
        OutputModelSha256 = Id65AuthoringSupportReplacementCompiler.Hash(_outputModel);
        _ownedRanges = Array.AsReadOnly(ownedRanges.Select(item => item.DeepCopy()).ToArray());
        _diffRanges = Array.AsReadOnly(diffRanges.ToArray());
        _relocations = Array.AsReadOnly(relocations.ToArray());
        _rebases = Array.AsReadOnly(rebases.ToArray());
        Capacity = capacity;
        SourceState = sourceState;
        OutputState = outputState;
        ChangedByteCount = changedByteCount;
        DiffManifestSha256 = diffManifestSha256;
        OwnedRangeMapSha256 = ownedRangeMapSha256;
        RelocationMapSha256 = relocationMapSha256;
        RebaseMapSha256 = rebaseMapSha256;
        TransactionSha256 = transactionSha256;
        DeterministicPlanSha256 = deterministicPlanSha256;
    }

    public string ProfileId => Id65AuthoringSupportReplacementCompiler.ProfileId;
    public Id65SupportTransitionKind Transition { get; }
    public Id65AuthoringSupportManifest SourceManifest { get; }
    public Id65AuthoringSupportManifest OutputManifest { get; }
    public string SourceRow80Sha256 { get; }
    public string OutputRow80Sha256 { get; }
    public string SourceModelSha256 { get; }
    public string OutputModelSha256 { get; }
    public IReadOnlyList<Id65SupportOwnedRange> OwnedRanges => _ownedRanges;
    public IReadOnlyList<Id65SupportDiffRange> DiffRanges => _diffRanges;
    public IReadOnlyList<Id65ModelComponentRelocation> Relocations => _relocations;
    public IReadOnlyList<Id65StableHandleRebase> HandleRebases => _rebases;
    public Id65SupportCapacityReadback Capacity { get; }
    public Id65SupportStateReadback SourceState { get; }
    public Id65SupportStateReadback OutputState { get; }
    public int ChangedByteCount { get; }
    public int DiffRangeCount => _diffRanges.Count;
    public string DiffManifestSha256 { get; }
    public string OwnedRangeMapSha256 { get; }
    public string RelocationMapSha256 { get; }
    public string RebaseMapSha256 { get; }
    public string TransactionSha256 { get; }
    public string DeterministicPlanSha256 { get; }

    public bool BuildsBothStatesFromExactLockedSource => true;
    public bool AcceptsCompiledAfterimages => false;
    public bool AcceptsSlicePlans => false;
    public bool RetiresLockedSector217 =>
        SourceManifest.RetiresLockedSector217 || OutputManifest.RetiresLockedSector217;
    public bool ClearsInheritedCollisionLookups =>
        SourceManifest.ClearsInheritedCollisionLookups || OutputManifest.ClearsInheritedCollisionLookups;
    public bool DirectRelocationsAndRebases => true;
    public bool ExactInverseVerified => true;
    public bool DeterministicReadbackRequired => true;
    public bool PostV2RetirementDiscriminatorOnly => true;
    public bool FourOrEightSectorsBelowNativeObservedMinimum64 =>
        SourceManifest.Sectors.Count is 4 or 8 || OutputManifest.Sectors.Count is 4 or 8;
    public bool RuntimeAccepted => false;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool WritesCue => false;
    public bool RetiredPublisherCalled => false;
    public bool AppIntegrated => false;
    public bool CreateBinEnabled => false;
    public bool NormalCreateBinEnabled => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool ReleaseIntegrated => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;

    internal byte[] CopySourceRow80() => _sourceRow80.ToArray();
    internal byte[] CopyOutputRow80() => _outputRow80.ToArray();
    internal byte[] CopySourceModel() => _sourceModel.ToArray();
    internal byte[] CopyOutputModel() => _outputModel.ToArray();
}

/// <summary>
/// Pure in-memory, post-v2 support replacement compiler. Both sides of every
/// transition are independently reconstructed from the immutable display-name
/// row 80 and the frozen typed T66 witness. Compiled afterimages are never an
/// input and no filesystem or publication surface exists.
/// </summary>
internal static class Id65AuthoringSupportReplacementCompiler
{
    public const string ProfileId = "id65-authoring-support-replacement-static-in-memory-v1";
    public const string DesiredProjectionProfileId =
        "id65-authoring-support-core4-desired-state-projection-v1";
    public const string EmptyProfileId = "support-empty-v1";
    public const string CoreProfileId = "support-core-4-v1";
    public const string FullProfileId = "support-full-8-v1";
    public const string ExpectedLockedRow80Sha256 =
        "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0";
    public const string ExpectedLockedModelSha256 =
        "1aa6950fe78e71ef4506d823fd33806c13a32838cdb00aadc4e35daffcf17f47";
    public const string ExpectedEmptyModelSha256 =
        "e9650867deb28a9b1673d56b7ae6f8576c0c9b1e739c6f52fdd321a53bab376f";
    public const string ExpectedEmptyRow80Sha256 =
        "9a7ba28aa977c7f2211c5a467d09f5f0f8e8ebc6bcdd5366d22564f44dadd237";
    public const string ExpectedCoreEnvironmentSha256 =
        "17d99300e91e05bab3f694f8bfc187c481251a61cb8a0fc382a14f6f10cc346c";
    public const string ExpectedFullEnvironmentSha256 =
        "57801025e0795aa2a805bf043299dd5af5ce7e7ed68173620a4caeb063ddea1c";
    public const string ExpectedCoreTreeSha256 =
        "c113ac30da735d5b8d3477a5686f9a2bd8a92dd6df9ec8ca9be54e821495c96b";
    public const string ExpectedCoreBlocksSha256 =
        "84079712f3a5a1cb14a40a1702e321c8b95c6132f7428ab21b83616250208115";
    public const string ExpectedCoreCollisionSha256 =
        "0db256e4d63f81d7ddb2e02940d774766540771bc96e9389777850d30ea9dbf3";
    public const string ExpectedFullTreeSha256 =
        "e13d9a2b00ca98305bae8413c5f116966ea882ce2b281384719123f967c09296";
    public const string ExpectedFullBlocksSha256 =
        "f0c6feb671a4c4b23b660e7fd8b4fe17249e6bb92662728e125c296bae382413";
    public const string ExpectedFullCollisionSha256 =
        "62964529a1f42fd90eaa3d5b09eafea4b684c1bd23f3e176a2cc1668eebdb67e";
    public const string ExpectedCoreOcclusionSha256 =
        "f0c08a832de0ade506a60c057b7b42b7803e2b532b01626e34d6cb58a7b6d09b";
    public const string ExpectedFullOcclusionSha256 =
        "3436e7e6a41f2214f4de074ebd7e6de57114b65a3d0aff5934a255f29e600231";
    public const string ExpectedOcclusionOpaqueTailSha256 =
        "660d21b9b9c402c46f27e3cdf4810733f2788251406654b8a6854342f9669c41";

    // Frozen by the dedicated smoke after independently deriving the exact
    // locked-source state images and transition proofs.
    public const string ExpectedCoreModelSha256 =
        "c062d328be8ee14e4afd1e95d7235472242993266cdcd44f21a99df4ce34bfcd";
    public const string ExpectedFullModelSha256 =
        "8967fbd01fdcffe17509b3fa3579d27211f77dbfd17611d93ceb501ac5db540f";
    public const string ExpectedCoreRow80Sha256 =
        "e6921e57cc34dbf3d8922e04381701f9b416aecdc53b16b4fae2f257541c44ba";
    public const string ExpectedFullRow80Sha256 =
        "3017eb5a3c402cfa25b0145ffe467fa8eac05f37b8fe7a80de808418fb412c1a";
    public const string ExpectedLockedT1LowRowSha256 =
        "9fcb815b1bd5707fcdbb0c6187fe78611d2d21ea2156de972195bdf7461c13d0";
    public const string ExpectedLockedT1HighRowSha256 =
        "1ee34fbe3ada6f90c6bee51f602f5d073155133d83cc9137b0faa0c1d0113697";
    public const string ExpectedTextureProjectionSha256 =
        "899bc97cdbf7a3f9da2fa5132d25665fb6cf6f2596d7b49d68733ca0e52212a7";
    public const string ExpectedEmptyManifestSha256 =
        "fc162fc5db6afa30952ca749ac4f60ce98cee6d9bce1ff8aa3f7ed3ac856182f";
    public const string ExpectedCoreManifestSha256 =
        "aaac3076e2246aeb481e25241fcef9d564aa8c338bef07b0bdf83af11726c270";
    public const string ExpectedFullManifestSha256 =
        "4ccb922f43a9ed2551ce700f56270b6c1207adf1c49eb26984b96e0bdacdd0fb";
    public const string ExpectedCoreDesiredPageDescriptorMapSha256 =
        "f2330da51ac4066674c113588d8ceff57873ec4020d539097c65056bd37f4edc";
    public const string ExpectedCoreDesiredLeafMapSha256 =
        "cd889d2b24b0b7a37de066fc6e56500b8d414cd2889d58352897816da6074f73";
    public const string ExpectedCoreDesiredReadbackSha256 =
        "b59b8c317f3e1a0dae8eb43cf6ad299705e7d6760cec14016ffd87baec662195";
    public const string ExpectedCoreDesiredCapacitySha256 =
        "a573a9c0194388299dd99dc80059ef1a877fb44c035cacfc68b80009cdcc6d5b";
    public const string ExpectedCoreDesiredProjectionSha256 =
        "2dda622f3d08a78564a0f2a583cb73fee5ec731d5cacbac24302e093e9747fa2";
    public const int ExpectedTexturePagePatchCount = 7_949;
    public const int ExpectedTexturePageChangedByteCount = 402_194;

    private const int Row80ByteLength = 0x2E2000;
    private const int TexturePagesOffset = 0x800;
    private const int TexturePagesLength = 0xDE000;
    private const int ModelOffset = 0xDE800;
    private const int ModelLength = 0x94800;
    private const int LandingOffset = 0x1D0000;
    private const int T92XyOffset = 0x1D211C;
    private const int SourceTextureLength = 0x2F78;
    private const int TextureLength = 0x3030;
    private const int SourceEnvironmentOffset = 0x2F78;
    private const int SourceEnvironmentLength = 0x284A4;
    private const int SourceOcclusionOffset = 0x2B41C;
    private const int OcclusionLength = 0x974;
    private const int SourceSpecialOffset = 0x2BD90;
    private const int SpecialLength = 0x30;
    private const int SourceCollisionOffset = 0x2BDC0;
    private const int CollisionLength = 0x5FAE8;
    private const int SourceCycloramaOffset = 0x8B8A8;
    private const int CycloramaLength = 0x84E4;
    private const int SourcePortalOffset = 0x93D8C;
    private const int PortalLength = 4;
    private const int SourceParticlesOffset = 0x93D90;
    private const int ParticlesLength = 0x80;
    private const int SourceSoundOffset = 0x93E10;
    private const int SoundLength = 0x6F8;
    private const int CollisionTreeOffset = 0x20;
    private const int CollisionTreeLength = 0x6A60;
    private const int CollisionBlocksOffset = 0x6A80;
    private const int CollisionBlocksLength = 0x17984;
    private const int CollisionTrianglesOffset = 0x1E404;
    private const int CollisionAssignmentsOffset = 0x58484;
    private const int CollisionFlagsOffset = 0x5D1E4;
    private const int CollisionTriangleCount = 19_808;
    private const string CommonSectorPayloadHex =
        "00000420004000020040003E00C0073E00C007022D39300030552C0037622C00" +
        "008210000082100000C320000082100000043100008210000041400000821000" +
        "00000420004000020040003E00C0073E00C0070299756B00775D6600896A6A00" +
        "775D6600806268007A5F6800000001020101000242024001080A100000000203" +
        "0101000242024001080A1000000003040101000242024001080A100000000401" +
        "0101000242024001080A1000";

    private static readonly string[] SectorHeaders =
    [
        "000200025401000200010001000000020503040905030409FFFFFFFF",
        "E003000254010002E0020001000000020503040905030409FFFFFFFF",
        "0002E003540100020001E002000000020503040905030409FFFFFFFF",
        "E003E00354010002E002E002000000020503040905030409FFFFFFFF",
        "C005000254010002C0040001000000020503040905030409FFFFFFFF",
        "A007000254010002A0060001000000020503040905030409FFFFFFFF",
        "C005E00354010002C004E002000000020503040905030409FFFFFFFF",
        "A007E00354010002A006E002000000020503040905030409FFFFFFFF"
    ];

    private static readonly string[] SectorHashes =
    [
        "7b9193499920903f81bcf08dbf089fb68d9afe8c894b95f47c172cbc4de41529",
        "b3759494929c4f83a44a2ab0ef745ee345affd6ba2c52420d72dc44d29a70348",
        "e5a6aa6c2fe6e6989b5eb3a88e70b39e6d4d0184a79a2ba41eb4737fc4cd5718",
        "8fd2f5d075936d96d0528da538af21728253d22d10f7da1cd392e47b2d553259",
        "0488ca7cb68d1e84ccaf951e3a06330b810706490dc9c567cc902da31eef55b4",
        "3d5ad914cd4d1b104a125e255fba52d2a6702ac6578e5a9849d059bd32b395e0",
        "03755b4baeb4075b696256d26fa6ea02113e45c73ba422102059f6591960082e",
        "ae91c5cb7b24b82f4dc73dda3a0c1633720a7cb197472c14714dd74455353112"
    ];

    private static readonly int[][] CollisionRows =
    [
        [13_995, 19_807, 19_806, 19_805],
        [19_804, 19_803, 19_802, 19_801],
        [19_800, 19_799, 19_798, 19_797],
        [19_796, 19_795, 19_794, 19_793],
        [19_792, 19_791, 19_790, 19_789],
        [19_788, 19_787, 19_786, 19_785],
        [19_784, 19_783, 19_782, 19_781],
        [19_780, 19_779, 19_778, 19_777]
    ];

    private static readonly string[] LowFaceHex =
    [
        "0082100000821000",
        "00C3200000821000",
        "0004310000821000",
        "0041400000821000"
    ];

    private static readonly string[] HighFaceHex =
    [
        "000001020101000242024001080A1000",
        "000002030101000242024001080A1000",
        "000003040101000242024001080A1000",
        "000004010101000242024001080A1000"
    ];

    private static readonly string[] Directions = ["south", "east", "north", "west"];

    public static Id65AuthoringSupportLockedSource CaptureLockedSource(
        ReadOnlySpan<byte> lockedRow80,
        Id65V2NativeTextureWitness textureWitness)
    {
        ArgumentNullException.ThrowIfNull(textureWitness);
        if (lockedRow80.Length != Row80ByteLength)
            throw new InvalidDataException("The support compiler locked row-80 length changed.");
        RequireHash(Hash(lockedRow80), ExpectedLockedRow80Sha256, "locked display-name row 80");
        RequireHash(Hash(lockedRow80.Slice(ModelOffset, ModelLength)), ExpectedLockedModelSha256,
            "locked display-name model");
        ValidateTextureWitness(textureWitness);
        IReadOnlyList<Id65V2NativeTexturePackedRow> completeRows = BuildCompleteTextureProjection(
            lockedRow80.Slice(ModelOffset, ModelLength),
            textureWitness.CopyPackedRows());
        Id65AuthoringSupportLockedSource captured = new(
            lockedRow80.ToArray(),
            completeRows,
            textureWitness.CopyPagePatches(),
            textureWitness.CanonicalSha256);
        ValidateLockedSource(captured);
        return captured;
    }

    public static Id65AuthoringSupportManifest CreateEmptyManifest() =>
        BuildExpectedManifest(Id65SupportProfileKind.Empty);

    public static Id65AuthoringSupportManifest AddCoreSupport(Id65AuthoringSupportManifest source)
    {
        ValidateExactManifest(source, Id65SupportProfileKind.Empty);
        return BuildExpectedManifest(Id65SupportProfileKind.Core4);
    }

    public static Id65AuthoringSupportManifest AddEnemyBay(Id65AuthoringSupportManifest source)
    {
        ValidateExactManifest(source, Id65SupportProfileKind.Core4);
        return BuildExpectedManifest(Id65SupportProfileKind.Full8);
    }

    public static Id65AuthoringSupportManifest RemoveEnemyBay(Id65AuthoringSupportManifest source)
    {
        ValidateExactManifest(source, Id65SupportProfileKind.Full8);
        return BuildExpectedManifest(Id65SupportProfileKind.Core4);
    }

    public static Id65AuthoringSupportManifest RemoveCore(Id65AuthoringSupportManifest source)
    {
        ValidateExactManifest(source, Id65SupportProfileKind.Core4);
        return BuildExpectedManifest(Id65SupportProfileKind.Empty);
    }

    public static Id65SupportDesiredStateProjection BuildCore4DesiredStateProjection(
        Id65AuthoringSupportLockedSource lockedSource,
        Id65AuthoringSupportManifest coreManifest,
        Id65AuthoringSupportCompilerLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(lockedSource);
        ArgumentNullException.ThrowIfNull(coreManifest);
        ValidateLockedSource(lockedSource);
        ValidateExactManifest(coreManifest, Id65SupportProfileKind.Core4);
        Id65AuthoringSupportManifest emptyManifest = BuildExpectedManifest(Id65SupportProfileKind.Empty);
        ValidateLimits(limits ?? new(), emptyManifest, coreManifest);

        byte[] lockedRow80 = lockedSource.CopyRow80();
        byte[] lockedModel = lockedRow80.AsSpan(ModelOffset, ModelLength).ToArray();
        IReadOnlyList<Id65V2NativeTexturePackedRow> textureRows = lockedSource.CopyTextureRows();
        IReadOnlyList<Id65V2NativeTexturePagePatch> pageDescriptors =
            lockedSource.CopyPagePatches();
        byte[] coreModel = BuildStateModel(lockedModel, textureRows, coreManifest);

        // Build the complete image only as an internal identity/readback cross-check.
        // It is discarded here and is intentionally absent from the returned projection.
        byte[] row80CrossCheck = BuildStateRow80(lockedRow80, coreModel, pageDescriptors);
        RequireHash(Hash(row80CrossCheck), ExpectedCoreRow80Sha256,
            "Core4 desired-state row identity cross-check");
        Id65SupportStateReadback readback =
            ValidateState(coreManifest, lockedModel, coreModel, row80CrossCheck);
        Id65SupportCapacityReadback capacity = BuildCapacity(emptyManifest, coreManifest);
        Id65SupportDesiredLeaf[] desiredLeaves = BuildCore4DesiredLeaves(lockedRow80);

        string pageMapSha256 = HashTexturePageDescriptors(pageDescriptors);
        string leafMapSha256 = HashDesiredLeaves(desiredLeaves);
        string readbackSha256 = HashDesiredReadback(readback);
        string capacitySha256 = HashDesiredCapacity(capacity);
        string canonicalSha256 = HashCore4DesiredProjection(
            lockedSource,
            coreManifest,
            coreModel,
            pageDescriptors,
            pageMapSha256,
            desiredLeaves,
            leafMapSha256,
            readbackSha256,
            capacitySha256);

        Id65SupportDesiredStateProjection projection = new(
            lockedSource.Row80Sha256,
            lockedSource.TextureWitnessSha256,
            lockedSource.TextureProjectionSha256,
            coreManifest.CanonicalSha256,
            coreModel,
            ExpectedCoreRow80Sha256,
            pageDescriptors,
            pageDescriptors.Sum(item => item.ByteLength),
            pageMapSha256,
            desiredLeaves,
            leafMapSha256,
            readback,
            readbackSha256,
            capacity,
            capacitySha256,
            canonicalSha256);
        ValidateCore4DesiredProjection(projection);
        return projection;
    }

    public static Id65CompiledSupportReplacement CompileTransition(
        Id65AuthoringSupportLockedSource lockedSource,
        Id65AuthoringSupportManifest sourceManifest,
        Id65AuthoringSupportManifest targetManifest,
        Id65AuthoringSupportCompilerLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(lockedSource);
        ArgumentNullException.ThrowIfNull(sourceManifest);
        ArgumentNullException.ThrowIfNull(targetManifest);
        ValidateLockedSource(lockedSource);
        ValidateExactManifest(sourceManifest, sourceManifest.Kind);
        ValidateExactManifest(targetManifest, targetManifest.Kind);
        Id65SupportTransitionKind transition = RequireAllowedTransition(sourceManifest.Kind, targetManifest.Kind);
        ValidateLimits(limits ?? new(), sourceManifest, targetManifest);

        byte[] lockedRow80 = lockedSource.CopyRow80();
        byte[] lockedModel = lockedRow80.AsSpan(ModelOffset, ModelLength).ToArray();
        IReadOnlyList<Id65V2NativeTexturePackedRow> rows = lockedSource.CopyTextureRows();
        IReadOnlyList<Id65V2NativeTexturePagePatch> patches = lockedSource.CopyPagePatches();
        byte[] sourceModel = BuildStateModel(lockedModel, rows, sourceManifest);
        byte[] outputModel = BuildStateModel(lockedModel, rows, targetManifest);
        byte[] sourceRow80 = BuildStateRow80(lockedRow80, sourceModel, patches);
        byte[] outputRow80 = BuildStateRow80(lockedRow80, outputModel, patches);

        Id65SupportStateReadback sourceState = ValidateState(sourceManifest, lockedModel, sourceModel, sourceRow80);
        Id65SupportStateReadback outputState = ValidateState(targetManifest, lockedModel, outputModel, outputRow80);
        List<Id65SupportOwnedRange> owned = BuildOwnedRanges(sourceRow80, outputRow80);
        ValidateDiffOwnership(sourceRow80, outputRow80, owned);
        Id65SupportDiffRange[] diff = owned.Select(item => new Id65SupportDiffRange(
            item.DataRelativeOffset,
            item.ByteLength,
            item.StableId,
            item.BeforeSha256,
            item.AfterSha256)).ToArray();
        int changedBytes = diff.Sum(item => item.ByteLength);
        string diffHash = HashDiffRanges(diff);
        string ownedHash = HashOwnedRanges(owned);
        string transactionHash = HashTransaction(owned);
        Id65ModelComponentRelocation[] relocations =
            BuildRelocations(sourceManifest.Layout, targetManifest.Layout, sourceModel, outputModel);
        ValidateRelocations(relocations, sourceManifest.Layout, targetManifest.Layout, sourceModel, outputModel);
        Id65StableHandleRebase[] rebases = BuildRebases(sourceManifest, targetManifest);
        ValidateRebases(rebases, sourceManifest, targetManifest);
        string relocationHash = HashRelocations(relocations);
        string rebaseHash = HashRebases(rebases);
        Id65SupportCapacityReadback capacity = BuildCapacity(sourceManifest, targetManifest);
        string planHash = ComputeDeterministicPlanHash(
            transition,
            sourceManifest,
            targetManifest,
            sourceRow80,
            outputRow80,
            sourceModel,
            outputModel,
            diffHash,
            ownedHash,
            relocationHash,
            rebaseHash,
            transactionHash);

        Id65CompiledSupportReplacement compiled = new(
            transition,
            sourceManifest,
            targetManifest,
            sourceRow80,
            outputRow80,
            sourceModel,
            outputModel,
            owned,
            diff,
            relocations,
            rebases,
            capacity,
            sourceState,
            outputState,
            changedBytes,
            diffHash,
            ownedHash,
            relocationHash,
            rebaseHash,
            transactionHash,
            planHash);
        ValidateFrozenTransitionPins(compiled);
        byte[] applied = ApplyTransactional(compiled, compiled.CopySourceRow80(), reverse: false);
        byte[] rolledBack = ApplyTransactional(compiled, applied, reverse: true);
        Id65CompiledSupportReplacement inverse = InvertCore(compiled, verifyDoubleInverse: false);
        if (!applied.SequenceEqual(outputRow80) || !rolledBack.SequenceEqual(sourceRow80) ||
            !ApplyTransactional(inverse, outputRow80, reverse: false).SequenceEqual(sourceRow80) ||
            !InvertRelocations(InvertRelocations(relocations)).SequenceEqual(relocations) ||
            !InvertRebases(InvertRebases(rebases)).SequenceEqual(rebases))
            throw new InvalidDataException("The support transition exact inverse failed.");
        return compiled;
    }

    public static Id65CompiledSupportReplacement Invert(Id65CompiledSupportReplacement compiled)
    {
        ArgumentNullException.ThrowIfNull(compiled);
        Id65CompiledSupportReplacement inverse = InvertCore(compiled, verifyDoubleInverse: true);
        return inverse;
    }

    public static byte[] ApplyTransactional(
        Id65CompiledSupportReplacement compiled,
        ReadOnlySpan<byte> input,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(compiled);
        string expectedHash = reverse ? compiled.OutputRow80Sha256 : compiled.SourceRow80Sha256;
        string outputHash = reverse ? compiled.SourceRow80Sha256 : compiled.OutputRow80Sha256;
        if (input.Length != Row80ByteLength || !HashEquals(Hash(input), expectedHash))
            throw new InvalidDataException("The support transaction received the wrong exact preimage.");
        byte[] output = input.ToArray();
        IEnumerable<Id65SupportOwnedRange> ranges = reverse
            ? compiled.OwnedRanges.Reverse()
            : compiled.OwnedRanges;
        foreach (Id65SupportOwnedRange range in ranges)
        {
            byte[] expected = reverse ? range.CopyAfter() : range.CopyBefore();
            byte[] replacement = reverse ? range.CopyBefore() : range.CopyAfter();
            if (!output.AsSpan(range.DataRelativeOffset, range.ByteLength).SequenceEqual(expected))
                throw new InvalidDataException($"Support range `{range.StableId}` lost its exact preimage.");
            replacement.CopyTo(output, range.DataRelativeOffset);
        }
        RequireHash(Hash(output), outputHash, "support transaction output");
        return output;
    }

    internal static IReadOnlyList<Id65ModelComponentRelocation> InvertRelocations(
        IEnumerable<Id65ModelComponentRelocation> relocations) =>
        relocations.Select(item => new Id65ModelComponentRelocation(
            item.StableId,
            item.OutputRelativeOffset,
            item.OutputByteLength,
            item.SourceRelativeOffset,
            item.SourceByteLength,
            item.OutputSha256,
            item.SourceSha256,
            item.ContentsPreserved)).ToArray();

    internal static IReadOnlyList<Id65StableHandleRebase> InvertRebases(
        IEnumerable<Id65StableHandleRebase> rebases) =>
        rebases.Select(item => item with { Source = item.Output, Output = item.Source }).ToArray();

    internal static void ValidateDiffOwnershipForSmoke(
        ReadOnlySpan<byte> source,
        ReadOnlySpan<byte> output,
        IEnumerable<Id65SupportOwnedRange> ranges) =>
        ValidateDiffOwnership(source.ToArray(), output.ToArray(), ranges.ToArray());

    internal static Id65SupportStateReadback ValidateStateForSmoke(
        Id65AuthoringSupportManifest manifest,
        ReadOnlySpan<byte> lockedModel,
        ReadOnlySpan<byte> stateModel,
        ReadOnlySpan<byte> stateRow80) =>
        ValidateState(manifest, lockedModel.ToArray(), stateModel.ToArray(), stateRow80.ToArray());

    internal static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static Id65AuthoringSupportManifest BuildExpectedManifest(Id65SupportProfileKind kind)
    {
        int sectorCount = kind switch
        {
            Id65SupportProfileKind.Empty => 0,
            Id65SupportProfileKind.Core4 => 4,
            Id65SupportProfileKind.Full8 => 8,
            _ => throw new InvalidDataException("Unknown support profile kind.")
        };
        Id65SupportSectorIntent[] sectors = Enumerable.Range(0, sectorCount)
            .Select(BuildSectorIntent)
            .ToArray();
        Id65SupportSeamIntent[] seams = BuildSeams(sectors);
        Id65SupportCollisionCellIntent[] cells = BuildCollisionCellIntents(sectors);
        Id65SupportStateLayout layout = LayoutFor(kind);
        string profileId = kind switch
        {
            Id65SupportProfileKind.Empty => EmptyProfileId,
            Id65SupportProfileKind.Core4 => CoreProfileId,
            Id65SupportProfileKind.Full8 => FullProfileId,
            _ => throw new InvalidDataException("Unknown support profile kind.")
        };
        string treeHash = kind switch
        {
            Id65SupportProfileKind.Empty =>
                UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputTreeSha256,
            Id65SupportProfileKind.Core4 => ExpectedCoreTreeSha256,
            Id65SupportProfileKind.Full8 => ExpectedFullTreeSha256,
            _ => throw new InvalidDataException("Unknown support profile kind.")
        };
        string blockHash = kind switch
        {
            Id65SupportProfileKind.Empty => UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputBlocksSha256,
            Id65SupportProfileKind.Core4 => ExpectedCoreBlocksSha256,
            Id65SupportProfileKind.Full8 => ExpectedFullBlocksSha256,
            _ => throw new InvalidDataException("Unknown support profile kind.")
        };
        string collisionHash = kind switch
        {
            Id65SupportProfileKind.Empty =>
                UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputCollisionComponentSha256,
            Id65SupportProfileKind.Core4 => ExpectedCoreCollisionSha256,
            Id65SupportProfileKind.Full8 => ExpectedFullCollisionSha256,
            _ => throw new InvalidDataException("Unknown support profile kind.")
        };
        string occlusionHash = kind switch
        {
            Id65SupportProfileKind.Empty =>
                "34c97013761d4ec0833745c3be420a0c416b1a7d6912070af5ce76ce00135ccd",
            Id65SupportProfileKind.Core4 => ExpectedCoreOcclusionSha256,
            Id65SupportProfileKind.Full8 => ExpectedFullOcclusionSha256,
            _ => throw new InvalidDataException("Unknown support profile kind.")
        };
        int blocksUsed = kind switch
        {
            Id65SupportProfileKind.Empty => 0x17960,
            Id65SupportProfileKind.Core4 => 0x82,
            Id65SupportProfileKind.Full8 => 0x10E,
            _ => throw new InvalidDataException("Unknown support profile kind.")
        };
        Id65AuthoringSupportManifest manifest = new(
            profileId,
            kind,
            217,
            kind != Id65SupportProfileKind.Empty,
            kind != Id65SupportProfileKind.Empty,
            CollisionTriangleCount,
            0,
            752,
            Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256,
            Id65V2NativeTextureCompositionCompiler.PrivateTextureIntentId,
            sectors,
            seams,
            cells,
            Enumerable.Range(0, sectorCount),
            treeHash,
            blockHash,
            collisionHash,
            blocksUsed,
            occlusionHash,
            ExpectedOcclusionOpaqueTailSha256,
            layout);
        string expectedManifestHash = kind switch
        {
            Id65SupportProfileKind.Empty => ExpectedEmptyManifestSha256,
            Id65SupportProfileKind.Core4 => ExpectedCoreManifestSha256,
            Id65SupportProfileKind.Full8 => ExpectedFullManifestSha256,
            _ => throw new InvalidDataException("Unknown support profile kind.")
        };
        RequireHash(manifest.CanonicalSha256, expectedManifestHash, $"{kind} support manifest");
        return manifest;
    }

    private static Id65SupportSectorIntent BuildSectorIntent(int index)
    {
        (int centerX, int centerY, int originX, int originY) = SectorCoordinates(index);
        Id65AuthoringPoint center = new(centerX, centerY, 512);
        Id65AuthoringPoint origin = new(originX, originY, 512);
        Id65AuthoringPoint e = center;
        Id65AuthoringPoint a = new(originX + 16, originY + 16, 512);
        Id65AuthoringPoint b = new(originX + 496, originY + 16, 512);
        Id65AuthoringPoint c = new(originX + 496, originY + 496, 512);
        Id65AuthoringPoint d = new(originX + 16, originY + 496, 512);
        string sectorId = $"support.sector.{index}";
        Id65SupportVertexIntent[] vertices =
        [
            Vertex("e", e, "00000420"),
            Vertex("a", a, "00400002"),
            Vertex("b", b, "0040003E"),
            Vertex("c", c, "00C0073E"),
            Vertex("d", d, "00C00702")
        ];
        Id65AuthoringPoint[][] collisionPoints =
        [
            [e, b, a],
            [e, c, b],
            [e, d, c],
            [e, a, d]
        ];
        Id65SupportFaceIntent[] faces = Enumerable.Range(0, 4).Select(face =>
        {
            string id = $"{sectorId}.face.{Directions[face]}";
            return new Id65SupportFaceIntent(
                id,
                Directions[face],
                $"{id}:lp",
                $"{id}:hp",
                $"{id}:pair",
                LowFaceHex[face],
                HighFaceHex[face],
                $"{id}:collision",
                CollisionRows[index][face],
                collisionPoints[face],
                115_200,
                -115_200,
                CollisionTouchedCells(collisionPoints[face]));
        }).ToArray();
        return new(
            sectorId,
            index,
            center,
            origin,
            new(originX + 16, originY + 16, 512),
            new(originX + 496, originY + 496, 512),
            340,
            SectorHeaders[index],
            SectorHashes[index],
            vertices,
            faces);

        Id65SupportVertexIntent Vertex(string name, Id65AuthoringPoint point, string encoded) =>
            new(
                $"{sectorId}.vertex.{name}",
                $"{sectorId}.vertex.{name}:lp",
                $"{sectorId}.vertex.{name}:hp",
                point,
                encoded);
    }

    private static (int CenterX, int CenterY, int OriginX, int OriginY) SectorCoordinates(int index) =>
        index switch
        {
            0 => (512, 512, 256, 256),
            1 => (992, 512, 736, 256),
            2 => (512, 992, 256, 736),
            3 => (992, 992, 736, 736),
            4 => (1472, 512, 1216, 256),
            5 => (1952, 512, 1696, 256),
            6 => (1472, 992, 1216, 736),
            7 => (1952, 992, 1696, 736),
            _ => throw new InvalidDataException("Support sector index is outside the exact eight-sector profile.")
        };

    private static Id65SupportSeamIntent[] BuildSeams(IReadOnlyList<Id65SupportSectorIntent> sectors)
    {
        List<Id65SupportSeamIntent> seams = [];
        for (int first = 0; first < sectors.Count; first++)
        for (int second = first + 1; second < sectors.Count; second++)
        {
            Id65SupportSectorIntent a = sectors[first];
            Id65SupportSectorIntent b = sectors[second];
            if (a.BoundsMaximum.X == b.BoundsMinimum.X &&
                Math.Max(a.BoundsMinimum.Y, b.BoundsMinimum.Y) < Math.Min(a.BoundsMaximum.Y, b.BoundsMaximum.Y))
            {
                int y1 = Math.Max(a.BoundsMinimum.Y, b.BoundsMinimum.Y);
                int y2 = Math.Min(a.BoundsMaximum.Y, b.BoundsMaximum.Y);
                seams.Add(new(
                    $"support.seam.{a.SectorIndex}.east-{b.SectorIndex}.west",
                    a.SectorIndex,
                    "east",
                    b.SectorIndex,
                    "west",
                    new(a.BoundsMaximum.X, y1, 512),
                    new(a.BoundsMaximum.X, y2, 512),
                    true,
                    true,
                    true));
            }
            else if (a.BoundsMaximum.Y == b.BoundsMinimum.Y &&
                     Math.Max(a.BoundsMinimum.X, b.BoundsMinimum.X) < Math.Min(a.BoundsMaximum.X, b.BoundsMaximum.X))
            {
                int x1 = Math.Max(a.BoundsMinimum.X, b.BoundsMinimum.X);
                int x2 = Math.Min(a.BoundsMaximum.X, b.BoundsMaximum.X);
                seams.Add(new(
                    $"support.seam.{a.SectorIndex}.north-{b.SectorIndex}.south",
                    a.SectorIndex,
                    "north",
                    b.SectorIndex,
                    "south",
                    new(x1, a.BoundsMaximum.Y, 512),
                    new(x2, a.BoundsMaximum.Y, 512),
                    true,
                    true,
                    true));
            }
        }
        return seams.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
    }

    private static Id65SupportCollisionCellIntent[] BuildCollisionCellIntents(
        IReadOnlyList<Id65SupportSectorIntent> sectors)
    {
        Dictionary<Id65AuthoringCollisionCell, List<int>> values = [];
        foreach (Id65SupportFaceIntent face in sectors.SelectMany(item => item.Faces))
        foreach (Id65AuthoringCollisionCell cell in face.CollisionCells)
        {
            if (!values.TryGetValue(cell, out List<int>? triangles))
            {
                triangles = [];
                values.Add(cell, triangles);
            }
            triangles.Add(face.CollisionTriangleIndex);
        }
        int wordOffset = 0;
        List<Id65SupportCollisionCellIntent> result = [];
        foreach ((Id65AuthoringCollisionCell cell, List<int> unordered) in values
                     .OrderBy(item => item.Key.Z)
                     .ThenBy(item => item.Key.Y)
                     .ThenBy(item => item.Key.X))
        {
            int[] triangles = unordered.OrderDescending().ToArray();
            int treePointer = TreePointerFor(cell);
            result.Add(new(cell, treePointer, wordOffset, triangles));
            wordOffset += triangles.Length;
        }
        int expectedTerminal = sectors.Count switch
        {
            0 => 0,
            4 => 64,
            8 => 134,
            _ => throw new InvalidDataException("Support collision cell graph has an invalid sector count.")
        };
        if (sectors.Count > 0 && wordOffset != expectedTerminal)
            throw new InvalidDataException("Support collision block terminal word changed.");
        return result.ToArray();
    }

    private static int TreePointerFor(Id65AuthoringCollisionCell cell)
    {
        if (cell.Z != 2 || cell.X is < 1 or > 8 || cell.Y is < 1 or > 4)
            throw new InvalidDataException("An authored support collision cell escaped the exact positive grid.");
        int row = cell.Y switch
        {
            1 => 0x1254,
            2 => 0x1294,
            3 => 0x12D6,
            4 => 0x1318,
            _ => throw new InvalidDataException("An authored support collision row escaped the exact grid.")
        };
        return row + ((cell.X - 1) * 2);
    }

    private static IReadOnlyList<Id65AuthoringCollisionCell> CollisionTouchedCells(
        IReadOnlyList<Id65AuthoringPoint> points)
    {
        if (points.Count != 3 || points.Any(point => point.X < 0 || point.Y < 0 || point.Z < 0))
            throw new InvalidDataException("Support collision geometry has an invalid point domain.");
        List<Id65AuthoringCollisionCell> result = [];
        for (int z = points.Min(point => point.Z) >> 8; z <= points.Max(point => point.Z) >> 8; z++)
        for (int y = points.Min(point => point.Y) >> 8; y <= points.Max(point => point.Y) >> 8; y++)
        for (int x = points.Min(point => point.X) >> 8; x <= points.Max(point => point.X) >> 8; x++)
            if (TriangleTouchesBlock(points, x, y, z))
                result.Add(new(x, y, z));
        return result;
    }

    private static bool TriangleTouchesBlock(
        IReadOnlyList<Id65AuthoringPoint> points,
        int x,
        int y,
        int z)
    {
        int x1 = x << 8;
        int x2 = (x + 1) << 8;
        int y1 = y << 8;
        int y2 = (y + 1) << 8;
        int z1 = z << 8;
        int z2 = (z + 1) << 8;
        if (points.Any(point => point.X >= x1 && point.X < x2 && point.Y >= y1 && point.Y < y2 &&
                                point.Z >= z1 && point.Z < z2))
            return true;
        for (int index = 0; index < 3; index++)
        {
            Id65AuthoringPoint current = points[index];
            Id65AuthoringPoint next = points[(index + 1) % 3];
            if (next.X != current.X)
            {
                foreach (int plane in new[] { x1, x2 })
                {
                    if ((current.X >= plane && next.X <= plane) || (current.X <= plane && next.X >= plane))
                    {
                        int testY = current.Y + (((next.Y - current.Y) * (plane - current.X)) /
                                                 (next.X - current.X));
                        int testZ = current.Z + (((next.Z - current.Z) * (plane - current.X)) /
                                                 (next.X - current.X));
                        if (testY >= y1 && testY < y2 && testZ >= z1 && testZ < z2)
                            return true;
                    }
                }
            }
            if (next.Y != current.Y)
            {
                foreach (int plane in new[] { y1, y2 })
                {
                    if ((current.Y >= plane && next.Y <= plane) || (current.Y <= plane && next.Y >= plane))
                    {
                        int testX = current.X + (((next.X - current.X) * (plane - current.Y)) /
                                                 (next.Y - current.Y));
                        int testZ = current.Z + (((next.Z - current.Z) * (plane - current.Y)) /
                                                 (next.Y - current.Y));
                        if (testX >= x1 && testX < x2 && testZ >= z1 && testZ < z2)
                            return true;
                    }
                }
            }
        }
        return false;
    }

    private static Id65SupportStateLayout LayoutFor(Id65SupportProfileKind kind) =>
        kind switch
        {
            Id65SupportProfileKind.Empty => new(
                0, 0x3030,
                0x3030, 0x28518,
                0x2B548, 0x974,
                0x2BEBC, 0x30,
                0x2BEEC, 0x5FAE8,
                0x8B9D4, 0x84E4,
                0x93EB8, 4,
                0x93EBC, 0x80,
                0x93F3C, 0x6F8,
                0x94634, 0x1CC),
            Id65SupportProfileKind.Core4 => new(
                0, 0x3030,
                0x3030, 0x338,
                0x3368, 0x974,
                0x3CDC, 0x30,
                0x3D0C, 0x5FAE8,
                0x637F4, 0x84E4,
                0x6BCD8, 4,
                0x6BCDC, 0x80,
                0x6BD5C, 0x6F8,
                0x6C454, 0x283AC),
            Id65SupportProfileKind.Full8 => new(
                0, 0x3030,
                0x3030, 0x668,
                0x3698, 0x974,
                0x400C, 0x30,
                0x403C, 0x5FAE8,
                0x63B24, 0x84E4,
                0x6C008, 4,
                0x6C00C, 0x80,
                0x6C08C, 0x6F8,
                0x6C784, 0x2807C),
            _ => throw new InvalidDataException("Unknown support profile layout.")
        };

    private static void ValidateLockedSource(Id65AuthoringSupportLockedSource source)
    {
        if (source.Row80ByteLength != Row80ByteLength ||
            !HashEquals(source.Row80Sha256, ExpectedLockedRow80Sha256) ||
            !HashEquals(source.TextureWitnessSha256,
                Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256) ||
            !HashEquals(source.TextureProjectionSha256, ExpectedTextureProjectionSha256) ||
            source.ContainsPath || source.ContainsCompiledAfterimage || source.ContainsSlicePlan ||
            source.WritesFileSystem)
            throw new InvalidDataException("The immutable support compiler source identity changed.");
        byte[] row80 = source.CopyRow80();
        RequireHash(Hash(row80), ExpectedLockedRow80Sha256, "copied locked row 80");
        RequireHash(Hash(row80.AsSpan(ModelOffset, ModelLength)), ExpectedLockedModelSha256,
            "copied locked model");
        IReadOnlyList<Id65V2NativeTexturePackedRow> rows = source.CopyTextureRows();
        IReadOnlyList<Id65V2NativeTexturePagePatch> patches = source.CopyPagePatches();
        string projection = ComputeTextureProjectionCanonical(rows, patches);
        if (!HashEquals(projection, source.TextureProjectionSha256) ||
            !HashEquals(projection, ExpectedTextureProjectionSha256))
            throw new InvalidDataException("The stored T66 projection canonical identity changed.");
        ValidateTextureProjection(rows, patches, source.TextureWitnessSha256, requireCompleteRows: true);
    }

    private static void ValidateTextureWitness(Id65V2NativeTextureWitness witness)
    {
        if (witness.ProfileId != Id65V2NativeTextureCompositionCompiler.WitnessProfileId ||
            witness.DonorManifestId != UnusedLevel65NativeTextureAuthoringContract.DonorManifestId ||
            !HashEquals(witness.DonorCompleteRecordSha256,
                UnusedLevel65NativeTextureAuthoringContract.ExpectedDonorCompleteRecordSha256) ||
            !HashEquals(witness.SourceTexturePagesSha256,
                Id65V2NativeTextureCompositionCompiler.ExpectedSourceTexturePagesSha256) ||
            !HashEquals(witness.OutputTexturePagesSha256,
                Id65V2NativeTextureCompositionCompiler.ExpectedOutputTexturePagesSha256) ||
            !HashEquals(witness.SourceTextureComponentSha256,
                Id65V2NativeTextureCompositionCompiler.ExpectedSourceTextureComponentSha256) ||
            !HashEquals(witness.OutputTextureComponentSha256,
                Id65V2NativeTextureCompositionCompiler.ExpectedOutputTextureComponentSha256) ||
            witness.SourceTextureCount != 66 || witness.OutputTextureCount != 67 ||
            witness.PrivateTextureId != 66 || witness.DonorWadEntry != 70 || witness.DonorTextureId != 12 ||
            witness.MaterialTemplateTextureId != 25 || witness.ContainsFoundationModelBytes ||
            witness.ContainsFoundationFaceBinding || witness.ContainsAppendAfterimage ||
            witness.ContainsPublisherState || !witness.LaterSinglePassCompositeInputAvailable ||
            witness.CrossSliceCompositionAuthorized || witness.AfterimageStackingAuthorized)
            throw new InvalidDataException("The frozen T66 witness identity or safety boundary changed.");
        IReadOnlyList<Id65V2NativeTexturePackedRow> rows = witness.CopyPackedRows();
        IReadOnlyList<Id65V2NativeTexturePagePatch> patches = witness.CopyPagePatches();
        string canonical = ComputeTextureWitnessCanonical(witness, rows, patches);
        if (!HashEquals(canonical, witness.CanonicalSha256) ||
            !HashEquals(canonical, Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256))
            throw new InvalidDataException("The frozen T66 witness canonical identity changed.");
        ValidateTextureProjection(rows, patches, canonical, requireCompleteRows: false);
    }

    private static void ValidateTextureProjection(
        IReadOnlyList<Id65V2NativeTexturePackedRow> rows,
        IReadOnlyList<Id65V2NativeTexturePagePatch> patches,
        string canonicalSha256,
        bool requireCompleteRows)
    {
        IEnumerable<int> expectedIds = requireCompleteRows
            ? Enumerable.Range(0, 67)
            : new[] { 0 }.Concat(Enumerable.Range(2, 65));
        int expectedCount = requireCompleteRows ? 67 : 66;
        if (!HashEquals(canonicalSha256, Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256) ||
            rows.Count != expectedCount ||
            !rows.Select(item => item.TextureId).Order().SequenceEqual(expectedIds) ||
            rows.Select(item => item.TextureId).Distinct().Count() != rows.Count ||
            rows.Count(item => item.Synthetic) != 1 ||
            rows.Any(item => item.TextureId is < 0 or > 66 || item.CopyLow().Length != 16 ||
                item.CopyHigh().Length != 168 || !HashEquals(Hash(item.CopyLow()), item.LowSha256) ||
                !HashEquals(Hash(item.CopyHigh()), item.HighSha256)) ||
            rows.Where(item => !item.Synthetic).Any(item => item.DonorWadEntry != 80 ||
                item.DonorTextureId != item.TextureId))
            throw new InvalidDataException("A frozen T66 row lost exact provenance or byte identity.");
        Id65V2NativeTexturePackedRow synthetic = rows.Single(item => item.Synthetic);
        if (synthetic.TextureId != 66 || synthetic.DonorWadEntry != 70 || synthetic.DonorTextureId != 12 ||
            !HashEquals(synthetic.LowSha256,
                Id65V2NativeTextureCompositionCompiler.ExpectedPrivateLowRowSha256) ||
            !HashEquals(synthetic.HighSha256,
                Id65V2NativeTextureCompositionCompiler.ExpectedPrivateHighRowSha256))
            throw new InvalidDataException("The private T66 row changed.");
        if (requireCompleteRows)
        {
            Id65V2NativeTexturePackedRow lockedT1 = rows.Single(item => item.TextureId == 1);
            if (lockedT1.Synthetic || lockedT1.DonorWadEntry != 80 || lockedT1.DonorTextureId != 1 ||
                !HashEquals(lockedT1.LowSha256, ExpectedLockedT1LowRowSha256) ||
                !HashEquals(lockedT1.HighSha256, ExpectedLockedT1HighRowSha256))
                throw new InvalidDataException("The materialized locked T1 row changed.");
        }
        if (patches.Count != ExpectedTexturePagePatchCount ||
            patches.Sum(item => item.ByteLength) != ExpectedTexturePageChangedByteCount)
            throw new InvalidDataException("The exact T66 page-patch count changed.");
        for (int index = 0; index < patches.Count; index++)
        {
            Id65V2NativeTexturePagePatch patch = patches[index];
            if (patch.ByteLength <= 0 || patch.RelativeOffset < 0 ||
                patch.RelativeOffset + (long)patch.ByteLength > TexturePagesLength ||
                patch.Owner != "terrain-texture-global-repack-data" ||
                !HashEquals(Hash(patch.CopyBefore()), patch.BeforeSha256) ||
                !HashEquals(Hash(patch.CopyAfter()), patch.AfterSha256) ||
                index > 0 && patches[index - 1].RelativeOffset + patches[index - 1].ByteLength >
                    patch.RelativeOffset)
                throw new InvalidDataException("A T66 page patch lost exact ownership or preimage identity.");
        }
    }

    private static IReadOnlyList<Id65V2NativeTexturePackedRow> BuildCompleteTextureProjection(
        ReadOnlySpan<byte> lockedModel,
        IReadOnlyList<Id65V2NativeTexturePackedRow> witnessRows)
    {
        if (lockedModel.Length != ModelLength || ReadInt32(lockedModel, 0) != SourceTextureLength ||
            ReadInt32(lockedModel, 4) != 66 || witnessRows.Count != 66 ||
            witnessRows.Any(item => item.TextureId == 1))
            throw new InvalidDataException("The exact locked T1 completion preimage changed.");
        const int textureId = 1;
        int highStart = 8 + (66 * 16);
        byte[] low = lockedModel.Slice(8 + (textureId * 16), 16).ToArray();
        byte[] high = lockedModel.Slice(highStart + (textureId * 168), 168).ToArray();
        RequireHash(Hash(low), ExpectedLockedT1LowRowSha256, "locked T1 low row");
        RequireHash(Hash(high), ExpectedLockedT1HighRowSha256, "locked T1 high row");
        Id65V2NativeTexturePackedRow lockedT1 = new(
            textureId,
            80,
            textureId,
            synthetic: false,
            low,
            high,
            Hash(low),
            Hash(high));
        Id65V2NativeTexturePackedRow[] complete = witnessRows
            .Append(lockedT1)
            .OrderBy(item => item.TextureId)
            .Select(item => item.DeepCopy())
            .ToArray();
        if (complete.Length != 67 ||
            !complete.Select(item => item.TextureId).SequenceEqual(Enumerable.Range(0, 67)))
            throw new InvalidDataException("The support T66 projection is not complete.");
        return complete;
    }

    internal static string ComputeTextureProjectionCanonical(
        IEnumerable<Id65V2NativeTexturePackedRow> sourceRows,
        IEnumerable<Id65V2NativeTexturePagePatch> sourcePatches)
    {
        Id65V2NativeTexturePackedRow[] rows = sourceRows.OrderBy(item => item.TextureId).ToArray();
        Id65V2NativeTexturePagePatch[] patches = sourcePatches.OrderBy(item => item.RelativeOffset).ToArray();
        StringBuilder text = new();
        text.AppendLine("id65-support-t66-projection-v1");
        foreach (Id65V2NativeTexturePackedRow row in rows)
            text.Append(row.TextureId).Append('|').Append(row.DonorWadEntry).Append('|')
                .Append(row.DonorTextureId).Append('|').Append(row.Synthetic ? '1' : '0').Append('|')
                .Append(row.LowSha256).Append('|').Append(row.HighSha256).Append('|')
                .Append(Hash(row.CopyLow())).Append('|').Append(Hash(row.CopyHigh())).Append('\n');
        foreach (Id65V2NativeTexturePagePatch patch in patches)
            text.Append(patch.RelativeOffset.ToString("X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(patch.ByteLength).Append('|').Append(patch.BeforeSha256).Append('|')
                .Append(patch.AfterSha256).Append('|').Append(Hash(patch.CopyBefore())).Append('|')
                .Append(Hash(patch.CopyAfter())).Append('|').Append(patch.Owner).Append('\n');
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static string ComputeTextureWitnessCanonical(
        Id65V2NativeTextureWitness witness,
        IReadOnlyList<Id65V2NativeTexturePackedRow> rows,
        IReadOnlyList<Id65V2NativeTexturePagePatch> patches)
    {
        StringBuilder text = new();
        text.AppendLine(witness.ProfileId).AppendLine(witness.DonorManifestId)
            .AppendLine(witness.DonorCompleteRecordSha256)
            .AppendLine(witness.SourceTexturePagesSha256).AppendLine(witness.OutputTexturePagesSha256)
            .AppendLine(witness.SourceTextureComponentSha256).AppendLine(witness.OutputTextureComponentSha256);
        foreach (Id65V2NativeTexturePackedRow row in rows.OrderBy(item => item.TextureId))
            text.Append(row.TextureId).Append('|').Append(row.DonorWadEntry).Append('|')
                .Append(row.DonorTextureId).Append('|').Append(row.Synthetic ? '1' : '0').Append('|')
                .Append(row.LowSha256).Append('|').Append(row.HighSha256).Append('\n');
        foreach (Id65V2NativeTexturePagePatch patch in patches)
            text.Append(patch.RelativeOffset.ToString("X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(patch.ByteLength).Append('|').Append(patch.BeforeSha256).Append('|')
                .Append(patch.AfterSha256).Append('|').Append(patch.Owner).Append('\n');
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static void ValidateExactManifest(
        Id65AuthoringSupportManifest manifest,
        Id65SupportProfileKind expectedKind)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        Id65AuthoringSupportManifest expected = BuildExpectedManifest(expectedKind);
        string recomputed = manifest.RecomputeCanonicalJson();
        string recomputedHash = Hash(Encoding.UTF8.GetBytes(recomputed));
        if (manifest.Kind != expectedKind || manifest.ProfileId != expected.ProfileId ||
            manifest.CanonicalJson != recomputed || !HashEquals(manifest.CanonicalSha256, recomputedHash) ||
            manifest.CanonicalJson != expected.CanonicalJson ||
            !HashEquals(manifest.CanonicalSha256, expected.CanonicalSha256))
            throw new InvalidDataException("The support manifest canonical identity or profile changed.");
        if (manifest.LockedSubstrateSectorCount != 217 ||
            manifest.RetiresLockedSector217 != (expectedKind != Id65SupportProfileKind.Empty) ||
            manifest.ClearsInheritedCollisionLookups != (expectedKind != Id65SupportProfileKind.Empty) ||
            manifest.CollisionTriangleCount != CollisionTriangleCount || manifest.CollisionAssignment != 0 ||
            manifest.AuthoredTranslationWorld != 752 || manifest.AuthoredTranslationRaw != 12_032 ||
            manifest.TextureWitnessSha256 !=
                Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256 ||
            manifest.TextureIntentId != Id65V2NativeTextureCompositionCompiler.PrivateTextureIntentId ||
            manifest.Layout != expected.Layout || manifest.CollisionTreeSha256 != expected.CollisionTreeSha256 ||
            manifest.CollisionBlocksSha256 != expected.CollisionBlocksSha256 ||
            manifest.CollisionComponentSha256 != expected.CollisionComponentSha256 ||
            manifest.CollisionBlocksUsedBytes != expected.CollisionBlocksUsedBytes ||
            manifest.OcclusionSha256 != expected.OcclusionSha256 ||
            manifest.OcclusionOpaqueTailSha256 != ExpectedOcclusionOpaqueTailSha256)
            throw new InvalidDataException("The support manifest native ownership facts changed.");
        if (!ManifestGeometryEqual(manifest, expected))
            throw new InvalidDataException("The support manifest geometry, seam, collision, tree, or occlusion intent changed.");

        int sectorCount = expectedKind switch
        {
            Id65SupportProfileKind.Empty => 0,
            Id65SupportProfileKind.Core4 => 4,
            Id65SupportProfileKind.Full8 => 8,
            _ => -1
        };
        int seamCount = expectedKind switch
        {
            Id65SupportProfileKind.Empty => 0,
            Id65SupportProfileKind.Core4 => 4,
            Id65SupportProfileKind.Full8 => 10,
            _ => -1
        };
        if (manifest.Sectors.Count != sectorCount || manifest.Seams.Count != seamCount ||
            manifest.CollisionCells.Count != sectorCount * 4 ||
            !manifest.OcclusionGroup0Sectors.SequenceEqual(Enumerable.Range(0, sectorCount)) ||
            manifest.Sectors.Select(item => item.SectorIndex).Distinct().Count() != sectorCount ||
            manifest.Seams.Any(item => !item.LowDetailExact || !item.HighDetailExact || !item.TjunctionFree))
            throw new InvalidDataException("The support sector/seam/occlusion cardinality changed.");
    }

    private static bool ManifestGeometryEqual(
        Id65AuthoringSupportManifest left,
        Id65AuthoringSupportManifest right)
    {
        if (left.Sectors.Count != right.Sectors.Count || left.Seams.Count != right.Seams.Count ||
            left.CollisionCells.Count != right.CollisionCells.Count ||
            !left.OcclusionGroup0Sectors.SequenceEqual(right.OcclusionGroup0Sectors))
            return false;
        for (int index = 0; index < left.Sectors.Count; index++)
        {
            Id65SupportSectorIntent a = left.Sectors[index];
            Id65SupportSectorIntent b = right.Sectors[index];
            if (a.Id != b.Id || a.SectorIndex != b.SectorIndex || a.Center != b.Center ||
                a.Origin != b.Origin || a.BoundsMinimum != b.BoundsMinimum ||
                a.BoundsMaximum != b.BoundsMaximum || a.Radius != b.Radius ||
                a.ExactHeaderHex != b.ExactHeaderHex || a.ExactSectorSha256 != b.ExactSectorSha256 ||
                !a.Vertices.SequenceEqual(b.Vertices) || a.Faces.Count != b.Faces.Count)
                return false;
            for (int face = 0; face < a.Faces.Count; face++)
            {
                Id65SupportFaceIntent x = a.Faces[face];
                Id65SupportFaceIntent y = b.Faces[face];
                if (x.Id != y.Id || x.Direction != y.Direction ||
                    x.LowDetailHandle != y.LowDetailHandle || x.HighDetailHandle != y.HighDetailHandle ||
                    x.HpLpPairId != y.HpLpPairId || x.LowDetailHex != y.LowDetailHex ||
                    x.HighDetailHex != y.HighDetailHex || x.CollisionHandle != y.CollisionHandle ||
                    x.CollisionTriangleIndex != y.CollisionTriangleIndex ||
                    !x.CollisionPoints.SequenceEqual(y.CollisionPoints) ||
                    x.RenderNormalZ != y.RenderNormalZ || x.CollisionNormalZ != y.CollisionNormalZ ||
                    !x.CollisionCells.SequenceEqual(y.CollisionCells))
                    return false;
            }
        }
        if (!left.Seams.SequenceEqual(right.Seams))
            return false;
        for (int index = 0; index < left.CollisionCells.Count; index++)
        {
            Id65SupportCollisionCellIntent a = left.CollisionCells[index];
            Id65SupportCollisionCellIntent b = right.CollisionCells[index];
            if (a.Cell != b.Cell || a.TreePointerByteOffset != b.TreePointerByteOffset ||
                a.BlockWordOffset != b.BlockWordOffset || !a.TriangleIndexes.SequenceEqual(b.TriangleIndexes))
                return false;
        }
        return true;
    }

    private static Id65SupportTransitionKind RequireAllowedTransition(
        Id65SupportProfileKind source,
        Id65SupportProfileKind target) =>
        (source, target) switch
        {
            (Id65SupportProfileKind.Empty, Id65SupportProfileKind.Core4) =>
                Id65SupportTransitionKind.AddCoreSupport,
            (Id65SupportProfileKind.Core4, Id65SupportProfileKind.Full8) =>
                Id65SupportTransitionKind.AddEnemyBay,
            (Id65SupportProfileKind.Full8, Id65SupportProfileKind.Core4) =>
                Id65SupportTransitionKind.RemoveEnemyBay,
            (Id65SupportProfileKind.Core4, Id65SupportProfileKind.Empty) =>
                Id65SupportTransitionKind.RemoveCore,
            _ => throw new InvalidDataException("Only Empty<->Core4<->Full8 support transitions are allowed.")
        };

    private static void ValidateLimits(
        Id65AuthoringSupportCompilerLimits limits,
        Id65AuthoringSupportManifest source,
        Id65AuthoringSupportManifest target)
    {
        int sectors = Math.Max(source.Kind == Id65SupportProfileKind.Empty ? 217 : source.Sectors.Count,
            target.Kind == Id65SupportProfileKind.Empty ? 217 : target.Sectors.Count);
        int used = Math.Max(source.Layout.UsedModelBytes, target.Layout.UsedModelBytes);
        if (limits.ModelByteCapacity < ModelLength || limits.MaximumOutputUsedModelBytes < used)
            throw new InvalidDataException("Support model/tail capacity is insufficient.");
        if (limits.MaximumSceneSectorCount < sectors)
            throw new InvalidDataException("Support scene-sector capacity is insufficient.");
        if (limits.MaximumSectorByteLength < 0xC8 || limits.MaximumSectorFaceCount < 4)
            throw new InvalidDataException("Support sector byte/face capacity is insufficient.");
        if (limits.MaximumCollisionTriangleCount < CollisionTriangleCount)
            throw new InvalidDataException("Support collision triangle capacity is insufficient.");
        if (limits.CollisionTreeCapacityBytes < CollisionTreeLength ||
            limits.CollisionBlockCapacityBytes < CollisionBlocksLength)
            throw new InvalidDataException("Support collision tree/block capacity is insufficient.");
        if (limits.MaximumNativeTextureId is < 66 or > 127)
            throw new InvalidDataException("Support seven-bit T66 texture capacity is invalid.");
    }

    private static byte[] BuildStateModel(
        byte[] lockedModel,
        IReadOnlyList<Id65V2NativeTexturePackedRow> textureRows,
        Id65AuthoringSupportManifest manifest)
    {
        if (lockedModel.Length != ModelLength)
            throw new InvalidDataException("The locked support model length changed.");
        RequireHash(Hash(lockedModel), ExpectedLockedModelSha256, "support model locked source");
        byte[] texture = BuildTextureComponent(lockedModel, textureRows);
        byte[] remoteEnvironment = BuildRemoteEnvironment(
            Slice(lockedModel, SourceEnvironmentOffset, SourceEnvironmentLength));
        byte[] remoteOcclusion = BuildRemoteOcclusion(
            Slice(lockedModel, SourceOcclusionOffset, OcclusionLength));
        byte[] remoteCollision = BuildRemoteCollision(
            Slice(lockedModel, SourceCollisionOffset, CollisionLength));
        const int repairedTriangleRelative = CollisionTrianglesOffset + (13_995 * 12);
        byte[] failed = Convert.FromHexString("100138381001007000020000");
        if (!remoteCollision.AsSpan(repairedTriangleRelative, 12).SequenceEqual(failed))
            throw new InvalidDataException("The exact post-v2 collision winding preimage changed.");
        Convert.FromHexString("10011C701001380000020000")
            .CopyTo(remoteCollision, repairedTriangleRelative);
        RequireHash(Hash(remoteCollision),
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputCollisionComponentSha256,
            "negative-winding v2 collision");

        byte[] environment;
        byte[] occlusion;
        byte[] collision;
        if (manifest.Kind == Id65SupportProfileKind.Empty)
        {
            environment = remoteEnvironment;
            occlusion = remoteOcclusion;
            collision = remoteCollision;
        }
        else
        {
            environment = BuildSupportEnvironment(manifest);
            occlusion = BuildSupportOcclusion(
                Slice(lockedModel, SourceOcclusionOffset, OcclusionLength), manifest);
            collision = BuildSupportCollision(remoteCollision, manifest);
        }

        byte[] output = new byte[ModelLength];
        int cursor = 0;
        void Append(byte[] bytes)
        {
            bytes.CopyTo(output, cursor);
            cursor += bytes.Length;
        }
        Append(texture);
        Append(environment);
        Append(occlusion);
        Append(Slice(lockedModel, SourceSpecialOffset, SpecialLength));
        Append(collision);
        Append(Slice(lockedModel, SourceCycloramaOffset, CycloramaLength));
        Append(Slice(lockedModel, SourcePortalOffset, PortalLength));
        Append(Slice(lockedModel, SourceParticlesOffset, ParticlesLength));
        Append(Slice(lockedModel, SourceSoundOffset, SoundLength));
        if (cursor != manifest.Layout.UsedModelBytes ||
            output.AsSpan(cursor).IndexOfAnyExcept((byte)0) >= 0 ||
            output.Length - cursor != manifest.Layout.ZeroTailBytes)
            throw new InvalidDataException("The support model used/tail boundary changed.");

        if (manifest.Kind == Id65SupportProfileKind.Empty)
        {
            int textureWordOffset = 0x2B540;
            uint textureWord = ReadUInt32(output, textureWordOffset);
            if ((textureWord & 0x7F) != 25)
                throw new InvalidDataException("The empty v2 face lost its locked T25 binding preimage.");
            WriteUInt32(output, textureWordOffset, (textureWord & 0xFFFFFF80u) | 66u);
            RequireHash(Hash(output), ExpectedEmptyModelSha256, "empty post-v2 T66 model");
        }
        else
        {
            string expected = manifest.Kind == Id65SupportProfileKind.Core4
                ? ExpectedCoreModelSha256
                : ExpectedFullModelSha256;
            RequireHash(Hash(output), expected, $"{manifest.Kind} support model");
        }
        return output;
    }

    private static byte[] BuildStateRow80(
        byte[] lockedRow80,
        byte[] stateModel,
        IReadOnlyList<Id65V2NativeTexturePagePatch> pagePatches)
    {
        if (lockedRow80.Length != Row80ByteLength || stateModel.Length != ModelLength)
            throw new InvalidDataException("A support state row/model has the wrong length.");
        byte[] lockedPlacement = Convert.FromHexString("29E901009A880100");
        if (!lockedRow80.AsSpan(LandingOffset, 8).SequenceEqual(lockedPlacement) ||
            !lockedRow80.AsSpan(T92XyOffset, 8).SequenceEqual(lockedPlacement))
            throw new InvalidDataException("The exact locked landing/T92 XY preimages changed.");
        byte[] output = lockedRow80.ToArray();
        byte[] pages = output.AsSpan(TexturePagesOffset, TexturePagesLength).ToArray();
        RequireHash(Hash(pages), Id65V2NativeTextureCompositionCompiler.ExpectedSourceTexturePagesSha256,
            "support T66 page preimage");
        foreach (Id65V2NativeTexturePagePatch patch in pagePatches)
        {
            byte[] before = patch.CopyBefore();
            byte[] after = patch.CopyAfter();
            if (!pages.AsSpan(patch.RelativeOffset, patch.ByteLength).SequenceEqual(before))
                throw new InvalidDataException("A support T66 page patch lost its exact preimage.");
            after.CopyTo(pages, patch.RelativeOffset);
        }
        RequireHash(Hash(pages), Id65V2NativeTextureCompositionCompiler.ExpectedOutputTexturePagesSha256,
            "support T66 pages");
        pages.CopyTo(output, TexturePagesOffset);
        stateModel.CopyTo(output, ModelOffset);
        Convert.FromHexString("091800000A180000").CopyTo(output, LandingOffset);
        WriteInt32(output, T92XyOffset, 6_153);
        WriteInt32(output, T92XyOffset + 4, 6_154);
        if (!output.AsSpan(LandingOffset, 8).SequenceEqual(Convert.FromHexString("091800000A180000")) ||
            ReadInt32(output, T92XyOffset) != 6_153 || ReadInt32(output, T92XyOffset + 4) != 6_154)
            throw new InvalidDataException("The post-v2 landing/T92 XY readback changed.");
        return output;
    }

    private static Id65SupportDesiredLeaf[] BuildCore4DesiredLeaves(byte[] lockedRow80)
    {
        byte[] lockedPlacement = Convert.FromHexString("29E901009A880100");
        byte[] desiredPlacement = Convert.FromHexString("091800000A180000");
        if (lockedRow80.Length != Row80ByteLength ||
            !lockedRow80.AsSpan(LandingOffset, 8).SequenceEqual(lockedPlacement) ||
            !lockedRow80.AsSpan(T92XyOffset, 8).SequenceEqual(lockedPlacement))
            throw new InvalidDataException("The Core4 desired placement preimages changed.");
        return
        [
            new Id65SupportDesiredLeaf(
                "spawn.remote-pad",
                new Id65LogicalAddress("landing-record", 0, 0),
                LandingOffset,
                lockedPlacement,
                desiredPlacement),
            new Id65SupportDesiredLeaf(
                "moby.player-anchor.t92",
                new Id65LogicalAddress("moby-row", 92, 0),
                T92XyOffset,
                lockedPlacement,
                desiredPlacement)
        ];
    }

    private static string HashTexturePageDescriptors(
        IEnumerable<Id65V2NativeTexturePagePatch> descriptors)
    {
        StringBuilder text = new();
        foreach (Id65V2NativeTexturePagePatch item in descriptors.OrderBy(item => item.RelativeOffset))
            text.Append(item.RelativeOffset.ToString("X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(item.ByteLength).Append('|').Append(item.BeforeSha256).Append('|')
                .Append(item.AfterSha256).Append('|').Append(Hash(item.CopyBefore())).Append('|')
                .Append(Hash(item.CopyAfter())).Append('|').Append(item.Owner).Append('\n');
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static string HashDesiredLeaves(IEnumerable<Id65SupportDesiredLeaf> leaves)
    {
        StringBuilder text = new();
        foreach (Id65SupportDesiredLeaf item in leaves.OrderBy(item => item.DataRelativeOffset))
            text.Append(item.StableId).Append('|').Append(item.LogicalAddress.Space).Append('|')
                .Append(item.LogicalAddress.Primary).Append('|').Append(item.LogicalAddress.Secondary)
                .Append('|').Append(item.DataRelativeOffset.ToString("X8", CultureInfo.InvariantCulture))
                .Append('|').Append(item.ByteLength).Append('|').Append(item.LockedPreimageSha256)
                .Append('|').Append(item.DesiredSha256).Append('|')
                .Append(Hash(item.CopyLockedPreimage())).Append('|')
                .Append(Hash(item.CopyDesiredBytes())).Append('\n');
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static string HashDesiredReadback(Id65SupportStateReadback value)
    {
        string canonical = string.Join("|", new[]
        {
            value.Kind.ToString(),
            value.Row80Sha256,
            value.ModelSha256,
            value.EnvironmentSha256,
            value.CollisionSha256,
            value.CollisionTreeSha256,
            value.CollisionBlocksSha256,
            value.OcclusionSha256,
            value.SectorCount.ToString(CultureInfo.InvariantCulture),
            value.CollisionTriangleCount.ToString(CultureInfo.InvariantCulture),
            value.ActiveCollisionCellCount.ToString(CultureInfo.InvariantCulture),
            value.CollisionBlocksUsedBytes.ToString(CultureInfo.InvariantCulture),
            value.UsedModelBytes.ToString(CultureInfo.InvariantCulture),
            value.ZeroTailBytes.ToString(CultureInfo.InvariantCulture),
            value.InheritedActiveLeavesCleared ? "1" : "0",
            value.ExactHpLpPairing ? "1" : "0",
            value.ExactSeams ? "1" : "0",
            value.ProtectedComponentsPreserved ? "1" : "0",
            value.RuntimeAccepted ? "1" : "0"
        });
        return Hash(Encoding.UTF8.GetBytes(canonical));
    }

    private static string HashDesiredCapacity(Id65SupportCapacityReadback value)
    {
        string canonical = string.Join("|", new[]
        {
            value.ModelByteCapacity.ToString(CultureInfo.InvariantCulture),
            value.SourceUsedModelBytes.ToString(CultureInfo.InvariantCulture),
            value.OutputUsedModelBytes.ToString(CultureInfo.InvariantCulture),
            value.SourceZeroTailBytes.ToString(CultureInfo.InvariantCulture),
            value.OutputZeroTailBytes.ToString(CultureInfo.InvariantCulture),
            value.SourceSectorCount.ToString(CultureInfo.InvariantCulture),
            value.OutputSectorCount.ToString(CultureInfo.InvariantCulture),
            value.SourceCollisionBlocksUsedBytes.ToString(CultureInfo.InvariantCulture),
            value.OutputCollisionBlocksUsedBytes.ToString(CultureInfo.InvariantCulture),
            value.CollisionTriangleCount.ToString(CultureInfo.InvariantCulture),
            value.CollisionTreeCapacityBytes.ToString(CultureInfo.InvariantCulture),
            value.CollisionBlockCapacityBytes.ToString(CultureInfo.InvariantCulture),
            value.TextureCount.ToString(CultureInfo.InvariantCulture),
            value.HighestTextureId.ToString(CultureInfo.InvariantCulture),
            value.NativeObservedMinimumSectorCount.ToString(CultureInfo.InvariantCulture)
        });
        return Hash(Encoding.UTF8.GetBytes(canonical));
    }

    private static string HashCore4DesiredProjection(
        Id65AuthoringSupportLockedSource lockedSource,
        Id65AuthoringSupportManifest coreManifest,
        byte[] coreModel,
        IReadOnlyList<Id65V2NativeTexturePagePatch> pageDescriptors,
        string pageMapSha256,
        IReadOnlyList<Id65SupportDesiredLeaf> desiredLeaves,
        string leafMapSha256,
        string readbackSha256,
        string capacitySha256) =>
        HashCore4DesiredProjection(
            lockedSource.Row80Sha256,
            lockedSource.TextureWitnessSha256,
            lockedSource.TextureProjectionSha256,
            coreManifest.CanonicalSha256,
            coreModel.Length,
            Hash(coreModel),
            ExpectedCoreRow80Sha256,
            pageDescriptors.Count,
            pageDescriptors.Sum(item => item.ByteLength),
            pageMapSha256,
            desiredLeaves.Count,
            leafMapSha256,
            readbackSha256,
            capacitySha256);

    private static string HashCore4DesiredProjection(Id65SupportDesiredStateProjection value) =>
        HashCore4DesiredProjection(
            value.LockedRow80Sha256,
            value.TextureWitnessSha256,
            value.TextureProjectionSha256,
            value.CoreManifestSha256,
            value.CoreModelByteLength,
            value.CoreModelSha256,
            value.CoreRow80CrossCheckSha256,
            value.TexturePageDescriptorCount,
            value.TexturePageChangedByteCount,
            value.TexturePageDescriptorMapSha256,
            value.DesiredLeaves.Count,
            value.DesiredLeafMapSha256,
            value.ReadbackSha256,
            value.CapacitySha256);

    private static string HashCore4DesiredProjection(
        string lockedRow80Sha256,
        string textureWitnessSha256,
        string textureProjectionSha256,
        string coreManifestSha256,
        int coreModelByteLength,
        string coreModelSha256,
        string coreRow80CrossCheckSha256,
        int pageDescriptorCount,
        int pageChangedByteCount,
        string pageMapSha256,
        int desiredLeafCount,
        string leafMapSha256,
        string readbackSha256,
        string capacitySha256)
    {
        StringBuilder text = new();
        text.AppendLine(DesiredProjectionProfileId)
            .AppendLine(lockedRow80Sha256)
            .AppendLine(textureWitnessSha256)
            .AppendLine(textureProjectionSha256)
            .AppendLine(coreManifestSha256)
            .Append(coreModelByteLength).Append('|').Append(coreModelSha256).Append('\n')
            .AppendLine(coreRow80CrossCheckSha256)
            .Append(pageDescriptorCount).Append('|').Append(pageChangedByteCount).Append('|')
            .Append(pageMapSha256).Append('\n')
            .Append(desiredLeafCount).Append('|').Append(leafMapSha256).Append('\n')
            .AppendLine(readbackSha256)
            .AppendLine(capacitySha256)
            .AppendLine("direct-locked=1|full-row=0|compiled=0|stacking=0|plan=0|path=0")
            .AppendLine("fs=0|bin=0|cue=0|writer=0|app=0|create-bin=0|normal-create-bin=0")
            .AppendLine("runtime-candidate=0|runtime=0|release=0|promotion=0|publishable=0")
            .AppendLine("executable-mutation-excluded=1|full8-excluded=1");
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static void ValidateCore4DesiredProjection(Id65SupportDesiredStateProjection value)
    {
        byte[] model = value.CopyCoreModel();
        IReadOnlyList<Id65V2NativeTexturePagePatch> pages = value.CopyTexturePageDescriptors();
        IReadOnlyList<Id65SupportDesiredLeaf> leaves = value.CopyDesiredLeaves();
        if (value.ProfileId != DesiredProjectionProfileId || value.Kind != Id65SupportProfileKind.Core4 ||
            !HashEquals(value.LockedRow80Sha256, ExpectedLockedRow80Sha256) ||
            !HashEquals(value.TextureWitnessSha256,
                Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256) ||
            !HashEquals(value.TextureProjectionSha256, ExpectedTextureProjectionSha256) ||
            !HashEquals(value.CoreManifestSha256, ExpectedCoreManifestSha256) ||
            model.Length != ModelLength || !HashEquals(Hash(model), ExpectedCoreModelSha256) ||
            !HashEquals(value.CoreModelSha256, ExpectedCoreModelSha256) ||
            !HashEquals(value.CoreRow80CrossCheckSha256, ExpectedCoreRow80Sha256) ||
            pages.Count != ExpectedTexturePagePatchCount ||
            pages.Sum(item => item.ByteLength) != ExpectedTexturePageChangedByteCount ||
            value.TexturePageDescriptorCount != ExpectedTexturePagePatchCount ||
            value.TexturePageChangedByteCount != ExpectedTexturePageChangedByteCount ||
            !HashEquals(HashTexturePageDescriptors(pages), value.TexturePageDescriptorMapSha256) ||
            !HashEquals(HashDesiredLeaves(leaves), value.DesiredLeafMapSha256) ||
            !HashEquals(HashDesiredReadback(value.Readback), value.ReadbackSha256) ||
            !HashEquals(HashDesiredCapacity(value.Capacity), value.CapacitySha256) ||
            !HashEquals(HashCore4DesiredProjection(value), value.CanonicalSha256) ||
            !value.DirectLockedSourceDerived || value.ContainsFullAuthoredRow80 ||
            value.ContainsCompiledAfterimage || value.AfterimageStackingAuthorized ||
            value.ContainsSlicePlan || value.ContainsPath || value.WritesFileSystem ||
            value.WritesDiscImage || value.WritesCue || value.PublisherCalled ||
            value.WriterAuthorized || value.AppIntegrated || value.CreateBinEnabled ||
            value.NormalCreateBinEnabled || value.RuntimeCandidateAuthorized || value.RuntimeAccepted ||
            value.ReleaseIntegrated || value.PromotionAuthorized || value.Publishable ||
            !value.ExecutableMutationExcluded || !value.Full8Excluded)
            throw new InvalidDataException("The direct Core4 desired-state projection identity changed.");

        for (int index = 0; index < pages.Count; index++)
        {
            Id65V2NativeTexturePagePatch page = pages[index];
            if (page.ByteLength <= 0 || page.RelativeOffset < 0 ||
                page.RelativeOffset + (long)page.ByteLength > TexturePagesLength ||
                page.Owner != "terrain-texture-global-repack-data" ||
                !HashEquals(Hash(page.CopyBefore()), page.BeforeSha256) ||
                !HashEquals(Hash(page.CopyAfter()), page.AfterSha256) ||
                index > 0 && pages[index - 1].RelativeOffset + pages[index - 1].ByteLength >
                    page.RelativeOffset)
                throw new InvalidDataException("A Core4 desired T66 page descriptor changed.");
        }

        if (leaves.Count != 2 || leaves.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count() != 2 ||
            leaves.Any(item => string.IsNullOrWhiteSpace(item.StableId) || item.DataRelativeOffset < 0 ||
                item.DataRelativeOffset + (long)item.ByteLength > Row80ByteLength) ||
            leaves.OrderBy(item => item.DataRelativeOffset).Zip(
                leaves.OrderBy(item => item.DataRelativeOffset).Skip(1),
                (left, right) => left.DataRelativeOffset + left.ByteLength > right.DataRelativeOffset).Any(overlap => overlap))
            throw new InvalidDataException("The Core4 desired leaf ownership map changed.");

        byte[] lockedPlacement = Convert.FromHexString("29E901009A880100");
        byte[] desiredPlacement = Convert.FromHexString("091800000A180000");
        (string Id, Id65LogicalAddress Address, int Offset)[] expectedLeaves =
        [
            ("spawn.remote-pad", new Id65LogicalAddress("landing-record", 0, 0), LandingOffset),
            ("moby.player-anchor.t92", new Id65LogicalAddress("moby-row", 92, 0), T92XyOffset)
        ];
        for (int index = 0; index < expectedLeaves.Length; index++)
        {
            Id65SupportDesiredLeaf leaf = leaves[index];
            (string id, Id65LogicalAddress address, int offset) = expectedLeaves[index];
            if (leaf.StableId != id || leaf.LogicalAddress != address || leaf.DataRelativeOffset != offset ||
                leaf.ByteLength != 8 || !leaf.CopyLockedPreimage().SequenceEqual(lockedPlacement) ||
                !leaf.CopyDesiredBytes().SequenceEqual(desiredPlacement) ||
                !HashEquals(Hash(leaf.CopyLockedPreimage()), leaf.LockedPreimageSha256) ||
                !HashEquals(Hash(leaf.CopyDesiredBytes()), leaf.DesiredSha256))
                throw new InvalidDataException($"The Core4 desired leaf `{id}` changed.");
        }

        Id65AuthoringSupportManifest empty = BuildExpectedManifest(Id65SupportProfileKind.Empty);
        Id65AuthoringSupportManifest core = BuildExpectedManifest(Id65SupportProfileKind.Core4);
        if (value.Capacity != BuildCapacity(empty, core) ||
            value.Readback.Kind != Id65SupportProfileKind.Core4 ||
            !HashEquals(value.Readback.Row80Sha256, ExpectedCoreRow80Sha256) ||
            !HashEquals(value.Readback.ModelSha256, ExpectedCoreModelSha256) ||
            value.Readback.SectorCount != 4 || value.Readback.CollisionTriangleCount != CollisionTriangleCount ||
            value.Readback.ActiveCollisionCellCount != 16 ||
            value.Readback.CollisionBlocksUsedBytes != 0x82 || value.Readback.RuntimeAccepted)
            throw new InvalidDataException("The Core4 desired readback or capacity changed.");

        RequireDesiredProjectionPin(value.TexturePageDescriptorMapSha256,
            ExpectedCoreDesiredPageDescriptorMapSha256, "Core4 desired T66 page descriptor map");
        RequireDesiredProjectionPin(value.DesiredLeafMapSha256,
            ExpectedCoreDesiredLeafMapSha256, "Core4 desired leaf map");
        RequireDesiredProjectionPin(value.ReadbackSha256,
            ExpectedCoreDesiredReadbackSha256, "Core4 desired readback");
        RequireDesiredProjectionPin(value.CapacitySha256,
            ExpectedCoreDesiredCapacitySha256, "Core4 desired capacity");
        RequireDesiredProjectionPin(value.CanonicalSha256,
            ExpectedCoreDesiredProjectionSha256, "Core4 desired projection canonical");
    }

    private static void RequireDesiredProjectionPin(string actual, string expected, string label)
        => RequireHash(actual, expected, label);

    private static byte[] BuildTextureComponent(
        byte[] lockedModel,
        IReadOnlyList<Id65V2NativeTexturePackedRow> textureRows)
    {
        ReadOnlySpan<byte> source = lockedModel.AsSpan(0, SourceTextureLength);
        if (ReadInt32(source, 0) != SourceTextureLength || ReadInt32(source, 4) != 66)
            throw new InvalidDataException("The locked 66-row texture component changed.");
        Dictionary<int, Id65V2NativeTexturePackedRow> packed =
            textureRows.ToDictionary(item => item.TextureId);
        byte[] output = new byte[TextureLength];
        WriteInt32(output, 0, output.Length);
        WriteInt32(output, 4, 67);
        int outputHighStart = 8 + (67 * 16);
        for (int textureId = 0; textureId < 66; textureId++)
        {
            if (!packed.TryGetValue(textureId, out Id65V2NativeTexturePackedRow? row))
                throw new InvalidDataException($"The complete T66 projection omitted native row T{textureId}.");
            byte[] low = row.CopyLow();
            byte[] high = row.CopyHigh();
            low.CopyTo(output, 8 + (textureId * 16));
            high.CopyTo(output, outputHighStart + (textureId * 168));
        }
        Id65V2NativeTexturePackedRow synthetic = textureRows.Single(item => item.Synthetic);
        synthetic.CopyLow().CopyTo(output, 8 + (66 * 16));
        synthetic.CopyHigh().CopyTo(output, outputHighStart + (66 * 168));
        RequireHash(Hash(output), Id65V2NativeTextureCompositionCompiler.ExpectedOutputTextureComponentSha256,
            "support T66 texture component");
        return output;
    }

    private static byte[] BuildRemoteEnvironment(byte[] source)
    {
        if (source.Length != SourceEnvironmentLength || ReadInt32(source, 0) != source.Length ||
            ReadInt32(source, 4) != 216)
            throw new InvalidDataException("The locked 216-sector environment changed.");
        int sourceTableEnd = 8 + (216 * 4);
        int outputTableEnd = 8 + (217 * 4);
        int payloadLength = source.Length - sourceTableEnd;
        byte[] sector = BuildRemoteSector();
        byte[] output = new byte[0x28518];
        WriteInt32(output, 0, output.Length);
        WriteInt32(output, 4, 217);
        for (int index = 0; index < 216; index++)
            WriteInt32(output, 8 + (index * 4), ReadInt32(source, 8 + (index * 4)) + 4);
        WriteInt32(output, 8 + (216 * 4), source.Length);
        source.AsSpan(sourceTableEnd, payloadLength).CopyTo(output.AsSpan(outputTableEnd));
        int sectorOffset = outputTableEnd + payloadLength;
        sector.CopyTo(output, sectorOffset);
        if (sectorOffset != source.Length + 4 || sectorOffset + sector.Length != output.Length)
            throw new InvalidDataException("The remote sector append boundary changed.");
        return output;
    }

    private static byte[] BuildRemoteSector()
    {
        byte[][] parts =
        [
            Convert.FromHexString("80018001C000000200010001000000020303010003030109FFFFFFFF"),
            Convert.FromHexString("004000020040001E00C00310"),
            Convert.FromHexString("2D39300030552C0037622C00"),
            Convert.FromHexString("0082100000821000"),
            Convert.FromHexString("004000020040001E00C00310"),
            Convert.FromHexString("99756B00775D6600896A6A00775D6600806268007A5F6800"),
            Convert.FromHexString("000001020101000219024001080A1000")
        ];
        byte[] sector = parts.SelectMany(item => item).ToArray();
        if (sector.Length != 0x70)
            throw new InvalidDataException("The remote sector byte length changed.");
        RequireHash(Hash(sector),
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedNewSectorSha256,
            "remote sector 216");
        return sector;
    }

    private static byte[] BuildSupportEnvironment(Id65AuthoringSupportManifest manifest)
    {
        int count = manifest.Sectors.Count;
        if (count is not (4 or 8))
            throw new InvalidDataException("Support environment requires exactly four or eight sectors.");
        byte[] output = new byte[8 + (count * 4) + (count * 0xC8)];
        WriteInt32(output, 0, output.Length);
        WriteInt32(output, 4, count);
        int payload = 8 + (count * 4);
        for (int index = 0; index < count; index++)
        {
            WriteInt32(output, 8 + (index * 4), (payload - 4) + (index * 0xC8));
            byte[] sector = Convert.FromHexString(
                manifest.Sectors[index].ExactHeaderHex + CommonSectorPayloadHex);
            if (sector.Length != 0xC8)
                throw new InvalidDataException("An authored support sector has the wrong byte length.");
            RequireHash(Hash(sector), manifest.Sectors[index].ExactSectorSha256,
                $"support sector {index}");
            sector.CopyTo(output, payload + (index * 0xC8));
        }
        string expected = count == 4 ? ExpectedCoreEnvironmentSha256 : ExpectedFullEnvironmentSha256;
        RequireHash(Hash(output), expected, $"{count}-sector support environment");
        return output;
    }

    private static byte[] BuildRemoteOcclusion(byte[] source)
    {
        if (source.Length != OcclusionLength || ReadInt32(source, 0) != source.Length ||
            ReadInt32(source, 4) != 0x71C || ReadInt32(source, 8) != 16)
            throw new InvalidDataException("The locked occlusion directory changed.");
        IReadOnlyList<int>[] sourceGroups = ParseOcclusionGroups(source, 216, out int[] pointers);
        if (pointers[0] != 0x48 || sourceGroups[0].Count != 124 || sourceGroups[0][8] != 213 ||
            source[0xC8] != 0xFF || source[0x71E] != 0xFF || source[0x71F] != 0xFF)
            throw new InvalidDataException("The locked remote occlusion insertion substrate changed.");
        byte[] output = source.ToArray();
        int environmentEnd = 4 + 0x71C;
        Array.Copy(source, 0xC8, output, 0xC9, environmentEnd - 2 - 0xC8);
        output[0xC8] = 216;
        for (int group = 1; group < pointers.Length; group++)
            WriteInt32(output, 12 + (group * 4), pointers[group] + 1);
        output[environmentEnd - 1] = source[environmentEnd - 1];
        RequireHash(Hash(output), "34c97013761d4ec0833745c3be420a0c416b1a7d6912070af5ce76ce00135ccd",
            "remote occlusion");
        return output;
    }

    private static byte[] BuildSupportOcclusion(
        byte[] lockedOcclusion,
        Id65AuthoringSupportManifest manifest)
    {
        if (lockedOcclusion.Length != OcclusionLength || ReadInt32(lockedOcclusion, 4) != 0x71C)
            throw new InvalidDataException("The locked occlusion component changed.");
        byte[] tail = lockedOcclusion.AsSpan(0x720, 0x254).ToArray();
        RequireHash(Hash(tail), ExpectedOcclusionOpaqueTailSha256, "occlusion opaque tail");
        int count = manifest.Sectors.Count;
        byte[] output = Enumerable.Repeat((byte)0xFF, OcclusionLength).ToArray();
        WriteInt32(output, 0, OcclusionLength);
        WriteInt32(output, 4, 0x71C);
        WriteInt32(output, 8, 16);
        WriteInt32(output, 12, 0x48);
        int next = 0x48 + count + 1;
        for (int group = 1; group < 16; group++)
            WriteInt32(output, 12 + (group * 4), next++);
        for (int sector = 0; sector < count; sector++)
            output[0x4C + sector] = checked((byte)sector);
        tail.CopyTo(output, 0x720);
        IReadOnlyList<int>[] groups = ParseOcclusionGroups(output, count, out _);
        if (!groups[0].SequenceEqual(Enumerable.Range(0, count)) ||
            groups.Skip(1).Any(group => group.Count != 0) ||
            output.AsSpan(0x4C + count + 16, 0x720 - (0x4C + count + 16)).IndexOfAnyExcept((byte)0xFF) >= 0)
            throw new InvalidDataException("The authored support occlusion group ownership changed.");
        string expected = count == 4 ? ExpectedCoreOcclusionSha256 : ExpectedFullOcclusionSha256;
        RequireHash(Hash(output), expected, $"{count}-sector support occlusion");
        return output;
    }

    private static IReadOnlyList<int>[] ParseOcclusionGroups(
        byte[] component,
        int sectorCount,
        out int[] pointers)
    {
        int environmentLength = ReadInt32(component, 4);
        int environmentEnd = 4 + environmentLength;
        int groupCount = ReadInt32(component, 8);
        if (environmentLength < 8 || environmentEnd > component.Length || groupCount != 16 ||
            12 + (groupCount * 4) > environmentEnd)
            throw new InvalidDataException("The occlusion group directory is invalid.");
        pointers = new int[groupCount];
        IReadOnlyList<int>[] groups = new IReadOnlyList<int>[groupCount];
        for (int group = 0; group < groupCount; group++)
        {
            int relative = ReadInt32(component, 12 + (group * 4));
            pointers[group] = relative;
            int offset = 4 + relative;
            if (offset < 12 + (groupCount * 4) || offset >= environmentEnd)
                throw new InvalidDataException("An occlusion group points outside its directory.");
            List<int> sectors = [];
            while (offset < environmentEnd && component[offset] != 0xFF)
            {
                if (component[offset] >= sectorCount)
                    throw new InvalidDataException("An occlusion group references an invalid sector.");
                sectors.Add(component[offset++]);
            }
            if (offset >= environmentEnd)
                throw new InvalidDataException("An occlusion group lacks a terminator.");
            groups[group] = sectors;
        }
        return groups;
    }

    private static byte[] BuildRemoteCollision(byte[] source)
    {
        ValidateCollisionHeader(source);
        byte[] sourceTriangle = source.AsSpan(CollisionTrianglesOffset + (13_995 * 12), 12).ToArray();
        if (Convert.ToHexString(sourceTriangle) != "A7A2140044631900E0010000" ||
            source[CollisionAssignmentsOffset + 13_995] != 0xFF)
            throw new InvalidDataException("The exact locked T13995 collision substrate changed.");
        byte[] tree = Slice(source, CollisionTreeOffset, CollisionTreeLength);
        byte[] blocks = Slice(source, CollisionBlocksOffset, CollisionBlocksLength);
        RequireHash(Hash(tree), UnusedLevel65RemoteBlankIsolationConstruction.ExpectedSourceTreeSha256,
            "locked collision tree");
        RequireHash(Hash(blocks), UnusedLevel65RemoteBlankIsolationConstruction.ExpectedSourceBlocksSha256,
            "locked collision blocks");
        NativeCollisionIndex native = DecodeCollisionIndex(
            tree, blocks, blocks.Length, CollisionTriangleCount, requireZeroTail: false);
        Id65AuthoringCollisionCell sourceCell = new(34, 35, 1);
        Id65AuthoringCollisionCell targetCell = new(1, 1, 2);
        int[] sourceSequence =
        [
            14_001, 14_000, 13_999, 13_998, 13_995, 11_276, 11_234, 11_197,
            11_196, 11_191, 11_190, 11_184, 3_130, 3_129, 3_128, 3_066,
            3_065, 3_064, 3_060, 3_058, 3_057, 3_056, 3_055
        ];
        if (native.Cells.Count != 4_252 || native.GroupStartCount != 4_253 ||
            !native.Cells.TryGetValue(sourceCell, out NativeCollisionBinding? binding) ||
            !binding.TriangleIndexes.SequenceEqual(sourceSequence) ||
            native.Cells.ContainsKey(targetCell))
            throw new InvalidDataException("The native collision-cell substrate changed.");
        TreeLeaf targetLeaf = EnumerateTreeLeaves(tree).Single(item => item.Cell == targetCell);
        if (targetLeaf.PointerByteOffset != 0x1254 || targetLeaf.BlockWordOffset != 0xFFFF)
            throw new InvalidDataException("The remote collision target leaf changed.");

        Dictionary<Id65AuthoringCollisionCell, int[]> desired = native.Cells
            .ToDictionary(item => item.Key, item => item.Value.TriangleIndexes.ToArray());
        desired[sourceCell] = desired[sourceCell].Where(index => index != 13_995).ToArray();
        desired.Add(targetCell, [13_995]);
        (byte[] outputTree, byte[] outputBlocks, int usedBytes) =
            RepackCollision(native, desired, targetCell, targetLeaf.PointerByteOffset);
        if (usedBytes != 0x17960)
            throw new InvalidDataException("The negative-winding v2 block used length changed.");
        RequireHash(Hash(outputTree),
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputTreeSha256,
            "negative-winding v2 tree");
        RequireHash(Hash(outputBlocks), UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputBlocksSha256,
            "negative-winding v2 blocks");

        byte[] output = source.ToArray();
        outputTree.CopyTo(output, CollisionTreeOffset);
        outputBlocks.CopyTo(output, CollisionBlocksOffset);
        Convert.FromHexString("100138381001007000020000")
            .CopyTo(output, CollisionTrianglesOffset + (13_995 * 12));
        output[CollisionAssignmentsOffset + 13_995] = 0;
        return output;
    }

    private static byte[] BuildSupportCollision(
        byte[] postV2Collision,
        Id65AuthoringSupportManifest manifest)
    {
        ValidateCollisionHeader(postV2Collision);
        int sectorCount = manifest.Sectors.Count;
        if (sectorCount is not (4 or 8))
            throw new InvalidDataException("Support collision requires four or eight sectors.");
        byte[] tree = Slice(postV2Collision, CollisionTreeOffset, CollisionTreeLength);
        byte[] sourceBlocks = Slice(postV2Collision, CollisionBlocksOffset, CollisionBlocksLength);
        NativeCollisionIndex postV2Index = DecodeCollisionIndex(
            tree, sourceBlocks, 0x17960, CollisionTriangleCount, requireZeroTail: true);
        TreeLeaf[] leaves = EnumerateTreeLeaves(tree);
        if (postV2Index.Cells.Count != 4_253 || leaves.Count(item => item.BlockWordOffset != 0xFFFF) != 4_253)
            throw new InvalidDataException("The exact 4,253-leaf post-v2 collision substrate changed.");
        foreach (TreeLeaf leaf in leaves)
            WriteUInt16(tree, leaf.PointerByteOffset, 0xFFFF);

        byte[] blocks = new byte[CollisionBlocksLength];
        int word = 0;
        foreach (Id65SupportCollisionCellIntent cell in manifest.CollisionCells)
        {
            if (cell.BlockWordOffset != word || cell.TriangleIndexes.Count == 0 ||
                !IsStrictlyDescending(cell.TriangleIndexes) ||
                cell.TriangleIndexes.Any(index => index < 0 || index >= CollisionTriangleCount))
                throw new InvalidDataException("A support collision block group is invalid or out of order.");
            TreeLeaf leaf = leaves.Single(item => item.Cell == cell.Cell);
            if (leaf.PointerByteOffset != cell.TreePointerByteOffset)
                throw new InvalidDataException("A support collision tree pointer changed.");
            WriteUInt16(tree, leaf.PointerByteOffset, checked((ushort)word));
            for (int index = 0; index < cell.TriangleIndexes.Count; index++)
            {
                ushort value = checked((ushort)cell.TriangleIndexes[index]);
                if (index == 0)
                    value |= 0x8000;
                WriteUInt16(blocks, word++ * 2, value);
            }
        }
        WriteUInt16(blocks, word++ * 2, 0x8000);
        int usedBytes = word * 2;
        if (usedBytes != manifest.CollisionBlocksUsedBytes)
            throw new InvalidDataException("The support collision block capacity readback changed.");

        byte[] output = postV2Collision.ToArray();
        tree.CopyTo(output, CollisionTreeOffset);
        blocks.CopyTo(output, CollisionBlocksOffset);
        HashSet<int> ownedRows = [];
        foreach (Id65SupportFaceIntent face in manifest.Sectors.SelectMany(item => item.Faces))
        {
            if (!ownedRows.Add(face.CollisionTriangleIndex) || face.CollisionNormalZ != -115_200 ||
                CollisionNormalZ(face.CollisionPoints) != -115_200 ||
                face.RenderNormalZ != 115_200)
                throw new InvalidDataException("A support collision row, point order, or normal changed.");
            byte[] triangle = EncodeCollisionTriangle(face.CollisionPoints);
            triangle.CopyTo(output, CollisionTrianglesOffset + (face.CollisionTriangleIndex * 12));
            output[CollisionAssignmentsOffset + face.CollisionTriangleIndex] = 0;
        }

        for (int index = 0; index < CollisionTriangleCount; index++)
        {
            if (ownedRows.Contains(index))
                continue;
            if (!output.AsSpan(CollisionTrianglesOffset + (index * 12), 12)
                    .SequenceEqual(postV2Collision.AsSpan(CollisionTrianglesOffset + (index * 12), 12)) ||
                output[CollisionAssignmentsOffset + index] != postV2Collision[CollisionAssignmentsOffset + index])
                throw new InvalidDataException("Support collision mutated an unowned triangle or assignment row.");
        }
        if (!output.AsSpan(0, CollisionTreeOffset).SequenceEqual(postV2Collision.AsSpan(0, CollisionTreeOffset)) ||
            !output.AsSpan(CollisionFlagsOffset).SequenceEqual(postV2Collision.AsSpan(CollisionFlagsOffset)))
            throw new InvalidDataException("Support collision changed a protected header or flag table.");

        NativeCollisionIndex readback = DecodeCollisionIndex(
            tree, blocks, usedBytes, CollisionTriangleCount, requireZeroTail: true);
        if (readback.Cells.Count != manifest.CollisionCells.Count ||
            manifest.CollisionCells.Any(expected =>
                !readback.Cells.TryGetValue(expected.Cell, out NativeCollisionBinding? actual) ||
                !actual.TriangleIndexes.SequenceEqual(expected.TriangleIndexes)) ||
            EnumerateTreeLeaves(tree).Any(leaf =>
                leaf.BlockWordOffset != 0xFFFF &&
                !manifest.CollisionCells.Any(cell => cell.Cell == leaf.Cell)))
            throw new InvalidDataException("Support collision tree/block semantic readback failed.");

        string expectedTree = sectorCount == 4 ? ExpectedCoreTreeSha256 : ExpectedFullTreeSha256;
        string expectedBlocks = sectorCount == 4 ? ExpectedCoreBlocksSha256 : ExpectedFullBlocksSha256;
        string expectedCollision = sectorCount == 4 ? ExpectedCoreCollisionSha256 : ExpectedFullCollisionSha256;
        RequireHash(Hash(tree), expectedTree, $"{sectorCount}-sector support collision tree");
        RequireHash(Hash(blocks), expectedBlocks, $"{sectorCount}-sector support collision blocks");
        RequireHash(Hash(output), expectedCollision, $"{sectorCount}-sector support collision component");
        return output;
    }

    private static void ValidateCollisionHeader(byte[] collision)
    {
        if (collision.Length != CollisionLength || ReadInt32(collision, 0) != CollisionLength ||
            ReadInt32(collision, 4) != CollisionTriangleCount || ReadInt32(collision, 8) != 0x2904 ||
            ReadInt32(collision, 12) != 0x1C || ReadInt32(collision, 16) != 0x6A7C ||
            ReadInt32(collision, 20) != 0x1E400 || ReadInt32(collision, 24) != 0x58480 ||
            ReadInt32(collision, 28) != 0x5D1E0 ||
            CollisionBlocksOffset - CollisionTreeOffset != CollisionTreeLength ||
            CollisionTrianglesOffset - CollisionBlocksOffset != CollisionBlocksLength ||
            CollisionTrianglesOffset + (CollisionTriangleCount * 12) != CollisionAssignmentsOffset ||
            CollisionFlagsOffset + 0x2904 != collision.Length)
            throw new InvalidDataException("The fixed support collision component header changed.");
    }

    private static NativeCollisionIndex DecodeCollisionIndex(
        byte[] tree,
        byte[] blocks,
        int usedBlockBytes,
        int triangleCount,
        bool requireZeroTail)
    {
        if (tree.Length != CollisionTreeLength || blocks.Length != CollisionBlocksLength ||
            usedBlockBytes <= 0 || (usedBlockBytes & 1) != 0 || usedBlockBytes > blocks.Length ||
            requireZeroTail && blocks.AsSpan(usedBlockBytes).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("The collision block used length or zero tail is invalid.");
        int usedWords = usedBlockBytes / 2;
        List<int> starts = [];
        for (int word = 0; word < usedWords; word++)
            if ((ReadUInt16(blocks, word * 2) & 0x8000) != 0)
                starts.Add(word);
        if (starts.Count < 2 || starts[^1] != usedWords - 1 ||
            ReadUInt16(blocks, starts[^1] * 2) != 0x8000)
            throw new InvalidDataException("The collision blocks lack their terminal sentinel.");
        Dictionary<int, int[]> groups = [];
        for (int group = 0; group + 1 < starts.Count; group++)
        {
            int start = starts[group];
            int end = starts[group + 1];
            int[] sequence = new int[end - start];
            for (int index = 0; index < sequence.Length; index++)
            {
                ushort value = ReadUInt16(blocks, (start + index) * 2);
                if ((index == 0) != ((value & 0x8000) != 0))
                    throw new InvalidDataException("A collision block group marker is invalid.");
                int triangle = value & 0x7FFF;
                if (triangle >= triangleCount)
                    throw new InvalidDataException("A collision group references an invalid triangle.");
                sequence[index] = triangle;
            }
            if (!IsStrictlyDescending(sequence))
                throw new InvalidDataException("A collision cell sequence is not strictly descending.");
            groups.Add(start, sequence);
        }
        Dictionary<Id65AuthoringCollisionCell, NativeCollisionBinding> cells = [];
        foreach (TreeLeaf leaf in EnumerateTreeLeaves(tree))
        {
            if (leaf.BlockWordOffset == 0xFFFF)
                continue;
            if (!groups.TryGetValue(leaf.BlockWordOffset, out int[]? sequence))
                throw new InvalidDataException("A collision leaf points outside a real block group.");
            cells.Add(leaf.Cell, new(leaf.Cell, leaf.PointerByteOffset, leaf.BlockWordOffset, sequence));
        }
        if (!cells.Values.Select(item => item.BlockWordOffset).Distinct().Order()
                .SequenceEqual(groups.Keys.Order()))
            throw new InvalidDataException("Collision blocks contain an unreferenced group.");
        return new(tree.ToArray(), blocks.ToArray(), cells, starts.Count, usedBlockBytes);
    }

    private static TreeLeaf[] EnumerateTreeLeaves(byte[] tree)
    {
        int SegmentLength(int offset)
        {
            if (offset < 0 || (offset & 1) != 0 || offset + 2 > tree.Length)
                throw new InvalidDataException("A collision tree segment escaped capacity.");
            int length = ReadUInt16(tree, offset);
            if (length > 256 || offset + ((length + 1L) * 2L) > tree.Length)
                throw new InvalidDataException("A collision tree segment is invalid.");
            return length;
        }
        List<TreeLeaf> leaves = [];
        int zLength = SegmentLength(0);
        for (int z = 0; z < zLength; z++)
        {
            int yOffset = ReadUInt16(tree, (z + 1) * 2);
            if (yOffset == 0xFFFF)
                continue;
            int yLength = SegmentLength(yOffset);
            for (int y = 0; y < yLength; y++)
            {
                int xOffset = ReadUInt16(tree, yOffset + ((y + 1) * 2));
                if (xOffset == 0xFFFF)
                    continue;
                int xLength = SegmentLength(xOffset);
                for (int x = 0; x < xLength; x++)
                {
                    int pointer = xOffset + ((x + 1) * 2);
                    leaves.Add(new(new(x, y, z), pointer, ReadUInt16(tree, pointer)));
                }
            }
        }
        return leaves.ToArray();
    }

    private static (byte[] Tree, byte[] Blocks, int UsedBytes) RepackCollision(
        NativeCollisionIndex native,
        IReadOnlyDictionary<Id65AuthoringCollisionCell, int[]> desired,
        Id65AuthoringCollisionCell addedCell,
        int addedTreePointer)
    {
        if (desired.Count != native.Cells.Count + 1)
            throw new InvalidDataException("The v2 collision repack ownership envelope changed.");
        Dictionary<string, int> emitted = new(StringComparer.Ordinal);
        Dictionary<Id65AuthoringCollisionCell, int> offsets = [];
        List<ushort> words = [];
        void Emit(Id65AuthoringCollisionCell cell, int[] sequence)
        {
            if (sequence.Length == 0 || !IsStrictlyDescending(sequence))
                throw new InvalidDataException("A v2 desired collision sequence is invalid.");
            string key = string.Join(',', sequence);
            if (!emitted.TryGetValue(key, out int offset))
            {
                offset = words.Count;
                for (int index = 0; index < sequence.Length; index++)
                {
                    ushort value = checked((ushort)sequence[index]);
                    if (index == 0)
                        value |= 0x8000;
                    words.Add(value);
                }
                emitted.Add(key, offset);
            }
            offsets.Add(cell, offset);
        }
        foreach (NativeCollisionBinding binding in native.Cells.Values
                     .OrderBy(item => item.BlockWordOffset)
                     .ThenBy(item => item.Cell.Z)
                     .ThenBy(item => item.Cell.Y)
                     .ThenBy(item => item.Cell.X))
            Emit(binding.Cell, desired[binding.Cell]);
        Emit(addedCell, desired[addedCell]);
        words.Add(0x8000);
        int usedBytes = words.Count * 2;
        if (usedBytes > CollisionBlocksLength)
            throw new InvalidDataException("The v2 collision repack exceeds block capacity.");
        byte[] tree = native.Tree.ToArray();
        foreach (NativeCollisionBinding binding in native.Cells.Values)
            WriteUInt16(tree, binding.TreePointerByteOffset, checked((ushort)offsets[binding.Cell]));
        WriteUInt16(tree, addedTreePointer, checked((ushort)offsets[addedCell]));
        byte[] blocks = new byte[CollisionBlocksLength];
        for (int index = 0; index < words.Count; index++)
            WriteUInt16(blocks, index * 2, words[index]);
        NativeCollisionIndex readback = DecodeCollisionIndex(
            tree, blocks, usedBytes, CollisionTriangleCount, requireZeroTail: true);
        if (readback.Cells.Count != desired.Count ||
            readback.Cells.Any(item => !desired[item.Key].SequenceEqual(item.Value.TriangleIndexes)))
            throw new InvalidDataException("The v2 collision repack semantic readback failed.");
        return (tree, blocks, usedBytes);
    }

    private static byte[] EncodeCollisionTriangle(IReadOnlyList<Id65AuthoringPoint> points)
    {
        if (points.Count != 3)
            throw new InvalidDataException("A support collision triangle must have three points.");
        Id65AuthoringPoint p1 = points[0];
        Id65AuthoringPoint p2 = points[1];
        Id65AuthoringPoint p3 = points[2];
        if (!TrySigned9(p2.X - p1.X, out uint p2Dx) ||
            !TrySigned9(p3.X - p1.X, out uint p3Dx) ||
            !TrySigned9(p2.Y - p1.Y, out uint p2Dy) ||
            !TrySigned9(p3.Y - p1.Y, out uint p3Dy) ||
            p1.X is < 0 or > 0x3FFF || p1.Y is < 0 or > 0x3FFF || p1.Z is < 0 or > 0x3FFF ||
            p2.Z - p1.Z is < 0 or > 0xFF || p3.Z - p1.Z is < 0 or > 0xFF)
            throw new InvalidDataException("A support collision point cannot pack into signed9 native geometry.");
        uint x = (uint)(p1.X & 0x3FFF) | (p2Dx << 14) | (p3Dx << 23);
        uint y = (uint)(p1.Y & 0x3FFF) | (p2Dy << 14) | (p3Dy << 23);
        uint z = (uint)(p1.Z & 0x3FFF) |
                 ((uint)(p2.Z - p1.Z) << 16) |
                 ((uint)(p3.Z - p1.Z) << 24);
        byte[] output = new byte[12];
        WriteUInt32(output, 0, x);
        WriteUInt32(output, 4, y);
        WriteUInt32(output, 8, z);
        return output;
    }

    private static bool TrySigned9(int value, out uint encoded)
    {
        if (value is < -256 or > 255)
        {
            encoded = 0;
            return false;
        }
        encoded = (uint)(value & 0x1FF);
        return true;
    }

    private static long CollisionNormalZ(IReadOnlyList<Id65AuthoringPoint> points)
    {
        Id65AuthoringPoint a = points[0];
        Id65AuthoringPoint b = points[1];
        Id65AuthoringPoint c = points[2];
        return ((long)b.X - a.X) * ((long)c.Y - a.Y) -
               ((long)b.Y - a.Y) * ((long)c.X - a.X);
    }

    private static bool IsStrictlyDescending(IReadOnlyList<int> values)
    {
        for (int index = 1; index < values.Count; index++)
            if (values[index - 1] <= values[index])
                return false;
        return true;
    }

    private static Id65SupportStateReadback ValidateState(
        Id65AuthoringSupportManifest manifest,
        byte[] lockedModel,
        byte[] stateModel,
        byte[] stateRow80)
    {
        ValidateExactManifest(manifest, manifest.Kind);
        if (lockedModel.Length != ModelLength || stateModel.Length != ModelLength ||
            stateRow80.Length != Row80ByteLength ||
            !stateRow80.AsSpan(ModelOffset, ModelLength).SequenceEqual(stateModel))
            throw new InvalidDataException("A support state image has an invalid model binding.");
        RequireHash(Hash(lockedModel), ExpectedLockedModelSha256, "support readback locked model");
        RequireHash(Hash(stateRow80.AsSpan(TexturePagesOffset, TexturePagesLength)),
            Id65V2NativeTextureCompositionCompiler.ExpectedOutputTexturePagesSha256,
            "support readback T66 pages");
        if (!stateRow80.AsSpan(LandingOffset, 8).SequenceEqual(Convert.FromHexString("091800000A180000")) ||
            ReadInt32(stateRow80, T92XyOffset) != 6_153 || ReadInt32(stateRow80, T92XyOffset + 4) != 6_154)
            throw new InvalidDataException("The support state lost post-v2 spawn ownership.");

        Id65SupportStateLayout layout = manifest.Layout;
        if (layout.UsedModelBytes + layout.ZeroTailBytes != ModelLength ||
            stateModel.AsSpan(layout.UsedModelBytes).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("The support state used/tail contract changed.");
        byte[] environment = Slice(stateModel, layout.EnvironmentOffset, layout.EnvironmentByteLength);
        byte[] occlusion = Slice(stateModel, layout.OcclusionOffset, layout.OcclusionByteLength);
        byte[] collision = Slice(stateModel, layout.CollisionOffset, layout.CollisionByteLength);
        ValidateCollisionHeader(collision);
        RequireHash(Hash(collision), manifest.CollisionComponentSha256, "support state collision");
        RequireHash(Hash(collision.AsSpan(CollisionTreeOffset, CollisionTreeLength)),
            manifest.CollisionTreeSha256, "support state collision tree");
        RequireHash(Hash(collision.AsSpan(CollisionBlocksOffset, CollisionBlocksLength)),
            manifest.CollisionBlocksSha256, "support state collision blocks");
        RequireHash(Hash(occlusion), manifest.OcclusionSha256, "support state occlusion");
        RequireHash(Hash(occlusion.AsSpan(0x720, 0x254)), ExpectedOcclusionOpaqueTailSha256,
            "support state occlusion opaque tail");

        int sectorCount;
        bool inheritedCleared;
        bool hpLp;
        bool seams;
        if (manifest.Kind == Id65SupportProfileKind.Empty)
        {
            if (ReadInt32(environment, 0) != environment.Length || ReadInt32(environment, 4) != 217)
                throw new InvalidDataException("The empty post-v2 environment lost 217 sectors.");
            sectorCount = 217;
            NativeCollisionIndex index = DecodeCollisionIndex(
                Slice(collision, CollisionTreeOffset, CollisionTreeLength),
                Slice(collision, CollisionBlocksOffset, CollisionBlocksLength),
                0x17960,
                CollisionTriangleCount,
                requireZeroTail: true);
            if (index.Cells.Count != 4_253)
                throw new InvalidDataException("The empty post-v2 collision lookup changed.");
            inheritedCleared = false;
            hpLp = true;
            seams = true;
        }
        else
        {
            int expectedCount = manifest.Sectors.Count;
            string expectedEnvironment = expectedCount == 4
                ? ExpectedCoreEnvironmentSha256
                : ExpectedFullEnvironmentSha256;
            RequireHash(Hash(environment), expectedEnvironment, "support state environment");
            if (ReadInt32(environment, 0) != environment.Length ||
                ReadInt32(environment, 4) != expectedCount)
                throw new InvalidDataException("The authored support environment header changed.");
            for (int index = 0; index < expectedCount; index++)
            {
                int offset = 4 + ReadInt32(environment, 8 + (index * 4));
                if (offset != 8 + (expectedCount * 4) + (index * 0xC8))
                    throw new InvalidDataException("A support sector pointer changed.");
                RequireHash(Hash(environment.AsSpan(offset, 0xC8)),
                    manifest.Sectors[index].ExactSectorSha256,
                    $"support state sector {index}");
            }
            NativeCollisionIndex indexReadback = DecodeCollisionIndex(
                Slice(collision, CollisionTreeOffset, CollisionTreeLength),
                Slice(collision, CollisionBlocksOffset, CollisionBlocksLength),
                manifest.CollisionBlocksUsedBytes,
                CollisionTriangleCount,
                requireZeroTail: true);
            if (indexReadback.Cells.Count != manifest.CollisionCells.Count ||
                indexReadback.Cells.Any(item =>
                    !manifest.CollisionCells.Any(expected => expected.Cell == item.Key &&
                        expected.TriangleIndexes.SequenceEqual(item.Value.TriangleIndexes))))
                throw new InvalidDataException("The support state collision cells changed.");
            sectorCount = expectedCount;
            inheritedCleared = true;
            hpLp = manifest.Sectors.All(sector => sector.Vertices.Count == 5 && sector.Faces.Count == 4 &&
                sector.Faces.All(face => face.HighDetailHex.Contains("42024001", StringComparison.Ordinal) &&
                    face.LowDetailHandle.EndsWith(":lp", StringComparison.Ordinal) &&
                    face.HighDetailHandle.EndsWith(":hp", StringComparison.Ordinal)));
            seams = manifest.Seams.All(item => item.LowDetailExact && item.HighDetailExact && item.TjunctionFree);
            if (!hpLp || !seams)
                throw new InvalidDataException("The support HP/LP pairing or seam contract changed.");
        }

        string[] protectedNames = ["special", "cyclorama", "portal", "particles", "sound"];
        (int Source, int Length, int Output)[] protectedRanges =
        [
            (SourceSpecialOffset, SpecialLength, layout.SpecialSurfaceOffset),
            (SourceCycloramaOffset, CycloramaLength, layout.CycloramaOffset),
            (SourcePortalOffset, PortalLength, layout.PortalOffset),
            (SourceParticlesOffset, ParticlesLength, layout.ParticlesOffset),
            (SourceSoundOffset, SoundLength, layout.SoundOffset)
        ];
        for (int index = 0; index < protectedRanges.Length; index++)
        {
            (int sourceOffset, int length, int outputOffset) = protectedRanges[index];
            if (!lockedModel.AsSpan(sourceOffset, length).SequenceEqual(stateModel.AsSpan(outputOffset, length)))
                throw new InvalidDataException($"Protected component `{protectedNames[index]}` changed.");
        }

        string modelHash = Hash(stateModel);
        string rowHash = Hash(stateRow80);
        string expectedModel = manifest.Kind switch
        {
            Id65SupportProfileKind.Empty => ExpectedEmptyModelSha256,
            Id65SupportProfileKind.Core4 => ExpectedCoreModelSha256,
            Id65SupportProfileKind.Full8 => ExpectedFullModelSha256,
            _ => throw new InvalidDataException("Unknown support state.")
        };
        string expectedRow = manifest.Kind switch
        {
            Id65SupportProfileKind.Empty => ExpectedEmptyRow80Sha256,
            Id65SupportProfileKind.Core4 => ExpectedCoreRow80Sha256,
            Id65SupportProfileKind.Full8 => ExpectedFullRow80Sha256,
            _ => throw new InvalidDataException("Unknown support state.")
        };
        RequireHash(modelHash, expectedModel, "support state model identity");
        RequireHash(rowHash, expectedRow, "support state row-80 identity");
        return new(
            manifest.Kind,
            rowHash,
            modelHash,
            Hash(environment),
            Hash(collision),
            Hash(collision.AsSpan(CollisionTreeOffset, CollisionTreeLength)),
            Hash(collision.AsSpan(CollisionBlocksOffset, CollisionBlocksLength)),
            Hash(occlusion),
            sectorCount,
            CollisionTriangleCount,
            manifest.Kind == Id65SupportProfileKind.Empty ? 4_253 : manifest.CollisionCells.Count,
            manifest.CollisionBlocksUsedBytes,
            layout.UsedModelBytes,
            layout.ZeroTailBytes,
            inheritedCleared,
            hpLp,
            seams,
            true,
            false);
    }

    private static List<Id65SupportOwnedRange> BuildOwnedRanges(byte[] source, byte[] output)
    {
        if (source.Length != Row80ByteLength || output.Length != Row80ByteLength)
            throw new InvalidDataException("Support ownership received an invalid row length.");
        List<Id65SupportOwnedRange> ranges = [];
        int cursor = 0;
        while (cursor < source.Length)
        {
            while (cursor < source.Length && source[cursor] == output[cursor])
                cursor++;
            if (cursor == source.Length)
                break;
            int start = cursor;
            while (cursor < source.Length && source[cursor] != output[cursor])
                cursor++;
            int length = cursor - start;
            ranges.Add(new(
                $"support.diff.{ranges.Count:D5}",
                "support.profile-transition",
                start,
                source.AsSpan(start, length).ToArray(),
                output.AsSpan(start, length).ToArray()));
        }
        if (ranges.Count == 0 || ranges.Any(item => item.DataRelativeOffset < ModelOffset ||
                item.DataRelativeOffset + item.ByteLength > ModelOffset + ModelLength))
            throw new InvalidDataException("Support state diff escaped the model or became empty.");
        return ranges;
    }

    private static void ValidateDiffOwnership(
        byte[] source,
        byte[] output,
        IReadOnlyList<Id65SupportOwnedRange> ranges)
    {
        if (source.Length != output.Length || source.Length != Row80ByteLength || ranges.Count == 0 ||
            ranges.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count() != ranges.Count)
            throw new InvalidDataException("The support diff ownership envelope is invalid.");
        bool[] owned = new bool[source.Length];
        for (int index = 0; index < ranges.Count; index++)
        {
            Id65SupportOwnedRange range = ranges[index];
            byte[] before = range.CopyBefore();
            byte[] after = range.CopyAfter();
            if (range.OwnerId != "support.profile-transition" || range.ByteLength <= 0 ||
                range.DataRelativeOffset < 0 || range.DataRelativeOffset + (long)range.ByteLength > source.Length ||
                before.Length != range.ByteLength || after.Length != range.ByteLength ||
                !HashEquals(Hash(before), range.BeforeSha256) ||
                !HashEquals(Hash(after), range.AfterSha256) ||
                !source.AsSpan(range.DataRelativeOffset, range.ByteLength).SequenceEqual(before) ||
                !output.AsSpan(range.DataRelativeOffset, range.ByteLength).SequenceEqual(after) ||
                before.Zip(after).Any(pair => pair.First == pair.Second) ||
                index > 0 && ranges[index - 1].DataRelativeOffset + ranges[index - 1].ByteLength >=
                    range.DataRelativeOffset)
                throw new InvalidDataException("A support owned range is malformed, overlapping, or non-maximal.");
            for (int offset = range.DataRelativeOffset;
                 offset < range.DataRelativeOffset + range.ByteLength;
                 offset++)
            {
                if (owned[offset])
                    throw new InvalidDataException("Support owned ranges overlap.");
                owned[offset] = true;
            }
        }
        for (int index = 0; index < source.Length; index++)
            if ((source[index] != output[index]) != owned[index])
                throw new InvalidDataException("A support changed byte is unowned or an unchanged byte is over-owned.");
    }

    private static Id65ModelComponentRelocation[] BuildRelocations(
        Id65SupportStateLayout sourceLayout,
        Id65SupportStateLayout outputLayout,
        byte[] source,
        byte[] output)
    {
        (string Name, int Source, int SourceLength, int Output, int OutputLength)[] layout =
        [
            ("texture", sourceLayout.TextureOffset, sourceLayout.TextureByteLength,
                outputLayout.TextureOffset, outputLayout.TextureByteLength),
            ("environment", sourceLayout.EnvironmentOffset, sourceLayout.EnvironmentByteLength,
                outputLayout.EnvironmentOffset, outputLayout.EnvironmentByteLength),
            ("occlusion", sourceLayout.OcclusionOffset, sourceLayout.OcclusionByteLength,
                outputLayout.OcclusionOffset, outputLayout.OcclusionByteLength),
            ("special-surface", sourceLayout.SpecialSurfaceOffset, sourceLayout.SpecialSurfaceByteLength,
                outputLayout.SpecialSurfaceOffset, outputLayout.SpecialSurfaceByteLength),
            ("collision", sourceLayout.CollisionOffset, sourceLayout.CollisionByteLength,
                outputLayout.CollisionOffset, outputLayout.CollisionByteLength),
            ("cyclorama", sourceLayout.CycloramaOffset, sourceLayout.CycloramaByteLength,
                outputLayout.CycloramaOffset, outputLayout.CycloramaByteLength),
            ("portal-table", sourceLayout.PortalOffset, sourceLayout.PortalByteLength,
                outputLayout.PortalOffset, outputLayout.PortalByteLength),
            ("particles", sourceLayout.ParticlesOffset, sourceLayout.ParticlesByteLength,
                outputLayout.ParticlesOffset, outputLayout.ParticlesByteLength),
            ("sound", sourceLayout.SoundOffset, sourceLayout.SoundByteLength,
                outputLayout.SoundOffset, outputLayout.SoundByteLength)
        ];
        return layout.Select(item =>
        {
            byte[] before = Slice(source, item.Source, item.SourceLength);
            byte[] after = Slice(output, item.Output, item.OutputLength);
            return new Id65ModelComponentRelocation(
                $"component.{item.Name}",
                item.Source,
                item.SourceLength,
                item.Output,
                item.OutputLength,
                Hash(before),
                Hash(after),
                before.SequenceEqual(after));
        }).ToArray();
    }

    private static void ValidateRelocations(
        IReadOnlyList<Id65ModelComponentRelocation> relocations,
        Id65SupportStateLayout sourceLayout,
        Id65SupportStateLayout outputLayout,
        byte[] source,
        byte[] output)
    {
        string[] ids =
        [
            "component.texture", "component.environment", "component.occlusion",
            "component.special-surface", "component.collision", "component.cyclorama",
            "component.portal-table", "component.particles", "component.sound"
        ];
        if (relocations.Count != ids.Length ||
            !relocations.Select(item => item.StableId).SequenceEqual(ids) ||
            relocations.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count() != ids.Length)
            throw new InvalidDataException("The direct support component relocation identity set changed.");
        int sourceCursor = 0;
        int outputCursor = 0;
        foreach (Id65ModelComponentRelocation item in relocations)
        {
            if (item.SourceRelativeOffset != sourceCursor || item.OutputRelativeOffset != outputCursor ||
                item.SourceByteLength <= 0 || item.OutputByteLength <= 0 ||
                item.SourceRelativeOffset + (long)item.SourceByteLength > source.Length ||
                item.OutputRelativeOffset + (long)item.OutputByteLength > output.Length ||
                !HashEquals(Hash(source.AsSpan(item.SourceRelativeOffset, item.SourceByteLength)),
                    item.SourceSha256) ||
                !HashEquals(Hash(output.AsSpan(item.OutputRelativeOffset, item.OutputByteLength)),
                    item.OutputSha256) ||
                source.AsSpan(item.SourceRelativeOffset, item.SourceByteLength)
                    .SequenceEqual(output.AsSpan(item.OutputRelativeOffset, item.OutputByteLength)) !=
                    item.ContentsPreserved)
                throw new InvalidDataException($"Support relocation `{item.StableId}` changed.");
            sourceCursor += item.SourceByteLength;
            outputCursor += item.OutputByteLength;
        }
        string[] preserved =
        [
            "component.texture", "component.special-surface", "component.cyclorama",
            "component.portal-table", "component.particles", "component.sound"
        ];
        if (sourceCursor != sourceLayout.UsedModelBytes || outputCursor != outputLayout.UsedModelBytes ||
            !relocations.Where(item => item.ContentsPreserved).Select(item => item.StableId)
                .SequenceEqual(preserved) ||
            source.AsSpan(sourceCursor).IndexOfAnyExcept((byte)0) >= 0 ||
            output.AsSpan(outputCursor).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("Support relocation coverage, preservation, or tail boundary changed.");
    }

    private static Id65StableHandleRebase[] BuildRebases(
        Id65AuthoringSupportManifest source,
        Id65AuthoringSupportManifest output)
    {
        Dictionary<string, (string Kind, Id65LogicalAddress Address)> sourceHandles = BuildHandleMap(source);
        Dictionary<string, (string Kind, Id65LogicalAddress Address)> outputHandles = BuildHandleMap(output);
        return sourceHandles.Keys.Concat(outputHandles.Keys).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(id =>
            {
                bool hasSource = sourceHandles.TryGetValue(id, out var before);
                bool hasOutput = outputHandles.TryGetValue(id, out var after);
                string kind = hasSource ? before.Kind : after.Kind;
                if (hasSource && hasOutput && before.Kind != after.Kind)
                    throw new InvalidDataException($"Stable support handle `{id}` changed kind.");
                return new Id65StableHandleRebase(
                    id,
                    kind,
                    hasSource ? before.Address : null,
                    hasOutput ? after.Address : null);
            })
            .ToArray();
    }

    private static Dictionary<string, (string Kind, Id65LogicalAddress Address)> BuildHandleMap(
        Id65AuthoringSupportManifest manifest)
    {
        Dictionary<string, (string Kind, Id65LogicalAddress Address)> handles =
            new(StringComparer.Ordinal)
            {
                [Id65V2NativeTextureCompositionCompiler.PrivateTextureIntentId] =
                    ("texture-record", new("texture-record", 66, 0)),
                ["texture.locked.record25"] =
                    ("texture-record", new("texture-record", 25, 0)),
                ["spawn.remote-pad"] =
                    ("landing-record", new("landing-record", 0, 0)),
                ["moby.player-anchor.t92"] =
                    ("moby-row", new("moby-row", 92, 0)),
                ["music.slot35"] =
                    ("music-slot", new("music-slot", 35, 0)),
                ["totals.slot65"] =
                    ("totals-slot", new("level-slot", 65, 0)),
                ["exit.slot65"] =
                    ("exit-slot", new("level-slot", 65, 0)),
                ["save.slot65"] =
                    ("save-slot", new("level-slot", 65, 0))
            };
        if (manifest.Kind == Id65SupportProfileKind.Empty)
        {
            handles.Add("sector.remote-pad.216", ("scene-sector", new("scene-sector", 216, 0)));
            foreach ((string name, int index) in new[] { ("a", 0), ("b", 1), ("c", 2) })
            {
                handles.Add($"vertex.remote-pad.{name}:lp",
                    ("low-detail-vertex", new("scene-sector", 216, index)));
                handles.Add($"vertex.remote-pad.{name}:hp",
                    ("high-detail-vertex", new("scene-sector", 216, index)));
            }
            handles.Add("terrain.remote-pad.face.0:lp",
                ("low-detail-face", new("scene-sector", 216, 0)));
            handles.Add("terrain.remote-pad.face.0:hp",
                ("high-detail-face", new("scene-sector", 216, 0)));
            handles.Add("terrain.remote-pad.face.0:pair",
                ("hp-lp-pair", new("scene-sector", 216, 0)));
            handles.Add("collision.remote-pad.t13995",
                ("collision-triangle", new("collision-triangle", 13_995, 0)));
            handles.Add("occlusion.remote-pad.group0",
                ("occlusion-membership", new("occlusion-group", 0, 216)));
            return handles;
        }

        foreach (Id65SupportSectorIntent sector in manifest.Sectors)
        {
            handles.Add(sector.Id, ("scene-sector", new("scene-sector", sector.SectorIndex, 0)));
            for (int vertex = 0; vertex < sector.Vertices.Count; vertex++)
            {
                Id65SupportVertexIntent item = sector.Vertices[vertex];
                handles.Add(item.LowDetailHandle,
                    ("low-detail-vertex", new("scene-sector", sector.SectorIndex, vertex)));
                handles.Add(item.HighDetailHandle,
                    ("high-detail-vertex", new("scene-sector", sector.SectorIndex, vertex)));
            }
            for (int face = 0; face < sector.Faces.Count; face++)
            {
                Id65SupportFaceIntent item = sector.Faces[face];
                handles.Add(item.LowDetailHandle,
                    ("low-detail-face", new("scene-sector", sector.SectorIndex, face)));
                handles.Add(item.HighDetailHandle,
                    ("high-detail-face", new("scene-sector", sector.SectorIndex, face)));
                handles.Add(item.HpLpPairId,
                    ("hp-lp-pair", new("scene-sector", sector.SectorIndex, face)));
                handles.Add(item.CollisionHandle,
                    ("collision-triangle", new("collision-triangle", item.CollisionTriangleIndex, 0)));
            }
            handles.Add($"{sector.Id}:occlusion-group0",
                ("occlusion-membership", new("occlusion-group", 0, sector.SectorIndex)));
        }
        for (int seam = 0; seam < manifest.Seams.Count; seam++)
            handles.Add(manifest.Seams[seam].Id,
                ("terrain-seam", new("terrain-seam", seam, manifest.Seams[seam].FirstSectorIndex)));
        return handles;
    }

    private static void ValidateRebases(
        IReadOnlyList<Id65StableHandleRebase> rebases,
        Id65AuthoringSupportManifest source,
        Id65AuthoringSupportManifest output)
    {
        Id65StableHandleRebase[] expected = BuildRebases(source, output);
        Id65StableHandleRebase texture = rebases.Single(item => item.StableId ==
            Id65V2NativeTextureCompositionCompiler.PrivateTextureIntentId);
        Id65StableHandleRebase landing = rebases.Single(item => item.StableId == "spawn.remote-pad");
        Id65StableHandleRebase t92 = rebases.Single(item => item.StableId == "moby.player-anchor.t92");
        if (!rebases.SequenceEqual(expected) ||
            rebases.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count() != rebases.Count ||
            rebases.Any(item => item.Source is null && item.Output is null) ||
            texture.Source != new Id65LogicalAddress("texture-record", 66, 0) ||
            texture.Output != new Id65LogicalAddress("texture-record", 66, 0) ||
            landing.Source != new Id65LogicalAddress("landing-record", 0, 0) ||
            landing.Output != new Id65LogicalAddress("landing-record", 0, 0) ||
            t92.Source != new Id65LogicalAddress("moby-row", 92, 0) ||
            t92.Output != new Id65LogicalAddress("moby-row", 92, 0))
            throw new InvalidDataException("The direct stable support handle map changed.");
        string[] forbiddenCollapsed =
        [
            "support.vertex", "support.face", "support.collision", "terrain.remote-pad.face.0"
        ];
        if (rebases.Any(item => forbiddenCollapsed.Contains(item.StableId, StringComparer.Ordinal)))
            throw new InvalidDataException("A collapsed LP/HP/collision support handle escaped.");
    }

    private static Id65SupportCapacityReadback BuildCapacity(
        Id65AuthoringSupportManifest source,
        Id65AuthoringSupportManifest output) =>
        new(
            ModelLength,
            source.Layout.UsedModelBytes,
            output.Layout.UsedModelBytes,
            source.Layout.ZeroTailBytes,
            output.Layout.ZeroTailBytes,
            source.Kind == Id65SupportProfileKind.Empty ? 217 : source.Sectors.Count,
            output.Kind == Id65SupportProfileKind.Empty ? 217 : output.Sectors.Count,
            source.CollisionBlocksUsedBytes,
            output.CollisionBlocksUsedBytes,
            CollisionTriangleCount,
            CollisionTreeLength,
            CollisionBlocksLength,
            67,
            66,
            64);

    private static string HashDiffRanges(IEnumerable<Id65SupportDiffRange> ranges)
    {
        StringBuilder text = new();
        foreach (Id65SupportDiffRange item in ranges)
            text.Append(item.DataRelativeOffset.ToString("X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(item.ByteLength).Append('|').Append(item.OwnerStableId).Append('|')
                .Append(item.BeforeSha256).Append('|').Append(item.AfterSha256).Append('\n');
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static string HashOwnedRanges(IEnumerable<Id65SupportOwnedRange> ranges)
    {
        StringBuilder text = new();
        foreach (Id65SupportOwnedRange item in ranges)
            text.Append(item.StableId).Append('|').Append(item.OwnerId).Append('|')
                .Append(item.DataRelativeOffset.ToString("X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(item.ByteLength).Append('|').Append(item.BeforeSha256).Append('|')
                .Append(item.AfterSha256).Append('\n');
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static string HashTransaction(IEnumerable<Id65SupportOwnedRange> ranges)
    {
        StringBuilder text = new();
        foreach (Id65SupportOwnedRange item in ranges)
            text.Append(item.StableId).Append('|').Append(item.DataRelativeOffset).Append('|')
                .Append(Convert.ToHexString(item.CopyBefore())).Append('|')
                .Append(Convert.ToHexString(item.CopyAfter())).Append('\n');
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static string HashRelocations(IEnumerable<Id65ModelComponentRelocation> relocations)
    {
        StringBuilder text = new();
        foreach (Id65ModelComponentRelocation item in relocations)
            text.Append(item.StableId).Append('|').Append(item.SourceRelativeOffset).Append('|')
                .Append(item.SourceByteLength).Append('|').Append(item.OutputRelativeOffset).Append('|')
                .Append(item.OutputByteLength).Append('|').Append(item.SourceSha256).Append('|')
                .Append(item.OutputSha256).Append('|').Append(item.ContentsPreserved ? '1' : '0').Append('\n');
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static string HashRebases(IEnumerable<Id65StableHandleRebase> rebases)
    {
        StringBuilder text = new();
        foreach (Id65StableHandleRebase item in rebases)
            text.Append(item.StableId).Append('|').Append(item.Kind).Append('|')
                .Append(FormatAddress(item.Source)).Append('|').Append(FormatAddress(item.Output)).Append('\n');
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static string FormatAddress(Id65LogicalAddress? address) =>
        address.HasValue
            ? $"{address.Value.Space}:{address.Value.Primary}:{address.Value.Secondary}"
            : "null";

    private static string ComputeDeterministicPlanHash(
        Id65SupportTransitionKind transition,
        Id65AuthoringSupportManifest sourceManifest,
        Id65AuthoringSupportManifest outputManifest,
        byte[] sourceRow80,
        byte[] outputRow80,
        byte[] sourceModel,
        byte[] outputModel,
        string diffHash,
        string ownedHash,
        string relocationHash,
        string rebaseHash,
        string transactionHash) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n',
        [
            ProfileId,
            transition.ToString(),
            ExpectedLockedRow80Sha256,
            Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256,
            sourceManifest.CanonicalSha256,
            outputManifest.CanonicalSha256,
            Hash(sourceRow80),
            Hash(outputRow80),
            Hash(sourceModel),
            Hash(outputModel),
            diffHash,
            ownedHash,
            relocationHash,
            rebaseHash,
            transactionHash
        ])));

    private static Id65SupportFrozenTransitionPins FrozenTransitionPins(
        Id65SupportTransitionKind transition) =>
        transition switch
        {
            Id65SupportTransitionKind.AddCoreSupport => new(
                550_741,
                27_683,
                "1461d1e4b3e1a4fee36ecd241189a5b32cc8e2b2e0ab54b91bc58d8924fd3619",
                "ed6f76359de3278e6ebed1b7efea6df093d51a06bc54a004bf18b85837a86917",
                "c18302c4c6c14ae00022dd6db8827d1021205231a0bb18bdfa742b7844020cf5",
                "c8f83a8f7318221169989b9b9f188666163bc0a7daf697cda993bf155be954ab",
                "c1e427bc481ed287aa216477bab99214f38f38673e9d5cec403f17c40eac981a",
                "dbac770db883f551fc7f37bbc38eeb13b1d5ed1ff5a385e58077981536637d3e"),
            Id65SupportTransitionKind.AddEnemyBay => new(
                225_506,
                25_424,
                "793281faee27ae95b6f144db5912b9f8c6b0f2091ca2ab1d4fcae19c95bf5c8a",
                "a995e2b2245a8a554b3c5f79b857a83bf80b58ea997c7b9c4129a3ed1e6ac50f",
                "897759c9bd28e97f5e2673e6f264c6195adb5290cd04edde64d11ca91fbac7ee",
                "04e9f8c23ccdf1d32745402437c9b431e202750484183bf8036748a3ebde55eb",
                "98032389fd159d1bb4a48473f9a87dfc7ebf70c2a811626b0557da533a17fa1e",
                "9010c618ee6d20c81ba8a1a8b6110c2a2d9ce9bb7df661502925c94e9100bb92"),
            Id65SupportTransitionKind.RemoveEnemyBay => new(
                225_506,
                25_424,
                "e00cb4d235f36f75bc5b9c4decf971aed2bca0250b9f0e9c328c613b9290dda1",
                "42279113c8679676937641a6e89569b7c08fdad8228305bfd0cdc9e4c465ee01",
                "42b0cdc4f1b552cde62bacee88a5d37157fd9b47e92cd07a87db165830d805b4",
                "4b2010a89ee6af2abd354253602870fb968ad790c0b3f82cb6ea5f92709d0529",
                "29ad57ab6559ac861c9ef3aa21c1f8f50d4160290be6723680ec02a5c70a0b74",
                "f51a21927eb701a134399681a793eb1cdd097d8180c549c8c9506e6c2932db60"),
            Id65SupportTransitionKind.RemoveCore => new(
                550_741,
                27_683,
                "e9ec44809fa10ca97bbcd31b5aef7c1b91db491de347751a476b922874a9a5cc",
                "b3a4428d1ac6dacbbfd93be569dffabdf00ddd29ac0b917cd52d2b18a14fe5bf",
                "84258e4c0a181fafd9c16ebabd6896ae60db2f4e9094cdc9cea4c3ecf97145d4",
                "4554e8cf2fb31da501ad27912b7ea431558d7b98944f4ad3d3be12ded48a7e7b",
                "0364dc4e7984445a582abf3f99c4462fa2aef3beb9d9625dbdb0105210e0006b",
                "051c65f5af2062a8a0fdcb2d20abf9446ee9db0879ac7fccaa44bbc2556db606"),
            _ => throw new InvalidDataException("Unknown support transition kind.")
        };

    private static void ValidateFrozenTransitionPins(Id65CompiledSupportReplacement compiled)
    {
        Id65SupportFrozenTransitionPins expected = FrozenTransitionPins(compiled.Transition);
        if (compiled.ChangedByteCount != expected.ChangedByteCount ||
            compiled.DiffRangeCount != expected.DiffRangeCount ||
            !HashEquals(compiled.DiffManifestSha256, expected.DiffManifestSha256) ||
            !HashEquals(compiled.OwnedRangeMapSha256, expected.OwnedRangeMapSha256) ||
            !HashEquals(compiled.RelocationMapSha256, expected.RelocationMapSha256) ||
            !HashEquals(compiled.RebaseMapSha256, expected.RebaseMapSha256) ||
            !HashEquals(compiled.TransactionSha256, expected.TransactionSha256) ||
            !HashEquals(compiled.DeterministicPlanSha256, expected.DeterministicPlanSha256))
            throw new InvalidDataException($"The frozen {compiled.Transition} transition proof changed.");
    }

    private static Id65CompiledSupportReplacement InvertCore(
        Id65CompiledSupportReplacement compiled,
        bool verifyDoubleInverse)
    {
        Id65SupportTransitionKind transition = compiled.Transition switch
        {
            Id65SupportTransitionKind.AddCoreSupport => Id65SupportTransitionKind.RemoveCore,
            Id65SupportTransitionKind.RemoveCore => Id65SupportTransitionKind.AddCoreSupport,
            Id65SupportTransitionKind.AddEnemyBay => Id65SupportTransitionKind.RemoveEnemyBay,
            Id65SupportTransitionKind.RemoveEnemyBay => Id65SupportTransitionKind.AddEnemyBay,
            _ => throw new InvalidDataException("Unknown support transition kind.")
        };
        Id65SupportOwnedRange[] owned = compiled.OwnedRanges.Select(item => item.Invert()).ToArray();
        byte[] sourceRow80 = compiled.CopyOutputRow80();
        byte[] outputRow80 = compiled.CopySourceRow80();
        byte[] sourceModel = compiled.CopyOutputModel();
        byte[] outputModel = compiled.CopySourceModel();
        ValidateDiffOwnership(sourceRow80, outputRow80, owned);
        Id65SupportDiffRange[] diff = owned.Select(item => new Id65SupportDiffRange(
            item.DataRelativeOffset,
            item.ByteLength,
            item.StableId,
            item.BeforeSha256,
            item.AfterSha256)).ToArray();
        Id65ModelComponentRelocation[] relocations = InvertRelocations(compiled.Relocations).ToArray();
        Id65StableHandleRebase[] rebases = InvertRebases(compiled.HandleRebases).ToArray();
        ValidateRelocations(
            relocations,
            compiled.OutputManifest.Layout,
            compiled.SourceManifest.Layout,
            sourceModel,
            outputModel);
        ValidateRebases(rebases, compiled.OutputManifest, compiled.SourceManifest);
        string diffHash = HashDiffRanges(diff);
        string ownedHash = HashOwnedRanges(owned);
        string relocationHash = HashRelocations(relocations);
        string rebaseHash = HashRebases(rebases);
        string transactionHash = HashTransaction(owned);
        string planHash = ComputeDeterministicPlanHash(
            transition,
            compiled.OutputManifest,
            compiled.SourceManifest,
            sourceRow80,
            outputRow80,
            sourceModel,
            outputModel,
            diffHash,
            ownedHash,
            relocationHash,
            rebaseHash,
            transactionHash);
        Id65CompiledSupportReplacement inverse = new(
            transition,
            compiled.OutputManifest,
            compiled.SourceManifest,
            sourceRow80,
            outputRow80,
            sourceModel,
            outputModel,
            owned,
            diff,
            relocations,
            rebases,
            compiled.Capacity.Invert(),
            compiled.OutputState,
            compiled.SourceState,
            compiled.ChangedByteCount,
            diffHash,
            ownedHash,
            relocationHash,
            rebaseHash,
            transactionHash,
            planHash);
        ValidateFrozenTransitionPins(inverse);
        if (!ApplyTransactional(inverse, sourceRow80, reverse: false).SequenceEqual(outputRow80) ||
            !ApplyTransactional(inverse, outputRow80, reverse: true).SequenceEqual(sourceRow80))
            throw new InvalidDataException("The inverted support transaction failed exact application.");
        if (verifyDoubleInverse)
        {
            Id65CompiledSupportReplacement twice = InvertCore(inverse, verifyDoubleInverse: false);
            if (!CompiledEquivalent(compiled, twice))
                throw new InvalidDataException("The support transition double inverse changed its proof.");
        }
        return inverse;
    }

    private static bool CompiledEquivalent(
        Id65CompiledSupportReplacement left,
        Id65CompiledSupportReplacement right) =>
        left.Transition == right.Transition &&
        left.SourceManifest.CanonicalSha256 == right.SourceManifest.CanonicalSha256 &&
        left.OutputManifest.CanonicalSha256 == right.OutputManifest.CanonicalSha256 &&
        left.SourceRow80Sha256 == right.SourceRow80Sha256 &&
        left.OutputRow80Sha256 == right.OutputRow80Sha256 &&
        left.SourceModelSha256 == right.SourceModelSha256 &&
        left.OutputModelSha256 == right.OutputModelSha256 &&
        left.ChangedByteCount == right.ChangedByteCount &&
        left.DiffRangeCount == right.DiffRangeCount &&
        left.DiffManifestSha256 == right.DiffManifestSha256 &&
        left.OwnedRangeMapSha256 == right.OwnedRangeMapSha256 &&
        left.RelocationMapSha256 == right.RelocationMapSha256 &&
        left.RebaseMapSha256 == right.RebaseMapSha256 &&
        left.TransactionSha256 == right.TransactionSha256 &&
        left.DeterministicPlanSha256 == right.DeterministicPlanSha256 &&
        left.Capacity == right.Capacity && left.SourceState == right.SourceState &&
        left.OutputState == right.OutputState &&
        left.DiffRanges.SequenceEqual(right.DiffRanges) &&
        left.Relocations.SequenceEqual(right.Relocations) &&
        left.HandleRebases.SequenceEqual(right.HandleRebases) &&
        left.OwnedRanges.Select(OwnedTuple).SequenceEqual(right.OwnedRanges.Select(OwnedTuple)) &&
        left.CopySourceRow80().SequenceEqual(right.CopySourceRow80()) &&
        left.CopyOutputRow80().SequenceEqual(right.CopyOutputRow80());

    private static (string, string, int, int, string, string) OwnedTuple(Id65SupportOwnedRange item) =>
        (item.StableId, item.OwnerId, item.DataRelativeOffset, item.ByteLength,
            item.BeforeSha256, item.AfterSha256);

    private static byte[] Slice(byte[] bytes, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset + (long)length > bytes.Length)
            throw new InvalidDataException("A support byte slice escaped its owner.");
        return bytes.AsSpan(offset, length).ToArray();
    }

    private static int ReadInt32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));

    private static int ReadInt32(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));

    private static uint ReadUInt32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));

    private static ushort ReadUInt16(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));

    private static void WriteInt32(byte[] bytes, int offset, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, 4), value);

    private static void WriteUInt32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);

    private static void WriteUInt16(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), value);

    private static bool HashEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static void RequireHash(string actual, string expected, string owner)
    {
        if (!HashEquals(actual, expected))
            throw new InvalidDataException($"The exact {owner} SHA-256 changed: expected {expected}, got {actual}.");
    }

    private sealed record TreeLeaf(
        Id65AuthoringCollisionCell Cell,
        int PointerByteOffset,
        int BlockWordOffset);

    private sealed record NativeCollisionBinding(
        Id65AuthoringCollisionCell Cell,
        int TreePointerByteOffset,
        int BlockWordOffset,
        IReadOnlyList<int> TriangleIndexes);

    private sealed record NativeCollisionIndex(
        byte[] Tree,
        byte[] Blocks,
        IReadOnlyDictionary<Id65AuthoringCollisionCell, NativeCollisionBinding> Cells,
        int GroupStartCount,
        int UsedBlockBytes);
}
