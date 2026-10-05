# Spyro Editor 1.1 — supported scope and limitations

- Version 1.1 reorganizes the interface (first-run welcome, carded Objects and
  Terrain tabs, plain wording, one Export Check), adds **Fly Anywhere**, a red
  bar that explains and undoes terrain the game cannot stand on, **Show Game
  File**, Delete / Undo keys in the view, and a confirmation with a backup
  before New Level replaces a level you made. It fixes Create BIN leaving large
  working copies in the project, and two export failures in projects that
  combine a moved portal or changed gem totals with added or removed terrain.
- Version 1.0.10 turns terrain brushes off outside the Terrain tab.
- Version 1.0.9 aligns the in-game inventory (one text size, a spinning gold
  dragonfly egg column header) and adds multi-tile terrain removal, a whole-tile
  terrain brush and fixes for "independent HP vertex slots" Create BIN errors.
  The gold egg header needs the Japanese disc at Create BIN (as dragonfly eggs
  do); it is skipped in a level that already uses its class slot. Tiles that
  would only hide ground Spyro can still stand on are still kept, and corner
  drags that would fold the ground are refused.
- Version 1.0.8 adds navigation and removal to the removal-plan inspector and
  retains the object-picker responsiveness fix. It also retains the guarded
  enemy groups and dragon statue colours from 1.0.6.
  The Magic Crafters druid pair in Artisans has fresh combat, Nestor rescue,
  2+5 gem collection, normal card save and cold-load verification. The reloaded
  inventory retains 7/94 gems and 1/4 dragons. A second defeat after reload is
  not yet verified. The unfinished Alpine group-one renderer stays disabled.
  Mac notarization, Windows gameplay and a whole-campaign playthrough remain open.

- Teal 50 and rainbow 100 gems are available in every level's object browser,
  with brighter, double-size models, idle sparkles, collection bursts and native
  treasure/save accounting. Normal Create BIN only needs the USA disc for these
  gems. All 35 retail slots and the fresh level passed composition/readback
  coverage; a full level can still require explicit removals to fit. Native
  collection and card persistence were checked in Artisans, with collection
  and the revised appearance also checked in the fresh level. This is not a
  full playthrough of every destination or every gem mixture.

- Portal destinations and per-level Return Home routes are editable. Routes
  require the original physical doorway and its travel path to remain present.
  Moving its stone arch is a separate terrain edit. Several doorways can share
  a destination, but each destination has one selected Return Home doorway.
  The fresh level must be saved before ID65 is offered. Native checks may refuse
  routes in a full level; the current full Twilight Harbor fixture lacks 400
  bytes of required buffer space. A route does not create a complete campaign.
- Cross-level imports have broader class remapping and dependency support, but
  **all Mobys working in all levels is not complete**. Static composition does
  not certify AI, sound, collision, triggers or linked behavior. The browser and
  exporter report unresolved dependencies and native resource limits.
- Original Flight gate and chest replacement is supported only within matching
  objective categories and the unchanged original course. Gate replacements
  keep the original position and crossing plane. Some compatible choices still
  exceed memory capacity. A Sunny gate in Crystal and an Icy chest in Night
  passed their native objective count, time bonus, results and retry checks;
  full course completion, persistence and other combinations remain unverified.
  Flight objectives in ordinary levels are not enabled by these adapters.
- The Wizard Peak Armored Druid source variant has passed appearance, enemy
  attack, flame resistance and charge defeat in the edited Artisans test project.
  The Alpine Ridge row-1 variant now imports its missing player reaction
  animation and has passed native contact damage and recovery in Artisans.
  Enemy defeat and card reload were not exercised in that specific retest.
  Fitting into memory is separate from passing the game's behavior checks.
- Imported red, green, blue, gold and purple gem variants from Jacques passed
  native collection, ordinary card save and cold Load Game in Artisans. Their
  values remained 1, 2, 5, 10 and 25; all five stayed collected after reload.
  The final collection-effect retakes passed in both Artisans and a fresh level;
  the corrected game also loaded the earlier native save without duplicate gems.
  This verifies those exact variants and destination, not every source/level pair.
