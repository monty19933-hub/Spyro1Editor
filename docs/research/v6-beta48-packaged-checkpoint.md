# V6 R11 / beta48 packaged checkpoint

September 28, 2026. Clean package source: `79f6ec5`. This is a separate private
preview, not an in-place replacement of R06/R07/R08/R09/R10 or a finished V6.

Exact directory:
`/Users/john/Documents/Codex/2026-07-13/i-needed-to-make-a-new/spyro1editor-v3-release/_local/v6-private-preview-20260928-r11.H9TyRW`.
Use `Open V6 R11 Preview.command`, not the app bundle directly, to retain the
independent copied test project, settings and diagnostics. Read its
`PRIVATE-PREVIEW.md` and `PACKAGED-UI-SESSION.md` before testing. The launcher
checks build48 and the test folders; it does not build, reset, update or download.

## Verification

- Default Release regression at b8634c8: **289 results, zero failures**, including
  26 picker-automation checks and the 55-check flight UI suite. Report:
  `/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-8v63b11t/summary.json`.
  A copy is preserved as `regression-summary.json` in the R11 directory.
- The first package attempt at c648167 was rejected for stale beta47 guide
  identity. The failed guide is retained locally. After guide/changelog fixes,
  the build at79f6ec5 and both Mac ARM64/Windows x64 package verifiers passed.
  No app was launched from the failed attempt. No Core/export logic changed in
  the picker fix or following packaging changes.
- Actual packaged Mac app: AX SetValue("Toasty") on the main picker no longer
  throws/crashes; Stone Hill remains selected, as the read-only contract requires.
- Ordinary keyboard selection loads Toasty (2966 terrain faces,60 Mobys).
  Level -> Fly Anywhere shows `Toasty — saved: Off`, checkbox OFF, Save Flight
  and Revert enabled. The saved choice was copied from R10's normal Save OFF;
  R11 made no project edits, saves or exports.
- Native preview quit normally with exit0; PID51606 absent afterwards. No
  background preview or emulator session is needed for this checkpoint.

The AX popup-item click route selected the wrong level during automation; using
the focused dropdown's ordinary Home/four Down/Return sequence selected Toasty.
That observation is an automation limitation, not proof of an application mouse
selection defect. Broad native-popup/accessibility certification is not claimed.

## Exact artifacts

| Artifact | SHA256 |
| --- | --- |
| Mac ZIP,82824615 bytes | 3145e3f079ebbc75e197b6a49c9d0e346ababf6f04622affe80609462ff43b1f |
| Windows ZIP,85181372 bytes | 03ea72dbdf8b62a736bee6e3b58f6c0ffb5533f9079a2c8ba0a66afdc215a27c |
| Packaged App DLL | 049fd3c39a693edcaad5739d83988c59453348e2e9fecb8fb5adb0b7056bbfae |
| Packaged Core DLL | aea716a08f835f0618b4085ec99e917351f173f4edd033ce7216a582e9a053a2 |
| Unchanged canonical fresh document | 0dade817ab4f9220e5aa3a74e1631e4689bad30ee12e91a943ceb0f6343e8acb |

Mac signing is private/ad-hoc, not Apple signing/notarization. Windows runtime
is untested. Project output files copied from R10 are not fresh R11 exports.

## What is testable, and what is still unfinished

The full known-Moby browsers (368 classes/6334 observations), guarded existing
placement, fresh four-tile editing/undo/save/reopen/exact-profile export, and
Toasty-only flight project choice are available. R09 supplies separate native
GUI/fresh-export readback evidence; R10 supplies normal ON/OFF Create BIN and
saved-ON persistence evidence. R11 adds the native crash fix and saved-OFF
visual check. A visible catalogue entry is not unrestricted portable placement.

The preserved R10 OFF BIN equals the original USA source byte-for-byte. Its ON
CUE booted in DuckStation with full firmware, loaded Stone Hill from the copied
slot0 and returned to Artisans with movement; Toasty was not reached. Flight
ON/OFF gameplay and native save/cold reload remain unverified. Normal emulator
settings/game-settings/card were hash-exact restored after that closed session.
These three hashes were rechecked after closing R11, along with both original
ROMs and R08's App/Core DLLs, project pointer and canonical fresh document; all
match their protected baselines. No editor or DuckStation process remained.

Japanese egg colors and bounded pickup/admission have separate research-runtime
evidence, not complete editor placement or durable custom saving. Editable sign
UI, native egg X/X inventory and required-for-100% consumers remain unfinished,
as do unrestricted all-Moby/all-texture cross-level use and general blank-level
authoring. Do not turn discovery or detached CPU tests into release claims.

Next native-save slice: follow the unimplemented
[IRQ scheduler successor plan](collectible-irq-scheduler-successor-plan.md).
Establish stack-boundary/cleanup/deferred-completion RED tests before composing
the opt-in IRQ controller into the scheduler; retain old goldens and owners.
Do not publish a writer disc or claim native Saved until the composition and
full-firmware save/cold-reload gates actually pass. Resume only after the user's
usage-limit pause has been lifted.
