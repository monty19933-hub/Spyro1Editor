# V6 development: what you can test

September 25 continuation. **R08 beta45 packages and actual GUI editing/export
now pass**, including independent new-process complete output verification and
all eight physical publication/failure cases. R08 is the latest packaged editor exercised here. R06/R07 are unchanged; the new work is
not silently installed into either. Universal Moby/texture interchangeability
remains the goal, not a completed feature or zero-bug guarantee.

## Latest source and gameplay checkpoint

September 27 continuation: a separate research disc now demonstrates **two
independently colored Japanese eggs animating together in USA Artisans** after
full-firmware cold boot. Ordinary inventory and return to gameplay also passed.
These are diagnostic authored colors, not final presets or a new editor build.
Custom pickup, inventory, memory-card and percentage hooks are not installed.
See [the exact runtime evidence](research/japanese-egg-authored-colors-runtime.md).
New source-only work adds a bounded selected-slot memory descriptor (90 offline,
133 exact-source checks) and collection bookkeeping machine code (1812 CPU
checks). Neither is installed or grants persistent-save approval. R08 remains
the latest packaged editor, unchanged; normal DuckStation settings/card are
restored and no test emulator remains running.

The latest September 27 default Release regression completed **246 checks,
0 failed**, including the observer, collection-state suites, reservation
mechanics and the combined pickup caller. Its report is
`/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-8lcvdcpv/summary.json`.
This is the default source-free gate, not the larger optional source-QA run.
The preceding 239/0 and 235/0 reports remain preserved as
`spyro-regression-pjvgr70q` and `spyro-regression-asd6fdwc`.

The [combined pickup caller](research/japanese-egg-detached-collection-attempt.md)
now joins actual source proximity checks with stable GUID-based collectible
bits (384 offline / 441 original-ROM checks). Moving or reordering rows does
not transfer collected state between identities. The separate
[native retirement proof](research/japanese-egg-placed-retirement.md) passes
541 source checks and proves removal on the next native render-list rebuild.
[Reservation mechanics](research/collectible-reservation-mechanics.md) pass
12 offline / 107 source checks, including native allocator/sentinel/copy writes.
The [ephemeral boot initializer](research/collectible-ephemeral-cold-boot-bootstrap.md)
passes 16 offline / 121 source checks: it clears the proposed ledger to
NOT-LOADED, then original startup erases the temporary code. A separately
published two-sector startup-only disc now also passes a bounded full-firmware
Artisans movement/jump/native inventory-open-return check. Three read-only
samples retain zero reservation bytes and no renderer exhaustion. See the
[exact startup runtime and limits](research/collectible-startup-runtime-r01.md).
This does not prove every cache/IRQ/lifetime property or clearing execution
from zero bytes alone, and no collectible or save hook is installed.
These units are not yet installed together in a game: native boot initialization,
selected-slot identity, safe dispatch/lifetime, inventory and card transactions
remain required before a usable collectible-enabled editor build.
The [selected-save boundary](research/collectible-selected-save-admission-boundary.md)
is now source-pinned, including repaired-save and stale-selection counterexamples.
Native new-game intent alone cannot safely reset or initialize custom progress.

After that full246/0 checkpoint, focused source work added
[validated detached save-to-ledger preparation](research/collectible-detached-ledger-hydration.md)
(896 checks) and a [read-only collectible query](research/collectible-read-only-state-query.md)
(3461 combined checks/233 query cases). The
[native scheduling tests](research/japanese-egg-pickup-scheduling.md) now pass
77 source checks, including the earned-outside-proximity respawn counterexample
and test-only previsual suppression. Pickup-caller384 and runner-contract104
checks also passed after integration. The full suite has not been repeated for
these last source-only components; no new packaged app or installed collector
is claimed. Native selected-save admission/card transactions remain next.

Checkpoint `dcf5dce` adds saved-project compilation of the exact four-tile,
two-material Wizard/Grass/key/chest layout. Its independent full-disc identity
matches the actual research candidate tested in DuckStation. The full firmware
boot, native LAB **20/20** inventory, both central tile seam crossings and a
healthy fall/reset have now been observed for that exact candidate. Collected
treasure stays20 and the chest stays retired after reset. This is a single
tested layout, not unrestricted new-level export or all-Moby compatibility.

The test files and screenshots are in this repository's
`_local/native-v6-tiled-family-runtime-20260925-r01.wqwkPe/`.
See [the exact runtime result and limitations](research/native-fresh-tiled-family-r01-runtime.md).
DuckStation settings were restored byte-for-byte and the ordinary memory card
and original BIN/CUE are unchanged. No test emulator is left running.

