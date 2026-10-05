using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.App.Views;

/// <summary>
/// Terrain changes that the game's level format cannot hold are found by the same planner Create BIN uses.
/// This part turns such a refusal into something a user can act on: a plain sentence, the tiles involved,
/// and one-click "Show it" / "Undo that change" in a bar under the toolbar. The export checks, Create BIN,
/// Save and the object browser all report through here.
/// </summary>
public sealed partial class MainWindow
{
    private const string TerrainGeometryBuildSafetyCodePrefix = "terrain-shape-export.";

    internal sealed record TerrainExportProblem(
        LevelDefinition Level,
        string Summary,
        string TechnicalMessage,
        IReadOnlyList<string> FaceKeys,
        bool ShapeOnly);

    private Border? _terrainProblemBar;
    private TextBlock? _terrainProblemText;
    private Button? _terrainProblemShowButton;
    private Button? _terrainProblemUndoButton;
    private TerrainExportProblem? _terrainExportProblem;
    private int _terrainExportCheckGeneration;

    internal TerrainExportProblem? CurrentTerrainExportProblemForTesting => _terrainExportProblem;

    private static readonly Regex TerrainFaceKeyPattern = new(@"\b\d+:\d+:(?:hp|lp)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private Control BuildTerrainProblemBar()
    {
        _terrainProblemText = new TextBlock
        {
            Name = "TerrainProblemText",
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(110, 32, 32)),
            FontWeight = FontWeight.SemiBold
        };
        _terrainProblemShowButton = NewButton("Show it", ShowTerrainExportProblem);
        _terrainProblemShowButton.Name = "TerrainProblemShow";
        ToolTip.SetTip(_terrainProblemShowButton, "Open this level's Terrain tab and select the tiles involved.");
        _terrainProblemUndoButton = NewButton("Undo that change", UndoTerrainExportProblem);
        _terrainProblemUndoButton.Name = "TerrainProblemUndo";
        ToolTip.SetTip(_terrainProblemUndoButton, "Put only the tiles involved back the way the original level had them. Other terrain changes stay.");
        Button dismiss = NewButton("Hide", () => SetTerrainExportProblem(null));
        dismiss.Name = "TerrainProblemHide";
        Grid content = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };
        content.Children.Add(_terrainProblemText);
        Grid.SetColumn(_terrainProblemShowButton, 1);
        content.Children.Add(_terrainProblemShowButton);
        Grid.SetColumn(_terrainProblemUndoButton, 2);
        content.Children.Add(_terrainProblemUndoButton);
        Grid.SetColumn(dismiss, 3);
        content.Children.Add(dismiss);
        _terrainProblemBar = new Border
        {
            Name = "TerrainProblemBar",
            IsVisible = false,
            Background = new SolidColorBrush(Color.FromRgb(255, 240, 238)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 170, 164)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, 8),
            Child = content
        };
        return _terrainProblemBar;
    }

    private void SetTerrainExportProblem(TerrainExportProblem? problem)
    {
        _terrainExportProblem = problem;
        if (_terrainProblemBar == null || _terrainProblemText == null)
            return;
        _terrainProblemBar.IsVisible = problem != null;
        if (problem == null)
            return;
        int tiles = problem.FaceKeys.Count;
        _terrainProblemText.Text =
            $"{problem.Level.DisplayName}: {problem.Summary} Create BIN can't include this level's terrain until it is fixed." +
            (tiles > 0 ? $" ({tiles} tile{(tiles == 1 ? "" : "s")} involved.)" : "");
        if (_terrainProblemShowButton != null)
            _terrainProblemShowButton.IsVisible = tiles > 0;
        if (_terrainProblemUndoButton != null)
            _terrainProblemUndoButton.IsVisible = tiles > 0;
    }

    /// <summary>Plain-language description of a terrain planner refusal. The technical text stays in Details.</summary>
    internal static string DescribeTerrainExportRefusal(string technical, out bool shapeOnly)
    {
        string text = technical ?? "";
        bool Has(string value) => text.Contains(value, StringComparison.OrdinalIgnoreCase);
        shapeOnly = true;
        if (Has("flat or reverse") || Has("winding"))
            return "A corner was moved so far that the ground folds over itself, which the game can't stand on.";
        if (Has("vertically shadowed") || Has("vertically overlapping"))
            return "A changed tile sits directly above or below other ground, and the game can't tell the two apart.";
        if (Has("independent HP vertex slots") || Has("alias"))
            return "A changed corner is stored in several places in the level and they no longer agree.";
        if (Has("collision-cell") || Has("cell membership") || Has("collision cell"))
            return "A tile was moved out of the small area where the game looks for ground to stand on.";
        if (Has("relative") || Has("coordinate range") || Has("out of range") || Has("cannot be encoded") || Has("cannot encode"))
            return "A tile was moved further than the game's level format allows.";
        shapeOnly = false;
        if (Has("memory") || Has("capacity") || Has("no room") || Has("space") || Has("too large") || Has("exceed"))
            return "This level has no room left for one of the terrain changes.";
        if (Has("translucent") || Has("semi-transparent"))
            return "A see-through tile was changed in a way that isn't supported yet.";
        return "One terrain change can't be written into the game's level format.";
    }

    private static IReadOnlyList<string> TerrainFaceKeysIn(string message) =>
        TerrainFaceKeyPattern.Matches(message ?? "")
            .Select(match => match.Value.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static bool LooksLikeTerrainExportRefusal(Exception exception) =>
        exception is InvalidDataException or InvalidOperationException &&
        (TerrainFaceKeyPattern.IsMatch(exception.Message) ||
         exception.Message.Contains("terrain BIN", StringComparison.OrdinalIgnoreCase) ||
         exception.Message.Contains("terrain", StringComparison.OrdinalIgnoreCase) &&
         exception.Message.Contains("collision", StringComparison.OrdinalIgnoreCase));

    private TerrainExportProblem NewTerrainExportProblem(LevelDefinition level, string technical)
    {
        string summary = DescribeTerrainExportRefusal(technical, out bool shapeOnly);
        return new TerrainExportProblem(level, summary, technical, TerrainFaceKeysIn(technical), shapeOnly);
    }

    /// <summary>Reports a terrain refusal from any command: bar, status line and diagnostics.</summary>
    private void ReportTerrainExportProblem(LevelDefinition level, string technical, string during)
    {
        TerrainExportProblem problem = NewTerrainExportProblem(level, technical);
        SetTerrainExportProblem(problem);
        EditorDiagnostics.RecordAction("Terrain export problem", $"{during}; level {level.Key}; {technical}");
        _statusText.Text = $"{level.DisplayName}: {problem.Summary} Use Show it or Undo that change in the red bar. Details: {technical}";
    }

    private List<int> TerrainExportProblemIndexes(TerrainExportProblem problem)
    {
        if (_currentGeometry == null)
            return [];
        HashSet<string> keys = new(problem.FaceKeys, StringComparer.OrdinalIgnoreCase);
        return _currentGeometry.Polygons
            .Select((face, index) => (face, index))
            .Where(item => keys.Contains(item.face.RuntimeKey))
            .Select(item => item.index)
            .ToList();
    }

    private async Task<bool> EnsureTerrainProblemLevelLoadedAsync(TerrainExportProblem problem)
    {
        if (string.Equals(_currentLevel?.Key, problem.Level.Key, StringComparison.OrdinalIgnoreCase))
            return true;
        await SelectLevelFromPickerAsync(problem.Level);
        return string.Equals(_currentLevel?.Key, problem.Level.Key, StringComparison.OrdinalIgnoreCase);
    }

    private async void ShowTerrainExportProblem()
    {
        TerrainExportProblem? problem = _terrainExportProblem;
        if (problem == null)
            return;
        try
        {
            if (!await EnsureTerrainProblemLevelLoadedAsync(problem))
                return;
            List<int> indexes = TerrainExportProblemIndexes(problem).Where(index => !_currentGeometry!.Polygons[index].IsTerrainRemoved).ToList();
            if (indexes.Count == 0)
            {
                _statusText.Text = "The tiles involved are no longer in this level. Use Check for Export Issues to look again.";
                return;
            }
            SelectEditorToolTab(_modernTerrainWorkspaceTab);
            _viewport.FocusTerrain(indexes[0]);
            _viewport.SetTerrainMultiSelection(indexes);
            _statusText.Text = indexes.Count == 1
                ? "Selected the tile involved. Move its corners back, or choose Undo that change in the red bar."
                : $"Selected the {indexes.Count} tiles involved. Move their shared corner back, or choose Undo that change in the red bar.";
        }
        catch (Exception exception)
        {
            EditorDiagnostics.RecordException("showing a terrain export problem", exception);
            _statusText.Text = $"Could not show the terrain problem: {exception.Message}";
        }
    }

    private async void UndoTerrainExportProblem()
    {
        TerrainExportProblem? problem = _terrainExportProblem;
        if (problem == null)
            return;
        try
        {
            if (TryBlockEditorMutationDuringId65BlankLabManualOperation("undoing a terrain change"))
                return;
            if (!await EnsureTerrainProblemLevelLoadedAsync(problem))
                return;
            List<int> indexes = TerrainExportProblemIndexes(problem);
            if (indexes.Count == 0)
            {
                _statusText.Text = "The tiles involved are no longer in this level. Use Check for Export Issues to look again.";
                return;
            }
            ClearTerrainBrushUndoHistory();
            int restored = 0;
            foreach (int index in indexes)
            {
                TerrainPolygon face = _currentGeometry!.Polygons[index];
                if (problem.ShapeOnly)
                {
                    if (!face.HasPositionEdit && !face.HasHeightEdit)
                        continue;
                    face.ResetTerrainPositionEdit();
                    face.ResetTerrainHeightEdit();
                }
                else
                {
                    if (!face.IsTerrainEdited)
                        continue;
                    face.ResetTerrainEdit();
                }
                restored++;
            }
            _viewport.NotifyTerrainPresentationDataChanged();
            RefreshCurrentLevelDetails();
            RefreshTerrainReadinessHint();
            RefreshTerrainSurfaceQuickState();
            RefreshActionAvailability();
            SetTerrainExportProblem(null);
            SelectEditorToolTab(_modernTerrainWorkspaceTab);
            _viewport.FocusTerrain(indexes[0]);
            _viewport.SetTerrainMultiSelection(indexes);
            _statusText.Text = restored == 0
                ? "Those tiles were already back to their original shape. Save, then use Check for Export Issues to look again."
                : $"Put {restored} tile{(restored == 1 ? "" : "s")} back to {(problem.ShapeOnly ? "their original shape" : "the original")}. Other terrain changes are untouched. Save to keep this.";
        }
        catch (Exception exception)
        {
            EditorDiagnostics.RecordException("undoing a terrain export problem", exception);
            _statusText.Text = $"Could not undo the terrain change: {exception.Message}";
        }
    }

    /// <summary>
    /// Plans the saved terrain of one level exactly as Create BIN would, without writing an image.
    /// Returns null when the terrain exports, otherwise the refusal.
    /// </summary>
    private async Task<TerrainExportProblem?> CheckSavedTerrainExportAsync(LevelDefinition level, string sourceImage)
    {
        string scratch = Path.Combine(Path.GetTempPath(), "spyro-terrain-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            await TryPlanTerrainPatchAsync(
                level,
                sourceImage,
                DiscImageLocator.FindCueForImage(sourceImage),
                scratch,
                "terrain-check",
                writeImage: false);
            return null;
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or OverflowException or ArgumentException)
        {
            return NewTerrainExportProblem(level, exception.Message);
        }
        finally
        {
            try { Directory.Delete(scratch, recursive: true); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>Adds a blocking issue for every edited level whose saved terrain Create BIN would refuse.</summary>
    private async Task<MobyBuildSafetyReport> AddTerrainShapeBuildSafetyAsync(
        MobyBuildSafetyReport report,
        IReadOnlyList<EditedLevelExportTarget> targets,
        string sourceImage)
    {
        List<MobyBuildSafetyLevelReport> levels = report.Levels.ToList();
        TerrainExportProblem? firstProblem = null;
        foreach (EditedLevelExportTarget target in targets.Where(candidate => candidate.HasTerrainEdits))
        {
            TerrainExportProblem? problem;
            try
            {
                problem = await CheckSavedTerrainExportAsync(target.Level, sourceImage);
            }
            catch (IOException exception)
            {
                problem = new TerrainExportProblem(target.Level,
                    "The saved terrain for this level could not be read.", exception.Message, [], ShapeOnly: false);
            }
            if (problem == null)
                continue;
            firstProblem ??= problem;
            MobyBuildSafetyIssue issue = new(
                Code: TerrainGeometryBuildSafetyCodePrefix + "refused",
                Status: MobyBuildSafetyStatus.Blocked,
                Message: problem.Summary + " " + problem.TechnicalMessage,
                LevelKey: target.Level.Key,
                LevelName: target.Level.DisplayName,
                EditorTrueIndex: problem.FaceKeys.Count > 0 ? 0 : null,
                MobyLabel: problem.FaceKeys.FirstOrDefault() ?? "");
            int existingIndex = levels.FindIndex(level => string.Equals(
                LevelCatalog.NormalizeKey(level.LevelKey), LevelCatalog.NormalizeKey(target.Level.Key), StringComparison.OrdinalIgnoreCase));
            MobyBuildSafetyLevelReport existing = existingIndex >= 0
                ? levels[existingIndex]
                : CreateTerrainTextureBuildSafetyLevelReport(target.Level);
            MobyBuildSafetyLevelReport updated = existing with
            {
                Status = MobyBuildSafetyStatus.Blocked,
                Issues = existing.Issues.Append(issue).ToArray(),
                Findings = existing.Findings.Append("Create BIN would refuse this level's saved terrain: " + problem.TechnicalMessage).ToArray(),
                Recommendations = existing.Recommendations.Append("Use Show it or Undo that change in the red bar, save, then check again.").ToArray()
            };
            if (existingIndex >= 0) levels[existingIndex] = updated;
            else levels.Add(updated);
        }
        SetTerrainExportProblem(firstProblem);
        if (firstProblem == null)
            return report;
        return report with { Status = MobyBuildSafetyStatus.Blocked, Levels = levels };
    }

    private static bool IsTerrainShapeBuildSafetyIssue(MobyBuildSafetyIssue issue) =>
        issue.Code.StartsWith(TerrainGeometryBuildSafetyCodePrefix, StringComparison.Ordinal);

    /// <summary>After saving terrain, quietly confirm it still exports; a refusal raises the red bar at once.</summary>
    private void BeginTerrainExportCheckAfterSave(LevelDefinition level)
    {
        int generation = ++_terrainExportCheckGeneration;
        string sourceImage = FirstExistingDiscImagePath(
            _discImagePathBox.Text,
            _skyboxDiscImagePathBox.Text,
            DiscImageLocator.FindImage(_workspace));
        if (!File.Exists(sourceImage) || _freshSceneDocument != null)
            return;
        _ = RunAsync();

        async Task RunAsync()
        {
            try
            {
                TerrainExportProblem? problem = await CheckSavedTerrainExportAsync(level, sourceImage);
                if (generation != _terrainExportCheckGeneration)
                    return;
                if (problem != null)
                    ReportTerrainExportProblem(level, problem.TechnicalMessage, "check after save");
                else if (_terrainExportProblem != null &&
                         string.Equals(_terrainExportProblem.Level.Key, level.Key, StringComparison.OrdinalIgnoreCase))
                {
                    SetTerrainExportProblem(null);
                    _statusText.Text = $"{level.DisplayName}: the terrain problem is fixed. Create BIN can include this level again.";
                }
            }
            catch (Exception exception)
            {
                EditorDiagnostics.RecordException("checking saved terrain after save", exception);
            }
        }
    }
}
