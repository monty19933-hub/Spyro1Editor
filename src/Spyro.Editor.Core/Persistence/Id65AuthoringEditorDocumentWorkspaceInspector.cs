using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Persistence;

/// <summary>
/// Read-only Core bridge for recognizing and inspecting the one canonical
/// persisted ID65 Blank-Level Lab editor document. The returned model contains
/// semantic handles and hashes only; it never contains paths or game bytes.
/// </summary>
public static class Id65AuthoringEditorDocumentWorkspaceInspector
{
    public const string DocumentFileName = Id65AuthoringEditorDocumentStore.FileName;

    public static async Task<Id65AuthoringEditorDocumentReadModel?> InspectAsync(
        UnusedLevel65BlankLevelLabWorkspacePaths paths,
        UnusedLevel65BlankLevelLabManifest expectedLabManifest,
        CancellationToken cancellationToken = default)
    {
        Id65AuthoringEditorDocumentEnvelopeIdentity? identity =
            await Id65AuthoringEditorDocumentStore.InspectEnvelopeIdentityAsync(
                paths,
                expectedLabManifest,
                cancellationToken);
        return identity == null
            ? null
            : new Id65AuthoringEditorDocumentReadModel(identity);
    }

    internal static Id65AuthoringEditorDocumentReadModel InspectCanonicalDocument(
        ReadOnlySpan<byte> canonicalUtf8) =>
        new(Id65AuthoringEditorDocument.Parse(canonicalUtf8));
}

/// <summary>
/// Immutable, path-free semantic view of one strictly validated persisted ID65
/// authoring document and the projection identities recorded with it.
/// </summary>
public sealed class Id65AuthoringEditorDocumentReadModel
{
    private const string ReadModelFormat =
        "spyro-editor-id65-authoring-editor-document-read-model";
    public const int LegacyReadModelSchemaVersion = 2;
    public const int RemotePadGridReadModelSchemaVersion = 3;
    public const int RemotePadMobyChoiceReadModelSchemaVersion = 4;
    public const int RemotePadMobyChoiceTerrainSetReadModelSchemaVersion = 5;
    public const int RemotePadMobyChoiceTerrainSetBlankCycloramaReadModelSchemaVersion = 6;

    private readonly ReadOnlyCollection<Id65AuthoringEditorTerrainReadModel> _terrain;
    private readonly ReadOnlyCollection<Id65AuthoringEditorMobyPlacementReadModel>
        _mobyPlacements;
    private readonly ReadOnlyCollection<string> _activeTerrainUnitIds;

    internal Id65AuthoringEditorDocumentReadModel(
        Id65AuthoringEditorDocumentEnvelopeIdentity identity)
        : this(identity.Document, identity)
    {
    }

    internal Id65AuthoringEditorDocumentReadModel(Id65AuthoringEditorDocument document)
        : this(document, identity: null)
    {
    }

