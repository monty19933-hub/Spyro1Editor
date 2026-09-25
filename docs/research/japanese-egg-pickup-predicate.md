# F5 detached pickup proximity/player-state predicate

2026-09-25. This slice emits a **detached callable CPU predicate**, not an active
collector. It returns a Boolean before the original sound/effect/collection-byte
store/retirement path. There is no PocketStation enable gate, per-level alias
identity, inventory/save mutation, UI, publisher or candidate change.

## Exact native boundary and arithmetic

The retained JP Artisans fragment is `8008DF64/98`, SHA256
`a787f4aab4b11fe4ebaf6694ef543ded92f36e47ea28873cf5b7028d7890b9bb`.
Its outside success endpoint is `8008DFFC`, **before** the first sound-map load.
Every outside rejection goes to the original next-row endpoint `80090FA4`.
The compiler replaces these endpoints with private Boolean-return stubs; it
does not enter either original continuation.

There is exactly one call, at `8008DF68`: JP `80016F74/60`. That complete leaf
is byte-identical to USA `80017990/60`, SHA256
`277c87f0a049389cf5c3e0510eac8b878a516d9f773d44545184f67910815d90`.
The detached allocation includes that leaf internally. It reads only two XY
words from each input and has no nested call, global read, stack access or store.

The native conditions, in their original short-circuit order, are:

1. Native approximate XY distance is **less than `0x1F4` (500 decimal)**. For
   ordinary non-wrapping differences, this is
   `max(abs(dx),abs(dy)) + floor(3*min(abs(dx),abs(dy))/8)`, not Euclidean distance
   and not an invented circular radius. Axis distance 499 passes and 500 fails;
   diagonal `(363,363)` passes and `(364,364)` fails.
2. Load row `+38` with **signed `LH`**. Subtract that signed 16-bit offset from
   current row `+14` Z, then compare with player Z using the original two-branch
   signed test against the same 500 threshold. These differences use **SUBU**:
   they wrap at 32 bits. They must not be replaced by widened C# `Math.Abs`.
3. Reject if the player flags word contains bit `0x2000`.
4. If the player state is 15, reject when its substate is nonzero. Other state
   values do not read the substate. No unsupported descriptive enum names are
   assigned to these native numeric states.

The XY leaf instead uses trapping signed **ADD/SUB**, plus 32-bit shifts.
Extreme coordinates can raise native arithmetic overflow, including during a
branch/return delay slot. The emitted kernel retains those opcodes; an exception
is not converted into `true` or `false`. The convenient distance formula above
does not supersede these native width/overflow rules. Tests also preserve the
counterintuitive wrapping-Z case where a difference of `Int32.MinValue` passes
the original signed comparisons.

## Source-bound USA inputs, not one regional address delta

| Input | Japanese address | USA address | Evidence |
| --- | --- | --- | --- |
| Player X/Y/Z | `80082898/9C/A0` | `80078A58/5C/60` | JP row-loop frame-pointer load; USA native XYZ-constructor input, complete three-word copy leaf, retail XY consumer |
| Player flags | `80082A90` | **`80078C4C`** | Matching bit-2000 consumers `8004E62C/20` and `8004A200/20`; only the flags-load instruction differs |
| Player state/substate | `80082910/14` | `80078AD0/D4` | Native state-15/substate-11 consumers `8003610C/28` and `80034EE0/28`; scheduling differs, executed behavior agrees |

Applying the coordinate-base delta to JP flags would incorrectly select USA
`80078C50`, a different native field. Regression fixtures deliberately put the
opposite flag bit there, so that mistake cannot pass silently.

The exact-source issuer authenticates both complete disc revisions using
`NativeRegionalSourceSession`, both complete Artisans overlays, and twelve
explicit evidence windows. Full per-window hashes are code-owned in
`NativeJapaneseEggPickupPredicateSource`. The source test additionally executes
both regional state and flags consumers, not merely their evidence labels.
Held-handle and freshly reopened named whole-image identity checks run at the
end, including failure paths. Fixture captures remain `SourceBound=false`.

## Callable ABI and detached ownership

