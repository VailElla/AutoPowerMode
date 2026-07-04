using System.Text;
using System.Text.RegularExpressions;

namespace AutoPowerMode;

public static class Logger
{
    internal const long MaxLogFileBytes = 1024 * 1024;
    internal const int MaxHistoryLogFiles = 3;

    private static readonly object LockObject = new();
    private static readonly Regex WindowsAppDataPathRegex = new(
        @"(?i)[A-Z]:[\\/]+Users[\\/]+[^\\/]+[\\/]+AppData[\\/]+Roaming[\\/]+AutoPowerMode",
        RegexOptions.Compiled);

    public static string AppDataDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AutoPowerMode");

    public static string LogDirectory => Path.Combine(AppDataDirectory, "logs");

    public static string LogFilePath => Path.Combine(LogDirectory, "app.log");

    public static string PortableLogDirectory => Path.Combine(AppContext.BaseDirectory, "logs");

    public static string PortableLogFilePath => Path.Combine(PortableLogDirectory, "app.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? exception = null)
    {
        if (exception is null)
        {
            Write("ERROR", message);
            return;
        }

        Write("ERROR", $"{message} {exception}");
    }

    public static string GetLogLocationMessage()
    {
        return $"日志位置：{SanitizeMessage(LogFilePath)}{Environment.NewLine}便携目录日志：{SanitizeMessage(PortableLogFilePath)}";
    }

    private static void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {SanitizeMessage(message)}{Environment.NewLine}";

        lock (LockObject)
        {
            TryAppend(LogDirectory, LogFilePath, line);
            TryAppend(PortableLogDirectory, PortableLogFilePath, line);
        }
    }

    internal static void TryAppend(string directory, string path, string line)
    {
        try
        {
            Directory.CreateDirectory(directory);
            RotateIfNeeded(path, Encoding.UTF8.GetByteCount(line));
            File.AppendAllText(path, line);
        }
        catch
        {
            // Logging must never crash the tray application.
        }
    }

    internal static string SanitizeMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return message;
        }

        var sanitized = message;
        sanitized = ReplacePath(sanitized, AppDataDirectory, @"%AppData%\AutoPowerMode");
        sanitized = WindowsAppDataPathRegex.Replace(sanitized, @"%AppData%\AutoPowerMode");
        sanitized = ReplacePath(sanitized, AppContext.BaseDirectory, "<AppDirectory>");
        return sanitized;
    }

    private static void RotateIfNeeded(string path, int incomingByteCount)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var currentSize = new FileInfo(path).Length;
        if (currentSize + incomingByteCount <= MaxLogFileBytes)
        {
            return;
        }

        Rotate(path);
    }

    private static void Rotate(string path)
    {
        var directory = Path.GetDirectoryName(path);
        var baseName = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(baseName))
        {
            return;
        }

        var oldestPath = GetArchivePath(directory, baseName, extension, MaxHistoryLogFiles);
        if (File.Exists(oldestPath))
        {
            File.Delete(oldestPath);
        }

        for (var index = MaxHistoryLogFiles - 1; index >= 1; index--)
        {
            var sourcePath = GetArchivePath(directory, baseName, extension, index);
            if (!File.Exists(sourcePath))
            {
                continue;
            }

            var targetPath = GetArchivePath(directory, baseName, extension, index + 1);
            File.Move(sourcePath, targetPath, overwrite: true);
        }

        var firstArchivePath = GetArchivePath(directory, baseName, extension, 1);
        File.Move(path, firstArchivePath, overwrite: true);
    }

    private static string GetArchivePath(string directory, string baseName, string extension, int index)
    {
        return Path.Combine(directory, $"{baseName}.{index}{extension}");
    }

    private static string ReplacePath(string value, string path, string replacement)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return value;
        }

        var result = value;
        foreach (var variant in GetPathVariants(path))
        {
            result = result.Replace(variant, replacement, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static IEnumerable<string> GetPathVariants(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (trimmed.Length == 0)
        {
            yield break;
        }

        yield return trimmed;
        yield return trimmed.Replace('\\', '/');
        yield return trimmed.Replace('/', '\\');
    }
}
