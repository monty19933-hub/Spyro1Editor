# Spyro Editor 2.5

Spyro Editor changes the levels of the original PlayStation game *Spyro the Dragon*
(USA) and builds a new, playable copy of the game from your changes. It never
writes to your own disc image, and it does not include the game.

## The short version

1. Open **Spyro Editor.app** on an Apple Silicon Mac, or **Launch Spyro Editor.bat**
   on Windows x64.
2. Choose **☰ → Open Game…** and select your own unchanged USA *Spyro the Dragon*
   disc image (a `.bin` or `.cue` file).
3. Click a level in **Your game**, the list on the left.
4. Change things in the five tabs on the right: **Ground**, **Objects**, **Look**,
   **Links** and **Game**. If you are not sure where something is, type it in
   **Find any command** at the top (⌘K on Mac, Ctrl+K on Windows).
5. Press **Save**, then **Create BIN**. Open the new `.cue` file in your emulator.
   **Show Game File** in the bottom bar takes you to it.

Projects and backups are stored outside the application folder (in
`Documents/Spyro Editor`), so replacing the app with a newer one keeps your work.
On a Mac, macOS asks once whether Spyro Editor may use your Documents folder;
choose **Allow**. The editor's window appears after you answer. It asks the
same for the Desktop or Downloads folder if your disc image is kept there. If
access was refused, the editor opens a small window that says where to allow it.

## Finding things

The window is laid out by what you are doing. Every feature of earlier versions
is still there and works the same; only where it lives has changed.

- **Your game**, on the left, lists every homeworld with its levels, then your
  own world and the levels you created. Click a level to open it. A level you
  built in one of the game's places shows "built". **Find a level** at the top
  of the list narrows it down.
- **Find any command**, at the top (⌘K on Mac, Ctrl+K on Windows): type what you
  want (a doorway, the music, a random level, Stone Hill) and press Enter. It
  finds every menu command, every card of every tab and every level.
- **Create ▾** makes something new: **New Level…**, **Random Level…**, **Random
  Homeworld…**, **New World…** and **New Level from Spyro: Year of the Dragon…**.
- **Save** and **Create BIN** come next, then a badge that gives the export check
  in a few words; click it for the full check.
- **☰** holds the project and the editor: **Open Game…**, **Save Level**, **Open
  Created Level…**, **Start Blank in Current Level…**, **Fill In What the Level
  Needs…**, **Restore Before Clear…**, **Free Deleted Objects' Save Rows…**,
  **Check for Export Issues…**, **Left Out of the Game…**, **Clear Entire Project
  to Base Game…**, **Project Data**, **Check for Updates** and **Help**.
- The **Selected** box at the top of the right-hand panel shows what you clicked
  in the view, whichever tab is open, with everything that acts on it (see
  "What you selected" under the Objects tab). Click its heading to fold it away,
  and again to bring it back. For a tile it offers **Show Tile Tools**, which
  opens the Ground tab.
- The five tabs:
  - **Ground**: the level's tiles: painting, moving, shaping and texturing them.
  - **Objects**: adding objects, Random objects, the list of what the level
    holds, and Recovery.
  - **Look**: this level's name, music, sky and lighting, and colours.
  - **Links**: how Spyro gets into levels and back out: Level Links, the balloon
    route, doorways, the world entrance, portals and Return Home.
  - **Game**: what applies to the whole game: Spyro's colours, the egg thieves'
    colour, Hover, Fly Anywhere, Play as Crash and memory, then the export check
    under "Before you play".

If you used an earlier version: the old **File ▾** and **More ▾** menus are gone.
Their commands that make something new are in **Create**, the rest are in **☰**,
and **Level Links…** and **World's Balloon Route…** are buttons on the Links tab.
The Terrain tab is now **Ground**, and its Doorways and World Entrance cards are
on **Links**. The Environment tab is **Look → Sky and lighting**. Game Settings
and the Colours tab are shared out: the level's name, music and colours are on
**Look**, portals and Return Home on **Links**, and everything for the whole game
on **Game**.

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
  changed or built, your new world, and the Game tab's settings.
- If something cannot be put in the game, the editor says what and where, and
  nothing half-built is written: the game you created last stays exactly as it
  was. **☰ → Check for Export Issues…** (or the badge beside Create BIN) runs the
  same check without building.
