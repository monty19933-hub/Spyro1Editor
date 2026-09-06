# One-original-address native resource composition

This is an additive, detached resource producer. It is not a new native level, a
universal importer, an active Wizard behavior transplant, or a candidate publisher.
The original append-only model registration API and existing Town candidates are
unchanged.

## Ownership and composition

`NativeLevelResourceCompositionLayout` takes a captured original model/data owner,
bounded original-address insertion payloads, and exact equal-width byte patches.
It computes one `NativeLevelDataGrowthLayout`, so each subfile is sector-rounded
once even when several producers contribute to it. Payload and patch enumeration
order cannot select final bytes. The shared growth implementation is unchanged.

The kernel distinguishes the player stream's start from subsequent generic roots:
root0 retains its original start within the moved model subfile; every original
generic root maps across the insertion before the player terminator. The supported
prefix insertion requires a nonempty original root0 stream. Empty-root0 start
ownership is still refused, rather than silently treating a right-biased boundary
map as a valid player start.

All original generic registrations are retained, including unplaced classes and
the complete allocation between consecutive roots. Opaque registered padding is
part of the source owner. New packages exactly partition one tail insertion in
class order. The new `NativeComposedMobyModelRegistrationPlan` sibling binds each
package in the same canonical final address domain before emitting its root.
One graph node owns the final 0x800-byte header and its new typed root references.
No old registration slot is reclaimed. PETE/local conflicts, repeated contradictory
identities, existing classes and the 63-generic-slot limit fail closed.

Scalar patches cannot own header bytes or any original model byte. Their exact
original preimages must match. Partial overlaps, contradictory writes, insertion-
split fields, cross-subfile ranges, wrong WAD coordinates, replaced bytes and
overflowing addresses are refused. Exact equivalent duplicate scalar claims are
deduplicated deterministically. Only a complete, same-length scene replacement is
accepted structurally; the public native adapter requires the actual source-issued
scene plan. Every remaining original byte is checked against its mapped final
location, including original padding.

Input limits are checked before cloning payloads: at most 1,024 payloads, 65,536
patch declarations, 8 MiB aggregate payload bytes and 8 MiB aggregate patch bytes.
A conservative four-million-scan work budget bounds preservation partition and
mapping work. The reader's complete source length is checked before its second
snapshot. The final data owner is limited to 8 MiB. These are producer resource
limits, not console-RAM capacity claims.

## Source-issued Dark Hollow/Wizard profile

`NativeDarkHollowWizardResourcePlan.CompileAsync` requires the actual clean source
image plus inventory-issued and source-issued player, sound, texture and optional
scene plans. It hashes the same open image before and after capture. The source is
the 661,547,040-byte USA image with SHA-256
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.

The supported composition contains:

- Wizard Peak player animation8 inserted into Dark Hollow's nonempty stream.
- Global electric selector0x1F's explicitly unresolved rebind and local Wizard
  descriptors49/50/51, using the existing sound producer's append/dedup/SPU rules.
  Death selector0x26 is independently required to have an equivalent resident
  descriptor and actual sample. No other global selector is silently rebound.
- Complete registered lightning0x26 (0x564 bytes) and Wizard0x11B (0x35B4 bytes,
  including eight opaque padding bytes), with only the issued texture/local-sound
  package patches. The original 34 generic registrations become36.
- The issued target VRAM pixel/CLUT patches, preserving every other VRAM byte.
- Optionally, the issued scene append with its original 0x11800-byte outer extent
  unchanged, original128 rows preserved, one new row/private payload and149 fixups.

Only insertion payloads and original-address field changes are composed. Whole
retail-derived player/sound/texture afterimages are used for independent final
readback comparisons, never layered over one another. `CopyFinalLevelData()` is a
detached exact-original-derived result, not permission to overwrite a user's
already edited target. Additional authored changes need their own explicit,
source-bound original-address stage. Arbitrary caller payloads are not accepted
by the public native adapter.

The registration graph fingerprint covers the package/header graph; the final
data SHA-256 covers the complete composed data bytes. The graph fingerprint is
not a claim that behavior, effects or every gameplay consumer is installed.

