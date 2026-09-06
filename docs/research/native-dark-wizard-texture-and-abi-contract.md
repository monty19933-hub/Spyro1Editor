# Dark Hollow native Wizard: texture producer and remaining ABI boundary

Status: a detached, exact-source texture/model-reference producer now passes
focused source-backed regression. This is **not** an intact enemy transplant,
candidate publication, runtime-fit certificate, or Create BIN approval. The
existing adapted Toasty v11 profile and Town Square V2/V3 outputs are unchanged.

This note supersedes two unsafe conclusions in the historical
[`dark-hollow-green-wizard-target-audit.md`](../dark-hollow-green-wizard-target-audit.md):
the raw Wizard handler does **not** expect its actor in `s7`, and zero bytes at
scene `B838` are **not** independent properties scratch. That older audit's
numerical source observations remain useful, but its “already-compatible shim”
and scratch-ownership conclusions must not authorize a new native transplant.

## Implemented source-owned resource seam

`NativeMobyModelTextureLayout.Decode` parses bounded animated-model normal/LP
face tables using the existing native renderer command grammar. It locates
actual twelve-byte texture fields, not pointer-shaped words. `Rebase` changes
only four UV byte pairs, CLUT and texture-page coordinates; ABR/depth/material
bits remain exact. It rejects unknown simple-model schemas, unsupported indexed
depths, overlapping/truncated owners, invalid relative targets and UV overflow.
It does not claim to decode all vertex, animation, collider or sound ownership.

`NativeMobyIndexedTextureAllocation` is a neutral deterministic allocator, not a
source authority. The exact native factory obtains the all-consumer ownership
report itself, protects every known consumer and every nonzero upload byte, and
commits a reservation only when both its pixel rectangle and independent CLUT
fit. Pixel rows use the native 1024-byte stride. Allocations cannot cross their
256-texel page or alias another pixel/CLUT owner. A failed CLUT reservation must
leave the allocator unchanged for retry.

`NativeDarkHollowWizardTexturePlan.Capture(originalImagePath)` binds the full
original image before and after extraction, including final length/position,
then binds exact level data, model intervals, the particle descriptor and a
fresh `NativeTexturePageOwnershipScanner` report. It emits detached packages,
137 target-VRAM row/CLUT patches, 60 typed model-field patches, and one particle
descriptor patch. It proves the target's complete unchanged complement,
including all **128 native placed rows**, properties and resident model bytes.
No model registration, scene row, overlay instruction or executable is changed
by this plan. `CompleteNativeClosureProven`, `RuntimeFitProven` and
`PublicationApproved` remain false.

Incoming electric-hurt player animation 8 is included in texture-consumer
reasoning: its complete `0x24`-byte header excluding only the two-byte frame
count matches Dark Hollow's resident animation 18. The comparison is against
source-issued player records, not caller metadata. It establishes the same
shared geometry/texture header consumers, not complete frame-stream validity.
The separate player-animation producer supplies that dependency.

## Exact source and extraction evidence

All offsets below are hexadecimal. Original MODE2/2352 image SHA-256:
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
WAD begins at logical LBA 37. Overlay runtime load base is `8007AA38`.

| Owner | WAD/source extent | SHA-256 |
| --- | --- | --- |
| Dark Hollow data, entry 14 | `F01000 + 28D800` | `a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97` |
| Wizard Peak data, entry 40 | `2F6B000 + 28A000` | `9973493f865b5e86bc17b32c5af9e3d7596e25615618c007ed28e0379161f2d8` |
| Wizard `11B` package | donor data `19B518 + 35AC` | `24df9ae279f32ba10a8e3bd25880d6989991b9225acf45755e6aaf985d66f529` |
| Lightning `26` package | donor data `1CAC88 + 564` | `9ea5c72ba675e9c6a81433f7408695399e19bc188b4b2a923069d71de601789a` |
| Particle-7 record | donor data `173518 + C` | `4baed93eb98ac71d27f59e3ddf8b5382dda7e74319de188749dce3831397f7b3` |
| Native model loader | EXE file `3BE0 + 26C`, PC `800133E0` | `fede3c414aea2dce0372fce574549d961123cf6456a7a291acef48b9c4af8598` |

The loader resolves root `+34`, animation roots `+38`, normal face `+14`, and
optional LP face `+1C`. At `80013518` a nonzero animation `+4` selects four-byte
frame records; zero selects eight-byte paired records. The codec accounts for
these headers before admitting face-table ownership. Wizard has 51 textured
commands in unique package-relative face tables `238`/`700`; lightning has nine
in `1A8`/`260`, across all its animations including native impact animation 3.

The following offsets are relative to each level's `0x80000`-byte right-half
VRAM image, which starts at data `+800`. Pixel rectangles are strided; palette
extents are 32 bytes each. Total copied bytes are **2056 pixels + 160 CLUT**.

| Native source descriptor | Source pixels / CLUT | Bytes per row × rows | Target pixels / CLUT |
| --- | --- | --- | --- |
| Wizard `7025 / 007A` | `58110 / 700A0` | `10 × 32` | `E400 / E420` |
| Wizard `722C / 001A` | `58160 / 72180` | `10 × 32` | `E410 / E440` |
| Wizard `7A27 / 001A` | `50100 / 7A0E0` | `10 × 32` | `E460 / E480` |
| Lightning `7822 / 0038` | `70050 / 78040` | `2 × 4` | `4400 / E4A0` |
| Particle 7 `752D / 0038` | `78010 / 751A0` | `10 × 32` | `E470 / E4C0` |

Here row-byte counts are hex; row counts are decimal. The source pixel SHA-256s
in table order are:

