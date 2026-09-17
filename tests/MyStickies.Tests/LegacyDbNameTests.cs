using MyStickies.Data;
using MyStickies.Models;

namespace MyStickies.Tests;

public sealed class LegacyDbNameTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"mystickies_legacy_{Guid.NewGuid():N}");
    private NoteStore? _store;

    public LegacyDbNameTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        _store?.Dispose();
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void DbFileName_IsMyStickies()
    {
        Assert.Equal("my_stickies.db", NoteRepository.DbFileName);
        Assert.Equal("notes.db", NoteRepository.LegacyDbFileName);
    }

    [Fact]
    public void Initialize_RenamesLegacyNotesDbAndKeepsData()
    {
        var legacyPath = Path.Combine(_dir, NoteRepository.LegacyDbFileName);
        var legacyRepo = new NoteRepository(legacyPath);
        legacyRepo.Initialize();
        legacyRepo.Insert(new Note { Title = "기존 메모" });

        var newPath = NoteRepository.PathFor(_dir);
        var repo = new NoteRepository(newPath);
        repo.Initialize();

        Assert.True(File.Exists(newPath));
        Assert.False(File.Exists(legacyPath));
        Assert.Equal("기존 메모", Assert.Single(repo.LoadAll()).Title);
    }

    [Fact]
    public void NoteStore_WithLegacyFile_IsNotFirstRun()
    {
        var legacyRepo = new NoteRepository(Path.Combine(_dir, NoteRepository.LegacyDbFileName));
        legacyRepo.Initialize();
        legacyRepo.Insert(new Note { Title = "기존 메모" });

        _store = new NoteStore(new NoteRepository(NoteRepository.PathFor(_dir)));

        var note = Assert.Single(_store.Notes);
        Assert.Equal("기존 메모", note.Title);
    }

    [Fact]
    public void RenameLegacyFile_DoesNothingWhenNewFileExists()
    {
        var newPath = NoteRepository.PathFor(_dir);
        File.WriteAllText(newPath, "");
        File.WriteAllText(Path.Combine(_dir, NoteRepository.LegacyDbFileName), "");

        Assert.False(NoteRepository.RenameLegacyFile(newPath));
        Assert.True(File.Exists(Path.Combine(_dir, NoteRepository.LegacyDbFileName)));
    }
}