- **Fix It For Me**: whenever Create BIN stops on something in your project,
  this button appears beside the message at the bottom of the window (and in
  **Details**). One press gets the game made: the editor runs Create BIN itself,
  as often as it takes, and at the end says everything it did. Each run is a
  whole Create BIN, so a hard case can take several minutes; the status line
  says which try it is on.
  - What it can repair, it repairs. When your new world's homeworld has no
    balloonist (players reach your world by balloon, and he flies Spyro back),
    it stands one with his balloon on open floor near where Spyro arrives, and
    says how far away; move them afterwards if you like. (If you chose a
    doorway into your world as well and none is placed yet, Create BIN places
    it itself, on level open ground near where Spyro arrives in the homeworld
    the world is entered from; in Gnasty's World, which has no free place, it
    says so and you place it yourself.) When your
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
    every game. **☰ → Left Out of the Game…** lists each part with what
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

## Ground tab

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
  ground, water, …). On another tab, the Selected box's **Show Tile Tools**
  brings you back here.
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
  says so, unless **Game → Memory** is set to Emulator (8 MB RAM).
- **Add Terrain** copies the selected tile so you can move the copy into place.
  **Remove Terrain** (or Delete in the view) takes the selected tiles out.
  **Undo Terrain** puts the selected tile back the way it was, or brings removed
  tiles back.
- **Brush**: Raise, Lower, Smooth, Level or Restore the ground under the circle.
  Keys 1 to 5 choose a brush; 0 or Esc switches it off. **Undo Stroke** is
  ⌘Z / Ctrl+Z in the view; **Redo Stroke** is ⇧⌘Z / Ctrl+Y. With **Whole tiles**
  ticked the brush moves every tile it touches evenly. Brushes switch off when
  you leave the Ground tab.
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

Doorways and the World Entrance, which used to be on this tab, are on the Links
tab.

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

**Random objects** fills a level you built with enemies, chests, gems, dragons
and the way out (see Random objects under Random levels).

**Objects in this level** lists what the level holds; **Find placed objects…**
and the category list narrow it down.

**Recovery** holds **Restore entire level** and **Restore Before Clear…** (also
in **☰**).

### What you selected

Select an object in the view and the **Selected** box at the top of the
right-hand panel holds everything that acts on it, whichever tab is open:

- **Edit…** changes the name, gem or reward value, position and facing.
- **Replace…** swaps a chest or enemy for another kind known to work in this level.
- **Copy** and **Paste** (⌘C / ⌘V, or Ctrl+C / Ctrl+V, in the view) duplicate the
  selected object at the pointer.
- **Undo** puts the selected object back the way it is in the original game.
- **Remove** (or the Delete key in the view) takes the object out and leaves
  nothing selected. **Undo last removal** (⌘Z / Ctrl+Z in the view) brings it
  back.
- **Height and ground** slides the object up or down, keeps it on the ground
  while you drag it, or jumps it to the floor above or below.
- **What it gives** appears for an enemy, a chest or anything else that pays out
  a gem when beaten or opened: choose any of the five gems (red 1, green 2, blue
  5, gold 10, pink 25) and that is what Spyro gets. It works for the level's own
  creatures and for ones you added from other levels; the level's treasure total
  follows. A creature that gives nothing in the game (it gives butterflies, or
  comes back to life) has no such choice.
