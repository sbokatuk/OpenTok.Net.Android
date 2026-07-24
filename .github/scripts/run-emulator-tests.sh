#!/usr/bin/env bash
set -euo pipefail

# Builds the device test app against a packed OpenTok.Net.Android package, installs it on a
# running Android emulator and runs its checks. The app reports its verdict to logcat under a
# single tag; this script turns that into an exit code.
#
# Assumes an emulator is already booted and visible to adb - in CI that is
# reactivecircus/android-emulator-runner, locally it is whatever you started yourself.
#
# Usage: run-emulator-tests.sh VERSION [TARGET_FRAMEWORK]

VERSION="${1:?a package version is required}"
TARGET_FRAMEWORK="${2:-net10.0-android36.0}"

PACKAGE_NAME="com.sbokatuk.opentok.android.devicetests"
LOG_FILE="emulator-tests.log"
LOG_TAG="OpenTokE2E"
# CI emulators are x86_64. Override for a local arm64 emulator on Apple silicon.
DEVICE_RID="${OPENTOK_DEVICE_RID:-android-x64}"
POLL_ATTEMPTS=90
POLL_INTERVAL=5

REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PROJECT="${REPO_ROOT}/tests/OpenTok.Net.Android.DeviceTests/OpenTok.Net.Android.DeviceTests.csproj"

# The SDK band is chosen by the *Android API level* in the target framework, not by the .NET
# version alone, because that is what decides which workload owns the runtime packs:
#
#   net8.0-android34.0  -> android 34.0.x, in the .NET 8 band
#   net9.0-android35.0  -> android 35.0.x, in the .NET 9 band
#   net10.0-android36.0 -> android 36.0.x, in the .NET 10 band
#
# The .NET 9 band compiles a net8 app happily - it has the API 34 *reference* packs - and then
# fails at packaging time, because it has no API 34 *runtime* packs and they cannot be restored
# from NuGet (NETSDK1112). The runtime packs come from the workload, not from a restore. The SDK
# is resolved from the working directory, and this repository's global.json pins .NET 9, hence
# the scratch directory below.
case "${TARGET_FRAMEWORK}" in
    net10.0-*) sdk_major=10 ;;
    net8.0-*)  sdk_major=8 ;;
    *)         sdk_major=9 ;;
esac

sdk_version="$(dotnet --list-sdks | grep "^${sdk_major}\." | tail -1 | cut -d' ' -f1)"
if [ -z "${sdk_version}" ]; then
    echo "::error::no .NET ${sdk_major} SDK installed, cannot build ${TARGET_FRAMEWORK}"
    exit 1
fi

SDK_DIR="$(mktemp -d)"
trap 'rm -rf "${SDK_DIR}"' EXIT
printf '{ "sdk": { "version": "%s", "rollForward": "latestFeature" } }\n' "${sdk_version}" \
    > "${SDK_DIR}/global.json"

# NuGet caches by package id + version, so rebuilding a version that was already restored once
# silently reuses the stale copy. CI versions are unique, but locally you will re-pack the same
# version repeatedly and test yesterday's bits without this.
rm -rf "${HOME}/.nuget/packages/opentok.net.android/${VERSION}"
rm -rf "${REPO_ROOT}/tests/OpenTok.Net.Android.DeviceTests/obj" \
       "${REPO_ROOT}/tests/OpenTok.Net.Android.DeviceTests/bin"

echo "==> building device tests (version=${VERSION}, tfm=${TARGET_FRAMEWORK}, sdk=${sdk_version})"
# Debug, not Release. Release AOT-compiles every assembly, and an AOT image built against an
# unlinked assembly set disagrees with what the runtime loads - the app aborts on startup before a
# single check runs. Debug also skips the R8 shrinking this app has to avoid anyway - see the
# comments in the .csproj.
#
# net8.0-android34.0's javac needs JDK 17 to read opentok-android-sdk's own class files (see the
# .csproj's own JavaSdkDirectory comment) - passed through explicitly here since $(JAVA_HOME) is
# read at the point the property group evaluates, and the CI job that calls this script is
# expected to have already selected JDK 17 via actions/setup-java.
( cd "${SDK_DIR}" && dotnet build "${PROJECT}" \
    --configuration Debug \
    -p:OpenTokDevicePackageVersion="${VERSION}" \
    -p:OpenTokDeviceTargetFramework="${TARGET_FRAMEWORK}" \
    -p:RuntimeIdentifier="${DEVICE_RID}" \
    -t:Install )

echo "==> granting camera/microphone permissions"
# Dangerous (runtime) permissions on Android 6+: declaring them in AndroidManifest.xml is not
# enough to have them granted. Without this, creating a Publisher reports a different failure
# than the one SmokeTests.cs isolates.
adb shell pm grant "${PACKAGE_NAME}" android.permission.CAMERA
adb shell pm grant "${PACKAGE_NAME}" android.permission.RECORD_AUDIO

echo "==> launching"
adb logcat -c
# The activity name is pinned in the app rather than left to the generated crc64* name, so this
# target stays stable across builds.
adb shell am start -n "${PACKAGE_NAME}/.MainActivity"

echo "==> waiting for the verdict"
for _ in $(seq "${POLL_ATTEMPTS}"); do
    if adb logcat -d -s "${LOG_TAG}:*" | grep -q "OPENTOK_E2E_DONE"; then
        break
    fi
    sleep "${POLL_INTERVAL}"
done

adb logcat -d -s "${LOG_TAG}:*" | tee "${LOG_FILE}"

if ! grep -q "OPENTOK_E2E_DONE PASS" "${LOG_FILE}"; then
    # No verdict usually means the app died before reporting, so keep the crash trace. A missing
    # Java dependency shows up here as a NoClassDefFoundError naming the class.
    echo "==> no passing verdict; capturing crash output"
    adb logcat -d -s AndroidRuntime:E DEBUG:F "${PACKAGE_NAME}:*" 2>/dev/null \
        | tail -100 | tee -a "${LOG_FILE}" || true
    echo "::error::OpenTok emulator smoke tests failed or timed out"
    exit 1
fi

echo "==> emulator smoke tests passed"
