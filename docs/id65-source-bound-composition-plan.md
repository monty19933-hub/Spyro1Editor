# Source-bound ID65 collision and music composition

Investigation and bounded implementation after `de1e809`, 2026-09-05. The new
source-bound input, composition plan and publisher are implemented. Source-free
composition/input checks, the complete source-backed candidate suite, and fresh
replay of both actual published input files pass.
This is not a gameplay pass, schema migration, or permission to discard native
storage.

## Implemented entry points

- `Id65AuthoredCollisionMusicInput`: explicit new profile around the unchanged
  schema-6 music input, with single-sector or exact collapsed-sky substrate.
- `NativeExactByteComposition`, in `Id65AuthoredCollisionMusicPlan.cs`: validates
  and folds complete sequential byte transactions before native disc mapping.
- `Id65AuthoredCollisionMusicPlan`: source-bound native recompilation, separate
  WAD/executable address spaces, exact logical inverses and whole-disc readback.
- `Id65AuthoredCollisionMusicPublisher`: fresh locked-source copy and shared
  new-only four-artifact publication, strict verified reuse, and explicit
  published-but-unverified retention after a postcommit fault.
- `Spyro.Editor.Id65AuthoredCollisionMusicSmoke`: source-free composition/input
  tests; `--candidates <repository-root>` generates two retained candidates in a
  new isolated workspace; `--verify-retained <existing-fixture-root>` replays
  their actual published inputs and verifies exact reuse without new BINs.

The existing stored-document writer intentionally leaves its writer-lease file
on disk. The smoke's initial one-file authored-directory expectation was
corrected to allow that established lease; no store/guard format was changed.
The paused test workspace is preserved at
`_local/Id65AuthoredCollisionMusicSmoke-6650ff1169ce4d648237ef131492e0cf`.
It contains two complete candidate sets but has no final suite acceptance
marker. The resumed complete run uses the separate new workspace recorded below.

The CUE is captured as a frozen publication companion before native compilation.
Its exact original bytes and source CUE/image paths are rebound before and after
native recapture and artifact verification, immediately before publication, and
through final verification and strict reuse. This includes changes which
normalize to the same exported CUE, such as an equivalent `FILE "./name.bin"`
spelling. Missing or linked source paths are refused; the shared CUE helper's
missing-file fallback cannot silently replace the captured source. The exported
CUE's bytes and hash are also checked with the committed input and receipt.
Neither source CUE spelling nor this freshness snapshot is part of the pure
native BIN plan identity; the existing input, receipt and candidate identities
remain unchanged. Trusted exclusive local paths are still required: these
rechecks do not claim hostile-filesystem or power-loss atomicity.

### Source CUE freshness follow-up

An independent review found that the first implementation could accept a valid
synthetic `PREGAP 00:01:00` change after staging: native plan identity was unchanged
and final verification compared the old captured CUE only. The new production
regression reproduced this against the prior build and reached its deliberate
`BeforePublication` safety fault (exit 134), proving the late change was not
rejected. No candidate was published. The preserved RED test workspace is
`_local/Id65AuthoredCollisionMusicCueSmoke-5e256603b4ad4266bbcc250d3cfa8280`.

`Id65AuthoredCollisionMusicPublisher.SourceCueSnapshot` now binds both exact
original bytes and the source paths, separately from rewritten output bytes.
Twenty source-free checks cover valid PREGAP changes, equivalent FILE spelling,
line-ending changes, a different referenced image, path rebinding, deletion,
source CUE/image symlinks, detached output buffers and exact restoration. The
source-backed `--cue-freshness` mode clones a retained test workspace, never
mutates the original fixture, and exercises real precommit and reuse boundaries.
Its copied existing candidate set is used only for strict reuse verification;
every native plan is still compiled from the locked source, never that candidate.

