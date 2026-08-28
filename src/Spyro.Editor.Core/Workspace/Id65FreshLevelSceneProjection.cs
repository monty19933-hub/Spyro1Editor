using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Rendering;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Workspace;

/// <summary>
/// Protected entry state for the fresh ID65 authoring scene. The player anchor
/// remains part of the candidate contract, but is deliberately not projected
/// as a visible or editable Moby.
/// </summary>
public sealed record Id65FreshLevelProtectedSpawn(
    string StableId,
    int PlayerAnchorTrueIndex,
    long LandingWadOffset,
    long PlayerAnchorWadOffset,
    int RawX,
    int RawY,
    int LandingRawZ,
    int PlayerRawZ,
    int GroundRawZ,
    int YawByte,
    int EntryDataByteLength)
{
    public bool IsProtected => true;
    public double X => RawX / 16.0;
    public double Y => RawY / 16.0;
    public double LandingZ => LandingRawZ / 16.0;
    public double PlayerZ => PlayerRawZ / 16.0;
    public double GroundZ => GroundRawZ / 16.0;
}

/// <summary>
/// Immutable presentation-only view of one schema-3/4 remote-pad Moby
/// placement. It is intentionally not an editable scene object, native
/// persistence instruction, runtime observation, candidate, or export grant.
/// </summary>
public sealed class Id65FreshLevelMobyPresentation
{
    internal Id65FreshLevelMobyPresentation(
        Id65RemotePadGrassT107Position position,
        string documentCanonicalSha256,
        string documentContentSha256)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (!IsCanonicalSha256(documentCanonicalSha256) ||
            !IsCanonicalSha256(documentContentSha256))
        {
            throw new InvalidDataException(
                "The fresh-level Moby presentation requires exact document hashes.");
        }

        MobyKind = Id65RemotePadMobyKind.GrassT107;
        MobyTypeName = "Grass";
        PlacementId = Id65RemotePadGrassT107Catalog.PlacementId;
        PositionId = position.PositionId;
        PositionLabel = position.Label;
        EditorX = position.EditorX;
        EditorY = position.EditorY;
        EditorZ = position.EditorZ;
        RawX = position.RawX;
        RawY = position.RawY;
        RawZ = position.RawZ;
        TargetTrueIndex = Id65RemotePadGrassT107Catalog.TargetTrueIndex;
        ActorId = Id65RemotePadGrassT107Catalog.ActorId;
        YawByte = Id65RemotePadGrassT107Catalog.YawByte;
        ExactYawByte = Id65RemotePadGrassT107Catalog.YawByte;
        SupportHandle = Id65RemotePadGrassT107Catalog.SupportHandle;
        SupportCollisionTrueIndex = Id65RemotePadGrassT107Catalog.SupportCollisionTrueIndex;
        RowSha256 = position.RowSha256;
        DocumentCanonicalSha256 = documentCanonicalSha256;
        DocumentContentSha256 = documentContentSha256;
        CanonicalSha256 = Hash(BuildCanonicalPayload());
    }

    internal Id65FreshLevelMobyPresentation(
        Id65RemotePadRedGemT21Position position,
        string documentCanonicalSha256,
        string documentContentSha256)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (!IsCanonicalSha256(documentCanonicalSha256) ||
            !IsCanonicalSha256(documentContentSha256))
        {
            throw new InvalidDataException(
                "The fresh-level Moby presentation requires exact document hashes.");
        }

        MobyKind = Id65RemotePadMobyKind.RedGemT21;
        MobyTypeName = Id65RemotePadRedGemT21Catalog.MobyTypeName;
        PlacementId = Id65RemotePadRedGemT21Catalog.PlacementId;
        PositionId = position.PositionId;
        PositionLabel = position.Label;
        EditorX = position.EditorX;
        EditorY = position.EditorY;
        EditorZ = position.EditorZ;
        RawX = position.RawX;
        RawY = position.RawY;
        RawZ = position.RawZ;
        TargetTrueIndex = Id65RemotePadRedGemT21Catalog.TargetTrueIndex;
        ActorId = Id65RemotePadRedGemT21Catalog.ActorClass;
        YawByte = 0;
        ExactYawByte = null;
        SupportHandle = Id65RemotePadRedGemT21Catalog.SupportHandle;
        SupportCollisionTrueIndex = Id65RemotePadRedGemT21Catalog.SupportCollisionTrueIndex;
        RowSha256 = position.OutputRowSha256;
        DocumentCanonicalSha256 = documentCanonicalSha256;
        DocumentContentSha256 = documentContentSha256;
        CanonicalSha256 = Hash(BuildCanonicalPayload());
    }

    public Id65RemotePadMobyKind MobyKind { get; }
    public string MobyTypeName { get; }
    public string PlacementId { get; }
    public string PositionId { get; }
    public string PositionLabel { get; }
    public int EditorX { get; }
    public int EditorY { get; }
    public int EditorZ { get; }
    public int RawX { get; }
    public int RawY { get; }
    public int RawZ { get; }
    public int TargetTrueIndex { get; }
    public ushort ActorId { get; }
    public byte YawByte { get; }
    public byte? ExactYawByte { get; }
    public string SupportHandle { get; }
    public int SupportCollisionTrueIndex { get; }
    public string RowSha256 { get; }
    public string DocumentCanonicalSha256 { get; }
    public string DocumentContentSha256 { get; }
    public string CanonicalSha256 { get; }

    public bool IsPresentationOnly => true;
    public bool IsEditable => false;
    public bool NativePersistenceEnabled => false;
    public bool RuntimeObserved => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool AuthorizesCreateBin => false;
    public bool AuthorizesReleasePromotion => false;

    private string BuildCanonicalPayload() => MobyKind == Id65RemotePadMobyKind.GrassT107
        ? "format=id65-fresh-level-visible-moby-presentation-v1\n" +
        $"placementId={PlacementId}\n" +
        $"positionId={PositionId}\n" +
        $"positionLabel={PositionLabel}\n" +
        $"editor={EditorX},{EditorY},{EditorZ}\n" +
        $"raw={RawX},{RawY},{RawZ}\n" +
        $"targetTrueIndex={TargetTrueIndex}\n" +
        $"actorId={ActorId}\n" +
        $"yawByte={YawByte}\n" +
        $"supportHandle={SupportHandle}\n" +
        $"supportCollisionTrueIndex={SupportCollisionTrueIndex}\n" +
        $"rowSha256={RowSha256}\n" +
        $"documentCanonicalSha256={DocumentCanonicalSha256}\n" +
        $"documentContentSha256={DocumentContentSha256}\n" +
        "authority=presentation-only|editable=false|nativePersistence=false" +
        "|runtimeObserved=false|runtimeCandidate=false|createBin=false|releasePromotion=false\n"
        : "format=id65-fresh-level-visible-moby-presentation-v2\n" +
        $"mobyKind={Id65RemotePadMobyChoiceCatalog.GetKindId(MobyKind)}\n" +
        $"mobyTypeName={MobyTypeName}\n" +
        $"placementId={PlacementId}\n" +
        $"positionId={PositionId}\n" +
        $"positionLabel={PositionLabel}\n" +
        $"editor={EditorX},{EditorY},{EditorZ}\n" +
        $"raw={RawX},{RawY},{RawZ}\n" +
        $"targetTrueIndex={TargetTrueIndex}\n" +
        $"actorClass={ActorId}\n" +
        "yawByte=null\n" +
        $"supportHandle={SupportHandle}\n" +
        $"supportCollisionTrueIndex={SupportCollisionTrueIndex}\n" +
        $"rowSha256={RowSha256}\n" +
        $"documentCanonicalSha256={DocumentCanonicalSha256}\n" +
        $"documentContentSha256={DocumentContentSha256}\n" +
        "authority=presentation-only|editable=false|nativePersistence=false" +
        "|runtimeObserved=false|runtimeCandidate=false|createBin=false|releasePromotion=false\n";

    private static bool IsCanonicalSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

/// <summary>
/// Read-only presentation result for the exact ID65 fresh-level identity.
/// It is not a BIN patch, a runtime candidate, or export authorization.
/// </summary>
public sealed class Id65FreshLevelSceneProjection
{
    private readonly ReadOnlyCollection<Id65FreshLevelMobyPresentation>
        _visibleMobyPresentations;
    private readonly ReadOnlyCollection<int> _visibleMobyTrueIndexes;
    private readonly ReadOnlyCollection<string> _activeTerrainUnitIds;

    internal Id65FreshLevelSceneProjection(
        GeometryCandidate geometry,
        Id65FreshLevelProtectedSpawn protectedSpawn,
        string canonicalSha256,
        string graphCanonicalSha256,
        string documentCanonicalSha256,
        string documentContentSha256,
        bool structuralTerrainPresent,
        IEnumerable<Id65FreshLevelMobyPresentation> visibleMobyPresentations,
        Id65RemotePadMobyKind? remotePadMobyKind = null,
        string? profileId = null,
        int? schemaVersion = null,
        IEnumerable<string>? activeTerrainUnitIds = null,
        string? terrainSetManifestSha256 = null)
    {
        ArgumentNullException.ThrowIfNull(visibleMobyPresentations);
        Id65FreshLevelMobyPresentation[] presentations =
            visibleMobyPresentations.ToArray();
        string[] terrainUnitIds = activeTerrainUnitIds?.ToArray() ?? [];
        string exactProfileId = profileId ?? Id65FreshLevelSceneProjector.ProfileId;
        int exactSchemaVersion = schemaVersion ?? Id65FreshLevelSceneProjector.SchemaVersion;
        bool terrainSetProjection = exactProfileId ==
            Id65FreshLevelSceneProjector.TerrainSetProfileId &&
            exactSchemaVersion == Id65FreshLevelSceneProjector.TerrainSetSchemaVersion;
        if (presentations.Any(item => item is null) ||
            presentations.Length > 1 ||
            presentations.Select(item => item.TargetTrueIndex).Distinct().Count() !=
                presentations.Length ||
            presentations.Any(item =>
                item.DocumentCanonicalSha256 != documentCanonicalSha256 ||
                item.DocumentContentSha256 != documentContentSha256) ||
            (remotePadMobyKind == Id65RemotePadMobyKind.None && presentations.Length != 0) ||
            ((remotePadMobyKind == Id65RemotePadMobyKind.GrassT107 ||
                remotePadMobyKind == Id65RemotePadMobyKind.RedGemT21) &&
                (presentations.Length != 1 || presentations[0].MobyKind != remotePadMobyKind)))
        {
            throw new InvalidDataException(
                "The fresh-level scene supports zero or one unique, document-bound visible Moby presentation.");
        }
        if (terrainSetProjection)
        {
            Id65AuthoringTerrainSetManifest manifest =
                Id65AuthoringTerrainSetCatalog.Create(terrainUnitIds);
            if (!manifest.ActiveUnits.Select(item => item.Id).SequenceEqual(terrainUnitIds) ||
                !string.Equals(
                    manifest.CanonicalSha256,
                    terrainSetManifestSha256,
                    StringComparison.Ordinal) ||
                structuralTerrainPresent != (terrainUnitIds.Length != 0) ||
                geometry.Name != Id65FreshLevelSceneProjector.TerrainSetGeometryName ||
                !Id65FreshLevelSceneProjector.IsExactProjectionGeometry(geometry) ||
                protectedSpawn != Id65FreshLevelSceneProjector.BuildProtectedSpawn() ||
                graphCanonicalSha256 != Id65FreshLevelSceneProjector
                    .GetTerrainSetGraphCanonicalSha256(terrainUnitIds) ||
                !IsCanonicalSha256(canonicalSha256) ||
                !IsCanonicalSha256(documentCanonicalSha256) ||
                !IsCanonicalSha256(documentContentSha256))
            {
                throw new InvalidDataException(
                    "The terrain-set scene projection requires one exact canonical terrain manifest.");
            }
        }
        else if (exactProfileId != Id65FreshLevelSceneProjector.ProfileId ||
                 exactSchemaVersion != Id65FreshLevelSceneProjector.SchemaVersion ||
                 terrainUnitIds.Length != 0 || terrainSetManifestSha256 is not null)
        {
            throw new InvalidDataException(
                "The fresh-level scene profile/schema or terrain-set identity is unsupported.");
        }

        ProfileId = exactProfileId;
        SchemaVersion = exactSchemaVersion;
        Geometry = geometry;
        ProtectedSpawn = protectedSpawn;
        CanonicalSha256 = canonicalSha256;
        GraphCanonicalSha256 = graphCanonicalSha256;
        DocumentCanonicalSha256 = documentCanonicalSha256;
        DocumentContentSha256 = documentContentSha256;
        StructuralTerrainPresent = structuralTerrainPresent;
        RemotePadMobyKind = remotePadMobyKind;
        TerrainSetManifestSha256 = terrainSetManifestSha256;
        _activeTerrainUnitIds = Array.AsReadOnly(terrainUnitIds);
        _visibleMobyPresentations = Array.AsReadOnly(presentations);
        _visibleMobyTrueIndexes = Array.AsReadOnly(presentations
            .Select(item => item.TargetTrueIndex)
            .ToArray());
        VisibleMobyPresentationCanonicalSha256 = HashVisibleMobyPresentations(presentations);
    }

