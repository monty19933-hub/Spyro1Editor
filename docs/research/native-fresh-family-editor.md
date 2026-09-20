# Complete locked-chest family authoring in Fresh Level

Status: integrated editor builds, focused neutral UI tests and the complete
343-step repository regression pass. Actual Window Save/research publication
also passes using the real saved worker and publisher, followed by separate-
process whole-disc verification. Separate R07 Mac/Windows packages pass the
release verifier; exercising the packaged app and its exact CUE are next.
R06 is preserved; no new gameplay result follows from these checks.

## One entry, complete controls

The Level aggregate inspector admits complete schema5 documents with either
legacy-floor or tiled content. One locked-chest family remains one selected
editor ID, with independent key/chest WORLD XYZ and all five named reward
members. Each reward uses either a source-relative native default or explicit
WORLD XYZ. Defaults have no invented coordinates; moving either anchor leaves
explicit destinations fixed. The closed source definition is read-only.

Add is pending until whole-document validation succeeds. Cancel or raw-field
Undo restores the applied form without installing an entry or schema upgrade.
Invalid values and inactive-field mutations retain the draft and actual applied
document. Valid Add/edit/remove/reorder use existing whole-document history;
no-op Apply preserves redo, and Save/Reopen retains exact semantic content.
No supporting Wizard or Grass is silently inserted when a family is added.

The important model correction is atomic tiled-content-and-placement editing.
The final tile and final family anchors are validated together, even when either
intermediate document would be invalid. Reconstruction retains schema5 rather
than passing through the schema4 factory, including after the last family is
removed. Existing one-argument tiled edits delegate to the same invariant.

## Honest schematic and asynchronous work

Every family exposes seven immutable presentation members. The schematic plots
key/chest anchors and explicit reward destinations in XY; exact Z remains
numeric. It never computes native default destinations or simulates trajectories.
The selected family (or clearly labeled first family) has a complete seven-row
legend. Off-surface/outside-view points retain authored coordinates and visible
callouts; outside-view markers are not clamped to a misleading edge position.
All other families remain represented by their actual parent IDs and markers.

All 21 coordinate fields and five reward modes participate in raw invalidation
and complete structured snapshots for catalogue, material-inspection and saved
research operations. A change followed by restoration during an await is still
stale. Outstanding workers are drained and their real editor owners settled;
late results do not replace the draft, create pending placements or publish.

## Native eligibility is narrower than authoring

The readiness predicate reuses the complete saved compiler's neutral policy,
not a second independently maintained recipe. Only the exact legacy-floor R03
Wizard + Grass + locked-chest profile currently enables saved research export.
The actual saved compiler then captures original source owners and uses the
same public saved publisher and full-image forward/inverse verification.
Three authored entries expand to nine native rows, not a partial P0/P2 export.

Other edited family layouts, default/reordered/extra/missing placements and
schema5 tiled documents remain editable and saveable, but authored-only.
Normal Create BIN, arbitrary family layouts, native persistent-save ownership,
complete dependency/census closure and universal Moby interchangeability are
not approved. See [saved compilation](native-fresh-saved-locked-chest-r03.md)
and the separately observed [R03 gameplay](native-fresh-chest-r03-runtime.md).

## Verification ledger

Logs: `_local/research/v6-mixed-export-20260912-r01.jR0oBr`.

- 557: tests-first missing session/atomic APIs (plus corrected preview test API).
- 558: missing new eligibility predicate captured during integration.
- 559/560: invalid evidence; an older executable entered isolated legacy UI smoke
  after a failed build. Both were stopped with exit130. They are not family RED
  results, source publication or gameplay tests.
- 561: fixed App reference to an internal-only variant constant; no behavioral claim.
- 562: baseline build, zero warnings/errors.
- 563: runtime RED, missing complete `FamilyPreviews`; eight old PNG/geometry/caption
  references captured before renderer changes.
- 564: session153 checks pass, then expected complete-schema5 admission RED.
- 565/566: complete App/UiSmoke builds, zero warnings/errors.
- 567: session153, admission86 (16 complete admissions, two malformed-target refusals,
  four old schemas), actual controls49, async/readiness1366, preview225 pass.
