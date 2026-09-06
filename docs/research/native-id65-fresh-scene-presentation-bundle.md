# Fresh ID65 scene and presentation bundle

Source-bound byte stage, 2026-09-06. This emits actual fresh scene bytes and a
paired upload revision. It does **not** emit a native level header, retail
inventory, callback installation, disc plan or candidate. Runtime and publication
approval remain false. The existing normal-resource and zero-scene-list output
hashes and their earlier refusal boundaries are unchanged.

## API and authority

`NativeId65FreshSceneBundle.CaptureAsync(originalImagePath, issuedChain,
authoredSpawn, dynamicSlotBudget)` consumes the private source-issued
`NativeId65NormalResourceChain`. A new exact original capture binds the complete
image, native directory, EXE, Town resources and resident PETE package before and
after reading. It rebinds the chain's source executable and exact scene-list
prefix. Caller-provided bytes, hashes or declared native inventory cannot issue
this authority.

The source remains the 661547040-byte original USA image, SHA256
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
Complete EXE SHA256 is
`a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`.
Town's source scene header is WAD `136E000+88`, SHA256
`1ba95c48a81510329cacf0b50b2de56fdc134d777efdb8cdddd09984c968da02`.
Only its fifteen eight-byte presentation descriptors are retained; the sixteen
spawn/header-prefix bytes are freshly authored.

Outputs are detached copies from `CopyScene()`, `CopyUploadPayload()`,
`CopyDataComponents()` and `CopyModelPayload()`. The last two are byte-exact
upstream outputs. Typed scene owners, source/independent upload owners and
scene-relative runtime anchors make their domains explicit. The neutral
`NativeId65FreshSceneLayout` is not itself a source issuer. Its fixed-length
decoder re-encodes the complete scene before accepting it.

The only change to an earlier producer is an internal read-only `Resources`
getter on the chain, used to preserve its immutable ownership—including
referenced zero-valued pixels. No public mutable byte or metadata authority was
added. `NativeId65NormalResourceBundle.CopyDataComponents()` still refuses as
before; only the already accepted paired chain supplies those bytes.

## Actual fresh scene storage

The supported authored coordinate range is `0..262143`, with heading `0..255`
and an explicit dynamic-slot budget `1..32`. This is a storage/indexing grammar,
not a collision, grounded-spawn or future-movement certificate.

| Owner | Encoding |
| --- | --- |
| Spawn prefix | Three signed32-bit XYZ words; heading byte at `E`; `C/D/F` explicitly zero |
| Header descriptors | Exact source120 bytes at scene `10..87`; no descriptor rebinding |
| Seven empty scene lists | Existing exact `44`-byte paired prefix at scene `88`, including its three guard words |
| Moby component | At `CC`: length, placed count0, fresh dynamic arena |
| Dynamic arena | At `D4`: `70*N+58` fresh zero bytes |
| Placed properties | Length4, no imported properties |
| Numeric/pod list | Length8, count0 |
| Spatial Moby buckets | Length `2004` plus two complete1024-word zero-pointer banks |
| Fixups and padding | Raw fixup count0 and explicitly owned fresh sector padding |

For N16 the arena is `758` bytes and the padded scene is `3000` bytes.
Native loader `80013F24..14080` installs the upward58-byte row cursor and
downward18-byte property cursor. Its actual multiply/high-word computation
reports `floor((70*N+58)/70)=N`. The extra58 bytes are a complete next-row
reserve, not another claimed slot.

The actual allocator `800524C4..52564` is bound **through its return-delay
store**, length `A4`, SHA256
`6625282792e81215f1f35e1aa8b4240937206bcc8bd494eef9e000f65a4bbf9e`.
Tests execute allocations1..N at N1/2/16/32, protect every allocated row and
property owner, and execute N+1. That last call is rejected by the fixture's
ownership boundary because native writes are unsafe; the native allocator has
no added or claimed capacity refusal. N is not measured gameplay demand.

Actual unsigned coordinate indexing at `8005270C..52748` reaches all2048
bucket words in4096 boundary/bank cases. These are spatial linked-list tables,
not path data. Future dynamic coordinates remain a separate obligation.
The normal particle pool and PETE terminal-prefetch boundary remain external
native RAM owners and are not relocated into scene padding.

## Retained presentation and conditional unavailable slot

The new upload owns complete referenced4bpp/8bpp pages and mode-sized eight-row
CLUT banks in the native right-half VRAM domain. This is intentionally retained
shared presentation, not donor terrain or minimal blank-world completion.

Stored UV endpoints are not sufficient: native shadow output adds UV height,
and specular output replaces UVs dynamically. The specular fade path can select
CLUT rows0..7; its alternate path adds a bounded low-halfword offset before UV
replacement. The neutral decoder rejects unsupported tags/modes, left-half or
cross-row pages, out-of-domain palettes and low-UV carries into another CLUT.
All existing protected source and detached companion pixels—including zeros—
must survive exactly. Matching bytes do not authorize an independent alias.