## Exact output and memory accounting

All three growing owners are rounded once:

| Owner | Exact addition | Sector-rounded addition |
| --- | ---: | ---: |
| VRAM/SPU subfile0 | 0xA2C0 sound bytes | 0xA800 |
| Components subfile1 | 0x50 descriptor bytes | 0x800 |
| Models subfile2 | 0x3E1C animation +0x3B18 registered packages | 0x8000 |
| Total | | 0x13000 |

An independent Python raw-sector oracle reads the original ROM directly and
replays the native splice, descriptor, package, texture and optional scene rules
without calling Core or writing any ROM/payload:

- Without scene append: `0ca779c580d580c219526d6fa98515bedaffa418db7d9c973d8d04c33ab9d9f4`.
- With scene append: `24ff0bc281e06c0344af00e4d7883331b8a73ee13b3ca4a564fe1a03477d9e86`.
- Registration graph: `a5dabebb0b55d856b9fe2929c859a958e69e1f95f46ea3e0059f87501ec42b95`.

The typed `NativeLevelRuntimeMemoryReport.AnalyzeResourceComposition` adapter
reuses the existing exact native data-return, full model/scene CD extents,
copy-buffer and PETE/render-buffer certificates. The scene variant has129 placed
Mobys,90 remaining dynamic rows, and0x45E7C bytes between the normal post-load end
and the first known reserved buffer. An independent final-component walk checks
these owners in the smoke suite. This does not reserve runtime stack, transient
loads, emitted effects, or prove peak fit.

## Regression evidence and invocation

The initial stub failed at the deliberately missing common-layout implementation
after a clean Debug build. A second finite RED demonstrated missing aggregate
payload pre-copy rejection before the three ingestion/work guards were added.
No infinite enumerator, OOM allocation, or timeout is a test oracle.

Final targeted GREEN: Debug build1.53 seconds, zero warnings/errors;
source-free121 checks/35 refusals and source142 checks/40 refusals. Both native
output goldens and reverse-stage graph equality passed. The final extension proves
exactly63 generic slots with every existing unplaced root preserved, rejects the
next slot and existing-class aliases, and exercises finite request-count tripwires.
Durable logs are in
`_local/research/native-resource-composition-final-20260906.t1Krlk/`.

From the repository root:

```sh
dotnet build src/Spyro.Editor.NativeResourceCompositionSmoke/Spyro.Editor.NativeResourceCompositionSmoke.csproj -c Debug --nologo
dotnet src/Spyro.Editor.NativeResourceCompositionSmoke/bin/Debug/net10.0/Spyro.Editor.NativeResourceCompositionSmoke.dll
dotnet src/Spyro.Editor.NativeResourceCompositionSmoke/bin/Debug/net10.0/Spyro.Editor.NativeResourceCompositionSmoke.dll --source 'Spyro the Dragon (USA).bin'
PYTHONDONTWRITEBYTECODE=1 /Users/john/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/bin/python3 src/Spyro.Editor.NativeResourceCompositionSmoke/source_composition_oracle.py 'Spyro the Dragon (USA).bin'
```

Markers are `NATIVE_RESOURCE_COMPOSITION_SOURCE_FREE_OK`,
`NATIVE_RESOURCE_COMPOSITION_SOURCE_OK`, and
`NATIVE_RESOURCE_COMPOSITION_ORACLE_OK`. Builds remain serialized by the parent.

## Remaining native gates

The default API retains particle7 as a detached pending payload. A later opt-in
`CompileWithParticleAsync` stage now installs its source-issued descriptor record;
see [the separate descriptor contract](native-particle-descriptor-append-contract.md).
It does not install particle behavior. Wizard/lightning behavior, callback
dispatch, particle07/41 spawn/tick/renderer consumers, electric-selector consumer
ownership, valid destination placement, stack/transient RAM, native gameplay,
audio, persistence and safe publication remain separate obligations. All complete
closure, behavior-installed, particle-installed, placement, runtime-fit, peak-fit
and publication flags stay false on the default API; the opt-in stage changes only
the descriptor-installed flag. No candidate or normal export path is added.
