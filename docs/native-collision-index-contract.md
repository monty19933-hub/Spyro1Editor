# Native collision lookup contract

`NativeCollisionIndexCodec` interprets the existing bounded Z/Y/X tree and
block-group storage. It does not infer geometry, retirement authority, or
gameplay behavior from a decoded pointer.

- Segment pointers are byte offsets within the tree. Leaf values are word
  offsets within the block buffer, or `FFFF` for an empty cell.
- Groups start with a high-bit marker; the final `8000` word is a sentinel,
  not a triangle. The remaining fixed-capacity tail must be zero.
- Triangle references are 15-bit indices with explicit table bounds. The
  existing terrain compiler also requires strict descending order. The older
  remote-isolation reader retains its explicitly non-descending policy.
- Exact same-depth segment aliases are accepted. Partial overlap,
  incompatible-depth aliases, interior-group leaf pointers, unreachable groups,
  malformed markers, capacity exhaustion, and conflicting shared-leaf contents
  are rejected.
- Snapshots own copies of arrays, dictionaries and sequences. Rebuild writes
  only existing leaf words and block storage; it cannot invent missing tree
  segments or relocate any component.
- Equal immutable block sequences may share storage. Logical cells that alias
  one physical leaf cannot be given conflicting content.

Both original private decoders now use the codec while preserving their
production-specific nonempty constraints. Fifty-four source-free checks pass.
The actual terrain-set regression retains the four original collision hashes:

| Terrain | Collision SHA-256 |
| --- | --- |
| No extra tile | `00c88deb64eed8d829b504422e1dd21b1d6b4449c75126bdb2c0821d077c17b1` |
| Tile 1 | `047b0043c6892e57dd3cdb76c656b93650461e68105352f5d8e3ae2f6d1c8644` |
| Tile 2 | `a8fd9d9018e2d27283fa02d7bd908dc28f24bf2b4dc47f128be71f77588bcfb5` |
| Both tiles | `84291d79c381b709ebff3d6c3cffa31b850d0da7b609bea9db9bcbfb2761b731` |

The separately compiled ID65 collision-isolation stage owns the decision to
retain authored support triangles and retire foreign lookup cells. It leaves
the triangle rows, counts, assignment bytes and flags intact. Therefore this
stage must not be described as a completely minimal collision component or as
evidence that all possible non-lookup runtime consumers have been eliminated.
