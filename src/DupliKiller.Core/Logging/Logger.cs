namespace DupliKiller.Core.Logging;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public static class Logger
{
    private static readonly string LogPath;
    private static readonly object _lock = new();
    private static readonly List<string> _recentLogs = new();
    private const int MaxRecentLogs = 1000;

    static Logger()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(appData, "DupliKiller", "Logs");
            Directory.CreateDirectory(dir);
            LogPath = Path.Combine(dir, $"DupliKiller_{DateTime.Now:yyyyMMdd}.log");
            Info("Logger initialized");
        }
        catch
        {
            LogPath = Path.Combine(Path.GetTempPath(), $"DupliKiller_{DateTime.Now:yyyyMMdd}.log");
        }
    }

    public static string GetLogPath() => LogPath;

    public static string GetRecentLogs()
    {
        lock (_lock)
        {
            return string.Join(Environment.NewLine, _recentLogs);
        }
    }

    public static void Debug(string message) => Write(LogLevel.Debug, message);
    public static void Info(string message) => Write(LogLevel.Info, message);
    public static void Warning(string message) => Write(LogLevel.Warning, message);
    public static void Error(string message) => Write(LogLevel.Error, message);

    private static void Write(LogLevel level, string message)
    {
        try
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level,-7}] {message}";
            lock (_lock)
            {
                File.AppendAllText(LogPath, line + Environment.NewLine);
                _recentLogs.Add(line);
                if (_recentLogs.Count > MaxRecentLogs)
                    _recentLogs.RemoveRange(0, _recentLogs.Count - MaxRecentLogs);
            }
        }
        catch
        {
        }
    }
}
