#!/usr/bin/env bash
# Shared build/verifier preflight. Select channels before parsing internal versions.
SOURCE_RELEASE_VALUES="$(python3 - "$1" <<'PY'
import sys,xml.etree.ElementTree as ET
root=ET.parse(sys.argv[1]).getroot()
def first(name):
    for group in root.findall('PropertyGroup'):
        node=group.find(name)
        if node is not None and node.text and '$(' not in node.text:return node.text
    return ''
print('\n'.join(first(n) for n in ('ReleaseChannel','StableReleaseVersion','Version','LegacyBetaInternalVersion')))
PY
)"
SOURCE_RELEASE_CHANNEL="$(printf '%s\n' "$SOURCE_RELEASE_VALUES" | sed -n '1p')"
SOURCE_STABLE_VERSION="$(printf '%s\n' "$SOURCE_RELEASE_VALUES" | sed -n '2p')"
SOURCE_STABLE_INTERNAL_VERSION="$(printf '%s\n' "$SOURCE_RELEASE_VALUES" | sed -n '3p')"
SOURCE_BETA_INTERNAL_VERSION="$(printf '%s\n' "$SOURCE_RELEASE_VALUES" | sed -n '4p')"
RELEASE_CHANNEL="${SPYRO_EDITOR_RELEASE_CHANNEL:-${SOURCE_RELEASE_CHANNEL:-beta}}"
if [[ "$RELEASE_CHANNEL" == stable ]]; then
    PROJECT_VERSION="${SPYRO_EDITOR_STABLE_INTERNAL_VERSION:-$SOURCE_STABLE_INTERNAL_VERSION}"
    BASE_VERSION="$PROJECT_VERSION"
    BUILD_NUMBER=1
elif [[ "$RELEASE_CHANNEL" == beta ]]; then
    PROJECT_VERSION="${SPYRO_EDITOR_BETA_INTERNAL_VERSION:-${SOURCE_BETA_INTERNAL_VERSION:-$SOURCE_STABLE_INTERNAL_VERSION}}"
    if [[ ! "$PROJECT_VERSION" =~ ^([0-9]+\.[0-9]+\.[0-9]+)-beta\.([0-9]+)$ ]]; then
        echo "Unsupported beta app version '$PROJECT_VERSION'; expected X.Y.Z-beta.N." >&2
        exit 1
    fi
    BASE_VERSION="${BASH_REMATCH[1]}"
    BUILD_NUMBER="${BASH_REMATCH[2]}"
else
    echo 'SPYRO_EDITOR_RELEASE_CHANNEL must be beta or stable.' >&2
    exit 1
fi
