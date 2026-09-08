# Actual source-texture preview boundary

September 8, 2026. The decoder boundary is implemented and tested; source
inspection in the window is not yet implemented. Preserved R03/R04 apps have
not been rebuilt. This is not a destination-compatibility or gameplay claim.

`NativeFreshMaterialSourcePreview` accepts only an actual validated static
terrain source. It retains that source, extracts descriptor0 through the existing
source API, and uses the existing LQ decoder. Its constructor is private, its
properties are read-only, and every RGBA result is a defensive copy. The caller
may select one of16 palette rows; the default is explicitly row0, not inferred
camera distance. The result is32×32; transparency and PSX RGB5 expansion remain
the decoder's existing behavior. No new byte/path/hash issuer or App friendship
was added. Destination allocation, schematic rendering, runtime and publication
flags all remain false.

Root's original-only oracle extracted four actual source records,14/T0,14/T1,
70/T0 and70/T1, with all16 rows each. H independently repeated it byte-for-byte.
The14746-byte literal SHA is
`f99881d5641d1d3c255e2fa71b97ded092176500bc41a6731514419ed663f149`.
It was not generated from the C# output. The actual C# source gate then matched
all64 RGBA hashes, descriptor/pixel/palette identities and transparent counts.
Whole held/current original authentication, true EOF and all closes passed.
The original BIN/CUE pair also matched independent before/after hashes.

The intended missing-type API probe failed with exactly one CS0246 before
implementation. Inclusive Release build then passed with zero warnings/errors;
source-free405 and actual-source268/total673 passed. Existing UI gates passed:
material48, aggregate219, session138, legacy65 and research333. The picker now
passes100 checks, including genuine unknown/non-string kind selections and
invalid Base fields combined with valid material/route changes. Those controls
prove whole-form refusal without losing raw input or partially applying edits.

H's complete wrapper/tests/fixture review was clear, with one test-only correction:
the existing LQ cache has two descriptor entries, distinct from the23-descriptor
source layout. After the330/zero-failure combined gate, the test added first-invalid
index2 and relabeled index23. A focused build passed0 warnings/errors and free406;
the actual source comparison and its immutable64-row literal were unchanged.

Both regenerated1120×800 picker images were inspected by Root and Zeno. They
show the existing raw controls, not source thumbnails. Evidence and exact logs:
`_local/research/material-source-preview-implementation-r01.XkJvUD/`.
Independent original-only oracle:
`_local/research/fresh-material-preview-oracle-r01.usQXof/`.

The next App worker must finish actual source reads and independent final checks
before releasing its owner, even on cancellation or close. Only an explicit
inspection action may open source data; Apply/Save and ordinary picker changes
must remain source-free. A captured source thumbnail must not imply that the
material fits a destination or that the schematic/game already renders it.
