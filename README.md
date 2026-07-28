# OpenTok.Net.Android

[![NuGet](https://img.shields.io/nuget/v/OpenTok.Net.Android?label=nuget)](https://www.nuget.org/packages/OpenTok.Net.Android)
[![Targets: net8.0 | net9.0 | net10.0](https://img.shields.io/badge/targets-net8.0%20%7C%20net9.0%20%7C%20net10.0-512BD4)](#packages)
[![opentok-android-sdk 2.34.1](https://img.shields.io/badge/opentok--android--sdk-2.34.1-099DFD)](https://central.sonatype.com/artifact/com.opentok.android/opentok-android-sdk)
[![Licence: MIT](https://img.shields.io/badge/licence-MIT-green.svg)](LICENSE)

.NET for Android and .NET MAUI bindings for the Vonage OpenTok (Video API) Android SDK.

## Packages

Two packages, from `net8.0-android` through `net10.0-android`:

| Package | Native artifact | Use it when |
| --- | --- | --- |
| `OpenTok.Net.Android` | [`com.opentok.android:opentok-android-sdk`](https://central.sonatype.com/artifact/com.opentok.android/opentok-android-sdk) | Always — `Session`, `Publisher`, `Subscriber` and the rest of OpenTok's own API. |
| `OpenTok.Net.webrtc.Dependency.Android` | [`com.vonage:webrtc`](https://central.sonatype.com/artifact/com.vonage/webrtc) | Never referenced directly — a NuGet dependency of the package above, carrying the WebRTC engine OpenTok is built on. |

```bash
dotnet add package OpenTok.Net.Android
```

`OpenTok.Net.webrtc.Dependency.Android` comes along transitively; you never reference it yourself.
It exists because `com.vonage:webrtc`'s `.aar` is 243 MB (four ABIs' worth of the native WebRTC
engine) — carrying it as a separate package keeps `OpenTok.Net.Android` itself a normal-sized
package with a NuGet dependency on this one, instead of ~250 MB every time either changes.

```csharp
using Com.Opentok.Android;

var publisher = new Publisher.Builder(context).Name("me").Build();
publisher.PublishAudio = true;
publisher.PublishVideo = true;
// publisher.View is a ready-made Android.Views.View — attach it into your layout to show the
// local camera preview. See samples/OpenTok.Sample.Android for a full MAUI example.

var session = new Session.Builder(context, apiKey, sessionId).Build();
session.StreamReceived += (s, e) => session.Subscribe(new Subscriber.Builder(context, e.Stream).Build());
session.Connect(token);
```

These are raw platform bindings — the full `class-parse`-generated surface, `Com.Opentok.Android`
and friends, namespaced exactly as the Java package (`com.opentok.android` →
`Com.Opentok.Android`, no rename). See [tokbox.com/developer/sdks/android](https://tokbox.com/developer/sdks/android/)
for the SDK's own API reference.

Two things in that snippet are this package's doing rather than `class-parse`'s, and both exist
because the generator loses information the Java API has:

- **`Build()` returns `Publisher`**, and the chain stays on `Publisher.Builder`. Java's covariant
  `build()` override and every fluent setter on `Publisher.Builder` are dropped along with their
  synthetic bridge methods, leaving only the inherited `PublisherKit`-typed versions — so this
  otherwise reads `new Publisher.Builder(context).Build().JavaCast<Publisher>()`, and the plain
  `(Publisher)` cast an author reaches for first *throws*. See `src/OpenTok.Net.Android/Additions/`.
- **`e.Stream`, not `e.P1`.** The `.aar` is compiled without `javac -parameters`, so every generated
  `EventArgs` would otherwise expose `P0`/`P1`/`P2`. See `Transforms/Metadata.xml`.

If you want one API across both platforms instead of the raw binding, see
[`sbokatuk/OpenTok.Net`](https://github.com/sbokatuk/OpenTok.Net) — a cross-platform
`Session`/`Publisher`/`Subscriber` and a MAUI video view over this package and its iOS counterpart.

---

## Requirements

- **.NET 9 SDK** with the `android` workload — `global.json` pins `9.0.100` (`rollForward:
  latestFeature`), which builds `net8.0-android34.0` and `net9.0-android35.0`.
- **.NET 10 SDK** with the `android` workload (`maui-android` too, for the sample) — needed only
  for the `net10.0-android36.0` leg and the sample app; not selected by this repository's
  `global.json`, so it always runs from a scratch `global.json` in a temporary directory (see
  `build/BuildNugets.sh` and the Sample section below for why: the SDK — and so the workload set —
  is resolved from the working directory).
- **JDK 17 or newer** on `$JAVA_HOME` before building anything that isn't just the binding
  libraries themselves — see "The JDK 17 requirement" below.

## Versioning

Each package's version is `<native artifact version>.<binding revision>` — `OpenTokVersion`/
`OpenTokBindingRevision` and `WebrtcVersion`/`WebrtcBindingRevision` in `Directory.Build.props`.
The two packages sit on independent native version lines (OpenTok's own SDK releases vs. Vonage's
WebRTC engine releases), so there is no single version stamped across both — `build/pins.sh`
resolves each from `Directory.Build.props` for shell scripts, and `build/BuildNugets.sh --suffix
...` appends a prerelease suffix to both when needed. The package set itself lives in
`build/packages.tsv`, in dependency order (`webrtc` before `opentok`) — adding a package means
adding a row there and a project under `src/`, nothing else keeps a second copy of the list.
`NuGet.config`'s `local-artifacts` source points at `artifacts/`, so the package tests, the device
tests and the sample all restore the packed `.nupkg` rather than the projects — they exercise
exactly what gets published.

## How this repository works

Nothing native is committed to this repository: `AndroidMavenLibrary` resolves both `.aar` files
straight from Maven Central for `net9.0-android35.0`/`net10.0-android36.0`, cached under
`~/.cache/dotnet-android/MavenCacheDirectory`. `net8.0-android34.0` uses a different path — that
SDK pack has no `AndroidMavenLibrary` support at all, so `src/OpenTok.Binding.props` downloads the
same `.aar`s directly with an MSBuild `DownloadFile` target instead. See the comments there for why
this matters: the unsupported item is silently ignored rather than erroring, so a build using it
on net8 "succeeds" with an empty few-KB binding assembly and no error anywhere in the log.

No single .NET SDK builds net8, net9 *and* net10 for Android, so `build/BuildNugets.sh` packs twice
(the installed SDK's band, then a `net10` pass from a scratch `global.json`) and merges the results
— see `build/merge-packages.py`.

### The webrtc split, and why it is a NuGet dependency rather than a project reference

`OpenTok.Net.Android` binds `opentok-android-sdk` directly. Its public API (custom video
renderers, connection stats, texture-backed frames) hands back a handful of `com.vonage.webrtc`
types, so those need real C# bindings too — provided by `OpenTok.Net.webrtc.Dependency.Android`,
referenced as a **packed** NuGet dependency, not a bare project reference. That distinction matters:
.NET for Android's Java dependency verification (`XA4241`/`XA4242`) reads a referenced NuGet
package's own metadata to know what it provides, but does not look inside a same-solution project
reference's own Maven declarations — so with a plain `ProjectReference` the build fails, reporting
`com.vonage:webrtc` and its own transitive dependencies as unsatisfied even though the referenced
project binds them. Packing the dependency first and referencing the `.nupkg` (see
`src/OpenTok.Net.Android/OpenTok.Net.Android.csproj`'s own comment) is what makes verification work
— and is also what keeps the 243 MB `.aar` out of `OpenTok.Net.Android`'s own package.

Several of `com.vonage:webrtc`'s own transitive Java dependencies (Guava, in particular) are
extremely common Android dependencies in their own right — a plain MAUI app's dependency graph
already pulls in some of them on its own. Where that collision was observed (Guava,
`error_prone_annotations`), `src/OpenTok.Net.webrtc.Dependency.Android/OpenTok.Net.webrtc.Dependency.Android.csproj`
resolves them through the same canonical `Xamarin.Google.*` NuGet packages the rest of the
ecosystem uses, so NuGet's normal single-version resolution deduplicates them instead of two
copies colliding at `dex` time (`R8: Type ... is defined multiple times`). See that file's own
comments for the specifics.

### The JDK 17 requirement

`opentok-android-sdk`'s own `classes.jar` is compiled to Java 17 class file format. Building
*against* it — anything that compiles generated Java glue for a bound listener interface, which is
any real app, not the binding libraries themselves — needs a JDK new enough to read that class
file. `net9.0-android35.0`/`net10.0-android36.0` pick up whatever JDK the environment's `$JAVA_HOME`
already points to; `net8.0-android34.0`'s tooling does not, by default, which is why
`tests/OpenTok.Net.Android.DeviceTests` and `samples/OpenTok.Sample.Android` both fall back to
`$JAVA_HOME` explicitly for that one target framework (see either project's own comment). Make sure
`$JAVA_HOME` (or an explicit `-p:JavaSdkDirectory=...`) points at JDK 17 or newer before building an
app — not just packing the libraries — against this SDK version.

## Building locally

```sh
./build/BuildNugets.sh
dotnet test tests/OpenTok.Net.Android.PackageTests
```

`OpenTok.Net.Android` restores `OpenTok.Net.webrtc.Dependency.Android` from the `local-artifacts`
NuGet source (`artifacts/`, see `NuGet.config`) — building it directly, on its own, only works once
that dependency has already been packed there. `./build/BuildNugets.sh` packs both, in the right
order (see "Versioning" above); after that, iterating on a single project directly is fine, e.g.:

```sh
dotnet build src/OpenTok.Net.Android/OpenTok.Net.Android.csproj -f net9.0-android35.0
```

## Tests

Everything runs against the packed `.nupkg` in `artifacts/`, not the build output, because the
failure modes worth catching here are packaging ones — most importantly the net8 "empty shell"
trap above, which builds with 0 errors and 0 warnings.

- **`tests/OpenTok.Net.Android.PackageTests`** (plain xUnit, runs anywhere) asserts the package
  layout — a real binding assembly and the native `.aar`s for every target framework — and, through
  the metadata reader, the API itself: the core `Com.Opentok.Android` types exist,
  `OpenTok.Net.Android` depends on `OpenTok.Net.webrtc.Dependency.Android` rather than embedding
  its `.aar`, and `SubscriberKit`'s two `onDisconnected` overloads (see
  `src/OpenTok.Net.Android/Transforms/Metadata.xml`) still carry the distinct names that keep them
  from colliding. A binding that failed to generate still packs cleanly; these are what notice.
- **`tests/OpenTok.Net.Android.DeviceTests`** is a bare Android app (no MAUI, no test framework)
  that consumes the packed package and drives the raw binding on an emulator: resolve the Java
  entry points out of the packaged `.aar`s, construct a `Session` and a `Publisher` (no credentials
  — nothing here connects a session or joins anything), toggle publish audio/video, tear down. It
  reports a single `OPENTOK_E2E_DONE PASS`/`FAIL` line to logcat, which
  `.github/scripts/run-emulator-tests.sh` turns into an exit code.

Run the emulator suite locally, with an emulator already booted:

```sh
./build/BuildNugets.sh
OPENTOK_DEVICE_RID=android-arm64 ./.github/scripts/run-emulator-tests.sh 2.34.1.1 net9.0-android35.0
```

## CI

`.github/workflows/build.yml` is a reusable workflow — `pack` builds both packages, `validate` runs
the package tests, `sample` builds the MAUI sample against them, and `e2e` runs the emulator suite
on `net8.0-android34.0` and `net10.0-android36.0` (the two extremes: net8's `.aar`s arrive through
the `DownloadFile` fallback, and net10's assets are grafted in by the merge step, so those are the
two that could each break alone). `.github/workflows/pr.yml` calls it on every pull request, then
publishes a beta build of both packages to nuget.org (skipped for forked PRs, which get no OIDC
token for this repository). `.github/workflows/release.yml` does the same on a `v*` tag push, then
creates a GitHub release. Both publish jobs use nuget.org trusted publishing (OIDC, no long-lived
API key) against the `nuget.org` environment.

## Sample

`samples/OpenTok.Sample.Android` is a MAUI app built straight against the packages — no
cross-platform façade — that connects to a session, publishes this device's camera and microphone,
and subscribes to the first remote stream: API key / session id / token entry, Connect / Disconnect
/ Publish, local and remote video, and a status log. It is deliberately the same flow and the same
layout as `samples/OpenTok.Sample.iOS` in the iOS binding repository, so the two can be read side by
side and the only differences are the ones the SDKs actually force.

Running it needs a Vonage Video API key, session id and token. Everything up to Connect works
without them.

It also carries a small custom MAUI handler hosting whatever native view OpenTok hands back (see its
`OpenTokVideoView.cs` for why a container rather than a wrapped `SurfaceView` — unlike Agora's SDK,
OpenTok hands a ready-made `View` *out* of `Publisher`/`Subscriber` rather than wanting one supplied
in). That handler is exactly what [`OpenTok.Net.Maui`](https://github.com/sbokatuk/OpenTok.Net)
packages, if you would rather not write it.

It consumes the packed `OpenTok.Net.Android` package from `./artifacts` (see `NuGet.config`), so
pack first. It targets `net10.0-android36.0` and needs the **.NET 10 SDK** with the `maui-android`
workload, which this repository's `global.json` does not select — hence the scratch directory:

```sh
./build/BuildNugets.sh
cd /tmp && dotnet new globaljson --sdk-version 10.0.100 --force
dotnet build <repo>/samples/OpenTok.Sample.Android/OpenTok.Sample.Android.csproj
```

`OpenTok.Net.Android.sln` deliberately contains the binding projects and the tests but **not** the
sample, so `dotnet build OpenTok.Net.Android.sln` does not require the MAUI workload.

## Licence

MIT — see [LICENSE](LICENSE). Vonage's own SDK is distributed under its own SDK licence terms.
