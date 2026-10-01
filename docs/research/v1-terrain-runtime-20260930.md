# V1 terrain continuation: native checks, 2026-09-30

These checks exercise the restored existing-level terrain writer. They do not
certify arbitrary topology, all levels, or a complete campaign. Original source
discs, the user's live project, earlier packages and cards remain unchanged.

## Runtime setup

The isolated `work/v1-continuation/runtime/Spyro V1 Runtime.app` booted each exact
candidate CUE from a cold process, using the real USA BIOS (SHA-256
`71af94d1e47a68c11e8fdb9f8368040601514a42a5a399cda48c7d3bff1e99d3`), software rendering
and a new private card. Native New Game reached Artisans, level 10. No save states
were loaded. The debugger was used for explicitly identified position assistance
in the first hillside check and read-only measurements in the second check.

## Existing hillside with changed collision lookup cells

Candidate: `work/v1-continuation/terrain-native-candidate/artisans-cell-crossing.cue`.
BIN SHA-256: `b044eed55c77fda60e9648af0b566445fbe59194b8b86bc0c2c36ead40418240`.

Artisans face `2:64:hp`, point 4, changed from `(6951,3421,760)` to
`(6951,3421,768)`. The ordinary terrain exporter emitted one HP vertex patch,
six collision triangle patches and the collision lookup tree/block patches.
All nine patches appeared byte-exactly and uniquely in loaded game RAM.
The independent index audit confirmed all six triangle memberships, the three
changed cells, unchanged active triangle identity set, retained native query order
for common/unaffected rows, and unchanged assignment/flag bytes.

Two position-only assists placed Spyro above the steep hillside. Native physics
slid him downhill. This is loaded-data and collision-response evidence, **not a
standing/walking acceptance pass**. See the assistance and RAM receipts under
`work/v1-continuation/runtime` and the independent audit under
`work/v1-continuation/verification/terrain-index-independent.json`.

## Raised reachable floor: controller-only walk, jump and landing

Candidate: `work/v1-continuation/terrain-native-candidate/artisans-reachable-floor.cue`.
BIN SHA-256: `d2d2eee50fca344ab71fcb2e1a9d13f7d86f72c23d812bfba09bc2f289a90684`.

Artisans face `90:0:hp` spans X 5248–5376 and Y 3200–3328. All four floor
corners changed from Z 566 to 574; the nine affected HP faces were edited together
to preserve their shared corners. The exporter emitted four physical HP vertex
patches and fourteen collision triangle patches, with no skipped edits. This
particular candidate did not require an LP vertex patch or lookup-cell change.
All eighteen patches appeared byte-exactly and uniquely in loaded game RAM.

The test used only native controller input for gameplay, with **no RAM/code
writes or position assistance**. From the original spawn `(5312,2948.3125,597.25)`,
Spyro walked forward onto the raised floor and naturally rescued Nestor. After
the dialogue, he walked back onto the edited floor. The fairy interaction was
closed with Continue. A native Cross jump then returned to the same floor:

| Observation | Editor X | Editor Y | Editor Z |
| --- | ---: | ---: | ---: |
| Walking on changed floor | 5289 | 3253 | 596.5625 |
| Settled before jump | 5280.25 | 3309.75 | 596.5 |
| Native jump, airborne | 5280.25 | 3309.75 | 663.625 |
| Landed and settled | 5280.25 | 3309.75 | 596.5 |

Debugger measurements were taken while the emulator was paused; gameplay was
resumed through its normal pause shortcut. A preliminary continuous sampler
paused the emulator and is not used as jump evidence. The successful jump uses
the four explicit measurements above and visible native input/results.

Receipts, relative to the task workspace:

- `work/v1-continuation/runtime/flat-floor-launch.json`
- `work/v1-continuation/runtime/reachable-floor-observations.jsonl`
- `work/v1-continuation/runtime/artisans-reachable-floor-loaded-patches.json`
- `work/v1-continuation/runtime/Spyro V1 Runtime.app/Contents/MacOS/screenshots/Spyro the Dragon 2026-09-30-21-33-17.png`

RAM snapshot SHA-256:
`699ac9b010332e66489584c38297e213b55eac596425cd5055c9e209ec4979a4`.
This supports walking, jumping and landing on this exact raised retail floor.
Structural terrain addition/removal and the final combined user project require
separate acceptance checks.

## Added floor: native gravity, walking, jumping and landing

Candidate: `work/v1-continuation/terrain-topology-native-v7/artisans-floor-copy.cue`.
BIN SHA-256: `b2d7577488a781d9004753c3eea8fbea1f7d654ff1ff4688b6aba73ae018fb57`.
The copy of Artisans `90:0:hp` is translated +160 X, with native HP/LP geometry,
visibility and two new collision rows (7209/7210). Independent source/readback
checks retain all original geometry, collision assignments, flags, query order,
and opaque scene suffixes. This candidate predates the corrected HP-to-LP native
corner-order conversion: its distant visual surface is not accepted by this test. The copied sector belongs to every visibility group;
native frustum bounds still perform ordinary culling.

A cold real-BIOS New Game ran in the separate `Spyro V1 Copy Runtime.app`.
After a controller approach, one explicitly recorded **position-only assist**
placed Spyro above the added floor at `(5472,3264,700)`. Normal gravity landed him
on it. Ordinary Right input walked across the new surface. A native Cross jump
returned to the same position:

| Observation | Editor X | Editor Y | Editor Z |
| --- | ---: | ---: | ---: |
| Landed after position assistance | 5473.875 | 3262.125 | 588.3125 |
| Walking on added floor | 5492.9375 | 3264.5625 | 588.375 |
| Standing near its far edge | 5528.4375 | 3262.5 | 588.4375 |
| Native jump | 5528.4375 | 3262.5 | 650.5 |
| Native landing | 5528.4375 | 3262.5 | 588.375 |

