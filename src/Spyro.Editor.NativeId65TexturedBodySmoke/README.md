# Source-issued textured normal staging/body

This isolated smoke exercises a closed composition path from the existing
source-issued fresh scene through its two-static-texture revision, transfer
source and full native body. It adds no writer, public byte/hash authority,
ordinary exporter, App control or native runtime approval.

The actual recipe remains Dark Hollow WAD14/T0 plus Gnasty's World WAD70/T0,
the two existing fresh HP triangles, all184 bytes per native record, all16 LQ
palette rows and clip0x8000. Complete HQ resources are retained, but HQ execution
and full-frame packet/GTE/GPU demands remain unapproved.

## Closed composition

An internal sealed `NativeId65NormalResourceRevision` can be created only from
actual private `NativeId65FreshSceneBundle` or `NativeId65FreshTerrainTexturePlan`
issuers. It copies all four byte domains and retains the real fresh ancestor's
entry profile/evidence plus the texture plan's exact source handles and six
native consumer records. No raw input, metadata hash, caller-provided profile,
neutral allocator or implemented interface can substitute for either issuer.

Existing `CaptureAsync` calls are unchanged. New named factories avoid ambiguous
null overloads:

```csharp
var stage = await NativeId65NormalUploadStaging.CaptureTexturedAsync(original, texture);
var body = await NativeId65NormalBodyPlan.CaptureTexturedAsync(original, texture, stage);
```

Both use the existing transfer kernel and body assembler. The read-only
`ResourceProfile` distinguishes their actual private byte revision. Old plan
serialization remains exactly unchanged; only the new textured branch adds
its own profile/source evidence payload. Existing `NativeId65FreshDiscPlan`
explicitly rejects the textured profile before source I/O. This is not textured
disc support.

## Why transfer hash alone is insufficient

The new full upload differs, but this allocation changes neither the retained
SPU bank nor the previous VRAM residue used by the transfer source. Consequently
the entire0x7F000 transfer image stays byte-identical to the old one. A second
fresh scene also has equal upload/transfer bytes but different spawn/heading and
complete scene identity. Both cases must reject cross-paired stages by the actual
upstream plan, full upload identity and source-private resource profile.

Used data grows0x8C8→0xA38 while the CD data allocation stays0x1000. Source-derived
model, scene and known scene-end addresses move together by0x170, to8008A904,
8009A104 and8009D104 for the16-slot scene. Data padding shrinks0x738→0x5C8.
These known extents do not prove peak memory or allocator exhaustion safety.

The full body remains0x1FB800. The independent raw oracle pins two outputs:

- `347ab532881afb3975088e0872792192d8567b0abe05e7708f271741b9568cdd`
- `34d8d4c7062c0f84ef14bbbd22f66ba3ae33e68116d0877c82f4517f21622ea5`

Every header/model/scene/auxiliary/overlay byte remains exact; only the complete
new upload/data owners and their data padding are substituted. The old staging
PlanSha remains `98bd43761b87de15d2915d1377e0298ce7a0f133d2ee07c2f5672bad503af000`;
the old body PlanSha remains `48f1f0b4f7740c8a6277a4b440990e1fd26cb609db2153c3c24fc61d70dbbb94`.

## Retained RED → GREEN evidence

The raw-source oracle and mandatory JSON golden existed before Core changes.
Its full original-image pre/post readback,16 native body source windows and two
old-body goldens pass independently of the editor DLL. Its upstream allocation
oracle has an explicit immutable bytes/tuple/string return API; default CLI
output and prior golden remain unchanged.

- Actual tests-first RED:25 intended missing-contract errors,0warnings,0.49s
  build/0.59s wall, Core references not rebuilt.
- First GREEN:3.91s build/4.01s wall,0warnings/errors,18free+135source=153.
- Final strengthening: exact ordered phase/transfer-owner inventories plus
  complete source-stage/fresh-ancestor/six-window evidence sequences. Final
  Release0.49s,0warnings/errors,18free+139source=157.
- Unchanged legacy NormalBody:689free/791source-inclusive; legacy NormalResource:
  327free/15423source-inclusive. Both targeted builds were clean.

No golden was revised after implementation. Logs are under
`_local/research/fresh-native-textures-20260906/`, prefixed
`textured-body-red-`, `textured-body-green-`, `textured-body-final-`,
`textured-body-legacy-body-` and `textured-body-legacy-resource-`.

Root independently read the complete adapter and all six production diffs;
a second complete read-only review found no material issue. The registered
default Release gate passed109 steps, zero failures:54 builds including App,
54 modes and one33-test Python runner step;67.67 seconds summed. Complete Python
discovery separately passed47 tests after the new missing-registration RED.
The complete default report was copied and byte-compared in
`textured-body-default-report/` beside `textured-body-default-release-gate.log`;
its JSON log paths still identify the original temporary report.
After the gate rebuilt the legacy fresh-disc test against this exact Core,
its source run again passed566016 checks, all three physical hashes and both
whole raw inverses (`textured-body-legacy-fresh-disc-source.log`). This default
gate plus targeted source runs is not a new full build-all/source-QA gate.

## Reproduction

From repository root, with an explicitly granted serialized build slot:

```sh
dotnet build src/Spyro.Editor.NativeId65TexturedBodySmoke/Spyro.Editor.NativeId65TexturedBodySmoke.csproj -c Release
dotnet src/Spyro.Editor.NativeId65TexturedBodySmoke/bin/Release/net10.0/Spyro.Editor.NativeId65TexturedBodySmoke.dll
dotnet src/Spyro.Editor.NativeId65TexturedBodySmoke/bin/Release/net10.0/Spyro.Editor.NativeId65TexturedBodySmoke.dll --source 'Spyro the Dragon (USA).bin'
python3 -B src/Spyro.Editor.NativeId65TexturedBodySmoke/source_textured_body_oracle.py 'Spyro the Dragon (USA).bin'
```

Source-free marker: `NATIVE_ID65_TEXTURED_BODY_FREE_OK`.
Explicit original-only marker: `NATIVE_ID65_TEXTURED_BODY_SOURCE_OK`.
The original argument is read-only; no Workspace, CUE, candidate directory,
publication mode or source-ROM fixture mutation exists.

Runtime, hardware completion, SPU wrap/low16 ownership, complete resource closure,
peak allocation, foreign entry/checkpoint, rescue/replay and publication flags
remain false. Source composition and independently verified bytes do not prove
what DuckStation or original hardware will render or play.
