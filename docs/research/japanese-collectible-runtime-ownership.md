# Japanese dragonfly collection, card ownership and USA extension boundary

September 25, 2026. Read-only source investigation. No disc, card, emulator,
settings, installed editor or preserved candidate was modified. The new
`tools/Inspect-SpyroJapaneseCollectibles.py` authenticates the complete supplied
image, pins complete executable owners and semantic windows, and rechecks both
the held and named image at completion. Its neutral typed state model is not a
native writer or a USA import/persistence certificate.

## Native collection identity and states

The Japanese source is 727,179,600 bytes, SHA256
`1bcf4353d6a4d4d0db152d2ebd60879ef85516feaf170f364ff419c54f599ea7`.
EXE at ISO LBA24, length0x70000, SHA256
`a67c21f441deb3a4df26099d78ddaa29cc66b927a7387718a678eee71664cee4`.
EXE PC-to-file conversion is `file = PC - 0x8000F800`.

Artisans overlay22 at WAD0x7F4000/0xF800 has SHA256
`bcfe62569db265135d5627de1902a9d31da82c72f51acdc461d2872e08f7cdf2`.
Its loader base0x80084A28 is corroborated by the actual executable load target
at file0xEF0 and loader code, separately retained in `loader-base-owner.log`.

Class0xF5 begins at PC0x8008DE1C, overlay offset0x93F4. During initialization
(`row[0x49] == 0`) it retires when global0x8007F330 is zero, or when this byte is
nonzero:

`0x800813EC + 32 * nativeBank + 5 * ((levelId - 10) / 10) + levelId % 10`

`nativeBank` is read at0x8007F390 and `levelId` at0x8007F510. The division is the
native signed divide-by-ten sequence; this formula is stated for nonnegative
retail IDs. The successful pickup path stores **0x69**, not1, at0x8008E0D4, then
retires through0x80056A5C. Source collection has position/height proximity and
player-state conditions; it does not just set a table byte on arbitrary input.

The auxiliary owner starts at0x800813E8. Its first four bytes remain opaque;
the following64 bytes form two32-byte banks. Any nonzero egg byte suppresses
the initialized actor. The observed write path normalizes0x69 to1, and the read
path restores only old0x69 into newly loaded zero entries. Unknown nonzero byte
values must therefore be preserved, not flattened into a new Boolean schema.

The bank is **not the ordinary save-slot index**. The same global affects
initial lives (0x80033278 onward), motion speed (0x80037CC4), animation tables
(0x80035DC4) and move availability (0x8004655C). Its user-facing mode name is
not established here. Ordinary card saves independently have three slots.

## Thirty native owners, not thirty-one independent placements

The separate object census finds31F5 rows across30 non-flight levels. Terrace
Village is the duplicate: data entry61, T176 andT177, source record hashes
`75cf03b9494bf846ac57c46a643704491c2969847ec4538845c20da1d657f3dd`
and `77e7a234362843f184674ffc4d5ae2be77f758031f235e5ad975a24e9899a187`.
Their records differ only in properties pointer and XYZ. Both twelve-byte
private headers are zero; behavior/render fields match.

Terrace overlay60 independently dispatches class0xF5 through table base
0x80084A2C, slot at overlay0x3D4, to0x8008AAD8 (overlay0x60B0). Its handler reads
the same byte at0x8008AB70 and stores0x69 at0x8008AD90. Both Terrace instances
therefore alias one bank byte (level41 -> index16). No per-instance/color field
enters this identity computation. Reusing the Artisans table displacement would
have selected the wrong Terrace code; the actual class dispatcher was checked.

The byte is only checked on initialization. The row0x49 countdown resets from1
to a random16..47, so an already initialized second actor is not automatically
rechecked when the first is collected. This is source control-flow evidence,
not a runtime claim about both eggs' reachability or simultaneous collection.

Raw arithmetic maps ID65 to index30, inside a32-byte bank; it is not an immediate
out-of-bounds write. However, no source egg owner or per-instance allocation is
established for ID65. Flight15 aliases level20 at index5, illustrating why raw
arithmetic is not an authorization scheme. The typed neutral model only admits
the thirty observed non-flight owner IDs and two bounded banks.

## Card flow and completion consumers

|Source consumer|Verified behavior|
|---|---|
|EXE0x800323BC..0x80032418|Normalizes64 egg bytes0x69->1, then requests a write of0x200 bytes from0x800813E8 at card file offset0x1400.|
|EXE0x800325A0..0x800325C8|Reads the same0x200-byte auxiliary extent at file0x1400.|
|EXE0x80032618..0x80032658|Only when loaded byte iszero and backed-up byte is0x69, restores0x69. Other loaded values remain exact.|
|EXE0x8005E180|Clears exactly0x200 auxiliary bytes on the failure/reset arm.|
|EXE0x8003229C..0x80032338|Ordinary slot write/read is file0x200 + slot*0x600, length0x600.|
|EXE0x8005DBD4|Ordinary checksum is unsigned byte sum over0x58C bytes; stored UInt32 at slot0x58C.|
|EXE0x8005E1A4..0x8005E2E0|Menu computes three ordinary slot percentages, stride0x600; sums36 dragon/gem owners and only18 ordinary egg bytes.|

The filename pointer0x800100C8 contains `BISCPS-10083SPYRO`. The transfer ABI is
`a3=file offset`, fifth stack argument=byte count:0x1400 is not the auxiliary
buffer length. The call pair is0x8006BFA4 write /0x8006BCE8 read. Their library
state retains offset, buffer and length separately.

