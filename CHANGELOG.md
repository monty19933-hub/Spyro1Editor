# Spyro Editor 2.2

## Changes since Spyro Editor 2.1

New

- Fix It For Me now works on every Create BIN stop that comes from your project, and one press gets the game made. It runs Create BIN itself, as often as it takes, and at the end says everything it did. A stop the editor has a repair for is repaired (the way into your world is placed; a world's homeworld is trimmed to the 82 objects it holds). For any other stop the editor finds the part of your project that causes it, by making the game without one part at a time (a level the message names first), and makes the game without that part. Nothing is deleted: the part stays in your project and is only left out of the game.
- It leaves out as little as it can. For one of the game's own levels it tries leaving out only one kind of your changes (its objects, its ground, its sky, its music or its name) before the whole level; if two things stop the game, both are left out and everything else stays in.
- Saved changes the game cannot take (an egg thief's run moved off its ground, for instance) no longer need you to find and undo them: Fix It For Me makes the game without them, and Create BIN names them each time.
- More ▾ › Left Out of the Game… lists what Fix It For Me left out, with what Create BIN said about each, and puts a part back with one button. Create BIN keeps leaving a part out until you put it back, and says so in its window and after each game. A newer version of the editor starts with nothing left out, in case it no longer stops there.
- Fix It For Me is not offered for a failure of the computer's (no disc image chosen, a full disk, no permission): those still say what to do.

Fixed

- A game with your new level (File ▾ › New Level, or a Random Level put in the new level's place) and no doorway leading to it froze when Spyro went into Town Square: the new level starts up through Town Square's start-up code, and without a doorway the two were not kept apart. They are now kept apart in every game. With Memory set to Emulator only (8 MB RAM), Create BIN stopped instead, with "Town Square does not fit game RAM even with 8 MB", although Town Square was untouched; that stop is gone with the cause.
- In a project that only changes the game's own levels, a failed Create BIN said "Could not create test" and offered nothing. It now reports the failure like every other Create BIN and offers Fix It For Me.