    public string ProfileId { get; }
    public int SchemaVersion { get; }
    public string LevelKey => UnusedLevel65BlankLevelLabProfileRegistry.Key;
    public int LevelId => UnusedLevel65BlankLevelLabProfileRegistry.LevelId;
    public int SourceWadEntry => UnusedLevel65BlankLevelLabProfileRegistry.DataWadEntry;
    public GeometryCandidate Geometry { get; }
    public Id65FreshLevelProtectedSpawn ProtectedSpawn { get; }
    public string CanonicalSha256 { get; }
    public string GraphCanonicalSha256 { get; }
    public string DocumentCanonicalSha256 { get; }
    public string DocumentContentSha256 { get; }
    public bool StructuralTerrainPresent { get; }
    public Id65RemotePadMobyKind? RemotePadMobyKind { get; }
    public IReadOnlyList<string> ActiveTerrainUnitIds => _activeTerrainUnitIds;
    public string? TerrainSetManifestSha256 { get; }
    public bool UsesTerrainSetProjection =>
        ProfileId == Id65FreshLevelSceneProjector.TerrainSetProfileId;
    public bool CanonicalDocumentBound => IsCanonicalSha256(DocumentCanonicalSha256) &&
        IsCanonicalSha256(DocumentContentSha256);
    public IReadOnlyList<Id65FreshLevelMobyPresentation> VisibleMobyPresentations =>
        _visibleMobyPresentations;
    public IReadOnlyList<int> VisibleMobyTrueIndexes => _visibleMobyTrueIndexes;
    public int VisibleMobyCount => _visibleMobyPresentations.Count;
    public string VisibleMobyPresentationCanonicalSha256 { get; }
    public bool IsPresentationOnly => true;
    public bool NativePersistenceEnabled => false;
    public bool PhysicalMaterializationEnabled => false;
    public bool RuntimeObserved => false;
    public bool WritesWorkspace => false;
    public bool WritesLockedBase => false;
    public bool AuthorizesCreateBin => false;
    public bool AuthorizesRuntimeCandidate => false;
    public bool AuthorizesReleasePromotion => false;

    private static bool IsCanonicalSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string HashVisibleMobyPresentations(
        IReadOnlyList<Id65FreshLevelMobyPresentation> presentations)
    {
        StringBuilder canonical = new(
            "format=id65-fresh-level-visible-moby-presentation-set-v1\n");
        canonical.Append("count=").Append(presentations.Count).Append('\n');
        foreach (Id65FreshLevelMobyPresentation presentation in presentations)
        {
            canonical.Append("moby=").Append(presentation.CanonicalSha256).Append('\n');
        }
        return Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }
}

/// <summary>
/// Builds the small, versioned scene used by the Level Building Editor for the
/// exact ID65 Blank-Level Lab identity. Town Square remains the evidence-bound
/// substrate, but none of its retail sectors or resident Mobys enter this
/// authoring presentation.
/// </summary>
public static class Id65FreshLevelSceneProjector
{
    public const string ProfileId = "id65-fresh-level-authoring-scene-projection-v1";
    public const int SchemaVersion = 1;
    public const string TerrainSetProfileId =
        "id65-fresh-level-authoring-scene-terrain-set-projection-v2";
    public const int TerrainSetSchemaVersion = 2;
    public const string GeometryName = ProfileId + ":unusedlevel65blank";
    public const string TerrainSetGeometryName =
        TerrainSetProfileId + ":unusedlevel65blank";
    public const int AuthoredSectorIndex = 216;
    public const int AuthoredFaceIndex = 0;
    public const int OptionalFaceIndex = 1;
    public const int SecondOptionalFaceIndex = 2;
    public const int StarterTextureId = 66;
    public const int CollisionTriangleIndex = 13_995;
    public const int OptionalCollisionTriangleIndex = 19_808;
    public const int SecondOptionalCollisionTriangleIndex = 19_809;
    public const int OcclusionGroupIndex = 0;
    public const string SectorStableId = "sector.remote-pad.216";
    public const string RenderFaceStableId = "terrain.remote-pad.face.0";
    public const string CollisionStableId = "collision.remote-pad.t13995";
    public const string OcclusionStableId = "occlusion.remote-pad.group0";
    public const string SpawnStableId = "spawn.remote-pad";
    public const string TerrainEditFileName =
        "unusedlevel65blank-fresh-scene-v1-terrain-edits.json";
    public const string ExpectedCanonicalSha256 =
        "12bb0b2efb1a598cb745c12b059b76d247a8031501b4ae8b6ba07debca22b014";
    public const string ExpectedStructuralGraphCanonicalSha256 =
        "17237b0532f6756dd2fea6bedd4a6017a94cdbf4a37c598d8db8f7bb57fdf631";
    public const string ExpectedDefaultBaseDocumentProjectionSha256 =
        "d13d6d3df46e3fc95c5cdc28160d769b50cec131743d830bc680578bccf377bf";
    public const string ExpectedDefaultStructuralDocumentProjectionSha256 =
        "749be61b77986039e41f017145509924f59eb09400147863c7fdba9a5bc55d79";
    public const string ExpectedResidentT25BaseGraphCanonicalSha256 =
        "5a94a4bbda2fe71c5ea9b8a3088ccf2e1ba5f03f0624c5b354b35e55fdfdb5e4";
    public const string ExpectedResidentT25StructuralGraphCanonicalSha256 =
        "218d5df3988d47b1f08b360e580a45e079c8d1203a40301119f2bd50fad68e9f";
    public const string ExpectedResidentT25DefaultBaseDocumentProjectionSha256 =
        "ced56fb1a42873028902a0d68442d95635ef8665f2995da996ae332636ad188a";
    public const string ExpectedResidentT25DefaultStructuralDocumentProjectionSha256 =
        "ee551d307c75face36f44b4400cba7f9317c77adff15a328613fe97e93d36368";
    public const string ExpectedDefaultRemotePadGridOmittedStructuralProjectionSha256 =
        "86c9d386f4edc9a25f3289590f30d7358747a1299034a8fdf1cc2dccef2ef7b6";
    public const string ExpectedDefaultRemotePadGridT107StructuralProjectionSha256 =
        "3abd241e4cca72cc4841b31bcf382ababd29de022cd3f9df41d29f7fe77218b0";
    public const string ExpectedDefaultRemotePadGridT107PresentationSha256 =
        "52e7b4653c2dfe03d37603050ee1041aa6e6758c53ed844fbc673fa0f0e051b1";
    public const string ExpectedTerrainSetZeroGraphCanonicalSha256 =
        "cd3881a7ce2d00be5fbfd5e43c72ef0424bf6f1f994185092690ddc3c6fd3ff6";
    public const string ExpectedTerrainSetTile1GraphCanonicalSha256 =
        "64f20f9841fb6909abcacdbd4acb92ba81ad0b77bc0ad303b3d0b038dad334e5";
    public const string ExpectedTerrainSetTile2GraphCanonicalSha256 =
        "deddaf3901596a3baf717742b470a305c976897560c57f928ff0f44228652b36";
    public const string ExpectedTerrainSetBothGraphCanonicalSha256 =
        "4f342c7f92da94583e7d71fbdffc3bc3aa40b46f6584d2d8b3ec80ee883e06d0";

    private const uint NativeCenterXyWord = 0x01800180;
    private const uint NativeCenterZRadiusFlagsWord = 0x020000C0;
    private const uint NativeXyPositionWord = 0x01000100;
    private const uint NativeZPositionWord = 0x02000000;
    private const uint LowFaceWord0 = 0x00108200;
    private const uint LowFaceWord1 = 0x00108200;
    private const uint HighFaceWord0 = 0x02010000;
    private const uint HighFaceWord1 = 0x02000101;
    private const uint HighFaceWord2 = 0x01400242;
    private const uint HighFaceWord3 = 0x00100A08;
    private const uint CollisionXWord = 0x701C0110;
    private const uint CollisionYWord = 0x00380110;
    private const uint CollisionZWord = 0x00000200;
    private const uint OptionalLowFaceWord0 = 0x04308200;
    private const uint OptionalLowFaceWord1 = 0x00108200;
    private const uint OptionalHighFaceWord0 = 0x02030101;
    private const uint OptionalHighFaceWord1 = 0x02000101;
    private const uint OptionalHighFaceWord2 = 0x01400219;
    private const uint OptionalHighFaceWord3 = 0x00100A08;
    private const uint OptionalCollisionXWord = 0x006401F0;
    private const uint OptionalCollisionYWord = 0x70380110;
    private const uint OptionalCollisionZWord = 0x00000200;
    private const uint Tile2OnlyLowFaceWord0 = 0x0020C300;
    private const uint Tile2BothLowFaceWord0 = 0x00210400;
    private const uint Tile2OnlyHighFaceWord0 = 0x03020000;
    private const uint Tile2BothHighFaceWord0 = 0x04020000;
    private const uint Tile2CollisionXWord = 0x38000110;
    private const uint Tile2CollisionYWord = 0x70380110;
    private const uint Tile2CollisionZWord = 0x00000200;
    private const int OptionalNativeTextureId = 25;

    private const string CanonicalPrefix =
        "format=id65-fresh-level-authoring-scene-graph-v1\n" +
        "profile=id65-fresh-level-authoring-scene-projection-v1\n" +
        "schema=1\n" +
        "level=unusedlevel65blank|65|80\n" +
        "geometry=id65-fresh-level-authoring-scene-projection-v1:unusedlevel65blank" +
        "|bounds=272,272,496,496|z=512,512|staticLp=true" +
        "|lpContract=static-lp-all-sectors-no-occlusion-v1" +
        "|exactHpMaterial=true|hpMaterialContract=raw-words0-3-explicit-word2-word3-material-v2" +
        "|exactHpCoordinates=true|hpCoordinateContract=raw-sector-header-words0-3-integer-world-xz-quarter-residue-v1\n";

    private const string CanonicalBaseUnit =
        "base.hp=216:0:hp|offsets=-1,-1|points=272,272,512;496,272,512;384,496,512" +
        "|vertices=0,0,1,2|colors=1,1,0,2|corners=0,0,1,2" +
        "|words=000001020101000242024001080A1000|texture=66,66,66,66" +
        "|detail=hp|faceColor=124,96,103" +
        "|near=128,98,104;128,98,104;119,93,102;122,95,104" +
        "|far=119,93,102;119,93,102;153,117,107;137,106,106" +
        "|flip=false|depth=1|untextured=false|semiTransparent=false" +
        "|lqBypass=false|hqBypass=false|edited=false\n" +
        "base.lp=216:0:lp|offsets=-1,-1|points=272,272,512;496,272,512;384,496,512" +
        "|vertices=0,1,2,2|corners=0,1,2,2|colors=0,1,2,2" +
        "|cornerColors=45,57,48;48,85,44;55,98,44;55,98,44" +
        "|words=0082100000821000|transitionBias=0|doubleSided=false" +
        "|semiTransparent=false|blendMode=0|orderingTableBias=0\n" +
        "base.collision=13995|words=10011C701001380000020000" +
        "|points=272,272,512;384,496,512;496,272,512|normalZ=-50176|group=0\n";

    private const string CanonicalOptionalUnit =
        "optional.hp=216:1:hp|offsets=-1,-1|points=496,272,512;496,496,512;384,496,512" +
        "|vertices=1,1,3,2|colors=1,1,0,2|corners=0,0,1,2" +
        "|words=010103020101000219024001080A1000|texture=25,25,25,25" +
        "|detail=hp|faceColor=124,96,103" +
        "|near=128,98,104;128,98,104;119,93,102;122,95,104" +
        "|far=119,93,102;119,93,102;153,117,107;137,106,106" +
        "|flip=false|depth=1|untextured=false|semiTransparent=false" +
        "|lqBypass=false|hqBypass=false|edited=false\n" +
        "optional.lp=216:1:lp|offsets=-1,-1|points=496,272,512;496,496,512;384,496,512" +
        "|vertices=1,3,2,2|corners=0,1,2,2|colors=0,1,2,2" +
        "|cornerColors=45,57,48;48,85,44;55,98,44;55,98,44" +
        "|words=0082300400821000|transitionBias=0|doubleSided=false" +
        "|semiTransparent=false|blendMode=0|orderingTableBias=0\n" +
        "optional.collision=19808|words=F00164001001387000020000" +
        "|points=496,272,512;384,496,512;496,496,512|normalZ=-25088|group=0\n";

    private const string CanonicalBaseTopology =
        "graph=base-only\n" +
        "sector=sector.remote-pad.216|216|-1|center=384,384,512|radius=192" +
        "|flags=false,false,false|counts=3,3,1,3,3,1" +
        "|words=80018001C00000020001000100000002|certification=RetailPackedSceneWords\n" +
        "edges=272,272,496,272;496,272,384,496;384,496,272,272\n" +
        "collisionOrder=13995\n";

    private const string CanonicalStructuralTopology =
        "graph=base-plus-optional\n" +
        "sector=sector.remote-pad.216|216|-1|center=384,384,512|radius=192" +
        "|flags=false,false,false|counts=4,3,2,4,3,2" +
        "|words=80018001C00000020001000100000002|certification=RetailPackedSceneWords\n" +
        "edges=272,272,496,272;496,272,384,496;384,496,272,272;" +
        "496,272,496,496;496,496,384,496\n" +
        "collisionOrder=19808,13995\n";

