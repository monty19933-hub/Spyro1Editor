# Spyro Editor Beta V4 Known Limitations

This beta is useful, but it is not a finished Spyro modding tool yet. Treat it as a testing build for editor workflow, object placement, terrain inspection, and moby identity mapping.

## Do Not Ship Or Share Game Data

The beta package must not include:

- Spyro disc images or patched disc images.
- BIOS files.
- Emulator saves or save states.
- RAM or VRAM dumps.
- Extracted WAD payloads.
- Runtime scene overlays generated from the game.
- Texture dumps or actor atlases.

Users must provide their own legally obtained game image and generate local cache data on their own machine.

## Level Names

All 35 catalog levels have indexed name editing. Regular level names share one
native string between portal lettering, entering/confronting text, and the
guidebook, so those appearances change together. Homeworld title strings are a
separate indexed table used by balloon transitions and guidebook headings.

Regular level and homeworld names can use up to 19 characters. Boss names can
use up to 16 because the longer `CONFRONTING` prefix must still fit the game's
32-byte transition buffer. Names longer than their original slot are packed
into the executable's bounded shared name region, with indexed pointers and
homeworld title lengths updated together. The editor refuses the export and
reports the minimum number of characters to remove if the combined saved names
would exceed that region.

Gold portal lettering supports uppercase A-Z, spaces, and apostrophes; the
editor rejects unsupported characters instead of letting the game render them
as apostrophes. The global `RETURN HOME` lettering is a separate string and is
not renamed per homeworld.

## Level Music

All 35 catalog levels can select from 46 built-in XA tracks already present on
the user's disc. This includes the normal level themes, title music, and hidden
late-play alternates. A saved override patches the level's initial track and
keeps that choice active when the native 8-12 minute alternate-music timer
fires.

Custom audio import is not enabled in this build. Tracks 30 and 35 are not used
by the normal level/title/late-alternate mappings and are reserved as candidate
custom-audio slots. A later importer still needs to encode user-supplied audio
as PlayStation XA, preserve the interleaved stream sectors, enforce the slot's
length limit, and verify playback in DuckStation. No game music or custom audio
is bundled with the editor.

## Object Editing

### Native Movement Controls

`Edit Run Path` is limited to the 12 verified native Egg Thieves on the checked
USA retail disc. Their existing fixed nodes can move, but nodes cannot be added,
deleted, or reordered. A route whose owner identity, native properties pointer,
node count, or original bytes no longer matches is rejected rather than
reinterpreted. Runtime validation is still required for unusually aggressive
route edits even when the byte-level export is safe.

`Spyro runs here` is limited to the 79 native dragon rescue scenes. It changes
only the native planar angle and radius; Z remains terrain-derived. Copied or
brand-new dragons do not gain rescue choreography. Zero/overflowing radii,
stale source bytes, and invalid numbers are blocked, while unusually long
destinations or endpoints without a terrain hit require Build Safety review.

### Special Chest Profiles

Beta V4 includes the checked profile registry, atomic editor grouping, and
Build Safety needed to develop special chests level by level. It does not mean
every chest family is now runtime-proven in every level. Normal Add/Create BIN
continues to expose only the proven Artisans gold Key + Locked Chest import.
Other destination profiles remain disposable-test-only or blocked until their
complete handler, companion/controller, texture/CLUT, reward, persistence, and
cleanup matrix passes DuckStation. Native presence is not proof that an extra
copy is safe. The five flight stages remain explicitly unsupported.

### Gem Values And Editor Icons

Decoded native loose gems can be changed between the five paired Spyro 1
encodings: Red (1), Green (2), Blue (5), Yellow (10), and Purple (25). The
editor writes the loose gem's native identity byte and value byte together; it
does not offer arbitrary values or treat an unpaired/unknown row as a safe gem.
Contained and reward gems continue to use their separately decoded native
fields.

Edit Map and Game Camera keep the original red gem editor artwork and use exact
color-only variants of that same silhouette for green, blue, yellow, and purple.
All five icons therefore share the same canvas, facets, placement, and alpha
mask. These are editor viewport assets, not extracted game textures, and they
do not change the gem's in-game model, palette, or behavior. An unknown value or
unavailable icon falls back to the procedural marker. The five-value edit path
has offline encode/writeback smoke coverage; this documentation does not claim
new runtime proof for every changed gem in every level.

Generally safer:

- Moving known gems.
- Moving known scenery.
- Moving known keys.
- Moving known chests that already exist in that level.
- Editing existing gem value bytes that are already proven by smoke tests.
- Replacing an existing object slot with a same-level donor object through
  `Edit` > `Replace object`.
- Using `Replace` to choose another same-level ordinary chest or
  self-contained enemy donor for an existing source slot.

Still guarded:

- Adding or copy/pasting more same-level enemies or chests than the exporter can
  route through matching reusable source slots. Large Gnorcs, ordinary
  Flame/Charge Chests, and Town Square Bulls currently receive one provisional
  true-add per family after slot reuse; further rows remain saved but are skipped.
  Same-level native Life Chests retain their separate private-runtime support.
- Copy/pasting arbitrary extra enemies, copied scenery, and copied chests as new
  source records when no same-level identity match is available.
- Cross-level `Replace` choices remain disposable existing-slot candidates,
  except for the runtime-verified Blowhard Green Wizard v2 route described
  below.
  Resident-class tests only use actor classes already loaded by the target;
  package-backed tests also import a checked model/special-data recipe. Normal
  `Create BIN` deliberately skips the complete unverified edit, including its
  identity bytes. `Create Swap Test` replaces one 0x58-byte target row, preserves
  its placement, keeps the source-object count unchanged, and still needs
  DuckStation proof for visuals, interaction, reward, and nearby-object stability.
- Catalogue entries marked `Needs model/behavior map` cannot be staged until the
  target-level actor package and any required overlay behavior are understood.
- The Toasty Dog to Wizard Peak Green Wizard v3 experiment is a useful boundary
  proof, not a supported swap. DuckStation loaded the imported model/root, but
  Toasty's level overlay has no update case for actor `0x011B` or the wizard's
  spawned lightning actor `0x0026`. The actor was distorted, inherited the Dog's
  pod/group effect, hurt Spyro on contact, did not cast lightning, and could not
  be defeated. Both normal and candidate builds now block this exact recipe;
  importing a model package is not treated as importing executable behavior.
- The later Toasty v11 full Green Wizard bundle is runtime-proven and supersedes
  that failed row/model-only experiment as the behavior proof: lightning damage,
  Wizard death, gem reward, model textures, and the lightning-trail particle all
  passed DuckStation play. Its editor route remains target-profile-gated.
- Blowhard Green Wizard v2 is runtime-verified and normal `Create BIN` now
  automatically composes its dependency-aware structural recipe. `Create Swap
  Test` remains available for isolated regression builds. Repeated casts produced real lightning, two
  separate bolts damaged Spyro normally, and the Wizard produced one death
  animation/sound, dropped one gem, remained gone, and left the surrounding area
  normal. The first composed resident recipe (v1) instead put its 0x40-byte
  mutable Wizard work block inside Blowhard's live collision-chain component,
  causing non-damaging cast loops and a repeating death state; v1 is retired and
  retained only as failed diagnostic evidence. V2 expands the real
  Moby-properties component, shifts the pod/collision/fixup components intact,
  preserves the target pod, and translates the native T0 route. Arbitrary true
  Add and multiple Wizard allocations remain unavailable until their separate
  properties, route, and fixup allocation policy is proven.
- Magic Crafters T107 -> T27 is runtime-verified. Select the original T27 Armored
  Druid and use `Replace` / Swap Catalog to choose the native T107 Green Wizard;
  normal `Create BIN` and `Create Swap Test` both compose v2. The expanded-properties v1 candidate cast
  normally, was attackable, used normal death animation/sounds, and dropped its
  gem, but its lightning never hit Spyro. V1 is retired as partial-failure
  evidence. V2 instead installs T107's private 0x50-byte/two-point properties in
  T27's existing 0x74-byte extent, removes the obsolete 0xE9F8 fixup, and leaves
  the properties size, pod, collision, and later scene components at their native
  offsets. DuckStation confirmed the real lightning hit while casting,
  attackability, death/sound, gem, retirement, and nearby behavior remained
  normal. Other Magic Crafters targets and true Add remain unavailable.
- Wizard Peak native T6 -> Elder Wizard T24 is runtime-proven for this exact
  existing-slot route. Retired T10 v1 was
  visible, killable, and dropped its gem, but never used its lightning attack.
  Live RAM confirmed that T10's properties and route pointers were relocated
  correctly, while the private `+0x04` state flags never took the native
  idle-to-attack transition. V1 retained Elder Wizard pod/group `0x03`; v2
  isolated T6's proven detached pod/group `0xFF` and still never attacked.
  Runtime-proven v3 therefore uses T24, whose original pod/group already matches
  `0xFF`. It keeps T24's placement `(39670,64655,27648)`, yaw `0x2B`, culling
  sector, and reward `0x56`; reuses the unique `0x74`-byte properties extent at
  `0xCAC0`; preserves the trailing `0x24` bytes; translates both route points;
  and replaces fixup `0xCACC` with `0xCAC0` at index 37 while keeping the
  `0xBF` count and all scene-component offsets unchanged. The user accepted
  visible lightning, the translated route, and one gem; live RAM separately
  confirmed actor/model initialization, attack state, movement, death, and
  retirement. Normal `Create BIN` and `Create Swap Test` compose only exact
  T6-to-T24 v3. Other Wizard Peak targets and true Add remain unavailable.
