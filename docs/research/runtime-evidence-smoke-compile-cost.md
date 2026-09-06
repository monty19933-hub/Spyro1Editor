# Runtime-evidence smoke compile-cost diagnosis

Read-only diagnosis during the `b9bcdfb` full Release gate; no builds were
launched by the diagnostic agent, and no processes, settings or code changed.
The gate's build step 79 passed in **257.99 seconds**. Its long-running compiler
was CPU-busy and exceeded 20 GiB resident memory; this reproduced the prior
checkpoint's slow compile, not an observed deadlock or an in-game RAM problem.

## Verified dependency and invalidation

`Spyro.Editor.Id65AuthoringRewardStagingRuntimeCandidatePublisherSmoke` references
Core and the entire `Spyro.Editor.Id65V3RuntimeEvidenceContractSmoke` executable.
The publisher uses the latter solely through reflection to obtain its local
`BuildCompleteFixture` helper (publisher `Program.cs`, around line 2282).
Neither old smoke source/project differs from `e4ff8ef`.

The evidence smoke has a roughly 3,950-line top-level execution body and a
1,197-line local fixture builder. Its compiled metadata has 26 generated
closure classes. Nullable analysis is enabled. Large shared local scopes,
nested lambdas and branches are plausible compiler-cost amplifiers, but the
specific expensive Roslyn phase has **not** been profiled or established.

Rebuild invalidation is not exclusively caused by Core changes: the generated
`AssemblyInformationalVersion` changed from `96ec93d...` to `b9bcdfb...`, so
unchanged smoke source was recompiled after the Git checkpoint changed. During
this run the dependency DLL appeared at local 22:46:13, followed by the publisher
DLL at 22:46:27. Do not disable provenance stamping or nullable analysis as a fix.

## Smallest regression-first refactor

First move `BuildCompleteFixture`, `CompleteFixture` and serialization/media/
watcher helpers into a named static fixture class within the same smoke assembly.
Preserve exactly one nonpublic static method named `BuildCompleteFixture`, its
four required/eight optional parameters, defaults and reflection-visible shape.
Then split the large execution body into ordinary methods with explicit typed
context/counters. Splitting files while retaining one giant method is not the
same compiler-scope change. A separate test-only fixture library can follow;
synthetic capability issuance and its calling/entry-assembly guards must not move
casually or be bypassed.

Regression scope must retain 43 artifacts, seven sessions, 23 runtime-evidence
checks, Unicode canonical-byte goldens, 121 full-readback refusals, 47 finalizer
refusals, publisher rollback/freshness tests and all existing byte oracles.
Absolute paths and filesystem identities are part of fixtures, so unrelated
temporary roots cannot be assumed byte-identical. Compare controlled Release
compile time and peak memory under the same SDK/settings after preserving
behavior. This is a maintainability follow-up, not a completed performance fix.
