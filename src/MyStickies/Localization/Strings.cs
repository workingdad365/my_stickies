using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;

namespace MyStickies.Localization;

/// <summary>한국어·영어 문구와 실행 중 언어 변경 알림을 제공함</summary>
public sealed class Strings : INotifyPropertyChanged
{
    private static readonly IReadOnlyDictionary<string, string> Korean = Load("ko");
    private static readonly IReadOnlyDictionary<string, string> English = Load("en");
    public static Strings Current { get; } = new();
    public string Language { get; private set; } = "ko";
    public CultureInfo Culture => CultureInfo.GetCultureInfo(Language == "en" ? "en-US" : "ko-KR");
    public string this[string key] => Catalog(Language).TryGetValue(key, out var value) ? value : key;
    public event PropertyChangedEventHandler? PropertyChanged;
    public static event Action? LanguageChanged;

    public static string Normalize(string? language) => language == "en" ? "en" : "ko";
    public static IReadOnlyDictionary<string, string> Catalog(string language) => language == "en" ? English : Korean;
    public static string Get(string key, params object[] args) =>
        args.Length == 0 ? Current[key] : string.Format(Current.Culture, Current[key], args);

    public static void SetLanguage(string? language)
    {
        var normalized = Normalize(language);
        if (Current.Language == normalized) return;
        Current.Language = normalized;
        Current.PropertyChanged?.Invoke(Current, new PropertyChangedEventArgs("Item[]"));
        LanguageChanged?.Invoke();
    }

    public static void Bind(DependencyObject target, DependencyProperty property, string key) =>
        BindingOperations.SetBinding(target, property, CreateBinding(key));

    internal static Binding CreateBinding(string key) => new($"[{key}]") { Source = Current, Mode = BindingMode.OneWay };

    private static IReadOnlyDictionary<string, string> Load(string language)
    {
        using var stream = typeof(Strings).Assembly.GetManifestResourceStream($"MyStickies.Localization.Strings.{language}.json")
                           ?? throw new InvalidOperationException($"Missing language resource: {language}");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
}
