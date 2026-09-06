# Native Wizard route-surface audit

Additive research engineering after source checkpoint `bb967e5`. This is a
read-only certificate over native destination bytes and an existing v1 Wizard
plan, not a route fix, a new candidate, or runtime acceptance.

## Implemented boundary

`Analysis/NativeRouteSurfaceCertificate.cs` captures an immutable native model
subfile. It walks actual component headers, reads exact special descriptors,
packed collision triangles including raw Z flag bits, explicit/beyond-count
flags, and the native lookup tree/groups through `NativeCollisionIndexCodec`.
Model/collision hashes, descriptor bytes and WAD offsets remain in the result.
The parser requires real descriptor ownership: only masked index 63 is ordinary;
a missing non-63 descriptor is an error, never the catalog's type -1 fallback.
Surface-transfer portability is not permission for an ordinary-land actor route.
Descriptor `Type` is the native byte at descriptor+0: the retail consumer at
EXE runtime `0x800573BC` uses `lbu v0,0(v0)` after loading the selected descriptor
pointer at `0x800573B4`. Upper bytes remain in `RawHex`; e.g. `00010000` means
native type 0, not 256. Special descriptors still all refuse this dry-land profile.

Each segment is continuously swept by an authored axis-aligned square: its
convex hull is the exact segment-plus-square region, including both endpoints.
BigInteger rational clipping proves an upward, ordinary, single-flat-plane floor
partition with no positive-area overlap and exactly complete area coverage.
It checks every positive-area support/tile intersection against the actual
native cell's triangle membership. Nonfloor triangles, including zero-XY-area
walls, must not intersect the ground+1 through clear-height prism. Unsupported
raw Z flags, nonordinary flag encodings, special surfaces, nonflat/stacked
support, missing support and missing index membership prevent acceptance.

This is deliberately conservative: special or unsupported geometry touching the
XY corridor is refused even when a separate vertical layer might make it safe.
No tolerant area epsilon can erase a thin gap. Native group order is retained;
retail Magic Crafters contains nondescending groups, so this read-only path uses
the codec's existing `requireDescendingOrder:false` option. It never rebuilds
the index. Routes/dimensions/coordinates and exact-geometry work are bounded;
budget exhaustion produces no certificate.

The route-local `Analysis/NativeRouteSurfaceBudget.cs` preflight runs before the
shared decoder can expand aliased groups. It permits at most 4,096 raw bytes per
descriptor and 65,536 aggregate descriptor bytes (aliases count again), 131,072
used group bytes, 65,536 logical leaves, 2,000,000 expanded triangle references,
2,048 unique tree segments and 65,536 segment visits. These are audit-resource
limits, not changes to native codec semantics. The full route shares a second
budget: 100,000 clipping calls with at most 64 input vertices each, 20,000 support
pairs, 4,096 issues, 16,384 bindings and 2,000,000 membership entries scanned.
No segment resets those counters; exceeding a limit throws before issuing any
certificate. A 2,048-support per-segment cap remains an additional bound.

The authored width is **not a physics footprint**. Neither renderer-radius
bytes nor the existing chest site's ground-test width supply Wizard collision
dimensions. The certificate does not prove native mover trajectory, slope/step
capability, initial origin height, rendered surfaces, resident-object clearance,
animated mesh visibility, combat, rewards or any runtime outcome. Route Z is
bound as authored intent but is not substituted for ground-clamped native Z.

## Source-backed entry point, unchanged v1

`Exporting/NativeGreenWizardRouteSurfaceAudit.cs` first obtains the existing
source-pinned v1 plan without modifying its request, writes, profile or hashes.
It independently binds the actual ISO/WAD target data, model subfile and native
EXE, then rehashes the entire original image after analysis. Exact USA source:
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.

For each target it audits origin → first route point → second route point,
which also covers the reverse traversal of this two-point patrol. A unique
upward flat datum must exist beneath the origin; otherwise the result is
unresolved, not accepted. The initial research policy is an authored half-width
512 and clear height 1536 in raw native world units. These values describe the
test region only. No new route or destination is selected.

```sh
dotnet build src/Spyro.Editor.NativeGreenWizardCandidateTool/Spyro.Editor.NativeGreenWizardCandidateTool.csproj -c Debug --no-restore
dotnet src/Spyro.Editor.NativeGreenWizardCandidateTool/bin/Debug/net10.0/Spyro.Editor.NativeGreenWizardCandidateTool.dll --audit-route 'Spyro the Dragon (USA).bin' magiccrafters 27
```

The command prints JSON plus `NATIVE_GREEN_WIZARD_ROUTE_AUDIT_DONE` and returns
0 only for accepted test corridors, 3 for a refused/unresolved corridor, or 1
for invalid source/request data. It creates no files. Existing `--plan`,
`--publish`, `--verify`, compiler, publisher, plans and retained candidates are
unchanged; a separate diagnostic does not silently enforce a new publication
policy or promote old v1 evidence. No durable authored schema was added.

## Regression evidence

Tests were added before the new API; the first targeted build was RED with the
new certificate types absent. The completed implementation passes:

