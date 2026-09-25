# Detached Japanese F5 RGB editing

`NativeJapaneseEggRgbEditSource` and `NativeJapaneseEggRgbEditPlan` provide a
bounded, reusable color-edit kernel. They produce detached model bytes only;
they do not import, publish or bind an edited model to a runtime instance.
The existing model, asset, integration and research candidate issuers are
unchanged and continue to require the original native model bytes.

## Exact source and editable ownership

The public source factory requires the authenticated
`NativeJapaneseEggModelAssetPlan`, an exact-USA regional source session, and
the animated RGB consumer at PC `800215C8`, executable offset `11DC8`, length
`174`, SHA256
`4cd236569c340e49360938ad8397aed87f2a7518d13134c935fd7f32acb856a5`.
The complete source model remains `808` bytes with SHA256
`da0c1ae7ab529a8ab5b43954452f368adb93c09f1a9b17013c52ee3039f4a810`.
Caller package bytes, declared hashes or synthetic fixtures cannot acquire
this public source ancestry.

The existing model parser supplies the two shading owners: 70 near words at
`45C/118` and 70 far words at `574/118`. Each edit is a typed three-byte extent
inside the asset's original-data address space. Only low 24 bits may change.
The fourth byte stays `FF` for near words and `00` for far words: native
CODE/control metadata, not alpha. All face, vertex, frame, alignment and eight
source-relative relocation fields remain exact. The existing complete model
parser verifies every afterimage; no second geometry grammar is introduced.

Input values are unsigned `0x00BBGGRR`, with R in the low byte. Exactly 70
values per table are required; high-byte input and other counts are refused.
The source instruction semantics and near/far/fog distinction are documented
in [the render admission evidence](japanese-egg-render-admission.md). The two
tables are not interchangeable. No tint algorithm or native color preset is
invented. The source census found one native palette across all 30 registered
Japanese F5 model packages; all custom arrays are explicitly authored.

## API and reversibility

```csharp
var source = NativeJapaneseEggRgbEditSource.Capture(asset, usaSession);
var native = source.NativeOriginal(); // byte-exact no-op, NativeOriginal origin
var edited = source.CreateAuthored(nearRgb, farRgb); // 70 + 70 explicit values
byte[] detached = edited.ApplyExact(asset.Model.CopyPackage());
byte[] restored = edited.RevertExact(detached);
```

Every input array and returned byte array is detached. Exposed lists are
read-only. Apply requires the complete exact original package; inverse and
readback require the complete exact afterimage. Even a one-bit difference
outside the colors, a truncated package or an extra trailing byte is refused.
Changed-byte accounting is at most 420 bytes across 140 words.

Content identity is lowercase SHA256 over UTF-8 text
`jp-f5-rgb-v1\n<original-package-sha256>\n<final-package-sha256>\n`.
Identical native and explicitly authored arrays therefore deduplicate by
content while keeping their separate `NativeOriginal`/`Authored` origin.
`SourceAncestryCertified` certifies the original source, never the authenticity
or runtime acceptance of an authored afterimage.

`NativeImportApproved`, `DistinctPerInstanceRootBindingProven`,
`FinalPixelsVerified` and `PublicationApproved` remain false. In particular,
two rows using one F5 model root cannot have independent RGB arrays through
this kernel. Independent model-root selection still needs a separate proved
binding. No class alias, renderer hook, UI, save or pickup change is included.

## Focused regression

The standalone `Spyro.Editor.NativeJapaneseEggRgbEditSmoke` needs no shared
smoke Program changes. No arguments run source-free synthetic grammar,
ownership, immutable-list, malformed-input and exact-inverse checks.
`--source <exact-JP-BIN> <exact-USA-BIN>` runs the same preservation tests
against the actual authenticated package and USA consumer, rejects a Japanese
consumer or fixture asset as source authority, and re-verifies both discs.

Tests include every-byte preimage/afterimage tamper refusal, all non-RGB bytes
unchanged, all original pointer fields unchanged, both CODE bytes preserved,
32 deterministic authored arrays with complete parser readback, black RGB,
unsupported otherwise-valid 1/69/71-word models, malformed source CODE,
truncations, array counts and attempts to supply CODE bits. Native source and
existing candidate goldens are unchanged. The focused Debug build passed with
zero warnings/errors; 15,746 source-free and 62,777 exact-source checks passed.
Logs are
`_local/research/japanese-content-20260925-r01.RCmBCn/egg-rgb-edit-{build,sourcefree,source}.log`.
This test route reads source discs only; it writes no disc, candidate, save,
emulator or settings files.

Parent review repeated a clean Release build and both routes: 15,746 source-free
and 62,777 exact-source checks passed. The source-free marker is registered in
the normal regression runner; its98 fixture tests pass. No preserved editor or
candidate is rebuilt by these checks.