An independent copied-Core replay of the original PREGAP experiment also passed
after the fix: it threw a source-CUE `InvalidDataException` at the late freshness
check, before its deliberate publication fault. The output directory was empty
after cleanup and the original retained fixture's CUE remained byte-identical.
The independent preserved harnesses are `/tmp/spyro-composition-cue-review.H1THRY`
(RED, old Core) and `/tmp/spyro-composition-cue-green.JKaKuI` (GREEN, fixed Core).
The static review of all snapshot/recheck boundaries found no further issue.

The fixed full production-boundary run completed successfully:
`ID65_AUTHORED_COLLISION_MUSIC_CUE_FRESHNESS_OK checks=18 rejected=6 precommit=3
reuse=3 rollback=1 exactCueRestored=1 retainedCandidatesUnchanged=1 runtime=false`.
Valid PREGAP, deleted CUE and equivalent FILE rebinding were each rejected after
staged readback and after the initial exact-reuse readback. Precommit refusals
left empty output directories; reuse refusals preserved all four copied artifact
hashes and left no residue. Restoring the exact source CUE allowed successful
strict reuse. Both original source files and the original retained candidate set
remained unchanged. No newly accepted candidate image was published by this
follow-up. Machine-readable evidence is
`_local/Id65AuthoredCollisionMusicCueSmoke-92ad3651bbb34004a46d83441e5fa3bf/cue-freshness.evidence.json`.

```text
dotnet build src/Spyro.Editor.Id65AuthoredCollisionMusicSmoke/Spyro.Editor.Id65AuthoredCollisionMusicSmoke.csproj -c Debug --no-restore
dotnet src/Spyro.Editor.Id65AuthoredCollisionMusicSmoke/bin/Debug/net10.0/Spyro.Editor.Id65AuthoredCollisionMusicSmoke.dll
dotnet src/Spyro.Editor.Id65AuthoredCollisionMusicSmoke/bin/Debug/net10.0/Spyro.Editor.Id65AuthoredCollisionMusicSmoke.dll --cue-freshness /Users/john/Documents/Codex/2026-07-13/i-needed-to-make-a-new/spyro1editor-v3-release/_local/Id65AuthoredCollisionMusicSmoke-c6f728b6596544d6bd2a050c7ce27a82
```

The fixed Debug build completed with zero warnings/errors; source-free output
reported 23 composition, 19 canonical-input and 20 CUE-snapshot checks. The
reviewed publisher source SHA-256 was
`6cfe9077dccd96ce78a316bf915a562613b55ea63d9d852b78d445f078a0d380`.
No Release/App build, installed editor, emulator or runtime acceptance was part
of this fix.

## Completed source-backed acceptance

The resumed run passed 23 source-free byte-composition checks, 19 canonical-input
checks, and the complete source-backed suite: **35 checks, 34 expected refusals,
five edit states and two retained immutable candidates**. Both Debug builds
succeeded with zero warnings/errors. The initial RED scaffold failed at the
unimplemented sequential composer before its implementation was added.

Evidence report:
`_local/Id65AuthoredCollisionMusicSmoke-c6f728b6596544d6bd2a050c7ce27a82/evidence.json`.

Candidate root beneath that fixture:
`workspace/_research/unusedlevel65blank/v1/materialized-source-bound-collision-music-candidates`.
Each directory contains `id65-authored-collision-music.bin`, the matching
`id65-authored-collision-music.cue`, `authored-input.json`, and
`candidate.receipt.json`. The CUE is the emulator entry point; no gameplay gate
has been run for these outputs by this backend subtask.

