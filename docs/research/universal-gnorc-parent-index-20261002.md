# Gnorc Cove parent and actor-index blocker — 2 October 2026

This bounded continuation adds evidence only. No native instruction guard was
changed, no copied disc was written, and no additional composition or gameplay
coverage is claimed. Originals, packages, cards and user projects remain separate.

The current isolated Core reproduces all **396** affected cells: 22 Gnorc Cove
class `09A` source rows across 18 destinations, all refused at parent class read
`800814E0`. The existing model-tail smoke passes 1,569 source-free checks and
4,680 total original-source checks. The Release build has zero warnings/errors.
The actual original USA image was authenticated before source access and again
after the scan: `fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.

## Concrete next target

The next source class `0CA` consumer loads `a0 = properties+0x04`, checks it for
null, loads `a0.class`, compares to `09A`, and conditionally sets the parent's
animation flag. Exact current source closure owns `800814D0..80081500`, including
the R3000 load and branch delay slots. The source `0CA` spawned initializer owns
`80085BB4` (`sw s5,4(s0)`) and `80085BBC` (sets `properties+0x10` to -1). This is
strong evidence for the native parent role, but the complete reaching producer,
unchanged emitted copies, and safe namespace translation still require a proof.
A class-comparison exception alone is not sufficient end-to-end admission.

The previously retained Gnorc receipt also identifies `80081764` as the next
actor-list consumer; this continuation independently confirms that source class
`0CF` owns it. It loads signed `properties+0x10`, excludes negative indices, and
computes `g_Mobys + index*0x58` using global `80075828` before comparing the
selected actor's class to `0C9`. This is a row identity dependency as well as a
class comparison. Current recomposition stops earlier at `800814E0`, so this
report does not claim a newly observed post-fix `80081764` outcome.

The actual `0CF` spawned initializer owns parent store `80085C34` and index -1
store `80085C40`. Original placed `0CF` rows differ: all 11 have nonnegative
source row indices at properties offset `0x10`. For example, row34 targets row32
(class `0C9`), row35 targets row33 (class `0C9`), and row56 targets row219 (class
`0C7`). These fields are not original scene pointer fixups. `CaptureRow` retains
such integers; its existing external-row restoration handles registered pointer
fixups. This does not prove an active bug for every spawned dependency: spawned
initializers reset the index, and their subsequent index producers need separate
verification. It does prevent assuming all placed and spawned instances share
the same index owner.

## Recommended priority and acceptance boundary

Keep this 396-cell family at a single bounded next milestone: prove the `0CA`
spawned parent producer and exact class comparison, then audit the `0CF` signed
actor-index producers and distinguish imported placed rows from initialized
spawned rows. Enroll only authenticated source row relationships with explicit
missing/duplicate target refusal or an independently proved inactive sentinel.
Do not map a copied source index to the destination's same numeric row.

A concrete implementation route is:

1. Reuse `UniversalClassRemapping.ProveInitializerRoles` to authenticate the
   source parent's saved register, then certify the exact `0CA` initializer
   parent store and update load/compare packets. Factor the existing proven
   classification translation so this authenticated linked actor can use it
   without weakening `RequireNoUnownedClassMemory`.
2. Add source-owned indexed-row metadata alongside `UniversalRowTemplate`'s
   existing pointer references, initially restricted to the authenticated Gnorc
   `0CF` properties+0x10 consumer. Resolve unique explicitly imported target
   placements using the existing row-owner map; retain refusal for missing or
   duplicate owners until a safe inactive sentinel is separately proved.
3. Apply resolved destination physical row indices after final row allocation,
   before properties are appended. Authenticate the actual final allocation;
   source indices, save identities and destination physical slots are different
   domains. Keep dynamically initialized -1 rows separate from placed rows.
4. Add dedicated source/mutation/assembled tests and the exact affected-cell
   rescan. The change belongs in `UniversalClassRemapping.cs`, a dedicated native
   parent/index proof helper, `UniversalLevelData.cs` and
   `UniversalMobyComposer.cs`, with no UI changes.

The follow-up must verify mutated/missing/duplicate witnesses, incoming control
entries, load delays, exact copied initializer/update ownership, final assembled
translation, and real composition. Recompute these exact 396 cells before
reporting a coverage gain; then independently read back one ordinary written
export before runtime interaction. No class-memory guard relaxation, guessed
asset substitute, or placeholder product implementation is justified by the
current evidence. The bounded task ends here with native product code unchanged.

## Exact evidence

Task-root `work/ui-release-20261002/universal/` contains:

- `verification-receipt.json`: snapshot, module hash, source identity and outcomes.
- `gnorc-current-audit.json`: exact 396 current Compose results.
- `gnorc-source-owners.json`: source closures, original properties, row targets,
  specialized initializer ownership and exact native witness words.
- `probe/Program.cs`: reproducible read-only source audit and affected-cell scan.
- `current-build-release.log`, `current-source-release.log`, and audit logs.
- `source-snapshot/`: isolated Core and existing native smoke source; all Core
  source bytes still match the shared checkout at receipt generation.

The current Core module SHA256 is
`bd89ec4d990bae87f4150f1aaeecd7cf8370e02e1eac28f0bec8389915c42f67`.
All runtime statuses remain **not-tested**; written-disc readback was not
performed because this task generated no candidate.
