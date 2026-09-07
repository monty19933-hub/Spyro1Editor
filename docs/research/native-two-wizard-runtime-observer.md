# Exact two-Wizard read-only observer

`tools/Observe-SpyroNativeTwoWizards.py` is a separate opt-in observer for the
unchanged `dark-hollow-native-two-wizards-r01` candidate. It does not modify the
one-Wizard observer, its90-row layout, or either fresh-ID65 observer profile.
No two-Wizard gameplay acceptance follows from these tests.

## Read and interpretation boundaries

Independently verify the full BIN/CUE/receipt and DuckStation's actual PID/open
BIN plus loopback-only listener before connecting. Hash annotations and equal
RAM values cannot authenticate the loaded image. Only bounded main-RAM `m`
reads and protocol ACKs are sent; controls, pause and resume remain UI-only.
Sample while paused: even legitimate animation can change a complete row and
cause the conservative consistency check to refuse it.

Fourteen fixed loader/layout gates precede all heap reads. Both placed rows128
and129 must be class11B with distinct relocated private owners801410B8 and
80141108, and each nested pointer must target its own private+28. Dynamic rows
start at8013DACC/index130 and stop after at most89 rows. Properties use the same
shared arena downward;89 rows/properties leave96 bytes, while a90th allocation
would overlap. This is a bound, not a safe peak-occupancy certificate.

StatusFF ends the row scan; other signed-negative states are holes, not actors.
Offset56 is exposed as a raw byte for all classes, and interpreted as a native
reward parent only for the source-proven class56 consumer. Live/checkpoint/
durable words800778C8/800778A8/80077958 are separate observations; bits0 and1
identify the two owners. No collected-state or reward verdict is synthesized.

The256-record particle pool uses eight bounded1024-byte reads. Its separate
PETE terminal must beFFFFFFFF. After payload reads, loader/cursor/bitmap fields
and both complete placed/private witnesses are reread. A mismatch refuses the
payload. Matching rereads are still sequential, not an atomic CPU/pool snapshot.
All runtime-pass and candidate-authentication flags remain false.

## Verification and evidence

Evidence: `_local/research/two-wizard-observer-r01.CWNDlq/`.
Tests preceded implementation:01 records the missing-observer RED,02 its first
eight-case GREEN. Expanded tests check CLI opt-in, protocol bounds, reclaimed
holes, parent-byte width and changed bitmap rejection. Peer review reproduced
two misleading diagnostics: assigning parent meaning to class26's raw byte,
and accepting a changed PETE terminal.05 retains those actual RED controls;
06 passes all14 corrected tests. A second peer independently repeated them.

07 passes the explicit original/candidate source-binding test. It verifies full
original and candidate SHA/length/actual EOF before and after reads; exact
companions, donor row/private complements, relocated copy/model/scene roots,
native data used-end, both distinct private owners,89-slot accounting and
PETE-derived pool boundaries. This is raw readback/arithmetic, not execution.
08 repeats the unchanged source-only native owner lifecycle oracle:1,148 checks.
Its bounded instruction execution supplies separate owner-bit/byte evidence,
not runtime collection or actual memory-card IO.09 passes the complete Python
suite:101 tests, four explicit-source skips. No socket or emulator was opened.

The exact candidate BIN is SHA256
`612e68e1a8642d42f2417fd6153368c9549094ac391a3835c9cf6dfee7808cba`.
The source-binding test requires both explicit `--source` and `--candidate-dir`;
default discovery skips file-dependent evidence instead of claiming it passed.
