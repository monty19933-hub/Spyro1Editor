# Saved-project export and native authored-level run, 2026-09-30

The private copy of the user's actual project contains five Artisans removals,
eight imported objects, eight terrain texture edits, appended private texture 68,
and a fresh level with two floor triangles and one native-colour dragonfly egg.
Old research documents were preserved separately.

The normal Create BIN/CUE dialog successfully produced both Artisans-start and
authored-start outputs. Independent readback verified all eight object identities,
physical and logical save slots, coordinates and rotations; all eight terrain
paint assignments against their source preimages; the private texture count 69;
and the fresh-level geometry and native-colour egg class 0x1E9. Original native
row 159 remains the Ambient Sound object (0x11E), despite a legacy import UI label
that also used 159. Both outputs preserve identical level bodies and overlays;
only the two guarded New Game instructions differ. The cancel case preserved the
previous output and old research documents.

Normal-dialog fixture evidence is in
`work/v1-continuation/verification/v1-fixture-r2-resume2-run.log`. Frozen outputs
are in `work/v1-continuation/verification/artifacts-r2-resume2`.

## Native run of the actual saved fresh level

Authored-start BIN SHA-256:
`31fb132654f637e78dff742ae5d30d204ae2b921f0886caa8362a2ba65f65ee1`.

A frozen copy booted from a cold isolated DuckStation process, real USA BIOS,
software renderer, empty private card, and the native New Game menu. No save
states, position assistance, or RAM/code writes were used. The game reached
level 65 and the user's authored spawn. Normal controller input demonstrated:

| Observation | X | Y | Z |
| --- | ---: | ---: | ---: |
| Settled spawn on the Z 544 floor | 560 | 560 | 566.25 |
| Cross jump | 560 | 560 | 633.375 |
| Native landing | 560 | 560 | 566.4375 |
| Rightward walk on floor | 586.4375 | 559.125 | 566.3125 |

The next controller approach collected the egg. The native inventory visibly
showed **Untitled Level 1/1** and the global dragonfly-egg total **1/5**, reflecting
the four eggs retained in Artisans and the one in the fresh level. No added eyes
were visible on the egg model or inventory icon. The approach subsequently
overshot the small authored floor's edge; this is recorded rather than hidden.
The run did not exercise a native card save/reload after this fresh-level pickup.

Evidence relative to the task workspace:

- `work/v1-continuation/runtime/saved-game-baseline-launch.json`
- `work/v1-continuation/runtime/saved-game-baseline/candidate.cue`
- `work/v1-continuation/runtime/saved-game-authored-observations.jsonl`
- `work/v1-continuation/runtime/Spyro V1 SavedGame Runtime.app/Contents/MacOS/screenshots`

This proves the exact normal-export authored startup, floor collision, native
movement and inventory pickup. It does not certify arbitrary user geometry,
all imported enemy behaviors, or whole-campaign progression.
