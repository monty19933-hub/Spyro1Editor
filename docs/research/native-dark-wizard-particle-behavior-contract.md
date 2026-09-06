# Source-issued native particle 07/41 behavior

This detached source issuer supplies the actual Wizard Peak particle code needed
by native Green Wizard/lightning. It does not install the fragments, patch the
disc, replace an effect callback, authorize a candidate, or establish gameplay or
peak-memory acceptance. The separately issued descriptor7 stage remains a data
owner, not a code certificate.

## Exact original owners

`NativeDarkHollowWizardParticleBehaviorSource.Capture` verifies the complete
original USA image before and after reading. Its immutable domains are:

- Wizard overlay: WAD `0x2F5B000 + 0x10000`, runtime base `0x8007AA38`, SHA-256
  `5d3996111567ffac6eca08d340f2f691824bb7969ba2a0cd95e9346ccfe157e1`.
- Dark overlay: WAD `0xEF6000 + 0xB000`, same runtime base, SHA-256
  `9920aa47b94005166f457b5b36df6dedd1f2400f91698bac21cdb01d8590ef32`.
- Shared executable: file length `0x66000`, runtime base `0x8000F800`, SHA-256
  `a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`.

The public issuer has no public metadata constructor. Original source reads use
identity-bearing address-space objects; similarly named domains do not bind.
Returned byte arrays are copies. The generic instruction/operand helpers do not
issue native source authority.

The four captured code owners total `0x380` bytes / 224 instructions:

| Owner | Source PC | Length | SHA-256 |
| --- | --- | --- | --- |
| Particle07 tick | `80088370` | `98` | `a2f5681329e63ab489abec1424ecc230f462f4af53bfc5d0e0ae94bb5a09afc4` |
| Particle41 tick | `800888E8` | `FC` | `bdfa5cf0752d3ca410a216f83a31d196492bd22de836ccfba9255582a5c01a19` |
| Particle07 spawn | `80089934` | `11C` | `c3e0db5def4d5c5c63c10b3e8acf314ebabfa91c93f8a2087a268e78da2df6e3` |
| Particle07 shared tail plus particle41 spawn | `80089F28` | `D0` | `fbff816f4c054e52e663d05b0c170db721b3931ce7baac3bd93340837db075d8` |

The last owner has entries at `80089F28` and `80089F34`. Coalescing this adjacent
tail and spawn body preserves exact native fallthrough ownership; no padding or
invented native fragment is used. All four complete owners end in a native
control transfer with its delay slot; none requires an appended fallthrough
veneer.

## Narrow operand adaptation

Wizard tick uses `s2 = raw particle + 1`; Dark tick uses `s1 = raw particle + 1`.
Exactly 37 source instructions use that register as their load/store base:
18 in particle07 and 19 in particle41. Only their five-bit `rs` field changes
from `s2` to `s1`; opcode, destination/value register and signed offset remain
unchanged. No branch register, general register permutation, or frame-wide
save/restore adapter is introduced.

Particle41 uses 12 halfword stores/loads at `sp+70/72/74`. Their exact instruction
fields are rebased to `sp+30/32/34`, within Dark's case scratch area below its
saved-register region. This is not a claim that every byte of either stack frame
is interchangeable. The source uses only SH/LHU at these fields; word-width,
different-offset and unrelated-register edits are rejected. Stack metadata
reports the adapted halfword offsets, and delay-slot metadata reports adapted
words while original reads retain their original native SHA certificates.

All 49 exact before/after instruction words are code-owned and validated before
copying output. The neutral helper bounds a fragment to `0x380` bytes and a
ledger to 49 unique instructions. A finite adversarial enumerator confirms that
the 50th item causes refusal without asking for a 51st. Caller ordering cannot
choose different bytes.

Operand-adapted, not yet control-relocated, tick hashes are:

- Particle07: `9e2b85a2ed04e469655995982c7f2f4690cb6c414dfa7b080f1a7270c84a08e4`.
- Particle41: `010d86e840c90f458fd1d99fcfdb6149d87b38d129fcb158889c43b732fd0a75`.

Spawn07/41 require no operand edits: both callback implementations use `s2` for
the allocated raw particle, `s3` for context, `s4` for the caller argument and
`s5=0x80`. The copied spawn fragments do not access the stack. The issuer pins
both source and destination callback prologues, complete overlays and native
continuations; it does not infer these roles from matching register names alone.

