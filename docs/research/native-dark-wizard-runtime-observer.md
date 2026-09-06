# Dark Hollow Wizard read-only runtime observer

`tools/Observe-SpyroNativeWizard.py` is a manual observation aid for the exact research candidate documented in `native-dark-wizard-research-candidate-contract.md`. It is not an emulator controller or a runtime certificate. Its profile and final-data SHA (`6ed74dbce04d6712e0d9ac12cfd05ae6ac334e0ec39fb7420f2105076132e537`) are explicitly annotations: they do not authenticate the loaded image. Identify the actual DuckStation process and its open, independently verified candidate BIN before connecting.

The tool requires explicit `--connect-identified-test-session`, connects only to localhost2345, and sends only bounded `m` memory reads and protocol ACK/NAK bytes. It never writes RAM, changes registers, installs breakpoints, or sends resume/stop/step commands. The audited DuckStation connection pauses emulation once; resume and pause only through the UI. Enter `p label` while paused to print one JSON sample, or `q` to disconnect. Samples comprise sequential reads, not atomic CPU snapshots. Every sample retains `runtime_pass=false`.

## Native bindings and bounded interpretation

All addresses below are main-RAM addresses, with little-endian fields. Individual reads are limited to1..1024 bytes entirely inside `[80000000,80200000)`. Any level other than12 or mismatched arena globals prevents actor scans; arbitrary reported model/sound/property pointers are not dereferenced.

| Native field | Meaning and exact research expectation |
| --- | --- |
| `8007596C`, `800757D8`, `8007582C` | Level ID, game state, lives. |
| `80078A58` | Three signed32-bit player XYZ values. |
| `80078AD0`, `80078AD4`, `80078AD8` | Player state, substate, state timer words. |
| `80078A71` | Player animation ID **byte**, not a32-bit word. |
| `80075828`, `80075890`, `800756E8` | Placed rows=`8013AE1C`, dynamic rows=`8013DA74`, Moby arena end=`8014021C`. Row128 is `8013DA1C`, expected class11B. |
| `800756A4`, `800756A8` | Current dynamic count and capacity90. Count>=69 is a conservative attention threshold, not a native invariant or allocator-safety proof. |
| `80075930`, `8007573C` | Downward next-free property cursor and next-free row; neither denotes active-list end. |
| `800767E4`, `80076410`, `800764D0`, `80076378` | Wizard11B, bolt26, reward56, and player root pointer fields. They are reported, not validated as complete resources. |
| `800761D4`, `800761D0`, `80076294` | Sound-map base, descriptor base, and particle7 descriptor pointer. No playback or sample-equivalence claim follows from these pointers. |
| `80075824`, `80075738` | Active particle pool=`801C3BB8`; next-free particle pointer is not active count. |
| `800757B0`, `80075780`, `80075820`, `800758B0` | Primitive cursor, exclusive limit, ordering-table pointer, renderer exhaustion flag. |
| `80077DFC` | Collision helper's last saved SP, **not** stack high-water. Do not use the earlier mistyped overlay address `8007DDFC`. |

Dynamic scanning stops at statusFF, skips every other signed-negative status, and never reads beyond90 rows of0x58 bytes. Their maximum end `8013F964` precedes the lowest of90 property slots, `8013F9AC`. The properties cursor must lie on the `arenaEnd-n*0x18` grid for n0..90; reuse means it is not `arenaEnd-activeCount*0x18` or the lowest live property pointer. A missing in-range row terminal requires attention, not a scan into properties.

Particle scanning is exactly256 records of0x20 bytes. Kind at raw+1, not selector at raw+0, controls inactivity: FF terminates, all other values>=80 skip. A full256-slot pool without an internal FF is valid. The separately read word at `801C5BB8` belongs to PETE, not a257th particle. Selector07/kind2 frame is raw+10; selector41/kind1 uses those payload bytes differently. Raw effect samples are retained for inspection.

Primitive slack is only the sampled `limit-cursor-0x400`. It is `null` with a reason for noninteger, uninitialized, out-of-main-RAM, or reversed pointers; the exclusive limit may equal `80200000`. Valid negative slack remains visible, not converted into a passing margin. Pointer ordering does not prove allocation ownership or transient headroom. Renderer exhaustion is an attention signal interpreted against baseline and frame phase, not a monotonic counter. No true peak stack, GTE context, or all-frame primitive usage is measured.

