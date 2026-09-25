# USA native save workspace and extension boundary

This is read-only evidence, not a card writer, save-hook implementation, or
authorization to use an unknown legacy file tail. The user's completion policy
is fixed: custom active dragonfly eggs are required for 100%. The detached
manifest, three-slot ledger and completion policy already implement that
policy; this report does not change them.

## Result and smallest next model

The native **card-menu workspace is explicitly placed 0x2000 bytes below a
frame-arena boundary**, not merely 0x1400. That is stronger than inferring RAM
from the 8 KiB card-file block. It is nevertheless a *temporary phase owner*:
the same complete range is covered by an earlier 0x40000-byte card-screen image
load, and later level loading/frame-layout changes replace its lifetime.

The **ordinary in-game save path has a different 0x600-byte scratch owner** and
never writes the proposed extension. Enlarging the menu prefix transfer alone
would not save custom eggs during play. Neither workspace is a demonstrated
persistent collectible-ledger owner.

The smallest promising design remains a **separate, checked tail transaction
inside the existing one-block file**, plus explicitly owned resident ledger
RAM. Preserve all native `[0,1400)` operations byte-for-byte. Establish new-file
initialization only after successful native file creation; read and validate
an existing tail before modifying it. Unknown bytes, including all-zero/all-FF
legacy tails, are not a recognized empty ledger and must be preserved/refused
unless a separately authorized migration owns them. No evidence here forces a
second card block, but no existing-file tail ownership or complete transaction
lifetime has yet been issued. A separate file remains a fallback with its own
allocation/full-card/identity lifecycle, not a substitute for the RAM proof.

## Exact source and corrected address domain

