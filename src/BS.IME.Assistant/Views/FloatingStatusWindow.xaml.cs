using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace BS.IME.Assistant.Views;

public partial class FloatingStatusWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const double IdleOpacity = 0.72;
    private const double HoverOpacity = 0.96;

    public FloatingStatusWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ApplyNoActivateStyle();
    }

    public event Action<double, double>? PositionChangedByUser;
    public event Action? SwitchChineseRequested;
    public event Action? SwitchEnglishRequested;

    public void UpdateStatus(string currentIme)
    {
        if (IsChineseImeText(currentIme))
        {
            BadgeText.Text = "中";
            StatusText.Text = "中文输入";
            Capsule.Background = BrushFrom("#99B45309");
            return;
        }

        if (IsEnglishImeText(currentIme))
        {
            BadgeText.Text = "EN";
            StatusText.Text = "英文输入";
            Capsule.Background = BrushFrom("#991D4ED8");
            return;
        }

        BadgeText.Text = "?";
        StatusText.Text = "输入法未知";
        Capsule.Background = BrushFrom("#99374151");
    }

    public void UpdateCustomStatus(string badge, string text, string color)
    {
        BadgeText.Text = badge;
        StatusText.Text = text;
        Capsule.Background = BrushFrom(color);
    }

    public void ClampToScreen()
    {
        var minLeft = SystemParameters.VirtualScreenLeft;
        var minTop = SystemParameters.VirtualScreenTop;
        var maxLeft = minLeft + SystemParameters.VirtualScreenWidth - Width;
        var maxTop = minTop + SystemParameters.VirtualScreenHeight - Height;

        try
        {
            var source = PresentationSource.FromVisual(this);
            var toDevice = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
            var fromDevice = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            var center = toDevice.Transform(new System.Windows.Point(Left + Width / 2, Top + Height / 2));
            var screen = Forms.Screen.FromPoint(new System.Drawing.Point((int)Math.Round(center.X), (int)Math.Round(center.Y)));
            var workTopLeft = fromDevice.Transform(new System.Windows.Point(screen.WorkingArea.Left, screen.WorkingArea.Top));
            var workBottomRight = fromDevice.Transform(new System.Windows.Point(screen.WorkingArea.Right, screen.WorkingArea.Bottom));

            minLeft = workTopLeft.X;
            minTop = workTopLeft.Y;
            maxLeft = workBottomRight.X - Width;
            maxTop = workBottomRight.Y - Height;
        }
        catch
        {
            // Fall back to the virtual desktop if screen work-area lookup fails.
        }

        Left = Math.Clamp(Left, minLeft, Math.Max(minLeft, maxLeft));
        Top = Math.Clamp(Top, minTop, Math.Max(minTop, maxTop));
    }

    private void Capsule_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == Badge || e.OriginalSource == BadgeText)
        {
            return;
        }

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

    private void Badge_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        if (BadgeText.Text.Equals("中", StringComparison.OrdinalIgnoreCase) ||
            BadgeText.Text.Equals("ZH", StringComparison.OrdinalIgnoreCase))
        {
            SwitchChineseRequested?.Invoke();
            return;
        }

        if (BadgeText.Text.Equals("EN", StringComparison.OrdinalIgnoreCase) ||
            BadgeText.Text.Equals("英", StringComparison.OrdinalIgnoreCase))
        {
            SwitchEnglishRequested?.Invoke();
        }
    }

    private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        Opacity = HoverOpacity;
    }

    private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        Opacity = IdleOpacity;
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

    private static bool IsChineseImeText(string text) =>
        text.Contains("中", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("zh", StringComparison.OrdinalIgnoreCase);

    private static bool IsEnglishImeText(string text) =>
        text.Contains("英", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("en", StringComparison.OrdinalIgnoreCase);

    private static SolidColorBrush BrushFrom(string color)
    {
        var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);
}
