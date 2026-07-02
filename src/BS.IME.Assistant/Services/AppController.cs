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
    private readonly FloatingStatusService _floatingStatusService;
    private readonly PipeCommandService _pipeCommandService;
    private readonly DispatcherTimer _timer;
    private AppSettings _settings;
    private ActiveWindowInfo? _lastWindow;
    private nint _lastSwitchWindowHandle;
    private string _lastSwitchTarget = "";
    private DateTimeOffset _lastSwitchAttemptAt = DateTimeOffset.MinValue;
    private string _cadPreferredIme = "";
    private string _cadMode = "";
    private bool _cadPluginConnected;
    private bool _cadPromptDismissedThisSession;
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
        _floatingStatusService = new FloatingStatusService(_logger, _settingsService);
        _pipeCommandService = new PipeCommandService(_logger);
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
        _floatingStatusService.Initialize(_settings);
        _floatingStatusService.CadPromptAccepted += EnableCadIntegration;
        _floatingStatusService.CadPromptDismissed += () => _cadPromptDismissedThisSession = true;

        _hotkeyService.HotkeyPressed += kind => ManualSwitch(kind, "hotkey");
        _hotkeyService.Register(windowHandle, _settings.Hotkeys.SwitchEnglish, _settings.Hotkeys.SwitchChinese);
        _pipeCommandService.CommandReceived += OnPipeCommandReceived;
        _pipeCommandService.Start();

        _trayService.ToggleEnabledRequested += ToggleEnabled;
        _trayService.ShowFloatingRequested += _floatingStatusService.Show;
        _trayService.HideFloatingRequested += _floatingStatusService.Hide;
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
            _pipeCommandService.Dispose();
            _hotkeyService.Dispose();
            _floatingStatusService.Dispose();
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
            UpdateFloatingStatus(window, currentIme);

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

            if (TryApplyCadPluginRequest(window))
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

    private void OnPipeCommandReceived(Models.PipeImeCommand command)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (!command.Source.Equals("AutoCAD", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _cadPluginConnected = !command.Event.Equals("PluginDisconnected", StringComparison.OrdinalIgnoreCase);

            if (command.Event.Equals("PluginReady", StringComparison.OrdinalIgnoreCase))
            {
                _cadMode = "CAD 插件已连接";
                if (!_settings.CadIntegration.Enabled)
                {
                    _floatingStatusService.ShowCadPrompt();
                }
                return;
            }

            if (!_settings.CadIntegration.Enabled)
            {
                _floatingStatusService.ShowCadPrompt();
                _logger.Info("CAD plugin command ignored because CAD integration is not enabled.");
                return;
            }

            _cadMode = string.IsNullOrWhiteSpace(command.Mode) ? command.Event : command.Mode;
            _cadPreferredIme = command.PreferredIme.Equals("zh", StringComparison.OrdinalIgnoreCase) ? "zh" : "en";

            var window = _activeWindowService.GetForegroundWindowInfo();
            if (window is not null && IsAutoCad(window.ProcessName))
            {
                _imeService.SwitchTo(_cadPreferredIme, window.Handle, _settings, $"cad-plugin:{command.Event}");
                RecordSwitchAttempt(window.Handle, _cadPreferredIme);
            }
        });
    }

    private bool TryApplyCadPluginRequest(ActiveWindowInfo window)
    {
        if (!_settings.CadIntegration.Enabled || !_cadPluginConnected || string.IsNullOrWhiteSpace(_cadPreferredIme) || !IsAutoCad(window.ProcessName))
        {
            return false;
        }

        var targetHkl = _cadPreferredIme == "zh" ? _settings.TargetChineseHkl : _settings.TargetEnglishHkl;
        if (ImeService.HklEquals(targetHkl, GetCurrentHkl(window.ThreadId)))
        {
            ResetSwitchAttempt();
            return true;
        }

        if (!CanRetrySwitch(window.Handle, _cadPreferredIme))
        {
            return true;
        }

        if (_imeService.SwitchTo(_cadPreferredIme, window.Handle, _settings, $"cad-plugin:{_cadMode}"))
        {
            RecordSwitchAttempt(window.Handle, _cadPreferredIme);
        }

        return true;
    }

    private void EnableCadIntegration()
    {
        _settings.CadIntegration.Enabled = true;
        _settingsService.Save(_settings);
        _cadPromptDismissedThisSession = false;
        _floatingStatusService.UpdateCustom("CAD", _cadPluginConnected ? "CAD 增强已启用" : "等待 CAD 插件", "#CC155E75");
        _logger.Info("CAD integration enabled by user.");
    }

    private void UpdateFloatingStatus(ActiveWindowInfo? window, string currentIme)
    {
        if (window is not null && IsAutoCad(window.ProcessName))
        {
            if (!_settings.CadIntegration.Enabled && _settings.CadIntegration.PromptOnDetect && !_cadPromptDismissedThisSession)
            {
                _floatingStatusService.ShowCadPrompt();
                return;
            }

            if (_settings.CadIntegration.Enabled && _cadPluginConnected)
            {
                var label = string.IsNullOrWhiteSpace(_cadMode) ? "CAD 增强已连接" : _cadMode;
                var imeText = currentIme == "中文" ? "中文输入" : currentIme == "英文" ? "英文输入" : "输入法未知";
                _floatingStatusService.UpdateCustom("CAD", $"{label} · {imeText}", "#CC155E75");
                return;
            }

            if (_settings.CadIntegration.Enabled)
            {
                _floatingStatusService.UpdateCustom("CAD", "等待 CAD 插件", "#CC374151");
                return;
            }
        }

        _floatingStatusService.Update(currentIme);
    }

    private static string NormalizeProcessName(string processName) =>
        processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? processName : $"{processName}.exe";

    private static bool IsAutoCad(string processName) =>
        string.Equals(NormalizeProcessName(processName), "acad.exe", StringComparison.OrdinalIgnoreCase);

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
