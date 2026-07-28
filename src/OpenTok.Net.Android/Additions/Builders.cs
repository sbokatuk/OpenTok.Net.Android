using Android.Runtime;

namespace Com.Opentok.Android
{
    // Java's covariant return types do not survive into the binding.
    //
    // com.opentok.android.Publisher.Builder declares `public Publisher build()`, overriding
    // PublisherKit.Builder's `public PublisherKit build()` — legal Java since 5, implemented by the
    // compiler emitting a synthetic *bridge* method alongside the real one. class-parse sees the
    // bridge (marked bridge="true" in api.xml) and drops it, and drops the covariant override with
    // it: neither `build` appears under Publisher.Builder in the generated api.xml at all. So the
    // only Build() a consumer can reach is the inherited one, typed PublisherKit — even though the
    // object it returns really is a Publisher.
    //
    // Without the members below, every consumer writes the downcast by hand:
    //
    //     var publisher = new Publisher.Builder(context).Build().JavaCast<Publisher>();
    //
    // and has to know to reach for JavaCast rather than a plain C# cast — a plain `(Publisher)` cast
    // throws, because the managed peer the inherited Build() created is typed PublisherKit and C#
    // cannot downcast an object to a type it was not constructed as. JavaCast is what asks the JNI
    // layer for a new peer of the right type over the same Java instance.
    //
    // Restoring the covariant return here is not merely shorter; it is what stops that being a trap.
    // Both types are generated as `partial`, so this file is a plain addition to them — see
    // <Compile Include="Additions/**/*.cs" /> in OpenTok.Net.Android.csproj.
    //
    // `new` rather than `override`: the inherited Build() is virtual but returns PublisherKit, and
    // C# has no covariant override across a *different* return type on a non-generic virtual (return
    // type covariance would need Publisher to be a subtype of PublisherKit *and* the override to be
    // declared with `override` — which the .NET Android binder cannot express against a method it
    // generated separately). Hiding is correct here for the same reason Java needed a bridge method.

    public partial class Publisher
    {
        public partial class Builder
        {
            /// <summary>
            /// Builds the <see cref="Publisher"/>, typed as one.
            /// </summary>
            /// <remarks>
            /// Hides <c>PublisherKit.Builder.Build()</c>, which is typed <see cref="PublisherKit"/>
            /// because Java's covariant override is dropped by the binding generator. Calling
            /// through a <c>PublisherKit.Builder</c>-typed reference still reaches the base method
            /// and still returns a <see cref="PublisherKit"/>; that is the same behaviour Java has
            /// through a base-typed reference, minus the bridge.
            /// </remarks>
            public new Publisher Build() => base.Build().JavaCast<Publisher>();

            // The fluent setters below are the same covariant-return problem as Build(), and
            // restoring Build() alone would not have been enough: every one of these is declared on
            // Java's Publisher.Builder returning Publisher.Builder, and every one is dropped, so
            // the inherited PublisherKit.Builder-typed version is what a chain actually reaches:
            //
            //     new Publisher.Builder(context).Name("me").Build()   // -> PublisherKit, not Publisher
            //
            // — the covariant Build() silently falls out of the chain at the first setter. Each
            // override below re-types the return so a chain stays on Publisher.Builder throughout.
            //
            // `return this` rather than the base call's result: these are builder methods, and the
            // SDK's own overrides are `{ super.x(v); return this; }`. Returning the base result
            // would mean a JavaCast per link for an object already in hand.

            /// <summary>Sets the stream name. See <see cref="Stream.Name"/>.</summary>
            public new Builder Name(string name)
            {
                base.Name(name);
                return this;
            }

            /// <summary>Whether the published stream carries audio.</summary>
            public new Builder AudioTrack(bool audioTrack)
            {
                base.AudioTrack(audioTrack);
                return this;
            }

            /// <summary>Whether the published stream carries video.</summary>
            public new Builder VideoTrack(bool videoTrack)
            {
                base.VideoTrack(videoTrack);
                return this;
            }

            /// <summary>Supplies a custom video source in place of the device camera.</summary>
            public new Builder Capturer(BaseVideoCapturer capturer)
            {
                base.Capturer(capturer);
                return this;
            }

            /// <summary>Supplies a custom renderer for the local preview.</summary>
            public new Builder Renderer(BaseVideoRenderer renderer)
            {
                base.Renderer(renderer);
                return this;
            }

            /// <summary>Audio bitrate in bits per second.</summary>
            public new Builder AudioBitrate(int audioBitrate)
            {
                base.AudioBitrate(audioBitrate);
                return this;
            }

            /// <summary>Enables scalable video for a screen-sharing publisher.</summary>
            public new Builder ScalableScreenshare(bool scalableScreenshare)
            {
                base.ScalableScreenshare(scalableScreenshare);
                return this;
            }

            /// <summary>Enables Opus DTX, which stops sending during silence.</summary>
            public new Builder EnableOpusDtx(bool enableOpusDtx)
            {
                base.EnableOpusDtx(enableOpusDtx);
                return this;
            }

            /// <summary>Lets subscribers drop this stream's video before its audio.</summary>
            public new Builder SubscriberAudioFallbackEnabled(bool enabled)
            {
                base.SubscriberAudioFallbackEnabled(enabled);
                return this;
            }

            /// <summary>Lets this publisher drop its own video before its audio.</summary>
            public new Builder PublisherAudioFallbackEnabled(bool enabled)
            {
                base.PublisherAudioFallbackEnabled(enabled);
                return this;
            }

            /// <summary>Keeps the microphone open while <c>PublishAudio</c> is off.</summary>
            public new Builder AllowAudioCaptureWhileMuted(bool allow)
            {
                base.AllowAudioCaptureWhileMuted(allow);
                return this;
            }

            /// <summary>Orders the video codecs offered during negotiation.</summary>
            public new Builder PreferredVideoCodecs(PublisherKit.PreferredVideoCodecs codecs)
            {
                base.PreferredVideoCodecs(codecs);
                return this;
            }

            /// <summary>Enables the sender-side statistics track.</summary>
            public new Builder SenderStatsTrack(bool senderStatsTrack)
            {
                base.SenderStatsTrack(senderStatsTrack);
                return this;
            }
        }
    }

    public partial class Subscriber
    {
        public partial class Builder
        {
            /// <summary>
            /// Builds the <see cref="Subscriber"/>, typed as one.
            /// </summary>
            /// <remarks>
            /// Same reasoning as <see cref="Publisher.Builder.Build"/> — the SDK's covariant
            /// <c>build()</c> override does not reach the generated C# API.
            /// </remarks>
            public new Subscriber Build() => base.Build().JavaCast<Subscriber>();

            /// <summary>Supplies a custom renderer for the remote video.</summary>
            public new Builder Renderer(BaseVideoRenderer renderer)
            {
                base.Renderer(renderer);
                return this;
            }
        }
    }
}