    private const string CanonicalSuffix =
        "occlusion=occlusion.remote-pad.group0|groups=16|group0=216|groups1-15=empty\n" +
        "spawn=spawn.remote-pad|protected=true|T92|landingOffset=0x6B06800" +
        "|playerOffset=0x6B08910|raw=6153,6154,8550,8704,8192|yaw=64|bytes=16\n" +
        "visibleMobys=0\n" +
        "authority=presentation-only|writesWorkspace=false|writesLockedBase=false" +
        "|createBin=false|runtimeCandidate=false|releasePromotion=false\n";

    private const string BaseGraphCanonicalPayload =
        CanonicalPrefix + CanonicalBaseUnit + CanonicalBaseTopology + CanonicalSuffix;
    private const string StructuralGraphCanonicalPayload =
        CanonicalPrefix + CanonicalBaseUnit + CanonicalOptionalUnit +
        CanonicalStructuralTopology + CanonicalSuffix;

    static Id65FreshLevelSceneProjector()
    {
        string baseSha256 = Hash(BaseGraphCanonicalPayload);
        string structuralSha256 = Hash(StructuralGraphCanonicalPayload);
        string residentBaseSha256 = Hash(BaseGraphCanonicalPayload
            .Replace("texture=66,66,66,66", "texture=25,25,25,25", StringComparison.Ordinal)
            .Replace("42024001", "19024001", StringComparison.Ordinal));
        string residentStructuralSha256 = Hash(StructuralGraphCanonicalPayload
            .Replace("texture=66,66,66,66", "texture=25,25,25,25", StringComparison.Ordinal)
            .Replace("42024001", "19024001", StringComparison.Ordinal));
        if (!string.Equals(baseSha256, ExpectedCanonicalSha256, StringComparison.Ordinal) ||
            !string.Equals(
                structuralSha256,
                ExpectedStructuralGraphCanonicalSha256,
                StringComparison.Ordinal) ||
            residentBaseSha256 != ExpectedResidentT25BaseGraphCanonicalSha256 ||
            residentStructuralSha256 != ExpectedResidentT25StructuralGraphCanonicalSha256)
        {
            throw new InvalidDataException(
                "The versioned ID65 fresh-level scene projection canonical graph changed: " +
                $"base={baseSha256}; structural={structuralSha256}.");
        }

        string[] terrainSetGraphHashes = TerrainSetTopologySelections()
            .Select(GetTerrainSetGraphCanonicalSha256)
            .ToArray();
        string[] expectedTerrainSetGraphHashes =
        [
            ExpectedTerrainSetZeroGraphCanonicalSha256,
            ExpectedTerrainSetTile1GraphCanonicalSha256,
            ExpectedTerrainSetTile2GraphCanonicalSha256,
            ExpectedTerrainSetBothGraphCanonicalSha256
        ];
        if (!terrainSetGraphHashes.SequenceEqual(expectedTerrainSetGraphHashes))
        {
            throw new InvalidDataException(
                "The versioned schema-5 ID65 terrain-set presentation graph changed: " +
                string.Join(',', terrainSetGraphHashes));
        }

        ValidateFrozenAuthoringIntent();
    }

    internal static string GetTerrainSetGraphCanonicalSha256(
        IEnumerable<string> activeTerrainUnitIds)
    {
        ArgumentNullException.ThrowIfNull(activeTerrainUnitIds);
        string[] requested = activeTerrainUnitIds.ToArray();
        Id65AuthoringTerrainSetManifest manifest =
            Id65AuthoringTerrainSetCatalog.Create(requested);
        if (!manifest.ActiveUnits.Select(item => item.Id).SequenceEqual(requested))
        {
            throw new InvalidDataException(
                "The fresh-scene terrain-set topology IDs must use canonical catalog order.");
        }

        bool tile1 = requested.Contains(
            Id65AuthoringTerrainSetCatalog.Tile1Id,
            StringComparer.Ordinal);
        bool tile2 = requested.Contains(
            Id65AuthoringTerrainSetCatalog.Tile2Id,
            StringComparer.Ordinal);
        int faceCount = 1 + requested.Length;
        int vertexCount = 3 + requested.Length;
        StringBuilder canonical = new(
            "format=id65-fresh-level-authoring-scene-terrain-set-graph-v2\n");
        canonical.Append("profile=").Append(TerrainSetProfileId).Append('\n');
        canonical.Append("schema=").Append(TerrainSetSchemaVersion).Append('\n');
        canonical.Append("level=unusedlevel65blank|65|80\n");
        canonical.Append("legacyBaseGraphCanonicalSha256=")
            .Append(ExpectedCanonicalSha256).Append('\n');
        canonical.Append("terrainCatalogProfileId=")
            .Append(Id65AuthoringTerrainSetCatalog.ProfileId).Append('\n');
        canonical.Append("terrainCatalogSchemaVersion=")
            .Append(Id65AuthoringTerrainSetCatalog.SchemaVersion).Append('\n');
        canonical.Append("terrainSetManifestSha256=")
            .Append(manifest.CanonicalSha256).Append('\n');
        canonical.Append("activeUnitCount=").Append(requested.Length).Append('\n');
        foreach (string id in requested)
            canonical.Append("activeUnitId=").Append(id).Append('\n');
        canonical.Append("geometry=").Append(TerrainSetGeometryName)
            .Append("|bounds=272,272,496,496|z=512,512")
            .Append("|sector=216|vertices=").Append(vertexCount)
            .Append("|faces=").Append(faceCount).Append('\n');
        canonical.Append("baseFace=0|hp=000001020101000242024001080A1000")
            .Append("|lp=0082100000821000|collision=13995:")
            .Append("10011C701001380000020000\n");
        if (tile1)
        {
            canonical.Append("terrainUnit=")
                .Append(Id65AuthoringTerrainSetCatalog.Tile1Id)
                .Append("|face=1|vertex=496,496,512")
                .Append("|hp=010103020101000219024001080A1000")
                .Append("|lp=0082300400821000|collision=19808:")
                .Append("F00164001001387000020000\n");
        }
        if (tile2)
        {
            canonical.Append("terrainUnit=")
                .Append(Id65AuthoringTerrainSetCatalog.Tile2Id)
                .Append("|face=").Append(tile1 ? 2 : 1)
                .Append("|vertex=272,496,512|hp=")
                .Append(tile1
                    ? "000002040101000219024001080A1000"
                    : "000002030101000219024001080A1000")
                .Append("|lp=")
                .Append(tile1 ? "0004210000821000" : "00C3200000821000")
                .Append("|collision=")
                .Append(tile1
                    ? SecondOptionalCollisionTriangleIndex
                    : OptionalCollisionTriangleIndex)
                .Append(":100100381001387000020000\n");
        }
        canonical.Append("collisionOrder=");
        if (tile2)
        {
            canonical.Append(tile1
                ? SecondOptionalCollisionTriangleIndex
                : OptionalCollisionTriangleIndex).Append(',');
        }
        if (tile1)
            canonical.Append(OptionalCollisionTriangleIndex).Append(',');
        canonical.Append(CollisionTriangleIndex).Append('\n');
        canonical.Append("occlusion=group0:216|groups1-15=empty\n");
        canonical.Append("spawn=spawn.remote-pad|protected=true|T92\n");
        canonical.Append(
            "authority=presentation-only|writesWorkspace=false|writesLockedBase=false" +
            "|nativePersistence=false|physicalMaterialization=false|createBin=false" +
            "|runtimeCandidate=false|releasePromotion=false\n");
        return Hash(canonical.ToString());
    }

    public static Id65FreshLevelSceneProjection ProjectExact(LevelDefinition level)
    {
        if (!UnusedLevel65BlankLevelLabProfileRegistry.IsLabLevel(level))
        {
            throw new InvalidOperationException(
                "The fresh-level scene projection accepts only the exact ID65 Blank-Level Lab definition.");
        }

        GeometryCandidate geometry = BuildGeometry(includeStructuralTerrain: false, StarterTextureId);
        Id65FreshLevelProtectedSpawn spawn = BuildProtectedSpawn();
        ValidateProjectedGraph(geometry, spawn, includeStructuralTerrain: false, StarterTextureId);
        return new Id65FreshLevelSceneProjection(
            geometry,
            spawn,
            ExpectedCanonicalSha256,
            ExpectedCanonicalSha256,
            "",
            "",
            structuralTerrainPresent: false,
            visibleMobyPresentations: []);
    }

    public static Id65FreshLevelSceneProjection ProjectExact(
        LevelDefinition level,
        Id65AuthoringEditorDocumentReadModel document)
    {
        if (!UnusedLevel65BlankLevelLabProfileRegistry.IsLabLevel(level))
        {
            throw new InvalidOperationException(
                "The fresh-level scene projection accepts only the exact ID65 Blank-Level Lab definition.");
        }
        ArgumentNullException.ThrowIfNull(document);
        if (document.UsesRemotePadTerrainSet)
            return ProjectTerrainSetExact(document);
        bool remotePadGrid = document.UsesRemotePadT107Grid;
        bool remotePadMobyChoice = document.UsesRemotePadMobyChoice;
        bool includeStructuralTerrain;
        int mandatoryBaseFaceTextureId;
        Id65FreshLevelMobyPresentation[] visibleMobyPresentations;
        if (remotePadMobyChoice)
        {
            visibleMobyPresentations = BuildRemotePadMobyChoicePresentations(
                document,
                out includeStructuralTerrain,
                out mandatoryBaseFaceTextureId);
        }
        else if (remotePadGrid)
        {
            visibleMobyPresentations = BuildRemotePadGridMobyPresentations(
                document,
                out includeStructuralTerrain,
                out mandatoryBaseFaceTextureId);
        }
        else
        {
            Id65AuthoringEditorControlState state =
                Id65AuthoringEditorControlStateCompiler.RecoverControlState(document);
            includeStructuralTerrain = state.IncludeStructuralTerrain;
            mandatoryBaseFaceTextureId = state.MandatoryBaseFaceTextureId;
            visibleMobyPresentations = [];
        }
        GeometryCandidate geometry = BuildGeometry(
            includeStructuralTerrain,
            mandatoryBaseFaceTextureId);
        Id65FreshLevelProtectedSpawn spawn = BuildProtectedSpawn();
        ValidateProjectedGraph(
            geometry,
            spawn,
            includeStructuralTerrain,
            mandatoryBaseFaceTextureId);
        string graphCanonicalSha256 = mandatoryBaseFaceTextureId == 66
            ? (includeStructuralTerrain
                ? ExpectedStructuralGraphCanonicalSha256
                : ExpectedCanonicalSha256)
            : (includeStructuralTerrain
                ? ExpectedResidentT25StructuralGraphCanonicalSha256
                : ExpectedResidentT25BaseGraphCanonicalSha256);
        string identity = remotePadMobyChoice
            ? BindRemotePadMobyChoiceDocumentIdentity(
                graphCanonicalSha256,
                document.DocumentCanonicalSha256,
                document.DocumentContentSha256,
                includeStructuralTerrain,
                document.RemotePadMobyKind!.Value,
                visibleMobyPresentations)
            : remotePadGrid
            ? BindRemotePadGridDocumentIdentity(
                graphCanonicalSha256,
                document.DocumentCanonicalSha256,
                document.DocumentContentSha256,
                includeStructuralTerrain,
                visibleMobyPresentations)
            : BindDocumentIdentity(
                graphCanonicalSha256,
                document.DocumentCanonicalSha256,
                document.DocumentContentSha256,
                includeStructuralTerrain);
        return new Id65FreshLevelSceneProjection(
            geometry,
            spawn,
            identity,
            graphCanonicalSha256,
            document.DocumentCanonicalSha256,
            document.DocumentContentSha256,
            includeStructuralTerrain,
            visibleMobyPresentations,
            document.RemotePadMobyKind);
    }

