# Complete Alpine group in Artisans

Ordinary placement/export now supports the original Alpine Ridge 0/1 group in
Artisans after an explicit, bounded set of native removals. This addresses actual
model, property and texture capacity: removing object instances alone does not
make their shared resources unused. The installer never deletes project objects.

The supported profile removes all six Gnorcs, Balloonist T45 and Balloon T46,
tower flags T0–6/95/96, all 20 tulips, trees T102–105, and grass T121/122/128–130.
The complete row list is in `UniversalAlpineArtisansHost.RemovedRows`. The export
warning states that removing the balloonist and balloon removes balloon travel.
All dragons, portals, existing chests, Life Chest T94 and uncertain prop T109 stay.
Partial or additional removal profiles currently refuse with named guidance.

The rebuild reclaims 198,656 model bytes and 1,392 exclusive property bytes. It
retains every source model (186,184 bytes), all source behavior, paths, sounds,
arms and reward resources, and all surviving native terrain/object identities.
No native code is reclaimed. Keeping the balloon and balloonist in this same
otherwise eligible profile leaves a measured 13,700-byte protected RAM deficit.

The original 174-row physical prefix remains intact. Imports reuse physical
rows 0/1 with immutable logical saved identities 174/175. A bounded native
1,192-byte collision-cache owner accommodates 122 complete dynamic lifetimes;
new arm/reward parents remain static rows 0/1. The original native 16-to-120
conversion has a separately checked parent-consumer path. No general physical
row compaction or arbitrary extended-index actor is admitted.

Final RAM end is `80186544`, with 5,740 bytes before the ordinary buffer and
4,716 bytes after the protected 1,024-byte margin. Other edits, mixed groups,
different removal profiles and the second Alpine group in Artisans remain open.

## Verification

Root evidence lives under `work/universal-finish-20261002`:

- `artisans-root-verification-r1.json`: Release App zero warnings/errors;
  Artisans 199 structural/mutation checks plus 532,125 independent asset checks;
  saved removal/reopen/ordinary export/readback 404; first Alpine group 734,
  second group 1,425 and Green Druid world 907, all passing.
- `artisans-root-candidate-match-r1.json`: independently rebuilt complete BIN
  matches the worker candidate exactly.
- The private delivery additionally records actual native constructor 1,339,
  extended parent/arena/save 240,534 and cache/ABI 296 instruction checks.
  These instruction checks are not emulator gameplay acceptance.

Registered repeatable tests:

```sh
dotnet run --project src/Spyro.Editor.AlpineArtisansSmoke -c Release -- ORIGINAL_BIN CATALOG_DIRECTORY NEW_COMPOSE_DIRECTORY
dotnet run --project src/Spyro.Editor.AlpineArtisansExportSmoke -c Release -- ORIGINAL_BIN CATALOG_DIRECTORY NEW_EXPORT_DIRECTORY COMPOSE_DIRECTORY
```

Candidate:
`work/universal-finish-20261002/artisans-root-export-r1/Alpine0-1-Artisans-explicit-removals-r1.cue`.
BIN SHA256: `1f8a62c1055645ce516798e53b36afcc79bcfdc938479aa76d34626da19236b3`.
Original Artisans New Game start 84992/47104/9564 is unchanged. The imported pair
uses a common translation: FD at 84992/47104/9568 and druid at 83466/50237/10335.
Native floor probes support this revised pose; the earlier candidate crossed a
terrain drop and remains preserved as superseded evidence.

Fresh full-firmware visual gameplay, contacts, reachable rewards, respawn,
card persistence and audio remain pending. Universal gameplay and the V1.0
release are not complete.
