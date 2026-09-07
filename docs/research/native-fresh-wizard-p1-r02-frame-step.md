# Fresh Wizard P1 R02 — frame stepping verified, cause still open

September 7, 2026. This was the same independently checked clean c4a7…5b5a6
research image, not a patched scene or an R03 editor update.

The new read-only health/motion fields were exercised against the actual native
P1 layout. The first inspected normal sample was already after one fall/respawn:
lives3, health3, position10988/8969/2070, state6/timer100, both raw vectors
0/0/-8960, scalar-192, branch inputs120/0. All fixed layout/code and reread
guards passed. The loading transition had run ahead of inspection, so this is
not an earliest-entry sample and full health does not prove the original cause.

Native F6 Frame Step now has observed evidence: the first request retained the
same player timer; the next advanced100→102 and Z2070→1790, returning to pause.
Two more separately sampled requests with Select entered inventory at timer104/
Z1510. Each video-frame request is not necessarily a Spyro gameplay update.
No GDB execution command or RAM/register write was sent.

Root switched native Emulation settings to10% wall-clock pacing for another
diagnostic entry. CPU/RAM remained native1:1/2MiB; this is not full-speed
acceptance. Opening Settings introduced `cache/http_cache.bin`, which the
observer's strict additional-image filter had not classified. The next sample
stopped the observer exit2. The failed terminal record remains failed; no guard
was bypassed and no further gameplay followed it. Native disconnect auto-resume
was visibly handled by re-pausing before quitting without resume-state save.

DuckStation and observer are closed. Fresh original global/game settings were
restored byte-for-byte; all nine protected hashes passed. The private card was
never copied back. No video capture was started. R03 and its document remain
unchanged. Defeat/reward, earliest-entry, audio and save/reboot gates remain open.

Exact journal, JSONL, raw FD fixture, fresh settings backups and restoration log:
`_local/native-fresh-wizard-p1-runtime-20260907-r02.luV04T/`.
JSONL SHA256:
`1b3fda8b0adc0cc293d9418e4aefdc56d0bb201f83376e4700fcbd8f010aaeae`.

## Source comparison and next investigation

The original image, exact P1 and preserved edited P0 share the inspected ordinary
spawn/reset code. The later loader selects player state0/timer0; it is wrong to
say entry simply preserves the old player state. The selected lightweight reset
and state0 initializer do not directly clear the two raw motion vectors. Actor
updates can precede the player update. These are competing hypotheses, not an
executed complete entry history or permission to clear movement state.

Root read the full source note/script and independently replayed its fixed
three-image inspection, with complete initial/final hash, true EOF, held/path
identity, receipt, EXE/body/overlay checks. Evidence:
`fresh-p1-entry-motion-r01.L9SeXy/` under the private dark-wizard research parent.
Next review the native cache classification and obtain earlier controlled
samples before changing spawn, collision, motion reset or enemy behavior.
