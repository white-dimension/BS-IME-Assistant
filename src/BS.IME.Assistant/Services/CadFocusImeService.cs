using System.Windows.Automation;

namespace BS.IME.Assistant.Services;

public sealed class CadFocusImeService
{
    private readonly Logger _logger;
    private string _lastSignature = "";
    private string _lastTarget = "";
    private bool _wasTextInput;

    public CadFocusImeService(Logger logger)
    {
        _logger = logger;
    }

    public CadFocusImeDecision? Detect(ActiveWindowInfo window)
    {
        try
        {
            var focused = AutomationElement.FocusedElement;
            if (focused is null)
            {
                return null;
            }

            var processId = focused.Current.ProcessId;
            if (processId != 0 && processId != window.ProcessId)
            {
                _wasTextInput = false;
                return null;
            }

            var signature = BuildSignature(focused, window);
            if (IsCommandOrSearchInput(focused))
            {
                return HandleNonInput(signature);
            }

            var controlType = focused.Current.ControlType;
            if (controlType == ControlType.Edit || controlType == ControlType.Document)
            {
                if (TryGetValue(focused, out var value) && IsNumericString(value))
                {
                    return HandleNonInput(signature);
                }

                return HandleTextInput("CAD text input", signature);
            }

            if (focused.TryGetCurrentPattern(ValuePattern.Pattern, out var patternObject))
            {
                var valuePattern = (ValuePattern)patternObject;
                if (!valuePattern.Current.IsReadOnly && LooksLikeTextEditor(focused))
                {
                    if (IsNumericString(valuePattern.Current.Value ?? ""))
                    {
                        return HandleNonInput(signature);
                    }

                    return HandleTextInput("CAD editable control", signature);
                }
            }

            return HandleNonInput(signature);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to inspect AutoCAD focused control.", ex);
            return null;
        }
    }

    public bool ShouldLog(CadFocusImeDecision decision)
    {
        if (decision.Signature == _lastSignature && decision.TargetIme == _lastTarget)
        {
            return false;
        }

        _lastSignature = decision.Signature;
        _lastTarget = decision.TargetIme;
        return true;
    }

    private CadFocusImeDecision HandleTextInput(string reason, string signature)
    {
        _wasTextInput = true;
        return new CadFocusImeDecision("zh", reason, signature);
    }

    private CadFocusImeDecision? HandleNonInput(string signature)
    {
        if (!_wasTextInput)
        {
            return null;
        }

        _wasTextInput = false;
        return new CadFocusImeDecision("en", "Leave CAD text input", signature);
    }

    private static bool IsCommandOrSearchInput(AutomationElement element)
    {
        var current = element.Current;
        var identity = $"{current.Name}|{current.AutomationId}|{current.ClassName}";
        string[] markers =
        [
            "command line", "commandline", "command:", "命令行", "命令:", "命令：",
            "search", "搜索", "查找命令"
        ];

        return markers.Any(marker => identity.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeTextEditor(AutomationElement element)
    {
        var current = element.Current;
        var className = current.ClassName ?? "";
        return className.Contains("Edit", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("TextBox", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("RichText", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("MText", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetValue(AutomationElement element, out string value)
    {
        value = "";
        if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out var patternObject))
        {
            return false;
        }

        value = ((ValuePattern)patternObject).Current.Value ?? "";
        return true;
    }

    private static bool IsNumericString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.All(character =>
            char.IsDigit(character) || character is '.' or '-' or '/' or ' ' or ',' or ':' or 'x' or 'X' or '*');
    }

    private static string BuildSignature(AutomationElement element, ActiveWindowInfo window)
    {
        var current = element.Current;
        return $"{current.ControlType.ProgrammaticName}|{current.Name}|{current.AutomationId}|{current.ClassName}|{window.Title}";
    }
}

public sealed record CadFocusImeDecision(string TargetIme, string Reason, string Signature);
