# Publisher smoke compiler-scope experiment

The existing `Id65AuthoringRewardStagingRuntimeCandidatePublisherSmoke` now has an explicit `Program` class and `async Task Main`. Its 74 static local helpers become ordinary static members. Only seven nonblank structural/annotation lines were added; no executable body, helper signature/body, assertion, fixture, checkpoint order, native import, or global type was rewritten. The macOS class annotation preserves the existing macOS-only harness contract; the original runtime OS guard remains and no analyzer is suppressed.

## Measured result

The preceding frozen Release gate reported 259.86 seconds for this project (another recorded run took 257.25 seconds). The first isolated wrapper compile took 2.11 seconds wall / 2.00 seconds MSBuild with zero errors and 36 CA1416 warnings caused by the changed member scope. After adding the exact `SupportedOSPlatform("macos")` annotation, a real recompilation took **2.00 seconds wall / 1.89 seconds MSBuild, zero warnings and errors**.

Both experiment builds used Release defaults, `--no-restore -p:BuildProjectReferences=false`; Core was not rebuilt. This demonstrates a substantial improvement in these recorded builds, not an identified Roslyn pass or a guaranteed timing for every host. The earlier samples' managed compiler frames were unsymbolized. The final `/usr/bin/time -l` reported 188661760 bytes maximum RSS and 143885200 bytes peak footprint for the timed build process; these are not a measured peak of a persistent compiler server and must not be directly relabeled as a replacement for the earlier 15–18 GB server samples.

## Exact-source regression

The source-free gate reconstructs the proposed source from commit `26042c661d2a2a322ef3179ed0b666f1ab51ebbe`, removes only the exact wrapper/annotation, and requires byte-identical original sections. It also compares the lexical token stream and seven checkpoint strings, verifies all 74 helper declarations occur at class scope, and preserves all five DllImports and the global native types. Eleven finite mutations are refused, including altered or missing platform annotation, changed assertions/checkpoints, lost closing boundaries and modified cleanup.

- Original Program SHA256: `9222c5e7e87fa73fab7090534407dc46ff5ac09d735507525da04e8b92cad48c`.
- Final annotated Program SHA256: `14a050c615d2b899ffdb1c48eb0f40c8e94d838701751484cd475859fd407923`.
- Original executable-body SHA256: `ff1455470061a1e0a5e09e00e2bed99875ce13f481482e60455fb030d91ae0e9`.
- Original helper-section SHA256: `ae5878edf636c49ff3851f228a4efe1ed7337211476c7ba0de5451145711cbff`.

Retained plan, source-free gate, result, preflight and build log are under `_local/research/publisher-compiler-scope-20260906/`; the original scratch evidence was not deleted. Replay from the repository with:

```sh
node _local/research/publisher-compiler-scope-20260906/equivalence.mjs src/Spyro.Editor.Id65AuthoringRewardStagingRuntimeCandidatePublisherSmoke/Program.cs
```

## Full legacy smoke was not run

The unchanged legacy test deliberately toggles byte 0 of its input BIN/CUE/foundation paths during freshness tests. Those paths currently name retained candidates, so execution would require a separately reviewed private copied workspace. Preflight freshly confirmed all three original hashes, without mutation.

Additionally, the shared fixture builder at `Id65V3RuntimeEvidenceContractSmoke/Program.cs:4385` copies the live normal-user DuckStation settings into its fixture. Its frozen contract requires 10058 bytes / SHA `896daa612142659aa950c16d2435f81cb034aa8b56768b91e60a4ec8f0a5ccc9`; the actual read-only sentinel was 10791 bytes / SHA `503cc28dcc0ea0e801fd9952d3146f2177529df01119b275f2b107887cd844a4`. No exact historical snapshot was located in the bounded locked/native evidence paths inspected. The live settings were not altered, no replacement evidence was invented, and no fixture check was skipped.

Accordingly this checkpoint proves the measured compilation and exact-source equivalence only. It does **not** claim a fresh pass of the legacy publication/rollback/recovery runtime smoke or any change in native/gameplay/release approval.
