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
32963/37458/9277. Source reward at row offset0x53 has value0x54, selecting the native green2 gem; row offset0x54 is a
sound-channel sentinel, not a save flag. Final reported RAM headroom is 917,852
bytes. All 254 unrelated WAD entries remain unchanged.

## Native gameplay verification

The closed second run confirms full-firmware world rendering, moving-floor carry,
normal jumping/landing, active native spell8 targeting, charge defeat, two-gem
collection and native inventory2/2. Normal death/respawn retains the collected
identity and total. Card persistence and listening remain pending. The editor's group drag/grid behavior
also needs direct UI verification. This is not universal-gameplay or V1.0 release
acceptance.


The spawn-only diagnostic disc SHA256 is
`05b49acf7d92211af9e6ff0475103ee60ccee2e655f6894e7e7b84c3fc0f0cba`.
It changes only initialXYZ to32592/37594/9584; independent extraction and sector
comparison finds one changed sector containing XYZ and regeneratedEDC/ECC.
Original heading128, all world/code/assets and all other entries are unchanged.
This starts above the imported moving platform; it is a diagnostic placement,
not a claim that the normal editor arrival-surface guard supports this location.

The run uses nativeNewGame20, fullUS firmware, software rendering/recompiler,
private blank card, no save states or RAM writes. Neutral80-frame interval:
Spyro31328/36337/9430→27792/32842/9430; linked platform/HP vertices/all48 moving
collision vertices translate−3536/−3504/0, versus Spyro−3536/−3495/0. Native
floor queries retain animated triangle47 atZ9072. World commands17→50 advance
and pause normally. Cross jumps off; Spyro lands on authored ground.

Approach triggers druid flee90. Controller charge causes native defeat3; green2
collects with durable/checkpoint logical135. Native inventory shows2/2. A normal
fall reduces lives4→3; respawn preserves total2/durable135, clears checkpoint,
restores druidstate0 with rewardFF, and resumes world animation. No second
postcollection defeat or card save/cold reload is claimed.

The corrected read-only particle scan finds live native type8 targeting row1 in
entry, carried and respawn captures. The old helper incorrectly treated next
allocation/free-hole pointer80075738 as the live end; the native allocator uses
theFF terminal withFE holes. The LP parser also used the wrong vertex offset;
correct stored LP vertices are unchanged while HP/moving collision animates.
These were verification-helper defects, not production bugs. The timer at
props14 progresses; props1C=40 is its reset configuration. Separate close spell
visual confirmation and audio listening remain pending.

Evidence: `work/universal-finish-20261002/runtime/green-world-native-r2/runtime-proof.json`,
ten native screenshots, read-only RAM/register captures and archived log/card.
No invalid access/panic lines; blank card unchanged. Authoritative eight-capture
trace: `work/universal-finish-20261002/green-native-trace-r2/saved-ram-trace-final-eight.json`.
The first entry summary passed incorrect physical148/149 row arguments; its RAM
is valid, but those two decoded rows are not evidence. Correct actor identities
are physical0/1 and logical135/136 in subsequent captures and the final trace.
