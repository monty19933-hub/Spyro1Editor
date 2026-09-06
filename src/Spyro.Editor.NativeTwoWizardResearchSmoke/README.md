# Exact two-Wizard research candidate

This separately named source-issued profile composes the two persistent proposed
owners `z`/128 then `a`/129 through the actual append, placement, resource,
overlay and death-reward plans. It shares immutable model/code resources and
one physical artifact/transaction implementation with the existing one-Wizard
profile. It does not accept a caller count, hash, receipt, actor list, placement,
or approval metadata as native source authority.

The two authored routes remain `(92832,25248,10445)→(91808,25248)` and
`(86688,25248,10445)→(87712,25248)`. The native scene has 130 placed rows, 151
fixups and 89 dynamic slots; the actual source reward owners derive target120.
The overlay and copy-buffer EXE remain byte-identical to the old profile. The
proposed ledger in the new receipt is **not** accepted App/card history.

## Commands and output boundary

Run from the repository root, substituting the exact original US source files.

```sh
dotnet build src/Spyro.Editor.NativeTwoWizardResearchSmoke/Spyro.Editor.NativeTwoWizardResearchSmoke.csproj -c Release --nologo
dotnet src/Spyro.Editor.NativeTwoWizardResearchSmoke/bin/Release/net10.0/Spyro.Editor.NativeTwoWizardResearchSmoke.dll
dotnet src/Spyro.Editor.NativeTwoWizardResearchSmoke/bin/Release/net10.0/Spyro.Editor.NativeTwoWizardResearchSmoke.dll --source original.bin original.cue .
node src/Spyro.Editor.NativeTwoWizardResearchSmoke/source_two_candidate_oracle.mjs original.bin
```

