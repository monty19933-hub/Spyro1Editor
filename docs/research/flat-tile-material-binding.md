# Shared flat-tile face binding

September 25, 2026. Source-only follow-up to R08 checkpoint `b50874d`.
The preserved beta45 app is not rebuilt by this refactor.

`NativeFlatTileMaterialBinding` consolidates the same pointer-derived material
word walk formerly duplicated by tiled P0 data composition and tiled-family
terrain intent. It accepts a bounded complete canonical surface, one binding
per actual face, and0..128 direct-LQ records. FF remains the base-color marker.
It captures inputs before caller enumeration, refuses excess/missing bindings,
validates all five components and preserves their complete complement. Exact
inverse verifies the entire bound surface, including non-material bytes.

P0 still owns its own material metadata and portal/particle/sound tail. The
family still owns its actual R03 resources. This shared byte operation does
not interchange those resources or broaden source/UI/export admission.

Tests-first log712 fails on21 missing-method references. Build713 passes
without warnings/errors. Log714 passes923 Wizard authoring checks, including
127 complete eight-face patterns with first face0 and both materials present,
the old independent surface golden, direct-index boundaries, malformed
preimages/complements and bounded hostile iteration. Existing P0 texture
goldens and translated-tail tests pass in716:19,269 checks, build715 clean.

Independent review found no actionable issue. Actual original-source texture
suite721 passes132,825 source checks (152,094 total), with final Original
preservation. Independent new-process readback720 reproduced the packaged
R08 complete BIN689f,1297 changed sectors and exact saved receipt using this
refactor; every byte inversed to Original and all final source guards passed.
Broader regression remains pending. Generalized face painting remains disabled until
those gates and its own exact-disc gameplay test are complete.
