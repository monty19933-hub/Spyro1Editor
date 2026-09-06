# Native portability runtime investigation — 2026-09-05

This is separate from structural readback and source-ROM QA. No broad Moby,
blank-level, UI, or release acceptance is implied. Candidates below are immutable.
DuckStation tests use digital-controller input, no memory cards, no resume saves,
and disabled game cheats. Read-only GDB packets observe RAM; no RAM, register,
breakpoint, or emulator-control writes are used.

## Preserved inputs and settings

Original USA BIN SHA256:
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
Original CUE SHA256:
`05142502c75d3a0b6374003a8fe41893d6faf0ad55863ff0b62a265a1b152766`.

The resumed session's exact pre-test settings and isolated helper sources are
retained under `_local/runtime-native-closure-20260905/resumed-session-20260905/`.
The earlier `paused-session-20260905/PAUSED.md` is historical, not a current pause.
Installed editor bundles are not replaced. The full repository regression gate
does rebuild source-tree Release outputs, including the app project.

## Wizard T27: transferred patrol crosses damaging water

Properties-only Magic Crafters parent BIN:
`4903db5268b2a99cae0a3a5d4718d7cce79a09b9c04e6351ab08fcb80d19da7e`.
Its native-navigation derivative is
`93775b448eb426ad6dab49beec331eeeb5023099b28d6a76d6377b9cbdb27889`.
The spawn-only r01 derivative, which changes only the native landing XYZ, is
`1c457cf1292f3ad56da4d6f6a72c79972276176102240f7511bf0ab460512755`.
Receipts and complete physical/parity readback are alongside each BIN under
`_local/runtime-native-closure-20260905/`.

The original navigation candidate cold-boots, enters Magic Crafters and respawns.
Several player falls while learning digital controls are not candidate failures.
The spawn-only derivative reaches the path near imported T27.

Current-candidate executable and existing `MobyLoader` independently identify:

```text
B = *(uint32*)0x80075828
T27 = B + 27 * 0x58
T107 = B + 107 * 0x58
class = uint16(row+0x36), XYZ = int32[3](row+0x0C)
```

At runtime B was `0x8017BAA0`. T27 at `0x8017C3E8` had native class `0x011B`,
properties `0x80181B70` and route header `0x80181B98`. Its full row/properties
changed while patrolling, distinct from untouched native T107 at `0x8017DF68`.

| UTC observation | Imported T27 XYZ | Stationary Spyro XYZ |
| --- | --- | --- |
| 2026-09-06 01:40:58 | 85873,40419,23200 | 88832,43899,23911 |
| 2026-09-06 01:43:57 | 85205,40347,23200 | 88832,43899,23911 |
| 2026-09-06 01:55:31 | 83943,39497,23200 | 88832,43899,23911 |

The hat-only appearance in the retained `Spyro the Dragon 2026-09-05-18-54-53.png`
initially suggested burial. Collision-only decoding found Z23200 at all three
Wizard positions and Z23552 at Spyro's then-current position, initially leaving
foreground-terrace occlusion as a possibility. That first check did **not**
establish that the Wizard's returned floor was ordinary walkable land. The
subsequent rendered-geometry and native surface-descriptor check below resolves
this material omission: the transferred patrol enters damaging water.

There is a separate exact initial-placement discrepancy: inherited T27 origin
23450 is102 below its initial floor23552; untouched native Wizards begin205
above their local floor. Candidate row policy/model bytes match native T107
apart from private properties, XYZ and yaw. Native movement calls `0x80039398`
and ground-clamps to collision plus signed row+0x38 (zero here); it does not copy
route Z directly into actor Z.

The r02 diagnostic changes only initial Z23450→23757. BIN:
`7f40bd22da87aa8ab489f5bd6e2555236faf41b6cc06c219c5d79d57e0d7834c`.
One changed sector: two payload bytes plus40 EDC/ECC bytes; all other raw bytes
match r01. It is an isolation experiment, **not a placement fix or acceptance**.
The same hat-only appearance persisted while the r02 actor again ground-clamped
to Z23200 during patrol; changing only initial Z did not change its destination
surfaces.

### Source-bound rendered surface and hazard proof

The r02 native model/environment subfile at WAD `0x2822800`, length 604,160, was
read directly and compared byte-for-byte with original USA source. Its SHA256 is
`6163a158a20940be6298b3169e6cb6fa4c56a5f3010fe33d00e28507cde89696`.
All 245 native environment index entries matched the source overlay's sector
order; all 5,020 HP faces, their four raw face words and 19,321 vertex corners were
independently checked against native sector headers and packed vertex words.
The sampled sectors have no special-Z-scale or disabled-HP flag. Thus the
following rendered-surface results are native-byte-bound, not unverified cache
coordinates or collision-only guesses. Coordinates below are native world
units; overlay coordinates were multiplied by 16.

