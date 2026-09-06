# ID65 native engine-profile prerequisites

`NativeId65EngineProfileContract.CaptureAsync(originalBin)` is a read-only,
exact-USA source contract. It emits no patches, resources or candidates. It
does not change the existing ID65 lab, schemas, save/music compilers, writers,
portal guards or ordinary Create BIN behavior.

The immediate purpose is to make the missing native engine prerequisites
explicit before an independently authored ID65 data producer is attempted.
Retail ID65 dispatches to `8005B6E0`, which only copies the previous
`g800785D8` into `g800785DC`. It does not initialize an empty callback set.

## Source authority

The public factory hashes the same open original image before and after its
reads; it requires the exact MODE2/2352 layout, original WAD/SCUS ISO extents,
complete WAD header, executable, Town overlay/data, and PETE header/native
copy. A caller catalog, callback name, hash string, candidate or prior receipt
cannot issue the contract. Source-free fixture decoding stays internal and
does not claim native source authority.

- Original image SHA:
  `fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
- Original executable SHA:
  `a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`.
- Town overlay WAD `118E800`, length `F800`, runtime base `8007AA38`;
  SHA `9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5`.
- Town data WAD `119E000`, length `2E2000`;
  SHA `7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0`.

Snapshot collections and source bytes are detached. `RequireCapturedDomains`
compares its three complete captured EXE/overlay/data owners, including the
untouched complement; it is not a disc-freshness or publication protocol.

## Ordered native callback and buffer owners

Town initializer EXE `4B098+98` installs buffer `80089ECC`, then these eight
callbacks in the displayed order, before jumping to the shared tail. The
decoder requires the actual LUI/ADDIU/LUI/SW words, signed low halves, outgoing
jump and its delay slot. Each complete overlay callback interval is separately
source-hashed. A non-null address or a caller-supplied function label is not
enough.

| Global | Native target | Responsibility / observed register inputs |
| --- | --- | --- |
| `800758CC` | `800872A4` | Moby spawn: class in a0, nullable source row in a1, returned Moby in v0 |
| `80075734` | `8007DA54` | Active-row and animation dispatch through native global lists |
| `800758E4` | `80088F68` | Particle count/selector in a0/a1; selector-specific a2/a3 |
| `800756BC` | `80087E20` | Particle tick, native delta in a0 |
| `800757A0` | `8007AF2C` | Balloon transition begin from native actor/properties |
| `800758A8` | `8007B068` | Balloon transition end/reset |
| `8007574C` | `8007B0AC` | State12 balloon update |
| `800758D8` | `8007D04C` | State12 balloon render |

No unconditional no-op is authorized. Active-Moby dispatch performs shared
list/animation maintenance before its empty-list exit; particle update can
return on its native empty sentinel, but player behavior can populate the pool
without any authored effects. Omitting the balloon family needs a separate
no-state12-entry proof, not merely zero placed balloonists.

## Mandatory local class0x78

The scene loader requests class `0x78` when its load argument is nonzero and
native flight classification is zero, with a null source-row argument. The
returned row is stored at `g80075898`. This occurs independently of authored
Moby count.

The contract independently parses all35 retail native headers: each registers
class0x78 locally. PETE's54 shared class registrations do **not** contain it.
Town slot20 owns data `1C9C7C..1CC5AB`, WAD `1367C7C`, length `2930`, SHA
`8e0d1cf58e8b164734f4aa3e91a60a1d04e78bb1f5e4cd5c4432b2e3e1e743f1`.
Native model-table slot is `80076558`.

Source prerequisites include:

- Six15-frame paired-word animations, not only the initial animation0.
- Model-local sound descriptor indices `48/49` hex, selected directly by the
  bound native resolver `8003851C` (EXE `28D1C+A0`), not global selector/map
  keys. The indices alone do not prove that a future destination has installed
  equivalent descriptors and complete samples.
- Thirty-six bounded native textured commands, page `1D`, palette `7A36`;
  the associated pixel/palette owners remain a separate prerequisite.
- Native allocation of one `58`-byte dynamic row plus `18`-byte private
  properties; exact default/init behavior and the null-source branch.
- Complete update case `80082FB8..80083B4B`, which is inside the dispatcher
  and depends on its saved-register/stack setup—not a standalone function.
- Source-bound direct helper-call roots, including native movement, sound,
  auxiliary state and frame/collider consumers. This list is not a complete
  transitive/indirect dependency closure.
- Particle selectors `42` and `0C` hex. Status-dependent companion effects
  occur without authored effect rows; auxiliary state allocation/release must
  also be retained or closed separately.

## Deliberately pending

`IndependentLevelSupported`, `NoOpCallbacksAuthorized`,
`CompleteNativeDependencyClosureProven`, `PublicationApproved` and
`RuntimeVerified` are always false. Pending prerequisites are returned as an
immutable list: independent native data/scene/collision/properties/fixups,
texture and sound ownership, companion/player particle closure, auxiliary and
peak memory ownership, complete transition-state consumers, existing ID65
identity/music/classifier composition, and normal arrival/save/reload/runtime
acceptance.

The first future profile can retain exact native engine code while producing
independent scenery. This contract does not claim that Town's overlay is
minimal or independently relocatable, and does not convert an existing lab
receipt into permission to add a new native level.

## Regression evidence

Intentional RED: the new smoke failed with16 missing-contract CS0103 errors;
Core built clean. Initial GREEN: Debug build5.40s, zero warnings/errors;
112 source-free checks and145 exact-source checks. Initial contract SHA:
`c112542983f96c87b26a7f85d69ef6d6c4e80cab2367182e9d89c76c954fb4c3`.

Final GREEN after independent review: Debug build 5.15s, zero warnings/errors;
115 source-free checks and 148 exact-source checks. Final contract SHA:
`cbd16852b72304695de92b78d4b1f9fc6e890daa434cff495f7b7f6ac03a8b30`.
The review corrected the local sound descriptor terminology; it found no
material source-authority, byte-owner or resource-budget issue. The final
three guards reject public metadata issuers and an invalid one-byte source
image. Neither test decoding nor correct-looking metadata can grant native
source authority.

Commands (from the repository root):

```sh
dotnet build src/Spyro.Editor.NativeId65EngineProfileSmoke/Spyro.Editor.NativeId65EngineProfileSmoke.csproj -c Debug --nologo -v minimal
dotnet src/Spyro.Editor.NativeId65EngineProfileSmoke/bin/Debug/net10.0/Spyro.Editor.NativeId65EngineProfileSmoke.dll
dotnet src/Spyro.Editor.NativeId65EngineProfileSmoke/bin/Debug/net10.0/Spyro.Editor.NativeId65EngineProfileSmoke.dll 'Spyro the Dragon (USA).bin'
```

The smoke covers every initializer/tail instruction, callback duplication and
reordering, malformed model/frame owners, decimal-level aliases, missing local
class ownership, immutable snapshots, all35 source registrations and corruption
of every callback body plus protected save/music/portal/source complements.
No runtime or Free Flight candidate was rebuilt or republished by this work.

Raw evidence is retained under
`_local/research/native-id65-engine-profile-20260906-r01/` (`red-build.log`,
`first-green.log`, `final-green.log`, and the detailed read-only
`next-producer.md`).
