using System.Windows.Threading;
using BS.IME.Assistant.Models;

namespace BS.IME.Assistant.Services;

public sealed class AppController : IDisposable
{
    private readonly MainWindow _window;
    private readonly Logger _logger;
    private readonly SettingsService _settingsService;
    private readonly ImeService _imeService;
    private readonly ActiveWindowService _activeWindowService;
    private readonly HotkeyService _hotkeyService;
    private readonly TrayService _trayService;
    private readonly DispatcherTimer _timer;
    private AppSettings _settings;
    private ActiveWindowInfo? _lastWindow;
    private nint _lastSwitchWindowHandle;
    private string _lastSwitchTarget = "";
    private DateTimeOffset _lastSwitchAttemptAt = DateTimeOffset.MinValue;
    private bool _disposed;

    public AppController(MainWindow window)
    {
        _window = window;
        var appDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BS-IME-Assistant");
        _logger = new Logger(appDirectory);
        _settingsService = new SettingsService(_logger);
        _imeService = new ImeService(_logger);
        _activeWindowService = new ActiveWindowService(_logger);
        _hotkeyService = new HotkeyService(_logger);
        _trayService = new TrayService(_logger);
        _settings = AppSettings.CreateDefault();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => Tick();
    }

    public Logger Logger => _logger;

    public void Start(nint windowHandle)
    {
        _logger.Info("Program started.");
        _settings = _settingsService.LoadOrCreate();
        var languages = _imeService.GetInstalledInputLanguages();
        _imeService.EnsureTargets(_settings, languages, _settingsService);
        _window.UpdateInfo(_settings.Enabled, _settings.TargetChineseHkl, _settings.TargetEnglishHkl, _settingsService.SettingsPath, _logger.LogPath);

        _hotkeyService.HotkeyPressed += kind => ManualSwitch(kind, "hotkey");
        _hotkeyService.Register(windowHandle, _settings.Hotkeys.SwitchEnglish, _settings.Hotkeys.SwitchChinese);

        _trayService.ToggleEnabledRequested += ToggleEnabled;
        _trayService.SwitchChineseRequested += () => ManualSwitch("zh", "tray");
        _trayService.SwitchEnglishRequested += () => ManualSwitch("en", "tray");
        _trayService.OpenSettingsRequested += () => TrayService.OpenPath(_settingsService.SettingsPath, _logger);
        _trayService.OpenLogsRequested += () => TrayService.OpenPath(_logger.LogDirectory, _logger);
        _trayService.ExitRequested += () => System.Windows.Application.Current.Shutdown();
        _trayService.Initialize(_settings.Enabled, _hotkeyService.HasRegistrationFailure);

        _timer.Start();
        Tick();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _timer.Stop();
            _hotkeyService.Dispose();
            _trayService.Dispose();
            _logger.Info("Program exited.");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed during application shutdown.", ex);
        }

        _disposed = true;
    }

    private void Tick()
    {
        try
        {
            var window = _activeWindowService.GetForegroundWindowInfo();
            var currentIme = window is null ? "未知" : _imeService.GetCurrentImeKind(window.ThreadId, _settings);
            _trayService.Update(_settings.Enabled, window?.ProcessName ?? "", currentIme, _hotkeyService.HasRegistrationFailure);

            if (window is null)
            {
                return;
            }

            if (_lastWindow is null || _lastWindow.Handle != window.Handle || !string.Equals(_lastWindow.ProcessName, window.ProcessName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Info($"Active window changed: {window.ProcessName}, pid={window.ProcessId}, title={window.Title}");
                _lastWindow = window;
                ResetSwitchAttempt();
            }

            if (!_settings.Enabled)
            {
                return;
            }

            var profile = _settings.Profiles.FirstOrDefault(x =>
                x.SwitchOnActivate &&
                string.Equals(NormalizeProcessName(x.ProcessName), NormalizeProcessName(window.ProcessName), StringComparison.OrdinalIgnoreCase));

            if (profile is null)
            {
                return;
            }

            var target = profile.DefaultIme.Equals("zh", StringComparison.OrdinalIgnoreCase) ? "zh" : "en";
            var targetHkl = target == "zh" ? _settings.TargetChineseHkl : _settings.TargetEnglishHkl;
            if (ImeService.HklEquals(targetHkl, GetCurrentHkl(window.ThreadId)))
            {
                ResetSwitchAttempt();
                return;
            }

            if (!CanRetrySwitch(window.Handle, target))
            {
                return;
            }

            if (_imeService.SwitchTo(target, window.Handle, _settings, $"auto:{profile.Name}"))
            {
                RecordSwitchAttempt(window.Handle, target);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Timer tick failed.", ex);
        }
    }

    private void ManualSwitch(string kind, string source)
    {
        try
        {
            var window = _activeWindowService.GetForegroundWindowInfo();
            if (window is null)
            {
                _logger.Warn($"Manual IME switch ignored because foreground window is unavailable. source={source}");
                return;
            }

            _logger.Info($"Manual IME switch requested: {kind}, source={source}");
            _imeService.SwitchTo(kind, window.Handle, _settings, $"manual:{source}");
            RecordSwitchAttempt(window.Handle, kind);
        }
        catch (Exception ex)
        {
            _logger.Error("Manual IME switch failed.", ex);
        }
    }

    private void ToggleEnabled()
    {
        _settings.Enabled = !_settings.Enabled;
        _settingsService.Save(_settings);
        _window.UpdateInfo(_settings.Enabled, _settings.TargetChineseHkl, _settings.TargetEnglishHkl, _settingsService.SettingsPath, _logger.LogPath);
        _logger.Info(_settings.Enabled ? "Auto switch enabled." : "Auto switch paused.");
    }

    private static string NormalizeProcessName(string processName) =>
        processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? processName : $"{processName}.exe";

    private bool CanRetrySwitch(nint windowHandle, string target)
    {
        if (_lastSwitchWindowHandle != windowHandle || !string.Equals(_lastSwitchTarget, target, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return DateTimeOffset.Now - _lastSwitchAttemptAt >= TimeSpan.FromSeconds(1);
    }

    private void RecordSwitchAttempt(nint windowHandle, string target)
    {
        _lastSwitchWindowHandle = windowHandle;
        _lastSwitchTarget = target;
        _lastSwitchAttemptAt = DateTimeOffset.Now;
    }

    private void ResetSwitchAttempt()
    {
        _lastSwitchWindowHandle = nint.Zero;
        _lastSwitchTarget = "";
        _lastSwitchAttemptAt = DateTimeOffset.MinValue;
    }

    private string GetCurrentHkl(uint threadId)
    {
        try
        {
            return ImeService.ToHklString(Native.Win32.GetKeyboardLayout(threadId));
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to get current HKL.", ex);
            return "";
        }
    }
}
