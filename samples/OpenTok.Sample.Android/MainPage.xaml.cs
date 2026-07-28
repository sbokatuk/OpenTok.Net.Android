using System.Text;
using Android.Content;
using Com.Opentok.Android;

namespace OpenTok.Sample.Android;

/// <summary>
/// Connects to an OpenTok session, publishing this device's camera/microphone and subscribing to
/// the first remote stream — built directly against <see cref="Session"/>/<see cref="Publisher"/>/
/// <see cref="Subscriber"/> rather than a cross-platform façade, since this repository binds only
/// the raw Android SDK.
/// </summary>
/// <remarks>
/// The counterpart to samples/OpenTok.Sample.iOS/MainPage.xaml.cs, and deliberately the same flow
/// so the two can be read against each other. Two differences are the SDKs', not this sample's:
/// <list type="bullet">
/// <item>
/// Android reports failures through an <c>Error</c> event carrying an <see cref="OpentokError"/>,
/// where iOS answers an <c>OTError</c> out parameter on each call. There is nothing to check
/// synchronously here — <see cref="Session.Connect"/> returns immediately either way.
/// </item>
/// <item>
/// Android needs runtime permission prompts (the manifest declaration alone grants nothing) and an
/// <c>OnPause</c>/<c>OnResume</c> pairing, because an Android app is routinely backgrounded with
/// the camera still open. See <see cref="OnDisappearing"/>.
/// </item>
/// </list>
/// </remarks>
public partial class MainPage : ContentPage
{
    private readonly StringBuilder _status = new();

    private Session? _session;
    private Publisher? _publisher;
    private Subscriber? _subscriber;

    public MainPage()
    {
        InitializeComponent();

        StatusLabel.Text = $"native SDK {BuildConfig.SdkVersion}\n";
    }

    private static Context Context => Platform.CurrentActivity ?? global::Android.App.Application.Context;

    private async void OnConnectClicked(object? sender, EventArgs e)
    {
        var apiKey = ApiKeyEntry.Text?.Trim();
        var sessionId = SessionIdEntry.Text?.Trim();
        var token = TokenEntry.Text?.Trim();

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(token))
        {
            Append("enter an API key, a session id and a token first");
            return;
        }

        // Dangerous (runtime) permissions: the manifest declaration alone does not grant them.
        // Microphone as well as camera — a publisher opens the audio device even when only its
        // video is wanted.
        if (!await RequestCapturePermissionsAsync())
        {
            Append("camera and microphone permission denied");
            return;
        }

        var session = new Session.Builder(Context, apiKey, sessionId).Build();

        // Event handlers rather than a Session.ISessionListener implementation: the binding
        // generates both, and the events avoid a Java.Lang.Object subclass per listener. The event
        // args carry named properties (e.Stream, e.Error) — see the parameter renames in
        // src/OpenTok.Net.Android/Transforms/Metadata.xml for why that is not the default.
        session.Connected += OnSessionConnected;
        session.Disconnected += OnSessionDisconnected;
        session.Error += OnSessionError;
        session.StreamReceived += OnStreamReceived;
        session.StreamDropped += OnStreamDropped;

        _session = session;
        ConnectButton.IsEnabled = false;
        Append("connecting…");

