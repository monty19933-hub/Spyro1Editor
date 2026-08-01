# Spyro Editor Beta V4 Known Limitations

- Use a supported retail Spyro the Dragon BIN/CUE. The editor stops an export when the selected disc does not match the expected game data.
- Some cross-level enemies and special chests are not yet available in every level. Options that are not ready are hidden from the normal Add and Replace galleries.
- Single-section cross-level texture painting depends on source-proven free texture capacity or an exact promoted private-row profile in the destination level. It is not available in every level yet.
- Linked-section replacement is offered throughout the level catalog, but every selected target/donor pair must pass its own PlayStation texture-memory and ownership checks. Some combinations cannot physically fit and remain blocked rather than producing an unsafe BIN.
- Imported music must fit the original level slot and PlayStation audio format.
- Arbitrary image replacement is not available for every native texture type.
- Flight levels have additional game-specific restrictions.
- Always test a generated CUE in DuckStation before sharing a project or release.

The editor creates a new BIN/CUE and never overwrites the original disc image.