- Adding a cross-level chest or enemy as an extra source row. The first catalogue
  slice is swap-only.
- Unmapped actor/package imports.
- Spring Chest normal exports.
- Gnasty's Loot Spring, Firework, and 3x Flame Chest normal exports.
- Key Chest in levels where no native proof exists.
- Wizard/enemy transforms.
- Anything that needs linked controller, reward, collision, or behavior records.
- Placeholder-looking mobys that may be triggers, camera starts, reward links,
  route markers, or helper/controller records. The editor shows these as
  system/control data and release builds do not offer them as normal Add Object
  donors or copy/paste objects yet.

### Build Safety Inspector

`Objects` > `Build safety` and `More` > `Build Safety` inspect every saved object
patch plan against the native Level Moby component. `Create BIN` runs the same
inspection automatically before writing object edits.

The inspector uses the game's decoded loader and allocator rules rather than
only counting editor donor slots:

- A static source row is 0x58 bytes. A true append takes that space from the
  same component arena used for runtime Mobys and their props.
- Each runtime allocation needs a 0x58-byte Moby plus 24 bytes of props, so the
  report shows allocator-rounded capacity before and after the planned rows.
- Stock killed/collected bookkeeping has eight 32-bit masks, so a build above
  256 persistent static indexes is blocked.
- A copied row is not equivalent to class-specific `SpawnMoby` initialization.
  Props, pods, routes, controllers, rewards, and actor-family setup can still be
  required even when the row fits.

`Stable` preserves the native arena and bookkeeping range. `Review` identifies
true appends, lost runtime capacity, skipped edits, or test-only package work and
can continue only after confirmation. `Blocked` stops writes that exceed the
component, exceed 256 static indexes, use an unresolved native layout, or append
against a nonstandard runtime-row alignment. The current source-table catalog
contains two flight-level layouts whose native runtime rows begin four bytes
before the catalog-aligned prefix; edits to existing rows remain available, but
true appends there are blocked until component repacking handles that alignment.

The output folder receives `.build-safety.json` and `.build-safety.md` reports.
They contain level names, counts, offsets, findings, and source links only, not
game data. This inspector exposes the real budget; it does not yet expand or
repack the Level Moby component.

