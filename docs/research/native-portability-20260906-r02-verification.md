# Native portability continuation: Release checkpoint and test image

2026-09-06. This report certifies source checkpoint `15ac960`, not later
in-progress changes, universal importing, a finished blank editor or gameplay.
The installed editor and original ROM were not replaced.

## Full Release gate

`python3 tools/Run-SpyroEditorRegression.py --build-all --with-source-qa
--configuration Release` completed **254 steps with zero failures**:
206 successful project builds (zero warnings/errors), 37 source-free modes,
the 21-test Python runner suite, offline editor QA and nine source-backed modes.
Offline QA exercised both native catalogue surfaces, exact-variant selection,
approved retail Add handoff and stale scene/source/navigation/file guards.

Durable raw logs and command-level results are under
`_local/overnight-native-20260906-r02.jPIcTb/spyro-regression-8064iyne/`.
The copied `summary.json` SHA-256 is
`46fd98c5cb8422dcb277aeda5a0f11c3e2300e773bae12ae7301962897efd25d`.
The top-level log is `full-release-gate.log` in the same evidence root.

Source-backed dependency closure passed 722 checks; player-animation merge
passed 642 checks with 49 deliberate refusals; sound passed 2,702 checks with
41 deliberate refusals. A separate Release reopen of the retained Town Square
V3 candidate passed 725 checks and preserved its original BIN hash
`15df0ee1edba6cb48fe1d34df5a5f51a7d6f289de387c499ca0ff04e99b443bf`.
This readback adds no new gameplay claim to the earlier key/chest recordings.

## Isolated Free Flight research publication

After the full gate, the exact Toasty research producer published a new-only
BIN/CUE/receipt set under
`_local/research/native-free-flight-research-20260906-r01/candidate-r01/`.
Publication verified the complete physical image and published the CUE last.
A separate process independently reopened the published candidate and passed
56 checks. Original source BIN/CUE hashes were protected before and after.

| Artifact | SHA-256 |
| --- | --- |
| `toasty-native-free-flight-r01.bin` | `c0b30e5354b1050d59a79593f54fc9e014c12819213bd719fc0ab5d42493d87d` |
| `toasty-native-free-flight-r01.cue` | `fde816d13f97689903fcf8cace561ab32ee083c87d662bad4743a5716c1c8454` |
| `native-free-flight.receipt.json` | `7dd5a7ff6fbc1c4f4b28308fad3a2fd14cdb265bee320efa17db9635e3e2ab4d` |

The exact source-path-bound plan is
`6f95efb443dc80ce9cc3c9e64fe2ae437f5dcbc28802b73cc69f02110b530337`.
The normal-load reserved-memory gap is `0xA780`; this is not a peak-runtime
certificate. `RuntimeVerified` and `NormalCreateBinApproved` remain false.
Publication and independent readback logs are `free-flight-publication.log`
and `free-flight-independent-reopen.log` under the evidence root above.

## Native test boundary

The candidate was cold-booted from its exact CUE in DuckStation. The actual
process open-file list bound the matching candidate BIN; no old emulator state
was loaded. Temporary test settings disable the previously enabled Moon Jump
cheat, isolate a copied memory card, screenshots/videos/save states and disable
achievement activity. Exact original global/per-game settings were backed up
before changes; restoration is required after the session. The user's original
memory card remains outside test writes.

At this checkpoint native gameplay testing is in progress, not passed. A fixed-
address, read-only loopback observer can supplement visible play, but its
sequential samples cannot certify gameplay independently. Do not promote this
research image to ordinary editor export based on build or image readback alone.
