# Spyro Editor 1.0.2

## Changes since Spyro Editor 1.0.1

- Add **File → Start Blank in Current Level** for ten supported ordinary levels and the fresh authored level. Confirmation saves a checked recovery snapshot before clearing; **Restore Before Clear** works after reopening the project.
- Keep cleared levels in the ordinary level picker and shared terrain/object editor. Include a safe player start and removable starter floor, allow empty drafts, and verify a complete floor beneath the player before export.
- Rebuild cleared terrain, collision, object rows and asset tables so removed content releases real capacity. Preserve engine resources and retired collectible identities.
- Combine saved cleared levels, ordinary edits and the fresh level in normal **Create BIN**. Choose the New Game starting level; read back the completed image before publishing its BIN/CUE pair.
- Correct authored terrain face-cache ownership, subdivide large untextured faces internally, enable native textured clipping and encode sloped collision with the native Z format. Editable faces remain intact.
- Keep Previous/Next navigation correct after clearing. Label authored-scene reload accurately and protect source discs from aliased temporary paths.

Start Blank supports Artisans, Stone Hill, Town Square, Peace Keepers, Dry Canyon, Cliff Town, Magic Crafters, Beast Makers, Dream Weavers and Gnasty's World. Other ordinary level slots remain unsupported. Cleared Town Square and the separate fresh level cannot share one export. Native geometry, memory and save-identity limits remain.

Full universal Moby gameplay coverage is unfinished. Fly Anywhere remains deferred. The local macOS package is Developer ID signed but not notarized; Windows packaging does not establish Windows runtime acceptance. See the included user guide and limitations for the current supported scope.
