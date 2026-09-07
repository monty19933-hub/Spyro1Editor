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

Frozen source SHA256:
`49db5b17e1ae8c2e6053131c4626be29eca2e412d5103855f60cfd02ba6c5e13`.
RED/GREEN/source receipts and Root replays:
`_local/research/dark-wizard-native-closure-20260906/fresh-p1-health-motion-r01.WhHw44/`.

No live health/motion result follows from these tests. The previous failed-entry
session is closed/restored. A later controlled run must bind a fresh actual
process and keep native connection/disconnection pause effects explicit.
Native Frame Step is a separately source-reviewed UI option, not yet exercised;
it does not authorize debugger stepping or assume one frame equals one game tick.
