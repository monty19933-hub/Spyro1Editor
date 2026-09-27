# Detached top-reservation mechanics

September 27, 2026. Source-bound afterimage and bounded CPU tests only.
`NativeCollectibleReservationMechanics` consumes the existing source-captured
`NativeCollectibleSelectedSlotResidence`; a neutral descriptor is refused.
It retains the complete original USA executable, produces a detached copy with
only the four-byte field at EXE `65DA4` / PC `800755A4` owned for the change
`800 → A00`, and requires every byte of the complete original/afterimage for
application or exact reversal. All complement bytes, including the separate
stack-origin word, remain unchanged. Returned byte arrays are copies.
There is no file writer, initialization hook, allocator issuer or publisher.

The original residence beforeimages are reused. One additional exact source
window, `80016914/1C`, pins the actual clear helper invoked twice by the native
frame allocator. Its SHA256 is
`572bd776ba509d54a3848293f5cf78731da439e3b203d100b9cb6fb2ec60decc`.
No cold-boot hook or unused-code allocation is part of this boundary, so no
new cold-boot callsite pin or installation is introduced.

All geometry below is hexadecimal. The proposal charges the complete `200`
reservation even though the two-egg selected-slot layout uses only `B0` bytes.

| Quantity | Original | Proposed |
| --- | --- | --- |
| Native allocation ceiling T | 801FF800 | 801FF600 |
| PETE resident base | 801C5BB8 | 801C59B8 |
| Normal frame floor | 80187BB0 | 801879B0 |
| Startup CD request start/end | 801BF800 / 801FF800 | 801BF600 / 801FF600 |
| PETE temporary CD start/end | 8018B800 / 801C5800 | 8018B600 / 801C5600 |
| PETE copy source/nominal end | 8018C000 / 801C5C48 | 8018BE00 / 801C5A48 |
| Actual prefetched source end | 801C5C50 | 801C5A50 |
| Boot SP | 801FFFF8 | 801FFFF8 |
| Initial heap byte count | 184DC0 | 184BC0 |

The copy's actual source-read overhang `450` and source/destination overlap
`98` remain explicit. These quantities are not owned spare space or a promise
that moved runtime tail contents are identical to the original.

## Exact-source CPU boundary

The dedicated smoke links `RenderProbeCpu` unchanged, with strict signed
arithmetic and R3000 load/branch delays. Complete original and proposed EXE
copies are used; original instructions and native helper bodies are executed.
No helper is replaced with a synthetic success result.

- All seven reserve readers run with both original and proposed values:
  startup CD (`12954`), boot FC reset (`12C70`), two alternate resets
  (`33084`, `3325C`), PETE staging/resident copy (`5B824`, `5B84C`) and heap
  capacity (`5B930`). Service calls stop at their actual entries after their
  delay slots; BIOS, CD and asynchronous completion are not executed.
- The source allocator `8005B6F8..8005B7D4` runs through both real calls to
  `80016914`. It is tested with FC at the proposed PETE resident base and at
  proposed T, each with mode 0 and 1. The two cross-combinations are arithmetic
  cases, not assertions that native control flow reaches those combinations.
  Exact pointer globals, both renderer control sets, native clear extent
  `F0/8 + F4/4000`, allocator stack return and complete write footprint pass.
- The native particle-initialization window `8001441C..8001444C` executes
  through its JAL delay-slot sentinel store, stopping at the next callee.
  Its thirteen written bytes are checked exactly. When FC=T, only the first
  four guard bytes change; all other guard, metadata, bitmaps and reserved
  tail bytes retain their poison patterns. Normal FC leaves the entire
  proposed reservation intact.
- The complete native `39C48` forward copy executes on synthetic initial RAM.
  To retain the unchanged interpreter's per-Run limit, the smoke stops and
  resumes each full group at native PCs `16960` and `16998`, where all pending
  loads/branches have retired. Registers, RAM and original instructions persist
  between runs. The final short group executes through the real return.
  Every destination byte is written once, the last eight-byte group reads
  sixteen bytes, and the proposed guard/ledger remains unchanged. This checks
  actual instruction extents, not original PETE payload equivalence.

## Regression and limitations

Tests were added before the production type; the initial Release run failed
on the missing `NativeCollectibleReservationMechanics` type. Final results:

```text
COLLECTIBLE_RESERVATION_MECHANICS_SOURCE_FREE_OK checks=12 sourceReads=0 nativeResidenceIssued=false
COLLECTIBLE_RESERVATION_MECHANICS_SOURCE_OK checks=107 allocatorCases=4 reserveReaders=7 fullNativeCopyBytes=39C48 imageWrites=0 cardWrites=0 nativeResidenceIssued=false
```

```sh
dotnet run --project src/Spyro.Editor.NativeCollectibleReservationMechanicsSmoke --configuration Release
dotnet run --no-build --project src/Spyro.Editor.NativeCollectibleReservationMechanicsSmoke --configuration Release -- --source '/Users/john/Documents/Codex/2026-04-24/could-you-create-a-level-editor/Spyro the Dragon (USA).bin'
```

Source tests also reject field/complement mutations, truncated/extended
executables, unapplied-original reversal and neutral ancestry. They verify the
complete actual original executable and held/named original image at the end.

All native residence, installation, lifetime, whole-engine fit, card and
publication gates remain false. The tests do not establish all transition
callees/computed pointers, IRQ/callback stack peaks, GPU/CD/SPU DMA lifetimes,
hardware destination alignment, cold-boot ledger initialization, selected-slot
ownership, transactions or gameplay. The lowered static normal floor is not an
engine-fit certificate. No ROM, app, card, emulator or settings were modified.
