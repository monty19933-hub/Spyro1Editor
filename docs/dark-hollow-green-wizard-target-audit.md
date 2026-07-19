# Dark Hollow Green Wizard target audit

Status: **NeedsTargetProfile / Blocked**. `CanStageInstance=false`; no candidate BIN was emitted.

The complete Toasty v11 Green Wizard bundle is not safe to install in Dark Hollow yet. The behavior-overlay ABI, particle ABI, RAM headroom, target enemy row, properties scratch, and pointer-fixup path all check out. Independent actor-package storage and descriptor-aware VRAM allocation do not.

The machine-readable evidence is in `docs/dark-hollow-green-wizard-target-audit.json` and is reproduced by `scripts/Audit-DarkHollowGreenWizardCandidate.mjs`.

## Checked source

- Clean USA BIN SHA-256: `fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`
- Dark Hollow overlay: WAD entry 13, offset `0xEF6000`, size `0xB000`, SHA-256 `9920aa47b94005166f457b5b36df6dedd1f2400f91698bac21cdb01d8590ef32`
- Dark Hollow data: WAD entry 14, offset `0xF01000`, size `0x28D800`, SHA-256 `a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97`
- Scene: data offset `0x194000`, size `0x11800`, SHA-256 `c48c344d3809f1ea5761e968ce6f18b02d5c7bbb82fcec4a7fd453343dbb84f8`
- SCUS SHA-256: `a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`
- Proven donor output SHA-256: `e2ec0b112c9f7782ec85e66118cf157550938e9c49146339df6d1000ffbff5e7`

## Static checks that passed

| Check | Evidence |
| --- | --- |
| Main dispatch ABI | Hook `0x8007AF24`; preimage `240200FB 106218F4 286200FC`; current Moby is `s3`; imported handler expects `s7`, so the shim must swap/restore `s3` and `s7`. |
| Particle ABI | Types `0x07` and `0x41` have replaceable default slots in the update table at `0x8007AB74` and spawn table at `0x8007ACB8`; target particle register is `s1`. |
| Overlay/ISO capacity | Native `0xB000` + compact donor `0xD108` + particle bundle `0x380` + shim `0xA0` aligns to `0x18800`; WAD growth is `0xD800`, with `0xB90800` ISO headroom. |
| RAM capacity | Copy buffer moves from `0x80085594` to `0x80093238`; SCUS HI/LO pair is at `0x4B048/0x4B04C`; the loaded scene ends at `0x80147988`, leaving `0x40228` before `0x80187BB0`. |
| Instance candidate | Dark Hollow T48 is Big Armor Gnorc actor `0x00A6`, row SHA-256 `3ddff384030fc76d677cc3c7e24fb70d392da48ab34b12ad1fc7251124c763a1`; yaw `0x87` and reward `0x55` can be preserved. |
| Properties/fixups | Zero `0x50`-byte scratch at scene `0xB838`, SHA-256 `5b6fb58e61fa475939767d68a446f97f1bff02c0e5935a3ea8bb51e6515783d8`; fixup list at `0x10EF0`, count 147, SHA-256 `791dc7f54f054799c479cbe8af1548a69136194debd8e169d037c92e11fb3585`; append slot `0x11140` is zero. |

## Hard blockers

1. **No independent actor-package allocation.** The Wizard package is `0x35AC` bytes and lightning is `0x564`, for `0x3B10` total. The model subfile's only qualifying zero tail is `0x254` bytes at data offset `0x193DAC`, a `0x38BC` shortfall. The root and actor-ID tables have free slots, but every resident package span large enough for the Wizard belongs to an active native actor class. Overwriting actor `0x00A6` would also break or retarget T49.

2. **All five proven Toasty v11 texture coordinates collide.** Dark Hollow has nonzero native data in every required region. The trail pixel tile at `0x78AB0` is zero, but its CLUT at `0x34C60` is occupied. The four Wizard/lightning pixel regions and all of their CLUTs are occupied. The complete Dark Hollow preimage aggregate is `7ff54417530ea12b6c6b73b5dc4998c1be340d1b1c84baca8cf74baad3572cb6`.

3. **Actor descriptors are not rebaseable yet.** Toasty v11 rebuilds the independent trail sprite descriptor, but the copied Wizard and lightning actor packages still address their native VRAM coordinates. A new Dark Hollow allocation is useless until their model face descriptors can be guarded and rebased.

Copying the Toasty package or texture coordinates now would be expected to corrupt Dark Hollow models or silently regress another native actor. Refusing to produce a BIN is therefore part of the audit result, not an incomplete verification step.

## Next safe experiment

1. Add a guarded model-subfile grower that shifts the scene and later subfiles, updates every data-header offset, and proves an otherwise byte-identical Dark Hollow boot.
2. Trace and implement actor-package face descriptor rebasing, then allocate non-overlapping Dark Hollow VRAM and CLUT regions with preimage and readback guards.
3. Install the already-compatible overlay and particle shim, register independent `0x011B` and `0x0026` roots, and stage T48 while preserving reward `0x55`.

Dark Hollow was deliberately not added to `GreenWizardRuntimeBundleCatalog`: the current transplanted profile type requires complete package and texture targets and is reported by the editor as stageable. Adding this incomplete profile would falsely claim `CanStageInstance=true`. The honest editor result remains `NeedsTargetProfile`.

## Reproduce

```sh
node scripts/Audit-DarkHollowGreenWizardCandidate.mjs \
  --image "/path/to/your/Spyro the Dragon (USA).bin" \
  --analysis spyro-wad-analysis.json \
  --output docs/dark-hollow-green-wizard-target-audit.json
```
