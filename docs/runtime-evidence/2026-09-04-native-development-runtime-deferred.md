# New native development candidates: runtime deferred

This records the runtime boundary for development after `c15f3b1`. No new
candidate in this phase was played in DuckStation. Source-backed readback,
native dependency checks, independent raw-sector integrity, and complete-image
hashes are static evidence only.

## Shared emulator conflict

The read-only launch preflight observed another task running Soul Reaver rescue
candidates through the same `/Applications/DuckStation.app` and user settings.
Observed process arguments included `sluagh_one_r02` (PID 70182) and later
`sluagh_one_r03` (PID 77545). An apparent gap between runs was not a safe exclusive
testing window: another run began before any Spyro settings edit or launch.
A later read-only task-status check still reported `Build Soul Reaver editor`
active with further runtime work pending. No inter-task instruction or interrupt
was sent, and no gap between that task's tests was treated as exclusive access.

This phase did not stop those processes, launch a Spyro game, modify DuckStation
settings, press emulator controls, or write a memory card/save state. It did run
the executable's help command and inspect the enabled-app inventory. Existing
source ROMs, installed editor and older candidates were not replaced.

A temporary preflight settings copy exists at
`/tmp/spyro-collision-duckstation.LX0Dnq`. It was captured during the other task's
testing and is **not an authoritative restore backup**. Do not restore it over
current settings. A future exclusive run must capture its own fresh settings
baseline, verify exact candidate identities, and restore only changes it owns.

## Retained candidates and focused acceptance

- [Cross-level and two-instance Green Wizard candidates](../native-green-wizard-dependency-contract.md):
  exercise navigation/route state, animation, lightning attacks, interaction,
  damage/death, rewards and simultaneous independent instances. The destination
  retains resident Wizard/lightning models and behavior; these are not missing
  executable/model imports. Test fresh level entry and reload independently.
- [Collision-isolated blank candidate](../id65-collision-isolated-candidate.md):
  verify initial spawn and support, movement, jump/landing, pad edges,
  off-pad fall/respawn, and reload. Check that retired source terrain no longer
  collides. Retained triangle storage and non-lookup consumers remain unproved.
- [Authored music candidates](../native-asset-development-phase.md):
  confirm the selected Stone Hill/Town Square tracks, loop behavior and reload.
  These are schema-6 geometry candidates, not the collision-isolated successor.
- [Growing texture images](../native-wad-growth-allocation-contract.md):
  remain structural research outputs, not normal gameplay-approved artifacts.

The older [collapsed-cyclorama runtime record](2026-09-04-collapsed-cyclorama-focused.md)
belongs only to its exact parent image. Its movement/fall observations are not
transferred to these new BINs, and it did not establish jump acceptance.

No current-output runtime verification or broader normal-export authorization
was added on the strength of these static checks.
