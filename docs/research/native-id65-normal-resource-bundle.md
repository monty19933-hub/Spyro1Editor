# Detached ID65 normal resource integration

This stage emits actual upload/model/resource-tail bytes from the fresh authored
surface and source-issued companion plan. It does **not** emit a level header,
scene, overlay, retail inventory, disc patch or publishable ID65 level. Existing
ID65 schemas, receipts, writer guards and candidates are unchanged.

`NativeId65NormalResourceBundle.CaptureAsync(originalImagePath, surface, companion)`
reopens the original source, hashes the same open image before and after its
reads, and rebinds both issued inputs to the captured executable/Town assets.
Neutral surfaces, absent companions, modified source owners or complements are
refused. Source certification describes this frozen capture, not subsequent
publication freshness or runtime acceptance.

## Actual emitted owners

- `CopyUploadPayload()`: `0xDE000` bytes. Its first `0x80000` bytes form a freshly
  populated right-half VRAM upload. Exactly **11,424 source VRAM bytes** cover the
  resident/Town-local player texture union and the retained particle frames;
  the issued companion supplies an additional512 pixel bytes and32 CLUT bytes
  at disjoint new locations. All other upload bytes are explicitly owned fresh
  zeros. Referenced zero-valued pixels are protected, not treated as free space.
  The final `0x5E000` bytes retain the complete Town SPU bank, including padding.
- `CopyModelPayload()`: `0xF800` bytes. Complete Town special root0 stream
  `0xCC30`, followed by the issued companion's complete `0x2930` allocation,
  followed by explicitly fresh `0x2A0` sector padding. The only companion
  integration mutations are local sound bytes+4/+5, detached0/1→native72/73.
  The sound descriptors/sample bytes are matched before linking, and inverting
  those two bytes recovers the entire issued package allocation exactly.
- `CopyDataComponentTail()`: `0x8C0` bytes for the default authored floor.
  Five actual authored surface components are followed by a raw zero portal
  count, Town's ten-record `0x80` particle component and complete `0x6F8` sound
  component. This is an **environment-through-sound tail**, not subfile1.

The public owner table partitions every emitted byte, including newly zeroed
storage. Every retained source owner has its actual WAD address and hash.
The model root table distinguishes the special player stream from generic
class`0x78`; it uses **model-relative offsets**, not finished native header
words. The final generic registration would span `0x2BD0` bytes through the
new padding. Its allocation hash is separate from the preserved `0x2930`
package hash; no fresh padding is mislabeled as a source package.

This deliberately retains a **complete Town audio baseline**. It is not a
minimal player sound set, blank audio, or retained Town terrain/scenery.
The original256-byte global map stays unchanged: selector0 maps to descriptor17,
selector1 to descriptor0; the companion's local indices72/73 bypass that map.
Native generic local resolution is bound at`8003851C+68`; player frame selectors
use global sound playback in`8004A638+10C` instead.

## Why the complete data chain refuses

`CompleteDataChainAvailable` is false, and `CopyDataComponents()` throws. No
eight-byte empty terrain texture component is manufactured.

The source loader at`80012D58` reads a texture count and installs pointers into
`g800785C0/C4`, with the count in`g800785C8`. That is insufficient proof of
count-zero use. Native texture animation and scrolling use **separate scene
lists** at`g80078560`: the ordinary updater's guards are`8002A718` and`8002A850`,
and the reset updater's guards are`8002B4C4` and`8002B57C`. Each guard's delay slot
still loads the first list word. The bodies then index or write the terrain
metadata through`g800785C0/C4` without checking its count.

The fresh surface's exact direct-FF-Gouraud renderer path avoids texture
metadata, but it cannot authorize unrelated scene updater inputs. A future
source-issued scene producer must own the zero animation/scroll counts, their
pointer bases, their readable speculative guard words and the corresponding
initialization/reset consumers before empty terrain metadata can be linked.
The existing resource bytes remain useful while that missing output is refused.

## Fixed player and particle resources

PETE's external shared player package remains unchanged at original WAD
`7B9000+32E1C`, SHA
`46f570bf0ad2d433f8b42874ae40a3e5d7155d7206dddd1cd08a2d4be16e0e76`.
Its geometry base is package+AA8. The22 resident animations and Town's local
IDs18/19/29/40 resolve one actual face-command table there:355 texture references,
page`1F`,17 CLUT IDs. The assembler preserves the native physical VRAM positions
and does not patch PETE GPU commands or pretend local root0 is generic geometry.
UV rectangles conservatively own whole bytes, including a partially referenced
nibble. Blend bits remain part of the descriptor while mapping to the same
physical page. Only the same captured source address may alias; unrelated
detached owners cannot alias merely because their bytes happen to match.

