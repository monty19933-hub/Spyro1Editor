# Actual-resource-bound fresh Wizard upload staging

`NativeId65FreshWizardResearchUploadStaging` is a new, detached research result,
not a widened old staging/body API. Its only factory accepts the original BIN
and an actual immutable Wizard resource result; it retains that exact reference,
complete upload identity and native consumer/source pins. Caller hashes, bytes
or flags cannot issue it. Full original SHA/length/position/actual EOF are checked
before and in a noncancelled finally path. Output buffers are copies.

The transfer is not simply a bank padded with zeros. Native bank loading replaces
the beginning of the same resource's VRAM buffer:

| Complete transfer owner | Offset | Length | Paired upload offset |
|---|---:|---:|---:|
| Issued bank CD read |0|65800|80000|
| Same-resource retained VRAM residue |65800|19800|65800|

All values above are hexadecimal. Whole transfer7F000 SHA256:
`33282cc17414acccd98381f8d5b9e068ea2327bb3c8a7138dd82b1705c217fad`.
Plan SHA256:
`dc72dc6a4598d4aff772e18eb99843e1d81a27373d96f234df60490a674d4003`.
The complete paired upload remains the previous d74c…9402 resource. The
original P0 staging626a…fb07 and all prior resource/body goldens remain exact.

CPU requests7EFF0, while DMA reads7F000. Its final16 bytes are
`3232b131122e903191311232f231322e`, not silence. Linear destination1010 plus
DMA length ends at80010. Hardware wrap, reserved low16, chronology/interference,
peak demand, listening and global1F compatibility remain unproved. No header,
body, overlay, disc, candidate or gameplay capability is issued.

## Independent source proof and regressions

The new smoke's `source_fresh_wizard_staging_oracle.py` reconstructs resources
from the original sectors, independently of Core. It first checks established
whole resource and legacy transfer goldens. Exact native bank-argument setup,
CD saved-register copies and a separate signed-sector-rounding slice establish
65800→203 sectors, versus old5E000→188. Real branch/load/JAL delays retire;
the wrong phase skips the call while still executing its delay. The two SPU
argument witnesses use separate fresh machines and explicit entry/stop PCs.
No intervening GPU/CD/SPU helper return or async scheduler is synthesized.

Evidence: `_local/research/fresh-wizard-staging-r01.0ojVms/`.
Independent raw derivation precedes the actual missing-API RED. Final focused
Release build is clean; source-free286 checks/271 refusals, source319/283
cumulative plus unchanged shared23 stream checks. Old NormalResource source
suite passes15,423. The raw source proof passes404/10; root's independent12
replay exactly matches final09 JSON, with empty stderr. Root and Huygens read
the complete implementation/tests/oracle; peer review found no material issue.

The fifth opt-in source-only Python runner case has a tests-first missing-
registration RED10 and GREEN11. Default/list modes do not invoke it or read a
source image. Actual full Python suite:110 tests/four explicit-source skips;
runner50. Full default Release gate:134 steps/zero failures, report
`/var/folders/ly/6mrqskxj60717y5ln83x8yjh0000gn/T/spyro-regression-82bbxs_f/summary.json`.
These are engineering checks, not gameplay runs.

See the smoke README for complete byte-owner hashes and exact commands. Future
fallback resources change the complete upload and may change its residue;
they must not reuse this existing stage merely because lengths or hashes appear
compatible. A future composition must derive transfer bytes from its actual
issued resource revision while preserving this stage's meaning.
