# Spyro Editor 1.2

## Changes since Spyro Editor 1.1

New

- Tile Type…: in the Terrain tab's Tiles card, choose what Spyro finds on the selected tiles — solid ground, water or lava that hurts, a supercharge ramp, and whatever else the level already defines. The picture stays; the ground behaves differently. The Tiles card names the kind of the selected tile. Undo Terrain puts it back.
- Fly Anywhere can be limited to chosen levels: open "Choose levels" under the whole-game switch and tick the levels you want. Everywhere else Spyro moves as in the original game. A level of your own flies whenever at least one level is ticked.
- A level you build, or clear with Start Blank, can use another level's skybox: in its level settings, pick a level under "Skybox (borrow a level's sky)" and press Use this skybox. Flight levels' skies are not offered.
- Lower now works on tiles at the floor of their part of the level. The game stores each part's heights within a band that used to start exactly at the lowest tile; Create BIN now moves the band when a lowered corner needs it.

Clearer

- Terrain edits are refused at the moment you make them, in plain words, instead of failing at Create BIN: tiles without collision (scenery), steps over 255 inside one piece of collision, folded ground, a surface within 64 above, crossing into another 256-unit collision cell, and the height and width bands of a part of the level. The message says what to do instead.
- Raise, Lower and drags move only the corners the game has collision for, and move corners shared with neighbouring tiles along with them, which is what the game does anyway. The status line says how many corners followed or stayed.
- A brush stroke leaves the corners it cannot move where they are and says how many; whole-tile strokes keep shared corners joined across tiles the brush did not touch.
- Find space for this object… shows which version and option it is trying, lists each way to make room as soon as it is found, and has a Stop button. The search is bounded (6 versions, 48 options each). When no original objects would make room it says so instead of searching on.
- The Create BIN question and result name the levels Fly Anywhere is on in.

Fixed

- Giving a level another level's look (the environment recolour) left far hills, and grass seen from afar, in their old green. Grass and distant scenery now take the colour of the level the look comes from, unless that level is green itself.
- Create BIN refused enemies from Dark Passage, Haunted Towers, Terrace Village and Twilight Harbor ("… lost native … owner") in a game that also had recoloured dragons or removed objects.
- Copied terrain (Add Terrain) now appears in the game. The copies were saved, but the game never drew them.
- Decorative tiles that Spyro never touches, such as the trim around a doorway, can now be removed and copied too.
- Create BIN could fail with "LP color … command byte" when one level's added or removed tiles made its data grow while another level in the same game had terrain, colour or sky changes.
- Find space for this object… could appear stuck for minutes on a static message with no way out.
- Create BIN's "visual-only geometry edit" warning appeared for tiles whose moved corners all have collision; the warning now looks at the corners that moved.
- Tiles cut along the other diagonal than their collision triangles can now take a texture's kind (and Tile Type…).
