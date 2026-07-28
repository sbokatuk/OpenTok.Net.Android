# The transformers package

`OpenTok.Net.Transformers.Android` is not a binding. It carries native payload and nothing else.

## Why it exists at all

Vonage ships the media transformers — background blur, background replacement, audio noise
suppression — as a separate library from the Video SDK, on both platforms. On Android that is
`com.vonage:client-sdk-video-transformers`, an opt-in Gradle dependency since SDK 2.27.2.

The classes that use it are already in `OpenTok.Net.Android`: `PublisherKit.VideoTransformer`,
`PublisherKit.AudioTransformer`, `SetVideoTransformers` and `SetAudioTransformers`. The SDK loads
the native transformers when one of those is constructed.

Without this package that construction **compiles, links and then fails at runtime**: the
transformer comes back null and the SDK raises

```
0x0A000006 — MediaTransformerOpenTokTransformersLibraryNotLoaded
```

a value that has been sitting in this binding's own `OpentokError.ErrorCode` enum for as long as it
has bound those classes without anyone shipping the library. It is neither a compile error nor a
link error, which is why it went unnoticed.

## What the package contains

`client-sdk-video-transformers`' POM declares three Maven artifacts. Two are shipped:

| Artifact | Shipped | Carries |
| --- | --- | --- |
| `com.vonage:client-sdk-video-transformers` | yes | `libopentok_transformers.so`, `libmltransformers.so` and `libmltransformersaudionoisesuppression.so` for three ABIs, plus the three `.tflite` models. **Its `classes.jar` is empty** — 22 bytes. |
| `com.vonage:mltransformers-ps16k` | yes | `libs/libVonageSelfieSegmentation_android_lib.jar` — 159 MediaPipe classes plus `com.vonage.mltransformers.NativeLib`, the loader — and `assets/selfie_segmentation_gpu.binarypb`. |
| `com.vonage:mltransformersaudionoisesuppression-ps16k` | no | an empty `classes.jar` and one `.so` that is already in the main `.aar`, byte for byte. |

The second row is the one worth knowing about. The main `.aar` is self-contained as far as *native*
code goes, so shipping it alone is tempting — but the background transformers reach MediaPipe's Java
components through JNI, and `NativeLib` is what loads the native library in the first place. A
package carrying only the main `.aar` would fail with `NoClassDefFoundError` rather than with
anything that names the real problem.

The third row is dropped deliberately, and "the POM declares it" is normally the end of that
argument. Opened and compared, it holds nothing that is not already shipped: no assets, no
resources, no Java, and a `libmltransformersaudionoisesuppression.so` whose SHA-256 matches the copy
in the main `.aar`. So it contributed 22.6 MB of duplicate to every target framework and nothing
else — tolerable while it was merely wasteful, not once this package reached nuget.org's 250 MB
ceiling. It is listed as an `AndroidIgnoredJavaDependency` rather than left out silently, because
Java dependency verification reads the POM and would otherwise fail the build with XA4241 naming an
artifact that is, in substance, present.

`mltransformers-ps16k` keeps its own duplicate `.so` files for the opposite reason: unlike the audio
one it *also* carries things nothing else has, and stripping them out would mean unpacking and
repacking an `.aar` at build time — a real risk to background blur, which cannot be tested without a
device, to save space the package no longer needs to save.

Both shipped artifacts are `Bind="false"`. Nothing in OpenTok's public API hands back a MediaPipe
type, so they need to be on the classpath rather than projected into C#; binding MediaPipe would
also mean maintaining a metadata file for a surface no consumer of this package calls.

## Target frameworks

`net8.0-android34.0` and `net10.0-android36.0` — two, where every other package here ships three.

Three copies of the payload came to 319 MB and nuget.org refused the push with `HTTP 413`; two come
to 167 MB. `net9` is the one to drop because it is the one nothing needs an exact match for: NuGet
resolves the best *compatible* asset folder, so a net9 app takes the net8 copy and gets everything.
That is verified rather than assumed — CI's `transformers-packaging` job builds an APK at both
net9 (the fallback path) and net10 (its own folder) and checks every `.so` and `.tflite` arrives.

This is safe here in a way it would not be for the binding, because the package has no managed API
at all. There is nothing target-framework-specific in it to get wrong.

One related knob, in the `.csproj`: `AndroidGenerateLibraryAar=false`. The .NET 10 Android SDK folds
the native payload of every `Bind="false"` library into a project-level `.aar`, which for a package
that is *nothing but* payload means a complete second copy — 42 MB, and the largest single entry in
the package. The .NET 9 band does not do this, which is why the net10 half was half again the size
of the net8 half until it was turned off.

## The `-ps16k` suffix

Vonage publishes each ML library twice — plain and `-ps16k` — and `client-sdk-video-transformers`'
POM declares the **`-ps16k`** pair. That is the 16 KB page-size build, which Android 15 requires on
64-bit devices. Verified against the published artifacts: every `.so` in the 64-bit ABIs reports a
maximum `PT_LOAD` alignment of `0x4000`.

Do not "simplify" these to the unsuffixed artifacts. The pins live in `Directory.Build.props` as
`MlTransformersVersion` / `MlTransformersNoiseSuppressionVersion`, and the upstream-drift workflow
watches them against that POM the same way it watches `WebrtcVersion`.

## Duplicate native libraries, deliberately

The main `.aar` and the two ML `.aar`s carry the same `.so` files at the same paths, byte for byte.
That duplication is Vonage's, not this repository's: a Gradle consumer of
`implementation 'com.vonage:client-sdk-video-transformers'` resolves the same three artifacts and
ends up with the same overlap, and the packaging step takes one copy per path. Reproducing the
configuration Vonage actually tests is worth more here than a tidier dependency graph.

## What it costs

About 70 MB. That is the whole reason it is a separate package rather than folded into
`OpenTok.Net.Android` — an app that does not blur backgrounds should not carry a segmentation model
and two noise-suppression models.

## Dependencies, deliberately none

The package does not depend on `OpenTok.Net.Android`, and the binding does not depend on it. A
consumer adds it alongside whichever it already has.

## The iOS side

`OpenTok.Net.Transformers.iOS`, in the sibling repository, is the same idea over the
`VonageClientSDKVideoTransformers` pod, which vendors `OpenTokTransformers.xcframework`. That one is
a *dynamic* framework, so it travels beside the binding assembly rather than being embedded into it
— see that repository's own copy of this document.
