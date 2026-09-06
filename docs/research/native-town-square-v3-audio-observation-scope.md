# Exact V3 native audio observation scope

Independent read-only cross-check for Town Square V3 BIN SHA256
`15df0ee1edba6cb48fe1d34df5a5f51a7d6f289de387c499ca0ff04e99b443bf`.
This note gives recording expectations, not an audio playback acceptance result.
The full native descriptor/sample/loader derivation is in
[sound and effects evidence](native-town-square-chest-sound-effects-evidence.md).

## Fresh V3 ownership readback

The complete V3 Town sound bank, data-relative `0x80800`, length `0x5E000`, is
byte-identical to retail Town, SHA256
`1771f66bb5315f8994cf2c843c0d5576f38fb347ae57b74ab9463abc21dd3332`.
The complete sound component body at data `0x172614`, length `0x6F4` (global map,
count 76 and 76 descriptors), is likewise retail-identical, SHA256
`f92cc85d9eb3fc002a1f9cb9e65ab2ef0e165f8737901cdba9590ea982865598`.
Original Town data begins at WAD `0x119E000`; V3 at `0x119F000`. No audio bank,
descriptor or global sound-map replacement was needed or performed.

The imported model at V3 data `0x1D0000` begins
`0100000036ffffffffffffffffffffffffffffff`. Its first local sound byte is
correctly relocated from Peace descriptor47 (`0x2F`) to Town descriptor54
(`0x36`). The unchanged native consumer `0x8003851C..0x800385BC` passes this
package byte directly to PlaySound; it does not perform a global map lookup.

Fresh donor-to-V3 comparisons confirm identical sample bytes and identical
descriptor playback metadata (`+4..+19`) for these three dependencies:

| Dependency | Town descriptor / sample, data-relative | Bytes | Native base pitch | Sample SHA256 |
| --- | --- | --- | --- | --- |
| Global ID0: key/gem pickup | 17 at `17286C` / `8E100` | `3C40` | `1000` | `57a13ba3c0348ecd5212ea7fca7c23d211c9e298e1a3a8ae06f571cd01459610` |
| Global ID37: reward release | 35 at `1729D4` / `AF2E0` | `B60` | `0800` | `dfb35ed7a3c560113f0e8530bf1efe423ea1306f90f432ec991f4949a000ee31` |
| Model-local slot0: chest final sound | 54 at `172B50` / `C5420` | `1430` | `0400` | `f10bef3c98281716bf79cb09c77ff6017c2959d10f11ba4f0b9cda1868c977c3` |

Addresses, lengths and pitch values in this table are hexadecimal. The first
two descriptors are fully byte-identical to Peace; the chest descriptor differs
only in the sample offset because Town repacks the same audio. All three exact
ADPCM spans end in a native end-flag frame without a loop marker. This guards
actual sample ownership rather than merely matching selector numbers.

## What the recording should establish

These expectations describe a fresh, previously uncollected six-reward family.
Previously collected controllers may legitimately be absent after reload.

1. Key pickup at native `0x80083C24` calls mapped ID0 and should have an audible
   cue accompanying key state1 and the key particles.
2. The chest's final opening/destruction stage at `0x8008A45C` calls model-local
   slot0. This is the important relocated sound, associated with the native
   debris/reward release, not a fabricated continuous lid-opening track.
3. The five reward controllers start one mapped ID37 note at its base pitch
   `0x0800` (`0x8008A9EC..0x8008AA40`). Their subsequent staggered emissions
   use the same sample with increasing native pitch values
   `0x0880,0x0900,0x0980,0x0A00,0x0A80`. Code at
   `0x8008A8CC..0x8008A930` adds controller ordinal shifted left 7 to descriptor
   pitch and supplies the native pitch override. These are native timer events,
   not guaranteed fixed wall-clock intervals.
4. Collection of each of the six physical gems calls mapped ID0 from
   `0x8003B9A4..0x8003B9B8`, while the actual treasure total increases by the
   corresponding native value. The release notes and collection cues are
   different event families and can overlap each other or the existing music.

Potential runtime failures worth distinguishing are an incorrect short sound
from unremapped Town local47, missing or prematurely cut reward notes when their
source controllers retire, lost pitch progression, looping/stuck voices, or a
visible event with no corresponding audible cue. A close recording from before
key pickup through opening, release and final gem collection provides useful
context; subsequent normal gameplay can expose a stuck voice or channel issue.

The pre-key red material response `0x8003A9EC..0x8003AA80` contains no sound
call. Flame/charge/bonk audio comes from unchanged native player/interaction
code; it is not a second imported chest sound slot. This bounded trace did not
fully enumerate those broader player sounds and does not invent a new chest
audio dependency from the visual red flash.

Sample and code equality do not prove runtime voice allocation, audible mix,
timing, attenuation or recording quality. Conversely, a recording is not a
bit-exact SPU waveform comparator or proof of every audio event in every native
state. A normal-gameplay recording should be paired with the existing exact
source binding and event/state observations. No ROM, emulator setting, source
code or candidate was modified by this investigation.

## Subsequent R03 observation

The [recorded runtime report](native-closure-runtime-20260905.md#r03-native-audiovideo-capture-on-the-exact-same-candidate)
now adds actual, source-bound waveform detections for these expected cues. Its
retained R03 directory contains the immutable 109.506-second native MP4,
`audit-native-audio.py`, full JSON results and a concise methodological summary.
The correctly remapped break, pickup cues and increasing release pitches match
the recording. Independent review checked descriptor/sample/pitch ownership and
FFT normalization against a direct-sum oracle. This closes the measured cue
execution question, not subjective listening, every voice/tail state or all
future imports. Source-bank equality alone was not used as playback proof.
