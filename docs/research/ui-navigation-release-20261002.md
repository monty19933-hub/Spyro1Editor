# UI navigation checkpoint — 2 October 2026

The ordinary editor workflow now offers a visible Save action and Cmd/Ctrl+S,
Open Game, Create BIN, and genuine File/More command menus. Menu navigation no
longer commits ComboBox selections. Fresh and retail scenes use the same toolbar.
Pending shortcut/button saves fence navigation, editing, repeat saves and close.

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
  cannot overlap or close the editor; toolbar bounds at 980×640, 1060×720 and 1320×860.
- `catalog/review-only.log`: 30 source-backed browser checks, including all >200
  actual source versions, selection preservation, empty search and footer bounds.
- `catalog/source-workspace/_local/ui-overhaul/`: minimum-size and selected-model
  screenshots from the headless renderer. Actual packaged GUI evidence is separate.
- `universal/verification-receipt.json`: unchanged USA/source/Core identity,
  4,680 native audit checks and the exact 396-cell linked-object refusal. This adds
  no new universal composition or gameplay support.

Final regression, package, actual GUI export/readback, preservation and source
checkpoint identities are recorded in `work/ui-release-20261002/final-checkpoint.json`.

## Scope

Native game generation is unchanged. Original textured terrain, bright 50/100
gems, portals, blank-level authoring and existing native guards are retained.
This UI checkpoint does not establish full universal gameplay, whole-game
progression, Mac notarization or Windows runtime acceptance.
