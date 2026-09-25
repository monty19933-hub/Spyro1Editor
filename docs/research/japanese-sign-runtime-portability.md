# Japanese sign runtime portability: bounded CPU leaf and remaining closure

2026-09-25. Read-only source analysis plus a detached internal CPU planning
contract. This is **not** a USA sign installation, VRAM reservation, executable
hook certificate, runtime fit result, or publication approval. The earlier
Japanese reference candidate is a separate same-region experiment.

## Exact sources

- Japanese original BIN SHA256
  `1bcf4353d6a4d4d0db152d2ebd60879ef85516feaf170f364ff419c54f599ea7`,
  EXE SHA256 `a67c21f441deb3a4df26099d78ddaa29cc66b927a7387718a678eee71664cee4`.
- USA original BIN SHA256
  `fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`,
  EXE SHA256 `a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`.
- Both executable mappings use PC = EXE file offset + `8000F800`.
  Japanese Artisans overlay22 loads at `80084A28`, WAD `7F4000/F800`,
  SHA256 `bcfe62569db265135d5627de1902a9d31da82c72f51acdc461d2872e08f7cdf2`.

`NativeRegionalSourceSession` authenticates whole images and held/named final
identities; Japanese and USA offsets are never treated as interchangeable.

## Native final-line termination: runtime-discovered authoring defect

The preserved first edited Japanese reference showed `181 ガパ` but lost its
second colored line after reveal completion. Its 13-byte stream was
`31383120c5bdd9bcff42c5e1e6` followed by NUL. That raw stream is decodable, but
is not a complete native modal message.

Exact consumer `8001B21C/170`, SHA256
`18c0271e566a18eeb514babaa6b0b80fddf5c6dd83866908d00bd90926c9a822`:

- `8001B238..260` counts **FF** bytes to position the group vertically.
- `8001B2B0` flushes the current line on FF. `8001B2C4` also flushes at the
  reveal cursor, explaining a transient partial line during typing.
- `8001B300..304` otherwise copies a byte into the 48-byte local line buffer.
- `8001B334..348` encounters NUL, sets reveal state1000, and exits **without
  flushing**. The final unfinished line therefore disappears once complete.

All 330 authenticated original signs end in FF before NUL. Corrected intended
message bytes are `31383120c5bdd9bcff42c5e1e6ff` (14 bytes before NUL). Terminal
FF is a line terminator, not an optional visible blank line. The raw and Unicode
codecs remain lossless; `AsCompleteDialog()` explicitly appends it if absent,
rechecking authored length/line policy. `BuildPrivateData`, source capture, and
the edit-plan request boundary reject a missing final FF. The first candidate
and its runtime evidence remain unchanged as failure evidence.

`SignDialogTerminationCases` has a bounded independent transcription of this
copy/flush/exit behavior, a complete source-loop hash gate, and all 330 source
line comparisons. The model is not a GPU emulator or substitute for the
parent's corrected-candidate gameplay observation.

## Smallest closed CPU font leaf

Two fragments contain every direct control-flow dependency of measurement,
centering and glyph-packet generation:

| Japanese source | Size | SHA256 |
|---|---:|---|
| width+centering `800176F0` (center entry `80017798`) | D0 | `4690d076e4549472b14435a12a3208d1b34fc734bf7a4f9820ee453ff630ef2b` |
| glyph draw `80022868` | 2D4 | `592e7b33f9ed7f898b0ce475a8567aad7d6cd69c7c28a14c9c30cc078d799820` |
| color words `80079598` | 20 | `6f267eba5b81e5ae2abd5a82d393938a6be96b1db08bf1bd56bcee67b6837f75` |
| 256 glyph halfwords `800795B8` | 200 | `3ebf51c29f337e461de260343b0e11b85eadeec9972b3eefd1afd04b33085913` |

The two routines total 932 bytes / 233 instructions. Only the center-to-width call
is inter-routine; it is internal to the captured package. The glyph renderer
has no external call. Six observed LUI/signed-ADDIU pairs require typed CPU
rebinding (12 instruction words), distinct from branch/jump relocation:

