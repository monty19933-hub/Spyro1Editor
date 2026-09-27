# Shared native Moby library

`Browse Native Library…` is available in the regular object browser, the existing
Add Object dialog and the ID65 Blank-Level Lab object manager. It reads the
unchanged original USA source disc and opens on **all 368 observed classes**:
**6,334 separate observations**, comprising 4,877 placed source rows, 1,403 local
registrations and 54 shared PETE registrations. This is the known source-table
union, not a claim to identify every dynamically spawned class. Source-level and token search filters are independent of
the current destination. Same-name and same-class instances keep their exact
source variant identities; browsing never uses the existing gallery's name
deduplication.

The optional Placed objects and Registered resources views narrow the list.
Resources include 35 explicitly labelled special Spyro animation streams and
the shared-only classes. The fresh-level document's existing native source
catalogue also opens on all observations.

Names, categories, native class, source level and true index are searchable.
Resource entries additionally show/search the names of classified placed rows
with the same class in the same source level; shared resources associate named
placements across source levels. These are explicitly labelled associations,
not model or behavior equivalence. Unknown resources keep their native class
identity instead of receiving an invented name.

## Selection and safety

Select a row to inspect its source level/T index, full native class, derived
classification evidence, properties location and exact source identity. Retail
destinations show local registration, placed count and remaining consecutive
model-header tail slots. These fields do not imply dependency or memory fit.
ID65 reports that retail residency is not certified for this destination.

The **Dependencies and existing evidence** expander connects the selected entry
to the existing portability matrix. It lists all eleven requirements for
arbitrary transfers and any recorded contracts bound to that exact donor
variant, with their destination scope and remaining limits. A second row with
the same class/name, or its associated model registration, cannot inherit a
donor's evidence. Requirements remain unresolved for arbitrary placement;
this does not mean that transfer is impossible or erase existing narrow recipes.

The only enabled placement handoff is **Use existing approved template**. It
requires an exact row/metadata match to a template already admitted by the
existing Create BIN gate. It then opens the unchanged Add Object workflow;
merely browsing inserts nothing. Unsupported variants, resource-only entries
and the blank lab cannot acquire export authority from the catalogue.

The dialog captures source/workspace/level operation identity and the current
Moby edit signature. Capture or selection after a source, destination or scene
change is rejected with an explanation. Search and cancel do not mutate scene
objects, selections, navigation, edit signatures or saved JSON. Catalogue
capture runs off the UI thread, with no on-disk cache or document migration.
Each capture computes the matrix once and keeps an immutable summary indexed
by exact donor variant. Search and repaint reuse it without rebuilding the
12,880 destination cells or reopening the disc. Detached test metadata reports
that source-bound evidence was not captured.

## Verification

- Inventory smoke covers search bounds, variant retention, metadata matching,
  resource-name associations, exact-donor evidence isolation, immutable summary
  reuse and foreign-snapshot selection refusal.
- Headless `UiSmoke --native-moby-library-only`: complete retail inventory,
  all-class defaults, source/class filtering, dependency explanations, empty search, resource separation, unsupported and
  stale-scene and invalid-source refusal, exact approved-template handoff/cancel and unchanged
  scene/navigation/persistence.
- The existing `--id65-blank-lab-ui-only` route exercises the same library in
  an actually loaded lab, preserving the canonical-document guard suite.
- Both routes are included in optional source-backed offline QA. They do not
  launch the installed editor or emulator.
- `UiSmoke --fresh-native-level-ui-only` covers all-entry defaults, associated
  name search, inspect-only resources, unissued evidence and the existing
  capture/cancel/stale-owner/authoring-reference lifecycle in the actual fresh window.

Headless screenshots are retained under `_local/ui-overhaul/` as
`native-moby-library-retail.png`, `native-moby-library-shared.png`,
`native-moby-library-dependencies.png` and (on the separate lab route)
`native-moby-library-blank.png`.
No new universal import or gameplay acceptance is claimed by this UI.

### September 27, 2026 focused verification

Tests were added first; the initial InventorySmoke build failed with the six
expected missing `RelatedPlacedNames`/`DescribeDependencies` API errors.
After implementation, Debug InventorySmoke and UiSmoke/App builds passed with
zero warnings/errors. Executed checks:

- InventorySmoke: **120 source-free** and **66 exact-source** checks. The actual
  USA capture retained 368 classes, 6,334 variants and all 12,880 matrix cells.
- `UiSmoke --native-moby-library-only`: actual normal browser, complete default
  inventory, expandable dependencies, source/resource filtering, existing
  approved-template handoff, stale/invalid-source refusal and no-write checks.
- `UiSmoke --fresh-native-level-ui-only`: **118 catalogue checks** plus the
  existing aggregate/material/family/readiness suite. Neutral fixtures confirmed
  all-entry defaults and resource associations without issuing source evidence.

The exact-source smoke measured **611 ms** for inventory capture and **81 ms**
for catalogue plus evidence-summary construction in one local run; these are
observations, not latency guarantees. Repeated selection checks prove the
same immutable summary is reused.

The normal shared-resource and expanded Wizard T6 dependency frames were
visually inspected. Fresh catalogue frames were retained at
`/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-fresh-level-ui-artifacts-oAJkby/`.
No installed/preserved app, R06/R07/R08 candidate, package, memory card or emulator
state was changed. Native gameplay and arbitrary cross-level import remain
outside this browser change.

An executed negative UI test initially exposed an unhandled async event error
on a wrong source BIN. The dialog now catches `InvalidDataException` explicitly
(it is not an `IOException` subtype), explains the rejection and remains safely
closable without altering scene or saved files.
