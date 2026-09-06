# Source-bound native Moby dependency closure

Research-only, 2026-09-05. This is a new absent-resource Town Square candidate,
not an editor capability or a gameplay certification. Original images, existing
candidates, the installed app and normal Create BIN remain unchanged.

## Reusable compiler boundary

`Scene/NativeMobyDependencyClosure.cs` is an internal detached compiler. Its
code-owned source factories declare actual original reads, exact final address
spaces, semantic edges, imported builders, native reference preimages and final
resident preservation spans. Authored JSON, actor names, cached catalog labels
and caller-supplied ownership metadata are not native write authority.

The compiler recursively selects a complete reachable graph before extraction.
It distinguishes aggregate, destination-resident, executable/global, importable,
conflicting and unrelocatable dependencies. Unknown kinds, dangling identities,
duplicate node IDs, stale full read hashes, unresolved payload recursion and
unbound cross-domain references fail closed. Valid semantic cycles and
self/mutual address-only pointers are supported; materialization first allocates
detached outputs, then resolves checked references.

Immutable resources share one graph identity. Mutable instance rows/properties
require a reachable canonical instance owner and exactly one private install.
Another instance cannot consume that private storage, even through a shared
helper. Referencing another *instance* is not ownership of that instance's
mutable storage. Every planned install must avoid all resident final spans and
all other installs. Overlapping resident promises must require identical bytes.
The observable node/read/preservation/install/reference order and binary plan
fingerprint are deterministic.

This is not a universal native schema decoder. Factories must still prove each
family's actual dependency grammar, mutable extent ownership and native address
encoding. The Town Square factory's exact-source descriptor comparisons must
not be reused as unchecked generic model equivalence tests.

## First source-bound factory

`Exporting/NativeTownSquareLockedChestClosureCompiler.cs` implements profile
`native-closure.peace-ae.townsquare.properties-and-sound.v2`. Its sole source is
the exact USA MODE2/2352 original, SHA256
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
It reads the actual ISO directory and WAD table; no WAD-analysis JSON or saved
level catalog supplies its extraction/ownership decisions.

Native source bindings:

| Resource | WAD offset / length | SHA256 |
| --- | --- | --- |
| Town overlay entry 15 | `0x118E800 / 0xF800` | `9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5` |
| Town data entry 16 | `0x119E000 / 0x2E2000` | `7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0` |
| Peace overlay entry 21 | `0x187E000 / 0x12800` | `299f4822489c48736c9f46bb9556e82952fc1693f5f9f42bc2e6415c1908aebc` |
| Peace data entry 22 | `0x1890800 / 0x29E800` | `c140bd422471c66cd57bd85b29912819f798f5b080779153674714889306c9d3` |

Town has 37 populated actual class registrations and no `0xAE` root. Its actual
`0xAE` and rootless `0x0D` dispatch paths reach the default jump at `0x8007DC4C`
and `0x8007DB80` (`0x08021C96`, delay NOP). Native Key `0xAD` dispatch remains at
`0x8007DBD8`/`0x8007DBDC`, branching to `0x80083B4C`. Neither the absent chest nor
the rootless key/reward controller is inferred from a placed-actor label.

The graph drives 159 actual installs: native handler extraction/relocation,
dispatch hooks, one complete animated `0xAE` package, 146 textured-face remaps,
four native tile/CLUT allocations, actor registration, outer subfile offsets,
seven private rows and property blocks, property-component growth, scene
fixups, runtime copy-buffer relocation and treasure/navigation EXE patches.
The single-animation package owns its animation bytes; this factory does not
split or deduplicate animation interiors independently of that native package.
Rootless Key and reward-controller nodes explicitly retain their native handler,
renderer, effect, sound and owner-index obligations.

Immutable model, behavior, texture, native global code, particle/debris and sound
resources are shared. T107 Key, T108 Chest and T109–T113 reward markers each have
independent source rows and genuinely owned mutable properties. Global sound IDs
0 and `0x37` use resident native maps. Critically, model header byte `+4` is a
*local sound descriptor*, not a mapped global ID. Peace local 47 must become
Town local 54; both own the exact same 0x1430-byte ADPCM sample with SHA256
`f10bef3c98281716bf79cb09c77ff6017c2959d10f11ba4f0b9cda1868c977c3`
and equal non-address playback metadata. Town local 47 is a different sound.
No native animation, damage, child effect or sound is suppressed/substituted.

