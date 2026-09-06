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

## Preservation and emulator cleanup

The owned Wizard observer and DuckStation session were closed normally. Temporary playback/controller settings were restored from their original backups. The original memory card was never overwritten. Final post-gate SHA-256 checks match the original BIN/CUE, retained Wizard BIN/CUE/receipt, both restored DuckStation INIs, and original memory card.

The finalized runtime recording is `_local/native-wizard-runtime-20260906-r01.rSQXSb/videos/wizard-r01-native-normal-entry-session-01.mp4`, SHA-256 `4bcc3f7b8a33025fb0a86b2f3a3dd104adea4d5fbfe466355239c9a7226ea1ce`. Container inspection and full video/audio decoding passed. This is not a listening-quality or Wizard-audio acceptance claim.

## Next implementation gates

1. Preserve the completed compiler scope/metadata/strict fixture-lookup checkpoint at `0fa2490`; its exact source-free regression must never invoke the legacy fixture builder.
2. Compile the fresh body into genuine appended WAD entries 79/80, relocate the EXE, and update actual ISO ownership. Authenticate all original sectors in the same pass as the complete image hash; all later metadata reads and both output verification/publication must use that private authentication. Establish independent whole-afterimage, EOF, cancellation, inverse, padding and parity regressions before adding a publisher.
3. Reuse the existing certified music compiler through exact intermediate executable bytes; establish the bounded native title and normal-entry profile. The proposed fixed `LAB` title fits a four-byte source owner; arbitrary names do not. The known global game/transition state 12 bypass remains excluded, and this state is not Spyro's animation/player-state field.
4. Preserve the now-tested detached two-texture allocation. Next recompute the shared staging/body layout from an exact closed, issued revision rather than introduce a second independent assembler. Reject old staging even if its transfer bytes match the new revision.
5. Finish Wizard defeat/reward, post-defeat exit/re-entry, repeated combat, rendering/audio and peak resource checks; then test independently placed multiple instances. Only widen native importing when dependency and gameplay evidence support the additional combinations.

Normal export approval, complete dependency closure, all-state/checkpoint support, unbounded Moby interchangeability, complete new-level UI integration and a fully editable shipped blank-level editor remain open. No build-only result promotes those gates.
