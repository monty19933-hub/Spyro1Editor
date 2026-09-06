# Wizard T27: next source-proof boundary

Read-only investigation during the `96ec93d` full regression gate. No C#,
runner, build, ROM, candidate or runtime state was changed. This proposes a
separate authored-route profile; it does not repair or recertify v1.

## What the native code actually supplies

Source: original USA BIN SHA256
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
This pass independently read/hashed its EXE
`a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`
and Magic overlay entry 33, WAD `0x2727000`, length `0x14000`, SHA256
`98e135e18876ea094ca9484b33cfb38438e2b99b72ee4d6d66e907722c744a1d`,
then disassembled those bytes with the existing local Capstone environment.
EXE file offsets below equal runtime address minus `0x8000F800`; overlay WAD
offsets equal `0x2727000 + runtime - 0x8007AA38`.

| Native evidence | Proven meaning / remaining boundary |
| --- | --- |
| Magic `0x800888C4..DC`, WAD `0x2734E8C`: `a1=0x60`, `a2=a3=0x180`, stack fifth argument `0x27`, `jal 0x80039398` | Ordinary travel uses step argument 96, resident-query radius argument 384, terrain-query radius argument 384. These are movement probes, not the animated body bounds. |
| EXE `0x800393E8..3944C` | XY movement uses yaw byte row+`0x46`, signed trig tables `0x8006CC78`/`0x8006CBF8`, multiplication and arithmetic shift 12. Global `0x800756C4` values 3/4 scale the step to 1.5×/2×; other values use the original argument. |
| EXE `0x80039490..A8` | Flags `0x27` select resident-object query `0x8004E3C8` with mode 3 and self excluded. This path may correct the proposed point; an ideal straight segment is not the complete native trajectory. |
| EXE `0x800394B8..DC` | Terrain query `0x8004BE4C` runs at proposed origin Z+300+384, with both arguments 384. Its code squares the radius and searches neighboring collision cells. |
| EXE `0x8003952C..3C`, `0x800395FC..39640` | Floor query `0x8004D5EC` starts at the proposed post-correction Z+1024 with range 5000 (current actor Z+1024 when no query displaced it). Target Z is returned floor plus **signed 16-bit row+0x38**. It snaps when absolute difference is at most 600; otherwise changes Z by ±250. Route Z is not copied into actor Z. |
| Magic `0x800888BC`, EXE `0x80038EE0..38FC4` | Heading changes use step 8, threshold 16 and an enabled final argument, with the same global scaling. Turning and integer rounding need a bounded trajectory proof, not endpoint-only validation. |
| Magic `0x80088520`, `0x80088870`; EXE `0x80017990..EC` | Arrival uses XY metric `max(abs(dx),abs(dy)) + floor(3*min(abs(dx),abs(dy))/8) <= 256`, not Euclidean distance or XYZ distance. |

There is no proved fixed +205 placement offset in the mover. +205 was an
observation of source origins above their local floor; row+0x38 is zero for both
source T6 and native T107. The initial T27 Z23450 is below its Z23552 terrace,
but merely replacing it with floor+205 does not repair the route. Initial
placement must bind the first native query/settling behavior and visible model
origin separately. Flags `0x27` do not enable the mover's other `0x10`/`0x40`
step/slope tests, so those branches cannot be cited as active safety limits.

## Actual collider chain, separate from renderer radius

Magic model root slot `0x98` binds data-relative root `0x1BAFDC`, WAD
`0x28F5FDC`. Its +`0x14` word is `0x800035A4`; +`0x18..0x30` are zero.
EXE `0x80013438..60` adds the root's low-address base to each nonzero one of
these eight words, preserving the sign bit. Thus the first collider's source
bytes are at root+`0x35A4`, WAD **`0x28F9580`**, not EXE address `0x800035A4`.

The native-consumed record consists of a 4-byte broad-phase header and one
12-byte final shape: `DB070000010390010000000000000000`.
Its 16-byte SHA256 is
`4fcf7e555a129092f9ab021641572ee5ec053fa4f1a1de148567335674586f25`.
The native resident-query consumer provides stronger evidence than a renderer
label: `0x8004E558..84` uses the header's u16 **2011** in a broad-phase radius
sum; `0x8004E5A4..B0` dispatches control `0x01900301` to case 3;
`0x8004E830..950` resolves packed animation **vertex 0**, transforms it using
row+`0x20..0x30`, and tests a sphere of radius **400** plus query radius.
The shape control's low bit 1 terminates the shape list on the next loop;
`0x8004E590..598` checks that previous control before reading another control
word in the branch delay slot. Do not extend ownership merely to include that
ignored read of the next word.
This boundary is independently corroborated by the next registered native
model root: class `0x01F7`, root slot 19, starts at WAD `0x28F9590`, exactly
16 bytes after this collider. Its first word 1 is not a collider terminator.

