# Saved whole-level materials and raw picker

September 8, 2026. This is source/headless evidence, not an updated packaged app
or universal texture support. R03 and R04 remain unchanged.

## What is implemented

The aggregate window now offers an explicit schema-2 material action and two
ordered floor-triangle slots. Each slot is either the Base floor color or a
retail source variant/WAD entry/texture ID. These are saved authoring choices;
opening the window does not read the ROM, import assets or upgrade schema 1.
The preview remains an explicitly labeled schematic.

All raw Base, selected Wizard route and material fields validate before one
session mutation. Invalid fields survive failed Apply, selection changes and
failed reopen. Undo/redo and save/reopen preserve both slots; changing only the
Base or a route retains the actual immutable material pair. Pending route Add
locks Base/material edits before allocating its new ID. Two color slots remain
schema 2, and color tags follow the Base color rather than a hidden swatch.

The separate internal `NativeFreshSavedMaterialCompilation` consumes an actual
saved aggregate read context, its actual Base compilation and ordered static
sources. It issues the selected texture/staging/body/disc chain with that saved
music selection. The first supported shape is zero placements and two static
retail materials. Mixed Wizard/material compilation, animated or ineligible
records and ordinary aggregate publication are not enabled by this bridge.
Neutral authoring may preserve a record that a native compiler later refuses.

The bridge independently checks the held original, current named original and
raw saved version, then attempts all acquired closes even after failure. A raw
version check is an observation, not a write-excluding lease. Publication still
needs its own actual final saved-document guard; the UI export remains disabled.

## Completed focused evidence

- Bridge: 43 source-free checks/17 refusals and 220 source checks/35 refusals.
  Five complete image goldens cover fixed T0 donors, selected T1 donors,
  reversed slots, repeated donor and changed heading/music. All five changed
  exactly 1,253 sectors. Original BIN/CUE final identities were unchanged.
- Root independently replayed the original-only saved-material reference; its
  complete JSON matched SHA `c70f992d8a47d1bbcb9abaf073b221cebcd6d73b43993c186e7e8bb9e79243cd`.
  Production round trips are not mislabeled as independent algorithms.
- New actual headless picker: 88 checks; prior material UI48, aggregate UI219,
  session138, legacy UI65 and research UI333 also pass. The strict isolated
  mode is `UiSmoke --fresh-material-picker-only`; mixed/repeated/extra arguments
  refuse before workspace discovery. These use retained tiny test projects.
- Root runner regression first failed on the missing registration, then passed
  68 tests after adding the distinct source-free marker/log route. Full Python
  discovery passed 223 tests with 13 explicit source/OS skips.
- Root's combined all-project Release/default gate passed328/zero failures.
  Root viewed both regenerated full-window frames after the final schematic
  footer wording change. H independently reviewed the complete picker contract,
  production diff and tests and found no production defect. Direct unknown-kind
  and reverse invalid-Base/material tests remain small coverage follow-ups.

The bridge's early failing tests incorrectly assumed a held raw read guard
excluded saves or allowed external reads of an exclusively held marker. Those
test assumptions were corrected, not the reviewed bridge. The picker's initial
missing-control failure was expected; a later fixture used an invalid uppercase
project ID and was corrected in the test only. Failed evidence remains retained.

Private evidence under the repository:

- `_local/research/dark-wizard-native-closure-20260906/saved-material-bridge-implementation-r01.LxljJf/`
- `_local/research/dark-wizard-native-closure-20260906/material-picker-implementation-r01.SZdmYG/`
- `_local/research/fresh-material-preview-oracle-r01.usQXof/` contains a separate
  Root-written, H-replayed raw pixel reference for 14/70 T0/T1, all 16 palettes.
  This last reference is preparation, not a shipped preview or static-eligibility
  certificate. A public preview wrapper and UI source worker are not yet present.

The previously played two-material ROM uses a fixed recipe, not this saved
aggregate path. See `native-fresh-textured-runtime-r01.md` for that narrower
normal-speed gameplay result. Combined-gate and later checkpoint status are
recorded in `native-editor-resume-20260906.md`.
