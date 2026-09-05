using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

internal readonly record struct Id65AuthoringEditorCoordinate(int X, int Y, int Z);

internal readonly record struct Id65AuthoringEditorPoint(
    string Handle,
    int X,
    int Y,
    int Z);

internal readonly record struct Id65AuthoringEditorCollisionCell(int X, int Y, int Z);

internal sealed class Id65AuthoringEditorCollisionIntent
{
    private readonly ReadOnlyCollection<string> _vertexHandles;

    internal Id65AuthoringEditorCollisionIntent(
        string id,
        IEnumerable<string> vertexHandles,
        int assignment,
        Id65AuthoringEditorCollisionCell targetCell)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(vertexHandles);
        Id = id;
        _vertexHandles = Array.AsReadOnly(vertexHandles.ToArray());
        Assignment = assignment;
        TargetCell = targetCell;
    }

    public string Id { get; }
    public IReadOnlyList<string> VertexHandles => _vertexHandles;
    public int Assignment { get; }
    public Id65AuthoringEditorCollisionCell TargetCell { get; }
}

internal sealed record Id65AuthoringEditorOcclusionMembership(
    string Id,
    string SectorId,
    int GroupIndex);

internal sealed class Id65AuthoringEditorTerrainIntent
{
    private readonly ReadOnlyCollection<string> _highPolyHandles;
    private readonly ReadOnlyCollection<string> _lowPolyHandles;

    internal Id65AuthoringEditorTerrainIntent(
        string id,
        string sectorId,
        int sectorIndex,
        Id65AuthoringEditorPoint addedVertex,
        IEnumerable<string> highPolyHandles,
        IEnumerable<string> lowPolyHandles,
        string textureIntentId,
        Id65AuthoringEditorCollisionIntent collision,
        Id65AuthoringEditorOcclusionMembership occlusion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectorId);
        ArgumentNullException.ThrowIfNull(highPolyHandles);
        ArgumentNullException.ThrowIfNull(lowPolyHandles);
        ArgumentException.ThrowIfNullOrWhiteSpace(textureIntentId);
        ArgumentNullException.ThrowIfNull(collision);
        ArgumentNullException.ThrowIfNull(occlusion);
        Id = id;
        SectorId = sectorId;
        SectorIndex = sectorIndex;
        AddedVertex = addedVertex;
        _highPolyHandles = Array.AsReadOnly(highPolyHandles.ToArray());
        _lowPolyHandles = Array.AsReadOnly(lowPolyHandles.ToArray());
        TextureIntentId = textureIntentId;
        Collision = collision;
        Occlusion = occlusion;
    }

    public string Id { get; }
    public string SectorId { get; }
    public int SectorIndex { get; }
    public Id65AuthoringEditorPoint AddedVertex { get; }
    public IReadOnlyList<string> HighPolyHandles => _highPolyHandles;
    public IReadOnlyList<string> LowPolyHandles => _lowPolyHandles;
    public string TextureIntentId { get; }
    public Id65AuthoringEditorCollisionIntent Collision { get; }
    public Id65AuthoringEditorOcclusionMembership Occlusion { get; }
}

internal sealed record Id65AuthoringEditorPrivateTextureSwap(
    string Id,
    string TargetRenderFaceId,
    string TextureIntentId,
    int DestinationRecordIndex,
    string SourceHandle,
    int DonorWadEntry,
    int DonorTextureId,
    int MaterialTemplateTextureId);

internal sealed record Id65AuthoringEditorMobyPlacement(
    string Id,
    string AtomicGroupId,
    string BundleId,
    string RowTemplateId,
    string RowPolicy,
    int TargetTrueIndex,
    string CoordinatePolicy,
    Id65AuthoringEditorCoordinate? CoordinatesOrDelta,
    byte? YawByte,
    string? SupportHandle);

internal sealed record Id65AuthoringEditorBaselineHandles(
    string Spawn,
    string Music,
    string Totals,
    string Exit,
    string Save);

/// <summary>
/// One path-free logical blank-cyclorama choice. It carries only the exact
/// code-owned preset identity and its visible RGB presentation color.
/// </summary>
internal sealed record Id65AuthoringBlankCycloramaIntent(
    string PresetId,
    string ColorHex)
{
    public bool ContainsNativePayloadBytes => false;
    public bool ContainsPhysicalAddresses => false;
    public bool ContainsFilePaths => false;
    public bool ContainsCompiledAfterimage => false;
    public bool ContainsPublisherState => false;
    public bool PersistenceEnabled => false;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool WritesCue => false;
    public bool AppIntegrated => false;
    public bool CreateBinEnabled => false;
    public bool NormalCreateBinEnabled => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool ReleaseIntegrated => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;
}

/// <summary>
/// Closed catalog for the first authoring-owned blank cyclorama. This is
/// descriptive intent only: no native payload, address, offset, or path is
/// admitted by the catalog or its document representation.
/// </summary>
internal static class Id65AuthoringBlankCycloramaCatalog
{
    public const int SchemaVersion = 1;
    public const string ProfileId = "id65-authoring-blank-cyclorama-catalog-v1";
    public const string BlankSlatePresetId = "blank-slate-v1";
    public const string BlankSlateColorHex = "#263447";
    public const string ExpectedCanonicalSha256 =
        "a4c3919239f7a5f817555e0c654e8025fab59f864aec3ea1499ea64bf491de75";

    private const string CanonicalCatalogJson =
        "{\"schemaVersion\":1,\"profileId\":\"id65-authoring-blank-cyclorama-catalog-v1\",\"presets\":[{\"presetId\":\"blank-slate-v1\",\"colorHex\":\"#263447\"}]}";

    public static string CanonicalSha256 { get; } = Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalCatalogJson)));

    static Id65AuthoringBlankCycloramaCatalog()
    {
        if (BlankSlatePresetId != "blank-slate-v1" ||
            BlankSlateColorHex != "#263447" ||
            BlankSlateColorHex == "#000000" ||
            CanonicalSha256 != ExpectedCanonicalSha256)
        {
            throw new InvalidDataException(
                "The code-owned ID65 blank-cyclorama catalog identity changed.");
        }
    }

    public static Id65AuthoringBlankCycloramaIntent CreateBlankSlate() =>
        new(BlankSlatePresetId, BlankSlateColorHex);

    public static Id65AuthoringBlankCycloramaIntent GetRequired(string presetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(presetId);
        if (presetId != BlankSlatePresetId)
        {
            throw new InvalidDataException(
                $"The ID65 blank-cyclorama preset '{presetId}' is unsupported.");
        }
        return CreateBlankSlate();
    }

    public static void RequireExact(Id65AuthoringBlankCycloramaIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        Id65AuthoringBlankCycloramaIntent expected = GetRequired(intent.PresetId);
        if (intent != expected)
        {
            throw new InvalidDataException(
                "The ID65 blank-cyclorama preset/color is unsupported or stale.");
        }
    }

    public static bool ContainsNativePayloadBytes => false;
    public static bool ContainsPhysicalAddresses => false;
    public static bool ContainsFilePaths => false;
    public static bool ContainsCompiledAfterimage => false;
    public static bool ContainsPublisherState => false;
    public static bool PersistenceEnabled => false;
    public static bool WritesFileSystem => false;
    public static bool WritesDiscImage => false;
    public static bool WritesCue => false;
    public static bool AppIntegrated => false;
    public static bool CreateBinEnabled => false;
    public static bool NormalCreateBinEnabled => false;
    public static bool RuntimeCandidateAuthorized => false;
    public static bool ReleaseIntegrated => false;
    public static bool PromotionAuthorized => false;
    public static bool Publishable => false;
}

/// <summary>
/// Immutable, path-free editor intent for the exact first ID65 single-pass
/// composite. The document owns stable logical identities only. It cannot
/// contain native payload bytes, physical addresses, file paths, a compiled
/// afterimage, or publisher state.
/// </summary>
internal sealed class Id65AuthoringEditorDocument
{
    public const int CurrentSchemaVersion = 1;
    public const int ResidentT25SchemaVersion = 2;
    public const int RemotePadGridSchemaVersion = 3;
    public const int RemotePadMobyChoiceSchemaVersion = 4;
    public const int RemotePadMobyChoiceTerrainSetSchemaVersion = 5;
    public const int RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion = 6;
    public const int MaximumSerializedByteCount = 64 * 1024;
    public const string DocumentProfileId = "id65-authoring-editor-document-exact-composite-v1";
    public const string ResidentT25DocumentProfileId =
        "id65-authoring-editor-document-resident-t25-v2";
    public const string RemotePadGridDocumentProfileId =
        "id65-authoring-editor-document-remote-pad-grid-v3";
    public const string RemotePadMobyChoiceDocumentProfileId =
        "id65-authoring-editor-document-remote-pad-moby-choice-v4";
    public const string RemotePadMobyChoiceTerrainSetDocumentProfileId =
        "id65-authoring-editor-document-remote-pad-moby-choice-terrain-set-v5";
    public const string RemotePadMobyChoiceTerrainSetBlankCycloramaDocumentProfileId =
        "id65-authoring-editor-document-remote-pad-moby-choice-terrain-set-blank-cyclorama-v6";
    public const int ResidentMandatoryBaseFaceTextureId = 25;
    public const int PrivateMandatoryBaseFaceTextureId = 66;
    public const string PrivateTextureSwapId = "swap.remote-pad.face0.private-t66";
    public const string OptionalTriangleCollisionId =
        Id65AuthoringTerrainMutationCompiler.TriangleId + ":collision";
    public const string OptionalTriangleOcclusionId = "occlusion.remote-pad.group0";
    public const string AppendContiguousRowPolicy = "append-contiguous";
    public const string ReplaceExactRowPolicy = "replace-exact";
    public const string ReuseResidentExactRowPolicy = "reuse-resident-exact";
    public const string PreserveDestinationCoordinatePolicy = "preserve-destination";
    public const string AuthorExactCoordinatePolicy = "author-exact";

    private const int ExactPrivateDonorWadEntry = 70;
    private const int ExactPrivateDonorTextureId = 12;

    private readonly ReadOnlyCollection<Id65AuthoringEditorTerrainIntent> _terrain;
    private readonly ReadOnlyCollection<Id65AuthoringEditorMobyPlacement> _mobyPlacements;
    private readonly ReadOnlyCollection<string> _activeTerrainUnitIds;
    private readonly byte[] _canonicalUtf8;

