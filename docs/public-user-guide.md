# Spyro Editor 1.3

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
choose **Allow**. The editor's window appears after you answer. It asks the
same for the Desktop or Downloads folder if your disc image is kept there. If
access was refused, the editor opens a small window that says where to allow it.

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
tries the object's versions one by one, shows its progress, and lists each way
to make room as soon as it finds one; **Stop searching** ends the search early.
Nothing is removed automatically: remove what you choose, then open
**Add object…** again. When no combination of original objects would make
room, it says so, and removing objects you added or choosing a smaller object
is the way forward. **Only what fits here** lists only objects whose check has
finished successfully.

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

### Crash Bandicoot boxes

**Crash crate**, **TNT crate** and **Nitro crate** (Interactive › Crash Bandicoot) use
box art and sounds from Crash Bandicoot: Warped (USA). Press **Choose Crash disc…**
in the Add Object browser once and pick your copy; it is only read. They can go in
any ground level (not the flight levels).

- **Crash crate** breaks when Spyro flames it, charges it or lands on it (he
  bounces, Crash style). Choose its contents in the list: a red, green, blue, gold
  or purple gem, which counts toward the level's treasure, or a butterfly. Broken
  crates stay broken, like Spyro's baskets.
- **TNT crate**: landing on it bounces Spyro and starts 3, 2, 1, with beeps; then it
  explodes. Flame or charge sets it off at once. Spyro loses one Sparx level if he
  is within about two crate widths. The blast breaks nearby crates and baskets and
  sets off nearby TNT and Nitro.
- **Nitro crate** hops and rattles. Anything touching it (walking into it, charging,
  landing on it) makes it explode and takes all of Spyro's health. Flame it from a
  distance, or set it off with a TNT blast.

### Crash Bandicoot enemies

**Crash frog**, **Crash knight** and **Crash wizard** (Enemies › Crash Bandicoot) are
Crash Bandicoot: Warped's medieval enemies, converted from your Crash disc with their
own models, textures, animations and sounds (choose the disc as for the boxes). They
can go in any ground level.

- **Crash frog** sits croaking, then hops towards Spyro when it sees him. Touching it
  costs a Sparx level. It leaves a butterfly.
- **Crash knight** turns to face Spyro, marches up to him and swings his sword; a hit
  costs a Sparx level. He walks back to his spot when Spyro gets away. He drops a
  green gem (2).
- **Crash wizard** stands his ground, turns to face Spyro and casts a zap when Spyro
  is close and in front of him; it costs a Sparx level. He drops a blue gem (5).
- Flame, charge or jump on any of them and they are knocked flying, Crash style
  (jumping on one bounces Spyro off). The wizard's robe blows off first. Their gems
  count toward the level's treasure.
- Frogs and knights move up to about 100 units from where you put them and keep
  their height, so place them on level ground away from edges. Wizards do not move.

Use a separate memory card for each edited game.

### Teal and rainbow gems

**Add object… → Collectibles → Gems** has **Teal gem (50)** and
**Rainbow gem (100)**: double-size gems that add 50 or 100 to collected
treasure and to the level's total. In the game they tumble and shine exactly
like the original gems; the rainbow gem cycles red, gold, green, teal, blue and
purple. They use only your USA disc.

### Replace a Flight gate

In a flight level, select an original gate and choose **Replace Flight gate…**.
Pick a replacement, review its time bonus and fit check, then choose
**Replace gate**. **Undo** restores the original.

## Terrain tab

- **Ground** (in a level you build: New Level or Start Blank): nine buttons —
  Grass, Dirt, Stone, Sand, Snow, Wood, Water, Lava and Goo. Click one, then
  click tiles in the view to paint them; selected tiles take it at once. A ground
  sets both how a tile looks (a texture from one of the game's own levels) and
  what Spyro finds on it: water, lava and goo cost him a life, as in the game.
  Esc or **Stop Painting** goes back to selecting tiles.
- **Tiles**: click a tile to select it; ⌘-click (Ctrl-click on Windows) selects
  several; drag to move; Shift-drag to change height. **Move Tile…** and
  **Edit Corner…** take exact amounts. **Raise**, **Lower** and **Reset Height**
  work on the selected tile. Corners that several tiles share move together,
  as they do in the game. The Tiles card names the selected tile's kind (solid
  ground, water, …).
- **Tile Type…** changes what Spyro finds on the selected tiles: solid ground,
  water, lava or ooze, a supercharge ramp, and whatever else the level already
  defines. Spyro cannot swim: water, lava and ooze cost him a life (he sinks; the
  "at once" kinds take it immediately). The picture of the tile stays; use
  **Choose Texture…** for that. Changing a pond tile to solid ground lets Spyro
  stand on it unharmed; changing grass to water makes him sink. **Undo Terrain**
  puts the kind back. Only tiles whose collision matches the tile can be changed;
  the editor says so otherwise. In a level you build (New Level or Start Blank)
  Tile Type offers water, lava, ooze, a supercharge ramp and an invisible wall,
  which the game handles in any level.
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
The game's level format has limits, and the editor now refuses a change at the
moment you make it, in plain words, rather than at Create BIN:

