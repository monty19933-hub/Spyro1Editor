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
- Start Blank works in every normal level, not only the ten it supported before. Dark Hollow, Toasty and the other levels whose start-up code is shorter get a copy of the cleared-level start-up in a spare place in the game; two of these can be cleared in one game (played in Dark Hollow).
- Levels you build, or clear with Start Blank, can be much bigger: up to 4,096 terrain tiles (5,000 drawn faces after the game's own splitting, 127 parts) instead of 255. Create BIN checks the game's close-up texture work for camera positions all over the level and, if one place is too crowded with textured tiles, names it.
- A built level's terrain tiles can show your own PNG pictures (one per material, written at every distance like the Terrain tab's pictures). For now the picture is set in the level's scene file; there is no picture chooser for built levels in the editor yet.
- Crash boxes and Crash enemies can be placed in levels you build or clear (Create BIN used to stop with "needs its original source disc").
- Tile Type… works in levels you build or clear with Start Blank: water, lava and ooze (Spyro cannot swim, so they cost a life), a supercharge ramp and an invisible wall. Before, every tile of a built level was solid ground.
- Example: WUMPA VILLAGE, a Crash Bandicoot: Warped style village built in Dark Hollow's place with Crash 3's textures, crates and enemies.
- Terrain textures from your own pictures: images added in the Terrain tab are now written into the game in every detail level (far, normal and close up), with the fade into the distance kept. Textures that share pixels in the level take new images together; the editor says which.
- Lower now works on tiles at the floor of their part of the level. The game stores each part's heights within a band that used to start exactly at the lowest tile; Create BIN now moves the band when a lowered corner needs it.
- Play as Crash (Game Settings → Play as Crash, chosen per level; needs your Crash Bandicoot: Warped disc): Crash replaces Spyro in that level. He runs, jumps, double-jumps (let go of X and press it again in the air), spins, slides, belly-flops, breaks crates and presses switches, and stands in Spyro's place when a dragon is rescued.
- Songs from other games for any level's music: Crash Bandicoot: Warped's Toad Village and Spyro: Year of the Dragon's Lost Fleet, taken from your own discs ("Year of the Dragon disc…" in the music panel).
- Year of the Dragon creatures (needs your Spyro: Year of the Dragon disc, under Enemies › Spyro: Year of the Dragon): Lost Fleet's rhynoc and crab, with their own models, animations and sounds. They guard their spot, attack when Spyro comes close and are knocked flying by flame, a charge or a jump.
- Nitro switch box: the green "!" box from Crash 3. Jump on it, flame it or charge it and every Nitro crate in the level explodes.
- Resize cannon from Dream Weavers works in other levels. Its gunner comes with it; to give it targets, select the cannon, open Add Object, pick an Armored Fool and tick "Let the Resize cannon shoot it". Defeat the gunner, stand behind the cannon and flame it: a hit makes a big fool small, and a small fool can be charged.
- A homeworld you empty with Start Blank keeps working doorways (moved to where you put them), its own dragons rescue properly, and the balloonist keeps his balloon.
- File ▾ → Free Deleted Objects' Save Rows…: a level keeps one of the game's 256 save rows for every object ever added, deleted ones included. When a level runs out, this gives the deleted objects' rows back (play that level from a new game afterwards).

Fixed

- Walls in levels you build stop Spyro. He used to walk through any wall from the side you can see (the game reads the solid side of a collision triangle from its corner order, and walls were stored the other way round from floors).
- Levels you build keep the game's 91 spare object slots (they had 16). The game puts every gem an enemy or crate drops, every butterfly and every projectile into those slots without checking the end, so with more than 16 at once it overwrote the level's data and could crash.
- Objects you add to one of the game's own levels no longer use up its spare object slots. Create BIN gives the level back its 91 (182 or 272 in the few levels that have more) as far as the level's game memory allows. Dark Hollow with 49 added objects used to keep only 52, so defeating many enemies and breaking many crates without collecting what they dropped could crash the game. If a level's memory is too full to give all of them back, Create BIN keeps as many as fit and stops with a message, naming the level, only when fewer would remain than the level's enemies and crates that drop something, plus 16.
- A large built level's terrain is cut into compact parts like the game's own, each with its true size, so the game can skip the parts out of view (they used to cover large areas and be drawn close up from far away). Small built levels keep their exact earlier layout.
- Enemies copied from another level stay where you put them. Many remember places in their home level (patrol paths, the circle Ice Cavern's snowballers skate, the area a Fool guards); copied, they headed back there, off the map and into walls. Those places now move with the enemy.
- Dragon statues you recoloured throw shards of their own colour when the dragon is freed, instead of green.
- Doorway flights into a level show that level's sky all the way, instead of switching halfway.
- Textures in levels you build no longer "swim" as you run: far away the game draws ground in plain colours, and faces whose far colours were never chosen used their shading values instead of their texture's colour.
- Faces drawn from both sides, or from their other side, keep that setting when the level is saved (it used to be lost on every save); a level built from another game can keep that game's distant meshes, so far-off terrain has no cracks.
- Crates, TNT and enemies keep their colours far away; jumping onto a crate or a TNT counts (a TNT counts down instead of exploding under Crash).
- Built levels with many small tiles are accepted (the game's close-up texture work is guarded while it runs).

Clearer

- Tile Type names water, lava and ooze by what they do: Spyro sinks and loses a life (or loses it at once). They were labelled "hurts Spyro", but in Spyro the Dragon they always cost a life.
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
