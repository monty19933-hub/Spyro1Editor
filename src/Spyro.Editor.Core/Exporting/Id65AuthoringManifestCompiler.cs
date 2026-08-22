using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

internal enum Id65AuthoringIntentDisposition
{
    PreserveLockedBase,
    AuthorExact
}

internal enum Id65RenderFaceOrderPolicy
{
    DeclaredAbcGpuOrder
}

internal enum Id65CollisionWindingPolicy
{
    NegativeNormalZAcb
}

internal readonly record struct Id65AuthoringPoint(int X, int Y, int Z);

internal readonly record struct Id65AuthoringCollisionCell(int X, int Y, int Z);

internal readonly record struct Id65LockedDisplayNameBase(
    string ProfileId,
    string ImageSha256,
    string Row80DataSha256,
    string ModelSha256,
    int ModelByteLength,
    int UsedModelByteLength,
    int ZeroTailByteCount);

internal readonly record struct Id65SectorIntent(
    string Id,
    int SectorIndex,
    Id65AuthoringPoint EncodingOrigin,
    Id65AuthoringPoint CullCenter,
    int CullRadius,
    int CullFlags);

internal readonly record struct Id65TerrainVertexIntent(
    string Handle,
    Id65AuthoringPoint Point);

internal readonly record struct Id65RenderFaceIntent(
    string Id,
    string SectorId,
    string TextureIntentId,
    string LowA,
    string LowB,
    string LowC,
    string HighA,
    string HighB,
    string HighC,
    Id65RenderFaceOrderPolicy OrderPolicy);

internal readonly record struct Id65CollisionIntent(
    string Id,
    int TriangleIndex,
    string A,
    string B,
    string C,
    int Assignment,
    Id65AuthoringCollisionCell TargetCell,
    Id65CollisionWindingPolicy WindingPolicy);

internal readonly record struct Id65OcclusionIntent(
    string Id,
    string SectorId,
    int GroupIndex);

internal readonly record struct Id65TextureIntent(
    string Id,
    Id65AuthoringIntentDisposition Disposition,
    int DestinationRecordIndex,
    string SourceHandle);

internal readonly record struct Id65MobyIntent(
    string Id,
    Id65AuthoringIntentDisposition Disposition,
    int TrueIndex,
    int RawX,
    int RawY,
    int RawZ,
    string DependencyBundleId);

internal readonly record struct Id65SpawnIntent(
    string Id,
    Id65AuthoringIntentDisposition Disposition,
    string PlayerMobyId,
    int LandingRawX,
    int LandingRawY,
    int LandingRawZ,
    int PlayerRawZ,
    int GroundRawZ,
    int YawByte);

internal readonly record struct Id65MusicIntent(
    string Id,
    Id65AuthoringIntentDisposition Disposition,
    int ExecutableSlot,
    int? TrackId);

internal readonly record struct Id65TotalsIntent(
    string Id,
    Id65AuthoringIntentDisposition Disposition,
    int LevelSlot,
    int? Gems,
    int? Dragons,
    int? Eggs);

internal readonly record struct Id65ExitIntent(
    string Id,
    Id65AuthoringIntentDisposition Disposition,
    int LevelSlot,
    int? ReturnHomeLevel,
    int? PauseExitLevel);

internal readonly record struct Id65SaveIntent(
    string Id,
    Id65AuthoringIntentDisposition Disposition,
    int LevelSlot,
    string SchemaId);

/// <summary>
/// Immutable, canonical ID65 authoring intent graph. It owns stable logical
/// identities only; it has no path, stream, BIN, CUE, or publisher surface.
/// </summary>
internal sealed class Id65AuthoringManifest
{
    private readonly ReadOnlyCollection<Id65SectorIntent> _sectors;
    private readonly ReadOnlyCollection<Id65TerrainVertexIntent> _vertices;
    private readonly ReadOnlyCollection<Id65RenderFaceIntent> _renderFaces;
    private readonly ReadOnlyCollection<Id65CollisionIntent> _collision;
    private readonly ReadOnlyCollection<Id65OcclusionIntent> _occlusion;
    private readonly ReadOnlyCollection<Id65TextureIntent> _textures;
    private readonly ReadOnlyCollection<Id65MobyIntent> _mobys;

