using BS.IME.Assistant.Models;
using BS.IME.Assistant.Native;

namespace BS.IME.Assistant.Services;

public sealed class CadIntegrationService
{
    private readonly Logger _logger;
    private readonly ImeService _imeService;
    private readonly FloatingStatusService _floatingStatusService;
    private readonly SettingsService _settingsService;

    private string _preferredIme = "";
    private string _mode = "";
    private bool _pluginConnected;
    private bool _promptDismissedThisSession;

    public CadIntegrationService(Logger logger, ImeService imeService,
        FloatingStatusService floatingStatusService, SettingsService settingsService)
    {
        _logger = logger;
        _imeService = imeService;
        _floatingStatusService = floatingStatusService;
        _settingsService = settingsService;
    }

    public string Mode => _mode;

    public void HandlePipeCommand(PipeImeCommand command, AppSettings settings,
        ActiveWindowInfo? activeWindow,
        out string? targetIme, out nint targetWindowHandle)
    {
        targetIme = null;
        targetWindowHandle = nint.Zero;

        _pluginConnected = !command.Event.Equals("PluginDisconnected", StringComparison.OrdinalIgnoreCase);

        if (command.Event.Equals("PluginReady", StringComparison.OrdinalIgnoreCase))
        {
            _mode = "CAD 插件已连接";
            if (!settings.CadIntegration.Enabled)
            {
                _floatingStatusService.ShowCadPrompt();
            }
            return;
        }

        if (!settings.CadIntegration.Enabled)
        {
            _floatingStatusService.ShowCadPrompt();
            _logger.Info("CAD plugin command ignored because CAD integration is not enabled.");
            return;
        }

        _mode = string.IsNullOrWhiteSpace(command.Mode) ? command.Event : command.Mode;
        _preferredIme = command.PreferredIme.Equals("zh", StringComparison.OrdinalIgnoreCase) ? "zh" : "en";

        if (activeWindow is not null && IsAutoCad(activeWindow.ProcessName))
        {
            _imeService.SwitchTo(_preferredIme, activeWindow.Handle, settings, $"cad-plugin:{command.Event}");
            targetIme = _preferredIme;
            targetWindowHandle = activeWindow.Handle;
        }
    }

    public bool TryApply(ActiveWindowInfo window, AppSettings settings, string currentHkl,
        out bool shouldResetRetry, out string? targetIme)
    {
        shouldResetRetry = false;
        targetIme = null;

        if (!settings.CadIntegration.Enabled || !_pluginConnected || string.IsNullOrWhiteSpace(_preferredIme) || !IsAutoCad(window.ProcessName))
        {
            return false;
        }

        var targetHkl = _preferredIme == "zh" ? settings.TargetChineseHkl : settings.TargetEnglishHkl;
        if (ImeService.HklEquals(targetHkl, currentHkl))
        {
            shouldResetRetry = true;
            return true;
        }

        targetIme = _preferredIme;
        return true;
    }

    public void EnableIntegration(AppSettings settings)
    {
        settings.CadIntegration.Enabled = true;
        _settingsService.Save(settings);
        _promptDismissedThisSession = false;
        _floatingStatusService.UpdateCustom("CAD", _pluginConnected ? "CAD 增强已启用" : "等待 CAD 插件", "#CC155E75");
        _logger.Info("CAD integration enabled by user.");
    }

    public void DismissPrompt()
    {
        _promptDismissedThisSession = true;
    }

    public bool TryUpdateFloatingStatus(ActiveWindowInfo? window, string currentIme, AppSettings settings)
    {
        if (window is null || !IsAutoCad(window.ProcessName))
        {
            return false;
        }

        if (!settings.CadIntegration.Enabled && settings.CadIntegration.PromptOnDetect && !_promptDismissedThisSession)
        {
            _floatingStatusService.ShowCadPrompt();
            return true;
        }

        if (settings.CadIntegration.Enabled && _pluginConnected)
        {
            var label = string.IsNullOrWhiteSpace(_mode) ? "CAD 增强已连接" : _mode;
            var imeText = currentIme == "中文" ? "中文输入" : currentIme == "英文" ? "英文输入" : "输入法未知";
            _floatingStatusService.UpdateCustom("CAD", $"{label} · {imeText}", "#CC155E75");
            return true;
        }

        if (settings.CadIntegration.Enabled)
        {
            _floatingStatusService.UpdateCustom("CAD", "等待 CAD 插件", "#CC374151");
            return true;
        }

        return false;
    }

    private static bool IsAutoCad(string processName)
    {
        var normalized = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName
            : $"{processName}.exe";
        return string.Equals(normalized, "acad.exe", StringComparison.OrdinalIgnoreCase);
    }
}
