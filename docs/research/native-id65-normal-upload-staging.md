# Native ID65 normal upload transfer-source stage

2026-09-06. This additive producer emits an actual deterministic detached
main-RAM transfer-source image from the previously issued fresh resource/scene
bundle. It emits no native body/header, padded subfile, loader patch, RAM address
assignment, candidate, or publication artifact. Old resource and scene outputs
and their authority limits remain unchanged.

## Actual bytes and their owners

`NativeId65NormalUploadStaging.CaptureAsync(originalBin, issuedFreshSceneBundle)`
requires the constructor-closed fresh bundle and reopens the exact original
USA source. It reuses the complete original image/layout/EXE/Town/PETE capture,
with full image SHA/length/EOF verification before and after reads. It also
binds eighteen exact transfer/boot/VRAM code or pointer spans, the actual retained
Town bank, and the preceding producer's plan and complete upload hash.

`CopyTransferSourceImage()` returns a copied `0x7F000`-byte image. The two immutable
relative owners partition it completely:

| Transfer-image range | Source in the same issued `0xDE000` upload | Meaning |
| --- | --- | --- |
| `[0,0x5E000)` | `[0x80000,0xDE000)` | Complete retained Town audio bank CD read |
| `[0x5E000,0x7F000)` | `[0x5E000,0x7F000)` | Residue of the preceding paired VRAM CD read |

This is not a `0x7F000` sound bank. The second owner is presentation data, including
its issued source/detached/fresh-zero bytes. It cannot be replaced with arbitrary
zeros or the original atlas. The old descriptor-0 payload remains `0xDE000`.
`RequireExactTransferSource()` validates every byte, complete lengths and the
DMA-only tail. Neutral internal composition is explicitly not source authority.

Default transfer SHA-256:
`626ae349b98cb4bea955ac53020586f645f872225085a67bc809f46f4ab0fb07`.

Default source-issued plan SHA-256:
`98bd43761b87de15d2915d1377e0298ce7a0f133d2ee07c2f5672bad503af000`.

Distinct upstream authored surface/spawn intents retain different plan identities
even when they happen to produce identical transfer bytes. Returned arrays,
input arrays and public owner snapshots cannot mutate an accepted stage.

## Why native CPU and DMA extents differ

The original normal loader first reads `0x80000` VRAM bytes into `g800785DC`
(`8001571C..15754`), submits two rectangles (`80015770..157CC`), then reads only
the bank prefix back into the same buffer (`800157CC..15830`). The source CD
wrapper `80016698..16780` rounds these positive sector-aligned requests to their
exact respective lengths. The later source request is at `8001583C..15854`:
address setter `8005CB24` receives `0x1010`, write wrapper `8005CAC4` receives
`0x7EFF0`. Both values are supplied in actual JAL delay slots.

The wrapper retains that capped request. `8005C450..C4D4` chooses CPU for a
nonzero `g80073570`, DMA for zero. The new contract does not assume the global
always keeps its static value:

- CPU `8005BE88..C050` reads halfwords into the FIFO in chunks at most `0x40`.
  For this fixed even request it consumes `0x7EFF0` bytes.
- DMA `8005C1C0..C44C` computes `(length>>6)+((length&63)!=0)` blocks at
  `8005C3CC..408`, with sixteen words per block. The request therefore programs
  8,128 blocks, BCR `0x1FC00010`, and a **`0x7F000`** source extent. Native write
  direction is CHCR `0x01000201`; the opposite direction is not a source upload.

`SourceReads` preserves these two main-RAM footprints separately. Hardware pointer
data at EXE `0x63D54+0x18` independently identifies the SPU/FIFO and DMA registers.
The preceding VRAM wrapper/table/queue/transfer are also bound: queue copies the
separate eight-byte rectangle and retains the pixel pointer; CPU transfer reads
pixels, while DMA programs that pointer as its source. This is exact source
direction evidence, not proof of asynchronous CD/GPU/SPU completion or lifetime.

## Low-16 SPU ownership remains unresolved

The DMA linear endpoint is `0x1010+0x7F000=0x80010`. The stage reports this without
claiming hardware wrap semantics, harmless writes, or ownership of SPU `0..15`.
The final sixteen source bytes are nonzero:
`3232b131122e903191311232f231322e` (SHA-256
`2f8bf1c6f5194330dc3e098f161072dfa82c936fd535878c54eacff772ba9d25`).
CPU does not consume them; the DMA source extent includes them.

The boot code does not establish a free low-16 region. `8005BCD0..BD04` sets
eight-byte address units/shift3. The `s0==0` initializer at `8005BD50..90` transfers
sixteen `07` source bytes from `80073594` at address units `0x200`, therefore
**SPU `0x1000..100F`**, not `0..F`. The next loop sets all 24 initial voice starts
to the same `0x200` units. These are called native boot/default bytes, not proven
silence. The public address setter's `0x1010..0x7FFF8` range likewise cannot
establish that lower SPU memory is never owned or read.

