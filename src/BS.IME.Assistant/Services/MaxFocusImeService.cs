using System.Windows.Automation;

namespace BS.IME.Assistant.Services;

public sealed class MaxFocusImeService
{
    private readonly Logger _logger;
    private string _lastSignature = "";
    private string _lastTarget = "";

    public MaxFocusImeService(Logger logger)
    {
        _logger = logger;
    }

    public MaxFocusImeDecision? Detect(ActiveWindowInfo window)
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
                return null;
            }

            var controlType = focused.Current.ControlType;
            var name = focused.Current.Name ?? "";
            var automationId = focused.Current.AutomationId ?? "";
            var className = focused.Current.ClassName ?? "";
            var signature = $"{controlType.ProgrammaticName}|{name}|{automationId}|{className}|{window.Title}";

            var isTextInput = IsTextInput(controlType, className);
            if (!isTextInput)
            {
                return new MaxFocusImeDecision("en", "3ds Max modeling", signature);
            }

            if (LooksLikeNumericInput(signature))
            {
                return new MaxFocusImeDecision("en", "3ds Max numeric input", signature);
            }

            if (LooksLikeChineseTextInput(signature))
            {
                return new MaxFocusImeDecision("zh", "3ds Max text/name input", signature);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to inspect 3ds Max focused control.", ex);
            return null;
        }
    }

    public bool ShouldLog(MaxFocusImeDecision decision)
    {
        if (decision.Signature == _lastSignature && decision.TargetIme == _lastTarget)
        {
            return false;
        }

        _lastSignature = decision.Signature;
        _lastTarget = decision.TargetIme;
        return true;
    }

    private static bool IsTextInput(ControlType controlType, string className)
    {
        if (controlType == ControlType.Edit || controlType == ControlType.ComboBox || controlType == ControlType.Document)
        {
            return true;
        }

        return ContainsAny(className, "edit", "textbox", "richtext");
    }

    private static bool LooksLikeChineseTextInput(string text)
    {
        return ContainsAny(
            text,
            "name",
            "rename",
            "object name",
            "layer",
            "material",
            "map",
            "text",
            "caption",
            "comment",
            "description",
            "notes",
            "annotation");
    }

    private static bool LooksLikeNumericInput(string text)
    {
        return ContainsAny(
            text,
            "spinner",
            "numeric",
            "number",
            "amount",
            "percent",
            "x position",
            "y position",
            "z position",
            "width",
            "height",
            "length",
            "radius",
            "angle",
            "segments",
            "scale",
            "rotation");
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (text.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

public sealed record MaxFocusImeDecision(string TargetIme, string Reason, string Signature);
