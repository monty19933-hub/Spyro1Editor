# Spyro editor — what is working now

Updated September 8, 2026 after the connected Save → research export, exact
DuckStation gameplay test, separate R05 package verification and source-tested
Wizard/material document ancestry.
Development continues with the requested pause near 5% usage **remaining**.
This is a development checkpoint, not a V6 release. Original game/save files and
the preserved R03 editor have not been replaced.

## New result: a blank level with a working enemy appeared in game

The corrected, separately published test ROM loaded the authored teal floor,
brown sky, Spyro, Sparx and the imported Wizard. Spyro descended vertically and
landed at the saved spawn without the earlier sideways launch. The Wizard then
attacked; health decreased from 3 to 2 to 1 while Spyro remained on the floor.
In a fresh cold-boot test, Sparx disappeared, the next attack killed Spyro,
the life count decreased from four to three, and Spyro respawned on the authored
floor with Sparx and full health restored. Native screenshots and independently
checked read-only observations support this exact sequence.

This used controlled 10% emulation speed. Player-state6/timer0 was observed at
the exact spawn with zero motion before the subsequent vertical descent. This
is not continuous execution tracing or an all-game acceptance test. A later
normal-speed test with working held controls now passed flame defeat, yellow
gem pickup (LAB10/10,total58→68), edge fall/respawn, and second defeat producing
a purple orb without another gem award. Ordinary portal reentry, sound,
resource limits and save/reboot persistence remain separate checks. All sessions
are closed and original settings restored.
See the [death/respawn report](native-fresh-separated-wizard-runtime-r02.md) and
[normal-speed entry report](native-fresh-separated-wizard-runtime-r03.md), then the
[held gameplay and reward report](native-fresh-separated-wizard-runtime-r04.md).
The earlier observer refusal remains preserved and was not reclassified.

Both DuckStation settings files were restored exactly after quitting. Original
BIN, CUE and memory card, the three candidate files and emulator executable
matched their pre-test hashes. No original card was replaced.

## Other working foundations

- A separate whole-level document preserves floor settings and an ordered object
  list with stable IDs. Its first supported placement type is a Wizard route.
- Explicitly forking a saved floor into that document preserves the legacy file;
  the two documents do not silently synchronize.
- The existing Fresh window now has an explicit whole-level mode with route
  adding/editing/removal/reordering, save/reopen and whole-document undo/redo.
  New headless UI219, session138 and unchanged legacy UI65/researchUI333 pass.
  Its 128 stored-placement limit is **not** a native-game allocation guarantee.
  Aggregate research export is now connected for a saved schema-2 document with
  two retail materials and no placements. Other shapes remain refused; it never
  exports an inactive legacy file or only the first object instead.
- The fresh-level texture backend can source two individually selected static
  materials from the original game. Textured disc composition passes source
  checks and six complete-image/inverse comparisons against an independent raw
  oracle. The window now publishes a bounded saved two-material candidate;
  texture painting and mixed texture/Wizard export remain unavailable. A separate exact
  two-material candidate has now been published and played: both textures
  appeared, and movement, edge fall/respawn, jump/landing and flame effects were
  observed at normal speed. See the [textured-floor test](native-fresh-textured-runtime-r01.md).
- The whole-level document now preserves two material selections through edits,
  undo/redo and save/reopen. It labels the schematic honestly as unrendered; a
  material picker now passes100 headless checks, including both ordered slots
  and whole-form edit/save protection. Actual saved-material compilation
  passes five complete-image goldens, including ordered donors and edited
  heading/music. Saved aggregate publication is now source-tested, and the
  preview decoder matches64 independent source-image references. All eight saved
  publication/failure cases and independent readback now pass. A candidate from
  the saved14/T1+70/T1, heading128/music20 document passed normal-speed native
  entry, jump/landing, flame, movement and fall/respawn. See the
  [saved-material gameplay report](native-fresh-saved-material-runtime-r01.md).
  The window now shows explicit source thumbnails, with75 actual-source UI checks
  passing alongside cancellation/stale-result and bitmap-cleanup tests. The same
  saved-level publisher is now connected to the existing research button. An
  actual Save followed by that action produced the expected complete BIN, and
  a separate process verified all output bytes and the saved project binding.
  Late cancellation and a simulated UI cleanup error did not erase its real
  publication result. That exact new output now also passed native jump/landing,
  flame and edge-fall/return checks, with both settings files restored and all
  nine protected hashes unchanged. See the
  [new UI-output gameplay report](native-fresh-saved-ui-runtime-r01.md).
  These controls are in the separate R05 package, not the preserved R04. See the
  [connected export report](native-fresh-saved-research-window.md),
  [source-thumbnail window](native-fresh-material-inspection-window.md) and
  [material-picker checkpoint](native-fresh-saved-material-picker.md).