Default mode is source-free. `--source` reads the original and hashes complete
expected images in memory; it writes no candidate or payload. The independent
raw oracle reads no editor DLL/candidate and writes JSON only to stdout. Original
SHA is `fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.

Only separately authorized fault exercises use this output-producing mode:

```sh
dotnet src/Spyro.Editor.NativeTwoWizardResearchSmoke/bin/Release/net10.0/Spyro.Editor.NativeTwoWizardResearchSmoke.dll --exercise-staging original.bin . NEW-private-directory
```

It requires an absent directory, creates its own source-CUE fixtures, and runs
the unchanged legacy14 fault cases plus two-profile cancellation, corruption,
companion, cross-profile, readback/retry and cleanup tests. Complete-set Publish
calls in this test take the verification-only branch. There is no successful
candidate publication. Tiny text-only transaction fixtures exercise all seven
rename fault boundaries and deliberately retain one incomplete rollback's
journal, lock and competing owners. These are not bootable candidate artifacts.
The smoke has no implicit `--publish` option. Two separately reviewed explicit
modes are prepared; neither is invoked by default/source/fault tests:

```sh
dotnet src/Spyro.Editor.NativeTwoWizardResearchSmoke/bin/Release/net10.0/Spyro.Editor.NativeTwoWizardResearchSmoke.dll --publish-reviewed original.bin original.cue . ABSENT-output-directory
dotnet src/Spyro.Editor.NativeTwoWizardResearchSmoke/bin/Release/net10.0/Spyro.Editor.NativeTwoWizardResearchSmoke.dll --verify-reviewed original.bin original.cue . EXISTING-output-directory
```

Each requires exactly five CLI arguments with no blank path fields. Publication
requires the output path to be absent, not an empty/existing folder. Verification
requires an existing non-linked set and is read-only. Both capture the real
fixed two-owner factory, compare independent full BIN/WAD/root goldens, check
all complete CUE/receipt bytes, recapture a new private plan and independently
Verify again, then confirm original BIN/CUE unchanged. The reviewed Plan golden
is additionally pinned for the exact retained original paths; other byte-exact
source copies have path-bound Plan/receipt identities verified by recapture.
Argument tests perform no source/output I/O and leave the existing free9/source275
counts and source-only path unchanged. Root's default Release gate passed121/0,
including the21 argument checks; its explicit source run passed275 and retained
legacy verification passed333. After checkpoint `4d6de96`, the separately
authorized new-only publication and standalone verification both passed, as
recorded below.

## Independent byte controls

The physical result was independently derived before comparison with C#:

| Owner | SHA-256 |
| --- | --- |
| Two complete BIN | `612e68e1a8642d42f2417fd6153368c9549094ac391a3835c9cf6dfee7808cba` |
| Two complete WAD | `4e7c47911822087eb30e531b8491b7548cfdf18d069e0a24a12e8711be971a94` |
| ISO root | `7a881ab875392b331f65dc294368ebc6963998b5c24632acf2f63f3bff0cc6a0` |
| Two final data | `375b9dc41a76d507ded9ffdf442e4febbb1916e68bb5d4597477bb9031e2fd90` |
| Two reward120 EXE | `ce9c99ba467b089603850989bd141f374a7f86cf8e4deeab3fea1a3fc30164d1` |
| Legacy complete BIN | `2a87dc12f9398ef873267e0f17792cfe160952c9f1bf12c137f64029863a39db` |

Both retain 41-sector growth, relocated EXE53916..54120 (exclusive) and 46,341
changed raw sectors versus original. Only seven sectors differ between the two
profiles: 8585,8591,8596,8597,8601,8602,54107. The independent oracle validates
its EDC/P/Q implementation against all 54,043 original Form1 sectors and accounts
for the 41 original canonical Form2 gap sectors before whole-image comparison.

With the retained exact original paths, legacy Plan remains
`255d3b51201aa664113e30deed4faee8b12a909c7560292c44c420f221adae87`,
receipt `dd7d490cdfa5cdf60d92e110b1946d4d296277563b77d360e4d6513821a0d32a`.
Source paths are intentionally part of Plan/receipt identity, unlike BIN bytes.

## Regression evidence

Initial tests-only Release RED: 0.49s, 11 intended missing-contract/friend-access
errors. The first shared wrapper exposed a real compatibility regression:
legacy null calls threw synchronously instead of returning faulted Tasks. The
retained RED is followed by the narrow old-wrapper async/await correction;
neither source bytes nor receipt serialization changed.

Focused Release then passed clean in 3.84s: default9, source275 (the source count
includes the9 default checks). Legacy default73 and explicit retained candidate
Verify333 passed with full old Plan/receipt/BIN acceptance unchanged. The final
fault-only harness build passed clean in 0.51s. Its first execution passed
legacy14 plus two-profile/transaction53, with zero successful candidate
publications and exact original-BIN pre/post hash equality. Full failed candidate
stages were cleaned; unrelated user sentinels and one deliberate tiny incomplete
rollback's recovery evidence are retained in `two-candidate-fault-LjcRmj/`.

Logs and earlier accepted design are retained under
`_local/research/dark-wizard-native-closure-20260906/` with the
`two-candidate-` prefix. Independent first and pinned-repeat physical results
are under `two-candidate-raw-oracle-r01.cOLxG3/`; the tracked oracle SHA is
`d5132569ef8cd9cce55aaee2d8c8a197281f1ae3a2fd5a870e7135448f9d94f8`.
The prior229 resource oracle and all old defaults remain unchanged.

## Reviewed publication and standalone readback

After confirming the target was absent, explicit `--publish-reviewed` exited0
for `_local/research/dark-wizard-native-closure-20260906/two-candidate-r01`.
A separate `--verify-reviewed` process also exited0. Both freshly recaptured
the actual source-issued plan and verified the entire image and exact CUE/receipt.
The set contains only the three expected files; no recovery/staging residue.

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| `dark-hollow-native-two-wizards-r01.bin` | 661547040 | `612e68e1a8642d42f2417fd6153368c9549094ac391a3835c9cf6dfee7808cba` |
| `dark-hollow-native-two-wizards-r01.cue` | 97 | `d54624ebee82a0362ca3c3c65e771bcba681c37f392e0a29a2c5adc34ea1c7e6` |
| `native-two-wizards.receipt.json` | 46201 | `ac0f7e2c49a1207b036444b386a3443376c2f29417958a0922a98df39ae59f75` |

Exact retained-path Plan:
`efed20f6d745d33190bec4b6d2cd038c4916bcda70d5b170c180f5e8de92eae1`.
An additional read-only raw probe confirms all41 prepared sectors plus old/new
WAD/EXE boundary EOF/EOR bits, complete source BIN/CUE hashes, and all three
old one-Wizard artifacts unchanged. Publication logs, standalone verification,
raw probe/JSON and exact commands are retained under the same research root as
`two-candidate-r01-*`; see `two-candidate-r01-publication-handoff.md`.

## Explicit limits

The exact two-Wizard research set has been published and independently reopened;
no two-Wizard native runtime test has run at this stage. Both deaths/rewards,
independent collection/checkpoint/re-entry,
simultaneous native effects/audio, global/GTE/stack and transient resource use
remain gameplay gates. Postcollection native RNG/companion behavior is retained;
absence of another deterministic treasure does not mean no possible drop.

The physical production source contract retains the previous whole-image
pre/post freshness checks; the oracle's same-pass per-sector authentication is
independent evidence, not a new hostile-filesystem-race guarantee. Publication
uses exception rollback, not crash atomicity. Runtime, complete native closure
and ordinary Create BIN approval remain false. No normal exporter, installed
editor, App workflow, live emulator setting or original input is changed here.
