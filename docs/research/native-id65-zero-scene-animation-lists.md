# Source-issued empty scene lists and paired normal resource bytes

This stage produces actual deterministic data-component bytes and the required
seven empty scene-list components. It does not produce or initialize a complete
scene, native level header, overlay, resource directory, RAM layout, ID65 engine
profile, or disc candidate. Runtime and publication remain false. Existing
`NativeId65NormalResourceBundle.CopyDataComponents()` still refuses, and its
upload/model/tail outputs are unchanged.

## Exact source and native consumers

`NativeId65ZeroSceneAnimationLists.CaptureAsync(originalImagePath)` captures the
complete original USA image before and after reading the native executable. It
uses the existing source reader, requiring 661,547,040 bytes and image SHA-256
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
SCUS is the exact ISO directory owner at LBA 53875, length `66000`, SHA-256
`a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`.
EXE file offset plus `8000F800` is the runtime address in this source.

| Consumer | Runtime span | EXE file span | SHA-256 |
| --- | --- | --- | --- |
| Seven-list loader and reset selection | `80013C40+2E4` | `4440+2E4` | `3d5c033d15f4e510c4326b9699c0b4b6d5def3cd3c288f528e0c7f823274b80e` |
| Ordinary updater | `8002A6FC+C94` | `1AEFC+C94` | `040a3b6ab61a418501453135f4b7cf41b94e57081377f301730f93d68970166b` |
| Reset updater | `8002B4AC+520` | `1BCAC+520` | `b71153d2ffb77871623e5f3f85e9adb9e4793d66368eee9d30b7fd96bef74354` |
| Ordinary update call before level callback | `80033A50+60` | `24250+60` | `12f23f264ca3b413c8a05ecba7a428f56bff1f127b6ad24f734f60bd002d7e97` |
| Texture metadata count/pointer loader | `80012D58+54` | `3558+54` | `25aede725485af9da35013da9c1adbcb8d4f336c588c272e29edb6c7115aca0c` |

The scene loader physically walks the following order, which is not numerical
global-address order. Each component stores its byte length and an exact zero
count in its first two 32-bit words. The first-entry pointer is component+8,
relative to scene+88 after the separately required fixed scene header.

| Index / component | Prefix offset / bytes | Count / pointer globals | Owned zero guard |
| --- | --- | --- | --- |
| 0 / texture animation | `00 / 0C` | `80078560 / 80078564` | `scene+90` |
| 1 / texture scroll | `0C / 0C` | `80078568 / 8007856C` | `scene+9C` |
| 2 / numeric list 20 | `18 / 08` | `80078580 / 80078584` | none |
| 3 / numeric list 10 | `20 / 08` | `80078570 / 80078574` | none |
| 4 / collision animation | `28 / 0C` | `800785A0 / 800785A4` | `scene+B8` |
| 5 / numeric list 28 | `34 / 08` | `80078588 / 8007858C` | none |
| 6 / numeric list 18 | `3C / 08` | `80078578 / 8007857C` | none |

The prefix is exactly `44` bytes, advancing the loader from scene+88 to scene+CC.
Three guards are necessary despite zero counts: ordinary updater BEQ instructions
at `8002A718`, `8002A850`, `8002AD68`, and reset BEQs at `8002B4C4`, `8002B57C`,
`8002B7D0`, all execute `LW t5,0(t6)` in their branch delay slots. The other four
zero-list paths do not read an entry. Their nonzero paths can rewrite environment
sector index bytes; the collision list can rewrite triangle data. All seven
lists, not just animation and scroll, therefore belong to this pairing.

The test entry also carries the preceding scene-header word in `v0`:
`80013C5C` loads scene+84, the second packed word of the final eight-byte
Tiledef at scene+80 (called specular-metal by the existing scene parser).
`80013C70` stores that word to `800756F4`, then `80013C74` replaces `v0` with
the first list count. This asset does not own or require a zero value for that
header field. The test preserves zero and nonzero incoming values exactly and
unmaps the global before executing either updater, so a hidden dependency on
the initially zero test mapping cannot pass.

Zero is a deliberately narrow grammar. Negative signed counts are rejected even
where a native loader BLEZ would happen to skip a loop. Missing guards, unknown
lengths, nonzero guard bytes and trailing storage are also rejected. The zero
list object has no public constructor; its internal neutral form cannot satisfy
the paired producer's source authority check.

## Actual paired output