| Candidate | A | B |
| --- | --- | --- |
| Intent | Tile1, Grass T107 at Center, Track 2 | Both tiles, Grass T107 at `remote-pad-grid.x368-y336`, Track 26 |
| Directory | `composition-9d0e14f31fd0a24690f3a431e58e977b2f699cfc549028700376da11374ff669` | `composition-403eea3e920db404fe9d96db43325884d70e82b263caf54b83e4e09524456df1` |
| BIN SHA-256 | `b071d5573953526eeaf973927a8c98476262ac9c09ce6d3bacda4b070c3309e0` | `36d759e18c15717eb8e4f51544fd2aeb46dfede27957ea4fe1e03887af3e2a6e` |
| Output row80 SHA-256 | `5ecbd0bc8be65ee3b558d08a27d3a2e7f6db7833452bd368e249be4af12deeee` | `daea1943e5d751e1df524b907c9ebacf513c37e198495e74c942eef078daf16c` |
| Changed physical bytes | 1,055,885 | 1,056,489 |
| Owned/changed raw sectors | 559 / 559 | 559 / 559 |
| Reachable collision cells/triangles | 1 / 2 | 1 / 3 |

The A-B-C-D-A edit sequence covered both tiles/Grass/noncenter, no optional
tiles/None, Tile2/Red Gem/Center, then exact restoration of A. The restored input,
plan and native outputs were byte-identical and the prior A artifact set was
reused. The exact collapsed-sky branch retained its independent existing row80
golden `9e9b8064516684eebbd86012621d20ceab60e39bfa7711ff427bda71da952fb1`;
an unsupported collapsed document was refused.

Negative cases cover four precommit fault points, competing output preservation,
an actual canonical-document edit before commit, stale earlier intent after a
document edit, cancellation, raw payload/header/subheader/EDC/ECC corruption,
matching corruption of source and output, initial and late source/output length
changes, and logical inverse corruption. A postcommit fault retained B; a fresh
exact four-artifact reuse verified it. Independent `cmp` calls also established
that both resumed BINs are byte-identical to the two preserved pre-pause BINs.
No earlier candidate set was changed to produce this evidence.

Commands from the repository root:

```text
dotnet build src/Spyro.Editor.Id65AuthoredCollisionMusicSmoke/Spyro.Editor.Id65AuthoredCollisionMusicSmoke.csproj -c Debug --no-restore
dotnet src/Spyro.Editor.Id65AuthoredCollisionMusicSmoke/bin/Debug/net10.0/Spyro.Editor.Id65AuthoredCollisionMusicSmoke.dll --candidates /Users/john/Documents/Codex/2026-07-13/i-needed-to-make-a-new/spyro1editor-v3-release
dotnet src/Spyro.Editor.Id65AuthoredCollisionMusicSmoke/bin/Debug/net10.0/Spyro.Editor.Id65AuthoredCollisionMusicSmoke.dll --verify-retained /Users/john/Documents/Codex/2026-07-13/i-needed-to-make-a-new/spyro1editor-v3-release/_local/Id65AuthoredCollisionMusicSmoke-c6f728b6596544d6bd2a050c7ce27a82
```

All three commands completed successfully. The final, separately rebuilt replay
reported `ID65_AUTHORED_COLLISION_MUSIC_RETAINED_OK candidates=2
publishedInputReplay=1 exactReuse=1 sourceAndDocumentRestored=1 runtime=false`.
It reopened each actual published input file, reproduced that file's exact
canonical stored envelope, and verified strict four-artifact reuse through the
production publisher. It restored the test workspace's prior canonical document
after the two replays and did not regenerate, patch or replace a candidate BIN.
Both BIN digests remained the values in the table above.

## Smallest useful milestone

Compile a new collision-isolated, single-sector authored level and its music
directly from the locked source plus canonical authored intent. Export a new
immutable BIN/input/receipt/CUE set, reload its canonical input, change terrain
and music, then export again from that same locked source. Reverting the intent
must reproduce the original complete output bytes. An existing candidate BIN
must never be the editable input to this route.

Keep the current `Id65AuthoringEditorDocument` schema 6 and all six historical
document goldens unchanged. Keep the current music, collision, and collapsed-sky
candidate formats and their identities unchanged. The names containing
`schema7` in compiler profiles are stage identifiers: both single-sector and
collapsed-sky compilers still require a schema-6 document and expose compiler
`SchemaVersion = 1`. They are not a durable schema-7 authoring document.

