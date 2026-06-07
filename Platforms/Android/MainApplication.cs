using Android.App;
using Android.Runtime;
using eMBTI.Services;

namespace eMBTI
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
            AppExceptionHandler.Initialize();

            AndroidEnvironment.UnhandledExceptionRaiser += (_, args) =>
            {
                _ = AppExceptionHandler.LogAsync(args.Exception, "Android 런타임 예외");
                args.Handled = true;
            };
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
