# Spyro Editor 1.2 — owner feedback on 1.1 — 5 October 2026

Version 1.2 (internal `1.2.0`; Claude; same branch `release/release-ready-20261004`)
answers four points the owner raised after using 1.1: "Find space for this
object…" got stuck; Fly Anywhere should be per level; editing several terrain
tiles gave errors for all of them; and there was no way to change a tile's type
(water to grass). Worklist section E in task-root
`work/release-ready-20261004/WORKLIST.md` carries the evidence paths.

Find space. The search reports which version and option it is trying, lists each
way to make room as soon as it is found, has a Stop button, and is bounded (6
versions × 48 options). When no combination of original objects would make room
it says so (the owner's Artisans has 11 added and 9 removed objects; that is the
case there) instead of searching for minutes.

Terrain. Four limits of the game's level format caused the errors: tiles the game
has no collision under (23% of Artisans faces) cannot be changed at all; corners
of one collision triangle may differ by at most 255; ground needs 64 units under
other ground; and moving ground across a 256-unit collision lookup cell overflows
the level's fixed lookup table. A fifth, Lower, was impossible for 31% of tiles
because a sector's stored origin equals its lowest tile. The editor now refuses
the first four at the moment of the edit in plain words (Raise, Lower, drags,
corner moves and brush strokes; a brush leaves the offending corners where they
are), moves shared corners together as the game does, and Create BIN re-bases a
sector's origin so Lower works. Emulator: Artisans tile 80:7:hp lowered 384→288,
Spyro stands on it at the new height (310 vs 407 on an unchanged 384 tile), 600
stable frames.

Tile Type…. A dialog in the Tiles card lists solid ground and the level's own
kinds (water hurts/deadly, lava, ooze, supercharge, invisible wall, electric
floor) and applies the choice to the selected tiles through the native
surface-flag patch texture painting already used; tiles cut along the other
diagonal now match. Emulator: an Artisans pond tile made solid was stood on
without harm; a grass tile made water put Spyro in the water state and cost a
life.

Fly Anywhere per level. A level-set executable: the rewritten animation loader
fills the flight animation slots only for the chosen levels (a 56-bit mask of
level IDs, plus Gnasty's Loot and the level of your own), and the five entry
windows and the permission rule read "slot 33 present" from the player's
animation table instead of "any normal level". Ceiling and pitch windows are the
whole-game ones. No new bytes, code cave or RAM field. The CPU oracle covers the
entry, permission and loader-classification windows for several level sets
(530 493 checks with the existing global cases). Emulator on a game with
Artisans and Town Square chosen: flight in Artisans (state 32, climb, dive,
landing, 600 stable frames); through the Stone Hill portal a second ✕ only
glides (state 15); through the Toasty portal likewise. A first version of the
level-set loader let Stone Hill inherit the resident flight slot a chosen level
had filled; the emulator caught it and the loader now clears the flight slots of
unlisted levels (oracle extended to the slot loop).

Packages: task-root `outputs/SpyroEditor-V1.2-release`, built from `6213733`
(macOS zip SHA-256 `e1a03758…f42fb3`, Windows zip `6941e6e8…327e92`; full sums in
`SHA256SUMS.txt`), Developer ID signed, not notarized; release verifier passes.
Checks on the final tree: 18/18 UI modes, the four new UI tests, regression gate
352/352, flight oracle 530 679 checks; the packaged app was opened through
LaunchServices with real mouse input (Tile Type…, the 64-unit refusal, Choose
levels).

Not verified: see 1.1 below; in addition, Tile Type… and the sector re-base were
played in Artisans only; per-level flight in the two chosen levels and two
unchosen ones only.

# Spyro Editor 1.1 release-readiness pass — 5 October 2026

Version 1.1 (internal `1.1.0`; Claude; branch `release/release-ready-20261004`,
started from 1.0.10) is a whole-editor pass: interface, bugs, unfinished
features, verification and packaging. The packages were built from `3353dd6`
and are in task-root `outputs/SpyroEditor-V1.1-release` together with the quick
start, release notes, playtest checklist, checksums and `ENGINEERING-REPORT.md`,
which holds every verification result. The worklist with each finding is
task-root `work/release-ready-20261004/WORKLIST.md`. It is a release candidate
for the owner's hands-on review, not a public release.

Interface. First launch shows a welcome with one **Open Game…** action and no
tools until a game is open; the editor reopens the last level. Objects and
Terrain are grouped into cards. Addresses, class numbers, slot names and
research wording are gone from release mode, including the status bar (a plain
wording layer; Details keeps the working text). There is one Save and one
Create BIN; **Show Game File** reveals the exported CUE; **Export Check**
replaces Build Safety and runs the same terrain check Create BIN uses. Delete,
Undo and Redo work in the view and never while typing in a field. A click never
moves an object (drags have a dead zone), nothing stays selected after a
removal, and the view does not jump. New Level asks before replacing a created
level and keeps a backup. Help is rewritten. Checked at 1320x892 and 1024x700.

Bugs fixed. Add object / egg / sign blocked by a terrain export error; a red bar
explains terrain the game cannot stand on, selects it and undoes it. Create BIN
no longer leaves 1.3–2.6 GB working copies per run, checks free disk space
before it starts, and a failed export leaves the last good BIN/CUE
byte-identical. Two export failures on the owner's project copy (moved portal
with terrain changes; changed gem totals with terrain changes) are fixed. A
painted tile wider than 255 units (the starting floor of a built level) can be
exported. When the project cannot be prepared at start the app explains it in a
window instead of ending silently. Six stale UI tests and two stale regression
pins were repaired.

