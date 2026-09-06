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
ROMs and installed editor files are not touched. No UI checkbox or generated
gameplay behavior is claimed by persistence alone; integration follows native
candidate validation.

The source-free preference smoke has 59 checks: default off, explicit disable,
per-level preservation, deterministic reload, stale snapshots, cancellation,
competing publication, malformed/unknown/duplicated fields, invalid IDs,
directory/symlink refusal and staging cleanup. The combined publication smoke
separately injects failures at every rename stage and retains an actual rollback
conflict, including its structured recovery path. Both tests were executed RED
before implementation and then GREEN without removing earlier publication gates.
