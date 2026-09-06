# Native ID65 detached normal body and retained auxiliary profile

2026-09-06. This additive stage emits an actual fresh relative header and normal
resource body, together with an unchanged, separately copied Town engine overlay.
It does **not** install that engine, create a native directory entry, patch the EXE,
write a ROM/candidate, or approve ID65 runtime. All earlier producers, byte goldens,
save/music/classifier/writer gates and conditional entry restrictions are unchanged.

## Source-issued API and actual bytes

`NativeId65NormalBodyPlan.CaptureAsync(originalBin, issuedFreshScene, issuedStaging)`
requires the constructor-closed fresh-scene and normal-upload stages. It binds
their complete plan identities, the paired upload hash and the original image
identity. A staging object from another scene intent is rejected even when its
transfer bytes happen to be identical. The private source capture reuses the
complete original image checks before/after, exact EXE/Town/PETE capture and engine
issuer, and additionally binds the complete overlay, four auxiliary allocations
and eighteen exact loader/reset/frame/rescue consumer spans.

`CopyLevelData()` and `CopyEngineOverlay()` return independent copied arrays.
`RequireExactPayloads()` checks complete lengths and every byte, including header
zeros, sector padding and the original overlay tail. Public descriptors, roots,
owners, callbacks and phase lists are immutable snapshots. The internal neutral
packing kernel accepts bounded synthetic test inputs but cannot issue source
authority; the public API accepts no arbitrary payload or callback metadata.

For the established sixteen-row fresh scene, the body is `0x1FB800` bytes:

| Header descriptor | Body-relative offset | Length | Owner |
| --- | --- | --- | --- |
| Header itself | `0` | `0x800` | Fresh fields and explicit zero complement |
| 0 | `0x800` | `0xDE000` | Paired VRAM upload and complete retained Town bank |
| 1 | `0xDE800` | `0x1000` | Fresh data `0x8C8`, then fresh zero CD padding `0x738` |
| 2 | `0xDF800` | `0xF800` | Special player stream and registered generic companion |
| 3 | `0xEF000` | `0x3000` | Fresh scene, guarded zero lists and authored dynamic arena |
| 4 | `0xF2000` | `0x49800` | Complete original Town auxiliary 0 |
| 5 | `0x13B800` | `0x42800` | Complete original Town auxiliary 1 |
| 6 | `0x17E000` | `0x5D000` | Complete original Town auxiliary 2 |
| 7 | `0x1DB000` | `0x20800` | Complete original Town auxiliary 3 |
| 8, 9 | `0` | `0` | Source-absent slots; not native selector guards |

The header's root words are entry-relative: root0 `0xDF800`, root1 `0xEC430`.
The corresponding generic class uses the native halfword at header `0x152`,
value `0x78`. Root0 owns `0xCC30` bytes. Root1 distinguishes payload `0x2930`
from its complete native registration allocation `0x2BD0`, including retained
padding through the model CD allocation end. No ordinary-class interpretation
is imposed on special root0. All other root/class fields and the remaining header
complement are explicit zeros. Capacities 1, 2, 16 and 32 retain the existing
canonical scene grammar; subsequent auxiliary offsets follow actual scene size.

The four original auxiliary source offsets within Town data are `0x1D8800`,
`0x222000`, `0x264800`, `0x2C1800`. Each complete allocation is retained byte-exact,
including its `0x24` header, audio, both model owners, 24-byte-stride track rows
and sector padding. None is presented as authored data or minimal player data.
Normal Town terrain and placed scene rows are not inherited. Retaining these
auxiliaries does not prove rescue/replay closure, linked-row validity or selectors
4 and above. The structural selector check resolves only the four retained owners;
it emits no native guard and does not authorize entering those modes.

## Initializer and callback lifetime are requirements, not installed code

