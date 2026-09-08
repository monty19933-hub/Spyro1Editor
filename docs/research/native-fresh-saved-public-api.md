# Same saved-level publisher available to the App

The existing saved Candidate and normal Publish/Verify methods now have a narrow
public boundary. CaptureSavedAsync receives the actual saved Store Snapshot and
checks pre-cancellation before context or source acquisition. Constructors,
compiled ancestry, serialization and fault seams stay private/internal. There is
no App friendship or second publisher. Existing Engine, Binding and SourceOwner
bodies remain byte-identical.

A new non-friend smoke project first failed with the17 intended inaccessible-API
errors. Positive external compilation now succeeds; nine separate negative
compilations each fail with the intended single diagnostic, protecting constructors,
internal context/capture, serializer/material access, fault seams and mutable metadata.
An exact65-check inverse restores the three complete previous production/friend
files; Root independently replayed it.

Actual default56/14refusals and public source136/17 pass, with complete held/current
BIN/CUE digest, EOF and independent final/close checks. The first source run failed
on a test's incorrect IOException expectation for the existing InvalidDataException
missing-artifact refusal; the retained failure was corrected only in that test.
Tiny source-owner tests also retain their real earlier short-circuit RED and35-check
corrected GREEN; neither changes native compiler or publisher behavior.

The existing internal suite still passes default157/76 and source222/98. All five
full-image goldens remain unchanged. Its joint saved14/T1→70/T1, heading128/music20
case verifies public/internal actual Snapshot retention and identical Plan/CUE/receipt
and expected image2533ee96…b75b70. These runs write no native image.

Exact logs, hashes, compile controls and source-final evidence:
`_local/research/saved-level-public-boundary-run-r01.HDhS3n/HANDOFF.md`.
Window action integration and actual new GUI publication are separate next gates.
