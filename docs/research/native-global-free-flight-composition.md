# Global Fly Anywhere composition

2026-09-30. Exact source USA BIN SHA256
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
This supersedes the destination limitation for the normal application export
path. The older destination-scoped13KB animation insertion API remains a
separate research recipe; it is not the global application's producer.

## Implementation and byte owners

`NativeFreeFlightCompositionPlan.CaptureAsync` authenticates the original
source, resident player package and executable consumers. `MergeLevelData`
validates the actual staged player stream and returns every level byte
unchanged. Ordinary geometry, actors, eggs, signs and imported content may
compose around that validated stream. Native courses15/25/35/45/55 remain
unchanged. The same native schema is accepted for caller-issued authored65;
it is not presented as a retail source level.

Global ON uses the existing free-flight controller states32/33/34 and its
rotation, velocity, collision, landing, damage, death and portal paths. The
eleven movement windows preserve native flight-global/level64 entry precedence
and native course limits. Normal-classified players gain permission and the
height/pitch ceiling bypasses. Ground and substate guards and the lower pitch
clamp remain. No new game-save bytes or RAM globals exist.

The twelfth window replaces the complete existing local-player loader at
`80013230..800133DC`,432bytes, source hash
`0cca46f0e44c16b8adea088e3081637ec5b04a512dc2c8eac494f9d16732cefb`.
The replacement caches its native root table and fits inside that same owner;
it does not use a cave. It preserves its extra scratch register in the
loader's existing8-byte stack reservation. Native branch and load delay slots
are explicit.

On a normal current level, missing animation roots33/34/35 point to the
already-loaded resident airborne root16. Currentlevel `8007596C` supplies
the catalog predicate10..64 with decimal remainder0..4, plus issued65.
This avoids dependence on a previous level's flight classifier during loading.
Native courses are excluded. The original local records load afterward and
retain precedence, so Gnasty's Loot64 keeps its actual native33/34/35 clips.
Transitions clear stale local pointers and install the same resident fallback
again; no saved preference or transient pointer is added to the game's card.

Resident16 is source package offset`72C`, type1,11frames. The package is
`0x32E1C`bytes, SHA256
`46f570bf0ad2d433f8b42874ae40a3e5d7155d7206dddd1cd08a2d4be16e0e76`.
Every packed frame start stays in its original resident geometry owner;
no compressed frame data is copied, rewritten or newly decoded. Metadata16
and33/34/35 all bound frames0..10 at4ticks/frame. All11 resident frame words
have high byteFF, which the source's direct frame-sound consumers skip.
The reused pose is the native normal airborne animation; the controller
remains free flight. Course-specific banking clips are not added to normal
levels. Practical appearance and control feel require native playtesting.

The global plan also binds the untouched resident initializer and complete
frame metadata. Only the exact fresh bootstrap's known classifier LUI change
at file`40C0` (`6666023C`→`005E023C`) is compatible; any other consumer change
is rejected. Final export checks the complete actual resident WADentry8, its allocation
header, unchanged reserved-buffer allocator/prefix, exact executable
afterimages and actual level allocation after ordinary composers finish.

## Capacity and persistence contract

Global flight adds zero level/model/scene bytes. The rejected exact-course
insertion added13256record bytes and14336rounded bytes and overlapped nine
normal levels' reserved buffers. Byte-identical model/frame deduplication did
not recover sufficient storage; no actors or assets were discarded.

The reserved floor remains`80187BB0`, and the ordinary1024-byte margin remains
required. Original level62 already ends at`801878F4`,700bytes before that
floor. Its only exception is an authenticated final read end no greater than
that exact original end. Added model/scene allocation fails unless the full
1024-byte margin is restored. This exception changes no buffer address or
reservation and is reported explicitly. Texture-only disc growth need not
increase the model/scene read end; the final actual binding and body decide.

Project settings use the existing version1 preference schema. `SaveGlobal`
atomically records the same ON/OFF choice for all30 normal owners. A legacy
per-level ON becomes global ON. OFF rebuilds from original or issued fresh
bytes and retains native flight rules; an already-patched executable cannot
silently serve as its baseline. The export intent allows ordinary edits;
final composition and allocation gates provide the actual admission evidence.

## Reproducible verification

Build `src/Spyro.Editor.NativeFreeFlightSmoke` in Release. Commands below use
the compiled DLL and accept an unchanged original USA BIN; no fixture ROM,
card or emulator state is checked into source.

```sh
dotnet src/Spyro.Editor.NativeFreeFlightSmoke/bin/Release/net10.0/Spyro.Editor.NativeFreeFlightSmoke.dll ORIGINAL_USA_BIN REPORT_JSON
python3 tools/Test-NativeFreeFlightLoader.py --source-free
python3 tools/Test-NativeFreeFlightLoader.py ORIGINAL_USA_BIN REPORT_JSON --fresh-bin ACTUAL_FRESH_BIN
python3 tools/Test-NativeFreeFlightEffects.py ORIGINAL_USA_BIN REPORT_JSON --fresh-bin ACTUAL_FRESH_BIN
dotnet src/Spyro.Editor.NativeFreeFlightSmoke/bin/Release/net10.0/Spyro.Editor.NativeFreeFlightSmoke.dll --apply-global ORIGINAL_USA_BIN STAGED_BIN NEW_OUTPUT_BIN
dotnet src/Spyro.Editor.NativeFreeFlightSmoke/bin/Release/net10.0/Spyro.Editor.NativeFreeFlightSmoke.dll --verify-global ORIGINAL_USA_BIN CANDIDATE_BIN REPORT_JSON
```

Observed results:137305 C# source/window checks;40261 source-free full-loader
checks across732 cases;49753 source-aware scalar checks including all35 source
streams and an actual authored65 candidate. Every actual local record/frame
fixup matches the original loader. Native course root results match exactly;
only missing normal33..35 roots gain the resident16 fallback. Source-free
cases cover empty streams, positive frame counts, packed flags, repeated
loads, previous local/resident pointers, invalid IDs and stale classification.
The standard regression runner includes this source-free full-loader check.

Actual source callback execution for selectors1A/1B passed754 checks/232cases
for29 newly enabled normal levels, with counts0/1/2/7. It called no helper or
allocator, wrote only each callback's native stack owner, and restored native
caller state. Original64 particle effects are unchanged. The actual fresh65
callback also passes the same no-allocation check when a candidate is supplied.

The candidate helper preserves the staged WAD byte-for-byte, checks all30
final allocations and authored65 when present, writes a new distinct BIN and
three-line CUE, verifies readback, and emits a `.flight.json` receipt. It never
overwrites the original, staged input or an existing output.

These are source, composition and bounded integer CPU results. They do not
certify native visual appearance, input feel, peak transient allocation,
collision behavior, death/respawn or portal transitions. Native runtime
receipts are maintained separately by the release verification owner.