    private Id65AuthoringEditorDocument(
        IEnumerable<Id65AuthoringEditorTerrainIntent> terrain,
        Id65AuthoringEditorPrivateTextureSwap privateTextureSwap,
        IEnumerable<Id65AuthoringEditorMobyPlacement> mobyPlacements,
        Id65AuthoringEditorBaselineHandles baselineHandles,
        int mandatoryBaseFaceTextureId,
        string? t107RemotePadPositionId,
        Id65RemotePadMobyKind? remotePadMobyKind,
        string? remotePadMobyPositionId,
        Id65AuthoringTerrainSetManifest? terrainSetManifest = null,
        Id65AuthoringBlankCycloramaIntent? blankCyclorama = null)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(privateTextureSwap);
        ArgumentNullException.ThrowIfNull(mobyPlacements);
        ArgumentNullException.ThrowIfNull(baselineHandles);
        _terrain = Array.AsReadOnly(terrain.ToArray());
        PrivateTextureSwap = privateTextureSwap;
        _mobyPlacements = Array.AsReadOnly(mobyPlacements.ToArray());
        BaselineHandles = baselineHandles;
        MandatoryBaseFaceTextureId = mandatoryBaseFaceTextureId;
        T107RemotePadPositionId = t107RemotePadPositionId;
        RemotePadMobyKind = remotePadMobyKind;
        RemotePadMobyPositionId = remotePadMobyPositionId;
        TerrainSetManifestSha256 = terrainSetManifest?.CanonicalSha256;
        _activeTerrainUnitIds = Array.AsReadOnly(
            terrainSetManifest?.ActiveUnits.Select(item => item.Id).ToArray() ?? []);
        BlankCyclorama = blankCyclorama;
        ValidateExactDocument(this);

