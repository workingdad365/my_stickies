using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace MyStickies.Layout;

/// <summary>
/// 메모 글꼴과 크기를 앱 리소스에 적용. 카드와 메모 관리 창은 DynamicResource로 참조하므로 즉시 반영됨.
/// 크기 계산은 본문 크기를 기준으로 파생
/// </summary>
public static class FontSettings
{
    public const string DefaultFamily = "Malgun Gothic";
    public const int DefaultSize = 14;
    public const int MinSize = 10;
    public const int MaxSize = 24;

    public static double TitleSize(int bodySize) => bodySize + 2;
    public static double BodyLineHeight(int bodySize) => Math.Round(bodySize * 1.6);
    public static double DetailTitleSize(int bodySize) => bodySize + 8;
    public static double DetailBodySize(int bodySize) => bodySize + 1;
    public static double DetailBodyLineHeight(int bodySize) => Math.Round((bodySize + 1) * 1.7);

    public static int ClampSize(int size) => Math.Clamp(size, MinSize, MaxSize);

    /// <summary>리소스 사전에 글꼴과 파생 크기 값을 기록</summary>
    public static void Apply(ResourceDictionary resources, string family, int bodySize)
    {
        var size = ClampSize(bodySize);
        resources["NoteFont"] = new FontFamily(string.IsNullOrWhiteSpace(family) ? DefaultFamily : family);
        resources["NoteBodySize"] = (double)size;
        resources["NoteTitleSize"] = TitleSize(size);
        resources["NoteBodyLineHeight"] = BodyLineHeight(size);
        resources["DetailTitleSize"] = DetailTitleSize(size);
        resources["DetailBodySize"] = DetailBodySize(size);
        resources["DetailBodyLineHeight"] = DetailBodyLineHeight(size);
    }

    /// <summary>설정 목록용 글꼴 항목. Name은 현재 언어 표시 이름, Source는 저장용 이름</summary>
    public sealed record FontChoice(string Name, string Source, FontFamily Family);

    /// <summary>시스템에 설치된 글꼴을 표시 이름 순으로 나열</summary>
    public static List<FontChoice> InstalledFonts()
    {
        var lang = XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag);
        var list = new List<FontChoice>();
        foreach (var family in Fonts.SystemFontFamilies)
        {
            var name = family.FamilyNames.TryGetValue(lang, out var localized)
                ? localized
                : family.FamilyNames.Values.FirstOrDefault() ?? family.Source;
            list.Add(new FontChoice(name, family.Source, family));
        }
        return list.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
