# Collision-isolated ID65 candidate

Status: complete offline candidate readback; **not gameplay accepted**.
This is a guarded tool path, not a new normal Create BIN or editor UI feature.

Exact candidate directory:
`/Users/john/Documents/Codex/2026-07-13/i-needed-to-make-a-new/spyro1editor-v3-release/_local/id65-isolated-collision-runtime-candidates/collision-6a768bff1bf2dac2c2c3af4928cc0fb99cd7606b8262c9b52748ee9ee9965495`

Files are `id65-collision-isolated-candidate.bin`,
`id65-collision-isolated-candidate.cue`, `authoring-input.json`, and
`candidate.receipt.json`. The CUE is the emulator entry point.

| Binding | SHA-256 |
| --- | --- |
| Exact existing collapsed-sky parent BIN | `65d70f5e91ed049a7a48d117a2e30223b77f37853a9a961120acb660c8cbc232` |
| Source row80 | `478850353edc16945cf9498c38aa3de4716a52b1531aa89108fcdbfbe610edf6` |
| Output row80 | `9e9b8064516684eebbd86012621d20ceab60e39bfa7711ff427bda71da952fb1` |
| Output BIN | `86ede1c7d0e5219bfc1ea24a655d531c59d07a6ce0c1629ba4d15e066fd71aae` |
| Pure compiler plan | `6a768bff1bf2dac2c2c3af4928cc0fb99cd7606b8262c9b52748ee9ee9965495` |

The new candidate retains native triangles 13995 and 19808 at support cell
(1,1,2). The collision lookup has one occupied cell instead of 4,253. It does
not delete the old triangle rows or claim that every non-lookup native consumer
has been eliminated. Environment/occlusion, source objects, texture resources,
the collapsed 35-part sky, executable and all unowned physical bytes remain
unchanged from the certified parent. Sixty-one raw sectors change, including
their exact rebuilt EDC/ECC; every physical byte is checked against the parent.

`Spyro.Editor.Id65CollisionCandidateTool` accepts:

```text
--publish <locked-fixture.bin> <exact-parent.bin> <parent.cue> <new-output-root>
--verify <locked-fixture.bin> <exact-parent.bin> <candidate.bin>
--verify-regressions <locked-fixture.bin> <exact-parent.bin> <candidate.bin>
```

Publication always replays the source-bound compiler. It refuses existing
candidate directories (including partial/residual sets), writes only a separate
staged copy, and uses the shared no-replace artifact transaction with CUE last.
Postpublication verification failures retain the committed files and report
their location. Recovery paths are returned explicitly. This is exception
rollback in a trusted local workspace, not whole-filesystem power-loss or
hostile-race atomicity.

The read-only regression mode injects corrupt views without modifying either
BIN. It rejects protected-byte changes, owned payload/header/subheader/EDC/ECC
changes, matching corruption of both input streams, initial and late length
changes, and cancellation. All 14 cases passed on the exact candidate above.
Both verification commands verify the BIN only; they do not recertify the input,
receipt or CUE files. Publication separately verifies all four artifact bytes.

Gameplay still needs cold boot, ID65 entry, stable support, jump/landing,
movement, falls/respawn and multiple camera directions. On this pass's first
runtime opportunity, DuckStation was already running an unrelated Soul Reaver
test; that process and its settings were not interrupted or modified. Do not
transfer the parent's earlier focused observations into approval of this BIN.
