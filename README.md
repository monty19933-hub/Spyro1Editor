# Spyro 1 Editor

Spyro 1 Editor is a native Mac and Windows editor for inspecting and testing
level/object edits in the original PlayStation release of Spyro the Dragon.

The editor is code and metadata only. It does not include a game image, BIOS,
patched disc, emulator save, RAM/VRAM dump, extracted WAD payload, texture dump,
or copied game asset. Users must provide their own legally obtained disc image.

## Releases

GitHub Releases are the intended place for public Mac and Windows builds.

The release packages are self-contained:

- `SpyroEditor-Beta-V3-osx-arm64.zip`
- `SpyroEditor-Beta-V3-win-x64.zip`

The public release name is independent of internal build iterations. Publish
only intentional bundled releases: tag `beta-v3`, use the exact GitHub title
`Spyro Editor Beta V3`, mark it as a prerelease, attach the two exact package
names above, and paste `CHANGELOG.md` unchanged into the GitHub release body.
Ordinary commits and fixes do not create update prompts.

V3 is an intentional compatibility bridge for installed V2 clients. Its tag,
title, asset names, schema-1 manifest, `publicBeta: 3`, and legacy integer
assembly identity retain the exact numbered-beta contract that V2 understands.
Do not publish this build as V2.1: V2's updater cannot parse a dotted release.

After that bridge, incremental public updates use canonical dotted identities:
`beta-v3.1`, `Spyro Editor Beta V3.1`, and matching `Beta-V3.1` assets. Those
packages use manifest schema 2, carry `publicVersion: "3.1"`, and retain legacy
integer beta metadata only for compatibility. Each `CHANGELOG.md` is solely the
delta from the immediately previous public release, not accumulated history.

The frozen V2 updater scans only the newest 30 prereleases and ignores dotted
identities. The release checklist therefore keeps a whole-number schema-1 bridge
inside that window, publishing a later bridge such as V4 before the previous one
ages out. Dotted increments between those bridges continue to use schema 2.

The updater rejects a mismatched tag, title, public beta, platform, archive
manifest, packaged changelog, embedded app identity/internal build, asset name,
or GitHub SHA-256 digest. Google Drive is not an update authority; supporting it
safely would require a separate signed manifest and stable direct-download
endpoint.

Use `docs/beta-release-checklist.md` for the exact build, verification, draft
release, asset, changelog, compatibility, and project-persistence gates.

Unzip the package, open `Spyro Editor.app` directly on macOS (there is no
separate `.command` launcher), or open `Launch Spyro Editor.bat` on Windows.
Choose your own BIN/CUE, then use
`Create BIN` to write local patched test output. Release builds keep the active
project under the user's Documents folder instead of inside the replaceable app
folder. Generated output stays on the user's machine and should not be uploaded
to GitHub.

Normal public macOS builds are notarized and stapled. An explicit
`SPYRO_EDITOR_MAC_BUILD_MODE=signed-only` build is reserved for a notarization
service outage: it remains Developer ID signed, hardened, and securely
timestamped, but includes `MACOS-OPEN-INSTRUCTIONS.txt` because testers may need
System Settings > Privacy & Security > Open Anyway. This emergency fallback must
be replaced by the normal notarized asset as soon as possible.
When the release owner explicitly approves an accept-the-risk package because
no Developer ID identity is installed, `SPYRO_EDITOR_MAC_BUILD_MODE=community`
produces a hardened ad-hoc-signed ZIP with the same opening instructions and
published SHA-256 digests. Beta V3 uses the `signed-only` path: it is Developer
ID signed, hardened, and securely timestamped, but not notarized.

## Current Editor

The active app lives in `src/Spyro.Editor.App`.

Current release features include:

- Protected external project storage keeps saved object, terrain, sky, music,
  text, and custom-art plans plus generated output outside the installed app.
  `More` > `Project Data` opens the project or copy-imports an older portable
  beta without moving/deleting it or overwriting conflicts. Rebuildable caches
  and stale WAD analysis are regenerated.
- A prominent, once-per-release in-app notification and `More` > `Check for
  Updates` select only the next canonical Mac/Windows beta release. The update
  window shows the GitHub changelog, preserves a copy beside the verified
  download, and creates a pre-update project snapshot. In-place replacement
  remains disabled so installation is an explicit user action. Normal production
  macOS packages are Developer ID signed, hardened, notarized by Apple, and
  stapled before upload; an explicitly approved community fallback is always
  labeled as ad-hoc signed and includes Open Anyway instructions.

