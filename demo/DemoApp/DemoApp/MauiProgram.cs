using DemoApp.Harness;
using Maui.Facebook.Core.Extensions;
using Microsoft.Extensions.Logging;

namespace DemoApp;

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
            });

        builder.Services.AddMauiBlazorWebView();

        builder.Services.AddFacebookCore();

        // The harness judges every check by what the wrapper logs, so its logger provider must be the
        // one the wrapper's ILogger<FacebookCoreService> resolves through.
        var harnessLog = new HarnessLog();
        var harnessLoggerProvider = new HarnessLoggerProvider(harnessLog);
        builder.Services.AddSingleton(harnessLog);
        builder.Services.AddSingleton(harnessLoggerProvider);
        builder.Services.AddSingleton<HarnessRunner>();
        builder.Logging.AddProvider(harnessLoggerProvider);

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