The repeated geometry fixture should use the general single-sector stage with
collision isolation. `Id65AuthoringCollapsedCycloramaGeometryProbeCompiler`
accepts only Tile 1 / Grass T107 / Center and binds its complete row80 hash;
it cannot be enabled for arbitrary re-edits without additional work. Its exact
collapsed-sky state remains a separate compatibility fixture.

## Existing compiler contracts and the missing join

| Producer | Exact input and result | Composition consequence |
| --- | --- | --- |
| `Id65AuthoringSingleSectorSubstrateCompiler.Compile` | `Id65AuthoringSingleSectorSubstrateCompileRequest` containing the schema-6 document, locked source, V3 terrain template, T66 witness, and Grass bundle. Returns full source/output row80, direct `OwnedRanges`, inverse, and `StageOwnedRanges`. | Already rebuilds schema6 terrain/texture/Moby/sky intent before reducing environment to physical sector 0. Its direct ledger is the first source-bound WAD transaction. |
| `Id65AuthoringCollisionIsolationCompiler.Compile` | Same request; optional exact collapsed-sky branch. Returns successor baseline/output row80 and only the collision retirement `OwnedRanges`. | Bind its baseline to the preceding exact compiled output. Do not interpret its ledger as locked-source preimages. |
| `Id65BuiltInMusicSelectionCompiler.Compile` | `Id65MusicSourceCertificate.FromFrozenExecutable` plus the complete frozen executable and selectable track ID. Returns five executable-relative windows, full output and inverse. | Independent executable domain; no WAD offsets, source-level model pointers, or music-stream copying. |
| `Id65AuthoringMusicDiscPlan.Compile` | `Id65AuthoringMusicBuildInput` and `Id65AuthoringBlankCycloramaAggregatePlanRequest`. Returns typed disc writes and whole-image readback. | Hard-codes the old schema-6 aggregate output and inverse. It cannot certify collision-isolated output merely by changing a hash. |
| `Id65CollisionCandidatePublisher` | Exact compiled collapsed/Tile1/Grass/Center result and parent BIN `65d70f...c232`. | Publication is deliberately tied to one existing parent. Leave it intact; the new route starts from the locked source. |
| `Id65AuthoringMusicCandidateWriter` | Exact live stored document, music envelope, native inputs, and existing schema-6 materializer. | Copies a baseline candidate before applying music. Leave this historical route intact; do not wrap it in another candidate-to-candidate patch. |

`NativeRelocationMap` supplies validated address geometry and reference encoding;
it deliberately does not discover byte ownership, hash input, move bytes, or
compose overlapping sequential writes. A new reusable exact-byte composition
leaf should therefore remain separate from relocation geometry.

That leaf needs to validate every stage against its complete expected preimage,
apply declared ranges, prove each protected complement and exact inverse, then
fold the sequence into disjoint original-source/final-output ranges. Sequential
overlap is valid only when the later preimage equals the earlier afterimage.
Do not concatenate overlapping ledgers or deduplicate mutable ranges by payload.
The fold must account for an earlier change restored to its original value, a
partially overwritten range, adjacent ranges, and multiple writes in one raw
sector. Final changed bytes must be covered by the validated stage ownership
union; a diff alone does not confer ownership. Retain stage IDs and source,
intermediate, final and inverse hashes in the plan evidence.

Use the folded WAD80 transaction and the independent executable transaction as
separate `NativeAddressSpace` instances. Map them into one final `DiscLogical`
space using existing `NativeRelocationMap`; reject overlapping final extents.
Reconstruct each affected MODE2/Form1 raw sector once from the locked source,
including all writes touching that sector, then rebuild and verify EDC/ECC.
Verify every other physical byte against the complete independently hashed
locked image. The first implementation has fixed native capacities and no disc
growth; a general relocation API does not imply that this level can grow.

