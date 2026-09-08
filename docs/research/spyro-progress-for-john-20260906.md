# Spyro editor — what is working now

Updated September 8, 2026 after textured gameplay and fresh-Wizard reward testing.
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
  Aggregate export is still disabled until the actual saved revision is bound
  through final publication; it never exports an inactive legacy file instead.
- The fresh-level texture backend can source two individually selected static
  materials from the original game. Textured disc composition passes source
  checks and six complete-image/inverse comparisons against an independent raw
  oracle. Physical textured candidates, texture painting and mixed
  texture/Wizard export are not yet available in the window. A separate exact
  two-material candidate has now been published and played: both textures
  appeared, and movement, edge fall/respawn, jump/landing and flame effects were
  observed at normal speed. See the [textured-floor test](native-fresh-textured-runtime-r01.md).
- The whole-level document now preserves two material selections through edits,
  undo/redo and save/reopen. It labels selections honestly as unrendered; a
  material picker now passes100 headless checks, including both ordered slots
  and whole-form edit/save protection. Actual saved-material compilation
  passes five complete-image goldens, including ordered donors and edited
  heading/music. Saved aggregate publication is now source-tested, and the
  preview decoder matches64 independent source-image references. Physical
  publication tests and the preview's window integration are next; the
  successful gameplay candidate still uses a fixed recipe. These new controls
  are in source, not yet in the preserved R04 package. See the
  [material-picker checkpoint](native-fresh-saved-material-picker.md).
- Two Wizards imported into an existing level previously passed a bounded
  gameplay sequence: attacks, separate rewards, repeat defeat without a
  duplicate reward, and ordinary portal re-entry preserving collected states.
  See [that separate runtime report](native-two-wizard-r01-runtime-observations.md).

## Which editor can I test now?

The newer, separate **R04 whole-level test editor** has now been built from
checkpoint60f6d6f. Package/signature, source preservation and packaged PNG checks
pass. Its earlier normal launch reached macOS Documents permission; the launch
helper has since exited and native edit/save/reopen acceptance remains pending.
No permission bypass was used. See the
[R04 app location and test checklist](native-fresh-level-mac-r04.md).

R04 adds **Level aggregate → Fork saved legacy floor** and Wizard-route
placement controls. Aggregate export remains disabled; this is not yet the
all-Moby/all-texture V6 editor. Your R03 copy remains separate and unchanged.

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

The immediate parallel tracks are saved whole-level publication, actual source
texture previews, and native validation/packaging of the placement window.
The authored Wizard source matrix completed72 recipes across46 music selections;
that is complete-image/initial-footprint evidence, not72 gameplay tests. See the
[issued-matrix report](native-fresh-wizard-issued-matrix.md).
The new saved-aggregate backend holds the actual final project/document guard
at publication. Physical interrupted-export checks and the App integration
remain to be completed before enabling that action in the window.

Universal importing still requires each object's behavior, effects, sound,
collision, animation, private/shared state and resource needs to remain correct
in each destination. A model catalog alone cannot provide that. Multiple
editable new-level slots, normal portal integration, save/reboot compatibility,
animated textures and arbitrary object/material mixtures remain open.

Latest completed all-project Release gate: **331 checks, zero failures**, including
the saved-level Candidate and export-failure test controls, background texture
inspection worker, source-preview decoder, raw picker, initial footprint
and prior texture publication/failure-recovery controls. Fixed-recipe exporter
checkpoint: f8b382c. These latest source
changes are not in the preserved R03/R04 apps.
These are engineering
checks, not331 gameplay runs. Texture inspection has passed actual source-file
and cancellation tests but is not yet connected to the window. The [engineering handoff](native-editor-resume-20260906.md)
and [V6 readiness assessment](v6-readiness-20260906.md) retain the detailed
boundaries and next steps.
