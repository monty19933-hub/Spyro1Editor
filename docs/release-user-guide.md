# Spyro Editor Beta V4 Guide

Internal diagnostic build: `0.1.0-beta.31`.

This beta build uses three everyday workspaces around the unchanged level view:
`Objects`, `Level`, and `Environment`. Internal research probes stay hidden.
Most cross-level chests and self-contained-enemy replacements remain separate
test builds rather than normal object imports. The runtime-verified Artisans
gold Key + Locked Chest V2 pair is the guarded exception described below.

## Start

1. On macOS, unzip the download and open `Spyro Editor.app` directly. On
   Windows, open `Launch Spyro Editor.bat`.
2. Click `Open BIN/CUE`.
3. Choose your own Spyro the Dragon disc image.
4. Wait until the status bar says the editor rebuilt level maps and object caches.
5. Pick a level, change its name or music, or edit supported objects, then click `Create BIN`.

The editor does not include game data, BIOS files, emulator files, RAM dumps,
patched discs, or extracted assets. Patched BIN/CUE output is written to the
active protected project's `output` folder under `Documents/Spyro Editor/Projects`,
not into the replaceable application folder. If saved edits are not export-ready,
the editor removes older matching test BIN/CUE output instead of leaving a stale
disc to load.

The normal Mac release is notarized by Apple. If a ZIP includes
`MACOS-OPEN-INSTRUCTIONS.txt`, read that file before opening the app and confirm
the ZIP came from the official Spyro Editor GitHub release. The file states
whether the package is Developer ID signed but not notarized, or uses the more
limited disclosed ad-hoc community signature. If macOS blocks it, use System
Settings > Privacy & Security > Open Anyway only if you accept that exact risk.
This Beta V4 test package is Developer ID signed, hardened, and securely
timestamped, but it is not notarized; compare its SHA-256 digest before opening
it.

## Projects and Updates

Packaged release builds keep application files and project data separate. Use
`More` > `Project Data` to see or open the exact active project folder. `Open
Workspace` remembers another project folder across launches and synchronizes
only versioned support catalogs into it; user edit files are never overwritten.

For the one-time move from Beta V1, use the first-launch `Import Beta V1
Project` notification or choose `Import a previous portable beta` under Project
Data. The editor copies saved edits, custom sky/texture art, source
selection, and research metadata. The old folder remains untouched. Existing
destination files win, and differing incoming files are preserved under
`_migration/conflicts`. Generated output is optional because it can be several
gigabytes. `editor-cache` and `spyro-wad-analysis.json` are deliberately rebuilt
against the selected disc instead of being trusted across installations.

Beta V4 checks GitHub Releases at most once per day. When a deliberate next
canonical release exists, a visible in-app notification offers `What's New &
Download`; `More` > `Check for Updates` performs an immediate check. The update
window shows the full release changelog in a scrollable panel. A copy of that
changelog is also saved beside the downloaded package.

V3 is the compatibility bridge from the updater already installed in Beta V2.
It is therefore published with the exact legacy identity `beta-v3`, title
`Spyro Editor Beta V3`, matching `Beta-V3` assets, manifest schema 1,
`publicBeta: 3`, and legacy assembly beta integer `3`. It must not be renamed
V2.1 because the V2 updater accepts positive integer release identities only.

After installing V3, later incremental releases use dotted canonical identities
such as `beta-v3.1`, `Spyro Editor Beta V3.1`, matching `Beta-V3.1` assets, and
manifest schema 2 with `publicVersion: "3.1"`. Their changelog contains only
changes since the immediately previous public release. Users still on V2 first
take the V3 bridge and can then receive the dotted update.

Because the frozen V2 updater scans only the newest 30 GitHub prereleases, the
release channel periodically publishes another whole-number schema-1 bridge
before the previous bridge leaves that window. Dotted increments remain schema
2; a later bridge such as V4 advances the legacy integer as well as the
canonical public version.

Downloads are accepted only when the canonical public tag/title, package manifest,
packaged changelog, platform, and SHA-256 digest agree. Before downloading, the
editor writes a verified snapshot of saved edits and imported assets under the
external application-data `Backups` folder. Automatic in-place replacement is
intentionally disabled so installation stays an explicit user action. Normal
production Mac apps are Developer ID signed, hardened, notarized by Apple, and
stapled. A release using the explicitly approved community fallback is instead
ad-hoc signed, clearly disclosed in its changelog, and accompanied by Open Anyway
instructions and SHA-256 digests. Neither installation path touches the external
project.

