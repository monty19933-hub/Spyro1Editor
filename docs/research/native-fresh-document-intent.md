# Fresh-native document intent

This separate document describes editable intent for the first untextured flat
fresh level. It is not a new version of the existing Blank-Level Lab schemas1–6,
not a source certificate, and not an exportable game image. Existing documents,
stores, projectors, UI selection and undo are unchanged.

The two Core/Editing types separate validated immutable intent from canonical
JSON parsing/serialization. They contain no filesystem operations, native
payloads, source paths, RAM offsets, publishers or runtime-approval fields.
Neither type starts a new editor or changes a saved workspace.

## First grammar

Canonical property order and one example:

```json
{"kind":"spyro-native-fresh-intent","schema":1,"floor":{"x":8448,"y":8448,"z":8704,"width":1024,"depth":1024},"spawn":{"x":8960,"y":8960,"heading":64},"display":{"floorRgb":[80,128,112],"skyRgb":[128,96,64]},"musicTrackId":19}
```

- Coordinates use native world units. Floor origin/size reuse the existing
  authored-surface validation:16-unit grid, bounded dimensions, and the complete
  rectangle inside one4096-unit collision cell.
- Spawn XY must be strictly inside that rectangle. This is point containment,
  not a safe player-radius inset, swept-path or landing guarantee. Heading uses
  the native byte range; spawn Z is derived as floor Z+512 and validated against
  the existing fresh-scene coordinate limits.
- Display colors contain exactly three integer RGB channels0..255. Typed input
  must be opaque. No clamping, alpha dropping or interpretation as a native
  packed word occurs at the document boundary. The explicit surface adapter
  handles native `0x00BBGGRR` conversion later.
- Music reuses the existing catalog's exact selectable IDs:0..47 excluding30
  and35, giving46 choices. A simple0..45 range would be incorrect.
- The two-triangle untextured grammar, nominal capacity16, zero placed Mobys
  and512-unit spawn height are code-owned, not caller-configurable JSON fields.
  Nominal capacity is not a measured native allocation limit.

The displayed example maps to the colors and authored coordinates used by the
retained LAB research starter. Merely creating or parsing that document does
not bind it to that candidate, authenticate any ROM, or grant export approval.

## Parsing and ownership

The codec accepts at most16KiB of UTF-8 JSON with depth bounded at8. Required
fields are exact and case-sensitive; unknown, duplicate or missing properties,
including escaped duplicate names, are rejected. Comments, BOMs, trailing
content, invalid UTF-8, fractional/exponent numbers and out-of-range values are
not accepted. Whitespace and property order can normalize to a deterministic
UTF-8 serialization with no BOM or newline.

Parsed data owns only scalar values and typed colors. Mutating the input array
or a returned serialized array cannot mutate the intent. Old Lab documents
are rejected rather than implicitly migrated. Filesystem persistence and stale-
write protection are now implemented in the separate store described below;
viewport integration remains pending. Neither follows from a JSON round trip.
Source-bound compilation is implemented as another separate boundary below.

## Regression registration

`Spyro.Editor.NativeId65FreshDocumentSmoke` is registered only as a source-free
default regression, with marker `NATIVE_ID65_FRESH_DOCUMENT_SMOKE_OK`. It has no
source, staging, publication or verification mode. The runner requires this
exact marker and supplies no BIN/CUE/workspace arguments. A real registration
RED preceded adding the case; all35 runner tests then passed.

The focused tests were likewise written before the Core types. The isolated
RED build failed only on24 missing Intent/Codec references, with zero warnings.
Initial GREEN passed154 source-free checks. Root and independent review then
identified escaped lone surrogates: ASCII `\\ud800` passes raw UTF-8 validation
but string unescaping threw an unexpected `InvalidOperationException`. A real
RED reproduced that failure before the fix. Only the actual string/property
decode boundaries now normalize that malformed-data exception; no broad catch
hides unrelated parser/programming errors. Six new Unicode cases pass.

Final focused Release:3.53 seconds, zero warnings/errors,160 source-free checks.
The integrated default Release gate passed113 steps/zero failures, including
App. Complete Python discovery passed71 tests with one explicit-source skip;
that original-source observer case had separately passed. These are engineering
checks, not a new native gameplay pass. The installed editor was not changed.
Logs and a byte-compared `root-default-report/` copy are retained in
`_local/research/native-fresh-document-20260906/`; runner registration RED/GREEN
logs are in `_local/research/fresh-native-starter-factory-20260906/`.

