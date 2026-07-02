using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace BS.IME.Assistant.Services;

public sealed class TrayService : IDisposable
{
    private readonly Logger _logger;
    private NotifyIcon? _notifyIcon;
    private ToolStripMenuItem? _statusItem;
    private ToolStripMenuItem? _foregroundItem;
    private ToolStripMenuItem? _currentImeItem;
    private ToolStripMenuItem? _toggleItem;
    private ToolStripMenuItem? _showFloatingItem;
    private ToolStripMenuItem? _hideFloatingItem;
    private ToolStripMenuItem? _hotkeyFailureItem;
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
    public event Action? OpenLogsRequested;
    public event Action? ExitRequested;

    public void Initialize(bool enabled, bool hotkeyFailed)
    {
        var menu = new ContextMenuStrip();
        _statusItem = new ToolStripMenuItem { Enabled = false };
        _foregroundItem = new ToolStripMenuItem { Enabled = false };
        _currentImeItem = new ToolStripMenuItem { Enabled = false };
        _hotkeyFailureItem = new ToolStripMenuItem("热键注册失败") { Enabled = false, Visible = hotkeyFailed };
        _toggleItem = new ToolStripMenuItem();
        _showFloatingItem = new ToolStripMenuItem("显示悬浮窗");
        _hideFloatingItem = new ToolStripMenuItem("隐藏悬浮窗");

        _toggleItem.Click += (_, _) => ToggleEnabledRequested?.Invoke();
        _showFloatingItem.Click += (_, _) => ShowFloatingRequested?.Invoke();
        _hideFloatingItem.Click += (_, _) => HideFloatingRequested?.Invoke();
        var switchChineseItem = new ToolStripMenuItem("切换到中文");
        switchChineseItem.Click += (_, _) => SwitchChineseRequested?.Invoke();
        var switchEnglishItem = new ToolStripMenuItem("切换到英文");
        switchEnglishItem.Click += (_, _) => SwitchEnglishRequested?.Invoke();
        var openSettingsItem = new ToolStripMenuItem("打开配置文件");
        openSettingsItem.Click += (_, _) => OpenSettingsRequested?.Invoke();
        var openLogsItem = new ToolStripMenuItem("打开日志目录");
        openLogsItem.Click += (_, _) => OpenLogsRequested?.Invoke();
        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => ExitRequested?.Invoke();

        menu.Items.AddRange([
            _statusItem,
            _foregroundItem,
            _currentImeItem,
            _hotkeyFailureItem,
            new ToolStripSeparator(),
            _toggleItem,
            _showFloatingItem,
            _hideFloatingItem,
            switchChineseItem,
            switchEnglishItem,
            new ToolStripSeparator(),
            openSettingsItem,
            openLogsItem,
            new ToolStripSeparator(),
            exitItem
        ]);

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "BS IME Assistant",
            Visible = true,
            ContextMenuStrip = menu
        };

        Update(enabled, "", "未知", hotkeyFailed);
        _logger.Info("Tray icon initialized.");
    }

    public void Update(bool enabled, string processName, string currentIme, bool hotkeyFailed)
    {
        if (_statusItem is null || _foregroundItem is null || _currentImeItem is null || _toggleItem is null)
        {
            return;
        }

        _statusItem.Text = enabled ? "状态：运行中" : "状态：已暂停";
        _foregroundItem.Text = $"当前前台软件：{(string.IsNullOrWhiteSpace(processName) ? "未知" : processName)}";
        _currentImeItem.Text = $"当前输入法：{currentIme}";
        _toggleItem.Text = enabled ? "暂停自动切换" : "启用自动切换";
        if (_hotkeyFailureItem is not null)
        {
            _hotkeyFailureItem.Visible = hotkeyFailed;
        }
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
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            _logger.Info("Tray icon disposed.");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to dispose tray icon.", ex);
        }

        _disposed = true;
    }
}
