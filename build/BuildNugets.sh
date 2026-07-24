#!/bin/sh

set -e

# Builds and packs every package listed in build/packages.tsv: the OpenTok.Net.Android binding and
# its OpenTok.Net.webrtc.Dependency.Android dependency.
#
# Usage:
#   ./build/BuildNugets.sh                       # each package at its own version (see below)
#   ./build/BuildNugets.sh --suffix beta.12.34   # same, with a prerelease suffix appended
#
# Each package packs at its own <VersionPrefix> from Directory.Build.props: OpenTok and its webrtc
# dependency sit on independent native version lines, so no single version can be stamped across
# the set — which is why there is no way to pass one. Releases publish whatever versions the pins
# say, and nuget.org's --skip-duplicate makes republishing an unchanged version a no-op.
#
# Nothing needs fetching first: the .aar files are resolved from Maven Central by
# AndroidMavenLibrary (net9+) or downloaded directly by the net8.0-android34.0 fallback target —
# see src/OpenTok.Binding.props — and cached under ~/.cache/dotnet-android/MavenCacheDirectory / obj/.
#
# Packages are written to ../artifacts.
#
# Each .NET SDK's Android workload ships reference packs for only two target frameworks - the .NET
# 9 band covers net8/net9, the .NET 10 band covers net9/net10 - so this runs two passes and merges
# them. The repository's global.json pins the .NET 9 SDK, so the second pass is invoked from a
# scratch directory carrying its own global.json, since the SDK is resolved from the working
# directory.

cd "$(dirname "$0")"

SUFFIX=""
case "${1:-}" in
    "") ;;
    --suffix)
        SUFFIX="${2:?--suffix needs a value}"
        ;;
    *)
        echo "error: unknown argument '$1' (a single version cannot be stamped across" >&2
        echo "       independent version lines — use --suffix for prereleases)" >&2
        exit 2
        ;;
esac

ROOT="$(cd .. && pwd)"
OUTPUT="$ROOT/artifacts"

PASS1_BAND="net9"
PASS2_BAND="net10"
PASS2_SDK="10.0.100"

# Column 2 (project) doubles as the project directory name — see packages.tsv's own header comment
# for why, unlike Net.Agora.Android, no bare id is expanded into a shared naming template here.
PROJECTS=$(grep -v '^#' packages.tsv | grep -v '^[[:space:]]*$' | cut -f2)

if [ -z "$PROJECTS" ]; then
    echo "error: no packages found in build/packages.tsv" >&2
    exit 1
fi

VERSION_ARG=""
if [ -n "$SUFFIX" ]; then
    case "$SUFFIX" in
        *[!A-Za-z0-9.-]*)
            echo "error: invalid suffix '$SUFFIX'" >&2
            exit 1
            ;;
    esac
    VERSION_ARG="-p:VersionSuffix=$SUFFIX"
fi

mkdir -p "$OUTPUT"

PASS1_DIR="$OUTPUT/.net9-pass"
PASS2_DIR="$OUTPUT/.net10-pass"

SDK10_DIR="$(mktemp -d)"
trap 'rm -rf "$SDK10_DIR" "$PASS1_DIR" "$PASS2_DIR"' EXIT
cat > "$SDK10_DIR/global.json" <<EOF
{ "sdk": { "version": "$PASS2_SDK", "rollForward": "latestFeature" } }
EOF

# Packed and merged one package at a time, in packages.tsv's dependency order, rather than both
# bands of every package followed by one merge pass at the end. OpenTok.Net.Android depends on
# OpenTok.Net.webrtc.Dependency.Android (see build/packages.tsv), and its own cross-targeting
# restore — even just for the net9 band pass, which targets net8.0-android34.0 *and*
# net9.0-android35.0 at once — needs a webrtc package that already carries both target frameworks.
# A single merge pass at the very end cannot provide that yet when OpenTok.Net.Android's own pack
# step runs; merging per package, immediately after its own two passes, can.
for name in $PROJECTS; do
    project="$ROOT/src/$name/$name.csproj"

    if [ ! -f "$project" ]; then
        echo "error: $project does not exist, but build/packages.tsv lists $name" >&2
        exit 1
    fi

    rm -rf "$PASS1_DIR" "$PASS2_DIR"

    echo "==> packing $name ($PASS1_BAND band)"
    dotnet pack "$project" \
        -c Release \
        -p:OpenTokSdkBand="$PASS1_BAND" \
        $VERSION_ARG \
        -o "$PASS1_DIR"

    echo "==> packing $name ($PASS2_BAND band)"
    (cd "$SDK10_DIR" && dotnet pack "$project" \
        -c Release \
        -p:OpenTokSdkBand="$PASS2_BAND" \
        $VERSION_ARG \
        -o "$PASS2_DIR")

    echo "==> merging target frameworks for $name"
    python3 "$ROOT/build/merge-packages.py" "$PASS1_DIR" "$PASS2_DIR" "$OUTPUT"
done

rm -rf "$PASS1_DIR" "$PASS2_DIR"
