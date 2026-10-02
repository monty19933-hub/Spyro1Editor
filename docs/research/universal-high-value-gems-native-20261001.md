# Teal 50 and rainbow 100: native collectible contract

These loose gems are editor special assets, with source key `editor` and IDs
`teal-gem-50` / `rainbow-gem-100`. Their runtime classes are `0x1CF` and `0x1DA`.
All 35 original USA levels exclude both classes from native rows, local/shared
model registrations, update dispatch and conservative integer-immediate scans.
The five retail gem classes, their reward selectors and recent-gem HUD ring are
unchanged. Ordinary level and fresh-level authoring both use the special-object
composition path; an editor label is not the native award implementation.

The model is a private copy of the certified USA purple shared PETE simple model
(SHA-256 `0c7823412b2a1a1df1da587d6bc9d9cde824062d4b9387715075112db241caea`).
It has 14 vertices, 18 faces and 15 RGB entries. The retail profile faces do not
use those white RGB entries directly, so both copies convert the face colour
commands to the native indexed-RGB mode. The same geometry vertices supply the
colour indices. Teal colours every vertex; rainbow cycles seven hues across the
vertex table. The current copies double every signed-byte coordinate exactly,
rejecting any signed-byte overflow. Teal uses full RGB `48/255/232`; rainbow
uses seven vivid hues with maximum channels at least 224. The donor gray value
of 127 no longer halves these authored colors. Counts and vertex/RGB pointers
are preserved. The unused fifteenth RGB word is removed, closing the indexed
RGB interval at fourteen words; the complete unchanged face/read-ahead stream
moves four bytes earlier and its pointer changes from 120 to 116. Each copy is
268 bytes and must pass the same strict geometry decoder used by selected-model
preview. The original PETE registration/payload is unchanged. There are no external
texture dependencies or embedded extracted assets in source control.

The update scans the actual placed-row range, spins active gems, and picks them
up within its native proximity bounds. It calls the original native
`MarkCollected(50 or 100, physicalActor)` at `0x8003B854`; that helper updates
current-level and global treasure and checkpoint/durable saved identity. When
physical slots map to logical saved slots, the existing certified save-map
routine handles both the award and the update's explicit already-collected
guard. A loaded collected gem retires without another award, effect or popup.

`FreeMoby`'s authenticated placed-row path removes the object and marks it FD
without reclaiming its model class or private properties. A 45-update popup timer
therefore survives retirement. Its age advances in the update even offscreen.
At render time the existing source-owned sign/HUD gateway invokes native 3D HUD
text for `50` or `100`, rising until that timer expires. A visible-list budget
preserves native HUD capacity. Timers are transient private properties and are
zero on a new load; the native card schema and checksum are unchanged.

Nearby uncollected gems also emit one original type-12 idle star every 32
updates. An eight-star per-scan limit gives all 256 admitted placed rows a chance
before the earliest emitter repeats. Native type-12 RGB fades after 16 updates,
so idle emission adds at most 128 live records (4096 bytes) to the existing
8192-byte particle pool under this schedule. The active-only cooldown occupies
private property word4; retired popup drawing then owns that same word as its
step X. Distant, inactive, saved, modal and non-gameplay gems emit no idle stars.
An idle-only check of the certified native next-free pointer skips a full pool
without consuming cooldown or budget, so idle cosmetics never invoke native
random replacement. Collection retains the original native replacement policy.
Neither effect retains an actor pointer or creates a persistent allocation.

Both values emit six original type-12 collection stars. Teal RGB is `00/80/80`;
rainbow RGB is `80/80/80`. Each channel is a multiple of eight, so the unchanged
native subtract-eight fade terminates. Complete original particle spawn/update
function owners and their separate type-12 jump-table words are authenticated
for every destination, including prepared fresh hosts. All 35 original levels
have their own type-12 texture record; none is replaced. Native particle pool
allocation/deletion owners are authenticated too.

## Checks and evidence boundary

