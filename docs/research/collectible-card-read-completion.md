# USA native card-read completion seam

The native read API returns **request acceptance**, and the native poll returns
**completion consumption**. Neither return value alone proves a successful read.
A successful completion attributable to an accepted read has operation **3** and result
**0**. The selected-save call at `8007CA98` uses the poll's **blocking mode 0**;
the nonblocking mode is any nonzero value. These are source facts, not an
implemented hook or evidence of card service execution.

This note extends [selected-save admission](collectible-selected-save-admission-boundary.md)
and [buffer ownership](usa-save-buffer-ownership.md). Its investigation is bounded
to the selected poll, read request/callback, completion publisher and shutdown.
It does not settle persistent ledger ownership or save transactions.

## API, completion and status words

`80066E28(controller, filename, buffer, offset, count)` takes the fifth argument
at the caller's `sp+10`. The native prefix callers pass controllers 0/16 and
`BASCUS-94228SPYRO`. The API rejects `g80075B50 != 0` and `count & 7F != 0`.
It does **not** check positive length, buffer extent, offset alignment or file
extent. On the accepted path it builds the filename, stores operation 3 at
`80066EB0`, clears `g75B54` and `g75B58`, copies offset/buffer/count/controller
to `g75B64/g75B6C/g75B68/g75B5C`, and queues callback `80066F34` at `80066EE4`.
Return 1 at `80066EF0` means accepted; rejected requests return 0 at `80066F04`.
In particular, accepting a new request clears an **unconsumed completion**.

| Global | Native role |
| --- | --- |
| `80075B50` | Current operation: 0 idle, 3 read. Not the callback's internal state. |
| `80075B54` | Current result, initially 0. A pending zero is not success. |
| `80075B58` | Completion latch, cleared by starting a request or consuming completion. |
| `80075B60` | Descriptor; read callback sets it to -1 after close. |
| `80075B94` / `80075B98` | Published completed operation / result. Values can be stale when latch is 0. |

`80067628(mode, operationOut, resultOut)` is a leaf occupying `[80067628,80067718)`.
Output pointers may be null. Its result is a signed value:

| State at poll | Return | Output writes | Latch |
| --- | ---: | --- | --- |
| active=0, latch=0 | -1 | Neither output is written. | Remains 0. |
| active!=0, latch=0, mode!=0 | 0 | Current operation/result (`5B50/54`). | Remains 0. |
| active!=0, latch=0, mode=0 | Does not return yet | Spins at `80067684..80067690`. | Waits for nonzero. |
| latch!=0 | 1 | Completed operation/result (`5B94/98`). | Cleared at `80067708`. |

The idle decision uses `80067638/4C`, pending/nonblocking writes use
`800676B8/C8`, and completion writes use `800676E4/80067700`. Do not treat
nonzero return as success: **-1 is nonzero**, and **1 can carry an error**.
Outputs from a -1 poll must not be reused as fresh evidence.

All three selected routes set `a0=0`, `a1=s1+3C`, `a2=s1+40` before CA98;
here `s1=80078D7C` from `8007C554/558`, so native outputs are `80078DB8/80078DBC`. The setting PCs
are `C7A0/C7A4/C7C0` (empty native slot), `C884/C888/C8B0` (confirmed overwrite),
and `CA3C/CA40/CA5C` (deserialize). CA98 drains earlier card work, then the
overlay jumps directly to its epilogue at `CAA0`. It does not issue another read.

## What publishes read completion

`800662BC` initializes operation/result/latch and installs `80067CD4` into
callback channel 7 at `80066304`. Callback stack push `80068F44` allocates a
zeroed four-word state record; step `80068FC4` calls its top callback and pops
it only when that callback returns nonzero. `80069030` reports whether the
stack index is negative (empty).

`80067CD4` checks for a nonempty stack, steps it once, then checks for newly
empty. Only then does it set latch 1 (`67D10`), copy operation/result to their
completed globals (`67D20/38`), clear the current pair (`67D3C/44`), and optionally
call the registered result callback at `g75B90`. The foreground polling path
does not itself advance this stack. Correct interrupt/callback delivery is an
external precondition, not proved by source-word checks or CPU boundary stubs.

The read callback state table at `800120C4` has states 0, 10, 11, 20 and 30:

| State | Target | Observed action |
| ---: | --- | --- |
| 0 | `80066F74` | Queue card precheck `800663D8`, reset read retry counter; next state 10. |
| 10 | `80066F98` | If precheck result is nonzero, complete with it. Otherwise open with flags `8001`; negative descriptor becomes result 5. |
| 11 | `80066FD8` | Seek to requested offset until the returned position equals it; next state 20. |
| 20 | `80067008` | Clear event latches and submit read with the unchanged caller buffer/count until veneer returns 0; next state 30. |
| 30 | `8006703C` | Wait for event bits, decode result, retry raw code 1 while attempt counter is below 16; otherwise close and complete. |

Open, seek, read and close veneers are `800684B4/C4/D4/E4`, dispatching BIOS
B0 selectors `32/33/34/35`. A successful asynchronous read call here returns
**0 from the submission veneer**; that is a different layer from the API's
accepted return 1 and poll's completed return 1.