Runtime collider pointer row+`0x08` is selected by `0x800529E4..52A60` from
`*(0x80076378 + class*4)`, animation/frame fields row+`0x3C..0x40`, and the
frame word's selector `(word >> 19) & 0x1C`. It is not the zero pointer stored
in the original scene row. Source animations at WAD `0x28F6020`, `0x28F60DC`,
`0x28F6178` have 19/15/15 frames and scale byte 1. The first two use selector 0
throughout; animation 2 uses 0 for frames 0–4 and 4 (the null collider slot) for
frames 5–14. This identifies a concrete frame-dependent body-proof task:
decode the selected vertex stream and exact transforms/rounding for every
reachable frame/transition. **Neither 2011, 400 nor the audit's authored width
512 is by itself a certified rendered-body footprint.**

## Smallest useful next stage

1. Add a read-only `NativeGreenWizardMoverContract` plus source-backed smoke
   cases, pinned to the native instruction/package spans below. Independently
   reconstruct step/turn/arrival/ground settling and animated collider selection;
   reject changed instructions, pointer owners, unsupported frame selectors,
   scales, descriptor semantics and unreachable collision cells. Include
   near-endpoint turning, the 600/601 settling boundary, global scale variants,
   animation-2 null-collider transition, and a resident collision that displaces
   the proposed point. Keep probe radius, combat collider and rendered mesh
   bounds explicitly distinct.
2. The existing Z23552 terrace is the first bounded destination area worth
   auditing: `(88832,41851)` and `(88832,43899)` individually have native dry
   support and rendered texture-31 bank surfaces. **No connecting corridor was
   certified in this investigation and no replacement route was selected.**
   Require continuous dry support/index coverage, rendered terrain agreement,
   enough space for the proved turning/body envelope, and resident-object
   clearance over both travel directions and the initial settling state.
   Existing T27's destination `(82309,38707)` is damaging water and stays RED.
   Stay on one flat terrace first; do not expand the grammar to slopes/banks.
3. Only then introduce an explicit new authored-route input/compiler profile,
   e.g. `NativeGreenWizardAuthoredRouteCandidateCompiler`, with source-bound
   origin, yaw and exactly two independent XYZ route points. The native private
   payload already owns count 2 at +`0x28`, header +`0x2C`, XYZ at +`0x30` and
   +`0x40`, and zero fourth words. Its pointer/fixup grammar does not require
   translation to be the only way those coordinates are authored. But v1's
   private `TranslateRoutes` deliberately computes target-origin+donor-delta;
   do not change that function or add a silent optional override to v1.
   Reuse the shared scene-properties ownership/linker and disc transaction
   infrastructure from the locked original, not a patched published BIN.
   Require positive/negative placement proof, independent byte/preimage and
   inverse readback, repeatability, two distinct intents, late-source rejection,
   and unchanged old v1 goldens before permitting any new publication.

Native route selection is state-dependent: the handler writes the route-header
index byte and uses properties+`0x04` flags/+`0x14` index, player proximity,
attack/recovery and alternative movement helpers. Two XYZ points alone are
therefore not an unconditional line-following specification. The first profile
must either prove every reachable native branch for the retained scalar state
or refuse that branch. Runtime visibility, attack, death/reward and normal
reload remain separate later gates.

## Reproducible code-span identities

| Span | Source offset / length | SHA256 |
| --- | --- | --- |
| Mover `0x80039398` | EXE `0x29B98`, `0x2F0` | `bc780cb5dd054b3a0c97fc1b25b1a85f3f0ac5c1c0404b60cb4df1d83bb81eca` |
| Heading `0x80038EE0` | EXE `0x296E0`, `0xE8` | `d58397320748b47f903d597b0b66e3b56397cad4530443b65c82123e739d63b4` |
| Root pointer loader `0x800133E0` | EXE `0x3BE0`, `0x268` | `030346c69253fbc971f3a5cea36550afe798a2982070e9da29b6ce8df858fc82` |
| Collider selection `0x800529E4` | EXE `0x431E4`, `0x80` | `ec41ef500fe7a57b343c8f0bbaf3dc6d1c96d500b2a0bbdf06092e33c784ce73` |
| Animated sphere branch `0x8004E830` | EXE `0x3F030`, `0x1A8` | `02e8b50f16b4ca67d32253a9dda3f918b10f43aa9615bf146c4d26111c702b6b` |
| Magic Wizard handler `0x800883A0` | WAD `0x2734968`, `0x668` | `715ddf657c49ffce419ad67f09e846708e48f4197dac4edd644d99d7437280f5` |
