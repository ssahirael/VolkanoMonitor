using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using SkiaSharp.Views.Maui.Controls.Hosting;
using VolcanoMonitor.Services;
using VolcanoMonitor.Pages;

namespace VolcanoMonitor;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseSkiaSharp()
            // Catatan: UseMauiMaps() dihapus karena butuh Bing API key di Windows
            // Peta diganti WebView + Leaflet.js (OpenStreetMap)
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Poppins-Regular.ttf", "PoppinsRegular");
                fonts.AddFont("Poppins-Medium.ttf", "PoppinsMedium");
                fonts.AddFont("Poppins-Bold.ttf", "PoppinsBold");
            });

        // Register core services
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<NeuralNetworkService>();
        builder.Services.AddSingleton<FuzzyLogicService>();
        builder.Services.AddSingleton<GeminiService>();
        builder.Services.AddSingleton<FaceRecognitionService>();

        // Register UI pages (transient)
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<FaceScanPage>();
        builder.Services.AddTransient<StatusPage>();
        builder.Services.AddTransient<MapPage>();
        builder.Services.AddTransient<JelajahPage>();
        builder.Services.AddTransient<NotifikasiPage>();
        builder.Services.AddTransient<PengaturanPage>();

        return builder.Build();
    }
}