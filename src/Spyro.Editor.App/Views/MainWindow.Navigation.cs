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

    private bool ToolbarSaveBlocked =>
        _createBinBusy || _regularEditorPersistenceBusy || _workspaceTransitionBusy || _loadingLevel ||
        _freshNativeResearchBusy || _buildSafetyBusy || _buildingPortableCache || _projectDataImportBusy ||
        _terrainTexturePaintBusy || _terrainTextureRelocationBusy || _terrainTexturePaintHistoryRestoreBusy ||
        _id65BlankLabBusy || _nativeLevelReplacementBusy || _handlingLevelSelection || _levelSelectionAsyncInFlight > 0;

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

    private static string SummarizeEditorStatus(string message)
    {
        // Preserve warnings verbatim; the detailed export receipt remains in Details.
        if (message.StartsWith("Created all-edits test ", StringComparison.Ordinal) &&
            !message.Contains("skipped", StringComparison.OrdinalIgnoreCase) &&
            !message.Contains("unavailable", StringComparison.OrdinalIgnoreCase) &&
            !message.Contains("could not", StringComparison.OrdinalIgnoreCase))
            return "Game exported. Open the CUE in your emulator. Details shows the files and treasure totals.";
        return message;
    }
}
