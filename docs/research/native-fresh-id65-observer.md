# Fresh ID65 read-only diagnostic observer

`tools/Observe-SpyroFreshId65.py` is a manual diagnostic for separately selected
fixed fresh-level candidates, not a general level viewer or a gameplay
certificate. Its default is the untextured A/N16 profile described first below.
No emulator was launched or connected during its implementation.

The expected whole BIN is
`1818fe50a132901b9ad670769d4967ccc0a5120897637efa2ad709b8f1d53188`.
Identify the actual DuckStation process and its open BIN independently, and
verify the published BIN/CUE/receipt before connecting. Matching RAM addresses
and printed hash annotations cannot authenticate the loaded disc.

## Scope and safety

- Requires explicit `--connect-identified-test-session`; connects only to
  localhost port 2345. Importing it or omitting the option cannot connect.
- Reuses the existing bounded, read-only memory protocol implementation. It
  sends memory-read packets and protocol acknowledgments, not execution,
  register, RAM, breakpoint or controller commands. Use the emulator UI to
  resume after its initial connection pause and to pause before each sample.
- Checks the exact level, initialization state, model/scene owners, callbacks,
  companion handle and allocation grids before interpreting heap data.
- Reads at most 16 dynamic rows and 256 particle records. It never follows
  sampled properties or model pointers. The particle end marker is a separate
  four-byte read, not a 257th record.
- Rereads 34 baseline fields after payload reads; changed fields invalidate
  all payload interpretations. Even equal fields do not make sequential reads
  atomic. Resource exhaustion, missing terminators and unavailable sampled
  primitive slack request attention rather than granting acceptance.
- Scope is initial idle and a short center-floor walk. Foreign entry, combat,
  power grants, rescue, checkpoint transitions and peak usage remain separate.
- After valid payload rereads, sampled XY outside the exact authored rectangle
  `[8448,9472] x [8448,9472]` requests attention without discarding the payload.
  These bounds are decoded from the pinned body's four vertices and two faces
  in the source fixture. They are not a player radius, safe inset, Z/contact or
  swept-path certificate. Even an inside point never grants runtime approval.

Once the exact test process is independently identified:

```sh
python3 tools/Observe-SpyroFreshId65.py --connect-identified-test-session
```

Enter `p idle` for a labeled sample or `q` to disconnect. All emitted samples
retain `runtime_pass=false` and `candidate_authenticated=false`.

## Regression evidence

Tests were written before the implementation; the absent production module
produced a real failing run. After implementation, all 15 observer tests passed.
The unchanged Wizard observer retained its 14 passing tests and still rejects
level 65. The new independent source fixture passed all five tests against the
complete original image, executable, companion package and exact emitted body.

That fixture executes the source-pinned registration routine and its complete
integer relocation helper: six animations and 90 frame-pair iterations, real
call/return delay instructions, then the actual companion-root store. It does
not synthesize the helper result or emulate GPU, audio or hardware behavior.
The derived model root is `80076558 -> 800973C4`.

Run source-free tests with:

```sh
python3 -m unittest discover -s tools/tests -p 'test_fresh_id65_observer*.py' -v
```

This now runs 22 tests with one explicit-source case skipped. Run that case with
`python3 tools/tests/test_fresh_id65_observer_source.py --source /path/to/original.bin`.
Passing either command is not a runtime pass. Root and an independent reviewer
also replayed the exact-source checks. Local RED/GREEN logs are retained under
`_local/research/native-id65-engine-profile-20260906-r01/`.

## First runtime follow-up

The exact R02 candidate entered LAB in DuckStation; see the
[runtime report](native-fresh-lab-r02-runtime-observations.md). The original
observer correctly reported matching resource gates but did not warn when the
first Down hold left the tiny floor. That recorded XYZ8848,7537,8882 case was
reproduced RED before adding the XY-only scope warning. Two new tests cover
the actual outside case, all closed edges/center, one-unit outside coordinates,
and Z-independence. No historical observation bytes were rewritten.

Root review caught an interaction with the old synthetic fixture's unrelated
8192,8192 coordinates: the new floor warning could mask existing assertions
that merely required some attention. A second real RED required a clean baseline;
the fixture now uses the actual center, and capacity, particle, PETE, renderer,
invalid-slack and changed-gate tests require their specific warning reasons.
Root replay passed22 default tests (one source-only skip) and all five exact
source checks. Logs use `fresh-id65-observer-floor-` and
`fresh-id65-observer-clean-baseline-` prefixes in the same evidence directory.

## Exact three-tile profile (2026-09-19)

`--profile tiled-three-track19-r01` selects only the independently reconstructed
three2048-square, color-only tiled fixture. It does not accept arbitrary schema4
documents or infer a profile from runtime memory. The prior default and
`edited-landing-track02-r01` profiles keep their existing gates and behavior.

The expected image annotation is
`7ba0b29b68a1bee415901382c3a3b5345ccb01ca84872502131f68361afdc305`;
body `37157a7248c90665309d8cee21c739a846aca284426cd602c65d627788c06009`.
Independently identify the exact process/open BIN and verify its saved-project
receipt before connecting. These annotations never authenticate it themselves.

Actual loader evidence286 derives used dataA58, model8008A924 and scene8009A124.
The same N16 scene layout yields rows8009A1F8, arena end8009A950 and
buckets8009A960; actual companion relocation yields root80097554. The old
profiles reject these moved values, and the tiled profile rejects old addresses,
before any dynamic-row or particle scan. No mutable global profile switching is
used. Its XY scope is the closed rectangle8448..14592 by8448..10496; Z remains
reported only, not a collision/contact assertion.

The independent review identified seams at X10496/12544 and the separate
collision-tree leaf boundary X12288 inside tile1. A proposed observed route near
Y9472, X8960->14080->8960 crosses both seams, the leaf change and the internal
triangle diagonals. Retain coordinate samples and continuous visual observations;
uniform floor colors alone do not identify a boundary crossing. Jump/landing,
camera visibility, outer-edge rejection and fall/recovery are separate checks.
No gameplay acceptance is established by preparing this observer.

Tests-first RED has11tests with39missing-profile subtest errors. Root replay310
passes42tests with3explicit-source tests skipped, including11new tiled cases:
all moved gates/cursors and rereads, complete bounded sixteenth row, malformed
profile before reads/socket, no opaque-pointer following, boundaries and Z
non-authority, mocked CLI only, unchanged legacy sample/read-trace hashes.
No source image or real socket is opened by that replay.
