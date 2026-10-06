using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.App.Views;

/// <summary>
/// "Tile Type…": what Spyro finds when he touches a tile (solid ground, water that hurts, lava, supercharge…).
/// The game keeps this in the collision triangles' surface flags, separately from the picture of the tile.
/// The choice is written by Create BIN through the same native surface-flag patch that texture painting
/// uses, so only the kinds this level already defines can be chosen.
/// </summary>
public sealed partial class MainWindow
{
    private Button? _terrainTileTypeButton;
    private NativeTerrainSurfaceLevelCatalog? _tileTypeCatalog;
    private string? _tileTypeCatalogLevelKey;
    private GeometryCandidate? _tileTypeCatalogGeometry;

    /// <summary>The kinds a player can tell apart; portal and level-linked surfaces are never offered.</summary>
    internal static bool IsSelectableTileType(NativeTerrainSurfaceSignature signature) =>
        signature.IsOrdinary || signature.SurfaceType is 0 or 1 or 4 or 5 or 7;

    /// <summary>Plain name of a surface kind, the way the player experiences it.</summary>
    internal static string PlainTileTypeName(NativeTerrainSurfaceSignature signature)
    {
        if (signature.IsOrdinary)
            return "Solid ground";
        return signature.SurfaceType switch
        {
            0 => (signature.Param2 switch { 0 => "Water", 1 => "Lava", 2 => "Ooze", _ => "Hazard floor" }) +
                 (signature.Param1 == 0 ? " (hurts Spyro)" : " (deadly)"),
            1 => "Damaging surface (hurts Spyro)",
            4 => "Supercharge ramp",
            5 => "Invisible wall",
            7 => "Electric floor (hurts when powered)",
            _ => signature.Label
        };
    }

    /// <summary>"Water (hurts Spyro)" → "water (hurts Spyro)": only the first letter changes.</summary>
    private static string LowerFirst(string text) => text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text[1..];

    /// <summary>Behaviour word for the viewport legend ("solid ground", "hurts Spyro", "special surface").</summary>
    private static string LegendBehaviorForTileType(NativeTerrainSurfaceSignature signature) =>
        signature.IsOrdinary ? "solid-proven" : signature.SurfaceType is 0 or 1 or 7 ? "damage-proven" : "state-response-proven";

    private NativeTerrainSurfaceLevelCatalog? TileTypeCatalog(out string error)
    {
        error = "";
        if (_currentLevel == null || _currentGeometry == null || _freshSceneDocument != null)
            return null;
        if (_tileTypeCatalog != null &&
            string.Equals(_tileTypeCatalogLevelKey, _currentLevel.Key, StringComparison.OrdinalIgnoreCase) &&
            ReferenceEquals(_tileTypeCatalogGeometry, _currentGeometry))
            return _tileTypeCatalog;
        _tileTypeCatalog = TryBuildNativeTerrainSurfaceCatalog(_currentLevel, _currentGeometry, out error);
        _tileTypeCatalogLevelKey = _currentLevel.Key;
        _tileTypeCatalogGeometry = _currentGeometry;
        return _tileTypeCatalog;
    }

    /// <summary>The kind a tile currently has: the staged choice when there is one, otherwise the original.</summary>
    private static NativeTerrainSurfaceSignature? CurrentTileType(TerrainPolygon face, NativeTerrainSurfaceLevelCatalog catalog)
    {
        if (face.SurfaceBehaviorEdit is { } staged)
            return new NativeTerrainSurfaceSignature(staged.SurfaceType, staged.Param1, staged.Param2);
        NativeTerrainFaceSurfaceBinding? binding = catalog.FindFace(face.RuntimeKey);
        return binding?.Signature;
    }

    /// <summary>Short text for the Tiles card: "solid ground", "water (hurts Spyro)"…, or null when unknown.</summary>
    private string? DescribeSelectedTileType(TerrainPolygon face)
    {
        NativeTerrainSurfaceLevelCatalog? catalog = _tileTypeCatalog;
        if (catalog == null || !ReferenceEquals(_tileTypeCatalogGeometry, _currentGeometry))
            return null;
        NativeTerrainSurfaceSignature? signature = CurrentTileType(face, catalog);
        if (signature == null)
            return null;
        return LowerFirst(PlainTileTypeName(signature));
    }