    private Id65AuthoringEditorDocumentReadModel(
        Id65AuthoringEditorDocument document,
        Id65AuthoringEditorDocumentEnvelopeIdentity? identity)
    {
        ArgumentNullException.ThrowIfNull(document);

        SchemaVersion = document.SchemaVersion;
        ProfileId = document.ProfileId;
        MandatoryBaseFaceTextureId = document.MandatoryBaseFaceTextureId;
        T107PlacementSemantics = document.UsesRemotePadMobyChoice
            ? Id65AuthoringEditorT107PlacementSemantics.RemotePadMobyChoiceV1
            : document.UsesRemotePadT107Grid
                ? Id65AuthoringEditorT107PlacementSemantics.RemotePadGridV1
                : Id65AuthoringEditorT107PlacementSemantics.LegacyNativeApronV1;
        T107RemotePadPositionId = document.T107RemotePadPositionId;
        T107RemotePadPosition = document.T107RemotePadPositionId is string positionId
            ? Id65RemotePadGrassT107PositionReadModel.FromCatalog(
                Id65RemotePadGrassT107Catalog.GetRequiredPosition(positionId))
            : null;
        RemotePadMobyKind = document.RemotePadMobyKind;
        RemotePadMobyPositionId = document.RemotePadMobyPositionId;
        RemotePadMobyPosition = document.RemotePadMobyPositionId is string mobyPositionId
            ? Id65RemotePadMobyPositionReadModel.FromCatalog(
                document.RemotePadMobyKind!.Value,
                mobyPositionId)
            : null;
        TerrainSelectionSemantics = document.UsesRemotePadTerrainSet
            ? Id65AuthoringEditorTerrainSelectionSemantics.RemotePadTerrainSetV1
            : Id65AuthoringEditorTerrainSelectionSemantics.LegacyOptionalTriangleV1;
        TerrainSetManifestSha256 = document.TerrainSetManifestSha256 ?? string.Empty;
        _activeTerrainUnitIds = Array.AsReadOnly(document.ActiveTerrainUnitIds.ToArray());
        if (document.BlankCyclorama is Id65AuthoringBlankCycloramaIntent blankCyclorama)
        {
            Id65AuthoringBlankCycloramaCatalog.RequireExact(blankCyclorama);
            BlankCycloramaPresetId = blankCyclorama.PresetId;
            BlankCycloramaColorHex = blankCyclorama.ColorHex;
        }
        else
        {
            BlankCycloramaPresetId = string.Empty;
            BlankCycloramaColorHex = string.Empty;
        }
        LabProfileId = document.LabProfileId;
        LabProfileVersion = document.LabProfileVersion;
        WorkspaceKey = document.WorkspaceKey;
        WorkspaceLayoutVersion = document.WorkspaceLayoutVersion;
        LockedBaseProfileId = document.LockedBaseProfileId;
        LockedBaseImageSha256 = document.LockedBaseImageSha256;
        LockedRow80Sha256 = document.LockedRow80Sha256;
        CompositeProfileId = document.CompositeProfileId;
        PrivateTextureWitnessSha256 = document.PrivateTextureWitnessSha256;
        MobyBundleSha256 = document.MobyBundleSha256;
        DocumentContentSha256 = document.ContentSha256;
        DocumentCanonicalSha256 = document.CanonicalSha256;
        EnvelopeCanonicalSha256 = identity?.EnvelopeCanonicalSha256 ?? string.Empty;
        EnvelopeSchemaVersion = identity?.EnvelopeSchemaVersion ?? 0;
        ProjectionContractId = identity?.ProjectionContractId ?? string.Empty;
        CompositeManifestSha256 = identity?.CompositeManifestSha256 ?? string.Empty;
        CompositeOutputRow80Sha256 = identity?.CompositeOutputRow80Sha256 ?? string.Empty;
        CompositeDeterministicPlanSha256 =
            identity?.CompositeDeterministicPlanSha256 ?? string.Empty;
        StaticAuthoringProfileId = identity?.StaticAuthoringProfileId ?? string.Empty;
        StaticAuthoringCatalogSha256 =
            identity?.StaticAuthoringCatalogSha256 ?? string.Empty;
        StaticAuthoringSceneGraphSha256 =
            identity?.StaticAuthoringSceneGraphSha256 ?? string.Empty;
        StaticAuthoringProjectionSha256 =
            identity?.StaticAuthoringProjectionSha256 ?? string.Empty;
        StaticAuthoringTerrainManifestSha256 =
            identity?.StaticAuthoringTerrainManifestSha256 ?? string.Empty;
        StaticAuthoringCycloramaCatalogSha256 =
            identity?.StaticAuthoringCycloramaCatalogSha256 ?? string.Empty;
        PersistedProjectionIdentityPresent = identity != null;

        _terrain = Array.AsReadOnly(document.Terrain
            .Select(Id65AuthoringEditorTerrainReadModel.FromInternal)
            .ToArray());
        PrivateTextureSwap =
            Id65AuthoringEditorPrivateTextureSwapReadModel.FromInternal(
                document.PrivateTextureSwap);
        _mobyPlacements = Array.AsReadOnly(document.MobyPlacements
            .Select(Id65AuthoringEditorMobyPlacementReadModel.FromInternal)
            .ToArray());
        BaselineHandles = Id65AuthoringEditorBaselineHandlesReadModel.FromInternal(
            document.BaselineHandles);
        ReadModelCanonicalSha256 = ComputeCanonicalSha256();
    }

