# V6 R10 / beta47 packaged checkpoint and bounded runtime

September28. Built from clean462b6c5. Full source Release gate288/0 is in
`/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-dh_7ytiz/summary.json`.
Separate Mac ARM64/Windows x64 package verification passed. Windows runtime is
untested; Mac signing is private/ad-hoc, not Apple signing or notarization.

Exact preserved directory:
`/Users/john/Documents/Codex/2026-07-13/i-needed-to-make-a-new/spyro1editor-v3-release/_local/v6-private-preview-20260928-r10.dAVQmg`.
Read its `PRIVATE-PREVIEW.md` and `PACKAGED-UI-SESSION.md` before testing.
Its launcher validates build47 and uses an independent copied test project;
R06/R07/R08/R09 and normal user projects are not replaced.

## What was verified in the packaged Mac application

- Real Stone Hill -> Toasty selection now enables flight checkbox/Save/Revert.
- Never-saved OFF, changed ON draft, and Create BIN refusal until Save/Revert.
- Saved ON -> normal Create BIN; an unsaved OFF draft was discarded at Quit.
- Fresh instance showed saved ON/checked; Save OFF -> normal full-source rebuild.
- ON/OFF outputs are preserved separately; OFF BIN equals the original USA BIN.

Fly Anywhere remains level14-only without ordinary saved edits; no destination
or compound-export gate was widened. Save Flight owns editor-project intent,
not a new memory-card flag. The third fresh-instance explicit-OFF visual check
was interrupted by the accessibility failure below and is still pending.

## New concrete failure — preserve before repair

R10's level ComboBox advertises AX value setting. An automation SetValue("Toasty")
throws NotSupportedException in Avalonia's ComboBoxAutomationPeer, escapes
the native value-provider callback and terminates the editor (session14218,
exit134). No project edits were made in that instance. Source repair must be
narrow and regression-first; do not globally swallow UI exceptions or convert
arbitrary text to level selections. R10 remains an unchanged diagnostic build.

## Exact hashes

| Artifact | SHA256 |
| --- | --- |
| Mac ZIP,82823881 bytes | 99f6b5e2bf81985a80ff715d5b34967e80bd91289684459ac28fe90395999227 |
| Windows ZIP,85180905 bytes | fe9acb906ed21adbab6f252222ba876f37743dfe39a1c1961cff830e69e17be8 |
| Packaged App DLL | a7ec1a39161a6096f15d3f07fb45109a7541998892da4c5a75815e22872863c4 |
| Packaged Core DLL | 8ce3941ecfe11e6f47cdc5d5f66525080dfdc329e22bb7ed75e6decb8e6a3696 |
| Normal ON BIN | c0b30e5354b1050d59a79593f54fc9e014c12819213bd719fc0ab5d42493d87d |
| Normal OFF BIN / original USA | fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37 |
| Both normal CUEs | 66728dda265e6fc522792620193a1c3f9422134bc61611aad5cf947c0332ced4 |

## Exact-disc runtime boundary

Closed run:`_local/native-flight-normal-runtime-20260928-r01.TJELiS`.
The exact normal ON CUE cold-booted with real SCPH1001 v2.2 BIOS, Interpreter,
no fastboot/HLE/directEXE/resume/savestate/debugger. At displayed100% speed it
loaded the copied native slot0 into Stone Hill and returned through native Exit
Level to Artisans; ordinary movement and charging worked. Toasty was not reached.
No flight ON/OFF behavior or native Save Game/cold-reload acceptance follows.
All normal DuckStation settings/game-settings/card hashes matched after exit;
the disposable card retained its seed unchanged. Session note has exact paths,
hashes, screenshot, input preset and restoration receipt.

The all-known browser and four-tile edit/undo/save/reopen/export remain supported
by separate R09 GUI/readback evidence, not a replayed R10 result. Japanese eggs
and signs still lack normal placement UI, durable custom native save, X/X and
required-for-100% consumers. The IRQ controller remains detached; see the
[unimplemented scheduler integration plan](collectible-irq-scheduler-successor-plan.md).
