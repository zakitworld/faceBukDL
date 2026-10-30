using faceBukDL.Services;
using faceBukDL.ViewModels;
using Microsoft.Extensions.Logging;

namespace faceBukDL;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddSingleton<IClipboardService, MauiClipboardService>();
        builder.Services.AddSingleton<IVideoExportService, VideoExportService>();
        builder.Services.AddSingleton<IVideoUrlResolver, DirectVideoUrlResolver>();
        builder.Services.AddSingleton<IVideoDownloadService>(services =>
            new VideoDownloadService(new HttpClient { Timeout = TimeSpan.FromMinutes(5) },
                services.GetRequiredService<IVideoUrlResolver>()));
#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
