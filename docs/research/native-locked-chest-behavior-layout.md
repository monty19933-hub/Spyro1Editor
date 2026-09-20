# Shared locked-chest behavior layout: preserved Town compatibility

September 19, 2026. This is a bounded extraction, not new Fresh-level admission.
`NativeLockedChestBehaviorLayout` replaces the relocation/assembly hidden inside
`TownSquareNativeLockedChestCandidateExporter.BuildHandlerPayload`. The old
wrapper signature, read order, hash-guard order and final payload guard remain.

The source snapshot retains the exact setup, state and reward blocks. The neutral
relocator records each direct transfer's original/final PC, target, words and
available delay slot. It snapshots bounded segments before caller iteration can
mutate them, validates explicit external mappings, and provides a complete
afterimage-to-source inverse. Unmapped JALs remain explicitly **retained but
unproven**, as in the historical producer. A missing delay slot is not invented.
This compatibility kernel is not a general behavior linker or an executable,
global-state, control-flow, runtime or destination certificate.

The Town layout uses the shared owned-overlay materializer for six exhaustive
blocks and nine declared entries. Its historical `F494` arena overwrites part
of the original `F800` allocation, so it is deliberately not represented as an
append. Future Fresh integration must preserve the complete `11000` host and
append after it; Town's old offset is not free space there.

The chest adapters only move `s4=s3` and allocate no private stack frame. They
return to Town loop `80087258`, with explicit reset/delete/sound continuations.
They cannot use the Wizard continuation that restores a private `2B0` frame.

## Verification

Evidence: `_local/research/v6-mixed-export-20260912-r01.jR0oBr/`.
Tests preceded the consumer switch: RED400 recorded 37 expected missing-type/
friend errors and zero warnings. GREEN404 builds without warnings or errors.
Free405 passes 106 checks: independent literal instructions/afterimage, branch
endpoints, transfer/delay ownership, complete inverse, malformed/unused targets,
mutation/disposal/enumeration bounds and historical read-failure ordering.
Source406 passes 28 focused checks through the existing read-only source factory.
No candidate image or runtime session was created.

Frozen identities remain:

- Complete `BC0` payload: `08f4372710b7162982397e247b75ed9b00cc5290ef9630d4e2fdd320611012b9`
- Town overlay: `fd859bff13985e18c3aff74cc889006c48b2743bf57a2b0f9d80662d25a09c2d`
- Town executable: `4268f9943233b9f8c24d1d4b0924cba8747547b44b82eddd13f0a2ac10ac17de`
- Dependency graph: `d484fc13504ac0afc0b7a63e81dd4a6a3a3a7672dd7959654b02fd6103a3edcc`

Source tests also compare all three inverse blocks to their actual captured
source bytes, pin dispatch/thunk words and arguments, and probe owner edges,
transfer/delay words, unchanged complements, truncation and extra bytes. These
focused probes supplement full-byte equality instead of inflating counts with
one nearly identical test per payload byte.

Fresh admission, per-instance seven-row/property rebasing, singleton globals,
current executable consumer domains, extended initializer/model addresses,
reward census and coexistence/runtime testing remain pending. Existing Town
runtime evidence applies only to its historical exact candidate.

## Independent continuation witness before Fresh integration

Original-only inspections417/421 established the actual words before scalar
tests were written. The historical overlapping40-byte reset guard was not a
complete block: it ended at a pending byte load. The actual reset entry is4C
bytes through its jump and delay store. Town and donor reset blocks are **not
byte-identical**: eight row operands use s3 versus s4, and the final jump owns
a different loop. Equivalence requires the explicit row mapping s4=s3.

`src/Spyro.Editor.NativeFreshWizardHostSmoke/source_fresh_chest_continuation_oracle.py`
SHA256 `4f407c80de5ae78642f94bf727dd113384ba3ad5baa62da308e9b4407a946b8a`
is a new original-only script; existing oracles/pins are unchanged. Run it with
`python3 -B <script> <original.bin> --witness` (`--inspect` remains available).
Root execution436 passed69 checks/four explicit refusals. Whole original image,
Town/Peace overlays, original executable, WAD entries and pinned transitive
helper sources are checked before/finally, including a final named-image reopen.
It invokes no Core producer and writes no image or overlay.

The bounded scalar cases execute all eight declared continuation sites, their
actual branch conditions/delay words, Town's next-row and null-list paths, and
complete outer-frame restoration. They verify seven reset byte writes, four
owned reads and the full unchanged register/memory complement at animation
bytes0/7/255. Synthetic model/table/animation memory is explicit, not evidence
that an actual rootless class has those owners. Unequal row-register and cursor
controls expose the difference rather than hiding it: Town uses sp+180 and a
1D0 outer frame; the donor loop reads sp+1E8. A Wizard2B0 private-frame restore,
missing model, missing terminal delay and cancelled animation-pointer load fail.

Delete and sound witnesses stop at the original EXE entry after executing real
argument and JAL delay words. Separate post-call-entry tests do not claim helper
execution. Original EXE consumer windows are pinned, but matching them against
the actual current Fresh executable is still required. Test-only thunk PCs are
not a proposed or admitted Fresh allocation. Whole-handler reachability,
rootless-class model/animation validity, aliased scratch, callbacks, singleton
state and native helper semantics remain unresolved. No Fresh placement, Body,
executable, disc or UI admission is widened by this witness.

## Original reset-precondition inventory441

`source_fresh_chest_reset_preconditions_oracle.py` is a separate inspection-only
oracle, SHA256 `8fda30564e5d1ad8f8262a77375dc6bbdc0b846be137ce842e5b90b4d4def6c8`.
Root441 passed with the original-only `--inspect` mode and final held/named image
and transitive helper identity checks. The passed436 oracle remains unchanged.
This reports the entire434-byte reward handler, every direct/computed control
and owned delay word, all seven58-byte source rows, their complete privateE4,
integer owner fields versus pointer-fixup fields, and all54 PETE registrations.

ClassD is absent from both the actual PETE registration table and Peace's local
model registrations. AD is locally absent but is present in PETE slot10: its
21C-byte package is a SimpleModel, not an animated reset +38 pointer-chain proof.
53/54 are also shared SimpleModels; AE is the local1008-byte animated package.
This corrects any inference from an empty *local* slot to global model absence.
All five retained D private blocks start with +14=0 and +18=1, but initialization
and all reachable writes have not yet been executed as a state invariant.

The exact reward reset transfers remain E708 (conditional) and E774 (jump).
The new bounded shared-loader inspection discovers B8AC/C bytes through
B8B0's conditional loop to B870 and delay s0+=8, then B8B8/8 through the call
to B6F8 and its delay word. It does not follow/execute the new call, presume its
return, certify a whole function tail, or compare the current Fresh executable.
No rootless-state admission, host extension, ROM or gameplay result follows from
this inventory; it is the source evidence for the next independent state proof.
