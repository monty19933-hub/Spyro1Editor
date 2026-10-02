# Per-statue color editing — 2 October 2026

Double-click a dragon statue, enable **Customize dragon color**, and choose a
color using the existing picker or hex field. **Use original color** disables
only that statue's customization. Copy, project save/reopen, fresh scene
serialization and undo preserve the same per-object behavior.

## Implementation

`DragonStatueColor` stores lossless RGB888 with renderer tag `0x7E` in native
row bytes `4C..4F`, retaining draw distance and enabling the shiny renderer.
Normal object export and Universal Moby composition both install the same
`NativeDragonStatueColors` extension. Installation is idempotent; partial or
changed installations and changed native owners are refused.

The original shiny renderer's `8002174C` load branches through four complete,
unused original math functions (352 bytes total): `80017188..800171FC`,
`80017548..800175B8`, `8001771C..80017758`, `80017854..80017894`.
The host audit covers the original EXE, all 35 level overlays, title/credits
code and WAD address consumers. Adjacent live functions are preserved. Host
preimages and renderer/caller windows are authenticated. The separate
`index-links/native-host-audit/renderer20F34-LA-audit.json` authenticates the two
original renderer callers and classifies raw asset address lookalikes.

The custom path applies the selected hue to each facet's original maximum
channel brightness in the current draw's existing 256-color fog buffer. It
preserves the palette command byte, T0-T2 and native LO ownership. The ordinary
path preserves the original behavior. Shared model palettes are never modified;
there is no per-statue model duplication or additional level RAM allocation.
`tools/Build-DragonStatueTint.py` assembles and verifies the shipped words;
`tools/Test-DragonStatueTint.py` executes them with native load/branch delay and
HI/LO hazard checks. A0-A3/V0-V1 are dead on authenticated native continuations.

Each accepted statue animation must have valid near/far palette extents and
1..256 colors. All original FA models passed these bounds. The strict original
renderer view lets the E2 metal texture owner remain authenticated when this
exact extension is installed; unknown code never bypasses the metal checks.

## Evidence

Task-root evidence: `work/universal-finish-20261002/`.

- `dragon-final-source-r6.log`: **2,400** source/export checks, all **79** native
  statue rows, source/installed-byte mutation refusal, normal retail export and
  independent readback, plus actual E2 composition after statue recoloring.
- `dragon-source-free-r1.log`: **523** row encoding and malformed model/executable
  checks. The R3000 scalar covers **856** executions, including 1/2/255/256-entry
  palettes, in-place fog input, ordinary pass-through, extremes and random RGB.
- `dragon-colour-ui-r2.log`: **14** actual dialog, copy, save/reopen and undo
  checks. Full integrated regression is still pending at this checkpoint.
- `runtime/dragon-body-r4/runtime-body-colour-proof.json`: real USA BIOS,
  software renderer, private blank card, native New Game and controller inputs.
  Red and blue statues appear together. Red rescue completes and returns to
  gameplay. Read-only RAM records red row65 collected and blue95/untouched111
  uncollected. No RAM writes or emulator save states were used.
- `runtime/dragon-body-r4/shared-palettes-unchanged.json`: both native shared
  255-color tables exactly match the prior unmodified-body baseline at runtime.
- Product export `runtime/dragon-body-product-r5/candidate.bin` has the same SHA
  as the emulator-tested private candidate:
  `510740dfbced8b33b809065871bb882b9ba67d284964cc86c3d995d8ec0fe83e`.

The original shine-only candidate failed the requested body-color behavior and
is retained as failed evidence. The contiguous `80017428` host was rejected
because original overlays call it. Neither is the implemented solution.

This proves bounded statue color/rescue behavior, not every color in every
level, memory-card reload, or all Universal Moby gameplay. No release package
has been published for these changes yet.
