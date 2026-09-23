using MyStickies.Data;
using MyStickies.Models;

namespace MyStickies.Tests;

public sealed class AppSettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"mystickies_settings_{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Load_ReturnsNullWhenFileMissing()
    {
        Assert.Null(AppSettings.Load(Path.Combine(_dir, "settings.json")));
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsDataDirectory()
    {
        var path = Path.Combine(_dir, "nested", "settings.json");
        var settings = new AppSettings { DataDirectory = @"D:\Sync\MyStickies" };

        settings.Save(path);
        var loaded = AppSettings.Load(path);

        Assert.NotNull(loaded);
        Assert.Equal(@"D:\Sync\MyStickies", loaded.DataDirectory);
    }

    [Fact]
    public void Load_ReturnsNullForCorruptFile()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, "{ not json");

        Assert.Null(AppSettings.Load(path));
    }

    [Theory]
    [InlineData("ko")]
    [InlineData("en")]
    [InlineData("zh-CN")]
    [InlineData("ja")]
    public void Language_RoundTripsWithoutChangingNotesLocationOrPins(string language)
    {
        var path = Path.Combine(_dir, "settings.json");
        var settings = new AppSettings { Language = language, DataDirectory = @"D:\Notes" };
        settings.PinnedNotes.Add(new PinnedNoteState(@"D:\Notes\my_stickies.db", Guid.NewGuid(), 100, 200));
        settings.Save(path);
        var restored = AppSettings.Load(path)!;
        Assert.Equal(language, restored.Language);
        Assert.Equal(settings.DataDirectory, restored.DataDirectory);
        Assert.Equal(settings.PinnedNotes, restored.PinnedNotes);
    }

    [Fact]
    public void ExistingSettingsWithoutLanguage_KeepKorean()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, """{"DataDirectory":"C:\\Notes"}""");
        Assert.Equal("ko", AppSettings.Load(path)!.Language);
    }

    [Fact]
    public void Save_ThenLoad_RestoresPinnedNotesAndPositionsAcrossDatabases()
    {
        var path = Path.Combine(_dir, "settings.json");
        var settings = new AppSettings
        {
            PinnedNotes =
            [
                new PinnedNoteState(@"C:\Notes\my_stickies.db", Guid.NewGuid(), -800, 240, 520, 360),
                new PinnedNoteState(@"D:\Other\my_stickies.db", Guid.NewGuid(), 640, 100),
            ],
        };
        settings.Save(path);

        var loaded = AppSettings.Load(path);
        Assert.NotNull(loaded);
        Assert.Equal(settings.PinnedNotes, loaded.PinnedNotes);
    }

    [Fact]
    public void Load_OldPinnedNotesWithoutSizePreservesPositionAndAutomaticSize()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "settings.json");
        var id = Guid.NewGuid();
        File.WriteAllText(path, $$"""
            {"PinnedNotes":[{"DatabasePath":"C:\\Notes\\my_stickies.db","NoteId":"{{id}}","Left":120,"Top":240}]}
            """);

        var pin = Assert.Single(AppSettings.Load(path)!.PinnedNotes);
        Assert.Equal(id, pin.NoteId);
        Assert.Equal(120, pin.Left);
        Assert.Equal(240, pin.Top);
        Assert.Null(pin.Width);
        Assert.Null(pin.Height);
    }

    [Fact]
    public void Load_OldSettingsWithoutPinsStartsWithEmptyList()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, """{"DataDirectory":"C:\\Notes"}""");

        Assert.Empty(AppSettings.Load(path)!.PinnedNotes);
    }

    [Fact]
    public void PathFor_AppendsDbFileName()
    {
        Assert.Equal(Path.Combine(@"D:\Sync", NoteRepository.DbFileName), NoteRepository.PathFor(@"D:\Sync"));
    }
}

public sealed class NoteStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"mystickies_store_{Guid.NewGuid():N}");
    private NoteStore? _store;

    public NoteStoreTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        _store?.Dispose();
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private NoteRepository Repo(string name) => new(Path.Combine(_dir, name, NoteRepository.DbFileName));

    [Fact]
    public void FirstRun_SeedsSampleNotes()
    {
        _store = new NoteStore(Repo("a"));
        Assert.Equal(SampleNotes.Create().Count, _store.Notes.Count);
    }

    [Fact]
    public void SwitchTo_NewLocation_CreatesFileWithGuideNotes()
    {
        _store = new NoteStore(Repo("a"));

        _store.SwitchTo(Repo("b"));

        Assert.Equal(SampleNotes.Create().Count, _store.Notes.Count);
        Assert.True(File.Exists(Path.Combine(_dir, "b", NoteRepository.DbFileName)));
    }

    [Fact]
    public void SwitchTo_ExistingEmptyFile_StaysEmpty()
    {
        _store = new NoteStore(Repo("a"));
        var repoB = Repo("b");
        repoB.Initialize();

        _store.SwitchTo(repoB);

        Assert.Empty(_store.Notes);
    }

    [Fact]
    public void SwitchTo_ExistingLocation_LoadsItsNotes()
    {
        var repoB = Repo("b");
        repoB.Initialize();
        repoB.Insert(new Note { Title = "from b" });

        _store = new NoteStore(Repo("a"));
        _store.SwitchTo(repoB);

        var note = Assert.Single(_store.Notes);
        Assert.Equal("from b", note.Title);
        Assert.Equal(repoB.DbPath, _store.DbPath);
    }

    [Fact]
    public void Add_And_Reload_PersistThroughRepository()
    {
        // 빈 DB 파일을 미리 만들어 두면 최초 실행 샘플이 들어가지 않음
        var repo = Repo("b");
        repo.Initialize();
        _store = new NoteStore(repo);
        Assert.Empty(_store.Notes);

        var added = _store.Add(new DateTime(2026, 9, 17, 21, 0, 0));
        _store.Reload();

        var note = Assert.Single(_store.Notes);
        Assert.Equal(added.Id, note.Id);
        Assert.Equal(Note.DefaultTitle(new DateTime(2026, 9, 17, 21, 0, 0)), note.Title);
    }
}