- **Edit Map** and **Game Camera** level inspection.
- Level terrain and object maps rebuilt from the user's selected BIN/CUE.
- The shipping editor retains every captured editable source-terrain face; no
  normal view removes geometry from the project. **Edit Map** is the exhaustive
  top-down locator. It keeps every captured face visible, material-rendered, and
  selectable, including source groups the game normally submits from different
  camera contexts.
  **Game Camera** is the default after loading a level. When the source payload
  resolves, the camera's collision triangle retains the native environment group
  as provenance and coarse painter-order context while every source sector remains
  eligible for decoded HP/LP distance and frustum rules. Visible HP faces keep
  their native materials and source depth cue; distant LP faces keep source
  Gouraud colors. A validated portable cache stores the source retail entry XYZ/yaw
  for all 35 levels. Game Camera derives its initial third-person position from
  that pose and the retail spherical camera preset; a native collision-group
  discontinuity may shorten the behind-distance. After the cache passes complete
  readback validation, viewport browsing no longer needs the original BIN/CUE to
  be present. The source image is still required to build or rebuild that cache
  and for disc patch/export workflows. These presentation rules never remove
  terrain from the project or `Create BIN`. Exact fixed-point GTE projection and
  clipping, HQ subdivision,
  full PlayStation ordering-table traversal, runtime texture animation, and the
  native sky/background are still research gaps, so neither view is a pixel-exact
  gameplay-frame claim.
- Moving, cloning, removing, and editing supported gems, chests, enemies, keys,
  scenery, and other decoded mobys.
- Red, green, blue, yellow, and purple loose gems use one consistent red-gem
  icon silhouette in Edit Map and Game Camera; the four alternate colors are exact
  color-only variants of the unchanged red artwork.
- Built-in diagnostics record editor actions, selected source/level state, edit
  counts, export plans, and unhandled exceptions. The Diagnostics window can
  copy the current report or last crash and create a metadata-only support ZIP;
  every successful `Create BIN` also writes a report and support ZIP beside the
  generated CUE/BIN without including copyrighted game data.
- A native Moby `Build Safety` inspector shows static rows, projected dynamic
  Moby/props capacity, persistent-index headroom, true appends, slot reuse, and
  skipped edits for every saved object level. `Create BIN` runs it automatically,
  requires confirmation for review builds, and blocks component overruns,
  unresolved layouts, shifted-row appends, and more than 256 persistent rows.
  Object-specific issues name the affected Moby and editor T-index; double-clicking
  an issue loads its level, reveals and selects the Moby, and centers it in Edit
  Map or Game Camera so the edit can be corrected immediately.
  Its JSON/Markdown reports contain metadata and source links only.
- Selected Mobys have a live Z-axis slider and an optional per-object terrain-Z
  snap toggle; linked scene companions move by the same vertical delta.
- Moving any of the 79 native dragons carries its pedestal and matched `0x6E`
  scene-link control by the same exact XYZ change. `Create BIN` also moves both
  hidden camera layers: the approach-camera coordinates and every XYZ keyframe
  in the later rescue cinematic. Native camera angles, framing, and timing stay
  unchanged. Same-level dragon copy/paste preserves the two visible editor
  companions and their native offsets; brand-new dragon rescue scenes remain
  beta research.
- Every non-homeworld destination exposes one exact `Fly-in Landing` marker
  decoded from its native level-entry block. Moving that marker changes where
  Spyro finishes the homeworld-to-level fly-in. Its selected direction arrow
  can be dragged, or an exact degree entered, to patch the native approach
  heading; the separate return-home path is deliberately left unchanged.
- Homeworlds expose all 29 native portal locations in the release sidebar. Each
  source-proven set keeps its lettering, companion control, and travel path
  aligned, while `Create BIN` applies the same movement to the blue portal plane,
  the private two-node Spyro transition route, the real type-6 walk-in collision
  surface, and its rebuilt collision lookup.
  Adding portals and moving their decorative stone arch/scenery remain post-beta work.
- Yaw/facing rotation for supported gems, chests, enemies, scenery, and pasted
  objects, with a compass preview and draggable selected-object facing arrow;
  `Create BIN` writes the native source rotation matrix plus the compatibility
  yaw byte.
- Same-level slot replacement for enemies and object classes that are not safe
  to add as brand-new source records yet.
- A searchable `Replace` catalogue for existing ordinary chest and self-contained
  enemy slots. It is built from the source-backed object caches for all 35
  levels. Same-level donors use the proven native slot-reuse path. Cross-level
  entries are separated into resident-class tests, mapped package-backed tests,
  and entries that still need a model/behavior map. Normal `Create BIN` saves
  but atomically skips every unverified cross-level replacement, including its
  identity bytes. `Create Swap Test` writes one disposable BIN/CUE without
  increasing the level's source-object count. Blowhard Green Wizard v2 and the
  exact Magic Crafters T107-to-T27 v2 route are runtime-verified exceptions:
  normal `Create BIN` composes each target's checked properties/route/fixup
  recipe for one existing-slot replacement.
