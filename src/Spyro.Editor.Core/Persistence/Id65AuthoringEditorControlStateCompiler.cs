using System.Collections.ObjectModel;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Persistence;

/// <summary>
/// Names the exact, non-interchangeable meaning of the T107 choice. Legacy
/// native-apron documents and schema-3 remote-pad-grid documents must never be
/// inferred from one another.
/// </summary>
public enum Id65AuthoringEditorT107PlacementSemantics
{
    LegacyNativeApronV1 = 1,
    RemotePadGridV1 = 2,
    RemotePadMobyChoiceV1 = 3
}

/// <summary>
/// Separates the legacy zero-or-tile1 structural choice from the schema-5
/// closed terrain-set selection. A schema-5 empty/tile1 state must not collapse
/// back into the schema-4 family merely because its visible topology matches.
/// </summary>
public enum Id65AuthoringEditorTerrainSelectionSemantics
{
    LegacyOptionalTriangleV1 = 1,
    RemotePadTerrainSetV1 = 2
}

/// <summary>
/// Framework-neutral control state for the exact first ID65 authoring document.
/// It models only choices already proved by the canonical document/compiler
/// chain. The mandatory base face is exactly Resident T25 or Proved private T66;
/// baseline spawn/music/totals/exit/save ownership remains fixed and cannot be
/// disabled or replaced through this surface.
/// </summary>
public sealed class Id65AuthoringEditorControlState
{
    public const string ProfileId =
        "id65-authoring-editor-control-state-exact-document-v1";
    public const int SchemaVersion = 1;
    public const string RemotePadGridProfileId =
        "id65-authoring-editor-control-state-t107-placement-v2";
    public const int RemotePadGridSchemaVersion = 2;
    public const string RemotePadMobyChoiceProfileId =
        "id65-authoring-editor-control-state-remote-pad-moby-choice-v3";
    public const int RemotePadMobyChoiceSchemaVersion = 3;
    public const string RemotePadMobyChoiceTerrainSetProfileId =
        "id65-authoring-editor-control-state-remote-pad-moby-choice-terrain-set-v4";
    public const int RemotePadMobyChoiceTerrainSetSchemaVersion = 4;
    public const string RemotePadMobyChoiceTerrainSetBlankCycloramaProfileId =
        "id65-authoring-editor-control-state-remote-pad-moby-choice-terrain-set-blank-cyclorama-v5";
    public const int RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion = 5;
    public const int T88TargetTrueIndex = 88;
    public const int T107TargetTrueIndex = 107;
    public const int ResidentMandatoryBaseFaceTextureId = 25;
    public const int PrivateMandatoryBaseFaceTextureId = 66;

    public Id65AuthoringEditorControlState(
        bool includeStructuralTerrain,
        bool includeGrassAtT88,
        bool includeGrassAtT107,
        int mandatoryBaseFaceTextureId = PrivateMandatoryBaseFaceTextureId)
        : this(
            includeStructuralTerrain,
            includeGrassAtT88,
            includeGrassAtT107,
            mandatoryBaseFaceTextureId,
            Id65AuthoringEditorT107PlacementSemantics.LegacyNativeApronV1,
            t107RemotePadPositionId: null,
            remotePadMobyKind: null,
            remotePadMobyPositionId: null,
            Id65AuthoringEditorTerrainSelectionSemantics.LegacyOptionalTriangleV1,
            includeTerrainTile2: false,
            includeBlankCyclorama: false)
    {
    }

