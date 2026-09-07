# Independent combined Wizard physical-disc reference

The new physical reference passed532 checks/43 refusals and was independently
repeated by root with identical JSON and empty stderr. It derives an expected
raw image entirely in memory; no candidate, CUE, app or original was written.
This pre-implementation reference is for the next separate closed Disc plan,
not a published image or runtime result.

## Exact allocation

| Owner | New extent |
| --- | --- |
| Appended Host | WAD offset6927000, length11000 |
| Appended Body | WAD offset6938000, length20C800 |
| WAD growth | 21D800 bytes,1083 logical sectors |
| New WAD length | 6B44800 bytes |
| Relocated EXE | LBA54958..55162 exclusive,66000 bytes |
| Extra gap beyond old certificate | LBA55125..55162,37 sectors |
| First untouched following XA owner | PETEXA0.STR at60000 |

The original complete image has281270 raw2352-byte sectors. The new layout
changes exactly1290: root22, WAD header37, and53874..55162 exclusive. Each of
the1083 consumed original gap sectors54079..55162 has an actual checked MSF,
canonical duplicated Form2 subheader,2324 zero payload bytes and valid EDC.
The old1046-sector certificate is reproduced separately, never widened by
changing its declared end. All4838 following gap sectors remain untouched.

The complete recursive ISO inventory has19 owners, including SOURCE and S0
directories; all four path tables agree with the three-directory hierarchy.
Both-endian fields, self/parent records, metadata extents, volume bounds and
directory tails are checked. No other ISO/XA owner overlaps the new WAD/EXE.
Only WAD rows79/80 and the two exact ISO root records change; adjacent81 and
all other complete metadata bytes remain intact.

Root flags are preserved. Original WAD/EXE terminal flags move from53874 and
54078 to54957 and55161. Destination MSF stays in place; complete duplicated
subheaders, EDC and P/Q parity are reconstructed from the actual payload.
An inverse validates every supplied2352-byte afterimage before restoring its
authenticated original preimage, including unchanged owners. The whole
inverse digest consumes these actual returned bytes.

## Full independent goldens

- Expected raw BIN: `c4a70f826b1a9a3d4ad8e36512be354b6205d27ea2c7a314776a51067f35b5a6`.
- New root: `2769092a104860606518faf9cc8188c27257236b471d11153ac714ebba6ee5ee`.
- New WAD header: `4a862b5fa39739d5a6041b8de75aa8801824aed35afe0283f9ad572810580e82`.
- All consumed gap sectors: `903c4e947b7daca77c8189f20ee4f25de111af3bb1fd61b6454693e4d5ade383`.
- Newly required37: `29d642e623d22a53a23c0e8140c0dff77afea4d9d344749be114d129402e13ee`.
- Inverse original: `fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.

These include the exact previously reviewed Body33e54…1bc00, Hostad6d…4fa7e
and EXEa0eb…3fc8dc (track19/static target10). A static target does not establish
project/save history or durable ownership.

Evidence:
`_local/research/dark-wizard-native-closure-20260906/fresh-wizard-disc-space-r01.5ixJ2l/`.
Final source SHA:
`cbfcc52fc5a2cab829e9b07c59ee5d7fe69819b9a1686e2841333c29dd1e5aa8`.
09/10/root11 report SHA:
`4f9058ad67d47715f2fa08736bdbe15dcd809f9c5d944f41832d48a01700b0a2`.
The physical-only mode separately passes92/9 and leaves full-afterimage false.

## Review corrections and limits

Retained real failures found the obsolete gap end, an incorrect flat-root
assumption and misuse of the writer's narrow XA flags for read-only PVD parity.
The final reader preserves legitimate PVD flags without changing the old
writer. Root also caught that early06/07 "inverse" fields only hashed the
original input. Those labels are superseded by09 onward, which executes the
bounded supplied-afterimage inverse and rejects34 added mutations/truncations.
None of these corrections changed the first independently derived image hash.

Every physical/metadata read authenticates a same-pass original per-LBA index.
Nested payload helpers use their own full-original/component hashes, not that
later index. The complete original SHA/length/actual EOF and all helper pins
are checked before/finally. These distinct source contracts are not a hostile
filesystem-race guarantee.

The production sibling must retain actual Executable/Body/Host ancestry and
run complete new geometry/gap checks while reusing the existing source index
and raw codec. Do not widen old Disc/Body APIs. Exact byte assembly and inverse
do not prove installed dispatch, full callbacks, global1F audio, capacity,
hardware, card/history, gameplay or normal export approval.