- Green Wizard rollout remains target-profile-gated. The Blowhard v2 editor
  route, Magic Crafters T107-to-T27 v2 editor route, and Toasty v11 standalone
  bundle are runtime-proven. In Magic Crafters, select the original T27 Armored
  Druid, open `Replace` / Swap Catalog, and choose native T107 Green Wizard.
  Normal `Create BIN` and `Create Swap Test` both use the proven in-place v2
  recipe. Other Magic Crafters targets and true Add remain unavailable until
  they receive their own properties-extent and fixup proof.
- Wizard Peak's exact T6-to-T24 route is runtime-proven: select Elder Wizard
  T24, choose native Green Wizard T6 in `Replace` / Swap Catalog, and use
  normal `Create BIN` or `Create Swap Test`. Both T10 candidates remain retired
  because they never attacked, including v2 after importing T6's detached group
  `0xFF`. Runtime-proven v3 uses T24's already-matching group `0xFF`, installs
  T6's private two-point route in T24's existing `0x74`-byte extent, and
  replaces stale fixup `0xCACC` with `0xCAC0` without moving scene components.
  The user accepted visible lightning, the translated route, and one gem; live
  RAM separately confirmed actor/model initialization, attack, movement, death,
  and retirement. See
  `docs/green-wizard-all-level-support.md` for the 35-level matrix.
- A resident-class test reuses an actor class already loaded by the target level.
  A package-backed test imports a checked model/special-data recipe as well as
  the replacement row. Both remain DuckStation tests until visuals, behavior,
  rewards, and nearby-object stability have been confirmed. Entries marked
  `Needs model/behavior map` cannot be staged yet.
- Deferred linked families remain visible in the catalogue with an explanation
  but cannot be selected. This includes Spring, Firework, multi-gem, Life, and
  locked chest families plus enemies with route, helper, special-data, boss,
  thief, dragon, or scripted dependencies.
- Same-level donor metadata is preserved for enemy/chest add and paste workflows,
  and `Create BIN` routes those copies through matching same-level source slots
  first. Large Gnorcs, ordinary Flame/Charge Chests, and Town Square Bulls have
  a conservative one-true-add-per-family release budget after those slots run
  out; later copies stay saved but are skipped because multiple active actor or
  chest appends can still crash in-game. Native Life Chests use a separate
  proven allocation path: in all 22 levels containing them, the original donor
  is preserved and every appended copy receives an independent private runtime
  block.
- The Add Object list and copy/paste status text show how many safe extra export
  slots remain for same-level enemy/chest copies, distinguish unlimited proven
  paths from the one-true-add provisional families, and explain when later rows
  will remain editor-only.
- When a placement consumes the last known-safe slot for an unproven object
  family, the editor warns that later objects of that kind may not work as
  intended in-game and that broader support is planned for a later update.
- Older copied object edits without donor metadata can still be matched by their
  same-level source identity bytes when a matching reusable slot is available.
- The release Add Object list still offers only simple safe records and donors
  that already exist in the selected level. Cross-level catalogue objects are
  swap-only tests for an existing source slot; they cannot be added as extra
  rows and never enter a normal `Create BIN` until separately proven.
- Trigger, camera, reward-link, route, and helper-looking mobys are shown as
  system/control records instead of normal placeable object templates.
- Terrain snapping for placed objects, including indoor/enclosed placement and
  donor-aware ground offsets for copied/same-level chests and enemies.
- Release-visible level-name editing for all 35 catalog levels. Regular level
  names update the gold portal lettering, entering/confronting transition, and
  guidebook entry from the same indexed game string. Homeworld names update the
  homeworld title, balloon transition, and guidebook heading. Saved names are
  included by the normal multi-level `Create BIN` output. Regular levels and
  homeworlds allow up to 19 characters; boss names allow up to 16 because their
  `CONFRONTING` transition uses more of the game's text buffer. Longer names are
  stored through a guarded shared-pool repack, with an explicit capacity error
  instead of an unsafe export if all saved names cannot fit.
- Per-level music selection for all 35 levels from 46 built-in tracks on the
  user's own disc, including the title theme and hidden late-play alternates.
  Saved choices are included by multi-level `Create BIN` and keep looping when
  the native 8-12 minute alternate-music timer fires.
