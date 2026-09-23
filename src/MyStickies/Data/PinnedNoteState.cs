using MyStickies.Models;

namespace MyStickies.Data;

/// <summary>이 PC에서 고정한 메모의 DB 식별자, 창 위치와 사용자가 조절한 크기</summary>
public sealed record PinnedNoteState(string DatabasePath, Guid NoteId, double Left, double Top,
    double? Width = null, double? Height = null)
{
    public bool IsForDatabase(string path) =>
        string.Equals(DatabasePath, path, StringComparison.OrdinalIgnoreCase);

    /// <summary>현재 DB의 활성 메모만 복원하며, 중복과 잘못된 좌표는 제외함</summary>
    public static IEnumerable<PinnedNoteState> Restorable(
        IEnumerable<PinnedNoteState> states, string databasePath, IEnumerable<Note> notes)
    {
        var activeIds = notes.Where(n => !n.IsArchived).Select(n => n.Id).ToHashSet();
        return states.Where(s => s.IsForDatabase(databasePath) && activeIds.Contains(s.NoteId)
                                 && double.IsFinite(s.Left) && double.IsFinite(s.Top))
                     .DistinctBy(s => s.NoteId);
    }
}