## Canonical save and re-edit boundary

`Id65AuthoringMusicBuildInput` already embeds the unchanged complete document and
track ID and binds the exact stored-document envelope. Parsing is canonical and
rejects paths, raw native bytes, caller addresses, unsupported properties and
stale document/envelope identities. It can be reused as nested intent in a new
compiled profile without modifying old serialized bytes.

For the first backend fixture, save each document through the existing
`Id65AuthoringEditorDocumentStore`, explicitly create a fresh music input for
that returned envelope, and serialize/reload the complete combined input before
compiling. Re-editing is A -> new document B -> new combined input B, not an old
music input silently following B. Publication should retain that complete input
as one immutable artifact with its BIN, receipt, and CUE.

This establishes repeatable canonical-input/export/reload, not a coherent live
editable music project. The current authored-edits guard allows only the existing
document and its writer lease. Do not add an independently editable music sidecar
there or loosen that guard. A later project-persistence milestone needs one
versioned record containing the authored document, music intent and explicit
substrate profile, with one save/recovery boundary and a deliberate import from
the old envelope. If introduced now, implement and test that single-record
boundary before exposing controls; the receipt is evidence, not editable intent.

## Native substrate and protected inheritance

The next combined output still relies on the locked source. Record this census
in its receipt rather than calling it an independent minimal native level.

| Structure | Present authored behavior | Storage that must remain protected |
| --- | --- | --- |
| Spawn and player | Native landing record and resident player anchor T92 authored to the remote support area, fixed yaw `0x40`. | Player native resources, initialization, save, totals and exit paths remain source-bound. The compiler does not synthesize a new player/level executable. |
| Environment | One physical sector 0, mandatory base support face and zero to two optional authored tiles; HP and LP records and stable handles are rebuilt together. | Fixed environment capacity is retained; its unused gap is zeroed, not reclaimed for arbitrary assets. Component topology remains validated. |
| Collision | Lookup isolation retains only cell `(1,1,2)` and base triangle 13995 plus optional authored tile triangles. | Full native triangle rows, assignments, flags, tree capacity and block capacity survive. Exact Tile1 candidate has 19,809 rows but only two reachable triangles. Non-lookup consumers are not proven absent. |
| Visibility | Environment occlusion references physical sector 0. | The `0x974` component and its `0x254` opaque tail at `0x720` remain; the sky tail has not become disposable. |
| Sky | General compiler recolors the inherited geometry to `#263447`; exact collapsed probe changes only vertex words. | 35-part, 1,915-vertex, 726-color, 2,808-polygon inherited structure remains. Collapse is transitional, not an original sky schema. |
| Textures | Source-derived pinned private T66 witness and selected compiled tile material mappings. | Existing native texture resources, layout and VRAM relationships remain source-certified; material labels do not grant new texture ownership. |
| Mobys | None / appended Grass T107 / resident Red Gem T21 placement; Grass installs the certified bundle and fixups. | Source object rows/resources remain. The locked object table has 107 rows; Grass appends one. Red Gem owns only its 12 coordinate bytes. Overlay, global executable, sounds/particles, links, totals/persistence and associated data dependencies remain explicitly certified or refused. |
| Other model components | Existing component ordering and lengths are checked. | Special-surface component, empty four-byte portal table, particles, sound, fixed model tail and unrelated scene storage are preserved. |
| Music | Four fixed ID65 late-selection bridge windows plus slot35 track word select one of the 46 built-in tracks. | The PS-X executable load range, unrelated code, retail mapping behavior, late table, PETEXA LBA table and audio stream sectors remain exact. No new audio asset is inserted. |
| Disc | Fixed WAD.WAD, row80 and executable placement, complete physical complement and sector parity. | ISO metadata and unrelated levels/files remain unchanged; source and installed editor are never export destinations. |

