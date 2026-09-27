# Authored eggs: placed-row retirement source proof

September 27, 2026. Test-only extension of the exact-source authored-class smoke;
no runtime hook, ROM change, collector activation or save approval.

The existing [Town Square persistence contract](native-town-square-persistence-contract.md)
already identifies USA `80052568..800525CC` (EXE `42D68/64`, SHA256
`602fc31b1351212e5d5c72c39ac59cc86a9afd0226a9a0549192855588498505`)
as native **placed-row deletion**. This is not generic kill `8003B9D4`; it
does not select effects/callbacks from the low class byte. The same structural
leaf begins at Japanese `80056A5C`, the target documented in the egg pickup.
The new tests reuse the established USA owner rather than inventing a marker.

After executing actual scene row setup, fixups and animated initialization,
both final authored rows (`801743D0` / `80174428`, classes `1E9` / `1F2`)
have signed row `+34=-1` and lie below dynamic base `80174480`. For this
specific non-grid placed path, native deletion reads the signed index, the
grid-base global in the branch delay slot, and the dynamic-base global. It
never reads class ID or private data, never follows a grid link and never calls
another routine. The exact write set is **one byte: row `+48=FD`** in the
native return delay slot. Every other row byte and the actual `FF` terminator
remain intact. Repeating this path is still only the same one-byte store.

`NativeRetirementCases` executes the unmodified native deletion on each egg
and then the actual animated render-list builder `800521C0`. The rebuilt list
excludes exactly the deleted egg(s), preserving all other entries and order.
The previously cached list remains unchanged until that rebuild: merely writing
FD does not prove current-frame renderer/animation scheduling safety.

The source-backed smoke passes **541 checks**, including 18 new retirement
checks and the existing exhaustive 65,536-class dispatch comparison. Its final
data remains `6675cf971f53b6b3fbb31b0d40ffd54f5404d3b8049c1ef64fff44a068ee4dac`
and overlay remains `76911d8441beaaf7788b5d4a9cc8d785274ffcf0f1b96d417db255598957ba66`.
Both original images are reauthenticated at completion. Run:

```sh
dotnet run --project src/Spyro.Editor.NativeJapaneseEggAuthoredClassesSmoke -c Release -- --source '<exact-JP-BIN>' '<exact-USA-BIN>'
```

This proves the supplied initialized rows' local source path, not a general
deletion API for grid-linked or dynamic objects. A future collector must guard
those preconditions, own dispatch/scheduling, pair collection with suppression,
and restore suppression after reload using stable custom IDs. No native
collection/checkpoint/gem/save bit is written by this leaf, and no runtime
pickup, disappearance, reentry or memory-card result is claimed here.
