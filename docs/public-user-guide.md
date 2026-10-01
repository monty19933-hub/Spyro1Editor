# Spyro Editor 1.0.1

## Start editing

Open **Spyro Editor.app** on an Apple Silicon Mac, or **Launch Spyro Editor.bat**
on Windows x64. The application is self-contained. Choose **Open BIN/CUE**, select
your unchanged USA Spyro the Dragon disc, and pick a level. Projects and backups
are stored outside the application folder.

Select an object or terrain face in the viewport. Use the toolbar to move, copy,
remove or edit it. **Undo** and **Redo** let you revise changes. **Save Level**
stores the current edits; **Create BIN** combines the project's saved changes
into a separate game image. Open that exported CUE in your emulator.

Existing levels and fresh levels share the terrain controls. In an existing
level, select a face to move it, edit a point, copy it with **Add Terrain**, remove
it, or paint it. The exporter updates the visible surface, distant geometry,
visibility and collision together. A shared corner moves with every face that
uses it. Save before creating the BIN/CUE.

The game has limits on coordinates, collision encoding and memory. If a shape
cannot be represented safely, the editor explains why. Try a smaller change or
a simpler face; an unsupported edit does not produce a partially patched game.
Translucent source faces cannot currently be copied or removed by these tools.

## Create a fresh level

Choose **File → New Level**. Your level opens in the same viewport and inspector
as the existing levels, with a starter floor and no inherited level objects.
Use **Add Terrain**, **Remove Terrain**, **Move Face**, **Edit Point** and the
brush tools to shape it. **Paint Face** opens a material chooser with previews.
Use the object browser to place Mobys, and set the player spawn in the inspector.
Save the level before exporting.

**Create BIN** includes the saved fresh level together with saved retail edits.
The export dialog lets you choose whether **New Game** starts in Artisans or
your authored level. Both current documents are included either way. Existing
save games keep their saved level. One fresh level per project uses native slot
65. Older experimental drafts stay separate and are preserved.

## Add or remove Mobys

Choose **Objects → Add object…** and search across the game. Select a source
variant and inspect its model preview and compatibility result before placement.
Different source levels may reuse an object number for different appearances or
behaviors. Check the selected source name and preview; a source variant is not
a promise of an identical enemy. Some objects require dependencies or resources that a destination cannot provide;
the exporter explains these cases. A visible catalog entry is not a guarantee
that every enemy behavior works in every level.

If an object does not fit, choose **Find space for this object…**. The editor
compares source versions and checks proposed removals against your current
objects and terrain textures. A successful plan names the objects to inspect,
explains the effect of removing them, and reports the remaining capacity.
Nothing is removed automatically. Use the normal Remove button for the objects
you choose, then reopen the browser to check the resulting scene.

**Only what fits here** includes only objects with a completed successful
compatibility check. Imported enemies can need additional player reaction
animations; these count toward the displayed memory use and removal plan.

Removing one copy frees an object slot. Its model and textures may still be
needed by other copies or linked objects. For example, removing one Gnorc while
others of that type remain may free a slot without freeing its artwork. This is
why the number of removed objects alone cannot predict whether a foreign enemy
will fit. Choosing another source version can also reduce what must be imported.

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
leaves your source disc untouched. **Details** beside the status message opens
the complete message when it is too long for the footer. Test the exported game, including the areas
and object behaviors you changed.

Fly Anywhere is deferred from this release. Earlier research preferences are
preserved but do not enable flight in normal release exports.

This package contains no game image, BIOS, emulator, memory card or extracted
game assets. See `public-known-limitations.md` for supported scope and verification.