That complete saved compiler now connects to the existing guarded
Candidate/publication/UI path. Actual source650 passes148 checks; headless
production-worker UI707 publishes the exact complete disc and passes44 checks.
The new tiled readiness suite passes559 checks. Private beta45 packages and
actual editing/Save/Reopen/native-folder-picker export pass. The emitted BIN
is byte-identical to the gameplay-tested689f candidate above; this is not a
fresh emulator session for the GUI CUE. Independent verification720 and the
full eight-case fault matrix651 pass. See the
[R08 integration evidence](research/native-fresh-tiled-family-editor-r08.md).
Latest full regression725 passes345/0. The source-only shared face-binding
refactor preserves the existing complete native output and passes127 material
patterns without enabling them for export. New ordinary-chest evidence tests
pass in source724; they prove a byte-identical control, not portable placement.
See [the binding work](research/flat-tile-material-binding.md) and
[C2 dependency evidence](research/c2-artisans-source-access-audit-2026-09-25.md).
Earlier full regression638 passed345/0; saved compiler source640 passed63 checks;
legacy source641 passed291 checks and preserved R07's complete native inverse;
the updated saved-level source-free suite643 passed1357 checks/937 refusals.

The current source also exposes54 shared PETE model registrations in both Moby
browsers. They are inspect-only: discovery does not grant safe placement.
This includes the ordinary retail dragon egg, **not a verified Japanese
dragonfly egg implementation**. Japanese editable signs, egg color correction,
new inventory and completion accounting are still implementation work. The user
has now supplied Japanese SCPS_100.83; its source identity and regional archive
reads pass the new isolated source checks. The new deterministic USA matrix
covers368 known classes and12,880 retail destination cells, without widening
placement/export approval. R08 remains unchanged. See the
[active full milestone](research/v6-universal-japanese-milestone.md) and
[machine-readable matrix work](research/universal-moby-matrix.md).
No game assets were downloaded. See
[Japanese objects: findings and next gates](research/japanese-pocketstation-objects.md).

Later on September25, a separate source-checkpoint37ffa24 research disc was
independently verified and tested through full firmware: two Japanese eggs now
visibly animate together in USA Artisans. They are still bright green at close
range; color editing, pickup, new inventory counters and save/completion hooks
are not installed. The ordinary inventory can open and return to gameplay.
This disc is **not a new editor build** or a universal-placement approval. See
[the exact visual experiment](research/japanese-egg-visual-research-publication.md).
DuckStation's normal settings/card are preserved, and the test session is closed.

## New R08: tiled terrain and complete chest family together

Open this repository's
`_local/v6-private-preview-20260925-r08.7HLbzh/Open V6 Tiled Family.command`.
Use **Fresh Level… → Level aggregate**. The isolated example combines four
tiles, two retail materials, Wizard, grass, key, chest and five reward members.
You can test whole-document editing, Apply, Save/Reopen and inspect-only shared
Moby resources. Only the exact included profile enables native research export;
other valid edits are saved as authored-only. The preview remains a schematic.

Its actual exported CUE is in
`native-ui-exports/fresh-native-research-20260925-160932140-fb266093/` inside that
R08 folder. Read `PRIVATE-PREVIEW.md` for opening, export and LAB-entry steps.
The beta45 app is preserved at checkpoint`b50874d`; later source refactoring is
not silently installed. This is not a public V6 release.

## Preserved R07: complete chest-family editor

Open this repository's
`_local/v6-private-preview-20260920-r07.6brXeJ/Open V6 Chest Family.command`.
It uses its own test project/settings without rebuilding or resetting yours.

In **Fresh Level… → Level aggregate**, select the chest family. You can edit
the key/chest positions and all five reward destinations as one complete entry,
with default or explicit world coordinates, Apply, Undo and Save/Reopen.
The seven-member schematic/legend shows every family member. Pending Add,
Cancel and raw-field guards are tested; incomplete raw edits disable export.

The included exact Wizard/grass/chest example can use **Create research
candidate…** to publish a new BIN/CUE/receipt folder. Other authored layouts can
be saved but may be labelled authored-only: this is not arbitrary native
placement or normal Create BIN support. Choose an R07 output folder explicitly;
the native folder picker may remember an older R06 folder.

This actual packaged workflow passed editing, Save, publication and independent
complete-disc verification. Its exact output cold-booted in DuckStation and
reached LAB65 with visible authored floor/actors, grounded movement and native
enemy damage. This new run did **not** repeat20/20 collection; it ended after
lost lives with0/20, so the earlier separate R03 full collection remains separate.
All usual emulator settings and original card/ROM contents are preserved.
See [the exact R07 evidence and limits](research/native-fresh-family-editor-r07-runtime.md)
and the launch folder's `PRIVATE-PREVIEW.md` for test instructions.

