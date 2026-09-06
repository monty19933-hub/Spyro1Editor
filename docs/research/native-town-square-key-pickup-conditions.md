# Town Square V3 native key pickup conditions

Read-only diagnosis for the immutable experimental V3 BIN SHA256
`15df0ee1edba6cb48fe1d34df5a5f51a7d6f289de387c499ca0ff04e99b443bf`.
No emulator memory, candidate or production code was changed by this probe.

The exact native pickup prefix at `0x80083B4C`, length `0x160`, is identical
between the retail source and V3. It resides at WAD `0x1197914` in the unchanged
Town overlay (WAD base `0x118E800`, load address `0x8007AA38`), SHA256
`bac4b44c8aaa1e0e382fbd603531810f84c9c916026e655a7a05ff581ea314d4`.

The source dispatch loop uses `0x80078A58`/`0x80078A5C` for Spyro X/Y and
`0x80078A60` for Z: `0x8007DAE0..0x8007DAE8` sets `$fp` to the X address.
The generic actor loop skips records whose unsigned byte `+0x48 >= 0x80`.
The key handler skips any key whose byte `+0x49 != 0`.

For an active key, pickup requires both strict inequalities:

```text
dx = abs(SpyroX - keyX)
dy = abs(SpyroY - keyY)
max(dx,dy) + floor(3 * min(dx,dy) / 8) < 512
abs(SpyroZ - (keyZ - signed16(key+0x38))) < 512
```

The horizontal metric is native routine `0x80017990..0x800179EC`, length `0x60`,
EXE file offset `0x8190`, SHA256
`277c87f0a049389cf5c3e0510eac8b878a516d9f773d44545184f67910815d90`.
The actual comparisons are at `0x80083BC0` and `0x80083BE8`/`0x80083C00`.
They are not a Euclidean sphere or a renderer-derived size. All units above are
unscaled native coordinates. In particular, a separation of exactly 512 fails.

V3 key T107's complete row at WAD `0x1372E38` has SHA256
`0b8e4d05f582671f6d2e79369061be4cdd4b8aac0eb8d18b2672e0bb68ad52a1`.
Its XYZ is `(119632,102288,8192)`, signed field `+0x38` is zero, and its private
four-byte source properties at WAD `0x137670C` are `00000000`. Property byte
`+2` drives rotation; the later property halfword `+0` and byte `+3` manage a
decorative effect. No property bit/timer gate precedes the pickup tests.

Thus ordinary grounded Spyro Z8550..8553 is valid for this key: the vertical
difference 358..361 is below 512. The observed sample
`(120512,102351,8553)` has `(dx,dy)=(880,63)`, native horizontal metric 903, and
fails the horizontal test. That sample does not demonstrate a pickup failure.
A grounded pass with both absolute XY differences at most 350 yields metric
at most 481 and satisfies the position gates. A high jump can fail the Z gate.

When the tests pass, the code plays mapped native sound 0, creates six native
type `0xC` key particles, writes global key state `0x80075830 = 1`, and changes
the key's `+0x49` byte to 2 and `+0x4A` byte to `0xFF`. Those provide precise
runtime observations; this static diagnosis does not itself claim pickup or
the subsequent chest/reward loop passed.

## Locked-chest initiation and state observations

V3's imported chest handler is `0x80089ECC..0x8008A608`. Chest T108's yaw byte
`+0x46` is `0xDC` (220), signed ground offset `+0x38` is zero, and private
properties begin at WAD `0x1376710`. Property word `+0` identifies key T107;
word `+4` is the opening/wobble timer. When the acquired key makes global key
state 1, the chest advances from byte `+0x49 = 0` to 1.

Opening initiation in state 1 (`0x80089FAC..0x8008A13C`) requires:

- Global key state is exactly 1.
- Spyro state word `0x80078AD0` is 0, 1, 2 or `0x15`.
- The same native horizontal metric is strictly below `0x600` (1536 raw units).
- The absolute vertical difference, including the signed chest ground offset,
  is strictly below `0x200` (512 raw units).
