using Android.Runtime;
using Com.Opentok.Android;

namespace OpenTok.Net.Android.DeviceTests;

/// <summary>A single on-device check. Throws to fail.</summary>
public sealed record SmokeTest(string Name, Action Execute);

/// <summary>
/// End-to-end checks that only mean anything on a real device or emulator: they load the native
/// OpenTok SDK out of the packaged .aar files and drive the raw binding — <see cref="Session"/> and
/// <see cref="Publisher"/> themselves, no cross-platform façade in between.
/// </summary>
/// <remarks>
/// Nothing here needs real Vonage Video API credentials. Connecting a session is a live signalling
/// call to Vonage's servers, so these checks stop short of it: the API key and session ID are
/// syntactically plausible (a numeric key, an opaque session-id-shaped string) but unregistered,
/// which is enough to construct a <see cref="Session"/> and a <see cref="Publisher"/> — the parts
/// that prove the packaging and the JNI wiring, which is what this suite is for. No secret is
/// required and nothing would leak if one were passed in.
/// <para>
/// The checks are ordered: the publisher has to be created before it can be driven, and driven
/// before it can be destroyed. A failure early on therefore cascades, which is the intent — the
/// first failure is the informative one.
/// </para>
/// </remarks>
public static class SmokeTests
{
    // A syntactically plausible (but unregistered) API key and session ID — the shapes OpenTok's
    // own client-side construction expects — so building a Session and a Publisher does not fail
    // on input validation before the checks that actually exercise the binding.
    private const string ApiKey = "45123182";
    private const string SessionId = "1_MX40NTEyMzE4Mn5-fk9wZW5Ub2tFMkUyMDI2fg";

    public static Action<string> Reporter { get; set; } = _ => { };

    private static void Report(string message) => Reporter(message);

    private static Context Context => global::Android.App.Application.Context;

    /// <summary>The publisher every check after creation shares. Set by <see cref="CreatesThePublisher"/>.</summary>
    private static Publisher? _publisher;

    private static Publisher PublisherInstance =>
        _publisher ?? throw new InvalidOperationException("the publisher has not been created yet.");

    public static SmokeTest[] All =>
    [
        new("the Java entry points resolve from the packaged .aar", JavaEntryPointsResolve),
        new("creates a session from a syntactically valid API key and session ID", CreatesTheSession),
        new("creates the publisher — native libraries loaded", CreatesThePublisher),
        new("toggles publish audio and video", TogglesPublishAudioAndVideo),
        new("destroys the publisher", DestroysThePublisher),
    ];

    /// <summary>
    /// Proves the .aar files actually made it into the app.
    /// </summary>
    /// <remarks>
    /// This is the check that catches a packaging regression the compiler cannot see. A binding
    /// assembly reaches its Java classes through JNI lookups by name, so a package whose .aar was
    /// missing still compiles and links — and then throws ClassNotFoundException at runtime the
    /// first time a type is touched. That is exactly the failure mode @(AndroidMavenLibrary) being
    /// silently ignored on net8 would produce — see src/OpenTok.Binding.props.
    /// </remarks>
    private static void JavaEntryPointsResolve()
    {
        string[] classes =
        [
            "com.opentok.android.Session",
            "com.opentok.android.Publisher",
            "com.opentok.android.PublisherKit",
            "com.opentok.android.Subscriber",
            "com.opentok.android.SubscriberKit",
            "com.opentok.android.BaseVideoRenderer",
        ];

        // The app's own class loader, not Class.forName(String). The single-argument overload
        // resolves against the *caller's* loader, and the caller here is a runtime frame whose
        // loader is the boot classpath - so every application class comes back not-found, however
        // correctly it was packaged.
        var loader = Context.ClassLoader!;

        var missing = new List<string>();
        foreach (var name in classes)
        {
            try
            {
                _ = Java.Lang.Class.ForName(name, false, loader);
            }
            catch (Java.Lang.ClassNotFoundException)
            {
                missing.Add(name);
            }
        }

        Assert(missing.Count == 0, $"these Java classes are not in the app: {string.Join(", ", missing)}");
        Report($"all {classes.Length} Java entry points resolved");
    }

    private static void CreatesTheSession()
    {
        var session = new Session.Builder(Context, ApiKey, SessionId).Build();

        Assert(session is not null, "Session.Builder.Build() returned null.");
        Report("session created (not connected — no network call made)");
    }

    /// <summary>
    /// The check that proves the JNI wiring end to end: constructing a publisher is what touches
    /// the camera/microphone pipeline and loads the native webrtc engine backing OpenTok —
    /// carried, unbound, by OpenTok.Net.webrtc.Dependency.Android (Bind="false" there is a
    /// class-parse choice; the .so itself still ships and still loads).
    /// </summary>
    private static void CreatesThePublisher()
    {
        // PublisherKit.Builder.Build() is what Publisher.Builder inherits — Java's own covariant
        // return (the runtime object is a Publisher) does not reach the C# static type, so this
        // needs an explicit cast back to the concrete type. See PublisherKit.Builder.Build().
        var built = new Publisher.Builder(Context).Build();
        _publisher = built.JavaCast<Publisher>();

        Assert(_publisher is not null, "Publisher.Builder.Build() returned null.");
        Report("publisher created — camera/microphone pipeline and native libraries loaded");
    }

    private static void TogglesPublishAudioAndVideo()
    {
        // Plain property gets/sets — no channel, no server, but they do reach into the native
        // publisher created above rather than a purely managed field.
        PublisherInstance.PublishAudio = false;
        Assert(!PublisherInstance.PublishAudio, "PublishAudio did not turn off.");
        PublisherInstance.PublishAudio = true;
        Assert(PublisherInstance.PublishAudio, "PublishAudio did not turn back on.");

        PublisherInstance.PublishVideo = false;
        Assert(!PublisherInstance.PublishVideo, "PublishVideo did not turn off.");
        PublisherInstance.PublishVideo = true;
        Assert(PublisherInstance.PublishVideo, "PublishVideo did not turn back on.");
    }

    private static void DestroysThePublisher()
    {
        PublisherInstance.Destroy();
        _publisher = null;

        Report("publisher destroyed");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