The original JP loop supplies `s3=row`, `fp=80082898`. The USA update prologue
instead sets **`fp=1`**. Copying the fragment into that loop without an adapter
would therefore try to read player coordinates at address 1.

The new internal `NativeJapaneseEggPickupPredicateKernel` has an explicit
callable ABI:

- Input: `a0` is an aligned, admitted native row owner; it must cover reads
  `+0C/4`, `+10/4`, `+14/4` and `+38/2`. This kernel does not certify row admission
  or execute its visual update; row Z is the current value supplied by the caller.
- Output: `v0=0/1` on normal return. The adapter establishes `s3` and the pinned
  USA player pointer explicitly.
- Preserves `s0–s7`, `fp`, `gp`, `sp`, `ra`, all t registers and HI/LO; may clobber
  `at`, `v0/v1` and `a0–a3`.
- A `0x20`-byte frame reserves the normal outgoing argument area and saves
  s3/fp/ra at offsets `10/14/18`. The exact helper needs no further stack.
  Restoration includes explicit R3000 load-delay spacing. The only stores are
  those twelve saved-register bytes; no row, player, model or save bytes change.

`NativeMipsBehaviorFragmentCodec` captures/relocates the 62 retained instructions
and their control/delay edges. The two native fragments plus intercepted
fallthrough use `0x100` bytes. Entry/return/result adapters make the complete
detached owner **`0x154` bytes**. Four code-owned global operands are rebound;
every other non-transfer source instruction, including delay slots, stays exact.
Caller-provided labels cannot issue source authority. Code placement requires
one aligned exact Final-domain owner and refuses overlap with its bound player
inputs. This is a proposed CPU owner, not proof of free native RAM.

The `0x20` frame is a **detached** requirement only. Future dispatch composition
must account for the existing visual adapter, interrupt reserve, actual caller
SP and non-overlap with code/row/player owners. Overflow tests stop at the
trapping instruction; they do not execute the PS1 exception handler or claim
stack restoration after an exception.

## Regression and remaining gate

New focused smoke: `Spyro.Editor.NativeJapaneseEggPickupPredicateSmoke`.
Build/run from the repository root:

```sh
dotnet build src/Spyro.Editor.NativeJapaneseEggPickupPredicateSmoke/Spyro.Editor.NativeJapaneseEggPickupPredicateSmoke.csproj --nologo
dotnet src/Spyro.Editor.NativeJapaneseEggPickupPredicateSmoke/bin/Debug/net10.0/Spyro.Editor.NativeJapaneseEggPickupPredicateSmoke.dll
dotnet src/Spyro.Editor.NativeJapaneseEggPickupPredicateSmoke/bin/Debug/net10.0/Spyro.Editor.NativeJapaneseEggPickupPredicateSmoke.dll --source "<exact-JP-BIN>" "<exact-USA-BIN>"
```

The tests use the existing independent `RenderProbeCpu` with its new opt-in
strict signed-arithmetic mode; the default remains unchanged. They exercise
417 nontrapping input combinations and eight extreme XY trapping cases against
both the original JP instruction fragment and the relocated callable kernel.
They verify instruction preservation, threshold edges/signs, signed row height,
short-circuit states, nonuniform globals, SP/register restoration on normal
return, exact read/store bounds, immutable copies and malformed-owner refusals.
Separate CPU controls prove ADD/ADDI/SUB suppress their destination on overflow,
ADDU/SUBU still wrap, delay-slot context is distinguished, and a preceding
delayed load retires. These are instruction-level tests, not gameplay acceptance.

Focused Debug build: zero warnings/errors. Final new smoke results: **3860
source-free checks; 3965 exact-source checks**. Shared-helper regression reruns
also pass: RenderAdmission 22/3206 source-free/source, InstanceRoot 6/4416,
and RgbVariantTail 113/4404. Both original image identities remained exact.

Before native pickup can be claimed, a separate collector must bind this result
to admitted per-instance identity, persistent state, retirement and reset/load
semantics. It must own the runtime hook and combined memory/stack budget. Sound,
effects, inventory/UI, required-percent logic, publication and observed gameplay
remain separate gates. No native output, emulator, card or settings were changed.