- The shortest unsigned-byte angle difference between Spyro's yaw at
  `0x80078A66` and the bearing from Spyro to chest is below `0x30`.
- The corresponding difference between chest yaw `+0x46` and the bearing from
  chest to Spyro is also below `0x30`.

Native angle helper `0x80017908` calculates the shortest wraparound distance on
256 units per turn; `0x30` is 67.5 degrees. Bearing routine `0x80016AB4` has
0 on +X, `0x40` on +Y, `0x80` on -X and `0xC0` on -Y. Thus this chest's front
points toward +X,-Y. An illustrative grounded point `(122150,101850,8550)` is
on that front side, within the authored dry-support square, with a native XY
metric of 634; facing toward the chest calls for a yaw near 97. This is a
navigation aid, not a mesh/physics collision certificate.

No attack-button test appears in the initiation path. Correct proximity,
front-side position and facing in an accepted player state start it
automatically. Initiation sets chest state 2. In that state, property timer
`+4` advances by native frame delta `0x800756CC`; the normal sequence reaches
the final stage at `0xD0` (208 native timer units). This is not asserted to be
a fixed wall-clock duration. The final stage plays the imported model-local
sound, spawns native debris classes `0x135..0x137` and particle type `0x46`,
activates the native reward chain, removes the chest, and writes global key
state 2 at `0x8008A560..0x8008A568`. Key state therefore remains 1 through
the opening sequence until that final consumption, rather than switching to
2 as soon as the chest begins opening. Runtime observations belong in the
separate runtime evidence record.

## Exact reward composition: six gems, ten treasure

The chest emits one gem itself in `0x8008A534..0x8008A544`, calling global native
emitter `0x8003ABC0` with motion mode 1. The five reward controllers each call
the same emitter once from `0x8008A8BC`, after their native owner/deletion/delay
checks. The second emitter argument selects motion, not quantity. At
`0x8003ABF4..0x8003ABFC` the emitted class is taken from source-row byte
`+0x53 & 0x7F`, and `0x8003AD4C..0x8003AD5C` dispatches that one class to the
native destination spawn callback.

Fresh reads of retail Peace rows and V3 rows agree on every reward selector:

| Retail donor | Donor row WAD offset | V3 row | Reward class | Native value |
| --- | --- | --- | --- | --- |
| T79 chest | `0x1A5CD88` | T108 | `0x54` green | 2 |
| T118 controller | `0x1A5DAF0` | T109 | `0x54` green | 2 |
| T119 controller | `0x1A5DB48` | T110 | `0x53` red | 1 |
| T120 controller | `0x1A5DBA0` | T111 | `0x54` green | 2 |
| T121 controller | `0x1A5DBF8` | T112 | `0x53` red | 1 |
| T122 controller | `0x1A5DC50` | T113 | `0x54` green | 2 |

All five controller property blocks retain native `+0x18 = 1`, `+0x14 = 0`,
with only owner index and coherent coordinates relocated. No color/value
substitution was introduced by closure or V3 placement.

The collected-gem handler at `0x8003BA64..0x8003BAB8` compares actual gem class
`+0x36`: class `0x53` loads award argument 1 at `0x8003BA78`; class `0x54`
loads 2 at `0x8003BA88`, before calling `0x8003B854`. The latter adds that
value to global treasure `0x80075860` and the current level's counter
`0x80077420 + 4 * [0x80075964]`. The encompassing native EXE span
`0x8003BA54`, length `0x68`, file offset `0x2C254`, has SHA256
`b34aefbb1203640fb4f4997db0687cbed2f53f4704df3557c634da99f979c175`.

Therefore native expected output is **four green gems plus two red gems**, six
physical gems worth **ten treasure**. Five controllers must not be confused
with five physical gems: the chest has its own separate green reward. The
unchanged 200→210 level treasure target is consistent with this native proof.
