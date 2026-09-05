# Structural export composition boundary

`StructuralExportCompositionPolicy` is an interim correctness gate, not a full
relocation engine. The normal combined-build coordinator plans every private
texture destination first, derives its checked address effects, and runs this
shared preflight before either the single-destination or aggregate image writer.
Previously only the aggregate branch checked pending writers, so one approved
sector-growing destination could leave later Moby edits pointing at retail bytes.

## Address effects, not just file size

A private texture record adds 184 bytes to the leading texture component of
nested level-data subfile 1. Later environment, occlusion, collision, cyclorama,
portal, particle-texture and sound components move within that subfile.

The proved `FixedTail` writer consumes zero tail while preserving nested subfile
boundaries and archive headers. Native Moby records occupy a separate scene
subfile (subfile 3 in the checked retail destinations), so their addresses remain
unchanged. Existing fixed-tail composition is retained; this is not permission
to compose arbitrary equal-length rewrites. Live component parsers or included
source-bound rebased patches must still own the moved level-data fields.

`SectorRelocation` additionally moves subsequent native scene subfiles, later
outer WAD entries, and the executable's ISO extent. Until pending writers have
explicit composed acceptance, the gate admits only ordinary terrain/native
texture writes already folded into the private transaction. It rejects pending
objects/paths/chests, custom image textures, sky/environment, names/music, and
other destinations' ordinary/native terrain writes. Names/music locate current
EXE extents, but this patch deliberately does not expand their existing aggregate
compatibility boundary without composed regression coverage.

The existing app regression adapter delegates to the same Core policy. Production
uses effects derived from exact writer plans, not that adapter's summary values.
Aggregate growth totals are checked against the preflighted child effects before
the aggregate image writer starts.

## What remains unresolved

Direct Moby exporter/CLI calls can still receive a previously relocated image
alongside a retail catalog. Their WAD table addresses are not generally rebased.
The before-image check proves bytes at the chosen address, not that the address
still denotes the intended object. Do not describe this normal-pipeline gate as
a general fix for arbitrary relocated input, dependency closure, or all structural
writer combinations. Dedicated proved recipe paths are unchanged.

A future shared build revision needs source identity, current WAD/ISO extents,
and typed entry/subfile/component-relative spans with explicit pointer ownership.
Existing private-texture maps handle their included writes and reject spans that
cross insertion points; they are not yet a universal map for scene pointers,
Moby tables, scripts, or all executable references.

## Regression

```sh
dotnet run --project src/Spyro.Editor.StructuralCompositionSmoke
```

The original red case executes the real Moby coordinate planner and writer against
a tiny synthetic shifted WAD: it changes the old row location's sentinel instead
of the relocated row. The guarded regression now rejects that composition before
planning/writing and checks the whole image remains unchanged. A proved fixed-tail
case executes the same production methods and verifies the exact intended change
with every other byte preserved. Additional source-free cases exercise address
proofs, every pending writer category, single/multi decisions, and supplemental
coordinator wiring. No game assets or large output image are required.
