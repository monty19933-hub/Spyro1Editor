# Fixed-a textured research candidate

September 8, 2026. Implemented and source-tested; no physical output yet.

A new sealed candidate binds the actual textured Disc → Body → texture plan →
fresh scene → ordered source materials. Its only admitted recipe is the existing
fixed-a floor, two static records (Dark Hollow WAD14 texture0; Gnasty WAD70
texture0), P0/N16 and Track19. Equal selected-material bytes from another profile
are not silently admitted. This is not arbitrary textured level export.

The new typed publisher uses the shared streaming writer and existing CUE-last
transaction. It separately accounts for operation errors, held/current original
BIN/CUE checks and acquired-handle close errors. Actual committed-but-unverified
results retain typed evidence. Cancellation does not suppress final accounting.
These are trusted-path/cooperating-writer guarantees, not hostile inode/race
authentication. No material-document binding or runtime approval is claimed.

Tests first failed on the missing public API. Final default55 and actual source325
pass; source tests preserve both originals and write no physical image. Concrete
partial acquisition and malformed-source tests exercise actual acquired handles,
not merely simulated error lists. Full virtual readback, changed/unchanged sector
refusals and all five historical recipe/role goldens pass. Root independently
replayed30 exact inverse checks, recovering the old complete Binding/Engine and
15 unchanged old/shared files. Root and independent peer review are clear.

Independent original-only full-image expectation (predates this candidate):
`998a47930c5f20e36cf87d41bfb9ec3cab237d8d42c443ed706bd50c692611a3`,
661547040 bytes,1253 changed raw sectors. Expected output stem:
`native-id65-fresh-textured-fixed-a-track19-r01`.

Retained commands, source-owner tests and prospective companion identities:
`_local/research/textured-candidate-step4-r01.7ADCKD/HANDOFF.md`.
Combined all-project Release/default325/zero failures, closed exit0:
`/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-kcn758vr/summary.json`.

Next: opt-in physical publication/fault checks, independent new-process readback,
then DuckStation visual/playtesting. The editor has no new aggregate publication
button enabled, and preserved app packages have not been replaced.
