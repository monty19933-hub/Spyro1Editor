# Spyro Editor 2.1

Spyro Editor changes the levels of the original PlayStation game *Spyro the Dragon*
(USA) and builds a new, playable copy of the game from your changes. It never
writes to your own disc image, and it does not include the game.

## The short version

1. Open **Spyro Editor.app** on an Apple Silicon Mac, or **Launch Spyro Editor.bat**
   on Windows x64.
2. Choose **Open Game…** and select your own unchanged USA *Spyro the Dragon* disc
   image (a `.bin` or `.cue` file).
3. Pick a level from the list at the top left.
4. Change things in the five tabs on the right: **Objects**, **Terrain**,
   **Game Settings**, **Colours** and **Environment**.
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
- Create BIN builds the whole project whichever level is open: every level you
  changed or built, your new world, and Game Settings.
- If something cannot be put in the game, the editor says what and where, and
  nothing half-built is written: the game you created last stays exactly as it
  was. **More ▾ → Check for Export Issues…** runs the same check without building.
- **Fix It For Me**: whenever Create BIN stops on something in your project,
  this button appears beside the message at the bottom of the window (and in
  **Details**). One press gets the game made: the editor runs Create BIN itself,
  as often as it takes, and at the end says everything it did. Each run is a
  whole Create BIN, so a hard case can take several minutes; the status line
  says which try it is on.
  - What it can repair, it repairs. (A new world with no way in yet no longer
    stops Create BIN: it places the doorway itself, in the homeworld the world
    is entered from, on level open ground near where Spyro arrives. Move it
    afterwards with Terrain › World Entrance if you like; in Gnasty's World,
    which has no free place, it says so and you place it yourself.) When your
    world's homeworld has more objects
    than it can hold (82), it first gives back the save rows of objects you
    deleted and then, if needed, takes out loose gems, the least valuable first;
    it says how many and what they were worth, and keeps the level as it was in
    the project's `backups/fix-it` folder.
  - Anything else, it gets past by making the game **without the part of your
    project that stops it**. It finds that part by trying: a level the message
    names first, then one part at a time (a level you built, your new world,
    your changes to one of the game's levels, a game setting such as Hover or
    the colours). For one of the game's own levels it tries leaving out only
    one kind of your changes (objects, ground, sky, music or name) before the
    whole level, and when it is the objects, only the kinds of object you added
    that cause the stop (every copy of one creature, for example): it puts your
    other objects back and tries the added kinds in halves, which takes a few
    more runs. Saved changes the game cannot take (an egg thief's run moved
    off its ground, for example) are left out by themselves and named each time.
  - Nothing is deleted. A left-out part stays in your project and is only kept
    out of the game. When Fix It For Me has left something out, a window opens
    that says what and why; Create BIN also says it in its window and after
    every game. **More ▾ → Left Out of the Game…** lists each part with what
    Create BIN said about it, and **Put Back** includes it again. A newer
    version of the editor starts with nothing left out.
  - It is not offered when the computer is the problem (no disc image chosen, a
    full disk, no permission to write): the message then says what to do.
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
- **What it gives** appears for an enemy, a chest or anything else that pays out
  a gem when beaten or opened: choose any of the five gems (red 1, green 2, blue
  5, gold 10, pink 25) and that is what Spyro gets. It works for the level's own
  creatures and for ones you added from other levels; the level's treasure total
  follows. A creature that gives nothing in the game (it gives butterflies, or
  comes back to life) has no such choice.
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
  which the game handles in any level. Charging down a supercharge ramp needs
  Spyro's supercharge moves, which only the levels with supercharge ramps of their
  own have (Magic Crafters, High Caves, Wizard Peak, Tree Tops, Lofty Castle,
  Haunted Towers, Twilight Harbor). In any other level Create BIN adds them,
  which takes about 14 KB of the level's memory. Peacekeepers, Beast Makers,
  Misty Bog, Dream Weavers and Dark Passage are too full for them: there Tile Type
  says so, unless Game Settings › Memory is set to Emulator (8 MB RAM).
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
- **Memory**: how much memory the games you create may use. **Emulator (8 MB
  RAM)** is the usual choice and what a project has until you choose: each
  level's objects load into the extra memory DuckStation has with Settings ›
  Console › Enable 8MB RAM, so levels hold more. A game made this way still
  plays on a normal 2 MB set-up wherever its levels fit 2 MB, and Create BIN
  says which levels need 8 MB. Choose **PlayStation (2 MB)** for a real console
  or a disc: every level must then fit 2 MB and Create BIN stops when one does
  not.
