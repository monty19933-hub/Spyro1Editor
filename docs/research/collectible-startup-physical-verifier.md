# Startup-only physical verifier

`tools/Verify-SpyroCollectibleStartup.py` independently reconstructs the fixed
USA startup experiment from the entire authenticated original image. It reads
no C# assembly, generated EXE, receipt or claimed candidate hash for authority.
It publishes no BIN/CUE and does not launch an emulator. The existing
[bootstrap CPU evidence](collectible-ephemeral-cold-boot-bootstrap.md) remains
separate from this physical-image check.

The recipe is closed to three original EXE extents:

| File extent | Independent intended edit |
| --- | --- |
| `10/4` | Entry PC `8005B8E0` becomes `80075640`. |
| `65DA4/4` | Paired reserve `800` becomes `A00`. |
| `65E40/2C` | Eleven fixed MIPS instruction words below. |

```text
3C02801F 3442F600 3C03801F 3463F800 AC400000 24420004
0043082B 1420FFFC 00000000 08016E38 00000000
```

Complete original EXE identity and the header, whole ephemeral tail and native
clear-loop pins are checked before applying the recipe. The raw sectors are
**53875 and 54078 only**. The independently inspected Mode2 Form1 EDC/ECC
primitives reused through `Verify-SpyroJapaneseEggResearch.py` must first
reproduce each complete original touched sector. Sync, MSF, mode and duplicated
subheaders—including original end flags—remain unchanged. Restoring only the
three edited field extents and rebuilding integrity bytes recovers each original
touched sector exactly.

The verifier streams the entire raw image, compares every candidate byte with
its independently reconstructed expected byte, and hashes the complete inverse.
It also checks the original ISO WAD/EXE owners; with an explicit candidate it
compares the complete logical WAD, complete EXE, ISO root and PVD. The complete
raw complement additionally protects all other ISO/disc bytes. Held and named
inputs are rechecked at completion. This is a cooperative freshness check, not
hostile-filesystem race safety.

## Results and use

Tests were written first and failed on the missing verifier; all **14 synthetic,
in-memory tests** then passed. Fault cases cover changed fields/unpaired reserve,
tail/length errors, raw headers, duplicated subheaders, stale EDC/ECC, unchanged
sector complement, candidate truncation/extension, inverse identity, and CUE
misbinding/extra tracks. Tests do not create or open BIN/CUE fixtures.

Independent original-only prediction passed against the actual USA source:

```text
Original BIN  fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37
Expected BIN  895074e78abcf7de47faa15b62ee340239d996a486d1bc7ef5897c68cd4a06d3
Expected EXE  a6da6e2bc54ea9eaac5e53987853eb96057beaa00cbdf57cbe2f040d26819a6b
Unchanged WAD 7ba8961c3626bcec816fbd6feecc4eff67dc8018ba43974e7838f4c341bf54a1
ISO root      f9a579fb5c468c67824b1cc53ab026167e2a81400e93511ed28dc519e284ca51
Changed LBAs  53875, 54078
Changed bytes 234 raw bytes, including EDC/ECC
```

The original-only result reports `candidateVerified=false`: predicting an exact
hash does not verify a subsequently published file. No BIN path is implicit.

```sh
python3 -B tools/tests/test_collectible_startup_physical.py
python3 -B tools/Verify-SpyroCollectibleStartup.py --usa '/Users/john/Documents/Codex/2026-04-24/could-you-create-a-level-editor/Spyro the Dragon (USA).bin'
```

To check a published artifact, explicitly add `--candidate` with its absolute
`collectible-startup-bootstrap-reservation-r01.bin` path. Optionally add `--cue`
with its same-directory CUE path. The CUE check requires that exact BIN basename,
one MODE2/2352 track and INDEX01 at00:00:00; omission reports `cueVerified=false`
and opens no CUE or receipt.

Successful physical verification does **not** prove BIOS loading, cache/IRQ
behavior, genuine native residence/lifetime, Ready admission, save/card behavior,
gameplay, normal export or publication safety. All those result gates remain
false. The proposed zeroed bytes are still NOT-LOADED, not a valid ready ledger.
