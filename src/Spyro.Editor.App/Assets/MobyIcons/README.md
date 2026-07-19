# Gem icon provenance

The red 256 x 256 RGBA PNG is a byte-for-byte copy of the red gem icon used by
the user's Spyro 2 Avalonia editor.

That icon was generated for the user's Spyro 2 editor task on 2026-07-12. On
2026-07-14, the green, blue, yellow, and purple files were replaced with
color-only variants of the red artwork at the user's request. All five now use
the red icon's exact canvas, silhouette, facet boundaries, placement, and alpha
mask. They are generated project artwork, not extracted from a Spyro game or
copied from a third-party icon library.

No standalone third-party license or attribution file accompanied the source
set. They are reused here as user-supplied/generated artwork within the user's
Spyro editor family. This provenance note records the local source and reuse;
it is not a separate license grant.

| Gem | Value | File | SHA-256 |
|---|---:|---|---|
| Red | 1 | `gem-red.png` | `34d6f350ecc0a6adf8892a47b0864ebc40eba7bb598786e18b592abeabbb629b` |
| Green | 2 | `gem-green.png` | `0d23f02fda93626fd48791955d06e9b7c5c942f560805251c6cf98a0fd5ea867` |
| Blue | 5 | `gem-blue.png` | `6ce0f7fe9eed673e6b1fc886410e8100e05958b38beb48160ff6d452e0ce74dc` |
| Yellow | 10 | `gem-yellow.png` | `6ae19b7aed5e6028dcb1e64f6445edf55e9a6b1641bc4331866b5904802c74c3` |
| Purple | 25 | `gem-purple.png` | `49ceadac27cd587c673c3a6e4d5ac23050844c5356f4c4abb3a78ae1d2ec6b86` |

## Optional Moby family icons

The viewport also has stable identity-to-file slots for the following generated
project artwork. These files use the same 256 x 256 transparent RGBA contract as
the gems. They are optional by design: if a file is absent or unreadable, the
editor retains its existing procedural marker instead of failing to load a level.

### Installed now: family atlas 1

The same fourteen slots are currently supplied by the generated project artwork
`moby-icon-atlas.png` (SHA-256
`57f81567e89447d193c08c4ccd5cf15b75c2ea996691e98935272e15d9fd7f31`).
It was created for this editor on 2026-07-16 as original low-poly artwork informed
by retro Spyro references; it is not an extraction from the game or a third-party
icon sheet. Its catalog contract key is `moby-family-atlas-1`: 4 columns, 4 rows,
14 used cells, and 2 explicitly empty trailing cells (14 and 15).

The source file is truthfully **1254 x 1254 opaque RGB**, arranged as a 4 x 4
grid, with a light checkerboard baked into the image. Its two dominant shades
have small RGB variations across the rendered canvas. Cells 14 and 15
are intentionally empty. Because 1254 is not evenly divisible by four, the
viewport uses exact integer cell boundaries instead of pretending the cells are
256 x 256 or allowing adjacent cells to overlap. At load time the editor learns
the dominant shades and their measured light-neutral color envelope from the
cells that this atlas contract marks empty, then clears only matching pixels
connected to the atlas perimeter. Low-variance sheets such as atlas 1 retain this
legacy path byte-for-byte; adaptive sheets may additionally use individual cell
edges and the checker period learned from their empty cells. The loader uses this
atlas's explicit `UsedCellCount`; it does not infer the empty-cell boundary from
the global number of family definitions. This produces a cached transparent
in-memory bitmap while retaining identical light pixels enclosed inside silver
and white subjects. The checked-in source PNG remains unchanged.

Each family definition records both an atlas contract and a cell. Viewport cache
and draw diagnostics use the combined atlas key plus cell number, so future
atlases can safely reuse cell numbers 0 through 13. Optional individual
transparent PNGs still take precedence over their atlas fallback when present.

| Family | Preferred individual file | Installed atlas fallback |
|---|---|---|
| Shielded Greenie | `shielded-greenie.png` | `moby-family-atlas-1:0` |
| Tin Soldier | `tin-soldier.png` | `moby-family-atlas-1:1` |
| Barrel supply hatch | `barrel-supply-hatch.png` | `moby-family-atlas-1:2` |
| Power pole | `power-pole.png` | `moby-family-atlas-1:3` |
| Volt Shooter | `volt-shooter.png` | `moby-family-atlas-1:4` |
| Flight lighthouse | `flight-lighthouse.png` | `moby-family-atlas-1:5` |
| Caged Fairy | `caged-fairy.png` | `moby-family-atlas-1:6` |
| Fairy cage | `fairy-cage.png` | `moby-family-atlas-1:7` |
| Clock Fool | `clock-fool.png` | `moby-family-atlas-1:8` |
| Metal door | `metal-door.png` | `moby-family-atlas-1:9` |
| Fat Bat | `fat-bat.png` | `moby-family-atlas-1:10` |
| Steel barrel | `steel-barrel.png` | `moby-family-atlas-1:11` |
| Train | `train.png` | `moby-family-atlas-1:12` |
| TNT train car / cluster | `tnt-train-car.png` | `moby-family-atlas-1:13` |

