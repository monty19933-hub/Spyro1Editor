# Normal Create BIN publication and recovery

Normal **Create BIN**, for either one edited level or several, composes its
writers in a new `_combined-build/run-<id>` directory beneath the selected
output directory. It prepares the BIN, CUE, combined summary, and optional
skipped-edits report before replacing the previous All Saved Edits output.
If no patches are ready or an upstream writer fails, the previous output stays
unchanged. The status message identifies that no new test was published.

`ExportArtifactTransaction` publishes that set using same-volume file renames.
It validates every staged file and rejects duplicate destinations, overlapping
source/final paths, directories used as files, and symbolic-link paths. A
publication lock prevents two cooperating editor processes from committing to
the same output directory concurrently. Old diagnostics are removed only with
the successful new publication; the new CUE is installed last.

After publication, diagnostics and opening the output folder are best-effort.
An artifact-enumeration or support-ZIP error leaves the published BIN/CUE
untouched and reports success with a diagnostics warning. Build Safety evidence
comes from the report captured by this Create BIN invocation, serialized into
its unique run directory. Name/music/sky-only builds do not attach historical
fixed-prefix Build Safety reports. Those reusable UI reports may remain in the
output folder, but are not evidence for the new build. The support ZIP includes
the current JSON snapshot when inspection ran; its Markdown companion remains
in the run directory under the existing JSON/TXT/LOG-only bundle policy.

The previous files are moved into a unique `.export-recovery-<id>` directory
before any new final file is installed. A flushed `recovery.json` records the
original destination, staged path, backup path, and whether an original existed
for each artifact. On an ordinary exception the publisher moves new artifacts
back to staging and restores the previous files. Cancellation is checked before
the short commit phase; the rename phase itself is noncancellable. The current
Create BIN UI does not expose an in-progress cancellation button.

Successful publication or successful rollback removes the recovery directory
and publication lock when possible. If rollback encounters another file at a
path it needs, it preserves that file and all recoverable originals, retains
the lock, and reports the exact recovery directory in the error. If cleanup
cannot remove the lock, its recovery journal is retained as well.

This is exception rollback, **not a claim of crash-atomic multi-file output**.
After a process interruption or power loss, the output directory may contain a
partial new set alongside the recovery directory. The surviving
`.export-publication.lock` contains the recovery directory path and stops
another publication until recovery has been reviewed.

For recovery, close editor processes using that output directory and preserve
copies of the current final artifacts, the entire recovery directory, and its
referenced staging directory. Use `recovery.json` to associate each
`previous-NNN` file with its original destination. An existing `previous-NNN`
is the displaced original; a missing backup alone does not prove that a final
file is old or new. Resolve that against the journal and staged files before
restoring the prior artifact set. Remove the lock only after the chosen BIN,
CUE, and companion reports are consistent. Recovery is deliberately explicit;
the editor never guesses that a lock is stale and deletes recoverable files.

This coordinator covers normal All Saved Edits output, including private
texture exporters used as intermediate steps. Dedicated research exporters
(ID65 candidates, standalone sky/UI-text tests, and other disposable probes)
retain their own publication contracts.

The source-free `Spyro.Editor.CombinedExportPublicationSmoke` verifies the
actual no-patch, upstream-failure, and successful synthetic-music coordinator
paths, failures at all nine
prepare/rename checkpoints, first-publication rollback, cancellation, success,
invalid paths, and a rollback conflict that retains the original BIN and
recovery instructions. It also verifies current safety-report provenance in
the support ZIP and preserves the successful outcome when the workspace
disappears before diagnostic artifact enumeration. These checks do not
constitute an in-game test.