    public int SchemaVersion { get; }
    public int ReadModelSchemaVersion => UsesBlankCyclorama
        ? RemotePadMobyChoiceTerrainSetBlankCycloramaReadModelSchemaVersion
        : UsesRemotePadTerrainSet
        ? RemotePadMobyChoiceTerrainSetReadModelSchemaVersion
        : UsesRemotePadMobyChoice
        ? RemotePadMobyChoiceReadModelSchemaVersion
        : UsesRemotePadT107Grid
            ? RemotePadGridReadModelSchemaVersion
        : LegacyReadModelSchemaVersion;
    public string ProfileId { get; }
    public int MandatoryBaseFaceTextureId { get; }
    public Id65AuthoringEditorT107PlacementSemantics T107PlacementSemantics { get; }
    public string? T107RemotePadPositionId { get; }
    public Id65RemotePadGrassT107PositionReadModel? T107RemotePadPosition { get; }
    public bool UsesRemotePadT107Grid =>
        T107PlacementSemantics == Id65AuthoringEditorT107PlacementSemantics.RemotePadGridV1;
    public Id65RemotePadMobyKind? RemotePadMobyKind { get; }
    public string? RemotePadMobyPositionId { get; }
    public Id65RemotePadMobyPositionReadModel? RemotePadMobyPosition { get; }
    public bool UsesRemotePadMobyChoice =>
        T107PlacementSemantics ==
            Id65AuthoringEditorT107PlacementSemantics.RemotePadMobyChoiceV1;
    public Id65AuthoringEditorTerrainSelectionSemantics TerrainSelectionSemantics { get; }
    public bool UsesRemotePadTerrainSet => TerrainSelectionSemantics ==
        Id65AuthoringEditorTerrainSelectionSemantics.RemotePadTerrainSetV1;
    public string TerrainSetManifestSha256 { get; }
    public IReadOnlyList<string> ActiveTerrainUnitIds => _activeTerrainUnitIds;
    public bool UsesBlankCyclorama => BlankCycloramaPresetId.Length != 0;
    public string BlankCycloramaPresetId { get; }
    public string BlankCycloramaColorHex { get; }
    public bool UsesStaticRemotePadAuthoring =>
        UsesRemotePadT107Grid || UsesRemotePadMobyChoice;
    public bool RequiresExplicitRemotePadMigration => !UsesStaticRemotePadAuthoring;
    public string LabProfileId { get; }
    public int LabProfileVersion { get; }
    public string WorkspaceKey { get; }
    public int WorkspaceLayoutVersion { get; }
    public string LockedBaseProfileId { get; }
    public string LockedBaseImageSha256 { get; }
    public string LockedRow80Sha256 { get; }
    public string CompositeProfileId { get; }
    public string PrivateTextureWitnessSha256 { get; }
    public string MobyBundleSha256 { get; }
    public string DocumentContentSha256 { get; }
    public string DocumentCanonicalSha256 { get; }
    public string EnvelopeCanonicalSha256 { get; }
    public int EnvelopeSchemaVersion { get; }
    public string ProjectionContractId { get; }
    public string CompositeManifestSha256 { get; }
    public string CompositeOutputRow80Sha256 { get; }
    public string CompositeDeterministicPlanSha256 { get; }
    public string StaticAuthoringProfileId { get; }
    public string StaticAuthoringCatalogSha256 { get; }
    public string StaticAuthoringSceneGraphSha256 { get; }
    public string StaticAuthoringProjectionSha256 { get; }
    public string StaticAuthoringTerrainManifestSha256 { get; }
    public string StaticAuthoringCycloramaCatalogSha256 { get; }
    public bool HasCompiledCompositeProjection =>
        PersistedProjectionIdentityPresent && !UsesStaticRemotePadAuthoring;
    public bool HasStaticAuthoringProjection =>
        PersistedProjectionIdentityPresent && UsesStaticRemotePadAuthoring;
    public IReadOnlyList<Id65AuthoringEditorTerrainReadModel> Terrain => _terrain;
    public Id65AuthoringEditorPrivateTextureSwapReadModel PrivateTextureSwap { get; }
    public IReadOnlyList<Id65AuthoringEditorMobyPlacementReadModel> MobyPlacements =>
        _mobyPlacements;
    public Id65AuthoringEditorBaselineHandlesReadModel BaselineHandles { get; }
    public string ReadModelCanonicalSha256 { get; }

    public bool EnvelopeCanonicalIdentityVerified => PersistedProjectionIdentityPresent;
    public bool DocumentCanonicalIdentityVerified => true;
    public bool PersistedProjectionIdentityPresent { get; }
    public bool DeterministicEnvelopeReadbackVerified => PersistedProjectionIdentityPresent;
    public bool CurrentProjectionRevalidated => false;
    public bool CurrentLockedSourceReprojected => false;
    public bool CurrentOnDiskFreshnessVerified => false;
    public bool ContainsGameBytes => false;
    public bool ContainsNativePayloadBytes => false;
    public bool ContainsPhysicalAddresses => false;
    public bool ContainsFilePaths => false;
    public bool ContainsCompiledAfterimage => false;
    public bool ContainsPublisherState => false;
    public bool PersistenceEnabled => false;
    public bool FileSystemMutationPerformed => false;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool WritesCue => false;
    public bool CarriesAppOrUiAuthority => false;
    public bool CreateBinEnabled => false;
    public bool NormalCreateBinEnabled => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool RuntimeProven => false;
    public bool ReleaseIntegrated => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;
    public bool RequiresTrustedExclusiveWorkspaceDirectory => true;
    public bool HostileFilesystemRaceSafe => false;
    public bool CommonWorkspaceLeaseOwned => false;

