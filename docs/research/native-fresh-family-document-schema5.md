# Neutral saved family authoring — schema5

This continuation adds saved-document semantics before native or UI admission.
It does not add a new installed editor, widen the existing research exporters,
or certify arbitrary family placement. R06 remains preserved. The exact R03
runtime success is recorded separately in `native-fresh-chest-r03-runtime.md`.

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
and full supported placement lists. Schema5 must fail before source access;
neither an empty family list nor projecting old rows permits an old exporter.
The current window cannot yet render or edit the complete family union. Its
admission check therefore refuses stored schema5 without replacing a current
session, preview, source selection or raw draft. Persistence support is not
presented as a working family UI. This guard also prevents the old tiled-field
reader from downgrading a newer document to schema4.

Before removing that UI guard, implement and verify a saved P9 compilation
that binds the entire canonical schema5 document, then complete family/child
editing and a schema-preserving tiled field reader. Do not remove the existing
zero-placement tiled export guard. Genuine tiled+family source composition,
ordinary portals, persistent save ownership and universal imports remain
separate work.

## Verification

The integrated Release build-all regression passes **343 command steps, zero
failures** (`534-family-schema5-full-regression.log`; full report in
`/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-gwxyu_tc/summary.json`).
The neutral-document smoke passes1750 checks including1083 expected refusals;
saved research passes917 checks including600 expected refusals. The focused
headless UI test passes124 checks,16 schema5 refusals and all four earlier
schema workflows, without source access or native output.

Independent canonical literals cover both content arms and all old schema
goldens. Tests cover anchor/WORLD-destination semantics, strict parsing,
Save/Reopen/cancellation/staleness, an eight-case source-admission refusal
matrix, and actual headless-window admission with old workflow preservation.
The UI regression first reproduced successful but misleading adoption of a
schema5 document; the production guard makes the same test pass. The integrated
codec test initially exposed an uppercase test-fixture project ID; that fixture
was corrected before the successful rerun. Earlier failed logs are retained.

A separate original-ROM R03 source replay is pending at this checkpoint.
No new runtime or native publication claim follows from these source-free
document/UI checks, and no application bundle was replaced.
