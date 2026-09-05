# Engineering baseline and stabilization plan

Audit started 2026-09-04. Baseline checkpoint: `1e8aba5`, preserving all
previously uncommitted ID65 work over `221d2a0`. The application builds on the
installed .NET 10 SDK with zero warnings and errors. This is build evidence;
the collapsed-cyclorama candidate has focused DuckStation observations, but
its complete runtime gate still needs jump and remaining camera checks.

Checkpoint status: the audit has led to implemented parser, export, state,
dependency and diagnostics corrections plus regression coverage, not merely a
plan. The full `334e135` build/source gate passed 211 commands; the final
`ee1abd6` Release gate passed all 44 commands, followed by the real-source Moby
catalogue regression. Universal asset placement and a fully authored blank
level remain unfinished. No installed application was replaced or release
published by this stabilization pass.

Development after `c15f3b1` continues in
[the native asset phase record](native-asset-development-phase.md). That phase
adds reusable mutable properties linkage, typed relocation, authored collision
isolation and composed music candidates. The stabilization results below remain
historical baseline evidence, not acceptance of those new outputs.

## Architecture

The production application is `src/Spyro.Editor.App`, an Avalonia desktop
application. `src/Spyro.Editor.Core` supplies native analysis, scene decoding,
editing models, export planners/writers, persistence, and release metadata.
`Spyro.Editor.CacheTool` constructs source-derived caches. Most remaining
projects are executable smoke tests, audits, or research candidate generators.
There is no shared C# unit-test framework or repository-wide solution.

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

## First-wave defects reproduced before correction

1. **Shared disc write integrity.** `DiscImage.WriteFileBytes` modifies logical
   data without rebuilding raw-sector EDC/ECC in the original implementation.
   Music, names and several Moby/sky
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
The runner currently requires POSIX process groups (macOS/Linux); Windows
process-tree termination is not implemented and fails explicitly. Eight
standard-library Python tests exercise the runner before any project build.
`--build-all` first builds every discovered C# project, including research
generators, without executing those generators. Each command has a timeout;
failures and missing success markers fail the run. Logs and JSON results are
kept in a unique temporary directory printed at startup. Builds run serially
because projects share Core/App output paths.

Individual builds/source-free runs default to 600 seconds. The optional complete
source suite has a separate `--source-qa-timeout` (1,800 seconds by default).
Timeout or keyboard interruption terminates the owned POSIX process group; it never
reports a timed-out run as passing. Forced termination cannot guarantee child
`finally` blocks completed: inspect retained temporary outputs/backups before
retrying. The runner does not automatically delete recovery artifacts.

`tools/Run-SpyroEditorOfflineQa.sh` remains the broader source-aware gate.
Its help documents modes that create disposable disc images. ID65 compiler,
materializer and runtime suites require their exact local fixtures and have
separate entry points. Builds and deterministic readback do not replace
DuckStation acceptance.

`--run-built` reruns the default offline suite without rebuilding; use it only
with identified compiled outputs. It deliberately does not establish freshness
against a changing working tree. The regression runner's ordinary source-QA
mode continues to build the source-aware projects.

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
- Frozen `67c87c4` integrated scan: all 186 discovered projects and all 16
  source-free executables passed (202 commands). The subsequent source suite
  stopped at a missing Desktop ROM path in ignored checkout-local settings.
  The path was backed up to `/tmp/spyro-source-settings-backup.XTX0vo/source-disc.json`
  and corrected to the already-verified original under the April 24 workspace.
  The HQ material test then passed all 35 levels and 2,070 records. Original
  failed-run evidence remains in
  `/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-tgj5dhdm/summary.json`;
  do not report that run as a complete source-QA pass.
- `b32c949`: normal Create BIN publication now has exception rollback, preserved previous
  output on no-op/failure, cancellation boundaries and retained recovery files.
  Nine injected rename-boundary failures pass. Independent review found stale
  post-export safety reports and misleading postpublication failure status;
  both now have passing integration regressions, including an actual synthetic
  ISO music export through final BIN/CUE publication.
- `9ebb12a`: shared CUE handling resolves the exact source image, validates its
  layout and index geometry, preserves secondary tracks, and is used across
  exporters. Twenty-two source-free cases pass; retail source CUE output is
  byte-stable except for the intended filename.
- `67c87c4`: native Moby dependency facts distinguish +0x00 properties from
  legacy +0x08 associated data and preserve unknown pointer provenance through
  JSON round trips. All 4,877 cached native rows have properties; 397 enemies
  had been wrongly classified as row-only imports. Those unsupported generic
  imports now remain blocked; the 625 ordinary-chest source-row eligibility
  entries and dedicated proved routes are unchanged. Those 625 entries are
  experimental shell-swap candidates, not approved exports or gameplay passes.
  Authored actor classes must match the full
  native class table or an explicit proved remap. Native WAD reads now use the
  shared checked parser, and executable patches validate PS-X EXE ownership.
