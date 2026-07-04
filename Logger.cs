namespace AutoPowerMode;

public static class Logger
{
    private static readonly object LockObject = new();

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
        return $"日志位置：{LogFilePath}{Environment.NewLine}便携目录日志：{PortableLogFilePath}";
    }

    private static void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";

        lock (LockObject)
        {
            TryAppend(LogDirectory, LogFilePath, line);
            TryAppend(PortableLogDirectory, PortableLogFilePath, line);
        }
    }

    private static void TryAppend(string directory, string path, string line)
    {
        try
        {
            Directory.CreateDirectory(directory);
            File.AppendAllText(path, line);
        }
        catch
        {
            // Logging must never crash the tray application.
        }
    }
}
