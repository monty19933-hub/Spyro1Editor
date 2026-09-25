# Detached Dark Hollow font and backup allocation

2026-09-25. `NativeJapaneseSignFontResidentPlan` supplies the first explicit
CPU owners for the Japanese font and its proposed modal texture backup. It
uses the existing `NativeLevelDataGrowthLayout`; it does not invent an actor
class, invoke `NativeModelTailBatch`, alter a native hook, or write a disc.
The separate [modal lifetime investigation](japanese-sign-modal-texture-lifecycle.md)
proves that ordinary USA pause restore cannot restore the mirrored font area.

## Why Dark Hollow is the first destination

The source two-F5 Artisans plan leaves only `E65C` normal resident bytes before
reserved renderer storage. Two `A000` owners already exceed that gap before
font code, sign models, scene properties or controller state. Dark Hollow has
a larger measured normal-load gap; these two research destinations need not
share one first runtime test.

| Exact USA source | Owner |
|---|---|
| Dark Hollow overlay13 | WAD `EF6000/B000`, SHA256 `9920aa47b94005166f457b5b36df6dedd1f2400f91698bac21cdb01d8590ef32` |
| Dark Hollow data14 | WAD `F01000/28D800`, SHA256 `a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97` |
| Native initializer | `8005A848/30`, SHA256 `269f2fd5ac1eccfa03136565538bc84a9d22af3bb2ddef9353fd1c8ebad4a40a` |
| Original copy buffer | `80085594`, unchanged by this plan |
| Complete JP font | WAD2 `6F800/A000`, SHA256 `917a2c272859d99b573ee3f79b1a62ca847dff218802b2c65f80bb55cb888ff1` |

Original used level data is `38750`; complete model read is `6A800`; complete
scene read is `11800`. Native normal scene-read end is `80139CE4`, below the
authenticated normal reserved floor `80187BB0` by `4DECC`. These are full
descriptor read extents, not reclaimed padding or an engine-wide heap limit.

Placement correspondence is unusually strong: all 128 USA Dark Hollow rows
have distinct exact class+XYZ matches in the 140-row Japanese level27. Japan
adds nine signs (rows128..136), plus three other rows. No regional coordinate
translation is needed for those 128 existing observations. This does not
prove terrain equality, safe sign collision, camera visibility or accessibility.
For example, original Japanese sign128 is `(16968,63355,17153)`; the nearest
unchanged USA actor is row117, class`143`, at `(14438,62761,17920)`. Sign129
is `(32778,50432,19646)`, near unchanged key chest row58 at
`(32778,51661,19646)`. No Japanese row, sign model, or other level content is
copied by this font-only plan. A placement producer must inspect the actual
USA surface/collision and author an independently owned scene append.

## Two explicit, nonregistered owners

The complete old model allocation remains unchanged. A single `14000`-byte
tail insertion contains two typed final-domain extents:

| Semantic owner | Final level-data extent | Projected normal RAM | Initialization |
|---|---|---|---|
| Immutable atlas | `194000/A000` | `801284E4..801324E4` | Every exact Japanese font byte |
| Mutable modal backup | `19E000/A000` | `801324E4..8013C4E4` | Explicitly authored zeros |

The backup is new storage, not source-free bytes discovered by scanning zeros.
Its initialization is immutable in this detached file plan; runtime readback
would later mutate the separately identified backup owner. It is one
level-scoped modal-service owner, not one copy per sign instance. No instance
linker or runtime controller is installed yet.

Both extents align to sectors, so no extra alignment tail is needed. The model
descriptor grows from `6A800` to `7E800`; the scene file offset becomes
`1A8000`; later subfile offsets also shift by `14000`. All35 original class/root
registrations stay byte-identical, including the player and unplaced models.
Every old descriptor-owned subfile—including all old model bytes and the
entire scene—is preserved exactly at its mapped location. The complete final
body is `2A1800` bytes, SHA256:

`103cf1622bbb903f863463c4f5a3036649f33a2c6d7966798575915e53b417d9`.

Plan SHA256:
`19fa53e7ff778d92ed6d63c087bac3b1eedf152dc483c31b93f949477cbb9d91`.

`RequireCurrentBody` refuses any changed preimage. `RequireFinal` and
`RevertExact` require the complete afterimage, not selected accepted fields.
The inverse removes the explicit append, recovers original descriptors and
compares every original byte. Callers cannot supply alternative source hashes,
classes, model lengths, VRAM coordinates or runtime allocation addresses to
the actual-source issuer. Synthetic fixture construction cannot acquire
`SourceBound` or `NormalLoadAddressProjectionSourceBound`.