    private Id65AuthoringEditorControlState(
        bool includeStructuralTerrain,
        bool includeGrassAtT88,
        bool includeGrassAtT107,
        int mandatoryBaseFaceTextureId,
        Id65AuthoringEditorT107PlacementSemantics t107PlacementSemantics,
        string? t107RemotePadPositionId,
        Id65RemotePadMobyKind? remotePadMobyKind,
        string? remotePadMobyPositionId,
        Id65AuthoringEditorTerrainSelectionSemantics terrainSelectionSemantics,
        bool includeTerrainTile2,
        bool includeBlankCyclorama)
    {
        if (mandatoryBaseFaceTextureId is not
            (ResidentMandatoryBaseFaceTextureId or PrivateMandatoryBaseFaceTextureId))
            throw new ArgumentOutOfRangeException(nameof(mandatoryBaseFaceTextureId));
        if (t107PlacementSemantics == Id65AuthoringEditorT107PlacementSemantics.LegacyNativeApronV1)
        {
            if (t107RemotePadPositionId is not null || remotePadMobyKind.HasValue ||
                remotePadMobyPositionId is not null)
            {
                throw new ArgumentException(
                    "Legacy native-apron T107 state cannot carry a remote-pad position.",
                    nameof(t107RemotePadPositionId));
            }
        }
        else if (t107PlacementSemantics == Id65AuthoringEditorT107PlacementSemantics.RemotePadGridV1)
        {
            if (remotePadMobyKind.HasValue || remotePadMobyPositionId is not null)
            {
                throw new ArgumentException(
                    "Schema-3 remote-pad state cannot carry a schema-4 Moby choice.",
                    nameof(remotePadMobyKind));
            }
            t107RemotePadPositionId = Id65RemotePadGrassT107Catalog
                .GetRequiredPosition(t107RemotePadPositionId!)
                .PositionId;
        }
        else if (t107PlacementSemantics ==
                 Id65AuthoringEditorT107PlacementSemantics.RemotePadMobyChoiceV1)
        {
            if (t107RemotePadPositionId is not null || !remotePadMobyKind.HasValue ||
                includeGrassAtT88 || mandatoryBaseFaceTextureId != PrivateMandatoryBaseFaceTextureId)
            {
                throw new ArgumentException(
                    "Schema-4 remote-pad Moby-choice state requires T88 off, T66, and no schema-3 position.");
            }
            _ = Id65RemotePadMobyChoiceCatalog.GetRequiredChoice(remotePadMobyKind.Value);
            remotePadMobyPositionId = Id65RemotePadMobyChoiceCatalog.GetRequiredPositionId(
                remotePadMobyPositionId!);
            if (includeGrassAtT107 !=
                (remotePadMobyKind.Value == Id65RemotePadMobyKind.GrassT107))
            {
                throw new ArgumentException(
                    "Schema-4 Grass inclusion must match the mutually exclusive Moby choice.");
            }
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(t107PlacementSemantics));
        }
        if (terrainSelectionSemantics ==
            Id65AuthoringEditorTerrainSelectionSemantics.LegacyOptionalTriangleV1)
        {
            if (includeTerrainTile2)
                throw new ArgumentException(
                    "Legacy ID65 terrain selection cannot carry tile 2.",
                    nameof(includeTerrainTile2));
        }
        else if (terrainSelectionSemantics ==
                 Id65AuthoringEditorTerrainSelectionSemantics.RemotePadTerrainSetV1)
        {
            if (t107PlacementSemantics !=
                    Id65AuthoringEditorT107PlacementSemantics.RemotePadMobyChoiceV1 ||
                mandatoryBaseFaceTextureId != PrivateMandatoryBaseFaceTextureId)
            {
                throw new ArgumentException(
                    "Schema-5 terrain-set selection requires the T66 remote-pad Moby-choice family.");
            }
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(terrainSelectionSemantics));
        }
        if (includeBlankCyclorama)
        {
            if (terrainSelectionSemantics !=
                    Id65AuthoringEditorTerrainSelectionSemantics.RemotePadTerrainSetV1)
            {
                throw new ArgumentException(
                    "The blank cyclorama requires exact schema-5 terrain-set semantics.",
                    nameof(includeBlankCyclorama));
            }
            Id65AuthoringBlankCycloramaCatalog.RequireExact(
                Id65AuthoringBlankCycloramaCatalog.CreateBlankSlate());
        }
        IncludeStructuralTerrain = includeStructuralTerrain;
        IncludeTerrainTile2 = includeTerrainTile2;
        IncludeGrassAtT88 = includeGrassAtT88;
        IncludeGrassAtT107 = includeGrassAtT107;
        MandatoryBaseFaceTextureId = mandatoryBaseFaceTextureId;
        T107PlacementSemantics = t107PlacementSemantics;
        T107RemotePadPositionId = t107RemotePadPositionId;
        RemotePadMobyKind = remotePadMobyKind;
        RemotePadMobyPositionId = remotePadMobyPositionId;
        TerrainSelectionSemantics = terrainSelectionSemantics;
        BlankCycloramaPresetId = includeBlankCyclorama
            ? Id65AuthoringBlankCycloramaCatalog.BlankSlatePresetId
            : string.Empty;
        BlankCycloramaColorHex = includeBlankCyclorama
            ? Id65AuthoringBlankCycloramaCatalog.BlankSlateColorHex
            : string.Empty;
    }

    public bool IncludeStructuralTerrain { get; }
    public bool IncludeTerrainTile1 => IncludeStructuralTerrain;
    public bool IncludeTerrainTile2 { get; }
    public bool HasAnyStructuralTerrain => IncludeTerrainTile1 || IncludeTerrainTile2;
    public bool IncludeGrassAtT88 { get; }
    public bool IncludeGrassAtT107 { get; }
    public int MandatoryBaseFaceTextureId { get; }
    public Id65AuthoringEditorT107PlacementSemantics T107PlacementSemantics { get; }
    public string? T107RemotePadPositionId { get; }
    public Id65RemotePadMobyKind? RemotePadMobyKind { get; }
    public string? RemotePadMobyPositionId { get; }
    public Id65AuthoringEditorTerrainSelectionSemantics TerrainSelectionSemantics { get; }
    public bool UsesRemotePadT107Grid =>
        T107PlacementSemantics == Id65AuthoringEditorT107PlacementSemantics.RemotePadGridV1;
    public bool UsesRemotePadMobyChoice =>
        T107PlacementSemantics ==
            Id65AuthoringEditorT107PlacementSemantics.RemotePadMobyChoiceV1;
    public bool UsesRemotePadTerrainSet => TerrainSelectionSemantics ==
        Id65AuthoringEditorTerrainSelectionSemantics.RemotePadTerrainSetV1;
    public bool UsesBlankCyclorama => BlankCycloramaPresetId.Length != 0;
    public string BlankCycloramaPresetId { get; }
    public string BlankCycloramaColorHex { get; }
    public bool IncludeRedGemAtT21 =>
        RemotePadMobyKind == Id65RemotePadMobyKind.RedGemT21;

    public static Id65AuthoringEditorControlState CreateDefault() =>
        new(
            includeStructuralTerrain: true,
            includeGrassAtT88: true,
            includeGrassAtT107: true,
            mandatoryBaseFaceTextureId: PrivateMandatoryBaseFaceTextureId);

    /// <summary>
    /// Creates the genuinely new-workspace default: structural base intent,
    /// no visible Grass, and a retained dormant Center choice for future T107.
    /// </summary>
    public static Id65AuthoringEditorControlState CreateRemotePadDefault() =>
        CreateRemotePad(
            includeStructuralTerrain: true,
            includeGrassAtT88: false,
            includeGrassAtT107: false,
            Id65RemotePadGrassT107Catalog.DefaultPositionId,
            mandatoryBaseFaceTextureId: PrivateMandatoryBaseFaceTextureId);

    public static Id65AuthoringEditorControlState CreateRemotePad(
        bool includeStructuralTerrain,
        bool includeGrassAtT88,
        bool includeGrassAtT107,
        string t107RemotePadPositionId,
        int mandatoryBaseFaceTextureId = PrivateMandatoryBaseFaceTextureId) =>
        new(
            includeStructuralTerrain,
            includeGrassAtT88,
            includeGrassAtT107,
            mandatoryBaseFaceTextureId,
            Id65AuthoringEditorT107PlacementSemantics.RemotePadGridV1,
            t107RemotePadPositionId,
            remotePadMobyKind: null,
            remotePadMobyPositionId: null,
            Id65AuthoringEditorTerrainSelectionSemantics.LegacyOptionalTriangleV1,
            includeTerrainTile2: false,
            includeBlankCyclorama: false);

    public static Id65AuthoringEditorControlState CreateRemotePadMobyChoiceDefault() =>
        CreateRemotePadMobyChoice(
            includeStructuralTerrain: true,
            Id65RemotePadMobyKind.None,
            Id65RemotePadMobyChoiceCatalog.DefaultPositionId);

    public static Id65AuthoringEditorControlState CreateRemotePadMobyChoice(
        bool includeStructuralTerrain,
        Id65RemotePadMobyKind kind,
        string remotePadMobyPositionId) =>
        new(
            includeStructuralTerrain,
            includeGrassAtT88: false,
            includeGrassAtT107: kind == Id65RemotePadMobyKind.GrassT107,
            mandatoryBaseFaceTextureId: PrivateMandatoryBaseFaceTextureId,
            Id65AuthoringEditorT107PlacementSemantics.RemotePadMobyChoiceV1,
            t107RemotePadPositionId: null,
            remotePadMobyKind: kind,
            remotePadMobyPositionId,
            Id65AuthoringEditorTerrainSelectionSemantics.LegacyOptionalTriangleV1,
            includeTerrainTile2: false,
            includeBlankCyclorama: false);

    public static Id65AuthoringEditorControlState
        CreateRemotePadMobyChoiceTerrainSet(
            bool includeTerrainTile1,
            bool includeTerrainTile2,
            Id65RemotePadMobyKind kind,
            string remotePadMobyPositionId) =>
        new(
            includeTerrainTile1,
            includeGrassAtT88: false,
            includeGrassAtT107: kind == Id65RemotePadMobyKind.GrassT107,
            mandatoryBaseFaceTextureId: PrivateMandatoryBaseFaceTextureId,
            Id65AuthoringEditorT107PlacementSemantics.RemotePadMobyChoiceV1,
            t107RemotePadPositionId: null,
            remotePadMobyKind: kind,
            remotePadMobyPositionId,
            Id65AuthoringEditorTerrainSelectionSemantics.RemotePadTerrainSetV1,
            includeTerrainTile2,
            includeBlankCyclorama: false);

    public static Id65AuthoringEditorControlState
        CreateRemotePadMobyChoiceTerrainSetDefault() =>
        CreateRemotePadMobyChoiceTerrainSet(
            includeTerrainTile1: true,
            includeTerrainTile2: false,
            Id65RemotePadMobyKind.None,
            Id65RemotePadMobyChoiceCatalog.DefaultPositionId);

    public static Id65AuthoringEditorControlState
        CreateRemotePadMobyChoiceTerrainSetBlankCyclorama(
            bool includeTerrainTile1,
            bool includeTerrainTile2,
            Id65RemotePadMobyKind kind,
            string remotePadMobyPositionId) =>
        new(
            includeTerrainTile1,
            includeGrassAtT88: false,
            includeGrassAtT107: kind == Id65RemotePadMobyKind.GrassT107,
            mandatoryBaseFaceTextureId: PrivateMandatoryBaseFaceTextureId,
            Id65AuthoringEditorT107PlacementSemantics.RemotePadMobyChoiceV1,
            t107RemotePadPositionId: null,
            remotePadMobyKind: kind,
            remotePadMobyPositionId,
            Id65AuthoringEditorTerrainSelectionSemantics.RemotePadTerrainSetV1,
            includeTerrainTile2,
            includeBlankCyclorama: true);

    public static Id65AuthoringEditorControlState
        CreateRemotePadMobyChoiceTerrainSetBlankCycloramaDefault() =>
        CreateRemotePadMobyChoiceTerrainSetBlankCyclorama(
            includeTerrainTile1: true,
            includeTerrainTile2: false,
            Id65RemotePadMobyKind.None,
            Id65RemotePadMobyChoiceCatalog.DefaultPositionId);

    /// <summary>
    /// Explicit one-way migration. Calling this method is the only API that may
    /// choose Center for legacy state; ordinary construction/recovery never does.
    /// </summary>
    public static Id65AuthoringEditorControlState MigrateLegacyToRemotePadGrid(
        Id65AuthoringEditorControlState legacyState,
        string? t107RemotePadPositionId = null)
    {
        ArgumentNullException.ThrowIfNull(legacyState);
        if (legacyState.T107PlacementSemantics !=
            Id65AuthoringEditorT107PlacementSemantics.LegacyNativeApronV1)
        {
            throw new InvalidOperationException(
                "Only legacy native-apron T107 control state can be migrated to the remote-pad grid.");
        }
        return CreateRemotePad(
            legacyState.IncludeStructuralTerrain,
            legacyState.IncludeGrassAtT88,
            legacyState.IncludeGrassAtT107,
            t107RemotePadPositionId ?? Id65RemotePadGrassT107Catalog.DefaultPositionId,
            legacyState.MandatoryBaseFaceTextureId);
    }

    /// <summary>
    /// Explicit schema-3 to schema-4 migration. It is deliberately narrower
    /// than ordinary editing: T88 must already be off, T66 is retained, the
    /// grid position is preserved, and T107 maps only to Grass or None.
    /// </summary>
    public static Id65AuthoringEditorControlState MigrateRemotePadGridToMobyChoice(
        Id65AuthoringEditorControlState remotePadGridState)
    {
        ArgumentNullException.ThrowIfNull(remotePadGridState);
        if (!remotePadGridState.UsesRemotePadT107Grid ||
            remotePadGridState.IncludeGrassAtT88 ||
            remotePadGridState.MandatoryBaseFaceTextureId != PrivateMandatoryBaseFaceTextureId)
        {
            throw new InvalidOperationException(
                "Only a schema-3 T66 remote-pad state with T88 disabled can migrate to schema 4.");
        }
        return CreateRemotePadMobyChoice(
            remotePadGridState.IncludeStructuralTerrain,
            remotePadGridState.IncludeGrassAtT107
                ? Id65RemotePadMobyKind.GrassT107
                : Id65RemotePadMobyKind.None,
            remotePadGridState.T107RemotePadPositionId!);
    }

    /// <summary>
    /// Explicit one-way schema-4 to schema-5 migration. The exact schema-4
    /// terrain choice maps only to tile 1; tile 2 is never inferred.
    /// </summary>
    public static Id65AuthoringEditorControlState
        MigrateRemotePadMobyChoiceToTerrainSet(
            Id65AuthoringEditorControlState remotePadMobyChoiceState)
    {
        ArgumentNullException.ThrowIfNull(remotePadMobyChoiceState);
        if (!remotePadMobyChoiceState.UsesRemotePadMobyChoice ||
            remotePadMobyChoiceState.UsesRemotePadTerrainSet)
        {
            throw new InvalidOperationException(
                "Only exact schema-4 remote-pad Moby-choice state can migrate to schema 5.");
        }
        return CreateRemotePadMobyChoiceTerrainSet(
            remotePadMobyChoiceState.IncludeTerrainTile1,
            includeTerrainTile2: false,
            remotePadMobyChoiceState.RemotePadMobyKind!.Value,
            remotePadMobyChoiceState.RemotePadMobyPositionId!);
    }

    /// <summary>
    /// Explicit one-way schema-5 to schema-6 migration. All terrain and Moby
    /// choices are preserved exactly; the only added intent is the single
    /// code-owned blank-cyclorama preset.
    /// </summary>
    public static Id65AuthoringEditorControlState
        MigrateRemotePadMobyChoiceTerrainSetToBlankCyclorama(
            Id65AuthoringEditorControlState terrainSetState)
    {
        ArgumentNullException.ThrowIfNull(terrainSetState);
        if (!terrainSetState.UsesRemotePadTerrainSet ||
            terrainSetState.UsesBlankCyclorama)
        {
            throw new InvalidOperationException(
                "Only exact schema-5 terrain-set control state can migrate to schema 6.");
        }
        return CreateRemotePadMobyChoiceTerrainSetBlankCyclorama(
            terrainSetState.IncludeTerrainTile1,
            terrainSetState.IncludeTerrainTile2,
            terrainSetState.RemotePadMobyKind!.Value,
            terrainSetState.RemotePadMobyPositionId!);
    }

    internal static Id65AuthoringEditorControlState CopyExact(
        Id65AuthoringEditorControlState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.UsesBlankCyclorama
            ? CreateRemotePadMobyChoiceTerrainSetBlankCyclorama(
                state.IncludeTerrainTile1,
                state.IncludeTerrainTile2,
                state.RemotePadMobyKind!.Value,
                state.RemotePadMobyPositionId!)
            : state.UsesRemotePadTerrainSet
            ? CreateRemotePadMobyChoiceTerrainSet(
                state.IncludeTerrainTile1,
                state.IncludeTerrainTile2,
                state.RemotePadMobyKind!.Value,
                state.RemotePadMobyPositionId!)
            : state.UsesRemotePadMobyChoice
            ? CreateRemotePadMobyChoice(
                state.IncludeStructuralTerrain,
                state.RemotePadMobyKind!.Value,
                state.RemotePadMobyPositionId!)
            : state.UsesRemotePadT107Grid
            ? CreateRemotePad(
                state.IncludeStructuralTerrain,
                state.IncludeGrassAtT88,
                state.IncludeGrassAtT107,
                state.T107RemotePadPositionId!,
                state.MandatoryBaseFaceTextureId)
            : new(
                state.IncludeStructuralTerrain,
                state.IncludeGrassAtT88,
                state.IncludeGrassAtT107,
                state.MandatoryBaseFaceTextureId);
    }
}

