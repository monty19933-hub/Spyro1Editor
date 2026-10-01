# Imported gem collection and native save verification

The original shared CollectItem helper reads the actor's class directly to
choose its award and particle colour. Universal class remapping left that
implicit dependency unresolved: an Ice Cavern red gem in Artisans disappeared
without awarding a gem or setting its saved bit, and emitted incorrect effects.
The failed normal-export fixture remains preserved, with BIN SHA-256
`2a9b459c875b12f38cdc521ce9fbfff6c873cb2b6fb893aaf3ef62a29e7f5bd4`.

The adapter authenticates the complete native helper and its five colour words.
Only proven source-owned direct calls for remapped native gem classes 0x53–0x57
are redirected. The original class is supplied during collection, then the
actual imported model class is restored. The physical actor pointer stays
unchanged for native logical-save-row translation. The 48-byte stack frame
reserves the native argument home area and preserves helper outputs. Identity
and non-gem calls do not need this adapter. Its exact size enters the normal
composition and capacity calculation.

## Independent instruction and source checks

Release passed 18,439 source-free checks and 18,723 checks with native source
guards and written-disc readback. A separate interpreter executed the original
native helper and generated five-class adapter: 180 checks cover all five
awards/colours, physical persistence pointer, all 31 register outputs and restored
classes. A callee that overwrites the argument home area is included. These
checks alone are not gameplay evidence.

## Native red gem retest

An ordinary export places Ice Cavern row 2 in Artisans physical/logical row 174,
mapped class 0x1FD. BIN SHA-256:
`cbd392c157dece4f31f8eccf9d5b6de489d24d63c68e74480bfec1ccf15b19c4`.
Cold New Game, collection and continued gameplay passed at 30 game FPS. The
native global and Artisans counters became 1, both collection bitmaps were set,
the actor reached state 0xFD, and its imported class was restored. The pause HUD
displayed 1. This single red-gem run did not exercise a card reload or establish
complete effect-lifetime closure; the later shared-collector diagnosis below
supersedes its earlier visual-effects acceptance.

## All five values, ordinary save and full cold load

The second normal export imports Jacques rows 59, 56, 64, 107 and 40, with original
classes 0x53–0x57 and values 1, 2, 5, 10 and 25. Physical/logical rows are 174–178;
mapped classes are 0x1FD, 0x1F7, 0x1F6, 0x1F3 and 0x1F1. BIN SHA-256:
`8ac9623a8909d5a301a9885c4f4b56aabb4c798fbd43251319b667b8d0c5c1af`.
The composition leaves 50,580 bytes RAM headroom, including 13,536 bytes overlay
growth and 2,048 bytes model growth. Independent WAD readback matched the
composed level/overlay and every unrelated entry remained unchanged.

Ordinary controller movement collected all five gems. Floating native rewards
displayed their five values; the global and Artisans counters reached 43 and the
pause HUD displayed 43. All five durable and checkpoint bits were set, all five
actors reached state 0xFD and all five mapped classes were restored. Read-only
RAM SHA-256 after pickup:
`6f04543437b6c139bbc976cdb212510b1c3ea8673c58de7c6019601473e35602`.

Controller movement rescued Nestor and reached his fairy. The native Save Game
action displayed GAME SAVED and wrote the private memory card. Its first slot's
stored checksum and independent byte-sum both equal 364. The initially blank
card SHA-256 was
`52e9972468ee7f0864c110c06dc13530b71c05e1669c9a4cbe7e8c26d91be220`;
the saved card SHA-256 is
`64263a213bb41e898309c0c494a2c682e41396ff7800196c63b967539e3e1759`.

The emulator process was fully closed. A new process booted the same CUE and
used ordinary Load Game, with no save state. The game resumed in Artisans at
30 game FPS; the five imported gems remained absent. Global/Artisans counters
were still 43, all five durable bits restored, and all five actors were state
0xFD with their mapped classes. The transient checkpoint bitmap was clear after
load. Walking back through the pickup area did not add gems; the pause HUD
still displayed 43 and one rescued dragon. The card stayed byte-identical.
Cold-load read-only RAM SHA-256:
`2cfd0c4b6db11b341203b4bfefd8f8e5c0c94cda47812bd6ad6af73337d72a7b`.

Both runs used a private DuckStation profile, real USA firmware SHA-256
`71af94d1e47a68c11e8fdb9f8368040601514a42a5a399cda48c7d3bff1e99d3`,
Software renderer, Recompiler CPU and NTSC-U region. There were no game RAM
writes, navigation assistance or save states. Both test processes were closed;
the user's original editor, project, discs and cards were not used as writable
test targets.

