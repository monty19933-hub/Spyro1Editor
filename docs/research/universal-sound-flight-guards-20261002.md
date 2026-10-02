# Universal sound storage and Flight dependency guards, 2 October 2026

Continued after checkpoint `200ca10`. These changes fix data preservation and
prevent unsupported copied behavior from being counted as universal gameplay.
They do not complete Universal Moby gameplay or authorize the final release.

## Sound component preservation

Peace Keepers has 20 bytes of real padding after its sound component. Its final
sound descriptor owns a further eight zero bytes. Two 12-byte particle texture
insertions previously consumed 24 apparent padding bytes and overwrote four
owned sound bytes. The insertion writer now takes an explicit owned-data end,
uses only zero bytes beyond that boundary, and grows the subfile by a sector
when needed. Following subfiles and model roots move together.

The registered `UniversalParticleBoundarySmoke` checks 1,856 synthetic cases
including invalid ownership bounds, dirty padding, zero-valued owned bytes and
two successive insertions. Its original-USA source mode passes 4,828 checks
across all 35 levels, preserving every sound byte and existing particle record,
all following subfiles and model roots. Private rechecks of 40 previously
malformed-sound compositions now reach genuine RAM-capacity refusals instead.
Deleting complete native model families can then release usable capacity;
removing four Cannon Gnorcs releases 79,872 model bytes in the tested Peace
Keepers case. That accounting is distinct from gameplay acceptance.

## Flight copied-root admission

A class ID remaining unchanged does not prove that a copied Flight object has
its timer, objective counters, spawns or course controller. The raw behavior
builder now checks every copied Flight source before namespace mapping. The
ordinary composer makes the same check when it needs copied source behavior,
while resident native Flight handlers remain available.

The refusal is typed as `UniversalFlightDependencyException`, retains immutable
source/root identities for reports, and tells the user that the Flight object
needs its course timer and objectives. Coverage labels it
`flight-dependency-ownership`, separately from memory capacity. Original overlay
hashes also prevent relabeling an original Flight source to evade the check.

`UniversalFlightDependencySmoke` derives all 20 visible root/source groups from
the original disc and passes 295 checks: no-map, identity-map and remapped raw
builder refusals, mutated source/callback checks, ordinary foreign composition,
and same-level native clone controls. The native controls retain their original
overlays exactly; this is readback evidence, not a Flight course-completion test.
The historical matrix had 1,912 copied identity-map cases affected by this gap;
those old static counts must not be treated as supported gameplay.

The editor Release build passes with no warnings/errors; all 115 regression
runner tests pass. Evidence is in task-root `work/universal-finish-20261002`:
`particle-unit-integrated-r1.log`, `particle-source-integrated-r1.json`,
`flight-guard-integrated-r1.json`, and `app-followup-build-r1.log`.

## Continuing work

The Haunted Towers wizard needs world-path and armor-index relocation. A private
adapter now passes 5,329 source/readback checks but is not integrated or accepted
in the emulator. Magic Crafters and positive Flight dependency adapters remain
private research. A separate Firework chest candidate revealed that a donor's
visibility-sector byte can hide an otherwise active imported actor. An isolated
one-byte comparison subsequently confirmed the cause. The normal exporter now
uses the native unrestricted visibility sector for new placements; the chest's
rendering, explosion and five-gem reward passed a bounded emulator test. See
[the visibility follow-up](universal-placement-visibility-20261002.md).
