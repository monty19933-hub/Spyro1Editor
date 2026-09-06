# Fresh native authored surface bundle

`NativeId65AuthoredSurfaceBundle` materializes five detached native components
from a bounded authored rectangle. It copies no retail environment sector,
triangle, visibility list, special surface, cyclorama part, or terrain texture.
This is an actual byte producer, not a report that calls retained scenery blank.

It is **not** a complete descriptor1 subfile or level. In particular, the texture
component that precedes these five components, portal/particle/sound components
that follow them, player root0, generic models, scene, companion, dynamic arenas,
engine callback installation, save/music tables, and normal/rescue-mode closure
are outside this stage. No disc writer, UI, old ID65 schema/receipt, candidate,
or runtime acceptance is changed.

## Deliberately small grammar

`NativeId65AuthoredFloor` uses actual native world units. Origins and extents are
multiples of16; origins are0..262128 and widths/depths16..2048. The entire closed
rectangle must remain inside one native4096-unit collision cell. An endpoint
exactly on the next cell boundary refuses. Color words are24-bit. There is no
caller-supplied arbitrary mesh, special material, texture number, or body radius.

The existing constructor's `FloorRgb` and `SkyRgb` are raw native
`0x00BBGGRR` words, emitted little-endian as R, G, B, 0 bytes; they are not display
hex `0xRRGGBB`. `NativeId65AuthoredFloor.FromDisplayColors` explicitly accepts
typed `ColorRgba` channels and packs that native byte order. Either alpha other
than255 refuses rather than being silently discarded. The static adapter adds
no instance properties and does not change geometry validation, serialization,
the original constructor, or any existing word/golden semantics.

The deterministic output contains:

- One environment sector, four LP and four HP vertices, two faces in each LOD,
  one LP color table and both complete HP color tables. Visible vertices are
  A=(X,Y), B=(X+W,Y), C=(X,Y+D), D=(X+W,Y+D), all atZ.
- One environment visibility group containing only sector0 and an `FF`
  terminator; one sky visibility group containing only the `FF` terminator.
- An explicit zero-count special-surface component.
- Two newly encoded collision triangles, their own Z/Y/X tree and block group,
  two group0 assignments and two explicit ordinary-land flag bytes`3F`.
- An RGB cyclorama with zero parts and an owned speculative-read guard.

The render-culling sphere is conservatively derived from the rectangle. It is
not a player/Moby physics envelope, swept-path certificate, slope grammar,
placement approval, or complete level RAM budget.

The initial fixture `(8448,8448,8704),1024x1024`, floorRGB`708050`, skyRGB`406080`
is324 bytes, independently encoded by the Python oracle as SHA-256
`ee82c4d3bb2589eb821028337499d062762c47d3f6af2267f6b0fabee8df58b9`.
Different supported positions, extents, heights and colors remain distinct and
round-trip; there is no collapse to a retained fixture row.
The same fixture is expressed through the display adapter with floor channels
`(0x50,0x80,0x70)` and sky channels`(0x80,0x60,0x40)`, i.e. nominal display colors
`#508070` and`#806040`. No renderer channel swap is required or introduced.

## Native consumer requirements, not guessed empty structures

The complete clean-USA executable is SHA-256
`a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`.
Executable file offsets become runtime addresses by adding`8000F800`.
Ten exact consumer-span certificates are retained in
`NativeId65AuthoredSurfaceConsumers.cs`; each is independently hashed on capture.

The descriptor1 parser`80012D58..8001322F` rebases all five collision pointers
relative to collision component+4, not the header or whole bundle. Environment
sector pointers and environment visibility pointers use their component+4.
Sky visibility pointers use the beginning of the second visibility section.
Native tree branches are16-bit byte offsets; leaves are16-bit block-word
offsets. Block values use bit15 as group marker, not part of the triangle index.
The emitted group is`8001,0000,8000`, followed by an owned zero padding halfword.
Only triangle indices1 and0 exist and both are reachable from the one cell.

Packed collision XY origins are14-bit, XY deltas signed9-bit, and packed Z has
14-bit base plus unsigned8-bit deltas. The profile emits no raw Z flags. Native
upward land uses negative XY normal, so collision order is A,C,B and B,C,D,
opposite the visible A,B,C and B,D,C. Index63 is ordinary land: absent special
descriptor0 must never be interpreted as ordinary floor.

Three easily missed native obligations are explicit bytes and tests:

1. Sector header+`18` is **not** zero padding. `80025B08` loads the four-byte
   word. Signed-byte branches`80025BBC`, `80025D18`, `80025E60`, `80025FD0`
   skip four optional scene-index consumers only when their bytes are negative.
   Fresh output emits`FFFFFFFF`; zero would request unowned scene index0.
2. HP triangle face-word3 bit`80` selects the direct primitive path at
   `80027260..80027264`. Material byte`FF` is sign-extended and branches at
   `8002755C` to `80027628`; opcode`30` is installed at`80027648`. It emits
   untextured Gouraud G3, before the texture dereference at`8002756C`. No fake
   texture0 or atlas dependency is introduced. Separate close/normal texture
   subdivision queues are deliberately not claimed by this profile.
