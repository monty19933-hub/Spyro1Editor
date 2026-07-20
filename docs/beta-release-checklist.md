# Spyro Editor Public Beta Release Checklist

Use this checklist only when publishing a deliberate public update such as
**Spyro Editor Beta V3** or **Spyro Editor Beta V3.1**. Ordinary fixes and internal
`0.1.0-beta.N` builds are not public releases and must not create update prompts.

## V2-to-V3 compatibility bridge

Installed Beta V2 clients understand only positive-integer public releases.
Their next update must therefore be the exact bridge below:

- `BetaReleaseNumber`: `3` (legacy assembly/update identity)
- `PublicReleaseVersion`: `3`
- `ReleaseManifestSchemaVersion`: `1`
- `PreviousPublicReleaseVersion`: `2`
- Git tag: `beta-v3`
- GitHub Release title: `Spyro Editor Beta V3`
- Assets: `SpyroEditor-Beta-V3-osx-arm64.zip` and
  `SpyroEditor-Beta-V3-win-x64.zip`
- Manifest: the unchanged schema-1 shape with `publicBeta: 3`

This exact V3 release is what makes the update notification appear in V2. A
release named V2.1 would be ignored by those already-installed clients.

## Incremental releases after V3

Future incremental releases use a dotted canonical public version and schema 2.
For example, V3.1 uses:

- `BetaReleaseNumber`: `3` (retained legacy major integer)
- `PublicReleaseVersion`: `3.1`
- `ReleaseManifestSchemaVersion`: `2`
- `PreviousPublicReleaseVersion`: `3`
- Git tag/title/assets: `beta-v3.1`, `Spyro Editor Beta V3.1`, and matching
  `SpyroEditor-Beta-V3.1-<platform>.zip` files
- Manifest: schema 2, retaining `publicBeta: 3` and adding
  `publicVersion: "3.1"`

Users still on V2 first install the V3 bridge; V3 can then discover V3.1.
Never repoint an old tag or silently replace an already-published asset.

The frozen Beta V2 updater reads only the newest 30 GitHub prereleases and
ignores dotted identities. Keep at least one whole-number schema-1 bridge among
those newest 30 releases. Before the current bridge would fall out of that
window, publish the next deliberate whole-number bridge (for example V4 with
`BetaReleaseNumber: 4`, `PublicReleaseVersion: 4`, and schema 1). Dotted releases
between bridges continue to use schema 2.

Every release assembly carries all three metadata fields:

- `SpyroEditorBetaRelease`: the legacy positive integer
- `SpyroEditorPublicReleaseVersion`: the canonical integer or dotted version
- `SpyroEditorReleaseManifestSchema`: the package manifest schema (`1` or `2`)

For the V3 bridge those values are `3`, `3`, and `1`. For V3.1 they are `3`,
`3.1`, and `2`.

## Choose the public release

1. Set `PublicReleaseVersion`, `ReleaseManifestSchemaVersion`, and
   `PreviousPublicReleaseVersion` in
   `src/Spyro.Editor.App/Spyro.Editor.App.csproj` for the intended release.
2. Keep `BetaReleaseNumber` as the positive-integer legacy major. V3 uses `3`;
   V3.1 retains `3` while its canonical version becomes `3.1`.
3. Keep `Version` as the independent internal build identity. It may advance for
   many fixes without changing `BetaReleaseNumber`.
4. Replace `CHANGELOG.md` with only the delta from the immediately previous
   public release. It must begin with `# Spyro Editor Beta V<version>`, a blank
   line, then `## Changes since Spyro Editor Beta V<previous-version>`.
   This exact file becomes both the packaged changelog and GitHub Release body;
   do not append earlier release history or hand-edit the GitHub body.
5. Update the public beta name in the README, guide, and known-limitations
   documents.

For canonical public version `R`, all names are exact and case-sensitive:

- Git tag: `beta-vR`
- GitHub Release title: `Spyro Editor Beta VR`
- macOS asset: `SpyroEditor-Beta-VR-osx-arm64.zip`
- Windows asset: `SpyroEditor-Beta-VR-win-x64.zip`

The updater ignores releases that do not match all four identities and the
required manifest schema. Whole-number compatibility bridges use schema 1;
dotted incremental releases use schema 2.

## Build and verify

From the repository root on macOS:

```sh
tools/Run-SpyroEditorOfflineQa.sh
SPYRO_EDITOR_NOTARY_KEYCHAIN_PROFILE=<profile-name> \
  tools/Build-SpyroEditorRelease.sh
```

Create the named Keychain profile once with `xcrun notarytool
store-credentials <profile-name>` and answer its prompts without writing the
Apple credential into this repository. A public release defaults to
`SPYRO_EDITOR_MAC_BUILD_MODE=production`, auto-selects the only installed
Developer ID Application identity, and rejects builds when the profile or
identity is unavailable. If more than one Developer ID identity is installed,
set `SPYRO_EDITOR_MAC_SIGN_IDENTITY` to the complete intended identity name.
`SPYRO_EDITOR_MAC_BUILD_MODE=local` is only for non-public research builds.

