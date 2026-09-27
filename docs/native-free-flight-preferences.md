# Persisted Free Flight intent

`NativeFreeFlightSettingsStore` owns only
`<workspace>/native-free-flight-settings-v1.json`. This is user intent, never
native resource data, executable bytes or export/runtime approval.

- Missing document means normal retail behavior with no explicit choice.
- Saving `false` retains the level row. An export coordinator must rebuild an
  earlier enabled output from the original source even when the desired final
  executable has zero flight patches; it must not take the old no-edits early
  return and leave flight enabled in an existing BIN.
- Choices are independent by native level ID. The initial policy accepts the
  30 normal retail identities, not flight courses or independently owned ID65.
  Settings can retain multiple choices, but the current executable producer
  targets one extra level: an export coordinator must reject conflicting enabled
  targets explicitly rather than applying patches sequentially or choosing one.
- Exact file hashes and an exclusive cooperating-writer lease reject stale
  captures, including stale absence. Deterministic writes preserve other levels'
  choices. The schema is bounded, rejects duplicate/missing/unknown fields and
  refuses corruption instead of silently resetting to defaults.
- Same-volume staged publication uses the existing artifact transaction,
  preserving the previous document on a recoverable failure. Cancellation is
  checked before its short noncancellable commit. A typed
  `ExportArtifactRollbackException` preserves its recovery directory, journal,
  lock and stages if rollback itself fails; callers need not parse error text.

Like the canonical authoring store, this is a trusted-workspace/cooperating-writer
boundary, not a hostile filesystem race or power-loss atomicity claim. Source
ROMs and installed editor files are not touched. Persistence alone does not
prove generated gameplay behavior.

## Current editor and export integration

The Level tab's existing **Fly Anywhere Enabled** checkbox starts unchecked.
Its details distinguish a never-saved `default: Off`, explicit `saved: Off` and
`saved: On`, and an unsaved draft. Save Flight persists a project export choice;
Create BIN consumes that saved choice. Closing and reopening the editor reloads
the saved choice, including explicit OFF's requirement to rebuild the original
image. A discarded unsaved draft cannot replace it.

Normal Create BIN supports ON for Toasty14 with no ordinary saved edits. ON uses
the destination-scoped native permission/entry patch and imported root0 flight
animations; it makes flight available after level entry or reset. Spyro still
enters flight with a jump and airborne Cross. It does not request an airborne
spawn or change the editor's Free Camera. OFF rebuilds from the complete original
disc and preserves ordinary retail behavior, including existing retail flight.
Unsupported enabled destinations, multiple enabled targets, compound ON edits
and independent ID65 remain refused.

This option is stored in the editor project and compiled into the exported game,
not added as a memory-card ability flag. Actual game save/reload with the normal
ON and OFF exports still needs native verification. Earlier R02 native entry,
respawn and portal-reentry observations are useful bounded evidence, but do not
close that memory-card/load gate. Broader starting-flight availability remains
pending each destination's animation/effect/resource, memory-fit and native
entry/reset/save-reload evidence. See
[the focused starting-choice record](research/native-free-flight-starting-choice-20260927.md).

The original source-free preference smoke had 59 checks: default off, explicit disable,
per-level preservation, deterministic reload, stale snapshots, cancellation,
competing publication, malformed/unknown/duplicated fields, invalid IDs,
directory/symlink refusal and staging cleanup. The combined publication smoke
separately injects failures at every rename stage and retains an actual rollback
conflict, including its structured recovery path. Both tests were executed RED
before implementation and then GREEN without removing earlier publication gates.
