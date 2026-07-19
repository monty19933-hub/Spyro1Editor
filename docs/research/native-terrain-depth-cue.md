# Native Spyro 1 terrain depth cue

This note records the renderer evidence used by the Fly 3D terrain preview. It
is intentionally narrower than a claim that the whole viewport is already a
pixel-exact PS1 renderer.

## Retail renderer evidence

The local Spyro 1 decomp at commit
`4e7b7f06e552a20b9fad76e8ee67e68027b39082` identifies
`func_800258F0` in `asm/renderers/r_environment.s` as the world renderer.

- Initialization sets `g_Environment.m_LodDistance` to `0x8000`.
- The HP path loads that distance at `0x800267B0` and divides it by four at
  `0x80026DE8`.
- For each raw face corner it computes `IR0 = 0x2000 - SZ`, compares the result
  with `0x1000`, and uses GTE `DPCS` only inside that interval
  (`0x80026E00` through `0x80026E48`, repeated for all four corners).
- The first physical HP color table is loaded into the GTE far-color registers
  and is selected at `IR0 >= 0x1000`. The second table is loaded as the RGB
  source and is selected at `IR0 <= 0`. Therefore table 1 is the visible/near
  endpoint and table 2 is the fade/far endpoint.
- `SourceSceneOverlayExporter` exposes scene coordinates at one quarter of the
  HP renderer's GTE coordinates. Game camera/Moby positions are fixed-16;
  `MobyLoader` divides them by 16, while the HP renderer shifts the camera by
  two and keeps HP local vertices four times larger. Consequently
  `GTE_SZ = editorCameraDepth * 4`.

The exact editor-space ramp is therefore:

```text
depth <= 1024: table 1 (near)
1024 < depth < 2048: DPCS(table 2 -> table 1, IR0 = 8192 - floor(depth*4))
depth >= 2048: table 2 (far)
```

The DPCS channel calculation used by the preview retains the retail integer
arithmetic and signed right-shift behavior:

```text
result = ((far << 12) + (near - far) * IR0) >> 12
```

## Serialized-name compatibility

The existing v3 overlay contract historically calls physical table 2
`NearColors` and physical table 1 `FarColors`. Those labels are reversed from
the retail renderer's semantic endpoints. This pass deliberately does not
rename or migrate persisted terrain edits, because exporter persistence patches
both raw table slots and needs a coordinated contract migration. The viewport
keeps the serialized fields stable and consumes them as:

```text
runtime near = legacy FarColors
runtime far  = legacy NearColors
```

## Face depth field

The face `depth` value comes from bits 3..7 of the fourth face word. In the HP
renderer, the low three bits are added to the ordering-table bucket at
`0x80026DC0` through `0x80026DCC`. The value is not read by the DPCS color path,
so it must not affect terrain color interpolation.

## Deterministic verification

`Spyro.Editor.UiSmoke --terrain-depth-cue-only` verifies the two endpoints,
the midpoint, and IR0 boundaries, then renders paired legacy/native frames from
the same camera in Stone Hill, Dark Passage, Dream Weavers, and Gnasty's World.
The July 16 retail-cache run exercised:

| Level | Visible faces | Near / blend / far corners | Corners changed from legacy | Mean RGB-channel delta |
| --- | ---: | ---: | ---: | ---: |
| Stone Hill | 2,309 | 720 / 3,118 / 5,398 | 3,245 | 5.80 |
| Dark Passage | 3,651 | 521 / 2,955 / 11,128 | 2,658 | 2.39 |
| Dream Weavers | 2,832 | 1,654 / 3,861 / 5,813 | 4,311 | 6.67 |
| Gnasty's World | 2,188 | 1,711 / 3,518 / 3,523 | 4,255 | 9.78 |

The smoke fails if a retail fixture has no blended corners, no distinct color
endpoints, no changed corners, or a pixel-identical before/after frame. It also
writes full SHA-256 values and capture paths to
`_local/ui-overhaul/terrain-depth-cue-metrics.json` in the tested project.

## Remaining renderer boundaries

This evidence proves the static HP terrain color ramp and the meaning of the
face depth field. It does not by itself prove PS1 semi-transparency ordering,
animated/scrolling texture timing, or every GPU blend edge case. Those remain
separate fidelity work and should not be described as solved by this change.