The normal caller at `800155E4..156B4` selects the resident initializer before
the incoming overlay read. The fixed Town initializer is `8005A898`; overlay
destination is `8007AA38`, allocation length `0xF800`. Its nine actual setters
bind copy anchor `g800785D8 = 80089ECC` and all eight callback slots:

| Global | Incoming Town entry | Source-bound role |
| --- | --- | --- |
| `800758CC` | `800872A4` | Moby spawn |
| `80075734` | `8007DA54` | Active Moby and animation tick |
| `800758E4` | `80088F68` | Particle spawn |
| `800756BC` | `80087E20` | Particle tick |
| `800757A0` | `8007AF2C` | Balloon transition begin |
| `800758A8` | `8007B068` | Balloon transition end |
| `8007574C` | `8007B0AC` | Balloon transition tick |
| `800758D8` | `8007D04C` | Balloon transition render |

The plan binds each complete callback code allocation below the copy anchor.
`CallbacksInstalled` and `NoOpCallbacksAuthorized` remain false. The existing
retail ID65 initializer default does not replace these stale callback values.
Merely copying an overlay cannot solve the resident initializer dispatch.
Skip-overlay entry and checkpoint/foreign-state entry are not supported profiles.
The native scene initializer still performs the mandatory null-source class78
spawn; zero authored Moby count does not justify removing its closure.

The cold-boot BSS reset is distinct from ordinary transition reset. The native
ordinary scene reset preserves state12; the tests exercise that counterexample,
not an invented all-state reset. Particle and companion auxiliary pools are reset
before null-source78 setup. The auxiliary allocator's seventeenth request returns
null after sixteen successful allocations; this does not replace or weaken the
separate authored Moby-arena N+1-unsafety contract.

## Explicit phased address reuse

The source-bound copy anchor is `80089ECC`. Native used data ends at `8008A794`,
not at the padded data descriptor end. Model RAM begins there; scene RAM begins
at `80099F94`, and the sixteen-row scene ends at `8009CF94`. This lies below the
known normal reserved-buffer lower address `80187BB0`; resident PETE begins at
`801C5BB8`. This arithmetic comparison is **not** peak or transient memory proof.

Nine deterministic phase observations describe overlay, header, VRAM, bank,
CPU SPU source, DMA SPU source, data, model and scene. Every phase explicitly
requires external completion. CPU `0x7EFF0` and programmed DMA `0x7F000` remain
different extents; the source staging's SPU wrap/low16 issue remains unresolved.
No caller-driven state machine can label a transfer completed.

Two additional lifetime intersections are named explicitly:

- The `0x36C` originally overwritten overlay tail at `80089ECC` is replaced by
  the header and subsequent copy-buffer reads. The disk overlay stays exact.
  Native preceding epilogue and a separate tail return stub are bound; an
  independent full EXE/overlay aligned literal/direct-jump/branch scan found no
  incoming encoding into `[80089ECC,8008A238)`. This does not prove absence of
  computed/indirect uses or asynchronous lifetime hazards.
- The model CD read replaces discarded data-sector padding `0x738` beginning
  at `8008A794`. Native used-data arithmetic establishes that address; permanent
  disjointness would be false. Request completion must still be independently
  established before reuse.

The scalar fixtures apply explicitly abstract completed byte writes to exercise
these overlaps. They do not emulate CD, GPU, SPU, DMA completion or BIOS services.
Rescue/replay SPU/model restoration remains a separate transient requirement.
Complete dependencies/resources, independent level support, peak RAM, transfer
timing, SPU wrap, rescue/replay runtime, native runtime and publication all remain
false. Source conditional normal/no-superflame restrictions, particle0C/42
obligations, dynamic demand and renderer packet ownership are not waived.

## Regression and independent evidence

The new smoke was written before Core implementation. Intentional Debug RED was
`1.40s`, zero warnings, exactly one CS0246 for missing `NativeId65NormalBodyPlan`;
shared Core compiled cleanly. Initial GREEN was `5.12s`, zero warnings/errors,
688 source-free / 790 exact-source checks. Final explicit registration-padding
assertion brought final totals to **689 source-free / 791 exact-source**, with
a `1.36s` clean Debug build.

