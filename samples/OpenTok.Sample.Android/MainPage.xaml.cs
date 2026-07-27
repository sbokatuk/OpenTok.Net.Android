using Android.Runtime;
using Com.Opentok.Android;

namespace OpenTok.Sample.Android;

/// <summary>
/// Creates a local <see cref="Publisher"/> directly against the raw binding and shows its camera
/// preview. Deliberately minimal: no <see cref="Session"/> is connected, so it runs with no
/// Vonage Video API credentials at all — a <see cref="Publisher"/> captures and renders locally
/// on its own, connecting to a session is what actually publishes it. That full
/// connect/publish/subscribe flow is what an app built on top of this binding adds.
/// </summary>
public partial class MainPage : ContentPage
{
    private Publisher? _publisher;
    private bool _audioMuted;

    public MainPage()
    {
        InitializeComponent();

        StatusLabel.Text = $"native SDK {Com.Opentok.Android.BuildConfig.SdkVersion}";
    }

    private async void OnStartPreviewClicked(object sender, EventArgs e)
    {
        // Dangerous (runtime) permissions: the manifest declaration alone does not grant them.
        // Microphone as well as camera — the publisher opens the audio device even for a
        // preview-only publisher with nothing connected.
        var camera = await Permissions.RequestAsync<Permissions.Camera>();
        var microphone = await Permissions.RequestAsync<Permissions.Microphone>();
        if (camera != PermissionStatus.Granted || microphone != PermissionStatus.Granted)
        {
            Append("camera and microphone permission denied");
            return;
        }

        var context = Platform.CurrentActivity ?? global::Android.App.Application.Context;

        // PublisherKit.Builder.Build() is what Publisher.Builder inherits — Java's own covariant
        // return (the runtime object is a Publisher) does not reach the C# static type, so this
        // needs an explicit cast back to the concrete type — see
        // tests/OpenTok.Net.Android.DeviceTests/SmokeTests.cs for the same cast.
        var built = new Publisher.Builder(context).Build();
        var publisher = built.JavaCast<Publisher>();

        // The handler's platform view is a plain container — see OpenTokVideoView.cs for why,
        // unlike Agora's raw SurfaceView, OpenTok hands its own view back only once a publisher
        // exists rather than wanting one supplied up front.
        var handler = (OpenTokVideoViewHandler)LocalView.Handler!;
        handler.SetChild(publisher.View!);

        _publisher = publisher;
        _audioMuted = false;
        SetPreviewing(true);
        Append("previewing — nothing connected, no credentials involved");
    }

    private void OnMuteClicked(object sender, EventArgs e)
    {
        if (_publisher is null)
        {
            return;
        }

        _audioMuted = !_audioMuted;
        _publisher.PublishAudio = !_audioMuted;
        MuteButton.Text = _audioMuted ? "Unmute audio" : "Mute audio";
        Append(_audioMuted ? "audio muted" : "audio unmuted");
    }

    private void OnStopClicked(object sender, EventArgs e)
    {
        StopPublisher();
        SetPreviewing(false);
        Append("stopped");
    }

    private void StopPublisher()
    {
        // Empty the container before releasing the view it was showing.
        (LocalView.Handler as OpenTokVideoViewHandler)?.Clear();

        // Releases the camera and the native renderer.
        _publisher?.Destroy();
        _publisher = null;
    }

    private void SetPreviewing(bool previewing)
    {
        StartButton.IsEnabled = !previewing;
        StopButton.IsEnabled = previewing;
        MuteButton.IsEnabled = previewing;
        MuteButton.Text = "Mute audio";
    }

    private void Append(string message) => MainThread.BeginInvokeOnMainThread(() =>
        ActivityLabel.Text = $"{DateTime.Now:HH:mm:ss}  {message}");

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is null)
        {
            StopPublisher();
        }
    }
}
