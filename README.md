# Spyro 1 Editor

Spyro 1 Editor is a native Mac and Windows editor for inspecting and testing
level/object edits in the original PlayStation release of Spyro the Dragon.

The editor is code and metadata only. It does not include a game image, BIOS,
patched disc, emulator save, RAM/VRAM dump, extracted WAD payload, texture dump,
or copied game asset. Users must provide their own legally obtained disc image.

## Releases

GitHub Releases are the intended place for public Mac and Windows builds.

The release packages are self-contained:

- `SpyroEditor-release-osx-arm64.zip`
- `SpyroEditor-release-win-x64.zip`

Unzip the package, launch Spyro Editor, choose your own BIN/CUE, then use
`Create BIN` to write local patched test output. Generated output stays on the
user's machine and should not be uploaded to GitHub.

## Current Editor

The active app lives in `src/Spyro.Editor.App`.

Current release-candidate features include:

- Map View and Fly 3D level inspection.
- Level terrain and object maps rebuilt from the user's selected BIN/CUE.
- Moving, cloning, removing, and editing supported gems, chests, enemies, keys,
  scenery, and other decoded mobys.
- Same-level slot replacement for enemies and object classes that are not safe
  to add as brand-new source records yet.
- Same-level donor clone adds for testing extra objects that already exist in
  the loaded level.
- Terrain snapping for placed objects, including indoor/enclosed placement.
- Multi-level `Create BIN` output that includes all saved editor changes.
- Object edits auto-save when changing levels so cross-level test builds do not
  lose staged work.
- Local patched BIN/CUE output folder opening after export.
- Focused smoke checks for multi-gem placement, chest/object copy-paste,
  all-level placement-sector coverage, and release packaging safety.

Some object classes are still experimental. For enemies such as Large Gnorc or
Big Armor Gnorc, replace an existing same-level object slot instead of adding a
brand-new one when you need the safest test. Same-level donor clone adds are
available for testing extra copies, but their behavior still needs in-game
validation. If something appears, disappears, clips, soft-locks, or changes
behavior in-game, please file an issue with steps to reproduce.

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
./tools/Build-SpyroEditorRelease.sh SpyroEditor-release
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
