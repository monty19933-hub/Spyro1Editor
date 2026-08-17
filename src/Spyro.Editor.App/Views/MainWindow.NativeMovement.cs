using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private const bool DefaultSnapNativePathNodesToTerrain = true;
    private List<NativeMobyPath> _currentNativeMobyPaths = [];
    private NativeMobyPathEditLoadResult _nativePathLoadResult =
        new(0, 0, Array.Empty<string>());
    private string _nativeMovementLoadMessage = "Native movement data has not been loaded.";
    private string _savedNativeMovementEditSignature = "";
    private bool _moveSelectedThiefPathWithOwner = true;
    private bool _snapNativePathNodesToTerrain = DefaultSnapNativePathNodesToTerrain;
    private List<NativeDragonRunToEdit> _currentDragonRunToEdits = [];
    private DragonRunToEditLoadResult _dragonRunToLoadResult =
        new(Array.Empty<NativeDragonRunToEdit>(), 0, Array.Empty<string>());
    private readonly Dictionary<int, ViewportDragonRunToTarget> _currentDragonRunToViewportTargets = [];
    private string _savedDragonRunToEditSignature = "";

    private NativeMovementLoadData LoadNativeMovementData(string levelKey)
    {
        LevelDefinition? level = _catalog.FindByKey(levelKey);
        if (level == null || !level.HasSourceTable)
            return NativeMovementLoadData.Empty("This level has no mapped native source table.");

        string sourceImage = FirstExistingDiscImagePath(DiscImageLocator.FindImage(_workspace));
        if (!File.Exists(sourceImage))
            return NativeMovementLoadData.Empty("Select the matching retail disc to decode native movement paths.");

        try
        {
            List<NativeMobyPath> paths = EggThiefPathLocator.Locate(sourceImage, level).ToList();
            string editPath = NativeMobyPathEditPath(level.Key);
            NativeMobyPathEditLoadResult loadResult = NativeMobyPathEditStore.Load(editPath, paths);
            IReadOnlyDictionary<int, DragonRescueCameraData> dragonScenes =
                DragonRescueCameraLocator.Locate(sourceImage, level);
            DragonRunToEditLoadResult dragonLoad = DragonRunToEditStore.LoadFromMobyManifest(
                DragonRunToEditPath(level.Key),
                level.Key,
                dragonScenes);
            List<string> messageParts = [];
            if (paths.Count > 0)
                messageParts.Add($"{paths.Count} egg-thief route(s), {paths.Sum(path => path.NodeCount)} fixed node(s)");
            if (dragonLoad.Edits.Count > 0)
                messageParts.Add($"{dragonLoad.Edits.Count} dragon run-to destination(s)");
            if (messageParts.Count == 0)
                messageParts.Add("no native thief or dragon movement controls in this level");
            int blocked = loadResult.BlockedReasons.Count + dragonLoad.BlockedReasons.Count;
            string message = $"Decoded {string.Join("; ", messageParts)}; loaded {loadResult.AppliedNodeCount + dragonLoad.AppliedCount} edit(s)." +
                (blocked > 0 ? $" {blocked} stale saved movement edit(s) were blocked." : "");
            return new NativeMovementLoadData(paths, loadResult, dragonLoad.Edits.ToList(), dragonLoad, message);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or EndOfStreamException)
        {
            return NativeMovementLoadData.Empty($"Native movement paths are unavailable: {ex.Message}");
        }
    }

    private void ApplyNativeMovementLoadData(NativeMovementLoadData data)
    {
        _currentNativeMobyPaths = data.Paths;
        _nativePathLoadResult = data.PathLoadResult;
        _currentDragonRunToEdits = data.DragonRunToEdits;
        _dragonRunToLoadResult = data.DragonLoadResult;
        _nativeMovementLoadMessage = data.Message;
        _savedNativeMovementEditSignature = BuildNativeMovementEditSignature();
        _savedDragonRunToEditSignature = BuildDragonRunToEditSignature();
        _moveSelectedThiefPathWithOwner = true;
        _snapNativePathNodesToTerrain = DefaultSnapNativePathNodesToTerrain;
        RefreshAllDragonRunToViewportTargets();
        _viewport.SetNativeMovementData(_currentNativeMobyPaths, _currentDragonRunToViewportTargets.Values);
        RefreshNativeMovementActionButton();
    }

    private string NativeMobyPathEditPath(string levelKey) =>
        Path.Combine(_workspace.RootPath, NativeMobyPathEditStore.DefaultFileName(levelKey));

    private string DragonRunToEditPath(string levelKey) =>
        Path.Combine(_workspace.RootPath, $"{LevelCatalog.NormalizeKey(levelKey)}-native-edits.json");

    private async Task<int> PersistCurrentNativeMovementEditsAsync(
        RegularEditorPersistenceOperation? existingOperation = null)
    {
        RegularObjectPersistenceResult result =
            await PersistCurrentObjectAndMovementEditsAsync(existingOperation);
        return result.NativePathCount;
    }

    private string BuildNativeMovementEditSignature()
    {
        return string.Join(
            "|",
            _currentNativeMobyPaths
                .OrderBy(path => path.OwnerTrueIndex)
                .SelectMany(path => path.Nodes.OrderBy(node => node.Index)
                    .Select(node => $"{path.OwnerTrueIndex}:{node.Index}:{node.RawX}:{node.RawY}:{node.RawZ}")));
    }

    private bool HasUnsavedNativeMovementEdits() =>
        !string.Equals(
            BuildNativeMovementEditSignature(),
            _savedNativeMovementEditSignature,
            StringComparison.Ordinal);

    private async Task<int> PersistCurrentDragonRunToEditsAsync(
        RegularEditorPersistenceOperation? existingOperation = null)
    {
        RegularObjectPersistenceResult result =
            await PersistCurrentObjectAndMovementEditsAsync(existingOperation);
        return result.DragonRunToCount;
    }

    private string BuildDragonRunToEditSignature() => string.Join(
        "|",
        _currentDragonRunToEdits
            .OrderBy(edit => edit.OwnerTrueIndex)
            .Select(edit => $"{edit.OwnerTrueIndex}:{edit.RawX}:{edit.RawY}"));

    private IReadOnlyList<NativeDragonRunToEdit> CaptureCurrentDragonRunToEdits()
    {
        return _currentDragonRunToEdits
            .Select(edit =>
            {
                NativeDragonRunToEdit captured = new(edit.LevelKey, edit.Scene);
                captured.SetRawEndpoint(edit.RawX, edit.RawY);
                return captured;
            })
            .ToArray();
    }

    private bool HasUnsavedDragonRunToEdits() =>
        !string.Equals(BuildDragonRunToEditSignature(), _savedDragonRunToEditSignature, StringComparison.Ordinal);

    private int NativeMovementEditedPathCount() =>
        _currentNativeMobyPaths.Count(path => path.HasEdits);

    private bool HasUndoableNativePathEdits(Moby owner) =>
        _currentNativeMobyPaths.Any(path =>
            path.OwnerTrueIndex == owner.TrueIndex &&
            path.HasEdits);

    private bool ResetOwnedNativePathEdits(Moby owner)
    {
        NativeMobyPath? path = _currentNativeMobyPaths.SingleOrDefault(candidate =>
            candidate.OwnerTrueIndex == owner.TrueIndex);
        if (path == null || !path.HasEdits)
            return false;

        path.ResetEdits();
        _snapNativePathNodesToTerrain = DefaultSnapNativePathNodesToTerrain;
        _viewport.RefreshNativeMovementData();
        return true;
    }

    private NativeMobyPath? SelectedNativeMobyPath()
    {
        if (_selectedMoby == null || _selectedMoby.IsAdded || _selectedMoby.IsRemoved)
            return null;
        return _currentNativeMobyPaths.SingleOrDefault(path => path.OwnerTrueIndex == _selectedMoby.TrueIndex);
    }

    private bool CanEditSelectedNativeMovement() =>
        SelectedNativeMobyPath() != null || CanEditSelectedDragonRunTo();

    private string SelectedNativeMovementButtonText() =>
        SelectedNativeMobyPath() != null ? "Edit Run Path" : "Edit Spyro Run-To";

    private void RefreshNativeMovementActionButton()
    {
        if (_objectNativeMovementButton == null)
            return;
        if (_releaseMode ||
            IsId65BlankLabObjectInspectionOnly() ||
            _workspaceTransitionBusy ||
            _id65BlankLabBusy && _id65BlankLabManualOperation != null ||
            _regularEditorPersistenceBusy ||
            _buildSafetyBusy)
        {
            _objectNativeMovementButton.IsVisible = false;
            _objectNativeMovementButton.IsEnabled = false;
            return;
        }

        bool available = CanEditSelectedNativeMovement();
        _objectNativeMovementButton.IsVisible = available;
        _objectNativeMovementButton.IsEnabled = available;
        _objectNativeMovementButton.Content = available ? SelectedNativeMovementButtonText() : "Edit Native Movement";
        ToolTip.SetTip(
            _objectNativeMovementButton,
            SelectedNativeMobyPath() != null
                ? "Edit this egg thief's ordered native PathData route. The dashed closing seam is a possible forward or reverse handler traversal, not necessarily the immediate next step. Keep terrain snap enabled. Edited routes remain runtime-gated in normal Create BIN until their exact profile is proven."
                : "Edit Spyro's native dragon-rescue approach destination without changing the cameras or choreography.");
    }

    private async Task EditSelectedNativeMovementAsync()
    {
        if (TryBlockId65ObjectMutation("Native movement editing"))
            return;

        NativeMobyPath? path = SelectedNativeMobyPath();
        if (path != null)
        {
            await EditNativeMobyPathAsync(path);
            return;
        }

        if (await TryEditSelectedDragonRunToAsync())
            return;

        _statusText.Text = _nativeMovementLoadMessage;
    }

    private async Task EditNativeMobyPathAsync(NativeMobyPath path)
    {
        if (TryBlockId65ObjectMutation("Native path editing") || _currentLevel == null)
            return;

        Moby? owner = _currentMobys.FirstOrDefault(moby => moby.TrueIndex == path.OwnerTrueIndex && !moby.IsRemoved);
        if (owner == null)
        {
            _statusText.Text = $"The owner of native route T{path.OwnerTrueIndex} is not present in this level view.";
            return;
        }
        ObjectDialogSceneIdentity capturedScene =
            CaptureObjectDialogSceneIdentity(_currentLevel, owner);

        Window dialog = new()
        {
            Title = $"Edit Run Path - {owner.DisplayLabel}",
            Width = 760,
            Height = 680,
            MinWidth = 640,
            MinHeight = 480,
            CanResize = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        CheckBox moveWithOwner = new()
        {
            Content = "Move thief and path together",
            IsChecked = _moveSelectedThiefPathWithOwner
        };
        CheckBox snapWhileDragging = new()
        {
            Content = "Snap path handles to terrain while dragging",
            IsChecked = _snapNativePathNodesToTerrain
        };
        ToolTip.SetTip(
            snapWhileDragging,
            "Recommended: keep terrain snap enabled so moved route nodes remain on a valid terrain surface.");
        TextBlock warning = new()
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(175, 66, 52)),
            IsVisible = !_moveSelectedThiefPathWithOwner,
            Text = "Warning: moving the thief without its route makes it return to the old path in-game."
        };
        TextBlock snapWarning = new()
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(175, 66, 52)),
            IsVisible = snapWhileDragging.IsChecked != true,
            Text = "Warning: terrain snap is off. Off-surface route nodes can make the thief leave playable terrain or behave unpredictably."
        };
        moveWithOwner.PropertyChanged += (_, e) =>
        {
            if (e.Property == CheckBox.IsCheckedProperty)
                warning.IsVisible = moveWithOwner.IsChecked != true;
        };
        snapWhileDragging.PropertyChanged += (_, e) =>
        {
            if (e.Property == CheckBox.IsCheckedProperty)
                snapWarning.IsVisible = snapWhileDragging.IsChecked != true;
        };

        List<PathNodeEditorRow> rows = [];
        StackPanel nodeList = new() { Spacing = 7 };
        foreach (NativePathNode node in path.Nodes)
        {
            TextBox xBox = NewNativeMovementPositionBox(node.Position.X);
            TextBox yBox = NewNativeMovementPositionBox(node.Position.Y);
            TextBox zBox = NewNativeMovementPositionBox(node.Position.Z);
            PathNodeEditorRow row = new(node, xBox, yBox, zBox);
            rows.Add(row);

            Grid grid = new()
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(56)),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(new GridLength(74)),
                    new ColumnDefinition(new GridLength(64))
                },
                ColumnSpacing = 7
            };
            TextBlock number = new()
            {
                Text = $"#{node.Index + 1}",
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeight.SemiBold
            };
            Button snap = NewButton("Snap");
            Button reset = NewButton("Reset");
            snap.Click += (_, _) => SnapPathNodeEditorRow(row);
            reset.Click += (_, _) => SetPathNodeEditorRow(row, node.OriginalPosition);
            AddNativeMovementGridChild(grid, number, 0);
            AddNativeMovementGridChild(grid, xBox, 1);
            AddNativeMovementGridChild(grid, yBox, 2);
            AddNativeMovementGridChild(grid, zBox, 3);
            AddNativeMovementGridChild(grid, snap, 4);
            AddNativeMovementGridChild(grid, reset, 5);
            nodeList.Children.Add(grid);
        }

        Grid headings = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(56)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(74)),
                new ColumnDefinition(new GridLength(64))
            },
            ColumnSpacing = 7
        };
        AddNativeMovementGridChild(headings, new TextBlock { Text = "Node", FontWeight = FontWeight.SemiBold }, 0);
        AddNativeMovementGridChild(headings, new TextBlock { Text = "X", FontWeight = FontWeight.SemiBold }, 1);
        AddNativeMovementGridChild(headings, new TextBlock { Text = "Y", FontWeight = FontWeight.SemiBold }, 2);
        AddNativeMovementGridChild(headings, new TextBlock { Text = "Z", FontWeight = FontWeight.SemiBold }, 3);

        TextBlock validation = new()
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(175, 66, 52))
        };
        Button resetWhole = NewButton("Reset Whole Path");
        Button cancel = NewButton("Cancel");
        Button apply = NewButton("Apply Path");
        resetWhole.Click += (_, _) =>
        {
            foreach (PathNodeEditorRow row in rows)
                SetPathNodeEditorRow(row, row.Node.OriginalPosition);
            snapWhileDragging.IsChecked = DefaultSnapNativePathNodesToTerrain;
            validation.Text = "All node fields were reset to their source coordinates and terrain snap was restored. Choose Apply Path to commit the reset.";
        };
        cancel.Click += (_, _) => dialog.Close(false);
        apply.Click += (_, _) =>
        {
            foreach (PathNodeEditorRow row in rows)
            {
                if (!TryParseFloat(row.XBox.Text, out float x) ||
                    !TryParseFloat(row.YBox.Text, out float y) ||
                    !TryParseFloat(row.ZBox.Text, out float z) ||
                    !float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z))
                {
                    validation.Text = $"Node {row.Node.Index + 1} has an invalid XYZ value.";
                    return;
                }
            }
            dialog.Close(true);
        };

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        buttons.Children.Add(resetWhole);
        buttons.Children.Add(cancel);
        buttons.Children.Add(apply);

        NativeMobyPathTraversalProfile? traversalProfile =
            NativeMobyPathTraversalProfileRegistry.Resolve(path);
        string traversalText =
            traversalProfile?.ClosingTraversal == NativeMobyPathClosingTraversal.CyclicForwardOrReverse
                ? "The dashed closing seam is proven handler traversal between the last and first nodes; it may be crossed forward or in reverse, possibly after other handler states."
                : "Route-end behavior is handler-specific and no closing seam is shown unless traversal between the last and first nodes is proven.";
        StackPanel panel = new() { Spacing = 10, Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock
        {
            Text = $"Native owner T{path.OwnerTrueIndex}; {path.NodeCount} fixed ordered nodes. {traversalText} Only XYZ is editable. The 8-byte header, node count/order, traversal state, pointers, and each unknown fourth word are preserved.",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Safety: keep terrain snap enabled so every moved node stays on terrain. Edited egg-thief routes are runtime-gated; normal Create BIN will not export an unproven route profile. Inspect Build Safety and validate a focused test in DuckStation before promotion.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(166, 106, 24))
        });
        panel.Children.Add(moveWithOwner);
        panel.Children.Add(snapWhileDragging);
        panel.Children.Add(warning);
        panel.Children.Add(snapWarning);
        panel.Children.Add(headings);
        panel.Children.Add(new ScrollViewer
        {
            Content = nodeList,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 430
        });
        panel.Children.Add(validation);
        panel.Children.Add(buttons);
        dialog.Content = panel;

        bool accepted = await dialog.ShowDialog<bool>(this);
        if (!IsCurrentObjectDialogSceneIdentity(capturedScene))
            return;
        _moveSelectedThiefPathWithOwner = moveWithOwner.IsChecked == true;
        _snapNativePathNodesToTerrain = snapWhileDragging.IsChecked == true;
        if (!accepted)
            return;

        foreach (PathNodeEditorRow row in rows)
        {
            TryParseFloat(row.XBox.Text, out float x);
            TryParseFloat(row.YBox.Text, out float y);
            TryParseFloat(row.ZBox.Text, out float z);
            row.Node.SetPosition(new Vector3f(x, y, z));
        }

        InvalidateBuildSafetySummary();
        _viewport.RefreshNativeMovementData();
        RefreshActionAvailability();
        _statusText.Text = path.HasEdits
            ? $"Updated {owner.DisplayLabel}'s native run path; {path.Nodes.Count(node => node.HasEdit)} node(s) differ from the source disc. Keep terrain snap enabled; normal Create BIN remains runtime-gated until this route profile is proven."
            : $"Reset {owner.DisplayLabel}'s native run path to the source disc.";
    }

    private void MoveNativePathNodeFromViewport(ViewportNativePathNodeMoveRequestedEventArgs e)
    {
        if (TryBlockId65ObjectMutation("Native path movement"))
            return;

        Vector3f current = e.Node.Position;
        float x = current.X + e.Dx;
        float y = current.Y + e.Dy;
        float z = current.Z;
        if (_snapNativePathNodesToTerrain && TryFindTerrainZAt(x, y, z, out float terrainZ, preferTopSurface: true))
            z = terrainZ;
        e.Node.SetPosition(new Vector3f(x, y, z));
        InvalidateBuildSafetySummary();
        _viewport.RefreshNativeMovementData();
        RefreshActionAvailability();
        _statusText.Text = $"Moved path node {e.Node.Index + 1} to {x:0.###}, {y:0.###}, {z:0.###}.";
    }

    private void TranslateOwnedNativeMovement(Moby owner, Vector3f appliedDelta)
    {
        if (TryBlockId65ObjectMutation("Native movement translation"))
            return;

        NativeMobyPath? path = _currentNativeMobyPaths.SingleOrDefault(candidate => candidate.OwnerTrueIndex == owner.TrueIndex);
        if (path != null && _moveSelectedThiefPathWithOwner)
        {
            path.TranslateRaw(
                checked((int)Math.Round(appliedDelta.X * 16f)),
                checked((int)Math.Round(appliedDelta.Y * 16f)),
                checked((int)Math.Round(appliedDelta.Z * 16f)));
        }

        TranslateOwnedDragonRunTo(owner, appliedDelta);
        _viewport.RefreshNativeMovementData();
    }

    private void SnapPathNodeEditorRow(PathNodeEditorRow row)
    {
        if (!TryParseFloat(row.XBox.Text, out float x) ||
            !TryParseFloat(row.YBox.Text, out float y) ||
            !TryParseFloat(row.ZBox.Text, out float z))
        {
            return;
        }

        if (TryFindTerrainZAt(x, y, z, out float terrainZ, preferTopSurface: true))
            row.ZBox.Text = FormatPosition(terrainZ);
    }

    private static void SetPathNodeEditorRow(PathNodeEditorRow row, Vector3f position)
    {
        row.XBox.Text = FormatPosition(position.X);
        row.YBox.Text = FormatPosition(position.Y);
        row.ZBox.Text = FormatPosition(position.Z);
    }

    private static TextBox NewNativeMovementPositionBox(float value) => new()
    {
        Text = FormatPosition(value),
        MinWidth = 82
    };

    private static void AddNativeMovementGridChild(Grid grid, Control child, int column)
    {
        Grid.SetColumn(child, column);
        grid.Children.Add(child);
    }

    private bool TryGetSelectedDragonRunToEdit(out NativeDragonRunToEdit? edit)
    {
        edit = null;
        if (_selectedMoby == null || _selectedMoby.IsAdded || _selectedMoby.IsRemoved)
            return false;
        edit = _currentDragonRunToEdits.SingleOrDefault(candidate => candidate.OwnerTrueIndex == _selectedMoby.TrueIndex);
        return edit != null;
    }

    private void RefreshAllDragonRunToViewportTargets()
    {
        _currentDragonRunToViewportTargets.Clear();
        foreach (NativeDragonRunToEdit edit in _currentDragonRunToEdits)
            RefreshDragonRunToViewportTarget(edit);
    }

    private void RefreshDragonRunToViewportTarget(NativeDragonRunToEdit edit)
    {
        Moby? dragon = _currentMobys.FirstOrDefault(moby =>
            !moby.IsRemoved && moby.TrueIndex == edit.OwnerTrueIndex);
        float fallbackZ = dragon?.Position.Z ?? edit.Scene.DragonRawZ / 16f;
        float x = (float)edit.EditorX;
        float y = (float)edit.EditorY;
        bool terrainHit = TryFindTerrainZAt(x, y, fallbackZ, out float terrainZ, preferTopSurface: true);
        float z = terrainHit ? terrainZ : fallbackZ;
        Vector3f original = new(
            edit.OriginalRawX / 16f,
            edit.OriginalRawY / 16f,
            z);
        if (_currentDragonRunToViewportTargets.TryGetValue(edit.OwnerTrueIndex, out ViewportDragonRunToTarget? target))
        {
            target.Position = new Vector3f(x, y, z);
            target.TerrainHit = terrainHit;
        }
        else
        {
            _currentDragonRunToViewportTargets[edit.OwnerTrueIndex] = new ViewportDragonRunToTarget(
                edit.OwnerTrueIndex,
                original,
                new Vector3f(x, y, z),
                terrainHit);
        }
    }

    private void MoveDragonRunToFromViewport(ViewportDragonRunToMoveRequestedEventArgs e)
    {
        if (TryBlockId65ObjectMutation("Dragon run-to movement"))
            return;

        NativeDragonRunToEdit? edit = _currentDragonRunToEdits.SingleOrDefault(candidate =>
            candidate.OwnerTrueIndex == e.Target.OwnerTrueIndex);
        if (edit == null)
            return;

        if (!DragonRescueRunTo.TryFromEditorUnits(
                edit.EditorX + e.Dx,
                edit.EditorY + e.Dy,
                out DragonRunToEndpoint endpoint,
                out string error))
        {
            _statusText.Text = error;
            return;
        }

        edit.SetRawEndpoint(endpoint.RawX, endpoint.RawY);
        RefreshDragonRunToViewportTarget(edit);
        InvalidateBuildSafetySummary();
        _viewport.RefreshNativeMovementData();
        RefreshActionAvailability();
        DragonRunToValidation validation = ValidateDragonRunTo(edit);
        _statusText.Text = $"Moved Spyro's run-to destination to {edit.EditorX:0.###}, {edit.EditorY:0.###}. {validation.Message}";
    }

    private void TranslateOwnedDragonRunTo(Moby owner, Vector3f appliedDelta)
    {
        if (TryBlockId65ObjectMutation("Dragon run-to translation"))
            return;

        NativeDragonRunToEdit? edit = _currentDragonRunToEdits.SingleOrDefault(candidate =>
            candidate.OwnerTrueIndex == owner.TrueIndex);
        if (edit == null)
            return;

        edit.TranslateRaw(
            checked((int)Math.Round(appliedDelta.X * 16f)),
            checked((int)Math.Round(appliedDelta.Y * 16f)));
        RefreshDragonRunToViewportTarget(edit);
    }

    private async Task<bool> TryEditSelectedDragonRunToAsync()
    {
        if (TryBlockId65ObjectMutation("Dragon run-to editing") || _currentLevel == null)
            return false;

        if (!TryGetSelectedDragonRunToEdit(out NativeDragonRunToEdit? edit) || edit == null || _selectedMoby == null)
            return false;

        Moby dragon = _selectedMoby;
        ObjectDialogSceneIdentity capturedScene =
            CaptureObjectDialogSceneIdentity(_currentLevel, dragon);
        Window dialog = new()
        {
            Title = $"Edit Spyro Run-To - {dragon.DisplayLabel}",
            Width = 560,
            Height = 410,
            MinWidth = 500,
            MinHeight = 360,
            CanResize = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        TextBox xBox = NewNativeMovementPositionBox((float)edit.EditorX);
        TextBox yBox = NewNativeMovementPositionBox((float)edit.EditorY);
        TextBlock zValue = new();
        TextBlock validation = new()
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(67, 78, 88))
        };

        void RefreshPreview()
        {
            string error = "";
            if (!TryParseFloat(xBox.Text, out float x) ||
                !TryParseFloat(yBox.Text, out float y) ||
                !DragonRescueRunTo.TryFromEditorUnits(x, y, out DragonRunToEndpoint endpoint, out error))
            {
                validation.Text = string.IsNullOrWhiteSpace(error) ? "Enter finite X and Y values." : error;
                validation.Foreground = new SolidColorBrush(Color.FromRgb(175, 66, 52));
                return;
            }

            int dragonRawX = checked((int)Math.Round(dragon.Position.X * 16f));
            int dragonRawY = checked((int)Math.Round(dragon.Position.Y * 16f));
            DragonRunToEncodingResult encoded = DragonRescueRunTo.EncodeEndpoint(
                dragonRawX,
                dragonRawY,
                endpoint.RawX,
                endpoint.RawY,
                new DragonRunToEncoding(edit.Scene.RunToAngle, edit.Scene.RunToRadius));
            float fallbackZ = dragon.Position.Z;
            bool hit = TryFindTerrainZAt(x, y, fallbackZ, out float z, preferTopSurface: true);
            zValue.Text = hit ? $"{z:0.###} (terrain-derived)" : "No terrain hit (Build Safety review)";
            validation.Text = encoded.Validation.Message +
                (encoded.CanPatch && encoded.QuantizationErrorRaw > 0.001
                    ? $" Native angle/radius quantization: {encoded.QuantizationErrorRaw / 16d:0.###} editor units."
                    : "");
            validation.Foreground = new SolidColorBrush(encoded.Validation.Status == MobyBuildSafetyStatus.Blocked
                ? Color.FromRgb(175, 66, 52)
                : encoded.Validation.Status == MobyBuildSafetyStatus.Review
                    ? Color.FromRgb(166, 106, 24)
                    : Color.FromRgb(45, 120, 73));
        }

        xBox.TextChanged += (_, _) => RefreshPreview();
        yBox.TextChanged += (_, _) => RefreshPreview();
        Button reset = NewButton("Reset Native");
        Button cancel = NewButton("Cancel");
        Button apply = NewButton("Apply Destination");
        reset.Click += (_, _) =>
        {
            xBox.Text = FormatPosition(edit.OriginalRawX / 16f);
            yBox.Text = FormatPosition(edit.OriginalRawY / 16f);
            RefreshPreview();
        };
        cancel.Click += (_, _) => dialog.Close(false);
        apply.Click += (_, _) =>
        {
            if (!TryParseFloat(xBox.Text, out float x) ||
                !TryParseFloat(yBox.Text, out float y) ||
                !DragonRescueRunTo.TryFromEditorUnits(x, y, out DragonRunToEndpoint endpoint, out _))
            {
                return;
            }
            int dragonRawX = checked((int)Math.Round(dragon.Position.X * 16f));
            int dragonRawY = checked((int)Math.Round(dragon.Position.Y * 16f));
            if (!DragonRescueRunTo.ValidateEndpoint(dragonRawX, dragonRawY, endpoint.RawX, endpoint.RawY).CanPatch)
                return;
            dialog.Close(true);
        };

        Grid fields = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(160)),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            RowSpacing = 8,
            ColumnSpacing = 10
        };
        AddNativeMovementGridChild(fields, new TextBlock { Text = "Destination X", VerticalAlignment = VerticalAlignment.Center }, 0);
        AddNativeMovementGridChild(fields, xBox, 1);
        Grid.SetRow(xBox, 0);
        TextBlock yLabel = new() { Text = "Destination Y", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(yLabel, 1);
        Grid.SetColumn(yLabel, 0);
        fields.Children.Add(yLabel);
        Grid.SetRow(yBox, 1);
        Grid.SetColumn(yBox, 1);
        fields.Children.Add(yBox);
        TextBlock zLabel = new() { Text = "Destination Z", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(zLabel, 2);
        Grid.SetColumn(zLabel, 0);
        fields.Children.Add(zLabel);
        Grid.SetRow(zValue, 2);
        Grid.SetColumn(zValue, 1);
        fields.Children.Add(zValue);

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        buttons.Children.Add(reset);
        buttons.Children.Add(cancel);
        buttons.Children.Add(apply);
        StackPanel panel = new() { Spacing = 13, Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock
        {
            Text = "Move the dashed endpoint only. X/Y export as the dragon-relative native 12-bit angle and planar radius. Z is read-only and terrain-derived; cameras, timing, pedestal, and +0x30 choreography data remain unchanged.",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(fields);
        panel.Children.Add(validation);
        panel.Children.Add(buttons);
        dialog.Content = panel;
        RefreshPreview();

        bool accepted = await dialog.ShowDialog<bool>(this);
        if (!IsCurrentObjectDialogSceneIdentity(capturedScene))
            return true;
        if (!accepted)
            return true;

        TryParseFloat(xBox.Text, out float editedX);
        TryParseFloat(yBox.Text, out float editedY);
        DragonRescueRunTo.TryFromEditorUnits(editedX, editedY, out DragonRunToEndpoint editedEndpoint, out _);
        edit.SetRawEndpoint(editedEndpoint.RawX, editedEndpoint.RawY);
        RefreshDragonRunToViewportTarget(edit);
        InvalidateBuildSafetySummary();
        _viewport.RefreshNativeMovementData();
        RefreshActionAvailability();
        _statusText.Text = edit.HasEdit
            ? $"Updated Spyro's run-to destination for {dragon.DisplayLabel}."
            : $"Reset Spyro's run-to destination for {dragon.DisplayLabel} to the source disc.";
        return true;
    }

    private DragonRunToValidation ValidateDragonRunTo(NativeDragonRunToEdit edit)
    {
        Moby? dragon = _currentMobys.FirstOrDefault(moby => moby.TrueIndex == edit.OwnerTrueIndex && !moby.IsRemoved);
        int dragonRawX = dragon == null ? edit.Scene.DragonRawX : checked((int)Math.Round(dragon.Position.X * 16f));
        int dragonRawY = dragon == null ? edit.Scene.DragonRawY : checked((int)Math.Round(dragon.Position.Y * 16f));
        return DragonRescueRunTo.ValidateEndpoint(dragonRawX, dragonRawY, edit.RawX, edit.RawY);
    }

    private bool CanEditSelectedDragonRunTo() => TryGetSelectedDragonRunToEdit(out _);

    private sealed record PathNodeEditorRow(
        NativePathNode Node,
        TextBox XBox,
        TextBox YBox,
        TextBox ZBox);

    private sealed record NativeMovementLoadData(
        List<NativeMobyPath> Paths,
        NativeMobyPathEditLoadResult PathLoadResult,
        List<NativeDragonRunToEdit> DragonRunToEdits,
        DragonRunToEditLoadResult DragonLoadResult,
        string Message)
    {
        public static NativeMovementLoadData Empty(string message) =>
            new(
                [],
                new NativeMobyPathEditLoadResult(0, 0, Array.Empty<string>()),
                [],
                new DragonRunToEditLoadResult(Array.Empty<NativeDragonRunToEdit>(), 0, Array.Empty<string>()),
                message);
    }
}
