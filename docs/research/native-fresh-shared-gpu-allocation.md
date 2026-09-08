# Shared GPU mechanics, unchanged existing texture output

The internal NativeStaticTerrainGpuAllocation extracts only the existing80000-byte
GPU placement mechanics. The public DE000 allocator retains its exact source
ownership, full hashes, SPU clip, provenance, metadata composer and result contract.
No zero-byte free-space inference, mixed-resource issuer, new publisher or public
resource authority is introduced.

Root and an independent peer fully reviewed the extraction and literal placement
tests. Tests first failed on the missing internal type after the old literal order
and refusal checks passed. The promoted default passed7627 checks, including48
new ones. Actual terrain source passed132825/total140452; adjacent body source139,
disc312 and saved-material220/35 all passed with unchanged prior full-image goldens.
No expectation or production correction followed the intended missing-type RED.

The exact inverse reconstructs the entire old allocator, including71 mechanics
and35 helper lines, byte-for-byte without whitespace normalization. Root replayed
both private and actual promoted forms. Original BIN/CUE complete before/after
hashes match. All processes closed successfully; no native output was written.
Details and exact logs: `_local/research/terrain-gpu-extraction-drafts-r01.i77NcQ/RESULTS.md`.

This shared allocator is groundwork for the independently checked mixed
Wizard/material composition. That later chain must preserve the completeE5800
upload and GPU65800:7F000 sound-transfer residue, retain actual source ancestry,
and match all three independent image expectations before publication/gameplay.
This extraction alone does not enable mixed export or universal texture importing.
