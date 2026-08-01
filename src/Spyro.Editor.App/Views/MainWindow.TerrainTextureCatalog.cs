using System.Globalization;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private const string LegacyCustomTerrainTextureBlock =
        "Custom terrain PNG painting is temporarily blocked: the legacy writer uses the obsolete 0x800-row and fixed 4-bpp close-detail model. No face, manifest, or BIN/CUE output was changed.";
    private const string CrossLevelTerrainTextureOwnershipBlock =
        "Cross-level native terrain art could not pass the source-bound ownership, runtime-control, complete-record, protected-storage, and exact-readback proof. The old raw/PNG transplant path remains disabled, so no face, manifest, or BIN/CUE output was changed.";

    private readonly Dictionary<string, NativeTerrainSurfaceSourceData> _nativeTerrainSurfaceSources =
        new(StringComparer.OrdinalIgnoreCase);
    private string _nativeTerrainSurfaceSourceImage = "";
    private long _nativeTerrainSurfaceSourceLength = -1;
    private DateTime _nativeTerrainSurfaceSourceWriteUtc = DateTime.MinValue;
    private readonly Dictionary<string, NativeTerrainTextureRuntimeControlAudit> _nativeTerrainRuntimeControlAudits =
        new(StringComparer.OrdinalIgnoreCase);
    private string _nativeTerrainRuntimeControlSourceImage = "";
    private long _nativeTerrainRuntimeControlSourceLength = -1;
    private DateTime _nativeTerrainRuntimeControlSourceWriteUtc = DateTime.MinValue;
    private readonly Dictionary<string, NativeTerrainDonorGeometryCacheEntry> _nativeTerrainDonorGeometryCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NativeTerrainTextureCatalogCacheEntry> _nativeTerrainTextureCatalogCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NativeTerrainVisualDonorCacheEntry> _nativeTerrainVisualDonorCache =
        new(StringComparer.OrdinalIgnoreCase);
    private string _nativeTerrainVisualDonorSourceImage = "";
    private long _nativeTerrainVisualDonorSourceLength = -1;
    private DateTime _nativeTerrainVisualDonorSourceWriteUtc = DateTime.MinValue;

    private GeometryCandidate? TryLoadNativeTerrainDonorGeometry(
        LevelDefinition level,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(level);
        error = "";
        string? overlayPath = FindCachedTerrainOverlayPath(level.Key);
        if (string.IsNullOrWhiteSpace(overlayPath) || !File.Exists(overlayPath))
        {
            error = $"No untouched cached terrain overlay is available for {level.DisplayName}.";
            return null;
        }

        try
        {
            GeometryCacheHealthIssue? healthIssue = GeometryCacheHealth.InspectOverlay(level.Key, overlayPath);
            if (healthIssue?.BlocksLoading == true)
            {
                error = healthIssue.Message;
                return null;
            }

            FileInfo overlayInfo = new(overlayPath);
            string levelKey = LevelCatalog.NormalizeKey(level.Key);
            if (_nativeTerrainDonorGeometryCache.TryGetValue(
                    levelKey,
                    out NativeTerrainDonorGeometryCacheEntry? cached) &&
                string.Equals(
                    cached.OverlayPath,
                    overlayPath,
                    StringComparison.OrdinalIgnoreCase) &&
                cached.OverlayLength == overlayInfo.Length &&
                cached.OverlayWriteUtc == overlayInfo.LastWriteTimeUtc)
            {
                return cached.Geometry;
            }

            // Deliberately load only the immutable overlay. Donor choices must not inherit the
            // current project's saved terrain edits, material overrides, or custom texture art.
            GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(overlayPath);
            _nativeTerrainDonorGeometryCache[levelKey] =
                new NativeTerrainDonorGeometryCacheEntry(
                    overlayPath,
                    overlayInfo.Length,
                    overlayInfo.LastWriteTimeUtc,
                    geometry);
            return geometry;
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or System.Text.Json.JsonException)
        {
            error = ex.Message;
            return null;
        }
    }

    private static IReadOnlyList<NativeTerrainDonorVariant> BuildNativeTerrainDonorVariants(
        int textureId,
        IReadOnlyList<TerrainPolygon> originalFaces,
        NativeTerrainSurfaceLevelCatalog? catalog)
    {
        List<NativeTerrainDonorVariant> result = new();
        HashSet<string> claimedRuntimeKeys = new(StringComparer.OrdinalIgnoreCase);
        NativeTerrainTextureSurfaceVariant[] safeVariants = catalog?.TextureVariants
            .Where(variant => variant.TextureId == textureId)
            .ToArray() ?? Array.Empty<NativeTerrainTextureSurfaceVariant>();

        foreach (NativeTerrainTextureSurfaceVariant safeVariant in safeVariants)
        {
            HashSet<string> safeRuntimeKeys = safeVariant.RuntimeKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            TerrainPolygon[] safeFaces = originalFaces
                .Where(face => safeRuntimeKeys.Contains(face.RuntimeKey))
                .ToArray();
            if (safeFaces.Length == 0)
                continue;

            foreach (TerrainPolygon face in safeFaces)
                claimedRuntimeKeys.Add(face.RuntimeKey);
            result.Add(new NativeTerrainDonorVariant(
                safeVariant.Signature,
                safeFaces,
                safeVariant.RepresentativeRuntimeKey,
                safeVariant.Signature.Label,
                "Exact native collision-property variant."));
        }

        TerrainPolygon[] residualFaces = originalFaces
            .Where(face => !claimedRuntimeKeys.Contains(face.RuntimeKey))
            .ToArray();
        if (residualFaces.Length > 0)
        {
            TerrainPolygon representative = residualFaces
                .OrderByDescending(face => face.Points.Count)
                .ThenBy(face => face.RuntimeKey, StringComparer.OrdinalIgnoreCase)
                .First();
            string reason = BuildNativeTerrainResidualNote(residualFaces, catalog);
            result.Add(new NativeTerrainDonorVariant(
                null,
                residualFaces,
                representative.RuntimeKey,
                "mixed or unmapped native property",
                reason));
        }

        return result;
    }

    private static string BuildNativeTerrainResidualNote(
        IReadOnlyList<TerrainPolygon> residualFaces,
        NativeTerrainSurfaceLevelCatalog? catalog)
    {
        if (catalog == null)
        {
            return $"Blocked residual: native collision-property data is unavailable for these {residualFaces.Count} original face(s).";
        }

        string[] notes = residualFaces
            .Select(face => catalog.FindFace(face.RuntimeKey)?.ReadinessNote ?? "No native face binding was decoded.")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();
        string detail = notes.Length == 0 ? "No exact uniform native property was decoded." : string.Join(" ", notes);
        return $"Blocked residual: {residualFaces.Count} original face(s) are not part of an exact, uniform, or visual-only native property variant. {detail}";
    }

    private static string InferNativeTerrainDonorSurface(
        IReadOnlyList<TerrainPolygon> faces,
        NativeTerrainSurfaceSignature? signature)
    {
        if (signature is { SurfaceType: 0 })
        {
            return signature.Param2 switch
            {
                0 => "water",
                1 => "lava",
                2 => "ooze",
                _ => "ground"
            };
        }

        if (faces.Count == 0)
            return "unknown";

        int r = 0;
        int g = 0;
        int b = 0;
        foreach (TerrainPolygon face in faces)
        {
            r += face.FaceColor.R;
            g += face.FaceColor.G;
            b += face.FaceColor.B;
        }

        r /= faces.Count;
        g /= faces.Count;
        b /= faces.Count;
        int max = Math.Max(r, Math.Max(g, b));
        int min = Math.Min(r, Math.Min(g, b));
        if (b >= r + 32 && b >= g + 24)
            return "water";
        if (r >= 172 && r >= g + 40 && g >= b + 20 && b <= 105)
            return "lava";
        if (g >= r + 16 && g >= b + 16)
            return "grass";
        if (r >= 168 && g >= 145 && b <= 126 && r >= b + 34)
            return "sand";
        if (max - min <= 24 || max < 115)
            return "stone";
        if (r >= g && g >= b && r >= b + 22)
            return "stone";
        return "ground";
    }

    private bool TrySelectNativeTerrainVisualDonor(
        string sourceImagePath,
        LevelDefinition level,
        int textureId,
        IReadOnlyList<TerrainPolygon> sourceFaces,
        out TerrainPolygon representative,
        out TerrainTextureVisualEdit visual,
        out string note)
    {
        representative = null!;
        visual = null!;
        FileInfo sourceInfo = new(sourceImagePath);
        if (!string.Equals(
                _nativeTerrainVisualDonorSourceImage,
                sourceImagePath,
                StringComparison.OrdinalIgnoreCase) ||
            _nativeTerrainVisualDonorSourceLength != sourceInfo.Length ||
            _nativeTerrainVisualDonorSourceWriteUtc != sourceInfo.LastWriteTimeUtc)
        {
            _nativeTerrainVisualDonorCache.Clear();
            _nativeTerrainVisualDonorSourceImage = sourceImagePath;
            _nativeTerrainVisualDonorSourceLength = sourceInfo.Length;
            _nativeTerrainVisualDonorSourceWriteUtc = sourceInfo.LastWriteTimeUtc;
        }

        string cacheKey =
            $"{LevelCatalog.NormalizeKey(level.Key)}:{textureId.ToString(CultureInfo.InvariantCulture)}";
        if (_nativeTerrainVisualDonorCache.TryGetValue(
                cacheKey,
                out NativeTerrainVisualDonorCacheEntry? cached))
        {
            TerrainPolygon? cachedRepresentative = sourceFaces.FirstOrDefault(face =>
                string.Equals(
                    face.RuntimeKey,
                    cached.RepresentativeRuntimeKey,
                    StringComparison.OrdinalIgnoreCase));
            if (cachedRepresentative != null)
            {
                representative = cachedRepresentative;
                visual = cached.Visual;
                note = cached.Note;
                return cached.Success;
            }

            _nativeTerrainVisualDonorCache.Remove(cacheKey);
        }

        string lastError = "No high-detail source face was available.";
        TerrainPolygon[] ranked = sourceFaces
            .Where(face =>
                face.OriginalTextureId == textureId &&
                string.Equals(face.Detail, "hp", StringComparison.OrdinalIgnoreCase))
            .OrderBy(NativeTerrainVisualDonorRank)
            .ThenBy(face => face.SectorIndex)
            .ThenBy(face => face.FaceIndex)
            .ToArray();
        foreach (TerrainPolygon face in ranked)
        {
            if (!NativeTerrainTextureVisualInspector.TryInspectSourceImage(
                    sourceImagePath,
                    level.Key,
                    face,
                    out TerrainTextureVisualEdit inspected,
                    out lastError))
            {
                continue;
            }
            if (inspected.SourceTextureId != textureId)
            {
                lastError = $"Source face {face.RuntimeKey} decoded texture {inspected.SourceTextureId}, expected {textureId}.";
                continue;
            }

            representative = face;
            visual = inspected with
            {
                Label = $"native resident texture {textureId} with source-bound near/fade tint"
            };
            note = visual.UniqueCornerPairCount == 1
                ? $"Source face {face.RuntimeKey} supplies one uniform native near/fade tint pair."
                : $"Source face {face.RuntimeKey} supplies {visual.UniqueCornerPairCount} native near/fade corner tint pairs.";
            _nativeTerrainVisualDonorCache[cacheKey] =
                new NativeTerrainVisualDonorCacheEntry(
                    true,
                    face.RuntimeKey,
                    visual,
                    note);
            return true;
        }

        note = $"No source-verifiable visual donor was found for texture {textureId}. {lastError}";
        _nativeTerrainVisualDonorCache[cacheKey] =
            new NativeTerrainVisualDonorCacheEntry(
                false,
                ranked.FirstOrDefault()?.RuntimeKey ?? "",
                null!,
                note);
        return false;
    }

    private static int NativeTerrainVisualDonorRank(TerrainPolygon face)
    {
        bool quad = face.Points.Count == 4;
        bool flat = Math.Abs(face.MaxZ - face.MinZ) <= 0.01f;
        bool uniformTint = face.ColourIndexes.Count >= 4 &&
            face.ColourIndexes.Take(4).Distinct().Count() == 1;
        if (quad && flat && uniformTint)
            return 0;
        if (quad && flat)
            return 1;
        if (quad && uniformTint)
            return 2;
        if (quad)
            return 3;
        if (flat && uniformTint)
            return 4;
        return 5;
    }

    private NativeTerrainSurfaceLevelCatalog? TryBuildNativeTerrainSurfaceCatalog(
        LevelDefinition level,
        GeometryCandidate geometry,
        out string error)
    {
        error = "";
        string sourceImage = FirstExistingDiscImagePath(
            _discImagePathBox.Text,
            _skyboxDiscImagePathBox.Text,
            DiscImageLocator.FindImage(_workspace));
        if (string.IsNullOrWhiteSpace(sourceImage) || !File.Exists(sourceImage))
        {
            error = "Choose the Spyro BIN/CUE to decode native terrain behavior.";
            return null;
        }

        try
        {
            FileInfo info = new(sourceImage);
            if (!string.Equals(_nativeTerrainSurfaceSourceImage, sourceImage, StringComparison.OrdinalIgnoreCase) ||
                _nativeTerrainSurfaceSourceLength != info.Length ||
                _nativeTerrainSurfaceSourceWriteUtc != info.LastWriteTimeUtc)
            {
                _nativeTerrainSurfaceSources.Clear();
                _nativeTerrainSurfaceSourceImage = sourceImage;
                _nativeTerrainSurfaceSourceLength = info.Length;
                _nativeTerrainSurfaceSourceWriteUtc = info.LastWriteTimeUtc;
            }

            string levelKey = LevelCatalog.NormalizeKey(level.Key);
            if (!_nativeTerrainSurfaceSources.TryGetValue(levelKey, out NativeTerrainSurfaceSourceData? source))
            {
                source = PortalSourceDataLocator.LocateTerrainSurfaces(sourceImage, level);
                _nativeTerrainSurfaceSources[levelKey] = source;
            }

            return NativeTerrainSurfaceCatalogBuilder.Build(level, geometry, source);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException)
        {
            error = ex.Message;
            return null;
        }
    }

    private NativeTerrainSurfaceTransferReadiness EvaluateNativeTerrainBehaviorTransfer(
        NativeTerrainSurfaceLevelCatalog? targetCatalog,
        NativeTerrainSurfaceSignature? signature,
        string sourceLevelKey)
    {
        if (_selectedTerrain == null)
            return new NativeTerrainSurfaceTransferReadiness(false, -1, 0, 0, "Select a target terrain face first.");
        if (signature == null)
            return new NativeTerrainSurfaceTransferReadiness(false, -1, 0, 0, "This look has no exact native collision-property proof.");
        if (targetCatalog == null)
            return new NativeTerrainSurfaceTransferReadiness(false, -1, 0, 0, "The target level's native collision-property table is unavailable.");

        bool crossLevel = _currentLevel != null &&
            !string.Equals(
                LevelCatalog.NormalizeKey(sourceLevelKey),
                LevelCatalog.NormalizeKey(_currentLevel.Key),
                StringComparison.OrdinalIgnoreCase);
        return targetCatalog.EvaluateTransfer(_selectedTerrain.RuntimeKey, signature, crossLevel);
    }

    private NativeTerrainSurfaceBatchTransferReadiness EvaluateNativeTerrainBehaviorBatchTransfer(
        NativeTerrainSurfaceLevelCatalog? targetCatalog,
        NativeTerrainSurfaceSignature? signature,
        string sourceLevelKey,
        int targetTextureId)
    {
        string[] targetRuntimeKeys = _currentGeometry?.Polygons
            .Where(face => !face.IsTerrainRemoved && face.TextureId == targetTextureId)
            .Select(face => face.RuntimeKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
        if (signature == null)
        {
            return new NativeTerrainSurfaceBatchTransferReadiness(
                false, -1, targetRuntimeKeys.Length, 0, 0, 0, 0, [], targetRuntimeKeys,
                "This look has no exact native collision-property proof.");
        }
        if (targetCatalog == null)
        {
            return new NativeTerrainSurfaceBatchTransferReadiness(
                false, -1, targetRuntimeKeys.Length, 0, 0, 0, 0, [], targetRuntimeKeys,
                "The target level's native collision-property table is unavailable.");
        }

        bool crossLevel = _currentLevel != null &&
            !string.Equals(
                LevelCatalog.NormalizeKey(sourceLevelKey),
                LevelCatalog.NormalizeKey(_currentLevel.Key),
                StringComparison.OrdinalIgnoreCase);
        return targetCatalog.EvaluateBatchTransfer(targetRuntimeKeys, signature, crossLevel);
    }

    private TerrainTextureCatalog BuildNativeTerrainTextureCatalog(
        LevelDefinition level,
        GeometryCandidate geometry,
        out string error)
    {
        error = "";
        IReadOnlyList<TerrainTextureSlot> slots = [];
        NativeTerrainTextureRuntimeControlAudit? runtimeControlAudit = null;
        string sourceImage = FirstExistingDiscImagePath(
            _discImagePathBox.Text,
            _skyboxDiscImagePathBox.Text,
            DiscImageLocator.FindImage(_workspace));
        if (string.IsNullOrWhiteSpace(sourceImage) || !File.Exists(sourceImage))
        {
            error = "Choose the retail Spyro BIN/CUE to prove native texture records.";
        }
        else
        {
            try
            {
                FileInfo sourceInfo = new(sourceImage);
                string levelKey = LevelCatalog.NormalizeKey(level.Key);
                if (_nativeTerrainTextureCatalogCache.TryGetValue(
                        levelKey,
                        out NativeTerrainTextureCatalogCacheEntry? cached) &&
                    string.Equals(
                        cached.SourceImagePath,
                        sourceImage,
                        StringComparison.OrdinalIgnoreCase) &&
                    cached.SourceImageLength == sourceInfo.Length &&
                    cached.SourceImageWriteUtc == sourceInfo.LastWriteTimeUtc &&
                    ReferenceEquals(cached.Geometry, geometry))
                {
                    error = cached.Error;
                    return cached.Catalog;
                }

                runtimeControlAudit = InspectNativeTerrainRuntimeControls(sourceImage, level);
                if (!runtimeControlAudit.Complete)
                {
                    string blocker = runtimeControlAudit.SafetyBlockers.FirstOrDefault()
                        ?? "The runtime texture-control audit is incomplete.";
                    error = string.IsNullOrWhiteSpace(error) ? blocker : $"{error} {blocker}";
                }
                else
                {
                    // Donor art must be judged after the retail loader has
                    // initialized animation/scroll destinations. The exporter
                    // uses the same initialized record and still repeats its
                    // ownership, alias, and final-readback proofs.
                    slots = TerrainPatchExporter.InspectInitializedTextureSlots(
                        sourceImage,
                        level,
                        runtimeControlAudit);
                }

                TerrainTextureCatalog catalog = TerrainTextureCatalogBuilder.Build([
                    new TerrainTextureCatalogLevelInput
                    {
                        Level = level,
                        Geometry = geometry,
                        TextureSlots = slots,
                        RuntimeControlAudit = runtimeControlAudit
                    }
                ]);
                _nativeTerrainTextureCatalogCache[levelKey] =
                    new NativeTerrainTextureCatalogCacheEntry(
                        sourceImage,
                        sourceInfo.Length,
                        sourceInfo.LastWriteTimeUtc,
                        geometry,
                        catalog,
                        error);
                return catalog;
            }
            catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException)
            {
                error = ex.Message;
            }
        }

        return TerrainTextureCatalogBuilder.Build([
            new TerrainTextureCatalogLevelInput
            {
                Level = level,
                Geometry = geometry,
                TextureSlots = slots,
                RuntimeControlAudit = runtimeControlAudit
            }
        ]);
    }

    private sealed record NativeTerrainDonorGeometryCacheEntry(
        string OverlayPath,
        long OverlayLength,
        DateTime OverlayWriteUtc,
        GeometryCandidate Geometry);

    private sealed record NativeTerrainTextureCatalogCacheEntry(
        string SourceImagePath,
        long SourceImageLength,
        DateTime SourceImageWriteUtc,
        GeometryCandidate Geometry,
        TerrainTextureCatalog Catalog,
        string Error);

    private sealed record NativeTerrainVisualDonorCacheEntry(
        bool Success,
        string RepresentativeRuntimeKey,
        TerrainTextureVisualEdit Visual,
        string Note);

    private NativeTerrainTextureRuntimeControlAudit InspectNativeTerrainRuntimeControls(
        string sourceImage,
        LevelDefinition level)
    {
        FileInfo info = new(sourceImage);
        if (!string.Equals(_nativeTerrainRuntimeControlSourceImage, sourceImage, StringComparison.OrdinalIgnoreCase) ||
            _nativeTerrainRuntimeControlSourceLength != info.Length ||
            _nativeTerrainRuntimeControlSourceWriteUtc != info.LastWriteTimeUtc)
        {
            _nativeTerrainRuntimeControlAudits.Clear();
            _nativeTerrainRuntimeControlSourceImage = sourceImage;
            _nativeTerrainRuntimeControlSourceLength = info.Length;
            _nativeTerrainRuntimeControlSourceWriteUtc = info.LastWriteTimeUtc;
        }

        string levelKey = LevelCatalog.NormalizeKey(level.Key);
        if (!_nativeTerrainRuntimeControlAudits.TryGetValue(levelKey, out NativeTerrainTextureRuntimeControlAudit? audit))
        {
            audit = NativeTerrainTextureRuntimeControlScanner.Inspect(sourceImage, level);
            _nativeTerrainRuntimeControlAudits[levelKey] = audit;
        }

        return audit;
    }

    private bool BlockLegacyCustomTerrainTextureAction()
    {
        _statusText.Text = LegacyCustomTerrainTextureBlock;
        return true;
    }

    private static bool IsVerifiedCrossLevelTerrainRelocationIntegrated() => true;

    private static bool TryProveCrossLevelTerrainTextureArt(
        string sourceImage,
        LevelDefinition targetLevel,
        LevelDefinition donorLevel,
        int targetTextureId,
        int donorTextureId,
        bool preserveTargetDescriptorMaterial,
        IReadOnlyList<NativeTerrainTextureRelocationEdit> existingEdits,
        out string strategy,
        out string reason)
    {
        strategy = "";
        reason = "";
        NativeTerrainTextureInPlaceTransplantRequest inPlaceRequest = new(
            targetTextureId,
            donorLevel.SourceWadEntry,
            donorTextureId);
        string inPlaceFailure = "";
        try
        {
            NativeTerrainTextureInPlaceTransplantSourceProof inPlaceProof =
                NativeTerrainTextureInPlaceTransplantBuilder.InspectSourceProof(
                    sourceImage,
                    targetLevel,
                    inPlaceRequest);
            if (NativeTerrainTextureInPlaceTransplantBuilder.TryBuild(
                    sourceImage,
                    targetLevel,
                    inPlaceRequest,
                    inPlaceProof,
                    out NativeTerrainTextureInPlaceTransplantPlan? inPlacePlan,
                    out inPlaceFailure) &&
                inPlacePlan != null &&
                inPlacePlan.SourceBindingVerified &&
                inPlacePlan.RuntimeControlClearanceVerified &&
                inPlacePlan.CompleteOwnershipClosureVerified &&
                inPlacePlan.DecodedAndExternalExclusivityVerified &&
                inPlacePlan.ExactDonorIndexedPixelsVerified &&
                inPlacePlan.ExactDonorPalettesVerified &&
                inPlacePlan.LowDetailAliasPreserved &&
                inPlacePlan.TargetDescriptorTableUnchanged &&
                inPlacePlan.TargetTextureIdPreserved &&
                inPlacePlan.LogicalReadbackVerified &&
                inPlacePlan.PhysicalAliasConflictCount == 0 &&
                inPlacePlan.OutOfOwnershipWriteCount == 0)
            {
                strategy = $"target-owned in-place ({inPlacePlan.TargetExclusiveByteCount:N0} proven bytes)";
                return true;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or IOException or OverflowException)
        {
            inPlaceFailure = ex.Message;
        }

        ArgumentNullException.ThrowIfNull(existingEdits);
        Dictionary<int, NativeTerrainTextureRelocationImport> explicitImports = existingEdits
            .Where(edit => edit.TargetTextureId != targetTextureId)
            .ToDictionary(
                edit => edit.TargetTextureId,
                edit => new NativeTerrainTextureRelocationImport(
                    edit.TargetTextureId,
                    edit.DonorWadEntry,
                    edit.DonorTextureId,
                    edit.DescriptorTier,
                    edit.PreservesTargetNativeSurface));
        explicitImports[targetTextureId] = new NativeTerrainTextureRelocationImport(
            targetTextureId,
            donorLevel.SourceWadEntry,
            donorTextureId,
            NativeTerrainTextureRelocationEditStore.CompleteDescriptorTier,
            preserveTargetDescriptorMaterial);

        HashSet<int> requiredFallbackTargets = [targetTextureId];
        List<string> combinedInPlaceFailures =
        [
            $"texture {targetTextureId}: {(string.IsNullOrWhiteSpace(inPlaceFailure) ? "in-place proof did not satisfy every required invariant" : inPlaceFailure)}"
        ];
        foreach ((int existingTargetId, NativeTerrainTextureRelocationImport existingImport) in explicitImports
                     .Where(pair => pair.Key != targetTextureId)
                     .OrderBy(pair => pair.Key))
        {
            string existingFailure = "";
            try
            {
                NativeTerrainTextureInPlaceTransplantRequest request = new(
                    existingImport.TargetTextureId,
                    existingImport.DonorWadEntry,
                    existingImport.DonorTextureId);
                NativeTerrainTextureInPlaceTransplantSourceProof proof =
                    NativeTerrainTextureInPlaceTransplantBuilder.InspectSourceProof(
                        sourceImage,
                        targetLevel,
                        request);
                if (NativeTerrainTextureInPlaceTransplantBuilder.TryBuild(
                        sourceImage,
                        targetLevel,
                        request,
                        proof,
                        out NativeTerrainTextureInPlaceTransplantPlan? plan,
                        out existingFailure) &&
                    plan != null &&
                    plan.SourceBindingVerified &&
                    plan.RuntimeControlClearanceVerified &&
                    plan.CompleteOwnershipClosureVerified &&
                    plan.DecodedAndExternalExclusivityVerified &&
                    plan.ExactDonorIndexedPixelsVerified &&
                    plan.ExactDonorPalettesVerified &&
                    plan.LowDetailAliasPreserved &&
                    plan.TargetDescriptorTableUnchanged &&
                    plan.TargetTextureIdPreserved &&
                    plan.LogicalReadbackVerified &&
                    plan.PhysicalAliasConflictCount == 0 &&
                    plan.OutOfOwnershipWriteCount == 0)
                {
                    continue;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or IOException or OverflowException)
            {
                existingFailure = ex.Message;
            }

            requiredFallbackTargets.Add(existingTargetId);
            combinedInPlaceFailures.Add(
                $"texture {existingTargetId}: {(string.IsNullOrWhiteSpace(existingFailure) ? "in-place proof did not satisfy every required invariant" : existingFailure)}");
        }

        NativeTerrainTextureRuntimeControlAudit targetRuntime;
        try
        {
            targetRuntime = NativeTerrainTextureRuntimeControlScanner.Inspect(sourceImage, targetLevel);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or OverflowException)
        {
            reason = $"In-place proof: {inPlaceFailure} Runtime texture-control proof: {ex.Message}".Trim();
            return false;
        }

        bool TryBuildRelocationAttempt(
            IReadOnlyList<int> promotedTargets,
            out int[] attemptTargetIds,
            out NativeTerrainTextureRelocationExportPlan? attemptPlan,
            out string attemptFailure)
        {
            attemptTargetIds = [];
            attemptPlan = null;
            attemptFailure = "";
            try
            {
                attemptTargetIds = NativeTexturePageOwnershipScanner
                    .FindTerrainTextureStorageOverlapClosure(
                        sourceImage,
                        targetLevel,
                        requiredFallbackTargets.Concat(promotedTargets).Distinct().Order().ToArray())
                    .ToArray();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or IOException or OverflowException)
            {
                attemptFailure = $"storage-overlap closure failed ({ex.Message})";
                return false;
            }

            int[] controlledClosure = attemptTargetIds
                .Where(textureId => !targetRuntime.IsRuntimePersistentTarget(textureId))
                .ToArray();
            if (controlledClosure.Length > 0)
            {
                attemptFailure =
                    $"record(s) {string.Join(", ", controlledClosure)} are rewritten by native animation or scrolling controls";
                return false;
            }

            if (!NativeTexturePageOwnershipScanner.TryBuildRelocationOwnershipProof(
                    sourceImage,
                    targetLevel,
                    attemptTargetIds,
                    out NativeTexturePageRelocationOwnershipProofResult? relocationProof,
                    out string ownershipFailure) ||
                relocationProof == null)
            {
                attemptFailure = $"relocation ownership proof failed ({ownershipFailure})";
                return false;
            }

            NativeTerrainTextureRelocationImport[] imports = attemptTargetIds
                .Select(textureId => explicitImports.TryGetValue(textureId, out NativeTerrainTextureRelocationImport? import)
                    ? import
                    : new NativeTerrainTextureRelocationImport(
                        textureId,
                        targetLevel.SourceWadEntry,
                        textureId,
                        NativeTerrainTextureRelocationEditStore.CompleteDescriptorTier,
                        PreserveTargetDescriptorMaterial: true))
                .ToArray();
            try
            {
                attemptPlan = NativeTerrainTextureRelocationComposer.BuildPlan(
                    new NativeTerrainTextureRelocationExportRequest(
                        SourceImagePath: sourceImage,
                        SourceCuePath: Path.ChangeExtension(sourceImage, ".cue"),
                        OutputPrefix: Path.Combine(
                            Path.GetTempPath(),
                            $"spyro-editor-{targetLevel.Key}-{targetTextureId}-texture-proof"),
                        TargetLevel: targetLevel,
                        Imports: imports,
                        OwnershipProof: relocationProof.Proof,
                        WriteImage: false));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or IOException or OverflowException)
            {
                attemptFailure = $"byte-private relocation failed ({ex.Message})";
                return false;
            }

            NativeTerrainTextureRelocationPlan plan = attemptPlan.Relocation;
            if (!attemptPlan.RuntimeTargetsPersistent ||
                !plan.ExactDonorIndexedPixelsVerified ||
                !plan.ExactDonorPalettesVerified ||
                !plan.LowDetailAliasPreserved ||
                !plan.LogicalReadbackVerified ||
                !plan.ProtectedStorageVerified ||
                !plan.TargetDescriptorMaterialPolicyVerified)
            {
                attemptFailure = "relocation omitted a required runtime, ownership, complete-record, or exact-readback proof";
                attemptPlan = null;
                return false;
            }
            return true;
        }

        int[] availableExistingTargets = existingEdits
            .Select(edit => edit.TargetTextureId)
            .Where(textureId => !requiredFallbackTargets.Contains(textureId))
            .Distinct()
            .Order()
            .ToArray();
        NativeTerrainTexturePromotionAttemptSet promotionSearch =
            NativeTerrainTextureRelocationAllocator.BuildBoundedPromotionAttempts(
                availableExistingTargets);

        List<string> failures = [];
        HashSet<string> attemptedClosures = new(StringComparer.Ordinal);
        foreach (int[] promotionAttempt in promotionSearch.Attempts)
        {
            if (!TryBuildRelocationAttempt(
                    promotionAttempt,
                    out int[] overlapClosure,
                    out NativeTerrainTextureRelocationExportPlan? relocation,
                    out string relocationFailure) ||
                relocation == null)
            {
                string closureKey = string.Join(",", overlapClosure);
                if (attemptedClosures.Add(closureKey))
                    failures.Add($"[{closureKey}] {relocationFailure}");
                continue;
            }

            NativeTerrainTextureRelocationPlan plan = relocation.Relocation;
            int[] promotedExplicit = overlapClosure
                .Where(textureId => !requiredFallbackTargets.Contains(textureId) && explicitImports.ContainsKey(textureId))
                .ToArray();
            int[] preservedCompanions = overlapClosure
                .Where(textureId => !explicitImports.ContainsKey(textureId))
                .ToArray();
            string promotionNote = promotedExplicit.Length == 0
                ? ""
                : $"; promoted staged record(s) {string.Join(", ", promotedExplicit)} into the same atomic batch";
            string companionNote = preservedCompanions.Length == 0
                ? ""
                : $"; preserved overlapping record(s) {string.Join(", ", preservedCompanions)}";
            strategy =
                $"byte-private relocation ({plan.AllocatedPixelByteCount + plan.AllocatedPaletteByteCount:N0} allocated bytes{promotionNote}{companionNote})";
            return true;
        }

        string pairGuardNote = promotionSearch.PairAttemptsTruncated
            ? $" Pair promotion search tried the first {promotionSearch.PairAttemptCount:N0} of {promotionSearch.TotalPairCount:N0} lexicographic pairs, then the complete staged set."
            : "";
        reason =
            ($"In-place proof: {string.Join(" | ", combinedInPlaceFailures.Take(3))} Bounded relocation proof: {string.Join(" | ", failures.Take(3))}" +
             pairGuardNote).Trim();
        return false;
    }

    internal async Task<string> AssertTerrainCatalogAtomicGuardsForTestingAsync()
    {
        if (_currentLevel == null || _currentGeometry == null)
            throw new InvalidOperationException("Load a level before running the terrain catalog atomic UI smoke.");

        TerrainTextureCatalog currentTextureCatalog = BuildNativeTerrainTextureCatalog(_currentLevel, _currentGeometry, out string catalogError);
        TerrainTextureCatalogEntry stableEntry = currentTextureCatalog.Entries.FirstOrDefault(entry =>
            entry.FaceCount > 0 && entry.Readiness.TargetRuntime.CanPersist)
            ?? throw new InvalidOperationException($"The loaded level has no source-proven runtime-persistent target texture for the atomic UI smoke. {catalogError}");
        TerrainTextureCatalogEntry controlledEntry = currentTextureCatalog.Entries.FirstOrDefault(entry =>
            entry.FaceCount > 0 &&
            !entry.Readiness.TargetRuntime.CanPersist &&
            entry.Readiness.TargetRuntime.Note.Contains("controlled by", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The loaded level has no face-referenced animation/scroll-controlled target texture for the atomic UI smoke.");
        TerrainPolygon target = _currentGeometry.Polygons.First(face =>
            !face.IsTerrainRemoved && face.TextureId == stableEntry.TextureId);
        TerrainPolygon controlledTarget = _currentGeometry.Polygons.First(face =>
            !face.IsTerrainRemoved && face.TextureId == controlledEntry.TextureId);
        int targetIndex = _currentGeometry.Polygons.IndexOf(target);
        int controlledTargetIndex = _currentGeometry.Polygons.IndexOf(controlledTarget);
        TerrainPolygon? previousSelection = _selectedTerrain;
        int previousSelectionIndex = _selectedTerrainIndex;
        string editsPath = Path.Combine(_workspace.RootPath, $"{_currentLevel.Key}-terrain-edits.json");
        string manifestPath = CustomTerrainTextureStore.ManifestPath(_workspace.RootPath, _currentLevel.Key);
        string relocationPath = NativeTerrainTextureRelocationEditStore.ManifestPath(
            _workspace.RootPath,
            _currentLevel.Key);
        string materialOverridesPath = Path.Combine(
            _workspace.RootPath,
            $"{_currentLevel.Key}-terrain-material-overrides.json");
        byte[]? editsBefore = ReadOptionalSmokeFile(editsPath);
        byte[]? manifestBefore = ReadOptionalSmokeFile(manifestPath);
        byte[]? relocationBefore = ReadOptionalSmokeFile(relocationPath);
        byte[]? materialOverridesBefore = ReadOptionalSmokeFile(materialOverridesPath);
        string customRoot = Path.Combine(_workspace.RootPath, "_local", "custom-textures");
        string[] customFilesBefore = Directory.Exists(customRoot)
            ? Directory.GetFiles(customRoot, "*", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase).ToArray()
            : [];
        int textureBefore = target.TextureId;
        TerrainSurfaceBehaviorEdit? behaviorBefore = target.SurfaceBehaviorEdit;
        TerrainTextureVisualEdit? visualBefore = target.TextureVisualEdit;
        string surfaceBefore = target.Surface;
        int controlledTextureBefore = controlledTarget.TextureId;
        TerrainSurfaceBehaviorEdit? controlledBehaviorBefore = controlledTarget.SurfaceBehaviorEdit;
        string controlledSurfaceBefore = controlledTarget.Surface;
        int affectedFaceCount = _currentGeometry.Polygons.Count(face =>
            !face.IsTerrainRemoved && face.TextureId == target.TextureId);
        int controlledAffectedFaceCount = _currentGeometry.Polygons.Count(face =>
            !face.IsTerrainRemoved && face.TextureId == controlledTarget.TextureId);
        IReadOnlyList<NativeTerrainTextureRelocationEdit> relocationsStateBefore = _nativeTerrainTextureRelocations;
        TerrainLookClipboard? clipboardBefore = _terrainLookClipboard;

        _selectedTerrain = target;
        _selectedTerrainIndex = targetIndex;
        try
        {
            TerrainTextureSwapChoice blockedSameLevel = new()
            {
                TextureId = target.TextureId == 0 ? 1 : 0,
                NativeBehavior = NativeTerrainSurfaceSignature.Ordinary,
                CanUseArt = true,
                ArtReadinessNote = "Synthetic resident art proof.",
                CanTransferBehavior = false,
                BehaviorTransferNote = "Synthetic missing selected-face property proof.",
                AffectedFaceCount = 1,
                BehaviorReadyFaceCount = 0
            };
            await ApplySelectedTerrainInGameLookAsync(blockedSameLevel);
            if (!(_statusText.Text ?? "").Contains("Cannot apply texture", StringComparison.OrdinalIgnoreCase) ||
                !(_statusText.Text ?? "").Contains("No face or saved edit was changed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The same-level catalog Apply did not stop before mutation when its property proof was missing.");
            }

            int syntheticTextureId = target.TextureId == 0 ? 1 : 0;
            TerrainTextureVisualCorner syntheticCorner = new(
                ColorRgba.FromRgb(24, 64, 104),
                ColorRgba.FromRgb(32, 88, 144));
            TerrainTextureVisualEdit syntheticVisual = new(
                syntheticTextureId,
                _currentLevel.Key,
                target.RuntimeKey,
                target.SectorOffset,
                target.FaceOffset,
                syntheticCorner,
                syntheticCorner,
                syntheticCorner,
                syntheticCorner,
                "synthetic resident visual");
            NativeTerrainTextureRelocationEdit syntheticRelocation = new(
                TargetTextureId: syntheticTextureId,
                DonorLevelKey: "gnastysworld",
                DonorLevelName: "Gnasty's World",
                DonorWadEntry: 70,
                DonorTextureId: 22,
                DonorRuntimeKey: "ui-smoke-active-relocation",
                DescriptorTier: NativeTerrainTextureRelocationEditStore.CompleteDescriptorTier,
                PreviewImagePath: "",
                PreviewImageName: "",
                CreatedAt: DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            _nativeTerrainTextureRelocations = [syntheticRelocation];
            TerrainTextureSwapChoice relocationConflictedResident = new()
            {
                TextureId = syntheticTextureId,
                RuntimeKey = target.RuntimeKey,
                NativeBehavior = NativeTerrainSurfaceSignature.Ordinary,
                NativeVisual = syntheticVisual,
                CanUseArt = true,
                CanTransferVisual = true,
                CanTransferBehavior = true,
                AffectedFaceCount = 1,
                BehaviorReadyFaceCount = 1
            };
            await ApplySelectedTerrainInGameLookAsync(relocationConflictedResident);
            if (!(_statusText.Text ?? "").Contains("shared replacement", StringComparison.OrdinalIgnoreCase) ||
                target.TextureId != textureBefore || target.TextureVisualEdit != visualBefore || target.SurfaceBehaviorEdit != behaviorBefore)
            {
                throw new InvalidOperationException("A resident Apply was not blocked atomically when its donor texture record had an active relocation.");
            }

            _terrainLookClipboard = new TerrainLookClipboard(
                syntheticTextureId,
                "water",
                syntheticVisual.AverageNearColor,
                target.RuntimeKey,
                "synthetic relocated donor",
                _currentLevel.Key,
                syntheticVisual,
                NativeTerrainSurfaceSignature.Ordinary,
                target.RuntimeKey,
                Array.Empty<CustomTerrainTextureImport>());
            await PasteTerrainLookAsync(targetIndex, target);
            if (!(_statusText.Text ?? "").Contains("shared replacement", StringComparison.OrdinalIgnoreCase) ||
                target.TextureId != textureBefore || target.TextureVisualEdit != visualBefore || target.SurfaceBehaviorEdit != behaviorBefore)
            {
                throw new InvalidOperationException("Paste Face Look was not blocked atomically when the copied donor texture record had an active relocation.");
            }
            _terrainLookClipboard = clipboardBefore;
            _nativeTerrainTextureRelocations = relocationsStateBefore;

            target.ApplyTextureVisualEdit(syntheticVisual with { SourceTextureId = target.TextureId });
            TerrainCrossLevelLookChoice faceLocalConflictedCrossLevel = new()
            {
                LevelKey = "gnastysworld",
                LevelName = "Gnasty's World",
                TextureId = syntheticTextureId,
                TargetTextureId = target.TextureId,
                RuntimeKey = "ui-smoke-face-local-conflict",
                NativeBehavior = NativeTerrainSurfaceSignature.Ordinary,
                CanUseArt = true,
                CanTransferBehavior = true,
                AffectedFaceCount = affectedFaceCount,
                BehaviorReadyFaceCount = affectedFaceCount,
                CanPersistAtRuntime = true,
                RuntimePersistenceNote = stableEntry.Readiness.TargetRuntime.Note
            };
            await ApplySelectedTerrainCrossLevelLookAsync(faceLocalConflictedCrossLevel);
            if (!(_statusText.Text ?? "").Contains("face-local", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("A cross-level Apply did not block an existing face-local native visual recipe before staging files.");
            if (visualBefore == null)
                target.ClearTextureVisualEdit();
            else
                target.ApplyTextureVisualEdit(visualBefore);

            TerrainCrossLevelLookChoice blockedCrossLevel = new()
            {
                LevelKey = "gnastysworld",
                LevelName = "Gnasty's World",
                TextureId = 0,
                TargetTextureId = target.TextureId,
                NativeBehavior = NativeTerrainSurfaceSignature.Ordinary,
                CanUseArt = false,
                ArtReadinessNote = CrossLevelTerrainTextureOwnershipBlock,
                CanTransferBehavior = true,
                BehaviorTransferNote = "Synthetic complete batch property proof.",
                AffectedFaceCount = affectedFaceCount,
                BehaviorReadyFaceCount = affectedFaceCount,
                CanPersistAtRuntime = stableEntry.Readiness.TargetRuntime.CanPersist,
                RuntimePersistenceNote = stableEntry.Readiness.TargetRuntime.Note
            };
            if (!blockedCrossLevel.CanPersistAtRuntime ||
                !blockedCrossLevel.RuntimePersistenceNote.Contains("does not rewrite", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The source-bound catalog did not mark the stable target texture as runtime-persistent.");
            }
            await ApplySelectedTerrainCrossLevelLookAsync(blockedCrossLevel);
            if (!(_statusText.Text ?? "").Contains("No face or saved edit was changed", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The blocked cross-level UI action did not explain that it stayed non-mutating.");

            _selectedTerrain = controlledTarget;
            _selectedTerrainIndex = controlledTargetIndex;
            TerrainCrossLevelLookChoice runtimeControlledCrossLevel = new()
            {
                LevelKey = "gnastysworld",
                LevelName = "Gnasty's World",
                TextureId = 0,
                TargetTextureId = controlledTarget.TextureId,
                NativeBehavior = NativeTerrainSurfaceSignature.Ordinary,
                CanUseArt = true,
                ArtReadinessNote = "Synthetic complete art proof.",
                CanTransferBehavior = true,
                BehaviorTransferNote = "Synthetic complete batch property proof.",
                AffectedFaceCount = controlledAffectedFaceCount,
                BehaviorReadyFaceCount = controlledAffectedFaceCount,
                CanPersistAtRuntime = controlledEntry.Readiness.TargetRuntime.CanPersist,
                RuntimePersistenceNote = controlledEntry.Readiness.TargetRuntime.Note
            };
            if (runtimeControlledCrossLevel.CanPersistAtRuntime ||
                !runtimeControlledCrossLevel.RuntimePersistenceNote.Contains("controlled by", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The source-bound catalog did not identify the controlled target texture.");
            }
            await ApplySelectedTerrainCrossLevelLookAsync(runtimeControlledCrossLevel);
            if (!(_statusText.Text ?? "").Contains("controlled by", StringComparison.OrdinalIgnoreCase) ||
                !(_statusText.Text ?? "").Contains("No face or saved edit was changed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The controlled target texture was not blocked before UI mutation.");
            }

            _selectedTerrain = target;
            _selectedTerrainIndex = targetIndex;

            await StageSelectedTerrainFaceGradientAsync(
                ColorRgba.FromRgb(24, 48, 72),
                ColorRgba.FromRgb(96, 144, 192),
                "ui-smoke-custom-png",
                "UI smoke",
                "ui-smoke",
                "Painted");
            if (!(_statusText.Text ?? "").Contains("obsolete 0x800-row", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The blocked custom PNG UI action did not name the obsolete layout.");

            string[] customFilesAfter = Directory.Exists(customRoot)
                ? Directory.GetFiles(customRoot, "*", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase).ToArray()
                : [];
            if (target.TextureId != textureBefore ||
                target.SurfaceBehaviorEdit != behaviorBefore ||
                target.TextureVisualEdit != visualBefore ||
                !string.Equals(target.Surface, surfaceBefore, StringComparison.Ordinal) ||
                controlledTarget.TextureId != controlledTextureBefore ||
                controlledTarget.SurfaceBehaviorEdit != controlledBehaviorBefore ||
                !string.Equals(controlledTarget.Surface, controlledSurfaceBefore, StringComparison.Ordinal) ||
                !OptionalSmokeFileEquals(editsBefore, editsPath) ||
                !OptionalSmokeFileEquals(manifestBefore, manifestPath) ||
                !OptionalSmokeFileEquals(relocationBefore, relocationPath) ||
                !OptionalSmokeFileEquals(materialOverridesBefore, materialOverridesPath) ||
                !customFilesBefore.SequenceEqual(customFilesAfter, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("A blocked terrain catalog/custom-art UI action partially mutated a face, saved edit, relocation manifest, material override, or staged file.");
            }

            string sourceImage = FirstExistingDiscImagePath(
                _discImagePathBox.Text,
                _skyboxDiscImagePathBox.Text,
                DiscImageLocator.FindImage(_workspace));
            LevelDefinition artisans = _catalog.FindByKey("artisans")
                ?? throw new InvalidOperationException("Artisans is missing from the UI smoke level catalog.");
            LevelDefinition gnastysWorld = _catalog.FindByKey("gnastysworld")
                ?? throw new InvalidOperationException("Gnasty's World is missing from the UI smoke level catalog.");
            LevelDefinition darkPassage = _catalog.FindByKey("darkpassage")
                ?? throw new InvalidOperationException("Dark Passage is missing from the UI smoke level catalog.");
            (int TargetTextureId, int DonorTextureId)[] existingRecipes =
            [
                (10, 9),
                (22, 29),
                (48, 23),
                (52, 7),
                (54, 23),
                (55, 17),
                (56, 16)
            ];
            NativeTerrainTextureRelocationEdit[] existingMixedDonorEdits = existingRecipes
                .Select(recipe => new NativeTerrainTextureRelocationEdit(
                    TargetTextureId: recipe.TargetTextureId,
                    DonorLevelKey: gnastysWorld.Key,
                    DonorLevelName: gnastysWorld.DisplayName,
                    DonorWadEntry: gnastysWorld.SourceWadEntry,
                    DonorTextureId: recipe.DonorTextureId,
                    DonorRuntimeKey: NativeTerrainTextureRelocationEditStore.BuildTextureRecordProvenanceKey(
                        gnastysWorld.Key,
                        recipe.DonorTextureId),
                    DescriptorTier: NativeTerrainTextureRelocationEditStore.CompleteDescriptorTier,
                    PreviewImagePath: "",
                    PreviewImageName: "",
                    CreatedAt: "2026-07-21T00:00:00.0000000+00:00"))
                .ToArray();
            bool mixedDonorReady = TryProveCrossLevelTerrainTextureArt(
                sourceImage,
                artisans,
                darkPassage,
                targetTextureId: 5,
                donorTextureId: 31,
                preserveTargetDescriptorMaterial: true,
                existingMixedDonorEdits,
                out string mixedDonorStrategy,
                out string mixedDonorFailure);
            if (!mixedDonorReady ||
                !mixedDonorStrategy.Contains("promoted staged record(s) 10", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"The exact multi-donor UI preflight did not promote Artisans texture 10 with target 5. {mixedDonorFailure}");
            }

            string discovery = await AssertNativeTerrainTextureRelocationBuildDiscoveryForTestingAsync(textureBefore);
            return $"stable target {textureBefore} passed runtime persistence; controlled target {controlledTextureBefore} was blocked; exact Gnasty's World plus Dark Passage preflight promoted texture 10 atomically; resident-relocation, Paste donor-relocation, face-local/cross-level stacking, same-level property, cross-level art, and legacy custom PNG guards stayed atomic and non-mutating; {discovery}";
        }
        finally
        {
            _nativeTerrainTextureRelocations = relocationsStateBefore;
            _terrainLookClipboard = clipboardBefore;
            if (target.TextureId != textureBefore)
                target.ApplyTextureOverride(textureBefore);
            if (visualBefore == null)
                target.ClearTextureVisualEdit();
            else
                target.ApplyTextureVisualEdit(visualBefore);
            if (behaviorBefore == null)
                target.ClearSurfaceBehaviorEdit();
            else
                target.ApplySurfaceBehaviorEdit(behaviorBefore);
            _selectedTerrain = previousSelection;
            _selectedTerrainIndex = previousSelectionIndex;
        }
    }

    private async Task<string> AssertNativeTerrainTextureRelocationBuildDiscoveryForTestingAsync(int targetTextureId)
    {
        if (_currentLevel == null)
            throw new InvalidOperationException("Load a level before checking native terrain texture build discovery.");

        LevelDefinition donor = _catalog.Levels.FirstOrDefault(level =>
            !string.Equals(level.Key, _currentLevel.Key, StringComparison.OrdinalIgnoreCase) &&
            level.SourceWadEntry >= 0)
            ?? throw new InvalidOperationException("The level catalog has no cross-level donor for native texture build discovery.");
        string relocationPath = NativeTerrainTextureRelocationEditStore.ManifestPath(
            _workspace.RootPath,
            _currentLevel.Key);
        byte[]? relocationBefore = ReadOptionalSmokeFile(relocationPath);
        try
        {
            await NativeTerrainTextureRelocationEditStore.SaveManifestAsync(
                relocationPath,
                _currentLevel.Key,
                _currentLevel.DisplayName,
                [
                    new NativeTerrainTextureRelocationEdit(
                        TargetTextureId: targetTextureId,
                        DonorLevelKey: donor.Key,
                        DonorLevelName: donor.DisplayName,
                        DonorWadEntry: donor.SourceWadEntry,
                        DonorTextureId: 0,
                        DonorRuntimeKey: "ui-smoke-native-relocation-only",
                        DescriptorTier: NativeTerrainTextureRelocationEditStore.CompleteDescriptorTier,
                        PreviewImagePath: "",
                        PreviewImageName: "",
                        CreatedAt: DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture))
                ]);

            EditedLevelExportTarget target = FindEditedLevelExportTargets().SingleOrDefault(candidate =>
                string.Equals(candidate.Level.Key, _currentLevel.Key, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("A relocation-only level was omitted from All Saved Edits build discovery.");
            if (!target.HasNativeTerrainTextureRelocations)
                throw new InvalidOperationException("All Saved Edits found the level but did not classify its native terrain texture relocation.");
            if (target.HasTerrainEdits || target.HasCustomTerrainTextures)
            {
                throw new InvalidOperationException(
                    "The relocation-only build-discovery fixture unexpectedly depended on another terrain edit surface.");
            }

            return "a relocation-only manifest was included in All Saved Edits discovery";
        }
        finally
        {
            if (relocationBefore == null)
            {
                if (File.Exists(relocationPath))
                    File.Delete(relocationPath);
            }
            else
            {
                File.WriteAllBytes(relocationPath, relocationBefore);
            }
        }
    }

    internal async Task<string> AssertNativeTerrainFaceTextureRoundTripForTestingAsync()
    {
        const int targetTextureId = 65;
        const int donorTextureId = 27;
        const string targetRuntimeKey = "47:6:hp";
        const string expectedVisualRuntimeKey = "38:105:hp";

        LevelDefinition level = _catalog.FindByKey("artisans")
            ?? throw new InvalidOperationException("Artisans is missing from the level catalog.");
        if (!string.Equals(_currentLevel?.Key, level.Key, StringComparison.OrdinalIgnoreCase))
            await SelectLevelAsync(level);
        if (_currentLevel == null || _currentGeometry == null)
            throw new InvalidOperationException("Artisans did not load for the resident face texture round-trip.");

        string terrainEditsPath = Path.Combine(_workspace.RootPath, $"{level.Key}-terrain-edits.json");
        if (File.Exists(terrainEditsPath))
        {
            throw new InvalidOperationException(
                "The resident face texture round-trip requires an isolated Artisans workspace with no existing terrain edit file.");
        }

        TerrainPolygon target = _currentGeometry.Polygons.Single(face =>
            !face.IsTerrainRemoved &&
            face.TextureId == targetTextureId &&
            string.Equals(face.RuntimeKey, targetRuntimeKey, StringComparison.OrdinalIgnoreCase));
        _selectedTerrain = target;
        _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(target);

        TerrainTextureSwapChoice choice = BuildTerrainTextureSwapChoices().Single(candidate =>
            candidate.TextureId == donorTextureId &&
            candidate.NativeBehavior is { SurfaceType: 0, Param1: 0, Param2: 0 });
        if (!choice.CanApplyAtomically || choice.NativeVisual == null)
            throw new InvalidOperationException($"The production same-level catalog choice is blocked: {choice.AtomicBlockReason}");
        if (!string.Equals(choice.VisualRuntimeKey, expectedVisualRuntimeKey, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(choice.NativeVisual.SourceRuntimeKey, expectedVisualRuntimeKey, StringComparison.OrdinalIgnoreCase) ||
            choice.NativeVisual.SourceTextureId != donorTextureId ||
            choice.NativeVisual.UniqueCornerPairCount != 1 ||
            choice.NativeVisual.Corner0.NearColor != ColorRgba.FromRgb(30, 138, 212) ||
            choice.NativeVisual.Corner0.FarColor != ColorRgba.FromRgb(47, 178, 242))
        {
            throw new InvalidOperationException("The production catalog did not select the proven horizontal-pool visual and exact native tint pair.");
        }

        await ApplySelectedTerrainInGameLookAsync(choice);
        TerrainTextureVisualEdit stagedVisual = choice.NativeVisual;
        if (target.TextureId != donorTextureId ||
            target.TextureVisualEdit != stagedVisual ||
            target.SurfaceBehaviorEdit is not { SurfaceType: 0, Param1: 0, Param2: 0 } ||
            target.SurfaceColor != stagedVisual.AverageNearColor ||
            !File.Exists(terrainEditsPath) ||
            HasUnsavedTerrainEdits())
        {
            throw new InvalidOperationException("The editor Apply path did not stage, preview, and save the complete native texture/visual/property edit.");
        }
        using (JsonDocument saved = JsonDocument.Parse(File.ReadAllText(terrainEditsPath)))
        {
            JsonElement edit = saved.RootElement.GetProperty("edits").EnumerateArray().Single();
            if (!edit.GetProperty("nativeTextureVisualEdit").GetBoolean() ||
                edit.GetProperty("nativeTextureVisualSourceTextureId").GetInt32() != donorTextureId ||
                !string.Equals(edit.GetProperty("nativeTextureVisualSourceRuntimeKey").GetString(), expectedVisualRuntimeKey, StringComparison.OrdinalIgnoreCase) ||
                edit.GetProperty("nativeTextureVisualNearColors").GetArrayLength() != 4 ||
                edit.GetProperty("nativeTextureVisualFarColors").GetArrayLength() != 4)
            {
                throw new InvalidOperationException("The production editor save omitted native visual provenance or four-corner tint arrays.");
            }
        }

        await SelectLevelAsync(level);
        TerrainPolygon reloaded = _currentGeometry?.Polygons.Single(face =>
            string.Equals(face.RuntimeKey, targetRuntimeKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The Artisans target did not reload after saving the resident texture swap.");
        if (reloaded.TextureId != donorTextureId ||
            reloaded.TextureVisualEdit != stagedVisual ||
            reloaded.SurfaceBehaviorEdit is not { SurfaceType: 0, Param1: 0, Param2: 0 } ||
            reloaded.SurfaceColor != stagedVisual.AverageNearColor)
        {
            throw new InvalidOperationException("Reload did not restore the exact resident texture, native visual, gameplay property, and preview tint.");
        }

        _selectedTerrain = reloaded;
        _selectedTerrainIndex = _currentGeometry!.Polygons.IndexOf(reloaded);
        CopyTerrainLook(_selectedTerrainIndex, reloaded);
        if (_terrainLookClipboard?.NativeVisual != stagedVisual ||
            _terrainLookClipboard.NativeBehavior is not { SurfaceType: 0, Param1: 0, Param2: 0 })
        {
            throw new InvalidOperationException("Copy Face Look did not retain the complete native visual/property recipe.");
        }
        await UndoSelectedTerrainAsync();
        using (JsonDocument undone = JsonDocument.Parse(File.ReadAllText(terrainEditsPath)))
        {
            if (undone.RootElement.GetProperty("editCount").GetInt32() != 0 ||
                undone.RootElement.GetProperty("edits").GetArrayLength() != 0)
            {
                throw new InvalidOperationException("Same-level Undo did not immediately persist removal of the terrain swap.");
            }
        }

        await PasteTerrainLookAsync(_selectedTerrainIndex, _selectedTerrain!);
        if (_selectedTerrain!.TextureId != donorTextureId ||
            _selectedTerrain.TextureVisualEdit != stagedVisual ||
            _selectedTerrain.SurfaceBehaviorEdit is not { SurfaceType: 0, Param1: 0, Param2: 0 } ||
            HasUnsavedTerrainEdits())
        {
            throw new InvalidOperationException("Paste Face Look did not reapply and save the complete source-verified native recipe.");
        }
        await UndoSelectedTerrainAsync();
        using (JsonDocument pasteUndone = JsonDocument.Parse(File.ReadAllText(terrainEditsPath)))
        {
            if (pasteUndone.RootElement.GetProperty("editCount").GetInt32() != 0 ||
                pasteUndone.RootElement.GetProperty("edits").GetArrayLength() != 0)
            {
                throw new InvalidOperationException("Undo after Paste Face Look did not persist removal of the pasted swap.");
            }
        }

        await SelectLevelAsync(level);
        TerrainPolygon undoneReload = _currentGeometry?.Polygons.Single(face =>
            string.Equals(face.RuntimeKey, targetRuntimeKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The Artisans target did not reload after Undo.");
        if (undoneReload.TextureId != targetTextureId ||
            undoneReload.TextureVisualEdit != null ||
            undoneReload.SurfaceBehaviorEdit != null)
        {
            throw new InvalidOperationException("Reload resurrected the resident texture swap after its persisted Undo.");
        }

        return $"{targetRuntimeKey} applied {targetTextureId}->{donorTextureId} with visual {expectedVisualRuntimeKey}, saved/reloaded exact tint and damaging-water property, Copy/Paste retained the full recipe, and persisted Undo did not resurrect it";
    }

    internal async Task<string> AssertNativeTerrainTextureRelocationRoundTripForTestingAsync()
    {
        const int targetTextureId = 54;
        const int donorTextureId = 22;
        const string targetRuntimeKey = "24:4:hp";
        const string donorRuntimeKey = "34:3:hp";

        LevelDefinition targetLevel = _catalog.FindByKey("artisans")
            ?? throw new InvalidOperationException("Artisans is missing from the level catalog.");
        LevelDefinition donorLevel = _catalog.FindByKey("gnastysworld")
            ?? throw new InvalidOperationException("Gnasty's World is missing from the level catalog.");
        if (!string.Equals(_currentLevel?.Key, targetLevel.Key, StringComparison.OrdinalIgnoreCase))
            await SelectLevelAsync(targetLevel);
        if (_currentLevel == null || _currentGeometry == null)
            throw new InvalidOperationException("Artisans did not load for the native texture staging round-trip.");

        string relocationPath = NativeTerrainTextureRelocationEditStore.ManifestPath(
            _workspace.RootPath,
            targetLevel.Key);
        string terrainEditsPath = Path.Combine(_workspace.RootPath, $"{targetLevel.Key}-terrain-edits.json");
        string materialOverridesPath = Path.Combine(
            _workspace.RootPath,
            $"{targetLevel.Key}-terrain-material-overrides.json");
        if (File.Exists(relocationPath) || File.Exists(terrainEditsPath) || File.Exists(materialOverridesPath))
        {
            throw new InvalidOperationException(
                "The focused native texture round-trip requires an isolated Artisans workspace with no existing terrain edit files.");
        }

        GeometryCandidate donorGeometry = TryLoadNativeTerrainDonorGeometry(donorLevel, out string donorGeometryError)
            ?? throw new InvalidOperationException(donorGeometryError);
        NativeTerrainSurfaceLevelCatalog targetSurfaceCatalog =
            TryBuildNativeTerrainSurfaceCatalog(targetLevel, _currentGeometry, out string targetSurfaceError)
            ?? throw new InvalidOperationException(targetSurfaceError);
        NativeTerrainSurfaceLevelCatalog donorSurfaceCatalog =
            TryBuildNativeTerrainSurfaceCatalog(donorLevel, donorGeometry, out string donorSurfaceError)
            ?? throw new InvalidOperationException(donorSurfaceError);
        NativeTerrainFaceSurfaceBinding donorBinding = donorSurfaceCatalog.FindFace(donorRuntimeKey)
            ?? throw new InvalidOperationException($"The focused donor face {donorRuntimeKey} is missing from the native surface catalog.");
        if (donorBinding.TextureId != donorTextureId || donorBinding.Signature == null)
        {
            throw new InvalidOperationException(
                $"The focused donor face {donorRuntimeKey} did not retain texture {donorTextureId} with one exact native property.");
        }

        TerrainPolygon targetFace = _currentGeometry.Polygons.FirstOrDefault(face =>
            !face.IsTerrainRemoved &&
            face.TextureId == targetTextureId &&
            string.Equals(face.RuntimeKey, targetRuntimeKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"The focused Artisans face {targetRuntimeKey} / texture {targetTextureId} is missing.");
        TerrainPolygon[] targetFaces = _currentGeometry.Polygons
            .Where(face => !face.IsTerrainRemoved && face.TextureId == targetTextureId)
            .ToArray();
        NativeTerrainSurfaceBatchTransferReadiness behaviorReadiness =
            targetSurfaceCatalog.EvaluateBatchTransfer(
                targetFaces.Select(face => face.RuntimeKey),
                donorBinding.Signature,
                crossLevel: true);
        if (!behaviorReadiness.CanApply || !behaviorReadiness.HasCompleteFaceCoverage)
            throw new InvalidOperationException($"The focused gameplay-property batch is not ready: {behaviorReadiness.Note}");

        TerrainTextureCatalogEntry targetCatalogEntry = BuildNativeTerrainTextureCatalog(
                targetLevel,
                _currentGeometry,
                out string targetCatalogError)
            .Entries.Single(entry => entry.TextureId == targetTextureId);
        TerrainTextureCatalogEntry donorCatalogEntry = BuildNativeTerrainTextureCatalog(
                donorLevel,
                donorGeometry,
                out string donorCatalogError)
            .Entries.Single(entry => entry.TextureId == donorTextureId);
        if (!targetCatalogEntry.Readiness.TargetRuntime.CanPersist)
            throw new InvalidOperationException(targetCatalogEntry.Readiness.TargetRuntime.Note);
        if (!donorCatalogEntry.Readiness.CrossLevelArt.CanApply)
            throw new InvalidOperationException($"{donorCatalogEntry.Readiness.CrossLevelArt.Note} {donorCatalogError} {targetCatalogError}".Trim());

        int expectedBehaviorEditCount = targetFaces.Count(face =>
            targetSurfaceCatalog.FindFace(face.RuntimeKey) is
            {
                HasExactTriangleMapping: true,
                NativeTriangleCount: > 0
            });
        TerrainCrossLevelLookChoice choice = new()
        {
            LevelKey = donorLevel.Key,
            LevelName = donorLevel.DisplayName,
            Surface = InferNativeTerrainDonorSurface([donorGeometry.Polygons.First(face =>
                string.Equals(face.RuntimeKey, donorRuntimeKey, StringComparison.OrdinalIgnoreCase))], donorBinding.Signature),
            TextureId = donorTextureId,
            FaceCount = 1,
            BehaviorSummary = donorBinding.Signature.Label,
            ProofSummary = "native exact",
            RuntimeKey = donorRuntimeKey,
            NativeBehavior = donorBinding.Signature,
            CanUseArt = true,
            ArtReadinessNote = donorCatalogEntry.Readiness.CrossLevelArt.Note,
            CanTransferBehavior = true,
            BehaviorTransferNote = behaviorReadiness.Note,
            TargetTextureId = targetTextureId,
            AffectedFaceCount = behaviorReadiness.TargetFaceCount,
            BehaviorReadyFaceCount = behaviorReadiness.ReadyFaceCount,
            BehaviorTargetTriangleCount = behaviorReadiness.TargetTriangleCount,
            CanPersistAtRuntime = true,
            RuntimePersistenceNote = targetCatalogEntry.Readiness.TargetRuntime.Note
        };
        if (!choice.CanApplyAtomically)
            throw new InvalidOperationException($"The focused UI choice is not atomic: {choice.AtomicBlockReason}");

        _selectedTerrain = targetFace;
        _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(targetFace);
        await ApplySelectedTerrainCrossLevelLookAsync(choice);
        if (!(_statusText.Text ?? "").Contains("Staged shared texture 54", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"The focused native texture swap did not stage successfully: {_statusText.Text}");

        NativeTerrainTextureRelocationEdit staged = NativeTerrainTextureRelocationEditStore
            .Load(_workspace.RootPath, targetLevel.Key)
            .Single();
        if (staged.TargetTextureId != targetTextureId ||
            staged.DonorWadEntry != donorLevel.SourceWadEntry ||
            staged.DonorTextureId != donorTextureId ||
            !string.Equals(staged.DonorRuntimeKey, donorRuntimeKey, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(staged.PreviewImagePath))
        {
            throw new InvalidOperationException("The successful UI stage lost its target, donor provenance, or generated preview.");
        }
        if (targetFaces.Count(face => face.SurfaceBehaviorEdit != null) != expectedBehaviorEditCount)
            throw new InvalidOperationException("The successful UI stage did not apply the exact expected gameplay-property edit coverage.");
        using (JsonDocument terrainEdits = JsonDocument.Parse(File.ReadAllText(terrainEditsPath)))
        {
            int savedBehaviorEdits = terrainEdits.RootElement.GetProperty("edits")
                .EnumerateArray()
                .Count(edit => edit.TryGetProperty("nativeSurfaceBehaviorEdit", out JsonElement native) && native.GetBoolean());
            if (savedBehaviorEdits != expectedBehaviorEditCount)
                throw new InvalidOperationException($"Saved behavior coverage is {savedBehaviorEdits}, expected {expectedBehaviorEditCount}.");
        }

        EditedLevelExportTarget discovered = FindEditedLevelExportTargets().Single(target =>
            string.Equals(target.Level.Key, targetLevel.Key, StringComparison.OrdinalIgnoreCase));
        if (!discovered.HasNativeTerrainTextureRelocations)
            throw new InvalidOperationException("All Saved Edits did not discover the successfully staged native texture relocation.");

        string previewPath = staged.PreviewImagePath;
        await SelectLevelAsync(targetLevel);
        TerrainPolygon reloadedTarget = _currentGeometry?.Polygons.FirstOrDefault(face =>
            !face.IsTerrainRemoved &&
            face.TextureId == targetTextureId &&
            string.Equals(face.RuntimeKey, targetRuntimeKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The focused Artisans target did not reload.");
        if (_nativeTerrainTextureRelocations.Count != 1 ||
            !reloadedTarget.SurfaceSource.Contains("native cross-level texture preview", StringComparison.OrdinalIgnoreCase) ||
            _currentGeometry!.Polygons.Count(face =>
                !face.IsTerrainRemoved && face.TextureId == targetTextureId && face.SurfaceBehaviorEdit != null) != expectedBehaviorEditCount)
        {
            throw new InvalidOperationException("Reload did not restore the staged native preview and exact gameplay-property coverage.");
        }

        _selectedTerrain = reloadedTarget;
        _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(reloadedTarget);
        await UndoSelectedTerrainAsync();
        if (_nativeTerrainTextureRelocations.Count != 0 ||
            NativeTerrainTextureRelocationEditStore.Load(_workspace.RootPath, targetLevel.Key).Count != 0 ||
            File.Exists(relocationPath) ||
            File.Exists(previewPath) ||
            _currentGeometry.Polygons.Any(face =>
                !face.IsTerrainRemoved && face.TextureId == targetTextureId && face.SurfaceBehaviorEdit != null) ||
            _currentGeometry.Polygons.Any(face =>
                !face.IsTerrainRemoved &&
                face.TextureId == targetTextureId &&
                face.SurfaceSource.Contains("native cross-level texture preview", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Undo left native relocation state, behavior edits, or its generated preview active.");
        }

        using (JsonDocument materialOverrides = JsonDocument.Parse(File.ReadAllText(materialOverridesPath)))
        {
            bool targetOverrideRemains = materialOverrides.RootElement.GetProperty("overrides")
                .EnumerateArray()
                .Any(item => item.GetProperty("textureId").GetInt32() == targetTextureId);
            if (targetOverrideRemains)
                throw new InvalidOperationException("Undo left the native donor material override active.");
        }
        if (FindEditedLevelExportTargets().Any(target =>
                string.Equals(target.Level.Key, targetLevel.Key, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Undo left the Artisans native relocation discoverable by All Saved Edits.");
        }

        await SelectLevelAsync(targetLevel);
        if (_nativeTerrainTextureRelocations.Count != 0 ||
            _currentGeometry!.Polygons.Any(face =>
                !face.IsTerrainRemoved &&
                face.TextureId == targetTextureId &&
                (face.SurfaceBehaviorEdit != null ||
                 face.SurfaceSource.Contains("native cross-level texture preview", StringComparison.OrdinalIgnoreCase))))
        {
            throw new InvalidOperationException("A second reload resurrected the undone native texture relocation.");
        }

        string behaviorResult = expectedBehaviorEditCount == 0
            ? "the source-proven visual-only target correctly persisted no invented collision-property edit"
            : $"{expectedBehaviorEditCount} exact gameplay-property edit(s) persisted";
        return
            $"Artisans {targetRuntimeKey} texture {targetTextureId} <- Gnasty's World {donorRuntimeKey} texture {donorTextureId}: " +
            $"manifest, generated preview, All Saved Edits discovery, and reload passed; {behaviorResult}; " +
            "Undo removed relocation, material, behavior, and preview state and it stayed removed after reload";
    }

    internal async Task<string> AssertNativeUnreferencedTerrainArtOnlyRoundTripForTestingAsync()
    {
        const int targetTextureId = 54;
        const int donorTextureId = 10;

        LevelDefinition targetLevel = _catalog.FindByKey("artisans")
            ?? throw new InvalidOperationException("Artisans is missing from the level catalog.");
        LevelDefinition donorLevel = _catalog.FindByKey("beastmakers")
            ?? throw new InvalidOperationException("Beast Makers is missing from the level catalog.");
        if (!string.Equals(_currentLevel?.Key, targetLevel.Key, StringComparison.OrdinalIgnoreCase))
            await SelectLevelAsync(targetLevel);
        if (_currentLevel == null || _currentGeometry == null)
            throw new InvalidOperationException("Artisans did not load for the native-unreferenced art-only round-trip.");

        string relocationPath = NativeTerrainTextureRelocationEditStore.ManifestPath(_workspace.RootPath, targetLevel.Key);
        string terrainEditsPath = Path.Combine(_workspace.RootPath, $"{targetLevel.Key}-terrain-edits.json");
        string materialOverridesPath = Path.Combine(
            _workspace.RootPath,
            $"{targetLevel.Key}-terrain-material-overrides.json");
        if (File.Exists(relocationPath) || File.Exists(terrainEditsPath) || File.Exists(materialOverridesPath))
        {
            throw new InvalidOperationException(
                "The focused art-only round-trip requires an isolated Artisans workspace with no existing terrain edit files.");
        }

        await TerrainMaterialClassifier.SaveOverrideAsync(
            targetLevel.Key,
            _workspace.RootPath,
            targetTextureId,
            "stone");
        TerrainMaterialClassifier.Apply(targetLevel.Key, _workspace.RootPath, _currentGeometry);
        byte[] materialOverrideBefore = File.ReadAllBytes(materialOverridesPath);

        TerrainPolygon[] targetFaces = _currentGeometry.Polygons
            .Where(face => !face.IsTerrainRemoved && face.TextureId == targetTextureId)
            .ToArray();
        if (targetFaces.Length == 0)
            throw new InvalidOperationException("Artisans texture 54 has no target faces for the art-only round-trip.");
        string targetInvariantBefore = BuildNativeTargetInvariantSignature(targetFaces);
        TerrainPolygon selectedFace = targetFaces.First();
        _selectedTerrain = selectedFace;
        _selectedTerrainIndex = _currentGeometry.Polygons.IndexOf(selectedFace);

        TerrainCrossLevelLookChoice[] choices = BuildCrossLevelTerrainLookChoices(selectedFace.Surface).ToArray();
        TerrainCrossLevelLookChoice[] artOnlyChoices = choices
            .Where(choice => choice.DonorRecordRole == TerrainTextureRecordRole.NativeUnreferencedStatic)
            .ToArray();
        TerrainCrossLevelLookChoice[] animationSources = choices
            .Where(choice => choice.DonorRecordRole == TerrainTextureRecordRole.AnimationSourceDiagnostic)
            .ToArray();
        TerrainCrossLevelLookChoice[] controlledWithoutFace = choices
            .Where(choice => choice.DonorRecordRole == TerrainTextureRecordRole.ControlledDestinationWithoutFace)
            .ToArray();
        if (artOnlyChoices.Length != 19 || artOnlyChoices.Any(choice =>
                !choice.PreservesTargetNativeSurface ||
                !choice.CanApplyAtomically ||
                choice.NativeBehavior != null ||
                choice.BehaviorReadyFaceCount != 0))
        {
            throw new InvalidOperationException(
                $"The production catalog exposed {artOnlyChoices.Length}/19 exact preserve-target donors or invented donor behavior coverage: " +
                $"preserve {artOnlyChoices.Count(choice => choice.PreservesTargetNativeSurface)}, " +
                $"atomic {artOnlyChoices.Count(choice => choice.CanApplyAtomically)}, " +
                $"null behavior {artOnlyChoices.Count(choice => choice.NativeBehavior == null)}, " +
                $"zero behavior coverage {artOnlyChoices.Count(choice => choice.BehaviorReadyFaceCount == 0)}. " +
                string.Join(" | ", artOnlyChoices.Where(choice => !choice.CanApplyAtomically).Take(3).Select(choice =>
                    $"{choice.LevelKey}:T{choice.TextureId} {choice.AtomicBlockReason}")));
        }
        if (animationSources.Length != 22 || animationSources.Any(choice => choice.CanApplyAtomically) ||
            controlledWithoutFace.Length != 4 || controlledWithoutFace.Any(choice => choice.CanApplyAtomically))
        {
            throw new InvalidOperationException(
                $"The production catalog did not keep 22 animation-source and 4 controlled-without-face records blocked ({animationSources.Length}/{controlledWithoutFace.Length}).");
        }

        TerrainCrossLevelLookChoice choice = artOnlyChoices.Single(candidate =>
            string.Equals(candidate.LevelKey, donorLevel.Key, StringComparison.OrdinalIgnoreCase) &&
            candidate.TextureId == donorTextureId);
        string expectedProvenance = NativeTerrainTextureRelocationEditStore.BuildTextureRecordProvenanceKey(
            donorLevel.Key,
            donorTextureId);
        if (!string.Equals(choice.RuntimeKey, expectedProvenance, StringComparison.Ordinal))
            throw new InvalidOperationException("The art-only catalog choice did not use canonical texture-record provenance.");

        await ApplySelectedTerrainCrossLevelLookAsync(choice);
        if (!(_statusText.Text ?? "").Contains("art-only shared texture 54", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"The art-only texture swap did not stage successfully: {_statusText.Text}");

        NativeTerrainTextureRelocationEdit staged = NativeTerrainTextureRelocationEditStore
            .Load(_workspace.RootPath, targetLevel.Key)
            .Single();
        if (!staged.PreservesTargetNativeSurface ||
            staged.DonorWadEntry != donorLevel.SourceWadEntry ||
            staged.DonorTextureId != donorTextureId ||
            !string.Equals(staged.DonorProvenanceKey, expectedProvenance, StringComparison.Ordinal) ||
            !File.Exists(staged.PreviewImagePath))
        {
            throw new InvalidOperationException("The staged art-only relocation lost its apply mode, record provenance, or preview.");
        }
        if (File.Exists(terrainEditsPath) ||
            !File.ReadAllBytes(materialOverridesPath).SequenceEqual(materialOverrideBefore) ||
            BuildNativeTargetInvariantSignature(targetFaces) != targetInvariantBefore)
        {
            throw new InvalidOperationException("Art-only Apply mutated target terrain edits, material/tint/face state, or the material-override file.");
        }

        EditedLevelExportTarget discovered = FindEditedLevelExportTargets().Single(target =>
            string.Equals(target.Level.Key, targetLevel.Key, StringComparison.OrdinalIgnoreCase));
        if (!discovered.HasNativeTerrainTextureRelocations || discovered.HasTerrainEdits || discovered.HasCustomTerrainTextures)
            throw new InvalidOperationException("All Saved Edits did not classify the art-only manifest as a relocation-only edit.");

        string previewPath = staged.PreviewImagePath;
        await SelectLevelAsync(targetLevel);
        TerrainPolygon[] reloadedFaces = _currentGeometry?.Polygons
            .Where(face => !face.IsTerrainRemoved && face.TextureId == targetTextureId)
            .ToArray() ?? [];
        if (_nativeTerrainTextureRelocations.Count != 1 ||
            BuildNativeTargetInvariantSignature(reloadedFaces) != targetInvariantBefore ||
            reloadedFaces.Any(face => !face.SurfaceSource.Contains("native cross-level texture preview", StringComparison.OrdinalIgnoreCase)) ||
            File.Exists(terrainEditsPath) ||
            !File.ReadAllBytes(materialOverridesPath).SequenceEqual(materialOverrideBefore))
        {
            throw new InvalidOperationException("Reload did not restore only the art preview while preserving target material/tint/behavior state.");
        }

        _selectedTerrain = reloadedFaces.First();
        _selectedTerrainIndex = _currentGeometry!.Polygons.IndexOf(_selectedTerrain);
        await UndoSelectedTerrainAsync();
        if (_nativeTerrainTextureRelocations.Count != 0 ||
            File.Exists(relocationPath) ||
            File.Exists(previewPath) ||
            File.Exists(terrainEditsPath) ||
            !File.ReadAllBytes(materialOverridesPath).SequenceEqual(materialOverrideBefore) ||
            BuildNativeTargetInvariantSignature(reloadedFaces) != targetInvariantBefore)
        {
            throw new InvalidOperationException("Art-only Undo changed target material/tint/behavior state or left relocation/preview/terrain-edit state active.");
        }

        await SelectLevelAsync(targetLevel);
        TerrainPolygon[] finalFaces = _currentGeometry?.Polygons
            .Where(face => !face.IsTerrainRemoved && face.TextureId == targetTextureId)
            .ToArray() ?? [];
        if (_nativeTerrainTextureRelocations.Count != 0 ||
            BuildNativeTargetInvariantSignature(finalFaces) != targetInvariantBefore ||
            finalFaces.Any(face => face.SurfaceSource.Contains("native cross-level texture preview", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("A second reload resurrected art-only relocation state or changed target native state.");
        }

        File.Delete(materialOverridesPath);
        TerrainMaterialClassifier.Apply(targetLevel.Key, _workspace.RootPath, _currentGeometry!);
        return
            $"19/19 native-unreferenced static donors were art-only ready; 22 animation sources and 4 controlled face-less rows stayed blocked; " +
            $"Artisans texture {targetTextureId} <- Beast Makers record {donorTextureId} preserved target material/tint/collision state across Apply, reload, Undo, and reload";
    }

    private static string BuildNativeTargetInvariantSignature(IEnumerable<TerrainPolygon> faces)
    {
        static string Colors(IEnumerable<ColorRgba> colors) =>
            string.Join(",", colors.Select(color => $"{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}"));

        return string.Join("\n", faces
            .OrderBy(face => face.RuntimeKey, StringComparer.OrdinalIgnoreCase)
            .Select(face => string.Join("|",
                face.RuntimeKey,
                face.OriginalTextureId,
                face.TextureId,
                face.NativeFaceWord2.ToString("X8", CultureInfo.InvariantCulture),
                face.NativeMaterialByte.ToString("X2", CultureInfo.InvariantCulture),
                face.IsNativeUntexturedSentinel,
                face.NativePrimitiveSemiTransparent,
                face.NativeTextureId,
                Colors(face.NearColors),
                Colors(face.FarColors),
                face.Surface,
                face.Behavior,
                face.BehaviorSource,
                face.BehaviorConfidence,
                face.BehaviorNote,
                face.TextureVisualEdit?.ToString() ?? "none",
                face.SurfaceBehaviorEdit?.ToString() ?? "none")));
    }

    private static byte[]? ReadOptionalSmokeFile(string path) =>
        File.Exists(path) ? File.ReadAllBytes(path) : null;

    private static bool OptionalSmokeFileEquals(byte[]? before, string path)
    {
        byte[]? after = ReadOptionalSmokeFile(path);
        return before == null ? after == null : after != null && before.SequenceEqual(after);
    }

    private void DeleteManagedNativeTerrainTexturePreview(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        string managedRoot = Path.GetFullPath(Path.Combine(
            _workspace.RootPath,
            "_local",
            "custom-textures",
            "generated"));
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return;
        }

        string rootPrefix = Path.EndsInDirectorySeparator(managedRoot)
            ? managedRoot
            : managedRoot + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!fullPath.StartsWith(rootPrefix, comparison))
            return;

        try
        {
            File.Delete(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Preview cleanup is best effort; the saved relocation state is authoritative.
        }
    }

    private static void ApplyNativeTerrainBehaviorToFace(
        TerrainPolygon face,
        NativeTerrainSurfaceSignature signature,
        string sourceLevelKey,
        string sourceRuntimeKey)
    {
        face.ApplySurfaceBehaviorEdit(new TerrainSurfaceBehaviorEdit(
            signature.SurfaceType,
            signature.Param1,
            signature.Param2,
            sourceLevelKey,
            sourceRuntimeKey,
            signature.Label));
        face.SetBehavior(
            signature.Label,
            $"native collision flags copied from {sourceLevelKey}:{sourceRuntimeKey}",
            "exact",
            "Create BIN patches the selected face's matched native collision-triangle surface flags.");
    }

    private static void ApplyVisualOnlyNativeTerrainBehaviorToFace(
        TerrainPolygon face,
        NativeTerrainSurfaceSignature signature,
        string sourceLevelKey,
        string sourceRuntimeKey)
    {
        face.ClearSurfaceBehaviorEdit();
        face.SetBehavior(
            signature.Label,
            $"visual-only donor from {sourceLevelKey}:{sourceRuntimeKey}",
            "exact",
            "The selected visual face has no matching playable collision triangles, so no gameplay-property bytes are required.");
    }

    private sealed record NativeTerrainDonorVariant(
        NativeTerrainSurfaceSignature? Signature,
        IReadOnlyList<TerrainPolygon> Faces,
        string RepresentativeRuntimeKey,
        string BehaviorSummary,
        string ReadinessNote);

    private sealed record NativeTerrainFaceStageSnapshot(
        TerrainPolygon Face,
        TerrainSurfaceBehaviorEdit? SurfaceBehaviorEdit,
        TerrainTextureVisualEdit? TextureVisualEdit,
        string Surface,
        ColorRgba SurfaceColor,
        string SurfaceSource,
        string Behavior,
        string BehaviorSource,
        string BehaviorConfidence,
        string BehaviorNote)
    {
        public void Restore()
        {
            Face.SetSurface(Surface, SurfaceColor, SurfaceSource);
            Face.SetBehavior(Behavior, BehaviorSource, BehaviorConfidence, BehaviorNote);
            if (SurfaceBehaviorEdit == null)
                Face.ClearSurfaceBehaviorEdit();
            else
                Face.ApplySurfaceBehaviorEdit(SurfaceBehaviorEdit);
            if (TextureVisualEdit == null)
                Face.ClearTextureVisualEdit();
            else
                Face.ApplyTextureVisualEdit(TextureVisualEdit);
        }
    }
}
