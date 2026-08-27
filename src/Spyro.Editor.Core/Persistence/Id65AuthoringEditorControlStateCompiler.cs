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
    RemotePadGridV1 = 2
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
            t107RemotePadPositionId: null)
    {
    }

    private Id65AuthoringEditorControlState(
        bool includeStructuralTerrain,
        bool includeGrassAtT88,
        bool includeGrassAtT107,
        int mandatoryBaseFaceTextureId,
        Id65AuthoringEditorT107PlacementSemantics t107PlacementSemantics,
        string? t107RemotePadPositionId)
    {
        if (mandatoryBaseFaceTextureId is not
            (ResidentMandatoryBaseFaceTextureId or PrivateMandatoryBaseFaceTextureId))
            throw new ArgumentOutOfRangeException(nameof(mandatoryBaseFaceTextureId));
        if (t107PlacementSemantics == Id65AuthoringEditorT107PlacementSemantics.LegacyNativeApronV1)
        {
            if (t107RemotePadPositionId is not null)
            {
                throw new ArgumentException(
                    "Legacy native-apron T107 state cannot carry a remote-pad position.",
                    nameof(t107RemotePadPositionId));
            }
        }
        else if (t107PlacementSemantics == Id65AuthoringEditorT107PlacementSemantics.RemotePadGridV1)
        {
            t107RemotePadPositionId = Id65RemotePadGrassT107Catalog
                .GetRequiredPosition(t107RemotePadPositionId!)
                .PositionId;
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(t107PlacementSemantics));
        }
        IncludeStructuralTerrain = includeStructuralTerrain;
        IncludeGrassAtT88 = includeGrassAtT88;
        IncludeGrassAtT107 = includeGrassAtT107;
        MandatoryBaseFaceTextureId = mandatoryBaseFaceTextureId;
        T107PlacementSemantics = t107PlacementSemantics;
        T107RemotePadPositionId = t107RemotePadPositionId;
    }

    public bool IncludeStructuralTerrain { get; }
    public bool IncludeGrassAtT88 { get; }
    public bool IncludeGrassAtT107 { get; }
    public int MandatoryBaseFaceTextureId { get; }
    public Id65AuthoringEditorT107PlacementSemantics T107PlacementSemantics { get; }
    public string? T107RemotePadPositionId { get; }
    public bool UsesRemotePadT107Grid =>
        T107PlacementSemantics == Id65AuthoringEditorT107PlacementSemantics.RemotePadGridV1;

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
            t107RemotePadPositionId);

    /// <summary>
    /// Explicit one-way migration. Calling this method is the only API that may
    /// choose Center for legacy state; ordinary construction/recovery never does.
    /// </summary>
    public static Id65AuthoringEditorControlState MigrateLegacyToRemotePadGrid(
        Id65AuthoringEditorControlState legacyState,
        string? t107RemotePadPositionId = null)
    {
        ArgumentNullException.ThrowIfNull(legacyState);
        if (legacyState.UsesRemotePadT107Grid)
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

    internal static Id65AuthoringEditorControlState CopyExact(
        Id65AuthoringEditorControlState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.UsesRemotePadT107Grid
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
            state.IncludeGrassAtT88 != recovered.IncludeGrassAtT88 ||
            state.IncludeGrassAtT107 != recovered.IncludeGrassAtT107 ||
            state.MandatoryBaseFaceTextureId != recovered.MandatoryBaseFaceTextureId ||
            state.T107PlacementSemantics != recovered.T107PlacementSemantics ||
            !string.Equals(
                state.T107RemotePadPositionId,
                recovered.T107RemotePadPositionId,
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
            document.T107RemotePadPosition is null;
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
            document.T107RemotePadPosition is null;
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
            document.T107RemotePadPosition is not null;
        bool supportedProfile = legacyPrivateT66 || legacyResidentT25 || remotePadGrid;
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

        Id65AuthoringEditorControlState state = remotePadGrid
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
        if (placementCount > 2 || (!state.UsesRemotePadT107Grid && placementCount < 1))
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
        return state.UsesRemotePadT107Grid
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
        Id65AuthoringEditorControlState state = canonical.UsesRemotePadT107Grid
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
            expected.IncludeGrassAtT88 != actual.IncludeGrassAtT88 ||
            expected.IncludeGrassAtT107 != actual.IncludeGrassAtT107 ||
            expected.MandatoryBaseFaceTextureId != actual.MandatoryBaseFaceTextureId ||
            expected.T107PlacementSemantics != actual.T107PlacementSemantics ||
            !string.Equals(
                expected.T107RemotePadPositionId,
                actual.T107RemotePadPositionId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The ID65 canonical document did not round-trip to its originating control state.");
        }
    }

    private static void ValidateCompilation(
        Id65AuthoringEditorControlStateCompilation result)
    {
        string expectedProfile = result.State.UsesRemotePadT107Grid
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
            result.TerrainIntentCount != (result.State.IncludeStructuralTerrain ? 1 : 0) ||
            result.PrivateTextureDestinationRecordIndex !=
                Id65V2NativeTextureCompositionCompiler.PrivateTextureId ||
            result.MobyTargetTrueIndices.Count !=
                ((result.State.IncludeGrassAtT88 ? 1 : 0) +
                 (result.State.IncludeGrassAtT107 ? 1 : 0)) ||
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