- **Portals & Return Home**: choose where each portal leads and where Return
  Home goes. **Select Portal** moves the trigger, lettering and travel path
  together; move the stone arch with the terrain tools.

## Colours tab

Everything about looks and moves. What you choose is saved at once (the dragon
statues' colour with the level) and Create BIN puts it in the game.

- **This level's colours**: the open level's enemies, chests, dragons and
  animals, each drawn as it will look, in four lists. Click a kind's colour to
  choose one of the quick colours or type one as #RRGGBB and press **Use**;
  **Its own** puts it back. **Randomize** beside a list gives that list's kinds
  other colours (press again for others), **Randomize All** does every list at
  once, and **Their Own Colours** clears them. A colour is for this level only,
  so the same gnorc or chest can be red in Artisans and blue in Dark Hollow. A
  thing keeps its shading, and what is of another colour on it (skin, armour,
  eyes) stays; a thing with no colour of its own (grey metal, stone) takes the
  colour all over. In a level you built the card lists what you placed in it.
  Two kinds that share a texture share its colour.
  - **Dragons**: **Dragon statues** sets one crystal colour for every dragon
    statue of the level (their own is green). It is kept with the level, so
    press **Save**. One statue alone can still be given a colour in its Edit
    window on the Objects tab. The **fairy at a freed dragon's pedestal** is
    listed too. The dragon who steps out of the statue for his speech keeps his
    own colours.
  - The Green Wizard in three levels and Haunted Towers' Summoning Wizard are
    not listed: the editor cannot read their models' colours yet.
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
- **Hover**: tick **Let Spyro hover (from Spyro 3)** and, in the games you
  create, pressing ▲ while gliding makes Spyro flutter: he beats his wings
  quickly, rises about as high as a short jump and drifts on a little before he
  drops, as in Spyro 2 and 3: good for a ledge just out of reach. As in those
  games he can't glide again until he lands. It is in every level of the game;
  unticked, ▲ in a glide drops Spyro straight down as in the original game. It
  is saved with the project. The wing flap is the flight levels' own, which
  Create BIN adds to each level that has room for it on a PlayStation; Tree
  Tops, Haunted Towers and Twilight Harbor have none, so there Spyro hovers
  without flapping. In levels where Fly Anywhere is on, Spyro flies instead of
  gliding, so there is no glide to hover from.
- **Egg thieves' colour**: the egg thieves' robes in any colour in place of
  their blue: pick one of the ready colours, type one as #RRGGBB and press **Use
  This Colour**, or **Surprise me**; **Their own blue** puts it back. The card
  draws a thief in the colour. It is the same in every level that has thieves and
  for thieves you add to levels you build; they keep their shading and their
  eyes. In High Caves the tornado wizards wear the same cloth as the thieves, so
  they change with them.
- **Spyro's colours**: give Spyro any colours you like, part by part: **Body**
  (head, back, legs, snout and tail), **Belly**, **Horns**, **Head spikes** (the
  spiky fins behind his head), **Wing frame**, **Wing skin**, **Tail tip**,
  **Eyes** and **Pupils**. The picture at the top of the card is Spyro as he
  will look in the game, redrawn as you choose. Click a part's colour to choose:
  a quick colour, the **Hue**, **Richness** and **Brightness** sliders, or a
  colour typed as #RRGGBB (the picture tries it on while you choose), then **Use
  this colour**. The small square in the corner of a changed part is his own
  colour, and **His own** puts it back. The looks under the picture (**Ember**,
  **Ice**, **Emerald**, **Gold**, **Shadow**) set every part at once,
  **Surprise me** makes one up, and **Classic** gives him his own colours again.
  Each part keeps his shading, and the colours are in every level, on the title
  screen, the loading screens and in the cutscenes. They are saved with the
  project, and Create BIN can make a game with only these settings, without any
  level edits. **A look for one level**: set **Colours for** at the top of the
  card to the open level, then choose a look or change a part; Spyro has it in
  that level only (Classic there gives him his own colours in that level,
  whatever the rest of the game has). **Same as the Whole Game** takes the
  level's look away again. The title screen and cutscenes always have the whole
  game's look.
- **Play as Crash** is here too (see Crash Bandicoot below).

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
- **File ▾ → Clear Entire Project to Base Game…** starts the whole project over.
  Everything you have made is taken out: your changes to every one of the
  game's levels, the levels and the world you built, and your game settings
  (Hover, colours, memory, doorways and the rest). The editor then opens the
  original game again, as on the day you first opened it. You do not have to
  choose your game again, and the games you already created (the BIN and CUE
  files) stay where they are.
  - It asks first. The question lists what is in your project now, and only
    **Yes, I am sure** clears; **Cancel**, Return and Escape leave everything as
    it is. Changes you have not saved are lost with the rest (the question says
    so when there are any).
  - Your work is moved, not deleted. It goes into a folder named after the
    project and the time, inside **Old projects** beside your project folder
    (for the usual project: Documents › Spyro Editor › Old projects). The line
    at the bottom of the window says where, and the folder holds a note that
    says how to bring the work back: clear the project, close the editor, move
    the folder's contents into the project folder, open the editor. Delete the
    folder when you are sure you do not want the work.
  - If the clear cannot finish (a file is in use, the disk refuses), nothing is
    changed and the editor says so.

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
  lead to; each level has one doorway), its **shape** from nine pictures — Stone
  Arch, Pointed Arch, Square Gate, Round Ring, Wide Arch, Hex Gate, Tall Arch,
  Gable Gate and Slanted Gate — its size,
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
- Your new world's homeworld (File ▾ → New World…) has the same card: a doorway
  to each of the world's levels, and one back to the game's homeworld the world
  is entered from (listed as "the way back out of your world").

