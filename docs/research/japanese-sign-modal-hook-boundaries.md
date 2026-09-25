# Japanese font modal: source-bound hook boundaries, not an installation

2026-09-25. This bounded follow-up joins the explicit
[Dark Hollow atlas/backup allocation](japanese-sign-font-resident-allocation.md)
to exact USA modal caller boundaries. It does not alter an EXE, overlay, scene,
disc, emulator, settings, or UI. The earlier
[native-backdrop negative proof](japanese-sign-modal-texture-lifecycle.md)
still holds: ordinary pause restores only x512,y0,w256,h225, not the proposed
font mirror x512,y472,w512,h40.

`NativeJapaneseSignModalHookPlan` retains the actual source-issued resident
plan, the entire exact original USA executable, seven instruction-plus-delay
candidate sites, and16 hash-pinned native windows. It has no hook emitter or
installation API. `RequireInstallation` always refuses. Source identity is
USA BIN SHA256
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`, EXE
`a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`.
Every PC below is USA runtime PC, not a Japanese offset.

## Exact caller boundaries

| Candidate boundary | Original pair | Retained meaning |
|---|---|---|
| draw unknown-state gate `8001EF40` | `14620080 00000000` | compare v1 with state15 in v0; unequal exits at `8001F144`, equal continues `8001EF48` |
| update unknown-state gate `80033A14` | `14620086 00000000` | unequal reaches common tail `80033C30`, equal continues `80033A1C` |
| existing modal call `8001EE34` | `0C006903 00000000` | calls `8001A40C` only for states2,3,6; returns `8001EE3C` |
| first-frame backdrop drain `8001A5C0` | `0C017DD9 00002021` | requests `DrawSync(0)` after backdrop/palette upload; returns `8001A5C8` |
| later-frame UI list seal `8001C598` | `0C0059E1 00002021` | calls `80016784(0)` to seal the current UI list, before submission |
| ordinary pause restore drain `8002C57C` | `0C017DD9 00002021` | backdrop-only restore request precedes it; state0 store follows at `8002C588` |
| inventory restore drain `8002C7FC` | `0C017DD9 00002021` | same narrow backdrop owner; state0 store follows at `8002C808` |

These pairs are **candidate boundaries**, not proof of sufficient overwrite
space for a future adapter. They do not reserve code, a stack frame, controller
RAM, register saves, or an indirect-entry exclusion. The entire captured EXE
must match beforeimages; mutating any other byte also refuses the plan.

The source CPU probe executes both complete dispatch ladders for states0..31
andFFFFFFFF, honoring branch and load delays. Mode17 reaches both common exits
without a handler. Wrapping only `8001EE34` therefore does not install Japanese
mode17. A new mode needs both independently bound dispatches, while retaining
the legacy branches and their delay effects. The current plan emits neither.

The update function additionally calls `8005637C` and `8002BBE0` **before** the
mode ladder (`80033874/8003387C`) and `80056ED4` at the common tail `80033C30`.
Suppressing the ordinary world-update branch is not proof that callbacks and
other global work stop. Their complete interaction with a new sign controller
is still unclosed.

## First frame is not the later primitive phase

`8001A40C` samples modal frame counter `800758B8`. Its first-frame branch
requests the world rendering, synchronization, backdrop readback, conversion,
uploads and palette work. After the drain candidate `8001A5C0`, it requests
`VSync(-1)`, records its return and jumps directly to `8001C648`. The common
tail increments the frame counter and returns. It does **not** pass through
the primitive-cursor reset at `8001A604` on that first frame.

Only the later-frame path publishes primitive cursor `800757B0` from
`u32[800785E8]` and limit `80075780=cursor+1C000`. The probe executes these
actual words with the resident plan's normal reserved floor. The primitive
arena is distinct from the explicit atlas and backup owners, but this is
address separation, not IRQ/lifetime or peak capacity proof.

At `8001C598`, the native `80016784(0)` path reaches `80016854`, consumes and
clears the head/tail addressed by global `8007581C`, terminates the list, and
returns the submitted list pointer. `8001C5A0` passes that pointer to the native
submission wrapper. The ordinary modal then drains and returns. Calling the
font leaf **after** the original modal returns is too late for that frame's
already-sealed list; it is not a proved draw injection. A future renderer must
own a before-seal packet phase with a complete glyph count/capacity debit.

## A DrawSync return is not automatically success

The executable's initial SDK table pointer at `80074A5C` is `80074A1C`.
Relevant entries are:

| Initial slot | Routine |
|---|---|
| +08 | `80061820`, GPU request queue admission |
| +1C | `80061470`, readback operation |
| +20 | `80061234`, upload operation |
| +3C | `80061F48`, draw synchronization implementation |

`StoreImage` at `8005FA8C` and `LoadImage` at `8005FA28` dispatch through these
slots. Queue admission can return a pending depth0..63 or -1. A positive depth
is accepted work, **not** completed transfer. The source result calculation
`80061AC4..80061ADC` is executed by the probe across wraparound cases.

For `DrawSync(0)`, `80061F48` first waits for queue head/tail equality, then
DMA channel activity to clear, then GPU status bit26 to become ready. Its
successful return value is0. Timeout branches `80061F78` and `80061FAC` return
-1; the native timeout helper `800620C4` can reset queue indices and hardware
state. The new source window `80062090/1A0` is SHA256
`7bef961d538a8d9ee19a43bebcf616f04b6611877bba6c0682f9aeeab06365ac`.
Clearing a queue during an error cannot be interpreted as restoration success.

The CPU fixture executes the actual blocking-drain implementation with timer
and queue-pump callbacks intercepted and RAM cells standing in for status
registers. It proves the source distinguishes successful idle from queued,
DMA-busy and GPU-not-ready timeout cases. It does **not** execute GPU/DMA,
timers, interrupt callbacks, queue pump or hardware recovery.

Both ordinary resume functions ignore the returned status and immediately
write game state0. The first-frame backdrop path likewise does not test the
drain return before `VSync`. New font restore must not inherit that unchecked
success assumption. The source initial table is also not proof that the live
writable table is unchanged; live binding and callback ownership remain gates.

## Detached ordered contract and exact transfer requirements

The plan describes three fixed requirements, not performed calls:

- capture the complete mirror with `8005FA8C` into the resident backup;
- upload the exact Japanese atlas with `8005FA28` from the resident atlas;
- restore with `8005FA28` from that same backup.

All three use x512,y472,w512,h40 and exactlyA000 bytes. They retain the actual
typed CPU owners, not caller-supplied addresses. The current detached Dark
Hollow projection is atlas `801284E4..801324E4` and backup
`801324E4..8013C4E4`. Subsequent code/model/scene composition must recalculate
these projections; this plan cannot spend a stale resident gap.

`NativeSignFontProtocol` is an immutable, source-free **authoring protocol**,
not an implemented native controller. It orders:

1. successful entry drain;
2. accepted backup capture, then successful capture drain;
3. accepted atlas upload, then successful upload drain;
4. active text packet phase;
5. successful exit drain;
6. accepted full backup restore, then successful restore drain;
7. permission, in the abstract protocol only, to resume ordinary rendering.

It distinguishes admission depth0..63 from drain success0. A failure becomes
faulted and never automatically permits glyphs or ordinary rendering. Unknown
exits also fault. A strictly newer level/modal generation invalidates the old
backup but does not by itself prove the new level's textures are initialized.
Old-generation completions/restores are refused. No retry/reset path invents
valid backup bytes or silently resets earned/game state.

There is currently no native storage for that generation or protocol state.
Tests of this abstract order must not be promoted into native exclusivity,
all-exit or GPU completion claims. `Begin` is not a mutex or an allocation of
the sole native modal; separate managed protocol objects are not concurrent
native ownership tokens.

## Remaining smallest native composition

The next implementation must allocate one level-scoped controller/code owner,
compose both mode gates with exact register/stack continuations, and bind entry,
draw, close and abnormal-exit ownership. Font packets need the already-defined
three mirrored tpage/CLUT rewrites plus bounded before-seal UI capacity. The
first actual runtime scope should reject nested menus and unrelated modes,
check transfer/drain results, and close every way that scope can return to
world rendering. Return-home, title, death, reset, callback and level-replacement
paths remain unclosed; this slice intentionally does not claim otherwise.

All of `CompleteModalLifetimeProven`, `LiveGpuDriverIdentityProven`,
`ControllerRamAllocated`, `NativeHooksEmitted`, `InstallationApproved` and
`PublicationApproved` remain false, including source-issued captures.

## Focused checks

The standalone smoke shares only the existing bounded `RenderProbeCpu` test
interpreter, not generated expected hook bytes or an emulator. It covers
beforeimage/delay/table corruption, captured-output immutability, actual
resident ancestry, every admitted queue status, all failure stages, generation
refusal, native dispatch/first-frame/queue/drain consumers and false gates.
No shared runner or existing Core file was edited.

The final Debug build passed with zero warnings/errors. The source-free route
passed **675 checks**; the exact-JP/USA route passed **862 total checks**,
including those source-free checks. Plan SHA256 is
`d0e2638af7a9fcc0395c3df2c9f9003ae324b94cefc9959a74d9df94be3c98a3`.
Both original regional sessions completed held/named identity verification.
No candidate was written or emulator started by these tests.

```text
dotnet build src/Spyro.Editor.NativeJapaneseSignModalHookSmoke/Spyro.Editor.NativeJapaneseSignModalHookSmoke.csproj -c Debug --nologo
dotnet src/Spyro.Editor.NativeJapaneseSignModalHookSmoke/bin/Debug/net10.0/Spyro.Editor.NativeJapaneseSignModalHookSmoke.dll
dotnet src/Spyro.Editor.NativeJapaneseSignModalHookSmoke/bin/Debug/net10.0/Spyro.Editor.NativeJapaneseSignModalHookSmoke.dll --source <exact-JP-BIN> <exact-USA-BIN>
```