| High/low source PCs | Role | Verified destination binding |
|---|---|---|
| `80022884 / 80022888`, `80022950 / 80022954` | immutable color table | materialized plan table at arena+3A4 |
| `80022898 / 8002289C` | immutable glyph descriptor table | materialized plan table at arena+3C4 |
| `800228A4 / 800228A8`, `80022B28 / 80022B2C` | primitive cursor global pointer | USA `800757B0` |
| `800228B0 / 800228B4` | UI primitive queue pointer | USA `8007581C` |

The UI queue is **not** the USA world ordering table at `80075820`. JP enqueue
`80015EC0/38` and USA enqueue `800168DC/38` differ only in their first two
address-loading words; their head/tail and 24-bit linking instructions match.
SHA256 respectively `cd0b30f8efebfc24f3804bf75f6e7e45ee5f2a035b9a2139a76c3511ae636800`
and `1eb9f092558eb96c40be2ab0c9b60716c434e5e51eba954348dc526856800110`.
USA primitive cursor store at `800580AC..B4` is included in certified
`80058070/70`, SHA256
`b04980d58c31a81ec8d35699e36d0e53bd77b1ac993e86c021adba6a1c12c08e`.
These certify operand meanings; they do not prove every pointed-to buffer's
initialization, capacity, lifetime, or safe hook phase.

`NativeJapaneseSignFontLeafSource.Capture(japan, usa)` requires both revisions,
all listed code/data hashes, and the exact Japanese font atlas. `CompileCpu`
produces a detached 1476-byte plan: code 932 + colors 32 + descriptors 512. Its final
overlay arena has explicit extent/alignment/RAM bounds and cannot overlap the
two bound globals. `NativeMipsBehaviorFragmentCodec` handles only internal
control transfers; fixed-role pair rewriting then preserves opcode/register
fields and handles signed low-half carry. Synthetic fixtures cannot acquire
`SourceBound=true`. Complete-closure, VRAM, runtime-fit and publication flags
remain false, including actual-source plans.

`SignFontPortabilityCases` checks every emitted instruction against an
independent relocation/complement oracle, all data bytes, high-half carry,
wrong revisions/owners/overlaps, altered native operand sites, unclosed
transfers, fallthrough, immutable capture and detached output. These files
were initially added without running dotnet while the parent's full suite
held existing DLLs. After release, the coordinated Debug build passed cleanly;
the combined font CPU/VRAM dual-source route passed623 checks. See
`japanese-sign-font-vram-lifetime.md` for the newer placement/lifetime contract,
focused test results and the still-unapproved right-half mirror strategy.

## Font resources still require installation ownership

Japanese WAD2 is `6F800/A000`, SHA256
`917a2c272859d99b573ee3f79b1a62ca847dff218802b2c65f80bb55cb888ff1`.
USA WAD2 is `5B800/3800`, not an equivalent resident allocation. JP uploader
`800120F0/54`, SHA256
`6fcc7c159e789ef4a3131119db6b8740ff8926e56e8e680a239eb1d321584ee7`,
reads A000 bytes through loader `80015AE4`, then uploads a rectangle
`x=0,y=472,width=512 words,height=40` through `800648A4`.

Glyph code embeds CLUT values `7FD6/7F96`, texture-page base16 and UV coordinates;
rebinding CPU pointers does not relocate these GPU addresses. A real USA
integration must own the complete atlas/CLUT upload, destination VRAM
non-overlap and lifetime, or use a separate typed UV/tpage/CLUT rewrite
contract. It must also bound40-byte packets per visible glyph and establish
UI queue/frame lifetime. See `japanese-font-encoding.md` for verified UV math.

## Complete sign closure is larger than the font

