# Fresh chest: authored reward destinations after R02

Read-only investigation of R02 at checkpoint `55dd291`, followed by this
research note only. No executable, native behavior, candidate, emulator state,
or production admission was changed by this audit. **Key-zero remains unproved.**

## Finding and proposed R03 input

The escaped red was a correctly emitted native gem, not an omitted reward or a
wrong treasure value. It retained ballistic substate2, crossed the north edge,
and fell. A controller's authored XYZ is a launch destination, **not a boundary
that contains subsequent native bounces**.

For the next private experiment, keep the existing3072 floor, key, chest and
other four controller destinations. Override only `Reward2` (placed row5,
selector53) to world **`(10454,10503,9216)`**, relative to the chest
`(-410,+615,+512)`. This is the unchanged existing `Reward4` red destination.
It is preferable to the first proposed green destination for this experiment
because it is farther from the Wizard route, while still improving the
conditional north-edge margin. It is **not** a universal trajectory certificate.

## What R02 actually observed

Evidence: `_local/native-v6-chest-runtime-20260920-r02.hpMyEW/observations/observer01.jsonl`.
The observer reads sequentially, not as an atomic simulation snapshot.

| Sample | Red dynamic index24 XYZ | Row+49 | Slot35 treasure |
| --- | --- | --- | --- |
| 12 | 10864,9974,9230 | 2 | 0 |
| 14 | 10864,10834,9730 | 2 | 2 |
| 15 | 10846,11254,8751 | 2 | 5 |
| 16 | 10659,11581,8722 | 2 | 9 |
| 17 | 10559,11677,8702 | 2 | 9 |
| 18 | 10309,11917,7952 | 2 | 9 |
| 19 | 10059,12156,6203 | 2 | 9 |

Floor north edge is11520 and floor Z8704. Player XYZ stayed10555/8762/9067
through these collection samples. Other gems entered substate3 and approached
the player. Red24 remained class53 and owner byte row+56 was5, identifying the
second D controller, not the directly emitted chest gem. Its runtime private
pointer was800A9568; **its dynamic private bytes were not sampled**, so bounce
counts and velocities below are source deductions, not RAM measurements.

The placed D row5 private owner is scene+12E4, runtime800A9788. Before emission,
sample00 shows linked parent3, relative vector `(0,1229,512)`, private+10=1,
private+14=0 and private+18=1. Its authored absolute source destination was
10864/11117/9216, already inside the floor. That fact did not contain its flight.

## Native owners and independent authoring semantics

The complete Original USA BIN is661547040 bytes, SHA256
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
Existing byte evidence used here includes:

- `441-chest-reset-preconditions-inspection.log` in the existing
  `_local/research/v6-mixed-export-20260912-r01.jR0oBr` directory: complete Peace
  reward owner8007E59C/434, SHA256
  `129d8511c63925af80ab73a02def446efc39e23974255fd4b8d38f51411c2056`.
- `_local/research/dark-wizard-native-closure-20260906/town-repeat-reward-callback-r01.ban08o/13-root-repeat.json`:
  Town complete spawn800872A4/B7C, SHA256
  `3c8dcb5a5a28fd197ad04c9512fbe00519bee1097d62dd27d4461931f53bb225`;
  shared emitter8003ABC0/51C, SHA256
  `979e567dd4bebbdd9cadaf8f884e1110be16f293c6590ba6214941be5be7a4a8`;
  Town gem/0E/0F tick80081BAC/C98, SHA256
  `9db058c9cd3f08e4d8c4d6902cdf34ffc4c5e38d8bdafcb0f38e28d73f7f6d5f`.

Additional narrow helper inspection used the existing pinned Original reader
and complete EXE SHA256
`a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`.
Whole BIN SHA/length/EOF and held/named filesystem identity were checked before
and after, including a named reopen. No source bytes were modified.

