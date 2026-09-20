# V6 development: what you can test

September 19 continuation. The preserved **R06 private preview** is the latest
editor build tested here. Newer source checkpoints are not silently installed
into it. Universal Moby/texture interchangeability remains the goal, not a
completed feature or a zero-bug guarantee.

## Playable editor workflow

Open this repository's
`_local/v6-private-preview-20260919-r06.ztR4LG/Open V6 Selected Materials.command`.
Use the launcher rather than the bare app: it selects the isolated test project
and settings without rebuilding or resetting your normal project.

In **Fresh Level… → Level aggregate**, the saved example has three adjoining
tiles and three static retail material selections. You can change the geometry
and supported material choices, apply the preview, save the document, and use
**Create research candidate…** to create a new BIN/CUE/receipt folder.
The folder's `PRIVATE-PREVIEW.md` has exact instructions and the LAB-entry code.

The exact editor-produced example cold-booted through full firmware in
DuckStation. Grounded crossings of both seams and a collision-cell boundary,
forward/reverse material views, jumping, falling and respawning were observed.
This is actual gameplay evidence for that example, not just a successful build.
Its source checkpoint is `01ef92a`; its whole BIN hash is
`f1c137476833d1c046a78df812b40a47f8dc6558051c823437d7c5b3f86d8e32`.
Original emulator settings and memory-card contents were preserved/restored.

## Not yet available in that build

- Tiled new-level export still requires zero placed Mobys. Existing separate
  Wizard/Grass research paths are not unrestricted tiled-level placement.
- Static material selection is bounded by native data and GPU capacity.
  Animated/scrolling/HQ materials are not universally supported.
- The catalogue can show assets without proving that every destination has
  their behavior, resources, state, effects and save/re-entry dependencies.
- Full portal/save ownership, arbitrary new levels and universal import remain
  unfinished. No public V6 release was made.

## Source work after R06

Checkpoint `72ace6f` adds a detached chest-code extension bound to the actual
mixed Wizard/Grass executable. Its independent source tests preserve the old
code and verify changed dispatch, callback-entry arguments, initializer and
complete inverse. The full regression passes343 command steps; the final
source-backed run passes8579 assertions with original BIN/CUE unchanged.

The scene stage now adds the key, chest and five reward rows through
the existing scene allocator. It matches the independent nine-row scene bytes,
passes343 full regression steps and8885 final source assertions, and preserves
the complete inverse to the previous scene. A subsequent Body stage installs
that scene while preserving every byte outside it, passes9034 source assertions
and343 full regression steps, and checks the enlarged Host's runtime addresses.
The next source stage places that family on the authored floor and binds an
explicitly experimental target20 executable. Shared physical export machinery
now constructs the chest image and inverses all sectors back to the original;
the full run passes9573 source assertions and343 regression steps, with prior
export goldens preserved. This is not yet a gameplay pass or editor admission.
Key-zero contribution remains an explicit assumption, not a complete census.
See [the private experiment record](research/native-fresh-chest-experimental-disc.md).
Complete native behavior/lifecycle, actual output and fresh gameplay checks
remain distinct gates.
See [the scene record](research/native-fresh-chest-scene-extension.md) and
[the behavior record](research/native-locked-chest-behavior-layout.md) for current
implementation evidence. These source-only changes do not replace R06.
