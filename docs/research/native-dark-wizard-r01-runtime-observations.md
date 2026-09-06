# Dark Hollow one-Wizard R01 runtime observations

The2026-09-06 session is closed with retained video, normal emulator quit and
original settings restored. It provides bounded evidence of normal entry,
visible imported attacks, native electric state7/animation8 and a completed
player death/reload. **Wizard defeat, the imported10-gem reward, post-defeat
exit/re-entry, audio acceptance and complete body/feet presentation remain
unverified.** This is not a complete combat, memory-budget or universal-import
certificate. No producer approval flags are changed.

## Exact session and evidence

The unchanged candidate was published from Release checkpoint26042c6 after
the275-step/zero-failure gate. Publication and independent reopen each passed
333 checks; those artifact checks are separate from the observations below.
Its directory is
`_local/research/dark-wizard-native-closure-20260906/candidate-r01/`.

| Artifact | SHA256 |
| --- | --- |
| Candidate BIN | `2a87dc12f9398ef873267e0f17792cfe160952c9f1bf12c137f64029863a39db` |
| Candidate CUE | `532be677b7d3aaa575f0c93af53a62630b5b5c9cacaf14db156330291530a414` |
| Candidate receipt | `dd7d490cdfa5cdf60d92e110b1946d4d296277563b77d360e4d6513821a0d32a` |
| Finalized MP4 | `4bcc3f7b8a33025fb0a86b2f3a3dd104adea4d5fbfe466355239c9a7226ea1ce` |

Evidence root: `_local/native-wizard-runtime-20260906-r01.rSQXSb/`.
Relevant retained records are `PREPARATION.md`, `SESSION-NOTES.md`,
`native-wizard-observations.log`, `native-wizard-navigation-observations.log`,
`observer-navigation-source-readback.json` and the video
`videos/wizard-r01-native-normal-entry-session-01.mp4`.

The independently reviewed navigation log has272865bytes,81 labeled samples,
and SHA256 `e4e6137c79f9528001a586a633c864203be43b37fd6820f1cd8138d3e29f16ac`.
All81 retain non-atomic-read and `runtime_pass:false` annotations.

The session operator finalized capture before quitting. ffprobe reported
1136.756009seconds and758126173bytes, H.264640×480 video plus AAC44100Hz stereo.
The operator's full video/audio decode completed with exit0 and no errors,
and the completed file hash is pinned above. File decodability does not
establish correct native cues, pitch, synchronization or listening quality.

The game was entered through normal menus and portals, using an isolated copy
of the original memory card. No RAM/register writes, breakpoints, savestates,
cheats, level selector or Free Flight were used. Controller macros were bounded
normal button holds, released before observation; pause/resume remained UI
operations. Navigation briefly used25-percent playback, then restored100 percent;
the native CPU clock and2MiB RAM configuration were not changed.

The observer performs sequential bounded reads, not atomic CPU snapshots.
Every record retains `runtime_pass:false`. Profile/data hashes printed by the
observer are annotations, not authentication of the loaded BIN. Native field
meanings and source fixtures are documented separately in
`native-dark-wizard-runtime-observer.md`.

## Observed sequence

Times below are UTC from the logs, not inferred MP4 timestamps. Line numbers
refer to `native-wizard-navigation-observations.log` unless stated otherwise.
Visible descriptions come from the retained session operator's notes; scalar
states/counts below were independently checked against the JSON records.

| Event | Recorded evidence | Bounded conclusion |
| --- | --- | --- |
| Normal entry,13:18:15 (line23, `dark-entry-baseline`) | Level12, player state0/animation0,4 lives; appended row128 class0x11B at(92832,25248,10445); capacity90, one active dynamic row and an in-range terminal; renderer exhaustion0. | The real normal Dark Hollow entry loaded the appended instance. This alone is not behavior evidence. |
| First visible encounter,14:07:44 (line74) | Operator saw the Wizard in the low room. Row128 was state0 at the authored position; player(82709,27159,10599). | An imported Wizard was visually encountered; full silhouette/feet judgment remains separate. |
| Visible attack,14:09:38 (line77, `wizard-first-visible-native-bolts-close-range`) | Operator saw yellow lightning. Wizard state1/animation1; one live class0x26 bolt in state2 and selector0x07/kind2 particles were sampled. | An imported native attack path executed. A single paused sample does not prove every projectile in a complete three-bolt volley. |
| Electric hit,14:10:14 (line78, `wizard-native-electric-hurt-visible-r01`) | Stationary no-controller-input observation: player state7, animation8, timer20,4 lives at(90401,24370,10599). Four class0x26 impact children were in state3; selector0x07/kind2 and0x41/kind1 particles were present. Operator noted blue-to-green Sparx change. | Native electric player state/animation and unsuppressed impact-child activity were observed. Audio remains unaccepted. |
| Misleading recovery label,14:11:34 (line79, `wizard-electric-hit-recovered-native-movement`) | Player moved to(88318,25787,10597), but the actual fields remain state7/animation8, timer0; four state3 impact children were sampled again. | The label is not stable-recovery evidence. Movement between samples does not override the recorded electric state. |
| Later ordinary state,14:13:39 (line80, `wizard-close-after-second-electric-hit`) | Player state0/animation0 at(88596,25877,10599),4 lives. Wizard remained active in state1. | A later sample returned to ordinary state; this is not a measured uninterrupted recovery interval. |
| Player death transition,14:14:23 (line81, `wizard-flame-attempt-black-transition`) | Lives4→3; player state30/animation31, timer102; operator observed a black native transition following close flame attempts. | Player death occurred. Wizard defeat/reward did not follow from this event. Loader-phase rows require the separate caution below. |
| Reload complete,14:21:47 (line82, `wizard-native-death-reload-complete`) | Level12, state0/animation0,3 lives at native start(15119,62351,17767). Wizard128 reset at authored position with relocated properties0x801410B8. Dynamic count1, capacity90, one Sparx/class0x78 row then terminal slot1; exhaustion0. Operator observed HUD12 gems/2 dragons/3 lives. | The normal death/reload completed and the Wizard reloaded. This is not post-defeat persistence or exit/re-entry acceptance. |

