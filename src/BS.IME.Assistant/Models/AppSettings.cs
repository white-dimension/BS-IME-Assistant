namespace BS.IME.Assistant.Models;

public sealed class AppSettings
{
    public bool Enabled { get; set; } = true;
    // Reserved for a later startup registration feature.
    public bool StartWithWindows { get; set; }
    public string TargetChineseHkl { get; set; } = "";
    public string TargetEnglishHkl { get; set; } = "";
    public List<AppProfile> Profiles { get; set; } = CreateDefaultProfiles();
    public HotkeySettings Hotkeys { get; set; } = new();

    public static AppSettings CreateDefault() => new()
    {
        Enabled = true,
        StartWithWindows = false,
        TargetChineseHkl = "",
        TargetEnglishHkl = "",
        Profiles = CreateDefaultProfiles(),
        Hotkeys = new HotkeySettings()
    };

    public static List<AppProfile> CreateDefaultProfiles() =>
    [
        new() { Name = "AutoCAD", ProcessName = "acad.exe", DefaultIme = "en", SwitchOnActivate = true },
        new() { Name = "3ds Max", ProcessName = "3dsmax.exe", DefaultIme = "en", SwitchOnActivate = true },
        new() { Name = "SketchUp", ProcessName = "SketchUp.exe", DefaultIme = "en", SwitchOnActivate = true },
        new() { Name = "Rhino", ProcessName = "Rhino.exe", DefaultIme = "en", SwitchOnActivate = true },
        new() { Name = "Revit", ProcessName = "Revit.exe", DefaultIme = "en", SwitchOnActivate = true },
        new() { Name = "Photoshop", ProcessName = "Photoshop.exe", DefaultIme = "en", SwitchOnActivate = true }
    ];
}
