using System.Windows.Interop;
using BS.IME.Assistant.Native;

namespace BS.IME.Assistant.Services;

public sealed class HotkeyService : IDisposable
{
    private const int EnglishHotkeyId = 101;
    private const int ChineseHotkeyId = 102;
    private readonly Logger _logger;
    private HwndSource? _source;
    private nint _handle;
    private bool _disposed;

    public HotkeyService(Logger logger)
    {
        _logger = logger;
    }

    public bool HasRegistrationFailure { get; private set; }
    public event Action<string>? HotkeyPressed;

    public void Register(nint handle, string englishHotkey, string chineseHotkey)
    {
        _handle = handle;
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WndProc);

        RegisterOne(EnglishHotkeyId, englishHotkey, "en");
        RegisterOne(ChineseHotkeyId, chineseHotkey, "zh");
    }

    public void Unregister()
    {
        try
        {
            if (_handle != nint.Zero)
            {
                _ = Win32.UnregisterHotKey(_handle, EnglishHotkeyId);
                _ = Win32.UnregisterHotKey(_handle, ChineseHotkeyId);
            }

            _source?.RemoveHook(WndProc);
            _logger.Info("Hotkeys unregistered.");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to unregister hotkeys.", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Unregister();
        _disposed = true;
    }

    private void RegisterOne(int id, string text, string imeKind)
    {
        try
        {
            if (!TryParseHotkey(text, out var modifiers, out var key))
            {
                HasRegistrationFailure = true;
                _logger.Warn($"Invalid hotkey setting for {imeKind}: {text}");
                return;
            }

            var ok = Win32.RegisterHotKey(_handle, id, modifiers | Win32.MOD_NOREPEAT, key);
            if (!ok)
            {
                HasRegistrationFailure = true;
                _logger.Warn($"Failed to register hotkey {text} for {imeKind}.");
                return;
            }

            _logger.Info($"Registered hotkey {text} for {imeKind}.");
        }
        catch (Exception ex)
        {
            HasRegistrationFailure = true;
            _logger.Error($"Failed to register hotkey {text}.", ex);
        }
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg != Win32.WM_HOTKEY)
        {
            return nint.Zero;
        }

        var id = wParam.ToInt32();
        if (id == EnglishHotkeyId)
        {
            HotkeyPressed?.Invoke("en");
            handled = true;
        }
        else if (id == ChineseHotkeyId)
        {
            HotkeyPressed?.Invoke("zh");
            handled = true;
        }

        return nint.Zero;
    }

    private static bool TryParseHotkey(string value, out uint modifiers, out uint key)
    {
        modifiers = 0;
        key = 0;
        var parts = value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= Win32.MOD_CONTROL;
            }
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= Win32.MOD_ALT;
            }
            else if (part.Length == 1)
            {
                key = char.ToUpperInvariant(part[0]);
            }
            else if (Enum.TryParse<System.Windows.Forms.Keys>(part, true, out var parsed))
            {
                key = (uint)parsed;
            }
        }

        return modifiers != 0 && key != 0;
    }
}