Atlas cell order, left-to-right and top-to-bottom:

1. Shielded Greenie
2. Tin Soldier
3. Barrel supply hatch
4. Power pole
5. Volt Shooter
6. Flight lighthouse
7. Caged Fairy
8. Fairy cage
9. Clock Fool
10. Metal door
11. Fat Bat
12. Steel barrel
13. Train
14. TNT train car / cluster

### Installed now: family atlas 2

The second fourteen-slot sheet is installed as `moby-icon-atlas-2.png`
(SHA-256
`80387fe566d1366b6756299c88fd8dc6b4ba3113586cbae848c2d109fc9480d8`).
It is a byte-for-byte copy of the original image generated for this editor on
2026-07-16.

The generated source is original project artwork informed by retro Spyro
references; it is not an extraction from the game or a third-party icon sheet.
Its catalog contract key is `moby-family-atlas-2`: 4 columns, 4 rows, 14 used
cells, and 2 explicitly empty trailing cells (14 and 15).

Like atlas 1, the source is truthfully **1254 x 1254 opaque RGB** with a baked-in
checkerboard. The same runtime loader applies exact integer cell boundaries and
uses the two contract-declared empty cells to learn and remove only
edge-connected background regions in memory. The per-cell pass also handles the
slightly darker neutral checker variations present around atlas 2's occupied
cells. For negative spaces completely enclosed by a subject, the loader
independently redetects the empty cells' 60-tile checker rhythm and clears only
substantial components that reuse the exact empty-cell palette and reproduce its
alternating luminance. Non-periodic white armor, petals, horns, wizard detail,
and fairy glow remain intact. Cells 14 and 15 are empty after
masking, all fourteen used cells retain visible subjects, and the checked-in PNG
is never rewritten. Optional individual transparent PNGs retain precedence over
this atlas fallback exactly as they do for atlas 1.

| Family | Preferred individual file | Installed atlas fallback |
|---|---|---|
| Fat Claw Monster | `fat-claw-monster.png` | `moby-family-atlas-2:0` |
| Barrel Engineer | `barrel-engineer.png` | `moby-family-atlas-2:1` |
| Beast | `beast.png` | `moby-family-atlas-2:2` |
| Dockworker / TNT Wrangler | `dockworker-tnt-wrangler.png` | `moby-family-atlas-2:3` |
| Lantern post | `lantern-post.png` | `moby-family-atlas-2:4` |
| Bull | `bull.png` | `moby-family-atlas-2:5` |
| Ram | `ram.png` | `moby-family-atlas-2:6` |
| Floor Shocker | `floor-shocker.png` | `moby-family-atlas-2:7` |
| Metal Claw Monster | `metal-claw-monster.png` | `moby-family-atlas-2:8` |
| Shepherd / Shepard | `shepherd.png` | `moby-family-atlas-2:9` |
| Summoning Wizard | `summoning-wizard.png` | `moby-family-atlas-2:10` |
| Flight direction arrow sign | `flight-direction-arrow-sign.png` | `moby-family-atlas-2:11` |
| Superflame Fairy | `superflame-fairy.png` | `moby-family-atlas-2:12` |
| Armored Gnorc | `armored-gnorc.png` | `moby-family-atlas-2:13` |

Atlas cell order, left-to-right and top-to-bottom:

1. Fat Claw Monster
2. Barrel Engineer
3. Beast
4. Dockworker / TNT Wrangler
5. Lantern post
6. Bull
7. Ram
8. Floor Shocker
9. Metal Claw Monster
10. Shepherd / Shepard
11. Summoning Wizard
12. Flight direction arrow sign
13. Superflame Fairy
14. Armored Gnorc

Atlas 2 definitions are additionally restricted to Mobys that would otherwise
reach one of the viewport's generic marker fallbacks. This prevents broad name
matches from replacing linked logic/control records or balloon transport parts.
The Beast definition also explicitly excludes `Beast Makers banner`. These
guards apply only to atlas 2; atlas 1 matching order and precedence are unchanged.

