# V6 universal Mobys and Japanese-content milestone

September 25, 2026. User-confirmed continuation from source `1d43218` and
preserved R08/beta45. The complete attached milestone remains in scope. Japanese
content import is limited to the requested dragonfly eggs and editable signs;
this is not permission to replace the game with its Japanese version.

## Execution order and acceptance

1. Authenticate the supplied Japanese revision read-only. Keep regional source
   identity, directory ownership and exact byte evidence separate from USA.
2. Identify egg/sign models, rendering/material variants, handlers, message
   codec/font, private data, and transitive dependencies. Establish regression
   fixtures before implementing mutable native behavior.
3. Build the deterministic all-known-Moby matrix from existing inventories and
   dependency evidence. Distinguish discovery, narrow tested profiles and true
   arbitrary-destination portability; do not equate a resident root with closure.
4. Implement native-backed sign authoring and independent egg appearance and
   collectible identity, with Save/Reopen/Undo/Redo and bounded serialization.
   Establish actual native save/inventory/percentage consumers before changing
   accounting. Ordinary retail eggs and player saves remain protected.
5. Compose dependency/texture/text/runtime ownership through existing relocation
   and guarded publication. Independently verify output/complement before a
   cold-boot DuckStation test of visuals, messages, pickup, inventory, percentage,
   reentry and memory-card save/reload.
6. Generalize proven packages across representative missing-content destinations,
   then broader matrix coverage, adversarial composition and final regression.
   Preserve exact evidence scope; unknown dependencies are not engine limitations.
7. Only package a new private editor after coherent native-backed integration.
   R06/R07/R08 apps, their isolated state, original ROMs and runtime evidence are
   not overwritten or silently upgraded.

The stop conditions in the supplied milestone are not yet met. Intermediate
checkpoints document progress, not completed universal portability. The standing
request to stop safely near 5% remaining account usage still applies.

## Current source authentication

Supplied original:
`/Users/john/Desktop/Spyro the Dragon (Japan)/Spyro the Dragon (Japan).bin`

- BIN length727,179,600, SHA256
  `1bcf4353d6a4d4d0db152d2ebd60879ef85516feaf170f364ff419c54f599ea7`.
- CUE length90, SHA256
  `0faaba827d5a480062dca5eb0a74d31b3079ef9a52071ba3c22acb028fea02ba`.
- MODE2/2352, logical payload2048 at raw offset24; ISO rootLBA22.
- `SCPS_100.83`, LBA24, length0x70000, SHA256
  `a67c21f441deb3a4df26099d78ddaa29cc66b927a7387718a678eee71664cee4`.
- `WAD.WAD`, LBA500, length112,793,600. Directory SHA256
  `ae42b988d2f57b99713e0d2b8a406415c445045864c69a28c4b2cca38fc0cbfc`.
- Artisans overlay/data entries22/23, not USA entries9/10. Japanese unused
  entries8..19 have zero length but retain a nonzero next-allocation offset.
- Shared PETE entry21, not USA entry8. Its explicit runtime-copy length0x2F734
  extends past the declared directory allocation when its0x800 header is added;
  an entry boundary is not a native-copy or individual-model boundary.

`NativeRegionalSourceSession` reuses the existing ISO reader, verifies both known
revision identities, bounds logical reads to explicit file owners, retains all
256 directory slots, refuses empty-slot extraction and cross-region substitution,
and checks both held and named original images at completion. It is read-only
and grants no placement/publication authority. Existing USA capabilities have not
been broadened or redirected through this research session.

Focused checks: source-free28, authenticated Japan16 and USA13, all pass.
The source-free case is registered in the normal regression runner; actual disc
checks remain explicit opt-in and do not launch an emulator or write a candidate.

New local evidence root:
`_local/research/japanese-content-20260925-r01.RCmBCn/`

- `regional-build.log`: build, zero warnings/errors.
- `regional-japan-source.log`, `regional-usa-source.log`: complete directory
  metadata, shared native-copy hashes and held/named original final checks.

No new native egg/sign publication, GUI placement, save integration or in-game
result is claimed by this initial source-foundation checkpoint.

## Initial matrix checkpoint

The [implemented matrix](universal-moby-matrix.md) resolves the exact USA class
union to368, retaining6,334 source observations and12,880 destination cells.
Independent JSON readback checks every variant/class reference and verifies
that the exact LAB65/R08 record is not misrepresented as a retail destination
approval. The inventory smoke passes120 source-free and57 actual-source checks;
the regression-runner suite passes90 tests after registering the regional case
and requiring the new matrix markers. Broader regression is reserved for the
coherent feature checkpoint rather than repeated for every source-only edit.

