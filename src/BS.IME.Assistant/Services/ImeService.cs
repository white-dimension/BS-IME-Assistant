using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using BS.IME.Assistant.Models;
using BS.IME.Assistant.Native;

namespace BS.IME.Assistant.Services;

public sealed class ImeService
{
    private const int MaxSwitchRetries = 3;
    private const int RetryDelayMs = 30;

    private readonly Logger _logger;

    public ImeService(Logger logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<InputLanguageInfo> GetInstalledInputLanguages()
    {
        var results = new List<InputLanguageInfo>();
        try
        {
            var count = Win32.GetKeyboardLayoutList(0, null);
            if (count <= 0)
            {
                _logger.Warn("No keyboard layouts returned by GetKeyboardLayoutList.");
                return results;
            }

            var layouts = new nint[count];
            var actual = Win32.GetKeyboardLayoutList(layouts.Length, layouts);
            foreach (var hkl in layouts.Take(actual))
            {
                results.Add(CreateLanguageInfo(hkl));
            }

            _logger.Info("Input language detection result: " + string.Join("; ", results.Select(x => $"{x.Hkl} {x.FriendlyName}")));
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to detect input languages.", ex);
        }

        return results;
    }

    public void EnsureTargets(AppSettings settings, IReadOnlyList<InputLanguageInfo> languages, SettingsService settingsService)
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(settings.TargetEnglishHkl))
        {
            var english = PickEnglish(languages);
            if (english is not null)
            {
                settings.TargetEnglishHkl = english.Hkl;
                changed = true;
                _logger.Info($"Auto-selected English HKL: {english.Hkl} {english.FriendlyName}");
            }
        }

        if (string.IsNullOrWhiteSpace(settings.TargetChineseHkl))
        {
            var chinese = PickChinese(languages);
            if (chinese is not null)
            {
                settings.TargetChineseHkl = chinese.Hkl;
                changed = true;
                _logger.Info($"Auto-selected Chinese HKL: {chinese.Hkl} {chinese.FriendlyName}");
            }
        }