- A tile the game has no collision under (scenery: walls, decoration, the
  underside of things) cannot be moved; Spyro never touches it, and the game
  cannot store a change to the picture alone.
- Within one piece of collision, corners can differ by at most 255 in height.
  To go higher or lower, raise or lower the neighbouring tiles too, or use the
  brush for a slope. Ground must not fold over itself.
- Ground needs 64 units of clear space under anything above it, so a tile
  under a low bridge or arch cannot be changed.
- The game looks ground up in a grid of 256-unit cells and keeps a fixed-size
  table for it; a tile cannot cross into another cell when the level's table
  has no spare room. The message tells you the range it can stay in.
- Each part of the level stores its heights within a band of 1023 and its
  sideways positions within 2047; Lower now works down to the bottom of that
  band, because the editor moves the band when needed.

A brush stroke simply leaves the corners it cannot move where they are and
says how many. Translucent original tiles cannot currently be copied or removed.

## Game Settings tab

- **Level name** changes the name on portals, loading screens and in the
  guidebook (capital letters, spaces and apostrophes).
- **Music** picks the level's track.
- **Fly Anywhere**: tick **Let Spyro fly in every level** and the game you
  export lets Spyro fly in every normal level the way he does in Gnasty's Loot:
  jump, then press ✕ again in the air. Steer with the direction buttons; land by
  flying down to the ground. Open **Choose levels** to tick only the levels you
  want instead; everywhere else Spyro moves as in the original game. A level of
  your own flies whenever at least one level is ticked. It is off unless you
  turn it on, it is saved with the project, and unticking everything gives back
  the game's normal gliding in the next Create BIN. The five flight levels keep
  their own rules. This is separate from the editor's **Game Camera**, which
  always flies.
- **Portals & Return Home**: choose where each portal leads and where Return
  Home goes. **Select Portal** moves the trigger, lettering and travel path
  together; move the stone arch with the terrain tools.

## Environment tab

Recolor the level's sky, borrow another level's sky, use a ready-made one, or
import your own. The 3D view (Game Camera) shows the sky the game will draw
behind the level: the original, your recoloured or borrowed one, or the one you
imported, updated when you click **Apply Sky**, **Restore Original Sky** or
change a level of your own's skybox.

### Importing a sky

**Import a sky** takes a 360° panorama picture (PNG, JPEG, WebP or BMP; twice as
wide as it is tall, as skybox makers export them), a sky model (OBJ or FBX, with
its textures next to it or packed inside), or a `.sky` file. In a game level
choose the **Import a sky** mode and **Choose Sky File…**; in a level of your own
use **Game Settings → Import a sky…**.

Spyro skies are made of coloured triangles around the player, not pictures, so
the editor converts what you choose and shows exactly what the game will draw
before you use it:

- **Detail** sets the number of triangles (1,000–4,000; the game's own skies use
  250–4,300). More detail keeps sharper shapes but makes the level bigger
  (about 20–55 KB). Small details such as single stars or writing soften.
- **Turn** turns the sky around the player. Without it, the middle of the
  picture faces east (+X; right in the Map view, where north is up).
- **Flip left and right** is for a model made to be looked at from outside,
  whose pictures read backwards from inside.
- A model is seen from its centre. Parts it leaves open, such as a dome without
  a floor, continue its nearest colours.

## Levels of your own

- **File ▾ → New Level…** starts a level of your own that opens when you choose
  New Game. It begins as a meadow that already looks like a Spyro level: grass,
  a stone path from the start, a ramp up to a raised ledge, a pond with a sand
  rim, and Artisans' sky. Repaint it with the Terrain tab's **Ground** card, move
  or remove its tiles and add your own. It uses the same Objects and Terrain tools. A project holds one such
  level: if you already have one, the editor asks before replacing it and keeps
  a copy in the project's `backups` folder.
- **File ▾ → Open Created Level…** reopens the level you made.
- **File ▾ → Start Blank in Current Level…** replaces the open level with the
  same meadow so you can rebuild it. A recovery copy is saved first;
  **Restore Before Clear…** brings the previous version back, also after
  reopening the project.

When a project has a level of your own or a blank level, **Create BIN** asks
which level **New Game** starts in. Use New Game for the first test; a saved game
from before keeps what it had already collected.

Before exporting a rebuilt level, place the player above a solid floor. Levels
reached through a portal need a continuous landing area at least 384 units wide
centered on the start; the starting floor provides it.

