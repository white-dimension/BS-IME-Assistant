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
        _logger.Info($"CAD bridge pipe started: {PipeName}");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _logger.Info("CAD bridge pipe stopped.");
        _disposed = true;
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(cancellationToken);
                using var reader = new StreamReader(pipe);
                var line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(line) || line.Length > 4096)
                {
                    continue;
                }

                var command = JsonSerializer.Deserialize<PipeImeCommand>(line, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (command is null || !command.Source.Equals("AutoCAD", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                CommandReceived?.Invoke(command);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error("CAD bridge pipe receive failed.", ex);
                await Task.Delay(500, cancellationToken);
            }
        }
    }
}