Gate setup requests the device operation through0x80073120 and polls0x80070FF8;
only a successful result sets0x8007F330 and backs up the64 egg bytes. This is a
device-conditioned flow associated with the Japanese feature. This pass does
not establish the complete PocketStation protocol, auxiliary checksum,
device-side creature state or all meanings of bytes outside the egg table.
Porting that entire device subsystem is not required to add independently owned
USA collectibles, and is outside the requested egg/sign scope.

The Japanese live percentage routine0x8002AB30/0xC0 has SHA256
`6fb34fa47ced6bf3e7f5432febecf89372bdb0d3c986127a467e3040683dbc53`.
Like the USA routine, it computes integer
`(gems*50 + dragons*6000 + ordinaryEggs*10000)/12000`, then
`100 + (gems-12000)/100` above12,000 gems. The observed F5 pickup only sets its
auxiliary byte and retires; it never increments those ordinary totals. Thus
the native Japanese dragonfly collection is not a fourth input to ordinary
completion. No new percentage policy is selected by this research.

No per-instance dragonfly inventory-display consumer has yet been identified.
The inspected menu branches gate an auxiliary/device option on the native gate
or auxiliary header. This is not evidence that the requested USA inventory
display already exists or can be enabled by modifying the ordinary egg counter.

## USA save extension: concrete available boundary, remaining ownership

The exact USA source retains neither class0xF5 nor0x47 in its368-class
placed/local/shared matrix. This says nothing about unregistered residual code,
but provides no reusable registered package or source instance for either.

USA memory-card overlay2, WAD0x5B800/0x3800, SHA256
`8ee66fe644407bf15f93af1615045fbaca17fe3daa9f3b67f38bee2277a94c7c`,
uses `BASCUS-94228SPYRO`. Header bytes and code have distinct runtime bases:
header0x8007AA38; code offset0x9C loads at0x8007B0E8.

The header template at EXE PC0x8006F494 starts `53 43 13 01` (SC, icon mode,
one-block count). Its512-byte SHA is
`bb38831d747ed2a6dd322aa74cbd10b82f7f7b79600610718ea22891a232f332`.
The new-file call0x8007C404 passes block count1 to0x80067718, whose0x800677E4
forms `(count<<16)|0x200` create flags. Native code constructs a0x200 header and
three0x600 slots, then writes exactly file `[0,0x1400)` at0x8007C4A4. Reads at
0x8007B980 and0x8007C514 use that same prefix. These are the only direct calls
to those read/write APIs in this complete overlay.

Within the one0x2000-byte block, `[0x1400,0x2000)` is a **candidate extension
owner of3072 bytes** beyond the observed native prefix. It is not presently
initialized, checksummed, preserved by a new extension reader, or declared
free by an application-wide certificate. The ordinary per-slot unchecked tail
`[0x590,0x600)` is unsuitable for persistent extension data: the native serializer
clears the entire0x600 slot before rebuilding it.

Actionable next implementation is a separate, versioned USA collectible owner:

1. Bind the complete original card overlay and library operations, explicit
   extension file offset/length, and a newly allocated runtime buffer. Do not
   reuse Japanese RAM addresses or simply enlarge the ordinary I/O into
   unowned adjacent RAM. Keep the native0x1400 prefix byte-compatible.
2. Give authored collectible instances stable native IDs with a bound
   level/row-to-ID table. Distinct same-level placements must receive distinct
   bits/records; edits, deletion and row compaction must not transfer collected
   state to another authored ID. Color is a separate instance attribute.
3. Define a magic/version/length, document/content identity, bounded per-slot
   collectible records and its own checksum. Distinguish absent legacy data
   from a recognized corrupt extension; preserve all ordinary bytes and saves.
   Confirm old/all-new/reset/replaced-card paths and save-file allocation before
   enabling writes. A separate game-owned file remains an alternative if the
   same-file tail cannot be certified.
4. Hook pickup and retirement, inventory display and the chosen percentage
   formula to those new owners. Live percentage and three-slot menu percentage
   are separate consumers. Do not relabel ordinary eggs or copy the device gate.
5. Prove full output/readback, then cold-boot two same-level eggs plus LAB65,
   independent colors/messages, pickup, reentry/death, save/reload and legacy,
   full/corrupt/removed-card behavior. No runtime or card acceptance follows from
   the source-only model/tests.

## Reproduction and focused checks

```sh
python3 tools/test_japanese_collectibles.py
python3 tools/Inspect-SpyroJapaneseCollectibles.py '<Japanese.bin>'
python3 tools/Inspect-SpyroJapaneseCollectibles.py '<USA.bin>' --usa
```

Eleven source-free test cases pass, covering exact byte owners, immutable
snapshots, pending normalization/merge, unknown-state preservation, two banks
and thirty owner slots, unsupported IDs, ordinary checksum boundaries,
18-byte ordinary egg accounting, percentage goldens, stale instruction pins
and wrong-image refusal. Actual Japanese and USA captures pass all pinned
owners/windows and held/named complete-image final checks.

Evidence is under `_local/research/japanese-content-20260925-r01.RCmBCn/`:
`collectibles-jp-source.json`, `collectibles-usa-save-source.json`, and
`collectibles-tests.log`. Disassembly flags additionally require the locally
available Capstone environment; default metadata capture and neutral tests use
only the Python standard library. No third-party game assets were downloaded.