## Document-bound native compilation

`NativeId65FreshDocumentCompilation.CaptureAsync(originalBin, intent, token)`
retains the caller's actual immutable intent, detached canonical bytes and
their SHA-256 alongside the privately issued native disc plan. The digest
identifies the document; it is not substituted for native source ownership.
The compiler has a private constructor and no CUE, file writer, persistence,
publisher or UI operation. Cancellation reaches every source capture.

All authored fields feed the existing surface, resource, scene, staging, body
and disc producers. Display RGB uses the explicit native-color adapter; the
derived spawn height, zero placed count and nominal capacity16 remain code-
owned. Existing bounded resource/body/disc shape guards still apply: accepting
a neutral document's grammar does not promise every valid rectangle fits the
currently supported compiled profile.

The fixed research factory now constructs the exact baseline intent and calls
this compiler. Its previous smoke-local native recipe remains an independent
control. Tests compare complete PlanSHA, all five byte owners, candidate CUE
and receipt against that control. Two edited documents additionally check raw
floor origins, every LP/HP vertex and color table, collision, sky, native spawn
XYZ/heading and executable music words. One varies geometry/spawn/colors/music;
the other independently selects the last supported music ID47. Both change
the native plan and are refused by the exact fixed-candidate publisher.

Tests preceded implementation: RED had8 missing API errors and zero warnings;
the initial production GREEN build took3.66 seconds with zero warnings/errors.
Focused checks passed81 source-free and182 source-backed. The first raw-field
test incorrectly treated the resource data start as the environment start. The
real source has an8-byte zero-texture-metadata component first; the corrected
test explicitly checks that length/count and walks it. The failing log remains;
no production bytes or expected native values were changed to obtain GREEN.
The retained R02 three-file candidate was then independently reopened again
through the document-backed factory with its exact hashes and1253 changed
sectors unchanged. No edited candidate was published or gameplay-tested.

Evidence is `_local/research/native-fresh-document-compilation-20260906/`.
The root and an independent reviewer checked field projection, cancellation,
detached ownership, the unchanged default recipe and publication scope.
The final root default Release gate passed113 steps with zero failures,
including App, and a separate root source run passed182 checks again.
Python discovery passed71 tests with one explicit-source skip; the five source
observer checks passed separately. Full logs and a byte-compared report are
under `root-closeout/`. No app was installed or new gameplay result inferred.

## Next integration boundary

Project-bound persistence and stale-write checks are now implemented. A
practical UI must save, reopen, preview and compile the same authored document,
then separately produce and test an appropriately scoped edited candidate. Normal Create BIN and the
exact fixed-candidate publisher keep their existing scopes; editing a document
does not silently grant publication approval or export the unchanged starter.

## Project-bound save and reopen

`NativeId65FreshDocumentStore.Capture(EditorProjectLayout)` reads an existing
project's actual `spyro-project.json` identity/schema header and the fixed
`native-id65-fresh-document-v1.json`. It validates the header, not every unrelated
manifest field. Missing documents return an absent snapshot, not default content;
malformed files refuse without being replaced. The manifest is bounded to 64 KiB,
the document to 16 KiB, and complete length/position/EOF are checked.

`Save(expectedSnapshot, intent)` binds the same project and exact raw manifest/
document hashes. External changes, including whitespace or deletion, require
recapture. A cooperating-writer lease, canonical staged bytes with readback and
the existing publication transaction provide exception rollback. Final reopen
after commit is noncancellable; genuine readback failure reports that the save
committed and retains the writer lease for recovery. Foreign locks are never
automatically removed. Trusted ancestors/cooperating writers are required;
hostile filesystem races and crash atomicity are not certified.

Old Lab documents, project manifests, source files and outputs are not edited.
There is no delete, retarget, migration, native approval or new UI operation.
Tests use only disposable fixture projects. The tests-first RED had 45 intended
missing-type errors and zero warnings. Reviewed Release build: 4.92 seconds,
zero warnings/errors; 83 store checks plus 160 existing checks give 243 total.
Root and independent review checked the full implementation. Root's default
Release gate passed 113 steps/zero failures, 72.83 seconds summed; all 71 Python
tests completed with one explicit-source skip, and the five original-source
observer cases passed separately.

Focused logs: `_local/research/native-id65-engine-profile-20260906-r01/`, prefix
`fresh-document-store-`. Root logs and a byte-compared full report:
`_local/research/native-fresh-document-store-20260906/root-gate/`.
