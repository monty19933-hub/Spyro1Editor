# Green Druid and original moving terrain

The original Alpine Ridge rows 5/48 now form one ordinary exportable linked group
in an empty authored Peace Keepers level. Either actor expands the complete group.
This is a bounded implementation; the other fourteen Green Druid variants,
occupied animation banks, mixed groups and other destinations remain restricted.

The installer retains the entire original sector 170: 30 vertices per geometry
tier, 8 moving and 14 fixed faces, 16 moving and 28 fixed collision triangles,
all seven collision frames, the complete native command records and three source
materials. Original texture depths, model, paths, sounds, constructors, callbacks,
target links, particle type 8 and collection identity are preserved. Four source
camera-occlusion assignments become unrestricted destination assignments, which
is reported in the placement behavior note. The source level-31 environmental
sound predicate stays unchanged and is inactive in Peace Keepers; sound listening
has not been verified.

The group must retain its common translation in multiples of 16 raw units, source
fractional position, yaw and state. Both physical row indices must fit in a byte.
The exporter validates complete source/native resource owners and the final
compiled code, world, rows and sampled VRAM. Same-level copies retain the original
world through a separate native-owner check; no generic world or texture guard is
relaxed.

## Integrated verification

Root checks in `work/universal-finish-20261002`:

- `green-world-root-verification-r1.json`: Release App build with zero warnings
  and errors; complete-world smoke 907; candidate readback 279.
- `green-world-root-regressions-r1.json`: previous Green Druid controls 32,
  High Caves world 1,039, first Alpine group 734, second Alpine group 1,425,
  Magic chain 5,749 and source-free universal controls 102, all passing.
- Independent delivery instruction checks cover native world/cache/material,
  camera and particle consumers; these do not substitute for emulator gameplay.

Registered smoke:

```sh
dotnet run --project src/Spyro.Editor.GreenDruidWorldSmoke -c Release -- ORIGINAL_BIN CATALOG_DIRECTORY NEW_OUTPUT_DIRECTORY
```

The independently reproduced candidate is
`work/universal-finish-20261002/green-world-root-candidate-r1/candidate.cue`.
BIN SHA256: `f18eb332075b4e0a094a5cf8990b9ded834b39138b60c2f606fab005ee1dea72`.
Its normal authored terrain includes a ramp and landing beside, but not beneath,
the full moving-platform envelope. Native New Game starts in level 20 at
32960/31200/7168. The druid is at 32942/32072/7084 and its terrain target at
32963/37458/9277. Source drop byte 0x53 selects native gold 10; byte 0x54 is a
sound-channel sentinel, not a save flag. Final reported RAM headroom is 917,852
bytes. All 254 unrelated WAD entries remain unchanged.

## Pending acceptance

Fresh full-firmware model/world rendering, spell targeting, moving-floor carry,
jumping/landing, combat, ten-gem collection and inventory, death/respawn, card
persistence and listening remain pending. The editor's group drag/grid behavior
also needs direct UI verification. This is not universal-gameplay or V1.0 release
acceptance.
