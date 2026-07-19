# Spyro Editor Numbered Beta Release Checklist

Use this checklist only when publishing a deliberate public update such as
**Spyro Editor Beta V2** or **Spyro Editor Beta V3**. Ordinary fixes and internal
`0.1.0-beta.N` builds are not public releases and must not create update prompts.

## Choose the public release

1. Set `BetaReleaseNumber` in
   `src/Spyro.Editor.App/Spyro.Editor.App.csproj` to the next public number.
2. Keep `Version` as the independent internal build identity. It may advance for
   many fixes without changing `BetaReleaseNumber`.
3. Update `CHANGELOG.md` for that numbered release. This exact file becomes both
   the packaged changelog and the GitHub Release body.
4. Update the public beta name in the guide and known-limitations documents.

For public beta `N`, all names are exact and case-sensitive:

- Git tag: `beta-vN`
- GitHub Release title: `Spyro Editor Beta VN`
- macOS asset: `SpyroEditor-Beta-VN-osx-arm64.zip`
- Windows asset: `SpyroEditor-Beta-VN-win-x64.zip`

The updater ignores releases that do not match all four identities.

## Build and verify

From the repository root on macOS:

```sh
tools/Run-SpyroEditorOfflineQa.sh
tools/Build-SpyroEditorRelease.sh
```

The release build runs `tools/Verify-SpyroEditorRelease.sh` automatically. Do
not publish unless it passes. The verifier checks the public beta identity,
internal version, archive root, release manifest, packaged changelog, embedded
app identity, Mac/Windows runtime parity, executable formats, launchers,
signature round-trip, and forbidden content.

Also confirm the focused project/update checks remain green:

```sh
dotnet run --project src/Spyro.Editor.PersistenceSmoke/Spyro.Editor.PersistenceSmoke.csproj -c Release
dotnet run --project src/Spyro.Editor.UpdateSmoke/Spyro.Editor.UpdateSmoke.csproj -c Release
dotnet run --project src/Spyro.Editor.UiSmoke/Spyro.Editor.UiSmoke.csproj -c Release -- --update-only
```

The finished archives are under `dist/release/`. Keep the SHA-256 values printed
by the verifier with the release notes.

## Project-safety gate

Before publishing, confirm all of the following:

- Release launchers set `SPYRO_EDITOR_INSTALL_ROOT`, not
  `SPYRO_EDITOR_WORKSPACE`, to the extracted application folder.
- Projects and generated output default to `Documents/Spyro Editor/Projects`.
- Settings, backups, and downloaded updates remain in the operating system's
  application-data folder.
- The Beta V1 import copies old edits and custom assets; it never moves or
  deletes the old folder and never overwrites an existing project file.
- Downloading an update first creates and verifies a project snapshot.
- No project, cache, generated BIN/CUE, disc image, BIOS, save, RAM dump, or
  machine-local path is present in either release archive.

## Publish to GitHub

1. Create a **draft prerelease** in `monty19933-hub/Spyro1Editor` using the exact
   tag and title above.
2. Paste `CHANGELOG.md` unchanged into the release body. Do not prepend or append
   different release text; the updater compares it to the packaged file.
3. Attach exactly one matching macOS archive and one matching Windows archive.
4. Confirm GitHub shows a SHA-256 digest for both uploaded assets.
5. Download both draft assets once and run the package verifier against them if
   they were re-uploaded or renamed.
6. Publish the prerelease only after the body, asset names, sizes, and digests
   are final.

Publishing—not pushing commits—is what makes the in-app notification appear.
Beta V2 checks no more than once per day in the background; users can force an
immediate check with `More` > `Check for Updates`.

## Manual packaged-app check

- Launch the extracted macOS package normally and the Windows package on Windows.
- Confirm the window title uses `Spyro Editor Beta VN`.
- Open `More` > `Project Data` and confirm the active project is outside the
  extracted application folder.
- Open `More` > `Check for Updates` and confirm the current public beta reports
  as current before the next release is published.
- Open a user-provided BIN/CUE, load a level, save one harmless edit, close and
  reopen the editor, and confirm the edit persists.
- Replace or move the extracted application folder, reopen it, and confirm the
  same external project is still selected.

## Never publish

- `.bin`, `.cue`, `.iso`, `.img`, `.chd`, BIOS, save-state, memory-card, RAM, or
  VRAM files.
- `editor-cache/`, `_local/`, `bin/`, `obj/`, debug symbols, crash dumps, or
  extracted game assets.
- A release whose GitHub body differs from the packaged `CHANGELOG.md`.
- A renamed archive that no longer matches the numbered public beta identity.
- A research/candidate build as a numbered public beta.

Google Drive is not an update authority. Adding it safely requires a separate
signed manifest and a stable direct-download endpoint; the current updater uses
GitHub Releases only.
