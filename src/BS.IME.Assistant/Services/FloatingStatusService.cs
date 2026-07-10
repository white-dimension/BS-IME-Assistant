using BS.IME.Assistant.Models;
using BS.IME.Assistant.Views;

namespace BS.IME.Assistant.Services;

public sealed class FloatingStatusService : IDisposable
{
    private readonly Logger _logger;
    private readonly SettingsService _settingsService;
    private FloatingStatusWindow? _window;
    private AppSettings? _settings;
    private bool _disposed;

    public FloatingStatusService(Logger logger, SettingsService settingsService)
    {
        _logger = logger;
        _settingsService = settingsService;
    }

    public event Action? CadPromptAccepted;
    public event Action? CadPromptDismissed;
    public event Action? SwitchChineseRequested;
    public event Action? SwitchEnglishRequested;

    public void Initialize(AppSettings settings)
    {
        _settings = settings;
        EnsureWindow();

        if (settings.FloatingStatus.Enabled)
        {
            Show();
        }
        else
        {
            Hide();
        }
    }

    public void Update(string currentIme)
    {
        if (_settings?.FloatingStatus.Enabled != true)
        {
            return;
        }

        EnsureWindow();
        RestoreConfiguredSize();
        _window?.UpdateStatus(currentIme);
    }

    public void UpdateCustom(string badge, string text, string color)
    {
        if (_settings?.FloatingStatus.Enabled != true)
        {
            return;
        }

        EnsureWindow();
        RestoreConfiguredSize();
        _window?.UpdateCustomStatus(badge, text, color);
    }

    public void ShowCadPrompt()
    {
        if (_settings?.FloatingStatus.Enabled != true)
        {
            return;
        }

        EnsureWindow();
        if (_window is null)
        {
            return;
        }

        _window.Width = Math.Max(_settings.FloatingStatus.Width, 260);
        _window.Height = Math.Max(_settings.FloatingStatus.Height, 64);
        _window.ClampToScreen();
        _window.ShowCadPrompt();
    }

    public void Show()
    {
        if (_settings is null)
        {
            return;
        }

        _settings.FloatingStatus.Enabled = true;
        EnsureWindow();
        _window?.Show();
        _window?.UpdateStatus("未知");
        _settingsService.Save(_settings);
        _logger.Info("Floating status window shown.");
    }

    public void Hide()
    {
        if (_settings is null)
        {
            return;
        }

        _settings.FloatingStatus.Enabled = false;
        _window?.Hide();
        _settingsService.Save(_settings);
        _logger.Info("Floating status window hidden.");
    }

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;
        EnsureWindow();

        if (_window is not null)
        {
            _window.Width = settings.FloatingStatus.Width;
            _window.Height = settings.FloatingStatus.Height;
            _window.Left = settings.FloatingStatus.Left;
            _window.Top = settings.FloatingStatus.Top;
            _window.Topmost = settings.FloatingStatus.Topmost;
            _window.ClampToScreen();
        }

        if (settings.FloatingStatus.Enabled)
        {
            _window?.Show();
        }
        else
        {
            _window?.Hide();
        }
    }

    public void ResetPositionToBottomRight()
    {
        if (_settings is null)
        {
            return;
        }

        EnsureWindow();
        if (_window is null)
        {
            return;
        }

        var area = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(_window).Handle).WorkingArea;
        var source = System.Windows.PresentationSource.FromVisual(_window);
        var transform = source?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;
        var bottomRight = transform.Transform(new System.Windows.Point(area.Right, area.Bottom));

        _window.Left = bottomRight.X - _settings.FloatingStatus.Width - 16;
        _window.Top = bottomRight.Y - _settings.FloatingStatus.Height - 16;
        _window.ClampToScreen();
        SaveWindowPosition();
    }

    public void ClampPositionToScreen()
    {
        EnsureWindow();
        _window?.ClampToScreen();
        SaveWindowPosition();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _window?.Close();
            _logger.Info("Floating status window disposed.");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to dispose floating status window.", ex);
        }

        _disposed = true;
    }

    private void EnsureWindow()
    {
        if (_settings is null || _window is not null)
        {
            return;
        }

        var floating = _settings.FloatingStatus;
        _window = new FloatingStatusWindow
        {
            Width = floating.Width,
            Height = floating.Height,
            Left = floating.Left,
            Top = floating.Top,
            Topmost = floating.Topmost
        };
        _window.ClampToScreen();
        _window.PromptAccepted += () => CadPromptAccepted?.Invoke();
        _window.PromptDismissed += () => CadPromptDismissed?.Invoke();
        _window.SwitchChineseRequested += () => SwitchChineseRequested?.Invoke();
        _window.SwitchEnglishRequested += () => SwitchEnglishRequested?.Invoke();
        _window.PositionChangedByUser += (left, top) =>
        {
            if (_settings is null)
            {
                return;
            }

            _settings.FloatingStatus.Left = left;
            _settings.FloatingStatus.Top = top;
            _settingsService.Save(_settings);
            _logger.Info($"Floating status position saved: left={left}, top={top}");
        };
    }

    private void RestoreConfiguredSize()
    {
        if (_settings is null || _window is null)
        {
            return;
        }

        _window.Width = _settings.FloatingStatus.Width;
        _window.Height = _settings.FloatingStatus.Height;
        _window.ClampToScreen();
    }

    private void SaveWindowPosition()
    {
        if (_settings is null || _window is null)
        {
            return;
        }

        _settings.FloatingStatus.Left = _window.Left;
        _settings.FloatingStatus.Top = _window.Top;
        _settingsService.Save(_settings);
    }
}