If Apple's notarization service is unavailable and an immediate tester build is
explicitly required, use the emergency fallback only:

```sh
SPYRO_EDITOR_MAC_BUILD_MODE=signed-only \
  tools/Build-SpyroEditorRelease.sh
```

This keeps the Developer ID signature, exact team, hardened runtime, secure
timestamps, and release-safe JIT entitlement, but deliberately skips submission
and stapling. The build automatically verifies with
`SPYRO_EDITOR_ALLOW_UNNOTARIZED=1` and includes
`MACOS-OPEN-INSTRUCTIONS.txt`. Testers should first try a normal open, then use
System Settings > Privacy & Security > Open Anyway only for the ZIP from the
official release. This is an emergency fallback, not the normal public-release
path; return to `production` and replace it with a notarized asset as soon as
Apple's service is available.

If no Developer ID Application identity is installed and the release owner has
explicitly approved an accept-the-risk build, use the community fallback:

```sh
SPYRO_EDITOR_MAC_BUILD_MODE=community \
  tools/Build-SpyroEditorRelease.sh
```

This mode is intentionally separate from production. It ad-hoc signs every
payload, enables the hardened runtime, retains the release-safe JIT entitlement,
checks the archive before and after ZIP round-trip, and includes prominent
Open Anyway instructions. It cannot provide Apple identity or notarization, so
the GitHub release notes must disclose that limitation and publish SHA-256
digests. Never silently select this mode, and return to production signing when
the Developer ID certificate is available again.

The release build runs `tools/Verify-SpyroEditorRelease.sh` automatically. Do
not publish unless it passes. The verifier checks the canonical public identity,
legacy integer bridge identity, manifest schema, internal version, archive root,
release manifest, packaged changelog, embedded
app identity, Mac/Windows runtime parity, executable formats, the Windows
launcher, forbidden content, and the Mac app's exact Developer ID team,
hardened runtime, secure timestamp, release-safe .NET JIT entitlements, stapled
notarization ticket, Gatekeeper acceptance, and macOS distribution-policy
acceptance before and after the ZIP round-trip. Emergency signed-only verification
still enforces every signature property before and after the ZIP, requires the
opening instructions, and fails unless the app has no stapled ticket. Community
verification instead enforces an explicit ad-hoc identity, hardened runtime,
the same release-safe entitlement, no certificate authority, no stapled ticket,
and the expected Gatekeeper rejection.

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

- The macOS package opens `Spyro Editor.app` directly and contains no external
  `.command` launcher. The Windows launcher sets `SPYRO_EDITOR_INSTALL_ROOT`,
  not `SPYRO_EDITOR_WORKSPACE`, to the extracted application folder.
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
2. Supply `CHANGELOG.md` unchanged as the release body (with `--notes-file
   CHANGELOG.md` when using `gh`). Do not prepend, append, reflow, or hand-edit
   release text; the packaged file is byte-compared to the source and the
   updater compares the GitHub body to that packaged changelog.
3. Attach exactly one matching macOS archive and one matching Windows archive.
4. Confirm GitHub shows a SHA-256 digest for both uploaded assets.
5. Download both draft assets once and run the package verifier against them if
   they were re-uploaded or renamed.
6. Count the releases newer than the most recent whole-number schema-1 bridge.
   If publishing this release would move that bridge outside the newest 30,
   make this release the next whole-number bridge instead of another dotted
   increment.
7. Publish the prerelease only after the body, asset names, sizes, and digests
   are final.

Publishing—not pushing commits—is what makes the in-app notification appear.
Beta V2 finds the exact V3 bridge and checks no more than once per day in the
background; users can force an immediate check with `More` > `Check for
Updates`. After installing V3, the canonical-version updater can discover later
dotted releases such as V3.1.

## Manual packaged-app check

- On a clean Mac user account, unzip the downloaded macOS asset and open
  `Spyro Editor.app` directly. For a normal production build, confirm there is
  no malware/unidentified-developer warning. For an explicitly approved
  signed-only fallback, confirm the warning is the expected unnotarized-app
  warning and that Privacy & Security > Open Anyway launches the signed app.
  Launch the Windows package on Windows through `Launch Spyro Editor.bat`.
- Confirm the window title uses `Spyro Editor Beta VR`, where `R` is the exact
  `PublicReleaseVersion`.
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
- A renamed archive that no longer matches the canonical public identity.
- A research/candidate build as a public beta.

Google Drive is not an update authority. Adding it safely requires a separate
signed manifest and a stable direct-download endpoint; the current updater uses
GitHub Releases only.
