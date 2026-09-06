# Separate test-only Toasty fast-entry flight candidate

2026-09-06. This is an optional diagnostic v2 profile. The v1 flight candidate
and all ordinary editor exports remain unchanged. The copied normal save reached
Artisans and rescued Argus, but the Toasty entrance remained locked. Fast entry
allows direct flight testing without modifying save progress or runtime RAM.
It is **not** evidence of normal portal entry.

## Source-issued composition

`NativeFreeFlightResearchCandidatePlan.CaptureWithTestLevelSelectorAsync` uses
the existing `TestLevelWarpPatch.CreateGuarded` against the untouched exact-USA
source executable. That guard still requires its complete original hash, its
eight-byte preimage and its native safe-fallthrough instructions. The existing
diagnostic image patch API is not weakened to accept arbitrary modified EXEs.

The v2 composer requires the complete original executable again, applies the
same eleven Toasty-only flight windows, and then applies the independently owned
eight-byte inventory-handler change at EXE `0x1E034`, runtime `0x8002D834`:

```text
before: 37 B6 00 08 00 00 00 00
after:  1C 06 84 AF 88 06 80 AF
```

The second instruction clears the native homeworld selection for repeated warps.
Every other EXE byte remains identical to the v1 flight recipe. Full player,
model, scene, WAD and ISO allocation plans are unchanged. The writer resolves
the live relocated EXE owner at LBA53882; patch metadata's image offset describes
the original source evidence, not an instruction to write the old LBA.

In native Inventory, the selector is activated with
`R1, R2, L1, L2, R1, L1, R2, L2`. Select Toasty14 with `Cross, Right`.
This is the original game's selector handler, not an emulator cheat or RAM write.

## Publication and evidence boundaries

The separate profile is `native-toasty-free-flight-fast-entry-research-v2`.
Its immutable new output names are:

- `toasty-native-free-flight-fast-entry-r02.bin`
- `toasty-native-free-flight-fast-entry-r02.cue`
- `native-free-flight.receipt.json`

The receipt explicitly binds the selector, its source preimages and target, and
records `normalPortalEntryVerified=false`. Runtime and ordinary-export approval
remain false. Default v1 plan/receipt serialization returns exactly its prior
bytes; no new default fields silently change retained artifact identity.
Both profiles use the existing new-only transaction, exact complete-set reuse,
source freshness, whole-image physical oracle and rollback safeguards.

The smoke's default and three-argument source modes do not publish. Explicit
`--publish-fast-entry`, `--verify-fast-entry`, and
`--exercise-staging-fast-entry` select v2. Normal `--publish` still selects v1.

## Executed focused checks

Tests first failed with five missing-API errors, zero warnings; Core remained
compilable. Implemented Debug built in5.01s with zero warnings/errors. Source-free
49 checks passed. The source-backed composition suite added24 checks, including
the exact eight-byte v1/v2 difference, immutable original input, already-patched
and stale-source refusals, unchanged resource entries, and separate receipt
authority. Retained v1 R01 whole-image/CUE/receipt verification passed80 checks:
plan `6f95efb443dc80ce9cc3c9e64fe2ae437f5dcbc28802b73cc69f02110b530337`.

V2 all-stage fault/rollback, companion corruption, unrelated raw-sector
corruption, late private CUE mutation, occupied/partial/link/directory/source
overlap refusals passed99 checks, with no candidate published by staging.
Its absolute-original-path plan is
`f8d268758f10d199cdf1210c2a3c38f2984eb8a86c4879ee95350f1709151a02`.
Raw focused logs are under `_local/overnight-native-20260906-r02.jPIcTb/`.
Release publication and gameplay are separate subsequent checks, not inferred
from this source/staging checkpoint.

Independent read-only review found no material issue. Three suggested boundary
cases were added: explicit false normal-portal authority, exact v2 CUE target,
and both directions of occupied cross-profile refusal with fixture preservation.
Final Release built in1.46s, zero warnings/errors; source-free49,
composition32, and retained R01 physical/CUE/receipt verification88 checks passed.
The default regression runner also now registers the source-free and read-only
source modes of the companion asset and combined-resource smoke projects;
its registration negatives failed first, then all23 Python runner tests passed.
