# Source-owned append-only Wizard scene tests

This isolated tests-first project verifies the implemented bounded scene seam.
It is registered in the full runner and emits no native candidate.

Executed RED on 2026-09-06: Debug build `5.25s`, zero warnings, nine expected
CS0103/CS0246 errors only for the three absent API types. Existing Core compiled
cleanly. Log: `_local/research/native-moby-owner-ledger-20260906/08-wizard-scene-red.log`.

Initial GREEN: Debug `5.22s`, zero warnings/errors; source-free **4** checks
(one refusal), exact-source **50** total checks (twenty refusals). The four
source-free checks cover only the closed public constructor/intent/source
boundary; actual scene/order/temporal/capacity semantics require exact source.
Logs `10-wizard-scene-green-build.log`, `11-wizard-scene-source-free.log` and
`12-wizard-scene-source.log` are retained in the same evidence directory.

Public intent is only a stable family ID. The new source-issued sibling chooses
the actual Wizard Peak33/T6 Wizard row, complete private properties and native
fixups. Tests require temporal z128 -> serialize/reopen -> a129 while retaining
z128, order-independent active requests, exact active-family bijection, no
retired-row emission, source/role/schema rebinding and atomic failure. Every
donor coordinate stays unchanged: placement, runtime and publication are false.
App persistence/undo, accepted export history and legacy migration are separate,
unimplemented integration gates.

The raw standard-library Python oracle reads only the exact original image and
independently reconstructs one/two/nineteen native scene splices without Core. It asserts
the original one-Wizard golden and emits two-Wizard SHA-256
`0766f7e16adc2a58ccbc2797d6aba0d2ca4cd5ef5d788ee99599f27e9bd78fb7`.
It verifies the entire source image before and after. This is byte evidence,
not native execution or authenticated family/export history.
The raw oracle passed before production implementation. Its first local draft
incorrectly required the unrelated donor scene tail to be zero; that expectation
was removed because the new stage copies only the certified donor row/private
owners. The actual destination zero tail is still required before/after splice.
The exact tail admits nineteen families, leaving `0x38` zero bytes; the twentieth
would exceed it by `0x20` bytes. The separate128-family negative exercises row
capacity first. Nineteen-family SHA-256 is
`7567f3b068447b2d216c0869d5314682b019e8d013790377c309e28a47c4da66`.

```text
dotnet build src/Spyro.Editor.NativeWizardAppendSmoke/Spyro.Editor.NativeWizardAppendSmoke.csproj -c Debug --nologo
dotnet src/Spyro.Editor.NativeWizardAppendSmoke/bin/Debug/net10.0/Spyro.Editor.NativeWizardAppendSmoke.dll
dotnet src/Spyro.Editor.NativeWizardAppendSmoke/bin/Debug/net10.0/Spyro.Editor.NativeWizardAppendSmoke.dll --source "Spyro the Dragon (USA).bin" .
python3 src/Spyro.Editor.NativeWizardAppendSmoke/source_wizard_append_oracle.py "Spyro the Dragon (USA).bin"
```

Builds require the root agent's serialized slot. Existing compiler API/goldens
passed unchanged in the source mode. The runner invokes the no-argument boundary
mode by default and `--source <selected image> <workspace root>` only with
`--with-source-qa`; no CUE, publish, fault or emulator mode is registered.
Registration tests first failed at the two missing entries; the complete Python
runner suite then passed **25** tests, including missing free/source-marker
refusals, no default source access and exact opted-in arguments. Logs13/14 are
retained beside the native scene logs. Root/peer source reviews are clear;
Release/full-gate checkpointing remains root-owned.

Later native naming cross-check corrected the reversed donor names in this
stage's diagnostics and notes: ID33/WAD40 is Wizard Peak; ID34/WAD42 is Blowhard.
The numeric donor, all source/scene/plan identities and existing payload goldens
are unchanged. Two added assertions bind the original EXE's title pointers and
strings to the corresponding source-catalog owners. Focused Release validation
passed52 source checks (20 refusals), plus the unchanged4 source-free checks.
The dependency-inclusive build passed5.71s with zero warnings/errors; it also
compiled the then-new fresh-disc Core files, but did not run their test suite.
Logs: `_local/research/native-moby-owner-ledger-20260906/native-donor-label-{build,source}.log`.
