# Fresh LAB research publication regressions

Implemented after the reviewed missing-API RED. No ordinary exporter, installed app
or native runtime is changed. Existing FreshDisc plans, source bindings, goldens and
receipts remain untouched.

## Focused evidence, 2026-09-06

- Intentional RED: Release build 4.98s, zero warnings, exactly one missing
  `NativeId65FreshDiscResearchCandidatePlan` error.
- A separate source-backed RED caught a third CUE FILE alias being retargeted
  while the supplied BIN/CUE paths and CUE bytes stayed unchanged. Freshness now
  revalidates that FILE relation using the same bounded captured snapshot.
- Final Release build: 4.94s, zero warnings/errors.
- Source-free: 60 checks. Exact source: 68 checks, 22.43s.
- Explicit forced staging: 33 checks, 101 including source/free, 46.75s;
  `published=false`. A complete staged image was independently reopened as the
  exact known full hash and 1253 changed sectors. Every publication attempt was
  forced to stop before commit.
- The staging workspace retains only its tiny private CUE, occupied-file fixture
  and intentional 29-byte foreign stage owner with its retained writer lease.
  No staged BIN, receipt or final candidate remains.
- Successful publication R02: 20 checks, 80 including source-free. The final
  exact three-file candidate was reopened against a newly captured source chain.
  Duplicate publication refused without changing it. Separate private copies
  exercised missing/extra/changed/linked companions and untouched Form1 P-parity
  corruption at LBA38 +2076, then restored and reverified both copies.
- A separate process using `--verify` recaptured the source and verified the R02
  BIN/CUE/receipt. The result has 1253 changed raw sectors, no retained recovery,
  and still-false runtime, normal-export and complete-dependency approvals.

Durable logs are under
`_local/research/native-id65-engine-profile-20260906-r01/`:
`fresh-disc-research-red-build.log`,
`fresh-disc-research-alias-red-{build,source}.log`, and
`fresh-disc-research-final-{build,free,source,staging}.log`.
The exact disposable workspace is `fresh-disc-research-staging-r01` below that
evidence directory. Earlier initial-green logs are retained unchanged.
Publication and independent-reopen evidence is in
`fresh-disc-research-publication-r02{,-build}.log` and
`fresh-disc-research-independent-verify-r02.log`. The retained candidate is
`_local/native-fresh-id65-runtime-20260906-r01.LbsgK0/publication-r02/candidate/`.
Earlier R01 tested untouched Form2 payload corruption, not parity; that log and
candidate remain separate and unchanged. R02 corrects the test's parity coverage.

## Modes

- No arguments: source-free bounds, CUE parser parity, immutable API shape and
  actual stream EOF/cancellation tests. Tiny path-resolution CUE fixtures are not
  original-ROM authority.
- `--source BIN CUE`: original-source read-only capture of actual source-issued
  A/N16/LAB/track19, deterministic repeat and actual issued B/N16, A/N1 and track20
  refusals. It also creates and removes a tiny temporary CUE/alias/different-file
  fixture to test alias retargeting. It does not create a candidate BIN or receipt
  and never modifies the supplied original BIN/CUE.
- `--stage-faults BIN CUE absent-workspace`: explicitly creates disposable staged
  images and a tiny private CUE. Every attempted publication uses the internal
  forced-fault API and stops before the commit. Complete staged output is reopened
  and verified; no final candidate is authorized by this mode.
- `--exercise-publication BIN CUE absent-workspace`: separately approved explicit
  output exercise. Creates the exact candidate and a separate private copy for
  verification negatives; recaptures the full typed source chain before reopening.
  This mode was added and successfully exercised after forced staging passed.
  It is never run by the default/source gate.
- `--verify BIN CUE existing-candidate-directory`: recaptures the exact source
  plan and verifies an existing complete set without writing to it. It does not
  run the source-variant/alias regression. As with all modes, the initial neutral
  source-free checks use their own tiny temporary CUE fixtures.
- No generic publish CLI or ordinary Create BIN approval is exposed. Root owns
  the separately authorized explicit output invocation and native runtime gates.

## Reviewed implementation boundary

`NativeId65FreshDiscResearchCandidatePlan` is sealed with a private constructor.
Its only public capture accepts original BIN path, source CUE path, the actual
privately issued `NativeId65FreshDiscPlan`, and a cancellation token. Tests use
immutable metadata, internal CUE/receipt copies, `RequireFreshAsync`, and these
neutral helpers used by the real implementation:

```csharp
internal static byte[] ReadCueSnapshot(Stream input, CancellationToken token);
internal static byte[] CompileCueForSmoke(
    ReadOnlySpan<byte> bytes, string cuePath, string originalBin);
```

The bounded reader must reject a nonzero initial position, invalid length before
allocation/read, actual extra EOF, final position/length changes, and cancellation
before and after the bounded read and EOF probe. It must be the implementation
used by capture/freshness, not a separate test-only reader. Neutral bytes cannot
construct the source-issued plan. Receipt construction rejects a claimed hash or
count inconsistent with the exact supported fixture; actual publication still
requires a fresh physical readback and never trusts a caller-provided receipt.

`NativeId65FreshDiscResearchCandidatePublisher` exposes reviewed `PublishAsync`
and read-only `VerifyAsync`. Its internal `RequireCompanion(Stream, expected,
token)` similarly checks exact bounded bytes and true EOF. The exact test friend
is scoped to this smoke assembly; no broad reflection or public byte
authority is required.

Internal `PublishForSmokeAsync(plan, outputDirectory, fault, callback?, token)`
uses `AfterFirstSector`, `AfterImageWrite`, or `BeforePublication`. Callbacks report
the actual checkpoint and staged image path. At BeforePublication the callback
runs before the final source/physical/companion checks; the deliberate exception
runs only after those checks. Consequently late PREGAP/deletion/rebinding and
cancellation must fail at their real guards, while a missing guard still cannot
publish. All path/occupied negatives also carry this forced precommit fault.
The scope excludes a generic transaction bypass or arbitrary postcommit callback.

`DiscCueSheet.ReferencesDataImageSnapshot(bytes, cuePath, imagePath)` is the one
implemented shared read-only seam. Existing `Read(path)` retains File.ReadAllLines
and its behavior. The snapshot uses the same extracted parser and BOM-aware line
semantics, a 64-KiB cap, and no new filesystem CUE read. LF/CRLF/CR, UTF-8 BOM and
shared UTF-16-BOM parity are tested; the strict research profile independently
refuses non-UTF8 CUE input.

## Remaining approval and test boundaries

The successful publication evidence above does not claim hardware durability,
hostile ancestor-race resistance, crash atomicity, native runtime acceptance or
normal Create BIN approval. Trusted ancestors and cooperating-writer leases are
explicit prerequisites. Existing transaction rollback-conflict tests remain its
lower-level evidence; the new layer must preserve typed retained-recovery paths.
The successful fixture covers final complete-set verification and
partial/companion/link corruption without changing legacy output.

Exact supported whole image: 661547040 bytes, 1253 changed raw sectors, SHA-256
`1818fe50a132901b9ad670769d4967ccc0a5120897637efa2ad709b8f1d53188`.
No other body shape, track, textured revision or caller-declared acceptance can
inherit this research profile.
