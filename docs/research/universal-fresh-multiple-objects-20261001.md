# Fresh levels with multiple imported objects

The packaged editor retake found that adding a second object to a fresh level
failed with only 15 dynamic slots remaining. The original canonical fresh scene
reserved 16 dynamic records and one 88-byte placed record. Further placed rows
were consuming that dynamic reserve.

`NativeFreshPlacementArena` authenticates the canonical empty scene, then adds
88 bytes per additional active placement before the empty properties component.
The component sizes relocate the empty properties, pods, collision buckets and
fixups together. The normal composer still checks its final 16 dynamic slots,
actual memory use and save identities. Retired placements consume no physical
storage. Retail scenes do not use this preparation.

At source commit `3785f227`, Release and Debug each passed 2,067 source-free and
2,088 source/readback checks. These include every placement count from 0 through
256, malformed canonical refusals, the old two-object failure as a negative
control, unchanged zero/single-placement output, two real Alpine Ridge imports,
animation 23, final memory accounting and written-disc readback. The complete
source-free Release regression passed 318 checks with zero failures in a separate
frozen source tree. This is separate from the earlier 316-check receipt.

## First native six-object retake

A private copy of the saved two-face level retained its original spawn and egg.
Five Jacques gems were added near the spawn: source rows 59, 56, 64, 107 and 40,
with native values 1, 2, 5, 10 and 25. Normal `ExportAsync` produced a BIN with
SHA-256 `79a9fc8f09f6ceb9d8df499c61ca39a648ba1579ece3deab517bdbecbd1645b2`.
Independent readback passed 286 checks: six physical/logical rows, exactly 440
additional arena bytes, 16 dynamic slots, unchanged geometry/spawn, and unchanged
unrelated WAD entries. Actual final RAM headroom was 943,356 bytes.

Cold New Game with the real USA BIOS entered slot 65. Controller-only jumping,
landing and a short walk worked. All five gems awarded 43 globally and in level
index 35. The egg collected and showed 1/1; all six durable and checkpoint bits
were set and all six rows reached native state FD. No RAM writes or save states
were used. This fresh fixture did not undergo card save/reload.

That first native retake also caught two release defects: the inventory used
an incorrect imported treasure target, showing 43/5, and large rotating collection
sparkles persisted beyond 84 seconds. It is not accepted as the final visual or
progression result. The first candidate, RAM captures and screenshots remain
preserved for comparison with the corrected retake.

Direct executable readback subsequently confirmed the original and issued fresh
target is zero. The five-point maximum was introduced by counting rows after
their class IDs had been remapped; it was not an inherited bootstrap target.

Private evidence is under the task workspace at
`work/universal-v1-continuation/runtime/fresh-six-objects`,
`fresh-six-objects-native-first-receipt.json` and the corresponding RAM/image
captures. The preserved broad regression is under
`work/universal-v1-continuation/verification/regression-fresh-arena`.
