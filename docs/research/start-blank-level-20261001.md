# Start Blank implementation and native verification — 2026-10-01

## Scope and preservation

The prior universal release is preserved at commit `0f0cdb623446e2f36720ac4b6fbd60f03601e879` and branch `checkpoint/universal-1.0.1-20261001`. Continuation uses `feature/start-blank-level-20261001`. Original USA/Japanese discs, the user project, earlier packages and cards remain separate from disposable test fixtures. Fly Anywhere remains deferred.

The workflow keeps the real level identity, confirms the clear, saves the current work, writes a hash-checked recovery snapshot, and replaces the scene with a safe spawn and removable starter floor. Empty drafts save/reopen; export requires complete support beneath the player's footprint. Restoring the before-clear scene also preserves the current replacement in recovery storage. Append-only retired collectible identities survive clear/restore independently of reusable physical rows.

Ten retail initializers have exact owned 152-byte bindings: Artisans10, StoneHill11, TownSquare13, PeaceKeepers20, DryCanyon21, CliffTown22, MagicCrafters30, BeastMakers40, DreamWeavers50 and GnastyWorld60. The other twenty ordinary levels have short initializers whose missing transition callbacks are not proved safe for arbitrary imported dependencies. They remain refused. Cleared Town Square and the separate fresh65 scene are refused together because they share native initializer storage.

## Export and resource accounting

Normal Create BIN aggregates every saved cleared scene, the saved fresh scene and ordinary saved retail edits. Original donor records remain the source even when importing a Moby from the very level being cleared. Actual terrain, collision, Moby rows, class/model/texture tables and goals are rebuilt. All 256 archive entries are independently read back before paired BIN/CUE publication. Source-alias guards protect both temporary and final paths. Failed preparation or changed input leaves the previous output intact.

The measured pre-subdivision Artisans fixture had 174 original placed rows, zero after clear and two after adding replacement actors. Its body fell from3,682,304 to2,078,720 bytes, or2,203,648 for the two-actor replacement. Total resident allocation fell by895,324 bytes for blank and772,524 bytes for that replacement. These figures are tied to the exact receipt, not every possible scene; final terrain subdivision adds a small measured allocation. See `work/start-blank-verification/source-native-release-r2/capacity.json` in the task workspace.

The retained kernel contains the Town engine overlay, Spyro, Sparx, the shared sound bank, particle support, auxiliary data and PETE structures. Unowned texture bytes are cleared. This is a practical reduced kernel, not a proof that every retained byte is indispensable. Universal imports still have genuine RAM/VRAM/dependency limits and finite saved-state identities.

## Rendering and collision findings

Native execution found authored floors could disappear after movement although collision remained. Each HP face had aliased reserved scratch index zero. Retail source census covered35 levels and194,933 HP faces; their cache ranges partition1..N. Authored faces now have distinct native cache indices. The large untextured direct-render path also bypasses clipping. Untextured faces now generate an exact integer lattice with axis extents at most64; editor surfaces remain unchanged and render/collision share the generated triangles. Near/far corner colours are interpolated. The255 generated-triangle ceiling and exact-coordinate guards apply before publication. Textured faces use the proven native clipping flag; untextured materialFF keeps its separate direct-packet flag.

Review also corrected collision Z encoding: unlike signed XY deltas at14/23, native Z uses unsigned offsets at16/24 from the lowest corner. A cyclic rotation retains face winding; independent decoding must match rendered vertices for slopes.

## Native evidence

All runs use isolated DuckStation roots, Software renderer, Recompiler CPU, full NTSC-U firmware SHA256 `71af94d1e47a68c11e8fdb9f8368040601514a42a5a399cda48c7d3bff1e99d3`, new copies of the blank card and ordinary controller input. RAM captures are read-only. No save states, memory writes or position assistance are used. Exact candidate paths, hashes, card hashes, observations and screenshots are in `work/start-blank-level-20261001/runtime/*-native-receipt.json`.

- Initial blank Artisans R1 cold-loaded with zero placed rows, the new floor and safe spawn. Jump/landing, fall/respawn, flame and inventory0/0 passed. This preceded final mesh changes and is retained separately.
- R3/R4/R6 are diagnostic failures: large floors lost visibility; R4 isolated the issue without an enemy. They are not accepted runtime examples.
- R5 is inconclusive because a directional toggle was inadvertently left held during menu navigation. It is not accepted traversal evidence.
- R7's manually generated50-unit grid plus corrected cache passed a controlled walk to rawXYZ[8961,10169,9061], jump/landing, gem collection and inventory1/1. An earlier longer movement left the small platform and respawned; it is recorded separately.
- R8 used a genuine Artisans texture with native flag0. A controlled walk reached rawXYZ[8960,10175,9069]; the floor remained visible through jump and landing, with the gem collected. This was a private diagnostic, followed by production implementation.

Final production-native results and release receipts are recorded below after verification. These bounded examples do not certify an entire campaign, every level initializer or every Moby variant. The previous universal matrix remains105,559/145,285 statically composed cells; all matrix runtime cells remain not-tested.