## Preserved R06: editable tiled terrain/material example

Open this repository's
`_local/v6-private-preview-20260919-r06.ztR4LG/Open V6 Selected Materials.command`.
Use the launcher rather than the bare app: it selects the isolated test project
and settings without rebuilding or resetting your normal project.

In **Fresh Level… → Level aggregate**, the saved example has three adjoining
tiles and three static retail material selections. You can change the geometry
and supported material choices, apply the preview, save the document, and use
**Create research candidate…** to create a new BIN/CUE/receipt folder.
The folder's `PRIVATE-PREVIEW.md` has exact instructions and the LAB-entry code.

The exact editor-produced example cold-booted through full firmware in
DuckStation. Grounded crossings of both seams and a collision-cell boundary,
forward/reverse material views, jumping, falling and respawning were observed.
This is actual gameplay evidence for that example, not just a successful build.
Its source checkpoint is `01ef92a`; its whole BIN hash is
`f1c137476833d1c046a78df812b40a47f8dc6558051c823437d7c5b3f86d8e32`.
Original emulator settings and memory-card contents were preserved/restored.

## Still unfinished

- R08 exports only its exact combined four-tile family profile. R06's broader
  selected-material tiled route still requires zero placed Mobys; R07 cannot
  export tiled families. Arbitrary mixed layouts remain authored-only.
- Static material selection is bounded by native data and GPU capacity.
  Animated/scrolling/HQ materials are not universally supported.
- The catalogue can show assets without proving that every destination has
  their behavior, resources, state, effects and save/re-entry dependencies.
- Full portal/save ownership, arbitrary new levels and universal import remain
  unfinished. No public V6 release was made.

## Earlier source work after R06 (chronological history)

Checkpoint `72ace6f` adds a detached chest-code extension bound to the actual
mixed Wizard/Grass executable. Its independent source tests preserve the old
code and verify changed dispatch, callback-entry arguments, initializer and
complete inverse. The full regression passes343 command steps; the final
source-backed run passes8579 assertions with original BIN/CUE unchanged.

The scene stage now adds the key, chest and five reward rows through
the existing scene allocator. It matches the independent nine-row scene bytes,
passes343 full regression steps and8885 final source assertions, and preserves
the complete inverse to the previous scene. A subsequent Body stage installs
that scene while preserving every byte outside it, passes9034 source assertions
and343 full regression steps, and checks the enlarged Host's runtime addresses.
The next source stage places that family on the authored floor and binds an
explicitly experimental target20 executable. Shared physical export machinery
now constructs the chest image and inverses all sectors back to the original;
the full run passes9573 source assertions and343 regression steps, with prior
export goldens preserved. This is not yet a gameplay pass or editor admission.
Key-zero contribution remains an explicit assumption, not a complete census.
See [the private experiment record](research/native-fresh-chest-experimental-disc.md).
The actual chest BIN was subsequently written and fully read back/inversed
(544 assertions). Its first DuckStation entry **failed usable spawn**: the
authored spawn overlaps the chest's native sphere and Spyro is displaced off
the small floor. This is not a verified chest gameplay path and is not enabled
in R06. A separate source-bound diagnostic and earliest-entry observations are
recorded in [the failed-spawn record](research/native-fresh-chest-spawn-failure.md).
The failed session was closed; both original settings files were restored
byte-for-byte and the original memory card was unchanged. A separate corrected
layout now passes source-owned spawn/landing clearance and independent wider
floor/material/placement tests. It keeps the original spawn and native chest
behavior, enlarging the finite floor to3072 and moving the key/chest family.
Actual R02 output verification passes392 assertions, including full readback
and complete Original inverse. Fresh full-firmware gameplay now confirms stable
landing, native key pickup, chest opening, debris/effects and nine of ten treasure points
from the chest. The last red gem travels off the finite floor and falls, so R02
is a preserved **incomplete reward-collection test**, not a lifecycle pass.
The next layout must accommodate actual reward motion, not merely the saved
controller centers. See [the R02 runtime record](research/native-fresh-chest-r02-runtime.md).
The R02 writer now refuses before source I/O or output-directory creation;
its source-only recipe and preserved artifact are unchanged.
Final source-backed checks pass9676 and2355 assertions/checks respectively;
the source-only key audit identifies the additional HUD key/particle path and
still does not claim a complete key-zero proof. Existing public
single-floor export retains its2048 limit; this is an internal experiment,
not a replacement R06 application. See [the wider-layout record](research/native-fresh-chest-wide-layout.md).
See [the scene record](research/native-fresh-chest-scene-extension.md) and
[the behavior record](research/native-locked-chest-behavior-layout.md) for current
implementation evidence. These source-only changes do not replace R06.

