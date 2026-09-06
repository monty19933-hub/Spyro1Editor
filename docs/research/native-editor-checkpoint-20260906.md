# Native editor checkpoint — 2026-09-06

The frozen source at `b50c9a83f03175c77979af339595e1e48ecf0f23` passes the complete Release gate: **290 steps, zero failures**. This is source/build/readback evidence, not universal Moby or new-level gameplay certification. The installed editor was not replaced or launched by this gate.

## Completed, separately bounded work

- Persistent native Moby identity: original retail ordinals remain fixed; append-only authored owners survive serialization/reopen. Retired owners remain reserved rather than renumbering native save-bit identities. The original 4,877-placement/35-level inventory remains intact.
- Source-native Wizard append and placement: private source-issued instance/properties allocations, pointer fixups and count-aware placement preserve original rows and non-coordinate bytes. One- and two-family afterimages are independently reconstructed from original source bytes. Two-family geometry checks are not a two-enemy runtime certificate.
- Genuine fresh level data: the new normal body assembles fresh terrain, collision, scene lists, spawn, resources, upload staging and complete source-owned auxiliary sections. It does not inherit Town Square's terrain or placed scene. It is not yet a published/booted independent level.
- Static texture sources: complete source-bound descriptors, palettes and pixels are captured across levels, including protected zero bytes and explicit distance/clip constraints. This supplies inputs to a fresh-level allocator, not permission for arbitrary animated/rotated textures or an unbounded VRAM claim.
- Native Wizard runtime observation: the research candidate entered Dark Hollow normally, spawned the foreign Wizard, produced lightning/projectile children, electrically hit Spyro, and completed a normal death/reload with the Wizard restored. The live observer now refuses dynamic scans while the placed row still contains an unrelocated properties pointer.

Details and limitations are in `native-dark-wizard-r01-runtime-observations.md`, `native-id65-normal-body.md`, `native-static-terrain-texture-source-contract.md`, and the append/placement smoke READMEs.

## Full regression evidence

Command, from the repository root:

```sh
python3 tools/Run-SpyroEditorRegression.py --build-all --with-source-qa --configuration Release
```

The runner executed 218 project builds, 49 default/mode checks, one Python runner-test step, and 22 source-backed steps. All passed; summed step time was 1,496.72 seconds. The Python runner step contains 28 passing tests. Separately, the complete Python test discovery passed 42 tests, including 14 native observer tests.

Retained evidence is `_local/full-native-regression-20260906-r01.2c9Jhh/`: the full console log, copied `runner-report/` with all 290 logs and `summary.json`, the compiler process sample, and `post-gate-inputs-sha256.txt`. The original temporary report was copied, not deleted. Its JSON log fields still identify the original temporary paths; the corresponding basenames are retained in `runner-report/`.

The source-offline QA step took 913.06 seconds and passed its final BIN/CUE identity guard. It exercised export/readback and headless UI rendering; it launched neither DuckStation nor a visible editor window. Its generated visual metrics are engineering checks, not a claim that every screenshot has received new manual visual acceptance.

The publisher build in this frozen gate took 247.08 seconds. A compiler sample recorded a 20.7 GiB physical footprint. That observation alone does not identify a Roslyn pass or prove causation. The earlier two-second publisher-only build excluded project references and must not be presented as a controlled fix for this dependency-inclusive delay. The subsequent controlled experiment and source-equivalent compilation-scope change are documented in `v3-smoke-compiler-scope-experiment.md` and committed at `0fa2490`.

## Subsequent texture and compiler integration gate

The follow-on source-free Release gate, with the two new texture Core files and
their explicit regression registration, passed **103 steps, zero failures**:
51 builds (including App), 51 run modes, and one Python runner-test step. Summed
step time was 65.78 seconds. This is the default gate, **not** a second full
`--build-all --with-source-qa` run. The still-unregistered fresh-disc project was
an intentional missing-production-type RED scaffold and was not included.

Evidence is `_local/fresh-texture-default-regression-20260906-r01.Lq9Rw3/`:
the complete console log and byte-for-byte copied `runner-report/` retain all103
step logs and `summary.json`. Its original temporary report remains available;
the copied JSON log fields still identify the original paths. Separately,
complete Python discovery passed44 tests. Texture runner registration was tested
RED before adding the new exact free/source markers, then the explicit native
source inventory assertion was extended to include this read-only mode.

