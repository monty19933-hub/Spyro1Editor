# Fresh LAB R02 — first native entry, idle and short walk

Observed September 6, 2026 against source checkpoint `75be52d`. This is a
bounded gameplay milestone, not a new editor release or general export approval.
The user explicitly authorized DuckStation testing. No candidate or game-source
bytes were changed during the session.

## Exact candidate and evidence

Repository-relative local evidence root:
`_local/native-fresh-id65-runtime-20260906-r01.LbsgK0/`.

The published three-file set is under `publication-r02/candidate/`:

| File | SHA-256 |
| --- | --- |
| `native-id65-fresh-lab-track19-r01.bin` | `1818fe50a132901b9ad670769d4967ccc0a5120897637efa2ad709b8f1d53188` |
| `native-id65-fresh-lab-track19-r01.cue` | `7e2bd7f0dcf23af6fc7ed5e7be56accf7b7ee354b335c238bd446c863f13023f` |
| `native-id65-fresh-lab-track19-r01.receipt.json` | `fd4ebbf408b06e5be3d3ff83c4926362856236b70feda4ef2095fd59587eabbd` |

The candidate was fully published/read back and verified by a separately
recaptured original-source chain before launch. DuckStation PID73363's actual
open BIN was independently identified, including immediately before shutdown.
Observer SHA annotations alone are not loaded-disc authentication.

Recorded video: `videos/fresh-id65-r02-native-selector-idle-walk.mp4`,
328,477,635 bytes, SHA-256
`fb9b3bd6abd31c5afe63407f83652ef77b5c5430b2332de5527713522e1aaa11`.
The finalized file contains 563.200 seconds, 33,688 H.264 frames at640x480 and
stereo AAC44.1kHz. Independent full strict video/audio decoding passed; the
original file's hash, size and timestamps stayed unchanged. This verifies media
integrity, not listening quality, native audio completeness or music identity.

A separate non-retimed, stream-copy tail preview is
`fresh-id65-r02-recording-tail-preview.mp4`:10.148571 seconds, SHA-256
`9c5ffb7f52ce687ef3abc39f73b48ae65232aa0cd2b595aac75c2e4f072e2b43`.
It is keyframe-aligned, not a frame-exact trim. Full originals remain available.
Metadata and decode logs use the `fresh-id65-r02-recording-` prefix.

Three screenshots are under `screenshots/`:

| Timestamp in filename | Visible evidence | SHA-256 |
| --- | --- | --- |
| `2026-09-06-10-41-24` | First stationary Spyro/Sparx on the isolated floor | `e01c6ebdda98719e50d13a208c0f552d45e2a5e5a482b57535b0625e344f2fc2` |
| `2026-09-06-10-42-32` | Native inventory showing Gnasty's World / LAB / 0/0 | `fd9c58eb29c5ad2bb69cfdd05aeb5ef04f106e90e543f43bcea4d41cd9624fdf` |
| `2026-09-06-10-48-06` | Normal-speed idle after the controlled walk | `49441548767abd7ea0f0343cf22dc9a43e81bb39e3b11ec39207d30526e7c1e3` |

Each filename begins `Spyro the Dragon ` and ends `.png`. The first, unrelated
R01 video is also retained: it reached title and the copied Stone Hill save,
**not LAB**, despite its intended-test filename. See local `RUNTIME-SESSION.md`.

## Observed sequence

Cold boot used the existing installed DuckStation and Rosetta/FFmpeg setup,
normal CPU clock, 2MiB RAM, native rendering resolution, PGXP/cheats/Free Flight
off, and a private copy of the original memory card. There was no savestate
startup, RAM/register write, breakpoint or execution-control debugger packet.
The fixed observer used only bounded memory reads and protocol acknowledgments.

1. The copied two-dragon save loaded Stone Hill normally. Native inventory
   accepted R1 R2 L1 L2 R1 L1 R2 L2, then Left Down for reserved level65.
   This is the game's diagnostic selector, not an ordinary portal-entry test.
2. LAB initialized through the normal path. Spyro and Sparx were visible on the
   new two-triangle untextured floor; Town Square scenery was not visible.
   The native inventory showed the bounded LAB title in Gnasty's World.
