#!/usr/bin/env bash
# Sourced after validating the selected version and legacy compatibility metadata.
RELEASE_CHANNEL="${SPYRO_EDITOR_RELEASE_CHANNEL:-${RELEASE_CHANNEL:-beta}}"
RELEASE_PUBLISH_PROPERTIES=()
RELEASE_IDENTITY_VERSION="$PUBLIC_RELEASE_VERSION"
if [[ "$RELEASE_CHANNEL" == stable ]]; then
    STABLE_PUBLIC_VERSION="${SPYRO_EDITOR_STABLE_VERSION:-${SOURCE_STABLE_VERSION:-}}"
    STABLE_INTERNAL_VERSION="${SPYRO_EDITOR_STABLE_INTERNAL_VERSION:-${SOURCE_STABLE_INTERNAL_VERSION:-}}"
    python3 - "$STABLE_PUBLIC_VERSION" "$STABLE_INTERNAL_VERSION" <<'PY'
import re,sys
public,internal=sys.argv[1:]
if not re.fullmatch(r'[1-9][0-9]*\.(0|[1-9][0-9]*)(\.[1-9][0-9]*)?',public):
    raise SystemExit('Stable version must be explicit and canonical, e.g.1.0 or1.0.1.')
expected=public+'.0' if public.count('.')==1 else public
if internal!=expected:raise SystemExit('Stable internal version must be the matching three-part version, without beta/prerelease identifiers.')
PY
    PUBLIC_RELEASE_VERSION="$STABLE_PUBLIC_VERSION"
    PROJECT_VERSION="$STABLE_INTERNAL_VERSION"
    BASE_VERSION="$STABLE_INTERNAL_VERSION"
    BUILD_NUMBER=1
    RELEASE_MANIFEST_SCHEMA_VERSION=3
    PUBLIC_RELEASE_NAME="Spyro Editor $STABLE_PUBLIC_VERSION"
    PUBLIC_RELEASE_SLUG="SpyroEditor-V$STABLE_PUBLIC_VERSION"
    EXPECTED_RELEASE_NAME="$PUBLIC_RELEASE_SLUG"
    PREVIOUS_PUBLIC_RELEASE_NAME="Spyro Editor Beta V$BETA_RELEASE_NUMBER"
    MAC_BUNDLE_SHORT_VERSION="$STABLE_INTERNAL_VERSION"
    RELEASE_IDENTITY_VERSION="stable:$STABLE_PUBLIC_VERSION"
    RELEASE_PUBLISH_PROPERTIES=("-p:ReleaseChannel=stable" "-p:StableReleaseVersion=$STABLE_PUBLIC_VERSION" "-p:Version=$STABLE_INTERNAL_VERSION" "-p:InformationalVersion=$STABLE_INTERNAL_VERSION")
elif [[ "$RELEASE_CHANNEL" == beta ]]; then
    RELEASE_PUBLISH_PROPERTIES=("-p:ReleaseChannel=beta" "-p:Version=$PROJECT_VERSION" "-p:InformationalVersion=$PROJECT_VERSION")
else
    echo 'SPYRO_EDITOR_RELEASE_CHANNEL must be beta or stable.' >&2
    exit 1
fi
