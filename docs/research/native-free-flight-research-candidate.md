# Toasty native Free Flight research producer

2026-09-06. This is a new-only, exact-source research recipe, not ordinary
Create BIN support or a gameplay certificate. The default smoke and its
three-argument source-plan mode do not publish. The separately approved
five-argument `--publish` command invokes explicit research publication.

## Source and composition

The unchanged USA BIN is SHA-256
`fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37`.
`NativeFreeFlightResearchCandidatePlan.CaptureAsync(BIN, CUE, catalog)`:

- Captures the [destination-scoped executable plan](native-free-flight-source-contract.md)
  for Toasty14 and the general source-bound root0 animation merge from
  Gnasty's Loot64, IDs33/34/35. It never substitutes an already edited scene.
- Requires exact inserted records `0x33C8`, model-sector growth `0x3800`,
  and final target SHA
  `251770f0eade8981fd6fca94d3a0bf434baeaff23ee83520dda0bf9d78079c5b`.
- Binds the exact original CUE bytes and BIN/CUE alias paths. The profile
  supports the current one-file, one MODE2/2352 track, INDEX01-at-zero CUE
  geometry only. Other initial geometry is refused; a later synthetic PREGAP,
  deletion, equivalent FILE spelling change or alias rebinding is not fresh.
- Obtains the typed native RAM report from the source-issued inventory and
  original complete EXE/PETE certificates, then analyzes the exact merge.
  Every EXE byte outside the eleven owned windows remains original, including
  the copy-buffer/PETE loader consumers; Toasty's copy initializer at EXE
  `4B130+10` is independently checked unchanged.

Known normal-load extents after the merge:

| Bound | Address/bytes |
| --- | --- |
| Native copy buffer | `80084A10` |
| Used level data | `0x6E220` |
| Model start/read bytes | `800F2C30 / 0x7D000` |
| Scene start/read bytes | `8016FC30 / 0xD800` |
| Scene read end | `8017D430` |
| Normal reserved-buffer start | `80187BB0` |
| Positive known-extents gap | `0xA780` |

This positive gap does **not** prove peak/transient RAM, alternate buffer
modes, live dynamic/particle occupancy, stack/renderer peaks, VRAM/SPU,
complete effects/sound consumers, controls or gameplay.
`RuntimeFitProven`, `PeakTransientFitProven`, `RuntimeVerified` and
`NormalCreateBinApproved` remain false.

## Writer and independent readback

The publisher writes a private source-derived WAD analysis and passes the
merge's copied typed insertion payload to the shared `NativeSkyWadRelocator`.
It resolves the **live relocated** `SCUS_942.28` from the output ISO before
applying the exact executable plan: LBA53875 becomes53882. WAD size becomes
`0x692A800`; the source image byte length is unchanged.

An independently composed complete WAD/root/EXE oracle checks the whole final
target against the animation plan, every other WAD entry (including overlays)
at its mapped location, all original model/scene payload bytes, the exact EXE
complement, and every physical sector. Raw-sector checks include newly owned
gap initialization, old/new EOF/EOR flags, header identity, EDC/ECC and the
complete untouched image complement. CUE/receipt bytes and whole-image hashes
are replayed before and after publication.

Publication uses `ExportArtifactTransaction.PublishNew`, BIN/receipt first
and CUE last. Existing complete sets require exact verification; partial or
occupied sets, directory artifacts, final symlinks and source/ancestor overlap
are refused. A cooperating-writer lease fences staging. Structured incomplete
rollback and cleanup-warning results retain stage/recovery evidence and expose
`RetainedRecoveryDirectory`. These are trusted-directory exception-rollback
guarantees, **not** hostile-filesystem-race or process-crash atomicity.

## Verification and publication boundary

The initial test-project build deliberately failed with four missing-contract
CS0103 errors, zero warnings,4.77s. The implemented Debug build passed in4.93s,
zero warnings/errors. Executed results before adding the explicit publication
CLI:

```text
NATIVE_FREE_FLIGHT_RESEARCH_SOURCE_FREE_OK checks=49
NATIVE_FREE_FLIGHT_RESEARCH_SOURCE_OK checks=55 plan=02dc94f3a45fac553ecc3ae234760c6ff3bd9e4cdf26a94d1a3e38a3bae1154a gap=A780
NATIVE_FREE_FLIGHT_RESEARCH_ROLLBACK_OK stage=AfterRelocation
NATIVE_FREE_FLIGHT_RESEARCH_ROLLBACK_OK stage=AfterExecutable
NATIVE_FREE_FLIGHT_RESEARCH_ROLLBACK_OK stage=BeforePublication
NATIVE_FREE_FLIGHT_RESEARCH_STAGING_OK checks=20 noCandidatePublished=true
NATIVE_FREE_FLIGHT_RESEARCH_SOURCE_OK checks=75 plan=02dc94f3a45fac553ecc3ae234760c6ff3bd9e4cdf26a94d1a3e38a3bae1154a gap=A780
```

Staging negatives require their exact intended failure, not any exception.
The complete private staged set is reused read-only, corrupted companions are
refused, and the enclosing test then forces rollback. Further tests corrupt an
unrelated raw sector, change only a private test-source CUE late, check every
failed stage ends empty, and preserve occupied fixture data.

Raw logs and the inspected private fixture are under
`_local/research/native-free-flight-research-20260906-r01/`.
No candidate was published by these tests. After source checkpoint/full-gate
verification, the approved new research destination is exactly
`_local/research/native-free-flight-research-20260906-r01/candidate-r01`.
The root task owns that explicit publication and subsequent runtime testing.
Neither the installed editor nor the original ROM/CUE is modified.
