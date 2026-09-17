using MyStickies.Models;

namespace MyStickies.Tests;

public class RelativeTimeTests
{
    private static readonly DateTime Now = new(2026, 9, 17, 20, 0, 0);

    [Theory]
    [InlineData(0, "방금")]
    [InlineData(30, "방금")]
    [InlineData(60, "1분 전")]
    [InlineData(23 * 60, "23분 전")]
    [InlineData(3600, "1시간 전")]
    [InlineData(15 * 3600, "15시간 전")]
    [InlineData(86400, "1일 전")]
    [InlineData(29 * 86400, "29일 전")]
    public void Format_UsesKoreanRelativeUnits(int secondsAgo, string expected)
    {
        Assert.Equal(expected, RelativeTime.Format(Now.AddSeconds(-secondsAgo), Now));
    }

    [Fact]
    public void Format_FallsBackToDateAfter30Days()
    {
        Assert.Equal("2026-08-01", RelativeTime.Format(new DateTime(2026, 8, 1, 9, 0, 0), Now));
    }

    [Fact]
    public void Format_TreatsFutureAsNow()
    {
        Assert.Equal("방금", RelativeTime.Format(Now.AddMinutes(5), Now));
    }
}

public class NotePreviewTests
{
    [Fact]
    public void Preview_JoinsLinesWithSpaces()
    {
        var note = new Note { Body = "- 사과\r\n- 바나나 4개\n\n- 땅콩  " };
        Assert.Equal("- 사과 - 바나나 4개 - 땅콩", note.Preview);
    }

    [Fact]
    public void Preview_IsEmptyForEmptyBody()
    {
        Assert.Equal(string.Empty, new Note().Preview);
    }

    [Fact]
    public void ArchivedAt_Change_RaisesIsArchived()
    {
        var note = new Note();
        var changed = new List<string?>();
        note.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        note.ArchivedAt = DateTime.Now;

        Assert.Contains(nameof(Note.IsArchived), changed);
        Assert.True(note.IsArchived);
    }
}