- Native sky editing for all 35 levels. Recolor keeps the level's existing sky
  geometry, including PNG-sampled custom color gradients. Same-disc swaps that
  fit use the original fixed slot; larger skies use guarded WAD expansion and
  executable relocation within the supported original USA disc's verified ISO
  gap. All 1,190 same-disc target/donor pairs pass structural write/readback.
  Advanced `.sky` imports must parse as a Spyro 1 native sky block and stay
  within the editor's import and disc-growth limits. Playable-level skies and
  their linked homeworld portal copies are patched together. Geometry swaps and
  imports automatically bypass stale target-level sky-occlusion lists so every
  donor part reaches the game's normal per-sector renderer culling pass.
  `Original Sky Preset` adds Stormy Spring, Blazing Desert, and Aurora Dream.
  These release-safe recipes combine geometry read from the user's own disc at
  runtime with original editor-authored palettes and preview artwork; no donor
  sky blocks are bundled. For a same-disc swap, `Match Level Terrain Palette`
  can also grade the destination's own
  near/far landscape lighting and only the texture palettes referenced by its
  landscape scene. Near and far terrain receive one shared affine
  color mapping, which preserves the game's vertex-color interpolation and
  low/high-detail averages while smoothing aggressive night grades. This keeps
  terrain-sector LOD changes from introducing rectangular hue or brightness
  seams. When a donor requires a drastic hue or saturation change, automatic
  luminance-preserving harmonization carries that donor balance through every
  near/far sector and every referenced landscape palette, preventing normal and
  distance texture tiers from retaining conflicting source colors. Both
  rendered RGB lanes in each native high-detail lighting entry are
  graded together; leaving either lane unchanged produces broken close-up
  interpolation even when distant terrain looks correct. A collapsed
  `Advanced Options` panel exposes strength, brightness, saturation, tint, and
  scope controls. The optional `Match scenery, chests, and creatures` switch
  grades the game's existing neutral object-lighting material per level. It
  does not reroute object rows, and it leaves the separate gem and special-
  effect materials unchanged. Levels without an object-lighting match restore
  the native neutral value when entered, preventing a dark grade from leaking
  through later portal transitions.
  `Reset to Normal Level Palette and Skybox` removes the saved sky and terrain
  palette match together.
- `Terrain` > `Choose Texture & Start Painting` presents the disc's decoded
  native terrain/building texture records in a six-column gallery. It loads the
  active level only; another level is read only after the user selects it and
  presses `Load Level Textures`. Double-clicking an available tile enters paint
  mode, while unsupported records stay visible as gray `BLOCKED` cards with an
  exact reason. A same-level choice is a selected-face swap that carries the
  donor's source-verified near/fade corner tints into private or unused color
  slots without recoloring neighboring faces. Save/reload and Undo preserve or
  remove that complete recipe rather than retaining only a texture number. Fast
  cross-level paint is also face-local, but only when the selected destination
  owns a unique safe texture record; shared targets are refused without
  mutation. The existing explicit whole-record workflow remains on the separate
  `Advanced / Shared Replacement` terrain-panel button, along with its custom
  color/import/paste controls. That action replaces the selected target texture
  record for every face that shares its texture ID. Apply is enabled only after
  the target's runtime-persistence,
  complete-record ownership, in-place-or-private-relocation storage, donor
  pixels/palettes, and collision-property batch all pass source-bound checks.
  The donor's exact native surface signature is copied to every matched target
  collision triangle; when the destination lacks that signature, the exporter
  appends the complete native descriptor and relocates the collision suffix
  only when the layout proof succeeds. `Create BIN` repeats these proofs, writes
  the swap and property edits atomically, and verifies the final BIN readback.
  Records without an exact property binding and animation/scroll-controlled
  targets stay visible with a blocker instead of producing a partial swap.
  Arbitrary custom PNG texture writeback remains disabled because the obsolete
  fixed-layout writer does not describe the game's packed native records.
- Multi-level `Create BIN` output that includes all saved editor changes,
  including sky-linked environment grades.
- Object edits auto-save when changing levels so multi-level test builds do not
  lose staged work.
- Local patched BIN/CUE output folder opening after export.
- Stale test BIN/CUE cleanup when saved edits are not export-ready, so an older
  unsafe output is not accidentally loaded in an emulator.
- Focused smoke checks for all 35 indexed level-name slots, multi-gem placement,
  chest/object copy-paste, all 79 dragon/pedestal/rescue-camera scenes,
  all-level sky blocks, sky-linked near/far environment grades, landscape
  palettes, guarded native object lighting with zero object-row reroutes, the
  all-level native terrain-texture catalog, ownership/runtime/surface-layout
  proofs, exact final-BIN readback, and the guard that keeps unproven native rows
  and custom PNG writeback blocked, the all-level chest/enemy swap catalogue and
  its normal-export guard, all-level placement-sector coverage, and release
  packaging safety.
