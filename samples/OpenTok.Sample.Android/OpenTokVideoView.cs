using Android.Views;
using Android.Widget;
using Microsoft.Maui.Handlers;

namespace OpenTok.Sample.Android;

/// <summary>
/// A MAUI view that hosts whatever native view OpenTok hands back for a preview or subscriber
/// stream.
///
/// Unlike Agora's <c>RtcEngine</c> — which wants an app-supplied <c>SurfaceView</c> handed
/// *into* <c>VideoCanvas</c> — OpenTok's own <c>PublisherKit.View</c> /
/// <c>SubscriberKit.View</c> hand a ready-made <c>Android.Views.View</c> *out*, created only once
/// a <c>Publisher</c>/<c>Subscriber</c> exists. So the platform view here is a plain
/// <c>FrameLayout</c> container created up front (as any MAUI handler's platform view must be),
/// and <see cref="OpenTokVideoViewHandler.SetChild"/> attaches OpenTok's view into it later, once
/// one exists — see MainPage.xaml.cs.
/// </summary>
// Explicitly Microsoft.Maui.Controls.View: Android.Views.View is also in scope (for the handler
// below) and "View" alone is ambiguous between the two.
public class OpenTokVideoView : Microsoft.Maui.Controls.View
{
}

/// <summary>Binds <see cref="OpenTokVideoView"/> to a native <see cref="FrameLayout"/> container.</summary>
public class OpenTokVideoViewHandler : ViewHandler<OpenTokVideoView, FrameLayout>
{
    /// <summary>The view has no properties of its own; the mapper exists because a handler needs one.</summary>
    public static readonly IPropertyMapper<OpenTokVideoView, OpenTokVideoViewHandler> PreviewMapper =
        new PropertyMapper<OpenTokVideoView, OpenTokVideoViewHandler>(ViewMapper);

    /// <summary>Creates the handler. Registered in <see cref="MauiProgram.CreateMauiApp"/>.</summary>
    public OpenTokVideoViewHandler() : base(PreviewMapper)
    {
    }

    protected override FrameLayout CreatePlatformView() => new(Context);

    /// <summary>Replaces whatever this container currently shows with <paramref name="child"/>.</summary>
    public void SetChild(global::Android.Views.View child)
    {
        PlatformView.RemoveAllViews();
        PlatformView.AddView(child, new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
    }

    /// <summary>Empties the container — called before releasing the publisher/subscriber that owned the view.</summary>
    public void Clear() => PlatformView.RemoveAllViews();
}
