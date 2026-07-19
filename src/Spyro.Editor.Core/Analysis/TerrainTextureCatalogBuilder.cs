using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Analysis;

public enum TerrainTextureCatalogReadinessKind
{
    MetadataOnly,
    NativeArtReady,
    CustomArtReady,
    NativeRelocationBlocked,
    LegacyCustomArtBlocked
}

public enum TerrainTextureArtPath
{
    ResidentNativeTexture,
    CrossLevelNativeRelocation,
    CustomPngImport
}

public sealed record TerrainTextureArtReadiness(
    TerrainTextureArtPath Path,
    bool CanApply,
    string Note);

public sealed record TerrainTextureTargetRuntimeReadiness(
    bool CanPersist,
    string Note);

public enum TerrainTextureApplyScope
{
    SelectedFace,
    SharedTextureRecord
}

public enum TerrainTextureSurfacePropertyMode
{
    TransferDonorNativeSurface,
    PreserveTargetNativeSurface
}

public enum TerrainTextureRecordRole
{
    Unknown,
    GeometryOnly,
    FaceReferencedStatic,
    ControlledFaceDestination,
    ControlledDestinationWithoutFace,
    AnimationSourceDiagnostic,
    NativeUnreferencedStatic
}

public sealed record TerrainTextureAtomicApplyReadiness(
    TerrainTextureApplyScope Scope,
    int TargetTextureId,
    int AffectedFaceCount,
    TerrainTextureArtReadiness Art,
    TerrainTextureTargetRuntimeReadiness TargetRuntime,
    bool SurfacePropertiesReady,
    int SurfacePropertyFaceCount,
    int SurfacePropertyTriangleCount,
    string SurfacePropertyNote,
    TerrainTextureSurfacePropertyMode SurfacePropertyMode = TerrainTextureSurfacePropertyMode.TransferDonorNativeSurface)
{
    public bool HasCompleteSurfacePropertyCoverage =>
        SurfacePropertyMode == TerrainTextureSurfacePropertyMode.PreserveTargetNativeSurface ||
        (SurfacePropertiesReady && SurfacePropertyFaceCount == AffectedFaceCount);

    public bool CanApply =>
        AffectedFaceCount > 0 &&
        Art.CanApply &&
        TargetRuntime.CanPersist &&
        HasCompleteSurfacePropertyCoverage;

    public string Note => CanApply
        ? SurfacePropertyMode == TerrainTextureSurfacePropertyMode.PreserveTargetNativeSurface
            ? $"Art is ready and the target native surface/material state will be preserved for all {AffectedFaceCount} affected face(s)."
            : $"Art and native surface properties are ready for all {AffectedFaceCount} affected face(s)."
        : !Art.CanApply
            ? $"Art blocked: {Art.Note}"
            : !TargetRuntime.CanPersist
                ? $"Runtime target blocked: {TargetRuntime.Note}"
            : !HasCompleteSurfacePropertyCoverage
                ? $"Surface properties blocked: {SurfacePropertyNote}"
                : "The atomic terrain texture replacement is not ready.";
}

public sealed record TerrainTextureCatalogReadiness(
    TerrainTextureCatalogReadinessKind Kind,
    bool HasNativeTextureRecord,
    bool HasNormalDescriptors,
    bool HasCloseDescriptors,
    int NormalDescriptorCount,
    int CloseDescriptorCount,
    bool HasStagedCustomArt,
    TerrainTextureArtReadiness ResidentArt,
    TerrainTextureArtReadiness CrossLevelArt,
    TerrainTextureArtReadiness CustomArt,
    TerrainTextureTargetRuntimeReadiness TargetRuntime,
    TerrainTextureRecordRole RecordRole = TerrainTextureRecordRole.Unknown)
{
    // This compatibility property intentionally means a same-level resident-ID switch.
    // Cross-level relocation and custom PNG import have independent readiness gates.
    public bool CanBorrowArt => ResidentArt.CanApply;
    public bool SupportsBothDescriptorTiers => HasNormalDescriptors && HasCloseDescriptors;
    public bool IsNativeUnreferencedStatic => RecordRole == TerrainTextureRecordRole.NativeUnreferencedStatic;

    public string DescriptorTier => SupportsBothDescriptorTiers
        ? "both"
        : HasNormalDescriptors
            ? "hqData"
            : HasCloseDescriptors
                ? "hqDataClose"
                : "none";
}