## Build Safety

Open `Objects` > `Build safety` or `More` > `Build Safety` to inspect all saved
object edits. The panel reports, per level:

- Native and planned static Moby rows.
- Native and projected runtime Moby/props capacity.
- True source-row appends and existing-slot reuse.
- Persistent-index headroom and edits normal export will skip.

Object-specific issues show the exact editor `T#` and Moby name. Double-click
one to close the report, load the correct level, clear any object-list filter,
select the Moby, and center it in the current Edit Map or Game Camera. Findings
that genuinely apply to the whole level remain labeled `Level-wide`.

`Create BIN` runs this check automatically when object edits are present.
`Stable` continues normally. `Review` opens the findings and requires `Create
BIN Anyway`. `Blocked` returns to the editor without writing the unsafe build.
The report does not claim that a fitting row has complete enemy/chest behavior;
linked props, routes, controllers, rewards, and class initialization still need
family-specific proof.

The output folder includes `Spyro Editor - All Saved Edits.build-safety.json`
and `.md`. These are metadata-only reports and are safe to attach to a GitHub
issue; do not attach the generated BIN/CUE.

## Reporting Issues

Use GitHub Issues for bugs found in release builds. Include the release zip name,
your operating system, the level, the editor action, and what happened in-game.

Use `More` > `Diagnostics` to add a short tester note and copy the
current report. After `Create BIN`, the output folder also contains a matching
`.diagnostics.txt` report and `.diagnostics.zip` support bundle. The bundle
includes the relevant editor-generated edit manifests, export summaries, patch
plans, and session log, but excludes BIN/CUE images and other game data. If the
editor itself closes unexpectedly, reopen it and use `Copy Last Crash`.

Do not upload game images, BIOS files, patched BIN/CUE output, emulator saves,
save states, RAM/VRAM dumps, extracted game files, or texture dumps. Screenshots,
short clips, logs, and editor-generated JSON reports are okay when they do not
contain copied disc data.

## What Is Enabled

- Level browsing from the bundled Spyro level catalog.
- **Edit Map** and **Game Camera** viewing. Every captured editable source-terrain
  face remains in the project. Edit Map is the exhaustive top-down locator and
  **Fit All** frames that complete scene. Every face remains material-rendered
  and selectable, including source groups the game normally submits from
  different camera contexts. Game Camera is the default after a level loads.
  Its collision context retains the native environment group as provenance and
  painter-order context while normal HP/LP distance and frustum rules determine
  the current draw list. **Frame Selection** moves the
  camera to a selected object, while **Return to Entry** restores the level-entry
  camera. These presentation choices never remove terrain from `Create BIN`.
- A complete portable `editor-cache/level-entry-poses.json` stores the exact
  retail entry XYZ/yaw record for all 35 levels, including all six homeworlds,
  with source provenance and a payload integrity hash. Game Camera uses that
  source pose plus the retail spherical third-person preset for its initial
  position; a native collision-group boundary may move the camera inward to keep
  the start coherent. Once the complete cache has passed readback validation,
  the viewport can load these starts without the original BIN/CUE being present.
  The original image is still required to build or rebuild the cache and for
  `Create BIN` or other disc patching.
- Terrain maps rebuilt from the BIN/CUE selected by the user.
- Native four-corner terrain color gradients and source-decoded terrain tiles in
  both views. Edit Map uses the native normal-HQ tile with physical HP color table 2,
  stored in the legacy `NearColors` field, and renders
  native untextured sentinels with their exact four-corner Gouraud colors. Game
  Camera queues decoded HP and LP geometry by source sector flags and distance;
  HP faces use native LQ descriptor/palette rows and add the normal/close HQ
  overlay in the decoded near ranges, while LP faces use their source Gouraud
  colors. The cache applies the game's
  exact native load/default initialization to animation and scrolling destinations and
  preserves normal/close HQ art and the indexed LQ base with all sixteen native
  distance-palette rows. The source-proven
  terrain path also applies Spyro's native 4x4 GPU dither/quantize stage before
  STP-gated ABR blending. This is the initial
  level state, not live playback or the current gameplay frame. HQ PNGs are
  checked against their exact dimensions, byte lengths, and SHA-256 values,
  and incomplete or mixed normal/close/LQ cache sets fail closed. Translucent
  water/lava/ooze keep the editor's material fallback rather than displaying
  falsely opaque native art.