## Native normal-load projection, not full runtime fit

The plan reuses `NativeMobySourceInventory.DecodeLevel` and the existing
original-only `NativeLevelRuntimeMemoryReport.CaptureOriginal` after privately
authenticating the exact source body, executable and PETE snapshots. Its final
body is parsed through the existing **neutral** fixture parser, without
relabelling that body or report as source-original. The plan checks the two
parsers' common original geometry and exposes a separate bounded address
projection derived from its exact insertion.

Native source windows are independently pinned for the full model descriptor
read (`800159FC/48`), registration loop and following scene placement
(`80015AD8/A8`), scene read (`80015B80/58`) and normal renderer allocation
(`8005B6F8/E0`). The loader reads the entire enlarged model descriptor, even
though no registration points to the new font or backup. Final scene start is
`8013C4E4`; final scene-read end is `8014DCE4`; the remaining normal gap is
`39ECC`. The gap decreases by the full `14000`, not just the atlas or backup.

This plan keeps the original overlay and copy anchor unchanged. Adding font
code or sign behavior to the overlay later requires a new complete composition
and recharged copy anchor, not reuse of this gap as a second budget. In
particular, original `80085594` is not the sector-padded overlay end
`80085A38`. New code must not treat that difference as a free append owner.

Preserved model bytes and registration fields do not themselves prove the
closure of every original model-internal reference or native relocation
consumer. `OriginalModelReferenceClosureProven` therefore stays false.
`ModalPhaseLifetimeProven`, `CompleteRuntimeMemoryProven`,
`VramInstallationApproved` and `PublicationApproved` also stay false.
GPU queues, DMA, interrupts, stack, alternate buffer modes, transitions and
all exits remain distinct obligations. The authenticated PETE disk snapshot
is not a claim that its native overhang/prefetched tail is initialized or
byte-identical in runtime RAM.

## Why temporary CD staging is not the first route

An alternative Artisans design could allocate only a resident backup and load
the font transiently into the existing first `1C000` primitive arena. `A000`
fits geometrically, but its complete phase ownership is unproved. Native
modal first-frame `DrawSync(0)` at `8001A5C0` is followed by `VSync(-1)` and a
jump to function exit. The primitive-cursor reset at `8001A604` is on a later
frame path, not an immediate straight-line continuation.

More importantly, synchronous CD wrapper `80016500/198` (SHA256
`b8e587e9f7f822b8b0877a42aa6ab202067f40ebf5c20229d95f322e7a1935e9`)
contains pre-read retry calls to `800163E4`, `8005637C`, and `8002BBE0`.
Blocking the caller and requesting a GPU drain does not prove those paths
leave the proposed arena untouched. Atlas upload must finish before any
primitive reuse, and read retries, errors and exit paths need their own proof.
The Dark Hollow resident-atlas route removes this additional CD/arena phase
dependency from the first sign installation attempt.

## Verification

The standalone `Spyro.Editor.NativeJapaneseSignFontResidentSmoke` Debug build
passed with zero warnings/errors; **83 source-free** and **159 exact JP/USA
source checks** passed. Tests cover owner partitioning, original complements,
all registrations, complete inverses, copied inputs/outputs, malformed
descriptors/roots/components, wrong domains/revisions, same-preimage retry,
short/extra buffers, insufficient normal gap and false capability gates.

The first source run refused the issuer's incomplete row metadata before
constructing a native report. The new issuer was corrected to provide exact
Dark Hollow count128 and table offsets`19CC38/109DC38`, which the existing
native parser independently checks. No existing parser guard was relaxed.

An independent stdlib Python reconstruction re-read the original raw sectors,
inserted the exact atlas and zero backup, adjusted only decoded descriptors,
matched the full C# afterimage hash above, and reconstructed the complete
original. Both named/held originals were verified afterward. No generated
game body was published, written to a candidate, or loaded in an emulator.

```text
dotnet build src/Spyro.Editor.NativeJapaneseSignFontResidentSmoke/Spyro.Editor.NativeJapaneseSignFontResidentSmoke.csproj -c Debug --nologo
dotnet src/Spyro.Editor.NativeJapaneseSignFontResidentSmoke/bin/Debug/net10.0/Spyro.Editor.NativeJapaneseSignFontResidentSmoke.dll
dotnet src/Spyro.Editor.NativeJapaneseSignFontResidentSmoke/bin/Debug/net10.0/Spyro.Editor.NativeJapaneseSignFontResidentSmoke.dll --source <exact-JP-BIN> <exact-USA-BIN>
```
