# Original Flight gate replacement — 2 October 2026

One original Flight gate (class 161) can be replaced by a gate from a course
using the same objective category. The replacement keeps the original gate's
position, yaw and crossing plane, and retains the donor's time bonus. It preserves
the full original course, controller, other objectives, results and retry path.
This does not enable arbitrary Flight objectives in ordinary levels.

## Editor and exporter

Select an original gate, choose **Replace Flight gate…**, select the source,
review its bonus and capacity result, then confirm. Cancel changes nothing.
The transaction removes the native row and inserts the imported gate into that
physical slot. Save/reopen preserves the transaction. Undo Object, Undo last
removal, or Remove on the replacement restores the original gate. Movement,
rotation, copying and property edits of the replacement are disabled because
this adapter requires the original crossing plane and position.

`UniversalFlightGateOwnership` checks the original executable, source data,
overlay callbacks, destination course population, prepared removal and final
authored row/properties. It certifies the root, time-bonus child and thirteen
fragment classes. The fragment spawn loop supports noncontiguous mapped class
IDs, authenticates the source and copied control-flow packet, and retains the
native loop counter and parent. The native fragment constructor alias is copied
when the class namespace changes. Native Flight particles have separate source
texture/palette evidence; that evidence does not imply runtime acceptance.

Sunny, Crystal and Wild gates share category 1. Night gates use category 2 and
currently have only their native control. Four foreign combinations and four
native controls compose. Crystal and Wild imports into original Sunny Flight
still exceed native RAM capacity and are refused; removing an objective does
not grant permission to discard its retained course dependencies.

## Verification

Evidence root: task `work/universal-finish-20261002`.

- `flight-gate-integrated-r1.json`: 1,050 source, emitted-instruction, authored
  state and composition checks across the ten same-category combinations;
  eight compose and two are genuine capacity refusals.
- `flight-gate-ui-integrated-r1.log`: 25 actual headless control checks covering
  cancel, confirmation, changed-target refusal, placement locks, save/reopen,
  ordinary removal/export/readback, undo and repeated transaction identity.
  The last three checks also exercise the normal catalog workflow for a
  supported class 12B chest replacement.
- `flight-gate-chest-regression-r1.json`: 1,011 existing chest checks pass.
- `flight-gate-negative-regression-r1.json`: 295 unsupported Flight checks pass.
- `flight-gate-runner-r1.log`: 115 runner tests pass.
- `flight-gate-ui-build-r1.log`: Release UI smoke and App build without warnings
  or errors.

The actual UI export SHA-256 is
`555eb341e0ab3ab29c7f9144764735376f4762383ca62af3c1e866bbc33082e4`.
`flight-gate-ui-export-identity-r1.json` compares the entire image with the
immutable ordinary runtime fixture. The tested image has the same complete WAD;
only the two native New Game level instructions select Crystal Flight 35.
Tested SHA-256:
`9fdf1e3d9e449600e59c7fb350f8b7f248d12b6a1b1b580ca5b0767d73835249`.

`runtime/flight161-crystal-r1/runtime-proof.json` records the exact CUE, full
firmware and blank-card hashes, screenshots, read-only RAM captures and exit.
In a cold full-BIOS DuckStation run, Sunny row 0 replaced Crystal row 15 at
the original pose and crossing plane. The gate rendered with its shimmer and
time label. Native flying through it retired the root, raised Arches to one,
created the bonus child and all thirteen mapped fragment classes, and awarded
exactly three seconds: 1,262 ticks minus eight frames plus 180 ticks = 1,434.
Native failure results displayed Arches 1/8. Native Try Again restored the gate,
zeroed all counts and restarted the countdown. There were no RAM writes or save
states; the private card stayed unchanged and no resume state was created.

Only Sunny-to-Crystal has this bounded runtime acceptance. Full course
completion, reward/save/reload, audio and the other source/destination runtime
combinations remain unverified. Universal gameplay remains in progress.
