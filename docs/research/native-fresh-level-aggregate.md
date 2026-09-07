# Fresh-level aggregate: neutral authoring checkpoint

September 7, 2026. This is a Core document/schema checkpoint, not a new app,
native compiler, saved document migration or gameplay approval.

`NativeFreshLevelDocumentIntent` now owns the unchanged immutable floor intent
and one ordered placement list. The first closed placement variant is a Wizard
route. Each placement has a stable, unique editor ID, independent of native
class IDs, allocation slots and persistent reward owners. Future object families
belong in this aggregate, not in separate family-specific documents and Stores.

Creation copies the input list during one bounded enumeration, refuses a129th
placement, and requires both route endpoints strictly inside the actual floor.
Neutral coordinate validation does not claim grounded placement or safe runtime
capacity. In particular Z8909 and multiple placements can be valid drafts without
being accepted by the existing fixed-candidate compiler.

`NativeFreshLevelDocumentCodec` uses a distinct kind/schema, strict UTF8/JSON,
exact field sets, a64KiB document bound and explicit canonical output. It embeds
the old v1 floor codec unchanged, including that codec's independent16KiB bound.
Old files are not silently reinterpreted. No file I/O, migration, source authority
or publication capability is added by these types.

## Verification

Missing-API RED was retained before implementation. Final focused Release build
passed with zero warnings/errors; the neutral smoke passed308 checks including
214 expected refusals, repeated outside the repository working directory.
The unchanged legacy smoke passed243 checks, including83 Store and59 read-lease
checks. Root and independent peer fully reviewed the new production and tests.

The new smoke is mandatory in the default regression registry, with its own
success marker and no source/output arguments. Root's actual all-project Release
build/default regression passed315 steps with zero failures, including this smoke:

`/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-imi_vl8_/summary.json`

Detailed RED/GREEN logs and frozen implementation handoff:
`_local/research/dark-wizard-native-closure-20260906/fresh-level-aggregate-r01.L7u8AO/`.
These are engineering checks, not hundreds of gameplay runs.

## Next boundary

Before storage extraction, characterize the legacy Store's observable API and
lease/save timing. Then extract one closed internal storage engine while keeping
the old typed facade and publication read-lease contract unchanged. A later new
aggregate Store will use a separate path and explicit legacy-floor fork; the fork
must use new-only publication so a late-created destination cannot be replaced.
No Store extraction, aggregate Save/UI, auto-migration, new enemy control or
native compilation is implemented in this checkpoint. R03 remains unchanged.
