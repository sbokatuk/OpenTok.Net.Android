using System.IO.Compression;

namespace OpenTok.Net.Android.PackageTests;

/// <summary>
/// Asserts what <c>OpenTok.Net.Transformers.Android</c> ships.
/// </summary>
/// <remarks>
/// <para>
/// Held to a different standard than the two binding packages, because it is a different kind of
/// thing: native payload and TensorFlow Lite models, with no managed API at all. Its
/// <c>classes.jar</c> is empty upstream, so the "is not an empty shell" check that guards the two
/// bindings would be exactly backwards here — an empty assembly is the correct outcome.
/// </para>
/// <para>
/// What is worth checking instead is that the payload it exists to carry is actually present. The
/// failure mode this package was created to fix is silent: <c>PublisherKit.VideoTransformer</c>
/// compiles and links without it and returns null at runtime with
/// <c>MediaTransformerOpenTokTransformersLibraryNotLoaded</c>. A package that shipped without its
/// models or without the MediaPipe classes would reproduce that, or something like it, just as
/// quietly. See src/OpenTok.Transformers.md.
/// </para>
/// </remarks>
public class TransformersPackageTests
{
    [Theory]
    [MemberData(nameof(Packages.TransformersFrameworks), MemberType = typeof(Packages))]
    public void Carries_an_assembly_for_its_target_framework(string tfm)
    {
        using var package = Packages.OpenPackage(Packages.Transformers);

        var expected = $"lib/{tfm}/{Packages.Transformers}.dll";
        Assert.True(package.GetEntry(expected) is not null, $"missing '{expected}'.");
    }

    [Fact]
    public void Ships_only_the_two_target_frameworks_that_fit_the_size_limit()
    {
        // The size fix, asserted rather than left to a comment. Three copies of this payload came
        // to 319 MB and nuget.org refused the push with HTTP 413; two come to ~160 MB. Restoring
        // net9 would rebuild that failure, and would do it where nothing else looks — the package
        // still packs and still installs, and only the release push finds out.
        using var package = Packages.OpenPackage(Packages.Transformers);

        var frameworks = package.Entries
            .Where(e => e.FullName.StartsWith("lib/", StringComparison.Ordinal))
            .Select(e => e.FullName.Split('/')[1])
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            Packages.TransformersTargetFrameworks.Order(StringComparer.Ordinal),
            frameworks);
    }

    [Theory]
    [MemberData(nameof(Packages.TransformersFrameworks), MemberType = typeof(Packages))]
    public void Carries_the_transformers_aar(string tfm)
    {
        using var package = Packages.OpenPackage(Packages.Transformers);

        var aar = AarsFor(package, tfm)
            .SingleOrDefault(e => Path.GetFileName(e.FullName)
                .StartsWith("client-sdk-video-transformers-", StringComparison.Ordinal));

        Assert.True(aar is not null, $"no client-sdk-video-transformers .aar under lib/{tfm}/.");

        Assert.True(
            aar!.Length > Packages.MinTransformersAarBytes,
            $"'{aar.FullName}' is only {aar.Length} bytes — the native payload did not ship.");
    }

    [Theory]
    [MemberData(nameof(Packages.TransformersFrameworks), MemberType = typeof(Packages))]
    public void Carries_the_ml_library_that_contributes_something_the_main_aar_lacks(string tfm)
    {
        using var package = Packages.OpenPackage(Packages.Transformers);

        var names = AarsFor(package, tfm).Select(e => Path.GetFileName(e.FullName)).ToList();

        // mltransformers is the reason shipping the main .aar alone is not enough: it carries
        // libs/libVonageSelfieSegmentation_android_lib.jar — the MediaPipe classes and
        // com.vonage.mltransformers.NativeLib — which the background transformers reach through
        // JNI. Without it the failure is NoClassDefFoundError, which names nothing useful.
        Assert.Contains(names, n => n.StartsWith("mltransformers-ps16k-", StringComparison.Ordinal));

        // The POM's other declared ML library is deliberately absent, and that is asserted rather
        // than merely allowed. It holds an empty classes.jar and one .so already present in the
        // main .aar byte for byte, so it was 22.6 MB of duplicate per target framework — which
        // stopped being merely wasteful when this package hit nuget.org's 250 MB ceiling. Its .so
        // is still checked for, from the main .aar, by Carries_the_models_each_transformer_needs.
        Assert.DoesNotContain(names, n =>
            n.StartsWith("mltransformersaudionoisesuppression", StringComparison.Ordinal));

        // "-ps16k", not the unsuffixed artifact. That is the 16 KB page-size build Android 15
        // requires on 64-bit devices, and it is what client-sdk-video-transformers' own POM
        // declares — so resolving the plain one would be both a regression and a silent one.
        Assert.DoesNotContain(names, n => n.StartsWith("mltransformers-4", StringComparison.Ordinal));
    }

    [Fact]
    public void Carries_the_models_each_transformer_needs()
    {
        // One model per feature; a payload missing one fails in that feature's own way rather than
        // as a load error:
        //
        //   selfie_segmentation.tflite  background blur and background replacement
        //   ns_model_1 / ns_model_2     audio noise suppression
        using var package = Packages.OpenPackage(Packages.Transformers);

        var aar = AarsFor(package, Packages.TransformersTargetFramework)
            .Single(e => Path.GetFileName(e.FullName)
                .StartsWith("client-sdk-video-transformers-", StringComparison.Ordinal));

        using var buffer = Packages.ReadEntry(package, aar.FullName);
        using var archive = new ZipArchive(buffer);

        var entries = archive.Entries.Select(e => e.FullName).ToList();

        Assert.Contains(entries, n => n.EndsWith("selfie_segmentation.tflite", StringComparison.Ordinal));
        Assert.Contains(entries, n => n.EndsWith("ns_model_1.tflite", StringComparison.Ordinal));
        Assert.Contains(entries, n => n.EndsWith("ns_model_2.tflite", StringComparison.Ordinal));

        // The native libraries themselves, for the 64-bit ABIs Android 15 devices actually run.
        Assert.Contains(entries, n => n == "jni/arm64-v8a/libopentok_transformers.so");
        Assert.Contains(entries, n => n == "jni/arm64-v8a/libmltransformers.so");
        Assert.Contains(entries, n => n == "jni/arm64-v8a/libmltransformersaudionoisesuppression.so");
    }

    [Fact]
    public void Does_not_depend_on_the_binding()
    {
        // Deliberately independent, in both directions — a consumer adds this alongside
        // OpenTok.Net.Android only if it wants the ~70 MB. A dependency either way would take that
        // choice away, which is the whole reason this is a separate package.
        using var package = Packages.OpenPackage(Packages.Transformers);

        var nuspec = package.Entries.Single(e => e.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        using var reader = new StreamReader(nuspec.Open());
        var text = reader.ReadToEnd();

        Assert.DoesNotContain($"id=\"{Packages.OpenTok}\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain($"id=\"{Packages.Webrtc}\"", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The .aar files a package carries for one target framework. .NET Android packs them under
    /// lib/&lt;tfm&gt;/ beside the assembly, not into a nested archive the way the iOS binding
    /// resource package works.
    /// </summary>
    private static IEnumerable<ZipArchiveEntry> AarsFor(ZipArchive package, string tfm) =>
        package.Entries.Where(e =>
            e.FullName.StartsWith($"lib/{tfm}/", StringComparison.Ordinal) &&
            e.FullName.EndsWith(".aar", StringComparison.Ordinal));
}