| Position / purpose | Rendered HP surface | Native collision / surface |
| --- | --- | --- |
| r02 T27 `(86523,40727,23200)` | sector 199 face 34, WAD `0x284AD00`, texture 52, Z23200 | triangle 334, Z23200; flag `0x01` at `0x288777E` |
| r02 T27 `(83035,39084,23200)` | sector 199 face 21, WAD `0x284AC30`, texture 52, Z23200 | triangle 382, Z23200; flag `0x01` at `0x28877AE` |
| r02 T27 `(82521,38816,23200)` | sector 199 face 21, texture 52, Z23200 | triangle 383, Z23200; flag `0x01` at `0x28877AF` |
| Translated route endpoint `(82309,38707,23737)` | sector 199 face 21, texture 52, Z23200 | triangle 383, Z23200; flag `0x01` |
| Initial route point `(88832,41851,23737)` | sector 191 face 48, WAD `0x28480B8`, texture 31, Z23552 | triangle 3220, Z23552; implicit ordinary surface `0x3F` |
| Earlier player spawn `(88832,43899,23911)` | sector 191 face 21, WAD `0x2847F08`, texture 31, Z23552 | triangle 2807, Z23552; implicit ordinary surface `0x3F` |

Flags `0x01` select native special-surface descriptor 1 at WAD `0x285A440`. Its
12 bytes are all zero: **type 0, param1=0, param2=0**, decoded by the existing
`NativeTerrainSurfaceSignature.Label` contract as **damaging water floor**.
The earlier three T27 samples likewise hit this descriptor (triangles 350/382).
Texture 52 is the visible blue pond material, whereas texture 31 is the grassy
bank and pale border. This classification does not rely on the texture's visual
appearance: the collision flag and native hazard descriptor independently prove
the water behavior.

The 946,176-byte texture-page subfile also matches original source and r02 exactly,
SHA256 `5968dc0e6921f3006120cd928a1c40cd14744c117ab2370dbe73127d2adfe888`.
The 12,336-byte native texture component matches its existing preview manifest,
SHA256 `2bf907128603b877d16b0a350aded128633078ef79271c2009885cba4c9fd311`.
Texture 52's static normal-preview PNG SHA256 is
`34f0202b811a27d35342df9ea6610f522e40181b102cda5bf91ef6c4f5287f51`.
The complete native collision component remains original/r02 byte-identical,
SHA256 `b6b1a033a9ecad87ff72285402c09bb240e73ef411d8d59d8f6f5d880e45ba7e`.

At the later player bank-edge position `(88801,40201,24052)`, the visible hit is
sector 199 face 39 / texture 49 at Z23712, and native collision triangle 2806 is an
ordinary sloping surface at approximately Z23671.27. This is a different sample
from the earlier Z23552 terrace; directly comparing the player's and Wizard's
origins is not a rendered-height test.

At the measured Wizard XY positions the origin lies on the rendered water sheet,
not below a separate hidden collision floor; there was no higher HP surface over
those exact XY samples. This does not by itself reconstruct animated model bounds
or the camera's foreground occlusion, so the hat-only pixels are not promoted to
a proven model defect or a complete occlusion diagnosis. The concrete acceptance
failure is now the **authored route's damaging-water crossing**, independent of
that remaining appearance attribution.

The required design gate is source-bound destination placement **and route**
validation: check the actor's supported ground/material semantics, its footprint
and swept patrol corridor against native collision and rendered geometry, with
bank/height transitions and resident-object clearance accounted for. A valid
initial XYZ or a native mover returning a floor is insufficient; a translated
endpoint in damaging water must not be accepted as an ordinary-land Wizard
route. Lifting only the initial Z cannot repair this path. No route, collision,
model, behavior or Core source was changed by this investigation.

Combat, effect attribution, death/reward and complete appearance checks remain
separate runtime gates. The Wizard Peak two-instance candidate has not yet been
gameplay-tested in this resumed session.

## Absent-resource chest v2: boot passes; placement confounded

Exact published BIN SHA256:
`d94d90b51c15b82328b6dbeb67474e563ddefc7d7f6c81bd4b61c526c58083fe`.
Directory:
`_local/research/native-moby-closure-20260905/town-square-native-closure-v2-r01/`.

