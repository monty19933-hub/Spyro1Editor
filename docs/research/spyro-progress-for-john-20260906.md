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

There is a separately compiled R03 test app with the corrected native Mac
launcher. Build, signature, actual PNG compression and preservation checks
passed. Its normal launch reached a macOS Documents-folder permission prompt;
the editor window has not yet been verified. If you approve, click Allow for
"Spyro Fresh Document Test r03" on the Mac. I cannot operate that protected
system prompt, and no Full Disk Access or permission bypass is being used.

The new private app is here:

`/Users/john/Documents/Codex/2026-07-13/i-needed-to-make-a-new/spyro1editor-v3-release/_local/research/fresh-document-mac-test-r03/Spyro Fresh Document Test r03.app`

This is not the finished unrestricted blank-level editor or a V6 release.
Actual launch, Fresh Level interaction and normal close/reopen are separate
acceptance checks before handing over the next app.
The app is built from checkpoint6eae8d4; later source-only Wizard scene work is
not silently being rebuilt into it. See [R03 package status](native-fresh-document-mac-r03.md).

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

The extra collision read found during review now passes against the actual
issued resource, including its generated padding. That closes one precise
relocation gap, not the whole collision or lifetime problem.

The new detached fresh-Wizard scene also matches independent native-source
checks. It preserves space for32 dynamic allocations plus the allocator's extra
write; the test catches the earlier layout corrupting another object's private
state. This is source-tested scene construction, not yet a playable fresh-Wizard
ROM. The separate sound-loading stage now also passes source tests, preserving
the exact native transfer bytes. The three repeat-reward models and their
textures are now implemented in a separate resource package, with independent
checks of the actual compiled output. Save handling and complete body integration
remain unfinished. See [fallback resources](native-fresh-wizard-fallback-resources.md),
[scene progress](native-fresh-wizard-p1-scene.md) and
[sound-loading progress](native-fresh-wizard-upload-staging.md).

We also verified that ID65 has its own progress-save row; it need not overwrite
another level's progress. Different fresh projects would still share that row,
so old-save compatibility needs explicit handling. The linked Town/Wizard host
layout now passes independent dispatch/relocation tests and is being implemented
against those reference bytes. Neither result is yet a playable fresh-Wizard ROM.

Arbitrary Moby mixtures, all textures in all levels, native fresh-level enemy
imports, normal portals to new levels, save/reboot coverage and full audio/
capacity testing are still open. The optional Free Flight research case worked
in Toasty; the finished persisted editor checkbox and disabled-output workflow
remain separate work.

## Verification and next milestone

Latest full default Release gate:136 steps, zero failures. The Python suite
passed111 tests with four explicit-source skips;51 runner tests passed. The
independent native scene check passed23 checks/four expected refusals and
sound-loading proof404 checks/ten expected refusals. New actual compiled fallback
resource reads passed34 checks/28 expected refusals; the separate original-source
resource check passed3018/four. Save-row source evidence passed164/20.
These counts are engineering checks, not hundreds of gameplay runs.

Actual two-Wizard recording:22 minutes39 seconds,81303 video frames, full strict
decode passed. DuckStation, capture and the read-only observer are closed.
All nine protected hashes passed after exact original settings restoration.

Next: finish the isolated app launch/close-reopen check after macOS consent,
complete fresh-Wizard reward/audio/resource contracts, then integrate and playtest that exact
enemy in the fresh scene. Continue expanding tested families/materials through
the editor without calling inventory coverage universal compatibility.

The [current engineering handoff](native-editor-resume-20260906.md) records exact
next steps. The [V6 readiness assessment](v6-readiness-20260906.md) explains why
universal importing is not yet a defensible percentage or release date.
