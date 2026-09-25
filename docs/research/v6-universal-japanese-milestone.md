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
