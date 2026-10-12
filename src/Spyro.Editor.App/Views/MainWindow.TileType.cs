using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.App.Views;

/// <summary>
/// "Tile Type…": what Spyro finds when he touches a tile (solid ground, water or lava that cost a life, supercharge…).
/// The game keeps this in the collision triangles' surface flags, separately from the picture of the tile.
/// The choice is written by Create BIN through the same native surface-flag patch that texture painting uses. Besides
/// the kinds the level defines, an original level is offered every kind the game handles in any level (water, lava,
/// ooze, supercharge, invisible wall): Create BIN copies the missing record in from an original level that has it
/// (<see cref="NativeTerrainSurfaceKindImport"/>), as far as the level's spare data bytes allow.
/// </summary>
public sealed partial class MainWindow
{
    private Button? _terrainTileTypeButton;
    private NativeTerrainSurfaceLevelCatalog? _tileTypeCatalog;
    private string? _tileTypeCatalogLevelKey;
    private GeometryCandidate? _tileTypeCatalogGeometry;
    private readonly Dictionary<NativeTerrainSurfaceSignature, NativeTerrainSurfaceDescriptorDonor?> _tileTypeDonors = new();
    private string _tileTypeDonorImage = "";

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
                 // Played: Spyro sinks and loses a life either way (Spyro cannot swim); with 0 the first touch costs a
                 // Sparx level and the life goes about three seconds later, with 1 at once.
                 (signature.Param1 == 0 ? " (Spyro sinks, costs a life)" : " (costs a life at once)"),
            1 => "Damaging surface (hurts Spyro)",
            4 => "Supercharge ramp",
            5 => "Invisible wall",
            7 => "Electric floor (hurts when powered)",
            _ => signature.Label
        };
    }

    /// <summary>"Water (Spyro sinks, costs a life)" → "water (…)": only the first letter changes.</summary>
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

    /// <summary>Short text for the Tiles card: "solid ground", "water (Spyro sinks, costs a life)"…, or null when unknown.</summary>
    private string? DescribeSelectedTileType(TerrainPolygon face)
    {
        if (_freshSceneDocument != null)
            return LowerFirst(PlainTileTypeName(face.SurfaceBehaviorEdit is { } built
                ? new NativeTerrainSurfaceSignature(built.SurfaceType, built.Param1, built.Param2) : NativeTerrainSurfaceSignature.Ordinary));
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
        List<TerrainPolygon> faces = SelectedTilesForTypeChange();
        if (faces.Count == 0)
        {
            _statusText.Text = "Select a tile first (⌘-click selects several).";
            return;
        }
        if (_freshSceneDocument != null)
        {
            await ChangeBuiltTileTypeAsync(faces);
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

        List<NativeTerrainSurfaceSignature> choices = RetailTileTypeChoices(catalog);
        NativeTerrainSurfaceSignature? current = faces.Count == 1 ? CurrentTileType(faces[0], catalog) : null;

        Window dialog = new()
        {
            Title = "Tile Type",
            Name = "TileTypeDialog",
            Width = 440,
            SizeToContent = SizeToContent.Height,
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
            Text = "This changes how the ground behaves, not how it looks; use Choose Texture… or the Ground card for the picture. " +
                   "A kind this level doesn't have yet is copied in from an original level that has it when you Create BIN. " +
                   "In Spyro the Dragon water and lava cost a life: Spyro cannot swim."
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

    /// <summary>A level you built has no ground kinds of its own: it offers the kinds the game itself handles in any
    /// level. Create BIN writes them into the level's surface table (see AuthoredSurfaceKind).</summary>
    private async Task ChangeBuiltTileTypeAsync(IReadOnlyList<TerrainPolygon> faces)
    {
        List<NativeTerrainSurfaceSignature> choices = [NativeTerrainSurfaceSignature.Ordinary,
            .. Spyro.Editor.Core.Editing.AuthoredSurfaceKind.All.Select(k => new NativeTerrainSurfaceSignature(k.Type, k.Param1, k.Param2))];
        NativeTerrainSurfaceSignature Current(TerrainPolygon face) => face.SurfaceBehaviorEdit is { } e
            ? new(e.SurfaceType, e.Param1, e.Param2) : NativeTerrainSurfaceSignature.Ordinary;
        NativeTerrainSurfaceSignature? current = faces.Select(Current).Distinct().Count() == 1 ? Current(faces[0]) : null;
        Window dialog = new()
        {
            Title = "Tile Type", Name = "TileTypeDialog", Width = 440, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = true
        };
        ListBox list = new() { Name = "TileTypeList", MaxHeight = 220 };
        foreach (NativeTerrainSurfaceSignature choice in choices)
            list.Items.Add(new ListBoxItem { Content = PlainTileTypeName(choice), Tag = choice });
        list.SelectedIndex = current == null ? 0 : Math.Max(0, choices.IndexOf(current));
        TextBlock intro = new()
        {
            TextWrapping = TextWrapping.Wrap,
            Text = faces.Count == 1 ? "What should Spyro find on this tile?" : $"What should Spyro find on these {faces.Count} tiles?"
        };
        TextBlock note = new()
        {
            TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(90, 99, 110)),
            Text = "This changes how the ground behaves, not how it looks. In Spyro the Dragon water and lava cost a life: Spyro cannot swim."
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
        if (!await dialog.ShowDialog<bool>(this) || list.SelectedItem is not ListBoxItem { Tag: NativeTerrainSurfaceSignature chosen })
            return;
        ApplyBuiltTileType(faces, chosen);
        if (await SaveFreshSceneAsync())
            _statusText.Text = faces.Count == 1 ? $"This tile is {LowerFirst(PlainTileTypeName(chosen))} now." : $"These {faces.Count} tiles are {LowerFirst(PlainTileTypeName(chosen))} now.";
    }

    /// <summary>Sets a built level's tiles to a ground kind (or back to solid ground) and updates the view.</summary>
    internal void ApplyBuiltTileType(IReadOnlyList<TerrainPolygon> faces, NativeTerrainSurfaceSignature chosen)
    {
        foreach (TerrainPolygon face in faces)
        {
            if (chosen.IsOrdinary) face.ClearSurfaceBehaviorEdit();
            else face.ApplySurfaceBehaviorEdit(new TerrainSurfaceBehaviorEdit(chosen.SurfaceType, chosen.Param1, chosen.Param2, "built", face.RuntimeKey, chosen.Label));
            face.SetBehavior(LegendBehaviorForTileType(chosen), "tile type chosen in the editor", "exact",
                chosen.IsOrdinary ? "" : $"Create BIN makes this tile {LowerFirst(PlainTileTypeName(chosen))}.");
        }
        _viewport.NotifyTerrainPresentationDataChanged();
        RefreshCurrentLevelDetails();
    }

    /// <summary>Solid ground, the kinds the level defines that a player can tell apart, then every kind the game handles
    /// in any level (the level gets a copy of a missing one at Create BIN).</summary>
    private static List<NativeTerrainSurfaceSignature> RetailTileTypeChoices(NativeTerrainSurfaceLevelCatalog catalog)
    {
        List<NativeTerrainSurfaceSignature> choices = [NativeTerrainSurfaceSignature.Ordinary];
        foreach (PortalSpecialSurfaceRecord record in catalog.Source.SpecialSurfaces)
        {
            NativeTerrainSurfaceSignature signature = new(record.Type, record.Param1, record.Param2);
            if (IsSelectableTileType(signature) && !choices.Contains(signature))
                choices.Add(signature);
        }
        foreach (NativeTerrainSurfaceSignature signature in NativeTerrainSurfaceKindImport.ImportableKinds)
            if (!choices.Contains(signature))
                choices.Add(signature);
        return choices;
    }

    /// <summary>The original level whose record a kind this level lacks is copied from (found once per disc).</summary>
    private NativeTerrainSurfaceDescriptorDonor? FindTileTypeDonor(NativeTerrainSurfaceSignature signature)
    {
        if (_currentLevel == null)
            return null;
        string image = ResolveTerrainTextureSourceImage(_currentLevel);
        if (!string.Equals(image, _tileTypeDonorImage, StringComparison.OrdinalIgnoreCase))
        {
            _tileTypeDonors.Clear();
            _tileTypeDonorImage = image;
        }
        if (_tileTypeDonors.TryGetValue(signature, out NativeTerrainSurfaceDescriptorDonor? cached))
            return cached;
        NativeTerrainSurfaceDescriptorDonor? donor = NativeTerrainSurfaceKindImport.FindDonor(
            signature,
            LevelRealmCatalog.OrderLevels(_retailCatalog.Levels).Where(level => !IsId65BlankLabKey(level.Key)),
            level => TryLocateNativeTerrainSurfaceSource(level, out _));
        _tileTypeDonors[signature] = donor;
        return donor;
    }

    private static bool LevelHasSurfaceKind(NativeTerrainSurfaceLevelCatalog catalog, NativeTerrainSurfaceSignature signature) =>
        signature.IsOrdinary || catalog.Source.SpecialSurfaces.Any(record =>
            record.Type == signature.SurfaceType && record.Param1 == signature.Param1 && record.Param2 == signature.Param2);

    /// <summary>Why a tile cannot take a kind, in plain words; null when it can (or already has it, or Spyro never
    /// touches it: <paramref name="untouchable"/>).</summary>
    private string? RetailTileTypeBlockReason(
        TerrainPolygon face,
        NativeTerrainSurfaceSignature chosen,
        NativeTerrainSurfaceLevelCatalog catalog,
        out bool alreadyThere,
        out bool untouchable)
    {
        alreadyThere = false;
        untouchable = false;
        NativeTerrainSurfaceSignature? current = CurrentTileType(face, catalog);
        if (current != null && current == chosen)
        {
            alreadyThere = true;
            return null;
        }
        NativeTerrainFaceSurfaceBinding? binding = catalog.FindFace(face.RuntimeKey);
        if (binding is { MatchedVisualTriangleCount: 0, HasAnyCollisionCandidate: false })
        {
            untouchable = true;
            return null;
        }
        if (!chosen.IsOrdinary && !LevelHasSurfaceKind(catalog, chosen) && FindTileTypeDonor(chosen) == null)
            return $"no original level has {LowerFirst(PlainTileTypeName(chosen))} to copy";
        NativeTerrainSurfaceTransferReadiness readiness = catalog.EvaluateTransfer(face.RuntimeKey, chosen, crossLevel: false, allowDescriptorImport: true);
        if (!readiness.CanApply)
        {
            return readiness.Note.Contains("one exact native collision triangle", StringComparison.OrdinalIgnoreCase)
                ? "the game's collision under it is cut differently from the tile, so its kind can't be changed on its own"
                : readiness.Note.Contains("promotion slot", StringComparison.OrdinalIgnoreCase)
                    ? $"{catalog.Level.DisplayName} has no room left in its data for more special tiles"
                    : readiness.Note;
        }
        if (readiness.TargetTriangleCount == 0)
            untouchable = true;
        return null;
    }

    /// <summary>Gives one tile a kind in memory (the caller saves). A kind the level lacks names its donor record.</summary>
    private void StageRetailTileType(TerrainPolygon face, NativeTerrainSurfaceSignature chosen, NativeTerrainSurfaceLevelCatalog catalog)
    {
        if (_currentLevel == null)
            return;
        NativeTerrainFaceSurfaceBinding? binding = catalog.FindFace(face.RuntimeKey);
        bool backToOriginal = (binding?.Signature ?? NativeTerrainSurfaceSignature.Ordinary) == chosen ||
            (chosen.IsOrdinary && binding is { MatchedVisualTriangleCount: 0, HasAnyCollisionCandidate: false });
        if (backToOriginal)
        {
            // The original kind needs no patch at all.
            face.ClearSurfaceBehaviorEdit();
        }
        else
        {
            NativeTerrainSurfaceDescriptorDonor? donor = LevelHasSurfaceKind(catalog, chosen) ? null : FindTileTypeDonor(chosen);
            face.ApplySurfaceBehaviorEdit(new TerrainSurfaceBehaviorEdit(
                chosen.SurfaceType, chosen.Param1, chosen.Param2, _currentLevel.Key, face.RuntimeKey, chosen.Label)
            {
                DescriptorDonorLevelKey = donor?.LevelKey ?? "",
                DescriptorDonorWadEntry = donor?.WadEntry ?? -1,
                DescriptorDonorIndex = donor?.Index ?? -1
            });
        }
        face.SetBehavior(
            LegendBehaviorForTileType(chosen),
            backToOriginal ? "original collision flags" : "tile type chosen in the editor",
            "exact",
            backToOriginal ? "" : $"Create BIN sets this tile's collision to {LowerFirst(PlainTileTypeName(chosen))}.");
    }

    /// <summary>Composes every kind staged in this level the way Create BIN will (the level's spare bytes hold the copied
    /// records and the extra flags); false with a plain reason when they don't fit.</summary>
    private bool RetailTileTypesFit(NativeTerrainSurfaceLevelCatalog catalog, out string reason)
    {
        reason = "";
        if (_currentLevel == null || _currentGeometry == null)
            return true;
        List<NativeTerrainSurfaceBehaviorAssignment> assignments = [];
        foreach (TerrainPolygon face in _currentGeometry.Polygons)
        {
            if (face.IsTerrainRemoved || face.SurfaceBehaviorEdit is not { } edit)
                continue;
            NativeTerrainSurfaceSignature signature = new(edit.SurfaceType, edit.Param1, edit.Param2);
            NativeTerrainFaceSurfaceBinding? binding = catalog.FindFace(face.RuntimeKey);
            if (binding is not { HasExactTriangleMapping: true })
                continue;
            NativeTerrainSurfaceDescriptorImport? donor = LevelHasSurfaceKind(catalog, signature) ? null : FindTileTypeDonor(signature)?.ToImport();
            foreach (NativeCollisionSurfaceTriangle triangle in binding.NativeTriangles)
            {
                if (signature.IsOrdinary && triangle.FlagWadOffset < 0)
                    continue;
                assignments.Add(new NativeTerrainSurfaceBehaviorAssignment(triangle.TriangleIndex, signature, donor, face.RuntimeKey, signature.Label));
            }
        }
        if (assignments.Count == 0)
            return true;
        try
        {
            byte[] levelData = PortalSourceDataLocator.ReadTerrainLevelData(ResolveTerrainTextureSourceImage(_currentLevel), catalog.Source);
            if (NativeTerrainSurfaceLayoutComposer.TryBuild(levelData, catalog.Source, assignments, [], out _, out string composeReason))
                return true;
            EditorDiagnostics.RecordWarning("Tile kinds do not fit", $"{_currentLevel.Key}: {composeReason}");
            reason = composeReason.Contains("zero-tail", StringComparison.OrdinalIgnoreCase) || composeReason.Contains("index 63", StringComparison.OrdinalIgnoreCase)
                ? $"{_currentLevel.DisplayName} has no room left in its data for that many special tiles"
                : $"the game data could not take it ({composeReason})";
            return false;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
        {
            reason = $"the game disc could not be read ({ex.Message})";
            return false;
        }
    }

    /// <summary>Stages the kind on every selected tile the game can take it on, saves, and says what happened.</summary>
    private async Task ApplyTileTypeAsync(IReadOnlyList<TerrainPolygon> faces, NativeTerrainSurfaceSignature chosen, NativeTerrainSurfaceLevelCatalog catalog)
    {
        if (_currentLevel == null)
            return;
        int changed = 0, unchanged = 0;
        List<string> skipped = new();
        List<(TerrainPolygon Face, TerrainSurfaceBehaviorEdit? Before, string Behavior, string Source, string Confidence, string Note)> previous = new();
        void Rollback()
        {
            foreach ((TerrainPolygon face, TerrainSurfaceBehaviorEdit? before, string behavior, string source, string confidence, string note) in previous)
            {
                if (before == null) face.ClearSurfaceBehaviorEdit(); else face.ApplySurfaceBehaviorEdit(before);
                face.SetBehavior(behavior, source, confidence, note);
            }
        }
        foreach (TerrainPolygon face in faces)
        {
            string? blocked = RetailTileTypeBlockReason(face, chosen, catalog, out bool alreadyThere, out bool untouchable);
            if (alreadyThere)
            {
                unchanged++;
                continue;
            }
            if (blocked != null)
            {
                skipped.Add(blocked);
                continue;
            }
            if (untouchable)
            {
                skipped.Add("Spyro never touches it (it has no collision), so its kind makes no difference");
                continue;
            }
            previous.Add((face, face.SurfaceBehaviorEdit, face.Behavior, face.BehaviorSource, face.BehaviorConfidence, face.BehaviorNote));
            StageRetailTileType(face, chosen, catalog);
            changed++;
        }

        if (changed > 0 && !RetailTileTypesFit(catalog, out string fitReason))
        {
            Rollback();
            _statusText.Text = $"No tile was changed: {fitReason}.";
            return;
        }
        if (changed > 0)
        {
            try
            {
                await PersistCurrentTerrainEditsAsync();
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
            {
                Rollback();
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
        if (changed > 0 && !LevelHasSurfaceKind(catalog, chosen) && FindTileTypeDonor(chosen) is { } donor)
            summary += $" {_currentLevel.DisplayName} has no {name.Split(" (")[0]} of its own, so Create BIN copies it in from {donor.LevelName}.";
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
