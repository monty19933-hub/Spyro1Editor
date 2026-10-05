# Spyro Editor 1.1

Spyro Editor changes the levels of the original PlayStation game *Spyro the Dragon*
(USA) and builds a new, playable copy of the game from your changes. It never
writes to your own disc image, and it does not include the game.

## The short version

1. Open **Spyro Editor.app** on an Apple Silicon Mac, or **Launch Spyro Editor.bat**
   on Windows x64.
2. Choose **Open Game…** and select your own unchanged USA *Spyro the Dragon* disc
   image (a `.bin` or `.cue` file).
3. Pick a level from the list at the top left.
4. Change things in the four tabs on the right: **Objects**, **Terrain**,
   **Game Settings** and **Environment**.
5. Press **Save**, then **Create BIN**. Open the new `.cue` file in your emulator.
   **Show Game File** in the bottom bar takes you to it.

Projects and backups are stored outside the application folder (in
`Documents/Spyro Editor`), so replacing the app with a newer one keeps your work.
On a Mac, macOS asks once whether Spyro Editor may use your Documents folder;
choose **Allow**. The editor's window appears after you answer.

## Save and Create BIN

- **Save** (⌘S on Mac, Ctrl+S on Windows) keeps the open level's changes in your
  project. It does not make a game.
- **Create BIN** saves first, then builds a new BIN and CUE from everything saved
  in the project. The files go in the project's `output` folder. Your original
  disc is never changed.
- Create BIN needs working room on the disk that holds your project: about
  2.1 GB, or 3.3 GB when the project has a level you built. With less it says
  so and starts nothing.
- If something cannot be put in the game, the editor says what and where, and
  nothing half-built is written: the game you created last stays exactly as it
  was. **More ▾ → Check for Export Issues…** runs the same check without building.
- A terrain shape the game cannot stand on (for example a corner dragged so far
  that the ground folds over itself) raises a red bar with **Show it** and
  **Undo that change**.

## The view

**Edit Map** looks straight down at the whole level; **Game Camera** flies
through it. **Fit All**, **Frame Selection** and **Return to Entry** bring the
view back.

- Click selects. Drag moves what you selected. Double-click opens its settings.
- Right-drag (or middle-drag) pans the map or looks around; the mouse wheel zooms
  the map or moves the camera forward and back.
- Game Camera keys, after clicking in the view: **W A S D** fly, **Q** and **E**
  go up and down, the arrow keys turn.

Shortcuts for the level only work after you click in the view. While you are
typing in a box, keys only affect that box. Changes to objects are written when
you leave a level or close the editor; terrain changes ask first.

## Objects tab

**Add to this level**

- **Add object…** searches every object from every level. Choose one, check its
  preview, then place it. The window checks in the background whether the
  object fits this level; you can keep searching while it checks. Esc cancels a
  placement.
- **Add dragonfly egg** and **Add sign** place the editor's own collectibles and
  readable signs (see below).
- **Add a gem or copy an object…** offers the standard gems and copies of what
  is already in the level.

**Selected object**

- **Edit…** changes the name, gem or reward value, position and facing.
- **Replace…** swaps a chest or enemy for another kind known to work in this level.
- **Copy** and **Paste** (⌘C / ⌘V, or Ctrl+C / Ctrl+V, in the view) duplicate the
  selected object at the pointer.
- **Remove** (or the Delete key in the view) takes the object out and leaves
  nothing selected. **Undo last removal** (⌘Z / Ctrl+Z in the view) brings it
  back.
- **Undo** puts the selected object back the way it is in the original game.
- **Height and ground** slides the object up or down, keeps it on the ground
  while you drag it, or jumps it to the floor above or below.

Dragons keep their pedestal and rescue scene with them when moved. The ENTRY
marker is where Spyro lands when he flies into the level. Double-click a dragon
statue to give it your own color.

**Recovery** holds **Restore entire level** and **Restore Before Clear…**.

### Objects from other levels

Different levels sometimes reuse an object number for a different look or
behavior, so check the source name and the preview. Some objects need artwork,
sounds or helper objects the destination level has no room for; the window
explains those cases.

If an object does not fit, choose **Find space for this object…**. The editor
names objects whose removal would make room and reports what would be left.
Nothing is removed automatically: remove what you choose, then open
**Add object…** again. **Only what fits here** lists only objects whose check
has finished successfully.

Removing one copy of an object frees its slot, but its model and textures may
still be needed by other copies. That is why the number of removed objects alone
cannot predict whether a new enemy will fit.

### Dragonfly eggs and signs

These come from the Japanese version of the game, so the editor asks for your
own unchanged Japanese disc the first time.

