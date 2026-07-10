using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using BS.IME.Assistant.Views;

namespace BS.IME.Assistant.Services;

public sealed class TrayService : IDisposable
{
    private readonly Logger _logger;
    private NotifyIcon? _notifyIcon;
    private TrayMenuWindow? _menuWindow;
    private bool _enabled;
    private string _processName = "";
    private string _currentIme = "未知";
    private bool _disposed;

    public TrayService(Logger logger)
    {
        _logger = logger;
    }

    public event Action? ToggleEnabledRequested;
    public event Action? SwitchChineseRequested;
    public event Action? SwitchEnglishRequested;
    public event Action? ShowFloatingRequested;
    public event Action? HideFloatingRequested;
    public event Action? OpenSettingsRequested;
    public event Action? ResetFloatingRequested;
    public event Action? RestartRequested;
    public event Action? ExitRequested;

    public void Initialize(bool enabled)
    {
        _enabled = enabled;
        EnsureMenuWindow();

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "BS IME Assistant",
            Visible = true
        };
        _notifyIcon.MouseUp += OnNotifyIconMouseUp;

        Update(enabled, "", "未知");
        _logger.Info("Tray icon initialized.");
    }

    public void Update(bool enabled, string processName, string currentIme)
    {
        _enabled = enabled;
        _processName = processName;
        _currentIme = currentIme;

        _menuWindow?.UpdateState(_enabled, _processName, _currentIme);
    }

    public static void OpenPath(string path, Logger logger)
    {
        try
        {
            var target = Directory.Exists(path) ? path : Path.GetFullPath(path);
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to open path: {path}", ex);
        }
    }

    private void OnNotifyIconMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        EnsureMenuWindow();
        if (_menuWindow is null)
        {
            return;
        }

        _menuWindow.UpdateState(_enabled, _processName, _currentIme);
        _menuWindow.ShowAt(Screen.FromPoint(Cursor.Position), Cursor.Position);
    }

    private void EnsureMenuWindow()
    {
        if (_menuWindow is not null)
        {
            return;
        }

        _menuWindow = new TrayMenuWindow();
        _menuWindow.ToggleEnabledRequested += () => ToggleEnabledRequested?.Invoke();
        _menuWindow.SwitchChineseRequested += () => SwitchChineseRequested?.Invoke();
        _menuWindow.SwitchEnglishRequested += () => SwitchEnglishRequested?.Invoke();
        _menuWindow.ShowFloatingRequested += () => ShowFloatingRequested?.Invoke();
        _menuWindow.HideFloatingRequested += () => HideFloatingRequested?.Invoke();
        _menuWindow.OpenSettingsRequested += () => OpenSettingsRequested?.Invoke();
        _menuWindow.ResetFloatingRequested += () => ResetFloatingRequested?.Invoke();
        _menuWindow.RestartRequested += () => RestartRequested?.Invoke();
        _menuWindow.ExitRequested += () => ExitRequested?.Invoke();
        _menuWindow.UpdateState(_enabled, _processName, _currentIme);
    }

    private Icon LoadTrayIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(iconPath))
            {
                return new Icon(iconPath);
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to load tray icon: {ex.Message}");
        }

        return SystemIcons.Application;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (_notifyIcon is not null)
            {
                _notifyIcon.MouseUp -= OnNotifyIconMouseUp;
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            _menuWindow?.Close();

            _logger.Info("Tray icon disposed.");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to dispose tray icon.", ex);
        }

        _disposed = true;
    }
}
