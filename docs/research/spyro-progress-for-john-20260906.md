# Spyro editor — what is working now

Updated September 7, 2026. Development is continuing with a safe pause planned
by5% remaining usage.
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

Yes: the separate R03 test app is already running. I verified its actual main
window and opened Fresh Level… without changing the saved document. Its earlier
permission-wait note is now out of date; no protected permission prompt was
operated by me. Build/signature/packaged PNG and pre-startup preservation checks
passed; full edit/save/close/reopen remains a hands-on acceptance check.

The new private app is here:

`/Users/john/Documents/Codex/2026-07-13/i-needed-to-make-a-new/spyro1editor-v3-release/_local/research/fresh-document-mac-test-r03/Spyro Fresh Document Test r03.app`

This is not the finished unrestricted blank-level editor or a V6 release.
Main/Fresh window opening is now verified; interaction and normal close/reopen
remain separate acceptance checks.
The app is built from checkpoint6eae8d4; later source-only Wizard scene work is
not silently being rebuilt into it. See [R03 package status](native-fresh-document-mac-r03.md).

## What remains usable in the preserved editor

- The preserved V5/schema6 research package retains its established level,
  object, terrain, name/music and guarded Create BIN workflows, plus the older
  separate ID65 Lab. Its research launcher is not unrestricted import approval.
- The older local fallback app is a different build. Neither preserved App
  contains the newer Native Moby Library or Fresh document UI types/labels.
  My earlier report incorrectly placed Native Moby Library in this section;
  the actual package audit corrected that distinction.

An older installed app does not acquire new source changes automatically.

## What you can actively test in R03 now

1. In the open Fresh Native Level Document, start with the existing saved
   2048×2048 floor, heading96 and Stone Hill Track02. New starter is disabled
   because that saved document already exists.
2. Change heading to128 or a floor/sky color, then click Apply to preview.
   Try Undo and Redo. The preview is schematic, not a gameplay viewport.
3. Click Save document, then Reopen saved and check your applied values.
   Close/reopen the document to test persistence. Do not expect typed-but-not-
   applied values to be the saved/exported document.
4. Try another music choice. This selects a native track for the research
   output; the schematic does not play an audio preview.
5. For a validation test, put spawnX8192 on the current floor boundary and
   click Apply: it should refuse without replacing the valid preview. Restore
   an interior value such as9216 before continuing.

Close the document to return to Objects → Browse Native Library… for searchable
native inventory. Library visibility is not approval to place every Moby into
every level. The fresh-floor document currently has no placed Mobys or texture
painting, and it does not include the newer source-only Wizard integration.

Create research candidate… is a separate saved-document action that makes a
new BIN/CUE; it is not the main Create BIN button or universal gameplay
approval. Its production worker/readback were tested separately. Use a new
output location, preserve your original ROM and report what actually happens
in DuckStation; R03 GUI publication itself has not yet been exercised.

## What still stands between this and universal importing

Models alone are not enough. Enemy behavior, effects, sound, collision,
animation, private state, shared state and resource limits must remain correct
together in the destination level.

The first fresh-Wizard ROM now loads the Wizard, Sparx and a projectile in LAB.
It also exposed a playability problem: Spyro leaves the small floor almost
immediately and falls after entry/respawn. Defeat/reward testing did not pass;
the exact cause is still being investigated. This research build is not in R03.
See [first fresh-Wizard playtest](native-fresh-wizard-p1-r01-runtime-observations.md).

The current fresh-Wizard resource work matches independently checked texture,
sound, model and particle buffers, and its combined body now has a source-tested
builder. Full native behavior integration is not finished. One concrete blocker
is the shared electric-damage sound: Town's
existing sound mapping is already occupied, and the tempting damage marker is
erased by native processing. Regression tests now preserve these facts so an
apparently convenient fix cannot silently break other gameplay.

The extra collision read found during review now passes against the actual
issued resource, including its generated padding. That closes one precise
relocation gap, not the whole collision or lifetime problem.

