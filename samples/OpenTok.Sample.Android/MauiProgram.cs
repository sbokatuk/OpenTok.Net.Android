using Microsoft.Extensions.Logging;

namespace OpenTok.Sample.Android;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            // The container OpenTok's publisher/subscriber view is attached into — see
            // OpenTokVideoView.cs for why a custom handler rather than a wrapped ContentView.
            .ConfigureMauiHandlers(handlers =>
                handlers.AddHandler<OpenTokVideoView, OpenTokVideoViewHandler>());

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
