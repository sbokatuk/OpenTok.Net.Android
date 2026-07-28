using System.IO.Compression;

namespace OpenTok.Net.Android.PackageTests;

/// <summary>Locates the packed .nupkg files and describes what each is expected to contain.</summary>
public static class Packages
{
    public const string OpenTok = "OpenTok.Net.Android";
    public const string Webrtc = "OpenTok.Net.webrtc.Dependency.Android";

    /// <summary>
    /// The media transformers package. Deliberately absent from <see cref="All"/>.
    /// </summary>
    /// <remarks>
    /// It is not a binding: it carries native payload and .tflite models and has no managed API at
    /// all (see src/OpenTok.Transformers.md), so every expectation in <see cref="All"/> — core
    /// types, a public-type floor, an assembly size floor — is the wrong question to ask of it. It
    /// gets its own checks in <c>TransformersPackageTests</c> instead, the same way the sibling
    /// Net.Agora.iOS repository holds its payload-only extension packages to a different standard
    /// than its real bindings.
    /// </remarks>
    public const string Transformers = "OpenTok.Net.Transformers.Android";

    /// <summary>
    /// The two target frameworks the transformers package ships, unlike every other package here,
    /// which ships three.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three copies of its payload came to 319 MB, and nuget.org refuses anything over 250 MB — the
    /// push failed with HTTP 413. net9 is the one dropped, because it is the one nothing needs an
    /// exact match for: NuGet resolves the best <em>compatible</em> asset folder, so a net9 app
    /// takes the net8 copy. net8 and net10 are the ends of the range and both stay.
    /// </para>
    /// <para>
    /// Safe precisely because the package has no managed API. There is nothing target-framework
    /// specific in it to get wrong — only .so files and .tflite models, byte identical in each copy.
    /// Verified by building the device tests against it at all three of net8.0-android34.0,
    /// net9.0-android35.0 and net10.0-android36.0 and inspecting the APKs, which is what the
    /// transformers-packaging CI job repeats on every pull request.
    /// </para>
    /// </remarks>
    public static readonly string[] TransformersTargetFrameworks =
    [
        "net8.0-android34.0", "net10.0-android36.0",
    ];

    /// <summary>The framework whose copy of the payload the content tests open. Any would do.</summary>
    public const string TransformersTargetFramework = "net8.0-android34.0";

    public static IEnumerable<object[]> TransformersFrameworks =>
        TransformersTargetFrameworks.Select(tfm => new object[] { tfm });

    /// <summary>
    /// A floor on the main transformers .aar, which is ~39 MB compressed (~74 MB on disk: three
    /// native libraries across three ABIs, plus three TensorFlow Lite models). The package carries
    /// one more .aar beside it — mltransformers-ps16k — for ~84 MB in total.
    /// </summary>
    /// <remarks>
    /// Well below the real size on purpose. The only thing a floor can usefully answer is "did a
    /// placeholder get packed instead of the artifact", and pinning it close to the true size just
    /// means re-editing this constant every time Vonage recompresses something.
    /// </remarks>
    public const long MinTransformersAarBytes = 20_000_000;

    /// <summary>The types a consumer of the OpenTok binding starts with.</summary>
    private static readonly string[] OpenTokCoreTypes =
    [
        "Com.Opentok.Android.Session",
        "Com.Opentok.Android.Publisher",
        "Com.Opentok.Android.PublisherKit",
        "Com.Opentok.Android.Subscriber",
        "Com.Opentok.Android.SubscriberKit",
        "Com.Opentok.Android.Stream",
        "Com.Opentok.Android.OpentokError",
        "Com.Opentok.Android.BaseVideoRenderer",
    ];

    /// <summary>
    /// The types a consumer of the webrtc dependency package would touch — reached only through
    /// what OpenTok's own API hands back (a custom renderer, stats), never referenced directly by
    /// a typical app; see OpenTok.Net.webrtc.Dependency.Android.csproj.
    /// </summary>
    private static readonly string[] WebrtcCoreTypes =
    [
        "Com.Vonage.Webrtc.PeerConnectionFactory",
        "Com.Vonage.Webrtc.PeerConnection",
        "Com.Vonage.Webrtc.MediaStream",
        "Com.Vonage.Webrtc.VideoTrack",
        "Com.Vonage.Webrtc.AudioTrack",
    ];