## Exact source evidence

Read-only source extraction verified the original EXE SHA `a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`. Original whole-BIN authority remains SHA `fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37` in the candidate factory, not in this observer.

- `8003CC30+0x20` (EXE offset2D430), SHA `2b99e9cee5b15975753d57b867d14096d518ce125e7bd22fdc1f65ec9b716411`: loads state at80078AD0, indexes table8006C470, then stores the selected byte at80078A71. Exact bytes: `0880053cd08aa58c0780023c70c442242130a2000000c2900880013c718a22a0`.
- Table8006C470 first8 bytes `0001020304050608`, SHA `bbfa8ae605ba3331a95204b71cb1d0454610815b50dd87334bce09124a38689f`: native state7 maps to animation8. The test decodes this exact instruction fixture; it does not emulate native CPU execution or prove animation rendering.
- Moby loader80013F24+AC, SHA `6338caf975b0fa524052abe3e2b439292791273aac359dc5e6d78b90d0a37e33`; allocator/deletion800524C4+1E4, SHA `768e4181d81333e0e27bbe6419e8d94114096a9bd8e8556314af5f0e628327ff`, establish the row/property globals and cursor semantics.
- Electric branch80040FB8+118, SHA `2c44899e9910a984a20a0d2ef03620d896df05f6cf9738c4c06f50dd59ca5c74`: low damage bit20 leads to map1F playback and state7. The intact bolt's20020 includes that bit. Shared selector rebinding still has the bounded consumer limitations documented by the resource plan.
- Collision save prologue8004AE38+80, SHA `e4305e32bc0ce6b66d71d5452e6d262de05486cc9e796dcf170a16f5c2d5fbbf`, places saved SP at80077DFC. Particle renderer80058070+70, SHA `b04980d58c31a81ec8d35699e36d0e53bd77b1ac993e86c021adba6a1c12c08e`, includes exhaustion store580A8 and primitive cursor store580B4.

## Minimal normal-entry observation sequence

1. Independently verify the published BIN/CUE/receipt and identify the actual open candidate image. Cold boot it normally, retaining video/audio if available. Do not use a fast-level selector, RAM patch, or imported save state to establish this baseline.
2. Enter Dark Hollow through its normal Artisans portal. Once normal gameplay is visible, pause in the UI, connect explicitly, and capture `p dark-entry-baseline`. Confirm level12, expected arena/capacity, row128 class11B, expected particle pool, and no refusal. Record current counts and pointer values; a matching layout is not a pass for behavior or audio.
3. Resume through the UI and approach the authored Wizard area near `(92832,25248)` without attacking. Check full body/feet, patrol, nearby original actors, camera-dependent rendering and ambient sound. Pause for `p patrol-baseline`; then observe one native charge/three-bolt volley and impacts, pausing for a separately labeled sample. Stop and preserve evidence on corruption, refusal, exhaustion, or approaching allocation capacity.

Only after this baseline is stable, separately test native electric hurt (state7 **and** animation byte8, plus visible/audio evidence), kill/drop/collect exactly one10-value gem, then normal exit/re-entry and repetition. A missed short-lived state or effect in a paused sample is not proof it never occurred. Dynamic counts, last saved SP and sampled primitive slack cannot certify unseen peaks. These steps are a proposed procedure; this observer implementation/review did not connect to or exercise the emulator.

## Regression evidence

The original eight tests remain. Tests-first additions initially failed with two missing-API errors and the old uninitialized slack value `-1024`; after the narrow implementation, all **10 tests passed** using fake sockets and synthetic memory only. Coverage includes exact native animation-address/byte derivation, state7→8 fixture, valid/exhausted/reversed/out-of-range primitive pointers, packet ownership, hostile replies, signed holes/terminals, stale arenas, and no runtime-pass promotion.

```sh
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tools/tests -p test_native_wizard_observer.py
```

No .NET build, socket connection, emulator control, candidate mutation, or runtime acceptance was performed for these changes.
