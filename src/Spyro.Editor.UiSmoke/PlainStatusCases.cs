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
        string[] forbidden = ["terrain face", "vertices", "vertex", " Z +", "moby"];
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