3. The first Down hold overshot the tiny authored floor. Paused XYZ
   `8848,7537,8882` lies outside its closed XY `[8448,9472]` square. This sample
   is explicitly **not** successful center-floor walking or a collision defect
   diagnosis. Native inventory/selector returned to the center with four lives;
   it does not establish ordinary death, checkpoint or portal behavior.
4. Through the emulator UI, wall-clock playback pacing was temporarily reduced
   to10% for short controller pulses. The CPU clock was not modified. Sampled
   movement `8960,8960 -> 8970,8931 -> 8980,8808` stayed within the floor.
   All controller toggles were released within their bounded UI calls.
5. Playback pacing was restored to100%. Roughly50 seconds of hands-off gameplay
   followed. The UI showed30 gameFPS /60 videoFPS /100%; Spyro and Sparx remained
   visible. The final paused sample was `8937,8515,9061`, playerstate13,
   animation14, four lives. Y8515 is only67 units inside the edge: no safe
   player-footprint margin or arbitrary movement range is implied.

## Read-only observations and limits

`observations-r02.jsonl` has a connection banner followed by ten JSON records;
SHA-256 `38312581869e54f80ea6db1289a354ce0b79e811cef4e968daca99d0e0a55a16`.
The first record's `loading` label was written before seeing the sample: its
actual state is already level65/global state0. Preserve labels and bytes rather
than rewriting history to make the recording look cleaner.

All ten samples matched the30 fixed resource/state gates and34 postpayload
rereads. Exact copy/model/scene anchors were `80089ECC/8008A794/80099F94`;
the companion model root was `800973C4`, actor arena `8009A068..8009A7C0`,
and all eight expected native callbacks matched. Each sample showed one active
class78 companion, a following row terminator, and nominal dynamic capacity16.
The particle pool had25..32 active `42/kind0` particles, in-range terminators,
and separate PETE word`FFFFFFFF`. No `0C/kind2` path was exercised here.

Lives stayed4 and the renderer-exhaustion flag stayed0 at sampled times.
Sampled primitive slack ranged96,360..104,144 bytes. These are sequential,
non-atomic observations, not peak-memory measurements or proof of arbitrary
allocation safety. Runtime and candidate-authentication flags intentionally
remain false. In particular, matching addresses do not authenticate complete
callback bytes, resource contents or lifetimes.

The original observer did not flag the out-of-floor coordinate. A subsequent
regression improvement adds a source-bound XY scope warning while preserving
otherwise valid payloads. The historical recording is not retroactively altered
or promoted. Being inside the rectangle still does not prove contact, a safe
inset, a valid Z state or swept-path collision correctness.

Not established: jumping, charging, flame, imported actors in this fresh scene,
death/reload, normal portals, card persistence, arbitrary authored geometry,
textures in an exported fresh disc, auditory/music acceptance, sustained peak
resources, general native closure or a shipped editable new-level UI. Retained
player/engine/auxiliary dependencies remain explicit rather than disappearing
merely because Town Square terrain is absent.

## Cleanup and preservation

Media capture was stopped normally. Observer session24428 quit with `q` and
exit0; DuckStation shell session59475 quit normally with Save State For Resume
unchecked and exit0. Process inspection confirmed both absent. Private
savestates and the live inputprofiles directory are empty. Only test-created
profile files had been moved to the evidence folder; no pre-existing profile
was removed.

Original settings were restored byte-for-byte after retaining both sessions'
after-test INIs. Fresh final SHA-256 checks matched:

- Global INI: `cd3bfb693556cc08c81b69f56199b61068ac7b483f3d5dae22e2a3931ca55721`.
- Game INI: `629d9bd3eb9890eedb2bfd9ae2458f7f214272f951a9081f687abe8e5f11d1f3`.
- Original card: `d69b43f8be2585f7b5e7fd1b07f85217ba74c01f102c01d7a22928ee0e9e8ae2`.
- Original BIN/CUE: `fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37` /
  `05142502c75d3a0b6374003a8fe41893d6faf0ad55863ff0b62a265a1b152766`.
- The exact R02 BIN, CUE and receipt listed above.

The installed editor was not replaced or launched. The next authoring work must
preserve existing Lab documents and distinguish a fixed research starter from
export of an editable document; this milestone does not authorize treating the
existing schema6 document as an interchangeable fresh-native scene.
