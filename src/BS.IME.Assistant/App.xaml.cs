using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using BS.IME.Assistant.Services;

namespace BS.IME.Assistant;

public partial class App : System.Windows.Application
{
    private static Mutex? _singleInstanceMutex;
    private AppController? _controller;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        WaitForPreviousInstance(e.Args);

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

    private static void WaitForPreviousInstance(string[] args)
    {
        if (args.Length != 2 ||
            !args[0].Equals("--wait-for-process", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(args[1], out var processId) ||
            processId == Environment.ProcessId)
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            process.WaitForExit(10_000);
        }
        catch (ArgumentException)
        {
            // The previous process has already exited.
        }
        catch (InvalidOperationException)
        {
            // The previous process exited while it was being inspected.
        }
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        _controller?.Dispose();
        _singleInstanceMutex?.Dispose();
    }
}
