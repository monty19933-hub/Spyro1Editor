# Japanese font mirror: native modal restore is insufficient

2026-09-25. This read-only follow-up to
`japanese-sign-font-vram-lifetime.md` establishes a concrete negative result,
not a complete modal hook or installation certificate. No native bytes, ROM,
card, settings, emulator, editor state, or previous candidate were changed.

## Exact owner and ordinary resume

The USA original is SHA256
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`,
661,547,040 bytes. Its complete executable is SHA256
`a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`.
`tools/Inspect-SpyroUsaModalTextureLifetime.py` authenticates both, pins 23
source windows, and verifies the complete held and named original again before
returning evidence. All instruction addresses below are USA runtime addresses.

| Native request | Request PC | Word rectangle | CPU owner |
|---|---|---|---|
| Initial backdrop backup | `8001A508` | x512,y0,w256,h225 | `u32[800785F0]-1C200` |
| Ordinary pause resume | `8002C574` | same | same |
| Inventory resume | `8002C7F4` | same | same |
| Proposed whole-font mirror | uninstalled | x512,y472,w512,h40 | unallocated |

The backdrop backup is exactly `1C200` bytes. Its rectangle has **no overlap**
with the proposed `A000`-byte font mirror at right-half upload offset `76000`.
Both resume paths request `LoadImage`, then `DrawSync(0)`, then write
game state `800757D8=0` (`8002C588` / `8002C808`). Neither restores any byte
of the mirror. This ordinary exit is sufficient to refute reuse of the native
backup as a whole-font restoration owner; uninspected exits cannot repair it.

Capture occurs only when modal frame counter `800758B8` is zero. After world
draw synchronization, the native routine saves the backdrop region. Four
passes then read 128x224-word framebuffer strips into `u32[800785E8]`, wait,
transform them through `80017E98`, and upload 64x224-word strips to the
backdrop. Its 32-word palette is uploaded at x512,y224. This all stays within
the saved rectangle, not within or below the mirrored atlas.

The bounded scalar fixture executes the actual capture/resume/upload argument
words, honoring load and JAL delay slots. It intercepts GPU calls: requested
ordering is proved, GPU completion is not. It does not execute entire menu
functions or synthesize their callees. A separate deterministic byte-owner
counterexample changes every mirror byte and applies the exact packed native
backdrop restore: all **40,960 mirror bytes remain changed**. Changing the
backdrop instead restores all 115,200 bytes, so the negative result is not an
always-failing restore model.

## What the inspected exits do, and what remains unknown

| Path | Pinned source behavior | Ownership consequence |
|---|---|---|
| Pause enter `8002C420/114` | Sets state2; normal entry resets modal frame counter | First modal draw creates backdrop backup |
| Inventory enter `8002C714/A8` | Sets state3; normal entry resets counter | Same owner |
| Nested pause/inventory | `8002EA34` enters inventory with0; `8002EDAC` enters pause with0 | Retains original capture, not a second backup |
| Ordinary resume | Complete `8002C534/E4`, `8002C7BC/A0` | Restores backdrop only, then returns to gameplay |
| Return home | `8002C618` fade; `8002E084` leads to `8002C664/B0` | Starts normal level loading, not synchronous same-level mirror restore |
| Quit to title | Within `8002E12C/A00`, `8002E27C..8002E380` clears display, loads WAD2 overlay and loops loader | Invalidates/reloads resources; no reusable mirror restoration claim |
| Death entry | `8002C85C/48` chooses state4 or5 | No new font lifetime is defined |
| Death scene reset | `8002C8A4/70`, `800144C8/9C` | State0/player restore followed by scene read/reinitialization; not evidence of full texture reload |
| New-game reset | `80012604/178` | Progress reset is pinned; complete graphics/reentry lifetime is not proved |
| Indirect overlay/card exits | Mode7 callback through `800757C0`; other overlay callbacks | Still unclosed |

The draw ladder `8001EDDC/254` sends states2,3,6 to the modal draw. The update
ladder `80033884/1D0` dispatches those states to their native handlers.
Neither establishes a state17 Japanese sign handler. Normal modal dispatch
bypasses world update, but that is not a proof that no global callback,
exception, death, reset or transition can affect a future sign modal.

The exact executable direct J/JAL census finds only `8001EE34` to modal draw,
`8002E9F0` to pause resume and `8002EDD8` to inventory resume. This census does
not cover indirect calls or overlay code. No all-exit closure is claimed.

## Native RAM and CD staging do not supply a spare backup

`8005B6F8/E0` derives the frame arenas from `u32[800785FC]`: globals F8/F4/F0
are FC minus `2000`, `6000`, `6008`. In normal mode the next two arenas are
each `1C000` bytes. The backdrop save at F0 minus `1C200` borrows this existing
render workspace. Adjacent bytes are not unowned, and extending the backup
downward by `A000` has no source certificate. Alternate buffer mode uses a
different `13000` stride.

Nor is the old complete texture image retained in CD staging. The two normal
uploads at `80015770/60` read right-half halves from `u32[800785DC]` and that
pointer plus `40000`; `800157CC..80015830` then reads an audio-bank prefix
over the same buffer. Subsequent data/models/scene loading further changes
its owners. See `native-id65-normal-upload-staging.md`: retained VRAM residue
is not a complete old atlas and cannot be passed to a later full restore.

## Smallest plausible positive owner: explicit resident extent, not a heap gap

Read-only feasibility finds a reusable geometry mechanism, not an existing
general-purpose allocator that already grants this font lifetime:

- `NativeLevelDataGrowthLayout` can append an explicitly produced, aligned
  extent to model subfile2, grow its descriptor, shift later subfiles and
  preserve existing roots. Its own contract explicitly leaves resource
  semantics and runtime fit to the producer.
- Native `800159FC/48` reads the complete model descriptor length from
  `80076C14` into `u32[800785E0]`. At `80015B34..80015B5C`, the scene address
  becomes that model pointer plus the full descriptor length. A new explicit
  model-tail extent therefore occupies actual loaded RAM and shifts the
  scene; it is not a guessed hole in model padding or the Moby arena.
- The generic registration loop `80015AD8..80015B30` walks only registered
  roots. The proposed backup must be a **nonregistered** level resource,
  never a fabricated actor class. `NativeModelTailBatch` is actor-specific and
  is not the correct issuer. Existing model/package ownership and pointers,
  including the final registered package's full bounded closure, still need
  validation against the appended extent.
- `NativeLevelRuntimeMemoryReport` already accounts for used level data,
  complete model CD read, scene CD read and reserved buffers. Growing models
  by `A000` shifts scene and later resident demand by that amount. A positive
  post-load gap alone is not transient/stack/render fit. The new owner must
  compose with the complete final level, not independently spend that gap.

A minimum subsequent semantic producer could wrap the existing growth kernel
and issue separate relative owners for (a) mutable `A000` backup, (b) immutable
font upload source, and (c) controller state and typed CPU/GPU references.
If the atlas is resident too, it costs another `A000` plus controller/code/data
and alignment—not just the backup. Streaming it instead needs a separately
proved CD staging and synchronization lifetime. No such producer is added here.

The required controller sequence remains: reject nested ownership; finish
ordinary draws; `StoreImage` the full mirror into its dedicated owner;
`DrawSync(0)` before dependent reuse; upload the font from its owned source;
complete upload before text draws; on close drain final text work, restore
the full backup, drain again, and only then reenter ordinary rendering.
Reset/level replacement must invalidate the old generation and establish the
new texture resource before any ordinary draw; it must never restore a stale
backup into a new level. Native wrappers `8005FA8C/64`, `8005FA28/64` and
`8005F764/6C` are pinned, but a new caller/state owner and complete exit hooks
are still required. This is a proposed contract, not implemented hooks.

## Validation and retained artifacts

`python3 tools/test_usa_modal_texture_lifetime.py` passes nine source-free tests:
exact geometry, full/partial restore complements, native backdrop success,
load/JAL delays, state-write order, malformed/unknown instruction/service/memory
refusal and non-disc identity refusal. The exact-USA source run verifies all23
pinned windows and both whole-original final checks. No .NET build was needed.

New-only report:
`_local/research/japanese-content-20260925-r01.RCmBCn/usa-modal-texture-lifetime-source-final.json`.
The earlier 20-span report `usa-modal-texture-lifetime-source.json` is retained.
All report gates remain false: whole mirror restored by native resume,
complete exits, GPU completion, native installation and publication. The
counterexample is actual byte restoration over fixture data, not GPU runtime.
