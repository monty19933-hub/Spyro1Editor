# Asset and Moby dependency audit — 2026-09-04

## Current architecture

The retail and ID65 authoring paths are substantially different systems. Retail
object editing uses mutable `Scene/Moby.cs` objects, donor/template metadata in
`Editing/MobyEditStore.cs`, and the large `Exporting/MobySourcePatchExporter.cs`
patch planner. `Views/MainWindow.cs` owns Add, copy/paste, clone-companion
construction, and much of export orchestration. `Analysis/CrossLevelSwapCatalog.cs`
classifies donor records from source caches, then combines resident actor roots,
mapped recipes, dedicated runtime profiles, and historical candidate evidence.

An actor source row is 0x58 bytes. Its class is the full word at +0x36; `Moby.Type`
is actually render radius at +0x50. The native properties pointer is +0x00.
`Moby.SpecialDataPointer` retains the legacy +0x08 cache field for compatibility
and must not be used as the native properties pointer. Presentation names are
still mixed into semantic classification: `Moby.IsChest` and much of
`Moby.VisualKind` depend on label/kind strings, which `CrossLevelSwapCatalog`
consumes. Moving safety policy to decoded class/behavior contracts would remove
that coupling without sacrificing user-editable labels.

Retail placement first tries matching source-slot reuse. Supported append paths
then clone rows and properties, with many family- and level-specific branches.
`MobyCompanionClonePlanner`/`MobyCompanionCloneBuilder` preserve selected editor
links. Dedicated Green Wizard and special-chest composers supply behavior,
private properties, fixups, model/texture dependencies, and reward contracts for
specific runtime-tested routes. Generic package recipes alone do not do that.

ID65 uses immutable canonical documents and its own logical-address, allocation,
composite compiler, receipt, and publisher stack. The useful foundation is
`Id65AuthoringMobyDependencyBundleCompiler.cs`: it distinguishes shared actor
packages from instance properties and rows, pointer fixups, external dependency
intents, allocation policies, and runtime requirements. Its current catalog is
still deliberately tiny: None, Grass T107, or Red Gem T21; one visible choice at
one of nine positions. `Views/MainWindow.Id65BlankLab.cs` blocks ordinary object
insertion and directs changes through those canonical documents. A complete
game-wide catalog has not been connected to that path.

## Why model-only imports have failed

The source and recorded diagnostics identify several independent obligations:

- Actor packages must live in the actor/model subfile and maintain valid root
  ordering. The historical Artisans 0x30800 chest packages occupied subfile 0;
  the game failed when Spyro approached them.
- Zero direct Moby-row references do not prove a model is disposable. Artisans
  Life Chest T94 dynamically spawns class 0x000E, so replacing that root broke a
  dependency absent from the level's initial row inventory.
- Models do not import executable behavior. The early Toasty Wizard experiment
  lacked handlers for Wizard 0x011B and lightning 0x0026. The later complete
  Toasty bundle supplied the behavior and runtime resources.
- Mutable properties must belong to each instance and occupy a decoded scene
  component. Blowhard Wizard v1 placed its work block inside live collision
  storage; v2 expanded the actual properties component and preserved later
  components and fixups.
- Pods/groups, patrol routes, reward ownership, particles, sounds, and retirement
  behavior are part of a working instance. Presence of an actor root alone does
  not settle those contracts.

These are supported by `docs/known-limitations.md`,
`Analysis/CrossLevelActorPackageRecipeCatalog.cs`,
`Analysis/GreenWizardRuntimeBundleCatalog.cs`, and the dedicated composers.
This audit did not run a new DuckStation session or promote any runtime result.

The legacy `BuildSpecialDataAllocatorCore` still estimates lengths from the next
distinct pointer (or a 0x18/0x28 default), and `ReserveSpecialDataOffset` searches
zero bytes below its upper bound. That is an architectural hazard because zeros
inside another component can be meaningful. This audit did not establish a new
specific crashing allocation, so it did not replace that allocator wholesale.
First establish source-bound scene-component/ownership fixtures, then migrate
one proven instance family at a time to bounded properties/fixup allocation.

## Implemented regression fix

`CrossLevelCandidateRecipeFingerprint` version 1 hashed recipe names, offsets,
lengths, and root identities. It did not include any imported bytes or source
image identity. `MobySourcePatchExporter` used that fingerprint to let historical
runtime evidence promote experimental packages into normal Create BIN. A model
or animation change at the same addresses therefore retained the old approval.

A source-free regression first reproduced that collision. Version 2 now binds:

- The actual source-image SHA-256, calculated lazily once per BuildPlan using
  its open source stream. This also covers uncopied native executable/overlay
  dependencies and does not trust file timestamps as content identity.