    /// <summary>
    /// Every package build/packages.tsv lists, with what its packed form is expected to contain:
    /// the native .aar (by name prefix and a size floor that rules out placeholders), the types a
    /// consumer starts with, and a public-type floor that rules out an empty binding shell. Pinned
    /// rather than parsed from the .tsv: a package silently dropped from the .tsv (and so from the
    /// pack) is a regression these tests should catch, not adapt to.
    /// </summary>
    public static readonly (string Id, string AarPrefix, long MinAarBytes, long MinAssemblyBytes, string[] CoreTypes, int MinPublicTypes)[] All =
    [
        // opentok-android-sdk is a ~17.6 MB .aar and binds ~90 public types into a >600 KB assembly.
        (OpenTok, "opentok-android-sdk-", 10_000_000, 600_000, OpenTokCoreTypes, 40),
        // webrtc is a ~243 MB .aar (four ABIs of the WebRTC native engine) and binds ~139 public
        // types into a >600 KB assembly. The floor is far below the real .aar size — it only needs
        // to rule out the net8 "empty shell" trap, not approximate the real artifact.
        (Webrtc, "webrtc-", 200_000_000, 600_000, WebrtcCoreTypes, 60),
    ];

    /// <summary>
    /// Target frameworks every package here must carry, one per SDK band pass. Pinned rather than
    /// discovered: a package that silently lost a target framework because a pack pass failed is
    /// exactly the regression these tests exist to catch.
    /// </summary>
    public static readonly string[] TargetFrameworks =
    [
        "net8.0-android34.0", "net9.0-android35.0", "net10.0-android36.0",
    ];

    public static IEnumerable<object[]> Frameworks =>
        TargetFrameworks.Select(tfm => new object[] { tfm });

    /// <summary>Every (package, target framework) pair — the axis most tests run over.</summary>
    public static IEnumerable<object[]> PackageFrameworks =>
        All.SelectMany(p => TargetFrameworks.Select(tfm => new object[] { p.Id, tfm }));

    /// <summary>Like <see cref="PackageFrameworks"/>, with the expected native .aar name and floor.</summary>
    public static IEnumerable<object[]> PackageFrameworkAars =>
        All.SelectMany(p => TargetFrameworks.Select(tfm => new object[] { p.Id, tfm, p.AarPrefix, p.MinAarBytes }));

    public static long MinAssemblyBytesOf(string packageId) => Row(packageId).MinAssemblyBytes;

    public static (string Id, string AarPrefix, long MinAarBytes, long MinAssemblyBytes, string[] CoreTypes, int MinPublicTypes) Row(string packageId) =>
        All.Single(p => p.Id == packageId);

    public static string ArtifactsDirectory { get; } = ResolveArtifactsDirectory();

    public static string FindPackage(string packageId, string extension = ".nupkg")
    {
        var matches = Directory.Exists(ArtifactsDirectory)
            ? Directory.GetFiles(ArtifactsDirectory, $"{packageId}.*{extension}")
                .Where(f => IsVersionOf(packageId, Path.GetFileName(f), extension))
                .ToArray()
            : [];

        Assert.True(
            matches.Length > 0,
            $"No {packageId}.<version>{extension} found in '{ArtifactsDirectory}'. " +
            "Run build/BuildNugets.sh first.");

        return matches.OrderByDescending(File.GetLastWriteTimeUtc).First();
    }

    private static bool IsVersionOf(string packageId, string fileName, string extension)
    {
        var remainder = fileName[(packageId.Length + 1)..^extension.Length];
        return remainder.Length > 0 && char.IsDigit(remainder[0]);
    }

    public static ZipArchive OpenPackage(string packageId, string extension = ".nupkg") =>
        ZipFile.OpenRead(FindPackage(packageId, extension));

    /// <summary>Reads a package entry fully into memory so it can be seeked.</summary>
    public static MemoryStream ReadEntry(ZipArchive package, string entryName)
    {
        var entry = package.GetEntry(entryName);
        Assert.True(entry is not null, $"Package has no entry '{entryName}'.");

        var buffer = new MemoryStream();
        using (var stream = entry!.Open())
        {
            stream.CopyTo(buffer);
        }

        buffer.Position = 0;
        return buffer;
    }

    private static string ResolveArtifactsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName ?? AppContext.BaseDirectory;

        return Environment.GetEnvironmentVariable("OPENTOK_ARTIFACTS") is { Length: > 0 } configured
            ? Path.GetFullPath(configured, root)
            : Path.Combine(root, "artifacts");
    }
}
