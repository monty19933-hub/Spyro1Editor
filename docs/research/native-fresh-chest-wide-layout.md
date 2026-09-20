# Source-owned chest clearance and wider-floor experiment

September20 continuation; internal research only. The failed R01 and preserved
R06 editor remain unchanged. No wider-floor runtime acceptance is implied.

## Root cause and policy

The failed R01 placed Spyro inside the initial native AE sphere. The new
`NativeFreshLockedChestSpawnClearancePlan` obtains the model/frame/descriptor,
native query premises and actual placed coordinates from retained source
owners. It checks both the initial3D separation and a fixed-XY landing column,
plus strict floor containment for the600-radius chest and356-radius player.
This is deliberately not an actual motion, full-family footprint or gameplay
certificate. Returned evidence/bytes cannot mutate the retained owners.

Moving the chest to9888/9216 on the original2048 floor clears that column, but
leaves only8 units beyond the chest's edge and no supported front-side approach
with the player's disk inside the floor. The source yawDC and native chest
collision stay intact; neither disabling collision nor changing player motion
is an acceptable layout correction.

R02 instead uses a3072 square, still inside one existing4096 collision cell:

| Owner | XYZ / geometry |
| --- | --- |
| Floor |8448/8448/8704,3072×3072|
| Player spawn |8960/8960/9216, heading64, unchanged|
| Wizard route |9984/9984/8704 to9472/9984, unchanged|
| Grass |9152/9152/8704, unchanged|
| Key |9728/9120/8704|
| Chest |10864/9888/8704, source yawDC unchanged|

The five reward-controller centers retain the complete original relative
pattern. All36 row/private coordinate words are independently constructed.
The chest's minimum floor-disk clearance is56; the reward centers have minimum
403 to the floor edge. A conditional route from spawn through the key, then
9728/8864 to10864/8864, separates the static chest/player disks and approaches
the source-facing side. It excludes actual enemy interference, motion and
emitted-gem trajectories. Authored spawn Z is512 above the floor: native chest
opening still requires landing and the other native key/state/distance gates.

## Geometry and material ownership

The neutral single-floor/shared geometry kernel accepts at most3072; schema4
tiles explicitly retain their existing2048 cap. Twelve independent literal
LP/HP/collision word changes predict the complete widened data through the
8C8,8FC andA6C owners. Non-geometry bytes, actual materials, audio, models,
GPU transfer payload, native Host and initializer remain unchanged.

`CaptureSameLayoutMaterialAuthoredAsync` is a separate internal scene/material
producer. It retains actual canonical document and ordered material/source
ancestry. The old exact separated-B producer and serialized identities remain
distinct. An actual-owner family validator narrowly admits the3072 recipe
above; neutral shape validation alone cannot issue a native family executable.
The normal editor's existing saved-publication limits are kept separately from
the wider internal geometry grammar. No normal Create BIN support is added.

## Evidence so far

Logs are under `_local/research/v6-mixed-export-20260912-r01.jR0oBr`.

- Geometry RED498 fails at the old2048 cap; GREEN500 passes1374 checks with a
  zero-warning/error build, including literal bounds, masks and complements.
- Named material RED499 fails on the absent APIs. GREEN502 passes251 free
  checks after replacing a stale enum-count test with an exact four-value list.
- Frozen source503 passes9672 final assertions and preserves Original BIN/CUE.
  The new mixed data hash is `cc8d9503d511c780ee6e583f0109f106b8f634eb8ff58f07283b8b8790a0e05b`;
  base mixed Body is `278da8d32d679692dc8be435aabbf9203b34c2b83df57d502cacddc860c017e8`.
  Old complete-image goldens, failed R01 image and complete inverse still pass.
- Actual CLI refusal504 rejects R01's known overlap before creating any output
  directory. Source-only reproduction remains available for diagnosis.
- Observer505 passes11 offline tests with no connections/ROM reads. Explicit
  R01/R02 annotations share unchanged bounded reads/gates; selecting a profile
  is not authentication. External process/open-disc identification is required.

The source-only mode `--chest-wide-source ORIGINAL_BIN ORIGINAL_CUE` preserves
the exact R02 recipe. The historical writer
`--chest-wide-experiment ORIGINAL_BIN ORIGINAL_CUE NEW_DIRECTORY` is now retired
after the observed escaped reward: it refuses before source I/O or directory
creation. Its preserved artifact was created after static landing clearance,
complete actual readback/inverse and final Original/CUE checks, with receipt and CUE last.
The key-zero treasure premise remains experimental. Build506 passes with zero
warnings/errors. Actual R02 output507 passes392 assertions, including complete
readback and inverse; scene hash is `6cda54db346a3fc7a4ae4338c885828d3a4759b4767872038b43bae3ede50092`.
Whole BIN is `0e1120a8f9a4c79ea1011bd63147892b10c7e8502204ef5e1038b652285d3998`;
physical lengths/1297 sector owners and the experimental executable remain
unchanged. The isolated artifact is
`_local/native-v6-chest-runtime-20260920-r02.hpMyEW/candidate-r02/fresh-chest-p9-experiment-r02.cue`.
Full regression508 passes343 command steps, zero failures, including both
saved-export UI paths and the larger-floor refusal tests. Final source510
passes9676 assertions and511 passes2355 checks; both preserve Original BIN/CUE.
The new full-firmware DuckStation session succeeds at stable landing, key
pickup, native chest opening, effects and nine of ten treasure points, but the last
red gem escapes the floor and falls. R02 therefore **fails complete reward
collection**, not export serialization or initial spawn. See
[the runtime record](native-fresh-chest-r02-runtime.md). Both original INIs
were restored byte-exact and the original card/BIN/CUE remain unchanged.
