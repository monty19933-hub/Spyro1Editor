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
rejecting any signed-byte overflow. Teal cycles seven bright highlight/mid/shadow
shades (`120/255/240`, `32/200/190`, `75/240/215`, `30/200/200`,
`165/255/245`, `40/225/210`, `18/175/165`) to retain facet definition;
rainbow uses seven vivid hues with maximum channels at least 224. The donor gray value
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

The original `MarkCollected` already calls the native amount-to-numeral helper
at `0x800557CC` when the actor's Z is at least 768. It divides the actual amount
by ten, creates original numeral actors through the destination's native spawn
callback, and initializes their original movement/properties at the gem's world
position. Amount 50 creates classes `0x104` and `0x109` (ones 0, tens 50); amount 100
creates `0x104`, `0x104`, `0x105` (ones 0, tens 0, hundreds 100). The complete
0x190-byte helper is authenticated with SHA-256
`ede979bdf91a0602de89166b225633adf582a1c1a4eabd32e81f131271723709`.
`FreeMoby` then retires the placed gem with state FD. Native numeral actors own
their own properties and lifecycle, independently of the retired gem.

A live r4 run exposed redundant pickup text: the earlier scalar adapter had
mistakenly treated `0x800557CC` as an audio call and doubled it. The added custom
HUD therefore duplicated an existing native popup. The custom draw routine,
45-update timer and high-gem render-hook wiring have now been removed. The
current scalar executes the original decimal-numeral helper rather than
masking that call. The native popup floor guard and card schema are unchanged.

Nearby uncollected gems also emit one original type-12 idle star every 32
updates. An eight-star per-scan limit gives all 256 admitted placed rows a chance
before the earliest emitter repeats. Native type-12 RGB fades after 16 updates,
so idle emission adds at most 128 live records (4096 bytes) to the existing
8192-byte particle pool under this schedule. The active-only cooldown occupies
private property word4 and is never written to a native save. Retirement leaves
it inert; native numeral actors never use the placed gem properties. Distant, inactive, saved, modal and non-gameplay gems emit no idle stars.
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

The final isolated Release Core/smoke build passed with zero warnings/errors.
Source smoke passed **1,965 checks**, including every-byte rejection for native
helper owners, the new native numeral owner, exact doubled coordinates, both
signed-byte overflow directions, strict selected-preview RGB closure, bright
facet palettes and source immutability. Particle source smoke passed **1,005
checks**, covering all 35 destination owners, table/function mutation rejection
and native texture availability.

The current scalar passed **26,681 checks across 36 accounting cases**, plus
idle and native-popup boundary cases. It executes original native award,
placed retirement and amount-to-numeral instructions, including canonical
physical-to-logical save mapping. Both amounts are checked at levels 0/7/35,
physical rows 0/31/32/255 and remaps 0→195/1→255. Numeral classes and place values
are exact, saved gems emit no popup/effect, and remaining in the pickup radius
cannot award or create digits again. Native actor allocation, trigonometry,
sound and particle callbacks are explicit ABI doubles; decimal decomposition
and all pickup accounting execute actual source instructions.

The same scalar verifies 32-update idle cadence, collection after idle,
inactive/distant/modal gates, full-pool skip and freed-slot resume, and 256
nearby gems each receiving exactly two stars over 64 updates with at most eight
emissions per scan. Actual update sizes are 752 bytes plain and 768 with save-row
remapping. There is no custom draw routine or render-hook requirement; signs
keep their original separate render path. Composition capacity and native
visual acceptance belong to the root task and independent verification worker.

The expanded particle scalar run passed **12,355 checks across 280 cases**
using every original level's actual type-12 constructor/update instructions plus
original particle pool allocation and deletion. Both classes are checked with
count 1 idle and count 6 collection emissions, in empty and full 256-record pools.
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
