# Spyro Editor 2.3

## Changes since Spyro Editor 2.2

New

- A **Colours** tab beside Game Settings holds everything about looks and moves: this level's colours, Spyro's colours, the egg thieves' colour, Hover, Fly Anywhere and Play as Crash. Memory, names, music and portals stay in Game Settings.
- **This level's colours** (it was Creatures' colours) now lists the open level's **chests**, **dragons** and **animals** as well as its enemies, each list with its own Randomize, and Randomize All for everything. A colour is for that level only, so chests can be blue in one level and red in the next. For dragons, one crystal colour goes on every dragon statue of the level (it used to be set statue by statue in the object's Edit window), and the fairy who waits at a freed dragon's pedestal can be given a colour too.
- Enemies and things with no colour of their own (grey metal, stone) can now be given one; they used to be missing from the list.
- **Memory is Emulator (8 MB RAM) unless you choose otherwise**, so levels hold more and fewer changes run out of room. A game made this way still plays on a normal 2 MB set-up wherever its levels fit 2 MB, and Create BIN says when one does not. Choose **PlayStation (2 MB)** in Game Settings › Memory for a real console or a disc. A project that never chose now gets 8 MB.
- Create BIN places the way into your new world by itself when there is none yet (on open ground near where Spyro arrives in the homeworld; move it with Terrain › World Entrance). It used to stop there.
- Fix It For Me leaves out far less. When a level's objects stop Create BIN, it now finds which **kinds of object you added** are the cause (every copy of one creature, say) and leaves out only those; your other objects and your changes to the level's own objects stay in the game. It tries the added kinds in halves, which takes a few more runs of Create BIN.
- When Fix It For Me has left anything out, a window opens that says what and why, with Put Back beside each thing. It is no longer only a line at the bottom of the window.
- After Create BIN the status says how much room the levels you added objects to have left, the fullest first, and warns when one is nearly full.

Fixed

- A homeworld with the way into your new world and an added object that brings its own sounds or sparks (a Crash frog or crate, a creature from another level) made Create BIN stop with "New World doorway failed its readback: particles and sounds changed". Fix It For Me then got the game made by leaving out every object change of that level. The check was wrong, not the level: such levels are made whole now.
- The export check said "Ready to export" when all it had checked was your object changes. It now says what it checked, and that Create BIN checks memory, doorways and your world while it builds.