```text
2229d62ddadd04752fcc5790b019b55d166b774007ab646e46f1d7a440069dea
6832e7f686f47f7224b1e7b1bb136885d2326650bde4eaf99cffce3c74dba445
8ff165f1d889594f9221913fdfa69f471eb33c57ea0780be4b1ddf8f22317c12
e6f48a0036f29213687545ad901eb55949d15e150213f2db8b32f248d55ec411
31f14b4cd1994f468e2bd1d9d1937ca4e7bccd276cd32efa37ecfba4ecb92249
```

Fresh output goldens, not replacements for any existing output:

| Detached result | SHA-256 |
| --- | --- |
| Texture-patched complete Dark data | `9c62ac9289cfdbac95c3bcc89a0cb8fdcc0f3ca8e53c7dbdbc601fd152d9f0e1` |
| Rebased Wizard model | `776a28cdb88cd542a10898b372c3dbd5fe95357046f7f72641d14e309b7759f3` |
| Rebased lightning model | `51748c9b1eea600542e5f4988078ddcb8149e77156674faa2fe0539f1906fc88` |

## Native behavior ABI correction: not the old Toasty adapter

Fresh source disassembly establishes that **both** Dark Hollow and raw Wizard
Peak main loops keep the current actor in `s3`, and Spyro's coordinate base in
`s7`. Wizard's handler at `8008281C/20` reads state/properties from `s3`; at
`80082868/70` it reads Spyro X/Y through `s7`. Swapping those registers corrupts
the native ABI. Wizard additionally sets `fp=1` at `8007B830`; Dark instead sets
`s6=1` at `8007AECC`.

The stack ownership mismatch independently forbids simply branching into donor
code. Dark allocates `1D8` bytes, saves its registers at `1B0..1D4`, and stores
its main-loop cursor at `188`. Wizard allocates `230`, saves at `208..22C`, and
stores its cursor at **`1C0`**, which is Dark's saved `s4`. Donor common exit
`80086D8C` begins with `lw t1,1C0(sp)`. A new adapter needs an independently
owned donor-compatible frame and destination-context restoration, with every
reachable scratch access and exit rebound. The historical compact-overlay
relocator and Toasty register adapter do not establish this new contract.

Exact repeatable ABI certificates (overlay-relative = PC minus `8007AA38`):

| Source | Runtime start + length | SHA-256 |
| --- | --- | --- |
| Dark entry 13, WAD `EF6000` | `8007AE40 + F4` | `56d3261e7e3843e492530f9c4470b1818b3474d275d41325c8de2870a7530a7c` |
| Wizard entry 39, WAD `2F5B000` | `8007B7A8 + A0` | `352ef40d508106eee9499c0786a0a286bc2e58928df34e1ecba77364a73d8904` |
| Wizard handler | `80082818 + 80` | `be43dc716d5569b722dc37656e9fe02f7e0dd415b1334f443868c1caf82b7597` |
| Wizard common exit | `80086D8C + 4C` | `d8c482d83bfe5091eaa55ab056f74ca2a01f96e846e0aaffcc51fd780ccf129e` |

## Private state and still-required intact closure

Dark's Moby component is scene `8C30 + 5408`; its 128 rows begin at `8C38`.
Therefore `8C38 + 128*58 = B838` is the first native dynamic Moby slot, not a
properties arena. Taking `50` bytes there for properties silently steals live
allocator memory. New placement must preserve all 128 original rows (including
both original class-A6 users), allocate an additional placed row explicitly,
and account for reduced dynamic capacity. Genuine properties are the separate
component `E038 + E9C`; its body ends at `EED4`. A new private `50`-byte Wizard
property block and its internal `+28` pointer belong in that component through
typed growth and fixups, not arbitrary zeros. Existing fixup count is 147 at
scene `10EF0`; the new list and scene suffix require typed relocation.

Particles `7` and `41` need both spawn/update handlers, table bindings, owned
scratch, and continuations. Dark spawn current-particle register is `s2`, while
update is `s1`; the main actor ABI is a different contract. Native update/spawn
tables are at `8007AB74`/`8007ACB8`. Their missing selectors are not satisfied by
importing only the particle-7 texture record. Exact raw donor fragments already
identified are spawn `89934..89A50`, tail `89F28..89F34`, spawn-41
`89F34..89FF8`, update-7 `88370..88408`, and update-41 `888E8..889E4` (all
addresses with `800` prefix). Their source/target register and stack semantics
remain a separate typed producer gate.

The new native profile must retain electric flags `20020`, hurt state 7/player
animation 8, and all four native lightning-impact children. Source model-local
sound descriptors **49/50/51** remain `31/32/33` in this texture result; they
are not global sound-map IDs and still need exact sample/descriptor relocation.
Global electric sound `1F` is another dependency. Screen-filter semantics,
effect/object pool demand, movement/route dry-surface validation, global
ownership, and final composed RAM/transient lifetime must also remain explicit.
No old v11 generic-hurt substitution or impact-child quarantine is carried into
this new native profile.

## Verification checkpoint

Initial focused Debug build passed with zero warnings/errors; source-free smoke
passed **223 checks**, and `--source-v3` passed **718**, including all 35 native
registration/RAM baselines, the new exact texture extraction, and unchanged
Town V2/V3 goldens. Independent review verified page/CLUT encoding,
transactional allocation and preserved complement. Final small hardening adds
relative-pointer overflow refusal, CLUT-failure rollback, explicit new output
goldens, and end-of-source-read length/position checks; these await the root's
serialized frozen Release gate. No source ROM, candidate or installed app was
modified and no candidate was emitted by these tests.
