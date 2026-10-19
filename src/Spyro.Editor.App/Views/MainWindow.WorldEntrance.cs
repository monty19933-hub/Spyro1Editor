using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

/// <summary>Terrain › World Entrance, in one of the game's own homeworlds: the doorway into the project's new world
/// (<see cref="NewWorldDocument"/>) placed in the world's entry homeworld — on the selected tile or in front of the camera,
/// then moved, turned, reshaped, retextured with the homeworld's own textures, or removed. It is saved as
/// <c>world-entrance.json</c> and shown over the level as a preview; Create BIN builds it into the homeworld
/// (<see cref="NativeWorldEntrance"/>). The card also says whether the homeworld has room for it.</summary>
public sealed partial class MainWindow
{
    private Border? _worldEntranceCard;
    private TextBlock? _worldEntranceNote, _worldEntranceRoom;
    private StackPanel? _worldEntranceTools;
    private Button? _worldEntrancePlace, _worldEntranceUseHere;
    private bool _worldEntranceBusy;
    private string _worldEntranceSignature = "", _worldEntranceRoomKey = "";
    private Task? _worldEntranceRoomCheck;
    private UniversalSourceLibrary? _worldEntranceOriginals;

    /// <summary>What the Place Entrance / Entrance Shape dialog answers: the shape, its size and (Place only) whether it goes
    /// in front of the camera rather than on the selected tile.</summary>
    internal sealed record WorldEntranceChoice(string ShapeId, float Scale, bool AtCamera);
    internal Func<WorldEntranceChoice?>? WorldEntranceChoiceForTesting { get; set; }
    /// <summary>Answers the texture grid in tests (a texture number of the homeworld, or null to cancel).</summary>
    internal Func<int?>? WorldEntranceTextureForTesting { get; set; }
    internal Func<bool>? WorldEntranceRemoveConfirmForTesting { get; set; }
    internal string WorldEntranceNoteForTesting => _worldEntranceNote?.Text ?? "";
    internal string WorldEntranceRoomForTesting => _worldEntranceRoom?.Text ?? "";
    internal Task WorldEntranceRoomCheckForTesting => _worldEntranceRoomCheck ?? Task.CompletedTask;
    internal Task PlaceWorldEntranceForTestingAsync() => PlaceWorldEntranceAsync();
    internal Task MoveWorldEntranceForTestingAsync(int dx, int dy, int dz) => MoveWorldEntranceAsync(dx, dy, dz);
    internal Task TurnWorldEntranceForTestingAsync(int steps) => TurnWorldEntranceAsync(steps);
    internal Task ReshapeWorldEntranceForTestingAsync() => ReshapeWorldEntranceAsync();
    internal Task RetextureWorldEntranceForTestingAsync(bool sides) => RetextureWorldEntranceAsync(sides ? DoorwayFramePart.Side : DoorwayFramePart.Face);
    internal Task RemoveWorldEntranceForTestingAsync() => RemoveWorldEntranceAsync();
    internal Task UseThisHomeworldForWorldForTestingAsync() => UseThisHomeworldForWorldAsync();

