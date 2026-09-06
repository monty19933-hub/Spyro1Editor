# Persistent native Moby owner — isolated tests-first contract

This project is registered in `Run-SpyroEditorRegression.py`: its no-argument
source-free mode runs by default; `--with-source-qa` adds only the read-only
`--source <selected-original.bin> <workspace-root>` mode. Neither path publishes,
reads a CUE, writes native saves or launches an emulator. Its isolated
missing-API compile RED preceded the additive identity-only Core implementation.
No existing compiler, fixed recipe, candidate, App state or ROM changes.

`OwnerLedgerCases` exercises an immutable ledger with `Append`, `Retire`, explicit
`Reactivate`, canonical `Serialize`, and source-bound `Restore`. The first test
is temporal: family `z` receives128, persists/reopens, then new family `a` must
receive129 without renumbering `z`. Role IDs are canonical within the initial
family and remain stable thereafter. High-water includes retired reservations
and cannot exceed256 even for nonreward classes. A retired row cannot emit until
a real native retirement producer exists; this layer does not invent FF/FD or
zero-row tombstones. Source-free fixture layouts never certify native authority.

`OwnerLedgerSourceCases` exercises the public issuer
`NativeMobyPersistentOwnerSource.Capture(originalBin, LevelCatalog, levelId)`.
It binds selected source level/row/role IDs through the complete captured native
inventory; no public constructor or caller-provided hash can create a family
certificate. Restore must re-resolve donor source variants and the exact target
level/save identity. This records identity, not a portable behavior schema.

The versioned JSON document stores original source/row identity, target level and
save slot, high-water, and ordered entries. Each entry retains family, role,
source variant/class and ordinal plus retirement state. It is not permission to
publish, edit memory cards or rewrite an old project's accepted history. Exact
prior mappings must be integrated in the actual project transaction separately;
legacy manifests and older project snapshots cannot claim compatibility from
their current order alone. Mutable-global/family closure checks remain separate
and must still be consumed by the future scene producer.

The independent `source_owner_lifecycle_oracle.py` reads the original raw image
and uses its own bounded scalar MIPS interpreter, including branch/load delays.
It executes only named native windows with declared synthetic register/memory
entries; sound, unlink and complete gameplay are not emulated. It checks native
owner addressing, byte-owner fallback, save/checkpoint copy semantics and the
class7D collected path. It does not run an emulator or write a ROM/save file.

Focused invocations (serialize .NET builds with the root gate owner):

```
dotnet build src/Spyro.Editor.NativeMobyPersistentOwnerSmoke -c Debug
dotnet src/Spyro.Editor.NativeMobyPersistentOwnerSmoke/bin/Debug/net10.0/Spyro.Editor.NativeMobyPersistentOwnerSmoke.dll
dotnet src/Spyro.Editor.NativeMobyPersistentOwnerSmoke/bin/Debug/net10.0/Spyro.Editor.NativeMobyPersistentOwnerSmoke.dll --source <original.bin> <workspace-root>
python3 src/Spyro.Editor.NativeMobyPersistentOwnerSmoke/source_owner_lifecycle_oracle.py <original.bin>
```

Initial isolated Debug RED executed: 22 missing-API compiler errors, zero
warnings, 5.24 seconds; existing Core compiled successfully. This is compile RED,
not an executed temporal assertion. Log:
`_local/research/native-moby-owner-ledger-20260906/01-isolated-red-build.log`.
Initial Debug GREEN5.49s with zero warnings/errors; all parser/catalog fixtures
passed:67 source-free checks/49 refusals and88 total exact-source checks/63
refusals. The additive production Core API is identity-only. Native emission,
whole dependency-family closure,
private owner relationships, App/project transaction integration, memory-card
and gameplay acceptance remain false regardless of structural/source results.

Independent raw/scalar oracle: 61 checks GREEN with the complete source image
identity verified before/after. Output:
`_local/research/native-moby-owner-ledger-20260906/02-raw-scalar-oracle.json`.
This includes exact ordinal256 live/next-level alias and Dark owner-zero fallback
demonstrations. The legacy edit-order example is only a source-aligned scalar
reconstruction; it does not execute App undo or the production exporter.

Independent root Release verification:4.98s, zero warnings/errors, the same
67 source-free/88 exact-source checks and49/63 refusals. Regression runner
registration was itself tests-first:24 Python tests with2 expected absent-mode
RED failures, then24 GREEN. The added test mocks every .NET command, verifies
default mode never resolves the source/CUE, and binds the exact opted-in image
plus workspace without duplicate builds or publication flags. Logs06/07 are in
the same owner-ledger research evidence directory.