- For an egg thief (and any object that walks a route): **Edit Route…** and the
  **Its run** tools (see An egg thief's run). An egg thief you added shows these
  and **What it gives** too; before 2.5 they did not appear for one.

Dragons keep their pedestal and rescue scene with them when moved. The ENTRY
marker is where Spyro lands when he flies into the level. Double-click a dragon
statue to give it your own color.

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

**Add object… → Interactive → Chests & Containers** has **Spring chest (Teal
reward)** and **Spring chest (Rainbow reward)**: the game's own spring chest
holding a teal or rainbow gem. Flame it or charge it and the gem springs up out
of the lid; Spyro has to grab it quickly, or it vanishes in a burst of sparkles
and the chest closes, ready to be opened again (jumping onto the lid does not
open it, as with any spring chest). The gem adds 50 or 100 to collected
treasure and to the level's total, and once collected the chest is gone for
good. They can be placed wherever a spring chest can (not in Artisans, Gnorc
Cove or Twilight Harbor, which have no room for one), in the game's own levels
and in levels you build.

### Replace a Flight gate

In a flight level, select an original gate and choose **Replace Flight gate…**.
Pick a replacement, review its time bonus and fit check, then choose
**Replace gate**. **Undo** restores the original.

## Look tab

How the open level looks and sounds. What you choose is kept in your project as
soon as you apply it (the dragon statues' colour with the level, so press
**Save**), and Create BIN puts it in the game.

- **A level you built** (New Level, a random level, Start Blank, a level from
  another game) shows its own settings first: its name, where Spyro starts, its
  sky colour, its skybox (your own, or borrow a level's sky) and **Import a
  sky…**.
- **Level name** (one of the game's levels) changes the name on portals, loading
  screens and in the guidebook (capital letters, spaces and apostrophes).
- **Music** picks the level's track, for a level you built too (a random level,
  a New Level): **Save Music** keeps it in the level.
- **Sky and lighting**: the level's sky (below).
- **This level's colours**: its enemies, chests, dragons and animals (below).
- **Level details** says how many tiles and objects the level has and what you
  have changed in it.

### Sky and lighting

Recolor the level's sky, borrow another level's sky, use a ready-made one, or
import your own. The 3D view (Game Camera) shows the sky the game will draw
behind the level: the original, your recoloured or borrowed one, or the one you
imported, updated when you click **Apply Sky**, **Restore Original Sky** or
change a level of your own's skybox.

#### Importing a sky

**Import a sky** takes a 360° panorama picture (PNG, JPEG, WebP or BMP; twice as
wide as it is tall, as skybox makers export them), a sky model (OBJ or FBX, with
its textures next to it or packed inside), or a `.sky` file. In a game level
choose the **Import a sky** mode in Sky and lighting and **Choose Sky File…**; in
a level of your own use **Import a sky…** in the level's own settings at the top
of the Look tab.

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

### This level's colours

The open level's enemies, chests, dragons and animals, each drawn as it will
look, in four lists. Click a kind's colour to choose one of the quick colours or
type one as #RRGGBB and press **Use**; **Its own** puts it back. **Randomize**
beside a list gives that list's kinds other colours (press again for others),
**Randomize All** does every list at once, and **Their Own Colours** clears them.
A colour is for this level only, so the same gnorc or chest can be red in
Artisans and blue in Dark Hollow. A thing keeps its shading, and what is of
another colour on it (skin, armour, eyes) stays; a thing with no colour of its
own (grey metal, stone) takes the colour all over. In a level you built the card
lists what you placed in it. Two kinds that share a texture share its colour.

- **The whole game**: **Randomize Every Level** gives the enemies, chests and
  animals of every one of the game's levels other colours at once, each level
  its own (press again for others); **Every Level Its Own** clears them all.
  Levels you built and your world's levels keep what they have: open one and
  use Randomize All there.
- **Dragons**: **Dragon statues** sets one crystal colour for every dragon
  statue of the level (their own is green). It is kept with the level, so
  press **Save**. One statue alone can still be given a colour in its Edit
  window (Selected box). The **fairy at a freed dragon's pedestal** is
  listed too. The dragon who steps out of the statue for his speech keeps his
  own colours.
- The Green Wizard in three levels and Haunted Towers' Summoning Wizard are
  not listed: the editor cannot read their models' colours yet.

## Links tab

How Spyro gets into levels and back out, and how the whole game hangs together.

- **The whole game**: **Level Links…** shows every homeworld's doorways and every
  level's Return Home on one page; **World's Balloon Route…** places your world
  among the balloonists' worlds; **New World…** is the same as **Create → New
  World…**. All three are described below.
- **Doorways**: in a homeworld you rebuilt and in your world's homeworld (below).
- **World Entrance**: in the game's homeworlds, an optional doorway into your
  world (below).
- **Portals & Return Home**: choose where each portal leads and where Return
  Home goes. **Open Level Links…** at the top opens Level Links. **Select
  Portal** moves the trigger, lettering and travel path together; move the
  stone arch with the Ground tab's tools.

### Level Links: the way in and the way back

**Level Links…** (or **Open Level Links…** in Portals & Return Home) shows on one
page how Spyro gets into every level and back out:

- **Doorways**: every homeworld's doorways, named by the level they lead to in
  the game, each with a **leads to** list: the game's levels, your new level
  (Create → New Level…) and the levels you built in the game's places, by the
  names you gave them. A level you built in Stone Hill's place is reached by
  the Stone Hill doorway without any change here.
- Choose another level for a doorway and it is saved at once. **Return Home
  from that level then brings Spyro back out of that same doorway**, and the
  level the doorway led to before comes back out of the doorway that still
  leads there. Choosing the game's own level again puts the doorway back.
- **Return Home**: for every level, where its Return Home vortex (and Exit Level
  in the pause menu) brings Spyro back out. **Automatic** is the doorway that
  leads to the level; choose another doorway only if you want him to come out
  somewhere else. Each row also says whether the level has its Return Home
  vortex: a level you built needs one added (☰ → Fill In What the Level
  Needs…, or Add Object: search for "Return Home"), or Spyro can leave it only by
  Exit Level.
- At the top, plain sentences say what needs looking at: a level no doorway
  leads to, two doorways to one level (and which one Return Home uses), a level
  without its vortex, or a link Create BIN cannot make, with a button where one
  press puts it right.
- A homeworld you rebuilt (Start Blank or Random Homeworld) keeps one doorway
  for each of its own levels, each always leading to the level in that place;
  move, add or remove them with the Doorways card on the Links tab in that
  homeworld.
- Your own world (Create → New World…) is summed up at the foot, starting with
  one sentence on how players get there and back, for example "Players reach
  GLOOMY FEN by balloon: Artisans' balloonist flies Spyro there once he has
  found 50 treasure, and GLOOMY FEN's balloonist NILS flies him back to
  Artisans (and on to Peace Keepers)." Then whether its balloonist stands in
  its homeworld, which of its levels have a doorway there, and a doorway into
  it only if you chose one; with buttons to open the homeworlds and the balloon
  route. The homeworld whose balloonist flies Spyro to your world says so in
  its own part of the window.
- **Undo Last Change** takes the last change back. Create BIN puts the links in
  the game.

### Your world on the balloon route

The game's balloonists fly Spyro from world to world in a fixed order, each once
Spyro has done what he asks (ten dragons in Artisans, 1,200 treasure in Peace
Keepers, and so on). **World's Balloon Route…** gives your world (Create → New
World…) a place in that order.

- The window starts by saying how players reach your world and come back, in
  the same words as Level Links, and what your world's homeworld still needs.
- **Your world comes** after one of the game's six homeworlds: "After Dream
  Weavers, before Gnasty's World", for one. The balloonist of the homeworld
  before it then flies Spyro to your world, your world's own balloonist flies him
  back and on to the world that used to come next, and every balloonist's menu
  lists your world in its place once it is open.
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
  with it). He stands in your world's homeworld with his balloon: New World puts
  him there, **Objects → Random objects** places him ("The way out"), or add a
  balloonist with **Add object…**. If he is missing, Create BIN stops and Fix It
  For Me stands him near where Spyro arrives.
