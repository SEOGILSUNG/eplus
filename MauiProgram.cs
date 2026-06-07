using Microsoft.Extensions.Logging;
using eMBTI.Services;

namespace eMBTI
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            AppExceptionHandler.Initialize();

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
}
