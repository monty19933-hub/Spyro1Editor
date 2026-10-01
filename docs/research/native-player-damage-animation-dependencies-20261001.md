# Native player damage animation dependencies

The new `UniversalPlayerDamageDependencies.Analyze(package, executable)` reports primary model-collider damage masks and animation IDs. `MapDamageMask(mask, executable)` uses the same certified native consumers for separately proved overlay writes. These are dependency findings, not runtime acceptance or proof of every native dependency. Callers must authenticate the pristine original model package and analyze the actual imported class/child closure; they must not inspect all classes in an unrelated donor overlay.

The exact original USA executable SHA256 is `a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`. Analyze admits a composed executable only when all nine pinned original consumer windows remain exact. The original image SHA256 is `fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.

Native `800133E0` distinguishes negative simple-array headers from animated headers. Animated headers contain eight collider pointers at `+0x14..+0x30`. Pointer sign controls the spatial table: `8005272C..80052738` assigns positive roots to the secondary table at `+0x1000`, while negative roots use the primary table. `8004D650..8004D684` reads the secondary table for polygon/ground queries; `8004E440` uses the primary table. Positive polygon data must never be parsed as primary command masks. Polygon surface effects remain a separate dependency scope.

For primary groups, native `8004E558..8004E5B4` skips the group radius word, dispatches shape bits 8..15, and stops when the preceding command has terminal bit 0. Shapes 0, 1, 2 and 3 consume respectively 8, 12, 20 and 12 command bytes. Each command's last word is the receiving damage mask. Native stores at `8004E620..8004E62C`, `8004E6C8..8004E6D8`, `8004E7E0..8004E7F0` and `8004E980..8004E990` OR it into the caller's receiving accumulator. The player call at `80040918` supplies `80078A84`. This is distinct from damage inflicted upon the actor and from positional pushback; zero receiving mask does not mean zero collision.

The handler at `80040F68` and contact handler at `80041270` select Spyro states from damage flags. State transition `8003EA68` takes the state in `a0`, stores it at `8003FD5C` into `80078AD0`, then consults the exact 45-byte state-to-animation table at executable `+0x5CC70` / RAM `8006C470`.

| Receiving mask | Potential state | Animation slots |
|---|---|---|
| `0x0001` / admitted generic damage | 14 | 15 |
| `0x0002`, `0x0004`, `0x8000` | 12 | 13 |
| `0x0010` | 25 or 31 depending on health | 26, 32 |
| `0x0020` | 7 | 8 |
| `0x0040` | 27 | 28 |
| `0x0080` | 28 | 29 |
| `0x0100` | 22 | 23 |
| `0x0400` | 29 | 30 |
| `0x0800` / release | 17 / 15 | 18, 16 |

Flags accumulate independently from different shapes. Analyzing only the highest-priority bit in their union would omit valid separate contacts. The analyzer therefore unions potential states. The authenticated animation merger reuses shared/resident equivalent slots and inserts only missing source-owned records.

Actual Alpine Ridge and Magic Crafters class `0x10F` have terminal receiving mask `0x106`; the relevant missing Artisans slot is 23. Their packages expose two primary groups with bounded shape-2 commands. Wizard Peak `0x11C` has receiving mask `0x1`, requiring ordinary shared slot 15 rather than 23. This derives from model descriptors, not enemy names or class roster counts.

Known additional overlay scope: original Wizard Peak `80084CA8` reads flags, `80084CC0` ORs `0x800`, and `80084CC8` stores `80078A84`. The common path at `8007BA74/8007BA8C/8007BA94` does the same. Such writes must be proved reachable in the selected class closure before using `MapDamageMask`. Spawned/shared child models also require their own source-owned descriptor analysis. Direct state changes, native special surfaces and other indirect helper calls are not claimed complete by this model parser.

Validation ran in an isolated source mirror under `work/universal-v1-continuation/player-dependency-research`. Release and Debug builds passed with zero warnings/errors. The focused source fixture checks all 1,368 local packages across 35 native retail levels, exact three Druid package results, native shape strides, positive polygon exclusion, independent mask union, malformed/unsupported command and pointer rejection, mixed-partition rejection, and every consumer preimage refusal. Native rendering/gameplay was not run by this worker.