| Native owner | Relevant effect |
| --- | --- |
| Peace7E5D0..7E5F8 | With private+18 nonzero, subtract linked parentXYZ from this controller's private+4/+8/+C. |
| Peace7E7B8..7E7D0 | Add this controller's currentXYZ back into that private vector before emission. |
| Peace7E7E8..7E848 | Add1024 to destinationZ for a5120-range floor query. On success restoreZ, pass mode2 and private+4 as emitter a3. Otherwise use mode1; private+24 selects the separate mode4 path. Actual retained +24 is zero. |
| EXE3ADD0..3ADE4 | Mode2 copies the supplied destination and selects initial vertical velocity140. |
| EXE3AF84..3AFA0 | With actual a2=0, launch origin is controllerXYZ plus256Z. |
| EXE3AFA0..3B02C | Destination is checked again for floor/slope and resident collision. A rejected destination is replaced by controllerXYZ. |
| EXE3B030..3B09C | Compute a finite flight divisor, destination-minus-launch vector, and initial velocity; write velocity XYZ to emitted private+0/+4/+8. |
| Town875F0..87604 | **Parent class0D selects two bounces**, private+10=2. Other parents take the default3. |
| Town87634 | Emitted gem starts row+49=2. |
| Town82058..820E0 | Move by scaled private velocity, cap displacement magnitude220, then subtract5 times global756CC from vertical velocity while it is at least−219. |
| Town820E4..82290 | Terrain query uses240/240; dry-floor/normal tests and zero remaining bounce count control settlement to substate1. |
| Town822EC..82364 | Call native reflection17428. On successful reflection decrement remaining count, randomize reflected velocities as described below. |
| Town82390..82490 | Companion pickup selection depends on player distance, vertical difference, companion availability/state and other native gates. |

These source PCs are not guessed Fresh relocation addresses. The current
shared chest layout relocates the Peace owner and retains the Town/EXE owners;
the existing Host/source and complete-inverse guards remain necessary.

Changing a child's authored destination does not require changing parent index,
reward selector, +14/+18 flags, delay/ordinal, callbacks or native instructions.
The coherent source-shaped edit changes **both** rowXYZ and private+4/+8/+C.
For row5 those scene words start at298 and12E8 respectively. A separate typed
override is appropriate: silently changing the old source-relative-cloning
contract would lose its historical meaning and goldens.

## Conditional arithmetic envelope, not engine execution

The following is an advisory calculation, **not** an executed scalar/GTE
witness or admission gate. Assumptions are explicit:

1. Global756CC remains2. R02 did not sample this global.
2. Destination survives the native validation and resident-object query.
3. Collision is with the dry, horizontal interior of this one flat floor;
   native response acts as exact flat-normal reflection and returns the point
   to floorZ. No other object or triangle-edge response intervenes.
4. No companion pickup interrupts flight. This makes the arithmetic useful for
   clearance, but does not establish real companion behavior or collection.
5. GTE multiplication/division/reflection have the ordinary unsaturated
   small-integer result. The actual helper bodies contain GTE instructions;
   this investigation did not execute or substitute for them.

The native emitter loop3B030..3B05C, including its delay updates, gives divisor28
for launchZ=floor+256, targetZ=floor+512 and initial vz140. Helper177F8 uses
the integer reciprocal `floor(65536/28)=2340`, then arithmetic shift16.
Thus `vxy=(delta*2340)>>16`, not arbitrary floating-point aim interpolation.

At global756CC=2, helper177C0 scaling followed by176C8 shift1 leaves velocity
as the proposed displacement. Gravity subtracts10 after movement. The first
flat-floor impact occurs after31 model ticks with post-gravity vz−170.
The movement cap220 is inactive throughout these calculated flights.

For each successful bounce, after native reflection:

```text
vx = (reflected_vx >> 3) + (RNG & 63) - 32
vy = (reflected_vy >> 3) + (RNG & 63) - 32
vz = (reflected_vz >> 2) + (RNG & 15)
```

Horizontal noise is therefore−32..31 and vertical addition0..15. A finite
arithmetic enumeration checked all16×16 vertical choices for the **two** D
bounces and both monotone horizontal extrema. It deliberately allows any
combination rather than claiming every combination is attainable by the
shared RNG stream. Maximum model stage lengths are31,13,8 ticks:52 total.

| Reward2 destination relative to chest | Initial vx/vy | Conditional whole-flight XY rectangle | Minimum floor-centre margin |
| --- | --- | --- | --- |
| Existing `(0,1229,512)` | 0,43 | X10160..11539; Y9888..11969 | Escapes east/north |
| Proposed green `(-615,205,512)` | −22,7 | X9431..10864; Y9401..10780 | 656 |
| **Recommended red `(-410,615,512)`** | **−15,21** | **X9661..11048; Y9861..11248** | **272** |