This exact BIN cold-boots, starts a new game without a memory card and reaches
Town Square13 through its native EXE navigation path. Native courtyard actors,
player movement, collision and camera are active. No key/chest interaction pass
is claimed from this run.

At 2026-09-06 02:03:27 UTC, B=`0x8017E0B0`, level13 and player position
125225,100506,8550. Live imported native identities were:

- T107 Key `0xAD`, address`0x80180578`, XYZ130150,111032,11264.
- T108 Chest `0xAE`, address`0x801805D0`, XYZ132198,111032,11264,
  model pointer`0x8017D738`, properties`0x80183E50`.
- T109–T113 native reward controllers `0x0D`, with independently allocated
  properties starting`0x80183E68`.

A subsequent source-native spatial review found preserved T27 charge chest
`0xC3` at **the exact same XYZ as imported T108**. The source model/behavior import
can be structurally correct while this authored placement still confounds
gameplay. The v2 candidate remains retained, but interaction acceptance must use
a separately generated clear-placement variant with original native actors
preserved. A spawn-only derivative completed before this finding was received
is likewise retained as confounded and must not be promoted.

## Absent-resource chest V3: native interaction and +10 reward observed

Source checkpoint `ade340a` adds a separately published closed clear-site recipe.
Exact directory:
`_local/research/native-moby-closure-20260905/town-square-native-closure-v3-r01/`.
BIN SHA256:
`15df0ee1edba6cb48fe1d34df5a5f51a7d6f289de387c499ca0ff04e99b443bf`.
Graph fingerprint:
`52c68e8595a21ef8807abb6090dfb02d84a8643af114346c3c7c962247c45d6d`.
Use its matching `town-square-native-closure-v3.cue`; no spawn-only derivative
was created or launched for this V3 test.

All 107 original placed actors and properties, original scene/collision, and
the shared imported resource/behavior/EXE bytes remain preserved. Compared to
V2, only the seven imported row XYZ fields and five reward-property XYZ fields
change. Key T107 is `(119632,102288,8192)`; chest T108 is
`(121680,102288,8192)`. The source-bound dry-ground and center-separation policy
is experimental, not a general mesh/physics certificate; see the
[spatial evidence](native-town-square-spatial-site-evidence.md).

This exact candidate cold-booted into a new game without a memory card, loaded
Artisans, and entered Town13 through the candidate's native navigation path.
Town's original spawn `(125225,100506,8550)` was unchanged. Ordinary controller
movement reached both imported objects; no RAM movement/teleport was used.

| UTC, 2026-09-06 | Native observation | Actual global / Town / HUD gems |
| --- | --- | --- |
| 02:51:23 | Normal spawn, key0, chest state0, target210 | 0 / 0 / 0 |
| 02:54:14 | Directed flame flashes the chest red; it stays closed with key0 | 0 / 0 / 0 |
| 02:59:36 | Key picked up at Spyro `(119374,102043,8553)`; key1 | 0 / 0 / 0 |
| 02:59:55 | Key row state2; chest key-ready state1 | 0 / 0 / 0 |
| 03:03:39 | Front/facing approach starts native chest state2, properties timer4 | 0 / 0 / 0 |
| 03:04:14 | Sparkling key/lock animation and camera sequence; timer192 | 0 / 0 / 0 |
| 03:04:40 | Chest breaks into metal debris and treasure; key2, chest lifecycle FD | 0 / 0 / 0 |
| 03:05:31 | Partial collection: key3, all five controllers deleted, but rewards incomplete | 7 / 7 / 1 |
| 03:06:22 | All six reward gems collected; lives4 unchanged, target210 unchanged | 10 / 10 / 10 |

The locked chest also stopped a short charge with Spyro's visible native bonk
and stars. Opening is automatic when the key, proximity, facing and player-state
gates pass; earlier flame attempts from behind are not opening failures.
The final reward is **six gems, four green and two red, worth +10**. The chest
emits its own green gem in addition to five native reward controllers. Native
selectors and award consumers independently confirm the visible breakdown;
see [pickup conditions](native-town-square-key-pickup-conditions.md) and the
[RAM observation contract](native-town-square-chest-runtime-observation-contract.md).
The courtyard's pre-existing gems were not collected during this measured loop.

Evidence is retained in
`_local/runtime-native-closure-20260905/resumed-session-20260905/`:

- `chest-v3-r01-ram-samples.json`: 24 full read-only snapshots, including row and
  properties bytes, correctly sized inventory/HUD fields and UTC times.
