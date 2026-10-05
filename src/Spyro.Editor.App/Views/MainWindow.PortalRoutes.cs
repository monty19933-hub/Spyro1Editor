using Avalonia.Controls;
using Avalonia.Layout;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private readonly ComboBox _portalDestinationBox = new() { Name = "PortalDestination", MinHeight = 34 };
    private readonly CheckBox _portalReturnHere = new() { Name = "PortalReturnHere", Content = "Return Home comes back through this portal", IsChecked = true };
    private readonly ComboBox _returnHomePortalBox = new() { Name = "ReturnHomePortal", MinHeight = 34 };
    private readonly TextBlock _portalRoutingHint = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private Button? _applyPortalRouteButton, _applyReturnHomeButton, _undoPortalRouteButton;
    private PortalRouteUndo? _portalRouteUndo;
    private bool _refreshingPortalRoutes;
    private bool _portalRoutesReadable;
    private string? _portalRouteFingerprint;

    private Control BuildPortalRoutingControls()
    {
        var panel = new StackPanel { Spacing = 8, Name = "PortalRoutingControls" };
        panel.Children.Add(new TextBlock { Text = "Selected portal leads to" });
        panel.Children.Add(_portalDestinationBox);
        panel.Children.Add(_portalReturnHere);
        _applyPortalRouteButton = NewButton("Apply portal destination", ApplyPortalDestination);
        _applyPortalRouteButton.Name = "ApplyPortalDestination";
        panel.Children.Add(_applyPortalRouteButton);
        panel.Children.Add(new TextBlock { Text = "This level’s Return Home destination" });
        panel.Children.Add(_returnHomePortalBox);
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        _applyReturnHomeButton = NewButton("Apply Return Home", ApplyReturnHomeRoute);
        _applyReturnHomeButton.Name = "ApplyReturnHome";
        _undoPortalRouteButton = NewButton("Undo route change", UndoPortalRouteChange);
        _undoPortalRouteButton.Name = "UndoPortalRoute";
        actions.Children.Add(_applyReturnHomeButton); actions.Children.Add(_undoPortalRouteButton);
        panel.Children.Add(actions); panel.Children.Add(_portalRoutingHint);
        return panel;
    }

    private PortalEndpoint? SelectedPortalEndpoint()
    {
        if (_freshSceneDocument != null || _currentLevel == null ||
            _portalControlBox.SelectedItem is not PortalControlEditorRow row || row.EntryTrigger == null) return null;
        var definition = HomeworldPortalControlCatalog.ForLevel(_currentLevel.Key)
            .SingleOrDefault(p => p.PathTrueIndex == row.EntryTrigger.TrueIndex);
        return definition == null ? null : new(definition.HomeworldKey, definition.PathTrueIndex);
    }

    private bool CanEditPortalRoutes() => _currentLevel != null && !_startBlankBusy && !FreeFlightOperationBusy();

    private void RefreshPortalRouteAvailability()
    {
        if (_applyPortalRouteButton == null) return;
        bool available = _portalRoutesReadable && CanEditPortalRoutes();
        _portalDestinationBox.IsEnabled = _portalReturnHere.IsEnabled = _applyPortalRouteButton.IsEnabled = available && SelectedPortalEndpoint() != null;
        _returnHomePortalBox.IsEnabled = _applyReturnHomeButton!.IsEnabled = available;
        _undoPortalRouteButton!.IsEnabled = available && _portalRouteUndo?.Workspace == _workspace.RootPath &&
            _portalRouteUndo.AfterFingerprint == _portalRouteFingerprint;
    }

    private void RefreshPortalRoutingEditor()
    {
        if (_applyPortalRouteButton == null || _refreshingPortalRoutes) return;
        _refreshingPortalRoutes = true;
        try
        {
            var snapshot = PortalRouteStore.Capture(_workspace.RootPath);
            var levels = _retailCatalog.Levels.Where(l => PortalRouteDocument.IsLevelId(l.LevelId)).ToList();
            var authored = SceneDocumentStore.ReadAll(_workspace.RootPath, _retailCatalog);
            foreach (var scene in authored)
            {
                var level = scene.ResolveTarget(_retailCatalog);
                levels.RemoveAll(l => l.LevelId == level.LevelId); levels.Add(level);
            }
            // Shipping builds list levels by name; the number is added only when two entries share a name
            // (for example a new level that was given an existing level's name).
            var repeatedNames = levels.GroupBy(l => l.DisplayName, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1)
                .Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var choices = levels.OrderBy(l => l.LevelId).Select(l => new PortalLevelChoice(l.LevelId,
                _releaseMode && !repeatedNames.Contains(l.DisplayName) ? l.DisplayName : $"{l.DisplayName} (ID {l.LevelId})")).ToArray();
            _portalDestinationBox.ItemsSource = choices;
            var endpoint = SelectedPortalEndpoint();
            _portalDestinationBox.SelectedItem = endpoint == null ? null : choices.FirstOrDefault(c => c.LevelId == snapshot.Document.DestinationFor(endpoint));
            var returns = new List<ReturnPortalChoice> { new(null, "Original game route") };
            foreach (var portal in HomeworldPortalControlCatalog.All)
            {
                string home = _retailCatalog.FindByKey(portal.HomeworldKey)?.DisplayName ?? portal.HomeworldKey;
                returns.Add(new(new(portal.HomeworldKey, portal.PathTrueIndex), $"{home} — {portal.DestinationName} doorway"));
            }
            _returnHomePortalBox.ItemsSource = returns;
            var explicitReturn = snapshot.Document.Returns.SingleOrDefault(r => r.LevelId == _currentLevel?.LevelId);
            _returnHomePortalBox.SelectedItem = returns.First(r => r.Endpoint == explicitReturn?.Portal);
            _portalRoutesReadable = true; _portalRouteFingerprint = snapshot.Fingerprint;
            RefreshPortalRouteAvailability();
            _portalRoutingHint.Text = "Route changes are saved immediately. Return Home uses the selected doorway’s current position and fly-out path. Moving its stone arch is a separate terrain edit.";
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or ArgumentException)
        {
            _portalRoutesReadable = false;
            RefreshPortalRouteAvailability();
            _portalRoutingHint.Text = "Could not read portal routes: " + e.Message;
        }
        finally { _refreshingPortalRoutes = false; }
    }

    private void ApplyPortalDestination()
    {
        if (!CanEditPortalRoutes() || SelectedPortalEndpoint() is not { } endpoint ||
            _portalDestinationBox.SelectedItem is not PortalLevelChoice destination) return;
        SavePortalRouteChange(d => d.WithDestination(endpoint, destination.LevelId, _portalReturnHere.IsChecked == true),
            $"Portal now leads to {destination.Label}." + (_portalReturnHere.IsChecked == true ? " Return Home comes back through this doorway." : ""));
    }

    private void ApplyReturnHomeRoute()
    {
        if (!CanEditPortalRoutes() || _currentLevel == null || _returnHomePortalBox.SelectedItem is not ReturnPortalChoice target) return;
        int level = _currentLevel.LevelId;
        SavePortalRouteChange(d => d.WithReturn(level, target.Endpoint), "Saved Return Home: " + target.Label + ".");
    }

    private void SavePortalRouteChange(Func<PortalRouteDocument, PortalRouteDocument> change, string message)
    {
        try
        {
            string root = _workspace.RootPath;
            var before = PortalRouteStore.Capture(root);
            var after = PortalRouteStore.Save(root, change(before.Document), before);
            _portalRouteUndo = new(root, before.Document, after.Fingerprint);
            InvalidateBuildSafetySummary(); RefreshPortalRoutingEditor(); _statusText.Text = message + " Create BIN includes the saved routes.";
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or ArgumentException or UnauthorizedAccessException)
        { _statusText.Text = "Portal route change stopped: " + e.Message; }
    }

    private void UndoPortalRouteChange()
    {
        if (!CanEditPortalRoutes() || _portalRouteUndo is not { } undo || undo.Workspace != _workspace.RootPath) return;
        try
        {
            var current = PortalRouteStore.Capture(undo.Workspace);
            if (current.Fingerprint != undo.AfterFingerprint) throw new InvalidDataException("The saved routes changed since this edit. Reopen the route settings before making another change.");
            PortalRouteStore.Save(undo.Workspace, undo.Before, current); _portalRouteUndo = null;
            InvalidateBuildSafetySummary(); RefreshPortalRoutingEditor(); _statusText.Text = "Restored the previous portal routes.";
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
        { _statusText.Text = "Could not undo portal routes: " + e.Message; }
    }

    private sealed record PortalLevelChoice(int LevelId, string Label) { public override string ToString() => Label; }
    private sealed record ReturnPortalChoice(PortalEndpoint? Endpoint, string Label) { public override string ToString() => Label; }
    private sealed record PortalRouteUndo(string Workspace, PortalRouteDocument Before, string AfterFingerprint);
}