Fixtures cover all descriptor/root/class widths and bases; complete owner
partitions; four auxiliary and scene-capacity profiles; nested integer overflow,
short/extra/corrupt data, source code, padding and callback bytes; bounded actual
enumeration versus forged Count/indexers; copies; repeat captures; two distinct
scene intents and wrong-upstream pairing. All old fresh scene/upload/model and
transfer hashes pass unchanged.

Sixteen pinned native instruction windows are independently rebound to the
complete original EXE in source mode. The bounded interpreter honors one-slot
load retirement, taken/untaken branch delays, JAL/JALR return timing and protected
or unmapped memory. Unsupported instructions and unfinished delays refuse.
It exercises stale callbacks, overlay omission, actual request arguments,
normal/state12 and cold reset, pool/auxiliary setup and exhaustion. It stops at
unbound native helpers and hardware calls. Huygens independently read the complete
machine/window fixtures and rebound all sixteen pins against the exact source;
no material issue was found in that bounded review.

The separate Python oracle reconstructs every fresh block directly from original
raw sectors using the earlier independent floor/field decoders, not a C# producer
or DLL. Full original length/hash/EOF checks run before and after. Pre-implementation
and final oracle runs agree with both complete bodies:

- Spawn `(8960,8960,9216)`, heading64, body SHA-256
  `b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c`.
- Spawn `(8976,8960,9216)`, heading128, body SHA-256
  `1314b299759ad0f66eed054db853c18e8db6dabda7c56a28cabd3fd74ea39643`.
- Both full fresh headers: `cd5658f83da4a953c5a3e66c848a8be606aabde9fea8b32d049c2bcf364c472d`.
- Exact separate overlay: `9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5`.

Commands from the repository root:

```text
dotnet build src/Spyro.Editor.NativeId65NormalBodySmoke/Spyro.Editor.NativeId65NormalBodySmoke.csproj -c Debug --nologo
dotnet src/Spyro.Editor.NativeId65NormalBodySmoke/bin/Debug/net10.0/Spyro.Editor.NativeId65NormalBodySmoke.dll
dotnet src/Spyro.Editor.NativeId65NormalBodySmoke/bin/Debug/net10.0/Spyro.Editor.NativeId65NormalBodySmoke.dll "Spyro the Dragon (USA).bin"
python3 src/Spyro.Editor.NativeId65NormalBodySmoke/source_normal_body_oracle.py "Spyro the Dragon (USA).bin"
```

Logs `00-before-implementation-raw-oracle.json`, `01-red-build.log`, initial
`02-debug-build.log` / `03-source-free.log` / `04-source.log`, final
`05-final-debug-build.log` / `06-final-source-free.log` / `07-final-source.log`,
and `08-final-raw-oracle.json` are retained under
`_local/research/native-id65-normal-body-20260906-r01/`.
No payload, source ROM, candidate, installed app or emulator setting was written.

Root independently reviewed both new Core files and complete fixtures, then
rebuilt Release in4.86s with zero warnings/errors:689 source-free and791
source-mode checks passed. Root also repeated the independent raw oracle,
including all16 native pins and both full body/header outputs. A mistaken
`--source` invocation was correctly refused; this project's source argument is
positional, as documented above. A second agent independently reviewed the new
Core/header/ownership and source-pairing boundaries and found no material issue
within their explicitly detached scope.

The regression runner registration was tests-first:27 Python tests initially
contained three expected missing-registration failures; adding exactly two
registry tuples made the suite pass. Root repeated all27 in2.561s. Default
mode supplies no arguments and does not resolve the source; optional source QA
supplies only the configured original BIN positionally, never a CUE, workspace,
candidate or publication option. Logs09/10 retain the registration RED/GREEN.
