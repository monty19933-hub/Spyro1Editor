# Detached authored-row collection attempt

September 27, 2026. This composes the existing exact authored-row plan,
source-bound pickup predicate and detached collectible collection leaf. It
does not install a hook, allocate native RAM, initialize a ledger, hide/retire a
row, write a save/card/disc, launch an emulator or authorize publication.

## Existing owners joined explicitly

`NativeJapaneseEggCollectibleBinding.Create(authored, manifest, mappings)`
requires one distinct nonempty persistent instance GUID for every authored
`RowId`, with no missing/duplicate/extra mapping. The existing manifest supplies
the stable full-width bit index through `GetEntry`; retired or absent entries
are refused. No GUID allocator, alternate identity schema or save serializer
is introduced. Placement order, RGB, class ID and native row index do not assign
collectible identity.

The binding retains the complete final data/overlay and canonical manifest
beforeimages, exact plan identities, final row/private typed extents, and final
projected addresses from `NativeJapaneseEggAuthoredModelBinding`. It uses the
final RGB-growth projection, not the earlier integration's scene RAM address.
`RequireCurrent` refuses a different manifest revision or authored composition.
Moving/reordering/relabeling a row requires a new binding with its same explicit
persistent GUID. Changing a label does not itself migrate or allocate identity.

`NativeJapaneseEggCollectionAttempt.Compile` accepts that binding, the current
authored plan/manifest, existing predicate/collector kernels, and a typed final
code capacity/address. It requires matching exact manifest and source ancestry;
fixtures cannot upgrade to source-bound evidence. All caller/predicate/collector
code, ledger, complete final model/scene and pinned player input ranges must fit
cached main RAM and be mutually disjoint. This is geometry, not native allocation
or proof that the current loader/session actually owns those addresses.

## Callable behavior and ABI

Input is `a0=exact bound row`, `a1=exact collector ledger base`. Wrong pointers
return before any data read. A bound row must have its expected fixed-up private
pointer at `+00`, full halfword class at `+36`, and live state zero at `+48` before
either callable is entered. Nonzero state is only refused; no new retirement
value or interpretation is invented.

The caller invokes the existing native predicate using current row coordinates.
False returns `NotEligible` without reading the ledger. True invokes the
collector using the saved ledger pointer and compiled UInt32 bit ID. Duplicate,
invalid-ledger and generation-overflow behavior remains the collector's existing
validated behavior. Duplicate checking occurs after eligibility, so this unit
does not independently query earned state or suppress a previously earned row.
It passes through collection results 0..5, with additional results 6 unbound row,
7 invalid row, 8 not live, and 9 not eligible. Retired/missing mapped identities
are already refused at binding time; the runtime caller cannot supply a bit ID.

Normal returns preserve `a0/a1`, `s0..s7`, `k0/k1`, `fp/gp/sp/ra` and HI/LO.
Other caller-saved registers and arguments may change. The caller reserves
0x20 bytes, including the outgoing argument area, and saves RA/row/ledger/bit at
frame offsets 10/14/18/1C. The predicate nests its existing 0x20-byte frame, for a
maximum declared depth of 0x40. The collector has no frame. Explicit MIPS-I
branch/load delays are retained. Native signed predicate overflow propagates
before the collector; no stack unwinding or PS1 exception-handler behavior is
claimed on that path.

Caller ownership/exclusivity of that full stack extent remains a precondition:
runtime SP, stack non-overlap and IRQ/transition safety are not proved by code
geometry. The caller must hold the exact scene generation and ledger exclusively
throughout the attempt. Structural row guards cannot authenticate a reused
address across loader sessions; no runtime epoch protocol is introduced here.
The collector's four successful stores remain non-atomic. Success writes only
the existing eight ledger bytes plus the declared saved-register stack bytes.
No model, private, native row, player, sound or effect state is written.

## Focused regression

The dedicated smoke was authored before the new production types; its initial
build failed with 34 missing-type errors. Both final runs use the unchanged
`RenderProbeCpu` with strict signed arithmetic and branch/load delays:

```text
JP_EGG_COLLECTION_ATTEMPT_SOURCE_FREE_OK checks=384 codeBytes=154 stack=40 nativeRowWrites=0 dispatch=false save=false
JP_EGG_COLLECTION_ATTEMPT_SOURCE_OK checks=441 codeBytes=154 stack=40 nativeRowWrites=0 dispatch=false save=false
```

Source-free fixtures use independently specified predicate instructions and
manual ledger bytes. The exact JP/USA route authenticates both original images,
executes their admitted predicate and actual USA scene arena/fixup consumers,
and rechecks source images on exit. Results are bounded instruction evidence,
not gameplay or persistence acceptance.

Checks cover reversed GUID/placement ordering, actual CPU collection after
moved/reordered/relabelled rows, bits255/256 together, exact complete read/write
sets, preserved row/private bytes and ledger complement, strict call whitelist,
pre-call pointer/class/private/live refusals, predicate rejection, duplicates,
wrong manifest/slot/count, overflow and native arithmetic traps. Stale bindings,
mixed fixture/source ancestry, malformed mappings and composed owner overlap
are refused. Existing native publishers, predicate, collector, residence and
shared CPU implementations are unchanged by this unit.

Run `dotnet run --project src/Spyro.Editor.NativeJapaneseEggCollectionAttemptSmoke`
without arguments for source-free checks, or append
`-- --source <exact-JP-BIN> <exact-USA-BIN>` for source-bound checks.

Native dispatch and source-proven placed-row retirement remain a separate
composition gate. The older placed-row deletion research and its newly focused
authored-row verification must be used for that gate; this caller deliberately
does not call generic kill/effect `8003B9D4`, whose high-class truncation is unsafe.
Visual scheduling, render-list rebuild after retirement, already-earned
suppression, slot/load/death behavior, inventory and card transactions remain
outside this callable's authority.
