# Fixed original Artisans egg test sites

`NativeJapaneseEggArtisansPlacementPlan` admits exactly two developer visual
test placements, not arbitrary editor positions. Its private constructor is
issued only by authenticating the complete original USA Artisans resource and
executable. It reuses the existing native collision-index/surface certificate.

| Row | XYZ | Yaw | Opaque native mode |
| --- | --- | --- | --- |
|174 / egg-a|72704,47104,6656|32|1|
|175 / egg-b|72704,49152,6656|96|1|

Both sites lie on unique original dry flat ground at Z6144. The **continuous**
corridor between them has half-width512 and clear height2048; four original
support triangles have four actual collision-index bindings. All174 original
actor centers are at least1536 horizontal units from that corridor, without
exempting actors at another height. The native radius byte is retained as opaque
evidence, not interpreted as an actual collision-volume radius.

This proves surface support and center separation, not safe navigation from
spawn, camera/frustum visibility, actor collision volumes, model pixels, pickup,
or gameplay. The original entry spawn remains84992,47104,9564, heading64. It
is not moved to make the test easier. Both eggs preserve original mode1; this
is not an independently colored pair.

The certificate binds the exact original data hash, complete surface report,
fixed ordered recipe and every original row hash. An actual integration must
share the exact original preimage and ordered recipe. Different positions,
order, count, identifiers or appearance bytes are refused.

Source-free17 checks and actual JP/USA24 checks pass in Release. Certificate:
`04eae9472e9c08186b02b42ffcace6aeabda0462db6b648cb70436610ffa85db`.
Logs: `_local/research/japanese-content-20260925-r01.RCmBCn/egg-placement-{release-build,source}.log`.

## Bounded read-only observer

`tools/Observe-SpyroJapaneseEggs.py` reuses the existing memory-read-only RSP
transport. It requires explicit opt-in **after** independently identifying the
test PID and exact open candidate. No receipt or annotation authenticates that
process. All input, execution and pause controls remain in the UI.

Source-derived constants: Scene80164D54, rows80172BD0/80172C28,
private states80176C20/80176C2C, shared model root80163D54,
near/far RGB arrays801641B0/801642C8, each118 bytes. The actual source loader
places rows at Scene+A2AC+index*58 and private states at Scene+11ECC/11ED8.
The enlarged model descriptor moves Scene's file offset to1CA800; Scene length
remains14800. The native class table slot80076378+F5*4 is8007674C.

Before reading these fixed owners, the observer gates level10, normal mode,
game state0, copy/model/scene pointers and descriptor lengths,176-row dynamic
start and89-slot capacity, and F5 root. Both row classes, private pointers,
format94, original mode1, live state0 and fixed XY must match. It does not chase
sampled pointers. It repeats gates and row identity afterward and discards an
actor result on a detected transition. Reads are sequential, not atomic, and
mutable bob/animation fields can change between reads. The observer reports
samples only; it never certifies gameplay, complete runtime memory or saves.
