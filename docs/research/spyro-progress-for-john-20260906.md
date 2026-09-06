# Spyro editor — what is working now

Newest progress: the imported Dark Hollow Wizard has now been defeated by a
normal charge and its native yellow gem raised the counter from13 to23. Normal
exit/portal re-entry also worked, retaining23 and marking the reward collected;
a second charge defeat also kept treasure at23 instead of awarding another10.
Every reload/save case is not certified; the exact new evidence is in
[the R03 report](native-dark-wizard-r03-reward-reentry.md).
The separate two-Wizard and edited fresh-floor exporters also pass complete
source/image checks, with a121-step/zero-failure default Release gate. Their
new research ROMs were created and independently reopened successfully; their
native tests remain next. The original
one-Wizard ROM and fresh starter remain unchanged. These exporters are not yet
ordinary editor buttons, and the installed editor has not been replaced.

Latest resumed work: a separate **Fresh Level…** document window now has
floor/spawn/color/music controls, a schematic preview, undo/redo and protected
save/reopen. It has passed65 headless UI checks and a separate retail-shell
integration check. It is not installed yet and does not publish edited ROMs.
Two independently placed Wizards now compose through the native resource and
reward chain, sharing class resources while retaining separate native identities.
Their physical test ROM is published; gameplay acceptance is next. R03 is closed,
and original settings, ROM, memory card and candidate hashes are restored/unchanged.

Updated September 6, 2026. This is a progress report, not a new release. The
installed editor has not been replaced by today's backend work. The original
ROM, saved edits, old candidates and original memory card are being preserved.

## What you can already explore in the editor

- **Native Moby Library:** browse the original game's variants across all 35
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
| Imported key and locked chest in Town Square | Key pickup, native opening/effects, six rewards worth exactly 10 gems, correct reward bits and normal exit/portal re-entry; recorded source-matched sound cues | Arbitrary duplicates, every level, partial-reward/death/card-persistence combinations |
| Wizard Peak Wizard imported into Dark Hollow | Normal portal entry, visible Wizard/lightning, electric damage, ordinary player death/reload, charge defeat and +10 reward, normal exit/re-entry, second defeat retaining the collected treasure total | Partial-collection death/card persistence, multiple simultaneous imported Wizards, full audio/presentation and peak-resource acceptance |
| Native Free Flight in Toasty | Ascent/descent, both turn directions, flap, flight exit, collision, death/respawn, normal exit/portal reload and controlled landing | Every level/state; finished editor checkbox and end-to-end disabled-output workflow |
| Fresh native LAB, without Town Square's terrain or placed scene | Native selector entry, LAB inventory title, visible Spyro and Sparx on the isolated floor, companion particles, a controlled short walk and normal-speed idle | Ordinary portals, jump/flame/death/card persistence, imported actors or exported textures in this fresh scene, complete audio/resource testing and editable UI integration |

These results belong to their exact retained candidates, not every possible
object/level combination. Gameplay permission is already granted. Future tests
will use DuckStation with isolated test saves/settings and cleanup afterward.

See the detailed [key/chest report](native-closure-runtime-20260905.md),
[Wizard report](native-dark-wizard-r01-runtime-observations.md), and
[Free Flight report](native-free-flight-r02-runtime-observations.md), and
[fresh LAB report](native-fresh-lab-r02-runtime-observations.md).

The LAB test recording and a short preview are saved in
`_local/native-fresh-id65-runtime-20260906-r01.LbsgK0/`. The first normal-speed
walk overshot the deliberately tiny floor; that attempt is not marked as a
successful center walk. After native re-entry, slower input pacing allowed
bounded movement, followed by roughly 50 seconds of normal-speed idle.
DuckStation is now closed and your original settings, ROM and memory card
have been verified unchanged.

## New engineering work behind the editor

- **Persistent object identity:** preserves the original 4,877 placements and
  native save-bit indices; authored additions receive stable append-only
  identities, including across save/reopen. Removed identities are reserved.
- **Fresh level body:** fresh floor, collision, spawn and scene lists without
  Town Square's terrain or placed scene. Required player/companion resources,
  native engine code and complete auxiliary allocations are still retained
  explicitly; this is not a claim that all retail dependencies are removed.
- **Cross-level texture allocation:** two genuine source materials from Dark
  Hollow and Gnasty's World can be allocated into the fresh resource set while
  preserving complete palettes, descriptors, pixels and protected storage.
  The old scene/model goldens stay unchanged. The new materials now pass through
  the shared native body builder with 157 focused checks; old plan identities
  remain exact. This new path is backend-only and not yet a textured disc export.
