# Source-issued particle descriptor append

This adds a detached indexed-particle descriptor codec and an opt-in resource
composition stage. It does not install particle spawn/tick code or prove native
effects, runtime, or publication. The default resource API and its two byte
goldens remain unchanged.

## Three different native namespaces

The descriptor loader at EXE `0x800131A4..0x8001322C` reads a size-prefixed
component, a signed record count, then records with signed-halfword selector and
payload length. It writes the record address to `0x80076278 + 4*selector`, advances
by `4 + payloadLength`, and finds the following sound component using the outer
component length. Certificate: EXE offset `0x39A4`, length `0x88`, SHA-256
`3919736090b47e74391a4eff0bce53a9aa009dac25f0a9151356a7fbe46e4f5a`.

The known nonaliasing descriptor region ends before `0x80076378`: descriptor ID64
would overwrite exactly the player/model root0 pointer loaded at `0x8001324C`.
This is a conservative source-derived ownership boundary, not a declaration that
every unused byte was independently allocated to a 64-entry table. Its certificate
is EXE `0x3A48 + 0x10`, SHA
`51ad7f5df52c80e5a6796e8423310a9d2849eec0b38f00a3038874c9d92d6736`.

Therefore the codec accepts descriptor IDs0–63, at most64 unique records, and the
observed eight-byte payload grammar. These IDs are not the `0x00..0x50` effect
callback selectors and not the 512-entry Moby class namespace. Negative IDs,
ID64, duplicate IDs, unsupported/signed lengths, inconsistent counts, truncation
and unowned trailing bytes fail closed before output allocation. The codec's
maximum component size is `8 + 64*12` bytes.

Fresh exact-source capture round-trips all35 native particle components and428
records. Every original record has an eight-byte payload; the largest component
has18 records. Observed IDs are0–33 with gaps. A structurally valid detached codec
result is not a native source certificate.

## Exact Dark Hollow stage

The public `NativeDarkHollowWizardParticlePlan.CaptureAsync` hashes the unchanged
original USA image before and after reads, pins the WAD directory and complete
EXE, and consumes the existing source-issued texture plan. It cannot be constructed
from caller metadata. Source image SHA remains
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.

Dark's actual particle component is data `0x128F7C + 0x8C`, count11, SHA
`b3594b40a08a83e32f464f7717379280706a35a92b2955ea922201ca37820e49`.
Its original descriptor order is `15,18,02,21,0C,0B,0A,09,01,00,06` (hex).
Descriptor7 is absent; no resident entry is replaced or silently deduplicated.

The donor record is Wizard Peak data `0x173518 + 0x0C`, source bytes
`0700080020e02d753fff3800`. The already ownership-checked texture producer's
pixel/CLUT relocation yields `07000800e039660eff582800`. The new stage retains the
exact source/after patch relationship, appends this12-byte record at original Dark
data `0x129008`, and owns only two existing scalar fields: component length
`0x8C→0x98` and count `11→12`. All eleven original records remain byte-exact and
in their original order. The next sound component begins12 bytes later.

`NativeLevelSoundLayout.ParticleOffset` is a read-only internal capture from its
existing six-component/portal walk; no shared parser traversal was relaxed.

## Opt-in one-layout composition

`NativeDarkHollowWizardResourcePlan.CompileWithParticleAsync` adds the issued
descriptor insertion and its two exact original-address scalar patches to the
existing common layout. The old `CompileAsync` path does not add this stage.
Both sound descriptor additions and this record share one sector rounding for
subfile1. Every sound length/count/map field maps across the12-byte insertion;
the final sound component and SPU bank hashes remain identical to the sound
producer. Full old models, registered padding, authored scene, and all VRAM
bytes remain unchanged relative to the same composition without this stage.

The optional stage still grows the whole level by `0x13000`. It increases native
used-data memory by12 bytes, changing the known normal-buffer gap from `0x45E7C`
to `0x45E70`. This does not reserve stack or prove peak/transient fit.

Independent raw-sector splice oracle goldens:

- Particle stage without scene append:
  `4080e940c46ac9c53712cc97285396ecdcae59e0a369c4e8971eeb49207de8d4`.
- Particle stage with scene append:
  `26e8b7f0bebb49c5c39c0e42ea680428ddd01c6e901ebc53e619d6f39c02be91`.

The original two resource goldens `0ca779c5…d9f4` and `24ff0bc2…9e86` also pass in
the same source run. The registration graph covers its package/header owners;
it is not a whole effect-code closure certificate.

## Native renderer and next code boundary

Kind2 sprite rendering selects `g80076278[selector]` at `0x80057C44..0x80057C58`.
The renderer later reads raw particle bytes `+0x10/+0x11`, uses the low byte as an
eight-byte frame offset, and loads descriptor words `+4/+8` at
`0x80057DBC/0x80057DC0`. Native particle07's spawn tail writes frame0; its captured
tick fragment does not change that field. Particle41 uses allocation kind1, whose
native colored-line renderer does not index this texture descriptor table.
There is no fictitious texture41 registration.

The code integration must preserve donor spawn07's common tail
`0x80089F28..0x80089F34`, mapping only its loop continuation to Dark
`0x8008553C`. The nearby Dark `0x80084CE4` tail is not equivalent: its live `t0/v0`
meanings differ and direct reuse would corrupt the native sprite command byte.
Particle41 tick must continue at Dark `0x8008442C`, not `0x80084410`, to avoid
repeating increments already performed by the incoming fragment. These are
source-derived integration obligations, not installed behavior in this stage.

## Regression evidence

Tests were written first. RED built cleanly and stopped at the unimplemented
descriptor codec. Initial GREEN: Debug5.37 seconds, zero warnings/errors;
aggregate common-resource plus particle checks147 source-free/53 refusals and222
source-backed/64 refusals. Source tests include all35 round-trips, original records,
both old/new golden pairs, reverse-order determinism, corrupted scalar/record/sound
bytes, detached output buffers and exact memory accounting.

Use the existing `NativeResourceCompositionSmoke` noargs and
`--source <original.bin>` commands. The independent `source_composition_oracle.py`
prints both old and opt-in particle goldens without writing a ROM or payload.
Logs are `05-particle-build.log`, `06-particle-free.log`,
`07-particle-source.log` and `08-particle-independent-oracle.log` under
`_local/research/native-resource-composition-final-20260906.t1Krlk/`.
An independent read-only review of the codec, source capture, composition deltas,
tests and this contract found no material issue. It did not repeat the .NET run.

`ParticleDescriptorInstalled` refers only to bytes in the opt-in detached final
level-data owner. Particle behavior, complete native closure, placement, runtime
fit and publication remain unapproved. No candidate, normal export or UI path is
added here.
