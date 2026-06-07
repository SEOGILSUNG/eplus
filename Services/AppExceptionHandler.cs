using System.Diagnostics;
using System.Text;

namespace eMBTI.Services;

/// <summary>
/// 앱 전체 예외를 기록하고 사용자에게 안전하게 안내하기 위한 공통 처리기입니다.
/// </summary>
public static class AppExceptionHandler
{
    private static bool _initialized;
    private static readonly SemaphoreSlim AlertLock = new(1, 1);
    private const string DefaultUserMessage = "일시적인 문제가 발생했습니다. 앱은 계속 사용할 수 있도록 복구했습니다.";

    public static void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString() ?? "Unknown unhandled exception");
            _ = LogAsync(ex, "UnhandledException");
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            _ = LogAsync(e.Exception, "UnobservedTaskException");
            e.SetObserved();
        };
    }

    public static async Task RunAsync(Func<Task> action, string workName = "처리")
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            await HandleAsync(ex, workName);
        }
    }

    public static async Task RunAsync(Action action, string workName = "처리")
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            await HandleAsync(ex, workName);
        }
    }

    public static async Task HandleAsync(Exception ex, string workName = "처리")
    {
        await LogAsync(ex, workName);
        await ShowUserMessageAsync(workName, DefaultUserMessage);
    }

    public static async Task ShowUserMessageAsync(string title, string message)
    {
        try
        {
            await AlertLock.WaitAsync();

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var page = Application.Current?.Windows.FirstOrDefault()?.Page;
                if (page == null)
                    return;

                string safeTitle = string.IsNullOrWhiteSpace(title) ? "안내" : title;
                string safeMessage = string.IsNullOrWhiteSpace(message) ? DefaultUserMessage : message;
                await page.DisplayAlert(safeTitle, safeMessage, "확인");
            });
        }
        catch
        {
            // 오류 안내 중 다시 오류가 발생하면 앱 종료를 막기 위해 무시합니다.
        }
        finally
        {
            if (AlertLock.CurrentCount == 0)
                AlertLock.Release();
        }
    }

    public static async Task LogAsync(Exception ex, string workName = "처리")
    {
        try
        {
            string filePath = Path.Combine(FileSystem.AppDataDirectory, "error.log");
            var sb = new StringBuilder();
            sb.AppendLine("========================================");
            sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine($"Work: {workName}");
            sb.AppendLine(ex.ToString());
            sb.AppendLine();

            await File.AppendAllTextAsync(filePath, sb.ToString());
            Debug.WriteLine(ex);
        }
        catch
        {
            // 로그 저장 실패 때문에 앱이 종료되지 않도록 무시합니다.
        }
    }
}
