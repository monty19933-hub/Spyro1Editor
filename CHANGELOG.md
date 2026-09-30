# Spyro Editor Beta V6

## Changes since Spyro Editor Beta V5

Local release candidate, internal build `0.1.0-beta.53`. Full release acceptance
remains open; see the accompanying coverage and verification report.

- Removed the added eyes and blinking from dragonfly eggs. Export now uses the
  original Japanese egg model, with independently selectable colors. The native
  inventory icons also have no added eyes.
- Added persistent placement identities and an append-only slot ledger. Duplicate
  objects receive new identities; recoloring and reordering keep their identities;
  deleted placements retain inactive slots so later objects cannot inherit their
  collected state. Portable projects retain the native layout binding. Changing
  the ordinary row prefix after an export is refused rather than shifting save bits.
- Added a native check that retires a collected egg before animation or pickup
  after actor reconstruction. The original save bitmap is reused; no extra card
  fields are allocated. Native serializer and instruction-level checks pass;
  normal memory-card save and cold-load acceptance remain open.
- Added required-egg weighting to the native live inventory percentage. Each
  dragonfly egg has the same score weight as an ordinary egg, with an expanded
  denominator and integer rounding down. With no custom eggs the original
  percentage function is unchanged. Save-menu percentage, bonus-entry rules and
  the requested per-level X/Y interface still need integration and runtime tests.
- Improved the game-wide object browser with real locally rendered model previews,
  global search, labeled egg color swatches, and a live sign-text preview. Signs
  support three lines of normalized native text; the preview shows export limits.
- Strengthened cross-level import checks for source model/behavior collisions,
  sound availability, texture ownership and shared dependencies. Added support
  for 8-bit and 16-bit texture relocation and missing particle texture records.
  Dependency equality is checked before sharing; unresolved behavior is reported.
- Removing every instance of a resident class can reclaim its proven exclusive
  texture allocation as well as its model memory. Shared terrain, particles and
  retained object resources remain protected. Capacity refusals identify the
  actual resource rather than treating every failure as a memory shortage.
- Added reproducible identity, import and native instruction tests, plus a
  machine-readable type/variant by destination matrix. Static composition does
  not certify enemy AI, collision, sound, links or other gameplay dependencies.
- Placed the existing flight preference under Game Settings > Starting Abilities.
  Fly Anywhere ON still supports Toasty alone without other saved edits; OFF
  rebuilds from the original disc. All-level flight and normal save/load, death
  and transition acceptance are not complete.
- Preserved the existing retail editor, fresh-document authoring and ID65 research
  workflows. File > New Level in the same complete retail editor is not finished;
  the current fresh workflow retains its documented narrow export profiles.

Supported export source: exact USA SCUS_942.28. Japanese egg/sign extraction:
SCPS_100.83, supplied locally by the user. No game images, BIOS, extracted native
assets, emulator cards or save states belong in the application packages.

macOS arm64 candidates use local ad-hoc signing and are not Apple notarized.
Windows x64 packaging remains supported; Windows runtime acceptance is separate.
Original source images, projects, saves and prior release packages are preserved.