The initial isolated Release Core/smoke build passed without warnings. Initial
source smoke passed **2,323 checks**, including individual mutation rejection for every
native helper owner byte, source model guards, admitted indexed-RGB bounds and
preserved original geometry/read-ahead bytes. The brighter/larger follow-up
adds exact doubled-coordinate and overflow checks, strict selected-preview
RGB closure, source immutability, bright palettes and a native code growth limit. Particle source smoke passed **1,005
checks**, covering all 35 native destination owners, table/function mutation
rejection and native texture availability.

The initial scalar execution passed **22,147 checks across 36 cases** for emitted
update/draw plus actual native award/placed retirement instructions. Cases cover
both values, levels 0/7/35, rows 0/31/32/255, physical-to-logical mappings 0→195
and 1→255, exact native totals and saved bits, already-collected suppression,
offscreen timer expiration, native HUD text arguments, no duplicate award,
retail-five isolation and visible-list capacity. Audio, particle callbacks and
HUD text are explicitly labelled ABI effect doubles in the broad matrix. A
separate run executes actual original `SpawnText` instructions, with only its
constructor/three-word copy helpers doubled, to verify both values' numeral
classes and identical native digit height/depth. This caught and corrected an
initial native step-vector depth/diagonal bug. Popup drawing now also respects
the native HUD classifier and normal gameplay mode.

The brighter/larger follow-up scalar execution passed **26,016 checks**. It
retains all 36 accounting cases and adds both idle cadences, collection after
idle, inactive/distant/modal gates, full-pool skip and freed-slot resume, and
256 simultaneous nearby gems receiving exactly two stars each over 64 updates
with no more than eight emissions in any scan. Neither active gem rendering
nor idle emission changes saved identity, treasure or popup age. Actual emitted
update sizes are 784 bytes plain and 800 bytes with save-row remapping; both
grow by 184 bytes, or at most 192 after resident alignment. Draw remains 408
bytes. Composition capacity and native visual acceptance belong to the root
task and its independent verification worker.

The expanded particle scalar run passed **12,355 checks across 280 cases**
using every original level's actual type-12 constructor/update instructions plus
original particle pool allocation and deletion. Both classes are checked with
count1 idle and count6 collection emissions, in empty and full 256-record pools.
The native full-pool allocator reuses a bounded existing record; all writes
remain within the pool. Every star fades and retires after 16 updates, and the
pool returns to its original empty/reusable state. The full-pool fixture owns
an explicit trailing tag read-ahead, as required by the native update loop. Matrix/vector transforms and PRNG are ABI doubles;
colour aging and pool retirement are the original native instructions.

These checks establish source ownership, bounded native instruction behavior,
accounting and transient lifecycle. They do not establish visual gameplay,
GTE/timing accuracy, human acceptance or an ordinary memory-card save/cold load.
The root task owns the independent composition/readback and runtime receipts.
No emulator launch, UI action, RAM write or card write was performed by this
native-analysis worker. Originals and earlier private fixtures were read only.

## Reproduce

Use an isolated build/output directory when another worker is building the
shared checkout. From the repository root, after a Release smoke build:

```sh
dotnet src/Spyro.Editor.UniversalHighValueGemSmoke/bin/Release/net10.0/Spyro.Editor.UniversalHighValueGemSmoke.dll /path/to/original-USA.bin .
dotnet src/Spyro.Editor.UniversalHighValueGemSmoke/bin/Release/net10.0/Spyro.Editor.UniversalHighValueGemSmoke.dll --emit /private/new-emitted.json
python3 tools/Test-UniversalHighValueGems.py --source-image /path/to/original-USA.bin --emitted /private/new-emitted.json --receipt /private/new-scalar-receipt.json

dotnet src/Spyro.Editor.UniversalHighValueGemSmoke/bin/Release/net10.0/Spyro.Editor.UniversalHighValueGemSmoke.dll --emit-particles /path/to/original-USA.bin . /private/new-particle-owners.json
python3 tools/Test-UniversalHighValueGemParticles.py --source-image /path/to/original-USA.bin --owners /private/new-particle-owners.json --receipt /private/new-particle-receipt.json
```

The particle owner output includes extracted native code and belongs in private
ignored work storage. Source/image, firmware, cards and extracted assets must
not enter public packages. Native worker receipts and the isolated build are at
`work/high-value-gems-native/` in this checkout.
