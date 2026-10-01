# Native portal routing source audit

The original USA image was authenticated as
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
The independent source reader used frozen Core source `3c1aa36` in
`work/universal-gameplay-20261001/fixtures/build-source`. These are source and
instruction witnesses; no edited portal has been exercised in gameplay here.

## Portal field owners

All 29 homeworld portal records match `HomeworldPortalControlCatalog`. Each
has a two-node native path. The path actor is class `0x18E`; its row properties
start with a scene-relative pointer to the path header. Header byte zero is
node count, byte one is the current node and signed halfword +6 is reversal.
Nodes begin at +8, have 16-byte stride and XYZ at +0/+4/+8.

The native portal record stores its path actor index at +0x18, return matching
token at +0x1C, center XYZ at +0x20/+0x24/+0x28 and plane points at +0x2C.
The parser historically calls +0x1C `DestinationLevelId`. The native entry
dispatcher obtains its actual travel destination from the type-6 special
surface descriptor, whose +4 is destination and +8 is portal index. Its
runtime +4/+8 fields are reached after the event dispatcher skips the type word.

Lettering actor class 1 properties contain the four-byte level-name table
index: Stone Hill 1, Toasty 4 and Wizard Peak 15. Retargeting visible lettering
requires the level-name table index rather than the game's level ID.

`MobySourcePatchExporter` already moves the linked three actor rows, plane
center/points, every native path node and dedicated type-6 collision triangles.
It rebuilds collision lookup membership. Thus moving a portal's source path
translates its native return fly-out endpoints as well as its walk-in entry.
Decorative arch terrain remains independently editable.

## Native return selection

The native executable uses these globals:

| Runtime address | Observed role |
| --- | --- |
| `8007596C` | Current level ID |
| `800758B4` | Transition target level |
| `800758AC` | Return portal matching token |
| `8007576C` | Active portal route index, -1 for ordinary exit |
| `800758BC` | Loaded homeworld portal count |
| `80088640` | Loaded portal pointer table |

Two independent arrival passes compare portal record +0x1C against
`800758AC`: camera/sky setup at EXE file `0x616C` / runtime `8001596C`, and
the actor path launch at file `0x670C` / runtime `80015F0C`. The path pass
branches on unequal at `80015F14`; its delay slot increments the portal
iteration index. A matched record's +0x18 chooses the row and its private path.
The reversal at properties +0x10 chooses whether path nodes +8/+0x18 are
launch/landing or landing/launch (`80015F7C` through `80015FF8`).

The normal scene spawn is applied only when the return matching token is zero
(`80013768` through `80013778`). The path setup clears the token at
`80016360`, after it has established the flight-out movement. The common
transition helper `8004AC24` does not compute the homeworld or overwrite these
route globals.

Maintaining each homeworld portal record's original unique +0x1C token permits
two walk-in portals to share a destination while still selecting one explicit
return path. This avoids changing both native arrival searches. Source table
users inspected for loading and rendering use the record's sky pointer,
world-sector index, plane and center; the two return searches are the observed
consumers of +0x1C. A new runtime retake remains necessary for the complete
round trip.

## Exit producers and patch boundaries

All 23 ordinary levels with a class-9 Return Home actor have a bounded class
handler closure which stores the current level token, derives
`floor(levelId/10)*10`, stores the transition target and calls `8004AC24` with
argument zero. The source register differs in Tree Tops, Metalhead and
Haunted Towers; patching a guessed fixed `sw a1` pattern misses those owners.
Representative common-transition callsites are Stone Hill `8007E090`,
Town Square `8007E07C`, Toasty `8007B528` and Wizard Peak `8007BB94`.

Pause Exit has its own complete 176-byte executable routine at file
`0x1CE64..0x1CF14` / runtime `8002C664..8002C714`. It resets the camera through
`80058C7C`, sets the native transition flags, stores route -1, stores the
current level as matching token and writes the derived homeworld target. Any
rescheduling must preserve its stack and saved return address, caller effects,
all flag stores and R3000 load/branch delays.

