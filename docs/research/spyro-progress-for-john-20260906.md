# Spyro editor — what is working now

Updated September 8, 2026 after the corrected fresh-Wizard DuckStation test.
Development continues with the requested pause near 5% usage **remaining**.
This is a development checkpoint, not a V6 release. Original game/save files and
the preserved R03 editor have not been replaced.

## New result: a blank level with a working enemy appeared in game

The corrected, separately published test ROM loaded the authored teal floor,
brown sky, Spyro, Sparx and the imported Wizard. Spyro descended vertically and
landed at the saved spawn without the earlier sideways launch. The Wizard then
attacked; health decreased from 3 to 2 to 1 while Spyro remained on the floor.

Testing stopped when Sparx disappeared and the strict observer refused its
now-null pointer. Original native source supports removing Sparx at nonpositive
health. The refused sample stays refused; a separately tested diagnostic change
is required before observing that state again. This is not evidence of a crash,
player death, successful re-entry or full gameplay completion.

This run used controlled 10% emulation speed. The earliest observed normal
update was timer2, not the first native update. Enemy defeat/reward, normal-speed
gameplay, death/return, sound and resource limits remain separate checks.
See [publication and closed runtime evidence](native-fresh-separated-wizard-publication.md).

Both DuckStation settings files were restored exactly after quitting. Original
BIN, CUE and memory card, the three candidate files and emulator executable
matched their pre-test hashes. No original card was replaced.

## Other working foundations, not yet packaged into R03

- A separate whole-level document preserves floor settings and an ordered object
  list with stable IDs. Its first supported placement type is a Wizard route.
- Explicitly forking a saved floor into that document preserves the legacy file;
  the two documents do not silently synchronize.
- The new App-side document session passes actual save/reopen, raw-file conflict,
  selection and whole-document undo/redo tests, including 128 stored placements.
  That storage capacity is **not** a native-game allocation guarantee.
- The fresh-level texture backend can source two individually selected static
  materials from the original game. Textured disc composition and its tests are
  under development; texture painting and mixed texture/Wizard export are not
  yet available in the window.
- Two Wizards imported into an existing level previously passed a bounded
  gameplay sequence: attacks, separate rewards, repeat defeat without a
  duplicate reward, and ordinary portal re-entry preserving collected states.
  See [that separate runtime report](native-two-wizard-r01-runtime-observations.md).

## Which editor can I test now?

The preserved private R03 app is here:

`/Users/john/Documents/Codex/2026-07-13/i-needed-to-make-a-new/spyro1editor-v3-release/_local/research/fresh-document-mac-test-r03/Spyro Fresh Document Test r03.app`

It is currently closed and remains built from checkpoint6eae8d4. Later backend
changes were not silently rebuilt into it. See [R03 package status](native-fresh-document-mac-r03.md).

In R03, open **Fresh Level…** to test:

1. Change floor/spawn coordinates, heading, floor/sky colors or music selection,
   then click Apply. The preview is schematic, not a gameplay renderer.
2. Try Undo/Redo, then Save document and Reopen saved. Only applied values are
   saved; typed-but-unapplied fields are not the document.
3. Close/reopen the window to check persistence. The existing saved floor is
   2048×2048 with heading96 and Stone Hill Track02.
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

The immediate parallel tracks are placement controls in the existing Fresh
window, the textured-disc path, and the next fresh-Wizard lifecycle playtest.
Saved aggregate export also needs a final project/document lease, so a valid
draft cannot be exported against a different saved revision.

Universal importing still requires each object's behavior, effects, sound,
collision, animation, private/shared state and resource needs to remain correct
in each destination. A model catalog alone cannot provide that. Multiple
editable new-level slots, normal portal integration, save/reboot compatibility,
animated textures and arbitrary object/material mixtures remain open.

Latest completed all-project Release gate: **319 checks, zero failures**, before
the current session/textured-disc changes. These are engineering checks, not
319 gameplay runs. The [engineering handoff](native-editor-resume-20260906.md)
and [V6 readiness assessment](v6-readiness-20260906.md) retain the detailed
boundaries and next steps.
