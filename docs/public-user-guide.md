# Spyro Editor Beta V4 User Guide

## Start a project

1. Open **Spyro Editor** on macOS or **Launch Spyro Editor.bat** on Windows.
2. Choose **Open BIN/CUE** and select your own Spyro the Dragon disc image.
3. Pick a level from the level menu.

The editor keeps projects and saved edits outside the application folder. Installing a newer beta does not replace your work.

## Edit objects

- Click an object in Map or Game Camera view to select it.
- Drag it to move it, or choose **Edit Object** for exact position and rotation.
- Choose **Add Object**, open the picture gallery, and select an available object to place.
- Use **Undo** if a placement is not right.

Only objects that the editor can safely export are shown in the normal gallery and replacement lists.

## Paint terrain textures

1. Open **Level Textures**.
2. The current level loads first for speed. Choose another source level only when you want its textures.
3. Double-click a texture to begin painting.
4. Click a terrain section.
5. When asked, choose:
   - **Only Selected Section** when enabled; it changes just the section you clicked and requires an available proven private slot.
   - Otherwise choose **Linked Sections** when offered to change the matching sections that currently share that texture, or cancel without changing anything.
6. Choose **Return to Texture Palette** to select another texture without unloading the source levels you already opened.

**Suggest Nearby Tiles** offers textures that best match the surrounding terrain.

## Other level edits

The editor also provides user-facing controls for supported sky colors and swaps, level names, music, object lighting, terrain height, and terrain position. Save after making changes you want to keep.

## Check and create a BIN

1. Choose **Build Safety**.
2. Fix any item marked as needing attention. Double-click an object or terrain finding to select and center it.
3. Choose **Create BIN**.
4. Select the generated CUE in DuckStation and test the edited areas.

Create BIN writes a new output and does not modify the source disc image.

## Backups and updates

Open **Project Data** to find the current project folder and automatic backups. The updater shows a changelog before installing a newer beta, and the existing project remains in place.

This package contains no game data, BIOS, emulator, or patched disc image.