- `read-chest-final.py`: final read-only sampler; only RSP memory reads/acks.
- `runtime-checkpoint.json`: exact candidate, restored settings, pending gates,
  and SHA256 identities of the 17 retained evidence files below/alongside it.
- Native PNGs `19-53-52` (flame reaction), `19-54-29` (charge bonk), `19-59-29`
  (pickup), `20-03-49`/`20-03-57`/`20-04-05`/`20-04-23` (opening), `20-04-30`
  (debris), `20-04-49` (released rewards), and `20-06-08` (settled HUD10), all
  named `Spyro the Dragon 2026-09-05-HH-MM-SS.png`.

### R01 remaining runtime gates and historical safe stop

Native audio/video capture could not start: DuckStation requires FFmpeg9's
63/63/61/10/7 library ABI. Its bundled `libavformat.63.dylib` is x86_64-only,
and bundled install/dependency paths reference a nonexistent runner directory.
No emulator libraries, app bundle or system capture settings were modified;
no audio-quality or sound-correctness pass is claimed.

The Mac locked during preparation for normal Exit Level/re-entry. No successful
exit, repeat entry, death/reload, memory-card persistence or full 210-gem level
completion was observed. Temporary control-profile experiments after the +10
sample are not additional gameplay acceptance. Nearby chickens, ordinary
movement, ground collision and camera remained active during the measured
interaction, but broad unrelated-actor/level regression acceptance is open.

The read-only connection was closed. Only the owned DuckStation test process
was stopped: its first termination request reported graceful shutdown but did
not exit; the second used its explicit force-exit path. This process termination
is not a game-level exit pass. The three temporary test profiles were moved
recoverably to `retired-test-profiles/` in the retained evidence directory.
Pre-test settings were restored byte-for-byte after process exit:

- `settings.ini`: `cd3bfb693556cc08c81b69f56199b61068ac7b483f3d5dae22e2a3931ca55721`.
- `gamesettings/SCUS-94228.ini`: `629d9bd3eb9890eedb2bfd9ae2458f7f214272f951a9081f687abe8e5f11d1f3`.

Original BIN/CUE and exact V3 BIN hashes above were rechecked unchanged.
The post-runtime Release `--verify-v3` also passed all 389 checks, including
complete candidate/receipt readback and refusal to overwrite the retained set.
Installed editor bundles remain untouched. The candidate receipt remains
immutable with `runtimeProven=false`: the directly observed interaction pass
does not waive its broader pending runtime gates. No normal Create BIN,
arbitrary imported-Moby support or finished blank-level editor is enabled.

## R02: fresh interaction and normal portal re-entry

After the user restored an unlocked test window, the same immutable V3 CUE/BIN
was cold-booted again without a card, save state, active cheats, or RAM writes.
Fresh settings backups were taken before this run. Ordinary movement repeated
key pickup and the native automatic opening, with all six gems collected.

| UTC, 2026-09-06 | Observation | Global / Town / HUD | Town saved reward bits |
| --- | --- | --- | --- |
| 03:37:05 | Unchanged normal Town spawn; key0, lives4 | 0 / 0 / 0 | `0` |
| 03:41:52 | Key1; chest state2 opening sequence | 0 / 0 / 0 | `0` |
| 03:42:27 | All six rewards collected; key3 | 10 / 10 / 1 (HUD still animating) | `0x3F000` |
| 03:46:13 | Native pause-menu Exit Level returned to Artisans, real Town portal visible | 10 / 10 / 0 (Artisans HUD) | `0x3F000` |
| 03:46:52 | Walked through the normal Town portal; Town13 reloaded | 13 / 10 / 10 | `0x3F000` |
| 03:47:34 | Inspected the cleared chest site after re-entry | 13 / 10 / 10 | `0x3F000` |

The menu HUD visibly settled to 10 before Exit Level. The three additional global
gems were original Artisans gems encountered while walking into its Town portal,
not another chest award. On re-entry, key state reset to 0; T107 reconstructed
active, while chest T108 and all five reward controllers T109–T113 reconstructed
with native deletion byte `+0x48 = 0xFD`. The transient deletion word reset to0,
but the durable destination-index reward mask at `0x80077974` stayed `0x3F000`.
These are separate native consumers, not interchangeable persistence indicators.
See the [source-backed persistence contract](native-town-square-persistence-contract.md).

Subsequent attempts to reach the courtyard cliff did **not** lose a life and
are not a death/reload pass. They collected one original red gem, raising Town
to 11/global 14; the six imported reward bits remained unchanged. No saved state
or memory-card test was performed. Normal menu exit plus portal re-entry is now
directly observed; death, partial-reward reload and card persistence remain open.

