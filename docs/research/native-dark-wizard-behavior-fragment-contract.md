# Detached native Wizard behavior fragments and owned-frame adapter

This stage is a strict relocation codec and an exact-source code package, not
an installed native enemy. It does not write an overlay, activate class dispatch,
publish a BIN, choose a runtime code allocation or approve a Dark Hollow route.
The previous adapted Wizard profiles and Town V2/V3 outputs remain separate.

## Actual source package

`NativeDarkHollowWizardBehaviorSource.Capture` pins the full original image,
Wizard/Dark overlays, executable and native ABI certificate spans. The source
Wizard overlay is WAD `0x2F5B000 + 0x10000`, SHA
`5d3996111567ffac6eca08d340f2f691824bb7969ba2a0cd95e9346ccfe157e1`.
Dark is WAD `0xEF6000 + 0xB000`, SHA
`9920aa47b94005166f457b5b36df6dedd1f2400f91698bac21cdb01d8590ef32`.
Overlay file offset is always source runtime PC minus `0x8007AA38`.

The seven main fragments retain 558 native instructions (`0x8B8` bytes):

| Source PC | Length | Purpose |
| --- | --- | --- |
| `0x8007D118` | `0x1C` | Wizard death helper tail. |
| `0x8007EAD0` | `0x1A0` | Native lightning update and four-child impact branch. |
| `0x80082818` | `0x668` | Native Wizard update. |
| `0x800842CC` | `0x24` | Reachable shared main-loop tail. |
| `0x8008434C` | `0x14` | Reachable shared main-loop tail. |
| `0x80085A4C` | `0x4C` | Reachable shared main-loop tail. |
| `0x80086A50` | `0x10` | Native deletion path. |

Three additional source-owned spawn fragments are captured now:

| Source PC | Length | Still-required binding |
| --- | --- | --- |
| `0x80086DD8` | `0x94` | Native `0x58` frame, allocator `0x800524C4`, class/parent durable-owner initialization; branch/fallthrough into the broad class dispatcher must be specialized only for proved class `0x26` inputs. |
| `0x800870F0` | `0x230` | Exact lightning initializer for Wizard-parent model states 0/1/2 and lightning-parent impact state 3; includes native four-particle-`0x07` callback at `0x8008728C`. |
| `0x80087B08` | `0x38` | Native finalizer `0x800526A8`, returned actor and source-owned frame restoration. |

Every direct branch/jump/call, source delay word, register callback and fragment
fallthrough is represented. All LUI literal words and exact EXE entry instruction
certificates are retained; these are address/code evidence, not proof of all
transitive data, global or GTE consumers. Main code has six callback sites;
spawn adds a seventh. Missing callback or continuation bindings remain explicit.

The native impact path is pinned independently by its instruction words:
`0x8007EB88/90` construct electric flags `0x20020`; `0x8007EBC0` tests the
capacity-minus-active count against `0x15` (21 decimal); `0x8007EBE8` retains
the four-child loop bound. There is no generic-hurt substitution or child
suppression. The source package includes no instruction at donor `0x80086D8C`.

## Owned main-loop frame

Dark and Wizard both use current actor `s3` and Spyro-coordinate base `s7`.
The adapter does not swap them. It sets donor-required `fp=1`, independently
reserves `0x2B0` below the current Dark SP, and preserves all 30 nonzero/non-SP
GPRs plus HI/LO in its upper `0x80` bytes. The lower `0x230` is reserved for the
donor-compatible frame. It never borrows Dark's saved-register slots or cursor.

Direct main-fragment stack stores end at `+0x34`, with no direct stack loads.
This does **not** bound helper writes: lightning instruction `0x8007EB34`
(`0x27B20068`) derives `s2 = sp + 0x68` for native vector-helper scratch.
The conservative `0x230` lower frame follows the source ABI; no claim is made
that all those bytes are necessary or that nominal size proves runtime stack fit.