        byte[] canonicalContent = WriteCanonical(this, contentSha256: null);
        ContentSha256 = Hash(canonicalContent);
        _canonicalUtf8 = WriteCanonical(this, ContentSha256);
        CanonicalJson = Encoding.UTF8.GetString(_canonicalUtf8);
        CanonicalSha256 = Hash(_canonicalUtf8);
    }

    public int SchemaVersion => UsesBlankCyclorama
        ? RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion
        : UsesRemotePadTerrainSet
        ? RemotePadMobyChoiceTerrainSetSchemaVersion
        : UsesRemotePadMobyChoice
        ? RemotePadMobyChoiceSchemaVersion
        : UsesRemotePadT107Grid
            ? RemotePadGridSchemaVersion
        : MandatoryBaseFaceTextureId == PrivateMandatoryBaseFaceTextureId
            ? CurrentSchemaVersion
            : ResidentT25SchemaVersion;
    public string ProfileId => UsesBlankCyclorama
        ? RemotePadMobyChoiceTerrainSetBlankCycloramaDocumentProfileId
        : UsesRemotePadTerrainSet
        ? RemotePadMobyChoiceTerrainSetDocumentProfileId
        : UsesRemotePadMobyChoice
        ? RemotePadMobyChoiceDocumentProfileId
        : UsesRemotePadT107Grid
            ? RemotePadGridDocumentProfileId
        : MandatoryBaseFaceTextureId == PrivateMandatoryBaseFaceTextureId
            ? DocumentProfileId
            : ResidentT25DocumentProfileId;
    public int MandatoryBaseFaceTextureId { get; }
    public string? T107RemotePadPositionId { get; }
    public bool UsesRemotePadT107Grid => T107RemotePadPositionId is not null;
    public Id65RemotePadMobyKind? RemotePadMobyKind { get; }
    public string? RemotePadMobyPositionId { get; }
    public bool UsesRemotePadMobyChoice => RemotePadMobyKind.HasValue;
    public bool UsesRemotePadTerrainSet => TerrainSetManifestSha256 is not null;
    public string? TerrainSetManifestSha256 { get; }
    public IReadOnlyList<string> ActiveTerrainUnitIds => _activeTerrainUnitIds;
    public Id65AuthoringBlankCycloramaIntent? BlankCyclorama { get; }
    public bool UsesBlankCyclorama => BlankCyclorama is not null;
    public bool UsesStaticRemotePadAuthoring =>
        UsesRemotePadT107Grid || UsesRemotePadMobyChoice;
    public string LabProfileId => UnusedLevel65BlankLevelLabProfileRegistry.ProfileId;
    public int LabProfileVersion => UnusedLevel65BlankLevelLabProfileRegistry.ProfileVersion;
    public string WorkspaceKey => UnusedLevel65BlankLevelLabProfileRegistry.Key;
    public int WorkspaceLayoutVersion => UnusedLevel65BlankLevelLabProfileRegistry.WorkspaceLayoutVersion;
    public string WorkspaceLayoutRelativeRoot =>
        UnusedLevel65BlankLevelLabProfileRegistry.WorkspaceLayoutRelativeRoot;
    public string LockedBaseProfileId =>
        UnusedLevel65BlankLevelLabProfileRegistry.Profile.LockedBaseProfileId;
    public string LockedBaseImageSha256 =>
        UnusedLevel65BlankLevelLabProfileRegistry.Profile.LockedBaseImageSha256;
    public string LockedRow80Sha256 => Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256;
    public string CompositeProfileId => Id65AuthoringCompositeCompiler.ProfileId;
    public string PrivateTextureWitnessSha256 =>
        Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256;
    public string MobyBundleSha256 =>
        Id65AuthoringMobyDependencyBundleCompiler.ExpectedArtisansGrassBundleSha256;
    public IReadOnlyList<Id65AuthoringEditorTerrainIntent> Terrain => _terrain;
    public Id65AuthoringEditorPrivateTextureSwap PrivateTextureSwap { get; }
    public IReadOnlyList<Id65AuthoringEditorMobyPlacement> MobyPlacements => _mobyPlacements;
    public Id65AuthoringEditorBaselineHandles BaselineHandles { get; }
    public string ContentSha256 { get; }
    public string CanonicalJson { get; }
    public string CanonicalSha256 { get; }

    public bool ContainsNativePayloadBytes => false;
    public bool ContainsPhysicalAddresses => false;
    public bool ContainsFilePaths => false;
    public bool ContainsCompiledAfterimage => false;
    public bool ContainsPublisherState => false;
    public bool PersistenceEnabled => false;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool WritesCue => false;
    public bool AppIntegrated => false;
    public bool CreateBinEnabled => false;
    public bool NormalCreateBinEnabled => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool ReleaseIntegrated => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;

    public byte[] SerializeUtf8() => _canonicalUtf8.ToArray();

    public static Id65AuthoringEditorDocument CreateExact(
        bool includeOptionalTerrain = true,
        IEnumerable<string>? placementIds = null,
        int mandatoryBaseFaceTextureId = PrivateMandatoryBaseFaceTextureId)
    {
        Id65AuthoringEditorTerrainIntent[] terrain = includeOptionalTerrain
            ? [CreateExactTerrainIntent()]
            : [];
        Id65AuthoringEditorMobyPlacement[] placements = ResolveExactPlacements(placementIds);
        return new(
            terrain,
            CreateExactPrivateTextureSwap(),
            placements,
            CreateExactBaselineHandles(),
            mandatoryBaseFaceTextureId,
            t107RemotePadPositionId: null,
            remotePadMobyKind: null,
            remotePadMobyPositionId: null);
    }

    public static Id65AuthoringEditorDocument CreateRemotePadGridExact(
        string t107RemotePadPositionId,
        bool includeOptionalTerrain = true,
        IEnumerable<string>? placementIds = null,
        int mandatoryBaseFaceTextureId = PrivateMandatoryBaseFaceTextureId)
    {
        Id65RemotePadGrassT107Position position =
            Id65RemotePadGrassT107Catalog.GetRequiredPosition(t107RemotePadPositionId);
        Id65AuthoringEditorTerrainIntent[] terrain = includeOptionalTerrain
            ? [CreateExactTerrainIntent()]
            : [];
        Id65AuthoringEditorMobyPlacement[] placements =
            ResolveRemotePadGridPlacements(placementIds, position.PositionId);
        return new(
            terrain,
            CreateExactPrivateTextureSwap(),
            placements,
            CreateExactBaselineHandles(),
            mandatoryBaseFaceTextureId,
            position.PositionId,
            remotePadMobyKind: null,
            remotePadMobyPositionId: null);
    }

    /// <summary>
    /// Creates one schema-4, T66-only fresh-ID65 document. The Moby choice is
    /// mutually exclusive and its position remains canonical even for None.
    /// </summary>
    public static Id65AuthoringEditorDocument CreateRemotePadMobyChoiceExact(
        Id65RemotePadMobyKind kind,
        string remotePadMobyPositionId,
        bool includeOptionalTerrain = true)
    {
        _ = Id65RemotePadMobyChoiceCatalog.GetRequiredChoice(kind);
        string positionId =
            Id65RemotePadMobyChoiceCatalog.GetRequiredPositionId(remotePadMobyPositionId);
        Id65AuthoringEditorTerrainIntent[] terrain = includeOptionalTerrain
            ? [CreateExactTerrainIntent()]
            : [];
        Id65AuthoringEditorMobyPlacement[] placements =
            ResolveRemotePadMobyChoicePlacements(kind, positionId);
        return new(
            terrain,
            CreateExactPrivateTextureSwap(),
            placements,
            CreateExactBaselineHandles(),
            PrivateMandatoryBaseFaceTextureId,
            t107RemotePadPositionId: null,
            remotePadMobyKind: kind,
            remotePadMobyPositionId: positionId);
    }

    /// <summary>
    /// Creates one schema-5, T66-only fresh-ID65 document whose structural
    /// terrain is selected from the exact closed two-unit catalog. Selection
    /// order is canonicalized by the catalog and never controls physical order.
    /// </summary>
    public static Id65AuthoringEditorDocument
        CreateRemotePadMobyChoiceTerrainSetExact(
            Id65RemotePadMobyKind kind,
            string remotePadMobyPositionId,
            IEnumerable<string> activeTerrainUnitIds)
    {
        ArgumentNullException.ThrowIfNull(activeTerrainUnitIds);
        _ = Id65RemotePadMobyChoiceCatalog.GetRequiredChoice(kind);
        string positionId =
            Id65RemotePadMobyChoiceCatalog.GetRequiredPositionId(remotePadMobyPositionId);
        Id65AuthoringTerrainSetManifest terrainSet =
            Id65AuthoringTerrainSetCatalog.Create(activeTerrainUnitIds);
        Id65AuthoringEditorTerrainIntent[] terrain = terrainSet.ActiveUnits
            .Select(CreateExactTerrainIntent)
            .ToArray();
        Id65AuthoringEditorMobyPlacement[] placements =
            ResolveRemotePadMobyChoicePlacements(kind, positionId);
        return new(
            terrain,
            CreateExactPrivateTextureSwap(),
            placements,
            CreateExactBaselineHandles(),
            PrivateMandatoryBaseFaceTextureId,
            t107RemotePadPositionId: null,
            remotePadMobyKind: kind,
            remotePadMobyPositionId: positionId,
            terrainSetManifest: terrainSet);
    }

    /// <summary>
    /// Creates one schema-6 document by preserving the complete schema-5
    /// terrain/Moby/T66 intent and attaching the single code-owned blank
    /// cyclorama preset. No native sky bytes or physical location enter the
    /// document.
    /// </summary>
    public static Id65AuthoringEditorDocument
        CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
            Id65RemotePadMobyKind kind,
            string remotePadMobyPositionId,
            IEnumerable<string> activeTerrainUnitIds)
    {
        ArgumentNullException.ThrowIfNull(activeTerrainUnitIds);
        _ = Id65RemotePadMobyChoiceCatalog.GetRequiredChoice(kind);
        string positionId =
            Id65RemotePadMobyChoiceCatalog.GetRequiredPositionId(remotePadMobyPositionId);
        Id65AuthoringTerrainSetManifest terrainSet =
            Id65AuthoringTerrainSetCatalog.Create(activeTerrainUnitIds);
        Id65AuthoringEditorTerrainIntent[] terrain = terrainSet.ActiveUnits
            .Select(CreateExactTerrainIntent)
            .ToArray();
        Id65AuthoringEditorMobyPlacement[] placements =
            ResolveRemotePadMobyChoicePlacements(kind, positionId);
        return new(
            terrain,
            CreateExactPrivateTextureSwap(),
            placements,
            CreateExactBaselineHandles(),
            PrivateMandatoryBaseFaceTextureId,
            t107RemotePadPositionId: null,
            remotePadMobyKind: kind,
            remotePadMobyPositionId: positionId,
            terrainSetManifest: terrainSet,
            blankCyclorama: Id65AuthoringBlankCycloramaCatalog.CreateBlankSlate());
    }

    public static Id65AuthoringEditorDocument Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (Encoding.UTF8.GetByteCount(json) > MaximumSerializedByteCount)
        {
            throw new InvalidDataException(
                $"The ID65 editor document exceeds the {MaximumSerializedByteCount}-byte canonical JSON limit.");
        }
        return Parse(Encoding.UTF8.GetBytes(json));
    }

    public static Id65AuthoringEditorDocument Parse(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.IsEmpty)
            throw new InvalidDataException("The ID65 editor document is empty.");
        if (utf8Json.Length > MaximumSerializedByteCount)
        {
            throw new InvalidDataException(
                $"The ID65 editor document exceeds the {MaximumSerializedByteCount}-byte canonical JSON limit.");
        }
        if (utf8Json.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
            throw new InvalidDataException("The ID65 editor document must be canonical UTF-8 JSON without a BOM.");

        try
        {
            using JsonDocument parsed = JsonDocument.Parse(
                utf8Json.ToArray(),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 32
                });
            JsonElement root = parsed.RootElement;
            int schemaVersion = RequireInt32(root, "schemaVersion", "root");
            string profileId = RequireString(root, "profileId", "root");
            bool legacyPrivateT66 = schemaVersion == CurrentSchemaVersion &&
                profileId == DocumentProfileId;
            bool residentT25 = schemaVersion == ResidentT25SchemaVersion &&
                profileId == ResidentT25DocumentProfileId;
            bool remotePadGrid = schemaVersion == RemotePadGridSchemaVersion &&
                profileId == RemotePadGridDocumentProfileId;
            bool remotePadMobyChoice = schemaVersion == RemotePadMobyChoiceSchemaVersion &&
                profileId == RemotePadMobyChoiceDocumentProfileId;
            bool remotePadMobyChoiceTerrainSet =
                schemaVersion == RemotePadMobyChoiceTerrainSetSchemaVersion &&
                profileId == RemotePadMobyChoiceTerrainSetDocumentProfileId;
            bool remotePadMobyChoiceTerrainSetBlankCyclorama =
                schemaVersion ==
                    RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion &&
                profileId ==
                    RemotePadMobyChoiceTerrainSetBlankCycloramaDocumentProfileId;
            if (remotePadMobyChoiceTerrainSetBlankCyclorama)
            {
                RequireObjectProperties(
                    root,
                    "root",
                    "schemaVersion", "profileId", "mandatoryBaseFaceTextureId",
                    "remotePadMobyChoice", "terrainSet", "blankCyclorama", "labProfileId",
                    "labProfileVersion", "workspaceKey", "workspaceLayoutVersion",
                    "workspaceLayoutRelativeRoot", "lockedBaseProfileId",
                    "lockedBaseImageSha256", "lockedRow80Sha256", "compositeProfileId",
                    "privateTextureWitnessSha256", "mobyBundleSha256", "terrain",
                    "privateTextureSwap", "mobyPlacements", "baselineHandles",
                    "contentSha256");
            }
            else if (remotePadMobyChoiceTerrainSet)
            {
                RequireObjectProperties(
                    root,
                    "root",
                    "schemaVersion", "profileId", "mandatoryBaseFaceTextureId",
                    "remotePadMobyChoice", "terrainSet", "labProfileId", "labProfileVersion",
                    "workspaceKey", "workspaceLayoutVersion", "workspaceLayoutRelativeRoot",
                    "lockedBaseProfileId", "lockedBaseImageSha256", "lockedRow80Sha256",
                    "compositeProfileId", "privateTextureWitnessSha256", "mobyBundleSha256",
                    "terrain", "privateTextureSwap", "mobyPlacements", "baselineHandles",
                    "contentSha256");
            }
            else if (remotePadMobyChoice)
            {
                RequireObjectProperties(
                    root,
                    "root",
                    "schemaVersion", "profileId", "mandatoryBaseFaceTextureId",
                    "remotePadMobyChoice", "labProfileId", "labProfileVersion", "workspaceKey",
                    "workspaceLayoutVersion", "workspaceLayoutRelativeRoot",
                    "lockedBaseProfileId", "lockedBaseImageSha256", "lockedRow80Sha256",
                    "compositeProfileId", "privateTextureWitnessSha256", "mobyBundleSha256",
                    "terrain", "privateTextureSwap", "mobyPlacements", "baselineHandles",
                    "contentSha256");
            }
            else if (remotePadGrid)
            {
                RequireObjectProperties(
                    root,
                    "root",
                    "schemaVersion", "profileId", "mandatoryBaseFaceTextureId",
                    "t107Placement", "labProfileId", "labProfileVersion", "workspaceKey",
                    "workspaceLayoutVersion", "workspaceLayoutRelativeRoot",
                    "lockedBaseProfileId", "lockedBaseImageSha256", "lockedRow80Sha256",
                    "compositeProfileId", "privateTextureWitnessSha256", "mobyBundleSha256",
                    "terrain", "privateTextureSwap", "mobyPlacements", "baselineHandles",
                    "contentSha256");
            }
            else if (residentT25)
            {
                RequireObjectProperties(
                    root,
                    "root",
                    "schemaVersion", "profileId", "mandatoryBaseFaceTextureId",
                    "labProfileId", "labProfileVersion", "workspaceKey",
                    "workspaceLayoutVersion", "workspaceLayoutRelativeRoot",
                    "lockedBaseProfileId", "lockedBaseImageSha256", "lockedRow80Sha256",
                    "compositeProfileId", "privateTextureWitnessSha256", "mobyBundleSha256",
                    "terrain", "privateTextureSwap", "mobyPlacements", "baselineHandles",
                    "contentSha256");
            }
            else
            {
                RequireObjectProperties(
                    root,
                    "root",
                    "schemaVersion", "profileId", "labProfileId", "labProfileVersion",
                    "workspaceKey", "workspaceLayoutVersion", "workspaceLayoutRelativeRoot",
                    "lockedBaseProfileId", "lockedBaseImageSha256", "lockedRow80Sha256",
                    "compositeProfileId", "privateTextureWitnessSha256", "mobyBundleSha256",
                    "terrain", "privateTextureSwap", "mobyPlacements", "baselineHandles",
                    "contentSha256");
            }
            string labProfileId = RequireString(root, "labProfileId", "root");
            int labProfileVersion = RequireInt32(root, "labProfileVersion", "root");
            string workspaceKey = RequireString(root, "workspaceKey", "root");
            int workspaceLayoutVersion = RequireInt32(root, "workspaceLayoutVersion", "root");
            string workspaceLayoutRelativeRoot =
                RequireString(root, "workspaceLayoutRelativeRoot", "root");
            string lockedBaseProfileId = RequireString(root, "lockedBaseProfileId", "root");
            string lockedBaseImageSha256 = RequireString(root, "lockedBaseImageSha256", "root");
            string lockedRow80Sha256 = RequireString(root, "lockedRow80Sha256", "root");
            string compositeProfileId = RequireString(root, "compositeProfileId", "root");
            string privateTextureWitnessSha256 =
                RequireString(root, "privateTextureWitnessSha256", "root");
            string mobyBundleSha256 = RequireString(root, "mobyBundleSha256", "root");
            string contentSha256 = RequireString(root, "contentSha256", "root");
            int mandatoryBaseFaceTextureId = residentT25 || remotePadGrid ||
                remotePadMobyChoice || remotePadMobyChoiceTerrainSet ||
                remotePadMobyChoiceTerrainSetBlankCyclorama
                ? RequireInt32(root, "mandatoryBaseFaceTextureId", "root")
                : PrivateMandatoryBaseFaceTextureId;
            string? t107RemotePadPositionId = remotePadGrid
                ? ReadRemotePadT107Placement(root)
                : null;
            (Id65RemotePadMobyKind Kind, string PositionId)? remotePadMobySelection =
                remotePadMobyChoice || remotePadMobyChoiceTerrainSet ||
                    remotePadMobyChoiceTerrainSetBlankCyclorama
                    ? ReadRemotePadMobyChoice(root)
                    : null;
            Id65AuthoringTerrainSetManifest? terrainSetManifest =
                remotePadMobyChoiceTerrainSet ||
                    remotePadMobyChoiceTerrainSetBlankCyclorama
                    ? ReadTerrainSet(root)
                    : null;
            Id65AuthoringBlankCycloramaIntent? blankCyclorama =
                remotePadMobyChoiceTerrainSetBlankCyclorama
                    ? ReadBlankCyclorama(root)
                    : null;
            if ((!legacyPrivateT66 && !residentT25 && !remotePadGrid && !remotePadMobyChoice &&
                    !remotePadMobyChoiceTerrainSet &&
                    !remotePadMobyChoiceTerrainSetBlankCyclorama) ||
                (residentT25 && mandatoryBaseFaceTextureId != ResidentMandatoryBaseFaceTextureId) ||
                (remotePadGrid && mandatoryBaseFaceTextureId is not
                    (ResidentMandatoryBaseFaceTextureId or PrivateMandatoryBaseFaceTextureId)) ||
                (remotePadMobyChoice &&
                    mandatoryBaseFaceTextureId != PrivateMandatoryBaseFaceTextureId) ||
                (remotePadMobyChoiceTerrainSet &&
                    mandatoryBaseFaceTextureId != PrivateMandatoryBaseFaceTextureId) ||
                (remotePadMobyChoiceTerrainSetBlankCyclorama &&
                    mandatoryBaseFaceTextureId != PrivateMandatoryBaseFaceTextureId) ||
                labProfileId != UnusedLevel65BlankLevelLabProfileRegistry.ProfileId ||
                labProfileVersion != UnusedLevel65BlankLevelLabProfileRegistry.ProfileVersion ||
                workspaceKey != UnusedLevel65BlankLevelLabProfileRegistry.Key ||
                workspaceLayoutVersion != UnusedLevel65BlankLevelLabProfileRegistry.WorkspaceLayoutVersion ||
                workspaceLayoutRelativeRoot !=
                    UnusedLevel65BlankLevelLabProfileRegistry.WorkspaceLayoutRelativeRoot ||
                lockedBaseProfileId !=
                    UnusedLevel65BlankLevelLabProfileRegistry.Profile.LockedBaseProfileId ||
                !HashEquals(
                    lockedBaseImageSha256,
                    UnusedLevel65BlankLevelLabProfileRegistry.Profile.LockedBaseImageSha256) ||
                !HashEquals(lockedRow80Sha256, Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256) ||
                compositeProfileId != Id65AuthoringCompositeCompiler.ProfileId)
            {
                throw new InvalidDataException(
                    "The ID65 editor document schema/profile/workspace/layout/locked row-80 identity is unsupported or stale.");
            }
            if (!HashEquals(
                    privateTextureWitnessSha256,
                    Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256))
            {
                throw new InvalidDataException(
                    "The ID65 editor document private texture witness SHA-256 is unsupported or stale.");
            }
            if (!HashEquals(
                    mobyBundleSha256,
                    Id65AuthoringMobyDependencyBundleCompiler.ExpectedArtisansGrassBundleSha256))
            {
                throw new InvalidDataException(
                    "The ID65 editor document Moby bundle SHA-256 is unsupported or stale.");
            }

            Id65AuthoringEditorTerrainIntent[] terrain = ReadTerrain(root);
            Id65AuthoringEditorPrivateTextureSwap privateTexture = ReadPrivateTexture(root);
            Id65AuthoringEditorMobyPlacement[] placements = ReadPlacements(root);
            Id65AuthoringEditorBaselineHandles baselineHandles = ReadBaselineHandles(root);
            Id65AuthoringEditorDocument document = new(
                terrain,
                privateTexture,
                placements,
                baselineHandles,
                mandatoryBaseFaceTextureId,
                t107RemotePadPositionId,
                remotePadMobySelection?.Kind,
                remotePadMobySelection?.PositionId,
                terrainSetManifest,
                blankCyclorama);
            if (!HashEquals(contentSha256, document.ContentSha256))
                throw new InvalidDataException("The ID65 editor document content SHA-256 is invalid or tampered.");
            if (!utf8Json.SequenceEqual(document._canonicalUtf8))
                throw new InvalidDataException("The ID65 editor document is not exact canonical UTF-8 JSON.");
            return document;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is
            JsonException or InvalidOperationException or FormatException or OverflowException or
            ArgumentException or KeyNotFoundException)
        {
            throw new InvalidDataException(
                "The ID65 editor document is not valid strict canonical UTF-8 JSON.",
                exception);
        }
    }

    private static Id65AuthoringEditorTerrainIntent CreateExactTerrainIntent() =>
        CreateExactTerrainIntent(
            Id65AuthoringTerrainMutationCompiler.CreateConcreteTwoTileWitness());

    private static Id65AuthoringEditorTerrainIntent CreateExactTerrainIntent(
        Id65TerrainTriangleIntent triangle)
    {
        ArgumentNullException.ThrowIfNull(triangle);
        Id65AuthoringManifest locked = Id65AuthoringModelCompiler.CreateMinimalV3EquivalentManifest();
        Id65SectorIntent sector = locked.Sectors.Single(item => item.Id == triangle.SectorId);
        Id65AuthoringCollisionCell targetCell = locked.Collision.Single().TargetCell;
        return new(
            triangle.Id,
            triangle.SectorId,
            sector.SectorIndex,
            new(
                triangle.AddedVertex.Handle,
                triangle.AddedVertex.Point.X,
                triangle.AddedVertex.Point.Y,
                triangle.AddedVertex.Point.Z),
            [$"{triangle.RenderA}:hp", $"{triangle.RenderB}:hp", $"{triangle.RenderC}:hp"],
            [$"{triangle.RenderA}:lp", $"{triangle.RenderB}:lp", $"{triangle.RenderC}:lp"],
            triangle.TextureIntentId,
            new(
                $"{triangle.Id}:collision",
                [triangle.CollisionA, triangle.CollisionB, triangle.CollisionC],
                triangle.CollisionAssignment,
                new(targetCell.X, targetCell.Y, targetCell.Z)),
            new(
                OptionalTriangleOcclusionId,
                triangle.SectorId,
                triangle.OcclusionGroupIndex));
    }

    private static Id65AuthoringEditorPrivateTextureSwap CreateExactPrivateTextureSwap()
    {
        Id65AuthoringManifest textureManifest =
            Id65V2NativeTextureCompositionCompiler.CreateMinimalV3T66Manifest();
        Id65TextureIntent texture = textureManifest.Textures.Single(item =>
            item.Disposition == Id65AuthoringIntentDisposition.AuthorExact &&
            item.DestinationRecordIndex == Id65V2NativeTextureCompositionCompiler.PrivateTextureId);
        Id65RenderFaceIntent face = textureManifest.RenderFaces.Single(item =>
            item.TextureIntentId == texture.Id);
        return new(
            PrivateTextureSwapId,
            face.Id,
            texture.Id,
            texture.DestinationRecordIndex,
            texture.SourceHandle,
            ExactPrivateDonorWadEntry,
            ExactPrivateDonorTextureId,
            Id65V2NativeTextureCompositionCompiler.MaterialTemplateTextureId);
    }

    private static Id65AuthoringEditorBaselineHandles CreateExactBaselineHandles()
    {
        Id65AuthoringManifest locked =
            Id65AuthoringModelCompiler.CreateMinimalV3EquivalentManifest();
        return new(
            locked.Spawn.Id,
            locked.Music.Id,
            locked.Totals.Id,
            locked.Exit.Id,
            locked.Save.Id);
    }

    private static Id65AuthoringEditorMobyPlacement[] ResolveExactPlacements(
        IEnumerable<string>? placementIds)
    {
        Id65MobyPlacementIntent t88 =
            Id65AuthoringMobyDependencyBundleCompiler.CreateZeroEggT88Placement();
        Id65MobyPlacementIntent t107 =
            Id65AuthoringMobyDependencyBundleCompiler.CreateT107Placement();
        Dictionary<string, Id65MobyPlacementIntent> supported = new(StringComparer.Ordinal)
        {
            [t88.Id] = t88,
            [t107.Id] = t107
        };
        string[] requested = placementIds?.ToArray() ?? [t88.Id, t107.Id];
        if (requested.Length == 0)
            throw new InvalidDataException("The ID65 editor document requires at least one exact Grass placement.");
        if (requested.Any(string.IsNullOrWhiteSpace) ||
            requested.Distinct(StringComparer.Ordinal).Count() != requested.Length)
        {
            throw new InvalidDataException("The ID65 editor document contains a missing or duplicate Moby placement ID.");
        }
        if (requested.Any(id => !supported.ContainsKey(id)))
            throw new InvalidDataException("The ID65 editor document contains an unsupported Moby placement ID.");
        return requested
            .Select(id => ToEditorPlacement(supported[id]))
            .OrderBy(item => item.TargetTrueIndex)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static Id65AuthoringEditorMobyPlacement[] ResolveRemotePadGridPlacements(
        IEnumerable<string>? placementIds,
        string positionId)
    {
        Id65MobyPlacementIntent t88 =
            Id65AuthoringMobyDependencyBundleCompiler.CreateZeroEggT88Placement();
        Id65MobyPlacementIntent t107 =
            Id65RemotePadGrassT107Catalog.CreateRemotePadT107Placement(positionId);
        Dictionary<string, Id65MobyPlacementIntent> supported = new(StringComparer.Ordinal)
        {
            [t88.Id] = t88,
            [t107.Id] = t107
        };
        string[] requested = placementIds?.ToArray() ?? [t88.Id, t107.Id];
        if (requested.Length > 2 ||
            requested.Any(string.IsNullOrWhiteSpace) ||
            requested.Distinct(StringComparer.Ordinal).Count() != requested.Length)
        {
            throw new InvalidDataException(
                "The schema-3 ID65 editor document contains too many, missing, or duplicate Moby placement IDs.");
        }
        if (requested.Any(id => !supported.ContainsKey(id)))
        {
            throw new InvalidDataException(
                "The schema-3 ID65 editor document contains an unsupported Moby placement ID.");
        }
        return requested
            .Select(id => ToEditorPlacement(supported[id]))
            .OrderBy(item => item.TargetTrueIndex)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static Id65AuthoringEditorMobyPlacement[] ResolveRemotePadMobyChoicePlacements(
        Id65RemotePadMobyKind kind,
        string positionId) =>
        Id65RemotePadMobyChoiceCatalog.CreateExactPlacements(kind, positionId)
            .Select(ToEditorPlacement)
            .OrderBy(item => item.TargetTrueIndex)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();

    private static Id65AuthoringEditorMobyPlacement ToEditorPlacement(
        Id65MobyPlacementIntent placement)
    {
        string rowPolicy = placement.RowPolicy switch
        {
            Id65MobyRowAllocationPolicy.AppendContiguous => AppendContiguousRowPolicy,
            Id65MobyRowAllocationPolicy.ReplaceExact => ReplaceExactRowPolicy,
            Id65MobyRowAllocationPolicy.ReuseResidentExact => ReuseResidentExactRowPolicy,
            _ => throw new InvalidDataException("The exact ID65 Grass placement row policy changed.")
        };
        string coordinatePolicy = placement.CoordinatePolicy switch
        {
            Id65MobyCoordinatePolicy.PreserveDestination => PreserveDestinationCoordinatePolicy,
            Id65MobyCoordinatePolicy.AuthorExact => AuthorExactCoordinatePolicy,
            _ => throw new InvalidDataException("The exact ID65 Grass placement coordinate policy changed.")
        };
        Id65AuthoringEditorCoordinate? coordinates = placement.CoordinatesOrDelta is
            Id65AuthoringPoint point
            ? new(point.X, point.Y, point.Z)
            : null;
        return new(
            placement.Id,
            placement.AtomicGroupId,
            placement.BundleId,
            placement.RowTemplateId,
            rowPolicy,
            placement.TargetTrueIndex,
            coordinatePolicy,
            coordinates,
            placement.YawByte,
            placement.SupportHandle);
    }

    private static void ValidateExactDocument(Id65AuthoringEditorDocument document)
    {
        if (document.MandatoryBaseFaceTextureId is not
            (ResidentMandatoryBaseFaceTextureId or PrivateMandatoryBaseFaceTextureId))
        {
            throw new InvalidDataException(
                "The ID65 mandatory base face texture must be exactly resident T25 or proved private T66.");
        }
        if (document.UsesRemotePadTerrainSet)
        {
            Id65AuthoringTerrainSetManifest exactTerrainSet =
                Id65AuthoringTerrainSetCatalog.Create(document._activeTerrainUnitIds);
            Id65AuthoringEditorTerrainIntent[] expectedTerrain = exactTerrainSet.ActiveUnits
                .Select(CreateExactTerrainIntent)
                .ToArray();
            if (!HashEquals(
                    document.TerrainSetManifestSha256!,
                    exactTerrainSet.CanonicalSha256) ||
                document._terrain.Count != expectedTerrain.Length)
            {
                throw new InvalidDataException(
                    "The schema-5 ID65 terrain-set manifest or terrain cardinality changed.");
            }
            for (int index = 0; index < expectedTerrain.Length; index++)
                RequireExactTerrain(document._terrain[index], expectedTerrain[index]);

            if (document.SchemaVersion is not
                (RemotePadMobyChoiceTerrainSetSchemaVersion or
                    RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion))
            {
                throw new InvalidDataException(
                    "The terrain-set marker must select the exact schema-5 or schema-6 document family.");
            }
        }
        else
        {
            if (document._terrain.Count > 1)
                throw new InvalidDataException("The schema-1/2/3/4 ID65 editor document supports zero or one exact optional terrain intent.");
            if (document._terrain.Count == 1)
                RequireExactTerrain(document._terrain[0], CreateExactTerrainIntent());
            if (document._activeTerrainUnitIds.Count != 0 ||
                document.TerrainSetManifestSha256 is not null)
            {
                throw new InvalidDataException(
                    "A schema-1/2/3/4 ID65 editor document cannot carry terrain-set identity.");
            }
        }
        if (document.UsesBlankCyclorama)
        {
            if (!document.UsesRemotePadTerrainSet ||
                document.SchemaVersion !=
                    RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion)
            {
                throw new InvalidDataException(
                    "The blank-cyclorama marker requires the exact schema-6 terrain-set family.");
            }
            Id65AuthoringBlankCycloramaCatalog.RequireExact(document.BlankCyclorama!);
        }
        Id65AuthoringEditorPrivateTextureSwap expectedPrivate = CreateExactPrivateTextureSwap();
        if (document.PrivateTextureSwap != expectedPrivate)
            throw new InvalidDataException("The ID65 editor document private T66 binding is unsupported or stale.");

        if (document.UsesRemotePadT107Grid && document.UsesRemotePadMobyChoice)
        {
            throw new InvalidDataException(
                "An ID65 editor document cannot combine schema-3 and schema-4 placement semantics.");
        }
        if (document.UsesRemotePadMobyChoice)
        {
            if (document.T107RemotePadPositionId is not null ||
                document.RemotePadMobyPositionId is null ||
                document.MandatoryBaseFaceTextureId != PrivateMandatoryBaseFaceTextureId)
            {
                throw new InvalidDataException(
                    "Schema-4 ID65 Moby-choice documents require exactly private T66 and one retained shared-grid position.");
            }
            _ = Id65RemotePadMobyChoiceCatalog.GetRequiredChoice(document.RemotePadMobyKind!.Value);
            _ = Id65RemotePadMobyChoiceCatalog.GetRequiredPositionId(
                document.RemotePadMobyPositionId);
        }
        else if (document.RemotePadMobyPositionId is not null)
        {
            throw new InvalidDataException(
                "A schema-1/2/3 ID65 editor document cannot carry a schema-4 Moby position.");
        }

        Id65AuthoringEditorMobyPlacement[] expectedPlacements = document.UsesRemotePadMobyChoice
            ? ResolveRemotePadMobyChoicePlacements(
                document.RemotePadMobyKind!.Value,
                document.RemotePadMobyPositionId!)
            : document.UsesRemotePadT107Grid
                ? ResolveRemotePadGridPlacements(
                    document._mobyPlacements.Select(item => item.Id),
                    document.T107RemotePadPositionId!)
                : ResolveExactPlacements(document._mobyPlacements.Select(item => item.Id));
        if (document._mobyPlacements.Count != expectedPlacements.Length ||
            !document._mobyPlacements.SequenceEqual(expectedPlacements))
        {
            throw new InvalidDataException(
                "The ID65 editor document Grass bundle placements are unsupported, duplicated, or noncanonical.");
        }
        if (document.BaselineHandles != CreateExactBaselineHandles())
        {
            throw new InvalidDataException(
                "The ID65 editor document non-editable spawn/music/totals/exit/save ownership handles are unsupported or stale.");
        }
    }

    private static void RequireExactTerrain(
        Id65AuthoringEditorTerrainIntent actual,
        Id65AuthoringEditorTerrainIntent expected)
    {
        if (actual.Id != expected.Id || actual.SectorId != expected.SectorId ||
            actual.SectorIndex != expected.SectorIndex || actual.AddedVertex != expected.AddedVertex ||
            actual.TextureIntentId != expected.TextureIntentId ||
            !actual.HighPolyHandles.SequenceEqual(expected.HighPolyHandles) ||
            !actual.LowPolyHandles.SequenceEqual(expected.LowPolyHandles) ||
            actual.Collision.Id != expected.Collision.Id ||
            actual.Collision.Assignment != expected.Collision.Assignment ||
            actual.Collision.TargetCell != expected.Collision.TargetCell ||
            !actual.Collision.VertexHandles.SequenceEqual(expected.Collision.VertexHandles) ||
            actual.Occlusion != expected.Occlusion)
        {
            throw new InvalidDataException(
                "The ID65 editor document terrain HP/LP/collision/occlusion handles are unsupported or stale.");
        }
    }

    private static byte[] WriteCanonical(
        Id65AuthoringEditorDocument document,
        string? contentSha256)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter json = new(buffer, new JsonWriterOptions
               {
                   Indented = false,
                   SkipValidation = false
               }))
        {
            json.WriteStartObject();
            json.WriteNumber("schemaVersion", document.SchemaVersion);
            json.WriteString("profileId", document.ProfileId);
            if (document.UsesStaticRemotePadAuthoring ||
                document.MandatoryBaseFaceTextureId == ResidentMandatoryBaseFaceTextureId)
            {
                json.WriteNumber(
                    "mandatoryBaseFaceTextureId",
                    document.MandatoryBaseFaceTextureId);
            }
            if (document.UsesRemotePadT107Grid)
            {
                json.WritePropertyName("t107Placement");
                json.WriteStartObject();
                json.WriteNumber("schemaVersion", Id65RemotePadGrassT107Catalog.SchemaVersion);
                json.WriteString("profileId", Id65RemotePadGrassT107Catalog.ProfileId);
                json.WriteString("positionId", document.T107RemotePadPositionId);
                json.WriteEndObject();
            }
            if (document.UsesRemotePadMobyChoice)
            {
                json.WritePropertyName("remotePadMobyChoice");
                json.WriteStartObject();
                json.WriteNumber("schemaVersion", Id65RemotePadMobyChoiceCatalog.SchemaVersion);
                json.WriteString("profileId", Id65RemotePadMobyChoiceCatalog.ProfileId);
                json.WriteString(
                    "kind",
                    Id65RemotePadMobyChoiceCatalog.GetKindId(
                        document.RemotePadMobyKind!.Value));
                json.WriteString("positionId", document.RemotePadMobyPositionId);
                json.WriteEndObject();
            }
            if (document.UsesRemotePadTerrainSet)
            {
                json.WritePropertyName("terrainSet");
                json.WriteStartObject();
                json.WriteNumber("schemaVersion", Id65AuthoringTerrainSetCatalog.SchemaVersion);
                json.WriteString("profileId", Id65AuthoringTerrainSetCatalog.ProfileId);
                json.WritePropertyName("activeUnitIds");
                json.WriteStartArray();
                foreach (string unitId in document._activeTerrainUnitIds)
                    json.WriteStringValue(unitId);
                json.WriteEndArray();
                json.WriteString("manifestSha256", document.TerrainSetManifestSha256);
                json.WriteEndObject();
            }
            if (document.UsesBlankCyclorama)
            {
                json.WritePropertyName("blankCyclorama");
                json.WriteStartObject();
                json.WriteNumber(
                    "schemaVersion",
                    Id65AuthoringBlankCycloramaCatalog.SchemaVersion);
                json.WriteString(
                    "profileId",
                    Id65AuthoringBlankCycloramaCatalog.ProfileId);
                json.WriteString("presetId", document.BlankCyclorama!.PresetId);
                json.WriteString("colorHex", document.BlankCyclorama.ColorHex);
                json.WriteEndObject();
            }
            json.WriteString(
                "labProfileId",
                UnusedLevel65BlankLevelLabProfileRegistry.ProfileId);
            json.WriteNumber(
                "labProfileVersion",
                UnusedLevel65BlankLevelLabProfileRegistry.ProfileVersion);
            json.WriteString("workspaceKey", UnusedLevel65BlankLevelLabProfileRegistry.Key);
            json.WriteNumber(
                "workspaceLayoutVersion",
                UnusedLevel65BlankLevelLabProfileRegistry.WorkspaceLayoutVersion);
            json.WriteString(
                "workspaceLayoutRelativeRoot",
                UnusedLevel65BlankLevelLabProfileRegistry.WorkspaceLayoutRelativeRoot);
            json.WriteString(
                "lockedBaseProfileId",
                UnusedLevel65BlankLevelLabProfileRegistry.Profile.LockedBaseProfileId);
            json.WriteString(
                "lockedBaseImageSha256",
                UnusedLevel65BlankLevelLabProfileRegistry.Profile.LockedBaseImageSha256);
            json.WriteString(
                "lockedRow80Sha256",
                Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256);
            json.WriteString("compositeProfileId", Id65AuthoringCompositeCompiler.ProfileId);
            json.WriteString(
                "privateTextureWitnessSha256",
                Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256);
            json.WriteString(
                "mobyBundleSha256",
                Id65AuthoringMobyDependencyBundleCompiler.ExpectedArtisansGrassBundleSha256);
            json.WritePropertyName("terrain");
            json.WriteStartArray();
            foreach (Id65AuthoringEditorTerrainIntent terrain in document._terrain)
                WriteTerrain(json, terrain);
            json.WriteEndArray();
            WritePrivateTexture(json, document.PrivateTextureSwap);
            json.WritePropertyName("mobyPlacements");
            json.WriteStartArray();
            foreach (Id65AuthoringEditorMobyPlacement placement in document._mobyPlacements)
                WritePlacement(json, placement);
            json.WriteEndArray();
            WriteBaselineHandles(json, document.BaselineHandles);
            if (contentSha256 != null)
                json.WriteString("contentSha256", contentSha256);
            json.WriteEndObject();
        }
        return buffer.ToArray();
    }

    private static void WriteTerrain(Utf8JsonWriter json, Id65AuthoringEditorTerrainIntent value)
    {
        json.WriteStartObject();
        json.WriteString("id", value.Id);
        json.WriteString("sectorId", value.SectorId);
        json.WriteNumber("sectorIndex", value.SectorIndex);
        json.WritePropertyName("addedVertex");
        WritePoint(json, value.AddedVertex);
        WriteStringArray(json, "highPolyHandles", value.HighPolyHandles);
        WriteStringArray(json, "lowPolyHandles", value.LowPolyHandles);
        json.WriteString("textureIntentId", value.TextureIntentId);
        json.WritePropertyName("collision");
        json.WriteStartObject();
        json.WriteString("id", value.Collision.Id);
        WriteStringArray(json, "vertexHandles", value.Collision.VertexHandles);
        json.WriteNumber("assignment", value.Collision.Assignment);
        json.WritePropertyName("targetCell");
        WriteCell(json, value.Collision.TargetCell);
        json.WriteEndObject();
        json.WritePropertyName("occlusion");
        json.WriteStartObject();
        json.WriteString("id", value.Occlusion.Id);
        json.WriteString("sectorId", value.Occlusion.SectorId);
        json.WriteNumber("groupIndex", value.Occlusion.GroupIndex);
        json.WriteEndObject();
        json.WriteEndObject();
    }

    private static void WritePrivateTexture(
        Utf8JsonWriter json,
        Id65AuthoringEditorPrivateTextureSwap value)
    {
        json.WritePropertyName("privateTextureSwap");
        json.WriteStartObject();
        json.WriteString("id", value.Id);
        json.WriteString("targetRenderFaceId", value.TargetRenderFaceId);
        json.WriteString("textureIntentId", value.TextureIntentId);
        json.WriteNumber("destinationRecordIndex", value.DestinationRecordIndex);
        json.WriteString("sourceHandle", value.SourceHandle);
        json.WriteNumber("donorWadEntry", value.DonorWadEntry);
        json.WriteNumber("donorTextureId", value.DonorTextureId);
        json.WriteNumber("materialTemplateTextureId", value.MaterialTemplateTextureId);
        json.WriteEndObject();
    }

    private static void WritePlacement(
        Utf8JsonWriter json,
        Id65AuthoringEditorMobyPlacement value)
    {
        json.WriteStartObject();
        json.WriteString("id", value.Id);
        json.WriteString("atomicGroupId", value.AtomicGroupId);
        json.WriteString("bundleId", value.BundleId);
        json.WriteString("rowTemplateId", value.RowTemplateId);
        json.WriteString("rowPolicy", value.RowPolicy);
        json.WriteNumber("targetTrueIndex", value.TargetTrueIndex);
        json.WriteString("coordinatePolicy", value.CoordinatePolicy);
        json.WritePropertyName("coordinatesOrDelta");
        if (value.CoordinatesOrDelta is Id65AuthoringEditorCoordinate point)
            WriteCoordinate(json, point);
        else
            json.WriteNullValue();
        if (value.YawByte.HasValue)
            json.WriteNumber("yawByte", value.YawByte.Value);
        else
            json.WriteNull("yawByte");
        if (value.SupportHandle != null)
            json.WriteString("supportHandle", value.SupportHandle);
        else
            json.WriteNull("supportHandle");
        json.WriteEndObject();
    }

    private static void WriteBaselineHandles(
        Utf8JsonWriter json,
        Id65AuthoringEditorBaselineHandles value)
    {
        json.WritePropertyName("baselineHandles");
        json.WriteStartObject();
        json.WriteString("spawn", value.Spawn);
        json.WriteString("music", value.Music);
        json.WriteString("totals", value.Totals);
        json.WriteString("exit", value.Exit);
        json.WriteString("save", value.Save);
        json.WriteEndObject();
    }

    private static void WritePoint(Utf8JsonWriter json, Id65AuthoringEditorPoint value)
    {
        json.WriteStartObject();
        json.WriteString("handle", value.Handle);
        json.WriteNumber("x", value.X);
        json.WriteNumber("y", value.Y);
        json.WriteNumber("z", value.Z);
        json.WriteEndObject();
    }

    private static void WriteCoordinate(
        Utf8JsonWriter json,
        Id65AuthoringEditorCoordinate value)
    {
        json.WriteStartObject();
        json.WriteNumber("x", value.X);
        json.WriteNumber("y", value.Y);
        json.WriteNumber("z", value.Z);
        json.WriteEndObject();
    }

    private static void WriteCell(
        Utf8JsonWriter json,
        Id65AuthoringEditorCollisionCell value)
    {
        json.WriteStartObject();
        json.WriteNumber("x", value.X);
        json.WriteNumber("y", value.Y);
        json.WriteNumber("z", value.Z);
        json.WriteEndObject();
    }

    private static void WriteStringArray(
        Utf8JsonWriter json,
        string name,
        IEnumerable<string> values)
    {
        json.WritePropertyName(name);
        json.WriteStartArray();
        foreach (string value in values)
            json.WriteStringValue(value);
        json.WriteEndArray();
    }

    private static string ReadRemotePadT107Placement(JsonElement root)
    {
        JsonElement value = RequireProperty(root, "t107Placement", "root");
        const string context = "t107Placement";
        RequireObjectProperties(
            value,
            context,
            "schemaVersion",
            "profileId",
            "positionId");
        int schemaVersion = RequireInt32(value, "schemaVersion", context);
        string profileId = RequireString(value, "profileId", context);
        string positionId = RequireString(value, "positionId", context);
        if (schemaVersion != Id65RemotePadGrassT107Catalog.SchemaVersion ||
            profileId != Id65RemotePadGrassT107Catalog.ProfileId)
        {
            throw new InvalidDataException(
                "The ID65 remote-pad T107 placement schema/profile is unsupported or stale.");
        }
        return Id65RemotePadGrassT107Catalog.GetRequiredPosition(positionId).PositionId;
    }

    private static (Id65RemotePadMobyKind Kind, string PositionId)
        ReadRemotePadMobyChoice(JsonElement root)
    {
        JsonElement value = RequireProperty(root, "remotePadMobyChoice", "root");
        const string context = "remotePadMobyChoice";
        RequireObjectProperties(
            value,
            context,
            "schemaVersion",
            "profileId",
            "kind",
            "positionId");
        int schemaVersion = RequireInt32(value, "schemaVersion", context);
        string profileId = RequireString(value, "profileId", context);
        if (schemaVersion != Id65RemotePadMobyChoiceCatalog.SchemaVersion ||
            profileId != Id65RemotePadMobyChoiceCatalog.ProfileId)
        {
            throw new InvalidDataException(
                "The ID65 remote-pad Moby-choice schema/profile is unsupported or stale.");
        }
        Id65RemotePadMobyKind kind = Id65RemotePadMobyChoiceCatalog.ParseKindId(
            RequireString(value, "kind", context));
        string positionId = Id65RemotePadMobyChoiceCatalog.GetRequiredPositionId(
            RequireString(value, "positionId", context));
        return (kind, positionId);
    }

    private static Id65AuthoringTerrainSetManifest ReadTerrainSet(JsonElement root)
    {
        JsonElement value = RequireProperty(root, "terrainSet", "root");
        const string context = "terrainSet";
        RequireObjectProperties(
            value,
            context,
            "schemaVersion",
            "profileId",
            "activeUnitIds",
            "manifestSha256");
        int schemaVersion = RequireInt32(value, "schemaVersion", context);
        string profileId = RequireString(value, "profileId", context);
        string[] activeUnitIds = ReadStringArray(value, "activeUnitIds", context);
        string manifestSha256 = RequireString(value, "manifestSha256", context);
        if (schemaVersion != Id65AuthoringTerrainSetCatalog.SchemaVersion ||
            profileId != Id65AuthoringTerrainSetCatalog.ProfileId)
        {
            throw new InvalidDataException(
                "The ID65 terrain-set schema/profile is unsupported or stale.");
        }
        Id65AuthoringTerrainSetManifest manifest =
            Id65AuthoringTerrainSetCatalog.Create(activeUnitIds);
        if (!manifest.ActiveUnits.Select(item => item.Id).SequenceEqual(activeUnitIds) ||
            !HashEquals(manifestSha256, manifest.CanonicalSha256))
        {
            throw new InvalidDataException(
                "The ID65 terrain-set unit order or selected manifest SHA-256 is noncanonical.");
        }
        return manifest;
    }

    private static Id65AuthoringBlankCycloramaIntent ReadBlankCyclorama(
        JsonElement root)
    {
        JsonElement value = RequireProperty(root, "blankCyclorama", "root");
        const string context = "blankCyclorama";
        RequireObjectProperties(
            value,
            context,
            "schemaVersion",
            "profileId",
            "presetId",
            "colorHex");
        int schemaVersion = RequireInt32(value, "schemaVersion", context);
        string profileId = RequireString(value, "profileId", context);
        if (schemaVersion != Id65AuthoringBlankCycloramaCatalog.SchemaVersion ||
            profileId != Id65AuthoringBlankCycloramaCatalog.ProfileId)
        {
            throw new InvalidDataException(
                "The ID65 blank-cyclorama schema/profile is unsupported or stale.");
        }
        Id65AuthoringBlankCycloramaIntent intent = new(
            RequireString(value, "presetId", context),
            RequireString(value, "colorHex", context));
        Id65AuthoringBlankCycloramaCatalog.RequireExact(intent);
        return intent;
    }

    private static Id65AuthoringEditorTerrainIntent[] ReadTerrain(JsonElement root)
    {
        JsonElement array = RequireArray(root, "terrain", "root");
        List<Id65AuthoringEditorTerrainIntent> result = new(array.GetArrayLength());
        int index = 0;
        foreach (JsonElement item in array.EnumerateArray())
        {
            string context = $"terrain[{index}]";
            RequireObjectProperties(
                item,
                context,
                "id",
                "sectorId",
                "sectorIndex",
                "addedVertex",
                "highPolyHandles",
                "lowPolyHandles",
                "textureIntentId",
                "collision",
                "occlusion");
            JsonElement collisionElement = RequireProperty(item, "collision", context);
            RequireObjectProperties(
                collisionElement,
                $"{context}.collision",
                "id",
                "vertexHandles",
                "assignment",
                "targetCell");
            JsonElement occlusionElement = RequireProperty(item, "occlusion", context);
            RequireObjectProperties(
                occlusionElement,
                $"{context}.occlusion",
                "id",
                "sectorId",
                "groupIndex");
            result.Add(new(
                RequireString(item, "id", context),
                RequireString(item, "sectorId", context),
                RequireInt32(item, "sectorIndex", context),
                ReadPoint(RequireProperty(item, "addedVertex", context), $"{context}.addedVertex"),
                ReadStringArray(item, "highPolyHandles", context),
                ReadStringArray(item, "lowPolyHandles", context),
                RequireString(item, "textureIntentId", context),
                new(
                    RequireString(collisionElement, "id", $"{context}.collision"),
                    ReadStringArray(
                        collisionElement,
                        "vertexHandles",
                        $"{context}.collision"),
                    RequireInt32(collisionElement, "assignment", $"{context}.collision"),
                    ReadCell(
                        RequireProperty(collisionElement, "targetCell", $"{context}.collision"),
                        $"{context}.collision.targetCell")),
                new(
                    RequireString(occlusionElement, "id", $"{context}.occlusion"),
                    RequireString(occlusionElement, "sectorId", $"{context}.occlusion"),
                    RequireInt32(occlusionElement, "groupIndex", $"{context}.occlusion"))));
            index++;
        }
        return result.ToArray();
    }

    private static Id65AuthoringEditorPrivateTextureSwap ReadPrivateTexture(JsonElement root)
    {
        JsonElement value = RequireProperty(root, "privateTextureSwap", "root");
        const string context = "privateTextureSwap";
        RequireObjectProperties(
            value,
            context,
            "id",
            "targetRenderFaceId",
            "textureIntentId",
            "destinationRecordIndex",
            "sourceHandle",
            "donorWadEntry",
            "donorTextureId",
            "materialTemplateTextureId");
        return new(
            RequireString(value, "id", context),
            RequireString(value, "targetRenderFaceId", context),
            RequireString(value, "textureIntentId", context),
            RequireInt32(value, "destinationRecordIndex", context),
            RequireString(value, "sourceHandle", context),
            RequireInt32(value, "donorWadEntry", context),
            RequireInt32(value, "donorTextureId", context),
            RequireInt32(value, "materialTemplateTextureId", context));
    }

    private static Id65AuthoringEditorMobyPlacement[] ReadPlacements(JsonElement root)
    {
        JsonElement array = RequireArray(root, "mobyPlacements", "root");
        List<Id65AuthoringEditorMobyPlacement> result = new(array.GetArrayLength());
        int index = 0;
        foreach (JsonElement item in array.EnumerateArray())
        {
            string context = $"mobyPlacements[{index}]";
            RequireObjectProperties(
                item,
                context,
                "id",
                "atomicGroupId",
                "bundleId",
                "rowTemplateId",
                "rowPolicy",
                "targetTrueIndex",
                "coordinatePolicy",
                "coordinatesOrDelta",
                "yawByte",
                "supportHandle");
            JsonElement coordinatesElement = RequireProperty(item, "coordinatesOrDelta", context);
            Id65AuthoringEditorCoordinate? coordinates = coordinatesElement.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.Object => ReadCoordinate(
                    coordinatesElement,
                    $"{context}.coordinatesOrDelta"),
                _ => throw new InvalidDataException(
                    $"The ID65 editor document field '{context}.coordinatesOrDelta' must be an object or null.")
            };
            JsonElement yawElement = RequireProperty(item, "yawByte", context);
            int? yaw = yawElement.ValueKind == JsonValueKind.Null
                ? null
                : RequireInt32Value(yawElement, $"{context}.yawByte");
            if (yaw is < byte.MinValue or > byte.MaxValue)
                throw new InvalidDataException($"The ID65 editor document field '{context}.yawByte' is outside one byte.");
            JsonElement supportElement = RequireProperty(item, "supportHandle", context);
            string? support = supportElement.ValueKind == JsonValueKind.Null
                ? null
                : RequireStringValue(supportElement, $"{context}.supportHandle");
            result.Add(new(
                RequireString(item, "id", context),
                RequireString(item, "atomicGroupId", context),
                RequireString(item, "bundleId", context),
                RequireString(item, "rowTemplateId", context),
                RequireString(item, "rowPolicy", context),
                RequireInt32(item, "targetTrueIndex", context),
                RequireString(item, "coordinatePolicy", context),
                coordinates,
                yaw.HasValue ? (byte)yaw.Value : null,
                support));
            index++;
        }
        return result.ToArray();
    }

    private static Id65AuthoringEditorBaselineHandles ReadBaselineHandles(JsonElement root)
    {
        JsonElement value = RequireProperty(root, "baselineHandles", "root");
        const string context = "baselineHandles";
        RequireObjectProperties(value, context, "spawn", "music", "totals", "exit", "save");
        return new(
            RequireString(value, "spawn", context),
            RequireString(value, "music", context),
            RequireString(value, "totals", context),
            RequireString(value, "exit", context),
            RequireString(value, "save", context));
    }

    private static Id65AuthoringEditorPoint ReadPoint(JsonElement value, string context)
    {
        RequireObjectProperties(value, context, "handle", "x", "y", "z");
        return new(
            RequireString(value, "handle", context),
            RequireInt32(value, "x", context),
            RequireInt32(value, "y", context),
            RequireInt32(value, "z", context));
    }

    private static Id65AuthoringEditorCoordinate ReadCoordinate(
        JsonElement value,
        string context)
    {
        RequireObjectProperties(value, context, "x", "y", "z");
        return new(
            RequireInt32(value, "x", context),
            RequireInt32(value, "y", context),
            RequireInt32(value, "z", context));
    }

    private static Id65AuthoringEditorCollisionCell ReadCell(
        JsonElement value,
        string context)
    {
        RequireObjectProperties(value, context, "x", "y", "z");
        return new(
            RequireInt32(value, "x", context),
            RequireInt32(value, "y", context),
            RequireInt32(value, "z", context));
    }

    private static string[] ReadStringArray(
        JsonElement owner,
        string name,
        string context)
    {
        JsonElement array = RequireArray(owner, name, context);
        List<string> values = new(array.GetArrayLength());
        int index = 0;
        foreach (JsonElement value in array.EnumerateArray())
        {
            values.Add(RequireStringValue(value, $"{context}.{name}[{index}]"));
            index++;
        }
        return values.ToArray();
    }

    private static void RequireObjectProperties(
        JsonElement value,
        string context,
        params string[] expectedNames)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"The ID65 editor document '{context}' must be an object.");
        HashSet<string> expected = expectedNames.ToHashSet(StringComparer.Ordinal);
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!seen.Add(property.Name))
                throw new InvalidDataException($"The ID65 editor document '{context}' contains duplicate field '{property.Name}'.");
            if (expected.Contains(property.Name))
                continue;
            if (IsUnsupportedPayloadOrAddressField(property.Name))
            {
                throw new InvalidDataException(
                    $"The ID65 editor document contains unsupported payload/address field '{context}.{property.Name}'.");
            }
            throw new InvalidDataException($"The ID65 editor document '{context}' contains unknown field '{property.Name}'.");
        }
        string? missing = expectedNames.FirstOrDefault(name => !seen.Contains(name));
        if (missing != null)
            throw new InvalidDataException($"The ID65 editor document '{context}' is missing field '{missing}'.");
    }

    private static bool IsUnsupportedPayloadOrAddressField(string name)
    {
        string normalized = name.ToLowerInvariant();
        return normalized.Contains("payload", StringComparison.Ordinal) ||
            normalized.Contains("bytes", StringComparison.Ordinal) ||
            normalized.Contains("hex", StringComparison.Ordinal) ||
            normalized.Contains("address", StringComparison.Ordinal) ||
            normalized.Contains("offset", StringComparison.Ordinal) ||
            normalized.Contains("path", StringComparison.Ordinal) ||
            normalized.Contains("lba", StringComparison.Ordinal) ||
            normalized.Contains("afterimage", StringComparison.Ordinal) ||
            normalized.Contains("publisher", StringComparison.Ordinal);
    }

    private static JsonElement RequireProperty(
        JsonElement owner,
        string name,
        string context)
    {
        if (!owner.TryGetProperty(name, out JsonElement value))
            throw new InvalidDataException($"The ID65 editor document '{context}' is missing field '{name}'.");
        return value;
    }

    private static JsonElement RequireArray(
        JsonElement owner,
        string name,
        string context)
    {
        JsonElement value = RequireProperty(owner, name, context);
        if (value.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException($"The ID65 editor document field '{context}.{name}' must be an array.");
        return value;
    }

    private static string RequireString(
        JsonElement owner,
        string name,
        string context) =>
        RequireStringValue(RequireProperty(owner, name, context), $"{context}.{name}");

    private static string RequireStringValue(JsonElement value, string context)
    {
        if (value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"The ID65 editor document field '{context}' must be a string.");
        string result = value.GetString() ?? "";
        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidDataException($"The ID65 editor document field '{context}' cannot be empty.");
        return result;
    }

    private static int RequireInt32(
        JsonElement owner,
        string name,
        string context) =>
        RequireInt32Value(RequireProperty(owner, name, context), $"{context}.{name}");

    private static int RequireInt32Value(JsonElement value, string context)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int result))
            throw new InvalidDataException($"The ID65 editor document field '{context}' must be one Int32.");
        return result;
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static bool HashEquals(string left, string right) =>
        left.Length == 64 && right.Length == 64 &&
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}

