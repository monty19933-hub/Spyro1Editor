# Unreleased

Fixed

- Textured ground and walls in levels you build no longer change colour as Spyro moves: up close they were drawn with the picture's colour applied twice (darker and more saturated, or washed out for bright pictures such as clouds) and only looked right from about 1,000 units away. Since Spyro Editor 1.2 (Wumpa Village, Cloudspire Isles and other built levels; export them again to get the fix).

# Spyro Editor 1.3

## Changes since Spyro Editor 1.2

New

- File ▾ → New Level from Spyro: Year of the Dragon…: open the level whose place it takes, pick one of Year of the Dragon's levels (35 of its 37 import) and the editor rebuilds it from your own disc in about a minute: its ground, walls, textures, water and lava, its distant scenery and a start where its level begins. The level it replaces is kept in a recovery copy (File ▾ → Restore Before Clear…) and keeps playing its own song.
- File ▾ → New Level from Crash Bandicoot: Warped…: the same for Crash 3's levels on foot (Toad Village, Gee Wiz and Double Header in halves, the Warp Room, the boss arenas): Crash's path, its pits (deadly, as for Crash), invisible walls along the path, and the scenery around it.
- File ▾ → Fill In What the Level Needs…: in a level you built, puts in the level's own dragons and egg thieves (each thief with a run around open ground), a Return Home vortex at the far end, enemies and animals of the level's own kinds, and gems worth the level's treasure, all on floor Spyro can walk or glide to from the start. A new level from another game does this at the end. Everything it adds is an ordinary object you can drag, turn or remove.
- "To finish 100%" under every level's treasure: how many of its own dragons and egg thieves it has and whether it has its Return Home vortex; amber with what to add when something is missing. Create BIN lists the same before you play, and warns about a dragon or egg thief placed twice.
- Edit Route… for objects you add (an egg thief's run, a patrolling enemy's path).
- Create BIN keeps every level you build within the game's drawing budget: it surveys the camera views from every walkable spot, draws busy distant scenery simpler and, if needed, brings textured scenery a little closer. A level that is still too heavy is refused with the place named; a guard in the game drops the farthest scenery in the rare heaviest frames instead of crashing (only in levels the editor built).

Fixed

- Crash spins all the way round in his spin.
- The sword knights keep their heads: their march used Crash 3's headless-knight animation. Faces of models far from their centre (the knight's swing, the wizard, the frog, Crash) no longer vanish; a conversion that would hide a part of a model is refused.
- Snowy trees stay snowy far away.
- A level's own egg thief, put back in its rebuilt level, carries its egg, drops it when flamed or charged, and the egg counts. A card that already has that egg finds the thief without one.
- Dragons from the Peace Keepers and Magic Crafters worlds can be copied (Create BIN refused them), and adding a dragon brings its pedestal and the dragon with it.
- A level rebuilt low down in a slot that kills Spyro high up (Ice Cavern, Cliff Town, Dry Canyon…) no longer kills him where he stands.
- A level's own creatures whose code checks what kind other objects are (Wizard Peak's Green Wizard's spell) can be put back in its rebuilt level.
- Scenery parts with more than 170 corners near the camera crashed the game; Create BIN splits them.
- Dry Canyon's egg thief was listed as a Flame/Charge Chest; "Egg Theif" spelled right.