The all-consumer native texture scanner proves every new tile/CLUT byte is
outside existing ownership and has the exact zero source preimage. Native
resident debris models 0x135–0x137 have equivalent geometry/animation with
repacked descriptors and byte-equal metal pixels/CLUT. Key particle 0xC likewise
has equivalent native pixels/CLUT. Native global initializer and overlay tables
bind the correct Town debris and particle callbacks; particle 0x46 uses native
line/vector code without a level-local texture.

## Corrected native properties ownership

The old Town Square v1 recipe stored new property bytes at `0x28A0` inside spare
Moby-row storage. V2 does not inherit that ownership assumption.

The actual scene components are:

| Original scene component | Native start | Native size | V2 change |
| --- | --- | --- | --- |
| Moby rows | `0x168` | `0x4CD0` | Count 107→114; only seven zero, reserved rows populated |
| Moby properties | `0x4E38` | `0x10D4` | Size→`0x11B8`; append `0xE4` at `0x5F0C` |
| Pod | `0x5F0C` | `0x18` | Move intact to `0x5FF0` |
| Collision | `0x5F24` | `0x2004` | Move intact to `0x6008` |
| Pointer fixups | `0x7F28` | count 129 + 129 words | Move to `0x800C`, count→136 |

All 129 native pointer fields and targets precede `0x5F0C`, so their exact
registrations and values remain unchanged. The seven appended row pointers are
resolved by the graph to private properties inside `[0x5F0C,0x5FF0)`. Donor
native fixup enumeration proves these seven private blocks have no internal
scene-pointer fields. Owner indices 78→107 and 79→108 are integer instance
indices, not pointers. Coordinates are translated coherently for rows and reward
properties. The genuine properties component—not a zero run elsewhere—owns all
new mutable bytes. The full native pod/collision suffix is byte-identical.

No extra scene subfile sector is required; its `0x8800` capacity has proven zero
tail after the original fixups. Actor subfile growth remains `0x1800`, overlay
growth `0x1000`, whole WAD growth `0x2800`, relocated EXE LBA 53875→53880.

## Regression and publication gates

The old Town Square exporter has only pure-builder/delegate refactors. Its old
logical component hashes remain oracle checks, not inputs to the new compiler.
The new graph's overlay and EXE are byte-identical to the old native recipe;
data differs only in the corrected scene ownership and the local sound byte.
Other per-destination exporters are not changed. No old candidate is rerun or
recertified by this work.

Initial V2 evidence:

- Source-free generic closure: 49 checks.
- Fresh original-source factory plus native row/fixup/ownership preservation:
  329 total checks; independently rerun by another agent.
- Three deliberate publication faults (after copy, after write, before commit):
  335 total checks; no final artifacts or partial CUE retained.
- Graph: 276 nodes, 159 installs, 8 resolved native scene/model pointers.
- Graph SHA256: `d484fc13504ac0afc0b7a63e81dd4a6a3a3a7672dd7959654b02fd6103a3edcc`.
- New data SHA256: `e442a02d3aa899dca0d5ddac4392f4b602aabb7ac6dc5dd1e4616c00fab6908d`.
- Overlay SHA256: `fd859bff13985e18c3aff74cc889006c48b2743bf57a2b0f9d80662d25a09c2d`.
- EXE SHA256: `4268f9943233b9f8c24d1d4b0924cba8747547b44b82eddd13f0a2ac10ac17de`.

Additional bounded regression hardening after publication: 68 source-free checks
now include synthetic read-only-stream corruption of protected raw bytes,
payload, duplicated XA header, EDC and both ECC regions; wrong source identity,
length and expected-sector shape; stale CUE/receipt bytes; a rejected tiny fake
source image; and occupied output-directory refusal before extraction, with its
sentinel unchanged. Source-side and output-side late appends are separately
rejected by final stream-length/position checks; the whole image cannot be
certified by hashing an initially sized prefix. The same streaming comparator
is used by the real publisher. Source CUE binding is checked at initial capture,
late recapture and retained verification, independently of CUE generation's
missing-file fallback.
Read-only replay of the retained V2 candidate passes 350 checks, reconstructing
the exact original receipt/CUE and the complete BIN, and refusing to overwrite
the occupied immutable candidate. A fresh three-stage fault rerun passes 354
checks and leaves its own temporary output directories empty. Only the one
successful V2 candidate BIN is retained.

