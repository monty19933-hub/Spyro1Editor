# Fresh P1 observer: report-only health and motion

September 7, 2026. The fixed-candidate observer now reports seven additional
fixed owners: health at80078BBC, damage accumulator at80078A84, three raw words
each at80078B4C and80078B64, and raw words at80078BA4/80078AF4/80078B08.
The44-byte group is read again before accepting a payload. It introduces no
value thresholds, pointer following, motion units, causal diagnosis or approval.
State6 and one cleared damage accumulator cannot exclude an earlier hit.

Tests-first RED covered missing fields, exact widths, partial reads and reread
drift. Independent original-source operands include signed health/state loads,
actual vector-copy arguments, all three copy stores including the return delay
slot, and the -192 scalar store. Complete original image/EXE and helper hashes,
true EOF and held/path identity are checked before and finally.

Root replay: whole Python174 tests/eight explicit-mode skips; the explicit
original-image, actual six-file and recorded-OS suite21 tests/no skips. The
separate regression-runner suite passed58 tests. Root and peer fully reviewed
the changed source/tests; old helper hashes, candidate guards, socket protocol
and false runtime/UI/export approval flags remain unchanged. No debugger
execution commands or memory writes were added.

Health-only checkpoint source SHA256:
`49db5b17e1ae8c2e6053131c4626be29eca2e412d5103855f60cfd02ba6c5e13`.
RED/GREEN/source receipts and Root replays:
`_local/research/dark-wizard-native-closure-20260906/fresh-p1-health-motion-r01.WhHw44/`.

No live health/motion result follows from these tests. The previous failed-entry
session is closed/restored. A later controlled run must bind a fresh actual
process and keep native connection/disconnection pause effects explicit.
Native Frame Step is a separately source-reviewed UI option, not yet exercised;
it does not authorize debugger stepping or assume one frame equals one game tick.

## Later exact HTTP-cache classification

After the closed R02 failure, the observer gained a second independent auxiliary
cache identity. Only the exact `cache/http_cache.bin` path, REG/u,
device0x1000011/inode691426 and valid nonnegative size are recognized. The
existing shader identity is unchanged. Both caches' presence, descriptor,
device, inode and path remain fixed across a session; sizes are descriptive and
contents unauthenticated. Every other extra image and invalid owner still refuses.
Opening a cache midway through an established session still refuses: prepare
settings before connecting, then establish a fresh actual binding. No auto-retry.

The [pinned native settings constructor](https://github.com/stenzek/duckstation/blob/fe2306b1f/src/duckstation-qt/advancedsettingswidget.cpp#L121)
checks cache size, and the [native HTTP cache](https://github.com/stenzek/duckstation/blob/fe2306b1f/src/util/http_cache.cpp#L85)
opens its object archive lazily. This supports the observed file classification,
not authentication of its contents or the exact process call history.

Tests-first reproduced the actual2088-byte recorded-file refusal before the
patch. Neighbor/duplicate/extra-image and independent session-drift tests pass;
postcheck drift discards the buffered payload and still accounts for all six
held owners. Root whole Python179/nine explicit skips and original+actual-file+
recorded-OS26/no skips pass. Root and independent peer fully reviewed the delta.
Current source SHA256:
`5e16c941f3deefce1625b09b89b35af4aaee3b3b008fbd33d56f1aaa0a75f870`.
Exact evidence: `fresh-p1-http-cache-owner-r01.tBfaL7/` under the same research
parent. No new live acceptance follows from these source/parser checks.
