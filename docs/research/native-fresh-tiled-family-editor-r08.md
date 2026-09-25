# Saved tiled-family editor integration (R08 work in progress)

September 25, 2026. This extends checkpoint `dcf5dce` through the existing saved
Candidate/publisher and actual editor controls. Private beta45 packages now
verify; actual packaged-GUI exercise and the complete physical fault matrix
are still in progress. R07 remains preserved.

## Scope

The admitted schema-5 document has four row-major 1536-square tiles, two retail
terrain descriptors (WAD14/T1 and WAD70/T1), the R03 Wizard and Grass, and one
key/chest/five-reward family. It retains all tile and actor IDs, every material
face, settings and the explicit Reward2 destination. IDs may differ without
changing native bytes; they still change saved provenance and receipts.

Only that closed profile is admitted. Other layouts remain editable/savable,
but authored-only. Normal Create BIN, arbitrary families, new persistent save
ownership and universal Moby/texture interchangeability remain unavailable.
The prior [exact-disc gameplay result](native-fresh-tiled-family-r01-runtime.md)
is separate from saved-publication and packaged-editor acceptance.

## Implementation and safety

- The new Candidate arm retains its actual complete saved compiler and shared
  R03 owners. It does not replace the saved document with its subordinate
  resource rectangle. All old arms explicitly exclude this new arm.
- Whole-image measurement must independently equal
  `689fcf296e26d635d9c5ce6fc3fc687c2b47decce10ade2a0903f0c6bd877192`,
  661,547,040 bytes and 1,297 changed sectors. Actual physical readback also
  inverses the complete image to the Original.
- The complete receipt is bounded to 65,536 bytes, with the canonical document,
  actual row/material/plan lineage and explicit invariant-string clearance
  margins. Review caught and corrected empty-object serialization of those
  internal margin structs. No receipt evidence is truncated.
- Original BIN/CUE identity and final checks, saved/manifest currentness,
  leases, failure retention and publication commit handling reuse the existing
  common engine. No parallel writer or synthetic successful test worker exists.
- UI readiness calls the same whole-document neutral preflight as the compiler.
  Raw edits, selection drift and equal-final-value ABA changes still invalidate
  the captured asynchronous ticket. Save and source validation remain required.

## Evidence so far

All numbered logs are retained under
`_local/research/v6-mixed-export-20260912-r01.jR0oBr/`.

- 644: tests-first missing Candidate members; 645: build, zero warnings/errors.
- 646: dedicated free31 checks/17 refusals; 647: default free1388/954.
- 648: source fixture refused macOS `/var` symlink before Candidate issuance;
  resolved only the new test-owned temporary root, retaining production guards.
- 649: rebuilt after receipt correction; 650: actual source148 checks/32
  refusals, complete31,134-byte receipt, deterministic reissue and both ID-only
  and raw-only saved revisions. Original final checks pass; no native output.
- 700: new runner registration test RED. 701 exposed two existing single-mode
  test assumptions. 702: all88 Python runner tests pass. Opt-in source QA now
  separately includes the old chest and new tiled Candidate, with distinct logs
  and required markers. Neither physical nor publishing modes enter that runner.
- 703: UI tests-first missing eligibility method, six errors/zero warnings.
- 704: App/Core/UiSmoke build, zero warnings/errors. Frozen source-test DLLs
  remain byte-identical while their source/physical checks execute.
- 705: focused family controls/session/admission/preview all pass, including
  new559 tiled readiness checks and24 delayed picker/prepare drift cases.
- 706: saved UI arguments218, existing readiness1319 and lifecycle6444 pass.

- 707: actual production-worker UI publication passes44 checks, including the
  complete physical689f image/1297 sectors and receipt, exact returned Save
  snapshot, and preservation of success despite post-return advisory cancel
  and owner-release failure. Original BIN/CUE finals and project guards pass.
- 709: whole aggregate UI suite passes, including new tiled-family readiness.
- 708: first package verification refused stale beta44 guide text. That package
  is retained in the private folder's `failed-build-708/`, not overwritten.
  Source guide/version/capability notes were updated before rebuilding.
- 710: Mac/Windows beta45 package verification passes, with strict signature,
  archive-roundtrip, assembly-identity and no-game-asset checks. This uses the
  same private community/ad-hoc mode as R07, not Apple notarization or upload.

651 is the separately authorized eight-case physical matrix. Its complete pass
is not claimed until the final success marker and preservation checks exist.

## Prepared private location

`_local/v6-private-preview-20260925-r08.7HLbzh/` contains an isolated test project,
copied rebuildable cache and a launch-only script expecting beta45. Its notice
still says preparation in progress pending actual packaged GUI checks. Do not distribute that folder: the
private project contains extracted game caches and will retain native outputs.
R06/R07 and ordinary user settings/projects are not overwritten.

## Next authoring step after the integration gate

The smallest meaningful extension identified by the architecture review is
painting individual faces with the two already-resident textures, while keeping
the proven geometry, actors, behavior and allocations. This is **not enabled**.
Keeping first-occurrence palette order14→70 and both selections yields127
possible eight-face patterns. One acceptance example changes the final face
from70 to14; its intended native difference is a material word plus disc parity.

Do not add another parallel Intent/Revision/Body/Executable/Disc hierarchy for
that experiment. Extract the duplicated source-neutral surface face-binding
algorithm from the existing tiled P0 and family paths, preserving both goldens.
Keep family resources and their particle/audio tail; a P0 resource certificate
cannot stand in for it. Exhaustive neutral bindings/inverse tests, independent
source and complete-image comparison, saved/UI fault coverage and exact-disc
DuckStation testing must precede any broader admission.
