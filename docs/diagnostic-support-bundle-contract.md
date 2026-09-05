# Support bundles and local diagnostics

This development checkout separates useful local debug data from shareable
support metadata. A JSON/TXT/LOG extension does not establish that a file is
metadata-only: native patch plans contain full before/after bytes encoded as
hex, and exceptions can include native byte excerpts.

The previous production bundle path copied those files verbatim and labeled
the ZIP as excluding game data. An isolated synthetic test using invented
bytes reproduced this through `RecordExport` and `CreateSupportBundle`; no
game payload or upload was needed. Previously generated or packaged bundles
retain their old contents and must be reviewed before sharing.

## Current output contract

The ZIP contains exactly two fixed entries: `bundle-manifest.txt` and
`support-inventory.json`. The inventory uses schema version 1 and policy
`inventory-only`. It includes fixed event severity counts and opaque artifact
IDs with role, closed file classification, status, byte length and SHA-256 when
available. It does not include file bodies, filenames, paths, action messages,
context values, tester notes, exception text or arbitrary version strings.

Reports, session/crash logs and supplied export artifacts all use the same
inventory-only boundary. Unknown schemas, malformed JSON and payloads renamed
as text or logs cannot bypass it. Entry count, per-file size and total hashing
work are bounded, and omissions or unavailable hashes remain explicit. A hash
matches a local file; it is not evidence of gameplay correctness or a complete
export dependency proof.

Detailed reports and plans remain on disk for local investigation. The
Diagnostics window's copy actions still copy raw diagnostic text and require
review before sharing. The bundle writer itself does not rewrite those files;
the UI or export workflow can generate a fresh local report before bundling.
ZIP creation refuses accidental overlap with known input files, stages a new
archive before replacement, and preserves an earlier ZIP when creation fails.
This is an accidental-overwrite guard, not protection against adversarial
filesystem races.

## Current-build provenance

Normal Create BIN supplies its unique run's reports to the inventory. Its
regression matches the exact current Build Safety snapshot by SHA-256 and byte
length and rejects the old reusable snapshot's hash. It does not substitute a
content marker or an old report for build-bound evidence.

The source-free bundle regression exercises full synthetic native patch bytes,
hex/base64 and nested payloads, renamed files, raw exceptions and supplied
report paths. No game image, emulator or external service is required.