    private string ComputeCanonicalSha256()
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter json = new(buffer, new JsonWriterOptions
               {
                   Indented = false,
                   SkipValidation = false
               }))
        {
            json.WriteStartObject();
            json.WriteString("format", ReadModelFormat);
            json.WriteNumber(
                "readModelSchemaVersion",
                ReadModelSchemaVersion);
            json.WriteNumber("documentSchemaVersion", SchemaVersion);
            json.WriteString("profileId", ProfileId);
            if (UsesRemotePadMobyChoice)
            {
                json.WriteNumber("envelopeSchemaVersion", EnvelopeSchemaVersion);
                json.WriteString(
                    "remotePadMobyChoiceProfileId",
                    Id65RemotePadMobyChoiceCatalog.ProfileId);
                json.WriteString(
                    "remotePadMobyKind",
                    Id65RemotePadMobyChoiceCatalog.GetKindId(RemotePadMobyKind!.Value));
                WriteRemotePadMobyPosition(json, RemotePadMobyPosition!);
                if (UsesRemotePadTerrainSet)
                {
                    json.WritePropertyName("terrainSet");
                    json.WriteStartObject();
                    json.WriteNumber(
                        "catalogSchemaVersion",
                        Id65AuthoringTerrainSetCatalog.SchemaVersion);
                    json.WriteString(
                        "catalogProfileId",
                        Id65AuthoringTerrainSetCatalog.ProfileId);
                    json.WritePropertyName("activeUnitIds");
                    json.WriteStartArray();
                    foreach (string unitId in ActiveTerrainUnitIds)
                        json.WriteStringValue(unitId);
                    json.WriteEndArray();
                    json.WriteString("manifestSha256", TerrainSetManifestSha256);
                    json.WriteEndObject();
                    if (UsesBlankCyclorama)
                    {
                        json.WritePropertyName("blankCyclorama");
                        json.WriteStartObject();
                        json.WriteNumber(
                            "catalogSchemaVersion",
                            Id65AuthoringBlankCycloramaCatalog.SchemaVersion);
                        json.WriteString(
                            "catalogProfileId",
                            Id65AuthoringBlankCycloramaCatalog.ProfileId);
                        json.WriteString(
                            "catalogCanonicalSha256",
                            Id65AuthoringBlankCycloramaCatalog.CanonicalSha256);
                        json.WriteString("presetId", BlankCycloramaPresetId);
                        json.WriteString("colorHex", BlankCycloramaColorHex);
                        json.WriteEndObject();
                    }
                }
            }
            else if (UsesRemotePadT107Grid)
            {
                json.WriteNumber("envelopeSchemaVersion", EnvelopeSchemaVersion);
                json.WriteString(
                    "t107PlacementSemantics",
                    Id65RemotePadGrassT107Catalog.ProfileId);
                WriteRemotePadPosition(json, T107RemotePadPosition!);
            }
            json.WriteNumber("mandatoryBaseFaceTextureId", MandatoryBaseFaceTextureId);
            json.WriteString("labProfileId", LabProfileId);
            json.WriteNumber("labProfileVersion", LabProfileVersion);
            json.WriteString("workspaceKey", WorkspaceKey);
            json.WriteNumber("workspaceLayoutVersion", WorkspaceLayoutVersion);
            json.WriteString("lockedBaseProfileId", LockedBaseProfileId);
            json.WriteString("lockedBaseImageSha256", LockedBaseImageSha256);
            json.WriteString("lockedRow80Sha256", LockedRow80Sha256);
            json.WriteString(
                UsesStaticRemotePadAuthoring
                    ? "documentCompositeProfileId"
                    : "compositeProfileId",
                CompositeProfileId);
            json.WriteString(
                "privateTextureWitnessSha256",
                PrivateTextureWitnessSha256);
            json.WriteString("mobyBundleSha256", MobyBundleSha256);
            json.WriteString("documentContentSha256", DocumentContentSha256);
            json.WriteString("documentCanonicalSha256", DocumentCanonicalSha256);
            json.WriteString("envelopeCanonicalSha256", EnvelopeCanonicalSha256);
            json.WriteString("projectionContractId", ProjectionContractId);
            if (UsesStaticRemotePadAuthoring)
            {
                json.WriteString("staticAuthoringProfileId", StaticAuthoringProfileId);
                json.WriteString(
                    "staticAuthoringCatalogSha256",
                    StaticAuthoringCatalogSha256);
                json.WriteString(
                    "staticAuthoringSceneGraphSha256",
                    StaticAuthoringSceneGraphSha256);
                json.WriteString(
                    "staticAuthoringProjectionSha256",
                    StaticAuthoringProjectionSha256);
                if (UsesRemotePadTerrainSet)
                {
                    json.WriteString(
                        "staticAuthoringTerrainManifestSha256",
                        StaticAuthoringTerrainManifestSha256);
                    if (UsesBlankCyclorama)
                    {
                        json.WriteString(
                            "staticAuthoringCycloramaCatalogSha256",
                            StaticAuthoringCycloramaCatalogSha256);
                    }
                }
                json.WriteBoolean(
                    "compiledCompositeProjectionPresent",
                    HasCompiledCompositeProjection);
            }
            else
            {
                json.WriteString("compositeManifestSha256", CompositeManifestSha256);
                json.WriteString("compositeOutputRow80Sha256", CompositeOutputRow80Sha256);
                json.WriteString(
                    "compositeDeterministicPlanSha256",
                    CompositeDeterministicPlanSha256);
            }
            json.WritePropertyName("terrain");
            json.WriteStartArray();
            foreach (Id65AuthoringEditorTerrainReadModel terrain in Terrain)
                WriteTerrain(json, terrain);
            json.WriteEndArray();
            WritePrivateTextureSwap(json, PrivateTextureSwap);
            json.WritePropertyName("mobyPlacements");
            json.WriteStartArray();
            foreach (Id65AuthoringEditorMobyPlacementReadModel placement in MobyPlacements)
                WriteMobyPlacement(json, placement);
            json.WriteEndArray();
            WriteBaselineHandles(json, BaselineHandles);
            json.WriteEndObject();
        }
        return Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray()));
    }

    private static void WriteTerrain(
        Utf8JsonWriter json,
        Id65AuthoringEditorTerrainReadModel value)
    {
        json.WriteStartObject();
        json.WriteString("id", value.Id);
        json.WriteString("sectorId", value.SectorId);
        json.WriteNumber("sectorIndex", value.SectorIndex);
        json.WritePropertyName("addedVertex");
        json.WriteStartObject();
        json.WriteString("handle", value.AddedVertex.Handle);
        json.WriteNumber("x", value.AddedVertex.X);
        json.WriteNumber("y", value.AddedVertex.Y);
        json.WriteNumber("z", value.AddedVertex.Z);
        json.WriteEndObject();
        WriteStringArray(json, "highPolyHandles", value.HighPolyHandles);
        WriteStringArray(json, "lowPolyHandles", value.LowPolyHandles);
        json.WriteString("textureIntentId", value.TextureIntentId);
        json.WritePropertyName("collision");
        json.WriteStartObject();
        json.WriteString("id", value.Collision.Id);
        WriteStringArray(json, "vertexHandles", value.Collision.VertexHandles);
        json.WriteNumber("assignment", value.Collision.Assignment);
        json.WritePropertyName("targetCell");
        json.WriteStartObject();
        json.WriteNumber("x", value.Collision.TargetCell.X);
        json.WriteNumber("y", value.Collision.TargetCell.Y);
        json.WriteNumber("z", value.Collision.TargetCell.Z);
        json.WriteEndObject();
        json.WriteEndObject();
        json.WritePropertyName("occlusion");
        json.WriteStartObject();
        json.WriteString("id", value.Occlusion.Id);
        json.WriteString("sectorId", value.Occlusion.SectorId);
        json.WriteNumber("groupIndex", value.Occlusion.GroupIndex);
        json.WriteEndObject();
        json.WriteEndObject();
    }

    private static void WriteRemotePadPosition(
        Utf8JsonWriter json,
        Id65RemotePadGrassT107PositionReadModel value)
    {
        json.WritePropertyName("t107RemotePadPosition");
        json.WriteStartObject();
        json.WriteNumber("catalogSchemaVersion", Id65RemotePadGrassT107Catalog.SchemaVersion);
        json.WriteString("catalogProfileId", Id65RemotePadGrassT107Catalog.ProfileId);
        json.WriteNumber("ordinal", value.Ordinal);
        json.WriteString("positionId", value.PositionId);
        json.WriteString("label", value.Label);
        json.WritePropertyName("editor");
        json.WriteStartObject();
        json.WriteNumber("x", value.EditorX);
        json.WriteNumber("y", value.EditorY);
        json.WriteNumber("z", value.EditorZ);
        json.WriteEndObject();
        json.WritePropertyName("raw");
        json.WriteStartObject();
        json.WriteNumber("x", value.RawX);
        json.WriteNumber("y", value.RawY);
        json.WriteNumber("z", value.RawZ);
        json.WriteEndObject();
        json.WriteString("rowSha256", value.RowSha256);
        json.WriteBoolean("isDefault", value.IsDefault);
        json.WriteEndObject();
    }

    private static void WriteRemotePadMobyPosition(
        Utf8JsonWriter json,
        Id65RemotePadMobyPositionReadModel value)
    {
        json.WritePropertyName("remotePadMobyPosition");
        json.WriteStartObject();
        json.WriteNumber("catalogSchemaVersion", Id65RemotePadMobyChoiceCatalog.SchemaVersion);
        json.WriteString("catalogProfileId", Id65RemotePadMobyChoiceCatalog.ProfileId);
        json.WriteNumber("ordinal", value.Ordinal);
        json.WriteString("positionId", value.PositionId);
        json.WriteString("label", value.Label);
        json.WritePropertyName("editor");
        json.WriteStartObject();
        json.WriteNumber("x", value.EditorX);
        json.WriteNumber("y", value.EditorY);
        json.WriteNumber("z", value.EditorZ);
        json.WriteEndObject();
        json.WritePropertyName("raw");
        json.WriteStartObject();
        json.WriteNumber("x", value.RawX);
        json.WriteNumber("y", value.RawY);
        json.WriteNumber("z", value.RawZ);
        json.WriteEndObject();
        if (value.SelectedExactRowSha256 is null)
            json.WriteNull("selectedExactRowSha256");
        else
            json.WriteString("selectedExactRowSha256", value.SelectedExactRowSha256);
        json.WriteBoolean("isDefault", value.IsDefault);
        json.WriteEndObject();
    }

    private static void WritePrivateTextureSwap(
        Utf8JsonWriter json,
        Id65AuthoringEditorPrivateTextureSwapReadModel value)
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
        json.WriteNumber(
            "materialTemplateTextureId",
            value.MaterialTemplateTextureId);
        json.WriteEndObject();
    }

    private static void WriteMobyPlacement(
        Utf8JsonWriter json,
        Id65AuthoringEditorMobyPlacementReadModel value)
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
        if (value.CoordinatesOrDelta == null)
        {
            json.WriteNullValue();
        }
        else
        {
            json.WriteStartObject();
            json.WriteNumber("x", value.CoordinatesOrDelta.X);
            json.WriteNumber("y", value.CoordinatesOrDelta.Y);
            json.WriteNumber("z", value.CoordinatesOrDelta.Z);
            json.WriteEndObject();
        }
        if (value.YawByte.HasValue)
            json.WriteNumber("yawByte", value.YawByte.Value);
        else
            json.WriteNull("yawByte");
        if (value.SupportHandle == null)
            json.WriteNull("supportHandle");
        else
            json.WriteString("supportHandle", value.SupportHandle);
        json.WriteEndObject();
    }

    private static void WriteBaselineHandles(
        Utf8JsonWriter json,
        Id65AuthoringEditorBaselineHandlesReadModel value)
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
}

