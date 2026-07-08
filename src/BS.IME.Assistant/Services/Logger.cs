namespace BS.IME.Assistant.Services;

public sealed class Logger
{
    private const long MaxLogBytes = 3 * 1024 * 1024;
    private readonly object _gate = new();

    public Logger(string appDirectory)
    {
        LogDirectory = AppPaths.LogDirectory;
        LogPath = AppPaths.LogFile;
        Directory.CreateDirectory(LogDirectory);
    }

    public string LogDirectory { get; }
    public string LogPath { get; }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");

    private void Write(string level, string message)
    {
        lock (_gate)
        {
            try
            {
                RotateIfNeeded();
                File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Logging must never crash the tray app.
            }
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(LogPath))
        {
            return;
        }

        var fileInfo = new FileInfo(LogPath);
        if (fileInfo.Length <= MaxLogBytes)
        {
            return;
        }

        var oldPath = Path.Combine(LogDirectory, "debug.old.log");
        if (File.Exists(oldPath))
        {
            File.Delete(oldPath);
        }

        File.Move(LogPath, oldPath);
    }
}
