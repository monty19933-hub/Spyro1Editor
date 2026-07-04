# Known Limitations For The First Tester Beta

This beta is useful, but it is not a finished Spyro modding tool yet. Treat it as a testing build for editor workflow, object placement, terrain inspection, and moby identity mapping.

## Do Not Ship Or Share Game Data

The beta package must not include:

- Spyro disc images or patched disc images.
- BIOS files.
- Emulator saves or save states.
- RAM or VRAM dumps.
- Extracted WAD payloads.
- Runtime scene overlays generated from the game.
- Texture dumps or actor atlases.

Users must provide their own legally obtained game image and generate local cache data on their own machine.

## Object Editing

Generally safer:

- Moving known gems.
- Moving known scenery.
- Moving known keys.
- Moving known chests that already exist in that level.
- Editing existing gem value bytes that are already proven by smoke tests.

Still guarded:

- Cross-level object imports.
- Actor/package imports.
- Spring Chest.
- Key Chest in levels where no native proof exists.
- Wizard/enemy transforms.
- Anything that needs linked controller, reward, collision, or behavior records.

If an object appears visually but does not react correctly in-game, do not promote it as working.

## Spring Chest Status

Spring Chests have two different paths in the editor.

The safest path is **From this level: Spring Chest (native Spring Chest)**. That clones a Spring Chest donor that already exists in the loaded level, including its same-level source special data. Use that first whenever it appears in Add Object.

The cross-level Spring Chest path is blocked for the public beta. Repeated Stone Hill candidates either stayed inert, misrouted Sparx/reward behavior, broke nearby chest behavior, or crashed while loading after full-package import attempts. The current evidence says imported Spring Chest shells can start in the wrong runtime lifecycle state before Spyro hits them, so the normal Add Object list hides cross-level Spring Chests unless a future candidate has passing in-game evidence. Do not report a cross-level Spring Chest as supported unless the exact beta candidate:

- Boots cleanly.
- Appears in the intended level.
- Reacts to flame or charge.
- Plays/behaves like a real spring chest.
- Spawns a visible gem.
- Does not let Sparx collect the gem incorrectly.
- Resets or disappears correctly after collection.
- Does not break nearby objects.

## Terrain Editing

The editor can preview terrain surfaces and stage some terrain art/height edits, but terrain behavior is not fully decoded.

Current labels such as grass, stone, water, lava, sand, ice, or ooze are editor/material labels unless a behavior proof says otherwise. A color or texture change does not automatically mean the in-game collision or damage behavior changed.

Before promoting terrain behavior, testers need before/after in-game evidence such as:

- Spyro health/state change.
- Knockback or death behavior.
- Solid-ground control capture.
- Repeated proof on another face of the same surface type.

## Moby Names

The editor intentionally leaves uncertain objects as Needs ID. Question-mark labels are better than fake confidence.

Only promote a moby identity when the tester can clearly observe the model or behavior. Do not promote names like:

- object
- moby
- prop
- scenery
- actor
- enemy
- chest
- marker
- helper
- control
- unknown
- possible/candidate names

## Generated Output

Patched BIN/CUE files are disposable local test output. Keep the original clean image safe and do not distribute patched game images.

For beta testing, report whether a generated CUE:

- Boots.
- Loads the intended level.
- Shows the intended object or terrain change.
- Preserves normal game behavior.
- Crashes, hangs, soft-locks, or changes unrelated objects.

## First Beta Success Criteria

This beta is successful if testers can:

- Launch the editor on Mac or Windows.
- Build or use a local editor cache.
- Browse levels.
- Move known objects and create disposable test BIN/CUE output.
- Use Fly 3D and Map View comfortably.
- Record moby ID review results inside the editor.
- Clearly understand which features are safe, experimental, or not proven yet.