public sealed class Id65AuthoringEditorPointReadModel
{
    internal Id65AuthoringEditorPointReadModel(string handle, int x, int y, int z)
    {
        Handle = handle;
        X = x;
        Y = y;
        Z = z;
    }

    public string Handle { get; }
    public int X { get; }
    public int Y { get; }
    public int Z { get; }

    internal static Id65AuthoringEditorPointReadModel FromInternal(
        Id65AuthoringEditorPoint value) =>
        new(value.Handle, value.X, value.Y, value.Z);
}

public sealed class Id65AuthoringEditorCollisionCellReadModel
{
    internal Id65AuthoringEditorCollisionCellReadModel(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public int X { get; }
    public int Y { get; }
    public int Z { get; }

    internal static Id65AuthoringEditorCollisionCellReadModel FromInternal(
        Id65AuthoringEditorCollisionCell value) =>
        new(value.X, value.Y, value.Z);
}

public sealed class Id65AuthoringEditorCollisionReadModel
{
    private readonly ReadOnlyCollection<string> _vertexHandles;

    internal Id65AuthoringEditorCollisionReadModel(
        string id,
        IEnumerable<string> vertexHandles,
        int assignment,
        Id65AuthoringEditorCollisionCellReadModel targetCell)
    {
        Id = id;
        _vertexHandles = Array.AsReadOnly(vertexHandles.ToArray());
        Assignment = assignment;
        TargetCell = targetCell;
    }