- **After Gnasty's World** your world is the last on the route: Gnasty's World's
  balloonist opens it with everything else and can ask for nothing; your world's
  balloonist then flies Spyro back to any world.
- **Not on the balloon route (a doorway instead)**: the balloonists do not know
  your world; players walk in through its doorway (the World Entrance card on
  the Links tab of the homeworld it is entered from) and out through its
  homeworld's doorway back.
- A world that has no place chosen follows the homeworld chosen in the New World
  window, with the game's own pace: free to your world, the game's price to fly on.
- The balloon sets Spyro down where your world's homeworld starts.

With your world on the route, a saved game made before (from the original game
or an earlier export) may find one more world open than it had.

### Doorways in a homeworld you rebuild

A homeworld you rebuilt with Start Blank (Artisans, Peace Keepers, Magic Crafters,
Beast Makers, Dream Weavers or Gnasty's World) has a **Doorways** card on the
Links tab. Other levels show it too, with the reason they cannot have doorways:
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
  frame, so the Ground tab's Ground card or Choose Texture… repaint it in one
  go; click single frame tiles to paint them one by one. Moving, turning or
  reshaping the doorway rebuilds the frame there and keeps its textures.
- In the game the opening shows the sky of the level it leads to. Spyro walks in,
  the level loads, and Return Home or Exit Level brings him back out of the same
  doorway. The game draws a doorway's sky inside an opening of at most ten
  corners that never bends inwards; the shapes are made that way.
- Doorways made before this tool (such as Lost Fleet's) keep working as they
  were; the first change finds their frame and moves it with them.
- Your new world's homeworld (Create → New World…) has the same card: a doorway
  to each of the world's levels. It needs no doorway back out: your world's
  balloonist flies Spyro back. Only if you chose a doorway into your world as
  well (below) does it offer one back to the game's homeworld the world is
  entered from (listed as "the way back out of your world").

### World Entrance: a doorway into your world as well (optional)

If you want players to be able to walk into your world (Create → New World…)
through a doorway too, open one of the game's homeworlds: its Links tab has a
**World Entrance** card. While your world is reached by balloon only, the card
says so and offers **Add a Doorway As Well…** (or **Build It As Well** for a
doorway placed there before). With a doorway, your world's homeworld also needs
its doorway back out to that homeworld (Doorways card, **Add Doorway…**, then the
homeworld's name). **Remove Entrance** makes the world balloon-only again.

- **Add a Doorway As Well…** (or **Place Entrance…**) asks for the doorway's
  shape (the same shapes as the Doorways card) and size, and puts it on the
  selected tile or on the ground in front of the camera, facing the camera:
  Spyro walks into it away from you. Give it open, level ground in front: coming
  back, Spyro walks out of it a good way, as he does from the homeworld's own
  doorways.
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
  with **Game → Memory → Emulator (8 MB RAM)**.
- Leaving your world's homeworld through its doorway home brings Spyro back out
  of the entrance. Add that doorway with the Doorways card in your world's
  homeworld (**Add Doorway…**, then the homeworld's name). Create BIN asks for
  both together: an entrance without a way back would trap Spyro in your world.

## Game tab

What applies to the whole game you create, in every level. What you choose is
saved at once and Create BIN puts it in the game; Create BIN can make a game
with only these settings, without any level edits.

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
  project. **A look for one level**: set **Colours for** at the top of the
  card to the open level, then choose a look or change a part; Spyro has it in
  that level only (Classic there gives him his own colours in that level,
  whatever the rest of the game has). **Same as the Whole Game** takes the
  level's look away again. The title screen and cutscenes always have the whole
  game's look.
- **Egg thieves' colour**: the egg thieves' robes in any colour in place of
  their blue: pick one of the ready colours, type one as #RRGGBB and press **Use
  This Colour**, or **Surprise me**; **Their own blue** puts it back. The card
  draws a thief in the colour. It is the same in every level that has thieves and
  for thieves you add to levels you build; they keep their shading and their
  eyes. In High Caves the tornado wizards wear the same cloth as the thieves, so
  they change with them.
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
- **Play as Crash**: Crash Bandicoot takes Spyro's place in the levels you tick.
  His model, animations and sounds come from your Crash Bandicoot: Warped disc
  (chosen with **Choose Crash disc…**, as for the Crash boxes). Each level needs
  about 70 KB of free game memory: Create BIN names any of the game's own levels
  that are too full, and levels you clear with Start Blank or build with New
  Level have room.
- **Memory**: how much memory the games you create may use. **Emulator (8 MB
  RAM)** is the usual choice and what a project has until you choose: each
  level's objects load into the extra memory DuckStation has with Settings ›
  Console › Enable 8MB RAM, so levels hold more. A game made this way still
  plays on a normal 2 MB set-up wherever its levels fit 2 MB, and Create BIN
  says which levels need 8 MB. Choose **PlayStation (2 MB)** for a real console
  or a disc: every level must then fit 2 MB and Create BIN stops when one does
  not. (The 8 MB setting goes into the unchanged USA game only; with any other
  game open, games are made for 2 MB as before.)
- **Before you play**: the export check lists anything that would stop Create
  BIN, the same as the badge in the top bar and **☰ → Check for Export
  Issues…**.

## Levels of your own

- **Create → New Level…** starts a level of your own that opens when you choose
  New Game. It begins as a meadow that already looks like a Spyro level: grass,
  a stone path from the start, a ramp up to a raised ledge, a pond with a sand
  rim, and Artisans' sky. Repaint it with the Ground tab's **Ground** card, move
  or remove its tiles and add your own. It uses the same Ground and Objects
  tools. A project holds one such
  level: if you already have one, the editor asks before replacing it and keeps
  a copy in the project's `backups` folder.
- **☰ → Open Created Level…** reopens the level you made.
- **☰ → Start Blank in Current Level…** replaces the open level with the
  same meadow so you can rebuild it. A recovery copy is saved first;
  **Restore Before Clear…** brings the previous version back, also after
  reopening the project.
- **☰ → Clear Entire Project to Base Game…** starts the whole project over.
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

A rebuilt level holds up to 4,096 terrain tiles. Untextured tiles are split into
smaller triangles automatically. Walls stop Spyro from the side you can see them.
The game gives close-up detail to the textured tiles near the camera, and only so
many at once: Create BIN checks camera positions all over the level and names the
place if textured tiles are too crowded there. Use larger tiles there (a few hundred
units across, like the game's own) or untextured faces.

### How players reach your new world

Players reach the world made with **Create → New World…** (also on the Links tab)
**by balloon**, like the game's own worlds: the balloonist of the homeworld
before it on the route (the one chosen in the New World window, Artisans unless
you choose another) flies Spyro there once he has what he asks, and your world's
own balloonist flies him back. **World's Balloon Route…** on the Links tab sets
where it lies and what each balloonist asks (see Your world on the balloon
route). Nothing is built into the game's homeworlds, and your world's homeworld
has no doorway back out. A doorway into your world as well is your choice: see
World Entrance under the Links tab.

- New World puts your world's balloonist and his balloon in its homeworld at
  once, near where Spyro arrives (when your original disc is chosen). If he is
  ever missing, Create BIN stops and Fix It For Me stands him there again.
- A Random Homeworld made in your world's homeworld's place has a doorway to
  each of your world's levels and none back out; it has no balloonist until you
  fill it with objects (Random objects, "The way out") or Fix It For Me stands
  one.
- Your world is called by its homeworld's name, as the game's worlds are: a
  Random Homeworld made in its place names it (GLOOMY FEN instead of SKY ISLES),
  and renaming the homeworld (its name on the Look tab) renames the world. The
  balloonists' menus and the inventory show that name.
- A project whose world was made before works the same way: it is reached by balloon.
  A doorway into it that was placed before stays in the project but is not
  built, and Create BIN takes the world's old doorway back out of its homeworld
  (keeping the level as it was in the project's `backups/fix-it` folder) and
  says so. Its balloon route and prices stay as they were.

### Your new world in the game

**Create BIN** builds your world into the game with everything else: its
homeworld, its levels and their doorways, and the balloonists' route with your
world on it (and, only if you chose one, the doorway into it and its way back).
Its status line ends with the sentence that says how players reach your world.

- Spyro flies in by balloon and his balloon sets him down where your world's
  homeworld starts; your world's balloonist flies him back (**Links → World's
  Balloon Route…**). In your world's homeworld each doorway
  leads to its level; **Return Home** and **Exit Level** in a level bring him back
  out of that level's doorway. With a doorway as well, he can also walk in
  through it, and its doorway home takes him back out of it. Create BIN can also
  start **New Game** in your world.
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

**Create → Random Level…** makes a whole new level the way the game's own levels
are made, from a seed and a few choices, then puts it where you say so you can add
objects and enemies yourself.

- **Laid out like the game's levels**, in one of eleven ways. Four are one piece
  of land of many parts, each at its own height, running into each other on the
  level, a step or a flight of steps up or down, now and then a path out to a
  part further off: a wide land with raised and sunken parts round a great middle
  (like Artisans); a low floor with plateaus standing round and in it, some on
  others (like Dry Canyon); long terraces climbing a hillside, zigzagging up (like
  Magic Crafters); and squared yards side by side on a grid at their own heights
  (like Town Square or Lofty Castle). The others: a way from room to
  room with a clear end (side rooms off it, sometimes a ledge over an earlier
  room); a ring that comes back over its start (the way round ends with a drop
  back to it); a clearing with rooms round it; one great room with a
  hill, a stepped mound or a moat in the middle; a hill of two to four terraces,
  each reached by a ramp that winds round it, with steps to the summit and a
  glide down; a winding gorge whose reaches run together with steps between
  them; or a web of rooms with more than one way round. Rooms are anything from
  about 300 to 1,150 units across: some levels have a few big rooms, some many
  small ones. Rooms have flights of steps, long paths that climb as they wind,
  raised terraces, hollows, islets in pools, steps to jump up, lookouts to glide
  from and hidden nooks; hilltops and islands get one of the look's tall things
  or a tower. Rooms are not round: their edges are rock shelves with flat sides
  and corners, long terraces or squared yards, straight across where a path or a
  flight of steps meets them.
- **Built on and watered like them:** houses with pitched roofs, doors and
  windows round the rooms, a few together like a village (Spyro walks round
  them); castle walls with a tower at each end along rooms' straight edges, with
  battlements, and battlements along a castle level's own walls; rivers across
  big rooms with a wooden bridge over them; marshes at a room's side with a
  boardwalk or stepping stones out to an islet with treasure.
- **Built like them:** one sheet of four-cornered faces about 128 units across
  whose lines follow the edges of the land, as in the original levels, enclosed by
  steep banks, rock walls, peaks, open water or the sky.
- **Painted like them:** with the textures the chosen level itself uses for its
  ground, banks, rock and paths, in the colours that level paints them with, under
  its sky and to its music. The steep side slopes (the banks) are bare rock all
  the way down, with no odd patches of grass. With your project's levels loaded,
  that level's own towers, trees and rocks are lifted from it and stood in the
  new level.
- **Named like them:** each random level and homeworld gets a made-up name of at
  most 15 letters, the most the game's level window shows (so a homeworld made in
  your world's place can always give the world its name). Change it on the Look
  tab.

The choices:

- **Kind of level:** clearings among the hills (like Artisans), islands in the
  water, islands in the sky (like Lofty Castle), caverns and tunnels, a canyon
  (like Dry Canyon) or castle courtyards. **Surprise me** lets the seed choose.
- **Laid out as:** one of the eleven layouts above, or **Surprise me** to let the
  seed choose one that suits the kind of level.
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
  that Spyro reaches only by gliding from it), pools and marshes, bridges
  (between islands; without them, stepping stones), hidden nooks, rivers, houses
  and castle walls. A tick asks for a feature, an empty box leaves it out, a
  filled square leaves it to chance.
- **Water, ooze or lava:** what the rivers, marshes, pools and moats are full
  of: water, the purple ooze or lava, or **Surprise me** (the look's own as a
  rule, now and then another). On Hard and Kaizo a fall in costs a life at once.
- **Textures:** that level's own textures and colours, or new pictures painted in
  the look's colours. **Use My Own Pictures…** puts your own picture (PNG, JPEG,
  WebP or BMP) on any kind of surface; pictures need your original disc.
- **Sky:** that level's sky, or a sky painted to match. Below the horizon the sky
  takes the ground's colour (except on sky islands), so the hairline gaps the game
  leaves near the camera between finely cut and whole floor tiles show ground, not
  sky.
- **Music:** the song the level plays: the look's own (one of its level's songs,
  picked by the seed) or any song of the game. A game level's place can also take
  the songs brought in from other games. Change it later in **Look → Music**.
- **Seed:** the same seed and choices always make the same level. **New Seed**
  makes another; **Rebuild** makes this seed again with changed choices. (A seed
  makes a different level than it did in 2.2.)

Rooms have **trodden paths** across them, from every way in (a path, a doorway,
where Spyro starts) to a meeting place near the middle, in the look's path surface;
gems lie in lines along them. A level with pools and marshes has at least one
marsh: a wide pool at a room's side with an islet, a boardwalk (or, when hard,
stepping stones) and treasure on it.

Every jump, glide and climb is built inside what Spyro can really do (measured in
the game): steps are 56 units high, stepping stones an easy jump apart, paths
slope no more than about 16°, a charge ramp about 26°, and a glide's ledge lies
within the glide from the lookout's height. Before a level is shown, the editor
checks on the finished faces that every floor can be reached from the start and
left again, and tries another layout if not.

**Put it** in place of the open level (a recovery copy is kept, and **Restore
Before Clear…** brings it back), as the project's new level, or in a level of
the world made with **Create → New World…**. Tick **Then fill it with objects for
me** to have Random objects (below) place enemies, chests, gems, dragons and the
way out at once, at the level's difficulty; otherwise add them yourself with Add
Object. Everything the generator makes is ordinary terrain: paint, move or remove
any tile afterwards.

### Random homeworlds

**Create → Random Homeworld…** (or **Make: A homeworld** in the Random Level
window) makes a homeworld, laid out in any of the level layouts; left to chance,
it is one land (a wide land, plateaus, terraces or yards) or a web, ring, climb or
journey. A doorway stands for every level the homeworld leads to: a big part of
the land holds two or three near its edge, backs to the bank and facing the land,
a smaller part one in its middle, each with open floor in front of it (which may
run on into the next part on the level) and behind it for Spyro to come back out
onto. Spyro flies in through the lower part
of each opening, whatever its shape and size, so every doorway loads its level.

- A generated homeworld goes in the place of any of the game's six homeworlds
  (open that homeworld first) or in the homeworld of your own world.
- In a homeworld's place it gets a doorway to each of that homeworld's levels
  (in Artisans' place: Stone Hill, Dark Hollow, Town Square, Toasty and Sunny
  Flight; four in Gnasty's World). In your own world it gets a doorway to each
  level you made (and the way back out to the game only if you chose a doorway
  into your world as well: otherwise its balloonist flies Spyro back), and your
  world takes its name.
- The doorways' look follows the level and is different every time: a shape of
  the look's kind (round, wide and tall stone arches in the Artisans' style;
  square, slanted and gabled gates for the Peace Keepers; pointed and tall
  arches for the Magic Crafters; six-sided and gabled gates for the Beast
  Makers; rings for the Dream Weavers; wide arches and heavy gates in Gnasty's
  harbour), usually one shape for the whole homeworld as in the game, sometimes
  a different one for each doorway; each doorway its own size, the farthest one
  the grandest; and frames of the level's own stone in the level's colours. The
  destination's sky fills each opening exactly. The **Doorways** card on the
  Links tab moves, turns, reshapes or retargets any of them afterwards; their
  frames are ordinary tiles you can repaint.
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
  whose key lies somewhere else in the level, floating as the game's keys do,
  most often up on a ledge or a perch (jump up to it).
- **Teal gems (50)** and **Rainbow gems (100)** (off unless you tick them): rare
  gems worth 50 and 100, each alone at one of the hardest places to reach (a
  perch, a high place, the far end of a way), counted in the treasure you typed.
  A level needs about 100 gems of treasure for a teal gem and 200 for a rainbow
  one (150 for one teal when both are ticked, 300 for one of each); up to four
  teal and three rainbow gems. The card says when the treasure is too low.
- **Trees, torches, flowers** (on unless you untick it): the level type's own
  trees, cactuses, torches, lamps, flowers and grass, placed where the game puts
  them: trees in little groups along the banks, torches and lamp posts along a
  random level's paths and trodden paths, a few hundred units apart on alternate
  sides. They go in last, so they use only save rows nothing else needs.
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
  (Select any of them and use **What it gives** in the Selected box to choose
  yourself.)
- Loose gems are mostly reds and greens, in ones, twos and threes about a hundred
  units apart, a group all one colour, more than half of them by the walls and
  the rest along the paths and out in the rooms; the best gems go to the hardest
  places, and one of every colour is always there when the total allows.
- Chests stand in ones, twos and threes a chest's width apart, most of them a
  step or two from a wall or where a walk ends. Enemies wait in ones, twos and
  threes (now and then a pack), spread through the rooms beyond the first,
  facing the way Spyro comes in, and on the paths between; most chests and gems
  stand where no enemy is close, as in the game.
- About a quarter of the chests and gems go up on raised places and small
  separate floors ("perches"), most keys go on a perch, and the extra life and
  the locked chest go to tucked-away places. Three or more gems are as often a
  little knot (a triangle, a square, a ring) as a line. Everything stands on the
  level's floor, never out on its banks or in the sea.

Nothing is put inside a tower or against a wall, and enemies keep away from a
homeworld's doorways. Before anything is placed, each kind is tried on in the
level the way Create BIN will build it: a kind this level cannot hold (some
creatures need things of their own level, and some kinds cannot share a level)
is left out and named, so what Randomize places always builds. Everything
Randomize places is an ordinary object: move, change or delete any of it. A level with few save rows left (a level built in
Artisans' place, for one) gets less of everything, each thing worth more; the
card says so.

### Egg thieves

**Add object… → Enemies → Egg Thieves** puts an egg thief in any level or
homeworld, also one that has none. Each egg thief in the game is one dragon egg:
the inventory, the 100% count and Gnasty's Loot's door count the eggs the game
really holds, so one added makes 13, one taken out 11. Create BIN says how many
eggs the game has when it is not the game's 12. Eggs of thieves past the third
world, in a level you built or in your new world are counted together in a spare
place the save keeps, so they are kept like any egg.

### An egg thief's run

Select an egg thief (one you added, or one of the game's own; also any added
object that walks a route, such as a sheep): **Edit Route…** or **Edit Run
Path** and the **Its run** tools appear in the Selected box, and the run's
numbered points show in the 3D view. (In 2.5 they appear for an egg thief you
added too, with **What it gives**; before, they were not shown for one.)

- Drag a point in the 3D view to move it (it stays on the ground), or type the
  points in **Edit Route…**.
- **New Loop Here** makes a new round run about where the thief stands, as wide
  as the open floor allows; press it again for another.
- **Bigger**, **Smaller** and **Turn** change the whole run about its middle;
  **Other Way Round** makes him run it the opposite way; **As It Came** brings
  back the run he had in his own level.
- A point goes onto the ground the thief runs on, and only where he can still
  run to it and on from it: ground all the way, and no step up or down of more
  than 150 units (where the game's own run already has a bigger step, that one
  stays allowed). Water, lava and ooze count as no ground: the thief could run
  there, but Spyro sinks and can't catch him. A point he can't take stays where
  it was and the editor says why; the run card warns while a thief stands in
  water.
- The run keeps to one level of floor (a thief cannot jump), and no step is
  longer than the game can follow. Give a thief room to run and Spyro room to
  chase: a round of 1,500 to 3,000 units on open floor plays best.

## Levels from other games

Open the level whose place the new level takes (a level or homeworld, not a flight
level), then choose **Create → New Level from Spyro: Year of the Dragon…**. It
rebuilds one of Year of the Dragon's levels from your own Year of the Dragon (USA)
disc: its ground, walls, textures, water and lava, its distant scenery, and a start
where its level begins. Lava and acid can be deadly as in that game; deep water costs
a life, because Spyro can't swim here. Its sky's shapes are not converted: the level
gets a plain sky colour, or borrow a Spyro sky with the skybox in the level's own
settings on the Look tab.

Crash Bandicoot: Warped's boxes, enemies and songs are still offered in Add Object and
the Look tab's Music; building whole Crash levels is not offered for now.

Building takes about a minute. The open level is emptied the way Start Blank empties
it, with a recovery copy, and keeps playing its own song. Characters, enemies and
boxes of the other game are not brought over.

**☰ → Fill In What the Level Needs…** (done for you at the end of the command
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
route changed with **Edit Route…** in the Selected box.

Create BIN keeps every level you build within what the game can draw in a frame: it
checks the views from every spot Spyro can reach and, where a view would draw too
much, draws distant scenery more simply and brings textures a little closer. A level
that is still too heavy is refused with the place named.

## What this package does not contain

No game image, BIOS, emulator, memory card or extracted game assets. See
`public-known-limitations.md` for the supported scope and what has been verified.
