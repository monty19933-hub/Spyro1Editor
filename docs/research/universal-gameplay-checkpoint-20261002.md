# Universal Moby gameplay checkpoint, 2 October 2026

Work continues from `ada3e545759c6af7b80e30a1082b7609c6bf5509` on
`release/universal-gameplay-20261002`. This checkpoint adds authored linked
groups, native dependency ownership fixes and per-statue color editing. Universal
gameplay and final release acceptance are still unfinished.

The task evidence root is
`/Users/john/Documents/Codex/2026-09-29/files-pasted-by-the-user-spyro/work/universal-finish-20261002`.

## Implemented and verified scope

- Integer source row links enroll authenticated companion groups. The editor
  saves, moves, copies, removes and restores complete groups. Native pointer
  fixups resolve only within their source level and copied group, including a
  final writer ownership check. Actual UI/export tests covered Lofty Castle
  whirlwind groups and the Gnorc Cove barrel pair. Generic arbitrary pointer
  companions are not automatically admitted.
- Native damage calls contribute Spyro reaction animations. The 46 original
  calls across 23 source families are authenticated separately from ordinary
  class reads. The Gnorc physical model-table lookup and parent class ownership
  have bounded adapters with mutation checks.
- Cliff Town E2 preserves a valid destination metal material or imports all
  source pixels and eight fog palettes when the slot is empty and capacity
  permits. It does not silently silence a missing sound or overlap textures.
- Dragon double-click editing has an RGB picker, presets, original-color reset,
  persistence and independent copies. The native renderer tints each statue's
  private draw palette without changing shared model palettes. See
  [the color proof](dragon-statue-colour-20261002.md).
- Dry Canyon B8 has a source-issued branch decision for its unreachable legacy
  B7 spawn. Complete source data, overlay/callbacks, animation frame counts and
  native transition engine are authenticated. Final row overrides and final
  animation assets are checked. The real E6 spawn remains. The source mutation
  and fresh-composition suite passes 107 checks; an independent native animation
  interpreter evaluated 97,536 ticks without an event at the impossible frame.
- Magic Crafters native removal protects positive druid indices and native pod
  memberships. Partial deletion of a live pod refuses; deleting a whole pod or
  an unrelated gem remains allowed. Its 242-check source suite also rejects
  altered table membership, pod identities and selectors. This does not yet
  authorize the full Magic Crafters druid import.

## Bounded emulator evidence

All tests use a private DuckStation app, original USA BIOS, new isolated blank
cards and ordinary controller input. RAM snapshots are read-only. No emulator
state or memory edits supply gameplay evidence.

| Candidate | SHA-256 | Accepted interaction |
| --- | --- | --- |
| `runtime/gnorc-darkhollow-r5/candidate.bin` | `5331a507a1cec289bb636f433b6dcd8476b73b835ae9ad7297445b0a01d52e14` | TNT explosion, Spyro hurt animation 29 and recovery |
| `runtime/e2-darkhollow-r2/candidate.bin` | `9c426273054fbe415907719059b7567359ca2fa555f392ac5858ca0d35544640` | Metal model, charge defeat, green reward and both totals 2 |
| `runtime/dragon-body-r4/candidate.bin` | `510740dfbced8b33b809065871bb882b9ba67d284964cc86c3d995d8ec0fe83e` | Simultaneous red/blue bodies; red rescue returns to gameplay |
| `runtime/b8-darkhollow-r1/candidate.bin` | `9ca381f19c4d3dd2e25779a6e473bbd4d4a266e0220fa52bb3f4bf8d03e0002f` | Handler attack and Spyro reactions, flame defeat, separate E6 bird, five-gem reward |

The B8 candidate uses original Dry Canyon row 51 at Dark Hollow physical row
128. Its origin matches the destination floor at raw Z 17152. The defeat capture
contains parent state 6 and child E6 at physical row 131/state 4. The final
capture has parent state 253, native checkpoint/durable flags, global gems 5 and
level gems 5. The transient black transition earlier in this run was not
instrumented as a lifecycle event; no death/respawn count claim is made.

Each runtime folder contains an interaction or body-color receipt and native
screenshots. Card reload, postcollection death/reentry, every variant and every
destination are separate pending checks. E2's earlier r1 run was invalidated by
a changed fixture; dragon shine-only r2 did not recolor the body. Those failed
receipts remain preserved and are not acceptance.

## Verification and remaining work

`full-release-regression-r2.log` records 527 passing build/source-free suite
entries and one failed optional offline-suite entry. The latter initially lacked
checkout-local generated caches. After generating all 35 caches and a separate
clean source copy, the older V5 locked-base terrain parity test failed because
its modified executable does not satisfy the exact-original authored surface
certificate. The failing call and guard are unchanged from the starting commit.
No source guard was weakened to make this older route pass.

All 68 current registered source suites pass. `source-combined-r1.json` joins
`source-registered-r1/` with the nine remaining cases in `source-registered-r2/`;
the first run exhausted its total time budget. The authoring case initially
lacked its exact original-first fixture. Restoring the preserved fixture with
SHA-256 `e04c7a6c17c3800bd1e53225bcca8f1d6c3cc32a890428ba0b8dc2021510df31`
and rerunning its registered command passed 10,348 checks. All seven registered
Python source audits also pass. Original failure logs remain preserved. These
passes do not resolve the separate legacy offline-suite failure. The regression
runner's own 115 tests passed. The native dragon tint interpreter passes 856 cases. The protected
project check compares all 173 files with the prior snapshot and finds no change.

Remaining Universal Moby families include Magic Crafters selector/state/pod
closure, Flight selector arithmetic and native course context, other source
class reads and model-less support markers. Flight objective counters and time
bonuses require their original course context; admitting a spawn argument alone
does not establish gameplay or completion. Resource-capacity refusals remain
distinct from missing behavior support. The historical all-cell coverage matrix
has not yet been regenerated on this final integrated source.

Original discs, user projects, cards, previous packages and all failed research
fixtures remain preserved. Fly Anywhere remains deferred. No public release,
Mac notarization or physical Windows acceptance is asserted by this checkpoint.

## Next fixes discovered during verification

Private research has reproduced a Peace Keepers particle insertion overwriting
owned zero bytes at the end of its sound bank. A bounded padding fix passes
1,856 synthetic checks and 4,828 checks across all 35 source levels; it is not
yet part of this checkpoint. Forty previous sound-corruption cases now reach
the actual RAM-capacity check in the private build.

The historical Flight matrix also contains 1,912 cross-level compositions that
copied Flight behavior without proving course dependencies when class IDs were
unchanged. They are unproven and must not be counted as gameplay support. A
private typed refusal closes that gap while retaining native same-level actors;
a positive Flight course adapter remains in progress.

A private Haunted Towers wizard into Peace Keepers candidate
`7fee851041caa332b6da368e325222e490ffc511c21a838a7d406fbfcf377139`
loaded but moved far from its authored placement toward untranslated source
patrol paths. Its armor indices also need companion enrollment and remapping.
This is a preserved runtime failure, not acceptance. A separate Firework chest
fixture is still being investigated and has no runtime acceptance yet.