### The way into your new world

Players reach the world made with **File ▾ → New World…** through a doorway in
one of the game's own homeworlds (the one chosen in that window). Open that
homeworld: its Terrain tab has a **World Entrance** card.

- **Place Entrance…** asks for the doorway's shape (the same six as the Doorways
  card) and size, and puts it on the selected tile or on the ground in front of
  the camera, facing the camera: Spyro walks into it away from you. Give it open,
  level ground in front: coming back, Spyro walks out of it a good way, as he does
  from the homeworld's own doorways.
- **◀ ▲ ▼ ▶**, **Up**/**Down**, **To Selected Tile**, **In Front of Camera** and
  **⟲ ⟳** move and turn it; **Shape…** changes its shape or size; **Front
  Texture…** and **Side Texture…** pick its frame's look from the homeworld's own
  textures (until you do, it uses the texture of the homeworld's own arches);
  **Remove Entrance** takes it out. The view draws it over the level; it becomes
  part of the homeworld when Create BIN builds the game.
- In another homeworld the card says where the world is entered from;
  **Use This Homeworld** moves the entrance there.
- The homeworld keeps its own five doorways (four in Gnasty's World): the
  entrance is a sixth, the most the game allows.
- Its opening shows your world's sky. A homeworld has little spare memory, so the
  card checks it: Artisans and Gnasty's World have room for a whole sky; Peace
  Keepers, Magic Crafters, Beast Makers and Dream Weavers take the part of the
  sky near the horizon (or just its colour) on a PlayStation, and the whole sky
  with Game Settings › Memory › Emulator (8 MB RAM).
- Leaving your world's homeworld through its doorway home brings Spyro back out
  of the entrance. Add that doorway with the Doorways card in your world's
  homeworld (**Add Doorway…**, then the homeworld's name). Create BIN asks for
  both together: an entrance without a way back would trap Spyro in your world.

### Your world on the balloon route

The game's balloonists fly Spyro from world to world in a fixed order, each once
Spyro has done what he asks (ten dragons in Artisans, 1,200 treasure in Peace
Keepers, and so on). **File ▾ → World's Balloon Route…** gives your world a place
in that order.

- **Your world comes** after one of the game's six homeworlds: "After Dream
  Weavers, before Gnasty's World", for one. The balloonist of the homeworld
  before it then flies Spyro to your world, your world's own balloonist flies him
  on to the world that used to come next, and every balloonist's menu lists your
  world in its place once it is open.
- **To fly Spyro to your world** the balloonist before it asks for what you
  choose: nothing, a number of dragons rescued, an amount of treasure found, or a
  number of dragon eggs recovered, counted over the whole game. **To fly Spyro
  on**, your world's balloonist asks for what you choose in the same way. A new
  place starts from the game's own pace: free to your world, and what the game
  asks at that point to fly on. Dragons and treasure of your world's levels
  count; dragon eggs are the twelve the game's thieves carry.
- A balloonist who asks for something else than he does in the game says so in
  his own words ("Come back when you have rescued 40 dragons."), and invites
  Spyro to the right world by name.
- **Your world's balloonist** has a name of your choosing (his talk is headed
  with it). He must stand in your world's homeworld, with his balloon:
  **Objects → Random objects** places them ("The way out"), or add a balloonist
  and a balloon with **Add object…**. Create BIN says so if he is missing.
- **After Gnasty's World** your world is the last on the route: Gnasty's World's
  balloonist opens it with everything else and can ask for nothing, and your
  world needs no balloonist (with one, Spyro can fly back out).
- **Not on the balloon route**: the balloonists do not know your world; Spyro
  reaches it through its doorway only.
- A world that has no place chosen follows the homeworld its doorway is in when
  its homeworld has a balloonist, and is last on the route when it has none.
- The balloon sets Spyro down where your world's homeworld starts. The entrance
  doorway is no longer needed to reach a world on the route (it still works).

With your world on the route, a saved game made before (from the original game
or an earlier export) may find one more world open than it had.

A rebuilt level holds up to 4,096 terrain tiles. Untextured tiles are split into
smaller triangles automatically. Walls stop Spyro from the side you can see them.
The game gives close-up detail to the textured tiles near the camera, and only so
many at once: Create BIN checks camera positions all over the level and names the
place if textured tiles are too crowded there. Use larger tiles there (a few hundred
units across, like the game's own) or untextured faces.

### Your new world in the game

**Create BIN** builds your world into the game with everything else: its
homeworld, its levels, their doorways, the entrance and the way back.

- Spyro walks in through the entrance or flies in by balloon (**World's Balloon
  Route…**). In your world's homeworld each doorway
  leads to its level; **Return Home** and **Exit Level** in a level bring him back
  out of that level's doorway, and the doorway home takes him back to the
  entrance. Create BIN can also start **New Game** in your world.
- The inventory has a page with your world's name, after the game's six worlds,
  listing its levels with their gems and dragons.
- A game with a new world saves in its own save file on the memory card (shown
  as a separate file next to the original game's). Saves from the original game
  or earlier exports are left alone; start a new save in the new game.
- Levels of your world are built like a New Level: dragons from the game's
  levels can't be added (their rescue scene belongs to their own level), and they
  play Spyro's own music, not songs from other games. Objects whose code the game
  changes for one level only are refused by Create BIN with the level's name.

## Random levels

**File ▾ → Random Level…** makes a whole new level the way the game's own levels
are made, from a seed and a few choices, then puts it where you say so you can add
objects and enemies yourself.

- **Laid out like the game's levels:** rooms strung along a route, each a little
  higher or lower than the last and joined by sloping paths: round a loop whose
  last room stands over the first (so the way round ends with a drop back to the
  start), off one big clearing, or along a winding trail. Rooms have steps to
  jump up, lookouts to glide from, pools and hidden nooks.
- **Built like them:** one sheet of four-cornered faces about 128 units across
  whose lines follow the edges of the land, as in the original levels, enclosed by
  steep banks, rock walls, peaks, open water or the sky.
- **Painted like them:** with the textures the chosen level itself uses for its
  ground, banks, rock and paths, in the colours that level paints them with, under
  its sky and to its music. With your project's levels loaded, that level's own
  towers, trees and rocks are lifted from it and stood in the new level.

The choices:

- **Kind of level:** clearings among the hills (like Artisans), islands in the
  water, islands in the sky (like Lofty Castle), caverns and tunnels, a canyon
  (like Dry Canyon) or castle courtyards. **Surprise me** lets the seed choose.
- **In the style of:** Artisans, Stone Hill, Dark Hollow, Town Square, Peace
  Keepers, Dry Canyon, Cliff Town, Ice Cavern, Magic Crafters, High Caves, Wizard
  Peak, Misty Bog, Tree Tops, Dream Weavers, Dark Passage, Lofty Castle, Haunted
  Towers or Twilight Harbor.
- **Size** and **Difficulty:** Easy, Medium, Hard or Kaizo. Easy levels have wide
  paths, bridges, short hops and room to spare. Hard ones have narrow paths,
  stepping stones in place of most bridges, more ledges and lookouts, glides with
  little to spare, and pools that cost a life at once. Kaizo leaves next to
  nothing to spare. Every jump and glide is still inside what Spyro can do.
- **What it has:** caves (in caverns, more rooms under rock; in canyons and
  castles, tunnels), charge ramps (a ramp too steep to walk, marked with arrows,
  up to a hidden nook), glides (steps up to a lookout and a ledge across the room
  that Spyro reaches only by gliding from it), pools, bridges (between islands;
  without them, stepping stones) and hidden nooks. A tick asks for a feature, an
  empty box leaves it out, a filled square leaves it to chance.
- **Textures:** that level's own textures and colours, or new pictures painted in
  the look's colours. **Use My Own Pictures…** puts your own picture (PNG, JPEG,
  WebP or BMP) on any kind of surface; pictures need your original disc.
- **Sky:** that level's sky, or a sky painted to match.
- **Seed:** the same seed and choices always make the same level. **New Seed**
  makes another; **Rebuild** makes this seed again with changed choices.

Every jump, glide and climb is built inside what Spyro can really do (measured in
the game): steps are 56 units high, stepping stones an easy jump apart, paths
slope no more than about 16°, a charge ramp about 26°, and a glide's ledge lies
within the glide from the lookout's height. Before a level is shown, the editor
checks on the finished faces that every floor can be reached from the start and
left again, and tries another layout if not.

**Put it** in place of the open level (a recovery copy is kept, and **Restore
Before Clear…** brings it back), as the project's new level, or in a level of
the world made with **File ▾ → New World…**. Tick **Then fill it with objects for
me** to have Random objects (below) place enemies, chests, gems, dragons and the
way out at once, at the level's difficulty; otherwise add them yourself with Add
Object. Everything the generator makes is ordinary terrain: paint, move or remove
any tile afterwards.

### Random homeworlds

**File ▾ → Random Homeworld…** (or **Make: A homeworld** in the Random Level
window) makes a homeworld: Spyro arrives in a plaza with rooms off it all round,
and a doorway stands in the middle of a room for every level the homeworld leads
to, with open floor in front of it and behind it for Spyro to come back out onto.

- A generated homeworld goes in the place of any of the game's six homeworlds
  (open that homeworld first) or in the homeworld of your own world.
- In a homeworld's place it gets a doorway to each of that homeworld's levels
  (in Artisans' place: Stone Hill, Dark Hollow, Town Square, Toasty and Sunny
  Flight; four in Gnasty's World). In your own world it gets a doorway to each
  level you made and the way back out to the game.
- The doorways' look follows the level and is different every time: a shape of
  the look's kind (round, wide and tall stone arches in the Artisans' style;
  square, slanted and gabled gates for the Peace Keepers; pointed and tall
  arches for the Magic Crafters; six-sided and gabled gates for the Beast
  Makers; rings for the Dream Weavers; wide arches and heavy gates in Gnasty's
  harbour), usually one shape for the whole homeworld as in the game, sometimes
  a different one for each doorway; each doorway its own size, the farthest one
  the grandest; and frames of the level's own stone in the level's colours. The
  destination's sky fills each opening exactly. **Terrain → Doorways** moves,
  turns, reshapes or retargets any of them afterwards; their frames are ordinary
  tiles you can repaint.
- With **Then fill it with objects for me**, the homeworld also gets its
  balloonist and his balloon (the way to the other worlds) and, in a homeworld of
  the game, its dragons. Arriving by balloon, Spyro is set down where the
  homeworld starts.

### Random objects

**Objects → Random objects** fills a level you built (a random level, a New
Level, a level from another game, a level you cleared with Start Blank) with
objects placed the way the game places them. Choose and press **Randomize**; press
it again for another arrangement (what the last press placed is taken away
first); **Remove Them** takes them all away. What you added yourself stays.

- **Level type:** whose animals, chests and gems (a level of the game).
- **Enemy type:** whose enemies: the same level's or another level's, all of its
  kinds or one kind only.
- **Difficulty:** Easy places a few enemies, well back from where Spyro comes into
  a room, and plenty of animals for Sparx. Medium is like the game's own levels.
  Hard places many enemies close together, few animals, and more of the treasure
  behind enemies and chests. Kaizo has enemies everywhere, no animals for Sparx
  and no extra life.
- **Gems:** type the treasure the level should hold. Loose gems of every colour
  (red, green, blue, gold and pink), with what the chests and enemies give, come
  to exactly that number; the best gems lie at the places hardest to reach.
- **Dragonfly eggs:** how many to hide (they come from the Japanese version of
  the game: choose that disc in Add object… first).
- **Chests of every kind:** baskets and vases, strong chests, chests that take
  three flames, firework and spring chests, an extra life, and a locked chest
  whose key lies somewhere else in the level.
- **Animals, egg thieves, dragons, the way out:** the level type's animals in
  little groups; the egg thieves and dragons that belong to the level's place in
  the game, each thief with room to run; a Return Home vortex at the far end (in a
  homeworld, the balloonist).

How much of everything, and where, follows the game's own thirty levels on foot,
measured from the disc:

- For the gems you ask for there are about as many chests, enemies and loose gems
  as a level of the game with that treasure has: for every hundred gems about ten
  chests, seven enemies (fewer when easy, more when hard) and sixteen loose gems.
- About a third of the treasure lies loose, nearly half is in chests and the rest
  comes from enemies. Chests are mostly baskets and strong chests, with some
  spring chests and a few three-flame and firework chests, and each gives what
  its kind gives in the game (a basket mostly a green or a blue gem). Enemies
  give what they give in their own level; when that would leave too little for
  loose gems of every colour, chests and then enemies give a colour less.
  (Select any of them and use **What it gives** to choose yourself.)
- Loose gems are mostly reds and greens, in ones, twos and threes about a hundred
  units apart, a group all one colour, more than half of them by the walls and
  the rest along the paths and out in the rooms; the best gems go to the hardest
  places, and one of every colour is always there when the total allows.
- Chests stand in ones, twos and threes a chest's width apart, most of them a
  step or two from a wall or where a walk ends. Enemies wait in ones, twos and
  threes (now and then a pack) in the rooms beyond the first, facing the way
  Spyro comes in, and on the paths between.

Nothing is put inside a tower or against a wall, and enemies keep away from a
homeworld's doorways. Before anything is placed, each kind is tried on in the
level the way Create BIN will build it: a kind this level cannot hold (some
creatures need things of their own level, and some kinds cannot share a level)
is left out and named, so what Randomize places always builds. Everything
Randomize places is an ordinary object: move, change or delete any of it. A level with few save rows left (a level built in
Artisans' place, for one) gets less of everything, each thing worth more; the
card says so.

### An egg thief's run

Select an egg thief you added (or any added object that walks a route, such as
a sheep): **Edit Route…** and the **Its run** tools appear under the object's
buttons, and the run's numbered points show in the 3D view.

- Drag a point in the 3D view to move it (it stays on the ground), or type the
  points in **Edit Route…**.
- **New Loop Here** makes a new round run about where the thief stands, as wide
  as the open floor allows; press it again for another.
- **Bigger**, **Smaller** and **Turn** change the whole run about its middle;
  **Other Way Round** makes him run it the opposite way; **As It Came** brings
  back the run he had in his own level.
- The run keeps to one level of floor (a thief cannot jump), and no step is
  longer than the game can follow. Give a thief room to run and Spyro room to
  chase: a round of 1,500 to 3,000 units on open floor plays best.

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
