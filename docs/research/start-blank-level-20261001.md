# Start Blank implementation and native verification — 2026-10-01

## Scope and preservation

The prior universal release is preserved at commit `0f0cdb623446e2f36720ac4b6fbd60f03601e879` and branch `checkpoint/universal-1.0.1-20261001`. Continuation uses `feature/start-blank-level-20261001`. Original USA/Japanese discs, the user project, earlier packages and cards remain separate from disposable test fixtures. Fly Anywhere remains deferred.

The workflow keeps the real level identity, confirms the clear, saves the current work, writes a hash-checked recovery snapshot, and replaces the scene with a safe spawn and removable starter floor. Empty drafts save/reopen; export requires complete support beneath the player's footprint. Restoring the before-clear scene also preserves the current replacement in recovery storage. Append-only retired collectible identities survive clear/restore independently of reusable physical rows.

Ten retail initializers have exact owned 152-byte bindings: Artisans (10), Stone Hill (11), Town Square (13), Peace Keepers (20), Dry Canyon (21), Cliff Town (22), Magic Crafters (30), Beast Makers (40), Dream Weavers (50) and Gnasty's World (60). The other twenty ordinary levels have short initializers whose missing transition callbacks are not proved safe for arbitrary imported dependencies. They remain refused. Cleared Town Square and the separate fresh level 65 scene are refused together because they share native initializer storage.

## Export and resource accounting

Normal Create BIN aggregates every saved cleared scene, the saved fresh scene and ordinary saved retail edits. Original donor records remain the source even when importing a Moby from the very level being cleared. Actual terrain, collision, Moby rows, class/model/texture tables and goals are rebuilt. All 256 archive entries are independently read back before paired BIN/CUE publication. Source-alias guards protect both temporary and final paths. Failed preparation or changed input leaves the previous output intact.

The final Artisans capacity fixture had 174 original placed rows, zero after clearing, and two after adding replacement actors. Its level body fell from 3,682,304 to 2,082,816 bytes, or 2,207,744 bytes for the two-actor replacement. The overlay grew from 57,344 to 63,488 bytes (blank), or 77,824 bytes (replacement), and is included in the accounting. Total resident allocation fell by **891,216 bytes** for the blank level and **768,416 bytes** for that replacement. Dynamic object capacity is 16 instead of the original 91. These figures describe the exact final fixtures in `work/start-blank-verification/production-subdivided/source-release/capacity.json`, not every possible scene. The separate cyan native examples contain one collectible.

The retained kernel contains the Town engine overlay, Spyro, Sparx, the shared sound bank, particle support, auxiliary data and PETE structures. Unowned texture bytes are cleared. This is a practical reduced kernel, not a proof that every retained byte is indispensable. Universal imports still have genuine RAM/VRAM/dependency limits and finite saved-state identities.

## Rendering and collision findings

Native execution found authored floors could disappear after movement although collision remained. Each HP face had aliased reserved scratch index zero. Retail source census covered 35 levels and 194,933 HP faces; their cache ranges partition 1..N. Authored faces now have distinct native cache indices. The large untextured direct-render path also bypasses clipping. Untextured faces now generate an exact integer lattice with axis extents at most 64; editor surfaces remain unchanged and render/collision share the generated triangles. Near/far corner colours are interpolated. The 255 generated-triangle ceiling and exact-coordinate guards apply before publication. Textured faces use the proven native clipping flag; untextured material FF keeps its separate direct-packet flag.

Review also corrected collision Z encoding: unlike signed XY deltas at 14/23, native Z uses unsigned offsets at 16/24 from the lowest corner. A cyclic rotation retains face winding; independent decoding must match rendered vertices for slopes.

## Native evidence

All runs use isolated DuckStation roots, Software renderer, Recompiler CPU, full NTSC-U firmware SHA256 `71af94d1e47a68c11e8fdb9f8368040601514a42a5a399cda48c7d3bff1e99d3`, new copies of the blank card and ordinary controller input. RAM captures are read-only. No save states, memory writes or position assistance are used. Exact candidate paths, hashes, card hashes, observations and screenshots are in `work/start-blank-level-20261001/runtime/*-native-receipt.json`.

- Initial blank Artisans R1 cold-loaded with zero placed rows, the new floor and safe spawn. Jump/landing, fall/respawn, flame and inventory 0/0 passed. This preceded final mesh changes and is retained separately.
- R3/R4/R6 are diagnostic failures: large floors lost visibility; R4 isolated the issue without an enemy. They are not accepted runtime examples.
- R5 is inconclusive because a directional toggle was inadvertently left held during menu navigation. It is not accepted traversal evidence.
- R7's manually generated 50-unit grid plus corrected cache passed a controlled walk to raw XYZ [8961,10169,9061], jump/landing, gem collection and inventory 1/1. An earlier longer movement left the small platform and respawned; it is recorded separately.
- R8 used a genuine Artisans texture with native flag 0. A controlled walk reached raw XYZ [8960,10175,9069]; the floor remained visible through jump and landing, with the gem collected. This was a private diagnostic, followed by production implementation.

