# Shared native Moby library

`Browse Native Library…` is available in the regular object browser, the existing
Add Object dialog and the ID65 Blank-Level Lab object manager. It reads the
unchanged original USA source disc and exposes all **4,877 placed source rows**
from 35 retail levels. Source-level and token search filters are independent of
the current destination. Same-name and same-class instances keep their exact
source variant identities; browsing never uses the existing gallery's name
deduplication.

The separate Registered resources view exposes **1,403 registrations**, including
35 explicitly labelled special Spyro animation streams. These are observations
of native resources, not invented placement templates.

## Selection and safety

Select a row to inspect its source level/T index, full native class, derived
classification evidence, properties location and exact source identity. Retail
destinations show local registration, placed count and remaining consecutive
model-header tail slots. These fields do not imply dependency or memory fit.
ID65 reports that retail residency is not certified for this destination.

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

## Verification

- Inventory smoke: 65 source-free checks and 28 exact-source checks, including
  search bounds, variant retention, metadata matching and foreign-snapshot
  selection refusal.
- Headless `UiSmoke --native-moby-library-only`: complete retail inventory,
  source/class filtering, empty search, resource separation, unsupported and
  stale-scene refusal, exact approved-template handoff/cancel and unchanged
  scene/navigation/persistence.
- The existing `--id65-blank-lab-ui-only` route exercises the same library in
  an actually loaded lab, preserving the canonical-document guard suite.
- Both routes are included in optional source-backed offline QA. They do not
  launch the installed editor or emulator.

Headless screenshots are retained under `_local/ui-overhaul/` as
`native-moby-library-retail.png` and `native-moby-library-blank.png`.
No new universal import or gameplay acceptance is claimed by this UI.
