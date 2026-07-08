using BS.IME.Assistant.Models;

namespace BS.IME.Assistant.Services;

public sealed class MaxIntegrationService
{
    private readonly Logger _logger;
    private readonly MaxFocusImeService _maxFocusImeService;
    private readonly FloatingStatusService _floatingStatusService;
    private readonly ImeService _imeService;

    private string _preferredIme = "";
    private string _mode = "";
    private bool _pluginConnected;

    public MaxIntegrationService(Logger logger, MaxFocusImeService maxFocusImeService,
        FloatingStatusService floatingStatusService, ImeService imeService)
    {
        _logger = logger;
        _maxFocusImeService = maxFocusImeService;
        _floatingStatusService = floatingStatusService;
        _imeService = imeService;
    }

    public string Mode => _mode;
    public bool IsPluginConnected => _pluginConnected;

    public void HandlePipeCommand(PipeImeCommand command, AppSettings settings,
        ActiveWindowInfo? activeWindow,
        out string? targetIme, out nint targetWindowHandle)
    {
        targetIme = null;
        targetWindowHandle = nint.Zero;

        _pluginConnected = !command.Event.Equals("PluginDisconnected", StringComparison.OrdinalIgnoreCase);

        if (command.Event.Equals("PluginReady", StringComparison.OrdinalIgnoreCase))
        {
            _mode = "3ds Max 插件已连接";
            _preferredIme = "en";
            _floatingStatusService.UpdateCustom("MAX", "MAX 已连接", "#CC4338CA");
            return;
        }

        if (command.Event.Equals("PluginDisconnected", StringComparison.OrdinalIgnoreCase))
        {
            _mode = "3ds Max 插件已断开";
            _preferredIme = "";
            _floatingStatusService.UpdateCustom("MAX", "MAX", "#CC374151");
            return;
        }

        _mode = string.IsNullOrWhiteSpace(command.Mode) ? command.Event : command.Mode;
        _preferredIme = command.PreferredIme.Equals("zh", StringComparison.OrdinalIgnoreCase) ? "zh" : "en";

        if (activeWindow is not null && Is3dsMax(activeWindow.ProcessName))
        {
            _imeService.SwitchTo(_preferredIme, activeWindow.Handle, settings, $"3dsmax-plugin:{command.Event}");
            targetIme = _preferredIme;
            targetWindowHandle = activeWindow.Handle;
        }
    }

    public bool TryApply(ActiveWindowInfo window, AppSettings settings, string currentHkl,
        out bool shouldResetRetry, out string? targetIme)
    {
        shouldResetRetry = false;
        targetIme = null;

        if (!_pluginConnected || string.IsNullOrWhiteSpace(_preferredIme) || !Is3dsMax(window.ProcessName))
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

    public bool TryApplyFocus(ActiveWindowInfo window, AppSettings settings, string currentHkl,
        out bool shouldResetRetry, out string? targetIme)
    {
        shouldResetRetry = false;
        targetIme = null;

        if (!Is3dsMax(window.ProcessName))
        {
            return false;
        }

        var decision = _maxFocusImeService.Detect(window);
        if (decision is null)
        {
            return false;
        }

        _mode = decision.Reason;
        _preferredIme = decision.TargetIme;

        if (_maxFocusImeService.ShouldLog(decision))
        {
            _logger.Info($"3ds Max focus IME decision: target={decision.TargetIme}, reason={decision.Reason}");
        }

        var targetHkl = decision.TargetIme == "zh" ? settings.TargetChineseHkl : settings.TargetEnglishHkl;
        if (ImeService.HklEquals(targetHkl, currentHkl))
        {
            shouldResetRetry = true;
            return true;
        }

        targetIme = decision.TargetIme;
        return true;
    }

    public bool TryUpdateFloatingStatus(ActiveWindowInfo? window, string currentIme)
    {
        if (window is null || !Is3dsMax(window.ProcessName) || !_pluginConnected)
        {
            return false;
        }

        var imeText = currentIme == "中文" ? "中文输入" : currentIme == "英文" ? "英文输入" : "输入法未知";
        _floatingStatusService.UpdateCustom("MAX", imeText, "#CC4338CA");
        return true;
    }

    private static bool Is3dsMax(string processName)
    {
        var normalized = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName
            : $"{processName}.exe";
        return string.Equals(normalized, "3dsmax.exe", StringComparison.OrdinalIgnoreCase);
    }
}
