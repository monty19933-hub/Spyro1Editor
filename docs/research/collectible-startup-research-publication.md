# Bootstrap/reservation-only startup research disc

This explicit research path discovers whether full-firmware loading tolerates
the existing ephemeral bootstrap. It does **not** grant native installation,
Ready state, ledger lifetime, whole-engine fit, collector, card save or normal
Create BIN authority. The previous bootstrap's `RequireNativeInstallation`
continues to refuse. No existing app, candidate or original image is edited.

`NativeCollectibleStartupResearchPlan.Capture` accepts only the exact original
USA BIN and a source-bound single-track MODE2/2352 CUE. Its private construction
uses the existing source-bound residence → A00 reservation → cold-boot bootstrap
ancestry. Two fixed identities size the existing descriptor; no identities,
manifest, Ready metadata, collectible hook, WAD, model or overlay are emitted.

The three logical edits occupy **two**, not three, raw sectors:

| EXE span | Change | Raw LBA |
| --- | --- | --- |
| `+0x10..14` | Entry `8005B8E0 → 80075640` | `53875` |
| `+0x65DA4..65DA8` | Reserve `800 → A00` | `54078` |
| `+0x65E40..65E6C` | Existing fixed 11-word bootstrap | `54078` |

The EXE remains LBA53875/0x66000 bytes. ISO directories, extent addresses,
WAD/model/overlay content, XA headers, and every other sector are unchanged.
The existing raw-sector codec rebuilds EDC/ECC for those two sectors only.
The bootstrap zeroes `[801FF600,801FF800)` to NOT-LOADED, then jumps to the
unchanged startup that erases the ephemeral stub. The original-reserve loader
overlap counterexample makes the A00 pairing mandatory.

Expected complete EXE SHA256:
`a6da6e2bc54ea9eaac5e53987853eb96057beaa00cbdf57cbe2f040d26819a6b`

Expected complete BIN SHA256:
`895074e78abcf7de47faa15b62ee340239d996a486d1bc7ef5897c68cd4a06d3`

## Publication and verification boundary

The writer copies the authenticated image to a CreateNew staged file and uses
the existing logical writer for the three spans. A separate expected-sector
path derives complete raw-sector afterimages from the original/full final EXE,
then the existing physical readback compares **every byte of every sector**.
Readback also extracts the full EXE and inverts to the exact original EXE.
Receipts and CUEs require bounded exact bytes, length and actual EOF.

Only the existing `ExportArtifactTransaction.PublishNew` moves the three
artifacts into place, with CUE last. Occupied directories, source-tree overlap,
and symlinks in output paths or ancestors are refused. Source BIN identity and
exact CUE bytes/binding are checked before, during and after admitted work,
including failure paths. Fresh source checks do not claim hostile filesystem
race safety. Cancellation is respected before the short transaction, not as
permission to roll back a transaction that already committed.

Every failed stage and its lease remain for explicit inspection. Actual
transaction state is reported as NotInvoked, Unconfirmed, RollbackIncomplete or
Committed; later checks cannot turn a committed operation into an aborted one.
There is no automatic recovery or retry. Success removes only its empty stage
and exact owned lease. This is exception rollback, not crash atomicity.

## Focused command line

Run from the repository root; source paths below are read-only inputs:

```sh
dotnet run --project src/Spyro.Editor.NativeCollectibleStartupResearchSmoke -c Release

dotnet run --project src/Spyro.Editor.NativeCollectibleStartupResearchSmoke -c Release -- \
  --source '/Users/john/Documents/Codex/2026-04-24/could-you-create-a-level-editor/Spyro the Dragon (USA).bin' \
  '/Users/john/Documents/Codex/2026-04-24/could-you-create-a-level-editor/Spyro the Dragon (USA).cue'
```

No arguments performs only source-free in-memory tests. `--source BIN CUE`
performs read-only source, inverse, independent full EXE recipe, every expected
raw sector and complete expected BIN hash checks; it writes no candidate.

Append exactly one explicit mode to that source command:

- `--publish <new-empty-dir>` creates the research BIN/receipt/CUE.
- `--verify <dir>` reads an existing complete set without writing.
- `--failure-tests <new-empty-test-dir>` writes isolated retained test fixtures,
  forced failed stages and one successful temporary selftest publication. It
  mutates only its own source copy, never the selected original. This mode is
  intentionally excluded from source-only/default regression.

Output names are `collectible-startup-bootstrap-reservation-r01.bin`,
`collectible-startup-bootstrap-reservation-r01.cue` and
`collectible-startup-bootstrap-reservation.receipt.json`. On macOS use canonical
`/private/tmp/...` for temporary outputs; `/tmp` and `/var` aliases are refused.

## Focused verification recorded 2026-09-27

Test-first build refused the missing plan/publisher types (11 errors). Release
then built with zero warnings/errors. Default source-free tests passed 6 checks;
read-only source tests passed 35 checks plus independently derived comparisons
of every raw sector and the complete expected image hash. The opt-in I/O suite
passed 71 checks: occupied/link/source-overlap/cancellation refusals, first-sector
partial evidence, corruption of an untouched raw sector, extended receipt/CUE,
actual unchanged PublishNew lock collision, one complete successful temporary
publication/verification, and exact-source freshness refusal after mutations to
its dedicated CUE/BIN copy. Original source authentication still passed.

Retained evidence root:
`/private/tmp/collectible-startup-publication-selftest-bvktXX`

Its `successful-test-publication` child holds the fully verified temporary
BIN/CUE/receipt; the other children preserve failed stages/leases, collision
fixtures and the deliberately mutated source copy. An earlier harness run
stopped when the existing path-separation helper correctly threw
`InvalidOperationException`, which its initial test catcher had not accepted.
That small evidence root also remains:
`/private/tmp/collectible-startup-publication-selftest-YdJxXR`.
No original source was mutated in either run. This focused implementation task
did not launch an emulator or modify an app/settings/card. Root subsequently
published and independently verified a designated candidate, then performed
[a separate isolated runtime check](collectible-startup-runtime-r01.md).

## Remaining external evidence

Physical publication alone does not establish real BIOS load extent, instruction
cache visibility, BIOS handoff-buffer or IRQ ownership, startup execution,
transition lifetime or all-engine memory fit. Zero RAM observed later is not
proof that the clearing instructions executed. Full-firmware runtime observation
and subsequent installation/save decisions are separate work. No emulator or
designated runtime candidate is created by the source-check command.
