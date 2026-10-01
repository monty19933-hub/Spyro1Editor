# Spyro Editor Beta V6 — build 54

## Start editing

Open **Spyro Editor.app** on an Apple Silicon Mac, or **Launch Spyro Editor.bat**
on Windows x64. The application is self-contained. Choose **Open BIN/CUE**, select
your unchanged USA Spyro the Dragon disc, and pick a level. Projects and backups
are stored outside the application folder.

Select an object or terrain face in the viewport. Use the toolbar to move, copy,
remove or edit it. **Undo** and **Redo** let you revise changes. **Save Level**
stores the current edits; **Create BIN** combines the project's saved changes
into a separate game image. Open that exported CUE in your emulator.

In existing retail levels, terrain export supports height changes and texture
painting. General terrain addition and removal currently belong to fresh levels.

## Create a fresh level

Choose **File → New Level**. Your level opens in the same viewport and inspector
as the existing levels, with a starter floor and no inherited level objects.
Use **Add Terrain**, **Remove Terrain**, **Move Face**, **Edit Point** and the
brush tools to shape it. **Paint Face** opens a material chooser with previews.
Use the object browser to place Mobys, and set the player spawn in the inspector.
Save the level before exporting.

**Create BIN** includes the saved fresh level together with saved retail edits.
In the exported game, choose **New Game** and an empty save slot to enter your
fresh level. Existing retail save games keep their native load route. This build
supports one fresh level per project, using native slot 65.

## Add or remove Mobys

Choose **Objects → Add object…** and search across the game. Select a source
variant and inspect its model preview and compatibility result before placement.
Some objects require dependencies or resources that a destination cannot provide;
the exporter explains these cases. A visible catalog entry is not a guarantee
that every enemy behavior works in every level.

**Remove Object** marks an object for removal from the exported level. Export
reuses removed native object slots for new placements and omits removed imports;
unused trailing slots are trimmed. Resources owned only by removed classes are
reclaimed when the exporter can prove they are no longer used. Save identities
remain separate from physical slots, so a replacement does not inherit another
object's collected state. The game's finite memory and save-identity limits still
apply. Undo or restore before export if you want the object back.

## Dragonfly eggs and signs

Choose **Add dragonfly egg** or **Add sign**, and select your unchanged Japanese
disc when prompted. Eggs have named color swatches. Select an egg and choose
**Edit** to recolor it; select a sign to edit its three lines of text. The preview
shows supported characters and length limits.

Dragonfly eggs have no added eyes in the game or inventory. The native inventory
shows collected/available counts globally and per level. Collected eggs use the
ordinary game save; copies receive new identities while recoloring preserves
them. Required dragonfly eggs contribute to completion and bonus admission.
Use a separate copied memory card for each edited game.

## Check and export

Choose **Build Safety** and resolve any reported issue, then **Create BIN**.
The exporter checks the finished image before publishing the BIN/CUE pair. It
leaves your source disc untouched. Test the exported game, including the areas
and object behaviors you changed.

Fly Anywhere is deferred from this release. Earlier research preferences are
preserved but do not enable flight in normal release exports.

This package contains no game image, BIOS, emulator, memory card or extracted
game assets. See `known-limitations.md` for supported scope and verification.
