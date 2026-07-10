using System.Windows.Threading;
using BS.IME.Assistant.Models;
using BS.IME.Assistant.Views;
using Microsoft.Win32;

namespace BS.IME.Assistant.Services;

public sealed class AppController : IDisposable
{
    private readonly MainWindow _window;
    private readonly Logger _logger;
    private readonly SettingsService _settingsService;
    private readonly ImeService _imeService;
    private readonly ActiveWindowService _activeWindowService;
    private readonly TrayService _trayService;
    private readonly FloatingStatusService _floatingStatusService;
    private readonly PipeCommandService _pipeCommandService;
    private readonly MaxFocusImeService _maxFocusImeService;
    private readonly CadIntegrationService _cadService;
    private readonly MaxIntegrationService _maxService;
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
        _logger = new Logger(AppPaths.Root);
        _settingsService = new SettingsService(_logger);
        _imeService = new ImeService(_logger);
        _activeWindowService = new ActiveWindowService(_logger);
        _trayService = new TrayService(_logger);
        _floatingStatusService = new FloatingStatusService(_logger, _settingsService);
        _pipeCommandService = new PipeCommandService(_logger);
        _maxFocusImeService = new MaxFocusImeService(_logger);
        _cadService = new CadIntegrationService(_logger, _imeService, _floatingStatusService, _settingsService);
        _maxService = new MaxIntegrationService(_logger, _maxFocusImeService, _floatingStatusService, _imeService);
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
        _floatingStatusService.CadPromptAccepted += () => _cadService.EnableIntegration(_settings);
        _floatingStatusService.CadPromptDismissed += () => _cadService.DismissPrompt();
        _floatingStatusService.SwitchChineseRequested += () => ManualSwitch("zh", "floating");
        _floatingStatusService.SwitchEnglishRequested += () => ManualSwitch("en", "floating");
        _pipeCommandService.CommandReceived += OnPipeCommandReceived;
        _pipeCommandService.Start();

        _trayService.ToggleEnabledRequested += ToggleEnabled;
        _trayService.ShowFloatingRequested += _floatingStatusService.Show;
        _trayService.HideFloatingRequested += _floatingStatusService.Hide;
        _trayService.OpenSettingsRequested += OpenSettingsWindow;
        _trayService.ResetFloatingRequested += _floatingStatusService.ResetPositionToBottomRight;
        _trayService.SwitchChineseRequested += () => ManualSwitch("zh", "tray");
        _trayService.SwitchEnglishRequested += () => ManualSwitch("en", "tray");
        _trayService.RestartRequested += RestartApplication;
        _trayService.ExitRequested += () => System.Windows.Application.Current.Shutdown();
        _trayService.Initialize(_settings.Enabled);

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
            _trayService.Update(_settings.Enabled, window?.ProcessName ?? "", currentIme);
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

            if (_cadService.TryApply(window, _settings, GetCurrentHkl(window.ThreadId), out var cadResetRetry, out var cadTargetIme))
            {
                if (cadResetRetry)
                {
                    ResetSwitchAttempt();
                    return;
                }

                if (cadTargetIme is not null && CanRetrySwitch(window.Handle, cadTargetIme))
                {
                    if (_imeService.SwitchTo(cadTargetIme, window.Handle, _settings, $"cad-plugin:{_cadService.Mode}"))
                    {
                        RecordSwitchAttempt(window.Handle, cadTargetIme);
                    }
                }

                return;
            }

            if (_maxService.TryApplyFocus(window, _settings, GetCurrentHkl(window.ThreadId), out var maxFocusReset, out var maxFocusTarget))
            {
                if (maxFocusReset)
                {
                    ResetSwitchAttempt();
                    return;
                }

                if (maxFocusTarget is not null && CanRetrySwitch(window.Handle, maxFocusTarget))
                {
                    if (_imeService.SwitchTo(maxFocusTarget, window.Handle, _settings, $"3dsmax-focus:{_maxService.Mode}"))
                    {
                        RecordSwitchAttempt(window.Handle, maxFocusTarget);
                    }
                }

                return;
            }

