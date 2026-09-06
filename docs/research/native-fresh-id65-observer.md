# Fresh ID65 read-only diagnostic observer

The new `tools/Observe-SpyroFreshId65.py` is a manual diagnostic for the fixed
untextured A/N16 fresh-level candidate, not a general level viewer or a gameplay
certificate. No emulator was launched or connected during its implementation.

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

This runs 20 tests with one explicit-source case skipped. Run that case with
`python3 tools/tests/test_fresh_id65_observer_source.py --source /path/to/original.bin`.
Passing either command is not a runtime pass. Root and an independent reviewer
also replayed the exact-source checks. Local RED/GREEN logs are retained under
`_local/research/native-id65-engine-profile-20260906-r01/`.
