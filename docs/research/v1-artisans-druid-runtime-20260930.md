# Artisans source-variant capacity and native combat verification

The copied user project exported through the normal editor with the Wizard Peak
Armored Druid source row 18, class `0x11C`. Its nine imports, eight terrain paints,
private texture 68, fresh scene and original saved edits passed independent
readback. The capacity plan names four additional removals: native rows 32 and
33 (the remaining Regular Gnorcs), 94 (Life Chest), and 109 (another Gnorc).
These removals were applied only in a private test copy. The original project
was not changed, and the browser never applies its proposed removals itself.

The frozen BIN SHA-256 is
`fc7de87cd8ebba132220d2d2478ab925f0b51ca52ba114fcead4ae78e14eb81c`.
Its normal-dialog export receipt is
`work/v1-continuation/verification/v1-wizard-normal-export-receipt.json`.
Native physical row 124 contains the imported class; its saved logical identity
is separate from the reused physical row. Native level 10 contains 174 physical
rows after the removals and all nine imports.

## Clean native combat run

`runtime/wizard-combat-launch.json` identifies the exact frozen CUE, isolated
DuckStation application, real USA BIOS and private blank card. The BIOS SHA-256
is `71af94d1e47a68c11e8fdb9f8368040601514a42a5a399cda48c7d3bff1e99d3`;
the starting card SHA-256 is
`52e9972468ee7f0864c110c06dc13530b71c05e1669c9a4cbe7e8c26d91be220`.
This run used normal New Game and controller inputs, with no game RAM writes,
position assistance, executable hooks or save states. Debugger observations
were read-only while paused.

- The source-matching model appeared ahead of the Artisans spawn. Independent
  previews from the unchanged source show the same green, staff-bearing model.
- On approach, the enemy struck Spyro; the hit animation and stars appeared,
  and Sparx changed from gold to blue. The native player state was 14.
- Normal flame turned the enemy's armor pink. The enemy survived and returned
  to state 0; its native dropped flag remained `0x7F`.
- Normal charge defeated it. The model disappeared, native actor state became
  `0xFD` and the dropped flag became `0xFF`.
- Gameplay continued through Nestor's rescue and a normal walk to
  `(5371.5, 3127.9375, 580.0625)`, with the enemy still defeated.

The exact observations are `runtime/wizard-combat-observations.jsonl`.
Final RAM SHA-256:
`c8deab3cbd1c6ca92eb8d5170593208585c15d7b864922a54d6257f895da3ac5`.
This proves appearance and the observed attack/flame/charge behavior for this
source variant and project. It does not prove all variants, all levels, reward
accounting, card persistence or a complete playthrough.

## Separate diagnostic and failed variant

An earlier Wizard run used one recorded actor-position change solely to
identify the foreground model; that diagnostic is not included in the clean
combat acceptance above. The nearby Nestor statue and its rescue initially
confounded visual identification. Neither a separate carried shield nor a
different enemy identity should be inferred from those early observations.

The Alpine Ridge row 1, class `0x10F`, variant fit after two additional Gnorc
removals and passed normal export/readback, but its native flame interaction
produced severe geometry corruption and a stalled frame. Its failed BIN SHA is
`056698d2e6556302c2c8edbe784807f9cac2420390543bfc989818277b253a05`;
failure RAM SHA is
`ae2e1ed37a76b1d5cf71bc9498439e0dffa2319a511e94ac00e3f89ac93efba4`.
This is a specific runtime failure, not proof of a memory-capacity or model-
identity cause. Release composition refuses that exact source/destination
combination while preserving existing saved placements for correction.