`checkpoint-baseline-preservation.log` independently matches the previously
recorded R08 App/Core DLLs, ordinary DuckStation INIs and USA memory-card hashes.
No emulator, installed app, old preview project or candidate was opened or
changed during this foundation work.

## Native-content checkpoint after f039f7b

The full milestone remains active. Added dragonfly eggs are user-confirmed
**required for100%**, not bonus percentage. Only the requested Japanese egg/sign
content is imported; the Japanese disc remains an authenticated read-only donor.

Implemented and tested:

- Native sign byte/Unicode codecs, native font glyph/encoding recovery,
  independent per-sign bounded edit plans, explicit complete-dialog termination,
  no unsupported-character substitution or silent truncation. Native kana,
  digits and punctuation are supported; unsupported Latin/kanji/emoji are refused.
  See [encoding](japanese-font-encoding.md).
- Original Japanese reference gameplay and two independently edited messages
  now verified in DuckStation, including native accents, two colored lines and
  repeat reading. The failing unterminated R01 remains preserved; corrected
  R02 is independently physically verified and tested. See
  [runtime evidence](japanese-reference-runtime.md).
- Exact native F5 animated geometry and native appearance-profile selection;
  detached source-backed model composition in USA Artisans and Town Square,
  through existing tail/relocation/dependency compilers. No pre-rebasing of
  source-relative disk pointers. See [model assembly](japanese-egg-model-assembly.md).
- USA shared ordinary egg22 simple geometry decoded separately from animated
  F5; adjacent allocation lookahead remains separately owned, not an invented
  dependency. See [PETE model closure](native-shared-usa-egg-model-closure.md).
- Appended F5 visual behavior composed with typed USA helper bindings and an
  existing owned frame adapter; delay-aware R3000 tests execute64 ticks with
  exact native helpers. Original USA dispatch remains unchanged; no activation
  or rendered-color claim. See [visual behavior](japanese-egg-visual-behavior.md).
- Stable per-instance GUID ledger, permanent retirement, explicit migrations,
  three independent earned-state slot bitsets and checksummed detached save
  payloads. Invalid/legacy/mismatched bytes are retained, never silently reset.
  See [save contract](detached-collectible-save-contract.md). Native card/RAM/hooks
  are not yet installed.
- Exact-source font CPU relocation and a VRAM ownership/lifetime contract.
  USA WAD2 is not the Japanese font resource; unchanged atlas placement overlaps
  display/reset ownership. The right-half mirror needs a real lifetime/reservation,
  not a guessed blank range. See [font runtime](japanese-sign-runtime-portability.md)
  and [VRAM lifetime](japanese-sign-font-vram-lifetime.md).

Checkpoint validation (all detailed logs under the existing research root):

- Five coordinated Release builds: zero warnings/errors; source-free checks
  sign38,220, USA simple model105, F5 model assembly165, detached save14,207,
  F5 visual behavior513.
- Focused exact-source checks: Japanese sign capture/edit3,417;
  combined font CPU/VRAM623; USA simple model21; F5 model assembly183;
  F5 visual behavior510. These are source evidence, not native gameplay approval.
- Python runner/collectible/font111 tests, object inspection9, independent
  physical sign verifier12, all passing. Default registry includes new source-free
  projects; dual-regional cases remain explicit, never supplied USA-only arguments.
- Broad regression built and passed source-free/offline QA and initial source
  tests (222 passing commands), then exhausted its aggregate1,800-second source
  deadline. The interrupted command and24 remaining source commands passed on
  targeted continuation. Original failed report is retained at
  `/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-2crge4vj/summary.json`;
  continuation at `source-continuation-r01/summary.json`. This is not a claim that
  the original monolithic invocation passed. Timeout cleanup also reported EPERM;
  subsequent process inspection found no surviving test process.

The exact R08 App/Core DLL hashes and ordinary DuckStation global/per-game/card
hashes still match preserved baselines. All Japanese sessions were closed and
ordinary settings restored. Source/final candidate hashes are preserved. No new
editor package was built, and existing previews/candidates were not replaced.

Still open: complete USA actor dispatch/scene/RAM/render binding; two egg colors
in-game; pickup/inventory/required-for100%/real-save integration; safe font/modal
installation; GUI placement/editing/Undo/Redo/Save/Reopen; generalization across
destinations. The368-class,12,880-cell matrix remains honest about unproved
dependencies; these new isolated contracts do not promote all classes to portable.