    private static Id65FreshLevelSceneProjection ProjectTerrainSetExact(
        Id65AuthoringEditorDocumentReadModel document)
    {
        Id65AuthoringEditorControlState state =
            Id65AuthoringEditorControlStateCompiler.RecoverControlState(document);
        if (!state.UsesRemotePadTerrainSet ||
            document.SchemaVersion !=
                Id65AuthoringEditorDocument.RemotePadMobyChoiceTerrainSetSchemaVersion ||
            document.ProfileId !=
                Id65AuthoringEditorDocument.RemotePadMobyChoiceTerrainSetDocumentProfileId ||
            document.MandatoryBaseFaceTextureId != StarterTextureId ||
            document.TerrainSetManifestSha256 is not string manifestSha256)
        {
            throw new InvalidDataException(
                "The schema-5 ID65 terrain-set document cannot enter fresh-scene presentation.");
        }

        string[] activeUnitIds =
        [
            .. state.IncludeTerrainTile1
                ? [Id65AuthoringTerrainSetCatalog.Tile1Id]
                : Array.Empty<string>(),
            .. state.IncludeTerrainTile2
                ? [Id65AuthoringTerrainSetCatalog.Tile2Id]
                : Array.Empty<string>()
        ];
        Id65AuthoringTerrainSetManifest manifest =
            Id65AuthoringTerrainSetCatalog.Create(activeUnitIds);
        if (!document.ActiveTerrainUnitIds.SequenceEqual(activeUnitIds) ||
            document.Terrain.Count != activeUnitIds.Length ||
            !string.Equals(
                document.TerrainSetManifestSha256,
                manifest.CanonicalSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The schema-5 ID65 terrain-set readback is not its exact canonical selection.");
        }

        Id65FreshLevelMobyPresentation[] visibleMobyPresentations =
            BuildRemotePadMobyChoicePresentations(
                document,
                out _,
                out int mandatoryBaseFaceTextureId);
        if (mandatoryBaseFaceTextureId != StarterTextureId ||
            !state.RemotePadMobyKind.HasValue)
        {
            throw new InvalidDataException(
                "The schema-5 terrain-set scene requires one exact T66 Moby choice.");
        }

        GeometryCandidate geometry = BuildTerrainSetGeometry(manifest);
        Id65FreshLevelProtectedSpawn spawn = BuildProtectedSpawn();
        ValidateTerrainSetProjectedGraph(geometry, spawn, manifest);
        string graphCanonicalSha256 =
            GetTerrainSetGraphCanonicalSha256(activeUnitIds);
        string identity = BindRemotePadMobyChoiceTerrainSetDocumentIdentity(
            graphCanonicalSha256,
            document.DocumentCanonicalSha256,
            document.DocumentContentSha256,
            manifest,
            state.RemotePadMobyKind.Value,
            visibleMobyPresentations);
        return new Id65FreshLevelSceneProjection(
            geometry,
            spawn,
            identity,
            graphCanonicalSha256,
            document.DocumentCanonicalSha256,
            document.DocumentContentSha256,
            structuralTerrainPresent: activeUnitIds.Length != 0,
            visibleMobyPresentations,
            state.RemotePadMobyKind,
            TerrainSetProfileId,
            TerrainSetSchemaVersion,
            activeUnitIds,
            manifestSha256);
    }

    private static Id65FreshLevelMobyPresentation[]
        BuildRemotePadMobyChoicePresentations(
            Id65AuthoringEditorDocumentReadModel document,
            out bool includeStructuralTerrain,
            out int mandatoryBaseFaceTextureId)
    {
        Id65AuthoringEditorControlState state =
            Id65AuthoringEditorControlStateCompiler.RecoverControlState(document);
        includeStructuralTerrain = state.IncludeStructuralTerrain;
        mandatoryBaseFaceTextureId = state.MandatoryBaseFaceTextureId;
        if (!state.UsesRemotePadMobyChoice ||
            !state.RemotePadMobyKind.HasValue ||
            state.RemotePadMobyPositionId is null ||
            document.RemotePadMobyPosition is null ||
            mandatoryBaseFaceTextureId != Id65AuthoringEditorDocument.PrivateMandatoryBaseFaceTextureId)
        {
            throw new InvalidDataException(
                "The schema-4 ID65 document cannot enter fresh-scene presentation.");
        }

        Id65RemotePadGrassT107Position sharedPosition =
            Id65RemotePadGrassT107Catalog.GetRequiredPosition(state.RemotePadMobyPositionId);
        RequireExactRemotePadMobyPosition(
            document.RemotePadMobyPosition,
            state.RemotePadMobyKind.Value,
            sharedPosition);

        if (state.RemotePadMobyKind == Id65RemotePadMobyKind.None)
        {
            if (document.MobyPlacements.Count != 0)
                throw new InvalidDataException("Schema-4 None must project no visible Moby.");
            return [];
        }
        if (document.MobyPlacements.Count != 1)
        {
            throw new InvalidDataException(
                "Schema-4 Grass or Red Gem must project exactly one visible Moby.");
        }

        Id65AuthoringEditorMobyPlacementReadModel placement = document.MobyPlacements[0];
        if (state.RemotePadMobyKind == Id65RemotePadMobyKind.GrassT107)
        {
            RequireExactRemotePadPlacement(placement, sharedPosition);
            return
            [
                new Id65FreshLevelMobyPresentation(
                    sharedPosition,
                    document.DocumentCanonicalSha256,
                    document.DocumentContentSha256)
            ];
        }

        Id65RemotePadRedGemT21Position redGemPosition =
            Id65RemotePadRedGemT21Catalog.GetRequiredPosition(sharedPosition.PositionId);
        RequireExactRemotePadRedGemPlacement(placement, redGemPosition);
        return
        [
            new Id65FreshLevelMobyPresentation(
                redGemPosition,
                document.DocumentCanonicalSha256,
                document.DocumentContentSha256)
        ];
    }

    private static Id65FreshLevelMobyPresentation[] BuildRemotePadGridMobyPresentations(
        Id65AuthoringEditorDocumentReadModel document,
        out bool includeStructuralTerrain,
        out int mandatoryBaseFaceTextureId)
    {
        includeStructuralTerrain = document.Terrain.Count == 1;
        mandatoryBaseFaceTextureId = document.MandatoryBaseFaceTextureId;
        if (document.SchemaVersion != Id65AuthoringEditorDocument.RemotePadGridSchemaVersion ||
            document.ProfileId != Id65AuthoringEditorDocument.RemotePadGridDocumentProfileId ||
            document.T107PlacementSemantics !=
                Id65AuthoringEditorT107PlacementSemantics.RemotePadGridV1 ||
            document.T107RemotePadPositionId is null ||
            document.T107RemotePadPosition is null ||
            document.Terrain.Count > 1 ||
            mandatoryBaseFaceTextureId is not
                (Id65AuthoringEditorDocument.PrivateMandatoryBaseFaceTextureId or
                 Id65AuthoringEditorDocument.ResidentMandatoryBaseFaceTextureId) ||
            !document.EnvelopeCanonicalIdentityVerified ||
            !document.DocumentCanonicalIdentityVerified ||
            !document.PersistedProjectionIdentityPresent ||
            document.CurrentProjectionRevalidated ||
            document.CurrentLockedSourceReprojected ||
            document.CurrentOnDiskFreshnessVerified ||
            document.ContainsGameBytes || document.ContainsNativePayloadBytes ||
            document.ContainsPhysicalAddresses || document.ContainsFilePaths ||
            document.ContainsCompiledAfterimage || document.ContainsPublisherState ||
            document.PersistenceEnabled || document.FileSystemMutationPerformed ||
            document.WritesFileSystem || document.WritesDiscImage || document.WritesCue ||
            document.CarriesAppOrUiAuthority || document.CreateBinEnabled ||
            document.NormalCreateBinEnabled || document.RuntimeCandidateAuthorized ||
            document.RuntimeProven || document.ReleaseIntegrated ||
            document.PromotionAuthorized || document.Publishable)
        {
            throw new InvalidDataException(
                "The schema-3 ID65 document cannot enter fresh-scene presentation.");
        }

        Id65RemotePadGrassT107Position position;
        try
        {
            position = Id65RemotePadGrassT107Catalog.GetRequiredPosition(
                document.T107RemotePadPositionId);
        }
        catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException)
        {
            throw new InvalidDataException(
                "The schema-3 ID65 document carries an unknown remote-pad position.",
                exception);
        }

        RequireExactRemotePadPosition(document.T107RemotePadPosition, position);

        string[] placementIds = document.MobyPlacements
            .Select(item => item.Id)
            .ToArray();
        Id65AuthoringEditorDocument expectedDocument;
        try
        {
            expectedDocument = Id65AuthoringEditorDocument.CreateRemotePadGridExact(
                position.PositionId,
                includeStructuralTerrain,
                placementIds,
                mandatoryBaseFaceTextureId);
        }
        catch (Exception exception) when (exception is
            InvalidDataException or ArgumentException or KeyNotFoundException)
        {
            throw new InvalidDataException(
                "The schema-3 ID65 read model does not describe one exact remote-pad document.",
                exception);
        }

        if (document.DocumentCanonicalSha256 != expectedDocument.CanonicalSha256 ||
            document.DocumentContentSha256 != expectedDocument.ContentSha256)
        {
            throw new InvalidDataException(
                "The schema-3 ID65 read model semantics do not match its document hashes.");
        }

        Id65AuthoringEditorMobyPlacementReadModel[] remotePlacements =
            document.MobyPlacements
                .Where(item => item.Id == Id65RemotePadGrassT107Catalog.PlacementId)
                .ToArray();
        if (remotePlacements.Length == 0)
        {
            if (document.MobyPlacements.Any(item =>
                    item.TargetTrueIndex == Id65RemotePadGrassT107Catalog.TargetTrueIndex))
            {
                throw new InvalidDataException(
                    "The schema-3 ID65 document aliases T107 through a non-grid placement.");
            }
            return [];
        }
        if (remotePlacements.Length != 1)
        {
            throw new InvalidDataException(
                "The schema-3 ID65 document contains more than one remote-pad T107 placement.");
        }

        RequireExactRemotePadPlacement(remotePlacements[0], position);
        return
        [
            new Id65FreshLevelMobyPresentation(
                position,
                document.DocumentCanonicalSha256,
                document.DocumentContentSha256)
        ];
    }

    private static void RequireExactRemotePadPosition(
        Id65RemotePadGrassT107PositionReadModel actual,
        Id65RemotePadGrassT107Position expected)
    {
        if (actual.Ordinal != expected.Ordinal ||
            actual.PositionId != expected.PositionId ||
            actual.Label != expected.Label ||
            actual.EditorX != expected.EditorX || actual.EditorY != expected.EditorY ||
            actual.EditorZ != expected.EditorZ || actual.RawX != expected.RawX ||
            actual.RawY != expected.RawY || actual.RawZ != expected.RawZ ||
            actual.RowSha256 != expected.RowSha256 || actual.IsDefault != expected.IsDefault)
        {
            throw new InvalidDataException(
                "The schema-3 ID65 remote-pad position read model does not match the frozen catalog.");
        }
    }

    private static void RequireExactRemotePadPlacement(
        Id65AuthoringEditorMobyPlacementReadModel actual,
        Id65RemotePadGrassT107Position position)
    {
        if (actual.Id != Id65RemotePadGrassT107Catalog.PlacementId ||
            actual.AtomicGroupId != Id65RemotePadGrassT107Catalog.AtomicGroupId ||
            actual.BundleId != Id65RemotePadGrassT107Catalog.BundleId ||
            actual.RowTemplateId != Id65RemotePadGrassT107Catalog.RowTemplateId ||
            actual.RowPolicy != Id65AuthoringEditorDocument.AppendContiguousRowPolicy ||
            actual.TargetTrueIndex != Id65RemotePadGrassT107Catalog.TargetTrueIndex ||
            actual.CoordinatePolicy != Id65AuthoringEditorDocument.AuthorExactCoordinatePolicy ||
            actual.CoordinatesOrDelta is not { } coordinates ||
            coordinates.X != position.RawX || coordinates.Y != position.RawY ||
            coordinates.Z != position.RawZ ||
            actual.YawByte != Id65RemotePadGrassT107Catalog.YawByte ||
            actual.SupportHandle != Id65RemotePadGrassT107Catalog.SupportHandle)
        {
            throw new InvalidDataException(
                "The schema-3 ID65 T107 placement does not match its selected catalog position.");
        }
    }

    private static void RequireExactRemotePadMobyPosition(
        Id65RemotePadMobyPositionReadModel actual,
        Id65RemotePadMobyKind kind,
        Id65RemotePadGrassT107Position expected)
    {
        string? expectedRowSha256 = Id65RemotePadMobyChoiceCatalog
            .GetSelectedExactRowSha256(kind, expected.PositionId);
        if (actual.Ordinal != expected.Ordinal || actual.PositionId != expected.PositionId ||
            actual.Label != expected.Label || actual.EditorX != expected.EditorX ||
            actual.EditorY != expected.EditorY || actual.EditorZ != expected.EditorZ ||
            actual.RawX != expected.RawX || actual.RawY != expected.RawY ||
            actual.RawZ != expected.RawZ ||
            actual.SelectedExactRowSha256 != expectedRowSha256 ||
            actual.IsDefault != expected.IsDefault)
        {
            throw new InvalidDataException(
                "The schema-4 ID65 shared-grid readback does not match its exact Moby choice.");
        }
    }

    private static void RequireExactRemotePadRedGemPlacement(
        Id65AuthoringEditorMobyPlacementReadModel actual,
        Id65RemotePadRedGemT21Position position)
    {
        if (actual.Id != Id65RemotePadRedGemT21Catalog.PlacementId ||
            actual.AtomicGroupId != Id65RemotePadRedGemT21Catalog.AtomicGroupId ||
            actual.BundleId != Id65RemotePadRedGemT21Catalog.BundleId ||
            actual.RowTemplateId != Id65RemotePadRedGemT21Catalog.RowTemplateId ||
            actual.RowPolicy != Id65AuthoringEditorDocument.ReuseResidentExactRowPolicy ||
            actual.TargetTrueIndex != Id65RemotePadRedGemT21Catalog.TargetTrueIndex ||
            actual.CoordinatePolicy != Id65AuthoringEditorDocument.AuthorExactCoordinatePolicy ||
            actual.CoordinatesOrDelta is not { } coordinates ||
            coordinates.X != position.RawX || coordinates.Y != position.RawY ||
            coordinates.Z != position.RawZ || actual.YawByte.HasValue ||
            actual.SupportHandle != Id65RemotePadRedGemT21Catalog.SupportHandle)
        {
            throw new InvalidDataException(
                "The schema-4 ID65 Red Gem placement does not match its selected exact row.");
        }
    }

