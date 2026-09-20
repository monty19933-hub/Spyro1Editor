# Fresh family authoring: bounded integration plan

Status: **neutral model/codec/storage complete at5756aaa; saved native
integration under verification; UI integration pending**. The design below
was originally reviewed September 20, 2026,
against checkpoint `55dd291` and the subsequent working-tree reward-destination
extension. This document proposes the next saved-document/editor integration;
it is not an export authorization or a report of completed tests. The R03
source-backed and full-regression runs were still running when this plan was
written. Subsequent exact-candidate R03 gameplay now shows all20 treasure points
collected and retained through native reset; see
[the separate runtime record](native-fresh-chest-r03-runtime.md). That result
permits the scoped neutral saved-family integration below, not a general
lifecycle or release pass. The schema5 implementation and343/0 regression
checkpoint are recorded in [the schema5 record](native-fresh-family-document-schema5.md).
The proposed names below are now implemented there; the ordered gates remain
applicable to the unfinished native/UI work.

## Current contract and intended outcome

The existing Fresh aggregate already owns immutable content, stable placement
IDs, ordered placements, Apply/Undo/Redo, transactional Save/Reopen and a
saved-revision-bound research publisher. Reuse those mechanisms. Do not add a
separate chest lab, sidecar authoring file or publication protocol.

The current internal source extension now provides:

- `NativeFreshChestRewardRole.Reward1` through `Reward5`.
- An immutable `NativeFreshChestRewardDestinationOverrides` owner containing
  one to five unique semantic roles, sorted by role, with finite **WORLD** XYZ
  coordinates in `0..262128`. Its existence supplies no native/source authority.
- The unchanged six-field `NativeFreshLockedChestPlacementIntent` key/chest
  anchors. `Compose` retains its null-override branch and old plan-hash JSON;
  `ComposeWithRewardDestinations` is a distinct authored arm. No empty override
  owner is accepted: absence selects the old source-relative behavior.
- A source-bound placement producer that installs each selected destination
  coherently into the D row XYZ and private `+4/+8/+C`. It preserves native
  parent/state/delay/selector ownership and checks the actual row/private
  preimages. The document/UI must not choose raw offsets or independently
  expose these two native representations.

The planned R03 experiment changes only `Reward2` to world
`(10454, 10503, 9216)`, retaining the R02 floor, key/chest anchors and other four
destinations. This is not `Reward5`, nor a request to move the entire package.
See [the source investigation and experiment limits](native-fresh-chest-authored-destinations.md).
R02 established usable landing/key/chest interaction but left one red gem
uncollected off the finite floor; see [its runtime record](native-fresh-chest-r02-runtime.md).

The smallest meaningful user-facing outcome is a saved family placement in
**Fresh Level → Level aggregate**: edit its supported anchors/destinations,
Apply, Undo, Save/Reopen and build the same fully represented research level.
The native saved-candidate path must be verified before exposing the full UI.
Universal Mobys, arbitrary tiled-family composition and normal Create BIN are
not part of this admission.

## Proposed neutral document model

Add a closed, source-independent family-placement variant to the existing
`NativeFreshPlacementIntent` union. It owns one stable editor ID, a versioned
family definition/source reference and typed parameters. A reference is intent,
not a captured asset, verified source hash, native row, runtime approval or
mutable properties buffer. The source resolver still authenticates the actual
original image and resolves every required owner.

Initially the locked-chest variant should contain independent key/chest
anchors and optional per-child WORLD destination overrides. The seven source
roles are key (`0xAD`), chest (`0xAE`) and five reward controllers (`0x0D`). They
are one authored family, not seven independently removable or duplicable
placements. The compiler owns child relationships, row expansion, native
indices, selectors, state, delays and persistence fields. Existing single-row
catalogue references remain distinct authored intent; choosing an AE row must
not silently manufacture a complete chest family.

WORLD destination semantics must be explicit:

- With no override, a reward keeps its source-relative destination as the chest
  moves. This is the existing contract.
- With an override, its WORLD XYZ remains fixed when the chest moves. A move
  may therefore become ineligible and must not silently move, clamp or discard
  the override.
- “Restore source-relative destination” removes that role's override. Removing
  the last override returns to absence, not an empty native override owner.
- An explicit override equal to today's default remains explicit authorship;
  do not normalize it away and change its behavior on a later chest move.