Earlier state6 knockbacks were explicitly attributed to original courtyard
and corridor enemies, not the imported Wizard. Original chests and BigArmor
rewards were collected during navigation; the HUD showed11 before the close
Wizard encounter. The later HUD12 is not an imported+10 result and must not
be credited to the Wizard. Row128's reward selector remained0x56 in the
samples; that configured selector is not proof that a reward was emitted or
collected.

Selectors are a separate namespace from Moby classes: the logs' `07` and
`41` particle labels are hexadecimal selectors0x07 and0x41. Particle0x41 was
also present in earlier scene samples, so its presence alone cannot identify
every particle's creator. The contemporaneous attack/impact records support
the bounded effect observation, not attribution of every active particle.

## Loader-phase and resource limits

During the black transition, line81 still reported a stale dynamic count6,
but all90 scanned row slots were **zero-filled class0 records**, with no
in-range terminal. Wizard properties were raw0xEED4, not a relocated RAM
pointer. This transient, incompletely initialized scene is not evidence of
90 live dynamic actors, allocator exhaustion, corruption or a crash. The
observer's attention was respected by remaining paused and inspecting before
bounded normal resume. The later initialized reload sample is the applicable
count/terminal observation.

In the initialized combat/reload samples above, renderer exhaustion was0 and
dynamic/particle terminals were present. These are sampled facts, not peak
usage measurements. Primitive slack, current counts and the collision helper's
last saved SP cannot certify unseen transient peaks, total stack demand, GTE
state or safe arbitrary instance counts.

Wizard movement reached(91969,25245,10240) from initial Z10445. The donor's
signed ground offset is0 and the initial origin is205 above its floor. The
separate bounded source-floor check found rendered HP and native collision
both at10240 for the initial and moved XY locations; no Magic Crafters-style
water/rendered-height mismatch was identified there. This supports the origin's
ground snap, **not** a full mesh/body-height, animation-pose or collision-envelope
certificate. Detailed body/feet presentation remains pending.

The bounded floor check is against actual original Dark data SHA256
`a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97`:
initial XY lies on HP sector75/face61 (original WAD0x1008FA8, texture1) and
collision875 (WAD0x1014B58); moved XY lies on HP sector75/face66
(WAD0x1008FF8, texture1) and collision927 (WAD0x1014DC8). Both HP quads and
supporting collision faces are flat at10240. These are source geometry
addresses, not observed RAM pointers or a new rendered-body measurement.

## Post-session observer regression

The actual unrelocated row exposed a missing necessary scan gate in the
observer. After this session closed, the observer was tightened to require
row128 properties exactly equal to the source-issued relocated owner:
`(moby_base - 0x8C38) + 0xEED4 = 0x801410B8`. Exact arena globals and class0x11B
alone had been insufficient during reload. Source scene code pins both the
row-table and appended-private offsets; this is not a caller-supplied pointer.

The check now precedes every dynamic-row and particle-pool scan. On a mismatch
it retains the raw Wizard row, records an unresolved layout/loader attention,
and leaves `runtime_pass:false`; it does not guess reload versus corruption,
follow the pointer, or claim that a matching pointer proves full initialization.

Tests first failed for all four wrong-pointer subcases:0, raw0xEED4, neighboring
0x801410BC and main-RAM end0x80200000. The corrected empty-pool fixture supplies
the real relocated owner, and all14 observer tests then passed. Independent
review and a second14-test synthetic rerun found no material issue in this
narrow gate. This is a **post-session source/test fix only**: no emulator was
reconnected, no runtime replay is claimed, and the original recorded logs were
not rewritten to resemble the new output.

## Pending acceptance and cleanup

Next gameplay evidence must separately establish Wizard defeat, emission and
collection of exactly one native class0x56/10-value reward, the correct native
treasure accounting, and normal exit/re-entry after that defeat. Complete
volley/impact behavior, repeat combat, body/feet presentation, and native
audio cues/pitches still need focused review. The finalized recording contains
audio, but no listening or source-template audio audit is claimed here.

Cleanup completed14:26..14:29UTC. Observer quit with`q`/exit0; DuckStation
PID49030 quit normally/exit0 without a resume state. Both after-test INIs were
retained, then the live files were restored byte-for-byte to their originals:

- Global INI SHA256 `cd3bfb693556cc08c81b69f56199b61068ac7b483f3d5dae22e2a3931ca55721`.
- Per-game INI SHA256 `629d9bd3eb9890eedb2bfd9ae2458f7f214272f951a9081f687abe8e5f11d1f3`.
- Original memory-card SHA256 `d69b43f8be2585f7b5e7fd1b07f85217ba74c01f102c01d7a22928ee0e9e8ae2`, unchanged.

The session closure records exact rechecks of original BIN/CUE and candidate
BIN/CUE/receipt against their preparation identities. The original card was
not overwritten. The installed editor was not rebuilt, replaced or launched.
Subsequent source checkpoints, including multi-Wizard scene/ledger and fresh
texture work, did not change the tested one-Wizard R01 candidate.
