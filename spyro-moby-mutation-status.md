# Spyro Moby Mutation Status

Updated: 2026-05-13

## Proven

- Stone Hill and Artisans now both have confirmed 0x58 moby source tables for permanent BIN patching.
- Stone Hill source table: WAD entry 12, WAD offset `0xD72B38`, records `T0..T194`.
- Artisans source table: WAD entry 10, WAD offset `0x9D42AC`, records `T0..T173`.
- Normal moby moves are permanent when the source-table XYZ fields at `+0x0C/+0x10/+0x14` are patched.
- Slot reuse can create a working enemy/object from another same-level source record. Artisans test: sheep slot `T110` cloned from Treasure Gnorc `T7` became a Treasure Gnorc in game.
- Slot-reused enemies may still carry donor-linked behavior/path data. Artisans Treasure Gnorc clone ran back toward the original donor route before doing its normal path.
- Standalone gem color/value edits are proven:
  - red: `+0x36=0x53`, `+0x4F=0x01`
  - green: `+0x36=0x54`, `+0x4F=0x02`
  - blue: `+0x36=0x55`, `+0x4F=0x03`
  - yellow: `+0x36=0x56`, `+0x4F=0x04`
  - purple: `+0x36=0x57`, `+0x4F=0x05`
- Type `0x20` chest reward color/value byte is confirmed at `+0x53` for at least normal reward chests. Artisans test: Flame/Charge chest `T50` changed from green `0x54` to purple `0x57` and dropped a purple 25 gem.
- The native editor now exposes slot reuse directly:
  - `Copy Obj` copies the selected source-table object as a donor.
  - `Clone/Add` clones that donor into the selected slot while keeping the selected slot's XYZ.
  - `Add at Click` lets the user pick an object template and a target slot, clone the template into that slot, then click the terrain map to place it.
  - `Remove Slot` soft-removes the selected object by moving its source-table XYZ out of bounds.
- Artisans has initial linked dragon/pedestal move groups for `T90/T91`, `T92/T93`, and `T142/T143`.

## Working Experiments

- `tools/Export-SpyroLevelMobyPatchTest.ps1` reads saved native editor edits and exports a fresh-loadable loader-table BIN for Stone Hill, Artisans, or all mapped levels together.
- Native editor `Create Loader BIN` exports saved Stone Hill + Artisans moby edits into `Spyro the Dragon (USA)-loaderpatchtest.bin`, so editing both levels no longer requires separate CUEs.
- `tools/Export-SpyroMobyRecordMutation.ps1` can do focused source-record experiments:
  - `CloneIntoSlot`: clone one existing source record into another slot while keeping the target position. This is the current practical "add by reusing a slot" method.
  - `Swap`: swap two source-record identities while preserving both positions.
  - `Hide`: soft-remove by moving a record far out of bounds.
  - `Zero`: hard-remove by zeroing a record; risky and not the first choice.
  - `RewardColor`: changes standalone gems with proven bytes, or type `0x20` chest/enemy reward byte `+0x53`.

## Still Needs Validation

- True add/remove by expanding the source table is not proven yet. Slot reuse is safer because it does not change loader counts or nearby tables.
- Enemy reward-byte behavior needs a focused test per enemy family. Chest `+0x53` is proven; enemies likely use the same byte when they directly drop gems, but path/special-data behavior may still be tied to donor records.
- Runtime records beyond Artisans `T173` are visible in RAM but are not covered by the mapped Artisans source table yet.
- True add/remove still means "slot reuse" for now. We can create extra-looking gems/enemies/chests by converting unused or less important slots, but we are not yet expanding the loader table count safely.

## Handy Tests

- `Create Artisans Loader Patch Test.bat`: exports saved Artisans editor moby edits.
- `Create All Mapped Levels Loader Patch Test.bat`: exports saved Stone Hill + Artisans editor moby edits into one CUE/BIN.
- `Create Artisans Slot Reuse Treasure Gnorc Test.bat`: clones Treasure Gnorc `T7` into sheep slot `T110`.
- `Create Artisans Hide Sheep T110 Test.bat`: soft-removes sheep `T110`.
- `Create Artisans Loose Gem Purple Test.bat`: proven loose gem purple/value test on `T165`.
- `Create Artisans Chest Purple Reward Test.bat`: confirmed chest reward byte test on `T50`.