public sealed record TerrainTextureCatalogLevelInput
{
    public required LevelDefinition Level { get; init; }
    public GeometryCandidate? Geometry { get; init; }
    public IReadOnlyList<TerrainTextureSlot> TextureSlots { get; init; } = Array.Empty<TerrainTextureSlot>();
    public IReadOnlyList<CustomTerrainTextureImport> CustomTextures { get; init; } = Array.Empty<CustomTerrainTextureImport>();
    public NativeTerrainTextureRuntimeControlAudit? RuntimeControlAudit { get; init; }
}

public sealed record TerrainTextureCatalogEntry(
    LevelDefinition Level,
    LevelRealmPosition? RealmPosition,
    int TextureId,
    int FaceCount,
    string Surface,
    string RepresentativeRuntimeKey,
    Vector3f RepresentativeCenter,
    ColorRgba PreviewColor,
    TerrainTextureCatalogReadiness Readiness,
    IReadOnlyList<CustomTerrainTextureImport> CustomTextures)
{
    public string LevelKey => Level.Key;
    public string LevelName => Level.DisplayName;
    public string RealmKey => RealmPosition?.Realm.Key ?? "unknown";
    public string RealmName => RealmPosition?.Realm.DisplayName ?? "Unknown";
    public int RealmOrder => RealmPosition?.Realm.Order ?? int.MaxValue;
    public int LevelSlot => RealmPosition?.LevelSlot ?? int.MaxValue;
}

public sealed record TerrainTextureCatalogLevelReadiness(
    LevelDefinition Level,
    LevelRealmPosition? RealmPosition,
    bool HasGeometry,
    int FaceCount,
    int TextureCount,
    int BorrowableTextureCount)
{
    public bool HasBorrowableTextures => BorrowableTextureCount > 0;
}

public sealed record TerrainTextureCatalog(
    IReadOnlyList<TerrainTextureCatalogLevelReadiness> Levels,
    IReadOnlyList<TerrainTextureCatalogEntry> Entries);

