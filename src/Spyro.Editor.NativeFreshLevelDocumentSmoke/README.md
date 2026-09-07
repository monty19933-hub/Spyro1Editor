# Neutral fresh-level aggregate smoke

Source-free authoring/schema tests only. The modern aggregate embeds the unchanged
legacy v1 floor intent and an ordered, bounded closed placement list. Editor IDs
are not native owners, save bits, ordinals or source authority. Zero/multiple
placements and neutral Z8909 drafts confer no compiler or publication capability.

Tests-first missing-API RED retained before either new Core file existed (12
missing-newtype errors). Final Release build:1.57s, zero warnings/errors.
Neutral smoke:308 checks/214 refusals, repeated identically from `/private/tmp`.
Existing v1 smoke rebuilt clean and passed243 checks, including83 Store checks;
its separate existing publication read-lease suite passed59 checks. Only those
legacy tests use their own temporary filesystem fixtures.

Coverage includes exact canonical bytes and old v1 preservation; closed immutable
types; one-pass bounded enumeration;128/129 placements; stable unique IDs across
reorder; defensive collection/input/output copies; every changed base field;
XY/world/route/strict-interior boundaries; independent Z grammar; strict unknown,
missing, duplicate and escaped fields; UTF8/UTF16/type/overflow/depth; exact64KiB
and unchanged nested16KiB limits. No Store, migration, UI, source image, compiler,
publisher or runtime implementation/action is added by this new project.

Run `dotnet run --project src/Spyro.Editor.NativeFreshLevelDocumentSmoke -c Release`.
Expected marker: `NATIVE_FRESH_LEVEL_DOCUMENT_SMOKE_OK`.