`Exporting/NativeTownSquareLockedChestClosurePublisher.cs` constructs the physical
WAD from the actual source table, uses the shared complete-ISO-inventory owned
gap/EOF preparation, and publishes BIN, receipt, then CUE via new-only exception
rollback. It rejects occupied output directories, recaptures fresh source/CUE
before commit and compares the entire output image against independently
reconstructed raw sectors, including relocated data, unchanged complement,
gap ownership, EOF flags, MSF and rebuilt EDC/ECC. Final companion bytes and CUE
binding are also exact. This is not crash-atomic or hostile-filesystem-race proof.

Command entry points (run from the repository root):

```sh
dotnet run --project src/Spyro.Editor.NativeMobyDependencyClosureSmoke -c Debug
dotnet run --project src/Spyro.Editor.NativeMobyDependencyClosureSmoke -c Debug -- 'Spyro the Dragon (USA).bin'
dotnet run --project src/Spyro.Editor.NativeMobyDependencyClosureSmoke -c Debug -- --publish 'Spyro the Dragon (USA).bin' 'Spyro the Dragon (USA).cue' <new-output-directory>
dotnet run --project src/Spyro.Editor.NativeMobyDependencyClosureSmoke -c Debug -- --verify 'Spyro the Dragon (USA).bin' 'Spyro the Dragon (USA).cue' <published-output-directory>
```

The first candidate location is
`_local/research/native-moby-closure-20260905/town-square-native-closure-v2-r01/`.
Its receipt is authoritative for the immutable final BIN hash and physical
verification. It includes the native navigation sequence, exact object indices
and positions, and an explicit `runtimeProven=false` gate.

Published V2 r01 BIN SHA256:
`d94d90b51c15b82328b6dbeb67474e563ddefc7d7f6c81bd4b61c526c58083fe`;
44,671 changed raw sectors. CUE SHA256:
`dc37b91922593355aa072e12b4525db925280b25adc7bba2423238ef6cad2674`.
Receipt SHA256:
`85f6806d7debdc08ce3d150a57265d3530ab8f37aa1adaa9f7dc6abb6405d46c`.

Runtime must separately observe the key, pre-key locking, native open animation,
correct sound/effects/debris, all reward gems, death/reload and repeat-entry
gating, nearby actors/collision/camera and normal exit. Neither structural
verification nor the old Artisans runtime result satisfies those new gates.

The exact native reward composition is **six gems: four green and two red,
worth ten total**, not five green gems. Chest T108 itself emits one class `0x54`
green gem. The five class `0x0D` controllers T109–T113 have source reward-selector
bytes `+0x53` equal to `54,53,54,53,54` (hex), emitting three additional green
gems and two red gems. Their donor rows preserve those bytes exactly. Native
emitter `0x8003ABC0` uses `source[0x53] & 0x7F` as the spawned class; its second
argument is a motion mode, not a gem count. The collected-gem native code at
`0x8003BA64..0x8003BAB8` awards class `0x53` one unit and class `0x54` two units.
Thus the existing 200→210 treasure target is correct. The five controllers are
not themselves the entire six-gem reward family. See
[native pickup and reward conditions](research/native-town-square-key-pickup-conditions.md).

## Experimental spatially separated V3

Runtime preparation exposed an exact V2 placement conflict: preserved native T27
class `0xC3` and imported T108 class `0xAE` both occupy
`(132198,111032,11264)`. V2 remains immutable and reproducible, but interaction at
that occupied site is confounded. No native actor was deleted or moved to hide it.

The explicit code-owned `SpatiallySeparatedV3` recipe selects profile
`native-closure.peace-ae.townsquare.experimental-spatial-separation.v3`.
The default `Capture`, publisher and verifier APIs still select V2, including its
original graph fingerprint and byte-exact retained receipt. Unknown recipe enum
values fail closed; a receipt cannot authorize selecting a new recipe.

V3 translates all seven imported rows and the five reward-property XYZ records
by `(-10518,-8744,-3072)`. Key T107 is `(119632,102288,8192)` and chest T108 is
`(121680,102288,8192)`. The new `placement.site` graph node reads the validated
native data/EXE, certifies the fixed site and produces the coordinates consumed
by every translated row/property builder. All 107 original native rows and their
property storage remain byte-identical; ownership, pointers, model, texture,
sound remap, behavior overlay and EXE remain unchanged from V2.

