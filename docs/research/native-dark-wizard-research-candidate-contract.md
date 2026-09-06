# Dark Hollow native Wizard research candidate

`NativeDarkHollowWizardResearchCandidatePlan` assembles one explicit, exact-original research recipe. It is separate from normal Create BIN and has no level-selector patch. Its source-free and read-only-source smoke modes do not create a candidate image. Publication requires an explicit separate invocation after review.

The factory itself captures the real original BIN/CUE, inventory, one appended scene, player animation 8, native mapped/local sounds, private textures, particle descriptor 7, authored dry-corridor placement, common-layout resources, native behavior/particle code, lifetime resource guard, owned overlay, and native death-reward target accounting. Public metadata or caller-supplied payload bytes cannot issue this candidate. All original 128 actors remain present. The intent is fixed to Wizard `(92832,25248,10445)` with route end `(91808,25248)`; the authored initial height and actual body/trajectory remain runtime questions, not source-certified physics.

## Whole-disc ownership

Only WAD entries 13 and 14 are replaced. Every occupied WAD directory entry is read from the actual packed source and has its complete before/after extent and SHA-256 retained. Unrelated entries keep identical contents and lengths. The inverse reconstructs the entire original WAD hash using the captured original two owners, original header, and preserved final copies of every other entry.

| Owner | Original | Final |
| --- | --- | --- |
| Entry 13 overlay | WAD `0xEF6000`, length `0xB000` | WAD `0xEF6000`, length `0xC800` |
| Entry 14 data | WAD `0xF01000`, length `0x28D800` | WAD `0xF02800`, length `0x2A0800` |
| Complete WAD | `0x6927000` bytes | `0x693B800` bytes |
| EXE LBA / end-exclusive | `53875 / 54079` | `53916 / 54120` |
| WAD last / EXE last sector | `53874 / 54078` | `53915 / 54119` |

Growth is derived from actual replacement lengths, then checked against the bounded profile: `0x1800 + 0x13000 = 0x14800`, exactly 41 sectors. The existing complete-ISO inventory and sector-preparation utility validates ownership of all newly used gap sectors before any output mutation. ISO root records are updated in both endian encodings. The source file length never changes.

The writer uses the existing source-bound raw-sector preparation and logical writer; its separately composed expected-sector oracle checks the entire raw image, including protected data and parity. All 41 newly occupied sectors and all old/new EOF/EOR boundaries are explicitly exercised in read-only-source regression. The normal-load copy buffer is `0x80087238`, scene-read end `0x801439E4`, reserved-buffer gap `0x441CC`, and shared dynamic Moby capacity 90. These are known resident extents, not peak/transient stack or allocator success guarantees.

Independent source-only oracle hashes:

- Original complete WAD: `7ba8961c3626bcec816fbd6feecc4eff67dc8018ba43974e7838f4c341bf54a1`.
- Final complete WAD: `d07c91d13d9a7653a3708afd5d6de9e0279e92b495883d519e967b618e83d1d3`.
- Final ISO root: `7a881ab875392b331f65dc294368ebc6963998b5c24632acf2f63f3bff0cc6a0`.
- Placed data: `6ed74dbce04d6712e0d9ac12cfd05ae6ac334e0ec39fb7420f2105076132e537`.
- Overlay: `10f82ee1028a9ad4fa9045d948a88e76545847031b954ac08ad0e74e6e9c7083`.
- Reward-accounted EXE: `ec1e8ae38fed404f9a28ccc7fbf17d81e92a341aab89906e94c5e6c793f4c3b4`.

The tracked `source_candidate_oracle.mjs` reuses the previously reviewed raw-source oracles' in-memory outputs, applies the nine placement fields independently, reconstructs the complete WAD/root/EXE, and writes no ROM or payload. Its overlay oracle dependency gained only an export statement; the original CLI output and golden bytes remain unchanged.

## Publication boundary

`NativeDarkHollowWizardResearchCandidatePublisher` accepts only the private-constructor plan. It uses a new-only directory, cooperating-writer lease, isolated staging, exact source/CUE freshness at capture/publication/verification boundaries, whole-image physical readback, and exact receipt/CUE companions. Existing complete sets are verified for immutable retry, not overwritten. Partial, occupied, linked, or recovery-bearing destinations are refused. The existing artifact transaction publishes BIN, receipt, then CUE last. Failed staging is cleaned only within this invocation's owned names; transaction recovery evidence is preserved.

This is exception-rollback publication in trusted directories, **not** crash-atomic publication or protection against hostile ancestor-directory races. The receipt explicitly retains those limits. No ordinary exporter is connected or unlocked.

## Checks and replay

Regression-first RED stopped in the unimplemented packed-entry seam. Final focused Debug passed with zero warnings/errors; source-free regression passed **73**, and read-only-source passed **333**. Checks include malformed/duplicate/missing/stale owners, bounded request enumeration and returned lengths, complete original complement and inverse, detached canonical stage authority, all 41 sector promotions/boundaries, actual 128-row source preservation, real CUE binding, and the independent complete-file hashes. Payload cloning follows actual directory-owner and final-allocation validation. Companion lengths are checked before allocation, then exact length/position/bytes after reading; tiny malformed, oversized and late-length streams exercise that boundary. The linked pre-existing physical/CUE suite checks corruption across headers, payload and parity and exact CUE mutation/removal refusals.

```sh
dotnet run --project src/Spyro.Editor.NativeWizardResearchSmoke -c Debug --no-build
dotnet run --project src/Spyro.Editor.NativeWizardResearchSmoke -c Debug --no-build -- 'Spyro the Dragon (USA).bin' 'Spyro the Dragon (USA).cue' .
node src/Spyro.Editor.NativeWizardResearchSmoke/source_candidate_oracle.mjs 'Spyro the Dragon (USA).bin'
```

Explicit `--exercise-staging`, `--publish`, and `--verify` modes take a final output-directory argument. They are not part of the read-only baseline. The separately authorized exercise passed **14 additional checks / 347 total** under `_local/research/dark-wizard-native-closure-20260906/staging-faults-r01`. It covered injected post-write/pre-publication failure, immutable complete-stage retry, stale companions, protected parity corruption, late private-CUE deletion, occupied data, and output links without changing the real source CUE/BIN. All four failed-output directories were empty afterward; only the restored private CUE and preserved `occupied/keep.txt` fixture remain. The original BIN SHA-256 was freshly rechecked as `fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`. No real candidate has yet been published at this checkpoint.

Complete transitive globals/GTE, actual peak stack use, body and route/bolt trajectories, native behavior/effects/sound/death/reward/persistence, neighboring actors, and normal entry/exit/death/re-entry still require runtime observation. Every candidate receipt keeps `CompleteNativeClosureProven`, `RuntimeVerified`, and `NormalCreateBinApproved` false. Publishing this reviewed research artifact is permission to test it, not evidence that those tests passed.