## Native continuations and preserved callbacks

Dark retains its own particle callbacks. EXE `8005A878 + 20`, SHA
`a4e1d56356d71a62cf4dcd80e14318f8f8f0022b32133796021b3350c22b0ae7`,
sets `g800758E4 = 800844A0`, then jumps to the shared initializer tail with
`v0 = 80083274`. EXE `8005B6D8 + 28`, SHA
`21cda117e3a5dd3a99cbfd588443a031501ca79b148102d67c8261d15dfdf2b2`,
writes that tick callback into `g800756BC`. This stage never writes either global.

Four source-issued hook claims name actual default table words:

| Hook | Original field PC | Before | Incoming source entry |
| --- | --- | --- | --- |
| Tick07 | `8007AB90` | `80084450` | `80088370` |
| Tick41 | `8007AC78` | `80084450` | `800888E8` |
| Spawn07 | `8007ACD4` | `8008553C` | `80089934` |
| Spawn41 | `8007ADBC` | `8008553C` | `80089F34` |

They are read-bound claims, not writes performed by the source issuer. The
installer must own the actual relocated hook words and preserve all other
original dispatch entries and overlay bytes outside explicitly declared writes.

Six native control sites map through four certified continuation bridges:

- `8837C/8838C → 893EC` maps to Dark delete continuation `84448`.
- `88400 → 893F4` maps to Dark next-particle continuation `84450`.
- `889DC → 893D0` maps to Dark tick tail `8442C`.
- `89F2C/89FF0 → 8A848` maps to Dark next-spawn continuation `8553C`.

Addresses in this list have high prefix `800`. The bridge bytes are only
`J exactNativeContinuation; NOP`: they preserve live temporary registers. In
particular, particle41 carries computed `v0/v1` into `8442C`; mapping to `84410`
would repeat increments. Particle07 keeps its donor tail at `89F28`, which writes
`t0=4` to particle byte11. Reusing Dark's visually similar `84CE4` tail would
misinterpret live `t0/v0` and corrupt the sprite command. Both invalid substitutes
are explicitly refused.

The only external direct JAL targets in the four owners are shared EXE
`80053570` (particle allocation), `8006272C` (random source) and `80017BFC`
(vector helper). They bind the complete captured executable; there is no JALR in
these owners. This is bounded source/effect integration evidence, not a universal
transitive proof of arbitrary global aliases or runtime allocator capacity.

## One caller-owned final overlay

Internal `LinkInto(arena, runtimeBase, bridges)` requires one canonical Final
LevelData domain, an append arena at or after original `0xB000`, and
`runtimeBase = 0x8007AA38 + arena.Offset`. It never creates a competing Final
domain. The caller supplies exactly four distinct eight-byte bridge owners,
keyed by the certified original Dark continuation PCs above. Their actual
appended PCs derive from the arena mapping, not caller-selected integers.

Each bridge must be aligned, disjoint, inside the same arena and outside the
actual generated-code prefix. `CopyBridgeBytes` returns the code-owned J/NOP
bytes for that key. The shared native linker checks signed branch displacement,
jump encoding, exact EXE reads and original fragment preimages. The result still
reports bridge reservations as unmaterialized. The overlay installer must install
and verify those bytes plus the four hooks; a reservation is not executable code.

## Tests and remaining acceptance

RED was executed after a clean Debug build, failing at the unimplemented typed
operand rewriter. Initial GREEN built in 5.40 seconds with zero warnings/errors:
162 aggregate source-free checks / 64 refusals and 490 aggregate source-backed
checks / 85 refusals. The source gate compares all 224 emitted instructions or
independently decoded branch/jump targets, all four source hooks, the full operand
ledger, complete original overlay complement and bridge semantics. Missing,
duplicate, overlapping, mis-mapped or detached bridges fail closed. Every prior
default/particle resource composition golden remains unchanged.

Logs `09-particle-behavior-red-build.log` through
`13-particle-behavior-source.log` are retained under
`_local/research/native-resource-composition-final-20260906.t1Krlk/`.

This source issuer leaves `BehaviorInstalled`, `CompleteNativeClosureProven` and
`PublicationApproved` false. Active overlay installation, whole-image protection,
RAM/stack/transient fit, mover/body/dry placement and actual native effect/audio
gameplay remain separate owners and acceptance gates. No candidate or normal
export path is added here.
