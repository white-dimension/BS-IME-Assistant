namespace BS.IME.Assistant.Models;

public sealed class FloatingStatusSettings
{
    public bool Enabled { get; set; } = true;
    public double Left { get; set; } = 1400;
    public double Top { get; set; } = 120;
    public double Width { get; set; } = 180;
    public double Height { get; set; } = 40;
    public bool Topmost { get; set; } = true;
}
