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

Three Maven artifacts, which is one more than it looks like from the outside:

| Artifact | Carries |
| --- | --- |
| `com.vonage:client-sdk-video-transformers` | `libopentok_transformers.so`, `libmltransformers.so` and `libmltransformersaudionoisesuppression.so` for three ABIs, plus the three `.tflite` models. **Its `classes.jar` is empty** — 22 bytes. |
| `com.vonage:mltransformers-ps16k` | `libs/libVonageSelfieSegmentation_android_lib.jar` — 159 MediaPipe classes plus `com.vonage.mltransformers.NativeLib`, the loader — and the segmentation model. |
| `com.vonage:mltransformersaudionoisesuppression-ps16k` | native only; its `classes.jar` is empty too. |

The second row is the one worth knowing about. The main `.aar` is self-contained as far as *native*
code goes, so shipping it alone is tempting — but the background transformers reach MediaPipe's Java
components through JNI, and `NativeLib` is what loads the native library in the first place. A
package carrying only the main `.aar` would fail with `NoClassDefFoundError` rather than with
anything that names the real problem.

All three are `Bind="false"`. Nothing in OpenTok's public API hands back a MediaPipe type, so they
need to be on the classpath rather than projected into C#; binding MediaPipe would also mean
maintaining a metadata file for a surface no consumer of this package calls.

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