- Full destination preimage and rebased donor-payload SHA-256 for every copy,
  including bytes beyond the 32-byte UI preview.
- Donor/destination level keys and WAD addresses, exact actor-root/actor-ID
  preimages and intended replacements. Duplicate copy targets or root slots
  are rejected.

Missing hashes and legacy offset-only evidence do not promote a new candidate.
Candidate reports expose the fingerprint version and source-image hash.
The generic catalog's Ready status now reconstructs the same current preview and
requires the same matching evidence as the exporter. A stale pass cannot make
the UI claim an export is ready when its current source cannot be validated.

The focused `Spyro.Editor.CrossLevelCandidateFingerprintSmoke` covers payload and
preimage mutations beyond previews, pointer rebases, source-image changes with
identical imported bytes, duplicate targets, missing/malformed hashes,
serialization roundtrip, ordering, stale catalog readiness, successful exact-v2
evidence, and rejection of changed-v2 evidence. It writes only temporary
synthetic JSON and does not need a ROM, BIOS, or emulator.

All 14 preserved default-project candidate-result files were inspected. The
only passed result has an empty generic `recipes` array and belongs to the
separate native Locked Chest route. All recipe-bearing results are failed or
blocked and contain no source/image hash fields. There is therefore no local
passing generic v1 approval that can or needs to be migrated. The dedicated
Artisans chest and Green Wizard runtime profiles are unchanged. Generic v1
passes in other projects require revalidation; they are not silently upgraded.

Version 2 remains a package approval contract. It does not prove arbitrary new
instance counts, property/controller combinations, authored routes, or composite
writer interactions. Those must move into the complete dependency/transaction
contract before universal placement is advertised.

## Implemented object-plan transaction correction

A generated 5 MB ISO fixture reproduced a second failure through the actual
`MobySourcePatchExporter.BuildPlan`: an added object with an out-of-range donor
was correctly skipped, but the plan retained three actor-package/root writes.
Package resolution had already mutated shared plan collections before donor and
instance validation returned false.

`MobyDependencyPlanTransaction` now keeps each non-simple cross-level object's
writes and allocation reservations provisional. Acceptance commits them;
rejection removes only that object's new patches, written offsets, recipe IDs,
and shared-property-cluster reservations. Earlier successfully installed shared
packages remain available. Diagnostic previews are retained but made
non-writable, so they cannot activate later helper emission or become runtime
candidate evidence. Two downstream controller-alias/runtime-variant selectors
were also corrected to ignore non-writable diagnostic previews.

An adjacent regression showed that an existing-slot transform with an unmapped
donor returned success from properties handling. It now reports an explicit
skip and rolls back its provisional package writes.

`Spyro.Editor.MobyPlanTransactionSmoke` covers rejected donor/type/properties,
existing-slot rejection, failed-before-success and success-before-failure
ordering, exact shared-package preservation, diagnostic helper isolation,
unchanged source, and absence of BIN publication. Its temporary runtime-evidence
JSON authorizes only the synthetic fixture for exercising the planner; it is
deleted with that fixture and represents no game runtime proof. This is an
object-scoped transaction. Multi-object behavior/reward dependency groups still
need the complete bundle contract described below.

## Textures and music

The retail texture chooser already catalogs 2,070 native donor records across
35 levels. `NativeTerrainTextureRelocationEditStore` preserves donor intent;
its appended-private v3 records include image/component/level-data bindings.
`NativeTerrainTextureRelocationAllocator`,
`NativeTexturePageOwnershipScanner`, runtime-control scanning, and private-table
composers handle VRAM ownership, CLUT/pixel allocations, HQ/LQ aliases, native
animation controllers, and component/sector growth.

Cross-level donor availability is consequently broader than private destination
capacity. Shared native-record replacement is broadly available; isolated
selected-face additions require an unused record or a compatible private row.
Current normal private-row promotions are source-bound Artisans (4), Gnasty's
World (50), and Wizard Peak (46). Many other destinations are research-only or
have packing/7-bit-ID capacity limitations. A single 7-bit texture ID cannot
address every game texture simultaneously. The design goal should be every
donor available on demand with explicit destination capacity, not an impossible
simultaneous resident palette. ID65 currently presents T25/T66 and needs this
general destination allocator integrated with its composite compiler.

`MusicTrackCatalog` exposes 46 selectable native XA tracks (of 48 slots; 30/35
reserved). `LevelMusicPatchExporter` handles 35 retail mappings plus late-play
alternates. ID65's index 35 is outside that writer's late-alternate table and
uses `UnusedLevel65MusicOwnershipClosureContract`/the guarded candidate exporter.
Thus all native music is already cataloged, but ID65 needs its ownership closure
composed into the authoring document/export path. Custom audio is separate.

