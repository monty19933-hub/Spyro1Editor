# Dark Hollow Wizard: source-issued authored placement stage

This is a coordinate-only research stage, not a general Moby placement approval,
native body/trajectory proof, gameplay pass or normal Create BIN integration.
The exact original USA image remains read-only; this stage produces detached
scene/resource bytes and no disc image.

## Ownership and acceptance

`NativeDarkHollowWizardPlacementSource.Capture` accepts the existing privately
issued, one-Wizard scene append and verifies the complete original image both
before and after capture. It freezes the exact original Dark level data,
original 128 Moby centers and native collision/index/surface bytes. Caller data,
an invented inventory entry or a caller-provided “accepted” report cannot issue
a placement plan.

`Compile` accepts a bounded authored intent: original position XYZ and a second
horizontal patrol point. Horizontal coordinates use the 16-unit grid; the two
points must differ and the segment cannot exceed 8192 units. It then requires:

- A unique flat source ground datum at the origin; initial Z must lie in the
  authored research interval from ground through ground+512. This range does
  **not** prove native initialization, vertical movement or collision safety.
- The existing exact-rational certificate accepts the continuous swept square
  corridor, with authored half-width512 and clear height1536. Native ordinary
  surface flags, index membership, floor coverage and blocking geometry are
  checked. Endpoint-only sampling is not sufficient.
- The entire closed horizontal segment stays strictly more than3072 units from
  every original Moby center, including both endpoint caps. Exact BigInteger
  arithmetic prevents overflow and floating-point threshold errors. This is
  a center-distance policy, not an inference about native object shapes.

The donor first patrol point is287 units above its instance origin. The two
patrol points retain that difference; it is not silently interpreted as a
ground offset. The chosen initial height and source-based policy still leave
`InitialHeightProven`, `NativeBodyAndTrajectoryProven`, `PlacementApproved`,
`RuntimeVerified` and `PublicationApproved` false.

## Exact scene mutation ledger

The base is the unchanged source-issued appended scene, SHA256
`9ac99fdd150133a75aac8298da4b738dcbcdd12a1ab649088f53c300bd1a37c4`.
Only these nine signed32 coordinate fields are owned by placement:

| Owner | X | Y | Z |
| --- | --- | --- | --- |
| Appended true-index128 row | `B844` | `B848` | `B84C` |
| Private route point0 | `EF04` | `EF08` | `EF0C` |
| Private route point1 | `EF14` | `EF18` | `EF1C` |

Offsets are relative to the final scene, not RAM, WAD or the source donor.
All original128 rows, private pointers, route count/header/padding, reward
selector, pod detachment, properties scalars and scene fixup metadata remain
unchanged. Full inverse readback proves the complete unowned byte complement;
returned arrays are detached and reverting refuses a modified afterimage.

The resource composer's new explicit `CompileWithPlacementAsync` consumes this
typed plan alongside its exact base scene. It substitutes only the existing
owned scene payload before shared relocation, and reads the resulting scene
back afterward. The original resource API remains unplaced. Coordinate intent
does not grow the scene or alter native code, models, audio, textures, particle
descriptors or EXE copy-buffer instructions.

## Selected research intent

The read-only survey found an ordinary flat corridor at groundZ10240:
originXY `(92832,25248)` to `(91808,25248)`, with supporting source triangles
874,875,921,927. The chosen originZ10445 is an authored research choice based
on the donor's205-unit difference above its nearest lower flat plane. The
donor also has overhead geometry at that XY, so this observation is not a
unique-flat-datum or native-mover certificate for the donor. PatrolZ is10732.
Every original Dark center passes the strict closed-segment separation policy.

`src/Spyro.Editor.NativeWizardPlacementSmoke/source_placement_oracle.mjs`
independently reconstructs the appended scene and nine coordinate writes
directly from source sectors. It separately decodes the four selected ordinary
floor triangles and verifies all128 exact center distances. It does not reuse
editor assemblies or claim to replace the C# continuous corridor/index check.

## Verification record

Initial tests failed because the13 neutral geometry/intent checks and the
source issuer did not exist. The first implementation built cleanly and the
read-only survey found12 accepted candidate corridors in143 evaluated paths.
The regression runner's new registrations first failed its expected three
assertions, then passed23 Python tests after implementation.

Final Debug build succeeded in1.58 seconds with zero warnings/errors. The
source-free suite passed13 checks; the complete original-source suite passed84
checks, including full resource composition and both placed/unplaced native
overlays. The prior overlay/EXE goldens stayed unchanged. The separate tracked
overlay raw-sector oracle also passed an independent root rerun.

Final scene SHA256 is
`6b34477c459224cbbf4153336d9c4f7e2aada20d9085c975c1eca2ef74c0d16f`,
matching the independent raw-sector coordinate oracle. The placement plan is
`b63d96ab43d6b54a97c8eca63d05fdc2ff6137cc21d195b8732f59fd8a927879`;
surface certificate is
`2b4843911aff7f3f41d011ae6dac0ed113255536b61622a31ec17781d70962f4`.
The final composed level data is
`6ed74dbce04d6712e0d9ac12cfd05ae6ac334e0ec39fb7420f2105076132e537`
with scene at data+`1A7000`. Removing just this scene-coordinate stage exactly
recovers prior resource output
`26e8b7f0bebb49c5c39c0e42ea680428ddd01c6e901ebc53e619d6f39c02be91`.
Nearest original center is true-index91, squared distance15691364, strictly
above the policy threshold9437184. A deliberate route toward that center is
refused by the source-backed test.

An intermediate build failed on an in-progress reward-stage type error. A
subsequent invocation of the earlier compiled smoke was not counted as current
acceptance; the final build and84-check run above supersede it.

Retained local evidence is under
`_local/research/dark-wizard-native-closure-20260906/`: `placement-red.log`,
`placement-first-green.log`, `placement-survey.log`,
`placement-source-oracle.json`, `placement-resource-runner-red.log` and
`placement-resource-runner-green.log`, plus `placement-final-build.log`,
`placement-final-free.log` and `placement-final-source.log`.
