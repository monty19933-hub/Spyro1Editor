# Exact fresh Wizard research materializer

This isolated smoke exercises the new source-issued Candidate and one closed
Wizard binding in the existing fresh publication engine. Public input is original
BIN/CUE plus the actual issued Disc object; no caller hashes, filenames, approval
flags, serialized receipts, raw bytes or invented project context can issue it.

The recipe is deliberately narrow: canonical A floor, P1/N32, primary placement
(8704,8960,8704) to (9216,8960), track19, seven-root body, static target10 at slot35.
Its full physical golden is
`c4a70f826b1a9a3d4ad8e36512be354b6205d27ea2c7a314776a51067f35b5a6`,
661547040 bytes and1290 changed sectors. Independently reissued equal primary
Discs can create their own candidate, but an existing candidate cannot exchange
its exact reference. The valid alternate Z8909 scene is refused, not replaced.

## Non-output routes

```sh
dotnet build src/Spyro.Editor.NativeId65FreshWizardResearchSmoke -c Release
dotnet src/Spyro.Editor.NativeId65FreshWizardResearchSmoke/bin/Release/net10.0/Spyro.Editor.NativeId65FreshWizardResearchSmoke.dll
dotnet src/Spyro.Editor.NativeId65FreshWizardResearchSmoke/bin/Release/net10.0/Spyro.Editor.NativeId65FreshWizardResearchSmoke.dll --source ORIGINAL.bin ORIGINAL.cue
```

Default and source never dispatch a physical stage or publication. Source creates
and removes a tiny named temporary CUE/alias fixture; it does not write a BIN,
project manifest, receipt, source original, app setting or emulator state.

The source stream tests use actual captured Candidate, original file handles and
a non-storing hash sink. They execute the complete production writer loop and
verify its returned full golden/count, but do not claim filesystem flush/reopen
or successful publication. Thirty additional invocations cover null/capability/
initial owner guards, pair position/length/short reads and writes, changed and
unchanged original LBA authentication, cancellation at first/middle/final pair
and actual EOF, output/callback errors, and complete noncancellable original
SHA/EOF finally checks. A final-pass-only returned-byte mutation proves that
the final original check is real and authoritative. Caller streams stay open.

Source also tests exact actual ancestry, canonical/CUE/receipt copies and
determinism, false receipt authority flags, genuine old-bound wrong-kind refusal,
supplied-path and third FILE-alias revalidation, source CUE CR/LF/BOM semantics,
and original BIN/CUE full before/finally hashes. Source-free tests cannot prove
wrong-kind issuance by passing null, and do not claim postcommit outcomes.

## Evidence and remaining gates

Actual missing-public-API RED is retained at
`_local/research/dark-wizard-native-closure-20260906/fresh-wizard-materializer-implementation-r01.yz0yeE/01-api-red.log`:
six missing-new-type errors only, zero warnings,0.53s. Initial production build
was clean5.35s; complete test build clean5.33s. First free API9/free151 and source
165checks/20refusals passed. Final reviewed build is clean5.07s and free9+151
passed. Final source repeated165checks/20refusals with identical candidate/CUE/
prospective-receipt identities. Three old-kind Release/free/source gates passed:
starter1.57s/free81/source182 plus89 timing cases; edited0.55s/free24/source281804
plus89 timing/43 argument cases; saved0.52s/free54/postcommitAPI10/arguments201,
source23/postcommit-sourceguards15. Edited/saved physical ecc8…ed16/1253 and
saved prospective receipt cc0184…4af1/4256bytes are unchanged. All build logs have
zero warnings/errors. Exact commands/logs/hashes are in the evidence HANDOFF.

`StagingCases.cs` is prepared but not dispatched or executed in this checkpoint
work. Every call forces precommit failure, including occupied/link/overlap/lock
cases; a missing guard must not silently publish. Actual staged complete
readback, corruption/cancellation, partial cleanup, foreign residue and duplicate
writer cases require separately reviewed exact output-directory authority.

Actual successful new-only Publish, separate-process Verify and postcommit
exercises remain unexecuted here. Those must prove late cancellation returns
verified success, while postcommit callback/cleanup/readback failures produce
the actual typed committed outcome and retained evidence. In particular,
subsequent stage owners using known output basenames, regular-file stage
replacement and an after-commit rollback-exception callback must be preserved.
Static review is not execution of these paths.

The three older fresh binding kinds keep their historical serializers, tokens,
failure timing and cleanup behavior. The stricter postcommit any-stage-owner
and regular-file stage checks apply only to the new Wizard kind; no claim is
made that analogous older cleanup behavior was changed.

Even exact physical publication would remain private research, not normal
Create BIN support, native gameplay certification, durable/card ownership,
global audio compatibility, live peak memory safety, complete incoming edges
or full native dependency closure. Trusted filesystem ancestors and cooperating
writers are assumed; this is not hostile-race safety or crash atomicity.