## Prioritized follow-on work

### Native type and instance dependency correction (2026-09-04)

`NativeMobyDependencyFacts` now extracts the full 16-bit native class, render
radius (not actor class), native properties at +0x00, the legacy associated word
at +0x08, and active link requirements. The swap catalog and actual row-only
enemy candidate writer share the facts. Labels remain useful for display and
provisional category classification, but cannot erase a native properties
dependency. A nonzero properties pointer does **not** forbid import: it requires
an explicitly planned properties extent, internal references and loader fixups.

Read-only audit of the current 35 retail caches found 4,877 rows, all with a
nonzero native properties pointer and zero legacy +0x08 word; none lack native
pointer evidence. Replaying the prior classification assumption against the
same enriched records produced 1,022 eligible row-only slots, including 397
enemies. Corrected eligibility is 625 chest slots and zero self-contained enemy
slots. The previous 397 enemy classifications were not properties-free and
were never normal-Create-BIN approvals. This is a material correction to a
flawed model, not completion of arbitrary enemy import. Existing dedicated
Wizard/locked-chest composers and same-level cloning are not gated by the new
generic source predicate. Actual catalog reads still expose the single
runtime-proven Wizard donor for each of Wizard Peak and Magic Crafters; their
focused target UI has an independent dedicated-route predicate.

Current `SourceMobyCacheBuilder` and `PortableEditorCacheBuilder` emit the native
pointer. Older or malformed fields remain unknown rather than becoming a known
zero, including native-edit save/reload and portable cache serialization. Known
pointer field names and hexadecimal encoding remain unchanged. Malformed actor
byte fields are rejected per catalog entry, while the typed facts API rejects
invalid records explicitly.

Package planning now checks the final authored class after raw source-byte
overrides against the **staged** destination root arrays. A replaced root no
longer proves its former class resident. Matching a donor's low class byte, or
having some unrelated resident root, does not suffice. Only the existing exact
Town Square T30 controller 0x00C2 -> 0x01FE alias recipe has explicit instance
remap authority; its 0x0186 helper and 0x0149 shell roots are dependencies, not
authority to transform the controller into either helper. This preserves that
guarded alias route without promoting it. Rejected rows roll back newly staged
package writes, and portal movement bookkeeping is deferred until validation.

Source-free regression markers:

- `NATIVE_MOBY_DEPENDENCY_FACTS_SMOKE_OK`: native +0x00 versus legacy +0x08,
  full class identity, known-zero/nonzero/sentinel/unknown pointer state,
  native-edit roundtrip, malformed catalog entry and explicit remap constraints.
- `MOBY_PLAN_TRANSACTION_SMOKE_OK`: actual synthetic-image `BuildPlan` rejects
  absent or unrelated final classes (including raw source-byte overrides),
  preserves valid appends/transforms and shared packages, and checks exact
  controller alias authority without granting helper classes.
- `CROSS_LEVEL_CANDIDATE_FINGERPRINT_SMOKE_OK`: source/content approval binding
  remains intact after the instance checks.

The next dependency-model work is an owned-properties extent/fixup linker, not
another class allowlist: derive typed scene component bounds, represent shared
immutable versus private mutable properties, register internal pointer fields,
reserve per-instance extents, and emit one reversible conflict-checked bundle.
First port an already proven passive Grass properties bundle and compare its
exact bytes before generalizing enemy/controller semantics. Complete behavior,
dynamic-spawn, texture, sound, reward and lifecycle closure still requires
separate evidence and runtime tests.

1. Extend the now-tested object-scoped transaction to complete multi-object
   behavior/reward dependency groups. A controller and its required companions
   must be accepted or rejected together; partial group success is not a complete
   runnable bundle.
2. Extract typed scene components and address spaces shared by retail and ID65.
   Migrate the zero-search property allocator to explicit component ownership;
   preserve runtime profiles through binary and gameplay regressions.
3. Unify native Moby class/dependency inventory without relying on editable
   labels. Display all discovered classes and precise missing dependencies;
   only promote placement after a complete bundle is compiled and tested.
4. Separate immutable shared assets from per-instance rows/properties/routes,
   and compose dynamically spawned classes, behavior handlers, textures,
   particles/sounds, rewards, totals, and save/retirement state in one plan.
5. Route ID65 catalogs through that shared planner, then join texture/music
   intents to its logical-address/rebase map. Test multiple instances and
   mixed asset edits, including death/reload/exit, before normal Create BIN
   promotion.
