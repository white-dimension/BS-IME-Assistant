using System.Text.Json;
using System.Windows;
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
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private AppSettings _settings;

    public SettingsWindow(
        AppSettings settings,
        IReadOnlyList<InputLanguageInfo> languages,
        SettingsService settingsService,
        Logger logger,
        Action resetCapsulePosition,
        Action clampCapsulePosition)
    {
        InitializeComponent();

        _settings = Clone(settings);
        _languages = languages;
        _settingsService = settingsService;
        _logger = logger;
        _resetCapsulePosition = resetCapsulePosition;
        _clampCapsulePosition = clampCapsulePosition;

        LoadControls();
    }

    public event Action<AppSettings>? ApplyRequested;

    public AppSettings Settings => _settings;

    private void LoadControls()
    {
        EnabledCheckBox.IsChecked = _settings.Enabled;
        StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
        CadIntegrationEnabledCheckBox.IsChecked = _settings.CadIntegration.Enabled;
        CadPromptOnDetectCheckBox.IsChecked = _settings.CadIntegration.PromptOnDetect;

        ChineseImeComboBox.ItemsSource = _languages;
        EnglishImeComboBox.ItemsSource = _languages;
        ChineseImeComboBox.SelectionChanged += (_, _) => UpdateHklPreview();
        EnglishImeComboBox.SelectionChanged += (_, _) => UpdateHklPreview();
        SelectIme(ChineseImeComboBox, _settings.TargetChineseHkl, preferChinese: true);
        SelectIme(EnglishImeComboBox, _settings.TargetEnglishHkl, preferChinese: false);
        UpdateHklPreview();

        ProfilesList.ItemsSource = _settings.Profiles;

        CapsuleEnabledCheckBox.IsChecked = _settings.FloatingStatus.Enabled;
        CapsuleTopmostCheckBox.IsChecked = _settings.FloatingStatus.Topmost;
        CapsuleWidthTextBox.Text = _settings.FloatingStatus.Width.ToString("0");
        CapsuleHeightTextBox.Text = _settings.FloatingStatus.Height.ToString("0");
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyCurrentSettings();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadControls())
        {
            return;
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
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

    private bool TryReadControls()
    {
        if (!TryReadNumber(CapsuleWidthTextBox.Text, 132, 360, "胶囊宽度", out var width) ||
            !TryReadNumber(CapsuleHeightTextBox.Text, 36, 90, "胶囊高度", out var height))
        {
            return false;
        }

        _settings.Enabled = EnabledCheckBox.IsChecked == true;
        _settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;

        _settings.FloatingStatus.Enabled = CapsuleEnabledCheckBox.IsChecked == true;
        _settings.FloatingStatus.Topmost = CapsuleTopmostCheckBox.IsChecked == true;
        _settings.FloatingStatus.Width = width;
        _settings.FloatingStatus.Height = height;

        _settings.CadIntegration.Enabled = CadIntegrationEnabledCheckBox.IsChecked == true;
        _settings.CadIntegration.PromptOnDetect = CadPromptOnDetectCheckBox.IsChecked == true;

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

    private void UpdateHklPreview()
    {
        ChineseHklTextBlock.Text = ChineseImeComboBox.SelectedItem is InputLanguageInfo chinese
            ? $"高级信息 HKL: {chinese.Hkl}"
            : "高级信息 HKL: 未选择";

        EnglishHklTextBlock.Text = EnglishImeComboBox.SelectedItem is InputLanguageInfo english
            ? $"高级信息 HKL: {english.Hkl}"
            : "高级信息 HKL: 未选择";
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

    private bool TryReadNumber(string value, double min, double max, string label, out double number)
    {
        if (!double.TryParse(value, out number) || number < min || number > max)
        {
            System.Windows.MessageBox.Show(this, $"{label}需要在 {min:0}-{max:0} 之间。", "BS IME Assistant", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    private AppSettings Clone(AppSettings source)
    {
        var json = JsonSerializer.Serialize(source, _jsonOptions);
        return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? AppSettings.CreateDefault();
    }
}