## Source identity and evidence boundary

Regeneration with the final Core at `07c4f2c`, DLL SHA-256
`da35796ed5dc2ca8d8a49c3b54bd035fe60127e7cbfec23dedbb8e3bee820826`,
produces BIN files byte-identical to both award/persistence fixtures. All matrix
runtime fields remain not-tested. These six source variants in Artisans do not
prove every gem source/destination combination, every spawned collectible, or
all enemy behavior. Broader universal gameplay coverage remains unfinished.

## Shared destination collector found during the fresh-level retake

The six-object fresh-level test exposed persistent collection sprites despite
correct awards and collected bits. Read-only comparison found the same malformed
type-12 effects in the earlier Artisans five-gem RAM capture; walking away had
obscured them. The earlier save/reload and counter evidence remains valid, but
does not establish correct particle cleanup.

The destination's original behavior contains both direct calls to CollectItem
and a separate linked-actor colour lookup before collection. Both can receive
imported gem aliases and bypass the source-handler adapter. A mapped class can
therefore select unrelated colour-table data. The native effect subtracts
eight from each nonzero colour channel and deletes the effect when all channels
reach zero. The malformed colours are not multiples of eight, so their channels
wrap instead of reaching zero.

Commit `d01b500` covered authenticated destination-native collection calls.
The fresh and retail retakes still contained two malformed live particles, so
that correction did not establish clean effects. The fresh inventory did pass
43/43 gems and 1/1 egg after corrected treasure accounting; the retail inventory
passed 43/143. Both failed particle receipts remain preserved.

`UniversalDestinationGemColourAdapter` now also authenticates the independent
linked colour producer. A non-linking jump preserves the original LUI delay,
replays the index/address operations and substitutes canonical gem colours only
for imported gem aliases. The actor class remains unchanged. The trampoline
preserves the original return address, stack and scratch registers, and requires
the exact independent instruction following the native load delay. Final-byte
preimages are checked before publication. Particle lifetimes and palettes are
not altered. Source checks cover all 35 native destination owners; fresh and
retail runtime retakes of this combined correction are tracked separately.

## Final combined native effects retake

The `3196a94` retail five-gem export has BIN SHA-256
`bb9f6dba4a1316b4ce8d8409d061394da19b3401ea1485da4b538410203a55b5`.
Written readback passed 279 checks, including unchanged positions, rows 174-178,
179 physical rows, 87 dynamic slots, a treasure target of 143, 50,404 bytes RAM
headroom and all 254 unrelated WAD entries.

An ordinary cold New Game began with both gem counters zero and all five
collection bits clear. A controller-only forward walk collected all five
values. Both counters became 43, all five actors reached FD, and all five durable
and checkpoint bits were set. The inventory displayed 43/143. After unpaused
settling, independent full-RAM inspection found zero live type-12/kind-2
collection particles and zero malformed stars. Read-only RAM SHA-256:
`9e1267996f00fdf58a34c01cc802e2ed61d04c687b820e9112e30c055817813c`.
The process was fully closed and the private blank card remained unchanged.

A second new process booted the same final CUE and used ordinary Load Game from
a copy of the earlier native fairy-save card (SHA-256 `64263a213bb41e898309c0c494a2c682e41396ff7800196c63b967539e3e1759`).
It restored 43 gems, one dragon and all five FD actors with durable bits set;
the transient checkpoint bits were clear. Walking through the pickup area
awarded nothing twice. The inventory remained 43/143 and 1/4 dragons, and the
particle gate remained zero. The private process was closed and its copied
card remained unchanged. Cold-load RAM SHA-256:
`68072e36675dae97a1f5ddae8417a118975c956c12ae51488e5c509fe9136723`.
This is final-candidate cold-load compatibility evidence; the card write itself
was performed on the earlier fixture described above.

The same combined source also passed the six-object fresh retake, including
43/43 gems, egg 1/1, jumping, walking and landing, and zero settled collection
particles in two captures. See `universal-fresh-multiple-objects-20261001.md`.
These tests use the real USA BIOS and no RAM writes or save states. They do not
convert the full variant matrix into runtime acceptance.

Private evidence is under the task's `work/universal-v1-continuation/runtime`:
`normal-ice-fixed-native-receipt.json`, `normal-five-gems-native-receipt.json`,
the before/after/cold-loaded RAM receipts, launch receipts and three five-gem
screenshots. Source, application and public archives exclude discs, firmware,
cards, RAM dumps and extracted game assets.
