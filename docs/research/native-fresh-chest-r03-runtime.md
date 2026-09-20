# Native chest R03: all20 collected, healthy native reset observed

September20,2026; source73bac96. This exact private P9/N32 candidate completed
Wizard defeat/reward, key pickup, chest opening and collection of the remaining
ten treasure points. Actual slot35 reached20/global78, remained unchanged after
death/reset, and native inventory displayed LAB20/20. This is a bounded runtime
success for the identified recipe, not general chest-family or editor approval.
The run is closed and protected settings/source/card owners are restored.

## Exact artifact and prior source checks

Run directory, relative to the repository:
`_local/native-v6-chest-runtime-20260920-r03.p2kFgp/`.
The exact CUE is `candidate-r03/fresh-chest-p9-experiment-r03.cue`.
Its `RUNTIME-SESSION.md` records these complete identities:

| Owner | SHA256 |
| --- | --- |
| BIN,661547040 bytes | `a5b00725388f73e7e6f7b9ed356c4a0d0f55b1c869d6a65f38e97ef2641d6c50` |
| CUE | `7c311399bb0aac79e908ca515be39e6da8215e7e0fc5d52f99bb3b0aa60411d8` |
| Receipt | `f28cd047e0062dd14194fb335f6042208309e943f5376d01e09c1de2cc0fa625` |
| Scene | `4f560dcbfa2d997a3f02554cf297d95c1778f67fc5d8fcf8ff47f64d609a89e8` |
| Body | `2d8c1bf1af35c8e5522b239c564d36c79c828539d133278c461b6178b53bb504` |
| Executable | `81501f07ec9ce336089ec5a92f81bf51569aed723ac4f8ba1cf85ba0ef69a431` |

Actual output525 passed417 assertions, complete readback and the full281270-
sector inverse. Source520 passed414; build518 had zero warnings/errors; broad
regression522 passed343 command steps with zero failures. Historical R01/R02
replays523/524 passed579/390 with unchanged complete image and physical plan
identities. These checks preceded runtime; they are not substitutes for it.

R03 changes only Reward2's authored WORLD destination to10454/10503/9216.
Four scene coordinate words differ from R02. The3072-square floor remains
X/Y8448..11520 atZ8704; spawn8960/8960/9216, key9728/9120/8704,
chest10864/9888/8704, Wizard/Grass and the other four destinations are unchanged.
Native instructions, collision descriptor, selectors, parent links and timing
are unchanged. The earlier conditional flight calculation motivated this
recipe; it did not certify the runtime trajectories.

## Boot, controls and evidence boundaries

Root launched the exact CUE with slowboot, checked the held exact BIN with
lsof, and observed the PlayStation licensed Sony firmware screen through CUA,
followed by the game's Sony Computer Entertainment America presentation.
The copied normal save and native LAB selector were used; no foreign savestate,
cheat, RAM/register write, fabricated helper return or changed native physics
was used. This was not an injected executable or resumed state.

The bounded observer used localhost read-only memory requests; gameplay used
native controller input. Paced frame-step batches and ordinary-speed intervals
were both used. Requested steps are not asserted to equal game-update frames.
The observer's annotations/false authentication flag are not disc identity;
external artifact checks and the launch record identify this run.

`observations/observer01.jsonl` contains exactly50 records, labels00..49.
Exactly46 have `bounded_sample_valid=true` and consistent gate rereads.
Records00/01 are loading/game1,45 is death-fade/game4, and49 is inventory/game3;
those four intentionally omit actor/private payload reads. Sequential samples
are not atomic snapshots or a continuous execution trace. Final JSONL SHA256:
`56661efb9b646f80d3638a8f817f33c15fad4d99ac95ff6f81e91becc142acd9`.

## Observed native sequence

