# Detached fresh Wizard upload staging smoke

This new research stage accepts only an actual `NativeId65FreshWizardResearchResourcePlan`. It retains that exact reference, its whole upload and resource-plan identities, and builds the complete transfer source from the issued `0x65800` bank followed by `0x19800` bytes of the **same** resource's VRAM residue. It does not extend the old staging, P0, body, disc, UI or publisher APIs.

```sh
dotnet build src/Spyro.Editor.NativeId65FreshWizardStagingSmoke/Spyro.Editor.NativeId65FreshWizardStagingSmoke.csproj -c Release
dotnet src/Spyro.Editor.NativeId65FreshWizardStagingSmoke/bin/Release/net10.0/Spyro.Editor.NativeId65FreshWizardStagingSmoke.dll
dotnet src/Spyro.Editor.NativeId65FreshWizardStagingSmoke/bin/Release/net10.0/Spyro.Editor.NativeId65FreshWizardStagingSmoke.dll --source /absolute/path/original.bin
python3 src/Spyro.Editor.NativeId65FreshWizardStagingSmoke/source_fresh_wizard_staging_oracle.py /absolute/path/original.bin
```

Default and source modes have no payload-file writer. Source mode reads only the original BIN and the smoke's own assembly (a deliberately wrong-sized input to the actual factory). It independently checks the original whole SHA, actual EOF, final length and position before and in `finally`. The exact shared production snapshot checker also runs 23 hostile stream checks: short/oversized/changed content, excessive returned counts, initial/final position, actual EOF, changed final length, and early/mid/EOF cancellation. Neutral caller bytes/hashes cannot issue a public stage.

Tests-first evidence is retained under `_local/research/fresh-wizard-staging-r01.0ojVms/`:

- `01-raw-derivation.json`: independent first full-image derivation before C# staging existed, 403 checks / 10 refusals.
- `02-missing-api-red-build.log`: actual missing-layout API RED, 1.28s, zero warnings / one missing-type error.
- `03-pinned-raw-oracle.json`: pinned repeat, 404 checks / 10 refusals, identical output hashes.
- `04-green-build.log`: full Core + new smoke Release build, 4.86s, zero warnings/errors.
- `05-source-free.log`: 286 checks / 271 refusals plus the separate 23 shared stream controls.
- `06-source.log`: cumulative 319 checks / 283 refusals (33 additional source checks / 12 refusals), plus the same stream controls. Exact repeated issuance, byte-identical but distinct resource ancestry refusal, copies, complete owner partitions, DMA-only tail, and all old source goldens passed.
- `07-legacy-build.log` / `08-legacy-source.log`: existing NormalResource smoke rebuilt against the new Core, 1.41s clean; all 15,423 checks GREEN, including unchanged original P0 and fixed staging byte/plan hashes.
- `09-independent-spu-slices-raw.json`: final root-reviewed independent SPU entry-witness clarification, 404 checks / 10 refusals, exit 0; all byte/count expectations unchanged.
- Root12 independent replay exactly matches09; both stderr files are empty.
  Full Huygens peer read found no material issue. Runner registration has actual
  missing-case RED10, then whole Python110/four explicit-source skips GREEN11;
  the full default Release suite passes134 steps/zero failures. See
  `docs/research/native-fresh-wizard-upload-staging.md` for the exact report.

The raw oracle pins its complete original EXE, the five actual source slices, the resource reconstruction helper and all nine transitive helper files. It reconstructs resource texture/audio bytes directly from original sectors and first verifies the established whole resource and old staging goldens. It executes the actual bank-request argument setup, the CD ABI register copies, and a separate signed sector-rounding slice: the old `0x5E000` bank requests 188 sectors; the new `0x65800` bank requests 203. Branch/load/JAL delays retire, missing owned descriptor memory refuses, and the wrong phase executes its delay but skips the bank call. The SPU address and count slices use separate fresh machines with explicit entry/stop PCs; the latter is an independent post-address-helper witness. The intervening GPU/CD/SPU helper bodies and asynchronous scheduler are **not** executed, no helper return is fabricated, and mapped table values are explicit arithmetic witnesses—not a newly emitted native header.

| Complete byte owner | Length | SHA-256 |
| --- | --- | --- |
| Issued paired upload | `0xE5800` | `d74c2571ed857865b2b989af3001117f44e0915f9c6d59927e30a49d90a79402` |
| Transfer source | `0x7F000` | `33282cc17414acccd98381f8d5b9e068ea2327bb3c8a7138dd82b1705c217fad` |
| Complete bank | `0x65800` | `f922913cf797ee91f0e0ccb909e511a2746c9f28a5df9d8ca2b54934979b968c` |
| Same-resource VRAM residue | `0x19800` | `e2e713a331281d553f43cd2df764208ba00770dc1d2fea767963f698cd238ac1` |

Plan SHA: `dc72dc6a4598d4aff772e18eb99843e1d81a27373d96f234df60490a674d4003`. Equal transfer SHA does not imply equal complete upload or actual resource identity; a neutral off-transfer mutation exercises this boundary.

Root full-read review and independent Huygens static review found no material issue in the final source/owner/ABI boundaries. Peer review did not execute hardware, an emulator, a build or a candidate.

CPU requests `0x7EFF0`; DMA source length is `0x7F000`. The final 16 bytes are `3232b131122e903191311232f231322e`, not generated silence. Linear SPU start `0x1010` plus DMA length ends at `0x80010`; wrap and reserved low-16 ownership remain unproved. Source-only byte issuance is not CD/GPU/SPU timing, peak memory, global-selector compatibility, complete callback/audio/reward closure, a new body/header/scene, runtime or publication approval. No candidate was written or emulator exercised by this smoke.
