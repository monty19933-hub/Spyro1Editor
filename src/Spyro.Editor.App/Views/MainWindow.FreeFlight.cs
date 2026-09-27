using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private readonly CheckBox _freeFlightEnabled = new() { Name = "FreeFlightEnabled", Content = "Fly Anywhere Enabled", IsChecked = false };
    private readonly TextBlock _freeFlightDetails = new() { Name = "FreeFlightDetails", TextWrapping = TextWrapping.Wrap };
    private Button? _freeFlightSave, _freeFlightRevert;
    private EditorWorkspace? _freeFlightWorkspace;
    private LevelDefinition? _freeFlightLevel;
    private NativeFreeFlightSettingsSnapshot? _freeFlightSnapshot;
    private string _freeFlightError = "";
    private int _freeFlightGeneration, _freeFlightLevelLoadRequest;
    private bool _syncingFreeFlight, _freeFlightDraft;

    private Control BuildFreeFlightSettingsPanel()
    {
        ToolTip.SetTip(_freeFlightEnabled,
            "Controls Spyro's native flight ability in the exported game. The editor's Free Camera is separate.");
        _freeFlightEnabled.PropertyChanged += (_, change) =>
        {
            if (_syncingFreeFlight || change.Property != ToggleButton.IsCheckedProperty) return;
            _freeFlightDraft = _freeFlightEnabled.IsChecked == true;
            _freeFlightGeneration++;
            RefreshFreeFlightSettingsAvailability();
        };
        _freeFlightSave = NewAsyncButton("Save Flight", SaveFreeFlightSettingsAsync, RefreshFreeFlightSettingsAvailability);
        _freeFlightSave.Name = "FreeFlightSave";
        _freeFlightRevert = NewButton("Revert", RevertFreeFlightSettings);
        _freeFlightRevert.Name = "FreeFlightRevert";
        return new StackPanel
        {
            Spacing = 8,
            Children = { _freeFlightEnabled, _freeFlightDetails,
                new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8,
                    Children = { _freeFlightSave, _freeFlightRevert } } }
        };
    }

    internal void RefreshFreeFlightSettingsEditor(LevelDefinition? level)
    {
        _freeFlightGeneration++;
        _freeFlightWorkspace = _workspace; _freeFlightLevel = level;
        _freeFlightLevelLoadRequest = _levelLoadRequestId;
        _freeFlightSnapshot = null; _freeFlightError = ""; _freeFlightDraft = false;
        if (IsRetailFreeFlightLevel(level))
        {
            try
            {
                _freeFlightSnapshot = NativeFreeFlightSettingsStore.Capture(_workspace.RootPath);
                _freeFlightDraft = _freeFlightSnapshot.IsEnabled(level!.LevelId);
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
            { _freeFlightError = error.Message; }
        }
        _syncingFreeFlight = true;
        try { _freeFlightEnabled.IsChecked = _freeFlightDraft; }
        finally { _syncingFreeFlight = false; }
        RefreshFreeFlightSettingsAvailability();
    }

    internal void RevertFreeFlightSettings()
    {
        if (FreeFlightOperationBusy())
        { _statusText.Text = "Wait for the active editor operation before reverting flight settings."; return; }
        RefreshFreeFlightSettingsEditor(_currentLevel);
    }

    internal async Task SaveFreeFlightSettingsAsync()
    {
        if (_createBinBusy)
        { _statusText.Text = "Wait for Create BIN to finish before saving Fly Anywhere settings."; RefreshFreeFlightSettingsAvailability(); return; }
        if (!IsCurrentFreeFlightBinding() || !IsRetailFreeFlightLevel(_freeFlightLevel) || _freeFlightSnapshot == null)
        {
            _statusText.Text = _freeFlightError.Length != 0
                ? "Fly Anywhere settings unavailable: " + _freeFlightError
                : "Fly Anywhere settings are unavailable for this level or stale project selection. Reload the level settings.";
            return;
        }
        if (_freeFlightError.Length != 0)
        { _statusText.Text = "Fly Anywhere settings unavailable: " + _freeFlightError; return; }
        if (_freeFlightDraft && _freeFlightLevel!.LevelId != NativeFreeFlightProjectExportIntent.SupportedEnabledLevelId)
        {
            _statusText.Text = "Fly Anywhere On currently supports Toasty only. No other level or saved choice was substituted.";
            RefreshFreeFlightSettingsAvailability(); return;
        }
        NativeFreeFlightSettingsSnapshot expected = _freeFlightSnapshot;
        LevelDefinition level = _freeFlightLevel!;
        int generation = _freeFlightGeneration;
        bool enabled = _freeFlightDraft;
        if (!TryBeginRegularEditorPersistenceOperation("saving Fly Anywhere settings", out var operation) || operation == null) return;
        RefreshFreeFlightSettingsAvailability();
        try
        {
            await AwaitRegularEditorPersistenceDelayForTestingAsync(operation, "free-flight-before-save");
            RequireCurrentRegularEditorPersistenceOperation(operation);
            if (!IsCurrentFreeFlightBinding() || generation != _freeFlightGeneration ||
                !ReferenceEquals(expected, _freeFlightSnapshot) || !ReferenceEquals(level, _freeFlightLevel))
                throw new OperationCanceledException("The flight draft or its actual project/level owner changed before Save.");
            // The bounded synchronous CAS remains inside the UI persistence
            // owner; no await separates the last current check from publication.
            var saved = NativeFreeFlightSettingsStore.Save(expected, level.LevelId, enabled);
            _freeFlightSnapshot = saved;
            _statusText.Text = $"Saved Fly Anywhere {(enabled ? "On" : "Off")} for {level.DisplayName} in this project. Create BIN will rebuild from the original source.";
            if (saved.RetainedRecoveryDirectory.Length != 0)
                _statusText.Text += " Previous-settings recovery files: " + saved.RetainedRecoveryDirectory;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or OperationCanceledException)
        {
            if (IsCurrentRegularEditorPersistenceOperation(operation) && generation == _freeFlightGeneration && IsCurrentFreeFlightBinding())
            {
                _freeFlightError = error.Message;
                _statusText.Text = "Could not save Fly Anywhere settings: " + error.Message;
            }
        }
        finally
        {
            CompleteRegularEditorPersistenceOperation(operation);
            RefreshFreeFlightSettingsAvailability();
        }
    }

    internal bool TryGetFreeFlightSettingsExportBlockReason(out string reason)
    {
        reason = "";
        if (_freeFlightError.Length != 0)
            reason = "Fly Anywhere settings unavailable: " + _freeFlightError;
        else if (_freeFlightLevel != null && !IsCurrentFreeFlightBinding())
            reason = "Fly Anywhere settings belong to a stale project/level selection. Reload before Create BIN.";
        else if (IsCurrentFreeFlightBinding() && _freeFlightSnapshot != null &&
            _freeFlightDraft != _freeFlightSnapshot.IsEnabled(_freeFlightLevel!.LevelId))
            reason = "Save or Revert the Fly Anywhere draft before Create BIN; unsaved flight choices are not exported.";
        if (reason.Length != 0) return true;
        try
        {
            var current = NativeFreeFlightSettingsStore.Capture(_workspace.RootPath);
            if (_freeFlightSnapshot != null && current.ContentSha256 != _freeFlightSnapshot.ContentSha256)
                throw new InvalidDataException("Saved flight preferences changed; reload the level settings before Create BIN.");
            _ = NativeFreeFlightProjectExportIntent.Capture(_workspace.RootPath);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        { reason = "Fly Anywhere settings unavailable: " + error.Message; }
        return reason.Length != 0;
    }

    private bool IsCurrentFreeFlightBinding() => _freeFlightLevel != null &&
        ReferenceEquals(_freeFlightWorkspace, _workspace) && ReferenceEquals(_freeFlightLevel, _currentLevel) &&
        _freeFlightLevelLoadRequest == _levelLoadRequestId &&
        (_freeFlightSnapshot == null || PathsEqual(_freeFlightSnapshot.Path, Path.Combine(_workspace.RootPath, NativeFreeFlightSettingsStore.FileName)));

    private bool IsRetailFreeFlightLevel(LevelDefinition? level) => level != null &&
        level.LevelId is >= 10 and <= 64 && level.LevelId % 10 <= 4 &&
        _retailCatalog.Levels.Any(retail => retail.LevelId == level.LevelId &&
            LevelCatalog.NormalizeKey(retail.Key) == LevelCatalog.NormalizeKey(level.Key));

    private bool FreeFlightOperationBusy() => _createBinBusy || _freshNativeResearchBusy || _workspaceTransitionBusy ||
        _regularEditorPersistenceBusy || _buildSafetyBusy || _buildingPortableCache || _projectDataImportBusy ||
        _loadingLevel || _levelSelectionAsyncInFlight > 0 || _id65BlankLabBusy;

    internal void RefreshFreeFlightSettingsAvailability()
    {
        bool retail = IsCurrentFreeFlightBinding() && IsRetailFreeFlightLevel(_freeFlightLevel);
        bool usable = retail && _freeFlightSnapshot != null && _freeFlightError.Length == 0;
        bool busy = FreeFlightOperationBusy();
        bool supported = !_freeFlightDraft || _freeFlightLevel?.LevelId == NativeFreeFlightProjectExportIntent.SupportedEnabledLevelId;
        _freeFlightEnabled.IsEnabled = usable && !busy;
        if (_freeFlightSave != null) _freeFlightSave.IsEnabled = usable && !busy && supported;
        if (_freeFlightRevert != null) _freeFlightRevert.IsEnabled = retail && !busy;
        if (_freeFlightError.Length != 0) _freeFlightDetails.Text = "Fly Anywhere settings unavailable: " + _freeFlightError;
        else if (!retail) _freeFlightDetails.Text = "Fly Anywhere is unavailable for independent ID65 and flight-course profiles.";
        else
        {
            string state = _freeFlightSnapshot?.IsEnabled(_freeFlightLevel!.LevelId) == true ? "On" : "Off";
            string selection = _freeFlightSnapshot?.HasExplicitSelection(_freeFlightLevel!.LevelId) == true ? "saved" : "default";
            bool dirty = _freeFlightSnapshot != null && _freeFlightDraft != _freeFlightSnapshot.IsEnabled(_freeFlightLevel!.LevelId);
            _freeFlightDetails.Text = $"{_freeFlightLevel!.DisplayName} — {selection}: {state}. " +
                (dirty ? $"Draft: {(_freeFlightDraft ? "On" : "Off")}. Save or Revert before Create BIN. " : "") +
                "On starts Toasty with the flight ability available: jump, then press Cross again to fly. " +
                "On currently supports Toasty only. Off rebuilds the original retail movement. " +
                "Save Flight keeps this project's export choice; it does not save an ability to the game's memory card. " +
                "Native game save/reload remains unverified.";
        }
    }
}
