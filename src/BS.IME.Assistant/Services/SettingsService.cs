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
            settings.Profiles ??= [];
            settings.Hotkeys ??= new HotkeySettings();
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
}
