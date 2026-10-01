# Imported enemy player-reaction repair

The universal composer now reads source collision damage masks and bounded direct
writes to Spyro's damage flags, derives the native reaction animation IDs, and
merges missing records from the authenticated original USA disc. Shared and
already resident records are reused. Exact aligned model growth enters the same
final RAM gate as models and behavior. Retail and fresh composition use this
provider; the UI includes reaction memory in the successful fit explanation.

## Reproduced failure and repaired contact

The earlier Alpine Ridge row 1/class 0x10F Artisans candidate (BIN
`056698d2e6556302c2c8edbe784807f9cac2420390543bfc989818277b253a05`)
entered Spyro state 22 / animation 23 with a null animation-table slot. Spyro's
rendering became corrupted and gameplay stopped. The captured model bytes did
not change, distinguishing the missing player record from an enemy-model write.

A private diagnostic copy imports Alpine animation 23 (22,196 exact bytes;
22,528 aligned model growth). The original aggregate had only 5,304 bytes free,
so this diagnostic also removes the separately proven, test-only class 50 row
174 model, reclaiming 32,768 bytes. The user project and original candidate are
preserved. The corrected contact fixture places the Druid at native XYZ
84992/47850/8960 and has BIN SHA-256
`fc3995041793d765a94fed4b524fba1a95dcd4614704bb0c8a931fc95615b65f`.

On a cold boot with the real USA BIOS, a separate initially blank card, software
GPU, no savestates, no cheats and no emulated RAM writes, ordinary controller
movement triggered state 22. Read-only sampling captured player XYZ
84279/47371/9658, animation 23 and a valid slot pointer `80123890`.
Hit RAM SHA-256:
`82d1b475b165178ade40eb69ca7f6505d946ed1665ef8b1ea8c34adaa67e2381`.
After disconnecting the sampler, Spyro visibly recovered, Sparx changed to blue,
and ordinary gameplay continued at 30 FPS. Recovered RAM SHA-256:
`e8fa4edad5cbc1fd7b40f897481a2a3018a099188eed1a271161f5e9a1ee965d`.
This resolves the exact Alpine/Artisans interaction guard; it does not establish
runtime acceptance for every other source variant or destination.

Evidence root relative to the task workspace:
`work/universal-v1-continuation/runtime/`:
- `alpine-animation23-contact-v2-launch.json`
- `alpine-contact-v2-side-hit.json` and `.ram`
- `alpine-contact-v2-recovered.json` and `.ram`
- `alpine-reproduced-corruption.json` and `.ram`

The earlier `alpine-animation23-close` fixture is superseded and invalid as a
near-spawn contact test: it wrote a scene-relative offset without the scene
subfile base, leaving the actor position unchanged. It is retained as diagnostic
history and is not accepted evidence. Breakpoint-based diagnostic traces are
also not used as gameplay acceptance.

## Automated integration

Release and Debug source-backed integration each pass 312 checks: exact animation
23 payload, shared 13/15 reuse, same-source no-growth, repeat-source and
cross-enemy deduplication, retained native model/scene payloads, original donor
immutability, genuine RAM and missing-provider refusal, resolved row 1 and fresh
ID65 provider integration. Normal exporter verification additionally writes and
reads back a repaired row-1 candidate without replacing prior output.

Unsupported source data and genuine memory/texture limits remain refusals. Frame
sound selectors and incompatible shared texture/geometry headers are rejected
rather than imported without dependencies. The animation parser's certificate
is scoped to primary collider damage and the direct-write helper's strict native
patterns; neither asserts that arbitrary behavior dependencies are complete.

## Normal exporter native retake

The actual current `UniversalMobyExport.Apply` path exported a fresh original
Artisans with Alpine row 1 and nine explicitly requested native removals. This
exposed an independent empty-slot bug: cleared unused rows retained group zero,
so native `KillMoby` searched a group list the row had never joined. The new
`UniversalRowRemoval` writes the original constructor's signed group sentinel -1.
See `docs/research/universal-unoccupied-row-group-20261001.md` for the
source-bound routine checks and the deliberate old-zero-group failure control.

Normal repaired candidate BIN SHA-256:
`04b94657ce92c8fbf34c567c9439d1ee01540115ea160b6e05d4a586a3fd985c`.
Cold New Game reached Artisans at 30 FPS. Read-only RAM confirms rows
30,32,33,94,109,113,121,124 are class1FE/group-1/stateFD; the Druid occupies reused
physical row29 while retaining its separate lifetime save identity. Its actual
state22/animation23 hit was captured with pointer `80122040`, RAM SHA-256
`35889d8083b8da5e8458af415f2a66c7c81b09478677a6d0e2eb7aaa523438e9`.
Spyro recovered normally and ordinary walking/charging continued. Recovered RAM
SHA-256: `1c23df92fb3d7f4cb813173036a549c7490a051b40b85ab8abd31e97279e2e34`.
The normal export has 44,296 bytes final headroom. It copies dependencies 10F and
13B, adds animation23, reuses shared13/15, and retains the native safety margin.

Private receipt: `runtime/normal-druid-fixed-native-receipt.json` under the same
work root. This retake used real USA BIOS, a separate initially blank card,
ordinary controller input and read-only sampling; no RAM writes or savestates.
Enemy defeat and a later card reload were not exercised in this particular run.
The earlier normal candidate `2d2a7e7f...` is preserved as the empty-slot failure,
not an accepted release artifact.
