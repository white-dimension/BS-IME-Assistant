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
        AppDirectory = AppPaths.Root;
        SettingsPath = AppPaths.SettingsFile;
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

            try
            {
                if (File.Exists(SettingsPath))
                {
                    File.Copy(SettingsPath, AppPaths.SettingsBackupFile, overwrite: true);
                    _logger.Warn($"Corrupted settings backed up to {AppPaths.SettingsBackupFile}");
                }
            }
            catch (Exception backupEx)
            {
                _logger.Warn($"Failed to backup corrupted settings: {backupEx.Message}");
            }

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
            var seenProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

                if (!IsSupportedProfile(profile.ProcessName))
                {
                    changed = true;
                    _logger.Info($"Removed unsupported software profile: {profile.ProcessName}");
                    continue;
                }

                if (!seenProcesses.Add(profile.ProcessName))
                {
                    changed = true;
                    _logger.Info($"Removed duplicate software profile: {profile.ProcessName}");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(profile.Name))
                {
                    profile.Name = Path.GetFileNameWithoutExtension(profile.ProcessName);
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

            foreach (var defaultProfile in AppSettings.CreateDefaultProfiles())
            {
                if (validProfiles.Any(profile =>
                    string.Equals(profile.ProcessName, defaultProfile.ProcessName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                validProfiles.Add(defaultProfile);
                seenProcesses.Add(defaultProfile.ProcessName);
                changed = true;
                _logger.Info($"Restored required software profile: {defaultProfile.ProcessName}");
            }

            settings.Profiles = validProfiles;
        }

        if (settings.FloatingStatus is null)
        {
            settings.FloatingStatus = new FloatingStatusSettings();
            changed = true;
            _logger.Warn("Floating status settings were missing; restored defaults.");
        }
        else
        {
            if (settings.FloatingStatus.Width < 132 || settings.FloatingStatus.Width > 360)
            {
                settings.FloatingStatus.Width = 180;
                changed = true;
            }

            if (settings.FloatingStatus.Height < 36 || settings.FloatingStatus.Height > 90)
            {
                settings.FloatingStatus.Height = 40;
                changed = true;
            }
            else if (Math.Abs(settings.FloatingStatus.Height - 48) < 0.1)
            {
                settings.FloatingStatus.Height = 40;
                changed = true;
            }
        }

        if (settings.CadIntegration is null)
        {
            settings.CadIntegration = new CadIntegrationSettings();
            changed = true;
            _logger.Warn("CAD integration settings were missing; restored defaults.");
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

    private static bool IsSupportedProfile(string processName) =>
        processName.Equals("acad.exe", StringComparison.OrdinalIgnoreCase) ||
        processName.Equals("3dsmax.exe", StringComparison.OrdinalIgnoreCase);
}
