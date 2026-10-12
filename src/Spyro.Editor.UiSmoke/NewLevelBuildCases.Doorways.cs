using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Scene;

/// <summary>Terrain › Doorways on a real project's rebuilt homeworld (Lost Fleet in Artisans' place), as a user would:
/// the four original doorways get shapes (their frames are found and replaced, keeping their textures), one gets new side
/// textures, the fifth is removed and a new Hex Gate doorway to the same level is added on the tile where it stood,
/// turned and moved with the tool's buttons. Then <c>export</c> makes the game for the emulator.
/// <c>doorway-shots</c> takes editor pictures of each doorway and of the Add Doorway window.</summary>
internal static partial class NewLevelBuildCases
{
    private static void Run(Task task, string label, int seconds = 300)
    {
        Wait(() => task.IsCompleted, label, seconds);
        if (task.IsFaulted) throw task.Exception!;
    }

    private static string DoorwayLine(AuthoredPortal p) =>
        $"{p.DestinationLevelId}:{p.Position.X:0},{p.Position.Y:0},{p.Position.Z:0},{p.Heading}:{p.Shape ?? "original"}";

    private static int TileUnder(MainWindow window, float x, float y, float z)
    {
        var g = window.CaptureEditorShellSessionSnapshotForTesting().CurrentGeometry!;
        int best = -1; float bestDz = float.MaxValue;
        for (int i = 0; i < g.Polygons.Count; i++)
        {
            var p = g.Polygons[i];
            if (p.IsTerrainRemoved || p.Points.Count < 3) continue;
            bool inside = false;
            for (int a = 0, b = p.Points.Count - 1; a < p.Points.Count; b = a++)
                if ((p.Points[a].Y > y) != (p.Points[b].Y > y) &&
                    x < (p.Points[b].X - p.Points[a].X) * (y - p.Points[a].Y) / (p.Points[b].Y - p.Points[a].Y) + p.Points[a].X) inside = !inside;
            if (!inside) continue;
            float dz = Math.Abs(p.ZValues.Average() - z) + (p.MaxZ - p.MinZ) / 4;
            if (dz < bestDz) { bestDz = dz; best = i; }
        }
        return best;
    }

    private static void DoorwaysStep(MainWindow window, string outDir)
    {
        Load(window, "artisans");
        SelectTab(window, "Terrain"); Pump(20);
        var scene = window.FreshSceneForTesting ?? throw new InvalidOperationException("Artisans is not a level you built.");
        var card = Find<Control>(window, "TerrainDoorwaysCard");
        var list = Find<ListBox>(window, "DoorwayList");
        Console.WriteLine($"doorways card visible {card.IsVisible}: {Find<TextBlock>(window, "DoorwaysNote").Text}");
        foreach (var row in list.ItemsSource!.Cast<object>()) Console.WriteLine("  " + row);
        if (!card.IsVisible || list.ItemsSource!.Cast<object>().Count() != scene.Portals!.Count) throw new InvalidOperationException("The Doorways card does not list the level's doorways.");
        Snapshot(window, Path.Combine(outDir, "editor-doorways-before.png"));

        // The four original doorways get shapes: their old frames are found and replaced.
        foreach (var (dest, shape) in new[] { (11, "stone-arch"), (12, "pointed-arch"), (13, "square-gate"), (14, "round-ring") })
        {
            window.SelectDoorwayForTesting(dest); Pump(5);
            int faces = window.FreshSceneForTesting!.Surfaces.Count;
            window.DoorwayChoiceForTesting = () => new(dest, shape, 1f, null, false);
            Run(window.ReshapeSelectedDoorwayForTestingAsync(), "reshape " + dest);
            var now = window.FreshSceneForTesting!;
            var portal = SceneDoorways.Find(now, dest)!;
            Console.WriteLine($"  {dest} -> {portal.Shape}: frame {portal.FrameFaces?.Count} faces, level {faces} -> {now.Surfaces.Count} faces; {Status(window)}");
            if (portal.Shape != shape || portal.FrameFaces == null || portal.FrameFaces.Any(id => now.Surfaces.All(s => s.Id != id)))
                throw new InvalidOperationException("The doorway did not get its shape and frame.");
        }
        // New side textures for the square gate.
        window.SelectDoorwayForTesting(13);
        window.DoorwayChoiceForTesting = () => new(13, "square-gate", 1f, ("drycanyon", 20), false);
        Run(window.RetextureSelectedDoorwayForTestingAsync(sides: true), "side texture");
        Console.WriteLine("  side texture: " + Status(window));

        // The fifth doorway is removed with its frame, then a new one to the same level is added where it stood.
        var old15 = SceneDoorways.Find(window.FreshSceneForTesting!, 15)!;
        window.SelectDoorwayForTesting(15);
        window.DoorwayRemoveConfirmForTesting = () => true;
        Run(window.RemoveSelectedDoorwayForTestingAsync(), "remove 15");
        var removed = window.FreshSceneForTesting!;
        Console.WriteLine($"  removed 15: {Status(window)} ({removed.Portals?.Count} doorways, {removed.Surfaces.Count} faces)");
        if (SceneDoorways.Find(removed, 15) != null) throw new InvalidOperationException("The doorway was not removed.");

        int tile = TileUnder(window, old15.Position.X, old15.Position.Y, old15.Position.Z);
        if (tile < 0) throw new InvalidOperationException("No tile under the old doorway.");
        var polygon = window.CaptureEditorShellSessionSnapshotForTesting().CurrentGeometry!.Polygons[tile];
        Console.WriteLine($"  tile {tile} under the old doorway: z {polygon.MinZ:0}..{polygon.MaxZ:0}, corners " + string.Join(" ", polygon.Points.Select(q => $"({q.X:0},{q.Y:0})")));
        SetField(window, "_selectedTerrain", polygon); SetField(window, "_selectedTerrainIndex", tile); SetField(window, "_selectedTerrainPointIndex", -1);
        window.DoorwayChoiceForTesting = () => new(15, "hex-gate", 1f, ("treetops", 40), false);
        Run(window.AddDoorwayForTestingAsync(), "add doorway");
        Console.WriteLine("  add: " + Status(window));
        var added = SceneDoorways.Find(window.FreshSceneForTesting!, 15) ?? throw new InvalidOperationException("No doorway was added: " + Status(window));
        Console.WriteLine("  added " + DoorwayLine(added));
        // Turn it to face the way the old one did (15° steps), then exercise the move buttons (net: back where it was).
        int steps = (int)Math.Round((old15.Heading - added.Heading) * 24 / 256.0);
        steps = ((steps % 24) + 36) % 24 - 12;
        Run(window.TurnSelectedDoorwayForTestingAsync(steps), "turn");
        Console.WriteLine("  turn: " + Status(window));
        foreach (var (dx, dy, dz) in new[] { (-16, 0, 0), (-16, 0, 0), (16, 0, 0), (16, 0, 0), (0, 16, 0), (0, -16, 0), (0, 0, 8), (0, 0, -8) })
            Run(window.MoveSelectedDoorwayForTestingAsync(dx, dy, dz), "move");
        Console.WriteLine("  moves: " + Status(window));
        var final = window.FreshSceneForTesting!;
        var hex = SceneDoorways.Find(final, 15)!;
        if (hex.Position != added.Position || Math.Abs(hex.Heading - old15.Heading) > 2)
            throw new InvalidOperationException($"The new doorway is not where it should be: {DoorwayLine(hex)}");
        Save(window);
        Console.WriteLine("final doorways (dest:x,y,z,heading:shape):");
        foreach (var p in final.Portals!) Console.WriteLine("  " + DoorwayLine(p) + $" frame {p.FrameFaces?.Count}");
        Snapshot(window, Path.Combine(outDir, "editor-doorways-after.png"));
        Console.WriteLine("NEW_LEVEL_DOORWAYS_OK");
    }

