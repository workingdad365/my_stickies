using MyStickies.Data;
using MyStickies.Models;

namespace MyStickies.Tests;

public class NoteExporterTests
{
    private static Note Sample() => new()
    {
        Title = "장보기",
        Body = "- 사과\n- 바나나 4개",
        ColorHex = NotePalette.Green,
        CreatedAt = new DateTime(2026, 9, 17, 19, 25, 41),
        UpdatedAt = new DateTime(2026, 9, 17, 20, 0, 0),
    };

    [Fact]
    public void ToMarkdown_HasHeadingBodyAndMetadata()
    {
        var md = NoteExporter.ToMarkdown(Sample());

        Assert.StartsWith("# 장보기", md);
        Assert.Contains("- 사과", md);
        Assert.Contains("<!-- mystickies:", md);
        Assert.Contains("color=#BFEBD6", md);
        Assert.Contains("archived=false", md);
    }

    [Fact]
    public void ToMarkdown_Multiple_SeparatesWithRule()
    {
        var md = NoteExporter.ToMarkdown([Sample(), new Note { Title = "둘째" }]);

        Assert.Contains("# 장보기", md);
        Assert.Contains("# 둘째", md);
        Assert.Contains("---", md);
    }

    [Fact]
    public void ToPlainText_HasTitleBlankLineBody()
    {
        var text = NoteExporter.ToPlainText(Sample());
        Assert.Equal("장보기" + Environment.NewLine + Environment.NewLine + "- 사과\n- 바나나 4개" + Environment.NewLine, text);
    }

    [Fact]
    public void Parse_RoundTripsExportedMarkdown()
    {
        var md = NoteExporter.ToMarkdown(Sample());

        var (title, body) = NoteExporter.Parse(md, "ignored.md");

        Assert.Equal("장보기", title);
        Assert.Equal("- 사과\n- 바나나 4개", body);
    }

    [Fact]
    public void Parse_UsesFileNameWhenNoHeading()
    {
        var (title, body) = NoteExporter.Parse("첫 줄\r\n둘째 줄\r\n", @"C:\notes\회의 메모.txt");

        Assert.Equal("회의 메모", title);
        Assert.Equal("첫 줄\n둘째 줄", body);
    }

    [Fact]
    public void Parse_EmptyContent_GivesEmptyBody()
    {
        var (title, body) = NoteExporter.Parse("", "empty.md");
        Assert.Equal("empty", title);
        Assert.Equal(string.Empty, body);
    }
}