3. HP iteration prefetches the next face's first word at`80026B88`, so the
   exact156-byte sector table owner is followed by an explicit four-byte zero
   guard. Zero-part sky is supported by the loader's`BLEZ` at`8001304C` and
   downstream loops, but `8004F09C` has a pointer `LW` in its delay slot at
   `8004F0A0`. The16-byte cyclorama therefore includes an owned zero word at+12.
   The grouped path's empty`FF` list also terminates before a part dereference.

## Source authority and ownership

The public `CaptureAsync(originalImagePath,floor)` hashes the same open original
image before and after its reads, checks raw2352/+24 layout and the exact
SCUS directory owner, and binds the complete executable and consumer spans.
Original image identity remains
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
No Town level-data capture is necessary; emitted surface bytes depend only on
authored intent and the fixed source-proven grammar.

The source-free encoder cannot issue `SourceCertified`. The public source-issued
bundle owns immutable bytes, separate component/owner digests, typed references
with exact field width, pointer base, units, mask and destination owner. Accessors
return copies. `RequireExactBundle` compares every byte, including guards and
unused capacity. This is frozen source capture, not later publication freshness.

The bounded reader first recovers intent, requires its exact canonical encoding,
and then independently applies the existing environment-header and collision-index
codecs. This intentionally rejects other potentially valid native encodings;
it does not pretend to be a general scene parser. The smoke's separate field-level
decoder and raw-sector Python oracle do not call the producer to derive geometry,
pointer bases or the byte golden.

`IndependentLevelSupported`, `CompleteLevelResourcesProven`, `PublicationApproved`
and `RuntimeVerified` all remain false even for an exact-source bundle. No empty
callback, absent rescue route, complete engine/player/scene closure, ordinary
Create BIN approval, or gameplay pass follows from these five components.

## Regression evidence

Tests were written first. The intentional RED build failed with22 errors, all
missing the intended new surface APIs, zero warnings,5.36 seconds. Shared Core
built successfully. Log:
`_local/research/native-id65-authored-surface-20260906-r01/01-red-build.log`.

GREEN Debug completed in5.46 seconds with zero warnings/errors. The source-free
suite passed625 checks; the exact-original source suite passed650 checks. Logs
`02-green-build.log`, `03-green-free.log`, `04-green-source.log` and
`05-independent-oracle.log` are in the same retained directory. Commands:

```text
dotnet build src/Spyro.Editor.NativeId65AuthoredSurfaceSmoke/Spyro.Editor.NativeId65AuthoredSurfaceSmoke.csproj --configuration Debug
dotnet src/Spyro.Editor.NativeId65AuthoredSurfaceSmoke/bin/Debug/net10.0/Spyro.Editor.NativeId65AuthoredSurfaceSmoke.dll
dotnet src/Spyro.Editor.NativeId65AuthoredSurfaceSmoke/bin/Debug/net10.0/Spyro.Editor.NativeId65AuthoredSurfaceSmoke.dll "Spyro the Dragon (USA).bin"
python3 src/Spyro.Editor.NativeId65AuthoredSurfaceSmoke/source_surface_oracle.py "Spyro the Dragon (USA).bin"
```

The independent Python command reports `FRESH_SURFACE 324` with the
golden above and `EXACT_SOURCE_NATIVE_SURFACE_CONSUMERS_OK`. It reads the source
without writing a ROM or detached payload file.

The tests include20 invalid intent profiles, every-byte exact-owner corruption,
typed field/base/mask resolution, six valid noncollapsed intents, all five
collision pointer mutations, missing descriptors, unsupported flag/Z bits,
foreign triangle indices, missing sentinels, missing branches, undefined
visibility groups, HP count/offset/material/repeated-corner/guard corruption,
zero-part sky ownership, ten changed source-consumer spans and detached copies.

Independent peer review of the complete bundle, consumer adapter, and smoke
found no material ownership, parser or resource-budget issue. That review was
static, not a second native-renderer execution or gameplay pass. Final wording
clarifies that zero origins are supported; no encoding change followed the
reported GREEN runs.

The later explicit display-color boundary was also regression-first: Release
RED failed only on four missing `FromDisplayColors` references (3.97 seconds,
zero warnings). Release GREEN then completed in3.83 seconds with zero
warnings/errors; source-free681 and exact-source707 checks passed. Logs
`01-red-build.log` through`04-green-source.log` are retained under
`_local/research/native-id65-display-colors-20260906-r01.mKZ3EM/`.
The added cases cover red/blue swaps, asymmetric channels, all twelve LP/HP
near/HP far color words, sky bytes, unsupported floor/sky alpha, unchanged
instance-property shape, and exact original-source equivalence. The original
324-byte golden above remains unchanged. Independent read-only peer review
found no material issue; this conversion test is not visual/runtime acceptance.
