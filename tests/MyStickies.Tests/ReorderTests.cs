using MyStickies.Data;
using MyStickies.Models;

namespace MyStickies.Tests;

public sealed class ReorderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"mystickies_reorder_{Guid.NewGuid():N}");
    private NoteStore? _store;

    public ReorderTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        _store?.Dispose();
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private NoteRepository Repo(string name) => new(Path.Combine(_dir, name, NoteRepository.DbFileName));

    private NoteStore EmptyStore(string name)
    {
        var repo = Repo(name);
        repo.Initialize();
        _store = new NoteStore(repo);
        return _store;
    }

    [Fact]
    public void Add_AssignsIncreasingSortOrder()
    {
        var store = EmptyStore("a");
        var t = new DateTime(2026, 9, 17, 22, 0, 0);

        var n1 = store.Add(t, "1", "");
        var n2 = store.Add(t, "2", "");
        var n3 = store.Add(t, "3", "");

        Assert.True(n1.SortOrder < n2.SortOrder && n2.SortOrder < n3.SortOrder);
    }

    [Fact]
    public void Reorder_MovesWithinOccupiedSlotsAndPersists()
    {
        var store = EmptyStore("b");
        var t = new DateTime(2026, 9, 17, 22, 0, 0);
        var a = store.Add(t, "a", "");
        var b = store.Add(t, "b", "");
        var c = store.Add(t, "c", "");
        var d = store.Add(t, "d", "");

        // 덱에 b, c, d만 보이는 상황에서 d를 맨 위로 끌어올림 -> a, d, b, c
        store.Reorder([d, b, c]);

        Assert.Equal(["a", "d", "b", "c"], store.Notes.Select(n => n.Title));
        Assert.Equal([0L, 1L, 2L, 3L], store.Notes.Select(n => n.SortOrder));

        store.Reload();
        Assert.Equal(["a", "d", "b", "c"], store.Notes.Select(n => n.Title));
    }

    [Fact]
    public void Reorder_IgnoresListWithUnknownNote()
    {
        var store = EmptyStore("c");
        var t = DateTime.Now;
        var a = store.Add(t, "a", "");
        var b = store.Add(t, "b", "");

        store.Reorder([b, new Note { Title = "ghost" }, a]);

        Assert.Equal(["a", "b"], store.Notes.Select(n => n.Title));
    }
}

public sealed class SortOrderMigrationTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"mystickies_v2mig_{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    [Fact]
    public void Initialize_MigratesV2ToV3_KeepingInsertionOrder()
    {
        using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath};Pooling=False"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE notes (
                    id TEXT PRIMARY KEY, title TEXT NOT NULL, body TEXT NOT NULL, color_hex TEXT NOT NULL,
                    created_at TEXT NOT NULL, updated_at TEXT NOT NULL, archived_at TEXT NULL);
                INSERT INTO notes VALUES ('11111111-1111-1111-1111-111111111111', 'second', '', '#BDD9FF',
                    '2026-02-01T00:00:00.0000000+09:00', '2026-02-01T00:00:00.0000000+09:00', NULL);
                INSERT INTO notes VALUES ('22222222-2222-2222-2222-222222222222', 'first', '', '#BDD9FF',
                    '2026-01-01T00:00:00.0000000+09:00', '2026-01-01T00:00:00.0000000+09:00', NULL);
                PRAGMA user_version = 2;
                """;
            cmd.ExecuteNonQuery();
        }

        var repo = new NoteRepository(_dbPath);
        repo.Initialize();

        Assert.Equal(NoteRepository.SchemaVersion, repo.GetSchemaVersion());
        // v2까지의 화면 순서(생성 시각 순)를 그대로 초기 순서로 삼아야 함
        var notes = repo.LoadAll();
        Assert.Equal(["first", "second"], notes.Select(n => n.Title));
        Assert.Equal([0L, 1L], notes.Select(n => n.SortOrder));
    }
}
