# Release Hygiene

This editor should be released without copyrighted game content. Ship source code, scripts, documentation, and editor binaries only.

## Do Not Ship

- PS1 disc images or patched disc images: `.bin`, `.cue`, `.iso`, `.img`, `.chd`, `.ecm`, `.m3u`, `.ccd`, `.sub`, `.toc`
- BIOS files, emulator saves, save states, RAM dumps, VRAM dumps, or memory captures
- Extracted WAD payloads, padded texture pages, texture dumps, actor atlases, screenshots from captured game assets, or generated runtime scene overlays
- Local edit/test artifacts such as `*-native-edits.json`, `*-terrain-edits.json`, `*-custom-terrain-textures.json`, `*-runtime-scene-editor-overlay.json`, `*-before-clean.bin`, `_local/custom-textures/`, or `Spyro the Dragon (USA)-*.bin`

## Safe To Ship

- Editor source code and build scripts
- Documentation
- Patch/export scripts that require the user to provide their own legal ROM
- Small hand-authored metadata such as `spyro-level-catalog.json`, provided it contains offsets/level metadata only and no extracted asset payloads
- Compiled editor binaries built from this source

## Before Publishing

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\Test-ReleaseReadiness.ps1
```

To create a release folder:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\New-SpyroEditorRelease.ps1
```

The release script builds the native editor, copies tracked source files plus the compiled editor into `dist\SpyroEditor-release`, and refuses to continue if tracked files match the unsafe content patterns.
