# Source-bound native Moby instance allocation

This phase adds a reusable allocation/instantiation layer, not a second chest
candidate or universal runtime acceptance. Two independent fixture families now
materialize through the real dependency graph with one immutable model binding
and separate rows/properties. The exact native Town V2/V3 single family uses the
same allocator. Two simultaneous native key/chest families are explicitly refused.

## Native behavior boundary

Private properties alone do not establish independent behavior. The unchanged
native family also consumes mutable global state:

| Native state | Actual consumer evidence | Consequence |
| --- | --- | --- |
| `0x80075830`, one key state word | Town key pickup writes 1 at `0x80083C7C`; chest reads this state and writes 2 at V3 `0x8008A568`; global award tail writes 3 at `0x8003BA2C` | A second private key/chest still addresses the same native state |
| `0x80075758`, one tracked chest-gem pointer | Chest emission sets the tracked gem; `0x8003BA1C..0x8003BA2C` compares the collected gem against it | Separate reward-owner indices do not make this pointer per-family |

The source-bound factory registers both mutable-global obligations with their
actual consumer reads and exact executable address domain. Overlapping global
ranges between different requested families fail before an allocation is
returned, even when the labels differ. This is a refusal of **unproved native
independence**, not a claim that every possible two-chest arrangement necessarily
crashes. A caller cannot evade it by requesting “sequential” activation: no
native scheduler/rearming/global-reset protocol has been certified or added.

The allocator considers its requested imported families. A producer must also
account for **existing destination-native users** or prove them absent. The
exact Town factory proves no original row has class `0xAD`, `0xAE` or `0x0D`,
under the already certified retail code. This is not a universal class-name
heuristic for modified overlays. Its entire original Town scene is now pinned:
length `0x8800`, SHA256
`63b5e699b3f175c0289a795c4f6f36786b9bc33ce608e1f1e9bd7bb4c04f09f6`.
Changing an otherwise untouched scene byte fails this source boundary.

Native emitted reward identity is separately byte-bounded: a child records its
placed parent index in `+0x56`; the native path has a `<256` guard and otherwise
falls back to owner zero. The allocator therefore refuses an imported
persistence-bearing source/destination index outside `0..255`. This protects
the actual durable-owner scheme; it does not grant arbitrary scene growth or
promise that every other native subsystem supports 256 rows.

## Code and actual data flow

- `src/Spyro.Editor.Core/Scene/NativeMobyDependencyInstantiation.cs` captures
  detached private schemas, immutable resource bindings and mutable-global
  obligations; allocates immutable append-only ledgers; remaps declared private
  owner-index fields; and emits typed property-reference plans.
- `src/Spyro.Editor.Core/Exporting/NativeTownSquareLockedChestInstantiation.cs`
  derives the exact seven-row schema from native donor rows, properties and
  fixups. It pins complete donor/executable/scene preimages and the destination
  row append arena. No caller-authored ownership JSON is accepted.
- `NativeTownSquareLockedChestClosureCompiler.cs` first closes the actual
  immutable package/texture/behavior/sound/effect graph. The allocator consumes
  that closed binding, then drives all seven row/property destinations, private
  owner remaps, property pointers, row count, property length and appended
  fixup entries. The final graph must retain the exact shared bindings.

The preserved recipe allocates T107–T113, seven private property blocks totaling
`0xE4` bytes, and seven new pointer-fixup fields. The returned allocation is used
to build bytes, not merely attached to a receipt after the old literals run.
All 107 original native rows/properties remain protected by the existing
source-to-final preservation map. Publisher code, recipe selection and candidate
sets were not changed in this phase.

## Reuse and ownership rules

1. An identical repeated family request reuses its exact private allocations.
   A new family identity receives fresh row/property extents. A repeated identity
   with different native source, payload, domain or relocation policy is refused.
2. A batch is sorted deterministically; failed later members cannot consume rows,
   properties, persistent-owner indices or shared-resource entries. Retrying the
   remaining valid batch has no allocation holes or private aliases.
3. Shared resources are compared by native payload, source reads, exact original
   and final address-domain objects, preservation/install extents, resolved
   references, dependency identity, and the code-owned relocation policy. Equal
   content hashes alone never authorize context-sensitive relocation reuse.
4. Unknown final domains, unproved overlapping shared bindings and any overlap
   with the reserved private arenas fail closed. Reusing a label on another
   address-domain object does not establish identity.
5. Declared owner-index fields are verified against actual source parent indices
   and remapped to the newly allocated family only. Scene pointers remain typed
   address references, not integer owner fields. Source properties may legitimately
   alias when the native schema permits that; final mutable copies remain separate.

`Capture` verifies declared native bytes and structural ownership. It does not
discover arbitrary actor semantics or turn an authored schema into authority.
Only a code-owned, source-validated factory may choose owner-field schemas,
mutable globals, rootless behavior obligations and resource relocation policies.
Spatial placement, runtime behavior and new native-family authorization remain
separate gates.

## Verification

Focused Debug build completed with zero warnings/errors. The source-free suite
passed 147 checks; the source-backed V2+V3 run passed 496 checks. New cases cover
actual two-family graph instantiation, private isolation, stable retry, a failed
later batch member, byte-owner 255/256 boundaries, native singleton exclusion,
unproved sequencing, stale full source preimages, source property aliases,
unknown preserved domains, and same-content/different-domain/policy resources.
Read-only retained candidate verification also passed: V2 455 checks and V3
499 checks, including exact BIN/parity/protected-byte and CUE/receipt readback.

Existing exact graph/data goldens remain unchanged:

| Recipe | Graph SHA256 | Logical data SHA256 |
| --- | --- | --- |
| V2 | `d484fc13504ac0afc0b7a63e81dd4a6a3a3a3a7672dd7959654b02fd6103a3edcc` | `e442a02d3aa899dca0d5ddac4392f4b602aabb7ac6dc5dd1e4616c00fab6908d` |
| V3 | `52c68e8595a21ef8807abb6090dfb02d84a8643af114346c3c7c962247c45d6d` | `880c487599e8382447cd7b5c2d1799abec4eafd5af26351f37a22f66ed8ec9b0` |

The new fixture capability is structural evidence. Existing exact V3 gameplay,
normal portal re-entry and recorded audio observations retain their original
scope; they do not become evidence for two simultaneous native chest families.
No new candidate, installed editor, ROM or emulator state was modified.