- Focused native Moby safety smoke resolves all 35 mapped component layouts,
  checks allocator-rounded capacity loss, and enforces the 256-row boundary.

Some object classes are still experimental. `Create BIN` tries same-level slot
reuse first for enemy/chest add, copy, and paste workflows. Large Gnorcs,
ordinary Flame/Charge Chests, and Town Square Bulls can export one validated
true-add per family after those slots run out; excess copies stay saved but are
skipped from a normal BIN. Same-level Life Chests in all 22 levels containing
native donors can continue through their independent private-runtime path.
Cross-level Life Chest imports remain guarded because levels without that native
family may not contain its model and behavior package.
Small/Regular Gnorc-style families remain guarded beyond reusable slots because
  live testing showed they can spawn inactive. Use `Edit` > `Replace object`
when you want to choose the exact source slot that gets consumed. If something
appears, disappears, clips, soft-locks, or changes behavior in-game, please file
an issue with steps to reproduce. `Replace` can stage cross-level ordinary
chest and self-contained-enemy replacements for that testing, but those builds
must be made with `Create Swap Test` and are not yet release-safe object imports,
except for the runtime-verified Blowhard v2 and exact Magic Crafters
T107-to-T27 v2 Green Wizard routes.
The Toasty v11 standalone Green Wizard bundle is also runtime-proven, but its
editor route remains profile-gated. Magic Crafters T107 -> T27 is supported
through `Replace` / Swap Catalog by both normal `Create BIN` and `Create Swap
Test`; other destination slots and true Add remain guarded.

## Reporting Issues

Use GitHub Issues for tester reports.

Helpful reports include:

- Operating system and release zip name.
- Level name.
- The action taken in the editor.
- What happened in the editor.
- What happened in-game.
- Screenshots or short clips when useful.

Do not attach game images, BIOS files, patched BIN/CUE output, emulator saves,
save states, RAM/VRAM dumps, extracted game files, or texture dumps. Screenshots,
short clips, logs, and editor-generated JSON reports are fine when they do not
contain copied disc data.

## Build From Source

Requirements:

- .NET 10 SDK.
- macOS is currently used for the two-platform release script.
- An Apple Developer ID Application identity for team `694865MF93` and the
  release JIT entitlements are required for normal production Mac packages.
  Production also requires configured Apple notarization credentials and rejects
  unstapled or Gatekeeper-rejected output. The explicit emergency `signed-only`
  mode requires Gatekeeper's exact `Unnotarized Developer ID` result. The
  separately approved `community` mode is available when no Developer ID
  identity exists; it verifies a hardened ad-hoc signature and includes Open
  Anyway instructions rather than claiming Apple identity or notarization.

Build release zips:

```bash
./tools/Build-SpyroEditorRelease.sh
```

The script writes packages under `dist/release/`. That folder is ignored by git.

Run the app directly:

```bash
dotnet run --project src/Spyro.Editor.App/Spyro.Editor.App.csproj
```

Run the smoke suite:

```bash
dotnet run --project src/Spyro.Editor.Smoke/Spyro.Editor.Smoke.csproj
```

## Repository Layout

- `src/Spyro.Editor.App` - Avalonia desktop editor.
- `src/Spyro.Editor.Core` - disc parsing, cache building, scene models, editing,
  and exporters.
- `src/Spyro.Editor.CacheTool` - local cache builder.
- `src/Spyro.Editor.Smoke` - smoke and packaging safety checks.
- `docs/release-user-guide.md` - release package user guide.
- `docs/known-limitations.md` - current tester limitations.
- `tools/Build-SpyroEditorRelease.sh` - Mac and Windows release zip builder.
- `spyro-level-catalog.json`, `spyro-object-templates.json`, and the small
  level override JSON files - safe editor metadata.

## Copyright Safety

This repository intentionally excludes copyrighted game data and generated
local test output. The `.gitignore` blocks common disc image, emulator capture,
RAM dump, texture dump, patched output, and generated cache patterns. Release
zips should also be scanned before upload.

If a file was generated from a user's disc, captured from an emulator, or copied
from the game, it does not belong in this repository.

The app icon and in-app title artwork under `src/Spyro.Editor.App/Assets/Brand`
are original generated editor branding assets. They are not copied from the game
and do not include official character art, logos, screenshots, disc data, or
extracted assets.
