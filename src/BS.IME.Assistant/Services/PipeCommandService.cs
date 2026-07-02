using System.IO.Pipes;
using System.Text.Json;
using BS.IME.Assistant.Models;

namespace BS.IME.Assistant.Services;

public sealed class PipeCommandService : IDisposable
{
    public const string PipeName = "BS_IME_Assistant_Pipe";
    private readonly Logger _logger;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public PipeCommandService(Logger logger)
    {
        _logger = logger;
    }

    public event Action<PipeImeCommand>? CommandReceived;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => ListenLoopAsync(_cts.Token));
        _logger.Info($"Pipe command service started: {PipeName}");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _logger.Info("Pipe command service stopped.");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to stop pipe command service.", ex);
        }

        _disposed = true;
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(cancellationToken);
                using var reader = new StreamReader(pipe);
                var line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var command = JsonSerializer.Deserialize<PipeImeCommand>(line, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (command is null)
                {
                    continue;
                }

                _logger.Info($"Pipe command received: source={command.Source}, event={command.Event}, ime={command.PreferredIme}, mode={command.Mode}");
                CommandReceived?.Invoke(command);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error("Pipe command receive failed.", ex);
                await Task.Delay(1000, cancellationToken);
            }
        }
    }
}