- A semantic role such as `Reward2` is not a final native row ordinal or save
  owner. UI child selection should be `(family editor ID, role)`.

The existing limit of128 authored placements remains only a bounded document
ingestion limit. One family expands to seven native rows; row/private storage,
dynamic slots, model/GPU/audio/code resources and persistent-owner indices have
separate actual-source capacity and conflict checks. Do not equate128 placements
with native capacity or permit128 chest families.

### Explicit schema5 option, with both content variants

Prefer an explicit schema5 upgrade rather than widening schemas1–4 implicitly.
Keep their parsers, canonical bytes and old saved/export goldens unchanged.
One proposed closed root is `kind`, `schema`, `content`, `placements`, where
`content` is a tagged union with exactly one of these closed object maps:

- `legacy-floor`: `tag`, `base` (unchanged legacy v1 floor), and nullable
  `floorMaterials`.
- `ordered-flat-tiles-v1`: `tag`, ordered `tiles`, `spawn`, `skyRgb` and
  `musicTrackId`, retaining the existing tile/material grammar.

These originally proposed names are implemented at5756aaa. There is never a second
root `base`, `surface`, spawn or music authority competing with `content`.
The new placement union can retain existing Wizard-route/native-row intent
alongside the explicit family variant; it must not reinterpret old rows.
Unknown tags, duplicate/unknown fields, malformed roles and raw authority
payloads fail closed. Keep bounded enumeration, byte/depth limits and defensive
copies; adjust a limit only with a demonstrated need and regression coverage.

Every content/material/placement edit must preserve schema5 and the entire
active content variant. In particular, current `WithTiledContent` and
`CreateTiled` hardcode schema4: they cannot be reused unchanged for a schema5
family document. Explicitly upgrading a family document to tiled content must
retain family IDs, parameters and overrides; removing its final family must
not downgrade it. This is an extension requirement, not a current schema4 bug.

## Ordered implementation and dependency gates

1. **Neutral model/codec/storage first — complete at5756aaa.** Add the family variant and explicit
   schema upgrade with source-free tests. Save/Reopen must preserve the full
   authored level, including family defaults versus explicit overrides, without
   needing a ROM or issuing compilation authority.
2. **Source-path-verified saved candidate before full UI.** Add a closed saved
   family compilation arm consuming the actual saved snapshot and the exact
   source BIN/CUE owners. Bind the complete canonical document to the issued
   Body/Host/Scene/executable/disc chain. Reproduce the validated P9 recipe, then
   prove deterministic reopened compilation, whole-image readback/inverse and
   publication failure behavior. Reuse the existing saved publisher and shared
   transaction; do not wrap a standalone test BIN as though it came from Save.
3. **Existing-window integration only after that vertical slice passes.** Add
   family/child selection and typed fields to the existing aggregate/session.
   Apply and Undo are atomic family/document operations. Include every new raw
   field, pending add and child selection in asynchronous stale-state checks.
   Show authored-only versus research-buildable status from the complete
   document, not merely a catalogue item, schema number or source class.
4. **Tiled+family composition separately.** Preserve schema4 P0 source capture
   and all standalone Wizard/Grass paths. A new family-bearing tiled producer
   must prove actual terrain/material/resource/scene/Host composition. Do not
   remove the zero-placement guard, compile a placement-free projection, use
   only the first tile or treat schema5 persistence as native export support.

The first P9 saved document must explicitly contain its required Wizard and
Grass placements as well as the chest family. The current chest chain requires
that actual P2/N32 host; no invisible Wizard/Grass scaffold may be injected.
Existing schema3 P2 and schema4 P0 arms remain independently callable and
unchanged. A full future standalone chest host is separate work.

The wider chest experiment also does not widen the historical public legacy
floor/export profile, which remains limited to2048. A schema5 family profile
must validate its actual supported wider geometry explicitly. Tiled children
remain bounded by their own current tile grammar. New geometry or placement
edits may be saved neutrally while research export remains refused.

## Required regression seams

Use these existing files as test seams; proposed new case files are extensions
to their projects, not a request to replace the old fixtures:

- `src/Spyro.Editor.NativeFreshLevelDocumentSmoke/MobyIntentCases.cs`,
  `MobyCodecCases.cs`, `MobyStorageCases.cs`, `TiledIntentCases.cs`,
  `TiledCodecCases.cs` and `TiledStorageCases.cs`: add family/schema5 cases with
  independent canonical literals for both content variants. Preserve every old
  literal. Cover null/empty/default/explicit overrides, all five roles, duplicate
  and unknown roles, coordinate boundaries, read-only copies,128/129 authored
  placements and stale/recovery storage behavior. No geometry/material operation
  may downgrade schema5 or drop family intent.
- `src/Spyro.Editor.NativeId65FreshWizardBodySmoke/FamilyChestPlacementCases.cs`
  and `src/Spyro.Editor.NativeFreshWizardAuthoringSmoke/AuthoredChestDestinationCases.cs`:
  retain the exact no-override hash branch and independently check one-role and
  multi-role edits, WORLD semantics on chest movement, reset-to-default,
  source-preimage guards, untouched child/owner/state/selector complement and
  complete Scene/Body inverse. These new source tests were not yet reported
  complete when this plan was written.
- `src/Spyro.Editor.NativeFreshSavedLevelResearchSmoke/FamilySavedCases.cs`,
  `TiledSavedCases.cs`, `SelectedTiledMaterialSavedCases.cs` and
  `SavedLevelPublicationCases.cs`: add a saved P9/family case without weakening
  old near-miss refusals. Test exact complete-document receipt membership,
  omitted/reordered/duplicate families, foreign or equal-reissued owners,
  unsupported tiled combinations, stale source/CUE/document/manifest and full
  physical readback/inverse. Preserve all old image goldens.
- `src/Spyro.Editor.UiSmoke/NativeFreshLevelDocumentSessionCases.cs`,
  `NativeFreshMobyDocumentUiCases.cs`, `NativeFreshMobyCatalogueUiCases.cs`,
  `NativeFreshTiledDocumentUiCases.cs`, `NativeFreshSavedResearchReadinessCases.cs`
  and `NativeFreshSavedResearchLifecycleCases.cs`: add atomic family/child
  editing, selection, no-op/redo, pending-add cancellation, malformed reopen,
  save failure, source/project changes and raw-field ABA during awaits. Confirm
  the same window remains honest for unsupported combinations.

Production seams are the existing
`src/Spyro.Editor.Core/Editing/NativeFreshLevelDocumentIntent.cs` and its
codec/store, a new neutral family-intent file, a new saved-family adapter beside
`src/Spyro.Editor.Core/Exporting/NativeFreshSavedMobyFamilyCompilation.cs`, and
`src/Spyro.Editor.App/Views/NativeFreshLevelDocumentSession.cs` plus the existing
`NativeFreshDocumentWindow` partials in that directory. The new arm must be dispatched deliberately by
`NativeFreshSavedLevelResearchCandidatePlan`; do not depend on today's
schema3-versus-tiled routing to infer family capability.

## Unknown owners and publication risks remain explicit

- The source family owns singleton key-state and tracked-gem globals. The shared
  instantiation allocator already rejects overlapping concurrent globals; keep
  a second live chest family refused rather than sharing private mutable state.
- Stable editor IDs do not prove native persistent-save ownership. Expansion,
  reorder, reset, re-entry, portals and old memory-card compatibility remain
  separate obligations, including native byte-index limits.
- Centre/destination containment is not footprint, route, bounce, resident
  collider or reward-collection proof. The R02 failure demonstrates this. The
  conditional trajectory investigation is not an admission certificate.
- Key-zero contribution, complete treasure census, dynamic lifetimes, peak
  memory and general native dependency closure remain unresolved where the
  compiled owner says so. Preserve experimental target20/pending-owner evidence
  in the receipt; do not serialize a user-controlled approval flag.
- Preserve held and named source identities, actual plan ancestry, saved raw
  revision/manifest identity and complete canonical authorship. Equal hashes or
  matching paths cannot replace actual source/compiled owners.
- Publish through the existing new-output transaction and leases, with exact
  artifacts and readbacks. Cancellation, competing output, stale source and
  rollback failures must preserve prior files and recovery evidence. A commit
  followed by verification/UI failure is published-but-unverified or unconfirmed
  as appropriate, never silently reported as no output. Do not promise crash
  atomicity or infer retry permission from files that happen to exist.

Acceptance of one corrected P9 candidate permits only the scoped next research
integration. It does not establish all-Moby transferability, all edited layouts,
normal Create BIN, an installed replacement for R06 or a public V6 release.