    private Control BuildTerrainWorldEntranceCard()
    {
        StackPanel content = new() { Name = "WorldEntrancePanel" };
        _worldEntranceNote = TerrainNote();
        _worldEntranceNote.Name = "WorldEntranceNote";
        content.Children.Add(_worldEntranceNote);
        _worldEntrancePlace = NewAsyncButton("Place Entrance…", PlaceWorldEntranceAsync);
        _worldEntrancePlace.Name = "WorldEntrancePlace";
        _worldEntrancePlace.HorizontalAlignment = HorizontalAlignment.Left;
        ToolTip.SetTip(_worldEntrancePlace, "Put the doorway into your new world here: choose its shape and size; it goes on the selected tile or in front of the camera, facing the camera.");
        content.Children.Add(_worldEntrancePlace);
        _worldEntranceUseHere = NewAsyncButton("Use This Homeworld", UseThisHomeworldForWorldAsync);
        _worldEntranceUseHere.Name = "WorldEntranceUseHere";
        _worldEntranceUseHere.HorizontalAlignment = HorizontalAlignment.Left;
        ToolTip.SetTip(_worldEntranceUseHere, "Make this homeworld the one your new world is entered from (the doorway placed in the other one is removed).");
        content.Children.Add(_worldEntranceUseHere);

        _worldEntranceTools = new StackPanel { Name = "WorldEntranceTools", Spacing = 8 };
        Grid move = new()
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 4
        };
        move.Children.Add(new TextBlock { Text = "Move", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        foreach (var (label, name, dx, dy, dz, tip, column) in new[]
        {
            ("◀", "West", -16, 0, 0, "Move the entrance 16 units west.", 1),
            ("▲", "North", 0, 16, 0, "Move the entrance 16 units north.", 2),
            ("▼", "South", 0, -16, 0, "Move the entrance 16 units south.", 3),
            ("▶", "East", 16, 0, 0, "Move the entrance 16 units east.", 4),
            ("Up", "Up", 0, 0, 8, "Raise the entrance 8 units.", 5),
            ("Down", "Down", 0, 0, -8, "Lower the entrance 8 units.", 6),
        })
        {
            Button button = NewAsyncButton(label, () => MoveWorldEntranceAsync(dx, dy, dz));
            button.Name = "WorldEntranceMove" + name;
            button.MinHeight = 30;
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            ToolTip.SetTip(button, tip);
            Grid.SetColumn(button, column);
            move.Children.Add(button);
        }
        _worldEntranceTools.Children.Add(move);
        Grid place = TwoColumnGrid(1);
        Button toTile = NewAsyncButton("To Selected Tile", WorldEntranceToTileAsync);
        toTile.Name = "WorldEntranceToTile";
        ToolTip.SetTip(toTile, "Stand the entrance on the middle of the tile you select (click a tile in the view first).");
        Button toCamera = NewAsyncButton("In Front of Camera", WorldEntranceToCameraAsync);
        toCamera.Name = "WorldEntranceToCamera";
        ToolTip.SetTip(toCamera, "Put the entrance on the ground in front of the 3D view's camera, facing the camera.");
        Place(place, toTile, 0, 0);
        Place(place, toCamera, 1, 0);
        _worldEntranceTools.Children.Add(place);
        Grid turn = new()
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 4
        };
        turn.Children.Add(new TextBlock { Text = "Turn", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) });
        foreach (var (label, name, steps, column) in new[] { ("⟲ 15°", "Left15", 1, 1), ("⟳ 15°", "Right15", -1, 2), ("⟲ 90°", "Left90", 6, 3), ("⟳ 90°", "Right90", -6, 4) })
        {
            Button button = NewAsyncButton(label, () => TurnWorldEntranceAsync(steps));
            button.Name = "WorldEntranceTurn" + name;
            button.MinHeight = 30;
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
            ToolTip.SetTip(button, steps > 0 ? "Turn the entrance anticlockwise (seen from above)." : "Turn the entrance clockwise (seen from above).");
            Grid.SetColumn(button, column);
            turn.Children.Add(button);
        }
        _worldEntranceTools.Children.Add(turn);
        Grid change = TwoColumnGrid(2);
        Button shape = NewAsyncButton("Shape…", ReshapeWorldEntranceAsync);
        shape.Name = "WorldEntranceShape";
        ToolTip.SetTip(shape, "Choose another shape or size for the entrance.");
        Button front = NewAsyncButton("Front Texture…", () => RetextureWorldEntranceAsync(DoorwayFramePart.Face));
        front.Name = "WorldEntranceFrontTexture";
        ToolTip.SetTip(front, "Choose one of this homeworld's own textures for the frame's front and back.");
        Button side = NewAsyncButton("Side Texture…", () => RetextureWorldEntranceAsync(DoorwayFramePart.Side));
        side.Name = "WorldEntranceSideTexture";
        ToolTip.SetTip(side, "Choose one of this homeworld's own textures for the frame's sides, top and inside.");
        Button remove = NewAsyncButton("Remove Entrance", RemoveWorldEntranceAsync);
        remove.Name = "WorldEntranceRemove";
        StyleModernSecondaryButton(remove, ModernRed);
        ToolTip.SetTip(remove, "Take the entrance out of this homeworld.");
        Place(change, shape, 0, 0);
        Place(change, remove, 1, 0);
        Place(change, front, 0, 1);
        Place(change, side, 1, 1);
        _worldEntranceTools.Children.Add(change);
        _worldEntranceRoom = TerrainNote();
        _worldEntranceRoom.Name = "WorldEntranceRoom";
        _worldEntranceTools.Children.Add(_worldEntranceRoom);
        content.Children.Add(_worldEntranceTools);