The resulting source-owned VRAM union is104576 bytes. The complete Town SPU
bank remains unchanged; no audio minimization or new sound mapping is claimed.
The independent raw oracle also verifies that all355 resident player texture
references are contained in the expanded source page/CLUT union. Full native
GTE/frame/particle/GPU consumer closure remains false.

Source slot13 is exactly zero and remains **unavailable**, with no VRAM0 owner.
The immutable source-issued entry profile is specifically
`native-id65-fresh-normal-no-superflame-clean-usa-v1`:

- Loader `80013B68+14` calls the exact native fill helper `80016914+1C`, clearing
  78 words in `[800786C8,80078800)`, including active flag `80078764` and duration
  `80078768`. Tests execute the real call/return and protect both adjacent words.
- Renderer base/load at `80058DB4`/`80058E74` and selector `80059260+24` choose
  normal descriptor0 only when active is zero. Nonzero active reads slot13
  **without a zero-descriptor guard**; that is unsupported, not a valid texture.
- Both native flame-start paths are tested with zero/nonzero duration and
  arbitrary unrelated random return values. The first path's `s0=1` branch-delay
  initializer is executed; the alternate path initializes its own one.
- The zero-duration update branch is tested before frame-step subtraction.
  Saved duration can nevertheless be restored at `8002CC2C`/`8002D0D4` from
  `800758A4`. Checkpoint, foreign/restored state and any power-granting behavior
  are explicitly outside this profile. No all-state or superflame approval follows.

High Caves and Haunted Towers are genuine nonzero source donors for a future
superflame stage, but their additional pixel/CLUT and behavior closure has not
been materialized here. The broader engine's eight callbacks, companion effects,
all player/particle state consumers, fixed resident/transient/GPU memory and
ID65 resource/save/music/mode/rescue linkage are still separate prerequisites.

## Regression evidence

Evidence directory:
`_local/research/native-id65-fresh-scene-20260906-r01/`.

1. Intentional RED: `03-red-build.log`, exactly two missing-layout CS0246 errors,
   zero warnings,5.30s. Tests preceded production implementation.
2. First GREEN build passed; source-free292 checks passed. The first source run
   (`06-source.log`) exposed an oracle boundary omission: the old A0-byte
   allocator span stopped just before the return-delay store. The exact span was
   extended to A4, and both source certificate and oracle were updated.
3. Final focused Debug build: `07-green-build.log`,5.30s, zero warnings/errors.
   `08-source.log`:7123 checks. `09-source-free-final.log`:292 checks.
4. `10-raw-oracle-final.log`: independent Python assembly from exact raw sectors,
   no editor DLL or payload file. Both scene intents and the upload hash match.
5. Root independently reviewed the complete production files, scalar fixtures
   and raw-sector oracle, then built Release in5.07s with zero warnings/errors.
   The exact-source Release run passed7123 checks (`root-release-source.log`);
   a separate source-free run passed292 checks and the raw-sector oracle
   reproduced both scenes and the upload. This is focused regression evidence,
   not a repeat of the preceding275-step full gate or emulator acceptance.

Commands, from the repository root:

```sh
dotnet build src/Spyro.Editor.NativeId65NormalResourceSmoke/Spyro.Editor.NativeId65NormalResourceSmoke.csproj -c Debug --nologo
dotnet src/Spyro.Editor.NativeId65NormalResourceSmoke/bin/Debug/net10.0/Spyro.Editor.NativeId65NormalResourceSmoke.dll
dotnet src/Spyro.Editor.NativeId65NormalResourceSmoke/bin/Debug/net10.0/Spyro.Editor.NativeId65NormalResourceSmoke.dll 'Spyro the Dragon (USA).bin'
_local/tools/capstone-venv/bin/python src/Spyro.Editor.NativeId65NormalResourceSmoke/source_fresh_scene_oracle.py 'Spyro the Dragon (USA).bin'
```

| Exact output | SHA256 |
| --- | --- |
| Scene, spawn8960/8960/9216 heading64 | `4a6e7b5c6c53199b8a7aa6ae0d24ac9bce32634aa259d5f49535282080641433` |
| Scene, spawn8976/8960/9216 heading128 | `268b36fdb7136d533c01a93a71fd62a71bc9ec58799558698b14ba1f21b18380` |
| Expanded upload, both intents | `b769a9a298285aa402956fdbd5721132736388c6f3f1dc1bfcd7e15a9afeb66d` |
| Default plan fingerprint | `384b5b1ae255f99f5c9587383cb5083a047defd06e1469ee578f03eeed69a8af` |

Repeat capture is deterministic; a second authored surface changes only its
issued data and chosen scene spawn. The earlier upload/model/tail and paired
data/list goldens remain checked in the same smoke. Corruption, missing owners,
truncation, invalid native widths and aliases refuse. Unsupported states are
excluded from the issued capability; this stage does not install a runtime guard.

Independent peer review covered all three Core files, the underlying VRAM owner
assembly and the native-entry/interpreter fixture, with no material issue found.
The bounded instruction oracle is not a native CPU/GPU emulator or gameplay test.
No source ROM, candidate, installed editor, emulator state or settings were changed.