    internal static Id65FreshLevelProtectedSpawn BuildProtectedSpawn() => new(
            SpawnStableId,
            UnusedLevel65FullAuthoringConstructionTemplate.PlayerAnchorTrueIndex,
            UnusedLevel65FullAuthoringConstructionTemplate.LandingWadOffset,
            UnusedLevel65FullAuthoringConstructionTemplate.PlayerAnchorWadOffset,
            UnusedLevel65RemoteBlankIsolationConstruction.AuthoredRawX,
            UnusedLevel65RemoteBlankIsolationConstruction.AuthoredRawY,
            UnusedLevel65RemoteBlankIsolationConstruction.LandingRawZ,
            UnusedLevel65RemoteBlankIsolationConstruction.PlayerRawZ,
            UnusedLevel65RemoteBlankIsolationConstruction.GroundRawZ,
            UnusedLevel65RemoteBlankIsolationConstruction.YawByte,
            UnusedLevel65FullAuthoringConstructionTemplate.LandingByteLength);

    public static bool IsProjectionGeometry(GeometryCandidate? geometry) =>
        geometry != null &&
        (string.Equals(geometry.Name, GeometryName, StringComparison.Ordinal) ||
         string.Equals(geometry.Name, TerrainSetGeometryName, StringComparison.Ordinal));

    public static bool IsExactProjectionGeometry(GeometryCandidate? geometry)
    {
        if (geometry == null)
            return false;
        try
        {
            if (geometry.Polygons.Any(item => item is null) ||
                geometry.LowDetailPolygons.Any(item => item is null) ||
                geometry.SourceSectors.Any(item => item is null) ||
                geometry.NativeTerrainOcclusion is { } nativeOcclusion &&
                (nativeOcclusion.CollisionTriangles.Any(item => item is null) ||
                 nativeOcclusion.EnvironmentGroups.Any(group => group is null)))
            {
                return false;
            }

            if (string.Equals(
                    geometry.Name,
                    TerrainSetGeometryName,
                    StringComparison.Ordinal))
            {
                foreach (string[] activeUnitIds in TerrainSetTopologySelections())
                {
                    try
                    {
                        ValidateTerrainSetProjectedGraph(
                            geometry,
                            BuildProtectedSpawn(),
                            Id65AuthoringTerrainSetCatalog.Create(activeUnitIds));
                        return true;
                    }
                    catch (Exception exception) when (IsMalformedGraphException(exception))
                    {
                    }
                }
                return false;
            }

            bool optional = geometry.Polygons.Count == 2;
            if (geometry.Polygons.Count is not (1 or 2))
                return false;
            int textureId = geometry.Polygons.Single(item =>
                item.FaceIndex == AuthoredFaceIndex).TextureId;
            ValidateProjectedGraph(geometry, BuildProtectedSpawn(), optional, textureId);
            return true;
        }
        catch (Exception exception) when (IsMalformedGraphException(exception))
        {
            return false;
        }
    }

    private static IReadOnlyList<string[]> TerrainSetTopologySelections() =>
    [
        [],
        [Id65AuthoringTerrainSetCatalog.Tile1Id],
        [Id65AuthoringTerrainSetCatalog.Tile2Id],
        [Id65AuthoringTerrainSetCatalog.Tile1Id, Id65AuthoringTerrainSetCatalog.Tile2Id]
    ];

    private static GeometryCandidate BuildGeometry(
        bool includeStructuralTerrain,
        int mandatoryBaseFaceTextureId) =>
        BuildGeometry(
            includeTile1: includeStructuralTerrain,
            includeTile2: false,
            mandatoryBaseFaceTextureId,
            GeometryName);

    private static GeometryCandidate BuildTerrainSetGeometry(
        Id65AuthoringTerrainSetManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        string[] activeUnitIds = manifest.ActiveUnits.Select(item => item.Id).ToArray();
        return BuildGeometry(
            activeUnitIds.Contains(Id65AuthoringTerrainSetCatalog.Tile1Id, StringComparer.Ordinal),
            activeUnitIds.Contains(Id65AuthoringTerrainSetCatalog.Tile2Id, StringComparer.Ordinal),
            StarterTextureId,
            TerrainSetGeometryName);
    }

    private static GeometryCandidate BuildGeometry(
        bool includeTile1,
        bool includeTile2,
        int mandatoryBaseFaceTextureId,
        string geometryName)
    {
        if (mandatoryBaseFaceTextureId is not (25 or 66))
            throw new InvalidDataException(
                "The fresh ID65 mandatory face texture must be exactly T25 or T66.");
        Vector2f[] points =
        [
            new(272, 272),
            new(496, 272),
            new(384, 496)
        ];
        float[] zValues = [512, 512, 512];
        int[] highVertexIndexes = [0, 0, 1, 2];
        int[] highColorIndexes = [1, 1, 0, 2];
        int[] highCornerPointIndexes = [0, 0, 1, 2];
        ColorRgba[] farColors = ExpectedFarColors();
        ColorRgba[] nearColors = ExpectedNearColors();

        GeometryCandidate geometry = new()
        {
            Name = geometryName,
            Bounds = Rect2f.FromBounds(272, 272, 496, 496),
            MinZ = 512,
            MaxZ = 512,
            HasStaticLowDetailPreview = true,
            TerrainLodPreviewContract = SourceSceneOverlayContract.TerrainLodPreview,
            HasExactHighPolyMaterialState = true,
            HighPolyMaterialContract = SourceSceneOverlayContract.HpFaceMaterialPayload,
            HasExactHighPolyCoordinateState = true,
            HighPolyCoordinateContract = NativeTerrainHighPolyCoordinates.Contract
        };

        geometry.Polygons.Add(new TerrainPolygon(
            points,
            zValues,
            mandatoryBaseFaceTextureId,
            AuthoredSectorIndex,
            AuthoredFaceIndex,
            "hp",
            ColorRgba.FromRgb(124, 96, 103),
            sectorOffset: -1,
            faceOffset: -1,
            vertexIndexes: highVertexIndexes,
            word3: mandatoryBaseFaceTextureId == 66 ? "0x01400242" : "0x01400219",
            word4: "0x00100A08",
            faceFlip: false,
            faceDepth: 1,
            colourIndexes: highColorIndexes,
            nearColors: nearColors,
            farColors: farColors,
            cornerPointIndexes: highCornerPointIndexes,
            nativeFaceWord2: mandatoryBaseFaceTextureId == 66
                ? HighFaceWord2
                : OptionalHighFaceWord2,
            nativeMaterialByte: mandatoryBaseFaceTextureId,
            nativeUntexturedSentinel: false,
            nativePrimitiveSemiTransparent: false,
            nativeTextureId: mandatoryBaseFaceTextureId,
            nativeFaceWord0: HighFaceWord0,
            nativeFaceWord1: HighFaceWord1,
            nativeFaceWord3: HighFaceWord3));

        int[] lowIndexes = [0, 1, 2, 2];
        ColorRgba[] lowColors = ExpectedLowColors();
        geometry.LowDetailPolygons.Add(new LowDetailTerrainPolygon(
            points,
            zValues,
            AuthoredSectorIndex,
            AuthoredFaceIndex,
            sectorOffset: -1,
            faceOffset: -1,
            vertexIndexes: lowIndexes,
            cornerPointIndexes: lowIndexes,
            colorIndexes: lowIndexes,
            cornerColors: lowColors,
            rawWord0: LowFaceWord0,
            rawWord1: LowFaceWord1,
            transitionBias: 0,
            doubleSided: false,
            semiTransparent: false,
            blendMode: 0,
            orderingTableBias: 0));

        if (includeTile1)
        {
            Vector2f[] optionalPoints =
            [
                new(496, 272),
                new(496, 496),
                new(384, 496)
            ];
            float[] optionalZ = [512, 512, 512];
            int[] optionalHighVertexIndexes = [1, 1, 3, 2];
            int[] optionalHighColorIndexes = [1, 1, 0, 2];
            geometry.Polygons.Add(new TerrainPolygon(
                optionalPoints,
                optionalZ,
                OptionalNativeTextureId,
                AuthoredSectorIndex,
                OptionalFaceIndex,
                "hp",
                ColorRgba.FromRgb(124, 96, 103),
                sectorOffset: -1,
                faceOffset: -1,
                vertexIndexes: optionalHighVertexIndexes,
                word3: "0x01400219",
                word4: "0x00100A08",
                faceFlip: false,
                faceDepth: 1,
                colourIndexes: optionalHighColorIndexes,
                nearColors: nearColors,
                farColors: farColors,
                cornerPointIndexes: [0, 0, 1, 2],
                nativeFaceWord2: OptionalHighFaceWord2,
                nativeMaterialByte: OptionalNativeTextureId,
                nativeUntexturedSentinel: false,
                nativePrimitiveSemiTransparent: false,
                nativeTextureId: OptionalNativeTextureId,
                nativeFaceWord0: OptionalHighFaceWord0,
                nativeFaceWord1: OptionalHighFaceWord1,
                nativeFaceWord3: OptionalHighFaceWord3));

            int[] optionalLowIndexes = [1, 3, 2, 2];
            geometry.LowDetailPolygons.Add(new LowDetailTerrainPolygon(
                optionalPoints,
                optionalZ,
                AuthoredSectorIndex,
                OptionalFaceIndex,
                sectorOffset: -1,
                faceOffset: -1,
                vertexIndexes: optionalLowIndexes,
                cornerPointIndexes: [0, 1, 2, 2],
                colorIndexes: [0, 1, 2, 2],
                cornerColors: lowColors,
                rawWord0: OptionalLowFaceWord0,
                rawWord1: OptionalLowFaceWord1,
                transitionBias: 0,
                doubleSided: false,
                semiTransparent: false,
                blendMode: 0,
                orderingTableBias: 0));
        }

        if (includeTile2)
        {
            Vector2f[] tile2Points =
            [
                new(272, 272),
                new(384, 496),
                new(272, 496)
            ];
            float[] tile2Z = [512, 512, 512];
            int faceIndex = includeTile1 ? SecondOptionalFaceIndex : OptionalFaceIndex;
            int addedVertexIndex = includeTile1 ? 4 : 3;
            geometry.Polygons.Add(new TerrainPolygon(
                tile2Points,
                tile2Z,
                OptionalNativeTextureId,
                AuthoredSectorIndex,
                faceIndex,
                "hp",
                ColorRgba.FromRgb(124, 96, 103),
                sectorOffset: -1,
                faceOffset: -1,
                vertexIndexes: [0, 0, 2, addedVertexIndex],
                word3: "0x01400219",
                word4: "0x00100A08",
                faceFlip: false,
                faceDepth: 1,
                colourIndexes: [1, 1, 0, 2],
                nearColors: nearColors,
                farColors: farColors,
                cornerPointIndexes: [0, 0, 1, 2],
                nativeFaceWord2: OptionalHighFaceWord2,
                nativeMaterialByte: OptionalNativeTextureId,
                nativeUntexturedSentinel: false,
                nativePrimitiveSemiTransparent: false,
                nativeTextureId: OptionalNativeTextureId,
                nativeFaceWord0: includeTile1
                    ? Tile2BothHighFaceWord0
                    : Tile2OnlyHighFaceWord0,
                nativeFaceWord1: OptionalHighFaceWord1,
                nativeFaceWord3: OptionalHighFaceWord3));

            geometry.LowDetailPolygons.Add(new LowDetailTerrainPolygon(
                tile2Points,
                tile2Z,
                AuthoredSectorIndex,
                faceIndex,
                sectorOffset: -1,
                faceOffset: -1,
                vertexIndexes: [0, 2, addedVertexIndex, addedVertexIndex],
                cornerPointIndexes: [0, 1, 2, 2],
                colorIndexes: [0, 1, 2, 2],
                cornerColors: lowColors,
                rawWord0: includeTile1 ? Tile2BothLowFaceWord0 : Tile2OnlyLowFaceWord0,
                rawWord1: OptionalLowFaceWord1,
                transitionBias: 0,
                doubleSided: false,
                semiTransparent: false,
                blendMode: 0,
                orderingTableBias: 0));
        }

        geometry.Edges.Add(new TerrainEdge(272, 272, 496, 272));
        geometry.Edges.Add(new TerrainEdge(496, 272, 384, 496));
        geometry.Edges.Add(new TerrainEdge(384, 496, 272, 272));
        if (includeTile1)
        {
            geometry.Edges.Add(new TerrainEdge(496, 272, 496, 496));
            geometry.Edges.Add(new TerrainEdge(496, 496, 384, 496));
        }
        if (includeTile2)
        {
            geometry.Edges.Add(new TerrainEdge(384, 496, 272, 496));
            geometry.Edges.Add(new TerrainEdge(272, 496, 272, 272));
        }

        NativeTerrainHpSectorCoordinatePayload coordinates = new(
            NativeCenterXyWord,
            NativeCenterZRadiusFlagsWord,
            NativeXyPositionWord,
            NativeZPositionWord,
            SpyroRetailTerrainCoordinateCertification.RetailPackedSceneWords);
        geometry.SourceSectors.Add(new SceneSectorRenderMetadata(
            AuthoredSectorIndex,
            SectorOffset: -1,
            Center: new Vector3f(384, 384, 512),
            Radius: 192,
            DisableLowDetail: false,
            DisableHighDetail: false,
            ForceLowDetail: false,
            LowDetailVertexCount: 3 + (includeTile1 ? 1 : 0) + (includeTile2 ? 1 : 0),
            LowDetailColorCount: 3,
            LowDetailFaceCount: 1 + (includeTile1 ? 1 : 0) + (includeTile2 ? 1 : 0),
            HighDetailVertexCount: 3 + (includeTile1 ? 1 : 0) + (includeTile2 ? 1 : 0),
            HighDetailColorCount: 3,
            HighDetailFaceCount: 1 + (includeTile1 ? 1 : 0) + (includeTile2 ? 1 : 0),
            HighPolyCoordinates: coordinates));

        IReadOnlyList<int>[] groups = Enumerable.Range(0, 16)
            .Select(index => (IReadOnlyList<int>)(index == OcclusionGroupIndex
                ? new[] { AuthoredSectorIndex }
                : Array.Empty<int>()))
            .ToArray();
        NativeTerrainOcclusionTriangle collision =
            NativeTerrainOcclusionTriangle.FromWords(
                CollisionTriangleIndex,
                CollisionXWord,
                CollisionYWord,
                CollisionZWord,
                OcclusionGroupIndex);
        NativeTerrainOcclusionTriangle optionalCollision =
            NativeTerrainOcclusionTriangle.FromWords(
                OptionalCollisionTriangleIndex,
                OptionalCollisionXWord,
                OptionalCollisionYWord,
                OptionalCollisionZWord,
                OcclusionGroupIndex);
        NativeTerrainOcclusionTriangle tile2Collision =
            NativeTerrainOcclusionTriangle.FromWords(
                includeTile1
                    ? SecondOptionalCollisionTriangleIndex
                    : OptionalCollisionTriangleIndex,
                Tile2CollisionXWord,
                Tile2CollisionYWord,
                Tile2CollisionZWord,
                OcclusionGroupIndex);
        IReadOnlyList<NativeTerrainOcclusionTriangle> collisionTriangles =
            (includeTile1, includeTile2) switch
            {
                (false, false) => [collision],
                (true, false) => [optionalCollision, collision],
                (false, true) => [tile2Collision, collision],
                (true, true) => [tile2Collision, optionalCollision, collision]
            };
        geometry.NativeTerrainOcclusion = new NativeTerrainOcclusionData(
            groups,
            collisionTriangles);
        return geometry;
    }