    private List<TerrainPolygon> SelectedTilesForTypeChange()
    {
        List<TerrainPolygon> faces = new();
        if (_currentGeometry == null)
            return faces;
        if (_viewport.MultiSelectedTerrainIndexes.Count >= 2)
        {
            foreach (int index in _viewport.MultiSelectedTerrainIndexes)
                if (index >= 0 && index < _currentGeometry.Polygons.Count)
                    faces.Add(_currentGeometry.Polygons[index]);
        }
        else if (_selectedTerrain != null)
            faces.Add(_selectedTerrain);
        return faces.Where(face => !face.IsTerrainRemoved && !face.IsTerrainAddClone).ToList();
    }

    private async Task ChangeSelectedTileTypeAsync()
    {
        if (_currentLevel == null || _currentGeometry == null)
        {
            _statusText.Text = "Open a level first.";
            return;
        }
        if (_freshSceneDocument != null)
        {
            _statusText.Text = "Tiles in a level of your own are solid ground; other kinds are not available there yet.";
            return;
        }
        List<TerrainPolygon> faces = SelectedTilesForTypeChange();
        if (faces.Count == 0)
        {
            _statusText.Text = "Select a tile first (⌘-click selects several).";
            return;
        }
        NativeTerrainSurfaceLevelCatalog? catalog = TileTypeCatalog(out string error);
        if (catalog == null)
        {
            _statusText.Text = string.IsNullOrWhiteSpace(error)
                ? "This level's ground kinds could not be read from the game disc."
                : $"This level's ground kinds could not be read from the game disc: {error}";
            return;
        }

        // Choices: solid ground plus every kind the level defines that a player can tell apart.
        List<NativeTerrainSurfaceSignature> choices = [NativeTerrainSurfaceSignature.Ordinary];
        foreach (PortalSpecialSurfaceRecord record in catalog.Source.SpecialSurfaces)
        {
            NativeTerrainSurfaceSignature signature = new(record.Type, record.Param1, record.Param2);
            if (IsSelectableTileType(signature) && !choices.Contains(signature))
                choices.Add(signature);
        }
        NativeTerrainSurfaceSignature? current = faces.Count == 1 ? CurrentTileType(faces[0], catalog) : null;

        Window dialog = new()
        {
            Title = "Tile Type",
            Name = "TileTypeDialog",
            Width = 440,
            Height = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        ListBox list = new() { Name = "TileTypeList", MaxHeight = 190 };
        foreach (NativeTerrainSurfaceSignature choice in choices)
            list.Items.Add(new ListBoxItem { Content = PlainTileTypeName(choice), Tag = choice });
        int currentIndex = current == null ? -1 : choices.IndexOf(current);
        list.SelectedIndex = currentIndex >= 0 ? currentIndex : 0;
        TextBlock intro = new()
        {
            TextWrapping = TextWrapping.Wrap,
            Text = faces.Count == 1
                ? $"What should Spyro find on this tile?{(current == null ? "" : $" It is {LowerFirst(PlainTileTypeName(current))} now.")}"
                : $"What should Spyro find on these {faces.Count} tiles?"
        };
        TextBlock note = new()
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(90, 99, 110)),
            Text = "This changes how the ground behaves, not how it looks; use Choose Texture… for the picture. " +
                   "Only kinds this level already has can be chosen."
        };
        Button apply = NewButton(faces.Count == 1 ? "Change tile" : $"Change {faces.Count} tiles", () => dialog.Close(true));
        apply.Name = "TileTypeApply";
        Button cancel = NewButton("Cancel", () => dialog.Close(false));
        StackPanel buttons = new() { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancel);
        buttons.Children.Add(apply);
        StackPanel content = new() { Spacing = 10, Margin = new Thickness(16) };
        content.Children.Add(intro);
        content.Children.Add(list);
        content.Children.Add(note);
        content.Children.Add(buttons);
        dialog.Content = content;
        bool accepted = await dialog.ShowDialog<bool>(this);
        if (!accepted || list.SelectedItem is not ListBoxItem { Tag: NativeTerrainSurfaceSignature chosen })
            return;
        await ApplyTileTypeAsync(faces, chosen, catalog);
    }

    /// <summary>Stages the kind on every selected tile the game can take it on, saves, and says what happened.</summary>
    private async Task ApplyTileTypeAsync(IReadOnlyList<TerrainPolygon> faces, NativeTerrainSurfaceSignature chosen, NativeTerrainSurfaceLevelCatalog catalog)
    {
        if (_currentLevel == null)
            return;
        int changed = 0, unchanged = 0;
        List<string> skipped = new();
        List<(TerrainPolygon Face, TerrainSurfaceBehaviorEdit? Before, string Behavior, string Source, string Confidence, string Note)> previous = new();
        foreach (TerrainPolygon face in faces)
        {
            NativeTerrainSurfaceSignature? current = CurrentTileType(face, catalog);
            if (current != null && current == chosen)
            {
                unchanged++;
                continue;
            }
            NativeTerrainSurfaceTransferReadiness readiness = catalog.EvaluateTransfer(face.RuntimeKey, chosen, crossLevel: false);
            if (!readiness.CanApply)
            {
                skipped.Add(readiness.Note.Contains("one exact native collision triangle", StringComparison.OrdinalIgnoreCase)
                    ? "the game's collision under it is cut differently from the tile, so its kind can't be changed on its own"
                    : readiness.Note);
                continue;
            }
            if (readiness.TargetTriangleCount == 0)
            {
                skipped.Add("Spyro never touches it (it has no collision), so its kind makes no difference");
                continue;
            }
            previous.Add((face, face.SurfaceBehaviorEdit, face.Behavior, face.BehaviorSource, face.BehaviorConfidence, face.BehaviorNote));
            NativeTerrainFaceSurfaceBinding? binding = catalog.FindFace(face.RuntimeKey);
            bool backToOriginal = binding?.Signature != null && binding.Signature == chosen;
            if (backToOriginal)
            {
                // The original kind needs no patch at all.
                face.ClearSurfaceBehaviorEdit();
            }
            else
            {
                ApplyNativeTerrainBehaviorToFace(face, chosen, _currentLevel.Key, face.RuntimeKey);
            }
            face.SetBehavior(
                LegendBehaviorForTileType(chosen),
                backToOriginal ? "original collision flags" : "tile type chosen in the editor",
                "exact",
                backToOriginal ? "" : $"Create BIN sets this tile's collision to {LowerFirst(PlainTileTypeName(chosen))}.");
            changed++;
        }

        if (changed > 0)
        {
            try
            {
                await PersistCurrentTerrainEditsAsync();
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
            {
                foreach ((TerrainPolygon face, TerrainSurfaceBehaviorEdit? before, string behavior, string source, string confidence, string note) in previous)
                {
                    if (before == null) face.ClearSurfaceBehaviorEdit(); else face.ApplySurfaceBehaviorEdit(before);
                    face.SetBehavior(behavior, source, confidence, note);
                }
                _statusText.Text = $"Could not save the tile type: {ex.Message} Nothing was changed.";
                return;
            }
            InvalidateBuildSafetySummary();
            RefreshCurrentLevelDetails();
            _viewport.NotifyTerrainPresentationDataChanged();
            if (_selectedTerrain != null)
                ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex, _selectedTerrain));
            RefreshTerrainReadinessHint();
            RefreshReleaseTerrainPanelTexts();
            EditorDiagnostics.RecordAction("Tile type changed", $"level {_currentLevel.Key}: {changed} tile(s) to {chosen.Label}; {skipped.Count} skipped");
        }

        string name = LowerFirst(PlainTileTypeName(chosen));
        string summary = changed switch
        {
            0 => "No tile was changed.",
            1 => $"Changed one tile to {name}; Create BIN will include it.",
            _ => $"Changed {changed} tiles to {name}; Create BIN will include them."
        };
        if (unchanged > 0)
            summary += $" {unchanged} {(unchanged == 1 ? "was" : "were")} already {name}.";
        if (skipped.Count > 0)
        {
            summary += skipped.Count == 1 && faces.Count == 1
                ? $" This tile can't be changed: {skipped[0]}."
                : $" {skipped.Count} {(skipped.Count == 1 ? "tile was" : "tiles were")} left alone: {skipped.Distinct().First()}.";
        }
        _statusText.Text = summary;
    }
}
