using System.Text.Json;
using BS.IME.Assistant.Models;

namespace BS.IME.Assistant.Services;

public sealed class SettingsService
{
    private readonly Logger _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public SettingsService(Logger logger)
    {
        _logger = logger;
        AppDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BS-IME-Assistant");
        SettingsPath = Path.Combine(AppDirectory, "settings.json");
        Directory.CreateDirectory(AppDirectory);
    }

    public string AppDirectory { get; }
    public string SettingsPath { get; }

    public AppSettings LoadOrCreate()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                var defaults = AppSettings.CreateDefault();
                Save(defaults);
                _logger.Info($"Created default settings: {SettingsPath}");
                return defaults;
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? AppSettings.CreateDefault();
            if (Normalize(settings))
            {
                Save(settings);
            }

            _logger.Info($"Loaded settings: {SettingsPath}");
            return settings;
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to load settings, using defaults.", ex);
            var defaults = AppSettings.CreateDefault();
            Save(defaults);
            return defaults;
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(AppDirectory);
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            File.WriteAllText(SettingsPath, json);
            _logger.Info("Saved settings.");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to save settings.", ex);
        }
    }

    private bool Normalize(AppSettings settings)
    {
        var changed = false;

        if (settings.Profiles is null || settings.Profiles.Count == 0)
        {
            settings.Profiles = AppSettings.CreateDefaultProfiles();
            changed = true;
            _logger.Warn("Settings profiles were missing or empty; restored default profiles.");
        }
        else
        {
            var validProfiles = new List<AppProfile>();
            foreach (var profile in settings.Profiles)
            {
                if (string.IsNullOrWhiteSpace(profile.ProcessName))
                {
                    changed = true;
                    _logger.Warn($"Ignored profile with empty process name: {profile.Name}");
                    continue;
                }

                if (!profile.ProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    profile.ProcessName = $"{profile.ProcessName}.exe";
                    changed = true;
                }

                if (!profile.DefaultIme.Equals("en", StringComparison.OrdinalIgnoreCase) &&
                    !profile.DefaultIme.Equals("zh", StringComparison.OrdinalIgnoreCase))
                {
                    profile.DefaultIme = "en";
                    changed = true;
                    _logger.Warn($"Profile {profile.Name} had invalid defaultIme; reset to en.");
                }

                validProfiles.Add(profile);
            }

            if (validProfiles.Count == 0)
            {
                settings.Profiles = AppSettings.CreateDefaultProfiles();
                changed = true;
                _logger.Warn("All profiles were invalid; restored default profiles.");
            }
            else if (validProfiles.Count != settings.Profiles.Count)
            {
                settings.Profiles = validProfiles;
                changed = true;
            }
        }

        if (settings.Hotkeys is null)
        {
            settings.Hotkeys = new HotkeySettings();
            changed = true;
            _logger.Warn("Hotkeys were missing; restored default hotkeys.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(settings.Hotkeys.SwitchEnglish))
            {
                settings.Hotkeys.SwitchEnglish = "Ctrl+Alt+E";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(settings.Hotkeys.SwitchChinese))
            {
                settings.Hotkeys.SwitchChinese = "Ctrl+Alt+C";
                changed = true;
            }
        }

        if (settings.FloatingStatus is null)
        {
            settings.FloatingStatus = new FloatingStatusSettings();
            changed = true;
            _logger.Warn("Floating status settings were missing; restored defaults.");
        }
        else
        {
            if (settings.FloatingStatus.Width < 120 || settings.FloatingStatus.Width > 360)
            {
                settings.FloatingStatus.Width = 200;
                changed = true;
            }

            if (settings.FloatingStatus.Height < 36 || settings.FloatingStatus.Height > 90)
            {
                settings.FloatingStatus.Height = 48;
                changed = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.TargetEnglishHkl) && !ImeService.TryParseHkl(settings.TargetEnglishHkl, out _))
        {
            settings.TargetEnglishHkl = "";
            changed = true;
            _logger.Warn("Invalid targetEnglishHkl was cleared.");
        }

        if (!string.IsNullOrWhiteSpace(settings.TargetChineseHkl) && !ImeService.TryParseHkl(settings.TargetChineseHkl, out _))
        {
            settings.TargetChineseHkl = "";
            changed = true;
            _logger.Warn("Invalid targetChineseHkl was cleared.");
        }

        return changed;
    }
}
