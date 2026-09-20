# V6 development: what you can test

September 20 continuation. The preserved **R06 private preview** is the latest
editor build tested here. Newer source checkpoints are not silently installed
into it. Universal Moby/texture interchangeability remains the goal, not a
completed feature or a zero-bug guarantee.

## Playable editor workflow

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

## Not yet available in that build

- Tiled new-level export still requires zero placed Mobys. Existing separate
  Wizard/Grass research paths are not unrestricted tiled-level placement.
- Static material selection is bounded by native data and GPU capacity.
  Animated/scrolling/HQ materials are not universally supported.
- The catalogue can show assets without proving that every destination has
  their behavior, resources, state, effects and save/re-entry dependencies.
- Full portal/save ownership, arbitrary new levels and universal import remain
  unfinished. No public V6 release was made.

## Source work after R06

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
and [the pending saved-document integration plan](research/native-fresh-family-authoring-integration-plan.md).

The next source checkpoint adds neutral schema5 persistence for a complete
locked-chest family (one editor identity, independent key/chest anchors and
optional named WORLD reward destinations) alongside legacy or tiled content.
Save/Reopen, strict parsing and old document compatibility pass343 integrated
regression steps. Existing UI and native exporters deliberately refuse schema5
until they can preserve and compile the entire family; this is not a new
user-facing editor feature yet. See [the schema5 record](research/native-fresh-family-document-schema5.md).
