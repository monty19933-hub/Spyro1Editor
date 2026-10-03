# Beast Makers and Misty Bog boar placement

All nine original class `0x89` placements now carry their native path nodes,
saved home coordinates and shared charge state when moved into another level.
Misty Bog's linked chicken and boar keep their reciprocal indices and relative
positions. Removing either original linked actor alone is refused. Class aliases
translate the native active-list comparison without changing its physical row
pointers or stores.

The integrated source checks pass 7,453 class-ownership checks and 7,111 placement
checks. These cover all nine moved variants, forced class aliases, native controls,
linked removal, source ownership and mutations. The Release App builds with zero
warnings and errors. These results establish composition, not native gameplay.

Evidence root:
`work/universal-finish-20261002` in the task workspace, two directories above this
repository. Reports are `beast-misty89-integrated-r1.json` and
`beast-misty89-placement-integrated-r1.json`.

## Failed native test retained for repair

`coverage/beast-misty89-cleared-runtime-candidate-r4/candidate.bin`, SHA-256
`1d6c2021114219d83d2a52107410f54429fc4b22add205d4d8cfc9a022345e99`, uses the
ordinary cleared-level compiler and composer. It places Misty Bog row 9 at
`36000,36000,8352` on 121 textured, collidable floor tiles in cleared Peace Keepers.
The boar visibly loads and moves along the translated path.

The actual New Game run used real Sony firmware with fast boot disabled, a private
blank card and native controls. No save state or emulated-memory writes were used.
Movement later exposed invalid native renderer reads at `800267F4` and `800269CC`,
followed by writes outside the scratchpad at `80026994` and `800269A0`. DuckStation
asserted in GPU DMA channel 2. This candidate is **failed**, not gameplay accepted.
Attack, defeat and reward were not verified.

`runtime/boar-misty9-cleared-r2/runtime-failure.json` records the sequence and card
hashes. Its `crash.ram` and host backtrace preserve the failure; the full emulator
log is `runtime/boar-misty9-cleared-r2-runtime.log`. A read-only host debugger
captured RAM after the assertion. No Ignore action was used. The private process
subsequently exited. Original disc images, user projects and original cards were
not modified. Terrain rendering repair and a fresh native test remain required.
