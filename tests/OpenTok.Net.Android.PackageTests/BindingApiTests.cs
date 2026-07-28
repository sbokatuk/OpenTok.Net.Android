namespace OpenTok.Net.Android.PackageTests;

/// <summary>
/// Asserts that the binding assembly inside the package actually exposes the expected API. A
/// binding that fails to generate still compiles and packs cleanly — it just produces an
/// almost-empty assembly — so the package layout (and its file-size heuristics in
/// <see cref="PackageLayoutTests"/>) is not enough to prove the build worked.
/// </summary>
/// <remarks>
/// The assembly is read with the metadata reader (<see cref="AssemblyApi"/>) rather than loaded:
/// it targets *-android and references Mono.Android, so it cannot be loaded into this desktop
/// test process.
/// </remarks>
public class BindingApiTests
{
    private static AssemblyApi OpenBinding(string packageId, string tfm)
    {
        using var package = Packages.OpenPackage(packageId);
        var assembly = Packages.ReadEntry(package, $"lib/{tfm}/{packageId}.dll");
        return new AssemblyApi(assembly);
    }

    [Theory]
    [MemberData(nameof(Packages.PackageFrameworks), MemberType = typeof(Packages))]
    public void Binding_exposes_the_core_types(string packageId, string tfm)
    {
        using var api = OpenBinding(packageId, tfm);

        var missing = Packages.Row(packageId).CoreTypes.Except(api.PublicTypes).ToList();

        Assert.True(
            missing.Count == 0,
            $"{packageId} ({tfm}) is missing bound types: {string.Join(", ", missing)}. " +
            $"The assembly exposes {api.PublicTypes.Count} public types in total.");
    }

    [Theory]
    [MemberData(nameof(Packages.PackageFrameworks), MemberType = typeof(Packages))]
    public void Binding_is_not_an_empty_shell(string packageId, string tfm)
    {
        using var api = OpenBinding(packageId, tfm);

        // Guards the real failure mode described in src/OpenTok.Binding.props: @(AndroidMavenLibrary)
        // silently ignored produces a valid but essentially empty assembly, which still packs and
        // installs fine. The floor is per package — see Packages.All — with the margin below each
        // artifact's real count as headroom for the SDK trimming its surface, not for a broken build.
        Assert.True(
            api.PublicTypes.Count >= Packages.Row(packageId).MinPublicTypes,
            $"{packageId} ({tfm}) exposes only {api.PublicTypes.Count} public types; " +
            "the binding generator likely did not run.");
    }

    [Theory]
    [MemberData(nameof(Packages.Frameworks), MemberType = typeof(Packages))]
    public void Session_exposes_the_connection_lifecycle_entry_points(string tfm)
    {
        using var api = OpenBinding(Packages.OpenTok, tfm);

        var methods = api.MethodsOf("Com.Opentok.Android.Session");

        Assert.Contains("Connect", methods);
        Assert.Contains("Disconnect", methods);
    }

    [Theory]
    [MemberData(nameof(Packages.Frameworks), MemberType = typeof(Packages))]
    public void PublisherKit_exposes_the_publish_toggles(string tfm)
    {
        using var api = OpenBinding(Packages.OpenTok, tfm);

        var properties = api.PropertiesOf("Com.Opentok.Android.PublisherKit");

        Assert.Contains("PublishAudio", properties);
        Assert.Contains("PublishVideo", properties);
    }

    [Theory]
    [MemberData(nameof(Packages.Frameworks), MemberType = typeof(Packages))]
    public void SubscriberKit_disconnected_events_do_not_collide(string tfm)
    {
        using var api = OpenBinding(Packages.OpenTok, tfm);

        // Transforms/Metadata.xml renames both SubscriberKit.StreamListener.onDisconnected and
        // SubscriberKit.SubscriberListener.onDisconnected away from class-parse's shared default
        // ("Disconnected"/"DisconnectedEventArgs" for both) to avoid a member collision (CS0102).
        // A regenerated binding that lost either half of the rename would still compile — the
        // *other* interface's onDisconnected would just silently reclaim the generic name — so
        // this checks both distinct names survived, not just that the package built.
        var methods = api.MethodsOf("Com.Opentok.Android.SubscriberKit");

        Assert.Contains("add_StreamDisconnected", methods);
        Assert.Contains("add_SubscriberDisconnected", methods);
    }

    [Theory]
    [MemberData(nameof(Packages.Frameworks), MemberType = typeof(Packages))]
    public void Listener_event_args_carry_named_properties(string tfm)
    {
        using var api = OpenBinding(Packages.OpenTok, tfm);

        // opentok-android-sdk's classes.jar carries no parameter names, so class-parse's default
        // for every listener callback is p0/p1/p2 — which becomes P0/P1/P2 on the generated
        // EventArgs, the form a consumer actually reads. Transforms/Metadata.xml renames them.
        //
        // This is a regression guard with real teeth: a metadata path that stops matching (a
        // renamed interface, a changed parameter count, a typo in the XPath) is silently ignored
        // by the binding generator. The build still succeeds and the package still installs — the
        // properties just quietly revert to P0/P1, which nothing else in this suite would notice.
        Assert.Equal(
            new[] { "Session", "Stream" },
            api.PropertiesOf("Com.Opentok.Android.Session/StreamReceivedEventArgs"));

        // Four parameters, two of them adjacent strings — the case where positional names are not
        // just ugly but genuinely ambiguous at the call site.
        Assert.Equal(
            new[] { "Session", "Type", "Data", "Connection" },
            api.PropertiesOf("Com.Opentok.Android.Session/SignalEventArgs"));

        Assert.Equal(
            new[] { "Publisher", "Stream" },
            api.PropertiesOf("Com.Opentok.Android.PublisherKit/StreamCreatedEventArgs"));

        Assert.Equal(
            new[] { "Subscriber", "Error" },
            api.PropertiesOf("Com.Opentok.Android.SubscriberKit/ErrorEventArgs"));
    }

    [Theory]
    [MemberData(nameof(Packages.Frameworks), MemberType = typeof(Packages))]
    public void Concrete_builders_return_their_own_type(string tfm)
    {
        using var api = OpenBinding(Packages.OpenTok, tfm);

        // Java's covariant `Publisher build()` override is dropped by class-parse along with its
        // synthetic bridge, leaving only the inherited PublisherKit-typed Build(). Additions/
        // Builders.cs restores it. Without that, consumers need a JavaCast<Publisher>() — and a
        // plain C# cast, the obvious thing to reach for, throws.
        //
        // Declared on the nested Builder itself, so finding Build here (rather than only on
        // PublisherKit.Builder) is exactly the assertion: an Additions file dropped from the
        // project would still build and pack, just without these.
        Assert.Contains("Build", api.MethodsOf("Com.Opentok.Android.Publisher/Builder"));
        Assert.Contains("Build", api.MethodsOf("Com.Opentok.Android.Subscriber/Builder"));
    }
}
