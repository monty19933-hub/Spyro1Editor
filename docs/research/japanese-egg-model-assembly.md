# Japanese F5 model: detached USA data assembly

This is a model-data assembly proof, not a cross-region actor or export approval. It installs no Moby rows, behavior, colors/profile selectors, collection logic, inventory, percent policy, save extension, or runtime hook. No disc, emulator, preserved app, or normal save is changed.

## Issuer and existing compiler path

`NativeJapaneseEggModelAssetPlan.Capture` requires distinct exact JP SCPS-10083 and USA SCUS-94228 source sessions. It retains the authenticated JP entry23 model from `NativeJapaneseEggModel.Capture` and a real USA `NativeMobyModelLoaderCertificate.CaptureRetail` with all 54 shared registrations. F5 is absent from those registrations. Fixture construction is internal and cannot issue a source identity or consumer proof. A certified plan requires the complete exact original USA destination resource; an edited or equal-length fabricated body cannot acquire that status.

The source package remains exactly `0x808` bytes, SHA256 `da0c1ae7ab529a8ab5b43954452f368adb93c09f1a9b17013c52ee3039f4a810`. Its extent comes from the geometry owners in [the source closure](japanese-egg-model-closure.md), never the next registration's root. The package has no external texture dependency and its six native pipeline read-aheads are inside proved package owners.

The new adapter contributes one immutable `NativeModelTailPackage` to the existing `NativeModelTailBatch`. That kernel owns allocation growth, all real writes, registration-table validation, class collision refusal, later subfile relocation, new zero padding, and the complete exact inverse. No new resource writer was added.

The existing `NativeMobyDependencyClosureCompiler` imports the exact JP package and independently resolves the new registration's zero root field to its actual Final package extent. It requires the four consumer-window reads below as a dependency. Destination-resident nodes promise retention of every original descriptor-owned subfile, including the whole old model allocation and scene. Materialized graph payloads must equal the tail kernel's final bytes. The kernel's exact inverse additionally covers the complete header and every complement byte.

## Native pointer encoding stays on disk

The existing `NativeRelocationMap` proves both field and target ownership inside the complete source package. These eight fields are **not pre-rebased in the resource**:

| Package field | Original word | Package target | Encoding |
| --- | --- | --- | --- |
| `34` | `00000070` | `70` | root-relative model data |
| `38` | `0000003C` | `3C` | root-relative animation |
| `50` | `00000000` | `70` | model-data-relative near faces; zero is valid |
| `54` | `000003EC` | `45C` | model-data-relative near shading |
| `58` | `000002A8` | `318` | model-data-relative far faces |
| `5C` | `00000504` | `574` | model-data-relative far shading |
| `60` | `FF00061C` | `68C` | frame0 flags plus low21 model-data-relative address |
| `68` | `FF0006D8` | `748` | frame1 flags plus low21 model-data-relative address |

`ProjectLoaderAddresses` is pure verification: normal fields become `runtimeRoot + packageTarget`; each frame word adds the low21 bits of `runtimeRoot + 70`, retaining the original high flags. Aligned KSEG0 roots must contain the whole package in main RAM without low21 carry. This does not establish free runtime space, placement, memory budgets, or safe behavior. On-disk bytes never receive these runtime values.

## Pinned USA consumer compatibility

Exact source loader windows have the same package grammar; differences are the two relocated jump words at relative `44` and `1C0`:

| Consumer | EXE offset / length | SHA256 |
| --- | --- | --- |
| JP loader `800128D8` | `30D8 / 26C` | `67eb37fcb000ba1634438c7d45311481c766e171114dae214429731cb8d3f7a4` |
| USA loader `800133E0` | `3BE0 / 26C` | `fede3c414aea2dce0372fce574549d961123cf6456a7a291acef48b9c4af8598` |
| JP vertex consumer `8001FEC4` | `106C4 / 474` | `cf1bf8f5b7852586b9d63f49d9ccf759fb426f3515f62880d617bee3887a6db6` |
| USA vertex consumer `80021138` | `11938 / 474` | `11c407e46e92faa2fa65b8b1f9689a99d00eb56aaafcbb25ceefd3fd2a9ebc22` |

The vertex consumer differs at relative `50,54,68,6C,E8,124,1A8,1E8,1FC,2B0,2F0,348,390,418`: ten jump relocations, two scratch-address words, and two screen-clipping constants. The issuer pins all four full-window hashes and the exact difference sets. This is bounded package-loader/vertex-stream compatibility, not behavior, renderer appearance, clipping equivalence, or gameplay certification.

## Two independent exact destination owners

| Original USA data | New F5 slot / root | Growth | Detached afterimage SHA256 |
| --- | --- | --- | --- |
| Artisans entry10 | `35 / 1C9800` | `1000` | `cb3a7ef022ed66113816e08b07ce8206922e3554ce36400c46e7541c749d1857` |
| Town Square entry16 | `37 / 1D0000` | `1000` | `b056437ab4f888ed02c99e3e97a2970b17cb0256a7412862d67dca19430f485c` |

These are separate complete destination resources, each with one F5 registration and identical unchanged package bytes at a different root. They are not two independent F5 class registrations in one table: the existing kernel correctly refuses that collision. Multiple actor instances and independent collectible identities remain separate work.

Focused tests live in `Spyro.Editor.NativeJapaneseEggModelAssemblySmoke`. No existing smoke project or regression runner was edited for this slice. Source-free tests cover two different model-tail positions, eight field/target mappings, valid zero-relative pointers, packed-frame flags, invalid runtime roots, whole-resource preservation/inverse, copied-buffer isolation, deterministic graph output without owner-identity aliasing, shared/local class collisions, source-ancestry refusal, and malformed descriptors. Exact-source tests authenticate both discs, compare the complete package through geometry readback, verify both resources and consumer dependencies, exercise afterimage/preimage tampering refusals, and verify both named/held source images unchanged.

Run source-free with no arguments; exact-source invocation is `--source <original-JP-BIN> <original-USA-BIN>`. Logs: `_local/research/japanese-content-20260925-r01.RCmBCn/egg-model-assembly-r01-{build,sourcefree,source}.log`.

Verified on 2026-09-25: focused Debug build with zero warnings/errors; `JP_EGG_MODEL_ASSEMBLY_SOURCE_FREE_OK checks=165`; `JP_EGG_MODEL_ASSEMBLY_SOURCE_OK checks=183`. The exact-source run includes both post-read held/named source verification gates. Broader regression and gameplay are not claimed by this slice.
