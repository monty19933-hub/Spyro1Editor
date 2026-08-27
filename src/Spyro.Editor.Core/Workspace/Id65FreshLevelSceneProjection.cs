using System.Buffers.Binary;
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
/// Read-only presentation result for the exact ID65 fresh-level identity.
/// It is not a BIN patch, a runtime candidate, or export authorization.
/// </summary>
public sealed class Id65FreshLevelSceneProjection
{
    internal Id65FreshLevelSceneProjection(
        GeometryCandidate geometry,
        Id65FreshLevelProtectedSpawn protectedSpawn,
        string canonicalSha256,
        string graphCanonicalSha256,
        string documentCanonicalSha256,
        string documentContentSha256,
        bool structuralTerrainPresent)
    {
        Geometry = geometry;
        ProtectedSpawn = protectedSpawn;
        CanonicalSha256 = canonicalSha256;
        GraphCanonicalSha256 = graphCanonicalSha256;
        DocumentCanonicalSha256 = documentCanonicalSha256;
        DocumentContentSha256 = documentContentSha256;
        StructuralTerrainPresent = structuralTerrainPresent;
    }

    public string ProfileId => Id65FreshLevelSceneProjector.ProfileId;
    public int SchemaVersion => Id65FreshLevelSceneProjector.SchemaVersion;
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
    public bool CanonicalDocumentBound => IsCanonicalSha256(DocumentCanonicalSha256) &&
        IsCanonicalSha256(DocumentContentSha256);
    public IReadOnlyList<int> VisibleMobyTrueIndexes { get; } = Array.Empty<int>();
    public int VisibleMobyCount => VisibleMobyTrueIndexes.Count;
    public bool IsPresentationOnly => true;
    public bool WritesWorkspace => false;
    public bool WritesLockedBase => false;
    public bool AuthorizesCreateBin => false;
    public bool AuthorizesRuntimeCandidate => false;
    public bool AuthorizesReleasePromotion => false;

    private static bool IsCanonicalSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');
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
    public const string GeometryName = ProfileId + ":unusedlevel65blank";
    public const int AuthoredSectorIndex = 216;
    public const int AuthoredFaceIndex = 0;
    public const int OptionalFaceIndex = 1;
    public const int StarterTextureId = 66;
    public const int CollisionTriangleIndex = 13_995;
    public const int OptionalCollisionTriangleIndex = 19_808;
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

        ValidateFrozenAuthoringIntent();
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
            structuralTerrainPresent: false);
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
        Id65AuthoringEditorControlState state =
            Id65AuthoringEditorControlStateCompiler.RecoverControlState(document);
        GeometryCandidate geometry = BuildGeometry(
            state.IncludeStructuralTerrain,
            state.MandatoryBaseFaceTextureId);
        Id65FreshLevelProtectedSpawn spawn = BuildProtectedSpawn();
        ValidateProjectedGraph(
            geometry,
            spawn,
            state.IncludeStructuralTerrain,
            state.MandatoryBaseFaceTextureId);
        string graphCanonicalSha256 = state.MandatoryBaseFaceTextureId == 66
            ? (state.IncludeStructuralTerrain
                ? ExpectedStructuralGraphCanonicalSha256
                : ExpectedCanonicalSha256)
            : (state.IncludeStructuralTerrain
                ? ExpectedResidentT25StructuralGraphCanonicalSha256
                : ExpectedResidentT25BaseGraphCanonicalSha256);
        string identity = BindDocumentIdentity(
            graphCanonicalSha256,
            document.DocumentCanonicalSha256,
            document.DocumentContentSha256,
            state.IncludeStructuralTerrain);
        return new Id65FreshLevelSceneProjection(
            geometry,
            spawn,
            identity,
            graphCanonicalSha256,
            document.DocumentCanonicalSha256,
            document.DocumentContentSha256,
            state.IncludeStructuralTerrain);
    }

    private static Id65FreshLevelProtectedSpawn BuildProtectedSpawn() => new(
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
        string.Equals(geometry.Name, GeometryName, StringComparison.Ordinal);

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

    private static GeometryCandidate BuildGeometry(
        bool includeStructuralTerrain,
        int mandatoryBaseFaceTextureId)
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
            Name = GeometryName,
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

        if (includeStructuralTerrain)
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

        geometry.Edges.Add(new TerrainEdge(272, 272, 496, 272));
        geometry.Edges.Add(new TerrainEdge(496, 272, 384, 496));
        geometry.Edges.Add(new TerrainEdge(384, 496, 272, 272));
        if (includeStructuralTerrain)
        {
            geometry.Edges.Add(new TerrainEdge(496, 272, 496, 496));
            geometry.Edges.Add(new TerrainEdge(496, 496, 384, 496));
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
            LowDetailVertexCount: includeStructuralTerrain ? 4 : 3,
            LowDetailColorCount: 3,
            LowDetailFaceCount: includeStructuralTerrain ? 2 : 1,
            HighDetailVertexCount: includeStructuralTerrain ? 4 : 3,
            HighDetailColorCount: 3,
            HighDetailFaceCount: includeStructuralTerrain ? 2 : 1,
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
        geometry.NativeTerrainOcclusion = new NativeTerrainOcclusionData(
            groups,
            includeStructuralTerrain ? [optionalCollision, collision] : [collision]);
        return geometry;
    }

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
