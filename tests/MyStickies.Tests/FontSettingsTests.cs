using System.Windows;
using System.Windows.Media;
using MyStickies.Layout;

namespace MyStickies.Tests;

public class FontSettingsTests
{
    [Fact]
    public void DerivedSizes_FollowBodySize()
    {
        Assert.Equal(16, FontSettings.TitleSize(14));
        Assert.Equal(22, FontSettings.BodyLineHeight(14));
        Assert.Equal(22, FontSettings.DetailTitleSize(14));
        Assert.Equal(15, FontSettings.DetailBodySize(14));
        Assert.Equal(26, FontSettings.DetailBodyLineHeight(14));
    }

    [Theory]
    [InlineData(5, 10)]
    [InlineData(14, 14)]
    [InlineData(99, 24)]
    public void ClampSize_KeepsWithinRange(int input, int expected)
    {
        Assert.Equal(expected, FontSettings.ClampSize(input));
    }

    [Fact]
    public void Apply_WritesAllResources()
    {
        var res = new ResourceDictionary();

        FontSettings.Apply(res, "Consolas", 18);

        Assert.Equal("Consolas", ((FontFamily)res["NoteFont"]).Source);
        Assert.Equal(18.0, res["NoteBodySize"]);
        Assert.Equal(20.0, res["NoteTitleSize"]);
        Assert.Equal(FontSettings.BodyLineHeight(18), res["NoteBodyLineHeight"]);
        Assert.Equal(26.0, res["DetailTitleSize"]);
        Assert.Equal(19.0, res["DetailBodySize"]);
        Assert.Equal(FontSettings.DetailBodyLineHeight(18), res["DetailBodyLineHeight"]);
    }

    [Fact]
    public void Apply_FallsBackToDefaultFamilyForBlankName()
    {
        var res = new ResourceDictionary();
        FontSettings.Apply(res, "  ", 14);
        Assert.Equal(FontSettings.DefaultFamily, ((FontFamily)res["NoteFont"]).Source);
    }

    [Fact]
    public void InstalledFonts_IsSortedAndContainsDefault()
    {
        var fonts = FontSettings.InstalledFonts();

        Assert.NotEmpty(fonts);
        Assert.Contains(fonts, f => string.Equals(f.Source, FontSettings.DefaultFamily, StringComparison.OrdinalIgnoreCase));
        var names = fonts.Select(f => f.Name).ToList();
        Assert.Equal(names.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase), names);
    }
}
