# Japanese F5: USA animated render admission

`NativeJapaneseEggRenderAdmissionPlan` is a read-only source-issued companion to
the existing detached two-F5 Artisans integration. It changes no model, scene,
EXE or overlay bytes. The issuer requires the actual exact-USA source session,
the authenticated integration ancestry, its complete original EXE, and the
unchanged Japanese F5 package SHA256
`da0c1ae7ab529a8ab5b43954452f368adb93c09f1a9b17013c52ee3039f4a810`.
No source is inferred from a filename, adjacent root gap or synthetic fixture.

## Native admission chain

The public contract contains15 exact SHA256-indexed consumer windows. The
standalone smoke executes the non-GTE consumers from the actual source EXE,
with branch and load delay slots, physical/KSEG RAM aliasing and scratchpad.
It is not a PS1 renderer or emulator acceptance test.

| Consumer | Exact USA PC/length | Binding tested |
| --- | --- | --- |
| Scene arena | `80013F24/A0` | Actual176-row base, end and native sentinel |
| Scene fixups | `80014040/44` | All203 declared scene-relative fields, including both new private pointers |
| Model registration + loader | `80015ACC/68`, `800133E0/26C` | Only F5 class slot and8 package pointer fields written; all remaining package bytes preserved |
| Initialization route + animated prefix | `8001417C/40`, `800526A8/B0` | Negative KSEG model selects animated initialization; speed32; zero auxiliary root bypasses spatial-grid insertion |
| Draw lists | `800521C0/100` | Live state0, podFF and format94 admit both appended rows exactly once to list `8006FCF4+2200`, with active pods both zero and one |
| Animation step | `800522C0/1A4` |32 ticks keep animation0/current-next frame0..1, blend0/32, independent private bytes unchanged; FF frame tags avoid event dispatch |
| Model/animation selection | `80020A14/24` | Both rows select the single registered model and its animation header |
| Draw-command emission | `80020BB0/FC` | Conditional on camera acceptance, both current/next frame references remain owned; row identity and opaque mode are copied without redirecting model data |
| RGB + ordinary face paths | `800215C8/174`, `800217C4/5F0` | Exact near/far packed RGB owners and ordinary untextured flags0/1; first RGB decode instructions executed for all70 pairs |
| Opaque mode path | `8002173C/40`, `800226F8/184` | Source-owned mode prepass, not a static-table colour selector |
| Static counterexample | `80023104/134` | Proves the earlier descriptor-table evidence belongs to a different renderer |

Both final rows retain source classF5, animation0/0, frames0/0, scale4,
radius28, live state0, podFF and format94. Their positions and yaw do not
change model pointers. All203 native scene fixups write only their declared
fields. No claim is made that the structural test positions are safe or in view.

For the current detached composition the loaded root is `80163D54`, animation
header `80163D90`, near colours `801641B0`, far colours `801642C8`.
The two rows have independent12-byte private owners but share this one model.
The native null-list branch delay speculatively reads physical RAM+40; the
test memory map explicitly models that read instead of inventing an extra row.
The animation selector's last LW is reported as pending before the next GTE
instruction. Command testing starts separately with that known loaded value
and prescribed camera outputs, not with invented GTE execution.

## Correction: row4F is not an egg tint selector

`NativeJapaneseObjectAppearance` correctly captured static/simple descriptor
tables, but its earlier attribution of that lookup to animated F5 was wrong.
F5's native format94 routes to the animated renderer. The packed row4C word is
copied to draw-command30 and its high byte enters `800226F8`; the prepass reads
view/orientation consumers and returns to ordinary face processing. That is
not a lookup in USA table `8006E3D8` (JP `80076E74`).

The compatibility names `ProfileIndex`, `ProfileDescriptor`, `WithProfile`
and `ShadingProfile` are retained to avoid changing serialized/test byte
behavior. For F5 they mean an opaque row4F byte or a numerical projection into
the static table, not proof of renderer applicability. The updated API states
`AnimatedEggDescriptorSelectionProven=false`; the admission contract refuses
independent colour claims. A visual experiment should retain original mode1
on both rows, rather than treating mode9 as green.

## Actual colour data and the smallest editing route

The package owns70 near words at `45C/118` and70 far words at `574/118`.
In the actual native instructions, low byte is R, next G, next B. The near word
is passed as RGBC and each far channel is shifted left4 into the GTE far-colour
inputs before command `4A780010` (DPCS). Other distance branches use far colours
directly or blend toward the engine fog colour. These are packed RGB data,
not XYZ normals. Every near high byte isFF; every far high byte is00. That
byte is preserved native CODE/control metadata, not an alpha channel.

The first near RGB word is `FF6A9230`: R48/G146/B106. Near and far arrays are
not interchangeable. The tests decode every packed channel using the actual
instruction prefix and prove both arrays remain byte-identical after all
loader, list, animation and command probes. They do not assert final lit pixels.

The smallest faithful colour editor would patch only low24bits in these two
bounded arrays, preserving high bytes, geometry, frame streams and relocation
fields, through the existing model-tail package materializer. Two independent
colour variants cannot be achieved merely by duplicating rows with one shared
F5 root: they require independently bound model roots/classes, or a separately
proved per-instance renderer selection hook. No such new hook or variant writer
is introduced here.

### Whole Japanese level color census

A subsequent read-only exact-source scan of all35 Japanese level resources
found F5 registrations in30. Each registration was parsed with the existing
complete bounded model parser (never a next-root size). All30 packages are
byte-identical to the Artisans2056-byte package above, including both RGB arrays.
There is **one native source palette**, not a set of hidden per-level egg color
variants. Near SHA256 is
`1c5666756ee77c12a6459b637cc470367a0ffdd6eaf8e4ed4a9ae6587bca1fe0`;
far SHA256 is
`30ce95dee693707b2d769c14fa4217d9a66d405a77858f120679a839ec60f308`.
The private source report and reproducible probe are
`_local/research/japanese-content-20260925-r01.RCmBCn/egg-color-census.json`
and sibling `egg-color-census/`. The original held/named image was reverified.
This census supplies no additional placement or runtime authority. User-chosen
colors must be explicit authored variants of these native RGB owners, not
mislabelled recovered native presets. Their near/far/CODE semantics and ability
to coexist remain separate implementation requirements.

## Checks and remaining gates

`Spyro.Editor.NativeJapaneseEggRenderAdmissionSmoke` is a separate project; it
needs no shared smoke Program edits. Run with no arguments for source-free
refusal/ownership checks, or `--source <exact-JP-BIN> <exact-USA-BIN>` for native
consumer execution. Clean Debug builds passed; source-free22 checks and exact
dual-source3206 checks passed. The existing affected
`JapaneseSignMessageSmoke` also passed38,220 source-free checks. Logs are under
`_local/research/japanese-content-20260925-r01.RCmBCn/egg-render-admission-r01-{build,sourcefree,source}.log`.

This certificate proves source-bound list/model ownership, not camera/frustum
acceptance, GTE rasterized pixels, collision, pickup, save behavior, transient
RAM/interrupt safety or normal export approval. Memory research is separately
owned by the parent contract. `ActivationApproved`, `PublicationApproved`,
`LitPixelsVerified` and `IndependentColorVariantsProven` remain false. It does
not weaken the existing integration's activation refusal. No disc, emulator,
card, save or settings are modified.
