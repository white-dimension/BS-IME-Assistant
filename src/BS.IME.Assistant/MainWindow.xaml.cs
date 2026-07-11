using System.Windows;

namespace BS.IME.Assistant;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public void UpdateInfo(bool enabled, string chineseHkl, string englishHkl, string settingsPath, string logPath)
    {
        InfoText.Text =
            $"当前状态：{(enabled ? "运行中" : "已暂停")}{Environment.NewLine}" +
            $"中文输入法 HKL：{DisplayOrUnknown(chineseHkl)}{Environment.NewLine}" +
            $"英文输入法 HKL：{DisplayOrUnknown(englishHkl)}{Environment.NewLine}" +
            $"配置文件路径：{settingsPath}{Environment.NewLine}" +
            $"日志路径：{logPath}";
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private static string DisplayOrUnknown(string value) => string.IsNullOrWhiteSpace(value) ? "未检测到" : value;
}