/// <summary>
/// Hash-only, path-free result of translating one exact control state into the
/// canonical editor document. The internal document is retained only for later
/// Core-owned persistence; public callers receive no native input or payload.
/// </summary>
public sealed class Id65AuthoringEditorControlStateCompilation
{
    private readonly ReadOnlyCollection<int> _mobyTargetTrueIndices;

    internal Id65AuthoringEditorControlStateCompilation(
        Id65AuthoringEditorControlState state,
        Id65AuthoringEditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(document);
        Id65AuthoringEditorDocument expected =
            Id65AuthoringEditorControlStateCompiler.CompileExactDocument(state);
        Id65AuthoringEditorDocument canonical =
            Id65AuthoringEditorDocument.Parse(document.SerializeUtf8());
        Id65AuthoringEditorControlState recovered =
            Id65AuthoringEditorControlStateCompiler.RecoverExactState(canonical);
        if (state.IncludeStructuralTerrain != recovered.IncludeStructuralTerrain ||
            state.IncludeTerrainTile2 != recovered.IncludeTerrainTile2 ||
            state.IncludeGrassAtT88 != recovered.IncludeGrassAtT88 ||
            state.IncludeGrassAtT107 != recovered.IncludeGrassAtT107 ||
            state.MandatoryBaseFaceTextureId != recovered.MandatoryBaseFaceTextureId ||
            state.T107PlacementSemantics != recovered.T107PlacementSemantics ||
            state.TerrainSelectionSemantics != recovered.TerrainSelectionSemantics ||
            !string.Equals(
                state.T107RemotePadPositionId,
                recovered.T107RemotePadPositionId,
                StringComparison.Ordinal) ||
            state.RemotePadMobyKind != recovered.RemotePadMobyKind ||
            !string.Equals(
                state.RemotePadMobyPositionId,
                recovered.RemotePadMobyPositionId,
                StringComparison.Ordinal) ||
            state.UsesBlankCyclorama != recovered.UsesBlankCyclorama ||
            !string.Equals(
                state.BlankCycloramaPresetId,
                recovered.BlankCycloramaPresetId,
                StringComparison.Ordinal) ||
            !string.Equals(
                state.BlankCycloramaColorHex,
                recovered.BlankCycloramaColorHex,
                StringComparison.Ordinal) ||
            !canonical.SerializeUtf8().SequenceEqual(expected.SerializeUtf8()))
        {
            throw new InvalidDataException(
                "The ID65 compilation receipt requires one matching exact control-state/document pair.");
        }

        State = Id65AuthoringEditorControlState.CopyExact(state);
        Document = canonical;
        DocumentProfileId = canonical.ProfileId;
        DocumentContentSha256 = canonical.ContentSha256;
        DocumentCanonicalSha256 = canonical.CanonicalSha256;
        LockedBaseImageSha256 = canonical.LockedBaseImageSha256;
        LockedRow80Sha256 = canonical.LockedRow80Sha256;
        PrivateTextureWitnessSha256 = canonical.PrivateTextureWitnessSha256;
        MobyBundleSha256 = canonical.MobyBundleSha256;
        UsesBlankCyclorama = canonical.UsesBlankCyclorama;
        BlankCycloramaPresetId = canonical.BlankCyclorama?.PresetId ?? string.Empty;
        BlankCycloramaColorHex = canonical.BlankCyclorama?.ColorHex ?? string.Empty;
        TerrainIntentCount = canonical.Terrain.Count;
        PrivateTextureDestinationRecordIndex =
            canonical.PrivateTextureSwap.DestinationRecordIndex;
        _mobyTargetTrueIndices = Array.AsReadOnly(canonical.MobyPlacements
            .Select(placement => placement.TargetTrueIndex)
            .ToArray());
    }