Native references: [Level Moby loader](https://github.com/TheMobyCollective/spyro-1/blob/4e7b7f06e552a20b9fad76e8ee67e68027b39082/src/loaders.c#L630-L648),
[runtime allocator](https://github.com/TheMobyCollective/spyro-1/blob/4e7b7f06e552a20b9fad76e8ee67e68027b39082/asm/42CC4.s#L12-L57),
[persistent masks](https://github.com/TheMobyCollective/spyro-1/blob/4e7b7f06e552a20b9fad76e8ee67e68027b39082/include/checkpoint.h#L17-L21), and
[class-specific spawn setup](https://github.com/TheMobyCollective/spyro-1/blob/4e7b7f06e552a20b9fad76e8ee67e68027b39082/include/overlays/moby_spawn.inc.h#L13-L71).

Dragon rescue scenes are the exception to the generic camera/control guard. The
editor now matches all 79 native dragon actors one-to-one with their 79
pedestals and 79 `0x6E` scene-link controls across 28 levels. The actual rescue
camera uses two additional sources linked through the dragon: a packed approach
camera record and a later cinematic track made of 24-byte camera keyframes.
Moving an existing dragon carries the three editor rows, shifts the approach
camera XYZ, and translates every cinematic keyframe by the same delta while
preserving the native shot angles and timing. This addresses both camera phases
returning to the dragon's original location.
Artisans DuckStation testing confirmed that both camera phases follow a moved
dragon and play at the edited location. All 79 native scenes pass the same
source-layout and export checks, but camera timing, easing, and shot
choreography are not separately editable yet. Copying brand-new dragon rescue
scenes is still beta: the companion rows are preserved, but allocating
additional packed scene data has not been promoted as release-safe.

Homeworld-to-level arrival is exposed as an exact `Fly-in Landing` marker for
all 29 destination levels. `Create BIN` patches the destination entry-block XYZ
and its decoded approach-heading byte. The opposite direction, including
return-home landing and camera behavior, is separate data and remains out of
scope for this first pass.

Homeworld portal movement is available for all 29 native portal sets. Each
source-proven set links the lettering, companion control, and travel path, while
`Create BIN` applies the movement to the dedicated portal plane, private
two-node Spyro transition route, and type-6 walk-in collision surface, then
rebuilds its collision lookup. Decorative stone arches, pads, and surrounding
terrain scenery remain separate, and the beta cannot allocate brand-new portal
destinations safely.

Artisans DuckStation testing confirmed that a relocated Stone Hill portal now
enters the destination without snapping Spyro back to the original doorway. The
six legacy proximity-based Artisans portal groups are review-only; all movable
homeworld portal links come from the exact native 29-portal catalog.

If an object appears visually but does not react correctly in-game, do not promote it as working.

Enemy adds need extra behavior/home/path data that a single new source row may
not carry correctly. `Create BIN` now tries to consume matching same-level
source slots first for add, copy, and paste workflows. Large Gnorcs, ordinary
Flame/Charge Chests, and Town Square Bulls may use one validated true-add per
family after those slots are consumed; later copies are deliberately omitted
from normal BINs because multiple active appends have produced delayed and
random in-game crashes. Artisans live RAM proved that a Life Chest donor must
remain untouched and each appended copy needs a unique pre-relocated private
runtime block. The original T94 and appended T174/T175/T176 loaded together
with independent pointers and intact positions. The level-loader layout now
provides the equivalent scene-relative private allocation in all 22 levels with
native Life Chests, including the one-row source-count prefixes in Beast Makers
and Terrace Village. Cross-level Life Chest imports remain guarded.
Small/Regular Gnorc-style families remain guarded beyond reusable slots because
live testing showed they can spawn inactive and cannot be flamed or charged. Use
`Edit` > `Replace object` when you want to choose the exact source slot that
is consumed. Add Object and copy/paste display a warning when a placement uses
the last known-safe slot for one of these unproven families; objects of that kind
placed afterward may not work as intended until a later update expands support.
When only guarded edits are present, the editor removes older
matching test BIN/CUE output so DuckStation cannot accidentally load a stale
unsafe disc.

Town Square true-append research is currently failed for several families:
Torro Gnorcs spawn inert, 3x Flame Chests can destroy themselves as the level
loads, and Charge Chests may fail to spawn or render as broken red geometry.
Normal `Create BIN` must keep those rows guarded after same-level reusable slots
are consumed until the deeper actor/chest behavior allocation is decoded.
The current research smoke report confirms that source-row append plus cloned
special data is enough for the Bull baseline, and that native-clone research
appends now preserve donor startup bytes instead of editor-cache state. The
Town Square research path also preserves the donor `0xFF` placement-sector byte
for guarded Torro/chest appends, because the prior forced terrain sector was a
live-behavior mismatch against the working donors. The remaining 3x Flame Chest
clue is its colocated runtime/control row T108 (`renderRadius=0x1A`,
`nativeClassLowByte=0x88`), which must be allocated with the chest shell before
that family can be promoted.

## Spring Chest Status

The supported release path is **From this level: Spring Chest (native Spring
Chest)**. That clones a Spring Chest donor that already exists in the loaded
level, including its same-level source special data. Use that first whenever it
appears in Add Object.

Cross-level Spring Chest candidates from Gnasty's Loot, Peace Keepers, Dry
Canyon, or other donor levels are not part of the release UI. They remain
post-release research until the linked controller/reward/behavior data is
understood. Do not report a cross-level Spring Chest as supported unless a
future candidate:

- Boots cleanly.
- Appears in the intended level.
- Reacts to flame or charge.
- Plays/behaves like a real spring chest.
- Spawns a visible gem.
- Does not let Sparx collect the gem incorrectly.
- Resets or disappears correctly after collection.
- Does not break nearby objects.

## Key And Locked Chest Status

Artisans now has one supported expanded V2 route for a native gold Key + Locked
Chest pair. The pair is saved and undone like other editor object changes and is
included by normal `Create BIN`. Its checked composer expands/rebases the
required level structures, transplants the native handler/dependencies, imports
the Key and Locked Chest model data, and uses private texture/CLUT storage rather
than overwriting a resident actor. The supported budget is exactly one pair in
Artisans.

For this beta, that pair must be the only saved Artisans object edit in the
same build, and it cannot be combined with an Artisans terrain, environment,
or sky edit. Those edits can change the exact guarded Artisans data/overlay
layout that the runtime-proven V2 composer verifies. Ordinary in-place edits
in other levels, plus level-name and music edits applied afterward, can remain
in the same `Create BIN` output. A build that already used another structural
WAD relocation is rejected rather than producing an unverified disc.

DuckStation testing proved the gold Key and correct Locked Chest textures, the
native Key-gated opening and sound, six separate gem objects totaling +10 treasure,
one reward sequence, one-shot cleanup, and persistence after collection. This
is the promoted path; it is not the older wooden-chest T50 shim and it is not a
generic source-row clone.

The standalone lightweight Key remains universal. Locked Chest portability is
still target-profile-gated:

- 13 levels already contain native class `0x00AE`, but native presence alone
  does not prove a new true-add allocation. Each still needs focused add-path
  validation before broad promotion.
- 16 other regular levels without a native Locked Chest still need per-level
  handler, model, texture/CLUT, reward, and relocation profiles comparable to
  the proven Artisans bundle.
- All five flight layouts remain blocked for true appends because their native
  runtime rows use the unresolved nonstandard alignment described by Build
  Safety.

The earlier Artisans Peace Keepers Spring Chest and Key Chest portability CUEs
remain explicit failed evidence. Both placed imported model data at level-relative
`0x30800`, which is in Artisans nested subfile 0 rather than its native
actor/model subfile 2 (`0x17C000`-`0x1C9800`), and then appended a lower root
after `0x1C95F0`. DuckStation did not encounter that invalid package until
proximity activated the chest, producing the black-screen halt. The exporter
now rejects any actor-package range outside the inferred actor/model subfile
and requires the final actor-root table to remain contiguous and strictly
ascending, even in disposable candidate mode.

Replacing a resident Artisans actor is not a safe shortcut. The qualifying
source-row-free packages are still used dynamically by Spyro, Sparx, drowning
effects, the Life Chest's Life Statue/Life Orb rewards, or other native paths.
Artisans therefore still has no safe in-place Spring Chest or Key Chest package
slot. The promoted Locked Chest V2 route succeeds by expanding/repacking the
actor/model subfile and installing its complete runtime profile; it does not
make an in-place resident slot safe. Artisans still has no supported cross-level
Spring Chest route.

The research-only Artisans Spring Chest v162 candidate exercised the first of
those safe architectures. It appended the byte-identical `0x0149` package at the
old end of actor/model subfile 2, grew the subfile/WAD by one sector, kept the
root table ascending, shifted the later nested subfiles and executable through
the checked ISO relocator, added the missing T174/T175 scene pointer fixups, and
rebased all seven v159 helper loads affected by the scene shift. DuckStation
proved that this repaired layout boots, survives approach, and allows the
reward to be collected, but the candidate is still blocked: its stiff 48-frame
Z-only pop and disappearance come from the synthetic v159 helper by design.
The imported package itself is complete. Correct support now requires the real
Peace Keepers Spring handler/dispatch plus its shared special-data and support
rows, not another model copy or scalar animation tweak. The native Peace Keepers
case spans `0x800885B8`-`0x80088D3F` (`0x788` bytes), branches into shared
overlay labels, and spawns fragment actors `0x0043`/`0x0044`/`0x0045`. Artisans
has neither those fragment packages nor their native spawn initialization, and
its `0xE000`-byte overlay ends before the whole source case. No v163 candidate
is generated until checked overlay expansion, relocated shared branches/calls,
and the complete fragment/SpawnMoby closure are proven. The Key Chest still has
no safe package-only candidate because Artisans has no native `0x00AE` behavior
dispatch; package layout alone cannot make that transplant valid. The promoted
Locked Chest V2 profile resolves that separate problem by transplanting and
verifying the required behavior/dependency bundle as well as the model.

The earlier Artisans Key-gated T50 route did not import a Locked Chest at all.
It appended an exact native Peace Keepers Key plus its
special data and scene-pointer fixup, then filtered only native wooden chest
T50's damage bits until the global Key flag was set. Every other `0x00C2` chest
returned immediately to the original handler. This was a behavior-first runtime
proof: T50 intentionally keeps its Artisans wooden-chest model, sound, native
green-gem reward, and one-shot removal. Key v2 is permanently blocked because
it mistook live PsyQ interrupt-system state at `0x80073924` for an executable
cave, corrupting startup before the game logos. Key v3 moved the same checked
88-byte shim to the separately runtime-proven `0x8007314C` helper region and
verified the interrupt-state range remained retail-exact. The native Locked
Chest V2 pair now supersedes this wooden-chest research route for normal
`Create BIN`; the T50 route remains historical evidence only.

Executable patch planning now permanently rejects writes overlapping
`0x80073924`-`0x800749AB`. A long all-zero EXE preimage is not sufficient proof
that a runtime range is an unused code cave.

## Terrain Editing

The editor can preview terrain surfaces, stage supported height edits, and swap
source-proven native terrain/building texture records between levels. Terrain
behavior outside the decoded native collision signatures is not fully decoded.

The shipping editor retains every captured editable source-terrain face; no
normal view removes geometry from the project. **Edit Map** is the exhaustive
top-down locator, and **Fit All** frames the complete captured scene. Every
captured face stays material-rendered and selectable. The game does not submit
every environment group together, so this exhaustive view can expose genuine
source sheets that are absent from any one gameplay camera. It is an editing
projection, not a gameplay screenshot.

**Game Camera** is the default after level load. Where the source occlusion
payload is available, it resolves the camera against the decoded collision
triangles, chooses the highest source triangle below the camera, and retains
that triangle's native environment-group byte as camera context. Every stored
unique sector now follows the ordinary native HP/LP distance queues, including
off-group terrain: close faces use their decoded texture material and only
genuinely distant faces use source LP Gouraud colors. Every visible HP face
keeps its material; no audited sheet is converted to outline-only mesh. The
active source group remains coarse painter-order context. This is editor
presentation only and never removes terrain from editing, saved manifests, or
`Create BIN` output.

The portable cache now includes `editor-cache/level-entry-poses.json`: one exact
retail entry XYZ/yaw record for each of the 35 catalog levels, including the six
homeworlds. The sidecar carries source-image provenance, catalog identity, and a
hash of the canonical pose payload; a missing row, duplicate key, invalid
coordinate, contract mismatch, or hash mismatch rejects the whole sidecar.
After that complete cache passes readback validation, Game Camera can recover the
same source entry focus and heading without the original BIN/CUE at viewport
runtime. The original image is still needed to build or rebuild the cache and to
create patched disc output.

The cached record is an entry/player focus pose, not a captured PlayStation
camera frame. The editor derives its third-person start with the retail spherical
preset `D_8006C934` (radius `0xA00`, elevation `0xA0`) and may shorten the
behind-distance when crossing a decoded collision-group boundary. If the
complete sidecar is unavailable, the editor may read the source image directly;
without either source it uses a median-object fallback that is not a retail
entry-camera claim.

Normal Game Camera navigation now combines that group with decoded HP/LP sector
flags and distance gates. Distant or disabled HP is suppressed while source LP
records provide their packed coordinates, transition bias, and four-corner
Gouraud colors; LP is not drawn as a duplicate whole-level shell over retained
HP. Selected, hovered, brush-targeted, or edited sectors may be force-shown in HP
so the user can work on them. Visible HP uses its native 32x32 4-bpp LQ base,
descriptor and distance-palette row, camera-dependent DPCS colors, and the
normal/close HQ overlay only in the decoded near ranges. Native untextured
sentinels use their source four-corner Gouraud colors. Translucent
water/lava/ooze retain the guarded editor material rather than being presented as
falsely opaque native art.

During W/A/S/D, wheel, right-drag, arrow-key, and focused-object navigation, a
lower-cost interpolation tier keeps native material instead of substituting flat
average colors. Full interpolation returns after input settles, and selected or
edited terrain stays detailed. This optimization does not alter the captured
source scene.

Edit Map deliberately uses the native normal-HQ tile and physical HP color table
2, stored in the legacy `NearColors` field, as a stable camera-less overview
endpoint. It does not apply one camera depth to the whole level. Every captured
face remains material-rendered and selectable, so mutually exclusive source
groups can appear together even though this is not one gameplay frame.

The source-proven Game Camera path reconstructs the native LP and HP base-bucket
equations, the HP renderer-flag mask, and the coarse terrain phases: LP, HP LQ
bases, normal HQ, then close HQ. When every required sidecar is source-proven,
its bounded RGB5 framebuffer also applies source zero/STP classification,
textured-Gouraud modulation, the native 4x4 dither/quantize stage, and ABR 0-3
arithmetic. That pixel math is exact only for the command stream and projected
inputs supplied by the editor.

This is not a 1:1 or pixel-exact PlayStation frame. The shipping renderer still
uses the editor's floating-point camera and viewport. Exact production GTE
projection, near-plane clipping, perspective correction, recursive HQ
subdivision and seam packets, HQ tile-local depth buckets, full ordering-table
traversal, and terrain/non-terrain interleaving remain research work. The native
fixed-point GTE experiment is a focused test path, not the normal editor camera.
The viewport is not a fixed retail 512x224 raster, and it does not yet reproduce
the game's full per-frame frustum/visibility pipeline.

When a user selects a BIN/CUE, the local cache preserves native near/fade corner
colors, raw corner order, normal/close HQ tiers, indexed LQ descriptors and
palettes, LP records, sector flags, and environment groups. It reproduces the
retail texture load/default initialization before decoding the initial preview,
and source-proven HQ/LQ/raw sidecars must agree before the bounded compositor is
used. Missing or inconsistent proof fails closed to a whole-frame guarded
fallback rather than mixing proven and unproven raw materials.

The initial texture state is not live gameplay playback. Texture animation and
scroll programs, pause/reverse/frame selection, and later runtime-controlled
frames are not animated in the viewport. The native sky/background is also not
rendered behind Game Camera yet. Custom PNG imports, native texture relocations,
or other edits without a complete final raw-material proof stay on the guarded
fallback until their final descriptor controls and pixels can be reconstructed.

Labels such as grass, stone, sand, or ice can still be visual/editor labels.
Water, lava, ooze, and other behavior are transferred only when the donor face
has an exact native collision-property signature and every target face's unique
collision triangles pass the batch proof. A mere color/preview change never
claims gameplay behavior.

Before promoting terrain behavior, testers need before/after in-game evidence such as:

- Spyro health/state change.
- Knockback or death behavior.
- Solid-ground control capture.
- Repeated proof on another face of the same surface type.

Terrain/building art uses indexed PlayStation texture pages. `Choose Texture &
Start Painting` initially loads only the active level into a six-column gallery;
another level is decoded only after the user explicitly loads it. Unsupported
records remain visible as gray `BLOCKED` cards with their exact reason. The
fast paint path is face-local. Same-level painting changes only the clicked
face; cross-level painting does so only when that destination owns a unique safe
texture record. A shared destination is refused without mutation.

The existing shared cross-level replacement and custom color/import/paste path
remains available through the separate `Advanced / Shared Replacement`
terrain-panel button. It is
intentionally explicit because a supported Apply changes the shared target
texture record for every face using that texture ID—not just the clicked
triangle—and uses either complete target-owned in-place storage or protected
byte-private relocation. `Create BIN` repeats the ownership, runtime-control,
indexed-pixel, palette, low-detail-alias, property-layout, and exact-readback
proofs before promoting the output.

There is one explicit exception to the face/property-donor requirement: the
source audit finds 19 `nativeUnreferencedStatic` records (17 in Beast Makers,
Icy Flight texture 11, and Peace Keepers texture 11). They can be selected as
art-only record donors. This mode preserves the target face texture ID, raw HP
material/semitransparency bits, descriptor ABR/alpha material controls, native
near/fade tint, editor material label, and collision/surface behavior; it never
fabricates a donor face runtime key or writes a terrain-property edit.
Save/reload and Undo retain that distinction.
The 22 face-less animation-source diagnostics and 4 controlled face-less
destinations are not part of this exception and remain blocked.

Same-level selected-face swaps use a different guarded path: the texture ID is
changed while the target face's native mapping/control bits remain intact, and
the donor face's exact four near/fade corner tint pairs are rebound only through
target-sector color slots that no neighboring face uses. A choice stays blocked
when its donor topology differs, its source bytes cannot be re-read, or the
target sector lacks enough private slots. The Artisans ground-to-damaging-water
candidate is the retained live proof for this path; it uses the horizontal pool
visual rather than the scrolling waterfall record.

Some visible rows remain intentionally blocked: face-less records that are
animation sources or runtime-controlled destinations, mixed or unresolved
property variants, and target texture records controlled by native animation
or scrolling tables. These rows are still useful for identifying all decoded
disc textures, but the editor will not invent behavior or partially write them.
Arbitrary custom PNG manifests are
also blocked because the old fixed-layout normal/close-detail writer does not
match the verified packed layout.

Structural coverage is not the same as live gameplay proof for every possible
target/donor combination. The retained Artisans dragon-to-Gnasty-metal and
ground-to-damaging-water candidates are focused runtime checks; unexpected
visuals, damage behavior, portal travel, or nearby terrain changes should still
be reported with the generated preflight/patch-plan files.

## Sky Editing

Spyro 1 skies are vertex-colored geometry rather than ordinary panoramic image
textures. Importing colors from a PNG samples a luminance-ordered gradient and
applies it to the level's existing geometry; it does not wrap the PNG around the
level.

Same-disc sky swaps that fit the destination remain fixed-size in-place edits;
smaller donors are padded without moving later game data. Larger donors use a
guarded expanded-WAD path on the supported original USA disc layout. That path
grows sector-aligned level archives and `WAD.WAD`, moves the executable into the
verified free ISO region before `PETEXA0.STR`, and updates the ISO directory
records. A batch is rejected if its growth would exceed that region. All 1,190
same-disc target/donor pairs pass structural write/readback, including linked
homeworld portal copies.

Structural archive validity does not prove the game's runtime memory capacity.
Normal Save Skybox and Create BIN therefore block any replacement that would
grow a linked homeworld portal copy. The error names the edited level, hidden
storage homeworld/block, donor size, native capacity, and required growth before
any BIN/CUE or plan is written. This specifically blocks the reproduced Cliff
Town <- Twilight Harbor route (`43,496 > 29,576`, `+13,920` bytes in Peace
Keepers block 2), which reached the PlayStation BIOS fatal exception handler
while flying into Peace Keepers. Expanded linked-copy relocation remains
available only to explicit offline research smokes until individual routes have
live runtime proof.

Each level also has a separate sky-occlusion list. Reusing the target list with
a donor sky can hide valid donor parts and produce large diagonal or rectangular
gaps. Geometry swaps and custom imports therefore patch the supported USA
executable to send every donor part through the renderer's normal frustum
culling. This has a small potential performance cost compared with the original
hand-authored lists. Recolor keeps the native occlusion path. Donor geometry and
solid background colors are still authored around their original terrain.
`Match Level Terrain Palette` offsets that mismatch by transforming the destination's
own validated low-detail and high-detail scene colors, plus only texture
palettes referenced by its landscape scene. It does not copy donor terrain or
texture indexes. Large hue or saturation shifts automatically use one affine,
luminance-preserving donor-color harmonization for every near/far sector and
carry that balance into the referenced normal/distance landscape palettes.
This reduces hard color boundaries without flattening native shading. Player,
HUD, and gem palettes remain separate. The optional
`Match scenery, chests, and creatures` control changes the game's valid neutral
material-0 lighting value per level. It does not rewrite any object-row material
byte and never uses the reserved material slot that produced flashing or solid
red polygons in earlier experiments. Native material-1 and material-2 objects,
including gems and special effects, keep their original lighting. Every level
without an object-lighting match explicitly restores the original neutral value,
so a dark grade does not carry through a portal. This is intentionally one
shared object-lighting control; separate actor, chest, scenery, and dragon
scopes are not yet safe because those categories share the native material.
The object-lighting helper also shares an executable hook/cave with the hidden
Spring Chest research helper, so the exporter rejects that unsupported
combination instead of overwriting either patch. The advanced strength,
brightness, saturation, tint, and scene/palette scope controls are optional.
High-detail terrain lighting stores two rendered RGB lanes per native entry;
the automatic match transforms both lanes together and preserves each command
byte so close-up Gouraud interpolation remains internally consistent.
The three original sky presets use palette art created for the editor, but they
derive their polygon geometry from a compatible level in the user's selected
disc at export time. Their preview images are mood references rather than exact
pixel-for-pixel renders of every target level.
Imports are limited to 1 MB and sky edits update the playable level and its
byte-linked homeworld portal copy together.

Extreme environment matches remain beta. Dark Hollow matched to Doctor Shemp
now preserves Spyro's own palettes and no longer corrupts nearby texture art,
and its GPU-proven close-tree palettes and far/untextured tree color tables are
graded together. This removes the green-at-distance and gray-up-close tree swap
without touching Spyro's player palettes. Other drastic donor/target pairs can
still expose an untraced level-specific scenery LOD; the exporter keeps every
palette and scenery-color write fixed-size and non-overlapping.

## Moby Names

The current 35-cache offline audit covers 4,934 real Moby rows and leaves zero
rows with an unresolved placeholder, question mark, `unknown`, or candidate
label. Three earlier rows were excluded because corrected source-table
boundaries proved they were misaligned header/data records rather than Mobys.

The same audit now reports zero generic identities: all 4,934 rows have a
specific object or behavior-role name. Of those, 3,730 are evidence-backed
precise identities and 1,204 are inferred precise identities. The final broad
families were resolved from native class handlers, source-scene links, decoded
models/animations, exact level placement, and existing live observations.

Zero generic or unresolved labels does **not** mean every row has independently
runtime-proven behavior. `Inferred precise` means the name is supported by the
current source-family, roster, model, or positional evidence; it is not a claim
that every instance received new DuckStation proof. Likewise, a precise label
does not make cross-level import, spawning, reward linkage, or behavior safe.
Control, route, reward, and boss-linked rows remain protected from ordinary Add
Object and copy/paste workflows where appropriate.

The native decomp also corrected the editor's technical field terminology.
Bytes `+0x36/+0x37` are the little-endian native Moby class; `+0x50` is render
radius, `+0x51` is the was-drawn byte, `+0x52` is update distance, `+0x53` is
the dropped Moby/class, and `+0x4F` is specular/metal type (also used in the gem
encoding). The UI now shows those meanings and classifies new proven families
by the full native class. Existing cache and saved-edit JSON retains its legacy
property names for compatibility.

Future naming changes should improve the evidence, not merely replace one
specific label with another. Revise an identity only when source structure, a
matching proven family, or clearly observed model/behavior evidence supports
it. If later evidence conflicts with a current inferred name, report the row
and evidence rather than substituting another guess.

## Generated Output

Patched BIN/CUE files are disposable local test output. Keep the original clean image safe and do not distribute patched game images.

For beta testing, report whether a generated CUE:

- Boots.
- Loads the intended level.
- Shows the intended object or terrain change.
- Preserves normal game behavior.
- Crashes, hangs, soft-locks, or changes unrelated objects.

## First Beta Success Criteria

This beta is successful if testers can:

- Launch the editor on Mac or Windows.
- Build or use a local editor cache.
- Browse levels.
- Move known objects and create disposable test BIN/CUE output.
- Use Game Camera and Edit Map comfortably.
- Report incorrect or still-too-generic Moby identities through GitHub Issues
  without attaching game data.
- Clearly understand which features are safe, experimental, or not proven yet.
