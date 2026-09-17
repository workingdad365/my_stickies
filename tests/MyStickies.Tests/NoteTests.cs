using MyStickies.Models;

namespace MyStickies.Tests;

public class NoteTests
{
    [Fact]
    public void DefaultTitle_HasExpectedFormat()
    {
        var now = new DateTime(2026, 9, 17, 19, 5, 0);
        Assert.Equal("새 메모 (2026-09-17 19:05)", Note.DefaultTitle(now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureTitle_FillsDefaultWhenBlank(string blank)
    {
        var note = new Note { Title = blank };
        var now = new DateTime(2026, 1, 2, 3, 4, 0);

        note.EnsureTitle(now);

        Assert.Equal(Note.DefaultTitle(now), note.Title);
    }

    [Fact]
    public void EnsureTitle_KeepsExistingTitle()
    {
        var note = new Note { Title = "장보기" };

        note.EnsureTitle(DateTime.Now);

        Assert.Equal("장보기", note.Title);
    }

    [Fact]
    public void Title_Change_RaisesLabelPropertyChanged()
    {
        var note = new Note();
        var changed = new List<string?>();
        note.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        note.Title = "abc";

        Assert.Contains(nameof(Note.Title), changed);
        Assert.Contains(nameof(Note.Label), changed);
        Assert.Equal("ABC", note.Label);
    }
}