    /// <summary>Editor pictures: the 3D view aimed at each doorway's frame, and the Add Doorway window with its gallery.</summary>
    private static void DoorwayShots(MainWindow window, string outDir)
    {
        Load(window, "artisans");
        SelectTab(window, "Terrain"); Pump(20);
        var scene = window.FreshSceneForTesting!;
        var viewport = Get<EditorViewport>(window, "_viewport");
        foreach (var portal in scene.Portals ?? [])
        {
            window.SelectDoorwayForTesting(portal.DestinationLevelId); Pump(10);
            // From where Spyro walks up to it, a little above his head, looking at the middle of the opening.
            var (fx, fy) = DoorwayShapes.Facing(portal.Heading);
            var p = portal.Position;
            viewport.LookFromForTesting(p.X - fx * 420, p.Y - fy * 420, p.Z + 150, p.X, p.Y, p.Z + 100); Pump(30);
            Snapshot(window, Path.Combine(outDir, $"editor-doorway-{portal.DestinationLevelId}-{portal.Shape ?? "original"}.png"));
            // The selection highlight hides the textures: a second picture without it.
            viewport.ClearTerrainMultiSelection(); Pump(10);
            Snapshot(window, Path.Combine(outDir, $"editor-doorway-{portal.DestinationLevelId}-{portal.Shape ?? "original"}-plain.png"));
        }
        // The Add Doorway window: shown without a free destination it would refuse, so open the Shape window instead.
        if (scene.Portals is { Count: > 0 } portals)
        {
            window.SelectDoorwayForTesting(portals[0].DestinationLevelId);
            var show = (Task)Invoke(window, "ShowDoorwayDialogAsync", "Doorway Shape", null, portals[0], false)!;
            Wait(() => window.OwnedWindows.Any(w => w.Name == "DoorwayDialog"), "doorway dialog", 30);
            var dialog = window.OwnedWindows.First(w => w.Name == "DoorwayDialog");
            Pump(30);
            Snapshot(dialog, Path.Combine(outDir, "editor-doorway-shape-window.png"));
            dialog.Close(); Pump(10);
            Wait(() => show.IsCompleted, "dialog closed", 30);
        }
        Console.WriteLine("NEW_LEVEL_DOORWAY_SHOTS_OK");
    }
}
