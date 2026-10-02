# UI navigation checkpoint — 2 October 2026

The ordinary editor workflow now offers a visible Save action and Cmd/Ctrl+S,
Open Game, Create BIN, and genuine File/More command menus. Menu navigation no
longer commits ComboBox selections. Fresh and retail scenes use the same toolbar.
Pending shortcut/button saves fence navigation, editing, repeat saves and close.
Save refreshes as both inner and outer level-load fences complete. File and Save
disable at Create BIN entry and restore after both export success and failure.

The Objects sidebar keeps its shared tools but groups height/snap controls under
Position & snapping, promotes placed-object search, and exposes Undo last removal
without opening Recovery. Export checks remain available through More and an
expander. Routine export success uses a concise status line while Details retains
the complete message. Failures and skipped-edit warnings are not shortened.

The Add Object dialog has a persistent footer and a minimum 900×620 layout.
Search has an explicit empty state and Clear search. Source versions are no
longer silently limited to 200, and asynchronous capacity refreshes preserve the
exact chosen version. Old capacity plans cannot be applied after selection changes.
Technical details retains precise native information. Native placement/export
guards remain unchanged.

## Evidence

Private evidence lives under task-root `work/ui-release-20261002`.

- `navigation-final-r3.log`: 50 headless checks; menu navigation does not create or
  save a scene; Cmd/Ctrl+S and Save write real fresh-scene geometry; held saves
  cannot overlap or close the editor; load/export completion restores controls;
  toolbar bounds at 980×640, 1060×720 and 1320×860.
- `regression-report/summary.json`: 346 Release gate cases, zero failures.
  The two subsequent UI-only refresh corrections were built and rerun across
  all 14 UI groups in `ui-final-suites-r3/summary.json`, zero failures. The native
  Core is byte-identical in source to the previous gem checkpoint.
- `catalog/review-only.log`: 30 source-backed browser checks, including all >200
  actual source versions, selection preservation, empty search and footer bounds.
- `catalog/source-workspace/_local/ui-overhaul/`: minimum-size and selected-model
  screenshots from the headless renderer. Actual packaged GUI evidence is separate.
- `universal/verification-receipt.json`: unchanged USA/source/Core identity,
  4,680 native audit checks and the exact 396-cell linked-object refusal. This adds
  no new universal composition or gameplay support.

## Final package and actual GUI

Final packages are in task-root `outputs/SpyroEditor-V1.0.5-release-r3`, built
from clean source `a4e8a35d7c913feb947638e5ec9cb8adb46762f1`.

- Mac ZIP SHA256:
  `88ab5022727b7ec6dfd80c66c7fd47e9f61c520e23c4228d7ff2052ac92be5f3`.
- Windows ZIP SHA256:
  `f6b4f5130c0a924bd45038524b2b78b73ebb4d7801a8643bae078bcf180c18ff`.
- `package-build-r3.log`: both packages passed verification; Mac Developer ID
  signed without notarization. Windows runtime remains untested.
- CUA operated the signed Mac app in the private cloned project at
  `gui/projects/default-project`: Save after loading, Save button and Cmd+S,
  ordinary Create BIN, short success status and full Details, control recovery
  after export, textured terrain tools and process restart. Artisans retained
  all 176 objects and 250 treasure including both added gems. The object picker
  empty state, Clear search, teal model preview, fit result and footer were
  checked in r2; its implementation is unchanged in r3.
- Native Mac flyout child windows were not exposed in the CUA main-window
  capture. Menu navigation is covered by the headless tests; native menu-command
  activation is not claimed as a separate GUI acceptance result.
- `gui/export-r3-readback.json`: final BIN SHA256
  `be08b8ea0a9766067bdf56764edeb1b0196d294629b2b24a991f6242f0bfb556`,
  661,547,040 bytes. It is byte-identical to the previous independently checked
  GUI gem export, and the CUE points to that BIN. No new emulator run was needed
  to establish that this UI change generates the same bytes; no new gameplay
  coverage is claimed.
- `user-project-preservation.json`: all 173 protected files match the original
  snapshot. A CUA observation made after quitting r1 automatically relaunched
  the default workspace and migrated four metadata/guide files. The instance
  was closed, those four files were archived under
  `gui/automatic-reopen-restored` and restored from the authenticated snapshot.
  No scene edits changed. Later launches explicitly used only the private clone,
  and observations were not made after quitting. Original USA disc hash was
  rechecked and matches the established source identity.

Final identities and detailed evidence boundaries are in
`work/ui-release-20261002/final-checkpoint.json`. The final documentation
checkpoint is identified by local tag `checkpoint/v1.0.5-ui-navigation-20261002`.

## Scope

Native game generation is unchanged. Original textured terrain, bright 50/100
gems, portals, blank-level authoring and existing native guards are retained.
This UI checkpoint does not establish full universal gameplay, whole-game
progression, Mac notarization or Windows runtime acceptance.
