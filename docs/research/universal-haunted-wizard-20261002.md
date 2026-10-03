# Haunted Towers wizard and armor group — 2026-10-02

Evidence root: `/Users/john/Documents/Codex/2026-09-29/files-pasted-by-the-user-spyro/work/universal-finish-20261002`.

Later terrain finding: the stepped example contains 63 authored surfaces and
can exceed the native close-rendering queue with its one-face sectors. Its
bounded gameplay log contains no invalid memory accesses, so the actor/spell
observations below remain recorded, but the floor is not accepted as safe for
arbitrary movement. The separate boar run reproduced this renderer overflow.
Terrain repair and a rebuilt example remain required; see
`runtime/renderer-fault-audit-r1.json` and
`terrain-render-crash-r1/renderer-queue-evidence.json` in the evidence root.

The exporter translates the original wizard/armor paths and trigger rectangles
with each placement, resolves all seven signed armor indices to the output
rows, and imports the armor's native metal material. It preserves source state,
animation, path timing, flags and collision inputs. Removing a native linked
actor is checked against surviving native owners. A wizard must occupy a
physical row below 256 because its resurrection particles store that index in
one byte. This also applies to same-level imported copies.

Foreign wizard support currently requires the authenticated Peace Keepers
particle manager or Start Blank's authenticated Town Square manager. Other
managers remain refused. Source particle 29 receives a dedicated copied
constructor/update dispatcher; all other copied particle entries use the
source no-op tail. The wrapper preserves the native ABI and calls the
destination manager once. Exact source bodies, primary tables, native helpers,
renderer, texture pixels/palette and the wizard's emitted request are checked.
Particle 29 is reported as `source-certified-copied`; other effect types retain
their separate unverified status.

Class aliases preserve model reads and translate only the projectile's parent
classification. All 31 original armor/wizard variants compose in the cleared
host. The tests also cover two independent copies of the seven-armor group,
forced occupied class IDs, damaged source/emitted packets, and native controls.

## Integrated checks

- `haunted-ownership-integrated-r1.json`: 13,895 checks across 31 variants.
- `haunted-compose-integrated-r2/report.json`: 10,925 checks across 33 cases,
  including effect-reporting boundaries and no unresolved links.
- `haunted-current-candidate-r6/candidate.receipt.json`: 28 native BIN readback
  checks. Current integrated output is byte-identical to the runtime candidate.
- `terrace77-integrated-r1.json`: 5,232 independent native-equivalence checks;
  Terrace 77 is an existing egg-completion effect, not a missing enemy arc.

## Native cleared-level observation

Candidate BIN SHA-256:
`ccc475431b86b2aa2eba4a6d1c1579fa1a21c00e8c0020462adc9461f3670bac`.

Original Haunted Towers wizard row 52 and armor rows 59 through 53 become
physical rows 0 through 7 and persistent logical rows 135 through 142 in
cleared Peace Keepers. The cleared export contains textured replacement
platforms and ramps at three heights, and a safe native player start.

Full BIOS/New Game testing showed Spyro standing and moving on that terrain,
the wizard moving and firing, native death/respawn, and the translated trigger
starting resurrection. Green spell particles rendered. Read-only snapshots
show their live physical wizard owner and successive armor selectors. All
seven exact linked armor rows received substate 1, and the wizard advanced to
selector 7. The first armor visibly attacked; health changed from 3 to 2 with
Sparx changing from yellow to blue. The final capture contains no remaining
particle 29 instances. Distant armor activation is evidenced by its substate;
seven separate visible attack cycles are not claimed.

Receipts: `runtime/haunted-stepped-r5b/runtime-proof.json` and
`runtime/haunted-stepped-r5b/particle29-readback.json`, with raw RAM, registers
and native screenshots preserved. The test used no runtime writes or
savestates, exited without a resume state, and left its isolated card unchanged.
Earlier attract-mode attempts are preserved separately and excluded from
acceptance. The accepted run used a native game restart and observed New Game
menus.

This does not verify wizard/armor defeat, superflame interaction, their rewards,
audio, card persistence, other native destinations or all variants in gameplay.
Effect types 1, 7, 65 and 70 remain unverified. Universal gameplay and the V1.0
release remain in progress; this checkpoint creates no release package.