internal sealed record Id65AuthoringEditorProjectionRequest(
    Id65AuthoringEditorDocument Document,
    Id65AuthoringCompositeLockedSource LockedSource,
    Id65V2NativeTextureWitness PrivateTextureWitness,
    Id65MobyDependencyBundleDescriptor GrassBundle,
    Id65AuthoringCompositeCompilerLimits? Limits = null);

internal sealed class Id65AuthoringEditorProjection
{
    internal Id65AuthoringEditorProjection(
        Id65AuthoringEditorDocument document,
        Id65CompiledAuthoringComposite compiled)
    {
        Document = document;
        DocumentCanonicalSha256 = document.CanonicalSha256;
        Compiled = compiled;
    }

    public Id65AuthoringEditorDocument Document { get; }
    public string DocumentCanonicalSha256 { get; }
    public Id65CompiledAuthoringComposite Compiled { get; }
    public bool DocumentIdentityVerified => true;
    public bool TerrainHandlesVerified => true;
    public bool CollisionMembershipVerified => true;
    public bool OcclusionMembershipVerified => true;
    public bool PrivateTextureBindingVerified => true;
    public bool MobyPlacementBindingVerified => true;
    public bool BaselineOwnershipHandlesVerified => true;
    public bool SemanticReadbackVerified => true;
    public bool ExactFactoriesOnly => true;
    public bool PersistenceEnabled => false;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool WritesCue => false;
    public bool AppIntegrated => false;
    public bool CreateBinEnabled => false;
    public bool NormalCreateBinEnabled => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool ReleaseIntegrated => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;
}

