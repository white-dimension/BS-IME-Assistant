using System.IO.Pipes;
using System.Text.Json;

namespace BS.IME.Assistant.CadBridge;

internal static class CadImeNotifier
{
    private const string PipeName = "BS_IME_Assistant_Pipe";

    public static void Notify(string eventName, string preferredIme, string mode)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var payload = JsonSerializer.Serialize(new
                {
                    source = "AutoCAD",
                    @event = eventName,
                    processName = "acad.exe",
                    preferredIme,
                    mode
                });

                using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
                await using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous);
                await pipe.ConnectAsync(timeout.Token);
                await using var writer = new StreamWriter(pipe) { AutoFlush = true };
                await writer.WriteLineAsync(payload);
            }
            catch
            {
                // The bridge must never interrupt AutoCAD when the assistant is not running.
            }
        });
    }
}
