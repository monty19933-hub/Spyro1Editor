# Spyro Editor 2.1

## Changes since Spyro Editor 2.0

New

- Fix It For Me. When Create BIN stops on something the editor can put right by itself, a Fix It For Me button appears beside the message (and in the Details window). Press it, read what it did, and press Create BIN again. It handles two things so far. "…needs the way into your world there": the editor places the doorway into your world in the homeworld it is entered from, on level open ground it finds near where Spyro arrives, clear of walls, water, objects and the homeworld's own doorways, in that homeworld's kind of doorway (in Dream Weavers the small size; in Gnasty's World, which has no free place, it says so and you place it yourself). "…has more objects than a new world's homeworld has room for": it gives back the save rows of deleted objects and, if that is not enough, takes out loose gems, the least valuable first, says how many and what they were worth, and keeps the level as it was in the project's backups folder.
- Creatures' colours, level by level. Game Settings › This level › Creatures' colours lists the open level's kinds of enemy, each drawn as it will look, and gives each any colour for that level only: the same gnorc can be red in Artisans and blue in Dark Hollow. Randomize gives every kind another colour (again for others), Their Own Colours puts them back. A creature keeps its shading, and what is of another colour on it (skin, armour, eyes) stays. It works in the game's own levels and in levels you build (for the enemies placed in them).
- Spyro's colours for one level. On Spyro's colours, set "Colours for" to the open level and choose a look or change a part: Spyro has that look in that level only, and the whole game's look (or his own colours) everywhere else. Same as the Whole Game takes a level's look away again.

Fixed

- Create BIN with one of the game's own levels open left your new world out of the game without saying so, when the world's levels were the only levels you had built (the game was made as "All Saved Edits", with no world and no balloon route in it). Create BIN now builds the whole project whichever level is open.
- Random objects could put more objects into a new world's homeworld than it can hold (82: it shares the game's 256 save rows with Artisans' own objects), and Create BIN then stopped with "the cleared level's previous collectible identities fill the native save rows". Random objects now keeps to the room there is, and the message names the level and the numbers.
- The doorway into your world could not be built into a homeworld to which you had also added objects ("No class switch was found in the level update function"). It is now built together with them.
- With many features on at once (a world of five levels, Hover, colours, added objects), Create BIN could stop with "the level window's … does not fit its executable caves" although the game's program had the room. The pieces are now fitted by search when the simple way leaves one out, and when there really is no room the message says how much is needed and how much there is.
- After File ▾ › Free Deleted Objects' Save Rows…, Random objects counted rows it had already given back.
