namespace BS.IME.Assistant.Models;

public sealed class InputLanguageInfo
{
    public string Hkl { get; set; } = "";
    public string CultureName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string ImeDescription { get; set; } = "";
    public bool IsChinese { get; set; }
    public bool IsEnglish { get; set; }

    public string FriendlyName => string.IsNullOrWhiteSpace(ImeDescription)
        ? DisplayName
        : $"{DisplayName} - {ImeDescription}";
}
