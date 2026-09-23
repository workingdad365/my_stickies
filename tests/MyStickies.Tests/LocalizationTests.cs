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
    [Theory]
    [InlineData("ko-KR", "ko")]
    [InlineData("en-US", "en")]
    [InlineData("zh-CN", "zh-CN")]
    [InlineData("zh-SG", "zh-CN")]
    [InlineData("zh-TW", "zh-CN")]
    [InlineData("ja-JP", "ja")]
    [InlineData("fr-FR", "en")]
    public void FirstRun_SuggestsSupportedLanguageFromWindows(string culture, string language)
    {
        Assert.Equal(language, Strings.PreferredLanguage(System.Globalization.CultureInfo.GetCultureInfo(culture)));
    }

    [Theory]
    [InlineData("zh-CN", "zh-CN", "刚刚", "1 分钟前", "2 小时前", "3 天前")]
    [InlineData("ja", "ja-JP", "たった今", "1 分前", "2 時間前", "3 日前")]
    public void NewLanguages_UseLocalizedCultureAndRelativeTimes(string language, string culture,
        string justNow, string minute, string hours, string days)
    {
        try
        {
            Strings.SetLanguage(language);
            var now = new DateTime(2026, 9, 23, 12, 0, 0);
            Assert.Equal(culture, Strings.Current.Culture.Name);
            Assert.Equal(justNow, RelativeTime.Format(now, now));
            Assert.Equal(minute, RelativeTime.Format(now.AddMinutes(-1), now));
            Assert.Equal(hours, RelativeTime.Format(now.AddHours(-2), now));
            Assert.Equal(days, RelativeTime.Format(now.AddDays(-3), now));
        }
        finally { Strings.SetLanguage("ko"); }
    }

    [Theory]
    [InlineData("en")]
    [InlineData("zh-CN")]
    [InlineData("ja")]
    public void Catalogs_HaveMatchingKeysAndFormatArguments(string language)
    {
        var korean = Strings.Catalog("ko");
        var english = Strings.Catalog(language);
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

    [Theory]
    [InlineData("en", "New note", "Welcome")]
    [InlineData("zh-CN", "新便笺", "欢迎使用")]
    [InlineData("ja", "新しいメモ", "ようこそ")]
    public void ChangingLanguage_OnlyChangesNewTitlesAndWelcomeNotes(string language, string title, string welcome)
    {
        var existing = new Note { Title = "직접 쓴 제목", Body = "직접 쓴 본문" };
        try
        {
            Strings.SetLanguage(language);
            Assert.Equal($"{title} (2026-09-22 12:30)", Note.DefaultTitle(new DateTime(2026, 9, 22, 12, 30, 0)));
            var notes = SampleNotes.Create();
            Assert.Equal(4, notes.Count);
            Assert.Equal(welcome, notes[0].Title);
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

    [Theory]
    [InlineData("en", "Pinned note", "Unpin and return to the deck", "Hide", "Note storage", "Use default folder")]
    [InlineData("zh-CN", "固定便笺", "取消固定并放回便笺栏", "隐藏", "便笺存储", "使用默认文件夹")]
    [InlineData("ja", "固定メモ", "固定を解除してメモ一覧に戻す", "隠す", "メモの保存先", "既定のフォルダーを使用")]
    public void OpenWindows_SwitchLanguageAndPreserveAnEditingDraft(string language, string pinned,
        string unpin, string hide, string storage, string defaultFolder)
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

                Strings.SetLanguage(language);
                WpfTestHost.Flush();
                Assert.Equal($"{pinned} · 내용 보존", window.Title);
                Assert.Equal(unpin, ((Border)card.FindName("PinButton")).ToolTip);
                Assert.Equal(hide, ((Border)card.FindName("CompleteButton")).ToolTip);
                Assert.True(card.IsEditing);
                Assert.Equal("저장 전 편집 내용", editor.Text);

                settings = new DataLocationWindow(new AppSettings { Language = language }, firstRun: true);
                WpfTestHost.Flush();
                Assert.Equal(storage, settings.Title);
                Assert.Equal(language, settings.SelectedLanguage);
                Assert.Equal(defaultFolder, ((Button)settings.FindName("CancelButton")).Content);
                var settingsOptions = (ComboBox)settings.FindName("LanguageBox");
                Assert.Equal(new[] { "ko", "en", "zh-CN", "ja" }, settingsOptions.Items.Cast<ComboBoxItem>().Select(i => (string)i.Tag));

                chooser = new LanguageWindow();
                var options = (ComboBox)chooser.FindName("LanguageBox");
                Assert.Equal(new[] { "ko", "en", "zh-CN", "ja" }, options.Items.Cast<ComboBoxItem>().Select(i => (string)i.Tag));
                options.SelectedValue = language;
                Assert.Equal(language, chooser.SelectedLanguage);
                Assert.Equal(Strings.Get("ChooseLanguage"), ((TextBlock)chooser.FindName("Heading")).Text);
                Assert.Equal(Strings.Get("Continue"), ((Button)chooser.FindName("ContinueButton")).Content);
                options.SelectedValue = "ko";
                Assert.Equal("계속", ((Button)chooser.FindName("ContinueButton")).Content);
                Assert.Equal(language, Strings.Current.Language);

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

    [Theory]
    [InlineData("en", "All notes", "4 notes", "Active", "Updated ", "Install now")]
    [InlineData("zh-CN", "全部便笺", "4 张便笺", "未隐藏", "修改于 ", "立即安装")]
    [InlineData("ja", "すべてのメモ", "4 件のメモ", "表示中", "更新 ", "今すぐインストール")]
    public void ManagerAndUpdateWindow_LoadTemplatesAndRefreshLocalizedDates(string language, string title,
        string count, string active, string updated, string install)
    {
        WpfTestHost.Run(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "mystickies_language_" + Guid.NewGuid().ToString("N"));
            NoteStore? store = null;
            AllNotesWindow? manager = null;
            UpdateWindow? update = null;
            try
            {
                Strings.SetLanguage(language);
                store = new NoteStore(new NoteRepository(Path.Combine(directory, "notes.db")));
                manager = new AllNotesWindow(store);
                ((ListBox)manager.FindName("NoteList")).SelectedIndex = 0;
                update = new UpdateWindow(new UpdateInfo(new Version(1, 0, 9), "v1.0.9", "", "", 0, "Release details"));
                WpfTestHost.Flush();
                var content = (FrameworkElement)manager.Content;
                content.Measure(new Size(960, 620));
                content.Arrange(new Rect(0, 0, 960, 620));
                WpfTestHost.Flush();

                Assert.EndsWith(title, manager.Title);
                Assert.Equal(count, ((TextBlock)manager.FindName("CountText")).Text);
                Assert.Contains(TextBlocks(content), t => t.Text == active);
                Assert.Contains(TextBlocks(content), t => t.Text.StartsWith(updated));
                Assert.Equal(install, ((Button)update.FindName("InstallButton")).Content);

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