    private static void ValidateTerrainSetProjectedGraph(
        GeometryCandidate geometry,
        Id65FreshLevelProtectedSpawn spawn,
        Id65AuthoringTerrainSetManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(spawn);
        ArgumentNullException.ThrowIfNull(manifest);
        string[] activeUnitIds = manifest.ActiveUnits.Select(item => item.Id).ToArray();
        Id65AuthoringTerrainSetManifest exactManifest =
            Id65AuthoringTerrainSetCatalog.Create(activeUnitIds);
        GeometryCandidate expected = BuildTerrainSetGeometry(exactManifest);
        NativeTerrainOcclusionData? collision = geometry.NativeTerrainOcclusion;
        TerrainPolygon? baseFace = geometry.Polygons.SingleOrDefault(item =>
            item.FaceIndex == AuthoredFaceIndex);
        if (manifest.ProfileId != Id65AuthoringTerrainSetCatalog.ProfileId ||
            manifest.CanonicalSha256 != exactManifest.CanonicalSha256 ||
            !manifest.ActiveUnits.Select(item => item.Id).SequenceEqual(activeUnitIds) ||
            !GeometryGraphsEqual(geometry, expected) ||
            spawn != BuildProtectedSpawn() ||
            baseFace is null || !baseFace.ContainsXY((float)spawn.X, (float)spawn.Y) ||
            collision is null ||
            !collision.TryResolveGroup(
                384,
                384,
                534,
                out int groupIndex,
                out int triangleIndex,
                out double floorZ) ||
            groupIndex != OcclusionGroupIndex ||
            triangleIndex != CollisionTriangleIndex || floorZ != 512)
        {
            throw new InvalidDataException(
                "The schema-5 ID65 fresh-scene terrain-set graph is not one exact presentation-only topology.");
        }
    }

    private static bool GeometryGraphsEqual(
        GeometryCandidate actual,
        GeometryCandidate expected) =>
        actual.Name == expected.Name &&
        actual.Bounds == expected.Bounds &&
        actual.MinZ == expected.MinZ && actual.MaxZ == expected.MaxZ &&
        actual.HasStaticLowDetailPreview == expected.HasStaticLowDetailPreview &&
        actual.TerrainLodPreviewContract == expected.TerrainLodPreviewContract &&
        actual.HasExactHighPolyMaterialState == expected.HasExactHighPolyMaterialState &&
        actual.HighPolyMaterialContract == expected.HighPolyMaterialContract &&
        actual.HasExactHighPolyCoordinateState == expected.HasExactHighPolyCoordinateState &&
        actual.HighPolyCoordinateContract == expected.HighPolyCoordinateContract &&
        actual.Polygons.Count == expected.Polygons.Count &&
        actual.Polygons.Zip(expected.Polygons).All(pair =>
            TerrainPolygonsEqual(pair.First, pair.Second)) &&
        actual.LowDetailPolygons.Count == expected.LowDetailPolygons.Count &&
        actual.LowDetailPolygons.Zip(expected.LowDetailPolygons).All(pair =>
            LowDetailPolygonsEqual(pair.First, pair.Second)) &&
        actual.Edges.SequenceEqual(expected.Edges) &&
        actual.SourceSectors.SequenceEqual(expected.SourceSectors) &&
        OcclusionEqual(actual.NativeTerrainOcclusion, expected.NativeTerrainOcclusion);

    private static bool TerrainPolygonsEqual(TerrainPolygon actual, TerrainPolygon expected) =>
        actual.Points.SequenceEqual(expected.Points) &&
        actual.OriginalPoints.SequenceEqual(expected.OriginalPoints) &&
        actual.ZValues.SequenceEqual(expected.ZValues) &&
        actual.OriginalZValues.SequenceEqual(expected.OriginalZValues) &&
        actual.TextureId == expected.TextureId &&
        actual.OriginalTextureId == expected.OriginalTextureId &&
        actual.SectorIndex == expected.SectorIndex &&
        actual.FaceIndex == expected.FaceIndex && actual.Detail == expected.Detail &&
        actual.SectorOffset == expected.SectorOffset &&
        actual.FaceOffset == expected.FaceOffset &&
        actual.VertexIndexes.SequenceEqual(expected.VertexIndexes) &&
        actual.ColourIndexes.SequenceEqual(expected.ColourIndexes) &&
        actual.CornerPointIndexes.SequenceEqual(expected.CornerPointIndexes) &&
        actual.NearColors.SequenceEqual(expected.NearColors) &&
        actual.FarColors.SequenceEqual(expected.FarColors) &&
        actual.Word3 == expected.Word3 && actual.Word4 == expected.Word4 &&
        actual.NativeFaceWord0 == expected.NativeFaceWord0 &&
        actual.NativeFaceWord1 == expected.NativeFaceWord1 &&
        actual.NativeFaceWord2 == expected.NativeFaceWord2 &&
        actual.NativeFaceWord3 == expected.NativeFaceWord3 &&
        actual.NativeMaterialByte == expected.NativeMaterialByte &&
        actual.NativeTextureId == expected.NativeTextureId &&
        actual.IsNativeUntexturedSentinel == expected.IsNativeUntexturedSentinel &&
        actual.NativePrimitiveSemiTransparent ==
            expected.NativePrimitiveSemiTransparent &&
        actual.FaceFlip == expected.FaceFlip && actual.FaceDepth == expected.FaceDepth &&
        actual.FaceColor == expected.FaceColor &&
        actual.SurfaceColor == expected.SurfaceColor &&
        actual.HasCompleteNativeHighPolyFacePayload &&
        actual.HasNativeHighPolyMaterialPayload && actual.HasNativeCornerPayload &&
        !actual.IsTerrainEdited;

    private static bool LowDetailPolygonsEqual(
        LowDetailTerrainPolygon actual,
        LowDetailTerrainPolygon expected) =>
        actual.Points.SequenceEqual(expected.Points) &&
        actual.ZValues.SequenceEqual(expected.ZValues) &&
        actual.SectorIndex == expected.SectorIndex &&
        actual.FaceIndex == expected.FaceIndex &&
        actual.SectorOffset == expected.SectorOffset &&
        actual.FaceOffset == expected.FaceOffset &&
        actual.VertexIndexes.SequenceEqual(expected.VertexIndexes) &&
        actual.CornerPointIndexes.SequenceEqual(expected.CornerPointIndexes) &&
        actual.ColorIndexes.SequenceEqual(expected.ColorIndexes) &&
        actual.CornerColors.SequenceEqual(expected.CornerColors) &&
        actual.RawWord0 == expected.RawWord0 && actual.RawWord1 == expected.RawWord1 &&
        actual.TransitionBias == expected.TransitionBias &&
        actual.DoubleSided == expected.DoubleSided &&
        actual.SemiTransparent == expected.SemiTransparent &&
        actual.BlendMode == expected.BlendMode &&
        actual.OrderingTableBias == expected.OrderingTableBias &&
        actual.HasCompleteNativePayload;

    private static bool OcclusionEqual(
        NativeTerrainOcclusionData? actual,
        NativeTerrainOcclusionData? expected) =>
        actual is not null && expected is not null &&
        actual.EnvironmentGroups.Count == expected.EnvironmentGroups.Count &&
        actual.EnvironmentGroups.Zip(expected.EnvironmentGroups).All(pair =>
            pair.First.SequenceEqual(pair.Second)) &&
        actual.CollisionTriangles.SequenceEqual(expected.CollisionTriangles);

