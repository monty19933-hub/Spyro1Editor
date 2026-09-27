# Authored Japanese egg colors — September 27 runtime R02

The unchanged `6b4502a` research candidate was cold-booted through full USA
firmware in DuckStation. Two independent diagnostic palettes now **visibly
coexist and animate in USA Artisans**. Movement, jumping/gliding, opening the
ordinary inventory and returning to gameplay were observed. This is a narrow
color/visual test, not pickup, persistence, inventory integration, arbitrary
placement or a new packaged editor.

The previous September 25 R01 session was interrupted; it remains incomplete.
R02 is a fresh session with a newly created isolated card, not a resumed state.

## Exact candidate and configuration

- CUE: `_local/japanese-egg-usa-authored-color-candidate-20260925-r01/artisans-native-japanese-eggs-authored-colors-r01.cue`.
- BIN SHA256: `8d99201784ec1c6be4219c98ac72e148258cb13a7a9b539d5937af83a9b4c1ff`.
- Executable: `/Applications/DuckStation.app/Contents/MacOS/DuckStation`.
- Arguments: `-slowboot -nofullscreen` followed by the exact CUE above.
- Firmware: Sony SCPH-1001 / DTLH-3000 USA v2.2, SHA256
  `71af94d1e47a68c11e8fdb9f8368040601514a42a5a399cda48c7d3bff1e99d3`.
- Vulkan 1x, enhancements/PGXP off, DigitalController keyboard controls.
  No HLE, direct-EXE boot, cheats or loaded savestate.
- Session: `_local/japanese-egg-authored-color-runtime-20260927-r02.JKW7cu/`.
  Settings, card, captures, cache and savestate directories are isolated there.

The firmware logo, title sequence, native Creating Save File and new-game
entry into Artisans were observed. The read-only observer authenticated the
complete candidate before and after observation, held/named file identity,
DuckStation PID 61932/start identity/executable, its open exact BIN inode and
its loopback-only GDB listener. It issued bounded RAM reads, not writes or
game-control commands. Sequential snapshots are not atomic or an independent
full-image runtime attestation; the observer retains its false runtime/save
approval flags. Human-readable sample labels are not test authority.

## Observed behavior

The two rows use separate classes 489/498 and roots `80164D54`/`8016555C`.
Egg A is dark blue; egg B is pale gray. Both changed animation/yaw while their
four authored near/far RGB arrays and guard/dispatch bytes remained unchanged.
All 70 RGB words per array were deliberately given diagnostic values. Uniform
coloring demonstrates independent authoring, **not recovered Japanese presets,
final artwork, selectable editor materials or every far-LOD branch**.

Five read-only samples span spawn, plinth approach, both colors together,
close approach and inventory return. Recorded renderer slack was respectively
40552, 62196, 63220, 64560 and 72084 bytes; exhaustion remained zero. This is
session-local sampled evidence, not whole-engine capacity certification.

One ordinary red gem was collected. The normal inventory showed Artisans
1/100 gems, 0/4 dragons and 0% completion; Continue returned to movement.
There is no custom egg counter or collector installed. No custom pickup,
save/reload, death/reentry, level transition or completion result is claimed.

## Evidence and closure

`observations.jsonl` SHA256:
`48cfc1dcf9784c63e09b86eddfc347bfd112b7d89a947ecf3d71ef97f54f17d3`.
Native screenshots under the session's `screenshots/`:

| File | Observation | SHA256 |
| --- | --- | --- |
| Spyro the Dragon 2026-09-27-13-37-36.png | Full firmware | `6d7b0bfbbe91ba2d75e9d9770c360b15b7970d983663ca1e3103de128f513564` |
| Spyro the Dragon 2026-09-27-13-41-50.png | Both authored colors together | `317c6a79f4173d99a7a1eb1da55264edc92da9ee70612f692643fc234059bf70` |
| Spyro the Dragon 2026-09-27-13-42-17.png | Close approach | `6625843e3be723a27684b3debd9432f4c29310fdc89da91e54634f76d5a703ae` |
| Spyro the Dragon 2026-09-27-13-49-48.png | Ordinary inventory | `ba37f31aad89c956c8b7dca56c9425d883b43fb3153d50d0da1ceeee180567cb` |

The observer exited cleanly after its final candidate hash. DuckStation then
exited with status 0; Save State For Resume was unchecked. No emulator remains
running. Normal settings were restored byte-for-byte **after** confirmed exit:

- Global settings: `c07823d8de99c88b560fb5dc356fd1247aa391b88ff91284d7ef1354c10fb854`.
- USA game settings: `629d9bd3eb9890eedb2bfd9ae2458f7f214272f951a9081f687abe8e5f11d1f3`.
- Ordinary USA card: `d69b43f8be2585f7b5e7fd1b07f85217ba74c01f102c01d7a22928ee0e9e8ae2`.

R08 app/Core DLLs and its gameplay-tested candidate retain their recorded
hashes. Original USA/Japan images were authenticated unchanged. No preserved
packaged app was rebuilt, replaced or promoted. The regression gate separately
built the worktree sources. The new collection kernel and selected-slot residence
descriptor are separate CPU/source-only work and are not on this disc.