Evidence: `_local/runtime-native-closure-20260905/reentry-session-r02-20260905/`.
It retains fourteen complete `chest-v3-r02-ram-samples.json` snapshots, the exact
read-only sampler, and native PNGs `20-38-51`, `20-42-04`, `20-46-19`,
`20-46-45`, and `20-47-24`. Both this ARM64 session and R03 below exited normally
through DuckStation's Quit command with exit0.

## R03: native audio/video capture on the exact same candidate

The earlier ARM64 recording failure was investigated without changing any app
bundle or installing libraries. Existing Rosetta could load all five bundled
FFmpeg libraries and resolve all 69 required symbols in a bounded x86_64 helper.
Its missing runner install paths did not prevent loading once the bundled
dependencies were loaded in order. DuckStation was then launched explicitly
under `arch -x86_64`, with `DYLD_LIBRARY_PATH` scoped to that single process and
the existing `/Applications/DuckStation.app/Contents/Frameworks` directory.
This is an emulator capture workaround, not an editor or candidate change.

DuckStation's native Start Capture successfully recorded game audio and video.
The game displayed approximately 30 game FPS / 60 video FPS at 100% speed during
the run. A fresh no-card game entered Town through the existing test-only native
navigation path. The recorded interaction includes a pre-key red flame reaction,
key pickup, automatic opening, sparkling key/lock animation, metal debris and
all six rewards. One original green gem was collected on the approach, so this
run's measured chest award is **Town/global 2 → 12**, not an unexplained 12-gem
reward. At `03:59:50.721480 UTC`, actual global/Town/HUD were 12, key3, lives4,
and all six imported saved reward bits were set (`0x3F000`).

Evidence: `_local/runtime-native-closure-20260905/recorded-session-r03-20260905/`.
The original native recording is `Spyro the Dragon 2026-09-05-20-54-23.mp4`:
109.506009 seconds, 81,793,020 bytes, H.264 640×480 at approximately 59.8173 FPS
(6,549 frames), with 44.1kHz stereo AAC audio at approximately 128kbit/s.
Both streams decode completely with `ffmpeg -xerror -err_detect explode`, exit0.
Decoded frames 5210, 5384 and 5504 were independently inspected: key/lock animation,
debris and rewards, then gem award numerals. Native PNGs `20-58-27` and `20-59-57`
and complete read-only RAM snapshots accompany the recording. Pauses used for
observation stop game/capture time; the file duration is not elapsed wall time.
Encoding and decode success alone do not prove sound selection or subjective
audio quality; source binding and cue-specific evidence are separate gates.

The independent source-bound waveform audit now supplies that cue-specific
evidence. The original, V3 and MP4 hashes are checked before extracting the
exact native ADPCM templates. Town's entire sound bank/component still equals
retail, and imported chest model-local slot0 is correctly remapped to Town
descriptor 54. Native-pitch templates match the key pickup at 80.233s, the correct
chest break at 89.363s, six post-break pickup cues, and the source-prescribed
release pitches `0x0800,0x0880,0x0900,0x0980,0x0A00,0x0A80` in chronological
order from 89.363 to 90.143s. The chest's first-difference waveform correlation
is 0.712 at the event versus at most 0.054 outside the opening interval. These
coefficients measure similarity, not confidence percentages; the pitch matrix
is descriptive rather than a held-out statistical test.

`audit-native-audio.py`, `native-audio-correlation-audit.json` and
`native-audio-audit-summary.md` retain the method, exact payload hashes, search
windows, controls and results. This supports execution of the intended native
cues and pitch progression in the measured interaction. It does not certify
subjective quality, every voice/tail/panning state or bit-exact SPU output. See
[source sound scope](native-town-square-v3-audio-observation-scope.md).
The complete recording SHA256 is
`1daec790990b8a8110ba86d281f491b5d12de4b88773812a697ada63ebf74a91`.

### Preservation after R02/R03

After normal process exit, both pre-test settings files were restored exactly
to the same hashes recorded for R01 above. The sole temporary gameplay/menu
preset was moved recoverably to R02's `retired-test-profiles/` directory. The
read-only debugger connections were closed; no save states were written or
memory cards attached. Original BIN/CUE and the exact V3 BIN were rehashed and
still match the identities above. Installed editor bundles remain untouched.
The immutable candidate receipt is not rewritten to promote these additional
observations into general native-import or normal Create BIN authorization.