The placement gate proves complete native dry-floor support for authored test
squares, native collision-index reachability, and no terrain intersection in
their tested columns. Actual EXE flag decoding proves every support triangle
selects no special surface. It additionally applies an explicit experimental
policy: more than 3072 raw horizontal units from every original native actor
center, without exempting other heights. This is **center separation, not a mesh
or physics envelope proof**. The authored 512-halfwidth test squares do not claim
that every imported model fits inside them; renderer clipping radii are not used
as world-space physics bounds. Moving actors, actual visual separation and the
complete native interaction loop still require gameplay. See
[native spatial evidence](research/native-town-square-spatial-site-evidence.md).

V3 regression results: 386 source-bound checks, 392 checks through three deliberate
publication faults, and 389 checks replaying the retained published candidate.
The unchanged V2 replay passes 368 checks. Whole-data V3-versus-V2 comparison
permits differences only in the exact seven row-XYZ and five reward-property-XYZ
extents; overlay and EXE compare byte-identical. The occupied V2 site deliberately
fails the new placement certificate. The new graph has 277 nodes, 159 installs
and 8 resolved references; no extra resource installation is implied by the
placement node.

The immutable V3 directory is
`_local/research/native-moby-closure-20260905/town-square-native-closure-v3-r01/`.

- BIN SHA256: `15df0ee1edba6cb48fe1d34df5a5f51a7d6f289de387c499ca0ff04e99b443bf`.
- CUE SHA256: `3b44912bd798c9968197012f045c4663e9450412cf5257ba87aa25405a9ffbbf`.
- Receipt SHA256: `a466629272cc2c0ff385d52cab29452ef7056d700bf038d4181e1e977d527c2c`.
- Graph SHA256: `52c68e8595a21ef8807abb6090dfb02d84a8643af114346c3c7c962247c45d6d`.
- Logical data SHA256: `880c487599e8382447cd7b5c2d1799abec4eafd5af26351f37a22f66ed8ec9b0`.

Use `--source-v3`, `--publish-v3`, `--faults-v3` and `--verify-v3` on the same smoke
entry point for explicit V3 operations. Normal Create BIN remains disabled for
this research recipe. Its original native landing is unchanged. An optional
spawn-only helper was compiled but not executed; source only is retained in
`_local/research/native-moby-closure-20260905/v3-placement-source-evidence/unused-spawn-helper-source/`.
The independent read-only geometry/mesh probes are retained alongside it.

## Portability complexity roadmap

These are engineering classes and required proofs, not authorization derived
from catalog names. Each actual donor/destination pair must independently pass
native source discovery, structural closure and gameplay acceptance.

| Complexity class | New obligations | Next acceptance boundary |
| --- | --- | --- |
| Model-only static resource | Complete native model grammar, native root/ID registration, exact textures/CLUTs and all-consumer allocation | Correct rendering and culling with resident behavior unchanged |
| Animated resource | All animation tables/streams, frame/face interiors, model-local sounds and runtime loader assumptions | Every exercised animation and sound, not just idle pose |
| Model plus absent behavior | Level overlay dispatch, relocation of all branches/calls, rootless controllers, owner indices, private mutable state, copied runtime buffers | Full native interaction and reward loop; this Town Square family is the current candidate |
| Native child/effect family | Recursive spawned Moby classes, particle dispatch/data/texture, native sound samples, renderer/global callback bindings, cleanup paths | Child creation, impact, repeated use, death/reload and coexistence with existing effects |
| Player-animation hard dependency | Native player package/animation streams, damage-state transitions, global/player consumers and any associated sound/effect dependencies | Native damage sequence and recovery work without suppression or substitute animations |
| Multiple imported instances/families | One shareable immutable resource identity with disjoint native mutable instances, cross-family allocation/index conflicts and reload/reset lifetimes | Simultaneous independent behavior and death/reload; pairwise structural success alone is insufficient |

The existing adapted Toasty Wizard route suppressed unsafe lightning children
and substituted generic hurt because the destination lacks player electrocution
animation 8. It remains useful relocation evidence but does **not** pass the
native child/player-animation classes above. Its exact prior gameplay evidence
must not be broadened by this roadmap. Do not begin another large family until
the current native candidate's gameplay gate is resolved.
