# Neutral saved family authoring — schema5

Current status: schema5 neutral persistence, the bounded saved R03 compiler and
complete family editor are implemented and tested. Full regression578 passes
343 command steps with zero failures. Actual source-backed publication initiated
from the UI and the separate R07 application package remain pending; R06 is
preserved and no new runtime result is claimed. See
[the family editor implementation and test ledger](native-fresh-family-editor.md).

The original5756aaa checkpoint deliberately added saved-document semantics
before native or UI admission. That historical boundary and its tests are
recorded below. Later integration does not widen the old research profiles or
certify arbitrary family placement. The exact R03 runtime success remains
recorded separately in [its runtime record](native-fresh-chest-r03-runtime.md).

## One complete authoring document

An explicit `UpgradeForFamilies()` creates schema5. Earlier schemas1..4 keep
their existing root shapes and canonical bytes; no parser guesses an upgrade.
The root owns exactly `kind`, `schema`, `content`, and ordered `placements`.

The closed `content` union has one active arm:

- `legacy-floor`: the unchanged legacy base document plus nullable materials.
- `ordered-flat-tiles-v1`: the ordered tiles, spawn, sky RGB and music selection.

There is no competing root Base, surface, spawn, sky, music or material owner.
Legacy/tiled content edits, family removal and explicit tiled conversion retain
schema5. Existing tile2048 and public single-floor2048 export limits stay intact;
neutral legacy geometry already supports the separately bounded3072 experiment.

The placement union retains Wizard routes and native-row references and adds
`locked-chest-family`. One family has one stable editor ID, a closed versioned
definition/source reference, independent WORLD key/chest anchors, and optional
named reward destinations. The initial definition is
`peace-keepers-locked-chest-v1` for the existing USA retail source variant.
It is a neutral reference, not captured ROM bytes or native import permission.

The compiler, not the document, owns expansion to the key, chest and five D
controllers, their parent links, native indices, selectors, yaw, timing, code,
resources and persistent-state fields. Ordinary AE-row intent is not silently
converted into this family. Multiple neutral family entries may be saved, but
the128-authoring-entry bound does not admit128 native singleton instances.

## WORLD destinations and shared values

`rewardDestinations:null` means the original source-relative layout. An explicit
array has1..5 unique semantic `reward.1` through `reward.5` entries; an empty
array is rejected rather than inventing another default representation. The
canonical order follows semantic role, not input order or coordinates.

An explicit WORLD destination remains fixed when either anchor moves. A value
equal to a source default remains explicit; no source is opened to normalize
it away. Clearing overrides returns to null absence. The existing immutable
`NativeFreshChestRewardDestinationOverrides` value owner is shared with the
editor model rather than introducing a second enum or independent validator.
Its public visibility exposes only neutral values; source/placement/executable
issuers remain internal and keep their existing evidence contracts.

Anchor and destination XYZ use integer WORLD0..262128 without snapping,
clamping or wrapping. The aggregate requires key/chest XY strictly inside the
actual content union. Explicit destinations may remain outside the floor as
unsupported authorship. None of these checks proves body footprint, spawn
clearance, bounce containment, collection, capacity or native eligibility.

## Persistence and deliberate admission boundary

The existing aggregate Store, raw-revision comparison, cooperating read guard,
Save/Reopen and cancellation/publication handling retain the complete schema5
document. There is no sidecar or alternate save protocol. The64KiB outer,
depth10 and independent16KiB embedded-base parser limits are unchanged.
Unknown, missing, duplicate, case-mismatched and malformed fields fail closed;
native rows, private bytes, hashes and approval flags are not authored payloads.

Existing native saved research arms still require their original exact schemas
and full supported placement lists. Schema5 cannot enter an old exporter by
removing its family or projecting old rows. The separately verified saved P9
arm retains the entire canonical document and actual source/compiled owners;
only the exact legacy-floor R03 Wizard + Grass + complete locked-chest recipe
passes its neutral readiness preflight. See
[the saved compilation record](native-fresh-saved-locked-chest-r03.md).

At5756aaa, the window could not render/edit that complete union and therefore
refused stored schema5 without replacing its current session, preview, source
selection or raw draft. That intentional admission guard was removed only after
the saved native path and complete family editor were implemented and tested.
The existing window now edits both schema5 content variants, with independent
key/chest XYZ and five explicit/default reward controls. The seven-member
schematic/legend preserves exact authored coordinates and does not invent
default positions or simulate trajectories.

The atomic tiled-content-and-placement reader retains schema5, including after
the last family is removed. Pending Add, Cancel, Apply, Undo/Redo, no-op Apply
and Save/Reopen retain whole-document semantics. All45 authored text fields use
synchronous `TextChanging` invalidation; the21 family coordinates and five modes
also have complete structured snapshots across catalogue, inspection and saved
research awaits. Raw A-B-A changes remain stale before a dispatcher pump, and
owned form refreshes do not enqueue later raw-generation changes.

Edited/unsupported family recipes and schema5 tiled documents remain saveable
authorship, not native output. The schema4 zero-placement tiled export guard is
unchanged. Genuine tiled+family source composition, ordinary portals, persistent
save ownership and universal imports remain separate work.

## Verification

### Historical neutral checkpoint5756aaa

The integrated Release build-all regression passed **343 command steps, zero
failures** (`534-family-schema5-full-regression.log`; full report in
`/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-gwxyu_tc/summary.json`).
The neutral-document smoke passed1750 checks including1083 expected refusals;
saved research passed917 checks including600 expected refusals. The focused
headless UI test passed124 checks,16 schema5 refusals and all four earlier
schema workflows, without source access or native output.

Independent canonical literals cover both content arms and all old schema
goldens. Tests cover anchor/WORLD-destination semantics, strict parsing,
Save/Reopen/cancellation/staleness, an eight-case source-admission refusal
matrix, and actual headless-window admission with old workflow preservation.
The UI regression first reproduced successful but misleading adoption of a
schema5 document; the then-required production guard made the same test pass. The integrated
codec test initially exposed an uppercase test-fixture project ID; that fixture
was corrected before the successful rerun. Earlier failed logs are retained.

A separate original-ROM R03 source replay passes414 checks
(`535-family-schema5-r03-source.log`). Complete image, physical plan, scene,
body and executable identities remain unchanged; its full inverse restores
Original and the final BIN/CUE checks pass. This replay writes no candidate.
No new runtime or native publication claim follows from these source-free
document/UI checks, and no application bundle was replaced.

### Implemented editor checkpoint578

Focused current tests pass153 session checks,86 admission checks (16 complete
schema5 admissions, two malformed-target refusals and all four old schemas),
49 control checks,1366 family async/readiness checks and232 preview checks.
The eight old geometry/caption and macOS pixel references remain unchanged.

Timing RED574 failed before the first dispatcher pump on `FreshFloorX`.
Replacing the seven authored-text `TextChanged` subscriptions with synchronous
`TextChanging` then passed213 timing checks in576/577, covering45 text fields,
owned refresh/no-op Apply and native Stage from selected family/native entries.
The actual queued-reveal owner/generation guards remain unchanged.

The final full build-all regression passes **343 command steps, zero failures**
(`578-family-editor-final-regression.log`; report
`/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-c7chmqw2/summary.json`).
Detailed intermediate failures and reruns are retained in
[the editor ledger](native-fresh-family-editor.md). These results do not claim
source-backed publication from the UI, R07 packaging, new gameplay acceptance
or normal Create BIN support.
