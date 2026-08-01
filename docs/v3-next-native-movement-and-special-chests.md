# V3-next Native Movement and Special-Chest Status

This document describes development work after the published Spyro Editor Beta
V3 release. It is not a Stable V1 claim and does not promote an untested chest
profile into normal `Add Object` or `Create BIN`.

## Native movement editing

### Egg thieves

- The clean USA disc resolves exactly 12 owned egg-thief `PathData` blocks with
  121 fixed ordered nodes.
- `Edit Run Path` is available only for the verified native owner.
- The viewport shows numbered handles, every ordered segment, and the distinct
  handler-traversed closing seam in Edit Map and Game Camera. The seam may be
  taken forward or in reverse depending on the level handler.
- Nodes support drag, exact XYZ entry, terrain snap (on by default), individual
  reset, and whole-path reset.
- `Move thief and path together` defaults on. Turning it off shows the in-game
  snap-back warning.
- The versioned path document records the level, owner T index, native class,
  node count, native pointers, original-path SHA-256, original XYZ, edited XYZ,
  and preserved fourth word.
- Export writes exactly the three 32-bit XYZ words for each edited node. It does
  not rewrite the eight-byte header, node count/order, traversal state,
  pointers, or unknown fourth word.
- A stale owner, class, pointer, node count, node preimage, or path fingerprint
  is rejected and targeted in Build Safety.
- Runtime safety also checks every sequential and closing-edge vector, the
  owner-to-current-node vector, the PS1 GTE signed-component and magnitude
  limits, and each edited node's terrain clearance relative to its retail
  preimage. Normal `Create BIN` fails closed for an unproven route fingerprint;
  a disposable research build cannot override a geometric blocker.

### Dragon rescue approach

- The clean USA disc resolves all 79 native rescue scenes across 28 levels.
- Selecting a native dragon shows the dashed `Spyro runs here` guide and
  draggable endpoint.
- X/Y are editable. Z is derived from source terrain, read-only, and never
  exported.
- Moving the dragon carries its endpoint. Moving the endpoint does not move the
  dragon, pedestal, approach camera, cinematic track, or timing data.
- The synthetic control uses stable identity `dragon-run-to:T#` in the existing
  native object manifest and preserves its original raw endpoint and packed
  preimage.
- Export normalizes the 12-bit angle and writes only packed fields `+0x28` and
  `+0x2C` relative to the dragon's final position. `+0x30` and all camera
  choreography remain unchanged.
- Copied/new controls, stale scenes, invalid numbers, zero radii, and unsafe
  overflow are blocked. A radius outside the audited retail range or a missing
  source-terrain hit is a targeted Build Safety review.

## Special-chest evidence registry

The checked clean-USA registry contains six families for each of the 30
non-flight levels: 180 exact family/level/disc keys. Sunny Flight, Night Flight,
Crystal Flight, Wild Flight, and Icy Flight have no profiles and remain visibly
blocked.

| Family | Native-family levels | Missing-family destinations | Normal-build imports |
| --- | ---: | ---: | ---: |
| Key + Locked Chest | 13 | 17 | Artisans V2 only |
| Life Chest | 22 | 8 | None yet |
| Spring Chest | 24 | 6 | None yet |
| Firework Chest | 13 | 17 | None yet |
| 3x Flame / Multi-hit Chest | 13 | 17 | None yet |
| Armored / Strong Chest | 11 | 19 | None yet |

The Artisans Key + Locked Chest V2 profile is the sole runtime-proven import.
Its fixed pair and hidden reward rows remain one atomic bundle, limited to one
pair and incompatible with another structural relocation or unrelated Artisans
object edits.

The other 16 missing Locked Chest destinations have static target-profile
records for disposable research only. Native-family rows may also be inspected
as research candidates, but native presence alone does not prove an independent
extra instance. All other missing-family destinations remain blocked until the
handler, package, child/controller, private allocation, texture/CLUT, reward,
cleanup, and persistence closure is mapped.

Atomic bundle metadata is saved with every imported member. Move, remove,
restore, undo, save, and load carry the complete group. Build Safety blocks
partial groups, orphaned key/chest halves, incoherent hidden references,
duplicate bundle identities, capacity overflow, stale evidence/profile data,
unsupported disc fingerprints, and structural conflicts. Findings target the
visible root so double-click navigation can center it.

## Promotion boundary

Normal release-mode Add and Create BIN expose only a destination profile with
recorded runtime proof. Development candidates remain separate disposable
tests. A profile is not promoted by a clean compiler result, static readback,
successful boot, or native-family presence.

Each future promotion still requires the plan's DuckStation matrix: cold boot,
fast entry, approach, model/texture/animation/sound, exact one-time reward and
global total, death/reload, leave/re-enter persistence, cleanup, nearby actors,
unchanged donors, family-specific interaction rules, supported multi-copy
capacity, mixed-family/structural edits, and final-BIN readback.

Stable V1 remains blocked until every requested non-flight destination has that
recorded runtime evidence.
