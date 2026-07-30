# OpenTok.Net.Android — repository instructions

## Overview

.NET for Android / .NET MAUI bindings for the Vonage OpenTok (Video API) Android SDK. Three
packages, listed in dependency order in `build/packages.tsv`:

- `OpenTok.Net.webrtc.Dependency.Android` — binds `com.vonage:webrtc` (the 243 MB WebRTC engine),
  on its own version line.
- `OpenTok.Net.Android` — binds `com.opentok.android:opentok-android-sdk`; the full `class-parse`
  surface under `Com.Opentok.Android`.
- `OpenTok.Net.Transformers.Android` — native payload only, no managed API
  (`com.vonage:client-sdk-video-transformers` plus the ML libraries); see `src/OpenTok.Transformers.md`.

Two independent native version lines (OpenTok's SDK, Vonage's WebRTC engine), each
`<native>.<binding revision>`. Generated code is repaired by hand in `Additions/` and
`Transforms/Metadata.xml`. `sbokatuk/OpenTok.Net` pins `OpenTok.Net.Android` exactly, so a
published version is immediately downstream.

## Build & verify

```sh
./build/BuildNugets.sh                       # packs all three into artifacts/, packages.tsv order
dotnet test tests/OpenTok.Net.Android.PackageTests
```

- Needs the .NET 9 **and** .NET 10 SDKs with the `android` workload (`maui-android` too, for the
  sample). `global.json` pins 9.0.100; the net10 pass runs from a scratch `global.json` because the
  SDK — and so the workload set — is resolved from the working directory.
- JDK 17+ on `$JAVA_HOME` before building any *app* (device tests, sample) against the SDK.
- `./build/BuildNugets.sh --suffix beta.<pr>.<run>` for prereleases; `. build/pins.sh` exports
  `OPENTOK_PACKAGE_VERSION` and `WEBRTC_PACKAGE_VERSION`.
- Iterating on one project alone (`dotnet build src/OpenTok.Net.Android/... -f net9.0-android35.0`)
  only works once the webrtc package exists in `artifacts/` — it is restored from the
  `local-artifacts` feed in `NuGet.config`.
- Emulator suite, with an emulator booted:
  `OPENTOK_DEVICE_RID=android-arm64 ./.github/scripts/run-emulator-tests.sh 2.34.1.4 net9.0-android35.0`.