- **Fresh disc composition:** reserved level entries, relocated executable,
  bounded `LAB` title, 46 built-in music selections, ISO records and raw CD
  error-correction bytes now have an exact read-only builder/verifier. Three
  whole-image results match an independent implementation; both body sizes
  reconstruct the complete original exactly. Focused tests passed 2,182
  source-free and 566,016 total exact-source checks. An exact fresh-level BIN
  has now been published and independently reopened, with 20 publication and
  corruption checks passing. This exact candidate has now reached LAB and
  passed the limited entry/idle/controlled-walk observations described above.
- **Shared starter integration:** the exact tested starter recipe now lives in
  production code with cancellation and unchanged verification guards, rather
  than only inside a test program. This is a building block for the editor,
  not yet a new UI action or export of your edited Lab document.
- **Editable fresh-document foundation:** a separate, immutable document format
  now holds floor dimensions/position, spawn, display colors and all 46 supported
  music choices. Its strict canonical parser passes 160 checks. The document
  now drives native compilation: two edited examples changed the actual floor,
  collision, spawn, colors and/or music bytes. The original starter remains
  byte-exact. Existing Lab saves keep their original meaning. Project-bound
  save/reopen now works with stale-write protection and 83 additional checks.
  Viewport controls and publication of edited documents are still pending;
  these new examples have not yet been gameplay-tested.
- **Fresh-level gameplay diagnostics:** a dedicated, read-only observer now
  checks the new level's actual initialized resource layout. Its 17 tests and
  five original-game source checks pass; the existing Wizard observer's 14
  tests remain green. It supported the actual LAB session; the observed
  off-floor movement now has a regression-tested scope warning. Matching
  sampled resource addresses still does not certify every gameplay state.
  See the [observer notes](native-fresh-id65-observer.md).

## Next steps

1. Complete Wizard combat/reward/re-entry. Make the remaining native
   initialization and reward integration count-aware before building and
   testing the complete multiple-instance candidate; scene allocation alone
   is not enough. Then expand the supported dependency/destination combinations.
2. Build a practical fresh-level editing workflow around the now-connected
   document and native compiler: connect save/reopen to viewport editing and preview,
   then separately guarded edited-candidate output and DuckStation tests.
   Preserve existing saved Lab documents and the exact tested starter.
3. Carry the tested cross-level texture/body work through disc output and UI,
   retaining complete texture/resource ownership checks. Expand native gameplay
   action/transition coverage as each new combination becomes available.

Unsupported combinations must remain visible rather than silently claiming
universal compatibility. The detailed [resume checklist](native-editor-resume-20260906.md)
records what to do next without repeating the completed investigation.

**There is not yet a new unrestricted, fully editable blank-level editor to
install.** The current editor remains usable, and the new foundations are
substantially further along. Source tests, actual gameplay and a shipped UI are
tracked separately so the eventual editor reflects what has really passed.

Latest integrated verification: 113 default Release steps passed with zero
failures, including rebuilding App. All 71 Python tests completed successfully
(one original-ROM-only case is explicitly skipped there and passed separately).
The final document compiler's 182 source-inclusive checks passed again in the
lead engineer's independent run. The full fresh-disc source checks also passed
after the earlier textured-body changes. This did not install or launch a new
editor, and is separate from the earlier full 290-step gate.

Work is paused at this tested checkpoint with approximately 8% account usage
remaining, leaving a little extra reserve before the requested 5% limit. The
editor goal is not complete. The next steps and exact evidence are saved in the
resume checklist. No reset credit has been consumed; wait for John to explicitly
resume rather than restarting automatically.

## Resumed development

John explicitly resumed development later on September 6 and reconfirmed the
goal of Mobys available across levels. The pause above is historical. Work has
resumed from clean `f514085`; John will handle his usage reset personally.
Project-bound fresh-document save/reopen is now implemented and reviewed. The
final gate again passed 113 steps with no failures; the focused document suite
now has 243 checks. This did not install a new editor.

For multiple Wizards, new independent native-byte checks cover two separate
reward/save identities, collection in either order and checkpoint reloads.
The remaining resource/initialization/reward composition is not implemented yet;
no two-Wizard candidate or new gameplay pass is claimed. The exact source oracle
has 1,148 assertions, including 929 source-word consistency checks, not 1,148
independent gameplay tests.

The new isolated Wizard session cold-booted the unchanged candidate and loaded
the copied Stone Hill save. It was closed at the usage reserve before reaching
the imported Wizard. Capture finalized, observer and emulator exited, and the
original INIs/ROM/card/candidate were rechecked unchanged. The test video's
intended combat filename is not evidence of completed combat.

Usage reached about 5% during closeout. This tested checkpoint is paused for
the user's manual reset and explicit resume; the overall editor goal is open.

The [V6 readiness assessment](v6-readiness-20260906.md) explains the distinction
between a useful V6 beta and universal importing. The latter is not yet a final
polish step or a defensible percentage/date estimate.
