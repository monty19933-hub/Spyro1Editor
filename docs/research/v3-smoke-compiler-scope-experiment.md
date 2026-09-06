# V3 smoke compilation and exact fixture binding

The V3 smoke's enclosing local-function scope caused a reproducible, expensive compilation in the measured configuration. A bounded declaration rearrangement substantially improves the measured compile. A compiled-metadata check also caught a **refactor-induced** fixture-lookup regression before commit; the consumer now uses a tested exact member lookup. This changes the test harness, not native game code or export approval.

## Controlled measurements

Both arms used .NET SDK10.0.107, Release, identical project/props, the same already-built Core reference, `BuildProjectReferences=false` and `UseSharedCompilation=false`. Fresh private output directories ensured actual Csc execution; diagnostic logs/binlogs retain the source/reference/compiler arguments. No compiler server was used in these isolated measurements.

| Measured arm | Csc time | Wall time | Warnings/errors |
| --- | ---: | ---: | ---: |
| Original top-level V3 | 291.521 s | 291.97 s | 0/0 |
| Exact scoped variant, matching generated assembly metadata | 2.665 s | 3.09 s | 0/0 |

Thirty Core output/cache files stayed hash-identical throughout these private experiments. The baseline and accepted variant's generated AssemblyInfo files are byte-identical, including `1.0.0+b50c9a83f03175c77979af339595e1e48ecf0f23`. The initial 3.16-second variant used a later documentation commit's informational version; that discrepancy was found and a **fresh** variant was recompiled with the baseline SourceRevisionId. Both variants and all logs are retained. No up-to-date build is counted as a compilation measurement.

The baseline command's `/usr/bin/time -l` reported 27,137,458,176 maximum-RSS bytes, versus 985,448,448 for the matching variant. Separately, an owned baseline compiler-process sample recorded 16.8 GiB physical footprint/peak **at sample time**. The timed launcher footprint is a different metric; these observations are not an exhaustive process-tree or compiler-server peak comparison. Managed sample frames do not establish a particular Roslyn pass. The timings are one isolated baseline and two variant observations, not a guarantee across hosts or compiler versions.

After integration, a real repository Release build of Core, V3 and publisher together passed in 8.46 seconds, with zero warnings/errors. This is distinct from the isolated timing and from the preceding 290-step full gate at `b50c9a8`.

## Preserved source and deliberate interface change

The V3 transformation adds an explicit synchronous Main/Program; moves six literal watcher constants and 44 static helpers to class scope; adds `static` to the fixture builder and watcher formatter; and keeps captured counters and thirteen capturing helpers inside Main. Five interspersed capturing declarations move intact into Main. Executable statements, assertions, fixture bodies, cleanup, native import and three global types remain unchanged. No analyzer, optimization or test-count suppression was added.

`tools/Verify-SpyroV3SmokeCompilerScope.mjs` reconstructs the exact declaration permutation from the pinned Git baseline and verifies its complete inverse, fourteen negative mutations and unchanged source sections. The tracked-file gate was observed RED before the transformation and GREEN afterward. This checkpoint-specific gate expects Git's canonical LF source bytes; it is not a general C# semantic-equivalence tool.

- Baseline Program SHA-256: `626370c366864367576592167e619b0ffe54841f87e4dc085aae686ed6c36ebc`.
- Scoped Program SHA-256: `be9af5fd1410bbfcd2b486acc06aa0f80726631fdf3410b042805ff2d78a1c95`.

The original publisher's substring lookup selects **one** baseline method but **eight** after the scope change: the builder and seven newly named compiler-generated inner helpers. The initial two-arm probe failed on the second arm, not the baseline; an early inference that this was a pre-existing failure was corrected. The original predicate therefore cannot accompany the scope change unchanged.

The publisher now resolves the exact global `Program.BuildCompleteFixture` private static member, verifies the exact twelve parameter names/types and eight typed defaults, and verifies the return-type owner. Both the real fixture path and the new isolated regression use this same resolver. It does not parse compiler-mangled names or rely on a substring match.

A separate PEReader-only comparison pins the exact known baseline method and exact variant member. It confirms matching required/default inputs, six public init-only fixture properties, global native `libc/link` import and UTF-8 marshaling, synchronous entrypoint and assembly metadata. The baseline compiler emits internal visibility and the variant emits private visibility; this explicit change is recorded, not called byte-identical metadata. Both are nonpublic. The old predicate's 1/8 counts remain recorded and its failure is not relabeled as a pass. No inspected assembly, fixture builder or native import was invoked by this probe.

## Regression and replay

The new publisher mode returns before source/candidate/settings paths, platform-dependent fixture setup or publication. It inspects the compiled dependency but never invokes its code:

```sh
node tools/Verify-SpyroV3SmokeCompilerScope.mjs
dotnet run --project src/Spyro.Editor.Id65AuthoringRewardStagingRuntimeCandidatePublisherSmoke --configuration Release --no-build --no-restore -- --source-free-fixture-lookup
python3 -m unittest discover -s tools/tests -v
```

The compiled lookup regression was RED against the old V3 member shape, then GREEN with **5 positive checks and 67 refusals**, including the real ambiguous helper set, empty/duplicate/foreign/nested-only inputs, bounded enumeration, every parameter name/type/flag and every default. Int32 zero cannot substitute for the Int64 default. Extra CLI arguments are rejected before fixture work. The complete Python suite passes **43 tests**; the runner's new registration test was observed RED before registration and GREEN afterward. The default regression runner registers only this explicit safe publisher mode, never its legacy default invocation.

Evidence: `_local/research/v3-smoke-compiler-scope-20260906/experiment-r01.3HIzLK/` and sibling `metadata-probe/`. The standalone metadata tool and controlled-project preparation remain retained research artifacts. The source gate and actual resolver regression are tracked.

## Deliberately unrun

Neither the V3 Main nor the full legacy publisher fixture was executed. They still deliberately mutate retained candidate paths during freshness tests and require the unavailable historical normal-settings sentinel. The existing private-copy/sentinel boundary remains; no check was skipped and no historical settings evidence was fabricated. This checkpoint does not certify full legacy publication/rollback runtime behavior, gameplay, complete resource closure, or the newly drafted fresh-level/texture producers.
