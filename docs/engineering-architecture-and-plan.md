# Engineering baseline and stabilization plan

Audit started 2026-09-04. Baseline checkpoint: `1e8aba5`, preserving all
previously uncommitted ID65 work over `221d2a0`. The application builds on the
installed .NET 10 SDK with zero warnings and errors. This is build evidence;
the collapsed-cyclorama candidate has focused DuckStation observations, but
its complete runtime gate still needs jump and remaining camera checks.

## Architecture

The production application is `src/Spyro.Editor.App`, an Avalonia desktop
application. `src/Spyro.Editor.Core` supplies native analysis, scene decoding,
editing models, export planners/writers, persistence, and release metadata.
`Spyro.Editor.CacheTool` constructs source-derived caches. Most remaining
projects are executable smoke tests, audits, or research candidate generators.
There is no shared unit-test framework or repository-wide solution.

| Layer | Principal code | Responsibility and coupling |
| --- | --- | --- |
| Disc and native input | `Core/Exporting/DiscImage.cs`, `Analysis`, `Levels` | ISO root records, logical WAD/executable reads, source identity and 35-level catalog |
| Scene and cached projection | `Core/Scene`, `Cache`, `Workspace/EditorWorkspace.cs` | Decoded objects and terrain; cache provenance; mutable editor projection |
| Edit intent | `Core/Editing`, `Music`, `Skyboxes`, `Text` | Per-level edits, donor metadata, linked entities, snapshots and serialization |
| Export | `Core/Exporting/MobySourcePatchExporter.cs`, `TerrainPatchExporter.cs`, texture composers, music/sky/text writers | Offset-based plans, source-byte validation, private allocation, WAD/ISO growth and physical writes |
| Project persistence | `Core/Persistence` | Protected external projects, migration, snapshots, ID65 canonical documents and transactional materialization |
| Desktop integration | `App/Views/MainWindow*.cs` | Loading, saving, dialogs, undo, galleries, export routing and research gates |
| Rendering | `App/Views/EditorViewport*.cs`, `Core/Rendering` | Map/game-camera projection and native material preview |
| ID65 research | `Core/Exporting/Id65*`, `UnusedLevel65*`, `Persistence/Id65*` | Exact-state compilers, dependency witnesses, isolated candidates and runtime evidence |

The main window coordinates more than 60,000 lines across its partial files;
its main file alone exceeds 33,000 lines. The ID65 UI and viewport each exceed
12,000 lines. This makes unrelated feature policies and state transitions hard
to review. Extraction should follow tested boundaries: workspace identity,
asset dependency planning, and disc transactions. Moving code between files
without changing ownership would not address this coupling.

## Asset support and the underlying constraints

Retail music already offers 46 native tracks. The texture catalog exposes
2,070 native donor records across 35 levels. Catalog visibility is therefore
distinct from destination allocation: texture records share native pages,
palettes, controllers and LOD descriptors, while structural growth changes
offsets used by other writers.

A Moby is more than a model and a placed row. Working imports can require
animation/model roots, behavior code in level overlays, private properties,
pointer fixups, linked companions, routes, rewards and persistence indices.
Current imports encode many of these facts as donor/target-specific recipes.
Those recipes contain useful evidence but do not constitute a universal
dependency linker. Existing same-level and runtime-proven routes must retain
regression coverage while those dependencies are made explicit.

The current ID65 durable document is schema 6. Physical compiler stages 7 and
8 are separate version numbers; they must not be treated as document migrations.
The UI currently has None/Grass T107/Red Gem T21 at fixed grid positions, a small
terrain catalog and restricted texture choices. The single-sector substrate
has prior focused runtime evidence. The collapsed 35-part sky candidate is
published and has focused background/movement/two-respawn observations. Jump
was not verified in that run. The one-part sky and matching visibility
directory compiler has offline evidence only. Collision remains inherited.

## Confirmed first-wave defects

1. **Shared disc write integrity.** `DiscImage.WriteFileBytes` modifies logical
   data without rebuilding raw-sector EDC/ECC. Music, names and several Moby/sky
   writers use it; some terrain writers independently repair integrity. A later
   exporter can therefore invalidate sectors after an earlier writer succeeded.
   Fix the shared write contract and test all supported disc layouts with
   synthetic sectors, including multi-sector rejection before any mutation.
2. **Texture undo crosses project boundaries.** Texture history is keyed by
   level and retains absolute manifest/preview paths. A workspace switch can
   leave an old checkpoint eligible in another project with the same level.
   Bind history to the owning workspace and clear it on workspace replacement;
   test project switching and same-workspace navigation separately.
3. **Moby runtime approval omits content.** The v1 cross-level recipe fingerprint
   records offsets, lengths and roots but not the bytes being imported.
   Different assets at the same locations can retain the same runtime approval.
   Bind approval to full payload/preimage hashes and inspect legacy evidence
   before any migration. Existing exact recipes must not gain unearned approval.

Each first-wave fix requires a failing regression before implementation and a
passing regression afterward. Independent review checks the shared effects.

