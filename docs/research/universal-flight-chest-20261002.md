# Original Flight chest replacement — 2 October 2026

The composer now admits one original Flight chest (class 12B) replacing one
original chest in an unchanged original Flight course with the same objective
category. Sunny, Crystal and Wild use category 3; Night and Icy use category 1.
This admits eight foreign source/destination combinations and five native
controls. It does not enable Flight objectives in ordinary levels or arbitrary
course reconstruction.

## Implementation

`UniversalFlightChestOwnership` authenticates the original USA executable,
source overlay/data, destination controller and all retained course actors.
It requires the five-class chest, timer-bonus and fragment closure, authenticates
source callbacks and copied instruction packets, checks the final authored
selector/state bytes, and refuses unsupported destination or mixed edits.
`UniversalFlightChestParticles` carries separate source evidence for native
Flight particles and the original no-op request. Matching source/destination
texture pixels and complete palettes are checked without claiming runtime proof.

The ordinary export first marks a removed native row with three -480000
coordinates. The certificate accepts exactly that known staging change for the
single replacement row, checks all other bytes, then reuses its physical slot.
It does not waive the destination guard for unrelated edits. Authored chest
position and yaw remain supported. The normal Remove then Add workflow can
supply this transaction; interactive UI acceptance is still pending.

## Verification

Evidence root: task `work/universal-finish-20261002`.

- Integrated `flight-chest-integrated-r1.json`: 1,011 source/composition and
  malformed-source/emission/authored-state checks across all 13 admitted cells.
- `flight-chest-negative-regression-r1.json`: 295 existing unsupported Flight
  guards pass, including copied identity classes.
- `flight-chest-runner-r1.log`: 115 runner checks pass.
- `flight-chest-app-build-r1.log`: Release App builds with zero warnings/errors.
- `coverage/flight-positive-r1/staged-export-run-r1/staged-export.receipt.json`:
  265 ordinary native-remove/export/readback checks. This is export evidence,
  not interactive UI evidence. Ordinary output SHA-256:
  `1291d1fe1adac3ad99e2bcb3f75514d80baa29d01963705f8c1b3162676b1d46`.

The Night Flight native fixture imports Icy Flight row 28 into original Night
row 28 at its original pose. Its New Game entry selects Night Flight; the full
32-objective course and native controller are retained. Tested image SHA-256:
`9ba42947d0a821df6b3ca257820014fccc6a5186e1cc3b8bf94e029d6bc739fa`.
Exact launch, firmware/card hashes, screenshots, read-only RAM and limitations:
`runtime/flight12b-night-r1/runtime-proof.json`.

A cold full-BIOS DuckStation run, without RAM writes or save states, showed the
chest, animated glow, flame destruction, textured debris and +2 bonus. The
native timer changed 1190 to 1296 over 14 frames: 120 ticks awarded. The chest
counter became one. Native failure results displayed Chests 1/8 and all other
categories 0/8. Native Try Again restored the chest, zeroed all counts and
restarted the countdown. The private blank card was unchanged and the emulator
exited without a resume state.

This is one bounded foreign replacement runtime test. The other seven foreign
cells, full course completion, course reward/save/reload and audio remain
unverified. Universal Moby gameplay is still in progress. The experimental Magic
pair runtime result remains quarantined pending its export ownership issuer;
it is not admitted by this change.
