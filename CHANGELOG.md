# Spyro Editor Beta V6

## Changes since Spyro Editor Beta V5

Private development preview, internal build `0.1.0-beta.44`. This is not a
published release or a claim of universal Moby, texture or new-level support.

- Added Fresh Level authoring to the normal application: floor bounds, spawn,
  heading, colors, native music selection, ordered placements and two floor
  material references. Apply, undo/redo, save and reopen preserve the complete
  document. The preview remains schematic, not game rendering.
- Added complete locked-chest family authoring: independent key/chest anchors,
  all five default or explicit WORLD reward destinations, one stable editor ID,
  and whole-document history/save/reopen. Tiled edits retain family content
  atomically; only the exact saved legacy-floor R03 profile enables research
  export. Arbitrary families and tiled Mobys remain authored-only.
- Fixed raw-field event timing so edits invalidate pending work immediately,
  while form refreshes do not leave delayed events that disrupt Moby staging.
- Added source browsing for native Moby donor rows across the inventoried game,
  with source variants kept distinct, explicit placement staging/cancellation,
  and clear separation between saved authoring intent and supported imports.
- Connected supported saved fresh-level profiles to the existing checked
  candidate/publisher path, with separate BIN/CUE output, exact source checks,
  stale-save refusal, complete disc readback and retained recovery evidence.
- Added the bounded Wizard + Grass, two-material example. Focused native tests
  observed its authored background, Wizard attack/charge defeat and gem reward,
  fall/respawn and isolated jump/landing. Other combinations, portals and
  durable native save ownership are not implied by those observations.
- Strengthened shared Moby source/dependency handling and regression coverage
  while retaining historical locked-chest outputs and the original retail
  editor's guarded export behavior. Native family portability remains ongoing.
- Added default-OFF Fly Anywhere settings with Save/Revert in the Level tab.
  The initial ON export supports Toasty without other edits; explicit OFF
  rebuilds the complete original ROM through normal Create BIN. Stale settings,
  unsaved drafts and source/output aliases are refused. ON/OFF physical export
  checks pass; native gameplay acceptance of this integrated path is pending.
- Replaced the family composer's single-model append with shared ordered batch
  storage while preserving existing output bytes and native admission limits.
- Preserved the legacy ID65 Blank-Level Lab separately from fresh documents.
  Normal retail Create BIN does not consume fresh-level documents; the separate
  fresh-level action reports its supported profiles and refusals explicitly.
- The private Mac preview uses disclosed ad-hoc signing, not Apple signing or
  notarization. No game images, BIOS, extracted assets, emulator cards or states
  are bundled. Existing apps and protected projects are not replaced.