Town's exact ten particle descriptors are IDs`15,18,02,21,0C,0B,0A,09,01,00`
(hex), each an eight-byte frame plus a four-byte signed ID/length header.
Their four physical32x32 rectangles/CLUTs are copied at native addresses; the
descriptor bytes, including blend flags, remain unchanged. `CopyParticleFrame`
only accepts frame0 from these actual owners; frame1 cannot read into the next
descriptor, and ID64 cannot alias the player model pointer table.

The native kind2 renderer at`80057C08` advances its raw cursor by`0x20`, then
reads the halfword corresponding to raw+`0x10/11`. The low byte is the frame
index multiplied by8; the high byte is depth bias, not another frame bit.
For companion selector`0C`, spawn`800892C0+148` unconditionally writes zero to
raw+`0x10` at`80089404`. Its tick`800881E8+74` uses `s0=raw+1` from the callback
prefix and does not write the frame byte. Other retained player particle
spawn/tick state closures are not promoted by merely copying their descriptors.

The external normal particle pool, shared PETE decoded roots and renderer
state are separate runtime owners. In particular the native sentinel's
speculative next-header read reaches the resident PETE header; an eventual
fresh scene/RAM producer must preserve that deliberate shared boundary rather
than allocate only a terminal word. Full variable player-frame decoding,
complete player states, particle retirement/reuse, callbacks, scene, rescue/
replay/transition modes, RAM and publication remain open.

## Exact source and regression evidence

Original BIN length661547040, SHA
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
SCUS root LBA53875, length`66000`, SHA
`a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`.
Town full data WAD`119E000+2E2000`, SHA
`7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0`.
All source consumer spans and their exact EXE/WAD offsets/hashes are retained
in `NativeId65NormalResourceSource` and emitted as evidence; no cached label
alone issues source authority.

Durable logs: `_local/research/native-id65-normal-resources-20260906-r01/`.
The initial Debug RED built Core cleanly and failed only nine intended missing
API references, zero warnings,5.03seconds (`01-red-build.log`). The independent
raw-sector Python oracle passed (`02-independent-oracle.log`), using no editor
DLL and writing no ROM/payload. Serialized Debug GREEN completed in5.08seconds,
zero warnings/errors (`03-green-build.log`). The built smoke passed32
source-free checks (`04-green-free.log`) and966 exact-source checks
(`05-green-source.log`), including two authored surface intents and repeat
source capture. Huygens separately read all three Core files and found no
material issue; that review was static, not an independent .NET/runtime run.

Exact targeted commands, from the release repository:

```text
dotnet build src/Spyro.Editor.NativeId65NormalResourceSmoke/Spyro.Editor.NativeId65NormalResourceSmoke.csproj --configuration Debug --verbosity minimal
dotnet src/Spyro.Editor.NativeId65NormalResourceSmoke/bin/Debug/net10.0/Spyro.Editor.NativeId65NormalResourceSmoke.dll
dotnet src/Spyro.Editor.NativeId65NormalResourceSmoke/bin/Debug/net10.0/Spyro.Editor.NativeId65NormalResourceSmoke.dll "Spyro the Dragon (USA).bin"
python3 src/Spyro.Editor.NativeId65NormalResourceSmoke/source_resource_oracle.py "Spyro the Dragon (USA).bin"
```

Independent payload SHA256 values:

| Payload | SHA256 |
| --- | --- |
| Upload | `ea5e5e56b7ba35abaaa55f7fc3cabe388cc64a47a00a5a07b8d72fecd475e1e4` |
| Model | `88c78343c283256de6ce8d3151b43fa72ba4d96b046d869bd0a7209cbff4c23d` |
| Default floor tail, RGB708050 | `a862c007087cfc2281e6a3cc1426794d335d96717c8f53f1d24fd2026c039a56` |
| Second floor tail, RGBE03040 | `3c213c148f01a5ddea5b7a1210c1882fca2349fd429b773b8248efa0821dea89` |

Two distinct authored floor intents share exactly the same integrated player,
companion and audio payloads while producing distinct fresh surface bytes.
Resource/frame/schema/work bounds, alias refusal, complete source corruption,
sound domain traps, actual owner readback and repeated capture are covered by
the focused smoke. These are engineering checks, not gameplay acceptance.
