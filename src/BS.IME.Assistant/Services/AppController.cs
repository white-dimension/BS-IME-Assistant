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
    private readonly CadFocusImeService _cadFocusImeService;
    private readonly MaxFocusImeService _maxFocusImeService;
    private readonly DispatcherTimer _timer;
    private AppSettings _settings;
    private ActiveWindowInfo? _lastWindow;
    private nint _lastSwitchWindowHandle;
    private string _lastSwitchTarget = "";
    private DateTimeOffset _lastSwitchAttemptAt = DateTimeOffset.MinValue;
    private string _cadBridgePreferredIme = "";
    private string _cadBridgeMode = "";
    private bool _cadBridgeConnected;
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
        _cadFocusImeService = new CadFocusImeService(_logger);
        _maxFocusImeService = new MaxFocusImeService(_logger);
        _settings = AppSettings.CreateDefault();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => Tick();
    }

    public Logger Logger => _logger;

    public void Start(nint windowHandle)
    {
        _logger.Info("Program started.");
        _settings = _settingsService.LoadOrCreate();
        if (TryGetStartupRegistration(out var registeredForStartup) &&
            _settings.StartWithWindows != registeredForStartup)
        {
            _settings.StartWithWindows = registeredForStartup;
            _settingsService.Save(_settings);
        }
        var languages = _imeService.GetInstalledInputLanguages();
        _imeService.EnsureTargets(_settings, languages, _settingsService);
        _window.UpdateInfo(_settings.Enabled, _settings.TargetChineseHkl, _settings.TargetEnglishHkl, _settingsService.SettingsPath, _logger.LogPath);
        _floatingStatusService.Initialize(_settings);
        _floatingStatusService.SwitchChineseRequested += () => ManualSwitch("zh", "floating");
        _floatingStatusService.SwitchEnglishRequested += () => ManualSwitch("en", "floating");
        _pipeCommandService.CommandReceived += OnCadBridgeCommandReceived;
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

            var profile = FindProfile(window.ProcessName);
            if (profile?.Enabled == true && IsProcess(window.ProcessName, "acad.exe"))
            {
                if (_cadBridgeConnected && !string.IsNullOrWhiteSpace(_cadBridgePreferredIme))
                {
                    ApplyFocusDecision(window, _cadBridgePreferredIme, _cadBridgeMode, "autocad-bridge", false);
                    return;
                }

                var decision = _cadFocusImeService.Detect(window);
                if (decision is not null)
                {
                    ApplyFocusDecision(window, decision.TargetIme, decision.Reason, "autocad-focus", _cadFocusImeService.ShouldLog(decision));
                    return;
                }
            }

            if (profile?.Enabled == true && IsProcess(window.ProcessName, "3dsmax.exe"))
            {
                var decision = _maxFocusImeService.Detect(window);
                if (decision is not null)
                {
                    ApplyFocusDecision(window, decision.TargetIme, decision.Reason, "3dsmax-focus", _maxFocusImeService.ShouldLog(decision));
                    return;
                }
            }

            if (profile is null || !profile.Enabled || !profile.SwitchOnActivate)
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
        if (!ApplyStartupRegistration(settings.StartWithWindows))
        {
            settings.StartWithWindows = TryGetStartupRegistration(out var registeredForStartup)
                ? registeredForStartup
                : _settings.StartWithWindows;
        }

        _settings = settings;
        _imeService.EnsureTargets(_settings, languages, _settingsService);
        _settingsService.Save(_settings);
        _floatingStatusService.ApplySettings(_settings);
        _window.UpdateInfo(_settings.Enabled, _settings.TargetChineseHkl, _settings.TargetEnglishHkl, _settingsService.SettingsPath, _logger.LogPath);
        _trayService.Update(_settings.Enabled, _lastWindow?.ProcessName ?? "", "未知");
        _logger.Info("Settings applied.");
    }

    private bool ApplyStartupRegistration(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (key is null)
            {
                _logger.Warn("Windows startup registry key is unavailable.");
                return false;
            }

            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(exe))
                {
                    key.SetValue("BS-IME-Assistant", $"\"{exe}\"");
                }
                else
                {
                    return false;
                }
            }
            else
            {
                key.DeleteValue("BS-IME-Assistant", throwOnMissingValue: false);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to update Windows startup registration.", ex);
            return false;
        }
    }

    private bool TryGetStartupRegistration(out bool enabled)
    {
        enabled = false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            enabled = key?.GetValue("BS-IME-Assistant") is string value && !string.IsNullOrWhiteSpace(value);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to read Windows startup registration.", ex);
            return false;
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
                    Arguments = $"--wait-for-process {Environment.ProcessId}",
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

    private void OnCadBridgeCommandReceived(PipeImeCommand command)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (command.Event.Equals("PluginDisconnected", StringComparison.OrdinalIgnoreCase))
            {
                _cadBridgeConnected = false;
                _cadBridgePreferredIme = "";
                _cadBridgeMode = "";
                _logger.Info("AutoCAD IME bridge disconnected.");
                return;
            }

            _cadBridgeConnected = true;
            if (command.Event.Equals("PluginReady", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Info("AutoCAD IME bridge connected.");
                return;
            }

            _cadBridgePreferredIme = command.PreferredIme.Equals("zh", StringComparison.OrdinalIgnoreCase) ? "zh" : "en";
            _cadBridgeMode = string.IsNullOrWhiteSpace(command.Mode) ? command.Event : command.Mode;
            _logger.Info($"AutoCAD bridge IME decision: target={_cadBridgePreferredIme}, mode={_cadBridgeMode}");

            if (!_settings.Enabled || !IsProfileEnabled("acad.exe"))
            {
                return;
            }

            var window = _activeWindowService.GetForegroundWindowInfo();
            if (window is not null && IsProcess(window.ProcessName, "acad.exe"))
            {
                ApplyFocusDecision(window, _cadBridgePreferredIme, _cadBridgeMode, "autocad-bridge", false);
            }
        });
    }

    private void UpdateFloatingStatus(ActiveWindowInfo? window, string currentIme)
    {
        if (window is not null && IsProfileEnabled(window.ProcessName) && IsProcess(window.ProcessName, "acad.exe"))
        {
            _floatingStatusService.UpdateCustom("CAD", FormatImeStatus(currentIme), "#CC155E75");
            return;
        }

        if (window is not null && IsProfileEnabled(window.ProcessName) && IsProcess(window.ProcessName, "3dsmax.exe"))
        {
            _floatingStatusService.UpdateCustom("MAX", FormatImeStatus(currentIme), "#CC4338CA");
            return;
        }

        _floatingStatusService.Update(currentIme);
    }

    private static string NormalizeProcessName(string processName) =>
        processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? processName : $"{processName}.exe";

    private AppProfile? FindProfile(string processName) =>
        _settings.Profiles.FirstOrDefault(profile =>
            string.Equals(
                NormalizeProcessName(profile.ProcessName),
                NormalizeProcessName(processName),
                StringComparison.OrdinalIgnoreCase));

    private bool IsProfileEnabled(string processName) => FindProfile(processName)?.Enabled == true;

    private void ApplyFocusDecision(ActiveWindowInfo window, string targetIme, string reason, string source, bool shouldLog)
    {
        if (shouldLog)
        {
            _logger.Info($"Standalone focus IME decision: process={window.ProcessName}, target={targetIme}, reason={reason}");
        }

        var targetHkl = targetIme == "zh" ? _settings.TargetChineseHkl : _settings.TargetEnglishHkl;
        if (ImeService.HklEquals(targetHkl, GetCurrentHkl(window.ThreadId)))
        {
            ResetSwitchAttempt();
            return;
        }

        if (CanRetrySwitch(window.Handle, targetIme) &&
            _imeService.SwitchTo(targetIme, window.Handle, _settings, $"{source}:{reason}"))
        {
            RecordSwitchAttempt(window.Handle, targetIme);
        }
    }

    private static string FormatImeStatus(string currentIme) =>
        currentIme == "中文" ? "中文输入" : currentIme == "英文" ? "英文输入" : "输入法未知";

    private static bool IsProcess(string processName, string expected) =>
        string.Equals(NormalizeProcessName(processName), NormalizeProcessName(expected), StringComparison.OrdinalIgnoreCase);

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
