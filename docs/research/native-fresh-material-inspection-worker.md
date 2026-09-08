# Bounded App texture-inspection worker

September8,2026. Implemented and source-tested, not yet wired to the Fresh
window. No preserved app was rebuilt, no native image was written, and the
schematic renderer is unchanged. The public source-preview decoder checkpoint
is described in `native-fresh-material-source-preview.md`.

The new App request retains the actual current document and the separately
validated raw two-slot material pair. Inspecting a draft does not Apply, Save,
upgrade its schema or establish native eligibility. At least one retail slot
is required. A completed result can only be created by the real source worker;
there is no supplied successful-result or source factory.

One background task owns up to two ordered actual static-source captures. It
has no scheduling cancellation token that could abandon cleanup. Before the
task settles, its concrete original-file owner and current named original are
independently authenticated by complete hash/length/true EOF, and every acquired
handle is closed. All primary/final/close errors retain their order and identity.
Late cancellation may suppress a thumbnail result, but only after cleanup and
without masking an earlier source failure. Same source twice remains two actual
captures with distinct slot identity. Base color receives no fake thumbnail.

The result retains the exact request and actual previews. Returned RGBA arrays
remain defensive copies. Saved-document binding, destination allocation,
schematic rendering, runtime and publication flags are explicitly false. Current
Window/session/source-selection fences and close/busy behavior still require
their separate integration; they are not attributed to this worker.

## Completed focused checks

- Genuine API RED: two missing types before production,0 warnings,1.38s.
- A test-only multiple-variable `var` declaration failed compilation; the log is
  retained. Changing it to explicit IOException fixed the syntax, not a test
  expectation or production behavior. Inclusive Release build then passed0/0.
- Source-free35 and13 real malformed CLI invocations passed; malformed modes
  left the empty test cwd unchanged and never initialized a Window or source.
- Actual-source51 passed in16.79s, covering14/T0+70/T1, reverse, same-source twice,
  base-color+static, defensive copies and exact request ownership. RGBA matches
  the independently derived original-only preview literals.
- Actual16/T1 and40/T70 hit the combined runtime-control/stale-source guard;
 70/T12 hit the combined canonical-layout guard after the first slot captured.
  These checks do not identify which individual predicate within each guard fired.
- A real opened tiny-file job and a job with the first actual source captured
  were canceled, released and drained. Late final-phase cancellation also passed.
  Unexpected drained errors are retained before assertion failures; an actual
  exclusive tiny-file reopen independently checks handle closure.
- Existing picker100, material48, aggregate219, session138, legacy65 and
  research333 stayed green. Before/after original BIN/CUE hashes matched exactly.

Exact logs and frozen source hashes:
`_local/research/material-inspection-worker-implementation-r01.yxS7Sd/HANDOFF.md`.
Reviewed tests/API RED:
`_local/research/material-inspection-worker-tests-r01.kkCvWB/`.

Runner registration is tests-first: the missing registration failed, then the
new isolated default mode and separate opt-in original-BIN/CUE mode were added.
Older UI tests now forbid their specific modes from source execution rather
than forbidding every mode of the shared executable. Their old flags and
markers are unchanged. One missed old assertion produced a retained RED before
the same scope correction. Runner71 and all Python226/13 explicit skips pass.
Neither default nor list resolves source paths; only the opt-in worker mode
does so, without a workspace argument, publisher, native output or UI setup.

Final combined all-project Release/default gate:331 checks, zero failures,
Root91392 CLOSED0. Exact report:
`/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-o2zgovh9/summary.json`.

Next is explicit two-slot inspection UI with current-session/raw-field fences,
complete cancellation/close draining and bounded bitmap disposal. Apply/Save,
ordinary picker edits and existing legacy export must remain unchanged.