The detached two-material allocator's first focused Release build passed with
zero warnings/errors,7,572 source-free checks and33,022 additional source checks.
Independent original-disc reconstruction and peer review agree with both whole
upload/data goldens, all46 descriptors and their complete palettes/pixels,
31 allocated regions,16 contained aliases and28,160 allocated bytes. Source,
companion, particle and SPU ownership remains protected, including zero bytes.
The old untextured scene/model/data/upload hashes remain unchanged. Further
hostile-test additions have their own focused evidence in the texture smoke's
README; do not conflate counts from different snapshots.

This allocator produces a detached revision, not a replacement body, physical
candidate, editor UI capability or gameplay certificate. Pairing it with the old
staging/body is intentionally unsupported. Its allocations happen below the
retained transfer-residue interval, so a new correctly paired transfer image may
have identical bytes: complete upstream revision and upload identity remain
necessary even when transfer hashes match.

## Subsequent fresh-disc and closed textured-body checkpoints

Fresh-disc composition was committed at`e106ad2`: actual reserved WAD79/80,
source-authenticated full-image sector afterimages/inverses, fixed LAB title,
46 built-in tracks and exact ISO/EXE relocation. Its default gate passed107
steps after correcting one older test's ambiguous name-only reflection lookup.
See`native-id65-fresh-disc-plan.md` for the566016-check source result, both-size
full inverses and all three independently reproduced physical hashes.

The new closed textured resource revision now reuses the same staging/body
assemblers, retaining its actual source-issued ancestor and full upload/plan
identity. Old plan JSON and all old byte goldens remain exact; new used data
0xA38 shifts native model/scene addresses by0x170 without growing the CD sector.
The current disc issuer explicitly remains untextured-only. Focused new tests
passed18 free+139 source, and old body/resource source checks passed791/15423.

The integrated default Release gate passed109 steps, zero failures,67.67 seconds
summed:54 builds including App,54 run modes and one33-test Python runner step.
Separate complete Python discovery passed47 tests. A fresh source run of the
rebuilt legacy fresh-disc smoke passed566016 checks again. Retained evidence:
`_local/research/fresh-native-textures-20260906/textured-body-default-report/`
(byte-compared copy),`textured-body-default-release-gate.log`, and
`textured-body-legacy-fresh-disc-source.log`. This is not a second full
`--build-all --with-source-qa` gate. No installed app or emulator session changed.

## Fresh research publication and observer checkpoint

The exact A/N16, untextured LAB/track19 research publisher is implemented and
independently reviewed. Its private plan accepts only the actual issued disc
plan and complete known body/overlay/EXE/WAD/root hashes. Streaming output
authenticates every original raw sector; staged and final files are fully
reopened, companions are exact-bounded, and publication is new-only with CUE
last. A reproduced stale third-FILE-alias defect is now guarded by fresh
binding checks on the same bounded CUE snapshot.

Focused checks: 60 source-free, 68 with original source, 33 forced staging,
and 20 successful-publication/verification cases. The corrected final R02
corruption case targets actual untouched Form1 P-parity at LBA38 +2076; earlier
R01 exercised Form2 payload and remains separately retained. A fresh process
recaptured the source and successfully verified the exact R02 three-file set.
No runtime, ordinary Create BIN or complete native closure approval follows.

R02 candidate directory:
`_local/native-fresh-id65-runtime-20260906-r01.LbsgK0/publication-r02/candidate/`.
BIN `1818fe50a132901b9ad670769d4967ccc0a5120897637efa2ad709b8f1d53188`;
CUE `7e2bd7f0dcf23af6fc7ed5e7be56accf7b7ee354b335c238bd446c863f13023f`;
receipt `fd4ebbf408b06e5be3d3ff83c4926362856236b70feda4ef2095fd59587eabbd`.
Original BIN/CUE, both live DuckStation INIs and original card were freshly
hashed unchanged before any new emulator launch.

The fixed fresh-ID65 read-only observer was committed at `e741add`: 15 observer
tests, five exact-source scalar fixtures and the unchanged Wizard's 14 tests
pass. Root and independent peers reviewed/replayed it; it has no execution or
memory-write commands and cannot authenticate a loaded disc from annotations.
See [observer notes](native-fresh-id65-observer.md).

Integrated default Release gate: **111 steps, zero failures**, including App.
Python discovery: **68 tests, one explicit-source skip**, with that source case
separately passing. Default and opted-in source registration cannot invoke
publication, staging or output-verification modes. Exact logs and a byte-compared
copy of the default report are under
`_local/research/native-id65-engine-profile-20260906-r01/` with prefix
`fresh-disc-research-`. This is not a new full build-all/source-QA gate or app
installation. The candidate has not yet been booted at this checkpoint.

