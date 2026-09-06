# Destination-scoped native Free Flight — executable stage

2026-09-06. Source: unchanged USA BIN SHA-256
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`;
`SCUS_942.28` SHA-256
`a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`.
Executable file offset is runtime address minus `0x8000F800`.

## Boundary

`Exporting/NativeFreeFlightPatchPlan` is an immutable, source-bound, reversible
**executable byte producer**, not a publisher or gameplay certificate. It
accepts one retail normal level identity, including native Gnasty's Loot; the
independent ID65 profile is not yet accepted. Disabled emits no instructions or
added animation requirements and returns the unchanged input. Re-edit/export
must rebuild from the locked source; an already enabled executable is rejected
as a disabled preimage. An explicit inverse validates every afterimage before
returning reverted bytes.

No original image, installed editor, emulator, settings or candidate is changed
by this API. `PublicationAuthorized` and `RuntimeVerified` remain false.
Player-animation closure, destination memory fit, effects and gameplay remain
separate obligations before a checked output can be published.

## Native architecture and actual consumers

Do not turn normal destinations into flight levels. The loader classifier at
`800138C0..139B0` writes global `80075690` and initializes flight-only state and
overlay paths. The previous ID65 classifier failure already demonstrates why
this is not a generic flight permission.

Gnasty's Loot instead has a separate permission, `u32 80078CA4`. Shared movement
`80044038..4406C` enables it when gameplay global `800757D8` is zero, level is
64, and player field `80078AF4` is zero. This field's zero/landing path is also
consumed by native flight states (for example `8004394C..43A1C`); the patch
retains that exact predicate. Player reset `8004AC24` clears the permission at
`8004ADD0`. The new target is added to this permission predicate without
changing the native64 predicate or using new RAM.

The five entry consumers formerly selected native flight on
`flight-global != 0 OR level == 64`. Their replacement preserves both terms
and adds only `level == target`; it does **not** substitute permission for
native64, which could alter the first frames following reset.

State `0x20` selects animation33; states `0x21/0x22` select34/35 through the
state table `8006C470` (EXE `5CC70`, 45 bytes, SHA
`9f502dce2b68cb8497072c5c9c92d3f914710cdbbcbc9c25c86cbe29a4241e0b`).
Native transitions at `8004381C/43834` and `80043C9C/43CDC` select these states.
They share initialization `8003F570` and mover `80045230`.

The actual 3D movement helper `8003D978..3DA08` constructs a rotation from
the native bank/yaw/pitch values and rotates the speed vector into XYZ velocity
at `8003D9EC`. The flight mover reads analog axes from pad `+16/+17`, or
digital direction bits at `80045294..45440`, updates yaw/pitch, and calls this
helper at `800457AC`. Cross/flap, Triangle/drop and landing/damage branches are
retained. These are native forward flight controls, not an editor noclip camera
or a new hover control scheme. Exact practical ascent/descent polarity and
feel still require gameplay verification.

The native permission alone is insufficient: the mover caps pitch against
takeoff height, and flight-state handlers force falling above takeoff+512.
The new stage removes those two restrictions **only for the target level**.
Normal collision, landing, damage, death, portals, transitions, native flight
timers and unrelated-level ceilings are not suppressed.

## Fixed instruction ownership

Eleven disjoint windows, no executable growth, code cave, global-pointer
assumption or invented field:

| Purpose | Runtime start | Bytes |
| --- | --- | ---: |
| Target-or-native64 permission | `80044040` | 48 |
| Jump entry | `80041E9C` | 48 |
| Cross entry | `80041F84` | 40 |
| Falling entry | `800430B4` | 48 |
| Recovery entry | `800432D8` | 48 |
| Additional entry | `800434DC` | 48 |
| Permission-enabled glide ceiling | `800429A0` | 56 |
| Flight state ceiling | `80043844` | 56 |
| Rising state ceiling | `80043A88` | 60 |
| Diving state ceiling | `80043CEC` | 60 |
| Pitch ceiling | `80045690` | 84 |

Every before/after byte is exposed in immutable evidence. Scheduling reuses
existing absolute-address bases; R3000 load and branch delay slots are explicit.
The height windows preserve existing failure labels, substate guards, downstream
Z arguments and substate pointers. The pitch window uses the already established
`s5 = 80078B70` (at `80045670..74`), whose `+8` is pitch; the native -800 lower
clamp and earlier +800 upper clamp remain. Complete affected functions and
explicitly named native consumer spans are
hashed in addition to exact instruction preimages, so a compatible-looking
four-byte edit cannot silently change the relied-upon engine semantics.

### Exit-register and native continuation review

The refined schedule leaves `t0` entirely untouched by every height window.
For all guard paths, the native `a0/a1` values are identical, including the
nonzero-substate early exit. Only `v0/v1` can differ; success immediately
defines `v0` and loads a fresh `v1` from the input record at
`429EC/43898/43AE0/43D44`. Failure calls state initialization `8003EA68`,
which defines `v0` at entry and `v1` at `8003EA90` before either is read.
The three nonzero-substate continuations `438D8/43B14/43D78` only use fresh
`v0` and write `a0` in their native branch delay slots before invoking a state.

The common state epilogue ends at `80043FE4`. A scan of the EXE and all35
overlays finds exactly one direct caller, `8004A30C`, and no literal function
pointer. Its return continuation first defines `v0` at `8004A314`; both
branches kill `v1` before use, either at callee entry `8003D194` or at
`8004A398`. These actual instructions are bound, rather than assuming that
caller-saved registers are dead inside a function. This remains bounded
direct-source linkage evidence, not arbitrary computed-branch proof.

Permission's additional `at` scratch is killed at `80044088` on the valid
state path. On the invalid-state path, the common epilogue calls vector-add
`80017758`; its first instruction loads `at`, and its following load-delay
instruction does not read it. Pitch now computes the native `s2/s3` values
for **every** destination, then bypasses only the target's height-based store.
Its additional `v1` is killed at `80045708`; `at` is killed at `8004573C`.
The smoke checks all32 registers from nonzero distinct initial values and
walks actual continuations, both branch alternatives, delay slots and callee
entries. Hostile successor-use and load-delay mutations must fail.

Complete affected function certificates are EXE `31E70+2974` (state,
SHA `51bc8b945b30f188e5d8b48a51c7148c504a453da28ba204622dcd564d1d6121`),
`347E4+3B7C` (movement,
SHA `8bf295cd4202b6aaad490a2b87df209c43b1d6f508dd10068f50c22ada2d35e4`),
and `3B424+214` (reset,
SHA `9d80836708239aa7f8b5a8888b955928ae0051ca8eb8ed00b4b7e9098e84a3c2`).
The prior prototype's state/movement ranges stopped short of their actual
epilogues; no output was published from that prototype.

## Required player/effect resources

The source root0 stream, not a generic Moby package, owns animations33/34/35.
Gnasty's Loot records at WAD `5C659C4`, `5C66AE8`, `5C67C88` have payload lengths
`111C`, `1198`, `10FC` and are byte-identical to Sunny Flight's corresponding
records. The general `NativePlayerAnimationSource` merge can supply them to a
destination without changing ordinary model packages or scene bytes. For
Toasty, the tested merge adds `0x33C8` record bytes and `0x3800` sector-rounded
model storage. Disc growth is not runtime-fit proof.

All 11 frame words in each of these three animations have high byte `FF`.
Native `8004A668..4A670` reads frame byte `+27` and skips the sound call on `FF`;
the companion consumer does likewise at `8004A708..4A710`. This closes these
direct frame-triggered sound selectors, not all flight-state sounds.

The mover separately calls the destination particle callback `800758E4` with
count1 and selectors `1A/1B` at `80046390..463B0` and `80046214..4625C`.
These are particle selectors, **not** generic Moby classes26/27. Town Square's
two dispatch entries lead to its native loop tail `80089E74` (no-op). Toasty's
entries at WAD `148043C/1480440` likewise lead to its loop tail `800849B8`:
the native callback exists and deliberately emits neither of these trails.
Other
destinations' actual dispatch, texture ownership and allocation behavior must
be bound before claiming their effect closure. No substitute visual or sound
is inserted by this stage.

## Regression status

Intentional RED: `dotnet build
src/Spyro.Editor.NativeFreeFlightSmoke/Spyro.Editor.NativeFreeFlightSmoke.csproj
-c Debug --nologo` failed in1.43s with exactly four CS0103 missing-contract
errors before implementation, zero warnings.

Initial GREEN: the same Debug build succeeded in4.79s, zero warnings/errors;
source-free mode passed21,660 checks and exact-source mode passed21,706 checks.
Plan SHA was `e79a236594201dc1a25929b6747e4c27aa3dfe7777567a1003889a715241efba`.
The next source-wide linkage build passed in4.93s with21,714 exact-source
checks. Following the exit-liveness review, the final refined Debug build
passed in4.80s, zero warnings/errors; source-free passed32,190 checks and
exact-source passed32,284 checks. Refined plan SHA:
`74746b8b7ff855f4bf35f1891795050add292bd6c55068dbec12389e4ac89bd7`.

The focused smoke contains an independent bounded R3000 window interpreter
with both delay semantics; differing levels, native64, failed guards, signed
limits, exact-source preimages, inverse, double-application/corruption refusal,
disabled state and complete untouched complement are explicit tests. These
instruction/byte checks do not claim ascending, descending, collisions,
reload, death/respawn or portals have been exercised in-game.

The independent direct-reference source scan covered the EXE payload and all
35 retail overlays: 36 domains / 602,112 aligned words. Its six external
branches all enter preserved window starts (`4382C→43844`,
`43A58/43A70→43A88`, `43CBC/43CD4→43CEC`, `45684→45690`); no external branch
enters a changed interior and no literal table pointer names these windows.
This is a bounded direct-control-flow check, not a claim about arbitrary
computed runtime branches.
