# Artisans C2: bounded source-access audit

2026-09-25. Read-only original-USA byte/instruction audit. No build, native-byte
write, candidate generation, emulator execution, or placement approval. This note
extends `next-native-moby-family-evidence.md`; it does not replace its limitations.

## Source and method

Opened `/Users/john/Documents/Codex/2026-04-24/could-you-create-a-level-editor/Spyro the Dragon (USA).bin`
read-only. Full-image checks at the beginning and end matched 661,547,040 bytes,
SHA-256 `fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
Used the existing independent oracle's MODE2/2352 logical-read mapping (2048
user bytes at sector +24), with bounded inline instruction decoding, not CPU
execution or a newly built editor assembly. WAD LBA37 directory entries9/10
read back as `0x7F2800/0xE000` and `0x800800/0x383000`.

| Re-read owner | SHA-256 |
| --- | --- |
| Artisans overlay | `7df6cf2f3ed4a71a123f03621019470bd6fe0cd243f75b1e8c3a115e6b1fcb55` |
| Artisans data | `86cde4f886d97821a3a4b869100b586842dbd4b9fb696558bead274bb547af4f` |
| Scene, data+`0x1C9800`, length`0x14800` | `6ca481a134fa5e2801b31cfeb97ec22a8bfb56c46cd0990ecc5718ab61a99342` |
| Original executable, LBA53875, length`0x66000` | `a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998` |

T50/T51 row hashes and all201 fixups reproduce the earlier note. Scene count is
174. Their properties pointers remain `0x11394`/`0x1139C`, each inspected eight-byte
window is zero, each has just its own row as registered inbound reference, and
neither window contains a registered fixup. This is **window ownership evidence,
not proof that either complete parent properties owner is eight bytes**.

## Concrete access and dependency trace

- C2 dispatch: `8007DB94` loads class`0xC2`; branch`8007DB98` enters
  `[800829FC,80082B5C)`. The branch delay slot is retained in the audit.
- At`800829FC/80082A04`, the update reads `row+0 -> properties`, then
  `properties+4` as a word. Nonzero invokes`80038458(row)`. That helper calls
  `80038340`, temporarily adjusts row Z for `8004D5EC`, then adds signed
  `row+0x38`, writes row Z, and calls`800533D0`. The first two helpers' direct
  instructions do not establish all transitive collision/helper accesses.
  The C2 block itself contains no direct properties store. This proves an
  accessed lower bound of eight bytes, **not an exact extent or immutability**.
- The update tests `row+0x18 & 0x000B0000`. The nonzero path writes
  `row+0x55=0x20`, then invokes`8003851C(row,0,0)`. That helper reads
  `class-model-root+4` and calls native sound routine`80055A78`, using
  `row+0x54` as handle storage. Actual C2 slot9/root`0x1AE664` has byte`0x41`
  at root+4. This is a concrete sound selector, not a closed sound-bank/voice
  lifetime certificate.
- Artisans level10's EXE binding block`8005A718..8005A7B0` sets spawn callback
  `800758CC -> 800857CC` and effects callback `800758E4 -> 800873E0`.
  C2 requests at most two`0xFF`, two`0x100`, and six`0x101` children. Each loop
  checks `*(800756A8)-*(800756A4) >= 21`; this is not complete peak-demand proof.
  Their resident model roots are slots27/28/29 at data`0x1C73F4`, `0x1C7550`,
  `0x1C7654`. Root presence gives neither package extent nor rendering closure.
- Spawn`800857CC` calls allocator`800524C4`; that allocator uses separate
  `0x58` row and descending`0x18` runtime-properties strides and installs the
  latter at child row+0. The parent ordinal is converted to child byte+`0x56`
  with an explicit `<256` test (otherwise zero). FF/100/101 route to
  `80085C4C`: direct child-properties writes cover halfwords+0,+2,+4,+6,+8,+A
  and words+C,+10, with RNG, parent-position/impact and physics helpers.
  These are **child runtime slots**, not the parent serialized-properties size.
- Effects calls are `(count5, selector2, position=row+C, argument0)` and
  `(count16, selector0x46, position=row+C, argument0x20)`. The selector table
  at`8007AD18` routes them to`80087520` and`80087F60`; initial allocation calls
  request type2 and type1 from`80053570`. These callback selectors are not
  the separate particle-descriptor IDs. Complete effects/renderer/VRAM and
  lifetime closure remains outstanding.
- `8003ABC0(row,3,0,0)` reads reward selector`row+0x53 & 0x7F` (both source
  rows are`0x54`) and guards/sets `row+0x3A` bit`0x80` before the selected
  reward spawn. Then`8003B7C0` bounds-checks the placed-row range and sets its
  retirement bit in the`800778B8` bitset. C2 exits via`80085438`, which calls
  `80052568(row)`. Existing reload owner`800142C8..80014404` contains the
  collected/checkpoint row handling; re-read hash matches the current
  persistent-owner source certificate. This does not certify save transactions.

## Exact instruction-window pins

These identify the inspected windows, not a claim that all functions or every
transitive owner have been captured. Addresses are original native runtime PCs;
overlay file offsets use base`8007AA38`, EXE offsets use base`8000F800`.

| Start / bytes | SHA-256 |
| --- | --- |
| `8007DB94 / C` | `a04097b1b24ef96de5d2ddd89d8b12b4faddee3152d7201d519d14b8f6129383` |
| `800829FC / 160` | `8db54c32b58e46715f89f990c1fabf219fe9cc5d6dd5ccefd826e07f2c0cab05` |
| `80038458 / 3C` | `1fe1b570ace9e36b673686423839d99ec9df1f3f048da204b53283110d29ce60` |
| `8003851C / A0` | `af902cc21e59f2794bc459d6cfaf528f36c28b73f39fc4221c1fe6d384331e91` |
| `8005A718 / 98` | `fe806d69534bcb66ae79423ff1c325fe7785c5c3857b4d92898df8b565494dfa` |
| `800524C4 / A4` | `6625282792e81215f1f35e1aa8b4240937206bcc8bd494eef9e000f65a4bbf9e` |
| `80085C4C / 1AC` | `0646b82d3be5bd9f562476900869721dad07324a63add22956da3e732b03eee2` |
| `8003B7C0 / 94` | `a8378b5d829c8a0661affbb6e50eb64b606c3114e116a38ec1b7137ba8a30ec4` |

## Certificate boundary and smallest next step

An independent follow-up traced the placed-parent loader, rather than the
FF/100/101 child initializer. The Artisans row header at Scene`0xA2A4` has
length`0x63D8` and count174. The private-properties header at`0x1067C` has
length`0x1850`, making its payload`[0x10680,0x11ECC)`. Loader instructions
`80013FC4/80013FCC` skip that complete block; there is no per-C2 length there.
Registered-field relocation`[80014038,80014084)` fixes the parent row pointers.
Shared initialization`[80014158,800141BC)` selects model-root helpers
`[800526A8,800529CC)` / `[800529CC,800529E4)`; direct instructions initialize
row model/animation/transform fields, not parent row+0 properties.

T173 is also C2, with properties at`0x11EC4`, exactly eight bytes before the
independently length-delimited block end. This is stronger boundary evidence
than T50/T51 adjacency, but not a uniform per-C2 grammar or mutable allocation
certificate. Properties+0 semantics and transitive collision/global lifecycle
accesses remain unresolved. Root independently re-read all five window hashes
and this final-row boundary in log726, with held/current Original finals:

| PC / length | SHA-256 |
| --- | --- |
| `80013F24 / AC` | `6338caf975b0fa524052abe3e2b439292791273aac359dc5e6d78b90d0a37e33` |
| `80014038 / 4C` | `859120e6052efe6b6639acc060e33a06740c057c112fea7de12d16dd1f711dfb` |
| `80014158 / 64` | `e88d512f22084bb0a28ba741f2e83962d436e65a29dbb8a92a5fc9009c0225f6` |
| `800526A8 / 324` | `5483d5e337730567fd89f6118036d31f8c3a93fd281ff444b5d8149b4e2f03d5` |
| `800529CC / 18` | `4973eb40a058490002e752ee189564f94c173b9489f03b6f77bd25f2b2a3c072` |

The **placed parent C2's complete startup/loader path and serialized field
grammar were not closed** by this bounded audit. No complete C2 private-schema
or instantiation certificate can be issued. In particular, the proven runtime
child allocation must not be repurposed as a parent allocation rule.

An evidence-only T50/T51 fixture can now pin these real owners and reject stale
bytes, eight-byte adjacency-as-size, hidden aliases, and changed callback/sound/
reward bindings. A byte-identical existing-owner control may assert complete
source/complement equality while preserving the target row, properties pointer,
root, reward, ordinal and fixup list; label it a no-op, not safe instantiation.
Before a typed mutable template is issued, close the parent loader/initializer,
all transitive accesses and exact source extent; then prove complete fixup and
inbound-alias ownership. Cross-level admission additionally requires model,
sound, effects, child/reward lifetime, destination globals/capacity and save-owner
certificates. Neither this note nor root registration relaxes any placement gate.

## Evidence-only regression fixture

`NativeMobyDependencyClosureSmoke` now invokes
`NativeC2ArtisansEvidenceCases` only in its existing explicit `--source` mode.
It pins actual ISO/WAD/source owners, observed windows, root/sound/callback/
reward/retirement consumers and all201 fixups. The only copy is an in-memory,
byte-identical observed-window no-op; no image or native instance is written.
Stale owners, inferred complete8/24-byte sizes, hidden row/registered aliases,
fixup changes and altered callback/sound/reward bytes refuse. The shared
`Reject` helper requires the expected diagnostic substring, not just any error.

Build723 passes0 warnings/errors. Source724 passes4510 checks including the
new `NATIVE_C2_ARTISANS_EVIDENCE_NOOP_SOURCE_OK` marker and held/current-path
whole-image final checks. This adds no Core capability, mutable schema,
exporter, import approval or gameplay evidence. Full all-project regression725
passes345 steps with zero failures. Its default path remains source-free;724
is the separate, explicitly source-backed C2 result.
