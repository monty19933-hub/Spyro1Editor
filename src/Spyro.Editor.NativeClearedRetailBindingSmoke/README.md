# Cleared retail binding smoke

Build this standalone project in a private source mirror. With no arguments it
checks that fabricated source bytes cannot issue a bootstrap. With an unchanged
original USA BIN and an optional report directory, it performs the complete
30-slot source census, all supported target rewrites, exact untouched-byte
comparisons, owner/alias/table/new-game mutations and atomic refusal tests.

```sh
dotnet build src/Spyro.Editor.NativeClearedRetailBindingSmoke -c Release
dotnet src/Spyro.Editor.NativeClearedRetailBindingSmoke/bin/Release/net10.0/Spyro.Editor.NativeClearedRetailBindingSmoke.dll ORIGINAL_USA.bin REPORT_DIRECTORY
```

The initial producer supports ten full nine-setter initializers; twenty short
initializers require a separate source-proven implementation. It does not claim
native runtime, complete load-state coverage, minimum retained assets, save
identity migration or title-demo safety. Its JSON report contains metadata and
hashes only, not extracted executable payloads.