The red recommendation leaves32 beyond the native240 terrain-query radius in
this conditional model. That is useful experimental headroom, **not** a broad
margin for unknown step changes, animated colliders, triangle edges or other
physics. The unchanged other gems are not certified by this row5 calculation.

The earlier three-bounce estimate was a generic-parent overestimate and must
not be cited as the actual D branch. The exact native class test above resolves
that distinction. Similarly, shifting the observed R02 path by a fixed offset
is only an empirical estimate: it does not reproduce RNG/companion timing.

## Destination validation and recommendation limits

EXE4E3C8 is the **resident-object/model-collider query**, not the static terrain
query. Emitter3B000..3B010 supplies radius200 with the remaining explicit
arguments zero. Static terrain is queried through4D5EC/4BE4C. Therefore a
valid authored floor point can still be rejected because of a resident object;
the mode2 emitter then falls back to controllerXYZ at3B020..3B028.

The green proposal10249/10093 has horizontal distance about286 from the Wizard
route endpoint9984/9984; the red recommendation10454/10503 has distance about700.
This comparison favors the red point but is **not** an animated Wizard collider
proof: its selected model/frame/vertex transform and timing still matter.
The chest itself may also be present in a query depending on its actual native
retirement state. These are reasons to observe the next candidate, not to
silently certify a helper result.

If destination fallback occurs, the conditional zero-horizontal-launch model
has possible XY drift−704..675 from the chest. Its east extreme11539 is19
beyond this floor. Consequently neither recommended recipe nor a numeric
endpoint check proves every fallback flight stays supported.

Moving the whole chest package to10048/9344 is statically plausible but changes
the player route and all five launches; the same conditional old red north
extreme becomes11425, only95 inside the existing edge. Widening to3584 with the
old chest is also not automatically generous: its conditional old red north
extreme11969 is only63 inside12032. Neither calculation justifies replacing a
real native test with a translated observed path or an endpoint-only gate.

For R03, prefer the one semantic child override above, preserving all other
tested inputs. Recheck actual emission origin/direction, emitted parent owner,
all six chest rewards, slot35 delta10, the separate Wizard reward, and ordinary
collection/reset behavior. Runtime, full lifecycle, complete census, key-zero,
unrestricted authoring and publication remain separate, unproved gates.

## Implemented source extension and verification

The internal immutable WORLD destination owner now captures one to five closed
semantic roles, with detached/sorted values and exact-instance ancestry.
The existing six-field key/chest intent is unchanged. No override uses the
literal old fingerprint branch; the separate authored branch identifies its
actual overrides. It coherently writes row/private XYZ and retains the same36
source-coordinate owners, all non-coordinate bytes and complete inverses.
Explicit WORLD destinations remain fixed when the chest moves.

RED516 fails on23 absent API references; build518 passes with zero warnings
or errors. Free519 passes270 authoring checks; full regression522 builds all
projects and passes343 command steps with zero failures, including the separate
override validation/mutation cases. Observer517 exposed a stale invalid-profile
fixture; corrected521 passes13 offline tests with no connections or ROM reads.

Source520 passes414 assertions. New R03 scene:
`4f560dcbfa2d997a3f02554cf297d95c1778f67fc5d8fcf8ff47f64d609a89e8`;
Body `2d8c1bf1af35c8e5522b239c564d36c79c828539d133278c461b6178b53bb504`.
Independent literal tests show exactly four differing words versus R02.
Historical source replays523/524 pass579/390 assertions respectively; their
whole images **and complete physical plan identities** match historical
497/507 byte-for-byte. R01/R02 write recipes remain retired; their source-only
recipes are still distinct and reproducible.

Actual R03 creation525 passes417 assertions, including full readback and
complete281270-sector inverse to Original. BIN:
`a5b00725388f73e7e6f7b9ed356c4a0d0f55b1c869d6a65f38e97ef2641d6c50`.
It is isolated under
`_local/native-v6-chest-runtime-20260920-r03.p2kFgp/candidate-r03`.
Native executable and overlay remain unchanged. Original BIN/CUE pass final
held/named checks. This evidence is not a fresh DuckStation result.
