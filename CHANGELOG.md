# Spyro Editor 1.2

## Changes since Spyro Editor 1.1

New

- Tile Type…: in the Terrain tab's Tiles card, choose what Spyro finds on the selected tiles — solid ground, water or lava that hurts, a supercharge ramp, and whatever else the level already defines. The picture stays; the ground behaves differently. The Tiles card names the kind of the selected tile. Undo Terrain puts it back.
- Fly Anywhere can be limited to chosen levels: open "Choose levels" under the whole-game switch and tick the levels you want. Everywhere else Spyro moves as in the original game. A level of your own flies whenever at least one level is ticked.
- Lower now works on tiles at the floor of their part of the level. The game stores each part's heights within a band that used to start exactly at the lowest tile; Create BIN now moves the band when a lowered corner needs it.

Clearer

- Terrain edits are refused at the moment you make them, in plain words, instead of failing at Create BIN: tiles without collision (scenery), steps over 255 inside one piece of collision, folded ground, a surface within 64 above, crossing into another 256-unit collision cell, and the height and width bands of a part of the level. The message says what to do instead.
- Raise, Lower and drags move only the corners the game has collision for, and move corners shared with neighbouring tiles along with them, which is what the game does anyway. The status line says how many corners followed or stayed.
- A brush stroke leaves the corners it cannot move where they are and says how many; whole-tile strokes keep shared corners joined across tiles the brush did not touch.
- Find space for this object… shows which version and option it is trying, lists each way to make room as soon as it is found, and has a Stop button. The search is bounded (6 versions, 48 options each). When no original objects would make room it says so instead of searching on.
- The Create BIN question and result name the levels Fly Anywhere is on in.

Fixed

- Find space for this object… could appear stuck for minutes on a static message with no way out.
- Create BIN's "visual-only geometry edit" warning appeared for tiles whose moved corners all have collision; the warning now looks at the corners that moved.
- Tiles cut along the other diagonal than their collision triangles can now take a texture's kind (and Tile Type…).

# Spyro Editor 1.1

## Changes since Spyro Editor 1.0.10

New

- Fly Anywhere: Game Settings has one switch, "Let Spyro fly in every level". With it on, the game you export lets Spyro fly in every normal level (jump, then ✕ again in the air), as in Gnasty's Loot. It is off unless you turn it on, it is saved with your project, and turning it off gives the normal game back. The five flight levels keep their own rules. It has nothing to do with the editor's Game Camera.
- A welcome screen on first start explains the three steps (open your game, edit, Create BIN). The editor reopens the level you last worked on.
- When terrain cannot be exported (for example a corner dragged until the ground folds over itself), a red bar says so in plain words, with "Show it" and "Undo that change". Check for Export Issues now finds this too, and the Add object window keeps working.
- "Show Game File" appears in the bottom bar after Create BIN and opens the folder with your new game.
- In the view: Delete removes the selected object, Ctrl/Cmd+Z brings back the last removed object or undoes the last brush stroke, Ctrl+Y or Shift+Cmd+Z redoes a stroke.
- File > New Level asks before replacing a level you already made, and keeps a copy of it in the project's backups folder.

Clearer

- Objects tab: "Add to this level" and "Selected object" cards, a longer object list, plain notes about what is selected.
- Terrain tab: four groups — Tiles, Brush, Textures, Corner by corner — instead of three overlapping button sets. "Reset Height" puts a tile back at its original height and keeps its texture.
- Game Settings and Environment: "Apply Name", "Apply Music", "Apply Sky"; portal lists show level names.
- Everyday wording throughout: level details, treasure line, save and load messages, export results, object kinds, the Start Blank and Create BIN questions. "Build Safety" is now "Export Check".
- The view shows object names without internal numbers, a short hint line that fits the window, and a legend only while you work on terrain.
- Help is rewritten for the current layout.
- The status bar describes terrain work in plain words ("Raised the tile by 32", "Undid the brush stroke"). Details keeps the full technical text.
- Selecting an object in the view keeps its card (Edit…, Remove) on screen instead of scrolling the panel down to the list.
- The view stays where it is when you place or paste an object by clicking; it no longer jumps to the new object.
- Coloured buttons keep their colour under the pointer instead of turning grey.
- While the game is being read for the first time the view says so, and the result is reported in one plain sentence.
- The question asked when you close with unsaved terrain explains that object changes are kept automatically.
- The Create BIN question lists only the levels you changed; Fly Anywhere has its own line.
- Project Data and the Export Check window are plainer: Project Data says where your project is kept in everyday words, and Export Check is only as tall as what it has to report.
- In a level you built, tiles without a known surface are called "Tile" instead of "Unknown tile".