        // Asynchronous: this returns straight away and the outcome arrives on Connected or Error.
        session.Connect(token);
    }

    private void OnDisconnectClicked(object? sender, EventArgs e)
    {
        TeardownPublisher();
        TeardownSubscriber();
        TeardownSession();

        SetConnected(false);
        Append("disconnected");
    }

    private void OnPublishClicked(object? sender, EventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        // Build() returns a Publisher, not a PublisherKit, and the chain stays on
        // Publisher.Builder throughout — both restored in this package's Additions/Builders.cs,
        // because Java's covariant return types do not survive the binding generator. Without
        // those, this line reads
        //     ((Publisher)…) new Publisher.Builder(Context).Name("…").Build().JavaCast<Publisher>()
        // and the plain C# cast an author would reach for first throws at runtime.
        var publisher = new Publisher.Builder(Context)
            .Name("opentok-net-android-sample")
            .Build();

        publisher.StreamCreated += OnPublisherStreamCreated;
        publisher.Error += OnPublisherError;

        // Publisher.View exists as soon as the publisher does — the camera preview starts rendering
        // into it immediately, before the stream is created. Attaching it here rather than waiting
        // for a callback is what every OpenTok sample does.
        LocalHandler?.SetChild(publisher.View!);

        _publisher = publisher;
        PublishButton.IsEnabled = false;

        _session.Publish(publisher);
        Append("publishing");
    }

    private void OnSessionConnected(object? sender, Session.ConnectedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SetConnected(true);
            Append("connected");
        });

    private void OnSessionDisconnected(object? sender, Session.DisconnectedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SetConnected(false);
            Append("session disconnected");
        });

    // GetErrorCode() rather than an ErrorCode property: OpentokError.ErrorCode is a *nested type*
    // (a Java enum), so the accessor cannot be projected as a property of the same name.
    private void OnSessionError(object? sender, Session.ErrorEventArgs e) =>
        Append($"session error: {e.Error?.GetErrorCode()} {e.Error?.Message}");

    /// <summary>
    /// Subscribes to the first remote stream the session reports and renders it into
    /// <c>RemoteView</c>. Only one remote view exists in this sample, so later streams are noted
    /// and ignored.
    /// </summary>
    private void OnStreamReceived(object? sender, Session.StreamReceivedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Append($"remote stream created: {e.Stream?.StreamId}");

            if (_subscriber is not null || _session is null || e.Stream is null)
            {
                return;
            }

            var subscriber = new Subscriber.Builder(Context, e.Stream).Build();

            // Unlike the publisher's, a subscriber's view is only worth attaching once it has
            // connected to the stream — before that there is nothing decoded to show.
            subscriber.Connected += OnSubscriberConnected;
            subscriber.Error += OnSubscriberError;

            _subscriber = subscriber;
            _session.Subscribe(subscriber);
        });

    private void OnStreamDropped(object? sender, Session.StreamDroppedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Append($"remote stream destroyed: {e.Stream?.StreamId}");

            if (_subscriber?.Stream?.StreamId == e.Stream?.StreamId)
            {
                TeardownSubscriber();
            }
        });

    private void OnSubscriberConnected(object? sender, SubscriberKit.ConnectedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_subscriber?.View is { } view)
            {
                RemoteHandler?.SetChild(view);
                Append("subscribed — remote video attached");
            }
        });

    private void OnSubscriberError(object? sender, SubscriberKit.ErrorEventArgs e) =>
        Append($"subscriber error: {e.Error?.GetErrorCode()} {e.Error?.Message}");

    private void OnPublisherStreamCreated(object? sender, PublisherKit.StreamCreatedEventArgs e) =>
        Append("local stream published");

    private void OnPublisherError(object? sender, PublisherKit.ErrorEventArgs e) =>
        Append($"publisher error: {e.Error?.GetErrorCode()} {e.Error?.Message}");

    private static async Task<bool> RequestCapturePermissionsAsync()
    {
        var camera = await Permissions.RequestAsync<Permissions.Camera>();
        var microphone = await Permissions.RequestAsync<Permissions.Microphone>();

        return camera == PermissionStatus.Granted && microphone == PermissionStatus.Granted;
    }

    private OpenTokVideoViewHandler? LocalHandler => LocalView.Handler as OpenTokVideoViewHandler;

    private OpenTokVideoViewHandler? RemoteHandler => RemoteView.Handler as OpenTokVideoViewHandler;

    private void SetConnected(bool connected)
    {
        ConnectButton.IsEnabled = !connected;
        DisconnectButton.IsEnabled = connected;
        PublishButton.IsEnabled = connected && _publisher is null;
    }

    private void TeardownPublisher()
    {
        if (_publisher is null)
        {
            return;
        }

        _publisher.StreamCreated -= OnPublisherStreamCreated;
        _publisher.Error -= OnPublisherError;

        // Empty the container before releasing the view it was showing.
        LocalHandler?.Clear();

        _session?.Unpublish(_publisher);

        // Releases the camera and the native renderer. Not optional: without it the camera stays
        // open until the process dies.
        _publisher.Destroy();
        _publisher = null;

        PublishButton.IsEnabled = _session is not null;
    }

    private void TeardownSubscriber()
    {
        if (_subscriber is null)
        {
            return;
        }

        _subscriber.Connected -= OnSubscriberConnected;
        _subscriber.Error -= OnSubscriberError;

        RemoteHandler?.Clear();

        // Unsubscribe is the whole teardown — SubscriberKit.Destroy() is deprecated in this SDK
        // version, unlike the publisher's, which still has a camera to hand back.
        _session?.Unsubscribe(_subscriber);
        _subscriber = null;
    }

    private void TeardownSession()
    {
        if (_session is null)
        {
            return;
        }

        _session.Connected -= OnSessionConnected;
        _session.Disconnected -= OnSessionDisconnected;
        _session.Error -= OnSessionError;
        _session.StreamReceived -= OnStreamReceived;
        _session.StreamDropped -= OnStreamDropped;

        _session.Disconnect();
        _session = null;
    }

    private void Append(string message) => MainThread.BeginInvokeOnMainThread(() =>
    {
        _status.AppendLine($"{DateTime.Now:HH:mm:ss}  {message}");
        StatusLabel.Text = _status.ToString();
        StatusScroll.ScrollToAsync(0, StatusLabel.Height, animated: false);
    });

    /// <summary>
    /// Releases the camera and the session when the page goes away.
    /// </summary>
    /// <remarks>
    /// <see cref="Page.OnDisappearing"/> rather than the iOS sample's <c>OnHandlerChanged</c>: on
    /// Android the page is routinely torn down and rebuilt (a rotation, or the activity being
    /// recreated) while the app is still running, and a publisher whose camera was never released
    /// blocks the next one from opening it.
    /// </remarks>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        TeardownPublisher();
        TeardownSubscriber();
        TeardownSession();
    }
}
