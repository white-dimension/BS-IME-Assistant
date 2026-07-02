using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace BS.IME.Assistant.Views;

public partial class FloatingStatusWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const double MinCapsuleWidth = 150;
    private const double MaxCapsuleWidth = 420;

    public FloatingStatusWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ApplyNoActivateStyle();
    }

    public event Action<double, double>? PositionChangedByUser;
    public event Action? PromptAccepted;
    public event Action? PromptDismissed;

    public void UpdateStatus(string currentIme)
    {
        StatusPanel.Visibility = Visibility.Visible;
        PromptPanel.Visibility = Visibility.Collapsed;

        if (currentIme == "中文")
        {
            BadgeText.Text = "中";
            StatusText.Text = "中文输入";
            Capsule.Background = BrushFrom("#CCB45309");
            ResizeForText(StatusText.Text);
            return;
        }

        if (currentIme == "英文")
        {
            BadgeText.Text = "EN";
            StatusText.Text = "英文输入";
            Capsule.Background = BrushFrom("#CC1D4ED8");
            ResizeForText(StatusText.Text);
            return;
        }

        BadgeText.Text = "?";
        StatusText.Text = "输入法未知";
        Capsule.Background = BrushFrom("#CC374151");
        ResizeForText(StatusText.Text);
    }

    public void UpdateCustomStatus(string badge, string text, string color)
    {
        StatusPanel.Visibility = Visibility.Visible;
        PromptPanel.Visibility = Visibility.Collapsed;
        BadgeText.Text = badge;
        StatusText.Text = text;
        Capsule.Background = BrushFrom(color);
        ResizeForText(text);
    }

    public void ShowCadPrompt()
    {
        StatusPanel.Visibility = Visibility.Collapsed;
        PromptPanel.Visibility = Visibility.Visible;
        Capsule.Background = BrushFrom("#CC155E75");
        Width = 300;
        ClampToScreen();
    }

    public void ClampToScreen()
    {
        var minLeft = SystemParameters.VirtualScreenLeft;
        var minTop = SystemParameters.VirtualScreenTop;
        var maxLeft = minLeft + SystemParameters.VirtualScreenWidth - Width;
        var maxTop = minTop + SystemParameters.VirtualScreenHeight - Height;

        Left = Math.Clamp(Left, minLeft, Math.Max(minLeft, maxLeft));
        Top = Math.Clamp(Top, minTop, Math.Max(minTop, maxTop));
    }

    private void Capsule_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
            ClampToScreen();
            PositionChangedByUser?.Invoke(Left, Top);
        }
        catch (InvalidOperationException)
        {
            // DragMove can throw if the mouse state changes during a drag.
        }
    }

    private void EnablePrompt_Click(object sender, RoutedEventArgs e)
    {
        PromptAccepted?.Invoke();
    }

    private void DismissPrompt_Click(object sender, RoutedEventArgs e)
    {
        PromptDismissed?.Invoke();
    }

    private void ApplyNoActivateStyle()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == nint.Zero)
        {
            return;
        }

        var style = GetWindowLong(handle, GwlExStyle);
        _ = SetWindowLong(handle, GwlExStyle, style | WsExToolWindow | WsExNoActivate);
    }

    private static SolidColorBrush BrushFrom(string color)
    {
        var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    private void ResizeForText(string text)
    {
        var textWeight = text.Count(c => c > 127) * 18 + text.Count(c => c <= 127) * 9;
        Width = Math.Clamp(76 + textWeight, MinCapsuleWidth, MaxCapsuleWidth);
        ClampToScreen();
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);
}
