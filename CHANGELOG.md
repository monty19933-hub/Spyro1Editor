# Spyro Editor 1.4

## Changes since Spyro Editor 1.3

New

- File ▾ → New World…: a world of your own in the game, with a homeworld and up to four levels. Its homeworld has a doorway to each of its levels (the Terrain tab's Doorways card), and the World Entrance card puts the way in into one of the game's own homeworlds, with a doorway back out of your world. Create BIN builds it all into the game: Spyro walks in from that homeworld, Return Home and Exit Level bring him back to your world's homeworld, the inventory has a page with your world's name, and New Game can start in it. Your world's progress is saved in a new save file, so saves from earlier versions are left alone.
- Terrain tab › Doorways: doorways in a homeworld you rebuild with Start Blank, and in your world's homeworld. Six shapes (Stone Arch, Pointed Arch, Square Gate, Round Ring, Wide Arch, Hex Gate) from 75% to 150% size. The frame is ordinary tiles you can repaint, and it moves, turns and changes shape with its doorway. In the game the opening shows the sky of the level it leads to.
- Import a sky from a 360° panorama picture (PNG, JPEG, WebP or BMP) or a sky model (OBJ or FBX): the editor turns it into a Spyro sky and shows what the game will draw, with Detail, Turn and Flip left and right. It works in the game's levels and in levels you build.
- The 3D view (Game Camera) shows each level's real sky behind it.
- File ▾ → Random Level…: a whole level from a seed and a few choices (a valley, islands, sky islands, caverns, a canyon trail or a castle; nine looks), with caves, supercharge runways, glides, water or lava, bridges and secret ledges, ready for your objects.
- New Level and Start Blank begin as a meadow that looks like a Spyro level: grass, a stone path, a ramp up to a ledge and a pond with a sand rim, under Artisans' sky.
- Terrain tab › Ground: Grass, Dirt, Stone, Sand, Snow, Wood, Water, Lava and Goo, each with its look and what Spyro finds on it. Tile Type… works in levels you build again, and in the game's own levels it now offers every ground kind, supercharge included; a kind a level lacks is brought in from another level.
- Choose Texture… shows a level's textures as a grid of pictures.
- Terrain tab › Tiles › Add beside ◀ ▲ ▼ ▶ adds an exact copy of the selected tile against one of its edges; at the map's edge it offers to move the level and make room.
- Teal and rainbow gems tumble and shine like the game's own gems.
- Game Settings › Memory › Emulator only (8 MB RAM): levels may use more memory when the game runs in an emulator set to 8 MB.

Fixed

- Textured ground and walls in levels you build no longer change colour as Spyro moves: up close they were drawn with the picture's colour applied twice (darker and more saturated, or washed out for bright pictures such as clouds). Since Spyro Editor 1.2; export such levels again to get the fix.
- A dragon from another level in a level made with New Level crashed the game when Spyro freed it: its rescue scene belongs to its own level. Add object… and Create BIN now say so; a level rebuilt with Start Blank keeps its own dragons.
- Supercharge ground made with Tile Type… crashed the game in the game's own levels.
- An imported sky in Stone Hill was drawn only in part.
- Large levels you build no longer freeze the editor for a while each time a tile is selected.

Removed

- File ▾ → New Level from Crash Bandicoot: Warped… is gone from the menu. Crash's crates, enemies and songs stay.
