# Spyro Editor Beta V6 User Guide

Private development preview, internal build `0.1.0-beta.47`. V6 is not finished
or publicly released. The Mac preview is ad-hoc signed and is not Apple-signed
or notarized; read the included opening instructions before launching it.

## Start a project

1. Open **Spyro Editor** on macOS or **Launch Spyro Editor.bat** on Windows.
2. Choose **Open BIN/CUE** and select your own Spyro the Dragon disc image.
3. Pick a level from the level menu.

The editor keeps projects and saved edits outside the application folder. Installing a newer beta does not replace your work.

## Make a fresh level

Choose **Fresh Level…** in the normal editor. This window is separate from the
older Blank-Level Lab below, but belongs to the same application and active
project. It stores its own document; normal **Create BIN** does not include it.

1. Select **Level aggregate** to open the saved example. For an empty project,
   first use **Legacy floor (v1)** → **New starter** → **Save document**, then
   **Level aggregate** → **Fork saved legacy floor**. Existing work is never
   reset automatically.
2. Edit floor bounds, spawn, heading, colors and music. Add or select placements
   in the list. Their markers and routes are schematic, not rendered game
   models, collision or lighting previews.
3. Use **Browse native Moby sources…** to search placed donors across the game.
   Choose a donor, enter position and rotation, then apply it. **Cancel new native
   Moby** leaves the document unchanged. A source reference can be saved before
   its native import is supported; browsing does not make it playable. Shared
   PETE model registrations are inspect-only and have no placed-donor row.
   Both browsers also expose all 368 identified classes, including 6,334 native
   observations and entries with unresolved dependencies. Use their search and
   source/resource filters; read the selected variant's compatibility reason
   before placing it. A visible entry does not imply supported native export.
4. The two floor-material slots can reference retail textures. Inspect sources
   explicitly for donor thumbnails. Selected art is not painted onto the
   schematic. Material references and native export support are separate.
5. **Apply to preview**, **Undo**, **Redo**, **Save document**, and **Reopen saved**
   operate on the complete document. Pending fields are not silently discarded
   when selecting an object or starting source work.
6. **Create research candidate…** is the current separate test-export action.
   It requires a clean, explicitly saved supported document, the exact original
   USA BIN/CUE, and a new output location. It verifies the complete written disc
   before reporting success. Load that exact resulting CUE in DuckStation.

The bounded native examples include Wizard + Artisans Grass and the separate
R03 Wizard + Grass + locked-chest family on its legacy floor or exact four-tile
layout, with two floor materials and an
authored background without Town Square's visible terrain. Their saved export
requires the exact supported geometry, placements and material selections;
editing outside those profiles can disable export. Other donors remain
authoring-only. This is not yet general new-level publishing.
Portals, durable saves, unrestricted materials/Mobys and fresh-level gameplay
flight remain unfinished. The separate retail Toasty flight option is below.

For an exported ID65 test, open the in-game inventory and enter:
**R1, R2, L1, L2, R1, L1, R2, L2, Left, Down**. Use a disposable copied emulator
memory card. This development entry route is not a finished portal.

### Edit a complete locked-chest family

Select its single entry in the placement list, or choose **Add locked-chest
family**. Edit independent key/chest XYZ and all five named reward members.
Each reward has **Source-relative default** or **Explicit WORLD XYZ** mode.
Defaults are resolved by native source compilation, not invented by the preview.
Explicit destinations stay fixed when the key or chest moves. Choosing default
explicitly removes that reward's authored override.

Apply validates the complete document; invalid input is retained. Cancel a new
family to leave the prior document unchanged. Undo/redo, removal and reordering
operate on the whole family, preserving one editor ID. Save/Reopen keeps all
members. The schematic shows anchors and explicit destinations, exact numeric Z,
and a complete seven-member legend; it does not simulate native trajectories.

Schema5 supports neutral families on legacy or tiled content. Only the exact
saved legacy-floor or four-tile R03 profiles enable its research-export action.
Changing anchors, reward destinations or supporting content may make the document
authored-only. No Wizard, Grass or other supporting Moby is silently inserted
when you add a family. General interchangeable families remain unfinished.

