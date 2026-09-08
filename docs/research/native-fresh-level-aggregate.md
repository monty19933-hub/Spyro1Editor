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

Legacy storage characterization is now complete:38 additional real-file checks
pin the exact public/nested API, filename/stage/lease namespaces, null/cancel
precedence, actual publication callback ordering, empty writer versus32-byte
read lease, timestamp-preserving canonical no-op, raw-whitespace canonicalization
and same-byte regular replacement. An unrelated aggregate-path sentinel remains
untouched. Root replayed the clean Release build and all old83/59 plus new38
checks; independent peer review is clear. Core remains unchanged. Evidence:
`fresh-store-characterization-r01.9jdmN4/` under the same private research parent.
This is a baseline against existing behavior, not a missing-API RED.

The extraction-only checkpoint is now implemented: one internal helper owns the
existing storage mechanics while the old typed facade, Snapshot and nested
publication read lease remain intact. Independent actual-file comparison finds
the moved class identical after only the approved name/visibility/constant
substitutions. Root and peer fully reviewed both files. The38 compatibility
assertions are unchanged; only their obsolete reporting suffix was removed.

Root all-project Release315/zero failures passed again:
`/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-st2mbq1y/summary.json`.
Root also replayed the actual original-ROM-backed research suite2339 checks,
including the separate saved-context42 and complete original BIN/CUE preservation.
No image output was published. Evidence: `fresh-store-extraction-r01.o7LeqT/`.
That extraction checkpoint had only the legacy storage profile; it established
the behavior preserved by the next adapter below.

## Whole-level Capture/Save adapter

The new `NativeFreshLevelDocumentStore` now captures and saves the whole aggregate
at `native-fresh-level.json`. Its immutable snapshot binds the raw manifest and
document versions. One shared lifecycle selects only two closed typed profiles;
the new profile owns a separate writer lease and stage namespace. Legacy files,
their narrower byte bound, API, read lease and saved export context stay unchanged.
There is no caller-selected path/codec registry or new native export authority.

Actual public missing-API RED preceded implementation. Final focused storage112
checks passed alongside unchanged neutral308/214, legacy83/59/38/243 and saved
context/publication source-free gates. Root and independent peer fully read the
production and final tests. Root's full all-project Release gate passed315 steps
with zero failures:
`/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-y99c4t1g/summary.json`.
Evidence: `aggregate-store-r01.VBe0I6/` under the same private research parent.
Root then replayed the actual original-ROM-backed research gate2339 checks,
including saved-context42, all editable-field/music cases and complete original
BIN/CUE preservation. It exited0 with `outputWritten=false`; no candidate was
published by this gate. Exact log: `10-root-source-saved-context.log`.

Tests exercise actual128-ID/order/non-grid-Z readback, changed base and empty
placement lists, malformed other-profile isolation, raw conflict/no-op semantics,
real staged cancellation and committed readback failure with retained evidence.
Trusted ancestors/cooperating writers remain the scope; no hostile-race or crash
atomicity claim is added. The aggregate is still not connected to R03 controls.

At this adapter checkpoint, explicit Fork/UI/native compilation were not yet
implemented. Later commitffcf176 adds actual new-only legacy-floor Fork;
ab582c2 adds the separate source-bound authored Wizard recipe. Neither silently
migrates files or changes the preserved R03 app.

## App document session — September 8

`NativeFreshLevelDocumentSession` now owns the applied whole aggregate, selection
by stable editor ID and100-entry undo/redo history over the actual Store snapshot.
Only explicit Add generates an ID. Route changes preserve every unrelated row
and the complete floor intent; canonical no-ops preserve identity and redo.
Neutral multi-placement drafts and non-grid heights are not silently snapped,
converted to a preset or reduced to the first placement.

Save installs only the Store's actual successful returned Snapshot while retaining
the applied CurrentIntent. Its independently parsed Snapshot.Intent can have
equal canonical bytes without being the same object; later native export must
compile the actual captured Snapshot.Intent. Reopen installs nothing on failure.
Fork requires a pristine absent session and propagates actual typed uncertainty
without adopting observed destination bytes or cleaning retained evidence.

Missing-class RED preceded implementation. Final actual-file tests pass138 checks
from an empty cwd; unchanged old headless UI65 and researchUI333 also pass. Tests
cover128 stable ordered IDs, exact Z8909 preservation, selection/history, raw CAS,
malformed manifest/document, cancellation and genuine foreign-lock Fork failure.
Root and an independent peer fully reviewed production and tests. This is not yet
window integration or a new app package.

The dedicated `UiSmoke --fresh-native-level-session-only` dispatch runs before
workspace discovery and without Avalonia startup, source I/O or export. It is now
a mandatory additional default regression, preserving the old UI mode. Two real
runner RED failures preceded registration; all62 runner tests pass afterward,
including unique result names and refusal to substitute the old UI success marker.
Detailed frozen evidence: `aggregate-session-r01.oGDkMe/` under the private
`dark-wizard-native-closure-20260906` research parent.