### Installed now: family atlas 3

The final twelve-slot sheet is installed as `moby-icon-atlas-3.png` (SHA-256
`c6157c97434a9b3df6952e2e39752ececc297c7349ead75c240fef53ae7ef29d`).
It is a byte-for-byte copy of the original image generated for this editor on
2026-07-16.

The generated source is original project artwork informed by researched retro
Spyro visual references; it is not an extraction from the game or a third-party
icon sheet. Its catalog contract key is `moby-family-atlas-3`: 4 columns, 4
rows, 12 used cells, and 4 explicitly empty trailing cells (12 through 15).
The source is exactly **1254 x 1254 opaque RGB**, with a baked-in light
checkerboard and the same gap-free integer cell-boundary rule used by atlases 1
and 2.

| Family | Preferred individual file | Installed atlas fallback |
|---|---|---|
| Beast Makers banner | `beast-makers-banner.png` | `moby-family-atlas-3:0` |
| Crocodile | `crocodile.png` | `moby-family-atlas-3:1` |
| Ice stalactite | `ice-stalactite.png` | `moby-family-atlas-3:2` |
| Rescue Fairy | `rescue-fairy.png` | `moby-family-atlas-3:3` |
| Super Grenadier | `super-grenadier.png` | `moby-family-atlas-3:4` |
| Campfire | `campfire.png` | `moby-family-atlas-3:5` |
| Chicken cage | `chicken-cage.png` | `moby-family-atlas-3:6` |
| Drawbridge lever | `drawbridge-lever.png` | `moby-family-atlas-3:7` |
| Wall lantern | `wall-lantern.png` | `moby-family-atlas-3:8` |
| Arrow-sign Fairy | `arrow-sign-fairy.png` | `moby-family-atlas-3:9` |
| Balloon (`Ballon` alias included) | `balloon.png` | `moby-family-atlas-3:10` |
| Metalhead boss | `metalhead-boss.png` | `moby-family-atlas-3:11` |

Atlas cell order, left-to-right and top-to-bottom:

1. Beast Makers banner
2. Crocodile
3. Ice stalactite
4. Rescue Fairy
5. Super Grenadier
6. Campfire
7. Chicken cage
8. Drawbridge lever
9. Wall lantern
10. Arrow-sign Fairy
11. Balloon
12. Metalhead boss

Every atlas 3 definition is restricted to Mobys that would otherwise reach a
generic tree, Gnorc, or actor-triangle viewport marker. Phrase matching is token
bounded and retains the existing specific-before-broad ordering: the Beast
Makers banner is resolved before the atlas 2 Beast family, and `Ballon` and
`Balloon` both resolve without catching Balloonists, transport parts, or
Balloognorcs. The Super Grenadier gate similarly excludes its linked control
record. Optional individual transparent PNGs retain precedence over the atlas.

The focused UI gate checks the exact source hash and dimensions, all sixteen
gap-free integer cell bounds, the opaque-source contract, the empty cells'
two-color checker sample and 48-tile rhythm, all four empty cells becoming
fully transparent, twelve nonempty unique subject cells, atlas-qualified cache
identity, safe missing/invalid fallbacks, and real viewport draws in Beast
Makers, High Caves, Twilight Harbor, Dark Hollow, Misty Bog, Dream Weavers,
Artisans, Ice Cavern, and Metalhead. The measured source-locked transparency,
preserved-light, masked-image, and per-cell baselines are recorded directly in
the UI smoke so any background or subject-detail drift fails the release gate.
The installed decode measures exactly **1,157,885 / 1,572,516 transparent
pixels (73.6%)**, with **2,735 disconnected background-like light pixels
preserved** as subject detail. All twelve occupied cells retain between 21,674
and 53,380 opaque pixels, all four trailing cells retain zero, and the enclosed
checker regression finds zero visible alternating components across the twelve
used cells. The resulting masked in-memory capture has SHA-256
`05d78eb1331c8d77af86ec99b836b2700940de94e3f9d1a955026d8da18c4aea`.

The audited generic-marker baseline contains 380 Mobys. Atlas 1 resolves 242,
atlas 2 resolves 109, and atlas 3 resolves the final 29 (17 tree, 8 Gnorc, and
4 actor-triangle fallbacks), for exact non-overlapping coverage of **380 / 380**.

Matching uses the enriched visible label and cross-level family name, not native
type/radius bytes or level-specific true indexes. Specific identities are ordered
ahead of broad ones (for example, TNT train pieces resolve before ordinary train
pieces, and Fairy cage props resolve before Caged Fairy actors).