### Author a larger tiled floor

In **Level aggregate**, choose **Use tiled geometry (schema 4)** explicitly.
The existing floor becomes the first tile, retaining its material choices,
spawn, settings and placements. Single-tile conversion requires each legacy
floor side to be at most2048; a wider floor is not silently split. Existing
documents are never upgraded merely by opening them. Family documents retain
schema5 instead of downgrading to4.

- Select a tile to edit its position, size, color and two material slots.
  **Add tile to right** copies the selected tile's size and materials into an
  adjacent tile. Apply pending fields before changing selection.
- **Move tile up/down** changes list order, not world position. **Remove tile**
  refuses if it would invalidate the spawn or a placement endpoint.
- Spawn, heading, sky and music belong to the whole level. Spawn Z must be
  exactly 512 above a supporting tile plane. Shared edges are valid; gaps are
  not treated as floor, and overlapping interiors on the same plane refuse.
- **Apply**, **Undo/Redo**, **Save document** and **Reopen saved** use the same
  whole-document workflow. Tile IDs and per-tile materials survive save/reopen.

Each rectangular tile is 16–2048 native units per side, on a 16-unit grid.
The document permits at most 255 tiles and 128 placements, also subject to its
64 KiB file-size limit. These are authoring limits, not proven in-game capacity.
The schematic draws the actual tiles, but does not render their selected
textures or prove collision, enemy behavior or a safe route between endpoints.
Schema4 tiled research export supports zero placed Mobys with Base-color faces
or bounded supported static retail material selections. Native data must fit
0x1000 bytes, source/GPU ownership must validate, and the complete report must
fit64KiB. The R06 three-material example has separate gameplay observations;
that does not certify every combination. Animated/HQ/scrolling materials and
general tiled Moby export remain unavailable. The exact saved four-tile schema5
R03 Wizard/Grass/chest example is a separate guarded exception; other tiled
families remain authored-only. Editing a face, tile or actor outside that exact
recipe disables its research export, but still permits saving the document.

### Current two-family test recipe

To recreate the supported example, keep these exact values. This is a temporary
testing boundary, not the intended long-term editing limit. Editor-generated
placement IDs can differ, but placement order and the values below must match.

| Part | Values |
| --- | --- |
| Floor | X 8448, Y 8448, Z 8704, width 2048, depth 2048 |
| Spawn | X 8960, Y 8960, heading 64; spawn height is floor +512 |
| Colors | Floor `#508070`, sky `#806040` |
| Music | Native track 19 |
| Placement 1 | **Add Wizard route**: origin 9984 / 9984 / 8704, route end 9472 / 9984 |
| Placement 2 | Browse **Artisans Grass**, WAD 10, true index 121, class 501; position 9152 / 9152 / 8704, native yaw 64 |
| Triangle 0 material | Retail source WAD 14, texture 1 |
| Triangle 1 material | Retail source WAD 70, texture 1 |

Use the original USA retail source for both materials and the Grass. Apply each
placement before adding the next, then Save document before creating the test
candidate. Choosing a Wizard from the generic Moby browser does not substitute
for **Add Wizard route** in this particular test profile.

## Use the older ID65 Blank-Level Lab

1. Open the **Level** workspace and expand **ID65 Blank-Level Lab**.
2. Select your exact clean USA BIN/CUE with **Open BIN/CUE**, then choose
   **Build / Refresh Locked Base**. The editor derives the independent ID65 base
   locally; the package contains no game data.
3. Choose **Load Lab** only after the manifest and locked BIN/CUE validate.
4. In this first V5 lab, edit existing high-detail terrain height and use
   **Choose Texture & Start Painting** to paint those HP faces with any of the
   66 resident textures decoded from the exact locked ID65 payload. Lab texture
   painting changes only the face texture ID; it preserves the target material,
   tint, surface, and collision behavior. Save with **Save Research Workspace**.
5. Resident texture painting is currently an editor-preview authored layer; it
   is not written to a test BIN yet. Undo resident texture paints before choosing
   **Create Disposable Test CUE**, which remains the proven HP-Z-only writer.
