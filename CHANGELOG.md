# Spyro Editor 1.2

## Changes since Spyro Editor 1.1

New

- Tile Type…: in the Terrain tab's Tiles card, choose what Spyro finds on the selected tiles — solid ground, water or lava that hurts, a supercharge ramp, and whatever else the level already defines. The picture stays; the ground behaves differently. The Tiles card names the kind of the selected tile. Undo Terrain puts it back.
- Fly Anywhere can be limited to chosen levels: open "Choose levels" under the whole-game switch and tick the levels you want. Everywhere else Spyro moves as in the original game. A level of your own flies whenever at least one level is ticked.
- A level you build, or clear with Start Blank, can use another level's skybox: in its level settings, pick a level under "Skybox (borrow a level's sky)" and press Use this skybox. Flight levels' skies are not offered.
- Snow on trees and plants: on the Environment tab, a level whose look is matched to another level can have its trees and plants turn snow white where the light hits them and frosty blue in shade, close up and far away. Trunks keep their colour.
- Crash Bandicoot boxes (needs your Crash Bandicoot: Warped (USA) disc; choose it once with "Choose Crash disc…" in the Add Object browser, it is only read). In any ground level:
  - Crash crate: breaks when Spyro flames it, charges it or jumps on it (he bounces off, Crash style). Choose what is inside: a red, green, blue, gold or purple gem (counted in the level's treasure) or a butterfly.
  - TNT crate: jump on it and it counts down 3, 2, 1 with Crash's beeps, then explodes. Flaming or charging it sets it off at once. The blast costs Spyro one Sparx level if he is close, and breaks crates, baskets, TNT and Nitro nearby.
  - Nitro crate: hops and rattles, and explodes when anything touches it, taking all of Spyro's health. Flame it from a distance or set it off with TNT.
  - The boxes use Crash 3's own box art and sounds from your disc.
- Crash Bandicoot enemies (same disc, under Enemies › Crash Bandicoot), with their own Crash 3 models, textures, animations and sounds:
  - Crash frog (Toad Village): sits croaking, then hops towards Spyro when it sees him. Touching it costs a Sparx level. Leaves a butterfly.
  - Crash knight (Toad Village): turns to face Spyro, marches up to him and swings his sword (a hit costs a Sparx level), and walks back to his spot when Spyro gets away. Drops a green gem (2).
  - Crash wizard (Gee Wiz lab assistant): turns to face Spyro and casts a zap when Spyro is close and in front of him. Drops a blue gem (5).
  - Flame, charge or jump on any of them and they are knocked flying, Crash style (jumping on them bounces Spyro off). The wizard's robe blows off first. The gems count toward the level's treasure.
  - Frogs and knights stay within about 100 units of where you put them; place them on level ground (they keep their height).
- Terrain textures from your own pictures: images added in the Terrain tab are now written into the game in every detail level (far, normal and close up), with the fade into the distance kept. Textures that share pixels in the level take new images together; the editor says which.
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
- Rescuing a dragon could crash the game in a level where whole object families (such as Stone Hill's rams and shepherds) were removed and objects from other levels were added. The rescue scene ran out of room below the dragon statue. Create BIN now measures the whole rescue scene, places imported models where the game reloads them after a rescue, and refuses a level that still lacks room.
- Recoloured dragons turned back to green crystal in their rescue scene. When every statue in a level has the same colour, the scene now shows it too.
- Applying a sky in a mode other than Swap From Level switched off the level's matched look (terrain lighting). It is kept now.
- Copied terrain (Add Terrain) now appears in the game. The copies were saved, but the game never drew them.
- Decorative tiles that Spyro never touches, such as the trim around a doorway, can now be removed and copied too.
- Create BIN could fail with "LP color … command byte" when one level's added or removed tiles made its data grow while another level in the same game had terrain, colour or sky changes.
- Find space for this object… could appear stuck for minutes on a static message with no way out.
- Create BIN's "visual-only geometry edit" warning appeared for tiles whose moved corners all have collision; the warning now looks at the corners that moved.
- Tiles cut along the other diagonal than their collision triangles can now take a texture's kind (and Tile Type…).
