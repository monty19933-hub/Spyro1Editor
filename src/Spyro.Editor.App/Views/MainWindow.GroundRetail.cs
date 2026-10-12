using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.App.Views;

/// <summary>The Ground card in one of the game's own levels (Artisans, Stone Hill…). Each tile clicked gets:
/// <list type="bullet">
/// <item>the ground's look through texture painting: the level's own texture when the ground comes from this level
/// (Grass in Artisans), otherwise the other level's texture as an art-only paint, which takes one of the level's spare
/// texture slots for that tile or, when there is none, asks before replacing the texture every tile sharing it shows;</item>
/// <item>the ground's kind through Tile Type… (solid ground for grass, dirt, stone, sand, snow and wood; water, lava and
/// ooze for the others). A kind the level has never had is copied in from an original level that has it when you
/// Create BIN (<see cref="Spyro.Editor.Core.Exporting.NativeTerrainSurfaceKindImport"/>).</item>
/// </list>
/// The kind is checked first, so a tile never ends up looking like lava without burning. The game's levels keep each
/// tile's colours in tables shared with its neighbours (most tiles have no colour of their own), so the lava shade cannot
/// be applied here: Lava paints a texture that is red-orange by itself (<see cref="GroundType.OriginalLevelKey"/>).</summary>
public sealed partial class MainWindow
{
    private string _activeGroundLevelKey = "";
    private bool _groundPaintBusy;

    /// <summary>The Ground card is offered in a built level and in any of the game's own levels (not the ID65 Lab).</summary>
    private bool GroundCardAvailable =>
        _currentGeometry != null && (_freshSceneDocument != null || (_currentLevel != null && !IsCurrentId65BlankLab()));

    private string CurrentGroundLevelKey =>
        _freshSceneDocument != null ? "built:" + _freshSceneDocument.TargetKey : _currentLevel?.Key ?? "";

    /// <summary>For tests: the answer the texture-scope question gets while the Ground card paints (null: ask).</summary>
    internal Func<int, bool, TerrainTexturePaintScopeChoice>? GroundScopeAnswerForTesting { get; set; }

    private async Task ChooseRetailGroundAsync(GroundType ground)
    {
        bool painting = _activeGround != null;
        StopTerrainTexturePaintMode(announce: false);
        List<TerrainPolygon> selected = painting ? [] : SelectedTilesForTypeChange();
        _activeGround = ground;
        _activeGroundLevelKey = CurrentGroundLevelKey;
        ResumeGroundPaintMode(ground);
        if (selected.Count > 0)
        {
            await PaintRetailGroundAsync(selected, ground);
            ClearGroundSelection();
        }
        else
            _statusText.Text = $"Painting {ground.Name.ToLowerInvariant()}: click tiles in the view. Esc stops.";
        RefreshGroundCard();
    }

    private void ResumeGroundPaintMode(GroundType ground)
    {
        if (_activeGround != ground)
            return;
        _viewport.TerrainBrushAction = TerrainBrushAction.Off;
        _viewport.ObjectPlacementMode = false;
        _viewport.TerrainTexturePaintMode = true;
        _viewport.TerrainTexturePaintHint = ground.Name.ToLowerInvariant();
    }

