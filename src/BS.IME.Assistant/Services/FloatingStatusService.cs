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
        _window?.UpdateStatus(currentIme);
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
}