Fixed

- With Fly Anywhere on, entering a level through a portal could crash the game while the level loaded. Found by playing the exported game and fixed before this feature was released.
- Create BIN left 1.3 to 2.6 GB of working copies in the project folder every time. They are now removed after every export, also when the export fails, and old ones are cleaned up at start.
- Create BIN failed with "collision block storage contains unreferenced groups" in a project that both moved a portal and added or removed terrain in the same level.
- Create BIN failed with "authored surface consumer authority requires the complete exact original USA executable" when a level had both changed gem totals and added or removed terrain.
- In a level you built yourself (New Level or Start Blank), painting a texture on the starting floor, or on any tile wider than 255 units, made Create BIN refuse the level. Large painted tiles now export.
- A failed Create BIN never touches the game you created before; this is now tested.
- A single-letter key (C or V) could copy and paste a tile's look without any hint. That shortcut is off.
- Create BIN did not look at free disk space. On a nearly full disk it kept writing working copies of the game (several gigabytes) until nothing was left. It now says how much room it needs and starts nothing when there is too little.
- When the project could not be prepared at start (for example macOS had not allowed the Documents folder, or a file in the project's support folder was in the way) the editor ended without showing anything. It now opens a small window that says what is wrong and what to do.
- Clicking an object to select it could move it a little: any pointer movement between pressing and releasing counted as a drag. Objects and their facing handle now need a real drag (a few pixels) before they move or turn, like terrain tiles.
- After removing an object the editor selected the first object in the list by itself, so a second press of Delete would have removed something you never chose. Nothing is selected after a removal now.
- The camera keys (W A S D Q E and the arrows) also reacted while Command, Control or Alt was held, so a shortcut could nudge the view. They are bare keys only now.

All existing gameplay limits are retained; see the known limitations. The Mac package is not notarized; the Windows package has not been run on Windows.

## Spyro Editor 1.0.10 — changes since Spyro Editor 1.0.9

- Terrain brushes and texture painting turn off when you leave the Terrain tab. The brush keys 1–5 only work in the Terrain tab, so clicks in Objects and the other tabs always select objects.

## Spyro Editor 1.0.9 — changes since Spyro Editor 1.0.8

- Inventory: every row now uses one text size, so level names, dragonfly eggs, treasure and dragons line up and fit.
- Inventory: the dragonfly egg column has a spinning gold dragonfly egg above it, like the treasure chest and dragon. The egg picture and project total at the top of the screen are gone; levels without dragonfly eggs show a dash.
- Terrain: select several tiles with Cmd-click (Ctrl-click on Windows) or Cmd-drag, then remove them together with one confirmation. Undo Terrain brings removed ones back.
- Terrain: new brush option "Whole tiles" (on by default) raises, lowers or levels every tile the circle touches evenly instead of pulling single corners into spikes.
- Terrain: Create BIN no longer fails with "stored in N independent HP vertex slots" after removing or editing tiles. Shared corners are updated in every copy the game keeps, and tiny accidental moves from clicking are ignored.
- Terrain: clicking a tile or corner never nudges it; dragging starts after a short movement. Dragging a corner so far that the ground would fold over itself is blocked with an explanation.

All existing gameplay restrictions are retained. Universal Moby gameplay remains incomplete. The Mac package is not notarized; the Windows package has not been run on Windows.