    public string Id { get; }
    public IReadOnlyList<string> VertexHandles => _vertexHandles;
    public int Assignment { get; }
    public Id65AuthoringEditorCollisionCellReadModel TargetCell { get; }

    internal static Id65AuthoringEditorCollisionReadModel FromInternal(
        Id65AuthoringEditorCollisionIntent value) =>
        new(
            value.Id,
            value.VertexHandles,
            value.Assignment,
            Id65AuthoringEditorCollisionCellReadModel.FromInternal(value.TargetCell));
}

public sealed class Id65AuthoringEditorOcclusionReadModel
{
    internal Id65AuthoringEditorOcclusionReadModel(
        string id,
        string sectorId,
        int groupIndex)
    {
        Id = id;
        SectorId = sectorId;
        GroupIndex = groupIndex;
    }

    public string Id { get; }
    public string SectorId { get; }
    public int GroupIndex { get; }

    internal static Id65AuthoringEditorOcclusionReadModel FromInternal(
        Id65AuthoringEditorOcclusionMembership value) =>
        new(value.Id, value.SectorId, value.GroupIndex);
}

public sealed class Id65AuthoringEditorTerrainReadModel
{
    private readonly ReadOnlyCollection<string> _highPolyHandles;
    private readonly ReadOnlyCollection<string> _lowPolyHandles;

