# Source-owned count-aware Wizard placement tests

The source-issued scene-only sibling is implemented and registered in
`tools/Run-SpyroEditorRegression.py`. Default execution uses no arguments;
`--source <original.bin> <workspace>` is opt-in through `--with-source-qa`.
Neither mode reads a CUE, publishes a candidate, or launches the emulator.

Only a sealed source-issued persistent Wizard append plan can issue placement
owners. The factory recaptures the complete original image and Dark data,
verifies the exact append afterimage and donor 33/T6 row/private schema, and
obtains canonical row/private extents from the allocator. Complete FamilyId
placement intent sets are ordered by persistent native ordinal, not caller order.

Tests cover temporal z128 -> reopen -> a129, complete FamilyId intent bijection,
all 128 original resident-center checks, exact full closed-segment pairwise
separation and nine owned int32 fields per family. Proper/interior, collinear
and endpoint intersection checks precede all four endpoint-to-segment distances.
Signed-int extremes use exact BigInteger arithmetic. Malformed, incomplete,
overlapping and finite adversarial enumeration inputs are refused atomically.

The independent raw oracle reads/hash-binds the original image pre/post,
reconstructs one/two scene splices and all 9/18 coordinate changes, and binds
selected support bytes and exact center distances. Full continuous corridor
coverage/native index membership remains the separate C# source certificate,
not an oracle claim. It imports the existing standard-library raw append grammar,
not Core.

## Executed evidence

- Tests-first Debug RED: seven missing intent-type errors, existing Core clean.
- Debug GREEN: 5.01s, zero warnings/errors; source-free 19 checks/1 refusal;
  exact-source 108 checks/54 refusals.
- Root independent Release: 4.91s, zero warnings/errors; same 19/108 checks and
  same 1/54 refusals; independent raw one/two goldens passed.
- Runner registration regression was RED before registration; full Python
  runner suite then passed all 28 tests in 2.543s.
- Root reran all 42 Python tests (28 runner plus 14 observer), passing in 2.593s.
  Independent review of the complete placement implementation/tests found no
  material issue within the stated source-only boundary. The raw oracle disables
  imported Python bytecode writes so its replay leaves no generated cache in src.
- Unchanged old fixed one-Wizard scene:
  `6b34477c459224cbbf4153336d9c4f7e2aada20d9085c975c1eca2ef74c0d16f`.
- Two-family authored scene:
  `9dbdf8839f19cf31becda27b52b5a854f6badb8b59ad1db110c921055e5263dc`.

Exact command logs and the full raw oracle output are retained under
`_local/research/native-moby-owner-ledger-20260906/` (15–21 count-placement logs).

## Commands

Builds require the coordinated serial .NET slot.

```sh
dotnet build src/Spyro.Editor.NativeWizardAppendPlacementSmoke/Spyro.Editor.NativeWizardAppendPlacementSmoke.csproj -c Debug --nologo
dotnet src/Spyro.Editor.NativeWizardAppendPlacementSmoke/bin/Debug/net10.0/Spyro.Editor.NativeWizardAppendPlacementSmoke.dll
dotnet src/Spyro.Editor.NativeWizardAppendPlacementSmoke/bin/Debug/net10.0/Spyro.Editor.NativeWizardAppendPlacementSmoke.dll --source "Spyro the Dragon (USA).bin" .
python3 src/Spyro.Editor.NativeWizardAppendPlacementSmoke/source_count_placement_oracle.py "Spyro the Dragon (USA).bin"
```

## Explicit boundary

This is coordinate-only composition over an original-derived append afterimage,
not a full retail afterimage to layer over an edited project. All old fixed APIs
and single-candidate bytes are unchanged. The accepted static policy is an
authored dry square corridor and strict center separation; it does not prove
physical shapes, initial height, visible body or native movement. Proposed owner
documents are unchanged, never accepted into project/export history by placement.

Native behavior, resource/reward multiplicity, retirement emission, accepted
save/export history, App transaction, runtime fit, runtime and publication remain
unapproved. This stage writes no disc or candidate.