        if (changed)
        {
            settingsService.Save(settings);
        }
    }

    public string GetCurrentImeKind(uint threadId, AppSettings settings)
    {
        try
        {
            var hklText = ToHklString(Win32.GetKeyboardLayout(threadId));
            if (HklEquals(hklText, settings.TargetEnglishHkl))
            {
                return "英文";
            }

            if (HklEquals(hklText, settings.TargetChineseHkl))
            {
                return "中文";
            }

            return "未知";
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to read current input language.", ex);
            return "未知";
        }
    }

    public bool SwitchTo(string imeKind, nint foregroundWindow, AppSettings settings, string reason)
    {
        try
        {
            var hkl = imeKind.Equals("zh", StringComparison.OrdinalIgnoreCase)
                ? settings.TargetChineseHkl
                : settings.TargetEnglishHkl;

            if (foregroundWindow == nint.Zero)
            {
                _logger.Warn($"Cannot switch IME to {imeKind}: foreground window is invalid.");
                return false;
            }

            if (!TryParseHkl(hkl, out var hklValue))
            {
                _logger.Warn($"Cannot switch IME to {imeKind}: HKL is not configured.");
                return false;
            }

            // Get the thread that owns the target window for verification.
            var threadId = Win32.GetWindowThreadProcessId(foregroundWindow, out _);

            for (var attempt = 0; attempt < MaxSwitchRetries; attempt++)
            {
                var ok = Win32.PostMessage(foregroundWindow, Win32.WM_INPUTLANGCHANGEREQUEST, nint.Zero, hklValue);
                if (!ok)
                {
                    _logger.Warn($"PostMessage failed on attempt {attempt + 1} when switching IME to {imeKind}. LastError={Marshal.GetLastWin32Error()}");
                    return false;
                }

                // Short synchronous wait for the target window to process the WM_INPUTLANGCHANGEREQUEST message.
                // IME switches happen at low frequency (user-initiated or window-change driven),
                // so a brief 30ms pause is acceptable to confirm the input language actually changed.
                Thread.Sleep(RetryDelayMs);

                var currentHklPtr = Win32.GetKeyboardLayout(threadId);
                if (HklEquals(ToHklString(currentHklPtr), hkl))
                {
                    _logger.Info($"IME switch confirmed: {imeKind}, HKL={hkl}, reason={reason}");
                    return true;
                }

                if (attempt < MaxSwitchRetries - 1)
                {
                    _logger.Warn($"IME switch not confirmed on attempt {attempt + 1}. " +
                        $"currentHkl={ToHklString(currentHklPtr)}, targetHkl={hkl}, " +
                        $"threadId={threadId}, windowHandle={foregroundWindow}, reason={reason}");
                }
            }

            _logger.Warn($"IME switch failed after {MaxSwitchRetries} attempts: " +
                $"imeKind={imeKind}, targetHkl={hkl}, " +
                $"threadId={threadId}, windowHandle={foregroundWindow}, reason={reason}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to switch IME to {imeKind}.", ex);
            return false;
        }
    }

    public static bool HklEquals(string? left, string? right) =>
        TryParseHkl(left, out var leftValue) && TryParseHkl(right, out var rightValue) && leftValue == rightValue;

    public static bool TryParseHkl(string? value, out nint hkl)
    {
        hkl = nint.Zero;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..];
        }

        if (!long.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        hkl = (nint)number;
        return true;
    }

    public static string ToHklString(nint hkl) => $"0x{hkl.ToInt64():X8}";

    private InputLanguageInfo CreateLanguageInfo(nint hkl)
    {
        var langId = (int)(hkl.ToInt64() & 0xFFFF);
        var cultureName = "";
        var displayName = "";
        try
        {
            var culture = CultureInfo.GetCultureInfo(langId);
            cultureName = culture.Name;
            displayName = culture.DisplayName;
        }
        catch
        {
            displayName = $"LANGID 0x{langId:X4}";
        }

        var description = GetImeDescription(hkl);
        var haystack = $"{cultureName} {displayName} {description}";
        var isChinese = cultureName.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            || ContainsAny(haystack, ["Microsoft", "Pinyin", "WeChat", "Sogou", "Baidu", "Rime", "Chinese", "中文", "拼音", "微软"]);
        var isEnglish = cultureName.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            || ContainsAny(haystack, ["English", "US", "Keyboard"]);

        return new InputLanguageInfo
        {
            Hkl = ToHklString(hkl),
            CultureName = cultureName,
            DisplayName = displayName,
            ImeDescription = description,
            IsChinese = isChinese,
            IsEnglish = isEnglish
        };
    }

    private InputLanguageInfo? PickEnglish(IReadOnlyList<InputLanguageInfo> languages) =>
        languages
            .Where(x => x.IsEnglish)
            .OrderByDescending(x => x.CultureName.Equals("en-US", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => x.FriendlyName.Contains("US", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

    private InputLanguageInfo? PickChinese(IReadOnlyList<InputLanguageInfo> languages) =>
        languages
            .Where(x => x.IsChinese)
            .OrderByDescending(x => ContainsAny(x.FriendlyName, ["Microsoft", "Pinyin", "微软", "拼音"]))
            .ThenByDescending(x => ContainsAny(x.FriendlyName, ["WeChat", "Sogou", "Baidu", "Rime", "Chinese", "中文"]))
            .FirstOrDefault();

    private static string GetImeDescription(nint hkl)
    {
        try
        {
            var length = Win32.ImmGetDescription(hkl, new StringBuilder(), 0);
            if (length == 0)
            {
                return "";
            }

            var builder = new StringBuilder((int)length + 1);
            _ = Win32.ImmGetDescription(hkl, builder, (uint)builder.Capacity);
            return builder.ToString();
        }
        catch
        {
            return "";
        }
    }

    private static bool ContainsAny(string value, IEnumerable<string> needles) =>
        needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
}
