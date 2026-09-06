# Source-issued fresh ID65 disc byte plan

This stage composes actual fresh body bytes, an exact retained engine overlay,
the reserved WAD pair, and the original executable into a read-only physical
afterimage/inverse plan. It has no output path, CUE, publisher or normal Create
BIN mode. Native execution and complete level/resource approval remain false.

## Source and input boundary

`NativeId65FreshDiscPlan.CaptureAsync(originalBin, issuedBody, track)` accepts a
private-issued `NativeId65NormalBodyPlan`. Its acceptance is deliberately the
current untextured two-triangle resource profile: used data `0x8C8`, data sector
`0x1000`, upload `0xDE000`, model `0xF800`, scene `0x2800` or `0x3000`, exact two
model roots and original copy/model/scene/PETE/reserved RAM anchors. Total CD
length, a class name or `SourceCertified` alone is insufficient. A future
textured body with used data `0xA38` but identical total CD length refuses.
Any future closed body-instance resource discriminator must remain an explicit
check here; no future typed revision is implicitly accepted.

The original-only BIN is `661547040` bytes (`281270` MODE2/2352 sectors), SHA-256
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
One complete read hashes the whole image and each physical sector from the SAME
buffer bytes. The private index is `9000640` bytes. No caller digest, metadata
or sector can confer source authority. Every subsequent read, including PVD,
root, path tables, WAD header, complete EXE and gap, reads and authenticates its
whole containing sector before a parser receives any bytes. A final whole hash,
actual EOF probe, length/position checks and cancellation guard finish capture.

The existing ISO/growth parsers are reused through narrow read-only `Stream`
overloads. Existing string/FileStream signatures delegate to the same code;
write APIs and CUE behavior are unchanged. The old relocation record is used
only to validate physical growth geometry, never passed off as an occupied-entry
or payload authority to the NativeSky writer.

## Exact composition and inverse

The exact original EXE SHA is
`a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998`.
Four guarded bootstrap windows produce
`0f6996babd64e815529df648fd7136f478c42f8187567d71439462df12b931ec`.
A temporary pointer at file `0x6007C` reaches the unchanged music compiler's
source certificate `fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442`.
Its native built-in music plan runs unchanged, then the pointer is restored and
the exact four-byte placeholder at `0x65D64` changes from `A\0\0\0` to `LAB\0`.
The preceding eight-byte count table and separate `JACQUES\0` at `0x65D68` are
preserved. Sequential preimages/inverses are verified before final disjoint
original-byte differences are exposed. All 46 selectable tracks have separate
independent complete-EXE golden hashes; reserved IDs 30 and 35 refuse.

The WAD at LBA37 grows only by the appended exact overlay `0xF800` and fresh body
`0x1FB000` or `0x1FB800`. Original empty rows79/80 gain these allocations;
occupied row81 and all other WAD storage survive. The original EXE payload is
relocated from LBA53875 to54920 or54921 and its live ISO root entry is updated
in both endian encodings. The complete following XA owner at60000 bounds growth.

Destination-sector MSF headers stay at destination LBAs. Old/new EOF/EOR bits
move with the WAD/EXE boundaries; canonical Form2 gap sectors become exact
Form1 afterimages with rebuilt EDC/P/Q. The plan retains at most1253 complete
raw original preimages (`2947056` bytes), so inverse restores the overwritten
original EXE AND original Form2 gap, not merely logical payload/root edits.
Even untouched sectors authenticate their own LBA digest in forward and inverse
paths. Changed inverse input must equal the complete final raw afterimage,
including headers, parity and MSF, before any original bytes are returned.

`VerifyImage` shares the same expected-sector implementation, checks cancellation
per sector, then probes actual source and output EOF in addition to the shared
whole-physical verifier's hashes/lengths/positions. It performs no write.

## Regressions and independent oracle

The focused smoke has explicit Contracts and StreamSeam compile modes with
different assembly names/output/intermediate paths. Each has a distinct marker;
one mode cannot silently stand in for the other. Both accept only zero arguments
or one positional original BIN. No workspace, CUE, output or publish argument
exists.

The regression runner builds Contracts through the ordinary project and links
the exact same `StreamReaderProbe.cs` into a dedicated
`Spyro.Editor.NativeId65FreshDiscStreamSmoke` project. This avoids mutable build
property selection in the runner and keeps both binaries independently present.
The dedicated Release build passed in1.55 seconds with zero warnings/errors;
its actual source-free and source runs passed8 and14 checks respectively.
The two registrations use distinct required markers and only a positional
original BIN in opted-in source QA. Runner regressions were RED on both missing
registrations before implementation; all46 Python tests then passed.

The integrated default Release gate passed107 steps with zero failures:
53 builds including App,53 run modes and one32-test Python runner step,
57.46 seconds summed. This is not a second complete build-all/source-QA run.
The first107-step attempt failed one existing test because its reflection
lookup selected `DiscImage.DetectLayout` by name alone after the new overload.
The test now requests the exact existing `string` signature; original fixtures
and production behavior were not changed. The final gate reran every step.
Both complete reports are retained under the evidence directory as
`default-gate-first-report/` and `default-gate-final-report/`; the final copy
was byte-compared with the original temporary report. JSON log fields still
refer to original temporary paths. Console logs are
`fresh-disc-default-release-gate.log` and `fresh-disc-default-release-gate-r02.log`.