/// <summary>
/// Pure adapter from the strict editor document into the already-pinned exact
/// factories and the single-pass composite compiler. It owns no persistence or
/// publication surface.
/// </summary>
internal static class Id65AuthoringEditorDocumentProjector
{
    public static Id65AuthoringEditorProjection Project(
        Id65AuthoringEditorProjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Document);
        ArgumentNullException.ThrowIfNull(request.LockedSource);
        ArgumentNullException.ThrowIfNull(request.PrivateTextureWitness);
        ArgumentNullException.ThrowIfNull(request.GrassBundle);

        // Reparse the canonical bytes so even a future document construction
        // path cannot bypass the strict serialized contract.
        Id65AuthoringEditorDocument document =
            Id65AuthoringEditorDocument.Parse(request.Document.SerializeUtf8());
        if (document.UsesStaticRemotePadAuthoring)
        {
            throw new InvalidDataException(
                "Schema-3/4 remote-pad authoring documents are canonical offline intent only; exact composite projection is not implemented.");
        }
        if (!HashEquals(
                request.PrivateTextureWitness.CanonicalSha256,
                document.PrivateTextureWitnessSha256) ||
            !HashEquals(
                request.GrassBundle.CanonicalSha256,
                document.MobyBundleSha256))
        {
            throw new InvalidDataException(
                "The ID65 editor projection inputs do not match the document's pinned texture-witness/Moby-bundle identities.");
        }
        Id65TerrainTriangleIntent? triangle = document.Terrain.Count == 0
            ? null
            : Id65AuthoringTerrainMutationCompiler.CreateConcreteTwoTileWitness();
        Id65MobyPlacementIntent[] placements = document.MobyPlacements
            .Select(ResolvePlacement)
            .ToArray();
        Id65AuthoringCompositeManifest manifest =
            Id65AuthoringCompositeCompiler.CreateManifest(
                triangle,
                request.PrivateTextureWitness,
                request.GrassBundle,
                placements,
                document.MandatoryBaseFaceTextureId);
        Id65CompiledAuthoringComposite compiled =
            Id65AuthoringCompositeCompiler.Compile(new(
                request.LockedSource,
                manifest,
                request.PrivateTextureWitness,
                [request.GrassBundle],
                placements,
                request.Limits));
        ValidateSemanticReadback(document, request, triangle, placements, compiled);
        return new(document, compiled);
    }

    private static Id65MobyPlacementIntent ResolvePlacement(
        Id65AuthoringEditorMobyPlacement placement)
    {
        Id65MobyPlacementIntent resolved = placement.Id switch
        {
            Id65AuthoringMobyDependencyBundleCompiler.T88PlacementId =>
                Id65AuthoringMobyDependencyBundleCompiler.CreateZeroEggT88Placement(),
            Id65AuthoringMobyDependencyBundleCompiler.T107PlacementId =>
                Id65AuthoringMobyDependencyBundleCompiler.CreateT107Placement(),
            _ => throw new InvalidDataException(
                $"The ID65 editor document Moby placement '{placement.Id}' has no exact factory.")
        };
        Id65AuthoringEditorMobyPlacement exact = ToEditorPlacement(resolved);
        if (placement != exact)
            throw new InvalidDataException("The ID65 editor document Moby placement changed after validation.");
        return resolved;
    }

    private static Id65AuthoringEditorMobyPlacement ToEditorPlacement(
        Id65MobyPlacementIntent placement)
    {
        string rowPolicy = placement.RowPolicy switch
        {
            Id65MobyRowAllocationPolicy.AppendContiguous =>
                Id65AuthoringEditorDocument.AppendContiguousRowPolicy,
            Id65MobyRowAllocationPolicy.ReplaceExact =>
                Id65AuthoringEditorDocument.ReplaceExactRowPolicy,
            _ => throw new InvalidDataException("The exact ID65 Grass placement row policy changed.")
        };
        string coordinatePolicy = placement.CoordinatePolicy switch
        {
            Id65MobyCoordinatePolicy.PreserveDestination =>
                Id65AuthoringEditorDocument.PreserveDestinationCoordinatePolicy,
            Id65MobyCoordinatePolicy.AuthorExact =>
                Id65AuthoringEditorDocument.AuthorExactCoordinatePolicy,
            _ => throw new InvalidDataException("The exact ID65 Grass placement coordinate policy changed.")
        };
        Id65AuthoringEditorCoordinate? coordinates = placement.CoordinatesOrDelta is
            Id65AuthoringPoint point
            ? new(point.X, point.Y, point.Z)
            : null;
        return new(
            placement.Id,
            placement.AtomicGroupId,
            placement.BundleId,
            placement.RowTemplateId,
            rowPolicy,
            placement.TargetTrueIndex,
            coordinatePolicy,
            coordinates,
            placement.YawByte,
            placement.SupportHandle);
    }

    private static void ValidateSemanticReadback(
        Id65AuthoringEditorDocument document,
        Id65AuthoringEditorProjectionRequest request,
        Id65TerrainTriangleIntent? triangle,
        IReadOnlyList<Id65MobyPlacementIntent> placements,
        Id65CompiledAuthoringComposite compiled)
    {
        if (compiled.ProfileId != Id65AuthoringCompositeCompiler.ProfileId ||
            compiled.Manifest.OptionalTerrainTriangle != triangle ||
            !compiled.Manifest.Placements.SequenceEqual(placements) ||
            !HashEquals(compiled.Manifest.TextureWitnessSha256, request.PrivateTextureWitness.CanonicalSha256) ||
            !HashEquals(compiled.Manifest.MobyBundleSha256, request.GrassBundle.CanonicalSha256))
        {
            throw new InvalidDataException("The ID65 editor projection manifest readback changed.");
        }

        bool hasTerrain = document.Terrain.Count == 1;
        Id65AuthoringCompositeTerrainReadback terrain = compiled.Terrain;
        if (!terrain.MandatoryV3CollisionAndEnvironmentHeaderRepairs ||
            terrain.OptionalTrianglePresent != hasTerrain)
        {
            throw new InvalidDataException("The ID65 editor projection terrain-presence readback changed.");
        }
        if (hasTerrain)
        {
            Id65AuthoringEditorTerrainIntent expected = document.Terrain[0];
            Id65TerrainTriangleReadback readback = terrain.OptionalTriangle ??
                throw new InvalidDataException("The ID65 editor projection omitted its terrain readback.");
            if (readback.TriangleId != expected.Id ||
                readback.AddedVertexHandle != expected.AddedVertex.Handle ||
                !readback.PresentInOutput ||
                readback.SectorIndex != expected.SectorIndex ||
                readback.CollisionCell.X != expected.Collision.TargetCell.X ||
                readback.CollisionCell.Y != expected.Collision.TargetCell.Y ||
                readback.CollisionCell.Z != expected.Collision.TargetCell.Z ||
                readback.OcclusionGroupIndex != expected.Occlusion.GroupIndex)
            {
                throw new InvalidDataException(
                    "The ID65 editor projection HP/LP/collision/occlusion semantic readback changed.");
            }

            RequireAuthoredRebase(
                compiled.HandleRebases,
                expected.Id,
                "terrain-tile",
                "terrain-tile",
                216,
                1);
            RequireAuthoredRebase(
                compiled.HandleRebases,
                $"{expected.Id}:hp",
                "high-detail-face",
                "scene-sector",
                216,
                1);
            RequireAuthoredRebase(
                compiled.HandleRebases,
                $"{expected.Id}:lp",
                "low-detail-face",
                "scene-sector",
                216,
                1);

            int[] vertexOrdinals = [1, 3, 2];
            for (int index = 0; index < vertexOrdinals.Length; index++)
            {
                RequireAuthoredRebase(
                    compiled.HandleRebases,
                    expected.HighPolyHandles[index],
                    "high-detail-vertex",
                    "scene-sector",
                    216,
                    vertexOrdinals[index]);
                RequireAuthoredRebase(
                    compiled.HandleRebases,
                    expected.LowPolyHandles[index],
                    "low-detail-vertex",
                    "scene-sector",
                    216,
                    vertexOrdinals[index]);
            }

            RequireAuthoredRebase(
                compiled.HandleRebases,
                expected.Collision.Id,
                "collision-triangle",
                "collision-triangle",
                19_808,
                0);
            RequireAuthoredRebase(
                compiled.HandleRebases,
                expected.Occlusion.Id,
                "occlusion-membership",
                "occlusion-group",
                expected.Occlusion.GroupIndex,
                expected.SectorIndex);
        }
        else if (terrain.OptionalTriangle != null)
        {
            throw new InvalidDataException("The ID65 editor projection produced an unrequested terrain readback.");
        }

        Id65AuthoringEditorPrivateTextureSwap privateTexture = document.PrivateTextureSwap;
        Id65AuthoringCompositeTextureReadback texture = compiled.Texture;
        if (texture.PrivateTextureId != privateTexture.DestinationRecordIndex ||
            texture.DonorWadEntry != privateTexture.DonorWadEntry ||
            texture.DonorTextureId != privateTexture.DonorTextureId ||
            texture.TemplateTextureId != privateTexture.MaterialTemplateTextureId ||
            texture.RenderFaceId != privateTexture.TargetRenderFaceId ||
            texture.OutputTextureId != document.MandatoryBaseFaceTextureId)
        {
            throw new InvalidDataException("The ID65 editor projection mandatory face texture semantic readback changed.");
        }
        RequireAuthoredRebase(
            compiled.HandleRebases,
            privateTexture.TextureIntentId,
            "texture-record",
            "texture-record",
            privateTexture.DestinationRecordIndex,
            0);
        RequireAuthoredRebase(
            compiled.HandleRebases,
            $"{privateTexture.TargetRenderFaceId}:hp",
            "high-detail-face",
            "scene-sector",
            216,
            0);
        RequireAuthoredRebase(
            compiled.HandleRebases,
            $"{privateTexture.TargetRenderFaceId}:lp",
            "low-detail-face",
            "scene-sector",
            216,
            0);

        if (compiled.MobyPlacements.Count != document.MobyPlacements.Count)
            throw new InvalidDataException("The ID65 editor projection Moby readback count changed.");
        Dictionary<int, Id65MobyPlacementReadback> byTrueIndex =
            compiled.MobyPlacements.ToDictionary(item => item.TargetTrueIndex);
        foreach (Id65AuthoringEditorMobyPlacement placement in document.MobyPlacements)
        {
            if (!byTrueIndex.TryGetValue(placement.TargetTrueIndex, out Id65MobyPlacementReadback? readback) ||
                readback.PlacementId != placement.Id ||
                (placement.CoordinatesOrDelta is Id65AuthoringEditorCoordinate coordinate &&
                 (readback.RawX != coordinate.X || readback.RawY != coordinate.Y || readback.RawZ != coordinate.Z)))
            {
                throw new InvalidDataException("The ID65 editor projection Grass placement semantic readback changed.");
            }
        }

        RequirePreservedBaselineRebase(
            compiled.HandleRebases,
            document.BaselineHandles.Spawn,
            "landing-record",
            "landing-record",
            0);
        RequirePreservedBaselineRebase(
            compiled.HandleRebases,
            document.BaselineHandles.Music,
            "music-slot",
            "music-slot",
            35);
        RequirePreservedBaselineRebase(
            compiled.HandleRebases,
            document.BaselineHandles.Totals,
            "totals-slot",
            "level-slot",
            65);
        RequirePreservedBaselineRebase(
            compiled.HandleRebases,
            document.BaselineHandles.Exit,
            "exit-slot",
            "level-slot",
            65);
        RequirePreservedBaselineRebase(
            compiled.HandleRebases,
            document.BaselineHandles.Save,
            "save-slot",
            "level-slot",
            65);

        if (compiled.WritesFileSystem || compiled.WritesDiscImage || compiled.WritesCue ||
            compiled.AppIntegrated || compiled.CreateBinEnabled || compiled.NormalCreateBinEnabled ||
            compiled.RuntimeCandidateAuthorized || compiled.ReleaseIntegrated ||
            compiled.PromotionAuthorized || compiled.Publishable)
        {
            throw new InvalidDataException("The ID65 editor projection crossed a production capability boundary.");
        }
    }

    private static void RequirePreservedBaselineRebase(
        IReadOnlyList<Id65StableHandleRebase> rebases,
        string stableId,
        string kind,
        string addressSpace,
        int primary)
    {
        Id65StableHandleRebase? rebase = rebases.SingleOrDefault(item => item.StableId == stableId);
        if (rebase == null || rebase.Kind != kind ||
            rebase.Source is not Id65LogicalAddress source ||
            rebase.Output is not Id65LogicalAddress output ||
            source.Space != addressSpace || output.Space != addressSpace ||
            source.Primary != primary || output.Primary != primary ||
            source.Secondary != 0 || output.Secondary != 0 || source != output)
        {
            throw new InvalidDataException(
                $"The ID65 editor projection baseline ownership rebase '{stableId}' changed.");
        }
    }

    private static void RequireAuthoredRebase(
        IReadOnlyList<Id65StableHandleRebase> rebases,
        string stableId,
        string kind,
        string outputSpace,
        int outputPrimary,
        int outputSecondary)
    {
        Id65StableHandleRebase? rebase = rebases.SingleOrDefault(item => item.StableId == stableId);
        if (rebase == null || rebase.Kind != kind || rebase.Source != null ||
            rebase.Output is not Id65LogicalAddress output ||
            output.Space != outputSpace || output.Primary != outputPrimary ||
            output.Secondary != outputSecondary)
        {
            throw new InvalidDataException(
                $"The ID65 editor projection authored rebase '{stableId}' changed.");
        }
    }

    private static bool HashEquals(string left, string right) =>
        left.Length == 64 && right.Length == 64 &&
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
