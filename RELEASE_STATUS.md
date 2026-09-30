# beta53 RC2 release checklist

This candidate is usable locally; the requested full release is incomplete.
Do not promote static composition or scalar instruction tests to gameplay proof.

- [x] Preserve the original checkout, 36 handoff files, discs and prior packages.
- [x] Checkpoint the handoff at `c2d01e3` on isolated `release/v6-20260929`.
- [x] Remove added eyes from the exported egg model and inventory drawing.
- [x] Preserve seven selectable egg palettes, real previews and sign text editing.
- [x] Stable placement IDs, lifetime slots, deletion tombstones and native row binding.
- [x] Add durable-bit suppression and native live required-egg percentage.
- [x] Import ownership/capacity checks, 8/16-bit textures and missing particle art.
- [x] Enumerate 145,285 variant/destination cells with separate runtime status.
- [x] Pass 294 integrated build/test steps and final affected focused checks.
- [x] Build and verify macOS arm64 and Windows x64 packages.
- [x] Exercise the exact macOS RC2 GUI; preserve its tested package unchanged.
- [x] Boot the final exported CUE and load a copied card into native Stone Hill.
- [ ] Observe the final eye-free model and inventory inside the exported game.
- [ ] Finish native egg collection, normal save/cold-load and lifecycle acceptance.
- [ ] Per-level Dragonfly Eggs X/Y, save-menu percentage and bonus admission.
- [ ] Universal class remapping and all behavior, links, triggers and runtime coverage.
- [ ] Blank level in the same complete main editor and general native compiler.
- [ ] All-level Fly Anywhere, mixed edits and native lifecycle acceptance.
- [ ] Complete preview coverage, remaining UI review and Windows runtime acceptance.

Application packages were built from `9e73e3357c24d75085e0e6b4b7dcc349315b4d7b`.
Later source commits retain verification tools and documentation only. No app was
rebuilt after the RC2 desktop test. Community signing is ad-hoc, not notarization.

Deliverables and full evidence live in the parent `outputs` folder: `Quick-start.md`,
`Release-notes.md`, `Coverage-and-verification.md`, `Verification/`, and
`Universal-placement-coverage/`. The source ZIP contains tracked files only;
original discs, private native exports and copied cards stay outside the packages.

Next native egg work starts at `UniversalSpecialBehaviors`, `UniversalEggProgress`
and `docs/research/native-egg-verification-20260929.md`. The current inventory is a
global row. Original per-level inventory drawing is `0x8001BE14..0x8001C4E4`, with
world index `0x80075744`, row register `s3`, and treasure index `world*6+row`.
Any new hook needs source-byte ownership, branch/load-delay and actual render
verification. Do not reuse the global count hook as a per-level claim.

Fresh integration still needs a neutral main-editor document owner and a native
compiler accepting its geometry, materials, collision, spawn and universal
placements. The preserved narrow fresh research profiles must not be presented
as a complete File > New Level workflow.
