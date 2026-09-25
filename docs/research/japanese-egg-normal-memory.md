# Two-F5 Artisans: normal transfers and synchronous stack

September 25, 2026. `NativeJapaneseEggMemoryReport` consumes the actual
authenticated two-instance integration plan. It narrows two previously open
questions: complete normal-load transfer geometry and the synchronous visual
update's stack footprint. It does not turn the existing normal resident report
into a whole-engine peak, interrupt, renderer, GPU, transition or publication
certificate. No candidate or emulator is written by this report.

## Source and normal-load owners

The complete original USA executable and original Artisans overlay/data must
match both the regional source session and integration ancestor. The candidate
must match that ancestor's complete afterimages. Twelve named executable ranges
bind boot, allocator, loader, CD rounding and the synchronous call chain; the
existing source-certified integration binds model/scene lengths and code.
All addresses and sizes below are hexadecimal.

| Phase or owner | Address / length / endpoint |
| --- | --- |
| Complete appended overlay and following copy anchor | `8007AA38 + E800 = 80089238` |
| Paired texture CD staging | `80089238 + 80000 = 80109238` |
| Reused sound bank staging | `80089238 + 6E000 = 800F7238` |
| SPU CPU/DMA transfer source | same anchor, `7EFF0 / 7F000`, within preceding texture staging |
| Full level data CD read | `80089238 + 8D800 = 80116A38` |
| Actual used level data (model anchor) | `80089238 + 8D31C = 80116554` |
| Full final model CD read | `80116554 + 4E800 = 80164D54` |
| Full scene CD read | `80164D54 + 14800 = 80179554` |
| Lowest normal renderer allocation | `80187BB0` |
| Final normal-load gap | `E65C` |

The fields at header0/4 and8/C remain unchanged from Artisans. Native staging
is reused in sequence, not simultaneously reserved and added together. The full
data read includes `4E4` bytes after the used-component return; model placement
uses the used return, not the rounded CD length. Complete model and scene reads
are charged. These are transfer-source/destination extents, not hardware
completion, SPU behavior or asynchronous lifetime claims.

## PETE temporary source is not its resident copy

`8005B7D8..8005B8DC` reads directory entry8 length `3A000` through global
`8007A714`. It places the temporary CD request at
`80200000 - 800 - 2*3A000 = 8018B800`; that read ends at `801C5800`.
This is **not** calculated using the `39C48` runtime-copy length.

After the read, the first header word supplies `39C48`. The copy source is
temporary+800 (`8018C000`), and the resident destination is
`80200000 - 800 - 39C48 = 801C5BB8`. Therefore the native copy source ends at
`801C5C48`, **448 bytes beyond its preceding CD request**. This unchanged native
overhang is reported explicitly. It is not certified as freshly read disc data,
zero-filled, silently reduced, or claimed to be an unused memory hole. The actual
copy routine `80016958/54` prefetches four words per group before conditionally
storing them. Its final eight-byte store still reads sixteen bytes, so the real
source-read end is `801C5C50` (450 beyond the CD read). Nominal source/destination
overlap is90 bytes; actual read overlap is98. This is a forward copy, not memmove:
late source bytes may reflect earlier destination writes. An offline snapshot of
the WAD overhang is **not** proof of identical runtime PETE tail contents. Neither
the directory entry nor shared PETE bytes is modified by this integration.
The entire temporary read/copy footprint is above the final level endpoint.

The existing allocator subtracts `6008` and two `1C000` regions from the
resident base, producing `80187BB0`. Alternate `a0=1` buffer mode and title,
rescue/replay/reset overlaps are not covered by this ordinary-mode analysis.

## Actual synchronous visual stack

The native boot word `800755A8=200000` and sequence `8005B904..8005B918`
produce SP `801FFFF8`. The ordinary chain is:

`main80012204 (-20) -> update8003385C (-20) -> Artisans8007D9C8 (-1C0)`

The main's direct call and the update's `80033AA4` indirect call are bound.
The Artisans initializer at `8005A738..8005A744` assigns that callback to
`80075734`. Thus SP entering the F5 guard is `801FFDF8`, not a synthetic test
address. The reused owned adapter allocates `2B0`; its deepest native helper,
turn80038EE0, allocates `28`. Sine, RNG and the called angle leaves allocate no
stack. The minimum synchronous SP is `801FFB20`, leaving `320` above the
source top-reservation boundary `801FF800`. Instances run sequentially and do
not multiply this stack depth.

This establishes the selected visual call chain, not an interrupt-inclusive
whole-engine peak. The report keeps `FullRuntimePeakProven` and
`PublicationApproved` false. Rendering allocation/high-water use, GPU/GTE state,
modal or transition paths and visible gameplay still need their own evidence.

## Regression

`Spyro.Editor.NativeJapaneseEggMemorySmoke` was introduced tests-first. Initial
RED was the intended missing report. The first source run caught three manually
miscomputed PC-to-file offsets in assertions; they now use explicit
`PC - 8000F800`. A second run caught a test stop before a pending native LW had
retired; the probe now executes the real JAL/delay pair and stops at the copy
callee entry. No native instruction was altered to accommodate these tests.

The initial Debug pass had17 source-free and154 source checks. Independent
review then corrected the evidence span for the main's direct update call and
identified the native copy's extra eight-byte prefetch, now explicitly tested.
Final reviewed Debug build passed with zero warnings/errors; **18 source-free
and158 exact-source checks passed**.
Checks include execution of actual boot/prologue
instructions, PETE request/copy argument setup and 32 ticks per real relocated
instance with the actual caller SP. Tests cover rounded-vs-used lengths, exact
boundaries, one-word overflow, malformed inputs and changed source refusal.
The interpreter models R3000 branch/load delays; it is not a GPU or PS1 runtime.

Logs: `_local/research/japanese-content-20260925-r01.RCmBCn/egg-memory-*`.
Default regression uses the source-free case. The dual-source route is explicit:
`--source <exact-JP-BIN> <exact-USA-BIN>`; it must not receive the USA-only source
runner's arguments.
