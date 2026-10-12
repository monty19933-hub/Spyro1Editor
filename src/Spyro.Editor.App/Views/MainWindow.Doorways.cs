using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.App.Views;

/// <summary>Terrain › Doorways, in a level you build that stands in a homeworld's place (a rebuilt homeworld such as Lost
/// Fleet) or is the new world's homeworld: add a doorway (where it leads, its shape from a gallery, its frame's texture,
/// at the selected tile or in front of the camera), then move, turn, retarget, reshape, retexture or remove it. A
/// doorway's frame is ordinary scene faces that move and change with it; clicking one of them selects its doorway.
/// Other levels show why they cannot have doorways.</summary>
public sealed partial class MainWindow
{
    private Border? _doorwaysCard;
    private TextBlock? _doorwaysNote, _doorwaySelectedText;
    private ListBox? _doorwayList;
    private StackPanel? _doorwayTools;
    private Button? _doorwayAddButton;
    private int? _selectedDoorway;
    private string _doorwaysSignature = "";
    private bool _syncingDoorwayList, _doorwayBusy;
    private readonly Dictionary<string, IReadOnlyDictionary<int, NativeHubPortals.DoorwayTemplate>> _doorwayTemplates = new(StringComparer.Ordinal);

    /// <summary>What the Add Doorway / Shape dialog answers: where it leads (Add), the shape and size, the frame texture
    /// (a level key and texture id, or null to keep), and for Add whether it goes in front of the camera.</summary>
    internal sealed record DoorwayChoice(int Destination, string ShapeId, float Scale, (string LevelKey, int TextureId)? Texture, bool AtCamera);
    /// <summary>Answers the doorway dialogs in tests (null: cancel).</summary>
    internal Func<DoorwayChoice?>? DoorwayChoiceForTesting { get; set; }
    /// <summary>Answers "Remove this doorway?" in tests.</summary>
    internal Func<bool>? DoorwayRemoveConfirmForTesting { get; set; }
    internal int? SelectedDoorwayForTesting => _selectedDoorway;
    internal void SelectDoorwayForTesting(int destination) => SelectDoorway(destination, highlight: true);

    private static readonly float[] DoorwaySizes = [0.75f, 1f, 1.25f, 1.5f];
    private static string DoorwaySizeName(float scale) => scale switch
    {
        <= 0.8f => "Smaller (three quarters)", < 1.1f => "Normal", < 1.4f => "Larger (a quarter more)", _ => "Largest (half as big again)"
    };