The flight/post-reset state has a separate homeworld division block at file
`0x1F850..0x1F8A4` / runtime `8002F050..8002F0A4`. It sets target homeworld,
transition phase 2 and route -1; it previously zeroed the token at file
`0x1F83C`. This path must be addressed explicitly if configured flight exits
are to use the selected portal instead of the ordinary homeworld spawn.

The hidden warp path at EXE file `0x1DF9C` also calls the common transition
helper after deliberately clearing the token. It is a distinct producer and
is not a Return Home mapping owner.

Private exact reports are `portal-source-analysis.json`,
`portal-source-analysis.return-closures.json`, `.return-sites.json`,
`.exit-calls.json`, `.child-surfaces.json` and the read-only executable/overlay
copies under `work/universal-gameplay-20261001/fixtures`. The local
`disassemble.py` prints the corresponding source words.

## Required acceptance

Verify store save/reopen and changed-input refusal; source byte preimages;
normal production export and independent readback; all 29 entry mappings;
same-homeworld and cross-homeworld destination changes; two portals sharing
one destination with an explicit return endpoint; all 23 Return Home class
owners; Pause Exit and flight exit; compatibility with save-row/egg/sign
update wrapper metadata; and preservation of other archive entries. Native
testing must cold-enter the edited portal, use ordinary Return Home and Pause
Exit, visibly fly out from its moved position, re-enter and inspect inventory
and saved progress. Readback alone does not establish those interactions.

## Exact instruction certificates

The 23 ordinary Return Home callsites are:

| Level | Common transition call PC |
| --- | --- |
| Stone Hill | `8007E090` |
| Dark Hollow | `8007B424` |
| Town Square | `8007E07C` |
| Toasty | `8007B528` |
| Dry Canyon | `8007E634` |
| Cliff Town | `8007E62C` |
| Ice Cavern | `8007B8B8` |
| Doctor Shemp | `8007B49C` |
| Alpine Ridge | `8007BEF4` |
| High Caves | `8007BA28` |
| Wizard Peak | `8007BB94` |
| Blowhard | `8007B52C` |
| Terrace Village | `8007B9C8` |
| Misty Bog | `8007B5B8` |
| Tree Tops | `8007BA8C` |
| Metalhead | `8007BB58` |
| Dark Passage | `8007B8D4` |
| Lofty Castle | `8007B8D0` |
| Haunted Towers | `8007B8EC` |
| Jacques | `8007B548` |
| Gnorc Cove | `8007B914` |
| Twilight Harbor | `8007B48C` |
| Gnasty's Loot | `8007B35C` |

An uninterrupted 52-byte source packet begins 44 bytes before each call,
including the `lui at,8007` before the matching-token store and the call's
`a0=0` delay slot. Five exact signatures cover all 23 variants. No source
class-9 closure branch enters a packet interior. The complete exact packets,
PCs and SHA-256 certificates are in `portal-return-packets.json` under the
private fixture directory.

The only direct Pause Exit caller is `8002E0E8`; its delay slot is zero. It
then jumps to the restoring epilogue at `8002E11C`. No added volatile register
clobber is consumed. A complete original EXE direct-edge scan finds no branch,
J or JAL target inside `[8002F050,8002F0B0)`, including its first instruction:
this flight owner is reached by fall-through.

The actual indexed LevelNames pointer table is at EXE file `0x5FFF0`.
Selectors 0, 6, 12, 18 and 24 say `HOME`; selector 30 says `GNORC GNEXUS`;
35 is the retail placeholder `A` replaced by the authored-level compiler;
36 says `RETURN HOME`. Thus `(levelId / 10 - 1) * 6 + levelId % 10` is the
native doorway-lettering selector, including homeworlds and flights.

