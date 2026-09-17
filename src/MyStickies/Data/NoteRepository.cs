using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using MyStickies.Models;

namespace MyStickies.Data;

/// <summary>SQLite 파일 기반 노트 저장소</summary>
public sealed class NoteRepository
{
    /// <summary>현재 스키마 버전. PRAGMA user_version에 기록</summary>
    public const int SchemaVersion = 3;

    /// <summary>DB 파일 이름</summary>
    public const string DbFileName = "my_stickies.db";

    /// <summary>이전 버전의 DB 파일 이름. 발견되면 새 이름으로 바꿔 이어서 사용</summary>
    public const string LegacyDbFileName = "notes.db";

    private readonly string _connectionString;

    public string DbPath { get; }

    /// <summary>기본 DB 경로: %LocalAppData%\MyStickies\my_stickies.db</summary>
    public static string DefaultPath => PathFor(AppSettings.DefaultDataDirectory);

    /// <summary>지정 폴더 안의 DB 파일 경로</summary>
    public static string PathFor(string directory) => Path.Combine(directory, DbFileName);

    public NoteRepository(string dbPath)
    {
        DbPath = dbPath;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Pooling = false,
        }.ToString();
    }

    /// <summary>같은 폴더에 이전 이름(notes.db)의 파일만 있으면 새 이름으로 바꿈. 바꿨으면 true</summary>
    public static bool RenameLegacyFile(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (string.IsNullOrEmpty(dir) || File.Exists(dbPath)) return false;

        var legacy = Path.Combine(dir, LegacyDbFileName);
        if (!File.Exists(legacy)) return false;

        File.Move(legacy, dbPath);
        return true;
    }

    /// <summary>DB 파일과 테이블 생성. 이전 이름의 파일이 있으면 먼저 개명하고, 이미 있으면 스키마 버전에 따라 마이그레이션</summary>
    public void Initialize()
    {
        var dir = Path.GetDirectoryName(DbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        RenameLegacyFile(DbPath);

        using var conn = Open();
        Exec(conn, """
            CREATE TABLE IF NOT EXISTS notes (
                id          TEXT PRIMARY KEY,
                title       TEXT NOT NULL,
                body        TEXT NOT NULL,
                color_hex   TEXT NOT NULL,
                created_at  TEXT NOT NULL,
                updated_at  TEXT NOT NULL,
                archived_at TEXT NULL,
                sort_order  INTEGER NOT NULL DEFAULT 0
            );
            """);

        var version = ReadUserVersion(conn);
        if (version == 0 && !HasColumn(conn, "notes", "archived_at"))
        {
            // v1: archived_at 컬럼이 없던 초기 스키마
            Exec(conn, "ALTER TABLE notes ADD COLUMN archived_at TEXT NULL");
        }
        if (version < 3 && !HasColumn(conn, "notes", "sort_order"))
        {
            // v2 -> v3: 표시 순서 컬럼 추가. 기존 행은 그때까지의 화면 순서(생성 시각, rowid)를 초기값으로
            Exec(conn, "ALTER TABLE notes ADD COLUMN sort_order INTEGER NOT NULL DEFAULT 0");
            Exec(conn, """
                UPDATE notes SET sort_order = (
                    SELECT COUNT(*) FROM notes AS o
                    WHERE o.created_at < notes.created_at
                       OR (o.created_at = notes.created_at AND o.rowid < notes.rowid))
                """);
        }

        if (version != SchemaVersion)
            Exec(conn, $"PRAGMA user_version = {SchemaVersion}");
    }

    /// <summary>현재 DB 파일의 스키마 버전</summary>
    public int GetSchemaVersion()
    {
        using var conn = Open();
        return ReadUserVersion(conn);
    }

    /// <summary>표시 순서(sort_order) 오름차순으로 전체 노트 조회. 같으면 생성 시각 순</summary>
    public List<Note> LoadAll()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, title, body, color_hex, created_at, updated_at, archived_at, sort_order FROM notes ORDER BY sort_order, created_at, rowid";

        var notes = new List<Note>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            notes.Add(new Note
            {
                Id = Guid.Parse(reader.GetString(0)),
                Title = reader.GetString(1),
                Body = reader.GetString(2),
                ColorHex = reader.GetString(3),
                CreatedAt = ParseDate(reader.GetString(4)),
                UpdatedAt = ParseDate(reader.GetString(5)),
                ArchivedAt = reader.IsDBNull(6) ? null : ParseDate(reader.GetString(6)),
                SortOrder = reader.GetInt64(7),
            });
        }
        return notes;
    }

    public void Insert(Note note)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO notes (id, title, body, color_hex, created_at, updated_at, archived_at, sort_order)
            VALUES ($id, $title, $body, $color, $created, $updated, $archived, $order)
            """;
        BindAll(cmd, note);
        cmd.ExecuteNonQuery();
    }

    public void Update(Note note)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE notes
            SET title = $title, body = $body, color_hex = $color, updated_at = $updated, archived_at = $archived, sort_order = $order
            WHERE id = $id
            """;
        BindAll(cmd, note);
        cmd.ExecuteNonQuery();
    }

    /// <summary>여러 노트의 표시 순서를 한 트랜잭션으로 저장</summary>
    public void UpdateSortOrders(IEnumerable<Note> notes)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE notes SET sort_order = $order WHERE id = $id";
        var idParam = cmd.Parameters.Add("$id", SqliteType.Text);
        var orderParam = cmd.Parameters.Add("$order", SqliteType.Integer);
        foreach (var note in notes)
        {
            idParam.Value = note.Id.ToString();
            orderParam.Value = note.SortOrder;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public void Delete(Guid id)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM notes WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id.ToString());
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private static void BindAll(SqliteCommand cmd, Note note)
    {
        cmd.Parameters.AddWithValue("$id", note.Id.ToString());
        cmd.Parameters.AddWithValue("$title", note.Title);
        cmd.Parameters.AddWithValue("$body", note.Body);
        cmd.Parameters.AddWithValue("$color", note.ColorHex);
        cmd.Parameters.AddWithValue("$created", FormatDate(note.CreatedAt));
        cmd.Parameters.AddWithValue("$updated", FormatDate(note.UpdatedAt));
        cmd.Parameters.AddWithValue("$archived", note.ArchivedAt is { } archived ? FormatDate(archived) : DBNull.Value);
        cmd.Parameters.AddWithValue("$order", note.SortOrder);
    }

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static int ReadUserVersion(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static bool HasColumn(SqliteConnection conn, string table, string column)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table})";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static string FormatDate(DateTime value) =>
        value.ToString("o", CultureInfo.InvariantCulture);

    private static DateTime ParseDate(string text) =>
        DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