## Final production verification

- **R9 blank**: cold New Game, safe spawn, walking to raw XYZ [8960,10031,9062], jumping and landing passed. The floor stayed visible; inventory showed 0/0 treasure and no dragon goal. The compiler generated eight native triangles from the two editor faces. BIN SHA256: `54d48755c139415fb5dc4be35e6dbfd78d2378d62e830166e6df8cdcc58dc563`.
- **R10 replacement**: the 200-unit cyan floor compiled to 32 triangles. A red gem collected correctly; inventory showed 1/1. A controlled diagonal walk to [9740,9672,9061], jump and landing passed with the floor visible. Two earlier long movements left the small platform and respawned; those attempts remain in the receipt and are not evidence of supported traversal. BIN SHA256: `7bedf71ed3b8dc0997d3b59fd01f2bef6c764bc1f4707455a35eba9c086a659d`.
- **R11 slope**: cold New Game, diagonal uphill walking to [10175,9988,9213], jumping and landing passed. The player height followed the slope; the floor stayed visible and gem inventory showed 1/1. BIN SHA256: `4ed4f5c0605666d26e7bb0edc82e26f36eaf637cd8d8fe1c348b39f1698e2bb2`.
- Each final run closed without a resume state. The blank card retained SHA256 `52e9972468ee7f0864c110c06dc13530b71c05e1669c9a4cbe7e8c26d91be220`. None of these three runs exercises ordinary memory-card save/reload or a full campaign.
- Frozen-source Release regression at `aabe94d`: **335 checks passed, zero failed**. Summary: `work/start-blank-level-20261001/verification/final-release-regression-summary.json`.
- Focused Debug UI checks initially passed 25 Start Blank checks and authored mesh checks. Actual packaged recovery then exposed a lost level-picker label and a stale loading message. Both were corrected; final focused Start Blank checks passed 27 in Release and Debug, level-picker checks passed 26, and the runner self-tests passed 115. Builds had no warnings or errors. Native compilation source remained unchanged.
- Final geometry checks: 1,158 in both Release and Debug. The source-backed Release compilation/readback batch passed 2,435 checks before 83 extra test-only assertions; final Debug replacement compilation/readback passed 1,741. Exact product and test identities are preserved in `work/start-blank-verification/production-subdivided/final-verification-receipt.json`.
- Cleared Town Square/fresh-level initializer ownership rejection: 32 checks passed in both configurations, before any output mutation.

These bounded examples do not certify an entire campaign, every initializer or every Moby variant. The previous universal matrix remains 105,559/145,285 statically composed cells; all matrix runtime cells remain not-tested. Twenty normal level initializers still require transition-callback work. The retained sound/particle kernel has not been proved reducible further. Mac packages are Developer ID signed but not notarized; the Windows package has not been executed on Windows.

## Packaged workflow and artifacts

The private packaged UI project is `work/start-blank-level-20261001/packaged-gui`.
Cancellation preserved Artisans. Confirmed clearing produced zero objects and two
floor faces. Remove Terrain, Undo Terrain and Save Terrain worked on that floor.
Normal Create BIN/CUE exported the saved replacement. After closing and reopening,
the replacement remained present; Restore Before Clear recovered the original
174 objects and terrain while preserving the replacement scene in recovery storage.
The final package retested clearing, switching away/back and restoration: both the
finished loading message and the Artisans level-picker label were correct.
Exact observations, snapshot file hashes and the GUI export hash are in
`packaged-gui/verification-receipt.json`. That particular GUI-export BIN was not
booted; native runs R9–R11 are separately bound to their production compiler fixtures.

Final local packages in `outputs/SpyroEditor-V1.0.2-release`:

- `SpyroEditor-V1.0.2-osx-arm64.zip`: SHA256 `a8d559351bc2ac89c4543be7e2076317b41ecac79058e5c378aad0e27a78718a`.
- `SpyroEditor-V1.0.2-win-x64.zip`: SHA256 `10a85be711c3396582aff18b4e796a5374dbf07a2cae4b13d7f85cf2e462b830`.
- Source archive and final commit identity: see the task's `final-checkpoint.json`.

`verification/package-build-final.log` records successful full package verification.
All 335 frozen regression logs are copied into `verification/frozen-release-logs`.
The final USA/Japan source-disc hashes match their originals; all 173 protected
user-project files and their preserved snapshot match the earlier manifest.
Private verification apps were closed; the user's original beta process was retained.

Remaining work: prove the 20 short-initializer transition paths before extending
Start Blank coverage; continue exact Moby behavior/save tests beyond the recorded
variants; complete platform/runtime and distribution acceptance. No Fly Anywhere
work or remote publication was performed in this continuation.