Start Blank works in every normal level (not flight levels) and in the level of
your own. Artisans, Stone Hill, Town Square, Peace Keepers, Dry Canyon, Cliff Town,
Magic Crafters, Beast Makers, Dream Weavers and Gnasty's World can all be cleared
together; of the other twenty levels (Dark Hollow, Toasty and most levels of the
later worlds) two can be cleared in one game. A cleared Town Square and a
level of your own cannot currently be exported together.

### Doorways in a homeworld you rebuild

A homeworld you rebuilt with Start Blank (Artisans, Peace Keepers, Magic Crafters,
Beast Makers, Dream Weavers or Gnasty's World) has a **Doorways** card in the
Terrain tab. Other levels show it too, with the reason they cannot have doorways:
the game keeps its doorway code in the homeworlds only.

- **Add Doorway…** asks where it leads (the levels that homeworld's own doorways
  lead to; each level has one doorway), its **shape** from six pictures — Stone
  Arch, Pointed Arch, Square Gate, Round Ring, Wide Arch and Hex Gate — its size,
  and its frame's texture (the same texture grid as Choose Texture…). It stands
  on the selected tile, or on the ground in front of the camera, facing the
  camera: Spyro walks into it away from you.
- Select a doorway in the list, or click its frame in the view. **◀ ▲ ▼ ▶** move it
  16 units (west, north, south, east as in the Map view), **Up**/**Down** 8 units;
  **To Selected Tile** and **In Front of Camera** put it somewhere new; **⟲ ⟳**
  turn it 15° or 90°. **Shape…** changes its shape or size, **Leads To…** where it
  goes, **Front Texture…** and **Side Texture…** its frame's look, and **Remove
  Doorway** takes it out with its frame.
- The frame is made of ordinary tiles. Selecting a doorway selects its whole
  frame, so the Ground card or Choose Texture… repaint it in one go; click single
  frame tiles to paint them one by one. Moving, turning or reshaping the doorway
  rebuilds the frame there and keeps its textures.
- In the game the opening shows the sky of the level it leads to. Spyro walks in,
  the level loads, and Return Home or Exit Level brings him back out of the same
  doorway. The game draws a doorway's sky inside an opening of at most ten
  corners that never bends inwards; the shapes are made that way.
- Doorways made before this tool (such as Lost Fleet's) keep working as they
  were; the first change finds their frame and moves it with them.
- The new world's homeworld (File ▾ → New World…) can have a doorway to each of
  its levels, marked "playable after New World export": Create BIN cannot build
  the new world yet and says so.

A rebuilt level holds up to 4,096 terrain tiles. Untextured tiles are split into
smaller triangles automatically. Walls stop Spyro from the side you can see them.
The game gives close-up detail to the textured tiles near the camera, and only so
many at once: Create BIN checks camera positions all over the level and names the
place if textured tiles are too crowded there. Use larger tiles there (a few hundred
units across, like the game's own) or untextured faces.

## Levels from other games

Open the level whose place the new level takes (a level or homeworld, not a flight
level), then choose **File ▾ → New Level from Spyro: Year of the Dragon…**. It
rebuilds one of Year of the Dragon's levels from your own Year of the Dragon (USA)
disc: its ground, walls, textures, water and lava, its distant scenery, and a start
where its level begins. Lava and acid can be deadly as in that game; deep water costs
a life, because Spyro can't swim here. Its sky's shapes are not converted: the level
gets a plain sky colour, or borrow a Spyro sky in Game Settings.

Crash Bandicoot: Warped's boxes, enemies and songs are still offered in Add Object and
Game Settings; building whole Crash levels is not offered for now.

Building takes about a minute. The open level is emptied the way Start Blank empties
it, with a recovery copy, and keeps playing its own song. Characters, enemies and
boxes of the other game are not brought over.

**File ▾ → Fill In What the Level Needs…** (done for you at the end of the command
above) puts into a level you built:

- the level's own dragons, spread out on open floor;
- its egg thieves, each with a run around open ground;
- a Return Home vortex at the far end;
- enemies and animals of the level's own kinds, spread along the way;
- gems worth the level's treasure, where the other game had its gems.

Everything goes on floor Spyro can walk or glide to from the start, and everything is
an ordinary object you can drag, turn or remove. Each object takes one of the level's
256 save rows (a dragon three); if the gems don't all fit, fewer gems carry the total.

Under each level's treasure, **To finish 100%** counts its own dragons and egg
thieves and says whether it has its Return Home vortex. It turns amber and names what
to add when something is missing; Create BIN lists the same before you play.

An object you add that walks a route (an egg thief, a patrolling enemy) can have its
route changed with **Edit Route…** in its object panel.

Create BIN keeps every level you build within what the game can draw in a frame: it
checks the views from every spot Spyro can reach and, where a view would draw too
much, draws distant scenery more simply and brings textures a little closer. A level
that is still too heavy is refused with the place named.

## What this package does not contain

No game image, BIOS, emulator, memory card or extracted game assets. See
`public-known-limitations.md` for the supported scope and what has been verified.
