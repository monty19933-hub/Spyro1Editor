# Source-native companion asset materializer

This is a byte-producing step beyond the [engine prerequisite contract](native-id65-engine-profile-contract.md),
not a new-level writer. `NativeId65CompanionAssetPlan.CaptureAsync(originalBin)`
uses that exact-USA issuer, then `CompileAssetBlock()` returns immutable native
resource fragments and typed reference/placement metadata. It writes no files,
patches no source image and grants no candidate, ID65 or runtime approval.

The source issuer hashes the same open original image before and after every
source read. The materializer uses only its detached captured bytes. A public
caller cannot provide a source hash, arbitrary byte array, class identity or
empty retail level to grant source authority. Re-compilation uses the same
immutable capture; this is not a later publication-freshness protocol.

## Actual emitted bytes

The native block is `0x5168` bytes (20,840), SHA-256
`0a7bcb56eaedded6878ae739aae17d8cb3857eaf74edec52e6f8c01a1667fb2a`.
No donor scene, terrain, collision, properties, fixup list or general texture
page is included.

| Compact block owner | Offset | Bytes | Meaning |
| --- | --- | --- | --- |
| `model` | `0000` | `2930` | Complete registered Town class0x78 allocation, with only declared texture/local-sound references rebound |
| `pixels` | `2930` | `0200` | One 32×32 4-bpp source tile, packed as 32 rows of 16 bytes |
| `clut` | `2B30` | `0020` | Its complete 16-color palette |
| `sound-descriptors` | `2B50` | `0028` | Two native 20-byte local sound descriptors |
| `sample-bank` | `2B78` | `25F0` | Both complete native one-shot ADPCM samples |

All numbers in the table are hexadecimal. Owners are aligned, contiguous and
nonoverlapping. The block is a detached container of native fragments, **not** a
native level, complete VRAM upload, sound component or SPU upload command.

The complete model allocation remains intact—including opaque bytes/padding—
except 36 texture descriptors and the two local sound selector bytes. All six
15-frame animations and their paired frame words are preserved. An inverse of
every allowed edit must recover the complete source package exactly.

The existing neutral indexed-texture allocator reserves a new detached VRAM
namespace: 32 pixel rows at relative offsets `row*1024`, and a disjoint 32-byte
CLUT at offset `0x20`. The 36 native model references use page `0x08`/palette `0x21`,
UV0..31. These are proposed fragment placements, not a claim that those
locations are available or sufficient in any existing or future level.

The package's original bytes `48/49` (hex) are **local descriptor indices**,
not keys in the 256-entry global sound map. They become indices0/1 only within
this block's two-descriptor namespace. Sample pointers become offsets0/C60
relative to this block's sample bank. No global map is emitted or changed.
Installing these fragments alongside player/engine sounds needs a separately
source-issued composition plan to allocate final indices and rebind them.

The public block exposes 76 typed references: 36 page/CLUT pairs, two local
descriptor references and two sample-bank offsets. It also exposes 33 exact
pixel/CLUT placements. Copy methods return detached arrays; exact block
readback rejects corruption, truncation and appended unowned bytes.

## Exact source resources

The source image and full Town data/EXE/overlay hashes are pinned by the engine
contract. The class0x78 package is WAD `1367C7C+2930`, SHA
`8e0d1cf58e8b164734f4aa3e91a60a1d04e78bb1f5e4cd5c4432b2e3e1e743f1`.

- Source model UV is U64..95/V96..127 (decimal), page `1D`, palette `7A36`.
  Pixel rows start at Town VRAM-relative `582A0`, stride `400`, width `10`
  bytes. Compact pixel SHA:
  `929c6ec41e8be9595ccf1ee140fb67c3f08be94cb65744c16231e56996124d6a`.
- The CLUT starts at VRAM-relative `7A2C0`, length `20`. SHA:
  `d76c613b9fe73bf5232c70143c622a61b3d7ba02c5a59b07692765182bb86656`.
- Local descriptors72/73 (decimal) occupy WAD `1310CB8+28`, combined SHA:
  `903b3ac048b6c70a5402e072a05f402a79ff9f024b595b4ece1e12bfaa647f9a`.