| Samples | Measured result |
| --- | --- |
|02|First accepted normal sample at8960/8960/9066, state0/timer28, health2/lives3. It is already after unobserved activity, not an earliest-entry or untouched-health sample.|
|03..21|Native movement, turning, flame attempts and damage/recovery. Key state becomes1 at18 while actual slot35 remains0/global58.|
|22..28|Wizard enters native dying state and its value10 reward is collected. At28 actual slot35 is10/global68; the Wizard is retired by29.|
|29..34|Player stops at10458/9022/9066. Chest is in opening state2; its private timer advances while key animation runs and key global remains1. State2 is an in-progress sequence, not a failed opening.|
|35|After an ordinary-speed interval, chest and all five D controllers are retired; key global3; actual slot35 is20/global78. Only Sparx remains active in the dynamic pool.|
|36|Ordinary idle retains20/78, retired chest/controllers and no remaining dynamic gem. Player health1/lives3.|
|37..40|Deliberate movement beyond the south floor edge:39 hasY8361, below floor minimum8448. At40 Spyro is back at spawnXY and lives decreased3→2; the Wizard has returned with reward selectorFF. Treasure remains20/78 and chest/controllers remain retired.|
|40..47|A later death-state sequence is sampled: health−1, state30/timer30→102, Sparx absent; lives2→1 at45. The long interval before40 did not capture each intervening attack or first reset.|
|48|Healthy native reset directly observed:8960/8960/9060, state0/timer2, health3, lives1 and restored Sparx. Treasure20/78; chest and all five D rows remain retired; Wizard is active with selectorFF.|
|49|Native inventory scalar sample retains20/78 and health3. The final native screenshot independently displays LAB20/20. No new-level transition follows this sample.|

The Wizard's ten points and chest's remaining ten points were separately
measured. The chest emits six physical gems worth ten points, not ten gems.
The interval34→35 was not sampled continuously: no claim is made that every
bounce, effect, key-state intermediate or individual pickup trajectory was
recorded. Unlike R02's final escaped red gem, this run ends with all20 actually
credited and no remaining dynamic gem in the bounded pool. This demonstrates
one successful RNG/input history, not a universal landing envelope.

The health−1 samples40/41 are not a persistent failed-reset finding:48 directly
shows health3/state0/Sparx restoration. Reattack by the returned Wizard is
consistent with the native behavior, but individual hits during the unsampled
interval are not reconstructed as observations. The earlier corrected Wizard
R02 separately measured the same health−1/state30→life decrease→health3/state0/
Sparx cycle; see `native-fresh-separated-wizard-runtime-r02.md:25–30`.

No duplicate chest reward was observed after either sampled return. The key
row resets to its authored position while key global remains3; this is not a
general new-level key-reset claim. Wizard selectorFF was observed after reset,
but repeat killing and its possible native RNG drop were not tested.
No sampled renderer-exhaustion flag was set; unsampled allocation peaks and
global audio correctness remain outside this evidence.

## Native screenshots and untested gates

The run's `screenshots/` directory contains F10 captures:

- `Spyro the Dragon 2026-09-20-02-35-08.png`: cleared textured-floor gameplay.
- `Spyro the Dragon 2026-09-20-02-42-21.png`: native LAB20/20 inventory/global78.
- `Spyro the Dragon 2026-09-20-02-10-15.png`: saved during the presentation
  interval, but the preserved PNG renders black on this review. It does not
  establish the firmware screen; that evidence is the contemporaneous CUA
  observation recorded by root. Game presentation alone would not prove full
  firmware boot either.

Inventory display is not an ordinary portal exit, new-level re-entry or
save/card-reload test. None of those gates was completed. Full lifecycle,
repeat-kill reward behavior, arbitrary placements/materials/families and other
RNG histories remain unaccepted. The experimental key-zero source premise and
complete static/transitive census remain open: observed20 does not supply a
universal key-zero proof. Runtime/lifecycle/census/key-zero/publication flags
remain false. No public Create BIN, general editor admission or preserved-app
replacement follows from this private runtime result.

## Closed and preserved

Root recorded observer execution8200 exit0 and DuckStation execution37543
exit0; no DuckStation process remained. No resume savestate was saved.
After-test INIs were retained privately, then both live INIs were restored
byte-for-byte and checked:

- Global INI: `c07823d8de99c88b560fb5dc356fd1247aa391b88ff91284d7ef1354c10fb854`.
- Game INI: `629d9bd3eb9890eedb2bfd9ae2458f7f214272f951a9081f687abe8e5f11d1f3`.
- Original card unchanged: `d69b43f8be2585f7b5e7fd1b07f85217ba74c01f102c01d7a22928ee0e9e8ae2`.

Original BIN/CUE were checked unchanged. The private card was not copied back;
normal editor projects and the preserved R06 editor were not modified. The
run-local journal is marked CLOSED and records the completed exit and
restoration checks alongside its historical setup instructions.