## Priorities and acceptance

1. Preserve the baseline and establish a repeatable source-free gate. Build all
   projects to find orphaned research tests; classify source-required and
   runtime tests separately. Add new correctness regressions to the fast gate.
2. Fix the three confirmed defects above. Run focused tests and application
   builds, then verify representative composed exports against the user's local
   source image. Commit each coherent change with its test evidence.
3. Consolidate asset identity and dependency planning. Represent donor source,
   payload, native roots, properties, links and relocation obligations together.
   Keep destinations with unresolved dependencies explicit. Generalize proven
   recipes through the shared planner, with byte-level equivalence tests.
4. Make export composition a single transaction with validated preimages,
   disjoint/compatible writes, one relocation map, complete sector integrity,
   readback, and recoverable BIN/CUE-set publication. Remove writer-order
   assumptions. Multi-file exception rollback must not be described as
   filesystem-wide power-loss atomicity.
5. Complete ID65 sky runtime testing, then collision isolation. Integrate the
   resulting compiler with canonical edit intent. Expand terrain/Moby/texture/
   music choices through the same asset planner used for retail levels.
6. Expand destination/runtime coverage by asset family, including animation,
   attacks, routes, rewards, death, respawn, saving, LOD and nearby object
   stability. The requested universal support is complete only when these
   dependencies and physical limits are handled through the editor workflow.
7. Extract application services from MainWindow behind the tested state and
   export contracts; update user docs and package verified builds. No public
   release or remote push is part of this local engineering checkpoint.

## Running checks

`python3 tools/Run-SpyroEditorRegression.py` builds the app and runs a small
explicit suite requiring no game image, cache, emulator, or user project.
`--build-all` first builds every discovered C# project, including research
generators, without executing those generators. Each command has a timeout;
failures and missing success markers fail the run. Logs and JSON results are
kept in a unique temporary directory printed at startup. Builds run serially
because projects share Core/App output paths.

`tools/Run-SpyroEditorOfflineQa.sh` remains the broader source-aware gate.
Its help documents modes that create disposable disc images. ID65 compiler,
materializer and runtime suites require their exact local fixtures and have
separate entry points. Builds and deterministic readback do not replace
DuckStation acceptance.

## Progress

- Baseline saved as `1e8aba5`; app baseline build passed.
- Parallel parser/save, asset/Moby, and UI/state audits completed their first
  trace of production paths and identified the defects above.
- `a5abace`: texture history now belongs to its workspace session; actual
  cross-project undo failure reproduced first, then all seven invariants passed.
- `a1eb46e` and `034e2ce`: common raw-sector integrity and strict disc parsing.
  Source-free suites pass 39 write and 60 read cases. Four real-source music
  edits generated 13 patches across two valid raw sectors; a full-image diff
  found exactly 139 intended payload/parity bytes, with the source unchanged.
- `0e49ef4`: content-bound Moby approval, per-accepted-edit dependency
  transactions, exact patch preimages/readback and native treasure-table mapping.
  Generated ISO fixtures reproduce and guard rejected-import orphan writes,
  stale approval, all three layouts and relocated executables.
- Initial repository-wide scan: 179 discovered C# projects built successfully;
  six initial source-free executables passed. This is a build inventory, not
  execution of every research tool or acceptance of its assertions.
- Expanded Release gate: app plus 13 source-free regressions built and ran;
  all 27 build/run commands passed. Results:
  `/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-ftfhc1y3/summary.json`.
- Normal Create BIN publication now has exception rollback, preserved previous
  output on no-op/failure, cancellation boundaries and retained recovery files.
  Nine injected rename-boundary failures pass. Independent review found stale
  post-export safety reports and misleading postpublication failure status;
  these integration regressions are being corrected before checkpointing.
- Shared CUE identity/layout handling and native Moby dependency classification
  are the next in-progress root fixes. The latter corrects +0x00 native
  properties versus legacy +0x08 associated-data confusion and will retain the
  existing JSON compatibility contract.
- Focused DuckStation run: blank background in inspected views, working
  movement, two respawns, lives 4 -> 3 -> 2. Jump is still unverified. See
  [the exact runtime record](runtime-evidence/2026-09-04-collapsed-cyclorama-focused.md).
  Original global/game settings were restored with matching SHA-256 hashes;
  no resume state or memory-card progress was written.

## Remaining design risks

- Export composition still has multiple WAD-growth owners and retail-relative
  addressing assumptions. A shared final relocation map is not implemented.
- Package runtime evidence does not establish correctness of every authored
  instance, companion, path, reward, or target behavior overlay.
- Native property extent/allocation heuristics and full actor-class identity
  require explicit dependency closure, not label-based readiness.
- The public release documentation describes the packaged release; schema-6
  canonical authoring and compiler-stage 7/8 work in this checkout must not be
  mistaken for a released, fully editable blank-level editor.
- Universal asset availability remains the objective. PS1 memory, texture-ID,
  VRAM and native-controller limits still require validated allocation and
  linking; removing the current gates cannot make those constraints disappear.