- Game Camera retains the collision-selected native environment group as camera
  provenance while every stored unique sector follows the normal decoded HP/LP
  distance queues and HP depth cue. Every visible close HP face keeps its native
  material, including off-group terrain; only genuinely distant terrain uses
  its LP Gouraud representation. The active retail group remains coarse
  painter-order context rather than a material-suppression rule. During camera input a lower-cost
  interpolation tier avoids flat averaged-color terrain; full interpolation
  returns after input settles. Edit Map remains the complete selectable source
  overview. This is not a pixel-exact gameplay-frame claim: exact fixed-point GTE
  projection and clipping, HQ subdivision, complete PlayStation ordering-table
  traversal and non-terrain interleaving, runtime texture animation, and native
  sky/background rendering remain research gaps.
- In Game Camera, `Q` raises and `E` lowers the camera in a fine 64-unit step. This is
  one quarter of the unchanged W/A/S/D movement step for easier positioning in
  indoor spaces.
- Moby/object placement rebuilt from the BIN/CUE selected by the user where the
  level has decoded source tables.
- Terrain-snapped object placement that preserves same-level donor ground
  offsets for copied chests, enemies, and scenery.
- A selected-object `Z axis` slider for direct vertical placement. Moving the
  slider switches that Moby to manual Z. `Snap to terrain Z` is enabled by
  default for ordinary objects; unchecking it keeps the current Z while the
  object is moved horizontally. Checking it again snaps the object and its
  linked scene companions to the nearest surface at the current X/Y.
- Perspective-correct placement in both Edit Map and Game Camera, so object markers
  stay anchored to the terrain point that was clicked while the view moves.
- `Lower Layer` and `Upper Layer` object actions for stepping a selected object
  between overlapping floors at the same X/Y, including enclosed rooms where a
  roof would otherwise be the easiest surface to hit.
- Known object labels and relationships from the bundled metadata.
- Editing all 35 level names from the release-visible `Level Name` panel. For a
  regular level, the saved name is used by its gold portal lettering, entering
  or confronting transition, and guidebook entry. Homeworld names are used by
  the homeworld title, balloon transition, and guidebook heading. Regular
  levels and homeworlds accept up to 19 characters; boss levels accept up to
  16. Longer names share a bounded native string pool, and `Create BIN` reports
  how many characters must be shortened if a large saved batch cannot fit.
- Choosing music for any level from the 46 built-in tracks present on the
  user's disc. A saved choice keeps looping instead of switching back to that
  level's native late-play alternates after 8-12 minutes.
