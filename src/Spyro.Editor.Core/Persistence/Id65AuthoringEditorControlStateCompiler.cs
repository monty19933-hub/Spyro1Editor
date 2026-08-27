using System.Collections.ObjectModel;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Persistence;

/// <summary>
/// Framework-neutral control state for the exact first ID65 authoring document.
/// It models only choices already proved by the canonical document/compiler
/// chain. Private T66 and baseline spawn/music/totals/exit/save ownership remain
/// fixed and cannot be disabled or replaced through this surface.
/// </summary>
public sealed class Id65AuthoringEditorControlState
{
    public const string ProfileId =
        "id65-authoring-editor-control-state-exact-document-v1";
    public const int SchemaVersion = 1;
    public const int T88TargetTrueIndex = 88;
    public const int T107TargetTrueIndex = 107;
    public const int ResidentMandatoryBaseFaceTextureId = 25;
    public const int PrivateMandatoryBaseFaceTextureId = 66;

    public Id65AuthoringEditorControlState(
        bool includeStructuralTerrain,
        bool includeGrassAtT88,
        bool includeGrassAtT107,
        int mandatoryBaseFaceTextureId = PrivateMandatoryBaseFaceTextureId)
    {
        if (mandatoryBaseFaceTextureId is not
            (ResidentMandatoryBaseFaceTextureId or PrivateMandatoryBaseFaceTextureId))
            throw new ArgumentOutOfRangeException(nameof(mandatoryBaseFaceTextureId));
        IncludeStructuralTerrain = includeStructuralTerrain;
        IncludeGrassAtT88 = includeGrassAtT88;
        IncludeGrassAtT107 = includeGrassAtT107;
        MandatoryBaseFaceTextureId = mandatoryBaseFaceTextureId;
    }

    public bool IncludeStructuralTerrain { get; }
    public bool IncludeGrassAtT88 { get; }
    public bool IncludeGrassAtT107 { get; }
    public int MandatoryBaseFaceTextureId { get; }

    public static Id65AuthoringEditorControlState CreateDefault() =>
        new(
            includeStructuralTerrain: true,
            includeGrassAtT88: true,
            includeGrassAtT107: true,
            mandatoryBaseFaceTextureId: PrivateMandatoryBaseFaceTextureId);
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
            !canonical.SerializeUtf8().SequenceEqual(expected.SerializeUtf8()))
        {
            throw new InvalidDataException(
                "The ID65 compilation receipt requires one matching exact control-state/document pair.");
        }

        State = new(
            state.IncludeStructuralTerrain,
            state.IncludeGrassAtT88,
            state.IncludeGrassAtT107,
            state.MandatoryBaseFaceTextureId);
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
        bool supportedProfile =
            (document.SchemaVersion == Id65AuthoringEditorDocument.CurrentSchemaVersion &&
             document.ProfileId == Id65AuthoringEditorDocument.DocumentProfileId &&
             document.MandatoryBaseFaceTextureId == Id65AuthoringEditorControlState.PrivateMandatoryBaseFaceTextureId) ||
            (document.SchemaVersion == Id65AuthoringEditorDocument.ResidentT25SchemaVersion &&
             document.ProfileId == Id65AuthoringEditorDocument.ResidentT25DocumentProfileId &&
             document.MandatoryBaseFaceTextureId == Id65AuthoringEditorControlState.ResidentMandatoryBaseFaceTextureId);
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

        Id65AuthoringEditorControlState state = SelectState(
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
        if (placementCount is < 1 or > 2)
        {
            throw new InvalidDataException(
                "The ID65 control state lost an exact choice or fail-closed boundary.");
        }

        List<string> placementIds = [];
        if (state.IncludeGrassAtT88)
            placementIds.Add(Id65AuthoringMobyDependencyBundleCompiler.T88PlacementId);
        if (state.IncludeGrassAtT107)
            placementIds.Add(Id65AuthoringMobyDependencyBundleCompiler.T107PlacementId);
        return Id65AuthoringEditorDocument.CreateExact(
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
        Id65AuthoringEditorControlState state = SelectState(
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

    private static Id65AuthoringEditorControlState SelectState(
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

    private static void RequireEquivalent(
        Id65AuthoringEditorControlState expected,
        Id65AuthoringEditorControlState actual)
    {
        if (expected.IncludeStructuralTerrain != actual.IncludeStructuralTerrain ||
            expected.IncludeGrassAtT88 != actual.IncludeGrassAtT88 ||
            expected.IncludeGrassAtT107 != actual.IncludeGrassAtT107 ||
            expected.MandatoryBaseFaceTextureId != actual.MandatoryBaseFaceTextureId)
        {
            throw new InvalidDataException(
                "The ID65 canonical document did not round-trip to its originating control state.");
        }
    }

    private static void ValidateCompilation(
        Id65AuthoringEditorControlStateCompilation result)
    {
        string expectedProfile = result.State.MandatoryBaseFaceTextureId ==
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