The original Town Square initializer owns `8005A898..8005A930` (152 bytes,
SHA-256 `fe4669229aedbbc8ff698d57a237327af36025aaeda0ff5cc2e1c806fd23e50b`).
Only original table ID 13 enters that block. ID 65 becomes a Town alias when
the authored-level compiler selects that initializer. No original direct
branch/J/JAL enters the block interior. Its common tail at
`8005B6E0..8005B6F8` copies `85D8` into `85DC` and returns, SHA-256
`19566edf84d04523d6cc943feb5107817297413669d3838b10ef868db744151d`.
The dispatcher `8005A470` has one direct caller, `800155F4`. The continuation
redefines v0 at `800155FC`, v1 at `80015634` and t1 at `80015624` before
consumption; neither t0 nor t2 is consumed on either outgoing state edge.
The state either branches through `800156A4` or calls `80016698`, then
finishes at `800163B4`. An initializer split may therefore clobber those
volatile registers while preserving a0, saved registers and ra.

## Production fixture receipt

The isolated Release smoke first passed 1,654 checks using frozen Core
`3c1aa36` plus the three new portal modules. It covered all 29 entry owners,
every native destination ID 10..64, all 35 retail level callback bindings
(including six homeworlds, five flights and Gnasty Gnorc), 22 exported ordinary
class-9 owners and the 23rd owner's exact packet certificate. Original
Twilight Harbor is refused because wrapper growth exceeds the current safety
margin by 400 bytes; that refusal is preserved rather than advertised as an
export success.

Artifacts are in
`work/universal-gameplay-20261001/fixtures/portal-production-r05/`:
`source-store-checks.json`, `export-matrix.json`, `fixtures.json`, saved normal
Moby manifests and four BIN/CUE pairs. Core MVID was
`bfca10c4-a912-4c4d-a404-1a66eb4c54ef`, Core SHA-256
`adc6178d0feb1465720497c3aca86de7daa94162fdeae7318b058f707687e89a`.
The first `stone-to-toasty.bin` SHA-256 is
`b98ad9908de2919b9d372936257e06147d9bafece86834559a674c9f8163f89a`.
Receipts carry the other exact hashes. All 256 WAD entries in every image were
independently read back against staged expected data; executable bytes were
also independently read back.

Normal production `MobyEditStore` and `MobySourcePatchExporter` move the
Artisans Stone Hill control triplet by +256 raw X. Both native arrival path
nodes, plane center/points and collision trigger move with it. The fixture
redirects that physical doorway to Toasty and returns Toasty through Stone
Hill's retained token 11. Three alternatives exercise duplicated Toasty
entry destinations with one explicit endpoint, a Wizard Peak cross-homeworld
route, and a Sunny Flight return route.

Toasty's class-9 Return Home actor row 52 moves from raw
`[105667,117084,19421]` to `[129239,114145,15837]`. Its floor is ordinary
collision triangle 12125, vertices `[8046,7230,990]`, `[8174,7102,990]`,
`[8046,7102,990]`. The new XY is inside this triangle and preserves the
original -3 raw Z offset to floor. The class `0x011E` ambient-sound companion
row 56 moves by the same delta; its private three-node path remains native.
That path's use as a Spyro outgoing-flight path is not inferred.

The class-9 planar trigger at `8007B370` requires native distance below 1024
raw. Distance helper `80017990` computes max(|dx|,|dy|) + 3 min(|dx|,|dy|)/8.
The fixture's -512 X/-512 Y offset is 704 raw from normal spawn, within this
horizontal range; the vertical check at `8007B390..8007B398` requires
Spyro Z minus actor Z in 513..16383 raw. Normal spawn `[129751,114657,16200]`
has difference 363 and thus misses the vertical trigger. A raised fly-in may
need a wider horizontal separation. Root owns runtime testing and can judge
that behavior; this worker has not run an emulator.

Run the new smoke with original BIN, catalog workspace and a new private
artifact directory. No option checks source/store behavior; `--matrix` adds
composition/owner checks; `--export` also produces normal movement and routed
BIN/CUE fixtures. It refuses to overwrite an existing artifact directory.

A subsequent source-owner matrix passed 683 checks without generating more
discs. It independently limits destination writes to the original descriptor
and lettering bytes, checks original bound overlay bytes outside the exact
class-9 JAL owners, and verifies unrelated callback bindings are retained.
Its receipt is in `fixtures/portal-owner-matrix-r07/export-matrix.json`.