- Descriptor72 sample: WAD `1277CD0+C60`, SHA
  `78dde2c2a84b201d248a8e28afaaab83efc8cb615ab6d03d9c80c8a98edaa75f`.
- Descriptor73 sample: WAD `1278940+1990`, SHA
  `cf2648e50743f07a2117891bc1fc2f1b8289070109a1e11ad1a0c0ed3cf1b4b3`.

The complete bounded sound parser supplies descriptor and terminal-frame
ownership. The materializer accepts only its supported one-shot semantics,
preserves non-pointer descriptor fields and limits each selected sample to
64KiB before output allocation. It does not widen the separate Wizard-only
sound source adapter.

## Particle schemas are requirements, not completed effects

The contract binds actual Town tables at `8007ADC4` (spawn) and `8007AC80`
(tick), the exact selected case intervals, complete containing callbacks and
the complete native companion caller context. It does not infer callback
meaning from a non-null address or treat a particle selector as a texture ID.

| Selector | Native allocator kind | Spawn case | Tick case | Actual companion context |
| --- | --- | --- | --- | --- |
| `0C` | 2 | `800892C0..80089407` | `800881E8..8008825B` | Count1; a2 is class0x78 Moby; a3 is native selected packed color |
| `42` | 0 | `8008962C..8008978B` | `80088510..800885DB` | Two calls of count2; a2 is a world-position vector; a3=0; player status/RNG affect color |

Source-bound spawn/tick schemas are **not transitive resource closure**.
Native particle rendering/GPU consumers, shared helper/state ownership,
collection-color tables, RNG initialization, companion auxiliary state,
player-generated effects, pool capacity and reload/retirement remain explicit
pending requirements. There is no particle no-op or empty-pool approval.

## Verification and remaining writer boundary

Intentional RED: new smoke failed on14 missing new-type references; Core built
clean. Initial GREEN: Debug5.24s, zero warnings/errors,42 source-free and67
exact-source checks. The separate read-only Python oracle (no editor DLL)
independently parses native face commands and splices the complete original
package/resources; it reproduced the exact20,840-byte block and SHA above.
Final GREEN after the main agent's independent review: Debug1.53s, zero
warnings/errors,87 source-free and113 exact-source checks, followed by the
same independent Python oracle. The final smoke includes per-command UV/page/
CLUT arithmetic, complete model-byte inverse, owner hashes/offsets, strided
pixel/CLUT placements and a safe64KiB+16 sample-budget refusal fixture.
Final typed plan SHA:
`67036ef28624509eb4ee53ac554ee54480ca37474df036ea07c1e7bb6ba5421a`.
The main agent and a separate subagent independently reviewed the complete
materializer/particle contract and decoder linkage. Neither found a material
source-authority, byte-owner, resource-budget or detached-namespace issue.
Those static reviews did not constitute runtime or transitive effect closure.

```sh
dotnet build src/Spyro.Editor.NativeId65CompanionAssetSmoke/Spyro.Editor.NativeId65CompanionAssetSmoke.csproj -c Debug --nologo -v minimal
dotnet src/Spyro.Editor.NativeId65CompanionAssetSmoke/bin/Debug/net10.0/Spyro.Editor.NativeId65CompanionAssetSmoke.dll
dotnet src/Spyro.Editor.NativeId65CompanionAssetSmoke/bin/Debug/net10.0/Spyro.Editor.NativeId65CompanionAssetSmoke.dll 'Spyro the Dragon (USA).bin'
python3 src/Spyro.Editor.NativeId65CompanionAssetSmoke/source_asset_oracle.py 'Spyro the Dragon (USA).bin'
```

Raw evidence: `_local/research/native-id65-companion-assets-20260906-r01/`.
No old candidate, installed app, emulator or source ROM was changed.

A minimal writer still needs genuinely authored packed subfiles, the distinct
root0 player stream and all player/engine visual/audio consumers, a fresh
native registration/header producer, scene/arena/sentinel/props/fixup grammar,
complete engine state dependencies, memory ownership, ID65 save/music/arrival
composition and independent runtime acceptance. This materializer supplies
actual small resource bytes for that future writer; it does not pretend that
an existing retail merge API accepts an invented empty target.
