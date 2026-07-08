using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace BS.IME.Assistant.Views;

public partial class TrayMenuWindow : Window
{
    public TrayMenuWindow()
    {
        InitializeComponent();
    }

    public event Action? ToggleEnabledRequested;
    public event Action? SwitchChineseRequested;
    public event Action? SwitchEnglishRequested;
    public event Action? ShowFloatingRequested;
    public event Action? HideFloatingRequested;
    public event Action? OpenSettingsRequested;
    public event Action? OpenLogsRequested;
    public event Action? ExitRequested;

    public void UpdateState(bool enabled, string processName, string currentIme)
    {
        StatusText.Text = enabled ? "状态：运行中" : "状态：已暂停";
        ForegroundText.Text = $"当前软件：{GetFriendlyProcessName(processName)}";
        CurrentImeText.Text = $"当前输入法：{currentIme}";
        ToggleButton.Content = enabled ? "暂停自动切换" : "启用自动切换";
    }

    public void ShowAt(Forms.Screen screen, System.Drawing.Point cursorPosition)
    {
        if (!IsVisible)
        {
            Show();
        }

        UpdateLayout();

        var source = PresentationSource.FromVisual(this);
        var transform = source?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;
        var cursor = transform.Transform(new System.Windows.Point(cursorPosition.X, cursorPosition.Y));
        var workTopLeft = transform.Transform(new System.Windows.Point(screen.WorkingArea.Left, screen.WorkingArea.Top));
        var workBottomRight = transform.Transform(new System.Windows.Point(screen.WorkingArea.Right, screen.WorkingArea.Bottom));

        Left = Math.Min(Math.Max(cursor.X - ActualWidth + 8, workTopLeft.X + 8), workBottomRight.X - ActualWidth - 8);
        Top = Math.Min(Math.Max(cursor.Y - ActualHeight - 8, workTopLeft.Y + 8), workBottomRight.Y - ActualHeight - 8);

        Activate();
    }

    private void ToggleButton_Click(object sender, RoutedEventArgs e) => InvokeAndHide(ToggleEnabledRequested);

    private void ShowFloatingButton_Click(object sender, RoutedEventArgs e) => InvokeAndHide(ShowFloatingRequested);

    private void HideFloatingButton_Click(object sender, RoutedEventArgs e) => InvokeAndHide(HideFloatingRequested);

    private void SwitchChineseButton_Click(object sender, RoutedEventArgs e) => InvokeAndHide(SwitchChineseRequested);

    private void SwitchEnglishButton_Click(object sender, RoutedEventArgs e) => InvokeAndHide(SwitchEnglishRequested);

    private void OpenSettingsButton_Click(object sender, RoutedEventArgs e) => InvokeAndHide(OpenSettingsRequested);

    private void OpenLogsButton_Click(object sender, RoutedEventArgs e) => InvokeAndHide(OpenLogsRequested);

    private void ExitButton_Click(object sender, RoutedEventArgs e) => InvokeAndHide(ExitRequested);

    private void Window_Deactivated(object sender, EventArgs e) => Hide();

    private void InvokeAndHide(Action? action)
    {
        Hide();
        action?.Invoke();
    }

    private static string GetFriendlyProcessName(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return "未知";
        }

        return processName.ToLowerInvariant() switch
        {
            "acad.exe" => "AutoCAD",
            "3dsmax.exe" => "3ds Max",
            "sketchup.exe" => "SketchUp",
            "rhino.exe" => "Rhino",
            "revit.exe" => "Revit",
            "photoshop.exe" => "Photoshop",
            "bs.ime.assistant.exe" => "IME 助手",
            _ => processName
        };
    }
}