    /// <summary>Paints tiles of one of the game's levels with a ground (look and kind), saves, and says in plain words
    /// what happened, including why any tile was left alone.</summary>
    internal async Task PaintRetailGroundAsync(IReadOnlyList<TerrainPolygon> faces, GroundType ground)
    {
        if (_currentLevel == null || _currentGeometry == null || _freshSceneDocument != null)
            return;
        if (_groundPaintBusy)
        {
            _statusText.Text = "Still painting the last tile; click again in a moment.";
            return;
        }
        NativeTerrainSurfaceLevelCatalog? catalog = TileTypeCatalog(out string catalogError);
        if (catalog == null)
        {
            _statusText.Text = string.IsNullOrWhiteSpace(catalogError)
                ? "This level's ground kinds could not be read from the game disc, so it can't be painted."
                : $"This level's ground kinds could not be read from the game disc, so it can't be painted: {catalogError}";
            return;
        }

        _groundPaintBusy = true;
        string name = ground.Name.ToLowerInvariant();
        NativeTerrainSurfaceSignature kind = ground.Kind is { } k
            ? new NativeTerrainSurfaceSignature(k.Type, k.Param1, k.Param2)
            : NativeTerrainSurfaceSignature.Ordinary;
        int painted = 0, untouchable = 0, sharedLook = 0;
        List<string> problems = [];
        TerrainPolygon? previousSelection = _selectedTerrain;
        int previousSelectionIndex = _selectedTerrainIndex;
        try
        {
            foreach (TerrainPolygon face in faces)
            {
                (bool ok, bool noCollision, string? problem, int sharedWith) = await PaintRetailGroundTileAsync(face, ground, kind, catalog);
                if (ok) painted++;
                if (ok && noCollision) untouchable++;
                sharedLook = Math.Max(sharedLook, sharedWith);
                if (problem != null) problems.Add(problem);
            }
            if (painted > 0)
            {
                await PersistCurrentTerrainEditsAsync();
                InvalidateBuildSafetySummary();
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
        {
            EditorDiagnostics.RecordException("painting a ground on an original level", ex);
            problems.Add($"the change could not be saved ({ex.Message})");
        }
        finally
        {
            _groundPaintBusy = false;
            if (_activeTerrainTexturePaintBrush != null)
                StopTerrainTexturePaintMode(announce: false);
            ResumeGroundPaintMode(ground);
            _selectedTerrain = previousSelection is { } p && _currentGeometry.Polygons.Contains(p) ? p : null;
            _selectedTerrainIndex = _selectedTerrain == null ? -1 : previousSelectionIndex;
            TerrainMaterialClassifier.Apply(_currentLevel.Key, _workspace.RootPath, _currentGeometry);
            RefreshOwnTileTypeLabels();
            ApplyCustomTerrainTexturePreviews();
            _viewport.NotifyTerrainPresentationDataChanged();
            RefreshCurrentLevelDetails();
            RefreshTerrainReadinessHint();
            RefreshReleaseTerrainPanelTexts();
        }

        string tiles = painted == 1 ? "1 tile is" : $"{painted} tiles are";
        string summary = painted > 0
            ? $"{tiles} {name} now{(_activeGround == ground ? "; click more tiles to paint them, Esc stops" : "")}."
            : $"No tile was painted {name}.";
        if (painted > 0 && kind is { IsOrdinary: false } && !LevelHasSurfaceKind(catalog, kind) && FindTileTypeDonor(kind) is { } donor)
            summary += $" {_currentLevel.DisplayName} has no {name} of its own, so Create BIN copies it in from {donor.LevelName}.";
        if (sharedLook > 0)
            summary += $" The {sharedLook:N0} other tiles that share its texture took the {name} look too (this level had no spare texture slot); only the painted tiles are {name} underfoot.";
        if (untouchable > 0)
            summary += $" Spyro can't reach {(untouchable == 1 ? "one of them" : $"{untouchable} of them")} (no ground under it), so only the look changed there.";
        if (problems.Count > 0)
            summary += problems.Count == 1 && faces.Count == 1
                ? $" This tile can't be {name}: {problems[0]}."
                : $" {problems.Count} {(problems.Count == 1 ? "tile was" : "tiles were")} left alone: {problems.Distinct().First()}.";
        _statusText.Text = summary;
        EditorDiagnostics.RecordAction("Ground painted", $"level {_currentLevel.Key}: {painted}/{faces.Count} tile(s) {name}; {string.Join(" | ", problems.Distinct())}");
    }

    /// <summary>One tile: the kind is checked (and that the level's data can hold it) before anything changes, then
    /// the look is painted, then the kind is staged. The caller saves.</summary>
    private async Task<(bool Ok, bool NoCollision, string? Problem, int SharedWith)> PaintRetailGroundTileAsync(
        TerrainPolygon face,
        GroundType ground,
        NativeTerrainSurfaceSignature kind,
        NativeTerrainSurfaceLevelCatalog catalog)
    {
        if (_currentLevel == null || _currentGeometry == null)
            return (false, false, "no level is open", 0);
        if (face.IsTerrainRemoved)
            return (false, false, "it is removed (Undo Terrain brings it back)", 0);
        if (face.IsTerrainAddClone)
            return (false, false, "it is a tile you added, which takes the texture of the tile it was copied from", 0);
        if (face.TextureId < 0)
            return (false, false, "it has no texture to change", 0);

        // 1. The kind, before anything changes.
        string? kindBlock = RetailTileTypeBlockReason(face, kind, catalog, out bool kindAlready, out bool noCollision);
        if (kindBlock != null)
            return (false, false, kindBlock, 0);
        if (!kindAlready && !noCollision)
        {
            TerrainSurfaceBehaviorEdit? before = face.SurfaceBehaviorEdit;
            StageRetailTileType(face, kind, catalog);
            bool fits = RetailTileTypesFit(catalog, out string fitReason);
            if (before == null) face.ClearSurfaceBehaviorEdit(); else face.ApplySurfaceBehaviorEdit(before);
            if (!fits)
                return (false, false, fitReason, 0);
        }

        // 2. The look.
        int sharedWith = 0;
        if (!GroundTextureShown(face, ground))
        {
            string? textureProblem = await PaintRetailGroundTextureAsync(face, ground);
            if (textureProblem != null)
                return (false, false, textureProblem, 0);
            // No spare slot: the texture every tile sharing it shows was replaced (after the question).
            if (face.TextureId == face.OriginalTextureId && _currentGeometry != null)
                sharedWith = _currentGeometry.Polygons.Count(other => !other.IsTerrainRemoved && other != face && other.TextureId == face.TextureId);
        }

        // 3. The kind (repainting the look may have put the tile's old kind back to the original).
        NativeTerrainSurfaceSignature? current = CurrentTileType(face, catalog);
        if (!noCollision && current != kind)
            StageRetailTileType(face, kind, catalog);
        return (true, noCollision, null, sharedWith);
    }

    /// <summary>The material classifier relabels every tile from its texture; tiles given a kind of their own keep
    /// saying what Spyro finds there (the legend and the Tiles card read this label).</summary>
    private void RefreshOwnTileTypeLabels()
    {
        if (_currentGeometry == null)
            return;
        foreach (TerrainPolygon face in _currentGeometry.Polygons)
        {
            if (face.SurfaceBehaviorEdit is not { } edit || !IsOwnTileTypeEdit(face))
                continue;
            NativeTerrainSurfaceSignature kind = new(edit.SurfaceType, edit.Param1, edit.Param2);
            face.SetBehavior(LegendBehaviorForTileType(kind), "tile type chosen in the editor", "exact",
                $"Create BIN sets this tile's collision to {LowerFirst(PlainTileTypeName(kind))}.");
        }
    }

    /// <summary>The ground a tile of one of the game's levels shows (its texture is the ground's), or null.</summary>
    private GroundType? GroundShownBy(TerrainPolygon face) =>
        _freshSceneDocument == null && _currentLevel != null && face.TextureId >= 0
            ? GroundTypes.All.FirstOrDefault(ground => GroundTextureShown(face, ground))
            : null;

    /// <summary>The tile already shows the ground's texture.</summary>
    private bool GroundTextureShown(TerrainPolygon face, GroundType ground)
    {
        if (_currentLevel == null)
            return false;
        if (IsGroundFromCurrentLevel(ground))
            return face.TextureId == ground.OriginalLevelTextureId && !_nativeTerrainTextureRelocations.Any(edit => edit.TargetTextureId == face.TextureId);
        return _nativeTerrainTextureRelocations.Any(edit =>
            edit.TargetTextureId == face.TextureId &&
            edit.DonorTextureId == ground.OriginalLevelTextureId &&
            string.Equals(LevelCatalog.NormalizeKey(edit.DonorLevelKey), LevelCatalog.NormalizeKey(ground.OriginalLevelKey), StringComparison.OrdinalIgnoreCase));
    }

    private bool IsGroundFromCurrentLevel(GroundType ground) =>
        _currentLevel != null &&
        string.Equals(LevelCatalog.NormalizeKey(ground.OriginalLevelKey), LevelCatalog.NormalizeKey(_currentLevel.Key), StringComparison.OrdinalIgnoreCase);

    /// <summary>Paints the ground's texture on one tile with the existing texture painting (its slot limits, its question
    /// and its checks); null when the tile shows it now, otherwise why not in plain words.</summary>
    private async Task<string?> PaintRetailGroundTextureAsync(TerrainPolygon face, GroundType ground)
    {
        if (_currentLevel == null || _currentGeometry == null)
            return "no level is open";
        string name = ground.Name.ToLowerInvariant();
        int faceIndex = _currentGeometry.Polygons.IndexOf(face);
        if (faceIndex < 0)
            return "it is no longer in the level";

        // A tile painted before goes back to its own look first; its old kind goes with it and is staged again after.
        if (face.SurfaceBehaviorEdit != null && !IsOwnTileTypeEdit(face))
            return "it has another texture change that brought its own kind; use Undo Terrain on it first";
        if (face.HasTextureVisualEdit)
            return "it has another texture change; use Undo Terrain on it first";
        if (face.HasTextureEdit)
        {
            bool privatePaint = _nativeTerrainTextureRelocations.Any(edit => edit.TargetTextureId == face.TextureId) &&
                face.OriginalTextureId >= 0 && face.OriginalTextureId != face.TextureId;
            if (privatePaint)
            {
                _selectedTerrain = face;
                _selectedTerrainIndex = faceIndex;
                _selectedTerrainPointIndex = -1;
                await UndoSelectedTerrainAsync();
                if (face.HasTextureEdit)
                    return "its earlier paint could not be taken off";
            }
            else
                face.ApplyTextureOverride(face.OriginalTextureId);
        }
        face.ClearSurfaceBehaviorEdit();
        if (GroundTextureShown(face, ground))
            return null;

        _selectedTerrain = face;
        _selectedTerrainIndex = faceIndex;
        _selectedTerrainPointIndex = -1;
        if (IsGroundFromCurrentLevel(ground))
        {
            // The level's own texture: only the tile's texture number changes.
            TerrainTextureSwapChoice? choice = BuildTerrainTextureSwapChoices()
                .Where(c => c.TextureId == ground.OriginalLevelTextureId && !c.RequiresPrivateCopy)
                .OrderByDescending(c => c.CanUseArt)
                .FirstOrDefault();
            if (choice == null)
                return $"this level's {name} texture could not be read";
            if (!choice.CanUseArt)
            {
                EditorDiagnostics.RecordWarning("Ground texture refused", $"{face.RuntimeKey}: {choice.ArtReadinessNote}");
                return choice.ArtReadinessNote.Contains("shared replacement", StringComparison.OrdinalIgnoreCase)
                    ? $"its {name} texture has been replaced by another level's texture here; Undo Terrain on a tile showing it first"
                    : $"the game's data can't show the {name} texture on it";
            }
            return await ApplySelectedTerrainResidentArtOnlyAsync(choice, face) ? null : PlainGroundTextureProblem(_statusText.Text ?? "", name);
        }

        LevelDefinition? donor = _catalog.FindByKey(ground.OriginalLevelKey);
        if (donor == null)
            return $"the level the {name} texture comes from is not in the level list";
        if (FindCachedTerrainOverlayPath(donor.Key) == null &&
            !TryPrepareSourceDerivedGeometryOverlay(donor.Key, SourceDerivedTerrainOverlayPath(donor.Key), out string overlayMessage))
        {
            EditorDiagnostics.RecordWarning("Ground donor map", $"{donor.Key}: {overlayMessage}");
            return $"{donor.DisplayName}'s map could not be read from the game disc";
        }
        TerrainTexturePaintBrush brush = new(
            TerrainTexturePaintSourceKind.CrossLevel,
            _currentLevel.Key,
            donor.Key,
            donor.DisplayName,
            ground.OriginalLevelTextureId,
            Spyro.Editor.Core.Editing.NativeTerrainTextureRelocationEditStore.BuildTextureRecordProvenanceKey(donor.Key, ground.OriginalLevelTextureId),
            "",
            TerrainTextureSurfacePropertyMode.PreserveTargetNativeSurface,
            ground.Shade,
            name);
        Func<string, int, int, bool, string, Task<TerrainTexturePaintScopeChoice>>? previousScope = TerrainTexturePaintScopeConfirmationForTesting;
        if (GroundScopeAnswerForTesting is { } answer)
            TerrainTexturePaintScopeConfirmationForTesting = (_, _, count, canSelected, _) => Task.FromResult(answer(count, canSelected));
        try
        {
            StartTerrainTexturePaintMode(brush);
            await ApplyTerrainTexturePaintBrushAsync(faceIndex, face);
        }
        finally
        {
            TerrainTexturePaintScopeConfirmationForTesting = previousScope;
            StopTerrainTexturePaintMode(announce: false);
            if (_activeGround != null)
                ResumeGroundPaintMode(_activeGround);
        }
        return GroundTextureShown(face, ground) ? null : PlainGroundTextureProblem(_statusText.Text ?? "", name);
    }

    /// <summary>The texture painting's own (technical) reason, said plainly; the full text goes to the diagnostics log.</summary>
    private static string PlainGroundTextureProblem(string status, string name)
    {
        EditorDiagnostics.RecordWarning("Ground texture not painted", status);
        if (status.Contains("Canceled", StringComparison.OrdinalIgnoreCase) || status.Contains("cancelled", StringComparison.OrdinalIgnoreCase))
            return "you chose Cancel";
        if (status.Contains("no longer available", StringComparison.OrdinalIgnoreCase) || status.Contains("choose the retail", StringComparison.OrdinalIgnoreCase))
            return $"the {name} texture could not be read from the game disc";
        if (status.Contains("private", StringComparison.OrdinalIgnoreCase) || status.Contains("slot", StringComparison.OrdinalIgnoreCase) ||
            status.Contains("record", StringComparison.OrdinalIgnoreCase) || status.Contains("shared", StringComparison.OrdinalIgnoreCase))
            return $"this level has no free texture slot for {name} on that tile, and the texture it shares with other tiles can't be replaced safely";
        return $"the game's data for that tile can't take the {name} texture safely";
    }
}
