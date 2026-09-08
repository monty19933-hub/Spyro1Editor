# Fresh textured Disc — source-bound composition

September 8, 2026. The existing Disc type now has a separately named
`CaptureTexturedAsync` factory. It accepts the actual issued two-static-material
body and reuses the existing physical disc geometry, authenticated sectors,
inverse and readback implementation. No new allocator or image writer was added.

The textured profile must be `DirectLqTwoStaticMaterials` AND match the exact
A38/1000/DE000/F800/2800-or3000 shape. A38 used length alone is insufficient:
actual maximum-cell untextured data can share it. The old factory still refuses
the textured revision before source I/O and retains its old JSON/Plan goldens.
The new binding retains its actual body ancestor; equal hashes do not substitute
another instance for that retained reference.

## Tests and independent derivation

An actual missing-API RED preceded the sole production change. First inclusive
Release build passed with zero warnings/errors, new free47 and explicit source312
checks. Six complete physical images and their full original inverses match an
independent original-only Python derivation: original fixed donors, alternate
spawn, shorter scene, selected alternatives, reversed material order and the
same source in both slots. The actual catalog's46 selectable music tracks also
match independently calculated executable hashes; this is not46 gameplay runs.

First fixed-a expected BIN SHA:
`998a47930c5f20e36cf87d41bfb9ec3cab237d8d42c443ed706bd50c692611a3`.
No such physical BIN was published by these tests. The complete six outputs
are in `src/Spyro.Editor.NativeId65TexturedDiscSmoke/source_textured_disc_golden.json`.

Root reviewed production/tests/oracle and independently replayed the oracle;
its complete output matches the embedded golden byte-for-byte. Existing source
gates remain GREEN: texture140404, textured body157, Disc566016 and starter
research182 plus timing89/8. Counts include many byte/sector assertions, not
independent native playtests. All held original/helper owners receive final
hash/EOF checks; no original image or CUE was changed.

New default/source smoke modes and the optional raw-source oracle are registered
in the shared regression runner. Two expected registration REDs preceded that
change;63 runner tests pass, preserving exact argument/marker/deadline policies.
The full all-project gate must still be rerun after the current UI tranche.

Detailed retained evidence:
`_local/research/textured-fresh-disc-r01.ogZ57c/FINAL-HANDOFF.md`;
Root oracle repeat is `15-root-oracle.json` beside it.

## What this does not enable yet

Complete23-descriptor material records, pixels, CLUTs and UV/page fields are
preserved, but tested renderer consumption remains direct-LQ with clip8000.
HQ/near execution, native appearance, frame/packet peaks, animated materials,
arbitrary allocations and mixed texture/Wizard composition are not approved.
There is no material persistence, window control, saved-document compiler bridge,
new research publisher or normal Create BIN path from this change. A separately
guarded candidate and a real textured-level DuckStation test are next.
