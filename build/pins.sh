# The only parser of Directory.Build.props for shell callers. Source this, don't execute it.
#
#   . build/pins.sh
#   echo "$OPENTOK_PACKAGE_VERSION"

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export OPENTOK_REPO_ROOT
OPENTOK_REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

export OPENTOK_VERSION
OPENTOK_VERSION="$(grep -oE '<OpenTokVersion>[^<]+' "${OPENTOK_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<OpenTokVersion>//')"
export OPENTOK_BINDING_REVISION
OPENTOK_BINDING_REVISION="$(grep -oE '<OpenTokBindingRevision>[^<]+' "${OPENTOK_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<OpenTokBindingRevision>//')"
export OPENTOK_PACKAGE_VERSION="${OPENTOK_VERSION}.${OPENTOK_BINDING_REVISION}"

export WEBRTC_VERSION
WEBRTC_VERSION="$(grep -oE '<WebrtcVersion>[^<]+' "${OPENTOK_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<WebrtcVersion>//')"
export WEBRTC_BINDING_REVISION
WEBRTC_BINDING_REVISION="$(grep -oE '<WebrtcBindingRevision>[^<]+' "${OPENTOK_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<WebrtcBindingRevision>//')"
export WEBRTC_PACKAGE_VERSION="${WEBRTC_VERSION}.${WEBRTC_BINDING_REVISION}"
