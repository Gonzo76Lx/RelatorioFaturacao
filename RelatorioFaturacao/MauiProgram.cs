using Microsoft.Extensions.Logging;
using RelatorioFaturacao.Services;

namespace RelatorioFaturacao;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // Global unhandled exception logging
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            AppLogger.LogError("Exceção não tratada no AppDomain (UnhandledException)", ex);
        };

        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            AppLogger.LogError("Exceção não observada em Task (UnobservedTaskException)", args.Exception);
            args.SetObserved();
        };

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}