using MyStickies.Data;
using MyStickies.Models;

namespace MyStickies.Tests;

public sealed class NoteRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"mystickies_test_{Guid.NewGuid():N}.db");
    private readonly NoteRepository _repo;

    public NoteRepositoryTests()
    {
        _repo = new NoteRepository(_dbPath);
        _repo.Initialize();
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    [Fact]
    public void Initialize_IsIdempotent()
    {
        _repo.Initialize();
        Assert.Empty(_repo.LoadAll());
    }

    [Fact]
    public void Insert_ThenLoadAll_RoundTripsAllFields()
    {
        var created = new DateTime(2026, 9, 17, 10, 0, 0);
        var note = new Note
        {
            Title = "업무",
            Body = "- 첫 줄\n- 둘째 줄",
            ColorHex = NotePalette.Green,
            CreatedAt = created,
            UpdatedAt = created.AddMinutes(5),
        };

        _repo.Insert(note);
        var loaded = Assert.Single(_repo.LoadAll());

        Assert.Equal(note.Id, loaded.Id);
        Assert.Equal("업무", loaded.Title);
        Assert.Equal("- 첫 줄\n- 둘째 줄", loaded.Body);
        Assert.Equal(NotePalette.Green, loaded.ColorHex);
        Assert.Equal(created, loaded.CreatedAt);
        Assert.Equal(created.AddMinutes(5), loaded.UpdatedAt);
    }

    [Fact]
    public void LoadAll_OrdersByCreatedAt()
    {
        var older = new Note { Title = "old", CreatedAt = new DateTime(2026, 1, 1) };
        var newer = new Note { Title = "new", CreatedAt = new DateTime(2026, 6, 1) };
        _repo.Insert(newer);
        _repo.Insert(older);

        var titles = _repo.LoadAll().Select(n => n.Title).ToList();

        Assert.Equal(["old", "new"], titles);
    }

    [Fact]
    public void Update_ChangesStoredValues()
    {
        var note = new Note { Title = "before", Body = "b" };
        _repo.Insert(note);

        note.Title = "after";
        note.Body = "changed";
        note.ColorHex = NotePalette.Yellow;
        note.UpdatedAt = new DateTime(2026, 9, 17, 12, 0, 0);
        _repo.Update(note);

        var loaded = Assert.Single(_repo.LoadAll());
        Assert.Equal("after", loaded.Title);
        Assert.Equal("changed", loaded.Body);
        Assert.Equal(NotePalette.Yellow, loaded.ColorHex);
        Assert.Equal(note.UpdatedAt, loaded.UpdatedAt);
    }

    [Fact]
    public void Delete_RemovesOnlyThatNote()
    {
        var a = new Note { Title = "a" };
        var b = new Note { Title = "b" };
        _repo.Insert(a);
        _repo.Insert(b);

        _repo.Delete(a.Id);

        var remaining = Assert.Single(_repo.LoadAll());
        Assert.Equal(b.Id, remaining.Id);
    }
}

public sealed class NoteRepositoryMigrationTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"mystickies_mig_{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    private void CreateV1Database()
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath};Pooling=False");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE notes (
                id TEXT PRIMARY KEY, title TEXT NOT NULL, body TEXT NOT NULL,
                color_hex TEXT NOT NULL, created_at TEXT NOT NULL, updated_at TEXT NOT NULL);
            INSERT INTO notes VALUES ('11111111-1111-1111-1111-111111111111', 'v1', 'b', '#BDD9FF',
                '2026-01-01T00:00:00.0000000+09:00', '2026-01-01T00:00:00.0000000+09:00');
            """;
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public void Initialize_MigratesV1DatabaseToCurrentVersion()
    {
        CreateV1Database();
        var repo = new NoteRepository(_dbPath);

        repo.Initialize();

        Assert.Equal(NoteRepository.SchemaVersion, repo.GetSchemaVersion());
        var note = Assert.Single(repo.LoadAll());
        Assert.Equal("v1", note.Title);
        Assert.False(note.IsArchived);
    }

    [Fact]
    public void Initialize_OnFreshDatabase_SetsCurrentVersion()
    {
        var repo = new NoteRepository(_dbPath);
        repo.Initialize();
        Assert.Equal(NoteRepository.SchemaVersion, repo.GetSchemaVersion());
    }

    [Fact]
    public void ArchivedAt_RoundTrips()
    {
        var repo = new NoteRepository(_dbPath);
        repo.Initialize();
        var note = new Note { Title = "done" };
        repo.Insert(note);

        note.ArchivedAt = new DateTime(2026, 9, 17, 20, 0, 0);
        repo.Update(note);

        var loaded = Assert.Single(repo.LoadAll());
        Assert.True(loaded.IsArchived);
        Assert.Equal(note.ArchivedAt, loaded.ArchivedAt);

        loaded.ArchivedAt = null;
        repo.Update(loaded);
        Assert.False(Assert.Single(repo.LoadAll()).IsArchived);
    }
}