`CompleteSpuStateProven`, `SpuWrapSemanticsProven`,
`ReservedLow16PreservationProven`, `TransferTimingProven`,
`PeakRuntimeMemoryProven`, and `HardwareTransfersProven` remain false.
Complete level resources, independent level support, runtime and publication
also remain false. All eight callbacks, required companion/player/particle state,
auxiliary rescue/replay modes, PETE/pools and authored dynamic-arena demand still
need separate integration. No native instruction or requested length is changed.

## Regression and independent reconstruction

Focused project: `src/Spyro.Editor.NativeId65NormalResourceSmoke/`.

1. Tests-first compile RED: `1.47s`, zero warnings, fifteen CS0103 errors only for
   the intended absent APIs; existing Core compiled cleanly.
2. Initial Debug GREEN: `5.16s`, zero warnings/errors; 327 source-free checks.
3. Initial source run hit the test interpreter's finite instruction limit: the
   synthetic SPU ready-mode seed was reversed. The actual write selector expects
   control `0x20`, opposite direction `0x30`. Corrected only that fixture seed;
   no production change or old golden changed. The failure log is retained.
4. Final Debug GREEN: `1.56s`, zero warnings/errors. Final source-free **327**;
   exact-source **15,423** checks. All earlier upload/data/model/scene hashes and
   refusals passed unchanged.
5. DLL-independent standard-library Python reconstructs the entire paired upload
   and `0x7F000` transfer image directly from original raw sectors, matching the
   full hash above. It verifies the complete original before/after. It reports
   `nativeScalarInstructionsExecuted=false`; scalar execution is separate C#
   fixture evidence, not fabricated Python CPU or hardware execution.
6. Root independently reviewed both complete Core files, staging cases, raw
   oracle and the narrow interpreter additions. Root Release build passed in
   `5.01s` with zero warnings/errors; 327 source-free and 15,423 exact-source
   checks passed again. Root's independent raw-oracle rerun matched the same
   full transfer hash and checked the original image before/after.

Fixtures cover exact owner boundaries, short/extra/zero-padded/corrupt outputs,
input snapshots, actual repeated source captures, distinct upstream intents and
corruption of each native certificate plus whole-EXE complement. The small MIPS
fixture honors load/branch/JAL delays and executes cap, aligned CD-rounding,
backend selection, complete DMA varargs/direction/register setup, native address
range/rounding, boot arguments, and all 24 initial voice-start stores.

For CPU consumption, all **8,128** scalar chunks of the fixed even request execute
their source halfword loop with exact byte owners; the last incomplete owner
refuses. Intervening hardware waits/helper calls are not synthesized. Likewise
the boot CPU transfer itself is stopped at its call; the boot argument and later
voice initialization slices are explicitly separate. Generic odd transfer sizes,
hardware FIFOs, DMA completion, sound playback and native runtime are unclaimed.

Exact commands from the repository root:

```text
dotnet build src/Spyro.Editor.NativeId65NormalResourceSmoke/Spyro.Editor.NativeId65NormalResourceSmoke.csproj -c Debug --nologo
dotnet src/Spyro.Editor.NativeId65NormalResourceSmoke/bin/Debug/net10.0/Spyro.Editor.NativeId65NormalResourceSmoke.dll
dotnet src/Spyro.Editor.NativeId65NormalResourceSmoke/bin/Debug/net10.0/Spyro.Editor.NativeId65NormalResourceSmoke.dll "Spyro the Dragon (USA).bin"
_local/tools/capstone-venv/bin/python src/Spyro.Editor.NativeId65NormalResourceSmoke/source_normal_upload_staging_oracle.py "Spyro the Dragon (USA).bin"
```

Logs: `_local/research/native-id65-normal-upload-staging-20260906-r01/` contains
`01-red-build.log`, `02-green-build.log`, `03-source-free.log`, retained failing
`04-source.log`, final `05-green-build.log`, independent `06-raw-oracle.json`,
`07-source.log`, and `08-source-free-final.log`. The precursor exact disassembly
is retained under `_local/research/native-id65-engine-profile-20260906-r01/` as
`normal-upload-staging-source-audit.py` and `.json`.
Root's independent Release/source/free/raw-oracle logs are retained in the same
stage evidence directory under the `root-release-*` and `root-raw-oracle` names.

Original image SHA-256:
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
Complete EXE SHA-256:
`a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`.
The original ROM/CUE, installed editor, emulator settings and candidates were not
written. A subsequent normal body/header producer must consume these relative
owners without promoting pending callback/auxiliary/runtime obligations.
