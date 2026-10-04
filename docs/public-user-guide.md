# Spyro Editor 1.0.7

The full **Add object…** catalog checks space in the background. You can keep
searching, change source versions or close the window while it checks. Placement
becomes available only when the selected object passes its fit check.

## Start editing

Open **Spyro Editor.app** on an Apple Silicon Mac, or **Launch Spyro Editor.bat**
on Windows x64. The application is self-contained. Choose **Open Game…**, select
your unchanged USA Spyro the Dragon disc, and pick a level. Projects and backups
are stored outside the application folder.

Select an object or terrain face in the viewport. Use the toolbar to move, copy,
remove or edit it. **Undo** and **Redo** let you revise changes. **Save** (⌘S on Mac, Ctrl+S on Windows)
stores the current level’s object and terrain edits; **Create BIN** combines the project's saved changes
into a separate game image. Open that exported CUE in your emulator.

**Add object…** searches the whole game. **Objects in this level** finds things
already placed. Use **Quick Add…** for standard gems and local copies, and
**Position & snapping** for height controls. **Undo last removal** appears directly
after deleting an object; **Recovery** holds level restoration options.

File and More are command menus: arrow keys highlight an option, and Enter runs
it. **More → Check for Export Issues…** opens the detailed export checks. The
status bar keeps routine messages short; **Details** retains the full result,
including file locations and exported treasure totals. Warnings stay visible.

Existing levels and fresh levels share the terrain controls. In an existing
level, select a face to move it, edit a point, copy it with **Add Terrain**, remove
it, or paint it. The exporter updates the visible surface, distant geometry,
visibility and collision together. A shared corner moves with every face that
uses it. Save before creating the BIN/CUE.

The game has limits on coordinates, collision encoding and memory. If a shape
cannot be represented safely, the editor explains why. Try a smaller change or
a simpler face; an unsupported edit does not produce a partially patched game.
Translucent source faces cannot currently be copied or removed by these tools.

## Dragon statue colours

Double-click a dragon statue and use its colour picker to choose a colour.
Save the level, then use **Create BIN** to include the change in the game.

## Teal and rainbow gems

Choose **Objects → Add object… → Collectibles → Gems**, then **Teal gem (50)**
or **Rainbow gem (100)**. Place, move, duplicate and remove them with the usual
object tools. Both use the USA source disc. Save the level and use **Create BIN**
to include them in the game. They add 50 or 100 to collected treasure, increase
the level's available total and retain their collected state in ordinary saves.
Both gems are twice the normal gem size, with bright colours and sparkles while
waiting to be collected. Pickup uses the game's own rising value display.

They can be placed in existing or fresh levels, subject to normal level capacity.
If a full level needs room, use **Find space for this object…** and review the
proposed removals. Their value is fixed; place the other gem to change its value.
Existing saved games keep their collected identities, so use New Game when
testing a newly rearranged collection layout.

## Replace a Flight gate

Select an original Flight gate and choose **Replace Flight gate…**. Pick a
replacement, review its time bonus and fit check, then choose **Replace gate**.
Only compatible objective categories appear. Keep the rest of the course's
objects unchanged for this operation. The replacement stays at the original
gate position; **Undo Object** restores the original, including after reopening
the project. Save and use **Create BIN** to export it.

## Portal destinations and Return Home

Open **Game Settings → Portals & Return Home**. Choose a doorway, select its
new destination by name and level ID, then **Apply portal destination**. Leave
**Return Home comes back through this portal** checked to pair its destination
with this doorway. Each level also has a separate **Return Home destination**
choice. Changes save immediately; **Undo route change** restores the previous
version, and **Create BIN** includes the saved routes.

Use **Select Portal** to move the trigger, lettering and travel path together.
Returning uses that doorway's current fly-out path. Move its stone arch with
the terrain tools separately. If several doorways enter the same level, choose
which one that level returns through. A saved fresh level appears as ID65.
Deleted doorways cannot be used as return destinations; restore them or choose
another doorway. Native memory limits can prevent a route from exporting.

Original terrain textures remain visible while editing. If texture files are
missing or damaged, loading the level rebuilds them from the selected disc.
Without the source disc, cached geometry can still open and the status explains
why original materials are unavailable.

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

## Clear the current level

Choose **File → Start Blank in Current Level…** and review the confirmation.
The editor saves your current work and a checked recovery snapshot before
replacing the level with an empty scene and a small starting floor. The level
keeps its place in the level picker. Other levels remain in the project.

Use the same terrain and object tools to build the replacement. You can remove
both starter-floor faces, save an empty draft, and use **Add Terrain** to begin
again. Before export, place the player above a solid floor. Rebuilt retail levels
and fresh levels reached through a portal need a continuous landing area at
least 384 units wide, centered on the start. The new removable starter floor
provides this area. Existing saved terrain stays unchanged; enlarge it or move
the start if export reports insufficient landing space.

**Recovery → Restore Before Clear** restores the saved scene from before the
clear and preserves your replacement scene in the recovery folder. Recovery
also works after reopening the project. If a recovery file was changed outside
the editor, restore stops instead of overwriting it.

**Create BIN** rebuilds the cleared level's terrain, collision, object storage
and asset tables. Removed content frees actual game memory; player and engine
resources remain. All saved replacements, ordinary level edits and the fresh
level are included. Choose the New Game starting level in the export dialog.
Use New Game for the first test; existing saves retain their collected states.

Start Blank currently supports Artisans, Stone Hill, Town Square, Peace Keepers,
Dry Canyon, Cliff Town, Magic Crafters, Beast Makers, Dream Weavers and Gnasty's
World, plus the fresh authored level. Other level slots are not yet supported.
Clearing does not recycle old collectible identities or create a portal network.
A cleared Town Square and the separate fresh level cannot currently be exported
together; restore Town Square before combining it with the fresh level.

Untextured terrain is split internally into smaller triangles for reliable
visibility near Spyro. Your editable faces stay intact. The combined generated
terrain must fit the game's 255-triangle limit. If an exact split is impossible,
export explains which geometry needs simplifying.

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
