# Portal routing and textured terrain release checkpoint

This continuation adds saved portal destinations and per-level Return Home routes
in Game Settings > Portals & Return Home. Choose a physical doorway, choose a
level by name/ID, and Apply. The default checkbox pairs that level's Return Home
with the selected doorway. Each level can also choose its own explicit return
or restore the original game route. Undo and the previous route document are
retained. Normal Create BIN includes route-only projects and compositions with
retail edits, replacements, fresh65, imported objects, signs and eggs.

The original doorway identity is separate from the destination. Moving the
portal set moves its trigger, lettering and two-node fly-out path; Return Home
matches that original token and uses the moved path. The stone arch is terrain
and is moved separately. Duplicate destinations have one explicitly selected
return doorway. The implementation validates the original USA revision and
native field owners, stage ordering, all live Return Home handlers, and final
EXE/WAD readback. Unsupported or over-capacity combinations refuse before
publication. The current full Twilight Harbor return fixture exceeds the
required native buffer reserve by 400 bytes and remains refused.

The private app also now repairs missing/corrupt native terrain materials at
ordinary level load. Original textures and complete editable terrain coexist.
See [the texture recovery report](terrain-texture-cache-recovery-20261001.md).

## Verification

Task-root evidence is outside this checkout at
`../../work/universal-gameplay-20261001/`. Fixture evidence below is inside the
checkout at `work/universal-gameplay-20261001/fixtures/`.

- Release regression: 340 checks passed, zero failed, including the later
  arrival-height fix. Task-root `release-regression-after-arrival-summary.json` retains every
  command/log path. Portal UI: 18 checks passed in both Release and Debug.
- Native portal source validation: 9,440 checks in both configurations, including
  3,276 source-free cases. Delayed scalar initializer tests prove independent
  Town Square and fresh65 update/copy routing. The live imported handler test
  refuses a corrupted remapped class even with a valid dormant native handler.
- Source/store check: 321 checks; stable exclusive writer lock, stale-writer
  refusal, previous-version preservation. This is not a crash-durability claim.
- Production near-portal fixture readback: 1,993 checks, plus 683 owner-matrix
  checks and 361 fresh integration checks. These are source/readback evidence.
- Actual private editor: original Artisans materials rebuilt automatically;
  5,199 faces retained; face3120 raised32 and undone to zero edits. Portal
  destination14 plus paired return saved, and ordinary Create BIN succeeded for
  Artisans+Toasty with two route patches and zero other patch counts.
  `portal-gui/texture-and-portal-ui-receipt.json` identifies the UI export hash.
- Native emulator R2: cold New Game, moved Stone Hill doorway enters Toasty14;
  placed native Return Home vortex and pause Exit Level both return to
  Artisans10 via route2/token11. Returned player [93400,45356,7129]. The two
  moved fly-out nodes are [99328,51948,9257] and [101980,54886,9216].
  `runtime/portal-r2-roundtrip-receipt.json`.
- Native emulator R3: the same moved doorway enters Wizard Peak33; ordinary
  pause Exit Level returns to Artisans10 via route2, with playable landing
  [93401,45357,7129]. `runtime/portal-r3-cross-homeworld-receipt.json`.
- Native emulator R4: the doorway enters Sunny Flight15. Selecting No on the
  failed-flight results returns to Artisans10 via route2 and playable landing
  [93401,45358,7129]. This proves the failed-flight return, not completion of
  the flight challenge. `runtime/portal-r4-flight-receipt.json`.

## Fresh-level arrival investigation

The initial fresh65 candidate passed production readback but failed its first
portal arrival. R6 recorded ordinary transition state1 (not the separate
balloon state12), arrival [8960,8336,8192], a fall, and a later safe respawn.
The source-owned entry-height table retained unused65's flight value -1024;
the portal-only consumer added this to the correct scene spawnZ9216, placing
the arrival below floorZ8704. New Game does not use that adjustment.

The bounded correction changes only table slot35 to normal height8192, checks
the complete table complement and its native consumers, and preserves every
retail slot and the balloon/state12 path. R02 production readback proves all
256 WAD entries unchanged and only that four-byte EXE value changed. The
delayed scalar suite adds 1,221 checks, including all35 retail levels plus65,
eight headings, ordinary entry and New Game.

R7 verifies the corrected first height17408, but also exposes the original
128-unit starter floor's inadequate landing area: Spyro crosses floor height
about153 units behind the start. The small-floor candidate remains failed;
its subsequent respawn and successful Return Home do not count as safe arrival.
The traces are `runtime/portal-r6-transition.jsonl` and
`runtime/portal-r7-transition.jsonl`. The failed candidates and their cards are
preserved separately from the corrected candidate below.

R8 resolves the observed arrival failure with the new 384-by-384 starter floor,
bounds368..752 atZ544, while retaining spawn [8960,8960,9216], heading64 and the
same imported Return Home. The full firmware cold run entered65 from the moved
Stone Hill doorway, landed [8693,9517,9062], and remained playable. The 547-sample
read-only trace retains four lives throughout and observes states0/1/9 only,
with no death state. A normal jump into Return Home returned to Artisans10,
route2, landing [93400,45355,7130]. Exact BIN SHA256:
`1c263e0e3ce34833ea6795e0a34cf811cd16549a274e73381694112f92638a15`.
See `runtime/portal-r8-fresh-roundtrip-receipt.json` and its screenshots.

New Level and Start Blank now create that wider removable floor. Saved scenes
are never resized. Rebuilt retail levels and portal-accessible fresh levels
require continuous 384-wide floor coverage centered on the spawn before
export. The check uses exact polygon subtraction rather than point sampling;
the original footprint-only check remains for fresh New Game-only exports.
The area requirement is conservative product policy, not a proof of every
native heading, entry height or obstacle layout. See
[the landing-area basis and limits](native-portal-authored-landing-area-20261001.md).

Runtime uses the isolated DuckStation bundle, full USA firmware, Software
renderer and Recompiler. Each cold run has its own blank card; no save states
or runtime RAM writes were used. Sound was muted. This report does not claim
memory-card reload, every route permutation, all imported-object gameplay, or
Windows runtime acceptance. Runtime fixture exports and the GUI export retain
separate hashes and are not presented as the same disc.

## Broader release work

The new Gnorc lookup ownership proof removes the first blocker for 396 matrix
cells, but linked-parent ownership remains and no new successful universal
composition or gameplay coverage is claimed from it. The prior full matrix
remains 105,559/145,285 composed cells: 39,215 static-only and 66,344 with
unverified behavior dependencies. Universal gameplay is unfinished.

Start Blank, physical removal, terrain export and existing collected-state
proofs are retained from V1.0.2 and V1.0.1. Clear support still covers ten retail
levels; twenty shorter initializers remain guarded. Cleared Town Square plus
fresh65 remains refused, independently of the new portal initializer routing.
Fly Anywhere is deferred. Original discs, protected user projects, cards and
prior packages remain separate from these private verification artifacts.