            if (_maxService.TryApply(window, _settings, GetCurrentHkl(window.ThreadId), out var maxPluginReset, out var maxPluginTarget))
            {
                if (maxPluginReset)
                {
                    ResetSwitchAttempt();
                    return;
                }

                if (maxPluginTarget is not null && CanRetrySwitch(window.Handle, maxPluginTarget))
                {
                    if (_imeService.SwitchTo(maxPluginTarget, window.Handle, _settings, $"3dsmax-plugin:{_maxService.Mode}"))
                    {
                        RecordSwitchAttempt(window.Handle, maxPluginTarget);
                    }
                }

                return;
            }

            var profile = _settings.Profiles.FirstOrDefault(x =>
                x.Enabled &&
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

    private void OpenSettingsWindow()
    {
        try
        {
            var languages = _imeService.GetInstalledInputLanguages();
            var settingsWindow = new SettingsWindow(
                _settings,
                languages,
                _settingsService,
                _logger,
                _floatingStatusService.ResetPositionToBottomRight,
                _floatingStatusService.ClampPositionToScreen)
            {
                Owner = _window
            };
            settingsWindow.ApplyRequested += appliedSettings => ApplySettings(appliedSettings, languages);

            if (settingsWindow.ShowDialog() != true)
            {
                return;
            }

            ApplySettings(settingsWindow.Settings, languages);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to open settings window.", ex);
        }
    }

    private void ApplySettings(AppSettings settings, IReadOnlyList<InputLanguageInfo> languages)
    {
        _settings = settings;
        _imeService.EnsureTargets(_settings, languages, _settingsService);
        _settingsService.Save(_settings);
        ApplyStartupRegistration(_settings.StartWithWindows);
        _floatingStatusService.ApplySettings(_settings);
        _window.UpdateInfo(_settings.Enabled, _settings.TargetChineseHkl, _settings.TargetEnglishHkl, _settingsService.SettingsPath, _logger.LogPath);
        _trayService.Update(_settings.Enabled, _lastWindow?.ProcessName ?? "", "未知");
        _logger.Info("Settings applied.");
    }

    private void ApplyStartupRegistration(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(exe))
                {
                    key?.SetValue("BS-IME-Assistant", $"\"{exe}\"");
                }
            }
            else
            {
                key?.DeleteValue("BS-IME-Assistant", throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to update Windows startup registration.", ex);
        }
    }

    private void RestartApplication()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exe) && File.Exists(exe))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exe,
                    UseShellExecute = true,
                    WorkingDirectory = AppContext.BaseDirectory
                });
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to restart application.", ex);
        }

        System.Windows.Application.Current.Shutdown();
    }

    private void OnPipeCommandReceived(Models.PipeImeCommand command)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (command.Source.Equals("AutoCAD", StringComparison.OrdinalIgnoreCase))
            {
                var activeWindow = _activeWindowService.GetForegroundWindowInfo();
                _cadService.HandlePipeCommand(command, _settings, activeWindow, out var cadTargetIme, out var cadTargetHandle);
                if (cadTargetIme is not null)
                {
                    RecordSwitchAttempt(cadTargetHandle, cadTargetIme);
                }
                return;
            }

            if (command.Source.Equals("3dsMax", StringComparison.OrdinalIgnoreCase) ||
                command.Source.Equals("3ds Max", StringComparison.OrdinalIgnoreCase))
            {
                var activeWindow = _activeWindowService.GetForegroundWindowInfo();
                _maxService.HandlePipeCommand(command, _settings, activeWindow, out var maxTargetIme, out var maxTargetHandle);
                if (maxTargetIme is not null)
                {
                    RecordSwitchAttempt(maxTargetHandle, maxTargetIme);
                }
                return;
            }
        });
    }

    private void UpdateFloatingStatus(ActiveWindowInfo? window, string currentIme)
    {
        if (_cadService.TryUpdateFloatingStatus(window, currentIme, _settings))
        {
            return;
        }

        if (_maxService.TryUpdateFloatingStatus(window, currentIme))
        {
            return;
        }

        _floatingStatusService.Update(currentIme);
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
