# Guarded native collectible admission research R01

September27, 2026. A separate successor to the unchanged card-load R01; the
first admission-specific disc, not a replacement of the R08 editor package.
It adds source-tested live-state admission and warm-reset invalidation.
Collection, native custom saves, inventory X/X and required100% integration
are **not installed** by this profile. Runtime evidence is a separate gate.

## Narrow native handoff

The complete A800-byte card-reader overlay remains an ancestor. The successor
appends2000 bytes of admission code capacity and80 bytes of state, then pads
toD000. Three copy anchors move to80087A38. Only WAD entry2 grows; growth9800
relocates the EXE toLBA53894, with exclusive end54098. The exact original EXE,
overlay, WAD and raw-disc inverses remain required.

The ordinary card-update entry invalidates live phase during menu selection;
a successful native Begin arms a specific nonzero read epoch. Gate waits for
the existing Pump, checks Prepared/read-operation3/result0/failure0, drains
busy/completion, binds slot0/controller/marker/load intent and rechecks all
three native-prefix copies. It runs the actual native envelope/SHA validator
again, then repeats those checks. The live copy preserves the entire first16
bytes for the native guard/sentinel. Phase remains zero during all copying;
Ready is the final byte written, before native shutdown and phase3 handoff.

The resident native reset function reaches paths that bypass the card overlay.
Its exact80-byte window at80012700 compacts ten repeated-base stores without
changing their effects/order or final registers. Only with reserveA00 does it
also clear801FF616. It uses no stack, code cave or transient-overlay call.
Ordinary death/level re-entry should preserve earned state, not reset it.
Cold EXE entry still uses the earlier complete NOT-LOADED initialization.

Independent review found a concrete caller-frame overlap: SP801FF5E0 allowed
the live copy to overwrite the original function's saved registers. Regression
failed before the fix. Preflight now excludes the full[SP-240,SP+68) interval
from the overlay, live reservation and native menu, with SP<=801FFF98.
Five-reserve actual original/patched reset and new-game runs preserve the
native registers, writes and calls; ten additional boundary checks were
independently reviewed.

With valid reserve, preflight rejection deliberately clears only the owned
phase byte before returning: it does not claim all caller memory is untouched
if a hostile SP aliases that byte. A rejected second Begin retains a genuinely
outstanding read; refusal is not cancellation. Artificially forcing native
menu state away from the source-pinned Waiting path is not supported recovery.
IRQ/DMA, indirect branch targets and every transition remain separate limits.

## Source and physical gates

- Admission:299 fixture checks;677 cumulative with the exact original source.
- Closed publication:14 source-free,639 source checks,24 explicit fault checks.
- Prior R01 compatibility:492 source checks, unchanged whole-disc hash.
- Python:17 successor physical and9 observer cases; runner contracts106.

Generated control was frozen from the source-tested prepublication description
`_local/collectible-card-admission-description-20260927-r01.json`. Capacity SHA
`4cc3477d0415c3d881a37231d603b71325c6d9ce17ecd6b779948119bc687c2e`.
Entry80085238, Begin800852EC, Gate80085580, Validate800858AC,
phase-publication800857F4, state80087238. Reset patch SHA
`54c681de7e529f6595bbcc497e06d7821bfad3eb0ceaf0efeb4d3f185f352205`.

An independent original-only Python reconstruction agrees with the source
publisher's expected image hash:
`99cb82cc6af22b83004a022c4f33fc3ceb9f8477ec476c1b61139afeb46a8aff`.
Prediction: `_local/collectible-card-admission-prediction-20260927-r01.json`.
Generated control uses immutable literal/hash pins, not a second MIPS compiler;
physical layout, three hooks, reset/anchors, sector integrity, untouched bytes
and original inverse are independently reconstructed. R01's original-only
prediction still gives ef36363011f74a1f2bdb56f1e709d5654f8de0bbec497c95c16e3debf8e355e7.

The new read-only observer requires a fixed independently verified candidate.
It compares the entire live ABI outside the guard, native selected-save tuple,
resident code gates and repeated bytes. A Ready fixture snapshot cannot prove
collection, durability, inventory or all-transition lifetime. After the card
overlay unloads, its nonzero stable epoch is observed, not independently
authenticated as the current admission session. All broader flags stay false.

The manually seeded test envelope has one earned identity in ordinary slot0.
It must never be described as gameplay-created progress. Normal saves and
settings, original USA/JP images, R06/R07/R08 and earlier candidates remain
protected; only explicit new research outputs are permitted.

## Published isolated candidate

Directory:`_local/collectible-card-admission-candidate-20260927-r01/`.
BIN/CUE:`collectible-card-admission-research-r01.bin` and`.cue`.
The independent physical verifier matched the actual published files against
the prior prediction:53593 changed raw sectors/115759576 changed bytes, all100
WAD entries accounted, full original inverse, EDC/ECC and extra consumed
padding54093..54097 verified. The changed size reflects relocation, not edits
to every level. Report:`physical-verification.json` beside the candidate.

Full Release passed272 checks, zero failures:
`/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-i3unu0hm/summary.json`.
The final frozen-control regression and updated runner contracts were also
rerun separately (17/106 passing). This publication checkpoint does not claim
a DuckStation run; runtime checks follow separately with a disposable card.