6. Choose **Reveal Test CUE** to select the resulting CUE
   in Finder on macOS or Explorer on Windows, and load that exact CUE in
   DuckStation.

The lab is a Town Square-derived construction substrate, not a byte-empty map.
It is separate from normal **Create BIN**. True terrain addition, LP/XY edits,
arbitrary Mobys, custom/cross-level/private texture allocation, portals, totals,
music ownership, and save ownership remain blocked until their own runtime tests
pass. Restore the Lab authored layer before using normal retail **Create BIN**;
the editor never silently omits ID65 research edits from a retail build.

## Edit objects

- Click an object in Map or Game Camera view to select it.
- Drag it to move it, or choose **Edit Object** for exact position and rotation.
- Choose **Add Object**, open the picture gallery, and select an available object to place.
- Use **Undo** if a placement is not right.

Only objects that the editor can safely export are shown in the normal gallery and replacement lists.

## Paint terrain textures

1. Open **Level Building Editor**, choose **Terrain**, then select
   **Choose Texture & Start Painting**.
2. The current level loads first for speed. Choose another source level only when you want its textures.
3. Double-click a texture to begin painting.
4. Click a terrain section.
5. When asked, choose:
   - **Only Selected Section** when enabled; it changes just the section you clicked and requires an available proven private slot.
   - Otherwise choose **Linked Sections** when offered to change the matching sections that currently share that texture, or cancel without changing anything.
6. Choose **Return to Texture Palette** to select another texture without unloading the source levels you already opened.

**Suggest Nearby Tiles** offers textures that best match the surrounding terrain.

## Other level edits

The editor also provides user-facing controls for supported sky colors and swaps,
level names, music, object lighting, and existing high-detail terrain height.
Terrain **Raise/Lower** adds Z to each current vertex without flattening the
face; **Undo Height Only** removes only the selected face's Z change, preserves
texture and surface work, and immediately saves the current terrain changes.
XY movement, low-detail terrain, add/remove, and topology growth remain
research-only and are refused by Save Terrain Changes and Create BIN. Choose
**Save Terrain Changes** after other changes you want to keep.

## Check and create a BIN

1. Choose **Build Safety**.
2. Fix any item marked as needing attention. Double-click an object or terrain finding to select and center it.
3. Choose **Create BIN**.
4. Select the generated CUE in DuckStation and test the edited areas.

Create BIN writes a new output and does not modify the source disc image.

### Fly Anywhere (initial Toasty support)

The Level tab now has **Fly Anywhere Enabled**, **Save Flight**, and **Revert**.
The initial choice is OFF. Save stores the choice in the current project for
the selected level; Revert discards the checkbox draft. A draft must be saved
or reverted before Create BIN.

This controls Spyro in the exported game, not **Editor Free Camera**. Default
OFF and explicitly saved OFF are shown separately. Save Flight does not change
a memory-card save. In a supported enabled export, jump and press Cross while
airborne to enter flight; the option does not make Spyro spawn in midair.

ON currently supports **Toasty only**, in a project without other saved edits.
Other levels and independent ID65 are not enabled by this initial native recipe.
To test, select the original BIN/CUE, open Toasty, enable the checkbox, save it,
and use the normal **Create BIN** command. Enter Toasty through the normal game;
this export does not add a diagnostic level-selector shortcut or emulator cheat.

To disable it, uncheck the box, save, and create the BIN again. Explicit OFF
rebuilds from the complete original ROM even if there are no other edits, so an
older enabled output is not silently left in place. Ordinary edits may then be
applied to that original-layout OFF build. ON/OFF source and export tests pass;
visible native gameplay acceptance remains separate and pending.

The sealed September 12 R01 private-preview package was not overwritten and does
not contain these later source changes. Do not assume an older app has this panel.

## Backups and updates

Open **Project Data** to find the current project folder and automatic backups. The updater shows a changelog before installing a newer beta, and the existing project remains in place.

This package contains no game data, BIOS, emulator, or patched disc image.
