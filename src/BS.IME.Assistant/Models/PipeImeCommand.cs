namespace BS.IME.Assistant.Models;

public sealed class PipeImeCommand
{
    public string Source { get; set; } = "";
    public string Event { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public string PreferredIme { get; set; } = "";
    public string Mode { get; set; } = "";
}
