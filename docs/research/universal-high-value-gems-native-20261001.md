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
vertex table. Geometry, counts, pointers and read-ahead bytes remain unchanged;
the original PETE registration/payload is unchanged. There are no external
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

Both values emit six original type-12 collection stars. Teal RGB is `00/80/80`;
rainbow RGB is `80/80/80`. Each channel is a multiple of eight, so the unchanged
native subtract-eight fade terminates. Complete original particle spawn/update
function owners and their separate type-12 jump-table words are authenticated
for every destination, including prepared fresh hosts. All 35 original levels
have their own type-12 texture record; none is replaced. Native particle pool
allocation/deletion owners are authenticated too.

## Checks and evidence boundary

An isolated Release Core/smoke build passed without warnings. Source smoke
passed **2,323 checks**, including individual mutation rejection for every
native helper owner byte, source model guards, admitted indexed-RGB bounds and
preserved geometry/read-ahead bytes. Particle source smoke passed **1,005
checks**, covering all 35 native destination owners, table/function mutation
rejection and native texture availability.

Strict scalar execution passed **22,147 checks across 36 cases** for emitted
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

A second strict scalar run passed **3,605 checks across 70 cases** using every
original level's actual type-12 constructor/update instructions plus original
particle pool allocation and deletion. Both classes allocate six native stars;
every star fades and retires after 16 updates, and the pool returns to its
original empty/reusable state. Matrix/vector transforms and PRNG are ABI doubles;
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
