# Stable release identity mechanism (inactive)

The current source defaults to beta6/0.1.0-beta.54. Stable support is opt-in and
has not activated a release, rebuilt a public package, or published to GitHub.

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
stable client only for an authenticated opt-in stable App identity.

Local package activation, after functional acceptance and matching public docs:

```sh
SPYRO_EDITOR_RELEASE_CHANNEL=stable \
SPYRO_EDITOR_STABLE_VERSION=1.0 \
SPYRO_EDITOR_STABLE_INTERNAL_VERSION=1.0.0 \
DIST_DIR=/absolute/new/output \
tools/Build-SpyroEditorRelease.sh SpyroEditor-V1.0
```

Use the same three environment variables for package verification. Existing Mac
signing/notarization policy is unchanged. No upload or GitHub release step exists
in this mechanism. The changelog must name the stable release and its delta from
the current beta bridge; default beta package identity/preflight stays intact.

Verification in an isolated source mirror: Release+Debug UpdateSmoke passed31
new offline stable checks plus the existing whole/incremental beta suite.
An actual opt-in App build was independently read by ReleaseIdentityTool as
stable1.0/schema3/internal1.0.0. `Test-StableReleaseChannel.py` passes4 source-free
shell-selection tests, including invalid/missing identity refusal. Build/Verify
shell syntax checks pass. These are mechanism/build proofs; stable full ZIP,
updater HTTP against a published stable release, and signed GUI are pending.
