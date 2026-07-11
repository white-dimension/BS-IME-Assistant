using System.Windows.Automation;

namespace BS.IME.Assistant.Services;

public sealed class MaxFocusImeService
{
    public static bool DebugLogging { get; set; }

    private readonly Logger _logger;
    private string _lastSignature = "";
    private string _lastTarget = "";
    private bool _wasTextInput;
    private nint _lastTextInputWindow;
    private DateTime _lastTextInputTime = DateTime.MinValue;

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
                _wasTextInput = false;
                return null;
            }

            if (DebugLogging)
            {
                LogFocusedElementInfo(focused, window.ProcessName);
            }

            // ── 非输入控件类型快速排除 ──
            var ct = focused.Current.ControlType;
            if (ct == ControlType.Button || ct == ControlType.RadioButton ||
                ct == ControlType.Slider || ct == ControlType.ScrollBar ||
                ct == ControlType.ProgressBar || ct == ControlType.TabItem ||
                ct == ControlType.Thumb || ct == ControlType.TitleBar ||
                ct == ControlType.Menu || ct == ControlType.MenuBar ||
                ct == ControlType.ToolBar || ct == ControlType.StatusBar ||
                ct == ControlType.ToolTip || ct == ControlType.Image ||
                ct == ControlType.Hyperlink || ct == ControlType.Separator)
            {
                return HandleNonInput(window);
            }

            var signature = BuildSignature(focused, window);

            // ── Pattern 快速排除 ──
            if (HasPattern(focused, TogglePattern.Pattern))
                return HandleNonInput(window);

            if (HasPattern(focused, RangeValuePattern.Pattern))
                return HandleNonInput(window);

            if (HasPattern(focused, ExpandCollapsePattern.Pattern) && !HasPattern(focused, ValuePattern.Pattern))
                return HandleNonInput(window);

            if (HasPattern(focused, InvokePattern.Pattern) && !HasPattern(focused, ValuePattern.Pattern))
                return HandleNonInput(window);

            // ── TextPattern → 读取实际文本内容 ──
            if (TryGetTextContent(focused, out var textContent) && !string.IsNullOrWhiteSpace(textContent))
            {
                if (!IsNumericString(textContent))
                {
                    return HandleTextInput("zh", "TextPlus/text input", signature, window);
                }
                return HandleNonInput(window);
            }

            // ── ValuePattern + 非只读 → 文本输入框 ──
            if (focused.TryGetCurrentPattern(ValuePattern.Pattern, out var vpObj))
            {
                var vp = (ValuePattern)vpObj;
                if (!vp.Current.IsReadOnly && IsValuePatternTextInput(focused))
                {
                    return HandleTextInput("zh", "Name/text edit", signature, window);
                }
            }

            // ── ControlType.Edit 兜底 ──
            // 当 Pattern 检测均未匹配时，如果控件是 Edit 类型，大概率是文本输入框。
            // 例如 3ds Max F2 重命名弹出框中的 WindowsForms10 Edit 控件，
            // 不暴露任何 UIAutomation Pattern，只能靠 ControlType 判断。
            if (ct == ControlType.Edit)
            {
                return HandleTextInput("zh", "Edit fallback", signature, window);
            }

            return HandleNonInput(window);
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

    private MaxFocusImeDecision? HandleTextInput(string targetIme, string reason, string signature, ActiveWindowInfo window)
    {
        if (!_wasTextInput && DebugLogging)
        {
            _logger.Info("[MaxFocusIme] Enter text input, reason=" + reason);
        }

        _wasTextInput = true;
        _lastTextInputWindow = window.Handle;
        _lastTextInputTime = DateTime.Now;

        return new MaxFocusImeDecision(targetIme, reason, signature);
    }

    private MaxFocusImeDecision? HandleNonInput(ActiveWindowInfo window)
    {
        if (_wasTextInput)
        {
            _wasTextInput = false;

            if (DebugLogging)
            {
                _logger.Info("[MaxFocusIme] Leave text input, restore english");
            }

            return new MaxFocusImeDecision("en", "Leave text input", "leave_text_input");
        }

        return null;
    }

    private void LogFocusedElementInfo(AutomationElement element, string processName)
    {
        try
        {
            var hasValue = element.TryGetCurrentPattern(ValuePattern.Pattern, out var valueObj);
            var isReadOnly = hasValue && ((ValuePattern)valueObj).Current.IsReadOnly;
            var hasRangeValue = element.TryGetCurrentPattern(RangeValuePattern.Pattern, out _);
            var hasText = element.TryGetCurrentPattern(TextPattern.Pattern, out _);
            var hasInvoke = element.TryGetCurrentPattern(InvokePattern.Pattern, out _);
            var hasToggle = element.TryGetCurrentPattern(TogglePattern.Pattern, out _);
            var hasExpand = element.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out _);

            var control = element.Current;

            _logger.Info($"[MaxFocusIme.Diagnostic] " +
                $"process={processName}, " +
                $"ctrlType={control.ControlType.ProgrammaticName}, " +
                $"name=\"{control.Name}\", " +
                $"className=\"{control.ClassName}\", " +
                $"autoId=\"{control.AutomationId}\", " +
                $"ValuePattern={hasValue}, " +
                $"IsReadOnly={isReadOnly}, " +
                $"RangeValue={hasRangeValue}, " +
                $"TextPattern={hasText}, " +
                $"InvokePattern={hasInvoke}, " +
                $"TogglePattern={hasToggle}, " +
                $"ExpandPattern={hasExpand}");
        }
        catch (Exception ex)
        {
            _logger.Warn($"[MaxFocusIme.Diagnostic] Logging failed: {ex.Message}");
        }
    }

    private static bool HasPattern(AutomationElement element, AutomationPattern pattern)
    {
        return element.TryGetCurrentPattern(pattern, out _);
    }

    private static bool IsValuePatternTextInput(AutomationElement element)
    {
        var control = element.Current;
        if (control.ControlType == ControlType.Edit || control.ControlType == ControlType.Document)
        {
            return true;
        }

        var className = control.ClassName ?? "";
        return className.Contains("Edit", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("TextBox", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("LineEdit", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetTextContent(AutomationElement element, out string text)
    {
        text = "";
        if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var obj))
        {
            return false;
        }

        try
        {
            var tp = (TextPattern)obj;
            var range = tp.DocumentRange;
            text = range.GetText(-1) ?? "";
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsNumericString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!char.IsDigit(c) && c != '.' && c != '-' && c != '/' &&
                c != ' ' && c != ',' && c != ':' && c != 'x' && c != 'X' && c != '*')
            {
                return false;
            }
        }

        return true;
    }

    private static string BuildSignature(AutomationElement element, ActiveWindowInfo window)
    {
        var control = element.Current;
        return $"{control.ControlType.ProgrammaticName}|{control.Name}|{control.AutomationId}|{control.ClassName}|{window.Title}";
    }
}

public sealed record MaxFocusImeDecision(string TargetIme, string Reason, string Signature);