public static class TerrainTextureCatalogBuilder
{
    public static TerrainTextureCatalog Build(IEnumerable<TerrainTextureCatalogLevelInput> levelInputs)
    {
        ArgumentNullException.ThrowIfNull(levelInputs);

        List<TerrainTextureCatalogEntry> entries = new();
        List<TerrainTextureCatalogLevelReadiness> levels = new();
        HashSet<string> seenLevelKeys = new(StringComparer.OrdinalIgnoreCase);

        foreach (TerrainTextureCatalogLevelInput input in levelInputs)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(input.Level);
            string normalizedLevelKey = LevelCatalog.NormalizeKey(input.Level.Key);
            if (string.IsNullOrWhiteSpace(normalizedLevelKey))
                throw new ArgumentException("Terrain texture catalog levels need a non-empty level key.", nameof(levelInputs));
            if (!seenLevelKeys.Add(normalizedLevelKey))
                throw new ArgumentException($"Terrain texture catalog input contains duplicate level '{input.Level.Key}'.", nameof(levelInputs));
            if (input.RuntimeControlAudit != null &&
                input.RuntimeControlAudit.TargetWadEntry != input.Level.SourceWadEntry)
            {
                throw new ArgumentException(
                    $"Terrain texture catalog level '{input.Level.Key}' uses WAD entry {input.Level.SourceWadEntry}, but its runtime-control audit is bound to WAD entry {input.RuntimeControlAudit.TargetWadEntry}.",
                    nameof(levelInputs));
            }

            LevelRealmPosition? realmPosition = LevelRealmCatalog.TryGetPosition(input.Level, out LevelRealmPosition resolvedPosition)
                ? resolvedPosition
                : null;
            Dictionary<int, TerrainTextureSlot> slotsByTextureId = MergeSlots(input.TextureSlots);
            Dictionary<int, CustomTerrainTextureImport[]> customByTextureId = input.CustomTextures
                .Where(texture => texture.TextureId >= 0)
                .GroupBy(texture => texture.TextureId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderBy(texture => texture.DescriptorTier, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(texture => texture.SourceImageName, StringComparer.OrdinalIgnoreCase)
                        .ToArray());
            TerrainPolygon[] faces = (input.Geometry?.Polygons ?? [])
                .Where(face => face.OriginalTextureId >= 0)
                .ToArray();
            int firstEntryIndex = entries.Count;

            Dictionary<int, TerrainPolygon[]> facesByTextureId = faces
                .GroupBy(face => face.OriginalTextureId)
                .ToDictionary(group => group.Key, group => group.ToArray());
            int[] originalTextureIds = slotsByTextureId.Keys
                .Concat(facesByTextureId.Keys)
                .Distinct()
                .OrderBy(textureId => textureId)
                .ToArray();

            foreach (int textureId in originalTextureIds)
            {
                TerrainPolygon[] textureFaces = facesByTextureId.TryGetValue(textureId, out TerrainPolygon[]? groupedFaces)
                    ? groupedFaces
                    : [];
                TerrainPolygon? representative = textureFaces
                    .OrderByDescending(face => face.Points.Count)
                    .ThenBy(face => face.RuntimeKey, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                string surface = textureFaces.Length == 0
                    ? "unreferenced"
                    : textureFaces
                        .GroupBy(face => TerrainMaterialClassifier.NormalizeSurfaceName(face.Surface))
                        .OrderByDescending(group => group.Count())
                        .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.Key)
                        .FirstOrDefault("unknown");
                slotsByTextureId.TryGetValue(textureId, out TerrainTextureSlot? slot);
                CustomTerrainTextureImport[] customTextures = customByTextureId.TryGetValue(textureId, out CustomTerrainTextureImport[]? custom)
                    ? custom
                    : Array.Empty<CustomTerrainTextureImport>();
                TerrainTextureCatalogReadiness readiness = BuildReadiness(
                    slot,
                    textureId,
                    textureFaces.Length,
                    customTextures.Length > 0,
                    input.RuntimeControlAudit);

                entries.Add(new TerrainTextureCatalogEntry(
                    input.Level,
                    realmPosition,
                    textureId,
                    textureFaces.Length,
                    surface,
                    representative?.RuntimeKey ?? "",
                    representative == null
                        ? new Vector3f(0, 0, 0)
                        : new Vector3f(representative.Center.X, representative.Center.Y, representative.AvgZ),
                    representative?.SurfaceColor ?? ColorRgba.FromRgb(92, 96, 104),
                    readiness,
                    customTextures));
            }

            int textureCount = entries.Count - firstEntryIndex;
            int borrowableTextureCount = entries
                .Skip(firstEntryIndex)
                .Count(entry => entry.Readiness.CanBorrowArt);
            levels.Add(new TerrainTextureCatalogLevelReadiness(
                input.Level,
                realmPosition,
                input.Geometry != null,
                faces.Length,
                textureCount,
                borrowableTextureCount));
        }

        TerrainTextureCatalogEntry[] orderedEntries = entries
            .OrderBy(entry => entry.RealmPosition?.GlobalOrder ?? int.MaxValue)
            .ThenBy(entry => entry.Level.LevelId)
            .ThenBy(entry => entry.TextureId)
            .ThenBy(entry => entry.Level.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        TerrainTextureCatalogLevelReadiness[] orderedLevels = levels
            .OrderBy(level => level.RealmPosition?.GlobalOrder ?? int.MaxValue)
            .ThenBy(level => level.Level.LevelId)
            .ThenBy(level => level.Level.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new TerrainTextureCatalog(orderedLevels, orderedEntries);
    }

    private static Dictionary<int, TerrainTextureSlot> MergeSlots(IReadOnlyList<TerrainTextureSlot> slots)
    {
        return (slots ?? Array.Empty<TerrainTextureSlot>())
            .Where(slot => slot.TextureId >= 0)
            .GroupBy(slot => slot.TextureId)
            .ToDictionary(
                group => group.Key,
                group => new TerrainTextureSlot(
                    group.Key,
                    group.Any(slot => slot.HasNormalDescriptors),
                    group.Any(slot => slot.HasCloseDescriptors),
                    group.Max(slot => Math.Max(0, slot.NormalDescriptorCount)),
                    group.Max(slot => Math.Max(0, slot.CloseDescriptorCount)),
                    group.Select(slot => slot.NormalTopologySignature).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "",
                    group.Select(slot => slot.CloseTopologySignature).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "",
                    group.Select(slot => slot.CombinedTopologySignature).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? ""));
    }

    private static TerrainTextureCatalogReadiness BuildReadiness(
        TerrainTextureSlot? slot,
        int textureId,
        int originalFaceCount,
        bool hasCustomArt,
        NativeTerrainTextureRuntimeControlAudit? runtimeControlAudit)
    {
        // A positive descriptor count is diagnostic only.  Borrowability needs
        // the complete tier gate established by InspectTextureSlots; treating a
        // partial tier as ready can produce a half-painted or distorted LOD.
        bool hasNormal = slot?.HasNormalDescriptors == true;
        bool hasClose = slot?.HasCloseDescriptors == true;
        bool hasNativeRecord = slot != null;
        bool residentArtReady = hasNativeRecord && originalFaceCount > 0;
        TerrainTextureCatalogReadinessKind kind = !residentArtReady
            ? TerrainTextureCatalogReadinessKind.MetadataOnly
            : TerrainTextureCatalogReadinessKind.NativeArtReady;
        TerrainTextureArtReadiness residentArt = new(
            TerrainTextureArtPath.ResidentNativeTexture,
            residentArtReady,
            residentArtReady
                ? $"The retail level already renders this native texture record on {originalFaceCount} original face(s); a same-level texture-ID switch changes no texture-page bytes."
                : hasNativeRecord
                    ? "The native record exists, but no original face renders it, so its art is not runtime-proven. It remains selectable for inspection."
                    : "The level geometry references this texture ID, but the native texture record was not decoded from the selected source image.");
        bool crossLevelArtReady = hasNativeRecord && hasNormal && hasClose;
        TerrainTextureArtReadiness crossLevelArt = new(
            TerrainTextureArtPath.CrossLevelNativeRelocation,
            crossLevelArtReady,
            crossLevelArtReady
                ? "The native normal and close-detail tiers are complete. Apply/Create BIN revalidate the full 23-descriptor record, source-bound ownership closure, target runtime control, protected storage, and exact logical readback before saving or writing output."
                : "Cross-level native art needs complete normal and close-detail tiers before the full 23-descriptor record (2 low-detail, 1 leading, 4 normal, 16 close) can be proof-checked. The legacy raw transplant is not used.");
        TerrainTextureArtReadiness customArt = new(
            TerrainTextureArtPath.CustomPngImport,
            false,
            "Custom PNG terrain art is blocked because the legacy writer uses the obsolete 0x800-row and fixed 4-bpp close-detail model.");
        bool runtimePersistent = runtimeControlAudit?.IsRuntimePersistentTarget(textureId) == true;
        TerrainTextureRecordRole recordRole = BuildRecordRole(
            hasNativeRecord,
            originalFaceCount,
            textureId,
            runtimeControlAudit);
        TerrainTextureTargetRuntimeReadiness targetRuntime = new(
            runtimePersistent,
            runtimeControlAudit == null
                ? "No source-bound runtime animation/scroll control audit is available for this target texture record."
                : runtimeControlAudit.TargetReadinessNote(textureId));
        return new TerrainTextureCatalogReadiness(
            kind,
            hasNativeRecord,
            hasNormal,
            hasClose,
            Math.Max(0, slot?.NormalDescriptorCount ?? 0),
            Math.Max(0, slot?.CloseDescriptorCount ?? 0),
            hasCustomArt,
            residentArt,
            crossLevelArt,
            customArt,
            targetRuntime,
            recordRole);
    }

    private static TerrainTextureRecordRole BuildRecordRole(
        bool hasNativeRecord,
        int originalFaceCount,
        int textureId,
        NativeTerrainTextureRuntimeControlAudit? runtimeControlAudit)
    {
        if (!hasNativeRecord)
            return TerrainTextureRecordRole.GeometryOnly;
        if (runtimeControlAudit is not { Complete: true })
            return TerrainTextureRecordRole.Unknown;

        bool faceReferenced = originalFaceCount > 0;
        if (runtimeControlAudit.ControlledTextureIds.Contains(textureId))
        {
            return faceReferenced
                ? TerrainTextureRecordRole.ControlledFaceDestination
                : TerrainTextureRecordRole.ControlledDestinationWithoutFace;
        }
        if (runtimeControlAudit.AnimationSourceTextureIds.Contains(textureId))
            return TerrainTextureRecordRole.AnimationSourceDiagnostic;
        return faceReferenced
            ? TerrainTextureRecordRole.FaceReferencedStatic
            : TerrainTextureRecordRole.NativeUnreferencedStatic;
    }
}