The event mapper `80067C7C` maps raw 0→0, 1→2, 2→1, 4→3; other values become
`raw | 8000`. Open failure is result 5 directly. The precheck can produce the
same mapped results. No unverified symbolic error names are attached here.
The ordinary menu itself only proceeds to native checksum checks when its
result output is zero (`8007B390/398`).

At final read completion, result is stored at `800670A8`, close is called at
`800670AC`, descriptor -1 is stored at `800670BC`, then the callback returns 1.
Only subsequent callback-stack publication makes operation 0/latch 1 visible.
There is no returned byte-count comparison proving the caller buffer was
completely filled; that conclusion depends on actual service behavior.

## Smallest extra-read sequence before shutdown

The minimum source-compatible sequence is pending selection → drained native
operation → one accepted read → nonblocking completion checks → resolved read
→ native shutdown. This is a sequencing constraint, not runtime authorization:

1. At CA98 retain the ordinary native drain and its original outputs. Capture
   the selected tuple/provenance separately. Do not use that drain's outputs as
   completion of a new request.
2. Before issuing the extra read, establish no active operation and no pending
   completion. Own and keep the destination buffer valid through completion.
   The existing API does not prove these caller conditions. Record pending only
   when the new read returns 1; return 0 is refusal, not a request to poll.
3. While that read is pending, call `80067628(1, ownOperationOut, ownResultOut)`
   from a repeated pre-shutdown gate. Return 0 keeps the gate pending and
   returns to the normal update; do not advance to shutdown or outer phase 3.
   Return -1 loses the expected completion and is refusal. Return 1 consumes
   exactly one completion; require operation 3 and result 0 before inspecting
   the buffer as a successful read. Other operation/result values are refusal.
4. Resolve the read and any buffer validation before the `8007CC00` shutdown
   call. A consumed read completion is not custom Ready authority, freshness,
   identity, or persistent-RAM ownership. Invalid bytes must remain refused.

Native shutdown `8006631C` only spins until `g75B50==0` (`6632C..66338`),
unregisters channel 7, then invokes close/stop services. It does not consume the
completion latch or check a result. The overlay sets outer phase 3 later at
`8007CC20`. Calling shutdown while pending forfeits the caller's opportunity
to check the result and uses another unbounded wait.

The existing library has **no source-proven bounded completion time**: seek
retries at `80066FF8`, read submission retries at `8006702C`, event waiting and
mode-0 polling can all wait indefinitely. The retry counter limits only one
specific reported event case. Nonblocking foreground polling does not make
those underlying service paths bounded, and fabricating callback globals in a
CPU test does not validate BIOS/card behavior. No cancellation path is issued.

## Stack extent and frozen evidence

Poll allocates no stack. Read API's local frame is 30 hex; its accepted-path
filename concat allocates 20 hex, with leaf strlen, so observed synchronous
local depth is at most **50 hex below read entry SP**. Filename formatter is
leaf; enqueue allocates 18 hex (nested depth 48 hex). This excludes interrupt,
BIOS, diagnostic/error and caller frames. Read callback's own frame is 20 hex,
publisher and callback-stack step each 18 hex; these are separately invoked.

Original USA image SHA256
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`;
EXE SHA256 `a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`;
whole card overlay SHA256
`8ee66fe644407bf15f93af1615045fbaca17fe3daa9f3b67f38bee2277a94c7c`.

| Window | SHA256 |
| --- | --- |
| Read API `80066E28/10C` | `87ba1be6dead52b5b297e0587aad7c37f99b5676e497988c0846859e802f9b6d` |
| Read callback `80066F34/1B0` | `9ccae6396d8da31d84cd08ee1a9a885cd7a69a9d4eada22f94293e350541ae10` |
| Poll `80067628/F0` | `19553c82e1ac2ff3a3089e3c8b101ade63a08c9620216785f79df4a2d2da3e21` |
| Publisher `80067CD4/A0` | `cbf560c2cda1c506b7aebf714a10a70ff5ea759b7253ea21c91d302a7a12db95` |
| Result map `80067C7C/58` | `1ea0b56fedceaa9c7937eaf613dc8240131284307df24e5e454e5acba6c1504e` |
| Callback stack `80068F30/110` | `4d53f4a9da52c4f13eb3ca72aa0e2bbc6649ce102256123073bbfd3b989fe214` |
| Shutdown `8006631C/40` | `966aaf82f3e029b34567f511b5d1933f6f053bb91180eb7f248958d6e5631e9e` |

The focused oracle freezes all 38 inherited windows plus 13 completion/helper
windows, 60 specific instruction words and all 31 callback-table targets. Its
snapshot models do not execute MIPS. Fourteen source-free tests pass, and the
separate exact-source capture passes all pins then rehashes both held and named
original unchanged. Output is emitted to stdout; no card, source image, emulator,
settings, application or candidate was written.

```
python3 -B tools/test_usa_card_read_completion.py
python3 -B tools/Inspect-SpyroUsaCardReadCompletion.py '/Users/john/Documents/Codex/2026-04-24/could-you-create-a-level-editor/Spyro the Dragon (USA).bin'
```
