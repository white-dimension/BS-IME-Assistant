using System.Diagnostics;
using System.Text;
using BS.IME.Assistant.Native;

namespace BS.IME.Assistant.Services;

public sealed record ActiveWindowInfo(nint Handle, uint ThreadId, int ProcessId, string ProcessName, string Title);

public sealed class ActiveWindowService
{
    private readonly Logger _logger;

    public ActiveWindowService(Logger logger)
    {
        _logger = logger;
    }

    public ActiveWindowInfo? GetForegroundWindowInfo()
    {
        try
        {
            var handle = Win32.GetForegroundWindow();
            if (handle == nint.Zero)
            {
                _logger.Warn("Foreground window handle is empty.");
                return null;
            }

            var threadId = Win32.GetWindowThreadProcessId(handle, out var pid);
            if (pid == 0)
            {
                _logger.Warn("Foreground window process id is empty.");
                return null;
            }

            var title = GetWindowTitle(handle);
            var processName = "";
            try
            {
                using var process = Process.GetProcessById((int)pid);
                processName = process.ProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? process.ProcessName
                    : $"{process.ProcessName}.exe";
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to read process name for pid {pid}.", ex);
            }

            return new ActiveWindowInfo(handle, threadId, (int)pid, processName, title);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to get foreground window.", ex);
            return null;
        }
    }

    private string GetWindowTitle(nint handle)
    {
        try
        {
            var length = Math.Max(Win32.GetWindowTextLength(handle), 0);
            var builder = new StringBuilder(length + 1);
            _ = Win32.GetWindowText(handle, builder, builder.Capacity);
            return builder.ToString();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to get window title.", ex);
            return "";
        }
    }
}
