# Spyro editor — what is working now

Updated September 6, 2026. Development is continuing after John's reset.
This is a progress report, not a V6 release. Your preserved editor, original
game and original memory card have not been replaced.

## The biggest new results

**Two imported Wizards now work through a complete bounded gameplay sequence.**
Each cast and produced its own native reward. Defeating the already-collected
nearer Wizard again did not award another ten gems, and leaving/re-entering
through the original portal preserved both collected states. The first reward
changed the HUD from9 to19. The final32 includes three original red gems;
separate native reward-owner evidence identifies the second Wizard's yellow.
See the [two-Wizard report](native-two-wizard-r01-runtime-observations.md).

**The Fresh Level document can now export its saved edits.**
The separate window has floor/spawn/color/music controls, a schematic preview,
undo/redo, protected save/reopen and a guarded Create research candidate action.
An actual editor UI test used the production export worker to create a complete
edited BIN/CUE/receipt, then a separate process verified that output against the
saved document. See [saved-document publication](native-fresh-saved-document-publication.md).

**An edited fresh floor has already been played in DuckStation.**
The separate exact test ROM loaded without Town Square's terrain or placed
scene and supported controlled walking, flame, jump/landing, native fall/
respawn and normal-speed idle. The generic UI-produced image matches that
edited BIN, but its own new output companions were not separately launched.
See the [edited LAB report](native-fresh-edited-r01-runtime-observations.md).

## Is there a new app to open?

There is a separately compiled private test package, but its ordinary Mac
launch is not ready yet. Build, signature and actual PNG compression checks
passed; macOS then denied its launcher access before the editor window opened.
The launcher is being corrected in a new isolated package with a proper native
Mac executable and the same private-project safeguards. No permission bypass
or installed-editor replacement is being used.

The retained R02 package/evidence is here:

`/Users/john/Documents/Codex/2026-07-13/i-needed-to-make-a-new/spyro1editor-v3-release/_local/research/fresh-document-mac-test-r02/`

This is not the finished unrestricted blank-level editor or a V6 release.
Actual launch, Fresh Level interaction and normal close/reopen are separate
acceptance checks before handing over the next app.

## What remains usable in the preserved editor

- Native Moby Library: original variants across all35 retail levels, searchable
  by name/class/source. Visibility does not imply that every destination is
  approved; existing placement templates still govern adding objects.
- Established level/object/terrain editing and guarded Create BIN workflows.
  Cross-level native terrain choices remain limited to verified combinations.
- Existing Blank-Level Lab workflows and saved schemas. Their meaning has not
  been silently changed by the new Fresh Level document implementation.

An older installed app does not acquire new source changes automatically.

## What still stands between this and universal importing

Models alone are not enough. Enemy behavior, effects, sound, collision,
animation, private state, shared state and resource limits must remain correct
together in the destination level.

The current fresh-Wizard resource work matches independently checked texture,
sound, model and particle buffers, but native behavior/body integration is not
finished. One concrete blocker is the shared electric-damage sound: Town's
existing sound mapping is already occupied, and the tempting damage marker is
erased by native processing. Regression tests now preserve these facts so an
apparently convenient fix cannot silently break other gameplay.

Another review caught a real extra collision read in a branch delay slot.
The original read belongs to the next registered model, not the Wizard's own
package. Its target equivalent needs explicit ownership coverage before broader
resource relocation can be trusted.

Arbitrary Moby mixtures, all textures in all levels, native fresh-level enemy
imports, normal portals to new levels, save/reboot coverage and full audio/
capacity testing are still open. The optional Free Flight research case worked
in Toasty; the finished persisted editor checkbox and disabled-output workflow
remain separate work.

## Verification and next milestone

Latest full default Release gate:130 steps, zero failures. Subsequent source-
regression checkpoint eb78f3f passed108 Python tests with four explicit-source
skips,48 runner tests and370 native scalar checks/17 expected refusals. These
counts are engineering checks, not hundreds of gameplay runs.

Actual two-Wizard recording:22 minutes39 seconds,81303 video frames, full strict
decode passed. DuckStation, capture and the read-only observer are closed.
All nine protected hashes passed after exact original settings restoration.

Next: finish the isolated app launch/close-reopen check, complete the source-owned
fresh-Wizard audio/collision contracts, then integrate and playtest that exact
enemy in the fresh scene. Continue expanding tested families/materials through
the editor without calling inventory coverage universal compatibility.

The [current engineering handoff](native-editor-resume-20260906.md) records exact
next steps. The [V6 readiness assessment](v6-readiness-20260906.md) explains why
universal importing is not yet a defensible percentage or release date.
