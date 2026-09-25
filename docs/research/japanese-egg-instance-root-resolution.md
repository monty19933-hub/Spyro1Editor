# F5 independent RGB roots: native resolution and required hook

The unmodified USA animated path cannot bind different F5 model roots to two
rows. It resolves one entry at `80076378 + F5*4 = 8007674C`. Appending two
models under class F5 makes the last registration replace the first; changing
that entry every frame would affect both rows and is not an instance binding.
No alternate class or per-frame global-table rewrite is introduced here.

`NativeJapaneseEggInstanceRootResolution.Capture` records this negative result
for two actual `NativeJapaneseEggRgbEditPlan` instances sharing one certified
source issuer. It authenticates nine exact USA consumer windows, checks five
lookup instructions, and rechecks the complete RGB-only complement. It exposes
the required renderer join and missing owners, not a materializer or installed
hook. All instance-binding, residency, hook, pixel and publication flags remain
false. Existing assets, integration, candidate and publisher are unchanged.

## Exact native resolution

| Stage | Lookup PC | Behavior |
| --- | --- | --- |
| Scene initialization route | `80014194` | Row class `+36` chooses the class-table entry; negative root selects animated initialization |
| Animated initializer | `800526C0` | Re-reads that entry; sets animation speed and overwrites row `+08` from root auxiliary `+14` (zero for F5) |
| Frame wrap | `8005235C` | Re-reads the class root, then animation header at root `+38 + animation*4` |
| Animation transition | `800523AC` | Same class root; the transition does not preserve a per-row model pointer |
| Animated render | `80020A24` | `LHU t7,36(sp)` then class-table lookup; animation header is subsequently loaded into `t5` |

The entire render selector `80020A14/24` has SHA256
`f923ba46fda84cbf6237a93ec76f0e67f1426f666804392edcd245bb8aed6c36`.
Its reads are exactly row `+36`, row `+3C`, the class slot and selected root
`+38` for animation zero. Planting model addresses in row `+00/+04/+08/+30`
does not redirect it. Row `+00` remains the actor's private-data pointer;
`+08` is not a free root field. The actual registration store at `80015B24`
writes only the one class slot. The new contract retains hashes for all nine
complete bounded windows rather than asserting broader EXE ownership.

## Smallest supported future selector design

Only RGB differs between these variants. All initialization, auxiliary,
animation, vertex, face, frame and CODE bytes are identical. Therefore the
canonical F5 model can continue to serve the current native initialization and
animation-update paths. This reasoning does **not** permit geometry or animation
variants through the same design.

A proposed render interception replaces the class-resolution instructions at
`80020A1C..80020A24` and resumes at `80020A28`. On entry:

- `sp` is the current row, not a usable ordinary stack pointer.
- `gp` is the native class table; `t7` contains the row's class.
- `t6` is the packed animation state loaded from row `+3C`.
- The required output is `t7 = selected loaded root`, with `t6` and every
  other live register/GTE state preserved. R3000 load and branch delays matter.

For a nonmatching row, the trampoline must replay the original lookup and use
the canonical result. For an explicitly owned F5 row in the current loaded
scene generation, it may select that row's separately loaded immutable model.
The original suffix then loads the selected animation header; draw commands
capture its current and next frame references and the original row identity.
No shared class-table write is needed. A generic function call using `sp`, or
an unscoped comparison against two hard-coded row addresses, is not a proved
implementation of this ABI.

This slice supplies **no trampoline bytes**. Tests prescribe `t7` at the join
and execute the unchanged native suffix, command emission and RGB prefix. They
prove the expected continuation behavior, not a register-preserving hook,
camera acceptance, actual model allocation or final rendered color.

## Required model and scene lifetime

The existing native loader at `800133E0` can relocate each RGB-only package
independently: all eight owned pointer fields resolve inside that package,
while every RGB and other non-pointer byte stays exact. This function alone
does not register a class. The registration loop calls it, then separately
writes the returned root into the class table.

An implementation still needs nonregistered package owners in the existing
model subfile, using the existing growth/typed-relocation machinery. It must
load the second source-form package exactly once per fresh level load, before
rendering. Native relocation adds addresses into the stored pointer fields;
already-relocated bytes cannot be treated as fresh source form. New memory
requirements and scene shifts must be certified against the actual combined
level, not the disjoint synthetic RAM addresses used by these probes.

The exact `80015B34/4C` continuation (SHA256
`556eae60eabc2d6d06e0c7bf20086d0d60cdf371a7668041a58af7362e1b2760`)
follows registered-model relocation and derives the scene address from model
start plus the complete model length. This pins a phase boundary; it is not
proof that a new callback there preserves all loader control flow or reload
semantics. The row-to-root mapping must be initialized for the final scene,
invalidated before unload, and rebound after every fresh load. Existing scene
addresses or row private bytes may not be silently repurposed.

## Tests and appearance boundary

The preliminary exact-source smoke passed 4,387 checks **before** adding the
Core report. It executes the native loader twice on distinct detached RGB
packages, both duplicate-registration stores, initialization, both animation
branches, native shared render selection and hypothetical join continuations
for both rows and blend values 0/32. All 70 near/far RGB pairs are decoded with
native CPU instructions. The bounded interpreter models R3000 load and branch
delay slots; GTE/camera outputs are prescribed, not rasterized.

The standalone `Spyro.Editor.NativeJapaneseEggInstanceRootSmoke` accepts no
arguments for source-free API/address checks, or
`--source <exact-JP-BIN> <exact-USA-BIN>` for the actual native instruction
probes and source-ancestry refusals. Logs use
`_local/research/japanese-content-20260925-r01.RCmBCn/egg-instance-root-*`.
The final Debug build passed with zero warnings/errors; 6 source-free and
4,416 exact-source checks passed.
All disc accesses are read-only and held/named sources are reverified. No
existing candidate, emulator, save, app or settings is mutated.

Parent runtime observation of the separate native-palette candidate found two
visible animated eggs, bright green with purple side/wing regions, while both
RGB arrays remained byte-identical to the native source. That observation is
not a color-correction certificate. An original-JP visual comparison or native
GPU-packet evidence is still needed before attributing the appearance, and
this instance-root work does not resolve it.
