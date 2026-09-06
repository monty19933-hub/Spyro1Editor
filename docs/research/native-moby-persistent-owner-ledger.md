# Persistent native Moby owner ledger (identity-only)

2026-09-06. This additive component fixes temporal allocation bookkeeping in its
own API. It is not wired into the App/project transaction or any existing native
scene/exporter. It emits no native bytes and changes no old candidate/golden.

## Why ordinals must remain stable

The original EXE's durable collection key is `(levelSaveSlot, placedRowOrdinal)`:

```
word = 80077908 + slot*32 + (ordinal>>5)*4
mask = 1 << (ordinal&31)
```

`8003B854` uses placed actor address/stride58 hex, or a dynamic actor's byte+56.
Live deletion is a separate32-byte owner at`800778B8`; checkpoint capture copies
it to`80077898`, reconstruction copies it back. Save/restore copies the durable
36x32 bytes to/from native save record+10C. There is no editor UUID in this key.

The limit applies to all placed rows, not only reward-flagged templates: native
loader`800142C8` reads the bitmap using each row ordinal. Exact scalar execution
shows ordinal255 uses live`800778D4`/Dark saved`80077964`, but256 writes
live`800778D8` (outside32 bytes) and Dark saved`80077968` (Town's first word).
Dark spawn`8008298C..829F0` emits parent ordinal<256 into child+56; its fallback
for256/null is owner0, not independent identity capacity.

An immutable existing `NativeMobyDependencyInstantiationPlan.Allocate` preserves
prior in-memory reservations. However, the current scene compiler constructs a
fresh plan each compile and sorts a new batch by family ID. `z` at128 can become
129 when a new earlier`a` joins that batch. Legacy added-Moby export also assigns
append indexes in manifest enumeration order; deleting an added row/undoing it
can change that order despite retained editor TrueIndex. Those old APIs are not
modified here. The new ledger must be consumed by a later real scene producer.

## API and authority

`NativeMobyPersistentOwnerSource.Capture(originalBin, catalog, targetLevelId)`
reuses the exact all35-level native inventory and existing retail slot ownership
source gate. All catalog table locators/counts are checked against actual native
headers/scenes. The source image is bound in full; a second complete source gate
surrounds exact EXE/owner-consumer capture. Only the constructor-closed source
object can issue a source-certified ledger. Original rows are immutable seed
owners, identified by their complete concatenated58-byte-row hash and exact
source/level/save identity. ID65 is not accepted as a new independent slot.

`BindFamily` accepts stable role IDs and donor level/row selections. It resolves
the real source class and complete source variant identity through the captured
inventory. These selections group identities; they do not prove a whole native
behavior family, owner-pointer relationships or simultaneous activation.
Private properties, mutable globals, resources and actual closure checks remain
separate and must still pass in the future materializer.

An immutable ledger supports:

- `Append`: previously accepted family/role ordinals survive reorder/reopen.
  Only new families/roles are canonically sorted at initial allocation. Every
  role occupies the monotonic suffix, and total high-water cannot exceed256.
- `Retire`: reserves all ordinals of a family permanently, including the tail.
  `Reactivate` is explicit and accepts only the same source-bound role set.
- `Serialize` / source `Restore`: bounded version1 UTF8 JSON with exact source,
  native slot, original rows, high-water and entries. Reopen rebinds all donor
  variants, rejects unknown/duplicate properties, depth/length/identity overflow,
  gaps, aliases and partial-family retirement. No canonical physical domains or
  delegates are persisted. Input/display order does not reassign prior owners.
- Complete batches validate before returning a new ledger. A late source,
  duplicate, role or capacity failure cannot consume earlier reservations.

Retired rows are NOT represented by an invented native tombstone. `FF` in the
middle terminates later rows; `FD` does not skip the earlier native model init
loop. `RequireNoUnmaterializedRetirements` refuses until a genuine source-owned
retired-row/materializer policy exists. Passing it is not native emission
approval. Native emission, runtime, complete dependency-family, private owner
relationship and App/project transaction flags all remain false.

This is not cryptographically authenticated history. A self-consistent external
document rewrite, older project snapshot or pre-ledger manifest cannot establish
which mapping was previously exported or paired with a memory card. The actual
project transaction and accepted-export history/compatibility policy remain
integration work. A JSON epoch cannot stop retail saves from being reused; no
save clearing, migration or game-file mutation is performed.

## Regression evidence

New isolated project: `src/Spyro.Editor.NativeMobyPersistentOwnerSmoke/`.
Registered in `tools/Run-SpyroEditorRegression.py`: the default source-free mode
runs without arguments; opting into `--with-source-qa` adds only
`--source <selected-original.bin> <workspace-root>`. The current workspace is
required for authoritative catalog cross-checks. No CUE/publication/emulator
mode is registered, and the shared source-QA deadline still applies.

- Initial executed compile RED: 22 absent new-API errors, zero warnings,5.24s;
  existing Core compiled cleanly. This was compile RED, not a runtime assertion.
- Initial Debug GREEN:5.49s, zero warnings/errors.
- Independent root Release verification:4.98s, zero warnings/errors; the same
  67 source-free/88 exact-source checks and49/63 refusals passed.
- Source-free GREEN:67 checks/49 refusals. First test is z128, serialize/reopen,
  append earlier a129 without moving z. Other cases cover retirement/refusal,
  role/source changes, all-role256 bound, immutable retries, finite max+1
  ingestion and hostile bounded JSON.
- Exact-source GREEN:88 total checks/63 refusals, including two source captures,
  full independent inventory variant equality and forged/stale catalog metadata.
- Independent raw/scalar oracle GREEN:61 checks, full original image hash before
  and after. Interpreter micro-controls and actual native load/branch/JAL delays;
  real word-copy/fill, owner arithmetic, Dark fallback, checkpoint/save restore
  and collected7D selection. Native audio/unlink/checksum are stopped before
  execution. Death cleanup/CD helpers are not synthesized. The legacy ordering
  example is a source-aligned scalar model, not an executed App/exporter test.
- Runner registration tests-first:24 Python tests,2 expected missing-mode RED
  failures, then24 GREEN. The focused registration test mocks all .NET commands,
  verifies default source isolation, exact opt-in image/workspace arguments,
  one project build across both modes, distinct logs and no publication flags.
  All previous runner tests remain present.

Logs/JSON: `_local/research/native-moby-owner-ledger-20260906/01-isolated-red-build.log`,
`03-initial-green-build.log`, `04-source-free.log`, `05-source.log`, and
`02-raw-scalar-oracle.json`; runner registration logs are`06-runner-red.log` and
`07-runner-green.log`. The independent root Release result was reported by the
root gate owner in this task. The tracked oracle is
`src/Spyro.Editor.NativeMobyPersistentOwnerSmoke/source_owner_lifecycle_oracle.py`.

Original SHA256:
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
EXE SHA256:
`a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`.
Exact per-window PCs/lengths/hashes and declared scalar stops are retained in the
oracle JSON. These checks are not native memory-card/gameplay acceptance.