The source ground at X 5504/5520, Y 3264 is around Z 424/422.6. Only the
candidate's added triangle supplies the Z 566 floor in that region, so the
walk is distinguishable from standing on nearby original terrain. No save
states, actor-state changes, collision writes or physics patches were used.
The assistance precludes calling this a controller-only approach test.

Receipts: `runtime/topology-copy-launch.json`,
`runtime/topology-copy-position-assist.json`,
`runtime/topology-copy-observations.jsonl`,
`runtime/topology-copy-height-comparison.json`, and
`verification/terrain-topology-v7-copy-independent.json`, all under
`work/v1-continuation`. Native screenshot:
`runtime/Spyro V1 Copy Runtime.app/Contents/MacOS/screenshots/Spyro the Dragon 2026-09-30-22-03-41.png`.

The earlier granular raised-floor and hillside candidates precede the final
coarse-distance terrain rewrite. Their evidence proves the native collision
behavior described above; it does not substitute for acceptance of later output.

The copied-floor RAM receipt `runtime/topology-copy-loaded.json` found the new
136-byte HP/LP sector at `0x800C0908`, its two appended collision rows at
`0x800DF09C`, and complete assignments/flag arrays byte-exactly. Original rows
0–243 differed at runtime; the whole collision array was therefore not reported
as an exact match. The added rows were exact. RAM SHA-256:
`045ca964a61a7133c9fd0053c189d0056769d7d99483ce007695c3e6b746c50a`.

## Final v23 raised floor and corrected distant geometry

Candidate: `work/v1-continuation/terrain-topology-native-v23/artisans-reachable-floor.cue`.
BIN SHA-256: `e7710d1298f9a6f0e44755d52483b426d51ffac7b6b6e131b0601e81b77b5d89`.
Unlike the earlier granular candidate, this version rebuilds the affected distant
surface with the independently authenticated native renderer corner order and
physical far-colour table. The source renderer's instructions were matched to the
USA executable by the independent packet verifier; source HP strip triangles and
resulting LP strip triangles agree, including winding and render flags.

A cold real-BIOS New Game in `Spyro V1 TerrainFinal Runtime.app` reached Artisans.
Controller input alone walked onto the changed floor and rescued Nestor. After
Continue closed the fairy menu, the final measurements were:

| Observation | Editor X | Editor Y | Editor Z |
| --- | ---: | ---: | ---: |
| Returned to changed floor | 5287.625 | 3262.625 | 596.5625 |
| Settled | 5282.125 | 3312.4375 | 596.4375 |
| Native Cross jump, airborne | 5282.0625 | 3312.5 | 663.6875 |
| Landed | 5282.0625 | 3312.5 | 596.5625 |

No runtime writes, position assists or save states were used. Read-only RAM
inspection found the exact final 2,076-byte sector 90 at `0x800AC278`, including
its HP and corrected LP geometry. All fourteen changed collision rows were
byte-exact in RAM, as were complete assignment and flag arrays. The original
runtime-mutated collision prefix prevents claiming the entire collision table is
byte-identical. RAM SHA-256:
`ef28ef41b3b4e11a5b87c170a6314626ddc4397a2917e3435710c1f65d4225fa`.

Receipts: `runtime/terrain-final-floor-launch.json`,
`runtime/terrain-final-floor-observations.jsonl`,
`runtime/terrain-final-floor.jsonl`, `runtime/terrain-final-floor-loaded.json`,
`runtime/terrain-final-floor-changed-collision.json`, all under
`work/v1-continuation`. A label `final-jump` at Z596.5625 records a press during
fairy dismissal; the subsequent `final-airborne` measurement is the successful
jump. The packet verifier establishes distant-face construction; this close-floor
gameplay run does not separately demonstrate a naturally selected distant LOD.

## Final v23 copied floor

Candidate `terrain-topology-native-v23/artisans-floor-copy.cue`, BIN SHA-256
`9df565cba641c82558cbea80f3561da92667e68266b5e2fab1339df9751ae8f3`,
was cold-booted in `Spyro V1 TerrainFinalCopy Runtime.app` using the same real BIOS
and a private empty card. One explicitly recorded position-only assist placed
Spyro above the copied floor at (5472,3264,700). Normal gravity landed at
(5472,3262.1875,588.4375); native Right input walked to
(5493.8125,3254.25,588.3125), then settled at (5531.375,3233.75,588.3125).
A native Cross jump reached Z655.4375 and landed at Z588.5 with the same XY.
The original surface there is far below the copied Z566 floor.

The exact final 136-byte sector 178 (including corrected LP corners/colours) was
found in RAM at 0x800C0908; both appended collision rows 7209/7210 were byte-exact
at 0x800DF09C. Complete assignments and flags were also exact. Original
runtime-mutated collision rows are excluded from any whole-table claim.
RAM SHA-256: `e89b7f8a8448648433611bc4fa338ce5057a90353b3e60de86d483b6163d3acc`.

Receipts under `work/v1-continuation/runtime`: `terrain-final-copy-launch.json`,
`terrain-final-copy-position-assist.json`, `terrain-final-copy.jsonl`,
`terrain-final-copy-loaded.json`. Screenshots are in the isolated app's
`Contents/MacOS/screenshots` directory (22:37:26 and 22:38:27 local time).
This proves the final added surface supports native gravity, movement and jump
collision after position assistance; no code, physics or collision writes and no
save states were used. Native packet readback separately validates distant
geometry construction.
