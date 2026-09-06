# Spyro editor — what is working now

Updated September 6, 2026. This is a progress report, not a new release. The
installed editor has not been replaced by today's backend work. The original
ROM, saved edits, old candidates and original memory card are being preserved.

## What you can already explore in the editor

- **Native Moby Library:** browse the original game's variants across all35
  retail levels, search by name/class/level, and distinguish placed objects from
  registered resources. It is wired into the retail Objects panel and the
  Blank-Level Lab. An item is only addable where an existing approved placement
  template matches; being visible in the library does not yet make every item
  interchangeable.
- **Existing-level editing:** the established object, terrain and guarded
  Create BIN workflows remain. Cross-level native terrain art/behavior choices
  already exist where complete destination/source ownership checks pass.
  Unsupported animated/runtime-controlled transfers and the old custom-PNG
  painting path remain blocked.
- **Blank-Level Lab:** canonical saved documents, explicit migrations, the
  limited two-tile and None/Grass/Red Gem choices, plus the separately guarded
  Blank Slate schema-6 candidate workflow. These are real existing tools, but
  they are not yet unrestricted terrain/Moby authoring or arbitrary new levels.

These UI statements were checked against current source. They do not claim
that an older installed app has automatically acquired every source change.

## What has actually worked in DuckStation

| Exact research case | Observed working | Still not established |
| --- | --- | --- |
| Imported key and locked chest in Town Square | Key pickup, native opening/effects, six rewards worth exactly10 gems, correct reward bits and normal exit/portal re-entry; recorded source-matched sound cues | Arbitrary duplicates, every level, partial-reward/death/card-persistence combinations |
| Wizard Peak Wizard imported into Dark Hollow | Normal portal entry, visible Wizard and lightning, native electric damage/animation, ordinary player death and level reload | Wizard defeat/+10 reward, post-defeat re-entry, multiple simultaneous imported Wizards, full audio/presentation and peak-resource acceptance |
| Native Free Flight in Toasty | Ascent/descent, both turn directions, flap, flight exit, collision, death/respawn, normal exit/portal reload and controlled landing | Every level/state; finished editor checkbox and end-to-end disabled-output workflow |

These results belong to their exact retained candidates, not every possible
object/level combination. Gameplay permission is already granted. Future tests
will use DuckStation with isolated test saves/settings and cleanup afterward.

See the detailed [key/chest report](native-closure-runtime-20260905.md),
[Wizard report](native-dark-wizard-r01-runtime-observations.md), and
[Free Flight report](native-free-flight-r02-runtime-observations.md).

## New engineering work behind the editor

- **Persistent object identity:** preserves the original4,877 placements and
  native save-bit indices; authored additions receive stable append-only
  identities, including across save/reopen. Removed identities are reserved.
- **Fresh level body:** fresh floor, collision, spawn and scene lists without
  Town Square's terrain or placed scene. Required player/companion resources,
  native engine code and complete auxiliary allocations are still retained
  explicitly; this is not a claim that all retail dependencies are removed.
- **Cross-level texture allocation:** two genuine source materials from Dark
  Hollow and Gnasty's World can be allocated into the fresh resource set while
  preserving complete palettes, descriptors, pixels and protected storage.
  The old scene/model goldens stay unchanged. This new allocator is backend-only.
- **Fresh disc composition:** reserved level entries, relocated executable,
  bounded `LAB` title,46 built-in music selections, ISO records and raw CD
  error-correction bytes now have an exact read-only builder/verifier. Three
  whole-image results match an independent implementation; both body sizes
  reconstruct the complete original exactly. Focused tests passed2,182
  source-free and566,016 total exact-source checks. No new BIN has been
  published or booted from this stage yet.

## Next steps

1. Finish the shared textured resource-to-body integration with regression
   tests that reject mixed/stale resource sets, even when transfer bytes match.
2. Close the remaining fresh-level native initialization/resource requirements,
   then produce a separately identified test candidate and exercise it in-game.
3. Complete Wizard combat/reward/re-entry and multiple-instance tests before
   expanding the supported native dependency combinations.
4. Connect the validated new-level pipeline to practical UI authoring, persistence
   and export controls. Expose unsupported combinations clearly rather than
   silently claiming universal compatibility.

**There is not yet a new unrestricted, fully editable blank-level editor to
install.** The current editor remains usable, and the new foundations are
substantially further along. Source tests, actual gameplay and a shipped UI are
tracked separately so the eventual editor reflects what has really passed.

Usage instruction for this work session: continue while budget permits, begin
safe closeout before roughly5% remaining, save commits and a precise continuation
note, then wait for John to reset usage and explicitly resume. Do not redeem a
reset credit or auto-resume while paused.