    private static void ValidateProjectedGraph(
        GeometryCandidate geometry,
        Id65FreshLevelProtectedSpawn spawn,
        bool includeStructuralTerrain,
        int mandatoryBaseFaceTextureId)
    {
        if (mandatoryBaseFaceTextureId is not (25 or 66))
            throw new InvalidDataException(
                "The fresh ID65 mandatory face texture must be exactly T25 or T66.");
        int expectedFaceCount = includeStructuralTerrain ? 2 : 1;
        TerrainPolygon hp = geometry.Polygons.SingleOrDefault(item =>
                item.FaceIndex == AuthoredFaceIndex)
            ?? throw new InvalidDataException(
                "The ID65 fresh-level projection lost its mandatory HP face.");
        LowDetailTerrainPolygon lp = geometry.LowDetailPolygons.SingleOrDefault(item =>
                item.FaceIndex == AuthoredFaceIndex)
            ?? throw new InvalidDataException(
                "The ID65 fresh-level projection lost its mandatory LP companion.");
        SceneSectorRenderMetadata sector = geometry.SourceSectors.SingleOrDefault()
            ?? throw new InvalidDataException(
                "The ID65 fresh-level projection must contain exactly one source sector.");
        NativeTerrainOcclusionData occlusion = geometry.NativeTerrainOcclusion
            ?? throw new InvalidDataException(
                "The ID65 fresh-level projection must contain collision/occlusion metadata.");
        NativeTerrainOcclusionTriangle triangle =
            occlusion.CollisionTriangles.SingleOrDefault(item =>
                item.Index == CollisionTriangleIndex)
            ?? throw new InvalidDataException(
                "The ID65 fresh-level projection lost mandatory collision T13995.");
        Vector2f[] expectedPoints =
        [
            new(272, 272),
            new(496, 272),
            new(384, 496)
        ];
        float[] expectedZ = [512, 512, 512];
        ColorRgba expectedFaceColor = ColorRgba.FromRgb(124, 96, 103);
        ColorRgba[] expectedNearColors = ExpectedNearColors();
        ColorRgba[] expectedFarColors = ExpectedFarColors();
        ColorRgba[] expectedLowColors = ExpectedLowColors();
        TerrainEdge[] expectedEdges =
        [
            new(272, 272, 496, 272),
            new(496, 272, 384, 496),
            new(384, 496, 272, 272)
        ];
        TerrainEdge[] expectedOptionalEdges =
        [
            .. expectedEdges,
            new(496, 272, 496, 496),
            new(496, 496, 384, 496)
        ];

        bool exactGraph =
            string.Equals(geometry.Name, GeometryName, StringComparison.Ordinal) &&
            geometry.Bounds == Rect2f.FromBounds(272, 272, 496, 496) &&
            geometry.MinZ == 512 &&
            geometry.MaxZ == 512 &&
            geometry.HasStaticLowDetailPreview &&
            geometry.TerrainLodPreviewContract == SourceSceneOverlayContract.TerrainLodPreview &&
            geometry.HasExactHighPolyMaterialState &&
            geometry.HighPolyMaterialContract == SourceSceneOverlayContract.HpFaceMaterialPayload &&
            geometry.HasExactHighPolyCoordinateState &&
            geometry.HighPolyCoordinateContract == NativeTerrainHighPolyCoordinates.Contract &&
            geometry.Polygons.Count == expectedFaceCount &&
            geometry.LowDetailPolygons.Count == expectedFaceCount &&
            geometry.Edges.SequenceEqual(
                includeStructuralTerrain ? expectedOptionalEdges : expectedEdges) &&
            geometry.SourceSectors.Count == 1 &&
            hp.SectorIndex == AuthoredSectorIndex &&
            hp.FaceIndex == AuthoredFaceIndex &&
            hp.RuntimeKey == $"{AuthoredSectorIndex}:{AuthoredFaceIndex}:hp" &&
            hp.Detail == "hp" &&
            hp.SectorOffset == -1 &&
            hp.FaceOffset == -1 &&
            hp.Points.SequenceEqual(expectedPoints) &&
            hp.OriginalPoints.SequenceEqual(expectedPoints) &&
            hp.ZValues.SequenceEqual(expectedZ) &&
            hp.OriginalZValues.SequenceEqual(expectedZ) &&
            hp.TextureId == mandatoryBaseFaceTextureId &&
            hp.OriginalTextureId == mandatoryBaseFaceTextureId &&
            hp.NativeTextureId == mandatoryBaseFaceTextureId &&
            hp.NativeMaterialByte == mandatoryBaseFaceTextureId &&
            hp.NativeFaceWord0 == HighFaceWord0 &&
            hp.NativeFaceWord1 == HighFaceWord1 &&
            hp.NativeFaceWord2 == (mandatoryBaseFaceTextureId == 66
                ? HighFaceWord2
                : OptionalHighFaceWord2) &&
            hp.NativeFaceWord3 == HighFaceWord3 &&
            hp.Word3 == (mandatoryBaseFaceTextureId == 66
                ? "0x01400242"
                : "0x01400219") &&
            hp.Word4 == "0x00100A08" &&
            hp.VertexIndexes.SequenceEqual(new[] { 0, 0, 1, 2 }) &&
            hp.ColourIndexes.SequenceEqual(new[] { 1, 1, 0, 2 }) &&
            hp.CornerPointIndexes.SequenceEqual(new[] { 0, 0, 1, 2 }) &&
            hp.FaceColor == expectedFaceColor &&
            hp.SurfaceColor == expectedFaceColor &&
            hp.NearColors.SequenceEqual(expectedNearColors) &&
            hp.FarColors.SequenceEqual(expectedFarColors) &&
            !hp.FaceFlip &&
            hp.FaceDepth == 1 &&
            !hp.IsNativeUntexturedSentinel &&
            !hp.NativePrimitiveSemiTransparent &&
            !hp.LqFadeBypass &&
            !hp.HqOverlayBypass &&
            hp.HasNativeHighPolyMaterialPayload &&
            hp.HasCompleteNativeHighPolyFacePayload &&
            hp.HasNativeCornerPayload &&
            !hp.IsTerrainEdited &&
            lp.SectorIndex == AuthoredSectorIndex &&
            lp.FaceIndex == AuthoredFaceIndex &&
            lp.RuntimeKey == $"{AuthoredSectorIndex}:{AuthoredFaceIndex}:lp" &&
            lp.SectorOffset == -1 &&
            lp.FaceOffset == -1 &&
            lp.Points.SequenceEqual(expectedPoints) &&
            lp.ZValues.SequenceEqual(expectedZ) &&
            lp.RawWord0 == LowFaceWord0 &&
            lp.RawWord1 == LowFaceWord1 &&
            lp.VertexIndexes.SequenceEqual(new[] { 0, 1, 2, 2 }) &&
            lp.CornerPointIndexes.SequenceEqual(new[] { 0, 1, 2, 2 }) &&
            lp.ColorIndexes.SequenceEqual(new[] { 0, 1, 2, 2 }) &&
            lp.CornerColors.SequenceEqual(expectedLowColors) &&
            lp.TransitionBias == 0 &&
            !lp.DoubleSided &&
            !lp.SemiTransparent &&
            lp.BlendMode == 0 &&
            lp.OrderingTableBias == 0 &&
            lp.HasCompleteNativePayload &&
            sector.SectorIndex == AuthoredSectorIndex &&
            sector.SectorOffset == -1 &&
            sector.Center == new Vector3f(384, 384, 512) &&
            sector.Radius == 192 &&
            !sector.DisableLowDetail &&
            !sector.DisableHighDetail &&
            !sector.ForceLowDetail &&
            sector.LowDetailVertexCount == (includeStructuralTerrain ? 4 : 3) &&
            sector.LowDetailColorCount == 3 &&
            sector.LowDetailFaceCount == expectedFaceCount &&
            sector.HighDetailVertexCount == (includeStructuralTerrain ? 4 : 3) &&
            sector.HighDetailColorCount == 3 &&
            sector.HighDetailFaceCount == expectedFaceCount &&
            sector.HighPolyCoordinates is { } coordinates &&
            coordinates.NativeCenterXyWord == NativeCenterXyWord &&
            coordinates.NativeCenterZRadiusFlagsWord == NativeCenterZRadiusFlagsWord &&
            coordinates.NativeXyPositionWord == NativeXyPositionWord &&
            coordinates.NativeZPositionWord == NativeZPositionWord &&
            coordinates.Certification ==
                SpyroRetailTerrainCoordinateCertification.RetailPackedSceneWords &&
            occlusion.EnvironmentGroups.Count == 16 &&
            occlusion.EnvironmentGroups[OcclusionGroupIndex]
                .SequenceEqual(new[] { AuthoredSectorIndex }) &&
            occlusion.EnvironmentGroups
                .Where((_, index) => index != OcclusionGroupIndex)
                .All(group => group.Count == 0) &&
            occlusion.CollisionTriangles.Count == expectedFaceCount &&
            triangle.Index == CollisionTriangleIndex &&
            triangle.GroupIndex == OcclusionGroupIndex &&
            triangle.P1 == new SpyroCollisionPoint(272, 272, 512) &&
            triangle.P2 == new SpyroCollisionPoint(384, 496, 512) &&
            triangle.P3 == new SpyroCollisionPoint(496, 272, 512) &&
            occlusion.TryResolveGroup(384, 384, 534, out int groupIndex, out int triangleIndex, out double floorZ) &&
            groupIndex == OcclusionGroupIndex &&
            triangleIndex == CollisionTriangleIndex &&
            floorZ == 512 &&
            spawn.IsProtected &&
            spawn.StableId == SpawnStableId &&
            spawn.PlayerAnchorTrueIndex == UnusedLevel65FullAuthoringConstructionTemplate.PlayerAnchorTrueIndex &&
            spawn.LandingWadOffset == UnusedLevel65FullAuthoringConstructionTemplate.LandingWadOffset &&
            spawn.PlayerAnchorWadOffset == UnusedLevel65FullAuthoringConstructionTemplate.PlayerAnchorWadOffset &&
            spawn.RawX == UnusedLevel65RemoteBlankIsolationConstruction.AuthoredRawX &&
            spawn.RawY == UnusedLevel65RemoteBlankIsolationConstruction.AuthoredRawY &&
            spawn.LandingRawZ == UnusedLevel65RemoteBlankIsolationConstruction.LandingRawZ &&
            spawn.PlayerRawZ == UnusedLevel65RemoteBlankIsolationConstruction.PlayerRawZ &&
            spawn.GroundRawZ == UnusedLevel65RemoteBlankIsolationConstruction.GroundRawZ &&
            spawn.YawByte == UnusedLevel65RemoteBlankIsolationConstruction.YawByte &&
            spawn.EntryDataByteLength == UnusedLevel65FullAuthoringConstructionTemplate.LandingByteLength &&
            hp.ContainsXY((float)spawn.X, (float)spawn.Y) &&
            ValidateOptionalUnit(geometry, occlusion, includeStructuralTerrain);
        if (!exactGraph)
        {
            throw new InvalidDataException(
                "The ID65 fresh-level projected geometry graph no longer matches the exact T66 pad, LP companion, collision/occlusion, and protected-spawn contract.");
        }
    }

    private static bool ValidateOptionalUnit(
        GeometryCandidate geometry,
        NativeTerrainOcclusionData occlusion,
        bool includeStructuralTerrain)
    {
        TerrainPolygon? hp = geometry.Polygons.SingleOrDefault(item =>
            item.FaceIndex == OptionalFaceIndex);
        LowDetailTerrainPolygon? lp = geometry.LowDetailPolygons.SingleOrDefault(item =>
            item.FaceIndex == OptionalFaceIndex);
        NativeTerrainOcclusionTriangle? collision =
            occlusion.CollisionTriangles.SingleOrDefault(item =>
                item.Index == OptionalCollisionTriangleIndex);
        if (!includeStructuralTerrain)
            return hp == null && lp == null && collision == null;
        if (hp == null || lp == null || collision == null)
            return false;

        Vector2f[] points = [new(496, 272), new(496, 496), new(384, 496)];
        float[] z = [512, 512, 512];
        ColorRgba faceColor = ColorRgba.FromRgb(124, 96, 103);
        return hp.SectorIndex == AuthoredSectorIndex &&
            hp.FaceIndex == OptionalFaceIndex &&
            hp.RuntimeKey == $"{AuthoredSectorIndex}:{OptionalFaceIndex}:hp" &&
            hp.Detail == "hp" &&
            hp.SectorOffset == -1 && hp.FaceOffset == -1 &&
            hp.Points.SequenceEqual(points) && hp.OriginalPoints.SequenceEqual(points) &&
            hp.ZValues.SequenceEqual(z) && hp.OriginalZValues.SequenceEqual(z) &&
            hp.TextureId == OptionalNativeTextureId &&
            hp.OriginalTextureId == OptionalNativeTextureId &&
            hp.NativeTextureId == OptionalNativeTextureId &&
            hp.NativeMaterialByte == OptionalNativeTextureId &&
            hp.VertexIndexes.SequenceEqual(new[] { 1, 1, 3, 2 }) &&
            hp.ColourIndexes.SequenceEqual(new[] { 1, 1, 0, 2 }) &&
            hp.CornerPointIndexes.SequenceEqual(new[] { 0, 0, 1, 2 }) &&
            hp.NativeFaceWord0 == OptionalHighFaceWord0 &&
            hp.NativeFaceWord1 == OptionalHighFaceWord1 &&
            hp.NativeFaceWord2 == OptionalHighFaceWord2 &&
            hp.NativeFaceWord3 == OptionalHighFaceWord3 &&
            hp.Word3 == "0x01400219" && hp.Word4 == "0x00100A08" &&
            hp.FaceColor == faceColor && hp.SurfaceColor == faceColor &&
            hp.NearColors.SequenceEqual(ExpectedNearColors()) &&
            hp.FarColors.SequenceEqual(ExpectedFarColors()) &&
            !hp.FaceFlip && hp.FaceDepth == 1 &&
            !hp.IsNativeUntexturedSentinel &&
            !hp.NativePrimitiveSemiTransparent &&
            !hp.LqFadeBypass && !hp.HqOverlayBypass &&
            hp.HasNativeHighPolyMaterialPayload &&
            hp.HasCompleteNativeHighPolyFacePayload && hp.HasNativeCornerPayload &&
            !hp.IsTerrainEdited &&
            lp.SectorIndex == AuthoredSectorIndex &&
            lp.FaceIndex == OptionalFaceIndex &&
            lp.RuntimeKey == $"{AuthoredSectorIndex}:{OptionalFaceIndex}:lp" &&
            lp.SectorOffset == -1 && lp.FaceOffset == -1 &&
            lp.Points.SequenceEqual(points) && lp.ZValues.SequenceEqual(z) &&
            lp.RawWord0 == OptionalLowFaceWord0 &&
            lp.RawWord1 == OptionalLowFaceWord1 &&
            lp.VertexIndexes.SequenceEqual(new[] { 1, 3, 2, 2 }) &&
            lp.CornerPointIndexes.SequenceEqual(new[] { 0, 1, 2, 2 }) &&
            lp.ColorIndexes.SequenceEqual(new[] { 0, 1, 2, 2 }) &&
            lp.CornerColors.SequenceEqual(ExpectedLowColors()) &&
            lp.TransitionBias == 0 && !lp.DoubleSided && !lp.SemiTransparent &&
            lp.BlendMode == 0 && lp.OrderingTableBias == 0 &&
            lp.HasCompleteNativePayload &&
            collision.Index == OptionalCollisionTriangleIndex &&
            collision.GroupIndex == OcclusionGroupIndex &&
            collision.P1 == new SpyroCollisionPoint(496, 272, 512) &&
            collision.P2 == new SpyroCollisionPoint(384, 496, 512) &&
            collision.P3 == new SpyroCollisionPoint(496, 496, 512) &&
            occlusion.CollisionTriangles.Select(item => item.Index)
                .SequenceEqual(new[] { OptionalCollisionTriangleIndex, CollisionTriangleIndex }) &&
            occlusion.TryResolveGroup(
                480,
                400,
                534,
                out int groupIndex,
                out int triangleIndex,
                out double floorZ) &&
            groupIndex == OcclusionGroupIndex &&
            triangleIndex == OptionalCollisionTriangleIndex &&
            floorZ == 512;
    }