A subsequent internal authored-destination extension removes the fixed-pattern
limitation for the five chest reward roles without exposing raw native rows.
The corrected R03 experiment changes only the lost red gem's destination;
actual full-image output/readback/inverse passes417 assertions, full regression
passes343/0, and both old image/plan identities remain unchanged. The exact R03
disc has now cold-booted through firmware and completed native Wizard/key/chest
interaction: all20 treasure points were collected, with no active reward gem
left in the sampled pool. The in-game inventory shows LAB20/20. A fall and
subsequent healthy reset preserved the totals and kept the collected chest
retired. This is one exact-candidate result, not all-layout/RNG/lifecycle or
portal/save compatibility acceptance. Both emulator settings files were
restored byte-for-byte; the original card and BIN/CUE are unchanged.
See [the R03 runtime record](research/native-fresh-chest-r03-runtime.md).
This does not yet add chest-family authoring to R06. See
[destination editing and evidence](research/native-fresh-chest-authored-destinations.md)
and [the saved-document integration plan](research/native-fresh-family-authoring-integration-plan.md).

The next source checkpoint adds neutral schema5 persistence for a complete
locked-chest family (one editor identity, independent key/chest anchors and
optional named WORLD reward destinations) alongside legacy or tiled content.
Save/Reopen, strict parsing and old document compatibility pass343 integrated
regression steps. At that checkpoint, UI and native exporters deliberately refused schema5
until they can preserve and compile the entire family; this is not a new
user-facing editor feature yet. See [the schema5 record](research/native-fresh-family-document-schema5.md).

The following saved-native compiler checkpoint now resolves the complete
schema5 R03 document to the actual Wizard/Grass/chest resource, behavior,
scene and physical-disc chain. Normal saved publication and a separate-process
verifier pass full readback plus complete Original inverse; its BIN matches the
previously playtested R03 image. This is a saved-project export result, not a
new gameplay test or an installed family editor. All reviewed publication-fault
checks now pass, including postcommit notification failure and cancellation. See
[the saved R03 compiler record](research/native-fresh-saved-locked-chest-r03.md).

The next complete family UI checkpoint adds one-entry key/chest and all five
reward controls, pending Add/Cancel, complete history/persistence, a seven-member
schematic and source-free eligibility matching the actual compiler. Synchronous
raw text invalidation fixes an existing asynchronous stale-edit/reveal defect.
The build-all repository gate passes343/0, including new timing, async, preview
and complete schema5 controls tests. Internal R07 version is beta.44, distinct
from preserved R06 beta.43. Actual headless Window source publication579 and
independent complete-disc verification582 pass. Separate Mac/Windows R07
packages pass verification581. Packaged-app controls/Save/publication583 and
independent verification585 subsequently passed, followed by the limited
exact-CUE runtime recorded above. See [the family editor record](research/native-fresh-family-editor.md).

The next source-only step is a four-tile terrain/family composition, preserving
the existing whole-floor material split and complete Wizard/grass/chest intent.
An independently assembled neutral reference passes102 negative controls586.
Subsequent complete data and Body references, source-only composition and
native parser/model witnesses now pass; the actual-source suite passes961
checks while preserving the exact old R03 disc and complete inverse.
The key's model is verified through the game's shared asset pool, not falsely
treated as a local level model. Combined tiled-family native Scene/behavior,
executable/disc, saved publication and exact-CUE gameplay are still next gates.
Neither preserved application has been replaced. See the complete
[tiled-family implementation and restart plan](research/native-fresh-tiled-family-resource-plan.md).

## Earlier safe usage checkpoint — September 20

The September20 continuation stops after full regression610 (343/0), source609
(961 assertions), and independent native-model witness603. All subagent work
is finished. No editor, emulator or observer session remains open. R06/R07
packaged App/Core hashes were rechecked unchanged; DuckStation global/game
settings and the Original memory card match the preserved pre-test bytes.
No original ROM, default project, save or known-good build was replaced.

Resume with the bounded native Scene-storage witness described in the restart
plan, then typed executable/clearance/disc and saved-publication integration.
Do not enable combined tiled-family export or describe all Mobys/textures as
interchangeable before those gates and fresh exact-CUE gameplay pass.
