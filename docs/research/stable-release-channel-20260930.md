# Stable 1.0 source identity and separate update channel

The source defaults to stable1.0/internal1.0.0. This activates the desktop
identity and packaging preflight; it does not publish a release or prove a
signed package, native gameplay, or a live updater server. Explicit beta builds
retain beta6/internal0.1.0-beta.54 and schema1 compatibility.

Stable public1.0 is a separate channel from beta6. It is never compared against
the beta integer. Stable tags are `v1.0`, names `Spyro Editor 1.0`, ZIP stems
`SpyroEditor-V1.0-{rid}`. Canonical stable versions have two components or a
nonzero third patch component. Stable builds use matching three-part internal
versions without beta/prerelease identifiers.

The stable assembly carries explicit `SpyroEditorReleaseChannel=stable`,
`SpyroEditorStableReleaseVersion` and `SpyroEditorStableManifestSchema=3`.
Legacy beta metadata remains available for project compatibility; the beta
assembly reader rejects an explicit foreign channel. Stable package manifests
use schema3/channel stable/publicVersion and exclude publicBeta. Stable discovery
admits only non-prerelease stable names/tags/assets. Download verifies repository
URL, exact version/channel/platform, bounded package bytes and ZIP entries,
SHA256, changelog, manifest and actual PE metadata. It retains the beta updater's
archive security checks in an independent stable client so schema1/2 behavior
continues unchanged. Future security fixes must be applied to both clients.

Stable notification/download state is `update-check-stable.json` and never
migrates or rewrites `update-check.json`. App check/download handlers select the
stable client only for an authenticated stable App identity.

Local package build, after final functional acceptance and matching public docs:

```sh
SPYRO_EDITOR_MAC_BUILD_MODE=signed-only \
DIST_DIR=/absolute/new/output \
tools/Build-SpyroEditorRelease.sh SpyroEditor-V1.0
```

The source selects stable1.0 without channel environment variables. Overrides
are validated against matching canonical public/internal identities. Existing
Mac signing/notarization policy is unchanged; signed-only is explicitly not
notarized. No upload or GitHub release step exists in this mechanism. The changelog must name the stable release and its delta from
the current beta bridge; explicit beta package identity/preflight stays intact.
To build that channel,
set `SPYRO_EDITOR_RELEASE_CHANNEL=beta`; publishing properties explicitly select
beta and its preserved legacy internal version.

Verification in an isolated source mirror: Release+Debug UpdateSmoke passed32
new offline stable checks plus the existing whole/incremental beta suite.
The default App build was independently read by ReleaseIdentityTool as
stable1.0/schema3/internal1.0.0; an explicit beta build remains beta6/internal
0.1.0-beta.54. Each channel reader rejects the other actual App DLL. Legacy
updater tests use a separately built beta fixture rather than relabeling the
active stable app. `Test-StableReleaseChannel.py` passes5 source-free
shell-selection tests, including invalid/missing identity refusal. Build/Verify
shell syntax checks pass. These are identity/build proofs; stable full ZIP,
updater HTTP against a published stable release, and signed GUI are pending.