    private Control BuildTerrainDoorwaysCard()
    {
        StackPanel content = new() { Name = "DoorwaysPanel" };
        _doorwaysNote = TerrainNote();
        _doorwaysNote.Name = "DoorwaysNote";
        content.Children.Add(_doorwaysNote);
        _doorwayList = new ListBox { Name = "DoorwayList", MaxHeight = 170, SelectionMode = SelectionMode.Single };
        _doorwayList.SelectionChanged += (_, _) =>
        {
            if (_syncingDoorwayList || _doorwayList.SelectedItem is not DoorwayRow row) return;
            SelectDoorway(row.Destination, highlight: true);
        };
        content.Children.Add(_doorwayList);
        _doorwayAddButton = NewAsyncButton("Add Doorway…", AddDoorwayAsync);
        _doorwayAddButton.Name = "DoorwayAdd";
        _doorwayAddButton.HorizontalAlignment = HorizontalAlignment.Left;
        ToolTip.SetTip(_doorwayAddButton, "Add a doorway: choose where it leads, its shape and its frame's texture. It is placed at the selected tile or in front of the camera, facing the camera.");
        content.Children.Add(_doorwayAddButton);

        _doorwayTools = new StackPanel { Name = "DoorwayTools", Spacing = 8 };
        _doorwaySelectedText = TerrainNote();
        _doorwaySelectedText.Name = "DoorwaySelected";
        _doorwayTools.Children.Add(_doorwaySelectedText);
        Grid move = new()
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 4
        };
        move.Children.Add(new TextBlock { Text = "Move", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        foreach (var (label, name, dx, dy, dz, tip, column) in new[]
        {
            ("◀", "West", -16, 0, 0, "Move the doorway 16 units west (left in the Map view).", 1),
            ("▲", "North", 0, 16, 0, "Move the doorway 16 units north (up in the Map view).", 2),
            ("▼", "South", 0, -16, 0, "Move the doorway 16 units south (down in the Map view).", 3),
            ("▶", "East", 16, 0, 0, "Move the doorway 16 units east (right in the Map view).", 4),
            ("Up", "Up", 0, 0, 8, "Raise the doorway 8 units.", 5),
            ("Down", "Down", 0, 0, -8, "Lower the doorway 8 units.", 6),
        })
        {
            Button button = NewAsyncButton(label, () => MoveSelectedDoorwayAsync(dx, dy, dz));
            button.Name = "DoorwayMove" + name;
            button.MinHeight = 30;
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            ToolTip.SetTip(button, tip);
            Grid.SetColumn(button, column);
            move.Children.Add(button);
        }
        _doorwayTools.Children.Add(move);
        Grid place = TwoColumnGrid(1);
        Button toTile = NewAsyncButton("To Selected Tile", MoveSelectedDoorwayToTileAsync);
        toTile.Name = "DoorwayToTile";
        ToolTip.SetTip(toTile, "Stand the doorway on the middle of the tile you select next (click a tile in the view first, then this).");
        Button toCamera = NewAsyncButton("In Front of Camera", MoveSelectedDoorwayToCameraAsync);
        toCamera.Name = "DoorwayToCamera";
        ToolTip.SetTip(toCamera, "Put the doorway on the ground in front of the 3D view's camera, facing the camera.");
        Place(place, toTile, 0, 0);
        Place(place, toCamera, 1, 0);
        _doorwayTools.Children.Add(place);
        Grid turn = new()
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 4
        };
        turn.Children.Add(new TextBlock { Text = "Turn", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        foreach (var (label, name, steps, column) in new[] { ("⟲ 15°", "Left15", 1, 1), ("⟳ 15°", "Right15", -1, 2), ("⟲ 90°", "Left90", 6, 3), ("⟳ 90°", "Right90", -6, 4) })
        {
            Button button = NewAsyncButton(label, () => TurnSelectedDoorwayAsync(steps));
            button.Name = "DoorwayTurn" + name;
            button.MinHeight = 30;
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            ToolTip.SetTip(button, steps > 0 ? "Turn the doorway anticlockwise (seen from above) about the middle of its floor." : "Turn the doorway clockwise (seen from above) about the middle of its floor.");
            Grid.SetColumn(button, column);
            turn.Children.Add(button);
        }
        _doorwayTools.Children.Add(turn);
        Grid change = TwoColumnGrid(3);
        Button shape = NewAsyncButton("Shape…", ReshapeSelectedDoorwayAsync);
        shape.Name = "DoorwayShape";
        ToolTip.SetTip(shape, "Choose another shape or size for this doorway. Its frame is rebuilt and keeps its textures.");
        Button destination = NewAsyncButton("Leads To…", RetargetSelectedDoorwayAsync);
        destination.Name = "DoorwayDestination";
        ToolTip.SetTip(destination, "Choose which level this doorway leads to.");
        Button front = NewAsyncButton("Front Texture…", () => RetextureSelectedDoorwayAsync(DoorwayFramePart.Face));
        front.Name = "DoorwayFrontTexture";
        ToolTip.SetTip(front, "Choose the texture of the frame's front and back. To paint single faces, click them and use Choose Texture or the Ground card.");
        Button side = NewAsyncButton("Side Texture…", () => RetextureSelectedDoorwayAsync(DoorwayFramePart.Side));
        side.Name = "DoorwaySideTexture";
        ToolTip.SetTip(side, "Choose the texture of the frame's sides, top and inside.");
        Button remove = NewAsyncButton("Remove Doorway", RemoveSelectedDoorwayAsync);
        remove.Name = "DoorwayRemove";
        StyleModernSecondaryButton(remove, ModernRed);
        ToolTip.SetTip(remove, "Remove this doorway and its frame from the level.");
        Place(change, shape, 0, 0);
        Place(change, destination, 1, 0);
        Place(change, front, 0, 1);
        Place(change, side, 1, 1);
        Place(change, remove, 0, 2, span: 2);
        _doorwayTools.Children.Add(change);
        content.Children.Add(_doorwayTools);

        _doorwaysCard = TerrainCard("Doorways", content);
        _doorwaysCard.Name = "TerrainDoorwaysCard";
        RefreshDoorwaysCard();
        return _doorwaysCard;
    }

    private sealed record DoorwayRow(int Destination, string Label)
    {
        public override string ToString() => Label;
    }

    private string DoorwayDestinationName(int levelId)
    {
        if (NewWorldDocument.LevelSlots.Contains(levelId))
            return DestinationNameOrNull(levelId) ?? $"World level {levelId - 70}";
        return _retailCatalog.Levels.FirstOrDefault(l => l.LevelId == levelId)?.DisplayName ?? $"Level {levelId}";
    }

    private static string CompassWord(int heading)
    {
        int degrees = SceneDoorways.Degrees(heading);
        string[] names = ["east", "north-east", "north", "north-west", "west", "south-west", "south", "south-east"];
        return names[(int)Math.Round(degrees / 45.0) % 8];
    }

    /// <summary>"Stone Hill — Stone Arch (larger), facing east": where it leads, its shape and the way Spyro walks in.</summary>
    private string DescribeDoorway(AuthoredPortal portal)
    {
        string shape = DoorwayShapes.Find(portal.Shape)?.Name ?? "Original doorway";
        string size = portal.Scale is float s && Math.Abs(s - 1) > 0.01f ? $" ({DoorwaySizeName(s).Split(' ')[0].ToLowerInvariant()})" : "";
        return $"{DoorwayDestinationName(portal.DestinationLevelId)} — {shape}{size}, facing {CompassWord(portal.Heading)}";
    }

    /// <summary>Shows the card in a level you build: why doorways cannot go here, or the level's doorways and the tools for
    /// the selected one (the doorway of a frame face clicked in the view is selected).</summary>
    private void RefreshDoorwaysCard()
    {
        if (_doorwaysCard == null || _doorwaysNote == null || _doorwayList == null || _doorwayTools == null || _doorwayAddButton == null)
            return;
        var scene = _freshSceneDocument;
        _doorwaysCard.IsVisible = scene != null;
        if (scene == null) { _selectedDoorway = null; return; }
        string? unavailable = SceneDoorways.Unavailable(scene, _retailCatalog);
        _doorwayList.IsVisible = _doorwayAddButton.IsVisible = unavailable == null;
        if (unavailable != null)
        {
            _doorwaysNote.Text = unavailable;
            _doorwayTools.IsVisible = false;
            _selectedDoorway = null;
            return;
        }
        var portals = scene.Portals ?? [];
        // A frame face selected in the view selects its doorway.
        if (_selectedTerrain is { SectorIndex: >= 0 } face && face.SectorIndex < scene.Surfaces.Count &&
            SceneDoorways.OwnerOf(scene, scene.Surfaces[face.SectorIndex].Id) is { } owner)
            _selectedDoorway = owner.DestinationLevelId;
        if (_selectedDoorway is int chosen && portals.All(p => p.DestinationLevelId != chosen))
            _selectedDoorway = null;
        string signature = string.Join("|", portals.Select(p => $"{p.DestinationLevelId}:{p.Shape}:{p.Scale}:{p.Heading}:{p.Position}")) + "#" + _selectedDoorway;
        if (signature != _doorwaysSignature)
        {
            _doorwaysSignature = signature;
            _syncingDoorwayList = true;
            try
            {
                var rows = portals.Select(p => new DoorwayRow(p.DestinationLevelId, DescribeDoorway(p))).ToArray();
                _doorwayList.ItemsSource = rows;
                _doorwayList.SelectedItem = rows.FirstOrDefault(r => r.Destination == _selectedDoorway);
            }
            finally { _syncingDoorwayList = false; }
        }
        bool world = scene.WorldLevelId == NewWorldDocument.Homeworld;
        var free = SceneDoorways.Destinations(scene, _retailCatalog, DestinationNameOrNull).Where(d => !d.Taken).ToArray();
        _doorwayAddButton.IsEnabled = !_doorwayBusy && free.Length > 0 && portals.Count < NativeHubPortals.MaxDoorways;
        _doorwaysNote.Text = portals.Count == 0
            ? (world
                ? "Add a doorway to each level of your new world. They are saved with the homeworld; Create BIN can't build the New World yet, so they are playable after New World export."
                : "This homeworld has no doorways yet. Add Doorway… puts one in: Spyro walks into it to reach the level, and comes back out of it.")
            : free.Length == 0 && !world
                ? $"Every level this homeworld's doorways can lead to has one ({Count(portals.Count, "doorway", "doorways")}). Select one to move, turn, reshape or remove it; Leads To… swaps where one goes."
                : $"{Count(portals.Count, "doorway", "doorways")}. Select one (here or by clicking its frame) to move, turn, reshape or remove it." +
                  (world ? " New World doorways are playable after New World export." : "");
        var selected = portals.FirstOrDefault(p => p.DestinationLevelId == _selectedDoorway);
        _doorwayTools.IsVisible = selected != null;
        _doorwayTools.IsEnabled = !_doorwayBusy;
        if (selected != null && _doorwaySelectedText != null)
            _doorwaySelectedText.Text = DescribeDoorway(selected) + $": Spyro walks in heading {SceneDoorways.Degrees(selected.Heading)}° (0° is east)." +
                (selected.FrameFaces == null ? " Its frame is not known yet: the first change looks for it." : $" Its frame has {Count(selected.FrameFaces.Count, "face", "faces")}.") +
                (world ? " Playable after New World export." : "");
    }

    private IReadOnlyDictionary<int, string> _doorwayWorldNames = new Dictionary<int, string>();
    private string _doorwayWorldNamesKey = "";

    /// <summary>The name of one of the new world's levels (null: not made, or not a world level). The names are read
    /// from the project only when its world files change (the card refreshes on every selection).</summary>
    private string? DestinationNameOrNull(int levelId)
    {
        if (!NewWorldDocument.LevelSlots.Contains(levelId) || string.IsNullOrEmpty(_workspace.RootPath)) return null;
        string root = _workspace.RootPath;
        string key = string.Join("|", new[] { NewWorldDocument.PathFor(root) }
            .Concat(NewWorldDocument.LevelSlots.Select(id => SceneDocumentStore.PathForTarget(root, NewWorldDocument.KeyFor(id))))
            .Select(path => path + "@" + (File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0)));
        if (key != _doorwayWorldNamesKey)
        {
            _doorwayWorldNames = WorldLevelPickerOptions().Where(l => NewWorldDocument.LevelSlots.Contains(l.LevelId)).ToDictionary(l => l.LevelId, l => l.DisplayName);
            _doorwayWorldNamesKey = key;
        }
        return _doorwayWorldNames.GetValueOrDefault(levelId);
    }

    private void SelectDoorway(int destination, bool highlight)
    {
        _selectedDoorway = destination;
        if (highlight && _freshSceneDocument is { } scene && _currentGeometry != null &&
            SceneDoorways.Find(scene, destination) is { FrameFaces: { } frame })
        {
            // Its frame's faces are selected together, so the Ground card and Choose Texture paint the whole frame.
            var ids = frame.ToHashSet(StringComparer.Ordinal);
            var indexes = Enumerable.Range(0, _currentGeometry.Polygons.Count)
                .Where(i => _currentGeometry.Polygons[i].SectorIndex is int s && s >= 0 && s < scene.Surfaces.Count && ids.Contains(scene.Surfaces[s].Id)).ToArray();
            _selectedTerrain = null;
            _selectedTerrainIndex = -1;
            _selectedTerrainPointIndex = -1;
            _viewport.SetTerrainMultiSelection(indexes);
            _viewport.InvalidateVisual();
        }
        _doorwaysSignature = "";
        RefreshDoorwaysCard();
    }

    /// <summary>The doorway templates (original records) of the homeworld this scene rebuilds, read from the disc once.</summary>
    private async Task<Func<int, NativeHubPortals.DoorwayTemplate?>?> DoorwayTemplatesAsync(SceneDocument scene)
    {
        if (scene.TargetBinding is not { } binding) return null;
        if (!_doorwayTemplates.TryGetValue(binding.LevelKey, out var templates))
        {
            try
            {
                string disc = ResolveUniversalSourceDisc();
                templates = await Task.Run(() => NativeHubPortals.Templates(disc, _retailCatalog, binding.LevelKey));
                _doorwayTemplates[binding.LevelKey] = templates;
            }
            catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
            {
                Spyro.Editor.Core.Diagnostics.EditorDiagnostics.RecordWarning("Doorway templates", e.Message);
                return null;
            }
        }
        return d => templates.GetValueOrDefault(d);
    }

    /// <summary>The open scene (with unsaved tile edits) and its selected doorway; an original doorway's frame is looked
    /// for first, so it moves and changes with it.</summary>
    private async Task<(SceneDocument Scene, AuthoredPortal Portal, Func<int, NativeHubPortals.DoorwayTemplate?>? Templates)?> CurrentDoorwayAsync()
    {
        if (_freshSceneDocument == null || _currentGeometry == null || _doorwayBusy) return null;
        if (_selectedDoorway is not int destination) { _statusText.Text = "Select a doorway first (in the Doorways list or by clicking its frame)."; return null; }
        var scene = CaptureFreshScene();
        var templates = await DoorwayTemplatesAsync(scene);
        if (SceneDoorways.Find(scene, destination) is { Shape: null, FrameFaces: null } && templates?.Invoke(destination) is { } template)
        {
            var (adopted, found) = SceneDoorways.AdoptOriginalFrame(scene, destination, template);
            scene = adopted;
            if (found > 0) Spyro.Editor.Core.Diagnostics.EditorDiagnostics.RecordAction($"Doorway to {destination}: found its frame ({found} faces).");
        }
        if (SceneDoorways.Find(scene, destination) is not { } portal) { _statusText.Text = "That doorway is no longer in this level."; return null; }
        return (scene, portal, templates);
    }

    private async Task ChangeSelectedDoorwayAsync(Func<AuthoredPortal, AuthoredPortal> change, Func<AuthoredPortal, string> done, DoorwayFramePart? repaint = null)
    {
        if (await CurrentDoorwayAsync() is not { } current) return;
        var (scene, portal, templates) = current;
        SceneDocument updated;
        AuthoredPortal changed = change(portal);
        try { updated = SceneDoorways.Update(scene, portal.DestinationLevelId, changed, templates, repaint); }
        catch (InvalidDataException e) { _statusText.Text = DoorwayProblem(e); return; }
        string message = done(SceneDoorways.Find(updated, changed.DestinationLevelId)!);
        if (portal.Shape == null && portal.FrameFaces == null && SceneDoorways.Find(updated, changed.DestinationLevelId)!.FrameFaces == null)
            message += " Its frame could not be found (it was not made by the editor), so it stays where it was: remove it, or choose a shape to give the doorway a new one.";
        await CommitDoorwaySceneAsync(updated, changed.DestinationLevelId, message);
    }

    private static string DoorwayProblem(InvalidDataException e) =>
        e.Message.Contains("coordinate domain", StringComparison.Ordinal)
            ? "There is no room for the doorway there: part of it would be outside the game's map. Move it further in."
            : e.Message;

    /// <summary>Puts a changed scene in place without moving the camera, selects the doorway, saves the level.</summary>
    private async Task CommitDoorwaySceneAsync(SceneDocument scene, int? select, string message)
    {
        ReplaceFreshSceneKeepingView(scene);
        _doorwaysSignature = "";
        if (select is int destination) SelectDoorway(destination, highlight: true);
        else { _selectedDoorway = null; RefreshDoorwaysCard(); }
        if (await SaveFreshSceneAsync())
            _statusText.Text = message;
        RefreshDoorwaysCard();
    }

    /// <summary>The scene's faces projected again in the same geometry (the view keeps its camera), as Apply would.</summary>
    private void ReplaceFreshSceneKeepingView(SceneDocument document)
    {
        if (_freshSceneDocument == null || _currentGeometry == null) return;
        document.Validate();
        var resolver = _freshSceneDocument.LevelSourceEntry;
        _freshSceneDocument = document with { OriginalSurfaces = document.Surfaces, LevelSourceEntry = resolver };
        _freshAddedFaces.Clear();
        _freshNativeCompilation = null; _freshNativeCompilationKey = null;
        var projected = document.ProjectGeometry();
        _viewport.ClearTerrainMultiSelection();
        _selectedTerrain = null; _selectedTerrainIndex = -1; _selectedTerrainPointIndex = -1;
        _currentGeometry.Polygons.Clear();
        _currentGeometry.Polygons.AddRange(projected.Polygons);
        _currentGeometry.Bounds = projected.Bounds;
        ClearTerrainBrushUndoHistory();
        _terrainCollisionMatchedFaces = _currentGeometry.Polygons.Count;
        _viewport.ResetSelection();
        _viewport.NotifyTerrainPresentationDataChanged();
        _viewport.InvalidateVisual();
        _ = RefreshFreshMaterialPreviewAsync();
        RefreshCurrentLevelDetails();
        RefreshTerrainReadinessHint();
    }

    internal Task AddDoorwayForTestingAsync() => AddDoorwayAsync();

    private async Task AddDoorwayAsync()
    {
        if (_freshSceneDocument == null || _currentGeometry == null || _doorwayBusy) return;
        if (SceneDoorways.Unavailable(_freshSceneDocument, _retailCatalog) is { } why) { _statusText.Text = why; return; }
        var scene = CaptureFreshScene();
        var free = SceneDoorways.Destinations(scene, _retailCatalog, DestinationNameOrNull).Where(d => !d.Taken).ToArray();
        if (free.Length == 0)
        {
            _statusText.Text = scene.WorldLevelId == NewWorldDocument.Homeworld
                ? "Every level of your new world already has a doorway here. Make another level with File > New World… first."
                : "Every level this homeworld's doorways can lead to already has one. Remove one first, or use Leads To… on one to swap where it goes.";
            return;
        }
        TerrainPolygon? tile = _selectedTerrain is { IsTerrainRemoved: false } t ? t : null;
        DoorwayChoice? choice = DoorwayChoiceForTesting != null ? DoorwayChoiceForTesting() : await ShowDoorwayDialogAsync("Add Doorway", free, null, tile != null);
        if (choice == null) { _statusText.Text = "No doorway was added."; return; }
        if (_freshSceneDocument?.Id != scene.Id) return;
        _doorwayBusy = true;
        try
        {
            var shape = DoorwayShapes.Find(choice.ShapeId) ?? DoorwayShapes.StoneArch;
            AuthoredMaterial? material = choice.Texture is { } texture ? await StaticFrameTextureAsync(texture) : GroundTypes.Stone.Material(_retailCatalog);
            if (material == null) return;
            if (!TryDoorwaySpot(choice.AtCamera ? null : tile, out Vector3f position, out int heading, out string where))
            {
                _statusText.Text = choice.AtCamera
                    ? "The doorway needs ground in front of the camera: turn the 3D view towards your level's floor, or select a tile and add it there."
                    : "Select the tile the doorway should stand on first, or choose \"In front of the camera\".";
                return;
            }
            var portal = new AuthoredPortal(choice.Destination, position, heading)
            { Shape = shape.Id, Scale = Math.Abs(choice.Scale - 1) < 0.01f ? null : choice.Scale, FaceMaterial = material, SideMaterial = material };
            SceneDocument updated;
            try { updated = SceneDoorways.Add(scene, portal); }
            catch (InvalidDataException e) { _statusText.Text = DoorwayProblem(e); return; }
            string name = DoorwayDestinationName(choice.Destination);
            bool world = scene.WorldLevelId == NewWorldDocument.Homeworld;
            await CommitDoorwaySceneAsync(updated, choice.Destination,
                $"Added a {shape.Name.ToLowerInvariant()} doorway to {name} {where}. Spyro walks in heading {CompassWord(heading)}; " +
                (world ? "it is playable after New World export (Create BIN can't build the New World yet)."
                       : "in the game the opening shows that level's sky. Its frame is ordinary tiles: paint them with the Ground card or Choose Texture."));
        }
        finally { _doorwayBusy = false; RefreshDoorwaysCard(); }
    }

    /// <summary>Where a new or moved doorway stands: on the middle of <paramref name="tile"/> (or, without one, on the ground
    /// about 300 units in front of the 3D view's camera), turned so that its front faces the camera.</summary>
    private bool TryDoorwaySpot(TerrainPolygon? tile, out Vector3f position, out int heading, out string where)
    {
        position = default; heading = 0; where = "";
        bool camera = _viewport.TryGetFlyCameraInLevel(out double cx, out double cy, out double cz, out double fx, out double fy);
        double x, y;
        float z;
        if (tile != null)
        {
            x = tile.Points.Average(p => p.X); y = tile.Points.Average(p => p.Y);
            if (!TryFindTerrainZAt((float)x, (float)y, tile.MaxZ + 1, out z, _currentGeometry!.Polygons.IndexOf(tile)))
                z = tile.ZValues.Average();
            where = "on the selected tile";
        }
        else
        {
            if (!camera) return false;
            x = cx + fx * 300; y = cy + fy * 300;
            if (!TryFindTerrainZAt((float)x, (float)y, (float)cz, out z, preferTopSurface: false)) return false;
            where = "in front of the camera";
        }
        // Spyro walks in away from the camera (the doorway faces it); without a camera, along the level's start heading.
        heading = camera && Math.Abs(x - cx) + Math.Abs(y - cy) > 1
            ? SceneDoorways.HeadingTowards(cx, cy, x, y)
            : SceneDoorways.Turn(_freshSceneDocument?.Heading ?? 0, 0);
        position = new((float)Math.Round(x), (float)Math.Round(y), MathF.Round(z));
        return true;
    }

    /// <summary>A texture from the texture grid as a frame material; textures that move in their level are refused.</summary>
    private async Task<AuthoredMaterial?> StaticFrameTextureAsync((string LevelKey, int TextureId) texture)
    {
        var level = _retailCatalog.FindByKey(texture.LevelKey);
        if (level == null) return null;
        string disc = ResolveUniversalSourceDisc();
        try
        {
            await Task.Run(() => NativeStaticTerrainTextureSource.Capture(disc, level.SourceWadEntry, texture.TextureId));
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or IOException)
        {
            _statusText.Text = $"{level.DisplayName}'s texture {texture.TextureId + 1} moves in its level, so it can't be copied into a level you build. Choose another one.";
            return null;
        }
        return new AuthoredMaterial(level.SourceWadEntry, texture.TextureId);
    }

    internal Task MoveSelectedDoorwayForTestingAsync(int dx, int dy, int dz) => MoveSelectedDoorwayAsync(dx, dy, dz);
    internal Task TurnSelectedDoorwayForTestingAsync(int steps) => TurnSelectedDoorwayAsync(steps);
    internal Task ReshapeSelectedDoorwayForTestingAsync() => ReshapeSelectedDoorwayAsync();
    internal Task RetargetSelectedDoorwayForTestingAsync() => RetargetSelectedDoorwayAsync();
    internal Task RetextureSelectedDoorwayForTestingAsync(bool sides) => RetextureSelectedDoorwayAsync(sides ? DoorwayFramePart.Side : DoorwayFramePart.Face);
    internal Task RemoveSelectedDoorwayForTestingAsync() => RemoveSelectedDoorwayAsync();

    private Task MoveSelectedDoorwayAsync(int dx, int dy, int dz) =>
        ChangeSelectedDoorwayAsync(p => p with { Position = new(p.Position.X + dx, p.Position.Y + dy, p.Position.Z + dz) },
            p => $"Moved the doorway to {DoorwayDestinationName(p.DestinationLevelId)} " + (dz != 0 ? (dz > 0 ? "up" : "down") : dx < 0 ? "west" : dx > 0 ? "east" : dy > 0 ? "north" : "south") +
                 $" (bottom middle now at {p.Position.X:0}, {p.Position.Y:0}, height {p.Position.Z:0}). Its frame moved with it.");

    private async Task MoveSelectedDoorwayToTileAsync()
    {
        if (_selectedTerrain is not { IsTerrainRemoved: false } tile || _freshSceneDocument == null ||
            tile.SectorIndex < 0 || tile.SectorIndex >= _freshSceneDocument.Surfaces.Count ||
            SceneDoorways.OwnerOf(_freshSceneDocument, _freshSceneDocument.Surfaces[tile.SectorIndex].Id) != null)
        {
            _statusText.Text = "Click the tile the doorway should stand on (not its own frame), then To Selected Tile.";
            return;
        }
        if (!TryDoorwaySpot(tile, out Vector3f position, out _, out _)) return;
        // The tile was clicked, so the frame's faces are no longer selected: keep the doorway the card shows.
        await ChangeSelectedDoorwayAsync(p => p with { Position = position },
            p => $"The doorway to {DoorwayDestinationName(p.DestinationLevelId)} now stands on the selected tile. Its frame moved with it.");
    }

    private async Task MoveSelectedDoorwayToCameraAsync()
    {
        if (!TryDoorwaySpot(null, out Vector3f position, out int heading, out _))
        {
            _statusText.Text = "Turn the 3D view towards your level's floor first: the doorway goes on the ground in front of the camera.";
            return;
        }
        await ChangeSelectedDoorwayAsync(p => p with { Position = position, Heading = heading },
            p => $"The doorway to {DoorwayDestinationName(p.DestinationLevelId)} now stands in front of the camera, facing it. Its frame moved with it.");
    }

    private Task TurnSelectedDoorwayAsync(int steps) =>
        ChangeSelectedDoorwayAsync(p => p with { Heading = SceneDoorways.Turn(p.Heading, steps) },
            p => $"Turned the doorway to {DoorwayDestinationName(p.DestinationLevelId)}: Spyro now walks in heading {CompassWord(p.Heading)} ({SceneDoorways.Degrees(p.Heading)}°).");

    private async Task ReshapeSelectedDoorwayAsync()
    {
        if (_freshSceneDocument == null || _selectedDoorway is not int destination || SceneDoorways.Find(_freshSceneDocument, destination) is not { } portal)
        { _statusText.Text = "Select a doorway first (in the Doorways list or by clicking its frame)."; return; }
        DoorwayChoice? choice = DoorwayChoiceForTesting != null ? DoorwayChoiceForTesting() : await ShowDoorwayDialogAsync("Doorway Shape", null, portal, false);
        if (choice == null) return;
        var shape = DoorwayShapes.Find(choice.ShapeId) ?? DoorwayShapes.StoneArch;
        AuthoredMaterial? material = null;
        if (choice.Texture is { } texture && (material = await StaticFrameTextureAsync(texture)) == null) return;
        await ChangeSelectedDoorwayAsync(p => p with
            {
                Shape = shape.Id, Scale = Math.Abs(choice.Scale - 1) < 0.01f ? null : choice.Scale,
                FaceMaterial = material ?? p.FaceMaterial ?? GroundTypes.Stone.Material(_retailCatalog),
                SideMaterial = material ?? p.SideMaterial ?? GroundTypes.Stone.Material(_retailCatalog)
            },
            p => $"The doorway to {DoorwayDestinationName(p.DestinationLevelId)} is a {shape.Name.ToLowerInvariant()} now ({DoorwaySizeName(choice.Scale).Split(' ')[0].ToLowerInvariant()} size). " +
                 "Its frame was rebuilt with the same textures.",
            material != null ? DoorwayFramePart.Face : null);
    }

    private async Task RetargetSelectedDoorwayAsync()
    {
        if (_freshSceneDocument == null || _selectedDoorway is not int destination) { _statusText.Text = "Select a doorway first."; return; }
        var scene = _freshSceneDocument;
        var free = SceneDoorways.Destinations(scene, _retailCatalog, DestinationNameOrNull).Where(d => !d.Taken).ToArray();
        if (free.Length == 0)
        {
            _statusText.Text = "Every level this homeworld's doorways can lead to already has a doorway. Remove the other one first.";
            return;
        }
        int? target;
        if (DoorwayChoiceForTesting != null) target = DoorwayChoiceForTesting()?.Destination;
        else
        {
            ComboBox box = new() { Name = "DoorwayDestinationChoice", ItemsSource = free, SelectedIndex = 0, MinWidth = 260 };
            target = await ShowDoorwayQuestionAsync("Doorway Leads To", $"Where should the doorway to {DoorwayDestinationName(destination)} lead?",
                "Only the levels this homeworld's own doorways lead to can be chosen: their doorway paths, names and return points come from the original homeworld.",
                box, "Change") ? (box.SelectedItem as DoorwayDestination)?.LevelId : null;
        }
        if (target is not int next || free.All(d => d.LevelId != next)) return;
        await ChangeSelectedDoorwayAsync(p => p with { DestinationLevelId = next },
            p => $"The doorway now leads to {DoorwayDestinationName(next)}; Return Home there brings Spyro back out of it.");
    }

    private async Task RetextureSelectedDoorwayAsync(DoorwayFramePart part)
    {
        if (_freshSceneDocument == null || _selectedDoorway is not int) { _statusText.Text = "Select a doorway first."; return; }
        (string LevelKey, int TextureId)? texture = DoorwayChoiceForTesting != null ? DoorwayChoiceForTesting()?.Texture : await ShowBuiltTextureGridAsync(this);
        if (texture is not { } picked || await StaticFrameTextureAsync(picked) is not { } material) return;
        string name = $"{_retailCatalog.FindByKey(picked.LevelKey)?.DisplayName} texture {picked.TextureId + 1}";
        await ChangeSelectedDoorwayAsync(p => part == DoorwayFramePart.Face ? p with { FaceMaterial = material } : p with { SideMaterial = material },
            p => $"The doorway's frame {(part == DoorwayFramePart.Face ? "front and back show" : "sides show")} {name} now.", part);
    }

    private async Task RemoveSelectedDoorwayAsync()
    {
        if (await CurrentDoorwayAsync() is not { } current) return;
        var (scene, portal, _) = current;
        string name = DoorwayDestinationName(portal.DestinationLevelId);
        bool confirmed = DoorwayRemoveConfirmForTesting != null
            ? DoorwayRemoveConfirmForTesting()
            : await ShowConfirmationAsync("DoorwayRemoveDialog", "Remove Doorway", $"Remove the doorway to {name}?",
                (portal.FrameFaces == null
                    ? "The doorway is removed. Its frame was not made by the editor, so its tiles stay; remove them with Remove Terrain if you like."
                    : $"The doorway and its frame ({Count(portal.FrameFaces.Count, "tile", "tiles")}) are removed from the level.") +
                " You can add a doorway to that level again with Add Doorway….", "Remove Doorway");
        if (!confirmed) return;
        SceneDocument updated;
        try { updated = SceneDoorways.Remove(scene, portal.DestinationLevelId); }
        catch (InvalidDataException e) { _statusText.Text = e.Message; return; }
        await CommitDoorwaySceneAsync(updated, null, $"Removed the doorway to {name}" + (portal.FrameFaces == null ? "." : " and its frame."));
    }

    /// <summary>A small question window holding one control.</summary>
    private async Task<bool> ShowDoorwayQuestionAsync(string title, string heading, string explanation, Control control, string confirmLabel)
    {
        var content = new StackPanel { Margin = new Thickness(22), Spacing = 12 };
        content.Children.Add(new TextBlock { Text = heading, FontSize = 18, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new TextBlock { Text = explanation, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(ModernMutedInk) });
        content.Children.Add(control);
        var dialog = new Window { Name = "DoorwayQuestionDialog", Title = title, Width = 480, SizeToContent = SizeToContent.Height, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = content };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        Button cancel = NewButton("Cancel"), confirm = NewButton(confirmLabel);
        cancel.IsCancel = true;
        cancel.Click += (_, _) => dialog.Close(false); confirm.Click += (_, _) => dialog.Close(true);
        StyleModernPrimaryButton(confirm, ModernBlue, 120);
        actions.Children.Add(cancel); actions.Children.Add(confirm); content.Children.Add(actions);
        return await dialog.ShowDialog<bool>(this);
    }

    /// <summary>A small picture of a shape: its frame's front (stone grey) round the opening (sky blue), as the game shows it.</summary>
    internal static Control DoorwayShapePicture(DoorwayShape shape, double size)
    {
        var at = new Vector3f(2000, 2000, 500);
        var faces = DoorwayShapes.Frame(shape, 1, at, 0);
        // Heading 0: the doorway runs along +Y; the side Spyro walks in from is -X. Its front faces are the frame faces
        // there (furthest towards -X), drawn with U = Y and V = Z.
        double front = faces.Where(f => f.Part == DoorwayFramePart.Face).Min(f => f.Corners.Min(c => c.X));
        var fronts = faces.Where(f => f.Part == DoorwayFramePart.Face && f.Corners.All(c => Math.Abs(c.X - front) < 0.6 || c.X < at.X - 1) && f.Corners.Average(c => c.X) < at.X).ToArray();
        var corners = fronts.SelectMany(f => f.Corners).ToArray();
        double minU = corners.Min(c => c.Y - at.Y), maxU = corners.Max(c => c.Y - at.Y), minV = corners.Min(c => c.Z - at.Z), maxV = corners.Max(c => c.Z - at.Z);
        double scale = (size - 6) / Math.Max(maxU - minU, maxV - minV);
        double offsetU = (size - (maxU - minU) * scale) / 2, offsetV = (size - (maxV - minV) * scale) / 2;
        // Seen from the front (from -X looking +X), +Y is on the left.
        Point P(double u, double v) => new(size - (offsetU + (u - minU) * scale), size - (offsetV + (v - minV) * scale));
        Canvas canvas = new() { Width = size, Height = size, Background = new SolidColorBrush(Color.FromRgb(88, 140, 72)) };
        canvas.Children.Add(new Polygon
        {
            Points = shape.Opening.Select(p => P(p.U, p.V)).ToList(),
            Fill = new SolidColorBrush(Color.FromRgb(120, 176, 232))
        });
        foreach (var f in fronts.OrderBy(f => f.Corners.Average(c => c.X)).Reverse())
            canvas.Children.Add(new Polygon
            {
                Points = f.Corners.Select(c => P(c.Y - at.Y, c.Z - at.Z)).ToList(),
                Fill = new SolidColorBrush(Color.FromRgb(150, 146, 138)), Stroke = new SolidColorBrush(Color.FromRgb(96, 92, 86)), StrokeThickness = 0.6
            });
        return canvas;
    }

    /// <summary>Add Doorway / Doorway Shape: where it leads (Add only), the shape gallery, the size, the frame texture and
    /// (Add only) where it goes.</summary>
    private async Task<DoorwayChoice?> ShowDoorwayDialogAsync(string title, IReadOnlyList<DoorwayDestination>? destinations, AuthoredPortal? current, bool tileSelected)
    {
        var content = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
        var dialog = new Window { Name = "DoorwayDialog", Title = title, Width = 670, SizeToContent = SizeToContent.Height, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = content };
        ComboBox? destinationBox = null;
        if (destinations != null)
        {
            content.Children.Add(new TextBlock { Text = "Leads to", FontWeight = FontWeight.SemiBold });
            destinationBox = new ComboBox { Name = "DoorwayDestinationBox", ItemsSource = destinations, SelectedIndex = 0, MinWidth = 280 };
            content.Children.Add(destinationBox);
        }
        content.Children.Add(new TextBlock { Text = "Shape", FontWeight = FontWeight.SemiBold });
        DoorwayShape chosenShape = DoorwayShapes.Find(current?.Shape) ?? DoorwayShapes.StoneArch;
        WrapPanel gallery = new() { Name = "DoorwayShapeGallery", Orientation = Orientation.Horizontal, ItemSpacing = 8, LineSpacing = 8 };
        Dictionary<DoorwayShape, Button> buttons = [];
        TextBlock shapeNote = new() { TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(ModernMutedInk), FontSize = 12 };
        void Mark()
        {
            foreach (var (s, b) in buttons)
            {
                b.BorderBrush = new SolidColorBrush(s == chosenShape ? ModernBlue : ModernLine);
                b.BorderThickness = new Thickness(s == chosenShape ? 3 : 1);
            }
            shapeNote.Text = chosenShape.Description + $" Opening {chosenShape.Width:0} wide and {chosenShape.Height:0} high at normal size.";
        }
        foreach (var shape in DoorwayShapes.All)
        {
            StackPanel face = new() { Spacing = 3, HorizontalAlignment = HorizontalAlignment.Center };
            face.Children.Add(new Border { CornerRadius = new CornerRadius(4), ClipToBounds = true, Child = DoorwayShapePicture(shape, 76) });
            face.Children.Add(new TextBlock { Text = shape.Name, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center });
            Button button = new()
            {
                Name = "DoorwayShape-" + shape.Id, Content = face, Padding = new Thickness(5), Width = 92,
                Background = Brushes.Transparent, BorderBrush = new SolidColorBrush(ModernLine), BorderThickness = new Thickness(1)
            };
            ToolTip.SetTip(button, shape.Description);
            var picked = shape;
            button.Click += (_, _) => { chosenShape = picked; Mark(); };
            buttons[shape] = button;
            gallery.Children.Add(button);
        }
        content.Children.Add(gallery);
        content.Children.Add(shapeNote);
        Mark();
        ComboBox sizeBox = new()
        {
            Name = "DoorwaySize", ItemsSource = DoorwaySizes.Select(DoorwaySizeName).ToArray(), MinWidth = 220,
            SelectedIndex = Math.Max(0, Array.FindIndex(DoorwaySizes, s => Math.Abs(s - (current?.Scale ?? 1f)) < 0.01f))
        };
        content.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { new TextBlock { Text = "Size", VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.SemiBold }, sizeBox } });
        (string LevelKey, int TextureId)? texture = null;
        TextBlock textureName = new()
        {
            VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(ModernMutedInk),
            Text = current == null ? "Stone Hill's paving stones (the Stone ground)" : "The frame keeps its textures"
        };
        Button pickTexture = NewAsyncButton("Frame Texture…", async () =>
        {
            if (await ShowBuiltTextureGridAsync(dialog) is { } picked)
            {
                texture = picked;
                textureName.Text = $"{_retailCatalog.FindByKey(picked.LevelKey)?.DisplayName} texture {picked.TextureId + 1}" + (current == null ? "" : " (front and back)");
            }
        });
        pickTexture.Name = "DoorwayTexture";
        content.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { pickTexture, textureName } });
        RadioButton? atTile = null, atCamera = null;
        if (destinations != null)
        {
            atTile = new RadioButton { Name = "DoorwayAtTile", Content = "On the selected tile", GroupName = "DoorwayPlace", IsChecked = tileSelected, IsEnabled = tileSelected };
            atCamera = new RadioButton { Name = "DoorwayAtCamera", Content = "On the ground in front of the camera", GroupName = "DoorwayPlace", IsChecked = !tileSelected };
            content.Children.Add(new TextBlock { Text = "Place it", FontWeight = FontWeight.SemiBold });
            content.Children.Add(atTile);
            content.Children.Add(atCamera);
            content.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = new SolidColorBrush(ModernMutedInk),
                Text = "The doorway faces the camera: Spyro walks into it away from you, and comes back out of it when he leaves the level. " +
                       "In the game the opening shows the sky of the level it leads to."
            });
        }
        DoorwayChoice? result = null;
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        Button cancel = NewButton("Cancel"), confirm = NewButton(destinations != null ? "Add Doorway" : "Change Shape");
        cancel.Name = "DoorwayCancel"; confirm.Name = "DoorwayConfirm"; cancel.IsCancel = true;
        cancel.Click += (_, _) => dialog.Close();
        confirm.Click += (_, _) =>
        {
            int destination = (destinationBox?.SelectedItem as DoorwayDestination)?.LevelId ?? current?.DestinationLevelId ?? 0;
            result = new(destination, chosenShape.Id, DoorwaySizes[Math.Max(0, sizeBox.SelectedIndex)], texture, atCamera?.IsChecked == true);
            dialog.Close();
        };
        StyleModernPrimaryButton(confirm, ModernBlue, 130);
        actions.Children.Add(cancel); actions.Children.Add(confirm);
        content.Children.Add(actions);
        await dialog.ShowDialog(this);
        return result;
    }

    /// <summary>Create BIN with one of the new world's levels open: the New World is not built by Create BIN yet, so say so
    /// (its doorways are kept for when it is) instead of failing on the missing level.</summary>
    private bool RefuseNewWorldExport()
    {
        if (_freshSceneDocument?.WorldLevelId is not int world) return false;
        string name = _freshSceneDocument.Name;
        int doorways = world == NewWorldDocument.Homeworld ? _freshSceneDocument.Portals?.Count ?? 0 : 0;
        _statusText.Text = $"Create BIN can't build your new world yet, so {name} can't go into a game. " +
            (doorways > 0 ? $"Its {Count(doorways, "doorway is", "doorways are")} saved and will lead to the world's levels once the New World is part of Create BIN. " : "") +
            "To make a game now, open one of the game's levels or a level you built in a game slot, and Create BIN again.";
        return true;
    }
}
