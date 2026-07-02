using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
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
    private readonly Font _menuFont = new("Microsoft YaHei UI", 10.5f, FontStyle.Regular);

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
        ConfigureMenu(menu);
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
        StyleMenuItems(menu.Items);

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

    private static void ConfigureMenu(ContextMenuStrip menu)
    {
        menu.RenderMode = ToolStripRenderMode.Professional;
        menu.Renderer = new DarkMenuRenderer();
        menu.BackColor = DarkMenuRenderer.Background;
        menu.ForeColor = Color.White;
        menu.ShowImageMargin = true;
        menu.ShowCheckMargin = true;
        menu.Padding = new Padding(8, 8, 8, 8);
        menu.Margin = Padding.Empty;
        menu.Opened += (_, _) => ApplyRoundedRegion(menu, 8);
        menu.Closed += (_, _) =>
        {
            var region = menu.Region;
            menu.Region = null;
            region?.Dispose();
        };
    }

    private void StyleMenuItems(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            item.Font = _menuFont;
            item.ForeColor = item.Enabled ? Color.White : Color.FromArgb(185, 185, 185);

            if (item is ToolStripMenuItem menuItem)
            {
                menuItem.AutoSize = false;
                menuItem.Height = 34;
                menuItem.Width = 248;
                menuItem.Padding = new Padding(12, 0, 12, 0);
            }

            if (item is ToolStripSeparator separator)
            {
                separator.AutoSize = false;
                separator.Height = 10;
                separator.Margin = new Padding(4, 3, 4, 3);
            }
        }
    }

    private static void ApplyRoundedRegion(ContextMenuStrip menu, int radius)
    {
        if (menu.Width <= 0 || menu.Height <= 0)
        {
            return;
        }

        menu.Region?.Dispose();
        using var path = CreateRoundRectanglePath(new Rectangle(0, 0, menu.Width, menu.Height), radius);
        menu.Region = new Region(path);
    }

    private static GraphicsPath CreateRoundRectanglePath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter - 1;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter - 1;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();

        return path;
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

            _menuFont.Dispose();

            _logger.Info("Tray icon disposed.");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to dispose tray icon.", ex);
        }

        _disposed = true;
    }

    private sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public static readonly Color Background = Color.FromArgb(38, 38, 38);
        private static readonly Color Border = Color.FromArgb(72, 72, 72);
        private static readonly Color Separator = Color.FromArgb(58, 58, 58);
        private static readonly Color Hover = Color.FromArgb(54, 54, 54);
        private static readonly Color Pressed = Color.FromArgb(62, 62, 62);
        private static readonly Color Text = Color.White;
        private static readonly Color DisabledText = Color.FromArgb(178, 178, 178);

        public DarkMenuRenderer() : base(new DarkColorTable())
        {
            RoundedEdges = true;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(Background);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Border);
            var rect = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = CreateRoundRectanglePath(rect, 8);
            e.Graphics.DrawPath(pen, path);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item is not ToolStripMenuItem item)
            {
                return;
            }

            Color color = item.Pressed ? Pressed : item.Selected ? Hover : Background;
            using var brush = new SolidBrush(color);
            var rect = new Rectangle(5, 2, item.Width - 10, item.Height - 4);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = CreateRoundRectanglePath(rect, 5);
            e.Graphics.FillPath(brush, path);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Text : DisabledText;
            e.TextFormat |= TextFormatFlags.VerticalCenter;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(Separator);
            int y = e.Item.Height / 2;
            e.Graphics.DrawLine(pen, 12, y, e.Item.Width - 12, y);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            TextRenderer.DrawText(e.Graphics, "✓", e.Item.Font, e.ImageRectangle, Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Text;
            base.OnRenderArrow(e);
        }
    }

    private sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(54, 54, 54);
        public override Color MenuItemSelectedGradientBegin => MenuItemSelected;
        public override Color MenuItemSelectedGradientEnd => MenuItemSelected;
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(62, 62, 62);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(62, 62, 62);
        public override Color ToolStripDropDownBackground => DarkMenuRenderer.Background;
        public override Color ImageMarginGradientBegin => DarkMenuRenderer.Background;
        public override Color ImageMarginGradientMiddle => DarkMenuRenderer.Background;
        public override Color ImageMarginGradientEnd => DarkMenuRenderer.Background;
        public override Color MenuBorder => Color.FromArgb(72, 72, 72);
        public override Color SeparatorDark => Color.FromArgb(58, 58, 58);
        public override Color SeparatorLight => Color.FromArgb(58, 58, 58);
    }
}