## Source-bound fixtures and regression order

Retained source fixture, confirmed present on this investigation:
`_local/id65-authoring-aggregate-workspace/_research/unusedlevel65blank/v1/locked-base/unusedlevel65blank-locked-base-v1.bin`
with its adjacent CUE. Size is 661,547,040 bytes. The existing certificates and
retained receipts bind it to
`9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8`.
The resumed implementation verifies this complete source hash at capture and
final readback, and the completed suite's final source/copy/CUE preservation
guard passed. Do not substitute the original retail BIN for this locked
fixture or assume old `/tmp` outputs survived restart.

1. Add source-free exact-byte composition regressions first: sequential partial
   overlap, independent file domains with equal numeric offsets, later restores,
   gaps, adjacent writes, invalid source/final lengths, stale intermediate input,
   undeclared byte changes, identity mismatches and inverse corruption. Reuse
   existing relocation tests; do not duplicate their entire established audit.
2. Prove old document/input/plan outputs unchanged with the existing focused
   goldens. New composition tests compare their own WAD output to the existing
   collision compiler, and their EXE output to the existing music compiler,
   rather than accepting a newly self-generated expected hash alone.
3. Start source-aware A = Tile1 / Grass / Center / Track 2. Reload canonical
   input and recompile A; require identical plan identity, complete row80 and
   EXE bytes, full inverse, and one reachable collision cell. A's music EXE
   must equal retained hash
   `39e53a1f21c4c75346a670c4ffea26cdab95498fc67b9e798284ba7dd8adfba2`.
4. Re-edit A through both tiles / Grass / an existing noncenter catalog position
   / Track 26, then no optional tiles / None / Track 2, then Tile2 / Red Gem /
   Center / Track 26, and finally restore A. This covers terrain component
   growth within fixed capacity, object append/remove/resident-coordinate
   branches and simultaneous geometry/music changes. Track 26 EXE must equal
   `30d7721b6b46b9753827ddee249bc2591dc9fc1b5a76db71a4ebffe146cc9f8c`.
   Every document revision needs a new exact envelope-bound music input; old
   inputs must fail against the changed stored document.
5. Keep the exact collapsed-sky compatibility case separate. Its collision
   output row80 must remain
   `9e9b8064516684eebbd86012621d20ceab60e39bfa7711ff427bda71da952fb1`,
   matching the retained collision candidate's receipt. Combining Track 2 or
   26 changes only the independently certified EXE relative to that logical
   row80. The old full BIN hash is not an expected combined BIN hash.
6. Materialize A and one changed state from the locked source into a new test
   root through `ExportArtifactTransaction.PublishNew`, CUE last. Validate the
   complete BIN and all three companion artifacts, exact repeat/reuse, stale
   live input, competing partial/full output, cancellation, precommit failures
   and published-but-unverified retention. Fault injection uses disposable
   candidates or corrupt read-only streams, never original or retained files.
7. Read-only whole-image corruptions must cover WAD payload, EXE payload,
   protected complement, raw headers/subheaders, EDC/ECC, initial/late length
   changes and matching source/output corruption. Refresh source binding before
   commit/reuse. Published receipts must keep `runtimeProven=false` until the
   exact output receives separate emulator evidence.

This fixture sequence adds a meaningful composition/re-edit proof without
rerunning the 108-state collision census times all 46 music tracks. Broaden it
only if a new coupling is found. Gameplay remains a distinct gate: support,
jump/landing, camera visibility, falls/respawn, selected music at initial and
late selection, and preservation of unrelated content on the exact new CUE.

Suggested ownership for an implementation: one new shared exact-byte composer,
one new fixed-profile ID65 isolated-music disc plan, one new candidate tool or
writer, and focused source-free/source-aware fixture files. Leave old producers,
old writers, Core/App build outputs, UI and emulator work outside this bounded
subtask unless the parent assigns them explicitly.