The new detached fresh-Wizard scene also matches independent native-source
checks. It preserves space for32 dynamic allocations plus the allocator's extra
write; the test catches the earlier layout corrupting another object's private
state. This scene is now included in the independently verified research ROM;
its gameplay has not yet been accepted. The separate sound-loading stage also
passes source tests, preserving
the exact native transfer bytes. The three repeat-reward models and their
textures are now implemented in a separate resource package, with independent
checks of the actual compiled output. Save handling and playable end-to-end
integration remain unfinished. See [fallback resources](native-fresh-wizard-fallback-resources.md),
[scene progress](native-fresh-wizard-p1-scene.md) and
[sound-loading progress](native-fresh-wizard-upload-staging.md).

We also verified that ID65 has its own progress-save row; it need not overwrite
another level's progress. Different fresh projects would still share that row,
so old-save compatibility needs explicit handling. The linked Town/Wizard host
layout now has a compiled builder matching the independent dispatch/relocation
tests, including a separate test consuming its actual compiled code. The
combined body builder now also matches independently checked loader addresses,
resource ownership and allocation layout. See
[body checkpoint](native-fresh-wizard-detached-body.md). The executable builder
now also passes its tests: it preserves LAB/music and gives the placed Wizard's
level a ten-gem treasure target. The new disc builder now also matches the
independent whole-ROM reference and reverses exactly to the original. A guarded
test-file exporter now passes its source tests and the all-project Release
regression run. Real file publication/recovery tests also passed, and a separate
clean fresh-Wizard ROM has been published and independently verified. It has
now been played in DuckStation with the failed playable-entry result above;
it is not integrated into R03. See
[materializer checkpoint](native-fresh-wizard-research-materializer.md),
[executable checkpoint](native-fresh-wizard-detached-executable.md) and
[disc checkpoint](native-fresh-wizard-detached-disc.md).

Reward testing uncovered two more native dependencies: a ten-gem pickup creates
two number objects, whose expiry can create twenty particles. The real spawn
and selected deletion paths now pass source tests. Capacity and complete
lifetimes still need checking; the original game does not safely refuse every
object allocation when a pool is full. See
[reward dependencies](native-fresh-wizard-number-and-electric-frontier.md).

Arbitrary Moby mixtures, all textures in all levels, native fresh-level enemy
imports, normal portals to new levels, save/reboot coverage and full audio/
capacity testing are still open. The optional Free Flight research case worked
in Toasty; the finished persisted editor checkbox and disabled-output workflow
remain separate work.

## Verification and next milestone

Latest post-publication Release gate:146 selected checks, zero failures; the
preceding all-project build/regression gate passed313 checks. The current Python
suite passed168 tests with seven explicit-mode skips;56 runner tests passed. The
independent native scene check passed23 checks/four expected refusals and
sound-loading proof404 checks/ten expected refusals. New actual compiled fallback
resource reads passed34 checks/28 expected refusals; the separate original-source
resource check passed3018/four. Save-row source evidence passed164/20.
These counts are engineering checks, not hundreds of gameplay runs.

Actual two-Wizard recording:22 minutes39 seconds,81303 video frames, full strict
decode passed. That DuckStation, capture and read-only observer session is closed.
All nine protected hashes passed after exact original settings restoration.

The separate fresh-Wizard test session is now closed. Its original settings were
restored and all nine protected hashes passed. This does not change the R03 app
or its saved document; the original memory card was never replaced.

Next: finish the isolated app edit/save/close-reopen check,
complete fresh-Wizard reward/audio/resource contracts, then integrate and playtest that exact
enemy in the fresh scene. Continue expanding tested families/materials through
the editor without calling inventory coverage universal compatibility.

The [current engineering handoff](native-editor-resume-20260906.md) records exact
next steps. The [V6 readiness assessment](v6-readiness-20260906.md) explains why
universal importing is not yet a defensible percentage or release date.
