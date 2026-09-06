# Source-issued persistent Wizard scene append

2026-09-06. Implemented detached **scene-only** integration of the persistent
owner ledger with the native private-row compiler. It adds actual scene bytes,
not just capability flags. No App/project migration, accepted export history,
placement, behavior/resource installation, native runtime, save mutation, or
candidate publication is implemented by this stage.

## Public source boundary

`NativeDarkHollowWizardAppendSource.Capture(originalBin, catalog)` reuses the
authoritative all35-level inventory/native slot/owner issuer, then independently
captures the exact Dark/Blowhard/EXE owners with whole-image SHA/length/position
verification before and after. The catalog is checked, never trusted to issue
offsets or a native source certificate. There are no public source/plan metadata
constructors; `NativeDarkHollowWizardAppendInstance` contains only `FamilyId`.

The fixed source profile is Dark12, native save slot2, original128 rows, and
the actual **Blowhard33/T6** class11B donor. The separate ledger kernel smoke's
valid WizardPeak34/T6 example is not substituted. Whole source data hashes and
the actual source row variant are bound in addition to these exact owners:

| Owner | Native source extent | SHA-256 |
| --- | --- | --- |
| Dark scene | WAD1095000, length11800 | `c48c344d3809f1ea5761e968ce6f18b02d5c7bbb82fcec4a7fd453343dbb84f8` |
| Blowhard row6 | scene6388, length58 | `59401eafeab453fd9d26f14eedca58390653dc5f4fcc3d2560f7b3e8fc7afa82` |
| Complete private properties | sceneC350, length50 | `5b3d762f6117ea30e959a21bf2778f3442b03eba93125c9a0e0a642cc58d345b` |

The native donor fixup table FC3C/count191 must contain exactly one field in
the complete imported row (`6388`) and exactly one in its complete private
owner (`C350`). Native preimages are row.m_Props=C350, private+0=C378, podFF.
The only output changes within each imported pair are the row properties pointer
and private self pointer to its own +28. Row/route coordinates and every other
donor byte remain unchanged. Several identical donor coordinates overlap;
this is deliberately **not** an authored multi-enemy placement plan.

`SourceSchemaSha256` binds the code-owned logical source/schema; it is not an
authenticated previous export receipt. Shared graph reads bind the existing
native allocator/loader and actual complete donor fixup list. Behavior/model/
sound/texture/particle/reward closure remains separately required.

## Temporal allocation and native output

`EmptyOwners()` and `RestoreOwners(bytes)` return source-rebound ledger values.
`CompileActive(acceptedOwners, activeInstances)` produces a proposed ledger and
scene together only after the entire compilation succeeds. It requires every
previously active family, rejects every retired reservation, rebinds all roles
to exactly `("wizard",33,6)`, and refuses duplicate/unknown/altered identities.
There is no native retired-row/tombstone encoding or silent reactivation.

The existing `NativeMobySceneAppendPlan.Compile` retains its old exact batch
allocation branch. New internal `CompileWithPersistentOwners` checks the actual
original row fingerprint against the source issuer, rebinds the ledger, requires
a complete one-role bijection, and calls the current allocator with one request
at a time in retained native ordinal order. Each allocated native index must
equal the ledger immediately. This preserves existing canonical-domain,
private/shared/global conflict, field preimage and graph readback checks; it
does not expose arbitrary caller-assigned native ordinals or reorder schemas
with positional owner references.

The exercised temporal sequence is z->128, serialize/reopen with fresh source
objects, then [a,z]->z128/a129. Reversing the active input or reopening the
complete two-family ledger produces identical bytes/mapping. Failed active,
source, retirement or capacity batches preserve the accepted ledger and prior
scene; a valid retry receives the same ordinals.

The output remains exactly11800 bytes. All128 source rows/private bytes and147
old fixup fields/targets stay unchanged. Every family owns one58-byte row and
one50-byte private extent. The native pod/collision suffix moves once, and two
new fixup entries are appended per family. The original Moby arena is shared
with dynamic rows plus18-byte private data, so capacity remains the native
`floor((propertiesOffset-rowTableOffset-finalPlacedCount*58)/70)` calculation.