    private static void ValidateFrozenAuthoringIntent()
    {
        Id65AuthoringManifest manifest =
            Id65V2NativeTextureCompositionCompiler.CreateMinimalV3T66Manifest();
        Id65SectorIntent sector = manifest.Sectors.Single();
        Dictionary<string, Id65AuthoringPoint> vertices = manifest.Vertices
            .ToDictionary(vertex => vertex.Handle, vertex => vertex.Point, StringComparer.Ordinal);
        Id65RenderFaceIntent face = manifest.RenderFaces.Single();
        Id65CollisionIntent collision = manifest.Collision.Single();
        Id65OcclusionIntent occlusion = manifest.Occlusion.Single();
        Id65TextureIntent authoredTexture = manifest.Textures.Single(texture =>
            texture.Id == Id65V2NativeTextureCompositionCompiler.PrivateTextureIntentId);
        Id65MobyIntent playerAnchor = manifest.Mobys.Single();
        Id65SpawnIntent spawn = manifest.Spawn;
        Id65TerrainTriangleIntent optional =
            Id65AuthoringTerrainMutationCompiler.CreateConcreteTwoTileWitness();

        bool pointsExact =
            vertices.TryGetValue(face.HighA, out Id65AuthoringPoint pointA) &&
            vertices.TryGetValue(face.HighB, out Id65AuthoringPoint pointB) &&
            vertices.TryGetValue(face.HighC, out Id65AuthoringPoint pointC) &&
            pointA == new Id65AuthoringPoint(272, 272, 512) &&
            pointB == new Id65AuthoringPoint(496, 272, 512) &&
            pointC == new Id65AuthoringPoint(384, 496, 512);
        if (manifest.ProfileId != Id65V2NativeTextureCompositionCompiler.ManifestProfileId ||
            !string.Equals(
                manifest.CanonicalSha256,
                Id65V2NativeTextureCompositionCompiler.ExpectedManifestSha256,
                StringComparison.OrdinalIgnoreCase) ||
            sector.Id != SectorStableId ||
            sector.SectorIndex != AuthoredSectorIndex ||
            sector.EncodingOrigin != new Id65AuthoringPoint(256, 256, 512) ||
            sector.CullCenter != new Id65AuthoringPoint(384, 384, 512) ||
            sector.CullRadius != 192 ||
            sector.CullFlags != 0 ||
            face.Id != RenderFaceStableId ||
            face.SectorId != SectorStableId ||
            face.TextureIntentId != Id65V2NativeTextureCompositionCompiler.PrivateTextureIntentId ||
            authoredTexture.Disposition != Id65AuthoringIntentDisposition.AuthorExact ||
            authoredTexture.DestinationRecordIndex != StarterTextureId ||
            authoredTexture.SourceHandle != Id65V2NativeTextureCompositionCompiler.PrivateTextureSourceHandle ||
            !pointsExact ||
            collision.Id != CollisionStableId ||
            collision.TriangleIndex != CollisionTriangleIndex ||
            collision.Assignment != OcclusionGroupIndex ||
            collision.WindingPolicy != Id65CollisionWindingPolicy.NegativeNormalZAcb ||
            occlusion.Id != OcclusionStableId ||
            occlusion.SectorId != SectorStableId ||
            occlusion.GroupIndex != OcclusionGroupIndex ||
            playerAnchor.TrueIndex != UnusedLevel65FullAuthoringConstructionTemplate.PlayerAnchorTrueIndex ||
            spawn.Id != SpawnStableId ||
            spawn.LandingRawX != UnusedLevel65RemoteBlankIsolationConstruction.AuthoredRawX ||
            spawn.LandingRawY != UnusedLevel65RemoteBlankIsolationConstruction.AuthoredRawY ||
            spawn.LandingRawZ != UnusedLevel65RemoteBlankIsolationConstruction.LandingRawZ ||
            spawn.PlayerRawZ != UnusedLevel65RemoteBlankIsolationConstruction.PlayerRawZ ||
            spawn.GroundRawZ != UnusedLevel65RemoteBlankIsolationConstruction.GroundRawZ ||
            spawn.YawByte != UnusedLevel65RemoteBlankIsolationConstruction.YawByte ||
            optional.Id != Id65AuthoringTerrainMutationCompiler.TriangleId ||
            optional.AddedVertex.Handle != Id65AuthoringTerrainMutationCompiler.AddedVertexHandle ||
            optional.AddedVertex.Point != new Id65AuthoringPoint(496, 496, 512) ||
            optional.RenderA != "vertex.remote-pad.b" ||
            optional.RenderB != Id65AuthoringTerrainMutationCompiler.AddedVertexHandle ||
            optional.RenderC != "vertex.remote-pad.c" ||
            optional.CollisionA != "vertex.remote-pad.b" ||
            optional.CollisionB != "vertex.remote-pad.c" ||
            optional.CollisionC != Id65AuthoringTerrainMutationCompiler.AddedVertexHandle ||
            optional.CollisionAssignment != OcclusionGroupIndex ||
            WordsHex(OptionalLowFaceWord0, OptionalLowFaceWord1) !=
                Id65AuthoringTerrainMutationCompiler.ExpectedLowDetailFaceHex ||
            WordsHex(
                OptionalHighFaceWord0,
                OptionalHighFaceWord1,
                OptionalHighFaceWord2,
                OptionalHighFaceWord3) !=
                Id65AuthoringTerrainMutationCompiler.ExpectedHighDetailFaceHex ||
            WordsHex(
                OptionalCollisionXWord,
                OptionalCollisionYWord,
                OptionalCollisionZWord) !=
                Id65AuthoringTerrainMutationCompiler.ExpectedCollisionTriangleHex)
        {
            throw new InvalidDataException(
                "The ID65 fresh-level scene projection no longer matches the frozen authoring intent.");
        }
    }

    private static string BindDocumentIdentity(
        string graphCanonicalSha256,
        string documentCanonicalSha256,
        string documentContentSha256,
        bool structuralTerrainPresent) =>
        Hash(
            "format=id65-fresh-level-authoring-scene-document-binding-v1\n" +
            $"profile={ProfileId}\n" +
            $"schema={SchemaVersion}\n" +
            $"graphCanonicalSha256={graphCanonicalSha256}\n" +
            $"documentCanonicalSha256={documentCanonicalSha256}\n" +
            $"documentContentSha256={documentContentSha256}\n" +
            $"structuralTerrainPresent={(structuralTerrainPresent ? "true" : "false")}\n" +
            "authority=presentation-only|writesWorkspace=false|writesLockedBase=false" +
            "|createBin=false|runtimeCandidate=false|releasePromotion=false\n");

    private static string BindRemotePadGridDocumentIdentity(
        string graphCanonicalSha256,
        string documentCanonicalSha256,
        string documentContentSha256,
        bool structuralTerrainPresent,
        IReadOnlyList<Id65FreshLevelMobyPresentation> visibleMobyPresentations)
    {
        StringBuilder canonical = new(
            "format=id65-fresh-level-authoring-scene-document-binding-remote-pad-grid-v2\n");
        canonical.Append("profile=").Append(ProfileId).Append('\n');
        canonical.Append("schema=").Append(SchemaVersion).Append('\n');
        canonical.Append("graphCanonicalSha256=").Append(graphCanonicalSha256).Append('\n');
        canonical.Append("documentCanonicalSha256=").Append(documentCanonicalSha256).Append('\n');
        canonical.Append("documentContentSha256=").Append(documentContentSha256).Append('\n');
        canonical.Append("structuralTerrainPresent=")
            .Append(structuralTerrainPresent ? "true" : "false").Append('\n');
        canonical.Append("visibleMobyCount=").Append(visibleMobyPresentations.Count).Append('\n');
        foreach (Id65FreshLevelMobyPresentation presentation in visibleMobyPresentations)
        {
            canonical.Append("visibleMoby=").Append(presentation.CanonicalSha256).Append('\n');
        }
        canonical.Append(
            "authority=presentation-only|writesWorkspace=false|writesLockedBase=false" +
            "|editable=false|nativePersistence=false|runtimeObserved=false" +
            "|createBin=false|runtimeCandidate=false|releasePromotion=false\n");
        return Hash(canonical.ToString());
    }

    private static string BindRemotePadMobyChoiceDocumentIdentity(
        string graphCanonicalSha256,
        string documentCanonicalSha256,
        string documentContentSha256,
        bool structuralTerrainPresent,
        Id65RemotePadMobyKind kind,
        IReadOnlyList<Id65FreshLevelMobyPresentation> visibleMobyPresentations)
    {
        StringBuilder canonical = new(
            "format=id65-fresh-level-authoring-scene-document-binding-remote-pad-moby-choice-v3\n");
        canonical.Append("profile=").Append(ProfileId).Append('\n');
        canonical.Append("schema=").Append(SchemaVersion).Append('\n');
        canonical.Append("choiceCatalogSha256=")
            .Append(Id65RemotePadMobyChoiceCatalog.CanonicalSha256).Append('\n');
        canonical.Append("mobyKind=")
            .Append(Id65RemotePadMobyChoiceCatalog.GetKindId(kind)).Append('\n');
        canonical.Append("graphCanonicalSha256=").Append(graphCanonicalSha256).Append('\n');
        canonical.Append("documentCanonicalSha256=").Append(documentCanonicalSha256).Append('\n');
        canonical.Append("documentContentSha256=").Append(documentContentSha256).Append('\n');
        canonical.Append("structuralTerrainPresent=")
            .Append(structuralTerrainPresent ? "true" : "false").Append('\n');
        canonical.Append("visibleMobyCount=").Append(visibleMobyPresentations.Count).Append('\n');
        foreach (Id65FreshLevelMobyPresentation presentation in visibleMobyPresentations)
            canonical.Append("visibleMoby=").Append(presentation.CanonicalSha256).Append('\n');
        canonical.Append(
            "authority=presentation-only|writesWorkspace=false|writesLockedBase=false" +
            "|editable=false|nativePersistence=false|runtimeObserved=false" +
            "|createBin=false|runtimeCandidate=false|releasePromotion=false\n");
        return Hash(canonical.ToString());
    }

    private static string BindRemotePadMobyChoiceTerrainSetDocumentIdentity(
        string graphCanonicalSha256,
        string documentCanonicalSha256,
        string documentContentSha256,
        Id65AuthoringTerrainSetManifest terrainSet,
        Id65RemotePadMobyKind kind,
        IReadOnlyList<Id65FreshLevelMobyPresentation> visibleMobyPresentations)
    {
        StringBuilder canonical = new(
            "format=id65-fresh-level-authoring-scene-document-binding-terrain-set-v4\n");
        canonical.Append("profile=").Append(TerrainSetProfileId).Append('\n');
        canonical.Append("schema=").Append(TerrainSetSchemaVersion).Append('\n');
        canonical.Append("choiceCatalogSha256=")
            .Append(Id65RemotePadMobyChoiceCatalog.CanonicalSha256).Append('\n');
        canonical.Append("mobyKind=")
            .Append(Id65RemotePadMobyChoiceCatalog.GetKindId(kind)).Append('\n');
        canonical.Append("terrainCatalogProfileId=")
            .Append(Id65AuthoringTerrainSetCatalog.ProfileId).Append('\n');
        canonical.Append("terrainCatalogSchemaVersion=")
            .Append(Id65AuthoringTerrainSetCatalog.SchemaVersion).Append('\n');
        canonical.Append("terrainSetManifestSha256=")
            .Append(terrainSet.CanonicalSha256).Append('\n');
        canonical.Append("activeTerrainUnitCount=")
            .Append(terrainSet.ActiveUnits.Count).Append('\n');
        foreach (Id65TerrainTriangleIntent unit in terrainSet.ActiveUnits)
            canonical.Append("activeTerrainUnitId=").Append(unit.Id).Append('\n');
        canonical.Append("graphCanonicalSha256=").Append(graphCanonicalSha256).Append('\n');
        canonical.Append("documentCanonicalSha256=")
            .Append(documentCanonicalSha256).Append('\n');
        canonical.Append("documentContentSha256=")
            .Append(documentContentSha256).Append('\n');
        canonical.Append("visibleMobyCount=").Append(visibleMobyPresentations.Count).Append('\n');
        foreach (Id65FreshLevelMobyPresentation presentation in visibleMobyPresentations)
            canonical.Append("visibleMoby=").Append(presentation.CanonicalSha256).Append('\n');
        canonical.Append(
            "authority=presentation-only|writesWorkspace=false|writesLockedBase=false" +
            "|editable=false|nativePersistence=false|physicalMaterialization=false" +
            "|runtimeObserved=false|createBin=false|runtimeCandidate=false" +
            "|releasePromotion=false\n");
        return Hash(canonical.ToString());
    }

    private static ColorRgba[] ExpectedNearColors() =>
    [
        ColorRgba.FromRgb(128, 98, 104),
        ColorRgba.FromRgb(128, 98, 104),
        ColorRgba.FromRgb(119, 93, 102),
        ColorRgba.FromRgb(122, 95, 104)
    ];

    private static ColorRgba[] ExpectedFarColors() =>
    [
        ColorRgba.FromRgb(119, 93, 102),
        ColorRgba.FromRgb(119, 93, 102),
        ColorRgba.FromRgb(153, 117, 107),
        ColorRgba.FromRgb(137, 106, 106)
    ];

    private static ColorRgba[] ExpectedLowColors() =>
    [
        ColorRgba.FromRgb(45, 57, 48),
        ColorRgba.FromRgb(48, 85, 44),
        ColorRgba.FromRgb(55, 98, 44),
        ColorRgba.FromRgb(55, 98, 44)
    ];

    private static bool IsMalformedGraphException(Exception exception) => exception is
        InvalidDataException or
        InvalidOperationException or
        ArgumentException or
        IndexOutOfRangeException or
        NullReferenceException or
        ArithmeticException;

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    private static string WordsHex(params uint[] words)
    {
        byte[] bytes = new byte[checked(words.Length * sizeof(uint))];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(
                bytes.AsSpan(index * sizeof(uint), sizeof(uint)),
                words[index]);
        }
        return Convert.ToHexString(bytes);
    }
}
