using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using BS.IME.Assistant.Models;
using BS.IME.Assistant.Services;

namespace BS.IME.Assistant.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly Logger _logger;
    private readonly IReadOnlyList<InputLanguageInfo> _languages;
    private readonly Action _resetCapsulePosition;
    private readonly Action _clampCapsulePosition;
    private readonly AppSettings _originalSettings;
    private readonly System.Windows.Threading.DispatcherTimer _autoApplyTimer;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private AppSettings _settings;
    private bool _isLoading = true;

    public SettingsWindow(
        AppSettings settings,
        IReadOnlyList<InputLanguageInfo> languages,
        SettingsService settingsService,
        Logger logger,
        Action resetCapsulePosition,
        Action clampCapsulePosition)
    {
        InitializeComponent();

        _originalSettings = Clone(settings);
        _settings = Clone(settings);
        _languages = languages;
        _settingsService = settingsService;
        _logger = logger;
        _resetCapsulePosition = resetCapsulePosition;
        _clampCapsulePosition = clampCapsulePosition;
        _autoApplyTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _autoApplyTimer.Tick += (_, _) =>
        {
            _autoApplyTimer.Stop();
            ApplyCurrentSettings();
        };

        LoadControls();
        Loaded += (_, _) => _isLoading = false;
        Closing += SettingsWindow_Closing;
    }

    public event Action<AppSettings>? ApplyRequested;

    public AppSettings Settings => _settings;

    private void LoadControls()
    {
        EnabledCheckBox.IsChecked = _settings.Enabled;
        StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;

        ChineseImeComboBox.ItemsSource = _languages;
        EnglishImeComboBox.ItemsSource = _languages;
        SelectIme(ChineseImeComboBox, _settings.TargetChineseHkl, preferChinese: true);
        SelectIme(EnglishImeComboBox, _settings.TargetEnglishHkl, preferChinese: false);

        ProfilesList.ItemsSource = _settings.Profiles;

        CapsuleEnabledCheckBox.IsChecked = _settings.FloatingStatus.Enabled;
        CapsuleTopmostCheckBox.IsChecked = _settings.FloatingStatus.Topmost;
        CapsuleWidthSlider.Value = _settings.FloatingStatus.Width;
        CapsuleHeightSlider.Value = _settings.FloatingStatus.Height;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _autoApplyTimer.Stop();
        if (!TryReadControls())
        {
            return;
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _autoApplyTimer.Stop();
        DialogResult = false;
        Close();
    }

    private void SettingsWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _autoApplyTimer.Stop();
        if (DialogResult != true)
        {
            ApplyRequested?.Invoke(Clone(_originalSettings));
        }
    }

    private void OpenConfigButton_Click(object sender, RoutedEventArgs e)
    {
        TrayService.OpenPath(_settingsService.SettingsPath, _logger);
    }

    private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        TrayService.OpenPath(_logger.LogDirectory, _logger);
    }

    private void ResetCapsuleButton_Click(object sender, RoutedEventArgs e)
    {
        _resetCapsulePosition();
        System.Windows.MessageBox.Show(this, "胶囊位置已重置到右下角。", "BS IME Assistant", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ClampCapsuleButton_Click(object sender, RoutedEventArgs e)
    {
        _clampCapsulePosition();
        System.Windows.MessageBox.Show(this, "胶囊位置已限制在当前屏幕内。", "BS IME Assistant", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private bool ApplyCurrentSettings()
    {
        if (!TryReadControls())
        {
            return false;
        }

        ApplyRequested?.Invoke(Clone(_settings));
        return true;
    }

    private void AutoApplyToggle_Changed(object sender, RoutedEventArgs e) => ScheduleAutoApply();

    private void AutoApplySelection_Changed(object sender, SelectionChangedEventArgs e) => ScheduleAutoApply();

    private void AutoApplySlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) => ScheduleAutoApply();

    private void ScheduleAutoApply()
    {
        if (_isLoading)
        {
            return;
        }

        _autoApplyTimer.Stop();
        _autoApplyTimer.Start();
    }

    private bool TryReadControls()
    {
        _settings.Enabled = EnabledCheckBox.IsChecked == true;
        _settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;

        _settings.FloatingStatus.Enabled = CapsuleEnabledCheckBox.IsChecked == true;
        _settings.FloatingStatus.Topmost = CapsuleTopmostCheckBox.IsChecked == true;
        _settings.FloatingStatus.Width = Math.Round(CapsuleWidthSlider.Value);
        _settings.FloatingStatus.Height = Math.Round(CapsuleHeightSlider.Value);

        if (ChineseImeComboBox.SelectedItem is InputLanguageInfo chinese)
        {
            _settings.TargetChineseHkl = chinese.Hkl;
        }

        if (EnglishImeComboBox.SelectedItem is InputLanguageInfo english)
        {
            _settings.TargetEnglishHkl = english.Hkl;
        }

        foreach (var profile in _settings.Profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.ProcessName))
            {
                System.Windows.MessageBox.Show(this, "软件规则里有空的进程名，请补充后再保存。", "BS IME Assistant", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(profile.Name))
            {
                profile.Name = Path.GetFileNameWithoutExtension(profile.ProcessName);
            }

            if (!profile.ProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                profile.ProcessName += ".exe";
            }

            if (!profile.DefaultIme.Equals("zh", StringComparison.OrdinalIgnoreCase))
            {
                profile.DefaultIme = "en";
            }
        }

        return true;
    }

    private void SelectIme(System.Windows.Controls.ComboBox comboBox, string hkl, bool preferChinese)
    {
        var selected = _languages.FirstOrDefault(x => ImeService.HklEquals(x.Hkl, hkl));
        selected ??= preferChinese
            ? _languages.FirstOrDefault(x => x.IsChinese)
            : _languages.FirstOrDefault(x => x.IsEnglish);
        selected ??= _languages.FirstOrDefault();

        comboBox.SelectedItem = selected;
    }

    private AppSettings Clone(AppSettings source)
    {
        var json = JsonSerializer.Serialize(source, _jsonOptions);
        return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? AppSettings.CreateDefault();
    }
}