- Two Wizards imported into an existing level previously passed a bounded
  gameplay sequence: attacks, separate rewards, repeat defeat without a
  duplicate reward, and ordinary portal re-entry preserving collected states.
  See [that separate runtime report](native-two-wizard-r01-runtime-observations.md).

## Which editor can I test now?

The newest separate build is **Spyro Fresh Level Materials Test r05.app**, from
checkpoint71fd499. It includes the material picker, source thumbnails, guarded
saved export, dropdown crash safeguard and small-window layout fix.
Package/signature, preservation, native launch-contract and packaged PNG checks
pass. Its first normal launch is waiting for you to allow Documents access;
native edit/save/reopen/export acceptance remains pending. No permission bypass
was used. See the [R05 app location and test checklist](native-fresh-level-mac-r05.md).

R05's research export supports a saved two-retail-material floor with no placed
Mobys. Placement editing exists, but mixed Wizard/material export remains
disabled. This is not the all-Moby/all-texture V6 editor. R03 and R04 remain
separate and unchanged; the older R04 dropdown crash is documented in the
[regression report](native-fresh-readonly-selection.md).

The preserved private R03 app is here:

`/Users/john/Documents/Codex/2026-07-13/i-needed-to-make-a-new/spyro1editor-v3-release/_local/research/fresh-document-mac-test-r03/Spyro Fresh Document Test r03.app`

It is currently closed and remains built from checkpoint6eae8d4. Later backend
changes were not silently rebuilt into it. See [R03 package status](native-fresh-document-mac-r03.md).

In R03, open **Fresh Level…** to test:

1. Change floor/spawn coordinates, heading, floor/sky colors or music selection,
   then click Apply. The preview is schematic, not a gameplay renderer.
2. Try Undo/Redo, then Save document and Reopen saved. Save validates and applies
   the complete form before writing; invalid text is retained without replacing
   the valid preview.
3. Close/reopen the window to check persistence. The current R03 saved file is
   2048×2048 with heading96 and Track18. Its original retained seed used Track02;
   the current edited file is preserved, not reset to that seed.
4. Check validation: on that existing floor, spawnX8192 lies on its boundary and
   should be refused by Apply without replacing the valid document.
5. Browse Objects → Browse Native Library… for searchable native inventory.
   Inventory visibility does not mean every object is safely importable.

Create research candidate… is a separate saved-document export, not the main
Create BIN button. Its production worker and independent readback were tested;
R03 GUI publication still needs its own hands-on acceptance. Always use a new
output location and preserve the original game.

R03 has no placed-Moby controls or texture painting in the fresh document, and
music selection has no audio preview. An edited floor from this pipeline has
already been played with walking, flame, jump/landing, fall/respawn and normal
speed idle; see [edited-floor gameplay](native-fresh-edited-r01-runtime-observations.md).
The corrected Wizard ROM above is a separate research candidate, not an R03
feature.

## Remaining work toward V6

The separate R05 package is built and verified. Immediate parallel tracks are
native testing after Documents consent, and mixed Wizard/material composition.
The authored Wizard source matrix completed72 recipes across46 music selections;
that is complete-image/initial-footprint evidence, not72 gameplay tests. See the
[issued-matrix report](native-fresh-wizard-issued-matrix.md).
The new saved-aggregate backend holds the actual final project/document guard
at publication. Physical interrupted-export checks and the guarded App action
are now tested. This does not enable Wizard/material mixtures. A separate
original-only mixed Wizard/material check fits three selected material pairs
while preserving enemy and sound-transfer memory. The actual code now matches
all three resource results. The next source-tested step now retains the whole
Wizard/material document and refuses older export paths that would lose its
textures. Connecting that mixed document to a complete test ROM, binding the
saved revision and checking it in game remain next. These backend components
are not in R05. See the [material ancestry checkpoint](native-fresh-schema2-material-ancestry.md)
and [mixed-resource checkpoint](native-fresh-mixed-resource-component.md).

Universal importing still requires each object's behavior, effects, sound,
collision, animation, private/shared state and resource needs to remain correct
in each destination. A model catalog alone cannot provide that. Multiple
editable new-level slots, normal portal integration, save/reboot compatibility,
animated textures and arbitrary object/material mixtures remain open.

Latest completed all-project Release gate: **335 checks, zero failures**, including
the public saved-level API, source-thumbnail window, Mac dropdown regression,
shared GPU allocation, mixed Wizard/material resources and document ancestry, connected saved research export,
layout, background inspection worker, raw picker, initial footprint
and prior texture publication/failure-recovery controls. Fixed-recipe exporter
checkpoint: f8b382c. These latest source
changes are not in the preserved R03/R04 apps.
These are engineering
checks, not335 gameplay runs. Texture inspection is now connected to the window
and separately passes actual-source and cancellation tests. The [engineering handoff](native-editor-resume-20260906.md)
and [V6 readiness assessment](v6-readiness-20260906.md) retain the detailed
boundaries and next steps.