- **Dragonfly eggs** come in seven colors. Select an egg and choose **Edit…** to
  recolor it. The game's inventory shows eggs collected and available for the
  whole game and per level, under a spinning gold egg. Collected eggs are saved
  with the ordinary game save and count toward completion.
- **Signs** show your message when Spyro stands next to them: up to three lines
  of 24 characters, in the game's own letters (A–Z, 0–9, space and `! ? . ,`;
  lowercase is shown in capitals). Select a sign and choose **Edit…** to type it.
  When several signs are close together the game shows one message at a time.

Use a separate memory card for each edited game.

### Teal and rainbow gems

**Add object… → Collectibles → Gems** has **Teal gem (50)** and
**Rainbow gem (100)**: double-size, bright gems that add 50 or 100 to collected
treasure and to the level's total. They use only your USA disc.

### Replace a Flight gate

In a flight level, select an original gate and choose **Replace Flight gate…**.
Pick a replacement, review its time bonus and fit check, then choose
**Replace gate**. **Undo** restores the original.

## Terrain tab

- **Tiles**: click a tile to select it; ⌘-click (Ctrl-click on Windows) selects
  several; drag to move; Shift-drag to change height. **Move Tile…** and
  **Edit Corner…** take exact amounts. **Raise**, **Lower** and **Reset Height**
  work on the selected tile.
- **Add Terrain** copies the selected tile so you can move the copy into place.
  **Remove Terrain** (or Delete in the view) takes the selected tiles out.
  **Undo Terrain** puts the selected tile back the way it was, or brings removed
  tiles back.
- **Brush**: Raise, Lower, Smooth, Level or Restore the ground under the circle.
  Keys 1 to 5 choose a brush; 0 or Esc switches it off. **Undo Stroke** is
  ⌘Z / Ctrl+Z in the view; **Redo Stroke** is ⇧⌘Z / Ctrl+Y. With **Whole tiles**
  ticked the brush moves every tile it touches evenly. Brushes switch off when
  you leave the Terrain tab.
- **Textures**: **Choose Texture…**, then click tiles to paint them. Esc stops.
- **Corner by corner** moves one corner at a time.
- **Restore Original Terrain…** undoes every terrain change in the level, after
  asking.

The exporter updates what you see, the distant version of the level, visibility
and collision together. A corner shared by several tiles moves with all of them.
The game has limits on coordinates, collision and memory; if a shape cannot be
represented safely the editor explains why and nothing half-patched is produced.
Translucent original tiles cannot currently be copied or removed.

## Game Settings tab

- **Level name** changes the name on portals, loading screens and in the
  guidebook (capital letters, spaces and apostrophes).
- **Music** picks the level's track.
- **Fly Anywhere (whole game)**: tick **Let Spyro fly in every level** and the
  game you export lets Spyro fly in every normal level the way he does in
  Gnasty's Loot: jump, then press ✕ again in the air. Steer with the direction
  buttons; land by flying down to the ground. It is off unless you turn it on,
  it is saved with the project, and unticking it gives back the game's normal
  gliding in the next Create BIN. The five flight levels keep their own rules.
  This is separate from the editor's **Game Camera**, which always flies.
- **Portals & Return Home**: choose where each portal leads and where Return
  Home goes. **Select Portal** moves the trigger, lettering and travel path
  together; move the stone arch with the terrain tools.

## Environment tab

Recolor the level's sky, borrow another level's sky, or use a ready-made one.

## Levels of your own

- **File ▾ → New Level…** starts a level of your own that opens when you choose
  New Game. It uses the same Objects and Terrain tools. A project holds one such
  level: if you already have one, the editor asks before replacing it and keeps
  a copy in the project's `backups` folder.
- **File ▾ → Open Created Level…** reopens the level you made.
- **File ▾ → Start Blank in Current Level…** empties the open level down to a
  small floor so you can rebuild it. A recovery copy is saved first;
  **Restore Before Clear…** brings the previous version back, also after
  reopening the project.

When a project has a level of your own or a blank level, **Create BIN** asks
which level **New Game** starts in. Use New Game for the first test; a saved game
from before keeps what it had already collected.

Before exporting a rebuilt level, place the player above a solid floor. Levels
reached through a portal need a continuous landing area at least 384 units wide
centered on the start; the starting floor provides it.

Start Blank currently supports Artisans, Stone Hill, Town Square, Peace Keepers,
Dry Canyon, Cliff Town, Magic Crafters, Beast Makers, Dream Weavers and Gnasty's
World, plus the level of your own. A cleared Town Square and a level of your own
cannot currently be exported together. Untextured terrain is split into smaller
triangles automatically and must fit the game's 255-triangle limit.

## What this package does not contain

No game image, BIOS, emulator, memory card or extracted game assets. See
`public-known-limitations.md` for the supported scope and what has been verified.
