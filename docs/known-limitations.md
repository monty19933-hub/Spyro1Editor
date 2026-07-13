# Known Limitations For The First Tester Beta

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

Generally safer:

- Moving known gems.
- Moving known scenery.
- Moving known keys.
- Moving known chests that already exist in that level.
- Editing existing gem value bytes that are already proven by smoke tests.
- Replacing an existing object slot with a same-level donor object through
  `Edit Object` > `Change To`.

Still guarded:

- Adding or copy/pasting more same-level enemies or chests than the exporter can
  route through matching reusable source slots, except for the ordinary
  Flame/Charge Chest, same-level native Life Chest, and Large Gnorc families
  that have promoted true-add support.
- Copy/pasting arbitrary extra enemies, copied scenery, and copied chests as new
  source records when no same-level identity match is available.
- Cross-level object imports.
- Actor/package imports.
- Spring Chest normal exports.
- Gnasty's Loot Spring, Firework, and 3x Flame Chest normal exports.
- Key Chest in levels where no native proof exists.
- Wizard/enemy transforms.
- Anything that needs linked controller, reward, collision, or behavior records.
- Placeholder-looking mobys that may be triggers, camera starts, reward links,
  route markers, or helper/controller records. The editor shows these as
  system/control data and release builds do not offer them as normal Add Object
  donors or copy/paste objects yet.

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
source slots first for add, copy, and paste workflows. Ordinary Flame/Charge
Chests, same-level Life Chests, and Large Gnorcs can continue as true-adds after
those slots are consumed. Artisans live RAM proved that a Life Chest donor must
remain untouched and each appended copy needs a unique pre-relocated private
runtime block. The original T94 and appended T174/T175/T176 loaded together
with independent pointers and intact positions. The level-loader layout now
provides the equivalent scene-relative private allocation in all 22 levels with
native Life Chests, including the one-row source-count prefixes in Beast Makers
and Terrace Village. Cross-level Life Chest imports remain guarded.
Small/Regular Gnorc-style families remain guarded beyond reusable slots because
live testing showed they can spawn inactive and cannot be flamed or charged. Use
`Edit Object` > `Change To` when you want to choose the exact source slot that
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
live-behavior mismatch against the working donors. The remaining 3x Flame Chest clue is its colocated runtime/control row T108
(`type=0x1A`, `sourceByte36=0x88`), which must be allocated with the chest shell
before that family can be promoted.

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

## Terrain Editing

The editor can preview terrain surfaces and stage some terrain art/height edits, but terrain behavior is not fully decoded.

Current labels such as grass, stone, water, lava, sand, ice, or ooze are editor/material labels unless a behavior proof says otherwise. A color or texture change does not automatically mean the in-game collision or damage behavior changed.

Before promoting terrain behavior, testers need before/after in-game evidence such as:

- Spyro health/state change.
- Knockback or death behavior.
- Solid-ground control capture.
- Repeated proof on another face of the same surface type.

Terrain/building art uses indexed PlayStation texture pages. Imported PNGs are
resized into the destination's existing native descriptors and quantized to a
256-color PS1 palette, so exact source pixels may shift slightly. Face-local
painting consumes an unused local texture slot; if a level has no compatible
unused slot, the editor must refuse that face-local operation rather than
overwrite unrelated art. Shared texture import intentionally changes every face
using that texture ID.

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
but a small number of distant terrain/scenery LODs can retain native green or
gray-green coloring. Close-tree palettes deliberately remain native until the
separate far/untextured tree LOD can receive the same grade, avoiding a visible
tree color change as the camera approaches. This is a visual limitation only;
the exporter still keeps palette writes fixed-size and non-overlapping.

## Moby Names

The editor intentionally leaves uncertain objects as Needs ID. Question-mark labels are better than fake confidence.

Only promote a moby identity when the tester can clearly observe the model or behavior. Do not promote names like:

- object
- moby
- prop
- scenery
- actor
- enemy
- chest
- marker
- helper
- control
- unknown
- possible/candidate names

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
- Use Fly 3D and Map View comfortably.
- Report unresolved mobys through GitHub Issues without attaching game data.
- Clearly understand which features are safe, experimental, or not proven yet.