- Fresh levels use the regular editor and normal Create BIN, with authored
  geometry, collision, static native materials and Mobys. This build supports
  one fresh level in slot 65, at most 255 generated triangles, and the native coordinate,
  collision and memory limits. Vertices use whole editor units; collision vertices
  must stay within the native relative-coordinate range. Untextured faces are subdivided
  internally into whole-unit triangles no wider than 64 units per axis. Exact
  subdivision can require more triangles than the visible editor faces;
  unsupported shapes or excessive totals are refused.
  A fresh level does not automatically receive a portal network or campaign.
  Multiple placed objects retain the native dynamic-object reserve. A six-object
  scene passed collection, jumping and landing with 43/43 gems and one egg.
- **Start Blank in Current Level** saves a recovery snapshot and replaces terrain,
  collision and placed objects with an editable starter floor. It supports
  Artisans, Stone Hill, Town Square, Peace Keepers, Dry Canyon, Cliff Town, Magic
  Crafters, Beast Makers, Dream Weavers and Gnasty's World, plus the fresh level.
  Other slots remain unsupported. Cleared Town Square and the separate fresh
  level share game initialization storage and cannot be combined in one export.
  Player/companion models, the engine overlay, shared sound and particle support
  remain; this is not a proof of the smallest possible engine footprint.
  Recovery works after reopening. Old collected-state identities remain retired.
  Export requires a continuous 384-wide landing area around the player start
  for rebuilt retail levels and portal-accessible fresh levels. Older small
  starter floors may need enlarging. This checks floor coverage; keep the
  arrival area clear of walls and objects and test the finished route in-game.
- Existing-level terrain supports guarded XYZ edits, face copying, face removal
  and texture painting through the shared editor controls. Close and distant
  geometry, visibility and native collision are updated together. Native
  coordinate, collision, memory and ownership checks can refuse a particular
  edit. Translucent source faces and ambiguous collision ownership are not
  supported by structural terrain editing. These limits are separate from the
  fresh-level triangle limit.
- Removed objects free reusable physical slots. Models and textures are shared:
  deleting one copy will not free artwork still used by other copies or linked
  objects. **Find space for this object…** names proposed removals and checks
  the resulting composition; you choose which objects to remove. Saved
  collected-state identities remain finite and are not recycled. A successful
  capacity check does not certify every enemy's AI or linked behavior.
- Eye-free eggs, per-level/global inventory, collection and normal card save and
  cold-load were observed in Stone Hill. Checkpoint navigation for that save test
  used position-only assistance. Endgame bonus predicates have source and
  instruction tests; a complete endgame playthrough and every egg/level lifecycle
  combination have not been verified.
- Normal Create BIN includes saved retail edits and the saved fresh level.
  Its dialog chooses where New Game starts. Both choices passed complete
  readback with the existing Artisans project, including its eight imported
  objects and private terrain texture. The saved fresh level also passed native
  New Game, walking, jumping and egg collection with its 1/1 inventory count.
  This does not certify every authored scene or a complete campaign.
- **Fly Anywhere** is one switch for the whole exported game, off by default.
  With it on, Spyro can fly in every normal level (jump, then ✕ again in the
  air); the five flight levels keep their own rules. It was played in the
  emulator in Artisans, Stone Hill, Toasty and Doctor Shemp: take-off, climbing,
  turning, landing, entering levels through portals, Exit Level while flying,
  and a death and respawn while flying. The Icy Flight demo plays frame for
  frame the same with the option on and off. With the option off the game only
  glides, and its files are identical to a project that never used the option.
  The other 26 normal levels, Gnasty's Loot among them, were not played with it. Flying
  lets the player reach places the levels were not built for, including out of
  bounds; that is the nature of the option. It is not stored on the memory card:
  it belongs to the game you export.
- Signs show one message at a time when several are close together. A sign
  with no message shows nothing.
- The macOS arm64 package was opened and used on the build Mac. It is signed
  but not notarized, so on another Mac macOS asks for confirmation the first
  time (see `MACOS-OPEN-INSTRUCTIONS.txt`). macOS also asks once whether the
  editor may use the Documents folder, where projects are kept, and the folder
  your disc image is in (Desktop, Downloads); the editor's window appears after
  the first question is answered. The Windows x64 package
  structure is checked; the Windows build was not run, because no Windows
  machine was available.
- Create BIN wants about 2.1 GB of free disk space (3.3 GB with a level you
  built) and refuses to start with less.
- Use the supported unchanged USA SCUS_942.28 source. Japanese eggs/signs require
  the supported SCPS_100.83 image supplied locally. Custom music import and every
  native texture format are not supported.

Original discs, projects, saves and earlier packages are preserved. Always test
an exported CUE before sharing an edited game or relying on its progression.