- 568: independent preview225 passes; eight old geometry/caption and macOS PNG
  identities unchanged. Minimum360-square and640×480 images inspected.
- 569: saved-research arguments152, prior readiness1319 and lifecycle6444 pass.
- 570: full343-step gate found one old catalogue auto-reveal regression. The
  new pending-add form unnecessarily cleared inactive Wizard coordinates,
  violating their existing preservation contract and queuing late raw events.
  All other342 steps passed; this is not a green whole-repository checkpoint.
- 571/572: restoring the inactive selected Wizard/family forms builds cleanly
  and the complete old/new aggregate UI mode passes, including preview232.
  A broader synchronous raw-event regression is being added before promotion:
  Avalonia12 documents `TextChanged` as asynchronous and `TextChanging` as
  synchronous after the text value changes. Legitimately resetting the active
  pending-native form must also be tested; preserving other forms alone is not
  claimed to solve that event-timing issue.
- 573/574: new timing tests build cleanly and fail before the first dispatcher
  pump on `FreshFloorX`, confirming the delayed raw-generation defect.
- 575/576: all seven authored-text subscriptions use synchronous `TextChanging`;
  clean build and213 timing checks pass across45 text fields, owned refresh,
  no-op Apply, and native Stage from selected family/existing native entries.
  The actual queued-reveal owner/generation checks remain unchanged.
- 577: the full old/new aggregate UI mode passes, including213 timing and1366
  async checks. The eight old pixel/geometry references remain unchanged;
  family preview232 also verifies Reward2-only mapping and suppresses only
  colliding marker labels without moving or omitting markers or legend rows.
- Internal preview build advances to0.1.0-beta.44 so R07 can be distinguished
  from the preserved beta.43 R06 application.
- 578: complete build-all repository regression passes343/0. Report:
  `/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-c7chmqw2/summary.json`.
- 579: actual Window Save and research action pass21 source-backed checks.
  Production saved worker/publisher emit the complete R03 image
  `a5b00725388f73e7e6f7b9ed356c4a0d0f55b1c869d6a65f38e97ef2641d6c50`.
  The test picker and deliberate post-success owner error are the only seams;
  the returned publication remains retained and correctly classified. Originals
  pass held/named/final checks and all acquired owners close.
- 580: independent verification invocation used an incorrect shortened project
  ID and correctly refused at the manifest-identity guard; output was retained.
  Rerun582 uses the actual captured manifest ID without modifying the fixture.
- 581: separate Mac/Windows R07 community packages build and pass the release
  verifier, both internal version0.1.0-beta.44. No notarization, public upload
  or Windows runtime test occurred. R06 remains untouched.
  Mac ZIP SHA256 `4fb368684dca59633c47b07940412fab986a97508876fcda1b10d39d863190ac`;
  Windows ZIP `c161f3bdc3ab32422e832da4f37b37d82224544b2456c84169dea6ce16a771a3`.
  App DLL `f62f3cd5a7b01c3b9cf31667c4aa3f2abd8f8e52fabeca0328aff78e9c4821e4`;
  Core DLL `61d662b602b05da38ec95b2c0b4c55fdc6262674402688c27897c8f4aca58e52`.
  Package root: `_local/v6-private-preview-20260920-r07.6brXeJ`.
- 582: corrected independent verification passes full readback, physical
  inverse, CUE/receipt/document identity and final Original owners.
  Image remains `a5b00725388f73e7e6f7b9ed356c4a0d0f55b1c869d6a65f38e97ef2641d6c50`,
 1297 changed sectors; plan
  `7b6f7e0fb9f9651daf75b50c824ee5529ef5493c2be5470007ff35db73a2d0ec`.
  Saved raw/canonical `b7c1513f277a2ced1a1c1ea36d2d668349c9c01f5173c618e76d744c9b7a177b`
  remains unchanged. All runtime/public/persistent-save/dependency-closure
  approval flags remain false.

Pixel references describe the captured macOS/Avalonia12/headless-Skia default-font
environment. Non-macOS runs explicitly report pixel equivalence as unverified;
exact geometry/caption invariants remain checked everywhere. No golden is silently
refreshed. A later test-only assertion also checks the untouched R03 Reward2-only
mapping independently of the all/default reward loops.
