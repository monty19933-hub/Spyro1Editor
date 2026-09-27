# Startup-only reservation research: exact R01 runtime

September 27, 2026. A separately published startup-only disc cold-booted through
firmware, reached a fresh native Artisans game, accepted movement and jumping,
and opened/closed the ordinary inventory. This is a bounded regression check
for the reservation/bootstrap pair, **not installed egg collection, saved custom
progress, native-residence approval or whole-engine memory certification**.

## Candidate and independent verification

Repository root:
`/Users/john/Documents/Codex/2026-07-13/i-needed-to-make-a-new/spyro1editor-v3-release`

Candidate directory:
`_local/collectible-startup-candidate-20260927-r01/`

- CUE: `collectible-startup-bootstrap-reservation-r01.cue`
- BIN: `collectible-startup-bootstrap-reservation-r01.bin`
- BIN SHA256: `895074e78abcf7de47faa15b62ee340239d996a486d1bc7ef5897c68cd4a06d3`
- CUE SHA256: `ff297804c1384cce52dbfa9f5869ccc09585150d9e4ec1aa56c618724e9805a3`
- Complete EXE SHA256: `a6da6e2bc54ea9eaac5e53987853eb96057beaa00cbdf57cbe2f040d26819a6b`

[The publisher](collectible-startup-research-publication.md) changes three logical
EXE spans in exactly two raw sectors (53875 and54078), 234 physical bytes.
No WAD, level, model, overlay, manifest, Ready state, collector or card-extension
hook is added. Separate-process C# readback and the
[independent fixed-opcode Python verifier](collectible-startup-physical-verifier.md)
both verified the complete disc, untouched complement and inverse before launch.
The original WAD and ISO root remain byte-identical.

## Actual session

Evidence directory:
`_local/collectible-startup-runtime-20260927-r01.tDX0tO/`

DuckStation was launched with the exact CUE and `-slowboot -nofullscreen`.
Sony firmware was observed live; the retained 14-51-12 screenshot independently
shows the licensed PlayStation/SCEA screen. Firmware selection was automatic
from the existing `/Users/john/Documents/DuckStation/Bios` directory; this run
did not separately record which firmware filename was selected. No direct EXE,
HLE, fast-boot patch or resume state was used. The isolated configuration used
Vulkan 1x, normal 2 MB RAM and Digital Controller keyboard bindings. All game
input was through the UI. The native menu created a fresh file on a new isolated
card and selected its first empty slot. The normal user card was never selected.

Short direct key taps failed to advance the title reliably, and an attract demo
briefly ran; neither counts as gameplay evidence. Existing toggle macros solved
the input-duration issue: 1/2/3/4 hold directions, 5 Circle, 6 Square, 7 Cross,
8 Start. Each press toggles held input, and each was explicitly released.
No new input binding was required. In the fresh game, left movement changed
position/view, Cross produced a visible airborne jump, and native
Pause → Inventory showed Artisans 0/100 gems, 0/4 dragons, 0%. Start returned
through the pause menu to ordinary gameplay.

The fixed-image observer authenticated PID67793, the open candidate file and
loopback listener before reading; it rechecked file/process ownership around
samples and the entire disc hash at entry/close. It sent only bounded memory
reads, no CPU control, memory writes or input. Three paused samples were taken:

| Sample | Player XYZ | Reserve / initial heap | Reservation |
| --- | --- | --- | --- |
| Artisans spawn | 84992,47173,9556 | A00 /184BC0 | All512 bytes zero |
| After movement/jump | 84628,46099,9765 | A00 /184BC0 | All512 bytes zero |
| After inventory return | 84628,46099,9765 | A00 /184BC0 | All512 bytes zero |

All samples retained level10/state0/classifier0, PETE base801C59B8, floor801879B0,
other floor801A39B0, ordering clear801BF9B0, ordering table801BF9B8,
particle base801C39B8, four lives and renderer-exhaustion flag0.
The native tuple was bound1/controller encoding0/slot0/marker57527. That16-bit
marker is **not** a collision-free identity or custom-save admission.
Reads are sequential, not atomic; the observer repeats geometry and the entire
reservation within each sample. It rejects changes instead of approving them.
Its `runtime_pass`, `clearing_execution_observed`, `save_verified` and
`native_residence_approved` fields deliberately remain false.

`observations.jsonl` SHA256:
`7085f2a4cb88d066a53beedf41ef73f53e06b91306e3cbc2a69252157e25dab8`

Screenshots in the evidence directory:

| File | Observed evidence | SHA256 |
| --- | --- | --- |
| `screenshots/Spyro the Dragon 2026-09-27-14-51-12.png` | Licensed firmware screen | `6d5ab95fcfbb0ecbdeb6acf0ac6d6fe4658c5070f61b67173a9247ce00d5ba94` |
| `screenshots/Spyro the Dragon 2026-09-27-15-01-15.png` | Artisans spawn | `b07328ff7053f49e8bbc795ed9de4d5b218c43075c03aa767b72863300a92060` |
| `screenshots/Spyro the Dragon 2026-09-27-15-01-57.png` | After movement/jump | `dcd80cf2730c3bf3d6a8d829dc29906cf1cba643d40bd076a9f52bc45b3facd4` |
| `screenshots/Spyro the Dragon 2026-09-27-15-03-01.png` | Native inventory | `80eecd125a04078da26478f238ed9e1549fea7396b41721dd47589c313a63d88` |
| `screenshots/Spyro the Dragon 2026-09-27-15-03-33.png` | Returned gameplay | `e8af6d46b7ceca60ce37eea58db99212c22ed813073b79e41ba55e13d133a85a` |

## Shutdown, preservation and limits

The observer closed successfully, DuckStation exited normally with Save State
For Resume unchecked, and its process ended. Only then were original settings
restored, with exact post-run hashes:

- Global settings: `c07823d8de99c88b560fb5dc356fd1247aa391b88ff91284d7ef1354c10fb854`.
- Ordinary SCUS-94228 settings: `629d9bd3eb9890eedb2bfd9ae2458f7f214272f951a9081f687abe8e5f11d1f3`.
- Ordinary USA card: `d69b43f8be2585f7b5e7fd1b07f85217ba74c01f102c01d7a22928ee0e9e8ae2`.

Original USA/Japan BINs, USA CUE and packaged R08 App/Core DLL hashes also match
their preserved baselines. R08 beta45 is not rebuilt or replaced. Isolated test
settings/card, verification logs and screenshots remain as evidence.

Zero RAM in later samples cannot alone establish that the clearing instructions
executed, nor prove instruction-cache visibility, every BIOS handoff byte, IRQ/
stack/DMA exclusion, death/reset, level reentry, alternate loaders or all-level
fit. There was no save/reload acceptance test or custom inventory/% behavior.
The selected-save admission boundary still grants no Ready authority.

The source-only default Release regression passed **246 checks,0 failed**:
`/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-8lcvdcpv/summary.json`.
This is not the larger optional original-source QA matrix. New observer and
independent-verifier Python suites passed22 tests; runner contracts passed103.
