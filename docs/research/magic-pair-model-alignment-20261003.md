# Magic pair asset certificates after native model alignment

Fresh current-source verification found that the earlier player-animation alignment fix changed the complete Magic12/13 model owners in both supported hosts. The old Artisans rescue normalizer and Dark Hollow final asset certificate correctly refused the changed bytes, but consequently blocked those previously available exports. The failure logs are preserved as `magic-rescue-current-r2.log` and `magic-darkhollow-alignment-current-r1.log` under `work/universal-finish-20261002`.

Independent byte comparisons authenticate the former source-bound assets and prove that the current importer retains the complete animation stream and terminator, adds exactly four zero bytes after it, preserves every model payload and consumes four terminal zero-padding bytes. All later model roots retain their original source alignment. Every unrelated asset subfile remains byte-identical. The two immutable asset manifests now bind these verified current bytes.

Artisans retains all33 models, all83756 imported payload bytes and the same288768-byte model subfile. The imported package is now84296 bytes including540 padding bytes, so the exact rescue rotation moves the whole package. No padding moves between packages and every complete before/after package is byte-identical. Required rescue reservation remains243712 with280576 available; RAM headroom remains88728. The six original Gnorc removals and all supported/unsupported placement rules are unchanged.

Regression checks now compare every retained and imported root modulo8 directly to its original source, verify the exact player terminator/gap, and retain full package, registration, input-mutation and rescue-capacity controls. Artisans ordinary export receipts also record the actual exporting Core version and SHA256.

Root evidence:

- `magic-rescue-alignment-current-r1/independent-full-model-proof.json` and `darkhollow-independent-full-model-proof.json`: independent complete-package/alignment migration proofs; the independently rotated Artisans header/model section exactly matches the fresh ordinary export.
- `magic-rescue-current-r4.json` and adjacent `.artifacts`:3302 source checks including415 rescue/alignment controls;284 saved deletion/reopen/ordinary export/readback checks. Candidate BIN SHA256 `01917fda89c0ed3bff23c9210efc90c29bc73227895dc7ed5023aaa5c9a85c90` matches the separately built corrected r3 candidate exactly.
- `magic-rescue-current-r3-native.log`:128 actual original native rescue reader/copy/model-reload checks against independently extracted final data. These retain CD/SPU hardware boundaries and are not gameplay evidence.
- `magic-darkhollow-alignment-current-r2.json`:8791 source/certificate/mutation checks. New complete Dark Hollow data SHA256 `b737012df4b565c3e2f76fd4d419267f87092c045afe4b1e5582bfa31b1df08b`; native overlay bytes remain unchanged.
- `magic-rescue-alignment-current-r1/app-build-final.log`: Release App builds with zero warnings/errors.

Earlier native r5, Artisans combat/card and rescue evidence remains attached to its earlier candidates. Fresh gameplay of these current exports is pending. No ordinary-game runtime writes, save states, original-disc changes or user-project edits were used for this repair. Universal gameplay and the V1 release remain unfinished.