    internal Id65AuthoringEditorTerrainReadModel(
        string id,
        string sectorId,
        int sectorIndex,
        Id65AuthoringEditorPointReadModel addedVertex,
        IEnumerable<string> highPolyHandles,
        IEnumerable<string> lowPolyHandles,
        string textureIntentId,
        Id65AuthoringEditorCollisionReadModel collision,
        Id65AuthoringEditorOcclusionReadModel occlusion)
    {
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
    public Id65AuthoringEditorPointReadModel AddedVertex { get; }
    public IReadOnlyList<string> HighPolyHandles => _highPolyHandles;
    public IReadOnlyList<string> LowPolyHandles => _lowPolyHandles;
    public string TextureIntentId { get; }
    public Id65AuthoringEditorCollisionReadModel Collision { get; }
    public Id65AuthoringEditorOcclusionReadModel Occlusion { get; }

    internal static Id65AuthoringEditorTerrainReadModel FromInternal(
        Id65AuthoringEditorTerrainIntent value) =>
        new(
            value.Id,
            value.SectorId,
            value.SectorIndex,
            Id65AuthoringEditorPointReadModel.FromInternal(value.AddedVertex),
            value.HighPolyHandles,
            value.LowPolyHandles,
            value.TextureIntentId,
            Id65AuthoringEditorCollisionReadModel.FromInternal(value.Collision),
            Id65AuthoringEditorOcclusionReadModel.FromInternal(value.Occlusion));
}

public sealed class Id65AuthoringEditorPrivateTextureSwapReadModel
{
    internal Id65AuthoringEditorPrivateTextureSwapReadModel(
        string id,
        string targetRenderFaceId,
        string textureIntentId,
        int destinationRecordIndex,
        string sourceHandle,
        int donorWadEntry,
        int donorTextureId,
        int materialTemplateTextureId)
    {
        Id = id;
        TargetRenderFaceId = targetRenderFaceId;
        TextureIntentId = textureIntentId;
        DestinationRecordIndex = destinationRecordIndex;
        SourceHandle = sourceHandle;
        DonorWadEntry = donorWadEntry;
        DonorTextureId = donorTextureId;
        MaterialTemplateTextureId = materialTemplateTextureId;
    }

    public string Id { get; }
    public string TargetRenderFaceId { get; }
    public string TextureIntentId { get; }
    public int DestinationRecordIndex { get; }
    public string SourceHandle { get; }
    public int DonorWadEntry { get; }
    public int DonorTextureId { get; }
    public int MaterialTemplateTextureId { get; }

    internal static Id65AuthoringEditorPrivateTextureSwapReadModel FromInternal(
        Id65AuthoringEditorPrivateTextureSwap value) =>
        new(
            value.Id,
            value.TargetRenderFaceId,
            value.TextureIntentId,
            value.DestinationRecordIndex,
            value.SourceHandle,
            value.DonorWadEntry,
            value.DonorTextureId,
            value.MaterialTemplateTextureId);
}

/// <summary>
/// Immutable, path-free copy of one frozen remote-pad catalog position. The
/// read model deliberately carries both editor and raw units so persistence
/// consumers never need to infer or round a placement coordinate.
/// </summary>
public sealed class Id65RemotePadGrassT107PositionReadModel
{
    private Id65RemotePadGrassT107PositionReadModel(
        int ordinal,
        string positionId,
        string label,
        int editorX,
        int editorY,
        int editorZ,
        int rawX,
        int rawY,
        int rawZ,
        string rowSha256,
        bool isDefault)
    {
        Ordinal = ordinal;
        PositionId = positionId;
        Label = label;
        EditorX = editorX;
        EditorY = editorY;
        EditorZ = editorZ;
        RawX = rawX;
        RawY = rawY;
        RawZ = rawZ;
        RowSha256 = rowSha256;
        IsDefault = isDefault;
    }

    public int Ordinal { get; }
    public string PositionId { get; }
    public string Label { get; }
    public int EditorX { get; }
    public int EditorY { get; }
    public int EditorZ { get; }
    public int RawX { get; }
    public int RawY { get; }
    public int RawZ { get; }
    public string RowSha256 { get; }
    public bool IsDefault { get; }

