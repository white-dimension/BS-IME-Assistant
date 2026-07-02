using System.Windows;
using System.Windows.Interop;
using BS.IME.Assistant.Services;

namespace BS.IME.Assistant;

public partial class App : System.Windows.Application
{
    private AppController? _controller;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var window = new MainWindow();
        var handle = new WindowInteropHelper(window).EnsureHandle();
        _controller = new AppController(window);

        DispatcherUnhandledException += (_, args) =>
        {
            _controller?.Logger.Error("Unhandled dispatcher exception.", args.Exception);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                _controller?.Logger.Error("Unhandled application exception.", ex);
            }
        };

        _controller.Start(handle);
        window.Hide();
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        _controller?.Dispose();
    }
}