    public Id65AuthoringEditorControlState State { get; }
    public string DocumentProfileId { get; }
    public string DocumentContentSha256 { get; }
    public string DocumentCanonicalSha256 { get; }
    public string LockedBaseImageSha256 { get; }
    public string LockedRow80Sha256 { get; }
    public string PrivateTextureWitnessSha256 { get; }
    public string MobyBundleSha256 { get; }
    public bool UsesBlankCyclorama { get; }
    public string BlankCycloramaPresetId { get; }
    public string BlankCycloramaColorHex { get; }
    public int TerrainIntentCount { get; }
    public int PrivateTextureDestinationRecordIndex { get; }
    public IReadOnlyList<int> MobyTargetTrueIndices => _mobyTargetTrueIndices;

    public bool ExactDocumentFactoryVerified => true;
    public bool CanonicalDocumentRoundTripVerified => true;
    public bool ControlStateRoundTripVerified => true;
    public bool ContainsFilePaths => false;
    public bool ContainsGameBytes => false;
    public bool ContainsNativePayloadBytes => false;
    public bool ContainsPhysicalAddresses => false;
    public bool ContainsCompiledAfterimage => false;
    public bool NativeInputsDerived => false;
    public bool CurrentProjectionRevalidated => false;
    public bool PersistencePerformed => false;
    public bool AggregateMaterializationEnabled => false;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool WritesCue => false;
    public bool CreateBinEnabled => false;
    public bool NormalCreateBinEnabled => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool RuntimeProven => false;
    public bool ReleaseIntegrated => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;

    internal Id65AuthoringEditorDocument Document { get; }
}

/// <summary>
/// Deterministic two-way bridge between framework-neutral editor controls and
/// the exact path-free authoring document. It performs no filesystem access,
/// native-input derivation, projection, publication, or runtime action.
/// </summary>
public static class Id65AuthoringEditorControlStateCompiler
{
    public static Id65AuthoringEditorControlStateCompilation Compile(
        Id65AuthoringEditorControlState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Id65AuthoringEditorDocument document = CompileExactDocument(state);
        Id65AuthoringEditorDocument reparsed =
            Id65AuthoringEditorDocument.Parse(document.SerializeUtf8());
        Id65AuthoringEditorControlState recovered = RecoverExactState(reparsed);
        RequireEquivalent(state, recovered);
        if (!document.SerializeUtf8().SequenceEqual(reparsed.SerializeUtf8()) ||
            document.CanonicalSha256 != reparsed.CanonicalSha256 ||
            document.ContentSha256 != reparsed.ContentSha256)
        {
            throw new InvalidDataException(
                "The ID65 control state did not produce one deterministic canonical document.");
        }

        Id65AuthoringEditorControlStateCompilation result = new(state, reparsed);
        ValidateCompilation(result);
        return result;
    }