- Focused DuckStation run: blank background in inspected views, working
  movement, two respawns, lives 4 -> 3 -> 2. Jump is still unverified. See
  [the exact runtime record](runtime-evidence/2026-09-04-collapsed-cyclorama-focused.md).
  Original global/game settings were restored with matching SHA-256 hashes;
  no resume state or memory-card progress was written.
- The source-backed catalogue regression now retains positive chest/Wizard
  coverage and explicitly forges stale enemy eligibility metadata. The actual
  candidate exporter refuses the Cliff Town T146 -> Alpine Ridge T1 properties
  obligation with zero patches. The old test's assumption that a generic enemy
  must be row-only was removed, not used to weaken the corrected dependency rule.
- Second-wave changes subsequently passed integrated verification: shared
  private-texture
  composition preflight (26 focused checks); private far-LOD appearance planning
  after final HP assignments; and the first fixed-reservation properties linker
  used by the existing ID65 Grass compiler. Existing Grass output/ownership/
  relocation/transaction goldens remain byte-identical for all three scenarios.
- `524b9c7`: properties/fixup linker extraction, including source-bound immutable
  sharing, undeclared inbound-reference rejection and fixup-envelope protection.
- `a2d1c9d`: native enemy properties regression replaces the obsolete positive
  row-only assumption, preserving positive chest and dedicated Wizard checks.
- `5d4d862`: shared single/multi private composition preflight and separate LP
  appearance dependencies. The four-row Artisans source-backed UI/build-plan
  regression proves every changed output byte is an approved rebased LP RGB
  byte; texture pages/descriptors/other structure remain unchanged. The scene
  inventory is 178 native sectors: 144 mapped HP sectors plus 34 HP-empty
  sectors supplied by the native directory and preserved without guessed
  cross-sector material relationships.
- `6a01cb1`: process-group timeout handling, explicit source-suite limits,
  non-executing `--list`, and missing-registered-project failure checks. Eight
  standard-library tests pass; that checkpoint's gate contains 19 source-free
  C# cases.
- Integrated `6a01cb1` compiled baseline: all 187 projects, all 19 source-free
  executables, the runner's eight unit cases, and the complete source-aware
  offline suite passed: 208 command-level checks, zero failures. The source
  suite took 931.58 seconds and passed its original BIN/CUE SHA-256 guard.
  Results: `/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-1mnrmg07/summary.json`.
  Final diagnostics/music source edits were prepared only after this run's
  compilation phase, without rebuilding or replacing any of its binaries;
  their subsequent integrated gate is recorded below.
- `d593de5`: support ZIPs no longer copy raw reports/logs/patch plans whose
  text can contain complete native byte payloads. The production-API leak was
  reproduced using synthetic bytes. The new inventory-only regression passes
  387 checks, and actual combined publication plus local crash/report tests
  pass with exact current-report hash/size membership and old-report exclusion.
  Local raw files remain; old generated ZIPs and installed builds are unchanged
  and require review before sharing. See the
  [bundle contract](diagnostic-support-bundle-contract.md).
- `334e135`: pure source-bound ID65 built-in music compiler. All 46 selectable
  tracks pass 4,968 independent emitted-opcode outcomes, protected-complement,
  exact-inverse, immutable-evidence and invalid/stale-input checks. The real
  source-backed Track 26 executable golden and entire serialized closure
  contract are unchanged; the latter remains
  `a3cac5dacd5adaffb1e6cc915015eb1a98478f988658895ff9e9569664ea3147`.
  This extracts a reusable compiler without changing schema/UI, enabling a
  writer or claiming runtime playback. The default gate now has 21 source-free
  C# cases.
- `ee1abd6`: follow-up candidate regression reproduced a raw saved-byte override
  replacing
  the properties pointer after the chest writer had preserved it. Post-authored
  checks now enforce all 18 protected chest bytes, the eight native enemy
  dependency bytes and supported final record types. Unsupported edits are
  rejected rather than silently discarded; all tested rejections have zero
  patches, dependency previews and outcome writes. Identical values, reward
  byte 0x53, XYZ/yaw, adjacent fields and supported type 0x18 remain accepted.
  The impossible generic enemy properties-copy fallback was removed. The
  dedicated Green Wizard route and global raw-byte editor are unchanged.
  Real-source Artisans T50 -> Alpine Ridge T33 still passes its full candidate
  plan, with count 198, while normal Create BIN still rejects that experimental
  route. Its gameplay result remains untested.
