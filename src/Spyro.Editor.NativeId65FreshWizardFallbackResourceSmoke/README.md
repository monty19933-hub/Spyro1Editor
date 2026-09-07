# Detached fresh Wizard fallback resource smoke

This is a separately named, exact-source resource revision. Its only factory
takes the actual issued A/N16 Wizard resource plus the exact original BIN. It
does not widen any existing resource, scene, staging, body, or publisher grammar.
Default execution is source-free. Optional `--source original.bin` is read-only;
there is no output/publication/fixture mode in this smoke.

The revision preserves the full prior `0x17800` model allocation, `0x8FC` data,
and `0x65800` sound bank. It appends complete original Town classes `0x0E`,
`0x0F`, and `0x10`, in that order, producing `0x19000` models. Simple class0E
keeps its distinct native grammar; adjacent original0F supplies the renderer's
two-word lookahead. The animated decoder is unchanged and still refuses0E.
The final `0x558` bytes are generated padding, separate from retained prior
Wizard `0x6CC` padding. The new model registrations total seven; number classes
`0x104`/`0x105` remain existing members of the source's54 shared PETE owners,
not newly imported local models or newly certified callbacks.

Texture ownership consists of67 new source row/CLUT spans (1120 bytes), plus
four unchanged two-byte aliases to the same original Town owner. Existing
protected zero bytes are not free space. Complete source extents and the full
unchanged VRAM/model/data/audio complement are checked. No texture fields,
audio selectors, or model references are re-encoded by this revision.

Independent pre-production raw goldens:

- Upload: `8c4438fc13eca3b3bd40e9c056094c308fb272aded227577fb812ec9b16d96dc`
- Models: `84d00dbbce264bc08c61abfe2579fe3d7a7464ba2df6b189834664da10b82c84`
- Unchanged data: `bea93c58d6d568c7c6162816ddad1afff32a51886f9412a0809b22887e0b83d4`

Actual focused Release build:4.93s, zero warnings/errors. Default:540 checks,
536 expected refusals; unchanged linked hostile-stream suite:23 checks.
Actual source:527,211 cumulative checks, 2,213 expected refusals, both raw
goldens exact. Tests cover byte-identical wrong owner classification, complete
partitions, exhaustive protected-byte complement, copy-on-read, exact actual
ancestry versus a different byte-identical result, upstream JSON preservation,
old grammar refusal, and bounded hostile stream/EOF/cancellation behavior.

Private evidence is retained at
`_local/research/dark-wizard-native-closure-20260906/fallback-resource-implementation-r01.q5dVlG/`.
The earlier independent raw proposal is
`../fresh-town-fallback-assets-r01.VLlafm/` under the same research directory:
3018 checks/4 expected refusals, repeated independently by root.
The private actual public-C# issued fixture then passed a separate native
seven-root/simple-prefetch gate:34 checks/28 expected refusals. Package-only
read failed at `0x800A27B4`; classified next0F ownership made both delayed reads
pass. Synthetic header/copy-address, post-GTE and separate loop-end entry
witnesses are explicit. No helper/GTE/whole-renderer or runtime was fabricated.

Global1F compatibility, full callback/effect/collider lifetime, peak resource
pressure, save/reward identity, body/scene/overlay/disc composition, and runtime
remain false or pending. Source-byte issuance is not universal portability.
The default runner now includes the neutral C# mode. Both source C# and the
permanent `source_fallback_resource_oracle.py` remain explicit-source-only;
default/list paths do not open the BIN. Five actual missing-registry REDs were
retained before integration; whole Python111 tests/four source skips passed,
including51 runner tests. Every raw source oracle now has generated marker,
nonzero-exit and inter-case deadline controls rather than hand-written ordinals.
Root's promoted raw3018/4 and actual issued34/28 repeats match the independently
frozen outputs byte-for-byte. See `docs/research/native-fresh-wizard-fallback-resources.md`.
