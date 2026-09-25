# Evidence-indexed native Moby portability matrix

September 25, 2026. `NativeMobyInventoryTool --matrix` produces a deterministic,
typed JSON report over the exact unchanged original USA source. It issues no
package, placement, runtime or publication certificate. Existing source and
Create BIN gates are unchanged.

The actual source contains **368 observed classes**: 321 placed/local classes
plus 54 shared PETE registrations, with seven overlapping classes and 47
shared-only classes. The report retains every one of the **6,334 observations**
(4,877 placed rows, 1,403 local registrations, 54 shared registrations), with
full source-variant identities. It compares each class with all 35 retail
destinations: **12,880 cells**. This is the union of those known source tables,
not a claim to discover every dynamically emitted class.

```sh
dotnet build src/Spyro.Editor.NativeMobyInventoryTool -c Debug --no-restore
dotnet src/Spyro.Editor.NativeMobyInventoryTool/bin/Debug/net10.0/Spyro.Editor.NativeMobyInventoryTool.dll '<original-USA.bin>' . --matrix
dotnet run --project src/Spyro.Editor.NativeMobyInventorySmoke -c Debug
dotnet run --project src/Spyro.Editor.NativeMobyInventorySmoke -c Debug -- --source '<original-USA.bin>' .
```

## Report interpretation

- `SourceVariants` retains individual row/root observations, exact source data
  and row hashes, native class, kind, level/row/registration identity, root
  address domain and properties-location discovery. Same-class/name variants
  are not interchangeable. Shared resources have no fabricated level, WAD data
  entry or placed row. `DiscoveredPackageByteLength` remains null.
- `Classes` indexes those exact variants once per class. Each destination cell
  records original local/shared registration, original placed count and generic
  header tail slots. Those slots do not measure runtime memory capacity.
- Every cell references the `universal-import` dependency set. Its eleven
  obligations cover model/animation/relocation, textures/CLUT/VRAM, behavior and
  globals, properties/fixups/private allocation, routes/links/collision,
  sound/SPU, children/effects/rewards, player/shared assets, persistent state,
  memory/lifetime capacity, and physical/runtime acceptance. `Unresolved`
  means no generic interchange certificate has been established, not that the
  import is impossible.
- `Evidence` indexes repository source contracts and recorded validation with
  their scope. `KnownScopedCapabilities` records Town V2/V3, Wizard Peak T6
  source assets, Artisans T121 Grass source assets, the fixed Dark Hollow Wizard
  recipe, and the exact R08 tiled family. Exact donor references resolve against
  the authenticated source snapshot. This indexing does not reexecute the old
  validators, recertify artifacts, or admit every variant of those classes.
- R08 is separately scoped to the exact authored LAB65 family and complete BIN
  SHA256 `689fcf296e26d635d9c5ce6fc3fc687c2b47decce10ade2a0903f0c6bd877192`.
  It is not attached to a retail destination cell. Recorded successful saved
  publication, packaged GUI, full-disc inverse and bounded gameplay coexist
  with its remaining dependency/persistent-owner/trajectory/audio/normal-export
  limits. The record distinguishes source contracts from recorded validation.

The matrix is read-only metadata. `NativeImportApproved=false` in a discovery
cell is not a claim that every narrow candidate is unsupported. In particular,
Town's absent original AE root coexists with its exact source-bound chest
recipes. Existing compiler factories remain the authority for issuing those
recipes. Synthetic regression observations have no authenticated image identity
and cannot bind exact-original donor capabilities.

## Current PETE package blocker

The complete USA PETE header and runtime-copy owner are authenticated, including
the native 0x448-byte overhang beyond the WAD directory allocation. A root only
locates a registered model; the next registration does not prove that model's
extent. The current `NativeMobyModelTextureLayout` explicitly decodes a bounded
animated-model texture grammar supplied by a caller; it neither discovers the
package boundary nor closes arbitrary animation, vertices, collision, sound or
runtime pointers. Simple models require a different schema.

The next useful source-only step is a loader-derived, class-specific shared
model owner certificate: begin with a selected root such as the ordinary retail
egg class0x22 at PETE0x3382C, prove its model form and all pointer/stream owners
inside the actual complete PETE source, and retain unknown fields as unresolved.
Add malformed/aliased/out-of-owner and stale-source tests before admitting a
bounded package. Texture extraction, behavior, instance grammar and destination
allocation follow as independent obligations. The retail egg's identity is not
evidence for the Japanese dragonfly-egg object. No adjacent-distance package
slice or generic import route was introduced here.

## Focused verification

Debug InventorySmoke and InventoryTool builds passed with zero warnings/errors.
The full focused smoke passes **120 source-free checks** and **57 exact-source
checks**. Added cases cover overlap deduplication, shared-only and 16-bit
rootless classes, complete variant/target coverage, deterministic JSON,
immutable snapshots, duplicate observations, evidence-reference integrity,
same-class donor substitution, nearby roots without inferred sizes, synthetic
identity refusal, scoped Town evidence and non-retail R08 separation.

New markers are `NATIVE_MOBY_PORTABILITY_MATRIX_SMOKE_OK` and
`NATIVE_MOBY_PORTABILITY_MATRIX_SOURCE_OK`. The smoke project already exists;
the regression runner now requires these markers on its existing InventorySmoke
commands; all preceding inventory checks still run in those same processes.

Actual report and logs:
`_local/research/japanese-content-20260925-r01.RCmBCn/matrix-usa.json`,
`matrix-free-smoke.log`, and `matrix-source-smoke.log`.
The JSON is 14,655,298 bytes, SHA256
`a4b778eac68c4b588c4e3d9151e6a2fced80b026c953456860119a25d2403b63`.
No BIN, installed/preserved app, R08 project, beta45 package or emulator state
was written or recertified by this work.
