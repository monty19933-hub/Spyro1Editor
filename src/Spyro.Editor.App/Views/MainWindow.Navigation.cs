using Avalonia.Controls;
using Avalonia.Input;
using Spyro.Editor.Core.Diagnostics;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private Button? _toolbarSaveButton;
    private Button? _toolbarFileMenu;
    private bool _saveCommandRunning;
    private readonly TextBlock _visibleStatusText = new() { Name = "EditorStatusSummary" };

    // A command menu must activate on click/Enter, never on highlight while navigating.
    private Button BuildActionMenu(string label, string name, params ToolbarAction[] actions)
    {
        var menu = new MenuFlyout();
        foreach (ToolbarAction action in actions)
        {
            var item = new MenuItem { Header = action.Label };
            item.Click += async (_, _) =>
            {
                try { await action.Execute(); }
                catch (Exception ex)
                {
                    EditorDiagnostics.RecordException(action.Label, ex);
                    _statusText.Text = $"Could not complete {action.Label}: {ex.Message}";
                }
            };
            menu.Items.Add(item);
        }
        var button = new Button { Content = label + " ▾", Name = name, Flyout = menu };
        StyleModernToolbarButton(button, 64);
        if (name == "EditorFileMenu") _toolbarFileMenu = button;
        return button;
    }

    private async void HandleEditorSaveShortcut(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.S || (e.KeyModifiers != KeyModifiers.Meta && e.KeyModifiers != KeyModifiers.Control))
            return;
        // Tunnel first so the viewport never interprets Cmd/Ctrl+S as camera movement.
        e.Handled = true;
        await SaveFromToolbarAsync();
    }

    /// <summary>
    /// Ctrl/Cmd+Z in the view takes back only true "last action" steps: the last brush stroke in Terrain,
    /// the last removal in Objects. The per-item Undo buttons put an item back to the original game state,
    /// which is more than a keystroke should do, so they are named in the status line instead.
    /// </summary>
    private void UndoFromKeyboard()
    {
        if (IsTerrainWorkspaceTabSelected())
        {
            if (_terrainUndoStrokeButton?.IsEnabled == true) UndoLastTerrainBrushStroke();
            else _statusText.Text = "No brush stroke to undo. To put a tile back the way it was, select it and use Undo Terrain.";
            return;
        }
        if (_objectUndoRemoveButton?.IsEnabled == true) UndoLastRemovedMoby();
        else _statusText.Text = "No removal to undo. To put an object back the way it was, select it and use Undo.";
    }

    private void RedoFromKeyboard()
    {
        if (IsTerrainWorkspaceTabSelected() && _terrainRedoStrokeButton?.IsEnabled == true) RedoLastTerrainBrushStroke();
        else _statusText.Text = "Nothing to redo.";
    }

    private bool ToolbarSaveBlocked =>
        _createBinBusy || _regularEditorPersistenceBusy || _workspaceTransitionBusy || _loadingLevel ||
        _freshNativeResearchBusy || _buildSafetyBusy || _buildingPortableCache || _projectDataImportBusy ||
        _terrainTexturePaintBusy || _terrainTextureRelocationBusy || _terrainTexturePaintHistoryRestoreBusy ||
        _id65BlankLabBusy || _nativeLevelReplacementBusy || _handlingLevelSelection || _levelSelectionAsyncInFlight > 0;

    /// <summary>A level (or an authored scene) is actually open; false on the first-run screen.</summary>
    private bool HasLoadedSceneContent =>
        !_releaseMode || _freshSceneDocument != null || _currentGeometry != null || _currentMobys.Count > 0;

    private void RefreshToolbarSaveAvailability() =>
        SetButtonEnabled(_toolbarSaveButton, _currentLevel != null && HasLoadedSceneContent && !_saveCommandRunning && !ToolbarSaveBlocked);

    private async Task SaveFromToolbarAsync()
    {
        if (_saveCommandRunning) return;
        if (ToolbarSaveBlocked)
        {
            _statusText.Text = "Wait for the current editor operation to finish, then save again.";
            return;
        }
        _saveCommandRunning = true;
        RefreshActionAvailability();
        RefreshLevelSelectionAvailability();
        if (_toolbarSaveButton != null)
        {
            _toolbarSaveButton.IsEnabled = false;
            _toolbarSaveButton.Content = "Saving…";
        }
        try
        {
            if (await SaveCurrentEditsAsync())
                _statusText.Text = $"Saved {_currentLevel?.DisplayName ?? "level"}. Create BIN when you are ready to play your changes.";
        }
        catch (Exception ex)
        {
            EditorDiagnostics.RecordException("saving the current level", ex);
            _statusText.Text = $"Could not save this level: {ex.Message}";
        }
        finally
        {
            _saveCommandRunning = false;
            if (_toolbarSaveButton != null) _toolbarSaveButton.Content = "Save";
            RefreshActionAvailability();
            RefreshLevelSelectionAvailability();
        }
    }

    /// <summary>The game file made by the last successful Create BIN in this session, if it still exists.</summary>
    private string? _lastExportedCuePath;
    private Button? _showExportedGameButton;

    private void RememberExportedGame(string? cuePath)
    {
        _lastExportedCuePath = string.IsNullOrWhiteSpace(cuePath) || !File.Exists(cuePath) ? null : cuePath;
        if (_showExportedGameButton != null)
            _showExportedGameButton.IsVisible = _lastExportedCuePath != null;
    }

    private Button BuildShowExportedGameButton()
    {
        _showExportedGameButton = NewButton("Show Game File", ShowExportedGame);
        _showExportedGameButton.Name = "ShowExportedGame";
        _showExportedGameButton.IsVisible = false;
        ToolTip.SetTip(_showExportedGameButton, "Show the game you last created in its folder. Open the CUE file in your emulator.");
        return _showExportedGameButton;
    }

    private void ShowExportedGame()
    {
        string? cue = _lastExportedCuePath;
        if (cue == null || !File.Exists(cue))
        {
            RememberExportedGame(null);
            _statusText.Text = "That game file is no longer there. Use Create BIN to make it again.";
            return;
        }
        try
        {
            System.Diagnostics.ProcessStartInfo start;
            if (OperatingSystem.IsMacOS())
            {
                start = new("open") { UseShellExecute = false };
                start.ArgumentList.Add("-R"); start.ArgumentList.Add(cue);
            }
            else if (OperatingSystem.IsWindows())
                start = new("explorer.exe") { UseShellExecute = false, Arguments = "/select,\"" + cue + "\"" };
            else
            {
                start = new("xdg-open") { UseShellExecute = false };
                start.ArgumentList.Add(Path.GetDirectoryName(cue)!);
            }
            System.Diagnostics.Process.Start(start);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            _statusText.Text = $"Could not open the folder. The game file is at {cue}";
        }
    }

    private string SummarizeEditorStatus(string message)
    {
        // Preserve warnings verbatim; the detailed export receipt remains in Details.
        bool clean = !message.Contains("skipped", StringComparison.OrdinalIgnoreCase) &&
            !message.Contains("unavailable", StringComparison.OrdinalIgnoreCase) &&
            !message.Contains("could not", StringComparison.OrdinalIgnoreCase);
        string file = _lastExportedCuePath == null ? "" : ": " + Path.GetFileName(_lastExportedCuePath);
        string find = _lastExportedCuePath == null ? "" : " Show Game File finds it.";
        string flight = message.Contains("Fly Anywhere is on", StringComparison.Ordinal) ? " Fly Anywhere is on."
            : message.Contains("Fly Anywhere is off", StringComparison.Ordinal) ? " Fly Anywhere is off." : "";
        if (clean && message.StartsWith("Created all-edits test ", StringComparison.Ordinal))
            return $"Game exported{file}. Open the CUE in your emulator.{find}{flight} Details lists what went in.";
        if (_releaseMode && clean && message.StartsWith("Created ", StringComparison.Ordinal) &&
            message.Contains(". New Game starts in ", StringComparison.Ordinal))
        {
            int at = message.IndexOf(". New Game starts in ", StringComparison.Ordinal) + 2;
            int end = message.IndexOf('.', at);
            string start = end > at ? message[at..(end + 1)] : "";
            return $"Game exported{file}. {start} Open the CUE in your emulator.{find}{flight}";
        }
        return _releaseMode ? PlainStatusForRelease(message) : message;
    }
}