    public Id65AuthoringManifest(
        string profileId,
        Id65LockedDisplayNameBase lockedBase,
        IEnumerable<Id65SectorIntent> sectors,
        IEnumerable<Id65TerrainVertexIntent> vertices,
        IEnumerable<Id65RenderFaceIntent> renderFaces,
        IEnumerable<Id65CollisionIntent> collision,
        IEnumerable<Id65OcclusionIntent> occlusion,
        IEnumerable<Id65TextureIntent> textures,
        IEnumerable<Id65MobyIntent> mobys,
        Id65SpawnIntent spawn,
        Id65MusicIntent music,
        Id65TotalsIntent totals,
        Id65ExitIntent exit,
        Id65SaveIntent save)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ProfileId = profileId;
        LockedBase = lockedBase;
        _sectors = Copy(sectors, nameof(sectors));
        _vertices = Copy(vertices, nameof(vertices));
        _renderFaces = Copy(renderFaces, nameof(renderFaces));
        _collision = Copy(collision, nameof(collision));
        _occlusion = Copy(occlusion, nameof(occlusion));
        _textures = Copy(textures, nameof(textures));
        _mobys = Copy(mobys, nameof(mobys));
        Spawn = spawn;
        Music = music;
        Totals = totals;
        Exit = exit;
        Save = save;
        CanonicalJson = BuildCanonicalJson();
        CanonicalSha256 = Hash(Encoding.UTF8.GetBytes(CanonicalJson));
    }

    public string ProfileId { get; }
    public Id65LockedDisplayNameBase LockedBase { get; }
    public IReadOnlyList<Id65SectorIntent> Sectors => _sectors;
    public IReadOnlyList<Id65TerrainVertexIntent> Vertices => _vertices;
    public IReadOnlyList<Id65RenderFaceIntent> RenderFaces => _renderFaces;
    public IReadOnlyList<Id65CollisionIntent> Collision => _collision;
    public IReadOnlyList<Id65OcclusionIntent> Occlusion => _occlusion;
    public IReadOnlyList<Id65TextureIntent> Textures => _textures;
    public IReadOnlyList<Id65MobyIntent> Mobys => _mobys;
    public Id65SpawnIntent Spawn { get; }
    public Id65MusicIntent Music { get; }
    public Id65TotalsIntent Totals { get; }
    public Id65ExitIntent Exit { get; }
    public Id65SaveIntent Save { get; }
    public string CanonicalJson { get; }
    public string CanonicalSha256 { get; }

    private string BuildCanonicalJson()
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter json = new(buffer, new JsonWriterOptions { Indented = false }))
        {
            json.WriteStartObject();
            json.WriteString("profileId", ProfileId);
            WriteLockedBase(json, LockedBase);
            WriteSectors(json, _sectors.OrderBy(item => item.Id, StringComparer.Ordinal));
            WriteVertices(json, _vertices.OrderBy(item => item.Handle, StringComparer.Ordinal));
            WriteRenderFaces(json, _renderFaces.OrderBy(item => item.Id, StringComparer.Ordinal));
            WriteCollision(json, _collision.OrderBy(item => item.Id, StringComparer.Ordinal));
            WriteOcclusion(json, _occlusion.OrderBy(item => item.Id, StringComparer.Ordinal));
            WriteTextures(json, _textures.OrderBy(item => item.Id, StringComparer.Ordinal));
            WriteMobys(json, _mobys.OrderBy(item => item.Id, StringComparer.Ordinal));
            WriteSpawn(json, Spawn);
            WriteMusic(json, Music);
            WriteTotals(json, Totals);
            WriteExit(json, Exit);
            WriteSave(json, Save);
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteLockedBase(Utf8JsonWriter json, Id65LockedDisplayNameBase value)
    {
        json.WritePropertyName("lockedBase");
        json.WriteStartObject();
        json.WriteString("profileId", value.ProfileId);
        json.WriteString("imageSha256", value.ImageSha256);
        json.WriteString("row80DataSha256", value.Row80DataSha256);
        json.WriteString("modelSha256", value.ModelSha256);
        json.WriteNumber("modelByteLength", value.ModelByteLength);
        json.WriteNumber("usedModelByteLength", value.UsedModelByteLength);
        json.WriteNumber("zeroTailByteCount", value.ZeroTailByteCount);
        json.WriteEndObject();
    }

    private static void WriteSectors(Utf8JsonWriter json, IEnumerable<Id65SectorIntent> values)
    {
        json.WritePropertyName("sectors");
        json.WriteStartArray();
        foreach (Id65SectorIntent value in values)
        {
            json.WriteStartObject();
            json.WriteString("id", value.Id);
            json.WriteNumber("sectorIndex", value.SectorIndex);
            WritePoint(json, "encodingOrigin", value.EncodingOrigin);
            WritePoint(json, "cullCenter", value.CullCenter);
            json.WriteNumber("cullRadius", value.CullRadius);
            json.WriteNumber("cullFlags", value.CullFlags);
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private static void WriteVertices(Utf8JsonWriter json, IEnumerable<Id65TerrainVertexIntent> values)
    {
        json.WritePropertyName("vertices");
        json.WriteStartArray();
        foreach (Id65TerrainVertexIntent value in values)
        {
            json.WriteStartObject();
            json.WriteString("handle", value.Handle);
            WritePoint(json, "point", value.Point);
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private static void WriteRenderFaces(Utf8JsonWriter json, IEnumerable<Id65RenderFaceIntent> values)
    {
        json.WritePropertyName("renderFaces");
        json.WriteStartArray();
        foreach (Id65RenderFaceIntent value in values)
        {
            json.WriteStartObject();
            json.WriteString("id", value.Id);
            json.WriteString("sectorId", value.SectorId);
            json.WriteString("textureIntentId", value.TextureIntentId);
            WriteHandles(json, "low", value.LowA, value.LowB, value.LowC);
            WriteHandles(json, "high", value.HighA, value.HighB, value.HighC);
            json.WriteString("orderPolicy", value.OrderPolicy.ToString());
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private static void WriteCollision(Utf8JsonWriter json, IEnumerable<Id65CollisionIntent> values)
    {
        json.WritePropertyName("collision");
        json.WriteStartArray();
        foreach (Id65CollisionIntent value in values)
        {
            json.WriteStartObject();
            json.WriteString("id", value.Id);
            json.WriteNumber("triangleIndex", value.TriangleIndex);
            WriteHandles(json, "vertices", value.A, value.B, value.C);
            json.WriteNumber("assignment", value.Assignment);
            WriteCell(json, "targetCell", value.TargetCell);
            json.WriteString("windingPolicy", value.WindingPolicy.ToString());
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private static void WriteOcclusion(Utf8JsonWriter json, IEnumerable<Id65OcclusionIntent> values)
    {
        json.WritePropertyName("occlusion");
        json.WriteStartArray();
        foreach (Id65OcclusionIntent value in values)
        {
            json.WriteStartObject();
            json.WriteString("id", value.Id);
            json.WriteString("sectorId", value.SectorId);
            json.WriteNumber("groupIndex", value.GroupIndex);
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private static void WriteTextures(Utf8JsonWriter json, IEnumerable<Id65TextureIntent> values)
    {
        json.WritePropertyName("textures");
        json.WriteStartArray();
        foreach (Id65TextureIntent value in values)
        {
            json.WriteStartObject();
            json.WriteString("id", value.Id);
            json.WriteString("disposition", value.Disposition.ToString());
            json.WriteNumber("destinationRecordIndex", value.DestinationRecordIndex);
            json.WriteString("sourceHandle", value.SourceHandle);
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private static void WriteMobys(Utf8JsonWriter json, IEnumerable<Id65MobyIntent> values)
    {
        json.WritePropertyName("mobys");
        json.WriteStartArray();
        foreach (Id65MobyIntent value in values)
        {
            json.WriteStartObject();
            json.WriteString("id", value.Id);
            json.WriteString("disposition", value.Disposition.ToString());
            json.WriteNumber("trueIndex", value.TrueIndex);
            json.WriteNumber("rawX", value.RawX);
            json.WriteNumber("rawY", value.RawY);
            json.WriteNumber("rawZ", value.RawZ);
            json.WriteString("dependencyBundleId", value.DependencyBundleId);
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private static void WriteSpawn(Utf8JsonWriter json, Id65SpawnIntent value)
    {
        json.WritePropertyName("spawn");
        json.WriteStartObject();
        json.WriteString("id", value.Id);
        json.WriteString("disposition", value.Disposition.ToString());
        json.WriteString("playerMobyId", value.PlayerMobyId);
        json.WriteNumber("landingRawX", value.LandingRawX);
        json.WriteNumber("landingRawY", value.LandingRawY);
        json.WriteNumber("landingRawZ", value.LandingRawZ);
        json.WriteNumber("playerRawZ", value.PlayerRawZ);
        json.WriteNumber("groundRawZ", value.GroundRawZ);
        json.WriteNumber("yawByte", value.YawByte);
        json.WriteEndObject();
    }

    private static void WriteMusic(Utf8JsonWriter json, Id65MusicIntent value)
    {
        json.WritePropertyName("music");
        json.WriteStartObject();
        json.WriteString("id", value.Id);
        json.WriteString("disposition", value.Disposition.ToString());
        json.WriteNumber("executableSlot", value.ExecutableSlot);
        WriteNullableNumber(json, "trackId", value.TrackId);
        json.WriteEndObject();
    }

    private static void WriteTotals(Utf8JsonWriter json, Id65TotalsIntent value)
    {
        json.WritePropertyName("totals");
        json.WriteStartObject();
        json.WriteString("id", value.Id);
        json.WriteString("disposition", value.Disposition.ToString());
        json.WriteNumber("levelSlot", value.LevelSlot);
        WriteNullableNumber(json, "gems", value.Gems);
        WriteNullableNumber(json, "dragons", value.Dragons);
        WriteNullableNumber(json, "eggs", value.Eggs);
        json.WriteEndObject();
    }

    private static void WriteExit(Utf8JsonWriter json, Id65ExitIntent value)
    {
        json.WritePropertyName("exit");
        json.WriteStartObject();
        json.WriteString("id", value.Id);
        json.WriteString("disposition", value.Disposition.ToString());
        json.WriteNumber("levelSlot", value.LevelSlot);
        WriteNullableNumber(json, "returnHomeLevel", value.ReturnHomeLevel);
        WriteNullableNumber(json, "pauseExitLevel", value.PauseExitLevel);
        json.WriteEndObject();
    }

    private static void WriteSave(Utf8JsonWriter json, Id65SaveIntent value)
    {
        json.WritePropertyName("save");
        json.WriteStartObject();
        json.WriteString("id", value.Id);
        json.WriteString("disposition", value.Disposition.ToString());
        json.WriteNumber("levelSlot", value.LevelSlot);
        json.WriteString("schemaId", value.SchemaId);
        json.WriteEndObject();
    }

    private static void WritePoint(Utf8JsonWriter json, string name, Id65AuthoringPoint point)
    {
        json.WritePropertyName(name);
        json.WriteStartArray();
        json.WriteNumberValue(point.X);
        json.WriteNumberValue(point.Y);
        json.WriteNumberValue(point.Z);
        json.WriteEndArray();
    }

    private static void WriteCell(Utf8JsonWriter json, string name, Id65AuthoringCollisionCell cell)
    {
        json.WritePropertyName(name);
        json.WriteStartArray();
        json.WriteNumberValue(cell.X);
        json.WriteNumberValue(cell.Y);
        json.WriteNumberValue(cell.Z);
        json.WriteEndArray();
    }

    private static void WriteHandles(Utf8JsonWriter json, string name, string a, string b, string c)
    {
        json.WritePropertyName(name);
        json.WriteStartArray();
        json.WriteStringValue(a);
        json.WriteStringValue(b);
        json.WriteStringValue(c);
        json.WriteEndArray();
    }

    private static void WriteNullableNumber(Utf8JsonWriter json, string name, int? value)
    {
        if (value.HasValue)
            json.WriteNumber(name, value.Value);
        else
            json.WriteNull(name);
    }

    private static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return Array.AsReadOnly(values.ToArray());
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

internal sealed record Id65AuthoringCompilerLimits(
    int ModelByteCapacity = UnusedLevel65RemoteBlankIsolationConstruction.ModelByteLength,
    int AvailableModelTailBytes = UnusedLevel65RemoteBlankIsolationConstruction.SourceZeroTailByteCount,
    int MaximumOutputUsedModelBytes = UnusedLevel65RemoteBlankIsolationConstruction.ModelByteLength,
    int MaximumSceneSectorCount = 256,
    int CollisionBlockCapacityBytes = UnusedLevel65RemoteBlankIsolationConstruction.CollisionBlocksCapacityBytes,
    int MaximumCollisionTriangleCount = UnusedLevel65RemoteBlankIsolationConstruction.CollisionTriangleCount,
    int MaximumTextureRecordIndex = 255,
    int MaximumMobyTrueIndex = UnusedLevel65FullAuthoringConstructionTemplate.ObjectRecordCount - 1);

internal sealed record Id65ModelComponentRelocation(
    string StableId,
    int SourceRelativeOffset,
    int SourceByteLength,
    int OutputRelativeOffset,
    int OutputByteLength,
    string SourceSha256,
    string OutputSha256,
    bool ContentsPreserved);

internal readonly record struct Id65LogicalAddress(string Space, int Primary, int Secondary);

internal sealed record Id65StableHandleRebase(
    string StableId,
    string Kind,
    Id65LogicalAddress? Source,
    Id65LogicalAddress? Output);

internal sealed record Id65AuthoringModelCapacityReadback(
    int ModelByteCapacity,
    int SourceUsedModelBytes,
    int OutputUsedModelBytes,
    int EnvironmentGrowthBytes,
    int SourceZeroTailBytes,
    int OutputZeroTailBytes,
    int SceneSectorCount,
    int CollisionTriangleCount,
    int CollisionBlocksUsedBytes,
    int CollisionBlocksCapacityBytes,
    int HighestTextureRecordIndex,
    int HighestMobyTrueIndex);

internal sealed class Id65CompiledAuthoringModel
{
    private readonly byte[] _sourceModel;
    private readonly byte[] _outputModel;
    private readonly ReadOnlyCollection<Id65ModelComponentRelocation> _relocations;
    private readonly ReadOnlyCollection<Id65StableHandleRebase> _handleRebases;

    internal Id65CompiledAuthoringModel(
        string profileId,
        Id65AuthoringManifest manifest,
        string sourceModelSha256,
        string outputModelSha256,
        byte[] sourceModel,
        byte[] outputModel,
        int changedModelByteCount,
        int diffRangeCount,
        string diffManifestSha256,
        IEnumerable<Id65ModelComponentRelocation> relocations,
        string relocationMapSha256,
        IEnumerable<Id65StableHandleRebase> handleRebases,
        string handleRebaseMapSha256,
        long renderFaceNormalZ,
        long collisionNormalZ,
        Id65AuthoringModelCapacityReadback capacity,
        string deterministicPlanSha256)
    {
        ProfileId = profileId;
        Manifest = manifest;
        ManifestSha256 = manifest.CanonicalSha256;
        SourceModelSha256 = sourceModelSha256;
        OutputModelSha256 = outputModelSha256;
        _sourceModel = sourceModel.ToArray();
        _outputModel = outputModel.ToArray();
        ChangedModelByteCount = changedModelByteCount;
        DiffRangeCount = diffRangeCount;
        DiffManifestSha256 = diffManifestSha256;
        _relocations = Array.AsReadOnly(relocations.ToArray());
        RelocationMapSha256 = relocationMapSha256;
        _handleRebases = Array.AsReadOnly(handleRebases.ToArray());
        HandleRebaseMapSha256 = handleRebaseMapSha256;
        RenderFaceNormalZ = renderFaceNormalZ;
        CollisionNormalZ = collisionNormalZ;
        Capacity = capacity;
        DeterministicPlanSha256 = deterministicPlanSha256;
    }

    public string ProfileId { get; }
    public Id65AuthoringManifest Manifest { get; }
    public string ManifestSha256 { get; }
    public string SourceModelSha256 { get; }
    public string OutputModelSha256 { get; }
    public int ChangedModelByteCount { get; }
    public int DiffRangeCount { get; }
    public string DiffManifestSha256 { get; }
    public IReadOnlyList<Id65ModelComponentRelocation> Relocations => _relocations;
    public string RelocationMapSha256 { get; }
    public IReadOnlyList<Id65StableHandleRebase> HandleRebases => _handleRebases;
    public string HandleRebaseMapSha256 { get; }
    public long RenderFaceNormalZ { get; }
    public long CollisionNormalZ { get; }
    public Id65AuthoringModelCapacityReadback Capacity { get; }
    public string DeterministicPlanSha256 { get; }
    public bool ImmutableLockedDisplayNameBaseVerified => true;
    public bool StableIntentIdsVerified => true;
    public bool ExplicitHpLpPairingVerified => true;
    public bool RenderAndCollisionOrderSeparated => true;
    public bool NegativeCollisionWindingVerified => true;
    public bool CapacityAndConflictValidationComplete => true;
    public bool RelocationMapExactInverseVerified => true;
    public bool HandleRebaseMapExactInverseVerified => true;
    public bool ModelExactInverseVerified => true;
    public bool DeterministicReadbackRequired => true;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool WritesCue => false;
    public bool AppIntegrated => false;
    public bool CreateBinEnabled => false;
    public bool NormalCreateBinEnabled => false;
    public bool ReleaseIntegrated => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool RetiredPublisherCalled => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;

    internal byte[] CopySourceModel() => _sourceModel.ToArray();
    internal byte[] CopyOutputModel() => _outputModel.ToArray();
}

/// <summary>
/// Pure in-memory compiler boundary for the first minimal one-pad manifest.
/// It consumes the exact v2 negative-winding construction as an immutable
/// readback template, never the failed positive-winding bytes as authority.
/// </summary>
internal static class Id65AuthoringModelCompiler
{
    public const string ProfileId = "id65-authoring-model-compiler-static-in-memory-v1";
    public const string MinimalManifestProfileId =
        "id65-authoring-manifest-minimal-v2-equivalent-one-pad-v1";
    public const string ExpectedMinimalManifestSha256 =
        "4c2b27f175b67b6abedb06632a14b4d0093d44b8078813be7f681c7d8873b789";
    public const string ExpectedMinimalRelocationMapSha256 =
        "5493511a3fa5b764a938680bce6545264623362cabc45f55464a3744ac16ac31";
    public const string ExpectedMinimalHandleRebaseMapSha256 =
        "c5dcc61650cb4b01c9eb840a6ebd2030de37654c5f1a7f556bd031a2f2ae04a4";
    public const string ExpectedMinimalModelDiffManifestSha256 =
        "2fa64f8ef0b3931f45b8eaf1206b6996e4603f44a8253e576bbb73d78b37dfdc";
    public const string ExpectedMinimalDeterministicPlanSha256 =
        "cc9bf65aa1680eef105cfb53eecf480a202669a07060bfb956a45dfb8dd44932";
    public const int ExpectedMinimalChangedModelByteCount = 484_324;
    public const int ExpectedMinimalModelDiffRangeCount = 55_382;

    private const string SectorId = "sector.remote-pad.216";
    private const string VertexA = "vertex.remote-pad.a";
    private const string VertexB = "vertex.remote-pad.b";
    private const string VertexC = "vertex.remote-pad.c";
    private const string RenderFaceId = "terrain.remote-pad.face.0";
    private const string CollisionId = "collision.remote-pad.t13995";
    private const string OcclusionId = "occlusion.remote-pad.group0";
    private const string TextureId = "texture.locked.record25";
    private const string MobyId = "moby.player-anchor.t92";
    private const string SpawnId = "spawn.remote-pad";
    private const string MusicId = "music.slot35";
    private const string TotalsId = "totals.slot65";
    private const string ExitId = "exit.slot65";
    private const string SaveId = "save.slot65";

    private static readonly Id65AuthoringPoint PointA = new(272, 272, 512);
    private static readonly Id65AuthoringPoint PointB = new(496, 272, 512);
    private static readonly Id65AuthoringPoint PointC = new(384, 496, 512);

    public static Id65AuthoringManifest CreateMinimalV2EquivalentManifest() =>
        new(
            MinimalManifestProfileId,
            new(
                UnusedLevel65DisplayNameCandidateExporter.ProfileId,
                UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedSourceImageSha256,
                UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedSourceDataSha256,
                UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedSourceModelSha256,
                UnusedLevel65RemoteBlankIsolationConstruction.ModelByteLength,
                UnusedLevel65RemoteBlankIsolationConstruction.SourceUsedModelByteLength,
                UnusedLevel65RemoteBlankIsolationConstruction.SourceZeroTailByteCount),
            [new(SectorId, 216, new(256, 256, 512), new(384, 384, 512), 192, 0)],
            [new(VertexA, PointA), new(VertexB, PointB), new(VertexC, PointC)],
            [new(
                RenderFaceId,
                SectorId,
                TextureId,
                VertexA,
                VertexB,
                VertexC,
                VertexA,
                VertexB,
                VertexC,
                Id65RenderFaceOrderPolicy.DeclaredAbcGpuOrder)],
            [new(
                CollisionId,
                13_995,
                VertexA,
                VertexC,
                VertexB,
                0,
                new(1, 1, 2),
                Id65CollisionWindingPolicy.NegativeNormalZAcb)],
            [new(OcclusionId, SectorId, 0)],
            [new(TextureId, Id65AuthoringIntentDisposition.PreserveLockedBase, 25, "locked-base.record25")],
            [new(
                MobyId,
                Id65AuthoringIntentDisposition.AuthorExact,
                92,
                UnusedLevel65RemoteBlankIsolationConstruction.AuthoredRawX,
                UnusedLevel65RemoteBlankIsolationConstruction.AuthoredRawY,
                UnusedLevel65RemoteBlankIsolationConstruction.PlayerRawZ,
                "locked-base.t92")],
            new(
                SpawnId,
                Id65AuthoringIntentDisposition.AuthorExact,
                MobyId,
                UnusedLevel65RemoteBlankIsolationConstruction.AuthoredRawX,
                UnusedLevel65RemoteBlankIsolationConstruction.AuthoredRawY,
                UnusedLevel65RemoteBlankIsolationConstruction.LandingRawZ,
                UnusedLevel65RemoteBlankIsolationConstruction.PlayerRawZ,
                UnusedLevel65RemoteBlankIsolationConstruction.GroundRawZ,
                UnusedLevel65RemoteBlankIsolationConstruction.YawByte),
            new(MusicId, Id65AuthoringIntentDisposition.PreserveLockedBase, 35, null),
            new(TotalsId, Id65AuthoringIntentDisposition.PreserveLockedBase, 65, null, null, null),
            new(ExitId, Id65AuthoringIntentDisposition.PreserveLockedBase, 65, null, null),
            new(SaveId, Id65AuthoringIntentDisposition.PreserveLockedBase, 65, "locked-base"));

    public static Id65CompiledAuthoringModel Compile(
        Id65AuthoringManifest manifest,
        UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan v2Template,
        Id65AuthoringCompilerLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(v2Template);
        limits ??= new();
        ValidateManifest(manifest, out long renderNormalZ, out long collisionNormalZ);
        ValidateV2Template(v2Template, collisionNormalZ);
        ValidateCapacity(manifest, v2Template, limits);

        UnusedLevel65RemoteBlankStructuralPatch modelPatch = v2Template.StructuralPatches
            .Single(patch => patch.Kind == "id65-model");
        byte[] sourceModel = modelPatch.Before.ToArray();
        byte[] outputModel = modelPatch.After.ToArray();
        string sourceHash = Hash(sourceModel);
        string outputHash = Hash(outputModel);
        RequireHash(sourceHash, UnusedLevel65RemoteBlankIsolationConstruction.ExpectedSourceModelSha256, "locked model");
        RequireHash(outputHash, UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputModelSha256, "v2 model");

        Id65ModelComponentRelocation[] relocations = BuildRelocations(v2Template);
        ValidateRelocations(relocations, v2Template);
        string relocationHash = HashRelocations(relocations);
        Id65ModelComponentRelocation[] inverseRelocations = InvertRelocations(relocations).ToArray();
        if (!InvertRelocations(inverseRelocations).SequenceEqual(relocations))
            throw new InvalidDataException("The ID65 component relocation map is not exactly invertible.");

        Id65StableHandleRebase[] rebases = BuildHandleRebases(manifest);
        ValidateHandleRebases(rebases);
        string rebaseHash = HashHandleRebases(rebases);
        Id65StableHandleRebase[] inverseRebases = InvertHandleRebases(rebases).ToArray();
        if (!InvertHandleRebases(inverseRebases).SequenceEqual(rebases))
            throw new InvalidDataException("The ID65 stable-handle rebase map is not exactly invertible.");

        ModelDiffProof diff = BuildModelDiffProof(sourceModel, outputModel);
        Id65AuthoringModelCapacityReadback capacity = new(
            limits.ModelByteCapacity,
            v2Template.SourceUsedModelByteLength,
            v2Template.OutputUsedModelByteLength,
            v2Template.Scene.EnvironmentGrowthBytes,
            v2Template.SourceZeroTailByteCount,
            v2Template.OutputZeroTailByteCount,
            v2Template.Scene.OutputSectorCount,
            v2Template.Collision.TriangleCount,
            v2Template.Collision.OutputBlocksUsedBytes,
            v2Template.Collision.BlocksCapacityBytes,
            manifest.Textures.Max(texture => texture.DestinationRecordIndex),
            manifest.Mobys.Max(moby => moby.TrueIndex));
        string deterministicHash = Hash(Encoding.UTF8.GetBytes(string.Join('\n',
        [
            ProfileId,
            manifest.CanonicalSha256,
            sourceHash,
            outputHash,
            diff.ManifestSha256,
            relocationHash,
            rebaseHash,
            renderNormalZ.ToString(CultureInfo.InvariantCulture),
            collisionNormalZ.ToString(CultureInfo.InvariantCulture),
            v2Template.DeterministicPlanSha256
        ])));
        if (manifest.CanonicalSha256 != ExpectedMinimalManifestSha256 ||
            diff.ChangedByteCount != ExpectedMinimalChangedModelByteCount ||
            diff.RangeCount != ExpectedMinimalModelDiffRangeCount ||
            diff.ManifestSha256 != ExpectedMinimalModelDiffManifestSha256 ||
            relocationHash != ExpectedMinimalRelocationMapSha256 ||
            rebaseHash != ExpectedMinimalHandleRebaseMapSha256 ||
            deterministicHash != ExpectedMinimalDeterministicPlanSha256)
        {
            throw new InvalidDataException("The deterministic minimal-v2 authoring compiler pins changed.");
        }

        Id65CompiledAuthoringModel compiled = new(
            ProfileId,
            manifest,
            sourceHash,
            outputHash,
            sourceModel,
            outputModel,
            diff.ChangedByteCount,
            diff.RangeCount,
            diff.ManifestSha256,
            relocations,
            relocationHash,
            rebases,
            rebaseHash,
            renderNormalZ,
            collisionNormalZ,
            capacity,
            deterministicHash);
        byte[] applied = ApplyModelTransactional(compiled, sourceModel, reverse: false);
        byte[] reversed = ApplyModelTransactional(compiled, applied, reverse: true);
        if (!applied.SequenceEqual(outputModel) || !reversed.SequenceEqual(sourceModel))
            throw new InvalidDataException("The compiled ID65 model is not exactly invertible to the locked base.");
        return compiled;
    }

    internal static byte[] ApplyModelTransactional(
        Id65CompiledAuthoringModel compiled,
        byte[] input,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(compiled);
        ArgumentNullException.ThrowIfNull(input);
        byte[] expected = reverse ? compiled.CopyOutputModel() : compiled.CopySourceModel();
        byte[] replacement = reverse ? compiled.CopySourceModel() : compiled.CopyOutputModel();
        if (!input.SequenceEqual(expected))
            throw new InvalidDataException($"The ID65 model {(reverse ? "afterimage" : "preimage")} changed.");
        byte[] output = replacement.ToArray();
        RequireHash(
            Hash(output),
            reverse ? compiled.SourceModelSha256 : compiled.OutputModelSha256,
            reverse ? "inverse locked model" : "compiled model");
        return output;
    }

    internal static IReadOnlyList<Id65ModelComponentRelocation> InvertRelocations(
        IEnumerable<Id65ModelComponentRelocation> relocations)
    {
        ArgumentNullException.ThrowIfNull(relocations);
        return relocations.Select(item => new Id65ModelComponentRelocation(
            item.StableId,
            item.OutputRelativeOffset,
            item.OutputByteLength,
            item.SourceRelativeOffset,
            item.SourceByteLength,
            item.OutputSha256,
            item.SourceSha256,
            item.ContentsPreserved)).ToArray();
    }

    internal static IReadOnlyList<Id65StableHandleRebase> InvertHandleRebases(
        IEnumerable<Id65StableHandleRebase> rebases)
    {
        ArgumentNullException.ThrowIfNull(rebases);
        return rebases.Select(item => item with { Source = item.Output, Output = item.Source }).ToArray();
    }

    private static void ValidateManifest(
        Id65AuthoringManifest manifest,
        out long renderNormalZ,
        out long collisionNormalZ)
    {
        Id65AuthoringManifest expected = CreateMinimalV2EquivalentManifest();
        ValidateStableTokens(manifest);
        if (manifest.ProfileId != MinimalManifestProfileId || manifest.LockedBase != expected.LockedBase)
            throw new InvalidDataException("The immutable locked display-name base identity changed.");
        RequireExactSet(manifest.Sectors, expected.Sectors, item => item.Id, "sector intents");
        RequireExactSet(manifest.Vertices, expected.Vertices, item => item.Handle, "terrain vertex handles");
        RequireExactSet(manifest.RenderFaces, expected.RenderFaces, item => item.Id, "render-face intents");
        RequireExactSet(manifest.Collision, expected.Collision, item => item.Id, "collision intents");
        RequireExactSet(manifest.Occlusion, expected.Occlusion, item => item.Id, "occlusion intents");
        RequireExactSet(manifest.Textures, expected.Textures, item => item.Id, "texture intents");
        RequireExactSet(manifest.Mobys, expected.Mobys, item => item.Id, "Moby intents");
        if (manifest.Spawn != expected.Spawn || manifest.Music != expected.Music ||
            manifest.Totals != expected.Totals || manifest.Exit != expected.Exit || manifest.Save != expected.Save)
        {
            throw new InvalidDataException("A spawn/music/totals/exit/save ownership intent conflicts with the minimal v2 contract.");
        }

        Dictionary<string, Id65AuthoringPoint> points = manifest.Vertices
            .ToDictionary(vertex => vertex.Handle, vertex => vertex.Point, StringComparer.Ordinal);
        Id65RenderFaceIntent render = manifest.RenderFaces.Single();
        Id65CollisionIntent collision = manifest.Collision.Single();
        string[] low = [render.LowA, render.LowB, render.LowC];
        string[] high = [render.HighA, render.HighB, render.HighC];
        string[] collisionHandles = [collision.A, collision.B, collision.C];
        if (!low.SequenceEqual(high) || low.Distinct(StringComparer.Ordinal).Count() != 3 ||
            collisionHandles.Distinct(StringComparer.Ordinal).Count() != 3 ||
            low.Any(handle => !points.ContainsKey(handle)) || collisionHandles.Any(handle => !points.ContainsKey(handle)) ||
            !low.Order(StringComparer.Ordinal).SequenceEqual(collisionHandles.Order(StringComparer.Ordinal)))
        {
            throw new InvalidDataException("The HP/LP render topology and collision point set conflict.");
        }
        if (render.OrderPolicy != Id65RenderFaceOrderPolicy.DeclaredAbcGpuOrder ||
            collision.WindingPolicy != Id65CollisionWindingPolicy.NegativeNormalZAcb ||
            low.SequenceEqual(collisionHandles))
        {
            throw new InvalidDataException("Render-face order and collision winding must remain explicitly separate.");
        }
        renderNormalZ = NormalZ(points[render.LowA], points[render.LowB], points[render.LowC]);
        collisionNormalZ = NormalZ(points[collision.A], points[collision.B], points[collision.C]);
        if (renderNormalZ != 50_176 || collisionNormalZ != -50_176)
            throw new InvalidDataException("The minimal pad is not exact ABC render / ACB negative collision winding.");
    }

    private static void ValidateStableTokens(Id65AuthoringManifest manifest)
    {
        List<string> ownerIds =
        [
            .. manifest.Sectors.Select(item => item.Id),
            .. manifest.Vertices.Select(item => item.Handle),
            .. manifest.RenderFaces.Select(item => item.Id),
            .. manifest.Collision.Select(item => item.Id),
            .. manifest.Occlusion.Select(item => item.Id),
            .. manifest.Textures.Select(item => item.Id),
            .. manifest.Mobys.Select(item => item.Id),
            manifest.Spawn.Id,
            manifest.Music.Id,
            manifest.Totals.Id,
            manifest.Exit.Id,
            manifest.Save.Id
        ];
        string[] referencedHandles =
        [
            .. manifest.RenderFaces.SelectMany(item => new[]
            {
                item.SectorId,
                item.TextureIntentId,
                item.LowA,
                item.LowB,
                item.LowC,
                item.HighA,
                item.HighB,
                item.HighC
            }),
            .. manifest.Collision.SelectMany(item => new[] { item.A, item.B, item.C }),
            .. manifest.Occlusion.Select(item => item.SectorId),
            .. manifest.Textures.Select(item => item.SourceHandle),
            .. manifest.Mobys.Select(item => item.DependencyBundleId),
            manifest.Spawn.PlayerMobyId,
            manifest.Save.SchemaId
        ];
        foreach (string id in ownerIds.Concat(referencedHandles).Prepend(manifest.ProfileId))
        {
            if (!IsStableToken(id))
            {
                throw new InvalidDataException($"Stable ID/handle `{id}` is not canonical lowercase ASCII.");
            }
        }
        if (ownerIds.Distinct(StringComparer.Ordinal).Count() != ownerIds.Count)
            throw new InvalidDataException("Stable ID/handle conflict: every manifest identity must be globally unique.");
        HashSet<string> sectorIds = manifest.Sectors.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        HashSet<string> vertexHandles = manifest.Vertices.Select(item => item.Handle).ToHashSet(StringComparer.Ordinal);
        HashSet<string> textureIds = manifest.Textures.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        HashSet<string> mobyIds = manifest.Mobys.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        if (manifest.RenderFaces.Any(item => !sectorIds.Contains(item.SectorId) ||
                !textureIds.Contains(item.TextureIntentId) ||
                new[] { item.LowA, item.LowB, item.LowC, item.HighA, item.HighB, item.HighC }
                    .Any(handle => !vertexHandles.Contains(handle))) ||
            manifest.Collision.Any(item => new[] { item.A, item.B, item.C }
                .Any(handle => !vertexHandles.Contains(handle))) ||
            manifest.Occlusion.Any(item => !sectorIds.Contains(item.SectorId)) ||
            !mobyIds.Contains(manifest.Spawn.PlayerMobyId))
        {
            throw new InvalidDataException("A manifest intent references an unknown stable ID/handle.");
        }
    }

    private static bool IsStableToken(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.All(character =>
            (character >= 'a' && character <= 'z') ||
            (character >= '0' && character <= '9') ||
            character is '.' or '-' or ':');

    private static void ValidateV2Template(
        UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan plan,
        long manifestCollisionNormalZ)
    {
        if (plan.ProfileId != UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ProfileId ||
            plan.SourceImageSha256 != UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedSourceImageSha256 ||
            plan.SourceDataSha256 != UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedSourceDataSha256 ||
            plan.SourceModelSha256 != UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedSourceModelSha256 ||
            plan.OutputModelSha256 != UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputModelSha256 ||
            plan.OutputDataSha256 != UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputDataSha256 ||
            plan.ModelByteLength != UnusedLevel65RemoteBlankIsolationConstruction.ModelByteLength ||
            plan.Scene.NewSectorIndex != 216 || plan.Scene.OutputSectorCount != 217 ||
            plan.Scene.MaterialTextureId != 25 || !plan.Scene.ExactHpLpPairing ||
            plan.Collision.ReusedTriangleIndex != 13_995 || plan.Collision.UpwardWinding ||
            plan.Collision.OutputTriangleHex != "10011C701001380000020000" ||
            plan.Repair.RepairedTriangleHex != plan.Collision.OutputTriangleHex ||
            plan.Repair.RepairedNormalZ != manifestCollisionNormalZ ||
            !plan.Repair.PointSetIdentical || !plan.Repair.AcbOrderVerified ||
            !plan.Repair.FourByteAllowlistComplete || !plan.Repair.ByteInverseVerified ||
            !plan.Repair.CollisionTreeIdentical || !plan.Repair.CollisionBlocksIdentical ||
            !plan.Repair.CollisionAssignmentIdentical || !plan.Repair.OcclusionIdentical ||
            !plan.Repair.SpawnIdentical || !plan.Repair.ProtectedDataIdentical ||
            !plan.PatchPreimagesVerified || !plan.PatchAllowlistComplete || !plan.ByteInverseVerified ||
            !plan.ProtectedSubfilesPreserved || !plan.InheritedObjectRowsPreserved ||
            !plan.RetailWadEntriesExcluded || !plan.ExecutableExcluded ||
            plan.SourceImageMutationPossible || plan.DisposableRuntimeCandidateAuthorized ||
            plan.PromotionAuthorized || plan.NormalCreateBinEnabled)
        {
            throw new InvalidDataException("The exact negative-winding v2 in-memory template changed or weakened.");
        }
        if (plan.Spawn.LandingRawX != UnusedLevel65RemoteBlankIsolationConstruction.AuthoredRawX ||
            plan.Spawn.LandingRawY != UnusedLevel65RemoteBlankIsolationConstruction.AuthoredRawY ||
            plan.Spawn.LandingRawZ != UnusedLevel65RemoteBlankIsolationConstruction.LandingRawZ ||
            plan.Spawn.PlayerAnchorTrueIndex != 92 ||
            plan.Spawn.PlayerRawZ != UnusedLevel65RemoteBlankIsolationConstruction.PlayerRawZ ||
            plan.Spawn.GroundRawZ != UnusedLevel65RemoteBlankIsolationConstruction.GroundRawZ ||
            !plan.Spawn.AtomicXyOnly)
        {
            throw new InvalidDataException("The exact v2 spawn/T92 readback changed.");
        }
    }

    private static void ValidateCapacity(
        Id65AuthoringManifest manifest,
        UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan plan,
        Id65AuthoringCompilerLimits limits)
    {
        if (limits.ModelByteCapacity < plan.ModelByteLength)
            throw new InvalidDataException("ID65 model byte capacity is insufficient.");
        if (limits.AvailableModelTailBytes < plan.Scene.EnvironmentGrowthBytes)
            throw new InvalidDataException("ID65 model tail capacity is insufficient for sector 216.");
        if (limits.MaximumOutputUsedModelBytes < plan.OutputUsedModelByteLength)
            throw new InvalidDataException("ID65 used-model capacity is insufficient.");
        if (limits.MaximumSceneSectorCount < plan.Scene.OutputSectorCount)
            throw new InvalidDataException("ID65 scene-sector capacity is insufficient.");
        if (limits.CollisionBlockCapacityBytes < plan.Collision.OutputBlocksUsedBytes)
            throw new InvalidDataException("ID65 collision-block capacity is insufficient.");
        if (limits.MaximumCollisionTriangleCount < plan.Collision.TriangleCount)
            throw new InvalidDataException("ID65 collision-triangle capacity is insufficient.");
        if (manifest.Textures.Any(texture => texture.DestinationRecordIndex < 0 ||
                texture.DestinationRecordIndex > limits.MaximumTextureRecordIndex))
            throw new InvalidDataException("ID65 texture-record capacity is insufficient.");
        if (manifest.Mobys.Any(moby => moby.TrueIndex < 0 || moby.TrueIndex > limits.MaximumMobyTrueIndex))
            throw new InvalidDataException("ID65 Moby-row capacity is insufficient.");
    }

    private static Id65ModelComponentRelocation[] BuildRelocations(
        UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan plan)
    {
        UnusedLevel65RemoteBlankComponentProof[] components = plan.Components
            .OrderBy(component => component.SourceRelativeOffset)
            .ToArray();
        List<Id65ModelComponentRelocation> result = new(components.Length);
        for (int index = 0; index < components.Length; index++)
        {
            UnusedLevel65RemoteBlankComponentProof component = components[index];
            int outputEnd = index + 1 < components.Length
                ? components[index + 1].OutputRelativeOffset
                : plan.OutputUsedModelByteLength;
            result.Add(new(
                $"component.{component.Name.Replace(' ', '-')}",
                component.SourceRelativeOffset,
                component.ByteLength,
                component.OutputRelativeOffset,
                checked(outputEnd - component.OutputRelativeOffset),
                component.SourceSha256,
                component.OutputSha256,
                component.ContentsPreserved));
        }
        return result.ToArray();
    }

    private static void ValidateRelocations(
        IReadOnlyList<Id65ModelComponentRelocation> relocations,
        UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan plan)
    {
        if (relocations.Count != 9 || relocations[0].StableId != "component.texture" ||
            relocations[^1].StableId != "component.sound")
            throw new InvalidDataException("The exact nine-component ID65 relocation map changed.");
        for (int index = 0; index < relocations.Count; index++)
        {
            Id65ModelComponentRelocation item = relocations[index];
            if (item.SourceRelativeOffset < 0 || item.OutputRelativeOffset < 0 ||
                item.SourceByteLength <= 0 || item.OutputByteLength <= 0 ||
                item.SourceRelativeOffset + (long)item.SourceByteLength > plan.SourceUsedModelByteLength ||
                item.OutputRelativeOffset + (long)item.OutputByteLength > plan.OutputUsedModelByteLength)
            {
                throw new InvalidDataException($"Component relocation {item.StableId} exceeds model capacity.");
            }
            if (index > 0)
            {
                Id65ModelComponentRelocation previous = relocations[index - 1];
                if (previous.SourceRelativeOffset + previous.SourceByteLength != item.SourceRelativeOffset ||
                    previous.OutputRelativeOffset + previous.OutputByteLength != item.OutputRelativeOffset)
                {
                    throw new InvalidDataException("ID65 component relocation ranges overlap or leave an unowned gap.");
                }
            }
        }
        if (relocations[0].SourceRelativeOffset != 0 || relocations[0].OutputRelativeOffset != 0 ||
            relocations[^1].SourceRelativeOffset + relocations[^1].SourceByteLength != plan.SourceUsedModelByteLength ||
            relocations[^1].OutputRelativeOffset + relocations[^1].OutputByteLength != plan.OutputUsedModelByteLength)
        {
            throw new InvalidDataException("The ID65 relocation map does not own the exact used-model span.");
        }
    }

    private static Id65StableHandleRebase[] BuildHandleRebases(Id65AuthoringManifest manifest)
    {
        List<Id65StableHandleRebase> result =
        [
            new(SectorId, "scene-sector", null, new("scene-sector", 216, 0)),
            new($"{RenderFaceId}:lp", "low-detail-face", null, new("scene-sector", 216, 0)),
            new($"{RenderFaceId}:hp", "high-detail-face", null, new("scene-sector", 216, 0)),
            new(CollisionId, "collision-triangle", new("collision-triangle", 13_995, 0), new("collision-triangle", 13_995, 0)),
            new(OcclusionId, "occlusion-membership", null, new("occlusion-group", 0, 216)),
            new(TextureId, "texture-record", new("texture-record", 25, 0), new("texture-record", 25, 0)),
            new(MobyId, "moby-row", new("moby-row", 92, 0), new("moby-row", 92, 0)),
            new(SpawnId, "landing-record", new("landing-record", 0, 0), new("landing-record", 0, 0)),
            new(MusicId, "music-slot", new("music-slot", 35, 0), new("music-slot", 35, 0)),
            new(TotalsId, "totals-slot", new("level-slot", 65, 0), new("level-slot", 65, 0)),
            new(ExitId, "exit-slot", new("level-slot", 65, 0), new("level-slot", 65, 0)),
            new(SaveId, "save-slot", new("level-slot", 65, 0), new("level-slot", 65, 0))
        ];
        foreach (Id65TerrainVertexIntent vertex in manifest.Vertices.OrderBy(item => item.Handle, StringComparer.Ordinal))
        {
            int ordinal = vertex.Handle switch
            {
                VertexA => 0,
                VertexB => 1,
                VertexC => 2,
                _ => throw new InvalidDataException($"Unknown minimal-pad vertex handle {vertex.Handle}.")
            };
            result.Add(new($"{vertex.Handle}:lp", "low-detail-vertex", null, new("scene-sector", 216, ordinal)));
            result.Add(new($"{vertex.Handle}:hp", "high-detail-vertex", null, new("scene-sector", 216, ordinal)));
        }
        return result.OrderBy(item => item.StableId, StringComparer.Ordinal).ToArray();
    }

    private static void ValidateHandleRebases(IReadOnlyList<Id65StableHandleRebase> rebases)
    {
        if (rebases.Count != 18 ||
            rebases.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count() != rebases.Count ||
            rebases.Any(item => item.Output is null))
        {
            throw new InvalidDataException("The minimal manifest stable-handle rebase map is incomplete or conflicting.");
        }
    }

    private static ModelDiffProof BuildModelDiffProof(byte[] before, byte[] after)
    {
        if (before.Length != after.Length)
            throw new InvalidDataException("The in-memory ID65 model patch changed fixed model capacity.");
        int changed = 0;
        int rangeCount = 0;
        StringBuilder manifest = new();
        int index = 0;
        while (index < before.Length)
        {
            if (before[index] == after[index])
            {
                index++;
                continue;
            }
            int start = index;
            while (index < before.Length && before[index] != after[index])
                index++;
            int length = index - start;
            changed += length;
            rangeCount++;
            manifest.Append(start.ToString("X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(length.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(Hash(before.AsSpan(start, length))).Append('|')
                .Append(Hash(after.AsSpan(start, length))).Append('\n');
        }
        return new(changed, rangeCount, Hash(Encoding.UTF8.GetBytes(manifest.ToString())));
    }

    private static string HashRelocations(IEnumerable<Id65ModelComponentRelocation> relocations)
    {
        StringBuilder text = new();
        foreach (Id65ModelComponentRelocation item in relocations.OrderBy(item => item.StableId, StringComparer.Ordinal))
        {
            text.Append(item.StableId).Append('|')
                .Append(item.SourceRelativeOffset.ToString("X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(item.SourceByteLength.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(item.OutputRelativeOffset.ToString("X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(item.OutputByteLength.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(item.SourceSha256).Append('|').Append(item.OutputSha256).Append('|')
                .Append(item.ContentsPreserved ? '1' : '0').Append('\n');
        }
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static string HashHandleRebases(IEnumerable<Id65StableHandleRebase> rebases)
    {
        StringBuilder text = new();
        foreach (Id65StableHandleRebase item in rebases.OrderBy(item => item.StableId, StringComparer.Ordinal))
        {
            text.Append(item.StableId).Append('|').Append(item.Kind).Append('|')
                .Append(Address(item.Source)).Append('|').Append(Address(item.Output)).Append('\n');
        }
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static string Address(Id65LogicalAddress? address) => address.HasValue
        ? string.Create(CultureInfo.InvariantCulture, $"{address.Value.Space}:{address.Value.Primary}:{address.Value.Secondary}")
        : "none";

    private static void RequireExactSet<T>(
        IReadOnlyList<T> actual,
        IReadOnlyList<T> expected,
        Func<T, string> key,
        string label)
        where T : struct
    {
        T[] left = actual.OrderBy(key, StringComparer.Ordinal).ToArray();
        T[] right = expected.OrderBy(key, StringComparer.Ordinal).ToArray();
        if (!left.SequenceEqual(right))
            throw new InvalidDataException($"The exact minimal-v2 {label} changed or conflict.");
    }

    private static long NormalZ(Id65AuthoringPoint a, Id65AuthoringPoint b, Id65AuthoringPoint c) =>
        ((long)b.X - a.X) * ((long)c.Y - a.Y) -
        (((long)b.Y - a.Y) * ((long)c.X - a.X));

    private static void RequireHash(string actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new InvalidDataException($"The exact {label} SHA-256 changed: {actual}.");
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed record ModelDiffProof(int ChangedByteCount, int RangeCount, string ManifestSha256);
}