Original USA image SHA256
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`;
EXE LBA 53875 / 0x66000, SHA256
`a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`.
WAD entry 2 is `5B800/3800`, directory bytes `00B8050000380000`, SHA256
`8ee66fe644407bf15f93af1615045fbaca17fe3daa9f3b67f38bee2277a94c7c`.

The overlay runtime mapping is **8007AA38 + file offset for both header and
code**. EXE word at 800113A0 equals 8007AA38; loader 80012978..80012998 passes
this destination and WAD entry-2 length/offset from 8007A6E4/8007A6E0 to
80016500. Directory installation 80012544..80012558 copies its 0x330 bytes to
8007A6D0. There is no 0x614-byte header/code displacement.

Earlier `Inspect-SpyroJapaneseCollectibles.py` and the internal ID65
save/death read-only contract incorrectly used the first state target as a code
base. A regression failed against that mapping before correction. Exact
byte-owner hashes are unchanged; historical JSON/candidate/app files were not
rewritten.

| Native fact | File offset | Correct PC | Previously reported PC |
| --- | ---: | ---: | ---: |
| First code: sound helper | 9C | 8007AAD4 | 8007B0E8 |
| 16-entry state-table dispatcher | 680 | 8007B0B8 | 8007B6CC |
| First state-table target | 6B0 | 8007B0E8 | not a code-base value |
| Existing preserved containing window | 1BFC | 8007C634 | 8007CC48 |
| Selected-slot call window | 1FC4 | 8007C9FC | 8007D010 |
| Call to deserialize 80059594 | 1FFC | 8007CA34 | 8007D048 |

There is also a control-flow correction: table entry 14 really is 8007CC48,
but that is the **common epilogue**, not the selected-slot load handler.
Outer phase `u32[80078D78]==1` uses the 16-entry table (indexed by 80078D88).
Outer phase 2 branches at 8007AD54 to 8007C454. That body initializes the slot
pointers and branches on substate 80078D7C. Its selected-slot deserialize call
is 8007CA34. The old containing window at 8007C634 actually starts in a branch
delay slot; it must not be described as a function entry. The corrected
internal report withdraws complete indirect-dispatch closure and its dependent
passive-save closure, while retaining the exact call and serializer evidence.
A complete outer/substate traversal is outside this bounded ownership report.
Repository references to that internal report are its dedicated smoke only;
no executable-issuance consumer was found.

## RAM placement, initialization and lifetime

`E = u32[800785E8]`, `F = u32[800785F0]` below denote live engine arena
pointers, **not fixed extension addresses**.

| Owner / consumer | Native evidence | Consequence |
| --- | --- | --- |
| Card-mode entry | 8002D170..8002D224 | Sets game mode 13 and clears exactly 0x5C card-state bytes at 80078D78. This does not clear card data. |
| Menu workspace pointer | 8002D1C8 load E; 8002D1E8 `addiu v0,-2000`; 8002D1F0 store g80078DC4 | Native `[E-2000,E)` placement. No heap allocator or size registration is called. |
| Frame layout | 8005B6F8..8005B7D4 | F=FC-6008; E=F-2*stride, stride=1C000 normal or 13000 alternate. Arena reconfiguration changes the anchor. |
| Earlier card intro | 8007AD64..8007AE6C | Outer phase 0 waits for CD conditions, requests 40000 bytes at E-40000 (8007ADFC), then uploads from that same range (8007AE60). Thus all menu save bytes can contain image data before the prefix read. |
| Menu slot pointers | 8007C4D4..8007C508 | g78DC8=buffer+200, g78DCC=buffer+800, g78DD0=buffer+E00. |
| Gameplay save scratch | 8003214C/3215C, 800321F0/321F8 | `[F-600,F)`; distinct from g78DC4, within a phase-reused arena. |
| Card exit | 8007CC00 calls 8006631C; 8007CC20 sets outer phase 3 | Shutdown waits until g80075B50 is zero before native close services. |
| Following update | 800339C0..800339F8 | Mode13 calls overlay update until phase3, then resident transition 80032B08; subsequent native level loading/arena reuse is not a persistent-ledger guarantee. |

The entry arithmetic proves an explicit 8 KiB *placement*, not a general
allocator reservation against every edited level, interrupt, alternate-mode,
reset or transition consumer. A new ledger still needs a source-bound owned
RAM extent and lifetime that survives ordinary play, loading, slot changes and
card retries. The current detached maximum ledger is 0x680 bytes: it exceeds
the gameplay 0x600 scratch by 0x80 even though it fits the 0xC00 on-card tail.
Do not conflate those two capacities.

## Every direct read/write setup in the bounded native owners

The probe scans the complete exact EXE payload and complete card-overlay code
for direct calls to 80066E28 (read) / 800670E4 (write), and pins this six-call
census. It executes the setup words with MIPS load/JAL delay semantics for
both controllers and all three slot indices; BIOS/card services are intercepted,
not executed. This is not an assertion that arbitrary indirect/BIOS callers
were globally proved absent.

| Caller | Operation | RAM buffer | File offset | Byte count |
| --- | --- | --- | --- | --- |
| 8007B36C | Menu read | E-2000 | 0 | 1400 |
| 8007BE90 | New-file prefix write | E-2000 | 0 | 1400 |
| 8007BF00 | Prefix verification read | E-2000 | 0 | 1400 |
| 80032158 | Gameplay header/identity read | F-600 | 0 | 80 |
| 80032230 | Gameplay selected-slot write | F-600 | 200+slot*600 | 600 |
| 800322B8 | Gameplay selected-slot verify read | F-600 | 200+slot*600 | 600 |

The creation call at 8007BDF0 passes count1 to 80067718. At 800677E4/E8/EC,
the library forms `(count<<16)|200` and calls the file-open veneer. Header
template 8006F494/200 begins `53 43 13 01`. No native creation/read path in
these owners initializes or reads `[1400,2000)`.

New-file construction copies the 200-byte header (8007BE2C), stores a native
16-bit generated identity at header+7E, then calls new-game initialization and
serializes three 600-byte slots (8007BE54, loop 8007BE5C..BE68). Serializer
80059864 clears the **whole 600** first and stores its checksum at +58C.
Checksum 8005956C sums precisely `[0,58C)`; neither slot `[590,600)` nor file
`[1400,2000)` has extension integrity or ownership. Bad ordinary slots can be
rebuilt by the menu; those branches confer no permission to reset earned
custom data. Gameplay compares the read header's 16-bit +7E with g80078D20
before saving; that small native identity is not a collision-free manifest ID.

## Native asynchronous service boundary

Both APIs reject busy g80075B50 or lengths not divisible by 0x80. They copy
file offset to g80075B64, buffer pointer to g80075B6C, count to g80075B68 and
controller to g80075B5C, then queue callbacks. Return1 means request accepted,
not persistence. Read callback 80066F34 seeks through 800684C4, then passes the
unchanged buffer/count to read veneer 800684D4 at 80067024. Write callback
800671F0 analogously calls 80062FC4 at 800672E0. Both poll completion, retry
some errors up to 16 attempts and close the descriptor. The library never
allocates, expands or clears the caller's buffer.

Before a tail writer could be enabled it must own that async lifetime, bind the
selected ordinary slot and content manifest, keep the native prefix untouched,
validate and read back its own checksum/version, preserve unknown/corrupt
bytes, and handle card removal, full card, recreation, reset, retry and partial
ordinary/tail transaction failure. Native success UI cannot report complete
custom progress until the extension is committed. These hooks and a persistent
RAM owner remain unimplemented; a second file would still need them.

## Reproduction

```
python3 tools/test_japanese_collectibles.py
python3 tools/test_usa_save_buffer_ownership.py
python3 tools/Inspect-SpyroUsaSaveBufferOwnership.py 'Spyro the Dragon (USA).bin'
```

Results: 12 existing diagnostic tests, 10 new source-free tests; exact source
capture verifies 30 hashed beforeimages, six direct callsites and 20 scalar
argument probes, then rehashes held and named original. Output is in
`_local/research/japanese-content-20260925-r01.RCmBCn/usa-save-buffer-ownership.json`.
No card, image, app, candidate, GUI, emulator or settings writes occurred.

The corrected internal diagnostic and its dedicated smoke build cleanly in
Debug and Release (zero warnings/errors). Its new semantic assertions failed against the
old implementation before correction, then all pass with corrected metadata
SHA256 `6c8595ee5b62826053354aa6df478cbe749f7cd58d3471ddc806057af4926bf0`.
The smoke checks the historical three input images/evidence and their containing
directories remained unchanged. Its result now explicitly says complete save
dispatch remains unproved; the invalid state-14 closure is not a green gate.

```
dotnet build src/Spyro.Editor.UnusedLevel65SaveDeathRespawnOwnershipClosureSmoke -c Debug --no-restore
dotnet src/Spyro.Editor.UnusedLevel65SaveDeathRespawnOwnershipClosureSmoke/bin/Debug/net10.0/Spyro.Editor.UnusedLevel65SaveDeathRespawnOwnershipClosureSmoke.dll .
```
