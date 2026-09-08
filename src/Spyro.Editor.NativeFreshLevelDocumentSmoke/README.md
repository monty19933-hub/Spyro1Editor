# Neutral fresh-level aggregate smoke

Source-free authoring/schema/storage tests only. The modern aggregate embeds the unchanged
legacy v1 floor intent and an ordered, bounded closed placement list. Editor IDs
are not native owners, save bits, ordinals or source authority. Zero/multiple
placements and neutral Z8909 drafts confer no compiler or publication capability.

Tests-first missing-API RED retained before either new Core file existed (12
missing-newtype errors). Final Release build:1.57s, zero warnings/errors.
Neutral smoke:308 checks/214 refusals, repeated identically from `/private/tmp`.
Existing v1 smoke rebuilt clean and passed243 checks, including83 Store checks;
its separate existing publication read-lease suite passed59 checks. The later
aggregate Store suite adds112 real-file checks in its own temporary projects;
its separate public missing-API RED is retained before Store implementation.

Coverage includes exact canonical bytes and old v1 preservation; closed immutable
types; one-pass bounded enumeration;128/129 placements; stable unique IDs across
reorder; defensive collection/input/output copies; every changed base field;
XY/world/route/strict-interior boundaries; independent Z grammar; strict unknown,
missing, duplicate and escaped fields; UTF8/UTF16/type/overflow/depth; exact64KiB
and unchanged nested16KiB limits. Storage coverage includes both fixed profiles,
raw compare-and-save, no-op and foreign-owner preservation, actual128-placement
reopen, cancellation and known-commit readback failure. It exercises the shared
document transaction, not ROM publication. No automatic migration, UI, source
image, native compiler or runtime action is added by this test project.

The explicit `ForkLegacyFloor` operation now creates an absent aggregate from
the actual saved legacy floor snapshot without overwriting that floor. Its
248 additional checks cover both compare-and-save guards, raw snapshot changes,
failed stage ownership, source/destination final readbacks, cancellation and
independent cleanup failures. A transaction invocation that does not return is
reported as publication-unconfirmed, never inferred to have aborted from the
bytes on disk. The tests do not claim filesystem crash atomicity or reproduce
an operating-system dispose failure after the last rename. Legacy save and
publication-context gates are rerun separately.

Run `dotnet run --project src/Spyro.Editor.NativeFreshLevelDocumentSmoke -c Release`.
Expected marker: `NATIVE_FRESH_LEVEL_DOCUMENT_SMOKE_OK`.
The separate storage marker is `NATIVE_FRESH_LEVEL_DOCUMENT_STORAGE_OK`.
The explicit fork marker is `NATIVE_FRESH_LEVEL_DOCUMENT_FORK_OK`.