```sh
dotnet build src/Spyro.Editor.Id65AuthoringMobyDependencyBundleCompilerSmoke/Spyro.Editor.Id65AuthoringMobyDependencyBundleCompilerSmoke.csproj -c Debug --no-restore
dotnet src/Spyro.Editor.Id65AuthoringMobyDependencyBundleCompilerSmoke/bin/Debug/net10.0/Spyro.Editor.Id65AuthoringMobyDependencyBundleCompilerSmoke.dll --source-free-route-surfaces
dotnet src/Spyro.Editor.Id65AuthoringMobyDependencyBundleCompilerSmoke/bin/Debug/net10.0/Spyro.Editor.Id65AuthoringMobyDependencyBundleCompilerSmoke.dll --source-backed-route-surfaces 'Spyro the Dragon (USA).bin'
```

- Source-free: `NATIVE_ROUTE_SURFACE_SMOKE_OK checks=48`. Native synthetic
  headers/tree/packed triangles exercise dry, diagonal, reversed and stationary
  corridors; dry endpoints with a middle gap or water; overlap; inaccessible
  support; walls/obstacles/slopes; missing/malformed/unknown descriptors; raw Z
  flags; unsupported high flag bits; implicit FF defaults; native nondescending
  order; malformed index references; bounded arguments and detached inputs.
  Added review regressions cover oversized/aliased descriptors, logical-cell and
  expanded-reference amplification, unique empty segments and repeated empty
  aliases, cumulative multi-segment issue output, exact work/output boundaries,
  and native one-byte type interpretation. Fixtures are small; no OOM/stress run
  is used. The resource regression first failed on the old implementation with
  `Expected rejection: descriptor byte budget` after a clean build (exit 134),
  then passed with the preflight and shared whole-route budget.
- Source-backed: `NATIVE_GREEN_WIZARD_ROUTE_SOURCE_SMOKE_OK checks=14` plus
  the existing v1 independent row/properties/fixup/complement/inverse goldens
  and 12 rejected v1 requests. Complete source hash is unchanged.
- Both targeted Debug builds finish with zero warnings/errors. No Release,
  App, emulator or candidate rebuild is part of this change.

Final targeted logs are retained under
`_local/research/native-route-surface-audit-20260905-r01/`:
`01-build-smoke.log`, `02-build-tool.log`, `03-source-free.log`,
`04-source-backed.log`, and the read-only CLI audit logs. Independent review
checked source binding, exact hull/coverage, resource budgets and native type
interpretation. The separately owned regression-runner integration retains the
existing properties-linker mode, adds this source-free mode, and includes the
source-backed mode only with source QA; its focused Python suite passes 14 tests.

The exact Magic Crafters T27 source-backed audit retains v1 plan SHA256
`fb9e28b4a4cbafa652b3df0104b56cc1e3f0b8066ce7bffb103cb19f702d4083`
and output scene SHA256
`68f283f14267dabdb22432cadcdd7108f9b0f350d75fba910ec1c5658bde7d19`.
Its source model WAD `0x2822800` has SHA256
`6163a158a20940be6298b3169e6cb6fa4c56a5f3010fe33d00e28507cde89696`;
collision SHA256 is
`b6b1a033a9ecad87ff72285402c09bb240e73ef411d8d59d8f6f5d880e45ba7e`.

The initial ±512 patch passes at Z23552. The continuous corridor fails with
native descriptor 1 at WAD `0x285A440` (12 zero bytes, damaging water floor),
including independently observed triangles 334/350/382/383. Its wider authored
test corridor also intersects water triangles 342/380 and unsupported terrain.
Simulating the diagnostic origin lift +307 changes the bound intent identity
but leaves every issue unchanged: initial Z cannot repair the transferred route.
These checks automate the material finding in
[the retained Wizard investigation](native-closure-runtime-20260905.md#source-bound-rendered-surface-and-hazard-proof)
without repeating or replacing the separate rendered-geometry/runtime evidence.

Current Magic audit SHA256:
`bb348eaa49195afa4d8bbcfb5f2fdc6f4b5e632ad9562d93a0be2bb0240ad591`;
surface certificate SHA256:
`b232fcc0d1a932a6ceefb2e5db73e1760dc5e54483000460dab1886f35a53cda`.
Both identify a **rejection**, not a safe route.

The same diagnostic on Wizard Peak targets 17 and 24 returns exit 3. Target 17
has a unique initial datum Z16368 but its swept corridor fails this strict flat
profile; target 24 has no unique flat datum and receives no certificate. Their
existing v1 plan and scene goldens remain unchanged. This deliberately limited
profile cannot certify those routes; that is not evidence they fail in gameplay.

The next engineering gate is an independently source-bound mover/body and
rendered-terrain model before extending to slopes, banks, actual physical
footprints or automatic route selection. A guarded publication profile would
require separate explicit integration; this audit does not change old v1.

## Integrated verification

Committed source `96ec93d` subsequently passed the parent-owned full serial
Release gate: 229 command-level steps / zero failures, including 197 builds,
29 core suites, Python runner tests and both source-aware stages. This includes
the 48 source-free route checks and 14 source-backed route checks with preserved
v1 goldens. Durable logs are under
`_local/runtime-native-closure-20260905/final-verification-20260905/spyro-regression-adf_5thx/`.
No C# or runner changes were made during that gate. The source-bound next-stage
[mover and animated-collider trace](native-wizard-mover-and-route-next-stage.md)
is a roadmap, not an expansion of this audit's declared geometry scope.