## Fresh LAB native runtime follow-up

The exact R02 BIN has now booted in DuckStation and reached LAB through the
native inventory selector. Spyro, Sparx, companion particles and the isolated
floor were visible without Town Square scenery; the native inventory showed
LAB. The first movement overshot the small authored rectangle and is explicitly
not a passing center walk. Native selector re-entry restored the center, then
10% wall-clock input pacing allowed short on-floor movement. Pacing returned
to100% for roughly50 seconds of idle. Ten matching read-only layout samples,
the original recording and screenshots are retained.

See [exact sequence, evidence and limits](native-fresh-lab-r02-runtime-observations.md).
This establishes limited entry/idle/controlled-walk observations, not ordinary
portal/death/persistence, arbitrary geometry/Mobys/textures, music/listening or
peak-resource acceptance. The observer's historical false runtime flags remain
unchanged; matching addresses are not full resource authentication.

A test-first observer follow-up now warns when sampled XY leaves the exact
authored floor. Root review also removed an unrelated floor warning from the
synthetic baseline, so older resource-warning tests cannot be accidentally
satisfied by the new warning. Root replay passed22 default tests/one source
skip and all five exact-source checks. Historical recordings are unchanged.

Both fresh sessions are closed: capture finalized, observers quit, DuckStation
exited normally without resume saves, original INIs restored byte-for-byte.
Original card, BIN/CUE and R02 candidate hashes were rechecked unchanged.
The installed editor was not replaced or launched.

## Fixed starter factory integration

The exact A/N16/untextured/track19 recipe is now composed by
`NativeId65FreshStarterResearchFactory`, not only by smoke-local code. Its sole
public operation accepts original BIN/CUE and a cancellation token, forwarding
that token through the existing privately issuing producers. No automatic
publication, arbitrary profile or document-export authority was added.

Test-first missing-API RED was followed by a zero-warning/error4.86-second
focused build,73 free and87 source checks. The old source recipe remains a
negative/comparison control; full old/new PlanSHA, CUE and receipt agree. A
fresh factory-backed process fully verified retained R02 again without writing
it. Root and a separate reviewer found no source-identity/authority expansion.

Integrated default Release gate:111 steps/zero failures,70.59 seconds summed;
App rebuilt but was not installed/launched. Complete Python discovery:70 tests,
one source skip; the five-test exact-source observer run separately passed.
Evidence and byte-compared default report:
`_local/research/fresh-native-starter-factory-20260906/`.
This is not a second full build-all/source-QA gate.

## Preservation and emulator cleanup (prior Wizard session)

The owned Wizard observer and DuckStation session were closed normally. Temporary playback/controller settings were restored from their original backups. The original memory card was never overwritten. Final post-gate SHA-256 checks match the original BIN/CUE, retained Wizard BIN/CUE/receipt, both restored DuckStation INIs, and original memory card.

The finalized runtime recording is `_local/native-wizard-runtime-20260906-r01.rSQXSb/videos/wizard-r01-native-normal-entry-session-01.mp4`, SHA-256 `4bcc3f7b8a33025fb0a86b2f3a3dd104adea4d5fbfe466355239c9a7226ea1ce`. Container inspection and full video/audio decoding passed. This is not a listening-quality or Wizard-audio acceptance claim.

## Next implementation gates

1. Preserve the completed compiler scope/metadata/strict fixture-lookup checkpoint at `0fa2490`; its exact source-free regression must never invoke the legacy fixture builder.
2. Preserve the now-published and separately verified R02 A/N16/LAB/track19 candidate, original source and retained test evidence. Keep normal exporter and general runtime approval separate.
3. Preserve the actual fixed-ID65 native entry/idle/controlled-walk milestone and its off-floor diagnostic regression. Expand action/transition tests deliberately. The known global game/transition state12 bypass remains excluded; unresolved hardware/resource lifetimes are observation priorities, not claims of demonstrated failure.
4. Preserve the now-tested two-texture staging/body composition, including exact legacy identities and mixed-revision refusals. Do not silently pass the new profile through the untextured disc issuer.
5. Finish Wizard defeat/reward, post-defeat exit/re-entry, repeated combat, rendering/audio and peak resource checks; then test independently placed multiple instances. Only widen native importing when dependency and gameplay evidence support the additional combinations.

Normal export approval, complete dependency closure, all-state/checkpoint support, unbounded Moby interchangeability, complete new-level UI integration and a fully editable shipped blank-level editor remain open. No build-only result promotes those gates.
