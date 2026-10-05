using System.Reflection;
using Spyro.Editor.App.Views;

/// <summary>
/// The status bar of the shipping editor says things the way a player would. These are the working
/// messages the Terrain and Objects tabs produce and what the bar must show for them.
/// Usage: UiSmoke --plain-status-only
/// </summary>
internal static class PlainStatusCases
{
    internal static void Run()
    {
        MethodInfo plain = typeof(MainWindow).GetMethod("PlainStatusForRelease", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("PlainStatusForRelease is missing.");
        (string Raw, string Shown)[] cases =
        [
            ("Raised terrain face 909 by Z +32 without flattening its existing shape. Save Terrain Changes and Create BIN will include the moved vertices.",
             "Raised the tile by 32."),
            ("Lowered terrain face 12 by Z -32 without flattening its existing shape. Save Terrain Changes and Create BIN will include the moved vertices.",
             "Lowered the tile by 32."),
            ("Raise terrain stroke finished: changed 639 vertices across 167 terrain faces.",
             "Raise brush: reshaped 167 tiles. Undo Stroke takes it back."),
            ("Smooth terrain stroke finished: changed 1 vertex across 1 terrain face.",
             "Smooth brush: reshaped 1 tile. Undo Stroke takes it back."),
            ("Undid a terrain brush stroke across 167 terrain faces.", "Undid the brush stroke (167 tiles)."),
            ("Undid a terrain brush stroke across 167 terrain faces. 2 more stroke undo(s) available.",
             "Undid the brush stroke (167 tiles). 2 earlier strokes can still be undone."),
            ("Redid a terrain brush stroke across 1 terrain face.", "Redid the brush stroke (1 tile)."),
            ("There is no terrain brush stroke to undo yet.", "No brush stroke to undo."),
            ("Moved terrain face 44 by X +64, Y -32. Save Terrain and Create BIN will include the moved vertices.", "Moved the tile."),
            ("Moved terrain face 44: X +64, Y -32, Z +0. Save Terrain and Create BIN will include the moved vertices.", "Moved the tile."),
            ("Staged terrain face 44 as an added copy. Keep dragging to place the copied terrain; the original source stays visible as a ghost.",
             "Added a copy of the tile. Drag the copy to where it should go."),
            ("Select a terrain face before using height nudges.", "Select a tile first, then use Raise or Lower."),
            ("Select a terrain face before moving terrain.", "Select a tile before moving terrain."),
            ("That terrain face is no longer available in the loaded level.", "That tile is no longer available in the loaded level."),
            ("Updated terrain face 7.", "Tile updated."),
            ("Set Red gem (1) Z to 447.0.", "Set the height of Red gem (1) to 447.0."),
            ("Set Dragon Z to 512.5 with 2 linked object(s).", "Set the height of Dragon to 512.5. 2 linked objects moved with it."),
            ("Snapped Sheep to terrain Z.", "Sheep now sits on the ground."),
            ("Snapped Dragon and 1 linked object(s) to terrain Z.", "Dragon and 1 linked object now sit on the ground."),
            ("Terrain Z snapping is enabled for Sheep.", "Sheep will stay on the ground when you move it."),
            ("Terrain Z snapping is off for Sheep; horizontal movement will preserve its current Z.", "Sheep keeps its height when you move it."),
            ("Removed Dragon and 2 linked object(s). Use Undo last removal to restore them.", "Removed Dragon and 2 linked objects. Use Undo last removal to restore them."),
            ("Restored Dragon and 1 linked object(s).", "Restored Dragon and 1 linked object."),
            // Already plain messages pass through untouched.
            ("Removed Red gem (1). Undo last removal brings it back.", "Removed Red gem (1). Undo last removal brings it back."),
            ("Tile selected.", "Tile selected."),
            ("Added Sign.", "Added Sign."),
            ("", ""),
        ];
        int checks = 0;
        foreach ((string raw, string shown) in cases)
        {
            string actual = (string)plain.Invoke(null, [raw])!;
            if (actual != shown)
                throw new InvalidOperationException($"Plain status: \"{raw}\" is shown as \"{actual}\", expected \"{shown}\".");
            checks++;
        }
        string[] forbidden = ["terrain face", "vertices", "vertex", " Z +", "moby", "(s)", "terrain Z", " Z to "];
        foreach ((string raw, _) in cases)
        {
            string actual = (string)plain.Invoke(null, [raw])!;
            foreach (string word in forbidden)
                if (actual.Contains(word, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Plain status still says \"{word}\": {actual}");
            checks++;
        }
        Console.WriteLine($"PLAIN_STATUS_OK checks={checks}");
    }
}
