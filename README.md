# Spyro 1 Editor

Spyro 1 Editor is a native Mac and Windows editor for inspecting and testing
level/object edits in the original PlayStation release of Spyro the Dragon.

The editor is code and metadata only. It does not include a game image, BIOS,
patched disc, emulator save, RAM/VRAM dump, extracted WAD payload, texture dump,
or copied game asset. Users must provide their own legally obtained disc image.

## Releases

GitHub Releases are the intended place for public Mac and Windows builds.

The release packages are self-contained:

- `SpyroEditor-0.1.0-beta.15-osx-arm64.zip`
- `SpyroEditor-0.1.0-beta.15-win-x64.zip`

Unzip the package, launch Spyro Editor, choose your own BIN/CUE, then use
`Create BIN` to write local patched test output. Generated output stays on the
user's machine and should not be uploaded to GitHub.

## Current Editor

The active app lives in `src/Spyro.Editor.App`.

Current release features include:

- Map View and Fly 3D level inspection.
- Level terrain and object maps rebuilt from the user's selected BIN/CUE.
- Moving, cloning, removing, and editing supported gems, chests, enemies, keys,
  scenery, and other decoded mobys.
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
- Same-level donor metadata is preserved for enemy/chest add and paste workflows,
  and `Create BIN` routes those copies through matching same-level source slots
  first, then allows the proven ordinary Flame/Charge Chest, native Life Chest,
  and Large Gnorc families to continue as true-adds when reuse slots run out.
  In all 22 levels containing native Life Chests, the original donor is
  preserved and every appended copy receives an independent private runtime
  block.
- The Add Object list and copy/paste status text show how many safe extra export
  slots remain for same-level enemy/chest copies, and identify proven families
  that can true-add after those slots are consumed.
- When a placement consumes the last known-safe slot for an unproven object
  family, the editor warns that later objects of that kind may not work as
  intended in-game and that broader support is planned for a later update.
- Older copied object edits without donor metadata can still be matched by their
  same-level source identity bytes when a matching reusable slot is available.
- The release Add Object list only offers simple safe records and donors that
  already exist in the selected level; cross-level object imports are held for
  post-release research.
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
- Terrain and building texture editing through the shared native texture-page
  path. A selected face can borrow actual art from another level into a local
  texture slot, while shared texture tools can import user PNG art. The editor
  previews image imports from their pixels and `Create BIN` writes both normal
  and close-detail descriptors when available.
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
  palettes, guarded native object lighting with zero object-row reroutes, cross-level terrain texture
  art, custom PNG writeback,
  all-level placement-sector coverage, and release packaging safety.

Some object classes are still experimental. `Create BIN` tries same-level slot
reuse first for enemy/chest add, copy, and paste workflows. Ordinary
Flame/Charge Chests, same-level Life Chests in all 22 levels containing native
donors, and Large Gnorcs can true-add after those slots run out. Cross-level
Life Chest imports remain guarded because levels without that native family may
not contain its model and behavior package.
Small/Regular Gnorc-style families remain guarded beyond reusable slots because
live testing showed they can spawn inactive. Use `Edit Object` > `Change To`
when you want to choose the exact source slot that gets consumed. If something
appears, disappears, clips, soft-locks, or changes behavior in-game, please file
an issue with steps to reproduce.

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