Thirty embedded original code/data pins drive bounded scalar tests, including
the sixteen title pins plus actual dispatch and next-cycle owners. The tests
honor branch/JAL and R3000 load delays, retain explicit partial-entry witnesses,
and do not fake omitted formatter, complete glyph, reset or audio helper bodies.
They cover all35 native initializer IDs,175 original/patched classifier pairs,
60 direct selectors,144 title consumers,48 navigation cases with14 motion steps,
actual string length/NUL ownership, and uppercase glyph selection `char+0x169`.
The conditional guard is global game/transition state `0x800757D8`, NOT player
state `0x80078AD0`. Global state12 still bypasses the patched classifier and is
explicitly outside this profile. The normal next-level cycle still wraps65 to10.

Physical fixtures cover exact full afterimages and full raw inverses for both
body sizes, corrupt changed/untouched sectors and wrong-LBA inputs, all46 EXE
tracks, six transient post-index metadata/preimage corruptions, actual EOF lies
and early/middle/final cancellation. A positive verifier control-path test also
requires the returned complete hash and changed-sector count after all guards.
Its virtual output is explicitly not independent byte evidence; the separate
all-sector fixtures and Python oracle establish those bytes. No mutation is
applied to the source file.

The separate Python oracle reconstructs all body/header/EXE/ISO/parity bytes
without reading a Core assembly and without writing a BIN. It first reproduces
five actual original Form1 sectors and certifies all1046 original gap sectors.
Three whole afterimages are retained:

| Body fixture | Full raw afterimage SHA-256 | Changed sectors / final SCUS LBA |
| --- | --- | --- |
| A, N=16 | `1818fe50a132901b9ad670769d4967ccc0a5120897637efa2ad709b8f1d53188` |1253 /54921 |
| B, N=16 | `40047bd8e40f23b522bfaa04dd72353013fbcfddbbd9ba6b1e357b87e8664343` |1253 /54921 |
| A, N=1 | `e6d5f2abd512814bac54f48b2d3bb3255d09dc4d5abfb6aef87ddf82ea43fc08` |1252 /54920 |

The oracle's inverse hash is explicitly an expected-original hash; the C# smoke
separately executes the real exact inverse for every sector of both body sizes.
Source before/after hashes and actual EOF are required by both paths.

Evidence lives under `_local/research/native-id65-engine-profile-20260906-r01/`.
The final repeated three-body oracle JSON is
`fresh-id65-disc-raw-oracle-three-bodies.json`; earlier two-body evidence remains
untouched in `fresh-id65-disc-raw-oracle.json`.
Root observed intentional missing-contract and missing-Stream RED separately.
The first combined Core Release build was clean. Focused Contracts GREEN was
initially blocked by one test-only span capture (`CS9108`); snapshotting the
integer length before the lambda fixed it without changing semantics or expected
values. Final Release build: **1.49 seconds, zero warnings/errors**. The built
Contracts DLL passed **2182 source-free checks** and **566016 checks with the
exact original** (the latter includes source-free checks), including all three
whole physical hashes, both-size full inverses and the positive verifier return.
Final closeout logs (earlier evidence is retained):
`fresh-disc-contracts-closeout-build.log`, `fresh-disc-contracts-closeout-free.log`,
`fresh-disc-contracts-closeout-source.log`. Earlier566015-check logs:
`fresh-disc-contracts-green-build-r02.log`, `fresh-disc-contracts-green-free.log`,
`fresh-disc-contracts-green-source.log`. Root separately built the isolated
StreamSeam in1.41 seconds with zero warnings/errors and passed14 source checks;
logs `fresh-disc-stream-green-build.log` and `fresh-disc-stream-green-source.log`.

Executed commands (from the repository root):

```sh
dotnet build src/Spyro.Editor.NativeId65FreshDiscSmoke/Spyro.Editor.NativeId65FreshDiscSmoke.csproj -c Release -p:FreshDiscProbe=Contracts
dotnet src/Spyro.Editor.NativeId65FreshDiscSmoke/bin/Release/net10.0/Spyro.Editor.NativeId65FreshDiscSmoke.dll
dotnet src/Spyro.Editor.NativeId65FreshDiscSmoke/bin/Release/net10.0/Spyro.Editor.NativeId65FreshDiscSmoke.dll 'Spyro the Dragon (USA).bin'
python3 src/Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_oracle.py 'Spyro the Dragon (USA).bin'
```

Independent review: root read the three complete production files, both shared
Stream diffs, scalar fixtures and physical/capture cases; Huygens independently
reviewed the complete production/authentication/inverse boundary and earlier
reran the full raw oracle. Wegener independently read all three production
files and both read-only shared diffs with no material finding. The main stage
owner also reviewed Huygens's complete scalar port. These are engineering/source
reviews, not native execution.

## Deliberately pending

This composes the existing conditional body profile; it does not close its
eight callback lifetimes, companion0C/42 transitive consumers, complete GPU/SPU
state/timing, low16 SPU wrap, peak transient memory, dynamic demand, rescue/replay,
checkpoint/foreign entry, portal/save travel, native glyph allocation or runtime
acceptance. No-op callbacks, geometry-free retail inventory fiction, source ROM
edits, normal exporter activation and publication are not authorized by this plan.