    internal static Id65RemotePadGrassT107PositionReadModel FromCatalog(
        Id65RemotePadGrassT107Position value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
            value.Ordinal,
            value.PositionId,
            value.Label,
            value.EditorX,
            value.EditorY,
            value.EditorZ,
            value.RawX,
            value.RawY,
            value.RawZ,
            value.RowSha256,
            value.IsDefault);
    }
}

/// <summary>
/// Immutable shared-grid readback for a schema-4 Moby choice. The selected row
/// hash is null for None and exact for Grass or Red Gem.
/// </summary>
public sealed class Id65RemotePadMobyPositionReadModel
{
    private Id65RemotePadMobyPositionReadModel(
        int ordinal,
        string positionId,
        string label,
        int editorX,
        int editorY,
        int editorZ,
        int rawX,
        int rawY,
        int rawZ,
        string? selectedExactRowSha256,
        bool isDefault)
    {
        Ordinal = ordinal;
        PositionId = positionId;
        Label = label;
        EditorX = editorX;
        EditorY = editorY;
        EditorZ = editorZ;
        RawX = rawX;
        RawY = rawY;
        RawZ = rawZ;
        SelectedExactRowSha256 = selectedExactRowSha256;
        IsDefault = isDefault;
    }

    public int Ordinal { get; }
    public string PositionId { get; }
    public string Label { get; }
    public int EditorX { get; }
    public int EditorY { get; }
    public int EditorZ { get; }
    public int RawX { get; }
    public int RawY { get; }
    public int RawZ { get; }
    public string? SelectedExactRowSha256 { get; }
    public bool IsDefault { get; }

    internal static Id65RemotePadMobyPositionReadModel FromCatalog(
        Id65RemotePadMobyKind kind,
        string positionId)
    {
        string exactId = Id65RemotePadMobyChoiceCatalog.GetRequiredPositionId(positionId);
        Id65RemotePadGrassT107Position position =
            Id65RemotePadGrassT107Catalog.GetRequiredPosition(exactId);
        return new(
            position.Ordinal,
            position.PositionId,
            position.Label,
            position.EditorX,
            position.EditorY,
            position.EditorZ,
            position.RawX,
            position.RawY,
            position.RawZ,
            Id65RemotePadMobyChoiceCatalog.GetSelectedExactRowSha256(kind, exactId),
            position.IsDefault);
    }
}

public sealed class Id65AuthoringEditorCoordinateReadModel
{
    internal Id65AuthoringEditorCoordinateReadModel(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public int X { get; }
    public int Y { get; }
    public int Z { get; }

    internal static Id65AuthoringEditorCoordinateReadModel FromInternal(
        Id65AuthoringEditorCoordinate value) =>
        new(value.X, value.Y, value.Z);
}

public sealed class Id65AuthoringEditorMobyPlacementReadModel
{
    internal Id65AuthoringEditorMobyPlacementReadModel(
        string id,
        string atomicGroupId,
        string bundleId,
        string rowTemplateId,
        string rowPolicy,
        int targetTrueIndex,
        string coordinatePolicy,
        Id65AuthoringEditorCoordinateReadModel? coordinatesOrDelta,
        byte? yawByte,
        string? supportHandle)
    {
        Id = id;
        AtomicGroupId = atomicGroupId;
        BundleId = bundleId;
        RowTemplateId = rowTemplateId;
        RowPolicy = rowPolicy;
        TargetTrueIndex = targetTrueIndex;
        CoordinatePolicy = coordinatePolicy;
        CoordinatesOrDelta = coordinatesOrDelta;
        YawByte = yawByte;
        SupportHandle = supportHandle;
    }

    public string Id { get; }
    public string AtomicGroupId { get; }
    public string BundleId { get; }
    public string RowTemplateId { get; }
    public string RowPolicy { get; }
    public int TargetTrueIndex { get; }
    public string CoordinatePolicy { get; }
    public Id65AuthoringEditorCoordinateReadModel? CoordinatesOrDelta { get; }
    public byte? YawByte { get; }
    public string? SupportHandle { get; }

    internal static Id65AuthoringEditorMobyPlacementReadModel FromInternal(
        Id65AuthoringEditorMobyPlacement value) =>
        new(
            value.Id,
            value.AtomicGroupId,
            value.BundleId,
            value.RowTemplateId,
            value.RowPolicy,
            value.TargetTrueIndex,
            value.CoordinatePolicy,
            value.CoordinatesOrDelta is Id65AuthoringEditorCoordinate coordinate
                ? Id65AuthoringEditorCoordinateReadModel.FromInternal(coordinate)
                : null,
            value.YawByte,
            value.SupportHandle);
}

public sealed class Id65AuthoringEditorBaselineHandlesReadModel
{
    internal Id65AuthoringEditorBaselineHandlesReadModel(
        string spawn,
        string music,
        string totals,
        string exit,
        string save)
    {
        Spawn = spawn;
        Music = music;
        Totals = totals;
        Exit = exit;
        Save = save;
    }

    public string Spawn { get; }
    public string Music { get; }
    public string Totals { get; }
    public string Exit { get; }
    public string Save { get; }

    internal static Id65AuthoringEditorBaselineHandlesReadModel FromInternal(
        Id65AuthoringEditorBaselineHandles value) =>
        new(value.Spawn, value.Music, value.Totals, value.Exit, value.Save);
}