- Target frameworks are band-split in `src/OpenTok.Binding.props`: `net9` band =
  `net8.0-android34.0;net9.0-android35.0`, `net10` band = `net10.0-android36.0`;
  `SupportedOSPlatformVersion` is 24 (the SDK's own minSdkVersion).

## Layout

- `src/OpenTok.Net.Android`, `src/OpenTok.Net.webrtc.Dependency.Android`,
  `src/OpenTok.Net.Transformers.Android`; shared `src/OpenTok.Binding.props`;
  `src/OpenTok.Transformers.md`.
- `build/` — `BuildNugets.sh`, `pins.sh`, `packages.tsv`, `upstream.tsv`, `merge-packages.py`,
  `check-upstream.sh`.
- `tests/OpenTok.Net.Android.PackageTests`, `tests/OpenTok.Net.Android.DeviceTests`,
  `samples/OpenTok.Sample.Android`, `docs/release-notes/`.
- `OpenTok.Net.Android.sln` holds the two binding projects and both test projects only — keep the
  sample and the transformers project out of it, so the solution builds without the MAUI workload.

## Conventions

- Versions live **only** in `Directory.Build.props` (`OpenTokVersion`/`OpenTokBindingRevision`,
  `WebrtcVersion`/`WebrtcBindingRevision`, and the derived `FloggerVersion`, `GuavaVersion`,
  `MlTransformersVersion`, `MlTransformersNoiseSuppressionVersion`). Shell callers read them via
  `build/pins.sh`; nothing else parses the props.
- `build/packages.tsv` is the only package list, in dependency order (webrtc before opentok).
  Adding a package = one row plus a project under `src/`.
- Keep the Java-visible surface as generated: `com.opentok.android` → `Com.Opentok.Android`, no
  namespace or type renames.
- Repair generator output in `src/OpenTok.Net.Android/Additions/` or `Transforms/Metadata.xml` only
  — never by editing generated code, and never by adding a new warning suppression without the same
  kind of justification the existing `NoWarn` comment carries.
- British spelling in prose ("licence", "behaviour"), matching the README.
- The long comments in `src/OpenTok.Net.Android/OpenTok.Net.Android.csproj`,
  `src/OpenTok.Binding.props` and the transformers `.csproj` record failures that already happened.
  Read them before changing references, target frameworks or Maven items, and keep them current.

## CI & release flow

- `.github/workflows/build.yml` is reusable (`verify` input; jobs `pack`, `validate`, `sample`,
  `transformers-packaging`, `e2e`). `e2e` runs `net8.0-android34.0` and `net10.0-android36.0` — the
  two paths that can break alone — on ubuntu for KVM. Both must stay green.
- `pr.yml` → beta `-beta.<pr>.<run>` published to nuget.org; forked PRs build but never publish.
- Release: add `docs/release-notes/<version>.md`; merging it is the release — `auto-release.yml`
  tags the merge and dispatches `release.yml`, whose `guard` job proves the tag is on the default
  branch before anything is published, and whose `version` job requires the tag to match the props.
- Publishing is nuget.org trusted publishing (OIDC, environment `nuget.org`, only
  `secrets.NUGET_USER`).
- `upstream-drift.yml` + `build/upstream.tsv` watch the SDK, webrtc, Flogger, Guava and the ML
  transformer pins daily and file one issue per group.

## Testing

- `tests/OpenTok.Net.Android.PackageTests` (plain xUnit, runs anywhere) reads the packed `.nupkg`
  from `artifacts/`, not build output: per-target-framework layout, a binding assembly big enough to
  be real (the net8 empty-shell trap), the native `.aar`s, `OpenTok.Net.Android` depending on the
  webrtc package rather than embedding it, and the Metadata renames. Extend these when you change
  packaging or the public surface.
- `tests/OpenTok.Net.Android.DeviceTests` is a bare Android app (no MAUI, no test framework) that
  consumes the packed package, constructs `Session`/`Publisher` with **no credentials**, and reports
  one `OPENTOK_E2E_DONE PASS`/`FAIL` logcat line. Keep it credential-free and keep its Debug-only,
  no-R8/no-AOT settings.

## Hard rules

- Never turn the webrtc dependency into a `ProjectReference`, and never pin it with a bracketed
  exact version inside this repository's csproj. The packed-package reference with a **bare**
  version is what makes `_VerifyJavaDependencies` (XA4241/XA4242) pass and what lets `-beta.*`
  local packs resolve. Consumers outside the repository (device tests, sample) do use brackets.
- Never embed `com.vonage:webrtc`'s `.aar` into `OpenTok.Net.Android` — the split is the point, and
  a package test asserts it.
- Never commit `.aar`s or any native artifact. Keep the `DownloadFile` fallback in
  `src/OpenTok.Binding.props` for `net8.0-android34.0`, and never rely on `AndroidMavenLibrary`
  there: the item is silently ignored and the build "succeeds" with an empty binding.
- Keep the `-ps16k` ML artifacts and the derived pins (Flogger, Guava, ML versions) equal to what
  the bound POMs declare. A stale `@(AndroidIgnoredJavaDependency)` entry stops matching silently
  and resurfaces XA4241 on the next bump.
- Do not rename Java-derived namespaces or types, and do not strip the Metadata renames (the two
  `onDisconnected` overloads, the EventArgs parameter names) or the covariant `Build()`.
- Change versions only in `Directory.Build.props`; never publish a version that would not sort above
  nuget.org's existing history for the package.
- Never bypass the release `guard` job, and never publish outside OIDC trusted publishing.

## References

- SDK reference: [tokbox.com/developer/sdks/android](https://tokbox.com/developer/sdks/android/);
  [release notes](https://developer.vonage.com/en/video/client-sdk/android/release-notes).
- Maven Central: `com.opentok.android:opentok-android-sdk`, `com.vonage:webrtc`,
  `com.vonage:client-sdk-video-transformers`.
- Siblings: [`OpenTok.Net`](https://github.com/sbokatuk/OpenTok.Net) (umbrella),
  [`OpenTok.Net.iOS`](https://github.com/sbokatuk/OpenTok.Net.iOS),
  [`OpenTok.Net.Win`](https://github.com/sbokatuk/OpenTok.Net.Win),
  [`Net.Agora.Android`](https://github.com/sbokatuk/Net.Agora.Android) (the pattern this repository
  follows).
- Native `.aar`s cache under `~/.cache/dotnet-android/MavenCacheDirectory`; CI restores it keyed on
  `Directory.Build.props`.

Trust these instructions. Search the codebase only when something here is incomplete or turns out
to be wrong.
