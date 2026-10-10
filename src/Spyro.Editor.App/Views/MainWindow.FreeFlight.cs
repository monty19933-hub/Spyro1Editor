using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Spyro.Editor.Core.Diagnostics;
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
    private readonly Dictionary<int, CheckBox> _freeFlightLevelBoxes = new();
    private Expander? _freeFlightLevelsSection;

    private Control BuildFreeFlightSettingsPanel()
    {
        ToolTip.SetTip(_freeFlightEnabled, _releaseMode
            ? "Changes the game you export: Spyro can fly in every normal level. The editor's Game Camera is separate and always flies."
            : "Controls Spyro's native flight ability in the exported game. The editor's Free Camera is separate.");
        if (_releaseMode)
        {
            _freeFlightEnabled.Content = "Let Spyro fly in every level";
            _freeFlightDetails.FontSize = 12;
            _freeFlightDetails.LineHeight = 17;
            _freeFlightDetails.Foreground = new SolidColorBrush(ModernMutedInk);
        }
        _freeFlightEnabled.PropertyChanged += async (_, change) =>
        {
            if (_syncingFreeFlight || change.Property != ToggleButton.IsCheckedProperty) return;
            _freeFlightDraft = _freeFlightEnabled.IsChecked == true;
            _freeFlightGeneration++;
            RefreshFreeFlightSettingsAvailability();
            // The shipping editor has no separate draft: ticking the box is the saved project choice.
            if (_releaseMode)
            {
                try { await SaveFreeFlightSettingsAsync(); }
                catch (Exception error)
                {
                    EditorDiagnostics.RecordException("saving the Fly Anywhere choice", error);
                    _statusText.Text = "Could not save the Fly Anywhere choice: " + error.Message;
                }
                if (_freeFlightSnapshot != null && (_freeFlightEnabled.IsChecked == true) != _freeFlightSnapshot.WholeGame)
                {
                    // Not saved (busy or failed): show what the project really holds.
                    _freeFlightDraft = _freeFlightSnapshot.GlobalEnabled;
                    _syncingFreeFlight = true;
                    try { _freeFlightEnabled.IsChecked = _freeFlightSnapshot.WholeGame; }
                    finally { _syncingFreeFlight = false; }
                    SyncFreeFlightLevelBoxes();
                    RefreshFreeFlightSettingsAvailability();
                }
            }
        };
        _freeFlightSave = NewAsyncButton("Save Flight", SaveFreeFlightSettingsAsync, RefreshFreeFlightSettingsAvailability);
        _freeFlightSave.Name = "FreeFlightSave";
        _freeFlightRevert = NewButton("Revert", RevertFreeFlightSettings);
        _freeFlightRevert.Name = "FreeFlightRevert";
        StackPanel panel = new() { Spacing = 8, Children = { _freeFlightEnabled, _freeFlightDetails } };
        if (!_releaseMode)
            panel.Children.Add(new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8,
                Children = { _freeFlightSave, _freeFlightRevert } });
        panel.Children.Add(BuildFreeFlightLevelChooser());
        return panel;
    }

    /// <summary>One box per normal level, grouped by home world; each tick is saved at once.</summary>
    private Control BuildFreeFlightLevelChooser()
    {
        StackPanel content = new() { Spacing = 6, Margin = new Avalonia.Thickness(2, 4, 2, 2) };
        content.Children.Add(new TextBlock
        {
            Name = "FreeFlightLevelsNote",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = new SolidColorBrush(ModernMutedInk),
            Text = "Tick the levels where Spyro may fly. The box above ticks them all. A level of your own flies whenever at least one level is ticked."
        });
        var worlds = _retailCatalog.Levels
            .Where(level => Spyro.Editor.Core.Exporting.NativeFreeFlightCompositionPlan.IsNormalLevel(level.LevelId))
            .GroupBy(level => level.LevelId / 10)
            .OrderBy(group => group.Key);
        foreach (var world in worlds)
        {
            LevelDefinition? home = world.FirstOrDefault(level => level.LevelId % 10 == 0);
            content.Children.Add(new TextBlock
            {
                Text = home?.DisplayName ?? $"World {world.Key}",
                FontWeight = FontWeight.SemiBold,
                FontSize = 12,
                Margin = new Avalonia.Thickness(0, 6, 0, 0)
            });
            WrapPanel row = new() { Orientation = Avalonia.Layout.Orientation.Horizontal };
            foreach (LevelDefinition level in world.OrderBy(level => level.LevelId))
            {
                int id = level.LevelId;
                CheckBox box = new() { Name = "FreeFlightLevel" + id, Content = level.DisplayName, Margin = new Avalonia.Thickness(0, 0, 14, 2), MinWidth = 150 };
                ToolTip.SetTip(box, $"Let Spyro fly in {level.DisplayName} in the game you export.");
                box.PropertyChanged += async (_, change) =>
                {
                    if (_syncingFreeFlight || change.Property != ToggleButton.IsCheckedProperty) return;
                    try { await SaveFreeFlightLevelAsync(id, box.IsChecked == true); }
                    catch (Exception error)
                    {
                        EditorDiagnostics.RecordException("saving a Fly Anywhere level choice", error);
                        _statusText.Text = "Could not save the Fly Anywhere choice: " + error.Message;
                        SyncFreeFlightLevelBoxes();
                    }
                };
                _freeFlightLevelBoxes[id] = box;
                row.Children.Add(box);
            }
            content.Children.Add(row);
        }
        _freeFlightLevelsSection = BuildCollapsedPanel("Choose levels", content);
        _freeFlightLevelsSection.Name = "FreeFlightLevelsSection";
        _freeFlightLevelsSection.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        return _freeFlightLevelsSection;
    }

    private void SyncFreeFlightLevelBoxes()
    {
        _syncingFreeFlight = true;
        try
        {
            foreach ((int id, CheckBox box) in _freeFlightLevelBoxes)
                box.IsChecked = _freeFlightSnapshot?.IsEnabled(id) == true;
        }
        finally { _syncingFreeFlight = false; }
    }

    /// <summary>Saves one level's choice at once (the shipping editor has no separate flight draft).</summary>
    private async Task SaveFreeFlightLevelAsync(int levelId, bool enabled)
    {
        if (FreeFlightOperationBusy())
        {
            _statusText.Text = "Wait for the current operation to finish, then tick the level again.";
            SyncFreeFlightLevelBoxes();
            return;
        }
        if (!IsCurrentFreeFlightBinding() || _freeFlightSnapshot == null || _freeFlightError.Length != 0)
        {
            _statusText.Text = _freeFlightError.Length != 0
                ? "Fly Anywhere settings unavailable: " + _freeFlightError
                : "Fly Anywhere settings are not loaded for this project yet. Open a level and try again.";
            SyncFreeFlightLevelBoxes();
            return;
        }
        NativeFreeFlightSettingsSnapshot expected = _freeFlightSnapshot;
        int generation = ++_freeFlightGeneration;
        if (!TryBeginRegularEditorPersistenceOperation("saving a Fly Anywhere level choice", out var operation) || operation == null)
        {
            SyncFreeFlightLevelBoxes();
            return;
        }
        RefreshFreeFlightSettingsAvailability();
        try
        {
            RequireCurrentRegularEditorPersistenceOperation(operation);
            if (!IsCurrentFreeFlightBinding() || !ReferenceEquals(expected, _freeFlightSnapshot))
                throw new OperationCanceledException("The project or level changed before the choice was saved.");
            var saved = NativeFreeFlightSettingsStore.Save(expected, levelId, enabled);
            _freeFlightSnapshot = saved;
            _freeFlightDraft = saved.GlobalEnabled;
            _syncingFreeFlight = true;
            try { _freeFlightEnabled.IsChecked = saved.WholeGame; }
            finally { _syncingFreeFlight = false; }
            string name = _retailCatalog.Levels.FirstOrDefault(level => level.LevelId == levelId)?.DisplayName ?? $"level {levelId}";
            int count = saved.EnabledLevelIds.Count;
            _statusText.Text = enabled
                ? $"Fly Anywhere is on in {name}. {DescribeFlightCount(count)} The next Create BIN includes it."
                : $"Fly Anywhere is off in {name}. {DescribeFlightCount(count)}";
            EditorDiagnostics.RecordAction("Fly Anywhere level choice", $"level {levelId} {(enabled ? "on" : "off")}; now {count} level(s)");
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or OperationCanceledException)
        {
            if (generation == _freeFlightGeneration) _freeFlightError = error is OperationCanceledException ? "" : error.Message;
            _statusText.Text = "Could not save the Fly Anywhere choice: " + error.Message;
        }
        finally
        {
            CompleteRegularEditorPersistenceOperation(operation);
            SyncFreeFlightLevelBoxes();
            RefreshFreeFlightSettingsAvailability();
        }
    }

    private static string DescribeFlightCount(int count) => count switch
    {
        0 => "No level has it now.",
        30 => "Every level has it now.",
        1 => "1 level has it now.",
        _ => $"{count} levels have it now."
    };

    private string DescribeFlightLevels(IReadOnlyList<int> levelIds)
    {
        string[] names = levelIds.Select(id => _retailCatalog.Levels.FirstOrDefault(level => level.LevelId == id)?.DisplayName ?? $"level {id}").ToArray();
        return names.Length switch
        {
            0 => "no level",
            1 => names[0],
            <= 4 => string.Join(", ", names[..^1]) + " and " + names[^1],
            _ => $"{names.Length} levels ({string.Join(", ", names.Take(3))}, …)"
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
                _freeFlightDraft = _freeFlightSnapshot.GlobalEnabled;
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
            { _freeFlightError = error.Message; }
        }
        _syncingFreeFlight = true;
        try { _freeFlightEnabled.IsChecked = _releaseMode ? _freeFlightSnapshot?.WholeGame == true : _freeFlightDraft; }
        finally { _syncingFreeFlight = false; }
        SyncFreeFlightLevelBoxes();
        RefreshFreeFlightSettingsAvailability();
        RefreshCrashPlayerSettings();
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
            var saved = NativeFreeFlightSettingsStore.SaveGlobal(expected, enabled);
            _freeFlightSnapshot = saved;
            SyncFreeFlightLevelBoxes();
            _statusText.Text = _releaseMode
                ? enabled
                    ? "Fly Anywhere is on for this project. The next Create BIN makes a game where Spyro can fly in every level."
                    : "Fly Anywhere is off for this project. The next Create BIN keeps the game's normal movement."
                : $"Saved Fly Anywhere {(enabled ? "On" : "Off")} for all normal levels in this project. Create BIN will rebuild from the original source.";
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
            _freeFlightDraft != _freeFlightSnapshot.GlobalEnabled)
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

    // The choice is for the whole game, so the shipping editor offers it with any level open.
    private bool IsRetailFreeFlightLevel(LevelDefinition? level) => level != null && (_releaseMode ||
        level.Key=="authored65" && _freshSceneDocument!=null ||
        level.LevelId is >= 10 and <= 64 && level.LevelId % 10 <= 4 &&
        _retailCatalog.Levels.Any(retail => retail.LevelId == level.LevelId &&
            LevelCatalog.NormalizeKey(retail.Key) == LevelCatalog.NormalizeKey(level.Key)));

    private bool FreeFlightOperationBusy() => _createBinBusy || _freshNativeResearchBusy || _workspaceTransitionBusy ||
        _regularEditorPersistenceBusy || _buildSafetyBusy || _buildingPortableCache || _projectDataImportBusy ||
        _loadingLevel || _levelSelectionAsyncInFlight > 0 || _id65BlankLabBusy;

    internal void RefreshFreeFlightSettingsAvailability()
    {
        bool retail = IsCurrentFreeFlightBinding() && IsRetailFreeFlightLevel(_freeFlightLevel);
        bool usable = retail && _freeFlightSnapshot != null && _freeFlightError.Length == 0;
        bool busy = FreeFlightOperationBusy();
        _freeFlightEnabled.IsEnabled = usable && !busy;
        if (_freeFlightSave != null) _freeFlightSave.IsEnabled = usable && !busy;
        if (_freeFlightRevert != null) _freeFlightRevert.IsEnabled = retail && !busy;
        if (_releaseMode)
        {
            const string how =
                "Spyro can fly in every normal level the way he does in Gnasty's Loot: jump, then press ✕ again in the air. " +
                "The five flight levels keep their own rules. This is separate from the editor's Game Camera, which always flies.";
            _freeFlightDetails.Text = _freeFlightError.Length != 0
                ? "This setting could not be read, so it is left as it was: " + _freeFlightError
                : !retail ? "Open a level to change this setting."
                : _freeFlightSnapshot?.WholeGame == true
                    ? "On in every level. The next Create BIN includes it. In the game you export, " + how
                : _freeFlightSnapshot?.GlobalEnabled == true
                    ? $"On in {DescribeFlightLevels(_freeFlightSnapshot.EnabledLevelIds)}; everywhere else Spyro moves as in the original game. The next Create BIN includes it. In those levels, " + how
                    : "Off. Spyro moves as in the original game. Tick the box, or choose levels below, and in the next game you export, " + how;
            if (_freeFlightLevelsSection != null)
                _freeFlightLevelsSection.IsEnabled = usable && !busy;
            foreach (CheckBox box in _freeFlightLevelBoxes.Values)
                box.IsEnabled = usable && !busy;
            return;
        }
        if (_freeFlightError.Length != 0) _freeFlightDetails.Text = "Fly Anywhere settings unavailable: " + _freeFlightError;
        else if (!retail) _freeFlightDetails.Text = "Open an ordinary level or the authored main-editor level to change this project setting. Native flight courses retain their own rules.";
        else
        {
            string state = _freeFlightSnapshot?.GlobalEnabled == true ? "On" : "Off";
            string selection = _freeFlightSnapshot?.HasExplicitSelection(_freeFlightLevel!.LevelId) == true ? "saved" : "default";
            bool dirty = _freeFlightSnapshot != null && _freeFlightDraft != _freeFlightSnapshot.GlobalEnabled;
            _freeFlightDetails.Text = $"{_freeFlightLevel!.DisplayName} — {selection}: {state}. " +
                (dirty ? $"Draft: {(_freeFlightDraft ? "On" : "Off")}. Save or Revert before Create BIN. " : "") +
                "On enables the native flight ability in all thirty normal retail levels and the authored slot 65 level: jump, then press Cross again to fly. " +
                "Native flight courses stay unchanged. Off rebuilds normal movement from the original source. Gameplay validation remains pending. " +
                "Save Flight keeps this project's export choice; it does not save an ability to the game's memory card. " +
                "Native game save/reload remains unverified.";
        }
    }
}
