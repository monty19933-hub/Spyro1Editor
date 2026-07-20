# Spyro Editor Beta V3

## Changes since Spyro Editor Beta V2

- `Choose Texture & Start Painting` now opens only the current level's texture catalog. Textures from another level are loaded only after that level is selected and `Load Level Textures` is pressed, avoiding the previous whole-game startup delay.
- The texture chooser now uses a compact six-column gallery with larger native previews, concise tile names, and double-click-to-paint behavior.
- Unsafe animated, runtime-controlled, incomplete, or otherwise unsupported texture records remain visible but are grayed out and marked `BLOCKED`, with the specific reason available in the chooser.
- Same-level painting now changes only the terrain face that was clicked. It no longer silently repaints every face that shares the source texture record.
- Cross-level painting proceeds only when the selected destination face owns a safe unique texture record. Shared destinations are refused without changing the level; the existing explicit shared cross-level replacement and custom color/import/paste workflow remains available through the separate `Advanced / Shared Replacement` terrain-panel button.
- Texture relocation now revalidates the selected level, face set, load request, and edit state immediately before mutation. Changing levels or editing the target while a donor is loading cancels the stale operation instead of applying it to the wrong scene.
- V3 retains the exact numbered release identity understood by installed Beta V2 clients, so publishing the official `beta-v3` prerelease creates their in-app update notification. Projects, generated output, settings, backups, and imported assets remain outside the replaceable application folder and are not overwritten by installation.
- V3's updater supports future canonical incremental releases such as V3.1 and V3.2. Every published release carries a changelog containing only the exact delta from the immediately previous public version.
- The Beta V3 macOS ZIP is Developer ID signed, hardened, and securely timestamped. It is not notarized, so it includes SHA-256 verification and System Settings > Privacy & Security > Open Anyway instructions; the Windows ZIP is unaffected.