| Added families | Placed rows | Fixups | Remaining dynamic capacity | Scene SHA-256 |
| --- | ---: | ---: | ---: | --- |
| 1 | 129 | 149 | 90 | `9ac99fdd150133a75aac8298da4b738dcbcdd12a1ab649088f53c300bd1a37c4` |
| 2 | 130 | 151 | 89 | `0766f7e16adc2a58ccbc2797d6aba0d2ca4cd5ef5d788ee99599f27e9bd78fb7` |
| 19 | 147 | 185 | 76 | `7567f3b068447b2d216c0869d5314682b019e8d013790377c309e28a47c4da66` |

All offsets/sizes in this document's unprefixed native formulas are hexadecimal.
Initial fixup end11140 leaves6C0 bytes. Each added family consumes50 private plus8
fixup bytes, so nineteen fit, ending117C8 with38 zero bytes. Twenty exceed the
fixed scene by20 bytes even though row/ordinal capacity remains. A separate
128-family request reaches row-arena refusal first; neither failure reserves
anything externally.

`CopyScene()` is retail-derived **scene-only** output. Its internal original-
address payload owns WAD1095000/subfile3, entry14, length11800. It must not be
layered over unrelated authored scene edits. Later consumers must compose exact
disjoint authored owners or explicitly refuse that unsupported combination.

## Evidence and remaining integration

The isolated `Spyro.Editor.NativeWizardAppendSmoke` was written before the new
APIs. RED: Debug5.25s, zero warnings, nine intended missing-API errors; existing
Core compiled. Initial GREEN: Debug5.22s, zero warnings/errors; four source-free
constructor/intent/source boundary checks and fifty total exact-source checks
including twenty refusals. The source suite also recompiles the old fixed
single-Wizard API and confirms both its scene and level goldens unchanged.
Source-free checks alone are not temporal/native-layout evidence.

Root independent Release validation: build4.98s, zero warnings/errors;
source-free4 and exact-source50 total including20 refusals passed. Root and
Wegener independently reviewed the new source issuer and shared compiler branch;
the old fixed compiler remains byte-identical in the source suite.

The tracked standard-library `source_wizard_append_oracle.py` independently
reads raw sectors and reconstructs all1/2/19 scene bytes, with whole original
image identity checked before/after. It uses no Core or CPU/GPU emulation.
The C# source test independently splices complete byte arrays and compares every
final byte. It also verifies exact pointers, old private/source complement,
fixup order, dynamic capacity, source/role changes, missing/retired active rows,
finite257-element enumeration, tail/row limits and immutable rollback/retries.
No source ROM, save, UI, emulator setting or candidate was written.

Evidence: `_local/research/native-moby-owner-ledger-20260906/08-wizard-scene-red.log`,
`09-wizard-scene-raw-oracle.json`, and `10..12-wizard-scene-*` build/source logs.
Root and an independent peer reviewed both source files and the shared compiler
diff without a material finding; the peer also reran the raw oracle independently.
The runner registers the no-argument mode by default and the exact
`--source <selected image> <workspace root>` mode only under `--with-source-qa`.
No CUE, publication, fault or runtime mode is registered. Registration tests
first produced two missing-entry failures, then all25 Python runner tests passed
with the new entries. Missing free/source success markers are independently
rejected. Logs13/14 retain that RED/GREEN; no .NET was run for those mocked tests.

Every placement/behavior/runtime-fit/runtime/publication flag remains false.
Project transaction and accepted-export-history flags remain false. The ledger
still cannot authenticate a self-consistent edited or old JSON history against
a prior memory-card save. Fixed-one placement/resource/reward/candidate wrappers
are not generalized by this scene stage. App save/reopen/undo, explicit legacy
opt-in, accepted artifact mapping, separated authored placements and all native
runtime gates remain subsequent work; no existing save is declared compatible.