Fly Anywhere ships as one saved whole-game switch, off by default. A crash found
in the emulator (entering a portal while flight was allowed called into the
level overlay being replaced) is fixed by withdrawing the permission whenever
the game is not in normal gameplay. Played: Artisans, Stone Hill and Toasty
through portals, Doctor Shemp (demo), a level built in the editor, Exit Level
while flying, death and respawn while flying; the Icy Flight demo is
frame-identical on and off; the OFF export is byte-identical to a project that
never used the option. The other 26 normal levels (Gnasty's Loot among them)
were not played with it.

Verification. Regression gate 352/352 on `3353dd6`; 18 UI modes and the export
UI tests pass; the release verifier passes in un-notarized mode. The final Mac
package was unzipped and driven through LaunchServices with real mouse and
keyboard input on an empty profile, a project it created itself and a copy of
the owner's project. Three images exported by the final package were played in
an isolated emulator: added teal gem and flight; eggs, signs, save at the fairy,
cold restart and Load Game; New Game in the built level with flight, death and
respawn. The cross-level coverage matrix (209 objects x 35 levels) measures
composition, not behaviour.

Not verified: the Windows package was built and structure-checked but never
run; the Mac package is Developer ID signed but not notarized; red and pink
dragonfly eggs were not seen in game in this pass; Start Blank was played in
Stone Hill only; no whole-campaign playthrough; universal object behaviour
beyond the documented runtime checks. The default no-argument UiSmoke and the
later stages of `--fresh-main-ui-only` are stale tests outside the gate.
Original discs, the owner's project, settings, saves and earlier packages are
untouched. On 5 October about 1 TB of regenerable test output (old test game
images and compiler output in task-root `work/`) was deleted at the owner's
request; lists are in `work/release-ready-20261004/cleanup/`.

# Terrain brush stays in the Terrain tab — 4 October 2026

Version 1.0.10 (Claude) turns the terrain brush and texture painting off when the
editor leaves the Terrain tab and ignores the brush keys 1–5 outside it, so clicks
in Objects always select objects. The editor-navigation UI test covers it (54
checks). Regression is unchanged: 349 of 351 cases pass, with the same two
failures as 1.0.8. Evidence is in task-root `work/terrain-brush-tab-20261004`.

# Inventory column and terrain editing — 4 October 2026

Version 1.0.9 (Claude; product source `f495eb7`) gives every inventory row
one text size, replaces the top egg picture and K/Y total with a spinning gold
dragonfly egg above the egg column, and fixes terrain editing: multi-tile removal
(Cmd/Ctrl-click or drag), a whole-tile brush, alias-safe export of shared corners,
ignored click drift, a drag dead zone and a fold guard for corner drags. The gold
egg is a HUD-format model converted from the Japanese egg and stored in unused
libgpu code at `0x8005EC04`; it is installed into model slot `0x1D2` only when the
slot is empty. Evidence is in task-root `work/inventory-terrain-20261004`
(inventory screenshots, exported Toasty eggs in an isolated emulator, logs).
Regression: 349 of 351 cases pass; the two failures and the runner self-test fail
identically on a clean `f1ab4d1`. Packages are community (ad-hoc) signed and pass
package verification with host provenance allowed; the Mac app launches.

# Removal-plan inspector controls — 4 October 2026

Version 1.0.8 adds double-click navigation and a Remove object action to the
removal-plan inspector. Removal uses the normal editor path, preserves camera
position and remains recoverable with Recovery > Undo last removal. Modeless
inspectors refresh their rows and reject actions after the scene changes.
Evidence is in task-root `work/removal-inspector-20261004`. No gameplay or
export-admission changes are included. All 30 workflow checks pass, including
normal removal, undo, camera preservation and stale-scene protection. The
packaged Mac app also verified double-click selection of T31 from the checked
six-object plan. Both Mac and Windows packages pass package verification.
Product source is `d74cefa`; package hashes and evidence are recorded in
`work/removal-inspector-20261004/final-receipt.json`.

# Object picker responsiveness fix — 4 October 2026

Version 1.0.7 moves selected-object and source-version fit calculations off the
Avalonia UI thread. This removes the synchronous wait on the private-terrain
async preparation path. Shared background checks are bounded to two workers;
queued checks cancel on close, and stale results cannot enable a different
selection. Export composition and gameplay admission rules are unchanged.

Focused actual-window headless verification covers cold selection, dispatcher
responsiveness, rapid source changes, stale results, search, close/reopen and
real gem capacity checks. Evidence is in task-root
`work/object-picker-freeze-20261004`. Package and native GUI results are recorded
there after building. The 18 responsiveness checks and 25 Magic Artisans
capacity checks pass. Both release packages pass their verification, and the
signed Mac app was exercised through search, object/source selection, normal
Add and Save in a private project (177 objects, treasure 350). Product source
is `24d5595`; ZIP hashes and the saved-edit receipt are in
`work/object-picker-freeze-20261004/final-receipt.json`. Mac notarization and
Windows execution remain unverified. Saved user edits and previous packages
are preserved. Universal gameplay remains incomplete.

# Local 1.0.6 playable checkpoint — 3 October 2026

Scope expansion stopped at the user's request to provide a usable release and
conserve credits. This stable-channel package retains the verified editor and
guarded adapters. Universal gameplay is incomplete; the unfinished Alpine
group-one renderer remains inert, with every admission flag false. No public
upload or notarization is performed. Original projects, discs and cards are preserved.
Private continuation notes are under `work/universal-finish-20261002/green-group1-frame-guard-r1/CONTINUATION-STOP-20261003.md` and `green-group1-modal-callback-independent-r1/CONTINUATION.md` in the task root.

Packages are in task-root `outputs/SpyroEditor-V1.0.6-release`, built from
clean product source `1f64331`. Mac Developer ID signing and both platform
package checks pass; notarization and Windows execution remain unverified.
The final packaged Mac app opened a copied project, loaded textured Artisans,
saved its edits and exported BIN/CUE successfully with zero skipped edits.
The resulting661547040-byte BIN SHA256 is
`be08b8ea0a9766067bdf56764edeb1b0196d294629b2b24a991f6242f0bfb556`,
byte-identical to the previously verified high-value-gem export. Terrain tools
and command controls remained available afterwards. Original USA hash still
matches; test app and emulator closed without resume states. No further research
workers are running. Exact hashes/evidence are in
`work/universal-finish-20261002/package-1.0.6-final-receipt.json`.

---

# In progress: complete Alpine6/52 terrain dependencies

The original group1 now has immutable source, complete-world storage and typed
renderer-queue prerequisites. All six sectors, nine animation tracks, 427 collision
owners, both visibility contexts and both original actor rows are retained. Root
validation passed 1,229 source checks, 220 source-native controls, 66 translated
controls, 3,099 storage checks with 39 refusal cases, 1,333 emitted-world controls,
39 queue checks with 31 refusal cases, 6,513 emitted renderer controls and nine full
scene-loader controls. The Release App builds with zero warnings/errors.

The exact actor/resource installation stage now also passes 72 source checks
with 62 refusal cases, 1,333 current-world controls, 145 initializer checks,
1,152 type-8 checks, 6,513 emitted-renderer controls, nine loaded-scene checks
and all 200 original fallback RNG/health branches. It retains both complete
actors, all native resources, the original 91 dynamic slots and a real scene
reservation for the renderer queues. The Release App builds cleanly.

The complete normal-frame replacement now also passes 88 source/refusal checks,
37,912 packet-boundary checks, 288 complete-frame comparisons and 30 native
factory/bootstrap controls against actual root-built output. It retains every
resource and fits the unchanged overlay ceiling with 34,816 bytes remaining.
The Release App build remains clean. Mode-12 loading, modal rendering and
hardware verification remain open; all admission flags remain false.
[Complete-frame prerequisite](docs/research/green-group1-complete-frame-prerequisite-20261003.md).

These stages remain inert. Normal GPU packet capacity and final export admission
are still being implemented; this checkpoint does not enroll group1, produce its
ordinary candidate or claim gameplay. Existing export guards remain.
[Installation contract](docs/universal-green-group1-inert-installation.md).
[Queue contract](docs/green-group1-terrain-queues-contract.md).

---

# Fixed: Magic pair exports after player-model alignment

Fresh verification caught stale complete-asset certificates in the Magic12/13
Artisans and Dark Hollow routes. Both now authenticate the aligned model layout.
Artisans rotates all33 whole packages without moving padding between them; all
source model alignments, payloads, six-removal scope and allocations remain.
The retained dragon streams still have36,864 bytes of spare reservation.
Current Artisans source3302, saved export/readback284, native rescue replay128
and Dark Hollow8791 checks pass. Release App builds without warnings/errors.
Current Artisans gameplay now verifies both charge defeats,2+5 gems,Nestor rescue,
normal card save and cold-process Load Game with inventory7/94 and1/4.
Other changed export identities still need fresh native gameplay.
[Evidence](docs/research/magic-pair-model-alignment-20261003.md).

---

# Follow-up: complete linked Alpine Druid and wizard world

Original11/44/53 now composes as one complete group into authored empty Peace
Keepers, retaining six whole sectors, two moving groups,183 source colliders,
paths, reciprocal links, models and native effects. Ordinary export313/6 refusals,
arena2086, actor/resource92, emitted342 and independent world controls pass;
the existing ramp remains byte-identical and the Release App builds cleanly.
Native testing found and fixed a16-slot allocation that blocked original fragment
effects: this group now allocates the original91 slots without changing its
native reserve guards. The corrected ordinary disc now has full-firmware native
evidence for linked reset, moving terrain, projectile damage and fragments,
both flame defeats, red1+red1 collection, inventory2/2, two ordinary restarts and
the Druid's repeat-defeat0F orb without duplicate treasure.1063 loaded-world and
176 complete loaded-model checks pass; the closed run has no invalid-memory/panic
entries and its private blank card is unchanged. Card persistence, level reentry,
audio and other hosts remain open. This does not finish
universal gameplay or issue V1.0. [Evidence](docs/research/universal-green-linked-world-20261003.md).

---

# Follow-up: original temporary Superflame fairy

The original Haunted fairy82 now composes with wizard21/armor1 in intact Dark Hollow, retaining its full path, kiss effects, temporary power, sounds and complete powered-flame texture page. Root source3909, complete assets648584, native instruction499757, cleared6048 and treasure2613 checks pass; Release App builds cleanly. The fresh native arrival diagnostic now confirms the fairy kiss/power grant, rendered powered flame, all six armor fragments, yellow10 and green2 pickups, durable live bits and inventory12/112 with zero invalid reads. Card persistence, repeat defeat and other destinations remain open. [Evidence](docs/research/universal-haunted-darkhollow-superflame-20261003.md).

---

# Fixed: imported player animations retain later model alignment

The shared animation importer now preserves every later native model root’s original alignment using an authenticated four-byte gap after the exact animation terminator when needed. Root source763, cleared Haunted10925 and actual native parser22019 checks pass. Complete animation and model payloads are retained. Affected ordinary export identities change and their gameplay evidence must be renewed. [Evidence](docs/research/player-animation-model-alignment-20261003.md).

---

# Follow-up: original Green Druid ramp group

Alpine18/49 now exports its complete original group2 ramp, both geometry and collision keyframes, four-node path and linked target into an authored cleared Peace Keepers level. Root export/readback9254, native frame2155, effect1162, route30 and runner131 checks pass; the Release App builds cleanly. The completed ordinary native run confirms spell emission, raised/lowered collision, player carry, flame defeat/blue5 pickup, death persistence, repeat-defeat0F orb pickup and inventory5/5 without invalid reads. Card persistence, audio and wider universal coverage remain open. [Evidence](docs/research/universal-green-druid-ramp-world-20261003.md).

---

# Fixed: native replacement resources in fresh and cleared levels

Fresh and cleared exports now preserve the complete original0E/0F/10 replacement
models, texture ownership and native model read-ahead alignment. The rebuilt
ordinary Green15/137 export cold-boots, awards its first gem, retains it through
normal death, and completes the subsequent flame defeat without the former
crash. The original0F replacement orb renders and is collected; its counter
advances0→1 while native inventory stays1/1. No invalid memory reads occurred.
Source9358, cleared export/batch6048, Fresh652613 and affected regressions pass.
Native0E/10 rendering, card persistence and wider universal gameplay remain open.
[Fix and bounded native evidence](docs/research/universal-green-second-world-20261003.md).

---

# Follow-up: two original Green Druid terrain controllers

Alpine16/group5 and17/group7 now export their complete original shrinking terrain
and paths while retaining target−1. They require one original actor each and
create no dummy target. Integrated saved/reopened export1594, native instruction
checks8834, paired regressions/byte identity and runner128 pass; the Release App
builds cleanly. Fresh row16 full-firmware gameplay now verifies flame defeat,
red1/inventory1/1, ordinary death persistence, repeat0F orb without duplicate
treasure and normal jump/glide landing on the complete expanded cube. Independent
saved-world/model/collider1122 and transition/contact270 checks pass. Its closed
run preserves the original disc/private card and has no invalid-memory/panic
entries. The corrected row17 example now also cold-boots through normal New Game,
preserves the original patrol floor, awards red1/inventory1/1, retains collected
progress through ordinary death and gives a repeat0F orb without duplicate
treasure. Seven saved captures pass491 complete world/model/collider checks.
Its closed run preserves the original disc/private card with no invalid-memory
or panic entries. Current row17 cube contact, live carry, card save/load and
other hosts remain pending.
[Evidence](docs/research/universal-green-no-target-world-20261003.md).

---

# Follow-up: Haunted resurrection in Dark Hollow

Original Haunted wizard21 and armor1 now compose/export into intact Dark Hollow
with native resurrection links, full source assets and all five authenticated
effect dependencies. Saved/reopened source checks2019, asset checks608984, actual
native instruction checks623470/1055 controls, existing Haunted regressions and
runner128 pass; the Release App builds cleanly. An arrival-only native diagnostic
now verifies resurrection, animated armor activation, normal wizard flame defeat,
green2 collection and inventory2/112. Powered armor defeat and persistence remain open.
[Evidence](docs/research/universal-haunted-darkhollow-20261003.md).

---

# Follow-up: second Green Druid moving-world group

Original Alpine15/137 now exports with its complete moving cube, textures,
collision, paths, native group6 controller and linked target into empty authored
Peace Keepers. Real saved/reopened export and readback1174, native instruction
replay2546, existing Green/Alpine/High Caves regressions and runner123 pass. The
existing5/48 output remains byte-identical; the Release App builds cleanly.
First pickup and terrain gameplay have native evidence; the replacement-resource
fix above additionally verifies repeat defeat and orb pickup. Its original−1
target cache is preserved. [Evidence](docs/research/universal-green-second-world-20261003.md).

---

# Follow-up: second Alpine linked pair

Original Alpine3/4 now place together into intact Blowhard through the ordinary
editor workflow. Both reciprocal native indices, selectors2/5, complete paths,
models, sounds and native behavior remain intact. Project save/reopen and normal
export/readback pass839 checks; native selector5 instruction replay38 and runner120
checks pass. Existing Alpine, Artisans rescue and Green Druid regressions pass;
the Release App builds with zero warnings/errors. Remaining unproved Alpine
groups now refuse instead of exporting unresolved native actor references.
Fresh full-firmware gameplay now confirms both linked actors, flame defeat,
blue5/green2 collection and durable identities after normal death/respawn. Native
inventory17/407 includes the imported7 and original10 collected along the route.
Card save/load, arm attacks and other hosts remain open. This does not finish
universal coverage or issue a V1.0 package. [Evidence](docs/research/universal-alpine-pair34-20261003.md).

---

# Follow-up: native dragon rescue capacity and Artisans imports

The native rescue-loader crash is fixed for the supported complete Alpine0/1
Artisans group. The first candidate's model order left too little temporary
space for dragon dialogue; the retail copy length underflowed and overwrote
native code. Complete models are now reordered without increasing allocation.
Fresh full-firmware gameplay completes Nestor's rescue, restores the world and
imported models, and confirms the armored enemy's blue5 reward with native
inventory5/102 and dragon1/4. A separate ordinary-export lower-field placement,
tested with only its initial player XYZ shortened, now confirms both enemies:
native flame/blue5, jump-charge/yellow10, both saved collection identities and
inventory15/102. Respawn/card persistence and the other retained rescues remain open.
The closed run has no invalid-memory/panic log entries or save-state/RAM writes.
[Evidence](docs/research/universal-alpine-artisans-20261003.md).

A final export guard now checks retained dragon rescue reservations across all28
original hosts with those streams. The Magic12/13 Artisans pair also has a complete
model-order correction: same six Gnorc removals and allocation, all payloads
preserved, with36,864bytes above the largest aligned rescue requirement. Root
source3268, normal saved export/readback284, actual native instruction replay128,
source-wide guard2029 and runner118 checks pass. Fresh gameplay of this revised
Magic candidate and all retained dragon dialogues remain pending.
[Technical evidence](docs/research/universal-dragon-rescue-capacity-20261003.md).

The editor now offers the exact named46-object Alpine removal plan through the
normal export check and manual review. It explicitly warns that removing the
balloonist and balloon disables balloon travel. No objects are auto-deleted. Registered chooser source/API checks57 pass.
The Release App builds with zero warnings/errors. Universal gameplay and V1.0
remain unfinished.

---

# Follow-up: Green Druid and original moving terrain

The complete original Alpine5/48 group now exports into empty authored Peace
Keepers with its original moving sector, fixed and animated collision, materials,
paths, spell particles and native callbacks. Integrated checks907, candidate
readback279 and affected regressions pass; the Release App builds cleanly. The
root-rebuilt disc matches the independent candidate exactly. A spawn-only diagnostic now confirms native platform animation/carry, jumping,
spell8 objects targeting the linked terrain, charge defeat, collected green2 and
inventory2/2. Normal death/respawn retains the total and restores the enemy with
its reward disabled. Close spell visual confirmation, card persistence, audio,
editor group movement and other hosts remain open. [Evidence](docs/research/universal-green-druid-world-20261003.md).

---

# Follow-up: Alpine Ridge armored druid gameplay

The complete original Alpine0/1 group now uses ordinary placement and export into
intact Blowhard, with its real dynamic arms, sounds, paths, links and native combat.
Integrated export/source/reward tests and affected regressions pass; the App builds
cleanly. Fresh full-firmware gameplay confirms arm movement/expiry, slam damage,
flame and charge defeats, five-plus-ten gem rewards and inventory15/415. After a
native death, both enemies give normal replacement objects and no duplicate gems.
The second complete24/25/26 group also passes1,425 export checks and fresh native
arm/phase/flame checks. A corrected common placement now confirms visible yellow10
and blue5 collection, both durable flags, native death/respawn retention and
inventory15/416. The elevated caster's red1 and complete16-gem run remain pending.
Card persistence, close arm contact, audio and other hosts remain open.
[Evidence](docs/research/universal-alpine-arm-groups-20261003.md). Universal gameplay
and the V1.0 release remain unfinished.

---

# Follow-up: High Caves gameplay and complete moving terrain

The two-actor High Caves example now passes bounded native testing: linked tornado
handoff, damage/reaction, flame defeats, five-plus-ten gem rewards and inventory
15/15. Both new collection flags are set. Card persistence, separate spell-particle
visual confirmation and other hosts/variants remain open.

One complete four-object moving-terrain group now exports into an empty authored
Peace Keepers level with its original world, animation banks, collision and effects.
Integrated1,039 checks, affected regressions, runner115 and Release App build pass;
the independently rebuilt candidate matches exactly. A separate spawn-only native
diagnostic now confirms animated-floor carry, jumping/landing, visible type8 spells,
controller flame defeat and five-gem collection with inventory5/20. Full-world
cohort completion, postcollection respawn/card persistence and other hosts remain open. [Evidence](docs/research/universal-high-caves-20261003.md).

---

# Follow-up: Twilight soldiers native gameplay

Two imported Twilight Harbor soldiers in original Dark Hollow now have bounded
native gameplay evidence: visible bodies and attack objects, damage/respawn,
defeated rows, five-plus-two gem rewards, collection sparkles and native inventory
7/107. Both new collection identities are set. The closed run uses a private
blank card with no save states or runtime writes. Postcollection respawn,
card persistence, sound listening and other variants/hosts remain unverified.
[Evidence](docs/research/universal-twilight-troopers-20261003.md).

---

# Follow-up: remaining original Magic Crafters druids

Six more singleton druids and the second linked pair now use ordinary placement
and export into original Dark Hollow, retaining their native behavior, paths and
actual gem rewards. The new 5,057 checks and affected pair/chain/High Caves
regressions pass; 42 compiled data/code comparisons match the independent
delivery and the Release App builds cleanly. Native acceptance for these new
variants remains pending. [Evidence](docs/research/universal-magic-originals-20261003.md).

---

# Follow-up: High Caves linked creatures and native spell effects

The first two High Caves groups now preserve both directions of creature links,
complete translated paths, native spell effect 5 and Spyro reaction 28. Integrated
placement 4,701, particle 663 and animation 497 checks pass, together with affected
Magic/fresh-level regressions and the Release App build. The rebuilt candidate
matches the independently verified disc exactly. Subsequent native and moving-world
evidence is recorded in the newer follow-up above; genuine capacity failures remain restricted.
[Evidence](docs/research/universal-high-caves-20261003.md).

---

# Follow-up: four linked Magic Crafters druids

Original rows 2–5 now expand and export as one linked group into unchanged Dark
Hollow, preserving native paths, pod, shared state and collection identities.
The integrated 5,749 checks and existing pair/Twilight regressions pass; the App
builds cleanly. Native gameplay for this chain remains pending.
[Evidence](docs/research/universal-magic-chain-20261003.md).

---

# Follow-up: imported effects in fresh levels

Fresh level 65 now retains imported attack effects through normal export and
portal routing while preserving Town Square's original effect callbacks.
Integrated binding/export and regression checks pass; the Release App builds
cleanly. Native testing shows the linked Dark Passage creature and lantern,
visible attack effects, health loss, charge defeat, five-gem collection and six
rendered collection stars. Fresh save persistence and routed-vortex gameplay
remain unverified. [Evidence](docs/research/universal-fresh-particle-effects-20261003.md).

---

# Follow-up: Artisans druid capacity recovery and gameplay

The editor now explains the exact six Gnorcs to remove for the supported Magic
Crafters pair, checks actual export capacity, and supports the normal manual
Remove/save/reopen/Add workflow. The rebuilt export reclaims132KB of model
storage and2.1KB of texture space, with86.6KB RAM headroom after import.
Source2,887, UI25, saved-export284, Dark Hollow8,753 and runner115 checks pass;
the UI/App builds cleanly.

Native Artisans testing shows both imported druids moving/attacking, health loss,
flame/charge defeats, two-plus-five gem rewards and new collection identities.
A private native card save and fresh-process card reload retain all seven gems
and the two new collected identities. Remaining
variants, edited hosts and mixed groups are still explicit limits. Universal
Moby gameplay remains unfinished. [Evidence](docs/research/universal-magic-pair-20261003.md).

The enemy-free authored floor passes stationary and moving jumps without life
loss. The earlier boar-run death is not reproduced. An experimental face flag
still has visible gaps and worsens texture distortion; it is not integrated.
Terrain seam quality remains open.

---

# Follow-up: native collection stars and linked Dark gameplay

Dark Passage attack-effect ownership now covers all 35 original particle hosts.
The linked example composes in ten populated levels; the others retain explicit
RAM/texture limits. All ten supported cleared-level targets passed source
composition checks. This is broader export coverage, not 35-level gameplay
acceptance. [Evidence](docs/research/universal-dark-passage-dogs-20261002.md).

Canonical native gem collection now retains source-certified stars and resources
in all 35 original levels, with final exported-code/data checks and one native
particle update. Integrated checks pass 2,995; arbitrary copied particle-12
callers remain unverified. Twilight, Magic and Haunted regressions pass, and the
App builds cleanly. [Evidence](docs/research/universal-collection-stars-20261003.md).

The moved Dark Passage lantern/creature group now has bounded native evidence:
form changes, health loss, charge defeat, five-gem collection and both collection
flags. Projectile-specific damage, impact visuals, respawn and save persistence
remain open. [Evidence](docs/research/universal-dark-passage-dogs-20261002.md).

---

# Follow-up: Twilight soldiers integrated for Dark Hollow verification

All fourteen original Twilight Harbor soldier variants now use ordinary
placement and export into Dark Hollow. Paths, activation rectangles, dynamic
children and source-owned effects follow the imported group. Integrated checks
pass 4,431, with 189 Dark Passage and 102 source-free regression checks; the App
builds cleanly. Native gameplay testing remains pending. Other destinations and
conflicting copied effect owners remain restricted.
[Evidence and limits](docs/research/universal-twilight-troopers-20261003.md).

---

# Follow-up: Magic pair integrated; boar gameplay and terrain repair

The original Magic Crafters selector-2 pair now reaches ordinary placement and
export into untouched Dark Hollow, with translated groups and persistent editor
identities. Integrated checks pass 8,754; the App builds cleanly. The original
placement reproduces the r5 native test's scene and overlay. Other selectors,
destinations, mixed imports and modified hosts remain restricted.
[Evidence and limits](docs/research/universal-magic-pair-20261003.md).

All nine Beast Makers/Misty Bog boar variants now preserve their paths, home
coordinates, charge state and linked chicken indices after relocation. Integrated
source checks pass 7,453 class-ownership and 7,111 placement checks; the Release
App builds cleanly. A smaller textured cleared-level example passes native boar
movement, contact damage, flame defeat and collection of its five-gem reward.
The original large floor exposed a renderer queue overflow. Batching addresses
the queue budget, and corrected quad winding restores visible textures during
movement. Native testing still shows tile seams and a life lost after a jump
inside the floor; these are under investigation. Failed exports and evidence
are preserved. Universal gameplay remains unfinished.
[Evidence and limits](docs/research/universal-boar-placement-20261002.md).

Dark Passage dogs now bring both transformation models/handlers and their attack
class. Linked lantern groups and the attack-effect adapter are now integrated;
338 lantern, 189 projectile and 2,489 existing dog checks pass. Imported lanterns
change linked creatures without changing destination terrain lighting, explained
in Add Object details. The bounded r5 native result is recorded above.
[Evidence and limits](docs/research/universal-dark-passage-dogs-20261002.md).

---

# Follow-up: Haunted Towers resurrection in a cleared level

Moved wizard groups now preserve their paths, trigger rectangles, seven armor
links and metal textures. A scoped particle adapter carries the resurrection
spell into Peace Keepers, including its cleared-level workflow. All 31 source
variants and independent duplicate groups pass integrated composition checks.

Native testing in the rebuilt, textured blank level shows the wizard moving
and firing, the green resurrection spell, activation of all seven linked armor
rows, and damage from the first armor. The current exporter reproduces the
tested BIN exactly. Other destinations, defeat/rewards, persistence and other
effects remain separate work. Universal gameplay is not complete.
[Evidence and limits](docs/research/universal-haunted-wizard-20261002.md).

---

# Follow-up: exact row coverage and native enemy rewards

Coverage now composes each variant separately with its required linked actors.
The Dark Hollow audit records 4,153 static cases, including 87 expanded groups;
no result is promoted to runtime support. In native gameplay, the Terrace
Village enemy awards two gems after flame defeat, while a quarantined Magic
Crafters pair awards five plus two after charge and flame defeats. Both tests
set their collection flags and show the final HUD totals.

Terrace particle 77 is now source-certified as an existing native egg effect,
not an enemy attack arc. Collection-star semantics and Magic Crafters
collision/state ownership remain open. Universal gameplay is not complete and no new package has been released.
[Evidence and limits](docs/research/universal-coverage-and-runtime-20261002.md).

---

# Follow-up: original Flight gate replacement

The editor now offers **Replace Flight gate…** for an original course gate,
with compatible source choices, a capacity check, confirmation and recoverable
undo after reopening. Gate position and crossing plane remain fixed. Eight
same-category source/native cells compose; two others correctly exceed memory.
The integrated checks pass: 1,050 gate checks, 25 actual UI cases, 1,011 chest
regressions and 295 unsupported Flight guards. The Release App builds cleanly.

In Crystal Flight, an imported Sunny gate rendered, awarded three seconds on
native crossing, spawned all thirteen fragment classes, appeared as Arches 1/8
in results, and returned on Try Again. The ordinary UI export is identical to
the tested content; only native New Game entry selects Crystal for the test.
The UI checks also cover the supported chest's ordinary Remove/Add workflow.
[Evidence and limits](docs/research/universal-flight-gate-20261002.md).

Universal gameplay is still in progress. Full Flight completion/persistence,
other combinations, Magic Crafters ownership and Haunted Towers wizard groups
remain open. This is a source checkpoint, not a new release package.

---

# Follow-up: original Flight chest replacement

A scoped adapter now supports replacing one original Flight chest with a chest
from another Flight that uses the same objective category. All 13 source/native
control cases pass 1,011 checks; 295 unsupported Flight checks still pass. The
Release App builds cleanly. In Night Flight, an imported Icy chest rendered,
broke under flame, awarded two seconds, appeared as Chests 1/8 in results, and
returned on native Try Again. Normal export/readback passes separately.

This does not complete universal gameplay or enable Flight objectives in normal
levels. Other source/destination runtime cases, full course completion,
persistence and audio remain open. The later gate checkpoint above adds
interactive chest replacement UI checks.
[Evidence and limits](docs/research/universal-flight-chest-20261002.md).

---

# Follow-up: imported actor visibility

New imported rows now use the native unrestricted visibility sector instead of
retaining an unrelated donor terrain sector. The change passes 1,212 source
checks while leaving native destination rows unchanged. A controlled one-byte
disc comparison confirmed the invisible-chest cause; the normal exporter now
produces the exact successful image. Native Peace Keepers testing shows the
imported Firework chest's fuse, sparks, explosion, damage and five-gem reward,
with both counters and collected flags verified. Audio and card reload remain
unverified. [Evidence and limits](docs/research/universal-placement-visibility-20261002.md).

Universal gameplay remains in progress, including further Flight adapters,
Magic Crafters ownership and Haunted Towers wizard paths/armor links.

---

# Follow-up: sound preservation and Flight admission

The integrated particle insertion writer preserves owned sound bytes, including
zero-valued descriptor tails, and grows the data subfile when padding is too
small. Checks pass: 1,856 source-free cases and 4,828 checks across all 35 levels.
A typed Flight dependency refusal also closes copied behavior admission through
unchanged class IDs; 295 source checks preserve native same-level handlers and
reject unsupported foreign course objects. The editor Release build is clean.

These are correctness fixes, not completion of Universal Moby gameplay. Positive
Flight adapters, Magic Crafters ownership, Haunted Towers wizard paths/armor
links are still being worked on. Imported visibility is addressed above. See
[the follow-up evidence](docs/research/universal-sound-flight-guards-20261002.md).

---

# Universal gameplay implementation — 2 October 2026

Active branch: `release/universal-gameplay-20261002`, continued from clean
checkpoint `ada3e545759c6af7b80e30a1082b7609c6bf5509`. Implementation and native
acceptance are still in progress; previous release packages are preserved.

- The editor adds, moves, copies, removes, restores and reopens complete linked
  groups. Actual UI-to-BIN tests pass for Lofty Castle's seven-object whirlwind
  groups (33 assertions) and Gnorc Cove's linked barrel pair (21 assertions).
- Source-owned integer links, native parent reads and Gnorc's physical model
  lookup are handled separately. Source/copy mutation tests retain the class
  and namespace guards; unsupported pointers remain refused.
- All 46 original native sphere-damage calls across 23 source families now
  contribute their verified Spyro reaction animations. The integrated suite
  passes 2,117 checks, including seven real compositions. In Dark Hollow, the
  Gnorc hatch/TNT candidate passed native explosion, hurt animation 29 and
  recovery. The current scoped hatch export is byte-identical to that tested
  candidate. This is bounded interaction evidence, not whole-family acceptance.
- Cliff Town's E2 metal model now retains a valid destination material or imports
  the complete source material when the slot is empty. Seven generated shader
  descriptors are authenticated separately from ordinary textures. The reserved
  footprint includes 30,976 pixel bytes and all eight fog palettes (4,096 bytes).
  The scoped 350 retail cases yield 130 static compositions, 150 RAM-capacity
  refusals and 70 VRAM-capacity refusals; all 10 fresh cases compose. Actual
  ordinary export/readback and source mutation checks passed. A cold BIOS run
  in Dark Hollow rendered an imported E2, defeated it by charging and collected
  its green gem: both game/level totals became 2 and native collection flags
  were set. Contact damage, flame resistance and card reload remain unverified.
- Dragon double-click editing now contains a color picker, independent copied
  colors, project persistence and an original-color reset (14 actual UI checks).
  The native renderer now tints each draw independently without changing shared
  palettes. Red and blue statues rendered together in Dark Hollow; the red
  rescue returned to gameplay with its native collected flags set. Normal export
  is byte-identical to that tested image. Source/export checks pass for all 79
  original statue rows, with exact code-space and palette bounds. Card reload
  and every-color/every-level emulator coverage are not claimed.
- Object pointer fixups now resolve within each copied group, with a final
  writer guard against cross-group pointers. The source-free Moby suite passes
  102 checks including two independent groups and ambiguous/missing targets.
- Dry Canyon's bird handler keeps its real E6 bird dependency while omitting an
  authenticated unreachable legacy B7 spawn. All six original variants compose
  in eight retail destinations and a fresh level; other tested destinations
  retain RAM/VRAM refusals. A cold BIOS Dark Hollow test rendered the handler,
  played its close attack and Spyro reaction, released the real bird on flame
  defeat, and awarded five gems to both totals with native collection flags.
  This covers one variant/destination, not every bird lifecycle.
- Magic Crafters deletion now protects native druid pod membership as well as
  integer links. Removing part of a live pod refuses instead of allowing a
  replacement actor to inherit its activation. Whole-pod and unrelated gem
  removal pass. The dedicated source suite passes 242 checks; import remains
  refused until the full state and dependency closure is established.

Evidence is under task-root `work/universal-finish-20261002`: `index-links/`,
`coverage/`, `universal-groups-ui-r10.log`, `dragon-colour-ui-r2.log`, and
`runtime/`. The complete older coverage matrix has not yet been refreshed for
these changes. Universal Moby gameplay, broad lifecycle/progression coverage,
Mac notarization and physical Windows acceptance remain unfinished. Fly Anywhere
remains deferred. Original discs, user projects, cards and packages are preserved.

The integrated Release run passed all 527 build/source-free suite entries. Its
optional older offline suite first required a generated local cache, then exposed
an existing V5 locked-base terrain path passing a modified executable to the
exact-original surface consumer certificate. That failure is preserved; no
certificate was relaxed. All 68 separately registered current source suites and seven Python source
audits pass, including the remaining cases after the first run reached its
time budget and the authoring case after restoring its exact preserved fixture.
The full optional legacy offline gate is still not marked passed. A newly found
Flight identity-map dependency gap and imported wizard path/index defect remain
open; historical static composition counts are not universal gameplay proof.

---

# V1.0.5 UI navigation checkpoint

Branch: `release/ui-navigation-20261002`, continued from the clean V1.0.4
checkpoint `f7ac875`. Native game generation is unchanged in this update.

- Visible **Save**, Cmd/Ctrl+S and real **File / More** command menus. Arrow keys
  highlight menu items without running them. Pending saves block duplicate
  commands, editing/navigation and window closure.
- **Open Game**, **Quick Add**, **Objects in this level**, **Position & snapping**
  and directly reachable **Undo last removal** make the main workflows clearer.
  Detailed export checks and object identities are kept out of ordinary editing.
  Export warnings remain visible and the full export result remains in Details.
- The Add Object window keeps placement/close buttons reachable at small sizes,
  explains empty searches, and exposes all source versions instead of truncating
  at 200. Background fit updates preserve the selected source version; removal
  advice discards stale selections.
- The full Release regression gate passed **346 checks, zero failures**. After
  the real GUI caught two control-refresh bugs, all **14 UI suites** passed
  again on the final source, including **50 navigation assertions**. Source-backed
  browser review passed 30 checks, including >200 versions and a 900×620 layout.
- Final packages are in task-root `outputs/SpyroEditor-V1.0.5-release-r3`, built
  from `a4e8a35d7c913feb947638e5ec9cb8adb46762f1`. Both passed full package
  verification. The signed Mac app saved Artisans by button and Cmd+S, exported
  the BIN/CUE, restored File/Save after export, and reopened **176 objects / 250
  treasure**. The final BIN is byte-identical to the previous verified GUI gem
  export. No new emulator playthrough is claimed for this UI update.
- All 173 protected user-project files match their snapshot. Exact artifacts,
  source/package hashes, verification scope and the recovered automatic UI
  reopen incident are recorded in task-root
  `work/ui-release-20261002/final-checkpoint.json` and the
  [UI checkpoint report](docs/research/ui-navigation-release-20261002.md).

## Remaining release priorities

1. Universal linked-object gameplay: the next Gnorc Cove blocker requires both
   parent-pointer and source-row-index ownership/relocation. All 396 exact cells
   remain refused; no guessed translation or native guard was relaxed. See
   [the bounded native audit](docs/research/universal-gnorc-parent-index-20261002.md).
2. Expand native enemy/interactive-object playtests, mixed-scene lifecycle and
   whole-game progression coverage beyond the exact verified scenes.
3. Mac notarization and physical Windows runtime acceptance remain pending.
   Fly Anywhere remains deferred at the user's request.

This is a UI release checkpoint, not a claim that universal Moby gameplay or
all original release gates are complete. Original discs, cards, user projects
and earlier packages remain preserved. No remote release is published.

---

# V1.0.4 high-value gem checkpoint

Branch: `feature/high-value-gems-20261001`. Teal 50 and rainbow 100 gems
are placeable through Add Object in every level, including the fresh level.
They use brighter, double-size native models with bounded idle sparkles,
collection bursts and the game's native rising value numerals. Collection adds
50/100 to global and level treasure, and normal memory-card saves preserve it.
The ordinary USA-only Create BIN path includes these gems.

The final Release regression gate passed **345 checks, zero failures**. Native
instruction checks verify award amounts, collected bits, cleanup, particle
limits and native popup digits. Composition/readback covers 144 combinations
across all 35 retail slots and the fresh slot: 130 fit unchanged; 14 first
refuse capacity and then compose after explicit source-row removals. Explicit object removal can create space; no automatic
removal is performed. This does not complete universal Moby gameplay.

Artisans native collection, ordinary save and cold reload passed. The final
appearance and effects passed native collection in the fresh level (193/193
including all seven gem values). Final package and runtime receipts are recorded
in [the gem checkpoint report](docs/research/high-value-gems-release-20261001.md).
A save/reopen regression also corrected added objects incorrectly binding to an
original row through a legacy index alias; old affected additions recover without
replacing the original object.

Final packages are in task-root `outputs/SpyroEditor-V1.0.4-release-r7`, built
from `d0e957b40e5f37bd062a76b686a7fa7e0c9335de`. Both passed full package
verification. The signed Mac application reopened 176 Artisans objects and
250 treasure, showed both model previews, created the USA-only BIN/CUE and
reopened the saved project again. Independent readback preserved every byte
of all 174 original object rows, with both final models/native code and the
250 target. Final native cold Load Game retained 193 treasure and all seven
collected identities. All 173 protected user-project files match their snapshot.
The exact receipt is task-root `work/high-value-gems-20261001/final-checkpoint.json`.

Original discs, the user's project, earlier packages, cards and runtime
candidates are preserved. Fly Anywhere remains deferred. Mac release packaging
uses Developer ID signing without notarization; Windows runtime remains
untested. No remote release has been published.

---

# V1.0.3 portal routing and textured terrain checkpoint

Branch: `release/universal-gameplay-20261001`. Original discs, the user's project,
earlier packages and every runtime candidate remain preserved separately.
Fly Anywhere remains deferred. Full universal Moby gameplay is unfinished.

- **Game Settings > Portals & Return Home** saves doorway destinations by
  level name/ID and each level's return doorway. Route-only projects work in
  normal Create BIN. Undo and a previous route document remain available.
- Moved doorway triggers, lettering and fly-out paths retain their original
  return identity. The stone arch remains separately editable terrain.
- Full-firmware emulator runs passed moved Stone Hill doorway travel to Toasty,
  Wizard Peak, Sunny Flight and fresh level65, with return to Artisans route2.
  Toasty's native vortex and pause exit passed; Wizard Peak used pause exit;
  Sunny Flight used No on failed-flight results. Fresh65 used its imported
  Return Home vortex. These are exact route examples, not every permutation.
- Fresh portal arrival exposed and corrected an unused-slot height mismatch.
  The new 384-wide removable starter floor passed first arrival with no life
  lost. Saved terrain is not resized. Rebuilt retail levels and fresh levels
  reached by portals require a continuous landing area before export. The
  coverage check is conservative; arbitrary obstacles and all headings still
  require in-game testing.
- Missing or corrupt original terrain materials rebuild automatically during
  ordinary level loading. Actual Artisans UI verification retained all 5,199
  editable faces, displayed the original materials, raised a selected face
  and undid it without losing textures. Offline missing-material status is
  retained through the outer loading workflow.
- Final production fresh-route export matches the runtime-verified safe-floor
  BIN byte for byte. Unsafe small-floor exports refuse before staging or
  changing outputs; a small fresh New Game-only scene still exports unchanged.
- The Gnorc dependency proof advances 396 cells past their first ownership
  blocker but adds no successful composition or gameplay coverage. The prior
  matrix remains 105,559/145,285 composed cells, with runtime evidence separate.

Exact test and runtime receipts are linked from
[the portal checkpoint report](docs/research/portal-routing-release-20261001.md)
and [the texture recovery report](docs/research/terrain-texture-cache-recovery-20261001.md).
The final Release gate resolves all 340 checks, including the documented serial
retry. Both packages passed full verification. The unchanged signed Mac package
reopened the saved portal routes, displayed textured Artisans with Terrain tools,
and created the expected route-only BIN through normal Create BIN. The editor is
left open in the private verification project. Original USA/Japan discs and all
173 protected user-project files match their preserved hashes.

Final packages are in task-root `outputs/SpyroEditor-V1.0.3-release-r03` and use
product source commit `6797636626beaae3e711ef97a52edad732da1e13`. Earlier V1.0.3
folders are retained candidates, not this final package. Mac signing is Developer
ID without notarization; Windows runtime remains untested. No remote release has
been published. The final receipt is task-root
`work/universal-gameplay-20261001/final-checkpoint.json`.

---

# V1.0.2 Start Blank checkpoint

Branch: `feature/start-blank-level-20261001`. The completed universal continuation
was checkpointed first at `0f0cdb623446e2f36720ac4b6fbd60f03601e879`
(`checkpoint/universal-1.0.1-20261001`). Fly Anywhere remains deferred.

- File > **Start Blank in Current Level…** confirms the clear, saves current
  work, creates a verified recovery snapshot and opens a safe player start with
  a removable floor in the same editor. **Restore Before Clear…** survives
  reopening and preserves the replacement before restoring the prior scene.
- Terrain, collision, placed objects and unused object assets are rebuilt in
  normal BIN/CUE export. The measured blank Artisans fixture removes 174 rows
  and frees **891,216 resident bytes**. Retired collectible identities remain
  protected independently of reusable physical storage.
- Native full-firmware New Game runs passed for final blank Artisans, a small
  flat replacement with one gem, and a sloped replacement with one gem.
  Walking, jumping, landing and visible floor rendering passed; inventories
  showed 0/0 for blank and 1/1 for the replacements. Exact CUEs, screenshots,
  source hashes and read-only RAM observations are in the research report.
- Frozen Release regression at `aabe94d`: **335 checks passed, zero failed**.
  Final UI polish passed **27 Start Blank checks in Release and Debug**,
  26 level-picker checks and 115 regression-runner tests. Builds had zero
  warnings/errors. No native compilation logic changed during the UI polish.
- Geometry tests passed 1,158 checks in both configurations; exact source-backed
  batch/readback and initializer ownership results retain their separate
  identities in the [verification report](docs/research/start-blank-level-20261001.md).
- Packaged macOS UI confirmed cancellation, clear, removable floor/undo, save,
  normal BIN/CUE creation, reopen and recovery of the original 174 objects and
  terrain. Reopening status and the restored level-picker label were corrected
  during that pass. Recovery preserves the replacement scene on disk.
- Final local macOS arm64 and Windows x64 packages passed full package
  verification. The Mac is Developer ID signed but **not notarized**; Windows
  runtime is **not tested**. Nothing has been published remotely.
- Original USA/Japan disc hashes and all 173 protected user-project files match
  their preserved inputs. The user's running beta and earlier packages remain
  separate from the verification project.

Start Blank currently supports **10 retail levels**: Artisans, Stone Hill, Town
Square, Peace Keepers, Dry Canyon, Cliff Town, Magic Crafters, Beast Makers,
Dream Weavers and Gnasty's World. Twenty other normal levels remain guarded
until their shorter initializers' transition callbacks are proved. Cleared Town
Square and fresh level 65 cannot be exported together because of shared native
storage. The retained sound/particle kernel is not proved to be the absolute
smallest possible footprint. Full universal Moby gameplay coverage remains
unfinished; the prior static matrix is 105,559/145,285 cells.

Final artifacts are in `outputs/SpyroEditor-V1.0.2-release` relative to the task
workspace. The machine-readable checkpoint receipt is
`work/start-blank-level-20261001/final-checkpoint.json`. The research report
records exact evidence paths and remaining work.

---

# V1.0.1 universal Moby continuation

Active branch: `release/universal-coverage-20261001`, preserved V1.0 source
checkpoint `2bc4b1be560dfce89bea1bae8e0c9c6bcdc9346c`.
Original discs, the user's project, immutable project snapshot and earlier
packages remain separate from the new fixtures. Fly Anywhere remains deferred.

- [x] Prove source model-table class lookups and initializer actor/parent roles
  instead of refusing those exact native patterns.
- [x] Recover bounded native particle/spawn arguments and direct player damage
  writes while retaining ambiguous dependency refusals.
- [x] Import missing original player reaction records, share resident records,
  and include aligned growth in retail/fresh capacity checks.
- [x] Retake Alpine Ridge row 1 in Artisans through normal export: native
  contact damage, valid animation 23, recovery and continued gameplay passed.
  The earlier source-variant refusal is resolved. This retest does not claim
  enemy defeat or card reload for that variant.
- [x] Correct permanently removed empty rows to the original unlinked group
  sentinel, preventing a native list-search hang. Normal export with eight
  unused removed slots cold-loaded and passed the Druid contact retest.
- [x] Make the browser's fit-only filter require a completed exact check and
  refresh the selected variant's result. Fresh-level advice uses its own controls.
- [x] Repair remapped gem collection: native red-gem and all-five-value pickup
  tests passed. Ordinary fairy save, full process restart and Load Game retained
  all five collected states and the correct total of 43.
- [x] Complete bounded child-spawn producer recovery: 345 previously refused
  affected cells now compose; 239 retain genuine RAM refusals.
- [x] Refresh exact saved-project capacity with all eight existing imports and
  private terrain retained. Alpine Ridge row 1 fits with additional native rows
  7, 31, 32 and 33 removed, leaving 29,880 bytes RAM headroom.
- [x] Source-free Release regression: 324 checks passed, zero failed, against
  the 2,333-file byte-verified frozen source at 3196a94. Focused source/Debug
  checks retain their separate module identities in the verification receipt.
- [x] Complete final retail/default/fresh matrices with explicit regressions
  caused by newly accounted native allocation.
- [x] Remove the fresh scene's accidental one-object allocation limit while
  retaining 16 dynamic slots. Release/Debug focused source checks passed; the
  separate frozen 3785f22 Release regression passed 318 checks, zero failed.
- [x] Correct treasure maxima and both destination-native gem colour producers.
  Final fresh and retail native runs passed all five values and zero settled
  collection particles. Fresh inventory is 43/43 plus egg 1/1; Artisans is 43/143.
  Final ordinary cold Load Game retained all five collected gems and one dragon
  from the copied earlier native save, without duplicate awards or card changes.
- [x] Prove thirteen native model-table tails and 24 linked particle no-op
  argument packets. Final callback/source-byte guards remain mandatory.
- [x] Build Developer ID signed macOS arm64 and Windows x64 packages; full
  package verifier passed. Mac is not notarized; Windows runtime is untested.
- [x] Actual packaged GUI: add a red gem beside the saved egg, save/reopen two
  objects, enable all terrain face actions, and Create BIN/CUE with both start
  choices. Exact saved Artisans content remains included.
- [x] Independent readback and cold New Game boot of the final two-object GUI
  export: red gem and eye-free egg pickup, platform jump/landing, inventory
  1/1 treasure plus 1/1 egg (global 1/5), and zero settled collection particles.
  No save states or RAM writes; the new blank card remained unchanged.

The final-r3 matrix composes 105,559 of 145,285 retail variant/destination cells:
39,215 static-only and 66,344 with unverified behavior dependencies. This is a
net increase of 42,873 over the preserved 62,686-cell baseline. Compared with
final-r2 (104,293), 1,601 ownership failures now compose, 820 now reach real RAM
limits, and 335 earlier compositions in Dream Weavers now correctly fail RAM
because the gem-colour adapter consumes actual space. Net gain is 1,266.
The final default-type matrix composes 4,242 of 7,175 cells (30 gains, three RAM
regressions); the exact default fresh scene composes 3,980 of 4,151 variants
(116 gains, no regressions). Every matrix runtime cell remains not-tested.
Full universal gameplay coverage is unfinished. Final reports and exact source
fingerprints are in `outputs/Universal-placement-coverage-20261001-final-r3` under
the task workspace. Retail refusals are 27,031 RAM, 7,225 VRAM, 3,203 ownership,
1,633 extraction, 340 reserved depth and 294 sound.

Current native receipts are described in
`docs/research/universal-player-reaction-runtime-20261001.md` and
`docs/research/universal-unoccupied-row-group-20261001.md` and
`docs/research/universal-gem-collection-runtime-20261001.md` and
`docs/research/universal-fresh-multiple-objects-20261001.md`.
The final packaged GUI/export/native chain is documented in
`docs/research/universal-packaged-editor-20261001.md`.
The bounded ownership proof is documented in
`docs/research/universal-native-ownership-20261001.md`. App packages use 4afc9bf;
all product and test files are byte-identical to the 3196a94 regression/matrix
snapshot, with only four evidence/user-documentation files changed.

## Preserved V1.0 release history

Active continuation: `release/v1-artisans-20260930`, checkpoint `7573728`.
The beta54 packages and the user's original project are preserved. Tests use
`work/v1-continuation/user-project-snapshot` or independent writable copies.
Fly Anywhere remains deferred.

- [x] Exact edited Artisans capacity planning names removals and checks source variants, current terrain and imports. Alpine Ridge class 0x10F fits after two additional Regular Gnorc removals (32 and 33). Wizard Peak class 0x11C fits after four additional removals (32, 33, 94 and 109). Both have normal-dialog export/readback receipts. The Wizard variant passed native attack, flame resistance and charge defeat. The Alpine row-1 variant failed a native flame interaction and is now refused for Artisans; its capacity result is not gameplay acceptance.
- [x] Remove, Restore Removed and transform Undo retain the camera; focused UI test passed.
- [x] Final retail terrain UI/native validation: raised/copied/removed physical surfaces, independent near/distant packet construction, collision lookup and genuine 2,048-byte allocation growth. Assistance and distant-LOD boundaries are recorded in the terrain report.
- [x] Normal Create BIN/CUE exported the copied user project with all five removals, eight imports, eight terrain paints, private texture 68 and the saved authored scene intact.
- [x] Inactive legacy drafts stay separate and preserved; cancel preserves the prior output.
- [x] Both explicit New Game destinations passed independent readback. Level content is identical; only the two guarded startup words differ.
- [x] Actual authored-start export cold-booted through New Game into level 65. Native walking, jumping, landing and egg pickup/inventory 1/1 passed without RAM writes or position assistance.
- [x] Final retail terrain native retake passed after correcting LP-only sector render-queue admission; the full-project Wizard Peak Druid combat run passed.
- [x] Integrated Release/Debug regressions and V1.0 packaging. Full package verification and the actual packaged saved-project export passed; final artifact hashes and presentation retake are recorded in the release receipt.

The old legacy-draft gate and the second texture-allocation failure are fixed.
Private terrain packing reserves the exact pending Moby texture allocation, so
the current eight imports and private terrain texture now fit together without
additional removals. The Wizard Peak variant's four-removal plan reclaims the resources it needs while
preserving the original eight imports, eight paints and private texture. The
Alpine row-1 variant is refused because its native flame interaction failed. Proposed removals are never applied
automatically. Finite native memory and unresolved behavior dependencies remain
real limits. See `docs/research/v1-artisans-druid-runtime-20260930.md` for the
clean, controller-only Wizard combat proof and the separate failed Alpine case.

See `docs/research/v1-saved-project-runtime-20260930.md` for exact normal-export
and authored-gameplay evidence. `docs/research/v1-terrain-runtime-20260930.md`
records native raised-floor and added-floor checks and their assistance/version
boundaries. Early topology readbacks compared raw face slots; review caught the
native HP/LP corner-order difference and physical far-colour table selection.
Final v23 output passes independent native packet-order/colour verification. Its
raised Artisans floor also passed controller-only native walking, jumping and
landing with the exact final HP/LP sector and all fourteen changed collision rows
verified in RAM. The final added floor also passed native walking, jumping and landing after a recorded position-only approach assist. The original removed-floor probe overlapped Nestor's solid platform and is not counted as a drop test; the independent plain-ramp fixture now passes assisted native falling, jumping and landing, including genuine native allocation growth. The source-free Release runner passed all 300 checks against a byte-verified frozen snapshot. Focused Debug checks also passed after the final terrain-queue and source-variant guard changes. The complete package verifier passed, and the packaged normal GUI generated a BIN byte-identical to the independently verified saved-project baseline. Final UI review also exercised fresh add/save/reopen/remove and led to a narrow Undo-enable and irrelevant-retail-helper cleanup. See `docs/research/v1-packaged-editor-20261001.md` and the final release receipt.

## Preserved beta54 baseline

The preserved beta54 application packages were built from `6a2d98e`. The V1.0
continuation above includes later product changes; its separate packages are built into `outputs/SpyroEditor-V1.0-release`. The release candidate prioritizes the shared editor workflow and usable capacity reclamation.
Fly Anywhere is deferred at the user's request. Broader universal gameplay
coverage remains open; catalog/static results are not runtime acceptance.

- [x] Preserve original checkout, handoff, game images, saves and prior packages.
- [x] Remove added eyes from both egg models and native inventory icons.
- [x] Native Stone Hill pickup, global/per-level 1/5, normal card write and cold load.
- [x] Native authored sign text displayed in Stone Hill.
- [x] Required-egg percentage and bonus predicates: source guards and 4,489 independent instruction checks.
- [x] File > New Level uses the same main viewport and terrain/object tools.
- [x] Authored geometry, collision, material selection, stable IDs, undo/redo and save/reopen.
- [x] Normal Create BIN combines saved retail and fresh content; failed export preserves existing output.
- [x] Normal native New Game enters the fresh level; textured terrain, imported sheep and walking observed.
- [x] Native rotation export and edits-during-save race fixed.
- [x] Cross-level class remapping and dependency closure expanded without prior composed-cell regression.
- [x] Research/flight controls hidden from normal release; saved research flight settings preserved and ignored by release export.
- [x] Permanent deletion: physical reuse, retired logical identities, native cold-load and six guarded save-map owners verified.
- [x] Release/Debug broad runs completed; affected failures corrected and focused retests passed. Source-QA limits are recorded below.
- [x] Developer ID signed macOS arm64 and Windows x64 packages; package identity/content verifier passed.
- [x] Exact final Mac GUI: reopen three authored faces, add default Sheep without painting, save/reopen and repeated Create BIN.
- [x] Exact final packaged export: native New Game entered slot 65, sheep and floor visible, short collision-supported walk passed.
- [x] Fixed untextured fresh-level imports by admitting only the exact empty native terrain texture component.

## Evidence boundaries

Stone Hill card save used position-only assistance to reach a native checkpoint;
collection, native save, checksum and full quit/cold Load Game were observed.
The cold-load and fresh New Game runs did not write game RAM or load save states.
A complete natural endgame playthrough and all egg lifecycle combinations remain
unverified. See `docs/research/native-egg-verification-20260930.md`.

The remapping matrix covers 145,285 variant/destination cells and 7,175 default
cells. 62,686 variant cells composed (32,578 static-only and 30,108 with unverified
behavior dependencies); all matrix runtime cells remain not-tested. That frozen
matrix predates the deletion change and is identified separately in its receipts.
All-Mobys-in-all-levels remains a coverage goal, not a completed release claim.

Fresh native export uses slot 65 and supports bounded authored triangles/quads,
static native terrain materials and admitted universal placements. It does not
automatically author portals or a full campaign. A tested scene does not certify
every possible geometry/material/Moby combination.

The Mac package uses Developer ID signing without notarization. Windows
package checks do not imply Windows runtime acceptance. No source image, firmware,
card, state or extracted native asset belongs in application/source archives.

Private logs and fixtures remain in the task `work` folder. The preserved beta54 deliverables remain in `outputs/SpyroEditor-beta54-release`;
V1.0 packages belong in `outputs/SpyroEditor-V1.0-release`. Earlier packages stay
unchanged. User-facing guides are `docs/public-user-guide.md` and
`docs/public-known-limitations.md`.

## Regression evidence and boundaries

Release and Debug each built all 294 smoke projects and passed 464 of 466 initial
runner checks. The two failures (deferred-flight expectation and alternate-profile
visual-parent duplication) were corrected and both focused cases passed in both
configurations. Final native mapper checks passed in both configurations; the
exact final packaged Core passed 6,072 scalar/emission/readback checks, including
all six source-bound native owner windows. These are instruction/byte checks,
not 6,072 gameplay tests. Texture ownership passed 97 source-free assertions in
each configuration, including malformed empty-header refusals.

The bounded source-native suite passed 52 of 68 cases (45 CLI and seven Python);
16 legacy research cases remained unrun at its original 1,800-second wall limit.
A pinned legacy oracle was copied into an isolated mirror, preserving its source
hash. Legacy UI expectations for hidden research controls and retired physical
rows were updated to the release contract and the affected focused checks passed.
Full histories and bounded continuation results are recorded in the delivery's
Verification/release-regression-20260930 files. This is not a blanket all-green
claim for every historical research test.

The final packaged export SHA-256 is
`6fa099c7e718384269281eb261294c43e839bc81780c7fdc516d1ab1d2256e2c`.
It was made through normal Create BIN after saving/reopening the GUI-authored
scene, then cold-booted with a real US BIOS and a new private memory card.
Read-only debugger inspection recorded native level 65, one class-0x00A actor,
spawn position 8705/8888/9060 and a short walk to 8705/9968/9063. No RAM writes
or save states were used. Exact receipts remain in the private task work folder.

Whole-game universal behavior, retail topology editing, full fresh-level campaign
entry/exit/save coverage, all egg lifecycle/endgame cases, Windows execution and
Mac notarization remain open. Fly Anywhere is intentionally deferred. Use the
candidate honestly within those limits; this is not full original-scope acceptance.
