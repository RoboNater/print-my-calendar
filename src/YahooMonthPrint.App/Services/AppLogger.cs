using System.IO;
using System.Reflection;

namespace YahooMonthPrint.App.Services;

public interface IAppLogger
{
    void Log(string category, string status, string? resourceId = null, Exception? exception = null);
}

public sealed class RotatingFileAppLogger : IAppLogger
{
    private const long MaximumBytes = 512 * 1024;
    private readonly object sync = new();
    private readonly string directory;
    private readonly string path;

    public RotatingFileAppLogger(string? applicationDataPath = null)
    {
        directory = Path.Combine(applicationDataPath ?? AppStoragePaths.Root, "Logs");
        path = Path.Combine(directory, "YahooMonthPrint.log");
    }

    public void Log(string category, string status, string? resourceId = null, Exception? exception = null)
    {
        var safeCategory = Sanitize(category);
        var safeStatus = Sanitize(status);
        var safeResource = Sanitize(resourceId ?? "-");
        var exceptionType = exception?.GetType().Name ?? "-";
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        var line = $"{DateTimeOffset.Now:O}\t{version}\t{safeCategory}\t{safeStatus}\t{safeResource}\t{exceptionType}{Environment.NewLine}";

        lock (sync)
        {
            Directory.CreateDirectory(directory);
            RotateIfRequired();
            File.AppendAllText(path, line);
        }
    }

    private void RotateIfRequired()
    {
        if (!File.Exists(path) || new FileInfo(path).Length < MaximumBytes)
        {
            return;
        }

        File.Delete(path + ".3");
        MoveIfPresent(path + ".2", path + ".3");
        MoveIfPresent(path + ".1", path + ".2");
        MoveIfPresent(path, path + ".1");
    }

    private static void MoveIfPresent(string source, string destination)
    {
        if (File.Exists(source))
        {
            File.Move(source, destination, true);
        }
    }

    private static string Sanitize(string value)
    {
        var flattened = value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ');
        var sensitiveIndex = flattened.IndexOf("Authorization", StringComparison.OrdinalIgnoreCase);
        return sensitiveIndex < 0
            ? flattened
            : flattened[..sensitiveIndex] + "[redacted-header]";
    }
}

public sealed class NullAppLogger : IAppLogger
{
    public void Log(string category, string status, string? resourceId = null, Exception? exception = null)
    {
    }
}
