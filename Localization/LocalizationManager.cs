using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace PcMate.Localization;

public sealed class LocalizationManager : INotifyPropertyChanged
{
    public const string SystemLanguage = "system";
    public const string EnglishLanguage = "en";
    public const string KoreanLanguage = "ko";

    private static readonly CultureInfo SystemUiCulture = CultureInfo.CurrentUICulture;
    private static readonly ResourceManager Resources = new(
        "PcMate.Localization.Strings",
        typeof(LocalizationManager).Assembly);
    private string _language = SystemLanguage;
    private CultureInfo _culture = ResolveCulture(SystemLanguage);

    private LocalizationManager()
    {
    }

    public static LocalizationManager Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Language => _language;

    public CultureInfo Culture => _culture;

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        return Resources.GetString(key, _culture) ?? key;
    }

    public string Format(string key, params object[] arguments)
    {
        return string.Format(_culture, Get(key), arguments);
    }

    public void SetLanguage(string? language)
    {
        string normalizedLanguage = NormalizeLanguage(language);
        CultureInfo culture = ResolveCulture(normalizedLanguage);
        if (_language == normalizedLanguage && _culture.Name == culture.Name)
        {
            return;
        }

        _language = normalizedLanguage;
        _culture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
    }

    public static string NormalizeLanguage(string? language)
    {
        return language?.Trim().ToLowerInvariant() switch
        {
            EnglishLanguage => EnglishLanguage,
            KoreanLanguage => KoreanLanguage,
            _ => SystemLanguage
        };
    }

    private static CultureInfo ResolveCulture(string language)
    {
        return language switch
        {
            EnglishLanguage => CultureInfo.GetCultureInfo("en"),
            KoreanLanguage => CultureInfo.GetCultureInfo("ko"),
            _ when SystemUiCulture.TwoLetterISOLanguageName.Equals(KoreanLanguage, StringComparison.OrdinalIgnoreCase)
                => CultureInfo.GetCultureInfo("ko"),
            _ => CultureInfo.GetCultureInfo("en")
        };
    }
}
