using System.IO;
using System.Text;

namespace EduAnalytics.UI.Services;

public interface IAppLogService
{
    string LogDirectory { get; }
    string CurrentLogFilePath { get; }
    void Info(string category, string message);
    void Warning(string category, string message);
    void Error(string category, string message, Exception? exception = null);
}

public sealed class AppLogService : IAppLogService
{
    private readonly object _sync = new();

    public AppLogService()
    {
        LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EduAnalytics",
            "Logs");
    }

    public string LogDirectory { get; }

    public string CurrentLogFilePath =>
        Path.Combine(LogDirectory, $"eduanalytics-{DateTime.Now:yyyyMMdd}.log");

    public void Info(string category, string message)
        => Write("INFO", category, message);

    public void Warning(string category, string message)
        => Write("WARN", category, message);

    public void Error(string category, string message, Exception? exception = null)
    {
        var details = exception == null
            ? message
            : $"{message}{Environment.NewLine}{exception}";

        Write("ERROR", category, details);
    }

    private void Write(string level, string category, string message)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {category} - {message}{Environment.NewLine}";

            lock (_sync)
            {
                File.AppendAllText(CurrentLogFilePath, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never break the UI flow.
        }
    }
}
