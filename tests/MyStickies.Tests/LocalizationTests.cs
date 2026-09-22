using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MyStickies.Controls;
using MyStickies.Data;
using MyStickies.Localization;
using MyStickies.Models;
using MyStickies.Update;
using MyStickies.Windows;

namespace MyStickies.Tests;

public class LocalizationTests
{
    [Fact]
    public void Catalogs_HaveMatchingKeysAndFormatArguments()
    {
        var korean = Strings.Catalog("ko");
        var english = Strings.Catalog("en");
        Assert.Equal(korean.Keys.Order(), english.Keys.Order());
        foreach (var key in korean.Keys)
        {
            Assert.False(string.IsNullOrWhiteSpace(english[key]), key);
            Assert.DoesNotMatch("[가-힣]", english[key]);
            var koArgs = Regex.Matches(korean[key], @"\{\d+(?=[:}])").Select(m => m.Value).Order();
            var enArgs = Regex.Matches(english[key], @"\{\d+(?=[:}])").Select(m => m.Value).Order();
            Assert.Equal(koArgs, enArgs);
        }
    }

    [Theory]
    [InlineData(0, "Just now")]
    [InlineData(60, "1 minute ago")]
    [InlineData(120, "2 minutes ago")]
    [InlineData(3600, "1 hour ago")]
    [InlineData(7200, "2 hours ago")]
    [InlineData(86400, "1 day ago")]
    [InlineData(172800, "2 days ago")]
    public void English_RelativeTimeUsesSingularAndPlural(int seconds, string expected)
    {
        try
        {
            Strings.SetLanguage("en");
            var now = new DateTime(2026, 9, 22, 12, 0, 0);
            Assert.Equal(expected, RelativeTime.Format(now.AddSeconds(-seconds), now));
        }
        finally { Strings.SetLanguage("ko"); }
    }

    [Fact]
    public void ChangingLanguage_OnlyChangesNewTitlesAndWelcomeNotes()
    {
        var existing = new Note { Title = "직접 쓴 제목", Body = "직접 쓴 본문" };
        try
        {
            Strings.SetLanguage("en");
            Assert.Equal("New note (2026-09-22 12:30)", Note.DefaultTitle(new DateTime(2026, 9, 22, 12, 30, 0)));
            var notes = SampleNotes.Create();
            Assert.Equal(4, notes.Count);
            Assert.Equal("Welcome", notes[0].Title);
            Assert.All(notes, n => Assert.DoesNotMatch("[가-힣]", n.Title + n.Body));
            Assert.Equal("직접 쓴 제목", existing.Title);
            Assert.Equal("직접 쓴 본문", existing.Body);
        }
        finally { Strings.SetLanguage("ko"); }
    }

    [Fact]
    public void UnsupportedLanguage_FallsBackToKorean()
    {
        try
        {
            Strings.SetLanguage("en");
            Strings.SetLanguage("invalid");
            Assert.Equal("설정", Strings.Get("Settings"));
        }
        finally { Strings.SetLanguage("ko"); }
    }

    [Fact]
    public void OpenWindows_SwitchLanguageAndPreserveAnEditingDraft()
    {
        WpfTestHost.Run(() =>
        {
            FloatingNoteWindow? window = null;
            DataLocationWindow? settings = null;
            LanguageWindow? chooser = null;
            try
            {
                Strings.SetLanguage("ko");
                window = new FloatingNoteWindow(new Note { Title = "내용 보존", Body = "기존 본문" });
                var card = (NoteTab)window.FindName("NoteView");
                WpfTestHost.Flush();
                Assert.Equal("고정 메모 · 내용 보존", window.Title);
                card.BeginEdit(false);
                var editor = (TextBox)card.FindName("BodyBox");
                editor.Text = "저장 전 편집 내용";

                Strings.SetLanguage("en");
                WpfTestHost.Flush();
                Assert.Equal("Pinned note · 내용 보존", window.Title);
                Assert.Equal("Unpin and return to the deck", ((Border)card.FindName("PinButton")).ToolTip);
                Assert.Equal("Hide", ((Border)card.FindName("CompleteButton")).ToolTip);
                Assert.True(card.IsEditing);
                Assert.Equal("저장 전 편집 내용", editor.Text);

                settings = new DataLocationWindow(new AppSettings { Language = "en" }, firstRun: true);
                WpfTestHost.Flush();
                Assert.Equal("Note storage", settings.Title);
                Assert.Equal("en", settings.SelectedLanguage);
                Assert.Equal("Use default folder", ((Button)settings.FindName("CancelButton")).Content);

                chooser = new LanguageWindow();
                var options = (ComboBox)chooser.FindName("LanguageBox");
                Assert.Equal(new[] { "ko", "en" }, options.Items.Cast<ComboBoxItem>().Select(i => (string)i.Tag));
                options.SelectedValue = "en";
                Assert.Equal("en", chooser.SelectedLanguage);

                Strings.SetLanguage("ko");
                WpfTestHost.Flush();
                Assert.Equal("고정 메모 · 내용 보존", window.Title);
                Assert.Equal("고정 해제하여 북마크로 돌려놓기", ((Border)card.FindName("PinButton")).ToolTip);
            }
            finally
            {
                window?.CloseWithoutSaving();
                settings?.Close();
                chooser?.Close();
                Strings.SetLanguage("ko");
            }
        });
    }

    [Fact]
    public void EnglishManagerAndUpdateWindow_LoadTemplatesAndRefreshLocalizedDates()
    {
        WpfTestHost.Run(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "mystickies_language_" + Guid.NewGuid().ToString("N"));
            NoteStore? store = null;
            AllNotesWindow? manager = null;
            UpdateWindow? update = null;
            try
            {
                Strings.SetLanguage("en");
                store = new NoteStore(new NoteRepository(Path.Combine(directory, "notes.db")));
                manager = new AllNotesWindow(store);
                ((ListBox)manager.FindName("NoteList")).SelectedIndex = 0;
                update = new UpdateWindow(new UpdateInfo(new Version(1, 0, 9), "v1.0.9", "", "", 0, "Release details"));
                WpfTestHost.Flush();
                var content = (FrameworkElement)manager.Content;
                content.Measure(new Size(960, 620));
                content.Arrange(new Rect(0, 0, 960, 620));
                WpfTestHost.Flush();

                Assert.EndsWith("All notes", manager.Title);
                Assert.Equal("4 notes", ((TextBlock)manager.FindName("CountText")).Text);
                Assert.Contains(TextBlocks(content), t => t.Text == "Active");
                Assert.Contains(TextBlocks(content), t => t.Text.StartsWith("Updated "));
                Assert.Equal("Install now", ((Button)update.FindName("InstallButton")).Content);

                Strings.SetLanguage("ko");
                WpfTestHost.Flush();
                Assert.EndsWith("메모 관리", manager.Title);
                Assert.Equal("4개", ((TextBlock)manager.FindName("CountText")).Text);
                Assert.Contains(TextBlocks(content), t => t.Text == "활성");
                Assert.Contains(TextBlocks(content), t => t.Text.StartsWith("수정 "));
                Assert.Equal("지금 설치", ((Button)update.FindName("InstallButton")).Content);
                Assert.Equal("Release details", ((TextBlock)update.FindName("Notes")).Text);
            }
            finally
            {
                manager?.Close();
                update?.Close();
                store?.Dispose();
                Strings.SetLanguage("ko");
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
        });
    }

    private static IEnumerable<TextBlock> TextBlocks(DependencyObject root)
    {
        if (root is TextBlock text) yield return text;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in TextBlocks(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
}
