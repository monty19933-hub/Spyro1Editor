# Native Dark Hollow Wizard: owned scene-append stage

This is a detached, source-bound scene producer, not a playable enemy import.
`NativeDarkHollowWizardScenePlan.Capture` accepts the exact original retail image
and issues one diagnostic Wizard Peak T6 → Dark Hollow T128 scene stage. It does
not publish a BIN, change any candidate, install native behavior/model resources,
or approve the donor coordinates as a Dark Hollow placement. All behavior,
placement, runtime-fit and publication flags remain false.

## Native ownership proved

The original image SHA is
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
The issuer verifies its complete length/hash before and after capture and pins
the complete original Dark/Wizard data entries, executable, scenes, donor row,
donor properties and native pointer-fixup obligations. Catalog names or authored
offsets cannot issue this public plan.

Dark scene is data `0x194000 + 0x11800`, original SHA
`c48c344d3809f1ea5761e968ce6f18b02d5c7bbb82fcec4a7fd453343dbb84f8`.
Its actual WAD address is `0x1095000`, not the level-entry anchor `0xF01000`.

| Owned change | Exact source-derived extent |
| --- | --- |
| Placed count | Scene `0x8C34`, 128 → 129. |
| New row | Scene `0xB838 + 0x58`; pointer → `0xEED4`. All 128 old rows remain exact. |
| Properties component | Header `0xE038`, length `0xE9C → 0xEEC`. All old body bytes `0xE03C..0xEED4` remain exact. |
| New private properties | `0xEED4 + 0x50`, with internal pointer at `+0` → own `+0x28` (`0xEEFC`). |
| Native pod/collision | Original `0xEED4 + 0x201C` copied exactly to `0xEF24`. |
| Fixups | Original 147 entries retain exact order/values; append `0xB838`, `0xEED4`. Final list starts `0x10F40`, count 149, ends `0x11198`. |
| Scene sectors | Still exactly `0x11800`; growth consumes `0x58` of certified zero tail. |
| Shared dynamic actor pool | Native `floor((MobyBytes - 8 - placedCount*0x58)/0x70)` decreases 91 → 90. |

The source donor row is scene `0x6388 + 0x58`, SHA
`59401eafeab453fd9d26f14eedca58390653dc5f4fcc3d2560f7b3e8fc7afa82`.
Its properties are `0xC350 + 0x50`, SHA
`5b3d762f6117ea30e959a21bf2778f3442b03eba93125c9a0e0a642cc58d345b`.
The original donor fixup list contains the row pointer and exactly one internal
field in this private extent, at `0xC350`. Source pod byte `row+0x43 = 0xFF`
and original route/position bytes are preserved. The row's native durable owner
index becomes 128 through ordinary append order; it is not an arbitrary new
byte in the donor row.

Dynamic-pool arithmetic is bound to executable file `0x46F4 + 0x168` (native
scene loader, SHA
`7bfb42fba56df4b4772f4a32f0114940090e0f4029456ed37ea8ac5e871be6d7`)
and file `0x42CC4 + 0xA0` (native allocator at `0x800524C4`, SHA
`f3f404687b98b5208f97274a1932c967ba8d0bd04337011b53d831f25f592335`).
The allocator consumes `0x58` bytes upward for a dynamic actor and `0x18`
downward for its properties. A zero-filled row interval is therefore not free
properties scratch. Unchanged scene sector count does not imply unchanged live
actor capacity or workload.

## Compiler and composition boundary

`Scene/NativeMobySceneAppend.cs` validates native component boundaries, every
old pointer field/target and the complete zero tail. It reuses
`NativeMobyDependencyInstantiationPlan` for transactional private allocation,
stable retry identity, source-bound schemas and byte-sized durable owners.
The dependency graph drives extraction, per-instance installs, row/self pointer
remaps, count/length changes, shifted components, fixups and preserved ranges.
Every final scene byte must have an explicit install or preserved-source owner.

Two synthetic scene instances share immutable schema evidence but receive
distinct rows, properties and self targets. This proves allocator isolation,
not that two native Wizards have completed behavior/global-state closure.
Original scene installs from a shared resource graph are refused, including an
attempt to overwrite a nonpointer field in an old row. Same content in a wrong
address domain is not accepted as an original Scene source.

The neutral stage deliberately refuses old pointer fields/targets requiring
unknown movement at or beyond the properties insertion. It also refuses
precompiled shared graphs containing resolved references: that receipt does
not retain the original unit/base encoding needed to relink them faithfully.
A future full behavior composition must supply its complete typed graph, not
infer an encoding from already-relocated bytes. This restriction does not alter
the established Town V2/V3 closure compiler or its fixed-reservation contract.

The detached scene replacement carries its actual original WAD address and
unchanged length. It composes through the existing one-pass outer level layout
after sound-bank, data-descriptor, player-stream or model growth. Tests cover a
synthetic model-end append and actual source-issued electric sound/player-8
plans alongside this scene. Changed global sound-map consumer ownership and
player consumer closure remain explicitly unresolved; composition is not their
approval.

## Independent output oracle and gates

A separate Node raw-sector reader constructed the new scene with native
component splices and the seven explicit count/pointer/fixup words, without
calling Core or writing any image. Its byte hashes match the source producer:

- Scene: `9ac99fdd150133a75aac8298da4b738dcbcdd12a1ab649088f53c300bd1a37c4`.
- Scene-only level: `6fd296b8f56f70f1d993596fe262076e3b2674865c24a1070543979334e56e38`.
- Initial scene graph: `4972546c3a37c9043e303d9f97722240fafbf89d61e89bf02e6737a73240f9bc`.
- Actual native sound-bank/data/player-8/scene composition:
  `0dbedb93c71342996d74ce7e347433b1afbd70a84f5a534107948466c63190dd`.

The read-only oracle source is retained at
`_local/research/dark-wizard-native-closure-20260906/scene-splice-oracle.mjs`.
Its fresh replay checks the complete original image before and after reading;
it creates no output image or other file.

Regression-first RED built clean and failed at the unimplemented source capture.
The final focused Debug build passed with zero warnings/errors. The complete
closure smoke suite passed **262 source-free / 1,056 source-backed checks**,
including unchanged Town V2/V3 reconstruction goldens, two-instance private
isolation, the old-row shared-write refusal and four-owner native composition.
These are aggregate suite counts, not a count of new scene tests alone.

Still required before a native enemy candidate: complete intact behavior and
callback linking with owned stack/context, model registration and resources,
sounds/effects/player dependencies, source-bound mover/collider placement on a
validated Dark surface, final composed memory/ownership checks, immutable
publication readback, and actual gameplay including movement, lightning,
native electric hurt, death effects/rewards and re-entry. No old adapted
generic-hurt or child-suppression profile is promoted by this scene stage.
