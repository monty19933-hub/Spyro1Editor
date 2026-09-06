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
are rejected rather than implicitly migrated. Filesystem persistence and CAS,
viewport integration and source-bound document compilation remain separate
steps; none is inferred from a JSON round trip.

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

## Next integration boundary

A subsequent compiler must retain the actual validated document alongside its
source-issued native body/disc plan and verify that every authored field was
projected. It must not pass edited intent through the fixed-starter factory and
silently export the default floor. Persistence needs an explicit fresh-document
identity and stale-write checks before being connected to UI authoring. Normal
Create BIN and the exact fixed-candidate publisher keep their existing scopes.
