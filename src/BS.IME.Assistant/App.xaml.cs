using System.Windows;
using System.Windows.Interop;
using System.Threading;
using BS.IME.Assistant.Services;

namespace BS.IME.Assistant;

public partial class App : System.Windows.Application
{
    private static Mutex? _singleInstanceMutex;
    private AppController? _controller;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, "Global\\BS_IME_Assistant_SingleInstance", out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

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
        _singleInstanceMutex?.Dispose();
    }
}