| Layer | Exact Japanese owner | Portability status |
|---|---|---|
| per-row sign update | `80089A74/2B8`, SHA `69dee0be5bb73ed828d985fdf5787d524beea1e5fa36de92774083f8f8550e00` | needs destination dispatcher frame/register adapter and private row/property storage |
| reset/state1 exits | `80088694/8`, SHA `c44749962208df7a70f85dc30ded53073cd0e008baefc034dc44ab8ea20f63bf`; `80090730/C`, SHA `78c807849e829d02665adb70940f8b28bea6316a290ab427dfefd34175d3935e` | exact small tails; must replace outside continuation `80090FA4`, not copy the donor loop |
| enter modal | `8002B684/58`, SHA `3714f511ef76e3707663b8cfebb2ff172e780086e5e784672f85f2d31588a35e` | player/sound calls, reveal/frame reset, game mode17 |
| modal update | `8002DB14/C0`, SHA `5249cca98a210b8770368e721520d1bcff859bca4ef4a9dc2cedcd3c22d32f16` | controller input, sound, reveal and exit dependencies |
| modal draw | `8001AEE4/58C`, SHA `524d2147b5853f2e133d1b9da5f3ff9304fb51cee819a93f2f0c0a4cb476a4f3` | first-frame backdrop/VRAM work, frame state, message buffer, font, completion prompt |
| exit modal | `8002B6DC/70`, SHA `8240112054e38a2f5950066355950b6e73148fcb65fca4d734edfcb2e5d7d9e3` | backdrop restore/upload, draw synchronization, player restore, mode0 |

JP game mode17 is explicitly dispatched to draw at `8001DCFC` and update at
`80034118`. The inspected USA draw ladder `8001EDDC/254`, SHA256
`37980df646580950e30917cdd6faba99605bac020d7c803660d050a850d44021`,
handles modes0..15 and falls through the common exit for17. Writing17 into
USA mode global `800757D8` is therefore not a draw installation. Both main-loop
hooks need destination-owned branch/complement proofs; the USA update ladder
has not been fully certified by this slice.

Shared modal globals include current text `8007F478`, reveal `8007F1E4`, frame
`8007F44C`, game mode `8007F35C`, tick `8007F4E0`, controller input `80080F78`,
sound map `8007FD84`, and player/backdrop state. Private instance state includes
row+48 and property+4. Native activation serializes on mode0; instances do not
own independent copies of the modal service. The existing dependency-instance
linker rejects overlapping mutable globals for separate families. Model this
as one level-scoped shared modal service with multiple private sign rows, not
as independent concurrent modal implementations. The secondary event global
`80082560` remains a semantic-owner obligation.

## Proven equivalent helpers and linker boundary

- JP distance `80016F74/60` equals USA `80017990/60`, SHA256
  `277c87f0a049389cf5c3e0510eac8b878a516d9f773d44545184f67910815d90`.
- JP angle error `80016EEC/20` equals USA `80017908/20`, SHA256
  `03690bf77631968cb6e2165178bfce78cef28620e589e78e2f0faa661c850e84`.
- JP atan `80016098/1A4` and USA `80016AB4/1A4` differ only in internal jump
  targets and two lookup-address low halves. Their full code hashes are
  `c5511ef458f07ec70bf7decbf10ffa1f87e9954402d58b461a6f65f8b3394ae9`
  and `56d40cafd1f03684a23c80f6ae253ade96a606053152e5c5cd21749273f28b89`.
  Tables JP`80075830/88`→USA`8006CE7C/88` and JP`800758B8/44`→USA`8006CF04/44`
  are exact byte matches, SHA256
  `40fdf829ed27f8ac3c6b769166d03f4c0278467668c22e81be844849f720741c`
  and `e3e2fdfdef42d162f4e1c44e47ae3858c23966bd0e18de62c06a686221a1aa86`.

The generic fragment codec deliberately requires original external-call
bindings to retain their original targetPC. It cannot certify a new USA PC
merely because a caller labels it equivalent. A future cross-region call
binding must carry both full source/target routine preimages, complete
relocation normalization (including tables), and a code-owned semantic role.
No generic weakening is implemented here. The font leaf avoids this unresolved
API altogether because its calls are internally owned.

Next implementable runtime slice: reserve and certify a destination font
atlas/CLUT upload lifetime plus bounded UI primitive usage, then install the
source-bound CPU leaf behind a narrowly owned USA draw observation hook.
Only after that should the complete entry/update/draw/exit modal controller
and per-instance sign adapters be linked. Copying the sign handler alone or
assuming mode17 exists would omit concrete observed dependencies.