- Editing the native sky in every level from the release-visible `Skybox`
  panel. `Recolor` preserves native geometry and accepts Night, Dusk, manual
  colors, or colors sampled from a user PNG. `Swap From Level` lists skies from
  the user's own disc. Fitting donors stay in place; larger donors use guarded
  WAD expansion and relocate the executable inside the supported original USA
  disc's verified free ISO region. `Import Native .sky` accepts only
  structurally valid Spyro 1 sky blocks within the import and disc-growth
  limits. The playable sky and linked homeworld portal copy are always patched
  together. Geometry swaps and native imports automatically bypass the target
  level's stale sky-occlusion lists so all donor parts can render. `Original
  Sky Preset` includes Stormy Spring, Blazing Desert, and Aurora Dream. Each
  uses geometry read from the user's selected disc plus original palette and
  preview artwork shipped by the editor; no extracted sky block is included in
  the app. For `Swap From Level`, the primary `Match Level Terrain Palette`
  button also matches the
  destination's own near/far landscape lighting and referenced landscape
  texture palettes to the donor. Near and far terrain use one shared affine
  color transform, preserving native vertex blending and low/high-detail color
  averages while smoothing an aggressive dark match. This prevents a terrain
  sector from changing hue or brightness when its LOD changes. Drastic warm,
  cool, or saturation shifts also harmonize all near/far sectors and referenced
  landscape palette tiers toward the donor while preserving local brightness.
  The exporter
  transforms both RGB lighting lanes in every validated high-detail entry while
  preserving their native command bytes. It changes color only, not terrain
  shape or collision. `Advanced Options` stays collapsed unless the user wants
  to adjust strength, brightness, saturation, tint, or terrain patch scope.
  The optional `Match scenery, chests, and creatures` switch uses the game's
  existing neutral object-lighting material to bring ordinary scenery, chests,
  creatures, and dragons into the same environment grade. It never reroutes
  object rows to a reserved material slot, and gem/special-effect materials are
  left unchanged. Entering a level without an object-lighting match restores the
  native neutral value, so the grade cannot leak through a later portal.
  Very large hue changes are still beta. Dark Hollow's close and distant tree
  LODs are graded together; other level-specific scenery LODs may still need
  runtime tracing. Spyro's player palettes are excluded.
  `Reset to Normal Level Palette and Skybox` removes both parts of the saved
  environment edit.
- Browsing decoded native terrain/building textures through `Terrain` >
  `Choose Texture & Start Painting`. The six-column gallery initially loads
  only the active level; another level is read only after it is selected and
  `Load Level Textures` is pressed. Double-clicking an available tile enters
  paint mode. Blocked records remain visible as gray `BLOCKED` cards with their
  exact reason. A same-level choice changes only the selected face and copies
  the donor's exact four-corner near/fade tint pairs into face-private or
  otherwise unused native color slots, so nearby terrain keeps its original
  lighting. The editor re-reads that donor provenance from the selected BIN
  during `Create BIN` and blocks stale, topology-mismatched, or capacity-limited
  choices before saving.
  Fast cross-level paint changes one face only when that destination owns a
  unique safe texture record; a shared target is refused without changing the
  level. The existing whole-record path remains on the separate `Advanced /
  Shared Replacement` terrain-panel button, including the custom
  color/import/paste controls. That explicit action replaces the selected shared
  target texture ID on every face that uses it and shows the complete count of
  affected faces before Apply. The editor copies the donor variant's exact native
  collision-property signature to the target batch, including damaging
  water/lava/ooze behavior.
  If the destination lacks that signature, `Create BIN` may append the donor's
  complete native surface descriptor and relocate the following collision data,
  but only after capacity, pointer, and reparse proofs pass. Native texture art
  uses either proven target-owned in-place storage or byte-private relocation.
  The catalog also identifies exactly 19 static native records that no retail
  terrain face, animation source, or runtime controller references. These rows
  use a clearly labeled art-only mode: their record art replaces the shared
  target record, while the target texture ID, face material/semitransparency
  bits, descriptor ABR/alpha controls, near/fade tint, editor material label,
  and collision behavior remain unchanged. Their saved provenance is the donor
  level/WAD/texture record—not a fabricated terrain-face key. The 22 face-less
  animation sources and 4
  controlled face-less destinations remain blocked.
  Rows with unmapped/mixed properties and animation/scroll-controlled targets
  remain visible with their specific blocker. Apply and `Create BIN` are atomic:
  ownership, runtime persistence, complete pixels/palettes, property coverage,
  patch boundaries, and final-BIN readback must all pass or no swap is saved or
  promoted.
- Previewing RGB, RGBA, grayscale, or indexed PNG terrain art within the
  editor's image-size limits. Custom PNG texture manifests are staging/research
  data only in Beta V4; `Create BIN` rejects them rather than using the obsolete
  fixed-layout normal/close-detail writer.
- `Objects` > `Special Chest Support` shows the exact checked status of Key +
  Locked, Life, Armored/Strong, Firework, 3x Flame/Multi-hit, and Spring Chest
  families for the loaded level. Normal Add/Create BIN exposes only a
  runtime-proven destination profile. The Artisans gold Key + Locked Chest pair
  is the currently proven imported bundle; candidate profiles stay disposable
  test-only and incomplete closures stay blocked. All five flight levels are
  explicitly unsupported.
- Imported special-chest rows are one atomic editor group. Moving, removing,
  restoring, undoing, saving, or loading the visible chest carries its required
  key, controller, reward, and hidden companion rows. Build Safety reports and
  targets the visible chest when the group is partial, orphaned, over capacity,
  stale, incompatible, or tied to the wrong disc/profile.
- Moving, removing, cloning, and editing supported objects.
- Selecting any of the 12 verified native Egg Thieves exposes `Edit Run Path`.
  Edit Map and Game Camera show the fixed ordered route as numbered handles and
  a polyline. Drag a handle or enter exact XYZ values, optionally snap it to
  terrain, reset one node or the entire route, and undo the complete route.
  `Move thief and path together` defaults on; disabling it warns that the thief
  will snap back to its unchanged native route in-game. Node insertion,
  deletion, and reordering are intentionally unavailable.
- Moving an existing dragon together with its pedestal and native `0x6E` scene-
  link control. Dragging, XYZ edits, nudges, and undo keep the three-row scene
  aligned, including when terrain snapping changes the final height. `Create
  BIN` also shifts the hidden approach-camera XYZ and every XYZ keyframe in the
  later rescue cinematic while preserving the original shot angles and timing.
- Selecting any of the 79 native dragon rescues also exposes the dashed
  `Spyro runs here` endpoint. X/Y are editable; Z is derived from source terrain
  and is never exported. Moving the dragon carries the endpoint, while moving
  only the endpoint leaves the dragon, pedestal, cameras, and cinematic timing
  unchanged. Copied/new dragons do not receive this control.
- Copying and pasting a same-level dragon together with its linked pedestal and
  scene-link row while preserving their native XYZ offsets. Additional rescue-
  scene data for brand-new dragons remains a beta research path.
- Editing the source-derived `Fly-in Landing` marker in every destination
  level. Drag it or enter exact XYZ values to change where Spyro finishes the
  flight from a homeworld. Drag its selected direction arrow or enter an exact
  heading degree to change the approach direction. This does not change the
  separate return-home path.
- Editing all 29 native homeworld portal locations from the release-visible
  `Portal Location` panel. Selecting and moving a portal keeps its lettering,
  companion control, and travel path together. `Create BIN` also moves the blue
  portal plane, the private two-node route used to place and fly Spyro through
  the transition, its real type-6 walk-in collision surface, and the rebuilt
  collision lookup. Decorative stone arches and adding new portals are not
  included in this beta path.
- Editing object yaw/facing rotation for supported gems, enemies, chests,
  scenery, triggers, and pasted objects, with a compass preview and draggable
  selected-object facing arrow; `Create BIN` writes the native rotation matrix
  plus the compatibility yaw byte.
- Replacing an existing object slot with another object from the same level for
  enemies or other classes that are not safe as brand-new adds yet.
- Opening `Replace` after selecting an original ordinary chest or
  self-contained enemy. Same-level choices are ready slot replacements and are
  included by normal `Create BIN`. Cross-level choices are compatibility-labeled
  existing-slot tests: `Resident-class test` means the target already loads the
  actor class, `Package-backed test` means a checked model/special-data import
  recipe exists, and `Needs model/behavior map` cannot be staged yet. Normal
  `Create BIN` keeps unverified test edits saved but skips their entire identity
  change. `Create Swap Test` produces the disposable BIN/CUE used for
  DuckStation validation. Each Swap Test validation report includes **Fast Level
  Entry** instructions: from any playable level, open Inventory with **Select**,
  enter **R1, R2, L1, L2, R1, L1, R2, L2**, then enter its printed target buttons.
  Wizard Peak uses **Triangle, then Triangle**. This reusable shortcut is guarded
  to the exact supported USA executable and is never added by normal `Create BIN`.
  The runtime-verified Blowhard v2 and exact Magic
  Crafters T107-to-T27 v2 Green Wizard routes are supported by normal `Create BIN`.
- The Blowhard `Green Wizard` resident-class route uses runtime-verified v2.
  It preserves the selected slot's pod, placement, facing, culling sector, and
  gem reward; expands Blowhard's native properties component for a private
  0x40-byte Wizard work block; translates the native T0 one-point route; and
  moves the pod, collision, and pointer-fixup components without changing their
  payloads. The retired v1 candidate must not be reused: it aliased live collision
  workspace and caused repeated cast/death states. V2 passed DuckStation play:
  repeated casts created real lightning, two separate bolts hit normally, the
  Wizard produced one death animation/sound and one gem, remained gone, and the
  surrounding area stayed normal. Normal `Create BIN` now automatically runs
  the dependency-aware v2 writer; `Create Swap Test` remains available as an
  isolated regression build. The route must not be replaced by a generic
  source-record clone. True Add and multiple Wizard allocations remain unavailable.
- The Toasty v11 standalone Green Wizard bundle is runtime-proven: lightning
  damage, Wizard death, gem reward, model textures, and the lightning-trail
  particle all passed DuckStation play. Its editor route remains target-profile
  gated rather than being treated as a generic cross-level replacement.
- Magic Crafters T107 -> T27 is runtime-verified. Select the
  original T27 Armored Druid, open `Replace` / Swap Catalog, choose the native
  T107 Green Wizard donor, and use normal `Create BIN` or `Create Swap Test`.
  Its expanded-properties v1 candidate
  passed casting, attackability, death, sound, and gem checks but its lightning
  never hit Spyro. Runtime-proven v2 reuses T27's existing properties extent
  and removes one stale fixup without resizing or shifting scene components.
  DuckStation confirmed the real lightning hit while the rest of the Wizard and
  nearby level behavior stayed normal. This is still not an all-target or
  true-add route.
- Wizard Peak has an exact runtime-proven route for native Green Wizard T6
  replacing Elder Wizard T24. Select T24, open `Replace` / Swap Catalog, choose
  T6, and use normal `Create BIN` or `Create Swap Test`. The retired T10 v1 and
  v2 candidates both appeared, were killable, and dropped one gem but never
  attacked; v2 still failed after using T6's detached group `0xFF`. Runtime-
  proven v3 uses T24 because its native pod/group already matches `0xFF`, reuses
  its `0x74`-byte private extent, and repairs the checked fixup in place. The
  user accepted visible lightning, the translated route, and one gem; live RAM
  separately confirmed actor/model initialization, attack state, movement,
  death, and retirement. Other targets and true Add remain blocked.
- In Artisans, adding one `Key + Key Chest pair` from Peace Keepers is a normal
  saved and undoable object edit. Place the linked gold Key and Locked Chest,
  then use normal `Create BIN`; the final build composes the checked V2 handler,
  model, private texture/CLUT, reward, and relocation bundle. DuckStation proved
  the correct gold Key and chest textures, native Key-gated opening and sound,
  six spawned gem objects totaling +10 treasure, one-shot cleanup, and persistence.
  The supported budget is exactly one pair in Artisans; a second pair is not a
  supported add. In this beta, keep the pair as the only Artisans object edit
  and do not combine it with an Artisans terrain, environment, or sky edit in
  the same output; Create BIN rejects those unverified structural mixes.
  Ordinary edits in other levels, along with level-name and music edits, may
  remain in the project. The standalone lightweight Key remains available universally.
  Locked Chests are not yet universal: 13 levels already contain native class
  `0x00AE` but still need focused true-add validation, 16 other regular levels
  without a native Locked Chest need per-level behavior/model/texture profiles,
  and all five flight layouts remain blocked for true appends.
- Reviewing deferred catalogue entries without applying them. Linked and
  special families explain what is missing instead of masquerading as safe
  swaps. The catalogue does not add extra source rows.
- Adding or pasting same-level enemy/chest clones by reusing matching same-level
  source slots when available.
- Exporting at most one validated Large Gnorc, ordinary Flame/Charge Chest, or
  Town Square Bull true-add per family after matching reuse slots are consumed.
  Later copies remain saved in the editor but normal `Create BIN` skips them to
  avoid the delayed and random crashes seen with multiple active appends. In all
  22 levels containing native Life Chests, copies preserve the original donor
  and receive independent private runtime blocks. Cross-level Life Chest imports
  remain guarded.
- Showing the remaining safe extra export slots for same-level enemy/chest
  copies in Add Object and copy/paste status text, including whether the family
  has an unlimited proven path, a one-true-add release budget, or remains fully
  guarded after slot reuse.
- Warning immediately after Add Object or copy/paste consumes the last
  known-safe slot for an unproven family. Later objects of that kind may not
  work as intended in-game until a later update expands support.
- Using `Edit` > `Replace object` when you want to choose the exact source
  slot consumed by a same-level enemy/chest clone.
- The Add Object list only offers simple safe records and donors that already
  exist in the selected level. Use `Replace`, not Add, for guarded
  cross-level existing-slot research.
- Inspecting trigger/helper-looking mobys as system/control records. They are
  not treated as normal placeable objects until their linked behavior is proven.
- Preserving the proven linked families: native dragon/pedestal/rescue-camera
  scenes, all 29 native homeworld portal triplets, 14 checked-in Return Home
  helper pairs, same-level chest contents, and both Artisans Treasure Gnorc
  reward-trigger clusters. Other trigger/control rows remain protected instead
  of being linked from proximity alone.
- Creating one local patched BIN/CUE from all saved level-name, music, skybox,
  environment-grade, object, and terrain edits across the editor.

## What Is Hidden

- Internal Spring Chest import research tools.
- Older actor-package and special-chest candidate probes outside the guarded
  chest/self-contained-enemy `Replace` slice.
- Identity batch tooling.
- Smoke-test and live-RAM helper workflows.
- Internal cache/testing utilities.

If a level shows missing map or object data, use `Open BIN/CUE` again and wait
for the build to finish before changing levels.