    public static Id65AuthoringEditorControlState RecoverControlState(
        Id65AuthoringEditorDocumentReadModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        bool legacyPrivateT66 =
            document.SchemaVersion == Id65AuthoringEditorDocument.CurrentSchemaVersion &&
            document.ProfileId == Id65AuthoringEditorDocument.DocumentProfileId &&
            document.MandatoryBaseFaceTextureId ==
                Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId &&
            document.ReadModelSchemaVersion ==
                Id65AuthoringEditorDocumentReadModel.LegacyReadModelSchemaVersion &&
            document.T107PlacementSemantics ==
                Id65AuthoringEditorT107PlacementSemantics.LegacyNativeApronV1 &&
            !document.UsesRemotePadT107Grid &&
            document.T107RemotePadPositionId is null &&
            document.T107RemotePadPosition is null &&
            !document.UsesRemotePadMobyChoice &&
            document.RemotePadMobyKind is null &&
            document.RemotePadMobyPositionId is null &&
            document.RemotePadMobyPosition is null;
        bool legacyResidentT25 =
            document.SchemaVersion == Id65AuthoringEditorDocument.ResidentT25SchemaVersion &&
            document.ProfileId == Id65AuthoringEditorDocument.ResidentT25DocumentProfileId &&
            document.MandatoryBaseFaceTextureId ==
                Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId &&
            document.ReadModelSchemaVersion ==
                Id65AuthoringEditorDocumentReadModel.LegacyReadModelSchemaVersion &&
            document.T107PlacementSemantics ==
                Id65AuthoringEditorT107PlacementSemantics.LegacyNativeApronV1 &&
            !document.UsesRemotePadT107Grid &&
            document.T107RemotePadPositionId is null &&
            document.T107RemotePadPosition is null &&
            !document.UsesRemotePadMobyChoice &&
            document.RemotePadMobyKind is null &&
            document.RemotePadMobyPositionId is null &&
            document.RemotePadMobyPosition is null;
        bool remotePadGrid =
            document.SchemaVersion == Id65AuthoringEditorDocument.RemotePadGridSchemaVersion &&
            document.ProfileId == Id65AuthoringEditorDocument.RemotePadGridDocumentProfileId &&
            (document.MandatoryBaseFaceTextureId is
                Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId or
                Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId) &&
            document.ReadModelSchemaVersion ==
                Id65AuthoringEditorDocumentReadModel.RemotePadGridReadModelSchemaVersion &&
            document.T107PlacementSemantics ==
                Id65AuthoringEditorT107PlacementSemantics.RemotePadGridV1 &&
            document.UsesRemotePadT107Grid &&
            document.T107RemotePadPositionId is not null &&
            document.T107RemotePadPosition is not null &&
            !document.UsesRemotePadMobyChoice &&
            document.RemotePadMobyKind is null &&
            document.RemotePadMobyPositionId is null &&
            document.RemotePadMobyPosition is null;
        bool remotePadMobyChoice =
            document.SchemaVersion ==
                Id65AuthoringEditorDocument.RemotePadMobyChoiceSchemaVersion &&
            document.ProfileId ==
                Id65AuthoringEditorDocument.RemotePadMobyChoiceDocumentProfileId &&
            document.MandatoryBaseFaceTextureId ==
                Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId &&
            document.ReadModelSchemaVersion ==
                Id65AuthoringEditorDocumentReadModel.RemotePadMobyChoiceReadModelSchemaVersion &&
            document.T107PlacementSemantics ==
                Id65AuthoringEditorT107PlacementSemantics.RemotePadMobyChoiceV1 &&
            !document.UsesRemotePadT107Grid &&
            document.T107RemotePadPositionId is null &&
            document.T107RemotePadPosition is null &&
            document.UsesRemotePadMobyChoice &&
            document.RemotePadMobyKind.HasValue &&
            document.RemotePadMobyPositionId is not null &&
            document.RemotePadMobyPosition is not null &&
            !document.UsesRemotePadTerrainSet;
        bool remotePadMobyChoiceTerrainSet =
            document.SchemaVersion ==
                Id65AuthoringEditorDocument.RemotePadMobyChoiceTerrainSetSchemaVersion &&
            document.ProfileId ==
                Id65AuthoringEditorDocument.RemotePadMobyChoiceTerrainSetDocumentProfileId &&
            document.MandatoryBaseFaceTextureId ==
                Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId &&
            document.ReadModelSchemaVersion ==
                Id65AuthoringEditorDocumentReadModel
                    .RemotePadMobyChoiceTerrainSetReadModelSchemaVersion &&
            document.T107PlacementSemantics ==
                Id65AuthoringEditorT107PlacementSemantics.RemotePadMobyChoiceV1 &&
            document.TerrainSelectionSemantics ==
                Id65AuthoringEditorTerrainSelectionSemantics.RemotePadTerrainSetV1 &&
            !document.UsesRemotePadT107Grid &&
            document.T107RemotePadPositionId is null &&
            document.T107RemotePadPosition is null &&
            document.UsesRemotePadMobyChoice &&
            document.UsesRemotePadTerrainSet &&
            !document.UsesBlankCyclorama &&
            document.BlankCycloramaPresetId.Length == 0 &&
            document.BlankCycloramaColorHex.Length == 0 &&
            document.StaticAuthoringCycloramaCatalogSha256.Length == 0 &&
            document.RemotePadMobyKind.HasValue &&
            document.RemotePadMobyPositionId is not null &&
            document.RemotePadMobyPosition is not null;
        bool remotePadMobyChoiceTerrainSetBlankCyclorama =
            document.SchemaVersion == Id65AuthoringEditorDocument
                .RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion &&
            document.ProfileId == Id65AuthoringEditorDocument
                .RemotePadMobyChoiceTerrainSetBlankCycloramaDocumentProfileId &&
            document.MandatoryBaseFaceTextureId ==
                Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId &&
            document.ReadModelSchemaVersion == Id65AuthoringEditorDocumentReadModel
                .RemotePadMobyChoiceTerrainSetBlankCycloramaReadModelSchemaVersion &&
            document.T107PlacementSemantics ==
                Id65AuthoringEditorT107PlacementSemantics.RemotePadMobyChoiceV1 &&
            document.TerrainSelectionSemantics ==
                Id65AuthoringEditorTerrainSelectionSemantics.RemotePadTerrainSetV1 &&
            !document.UsesRemotePadT107Grid &&
            document.T107RemotePadPositionId is null &&
            document.T107RemotePadPosition is null &&
            document.UsesRemotePadMobyChoice &&
            document.UsesRemotePadTerrainSet &&
            document.UsesBlankCyclorama &&
            document.BlankCycloramaPresetId ==
                Id65AuthoringBlankCycloramaCatalog.BlankSlatePresetId &&
            document.BlankCycloramaColorHex ==
                Id65AuthoringBlankCycloramaCatalog.BlankSlateColorHex &&
            document.StaticAuthoringCycloramaCatalogSha256 ==
                Id65AuthoringBlankCycloramaCatalog.CanonicalSha256 &&
            document.RemotePadMobyKind.HasValue &&
            document.RemotePadMobyPositionId is not null &&
            document.RemotePadMobyPosition is not null;
        bool supportedProfile =
            legacyPrivateT66 || legacyResidentT25 || remotePadGrid || remotePadMobyChoice ||
            remotePadMobyChoiceTerrainSet ||
            remotePadMobyChoiceTerrainSetBlankCyclorama;
        if (!supportedProfile ||
            document.LabProfileId != UnusedLevel65BlankLevelLabProfileRegistry.ProfileId ||
            document.LabProfileVersion !=
                UnusedLevel65BlankLevelLabProfileRegistry.ProfileVersion ||
            document.WorkspaceKey != UnusedLevel65BlankLevelLabProfileRegistry.Key ||
            document.WorkspaceLayoutVersion !=
                UnusedLevel65BlankLevelLabProfileRegistry.WorkspaceLayoutVersion ||
            document.LockedBaseProfileId !=
                UnusedLevel65BlankLevelLabProfileRegistry.Profile.LockedBaseProfileId ||
            document.LockedBaseImageSha256 !=
                UnusedLevel65BlankLevelLabProfileRegistry.Profile.LockedBaseImageSha256 ||
            document.LockedRow80Sha256 !=
                Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 ||
            document.PrivateTextureWitnessSha256 !=
                Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256 ||
            document.MobyBundleSha256 !=
                Id65AuthoringMobyDependencyBundleCompiler.ExpectedArtisansGrassBundleSha256 ||
            !document.EnvelopeCanonicalIdentityVerified ||
            !document.DocumentCanonicalIdentityVerified ||
            !document.PersistedProjectionIdentityPresent ||
            document.CurrentProjectionRevalidated ||
            document.CurrentLockedSourceReprojected ||
            document.ContainsGameBytes || document.ContainsNativePayloadBytes ||
            document.ContainsPhysicalAddresses || document.ContainsFilePaths ||
            document.ContainsCompiledAfterimage || document.ContainsPublisherState ||
            document.FileSystemMutationPerformed || document.WritesFileSystem ||
            document.WritesDiscImage || document.WritesCue || document.CreateBinEnabled ||
            document.NormalCreateBinEnabled || document.RuntimeCandidateAuthorized ||
            document.ReleaseIntegrated || document.PromotionAuthorized || document.Publishable)
        {
            throw new InvalidDataException(
                "The ID65 read model cannot be translated into an exact control state.");
        }

        Id65AuthoringEditorControlState state =
            remotePadMobyChoiceTerrainSetBlankCyclorama
            ? SelectRemotePadMobyChoiceTerrainSetBlankCycloramaState(document)
            : remotePadMobyChoiceTerrainSet
            ? SelectRemotePadMobyChoiceTerrainSetState(document)
            : remotePadMobyChoice
            ? SelectRemotePadMobyChoiceState(document)
            : remotePadGrid
                ? SelectRemotePadState(document)
            : SelectLegacyState(
                document.Terrain.Count,
                document.MobyPlacements.Select(placement => placement.Id),
                document.MandatoryBaseFaceTextureId);
        Id65AuthoringEditorDocument expected = CompileExactDocument(state);
        if (document.DocumentCanonicalSha256 != expected.CanonicalSha256 ||
            document.DocumentContentSha256 != expected.ContentSha256)
        {
            throw new InvalidDataException(
                "The ID65 read model semantics do not match its exact canonical document identity.");
        }
        return state;
    }

