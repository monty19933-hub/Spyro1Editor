# Spyro Player Editing Status

## Current Decision

The whirlwind/add-object path is parked for now. The single appended whirlwind record is not enough to recreate the complete behavior, so player-facing edits are the better next track.

## Feasibility

- Spyro color: likely feasible. The safest path is to identify Spyro's loaded texture/CLUT data in VRAM first, live-poke a candidate color, then trace that confirmed palette or texture data back to the disc.
- Crystal dragon color before rescue: likely feasible, but it may be palette, texture, or per-model color data. It should use the same live-poke-first workflow as Spyro color.
- Double jump / d-jump: possible, but it is a separate player-code patch. This should start as a live RAM prototype, then graduate into a PS-X EXE patch once the player state fields and jump/glide routine are known.

## Known Leads

- Main executable: `SCUS_942.28`, LBA `53875`, size `417792`, load address `0x80010000`, initial PC `0x8005B8E0`.
- Runtime cheat/code leads worth treating as addresses to inspect, not final patches:
  - `0x80077380`: controller/input condition lead.
  - `0x80078AD8`: jump or vertical-motion lead from a moon-jump style code.
  - `0x80078CA4`: glide/fly state lead from a fly-instead-of-glide style code.
  - Source lead: `https://www.supercheats.com/playstation/spyro-the-dragon/16012/fun-codes/`.
- Existing VRAM sample: `duckstation-state-gpu-vram-fresh-stonehill.bin`.

## Tooling Added

- `tools/New-SpyroVramPaletteCandidateReport.ps1` creates ranked VRAM palette candidate sheets for Spyro-like purple/warm palettes and crystal-dragon-like cyan/green/blue palettes.
- The tool now supports 16-color CLUT scans, which are more likely to match PS1 character textures than the earlier broad 256-color sweep.
- First strict 16-color scans against `duckstation-state-gpu-vram-fresh-stonehill.bin` found no obvious Spyro-purple or crystal-cool CLUT rows. That does not rule out color editing; it means the next proof should be a live mutation probe or a search for direct model/texture color data rather than relying only on hue-ranked palette sheets.

## Next Validation Steps

1. Build a live mutation probe that can safely poke small VRAM/RAM color candidates and immediately restore the original bytes.
2. Use the probe on Spyro first, then on a nearby crystal dragon, while watching DuckStation for a visible color change.
3. Once a color candidate is proven, add editor controls for palette swaps and connect the confirmed bytes back to the disc patcher.
4. For double jump, build a live prototype around the player input/state addresses before attempting a permanent executable patch.
