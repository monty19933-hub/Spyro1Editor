using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Music;

namespace Spyro.Editor.App.Views;

internal sealed partial class NativeFreshDocumentWindow
{
    private const string LegacyModeLabel = "Legacy floor (v1)";
    private const string AggregateModeLabel = "Level aggregate";
    private readonly ComboBox _documentMode = new()
    {
        Name = "FreshDocumentMode", ItemsSource = new[] { LegacyModeLabel, AggregateModeLabel },
        SelectedItem = LegacyModeLabel, MinWidth = 200
    };
    private readonly TextBlock _activeFile = new() { Name = "FreshDocumentActiveFile", TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _schemaVersion = new() { Name = "FreshLevelSchemaVersion", IsVisible = false };
    private readonly TextBlock _materialSummary = new()
    {
        Name = "FreshLevelMaterialSummary", TextWrapping = TextWrapping.Wrap, FontSize = 12, IsVisible = false
    };
    private readonly TextBlock _materialPreviewNotice = new()
    {
        Name = "FreshLevelMaterialPreviewNotice", TextWrapping = TextWrapping.Wrap, FontSize = 12,
        FontWeight = FontWeight.SemiBold, IsVisible = false
    };
    private readonly StackPanel _aggregateFields = new() { Spacing = 8, IsVisible = false };
    private readonly ListBox _placements = new() { Name = "FreshLevelPlacements", Height = 145 };
    private readonly TextBlock _placementSummary = new() { Name = "FreshLevelPlacementSummary", TextWrapping = TextWrapping.Wrap };
    private readonly Dictionary<string, TextBox> _routeFields = new(StringComparer.Ordinal);
    private Button _forkLegacy = null!;
    private Button _addWizard = null!;
    private Button _cancelAddWizard = null!;
    private Button _removeWizard = null!;
    private Button _moveWizardUp = null!;
    private Button _moveWizardDown = null!;
    private NativeFreshLevelDocumentSession? _aggregateSession;
    private NativeId65FreshDocumentSnapshot? _forkSourceSnapshot;
    private bool _aggregateMode;
    private bool _modeSwitchBusy;
    private bool _forkBusy;
    private bool _forkRecoveryPending;
    private bool _addingWizard;
    private string _appliedRouteFields = "";

    internal NativeFreshLevelDocumentIntent? CurrentAggregateIntent =>
        _aggregateMode ? _aggregateSession?.CurrentIntent : null;

    private bool AggregateCommandsBlocked => _decisionBusy || _researchBusy || _modeSwitchBusy || _forkBusy;
    private bool AggregateHasUnappliedFields => _addingWizard || CurrentAggregateIntent != null &&
        (FieldSignature() != _appliedFields || RouteFieldSignature() != _appliedRouteFields);

    private Control BuildAggregateActions()
    {
        _forkLegacy = Button("FreshDocumentForkLegacy", "Fork saved legacy floor", ForkLegacyFloor);
        _documentMode.SelectionChanged += async (_, _) => await ChangeDocumentModeAsync();
        return new StackPanel
        {
            Spacing = 5,
            Children =
            {
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _documentMode, _forkLegacy } },
                _activeFile, _schemaVersion, _materialSummary, _materialPreviewNotice
            }
        };
    }

    private Control BuildAggregateFields()
    {
        _addWizard = Button("FreshWizardAdd", "Add Wizard route", BeginWizardAdd);
        _cancelAddWizard = Button("FreshWizardCancelAdd", "Cancel new route", CancelWizardAdd);
        _removeWizard = Button("FreshWizardRemove", "Remove", RemoveWizard);
        _moveWizardUp = Button("FreshWizardMoveUp", "Move up", () => MoveWizard(-1));
        _moveWizardDown = Button("FreshWizardMoveDown", "Move down", () => MoveWizard(1));
        _placements.ItemTemplate = new FuncDataTemplate<NativeFreshPlacementIntent>((value, _) => new TextBlock
        {
            Text = value is NativeFreshWizardRouteIntent route
                ? $"Wizard route · {route.EditorId}\n({route.OriginX}, {route.OriginY}, {route.OriginZ}) → ({route.EndX}, {route.EndY})"
                : "", TextWrapping = TextWrapping.Wrap, FontSize = 11
        });
        _placements.SelectionChanged += (_, _) => SelectWizard();
        _aggregateFields.Children.Add(Section("Authored placements", "Ordered editor IDs are not native ownership. Routes are XY schematics only, with exact authored Z shown numerically. No trajectory, collision or gameplay is certified."));
        _aggregateFields.Children.Add(_placementSummary);
        _aggregateFields.Children.Add(_placements);
        _aggregateFields.Children.Add(new WrapPanel { Children = { _addWizard, _removeWizard, _moveWizardUp, _moveWizardDown } });
        _aggregateFields.Children.Add(Row(RouteField("FreshWizardOriginX", "Origin X"), RouteField("FreshWizardOriginY", "Origin Y"), RouteField("FreshWizardOriginZ", "Origin Z")));
        _aggregateFields.Children.Add(Row(RouteField("FreshWizardEndX", "End X"), RouteField("FreshWizardEndY", "End Y")));
        _aggregateFields.Children.Add(_cancelAddWizard);
        return _aggregateFields;
    }

    private Control RouteField(string name, string label)
    {
        var box = new TextBox { Name = name, MaxLength = 16, MinHeight = 32 };
        _routeFields.Add(name, box);
        box.TextChanged += (_, _) => FieldsChanged();
        return new StackPanel { Spacing = 4, Children = { new TextBlock { Text = label, FontSize = 12 }, box } };
    }

    private async Task ChangeDocumentModeAsync()
    {
        if (_refreshing) return;
        string? requested = _documentMode.SelectedItem as string;
        RestoreModeSelector();
        if (AggregateCommandsBlocked || requested is not (LegacyModeLabel or AggregateModeLabel) ||
            (requested == AggregateModeLabel) == _aggregateMode) return;

        bool sourceSaved = false;
        _modeSwitchBusy = true;
        RefreshChrome();
        try
        {
            RequireCurrentProject();
            bool wasDirty = HasUnsavedChanges;
            if (!await ResolveDirtyAsync("switching document mode")) return;
            sourceSaved = wasDirty && !HasUnsavedChanges;
            RequireCurrentProject();

            // Capture every required target observation before installing any state.
            // Discard permission alone never destroys a draft when target IO fails.
            if (requested == AggregateModeLabel)
            {
                var (session, source) = CaptureAggregateSession();
                _aggregateSession = session;
                _forkSourceSnapshot = source;
                _aggregateMode = true;
            }
            else
            {
                NativeId65FreshDocumentSnapshot target = NativeId65FreshDocumentStore.Capture(_project);
                _snapshot = target;
                CurrentIntent = target.Intent;
                _undo.Clear();
                _redo.Clear();
                _aggregateMode = false;
                _aggregateSession = null;
                _forkSourceSnapshot = null;
            }
            _addingWizard = false;
            _forkRecoveryPending = false;
            WriteFields();
            _status.Text = _aggregateMode ? AggregateOpenMessage() : $"Opened saved legacy floor: {_snapshot.Path}";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            _status.Text = sourceSaved
                ? $"The source document was saved and verified, but switching mode failed: {error.Message} The saved source remains active."
                : $"Mode switch refused: {error.Message} The prior mode, applied document and raw fields are retained.";
        }
        finally
        {
            _modeSwitchBusy = false;
            RestoreModeSelector();
            RefreshChrome();
        }
    }

    private (NativeFreshLevelDocumentSession Session, NativeId65FreshDocumentSnapshot? Source) CaptureAggregateSession()
    {
        NativeFreshLevelDocumentSnapshot target = NativeFreshLevelDocumentStore.Capture(_project);
        // Fork retains this actual saved source at mode entry/reopen. Never silently
        // recapture it in the Fork command after another writer changes the file.
        NativeId65FreshDocumentSnapshot? source = target.Exists ? null : NativeId65FreshDocumentStore.Capture(_project);
        return (new NativeFreshLevelDocumentSession(target), source);
    }

    private void RestoreModeSelector()
    {
        bool previous = _refreshing;
        _refreshing = true;
        try { _documentMode.SelectedItem = _aggregateMode ? AggregateModeLabel : LegacyModeLabel; }
        finally { _refreshing = previous; }
    }

    private void ForkLegacyFloor()
    {
        if (!_aggregateMode || AggregateCommandsBlocked || _forkRecoveryPending || _aggregateSession == null ||
            _forkSourceSnapshot?.Exists != true || CurrentAggregateIntent != null || HasUnsavedChanges) return;
        try
        {
            RequireCurrentProject();
            _forkBusy = true;
            RefreshChrome();
            NativeFreshLevelDocumentSnapshot result = _aggregateSession.ForkLegacyFloor(_forkSourceSnapshot);
            _addingWizard = false;
            WriteFields();
            _status.Text = $"Forked and verified a separate aggregate with no placements: {result.Path}. The legacy source was not changed." +
                (result.RetainedRecoveryDirectory.Length == 0 ? "" : $" Recovery was retained: {result.RetainedRecoveryDirectory}.");
        }
        catch (NativeFreshLevelDocumentForkPublicationUnconfirmedException error)
        {
            _forkRecoveryPending = true;
            _status.Text = $"Fork publication is unconfirmed. Do not retry or remove evidence. No observed file was adopted into this window. {error.Message} " +
                $"Inspect destination: {error.DestinationPath}; stage: {error.RetainedStagePath}; source lease: {error.SourceLeaseEvidencePath}; " +
                $"aggregate lease: {error.DestinationLeaseEvidencePath}; publication lock: {error.PublicationLockPath}. These paths do not certify ownership or existence.";
        }
        catch (NativeFreshLevelDocumentForkCommittedException error)
        {
            _forkRecoveryPending = true;
            _status.Text = $"Fork committed, but verification or cleanup did not complete. The previous window state is retained; inspect before reopening or retrying. {error.Message} " +
                $"Destination: {error.DestinationPath}; stage: {error.RetainedStagePath}; source lease: {error.SourceLeaseEvidencePath}; aggregate lease: {error.DestinationLeaseEvidencePath}.";
        }
        catch (ExportArtifactRollbackException error)
        {
            _forkRecoveryPending = true;
            _status.Text = $"Fork rollback is incomplete. Preserve recovery files: {error.RetainedRecoveryDirectory}. {error.Message}";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or InvalidOperationException)
        {
            _status.Text = $"Fork did not complete: {error.Message} Current window state is unchanged. Reopen explicitly to inspect current saved files.";
        }
        finally
        {
            _forkBusy = false;
            RefreshChrome();
        }
    }

    private bool ApplyAggregateFields(bool forSave = false)
    {
        if (!_aggregateMode || CurrentAggregateIntent == null || _researchBusy || _forkBusy ||
            !forSave && AggregateCommandsBlocked) return false;
        try
        {
            if (_addingWizard)
            {
                if (FieldSignature() != _appliedFields)
                    throw new InvalidDataException("Base fields are locked while adding a route. Cancel the new route before editing the Base.");
                _aggregateSession!.AddWizardRoute(RouteNumber("FreshWizardOriginX"), RouteNumber("FreshWizardOriginY"),
                    RouteNumber("FreshWizardOriginZ"), RouteNumber("FreshWizardEndX"), RouteNumber("FreshWizardEndY"));
                _addingWizard = false;
            }
            else
            {
                var floor = NativeId65FreshDocumentIntent.Create(Number("FreshFloorX"), Number("FreshFloorY"), Number("FreshFloorZ"),
                    Number("FreshFloorWidth"), Number("FreshFloorDepth"), Number("FreshSpawnX"), Number("FreshSpawnY"), Number("FreshHeading"),
                    Rgb("FreshFloorColor"), Rgb("FreshSkyColor"), (_music.SelectedItem as MusicTrackEntry)?.TrackId ?? -1);
                NativeFreshPlacementIntent[] rows = CurrentAggregateIntent.Placements.ToArray();
                string? selected = _aggregateSession!.SelectedEditorId;
                if (selected != null)
                {
                    int index = Array.FindIndex(rows, placement => placement.EditorId == selected);
                    rows[index] = NativeFreshWizardRouteIntent.Create(selected, RouteNumber("FreshWizardOriginX"), RouteNumber("FreshWizardOriginY"),
                        RouteNumber("FreshWizardOriginZ"), RouteNumber("FreshWizardEndX"), RouteNumber("FreshWizardEndY"));
                }
                // Validate the selected Base/route pair and every untouched route
                // together before the Session sees any mutation.
                _aggregateSession.Apply(CurrentAggregateIntent.WithContent(floor, rows));
            }
            WriteFields();
            _status.Text = "Applied the entire aggregate to the authored schematic. Save writes the exact applied values; aggregate export remains unavailable.";
            return true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidDataException or OverflowException or InvalidOperationException)
        {
            _status.Text = $"Changes not applied: {error.Message} The previous aggregate and your raw fields are retained.";
            RefreshChrome();
            return false;
        }
    }

    private bool SaveAggregateDocument()
    {
        if (CurrentAggregateIntent == null || _researchBusy || _forkBusy) return false;
        try
        {
            RequireCurrentProject();
            if (!ApplyAggregateFields(forSave: true)) return false;
            NativeFreshLevelDocumentSnapshot result = _aggregateSession!.Save();
            RefreshChrome();
            _status.Text = $"Saved and reopened verified aggregate: {result.Path}." +
                (result.RetainedRecoveryDirectory.Length == 0 ? "" : $" Recovery files were retained: {result.RetainedRecoveryDirectory}.");
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            _status.Text = $"Aggregate Save did not complete cleanly: {error.Message} Your draft is retained. Reopen to inspect current saved bytes before retrying.";
            RefreshChrome();
            return false;
        }
    }

    private async Task ReopenAggregateAsync()
    {
        if (AggregateCommandsBlocked) return;
        try
        {
            RequireCurrentProject();
            if (!await ResolveDirtyAsync("reopening the saved aggregate")) return;
            RequireCurrentProject();
            var (next, source) = CaptureAggregateSession();
            string? selected = _aggregateSession?.SelectedEditorId;
            if (selected != null && next.CurrentIntent?.Placements.Any(p => p.EditorId == selected) == true) next.Select(selected);
            _aggregateSession = next;
            _forkSourceSnapshot = source;
            _addingWizard = false;
            _forkRecoveryPending = false;
            WriteFields();
            _status.Text = AggregateOpenMessage() + " Undo history was reset.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            _status.Text = $"Aggregate Reopen refused: {error.Message} Current applied document and raw fields are retained.";
        }
    }

    private string AggregateOpenMessage() => CurrentAggregateIntent == null
        ? "No aggregate exists. Fork an actual saved legacy floor explicitly to begin; no default or placement was inferred."
        : $"Opened saved aggregate: {_aggregateSession!.Snapshot.Path}. Aggregate research export is unavailable until saved-revision publication binding is implemented.";

    private void BeginWizardAdd()
    {
        if (!_aggregateMode || AggregateCommandsBlocked || CurrentAggregateIntent == null ||
            HasUnappliedFields || CurrentAggregateIntent.Placements.Count >= NativeFreshLevelDocumentIntent.MaximumPlacements) return;
        _addingWizard = true;
        WriteFields();
        _status.Text = "New Wizard route is pending and unsaved. Enter all five values, then Apply; no ID is created until valid Apply. Base fields are locked until Apply or Cancel new route.";
    }

    private void CancelWizardAdd()
    {
        if (!_aggregateMode || AggregateCommandsBlocked || !_addingWizard) return;
        _addingWizard = false;
        WriteFields();
        _status.Text = "Canceled the pending new route. No placement or editor ID was added.";
    }

    private void SelectWizard()
    {
        if (_refreshing || !_aggregateMode || _aggregateSession == null) return;
        if (AggregateCommandsBlocked || HasUnappliedFields)
        {
            RestorePlacementSelection();
            if (!AggregateCommandsBlocked) _status.Text = "Apply or Undo fields before changing selection.";
            return;
        }
        _aggregateSession.Select((_placements.SelectedItem as NativeFreshPlacementIntent)?.EditorId);
        WriteFields();
    }

    private void RemoveWizard()
    {
        if (!_aggregateMode || AggregateCommandsBlocked || HasUnappliedFields || _aggregateSession?.SelectedEditorId is not { } id) return;
        _aggregateSession.RemovePlacement(id);
        WriteFields();
        _status.Text = "Removed the selected route from the applied aggregate. Save to persist; Undo restores its editor ID.";
    }

    private void MoveWizard(int direction)
    {
        if (!_aggregateMode || AggregateCommandsBlocked || HasUnappliedFields || CurrentAggregateIntent == null ||
            _aggregateSession?.SelectedEditorId is not { } id) return;
        NativeFreshPlacementIntent[] rows = CurrentAggregateIntent.Placements.ToArray();
        int index = Array.FindIndex(rows, placement => placement.EditorId == id);
        int target = index + direction;
        if (target < 0 || target >= rows.Length) return;
        (rows[index], rows[target]) = (rows[target], rows[index]);
        _aggregateSession.Apply(CurrentAggregateIntent.WithContent(CurrentAggregateIntent.Base, rows));
        WriteFields();
        _status.Text = "Changed authored order without changing editor IDs. Save to persist.";
    }

    private void UndoAggregate()
    {
        if (AggregateCommandsBlocked) return;
        if (HasUnappliedFields)
        {
            _addingWizard = false;
            WriteFields();
            _status.Text = "Discarded pending raw fields only; the applied aggregate is unchanged.";
            return;
        }
        if (_aggregateSession?.Undo() != true) return;
        WriteFields();
        _status.Text = "Undid one applied aggregate change. Save to persist.";
    }

    private void RedoAggregate()
    {
        if (AggregateCommandsBlocked || HasUnappliedFields || _aggregateSession?.Redo() != true) return;
        WriteFields();
        _status.Text = "Redid one applied aggregate change with the same editor IDs. Save to persist.";
    }

    private void WriteAggregateFields()
    {
        var current = CurrentAggregateIntent;
        _placements.ItemsSource = current?.Placements;
        RestorePlacementSelection();
        var selected = _addingWizard ? null : _placements.SelectedItem as NativeFreshWizardRouteIntent;
        SetRoute("FreshWizardOriginX", selected?.OriginX); SetRoute("FreshWizardOriginY", selected?.OriginY);
        SetRoute("FreshWizardOriginZ", selected?.OriginZ); SetRoute("FreshWizardEndX", selected?.EndX); SetRoute("FreshWizardEndY", selected?.EndY);
        _appliedRouteFields = RouteFieldSignature();
    }

    private void RestorePlacementSelection()
    {
        bool previous = _refreshing;
        _refreshing = true;
        try { _placements.SelectedItem = CurrentAggregateIntent?.Placements.FirstOrDefault(p => p.EditorId == _aggregateSession?.SelectedEditorId); }
        finally { _refreshing = previous; }
    }

    private void RefreshAggregateChrome(bool editable)
    {
        var current = CurrentAggregateIntent;
        _schemaVersion.IsVisible = _materialSummary.IsVisible = current != null;
        _schemaVersion.Text = current == null ? "" : $"Aggregate schema {current.SchemaVersion}";
        _materialSummary.Text = current?.FloorMaterials is { } materials
            ? $"Triangle 0: {MaterialSummary(materials.Triangle0)}\nTriangle 1: {MaterialSummary(materials.Triangle1)}"
            : "Materials: implicit base floor color (schema 1).";
        _materialPreviewNotice.Text = Preview.MaterialPreviewNotice;
        _materialPreviewNotice.IsVisible = current?.SchemaVersion == 2;
        _capability.Text = _aggregateMode
            ? "Whole-level authoring document — floor, spawn and ordered placements. Normal Create BIN is unavailable. Aggregate research export is unavailable until the actual saved revision has a final publication guard. The schematic does not certify gameplay."
            : LegacyCapabilityText;
        _documentMode.IsEnabled = editable;
        _activeFile.Text = _aggregateMode ? $"Active file: {_aggregateSession?.Snapshot.Path}" : $"Active file: {_snapshot.Path}";
        _aggregateFields.IsVisible = _aggregateMode;
        _forkLegacy.IsVisible = _aggregateMode;
        _forkLegacy.IsEnabled = editable && _aggregateMode && !_forkRecoveryPending && _aggregateSession?.Snapshot.Exists == false &&
            CurrentAggregateIntent == null && !HasUnsavedChanges && _forkSourceSnapshot?.Exists == true && _forkSourceSnapshot.RetainedRecoveryDirectory.Length == 0;
        bool canEditRows = editable && _aggregateMode && CurrentAggregateIntent != null;
        bool hasSelection = _aggregateSession?.SelectedEditorId != null;
        foreach (TextBox field in _fields.Values) field.IsEnabled = !_aggregateMode || !_addingWizard;
        _music.IsEnabled = !_aggregateMode || !_addingWizard;
        foreach (TextBox field in _routeFields.Values) field.IsEnabled = canEditRows && (hasSelection || _addingWizard);
        _placements.IsEnabled = canEditRows && !HasUnappliedFields;
        _addWizard.IsEnabled = canEditRows && !HasUnappliedFields && CurrentAggregateIntent!.Placements.Count < NativeFreshLevelDocumentIntent.MaximumPlacements;
        _cancelAddWizard.IsVisible = _aggregateMode && _addingWizard;
        _cancelAddWizard.IsEnabled = canEditRows && _addingWizard;
        _removeWizard.IsEnabled = canEditRows && hasSelection && !HasUnappliedFields;
        int index = hasSelection && CurrentAggregateIntent != null
            ? CurrentAggregateIntent.Placements.ToList().FindIndex(p => p.EditorId == _aggregateSession!.SelectedEditorId) : -1;
        _moveWizardUp.IsEnabled = _removeWizard.IsEnabled && index > 0;
        _moveWizardDown.IsEnabled = _removeWizard.IsEnabled && index >= 0 && index + 1 < CurrentAggregateIntent!.Placements.Count;
        _placementSummary.Text = $"{CurrentAggregateIntent?.Placements.Count ?? 0} authored placements · maximum {NativeFreshLevelDocumentIntent.MaximumPlacements}. Aggregate export unavailable.";
    }

    private static string MaterialSummary(NativeFreshFloorMaterialIntent material) => material switch
    {
        NativeFreshRetailTerrainMaterialIntent terrain =>
            $"retail terrain record · {terrain.SourceVariant} · WAD {terrain.SourceWadEntry} · texture {terrain.TextureId}",
        { Tag: "base-floor-color" } => "base floor color",
        _ => throw new InvalidDataException("Unsupported floor material intent.")
    };

    private int RouteNumber(string name) => int.TryParse(_routeFields[name].Text,
        System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int value)
        ? value : throw new InvalidDataException($"{name} requires an integer.");
    private void SetRoute(string name, int? value) => _routeFields[name].Text = value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";
    private string RouteFieldSignature() => string.Join("\n", _routeFields.Values.Select(field => field.Text ?? ""));
}
