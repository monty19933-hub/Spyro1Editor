# Object selector icon provenance and contract

These twelve PNG atlases are original project artwork generated for the Spyro
Editor object selector on 2026-07-23. Image-search results for the original
1998 PlayStation game were used only as visual references for recognizable
silhouettes and color cues. The final artwork was created with image generation;
it is not extracted game art, a screenshot crop, or a third-party icon sheet.

The selector-only catalog intentionally does not replace the viewport's existing
Moby raster catalog. This keeps map marker identity matching and its three
source-locked atlas smoke contracts unchanged.

Every source sheet is a 1254 x 1254 opaque RGB PNG arranged as an exact 4 x 4
grid. The baked light checkerboard is removed in memory by
`MobyIconAtlasLoader`; the checked-in files are never rewritten. Each 14-icon
sheet reserves cells 14 and 15 as empty background samples. The final eight-icon
sheet reserves cells 8 through 15.

`object-gallery-atlas-core.png` uses cells:

1. Gem / treasure
2. Key
3. Key + Locked Chest
4. Flame/charge chest
5. Charge chest
6. Life Chest
7. Spring Chest with a gem inside (no literal spring)
8. Firework Chest
9. Super Flame / armored strong chest
10. 3x Flame Chest
11. Crystal dragon statue
12. Whirlwind
13. Portal
14. Lamp/scenery

The enemy/scenery sheets follow the cell order declared next to their contracts
in `ObjectGalleryIconCatalog.cs`. Reward-color suffixes, the known `Sping Chest`
spelling, spring color aliases, tulip variants, and case-only label variants
resolve to one visual family. Dynamic `Copy selected` entries retain the selected
object's normal preview.

`object-gallery-atlas-landmarks-f.png` uses cells for Crystal Dragon, Whirlwind,
Portal, Balloon, Balloonist, Dragon Pedestal, Rescue Fairy, level-entry vortex,
wooden bridge, stone platform, flowering tree, and Beast Makers banner. Its
last four cells are empty.

`object-gallery-atlas-objects-g.png` uses cells for Armored Gnorc, Barrel
Engineer, Beast, Bull, Caged Fairy, Campfire, Chicken Cage, Clock Fool,
Crocodile, Dockworker TNT Wrangler, Drawbridge Lever, Fairy Cage Prop, Fat Bat,
and Fat Claw Monster. Its last two cells are empty.

`object-gallery-atlas-objects-h.png` uses cells for the flight-direction arrow,
Floor Shocker, Lantern Post, and Metal Claw Monster. Its last twelve cells are
empty.

`object-gallery-atlas-objects-i.png` uses its first two cells for Volt Shooter
and Wall Lantern. Its last fourteen cells are empty.

`object-gallery-atlas-objects-j.png` uses its first two cells for Ram and
Shepherd. Its last fourteen cells are empty.

`object-gallery-atlas-objects-k.png` uses its first two cells for Shielded
Greenie and Summoning Wizard. Its last fourteen cells are empty.

SHA-256 values at generation time:

- `object-gallery-atlas-core.png`:
  `d6486fd25b13e5dfa5e71ec6dc56e4e7a629ffc3f4058b66c799f5fda392b5e6`
- `object-gallery-atlas-enemies-a.png`:
  `0aab76e327336bec3c50398a5339e32a0902feb8f06c337ce489a27ae607ed57`
- `object-gallery-atlas-enemies-b.png`:
  `66c7ff3fe6fcbce2f20582abf0bea682b492aa781739f78c1e34856174a4bf04`
- `object-gallery-atlas-enemies-c.png`:
  `b808e9a0801ee113ed124c14d3bd910647743bd3adb0ea5b32453b59b84dda7e`
- `object-gallery-atlas-scenery-d.png`:
  `167ddd8307a6c3d7f3d831baa43d0610b1f63d6b8ad585e5276e49c645f3fe30`
- `object-gallery-atlas-scenery-e.png`:
  `d44397b3332a987240ce6810b5515e93a8d063703d1a3541a1a570f72f80ecff`
- `object-gallery-atlas-landmarks-f.png`:
  `b69982b69b95983ac9f0a9124924f96e15a8bcda9ab7ec28a3f153165d756cb2`
- `object-gallery-atlas-objects-g.png`:
  `9decf0e7bcbfb7c54b5caf2c371a6e4ebb7c53cef154c950da3cd53910ac658b`
- `object-gallery-atlas-objects-h.png`:
  `3a246b6770feb34370d2b33b3dc725c3e01edcbb807d1323ce60e269ad1be981`
- `object-gallery-atlas-objects-i.png`:
  `2a574d732e929524e4b0788ee82a312409bf146fa12adf4b4e9cf71bc4ab5368`
- `object-gallery-atlas-objects-j.png`:
  `e545e76d1d68615b6dc098726c06ae2bc1193195ff49255931038835c82af405`
- `object-gallery-atlas-objects-k.png`:
  `0ec1ddc212d085f2f12313af5551fb7ef4a62db8badad94e27c61c4f4086c641`

The focused gallery smoke decodes every sheet and verifies its used and empty
cells, so any future source replacement must update this note and the smoke
evidence together.
