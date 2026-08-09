# Spyro Editor Beta V5 Known Limitations

- Use a supported retail Spyro the Dragon BIN/CUE. The editor stops an export when the selected disc does not match the expected game data.
- ID65 Blank-Level Lab is an evidence-bound construction lab over an independent
  Town Square-derived engine substrate, not a truly empty level. V5 supports the
  guarded existing-HP-height/collision-fan path there. True Add Terrain, LP/XY
  geometry, arbitrary cross-level Mobys, private/custom texture allocation,
  portals, authored totals/music, and save-slot ownership are not yet public Lab
  capabilities. The Lab's disposable CUE is deliberately separate from normal
  Create BIN.
- Some cross-level enemies and special chests are not yet available in every level. Options that are not ready are hidden from the normal Add and Replace galleries.
- Single-section cross-level texture painting depends on source-proven free texture capacity or an exact promoted private-row profile in the destination level. It is not available in every level yet.
- Linked-section replacement is offered throughout the level catalog, but every selected target/donor pair must pass its own PlayStation texture-memory and ownership checks. Some combinations cannot physically fit and remain blocked rather than producing an unsafe BIN.
- Imported music must fit the original level slot and PlayStation audio format.
- Arbitrary image replacement is not available for every native texture type.
- Flight levels have additional game-specific restrictions.
- Always test a generated CUE in DuckStation before sharing a project or release.

The editor creates a new BIN/CUE and never overwrites the original disc image.
