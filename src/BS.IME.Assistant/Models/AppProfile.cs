namespace BS.IME.Assistant.Models;

public sealed class AppProfile
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public string DefaultIme { get; set; } = "en";
    public bool SwitchOnActivate { get; set; } = true;
}