The generated exit restores HI/LO with explicit R3000 load-delay NOPs, restores
the saved GPRs, and jumps to Dark `0x80082914` with exact SP restoration in the
jump delay slot. That native continuation loads its cursor from **original
Dark SP + `0x188`**, then advances to the next native actor. The donor's
`0x80086D8C` cursor load from `sp + 0x1C0` is intercepted before execution.

The source-backed Dark continuation `0x80082914 + 0x4C` hashes to
`93e30da4cb5ceba3232f2ef282fff31f44c453a6833e8aa70f25d5ca7f0f0a36`.
A separate tiny test interpreter executes the generated adapter with branch
delay and one-instruction load delay, poisons all donor scratch/GPR/HI/LO
values, then verifies exact restoration and the actual native cursor load.
This is an instruction-level adapter regression, not emulator gameplay or
interrupt/GTE/global-state/runtime-stack acceptance.

## Relocation and ownership limits

The internal codec verifies the exact returned source length before cloning,
then complete source preimages. It bounds fragment count, byte budget,
instruction variants and delay slots. Entry, exit and external-binding requests
are captured with bounded enumeration, independently of caller-reported list
counts. It refuses unproved computed
transfers, branch displacement overflow and missing, stale, duplicate or unused
external bindings. Fragment fallthrough uses an explicit typed jump/NOP tail;
independent entry into an otherwise-final delay word retains its own ordinary
fallthrough obligation. Adjacent source fragments must be captured as one span
in this bounded codec rather than creating ambiguous synthetic edge identities.

All final targets must share the one canonical reserved code domain and its
runtime-address mapping. Unchanged global EXE targets require the exact original
address plus full supplied source certificate; a different source routine is
not accepted as equivalent. Native factories/integration own semantic bindings;
there is no public API granting native authority from caller-authored addresses.

A final external target reservation may be filled by the later adapter/effect
integrator. Its bytes are **not** supplied merely by relocating the call.
The entire reservation must be outside the actual emitted code prefix; it
cannot claim instructions already owned by the linked fragments. An immediately
adjoining reservation is allowed, including at a nonzero owner offset.
The detached result exposes these as `UnmaterializedExternalTargets`, and
`CompleteInstalledCodeOwnerProven` remains false. All public source package
closure/runtime/publication flags also remain false.

The subsequent [private class-`0x26` specialization](native-dark-wizard-class26-specialization.md)
now carries the exact source-call and single-instruction proof separately.
Next required work is source-certified particle `0x07/0x41` spawn/update integration, actual owned
main adapter/callback destinations and active class roots, one final resource
and code memory layout, valid source-bound route/collider placement, then strict
candidate readback and runtime attack/hurt/death/re-entry testing. Source-byte
capture or a successful relocation never skips those gates.

## Regression status

Tests were added first. RED built with zero warnings/errors and failed at the
unimplemented fragment capture after all prior source-free groups passed.
The focused Debug GREEN build also passed with zero warnings/errors. Complete
closure-suite counts are **299 source-free / 1,101 source-backed checks**;
these are aggregate suite counts, not only new behavior assertions. All prior
scene/composition and Town V2/V3 byte goldens remain exact.

An independent raw-sector Node inventory agrees with the 749-instruction,
10-fragment package and its 26 distinct direct EXE entry targets. Manifest SHA:
`a9a8227c74a9c479f7f94c82314813809bd5caa0a5a137b0c18d614b80b3e3df`.
The source issuer currently lists 57 unresolved callback, branch/continuation,
transitive consumer and integration obligations; it does not claim closure.
Full focused output is retained in
`_local/research/dark-wizard-native-closure-20260906/behavior-focused-green.log`.
Independent review then identified missing bounded-list/pre-clone guards and
an external-reservation overlap refusal. Finite regression fixtures were added
first; RED reproduced the overlap acceptance. The corrected guard gate above
and its full output are retained in
`_local/research/dark-wizard-native-closure-20260906/behavior-guard-red-green.log`.