`NativeId65NormalResourceChain.Compile(sourceIssuedResources, sourceIssuedLists)`
requires the same full captured executable as the source-issued fresh surface.
It prepends exactly `[length=8, count=0]` texture metadata to the old resource tail.
The surface remains the supported FF untextured Gouraud producer with four
negative scene-index sentinels; no texture index 0 or donor terrain is invented.
The empty lists prevent the bound animation consumers from dereferencing that
absent metadata or overwriting the fresh sentinels/collision geometry.

The data chain has contiguous typed byte owners for metadata, environment,
visibility, special collision, collision, cyclorama, raw zero portal count,
retained particle descriptors and retained complete Town sound component.
`RequireExactPairedBytes` binds both complete outputs, including all guards and
the entire inherited data-tail complement. Upload and model copies are delegated
to the immutable source-issued resource bundle and remain byte-exact.

Default authored floor `(8448,8448,8704)`, size `1024×1024`, RGB `708050`:

- Data components: `8C8` bytes, SHA-256 `821d25d4c012aa180d369f755d82a780dbcf244b4e53c6c4bcf349da533dce65`.
- Scene-list prefix: `44` bytes, SHA-256 `b9ee5eb6825630fd5d4ede0bba2a140552191a4e8edf369ddc4aacf36fe09f60`.
- Plan SHA-256: `886c9d850a2159e6dfd8ed235aa9c3080f90a89f322a8714f834147c780de078`.
- Alternate floor RGB `E03040` data SHA-256: `e1759617b8312795595d0b1229565e68725739404b4416641a9ba408cf512ddd`; identical scene-list bytes.

The typed placement requirement is still unsatisfied until a later producer
places this exact prefix at scene+88 and supplies the remaining scene structure.
Shared PETE/player geometry, native pool/PETE overlap, complete player/particle
state consumers, companion effects/callbacks, transient GPU/RAM, no-rescue and
transition closure remain external or pending. Complete Town audio is explicitly
retained; this is not minimal-audio or blank-level completion.

## Regression evidence

Evidence directory: `_local/research/native-id65-zero-scene-lists-20260906-r01/`.

- `01-red-build.log`: intended missing scene-list type only, two CS0246 errors,
  zero warnings, 1.29 seconds; Core compiled.
- `02-independent-oracle.log`: retained failed test-oracle assertion assuming
  BLEZ for the updater's zero path. Direct source inspection showed BEQ; no
  production bytes changed. The corrected oracle pins actual `LW t5,0(t6)`.
- `03-independent-oracle.log`: independent Python raw-sector/native-field oracle
  reproduces both data hashes and list hash without the editor DLL or output writes.
- `04-green-build.log`: targeted Debug build, zero warnings/errors, 5.00 seconds.
- `05-green-free.log`: `NATIVE_ID65_NORMAL_RESOURCE_SMOKE_OK checks=139`.
- `06-green-source.log`: `NATIVE_ID65_NORMAL_RESOURCE_SOURCE_OK checks=1305`;
  old upload, data-tail and model SHA goldens unchanged.
- `07-final-build.log`, `08-final-free.log`, `09-final-source.log`: after adding
  the unrelated scene+84 passthrough/nonzero and unmapped-global regression,
  Debug 4.97 seconds, zero warnings/errors, **139 source-free / 1,307 source
  checks**. All payload and plan hashes remain unchanged.

The bounded test instruction oracle executes the actual captured native loader
with both reset modes, then both actual updater entry points for six unsigned
frame values including signed extremes. It honors R3000 load delays and branch
delay slots and has independent delayed-load self-tests. Only the exact list
prefix, seven global pairs and the loader's prior header global are mapped
during loading; the header global is then removed before updater execution;
terrain metadata, collision targets and other scene memory are deliberately
unmapped. Every updater must make zero writes, and removing each guard must
cause an unowned-read rejection. This is execution of those bounded instruction
paths, not a general MIPS/GTE/GPU emulator or gameplay test.

Commands, from the repository root:

```sh
dotnet build src/Spyro.Editor.NativeId65NormalResourceSmoke/Spyro.Editor.NativeId65NormalResourceSmoke.csproj -c Debug
dotnet src/Spyro.Editor.NativeId65NormalResourceSmoke/bin/Debug/net10.0/Spyro.Editor.NativeId65NormalResourceSmoke.dll
dotnet src/Spyro.Editor.NativeId65NormalResourceSmoke/bin/Debug/net10.0/Spyro.Editor.NativeId65NormalResourceSmoke.dll 'Spyro the Dragon (USA).bin'
_local/tools/capstone-venv/bin/python src/Spyro.Editor.NativeId65NormalResourceSmoke/source_scene_list_oracle.py 'Spyro the Dragon (USA).bin'
```