        _worldEntranceCard = TerrainCard("World Entrance", content);
        _worldEntranceCard.Name = "TerrainWorldEntranceCard";
        RefreshWorldEntranceCard();
        return _worldEntranceCard;
    }

    /// <summary>The project's new world and its entrance (null when unreadable or absent).</summary>
    private (NewWorldDocument? World, WorldEntranceDocument? Entrance) WorldEntranceProject()
    {
        if (string.IsNullOrEmpty(_workspace.RootPath)) return (null, null);
        try { return (NewWorldDocument.Read(_workspace.RootPath), WorldEntranceDocument.Read(_workspace.RootPath)); }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            Spyro.Editor.Core.Diagnostics.EditorDiagnostics.RecordWarning("World entrance", e.Message);
            return (null, null);
        }
    }

    /// <summary>The game's own homeworld open in the editor (not a level built in its place), else null.</summary>
    private LevelDefinition? OpenOriginalHomeworld() =>
        _freshSceneDocument == null && _currentLevel is { } level && NewWorldDocument.HomeworldKeys.Contains(level.Key) ? level : null;

    private string WorldEntranceName(NewWorldDocument world) => world.Name;

    private static string DescribeWorldEntrance(WorldEntranceDocument e)
    {
        string shape = DoorwayShapes.Find(e.Shape)?.Name ?? e.Shape;
        string size = Math.Abs(e.Scale - 1) > 0.01f ? $" ({DoorwaySizeName(e.Scale).Split(' ')[0].ToLowerInvariant()})" : "";
        return $"{shape}{size}, Spyro walks in heading {CompassWord(e.Heading)}, bottom middle at {e.Position.X:0}, {e.Position.Y:0}, height {e.Position.Z:0}";
    }

    private void RefreshWorldEntranceCard()
    {
        if (_worldEntranceCard == null || _worldEntranceNote == null || _worldEntranceTools == null || _worldEntrancePlace == null || _worldEntranceUseHere == null)
            return;
        var level = OpenOriginalHomeworld();
        var (world, entrance) = level == null ? (null, null) : WorldEntranceProject();
        _worldEntranceCard.IsVisible = level != null && world != null;
        if (level == null || world == null)
        {
            _viewport.SetWorldEntrancePreview([], []);
            _worldEntranceSignature = "";
            return;
        }
        bool here = world.EntryHomeworldKey == level.Key;
        string entry = _retailCatalog.FindByKey(world.EntryHomeworldKey)?.DisplayName ?? world.EntryHomeworldKey;
        var mine = here && entrance?.HomeworldKey == level.Key ? entrance : null;
        _worldEntranceUseHere.IsVisible = !here;
        _worldEntrancePlace.IsVisible = here;
        _worldEntrancePlace.IsEnabled = !_worldEntranceBusy;
        _worldEntrancePlace.Content = mine == null ? "Place Entrance…" : "Place Again…";
        _worldEntranceTools.IsVisible = mine != null;
        _worldEntranceTools.IsEnabled = !_worldEntranceBusy;
        _worldEntranceNote.Text = !here
            ? $"{WorldEntranceName(world)} is entered from {entry}. Open {entry} to place its doorway there, or Use This Homeworld."
            : mine == null
                ? $"{WorldEntranceName(world)} has no doorway yet. Place Entrance… puts it in {level.DisplayName}: on the selected tile or in front of the camera. " +
                  $"{level.DisplayName} keeps its own doorways."
                : $"The doorway to {WorldEntranceName(world)}: {DescribeWorldEntrance(mine)}. It is drawn over the level here and built into {level.DisplayName} by Create BIN; " +
                  "in the game the opening shows your world's sky.";
        string signature = mine == null ? "" : System.Text.Json.JsonSerializer.Serialize(mine);
        if (signature != _worldEntranceSignature)
        {
            _worldEntranceSignature = signature;
            if (mine == null) _viewport.SetWorldEntrancePreview([], []);
            else
            {
                var shape = DoorwayShapes.Find(mine.Shape) ?? DoorwayShapes.StoneArch;
                var frame = DoorwayShapes.Frame(shape, mine.Scale, mine.Position, mine.Heading);
                var opening = DoorwayShapes.OutlineInLevel(shape, mine.Scale, mine.Position, mine.Heading).Select(p => new Vector3f((float)p.X, (float)p.Y, (float)p.Z)).ToArray();
                _viewport.SetWorldEntrancePreview(frame.Select(f => (f.Corners, f.Part == DoorwayFramePart.Face)).ToArray(), opening);
            }
        }
        if (mine == null && _worldEntranceRoom != null) _worldEntranceRoom.Text = "";
        else if (mine != null && signature + "|" + GameRam.Mode != _worldEntranceRoomKey) CheckWorldEntranceRoom(level, world, mine, signature + "|" + GameRam.Mode);
    }

    /// <summary>Builds the entrance into the game's own homeworld data (from the disc, without the project's other changes to
    /// it) in the background and says which sky fits and how much memory is left, or why it does not fit.</summary>
    private void CheckWorldEntranceRoom(LevelDefinition level, NewWorldDocument world, WorldEntranceDocument entrance, string key)
    {
        _worldEntranceRoomKey = key;
        if (_worldEntranceRoom == null) return;
        _worldEntranceRoom.Text = $"Checking {level.DisplayName}'s room for the doorway…";
        string root = _workspace.RootPath, disc = ResolveUniversalSourceDisc();
        var spec = entrance.Spec();
        var mode = GameRam.Mode;
        _worldEntranceRoomCheck = Task.Run(() =>
        {
            try
            {
                if (string.IsNullOrEmpty(disc) || !File.Exists(disc)) return "Choose the original Spyro the Dragon (USA) disc in the editor's settings to check the room for the doorway.";
                UniversalSourceLibrary.RequireOriginal(disc);
                var originals = _worldEntranceOriginals is { } cached && cached.ImagePath == disc ? cached : _worldEntranceOriginals = new UniversalSourceLibrary(disc, _retailCatalog);
                string scenePath = SceneDocumentStore.PathForTarget(root, NewWorldDocument.KeyFor(NewWorldDocument.Homeworld));
                byte[] sky = File.Exists(scenePath) ? NativeWorldEntrance.SceneSky(disc, SceneDocument.Read(scenePath)) : NativeWorldEntrance.PlainSky(new ColorRgba(120, 170, 230));
                var image = originals.Disc;
                using var scope = GameRam.Use(mode);
                var r = NativeWorldEntrance.Apply(originals, _retailCatalog, spec, image.EntryBytes(level.SourceWadEntry), image.EntryBytes(level.SourceWadEntry - 1), image.Exe, sky);
                string skyText = r.Sky switch
                {
                    WorldEntranceSky.Full => $"the whole of {world.Name}'s sky ({r.SkyBytes:N0} bytes)",
                    WorldEntranceSky.Reduced => $"the part of {world.Name}'s sky near the horizon ({r.SkyBytes:N0} of {sky.Length:N0} bytes)",
                    _ => "one colour of your world's sky (there is no room for more)",
                };
                return $"There is room: the doorway carries {skyText} and {level.DisplayName} keeps {r.RoomLeft:N0} bytes of the console's memory " +
                       "(measured without your other changes to it).";
            }
            catch (Exception e) when (e is InvalidDataException or IOException or InvalidOperationException or UnauthorizedAccessException)
            { return e.Message; }
        }).ContinueWith(t => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_worldEntranceRoomKey == key && _worldEntranceRoom != null) _worldEntranceRoom.Text = t.IsFaulted ? t.Exception?.GetBaseException().Message ?? "" : t.Result;
        }));
    }

    private async Task SaveWorldEntranceAsync(WorldEntranceDocument? entrance, string message)
    {
        try
        {
            if (entrance == null) WorldEntranceDocument.Delete(_workspace.RootPath);
            else await entrance.SaveAsync(_workspace.RootPath);
            _statusText.Text = message;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        { _statusText.Text = "Could not save the world entrance: " + e.Message; }
        RefreshWorldEntranceCard();
    }

    /// <summary>The entrance placed in the homeworld open now (null, with a status message, when there is none).</summary>
    private (LevelDefinition Level, NewWorldDocument World, WorldEntranceDocument Entrance)? CurrentWorldEntrance()
    {
        if (_worldEntranceBusy || OpenOriginalHomeworld() is not { } level) return null;
        var (world, entrance) = WorldEntranceProject();
        if (world == null) { _statusText.Text = "Make a world first with File ▾ › New World…."; return null; }
        if (entrance == null || entrance.HomeworldKey != level.Key || world.EntryHomeworldKey != level.Key)
        { _statusText.Text = "Place the entrance first (Place Entrance…)."; return null; }
        return (level, world, entrance);
    }

    private async Task PlaceWorldEntranceAsync()
    {
        if (_worldEntranceBusy || OpenOriginalHomeworld() is not { } level) return;
        var (world, entrance) = WorldEntranceProject();
        if (world == null) { _statusText.Text = "Make a world first with File ▾ › New World…."; return; }
        if (world.EntryHomeworldKey != level.Key) { _statusText.Text = $"{world.Name} is entered from another homeworld. Use This Homeworld first."; return; }
        TerrainPolygon? tile = _selectedTerrain is { IsTerrainRemoved: false } t ? t : null;
        var current = entrance?.HomeworldKey == level.Key ? entrance : null;
        WorldEntranceChoice? choice = WorldEntranceChoiceForTesting != null ? WorldEntranceChoiceForTesting()
            : await ShowWorldEntranceDialogAsync(current == null ? "Place Entrance" : "Place Entrance Again", current, place: true, tile != null);
        if (choice == null) { _statusText.Text = "The entrance was not placed."; return; }
        _worldEntranceBusy = true;
        try
        {
            if (!TryDoorwaySpot(choice.AtCamera ? null : tile, out Vector3f position, out int heading, out string where))
            {
                _statusText.Text = choice.AtCamera
                    ? "The entrance needs ground in front of the camera: turn the 3D view towards the floor, or select a tile and place it there."
                    : "Select the tile the entrance should stand on first, or choose \"In front of the camera\".";
                return;
            }
            var shape = DoorwayShapes.Find(choice.ShapeId) ?? DoorwayShapes.StoneArch;
            var placed = new WorldEntranceDocument(1, level.Key, position, heading, shape.Id, choice.Scale)
            { FaceTexture = current?.FaceTexture ?? -1, SideTexture = current?.SideTexture ?? -1 };
            await SaveWorldEntranceAsync(placed,
                $"Placed a {shape.Name.ToLowerInvariant()} doorway to {world.Name} {where} in {level.DisplayName}. Spyro walks in heading {CompassWord(heading)}; " +
                "Create BIN builds it into the homeworld (its frame uses the homeworld's own arch texture until you choose another).");
        }
        finally { _worldEntranceBusy = false; RefreshWorldEntranceCard(); }
    }

    private async Task ChangeWorldEntranceAsync(Func<WorldEntranceDocument, WorldEntranceDocument> change, Func<WorldEntranceDocument, string> done)
    {
        if (CurrentWorldEntrance() is not { } current) return;
        var changed = change(current.Entrance);
        try { changed.Validate(); }
        catch (InvalidDataException e) { _statusText.Text = e.Message; return; }
        var shape = DoorwayShapes.Find(changed.Shape) ?? DoorwayShapes.StoneArch;
        if (DoorwayShapes.Frame(shape, changed.Scale, changed.Position, changed.Heading).SelectMany(f => f.Corners).Any(c => c.X is < 0 or > 16383 || c.Y is < 0 or > 16383 || c.Z is < 0 or > 16383))
        { _statusText.Text = "There is no room for the entrance there: part of it would be outside the game's map. Move it further in."; return; }
        await SaveWorldEntranceAsync(changed, done(changed));
    }

    private Task MoveWorldEntranceAsync(int dx, int dy, int dz) =>
        ChangeWorldEntranceAsync(e => e with { Position = new(e.Position.X + dx, e.Position.Y + dy, e.Position.Z + dz) },
            e => "Moved the entrance " + (dz != 0 ? (dz > 0 ? "up" : "down") : dx < 0 ? "west" : dx > 0 ? "east" : dy > 0 ? "north" : "south") +
                 $" (bottom middle now at {e.Position.X:0}, {e.Position.Y:0}, height {e.Position.Z:0}).");

    private async Task WorldEntranceToTileAsync()
    {
        if (_selectedTerrain is not { IsTerrainRemoved: false } tile) { _statusText.Text = "Click the tile the entrance should stand on, then To Selected Tile."; return; }
        if (!TryDoorwaySpot(tile, out Vector3f position, out _, out _)) return;
        await ChangeWorldEntranceAsync(e => e with { Position = position }, _ => "The entrance now stands on the selected tile.");
    }

    private async Task WorldEntranceToCameraAsync()
    {
        if (!TryDoorwaySpot(null, out Vector3f position, out int heading, out _))
        { _statusText.Text = "Turn the 3D view towards the floor first: the entrance goes on the ground in front of the camera."; return; }
        await ChangeWorldEntranceAsync(e => e with { Position = position, Heading = heading }, _ => "The entrance now stands in front of the camera, facing it.");
    }

    private Task TurnWorldEntranceAsync(int steps) =>
        ChangeWorldEntranceAsync(e => e with { Heading = SceneDoorways.Turn(e.Heading, steps) },
            e => $"Turned the entrance: Spyro now walks in heading {CompassWord(e.Heading)} ({SceneDoorways.Degrees(e.Heading)}°).");

    private async Task ReshapeWorldEntranceAsync()
    {
        if (CurrentWorldEntrance() is not { } current) return;
        WorldEntranceChoice? choice = WorldEntranceChoiceForTesting != null ? WorldEntranceChoiceForTesting() : await ShowWorldEntranceDialogAsync("Entrance Shape", current.Entrance, place: false, false);
        if (choice == null) return;
        var shape = DoorwayShapes.Find(choice.ShapeId) ?? DoorwayShapes.StoneArch;
        await ChangeWorldEntranceAsync(e => e with { Shape = shape.Id, Scale = choice.Scale },
            _ => $"The entrance is a {shape.Name.ToLowerInvariant()} now ({DoorwaySizeName(choice.Scale).Split(' ')[0].ToLowerInvariant()} size).");
    }

    private async Task RetextureWorldEntranceAsync(DoorwayFramePart part)
    {
        if (CurrentWorldEntrance() is not { } current) return;
        int? texture;
        if (WorldEntranceTextureForTesting != null) texture = WorldEntranceTextureForTesting();
        else texture = await ShowBuiltTextureGridAsync(this, current.Level.Key) is { } picked ? picked.TextureId : null;
        if (texture is not int id) return;
        string name = $"{current.Level.DisplayName} texture {id + 1}";
        await ChangeWorldEntranceAsync(e => part == DoorwayFramePart.Face ? e with { FaceTexture = id } : e with { SideTexture = id },
            _ => $"The entrance's frame {(part == DoorwayFramePart.Face ? "front and back show" : "sides show")} {name} now (in the game; the preview here stays plain).");
    }

    private async Task RemoveWorldEntranceAsync()
    {
        if (CurrentWorldEntrance() is not { } current) return;
        bool confirmed = WorldEntranceRemoveConfirmForTesting != null ? WorldEntranceRemoveConfirmForTesting()
            : await ShowConfirmationAsync("WorldEntranceRemoveDialog", "Remove Entrance", $"Remove the doorway to {current.World.Name}?",
                $"Without it the game has no way into your world. You can place it again with Place Entrance….", "Remove Entrance");
        if (!confirmed) return;
        await SaveWorldEntranceAsync(null, $"Removed the doorway to {current.World.Name} from {current.Level.DisplayName}.");
    }

    private async Task UseThisHomeworldForWorldAsync()
    {
        if (_worldEntranceBusy || OpenOriginalHomeworld() is not { } level) return;
        var (world, entrance) = WorldEntranceProject();
        if (world == null || world.EntryHomeworldKey == level.Key) return;
        try
        {
            await (world with { EntryHomeworldKey = level.Key }).SaveAsync(_workspace.RootPath);
            if (entrance != null && entrance.HomeworldKey != level.Key) WorldEntranceDocument.Delete(_workspace.RootPath);
            _statusText.Text = $"{world.Name} is now entered from {level.DisplayName}. Place Entrance… puts its doorway here.";
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        { _statusText.Text = "Could not change the world's homeworld: " + e.Message; }
        RefreshWorldEntranceCard();
    }

    /// <summary>Place Entrance / Entrance Shape: the shape gallery, the size and (Place only) where it goes.</summary>
    private async Task<WorldEntranceChoice?> ShowWorldEntranceDialogAsync(string title, WorldEntranceDocument? current, bool place, bool tileSelected)
    {
        var content = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
        var dialog = new Window { Name = "WorldEntranceDialog", Title = title, Width = 670, SizeToContent = SizeToContent.Height, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = content };
        content.Children.Add(new TextBlock { Text = "Shape", FontWeight = FontWeight.SemiBold });
        DoorwayShape chosen = DoorwayShapes.Find(current?.Shape) ?? DoorwayShapes.StoneArch;
        WrapPanel gallery = new() { Name = "WorldEntranceShapeGallery", Orientation = Orientation.Horizontal, ItemSpacing = 8, LineSpacing = 8 };
        Dictionary<DoorwayShape, Button> buttons = [];
        TextBlock note = new() { TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(ModernMutedInk), FontSize = 12 };
        void Mark()
        {
            foreach (var (s, b) in buttons)
            {
                b.BorderBrush = new SolidColorBrush(s == chosen ? ModernBlue : ModernLine);
                b.BorderThickness = new Thickness(s == chosen ? 3 : 1);
            }
            note.Text = chosen.Description + $" Opening {chosen.Width:0} wide and {chosen.Height:0} high at normal size.";
        }
        foreach (var shape in DoorwayShapes.All)
        {
            StackPanel face = new() { Spacing = 3, HorizontalAlignment = HorizontalAlignment.Center };
            face.Children.Add(new Border { CornerRadius = new CornerRadius(4), ClipToBounds = true, Child = DoorwayShapePicture(shape, 76) });
            face.Children.Add(new TextBlock { Text = shape.Name, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center });
            Button button = new()
            {
                Name = "WorldEntranceShape-" + shape.Id, Content = face, Padding = new Thickness(5), Width = 92,
                Background = Brushes.Transparent, BorderBrush = new SolidColorBrush(ModernLine), BorderThickness = new Thickness(1)
            };
            var picked = shape;
            button.Click += (_, _) => { chosen = picked; Mark(); };
            buttons[shape] = button;
            gallery.Children.Add(button);
        }
        content.Children.Add(gallery);
        content.Children.Add(note);
        Mark();
        ComboBox size = new()
        {
            Name = "WorldEntranceSize", ItemsSource = DoorwaySizes.Select(DoorwaySizeName).ToArray(), MinWidth = 220,
            SelectedIndex = Math.Max(0, Array.FindIndex(DoorwaySizes, s => Math.Abs(s - (current?.Scale ?? 1f)) < 0.01f))
        };
        content.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { new TextBlock { Text = "Size", VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeight.SemiBold }, size } });
        RadioButton? atCamera = null;
        if (place)
        {
            var atTile = new RadioButton { Name = "WorldEntranceAtTile", Content = "On the selected tile", GroupName = "WorldEntrancePlace", IsChecked = tileSelected, IsEnabled = tileSelected };
            atCamera = new RadioButton { Name = "WorldEntranceAtCamera", Content = "On the ground in front of the camera", GroupName = "WorldEntrancePlace", IsChecked = !tileSelected };
            content.Children.Add(new TextBlock { Text = "Place it", FontWeight = FontWeight.SemiBold });
            content.Children.Add(atTile);
            content.Children.Add(atCamera);
            content.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = new SolidColorBrush(ModernMutedInk),
                Text = "The doorway faces the camera: Spyro walks into it away from you to reach your world, and comes back out of it when he " +
                       "leaves your world's homeworld through its doorway home. Give it open, level ground in front."
            });
        }
        WorldEntranceChoice? result = null;
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        Button cancel = NewButton("Cancel"), confirm = NewButton(place ? "Place Entrance" : "Change Shape");
        cancel.Name = "WorldEntranceCancel"; confirm.Name = "WorldEntranceConfirm"; cancel.IsCancel = true;
        cancel.Click += (_, _) => dialog.Close();
        confirm.Click += (_, _) => { result = new(chosen.Id, DoorwaySizes[Math.Max(0, size.SelectedIndex)], atCamera?.IsChecked == true); dialog.Close(); };
        StyleModernPrimaryButton(confirm, ModernBlue, 130);
        actions.Children.Add(cancel); actions.Children.Add(confirm);
        content.Children.Add(actions);
        await dialog.ShowDialog(this);
        return result;
    }
}
