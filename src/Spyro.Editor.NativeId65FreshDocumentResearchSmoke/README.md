# Generic fresh-document research plan smoke

This is a read-only plan stage, not a publisher. The initial missing-contract RED is retained: Release build 4.02s, zero warnings, exactly two missing `NativeId65FreshDocumentResearchPlan` errors. The plan then built cleanly. A real malformed-CUE test exposed `DecoderFallbackException`; only the new plan normalizes that exception to `InvalidDataException`. Shared helpers and both fixed research profiles are unchanged.

Default mode is source-free. `--source original-BIN original-CUE` captures actual immutable compilations and dynamically measures/verifies their expected raw-sector images without writing a candidate. Tiny owned temporary CUE/alias fixtures are removed; original BIN/CUE remain read-only. There is no output/workspace/publish mode.

The plan accepts only an actual sealed `NativeId65FreshDocumentCompilation`. Its canonical intent, complete body/disc/EXE/directory owners and privately captured original-sector index stay paired. Every source sector is authenticated before computing an expected sector, including unchanged metadata. Expected SHA and changed-sector count are derived by a bounded streaming pass; full original SHA, actual EOF, length/position and cancellation are checked. No caller hash, body/intent pair, receipt or policy flag can issue it.

The current family remains untextured two-triangle floor, zero placed Mobys, N16, fixed LAB title and selectable native music. Variable geometry changes the native data used-end; it does not permit the distinct textured resource revision. The existing compilation/DiscPlan owners, conditional normal-entry and unresolved native resource/runtime boundaries remain in force. A saved project snapshot is not required or claimed by this project-neutral plan.

The four whole-image goldens below are independently retained by the pinned starter/edited/variable raw-sector oracles, not generated from the new plan's answer. The existing `source_variable_data_oracle.py` was independently rerun for this batch without edits: all 190 cell sums/96 used lengths, original Form1 parity, three old full-disc controls, the exact edited image and both variable images passed. Its inverse hash is expected-original evidence; the C# source cases separately execute raw inverse checks at metadata, replaced EXE, converted gap and terminal boundaries.

| Actual document | Expected complete image SHA256 |
|---|---|
| Starter | `1818fe50a132901b9ad670769d4967ccc0a5120897637efa2ad709b8f1d53188` |
| Edited landing | `ecc8f61cc16bf86a2f5117b327c49aede18f5c1e6a602c0e67f2ba58565aed16` |
| Origin zero | `0c3821141e50c74aee91d4ff4eeb7ef8632c3cd63daab624dd4d504d9ed83db2` |
| Maximum cell sum | `40d8cf3fb4168bdd870c8d6f73c2805e357160fa37486205f1b813b743855c2d` |

Each currently changes 1,253 raw sectors. The implementation derives that count rather than using the fixture value. Four actual full-image readbacks use a bounded generated stream, never a physical candidate. Fourteen non-music field alternatives and all 46 actual compiled music selections assert native geometry/collision/colors/spawn/heading/music words. Non-music changes preserve the complete EXE; music changes preserve the complete body and every EXE byte outside the selected slot. Fixed publishers retain their exact-profile acceptance/refusals and plan identities.

Hostile cases cover source mutation at untouched data and PVD metadata, incorrect LBA, wrong initial position, short input, oversized declarations without reads, extra EOF, final position/length changes, mid/late cancellation, raw header/parity mutations and malformed or retargeted source CUEs. Source-free cases exercise the exact shared bounded CUE reader, including 64 KiB, late EOF/cancellation and no-read oversize refusal. Source-mode temporary fixtures alter only owned private CUE/alias files, not supplied originals.

## Execution and retained evidence

```sh
dotnet build src/Spyro.Editor.NativeId65FreshDocumentResearchSmoke/Spyro.Editor.NativeId65FreshDocumentResearchSmoke.csproj -c Release
dotnet src/Spyro.Editor.NativeId65FreshDocumentResearchSmoke/bin/Release/net10.0/Spyro.Editor.NativeId65FreshDocumentResearchSmoke.dll
dotnet src/Spyro.Editor.NativeId65FreshDocumentResearchSmoke/bin/Release/net10.0/Spyro.Editor.NativeId65FreshDocumentResearchSmoke.dll --source ORIGINAL_BIN ORIGINAL_CUE
```

All logs are under `_local/research/native-id65-engine-profile-20260906-r01/`:

- `generic-document-plan-red-build.log`: intended missing-contract RED.
- `generic-document-plan-green-source.log`: retained malformed UTF8 exception RED after geometry/music successes; its initial filename does not mean it passed.
- `generic-document-plan-final-build.log`: Release 4.15s, zero warnings/errors.
- `generic-document-plan-final-free.log`: 35 checks passed.
- `generic-document-plan-final-source.log`: 2,339 checks passed, including the 35 source-free checks; all four full-image readbacks, 14 field alternatives, 46 music choices and hostile controls passed. Original BIN SHA `fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`, actual EOF/661,547,040 bytes and original 88-byte CUE SHA `05142502c75d3a0b6374003a8fe41893d6faf0ad55863ff0b62a265a1b152766` remained exact before/after.
- `generic-document-plan-independent-oracle.json`: independently repeated original/parity/legacy/variable golden evidence.

Derived expected hashes are not independently reviewed recipe certificates. No publisher, receipt generation, output transaction, normal Create BIN, installed app, emulator, card/settings change, project-save claim, hardware/runtime certification or postcommit-cancellation claim is supplied by this stage.
