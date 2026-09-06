# Detached fresh two-material texture allocation

This smoke exercises the new neutral allocator and its private exact-original
adapter. It does not publish or install a level, change ordinary exporters, or
approve renderer/runtime behavior. No old untextured output is replaced.

The source-issued profile takes an actual `NativeId65FreshSceneBundle`, Dark
Hollow WAD14/T0 and **Gnasty's World (level ID60), WAD70/T0**. The two actual fresh
HP triangles receive material IDs0 and1. Local texture ID0 is not a global asset
identity: the complete source handles, GPU domains and relocation policy remain
distinct. Public neutral inputs never become native source certificates.

## What is checked

- Both complete184-byte records/all46 descriptors, including HQ/close resources.
- All16 rows of each LQ distance palette; exact same-source contained aliases.
- 31 physical regions/28,160 allocated bytes and16 contained Gnasty close aliases.
- The actual exhaustive upload ownership partition, including17,369 protected
  zero bytes and the complete separate SPU bank. Free bytes after allocation:
  391,008. Capacity uses rectangles, not only aggregate byte counts.
- Native split metadata (all LQ followed by all HQ), the two HP material words,
  and the entire translated data complement. Used data grows0x8C8→0xA38; both still
  occupy a0x1000-byte CD allocation. Model, scene, seven scene lists and SPU remain
  byte-exact.
- Defensive copies, deterministic retry, finite enumeration, malformed owners,
  unknown source/domain/policy, partial-overlap refusal, narrow-hole/page-boundary
  failures and complete output readback.

Source-free adversarial masks pass through the **same full-record implementation**
as the private original-source adapter; there is no alternate fake allocator.

## Evidence and commands

Root observed actual Release RED:1.19s,0 warnings,8 missing new-type errors. The
first implementation Release build passed4.93s,0 warnings/errors; source-free
7,572 checks and33,022 source checks passed (40,594 total), with all pre-C# and old
goldens unchanged. The final targeted build after three extra null/straddling-owner
cases passed1.38s with zero warnings/errors:7,575 source-free and33,022 additional
source checks (40,597 total). Exact logs live in
`_local/research/fresh-native-textures-20260906/allocator-{red,green,final}-*.log`.

The integrated source-free Release gate passed103 steps with zero failures
(51 builds including App,51 run modes,one Python runner-test step). This gate
preceded the three test-only additions; it is not a new full source-QA pass.
Complete Python discovery separately passed44 tests. Gate logs and a complete
report copy are in `_local/fresh-texture-default-regression-20260906-r01.Lq9Rw3/`.

Run the built smoke with no arguments for source-free checks; add only
`--source original.bin` for read-only source tests. There are no workspace, CUE,
staging, publish or candidate modes. Builds require the team's serialized slot.

```sh
dotnet src/Spyro.Editor.NativeId65FreshTerrainTextureSmoke/bin/Release/net10.0/Spyro.Editor.NativeId65FreshTerrainTextureSmoke.dll
dotnet src/Spyro.Editor.NativeId65FreshTerrainTextureSmoke/bin/Release/net10.0/Spyro.Editor.NativeId65FreshTerrainTextureSmoke.dll --source 'Spyro the Dragon (USA).bin'
python3 src/Spyro.Editor.NativeId65FreshTerrainTextureSmoke/source_texture_allocation_oracle.py 'Spyro the Dragon (USA).bin'
```

The independent raw-sector oracle ran before the C# implementation, re-encodes
and re-decodes all46 descriptors, reconstructs actual full upload/data bytes,
checks complete source identity before/after, and compares its entire JSON with
`source_texture_allocation_golden.json`. It never writes a ROM/payload or loads an
editor DLL. Its explicit-return fresh fixture preserves the existing oracle and
helper hashes without editing either or inspecting frame locals.

Final upload SHA256:
`2ab548f540fbfadddc38d96e70a9633a1c6f37c37ee2d1a896919ac30f929333`.
Final data SHA256:
`c13c21171e5ed4755f1598914eba89b0b31795b91aaeb2605efc6fed9aa17160`.

## Limits that remain

The fixed direct-LQ profile requires the actual native initialization
`g800785CC=0x8000`; clip1/129 counterexamples still refuse. Retaining complete
HQ/close records does not prove those execution paths. The24 extra GT3 packet
bytes are **per emitted pass**, not a full-frame packet/GTE/RAM budget.

The old staging/body APIs cannot consume this different private revision. Any
future adapter must recompute transfer bytes and bind the full new plan/upload,
even when the transfer image happens to be equal: current allocations end by
GPU+FF90, outside the retained5E000..7F000 transfer residue. Unchanged transfer
bytes do not authorize pairing an old stage with the new texture or another scene.

Staging/body composition, complete player/particle/callback state, entry,
transient/GPU/SPU timing and memory, gameplay, and publication remain unapproved.
Free-space accounting and exact source bytes are not runtime resource acceptance.