    internal static Id65AuthoringEditorDocument CompileExactDocument(
        Id65AuthoringEditorControlState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        int placementCount =
            (state.IncludeGrassAtT88 ? 1 : 0) +
            (state.IncludeGrassAtT107 ? 1 : 0);
        if (placementCount > 2 ||
            (!state.UsesRemotePadT107Grid && !state.UsesRemotePadMobyChoice &&
                placementCount < 1) ||
            (state.UsesRemotePadMobyChoice &&
                (state.IncludeGrassAtT88 || placementCount > 1)))
        {
            throw new InvalidDataException(
                "The ID65 control state lost an exact choice or fail-closed boundary.");
        }

        List<string> placementIds = [];
        if (state.IncludeGrassAtT88)
            placementIds.Add(Id65AuthoringMobyDependencyBundleCompiler.T88PlacementId);
        if (state.IncludeGrassAtT107)
        {
            placementIds.Add(state.UsesRemotePadT107Grid
                ? Id65RemotePadGrassT107Catalog.PlacementId
                : Id65AuthoringMobyDependencyBundleCompiler.T107PlacementId);
        }
        string[] activeTerrainUnitIds =
        [
            .. state.IncludeTerrainTile1
                ? [Id65AuthoringTerrainSetCatalog.Tile1Id]
                : Array.Empty<string>(),
            .. state.IncludeTerrainTile2
                ? [Id65AuthoringTerrainSetCatalog.Tile2Id]
                : Array.Empty<string>()
        ];
        return state.UsesBlankCyclorama
            ? Id65AuthoringEditorDocument
                .CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
                    state.RemotePadMobyKind!.Value,
                    state.RemotePadMobyPositionId!,
                    activeTerrainUnitIds)
            : state.UsesRemotePadTerrainSet
            ? Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetExact(
                state.RemotePadMobyKind!.Value,
                state.RemotePadMobyPositionId!,
                activeTerrainUnitIds)
            : state.UsesRemotePadMobyChoice
            ? Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceExact(
                state.RemotePadMobyKind!.Value,
                state.RemotePadMobyPositionId!,
                state.IncludeStructuralTerrain)
            : state.UsesRemotePadT107Grid
            ? Id65AuthoringEditorDocument.CreateRemotePadGridExact(
                state.T107RemotePadPositionId!,
                state.IncludeStructuralTerrain,
                placementIds,
                state.MandatoryBaseFaceTextureId)
            : Id65AuthoringEditorDocument.CreateExact(
                state.IncludeStructuralTerrain,
                placementIds,
                state.MandatoryBaseFaceTextureId);
    }

    internal static Id65AuthoringEditorControlState RecoverExactState(
        Id65AuthoringEditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Id65AuthoringEditorDocument canonical =
            Id65AuthoringEditorDocument.Parse(document.SerializeUtf8());
        Id65AuthoringEditorControlState state = canonical.UsesBlankCyclorama
            ? SelectRemotePadMobyChoiceTerrainSetBlankCycloramaState(canonical)
            : canonical.UsesRemotePadTerrainSet
            ? SelectRemotePadMobyChoiceTerrainSetState(canonical)
            : canonical.UsesRemotePadMobyChoice
            ? SelectRemotePadMobyChoiceState(canonical)
            : canonical.UsesRemotePadT107Grid
                ? SelectRemotePadState(canonical)
            : SelectLegacyState(
                canonical.Terrain.Count,
                canonical.MobyPlacements.Select(placement => placement.Id),
                canonical.MandatoryBaseFaceTextureId);
        Id65AuthoringEditorDocument expected = CompileExactDocument(state);
        if (!canonical.SerializeUtf8().SequenceEqual(expected.SerializeUtf8()))
        {
            throw new InvalidDataException(
                "The ID65 document cannot be recovered as one exact control state.");
        }
        return state;
    }

    private static Id65AuthoringEditorControlState SelectLegacyState(
        int terrainIntentCount,
        IEnumerable<string> placementIds,
        int mandatoryBaseFaceTextureId)
    {
        if (terrainIntentCount is < 0 or > 1)
            throw new InvalidDataException("The ID65 control state supports zero or one structural terrain intent.");
        string[] ids = placementIds.ToArray();
        if (ids.Length is < 1 or > 2 ||
            ids.Any(string.IsNullOrWhiteSpace) ||
            ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
        {
            throw new InvalidDataException(
                "The ID65 control state requires one or two distinct proved Grass placements.");
        }
        bool t88 = ids.Contains(
            Id65AuthoringMobyDependencyBundleCompiler.T88PlacementId,
            StringComparer.Ordinal);
        bool t107 = ids.Contains(
            Id65AuthoringMobyDependencyBundleCompiler.T107PlacementId,
            StringComparer.Ordinal);
        if (ids.Any(id =>
                id != Id65AuthoringMobyDependencyBundleCompiler.T88PlacementId &&
                id != Id65AuthoringMobyDependencyBundleCompiler.T107PlacementId))
        {
            throw new InvalidDataException(
                "The ID65 control state contains an unsupported Grass placement.");
        }
        return new(terrainIntentCount == 1, t88, t107, mandatoryBaseFaceTextureId);
    }

    private static Id65AuthoringEditorControlState SelectRemotePadState(
        Id65AuthoringEditorDocument document)
    {
        if (!document.UsesRemotePadT107Grid || document.T107RemotePadPositionId is null)
            throw new InvalidDataException("The schema-3 ID65 document lost its retained T107 position.");
        if (document.Terrain.Count > 1)
            throw new InvalidDataException("The schema-3 ID65 control state supports zero or one structural terrain intent.");
        string[] ids = document.MobyPlacements.Select(item => item.Id).ToArray();
        if (ids.Length > 2 || ids.Any(string.IsNullOrWhiteSpace) ||
            ids.Distinct(StringComparer.Ordinal).Count() != ids.Length ||
            ids.Any(id => id != Id65AuthoringMobyDependencyBundleCompiler.T88PlacementId &&
                id != Id65RemotePadGrassT107Catalog.PlacementId))
        {
            throw new InvalidDataException(
                "The schema-3 ID65 control state contains an unsupported or duplicate Grass placement.");
        }
        return Id65AuthoringEditorControlState.CreateRemotePad(
            document.Terrain.Count == 1,
            ids.Contains(
                Id65AuthoringMobyDependencyBundleCompiler.T88PlacementId,
                StringComparer.Ordinal),
            ids.Contains(Id65RemotePadGrassT107Catalog.PlacementId, StringComparer.Ordinal),
            document.T107RemotePadPositionId,
            document.MandatoryBaseFaceTextureId);
    }

    private static Id65AuthoringEditorControlState SelectRemotePadMobyChoiceState(
        Id65AuthoringEditorDocument document)
    {
        if (!document.UsesRemotePadMobyChoice ||
            !document.RemotePadMobyKind.HasValue ||
            document.RemotePadMobyPositionId is null ||
            document.MandatoryBaseFaceTextureId !=
                Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId ||
            document.UsesRemotePadTerrainSet ||
            document.Terrain.Count > 1)
        {
            throw new InvalidDataException(
                "The schema-4 ID65 document lost its exact T66 Moby-choice semantics.");
        }
        string[] actualIds = document.MobyPlacements.Select(item => item.Id).ToArray();
        string[] expectedIds = document.RemotePadMobyKind.Value switch
        {
            Id65RemotePadMobyKind.None => [],
            Id65RemotePadMobyKind.GrassT107 => [Id65RemotePadGrassT107Catalog.PlacementId],
            Id65RemotePadMobyKind.RedGemT21 => [Id65RemotePadRedGemT21Catalog.PlacementId],
            _ => throw new InvalidDataException("The schema-4 ID65 Moby kind is unsupported.")
        };
        if (!actualIds.SequenceEqual(expectedIds))
        {
            throw new InvalidDataException(
                "The schema-4 ID65 document Moby placements do not match its exclusive choice.");
        }
        return Id65AuthoringEditorControlState.CreateRemotePadMobyChoice(
            document.Terrain.Count == 1,
            document.RemotePadMobyKind.Value,
            document.RemotePadMobyPositionId);
    }

    private static Id65AuthoringEditorControlState SelectRemotePadMobyChoiceState(
        Id65AuthoringEditorDocumentReadModel document)
    {
        if (!document.UsesRemotePadMobyChoice ||
            !document.RemotePadMobyKind.HasValue ||
            document.RemotePadMobyPositionId is null ||
            document.RemotePadMobyPosition is null ||
            document.MandatoryBaseFaceTextureId !=
                Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId ||
            document.UsesRemotePadTerrainSet ||
            document.Terrain.Count > 1)
        {
            throw new InvalidDataException(
                "The schema-4 ID65 read model lost its exact T66 Moby-choice semantics.");
        }
        Id65RemotePadMobyPositionReadModel actual = document.RemotePadMobyPosition;
        Id65RemotePadGrassT107Position expected = Id65RemotePadGrassT107Catalog
            .GetRequiredPosition(document.RemotePadMobyPositionId);
        string? expectedRowSha256 = Id65RemotePadMobyChoiceCatalog
            .GetSelectedExactRowSha256(document.RemotePadMobyKind.Value, expected.PositionId);
        if (actual.Ordinal != expected.Ordinal || actual.PositionId != expected.PositionId ||
            actual.Label != expected.Label || actual.EditorX != expected.EditorX ||
            actual.EditorY != expected.EditorY || actual.EditorZ != expected.EditorZ ||
            actual.RawX != expected.RawX || actual.RawY != expected.RawY ||
            actual.RawZ != expected.RawZ ||
            actual.SelectedExactRowSha256 != expectedRowSha256 ||
            actual.IsDefault != expected.IsDefault)
        {
            throw new InvalidDataException(
                "The schema-4 ID65 read-model position does not match the frozen shared catalog.");
        }
        string[] actualIds = document.MobyPlacements.Select(item => item.Id).ToArray();
        string[] expectedIds = document.RemotePadMobyKind.Value switch
        {
            Id65RemotePadMobyKind.None => [],
            Id65RemotePadMobyKind.GrassT107 => [Id65RemotePadGrassT107Catalog.PlacementId],
            Id65RemotePadMobyKind.RedGemT21 => [Id65RemotePadRedGemT21Catalog.PlacementId],
            _ => throw new InvalidDataException("The schema-4 ID65 Moby kind is unsupported.")
        };
        if (!actualIds.SequenceEqual(expectedIds))
        {
            throw new InvalidDataException(
                "The schema-4 ID65 read-model placements do not match its exclusive choice.");
        }
        return Id65AuthoringEditorControlState.CreateRemotePadMobyChoice(
            document.Terrain.Count == 1,
            document.RemotePadMobyKind.Value,
            expected.PositionId);
    }

    private static Id65AuthoringEditorControlState
        SelectRemotePadMobyChoiceTerrainSetState(
            Id65AuthoringEditorDocument document)
    {
        if (!document.UsesRemotePadTerrainSet ||
            document.UsesBlankCyclorama ||
            !document.UsesRemotePadMobyChoice ||
            !document.RemotePadMobyKind.HasValue ||
            document.RemotePadMobyPositionId is null ||
            document.MandatoryBaseFaceTextureId !=
                Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId)
        {
            throw new InvalidDataException(
                "The schema-5 ID65 document lost its exact terrain-set/Moby-choice semantics.");
        }
        Id65AuthoringTerrainSetManifest manifest =
            Id65AuthoringTerrainSetCatalog.Create(document.ActiveTerrainUnitIds);
        if (!manifest.ActiveUnits.Select(item => item.Id)
                .SequenceEqual(document.ActiveTerrainUnitIds) ||
            !string.Equals(
                manifest.CanonicalSha256,
                document.TerrainSetManifestSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The schema-5 ID65 document terrain-set manifest changed.");
        }
        return Id65AuthoringEditorControlState.CreateRemotePadMobyChoiceTerrainSet(
            document.ActiveTerrainUnitIds.Contains(
                Id65AuthoringTerrainSetCatalog.Tile1Id,
                StringComparer.Ordinal),
            document.ActiveTerrainUnitIds.Contains(
                Id65AuthoringTerrainSetCatalog.Tile2Id,
                StringComparer.Ordinal),
            document.RemotePadMobyKind.Value,
            document.RemotePadMobyPositionId);
    }

    private static Id65AuthoringEditorControlState
        SelectRemotePadMobyChoiceTerrainSetBlankCycloramaState(
            Id65AuthoringEditorDocument document)
    {
        if (!document.UsesBlankCyclorama ||
            !document.UsesRemotePadTerrainSet ||
            !document.UsesRemotePadMobyChoice ||
            !document.RemotePadMobyKind.HasValue ||
            document.RemotePadMobyPositionId is null ||
            document.MandatoryBaseFaceTextureId !=
                Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId ||
            document.BlankCyclorama is null)
        {
            throw new InvalidDataException(
                "The schema-6 ID65 document lost its exact blank-cyclorama/terrain-set/Moby-choice semantics.");
        }
        Id65AuthoringBlankCycloramaCatalog.RequireExact(document.BlankCyclorama);
        Id65AuthoringTerrainSetManifest manifest =
            Id65AuthoringTerrainSetCatalog.Create(document.ActiveTerrainUnitIds);
        if (!manifest.ActiveUnits.Select(item => item.Id)
                .SequenceEqual(document.ActiveTerrainUnitIds) ||
            !string.Equals(
                manifest.CanonicalSha256,
                document.TerrainSetManifestSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The schema-6 ID65 document terrain-set manifest changed.");
        }
        return Id65AuthoringEditorControlState
            .CreateRemotePadMobyChoiceTerrainSetBlankCyclorama(
                document.ActiveTerrainUnitIds.Contains(
                    Id65AuthoringTerrainSetCatalog.Tile1Id,
                    StringComparer.Ordinal),
                document.ActiveTerrainUnitIds.Contains(
                    Id65AuthoringTerrainSetCatalog.Tile2Id,
                    StringComparer.Ordinal),
                document.RemotePadMobyKind.Value,
                document.RemotePadMobyPositionId);
    }

    private static Id65AuthoringEditorControlState
        SelectRemotePadMobyChoiceTerrainSetState(
            Id65AuthoringEditorDocumentReadModel document)
    {
        if (!document.UsesRemotePadTerrainSet ||
            document.UsesBlankCyclorama ||
            document.ReadModelSchemaVersion !=
                Id65AuthoringEditorDocumentReadModel
                    .RemotePadMobyChoiceTerrainSetReadModelSchemaVersion ||
            !document.RemotePadMobyKind.HasValue ||
            document.RemotePadMobyPositionId is null ||
            document.RemotePadMobyPosition is null)
        {
            throw new InvalidDataException(
                "The schema-5 ID65 read model lost its exact terrain-set/Moby-choice semantics.");
        }
        Id65AuthoringTerrainSetManifest manifest =
            Id65AuthoringTerrainSetCatalog.Create(document.ActiveTerrainUnitIds);
        if (!manifest.ActiveUnits.Select(item => item.Id)
                .SequenceEqual(document.ActiveTerrainUnitIds) ||
            manifest.CanonicalSha256 != document.TerrainSetManifestSha256)
        {
            throw new InvalidDataException(
                "The schema-5 ID65 read-model terrain-set manifest changed.");
        }
        return Id65AuthoringEditorControlState.CreateRemotePadMobyChoiceTerrainSet(
            document.ActiveTerrainUnitIds.Contains(
                Id65AuthoringTerrainSetCatalog.Tile1Id,
                StringComparer.Ordinal),
            document.ActiveTerrainUnitIds.Contains(
                Id65AuthoringTerrainSetCatalog.Tile2Id,
                StringComparer.Ordinal),
            document.RemotePadMobyKind.Value,
            document.RemotePadMobyPositionId);
    }

    private static Id65AuthoringEditorControlState
        SelectRemotePadMobyChoiceTerrainSetBlankCycloramaState(
            Id65AuthoringEditorDocumentReadModel document)
    {
        if (!document.UsesBlankCyclorama ||
            !document.UsesRemotePadTerrainSet ||
            document.ReadModelSchemaVersion != Id65AuthoringEditorDocumentReadModel
                .RemotePadMobyChoiceTerrainSetBlankCycloramaReadModelSchemaVersion ||
            document.BlankCycloramaPresetId !=
                Id65AuthoringBlankCycloramaCatalog.BlankSlatePresetId ||
            document.BlankCycloramaColorHex !=
                Id65AuthoringBlankCycloramaCatalog.BlankSlateColorHex ||
            document.StaticAuthoringCycloramaCatalogSha256 !=
                Id65AuthoringBlankCycloramaCatalog.CanonicalSha256 ||
            !document.RemotePadMobyKind.HasValue ||
            document.RemotePadMobyPositionId is null ||
            document.RemotePadMobyPosition is null)
        {
            throw new InvalidDataException(
                "The schema-6 ID65 read model lost its exact blank-cyclorama/terrain-set/Moby-choice semantics.");
        }
        Id65AuthoringTerrainSetManifest manifest =
            Id65AuthoringTerrainSetCatalog.Create(document.ActiveTerrainUnitIds);
        if (!manifest.ActiveUnits.Select(item => item.Id)
                .SequenceEqual(document.ActiveTerrainUnitIds) ||
            manifest.CanonicalSha256 != document.TerrainSetManifestSha256)
        {
            throw new InvalidDataException(
                "The schema-6 ID65 read-model terrain-set manifest changed.");
        }
        return Id65AuthoringEditorControlState
            .CreateRemotePadMobyChoiceTerrainSetBlankCyclorama(
                document.ActiveTerrainUnitIds.Contains(
                    Id65AuthoringTerrainSetCatalog.Tile1Id,
                    StringComparer.Ordinal),
                document.ActiveTerrainUnitIds.Contains(
                    Id65AuthoringTerrainSetCatalog.Tile2Id,
                    StringComparer.Ordinal),
                document.RemotePadMobyKind.Value,
                document.RemotePadMobyPositionId);
    }

    private static Id65AuthoringEditorControlState SelectRemotePadState(
        Id65AuthoringEditorDocumentReadModel document)
    {
        if (!document.UsesRemotePadT107Grid ||
            document.T107RemotePadPositionId is null ||
            document.T107RemotePadPosition is null)
        {
            throw new InvalidDataException(
                "The schema-3 ID65 read model lost its retained T107 position.");
        }
        Id65RemotePadGrassT107Position expectedPosition =
            Id65RemotePadGrassT107Catalog.GetRequiredPosition(
                document.T107RemotePadPositionId);
        Id65RemotePadGrassT107PositionReadModel actualPosition =
            document.T107RemotePadPosition;
        if (actualPosition.Ordinal != expectedPosition.Ordinal ||
            actualPosition.PositionId != expectedPosition.PositionId ||
            actualPosition.Label != expectedPosition.Label ||
            actualPosition.EditorX != expectedPosition.EditorX ||
            actualPosition.EditorY != expectedPosition.EditorY ||
            actualPosition.EditorZ != expectedPosition.EditorZ ||
            actualPosition.RawX != expectedPosition.RawX ||
            actualPosition.RawY != expectedPosition.RawY ||
            actualPosition.RawZ != expectedPosition.RawZ ||
            actualPosition.RowSha256 != expectedPosition.RowSha256 ||
            actualPosition.IsDefault != expectedPosition.IsDefault)
        {
            throw new InvalidDataException(
                "The schema-3 ID65 read model remote-pad position does not match the frozen catalog.");
        }
        if (document.Terrain.Count > 1)
            throw new InvalidDataException("The schema-3 ID65 control state supports zero or one structural terrain intent.");
        string[] ids = document.MobyPlacements.Select(item => item.Id).ToArray();
        if (ids.Length > 2 || ids.Any(string.IsNullOrWhiteSpace) ||
            ids.Distinct(StringComparer.Ordinal).Count() != ids.Length ||
            ids.Any(id => id != Id65AuthoringMobyDependencyBundleCompiler.T88PlacementId &&
                id != Id65RemotePadGrassT107Catalog.PlacementId))
        {
            throw new InvalidDataException(
                "The schema-3 ID65 read model contains an unsupported or duplicate Grass placement.");
        }
        return Id65AuthoringEditorControlState.CreateRemotePad(
            document.Terrain.Count == 1,
            ids.Contains(
                Id65AuthoringMobyDependencyBundleCompiler.T88PlacementId,
                StringComparer.Ordinal),
            ids.Contains(Id65RemotePadGrassT107Catalog.PlacementId, StringComparer.Ordinal),
            expectedPosition.PositionId,
            document.MandatoryBaseFaceTextureId);
    }

    private static void RequireEquivalent(
        Id65AuthoringEditorControlState expected,
        Id65AuthoringEditorControlState actual)
    {
        if (expected.IncludeStructuralTerrain != actual.IncludeStructuralTerrain ||
            expected.IncludeTerrainTile2 != actual.IncludeTerrainTile2 ||
            expected.IncludeGrassAtT88 != actual.IncludeGrassAtT88 ||
            expected.IncludeGrassAtT107 != actual.IncludeGrassAtT107 ||
            expected.MandatoryBaseFaceTextureId != actual.MandatoryBaseFaceTextureId ||
            expected.T107PlacementSemantics != actual.T107PlacementSemantics ||
            expected.TerrainSelectionSemantics != actual.TerrainSelectionSemantics ||
            !string.Equals(
                expected.T107RemotePadPositionId,
                actual.T107RemotePadPositionId,
                StringComparison.Ordinal) ||
            expected.RemotePadMobyKind != actual.RemotePadMobyKind ||
            !string.Equals(
                expected.RemotePadMobyPositionId,
                actual.RemotePadMobyPositionId,
                StringComparison.Ordinal) ||
            expected.UsesBlankCyclorama != actual.UsesBlankCyclorama ||
            !string.Equals(
                expected.BlankCycloramaPresetId,
                actual.BlankCycloramaPresetId,
                StringComparison.Ordinal) ||
            !string.Equals(
                expected.BlankCycloramaColorHex,
                actual.BlankCycloramaColorHex,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The ID65 canonical document did not round-trip to its originating control state.");
        }
    }

    private static void ValidateCompilation(
        Id65AuthoringEditorControlStateCompilation result)
    {
        string expectedProfile = result.State.UsesBlankCyclorama
            ? Id65AuthoringEditorDocument
                .RemotePadMobyChoiceTerrainSetBlankCycloramaDocumentProfileId
            : result.State.UsesRemotePadTerrainSet
            ? Id65AuthoringEditorDocument.RemotePadMobyChoiceTerrainSetDocumentProfileId
            : result.State.UsesRemotePadMobyChoice
            ? Id65AuthoringEditorDocument.RemotePadMobyChoiceDocumentProfileId
            : result.State.UsesRemotePadT107Grid
                ? Id65AuthoringEditorDocument.RemotePadGridDocumentProfileId
            : result.State.MandatoryBaseFaceTextureId ==
                Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId
                ? Id65AuthoringEditorDocument.ResidentT25DocumentProfileId
                : Id65AuthoringEditorDocument.DocumentProfileId;
        if (result.DocumentProfileId != expectedProfile ||
            result.LockedBaseImageSha256 !=
                UnusedLevel65BlankLevelLabProfileRegistry.Profile.LockedBaseImageSha256 ||
            result.LockedRow80Sha256 !=
                Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 ||
            result.PrivateTextureWitnessSha256 !=
                Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256 ||
            result.MobyBundleSha256 !=
                Id65AuthoringMobyDependencyBundleCompiler.ExpectedArtisansGrassBundleSha256 ||
            result.UsesBlankCyclorama != result.State.UsesBlankCyclorama ||
            result.BlankCycloramaPresetId != result.State.BlankCycloramaPresetId ||
            result.BlankCycloramaColorHex != result.State.BlankCycloramaColorHex ||
            (result.UsesBlankCyclorama &&
                (result.BlankCycloramaPresetId !=
                    Id65AuthoringBlankCycloramaCatalog.BlankSlatePresetId ||
                 result.BlankCycloramaColorHex !=
                    Id65AuthoringBlankCycloramaCatalog.BlankSlateColorHex)) ||
            result.TerrainIntentCount !=
                ((result.State.IncludeTerrainTile1 ? 1 : 0) +
                 (result.State.IncludeTerrainTile2 ? 1 : 0)) ||
            result.PrivateTextureDestinationRecordIndex !=
                Id65V2NativeTextureCompositionCompiler.PrivateTextureId ||
            result.MobyTargetTrueIndices.Count !=
                ((result.State.IncludeGrassAtT88 ? 1 : 0) +
                 (result.State.IncludeGrassAtT107 ? 1 : 0) +
                 (result.State.IncludeRedGemAtT21 ? 1 : 0)) ||
            result.MobyTargetTrueIndices.SequenceEqual(
                result.MobyTargetTrueIndices.Order()) == false ||
            !result.ExactDocumentFactoryVerified ||
            !result.CanonicalDocumentRoundTripVerified ||
            !result.ControlStateRoundTripVerified ||
            result.ContainsFilePaths || result.ContainsGameBytes ||
            result.ContainsNativePayloadBytes || result.ContainsPhysicalAddresses ||
            result.ContainsCompiledAfterimage || result.NativeInputsDerived ||
            result.CurrentProjectionRevalidated || result.PersistencePerformed ||
            result.AggregateMaterializationEnabled ||
            result.WritesFileSystem || result.WritesDiscImage || result.WritesCue ||
            result.CreateBinEnabled || result.NormalCreateBinEnabled ||
            result.RuntimeCandidateAuthorized || result.RuntimeProven ||
            result.ReleaseIntegrated || result.PromotionAuthorized || result.Publishable)
        {
            throw new InvalidDataException(
                "The ID65 control-state compilation lost an exact identity or fail-closed boundary.");
        }
    }
}