- Integrated `334e135` compiled baseline: all 188 projects, all 21 source-free
  C# executables, the runner's eight unit cases, and complete source-aware
  offline QA passed: 211 command-level checks, zero failures. Source QA took
  961.87 seconds and confirmed the original BIN/CUE remained byte-identical.
  This includes the diagnostics and music compiler changes. Results:
  `/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-6slmhtou/summary.json`.
  The final `ee1abd6` guard was developed and Debug-tested after this compilation
  without replacing its Release binaries; its separate final Release gate must
  not be conflated with this full scan.
- Final `ee1abd6` Release gate: all 44 command-level checks passed (one Python
  invocation covering eight unit cases, 22 project builds including the
  application, 21 source-free C#
  runs). Results:
  `/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-81ih_nmk/summary.json`.
  A freshly built Release source-aware catalogue run also passed, preserving
  the positive Artisans T50 -> Alpine Ridge T33 candidate plan and the native
  enemy-properties rejection. Report:
  `_local/smoke/cross-level-swap-catalog/cross-level-swap-catalog-smoke-report.json`.
  This creates plan/checklist evidence, not a gameplay-approved image. Final
  independent SHA-256 checks again matched both original source files and both
  restored DuckStation settings files.
- The actual Diagnostics dialog also passed isolated headless rendering at its
  default 720 x 520 and minimum 600 x 440 sizes. Default content fits; minimum
  size scrolls to all controls without horizontal overflow. Screenshots:
  `/tmp/spyro-diagnostics-layout-VSBzf0/diagnostics-populated-default-top.png`
  and `/tmp/spyro-diagnostics-layout-VSBzf0/diagnostics-populated-minimum-bottom.png`.
  This is not native desktop DPI, clipboard or OS file-opening acceptance.

## Next capability milestone

The smallest well-grounded ID65 expansion is guarded selection from the 46
built-in music tracks. The extracted source-bound compiler now preserves the
native slot-35 bridge and passes the tests recorded above. The
retail exporter must not append a 36th late-music row: that address belongs to
PETEXA stream LBAs. The existing Track 26 executable golden is
`30d7721b6b46b9753827ddee249bc2591dc9fc1b5a76db71a4ebffe146cc9f8c`.

Next, add explicit opt-in music intent without reinterpreting schema-1–6
baseline handles. Compose its executable-file
relative writes with the WAD80 plan at a disc-level boundary, not as fake WAD80
ranges. Establish exact overlap/preimage/readback/sector-integrity/rollback
coverage before exposing a guarded candidate control. Runtime acceptance still
needs cold start, pause, death, reset/re-entry and sustained audio, including
observed or deliberately exercised alternate choices 0, 1 and 2. The existing
Track 26 candidate has no completed runtime acceptance record.

For Mobys, the next reusable family should be resident Green Wizard properties
in Wizard Peak/Magic Crafters: explicit mutable 0x50-byte per-instance storage,
translated routes and internal pointer fixup removal/replacement. This extends
the pointer-free linker through known adapters. Importing that family into ID65
also requires behavior, lightning and particle closure; resident-level success
cannot stand in for those missing dependencies.

## Remaining design risks

- Export composition still has multiple WAD-growth owners and retail-relative
  addressing assumptions. A shared final relocation map is not implemented.
  The normal coordinator now applies one checked address-effect preflight to
  single and multiple private destinations. Direct exporters/CLIs still need
  typed live addresses for arbitrary relocated inputs; accepting live bytes at
  a stale retail address is not valid provenance. See the
  [interim composition contract](structural-export-composition-boundary.md).
- Private texture batches now consume separate far-LOD colour companions,
  without re-enabling the ordinary page allocator. This retains the existing
  representative-colour approximation. Shared LP corners and exact selected-face
  visual ownership still need better modeling and near/far runtime acceptance.
- Package runtime evidence does not establish correctness of every authored
  instance, companion, path, reward, or target behavior overlay.
- Native property extent/allocation heuristics and full actor-class identity
  require explicit dependency closure, not label-based readiness.
- ID65 Grass's proved eight-byte properties reservation is a passive scene-tail
  range, not a general native properties allocator. A shared linker must model
  these reservation kinds separately and preserve existing byte-exact Grass
  outputs. Identical property bytes do not authorize mutable instance sharing.
  The first internal linker now makes those distinctions explicit, but its
  ownership certificates come from trusted source-bound adapters. A SHA digest
  alone does not discover an owned component, prove immutability, or import
  behavior. Internal-pointer schemas and growing properties remain unsupported.
- The public release documentation describes the packaged release; schema-6
  canonical authoring and compiler-stage 7/8 work in this checkout must not be
  mistaken for a released, fully editable blank-level editor.
- Universal asset availability remains the objective. PS1 memory, texture-ID,
  VRAM and native-controller limits still require validated allocation and
  linking; removing the current gates cannot make those constraints disappear.
