# Exact coverage reports and bounded native gameplay checks — 2026-10-02

Evidence root: `/Users/john/Documents/Codex/2026-09-29/files-pasted-by-the-user-spyro/work/universal-finish-20261002`.

## Coverage correction

`UniversalMobyCoverage.Run` now composes each semantic variant representative
independently with its indexed companions and source heading. It no longer
reuses another row's result merely because source/class, property length,
fixup/link counts and treasure delta match. Those resource measures do not
prove row-state or linked-group ownership. Schema 3 records the actual source
rows supplied to composition. Catalog duplicate rows are explicitly enumeration
only; their individual success is not inferred. Flight replacement transactions
and level-removal scenarios remain separate evidence.

The original Dark Hollow audit covers 4,153 cells: 1,414 static compositions,
2,511 compositions with unverified dependencies, 37 class-remap refusals,
130 unsupported Flight contexts, 17 sound dependency failures, 33 extraction
failures, three VRAM failures and eight catalog restrictions. All runtime fields
remain `not-tested`. The independent source comparison passes 16,151 checks,
including 87 expanded groups with no unresolved links. No status difference
between rows sharing the former resource key was observed in this one target;
the old key is nevertheless not an ownership proof.

Receipts: `coverage-exact-darkhollow-r1.json.gz`,
`coverage-exact-darkhollow-r1.log`, `coverage-plan-regression-r1.json`.
Release Core and UniversalMobySmoke build with no warnings or errors.

## Terrace Village enemy in Dark Hollow

Current ordinary export, original Terrace row 0/class 1D3 into physical/logical
row 128, retaining all 128 original Dark Hollow actors. BIN SHA-256:
`5544de216fdb6cd3da9d042b4c25504a41f10fcb9e4c8e89eb387a00f875bcdc`.

Native rendering, flame defeat, death-flight animation, two-gem reward and
both checkpoint/durable collection flags were observed. The settled HUD shows
2. Missing electric arc particle 77 and collection-star semantic certification
remain open. The first capture was after an arrival attack, so an exact initial
health transition is not claimed.

Receipt: `runtime/terrace-darkhollow-r1/runtime-partial-proof.json`.

## Quarantined Magic Crafters pair

The private r4 collision-shadow candidate loads original source rows 12/13 in
Dark Hollow as physical/logical 128/129. BIN SHA-256:
`4c75000f7c5ad0f2e822349e9fc83bb2faf6cbebb4f2c4cd76c8fd814bea9c1c`.

Both actors render and animate. Charge defeats the blue-five actor; flame
defeats the green-two actor. Both finish retired with both collection flags,
and global/level counters plus the settled HUD show 7. This run recorded no
health loss. The source collision/state certificate is still incomplete, so
this candidate remains quarantined and is not admitted to product exports.

Receipt: `runtime/magic-shadow-r4/runtime-quarantined-proof.json`.

Both native tests used full USA BIOS, software rendering, recompiler and fresh
blank cards. No runtime writes or savestates were used. Both test processes
exited without resume states; card hashes remained unchanged. Neither test
establishes card persistence, all destination/variant coverage or full-session
acceptance. Audio was muted. These are source/evidence checkpoints, not a new
release package or completed universal gameplay claim.
